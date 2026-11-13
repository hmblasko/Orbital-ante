// Ground, rocks, trees and cave walls. Colour comes from the colours stored in the mesh itself (no textures),
// optionally blended into the body's far-away picture at a distance so the two never disagree.
// Light comes from where the Sun actually is, plus the craft's lamp, so night, caves and deep water are dark.
// The soft lighting, the painted break-up of colour and the haze are the game's shared look (OA_Look.hlsl).
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
        _Paint ("Painted break-up of colour", Range(0, 1)) = 1
        _PaintScale ("Paint patch size: 1 for ground, larger for small things", Range(1, 63)) = 1
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
            half _Paint;
            half _PaintScale;
        CBUFFER_END

        TEXTURE2D(_FarMap);
        SAMPLER(sampler_FarMap);

        #include "Assets/OrbitalAnte/Resources/Shaders/OA_Look.hlsl"

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
            float3 direction : TEXCOORD2;
            float3 positionOS : TEXCOORD3;
            float3 upWS : TEXCOORD4;
        };

        Varyings vert(Attributes input)
        {
            Varyings output;
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionWS = positionWS;
            output.positionCS = TransformWorldToHClip(positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.color = input.color;
            output.direction = input.direction;
            // Painted in the object's own space and size, so the paint stays put on a turning planet.
            float3 scale = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
            output.positionOS = input.positionOS.xyz * scale * _PaintScale;
            // Ground knows which way is up at every corner (planets are small, so "up" changes quickly). Rocks,
            // the ship and anything else without that information use "up" where the viewer is.
            float3 upWS = TransformObjectToWorldDir(input.direction, false);
            output.upWS = dot(input.direction, input.direction) > 0.25 ? upWS : _OA_SkyUp.xyz;
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
                half3 albedo = input.color.rgb * _BaseColor.rgb;
                // Baked interiors carry their own light in the mesh colours: no paint, sky, lamp or haze on top.
                if (_Outdoors < 0.5) return half4(albedo * _Glow, 1.0);

                float3 toEye = _WorldSpaceCameraPos.xyz - input.positionWS;
                float range = length(toEye);

                // Far ground takes its colour from the body's far-away picture, so the two never disagree.
                half blend = 0.0;
                half3 farColor = albedo;
                if (_FarBlend.z > 0.5)
                {
                    float3 d = normalize(input.direction);
                    float u = atan2(d.z, d.x) * 0.15915494;
                    float v = 1.0 - acos(clamp(d.y, -1.0, 1.0)) * 0.31830989;
                    // Work out the blur level from the direction, not from u, so the line where u wraps round
                    // from 1 back to 0 does not show as a seam.
                    float spread = max(length(ddx(d)), length(ddy(d)));
                    float lod = max(0.0, log2(max(1e-6, spread * _FarBlend.w * 0.15915494)));
                    farColor = SAMPLE_TEXTURE2D_LOD(_FarMap, sampler_FarMap, float2(u, v), lod).rgb;
                    blend = saturate((range - _FarBlend.x) / max(1.0, _FarBlend.y - _FarBlend.x));
                }
                // Paint only where the ground's own colour still shows.
                // Smaller patches (things held in the hand) fade out sooner, so they never shimmer at a distance.
                albedo = OA_Paint(albedo, input.positionOS, range * _PaintScale, _OA_Haze.y * _Paint * (1.0 - blend));
                albedo = lerp(albedo, farColor, blend);

                half3 normal = normalize(input.normalWS);
                float3 up = input.upWS / max(0.0001, length(input.upWS));
                half3 light = OA_Light(normal, input.positionWS, up, _SunAmount);
                half3 color = albedo * light + albedo * _Glow;
                color = OA_ApplyHaze(color, input.positionWS);
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
