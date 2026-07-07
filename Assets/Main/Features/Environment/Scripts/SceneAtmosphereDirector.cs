using Neighbor.Main.Features.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Neighbor.Main.Features.Environment
{
    [ExecuteAlways]
    public sealed class SceneAtmosphereDirector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Light sunLight;
        [SerializeField] private Light moonLight;
        [SerializeField] private Volume colorGradingVolume;
        [SerializeField] private AtmosphereFlickerLight[] flickerLights;
        [SerializeField] private AtmosphereDressingAnchor[] dressingAnchors;

        [Header("Fog And Ambient")]
        [SerializeField] private bool driveFog = true;
        [SerializeField] private Color fogColor = new(0.34f, 0.43f, 0.46f, 1f);
        [SerializeField, Min(0f)] private float fogDensity = 0.022f;
        [SerializeField] private Color ambientColor = new(0.2f, 0.23f, 0.28f, 1f);
        [SerializeField, Min(0f)] private float maximumSunIntensity = 0.95f;
        [SerializeField, Min(0f)] private float minimumMoonIntensity = 0.18f;
        [SerializeField, Range(0f, 1f)] private float stealthSunIntensityDip = 0.32f;
        [SerializeField, Min(0f)] private float stealthMoonIntensityBoost = 0.34f;

        [Header("Color Grade")]
        [SerializeField, Range(-2f, 2f)] private float exposure = -0.18f;
        [SerializeField, Range(-100f, 100f)] private float contrast = 18f;
        [SerializeField, Range(-100f, 100f)] private float saturation = -16f;
        [SerializeField] private Color colorFilter = new(0.86f, 0.93f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float vignetteIntensity = 0.26f;
        [SerializeField, Range(0f, 1f)] private float filmGrainIntensity = 0.22f;

        [Header("Stealth Atmosphere Response")]
        [SerializeField] private bool respondToStealthLoop = true;
        [SerializeField, Range(0f, 1f)] private float searchingAtmosphereIntensity = 0.52f;
        [SerializeField, Range(0f, 1f)] private float postChaseAtmosphereIntensity = 0.64f;
        [SerializeField, Range(0f, 1f)] private float hidingAtmosphereIntensity = 0.42f;
        [SerializeField, Range(0f, 1f)] private float calmHidingAtmosphereIntensity = 0.14f;
        [SerializeField, Min(0f)] private float stealthAtmosphereHoldDuration = 2.8f;
        [SerializeField, Min(0f)] private float stealthAtmosphereFadeSpeed = 1.6f;
        [SerializeField, Min(0f)] private float stealthFogDensityBoost = 0.02f;
        [SerializeField] private Color stealthFogColor = new(0.2f, 0.26f, 0.3f, 1f);
        [SerializeField, Range(-2f, 0f)] private float stealthExposureOffset = -0.24f;
        [SerializeField, Range(0f, 100f)] private float stealthContrastBoost = 14f;
        [SerializeField, Range(-100f, 0f)] private float stealthSaturationOffset = -10f;
        [SerializeField] private Color stealthColorFilter = new(0.76f, 0.86f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float stealthVignetteBoost = 0.24f;
        [SerializeField, Range(0f, 1f)] private float stealthFilmGrainBoost = 0.2f;
        [Header("Memory Atmosphere Response")]
        [SerializeField] private bool respondToNeighborMemory = true;
        [SerializeField, Range(0f, 1f)] private float memoryAtmosphereIntensity = 0.42f;
        [SerializeField, Range(0f, 1f)] private float stackedMemoryAtmosphereBoost = 0.16f;
        [SerializeField, Min(0f)] private float memoryAtmosphereHoldDuration = 4.2f;
        [Header("Investigation Atmosphere Response")]
        [SerializeField] private bool respondToNeighborInvestigation = true;
        [SerializeField, Range(0f, 1f)] private float trailInvestigationAtmosphereIntensity = 0.72f;
        [SerializeField, Range(0f, 1f)] private float searchingInvestigationAtmosphereIntensity = 0.5f;
        [SerializeField, Range(0f, 1f)] private float resolvedTrailAtmosphereIntensity = 0.16f;
        [SerializeField, Min(0f)] private float investigationAtmosphereHoldDuration = 3.2f;
        [Header("Noise Atmosphere Response")]
        [SerializeField] private bool respondToNoiseFeedback = true;
        [SerializeField, Range(0f, 1f)] private float heardNoiseAtmosphereIntensity = 0.5f;
        [SerializeField, Min(0f)] private float heardNoiseAtmosphereHoldDuration = 2.4f;

        private float currentStealthAtmosphereIntensity;
        private float targetStealthAtmosphereIntensity;
        private float stealthAtmosphereHoldUntilTime;
        private Light capturedSunLight;
        private Light capturedMoonLight;
        private float capturedSunIntensity = -1f;
        private float capturedMoonIntensity = -1f;

        public Light SunLight => sunLight;
        public Light MoonLight => moonLight;
        public Volume ColorGradingVolume => colorGradingVolume;
        public bool HasColorGradingVolume => colorGradingVolume != null
            && colorGradingVolume.isGlobal
            && colorGradingVolume.profile != null;
        public int FlickerLightCount => CountLive(flickerLights);
        public int DirtyDecalAnchorCount => CountAnchors(AtmosphereDressingAnchor.DressingKind.DirtyDecal);
        public int PropDressingAnchorCount => CountAnchors(AtmosphereDressingAnchor.DressingKind.PropDressing);
        public float CurrentStealthAtmosphereIntensity => currentStealthAtmosphereIntensity;
        public float TargetStealthAtmosphereIntensity => targetStealthAtmosphereIntensity;

        private void Awake()
        {
            ApplyAtmosphere();
        }

        private void OnEnable()
        {
            PlayerFeedbackEvents.StealthLoopChanged -= HandleStealthLoopChanged;
            PlayerFeedbackEvents.StealthLoopChanged += HandleStealthLoopChanged;
            PlayerFeedbackEvents.NeighborMemoryChanged -= HandleNeighborMemoryChanged;
            PlayerFeedbackEvents.NeighborMemoryChanged += HandleNeighborMemoryChanged;
            PlayerFeedbackEvents.NeighborInvestigationChanged -= HandleNeighborInvestigationChanged;
            PlayerFeedbackEvents.NeighborInvestigationChanged += HandleNeighborInvestigationChanged;
            PlayerFeedbackEvents.NoiseEmitted -= HandleNoiseEmitted;
            PlayerFeedbackEvents.NoiseEmitted += HandleNoiseEmitted;
            ApplyAtmosphere();
        }

        private void OnDisable()
        {
            PlayerFeedbackEvents.StealthLoopChanged -= HandleStealthLoopChanged;
            PlayerFeedbackEvents.NeighborMemoryChanged -= HandleNeighborMemoryChanged;
            PlayerFeedbackEvents.NeighborInvestigationChanged -= HandleNeighborInvestigationChanged;
            PlayerFeedbackEvents.NoiseEmitted -= HandleNoiseEmitted;
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                UpdateStealthAtmosphere(Time.unscaledDeltaTime);
            }
            else
            {
                ApplyAtmosphere();
            }
        }

        public void Configure(
            Light sun,
            Light moon,
            Volume volume,
            AtmosphereFlickerLight[] flickers,
            AtmosphereDressingAnchor[] anchors)
        {
            sunLight = sun;
            moonLight = moon;
            colorGradingVolume = volume;
            flickerLights = flickers;
            dressingAnchors = anchors;
            CaptureLightingBaselines();
            ApplyAtmosphere();
        }

        [ContextMenu("Apply Atmosphere")]
        public void ApplyAtmosphere()
        {
            ApplyLightingMood();
            ApplyFog();
            ApplyColorGrade();
        }

        private void ApplyLightingMood()
        {
            CaptureLightingBaselines();
            if (sunLight != null)
            {
                float baseSunIntensity = capturedSunIntensity >= 0f
                    ? capturedSunIntensity
                    : sunLight.intensity;
                baseSunIntensity = Mathf.Min(baseSunIntensity, maximumSunIntensity);
                float dangerSunScale = Mathf.Lerp(
                    1f,
                    Mathf.Clamp01(1f - stealthSunIntensityDip),
                    currentStealthAtmosphereIntensity);
                sunLight.intensity = Mathf.Max(0f, baseSunIntensity * dangerSunScale);
                sunLight.color = Color.Lerp(sunLight.color, new Color(1f, 0.88f, 0.68f, 1f), 0.2f);
            }

            if (moonLight != null)
            {
                float baseMoonIntensity = capturedMoonIntensity >= 0f
                    ? capturedMoonIntensity
                    : moonLight.intensity;
                baseMoonIntensity = Mathf.Max(baseMoonIntensity, minimumMoonIntensity);
                moonLight.intensity = baseMoonIntensity
                    + stealthMoonIntensityBoost * currentStealthAtmosphereIntensity;
                moonLight.color = Color.Lerp(moonLight.color, new Color(0.56f, 0.66f, 1f, 1f), 0.35f);
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;
        }

        private void CaptureLightingBaselines()
        {
            if (sunLight != capturedSunLight)
            {
                capturedSunLight = sunLight;
                capturedSunIntensity = sunLight != null
                    ? Mathf.Min(sunLight.intensity, maximumSunIntensity)
                    : -1f;
            }

            if (moonLight != capturedMoonLight)
            {
                capturedMoonLight = moonLight;
                capturedMoonIntensity = moonLight != null
                    ? Mathf.Max(moonLight.intensity, minimumMoonIntensity)
                    : -1f;
            }
        }

        private void ApplyFog()
        {
            if (!driveFog)
            {
                return;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(fogColor, stealthFogColor, currentStealthAtmosphereIntensity);
            RenderSettings.fogDensity = fogDensity + stealthFogDensityBoost * currentStealthAtmosphereIntensity;
        }

        private void ApplyColorGrade()
        {
            if (colorGradingVolume == null)
            {
                return;
            }

            colorGradingVolume.isGlobal = true;
            colorGradingVolume.priority = Mathf.Max(colorGradingVolume.priority, 20f);
            colorGradingVolume.weight = 1f;
            if (colorGradingVolume.profile == null)
            {
                colorGradingVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
                colorGradingVolume.profile.name = "PrototypeAtmosphereProfile";
                colorGradingVolume.profile.hideFlags = HideFlags.DontSaveInBuild;
            }

            VolumeProfile profile = colorGradingVolume.profile;
            if (!profile.TryGet(out ColorAdjustments colorAdjustments))
            {
                colorAdjustments = profile.Add<ColorAdjustments>(true);
            }

            colorAdjustments.postExposure.Override(
                exposure + stealthExposureOffset * currentStealthAtmosphereIntensity);
            colorAdjustments.contrast.Override(
                contrast + stealthContrastBoost * currentStealthAtmosphereIntensity);
            colorAdjustments.saturation.Override(
                saturation + stealthSaturationOffset * currentStealthAtmosphereIntensity);
            colorAdjustments.colorFilter.Override(
                Color.Lerp(colorFilter, stealthColorFilter, currentStealthAtmosphereIntensity));

            if (!profile.TryGet(out Vignette vignette))
            {
                vignette = profile.Add<Vignette>(true);
            }

            vignette.intensity.Override(
                Mathf.Clamp01(vignetteIntensity + stealthVignetteBoost * currentStealthAtmosphereIntensity));
            vignette.smoothness.Override(0.42f);

            if (!profile.TryGet(out FilmGrain filmGrain))
            {
                filmGrain = profile.Add<FilmGrain>(true);
            }

            filmGrain.type.Override(FilmGrainLookup.Thin1);
            filmGrain.intensity.Override(
                Mathf.Clamp01(filmGrainIntensity + stealthFilmGrainBoost * currentStealthAtmosphereIntensity));
            filmGrain.response.Override(0.74f);
        }

        private void HandleStealthLoopChanged(PlayerFeedbackEvents.StealthLoopFeedback feedback)
        {
            if (!respondToStealthLoop)
            {
                return;
            }

            RaiseAtmospherePressure(GetStealthAtmosphereIntensity(feedback), stealthAtmosphereHoldDuration);
        }

        private void HandleNeighborMemoryChanged(PlayerFeedbackEvents.NeighborMemoryFeedback feedback)
        {
            if (!respondToNeighborMemory)
            {
                return;
            }

            RaiseAtmospherePressure(GetMemoryAtmosphereIntensity(feedback), memoryAtmosphereHoldDuration);
        }

        private void HandleNeighborInvestigationChanged(PlayerFeedbackEvents.NeighborInvestigationFeedback feedback)
        {
            if (!respondToNeighborInvestigation)
            {
                return;
            }

            float pressure = GetInvestigationAtmosphereIntensity(feedback);
            if (pressure > 0f)
            {
                RaiseAtmospherePressure(pressure, investigationAtmosphereHoldDuration);
            }
        }

        private void HandleNoiseEmitted(PlayerFeedbackEvents.NoiseFeedback feedback)
        {
            if (!respondToNoiseFeedback || !feedback.HeardByNeighbor)
            {
                return;
            }

            RaiseAtmospherePressure(GetNoiseAtmosphereIntensity(feedback), heardNoiseAtmosphereHoldDuration);
        }

        private void RaiseAtmospherePressure(float pressure, float holdDuration)
        {
            targetStealthAtmosphereIntensity = Mathf.Clamp01(pressure);
            stealthAtmosphereHoldUntilTime = targetStealthAtmosphereIntensity <= 0f
                ? 0f
                : Time.unscaledTime + holdDuration;

            if (targetStealthAtmosphereIntensity > currentStealthAtmosphereIntensity)
            {
                currentStealthAtmosphereIntensity = targetStealthAtmosphereIntensity;
            }

            ApplyAtmosphere();
        }

        private void UpdateStealthAtmosphere(float deltaTime)
        {
            if (!respondToStealthLoop
                && !respondToNeighborMemory
                && !respondToNeighborInvestigation
                && !respondToNoiseFeedback)
            {
                targetStealthAtmosphereIntensity = 0f;
            }

            float desiredIntensity = Time.unscaledTime <= stealthAtmosphereHoldUntilTime
                ? targetStealthAtmosphereIntensity
                : 0f;
            float previousIntensity = currentStealthAtmosphereIntensity;
            currentStealthAtmosphereIntensity = Mathf.MoveTowards(
                currentStealthAtmosphereIntensity,
                desiredIntensity,
                stealthAtmosphereFadeSpeed * Mathf.Max(0f, deltaTime));

            if (!Mathf.Approximately(previousIntensity, currentStealthAtmosphereIntensity))
            {
                ApplyAtmosphere();
            }
        }

        private float GetStealthAtmosphereIntensity(PlayerFeedbackEvents.StealthLoopFeedback feedback)
        {
            float feedbackPressure = Mathf.Max(feedback.Suspicion, feedback.Noise, feedback.Tension);
            return feedback.Phase switch
            {
                PlayerFeedbackEvents.StealthLoopPhase.Chased => 1f,
                PlayerFeedbackEvents.StealthLoopPhase.Searching => Mathf.Max(searchingAtmosphereIntensity, feedbackPressure),
                PlayerFeedbackEvents.StealthLoopPhase.PostChase => Mathf.Max(postChaseAtmosphereIntensity, feedbackPressure),
                PlayerFeedbackEvents.StealthLoopPhase.Hiding => feedback.IsCalming
                    ? Mathf.Max(calmHidingAtmosphereIntensity, feedback.Tension)
                    : Mathf.Max(hidingAtmosphereIntensity, feedback.Tension),
                PlayerFeedbackEvents.StealthLoopPhase.Certain => Mathf.Max(0.78f, feedbackPressure),
                PlayerFeedbackEvents.StealthLoopPhase.Suspicious => Mathf.Max(0.48f, feedbackPressure),
                PlayerFeedbackEvents.StealthLoopPhase.Curious => Mathf.Max(0.22f, Mathf.Min(0.42f, feedbackPressure)),
                _ => 0f
            };
        }

        private float GetMemoryAtmosphereIntensity(PlayerFeedbackEvents.NeighborMemoryFeedback feedback)
        {
            float stackPressure = Mathf.Clamp01(feedback.TotalMemoryCount / 4f) * stackedMemoryAtmosphereBoost;
            float pressure = Mathf.Max(memoryAtmosphereIntensity, feedback.Urgency) + stackPressure;
            return Mathf.Clamp01(pressure);
        }

        private float GetInvestigationAtmosphereIntensity(PlayerFeedbackEvents.NeighborInvestigationFeedback feedback)
        {
            float feedbackPressure = Mathf.Max(feedback.Suspicion, feedback.Urgency);
            return feedback.Kind switch
            {
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.CheckingHideSpot =>
                    Mathf.Max(0.92f, feedbackPressure),
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.FollowingTrail =>
                    Mathf.Max(trailInvestigationAtmosphereIntensity, feedbackPressure),
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Searching =>
                    feedback.IsTrailRelated
                        ? Mathf.Max(trailInvestigationAtmosphereIntensity, feedbackPressure)
                        : Mathf.Max(searchingInvestigationAtmosphereIntensity, feedbackPressure * 0.85f),
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Started =>
                    Mathf.Max(0.38f, feedbackPressure * 0.75f),
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Returning when feedback.IsTrailRelated =>
                    resolvedTrailAtmosphereIntensity,
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Abandoned when feedback.IsTrailRelated =>
                    resolvedTrailAtmosphereIntensity,
                _ => 0f
            };
        }

        private float GetNoiseAtmosphereIntensity(PlayerFeedbackEvents.NoiseFeedback feedback)
        {
            return Mathf.Clamp01(Mathf.Max(heardNoiseAtmosphereIntensity, Mathf.Max(feedback.Loudness, feedback.Urgency)));
        }

        private int CountAnchors(AtmosphereDressingAnchor.DressingKind kind)
        {
            int count = 0;
            if (dressingAnchors == null)
            {
                return count;
            }

            for (int i = 0; i < dressingAnchors.Length; i++)
            {
                if (dressingAnchors[i] != null && dressingAnchors[i].Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountLive<T>(T[] items) where T : Object
        {
            int count = 0;
            if (items == null)
            {
                return count;
            }

            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
