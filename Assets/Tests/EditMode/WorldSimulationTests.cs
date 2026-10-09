using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using ReAction.Actions;
using ReAction.Core;
using ReAction.Enemies;
using ReAction.Player;
using ReAction.Stage;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReAction.Tests
{
    /// <summary>
    /// 결정론(같은 입력 → 같은 결과)과 리셋 범위(월드는 초기화, 발견 정보는 유지)를 검증한다.
    /// 열려 있는 씬을 건드리지 않도록 별도 미리보기 씬(자체 물리 씬)에서 돌린다.
    /// </summary>
    public sealed class WorldSimulationTests
    {
        private const int TicksPerRun = 240;

        private Scene _scene;
        private PlayerMotor _player;
        private MeleeEnemy _goblin;
        private WorldSimulation _sim;
        private WorldResetter _resetter;
        private ActionCatalog _catalog;
        private readonly List<Object> _assets = new List<Object>();

        [SetUp]
        public void BuildWorld()
        {
            _scene = EditorSceneManager.NewPreviewScene();

            var ground = new GameObject("Ground");
            SceneManager.MoveGameObjectToScene(ground, _scene);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.AddComponent<BoxCollider2D>().size = new Vector2(40f, 1f);

            _player = CreateBody("Player", new Vector2(0f, 0f), new Vector2(0.5f, 1.2f)).AddComponent<PlayerMotor>();
            _player.Initialize();

            var settings = Track(ScriptableObject.CreateInstance<MeleeEnemySettings>());
            _goblin = CreateBody("Goblin", new Vector2(4f, 0f), new Vector2(0.6f, 1f)).AddComponent<MeleeEnemy>();
            _goblin.Setup(settings, _player, null);

            _catalog = Track(ScriptableObject.CreateInstance<ActionCatalog>());
            _catalog.EditorSetDefinitions(new List<ActionDefinition>
            {
                Definition(ActionType.Run, speed: 6f, distance: 1.5f, duration: 0f),
                Definition(ActionType.Slash, speed: 0f, distance: 1.3f, duration: 0.43f),
                Definition(ActionType.Wait, speed: 0f, distance: 0f, duration: 0.5f),
                Definition(ActionType.Roll, speed: 7f, distance: 0f, duration: 0.6f),
            });

            _sim = new WorldSimulation(_player);
            _sim.Register(_goblin);
            _resetter = new WorldResetter();
            _resetter.Register(_player);
            _resetter.Register(_goblin);
            _resetter.CaptureAll();
        }

        [TearDown]
        public void Cleanup()
        {
            EditorSceneManager.ClosePreviewScene(_scene);
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void PreviewScene_HasWorkingPhysics()
        {
            var start = _player.Position;
            Run(new ActionRequest(ActionType.Run, Direction.Right));
            Assert.That(_player.Position.x, Is.GreaterThan(start.x + 1f), "player should have moved in the isolated physics scene");
        }

        [Test]
        public void SameInputs_ProduceIdenticalTrace()
        {
            var plan = new[]
            {
                new ActionRequest(ActionType.Run, Direction.Right),
                new ActionRequest(ActionType.Slash),
            };

            var first = Run(plan);
            _resetter.ResetAll();
            _sim.ResetTicks();
            var second = Run(plan);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void WaitingNextToGoblin_KillsPlayer_Deterministically()
        {
            var first = Run(new ActionRequest(ActionType.Wait), new ActionRequest(ActionType.Wait),
                new ActionRequest(ActionType.Wait));
            Assert.That(_player.IsDead, Is.True, "goblin should reach and kill an idle player");

            _resetter.ResetAll();
            _sim.ResetTicks();
            var second = Run(new ActionRequest(ActionType.Wait), new ActionRequest(ActionType.Wait),
                new ActionRequest(ActionType.Wait));

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Reset_RestoresWorld_ButKeepsRevealedZones()
        {
            var graph = new ZoneGraph(new List<ZoneData>
            {
                new ZoneData { Id = "EXT", Kind = ZoneKind.Exterior, RevealedAtStart = true },
                new ZoneData { Id = "1F-A", Kind = ZoneKind.Room },
            });
            var knowledge = new ZoneKnowledge(graph);
            knowledge.Reveal("1F-A");

            var playerStart = _player.Position;
            var goblinStart = _goblin.Position;
            Run(new ActionRequest(ActionType.Run, Direction.Right), new ActionRequest(ActionType.Slash));
            Assume.That(_player.Position, Is.Not.EqualTo(playerStart));

            _resetter.ResetAll();

            Assert.That(_player.Position, Is.EqualTo(playerStart));
            Assert.That(_player.IsDead, Is.False);
            Assert.That(_player.Facing, Is.EqualTo(1));
            Assert.That(_goblin.Position, Is.EqualTo(goblinStart));
            Assert.That(_goblin.IsDead, Is.False);
            Assert.That(_goblin.State, Is.EqualTo(MeleeEnemyState.Idle));
            Assert.That(knowledge.IsRevealed("1F-A"), Is.True, "revealed zones are player knowledge and survive a reset");
        }

        /// <summary>행동을 실행하고 TicksPerRun 동안 매 틱 상태를 문자열로 기록한다 (float는 비트 단위 비교).</summary>
        private List<string> Run(params ActionRequest[] plan)
        {
            var actions = new List<PlayerAction>();
            foreach (var request in plan) actions.Add(ActionFactory.Create(request, _catalog));
            Assert.That(_sim.Executor.TryStart(actions), Is.True);

            var trace = new List<string>();
            for (int i = 0; i < TicksPerRun; i++)
            {
                _sim.Step();
                trace.Add(string.Join("|",
                    _sim.Tick,
                    F(_player.Position.x), F(_player.Position.y), _player.Facing, _player.IsDead,
                    F(_goblin.Position.x), F(_goblin.Position.y), _goblin.State));
                if (_player.IsDead) _sim.Executor.Cancel();
            }
            return trace;
        }

        private static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);

        private GameObject CreateBody(string name, Vector2 feet, Vector2 size)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, _scene);
            go.transform.position = feet;
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.offset = new Vector2(0f, size.y / 2f);
            return go;
        }

        private ActionDefinition Definition(ActionType type, float speed, float distance, float duration)
        {
            var d = Track(ScriptableObject.CreateInstance<ActionDefinition>());
            d.EditorSetup(type, type.ToString(), "", speed, 0f, distance, duration, timeout: 3f);
            return d;
        }

        private T Track<T>(T asset) where T : Object
        {
            _assets.Add(asset);
            return asset;
        }
    }
}
