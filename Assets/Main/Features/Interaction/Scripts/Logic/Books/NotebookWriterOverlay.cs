using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class NotebookWriterOverlay : MonoBehaviour
    {
        private const int Width = 760;
        private const int Height = 500;

        private static NotebookWriterOverlay activeOverlay;

        private string title;
        private string[] pages;
        private Action<int, string> savePage;
        private int pageIndex;
        private Text titleText;
        private Text pageCounterText;
        private InputField pageInput;
        private CursorLockMode previousLockState;
        private bool previousCursorVisible;
        private bool hasClosed;

        public static bool IsOpen => activeOverlay != null;

        public static void Open(string notebookTitle, string[] notebookPages, Action<int, string> savePageCallback)
        {
            if (activeOverlay != null)
            {
                activeOverlay.Close();
            }

            GameObject overlayObject = new GameObject("NotebookWriterOverlay");
            DontDestroyOnLoad(overlayObject);
            activeOverlay = overlayObject.AddComponent<NotebookWriterOverlay>();
            activeOverlay.Initialize(notebookTitle, notebookPages, savePageCallback);
        }

        private void Initialize(string notebookTitle, string[] notebookPages, Action<int, string> savePageCallback)
        {
            title = string.IsNullOrWhiteSpace(notebookTitle) ? "Notebook" : notebookTitle;
            pages = notebookPages != null && notebookPages.Length > 0
                ? notebookPages
                : new[] { string.Empty };
            savePage = savePageCallback;

            previousLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            BuildUi();
            RefreshPage();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (keyboard.pageDownKey.wasPressedThisFrame)
            {
                NextPage();
            }

            if (keyboard.pageUpKey.wasPressedThisFrame)
            {
                PreviousPage();
            }
        }

        public void NextPage()
        {
            if (pages == null || pages.Length == 0)
            {
                return;
            }

            StoreCurrentPage();
            pageIndex = Mathf.Min(pageIndex + 1, pages.Length - 1);
            RefreshPage();
        }

        public void PreviousPage()
        {
            if (pages == null || pages.Length == 0)
            {
                return;
            }

            StoreCurrentPage();
            pageIndex = Mathf.Max(pageIndex - 1, 0);
            RefreshPage();
        }

        public void Close()
        {
            CompleteClose(true);
        }

        private void OnDestroy()
        {
            CompleteClose(false);
        }

        private void CompleteClose(bool destroyObject)
        {
            if (hasClosed)
            {
                return;
            }

            hasClosed = true;
            StoreCurrentPage();
            Cursor.lockState = previousLockState;
            Cursor.visible = previousCursorVisible;

            if (activeOverlay == this)
            {
                activeOverlay = null;
            }

            if (destroyObject)
            {
                Destroy(gameObject);
            }
        }

        private void BuildUi()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 205;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            Font font = GetBuiltInFont();
            Image dimmer = CreateImage("Dimmer", transform, new Color(0f, 0f, 0f, 0.72f));
            Stretch(dimmer.rectTransform, Vector2.zero, Vector2.zero);

            Image panel = CreateImage("NotebookPanel", transform, new Color(0.9f, 0.86f, 0.72f, 1f));
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(Width, Height);
            panelRect.anchoredPosition = Vector2.zero;

            titleText = CreateText("Title", panelRect, font, 32, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.12f, 0.1f, 0.07f, 1f));
            SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -52f), new Vector2(0f, -14f));

            pageInput = CreateInput("PageInput", panelRect, font);
            SetRect(pageInput.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(52f, 88f), new Vector2(-52f, -84f));

            pageCounterText = CreateText("PageCounter", panelRect, font, 18, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.2f, 0.16f, 0.1f, 1f));
            SetRect(pageCounterText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 30f), new Vector2(0f, 62f));

            CreateButton("PreviousButton", panelRect, font, "Previous", new Vector2(52f, 16f), PreviousPage);
            CreateButton("NextButton", panelRect, font, "Next", new Vector2(168f, 16f), NextPage);
            CreateButton("CloseButton", panelRect, font, "Close", new Vector2(616f, 16f), Close);
        }

        private void StoreCurrentPage()
        {
            if (pages == null || pageInput == null || pageIndex < 0 || pageIndex >= pages.Length)
            {
                return;
            }

            pages[pageIndex] = pageInput.text ?? string.Empty;
            savePage?.Invoke(pageIndex, pages[pageIndex]);
        }

        private void RefreshPage()
        {
            if (titleText != null)
            {
                titleText.text = title;
            }

            if (pageInput != null)
            {
                pageInput.text = pages[pageIndex] ?? string.Empty;
                pageInput.ActivateInputField();
            }

            if (pageCounterText != null)
            {
                pageCounterText.text = $"Page {pageIndex + 1} / {pages.Length}";
            }
        }

        private static InputField CreateInput(string objectName, Transform parent, Font font)
        {
            GameObject inputObject = new GameObject(objectName);
            inputObject.transform.SetParent(parent, false);

            Image image = inputObject.AddComponent<Image>();
            image.color = new Color(0.98f, 0.95f, 0.84f, 1f);

            InputField input = inputObject.AddComponent<InputField>();
            input.lineType = InputField.LineType.MultiLineNewline;
            input.characterLimit = 1200;

            Text text = CreateText("Text", inputObject.transform, font, 21, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.1f, 0.08f, 0.05f, 1f));
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 10f), new Vector2(-12f, -10f));

            Text placeholder = CreateText("Placeholder", inputObject.transform, font, 21, FontStyle.Italic, TextAnchor.UpperLeft, new Color(0.42f, 0.36f, 0.25f, 0.7f));
            placeholder.text = "Write here...";
            placeholder.horizontalOverflow = HorizontalWrapMode.Wrap;
            placeholder.verticalOverflow = VerticalWrapMode.Truncate;
            SetRect(placeholder.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 10f), new Vector2(-12f, -10f));

            input.textComponent = text;
            input.placeholder = placeholder;
            return input;
        }

        private static void CreateButton(string objectName, Transform parent, Font font, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObject = new GameObject(objectName);
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.24f, 0.2f, 0.14f, 1f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(92f, 38f);
            rect.anchoredPosition = anchoredPosition;

            Text text = CreateText("Label", buttonObject.transform, font, 16, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            text.raycastTarget = false;
            Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
            text.text = label;
        }

        private static Image CreateImage(string objectName, Transform parent, Color color)
        {
            GameObject imageObject = new GameObject(objectName);
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(string objectName, Transform parent, Font font, int size, FontStyle style, TextAnchor alignment, Color color)
        {
            GameObject textObject = new GameObject(objectName);
            textObject.transform.SetParent(parent, false);
            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static Font GetBuiltInFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                return font;
            }

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
