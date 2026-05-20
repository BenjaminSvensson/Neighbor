Shader "Hidden/Neighbor/RetroPixelation"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "RetroPixelation"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Intensity;
            float _PixelSize;
            float _ColorLevels;
            float _PaletteStrength;
            float _DitherStrength;
            float _GridStrength;

            float Bayer4(float2 pixel)
            {
                int x = (int)pixel.x & 3;
                int y = (int)pixel.y & 3;
                int index = x + y * 4;

                if (index == 0) return 0.0 / 16.0;
                if (index == 1) return 8.0 / 16.0;
                if (index == 2) return 2.0 / 16.0;
                if (index == 3) return 10.0 / 16.0;
                if (index == 4) return 12.0 / 16.0;
                if (index == 5) return 4.0 / 16.0;
                if (index == 6) return 14.0 / 16.0;
                if (index == 7) return 6.0 / 16.0;
                if (index == 8) return 3.0 / 16.0;
                if (index == 9) return 11.0 / 16.0;
                if (index == 10) return 1.0 / 16.0;
                if (index == 11) return 9.0 / 16.0;
                if (index == 12) return 15.0 / 16.0;
                if (index == 13) return 7.0 / 16.0;
                if (index == 14) return 13.0 / 16.0;
                return 5.0 / 16.0;
            }

            half3 QuantizeColor(half3 color, float2 pixelCoord)
            {
                float levels = max(2.0, _ColorLevels);
                float dither = (Bayer4(pixelCoord) - 0.5) * _DitherStrength;
                half3 quantized = floor(saturate(color + dither) * (levels - 1.0) + 0.5) / (levels - 1.0);
                return lerp(color, quantized, _PaletteStrength);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenSize = max(_BlitTexture_TexelSize.zw, 1.0.xx);
                float2 sourcePixel = input.texcoord * screenSize;
                float pixelSize = max(1.0, _PixelSize);
                float2 cell = floor(sourcePixel / pixelSize);
                float2 cellCenter = (cell + 0.5) * pixelSize;
                float2 pixelUv = saturate(cellCenter / screenSize);

                half4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord);
                half4 pixelated = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, pixelUv);
                pixelated.rgb = QuantizeColor(pixelated.rgb, cell);

                float2 cellUv = frac(sourcePixel / pixelSize);
                float2 edge = smoothstep(0.0, 0.08, cellUv) * smoothstep(0.0, 0.08, 1.0 - cellUv);
                float grid = edge.x * edge.y;
                pixelated.rgb *= lerp(1.0 - _GridStrength, 1.0, grid);

                return half4(lerp(original.rgb, pixelated.rgb, _Intensity), original.a);
            }
            ENDHLSL
        }
    }
}
