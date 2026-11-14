// The game's own models (enemies, guns, the ship's fittings): colour from the colours stored in the mesh, lit
// by the shared look (OA_Look.hlsl) exactly as the ground is, plus what the ground does not have: shine.
// A model made for the realistic look says, at every corner, how smooth its surface is and whether it is bare
// metal (the mesh's second pair of extra numbers: x how smooth, y metal; the first pair is left alone, because
// the ground's shader reads it as "up"). Smooth surfaces show the Sun's highlight
// and the sky. A model that says nothing is chalk all over and looks as it did before this shader existed.
Shader "OrbitalAnte/Model"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _SunAmount ("Sunlight reaching it", Range(0, 1)) = 1
        _Glow ("Glows by itself", Range(0, 2)) = 0
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
            half _SunAmount;
            half _Glow;
            half _Paint;
            half _PaintScale;
        CBUFFER_END

        #include "Assets/OrbitalAnte/Resources/Shaders/OA_Look.hlsl"

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            half4 color : COLOR;
            float2 finish : TEXCOORD1;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half4 color : COLOR;
            half2 finish : TEXCOORD2;
            float3 positionOS : TEXCOORD3;
        };

        Varyings vert(Attributes input)
        {
            Varyings output;
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionWS = positionWS;
            output.positionCS = TransformWorldToHClip(positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.color = input.color;
            output.finish = saturate(input.finish);
            // Painted in the object's own space and size, so the paint stays put on a thing that moves.
            float3 scale = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
            output.positionOS = input.positionOS.xyz * scale * _PaintScale;
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
                float range = length(_WorldSpaceCameraPos.xyz - input.positionWS);
                albedo = OA_Paint(albedo, input.positionOS, range * _PaintScale, _OA_Haze.y * _Paint);

                half3 normal = normalize(input.normalWS);
                float3 up = _OA_SkyUp.xyz;
                half gloss = input.finish.x;
                half metal = input.finish.y;

                half3 light = OA_Light(normal, input.positionWS, up, _SunAmount);
                // Bare metal scatters almost no light: nearly all of what you see of it is reflection. It only
                // gives that light up where there is a daylit sky to mirror in its place. Inside the ship, at
                // night, in open space and with shine turned off it is lit like everything else, so it never
                // goes black.
                half mirrored = metal * 0.85 * saturate(OA_ShineAmount(gloss)) * _SunAmount;
                half scattered = 1.0 - mirrored * OA_DaylitSky(input.positionWS, up);
                // How fast the surface turns across this pixel, to spread a highlight too small to draw.
                half3 turnX = ddx(normal);
                half3 turnY = ddy(normal);
                half widen = min(0.5 * (dot(turnX, turnX) + dot(turnY, turnY)), 0.18);
                half3 color = albedo * light * scattered + albedo * _Glow;
                color += OA_Shine(normal, input.positionWS, up, _SunAmount, gloss, metal, albedo, widen);
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
