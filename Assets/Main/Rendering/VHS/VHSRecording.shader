Shader "Hidden/Neighbor/VHSRecording"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "VHSRecording"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            SAMPLER(sampler_BlitTexture);

            float _Intensity;
            float _EffectTime;
            float _ScanlineIntensity;
            float _NoiseIntensity;
            float _TrackingNoise;
            float _ChromaticAberration;
            float _HorizontalJitter;
            float _VerticalRoll;
            float _TapeBleed;
            float _Desaturation;
            float _Vignette;

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float BandNoise(float y, float speed, float scale)
            {
                float band = floor((y + _EffectTime * speed) * scale);
                return Hash12(float2(band, floor(_EffectTime * 24.0))) * 2.0 - 1.0;
            }

            half4 SampleTape(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, saturate(uv));
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 centered = uv * 2.0 - 1.0;
                float time = _EffectTime;

                float roll = sin(time * 1.7) * 0.006 * _VerticalRoll;
                uv.y = frac(uv.y + roll);

                float broadWarp = BandNoise(uv.y, 0.22, 18.0) * 0.008 * _HorizontalJitter;
                float fineWarp = sin((uv.y * 420.0) + time * 22.0) * 0.0015 * _HorizontalJitter;
                float headSwitch = smoothstep(0.78, 1.0, uv.y) * BandNoise(uv.y, 1.8, 42.0) * 0.02 * _TrackingNoise;
                uv.x += broadWarp + fineWarp + headSwitch;

                float chroma = _ChromaticAberration * _Intensity * (1.0 + abs(broadWarp) * 40.0);
                float2 redUv = uv + float2(0.0025 * chroma, 0.0);
                float2 blueUv = uv - float2(0.0035 * chroma, 0.0);

                half4 baseColor = SampleTape(uv);
                half red = SampleTape(redUv).r;
                half blue = SampleTape(blueUv).b;
                half3 color = half3(red, baseColor.g, blue);

                half3 bleed = SampleTape(uv - float2(0.004 + 0.008 * _TapeBleed, 0.0)).rgb;
                color = lerp(color, half3(max(color.r, bleed.r), color.g * 0.96 + bleed.g * 0.04, color.b), _TapeBleed * _Intensity);

                float scanline = sin(input.positionCS.y * 3.14159);
                float scanMask = 1.0 - (0.5 + 0.5 * scanline) * _ScanlineIntensity * _Intensity;
                float lineFlicker = 1.0 + BandNoise(uv.y, 4.0, 140.0) * 0.06 * _Intensity;
                color *= scanMask * lineFlicker;

                float staticNoise = Hash12(input.positionCS.xy + float2(time * 91.7, time * 37.2)) - 0.5;
                float horizontalSnow = step(0.985 - _TrackingNoise * 0.03, Hash12(float2(floor(uv.y * 260.0), floor(time * 18.0))));
                color += (staticNoise * _NoiseIntensity + horizontalSnow * _TrackingNoise * 0.45) * _Intensity;

                half luma = dot(color, half3(0.299, 0.587, 0.114));
                color = lerp(color, luma.xxx, _Desaturation * _Intensity);

                float vignette = 1.0 - smoothstep(0.28, 1.45, dot(centered, centered));
                color *= lerp(1.0, vignette, _Vignette * _Intensity);

                color = saturate(color);
                return half4(lerp(baseColor.rgb, color, _Intensity), baseColor.a);
            }
            ENDHLSL
        }
    }
}
