using Neighbor.Main.Features.Interaction;
using Neighbor.Main.Features.Neighbor;
using UnityEngine;

namespace Neighbor.Main.Features.Player
{
    public sealed class PlayerHidingState : MonoBehaviour
    {
        [Header("Breath Tension")]
        [SerializeField, Min(0f)] private float hiddenBreathGraceTime = 1.5f;
        [SerializeField, Min(0f)] private float breathTensionBuildRate = 0.035f;
        [SerializeField, Min(0f)] private float breathTensionRecoveryRate = 0.3f;
        [SerializeField, Min(0f)] private float hiddenBreathRecoveryDelay = 1.2f;
        [SerializeField, Min(0f)] private float calmHiddenBreathRecoveryRate = 0.16f;
        [SerializeField, Range(0f, 1f)] private float recoveredBreathTensionThreshold = 0.22f;
        [SerializeField, Range(0f, 1f)] private float inspectionTensionIncrease = 0.42f;
        [SerializeField, Range(0f, 1f)] private float compromisedTensionThreshold = 0.94f;
        [SerializeField, Range(0f, 1f)] private float exposedVisibilityThreshold = 0.78f;
        [SerializeField, Min(0f)] private float hidingDangerNeighborRadius = 9f;
        [SerializeField, Min(0.05f)] private float neighborSearchInterval = 0.5f;

        [Header("Breath Noise")]
        [SerializeField] private bool emitBreathNoise = true;
        [SerializeField, Range(0f, 1f)] private float breathNoiseThreshold = 0.82f;
        [SerializeField, Min(0f)] private float breathNoiseCooldown = 1.4f;
        [SerializeField, Min(0f)] private float breathNoiseRadius = 4.5f;
        [SerializeField, Range(0f, 1f)] private float breathNoiseLoudness = 0.18f;
        [SerializeField, Min(0f)] private float breathNoiseLifetime = 0.35f;

        [Header("Exit Risk")]
        [SerializeField] private bool emitNoisyExit = true;
        [SerializeField, Range(0f, 1f)] private float noisyExitTensionThreshold = 0.45f;
        [SerializeField, Range(0f, 1f)] private float inspectedExitNoiseFloor = 0.38f;
        [SerializeField, Range(0f, 1f)] private float compromisedExitNoiseFloor = 0.62f;
        [SerializeField, Min(0f)] private float noisyExitRadius = 5.5f;
        [SerializeField, Range(0f, 1f)] private float noisyExitLoudness = 0.28f;
        [SerializeField, Min(0f)] private float noisyExitLifetime = 0.35f;

        [Header("Peek Risk")]
        [SerializeField, Min(0f)] private float peekExposureRecoveryRate = 2.2f;
        [SerializeField, Min(0f)] private float peekTensionBuildRate = 0.08f;

        private float hiddenSinceTime;
        private float lastInspectionTime = float.NegativeInfinity;
        private float nextBreathNoiseTime = float.NegativeInfinity;
        private float nextNeighborSearchTime;
        private NeighborBrain dangerNeighbor;
        private bool waitingForBreathRecoveryFeedback;

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

            UpdateHiddenBreathTension(Time.deltaTime);
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
                float exitNoise = wasHidden ? CalculateNoisyExitLoudness(wasCompromised) : 0f;
                IsHidden = false;
                CurrentHideSpot = null;
                IsCompromised = false;
                PeekExposure01 = 0f;
                if (wasHidden)
                {
                    TryEmitNoisyExit(exitNoise);
                    ReportHidingFeedback(
                        previousHideSpot,
                        PlayerFeedbackEvents.HidingFeedbackKind.Exited,
                        false,
                        wasCompromised,
                        exitNoise);
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
                waitingForBreathRecoveryFeedback = false;
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
            waitingForBreathRecoveryFeedback = true;
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
            if (BreathTension01 > recoveredBreathTensionThreshold)
            {
                waitingForBreathRecoveryFeedback = true;
            }

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
            ReportHidingFeedback(
                CurrentHideSpot,
                PlayerFeedbackEvents.HidingFeedbackKind.BreathNoisy,
                true,
                IsCompromised,
                loudness);
            noiseEvent.Initialize(
                transform.position,
                breathNoiseRadius,
                loudness,
                gameObject,
                breathNoiseLifetime,
                BreathTension01,
                gameObject);
        }

        private float CalculateNoisyExitLoudness(bool wasCompromised)
        {
            if (!emitNoisyExit
                || noisyExitRadius <= 0f
                || noisyExitLoudness <= 0f)
            {
                return 0f;
            }

            float exitPressure = BreathTension01;
            if (WasInspectedRecently)
            {
                exitPressure = Mathf.Max(exitPressure, inspectedExitNoiseFloor);
            }

            if (wasCompromised || IsDangerouslyExposed)
            {
                exitPressure = Mathf.Max(exitPressure, compromisedExitNoiseFloor);
            }

            if (exitPressure < noisyExitTensionThreshold)
            {
                return 0f;
            }

            return Mathf.Clamp01(Mathf.Lerp(noisyExitLoudness * 0.6f, noisyExitLoudness, exitPressure));
        }

        private void TryEmitNoisyExit(float loudness)
        {
            if (loudness <= 0f)
            {
                return;
            }

            GameObject noiseObject = new("HiddenExitNoiseEvent");
            noiseObject.transform.position = transform.position;
            noiseObject.AddComponent<SphereCollider>();
            NoiseEvent noiseEvent = noiseObject.AddComponent<NoiseEvent>();
            noiseEvent.Initialize(
                transform.position,
                noisyExitRadius,
                loudness,
                gameObject,
                noisyExitLifetime,
                Mathf.Max(loudness, BreathTension01),
                gameObject);
        }

        private void UpdateHiddenBreathTension(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            float danger01 = GetHidingDanger01();
            if (danger01 > 0.05f && HiddenDuration > hiddenBreathGraceTime)
            {
                float dangerBuildMultiplier = Mathf.Lerp(0.35f, 1f, danger01);
                BreathTension01 = Mathf.Clamp01(BreathTension01 + breathTensionBuildRate * dangerBuildMultiplier * deltaTime);
                if (BreathTension01 > recoveredBreathTensionThreshold)
                {
                    waitingForBreathRecoveryFeedback = true;
                }

                if (BreathTension01 >= compromisedTensionThreshold)
                {
                    IsCompromised = true;
                }

                return;
            }

            if (HiddenDuration > hiddenBreathRecoveryDelay)
            {
                float previousTension = BreathTension01;
                BreathTension01 = Mathf.MoveTowards(
                    BreathTension01,
                    0f,
                    calmHiddenBreathRecoveryRate * deltaTime);
                if (waitingForBreathRecoveryFeedback
                    && previousTension > recoveredBreathTensionThreshold
                    && BreathTension01 <= recoveredBreathTensionThreshold
                    && !IsCompromised)
                {
                    waitingForBreathRecoveryFeedback = false;
                    ReportHidingFeedback(
                        CurrentHideSpot,
                        PlayerFeedbackEvents.HidingFeedbackKind.Recovered,
                        true,
                        false);
                }
            }
        }

        private float GetHidingDanger01()
        {
            ResolveDangerNeighbor();
            if (dangerNeighbor == null)
            {
                return 0f;
            }

            float stateDanger = dangerNeighbor.CurrentState switch
            {
                NeighborBrain.BehaviorState.Chase => 1f,
                NeighborBrain.BehaviorState.Catching => 1f,
                NeighborBrain.BehaviorState.HuntMode => 0.9f,
                NeighborBrain.BehaviorState.Investigate => 0.7f,
                NeighborBrain.BehaviorState.DoorSecurityCheck => 0.45f,
                _ => dangerNeighbor.CurrentSuspicionLevel >= NeighborBrain.SuspicionLevel.Suspicious ? 0.25f : 0f
            };

            if (dangerNeighbor.IsPostChaseTensionActive)
            {
                stateDanger = Mathf.Max(stateDanger, dangerNeighbor.PostChaseTension01 * 0.55f);
            }

            if (stateDanger <= 0f)
            {
                return 0f;
            }

            float dangerRadius = Mathf.Max(0.01f, hidingDangerNeighborRadius);
            float distance = Vector3.Distance(transform.position, dangerNeighbor.transform.position);
            if (distance > dangerRadius)
            {
                return 0f;
            }

            float proximity = Mathf.Clamp01(1f - distance / dangerRadius);
            return Mathf.Clamp01(stateDanger * Mathf.Lerp(0.35f, 1f, proximity));
        }

        private void ResolveDangerNeighbor()
        {
            if (dangerNeighbor != null)
            {
                return;
            }

            if (Time.time < nextNeighborSearchTime)
            {
                return;
            }

            dangerNeighbor = FindAnyObjectByType<NeighborBrain>();
            nextNeighborSearchTime = Time.time + neighborSearchInterval;
        }

        private void ReportHidingFeedback(
            ClosetHideSpot hideSpot,
            PlayerFeedbackEvents.HidingFeedbackKind kind,
            bool isHidden,
            bool isCompromised,
            float noise = 0f)
        {
            string spotName = hideSpot != null ? hideSpot.DisplayName : "hiding spot";
            float stealthTension = isHidden && isCompromised ? 1f : BreathTension01;
            PlayerFeedbackEvents.ReportHiding(spotName, kind, BreathTension01, isHidden, isCompromised);
            PlayerFeedbackEvents.ReportStealthLoop(
                isHidden
                    ? PlayerFeedbackEvents.StealthLoopPhase.Hiding
                    : PlayerFeedbackEvents.StealthLoopPhase.Quiet,
                isHidden && isCompromised ? 1f : BreathTension01,
                noise,
                stealthTension,
                GetHidingStealthLoopMessage(kind, isHidden, isCompromised, noise),
                kind == PlayerFeedbackEvents.HidingFeedbackKind.Recovered);
        }

        private static string GetHidingStealthLoopMessage(
            PlayerFeedbackEvents.HidingFeedbackKind kind,
            bool isHidden,
            bool isCompromised,
            float noise)
        {
            return kind switch
            {
                PlayerFeedbackEvents.HidingFeedbackKind.Found => "He found your hiding spot.",
                PlayerFeedbackEvents.HidingFeedbackKind.Inspected => "Stay still. He is checking the hiding spot.",
                PlayerFeedbackEvents.HidingFeedbackKind.BreathNoisy => "Your breathing is too loud.",
                PlayerFeedbackEvents.HidingFeedbackKind.Recovered => "Breathing under control.",
                PlayerFeedbackEvents.HidingFeedbackKind.Exited => noise > 0.01f
                    ? "You made noise leaving cover."
                    : "Back in the open.",
                _ => isHidden && isCompromised
                    ? "He found your hiding spot."
                    : isHidden ? "Stay still. Let the tension drop." : "Back in the open."
            };
        }
    }
}
