using System.IO;
using System.Linq;
using ReAction.Core;
using ReAction.Fx;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ReAction.Editor
{
    /// <summary>
    /// 시간 지연·VHS 연출 설정: 2D 렌더러에 VHS 전체 화면 패스를 한 번만 추가하고,
    /// 연출 수치 에셋과 씬의 연출 오브젝트를 만든다.
    /// </summary>
    public static class FxBuilder
    {
        private const string RendererPath = "Assets/Settings/Renderer2D.asset";
        private const string VhsShader = "Hidden/ReAction/Vhs";
        private const string VhsMaterialPath = "Assets/_Project/Art/Materials/Vhs.mat";
        private const string SettingsPath = "Assets/_Project/ScriptableObjects/Fx/TimeFx.asset";
        private const string FeatureName = "VhsRewind";

        public static TimeFxSettings EnsureSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TimeFxSettings>(SettingsPath);
            if (settings != null) return settings;
            EnsureFolder(Path.GetDirectoryName(SettingsPath));
            settings = ScriptableObject.CreateInstance<TimeFxSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        /// <summary>
        /// Renderer2D에 VHS 패스를 추가한다 (이미 있으면 그대로 둔다). 에디터의 "Add Renderer Feature"와 같은 방식으로
        /// 기능을 렌더러 에셋의 하위 에셋으로 저장하고 m_RendererFeatureMap도 맞춘다.
        /// </summary>
        public static void EnsureVhsFeature()
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
            if (data == null)
            {
                PzLog.Error($"Renderer not found: {RendererPath}");
                return;
            }
            if (data.rendererFeatures.Any(f => f != null && f.name == FeatureName)) return;

            var material = AssetDatabase.LoadAssetAtPath<Material>(VhsMaterialPath);
            if (material == null)
            {
                EnsureFolder(Path.GetDirectoryName(VhsMaterialPath));
                material = new Material(Shader.Find(VhsShader));
                AssetDatabase.CreateAsset(material, VhsMaterialPath);
            }

            var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = FeatureName;
            feature.passMaterial = material;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            AssetDatabase.AddObjectToAsset(feature, data);

            var so = new SerializedObject(data);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            PzLog.Info("VHS renderer feature added to Renderer2D");
        }

        public static TimeDilationFx Build(Transform parent, GameController game, TimeFxSettings settings)
        {
            var go = new GameObject("TimeFx");
            go.transform.SetParent(parent);
            var fx = go.AddComponent<TimeDilationFx>();
            var so = new SerializedObject(fx);
            so.FindProperty("_game").objectReferenceValue = game;
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 후처리(색수차 등)가 보이려면 카메라에서 켜야 한다.
            var cameraData = Camera.main.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData != null) cameraData.renderPostProcessing = true;
            return fx;
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
