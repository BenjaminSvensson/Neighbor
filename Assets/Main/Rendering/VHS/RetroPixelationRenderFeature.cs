using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Neighbor.Rendering
{
    public sealed class RetroPixelationRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

            [Range(0f, 1f)] public float intensity = 1f;
            [Range(1f, 24f)] public float pixelSize = 5f;
            [Range(2f, 32f)] public float colorLevels = 10f;
            [Range(0f, 1f)] public float paletteStrength = 0.35f;
            [Range(0f, 1f)] public float ditherStrength = 0.18f;
            [Range(0f, 1f)] public float gridStrength = 0.12f;
        }

        [SerializeField] Settings settings = new();
        [SerializeField] Shader shader;

        Material material;
        RetroPixelationPass pass;

        public override void Create()
        {
            pass ??= new RetroPixelationPass();
            pass.renderPassEvent = settings.renderPassEvent;

            if (shader == null)
                shader = Shader.Find("Hidden/Neighbor/RetroPixelation");

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

        sealed class RetroPixelationPass : ScriptableRenderPass
        {
            const string PassName = "Retro Pixelation";
            static readonly int IntensityId = Shader.PropertyToID("_Intensity");
            static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");
            static readonly int ColorLevelsId = Shader.PropertyToID("_ColorLevels");
            static readonly int PaletteStrengthId = Shader.PropertyToID("_PaletteStrength");
            static readonly int DitherStrengthId = Shader.PropertyToID("_DitherStrength");
            static readonly int GridStrengthId = Shader.PropertyToID("_GridStrength");

            Material material;

            public void Setup(Material sourceMaterial, Settings settings)
            {
                material = sourceMaterial;
                requiresIntermediateTexture = true;

                material.SetFloat(IntensityId, settings.intensity);
                material.SetFloat(PixelSizeId, settings.pixelSize);
                material.SetFloat(ColorLevelsId, settings.colorLevels);
                material.SetFloat(PaletteStrengthId, settings.paletteStrength);
                material.SetFloat(DitherStrengthId, settings.ditherStrength);
                material.SetFloat(GridStrengthId, settings.gridStrength);
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
                destinationDesc.name = "CameraColor-RetroPixelation";
                destinationDesc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

                RenderGraphUtils.BlitMaterialParameters parameters = new(source, destination, material, 0);
                renderGraph.AddBlitPass(parameters, PassName);
                resourceData.cameraColor = destination;
            }
        }
    }
}
