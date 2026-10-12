using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReAction.Actions;
using ReAction.Stage;
using UnityEngine;

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

            var actions = MergeDuplicates(text, Expand(SelectLongestNonOverlapping(FindAll(text, _table.Actions))));
            var actionRanges = actions.Select(m => (m.Start, m.End)).ToList();
            var modifiers = SelectLongestNonOverlapping(FindModifiers(text).Where(m => !Overlaps(m, actionRanges)));

            var blocked = new List<(int Start, int End)>(actionRanges);
            blocked.AddRange(modifiers.Select(m => (m.Start, m.End)));
            blocked.AddRange(FindIgnored(text));
            var directions = SelectLongestNonOverlapping(
                FindAll(text, _table.Directions).Where(d => !Overlaps(d, blocked)));

            var requests = actions.Select(m => new ActionRequest(m.Value)).ToList();
            AssignDirections(actions, directions, requests);
            var repeats = ApplyModifiers(actions, modifiers, requests);
            requests = ExpandRepeats(requests, repeats);
            ResolveClimbUp(request, requests);
            AssignTargets(request, requests);

            result.Actions = requests;
            result.OverLimit = ActionRules.CountTowardLimit(requests.Select(r => r.Type)) > request.MaxActions;
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

        /// <summary>키워드 하나가 행동 여러 개인 경우 ("점프 공격") 같은 위치의 행동들로 펼친다.</summary>
        private static List<Match<ActionType>> Expand(List<Match<ActionType[]>> matches)
        {
            var expanded = new List<Match<ActionType>>();
            foreach (var m in matches)
            {
                foreach (var type in m.Value)
                    expanded.Add(new Match<ActionType>(m.Start, m.End - m.Start, type));
            }
            return expanded;
        }

        // "점프해서 넘어", "살금살금 걸어"처럼 같은 동작을 두 단어로 말한 것. "베고 베어"는 두 번으로 둔다.
        private static readonly System.Text.RegularExpressions.Regex SameMotionGap =
            new System.Text.RegularExpressions.Regex(@"^\s*(?:해서|하여|해|서)?\s*$");

        private static readonly HashSet<ActionType> MergeableMoves = new HashSet<ActionType>
            { ActionType.Run, ActionType.Walk, ActionType.Sprint, ActionType.Jump };

        /// <summary>
        /// 한 동작을 두 단어로 말한 것을 하나로 합친다.
        /// "벽을 타고 올라가", "사다리 타고 올라가"의 "올라가"는 앞의 오르기 행동에 포함된다.
        /// </summary>
        private static List<Match<ActionType>> MergeDuplicates(string text, List<Match<ActionType>> actions)
        {
            var merged = new List<Match<ActionType>>();
            foreach (var m in actions)
            {
                if (merged.Count > 0)
                {
                    var prev = merged[merged.Count - 1];
                    bool climbContinues = m.Value == ActionType.None &&
                        (prev.Value == ActionType.WallClimb || prev.Value == ActionType.ClimbLadder);
                    bool sameMove = m.Value == prev.Value && MergeableMoves.Contains(m.Value) && m.Start >= prev.End &&
                        SameMotionGap.IsMatch(text.Substring(prev.End, m.Start - prev.End));
                    if (climbContinues || sameMove)
                    {
                        merged[merged.Count - 1] = new Match<ActionType>(prev.Start, m.End - prev.Start, prev.Value);
                        continue;
                    }
                }
                merged.Add(m);
            }
            return merged;
        }

        /// <summary>
        /// "올라가"(ClimbUp)를 상황에 맞는 오르기로 정한다: 목표가 벽이면 벽 타기, 사다리면 사다리.
        /// 목표가 없으면 건물 밖에서는 벽 타기, 안에서는 사다리.
        /// </summary>
        private static void ResolveClimbUp(InterpretRequest request, List<ActionRequest> requests)
        {
            var target = request.RevealedZones.FirstOrDefault(z => z.Id == request.TargetZoneId);
            var current = request.RevealedZones.FirstOrDefault(z => z.Id == request.CurrentZoneId);
            ActionType climb;
            if (target != null && target.Kind == ZoneKind.ExteriorWall) climb = ActionType.WallClimb;
            else if (target != null && target.Kind == ZoneKind.Ladder) climb = ActionType.ClimbLadder;
            else if (current != null && (current.Kind == ZoneKind.Exterior || current.Kind == ZoneKind.ExteriorWall))
                climb = ActionType.WallClimb;
            else climb = ActionType.ClimbLadder;

            for (int i = 0; i < requests.Count; i++)
            {
                if (requests[i].Type != ActionType.None) continue;
                var r = requests[i];
                r.Type = climb;
                requests[i] = r;
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
        /// 수식어(방향·수치·반복·속도)가 붙을 행동을 고른다: 바로 뒤에 오는 첫 행동 ("왼쪽으로 달려", "3초 동안 달려").
        /// 뒤에 행동이 없거나 이미 같은 수식어가 있으면, 수식어 앞쪽에서 가장 가까운 받을 수 있는 행동 ("달려 3초 동안").
        /// </summary>
        private static int FindAttachIndex(List<Match<ActionType>> actions, List<ActionRequest> requests,
            int modifierStart, int modifierEnd, System.Func<ActionRequest, bool> canTake)
        {
            int next = actions.FindIndex(a => a.Start >= modifierEnd);
            if (next >= 0 && canTake(requests[next])) return next;
            for (int i = actions.Count - 1; i >= 0; i--)
            {
                if (actions[i].End <= modifierStart && canTake(requests[i])) return i;
            }
            return -1;
        }

        private static void AssignDirections(List<Match<ActionType>> actions, List<Match<Direction>> directions,
            List<ActionRequest> requests)
        {
            foreach (var d in directions)
            {
                int index = FindAttachIndex(actions, requests, d.Start, d.End, r => r.Direction == Direction.None);
                if (index < 0) continue;
                var r = requests[index];
                r.Direction = d.Value;
                requests[index] = r;
            }
        }

        private enum ModifierKind
        {
            Amount,
            Repeat,
            Slow,
            Fast,
        }

        private readonly struct Modifier
        {
            public Modifier(ModifierKind kind, float value = 0f, AmountUnit unit = AmountUnit.None)
            {
                Kind = kind;
                Value = value;
                Unit = unit;
            }

            public ModifierKind Kind { get; }
            public float Value { get; }
            public AmountUnit Unit { get; }
        }

        private IEnumerable<Match<Modifier>> FindModifiers(string text)
        {
            if (_table.Amount != null)
            {
                foreach (System.Text.RegularExpressions.Match m in _table.Amount.Matches(text))
                {
                    float? value = _table.ParseNumber(m.Groups["num"].Value);
                    if (!value.HasValue) continue;
                    string unit = m.Groups["unit"].Value;
                    Modifier modifier;
                    if (_table.SecondUnits.Contains(unit)) modifier = new Modifier(ModifierKind.Amount, value.Value, AmountUnit.Seconds);
                    else if (_table.DistanceUnits.Contains(unit)) modifier = new Modifier(ModifierKind.Amount, value.Value, AmountUnit.Units);
                    else modifier = new Modifier(ModifierKind.Repeat, value.Value);
                    yield return new Match<Modifier>(m.Index, m.Length, modifier);
                }
            }

            foreach (var m in FindKeywords(text, _table.UntilBlocked))
                yield return new Match<Modifier>(m.Start, m.End - m.Start, new Modifier(ModifierKind.Amount, 0f, AmountUnit.UntilBlocked));
            foreach (var m in FindKeywords(text, _table.Brief))
                yield return new Match<Modifier>(m.Start, m.End - m.Start, new Modifier(ModifierKind.Amount, _table.BriefSeconds, AmountUnit.Seconds));
            foreach (var m in FindKeywords(text, _table.Slow))
                yield return new Match<Modifier>(m.Start, m.End - m.Start, new Modifier(ModifierKind.Slow));
            foreach (var m in FindKeywords(text, _table.Fast))
                yield return new Match<Modifier>(m.Start, m.End - m.Start, new Modifier(ModifierKind.Fast));
        }

        private static IEnumerable<(int Start, int End)> FindKeywords(string text, List<System.Text.RegularExpressions.Regex> patterns)
        {
            foreach (var regex in patterns)
            {
                foreach (System.Text.RegularExpressions.Match m in regex.Matches(text))
                    yield return (m.Index, m.Index + m.Length);
            }
        }

        // 반복은 행동 수에 그대로 센다. 제한(3개) 초과를 알려줄 수 있을 만큼만 펼친다.
        private const int MaxRepeat = 5;

        /// <summary>수치·속도 수식어를 행동에 적용하고, 행동별 반복 횟수를 돌려준다.</summary>
        private static int[] ApplyModifiers(List<Match<ActionType>> actions, List<Match<Modifier>> modifiers,
            List<ActionRequest> requests)
        {
            var repeats = Enumerable.Repeat(1, requests.Count).ToArray();
            foreach (var m in modifiers)
            {
                var mod = m.Value;
                switch (mod.Kind)
                {
                    case ModifierKind.Amount:
                    {
                        int i = FindAttachIndex(actions, requests, m.Start, m.End, r => !r.HasAmount);
                        if (i < 0) break;
                        var r = requests[i];
                        r.Amount = mod.Value;
                        r.Unit = mod.Unit;
                        requests[i] = r;
                        break;
                    }
                    case ModifierKind.Repeat:
                    {
                        int i = FindAttachIndex(actions, requests, m.Start, m.End, r => true);
                        if (i >= 0) repeats[i] = Mathf.Clamp(Mathf.RoundToInt(mod.Value), 1, MaxRepeat);
                        break;
                    }
                    case ModifierKind.Slow:
                    case ModifierKind.Fast:
                    {
                        int i = FindAttachIndex(actions, requests, m.Start, m.End, r => IsMove(r.Type));
                        if (i < 0) break;
                        var r = requests[i];
                        r.Type = mod.Kind == ModifierKind.Slow ? ActionType.Walk : ActionType.Sprint;
                        requests[i] = r;
                        break;
                    }
                }
            }
            return repeats;
        }

        private static List<ActionRequest> ExpandRepeats(List<ActionRequest> requests, int[] repeats)
        {
            var expanded = new List<ActionRequest>();
            for (int i = 0; i < requests.Count; i++)
            {
                for (int n = 0; n < repeats[i]; n++) expanded.Add(requests[i]);
            }
            return expanded;
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
