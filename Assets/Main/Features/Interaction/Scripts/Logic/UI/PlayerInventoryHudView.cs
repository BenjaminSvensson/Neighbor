using System;
using UnityEngine;
using UnityEngine.UI;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class PlayerInventoryHudView : MonoBehaviour
    {
        private const float InteractorSearchInterval = 0.5f;

        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private CanvasGroup canvasGroup;
        [Header("Layout")]
        [SerializeField, Min(1f)] private float slotSize = 42f;
        [SerializeField, Min(0f)] private float slotGap = 6f;
        [SerializeField, Min(0f)] private float panelPadding = 6f;
        [SerializeField, Min(0f)] private float leftOffset = 32f;
        [SerializeField, Min(0f)] private float bottomOffset = 32f;

        [Header("Colors")]
        [SerializeField] private Color panelColor = new(0.015f, 0.016f, 0.018f, 0.32f);
        [SerializeField] private Color selectedSlotColor = new(1f, 1f, 1f, 0.82f);
        [SerializeField] private Color unselectedSlotColor = new(1f, 1f, 1f, 0.08f);
        [SerializeField] private Color selectedBackgroundColor = new(0.06f, 0.06f, 0.06f, 0.72f);
        [SerializeField] private Color unselectedBackgroundColor = new(0f, 0f, 0f, 0.46f);
        [SerializeField] private Color selectedNumberColor = new(1f, 1f, 1f, 0.94f);
        [SerializeField] private Color unselectedNumberColor = new(1f, 1f, 1f, 0.52f);
        [SerializeField] private Color itemInitialColor = Color.white;
        [SerializeField] private Color emptyInitialColor = new(1f, 1f, 1f, 0.28f);

        private RectTransform panelRectTransform;
        private SlotView[] slotViews;
        private int builtSlotCount;
        private float nextInteractorSearchTime;

        public static PlayerInventoryHudView CreateRuntimeHud(PlayerInteractor interactor)
        {
            GameObject hudObject = new GameObject("PlayerInventoryHud", typeof(RectTransform));
            PlayerInventoryHudView hudView = hudObject.AddComponent<PlayerInventoryHudView>();
            hudView.SetInteractor(interactor);
            return hudView;
        }

        public void SetInteractor(PlayerInteractor playerInteractor)
        {
            interactor = playerInteractor;
            EnsureBuilt();
            UpdateSlots();
            Show();
        }

        public void Show()
        {
            EnsureBuilt();
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        public void Hide()
        {
            EnsureBuilt();
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void Awake()
        {
            EnsureBuilt();
        }

        private void Update()
        {
            ResolveInteractor();

            EnsureBuiltIfNeeded();
            UpdateSlots();

            if (canvasGroup != null)
            {
                bool shouldShow = interactor != null && interactor.isActiveAndEnabled && !InteractionOverlayState.IsGameplayInputBlocked;
                canvasGroup.alpha = shouldShow ? 1f : 0f;
            }
        }

        private void ResolveInteractor()
        {
            if (interactor != null)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now < nextInteractorSearchTime)
            {
                return;
            }

            interactor = FindAnyObjectByType<PlayerInteractor>();
            nextInteractorSearchTime = now + InteractorSearchInterval;
        }

        private void EnsureBuiltIfNeeded()
        {
            int slotCount = interactor != null ? interactor.InventorySlotCount : 6;
            if (canvasGroup == null
                || panelRectTransform == null
                || slotViews == null
                || builtSlotCount != Mathf.Clamp(slotCount, 1, 6))
            {
                EnsureBuilt();
            }
        }

        private void EnsureBuilt()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;

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

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            Font font = GetDefaultFont();
            RectTransform panel = EnsurePanel();
            int slotCount = interactor != null ? interactor.InventorySlotCount : 6;
            EnsureSlotViews(panel, font, slotCount);
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

            panelRectTransform = rectTransform;

            Image panelImage = panelObject.GetComponent<Image>();
            if (panelImage == null)
            {
                panelImage = panelObject.AddComponent<Image>();
            }

            panelImage.color = panelColor;
            panelImage.raycastTarget = false;
            return rectTransform;
        }

        private void EnsureSlotViews(RectTransform panel, Font font, int slotCount)
        {
            slotCount = Mathf.Clamp(slotCount, 1, 6);
            float panelWidth = panelPadding * 2f + slotCount * slotSize + (slotCount - 1) * slotGap;
            float panelHeight = panelPadding * 2f + slotSize;
            SetRect(panel, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(leftOffset, bottomOffset), new Vector2(panelWidth, panelHeight), new Vector2(0f, 0f));

            if (slotViews != null && builtSlotCount == slotCount)
            {
                UpdateSlotLayout(panel);
                return;
            }

            for (int i = panel.childCount - 1; i >= 0; i--)
            {
                Destroy(panel.GetChild(i).gameObject);
            }

            slotViews = new SlotView[slotCount];
            builtSlotCount = slotCount;

            for (int i = 0; i < slotViews.Length; i++)
            {
                slotViews[i] = CreateSlotView(panel, font, i);
            }
        }

        private void UpdateSlotLayout(RectTransform panel)
        {
            for (int i = 0; i < panel.childCount; i++)
            {
                RectTransform slot = panel.GetChild(i) as RectTransform;
                if (slot == null)
                {
                    continue;
                }

                float x = panelPadding + i * (slotSize + slotGap);
                SetRect(slot, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, panelPadding), new Vector2(slotSize, slotSize), new Vector2(0f, 0f));
            }
        }

        private SlotView CreateSlotView(RectTransform panel, Font font, int slotIndex)
        {
            GameObject rootObject = new GameObject($"Slot{slotIndex + 1}", typeof(RectTransform));
            rootObject.transform.SetParent(panel, false);

            RectTransform root = (RectTransform)rootObject.transform;
            float x = panelPadding + slotIndex * (slotSize + slotGap);
            SetRect(root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, panelPadding), new Vector2(slotSize, slotSize), new Vector2(0f, 0f));

            Image selectionImage = rootObject.AddComponent<Image>();
            selectionImage.color = Color.clear;
            selectionImage.raycastTarget = false;

            GameObject backgroundObject = new GameObject("Background", typeof(RectTransform));
            backgroundObject.transform.SetParent(root, false);
            RectTransform backgroundRect = (RectTransform)backgroundObject.transform;
            SetRect(backgroundRect, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-4f, -4f), new Vector2(0.5f, 0.5f));

            Image backgroundImage = backgroundObject.AddComponent<Image>();
            backgroundImage.color = new Color(0f, 0f, 0f, 0.46f);
            backgroundImage.raycastTarget = false;

            Text numberText = CreateText("Number", root, font, TextAnchor.UpperLeft, 11, FontStyle.Bold);
            SetRect(numberText.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, -4f), Vector2.zero, new Vector2(0f, 1f));
            numberText.text = (slotIndex + 1).ToString();
            numberText.color = new Color(1f, 1f, 1f, 0.52f);

            Text initialText = CreateText("Initial", root, font, TextAnchor.MiddleCenter, 20, FontStyle.Bold);
            SetRect(initialText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));

            return new SlotView
            {
                SelectionImage = selectionImage,
                BackgroundImage = backgroundImage,
                NumberText = numberText,
                InitialText = initialText,
                SelectedSlotColor = selectedSlotColor,
                UnselectedSlotColor = unselectedSlotColor,
                SelectedBackgroundColor = selectedBackgroundColor,
                UnselectedBackgroundColor = unselectedBackgroundColor,
                SelectedNumberColor = selectedNumberColor,
                UnselectedNumberColor = unselectedNumberColor,
                ItemInitialColor = itemInitialColor,
                EmptyInitialColor = emptyInitialColor
            };
        }

        private void UpdateSlots()
        {
            if (interactor == null || panelRectTransform == null)
            {
                return;
            }

            if (slotViews == null || builtSlotCount != interactor.InventorySlotCount)
            {
                EnsureSlotViews(panelRectTransform, GetDefaultFont(), interactor.InventorySlotCount);
            }

            for (int i = 0; i < slotViews.Length; i++)
            {
                Pickupable pickupable = interactor.GetInventorySlotPickup(i);
                bool selected = i == interactor.ActiveInventorySlot;
                slotViews[i].ApplyColors(
                    selectedSlotColor,
                    unselectedSlotColor,
                    selectedBackgroundColor,
                    unselectedBackgroundColor,
                    selectedNumberColor,
                    unselectedNumberColor,
                    itemInitialColor,
                    emptyInitialColor);
                slotViews[i].Set(pickupable, selected);
            }
        }

        private static Text CreateText(string objectName, Transform parent, Font font, TextAnchor alignment, int fontSize, FontStyle fontStyle)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);

            Text text = textObject.AddComponent<Text>();
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

        private static void SetRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 pivot)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
        }

        private static Font GetDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private sealed class SlotView
        {
            public Image SelectionImage;
            public Image BackgroundImage;
            public Text NumberText;
            public Text InitialText;
            public Color SelectedSlotColor;
            public Color UnselectedSlotColor;
            public Color SelectedBackgroundColor;
            public Color UnselectedBackgroundColor;
            public Color SelectedNumberColor;
            public Color UnselectedNumberColor;
            public Color ItemInitialColor;
            public Color EmptyInitialColor;
            private Pickupable lastPickupable;
            private string lastPickupName;

            public void ApplyColors(
                Color selectedSlotColor,
                Color unselectedSlotColor,
                Color selectedBackgroundColor,
                Color unselectedBackgroundColor,
                Color selectedNumberColor,
                Color unselectedNumberColor,
                Color itemInitialColor,
                Color emptyInitialColor)
            {
                SelectedSlotColor = selectedSlotColor;
                UnselectedSlotColor = unselectedSlotColor;
                SelectedBackgroundColor = selectedBackgroundColor;
                UnselectedBackgroundColor = unselectedBackgroundColor;
                SelectedNumberColor = selectedNumberColor;
                UnselectedNumberColor = unselectedNumberColor;
                ItemInitialColor = itemInitialColor;
                EmptyInitialColor = emptyInitialColor;
            }

            public void Set(Pickupable pickupable, bool selected)
            {
                SetImageColor(SelectionImage, selected ? SelectedSlotColor : UnselectedSlotColor);
                SetImageColor(BackgroundImage, selected ? SelectedBackgroundColor : UnselectedBackgroundColor);
                SetTextColor(NumberText, selected ? SelectedNumberColor : UnselectedNumberColor);
                SetTextColor(InitialText, pickupable != null ? ItemInitialColor : EmptyInitialColor);

                string pickupName = pickupable != null ? pickupable.gameObject.name : string.Empty;
                if (pickupable == lastPickupable && pickupName == lastPickupName)
                {
                    return;
                }

                lastPickupable = pickupable;
                lastPickupName = pickupName;
                string initialText = GetItemInitial(pickupable, pickupName);
                if (InitialText.text != initialText)
                {
                    InitialText.text = initialText;
                }
            }

            private static void SetImageColor(Image image, Color color)
            {
                if (image != null && image.color != color)
                {
                    image.color = color;
                }
            }

            private static void SetTextColor(Text text, Color color)
            {
                if (text != null && text.color != color)
                {
                    text.color = color;
                }
            }

            private static string GetItemInitial(Pickupable pickupable, string pickupName)
            {
                if (pickupable == null)
                {
                    return "-";
                }

                string itemName = pickupName.Replace("(Clone)", string.Empty).Trim();
                if (itemName.StartsWith("Placeholder", StringComparison.OrdinalIgnoreCase))
                {
                    itemName = itemName.Substring("Placeholder".Length).Trim();
                }

                foreach (char character in itemName)
                {
                    if (char.IsLetterOrDigit(character))
                    {
                        return char.ToUpperInvariant(character).ToString();
                    }
                }

                return "?";
            }
        }
    }
}
