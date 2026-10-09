using System.IO;
using System.Linq;
using ReAction.Core;
using ReAction.Enemies;
using ReAction.Fx;
using ReAction.Player;
using ReAction.Stage;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ReAction.Editor
{
    /// <summary>
    /// 고블린 애니메이션(스프라이트 시트 → 클립), 애니메이터, 프리팹, 수치 에셋을 만들고 씬에 배치한다.
    /// 외부 에셋은 수정하지 않고 _Project 아래에 새로 만든다.
    /// </summary>
    public static class EnemyBuilder
    {
        private const string GoblinSprites = "Assets/ExternalAssets/Monsters Creatures Fantasy/Sprites/Goblin";
        private const string AnimFolder = "Assets/_Project/Art/Animations/Goblin";
        private const string ControllerPath = AnimFolder + "/Goblin.controller";
        private const string PrefabPath = "Assets/_Project/Prefabs/Goblin.prefab";
        private const string SettingsPath = "Assets/_Project/ScriptableObjects/Enemies/Goblin.asset";

        // 150x150 프레임에서 발은 아래에서 49px, 피벗은 중앙(75px) → 스프라이트를 (75-49)/32 유닛 올린다.
        private const float GoblinFootOffset = (75f - 49f) / 32f;

        public static MeleeEnemySettings EnsureSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<MeleeEnemySettings>(SettingsPath);
            if (settings != null) return settings;
            EnsureFolder(Path.GetDirectoryName(SettingsPath));
            settings = ScriptableObject.CreateInstance<MeleeEnemySettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        public static GameObject BuildGoblinPrefab(PhysicsMaterial2D material)
        {
            var controller = EnsureGoblinAnimator();
            var root = new GameObject("Goblin");
            try
            {
                var body = root.AddComponent<Rigidbody2D>();
                body.gravityScale = 3f;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

                var collider = root.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.6f, 1.0f);
                collider.offset = new Vector2(0f, 0.5f);
                collider.sharedMaterial = material;

                var enemy = root.AddComponent<MeleeEnemy>();

                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = new Vector3(0f, GoblinFootOffset, 0f);
                var renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sprite = LoadFrames("Idle")[0];
                renderer.sortingOrder = 5;
                var animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                visual.AddComponent<AfterimageTrail>();

                var view = root.AddComponent<MeleeEnemyView>();
                SetRef(view, "_enemy", enemy);
                SetRef(view, "_animator", animator);
                SetRef(view, "_renderer", renderer);

                EnsureFolder(Path.GetDirectoryName(PrefabPath));
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>스테이지 데이터의 적을 배치하고 참조를 연결한다.</summary>
        public static MeleeEnemy[] Spawn(Transform parent, StageDefinition definition, GameObject goblinPrefab,
            MeleeEnemySettings settings, PlayerMotor player, StageController stage, GameController game)
        {
            var root = new GameObject("Enemies").transform;
            root.SetParent(parent);

            return definition.Enemies.Select((spawn, i) =>
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(goblinPrefab, root);
                go.name = $"Goblin_{i}";
                go.transform.position = spawn.Position;

                var enemy = go.GetComponent<MeleeEnemy>();
                SetRef(enemy, "_settings", settings);
                SetRef(enemy, "_target", player);
                SetRef(enemy, "_stage", stage);
                var so = new SerializedObject(enemy);
                so.FindProperty("_initialFacing").intValue = spawn.Facing;
                so.ApplyModifiedPropertiesWithoutUndo();

                SetRef(go.GetComponent<MeleeEnemyView>(), "_game", game);
                return enemy;
            }).ToArray();
        }

        private static AnimatorController EnsureGoblinAnimator()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null) return existing;

            EnsureFolder(AnimFolder);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;

            // (상태 이름, 시트 파일, fps, 반복)
            var specs = new[]
            {
                ("Idle", "Idle", 8f, true),
                ("Run", "Run", 12f, true),
                ("Attack", "Attack1", 14f, false),
                ("Death", "Death", 8f, false),
            };
            foreach (var (state, sheet, fps, loop) in specs)
            {
                var clip = CreateClip($"Goblin_{state}", LoadFrames(sheet), fps, loop);
                var s = machine.AddState(state);
                s.motion = clip;
                if (state == "Idle") machine.defaultState = s;
            }
            return controller;
        }

        private static AnimationClip CreateClip(string name, Sprite[] frames, float fps, bool loop)
        {
            string path = $"{AnimFolder}/{name}.anim";
            var clip = new AnimationClip { name = name, frameRate = fps };
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            var keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i < frames.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
            // 마지막 프레임도 한 프레임 길이만큼 보이게 끝 키를 하나 더 둔다
            keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / fps, value = frames[frames.Length - 1] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static Sprite[] LoadFrames(string sheet) =>
            AssetDatabase.LoadAllAssetsAtPath($"{GoblinSprites}/{sheet}.png")
                .OfType<Sprite>()
                .OrderBy(s => s.rect.x)
                .ToArray();

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
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
