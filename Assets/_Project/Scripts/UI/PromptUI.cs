using ReAction.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ReAction.UI
{
    /// <summary>
    /// 하단 프롬프트 입력창 + 실행 중인 계획 요약 + 안내 문구 + 상단 상태 표시.
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

        [Header("Death")]
        [SerializeField] private GameObject _deathPanel;
        [SerializeField] private TMP_Text _deathText;
        [SerializeField] private Button _restartButton;

        private bool _moveCaretToEnd;

        private void OnEnable()
        {
            _input.onSubmit.AddListener(OnSubmit);
            _input.onValueChanged.AddListener(OnValueChanged);
            _restartButton.onClick.AddListener(OnRestartClicked);
            _planning.Changed += Refresh;
            _game.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            _input.onSubmit.RemoveListener(OnSubmit);
            _input.onValueChanged.RemoveListener(OnValueChanged);
            _restartButton.onClick.RemoveListener(OnRestartClicked);
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
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) _planning.ClearFeedback();

            KeepInputFocused();
            UpdateDeathPanel(keyboard);

            _status.text = $"시도 #{_game.Attempt} · {StatusText()}";
        }

        private string StatusText()
        {
            switch (_game.State)
            {
                // 구역 이름은 보여주지 않는다. 목표가 정해졌는지만 알려준다.
                case GameState.Planning:
                    return _selector.SelectedId != null ? "계획 중 · 목표 지정됨" : "계획 중 · 목표를 클릭하세요";
                case GameState.Executing: return "실행 중";
                case GameState.Dead: return "죽었다";
                case GameState.Rewind: return "되감는 중 (클릭: 건너뛰기)";
                default: return "";
            }
        }

        /// <summary>
        /// Planning 중에는 입력창을 항상 활성 상태로 둔다. 월드 클릭(목표 지정)이나 Esc로 포커스가 풀려도
        /// 다음 프레임에 다시 잡고, 쓰던 글자 끝에 커서를 둔다.
        /// </summary>
        private void KeepInputFocused()
        {
            if (_game.State != GameState.Planning || !_input.interactable) return;

            if (!_input.isFocused)
            {
                _input.ActivateInputField();
                _moveCaretToEnd = true;
            }
            else if (_moveCaretToEnd)
            {
                _input.MoveTextEnd(false);
                _moveCaretToEnd = false;
            }
        }

        /// <summary>죽으면 원인과 다시 시작 버튼을 띄운다. 사망 연출이 끝나야 누를 수 있다 (Enter도 가능).</summary>
        private void UpdateDeathPanel(Keyboard keyboard)
        {
            bool dead = _game.State == GameState.Dead;
            if (_deathPanel.activeSelf != dead) _deathPanel.SetActive(dead);
            if (!dead) return;

            _deathText.text = _game.LastDeathCause;
            _restartButton.interactable = _game.CanRestart;
            if (_game.CanRestart && keyboard != null &&
                (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                _game.RequestRestart();
        }

        private void OnRestartClicked() => _game.RequestRestart();

        private void OnSubmit(string text)
        {
            _planning.Submit(text);
            // 실행이 시작되면 입력창을 비운다. 해석·검증에 실패했으면 그대로 둬서 바로 고칠 수 있게 한다.
            if (_game.State == GameState.Executing) _input.text = "";
            _input.ActivateInputField();
        }

        private void OnValueChanged(string text)
        {
            // 실행 직후 입력창을 비울 때는 방금 실행한 계획 요약을 남겨 둔다. 새로 타이핑하면 지운다.
            if (text.Length == 0) return;
            _planning.ClearFeedback();
            _message.text = _planning.Message;
        }

        private void OnStateChanged(GameState from, GameState to)
        {
            _input.interactable = to == GameState.Planning || to == GameState.Interpreting;
            if (to == GameState.Dead)
            {
                // 입력하던 중에 죽었으면 쓰던 프롬프트는 버린다.
                _input.text = "";
                _planning.ClearFeedback();
            }
            if (to != GameState.Planning) return;
            _input.ActivateInputField();

            // 실행 중 실패한 행동이 있으면 이유를 보여준다. 다음 입력을 시작하면 지워진다.
            if (from == GameState.Executing && _game.LastRunFailed) _message.text = _game.LastResult.TrimEnd();
            if (from == GameState.Reset)
            {
                _planning.ClearFeedback();
                _message.text = $"{_game.LastDeathCause}. 처음부터 다시";
            }
        }

        private void Refresh()
        {
            _preview.text = _planning.PlanText;
            _message.text = _planning.Message;
        }
    }
}
