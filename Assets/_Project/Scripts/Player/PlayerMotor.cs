using ReAction.Actions;
using UnityEngine;

namespace ReAction.Player
{
    /// <summary>
    /// 플레이어 물리 몸체. 행동(PlayerAction)은 IActorBody를 통해서만 이 컴포넌트를 조작한다.
    /// 물리는 GameController가 고정 틱으로 직접 진행시킨다 (Physics2D.simulationMode = Script).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class PlayerMotor : MonoBehaviour, IActorBody
    {
        private const float GroundProbeDistance = 0.05f;
        private const float GroundNormalMinY = 0.7f;

        [SerializeField] private int _initialFacing = 1;

        private readonly RaycastHit2D[] _hits = new RaycastHit2D[4];
        private Rigidbody2D _body;
        private Collider2D _collider;
        private ContactFilter2D _groundFilter;

        public Vector2 Position => _body.position;

        public Vector2 Velocity
        {
            get => _body.linearVelocity;
            set => _body.linearVelocity = value;
        }

        public int Facing { get; set; }

        public bool IsGrounded
        {
            get
            {
                int count = _collider.Cast(Vector2.down, _groundFilter, _hits, GroundProbeDistance);
                for (int i = 0; i < count; i++)
                {
                    if (_hits[i].normal.y >= GroundNormalMinY) return true;
                }
                return false;
            }
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            _groundFilter = new ContactFilter2D { useTriggers = false };
            _groundFilter.useLayerMask = false;
            Facing = _initialFacing >= 0 ? 1 : -1;
        }
    }
}
