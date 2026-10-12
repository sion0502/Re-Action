using ReAction.Core;
using ReAction.Player;
using ReAction.Stage;
using UnityEngine;

namespace ReAction.Enemies
{
    public enum MeleeEnemyState
    {
        Idle,
        Chase,
        Windup,
        Recover,
        Dead,
    }

    /// <summary>
    /// 근접 적 (고블린). 같은 구역에서 플레이어를 보면 쫓아가고, 가까우면 예비 동작 후 벤다.
    /// 모든 판단은 고정 틱 단위이고 랜덤이 없다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class MeleeEnemy : MonoBehaviour, ISimulated, IResettable, IDamageable
    {
        private const float BodyCenterY = 0.5f;

        [SerializeField] private MeleeEnemySettings _settings;
        [SerializeField] private PlayerMotor _target;
        [Tooltip("없으면 구역 검사 없이 거리만 본다 (테스트용)")]
        [SerializeField] private StageController _stage;
        [SerializeField] private int _initialFacing = -1;

        private Rigidbody2D _body;
        private Collider2D _collider;
        private int _timer;

        private Vector2 _startPosition;
        private int _startFacing;

        // 메인 화면 배경용 순찰 (게임 중에는 쓰지 않는다)
        private const float PatrolSpeedRatio = 0.5f;
        private const float PatrolEdgeMargin = 0.6f;
        private bool _patrolling;
        private float _patrolMin;
        private float _patrolMax;
        private int _patrolWalkTicks;
        private int _patrolIdleTicks;

        public MeleeEnemyState State { get; private set; } = MeleeEnemyState.Idle;
        public int Facing { get; private set; }
        public bool IsDead => State == MeleeEnemyState.Dead;

        /// <summary>쓰러졌을 때 (연출용).</summary>
        public event System.Action<MeleeEnemy> Died;
        public Vector2 Position
        {
            get
            {
                Initialize();
                return _body.position;
            }
        }

        /// <summary>상태가 바뀐 틱 (애니메이션을 처음부터 재생할지 판단용).</summary>
        public int StateVersion { get; private set; }

        private void Awake() => Initialize();

        public void Initialize()
        {
            if (_body != null) return;
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            Facing = _initialFacing >= 0 ? 1 : -1;
            if (_target != null) Physics2D.IgnoreCollision(_collider, _target.Collider);
        }

        /// <summary>테스트·빌더에서 참조를 주입할 때.</summary>
        public void Setup(MeleeEnemySettings settings, PlayerMotor target, StageController stage)
        {
            _settings = settings;
            _target = target;
            _stage = stage;
            Initialize();
            if (_target != null) Physics2D.IgnoreCollision(_collider, _target.Collider);
        }

        public void PreTick(int tick)
        {
            if (IsDead) return;
            if (_patrolling)
            {
                PatrolTick();
                return;
            }

            if (_target == null || _target.IsDead)
            {
                Stop();
                return;
            }

            float dx = _target.Position.x - _body.position.x;
            switch (State)
            {
                case MeleeEnemyState.Idle:
                    Stop();
                    if (CanSeeTarget()) SetState(MeleeEnemyState.Chase);
                    break;

                case MeleeEnemyState.Chase:
                    if (!CanSeeTarget())
                    {
                        SetState(MeleeEnemyState.Idle);
                        break;
                    }
                    Facing = dx >= 0f ? 1 : -1;
                    if (Mathf.Abs(dx) <= _settings.AttackStartRange)
                    {
                        Stop();
                        _timer = DeterministicClock.SecondsToTicks(_settings.WindupSeconds);
                        SetState(MeleeEnemyState.Windup);
                    }
                    else
                    {
                        _body.linearVelocity = new Vector2(Facing * _settings.MoveSpeed, _body.linearVelocity.y);
                    }
                    break;

                case MeleeEnemyState.Windup:
                    Stop();
                    if (--_timer > 0) break;
                    Strike();
                    _timer = DeterministicClock.SecondsToTicks(_settings.RecoverSeconds);
                    SetState(MeleeEnemyState.Recover);
                    break;

                case MeleeEnemyState.Recover:
                    Stop();
                    if (--_timer <= 0) SetState(CanSeeTarget() ? MeleeEnemyState.Chase : MeleeEnemyState.Idle);
                    break;
            }
        }

        public void PostTick(int tick) { }

        /// <summary>
        /// 메인 화면 배경: 플레이어를 무시하고 자기 방 안에서 걷기와 쉬기를 번갈아 한다.
        /// index로 시작 시점과 걷는 시간을 달리해 모두가 똑같이 움직이지 않게 한다.
        /// </summary>
        public void StartPatrol(int index)
        {
            Initialize();
            _patrolling = true;
            ComputePatrolRange();
            _patrolWalkTicks = DeterministicClock.SecondsToTicks(1.4f + 0.35f * (index % 4));
            _patrolIdleTicks = DeterministicClock.SecondsToTicks(0.8f + 0.25f * (index % 3));
            _timer = DeterministicClock.SecondsToTicks(0.3f * index);
            SetState(MeleeEnemyState.Idle);
        }

        private void PatrolTick()
        {
            float x = _body.position.x;
            if (State == MeleeEnemyState.Chase)
            {
                // 끝에 닿으면 돌아선다
                if ((Facing > 0 && x >= _patrolMax) || (Facing < 0 && x <= _patrolMin)) Facing = -Facing;
                _body.linearVelocity = new Vector2(Facing * _settings.MoveSpeed * PatrolSpeedRatio, _body.linearVelocity.y);
                if (--_timer > 0) return;
                Stop();
                _timer = _patrolIdleTicks;
                SetState(MeleeEnemyState.Idle);
                return;
            }

            Stop();
            if (--_timer > 0) return;
            // 쉬고 나면 방향을 바꿔 걷는다 (너무 끝에 있으면 안쪽으로)
            Facing = x >= _patrolMax ? -1 : x <= _patrolMin ? 1 : -Facing;
            _timer = _patrolWalkTicks;
            SetState(MeleeEnemyState.Chase);
        }

        /// <summary>순찰 범위: 서 있는 방의 안쪽. 바닥의 사다리 구멍에 빠지지 않게 그 앞에서 멈춘다.</summary>
        private void ComputePatrolRange()
        {
            var me = _body.position;
            _patrolMin = me.x - 1.5f;
            _patrolMax = me.x + 1.5f;
            if (_stage == null) return;

            var room = _stage.Graph.FindAt(me + Vector2.up * BodyCenterY, z => z.Kind.IsSpace());
            if (room == null) return;
            _patrolMin = room.Bounds.xMin + PatrolEdgeMargin;
            _patrolMax = room.Bounds.xMax - PatrolEdgeMargin;

            foreach (var zone in _stage.Graph.Zones)
            {
                // 아래층에서 올라오는 사다리 = 이 방 바닥의 구멍
                if (zone.Kind != ZoneKind.Ladder || Mathf.Abs(zone.Bounds.yMax - room.Bounds.yMin) > 0.5f) continue;
                if (zone.Bounds.xMax < _patrolMin || zone.Bounds.xMin > _patrolMax) continue;
                if (zone.Bounds.center.x > me.x) _patrolMax = Mathf.Min(_patrolMax, zone.Bounds.xMin - PatrolEdgeMargin);
                else _patrolMin = Mathf.Max(_patrolMin, zone.Bounds.xMax + PatrolEdgeMargin);
            }
            if (_patrolMin > _patrolMax) _patrolMin = _patrolMax = me.x;
        }

        public void Kill()
        {
            if (IsDead) return;
            Stop();
            _body.simulated = false;
            SetState(MeleeEnemyState.Dead);
            PzLog.Info($"{name} killed");
            Died?.Invoke(this);
        }

        public void CaptureInitialState()
        {
            Initialize();
            _startPosition = _body.position;
            _startFacing = Facing;
        }

        public void ResetToInitialState()
        {
            Initialize();
            _body.simulated = true;
            _body.position = _startPosition;
            transform.position = _startPosition;
            _body.linearVelocity = Vector2.zero;
            Facing = _startFacing;
            _timer = 0;
            _patrolling = false;
            SetState(MeleeEnemyState.Idle);
        }

        private bool CanSeeTarget()
        {
            var me = _body.position;
            var target = _target.Position;
            if (Mathf.Abs(target.x - me.x) > _settings.AggroRange) return false;
            if (Mathf.Abs(target.y - me.y) > _settings.VerticalTolerance) return false;
            if (_stage == null) return true;

            // 벽 너머는 못 본다: 같은 공간 구역에 있어야 한다.
            var myZone = _stage.Graph.FindAt(me + Vector2.up * BodyCenterY, z => z.Kind.IsSpace());
            var targetZone = _stage.Graph.FindAt(target + Vector2.up * BodyCenterY, z => z.Kind.IsSpace());
            return myZone != null && myZone == targetZone;
        }

        private void Strike()
        {
            float dx = _target.Position.x - _body.position.x;
            bool inFront = dx * Facing >= 0f;
            bool inRange = Mathf.Abs(dx) <= _settings.StrikeRange &&
                           Mathf.Abs(_target.Position.y - _body.position.y) <= _settings.VerticalTolerance;
            if (inFront && inRange) _target.Kill($"{DisplayName}에게 베였다");
        }

        private string DisplayName => "고블린";

        private void Stop() => _body.linearVelocity = new Vector2(0f, _body.linearVelocity.y);

        private void SetState(MeleeEnemyState state)
        {
            State = state;
            StateVersion++;
        }
    }
}
