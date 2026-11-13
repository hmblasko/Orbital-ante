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
        _Boost ("Extra brightness when it shines by itself, so it blooms", Range(1, 12)) = 1
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
            half _Boost;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        #include "Assets/OrbitalAnte/Resources/Shaders/OA_Look.hlsl"

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
                // Light wraps a little way round the edge of the shadow, and that edge glows warm, the way a
                // real atmosphere or rough ground softens the line between day and night.
                half wrap = saturate((facing + 0.22) / 1.22);
                half sun = lerp(wrap, smoothstep(0.03, 0.5, wrap), 0.5);
                half edge = saturate(1.0 - abs(wrap - 0.16) * 6.0);
                half3 sunlight = _OA_SunRaw.rgb * (sun + half3(1.0, 0.36, 0.14) * (edge * 0.16));
                half3 shaded = albedo * (_OA_SpaceAmbient.rgb + sunlight);
                half3 color = _Emission > 0.99 ? albedo * _Boost : lerp(shaded, albedo, _Emission);
                // Inside an atmosphere, far-off things fade into the sky that is behind them.
                float3 view = normalize(input.positionWS - _WorldSpaceCameraPos.xyz);
                color = lerp(color, OA_SkyColor(view), saturate(_OA_Veil.a * _Veil));
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
