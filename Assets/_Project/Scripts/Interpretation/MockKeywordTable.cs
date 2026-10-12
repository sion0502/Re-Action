using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ReAction.Actions;
using UnityEngine;

namespace ReAction.Interpretation
{
    /// <summary>
    /// MockInterpreter 키워드 사전 (JSON: Assets/_Project/Data/mock_keywords.json).
    /// 키워드 안의 공백은 "조사 + 띄어쓰기"를 허용한다. 예: "문 걷어차" → "문을 걷어차", "문걷어차".
    /// </summary>
    public sealed class MockKeywordTable
    {
        // 긴 조사부터 시도하도록 정렬해 둔다.
        private const string Particles = "(?:에서|으로|을|를|이|가|은|는|에|로|의)?\\s*";

        public readonly struct Pattern<T>
        {
            public Pattern(Regex regex, T value)
            {
                Regex = regex;
                Value = value;
            }

            public Regex Regex { get; }
            public T Value { get; }
        }

        /// <summary>
        /// JSON type "ClimbUp": "올라가"처럼 무엇을 타고 오를지 상황에 따라 정해지는 말.
        /// 파싱 중에는 ActionType.None으로 두고, 해석기가 목표·현재 구역을 보고 벽 타기/사다리로 정한다.
        /// </summary>
        public const string ClimbUpType = "ClimbUp";

        /// <summary>JSON type "ClimbDown": "내려가", "아래층". ClimbUp처럼 상황에 따라 정하되 사다리를 내려간다(Direction.Down).</summary>
        public const string ClimbDownType = "ClimbDown";

        /// <summary>ClimbDown 키워드. 이 말이 들어간 오르기 행동은 내려가기로 본다 ("사다리 타고 내려가").</summary>
        public List<Regex> ClimbDownWords { get; } = new List<Regex>();

        /// <summary>키워드 하나가 행동 여러 개가 될 수 있다 ("점프 공격" → 점프, 베기).</summary>
        public List<Pattern<ActionType[]>> Actions { get; } = new List<Pattern<ActionType[]>>();
        public List<Pattern<Direction>> Directions { get; } = new List<Pattern<Direction>>();
        public List<Regex> Ignore { get; } = new List<Regex>();

        // 수식어: "3초 동안", "두 칸", "두 번", "벽까지", "잠깐", "천천히", "빨리"
        public Regex Amount { get; private set; }
        public Dictionary<string, float> NumberWords { get; } = new Dictionary<string, float>();
        public HashSet<string> SecondUnits { get; } = new HashSet<string>();
        public HashSet<string> DistanceUnits { get; } = new HashSet<string>();
        public HashSet<string> RepeatUnits { get; } = new HashSet<string>();
        public List<Regex> UntilBlocked { get; } = new List<Regex>();
        public List<Regex> Brief { get; } = new List<Regex>();
        public float BriefSeconds { get; private set; } = 0.5f;
        /// <summary>정도를 나타내는 말 → 기본 거리·시간의 배수 ("조금" 0.5, "멀리" 2).</summary>
        public List<Pattern<float>> ScaleWords { get; } = new List<Pattern<float>>();
        public List<Regex> Slow { get; } = new List<Regex>();
        public List<Regex> Fast { get; } = new List<Regex>();

        /// <summary>"3", "1.5", "세" 같은 수 표현을 숫자로. 모르면 null.</summary>
        public float? ParseNumber(string text)
        {
            if (float.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float value)) return value;
            return NumberWords.TryGetValue(text, out value) ? value : (float?)null;
        }

        public static MockKeywordTable FromJson(string json)
        {
            var data = JsonUtility.FromJson<Data>(json) ?? throw new ArgumentException("Invalid keyword json");
            var table = new MockKeywordTable();

            // 아직 구현하지 않은 행동은 가장 비슷한 행동으로 바꾼다 ("찔러" → 베기). 구현하면 별칭에서 지운다.
            var aliases = new Dictionary<string, string>();
            foreach (var alias in data.aliases ?? Array.Empty<AliasEntry>()) aliases[alias.from] = alias.to;

            foreach (var entry in data.actions ?? Array.Empty<ActionEntry>())
            {
                var names = entry.types != null && entry.types.Length > 0 ? entry.types : new[] { entry.type };
                var types = names.Select(name => ParseActionType(name, aliases)).ToArray();
                foreach (var keyword in entry.keywords)
                {
                    table.Actions.Add(new Pattern<ActionType[]>(Compile(keyword), types));
                    if (entry.type == ClimbDownType) table.ClimbDownWords.Add(Compile(keyword));
                }
            }

            foreach (var entry in data.directions ?? Array.Empty<DirectionEntry>())
            {
                if (!Enum.TryParse(entry.direction, out Direction direction))
                    throw new ArgumentException($"Unknown Direction in keyword table: {entry.direction}");
                foreach (var keyword in entry.keywords)
                    table.Directions.Add(new Pattern<Direction>(Compile(keyword), direction));
            }

            foreach (var keyword in data.ignore ?? Array.Empty<string>())
                table.Ignore.Add(Compile(keyword));

            foreach (var n in data.numberWords ?? Array.Empty<NumberWord>()) table.NumberWords[n.word] = n.value;
            foreach (var u in data.secondUnits ?? Array.Empty<string>()) table.SecondUnits.Add(u);
            foreach (var u in data.distanceUnits ?? Array.Empty<string>()) table.DistanceUnits.Add(u);
            foreach (var u in data.repeatUnits ?? Array.Empty<string>()) table.RepeatUnits.Add(u);
            table.Amount = CompileAmount(table);
            AddAll(table.UntilBlocked, data.untilBlocked);
            AddAll(table.Brief, data.brief);
            foreach (var s in data.scaleWords ?? Array.Empty<ScaleWord>())
                table.ScaleWords.Add(new Pattern<float>(Compile(s.word), s.scale));
            AddAll(table.Slow, data.slow);
            AddAll(table.Fast, data.fast);
            if (data.briefSeconds > 0f) table.BriefSeconds = data.briefSeconds;

            return table;
        }

        private static ActionType ParseActionType(string name, Dictionary<string, string> aliases)
        {
            if (name == ClimbUpType || name == ClimbDownType) return ActionType.None;
            if (aliases.TryGetValue(name, out var alias)) name = alias;
            if (!Enum.TryParse(name, out ActionType type) || type == ActionType.None)
                throw new ArgumentException($"Unknown ActionType in keyword table: {name}");
            return type;
        }

        private static void AddAll(List<Regex> target, string[] keywords)
        {
            foreach (var keyword in keywords ?? Array.Empty<string>()) target.Add(Compile(keyword));
        }

        /// <summary>(수)(단위)[ 동안|간] — 수는 숫자 또는 한국어 수사, 긴 단어부터 시도한다.</summary>
        private static Regex CompileAmount(MockKeywordTable table)
        {
            var numbers = table.NumberWords.Keys.OrderByDescending(w => w.Length).Select(Regex.Escape);
            var units = table.SecondUnits.Concat(table.DistanceUnits).Concat(table.RepeatUnits)
                .OrderByDescending(u => u.Length).Select(Regex.Escape).ToList();
            if (units.Count == 0) return null;
            string number = $@"(?<num>\d+(?:\.\d+)?|{string.Join("|", numbers)})";
            return new Regex($@"{number}\s*(?<unit>{string.Join("|", units)})(?:\s*(?:동안|간))?",
                RegexOptions.CultureInvariant);
        }

        private static Regex Compile(string keyword)
        {
            var parts = keyword.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape);
            return new Regex(string.Join(Particles, parts), RegexOptions.CultureInvariant);
        }

#pragma warning disable 0649 // JsonUtility가 채운다
        [Serializable]
        private sealed class Data
        {
            public ActionEntry[] actions;
            public AliasEntry[] aliases;
            public DirectionEntry[] directions;
            public string[] ignore;
            public NumberWord[] numberWords;
            public string[] secondUnits;
            public string[] distanceUnits;
            public string[] repeatUnits;
            public string[] untilBlocked;
            public string[] brief;
            public float briefSeconds;
            public ScaleWord[] scaleWords;
            public string[] slow;
            public string[] fast;
        }

        [Serializable]
        private sealed class ScaleWord
        {
            public string word;
            public float scale;
        }

        [Serializable]
        private sealed class NumberWord
        {
            public string word;
            public float value;
        }

        [Serializable]
        private sealed class ActionEntry
        {
            public string type;
            /// <summary>type 대신 쓰면 키워드 하나가 행동 여러 개가 된다.</summary>
            public string[] types;
            public string[] keywords;
        }

        [Serializable]
        private sealed class AliasEntry
        {
            public string from;
            public string to;
        }

        [Serializable]
        private sealed class DirectionEntry
        {
            public string direction;
            public string[] keywords;
        }
#pragma warning restore 0649
    }
}
