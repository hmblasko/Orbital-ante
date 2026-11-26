// A layer of cloud round a planet seen from space: Earth's weather, Mars's wisps of ice, Venus's unbroken deck.
// A ball a little larger than the planet wearing a painted picture (colour, and in alpha how much cloud), lit by
// the Sun with a soft edge to the night and red at sunset, and drifting slowly round. It is only for looking at
// from above: the game fades it out as the viewer comes down to it (_Strength), and the sky seen from the ground
// is PlanetSky's business.
Shader "OrbitalAnte/Clouds"
{
    Properties
    {
        [MainTexture] _BaseMap ("Clouds: colour, and alpha how much", 2D) = "black" {}
        _Strength ("How much of it shows (faded as the viewer nears)", Range(0, 1)) = 1
        _Amount ("How thick the cloud in the picture is taken to be", Range(0, 1)) = 1
        _Drift ("Turns round the planet a second", Float) = 0
        _Soft ("How soft the edge of the night is", Range(0.002, 0.2)) = 0.03
        _Redden ("How much low sunlight is reddened by the air (rgb)", Vector) = (0, 0, 0, 0)
        [NoScaleOffset] _GroundMap ("The ground under the clouds: alpha is its height", 2D) = "black" {}
        _Ground ("Ground heights in metres: x lowest, y highest, z the cloud layer's height, w band it parts over (0: not known yet)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        // Before the air (Transparent+10), which lies over the clouds.
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+5" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha, Zero One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _Strength;
                half _Amount;
                float _Drift;
                half _Soft;
                half4 _Redden;
                float4 _Ground;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_GroundMap);
            SAMPLER(sampler_GroundMap);

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
                float2 groundUv : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv + float2(_Time.y * _Drift, 0.0);
                output.groundUv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 cloud = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 normal = normalize(input.normalWS);
                float3 toSun = normalize(_OA_SunPosition.xyz - input.positionWS);
                half mu = dot(normal, toSun);
                half edge = smoothstep(-_Soft, _Soft * 2.5, mu);
                half3 sunlight = _OA_SunRaw.rgb * exp(-_Redden.rgb * min(1.0 / max(mu + 0.06, 0.03), 14.0));
                // Cloud tops scatter light every way, so they stay bright until the Sun is nearly down.
                half3 light = sunlight * (saturate(mu * 0.9 + 0.1) * edge * 0.92) + _OA_SpaceAmbient.rgb * 0.06;
                // Seen edge-on a layer hides more than seen from above.
                float3 toEye = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                half slant = max(0.25, saturate(dot(normal, toEye)));
                half amount = 1.0 - pow(1.0 - saturate(cloud.a * _Amount), 1.0 / slant);
                // Cloud parts round ground that stands as high as the layer, so a mountain range rises out of a gap
                // in the cloud and is never cut by it along a hard line.
                if (_Ground.w > 0.0)
                {
                    half ground = lerp(_Ground.x, _Ground.y, SAMPLE_TEXTURE2D(_GroundMap, sampler_GroundMap, input.groundUv).a);
                    amount *= 1.0 - smoothstep(_Ground.z - _Ground.w, _Ground.z, ground);
                }
                amount *= _Strength * (1.0 - _OA_Veil.a);
                return half4(cloud.rgb * light * amount, amount);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
