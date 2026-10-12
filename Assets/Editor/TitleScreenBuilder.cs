using ReAction.Interpretation.Llm;
using ReAction.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ReAction.Editor
{
    /// <summary>
    /// 메인 화면 UI: 도트 로고, "아무 키나 누르세요", 해석 방식 메뉴, Ollama 상태.
    /// 게임 UI 위에 겹치는 별도 캔버스이고, 배경은 실제 스테이지가 그대로 보인다.
    /// </summary>
    public static class TitleScreenBuilder
    {
        private const int LogoScale = 3;

        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.35f);
        private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.7f);
        private static readonly Color TextColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        private static readonly Color HintColor = new Color(0.6f, 0.6f, 0.65f, 1f);

        public static TitleScreen Build(Transform parent, GameObject gameUi, LlmSettings llmSettings, TMP_FontAsset font)
        {
            var logoSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TitleLogoBuilder.LogoPath);

            var canvasGo = new GameObject("TitleUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 10; // 게임 UI 위
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            var root = (RectTransform)canvasGo.transform;

            // 배경 게임 화면을 살짝 어둡게 해서 글자가 잘 보이게
            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(root, false);
            PromptUIBuilder.Place((RectTransform)dim.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            var dimImage = dim.GetComponent<Image>();
            dimImage.color = DimColor;
            dimImage.raycastTarget = false;

            var logoGo = new GameObject("Logo", typeof(RectTransform), typeof(Image));
            logoGo.transform.SetParent(root, false);
            var logoImage = logoGo.GetComponent<Image>();
            logoImage.sprite = logoSprite;
            logoImage.raycastTarget = false;
            var logo = (RectTransform)logoGo.transform;
            var size = logoSprite.rect.size * LogoScale;
            PromptUIBuilder.Place(logo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -260f), size);

            var pressKey = PromptUIBuilder.CreateText("PressAnyKey", root, font, TextColor, TextAlignmentOptions.Center);
            PromptUIBuilder.Place(pressKey.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(300f, 14f));
            pressKey.text = "아무 키나 누르세요";

            var menu = new GameObject("Menu", typeof(RectTransform));
            menu.transform.SetParent(root, false);
            PromptUIBuilder.Place((RectTransform)menu.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 200f));

            // 메뉴 글자 뒤 어두운 판 (뒤에서 움직이는 고블린 위에서도 잘 읽히게)
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(menu.transform, false);
            PromptUIBuilder.Place((RectTransform)panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(360f, 96f));
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = PanelColor;
            panelImage.raycastTarget = false;

            var items = new TMP_Text[3];
            for (int i = 0; i < items.Length; i++)
            {
                var item = PromptUIBuilder.CreateText($"Item{i}", menu.transform, font, TextColor,
                    TextAlignmentOptions.Center);
                PromptUIBuilder.Place(item.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(0f, 4f - i * 20f), new Vector2(260f, 16f));
                item.raycastTarget = true; // 마우스 올리기·클릭
                item.gameObject.AddComponent<TitleMenuItem>();
                items[i] = item;
            }

            var status = PromptUIBuilder.CreateText("OllamaStatus", menu.transform, font, HintColor,
                TextAlignmentOptions.Center);
            PromptUIBuilder.Place(status.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -66f), new Vector2(400f, 14f));

            var hint = PromptUIBuilder.CreateText("Hint", menu.transform, font, HintColor, TextAlignmentOptions.Center);
            PromptUIBuilder.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(400f, 14f));
            hint.text = "↑↓ 선택 · Enter 결정 · 마우스로도 고를 수 있다";
            menu.SetActive(false);

            var screen = canvasGo.AddComponent<TitleScreen>();
            PromptUIBuilder.SetRef(screen, "_llmSettings", llmSettings);
            PromptUIBuilder.SetRef(screen, "_gameUi", gameUi);
            PromptUIBuilder.SetRef(screen, "_logo", logo);
            PromptUIBuilder.SetRef(screen, "_pressKey", pressKey);
            PromptUIBuilder.SetRef(screen, "_menu", menu);
            PromptUIBuilder.SetRef(screen, "_ollamaStatus", status);
            var so = new SerializedObject(screen);
            var itemsProp = so.FindProperty("_items");
            itemsProp.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) itemsProp.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            so.FindProperty("_dot").intValue = LogoScale;
            so.ApplyModifiedPropertiesWithoutUndo();
            return screen;
        }
    }
}
