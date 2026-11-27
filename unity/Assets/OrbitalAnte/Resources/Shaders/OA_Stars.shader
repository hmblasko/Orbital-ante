// The stars: every star is a small square that always faces the screen, drawn as a soft round dot of light laid
// over the sky. Its size is in screen dots, not metres, so a star is as crisp on the Deck's screen as on a large
// monitor and never becomes a visible square. The star's own colour is in the mesh; its size and brightness come
// from its real magnitude (StarLook in StarCatalogue.cs). Inside an atmosphere the stars fade behind the daytime
// sky (the sky veil), and near the Sun they fade in its glare, as they do for any eye or camera.
Shader "OrbitalAnte/Stars"
{
    Properties
    {
        _Brightness ("How bright the stars are", Range(0, 4)) = 1
        _SunGlare ("How much the Sun's glare hides the stars near it", Range(0, 1)) = 0.75
    }

    SubShader
    {
        // After everything solid and the sky ball, before the air rims, water and glass.
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-400" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One, Zero One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Brightness;
                half _SunGlare;
            CBUFFER_END

            #include "Assets/OrbitalAnte/Resources/Shaders/OA_Look.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;   // the middle of the star: all four corners are the same point
                half4 color : COLOR;            // the star's tint
                float2 uv : TEXCOORD0;          // which corner: -1 or 1 each way
                float2 uv2 : TEXCOORD1;         // x half the square's width in screen dots (at 800 high), y brightness
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 light : TEXCOORD0;
                float2 corner : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float4 positionCS = TransformWorldToHClip(positionWS);
                // Push the corner out by a number of screen dots. On a taller screen a star grows a little, so it
                // does not shrink to a speck; on the cockpit's small camera screens it is never under one dot.
                float dots = input.uv2.x * max(1.0, _ScreenParams.y / 800.0);
                positionCS.xy += input.uv * (dots * 2.0 / _ScreenParams.xy) * positionCS.w;
                output.positionCS = positionCS;
                output.corner = input.uv;

                float3 view = normalize(positionWS - _WorldSpaceCameraPos.xyz);
                half glare = OA_SunGlare(view) * _SunGlare;
                half shown = (1.0 - _OA_Veil.a) * (1.0 - glare);
                output.light = input.color.rgb * (input.uv2.y * _Brightness * shown);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // A round, soft dot: brightest in the middle, gone by the edge of the square.
                half distance2 = dot(input.corner, input.corner);
                half dotShape = saturate(exp(-distance2 * 4.5) - 0.0111);
                return half4(input.light * dotShape, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
