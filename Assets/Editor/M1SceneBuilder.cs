using System.Collections.Generic;
using System.IO;
using ReAction.Actions;
using ReAction.Core;
using ReAction.Player;
using ReAction.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ReAction.Editor
{
    /// <summary>
    /// M1 테스트 씬과 필요한 에셋(행동 정의, 애니메이터, 플레이어 프리팹)을 만든다.
    /// 에셋은 없을 때만 생성해서 직접 조정한 값이 덮어써지지 않게 하고, 씬의 Stage는 매번 다시 만든다.
    /// </summary>
    public static class M1SceneBuilder
    {
        private const string Root = "Assets/_Project";
        private const string ScenePath = Root + "/Scenes/Main.unity";
        private const string SquareSpritePath = Root + "/Art/Placeholder/Square.png";
        private const string NoFrictionPath = Root + "/Art/Physics/NoFriction.physicsMaterial2D";
        private const string ActionsFolder = Root + "/ScriptableObjects/Actions";
        private const string CatalogPath = Root + "/ScriptableObjects/ActionCatalog.asset";
        private const string PlayerControllerPath = Root + "/Art/Animations/Player.controller";
        private const string PlayerPrefabPath = Root + "/Prefabs/Player.prefab";

        private const string HeroFolder = "Assets/ExternalAssets/Hero Knight - Pixel Art";
        private const string HeroSheetPath = HeroFolder + "/Sprites/HeroKnight.png";
        private const int PixelsPerUnit = 32;

        private static readonly string[] HeroStates =
            { "Idle", "Run", "Jump", "Fall", "Roll", "Attack1", "Attack2", "Attack3", "WallSlide", "LedgeGrab", "Hurt", "Death" };

        [MenuItem("ReAction/Build M1 Test Scene")]
        public static void Build()
        {
            Physics2D.simulationMode = SimulationMode2D.Script;

            var square = EnsureSquareSprite();
            var noFriction = EnsureNoFrictionMaterial();
            var catalog = EnsureActionCatalog();
            var controller = EnsurePlayerAnimator();
            var playerPrefab = BuildPlayerPrefab(controller, noFriction);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var gameRoot = FindOrCreateRoot("--- Game ---");
            for (int i = gameRoot.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(gameRoot.transform.GetChild(i).gameObject);

            BuildStage(gameRoot.transform, square, noFriction);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, gameRoot.transform);
            player.transform.position = new Vector3(-6f, -4f, 0f);

            var controllerGo = new GameObject("GameController");
            controllerGo.transform.SetParent(gameRoot.transform);
            var game = controllerGo.AddComponent<GameController>();
            SetRef(game, "_catalog", catalog);
            SetRef(game, "_player", player.GetComponent<PlayerMotor>());
            SetRef(game, "_playerView", player.GetComponent<PlayerView>());
            var panel = controllerGo.AddComponent<DebugActionPanel>();
            SetRef(panel, "_game", game);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            PzLog.Info("M1 test scene built");
        }

        private static void BuildStage(Transform parent, Sprite square, PhysicsMaterial2D material)
        {
            var stage = new GameObject("Stage").transform;
            stage.SetParent(parent);

            var floorColor = new Color(0.35f, 0.33f, 0.4f);
            var obstacleColor = new Color(0.5f, 0.42f, 0.32f);

            // 화면은 20 x 11.25 유닛 (640x360, PPU 32), 바닥 윗면은 y = -4
            CreateBlock(stage, "Ground", new Vector2(0f, -4.5f), new Vector2(20f, 1f), floorColor, square, material);
            CreateBlock(stage, "Wall_Left", new Vector2(-9.5f, 0f), new Vector2(1f, 11.25f), floorColor, square, material);
            CreateBlock(stage, "Wall_Right", new Vector2(9.5f, 0f), new Vector2(1f, 11.25f), floorColor, square, material);
            CreateBlock(stage, "Crate", new Vector2(0f, -3.5f), new Vector2(1f, 1f), obstacleColor, square, material);
            CreateBlock(stage, "Platform", new Vector2(5.5f, -2.25f), new Vector2(3f, 0.5f), floorColor, square, material);
        }

        private static void CreateBlock(Transform parent, string name, Vector2 center, Vector2 size, Color color,
            Sprite sprite, PhysicsMaterial2D material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = center;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;

            var collider = go.AddComponent<BoxCollider2D>();
            collider.sharedMaterial = material;
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
                renderer.sortingOrder = 10;
                var animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;

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
