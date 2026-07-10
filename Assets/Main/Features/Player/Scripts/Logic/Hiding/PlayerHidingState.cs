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

        [Header("Breath Control")]
        [SerializeField] private bool allowBreathControl = true;
        [SerializeField] private PlayerInputBindingAction breathHoldInputAction = PlayerInputBindingAction.Run;
        [SerializeField, Min(0.1f)] private float breathHoldDuration = 3.8f;
        [SerializeField, Range(0f, 1f)] private float breathHoldMinimumCapacityToStart = 0.12f;
        [SerializeField, Min(0f)] private float breathHoldRecoveryDelay = 0.85f;
        [SerializeField, Min(0f)] private float breathHoldRecoveryRate = 0.28f;
        [SerializeField, Min(0f)] private float breathHoldTensionReliefRate = 0.18f;
        [SerializeField, Range(0f, 1f)] private float breathHoldDangerBuildMultiplier = 0.08f;
        [SerializeField, Range(0f, 1f)] private float breathHoldExhaustionTensionIncrease = 0.46f;
        [SerializeField, Range(0f, 1f)] private float breathHoldExhaustionRecoveryThreshold = 0.35f;
        [SerializeField, Range(0f, 1f)] private float breathHoldExhaustionNoiseLoudness = 0.32f;
        [SerializeField, Min(0f)] private float breathHoldExhaustionNoiseRadius = 6f;
        [SerializeField, Min(0f)] private float breathHoldExhaustionNoiseLifetime = 0.45f;

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
        private float breathHoldRecoveryBlockedUntilTime;
        private NeighborBrain dangerNeighbor;
        private bool waitingForBreathRecoveryFeedback;

        public bool IsHidden { get; private set; }
        public ClosetHideSpot CurrentHideSpot { get; private set; }
        public float BreathTension01 { get; private set; }
        public float BreathHoldCapacity01 { get; private set; } = 1f;
        public bool IsHoldingBreath { get; private set; }
        public bool IsBreathHoldExhausted { get; private set; }
        public bool CanHoldBreath => allowBreathControl
            && IsHidden
            && !IsCompromised
            && !IsBreathHoldExhausted
            && BreathHoldCapacity01 >= breathHoldMinimumCapacityToStart;
        public PlayerInputBindingAction BreathHoldInputAction => breathHoldInputAction;
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
                RecoverBreathHoldCapacity(Time.deltaTime);
                return;
            }

            PeekExposure01 = Mathf.MoveTowards(
                PeekExposure01,
                0f,
                peekExposureRecoveryRate * Time.deltaTime);

            UpdateBreathControl(
                Time.deltaTime,
                PlayerInputBindings.IsPressed(breathHoldInputAction));
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
                IsHoldingBreath = false;
                IsBreathHoldExhausted = false;
                BreathHoldCapacity01 = 1f;
                breathHoldRecoveryBlockedUntilTime = 0f;
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
                IsHoldingBreath = false;
                IsBreathHoldExhausted = false;
                BreathHoldCapacity01 = 1f;
                breathHoldRecoveryBlockedUntilTime = 0f;
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
                || IsHoldingBreath
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

        private void UpdateBreathControl(float deltaTime, bool wantsToHoldBreath)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            if (!allowBreathControl || !IsHidden || IsCompromised)
            {
                StopHoldingBreath(false);
                RecoverBreathHoldCapacity(deltaTime);
                return;
            }

            bool canContinueHolding = IsHoldingBreath && BreathHoldCapacity01 > 0f;
            if (wantsToHoldBreath && (canContinueHolding || CanHoldBreath))
            {
                if (!IsHoldingBreath)
                {
                    IsHoldingBreath = true;
                    ReportHidingFeedback(
                        CurrentHideSpot,
                        PlayerFeedbackEvents.HidingFeedbackKind.BreathHeld,
                        true,
                        false);
                }

                BreathHoldCapacity01 = Mathf.MoveTowards(
                    BreathHoldCapacity01,
                    0f,
                    deltaTime / Mathf.Max(0.1f, breathHoldDuration));
                if (BreathHoldCapacity01 <= 0.001f)
                {
                    ExhaustBreathHold();
                }

                return;
            }

            StopHoldingBreath(true);
            RecoverBreathHoldCapacity(deltaTime);
        }

        private void StopHoldingBreath(bool reportRelease)
        {
            if (!IsHoldingBreath)
            {
                return;
            }

            IsHoldingBreath = false;
            breathHoldRecoveryBlockedUntilTime = Time.time + breathHoldRecoveryDelay;
            if (reportRelease && IsHidden && !IsBreathHoldExhausted)
            {
                PlayerFeedbackEvents.ReportStealthLoop(
                    PlayerFeedbackEvents.StealthLoopPhase.Hiding,
                    BreathTension01,
                    0f,
                    BreathTension01,
                    "Breathing again.");
            }
        }

        private void RecoverBreathHoldCapacity(float deltaTime)
        {
            if (deltaTime <= 0f || IsHoldingBreath || Time.time < breathHoldRecoveryBlockedUntilTime)
            {
                return;
            }

            BreathHoldCapacity01 = Mathf.MoveTowards(
                BreathHoldCapacity01,
                1f,
                breathHoldRecoveryRate * deltaTime);
            if (IsBreathHoldExhausted
                && BreathHoldCapacity01 >= breathHoldExhaustionRecoveryThreshold)
            {
                IsBreathHoldExhausted = false;
            }
        }

        private void ExhaustBreathHold()
        {
            IsHoldingBreath = false;
            IsBreathHoldExhausted = true;
            BreathHoldCapacity01 = 0f;
            breathHoldRecoveryBlockedUntilTime = Time.time + breathHoldRecoveryDelay;
            AddBreathTension(breathHoldExhaustionTensionIncrease);

            float loudness = breathHoldExhaustionNoiseRadius > 0f
                ? breathHoldExhaustionNoiseLoudness
                : 0f;
            ReportHidingFeedback(
                CurrentHideSpot,
                PlayerFeedbackEvents.HidingFeedbackKind.BreathExhausted,
                true,
                IsCompromised,
                loudness);
            if (loudness <= 0f)
            {
                return;
            }

            GameObject noiseObject = new("HeldBreathGaspNoiseEvent");
            noiseObject.transform.position = transform.position;
            noiseObject.AddComponent<SphereCollider>();
            NoiseEvent noiseEvent = noiseObject.AddComponent<NoiseEvent>();
            noiseEvent.Initialize(
                transform.position,
                breathHoldExhaustionNoiseRadius,
                loudness,
                gameObject,
                breathHoldExhaustionNoiseLifetime,
                Mathf.Max(loudness, BreathTension01),
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
            if (IsHoldingBreath)
            {
                float dangerBuild = 0f;
                if (danger01 > 0.05f && HiddenDuration > hiddenBreathGraceTime)
                {
                    float dangerBuildMultiplier = Mathf.Lerp(0.35f, 1f, danger01);
                    dangerBuild = breathTensionBuildRate
                        * dangerBuildMultiplier
                        * breathHoldDangerBuildMultiplier;
                }

                BreathTension01 = Mathf.Clamp01(
                    BreathTension01
                    + (dangerBuild - breathHoldTensionReliefRate) * deltaTime);
                return;
            }

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
                kind == PlayerFeedbackEvents.HidingFeedbackKind.Recovered
                    || kind == PlayerFeedbackEvents.HidingFeedbackKind.BreathHeld);
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
                PlayerFeedbackEvents.HidingFeedbackKind.BreathHeld => "Holding breath",
                PlayerFeedbackEvents.HidingFeedbackKind.BreathNoisy => "Your breathing is too loud.",
                PlayerFeedbackEvents.HidingFeedbackKind.BreathExhausted => "You gasped for air.",
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
