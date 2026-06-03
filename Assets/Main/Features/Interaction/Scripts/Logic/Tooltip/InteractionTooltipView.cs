using UnityEngine;
using UnityEngine.UI;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class InteractionTooltipView : MonoBehaviour
    {
        private const float MinPanelWidth = 188f;
        private const float MaxPanelWidth = 360f;
        private const float PanelHeight = 38f;
        private const float PanelBottomOffset = 86f;
        private const float HorizontalPadding = 12f;
        private const float Gap = 10f;
        private const float KeyHorizontalPadding = 12f;
        private const float KeyHeight = 26f;
        private const float ActionRightPadding = 12f;

        [SerializeField] private Text keyText;
        [SerializeField] private Text actionText;
        [SerializeField] private CanvasGroup canvasGroup;

        private RectTransform panelRectTransform;
        private RectTransform keyBackgroundRectTransform;

        public void Show(string key, string action)
        {
            if (this == null)
            {
                return;
            }

            EnsureBuilt();

            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(action))
            {
                Hide();
                return;
            }

            keyText.text = key;
            actionText.text = action;
            UpdateLayout();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        public void Hide()
        {
            if (this == null)
            {
                return;
            }

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
            if (this == null)
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
            keyText = EnsureText("KeyText", panel, font, TextAnchor.MiddleCenter, 16, FontStyle.Bold);
            actionText = EnsureText("ActionText", panel, font, TextAnchor.MiddleLeft, 18, FontStyle.Normal);

            SetRect(keyText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(HorizontalPadding, 0f), new Vector2(84f, KeyHeight));
            SetRect(actionText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(HorizontalPadding + 94f, 0f), new Vector2(-ActionRightPadding, 0f));
            UpdateLayout();
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

            SetRect(rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, PanelBottomOffset), new Vector2(MinPanelWidth, PanelHeight));
            panelRectTransform = rectTransform;

            Image panelImage = panelObject.GetComponent<Image>();
            if (panelImage == null)
            {
                panelImage = panelObject.AddComponent<Image>();
            }

            panelImage.color = new Color(0.02f, 0.02f, 0.02f, 0.36f);

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

            SetRect(keyBackgroundRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(HorizontalPadding, 0f), new Vector2(84f, KeyHeight));
            keyBackgroundRectTransform = keyBackgroundRect;

            Image keyBackgroundImage = keyBackground.GetComponent<Image>() ?? keyBackground.AddComponent<Image>();
            keyBackgroundImage.color = new Color(1f, 1f, 1f, 0.14f);

            return rectTransform;
        }

        private void UpdateLayout()
        {
            if (panelRectTransform == null || keyBackgroundRectTransform == null || keyText == null || actionText == null)
            {
                return;
            }

            float keyWidth = Mathf.Clamp(keyText.preferredWidth + KeyHorizontalPadding * 2f, 56f, 132f);
            float actionWidth = Mathf.Clamp(actionText.preferredWidth, 64f, MaxPanelWidth - keyWidth - HorizontalPadding * 2f - Gap - ActionRightPadding);
            float panelWidth = Mathf.Clamp(HorizontalPadding + keyWidth + Gap + actionWidth + ActionRightPadding, MinPanelWidth, MaxPanelWidth);

            panelRectTransform.sizeDelta = new Vector2(panelWidth, PanelHeight);
            keyBackgroundRectTransform.sizeDelta = new Vector2(keyWidth, KeyHeight);
            keyBackgroundRectTransform.anchoredPosition = new Vector2(HorizontalPadding + keyWidth * 0.5f, 0f);
            keyText.rectTransform.sizeDelta = new Vector2(keyWidth, KeyHeight);
            keyText.rectTransform.anchoredPosition = new Vector2(HorizontalPadding + keyWidth * 0.5f, 0f);

            float actionLeft = HorizontalPadding + keyWidth + Gap;
            actionText.rectTransform.anchorMin = new Vector2(0f, 0f);
            actionText.rectTransform.anchorMax = new Vector2(1f, 1f);
            actionText.rectTransform.offsetMin = new Vector2(actionLeft, 0f);
            actionText.rectTransform.offsetMax = new Vector2(-ActionRightPadding, 0f);
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
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
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
