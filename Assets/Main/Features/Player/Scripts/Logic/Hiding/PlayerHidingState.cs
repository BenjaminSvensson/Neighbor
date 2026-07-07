using Neighbor.Main.Features.Interaction;
using UnityEngine;

namespace Neighbor.Main.Features.Player
{
    public sealed class PlayerHidingState : MonoBehaviour
    {
        [Header("Breath Tension")]
        [SerializeField, Min(0f)] private float hiddenBreathGraceTime = 1.5f;
        [SerializeField, Min(0f)] private float breathTensionBuildRate = 0.035f;
        [SerializeField, Min(0f)] private float breathTensionRecoveryRate = 0.3f;
        [SerializeField, Range(0f, 1f)] private float inspectionTensionIncrease = 0.42f;
        [SerializeField, Range(0f, 1f)] private float compromisedTensionThreshold = 0.94f;
        [SerializeField, Range(0f, 1f)] private float exposedVisibilityThreshold = 0.78f;

        [Header("Breath Noise")]
        [SerializeField] private bool emitBreathNoise = true;
        [SerializeField, Range(0f, 1f)] private float breathNoiseThreshold = 0.82f;
        [SerializeField, Min(0f)] private float breathNoiseCooldown = 1.4f;
        [SerializeField, Min(0f)] private float breathNoiseRadius = 4.5f;
        [SerializeField, Range(0f, 1f)] private float breathNoiseLoudness = 0.18f;
        [SerializeField, Min(0f)] private float breathNoiseLifetime = 0.35f;

        [Header("Peek Risk")]
        [SerializeField, Min(0f)] private float peekExposureRecoveryRate = 2.2f;
        [SerializeField, Min(0f)] private float peekTensionBuildRate = 0.08f;

        private float hiddenSinceTime;
        private float lastInspectionTime = float.NegativeInfinity;
        private float nextBreathNoiseTime = float.NegativeInfinity;

        public bool IsHidden { get; private set; }
        public ClosetHideSpot CurrentHideSpot { get; private set; }
        public float BreathTension01 { get; private set; }
        public float PeekExposure01 { get; private set; }
        public bool IsCompromised { get; private set; }
        public bool IsDangerouslyExposed => IsHidden && PeekExposure01 >= exposedVisibilityThreshold;
        public bool IsConcealedFromVision => IsHidden && !IsCompromised && !IsDangerouslyExposed;
        public bool WasInspectedRecently => Time.time - lastInspectionTime <= 2.5f;
        public float HiddenDuration => IsHidden ? Mathf.Max(0f, Time.time - hiddenSinceTime) : 0f;

        private void Update()
        {
            if (!IsHidden)
            {
                BreathTension01 = Mathf.MoveTowards(
                    BreathTension01,
                    0f,
                    breathTensionRecoveryRate * Time.deltaTime);
                PeekExposure01 = Mathf.MoveTowards(
                    PeekExposure01,
                    0f,
                    peekExposureRecoveryRate * Time.deltaTime);
                return;
            }

            PeekExposure01 = Mathf.MoveTowards(
                PeekExposure01,
                0f,
                peekExposureRecoveryRate * Time.deltaTime);

            if (HiddenDuration > hiddenBreathGraceTime)
            {
                BreathTension01 = Mathf.Clamp01(BreathTension01 + breathTensionBuildRate * Time.deltaTime);
            }

            TryEmitBreathNoise();
        }

        public void SetHidden(bool hidden)
        {
            SetHidden(hidden, CurrentHideSpot);
        }

        public void SetHidden(bool hidden, ClosetHideSpot hideSpot)
        {
            if (!hidden)
            {
                ClosetHideSpot previousHideSpot = CurrentHideSpot;
                bool wasHidden = IsHidden;
                bool wasCompromised = IsCompromised;
                IsHidden = false;
                CurrentHideSpot = null;
                IsCompromised = false;
                PeekExposure01 = 0f;
                if (wasHidden)
                {
                    ReportHidingFeedback(previousHideSpot, PlayerFeedbackEvents.HidingFeedbackKind.Exited, false, wasCompromised);
                }

                return;
            }

            bool enteredNewHideState = !IsHidden;
            if (!IsHidden)
            {
                hiddenSinceTime = Time.time;
                BreathTension01 = 0f;
                PeekExposure01 = 0f;
                lastInspectionTime = float.NegativeInfinity;
                nextBreathNoiseTime = Time.time + breathNoiseCooldown;
                IsCompromised = false;
            }

            IsHidden = true;
            CurrentHideSpot = hideSpot != null ? hideSpot : CurrentHideSpot;
            if (enteredNewHideState)
            {
                ReportHidingFeedback(CurrentHideSpot, PlayerFeedbackEvents.HidingFeedbackKind.Entered, true, IsCompromised);
            }
        }

        public void RegisterNeighborInspection(bool foundPlayer)
        {
            lastInspectionTime = Time.time;
            BreathTension01 = Mathf.Clamp01(BreathTension01 + inspectionTensionIncrease);
            if (foundPlayer || BreathTension01 >= compromisedTensionThreshold)
            {
                IsCompromised = true;
            }

            ReportHidingFeedback(
                CurrentHideSpot,
                foundPlayer || IsCompromised
                    ? PlayerFeedbackEvents.HidingFeedbackKind.Found
                    : PlayerFeedbackEvents.HidingFeedbackKind.Inspected,
                IsHidden,
                IsCompromised);
        }

        public void AddBreathTension(float amount)
        {
            BreathTension01 = Mathf.Clamp01(BreathTension01 + Mathf.Max(0f, amount));
            if (BreathTension01 >= compromisedTensionThreshold)
            {
                IsCompromised = true;
            }
        }

        public void SetPeekExposure(float exposure, float deltaTime)
        {
            PeekExposure01 = Mathf.Clamp01(exposure);
            if (IsHidden && PeekExposure01 > 0f && deltaTime > 0f)
            {
                AddBreathTension(PeekExposure01 * peekTensionBuildRate * deltaTime);
            }
        }

        private void TryEmitBreathNoise()
        {
            if (!emitBreathNoise
                || BreathTension01 < breathNoiseThreshold
                || Time.time < nextBreathNoiseTime
                || breathNoiseRadius <= 0f
                || breathNoiseLoudness <= 0f)
            {
                return;
            }

            nextBreathNoiseTime = Time.time + breathNoiseCooldown;
            GameObject noiseObject = new("HiddenBreathNoiseEvent");
            noiseObject.transform.position = transform.position;
            noiseObject.AddComponent<SphereCollider>();
            NoiseEvent noiseEvent = noiseObject.AddComponent<NoiseEvent>();
            float loudness = Mathf.Lerp(breathNoiseLoudness * 0.55f, breathNoiseLoudness, BreathTension01);
            noiseEvent.Initialize(
                transform.position,
                breathNoiseRadius,
                loudness,
                gameObject,
                breathNoiseLifetime,
                BreathTension01,
                gameObject);
        }

        private void ReportHidingFeedback(
            ClosetHideSpot hideSpot,
            PlayerFeedbackEvents.HidingFeedbackKind kind,
            bool isHidden,
            bool isCompromised)
        {
            string spotName = hideSpot != null ? hideSpot.DisplayName : "hiding spot";
            PlayerFeedbackEvents.ReportHiding(spotName, kind, BreathTension01, isHidden, isCompromised);
        }
    }
}
