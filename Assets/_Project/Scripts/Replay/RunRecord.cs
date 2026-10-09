using System.Collections.Generic;
using ReAction.Actions;

namespace ReAction.Replay
{
    /// <summary>프롬프트 하나의 실행 기록. 계획 중에도 시간이 흐르므로 실행된 틱까지 있어야 재현된다.</summary>
    public sealed class PlanRecord
    {
        public PlanRecord(int tick, string prompt, string targetZoneId, IReadOnlyList<ActionRequest> actions)
        {
            Tick = tick;
            Prompt = prompt ?? "";
            TargetZoneId = targetZoneId;
            Actions = new List<ActionRequest>(actions);
        }

        public int Tick { get; }
        public string Prompt { get; }
        public string TargetZoneId { get; }
        public IReadOnlyList<ActionRequest> Actions { get; }
    }

    /// <summary>한 시도(처음부터 죽거나 클리어할 때까지)의 프롬프트 기록. M6 FINAL RUN 리플레이의 입력.</summary>
    public sealed class RunRecord
    {
        private readonly List<PlanRecord> _plans = new List<PlanRecord>();

        public IReadOnlyList<PlanRecord> Plans => _plans;

        public void Add(PlanRecord plan) => _plans.Add(plan);
        public void Clear() => _plans.Clear();
    }
}
