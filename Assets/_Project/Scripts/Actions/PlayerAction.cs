using ReAction.Core;
using UnityEngine;

namespace ReAction.Actions
{
    public enum ActionStatus
    {
        Pending,
        Running,
        Succeeded,
        Failed,
    }

    /// <summary>
    /// 실행 중인 행동 하나. 고정 틱마다 Tick이 호출되고, 스스로 성공/실패를 판정한다.
    /// </summary>
    public abstract class PlayerAction
    {
        protected PlayerAction(ActionDefinition definition, ActionRequest request)
        {
            Definition = definition;
            Request = request;
        }

        public ActionDefinition Definition { get; }
        public ActionRequest Request { get; }
        public ActionStatus Status { get; private set; } = ActionStatus.Pending;
        public string FailReason { get; private set; }
        public int ElapsedTicks { get; private set; }
        public bool IsDone => Status == ActionStatus.Succeeded || Status == ActionStatus.Failed;

        private int TimeoutTicks => DeterministicClock.SecondsToTicks(Definition.Timeout);

        public void Begin(IActorBody body)
        {
            Status = ActionStatus.Running;
            OnBegin(body);
        }

        public void Tick(IActorBody body)
        {
            if (IsDone) return;
            ElapsedTicks++;
            OnTick(body);
            if (!IsDone && TimeoutTicks > 0 && ElapsedTicks >= TimeoutTicks)
                Fail("시간 안에 끝나지 않았다");
        }

        /// <summary>행동이 끝났을 때 호출된다. 기본은 수평 이동을 멈춘다.</summary>
        public virtual void End(IActorBody body)
        {
            body.Velocity = new Vector2(0f, body.Velocity.y);
        }

        protected abstract void OnBegin(IActorBody body);
        protected abstract void OnTick(IActorBody body);

        protected void Succeed()
        {
            if (!IsDone) Status = ActionStatus.Succeeded;
        }

        protected void Fail(string reason)
        {
            if (IsDone) return;
            Status = ActionStatus.Failed;
            FailReason = reason;
        }

        /// <summary>방향이 없으면 바라보는 방향으로, 있으면 그쪽으로 돌아선 뒤 x 부호를 반환한다.</summary>
        protected int FaceRequestedDirection(IActorBody body)
        {
            int sign = Request.Direction.ToSign(body.Facing);
            if (sign == 0) sign = body.Facing;
            body.Facing = sign;
            return sign;
        }

        public override string ToString() => Request.ToString();
    }
}
