using UnityEngine;

namespace ReAction.Interpretation.Llm
{
    public enum LlmProviderKind
    {
        Claude,
        /// <summary>이 PC의 Ollama 로컬 모델 (키·비용 없음)</summary>
        Ollama,
    }

    /// <summary>
    /// LLM 해석기 설정. 공급자를 바꾸려면 ILlmProvider 구현을 추가하고 Provider만 바꾼다.
    /// API 키는 여기에 넣지 않는다 (ApiKeyStore 참고).
    /// </summary>
    [CreateAssetMenu(menuName = "ReAction/LLM Settings", fileName = "LlmSettings")]
    public sealed class LlmSettings : ScriptableObject
    {
        [Tooltip("끄면 항상 목업(키워드) 해석기만 쓴다")]
        public bool Enabled = true;

        public LlmProviderKind Provider = LlmProviderKind.Claude;

        [Tooltip("Claude 모델 ID")]
        public string Model = "claude-opus-5-5";

        [Tooltip("생각 깊이: low / medium / high / xhigh / max. 행동 해석은 단순한 분류라 low로 빠르게")]
        public string Effort = "low";

        [Tooltip("응답 대기 한도 (초). 넘으면 목업 해석기로 대체한다")]
        public float TimeoutSeconds = 8f;

        [Tooltip("응답 최대 토큰 (생각 + JSON). 너무 작으면 응답이 잘려 실패한다")]
        public int MaxTokens = 8000;

        [Header("Ollama (로컬 모델)")]
        public string OllamaUrl = "http://localhost:11434";

        [Tooltip("ollama list에 나오는 모델 이름")]
        public string OllamaModel = "richardyoung/qwen3-14b-abliterated:Q5_K_M";

        [Tooltip("로컬 모델 응답 대기 한도 (초). GPU에 따라 몇 초씩 걸린다")]
        public float OllamaTimeoutSeconds = 20f;
    }
}
