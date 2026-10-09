using System;
using System.Collections.Generic;
using System.Linq;
using ReAction.Actions;
using ReAction.Enemies;
using ReAction.Player;
using ReAction.Replay;
using UnityEngine;

namespace ReAction.Core
{
    /// <summary>
    /// 게임 루프의 중심. 월드는 항상 고정 틱으로만 진행한다: Executing은 정상 속도, Planning은 아주 느리게
    /// (_planningTimeScale), Interpreting과 그 밖의 상태는 정지.
    /// 플레이어가 죽으면 Dead(다시 시작 버튼 대기) → Rewind(되감기 연출) → Reset(월드 초기화, 발견 정보는 유지) → Planning.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        // 행동이 끝난 뒤 공중에 떠 있으면 착지할 때까지 시뮬레이션을 더 돌린다.
        private const int SettleGroundedTicks = 2;
        private const int MaxSettleTicks = 180;

        [SerializeField] private ActionCatalog _catalog;
        [SerializeField] private PlayerMotor _player;
        [SerializeField] private PlayerView _playerView;
        [SerializeField] private List<MeleeEnemy> _enemies = new List<MeleeEnemy>();
        [SerializeField] private RewindPlayer _rewindPlayer;

        [Tooltip("계획(프롬프트 입력) 중 시간 배율. 0이면 완전 정지")]
        [SerializeField, Range(0f, 0.2f)] private float _planningTimeScale = 0.01f;

        [Tooltip("사망 후 다시 시작 버튼을 누를 수 있게 되기까지의 시간 (초, 사망 연출이 보이도록)")]
        [SerializeField] private float _deathPauseSeconds = 0.6f;

        private readonly GameStateMachine _states = new GameStateMachine();
        private readonly DeterministicClock _clock = new DeterministicClock();
        private readonly WorldResetter _resetter = new WorldResetter();
        private readonly RewindRecorder _recorder = new RewindRecorder();
        private readonly RunRecord _runRecord = new RunRecord();
        private WorldSimulation _sim;

        private bool _settling;
        private int _settleTicks;
        private int _groundedTicks;
        private float _deadTimer;

        public GameState State => _states.Current;
        public int Tick => _sim?.Tick ?? 0;
        public ActionCatalog Catalog => _catalog;
        public PlayerAction CurrentAction => _sim?.Executor.Current;

        /// <summary>이번 시도에서 실행한 프롬프트 기록.</summary>
        public RunRecord RunRecord => _runRecord;

        /// <summary>지금 월드 시간이 흐르는 배율 (연출용). 실행 1, 계획 중 아주 느리게, 그 밖은 0.</summary>
        public float WorldTimeScale
        {
            get
            {
                switch (_states.Current)
                {
                    case GameState.Executing: return 1f;
                    case GameState.Planning: return _planningTimeScale;
                    default: return 0f;
                }
            }
        }

        /// <summary>몇 번째 시도인지 (1부터).</summary>
        public int Attempt { get; private set; } = 1;

        /// <summary>마지막 사망 원인 (리셋 후에도 다음 사망 전까지 유지).</summary>
        public string LastDeathCause { get; private set; } = "";

        /// <summary>사망 연출이 끝나 다시 시작할 수 있는 상태인지.</summary>
        public bool CanRestart => _states.Current == GameState.Dead && _deadTimer <= 0f;

        /// <summary>마지막 실행 결과를 사람이 읽을 수 있게 요약한 문자열.</summary>
        public string LastResult { get; private set; } = "";

        /// <summary>마지막 실행에서 실패한 행동이 있었는지.</summary>
        public bool LastRunFailed { get; private set; }

        public event Action<GameState, GameState> StateChanged
        {
            add => _states.Changed += value;
            remove => _states.Changed -= value;
        }

        /// <summary>시뮬레이션 틱 하나가 끝날 때마다 (물리 진행 후). 인자는 틱 번호.</summary>
        public event Action<int> Ticked;

        /// <summary>사망 후 월드가 초기 상태로 돌아갔을 때.</summary>
        public event Action WorldReset;

        private void Awake()
        {
            Physics2D.simulationMode = SimulationMode2D.Script;

            _sim = new WorldSimulation(_player);
            _sim.Ticked += OnSimTicked;
            _sim.Executor.ActionStarted += OnActionStarted;
            _sim.Executor.ActionFinished += OnActionFinished;
            _sim.Executor.QueueFinished += OnQueueFinished;

            _resetter.Register(_player);
            AddRewindTrack(_player.transform);
            foreach (var enemy in _enemies)
            {
                if (enemy == null) continue;
                _sim.Register(enemy);
                _resetter.Register(enemy);
                AddRewindTrack(enemy.transform);
            }

            _states.Changed += (from, to) =>
            {
                // 상태가 바뀌면 시간 배율이 달라지므로 남은 누적 시간을 버린다.
                _clock.Pause();
                PzLog.Info($"State {from} -> {to} (tick {Tick}, attempt {Attempt})");
            };
        }

        private void Start()
        {
            _resetter.CaptureAll();
            _recorder.Record();
            _states.TryTransition(GameState.Planning);
        }

        /// <summary>Planning → Interpreting. 해석 중에는 다른 입력을 받지 않는다.</summary>
        public bool TryBeginInterpreting() => _states.TryTransition(GameState.Interpreting);

        /// <summary>Interpreting → Planning (해석 실패 등).</summary>
        public void EndInterpreting()
        {
            if (_states.Current == GameState.Interpreting) _states.TryTransition(GameState.Planning);
        }

        /// <summary>
        /// Planning 중에 행동 목록을 실행한다. 실패하면 false와 플레이어에게 보여줄 이유를 돌려준다.
        /// 실패한 요청은 세계를 전혀 바꾸지 않는다.
        /// </summary>
        public bool TryExecute(IReadOnlyList<ActionRequest> requests, out string error,
            string prompt = null, string targetZoneId = null)
        {
            error = null;
            if (_states.Current != GameState.Planning)
            {
                error = "지금은 행동을 입력할 수 없다";
                return false;
            }
            if (requests == null || requests.Count == 0)
            {
                error = "행동을 하나 이상 입력하세요";
                return false;
            }
            int counted = ActionRules.CountTowardLimit(requests.Select(r => r.Type));
            if (counted > ActionExecutor.MaxActionsPerPrompt)
            {
                error = $"행동이 {counted}개입니다. {ActionExecutor.MaxActionsPerPrompt}개 이하로 줄여주세요";
                return false;
            }

            var actions = new List<PlayerAction>(requests.Count);
            foreach (var request in requests)
            {
                var action = ActionFactory.Create(request, _catalog);
                if (action == null)
                {
                    error = $"아직 실행할 수 없는 행동: {request.Type}";
                    return false;
                }
                actions.Add(action);
            }

            if (!_sim.Executor.TryStart(actions))
            {
                error = "행동을 시작하지 못했다";
                return false;
            }

            _runRecord.Add(new PlanRecord(Tick, prompt, targetZoneId, requests));
            LastResult = "";
            LastRunFailed = false;
            _settling = false;
            _states.TryTransition(GameState.Executing);
            PzLog.Info($"Execute at tick {Tick} [{string.Join(", ", requests)}]");
            return true;
        }

        /// <summary>
        /// 사망 후 "다시 시작". 되감기 연출 → 월드 리셋 → Planning. 사망 연출 중에는 무시한다.
        /// </summary>
        public bool RequestRestart()
        {
            if (!CanRestart || !_states.TryTransition(GameState.Rewind)) return false;
            _rewindPlayer.Play(_recorder, OnRewindFinished);
            return true;
        }

        /// <summary>되감기 연출을 즉시 끝낸다 (검증용).</summary>
        public void SkipRewind() => _rewindPlayer.Skip();

        private void Update()
        {
            switch (_states.Current)
            {
                case GameState.Executing:
                case GameState.Planning:
                    var state = _states.Current;
                    int steps = _clock.Advance(Time.deltaTime * WorldTimeScale);
                    for (int i = 0; i < steps && _states.Current == state; i++)
                        SimulateTick();
                    break;

                case GameState.Dead:
                    // 자동으로 넘어가지 않는다. 연출 시간이 지나면 다시 시작 버튼이 활성화된다.
                    if (_deadTimer > 0f) _deadTimer -= Time.deltaTime;
                    break;
            }
        }

        private void SimulateTick()
        {
            _sim.Step();

            if (_player.IsDead)
            {
                OnPlayerDied();
                return;
            }
            if (_settling) UpdateSettle();
        }

        private void OnSimTicked(int tick)
        {
            _recorder.Record();
            Ticked?.Invoke(tick);
        }

        private void UpdateSettle()
        {
            _settleTicks++;
            _groundedTicks = _player.IsGrounded ? _groundedTicks + 1 : 0;
            if (_groundedTicks < SettleGroundedTicks && _settleTicks < MaxSettleTicks) return;

            _settling = false;
            _player.Velocity = Vector2.zero;
            _states.TryTransition(GameState.Planning);
        }

        private void OnPlayerDied()
        {
            _sim.Executor.Cancel();
            _playerView.ClearAction();
            _settling = false;
            _deadTimer = _deathPauseSeconds;
            LastDeathCause = _player.DeathCause;
            LastResult += $"죽었다: {_player.DeathCause}\n";
            _states.TryTransition(GameState.Dead);
        }

        private void OnRewindFinished()
        {
            _states.TryTransition(GameState.Reset);

            // 월드 상태만 초기화한다. 발견한 구역(ZoneKnowledge)은 플레이어 지식이라 건드리지 않는다.
            _resetter.ResetAll();
            _sim.Executor.Cancel();
            _sim.ResetTicks();
            _recorder.Clear();
            _recorder.Record();
            _runRecord.Clear();
            Attempt++;
            WorldReset?.Invoke();

            _states.TryTransition(GameState.Planning);
        }

        private void AddRewindTrack(Transform root)
        {
            var renderer = root.GetComponentInChildren<SpriteRenderer>();
            var animator = root.GetComponentInChildren<Animator>();
            if (renderer != null && animator != null) _recorder.AddTrack(new RewindTrack(root, renderer, animator));
        }

        private void OnActionStarted(PlayerAction action, int index)
        {
            _playerView.PlayAction(action.Definition.AnimationState);
            PzLog.Info($"  [{index + 1}] {action} start (tick {Tick})");
        }

        private void OnActionFinished(PlayerAction action, int index)
        {
            _playerView.ClearAction();
            string result = action.Status == ActionStatus.Succeeded ? "OK" : $"FAIL: {action.FailReason}";
            PzLog.Info($"  [{index + 1}] {action} {result} ({action.ElapsedTicks} ticks, pos {_player.Position})");
            LastResult += $"[{index + 1}] {action.Definition.DisplayName} {(action.Status == ActionStatus.Succeeded ? "성공" : "실패 - " + action.FailReason)}\n";
        }

        private void OnQueueFinished(bool allSucceeded)
        {
            LastRunFailed = !allSucceeded;
            if (_sim.Executor.CancelledCount > 0) LastResult += $"남은 행동 {_sim.Executor.CancelledCount}개는 취소됨\n";
            _settling = true;
            _settleTicks = 0;
            _groundedTicks = 0;
        }
    }
}
