// The thin glowing rim of air round a planet seen from space. Drawn as a slightly larger ball that only adds
// light: brightest along the planet's edge, fading to nothing at its own edge and toward the middle of the disc,
// and only on the sunlit side.
Shader "OrbitalAnte/Atmosphere"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (0.4, 0.6, 1, 1)
        _Strength ("Strength", Range(0, 2)) = 0.6
        _Limb ("Where the planet's edge falls (0 to 1)", Range(0.05, 0.98)) = 0.76
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Strength;
                half _Limb;
            CBUFFER_END

            #include "Assets/OrbitalAnte/Resources/Shaders/OA_Look.hlsl"

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
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                float3 toEye = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                float3 toSun = normalize(_OA_SunPosition.xyz - input.positionWS);
                // 0 at the middle of the disc, 1 at the very edge of this ball.
                half grazing = 1.0 - saturate(dot(normal, toEye));
                half inside = saturate(grazing / _Limb);
                half outside = saturate((1.0 - grazing) / max(0.02, 1.0 - _Limb));
                half rim = grazing < _Limb ? inside * inside * inside : outside * outside;
                half facing = dot(normal, toSun);
                half day = saturate(facing * 1.6 + 0.35);
                // Along the line between day and night the air turns the colour of a sunset.
                half dusk = saturate(1.0 - abs(facing + 0.05) * 3.2);
                half brightness = dot(_BaseColor.rgb, half3(0.3, 0.5, 0.2));
                half3 air = lerp(_BaseColor.rgb, half3(1.0, 0.52, 0.3) * (brightness * 1.5), dusk * 0.55);
                // Looking toward the Sun through the rim, the air lights up.
                half through = saturate(dot(-toEye, toSun));
                through = through * through;
                through = through * through;
                half3 color = air * (rim * day * _Strength * (1.0 + 1.5 * through) * (1.0 - _OA_Veil.a));
                return half4(color, 0.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
