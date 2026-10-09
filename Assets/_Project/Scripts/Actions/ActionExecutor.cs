using System;
using System.Collections.Generic;

namespace ReAction.Actions
{
    /// <summary>
    /// 검증된 행동 1~3개를 순서대로 실행한다. 행동 하나가 실패하면 남은 행동은 취소한다.
    /// </summary>
    public sealed class ActionExecutor
    {
        public const int MaxActionsPerPrompt = 3;

        private readonly Queue<PlayerAction> _pending = new Queue<PlayerAction>();
        private int _index;
        private bool _anyFailed;

        public PlayerAction Current { get; private set; }
        public bool IsRunning => Current != null || _pending.Count > 0;

        /// <summary>마지막 실행에서 앞 행동 실패로 취소된 행동 수.</summary>
        public int CancelledCount { get; private set; }

        /// <summary>(행동, 순번 0부터)</summary>
        public event Action<PlayerAction, int> ActionStarted;
        public event Action<PlayerAction, int> ActionFinished;
        /// <summary>true면 모든 행동이 성공.</summary>
        public event Action<bool> QueueFinished;

        public bool TryStart(IReadOnlyList<PlayerAction> actions)
        {
            if (IsRunning || actions == null || actions.Count == 0 || actions.Count > MaxActionsPerPrompt)
                return false;

            foreach (var a in actions)
            {
                if (a == null) return false;
            }

            foreach (var a in actions) _pending.Enqueue(a);
            _index = -1;
            _anyFailed = false;
            CancelledCount = 0;
            return true;
        }

        /// <summary>고정 틱마다 한 번 호출한다.</summary>
        public void Tick(IActorBody body)
        {
            if (Current == null && !BeginNext(body)) return;

            Current.Tick(body);
            if (Current.IsDone) FinishCurrent(body);
        }

        /// <summary>사망 등으로 실행을 즉시 중단한다. QueueFinished는 발생하지 않는다.</summary>
        public void Cancel()
        {
            _pending.Clear();
            Current = null;
        }

        private bool BeginNext(IActorBody body)
        {
            while (_pending.Count > 0)
            {
                Current = _pending.Dequeue();
                _index++;
                Current.Begin(body);
                ActionStarted?.Invoke(Current, _index);

                // 시작 조건이 안 맞아 바로 실패한 경우 (예: 공중에서 점프)
                if (!Current.IsDone) return true;
                FinishCurrent(body);
            }
            return false;
        }

        private void FinishCurrent(IActorBody body)
        {
            var finished = Current;
            Current = null;
            finished.End(body);
            ActionFinished?.Invoke(finished, _index);

            if (finished.Status == ActionStatus.Failed)
            {
                _anyFailed = true;
                CancelledCount = _pending.Count;
                _pending.Clear();
            }

            if (_pending.Count == 0) QueueFinished?.Invoke(!_anyFailed);
        }
    }
}
