// The sky itself: a big ball drawn round the viewer before anything else. In open space it shows the soft
// coloured clouds of the galaxy behind the stars and the glow round the Sun. Inside an atmosphere it shows the
// body's sky as a gradient from the horizon colour up to the deeper colour overhead, with a warm band at
// sunrise and sunset. Every other shader takes its haze from the same sums (OA_Look.hlsl), so far-off hills
// melt into exactly the sky that is behind them.
Shader "OrbitalAnte/Sky"
{
    Properties
    {
        _Backdrop ("Picture of deep space (wrapped round the sky)", 2D) = "black" {}
        _BackdropStrength ("How bright deep space is", Range(0, 3)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Background" "RenderPipeline" = "UniversalPipeline" "Queue" = "Background" "PreviewType" = "Skybox" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Backdrop_ST;
                half _BackdropStrength;
            CBUFFER_END

            TEXTURE2D(_Backdrop);
            SAMPLER(sampler_Backdrop);

            #include "Assets/OrbitalAnte/Resources/Shaders/OA_Look.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 directionOS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                // The ball turns with the stars, so its own directions are fixed to the galaxy.
                output.directionOS = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 view = normalize(input.positionWS - _WorldSpaceCameraPos.xyz);
                half3 color = OA_SkyColor(view);

                float3 d = normalize(input.directionOS);
                float u = atan2(d.z, d.x) * 0.15915494 + 0.5;
                float v = acos(clamp(d.y, -1.0, 1.0)) * 0.31830989;
                // Always the sharpest copy: the picture is soft already, and this keeps the join where u wraps
                // round from 1 back to 0 from showing as a line.
                half3 space = SAMPLE_TEXTURE2D_LOD(_Backdrop, sampler_Backdrop, float2(u, v), 0).rgb;
                color += space * (_BackdropStrength * (1.0 - _OA_Veil.a));
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
