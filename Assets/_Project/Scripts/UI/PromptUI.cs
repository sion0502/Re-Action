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
        [Tooltip("화면 위 가운데 큰 글씨 (CLEAR / FINAL RUN)")]
        [SerializeField] private TMP_Text _title;

        [Header("Death")]
        [SerializeField] private GameObject _deathPanel;
        [SerializeField] private TMP_Text _deathText;
        [SerializeField] private Button _restartButton;
        [Tooltip("사망 화면의 \"메인 화면으로\" 버튼 (Esc)")]
        [SerializeField] private Button _titleButton;
        [Tooltip("사망 화면에서 두 버튼을 나란히 둘 때 가운데에서 떨어진 거리")]
        [SerializeField] private float _deathButtonOffset = 58f;

        [Header("Tutorial")]
        [Tooltip("입력칸이 비어 있을 때 돌아가며 보여주는 예시 프롬프트")]
        [SerializeField] private string[] _examples =
        {
            "문을 걷어차고 들어가",
            "외벽을 타고 올라가",
            "창문을 깨고 들어가",
            "오른쪽으로 3칸 달려",
            "0.5초 기다리고 베어",
            "뒤돌아서 베어",
            "사다리 타고 올라가",
            "벽까지 달려",
            "앞으로 구르기",
        };
        [SerializeField] private float _exampleSeconds = 4f;
        [TextArea]
        [SerializeField] private string _goalHint =
            "목표: 건물 옥상까지 올라가라. 마우스로 목표를 찍고, 어떻게 움직일지 입력한다. (행동은 한 번에 3개까지)";

        private bool _moveCaretToEnd;
        private TMP_Text _restartLabel;
        private TMP_Text _placeholder;
        private int _exampleIndex;
        private float _exampleTimer;

        private void OnEnable()
        {
            _input.onSubmit.AddListener(OnSubmit);
            _input.onValueChanged.AddListener(OnValueChanged);
            _restartButton.onClick.AddListener(OnRestartClicked);
            if (_titleButton != null) _titleButton.onClick.AddListener(OnTitleClicked);
            _planning.Changed += Refresh;
            _game.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            _input.onSubmit.RemoveListener(OnSubmit);
            _input.onValueChanged.RemoveListener(OnValueChanged);
            _restartButton.onClick.RemoveListener(OnRestartClicked);
            if (_titleButton != null) _titleButton.onClick.RemoveListener(OnTitleClicked);
            _planning.Changed -= Refresh;
            _game.StateChanged -= OnStateChanged;
        }

        private void Start()
        {
            _placeholder = _input.placeholder as TMP_Text;
            Refresh();
            OnStateChanged(_game.State, _game.State);
            ShowExample();
            // 처음 시작할 때만 목표를 알려준다. 무엇이든 입력하면 지워진다.
            if (_game.Attempt == 1 && _message.text.Length == 0) _message.text = _goalHint;
        }

        /// <summary>입력칸이 비어 있으면 예시 프롬프트를 몇 초마다 바꿔 보여준다.</summary>
        private void UpdateExamples()
        {
            if (_placeholder == null || _examples.Length == 0) return;
            _exampleTimer += Time.unscaledDeltaTime;
            if (_exampleTimer < _exampleSeconds) return;
            _exampleTimer = 0f;
            _exampleIndex = (_exampleIndex + 1) % _examples.Length;
            ShowExample();
        }

        private void ShowExample()
        {
            if (_placeholder == null || _examples.Length == 0) return;
            _placeholder.text = $"예: {_examples[_exampleIndex]}   (Enter: 실행)";
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) _planning.ClearFeedback();

            KeepInputFocused();
            UpdateDeathPanel(keyboard);
            UpdateFinalRun(keyboard);
            UpdateExamples();

            string label = _planning.InterpreterLabel;
            _status.text = label.Length > 0
                ? $"시도 #{_game.Attempt} · {StatusText()} · {label}"
                : $"시도 #{_game.Attempt} · {StatusText()}";
        }

        private string StatusText()
        {
            switch (_game.State)
            {
                // 구역 이름은 보여주지 않는다. 목표가 정해졌는지만 알려준다.
                case GameState.Planning:
                    return _selector.SelectedId != null ? "계획 중 · 목표 지정됨" : "계획 중 · 목표를 클릭하세요";
                case GameState.Interpreting: return "해석 중… (시간 정지)";
                case GameState.Executing: return "실행 중";
                case GameState.Dead: return "죽었다";
                case GameState.Rewind: return "되감는 중 (클릭: 건너뛰기)";
                case GameState.Cleared: return "클리어!";
                case GameState.FinalRun: return "FINAL RUN 재생 중 (클릭: 건너뛰기)";
                case GameState.Result: return "결과";
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

        /// <summary>
        /// 죽으면 원인과 다시 시작·메인 화면으로 버튼을, FINAL RUN이 끝나면 결과와 처음부터 다시 버튼을 띄운다.
        /// 사망 연출이 끝나야 누를 수 있다 (Enter: 다시 시작, Esc: 메인 화면으로).
        /// </summary>
        private void UpdateDeathPanel(Keyboard keyboard)
        {
            bool dead = _game.State == GameState.Dead;
            bool result = _game.State == GameState.Result;
            bool show = dead || result;
            if (_deathPanel.activeSelf != show) _deathPanel.SetActive(show);
            if (!show) return;

            _deathText.text = result ? ResultText() : _game.LastDeathCause;
            if (_restartLabel == null) _restartLabel = _restartButton.GetComponentInChildren<TMP_Text>();
            if (_restartLabel != null) _restartLabel.text = result ? "처음부터 다시 (Enter)" : "다시 시작 (Enter)";
            _restartButton.interactable = _game.CanRestart;
            if (_titleButton != null)
            {
                if (_titleButton.gameObject.activeSelf != dead) _titleButton.gameObject.SetActive(dead);
                _titleButton.interactable = _game.CanRestart;
                var pos = ((RectTransform)_restartButton.transform).anchoredPosition;
                ((RectTransform)_restartButton.transform).anchoredPosition =
                    new Vector2(dead ? -_deathButtonOffset : 0f, pos.y);
            }
            if (!_game.CanRestart || keyboard == null) return;
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                _game.RequestRestart();
            else if (dead && _titleButton != null && keyboard.escapeKey.wasPressedThisFrame)
                _game.RequestReturnToTitle();
        }

        private void OnRestartClicked() => _game.RequestRestart();

        private void OnTitleClicked() => _game.RequestReturnToTitle();

        private string ResultText()
        {
            string text = $"클리어! 총 시도 {_game.Attempt}회\n" +
                          $"프롬프트 {_game.ClearPrompts}개 · 행동 {_game.ClearActions}개 · {_game.ClearSeconds:0.0}초";
            if (_game.FinalRunDesynced) text += "\n(리플레이가 원래 시도와 어긋났다)";
            return text;
        }

        /// <summary>CLEAR / FINAL RUN 제목과 재생 중인 프롬프트 자막. FINAL RUN은 클릭이나 키로 건너뛴다.</summary>
        private void UpdateFinalRun(Keyboard keyboard)
        {
            var state = _game.State;
            _title.text = state == GameState.Cleared ? "CLEAR"
                : state == GameState.FinalRun ? "FINAL RUN"
                : "";
            if (state != GameState.FinalRun) return;

            _preview.text = _game.ReplayPrompt.Length > 0 ? $"> {_game.ReplayPrompt}" : "";
            var mouse = Mouse.current;
            bool skip = (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                        (keyboard != null && keyboard.anyKey.wasPressedThisFrame);
            if (skip) _game.SkipFinalRun();
        }

        private void OnSubmit(string text)
        {
            // 입력창은 실행이 시작될 때 비운다 (OnStateChanged). AI 해석은 몇 초 걸려서
            // 여기서는 아직 해석 중일 수 있다. 해석·검증에 실패하면 글자를 그대로 둬서 바로 고칠 수 있게 한다.
            _planning.Submit(text);
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
            // 해석 중에는 입력을 막는다 (그사이 친 글자가 다음 프롬프트에 섞이지 않게).
            _input.interactable = to == GameState.Planning;
            if (to == GameState.Executing) _input.text = "";
            if (to == GameState.Cleared || to == GameState.Result)
            {
                _input.text = "";
                _planning.ClearFeedback();
                _preview.text = "";
            }
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
