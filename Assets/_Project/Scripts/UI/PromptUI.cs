using ReAction.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ReAction.UI
{
    /// <summary>
    /// 하단 프롬프트 입력창 + 해석 미리보기 + 안내 문구 + 상단 상태 표시.
    /// 로직은 PlanningController에 있고, 여기서는 입력 전달과 표시만 한다.
    /// </summary>
    public sealed class PromptUI : MonoBehaviour
    {
        [SerializeField] private GameController _game;
        [SerializeField] private PlanningController _planning;
        [SerializeField] private ZoneSelector _selector;
        [SerializeField] private TMP_InputField _input;
        [SerializeField] private TMP_Text _preview;
        [SerializeField] private TMP_Text _message;
        [SerializeField] private TMP_Text _status;

        private void OnEnable()
        {
            _input.onSubmit.AddListener(OnSubmit);
            _input.onValueChanged.AddListener(OnValueChanged);
            _planning.Changed += Refresh;
            _game.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            _input.onSubmit.RemoveListener(OnSubmit);
            _input.onValueChanged.RemoveListener(OnValueChanged);
            _planning.Changed -= Refresh;
            _game.StateChanged -= OnStateChanged;
        }

        private void Start()
        {
            Refresh();
            OnStateChanged(_game.State, _game.State);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) _planning.ClearPreview();

            // 구역 이름은 보여주지 않는다. 목표가 정해졌는지만 알려준다.
            _status.text = _game.State == GameState.Planning
                ? (_selector.SelectedId != null ? "계획 중 · 목표 지정됨" : "계획 중 · 목표를 클릭하세요")
                : _game.State == GameState.Executing ? "실행 중" : "";
        }

        private void OnSubmit(string text)
        {
            _planning.Submit(text);
            // 실행이 시작되면 입력창을 비운다. 미리보기 단계면 그대로 둬서 바로 고칠 수 있게 한다.
            if (_game.State == GameState.Executing) _input.text = "";
            _input.ActivateInputField();
        }

        private void OnValueChanged(string text)
        {
            _planning.ClearPreview();
            _message.text = _planning.Message;
        }

        private void OnStateChanged(GameState from, GameState to)
        {
            _input.interactable = to == GameState.Planning || to == GameState.Interpreting;
            if (to != GameState.Planning) return;
            _input.ActivateInputField();

            // 실행 중 실패한 행동이 있으면 이유를 보여준다. 다음 입력을 시작하면 지워진다.
            if (from == GameState.Executing && _game.LastRunFailed) _message.text = _game.LastResult.TrimEnd();
        }

        private void Refresh()
        {
            _preview.text = _planning.PreviewText;
            _message.text = _planning.Message;
        }
    }
}
