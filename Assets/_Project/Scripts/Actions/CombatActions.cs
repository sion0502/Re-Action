using ReAction.Core;
using ReAction.Enemies;
using UnityEngine;

namespace ReAction.Actions
{
    /// <summary>
    /// 앞을 벤다. 휘두르는 도중 한 틱에 앞쪽 범위를 판정해서 맞은 적은 즉사한다.
    /// Distance = 사거리, Duration = 전체 동작 시간.
    /// </summary>
    public sealed class SlashAction : PlayerAction
    {
        // 동작 시작 후 이 비율 지점에서 판정한다 (칼이 내려오는 프레임).
        private const float HitFraction = 0.3f;
        private const float BodyCenterY = 0.6f;
        private const float HitHeight = 1.0f;

        private static readonly Collider2D[] Hits = new Collider2D[8];

        private int _sign;
        private int _hitTick;
        private int _durationTicks;

        public SlashAction(ActionDefinition definition, ActionRequest request) : base(definition, request) { }

        /// <summary>이번 베기로 쓰러뜨린 적 수.</summary>
        public int KillCount { get; private set; }

        protected override void OnBegin(IActorBody body)
        {
            _sign = FaceRequestedDirection(body);
            _durationTicks = DeterministicClock.SecondsToTicks(Definition.Duration);
            _hitTick = Mathf.Max(1, Mathf.RoundToInt(_durationTicks * HitFraction));
            body.Velocity = new Vector2(0f, body.Velocity.y);
        }

        protected override void OnTick(IActorBody body)
        {
            body.Velocity = new Vector2(0f, body.Velocity.y);
            if (ElapsedTicks == _hitTick) Strike(body);
            if (ElapsedTicks >= _durationTicks) Succeed();
        }

        private void Strike(IActorBody body)
        {
            float reach = Definition.Distance;
            var center = body.Position + new Vector2(_sign * reach / 2f, BodyCenterY);
            int count = body.PhysicsScene.OverlapBox(center, new Vector2(reach, HitHeight), 0f, Hits);

            for (int i = 0; i < count; i++)
            {
                var target = Hits[i].GetComponentInParent<IDamageable>();
                if (target == null || target.IsDead) continue;
                target.Kill();
                KillCount++;
            }
        }
    }
}
