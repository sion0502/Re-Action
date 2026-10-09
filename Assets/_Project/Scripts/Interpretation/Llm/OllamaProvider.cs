using System;
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
    /// 이 PC에서 돌아가는 Ollama 로컬 모델 (POST {url}/api/chat). API 키·비용 없음.
    /// - format에 JSON 스키마를 주면 응답이 스키마를 따른다
    /// - think=false: Qwen3 같은 생각 모델의 생각 단계를 꺼서 빠르게
    /// - keep_alive: 모델을 GPU에 올려 둔 채로 유지 (처음 올리는 데 수십 초 걸린다)
    /// </summary>
    public sealed class OllamaProvider : ILlmProvider
    {
        private const string KeepAlive = "30m";

        private readonly LlmSettings _settings;

        public OllamaProvider(LlmSettings settings)
        {
            _settings = settings;
        }

        public string Name => $"Ollama ({_settings.OllamaModel})";

        private string BaseUrl => _settings.OllamaUrl.TrimEnd('/');

        public async Task<string> CompleteJsonAsync(string systemPrompt, string userContent, JObject schema,
            CancellationToken ct)
        {
            var body = new JObject
            {
                ["model"] = _settings.OllamaModel,
                ["stream"] = false,
                ["think"] = false,
                ["format"] = schema,
                ["keep_alive"] = KeepAlive,
                ["options"] = new JObject { ["temperature"] = 0 },
                ["messages"] = new JArray
                {
                    new JObject { ["role"] = "system", ["content"] = systemPrompt },
                    new JObject { ["role"] = "user", ["content"] = userContent },
                },
            };

            string response = await Post("/api/chat", body, _settings.OllamaTimeoutSeconds, ct);
            JObject root;
            try
            {
                root = JObject.Parse(response);
            }
            catch (Exception)
            {
                throw new LlmException("응답을 읽을 수 없다");
            }

            if ((string)root["done_reason"] == "length") throw new LlmException("응답이 잘렸다");
            string json = (string)root["message"]?["content"];
            if (string.IsNullOrWhiteSpace(json)) throw new LlmException("응답에 결과가 없다");
            PzLog.Info($"Ollama: prompt={root["prompt_eval_count"]} out={root["eval_count"]} " +
                       $"total={(long?)root["total_duration"] / 1_000_000}ms");
            return json;
        }

        /// <summary>
        /// 모델을 미리 GPU에 올린다 (프롬프트 없는 generate 요청). 실패해도 게임에는 영향 없다.
        /// 첫 프롬프트가 모델 로딩 때문에 시간 초과로 키워드 해석기로 넘어가는 것을 막는다.
        /// </summary>
        public async void WarmUp()
        {
            try
            {
                var body = new JObject { ["model"] = _settings.OllamaModel, ["keep_alive"] = KeepAlive };
                await Post("/api/generate", body, 120f, CancellationToken.None);
                PzLog.Info($"{Name} loaded");
            }
            catch (Exception e)
            {
                PzLog.Warn($"{Name} warm-up failed: {e.Message}");
            }
        }

        private async Task<string> Post(string path, JObject body, float timeoutSeconds, CancellationToken ct)
        {
            using (var request = new UnityWebRequest(BaseUrl + path, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Newtonsoft.Json.Formatting.None)));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("content-type", "application/json");
                request.timeout = Mathf.Max(1, Mathf.CeilToInt(timeoutSeconds));

                var done = new TaskCompletionSource<bool>();
                request.SendWebRequest().completed += _ => done.TrySetResult(true);
                using (ct.Register(() =>
                       {
                           request.Abort();
                           done.TrySetCanceled();
                       }))
                {
                    await done.Task;
                }

                if (request.result == UnityWebRequest.Result.ConnectionError)
                    throw new LlmException($"Ollama에 연결할 수 없다 ({BaseUrl}). Ollama를 켜 두었는지 확인: {request.error}");
                if (request.result != UnityWebRequest.Result.Success)
                    throw new LlmException($"HTTP {request.responseCode} {DescribeError(request.downloadHandler?.text)}");
                return request.downloadHandler.text;
            }
        }

        private static string DescribeError(string body)
        {
            try
            {
                string error = (string)JObject.Parse(body ?? "")["error"];
                if (!string.IsNullOrEmpty(error)) return error;
            }
            catch (Exception)
            {
                // 본문을 그대로 보여준다
            }
            return string.IsNullOrEmpty(body) ? "" : body.Length <= 120 ? body : body.Substring(0, 120) + "…";
        }
    }
}
