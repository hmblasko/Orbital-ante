// Ground, rocks, trees and cave walls. Colour comes from the colours stored in the mesh itself (no textures),
// optionally blended into the body's far-away picture at a distance so the two never disagree.
// Light comes from where the Sun actually is, plus the craft's lamp, so night, caves and deep water are dark.
Shader "OrbitalAnte/Terrain"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _FarMap ("Far-away picture", 2D) = "white" {}
        _FarBlend ("Far blend: start, end, on (0 or 1), picture width", Vector) = (150, 600, 0, 1024)
        _SunAmount ("Sunlight reaching it", Range(0, 1)) = 1
        _Glow ("Glows by itself", Range(0, 2)) = 0
        _Outdoors ("Lit by sky, lamp and fog (0 for baked interiors)", Range(0, 1)) = 1
        _VertexGlow ("Glow where the mesh says so (strength)", Range(0, 8)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _FarMap_ST;
            float4 _FarBlend;
            half _SunAmount;
            half _Glow;
            half _Outdoors;
            half _VertexGlow;
        CBUFFER_END

        TEXTURE2D(_FarMap);
        SAMPLER(sampler_FarMap);

        // Set once a frame by the game (PlanetSky).
        float4 _OA_SunPosition;
        half4 _OA_SunColor;
        half4 _OA_Ambient;
        float4 _OA_LampPosition;   // w is one over the lamp's reach
        half4 _OA_LampColor;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            half4 color : COLOR;
            float3 direction : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half4 color : COLOR;
            float4 directionAndFog : TEXCOORD2;
        };

        Varyings vert(Attributes input)
        {
            Varyings output;
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionWS = positionWS;
            output.positionCS = TransformWorldToHClip(positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.color = input.color;
            output.directionAndFog = float4(input.direction, ComputeFogFactor(output.positionCS.z));
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
            #pragma multi_compile_fog

            half4 frag(Varyings input) : SV_Target
            {
                half3 albedo = input.color.rgb * _BaseColor.rgb;
                float3 toEye = _WorldSpaceCameraPos.xyz - input.positionWS;
                float range = length(toEye);

                if (_FarBlend.z > 0.5)
                {
                    float3 d = normalize(input.directionAndFog.xyz);
                    float u = atan2(d.z, d.x) * 0.15915494;
                    float v = 1.0 - acos(clamp(d.y, -1.0, 1.0)) * 0.31830989;
                    // Work out the blur level from the direction, not from u, so the line where u wraps round
                    // from 1 back to 0 does not show as a seam.
                    float spread = max(length(ddx(d)), length(ddy(d)));
                    float lod = max(0.0, log2(max(1e-6, spread * _FarBlend.w * 0.15915494)));
                    half3 farColor = SAMPLE_TEXTURE2D_LOD(_FarMap, sampler_FarMap, float2(u, v), lod).rgb;
                    float blend = saturate((range - _FarBlend.x) / max(1.0, _FarBlend.y - _FarBlend.x));
                    albedo = lerp(albedo, farColor, blend);
                }

                half3 normal = normalize(input.normalWS);
                float3 toSun = normalize(_OA_SunPosition.xyz - input.positionWS);
                half sun = saturate(dot(normal, toSun)) * _SunAmount;
                half3 light = _OA_Ambient.rgb + _OA_SunColor.rgb * sun;

                float3 toLamp = _OA_LampPosition.xyz - input.positionWS;
                float lampDistance = max(0.001, length(toLamp));
                half lamp = saturate(1.0 - lampDistance * _OA_LampPosition.w);
                lamp = lamp * lamp * saturate(dot(normal, toLamp / lampDistance) * 0.7 + 0.3);
                light += _OA_LampColor.rgb * lamp;

                // Baked interiors carry their own light in the mesh colours: no sky, lamp or fog on top.
                // Lamps and lit windows on built things: the mesh marks them (in the colour's fourth number).
                half3 color = albedo * light * _Outdoors + albedo * (_Glow + _VertexGlow * (1.0 - input.color.a));
                color = lerp(color, MixFog(color, input.directionAndFog.w), _Outdoors);
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
