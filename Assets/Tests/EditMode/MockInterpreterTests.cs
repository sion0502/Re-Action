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
            Assert.That(Types(Interpret("점프 공격")), Is.EqualTo(new[] { ActionType.Jump, ActionType.Slash }));
            Assert.That(Types(Interpret("창문을 깨고 들어가")), Is.EqualTo(new[] { ActionType.CrashWindow, ActionType.Enter }));
            Assert.That(Types(Interpret("문을 걷어차")), Is.EqualTo(new[] { ActionType.KickDoor }));
        }

        [Test]
        public void UnimplementedActions_MapToClosestImplemented()
        {
            Assert.That(Types(Interpret("적을 걷어차")), Is.EqualTo(new[] { ActionType.Slash }));
            Assert.That(Types(Interpret("칼로 찔러")), Is.EqualTo(new[] { ActionType.Slash }));
            Assert.That(Types(Interpret("이단 점프")), Is.EqualTo(new[] { ActionType.Jump }));
            Assert.That(Types(Interpret("구르면서 공격")), Is.EqualTo(new[] { ActionType.Roll, ActionType.Slash }));
        }

        [Test]
        public void GoVerb_IsRunWithDirection()
        {
            var r = Interpret("오른쪽으로 가");
            Assert.That(Types(r), Is.EqualTo(new[] { ActionType.Run }));
            Assert.That(r.Actions[0].Direction, Is.EqualTo(Direction.Right));

            Assert.That(Types(Interpret("앞으로 가자")), Is.EqualTo(new[] { ActionType.Run }));
            // 더 긴 동사가 먼저 잡힌다: "들어가", "걸어가", "사다리로 가"
            Assert.That(Types(Interpret("들어가서 베어")), Is.EqualTo(new[] { ActionType.Enter, ActionType.Slash }));
            Assert.That(Types(Interpret("걸어가서 기다려")), Is.EqualTo(new[] { ActionType.Walk, ActionType.Wait }));
            Assert.That(Types(Interpret("사다리로 가")), Is.EqualTo(new[] { ActionType.ClimbLadder }));
        }

        [Test]
        public void ClimbUp_ResolvedByTargetOrPlace()
        {
            var zones = new List<ZoneData>
            {
                new ZoneData { Id = "EXT", Kind = ZoneKind.Exterior, Connections = new[] { "Wall_W" } },
                new ZoneData { Id = "Wall_W", Kind = ZoneKind.ExteriorWall },
                new ZoneData { Id = "1F", Kind = ZoneKind.Room, Connections = new[] { "Ladder_1" } },
                new ZoneData { Id = "Ladder_1", Kind = ZoneKind.Ladder },
            };

            Assert.That(Types(Interpret("올라가", current: "EXT", zones: zones)), Is.EqualTo(new[] { ActionType.WallClimb }));
            Assert.That(Types(Interpret("위로 올라가", current: "1F", zones: zones)), Is.EqualTo(new[] { ActionType.ClimbLadder }));
            Assert.That(Types(Interpret("올라가", current: "EXT", target: "Ladder_1", zones: zones)),
                Is.EqualTo(new[] { ActionType.ClimbLadder }));
            // "타고 올라가"는 앞의 오르기 하나로 센다
            Assert.That(Types(Interpret("사다리 타고 올라가서 베어")), Is.EqualTo(new[] { ActionType.ClimbLadder, ActionType.Slash }));
            Assert.That(Types(Interpret("벽을 타고 올라가", current: "1F", zones: zones)), Is.EqualTo(new[] { ActionType.WallClimb }));
        }

        [Test]
        public void SameMotionInTwoWords_CountsOnce()
        {
            Assert.That(Types(Interpret("점프해서 넘어")), Is.EqualTo(new[] { ActionType.Jump }));
            Assert.That(Types(Interpret("살금살금 걸어")), Is.EqualTo(new[] { ActionType.Walk }));
            // 공격은 두 번 말하면 두 번이다
            Assert.That(Types(Interpret("베고 베어")), Is.EqualTo(new[] { ActionType.Slash, ActionType.Slash }));
        }

        [Test]
        public void EnterThroughWindow_BreaksItFirst()
        {
            Assert.That(Types(Interpret("창문으로 들어가")), Is.EqualTo(new[] { ActionType.CrashWindow, ActionType.Enter }));
            Assert.That(Types(Interpret("창문으로 뛰어들어")), Is.EqualTo(new[] { ActionType.CrashWindow, ActionType.Enter }));
        }

        [Test]
        public void PlaceWordFront_IsNotADirection()
        {
            var r = Interpret("적 앞까지 달려");
            Assert.That(Types(r), Is.EqualTo(new[] { ActionType.Run }));
            Assert.That(r.Actions[0].Direction, Is.EqualTo(Direction.None));
        }

        [Test]
        public void MoreSynonyms()
        {
            Assert.That(Types(Interpret("때려")), Is.EqualTo(new[] { ActionType.Slash }));
            Assert.That(Types(Interpret("몸을 날려")), Is.EqualTo(new[] { ActionType.Roll }));
            Assert.That(Types(Interpret("높이 뛰어")), Is.EqualTo(new[] { ActionType.Jump }));
            Assert.That(Types(Interpret("유리창 박살내")), Is.EqualTo(new[] { ActionType.CrashWindow }));
            Assert.That(Types(Interpret("반대로 돌아")), Is.EqualTo(new[] { ActionType.Turn }));
            Assert.That(Types(Interpret("문을 쾅 차")), Is.EqualTo(new[] { ActionType.KickDoor }));
            Assert.That(Types(Interpret("문을 때려 부숴")), Is.EqualTo(new[] { ActionType.BreakDoor }));
            Assert.That(Types(Interpret("거리를 벌려")), Is.EqualTo(new[] { ActionType.Backstep }));
            Assert.That(Types(Interpret("타이밍을 재")), Is.EqualTo(new[] { ActionType.Wait }));
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
