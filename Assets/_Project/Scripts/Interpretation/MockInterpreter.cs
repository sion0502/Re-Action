using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReAction.Actions;
using ReAction.Stage;

namespace ReAction.Interpretation
{
    /// <summary>
    /// LLM 없이 동작하는 한국어 키워드 해석기. 항상 사용 가능해야 하며 LLM 실패 시 폴백이 된다.
    /// 문장에 나온 순서대로 행동을 나열하고, 개수가 많아도 자르지 않는다.
    /// </summary>
    public sealed class MockInterpreter : IActionInterpreter
    {
        private readonly MockKeywordTable _table;

        public MockInterpreter(MockKeywordTable table)
        {
            _table = table;
        }

        public Task<InterpretResult> InterpretAsync(InterpretRequest request, CancellationToken ct) =>
            Task.FromResult(Interpret(request));

        public InterpretResult Interpret(InterpretRequest request)
        {
            var result = new InterpretResult { Raw = request.Prompt ?? "" };
            string text = request.Prompt ?? "";

            var actions = SelectLongestNonOverlapping(FindAll(text, _table.Actions));
            var blocked = actions.Select(m => (m.Start, m.End)).ToList();
            blocked.AddRange(FindIgnored(text));
            var directions = SelectLongestNonOverlapping(
                FindAll(text, _table.Directions).Where(d => !Overlaps(d, blocked)));

            var requests = actions.Select(m => new ActionRequest(m.Value)).ToList();
            AssignDirections(actions, directions, requests);
            AssignTargets(request, requests);

            result.Actions = requests;
            result.OverLimit = requests.Count > request.MaxActions;
            if (requests.Count == 0) result.Notes = "어떤 행동인지 알 수 없다";
            return result;
        }

        private readonly struct Match<T>
        {
            public Match(int start, int length, T value)
            {
                Start = start;
                End = start + length;
                Value = value;
            }

            public int Start { get; }
            public int End { get; }
            public T Value { get; }
        }

        private static IEnumerable<Match<T>> FindAll<T>(string text, List<MockKeywordTable.Pattern<T>> patterns)
        {
            foreach (var pattern in patterns)
            {
                foreach (System.Text.RegularExpressions.Match m in pattern.Regex.Matches(text))
                    yield return new Match<T>(m.Index, m.Length, pattern.Value);
            }
        }

        /// <summary>앞에서부터, 같은 위치면 긴 것 우선으로 겹치지 않게 고른다.</summary>
        private static List<Match<T>> SelectLongestNonOverlapping<T>(IEnumerable<Match<T>> matches)
        {
            var selected = new List<Match<T>>();
            int end = 0;
            foreach (var m in matches.OrderBy(m => m.Start).ThenByDescending(m => m.End - m.Start))
            {
                if (m.Start < end) continue;
                selected.Add(m);
                end = m.End;
            }
            return selected;
        }

        private IEnumerable<(int Start, int End)> FindIgnored(string text)
        {
            foreach (var regex in _table.Ignore)
            {
                foreach (System.Text.RegularExpressions.Match m in regex.Matches(text))
                    yield return (m.Index, m.Index + m.Length);
            }
        }

        private static bool Overlaps<T>(Match<T> m, List<(int Start, int End)> ranges) =>
            ranges.Any(r => m.Start < r.End && r.Start < m.End);

        /// <summary>
        /// 방향어는 바로 뒤에 오는 첫 행동에 붙는다 ("왼쪽으로 달려", "앞구르기").
        /// 뒤에 행동이 없으면 앞쪽에서 가장 가까운, 방향이 없는 행동에 붙인다.
        /// </summary>
        private static void AssignDirections(List<Match<ActionType>> actions, List<Match<Direction>> directions,
            List<ActionRequest> requests)
        {
            foreach (var d in directions)
            {
                int index = actions.FindIndex(a => a.Start >= d.End);
                if (index < 0 || requests[index].Direction != Direction.None)
                    index = requests.FindLastIndex(r => r.Direction == Direction.None);
                if (index < 0) continue;

                var r = requests[index];
                r.Direction = d.Value;
                requests[index] = r;
            }
        }

        private static void AssignTargets(InterpretRequest request, List<ActionRequest> requests)
        {
            var target = request.RevealedZones.FirstOrDefault(z => z.Id == request.TargetZoneId);
            string lastEntryPoint = null;

            for (int i = 0; i < requests.Count; i++)
            {
                var r = requests[i];
                var kind = ActionTargeting.RequiredKind(r.Type);

                if (kind.HasValue)
                {
                    r.Target = target != null && target.Kind == kind.Value
                        ? target.Id
                        : GuessZone(request, kind.Value);
                    lastEntryPoint = r.Target;
                }
                else if (r.Type == ActionType.Enter)
                {
                    // "문을 걷어차고 들어가" → 방금 연 문으로 들어간다.
                    r.Target = lastEntryPoint ?? target?.Id;
                }
                else if (IsMove(r.Type) && target != null && target.Kind.IsSpace())
                {
                    r.Target = target.Id;
                }

                requests[i] = r;
            }
        }

        /// <summary>목표를 안 찍었을 때: 현재 구역과 연결된 해당 종류 구역이 하나뿐이면 그것으로 추정.</summary>
        private static string GuessZone(InterpretRequest request, ZoneKind kind)
        {
            var current = request.RevealedZones.FirstOrDefault(z => z.Id == request.CurrentZoneId);
            var candidates = request.RevealedZones.Where(z => z.Kind == kind).ToList();
            if (current != null)
            {
                var connected = candidates
                    .Where(z => current.Connections.Contains(z.Id) || z.Connections.Contains(current.Id))
                    .ToList();
                if (connected.Count == 1) return connected[0].Id;
            }
            return candidates.Count == 1 ? candidates[0].Id : null;
        }

        private static bool IsMove(ActionType type) =>
            type == ActionType.Run || type == ActionType.Walk || type == ActionType.Sprint;
    }
}
