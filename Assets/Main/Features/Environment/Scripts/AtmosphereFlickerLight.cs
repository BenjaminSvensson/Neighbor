using UnityEngine;

namespace Neighbor.Main.Features.Environment
{
    [RequireComponent(typeof(Light))]
    public sealed class AtmosphereFlickerLight : MonoBehaviour
    {
        [SerializeField] private Light targetLight;
        [SerializeField, Min(0f)] private float baseIntensity = 0.9f;
        [SerializeField, Range(0f, 1f)] private float flickerAmount = 0.22f;
        [SerializeField, Min(0f)] private float flickerSpeed = 7.5f;
        [SerializeField, Min(0f)] private float pulseSpeed = 1.15f;
        [SerializeField] private bool activeInEditMode = true;

        private float noiseSeed;

        public Light TargetLight => targetLight;
        public float BaseIntensity => baseIntensity;
        public float FlickerAmount => flickerAmount;

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
        }

        private void Update()
        {
            if (!Application.isPlaying && !activeInEditMode)
            {
                return;
            }

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

            float noise = Mathf.PerlinNoise(noiseSeed, time * flickerSpeed);
            float pulse = Mathf.Sin((time + noiseSeed) * pulseSpeed) * 0.5f + 0.5f;
            float flicker = Mathf.Lerp(noise, pulse, 0.35f);
            targetLight.intensity = Mathf.Max(0f, baseIntensity * Mathf.Lerp(1f - flickerAmount, 1f, flicker));
        }
    }
}
