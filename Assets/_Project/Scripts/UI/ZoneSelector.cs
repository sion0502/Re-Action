using ReAction.Core;
using ReAction.Stage;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ReAction.UI
{
    /// <summary>
    /// Planning 중 마우스로 목표 구역을 고른다. 공개된 구역만 강조·선택할 수 있다.
    /// 화면에는 구역 이름을 표시하지 않는다. 선택된 Id는 해석기 컨텍스트로 쓰인다.
    /// </summary>
    public sealed class ZoneSelector : MonoBehaviour
    {
        private const float OutlinePixels = 1f;
        private const int PixelsPerUnit = 32;

        [SerializeField] private GameController _game;
        [SerializeField] private StageController _stage;
        [SerializeField] private Camera _camera;
        [SerializeField] private DebugActionPanel _panel;
        [SerializeField] private Sprite _lineSprite;
        [SerializeField] private Color _hoverColor = new Color(1f, 1f, 1f, 0.6f);
        [SerializeField] private Color _selectedColor = new Color(1f, 0.85f, 0.2f, 1f);

        private ZoneOutline _hoverOutline;
        private ZoneOutline _selectedOutline;

        public ZoneData Hovered { get; private set; }
        public ZoneData Selected { get; private set; }
        public string SelectedId => Selected?.Id;

        /// <summary>선택 실패 등 플레이어에게 보여줄 짧은 안내.</summary>
        public string Message { get; private set; } = "";

        private void Awake()
        {
            float thickness = OutlinePixels / PixelsPerUnit;
            _hoverOutline = new ZoneOutline(transform, "HoverOutline", _lineSprite, _hoverColor, thickness, 110);
            _selectedOutline = new ZoneOutline(transform, "SelectedOutline", _lineSprite, _selectedColor, thickness, 111);
        }

        private void OnEnable() => _game.StateChanged += OnStateChanged;
        private void OnDisable() => _game.StateChanged -= OnStateChanged;

        private void OnStateChanged(GameState from, GameState to)
        {
            // 목표는 한 번의 계획에만 쓰인다. 실행이 시작되면 지운다.
            if (to != GameState.Executing) return;
            ClearSelection();
            Message = "";
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || _game.State != GameState.Planning)
            {
                SetHovered(null);
                return;
            }

            Vector2 screen = mouse.position.ReadValue();
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (overUi || (_panel != null && _panel.ContainsScreenPoint(screen)))
            {
                SetHovered(null);
                return;
            }

            var world = (Vector2)_camera.ScreenToWorldPoint(screen);
            var zone = _stage.Graph.FindAt(world);
            SetHovered(zone != null && _stage.Knowledge.IsRevealed(zone.Id) ? zone : null);

            if (mouse.leftButton.wasPressedThisFrame) ClickAt(world);
        }

        /// <summary>월드 좌표 클릭 처리. 공개된 구역이면 목표로 선택하고, 아니면 안내만 한다.</summary>
        public void ClickAt(Vector2 world)
        {
            var zone = _stage.Graph.FindAt(world);
            if (zone == null) Message = "";
            else if (!_stage.Knowledge.IsRevealed(zone.Id)) Message = "아직 모르는 곳이다";
            else Select(zone);
        }

        public void Select(ZoneData zone)
        {
            Selected = zone;
            Message = "";
            _selectedOutline.Show(zone.Bounds);
            PzLog.Info($"Target zone = {zone.Id}");
        }

        public void ClearSelection()
        {
            Selected = null;
            _selectedOutline.Hide();
        }

        private void SetHovered(ZoneData zone)
        {
            if (zone == Hovered) return;
            Hovered = zone;
            if (zone == null || zone == Selected) _hoverOutline.Hide();
            else _hoverOutline.Show(zone.Bounds);
        }
    }
}
