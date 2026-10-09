using System;
using System.Linq;
using System.Threading;
using ReAction.Actions;
using ReAction.Interpretation;
using ReAction.Stage;
using ReAction.UI;
using UnityEngine;

namespace ReAction.Core
{
    /// <summary>
    /// 프롬프트 한 번의 흐름: 해석 → 검증 → 미리보기 → (같은 프롬프트로 한 번 더 Enter) → 실행.
    /// 실패한 해석은 세계를 바꾸지 않고 런에도 기록하지 않는다.
    /// </summary>
    public sealed class PlanningController : MonoBehaviour
    {
        [SerializeField] private GameController _game;
        [SerializeField] private StageController _stage;
        [SerializeField] private ZoneSelector _selector;
        [SerializeField] private TextAsset _mockKeywords;

        private IActionInterpreter _interpreter;
        private ActionValidator _validator;
        private CancellationTokenSource _cts;

        private InterpretResult _preview;
        private string _previewPrompt;
        private string _previewTarget;

        /// <summary>실행 전 확인용 요약. 예: "[1] 문 걷어차기 → [2] 진입 → [3] 앞으로 구르기"</summary>
        public string PreviewText { get; private set; } = "";

        /// <summary>해석·검증 실패 등 플레이어에게 보여줄 안내.</summary>
        public string Message { get; private set; } = "";

        public bool HasPreview => _preview != null;

        /// <summary>미리보기·안내가 바뀔 때.</summary>
        public event Action Changed;

        private void Start()
        {
            // StageController.Awake에서 그래프가 만들어진 뒤에 초기화한다.
            _interpreter = new MockInterpreter(MockKeywordTable.FromJson(_mockKeywords.text));
            var allowed = _game.Catalog.Definitions.Where(d => d != null).Select(d => d.Type);
            _validator = new ActionValidator(_stage.Graph, _stage.Knowledge, allowed);
        }

        private void OnDestroy() => _cts?.Cancel();

        /// <summary>
        /// Enter 입력. 같은 프롬프트·같은 목표로 미리보기가 떠 있으면 실행하고, 아니면 새로 해석한다.
        /// </summary>
        public async void Submit(string prompt)
        {
            prompt = (prompt ?? "").Trim();
            if (_game.State != GameState.Planning) return;

            if (_preview != null && prompt == _previewPrompt && _selector.SelectedId == _previewTarget)
            {
                Execute();
                return;
            }

            ClearPreview();
            if (prompt.Length == 0)
            {
                SetMessage("행동을 입력하세요");
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
                return;
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
            }

            _game.EndInterpreting();
            if (this == null) return;

            PzLog.Info($"Interpret \"{prompt}\" target={request.TargetZoneId ?? "-"} -> " +
                       $"[{string.Join(", ", result.Actions.Select(a => $"{a}@{a.Target ?? "-"}"))}] overLimit={result.OverLimit}");

            var validation = _validator.Validate(result, request.CurrentZoneId);
            if (!validation.IsValid)
            {
                SetMessage(string.Join("\n", validation.Errors));
                return;
            }

            _preview = result;
            _previewPrompt = prompt;
            _previewTarget = request.TargetZoneId;
            PreviewText = string.Join(" → ",
                result.Actions.Select((a, i) => $"[{i + 1}] {ActionNames.Describe(a)}"));
            SetMessage("Enter: 실행 / 내용을 고치면 다시 해석");
        }

        /// <summary>프롬프트를 고치거나 Esc를 누르면 미리보기를 버린다.</summary>
        public void ClearPreview()
        {
            if (_preview == null && Message.Length == 0) return;
            _preview = null;
            _previewPrompt = null;
            _previewTarget = null;
            PreviewText = "";
            SetMessage("");
        }

        private void Execute()
        {
            var actions = _preview.Actions;
            string prompt = _previewPrompt;
            string target = _previewTarget;

            if (!_game.TryExecute(actions, out var error))
            {
                SetMessage(error);
                return;
            }

            PzLog.Info($"Plan executed: target={target ?? "-"} prompt=\"{prompt}\"");
            _preview = null;
            _previewPrompt = null;
            _previewTarget = null;
            PreviewText = "";
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
