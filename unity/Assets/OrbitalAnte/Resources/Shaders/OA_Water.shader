// The surface of a sea or a pool: see-through, darker and more mirror-like at a glancing angle, with a moving
// glitter where it faces the Sun. Visible from above and from underneath.
Shader "OrbitalAnte/Water"
{
    Properties
    {
        _BaseColor ("Colour (alpha is how see-through)", Color) = (0.12, 0.43, 0.66, 0.8)
        _DeepColor ("Colour at a glancing angle", Color) = (0.04, 0.18, 0.33, 1)
        _Ripple ("Ripple size in metres", Range(0.2, 20)) = 3
        _Glow ("Glows by itself (lava)", Range(0, 2)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _DeepColor;
                half _Ripple;
                half _Glow;
            CBUFFER_END

            float4 _OA_SunPosition;
            half4 _OA_SunColor;
            half4 _OA_Ambient;
            float4 _OA_LampPosition;
            half4 _OA_LampColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float4 positionOSAndFog : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                // Ripples are worked out in the object's own space and size, so they stay put on a turning planet.
                float3 scale = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
                output.positionOSAndFog = float4(input.positionOS.xyz * scale, ComputeFogFactor(output.positionCS.z));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 toEye = _WorldSpaceCameraPos.xyz - input.positionWS;
                float range = max(0.001, length(toEye));
                toEye /= range;

                half3 normal = normalize(input.normalWS);
                // Small moving ripples, fading out with distance so far water does not shimmer.
                float3 p = input.positionOSAndFog.xyz / _Ripple;
                float t = _Time.y;
                half3 wobble = half3(
                    sin(p.y * 1.7 + p.z * 1.3 + t * 1.1) + sin(p.z * 3.1 - t * 1.7) * 0.5,
                    sin(p.z * 1.9 + p.x * 1.1 + t * 0.9) + sin(p.x * 2.7 + t * 1.3) * 0.5,
                    sin(p.x * 1.5 + p.y * 1.2 - t * 1.2) + sin(p.y * 3.3 + t * 1.9) * 0.5);
                normal = normalize(normal + wobble * (0.06 * saturate(1.0 - range / 220.0)));
                // Seen from underneath, treat the underside as the front.
                if (dot(normal, toEye) < 0.0) normal = -normal;

                half grazing = 1.0 - saturate(dot(normal, toEye));
                half fresnel = grazing * grazing * grazing;
                half3 albedo = lerp(_BaseColor.rgb, _DeepColor.rgb, saturate(fresnel * 1.5 + 0.15));

                float3 toSun = normalize(_OA_SunPosition.xyz - input.positionWS);
                half sun = saturate(dot(normal, toSun) * 0.8 + 0.2);
                half3 light = _OA_Ambient.rgb + _OA_SunColor.rgb * sun;

                float3 toLamp = _OA_LampPosition.xyz - input.positionWS;
                float lampDistance = max(0.001, length(toLamp));
                half lamp = saturate(1.0 - lampDistance * _OA_LampPosition.w);
                light += _OA_LampColor.rgb * (lamp * lamp);

                half3 halfway = normalize(toSun + toEye);
                half glitter = pow(saturate(dot(normal, halfway)), 90.0) * (1.0 - _Glow * 0.5);

                half3 color = albedo * light + _OA_SunColor.rgb * glitter + albedo * _Glow;
                color = MixFog(color, input.positionOSAndFog.w);
                half alpha = saturate(lerp(_BaseColor.a, 1.0, fresnel) + glitter);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
