// A whole planet, moon, the Sun, a ring or an asteroid seen from a distance. Lit from where the Sun actually
// is, so every body shows the correct phase (full, half, crescent) wherever it is in the sky. Instead of fog
// it uses the "sky veil": when you are inside an atmosphere, far-off things fade into the colour of the sky.
Shader "OrbitalAnte/Body"
{
    Properties
    {
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap ("Picture", 2D) = "white" {}
        _Emission ("Shines by itself (the Sun)", Range(0, 1)) = 0
        _Veil ("How much the sky hides it", Range(0, 1)) = 1
        _TwoSided ("Lit the same from both sides (rings)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _BaseMap_ST;
            half _Emission;
            half _Veil;
            half _TwoSided;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        float4 _OA_SunPosition;
        half4 _OA_SunRaw;
        half4 _OA_SpaceAmbient;
        half4 _OA_Veil;   // rgb is the sky's colour, a is how thick it is

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
        };

        Varyings vert(Attributes input)
        {
            Varyings output;
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionWS = positionWS;
            output.positionCS = TransformWorldToHClip(positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            half4 frag(Varyings input) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                // Stars carry no surface direction at all, so guard against dividing by nothing.
                half3 normal = input.normalWS / max(0.0001, length(input.normalWS));
                float3 toSun = normalize(_OA_SunPosition.xyz - input.positionWS);
                half facing = dot(normal, toSun);
                facing = lerp(facing, abs(facing) * 0.8 + 0.2, _TwoSided);
                // A slightly soft edge to the shadow, as a real atmosphere or rough ground gives.
                half sun = saturate(facing * 1.1 + 0.06);
                half3 shaded = albedo * (_OA_SpaceAmbient.rgb + _OA_SunRaw.rgb * sun);
                half3 color = _Emission > 0.99 ? albedo : lerp(shaded, albedo, _Emission);
                color = lerp(color, _OA_Veil.rgb, saturate(_OA_Veil.a * _Veil));
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragDepth

            half4 fragDepth(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragNormals

            half4 fragNormals(Varyings input) : SV_Target
            {
                return half4(normalize(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
