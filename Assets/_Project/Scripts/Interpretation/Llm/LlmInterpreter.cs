using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ReAction.Actions;
using ReAction.Core;

namespace ReAction.Interpretation.Llm
{
    /// <summary>
    /// LLM 해석기. 실패(시간 초과·네트워크·거절·JSON 오류)하면 목업 해석기 결과로 대신한다.
    /// 같은 (프롬프트 + 상황) 요청은 캐시해서 다시 물을 때 즉시, 같은 결과를 준다.
    /// </summary>
    public sealed class LlmInterpreter : IActionInterpreter
    {
        private readonly ILlmProvider _provider;
        private readonly MockInterpreter _fallback;
        private readonly string _systemPrompt;
        private readonly JObject _schema;
        private readonly int _maxActions;
        private readonly Dictionary<string, InterpretResult> _cache = new Dictionary<string, InterpretResult>();

        public LlmInterpreter(ILlmProvider provider, MockInterpreter fallback, IEnumerable<ActionType> allowedActions,
            int maxActions = ActionExecutor.MaxActionsPerPrompt)
        {
            var allowed = allowedActions.ToList();
            _provider = provider;
            _fallback = fallback;
            _maxActions = maxActions;
            _systemPrompt = LlmPrompt.BuildSystemPrompt(allowed, maxActions);
            _schema = LlmPrompt.BuildSchema(allowed);
        }

        public async Task<InterpretResult> InterpretAsync(InterpretRequest request, CancellationToken ct)
        {
            string userContent = LlmPrompt.BuildUserContent(request);
            if (_cache.TryGetValue(userContent, out var cached))
            {
                return Tag(cached, InterpretSource.LlmCache);
            }

            try
            {
                string json = await _provider.CompleteJsonAsync(_systemPrompt, userContent, _schema, ct);
                var result = LlmPrompt.ParseResult(json, _maxActions);
                result.Source = InterpretSource.Llm;
                _cache[userContent] = result;
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                // AI가 안 되면 키워드 해석기로 대신한다. 게임은 LLM 없이도 돌아가야 한다.
                string reason = e is LlmException ? e.Message : $"{e.GetType().Name}: {e.Message}";
                PzLog.Warn($"{_provider.Name} failed ({reason}) - falling back to mock interpreter");
                var fallback = await _fallback.InterpretAsync(request, ct);
                fallback.Source = InterpretSource.MockFallback;
                fallback.FallbackReason = reason;
                return fallback;
            }
        }

        private static InterpretResult Tag(InterpretResult cached, InterpretSource source) =>
            new InterpretResult
            {
                Actions = new List<ActionRequest>(cached.Actions),
                OverLimit = cached.OverLimit,
                Notes = cached.Notes,
                Raw = cached.Raw,
                Source = source,
            };
    }
}
