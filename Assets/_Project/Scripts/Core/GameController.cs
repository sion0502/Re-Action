using System;
using System.Collections.Generic;
using ReAction.Actions;
using ReAction.Player;
using UnityEngine;

namespace ReAction.Core
{
    /// <summary>
    /// 게임 루프의 중심. Planning 중에는 세계를 멈추고, Executing 중에만 고정 틱으로 행동과 물리를 진행한다.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        // 행동이 끝난 뒤 공중에 떠 있으면 착지할 때까지 시뮬레이션을 더 돌린다.
        private const int SettleGroundedTicks = 2;
        private const int MaxSettleTicks = 180;

        [SerializeField] private ActionCatalog _catalog;
        [SerializeField] private PlayerMotor _player;
        [SerializeField] private PlayerView _playerView;

        private readonly GameStateMachine _states = new GameStateMachine();
        private readonly DeterministicClock _clock = new DeterministicClock();
        private readonly ActionExecutor _executor = new ActionExecutor();

        private bool _settling;
        private int _settleTicks;
        private int _groundedTicks;

        public GameState State => _states.Current;
        public int Tick => _clock.Tick;
        public ActionCatalog Catalog => _catalog;
        public PlayerAction CurrentAction => _executor.Current;

        /// <summary>마지막 실행 결과를 사람이 읽을 수 있게 요약한 문자열.</summary>
        public string LastResult { get; private set; } = "";

        public event Action<GameState, GameState> StateChanged
        {
            add => _states.Changed += value;
            remove => _states.Changed -= value;
        }

        /// <summary>시뮬레이션 틱 하나가 끝날 때마다 (물리 진행 후). 인자는 틱 번호.</summary>
        public event Action<int> Ticked;

        private void Awake()
        {
            Physics2D.simulationMode = SimulationMode2D.Script;

            _states.Changed += (from, to) => PzLog.Info($"State {from} -> {to} (tick {_clock.Tick})");
            _executor.ActionStarted += OnActionStarted;
            _executor.ActionFinished += OnActionFinished;
            _executor.QueueFinished += OnQueueFinished;
        }

        private void Start()
        {
            _states.TryTransition(GameState.Planning);
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
            if (requests.Count > ActionExecutor.MaxActionsPerPrompt)
            {
                error = $"행동이 {requests.Count}개입니다. {ActionExecutor.MaxActionsPerPrompt}개 이하로 줄여주세요";
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

            if (!_executor.TryStart(actions))
            {
                error = "행동을 시작하지 못했다";
                return false;
            }

            LastResult = "";
            _settling = false;
            _clock.Pause();
            _states.TryTransition(GameState.Executing);
            PzLog.Info($"Execute [{string.Join(", ", requests)}]");
            return true;
        }

        private void Update()
        {
            if (_states.Current != GameState.Executing) return;

            int steps = _clock.Advance(Time.deltaTime);
            for (int i = 0; i < steps && _states.Current == GameState.Executing; i++)
                SimulateTick();
        }

        private void SimulateTick()
        {
            if (_executor.IsRunning) _executor.Tick(_player);

            Physics2D.Simulate(DeterministicClock.TickDuration);
            _clock.Step();
            Ticked?.Invoke(_clock.Tick);

            if (_settling) UpdateSettle();
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

        private void OnActionStarted(PlayerAction action, int index)
        {
            _playerView.PlayAction(action.Definition.AnimationState);
            PzLog.Info($"  [{index + 1}] {action} start (tick {_clock.Tick})");
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
            if (!allSucceeded) LastResult += "실패한 행동 뒤의 행동은 취소됨\n";
            _settling = true;
            _settleTicks = 0;
            _groundedTicks = 0;
        }
    }
}
