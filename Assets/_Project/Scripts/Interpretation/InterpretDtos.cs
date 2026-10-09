using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ReAction.Actions;
using ReAction.Stage;

namespace ReAction.Interpretation
{
    /// <summary>
    /// 자연어 프롬프트를 행동 목록으로 바꾸는 해석기. 구현체(Mock, LLM)를 교체할 수 있다.
    /// 해석기는 판정하지 않는다. 결과는 항상 ActionValidator를 거친다.
    /// </summary>
    public interface IActionInterpreter
    {
        Task<InterpretResult> InterpretAsync(InterpretRequest request, CancellationToken ct);
    }

    /// <summary>
    /// 해석기에 넘기는 컨텍스트. 미발견 구역·적 정보는 절대 넣지 않는다.
    /// </summary>
    public sealed class InterpretRequest
    {
        public string Prompt = "";
        public string CurrentZoneId;
        /// <summary>플레이어가 마우스로 고른 목표 구역 (없을 수 있음).</summary>
        public string TargetZoneId;
        /// <summary>공개된 구역만.</summary>
        public List<ZoneData> RevealedZones = new List<ZoneData>();
        public List<ActionType> AllowedActions = new List<ActionType>();
        public int MaxActions = ActionExecutor.MaxActionsPerPrompt;
    }

    [Serializable]
    public sealed class InterpretResult
    {
        /// <summary>인식된 행동 전부. 개수 제한으로 자르지 않는다 (판정은 검증기가 한다).</summary>
        public List<ActionRequest> Actions = new List<ActionRequest>();
        /// <summary>행동 수가 MaxActions를 넘었는지.</summary>
        public bool OverLimit;
        /// <summary>해석 불가·모호할 때 이유.</summary>
        public string Notes = "";
        /// <summary>디버그용 원문 (LLM 응답 등).</summary>
        public string Raw = "";
    }
}
