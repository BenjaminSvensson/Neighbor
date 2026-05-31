using UnityEngine;
using UnityEngine.UI;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class InteractionTooltipView : MonoBehaviour
    {
        [SerializeField] private Text keyText;
        [SerializeField] private Text actionText;
        [SerializeField] private CanvasGroup canvasGroup;

        public void Show(string key, string action)
        {
            EnsureBuilt();

            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(action))
            {
                Hide();
                return;
            }

            keyText.text = key;
            actionText.text = action;
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        public void Hide()
        {
            EnsureBuilt();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        public static InteractionTooltipView CreateRuntimeTooltip()
        {
            GameObject tooltipObject = new GameObject("InteractionTooltip", typeof(RectTransform));
            InteractionTooltipView tooltipView = tooltipObject.AddComponent<InteractionTooltipView>();
            tooltipView.EnsureBuilt();
            tooltipView.Hide();
            return tooltipView;
        }

        private void Awake()
        {
            EnsureBuilt();
            Hide();
        }

        private void EnsureBuilt()
        {
            if (canvasGroup != null && keyText != null && actionText != null)
            {
                return;
            }

            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            Font font = GetDefaultFont();
            RectTransform panel = EnsurePanel();
            keyText = EnsureText("KeyText", panel, font, TextAnchor.MiddleCenter, 22, FontStyle.Bold);
            actionText = EnsureText("ActionText", panel, font, TextAnchor.MiddleLeft, 24, FontStyle.Normal);

            SetRect(keyText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(118f, 44f));
            SetRect(actionText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(152f, 0f), new Vector2(-24f, 0f));
        }

        private RectTransform EnsurePanel()
        {
            Transform existingPanel = transform.Find("Panel");
            GameObject panelObject = existingPanel != null
                ? existingPanel.gameObject
                : new GameObject("Panel", typeof(RectTransform));
            panelObject.transform.SetParent(transform, false);

            RectTransform rectTransform = panelObject.transform as RectTransform;
            if (rectTransform == null)
            {
                Destroy(panelObject);
                panelObject = new GameObject("Panel", typeof(RectTransform));
                panelObject.transform.SetParent(transform, false);
                rectTransform = (RectTransform)panelObject.transform;
            }

            SetRect(rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(520f, 74f));

            Image panelImage = panelObject.GetComponent<Image>();
            if (panelImage == null)
            {
                panelImage = panelObject.AddComponent<Image>();
            }

            panelImage.color = new Color(0.04f, 0.04f, 0.04f, 0.72f);

            GameObject keyBackground = rectTransform.Find("KeyBackground")?.gameObject;
            if (keyBackground == null)
            {
                keyBackground = new GameObject("KeyBackground", typeof(RectTransform));
                keyBackground.transform.SetParent(rectTransform, false);
            }

            RectTransform keyBackgroundRect = keyBackground.transform as RectTransform;
            if (keyBackgroundRect == null)
            {
                Destroy(keyBackground);
                keyBackground = new GameObject("KeyBackground", typeof(RectTransform));
                keyBackground.transform.SetParent(rectTransform, false);
                keyBackgroundRect = (RectTransform)keyBackground.transform;
            }

            SetRect(keyBackgroundRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(118f, 44f));

            Image keyBackgroundImage = keyBackground.GetComponent<Image>() ?? keyBackground.AddComponent<Image>();
            keyBackgroundImage.color = new Color(1f, 1f, 1f, 0.18f);

            return rectTransform;
        }

        private static Text EnsureText(string objectName, Transform parent, Font font, TextAnchor alignment, int fontSize, FontStyle fontStyle)
        {
            Transform existing = parent.Find(objectName);
            GameObject textObject = existing != null ? existing.gameObject : new GameObject(objectName, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);

            if (textObject.transform is not RectTransform)
            {
                Object.Destroy(textObject);
                textObject = new GameObject(objectName, typeof(RectTransform));
                textObject.transform.SetParent(parent, false);
            }

            Text text = textObject.GetComponent<Text>() ?? textObject.AddComponent<Text>();
            text.font = font;
            text.alignment = alignment;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void SetRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
        }

        private static Font GetDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
