using System.Collections.Generic;
using System.IO;
using ReAction.Actions;
using ReAction.Core;
using ReAction.Fx;
using ReAction.Interpretation.Llm;
using ReAction.Player;
using ReAction.Replay;
using ReAction.Stage;
using ReAction.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ReAction.Editor
{
    /// <summary>
    /// 테스트 씬과 필요한 에셋(행동 정의, 애니메이터, 플레이어 프리팹, 테스트 스테이지 데이터)을 만든다.
    /// 행동 정의 등 조정 가능한 에셋은 없을 때만 생성하고, 씬의 게임 오브젝트는 매번 다시 만든다.
    /// 지형은 StageDefinition 데이터에서 만들어진다.
    /// </summary>
    public static class StageSceneBuilder
    {
        private const string Root = "Assets/_Project";
        private const string ScenePath = Root + "/Scenes/Main.unity";
        private const string SquareSpritePath = Root + "/Art/Placeholder/Square.png";
        private const string NoFrictionPath = Root + "/Art/Physics/NoFriction.physicsMaterial2D";
        private const string ActionsFolder = Root + "/ScriptableObjects/Actions";
        private const string CatalogPath = Root + "/ScriptableObjects/ActionCatalog.asset";
        private const string StagePath = Root + "/ScriptableObjects/Stages/Stage1.asset";
        private const string MockKeywordsPath = Root + "/Data/mock_keywords.json";
        private const string LlmSettingsPath = Root + "/ScriptableObjects/LlmSettings.asset";
        private const string PlayerControllerPath = Root + "/Art/Animations/Player.controller";
        private const string PlayerPrefabPath = Root + "/Prefabs/Player.prefab";

        private const string HeroFolder = "Assets/ExternalAssets/Hero Knight - Pixel Art";
        private const string HeroSheetPath = HeroFolder + "/Sprites/HeroKnight.png";
        private const int PixelsPerUnit = 32;

        private static readonly string[] HeroStates =
            { "Idle", "Run", "Jump", "Fall", "Roll", "Attack1", "Attack2", "Attack3", "WallSlide", "LedgeGrab", "Hurt", "Death" };

        [MenuItem("ReAction/Build Stage 1 Scene")]
        public static void Build()
        {
            Physics2D.simulationMode = SimulationMode2D.Script;

            var square = EnsureSquareSprite();
            var noFriction = EnsureNoFrictionMaterial();
            var catalog = EnsureActionCatalog();
            var controller = EnsurePlayerAnimator();
            var playerPrefab = BuildPlayerPrefab(controller, noFriction);
            WriteStage();
            var goblinPrefab = EnemyBuilder.BuildGoblinPrefab(noFriction);
            EnemyBuilder.EnsureSettings();
            FxBuilder.EnsureSettings();
            FxBuilder.EnsureVhsFeature();
            EnsureLlmSettings();
            TitleLogoBuilder.Build();
            TitleLogoBuilder.BuildFogMark();

            // 씬을 열면 저장 안 된 새 에셋이 언로드될 수 있다. 먼저 저장하고, 씬을 연 뒤 다시 불러 쓴다.
            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var goblinSettings = EnemyBuilder.EnsureSettings();
            var fxSettings = FxBuilder.EnsureSettings();
            var stageDefinition = AssetDatabase.LoadAssetAtPath<StageDefinition>(StagePath);
            catalog = AssetDatabase.LoadAssetAtPath<ActionCatalog>(CatalogPath);
            var gameRoot = FindOrCreateRoot("--- Game ---");
            for (int i = gameRoot.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(gameRoot.transform.GetChild(i).gameObject);


            Camera.main.transform.position = new Vector3(0f, Stage1Data.CameraY, -10f);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, gameRoot.transform);
            player.transform.position = stageDefinition.PlayerStart;

            var controllerGo = new GameObject("GameController");
            controllerGo.transform.SetParent(gameRoot.transform);
            var game = controllerGo.AddComponent<GameController>();
            SetRef(game, "_catalog", catalog);
            SetRef(game, "_player", player.GetComponent<PlayerMotor>());
            SetRef(game, "_playerView", player.GetComponent<PlayerView>());
            SetRef(game, "_rewindPlayer", controllerGo.AddComponent<RewindPlayer>());
            SetRef(player.GetComponent<PlayerView>(), "_game", game);

            var stageGo = new GameObject("StageController");
            stageGo.transform.SetParent(gameRoot.transform);
            var stage = stageGo.AddComponent<StageController>();
            SetRef(stage, "_definition", stageDefinition);
            SetRef(stage, "_game", game);
            SetRef(stage, "_player", player.GetComponent<PlayerMotor>());
            SetRef(stage, "_fogSprite", square);
            SetRef(stage, "_fogMarkSprite", AssetDatabase.LoadAssetAtPath<Sprite>(TitleLogoBuilder.FogMarkPath));
            SetRef(game, "_stage", stage);

            // 지형은 Stage 아래, 문·창문은 StageController 아래 (StageController가 찾아서 관리한다)
            BuildStage(gameRoot.transform, stageDefinition, square, noFriction);
            BuildEntries(stageGo.transform, stageDefinition, square, noFriction);

            var enemies = EnemyBuilder.Spawn(gameRoot.transform, stageDefinition, goblinPrefab, goblinSettings,
                player.GetComponent<PlayerMotor>(), stage, game);
            var gameSo = new SerializedObject(game);
            var enemyList = gameSo.FindProperty("_enemies");
            enemyList.arraySize = enemies.Length;
            for (int i = 0; i < enemies.Length; i++) enemyList.GetArrayElementAtIndex(i).objectReferenceValue = enemies[i];
            gameSo.ApplyModifiedPropertiesWithoutUndo();

            var uiGo = new GameObject("DebugUI");
            uiGo.transform.SetParent(gameRoot.transform);
            var panel = uiGo.AddComponent<DebugActionPanel>();
            var selector = uiGo.AddComponent<ZoneSelector>();
            SetRef(panel, "_game", game);
            SetRef(panel, "_stage", stage);
            SetRef(panel, "_selector", selector);
            SetRef(selector, "_game", game);
            SetRef(selector, "_stage", stage);
            SetRef(selector, "_camera", Camera.main);
            SetRef(selector, "_panel", panel);
            SetRef(selector, "_lineSprite", square);

            var planning = controllerGo.AddComponent<PlanningController>();
            SetRef(planning, "_game", game);
            SetRef(planning, "_stage", stage);
            SetRef(planning, "_selector", selector);
            SetRef(planning, "_mockKeywords", AssetDatabase.LoadAssetAtPath<TextAsset>(MockKeywordsPath));
            SetRef(planning, "_llmSettings", EnsureLlmSettings());

            var font = PromptUIBuilder.EnsureFontAsset();
            var promptUi = PromptUIBuilder.Build(gameRoot.transform, game, planning, selector, font);
            TitleScreenBuilder.Build(gameRoot.transform, promptUi.gameObject, EnsureLlmSettings(), font);
            var fx = FxBuilder.Build(gameRoot.transform, game, fxSettings);
            FxBuilder.BuildFeedback(fx.gameObject, game, planning, stage);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            PzLog.Info("Stage 1 scene built");
        }

        private static void BuildStage(Transform parent, StageDefinition definition, Sprite square, PhysicsMaterial2D material)
        {
            var stage = new GameObject("Stage").transform;
            stage.SetParent(parent);

            foreach (var block in definition.Blocks)
            {
                var go = new GameObject(block.Name);
                go.transform.SetParent(stage);
                go.transform.position = block.Bounds.center;
                go.transform.localScale = new Vector3(block.Bounds.width, block.Bounds.height, 1f);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = square;
                renderer.color = block.Color;
                renderer.sortingOrder = block.SortingOrder;

                if (!block.Solid) continue;
                var collider = go.AddComponent<BoxCollider2D>();
                collider.sharedMaterial = material;

                if (!block.OneWay) continue;
                // 사다리 구멍 덮개: 아래에서 위로는 통과, 위에서는 밟을 수 있다
                collider.usedByEffector = true;
                var effector = go.AddComponent<PlatformEffector2D>();
                effector.useOneWay = true;
                effector.surfaceArc = 170f;
            }
        }

        /// <summary>문 구역마다 닫힌 문을, 창문 구역마다 깰 수 있는 유리를 만든다.</summary>
        private static void BuildEntries(Transform parent, StageDefinition definition, Sprite square, PhysicsMaterial2D material)
        {
            const float doorWidth = 0.2f;
            const float doorHeight = 1.5f;
            const float glassWidth = 0.25f;

            foreach (var zone in definition.Zones)
            {
                if (zone.Kind != ZoneKind.Door && zone.Kind != ZoneKind.Window) continue;

                var go = new GameObject($"{zone.Kind}_{zone.Id}");
                go.transform.SetParent(parent);
                var b = zone.Bounds;
                if (zone.Kind == ZoneKind.Door)
                {
                    go.transform.position = new Vector3(b.center.x, b.yMin + doorHeight / 2f, 0f);
                    go.transform.localScale = new Vector3(doorWidth, doorHeight, 1f);
                }
                else
                {
                    go.transform.position = b.center;
                    go.transform.localScale = new Vector3(glassWidth, b.height, 1f);
                }

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = square;
                renderer.sortingOrder = 2;
                go.AddComponent<BoxCollider2D>().sharedMaterial = material;

                if (zone.Kind == ZoneKind.Door) go.AddComponent<Door>().Setup(zone.Id);
                else go.AddComponent<BreakableWindow>().Setup(zone.Id);
            }
        }

        private static LlmSettings EnsureLlmSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LlmSettings>(LlmSettingsPath);
            if (settings != null) return settings;
            settings = ScriptableObject.CreateInstance<LlmSettings>();
            AssetDatabase.CreateAsset(settings, LlmSettingsPath);
            return settings;
        }

        private static void WriteStage()
        {
            var definition = AssetDatabase.LoadAssetAtPath<StageDefinition>(StagePath);
            if (definition == null)
            {
                EnsureFolder(Path.GetDirectoryName(StagePath));
                definition = ScriptableObject.CreateInstance<StageDefinition>();
                AssetDatabase.CreateAsset(definition, StagePath);
            }
            Stage1Data.Fill(definition);
            EditorUtility.SetDirty(definition);
        }

        private static GameObject BuildPlayerPrefab(AnimatorController controller, PhysicsMaterial2D material)
        {
            var root = new GameObject("Player");
            try
            {
                var body = root.AddComponent<Rigidbody2D>();
                body.gravityScale = 3f;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.interpolation = RigidbodyInterpolation2D.None;

                var collider = root.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.5f, 1.2f);
                collider.offset = new Vector2(0f, 0.6f);
                collider.sharedMaterial = material;

                var motor = root.AddComponent<PlayerMotor>();

                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                var renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sprite = LoadSprite(HeroSheetPath, "HeroKnight_0");
                // 플레이어는 항상 아는 존재라 안개(100)·구역 외곽선(110)보다 위에 그린다.
                renderer.sortingOrder = 120;
                var animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                visual.AddComponent<AfterimageTrail>();
                visual.AddComponent<HeroAnimationEvents>();

                var view = root.AddComponent<PlayerView>();
                SetRef(view, "_motor", motor);
                SetRef(view, "_animator", animator);
                SetRef(view, "_renderer", renderer);

                EnsureFolder(Path.GetDirectoryName(PlayerPrefabPath));
                return PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static AnimatorController EnsurePlayerAnimator()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
            if (existing != null) return existing;

            EnsureFolder(Path.GetDirectoryName(PlayerControllerPath));
            var controller = AnimatorController.CreateAnimatorControllerAtPath(PlayerControllerPath);
            var stateMachine = controller.layers[0].stateMachine;
            foreach (var stateName in HeroStates)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{HeroFolder}/Animations/HeroKnight_{stateName}.anim");
                if (clip == null)
                {
                    PzLog.Warn($"Hero clip not found: {stateName}");
                    continue;
                }
                var state = stateMachine.AddState(stateName);
                state.motion = clip;
                if (stateName == "Idle") stateMachine.defaultState = state;
            }
            return controller;
        }

        private static ActionCatalog EnsureActionCatalog()
        {
            EnsureFolder(ActionsFolder);
            var definitions = new List<ActionDefinition>
            {
                EnsureAction(ActionType.Run, "달리기", "Run", speed: 6f, verticalSpeed: 0f, distance: 4f, duration: 0f, timeout: 3f),
                EnsureAction(ActionType.Jump, "점프", "", speed: 4f, verticalSpeed: 11f, distance: 0f, duration: 0f, timeout: 3f),
                EnsureAction(ActionType.Roll, "구르기", "Roll", speed: 7f, verticalSpeed: 0f, distance: 0f, duration: 0.6f, timeout: 2f),
                EnsureAction(ActionType.Wait, "대기", "", speed: 0f, verticalSpeed: 0f, distance: 0f, duration: 0.5f, timeout: 0f),
                EnsureAction(ActionType.Slash, "베기", "Attack1", speed: 0f, verticalSpeed: 0f, distance: 1.3f, duration: 0.43f, timeout: 2f),
                EnsureAction(ActionType.Walk, "걷기", "", speed: 2.5f, verticalSpeed: 0f, distance: 2f, duration: 0f, timeout: 4f),
                EnsureAction(ActionType.Sprint, "전력 질주", "", speed: 9f, verticalSpeed: 0f, distance: 6f, duration: 0f, timeout: 3f),
                EnsureAction(ActionType.Backstep, "뒤로 물러나기", "", speed: 5f, verticalSpeed: 0f, distance: 1.5f, duration: 0f, timeout: 1.5f),
                EnsureAction(ActionType.Slide, "슬라이딩", "Roll", speed: 8f, verticalSpeed: 0f, distance: 0f, duration: 0.4f, timeout: 2f),
                EnsureAction(ActionType.OpenDoor, "문 열기", "Idle", speed: 0f, verticalSpeed: 0f, distance: 0f, duration: 0.25f, timeout: 6f),
                EnsureAction(ActionType.KickDoor, "문 걷어차기", "Attack2", speed: 0f, verticalSpeed: 0f, distance: 0f, duration: 0.4f, timeout: 6f),
                EnsureAction(ActionType.BreakDoor, "문 부수기", "Attack3", speed: 0f, verticalSpeed: 0f, distance: 0f, duration: 0.6f, timeout: 6f),
                EnsureAction(ActionType.Enter, "진입", "", speed: 6f, verticalSpeed: 0f, distance: 0f, duration: 0f, timeout: 4f),
                EnsureAction(ActionType.CrashWindow, "창문 깨고 진입", "Attack1", speed: 0f, verticalSpeed: 0f, distance: 0f, duration: 0.25f, timeout: 3f),
                EnsureAction(ActionType.VaultWindow, "창문 넘기", "", speed: 0f, verticalSpeed: 0f, distance: 0f, duration: 0f, timeout: 3f),
                EnsureAction(ActionType.WallClimb, "벽 타기", "WallSlide", speed: 3f, verticalSpeed: 0f, distance: 0f, duration: 0f, timeout: 6f),
                EnsureAction(ActionType.ClimbLadder, "사다리 오르기", "WallSlide", speed: 3.5f, verticalSpeed: 0f, distance: 0f, duration: 0f, timeout: 6f),
                EnsureAction(ActionType.Turn, "방향 전환", "", speed: 0f, verticalSpeed: 0f, distance: 0f, duration: 0f, timeout: 0f),
            };

            var catalog = AssetDatabase.LoadAssetAtPath<ActionCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ActionCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.EditorSetDefinitions(definitions);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static ActionDefinition EnsureAction(ActionType type, string displayName, string animationState,
            float speed, float verticalSpeed, float distance, float duration, float timeout)
        {
            string path = $"{ActionsFolder}/Action_{type}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<ActionDefinition>(path);
            if (definition != null) return definition;

            definition = ScriptableObject.CreateInstance<ActionDefinition>();
            definition.EditorSetup(type, displayName, animationState, speed, verticalSpeed, distance, duration, timeout);
            AssetDatabase.CreateAsset(definition, path);
            return definition;
        }

        private static Sprite EnsureSquareSprite()
        {
            if (!File.Exists(SquareSpritePath))
            {
                EnsureFolder(Path.GetDirectoryName(SquareSpritePath));
                var texture = new Texture2D(PixelsPerUnit, PixelsPerUnit, TextureFormat.RGBA32, false);
                var pixels = new Color32[PixelsPerUnit * PixelsPerUnit];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                texture.SetPixels32(pixels);
                File.WriteAllBytes(SquareSpritePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(SquareSpritePath);

                var importer = (TextureImporter)AssetImporter.GetAtPath(SquareSpritePath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
        }

        private static PhysicsMaterial2D EnsureNoFrictionMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(NoFrictionPath);
            if (material != null) return material;

            EnsureFolder(Path.GetDirectoryName(NoFrictionPath));
            material = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(material, NoFrictionPath);
            return material;
        }

        private static Sprite LoadSprite(string sheetPath, string spriteName)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            {
                if (asset is Sprite sprite && sprite.name == spriteName) return sprite;
            }
            PzLog.Warn($"Sprite not found: {sheetPath} / {spriteName}");
            return null;
        }

        private static GameObject FindOrCreateRoot(string name)
        {
            foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name == name) return go;
            }
            return new GameObject(name);
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null)
            {
                PzLog.Error($"{target.GetType().Name}.{field} not found");
                return;
            }
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
