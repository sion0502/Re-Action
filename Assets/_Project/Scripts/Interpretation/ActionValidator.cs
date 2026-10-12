using System.Collections.Generic;
using System.Linq;
using ReAction.Actions;
using ReAction.Stage;

namespace ReAction.Interpretation
{
    public sealed class ValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors { get; } = new List<string>();

        /// <summary>플레이어에게 보여줄 첫 번째 오류 (없으면 빈 문자열).</summary>
        public string FirstError => Errors.Count > 0 ? Errors[0] : "";
    }

    /// <summary>
    /// 해석 결과를 게임 규칙으로 검증한다. 해석기(LLM 포함)의 출력은 여기를 통과해야만 실행된다.
    /// </summary>
    public sealed class ActionValidator
    {
        public const float MaxSeconds = 5f;
        public const float MaxUnits = 15f;
        /// <summary>"아주 멀리"(3배)까지.</summary>
        public const float MaxScale = 3f;

        private readonly ZoneGraph _graph;
        private readonly ZoneKnowledge _knowledge;
        private readonly HashSet<ActionType> _allowed;
        private readonly int _maxActions;
        private readonly System.Func<string, bool> _isEntryOpen;

        /// <param name="isEntryOpen">지금 월드에서 문·창문이 열려 있는지 (없으면 항상 닫힌 것으로 본다).</param>
        public ActionValidator(ZoneGraph graph, ZoneKnowledge knowledge, IEnumerable<ActionType> allowedActions,
            int maxActions = ActionExecutor.MaxActionsPerPrompt, System.Func<string, bool> isEntryOpen = null)
        {
            _graph = graph;
            _knowledge = knowledge;
            _allowed = new HashSet<ActionType>(allowedActions);
            _maxActions = maxActions;
            _isEntryOpen = isEntryOpen;
        }

        public ValidationResult Validate(InterpretResult interpretation, string currentZoneId)
        {
            var result = new ValidationResult();
            var actions = interpretation.Actions;

            if (actions.Count == 0)
            {
                result.Errors.Add(string.IsNullOrEmpty(interpretation.Notes) ? "어떤 행동인지 알 수 없다" : interpretation.Notes);
                return result;
            }

            // 개수 초과는 아무것도 실행하지 않고 수정 안내만 한다 (임의로 자르지 않음). 방향 전환은 세지 않는다.
            int counted = ActionRules.CountTowardLimit(actions.Select(a => a.Type));
            if (interpretation.OverLimit || counted > _maxActions)
            {
                result.Errors.Add($"행동이 {counted}개입니다. {_maxActions}개 이하로 줄여주세요");
                return result;
            }
            // 이 프롬프트 안에서 앞선 행동으로 열린 출입구 (문 열기·창문 깨기 등)
            var openedEntries = new HashSet<string>();
            for (int i = 0; i < actions.Count; i++)
            {
                string error = ValidateOne(actions[i], openedEntries);
                if (error != null) result.Errors.Add($"[{i + 1}] {ActionNames.Describe(actions[i])}: {error}");
            }
            return result;
        }

        private string ValidateOne(ActionRequest action, HashSet<string> openedEntries)
        {
            if (action.Type == ActionType.None || !_allowed.Contains(action.Type))
                return "아직 할 수 없는 행동이다";

            if (action.Unit == AmountUnit.Seconds && (action.Amount <= 0f || action.Amount > MaxSeconds))
                return $"시간은 {MaxSeconds}초까지만 지정할 수 있다";
            if (action.Unit == AmountUnit.Units && (action.Amount <= 0f || action.Amount > MaxUnits))
                return $"거리는 {MaxUnits}칸까지만 지정할 수 있다";
            if (action.Unit == AmountUnit.Scale && (action.Amount <= 0f || action.Amount > MaxScale))
                return "그렇게 멀리는 갈 수 없다";

            var requiredKind = ActionTargeting.RequiredKind(action.Type);
            if (requiredKind.HasValue)
            {
                string error = ValidateTarget(action, requiredKind.Value);
                if (error != null) return error;
                if (ActionTargeting.OpensEntry(action.Type)) openedEntries.Add(action.Target);
                return null;
            }

            if (action.Type == ActionType.Enter) return ValidateEnter(action, openedEntries);

            // 이동 행동의 목표 구역은 있으면 공개돼 있어야 한다.
            if (!string.IsNullOrEmpty(action.Target))
            {
                if (_graph.Get(action.Target) == null) return "그런 곳은 없다";
                if (!_knowledge.IsRevealed(action.Target)) return "아직 모르는 곳이다";
            }
            return null;
        }

        private string ValidateTarget(ActionRequest action, ZoneKind requiredKind)
        {
            string kindName = ActionTargeting.KindName(requiredKind);
            if (string.IsNullOrEmpty(action.Target)) return $"어느 {kindName}인지 모르겠다. 목표를 클릭하세요";

            var zone = _graph.Get(action.Target);
            if (zone == null) return "그런 곳은 없다";
            if (!_knowledge.IsRevealed(zone.Id)) return "아직 모르는 곳이다";
            if (zone.Kind != requiredKind) return $"그곳에는 {kindName}이(가) 없다";
            if (System.Array.IndexOf(zone.Affordances, action.Type) < 0) return "그곳에서는 할 수 없다";
            return null;
        }

        private string ValidateEnter(ActionRequest action, HashSet<string> openedEntries)
        {
            if (string.IsNullOrEmpty(action.Target)) return "어디로 들어갈지 모르겠다. 목표를 클릭하세요";

            var zone = _graph.Get(action.Target);
            if (zone == null) return "그런 곳은 없다";
            if (!_knowledge.IsRevealed(zone.Id)) return "아직 모르는 곳이다";

            // 문·창문으로 들어가려면 이미 열려 있거나, 같은 프롬프트에서 먼저 열거나 깨야 한다.
            bool open = openedEntries.Contains(zone.Id) || (_isEntryOpen != null && _isEntryOpen(zone.Id));
            if (zone.Kind == ZoneKind.Door && !open) return "문을 먼저 열어야 한다";
            if (zone.Kind == ZoneKind.Window && !open) return "창문을 먼저 깨거나 열어야 한다";
            return null;
        }
    }
}
