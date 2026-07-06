using System;
using Neighbor.Main.Features.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace Neighbor.Main.Features.Environment
{
    public enum DayNightPhase
    {
        Night = 0,
        Dawn = 1,
        Day = 2,
        Dusk = 3
    }

    [ExecuteAlways]
    public sealed class DayNightCycle : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Light sunLight;
        [SerializeField] private Light moonLight;
        [SerializeField] private Material skyboxMaterial;

        [Header("Time")]
        [SerializeField, Range(0f, 1f)] private float timeOfDay = 0.36f;
        [SerializeField, Min(0.01f)] private float dayLengthMinutes = 14f;
        [SerializeField] private bool cycleRuns = true;
        [SerializeField] private bool previewInEditMode = true;

        [Header("Phase Thresholds")]
        [SerializeField, Range(0f, 1f)] private float dawnStart = 0.2f;
        [SerializeField, Range(0f, 1f)] private float dayStart = 0.3f;
        [SerializeField, Range(0f, 1f)] private float duskStart = 0.72f;
        [SerializeField, Range(0f, 1f)] private float nightStart = 0.82f;

        [Header("Sun")]
        [SerializeField] private float sunOrbitYaw = -30f;
        [SerializeField, Min(0f)] private float maxSunIntensity = 1.15f;
        [SerializeField] private AnimationCurve sunIntensityByTime = CreateDefaultSunIntensity();
        [SerializeField] private Gradient sunColorByTime = CreateDefaultSunColor();

        [Header("Moon")]
        [SerializeField] private float moonOrbitYaw = 150f;
        [SerializeField, Min(0f)] private float maxMoonIntensity = 0.28f;
        [SerializeField] private AnimationCurve moonIntensityByTime = CreateDefaultMoonIntensity();
        [SerializeField] private Gradient moonColorByTime = CreateDefaultMoonColor();
        [SerializeField] private bool disableInactiveLights = true;

        [Header("Environment")]
        [SerializeField] private bool driveAmbientColor = true;
        [SerializeField] private Gradient ambientColorByTime = CreateDefaultAmbientColor();
        [SerializeField, Min(0f)] private float ambientIntensity = 1f;
        [SerializeField] private bool driveFog = true;
        [SerializeField] private Gradient fogColorByTime = CreateDefaultFogColor();
        [SerializeField, Min(0f)] private float maxFogDensity = 0.018f;
        [SerializeField] private AnimationCurve fogDensityByTime = CreateDefaultFogDensity();
        [SerializeField] private bool rotateSkybox = true;
        [SerializeField] private float skyboxRotationOffset;

        [Header("Player Feedback")]
        [SerializeField] private bool announcePhaseChanges = true;
        [SerializeField] private bool announceInitialPhase;

        private bool hasObservedPhase;

        public event Action<DayNightPhase> PhaseChanged;

        public Light SunLight => sunLight;
        public Light MoonLight => moonLight;
        public float TimeOfDay => timeOfDay;
        public float DayLengthMinutes => dayLengthMinutes;
        public bool IsCycleRunning => cycleRuns;
        public DayNightPhase CurrentPhase { get; private set; }
        public bool IsDaytime => CurrentPhase == DayNightPhase.Dawn || CurrentPhase == DayNightPhase.Day;

        private void Awake()
        {
            ResolveReferences();
            ApplyLighting(false);
        }

        private void OnEnable()
        {
            ResolveReferences();
            ApplyLighting(false);
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                if (cycleRuns)
                {
                    Advance(Time.deltaTime);
                }

                return;
            }

            if (previewInEditMode)
            {
                ApplyLighting(false);
            }
        }

        public void SetLights(Light sun, Light moon)
        {
            sunLight = sun;
            moonLight = moon;
            ApplyLighting(false);
        }

        public void SetTimeOfDay(float normalizedTime)
        {
            timeOfDay = Mathf.Repeat(normalizedTime, 1f);
            ApplyLighting(true);
        }

        public void SetCycleRunning(bool isRunning)
        {
            cycleRuns = isRunning;
        }

        public void SetDayLengthMinutes(float minutes)
        {
            dayLengthMinutes = Mathf.Max(0.01f, minutes);
        }

        [ContextMenu("Apply Current Time")]
        public void ApplyLighting()
        {
            ApplyLighting(true);
        }

        private void Advance(float deltaTime)
        {
            float secondsPerDay = Mathf.Max(0.01f, dayLengthMinutes) * 60f;
            timeOfDay = Mathf.Repeat(timeOfDay + deltaTime / secondsPerDay, 1f);
            ApplyLighting(true);
        }

        private void ApplyLighting(bool allowPhaseFeedback)
        {
            NormalizeSettings();
            ApplySun(timeOfDay);
            ApplyMoon(timeOfDay);
            ApplyEnvironment(timeOfDay);
            UpdatePhase(timeOfDay, allowPhaseFeedback);
        }

        private void ApplySun(float normalizedTime)
        {
            if (sunLight == null)
            {
                return;
            }

            float intensity = Mathf.Max(0f, EvaluateCurve(sunIntensityByTime, normalizedTime, 1f) * maxSunIntensity);
            sunLight.transform.rotation = Quaternion.Euler(GetOrbitPitch(normalizedTime), sunOrbitYaw, 0f);
            sunLight.color = EvaluateGradient(sunColorByTime, normalizedTime, Color.white);
            sunLight.intensity = intensity;
            sunLight.shadows = LightShadows.Soft;
            sunLight.enabled = !disableInactiveLights || intensity > 0.001f;
            RenderSettings.sun = sunLight;
        }

        private void ApplyMoon(float normalizedTime)
        {
            if (moonLight == null)
            {
                return;
            }

            float intensity = Mathf.Max(0f, EvaluateCurve(moonIntensityByTime, normalizedTime, 1f) * maxMoonIntensity);
            moonLight.transform.rotation = Quaternion.Euler(GetOrbitPitch(Mathf.Repeat(normalizedTime + 0.5f, 1f)), moonOrbitYaw, 0f);
            moonLight.color = EvaluateGradient(moonColorByTime, normalizedTime, new Color(0.62f, 0.7f, 1f));
            moonLight.intensity = intensity;
            moonLight.shadows = LightShadows.None;
            moonLight.enabled = !disableInactiveLights || intensity > 0.001f;
        }

        private void ApplyEnvironment(float normalizedTime)
        {
            if (driveAmbientColor)
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = EvaluateGradient(ambientColorByTime, normalizedTime, Color.gray) * ambientIntensity;
            }

            if (driveFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogColor = EvaluateGradient(fogColorByTime, normalizedTime, Color.gray);
                RenderSettings.fogDensity = Mathf.Max(0f, EvaluateCurve(fogDensityByTime, normalizedTime, 0.5f) * maxFogDensity);
            }

            if (skyboxMaterial == null)
            {
                return;
            }

            RenderSettings.skybox = skyboxMaterial;
            if (rotateSkybox && skyboxMaterial.HasFloat("_Rotation"))
            {
                skyboxMaterial.SetFloat("_Rotation", Mathf.Repeat((normalizedTime * 360f) + skyboxRotationOffset, 360f));
            }
        }

        private void UpdatePhase(float normalizedTime, bool allowFeedback)
        {
            DayNightPhase phase = GetPhase(normalizedTime);
            bool isInitialPhase = !hasObservedPhase;
            if (!isInitialPhase && phase == CurrentPhase)
            {
                return;
            }

            hasObservedPhase = true;
            CurrentPhase = phase;
            PhaseChanged?.Invoke(phase);

            if (!allowFeedback
                || !Application.isPlaying
                || !announcePhaseChanges
                || (isInitialPhase && !announceInitialPhase))
            {
                return;
            }

            PlayerFeedbackEvents.ReportDayPhase(
                phase.ToString(),
                GetPhaseMessage(phase),
                GetPhaseMessageIntensity(phase),
                normalizedTime);
        }

        public DayNightPhase GetPhase(float normalizedTime)
        {
            float normalized = Mathf.Repeat(normalizedTime, 1f);
            if (normalized >= nightStart || normalized < dawnStart)
            {
                return DayNightPhase.Night;
            }

            if (normalized < dayStart)
            {
                return DayNightPhase.Dawn;
            }

            if (normalized < duskStart)
            {
                return DayNightPhase.Day;
            }

            return DayNightPhase.Dusk;
        }

        private void ResolveReferences()
        {
            if (sunLight == null)
            {
                sunLight = RenderSettings.sun != null ? RenderSettings.sun : FindDirectionalLight("sun", true);
            }

            if (moonLight == null)
            {
                moonLight = FindDirectionalLight("moon", false);
            }
        }

        private static Light FindDirectionalLight(string nameHint, bool allowFallback)
        {
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            Light fallback = null;
            for (int i = 0; i < lights.Length; i++)
            {
                Light candidate = lights[i];
                if (candidate == null || candidate.type != LightType.Directional)
                {
                    continue;
                }

                if (fallback == null && !candidate.name.ToLowerInvariant().Contains("moon"))
                {
                    fallback = candidate;
                }

                if (candidate.name.ToLowerInvariant().Contains(nameHint))
                {
                    return candidate;
                }
            }

            return allowFallback ? fallback : null;
        }

        private static string GetPhaseMessage(DayNightPhase phase)
        {
            return phase switch
            {
                DayNightPhase.Night => "NIGHTFALL",
                DayNightPhase.Dawn => "DAWN BREAKS",
                DayNightPhase.Day => "DAYLIGHT RETURNS",
                DayNightPhase.Dusk => "LIGHT IS FADING",
                _ => null
            };
        }

        private static float GetPhaseMessageIntensity(DayNightPhase phase)
        {
            return phase switch
            {
                DayNightPhase.Night => 0.78f,
                DayNightPhase.Dusk => 0.58f,
                DayNightPhase.Dawn => 0.34f,
                DayNightPhase.Day => 0.24f,
                _ => 0f
            };
        }

        private static float GetOrbitPitch(float normalizedTime)
        {
            return normalizedTime * 360f - 90f;
        }

        private static float EvaluateCurve(AnimationCurve curve, float normalizedTime, float fallback)
        {
            return curve != null && curve.length > 0 ? curve.Evaluate(normalizedTime) : fallback;
        }

        private static Color EvaluateGradient(Gradient gradient, float normalizedTime, Color fallback)
        {
            return gradient != null ? gradient.Evaluate(normalizedTime) : fallback;
        }

        private void OnValidate()
        {
            NormalizeSettings();

            if (previewInEditMode)
            {
                ResolveReferences();
                ApplyLighting(false);
            }
        }

        private void NormalizeSettings()
        {
            timeOfDay = Mathf.Repeat(timeOfDay, 1f);
            dayLengthMinutes = Mathf.Max(0.01f, dayLengthMinutes);
            maxSunIntensity = Mathf.Max(0f, maxSunIntensity);
            maxMoonIntensity = Mathf.Max(0f, maxMoonIntensity);
            ambientIntensity = Mathf.Max(0f, ambientIntensity);
            maxFogDensity = Mathf.Max(0f, maxFogDensity);

            dawnStart = Mathf.Clamp01(dawnStart);
            dayStart = Mathf.Clamp(dayStart, dawnStart, 1f);
            duskStart = Mathf.Clamp(duskStart, dayStart, 1f);
            nightStart = Mathf.Clamp(nightStart, duskStart, 1f);
        }

        private static AnimationCurve CreateDefaultSunIntensity()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.2f, 0f),
                new Keyframe(0.28f, 0.42f),
                new Keyframe(0.5f, 1f),
                new Keyframe(0.72f, 0.4f),
                new Keyframe(0.82f, 0f),
                new Keyframe(1f, 0f));
        }

        private static AnimationCurve CreateDefaultMoonIntensity()
        {
            return new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.18f, 0.55f),
                new Keyframe(0.3f, 0f),
                new Keyframe(0.7f, 0f),
                new Keyframe(0.82f, 0.55f),
                new Keyframe(1f, 1f));
        }

        private static AnimationCurve CreateDefaultFogDensity()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0.9f),
                new Keyframe(0.25f, 0.55f),
                new Keyframe(0.5f, 0.22f),
                new Keyframe(0.75f, 0.6f),
                new Keyframe(1f, 0.9f));
        }

        private static Gradient CreateDefaultSunColor()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.43f, 0.5f, 0.86f), 0f),
                    new GradientColorKey(new Color(1f, 0.57f, 0.32f), 0.25f),
                    new GradientColorKey(new Color(1f, 0.95f, 0.82f), 0.5f),
                    new GradientColorKey(new Color(1f, 0.48f, 0.28f), 0.75f),
                    new GradientColorKey(new Color(0.43f, 0.5f, 0.86f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        private static Gradient CreateDefaultMoonColor()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.45f, 0.56f, 0.95f), 0f),
                    new GradientColorKey(new Color(0.65f, 0.72f, 1f), 0.5f),
                    new GradientColorKey(new Color(0.45f, 0.56f, 0.95f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        private static Gradient CreateDefaultAmbientColor()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.05f, 0.06f, 0.12f), 0f),
                    new GradientColorKey(new Color(0.34f, 0.25f, 0.18f), 0.25f),
                    new GradientColorKey(new Color(0.68f, 0.74f, 0.78f), 0.5f),
                    new GradientColorKey(new Color(0.34f, 0.22f, 0.2f), 0.75f),
                    new GradientColorKey(new Color(0.05f, 0.06f, 0.12f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        private static Gradient CreateDefaultFogColor()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.03f, 0.04f, 0.08f), 0f),
                    new GradientColorKey(new Color(0.55f, 0.38f, 0.28f), 0.25f),
                    new GradientColorKey(new Color(0.6f, 0.72f, 0.82f), 0.5f),
                    new GradientColorKey(new Color(0.48f, 0.28f, 0.32f), 0.75f),
                    new GradientColorKey(new Color(0.03f, 0.04f, 0.08f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }
    }
}
