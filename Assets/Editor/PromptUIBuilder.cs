using ReAction.Core;
using ReAction.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace ReAction.Editor
{
    /// <summary>
    /// 프롬프트 UI(캔버스, 입력창, 미리보기, 안내, 상태)와 한글 픽셀 폰트 에셋을 만든다.
    /// 캔버스 기준 해상도는 월드 카메라와 같은 640x360이라 1080p에서 정확히 3배로 그려진다.
    /// </summary>
    public static class PromptUIBuilder
    {
        private const string FontPath = "Assets/_Project/Art/Fonts/Galmuri11.ttf";
        private const string FontAssetPath = "Assets/_Project/Art/Fonts/Galmuri11.asset";
        private const int FontSize = 11;

        private static readonly Color BarColor = new Color(0f, 0f, 0f, 0.75f);
        private static readonly Color FieldColor = new Color(0.12f, 0.12f, 0.15f, 1f);
        private static readonly Color TextColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        private static readonly Color PlaceholderColor = new Color(0.55f, 0.55f, 0.6f, 1f);
        private static readonly Color PreviewColor = new Color(1f, 0.85f, 0.3f, 1f);
        private static readonly Color MessageColor = new Color(1f, 0.6f, 0.5f, 1f);

        public static TMP_FontAsset EnsureFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            // 픽셀 폰트라 SDF 대신 래스터로 굽고 포인트 필터를 써서 정수 배율에서 선명하게 보이게 한다.
            var asset = TMP_FontAsset.CreateFontAsset(font, FontSize, 1, GlyphRenderMode.RASTER_HINTED,
                1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = "Galmuri11";
            AssetDatabase.CreateAsset(asset, FontAssetPath);

            var atlas = asset.atlasTextures[0];
            atlas.name = "Galmuri11 Atlas";
            atlas.filterMode = FilterMode.Point;
            AssetDatabase.AddObjectToAsset(atlas, asset);
            asset.material.name = "Galmuri11 Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        public static PromptUI Build(Transform parent, GameController game, PlanningController planning,
            ZoneSelector selector, TMP_FontAsset font)
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(parent);

            var canvasGo = new GameObject("UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            var root = canvasGo.transform;

            // 하단 입력 바
            var bar = CreateRect("PromptBar", root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 6f), new Vector2(-12f, 24f));
            var barImage = bar.gameObject.AddComponent<Image>();
            barImage.color = BarColor;

            var input = CreateInputField(bar, font);

            var preview = CreateText("Preview", root, font, PreviewColor, TextAlignmentOptions.BottomLeft);
            Place(preview.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 32f), new Vector2(-16f, 14f));

            var message = CreateText("Message", root, font, MessageColor, TextAlignmentOptions.BottomLeft);
            Place(message.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 47f), new Vector2(-16f, 42f));

            var status = CreateText("Status", root, font, TextColor, TextAlignmentOptions.TopLeft);
            Place(status.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(8f, -6f), new Vector2(620f, 14f));

            var (deathPanel, deathText, restartButton) = CreateDeathPanel(root, font);

            var title = CreateText("Title", root, font, TextColor, TextAlignmentOptions.Top);
            title.fontSize = FontSize * 2;
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -22f), new Vector2(300f, 28f));

            var ui = canvasGo.AddComponent<PromptUI>();
            SetRef(ui, "_game", game);
            SetRef(ui, "_planning", planning);
            SetRef(ui, "_selector", selector);
            SetRef(ui, "_input", input);
            SetRef(ui, "_preview", preview);
            SetRef(ui, "_message", message);
            SetRef(ui, "_status", status);
            SetRef(ui, "_title", title);
            SetRef(ui, "_deathPanel", deathPanel);
            SetRef(ui, "_deathText", deathText);
            SetRef(ui, "_restartButton", restartButton);
            return ui;
        }

        /// <summary>사망 시 가운데에 뜨는 패널: 사망 원인 + 다시 시작 버튼. 평소엔 꺼져 있다.</summary>
        private static (GameObject panel, TextMeshProUGUI cause, Button button) CreateDeathPanel(Transform root,
            TMP_FontAsset font)
        {
            var panel = CreateRect("DeathPanel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(240f, 84f));
            panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

            var cause = CreateText("Cause", panel, font, MessageColor, TextAlignmentOptions.Center);
            Place(cause.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -6f), new Vector2(-12f, 46f));

            var buttonGo = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
            buttonGo.name = "RestartButton";
            buttonGo.transform.SetParent(panel, false);
            Place((RectTransform)buttonGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 8f), new Vector2(150f, 20f));
            var button = buttonGo.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = FieldColor;
            colors.highlightedColor = new Color(0.25f, 0.25f, 0.3f, 1f);
            colors.pressedColor = new Color(0.35f, 0.35f, 0.4f, 1f);
            colors.selectedColor = FieldColor;
            colors.disabledColor = new Color(0.12f, 0.12f, 0.15f, 0.4f);
            button.colors = colors;

            var label = buttonGo.GetComponentInChildren<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = FontSize;
            label.color = TextColor;
            label.extraPadding = false;
            label.text = "다시 시작 (Enter)";

            panel.gameObject.SetActive(false);
            return (panel.gameObject, cause, button);
        }

        private static TMP_InputField CreateInputField(Transform bar, TMP_FontAsset font)
        {
            var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.name = "PromptInput";
            go.transform.SetParent(bar, false);
            Place((RectTransform)go.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(-8f, -8f));
            go.GetComponent<Image>().color = FieldColor;

            var input = go.GetComponent<TMP_InputField>();
            // 기본 컨트롤의 안쪽 여백(위아래 6~7)은 16px 높이 입력창에는 너무 커서 줄인다.
            input.textViewport.offsetMin = new Vector2(4f, 0f);
            input.textViewport.offsetMax = new Vector2(-4f, 0f);
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 80;
            input.richText = false;
            input.restoreOriginalTextOnEscape = false;
            // 포커스를 자동으로 다시 잡을 때 쓰던 글자가 전체 선택되어 덮어써지지 않게 한다.
            input.onFocusSelectAll = false;
            input.caretColor = TextColor;
            input.customCaretColor = true;
            input.fontAsset = font;
            input.pointSize = FontSize;

            var text = (TextMeshProUGUI)input.textComponent;
            text.font = font;
            text.fontSize = FontSize;
            text.color = TextColor;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            // 기본 입력창은 extraPadding이 켜져 있는데, 래스터 픽셀 폰트에서는 옆 글리프까지 샘플링해 글자가 깨진다.
            text.extraPadding = false;

            var placeholder = (TextMeshProUGUI)input.placeholder;
            placeholder.font = font;
            placeholder.fontSize = FontSize;
            placeholder.color = PlaceholderColor;
            placeholder.fontStyle = FontStyles.Normal;
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            placeholder.extraPadding = false;
            placeholder.text = "어떻게 움직일까? (예: 오른쪽으로 달려서 점프)  Enter: 실행";
            return input;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, Color color,
            TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = FontSize;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false; // 글자가 월드 클릭을 막지 않게
            text.text = "";
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Place(rect, anchorMin, anchorMax, pivot, position, size);
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
