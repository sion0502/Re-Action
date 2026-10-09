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

        public List<Pattern<ActionType>> Actions { get; } = new List<Pattern<ActionType>>();
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

            foreach (var entry in data.actions ?? Array.Empty<ActionEntry>())
            {
                if (!Enum.TryParse(entry.type, out ActionType type))
                    throw new ArgumentException($"Unknown ActionType in keyword table: {entry.type}");
                foreach (var keyword in entry.keywords)
                    table.Actions.Add(new Pattern<ActionType>(Compile(keyword), type));
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
            AddAll(table.Slow, data.slow);
            AddAll(table.Fast, data.fast);
            if (data.briefSeconds > 0f) table.BriefSeconds = data.briefSeconds;

            return table;
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
            public DirectionEntry[] directions;
            public string[] ignore;
            public NumberWord[] numberWords;
            public string[] secondUnits;
            public string[] distanceUnits;
            public string[] repeatUnits;
            public string[] untilBlocked;
            public string[] brief;
            public float briefSeconds;
            public string[] slow;
            public string[] fast;
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
            public string[] keywords;
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
