Shader "Hidden/Neighbor/SeventiesFilm"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "SeventiesFilm"
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
            float _Warmth;
            float _FadedContrast;
            float _GreenShift;
            float _Grain;
            float _ColorBleed;

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            half3 ApplyFilmCurve(half3 color)
            {
                color = saturate(color);

                half3 lifted = color * (1.0 - _FadedContrast * 0.22) + _FadedContrast * 0.055;
                half3 softShoulder = 1.0 - exp2(-lifted * (1.22 - _FadedContrast * 0.2));
                color = lerp(color, softShoulder, _FadedContrast);

                half luminance = dot(color, half3(0.299, 0.587, 0.114));
                half3 shadows = half3(0.03, 0.08, 0.065) * _GreenShift * (1.0 - luminance);
                half3 highlights = half3(0.14, 0.09, -0.035) * _Warmth * luminance;
                color += shadows + highlights;

                half3 sepia = half3(
                    dot(color, half3(0.393, 0.769, 0.189)),
                    dot(color, half3(0.349, 0.686, 0.168)),
                    dot(color, half3(0.272, 0.534, 0.131)));
                color = lerp(color, sepia, _Warmth * 0.18);

                half sat = 0.9 - _FadedContrast * 0.18 + _Warmth * 0.05;
                color = lerp(luminance.xxx, color, sat);
                return saturate(color);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                half4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float bleedOffset = _ColorBleed * 0.0035;
                half red = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(bleedOffset, 0.0)).r;
                half blue = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(bleedOffset * 0.75, 0.0)).b;
                half3 color = half3(red, original.g, blue);

                color = ApplyFilmCurve(color);

                float2 grainPixel = input.positionCS.xy + floor(_EffectTime * 16.0);
                float grain = Hash12(grainPixel) - 0.5;
                half luminance = dot(color, half3(0.299, 0.587, 0.114));
                color += grain * _Grain * lerp(1.25, 0.55, luminance);

                color = saturate(color);
                return half4(lerp(original.rgb, color, _Intensity), original.a);
            }
            ENDHLSL
        }
    }
}
