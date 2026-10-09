using System;
using ReAction.Core;
using UnityEngine;

namespace ReAction.Stage
{
    public enum DoorState
    {
        Closed,
        Open,
        Broken,
    }

    /// <summary>
    /// 문. 닫혀 있으면 지나갈 수 없다. 열거나(걷어차서) 부수면 통과할 수 있고, 시도가 끝나면 다시 닫힌다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
    public sealed class Door : MonoBehaviour, IResettable
    {
        [SerializeField] private string _zoneId;
        [SerializeField] private Color _closedColor = new Color(0.55f, 0.38f, 0.22f, 1f);
        [SerializeField] private Color _openColor = new Color(0.55f, 0.38f, 0.22f, 0.25f);
        [SerializeField] private Color _brokenColor = new Color(0.3f, 0.22f, 0.15f, 0.5f);

        private BoxCollider2D _collider;
        private SpriteRenderer _renderer;
        private Vector3 _closedScale;

        public string ZoneId => _zoneId;
        public DoorState State { get; private set; }
        public bool IsPassable => State != DoorState.Closed;
        public float X => transform.position.x;

        /// <summary>문이 열리거나 부서졌을 때 (안쪽이 보이게 된다).</summary>
        public event Action<Door> Opened;

        private void Awake() => Initialize();

        public void Initialize()
        {
            if (_collider != null) return;
            _collider = GetComponent<BoxCollider2D>();
            _renderer = GetComponent<SpriteRenderer>();
            _closedScale = transform.localScale;
            Apply();
        }

        public void Setup(string zoneId) => _zoneId = zoneId;

        public void Open() => SetState(DoorState.Open);
        public void Break() => SetState(DoorState.Broken);

        public void CaptureInitialState() => Initialize();
        public void ResetToInitialState()
        {
            Initialize();
            State = DoorState.Closed;
            Apply();
        }

        private void SetState(DoorState state)
        {
            Initialize();
            if (State == state || State == DoorState.Broken) return;
            bool wasClosed = State == DoorState.Closed;
            State = state;
            Apply();
            PzLog.Info($"Door {_zoneId} -> {state}");
            if (wasClosed) Opened?.Invoke(this);
        }

        private void Apply()
        {
            _collider.enabled = State == DoorState.Closed;
            switch (State)
            {
                case DoorState.Closed:
                    _renderer.color = _closedColor;
                    transform.localScale = _closedScale;
                    break;
                case DoorState.Open:
                    _renderer.color = _openColor;
                    transform.localScale = _closedScale;
                    break;
                case DoorState.Broken:
                    _renderer.color = _brokenColor;
                    transform.localScale = new Vector3(_closedScale.x, _closedScale.y * 0.25f, 1f);
                    break;
            }
        }
    }
}
