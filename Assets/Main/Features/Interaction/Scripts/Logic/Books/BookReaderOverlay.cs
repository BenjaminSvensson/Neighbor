using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class BookReaderOverlay : MonoBehaviour
    {
        private const int Width = 760;
        private const int Height = 500;

        private static BookReaderOverlay activeOverlay;

        private string title;
        private string[] pages;
        private int pageIndex;
        private Text titleText;
        private Text pageText;
        private Text pageCounterText;
        private CursorLockMode previousLockState;
        private bool previousCursorVisible;

        public static bool IsOpen => activeOverlay != null;

        public static void Open(string bookTitle, string[] bookPages)
        {
            if (activeOverlay != null)
            {
                activeOverlay.Close();
            }

            GameObject overlayObject = new GameObject("BookReaderOverlay");
            DontDestroyOnLoad(overlayObject);
            activeOverlay = overlayObject.AddComponent<BookReaderOverlay>();
            activeOverlay.Initialize(bookTitle, bookPages);
        }

        private void Initialize(string bookTitle, string[] bookPages)
        {
            title = string.IsNullOrWhiteSpace(bookTitle) ? "Book" : bookTitle;
            pages = bookPages != null && bookPages.Length > 0
                ? bookPages
                : new[] { "This book has no pages configured." };

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

            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame || keyboard.pageDownKey.wasPressedThisFrame)
            {
                NextPage();
            }

            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame || keyboard.pageUpKey.wasPressedThisFrame)
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

            pageIndex = Mathf.Min(pageIndex + 1, pages.Length - 1);
            RefreshPage();
        }

        public void PreviousPage()
        {
            if (pages == null || pages.Length == 0)
            {
                return;
            }

            pageIndex = Mathf.Max(pageIndex - 1, 0);
            RefreshPage();
        }

        public void Close()
        {
            Cursor.lockState = previousLockState;
            Cursor.visible = previousCursorVisible;

            if (activeOverlay == this)
            {
                activeOverlay = null;
            }

            Destroy(gameObject);
        }

        private void BuildUi()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            Font font = GetBuiltInFont();
            Image dimmer = CreateImage("Dimmer", transform, new Color(0f, 0f, 0f, 0.72f));
            Stretch(dimmer.rectTransform, Vector2.zero, Vector2.zero);

            Image panel = CreateImage("BookPanel", transform, new Color(0.94f, 0.88f, 0.72f, 1f));
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(Width, Height);
            panelRect.anchoredPosition = Vector2.zero;

            titleText = CreateText("Title", panelRect, font, 32, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.16f, 0.1f, 0.05f, 1f));
            SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -52f), new Vector2(0f, -14f));

            pageText = CreateText("PageText", panelRect, font, 22, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.12f, 0.08f, 0.04f, 1f));
            pageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            pageText.verticalOverflow = VerticalWrapMode.Truncate;
            SetRect(pageText.rectTransform, Vector2.zero, Vector2.one, new Vector2(52f, 82f), new Vector2(-52f, -78f));

            pageCounterText = CreateText("PageCounter", panelRect, font, 18, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.22f, 0.15f, 0.08f, 1f));
            SetRect(pageCounterText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 28f), new Vector2(0f, 62f));

            Text controls = CreateText("Controls", panelRect, font, 16, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(0.25f, 0.17f, 0.09f, 1f));
            controls.text = "A / Left: previous page     D / Right: next page     E / Esc: close";
            SetRect(controls.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 8f), new Vector2(0f, 30f));
        }

        private void RefreshPage()
        {
            if (titleText != null)
            {
                titleText.text = title;
            }

            if (pageText != null)
            {
                pageText.text = pages[pageIndex];
            }

            if (pageCounterText != null)
            {
                pageCounterText.text = $"Page {pageIndex + 1} / {pages.Length}";
            }
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
