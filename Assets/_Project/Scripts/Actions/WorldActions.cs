using ReAction.Core;
using ReAction.Enemies;
using ReAction.Stage;
using UnityEngine;

namespace ReAction.Actions
{
    /// <summary>
    /// 문 열기 / 걷어차기 / 부수기. 문 앞까지 달려간 뒤 동작한다.
    /// 걷어차기는 문을 열면서 문 바로 너머의 적을 쓰러뜨린다.
    /// Duration = 동작 시간.
    /// </summary>
    public sealed class DoorAction : PlayerAction
    {
        private const float StandOff = 0.55f;
        private const float ApproachSpeed = 6f;
        private const float KickReach = 1.4f;
        private const float BodyCenterY = 0.6f;

        private static readonly Collider2D[] Hits = new Collider2D[8];

        private Door _door;
        private int _side;
        private bool _acting;
        private int _actTicks;

        public DoorAction(ActionDefinition definition, ActionRequest request) : base(definition, request) { }

        public override string AnimationState => _acting ? base.AnimationState : null;

        protected override void OnBegin(IActorBody body)
        {
            _door = World?.GetDoor(Request.Target);
            if (_door == null)
            {
                Fail("그곳에는 문이 없다");
                return;
            }
            _side = body.Position.x <= _door.X ? -1 : 1; // 플레이어가 문의 어느 쪽에 있는지
        }

        protected override void OnTick(IActorBody body)
        {
            if (!_acting)
            {
                var result = MoveTowardX(body, _door.X + _side * StandOff, ApproachSpeed);
                if (result == MoveResult.Blocked)
                {
                    Fail("문에 닿을 수 없다");
                    return;
                }
                if (result != MoveResult.Arrived) return;

                _acting = true;
                body.Facing = -_side;
                return;
            }

            body.Velocity = new Vector2(0f, body.Velocity.y);
            _actTicks++;
            int duration = DeterministicClock.SecondsToTicks(Definition.Duration);
            if (_actTicks == Mathf.Max(1, duration / 3)) Apply(body);
            if (_actTicks >= duration) Succeed();
        }

        private void Apply(IActorBody body)
        {
            switch (Request.Type)
            {
                case ActionType.OpenDoor:
                    _door.Open();
                    break;
                case ActionType.BreakDoor:
                    _door.Break();
                    break;
                case ActionType.KickDoor:
                    _door.Open();
                    KnockDownBehindDoor(body);
                    break;
            }
        }

        private void KnockDownBehindDoor(IActorBody body)
        {
            var center = new Vector2(_door.X - _side * KickReach / 2f, body.Position.y + BodyCenterY);
            int count = body.PhysicsScene.OverlapBox(center, new Vector2(KickReach, 1f), 0f, Hits);
            for (int i = 0; i < count; i++)
            {
                var target = Hits[i].GetComponentInParent<IDamageable>();
                if (target != null && !target.IsDead) target.Kill();
            }
        }
    }

    /// <summary>
    /// 들어가기: 문·창문을 지나 안쪽까지 이동한다. 닫힌 문은 지나갈 수 없다.
    /// 목표가 공간(방)이면 그 방 중앙으로 간다.
    /// </summary>
    public sealed class EnterAction : PlayerAction
    {
        private const float PassDistance = 1.2f;
        private const float Speed = 6f;
        private const float BodyCenterY = 0.6f;

        private float _targetX;
        private bool _alreadyInside;

        public EnterAction(ActionDefinition definition, ActionRequest request) : base(definition, request) { }

        protected override void OnBegin(IActorBody body)
        {
            var zone = World?.GetZone(Request.Target);
            if (zone == null)
            {
                Fail("어디로 들어갈지 모르겠다");
                return;
            }

            if (zone.Kind.IsSpace())
            {
                _targetX = zone.Bounds.center.x;
                return;
            }

            // 문·창문과 이어진 공간 중 아직 들어가 있지 않은 쪽으로 지나간다.
            // (창문을 깨고 이미 안으로 뛰어든 뒤라면 할 일이 없다)
            var probe = body.Position + Vector2.up * BodyCenterY;
            ZoneData destination = null;
            bool inConnectedSpace = false;
            foreach (var id in zone.Connections)
            {
                var space = World.GetZone(id);
                if (space == null || !space.Kind.IsSpace()) continue;
                if (space.Bounds.Contains(probe)) inConnectedSpace = true;
                else destination = space;
            }
            // 창문 밖으로 뛰어내린 뒤(벽에 매달려 있지도 않음)라면 이미 나온 것이다. 다시 들어가지 않는다.
            if (zone.Kind == ZoneKind.Window && !inConnectedSpace && !body.IsClinging) destination = null;
            if (destination == null)
            {
                _alreadyInside = true;
                return;
            }

            if (!World.IsEntryOpen(zone.Id))
            {
                Fail(zone.Kind == ZoneKind.Window ? "창문이 막혀 있다" : "문이 닫혀 있다");
                return;
            }
            float entryX = zone.Bounds.center.x;
            int dir = destination.Bounds.center.x >= entryX ? 1 : -1;
            _targetX = entryX + dir * PassDistance;
        }

        protected override void OnTick(IActorBody body)
        {
            if (_alreadyInside)
            {
                Succeed();
                return;
            }
            var result = MoveTowardX(body, _targetX, Speed);
            if (result == MoveResult.Arrived) Succeed();
            else if (result == MoveResult.Blocked) Fail("막혀서 들어갈 수 없다");
        }
    }

    /// <summary>
    /// 창문 깨고 뛰어들기 / 창문 넘기. 밖에서는 창문 바로 옆(외벽에 매달린 상태)에서만 할 수 있고, 깨거나 넘은 뒤 안쪽으로 뛰어든다.
    /// 방 안에서는 창문 앞까지 걸어간 뒤 깨고 창턱을 넘어 바깥으로 뛰어내린다 (착지는 행동이 끝난 뒤 중력으로).
    /// </summary>
    public sealed class WindowAction : PlayerAction
    {
        private const float MaxReachX = 1.0f;
        private const float DiveSpeed = 5f;
        private const float DiveSeconds = 0.35f;
        private const float ApproachSpeed = 6f;
        // 방 안에서 창문 앞에 설 때 창문 안쪽 끝에서 떨어진 거리
        private const float InsideStandOff = 0.35f;
        // 안에서 밖으로 뛰어내릴 때 창턱을 넘도록 살짝 뛴다
        private const float ExitHopSpeed = 4f;
        private const float BodyCenterY = 0.6f;

        private BreakableWindow _window;
        private int _windupTicks;
        private int _sign;
        private int _diveTicks;
        private bool _diving;
        private bool _fromInside;
        private bool _approaching;
        private float _standX;

        public WindowAction(ActionDefinition definition, ActionRequest request) : base(definition, request) { }

        protected override bool KeepsClinging => true;

        public override string AnimationState => _diving ? "Roll" : base.AnimationState;

        protected override void OnBegin(IActorBody body)
        {
            _window = World?.GetWindow(Request.Target);
            if (_window == null)
            {
                Fail("그곳에는 창문이 없다");
                return;
            }

            var bounds = _window.Bounds;
            float feetY = body.Position.y;
            bool nearX = Mathf.Abs(body.Position.x - bounds.center.x) <= MaxReachX;
            bool nearY = feetY >= bounds.min.y - 0.6f && feetY <= bounds.max.y;
            var room = InsideRoom(body);
            _fromInside = room != null;
            if (!nearY || (!nearX && !_fromInside))
            {
                Fail("창문에 닿지 않는다. 먼저 창문 옆으로 가야 한다");
                return;
            }
            if (_fromInside && !nearX)
            {
                // 방 안: 창문 앞까지 걸어간다
                bool windowOnLeft = bounds.center.x < room.Bounds.center.x;
                _standX = windowOnLeft ? room.Bounds.xMin + InsideStandOff : room.Bounds.xMax - InsideStandOff;
                _approaching = true;
            }

            if (Request.Type == ActionType.VaultWindow && !_window.IsBroken)
            {
                Fail("창문이 닫혀 있다. 깨고 들어가야 한다");
                return;
            }

            _sign = bounds.center.x >= body.Position.x ? 1 : -1;
            body.Facing = _sign;
            _windupTicks = Request.Type == ActionType.CrashWindow ? DeterministicClock.SecondsToTicks(Definition.Duration) : 1;
        }

        protected override void OnTick(IActorBody body)
        {
            if (_approaching)
            {
                var result = MoveTowardX(body, _standX, ApproachSpeed);
                if (result == MoveResult.Blocked)
                {
                    Fail("창문에 닿을 수 없다");
                    return;
                }
                if (result != MoveResult.Arrived) return;
                _approaching = false;
                body.Facing = _sign;
                _windupTicks += ElapsedTicks; // 도착한 뒤부터 예비 동작
                return;
            }

            if (!_diving)
            {
                body.Velocity = new Vector2(0f, _fromInside ? body.Velocity.y : 0f);
                if (ElapsedTicks < _windupTicks) return;

                _window.Break();
                ReleaseCling(body);
                _diving = true;
                if (_fromInside) body.Velocity = new Vector2(body.Velocity.x, ExitHopSpeed);
            }

            _diveTicks++;
            // 밖에서 안으로는 떨어지지 않게 수평으로 뛰어든다. 안에서 밖으로는 중력대로 뛰어내린다.
            float vy = _fromInside ? body.Velocity.y : Mathf.Max(body.Velocity.y, 0f);
            body.Velocity = new Vector2(_sign * DiveSpeed, vy);
            if (_diveTicks >= DeterministicClock.SecondsToTicks(DiveSeconds)) Succeed();
        }

        /// <summary>플레이어가 서 있는, 이 창문과 이어진 방. 밖(외벽)에 있으면 null.</summary>
        private ZoneData InsideRoom(IActorBody body)
        {
            var zone = World.GetZone(Request.Target);
            if (zone == null) return null;
            var probe = body.Position + Vector2.up * BodyCenterY;
            foreach (var id in zone.Connections)
            {
                var space = World.GetZone(id);
                if (space != null && space.Kind == ZoneKind.Room && space.Bounds.Contains(probe)) return space;
            }
            return null;
        }
    }

    /// <summary>
    /// 외벽 타기: 벽 아래까지 가서 벽 구역의 꼭대기(보통 창문 높이)까지 기어오른 뒤 매달린다.
    /// 매달린 상태는 다음 행동(창문 깨기 등)까지 유지된다.
    /// </summary>
    public sealed class WallClimbAction : PlayerAction
    {
        private const float BodyHalfWidth = 0.26f;
        private const float ApproachSpeed = 6f;

        private ZoneData _wall;
        private float _climbX;
        private bool _climbing;

        public WallClimbAction(ActionDefinition definition, ActionRequest request) : base(definition, request) { }

        public override string AnimationState => _climbing ? base.AnimationState : null;

        protected override void OnBegin(IActorBody body)
        {
            _wall = World?.GetZone(Request.Target);
            if (_wall == null || _wall.Kind != ZoneKind.ExteriorWall)
            {
                Fail("탈 수 있는 벽이 없다");
                return;
            }
            var b = _wall.Bounds;
            _climbX = _wall.WallSide > 0 ? b.xMax - BodyHalfWidth : b.xMin + BodyHalfWidth;
        }

        protected override void OnTick(IActorBody body)
        {
            if (!_climbing)
            {
                var result = MoveTowardX(body, _climbX, ApproachSpeed);
                if (result == MoveResult.Blocked)
                {
                    Fail("벽에 닿을 수 없다");
                    return;
                }
                if (result != MoveResult.Arrived) return;

                _climbing = true;
                body.Facing = _wall.WallSide;
                body.GravityEnabled = false;
                ResetStuck();
                return;
            }

            if (body.Position.y >= _wall.Bounds.yMax)
            {
                body.Velocity = Vector2.zero;
                body.IsClinging = true;
                Succeed();
                return;
            }
            body.Velocity = new Vector2(0f, Definition.Speed);
        }

        public override void End(IActorBody body)
        {
            body.Velocity = Vector2.zero;
            if (Status == ActionStatus.Failed) ReleaseCling(body);
        }
    }

    /// <summary>
    /// 사다리 오르내리기. 오르기: 사다리 앞까지 가서 위층 바닥 높이까지 올라간 뒤 사다리 구멍 덮개 위에 선다.
    /// 내려가기: 위층에서 사다리 구멍 위로 가서 덮개를 통과해 아래층 바닥까지 내려간다.
    /// 방향(Up/Down)이 없으면 서 있는 높이로 정한다 (사다리 아래면 오르기, 위면 내려가기).
    /// </summary>
    public sealed class LadderAction : PlayerAction
    {
        private const float ApproachSpeed = 6f;
        private const float MaxFootGap = 0.6f;
        private const float TopClearance = 0.05f;
        private const float BottomClearance = 0.05f;

        private ZoneData _ladder;
        private bool _climbing;
        private bool _down;

        public LadderAction(ActionDefinition definition, ActionRequest request) : base(definition, request) { }

        public override string AnimationState => _climbing ? base.AnimationState : null;

        protected override void OnBegin(IActorBody body)
        {
            _ladder = World?.GetZone(Request.Target);
            if (_ladder == null || _ladder.Kind != ZoneKind.Ladder)
            {
                Fail("근처에 사다리가 없다");
                return;
            }
            bool atBottom = Mathf.Abs(body.Position.y - _ladder.Bounds.yMin) <= MaxFootGap;
            bool atTop = Mathf.Abs(body.Position.y - _ladder.Bounds.yMax) <= MaxFootGap;
            if (Request.Direction == Direction.Down && !atTop)
            {
                Fail(atBottom ? "이미 사다리 아래에 있다" : "사다리에 닿지 않는다");
                return;
            }
            if (Request.Direction == Direction.Up && !atBottom)
            {
                Fail(atTop ? "이미 사다리 위에 있다" : "사다리에 닿지 않는다");
                return;
            }
            if (!atBottom && !atTop)
            {
                Fail("사다리에 닿지 않는다");
                return;
            }
            _down = Request.Direction == Direction.Down || (Request.Direction != Direction.Up && !atBottom);
        }

        protected override void OnTick(IActorBody body)
        {
            if (!_climbing)
            {
                var result = MoveTowardX(body, _ladder.Bounds.center.x, ApproachSpeed);
                if (result == MoveResult.Blocked)
                {
                    Fail("사다리에 닿을 수 없다");
                    return;
                }
                if (result != MoveResult.Arrived) return;

                _climbing = true;
                body.GravityEnabled = false;
                if (_down) body.PassThroughPlatforms = true;
                return;
            }

            if (_down)
            {
                if (body.Position.y <= _ladder.Bounds.yMin + BottomClearance)
                {
                    body.GravityEnabled = true;
                    body.PassThroughPlatforms = false;
                    body.Velocity = Vector2.zero;
                    Succeed();
                    return;
                }
                body.Velocity = new Vector2(0f, -Definition.Speed);
                return;
            }

            if (body.Position.y >= _ladder.Bounds.yMax + TopClearance)
            {
                body.GravityEnabled = true;
                body.Velocity = Vector2.zero;
                Succeed();
                return;
            }
            body.Velocity = new Vector2(0f, Definition.Speed);
        }

        public override void End(IActorBody body)
        {
            body.Velocity = new Vector2(0f, Mathf.Min(0f, body.Velocity.y));
            body.GravityEnabled = true;
            body.PassThroughPlatforms = false;
        }
    }
}
