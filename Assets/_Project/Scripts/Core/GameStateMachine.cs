using System;
using System.Collections.Generic;

namespace ReAction.Core
{
    /// <summary>
    /// 게임 루프 상태 전이를 관리한다. 허용되지 않은 전이는 거부한다.
    /// </summary>
    public sealed class GameStateMachine
    {
        private static readonly Dictionary<GameState, GameState[]> Allowed = new Dictionary<GameState, GameState[]>
        {
            { GameState.Boot, new[] { GameState.Planning, GameState.Title } },
            // 메인 화면에서 게임을 시작하면 씬을 새로 불러온다 (메인 화면의 월드 상태가 게임에 섞이지 않게)
            { GameState.Title, new GameState[0] },
            // 계획 중에도 시간이 아주 느리게 흐르므로 그 사이에 죽을 수 있다
            { GameState.Planning, new[] { GameState.Interpreting, GameState.Executing, GameState.Dead } },
            { GameState.Interpreting, new[] { GameState.Planning, GameState.Executing } },
            { GameState.Executing, new[] { GameState.Planning, GameState.Dead, GameState.Cleared } },
            { GameState.Dead, new[] { GameState.Rewind } },
            { GameState.Rewind, new[] { GameState.Reset } },
            { GameState.Reset, new[] { GameState.Planning } },
            // 클리어 후 FINAL RUN(M6) 또는 처음부터 다시
            { GameState.Cleared, new[] { GameState.FinalRun, GameState.Rewind } },
            { GameState.FinalRun, new[] { GameState.Result } },
            { GameState.Result, new[] { GameState.Rewind } },
        };

        public GameState Current { get; private set; } = GameState.Boot;

        /// <summary>(이전 상태, 새 상태)</summary>
        public event Action<GameState, GameState> Changed;

        public bool CanTransition(GameState next) =>
            Allowed.TryGetValue(Current, out var targets) && Array.IndexOf(targets, next) >= 0;

        public bool TryTransition(GameState next)
        {
            if (!CanTransition(next)) return false;
            var previous = Current;
            Current = next;
            Changed?.Invoke(previous, next);
            return true;
        }
    }
}
