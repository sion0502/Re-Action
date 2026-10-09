using System.Collections.Generic;
using ReAction.Actions;
using ReAction.Core;
using ReAction.Stage;
using UnityEngine;

namespace ReAction.UI
{
    /// <summary>
    /// 디버그용 패널 (F1). 프롬프트 대신 버튼으로 행동을 골라 바로 실행한다. 해석기·검증기를 거치지 않는다.
    /// 행동 개수 거부를 확인할 수 있도록 3개를 넘겨서도 담을 수 있다.
    /// </summary>
    public sealed class DebugActionPanel : MonoBehaviour
    {
        private const float ReferenceHeight = 720f;
        private const int MaxQueuedForTesting = 5;

        private static readonly Direction[] Directions =
            { Direction.None, Direction.Forward, Direction.Backward, Direction.Left, Direction.Right };
        private static readonly string[] DirectionLabels = { "없음", "앞", "뒤", "왼쪽", "오른쪽" };

        private static readonly Rect PanelRect = new Rect(10, 10, 360, 360);

        [SerializeField] private GameController _game;
        [SerializeField] private StageController _stage;
        [SerializeField] private ZoneSelector _selector;
        [Tooltip("F1로 켜고 끈다")]
        [SerializeField] private bool _visible;

        private readonly List<ActionRequest> _queue = new List<ActionRequest>();
        private int _directionIndex;
        private string _message = "";

        private static float Scale => Screen.height / ReferenceHeight;

        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame) _visible = !_visible;
        }

        /// <summary>화면 좌표(왼쪽 아래 원점)가 패널 위인지. 패널 위 클릭은 월드 클릭으로 처리하지 않는다.</summary>
        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            if (!_visible) return false;
            var guiPoint = new Vector2(screenPoint.x, Screen.height - screenPoint.y) / Scale;
            return PanelRect.Contains(guiPoint);
        }

        private void OnGUI()
        {
            if (!_visible || _game == null || _game.Catalog == null) return;

            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));

            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            GUILayout.Label($"상태: {_game.State}   틱: {_game.Tick}");
            // 구역 Id는 디버그 표시일 뿐, 실제 게임 화면에는 나오지 않는다.
            if (_stage != null) GUILayout.Label($"[debug] 현재 구역: {_stage.CurrentZoneId ?? "-"}");
            if (_selector != null)
            {
                GUILayout.Label($"[debug] 목표: {_selector.SelectedId ?? "(클릭해서 선택)"}");
                if (!string.IsNullOrEmpty(_selector.Message)) GUILayout.Label(_selector.Message);
            }
            if (_game.CurrentAction != null)
                GUILayout.Label($"실행 중: {_game.CurrentAction.Definition.DisplayName} ({_game.CurrentAction.ElapsedTicks})");

            GUI.enabled = _game.State == GameState.Planning;

            GUILayout.Space(6);
            GUILayout.Label("방향");
            _directionIndex = GUILayout.Toolbar(_directionIndex, DirectionLabels);

            GUILayout.Space(6);
            GUILayout.Label("행동 추가");
            GUILayout.BeginHorizontal();
            foreach (var definition in _game.Catalog.Definitions)
            {
                if (definition == null) continue;
                if (GUILayout.Button(definition.DisplayName) && _queue.Count < MaxQueuedForTesting)
                    _queue.Add(new ActionRequest(definition.Type, Directions[_directionIndex]));
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label($"순서 ({_queue.Count}/{ActionExecutor.MaxActionsPerPrompt})");
            for (int i = 0; i < _queue.Count; i++)
                GUILayout.Label($"  [{i + 1}] {_queue[i]}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("실행")) Execute();
            if (GUILayout.Button("비우기"))
            {
                _queue.Clear();
                _message = "";
            }
            GUILayout.EndHorizontal();

            GUI.enabled = true;

            if (!string.IsNullOrEmpty(_message)) GUILayout.Label(_message);
            if (!string.IsNullOrEmpty(_game.LastResult)) GUILayout.Label(_game.LastResult);

            GUILayout.EndArea();
        }

        private void Execute()
        {
            if (_game.TryExecute(_queue, out var error))
            {
                _queue.Clear();
                _message = "";
            }
            else
            {
                _message = error;
            }
        }
    }
}
