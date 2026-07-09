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
        private Image tensionFill;
        private Image staminaFill;
        private Text noiseLabel;
        private Text awarenessText;
        private Text stealthStatusText;
        private Text objectiveText;
        private Text warningText;
        private PlayerController player;
        private PlayerHidingState hidingState;
        private NeighborBrain trackedNeighbor;
        private CoreLoopObjectiveTracker objectiveTracker;
        [SerializeField, Range(0f, 1f)] private float heardNoiseListenerHudBoost = 0.08f;
        private float noiseLevel;
        private PlayerFeedbackEvents.StealthLoopPhase lastStealthLoopPhase = PlayerFeedbackEvents.StealthLoopPhase.Quiet;
        private float lastStealthLoopSuspicion;
        private float lastStealthLoopNoise;
        private float lastStealthLoopTension;
        private bool lastStealthLoopIsCalming;
        private float stealthLoopStatusUntil;
        private int lastNeighborMemoryCount;
        private float lastNeighborMemorySuspicion;
        private bool hasLastNeighborMemoryKind;
        private PlayerFeedbackEvents.NeighborMemoryClueKind lastNeighborMemoryKind;
        private float neighborMemoryStatusUntil;
        private float lastNeighborTrailInvestigationPressure;
        private float neighborTrailInvestigationUntil;
        private float lastNeighborHeardNoise;
        private float neighborHeardNoiseDuration;
        private float neighborHeardNoiseUntil;
        private int lastNeighborHeardListenerCount;
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
            PlayerFeedbackEvents.StealthLoopChanged += HandleStealthLoopChanged;
            PlayerFeedbackEvents.NeighborMemoryChanged += HandleNeighborMemoryChanged;
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
            PlayerFeedbackEvents.StealthLoopChanged -= HandleStealthLoopChanged;
            PlayerFeedbackEvents.NeighborMemoryChanged -= HandleNeighborMemoryChanged;
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
            UpdateStealthStatus();
            UpdateObjective();
            UpdateNoise();
            UpdateTension();
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
            float memoryPressure = GetDisplayedMemoryPressure();

            if (trackedNeighbor == null)
            {
                awarenessText.text = memoryPressure >= 0.35f ? BuildAwarenessMemoryText(false) : "UNNOTICED";
                return;
            }

            if (trackedNeighbor.IsHuntingMemoryClue
                || trackedNeighbor.IsCurrentInvestigationTrailRelated
                || IsTrailInvestigationActive())
            {
                awarenessText.text = BuildAwarenessMemoryText(true);
                return;
            }

            int memoryCount = GetDisplayedMemoryCount();
            if (trackedNeighbor.PostChaseTension01 >= 0.05f && memoryPressure >= 0.55f)
            {
                awarenessText.text = BuildAwarenessMemoryText(memoryCount >= 3);
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

            if (trackedNeighbor.PostChaseTension01 >= 0.05f)
            {
                awarenessText.text = "TENSION";
                return;
            }

            if (memoryPressure >= 0.35f)
            {
                awarenessText.text = BuildAwarenessMemoryText(memoryCount >= 3);
                return;
            }

            if (trackedNeighbor.CurrentSuspicionLevel == NeighborBrain.SuspicionLevel.Relaxed)
            {
                awarenessText.text = "UNNOTICED";
                return;
            }

            awarenessText.text = trackedNeighbor.CurrentSuspicionLevel.ToString().ToUpperInvariant();
        }

        private void UpdateStealthStatus()
        {
            if (stealthStatusText == null)
            {
                return;
            }

            PlayerFeedbackEvents.StealthLoopPhase phase = GetDisplayedStealthLoopPhase();
            bool hasRecentLoopStatus = HasRecentStealthLoopStatus();
            float suspicion = trackedNeighbor != null
                ? trackedNeighbor.Suspicion
                : hasRecentLoopStatus ? lastStealthLoopSuspicion : 0f;
            float tension = Mathf.Max(
                trackedNeighbor != null ? trackedNeighbor.PostChaseTension01 : 0f,
                Mathf.Max(
                    hidingState != null ? hidingState.BreathTension01 : 0f,
                    hasRecentLoopStatus ? lastStealthLoopTension : 0f));
            float memoryPressure = GetDisplayedMemoryPressure();
            tension = Mathf.Max(tension, memoryPressure);
            float noise = Mathf.Max(GetDisplayedNoiseLevel(), hasRecentLoopStatus ? lastStealthLoopNoise : 0f);
            bool isCalming = hasRecentLoopStatus && lastStealthLoopIsCalming;
            stealthStatusText.text = BuildStealthStatusText(phase, suspicion, noise, tension, memoryPressure, isCalming);
            stealthStatusText.color = GetStealthStatusColor(phase, suspicion, noise, tension, memoryPressure, isCalming);
        }

        private void UpdateNoise()
        {
            float displayedNoise = GetDisplayedNoiseLevel();
            float heardNoisePressure = GetDisplayedHeardNoisePressure();
            bool heardByNeighbor = heardNoisePressure > 0.05f;
            noiseFill.fillAmount = displayedNoise;
            noiseFill.color = heardByNeighbor
                ? Color.Lerp(
                    new Color(1f, 0.68f, 0.18f, 0.96f),
                    new Color(1f, 0.16f, 0.06f, 1f),
                    heardNoisePressure)
                : Color.Lerp(
                    new Color(0.35f, 0.72f, 1f, 0.85f),
                    new Color(1f, 0.34f, 0.12f, 0.95f),
                    displayedNoise);

            if (noiseLabel != null)
            {
                noiseLabel.text = heardByNeighbor ? BuildHeardNoiseLabel() : "NOISE";
                noiseLabel.color = heardByNeighbor
                    ? new Color(1f, 0.54f, 0.14f, 0.96f)
                    : new Color(1f, 1f, 1f, 0.68f);
            }
        }

        private void UpdateTension()
        {
            if (tensionFill == null)
            {
                return;
            }

            float postChaseTension = trackedNeighbor != null ? trackedNeighbor.PostChaseTension01 : 0f;
            float breathTension = hidingState != null ? hidingState.BreathTension01 : 0f;
            float tension = Mathf.Max(
                Mathf.Max(postChaseTension, breathTension),
                Mathf.Max(GetRecentStealthLoopTension(), GetDisplayedMemoryPressure()));
            tensionFill.fillAmount = tension;
            tensionFill.color = Color.Lerp(
                new Color(0.36f, 0.4f, 0.72f, 0.8f),
                new Color(1f, 0.48f, 0.13f, 0.98f),
                tension);
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

            float heardNoisePressure = GetDisplayedHeardNoisePressure();
            if (heardNoisePressure >= 0.35f)
            {
                warningText.text = GetDisplayedHeardNoiseListenerCount() > 1 ? "THEY HEARD THAT" : "HE HEARD THAT";
                warningText.color = Color.Lerp(
                    new Color(1f, 0.72f, 0.18f, 0.98f),
                    new Color(1f, 0.26f, 0.08f, 1f),
                    heardNoisePressure);
                return;
            }

            if (noiseLevel >= 0.72f)
            {
                warningText.text = "LOUD NOISE";
                warningText.color = Color.Lerp(
                    new Color(1f, 0.68f, 0.18f, 0.96f),
                    new Color(1f, 0.26f, 0.08f, 1f),
                    noiseLevel);
                return;
            }

            if (noiseLevel >= 0.45f
                && trackedNeighbor != null
                && (trackedNeighbor.CurrentState == NeighborBrain.BehaviorState.Investigate
                    || trackedNeighbor.CurrentState == NeighborBrain.BehaviorState.HuntMode
                    || trackedNeighbor.CurrentSuspicionLevel >= NeighborBrain.SuspicionLevel.Curious))
            {
                warningText.text = "HE HEARD THAT";
                warningText.color = new Color(1f, 0.62f, 0.16f, 0.96f);
                return;
            }

            float memoryPressure = GetDisplayedMemoryPressure();
            int memoryCount = GetDisplayedMemoryCount();
            if (trackedNeighbor != null && trackedNeighbor.PostChaseTension01 >= 0.35f)
            {
                if (trackedNeighbor.IsHuntingMemoryClue || IsTrailInvestigationActive() || memoryPressure >= 0.55f)
                {
                    warningText.text = BuildPersistentMemoryWarningText(memoryCount >= 3 || IsTrailInvestigationActive());
                    warningText.color = Color.Lerp(
                        new Color(1f, 0.62f, 0.16f, 0.96f),
                        new Color(1f, 0.28f, 0.08f, 1f),
                        Mathf.Max(memoryPressure, trackedNeighbor.PostChaseTension01));
                    return;
                }

                warningText.text = "HE IS STILL SEARCHING";
                warningText.color = new Color(1f, 0.64f, 0.18f, 0.96f);
                return;
            }

            float recentLoopTension = GetRecentStealthLoopTension();
            if (recentLoopTension >= 0.55f)
            {
                warningText.text = lastStealthLoopPhase switch
                {
                    PlayerFeedbackEvents.StealthLoopPhase.PostChase => "POST-CHASE TENSION",
                    PlayerFeedbackEvents.StealthLoopPhase.Hiding => "HOLD YOUR BREATH",
                    PlayerFeedbackEvents.StealthLoopPhase.Chased => "DANGER CLOSE",
                    PlayerFeedbackEvents.StealthLoopPhase.Certain => "ALMOST SEEN",
                    _ => "TENSION HIGH"
                };
                warningText.color = Color.Lerp(
                    new Color(1f, 0.68f, 0.18f, 0.96f),
                    new Color(1f, 0.28f, 0.08f, 1f),
                    recentLoopTension);
                return;
            }

            if (memoryPressure >= 0.55f)
            {
                warningText.text = BuildPersistentMemoryWarningText(memoryCount >= 3);
                warningText.color = Color.Lerp(
                    new Color(0.9f, 0.82f, 0.62f, 0.95f),
                    new Color(1f, 0.36f, 0.12f, 1f),
                    memoryPressure);
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

            float feedbackNoise = GetNoiseFeedbackPressure(feedback);
            noiseLevel = Mathf.Max(noiseLevel, feedbackNoise);
            if (feedback.HeardByNeighbor)
            {
                if (Time.unscaledTime >= neighborHeardNoiseUntil)
                {
                    lastNeighborHeardNoise = 0f;
                    lastNeighborHeardListenerCount = 0;
                }

                lastNeighborHeardNoise = Mathf.Max(lastNeighborHeardNoise, feedbackNoise);
                lastNeighborHeardListenerCount = Mathf.Max(lastNeighborHeardListenerCount, Mathf.Max(1, feedback.NeighborListenerCount));
                neighborHeardNoiseDuration = Mathf.Lerp(1.8f, 3.4f, lastNeighborHeardNoise);
                neighborHeardNoiseUntil = Time.unscaledTime + neighborHeardNoiseDuration;
            }
        }

        private float GetNoiseFeedbackPressure(PlayerFeedbackEvents.NoiseFeedback feedback)
        {
            if (!feedback.HeardByNeighbor)
            {
                return feedback.Loudness;
            }

            float listenerPressure = Mathf.Clamp01(Mathf.Max(0, feedback.NeighborListenerCount - 1) / 2f)
                * heardNoiseListenerHudBoost;
            return Mathf.Clamp01(Mathf.Max(feedback.Loudness, feedback.Urgency) + listenerPressure);
        }

        private string BuildHeardNoiseLabel()
        {
            int listenerCount = GetDisplayedHeardNoiseListenerCount();
            return listenerCount > 1 ? $"HEARD x{Mathf.Min(listenerCount, 9)}" : "HEARD";
        }

        private int GetDisplayedHeardNoiseListenerCount()
        {
            return GetDisplayedHeardNoisePressure() > 0.05f ? lastNeighborHeardListenerCount : 0;
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
                PlayerFeedbackEvents.HidingFeedbackKind.Exited => feedback.BreathTension >= 0.45f
                    ? $"NOISY EXIT FROM {spotName}"
                    : $"LEFT {spotName}",
                PlayerFeedbackEvents.HidingFeedbackKind.Inspected => $"SEARCHED {spotName} - STAY STILL",
                PlayerFeedbackEvents.HidingFeedbackKind.BreathNoisy => "BREATH TOO LOUD",
                PlayerFeedbackEvents.HidingFeedbackKind.Found => $"FOUND IN {spotName}",
                PlayerFeedbackEvents.HidingFeedbackKind.Recovered => "BREATH STEADY",
                _ => spotName
            };
            warningText.color = feedback.Kind switch
            {
                PlayerFeedbackEvents.HidingFeedbackKind.Entered => new Color(0.62f, 0.9f, 1f, 0.96f),
                PlayerFeedbackEvents.HidingFeedbackKind.Exited => Color.Lerp(
                    new Color(0.78f, 0.84f, 0.94f, 0.94f),
                    new Color(1f, 0.5f, 0.12f, 0.98f),
                    feedback.BreathTension),
                PlayerFeedbackEvents.HidingFeedbackKind.Inspected => Color.Lerp(
                        new Color(1f, 0.72f, 0.18f, 0.98f),
                        new Color(1f, 0.46f, 0.12f, 1f),
                        feedback.BreathTension),
                PlayerFeedbackEvents.HidingFeedbackKind.BreathNoisy => Color.Lerp(
                    new Color(1f, 0.64f, 0.16f, 0.98f),
                    new Color(1f, 0.24f, 0.08f, 1f),
                    feedback.BreathTension),
                PlayerFeedbackEvents.HidingFeedbackKind.Found => new Color(1f, 0.12f, 0.08f, 1f),
                PlayerFeedbackEvents.HidingFeedbackKind.Recovered => new Color(0.62f, 0.95f, 1f, 0.96f),
                _ => new Color(0.82f, 0.86f, 0.92f, 0.95f)
            };
            messageUntil = Time.unscaledTime + (feedback.Kind == PlayerFeedbackEvents.HidingFeedbackKind.Found
                ? MessageDuration
                : feedback.Kind == PlayerFeedbackEvents.HidingFeedbackKind.BreathNoisy ? 3.2f
                : feedback.Kind == PlayerFeedbackEvents.HidingFeedbackKind.Recovered ? 2.4f : 2.8f);
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

        private void HandleStealthLoopChanged(PlayerFeedbackEvents.StealthLoopFeedback feedback)
        {
            lastStealthLoopPhase = feedback.Phase;
            lastStealthLoopSuspicion = feedback.Suspicion;
            lastStealthLoopNoise = feedback.Noise;
            lastStealthLoopTension = feedback.Tension;
            lastStealthLoopIsCalming = feedback.IsCalming;
            stealthLoopStatusUntil = Time.unscaledTime + Mathf.Lerp(
                2.2f,
                6f,
                Mathf.Max(feedback.Suspicion, feedback.Noise, feedback.Tension));

            if (feedback.Noise > 0f)
            {
                noiseLevel = Mathf.Max(noiseLevel, feedback.Noise);
            }

            if (feedback.Phase == PlayerFeedbackEvents.StealthLoopPhase.Quiet
                || string.IsNullOrWhiteSpace(feedback.Message))
            {
                return;
            }

            warningText.text = feedback.Message.ToUpperInvariant();
            warningText.color = GetStealthLoopColor(feedback);
            messageUntil = Time.unscaledTime + Mathf.Lerp(1.9f, MessageDuration, Mathf.Max(feedback.Suspicion, feedback.Tension));
        }

        private void HandleNeighborMemoryChanged(PlayerFeedbackEvents.NeighborMemoryFeedback feedback)
        {
            lastNeighborMemoryCount = feedback.TotalMemoryCount;
            lastNeighborMemorySuspicion = feedback.Urgency;
            hasLastNeighborMemoryKind = true;
            lastNeighborMemoryKind = feedback.Kind;
            neighborMemoryStatusUntil = Time.unscaledTime + Mathf.Lerp(
                4f,
                9f,
                Mathf.Max(feedback.Urgency, Mathf.Clamp01(feedback.TotalMemoryCount / 4f)));
            warningText.text = feedback.Kind switch
            {
                PlayerFeedbackEvents.NeighborMemoryClueKind.DoorOpened => "HE REMEMBERS THAT DOOR",
                PlayerFeedbackEvents.NeighborMemoryClueKind.ObjectMoved => "HE NOTICED THE ROOM CHANGED",
                PlayerFeedbackEvents.NeighborMemoryClueKind.GlassBroken => "HE REMEMBERS THE BROKEN GLASS",
                PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen => "HE KNOWS A KEY IS GONE",
                _ => "HE FOUND A CLUE"
            };
            warningText.color = Color.Lerp(
                new Color(0.8f, 0.86f, 0.96f, 0.95f),
                new Color(1f, 0.38f, 0.14f, 1f),
                feedback.Urgency);
            messageUntil = Time.unscaledTime + Mathf.Lerp(2.3f, MessageDuration, feedback.Urgency);
        }

        private void HandleNeighborInvestigationChanged(PlayerFeedbackEvents.NeighborInvestigationFeedback feedback)
        {
            if (feedback.HasMemoryClueKind)
            {
                hasLastNeighborMemoryKind = true;
                lastNeighborMemoryKind = feedback.MemoryClueKind;
            }

            UpdateTrailInvestigationPulse(feedback);
            warningText.text = BuildInvestigationWarningText(feedback);
            warningText.color = GetInvestigationWarningColor(feedback);
            messageUntil = Time.unscaledTime + Mathf.Lerp(2.4f, MessageDuration, Mathf.Max(feedback.Suspicion, feedback.Urgency));
        }

        private void UpdateTrailInvestigationPulse(PlayerFeedbackEvents.NeighborInvestigationFeedback feedback)
        {
            if (!feedback.IsTrailRelated)
            {
                return;
            }

            if (feedback.Kind == PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Returning
                || feedback.Kind == PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Abandoned)
            {
                lastNeighborTrailInvestigationPressure = 0f;
                neighborTrailInvestigationUntil = 0f;
                return;
            }

            float pressure = Mathf.Max(feedback.Suspicion, feedback.Urgency);
            lastNeighborTrailInvestigationPressure = Mathf.Max(lastNeighborTrailInvestigationPressure, pressure);
            neighborTrailInvestigationUntil = Time.unscaledTime + Mathf.Lerp(3.2f, 6.5f, pressure);
        }

        private static string BuildInvestigationWarningText(PlayerFeedbackEvents.NeighborInvestigationFeedback feedback)
        {
            if (feedback.IsTrailRelated)
            {
                if (feedback.HasMemoryClueKind)
                {
                    return BuildMemoryTrailWarningText(feedback);
                }

                return feedback.Kind switch
                {
                    PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.FollowingTrail => "HE IS FOLLOWING YOUR TRAIL",
                    PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Searching => "SEARCHING YOUR TRAIL",
                    PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.CheckingHideSpot => "HE KNOWS YOUR HIDING SPOT",
                    PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Returning => "TRAIL CLEARED",
                    PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Abandoned => "NEIGHBOR LOST THE TRAIL",
                    _ => "NEIGHBOR ON YOUR TRAIL"
                };
            }

            return feedback.Kind switch
            {
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Started => "NEIGHBOR HEARD SOMETHING",
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.FollowingTrail => "HE IS FOLLOWING YOUR TRAIL",
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Searching => "NEIGHBOR SEARCHING",
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.CheckingHideSpot => "HE KNOWS YOUR HIDING SPOT",
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Returning => "NOISE CLEARED",
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Abandoned => "LOST THE NOISE",
                _ => "NEIGHBOR ALERTED"
            };
        }

        private static string BuildMemoryTrailWarningText(PlayerFeedbackEvents.NeighborInvestigationFeedback feedback)
        {
            return feedback.Kind switch
            {
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.FollowingTrail =>
                    feedback.MemoryClueKind switch
                    {
                        PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen => "HE IS TRACKING THE STOLEN KEY",
                        PlayerFeedbackEvents.NeighborMemoryClueKind.GlassBroken => "HE IS CHECKING THE BROKEN GLASS",
                        PlayerFeedbackEvents.NeighborMemoryClueKind.ObjectMoved => "HE IS CHECKING THE MOVED OBJECT",
                        PlayerFeedbackEvents.NeighborMemoryClueKind.DoorOpened => "HE IS CHECKING THAT DOOR",
                        _ => "HE IS FOLLOWING YOUR TRAIL"
                    },
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Searching =>
                    feedback.MemoryClueKind switch
                    {
                        PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen => "SEARCHING THE MISSING KEY",
                        PlayerFeedbackEvents.NeighborMemoryClueKind.GlassBroken => "SEARCHING THE BROKEN GLASS",
                        PlayerFeedbackEvents.NeighborMemoryClueKind.ObjectMoved => "SEARCHING THE MOVED OBJECT",
                        PlayerFeedbackEvents.NeighborMemoryClueKind.DoorOpened => "SEARCHING THAT DOOR",
                        _ => "SEARCHING YOUR TRAIL"
                    },
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Returning =>
                    $"{GetMemoryClueLabel(feedback.MemoryClueKind)} TRAIL CLEARED",
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Abandoned =>
                    $"LOST THE {GetMemoryClueLabel(feedback.MemoryClueKind)} TRAIL",
                _ => "NEIGHBOR ON YOUR TRAIL"
            };
        }

        private static string GetMemoryClueLabel(PlayerFeedbackEvents.NeighborMemoryClueKind kind)
        {
            return kind switch
            {
                PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen => "KEY",
                PlayerFeedbackEvents.NeighborMemoryClueKind.GlassBroken => "GLASS",
                PlayerFeedbackEvents.NeighborMemoryClueKind.ObjectMoved => "OBJECT",
                PlayerFeedbackEvents.NeighborMemoryClueKind.DoorOpened => "DOOR",
                _ => "CLUE"
            };
        }

        private static Color GetInvestigationWarningColor(PlayerFeedbackEvents.NeighborInvestigationFeedback feedback)
        {
            float pressure = Mathf.Max(feedback.Suspicion, feedback.Urgency);
            if (feedback.IsTrailRelated)
            {
                return Color.Lerp(
                    new Color(1f, 0.72f, 0.18f, 0.98f),
                    new Color(1f, 0.22f, 0.06f, 1f),
                    pressure);
            }

            return feedback.Kind switch
            {
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.FollowingTrail => Color.Lerp(
                    new Color(1f, 0.72f, 0.18f, 0.98f),
                    new Color(1f, 0.28f, 0.08f, 1f),
                    pressure),
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Searching => new Color(1f, 0.66f, 0.16f, 1f),
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.CheckingHideSpot => new Color(1f, 0.16f, 0.05f, 1f),
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Returning => new Color(0.76f, 0.84f, 0.94f, 0.95f),
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Abandoned => new Color(0.92f, 0.78f, 0.48f, 0.96f),
                _ => Color.Lerp(
                    new Color(1f, 0.78f, 0.24f, 0.96f),
                    new Color(1f, 0.3f, 0.1f, 1f),
                    pressure)
            };
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

            noiseLabel = CreateText("NoiseLabel", font, 12, FontStyle.Bold, TextAnchor.MiddleLeft);
            noiseLabel.text = "NOISE";
            noiseLabel.color = new Color(1f, 1f, 1f, 0.68f);
            SetRect(noiseLabel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(32f, 104f), new Vector2(58f, 18f));

            Image noiseBackground = CreateImage("NoiseBackground", new Color(0f, 0f, 0f, 0.5f));
            SetRect(noiseBackground.rectTransform, Vector2.zero, Vector2.zero, new Vector2(102f, 108f), new Vector2(170f, 7f));
            noiseFill = CreateFill("NoiseFill", noiseBackground.transform);

            Text tensionLabel = CreateText("TensionLabel", font, 12, FontStyle.Bold, TextAnchor.MiddleLeft);
            tensionLabel.text = "TENSION";
            tensionLabel.color = new Color(1f, 1f, 1f, 0.68f);
            SetRect(tensionLabel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(42f, 76f), new Vector2(80f, 18f));

            Image tensionBackground = CreateImage("TensionBackground", new Color(0f, 0f, 0f, 0.5f));
            SetRect(tensionBackground.rectTransform, Vector2.zero, Vector2.zero, new Vector2(132f, 80f), new Vector2(170f, 7f));
            tensionFill = CreateFill("TensionFill", tensionBackground.transform);

            warningText = CreateText("Warning", font, 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(warningText.rectTransform, new Vector2(0.5f, 0.75f), new Vector2(0.5f, 0.75f), Vector2.zero, new Vector2(560f, 32f));

            stealthStatusText = CreateText("StealthStatus", font, 12, FontStyle.Bold, TextAnchor.MiddleCenter);
            stealthStatusText.color = new Color(0.78f, 0.86f, 0.96f, 0.82f);
            SetRect(stealthStatusText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(360f, 20f));
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

        private static Color GetStealthLoopColor(PlayerFeedbackEvents.StealthLoopFeedback feedback)
        {
            switch (feedback.Phase)
            {
                case PlayerFeedbackEvents.StealthLoopPhase.Chased:
                    return new Color(1f, 0.1f, 0.06f, 1f);
                case PlayerFeedbackEvents.StealthLoopPhase.Certain:
                    return new Color(1f, 0.24f, 0.08f, 1f);
                case PlayerFeedbackEvents.StealthLoopPhase.PostChase:
                    return feedback.IsCalming
                        ? Color.Lerp(
                            new Color(0.66f, 0.84f, 1f, 0.95f),
                            new Color(1f, 0.64f, 0.18f, 1f),
                            feedback.Tension)
                        : new Color(1f, 0.58f, 0.16f, 1f);
                case PlayerFeedbackEvents.StealthLoopPhase.Searching:
                    return new Color(1f, 0.68f, 0.18f, 0.98f);
                case PlayerFeedbackEvents.StealthLoopPhase.Hiding:
                    return Color.Lerp(
                        new Color(0.62f, 0.9f, 1f, 0.95f),
                        new Color(1f, 0.54f, 0.14f, 1f),
                        feedback.Tension);
                default:
                    return Color.Lerp(
                        new Color(0.78f, 0.86f, 0.96f, 0.95f),
                        new Color(1f, 0.5f, 0.14f, 1f),
                        Mathf.Max(feedback.Suspicion, feedback.Noise));
            }
        }

        private PlayerFeedbackEvents.StealthLoopPhase GetDisplayedStealthLoopPhase()
        {
            if (hidingState != null && hidingState.IsHidden)
            {
                return PlayerFeedbackEvents.StealthLoopPhase.Hiding;
            }

            if (trackedNeighbor != null)
            {
                return trackedNeighbor.CurrentState switch
                {
                    NeighborBrain.BehaviorState.Chase => PlayerFeedbackEvents.StealthLoopPhase.Chased,
                    NeighborBrain.BehaviorState.Catching => PlayerFeedbackEvents.StealthLoopPhase.Chased,
                    NeighborBrain.BehaviorState.HuntMode => PlayerFeedbackEvents.StealthLoopPhase.PostChase,
                    NeighborBrain.BehaviorState.Investigate => PlayerFeedbackEvents.StealthLoopPhase.Searching,
                    NeighborBrain.BehaviorState.DoorSecurityCheck => PlayerFeedbackEvents.StealthLoopPhase.Searching,
                    _ => GetSuspicionPhase(trackedNeighbor.CurrentSuspicionLevel)
                };
            }

            return Time.unscaledTime < stealthLoopStatusUntil
                ? lastStealthLoopPhase
                : PlayerFeedbackEvents.StealthLoopPhase.Quiet;
        }

        private static PlayerFeedbackEvents.StealthLoopPhase GetSuspicionPhase(NeighborBrain.SuspicionLevel level)
        {
            return level switch
            {
                NeighborBrain.SuspicionLevel.Certain => PlayerFeedbackEvents.StealthLoopPhase.Certain,
                NeighborBrain.SuspicionLevel.Suspicious => PlayerFeedbackEvents.StealthLoopPhase.Suspicious,
                NeighborBrain.SuspicionLevel.Curious => PlayerFeedbackEvents.StealthLoopPhase.Curious,
                _ => PlayerFeedbackEvents.StealthLoopPhase.Quiet
            };
        }

        private float GetDisplayedMemoryPressure()
        {
            float trackedPressure = trackedNeighbor != null ? trackedNeighbor.RememberedClueTension01 : 0f;
            float recentPressure = Time.unscaledTime < neighborMemoryStatusUntil ? lastNeighborMemorySuspicion : 0f;
            float trailPressure = Time.unscaledTime < neighborTrailInvestigationUntil
                ? lastNeighborTrailInvestigationPressure
                : 0f;
            return Mathf.Max(Mathf.Max(trackedPressure, recentPressure), trailPressure);
        }

        private int GetDisplayedMemoryCount()
        {
            int trackedCount = trackedNeighbor != null ? trackedNeighbor.TotalRememberedClueCount : 0;
            int recentCount = Time.unscaledTime < neighborMemoryStatusUntil ? lastNeighborMemoryCount : 0;
            return Mathf.Max(trackedCount, recentCount);
        }

        private bool IsTrailInvestigationActive()
        {
            if (trackedNeighbor != null
                && (trackedNeighbor.IsHuntingMemoryClue || trackedNeighbor.IsCurrentInvestigationTrailRelated))
            {
                return true;
            }

            return Time.unscaledTime < neighborTrailInvestigationUntil
                && lastNeighborTrailInvestigationPressure > 0.05f;
        }

        private string BuildAwarenessMemoryText(bool trailRelated)
        {
            if (!TryGetDisplayedMemoryClueKind(out PlayerFeedbackEvents.NeighborMemoryClueKind kind))
            {
                return trailRelated ? "TRAIL" : "MEMORY";
            }

            return $"{GetMemoryClueShortLabel(kind)} {(trailRelated ? "TRAIL" : "MEMORY")}";
        }

        private string BuildMemoryStatusLabel(bool trailRelated)
        {
            if (!TryGetDisplayedMemoryClueKind(out PlayerFeedbackEvents.NeighborMemoryClueKind kind))
            {
                return trailRelated ? "YOUR TRAIL" : "MEMORY";
            }

            return $"{GetMemoryClueShortLabel(kind)} {(trailRelated ? "TRAIL" : "MEMORY")}";
        }

        private string BuildPersistentMemoryWarningText(bool trailRelated)
        {
            if (!TryGetDisplayedMemoryClueKind(out PlayerFeedbackEvents.NeighborMemoryClueKind kind))
            {
                return trailRelated ? "HE IS FOLLOWING YOUR TRAIL" : "HE REMEMBERS";
            }

            return trailRelated
                ? BuildTrackingMemoryWarningText(kind)
                : BuildRememberedMemoryWarningText(kind);
        }

        private bool TryGetDisplayedMemoryClueKind(out PlayerFeedbackEvents.NeighborMemoryClueKind kind)
        {
            if (trackedNeighbor != null)
            {
                if (trackedNeighbor.IsHuntingMemoryClue)
                {
                    kind = trackedNeighbor.CurrentHuntMemoryClueKind;
                    return true;
                }

                if (trackedNeighbor.IsCurrentInvestigationMemoryClue)
                {
                    kind = trackedNeighbor.CurrentInvestigationMemoryClueKind;
                    return true;
                }

                if (trackedNeighbor.HasPendingMemoryClueFollowUp)
                {
                    kind = trackedNeighbor.PendingMemoryClueKind;
                    return true;
                }

                if (trackedNeighbor.TotalRememberedClueCount > 0
                    && trackedNeighbor.RememberedClueTension01 > 0.01f)
                {
                    kind = trackedNeighbor.LastRememberedClueKind;
                    return true;
                }
            }

            if (hasLastNeighborMemoryKind
                && (Time.unscaledTime < neighborMemoryStatusUntil
                    || Time.unscaledTime < neighborTrailInvestigationUntil))
            {
                kind = lastNeighborMemoryKind;
                return true;
            }

            kind = default;
            return false;
        }

        private static string GetMemoryClueShortLabel(PlayerFeedbackEvents.NeighborMemoryClueKind kind)
        {
            return kind switch
            {
                PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen => "KEY",
                PlayerFeedbackEvents.NeighborMemoryClueKind.GlassBroken => "GLASS",
                PlayerFeedbackEvents.NeighborMemoryClueKind.ObjectMoved => "OBJECT",
                PlayerFeedbackEvents.NeighborMemoryClueKind.DoorOpened => "DOOR",
                _ => "CLUE"
            };
        }

        private static string BuildTrackingMemoryWarningText(PlayerFeedbackEvents.NeighborMemoryClueKind kind)
        {
            return kind switch
            {
                PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen => "HE IS TRACKING THE STOLEN KEY",
                PlayerFeedbackEvents.NeighborMemoryClueKind.GlassBroken => "HE IS CHECKING THE BROKEN GLASS",
                PlayerFeedbackEvents.NeighborMemoryClueKind.ObjectMoved => "HE IS TRACKING THE MOVED OBJECT",
                PlayerFeedbackEvents.NeighborMemoryClueKind.DoorOpened => "HE IS CHECKING THAT DOOR",
                _ => "HE IS FOLLOWING YOUR TRAIL"
            };
        }

        private static string BuildRememberedMemoryWarningText(PlayerFeedbackEvents.NeighborMemoryClueKind kind)
        {
            return kind switch
            {
                PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen => "HE REMEMBERS THE STOLEN KEY",
                PlayerFeedbackEvents.NeighborMemoryClueKind.GlassBroken => "HE REMEMBERS THE BROKEN GLASS",
                PlayerFeedbackEvents.NeighborMemoryClueKind.ObjectMoved => "HE REMEMBERS THE MOVED OBJECT",
                PlayerFeedbackEvents.NeighborMemoryClueKind.DoorOpened => "HE REMEMBERS THAT DOOR",
                _ => "HE REMEMBERS"
            };
        }

        private float GetDisplayedNoiseLevel()
        {
            float heardPressure = GetDisplayedHeardNoisePressure();
            return Mathf.Clamp01(Mathf.Max(noiseLevel, heardPressure));
        }

        private float GetDisplayedHeardNoisePressure()
        {
            if (Time.unscaledTime >= neighborHeardNoiseUntil || lastNeighborHeardNoise <= 0.05f)
            {
                return 0f;
            }

            float duration = Mathf.Max(0.01f, neighborHeardNoiseDuration);
            float remaining01 = Mathf.Clamp01((neighborHeardNoiseUntil - Time.unscaledTime) / duration);
            return Mathf.Clamp01(lastNeighborHeardNoise * Mathf.SmoothStep(0f, 1f, remaining01));
        }

        private bool HasRecentStealthLoopStatus()
        {
            return Time.unscaledTime < stealthLoopStatusUntil;
        }

        private float GetRecentStealthLoopTension()
        {
            return HasRecentStealthLoopStatus() ? lastStealthLoopTension : 0f;
        }

        private string BuildStealthStatusText(
            PlayerFeedbackEvents.StealthLoopPhase phase,
            float suspicion,
            float noise,
            float tension,
            float memoryPressure,
            bool isCalming)
        {
            string memoryStatusLabel = BuildMemoryStatusLabel(false);
            string trailStatusLabel = BuildMemoryStatusLabel(true);
            if (memoryPressure >= 0.55f
                && phase != PlayerFeedbackEvents.StealthLoopPhase.Chased
                && phase != PlayerFeedbackEvents.StealthLoopPhase.Hiding
                && phase != PlayerFeedbackEvents.StealthLoopPhase.Certain
                && phase != PlayerFeedbackEvents.StealthLoopPhase.Searching
                && phase != PlayerFeedbackEvents.StealthLoopPhase.PostChase)
            {
                return memoryPressure >= 0.75f
                    ? $"SUSPICIOUS / {trailStatusLabel}"
                    : $"SUSPICIOUS / {memoryStatusLabel}";
            }

            if (memoryPressure >= 0.3f && phase == PlayerFeedbackEvents.StealthLoopPhase.Curious)
            {
                return $"CURIOUS / {memoryStatusLabel}";
            }

            switch (phase)
            {
                case PlayerFeedbackEvents.StealthLoopPhase.Chased:
                    return "CHASE / DANGER";
                case PlayerFeedbackEvents.StealthLoopPhase.Hiding:
                    if (isCalming)
                    {
                        return "HIDDEN / BREATH STEADY";
                    }

                    if (hidingState != null && hidingState.IsCompromised)
                    {
                        return "HIDDEN / FOUND";
                    }

                    if (hidingState != null && hidingState.IsDangerouslyExposed)
                    {
                        return "HIDDEN / EXPOSED";
                    }

                    return tension >= 0.7f ? "HIDDEN / BREATH HIGH" : "HIDDEN / STEADY";
                case PlayerFeedbackEvents.StealthLoopPhase.PostChase:
                    if (isCalming)
                    {
                        return memoryPressure >= 0.55f
                            ? $"RECOVERY / {trailStatusLabel}"
                            : "RECOVERY / STAY QUIET";
                    }

                    if (IsTrailInvestigationActive())
                    {
                        return TryGetDisplayedMemoryClueKind(out _)
                            ? $"RECOVERY / {trailStatusLabel}"
                            : "RECOVERY / TRAIL SEARCH";
                    }

                    if (memoryPressure >= 0.55f)
                    {
                        return $"RECOVERY / {trailStatusLabel}";
                    }

                    return tension >= 0.45f ? "RECOVERY / SEARCHING" : "RECOVERY / QUIET DOWN";
                case PlayerFeedbackEvents.StealthLoopPhase.Searching:
                    if (IsTrailInvestigationActive() || memoryPressure >= 0.55f)
                    {
                        return $"SEARCHING / {trailStatusLabel}";
                    }

                    return noise >= 0.35f ? "SEARCHING / NOISE TRACE" : "SEARCHING";
                case PlayerFeedbackEvents.StealthLoopPhase.Certain:
                    return "CERTAIN / ALMOST SEEN";
                case PlayerFeedbackEvents.StealthLoopPhase.Suspicious:
                    return suspicion >= 0.75f ? "SUSPICIOUS / ALMOST SEEN" : "SUSPICIOUS";
                case PlayerFeedbackEvents.StealthLoopPhase.Curious:
                    return noise >= 0.35f ? "CURIOUS / NOISE HEARD" : "CURIOUS";
                default:
                    return noise >= 0.35f ? "QUIET / NOISE FADING" : "QUIET";
            }
        }

        private static Color GetStealthStatusColor(
            PlayerFeedbackEvents.StealthLoopPhase phase,
            float suspicion,
            float noise,
            float tension,
            float memoryPressure,
            bool isCalming)
        {
            float intensity = Mathf.Max(suspicion, noise, tension, memoryPressure);
            return phase switch
            {
                PlayerFeedbackEvents.StealthLoopPhase.Chased => new Color(1f, 0.12f, 0.06f, 1f),
                PlayerFeedbackEvents.StealthLoopPhase.Hiding => Color.Lerp(
                    isCalming ? new Color(0.54f, 0.95f, 1f, 0.94f) : new Color(0.62f, 0.9f, 1f, 0.9f),
                    isCalming ? new Color(0.9f, 0.9f, 0.66f, 0.96f) : new Color(1f, 0.58f, 0.14f, 1f),
                    tension),
                PlayerFeedbackEvents.StealthLoopPhase.PostChase => Color.Lerp(
                    isCalming ? new Color(0.6f, 0.86f, 1f, 0.92f) : new Color(1f, 0.76f, 0.28f, 0.95f),
                    isCalming ? new Color(1f, 0.72f, 0.26f, 0.98f) : new Color(1f, 0.38f, 0.1f, 1f),
                    tension),
                PlayerFeedbackEvents.StealthLoopPhase.Searching => new Color(1f, 0.66f, 0.16f, 0.98f),
                PlayerFeedbackEvents.StealthLoopPhase.Certain => Color.Lerp(
                    new Color(1f, 0.42f, 0.12f, 0.98f),
                    new Color(1f, 0.08f, 0.04f, 1f),
                    Mathf.Max(suspicion, memoryPressure)),
                PlayerFeedbackEvents.StealthLoopPhase.Suspicious => Color.Lerp(
                    new Color(1f, 0.58f, 0.16f, 0.96f),
                    new Color(1f, 0.18f, 0.08f, 1f),
                    Mathf.Max(suspicion, memoryPressure)),
                PlayerFeedbackEvents.StealthLoopPhase.Curious => Color.Lerp(
                    new Color(0.78f, 0.86f, 0.96f, 0.9f),
                    new Color(1f, 0.72f, 0.18f, 0.98f),
                    Mathf.Max(noise, suspicion, memoryPressure)),
                _ => Color.Lerp(
                    new Color(0.64f, 0.76f, 0.9f, 0.72f),
                    new Color(1f, 0.72f, 0.18f, 0.9f),
                    intensity)
            };
        }
    }
}
