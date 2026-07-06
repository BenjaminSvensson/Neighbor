using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using EngineShadowResolution = UnityEngine.ShadowResolution;

namespace Neighbor.Main.Features.Player
{
    public enum PlayerPerformanceProfile
    {
        Performance,
        Balanced,
        Quality
    }

    public static class PlayerPerformanceSettings
    {
        public const string PreferenceKey = "Neighbor.PerformanceProfile";

        private static bool sceneHookRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeRuntime()
        {
            RegisterSceneHook();
            ApplySavedProfile();
        }

        public static PlayerPerformanceProfile LoadProfile()
        {
            PlayerPerformanceProfile profile = (PlayerPerformanceProfile)PlayerPrefs.GetInt(
                PreferenceKey,
                (int)PlayerPerformanceProfile.Balanced);
            return Normalize(profile);
        }

        public static void SetProfile(PlayerPerformanceProfile profile)
        {
            profile = Normalize(profile);
            PlayerPrefs.SetInt(PreferenceKey, (int)profile);
            PlayerPrefs.Save();
            ApplyProfile(profile);
        }

        public static void ApplySavedProfile()
        {
            ApplyProfile(LoadProfile());
        }

        public static void ApplyProfile(PlayerPerformanceProfile profile)
        {
            RegisterSceneHook();
            ProfileSettings settings = GetProfileSettings(Normalize(profile));
            ApplyQualitySettings(settings);
            ApplyTerrainSettings(settings);
            ApplyCameraSettings(settings);
        }

        public static PlayerPerformanceProfile GetNextProfile(PlayerPerformanceProfile profile)
        {
            return Normalize(profile) switch
            {
                PlayerPerformanceProfile.Performance => PlayerPerformanceProfile.Balanced,
                PlayerPerformanceProfile.Balanced => PlayerPerformanceProfile.Quality,
                _ => PlayerPerformanceProfile.Performance
            };
        }

        public static PlayerPerformanceProfile GetPreviousProfile(PlayerPerformanceProfile profile)
        {
            return Normalize(profile) switch
            {
                PlayerPerformanceProfile.Performance => PlayerPerformanceProfile.Quality,
                PlayerPerformanceProfile.Balanced => PlayerPerformanceProfile.Performance,
                _ => PlayerPerformanceProfile.Balanced
            };
        }

        public static string GetDisplayName(PlayerPerformanceProfile profile)
        {
            return Normalize(profile) switch
            {
                PlayerPerformanceProfile.Performance => "Performance",
                PlayerPerformanceProfile.Balanced => "Balanced",
                PlayerPerformanceProfile.Quality => "Quality",
                _ => "Balanced"
            };
        }

        private static void RegisterSceneHook()
        {
            if (sceneHookRegistered)
            {
                return;
            }

            SceneManager.sceneLoaded += HandleSceneLoaded;
            sceneHookRegistered = true;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplySavedProfile();
        }

        private static PlayerPerformanceProfile Normalize(PlayerPerformanceProfile profile)
        {
            return profile < PlayerPerformanceProfile.Performance || profile > PlayerPerformanceProfile.Quality
                ? PlayerPerformanceProfile.Balanced
                : profile;
        }

        private static ProfileSettings GetProfileSettings(PlayerPerformanceProfile profile)
        {
            return profile switch
            {
                PlayerPerformanceProfile.Performance => new ProfileSettings(
                    qualityLevel: 0,
                    targetFrameRate: 60,
                    lodBias: 0.85f,
                    maximumLodLevel: 1,
                    renderScale: 0.85f,
                    supportsHdr: false,
                    msaaSampleCount: 1,
                    globalTextureMipmapLimit: 1,
                    streamingMipmapsActive: true,
                    streamingMipmapsMemoryBudget: 256f,
                    particleRaycastBudget: 128,
                    shadowDistance: 24f,
                    shadowResolution: EngineShadowResolution.Low,
                    shadowCascadeCount: 1,
                    mainLightShadowmapResolution: 1024,
                    maxAdditionalLightsCount: 1,
                    additionalLightsShadowmapResolution: 1024,
                    treeDistance: 220f,
                    treeBillboardDistance: 32f,
                    treeCrossFadeLength: 4f,
                    treeMaximumFullLodCount: 16,
                    detailObjectDistance: 45f,
                    detailObjectDensity: 0.55f,
                    heightmapPixelError: 9f,
                    basemapDistance: 450f),
                PlayerPerformanceProfile.Quality => new ProfileSettings(
                    qualityLevel: 1,
                    targetFrameRate: 90,
                    lodBias: 1.8f,
                    maximumLodLevel: 0,
                    renderScale: 1f,
                    supportsHdr: true,
                    msaaSampleCount: 2,
                    globalTextureMipmapLimit: 0,
                    streamingMipmapsActive: true,
                    streamingMipmapsMemoryBudget: 512f,
                    particleRaycastBudget: 384,
                    shadowDistance: 55f,
                    shadowResolution: EngineShadowResolution.High,
                    shadowCascadeCount: 2,
                    mainLightShadowmapResolution: 2048,
                    maxAdditionalLightsCount: 4,
                    additionalLightsShadowmapResolution: 2048,
                    treeDistance: 550f,
                    treeBillboardDistance: 70f,
                    treeCrossFadeLength: 8f,
                    treeMaximumFullLodCount: 60,
                    detailObjectDistance: 95f,
                    detailObjectDensity: 1f,
                    heightmapPixelError: 3f,
                    basemapDistance: 1000f),
                _ => new ProfileSettings(
                    qualityLevel: 1,
                    targetFrameRate: 75,
                    lodBias: 1.35f,
                    maximumLodLevel: 0,
                    renderScale: 1f,
                    supportsHdr: true,
                    msaaSampleCount: 1,
                    globalTextureMipmapLimit: 0,
                    streamingMipmapsActive: true,
                    streamingMipmapsMemoryBudget: 384f,
                    particleRaycastBudget: 256,
                    shadowDistance: 38f,
                    shadowResolution: EngineShadowResolution.Medium,
                    shadowCascadeCount: 2,
                    mainLightShadowmapResolution: 2048,
                    maxAdditionalLightsCount: 2,
                    additionalLightsShadowmapResolution: 2048,
                    treeDistance: 360f,
                    treeBillboardDistance: 45f,
                    treeCrossFadeLength: 5f,
                    treeMaximumFullLodCount: 32,
                    detailObjectDistance: 70f,
                    detailObjectDensity: 0.85f,
                    heightmapPixelError: 5f,
                    basemapDistance: 700f)
            };
        }

        private static void ApplyQualitySettings(ProfileSettings settings)
        {
            string[] qualityNames = QualitySettings.names;
            if (qualityNames != null && qualityNames.Length > 0)
            {
                int qualityLevel = Mathf.Clamp(settings.QualityLevel, 0, qualityNames.Length - 1);
                if (QualitySettings.GetQualityLevel() != qualityLevel)
                {
                    QualitySettings.SetQualityLevel(qualityLevel, true);
                }
            }

            QualitySettings.vSyncCount = 0;
            QualitySettings.lodBias = settings.LodBias;
            QualitySettings.maximumLODLevel = settings.MaximumLodLevel;
            QualitySettings.globalTextureMipmapLimit = settings.GlobalTextureMipmapLimit;
            QualitySettings.streamingMipmapsActive = settings.StreamingMipmapsActive;
            QualitySettings.streamingMipmapsMemoryBudget = settings.StreamingMipmapsMemoryBudget;
            QualitySettings.particleRaycastBudget = settings.ParticleRaycastBudget;
            QualitySettings.shadowDistance = settings.ShadowDistance;
            QualitySettings.shadowResolution = settings.ShadowResolution;
            ApplyUniversalRenderPipelineSettings(settings);
            Application.targetFrameRate = settings.TargetFrameRate;
        }

        private static void ApplyUniversalRenderPipelineSettings(ProfileSettings settings)
        {
            RenderPipelineAsset renderPipelineAsset = GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline
                : QualitySettings.renderPipeline;
            if (renderPipelineAsset is not UniversalRenderPipelineAsset urpAsset)
            {
                return;
            }

            urpAsset.renderScale = settings.RenderScale;
            urpAsset.supportsHDR = settings.SupportsHdr;
            urpAsset.supportsCameraOpaqueTexture = false;
            urpAsset.msaaSampleCount = settings.MsaaSampleCount;
            urpAsset.shadowDistance = settings.ShadowDistance;
            urpAsset.shadowCascadeCount = settings.ShadowCascadeCount;
            urpAsset.mainLightShadowmapResolution = settings.MainLightShadowmapResolution;
            urpAsset.maxAdditionalLightsCount = settings.MaxAdditionalLightsCount;
            urpAsset.additionalLightsShadowmapResolution = settings.AdditionalLightsShadowmapResolution;
        }

        private static void ApplyTerrainSettings(ProfileSettings settings)
        {
            Terrain[] terrains = Terrain.activeTerrains;
            if (terrains == null)
            {
                return;
            }

            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null)
                {
                    continue;
                }

                terrain.drawTreesAndFoliage = true;
                terrain.treeDistance = settings.TreeDistance;
                terrain.treeBillboardDistance = settings.TreeBillboardDistance;
                terrain.treeCrossFadeLength = settings.TreeCrossFadeLength;
                terrain.treeMaximumFullLODCount = settings.TreeMaximumFullLodCount;
                terrain.detailObjectDistance = settings.DetailObjectDistance;
                terrain.detailObjectDensity = settings.DetailObjectDensity;
                terrain.heightmapPixelError = settings.HeightmapPixelError;
                terrain.basemapDistance = settings.BasemapDistance;
            }
        }

        private static void ApplyCameraSettings(ProfileSettings settings)
        {
            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null)
                {
                    continue;
                }

                camera.allowHDR = settings.SupportsHdr;
                camera.allowMSAA = settings.MsaaSampleCount > 1;
            }
        }

        private readonly struct ProfileSettings
        {
            public ProfileSettings(
                int qualityLevel,
                int targetFrameRate,
                float lodBias,
                int maximumLodLevel,
                float renderScale,
                bool supportsHdr,
                int msaaSampleCount,
                int globalTextureMipmapLimit,
                bool streamingMipmapsActive,
                float streamingMipmapsMemoryBudget,
                int particleRaycastBudget,
                float shadowDistance,
                EngineShadowResolution shadowResolution,
                int shadowCascadeCount,
                int mainLightShadowmapResolution,
                int maxAdditionalLightsCount,
                int additionalLightsShadowmapResolution,
                float treeDistance,
                float treeBillboardDistance,
                float treeCrossFadeLength,
                int treeMaximumFullLodCount,
                float detailObjectDistance,
                float detailObjectDensity,
                float heightmapPixelError,
                float basemapDistance)
            {
                QualityLevel = qualityLevel;
                TargetFrameRate = targetFrameRate;
                LodBias = lodBias;
                MaximumLodLevel = maximumLodLevel;
                RenderScale = renderScale;
                SupportsHdr = supportsHdr;
                MsaaSampleCount = msaaSampleCount;
                GlobalTextureMipmapLimit = globalTextureMipmapLimit;
                StreamingMipmapsActive = streamingMipmapsActive;
                StreamingMipmapsMemoryBudget = streamingMipmapsMemoryBudget;
                ParticleRaycastBudget = particleRaycastBudget;
                ShadowDistance = shadowDistance;
                ShadowResolution = shadowResolution;
                ShadowCascadeCount = shadowCascadeCount;
                MainLightShadowmapResolution = mainLightShadowmapResolution;
                MaxAdditionalLightsCount = maxAdditionalLightsCount;
                AdditionalLightsShadowmapResolution = additionalLightsShadowmapResolution;
                TreeDistance = treeDistance;
                TreeBillboardDistance = treeBillboardDistance;
                TreeCrossFadeLength = treeCrossFadeLength;
                TreeMaximumFullLodCount = treeMaximumFullLodCount;
                DetailObjectDistance = detailObjectDistance;
                DetailObjectDensity = detailObjectDensity;
                HeightmapPixelError = heightmapPixelError;
                BasemapDistance = basemapDistance;
            }

            public int QualityLevel { get; }
            public int TargetFrameRate { get; }
            public float LodBias { get; }
            public int MaximumLodLevel { get; }
            public float RenderScale { get; }
            public bool SupportsHdr { get; }
            public int MsaaSampleCount { get; }
            public int GlobalTextureMipmapLimit { get; }
            public bool StreamingMipmapsActive { get; }
            public float StreamingMipmapsMemoryBudget { get; }
            public int ParticleRaycastBudget { get; }
            public float ShadowDistance { get; }
            public EngineShadowResolution ShadowResolution { get; }
            public int ShadowCascadeCount { get; }
            public int MainLightShadowmapResolution { get; }
            public int MaxAdditionalLightsCount { get; }
            public int AdditionalLightsShadowmapResolution { get; }
            public float TreeDistance { get; }
            public float TreeBillboardDistance { get; }
            public float TreeCrossFadeLength { get; }
            public int TreeMaximumFullLodCount { get; }
            public float DetailObjectDistance { get; }
            public float DetailObjectDensity { get; }
            public float HeightmapPixelError { get; }
            public float BasemapDistance { get; }
        }
    }
}
