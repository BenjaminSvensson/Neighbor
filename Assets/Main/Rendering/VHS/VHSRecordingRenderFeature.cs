using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Neighbor.Rendering
{
    /// <summary>
    /// URP render feature that applies the VHS recording post-process shader to game cameras.
    /// </summary>
    public sealed class VHSRecordingRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

            [Range(0f, 1f)] public float intensity = 0.6f;
            [Range(0f, 1f)] public float scanlineIntensity = 0.22f;
            [Range(0f, 1f)] public float noiseIntensity = 0.1f;
            [Range(0f, 1f)] public float trackingNoise = 0.14f;
            [Range(0f, 1f)] public float chromaticAberration = 0.25f;
            [Range(0f, 1f)] public float horizontalJitter = 0.12f;
            [Range(0f, 1f)] public float verticalRoll = 0.04f;
            [Range(0f, 1f)] public float tapeBleed = 0.12f;
            [Range(0f, 1f)] public float desaturation = 0.18f;
            [Range(0f, 1f)] public float vignette = 0.12f;
        }

        [SerializeField] Settings settings = new();
        [SerializeField] Shader shader;

        Material material;
        VHSRecordingPass pass;

        public override void Create()
        {
            pass ??= new VHSRecordingPass();
            pass.renderPassEvent = settings.renderPassEvent;

            if (shader == null)
                shader = Shader.Find("Hidden/Neighbor/VHSRecording");

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

        sealed class VHSRecordingPass : ScriptableRenderPass
        {
            const string PassName = "VHS Recording";
            static readonly int IntensityId = Shader.PropertyToID("_Intensity");
            static readonly int TimeId = Shader.PropertyToID("_EffectTime");
            static readonly int ScanlineIntensityId = Shader.PropertyToID("_ScanlineIntensity");
            static readonly int NoiseIntensityId = Shader.PropertyToID("_NoiseIntensity");
            static readonly int TrackingNoiseId = Shader.PropertyToID("_TrackingNoise");
            static readonly int ChromaticAberrationId = Shader.PropertyToID("_ChromaticAberration");
            static readonly int HorizontalJitterId = Shader.PropertyToID("_HorizontalJitter");
            static readonly int VerticalRollId = Shader.PropertyToID("_VerticalRoll");
            static readonly int TapeBleedId = Shader.PropertyToID("_TapeBleed");
            static readonly int DesaturationId = Shader.PropertyToID("_Desaturation");
            static readonly int VignetteId = Shader.PropertyToID("_Vignette");

            Material material;

            public void Setup(Material sourceMaterial, Settings settings)
            {
                material = sourceMaterial;
                requiresIntermediateTexture = true;

                material.SetFloat(IntensityId, settings.intensity);
                material.SetFloat(TimeId, Time.time);
                material.SetFloat(ScanlineIntensityId, settings.scanlineIntensity);
                material.SetFloat(NoiseIntensityId, settings.noiseIntensity);
                material.SetFloat(TrackingNoiseId, settings.trackingNoise);
                material.SetFloat(ChromaticAberrationId, settings.chromaticAberration);
                material.SetFloat(HorizontalJitterId, settings.horizontalJitter);
                material.SetFloat(VerticalRollId, settings.verticalRoll);
                material.SetFloat(TapeBleedId, settings.tapeBleed);
                material.SetFloat(DesaturationId, settings.desaturation);
                material.SetFloat(VignetteId, settings.vignette);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                TextureHandle source = resourceData.activeColorTexture;
                // RenderGraph post-processing writes into a new color texture, then promotes
                // it to cameraColor for any later passes in the renderer.
                TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
                destinationDesc.name = "CameraColor-VHSRecording";
                destinationDesc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

                RenderGraphUtils.BlitMaterialParameters parameters = new(source, destination, material, 0);
                renderGraph.AddBlitPass(parameters, PassName);
                resourceData.cameraColor = destination;
            }
        }
    }
}
