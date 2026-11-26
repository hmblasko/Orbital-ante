// A planet's rings as one flat band wearing a strip of picture from its inner edge to its outer: hundreds of
// ringlets and gaps, see-through where they are thin. The planet's shadow lies across them, the side turned
// from the Sun is dim and lit only by what filters through, and stars and the planet show through the gaps.
// (The earlier rings, five solid bands drawn with OA_Body, stay as the stand-in.)
Shader "OrbitalAnte/Rings"
{
    Properties
    {
        [MainTexture] _BaseMap ("Across the rings: colour, and alpha how much they hide", 2D) = "white" {}
        _PlanetRadius ("The planet's radius, in the units of this mesh", Float) = 1
        _Veil ("How much the sky hides it", Range(0, 1)) = 1
    }

    SubShader
    {
        // Before clouds and air, so a planet's air lies over rings that pass behind its edge.
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha, Zero One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _PlanetRadius;
                half _Veil;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            #include "Assets/OrbitalAnte/Resources/Shaders/OA_Look.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;      // x runs 0 at the inner edge to 1 at the outer
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 centreWS : TEXCOORD2;
                float3 axisWS : TEXCOORD3;
                float planetRadius : TEXCOORD4;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.centreWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                output.axisWS = TransformObjectToWorldDir(float3(0.0, 1.0, 0.0));
                output.planetRadius = _PlanetRadius * length(TransformObjectToWorldDir(float3(1.0, 0.0, 0.0), false));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 ring = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, float2(input.uv.x, 0.5));
                float3 axis = normalize(input.axisWS);
                float3 toSun = normalize(_OA_SunPosition.xyz - input.positionWS);
                float3 toEye = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                half sunSide = dot(axis, toSun);
                half eyeSide = dot(axis, toEye);
                // Seen at a slant a thin ring hides more.
                half hide = 1.0 - pow(1.0 - saturate(ring.a), 1.0 / max(0.25, abs(eyeSide)));
                // The planet's shadow: does the way to the Sun from here pass through the planet?
                float3 from = input.positionWS - input.centreWS;
                float toward = dot(from, toSun);
                float miss = sqrt(max(0.0, dot(from, from) - toward * toward)) / max(0.001, input.planetRadius);
                half open = toward < 0.0 ? smoothstep(0.985, 1.0, miss) : 1.0;
                // The sunlit face is bright; the far face shows only light that filters through thin rings.
                half face = sunSide * eyeSide > 0.0
                    ? 0.35 + 0.65 * abs(sunSide)
                    : abs(sunSide) * (1.0 - ring.a) * ring.a;
                half3 color = ring.rgb * (_OA_SunRaw.rgb * (face * open) + _OA_SpaceAmbient.rgb * 0.08);
                float3 view = -toEye;
                color = lerp(color, OA_SkyColor(view), saturate(_OA_Veil.a * _Veil));
                return half4(color * hide, hide);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
