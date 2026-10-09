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

        public MeleeEnemyState State { get; private set; } = MeleeEnemyState.Idle;
        public int Facing { get; private set; }
        public bool IsDead => State == MeleeEnemyState.Dead;
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

        public void Kill()
        {
            if (IsDead) return;
            Stop();
            _body.simulated = false;
            SetState(MeleeEnemyState.Dead);
            PzLog.Info($"{name} killed");
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
