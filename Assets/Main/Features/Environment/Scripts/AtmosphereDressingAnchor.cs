using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Environment
{
    [ExecuteAlways]
    public sealed class AtmosphereDressingAnchor : MonoBehaviour
    {
        public enum DressingKind
        {
            DirtyDecal,
            PropDressing
        }

        [SerializeField] private DressingKind kind = DressingKind.PropDressing;
        [SerializeField, Range(0f, 1f)] private float intensity = 0.6f;
        [Header("Stealth Response")]
        [SerializeField] private bool respondToStealthLoop = true;
        [SerializeField, Range(0f, 1f)] private float dangerVisibilityBoost = 0.24f;
        [SerializeField, Range(0f, 1f)] private float dangerDarkening = 0.28f;
        [SerializeField, Range(0f, 1f)] private float calmPostChaseDressingPressure = 0.14f;
        [SerializeField, Range(0f, 1f)] private float calmHidingDressingPressure = 0.08f;
        [SerializeField, Min(0f)] private float dangerHoldDuration = 2.6f;
        [SerializeField, Min(0f)] private float dangerFadeSpeed = 1.8f;
        [SerializeField, Range(0f, 0.08f)] private float propScalePulse = 0.025f;
        [Header("Memory Response")]
        [SerializeField] private bool respondToNeighborMemory = true;
        [SerializeField, Range(0f, 1f)] private float memoryDressingPressure = 0.38f;
        [SerializeField, Range(0f, 1f)] private float stackedMemoryDressingBoost = 0.14f;
        [SerializeField, Min(0f)] private float memoryHoldDuration = 3.8f;
        [SerializeField, Min(1f)] private float maximumMemoryHoldMultiplier = 1.5f;
        [Header("Investigation Response")]
        [SerializeField] private bool respondToNeighborInvestigation = true;
        [SerializeField, Range(0f, 1f)] private float trailInvestigationDressingPressure = 0.6f;
        [SerializeField, Range(0f, 1f)] private float searchingInvestigationDressingPressure = 0.46f;
        [SerializeField, Range(0f, 1f)] private float resolvedTrailDressingPressure = 0.12f;
        [SerializeField, Range(0f, 1f)] private float resolvedInvestigationDressingPressure = 0.08f;
        [SerializeField, Min(0f)] private float investigationHoldDuration = 3f;
        [Header("Noise Response")]
        [SerializeField] private bool respondToNoiseFeedback = true;
        [SerializeField, Range(0f, 1f)] private float heardNoiseDressingPressure = 0.44f;
        [SerializeField, Range(0f, 1f)] private float heardNoiseListenerDressingBoost = 0.08f;
        [SerializeField, Min(0f)] private float heardNoiseHoldDuration = 2.4f;

        private Renderer targetRenderer;
        private MaterialPropertyBlock propertyBlock;
        private Color baseColor = Color.white;
        private Vector3 baseScale = Vector3.one;
        private float currentStealthPressure;
        private float targetStealthPressure;
        private float stealthPressureHoldUntilTime;

        public DressingKind Kind => kind;
        public float Intensity => intensity;
        public float CurrentStealthPressure => currentStealthPressure;
        public float EffectiveIntensity => Mathf.Clamp01(intensity + dangerVisibilityBoost * currentStealthPressure);

        private void Awake()
        {
            CaptureBaseVisualState();
            ApplyDressingVisuals();
        }

        private void OnEnable()
        {
            CaptureBaseVisualState();
            PlayerFeedbackEvents.StealthLoopChanged -= HandleStealthLoopChanged;
            PlayerFeedbackEvents.StealthLoopChanged += HandleStealthLoopChanged;
            PlayerFeedbackEvents.NeighborMemoryChanged -= HandleNeighborMemoryChanged;
            PlayerFeedbackEvents.NeighborMemoryChanged += HandleNeighborMemoryChanged;
            PlayerFeedbackEvents.NeighborInvestigationChanged -= HandleNeighborInvestigationChanged;
            PlayerFeedbackEvents.NeighborInvestigationChanged += HandleNeighborInvestigationChanged;
            PlayerFeedbackEvents.NoiseEmitted -= HandleNoiseEmitted;
            PlayerFeedbackEvents.NoiseEmitted += HandleNoiseEmitted;
            ApplyDressingVisuals();
        }

        private void OnDisable()
        {
            PlayerFeedbackEvents.StealthLoopChanged -= HandleStealthLoopChanged;
            PlayerFeedbackEvents.NeighborMemoryChanged -= HandleNeighborMemoryChanged;
            PlayerFeedbackEvents.NeighborInvestigationChanged -= HandleNeighborInvestigationChanged;
            PlayerFeedbackEvents.NoiseEmitted -= HandleNoiseEmitted;
            RestoreDressingVisuals();
        }

        private void Update()
        {
            UpdateStealthDressing(Time.unscaledDeltaTime);
        }

        public void Configure(DressingKind anchorKind, float anchorIntensity)
        {
            kind = anchorKind;
            intensity = Mathf.Clamp01(anchorIntensity);
            CaptureBaseVisualState();
            ApplyDressingVisuals();
        }

        private void CaptureBaseVisualState()
        {
            targetRenderer = targetRenderer != null ? targetRenderer : GetComponent<Renderer>();
            baseScale = transform.localScale;
            if (targetRenderer == null || targetRenderer.sharedMaterial == null)
            {
                return;
            }

            Material material = targetRenderer.sharedMaterial;
            if (material.HasProperty("_BaseColor"))
            {
                baseColor = material.GetColor("_BaseColor");
            }
            else if (material.HasProperty("_Color"))
            {
                baseColor = material.GetColor("_Color");
            }
        }

        private void HandleStealthLoopChanged(PlayerFeedbackEvents.StealthLoopFeedback feedback)
        {
            if (!respondToStealthLoop)
            {
                return;
            }

            float pressure = GetStealthPressure(feedback);
            if (ShouldSettleStealthPressure(feedback))
            {
                SettleStealthPressure(pressure, dangerHoldDuration);
                return;
            }

            RaiseStealthPressure(pressure, dangerHoldDuration);
        }

        private void HandleNeighborMemoryChanged(PlayerFeedbackEvents.NeighborMemoryFeedback feedback)
        {
            if (!respondToNeighborMemory)
            {
                return;
            }

            RaiseStealthPressure(GetMemoryPressure(feedback), GetMemoryHoldDuration(feedback));
        }

        private void HandleNeighborInvestigationChanged(PlayerFeedbackEvents.NeighborInvestigationFeedback feedback)
        {
            if (!respondToNeighborInvestigation)
            {
                return;
            }

            float pressure = GetInvestigationPressure(feedback);
            if (pressure > 0f)
            {
                if (IsResolvingInvestigation(feedback.Kind))
                {
                    SettleStealthPressure(pressure, investigationHoldDuration);
                    return;
                }

                RaiseStealthPressure(pressure, investigationHoldDuration);
            }
        }

        private void HandleNoiseEmitted(PlayerFeedbackEvents.NoiseFeedback feedback)
        {
            if (!respondToNoiseFeedback || !feedback.HeardByNeighbor)
            {
                return;
            }

            RaiseStealthPressure(GetNoisePressure(feedback), heardNoiseHoldDuration);
        }

        private void RaiseStealthPressure(float pressure, float holdDuration)
        {
            SetStealthPressure(pressure, holdDuration, false);
        }

        private void SettleStealthPressure(float pressure, float holdDuration)
        {
            SetStealthPressure(pressure, holdDuration, true);
        }

        private void SetStealthPressure(float pressure, float holdDuration, bool allowLowerPressure)
        {
            float nextPressure = Mathf.Clamp01(pressure);
            if (!allowLowerPressure
                && Time.unscaledTime <= stealthPressureHoldUntilTime
                && nextPressure < targetStealthPressure)
            {
                return;
            }

            targetStealthPressure = nextPressure;
            stealthPressureHoldUntilTime = targetStealthPressure <= 0f
                ? 0f
                : Time.unscaledTime + holdDuration;

            if (targetStealthPressure > currentStealthPressure)
            {
                currentStealthPressure = targetStealthPressure;
            }

            ApplyDressingVisuals();
        }

        private static bool ShouldSettleStealthPressure(PlayerFeedbackEvents.StealthLoopFeedback feedback)
        {
            return feedback.IsCalming || feedback.Phase == PlayerFeedbackEvents.StealthLoopPhase.Quiet;
        }

        private static bool IsResolvingInvestigation(PlayerFeedbackEvents.NeighborInvestigationFeedbackKind kind)
        {
            return kind == PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Returning
                || kind == PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Abandoned;
        }

        private void UpdateStealthDressing(float deltaTime)
        {
            if (!respondToStealthLoop
                && !respondToNeighborMemory
                && !respondToNeighborInvestigation
                && !respondToNoiseFeedback)
            {
                targetStealthPressure = 0f;
            }

            float desiredPressure = Time.unscaledTime <= stealthPressureHoldUntilTime
                ? targetStealthPressure
                : 0f;
            float previousPressure = currentStealthPressure;
            currentStealthPressure = Mathf.MoveTowards(
                currentStealthPressure,
                desiredPressure,
                dangerFadeSpeed * Mathf.Max(0f, deltaTime));

            if (!Mathf.Approximately(previousPressure, currentStealthPressure))
            {
                ApplyDressingVisuals();
            }
        }

        private void ApplyDressingVisuals()
        {
            ApplyRendererProperties();
            ApplyPropScale();
        }

        private void ApplyRendererProperties()
        {
            if (targetRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            Color color = GetEffectiveColor();
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private void ApplyPropScale()
        {
            if (kind != DressingKind.PropDressing)
            {
                return;
            }

            float scalePulse = 1f + propScalePulse * currentStealthPressure;
            transform.localScale = baseScale * scalePulse;
        }

        private void RestoreDressingVisuals()
        {
            if (targetRenderer != null)
            {
                targetRenderer.SetPropertyBlock(null);
            }

            if (kind == DressingKind.PropDressing)
            {
                transform.localScale = baseScale;
            }
        }

        private Color GetEffectiveColor()
        {
            float effectiveIntensity = EffectiveIntensity;
            Color color = baseColor;
            if (kind == DressingKind.DirtyDecal)
            {
                color.r *= Mathf.Lerp(1f, 1f - dangerDarkening, currentStealthPressure);
                color.g *= Mathf.Lerp(1f, 1f - dangerDarkening, currentStealthPressure);
                color.b *= Mathf.Lerp(1f, 1f - dangerDarkening, currentStealthPressure);
                color.a = Mathf.Clamp01(Mathf.Max(color.a, effectiveIntensity));
                return color;
            }

            float propDarkening = dangerDarkening * 0.45f * currentStealthPressure;
            color.r *= 1f - propDarkening;
            color.g *= 1f - propDarkening;
            color.b *= 1f - propDarkening;
            color.a = Mathf.Clamp01(Mathf.Max(color.a, 0.85f));
            return color;
        }

        private float GetStealthPressure(PlayerFeedbackEvents.StealthLoopFeedback feedback)
        {
            float pressure = Mathf.Max(feedback.Suspicion, feedback.Noise, feedback.Tension);
            return feedback.Phase switch
            {
                PlayerFeedbackEvents.StealthLoopPhase.Chased => 1f,
                PlayerFeedbackEvents.StealthLoopPhase.PostChase => feedback.IsCalming
                    ? Mathf.Max(calmPostChaseDressingPressure, feedback.Tension)
                    : Mathf.Max(0.62f, pressure),
                PlayerFeedbackEvents.StealthLoopPhase.Searching => Mathf.Max(0.52f, pressure),
                PlayerFeedbackEvents.StealthLoopPhase.Certain => Mathf.Max(0.74f, pressure),
                PlayerFeedbackEvents.StealthLoopPhase.Suspicious => Mathf.Max(0.42f, pressure),
                PlayerFeedbackEvents.StealthLoopPhase.Hiding => feedback.IsCalming
                    ? Mathf.Max(calmHidingDressingPressure, feedback.Tension)
                    : Mathf.Max(0.28f, feedback.Tension),
                PlayerFeedbackEvents.StealthLoopPhase.Curious => Mathf.Max(0.18f, pressure * 0.65f),
                _ => 0f
            };
        }

        private float GetMemoryPressure(PlayerFeedbackEvents.NeighborMemoryFeedback feedback)
        {
            float stackPressure = Mathf.Clamp01(feedback.TotalMemoryCount / 4f) * stackedMemoryDressingBoost;
            return Mathf.Clamp01(Mathf.Max(memoryDressingPressure, feedback.Urgency) + stackPressure);
        }

        private float GetMemoryHoldDuration(PlayerFeedbackEvents.NeighborMemoryFeedback feedback)
        {
            float stackPressure = Mathf.Clamp01(Mathf.Max(0, feedback.TotalMemoryCount - 1) / 3f);
            float commitment = Mathf.Max(GetMemoryClueSeverity(feedback.Kind), stackPressure);
            return memoryHoldDuration * Mathf.Lerp(1f, maximumMemoryHoldMultiplier, commitment);
        }

        private static float GetMemoryClueSeverity(PlayerFeedbackEvents.NeighborMemoryClueKind kind)
        {
            return kind switch
            {
                PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen => 1f,
                PlayerFeedbackEvents.NeighborMemoryClueKind.GlassBroken => 0.78f,
                PlayerFeedbackEvents.NeighborMemoryClueKind.ObjectMoved => 0.45f,
                PlayerFeedbackEvents.NeighborMemoryClueKind.DoorOpened => 0.16f,
                _ => 0f
            };
        }

        private float GetInvestigationPressure(PlayerFeedbackEvents.NeighborInvestigationFeedback feedback)
        {
            float feedbackPressure = Mathf.Max(feedback.Suspicion, feedback.Urgency);
            return feedback.Kind switch
            {
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.CheckingHideSpot =>
                    Mathf.Max(0.88f, feedbackPressure),
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.FollowingTrail =>
                    Mathf.Max(trailInvestigationDressingPressure, feedbackPressure),
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Searching =>
                    feedback.IsTrailRelated
                        ? Mathf.Max(trailInvestigationDressingPressure, feedbackPressure)
                        : Mathf.Max(searchingInvestigationDressingPressure, feedbackPressure * 0.8f),
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Started =>
                    Mathf.Max(0.34f, feedbackPressure * 0.7f),
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Returning when feedback.IsTrailRelated =>
                    resolvedTrailDressingPressure,
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Abandoned when feedback.IsTrailRelated =>
                    resolvedTrailDressingPressure,
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Returning =>
                    resolvedInvestigationDressingPressure,
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Abandoned =>
                    resolvedInvestigationDressingPressure,
                _ => 0f
            };
        }

        private float GetNoisePressure(PlayerFeedbackEvents.NoiseFeedback feedback)
        {
            float listenerPressure = Mathf.Clamp01(Mathf.Max(0, feedback.NeighborListenerCount - 1) / 2f)
                * heardNoiseListenerDressingBoost;
            return Mathf.Clamp01(Mathf.Max(heardNoiseDressingPressure, Mathf.Max(feedback.Loudness, feedback.Urgency))
                + listenerPressure);
        }
    }
}
