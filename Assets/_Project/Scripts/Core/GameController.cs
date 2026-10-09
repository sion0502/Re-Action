using System;
using System.Collections.Generic;
using System.Linq;
using ReAction.Actions;
using ReAction.Enemies;
using ReAction.Player;
using ReAction.Replay;
using ReAction.Stage;
using UnityEngine;

namespace ReAction.Core
{
    /// <summary>
    /// 게임 루프의 중심. 월드는 항상 고정 틱으로만 진행한다: Executing은 정상 속도, Planning은 아주 느리게
    /// (_planningTimeScale), Interpreting과 그 밖의 상태는 정지.
    /// 플레이어가 죽으면 Dead(다시 시작 버튼 대기) → Rewind(되감기 연출) → Reset(월드 초기화, 발견 정보는 유지) → Planning.
    /// 옥상에 닿으면 Cleared(잠깐) → FinalRun(이번 시도를 멈춤 없이 재생) → Result.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        // 행동이 끝난 뒤 공중에 떠 있으면 착지할 때까지 시뮬레이션을 더 돌린다.
        private const int SettleGroundedTicks = 2;
        private const int MaxSettleTicks = 180;

        [SerializeField] private ActionCatalog _catalog;
        [SerializeField] private PlayerMotor _player;
        [SerializeField] private PlayerView _playerView;
        [SerializeField] private StageController _stage;
        [SerializeField] private List<MeleeEnemy> _enemies = new List<MeleeEnemy>();
        [SerializeField] private RewindPlayer _rewindPlayer;

        [Tooltip("계획(프롬프트 입력) 중 시간 배율. 0이면 완전 정지")]
        [SerializeField, Range(0f, 0.2f)] private float _planningTimeScale = 0.01f;

        [Tooltip("클리어 후 FINAL RUN이 시작되기까지 \"CLEAR\"를 보여주는 시간 (초)")]
        [SerializeField] private float _clearPauseSeconds = 1.2f;

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
        private float _clearTimer;
        private RunRecord _clearedRecord;
        private RunReplayer _replayer;
        private int _clearTick;

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

        /// <summary>지금까지 실행한 프롬프트 수 (모든 시도 합계).</summary>
        public int TotalPrompts { get; private set; }

        /// <summary>몇 번째 시도인지 (1부터).</summary>
        public int Attempt { get; private set; } = 1;

        /// <summary>마지막 사망 원인 (리셋 후에도 다음 사망 전까지 유지).</summary>
        public string LastDeathCause { get; private set; } = "";

        /// <summary>사망 연출이 끝나 다시 시작할 수 있는 상태인지 (결과 화면에서도 가능).</summary>
        public bool CanRestart =>
            (_states.Current == GameState.Dead && _deadTimer <= 0f) || _states.Current == GameState.Result;

        /// <summary>클리어한 시도의 프롬프트 수.</summary>
        public int ClearPrompts => _clearedRecord?.Plans.Count ?? 0;

        /// <summary>클리어한 시도의 행동 수 (방향 전환 같은 무료 지시 제외).</summary>
        public int ClearActions { get; private set; }

        /// <summary>클리어까지 걸린 월드 시간 (초).</summary>
        public float ClearSeconds { get; private set; }

        /// <summary>FINAL RUN이 원래 시도와 다르게 흘러갔는지 (결정론이 깨졌다는 신호).</summary>
        public bool FinalRunDesynced { get; private set; }

        /// <summary>FINAL RUN에서 지금 재생 중인 프롬프트 (자막용).</summary>
        public string ReplayPrompt => _replayer?.Current?.Prompt ?? "";

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
            // 문·창문은 StageController.Awake에서 모이므로 Start에서 등록한다.
            if (_stage != null)
            {
                foreach (var resettable in _stage.Resettables) _resetter.Register(resettable);
            }
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
                var action = ActionFactory.Create(request, _catalog, _stage);
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
            TotalPrompts++;
            LastResult = "";
            LastRunFailed = false;
            _settling = false;
            _states.TryTransition(GameState.Executing);
            PzLog.Info($"Execute at tick {Tick} [{string.Join(", ", requests)}]");
            return true;
        }

        /// <summary>목표(옥상) 도달. 실행 중이면 클리어, FINAL RUN 중이면 재생 완료.</summary>
        public void NotifyCleared()
        {
            if (_states.Current == GameState.FinalRun)
            {
                // 원래 시도와 같은 틱에 옥상에 닿아야 정확히 재현된 것이다.
                FinishFinalRun(desynced: Tick != _clearTick);
                return;
            }
            if (_states.Current != GameState.Executing) return;

            _sim.Executor.Cancel();
            _settling = false;
            _clearedRecord = new RunRecord();
            foreach (var plan in _runRecord.Plans) _clearedRecord.Add(plan);
            ClearActions = _clearedRecord.Plans.Sum(p => ActionRules.CountTowardLimit(p.Actions.Select(a => a.Type)));
            _clearTick = Tick;
            ClearSeconds = Tick / (float)DeterministicClock.TicksPerSecond;
            _clearTimer = _clearPauseSeconds;
            PzLog.Info($"Cleared! attempt {Attempt}, prompts {TotalPrompts}, tick {Tick}");
            _states.TryTransition(GameState.Cleared);
        }

        /// <summary>FINAL RUN을 건너뛰고 결과 화면으로.</summary>
        public void SkipFinalRun()
        {
            if (_states.Current == GameState.FinalRun) FinishFinalRun(desynced: false);
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

                case GameState.Cleared:
                    _clearTimer -= Time.deltaTime;
                    if (_clearTimer <= 0f) StartFinalRun();
                    break;

                case GameState.FinalRun:
                    int replaySteps = _clock.Advance(Time.deltaTime);
                    for (int i = 0; i < replaySteps && _states.Current == GameState.FinalRun; i++)
                        ReplayTick();
                    break;
            }
        }

        private void StartFinalRun()
        {
            if (!_states.TryTransition(GameState.FinalRun)) return;

            // 월드만 처음 상태로 돌리고 (시도 횟수는 그대로), 기록된 프롬프트를 같은 틱에 다시 넣는다.
            _resetter.ResetAll();
            _sim.Executor.Cancel();
            _sim.ResetTicks();
            _recorder.Clear();
            _recorder.Record();
            FinalRunDesynced = false;
            _replayer = new RunReplayer(_clearedRecord, request => ActionFactory.Create(request, _catalog, _stage));
            WorldReset?.Invoke();
            PzLog.Info($"FINAL RUN start ({_clearedRecord.Plans.Count} prompts)");
        }

        private void ReplayTick()
        {
            // 마지막 프롬프트 뒤로 너무 오래 끝나지 않으면 재현이 어긋난 것으로 본다.
            const int maxTicksAfterLastPlan = 60 * 20;
            if (!_replayer.BeforeStep(_sim) || Tick > _replayer.LastPlanTick + maxTicksAfterLastPlan)
            {
                FinishFinalRun(desynced: true);
                return;
            }

            _sim.Step();
            if (_player.IsDead) FinishFinalRun(desynced: true);
        }

        private void FinishFinalRun(bool desynced)
        {
            FinalRunDesynced = desynced;
            _sim.Executor.Cancel();
            if (desynced) PzLog.Warn($"FINAL RUN diverged from the original run at tick {Tick}");
            else PzLog.Info($"FINAL RUN finished at tick {Tick}");
            _states.TryTransition(GameState.Result);
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
            // 외벽에 매달린 상태도 안정 상태로 본다 (다음 프롬프트에서 창문을 깨는 등).
            bool stable = _player.IsGrounded || _player.IsClinging;
            _groundedTicks = stable ? _groundedTicks + 1 : 0;
            if (_groundedTicks < SettleGroundedTicks && _settleTicks < MaxSettleTicks) return;

            _settling = false;
            // 여기서 속도 등 월드 상태를 건드리면 FINAL RUN 재현이 어긋난다. 틱 안에서만 바꾼다.
            _states.TryTransition(GameState.Planning);
        }

        private void OnPlayerDied()
        {
            _sim.Executor.Cancel();
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
            PzLog.Info($"  [{index + 1}] {action} start (tick {Tick})");
        }

        private void OnActionFinished(PlayerAction action, int index)
        {
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
