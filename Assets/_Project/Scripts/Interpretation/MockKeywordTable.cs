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

            return table;
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
