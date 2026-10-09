using ReAction.Core;
using ReAction.Stage;
using UnityEngine;

namespace ReAction.Actions
{
    /// <summary>
    /// 달리기·걷기·전력 질주 (속도만 다르고 정의 에셋으로 구분). 끝나는 조건:
    /// "N초 동안" → 시간, "N칸" → 거리, "벽까지" → 막힐 때까지(성공), 목표 구역 클릭 → 그 구역 중앙, 그 외 → 기본 거리.
    /// 목표·벽까지가 아닌데 막히면 실패.
    /// </summary>
    public sealed class RunAction : PlayerAction
    {
        private const float TimeoutMargin = 1f;

        private int _sign;
        private float _startX;
        private float? _targetX;

        public RunAction(ActionDefinition definition, ActionRequest request) : base(definition, request) { }

        protected override int TimeoutTicks
        {
            get
            {
                float seconds = Definition.Timeout;
                if (Request.Unit == AmountUnit.Seconds) seconds = Mathf.Max(seconds, Request.Amount + TimeoutMargin);
                if (Request.Unit == AmountUnit.Units) seconds = Mathf.Max(seconds, Request.Amount / Definition.Speed + TimeoutMargin);
                if (Request.Unit == AmountUnit.UntilBlocked || _targetX.HasValue) seconds = Mathf.Max(seconds, 6f);
                return DeterministicClock.SecondsToTicks(seconds);
            }
        }

        protected override void OnBegin(IActorBody body)
        {
            _startX = body.Position.x;

            // 방향 없이 목표 구역만 찍었으면 그 구역 중앙으로 간다.
            var zone = World?.GetZone(Request.Target);
            if (zone != null && Request.Direction == Direction.None && !Request.HasAmount)
            {
                _targetX = zone.Bounds.center.x;
                return;
            }
            _sign = FaceRequestedDirection(body);
        }

        protected override void OnTick(IActorBody body)
        {
            if (_targetX.HasValue)
            {
                var result = MoveTowardX(body, _targetX.Value, Definition.Speed);
                if (result == MoveResult.Arrived) Succeed();
                else if (result == MoveResult.Blocked) Fail("막혀서 더 갈 수 없다");
                return;
            }

            float x = body.Position.x;
            switch (Request.Unit)
            {
                case AmountUnit.Seconds:
                    if (ElapsedTicks > DeterministicClock.SecondsToTicks(Request.Amount))
                    {
                        Succeed();
                        return;
                    }
                    break;
                case AmountUnit.Units:
                    if (Mathf.Abs(x - _startX) >= Request.Amount)
                    {
                        Succeed();
                        return;
                    }
                    break;
                case AmountUnit.UntilBlocked:
                    break;
                default:
                    if (Definition.Distance > 0f && Mathf.Abs(x - _startX) >= Definition.Distance)
                    {
                        Succeed();
                        return;
                    }
                    break;
            }

            // 첫 틱은 아직 움직이기 전이라 막힘 판정에서 뺀다.
            if (ElapsedTicks > 1 && IsStuck(x))
            {
                if (Request.Unit == AmountUnit.UntilBlocked) Succeed();
                else Fail("막혀서 더 갈 수 없다");
                body.Velocity = new Vector2(0f, body.Velocity.y);
                return;
            }

            body.Facing = _sign;
            body.Velocity = new Vector2(_sign * Definition.Speed, body.Velocity.y);
        }
    }

    /// <summary>바라보는 방향은 그대로 두고 뒤로 물러난다.</summary>
    public sealed class BackstepAction : PlayerAction
    {
        private int _sign;
        private float _startX;

        public BackstepAction(ActionDefinition definition, ActionRequest request) : base(definition, request) { }

        private float Distance => Request.Unit == AmountUnit.Units ? Request.Amount : Definition.Distance;

        protected override void OnBegin(IActorBody body)
        {
            // "왼쪽으로 물러나" 처럼 방향을 주면 그쪽으로, 아니면 바라보는 반대쪽으로
            int requested = Request.Direction.ToSign(body.Facing);
            _sign = requested != 0 && Request.Direction != Direction.Backward ? requested : -body.Facing;
            _startX = body.Position.x;
        }

        protected override void OnTick(IActorBody body)
        {
            float x = body.Position.x;
            if (Mathf.Abs(x - _startX) >= Distance)
            {
                Succeed();
                return;
            }
            if (ElapsedTicks > 1 && IsStuck(x))
            {
                Succeed(); // 벽에 붙으면 거기서 멈춘다
                return;
            }
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

    /// <summary>정해진 시간 동안 빠르게 구른다 (슬라이딩도 같은 동작). 구르는 동안은 공격을 피한다.</summary>
    public sealed class RollAction : PlayerAction
    {
        private int _sign;

        public RollAction(ActionDefinition definition, ActionRequest request) : base(definition, request) { }

        protected override void OnBegin(IActorBody body)
        {
            _sign = FaceRequestedDirection(body);
            body.Invulnerable = true;
        }

        public override void End(IActorBody body)
        {
            base.End(body);
            body.Invulnerable = false;
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

        protected override bool KeepsClinging => true;

        protected override void OnBegin(IActorBody body)
        {
            int sign = Request.Direction == Direction.None ? -body.Facing : Request.Direction.ToSign(body.Facing);
            body.Facing = sign;
            Succeed();
        }

        protected override void OnTick(IActorBody body) { }
    }

    /// <summary>정해진 시간 동안 제자리에서 기다린다 ("N초 동안"이면 그 시간). 매달린 채로도 기다릴 수 있다.</summary>
    public sealed class WaitAction : PlayerAction
    {
        public WaitAction(ActionDefinition definition, ActionRequest request) : base(definition, request) { }

        protected override bool KeepsClinging => true;

        private float Seconds => Request.Unit == AmountUnit.Seconds ? Request.Amount : Definition.Duration;

        protected override int TimeoutTicks => 0;

        protected override void OnBegin(IActorBody body)
        {
            body.Velocity = new Vector2(0f, body.Velocity.y);
        }

        protected override void OnTick(IActorBody body)
        {
            if (ElapsedTicks >= DeterministicClock.SecondsToTicks(Seconds))
                Succeed();
        }
    }
}
