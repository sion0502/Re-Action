using System;
using ReAction.Actions;
using ReAction.Core;
using UnityEngine;

namespace ReAction.Player
{
    /// <summary>
    /// 플레이어 물리 몸체. 행동(PlayerAction)은 IActorBody를 통해서만 이 컴포넌트를 조작한다.
    /// 물리는 WorldSimulation이 고정 틱으로 직접 진행시킨다 (Physics2D.simulationMode = Script).
    /// 1히트에 죽는다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class PlayerMotor : MonoBehaviour, IActorBody, IResettable
    {
        private const float GroundProbeDistance = 0.05f;
        private const float GroundNormalMinY = 0.7f;

        [SerializeField] private int _initialFacing = 1;

        private readonly RaycastHit2D[] _hits = new RaycastHit2D[4];
        private Rigidbody2D _body;
        private Collider2D _collider;
        private ContactFilter2D _groundFilter;

        private int _facing;
        private float _gravityScale;
        private Vector2 _startPosition;
        private int _startFacing;

        public Collider2D Collider
        {
            get
            {
                Initialize();
                return _collider;
            }
        }

        public Vector2 Position
        {
            get
            {
                Initialize();
                return _body.position;
            }
        }

        public Vector2 Velocity
        {
            get
            {
                Initialize();
                return _body.linearVelocity;
            }
            set
            {
                Initialize();
                _body.linearVelocity = value;
            }
        }

        public int Facing
        {
            get
            {
                Initialize();
                return _facing;
            }
            set
            {
                Initialize();
                _facing = value;
            }
        }

        public bool Invulnerable { get; set; }
        public bool IsClinging { get; set; }

        public bool GravityEnabled
        {
            get
            {
                Initialize();
                return _body.gravityScale > 0f;
            }
            set
            {
                Initialize();
                _body.gravityScale = value ? _gravityScale : 0f;
            }
        }

        public PhysicsScene2D PhysicsScene => gameObject.scene.GetPhysicsScene2D();
        public bool IsDead { get; private set; }
        public string DeathCause { get; private set; } = "";

        public event Action Died;

        public bool IsGrounded
        {
            get
            {
                Initialize();
                int count = _collider.Cast(Vector2.down, _groundFilter, _hits, GroundProbeDistance);
                for (int i = 0; i < count; i++)
                {
                    if (_hits[i].normal.y >= GroundNormalMinY) return true;
                }
                return false;
            }
        }

        private void Awake() => Initialize();

        /// <summary>Awake가 불리지 않는 EditMode 테스트에서도 쓸 수 있게 지연 초기화한다.</summary>
        public void Initialize()
        {
            if (_body != null) return;
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            _gravityScale = _body.gravityScale;
            _groundFilter = new ContactFilter2D { useTriggers = false };
            _facing = _initialFacing >= 0 ? 1 : -1;
        }

        /// <summary>공격에 맞았을 때. 무적 중이면 무시하고 false.</summary>
        public bool Kill(string cause)
        {
            if (IsDead || Invulnerable) return false;
            IsDead = true;
            DeathCause = cause ?? "";
            Velocity = new Vector2(0f, Velocity.y);
            PzLog.Info($"Player died: {DeathCause}");
            Died?.Invoke();
            return true;
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
            _body.position = _startPosition;
            transform.position = _startPosition;
            _body.linearVelocity = Vector2.zero;
            Facing = _startFacing;
            Invulnerable = false;
            IsClinging = false;
            GravityEnabled = true;
            IsDead = false;
            DeathCause = "";
        }
    }
}
