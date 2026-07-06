using Neighbor.Main.Features.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Neighbor.Main.Features.Player
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public sealed class PlayerPauseMenu : MonoBehaviour
    {
        private const string SensitivityPreferenceKey = "Neighbor.MouseSensitivity";
        private const string VolumePreferenceKey = "Neighbor.MasterVolume";
        private const string FieldOfViewPreferenceKey = "Neighbor.FieldOfView";

        [Header("Defaults")]
        [SerializeField, Min(0f)] private float defaultSensitivity = 0.08f;
        [SerializeField, Range(0f, 1f)] private float defaultVolume = 1f;
        [SerializeField, Range(45f, 100f)] private float defaultFieldOfView = 72f;

        private PlayerController playerController;
        private PlayerCameraController cameraController;
        private CanvasGroup canvasGroup;
        private Text sensitivityValueText;
        private Text volumeValueText;
        private Text fieldOfViewValueText;
        private CursorLockMode previousCursorLockMode;
        private bool previousCursorVisible;
        private float previousTimeScale = 1f;
        private bool isOpen;

        private void Awake()
        {
            ResolveReferences();
            BuildMenu();
            LoadAndApplyOptions();
            SetVisible(false);
        }

        private void OnDisable()
        {
            if (isOpen)
            {
                Close();
            }

            InteractionOverlayState.SetExternalGameplayInputBlocked(this, false);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            if (isOpen)
            {
                Close();
                return;
            }

            if (!InteractionOverlayState.IsGameplayInputBlocked)
            {
                Open();
            }
        }

        private void Open()
        {
            if (isOpen)
            {
                return;
            }

            isOpen = true;
            previousTimeScale = Time.timeScale;
            previousCursorLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;

            Time.timeScale = 0f;
            InteractionOverlayState.SetExternalGameplayInputBlocked(this, true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SetVisible(true);
        }

        private void Close()
        {
            if (!isOpen)
            {
                return;
            }

            isOpen = false;
            Time.timeScale = Mathf.Approximately(previousTimeScale, 0f) ? 1f : previousTimeScale;
            InteractionOverlayState.SetExternalGameplayInputBlocked(this, false);
            Cursor.lockState = previousCursorLockMode;
            Cursor.visible = previousCursorVisible;
            SetVisible(false);
        }

        private void RestartScene()
        {
            isOpen = false;
            Time.timeScale = 1f;
            InteractionOverlayState.SetExternalGameplayInputBlocked(this, false);
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.buildIndex >= 0)
            {
                SceneManager.LoadScene(activeScene.buildIndex);
                return;
            }

            SceneManager.LoadScene(activeScene.path);
        }

        private void QuitGame()
        {
            Time.timeScale = 1f;
            InteractionOverlayState.SetExternalGameplayInputBlocked(this, false);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ResolveReferences()
        {
            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }

            if (cameraController == null)
            {
                cameraController = GetComponentInChildren<PlayerCameraController>(true);
            }
        }

        private void LoadAndApplyOptions()
        {
            float sensitivity = PlayerPrefs.GetFloat(
                SensitivityPreferenceKey,
                cameraController != null ? cameraController.RuntimeMouseSensitivity : defaultSensitivity);
            float volume = PlayerPrefs.GetFloat(VolumePreferenceKey, defaultVolume);
            float fieldOfView = PlayerPrefs.GetFloat(
                FieldOfViewPreferenceKey,
                cameraController != null ? cameraController.RuntimeFieldOfView : defaultFieldOfView);

            ApplySensitivity(sensitivity);
            ApplyVolume(volume);
            ApplyFieldOfView(fieldOfView);
        }

        private void ApplySensitivity(float sensitivity)
        {
            sensitivity = Mathf.Clamp(sensitivity, 0.02f, 0.2f);
            PlayerPrefs.SetFloat(SensitivityPreferenceKey, sensitivity);
            playerController?.SetRuntimeMouseSensitivity(sensitivity);
            cameraController?.SetRuntimeMouseSensitivity(sensitivity);
            if (sensitivityValueText != null)
            {
                sensitivityValueText.text = sensitivity.ToString("0.000");
            }
        }

        private void ApplyVolume(float volume)
        {
            volume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(VolumePreferenceKey, volume);
            AudioListener.volume = volume;
            if (volumeValueText != null)
            {
                volumeValueText.text = Mathf.RoundToInt(volume * 100f).ToString();
            }
        }

        private void ApplyFieldOfView(float fieldOfView)
        {
            fieldOfView = Mathf.Clamp(fieldOfView, 45f, 100f);
            PlayerPrefs.SetFloat(FieldOfViewPreferenceKey, fieldOfView);
            cameraController?.SetRuntimeFieldOfView(fieldOfView);
            if (fieldOfViewValueText != null)
            {
                fieldOfViewValueText.text = Mathf.RoundToInt(fieldOfView).ToString();
            }
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private void BuildMenu()
        {
            GameObject canvasObject = new("Pause Menu");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 10;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGroup = canvasObject.AddComponent<CanvasGroup>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Image dim = CreateImage("Dim", canvasObject.transform, new Color(0f, 0f, 0f, 0.68f));
            Stretch(dim.rectTransform);

            Image panel = CreateImage("Panel", canvasObject.transform, new Color(0.045f, 0.05f, 0.055f, 0.96f));
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(520f, 520f);

            Text title = CreateText("Title", panel.transform, font, 30, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(440f, 44f));
            title.text = "PAUSED";

            CreateSliderRow(panel.transform, font, "Sensitivity", new Vector2(0f, -130f), 0.02f, 0.2f, defaultSensitivity, ApplySensitivity, out sensitivityValueText);
            CreateSliderRow(panel.transform, font, "Volume", new Vector2(0f, -205f), 0f, 1f, defaultVolume, ApplyVolume, out volumeValueText);
            CreateSliderRow(panel.transform, font, "FOV", new Vector2(0f, -280f), 45f, 100f, defaultFieldOfView, ApplyFieldOfView, out fieldOfViewValueText);

            CreateButton(panel.transform, font, "Resume", new Vector2(0f, -365f), Close);
            CreateButton(panel.transform, font, "Restart", new Vector2(-122f, -432f), RestartScene);
            CreateButton(panel.transform, font, "Quit", new Vector2(122f, -432f), QuitGame);
        }

        private void CreateSliderRow(
            Transform parent,
            Font font,
            string label,
            Vector2 position,
            float minimumValue,
            float maximumValue,
            float value,
            UnityEngine.Events.UnityAction<float> changed,
            out Text valueText)
        {
            Text labelText = CreateText($"{label} Label", parent, font, 15, FontStyle.Bold, TextAnchor.MiddleLeft);
            labelText.text = label.ToUpperInvariant();
            labelText.color = new Color(1f, 1f, 1f, 0.76f);
            SetRect(labelText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position + new Vector2(-130f, 18f), new Vector2(180f, 24f));

            valueText = CreateText($"{label} Value", parent, font, 14, FontStyle.Bold, TextAnchor.MiddleRight);
            valueText.color = new Color(1f, 0.86f, 0.42f, 0.95f);
            SetRect(valueText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position + new Vector2(160f, 18f), new Vector2(72f, 24f));

            Slider slider = CreateSlider($"{label} Slider", parent);
            RectTransform sliderRect = slider.GetComponent<RectTransform>();
            SetRect(sliderRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position + new Vector2(0f, -12f), new Vector2(360f, 26f));
            slider.minValue = minimumValue;
            slider.maxValue = maximumValue;
            slider.value = value;
            slider.onValueChanged.AddListener(changed);
        }

        private Button CreateButton(Transform parent, Font font, string label, Vector2 position, UnityEngine.Events.UnityAction clicked)
        {
            Image image = CreateImage($"{label} Button", parent, new Color(0.16f, 0.18f, 0.19f, 0.96f));
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.26f, 0.29f, 0.31f, 1f);
            colors.pressedColor = new Color(0.9f, 0.7f, 0.28f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(clicked);

            SetRect(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(label == "Resume" ? 260f : 210f, 48f));
            Text text = CreateText($"{label} Text", image.transform, font, 16, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.text = label.ToUpperInvariant();
            Stretch(text.rectTransform);
            return button;
        }

        private Slider CreateSlider(string objectName, Transform parent)
        {
            GameObject sliderObject = new(objectName, typeof(RectTransform));
            sliderObject.transform.SetParent(parent, false);
            Slider slider = sliderObject.AddComponent<Slider>();

            Image background = CreateImage("Background", sliderObject.transform, new Color(0f, 0f, 0f, 0.55f));
            Stretch(background.rectTransform);

            GameObject fillArea = new("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObject.transform, false);
            Stretch((RectTransform)fillArea.transform);

            Image fill = CreateImage("Fill", fillArea.transform, new Color(1f, 0.72f, 0.24f, 0.95f));
            Stretch(fill.rectTransform);

            Image handle = CreateImage("Handle", sliderObject.transform, new Color(1f, 1f, 1f, 0.95f));
            RectTransform handleRect = handle.rectTransform;
            handleRect.anchorMin = new Vector2(0f, 0.5f);
            handleRect.anchorMax = new Vector2(0f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(16f, 30f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;
            return slider;
        }

        private static Text CreateText(string objectName, Transform parent, Font font, int fontSize, FontStyle style, TextAnchor alignment)
        {
            GameObject textObject = new(objectName, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static Image CreateImage(string objectName, Transform parent, Color color)
        {
            GameObject imageObject = new(objectName, typeof(RectTransform));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
