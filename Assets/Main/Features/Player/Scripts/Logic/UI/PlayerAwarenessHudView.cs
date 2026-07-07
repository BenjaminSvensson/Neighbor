using Neighbor.Main.Features.Interaction;
using Neighbor.Main.Features.Neighbor;
using Neighbor.Main.Features.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace Neighbor.Main.Features.Player
{
    public sealed class PlayerAwarenessHudView : MonoBehaviour
    {
        private const float MessageDuration = 4.5f;
        private const float TargetSearchInterval = 0.5f;

        private CanvasGroup canvasGroup;
        private Image suspicionFill;
        private Image noiseFill;
        private Image staminaFill;
        private Text awarenessText;
        private Text objectiveText;
        private Text warningText;
        private PlayerController player;
        private PlayerHidingState hidingState;
        private NeighborBrain trackedNeighbor;
        private CoreLoopObjectiveTracker objectiveTracker;
        private float noiseLevel;
        private float cameraWarningUntil;
        private float messageUntil;
        private float nextPlayerSearchTime;
        private float nextNeighborSearchTime;
        private float nextObjectiveSearchTime;

        private void Awake()
        {
            BuildHud();
        }

        private void OnEnable()
        {
            PlayerFeedbackEvents.NoiseEmitted += HandleNoise;
            PlayerFeedbackEvents.CameraDetectedPlayer += HandleCameraDetection;
            PlayerFeedbackEvents.SecurityEscalated += HandleSecurityEscalation;
            PlayerFeedbackEvents.AmbienceZoneChanged += HandleAmbienceZoneChanged;
            PlayerFeedbackEvents.DayPhaseChanged += HandleDayPhaseChanged;
            PlayerFeedbackEvents.CheckpointReached += HandleCheckpointReached;
            PlayerFeedbackEvents.PlayerRespawned += HandlePlayerRespawned;
            PlayerFeedbackEvents.HidingChanged += HandleHidingChanged;
            PlayerFeedbackEvents.OnboardingPrompted += HandleOnboardingPrompted;
            PlayerFeedbackEvents.ObjectiveProgressed += HandleObjectiveProgressed;
            PlayerFeedbackEvents.DoorInteractionReported += HandleDoorInteractionReported;
            PlayerFeedbackEvents.NeighborInvestigationChanged += HandleNeighborInvestigationChanged;
            ResolveObjective(true);
        }

        private void OnDisable()
        {
            PlayerFeedbackEvents.NoiseEmitted -= HandleNoise;
            PlayerFeedbackEvents.CameraDetectedPlayer -= HandleCameraDetection;
            PlayerFeedbackEvents.SecurityEscalated -= HandleSecurityEscalation;
            PlayerFeedbackEvents.AmbienceZoneChanged -= HandleAmbienceZoneChanged;
            PlayerFeedbackEvents.DayPhaseChanged -= HandleDayPhaseChanged;
            PlayerFeedbackEvents.CheckpointReached -= HandleCheckpointReached;
            PlayerFeedbackEvents.PlayerRespawned -= HandlePlayerRespawned;
            PlayerFeedbackEvents.HidingChanged -= HandleHidingChanged;
            PlayerFeedbackEvents.OnboardingPrompted -= HandleOnboardingPrompted;
            PlayerFeedbackEvents.ObjectiveProgressed -= HandleObjectiveProgressed;
            PlayerFeedbackEvents.DoorInteractionReported -= HandleDoorInteractionReported;
            PlayerFeedbackEvents.NeighborInvestigationChanged -= HandleNeighborInvestigationChanged;
            UnsubscribeObjective();
        }

        private void Update()
        {
            ResolveTargets();
            noiseLevel = Mathf.MoveTowards(noiseLevel, 0f, Time.unscaledDeltaTime * 0.55f);

            bool inputBlocked = InteractionOverlayState.IsGameplayInputBlocked;
            canvasGroup.alpha = inputBlocked ? 0f : 1f;

            UpdateAwareness();
            UpdateObjective();
            UpdateNoise();
            UpdateStamina();
            UpdateWarning();
        }

        private void ResolveTargets()
        {
            ResolvePlayer(false);
            ResolveNeighbor(false);
            ResolveObjective(false);
        }

        private void ResolvePlayer(bool force)
        {
            if (player != null)
            {
                hidingState = hidingState != null
                    ? hidingState
                    : player.GetComponent<PlayerHidingState>() ?? player.GetComponentInChildren<PlayerHidingState>();
                return;
            }

            float now = Time.unscaledTime;
            if (!force && now < nextPlayerSearchTime)
            {
                return;
            }

            player = FindAnyObjectByType<PlayerController>();
            hidingState = player != null
                ? player.GetComponent<PlayerHidingState>() ?? player.GetComponentInChildren<PlayerHidingState>()
                : null;
            nextPlayerSearchTime = now + TargetSearchInterval;
        }

        private void ResolveNeighbor(bool force)
        {
            if (trackedNeighbor != null)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (!force && now < nextNeighborSearchTime)
            {
                return;
            }

            trackedNeighbor = FindAnyObjectByType<NeighborBrain>();
            nextNeighborSearchTime = now + TargetSearchInterval;
        }

        private void ResolveObjective(bool force)
        {
            if (objectiveTracker != null)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (!force && now < nextObjectiveSearchTime)
            {
                return;
            }

            CoreLoopObjectiveTracker foundObjective = FindAnyObjectByType<CoreLoopObjectiveTracker>();
            if (foundObjective != objectiveTracker)
            {
                UnsubscribeObjective();
                objectiveTracker = foundObjective;
                SubscribeObjective();
                UpdateObjective();
            }

            nextObjectiveSearchTime = now + TargetSearchInterval;
        }

        private void UpdateAwareness()
        {
            float suspicion = trackedNeighbor != null ? trackedNeighbor.Suspicion : 0f;
            suspicionFill.fillAmount = suspicion;
            suspicionFill.color = GetAwarenessColor(suspicion);

            if (trackedNeighbor == null)
            {
                awarenessText.text = "UNNOTICED";
                return;
            }

            string activeStateText = trackedNeighbor.CurrentState switch
            {
                NeighborBrain.BehaviorState.Chase => "CHASE",
                NeighborBrain.BehaviorState.HuntMode => "HUNTING",
                NeighborBrain.BehaviorState.Investigate => "SEARCHING",
                _ => string.Empty
            };
            if (!string.IsNullOrEmpty(activeStateText))
            {
                awarenessText.text = activeStateText;
                return;
            }

            if (trackedNeighbor.CurrentSuspicionLevel == NeighborBrain.SuspicionLevel.Relaxed)
            {
                awarenessText.text = "UNNOTICED";
                return;
            }

            awarenessText.text = trackedNeighbor.CurrentSuspicionLevel.ToString().ToUpperInvariant();
        }

        private void UpdateNoise()
        {
            noiseFill.fillAmount = noiseLevel;
            noiseFill.color = Color.Lerp(
                new Color(0.35f, 0.72f, 1f, 0.85f),
                new Color(1f, 0.34f, 0.12f, 0.95f),
                noiseLevel);
        }

        private void UpdateStamina()
        {
            if (staminaFill == null)
            {
                return;
            }

            float stamina = player != null ? player.Stamina01 : 1f;
            staminaFill.fillAmount = stamina;
            staminaFill.color = Color.Lerp(
                new Color(1f, 0.3f, 0.16f, 0.95f),
                new Color(0.35f, 1f, 0.68f, 0.9f),
                stamina);
        }

        private void UpdateObjective()
        {
            if (objectiveText == null)
            {
                return;
            }

            if (objectiveTracker == null)
            {
                objectiveText.text = string.Empty;
                return;
            }

            string hint = objectiveTracker.CurrentHint;
            if (string.IsNullOrWhiteSpace(hint))
            {
                objectiveText.text = string.Empty;
                return;
            }

            if (objectiveTracker.IsComplete)
            {
                objectiveText.text = $"OBJECTIVE COMPLETE\n{hint.ToUpperInvariant()}";
                objectiveText.color = new Color(0.62f, 0.95f, 1f, 0.95f);
                return;
            }

            objectiveText.text = $"OBJECTIVE {objectiveTracker.CurrentStepIndex}/{CoreLoopObjectiveTracker.TotalObjectiveSteps}\n{hint.ToUpperInvariant()}";
            objectiveText.color = new Color(0.86f, 0.9f, 0.96f, 0.92f);
        }

        private void UpdateWarning()
        {
            if (Time.unscaledTime < cameraWarningUntil)
            {
                warningText.text = "CAMERA DETECTED YOU";
                warningText.color = new Color(1f, 0.18f, 0.12f, 1f);
                return;
            }

            if (Time.unscaledTime < messageUntil)
            {
                return;
            }

            if (hidingState != null && hidingState.IsHidden)
            {
                if (hidingState.IsDangerouslyExposed)
                {
                    warningText.text = "EXPOSED";
                    warningText.color = new Color(1f, 0.16f, 0.08f, 1f);
                    return;
                }

                if (hidingState.PeekExposure01 >= 0.45f)
                {
                    warningText.text = "PEEKING";
                    warningText.color = new Color(1f, 0.62f, 0.18f, 1f);
                    return;
                }

                if (hidingState.IsCompromised)
                {
                    warningText.text = "FOUND";
                    warningText.color = new Color(1f, 0.12f, 0.08f, 1f);
                    return;
                }

                if (hidingState.WasInspectedRecently)
                {
                    warningText.text = "STAY STILL";
                    warningText.color = new Color(1f, 0.72f, 0.18f, 1f);
                    return;
                }

                if (hidingState.BreathTension01 >= 0.7f)
                {
                    warningText.text = "BREATH TOO LOUD";
                    warningText.color = new Color(1f, 0.58f, 0.18f, 1f);
                    return;
                }
            }

            if (player != null && player.IsExhausted)
            {
                warningText.text = "EXHAUSTED";
                warningText.color = new Color(1f, 0.45f, 0.18f, 1f);
                return;
            }

            warningText.text = string.Empty;
        }

        private void HandleNoise(PlayerFeedbackEvents.NoiseFeedback feedback)
        {
            ResolvePlayer(true);

            float audibleRadius = Mathf.Max(5f, feedback.Radius);
            if (player == null || (player.transform.position - feedback.Origin).sqrMagnitude > audibleRadius * audibleRadius)
            {
                return;
            }

            noiseLevel = Mathf.Max(noiseLevel, feedback.Loudness);
        }

        private void HandleCameraDetection()
        {
            cameraWarningUntil = Time.unscaledTime + 2.25f;
        }

        private void HandleSecurityEscalation(PlayerFeedbackEvents.SecurityEscalationFeedback feedback)
        {
            warningText.text = feedback.Level > 0
                ? BuildSecurityEscalationMessage(feedback)
                : "THE HOUSE RESET";
            warningText.color = feedback.Level > 0
                ? new Color(1f, 0.58f, 0.18f, 1f)
                : new Color(0.8f, 0.82f, 0.86f, 1f);
            messageUntil = Time.unscaledTime + MessageDuration;
        }

        private static string BuildSecurityEscalationMessage(PlayerFeedbackEvents.SecurityEscalationFeedback feedback)
        {
            if (feedback.CameraCount <= 0 && feedback.TrapCount <= 0)
            {
                return $"SECURITY ADAPTED - LEVEL {feedback.Level}";
            }

            return $"SECURITY L{feedback.Level} - CAM {feedback.CameraCount} / TRAP {feedback.TrapCount}";
        }

        private void HandleAmbienceZoneChanged(PlayerFeedbackEvents.AmbienceZoneFeedback feedback)
        {
            warningText.text = feedback.Message.ToUpperInvariant();
            warningText.color = Color.Lerp(
                new Color(0.72f, 0.78f, 0.9f, 0.92f),
                new Color(1f, 0.55f, 0.18f, 1f),
                feedback.Intensity);
            messageUntil = Time.unscaledTime + Mathf.Lerp(2.2f, MessageDuration, feedback.Intensity);
        }

        private void HandleDayPhaseChanged(PlayerFeedbackEvents.DayPhaseFeedback feedback)
        {
            warningText.text = feedback.Message.ToUpperInvariant();
            warningText.color = Color.Lerp(
                new Color(0.52f, 0.62f, 0.92f, 0.95f),
                new Color(1f, 0.52f, 0.16f, 1f),
                feedback.Intensity);
            messageUntil = Time.unscaledTime + Mathf.Lerp(2f, MessageDuration, feedback.Intensity);
        }

        private void HandleCheckpointReached(PlayerFeedbackEvents.CheckpointFeedback feedback)
        {
            warningText.text = $"{feedback.Name} SAVED".ToUpperInvariant();
            warningText.color = new Color(0.58f, 0.9f, 1f, 0.95f);
            messageUntil = Time.unscaledTime + 2.6f;
        }

        private void HandlePlayerRespawned(PlayerFeedbackEvents.RespawnFeedback feedback)
        {
            warningText.text = feedback.UsedCheckpoint
                ? $"RESPAWNED AT {feedback.Name}".ToUpperInvariant()
                : "RESPAWNED AT START";
            warningText.color = feedback.UsedCheckpoint
                ? new Color(0.58f, 0.92f, 1f, 0.96f)
                : new Color(0.78f, 0.84f, 0.94f, 0.94f);
            messageUntil = Time.unscaledTime + 2.8f;
        }

        private void HandleHidingChanged(PlayerFeedbackEvents.HidingFeedback feedback)
        {
            string spotName = feedback.SpotName.ToUpperInvariant();
            warningText.text = feedback.Kind switch
            {
                PlayerFeedbackEvents.HidingFeedbackKind.Entered => $"HIDDEN IN {spotName}",
                PlayerFeedbackEvents.HidingFeedbackKind.Exited => $"LEFT {spotName}",
                PlayerFeedbackEvents.HidingFeedbackKind.Inspected => $"SEARCHED {spotName} - STAY STILL",
                PlayerFeedbackEvents.HidingFeedbackKind.Found => $"FOUND IN {spotName}",
                _ => spotName
            };
            warningText.color = feedback.Kind switch
            {
                PlayerFeedbackEvents.HidingFeedbackKind.Entered => new Color(0.62f, 0.9f, 1f, 0.96f),
                PlayerFeedbackEvents.HidingFeedbackKind.Exited => new Color(0.78f, 0.84f, 0.94f, 0.94f),
                PlayerFeedbackEvents.HidingFeedbackKind.Inspected => Color.Lerp(
                    new Color(1f, 0.72f, 0.18f, 0.98f),
                    new Color(1f, 0.46f, 0.12f, 1f),
                    feedback.BreathTension),
                PlayerFeedbackEvents.HidingFeedbackKind.Found => new Color(1f, 0.12f, 0.08f, 1f),
                _ => new Color(0.82f, 0.86f, 0.92f, 0.95f)
            };
            messageUntil = Time.unscaledTime + (feedback.Kind == PlayerFeedbackEvents.HidingFeedbackKind.Found ? MessageDuration : 2.8f);
        }

        private void HandleObjectiveProgressChanged(CoreLoopObjectiveTracker tracker)
        {
            if (tracker == null)
            {
                return;
            }

            UpdateObjective();
            if (string.IsNullOrWhiteSpace(tracker.CurrentHint))
            {
                return;
            }

            warningText.text = tracker.IsComplete
                ? "ESCAPED"
                : tracker.CurrentHint.ToUpperInvariant();
            warningText.color = tracker.IsComplete
                ? new Color(0.62f, 0.95f, 1f, 0.98f)
                : new Color(0.7f, 0.88f, 1f, 0.96f);
            messageUntil = Time.unscaledTime + (tracker.IsComplete ? MessageDuration : 3f);
        }

        private void HandleOnboardingPrompted(PlayerFeedbackEvents.OnboardingPromptFeedback feedback)
        {
            if (string.IsNullOrWhiteSpace(feedback.Message))
            {
                return;
            }

            warningText.text = feedback.Message.ToUpperInvariant();
            warningText.color = Color.Lerp(
                new Color(0.72f, 0.82f, 0.94f, 0.92f),
                new Color(0.62f, 0.92f, 1f, 0.98f),
                feedback.Intensity);
            messageUntil = Time.unscaledTime + Mathf.Lerp(2.4f, 3.8f, feedback.Intensity);
        }

        private void HandleObjectiveProgressed(PlayerFeedbackEvents.ObjectiveProgressFeedback feedback)
        {
            string message = !string.IsNullOrWhiteSpace(feedback.Message)
                ? feedback.Message
                : feedback.Hint;
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            warningText.text = feedback.IsComplete
                ? $"OBJECTIVE COMPLETE - {message}".ToUpperInvariant()
                : $"OBJECTIVE {feedback.StepIndex}/{feedback.TotalSteps} - {message}".ToUpperInvariant();
            warningText.color = feedback.IsComplete
                ? new Color(0.62f, 0.95f, 1f, 0.98f)
                : new Color(0.68f, 0.9f, 1f, 0.98f);
            messageUntil = Time.unscaledTime + (feedback.IsComplete ? MessageDuration : 3.4f);
        }

        private void HandleDoorInteractionReported(PlayerFeedbackEvents.DoorInteractionFeedback feedback)
        {
            if (string.IsNullOrWhiteSpace(feedback.Message))
            {
                return;
            }

            warningText.text = feedback.Message.ToUpperInvariant();
            warningText.color = GetDoorFeedbackColor(feedback);
            messageUntil = Time.unscaledTime + Mathf.Lerp(1.8f, 3.2f, feedback.Intensity);
        }

        private void HandleNeighborInvestigationChanged(PlayerFeedbackEvents.NeighborInvestigationFeedback feedback)
        {
            warningText.text = feedback.Kind switch
            {
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Started => "NEIGHBOR HEARD SOMETHING",
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Searching => "NEIGHBOR SEARCHING",
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Returning => "NEIGHBOR RETURNING",
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Abandoned => "NEIGHBOR LOST THE TRAIL",
                _ => "NEIGHBOR ALERTED"
            };

            warningText.color = feedback.Kind switch
            {
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Searching => new Color(1f, 0.66f, 0.16f, 1f),
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Returning => new Color(0.76f, 0.84f, 0.94f, 0.95f),
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Abandoned => new Color(0.92f, 0.78f, 0.48f, 0.96f),
                _ => Color.Lerp(
                    new Color(1f, 0.78f, 0.24f, 0.96f),
                    new Color(1f, 0.3f, 0.1f, 1f),
                    Mathf.Max(feedback.Suspicion, feedback.Urgency))
            };
            messageUntil = Time.unscaledTime + Mathf.Lerp(2.4f, MessageDuration, Mathf.Max(feedback.Suspicion, feedback.Urgency));
        }

        private void BuildHud()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 75;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            awarenessText = CreateText("Awareness", font, 14, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(awarenessText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(280f, 24f));

            objectiveText = CreateText("Objective", font, 14, FontStyle.Bold, TextAnchor.UpperLeft);
            objectiveText.color = new Color(0.86f, 0.9f, 0.96f, 0.92f);
            SetRect(objectiveText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(232f, -48f), new Vector2(384f, 46f));

            Image suspicionBackground = CreateImage("SuspicionBackground", new Color(0f, 0f, 0f, 0.55f));
            SetRect(suspicionBackground.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(280f, 8f));
            suspicionFill = CreateFill("SuspicionFill", suspicionBackground.transform);

            Text staminaLabel = CreateText("StaminaLabel", font, 12, FontStyle.Bold, TextAnchor.MiddleLeft);
            staminaLabel.text = "STAMINA";
            staminaLabel.color = new Color(1f, 1f, 1f, 0.68f);
            SetRect(staminaLabel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(42f, 136f), new Vector2(78f, 18f));

            Image staminaBackground = CreateImage("StaminaBackground", new Color(0f, 0f, 0f, 0.5f));
            SetRect(staminaBackground.rectTransform, Vector2.zero, Vector2.zero, new Vector2(132f, 140f), new Vector2(170f, 7f));
            staminaFill = CreateFill("StaminaFill", staminaBackground.transform);

            Text noiseLabel = CreateText("NoiseLabel", font, 12, FontStyle.Bold, TextAnchor.MiddleLeft);
            noiseLabel.text = "NOISE";
            noiseLabel.color = new Color(1f, 1f, 1f, 0.68f);
            SetRect(noiseLabel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(32f, 104f), new Vector2(58f, 18f));

            Image noiseBackground = CreateImage("NoiseBackground", new Color(0f, 0f, 0f, 0.5f));
            SetRect(noiseBackground.rectTransform, Vector2.zero, Vector2.zero, new Vector2(102f, 108f), new Vector2(170f, 7f));
            noiseFill = CreateFill("NoiseFill", noiseBackground.transform);

            warningText = CreateText("Warning", font, 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(warningText.rectTransform, new Vector2(0.5f, 0.75f), new Vector2(0.5f, 0.75f), Vector2.zero, new Vector2(560f, 32f));
        }

        private Text CreateText(string objectName, Font font, int fontSize, FontStyle style, TextAnchor alignment)
        {
            GameObject textObject = new(objectName, typeof(RectTransform));
            textObject.transform.SetParent(transform, false);
            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private Image CreateImage(string objectName, Color color)
        {
            GameObject imageObject = new(objectName, typeof(RectTransform));
            imageObject.transform.SetParent(transform, false);
            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Image CreateFill(string objectName, Transform parent)
        {
            GameObject fillObject = new(objectName, typeof(RectTransform));
            fillObject.transform.SetParent(parent, false);
            Image fill = fillObject.AddComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.raycastTarget = false;
            SetRect(fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return fill;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Color GetAwarenessColor(float suspicion)
        {
            return suspicion < 0.48f
                ? new Color(1f, 0.76f, 0.2f, 0.9f)
                : Color.Lerp(new Color(1f, 0.48f, 0.12f, 0.95f), new Color(1f, 0.08f, 0.05f, 1f), suspicion);
        }

        private void SubscribeObjective()
        {
            if (objectiveTracker == null)
            {
                return;
            }

            objectiveTracker.ProgressChanged -= HandleObjectiveProgressChanged;
            objectiveTracker.ProgressChanged += HandleObjectiveProgressChanged;
        }

        private void UnsubscribeObjective()
        {
            if (objectiveTracker != null)
            {
                objectiveTracker.ProgressChanged -= HandleObjectiveProgressChanged;
            }
        }

        private static Color GetDoorFeedbackColor(PlayerFeedbackEvents.DoorInteractionFeedback feedback)
        {
            if (feedback.Kind == PlayerFeedbackEvents.DoorInteractionFeedbackKind.Unlocked)
            {
                return new Color(0.55f, 0.9f, 1f, 0.95f);
            }

            if (feedback.Kind == PlayerFeedbackEvents.DoorInteractionFeedbackKind.Blocked)
            {
                return Color.Lerp(
                    new Color(1f, 0.58f, 0.16f, 0.96f),
                    new Color(1f, 0.2f, 0.1f, 1f),
                    feedback.Intensity);
            }

            if (feedback.Kind == PlayerFeedbackEvents.DoorInteractionFeedbackKind.Locked)
            {
                return Color.Lerp(
                    new Color(1f, 0.72f, 0.22f, 0.96f),
                    new Color(1f, 0.36f, 0.12f, 1f),
                    feedback.Intensity);
            }

            return new Color(0.82f, 0.86f, 0.92f, 0.95f);
        }
    }
}
