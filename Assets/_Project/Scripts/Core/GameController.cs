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
    /// 게임 루프의 중심. Planning 중에는 세계를 멈추고, Executing 중에만 고정 틱으로 월드를 진행한다.
    /// 플레이어가 죽으면 Dead → Rewind(되감기 연출) → Reset(월드 초기화, 발견 정보는 유지) → Planning.
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

        [Tooltip("사망 후 되감기 시작까지 멈춰 있는 시간 (초, 연출)")]
        [SerializeField] private float _deathPauseSeconds = 0.6f;

        private readonly GameStateMachine _states = new GameStateMachine();
        private readonly DeterministicClock _clock = new DeterministicClock();
        private readonly WorldResetter _resetter = new WorldResetter();
        private readonly RewindRecorder _recorder = new RewindRecorder();
        private WorldSimulation _sim;

        private bool _settling;
        private int _settleTicks;
        private int _groundedTicks;
        private float _deadTimer;

        public GameState State => _states.Current;
        public int Tick => _sim?.Tick ?? 0;
        public ActionCatalog Catalog => _catalog;
        public PlayerAction CurrentAction => _sim?.Executor.Current;

        /// <summary>몇 번째 시도인지 (1부터).</summary>
        public int Attempt { get; private set; } = 1;

        /// <summary>마지막 사망 원인 (리셋 후에도 다음 사망 전까지 유지).</summary>
        public string LastDeathCause { get; private set; } = "";

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

            _states.Changed += (from, to) => PzLog.Info($"State {from} -> {to} (tick {Tick}, attempt {Attempt})");
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
        public bool TryExecute(IReadOnlyList<ActionRequest> requests, out string error)
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

            LastResult = "";
            LastRunFailed = false;
            _settling = false;
            _clock.Pause();
            _states.TryTransition(GameState.Executing);
            PzLog.Info($"Execute [{string.Join(", ", requests)}]");
            return true;
        }

        /// <summary>되감기 연출을 즉시 끝낸다 (검증용).</summary>
        public void SkipRewind() => _rewindPlayer.Skip();

        private void Update()
        {
            switch (_states.Current)
            {
                case GameState.Executing:
                    int steps = _clock.Advance(Time.deltaTime);
                    for (int i = 0; i < steps && _states.Current == GameState.Executing; i++)
                        SimulateTick();
                    break;

                case GameState.Dead:
                    _deadTimer -= Time.deltaTime;
                    if (_deadTimer <= 0f && _states.TryTransition(GameState.Rewind))
                        _rewindPlayer.Play(_recorder, OnRewindFinished);
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
            _clock.Pause();
            _states.TryTransition(GameState.Planning);
        }

        private void OnPlayerDied()
        {
            _sim.Executor.Cancel();
            _playerView.ClearAction();
            _settling = false;
            _clock.Pause();
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
