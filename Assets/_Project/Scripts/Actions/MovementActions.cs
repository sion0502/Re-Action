using ReAction.Core;
using UnityEngine;

namespace ReAction.Actions
{
    /// <summary>정해진 거리만큼 달린다. 벽에 막히면 실패.</summary>
    public sealed class RunAction : PlayerAction
    {
        private const int BlockedTicksToFail = 4;
        private const float MinProgressPerTick = 0.001f;

        private int _sign;
        private float _startX;
        private float _lastX;
        private int _blockedTicks;

        public RunAction(ActionDefinition definition, ActionRequest request) : base(definition, request) { }

        protected override void OnBegin(IActorBody body)
        {
            _sign = FaceRequestedDirection(body);
            _startX = _lastX = body.Position.x;
        }

        protected override void OnTick(IActorBody body)
        {
            float x = body.Position.x;
            if (Definition.Distance > 0f && Mathf.Abs(x - _startX) >= Definition.Distance)
            {
                Succeed();
                return;
            }

            // 첫 틱은 아직 움직이기 전이라 막힘 판정에서 뺀다.
            if (ElapsedTicks > 1 && Mathf.Abs(x - _lastX) < MinProgressPerTick)
            {
                if (++_blockedTicks >= BlockedTicksToFail)
                {
                    Fail("막혀서 더 갈 수 없다");
                    return;
                }
            }
            else
            {
                _blockedTicks = 0;
            }

            _lastX = x;
            body.Velocity = new Vector2(_sign * Definition.Speed, body.Velocity.y);
        }
    }

    /// <summary>제자리 또는 지정 방향으로 점프하고, 착지하면 끝난다.</summary>
    public sealed class JumpAction : PlayerAction
    {
        // 점프 직후 몇 틱은 아직 땅에 닿아 있다고 판정될 수 있어 착지 검사를 미룬다.
        private const int MinAirTicks = 6;

        private int _sign;

        public JumpAction(ActionDefinition definition, ActionRequest request) : base(definition, request) { }

        protected override void OnBegin(IActorBody body)
        {
            if (!body.IsGrounded)
            {
                Fail("공중에서는 점프할 수 없다");
                return;
            }

            _sign = Request.Direction.ToSign(body.Facing);
            if (_sign != 0) body.Facing = _sign;
            body.Velocity = new Vector2(_sign * Definition.Speed, Definition.VerticalSpeed);
        }

        protected override void OnTick(IActorBody body)
        {
            if (ElapsedTicks > MinAirTicks && body.IsGrounded)
            {
                Succeed();
                return;
            }
            body.Velocity = new Vector2(_sign * Definition.Speed, body.Velocity.y);
        }
    }

    /// <summary>정해진 시간 동안 빠르게 구른다.</summary>
    public sealed class RollAction : PlayerAction
    {
        private int _sign;

        public RollAction(ActionDefinition definition, ActionRequest request) : base(definition, request) { }

        protected override void OnBegin(IActorBody body)
        {
            _sign = FaceRequestedDirection(body);
        }

        protected override void OnTick(IActorBody body)
        {
            if (ElapsedTicks > DeterministicClock.SecondsToTicks(Definition.Duration))
            {
                Succeed();
                return;
            }
            body.Velocity = new Vector2(_sign * Definition.Speed, body.Velocity.y);
        }
    }

    /// <summary>
    /// 방향만 바꾼다 (즉시 완료, 행동 수에 세지 않음). 방향이 없거나 "뒤"면 돌아선다.
    /// </summary>
    public sealed class TurnAction : PlayerAction
    {
        public TurnAction(ActionDefinition definition, ActionRequest request) : base(definition, request) { }

        protected override void OnBegin(IActorBody body)
        {
            int sign = Request.Direction == Direction.None ? -body.Facing : Request.Direction.ToSign(body.Facing);
            body.Facing = sign;
            Succeed();
        }

        protected override void OnTick(IActorBody body) { }
    }

    /// <summary>정해진 시간 동안 제자리에서 기다린다.</summary>
    public sealed class WaitAction : PlayerAction
    {
        public WaitAction(ActionDefinition definition, ActionRequest request) : base(definition, request) { }

        protected override void OnBegin(IActorBody body)
        {
            body.Velocity = new Vector2(0f, body.Velocity.y);
        }

        protected override void OnTick(IActorBody body)
        {
            if (ElapsedTicks >= DeterministicClock.SecondsToTicks(Definition.Duration))
                Succeed();
        }
    }
}
