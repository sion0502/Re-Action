using ReAction.Core;
using ReAction.Stage;
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

    public enum MoveResult
    {
        Moving,
        Arrived,
        Blocked,
    }

    /// <summary>
    /// 실행 중인 행동 하나. 고정 틱마다 Tick이 호출되고, 스스로 성공/실패를 판정한다.
    /// </summary>
    public abstract class PlayerAction
    {
        private const int BlockedTicksToFail = 4;
        private const float MinProgressPerTick = 0.001f;
        private const float ArriveTolerance = 0.06f;

        private float _lastX = float.NaN;
        private int _blockedTicks;

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

        /// <summary>문·창문·구역 조회. 없으면(테스트 등) 월드 오브젝트가 필요한 행동은 실패한다.</summary>
        public IStageWorld World { get; set; }

        /// <summary>
        /// 지금 재생할 애니메이션 상태. null이면 이동 상태(Idle/Run/Jump/Fall) 자동 선택.
        /// 다가가는 단계와 실제 동작 단계가 있는 행동은 단계마다 바꾼다.
        /// </summary>
        public virtual string AnimationState =>
            string.IsNullOrEmpty(Definition.AnimationState) ? null : Definition.AnimationState;

        /// <summary>외벽에 매달린 채로 할 수 있는 행동인지. 아니면 시작할 때 손을 놓는다.</summary>
        protected virtual bool KeepsClinging => false;

        protected virtual int TimeoutTicks => DeterministicClock.SecondsToTicks(Definition.Timeout);

        public void Begin(IActorBody body)
        {
            Status = ActionStatus.Running;
            if (body.IsClinging && !KeepsClinging) ReleaseCling(body);
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

        /// <summary>
        /// 목표 x까지 수평으로 이동한다. 목표를 지나치지 않게 마지막 틱 속도를 줄인다.
        /// 몇 틱 동안 전혀 못 움직이면 Blocked.
        /// </summary>
        protected MoveResult MoveTowardX(IActorBody body, float targetX, float speed)
        {
            float x = body.Position.x;
            float dx = targetX - x;
            if (Mathf.Abs(dx) <= ArriveTolerance)
            {
                body.Velocity = new Vector2(0f, body.Velocity.y);
                return MoveResult.Arrived;
            }

            if (IsStuck(x))
            {
                body.Velocity = new Vector2(0f, body.Velocity.y);
                return MoveResult.Blocked;
            }

            int sign = dx > 0f ? 1 : -1;
            body.Facing = sign;
            float maxStep = Mathf.Abs(dx) / DeterministicClock.TickDuration;
            body.Velocity = new Vector2(sign * Mathf.Min(speed, maxStep), body.Velocity.y);
            return MoveResult.Moving;
        }

        /// <summary>지난 틱과 비교해 x가 거의 그대로면 막힘 카운트를 올린다.</summary>
        protected bool IsStuck(float x)
        {
            bool stuck = false;
            if (!float.IsNaN(_lastX) && Mathf.Abs(x - _lastX) < MinProgressPerTick)
                stuck = ++_blockedTicks >= BlockedTicksToFail;
            else
                _blockedTicks = 0;
            _lastX = x;
            return stuck;
        }

        protected void ResetStuck()
        {
            _lastX = float.NaN;
            _blockedTicks = 0;
        }

        protected static void ReleaseCling(IActorBody body)
        {
            body.IsClinging = false;
            body.GravityEnabled = true;
        }

        public override string ToString() => Request.ToString();
    }
}
