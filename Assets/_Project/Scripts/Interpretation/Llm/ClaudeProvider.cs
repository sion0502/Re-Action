using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ReAction.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace ReAction.Interpretation.Llm
{
    /// <summary>
    /// LLM 공급자. "시스템 프롬프트 + 사용자 내용 + JSON 스키마"를 받아 스키마에 맞는 JSON 문자열을 돌려준다.
    /// 실패하면 LlmException. 공급자를 바꿀 때는 이 인터페이스만 새로 구현한다.
    /// </summary>
    public interface ILlmProvider
    {
        string Name { get; }
        Task<string> CompleteJsonAsync(string systemPrompt, string userContent, JObject schema, CancellationToken ct);
    }

    public sealed class LlmException : Exception
    {
        public LlmException(string message) : base(message) { }
    }

    /// <summary>
    /// Claude Messages API (POST /v1/messages)를 UnityWebRequest로 직접 호출한다.
    /// Unity에서는 NuGet SDK를 쓰기 어려워 HTTP로 부른다.
    /// - 구조화 출력: output_config.format = json_schema (응답이 항상 스키마를 따른다)
    /// - 프롬프트 캐싱: 변하지 않는 시스템 프롬프트에 cache_control (요청마다 바뀌는 상황은 user 메시지로)
    /// - 거절 대비: 서버 측 fallbacks "default"
    /// </summary>
    public sealed class ClaudeProvider : ILlmProvider
    {
        private const string Endpoint = "https://api.anthropic.com/v1/messages";
        private const string ApiVersion = "2023-06-01";
        private const string FallbackBeta = "server-side-fallback-2026-07-01";

        private readonly LlmSettings _settings;
        private readonly string _apiKey;

        public ClaudeProvider(LlmSettings settings, string apiKey)
        {
            _settings = settings;
            _apiKey = apiKey;
        }

        public string Name => $"Claude ({_settings.Model})";

        public async Task<string> CompleteJsonAsync(string systemPrompt, string userContent, JObject schema,
            CancellationToken ct)
        {
            var body = new JObject
            {
                ["model"] = _settings.Model,
                ["max_tokens"] = _settings.MaxTokens,
                ["system"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "text",
                        ["text"] = systemPrompt,
                        ["cache_control"] = new JObject { ["type"] = "ephemeral" },
                    },
                },
                ["messages"] = new JArray
                {
                    new JObject { ["role"] = "user", ["content"] = userContent },
                },
                ["output_config"] = new JObject
                {
                    ["effort"] = _settings.Effort,
                    ["format"] = new JObject { ["type"] = "json_schema", ["schema"] = schema },
                },
                // 안전 분류기가 거절하면 서버가 권장 모델로 다시 시도한다.
                ["fallbacks"] = "default",
            };

            using (var request = new UnityWebRequest(Endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Newtonsoft.Json.Formatting.None)));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("content-type", "application/json");
                request.SetRequestHeader("x-api-key", _apiKey);
                request.SetRequestHeader("anthropic-version", ApiVersion);
                request.SetRequestHeader("anthropic-beta", FallbackBeta);
                request.timeout = Mathf.Max(1, Mathf.CeilToInt(_settings.TimeoutSeconds));

                await Send(request, ct);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new LlmException(request.result == UnityWebRequest.Result.ConnectionError
                        ? $"연결 실패: {request.error}"
                        : $"HTTP {request.responseCode} {DescribeError(request.downloadHandler?.text)}");
                }

                return ExtractJson(request.downloadHandler.text);
            }
        }

        /// <summary>응답 본문에서 구조화 출력(JSON 텍스트)을 꺼낸다. 거절·잘림은 실패로 본다.</summary>
        public static string ExtractJson(string responseBody)
        {
            JObject response;
            try
            {
                response = JObject.Parse(responseBody);
            }
            catch (Exception)
            {
                throw new LlmException("응답을 읽을 수 없다");
            }

            string stopReason = (string)response["stop_reason"];
            if (stopReason == "refusal") throw new LlmException("모델이 요청을 거절했다");
            if (stopReason == "max_tokens") throw new LlmException("응답이 잘렸다 (max_tokens)");

            if (response["usage"] is JObject usage)
            {
                PzLog.Info($"LLM usage: in={usage["input_tokens"]} cacheRead={usage["cache_read_input_tokens"]} " +
                           $"cacheWrite={usage["cache_creation_input_tokens"]} out={usage["output_tokens"]}");
            }

            // 생각(thinking) 블록 등은 건너뛰고 마지막 텍스트 블록이 구조화 출력이다.
            var text = (response["content"] as JArray)?
                .OfType<JObject>()
                .LastOrDefault(b => (string)b["type"] == "text");
            string json = (string)text?["text"];
            if (string.IsNullOrWhiteSpace(json)) throw new LlmException("응답에 결과가 없다");
            return json;
        }

        private static async Task Send(UnityWebRequest request, CancellationToken ct)
        {
            var done = new TaskCompletionSource<bool>();
            var operation = request.SendWebRequest();
            operation.completed += _ => done.TrySetResult(true);
            using (ct.Register(() =>
                   {
                       request.Abort();
                       done.TrySetCanceled();
                   }))
            {
                await done.Task;
            }
        }

        /// <summary>오류 응답 {"error":{"type":..,"message":..}}을 짧게. 읽을 수 없으면 본문 앞부분.</summary>
        private static string DescribeError(string body)
        {
            try
            {
                var error = JObject.Parse(body ?? "")["error"];
                if (error != null) return $"{error["type"]}: {error["message"]}";
            }
            catch (Exception)
            {
                // 아래에서 본문 일부를 그대로 보여준다
            }
            return Truncate(body, 120);
        }

        private static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s.Substring(0, max) + "…";
    }
}
