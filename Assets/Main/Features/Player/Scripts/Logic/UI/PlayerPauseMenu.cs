using System.Collections.Generic;
using System.Globalization;
using Neighbor.Main.Features.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Neighbor.Main.Features.Player
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public sealed class PlayerPauseMenu : MonoBehaviour
    {
        private const string SensitivityPreferenceKey = "Neighbor.MouseSensitivity";
        private const string VolumePreferenceKey = "Neighbor.MasterVolume";
        private const string LegacyFieldOfViewPreferenceKey = "Neighbor.FieldOfView";
        private const string CameraMotionPreferenceKey = "Neighbor.CameraMotionIntensity";
        private const string InvertLookYPreferenceKey = "Neighbor.InvertLookY";
        private const string ReticlePulsePreferenceKey = "Neighbor.ReticlePulse";
        private const string FullscreenPreferenceKey = "Neighbor.Fullscreen";
        private const float DestructiveActionConfirmationDuration = 3f;
        private static readonly Color BackdropColor = new(0.008f, 0.012f, 0.014f, 0.82f);
        private static readonly Color PanelColor = new(0.035f, 0.042f, 0.044f, 0.985f);
        private static readonly Color CardColor = new(0.075f, 0.082f, 0.078f, 0.92f);
        private static readonly Color CardEdgeColor = new(0.95f, 0.69f, 0.31f, 0.22f);
        private static readonly Color AccentColor = new(1f, 0.68f, 0.25f, 1f);
        private static readonly Color AccentMutedColor = new(0.62f, 0.42f, 0.18f, 0.9f);
        private static readonly Color PrimaryTextColor = new(0.96f, 0.94f, 0.87f, 1f);
        private static readonly Color SecondaryTextColor = new(0.72f, 0.73f, 0.68f, 1f);
        private static readonly Color DangerColor = new(0.72f, 0.24f, 0.18f, 1f);

        [Header("Defaults")]
        [SerializeField, Min(0f)] private float defaultSensitivity = 0.08f;
        [SerializeField, Range(0f, 1f)] private float defaultVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float defaultCameraMotionIntensity = 1f;
        [SerializeField] private bool defaultInvertLookY;
        [SerializeField] private bool defaultReticlePulse = true;
        [SerializeField] private bool defaultFullscreen = true;
        [SerializeField] private PlayerPerformanceProfile defaultPerformanceProfile = PlayerPerformanceProfile.Quality;
        [SerializeField] private PlayerFrameRateLimit defaultFrameRateLimit = PlayerFrameRateLimit.Profile;

        private PlayerController playerController;
        private PlayerCameraController cameraController;
        private PlayerCrosshairFeedback crosshairFeedback;
        private CanvasGroup canvasGroup;
        private readonly Dictionary<PlayerInputBindingAction, Text> bindingValueTexts = new();
        private Text sensitivityValueText;
        private Text volumeValueText;
        private Text cameraMotionValueText;
        private Text invertLookYValueText;
        private Text reticlePulseValueText;
        private Text fullscreenValueText;
        private Text performanceProfileValueText;
        private Text frameRateLimitValueText;
        private Text restartButtonText;
        private Text quitButtonText;
        private Button resumeButton;
        private CursorLockMode previousCursorLockMode;
        private bool previousCursorVisible;
        private float previousTimeScale = 1f;
        private PlayerPerformanceProfile currentPerformanceProfile = PlayerPerformanceProfile.Balanced;
        private PlayerFrameRateLimit currentFrameRateLimit = PlayerFrameRateLimit.Profile;
        private PlayerInputBindingAction? pendingRebindAction;
        private int pendingRebindStartFrame;
        private DestructiveAction pendingDestructiveAction;
        private float destructiveActionConfirmationExpiresAt;
        private float cameraMotionIntensity = 1f;
        private bool invertLookY;
        private bool reticlePulse = true;
        private bool fullscreen;
        private bool isOpen;
        private bool settingsDirty;

        private enum DestructiveAction
        {
            None,
            Restart,
            Quit
        }

        private enum ButtonTone
        {
            Standard,
            Primary,
            Secondary,
            Danger
        }

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
            SaveSettingsIfDirty();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                SaveSettingsIfDirty();
            }
        }

        private void OnApplicationQuit()
        {
            SaveSettingsIfDirty();
        }

        private void Update()
        {
            UpdateDestructiveActionConfirmation();
            if (pendingRebindAction.HasValue)
            {
                UpdatePendingRebind();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            bool pausePressed = keyboard != null && keyboard.escapeKey.wasPressedThisFrame
                || gamepad != null && gamepad.startButton.wasPressedThisFrame;
            bool cancelPressed = isOpen && gamepad != null && gamepad.buttonEast.wasPressedThisFrame;
            if (!pausePressed && !cancelPressed)
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
            CancelDestructiveActionConfirmation();

            Time.timeScale = 0f;
            InteractionOverlayState.SetExternalGameplayInputBlocked(this, true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SetVisible(true);
            SelectDefaultControl();
        }

        private void Close()
        {
            if (!isOpen)
            {
                return;
            }

            isOpen = false;
            CancelPendingRebind();
            CancelDestructiveActionConfirmation();
            SaveSettingsIfDirty();
            Time.timeScale = Mathf.Approximately(previousTimeScale, 0f) ? 1f : previousTimeScale;
            InteractionOverlayState.ConsumeGameplayInputForCurrentFrame();
            InteractionOverlayState.SetExternalGameplayInputBlocked(this, false);
            Cursor.lockState = previousCursorLockMode;
            Cursor.visible = previousCursorVisible;
            ClearMenuSelection();
            SetVisible(false);
        }

        private void RestartScene()
        {
            SaveSettingsIfDirty();
            CancelDestructiveActionConfirmation();
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
            SaveSettingsIfDirty();
            CancelDestructiveActionConfirmation();
            Time.timeScale = 1f;
            InteractionOverlayState.SetExternalGameplayInputBlocked(this, false);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ConfirmOrRestartScene()
        {
            if (TryConfirmDestructiveAction(DestructiveAction.Restart))
            {
                RestartScene();
            }
        }

        private void ConfirmOrQuitGame()
        {
            if (TryConfirmDestructiveAction(DestructiveAction.Quit))
            {
                QuitGame();
            }
        }

        private bool TryConfirmDestructiveAction(DestructiveAction action)
        {
            if (pendingDestructiveAction == action
                && Time.unscaledTime <= destructiveActionConfirmationExpiresAt)
            {
                return true;
            }

            pendingDestructiveAction = action;
            destructiveActionConfirmationExpiresAt = Time.unscaledTime + DestructiveActionConfirmationDuration;
            RefreshDestructiveActionButtonText();
            return false;
        }

        private void UpdateDestructiveActionConfirmation()
        {
            if (pendingDestructiveAction != DestructiveAction.None
                && Time.unscaledTime > destructiveActionConfirmationExpiresAt)
            {
                CancelDestructiveActionConfirmation();
            }
        }

        private void CancelDestructiveActionConfirmation()
        {
            pendingDestructiveAction = DestructiveAction.None;
            destructiveActionConfirmationExpiresAt = 0f;
            RefreshDestructiveActionButtonText();
        }

        private void RefreshDestructiveActionButtonText()
        {
            if (restartButtonText != null)
            {
                restartButtonText.text = pendingDestructiveAction == DestructiveAction.Restart
                    ? "CONFIRM RESTART"
                    : "RESTART";
            }

            if (quitButtonText != null)
            {
                quitButtonText.text = pendingDestructiveAction == DestructiveAction.Quit
                    ? "CONFIRM QUIT"
                    : "QUIT";
            }
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

            if (crosshairFeedback == null)
            {
                crosshairFeedback = GetComponentInChildren<PlayerCrosshairFeedback>(true);
            }
        }

        private void LoadAndApplyOptions()
        {
            float sensitivity = PlayerPrefs.GetFloat(
                SensitivityPreferenceKey,
                cameraController != null ? cameraController.RuntimeMouseSensitivity : defaultSensitivity);
            float volume = PlayerPrefs.GetFloat(VolumePreferenceKey, defaultVolume);
            float savedCameraMotionIntensity = PlayerPrefs.GetFloat(
                CameraMotionPreferenceKey,
                cameraController != null
                    ? cameraController.RuntimeCameraMotionIntensity
                    : defaultCameraMotionIntensity);
            bool savedInvertLookY = PlayerPrefs.GetInt(
                InvertLookYPreferenceKey,
                (defaultInvertLookY
                    || playerController != null && playerController.RuntimeInvertLookY
                    || cameraController != null && cameraController.RuntimeInvertLookY)
                    ? 1
                    : 0) != 0;
            bool savedReticlePulse = PlayerPrefs.GetInt(
                ReticlePulsePreferenceKey,
                (crosshairFeedback != null ? crosshairFeedback.RuntimePulseEnabled : defaultReticlePulse) ? 1 : 0) != 0;
            bool savedFullscreen = PlayerPrefs.GetInt(FullscreenPreferenceKey, defaultFullscreen ? 1 : 0) != 0;

            ApplySensitivity(sensitivity);
            ApplyVolume(volume);
            ApplyCameraMotionIntensity(savedCameraMotionIntensity);
            ApplyInvertLookY(savedInvertLookY);
            ApplyReticlePulse(savedReticlePulse);
            ApplyFullscreen(savedFullscreen);

            currentPerformanceProfile = PlayerPrefs.HasKey(PlayerPerformanceSettings.PreferenceKey)
                ? PlayerPerformanceSettings.LoadProfile()
                : defaultPerformanceProfile;
            currentFrameRateLimit = PlayerPrefs.HasKey(PlayerPerformanceSettings.FrameRateLimitPreferenceKey)
                ? PlayerPerformanceSettings.LoadFrameRateLimit()
                : defaultFrameRateLimit;
            PlayerPerformanceSettings.ApplyFrameRateLimit(currentFrameRateLimit);
            PlayerPerformanceSettings.ApplyProfile(currentPerformanceProfile);
            RefreshPerformanceProfileText();
            RefreshFrameRateLimitText();
            RefreshBindingButtons();

            if (PlayerPrefs.HasKey(LegacyFieldOfViewPreferenceKey))
            {
                PlayerPrefs.DeleteKey(LegacyFieldOfViewPreferenceKey);
                settingsDirty = true;
            }
        }

        private void SetFloatPreference(string key, float value)
        {
            if (PlayerPrefs.HasKey(key) && Mathf.Approximately(PlayerPrefs.GetFloat(key), value))
            {
                return;
            }

            PlayerPrefs.SetFloat(key, value);
            settingsDirty = true;
        }

        private void SetIntPreference(string key, int value)
        {
            if (PlayerPrefs.HasKey(key) && PlayerPrefs.GetInt(key) == value)
            {
                return;
            }

            PlayerPrefs.SetInt(key, value);
            settingsDirty = true;
        }

        private void SaveSettingsIfDirty()
        {
            if (!settingsDirty)
            {
                return;
            }

            PlayerPrefs.Save();
            settingsDirty = false;
        }

        private void ApplySensitivity(float sensitivity)
        {
            sensitivity = Mathf.Clamp(sensitivity, 0.02f, 0.2f);
            SetFloatPreference(SensitivityPreferenceKey, sensitivity);
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
            SetFloatPreference(VolumePreferenceKey, volume);
            AudioListener.volume = volume;
            if (volumeValueText != null)
            {
                volumeValueText.text = Mathf.RoundToInt(volume * 100f).ToString();
            }
        }

        private void ApplyCameraMotionIntensity(float intensity)
        {
            cameraMotionIntensity = Mathf.Clamp01(intensity);
            SetFloatPreference(CameraMotionPreferenceKey, cameraMotionIntensity);
            cameraController?.SetRuntimeCameraMotionIntensity(cameraMotionIntensity);
            if (cameraMotionValueText != null)
            {
                cameraMotionValueText.text = $"{Mathf.RoundToInt(cameraMotionIntensity * 100f)}%";
            }
        }

        private void ToggleInvertLookY()
        {
            ApplyInvertLookY(!invertLookY);
        }

        private void ApplyInvertLookY(bool invert)
        {
            invertLookY = invert;
            SetIntPreference(InvertLookYPreferenceKey, invertLookY ? 1 : 0);
            playerController?.SetRuntimeInvertLookY(invertLookY);
            cameraController?.SetRuntimeInvertLookY(invertLookY);
            if (invertLookYValueText != null)
            {
                invertLookYValueText.text = invertLookY ? "ON" : "OFF";
            }
        }

        private void ToggleReticlePulse()
        {
            ApplyReticlePulse(!reticlePulse);
        }

        private void ApplyReticlePulse(bool enabled)
        {
            reticlePulse = enabled;
            SetIntPreference(ReticlePulsePreferenceKey, reticlePulse ? 1 : 0);
            crosshairFeedback?.SetRuntimePulseEnabled(reticlePulse);
            if (reticlePulseValueText != null)
            {
                reticlePulseValueText.text = reticlePulse ? "ON" : "OFF";
            }
        }

        private void ToggleFullscreen()
        {
            ApplyFullscreen(!fullscreen);
        }

        private void ApplyFullscreen(bool enabled)
        {
            fullscreen = enabled;
            SetIntPreference(FullscreenPreferenceKey, fullscreen ? 1 : 0);
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
            canvasObject.AddComponent<GraphicRaycaster>();
            RuntimeUiEventSystem.EnsureExists();

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGroup = canvasObject.AddComponent<CanvasGroup>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Image dim = CreateImage("Dim", canvasObject.transform, BackdropColor);
            Stretch(dim.rectTransform);
            CreateBackdropBars(dim.transform);

            Image panel = CreateImage("Panel", canvasObject.transform, PanelColor);
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(1240f, 820f);
            AddOutline(panel.gameObject, new Color(0f, 0f, 0f, 0.8f), new Vector2(14f, -14f));

            Image signalRail = CreateImage("Signal Rail", panel.transform, AccentColor);
            SetRect(signalRail.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(5f, 0f), new Vector2(10f, 820f));

            Text title = CreateText("Title", panel.transform, font, 36, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-380f, -52f), new Vector2(420f, 48f));
            title.text = "NEIGHBOR";
            title.color = PrimaryTextColor;

            Text subtitle = CreateText("Subtitle", panel.transform, font, 15, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-380f, -83f), new Vector2(420f, 24f));
            subtitle.text = "PAUSED // SIGNAL HELD";
            subtitle.color = AccentColor;

            Text resumeHint = CreateText("Resume Hint", panel.transform, font, 14, FontStyle.Bold, TextAnchor.MiddleRight);
            SetRect(resumeHint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(465f, -65f), new Vector2(300f, 28f));
            resumeHint.text = "ESC / START / B   RESUME";
            resumeHint.color = SecondaryTextColor;

            Image headerLine = CreateImage("Header Line", panel.transform, AccentMutedColor);
            SetRect(headerLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(1120f, 2f));

            Image settingsCard = CreateSectionCard("Settings Card", panel.transform, new Vector2(-336f, 32f), new Vector2(510f, 516f));
            Image controlsCard = CreateSectionCard("Controls Card", panel.transform, new Vector2(276f, 32f), new Vector2(660f, 516f));

            CreateSectionHeader(settingsCard.transform, font, "PLAYER & DISPLAY", "01", new Vector2(0f, 221f), 430f);
            CreateSectionHeader(controlsCard.transform, font, "CONTROL DECK", "02", new Vector2(0f, 221f), 578f);

            CreateSliderRow(settingsCard.transform, font, "Sensitivity", new Vector2(0f, 146f), 0.02f, 0.2f, defaultSensitivity, ApplySensitivity, out sensitivityValueText);
            CreateSliderRow(settingsCard.transform, font, "Volume", new Vector2(0f, 72f), 0f, 1f, defaultVolume, ApplyVolume, out volumeValueText);
            CreateSliderRow(
                settingsCard.transform,
                font,
                "Camera Motion",
                new Vector2(0f, -2f),
                0f,
                1f,
                defaultCameraMotionIntensity,
                ApplyCameraMotionIntensity,
                out cameraMotionValueText);

            CreateToggleRow(settingsCard.transform, font, "Invert Y", new Vector2(-115f, -72f), ToggleInvertLookY, out invertLookYValueText);
            CreateToggleRow(settingsCard.transform, font, "Reticle", new Vector2(116f, -72f), ToggleReticlePulse, out reticlePulseValueText);
            CreateToggleRow(settingsCard.transform, font, "Fullscreen", new Vector2(-115f, -122f), ToggleFullscreen, out fullscreenValueText);

            CreatePerformanceRow(settingsCard.transform, font, new Vector2(0f, -172f));
            CreateFrameRateRow(settingsCard.transform, font, new Vector2(0f, -218f));

            CreateButton(settingsCard.transform, font, "Reset Settings", new Vector2(0f, -260f), ResetUserSettings, new Vector2(200f, 34f), ButtonTone.Secondary);

            CreateBindingRows(controlsCard.transform, font);
            CreateButton(controlsCard.transform, font, "Reset Controls", new Vector2(244f, -260f), ResetControlBindings, new Vector2(168f, 34f), ButtonTone.Secondary);

            resumeButton = CreateButton(panel.transform, font, "Resume", new Vector2(0f, -352f), Close, new Vector2(330f, 54f), ButtonTone.Primary);
            Button restartButton = CreateButton(panel.transform, font, "Restart", new Vector2(-284f, -352f), ConfirmOrRestartScene, new Vector2(190f, 44f), ButtonTone.Secondary);
            Button quitButton = CreateButton(panel.transform, font, "Quit", new Vector2(284f, -352f), ConfirmOrQuitGame, new Vector2(190f, 44f), ButtonTone.Danger);
            restartButtonText = restartButton.GetComponentInChildren<Text>(true);
            quitButtonText = quitButton.GetComponentInChildren<Text>(true);

            Text footer = CreateText("Footer", panel.transform, font, 11, FontStyle.Normal, TextAnchor.MiddleCenter);
            SetRect(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(600f, 18f));
            footer.text = "ANALOG SURVEILLANCE SYSTEM // LOCAL SESSION";
            footer.color = new Color(SecondaryTextColor.r, SecondaryTextColor.g, SecondaryTextColor.b, 0.58f);
        }

        private void CreateToggleRow(
            Transform parent,
            Font font,
            string label,
            Vector2 position,
            UnityEngine.Events.UnityAction clicked,
            out Text valueText)
        {
            Text labelText = CreateText($"{label} Label", parent, font, 13, FontStyle.Bold, TextAnchor.MiddleLeft);
            labelText.text = label.ToUpperInvariant();
            labelText.color = SecondaryTextColor;
            SetRect(labelText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position + new Vector2(-58f, 0f), new Vector2(100f, 28f));

            Button button = CreateButton(parent, font, "OFF", position + new Vector2(58f, 0f), clicked, new Vector2(72f, 32f), ButtonTone.Secondary);
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
                float y = 164f - row * 40f;
                CreateBindingRow(parent, font, actions[i], new Vector2(x, y));
            }
        }

        private void SelectDefaultControl()
        {
            if (resumeButton == null)
            {
                return;
            }

            EventSystem eventSystem = RuntimeUiEventSystem.EnsureExists();
            eventSystem.SetSelectedGameObject(null);
            eventSystem.SetSelectedGameObject(resumeButton.gameObject);
        }

        private void ClearMenuSelection()
        {
            EventSystem eventSystem = EventSystem.current;
            GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (selected != null && canvasGroup != null && selected.transform.IsChildOf(canvasGroup.transform))
            {
                eventSystem.SetSelectedGameObject(null);
            }
        }

        private void CreatePerformanceRow(Transform parent, Font font, Vector2 position)
        {
            Text labelText = CreateText("Performance Label", parent, font, 13, FontStyle.Bold, TextAnchor.MiddleLeft);
            labelText.text = "PERFORMANCE";
            labelText.color = SecondaryTextColor;
            SetRect(labelText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position + new Vector2(-160f, 0f), new Vector2(150f, 28f));

            CreateButton(parent, font, "<", position + new Vector2(42f, 0f), () => CyclePerformanceProfile(-1), new Vector2(36f, 32f), ButtonTone.Secondary);

            performanceProfileValueText = CreateText("Performance Value", parent, font, 14, FontStyle.Bold, TextAnchor.MiddleCenter);
            performanceProfileValueText.color = AccentColor;
            SetRect(performanceProfileValueText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position + new Vector2(125f, 0f), new Vector2(120f, 28f));

            CreateButton(parent, font, ">", position + new Vector2(208f, 0f), () => CyclePerformanceProfile(1), new Vector2(36f, 32f), ButtonTone.Secondary);
        }

        private void CreateFrameRateRow(Transform parent, Font font, Vector2 position)
        {
            Text labelText = CreateText("Frame Rate Limit Label", parent, font, 13, FontStyle.Bold, TextAnchor.MiddleLeft);
            labelText.text = "FRAME CAP";
            labelText.color = SecondaryTextColor;
            SetRect(labelText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position + new Vector2(-160f, 0f), new Vector2(150f, 28f));

            CreateButton(parent, font, "<", position + new Vector2(42f, 0f), () => CycleFrameRateLimit(-1), new Vector2(36f, 32f), ButtonTone.Secondary);

            frameRateLimitValueText = CreateText("Frame Rate Limit Value", parent, font, 14, FontStyle.Bold, TextAnchor.MiddleCenter);
            frameRateLimitValueText.color = AccentColor;
            SetRect(frameRateLimitValueText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position + new Vector2(125f, 0f), new Vector2(120f, 28f));

            CreateButton(parent, font, ">", position + new Vector2(208f, 0f), () => CycleFrameRateLimit(1), new Vector2(36f, 32f), ButtonTone.Secondary);
        }

        private void CreateBindingRow(Transform parent, Font font, PlayerInputBindingAction action, Vector2 position)
        {
            Text labelText = CreateText($"{action} Binding Label", parent, font, 12, FontStyle.Bold, TextAnchor.MiddleLeft);
            labelText.text = PlayerInputBindings.GetActionLabel(action).ToUpperInvariant();
            labelText.color = SecondaryTextColor;
            SetRect(labelText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position + new Vector2(-58f, 0f), new Vector2(112f, 28f));

            Button button = CreateButton(
                parent,
                font,
                PlayerInputBindings.GetKeyLabel(action),
                position + new Vector2(70f, 0f),
                () => BeginRebind(action),
                new Vector2(124f, 30f),
                ButtonTone.Secondary);
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
            Text labelText = CreateText($"{label} Label", parent, font, 13, FontStyle.Bold, TextAnchor.MiddleLeft);
            labelText.text = label.ToUpperInvariant();
            labelText.color = SecondaryTextColor;
            SetRect(labelText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position + new Vector2(-150f, 18f), new Vector2(200f, 24f));

            valueText = CreateText($"{label} Value", parent, font, 14, FontStyle.Bold, TextAnchor.MiddleRight);
            valueText.color = AccentColor;
            SetRect(valueText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position + new Vector2(178f, 18f), new Vector2(72f, 24f));

            Slider slider = CreateSlider($"{label} Slider", parent);
            RectTransform sliderRect = slider.GetComponent<RectTransform>();
            SetRect(sliderRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position + new Vector2(0f, -12f), new Vector2(390f, 24f));
            slider.minValue = minimumValue;
            slider.maxValue = maximumValue;
            slider.value = value;
            slider.onValueChanged.AddListener(changed);
        }

        private Button CreateButton(Transform parent, Font font, string label, Vector2 position, UnityEngine.Events.UnityAction clicked)
        {
            Vector2 size = label == "Resume" ? new Vector2(260f, 48f) : new Vector2(210f, 48f);
            return CreateButton(parent, font, label, position, clicked, size, ButtonTone.Standard);
        }

        private Button CreateButton(Transform parent, Font font, string label, Vector2 position, UnityEngine.Events.UnityAction clicked, Vector2 size)
        {
            return CreateButton(parent, font, label, position, clicked, size, ButtonTone.Standard);
        }

        private Button CreateButton(
            Transform parent,
            Font font,
            string label,
            Vector2 position,
            UnityEngine.Events.UnityAction clicked,
            Vector2 size,
            ButtonTone tone)
        {
            Color normalColor = tone switch
            {
                ButtonTone.Primary => new Color(0.96f, 0.57f, 0.18f, 1f),
                ButtonTone.Secondary => new Color(0.105f, 0.115f, 0.108f, 0.98f),
                ButtonTone.Danger => new Color(0.27f, 0.08f, 0.065f, 1f),
                _ => new Color(0.14f, 0.15f, 0.145f, 0.98f)
            };
            Color highlightedColor = tone switch
            {
                ButtonTone.Primary => new Color(1f, 0.72f, 0.3f, 1f),
                ButtonTone.Danger => new Color(0.55f, 0.14f, 0.09f, 1f),
                _ => new Color(0.27f, 0.245f, 0.18f, 1f)
            };

            Image image = CreateImage($"{label} Button", parent, Color.white);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = highlightedColor;
            colors.pressedColor = AccentColor;
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(normalColor.r, normalColor.g, normalColor.b, 0.35f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(clicked);
            AddOutline(
                image.gameObject,
                tone == ButtonTone.Danger ? new Color(DangerColor.r, DangerColor.g, DangerColor.b, 0.8f) : CardEdgeColor,
                new Vector2(1f, -1f));

            SetRect(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            Text text = CreateText($"{label} Text", image.transform, font, 16, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.text = label.ToUpperInvariant();
            text.color = tone == ButtonTone.Primary ? new Color(0.11f, 0.07f, 0.025f, 1f) : PrimaryTextColor;
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

        private void CycleFrameRateLimit(int direction)
        {
            currentFrameRateLimit = direction >= 0
                ? PlayerPerformanceSettings.GetNextFrameRateLimit(currentFrameRateLimit)
                : PlayerPerformanceSettings.GetPreviousFrameRateLimit(currentFrameRateLimit);
            PlayerPerformanceSettings.SetFrameRateLimit(currentFrameRateLimit);
            RefreshFrameRateLimitText();
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
                defaultCameraMotionIntensity,
                defaultInvertLookY,
                defaultReticlePulse,
                defaultFullscreen,
                defaultPerformanceProfile,
                defaultFrameRateLimit);

            ApplySensitivity(defaultSensitivity);
            ApplyVolume(defaultVolume);
            ApplyCameraMotionIntensity(defaultCameraMotionIntensity);
            ApplyInvertLookY(defaultInvertLookY);
            ApplyReticlePulse(defaultReticlePulse);
            ApplyFullscreen(defaultFullscreen);
            currentPerformanceProfile = defaultPerformanceProfile;
            currentFrameRateLimit = defaultFrameRateLimit;
            PlayerPerformanceSettings.ApplyFrameRateLimit(currentFrameRateLimit);
            PlayerPerformanceSettings.ApplyProfile(currentPerformanceProfile);
            RefreshPerformanceProfileText();
            RefreshFrameRateLimitText();
            settingsDirty = false;
        }

        private static void ResetPersistentSettings(
            float sensitivity,
            float volume,
            float cameraMotion,
            bool invertLookY,
            bool reticlePulse,
            bool fullscreen,
            PlayerPerformanceProfile performanceProfile,
            PlayerFrameRateLimit frameRateLimit)
        {
            PlayerPrefs.SetFloat(SensitivityPreferenceKey, Mathf.Clamp(sensitivity, 0.02f, 0.2f));
            PlayerPrefs.SetFloat(VolumePreferenceKey, Mathf.Clamp01(volume));
            PlayerPrefs.DeleteKey(LegacyFieldOfViewPreferenceKey);
            PlayerPrefs.SetFloat(CameraMotionPreferenceKey, Mathf.Clamp01(cameraMotion));
            PlayerPrefs.SetInt(InvertLookYPreferenceKey, invertLookY ? 1 : 0);
            PlayerPrefs.SetInt(ReticlePulsePreferenceKey, reticlePulse ? 1 : 0);
            PlayerPrefs.SetInt(FullscreenPreferenceKey, fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(PlayerPerformanceSettings.PreferenceKey, (int)performanceProfile);
            PlayerPrefs.SetInt(PlayerPerformanceSettings.FrameRateLimitPreferenceKey, (int)frameRateLimit);
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

        private void RefreshFrameRateLimitText()
        {
            if (frameRateLimitValueText != null)
            {
                frameRateLimitValueText.text = PlayerPerformanceSettings.GetFrameRateLimitDisplayName(currentFrameRateLimit).ToUpperInvariant();
            }
        }

        private void CreateBackdropBars(Transform parent)
        {
            Image topBar = CreateImage("Top Letterbox", parent, new Color(0f, 0f, 0f, 0.42f));
            RectTransform topRect = topBar.rectTransform;
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.anchoredPosition = Vector2.zero;
            topRect.sizeDelta = new Vector2(0f, 52f);

            Image bottomBar = CreateImage("Bottom Letterbox", parent, new Color(0f, 0f, 0f, 0.42f));
            RectTransform bottomRect = bottomBar.rectTransform;
            bottomRect.anchorMin = Vector2.zero;
            bottomRect.anchorMax = new Vector2(1f, 0f);
            bottomRect.pivot = new Vector2(0.5f, 0f);
            bottomRect.anchoredPosition = Vector2.zero;
            bottomRect.sizeDelta = new Vector2(0f, 52f);

            for (int i = 1; i < 12; i++)
            {
                float anchor = i / 12f;
                Image scanline = CreateImage($"Menu Scanline {i:00}", parent, new Color(1f, 0.72f, 0.38f, 0.012f));
                RectTransform scanlineRect = scanline.rectTransform;
                scanlineRect.anchorMin = new Vector2(0f, anchor);
                scanlineRect.anchorMax = new Vector2(1f, anchor);
                scanlineRect.pivot = new Vector2(0.5f, 0.5f);
                scanlineRect.anchoredPosition = Vector2.zero;
                scanlineRect.sizeDelta = new Vector2(0f, 1f);
            }
        }

        private Image CreateSectionCard(string objectName, Transform parent, Vector2 position, Vector2 size)
        {
            Image card = CreateImage(objectName, parent, CardColor);
            SetRect(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            AddOutline(card.gameObject, CardEdgeColor, new Vector2(1f, -1f));

            Image topAccent = CreateImage($"{objectName} Accent", card.transform, AccentMutedColor);
            SetRect(
                topAccent.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -5f),
                new Vector2(size.x - 12f, 3f));
            return card;
        }

        private void CreateSectionHeader(Transform parent, Font font, string label, string index, Vector2 position, float width)
        {
            Text title = CreateText($"{label} Header", parent, font, 15, FontStyle.Bold, TextAnchor.MiddleLeft);
            title.text = label;
            title.color = PrimaryTextColor;
            SetRect(
                title.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                position + new Vector2((-width * 0.5f) + 92f, 0f),
                new Vector2(260f, 30f));

            Text number = CreateText($"{label} Index", parent, font, 13, FontStyle.Bold, TextAnchor.MiddleRight);
            number.text = index;
            number.color = AccentColor;
            SetRect(
                number.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                position + new Vector2((width * 0.5f) - 38f, 0f),
                new Vector2(48f, 30f));

            Image divider = CreateImage($"{label} Divider", parent, new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.3f));
            SetRect(
                divider.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                position + new Vector2(0f, -24f),
                new Vector2(width, 1f));
        }

        private static void AddOutline(GameObject target, Color color, Vector2 distance)
        {
            Outline outline = target.GetComponent<Outline>();
            if (outline == null)
            {
                outline = target.AddComponent<Outline>();
            }

            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }

        private Slider CreateSlider(string objectName, Transform parent)
        {
            GameObject sliderObject = new(objectName, typeof(RectTransform));
            sliderObject.transform.SetParent(parent, false);
            Slider slider = sliderObject.AddComponent<Slider>();

            Image background = CreateImage("Background", sliderObject.transform, new Color(0.015f, 0.018f, 0.017f, 0.95f));
            RectTransform backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(1f, 0.5f);
            backgroundRect.offsetMin = new Vector2(8f, -3f);
            backgroundRect.offsetMax = new Vector2(-8f, 3f);

            GameObject fillArea = new("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObject.transform, false);
            RectTransform fillAreaRect = (RectTransform)fillArea.transform;
            fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
            fillAreaRect.offsetMin = new Vector2(8f, -3f);
            fillAreaRect.offsetMax = new Vector2(-8f, 3f);

            Image fill = CreateImage("Fill", fillArea.transform, AccentColor);
            Stretch(fill.rectTransform);

            Image handle = CreateImage("Handle", sliderObject.transform, PrimaryTextColor);
            RectTransform handleRect = handle.rectTransform;
            handleRect.anchorMin = new Vector2(0f, 0.5f);
            handleRect.anchorMax = new Vector2(0f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(14f, 24f);
            AddOutline(handle.gameObject, CardEdgeColor, new Vector2(1f, -1f));

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
