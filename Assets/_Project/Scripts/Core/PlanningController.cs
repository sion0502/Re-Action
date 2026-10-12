using System;
using System.Linq;
using System.Threading;
using ReAction.Actions;
using ReAction.Interpretation;
using ReAction.Interpretation.Llm;
using ReAction.Stage;
using ReAction.UI;
using UnityEngine;

namespace ReAction.Core
{
    /// <summary>
    /// 프롬프트 한 번의 흐름: 해석 → 검증 → 실행 (Enter 한 번).
    /// 실패한 해석은 세계를 바꾸지 않고 런에도 기록하지 않는다.
    /// </summary>
    public sealed class PlanningController : MonoBehaviour
    {
        [SerializeField] private GameController _game;
        [SerializeField] private StageController _stage;
        [SerializeField] private ZoneSelector _selector;
        [SerializeField] private TextAsset _mockKeywords;
        [Tooltip("비워 두거나 Enabled를 끄면 키워드(목업) 해석기만 쓴다")]
        [SerializeField] private LlmSettings _llmSettings;

        private IActionInterpreter _interpreter;
        private ActionValidator _validator;
        private CancellationTokenSource _cts;

        /// <summary>마지막으로 실행한 계획 요약. 예: "[1] 문 걷어차기 → [2] 진입 → [3] 앞으로 구르기"</summary>
        public string PlanText { get; private set; } = "";

        /// <summary>해석·검증 실패 등 플레이어에게 보여줄 안내.</summary>
        public string Message { get; private set; } = "";

        /// <summary>지금 쓰는 해석기 이름 (상태 표시용). 예: "AI 해석 (Claude)", "키워드 해석"</summary>
        public string InterpreterLabel { get; private set; } = "";

        /// <summary>마지막 해석이 AI 실패로 키워드 해석기를 대신 썼다면 그 이유.</summary>
        public string LastFallbackReason { get; private set; } = "";

        /// <summary>계획 요약·안내가 바뀔 때.</summary>
        public event Action Changed;

        /// <summary>프롬프트 처리 결과: true = 실행됨, false = 해석·검증 실패 (효과음 등 연출용).</summary>
        public event Action<bool> Outcome;

        private void Start()
        {
            // 메인 화면에서는 해석기를 만들지 않는다 (Ollama 모델을 미리 올리지 않게). 게임을 시작하면 씬을 다시 불러온다.
            if (_game.StartsWithTitle) return;

            // StageController.Awake에서 그래프가 만들어진 뒤에 초기화한다.
            var allowed = _game.Catalog.Definitions.Where(d => d != null).Select(d => d.Type).ToList();
            _interpreter = CreateInterpreter(allowed);
            _validator = new ActionValidator(_stage.Graph, _stage.Knowledge, allowed,
                ActionExecutor.MaxActionsPerPrompt, _stage.IsEntryOpen);
        }

        private void OnDestroy() => _cts?.Cancel();

        /// <summary>
        /// LLM을 쓸 수 있으면 LLM 해석기(실패 시 키워드 해석기로 대체), 아니면 키워드 해석기.
        /// 메인 화면에서 고른 방식이 있으면 그것을 따른다 (로컬 LLM = Ollama, 키워드 = LLM에 전혀 연결하지 않음).
        /// </summary>
        private IActionInterpreter CreateInterpreter(System.Collections.Generic.List<ActionType> allowed)
        {
            var mock = new MockInterpreter(MockKeywordTable.FromJson(_mockKeywords.text));
            var choice = GameLaunchOptions.Interpreter;
            bool useLlm = choice == InterpreterChoice.LocalLlm ||
                          (choice == InterpreterChoice.FromSettings && _llmSettings != null && _llmSettings.Enabled);
            if (_llmSettings == null || !useLlm)
            {
                InterpreterLabel = "키워드 해석";
                PzLog.Info($"Using keyword interpreter (choice {choice})");
                return mock;
            }

            ILlmProvider provider;
            if (choice == InterpreterChoice.LocalLlm || _llmSettings.Provider == LlmProviderKind.Ollama)
            {
                var ollama = new OllamaProvider(_llmSettings);
                ollama.WarmUp(); // 첫 프롬프트 전에 모델을 GPU에 올려 둔다
                provider = ollama;
                InterpreterLabel = "AI 해석 (로컬 모델)";
            }
            else
            {
                string key = ApiKeyStore.GetAnthropicKey();
                if (key == null)
                {
                    PzLog.Warn($"LLM enabled but no API key ({ApiKeyStore.EnvVar} or {ApiKeyStore.LocalFile}) - using mock interpreter");
                    InterpreterLabel = "키워드 해석 (API 키 없음)";
                    return mock;
                }
                provider = new ClaudeProvider(_llmSettings, key);
                InterpreterLabel = "AI 해석 (Claude)";
            }

            PzLog.Info($"Using LLM interpreter: {provider.Name}");
            return new LlmInterpreter(provider, mock, allowed);
        }

        /// <summary>
        /// Enter 입력: 해석 → 검증 → 통과하면 바로 실행. 실패하면 이유만 보여주고 세계는 바꾸지 않는다.
        /// </summary>
        public async void Submit(string prompt)
        {
            prompt = (prompt ?? "").Trim();
            if (_game.State != GameState.Planning) return;

            ClearFeedback();
            if (prompt.Length == 0)
            {
                SetMessage("행동을 입력하세요");
                Outcome?.Invoke(false);
                return;
            }
            if (!_game.TryBeginInterpreting()) return;

            var request = BuildRequest(prompt);
            InterpretResult result;
            _cts = new CancellationTokenSource();
            try
            {
                result = await _interpreter.InterpretAsync(request, _cts.Token);
            }
            catch (Exception e)
            {
                PzLog.Error($"Interpret failed: {e.Message}");
                _game.EndInterpreting();
                SetMessage("해석하지 못했다. 다시 입력하세요");
                Outcome?.Invoke(false);
                return;
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
            }

            _game.EndInterpreting();
            if (this == null) return;

            LastFallbackReason = result.Source == InterpretSource.MockFallback ? result.FallbackReason : "";
            PzLog.Info($"Interpret [{result.Source}] \"{prompt}\" target={request.TargetZoneId ?? "-"} -> " +
                       $"[{string.Join(", ", result.Actions.Select(a => $"{a}@{a.Target ?? "-"}"))}] overLimit={result.OverLimit}");

            var validation = _validator.Validate(result, request.CurrentZoneId);
            if (!validation.IsValid)
            {
                SetMessage(WithSourceNote(string.Join("\n", validation.Errors)));
                Outcome?.Invoke(false);
                return;
            }

            if (!_game.TryExecute(result.Actions, out var error, prompt, request.TargetZoneId))
            {
                SetMessage(error);
                Outcome?.Invoke(false);
                return;
            }

            Outcome?.Invoke(true);
            PzLog.Info($"Plan executed: target={request.TargetZoneId ?? "-"} prompt=\"{prompt}\"");
            // 실행 중에는 무엇을 하고 있는지 보여준다. 다음 입력을 시작하면 지워진다.
            PlanText = ActionNames.DescribePlan(result.Actions);
            // AI가 일부를 이해하지 못했다면 그 설명을, AI가 실패했다면 그 사실을 작게 알려준다.
            SetMessage(WithSourceNote(result.Source == InterpretSource.Mock ? "" : result.Notes));
        }

        private string WithSourceNote(string message)
        {
            if (LastFallbackReason.Length == 0) return message;
            string note = $"(AI 연결 실패 - 키워드 해석 사용: {LastFallbackReason})";
            return string.IsNullOrEmpty(message) ? note : $"{message}\n{note}";
        }

        /// <summary>새 입력을 시작하면 지난 계획 요약과 안내를 지운다.</summary>
        public void ClearFeedback()
        {
            if (PlanText.Length == 0 && Message.Length == 0) return;
            PlanText = "";
            SetMessage("");
        }

        private InterpretRequest BuildRequest(string prompt)
        {
            // 공개된 구역만 넘긴다. 미발견 정보가 해석기에 들어가면 안 된다.
            return new InterpretRequest
            {
                Prompt = prompt,
                CurrentZoneId = _stage.CurrentZoneId,
                TargetZoneId = _selector.SelectedId,
                RevealedZones = _stage.Graph.Zones.Where(z => _stage.Knowledge.IsRevealed(z.Id)).ToList(),
                AllowedActions = _game.Catalog.Definitions.Where(d => d != null).Select(d => d.Type).ToList(),
            };
        }

        private void SetMessage(string message)
        {
            Message = message ?? "";
            Changed?.Invoke();
        }
    }
}
