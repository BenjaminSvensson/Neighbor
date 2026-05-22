using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Neighbor.Rendering
{
    public sealed class SeventiesFilmRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

            [Range(0f, 1f)] public float intensity = 0.75f;
            [Range(0f, 1f)] public float warmth = 0.45f;
            [Range(0f, 1f)] public float fadedContrast = 0.35f;
            [Range(0f, 1f)] public float greenShift = 0.16f;
            [Range(0f, 1f)] public float grain = 0.12f;
            [Range(0f, 1f)] public float colorBleed = 0.18f;
        }

        [SerializeField] Settings settings = new();
        [SerializeField] Shader shader;

        Material material;
        SeventiesFilmPass pass;

        public override void Create()
        {
            pass ??= new SeventiesFilmPass();
            pass.renderPassEvent = settings.renderPassEvent;

            if (shader == null)
                shader = Shader.Find("Hidden/Neighbor/SeventiesFilm");

            if (material == null && shader != null)
                material = CoreUtils.CreateEngineMaterial(shader);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null ||
                renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.isSceneViewCamera)
            {
                return;
            }

            pass.renderPassEvent = settings.renderPassEvent;
            pass.Setup(material, settings);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
        }

        sealed class SeventiesFilmPass : ScriptableRenderPass
        {
            const string PassName = "Seventies Film";
            static readonly int IntensityId = Shader.PropertyToID("_Intensity");
            static readonly int TimeId = Shader.PropertyToID("_EffectTime");
            static readonly int WarmthId = Shader.PropertyToID("_Warmth");
            static readonly int FadedContrastId = Shader.PropertyToID("_FadedContrast");
            static readonly int GreenShiftId = Shader.PropertyToID("_GreenShift");
            static readonly int GrainId = Shader.PropertyToID("_Grain");
            static readonly int ColorBleedId = Shader.PropertyToID("_ColorBleed");

            Material material;

            public void Setup(Material sourceMaterial, Settings settings)
            {
                material = sourceMaterial;
                requiresIntermediateTexture = true;

                material.SetFloat(IntensityId, settings.intensity);
                material.SetFloat(TimeId, Time.time);
                material.SetFloat(WarmthId, settings.warmth);
                material.SetFloat(FadedContrastId, settings.fadedContrast);
                material.SetFloat(GreenShiftId, settings.greenShift);
                material.SetFloat(GrainId, settings.grain);
                material.SetFloat(ColorBleedId, settings.colorBleed);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                TextureHandle source = resourceData.activeColorTexture;
                TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
                destinationDesc.name = "CameraColor-SeventiesFilm";
                destinationDesc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

                RenderGraphUtils.BlitMaterialParameters parameters = new(source, destination, material, 0);
                renderGraph.AddBlitPass(parameters, PassName);
                resourceData.cameraColor = destination;
            }
        }
    }
}
