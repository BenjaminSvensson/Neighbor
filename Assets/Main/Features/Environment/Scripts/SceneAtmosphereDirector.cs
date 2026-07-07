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

        [Header("Color Grade")]
        [SerializeField, Range(-2f, 2f)] private float exposure = -0.18f;
        [SerializeField, Range(-100f, 100f)] private float contrast = 18f;
        [SerializeField, Range(-100f, 100f)] private float saturation = -16f;
        [SerializeField] private Color colorFilter = new(0.86f, 0.93f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float vignetteIntensity = 0.26f;
        [SerializeField, Range(0f, 1f)] private float filmGrainIntensity = 0.22f;

        public Light SunLight => sunLight;
        public Light MoonLight => moonLight;
        public Volume ColorGradingVolume => colorGradingVolume;
        public bool HasColorGradingVolume => colorGradingVolume != null
            && colorGradingVolume.isGlobal
            && colorGradingVolume.profile != null;
        public int FlickerLightCount => CountLive(flickerLights);
        public int DirtyDecalAnchorCount => CountAnchors(AtmosphereDressingAnchor.DressingKind.DirtyDecal);
        public int PropDressingAnchorCount => CountAnchors(AtmosphereDressingAnchor.DressingKind.PropDressing);

        private void Awake()
        {
            ApplyAtmosphere();
        }

        private void OnEnable()
        {
            ApplyAtmosphere();
        }

        private void Update()
        {
            if (!Application.isPlaying)
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
            if (sunLight != null)
            {
                sunLight.intensity = Mathf.Min(sunLight.intensity, maximumSunIntensity);
                sunLight.color = Color.Lerp(sunLight.color, new Color(1f, 0.88f, 0.68f, 1f), 0.2f);
            }

            if (moonLight != null)
            {
                moonLight.intensity = Mathf.Max(moonLight.intensity, minimumMoonIntensity);
                moonLight.color = Color.Lerp(moonLight.color, new Color(0.56f, 0.66f, 1f, 1f), 0.35f);
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;
        }

        private void ApplyFog()
        {
            if (!driveFog)
            {
                return;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
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

            colorAdjustments.postExposure.Override(exposure);
            colorAdjustments.contrast.Override(contrast);
            colorAdjustments.saturation.Override(saturation);
            colorAdjustments.colorFilter.Override(colorFilter);

            if (!profile.TryGet(out Vignette vignette))
            {
                vignette = profile.Add<Vignette>(true);
            }

            vignette.intensity.Override(vignetteIntensity);
            vignette.smoothness.Override(0.42f);

            if (!profile.TryGet(out FilmGrain filmGrain))
            {
                filmGrain = profile.Add<FilmGrain>(true);
            }

            filmGrain.type.Override(FilmGrainLookup.Thin1);
            filmGrain.intensity.Override(filmGrainIntensity);
            filmGrain.response.Override(0.74f);
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
