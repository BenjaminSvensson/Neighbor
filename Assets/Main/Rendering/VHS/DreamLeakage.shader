Shader "Hidden/Neighbor/DreamLeakage"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "DreamLeakage"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Intensity;
            float _EffectTime;
            float _LightLeak;
            float _SpectralHalo;
            float _LensBreathing;
            float _DreamTint;
            float _Pulse;

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            half Luma(half3 color)
            {
                return dot(color, half3(0.299, 0.587, 0.114));
            }

            float SoftBlob(float2 uv, float2 center, float2 scale)
            {
                float2 p = (uv - center) / scale;
                return exp(-dot(p, p));
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 centered = uv * 2.0 - 1.0;
                float time = _EffectTime;
                float pulse = 1.0 + sin(time * 1.35) * 0.015 * _Pulse;
                float radius = dot(centered, centered);
                float2 warpedUv = centered * (1.0 + radius * 0.025 * _LensBreathing) * pulse;
                warpedUv = warpedUv * 0.5 + 0.5;

                half4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half4 baseColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(warpedUv));

                float2 texel = _BlitTexture_TexelSize.xy;
                half lumaCenter = Luma(baseColor.rgb);
                half lumaRight = Luma(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(warpedUv + float2(texel.x * 2.0, 0.0))).rgb);
                half lumaUp = Luma(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(warpedUv + float2(0.0, texel.y * 2.0))).rgb);
                float edge = saturate((abs(lumaCenter - lumaRight) + abs(lumaCenter - lumaUp)) * 6.0);

                float haloOffset = (2.0 + radius * 7.0) * _SpectralHalo;
                half3 halo;
                halo.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(warpedUv + centered * texel * haloOffset)).r;
                halo.g = baseColor.g;
                halo.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(warpedUv - centered * texel * haloOffset * 1.4)).b;

                half3 color = lerp(baseColor.rgb, halo, edge * _SpectralHalo);

                float leftLeak = SoftBlob(uv, float2(-0.12 + sin(time * 0.23) * 0.08, 0.22 + sin(time * 0.31) * 0.08), float2(0.42, 0.85));
                float lowerLeak = SoftBlob(uv, float2(0.82 + sin(time * 0.19) * 0.06, -0.08), float2(0.55, 0.34));
                float ribbon = smoothstep(0.72, 0.08, abs(uv.x - (0.5 + sin(uv.y * 5.0 + time * 0.55) * 0.18)));
                float shimmer = 0.75 + 0.25 * Hash12(float2(floor(uv.y * 28.0), floor(time * 8.0)));
                half3 leakColor = half3(1.0, 0.43, 0.16) * leftLeak;
                leakColor += half3(0.1, 0.74, 0.78) * lowerLeak;
                leakColor += half3(0.9, 0.08, 0.32) * ribbon * 0.18;
                color += leakColor * _LightLeak * shimmer;

                half3 dreamTint = half3(0.05, 0.12, 0.15) * (1.0 - lumaCenter) + half3(0.13, 0.07, 0.0) * lumaCenter;
                color = lerp(color, color + dreamTint, _DreamTint);

                float vignette = 1.0 - smoothstep(0.42, 1.5, radius);
                color *= lerp(0.92, 1.08, vignette);

                return half4(lerp(original.rgb, saturate(color), _Intensity), original.a);
            }
            ENDHLSL
        }
    }
}
