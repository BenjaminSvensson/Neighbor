using System.Collections.Generic;
using System.Globalization;
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
        private const string InvertLookYPreferenceKey = "Neighbor.InvertLookY";
        private const string FullscreenPreferenceKey = "Neighbor.Fullscreen";

        [Header("Defaults")]
        [SerializeField, Min(0f)] private float defaultSensitivity = 0.08f;
        [SerializeField, Range(0f, 1f)] private float defaultVolume = 1f;
        [SerializeField, Range(45f, 100f)] private float defaultFieldOfView = 72f;
        [SerializeField] private bool defaultInvertLookY;
        [SerializeField] private bool defaultFullscreen = true;
        [SerializeField] private PlayerPerformanceProfile defaultPerformanceProfile = PlayerPerformanceProfile.Balanced;

        private PlayerController playerController;
        private PlayerCameraController cameraController;
        private CanvasGroup canvasGroup;
        private readonly Dictionary<PlayerInputBindingAction, Text> bindingValueTexts = new();
        private Text sensitivityValueText;
        private Text volumeValueText;
        private Text fieldOfViewValueText;
        private Text invertLookYValueText;
        private Text fullscreenValueText;
        private Text performanceProfileValueText;
        private CursorLockMode previousCursorLockMode;
        private bool previousCursorVisible;
        private float previousTimeScale = 1f;
        private PlayerPerformanceProfile currentPerformanceProfile = PlayerPerformanceProfile.Balanced;
        private PlayerInputBindingAction? pendingRebindAction;
        private int pendingRebindStartFrame;
        private bool invertLookY;
        private bool fullscreen;
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
            CancelPendingRebind();
            if (isOpen)
            {
                Close();
            }

            InteractionOverlayState.SetExternalGameplayInputBlocked(this, false);
        }

        private void Update()
        {
            if (pendingRebindAction.HasValue)
            {
                UpdatePendingRebind();
                return;
            }

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
            CancelPendingRebind();
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
            bool savedInvertLookY = PlayerPrefs.GetInt(
                InvertLookYPreferenceKey,
                (defaultInvertLookY
                    || playerController != null && playerController.RuntimeInvertLookY
                    || cameraController != null && cameraController.RuntimeInvertLookY)
                    ? 1
                    : 0) != 0;
            bool savedFullscreen = PlayerPrefs.GetInt(FullscreenPreferenceKey, defaultFullscreen ? 1 : 0) != 0;

            ApplySensitivity(sensitivity);
            ApplyVolume(volume);
            ApplyFieldOfView(fieldOfView);
            ApplyInvertLookY(savedInvertLookY);
            ApplyFullscreen(savedFullscreen);

            currentPerformanceProfile = PlayerPrefs.HasKey(PlayerPerformanceSettings.PreferenceKey)
                ? PlayerPerformanceSettings.LoadProfile()
                : defaultPerformanceProfile;
            PlayerPerformanceSettings.ApplyProfile(currentPerformanceProfile);
            RefreshPerformanceProfileText();
            RefreshBindingButtons();
        }

        private void ApplySensitivity(float sensitivity)
        {
            sensitivity = Mathf.Clamp(sensitivity, 0.02f, 0.2f);
            PlayerPrefs.SetFloat(SensitivityPreferenceKey, sensitivity);
            PlayerPrefs.Save();
            playerController?.SetRuntimeMouseSensitivity(sensitivity);
            cameraController?.SetRuntimeMouseSensitivity(sensitivity);
            if (sensitivityValueText != null)
            {
                sensitivityValueText.text = sensitivity.ToString("0.000", CultureInfo.InvariantCulture);
            }
        }

        private void ApplyVolume(float volume)
        {
            volume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(VolumePreferenceKey, volume);
            PlayerPrefs.Save();
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
            PlayerPrefs.Save();
            cameraController?.SetRuntimeFieldOfView(fieldOfView);
            if (fieldOfViewValueText != null)
            {
                fieldOfViewValueText.text = Mathf.RoundToInt(fieldOfView).ToString();
            }
        }

        private void ToggleInvertLookY()
        {
            ApplyInvertLookY(!invertLookY);
        }

        private void ApplyInvertLookY(bool invert)
        {
            invertLookY = invert;
            PlayerPrefs.SetInt(InvertLookYPreferenceKey, invertLookY ? 1 : 0);
            PlayerPrefs.Save();
            playerController?.SetRuntimeInvertLookY(invertLookY);
            cameraController?.SetRuntimeInvertLookY(invertLookY);
            if (invertLookYValueText != null)
            {
                invertLookYValueText.text = invertLookY ? "ON" : "OFF";
            }
        }

        private void ToggleFullscreen()
        {
            ApplyFullscreen(!fullscreen);
        }

        private void ApplyFullscreen(bool enabled)
        {
            fullscreen = enabled;
            PlayerPrefs.SetInt(FullscreenPreferenceKey, fullscreen ? 1 : 0);
            PlayerPrefs.Save();
            Screen.fullScreen = fullscreen;
            if (fullscreenValueText != null)
            {
                fullscreenValueText.text = fullscreen ? "ON" : "OFF";
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
            panelRect.sizeDelta = new Vector2(680f, 960f);

            Text title = CreateText("Title", panel.transform, font, 30, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(440f, 44f));
            title.text = "PAUSED";

            CreateSliderRow(panel.transform, font, "Sensitivity", new Vector2(0f, 295f), 0.02f, 0.2f, defaultSensitivity, ApplySensitivity, out sensitivityValueText);
            CreateSliderRow(panel.transform, font, "Volume", new Vector2(0f, 235f), 0f, 1f, defaultVolume, ApplyVolume, out volumeValueText);
            CreateSliderRow(panel.transform, font, "FOV", new Vector2(0f, 175f), 45f, 100f, defaultFieldOfView, ApplyFieldOfView, out fieldOfViewValueText);

            CreateToggleRow(panel.transform, font, "Invert Y", new Vector2(-150f, 112f), ToggleInvertLookY, out invertLookYValueText);
            CreateToggleRow(panel.transform, font, "Fullscreen", new Vector2(170f, 112f), ToggleFullscreen, out fullscreenValueText);

            CreatePerformanceRow(panel.transform, font, new Vector2(0f, 52f));

            Text controlsTitle = CreateText("Controls Title", panel.transform, font, 15, FontStyle.Bold, TextAnchor.MiddleLeft);
            controlsTitle.text = "CONTROLS";
            controlsTitle.color = new Color(1f, 1f, 1f, 0.76f);
            SetRect(controlsTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-220f, -8f), new Vector2(180f, 24f));

            CreateButton(panel.transform, font, "Reset Settings", new Vector2(55f, -8f), ResetUserSettings, new Vector2(150f, 32f));
            CreateButton(panel.transform, font, "Reset Controls", new Vector2(215f, -8f), ResetControlBindings, new Vector2(150f, 32f));

            CreateBindingRows(panel.transform, font);

            CreateButton(panel.transform, font, "Resume", new Vector2(0f, -405f), Close, new Vector2(280f, 44f));
            CreateButton(panel.transform, font, "Restart", new Vector2(-122f, -455f), RestartScene, new Vector2(210f, 42f));
            CreateButton(panel.transform, font, "Quit", new Vector2(122f, -455f), QuitGame, new Vector2(210f, 42f));
        }

        private void CreateToggleRow(
            Transform parent,
            Font font,
            string label,
            Vector2 position,
            UnityEngine.Events.UnityAction clicked,
            out Text valueText)
        {
            Text labelText = CreateText($"{label} Label", parent, font, 15, FontStyle.Bold, TextAnchor.MiddleLeft);
            labelText.text = label.ToUpperInvariant();
            labelText.color = new Color(1f, 1f, 1f, 0.76f);
            SetRect(labelText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position + new Vector2(-70f, 0f), new Vector2(130f, 28f));

            Button button = CreateButton(parent, font, "OFF", position + new Vector2(80f, 0f), clicked, new Vector2(82f, 34f));
            valueText = button.GetComponentInChildren<Text>(true);
        }

        private void CreateBindingRows(Transform parent, Font font)
        {
            PlayerInputBindingAction[] actions = PlayerInputBindings.GetRebindableActions();
            for (int i = 0; i < actions.Length; i++)
            {
                int column = i % 2;
                int row = i / 2;
                float x = column == 0 ? -175f : 175f;
                float y = -52f - row * 34f;
                CreateBindingRow(parent, font, actions[i], new Vector2(x, y));
            }
        }

        private void CreatePerformanceRow(Transform parent, Font font, Vector2 position)
        {
            Text labelText = CreateText("Performance Label", parent, font, 15, FontStyle.Bold, TextAnchor.MiddleLeft);
            labelText.text = "PERFORMANCE";
            labelText.color = new Color(1f, 1f, 1f, 0.76f);
            SetRect(labelText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position + new Vector2(-185f, 0f), new Vector2(170f, 28f));

            CreateButton(parent, font, "<", position + new Vector2(38f, 0f), () => CyclePerformanceProfile(-1), new Vector2(42f, 34f));

            performanceProfileValueText = CreateText("Performance Value", parent, font, 15, FontStyle.Bold, TextAnchor.MiddleCenter);
            performanceProfileValueText.color = new Color(1f, 0.86f, 0.42f, 0.95f);
            SetRect(performanceProfileValueText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position + new Vector2(124f, 0f), new Vector2(120f, 28f));

            CreateButton(parent, font, ">", position + new Vector2(210f, 0f), () => CyclePerformanceProfile(1), new Vector2(42f, 34f));
        }

        private void CreateBindingRow(Transform parent, Font font, PlayerInputBindingAction action, Vector2 position)
        {
            Text labelText = CreateText($"{action} Binding Label", parent, font, 13, FontStyle.Bold, TextAnchor.MiddleLeft);
            labelText.text = PlayerInputBindings.GetActionLabel(action).ToUpperInvariant();
            labelText.color = new Color(1f, 1f, 1f, 0.72f);
            SetRect(labelText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position + new Vector2(-62f, 0f), new Vector2(126f, 28f));

            Button button = CreateButton(
                parent,
                font,
                PlayerInputBindings.GetKeyLabel(action),
                position + new Vector2(76f, 0f),
                () => BeginRebind(action),
                new Vector2(130f, 32f));
            bindingValueTexts[action] = button.GetComponentInChildren<Text>(true);
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
            Vector2 size = label == "Resume" ? new Vector2(260f, 48f) : new Vector2(210f, 48f);
            return CreateButton(parent, font, label, position, clicked, size);
        }

        private Button CreateButton(Transform parent, Font font, string label, Vector2 position, UnityEngine.Events.UnityAction clicked, Vector2 size)
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

            SetRect(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            Text text = CreateText($"{label} Text", image.transform, font, 16, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.text = label.ToUpperInvariant();
            Stretch(text.rectTransform);
            return button;
        }

        private void CyclePerformanceProfile(int direction)
        {
            currentPerformanceProfile = direction >= 0
                ? PlayerPerformanceSettings.GetNextProfile(currentPerformanceProfile)
                : PlayerPerformanceSettings.GetPreviousProfile(currentPerformanceProfile);
            PlayerPerformanceSettings.SetProfile(currentPerformanceProfile);
            RefreshPerformanceProfileText();
        }

        private void BeginRebind(PlayerInputBindingAction action)
        {
            pendingRebindAction = action;
            pendingRebindStartFrame = Time.frameCount;
            RefreshBindingButtons();
        }

        private void UpdatePendingRebind()
        {
            if (Time.frameCount <= pendingRebindStartFrame)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                if (!PlayerInputBindings.TryGetPressedControlThisFrame(out _))
                {
                    return;
                }
            }
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                CancelPendingRebind();
                return;
            }

            if (!PlayerInputBindings.TryGetPressedControlThisFrame(out PlayerInputControlBinding binding))
            {
                return;
            }

            if (pendingRebindAction.HasValue && PlayerInputBindings.TrySetBinding(pendingRebindAction.Value, binding))
            {
                pendingRebindAction = null;
                RefreshBindingButtons();
            }
        }

        private void ResetControlBindings()
        {
            pendingRebindAction = null;
            PlayerInputBindings.ResetToDefaults();
            RefreshBindingButtons();
        }

        private void ResetUserSettings()
        {
            CancelPendingRebind();
            ResetPersistentSettings(
                defaultSensitivity,
                defaultVolume,
                defaultFieldOfView,
                defaultInvertLookY,
                defaultFullscreen,
                defaultPerformanceProfile);

            ApplySensitivity(defaultSensitivity);
            ApplyVolume(defaultVolume);
            ApplyFieldOfView(defaultFieldOfView);
            ApplyInvertLookY(defaultInvertLookY);
            ApplyFullscreen(defaultFullscreen);
            currentPerformanceProfile = defaultPerformanceProfile;
            PlayerPerformanceSettings.ApplyProfile(currentPerformanceProfile);
            RefreshPerformanceProfileText();
        }

        private static void ResetPersistentSettings(
            float sensitivity,
            float volume,
            float fieldOfView,
            bool invertLookY,
            bool fullscreen,
            PlayerPerformanceProfile performanceProfile)
        {
            PlayerPrefs.SetFloat(SensitivityPreferenceKey, Mathf.Clamp(sensitivity, 0.02f, 0.2f));
            PlayerPrefs.SetFloat(VolumePreferenceKey, Mathf.Clamp01(volume));
            PlayerPrefs.SetFloat(FieldOfViewPreferenceKey, Mathf.Clamp(fieldOfView, 45f, 100f));
            PlayerPrefs.SetInt(InvertLookYPreferenceKey, invertLookY ? 1 : 0);
            PlayerPrefs.SetInt(FullscreenPreferenceKey, fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(PlayerPerformanceSettings.PreferenceKey, (int)performanceProfile);
            PlayerPrefs.Save();
        }

        private void CancelPendingRebind()
        {
            if (!pendingRebindAction.HasValue)
            {
                return;
            }

            pendingRebindAction = null;
            RefreshBindingButtons();
        }

        private void RefreshBindingButtons()
        {
            foreach (KeyValuePair<PlayerInputBindingAction, Text> bindingValueText in bindingValueTexts)
            {
                if (bindingValueText.Value == null)
                {
                    continue;
                }

                bindingValueText.Value.text = pendingRebindAction.HasValue && pendingRebindAction.Value == bindingValueText.Key
                    ? "PRESS INPUT"
                    : PlayerInputBindings.GetKeyLabel(bindingValueText.Key);
            }
        }

        private void RefreshPerformanceProfileText()
        {
            if (performanceProfileValueText != null)
            {
                performanceProfileValueText.text = PlayerPerformanceSettings.GetDisplayName(currentPerformanceProfile).ToUpperInvariant();
            }
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
