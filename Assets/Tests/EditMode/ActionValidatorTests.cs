using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ReAction.Actions;
using ReAction.Interpretation;
using ReAction.Stage;

namespace ReAction.Tests
{
    public sealed class ActionValidatorTests
    {
        private static readonly ActionType[] AllTypes =
            System.Enum.GetValues(typeof(ActionType)).Cast<ActionType>().Where(t => t != ActionType.None).ToArray();

        private ZoneGraph _graph;
        private ZoneKnowledge _knowledge;

        [SetUp]
        public void BuildStage()
        {
            _graph = new ZoneGraph(new List<ZoneData>
            {
                new ZoneData { Id = "EXT", Kind = ZoneKind.Exterior, RevealedAtStart = true, Connections = new[] { "Door_1F" } },
                new ZoneData
                {
                    Id = "Door_1F", Kind = ZoneKind.Door, RevealedAtStart = true,
                    Affordances = new[] { ActionType.OpenDoor, ActionType.KickDoor, ActionType.Enter },
                },
                new ZoneData { Id = "1F-A", Kind = ZoneKind.Room },
                new ZoneData
                {
                    Id = "Window_2F", Kind = ZoneKind.Window, RevealedAtStart = true,
                    Affordances = new[] { ActionType.CrashWindow },
                },
                new ZoneData
                {
                    Id = "Door_Hidden", Kind = ZoneKind.Door,
                    Affordances = new[] { ActionType.OpenDoor, ActionType.KickDoor },
                },
            });
            _knowledge = new ZoneKnowledge(_graph);
        }

        private ValidationResult Validate(IEnumerable<ActionType> allowed, params ActionRequest[] actions)
        {
            var validator = new ActionValidator(_graph, _knowledge, allowed);
            return validator.Validate(new InterpretResult { Actions = actions.ToList() }, "EXT");
        }

        private ValidationResult Validate(params ActionRequest[] actions) => Validate(AllTypes, actions);

        private static ActionRequest A(ActionType type, string target = null) =>
            new ActionRequest(type, Direction.None, target);

        [Test]
        public void SimpleMoves_AreValid()
        {
            var r = Validate(A(ActionType.Run), A(ActionType.Jump), A(ActionType.Roll));
            Assert.That(r.IsValid, Is.True, r.FirstError);
        }

        [Test]
        public void FourActions_RejectedWithCount()
        {
            var r = Validate(A(ActionType.Run), A(ActionType.Jump), A(ActionType.Roll), A(ActionType.Wait));

            Assert.That(r.IsValid, Is.False);
            Assert.That(r.FirstError, Does.Contain("4개"));
        }

        [Test]
        public void TurnDoesNotCountTowardLimit()
        {
            var r = Validate(A(ActionType.Turn), A(ActionType.Run), A(ActionType.Jump), A(ActionType.Roll));
            Assert.That(r.IsValid, Is.True, r.FirstError);
        }

        [Test]
        public void OverLimitFlag_RejectedEvenIfListWasShortened()
        {
            var validator = new ActionValidator(_graph, _knowledge, AllTypes);
            var r = validator.Validate(new InterpretResult { Actions = { A(ActionType.Run) }, OverLimit = true }, "EXT");
            Assert.That(r.IsValid, Is.False);
        }

        [Test]
        public void Empty_Rejected()
        {
            Assert.That(Validate().IsValid, Is.False);
        }

        [Test]
        public void NotAllowedType_Rejected()
        {
            var r = Validate(new[] { ActionType.Run }, A(ActionType.Run), A(ActionType.Slash));

            Assert.That(r.IsValid, Is.False);
            Assert.That(r.FirstError, Does.StartWith("[2]"));
        }

        [Test]
        public void DoorAction_WithoutTarget_Rejected()
        {
            Assert.That(Validate(A(ActionType.KickDoor)).IsValid, Is.False);
        }

        [Test]
        public void DoorAction_OnHiddenDoor_Rejected()
        {
            var r = Validate(A(ActionType.KickDoor, "Door_Hidden"));
            Assert.That(r.FirstError, Does.Contain("모르는 곳"));
        }

        [Test]
        public void DoorAction_OnWindow_Rejected()
        {
            var r = Validate(A(ActionType.OpenDoor, "Window_2F"));
            Assert.That(r.FirstError, Does.Contain("문"));
        }

        [Test]
        public void AffordanceMissing_Rejected()
        {
            Assert.That(Validate(A(ActionType.BreakDoor, "Door_1F")).IsValid, Is.False);
        }

        [Test]
        public void EnterThroughDoor_RequiresOpeningFirst()
        {
            Assert.That(Validate(A(ActionType.Enter, "Door_1F")).IsValid, Is.False);
            Assert.That(Validate(A(ActionType.KickDoor, "Door_1F"), A(ActionType.Enter, "Door_1F")).IsValid, Is.True);
        }

        [Test]
        public void EnterThroughWindow_AfterCrash_IsValid()
        {
            var r = Validate(A(ActionType.CrashWindow, "Window_2F"), A(ActionType.Enter, "Window_2F"));
            Assert.That(r.IsValid, Is.True, r.FirstError);
        }

        [Test]
        public void MoveToHiddenZone_Rejected()
        {
            Assert.That(Validate(A(ActionType.Run, "1F-A")).IsValid, Is.False);
            _knowledge.Reveal("1F-A");
            Assert.That(Validate(A(ActionType.Run, "1F-A")).IsValid, Is.True);
        }
    }
}
