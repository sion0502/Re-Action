using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ReAction.Actions;
using ReAction.Interpretation;
using ReAction.Stage;
using UnityEngine;

namespace ReAction.Tests
{
    public sealed class MockInterpreterTests
    {
        private MockInterpreter _interpreter;

        [OneTimeSetUp]
        public void LoadKeywords()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Data/mock_keywords.json"));
            _interpreter = new MockInterpreter(MockKeywordTable.FromJson(json));
        }

        private InterpretResult Interpret(string prompt, string current = null, string target = null,
            List<ZoneData> zones = null) =>
            _interpreter.Interpret(new InterpretRequest
            {
                Prompt = prompt,
                CurrentZoneId = current,
                TargetZoneId = target,
                RevealedZones = zones ?? new List<ZoneData>(),
            });

        private static ActionType[] Types(InterpretResult r) => r.Actions.Select(a => a.Type).ToArray();

        [Test]
        public void KickDoorEnterRoll_ThreeActionsInOrder()
        {
            var r = Interpret("문을 걷어차고 들어가서 앞구르기");

            Assert.That(Types(r), Is.EqualTo(new[] { ActionType.KickDoor, ActionType.Enter, ActionType.Roll }));
            Assert.That(r.Actions[2].Direction, Is.EqualTo(Direction.Forward));
            Assert.That(r.OverLimit, Is.False);
        }

        [Test]
        public void FourActions_AreKeptAndMarkedOverLimit()
        {
            var r = Interpret("문을 걷어차고 들어가서 앞구르기 후 공격");

            Assert.That(Types(r), Is.EqualTo(new[]
                { ActionType.KickDoor, ActionType.Enter, ActionType.Roll, ActionType.Slash }));
            Assert.That(r.OverLimit, Is.True);
        }

        [Test]
        public void DirectionAttachesToFollowingAction()
        {
            var r = Interpret("왼쪽으로 달려서 점프");

            Assert.That(Types(r), Is.EqualTo(new[] { ActionType.Run, ActionType.Jump }));
            Assert.That(r.Actions[0].Direction, Is.EqualTo(Direction.Left));
            Assert.That(r.Actions[1].Direction, Is.EqualTo(Direction.None));
        }

        [Test]
        public void TemporalWordIsNotADirection()
        {
            var r = Interpret("기다린 뒤에 구르기");

            Assert.That(Types(r), Is.EqualTo(new[] { ActionType.Wait, ActionType.Roll }));
            Assert.That(r.Actions[1].Direction, Is.EqualTo(Direction.None));
        }

        [Test]
        public void LongerKeywordWins()
        {
            Assert.That(Types(Interpret("점프 공격")), Is.EqualTo(new[] { ActionType.JumpAttack }));
            Assert.That(Types(Interpret("창문을 깨고 들어가")), Is.EqualTo(new[] { ActionType.CrashWindow, ActionType.Enter }));
            Assert.That(Types(Interpret("적을 걷어차")), Is.EqualTo(new[] { ActionType.Kick }));
        }

        [Test]
        public void ParticlesAndSpacingAreOptional()
        {
            Assert.That(Types(Interpret("문걷어차")), Is.EqualTo(new[] { ActionType.KickDoor }));
            Assert.That(Types(Interpret("문을 열어")), Is.EqualTo(new[] { ActionType.OpenDoor }));
            Assert.That(Types(Interpret("벽을 타고 올라가")), Is.EqualTo(new[] { ActionType.WallClimb }));
        }

        [Test]
        public void TurnAround_IsFreeAndNotCounted()
        {
            var r = Interpret("뒤돌아서 문을 걷어차고 들어가서 구르기");

            Assert.That(Types(r), Is.EqualTo(new[]
                { ActionType.Turn, ActionType.KickDoor, ActionType.Enter, ActionType.Roll }));
            Assert.That(r.OverLimit, Is.False);
        }

        [Test]
        public void TurnToSide_TakesDirection()
        {
            var r = Interpret("오른쪽을 보고 점프");

            Assert.That(Types(r), Is.EqualTo(new[] { ActionType.Turn, ActionType.Jump }));
            Assert.That(r.Actions[0].Direction, Is.EqualTo(Direction.Right));
        }

        [Test]
        public void FourCountedActionsPlusTurn_StillOverLimit()
        {
            var r = Interpret("뒤돌아서 문을 걷어차고 들어가서 구르고 공격");
            Assert.That(r.OverLimit, Is.True);
        }

        [Test]
        public void SecondsAmount_AttachesToFollowingAction()
        {
            var r = Interpret("3초 동안 달려");

            Assert.That(Types(r), Is.EqualTo(new[] { ActionType.Run }));
            Assert.That(r.Actions[0].Unit, Is.EqualTo(AmountUnit.Seconds));
            Assert.That(r.Actions[0].Amount, Is.EqualTo(3f));
        }

        [Test]
        public void AmountAfterAction_AttachesToPreviousAction()
        {
            var r = Interpret("오른쪽으로 달려 1.5초간");

            Assert.That(r.Actions[0].Unit, Is.EqualTo(AmountUnit.Seconds));
            Assert.That(r.Actions[0].Amount, Is.EqualTo(1.5f));
            Assert.That(r.Actions[0].Direction, Is.EqualTo(Direction.Right));
        }

        [Test]
        public void KoreanNumberDistance()
        {
            var r = Interpret("왼쪽으로 두 칸 걸어");

            Assert.That(Types(r), Is.EqualTo(new[] { ActionType.Walk }));
            Assert.That(r.Actions[0].Unit, Is.EqualTo(AmountUnit.Units));
            Assert.That(r.Actions[0].Amount, Is.EqualTo(2f));
            Assert.That(r.Actions[0].Direction, Is.EqualTo(Direction.Left));
        }

        [Test]
        public void UntilBlockedAndBrief()
        {
            var r = Interpret("벽까지 달리고 잠깐 기다려");

            Assert.That(Types(r), Is.EqualTo(new[] { ActionType.Run, ActionType.Wait }));
            Assert.That(r.Actions[0].Unit, Is.EqualTo(AmountUnit.UntilBlocked));
            Assert.That(r.Actions[1].Unit, Is.EqualTo(AmountUnit.Seconds));
            Assert.That(r.Actions[1].Amount, Is.EqualTo(0.5f));
        }

        [Test]
        public void Repeat_ExpandsAndCountsTowardLimit()
        {
            Assert.That(Types(Interpret("두 번 베어")), Is.EqualTo(new[] { ActionType.Slash, ActionType.Slash }));

            var r = Interpret("달려서 세 번 베어");
            Assert.That(r.Actions.Count, Is.EqualTo(4));
            Assert.That(r.OverLimit, Is.True);
        }

        [Test]
        public void SpeedWords_ChangeMoveType()
        {
            Assert.That(Types(Interpret("천천히 달려")), Is.EqualTo(new[] { ActionType.Walk }));
            Assert.That(Types(Interpret("빨리 달려")), Is.EqualTo(new[] { ActionType.Sprint }));
            Assert.That(Types(Interpret("최대한 빨리 달려")), Is.EqualTo(new[] { ActionType.Sprint }));
        }

        [Test]
        public void NumberWordInsideActionKeyword_IsNotAnAmount()
        {
            // "문 열"의 "열"은 숫자 10이 아니다
            var r = Interpret("문을 열고 들어가");
            Assert.That(Types(r), Is.EqualTo(new[] { ActionType.OpenDoor, ActionType.Enter }));
            Assert.That(r.Actions[0].HasAmount, Is.False);
        }

        [Test]
        public void NewSynonyms()
        {
            Assert.That(Types(Interpret("앞으로 전진")), Is.EqualTo(new[] { ActionType.Run }));
            Assert.That(Types(Interpret("후퇴해")), Is.EqualTo(new[] { ActionType.Backstep }));
            Assert.That(Types(Interpret("위층으로 가")), Is.EqualTo(new[] { ActionType.ClimbLadder }));
            Assert.That(Types(Interpret("적을 처치해")), Is.EqualTo(new[] { ActionType.Slash }));
        }

        [Test]
        public void UnknownPrompt_NoActionsWithNotes()
        {
            var r = Interpret("안녕하세요");

            Assert.That(r.Actions, Is.Empty);
            Assert.That(r.Notes, Is.Not.Empty);
        }

        [Test]
        public void DoorTarget_UsesClickedDoorAndEnterFollowsIt()
        {
            var zones = new List<ZoneData>
            {
                new ZoneData { Id = "EXT", Kind = ZoneKind.Exterior, Connections = new[] { "Door_A", "Door_B" } },
                new ZoneData { Id = "Door_A", Kind = ZoneKind.Door },
                new ZoneData { Id = "Door_B", Kind = ZoneKind.Door },
            };

            var r = Interpret("문을 걷어차고 들어가", current: "EXT", target: "Door_B", zones: zones);

            Assert.That(r.Actions[0].Target, Is.EqualTo("Door_B"));
            Assert.That(r.Actions[1].Target, Is.EqualTo("Door_B"));
        }

        [Test]
        public void DoorTarget_GuessedWhenOnlyOneConnectedDoor()
        {
            var zones = new List<ZoneData>
            {
                new ZoneData { Id = "EXT", Kind = ZoneKind.Exterior, Connections = new[] { "Door_A" } },
                new ZoneData { Id = "Door_A", Kind = ZoneKind.Door },
                new ZoneData { Id = "Door_Far", Kind = ZoneKind.Door },
            };

            var r = Interpret("문을 열어", current: "EXT", zones: zones);

            Assert.That(r.Actions[0].Target, Is.EqualTo("Door_A"));
        }
    }
}
