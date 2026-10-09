using System.Collections.Generic;
using ReAction.Actions;
using ReAction.Core;
using UnityEngine;

namespace ReAction.UI
{
    /// <summary>
    /// M1 디버그용 패널. 프롬프트 대신 버튼으로 행동을 골라 실행한다.
    /// 행동 개수 거부를 확인할 수 있도록 3개를 넘겨서도 담을 수 있다.
    /// </summary>
    public sealed class DebugActionPanel : MonoBehaviour
    {
        private const float ReferenceHeight = 720f;
        private const int MaxQueuedForTesting = 5;

        private static readonly Direction[] Directions =
            { Direction.None, Direction.Forward, Direction.Backward, Direction.Left, Direction.Right };
        private static readonly string[] DirectionLabels = { "없음", "앞", "뒤", "왼쪽", "오른쪽" };

        [SerializeField] private GameController _game;
        [SerializeField] private bool _visible = true;

        private readonly List<ActionRequest> _queue = new List<ActionRequest>();
        private int _directionIndex;
        private string _message = "";

        private void OnGUI()
        {
            if (!_visible || _game == null || _game.Catalog == null) return;

            float scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            GUILayout.BeginArea(new Rect(10, 10, 360, 330), GUI.skin.box);
            GUILayout.Label($"상태: {_game.State}   틱: {_game.Tick}");
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
