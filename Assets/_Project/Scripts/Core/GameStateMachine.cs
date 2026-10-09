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
            { GameState.Boot, new[] { GameState.Planning } },
            { GameState.Planning, new[] { GameState.Interpreting, GameState.Executing } },
            { GameState.Interpreting, new[] { GameState.Planning, GameState.Executing } },
            { GameState.Executing, new[] { GameState.Planning, GameState.Dead, GameState.Cleared } },
            { GameState.Dead, new[] { GameState.Rewind } },
            { GameState.Rewind, new[] { GameState.Reset } },
            { GameState.Reset, new[] { GameState.Planning } },
            { GameState.Cleared, new[] { GameState.FinalRun } },
            { GameState.FinalRun, new[] { GameState.Planning } },
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
