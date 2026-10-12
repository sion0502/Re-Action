using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ReAction.Core;
using ReAction.Interpretation.Llm;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

namespace ReAction.UI
{
    /// <summary>
    /// 메인 화면: 제목이 아래에서 도트 단위로 끊기며 올라오고, 아무 키나 누르면 제목이 조금 더 올라가며
    /// 해석 방식 메뉴(로컬 LLM / 키워드 / 종료)가 나온다. 배경은 실제 스테이지(GameState.Title)다.
    /// 입력 처리는 PressAnyKey / Move / Choose로 분리해 MCP에서 직접 호출해 검증할 수 있다.
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        private const string ChoicePrefKey = "ReAction.TitleChoice";

        public enum Phase
        {
            Intro,
            WaitKey,
            Rising,
            Menu,
        }

        [SerializeField] private LlmSettings _llmSettings;
        [Tooltip("메인 화면 동안 숨길 게임 UI (프롬프트 입력창 등)")]
        [SerializeField] private GameObject _gameUi;
        [SerializeField] private RectTransform _logo;
        [SerializeField] private TMP_Text _pressKey;
        [SerializeField] private GameObject _menu;
        [SerializeField] private TMP_Text[] _items;
        [SerializeField] private TMP_Text _ollamaStatus;

        [Header("Animation")]
        [Tooltip("로고 한 도트가 화면에서 차지하는 픽셀 (로고 확대 배율). 움직임도 이 단위로 끊긴다")]
        [SerializeField] private int _dot = 3;
        [SerializeField] private float _introStartY = -260f;
        [SerializeField] private float _introRestY = 30f;
        [SerializeField] private float _introSeconds = 1.3f;
        [SerializeField] private float _menuLogoY = 72f;
        [SerializeField] private float _riseSeconds = 0.4f;
        [SerializeField] private float _blinkSeconds = 0.5f;

        [Header("Menu")]
        [SerializeField] private string[] _labels = { "로컬 AI로 시작 (Ollama)", "키워드 해석으로 시작 (AI 없이)", "종료" };
        [SerializeField] private string _cursor = "▶ ";
        [SerializeField] private Color _selectedColor = new Color(1f, 0.85f, 0.3f, 1f);
        [SerializeField] private Color _normalColor = new Color(0.75f, 0.75f, 0.78f, 1f);
        [SerializeField] private Color _okColor = new Color(0.55f, 0.9f, 0.55f, 1f);
        [SerializeField] private Color _warnColor = new Color(1f, 0.6f, 0.5f, 1f);

        public Phase Current { get; private set; } = Phase.Intro;
        public int Selected { get; private set; }

        private float _timer;
        private float _riseFromY;

        private void Awake()
        {
            // 게임을 시작하며 씬을 다시 불러온 경우: 메인 화면 없이 바로 게임
            if (GameLaunchOptions.SkipTitle)
            {
                gameObject.SetActive(false);
                return;
            }
            if (_gameUi != null) _gameUi.SetActive(false);
        }

        private void Start()
        {
            Selected = Mathf.Clamp(PlayerPrefs.GetInt(ChoicePrefKey, 0), 0, _labels.Length - 1);
            SetLogoY(_introStartY);
            _pressKey.gameObject.SetActive(false);
            _menu.SetActive(false);
            for (int i = 0; i < _items.Length; i++)
            {
                var item = _items[i].GetComponent<TitleMenuItem>();
                if (item != null) item.Setup(this, i);
            }
            RefreshMenu();
            CheckOllama();
        }

        private void Update()
        {
            _timer += Time.unscaledDeltaTime;
            switch (Current)
            {
                case Phase.Intro:
                    float t = Mathf.Clamp01(_timer / _introSeconds);
                    SetLogoY(Mathf.Lerp(_introStartY, _introRestY, EaseOutCubic(t)));
                    if (t >= 1f) EnterWaitKey();
                    break;
                case Phase.WaitKey:
                    _pressKey.enabled = Mathf.FloorToInt(_timer / _blinkSeconds) % 2 == 0;
                    break;
                case Phase.Rising:
                    float r = Mathf.Clamp01(_timer / _riseSeconds);
                    SetLogoY(Mathf.Lerp(_riseFromY, _menuLogoY, EaseOutCubic(r)));
                    if (r >= 1f) EnterMenu();
                    break;
            }
            ReadInput();
        }

        private void ReadInput()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            bool click = mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame);
            bool anyKey = (keyboard != null && keyboard.anyKey.wasPressedThisFrame) || click;

            if (Current != Phase.Menu)
            {
                if (anyKey) PressAnyKey();
                return;
            }
            if (keyboard == null) return;
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) Move(-1);
            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) Move(1);
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
                keyboard.spaceKey.wasPressedThisFrame)
                Choose(Selected);
        }

        /// <summary>
        /// 아무 키: 제목이 올라오는 중이면 바로 제자리로 (실수로 메뉴까지 건너뛰지 않게),
        /// 대기 중이면 제목을 조금 더 올리고 메뉴를 연다.
        /// </summary>
        public void PressAnyKey()
        {
            if (Current == Phase.Intro)
            {
                SetLogoY(_introRestY);
                EnterWaitKey();
            }
            else if (Current == Phase.WaitKey)
            {
                Current = Phase.Rising;
                _timer = 0f;
                _riseFromY = _logo.anchoredPosition.y;
                _pressKey.gameObject.SetActive(false);
            }
            else if (Current == Phase.Rising)
            {
                SetLogoY(_menuLogoY);
                EnterMenu();
            }
        }

        public void Move(int delta)
        {
            if (Current != Phase.Menu) return;
            Selected = (Selected + delta + _labels.Length) % _labels.Length;
            RefreshMenu();
        }

        public void Hover(int index)
        {
            if (Current != Phase.Menu || index == Selected) return;
            Selected = index;
            RefreshMenu();
        }

        public void Choose(int index)
        {
            if (Current != Phase.Menu) return;
            Selected = index;
            PlayerPrefs.SetInt(ChoicePrefKey, index);
            PlayerPrefs.Save();
            switch (index)
            {
                case 0:
                    GameLaunchOptions.StartGame(InterpreterChoice.LocalLlm);
                    break;
                case 1:
                    GameLaunchOptions.StartGame(InterpreterChoice.Keyword);
                    break;
                default:
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                    break;
            }
        }

        private void EnterWaitKey()
        {
            Current = Phase.WaitKey;
            _timer = 0f;
            _pressKey.gameObject.SetActive(true);
        }

        private void EnterMenu()
        {
            Current = Phase.Menu;
            _timer = 0f;
            _menu.SetActive(true);
            RefreshMenu();
        }

        private void RefreshMenu()
        {
            for (int i = 0; i < _items.Length && i < _labels.Length; i++)
            {
                bool selected = i == Selected;
                _items[i].text = selected ? _cursor + _labels[i] : _labels[i];
                _items[i].color = selected ? _selectedColor : _normalColor;
            }
        }

        /// <summary>도트 단위로 끊기게 위치를 맞춘다.</summary>
        private void SetLogoY(float y)
        {
            float snapped = Mathf.Round(y / _dot) * _dot;
            _logo.anchoredPosition = new Vector2(_logo.anchoredPosition.x, snapped);
        }

        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        /// <summary>Ollama가 켜져 있고 모델이 있는지 미리 알려준다. 꺼져 있어도 AI를 고를 수는 있다 (키워드 해석으로 대체).</summary>
        private async void CheckOllama()
        {
            if (_llmSettings == null || _ollamaStatus == null) return;
            _ollamaStatus.text = "Ollama 확인 중…";
            _ollamaStatus.color = _normalColor;

            string url = _llmSettings.OllamaUrl.TrimEnd('/') + "/api/tags";
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 2;
                var op = request.SendWebRequest();
                while (!op.isDone) await Task.Yield();
                if (this == null) return; // 그사이 씬이 바뀌었다

                if (request.result != UnityWebRequest.Result.Success)
                {
                    SetOllamaStatus("Ollama가 꺼져 있다 · AI를 고르면 키워드 해석으로 대신한다", _warnColor);
                    return;
                }

                bool hasModel = false;
                try
                {
                    var models = JObject.Parse(request.downloadHandler.text)["models"] as JArray;
                    hasModel = models != null && models.Any(m => (string)m["name"] == _llmSettings.OllamaModel);
                }
                catch (Newtonsoft.Json.JsonException)
                {
                }

                if (hasModel) SetOllamaStatus("Ollama 연결됨 · 로컬 AI 사용 가능", _okColor);
                else SetOllamaStatus("Ollama는 켜져 있지만 모델이 없다 · 키워드 해석으로 대신한다", _warnColor);
            }
        }

        private void SetOllamaStatus(string text, Color color)
        {
            PzLog.Info($"Title: {text}");
            _ollamaStatus.text = text;
            _ollamaStatus.color = color;
        }
    }
}
