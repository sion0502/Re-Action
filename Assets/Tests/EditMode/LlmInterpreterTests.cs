using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using ReAction.Actions;
using ReAction.Interpretation;
using ReAction.Interpretation.Llm;
using ReAction.Stage;
using UnityEngine;

namespace ReAction.Tests
{
    /// <summary>
    /// LLM 연동 중 네트워크 없이 확인할 수 있는 부분: 응답 해석, 실패 시 키워드 해석기로 대체, 캐시.
    /// 실제 API 호출은 테스트하지 않는다 (키·비용·네트워크 필요).
    /// </summary>
    public sealed class LlmInterpreterTests
    {
        private static readonly ActionType[] Allowed =
            { ActionType.Run, ActionType.Jump, ActionType.Roll, ActionType.Slash, ActionType.KickDoor, ActionType.Enter, ActionType.Turn };

        private MockInterpreter _mock;

        [OneTimeSetUp]
        public void LoadKeywords()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Data/mock_keywords.json"));
            _mock = new MockInterpreter(MockKeywordTable.FromJson(json));
        }

        private static InterpretRequest Request(string prompt) => new InterpretRequest
        {
            Prompt = prompt,
            CurrentZoneId = "EXT",
            RevealedZones = new List<ZoneData>
            {
                new ZoneData { Id = "EXT", Kind = ZoneKind.Exterior, Connections = new[] { "Door_A" } },
                new ZoneData { Id = "Door_A", Kind = ZoneKind.Door, Affordances = new[] { ActionType.KickDoor } },
            },
        };

        [Test]
        public void ParseResult_MapsActionsAmountsAndTargets()
        {
            const string json = "{\"actions\":[" +
                                "{\"type\":\"KickDoor\",\"direction\":\"None\",\"target\":\"Door_A\",\"amount\":0,\"unit\":\"None\"}," +
                                "{\"type\":\"Run\",\"direction\":\"Right\",\"target\":\"\",\"amount\":3,\"unit\":\"Seconds\"}," +
                                "{\"type\":\"Turn\",\"direction\":\"None\",\"target\":\"\",\"amount\":0,\"unit\":\"None\"}" +
                                "],\"notes\":\"\"}";

            var r = LlmPrompt.ParseResult(json, 3);

            Assert.That(r.Actions.Count, Is.EqualTo(3));
            Assert.That(r.Actions[0].Target, Is.EqualTo("Door_A"));
            Assert.That(r.Actions[1].Unit, Is.EqualTo(AmountUnit.Seconds));
            Assert.That(r.Actions[1].Amount, Is.EqualTo(3f));
            Assert.That(r.Actions[1].Target, Is.Null);
            Assert.That(r.OverLimit, Is.False);
        }

        [Test]
        public void ParseResult_FourCountedActions_IsOverLimit_NotTruncated()
        {
            string one = "{\"type\":\"Slash\",\"direction\":\"None\",\"target\":\"\",\"amount\":0,\"unit\":\"None\"}";
            string json = $"{{\"actions\":[{one},{one},{one},{one}],\"notes\":\"\"}}";

            var r = LlmPrompt.ParseResult(json, 3);

            Assert.That(r.Actions.Count, Is.EqualTo(4));
            Assert.That(r.OverLimit, Is.True);
        }

        [Test]
        public void ParseResult_UnitWithZeroAmount_MeansNoAmount()
        {
            const string json = "{\"actions\":[{\"type\":\"Run\",\"direction\":\"Forward\",\"target\":\"\",\"amount\":0,\"unit\":\"Units\"}],\"notes\":\"\"}";

            var r = LlmPrompt.ParseResult(json, 3);

            Assert.That(r.Actions[0].HasAmount, Is.False);
        }

        [Test]
        public void ExtractJson_TakesLastTextBlock_AndRejectsRefusal()
        {
            const string ok = "{\"stop_reason\":\"end_turn\",\"content\":[{\"type\":\"thinking\",\"thinking\":\"\"}," +
                              "{\"type\":\"text\",\"text\":\"{\\\"actions\\\":[],\\\"notes\\\":\\\"x\\\"}\"}]," +
                              "\"usage\":{\"input_tokens\":10,\"output_tokens\":5}}";
            Assert.That(ClaudeProvider.ExtractJson(ok), Is.EqualTo("{\"actions\":[],\"notes\":\"x\"}"));

            const string refused = "{\"stop_reason\":\"refusal\",\"content\":[]}";
            Assert.Throws<LlmException>(() => ClaudeProvider.ExtractJson(refused));
        }

        [Test]
        public void SchemaAndSystemPrompt_AreStable()
        {
            // 프롬프트 캐싱이 되려면 같은 입력에 대해 바이트 단위로 같아야 한다.
            Assert.That(LlmPrompt.BuildSystemPrompt(Allowed, 3), Is.EqualTo(LlmPrompt.BuildSystemPrompt(Allowed, 3)));
            var schema = LlmPrompt.BuildSchema(Allowed);
            Assert.That(schema["additionalProperties"]?.Value<bool>(), Is.False);
            Assert.That(schema.ToString(), Does.Contain("KickDoor"));
        }

        [Test]
        public void ProviderFailure_FallsBackToMock()
        {
            var interpreter = new LlmInterpreter(new FakeProvider(_ => throw new LlmException("timeout")), _mock, Allowed);

            var r = interpreter.InterpretAsync(Request("오른쪽으로 달려"), CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(r.Source, Is.EqualTo(InterpretSource.MockFallback));
            Assert.That(r.FallbackReason, Does.Contain("timeout"));
            Assert.That(r.Actions[0].Type, Is.EqualTo(ActionType.Run));
        }

        [Test]
        public void SameRequest_IsCached()
        {
            int calls = 0;
            var provider = new FakeProvider(_ =>
            {
                calls++;
                return "{\"actions\":[{\"type\":\"Jump\",\"direction\":\"None\",\"target\":\"\",\"amount\":0,\"unit\":\"None\"}],\"notes\":\"\"}";
            });
            var interpreter = new LlmInterpreter(provider, _mock, Allowed);

            var first = interpreter.InterpretAsync(Request("점프"), CancellationToken.None).GetAwaiter().GetResult();
            var second = interpreter.InterpretAsync(Request("점프"), CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(first.Source, Is.EqualTo(InterpretSource.Llm));
            Assert.That(second.Source, Is.EqualTo(InterpretSource.LlmCache));
            Assert.That(second.Actions[0].Type, Is.EqualTo(ActionType.Jump));
        }

        private sealed class FakeProvider : ILlmProvider
        {
            private readonly Func<string, string> _respond;

            public FakeProvider(Func<string, string> respond) => _respond = respond;

            public string Name => "Fake";

            public Task<string> CompleteJsonAsync(string systemPrompt, string userContent, JObject schema, CancellationToken ct) =>
                Task.FromResult(_respond(userContent));
        }
    }
}
