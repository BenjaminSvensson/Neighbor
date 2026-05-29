using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Neighbor.Rendering
{
    /// <summary>
    /// URP render feature that applies the dream leakage post-process shader to game cameras.
    /// </summary>
    public sealed class DreamLeakageRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

            [Range(0f, 1f)] public float intensity = 0.55f;
            [Range(0f, 1f)] public float lightLeak = 0.45f;
            [Range(0f, 1f)] public float spectralHalo = 0.38f;
            [Range(0f, 1f)] public float lensBreathing = 0.2f;
            [Range(0f, 1f)] public float dreamTint = 0.3f;
            [Range(0f, 1f)] public float pulse = 0.25f;
        }

        [SerializeField] Settings settings = new();
        [SerializeField] Shader shader;

        Material material;
        DreamLeakagePass pass;

        public override void Create()
        {
            pass ??= new DreamLeakagePass();
            pass.renderPassEvent = settings.renderPassEvent;

            if (shader == null)
                shader = Shader.Find("Hidden/Neighbor/DreamLeakage");

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

        sealed class DreamLeakagePass : ScriptableRenderPass
        {
            const string PassName = "Dream Leakage";
            static readonly int IntensityId = Shader.PropertyToID("_Intensity");
            static readonly int TimeId = Shader.PropertyToID("_EffectTime");
            static readonly int LightLeakId = Shader.PropertyToID("_LightLeak");
            static readonly int SpectralHaloId = Shader.PropertyToID("_SpectralHalo");
            static readonly int LensBreathingId = Shader.PropertyToID("_LensBreathing");
            static readonly int DreamTintId = Shader.PropertyToID("_DreamTint");
            static readonly int PulseId = Shader.PropertyToID("_Pulse");

            Material material;

            public void Setup(Material sourceMaterial, Settings settings)
            {
                material = sourceMaterial;
                requiresIntermediateTexture = true;

                material.SetFloat(IntensityId, settings.intensity);
                material.SetFloat(TimeId, Time.time);
                material.SetFloat(LightLeakId, settings.lightLeak);
                material.SetFloat(SpectralHaloId, settings.spectralHalo);
                material.SetFloat(LensBreathingId, settings.lensBreathing);
                material.SetFloat(DreamTintId, settings.dreamTint);
                material.SetFloat(PulseId, settings.pulse);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                TextureHandle source = resourceData.activeColorTexture;
                // Blit through an intermediate texture so later RenderGraph passes receive
                // the processed camera color.
                TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
                destinationDesc.name = "CameraColor-DreamLeakage";
                destinationDesc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

                RenderGraphUtils.BlitMaterialParameters parameters = new(source, destination, material, 0);
                renderGraph.AddBlitPass(parameters, PassName);
                resourceData.cameraColor = destination;
            }
        }
    }
}
