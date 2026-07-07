using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Environment
{
    [ExecuteAlways]
    [RequireComponent(typeof(Light))]
    public sealed class AtmosphereFlickerLight : MonoBehaviour
    {
        [SerializeField] private Light targetLight;
        [SerializeField, Min(0f)] private float baseIntensity = 0.9f;
        [SerializeField, Range(0f, 1f)] private float flickerAmount = 0.22f;
        [SerializeField, Min(0f)] private float flickerSpeed = 7.5f;
        [SerializeField, Min(0f)] private float pulseSpeed = 1.15f;
        [SerializeField] private bool activeInEditMode = true;
        [Header("Stealth Response")]
        [SerializeField] private bool respondToStealthLoop = true;
        [SerializeField, Range(0f, 1f)] private float dangerFlickerAmountBoost = 0.35f;
        [SerializeField, Min(0f)] private float dangerFlickerSpeedBoost = 5f;
        [SerializeField, Range(0f, 1f)] private float dangerIntensityDip = 0.18f;
        [SerializeField, Min(0f)] private float dangerFlickerHoldDuration = 2.2f;
        [SerializeField, Min(0f)] private float dangerFlickerFadeSpeed = 2.4f;
        [Header("Memory Response")]
        [SerializeField] private bool respondToNeighborMemory = true;
        [SerializeField, Range(0f, 1f)] private float memoryFlickerPressure = 0.36f;
        [SerializeField, Range(0f, 1f)] private float stackedMemoryFlickerBoost = 0.12f;
        [SerializeField, Min(0f)] private float memoryFlickerHoldDuration = 3.4f;

        private float noiseSeed;
        private float currentStealthPressure;
        private float targetStealthPressure;
        private float stealthPressureHoldUntilTime;

        public Light TargetLight => targetLight;
        public float BaseIntensity => baseIntensity;
        public float FlickerAmount => flickerAmount;
        public float CurrentStealthPressure => currentStealthPressure;
        public float EffectiveFlickerAmount => GetEffectiveFlickerAmount();
        public float EffectiveFlickerSpeed => GetEffectiveFlickerSpeed();

        private void Awake()
        {
            ResolveLight();
            CaptureBaseIntensity();
            noiseSeed = Random.value * 100f;
        }

        private void OnEnable()
        {
            ResolveLight();
            CaptureBaseIntensity();
            PlayerFeedbackEvents.StealthLoopChanged -= HandleStealthLoopChanged;
            PlayerFeedbackEvents.StealthLoopChanged += HandleStealthLoopChanged;
            PlayerFeedbackEvents.NeighborMemoryChanged -= HandleNeighborMemoryChanged;
            PlayerFeedbackEvents.NeighborMemoryChanged += HandleNeighborMemoryChanged;
        }

        private void OnDisable()
        {
            PlayerFeedbackEvents.StealthLoopChanged -= HandleStealthLoopChanged;
            PlayerFeedbackEvents.NeighborMemoryChanged -= HandleNeighborMemoryChanged;
        }

        private void Update()
        {
            if (!Application.isPlaying && !activeInEditMode)
            {
                return;
            }

            UpdateStealthPressure(Time.unscaledDeltaTime);
            ApplyFlicker(Time.time);
        }

        public void Configure(float intensity, float amount, float speed)
        {
            ResolveLight();
            baseIntensity = Mathf.Max(0f, intensity);
            flickerAmount = Mathf.Clamp01(amount);
            flickerSpeed = Mathf.Max(0f, speed);
            ApplyFlicker(Time.time);
        }

        private void ResolveLight()
        {
            targetLight = targetLight != null ? targetLight : GetComponent<Light>();
        }

        private void CaptureBaseIntensity()
        {
            if (targetLight != null && baseIntensity <= 0f)
            {
                baseIntensity = targetLight.intensity;
            }
        }

        private void ApplyFlicker(float time)
        {
            if (targetLight == null)
            {
                return;
            }

            float noise = Mathf.PerlinNoise(noiseSeed, time * GetEffectiveFlickerSpeed());
            float pulse = Mathf.Sin((time + noiseSeed) * pulseSpeed) * 0.5f + 0.5f;
            float flicker = Mathf.Lerp(noise, pulse, 0.35f);
            float effectiveFlicker = GetEffectiveFlickerAmount();
            float dangerScale = Mathf.Lerp(1f, 1f - dangerIntensityDip, currentStealthPressure);
            targetLight.intensity = Mathf.Max(
                0f,
                baseIntensity * dangerScale * Mathf.Lerp(1f - effectiveFlicker, 1f, flicker));
        }

        private void HandleStealthLoopChanged(PlayerFeedbackEvents.StealthLoopFeedback feedback)
        {
            if (!respondToStealthLoop)
            {
                return;
            }

            RaiseStealthPressure(GetStealthPressure(feedback), dangerFlickerHoldDuration);
        }

        private void HandleNeighborMemoryChanged(PlayerFeedbackEvents.NeighborMemoryFeedback feedback)
        {
            if (!respondToNeighborMemory)
            {
                return;
            }

            RaiseStealthPressure(GetMemoryPressure(feedback), memoryFlickerHoldDuration);
        }

        private void RaiseStealthPressure(float pressure, float holdDuration)
        {
            targetStealthPressure = Mathf.Clamp01(pressure);
            stealthPressureHoldUntilTime = targetStealthPressure <= 0f
                ? 0f
                : Time.unscaledTime + holdDuration;

            if (targetStealthPressure > currentStealthPressure)
            {
                currentStealthPressure = targetStealthPressure;
            }

            ApplyFlicker(Time.time);
        }

        private void UpdateStealthPressure(float deltaTime)
        {
            if (!respondToStealthLoop && !respondToNeighborMemory)
            {
                targetStealthPressure = 0f;
            }

            float desiredPressure = Time.unscaledTime <= stealthPressureHoldUntilTime
                ? targetStealthPressure
                : 0f;
            currentStealthPressure = Mathf.MoveTowards(
                currentStealthPressure,
                desiredPressure,
                dangerFlickerFadeSpeed * Mathf.Max(0f, deltaTime));
        }

        private float GetEffectiveFlickerAmount()
        {
            return Mathf.Clamp01(flickerAmount + dangerFlickerAmountBoost * currentStealthPressure);
        }

        private float GetEffectiveFlickerSpeed()
        {
            return flickerSpeed + dangerFlickerSpeedBoost * currentStealthPressure;
        }

        private static float GetStealthPressure(PlayerFeedbackEvents.StealthLoopFeedback feedback)
        {
            float pressure = Mathf.Max(feedback.Suspicion, feedback.Noise, feedback.Tension);
            return feedback.Phase switch
            {
                PlayerFeedbackEvents.StealthLoopPhase.Chased => 1f,
                PlayerFeedbackEvents.StealthLoopPhase.PostChase => Mathf.Max(0.58f, pressure),
                PlayerFeedbackEvents.StealthLoopPhase.Searching => Mathf.Max(0.48f, pressure),
                PlayerFeedbackEvents.StealthLoopPhase.Suspicious => Mathf.Max(0.36f, pressure),
                PlayerFeedbackEvents.StealthLoopPhase.Hiding => Mathf.Max(0.3f, feedback.Tension),
                PlayerFeedbackEvents.StealthLoopPhase.Curious => Mathf.Max(0.16f, pressure * 0.7f),
                _ => 0f
            };
        }

        private float GetMemoryPressure(PlayerFeedbackEvents.NeighborMemoryFeedback feedback)
        {
            float stackPressure = Mathf.Clamp01(feedback.TotalMemoryCount / 4f) * stackedMemoryFlickerBoost;
            return Mathf.Clamp01(Mathf.Max(memoryFlickerPressure, feedback.Urgency) + stackPressure);
        }
    }
}
