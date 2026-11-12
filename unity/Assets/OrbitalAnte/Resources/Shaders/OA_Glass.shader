// Window glass. See-through, a little more solid where you look along it than straight through it, with a
// glint where the Sun catches it. Drawn from both sides, after the sea and the air so those show through it.
Shader "OrbitalAnte/Glass"
{
    Properties
    {
        [MainColor] _BaseColor ("Tint (alpha is how solid it looks head-on)", Color) = (0.62, 0.84, 0.92, 0.10)
        _Rim ("Extra solidity when seen at a slant", Range(0, 1)) = 0.35
        _Frost ("Whitening as it cracks", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+20" }

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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Rim;
                half _Frost;
            CBUFFER_END

            float4 _OA_SunPosition;
            half4 _OA_SunColor;

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
                half facing = abs(dot(normal, toEye));
                half slant = 1.0 - facing;
                slant = slant * slant * slant;
                // The Sun's reflection, whichever side of the glass you are on.
                float3 mirrored = reflect(-toSun, normal);
                half glint = pow(saturate(abs(dot(mirrored, toEye))), 90.0);
                half3 color = lerp(_BaseColor.rgb, half3(0.93, 0.96, 1.0), _Frost * 0.6) + _OA_SunColor.rgb * glint;
                half alpha = saturate(_BaseColor.a + _Rim * slant + _Frost * 0.22 + glint);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
