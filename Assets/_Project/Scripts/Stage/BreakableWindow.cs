using System;
using ReAction.Core;
using UnityEngine;

namespace ReAction.Stage
{
    /// <summary>
    /// 깰 수 있는 창문 (유리). 깨지기 전에는 막혀 있고, 깨면 통과할 수 있다. 시도가 끝나면 복구된다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
    public sealed class BreakableWindow : MonoBehaviour, IResettable
    {
        [SerializeField] private string _zoneId;
        [SerializeField] private Color _intactColor = new Color(0.6f, 0.8f, 0.95f, 0.55f);
        [SerializeField] private Color _brokenColor = new Color(0.6f, 0.8f, 0.95f, 0.08f);

        private BoxCollider2D _collider;
        private SpriteRenderer _renderer;

        public string ZoneId => _zoneId;
        public bool IsBroken { get; private set; }
        public Bounds Bounds => GetComponent<SpriteRenderer>().bounds;

        public event Action<BreakableWindow> Broken;

        private void Awake() => Initialize();

        public void Initialize()
        {
            if (_collider != null) return;
            _collider = GetComponent<BoxCollider2D>();
            _renderer = GetComponent<SpriteRenderer>();
            Apply();
        }

        public void Setup(string zoneId) => _zoneId = zoneId;

        public void Break()
        {
            Initialize();
            if (IsBroken) return;
            IsBroken = true;
            Apply();
            PzLog.Info($"Window {_zoneId} broken");
            Broken?.Invoke(this);
        }

        public void CaptureInitialState() => Initialize();
        public void ResetToInitialState()
        {
            Initialize();
            IsBroken = false;
            Apply();
        }

        private void Apply()
        {
            _collider.enabled = !IsBroken;
            _renderer.color = IsBroken ? _brokenColor : _intactColor;
        }
    }
}
