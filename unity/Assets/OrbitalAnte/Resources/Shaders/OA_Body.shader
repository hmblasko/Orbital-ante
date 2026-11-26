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
        // The realistic far look (keyword _OA_REAL): a true night side and, with _OA_RELIEF, the lie of the land.
        _Soft ("How soft the edge of the night is (airless 0.008, thick air 0.08)", Range(0.002, 0.2)) = 0.03
        _Redden ("How much low sunlight is reddened by the air (rgb)", Vector) = (0, 0, 0, 0)
        [NoScaleOffset] _ReliefMap ("Slopes east and north, sea, spare", 2D) = "gray" {}
        _Relief ("How strongly the land's slopes tilt the light", Range(0, 2)) = 0.8
        [NoScaleOffset] _LightsMap ("Lights on the night side", 2D) = "black" {}
        _LightsColor ("Colour of those lights", Color) = (0, 0, 0, 0)
        [NoScaleOffset] _CloudMap ("Clouds that cast shadows", 2D) = "black" {}
        _CloudShadow ("x how dark the clouds' shadows are, y how much cloud, z turns of drift a second", Vector) = (0, 1, 0, 0)
        _Aurora ("Polar lights on the night side (a switches them on)", Color) = (0, 0, 0, 0)
        [NoScaleOffset] _RingMap ("Rings across their width: a is how much they hide", 2D) = "black" {}
        _RingRadii ("Rings' inner and outer edge in body radii (y 0: no rings)", Vector) = (0, 0, 0, 0)
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
            half _Soft;
            half4 _Redden;
            half _Relief;
            half4 _LightsColor;
            float4 _CloudShadow;
            half4 _Aurora;
            float4 _RingRadii;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_ReliefMap);
        SAMPLER(sampler_ReliefMap);
        TEXTURE2D(_LightsMap);
        SAMPLER(sampler_LightsMap);
        TEXTURE2D(_CloudMap);
        SAMPLER(sampler_CloudMap);
        TEXTURE2D(_RingMap);
        SAMPLER(sampler_RingMap);

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
            float3 normalOS : TEXCOORD3;
        };

        Varyings vert(Attributes input)
        {
            Varyings output;
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionWS = positionWS;
            output.positionCS = TransformWorldToHClip(positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.normalOS = input.normalOS;
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
            // multi_compile, not shader_feature: the materials are made while the game runs, so nothing saved
            // uses these and shader_feature would leave them out of a build.
            #pragma multi_compile_local _ _OA_REAL
            #pragma multi_compile_local _ _OA_RELIEF

            #if defined(_OA_REAL)
            // The realistic far look: light that falls off as on a real ball, a night side that is black, sunlight
            // reddened where it skims through air, the land's slopes, the Sun's glint on a sea, the shadows of
            // clouds and of rings, and lights and polar glow in the dark.
            half3 RealLook(Varyings input, half3 albedo, half3 normal, float3 toSun)
            {
                half mu = dot(normal, toSun);
                half3 lightNormal = normal;
                half sea = 0.0;
                half shadow = 1.0;
                half3 extra = 0.0;
                float3 up = normalize(input.normalOS);
                half edge = smoothstep(-_Soft, _Soft * 2.5, mu);
                // Sunlight that arrives low has come a long way through the air.
                half3 sunlight = _OA_SunRaw.rgb * exp(-_Redden.rgb * min(1.0 / max(mu + 0.06, 0.03), 14.0));
            #if defined(_OA_RELIEF)
                half4 lie = SAMPLE_TEXTURE2D(_ReliefMap, sampler_ReliefMap, input.uv);
                sea = lie.b;
                // East and north on the ball, in its own frame; the picture holds the ground's slope along each.
                float3 east = float3(-up.z, 0.0, up.x);
                east /= max(0.001, length(east));
                float3 north = cross(east, up);
                float2 slope = (lie.rg * 2.0 - 1.0) * (2.0 * _Relief * (1.0 - sea));
                float3 tiltedOS = normalize(up - slope.x * east - slope.y * north);
                lightNormal = normalize(TransformObjectToWorldNormal(tiltedOS));
                float3 sunOS = normalize(TransformWorldToObjectDir(toSun));
                // Clouds' shadows fall a little way down-Sun of the clouds.
                float2 toward = float2(dot(sunOS, east) / (6.2832 * max(0.2, length(up.xz))), dot(sunOS, north) / 3.1416);
                float2 cloudUv = input.uv + float2(_Time.y * _CloudShadow.z, 0.0) + toward * 0.035;
                shadow = 1.0 - _CloudShadow.x * saturate(SAMPLE_TEXTURE2D(_CloudMap, sampler_CloudMap, cloudUv).a * _CloudShadow.y);
                // The Sun's glint: a bright smear where a sea mirrors it to the eye.
                float3 toEye = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                half glint = pow(saturate(dot(normal, normalize(toSun + toEye))), 45.0);
                extra += sunlight * (glint * sea * edge * shadow * 1.4);
                // Lights of the bases, on dry land, once it is dark.
                half dark = smoothstep(0.02, -0.12, mu);
                extra += _LightsColor.rgb * (SAMPLE_TEXTURE2D(_LightsMap, sampler_LightsMap, input.uv).r * (1.0 - sea) * dark);
                if (_Aurora.a > 0.001)
                {
                    // A ragged oval of light round each pole, only where it is night.
                    half latitude = degrees(asin(clamp(up.y, -1.0, 1.0)));
                    half longitude = atan2(up.z, up.x);
                    half band = (abs(latitude) - 68.0 - 4.0 * sin(longitude * 2.0 + 1.0)) / 2.2;
                    half curtain = 0.35 + 0.65 * OA_Noise(up * 9.0 + _Time.y * 0.02);
                    extra += _Aurora.rgb * (exp(-band * band) * curtain * dark * _Aurora.a);
                }
            #endif
                if (_RingRadii.y > 0.0)
                {
                    // The rings' shadow: follow the sunlight back from this spot to where it crossed their plane.
                    float3 centre = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                    float3 axis = normalize(TransformObjectToWorldDir(float3(0.0, 1.0, 0.0)));
                    float3 from = input.positionWS - centre;
                    float radius = max(0.001, length(from));
                    half across = dot(toSun, axis);
                    float reach = -dot(from, axis) / (abs(across) < 0.0001 ? 0.0001 : across);
                    float where = (length(from + toSun * reach) / radius - _RingRadii.x) / max(0.001, _RingRadii.y - _RingRadii.x);
                    half hidden = SAMPLE_TEXTURE2D_LOD(_RingMap, sampler_RingMap, float2(saturate(where), 0.5), 0).a;
                    shadow *= (reach > 0.0 && where > 0.0 && where < 1.0) ? 1.0 - 0.85 * hidden : 1.0;
                }
                half shone = saturate(dot(lightNormal, toSun)) * edge * shadow;
                return albedo * (sunlight * shone + _OA_SpaceAmbient.rgb * 0.08) + extra;
            }
            #endif

            half4 frag(Varyings input) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                // Stars carry no surface direction at all, so guard against dividing by nothing.
                half3 normal = input.normalWS / max(0.0001, length(input.normalWS));
                float3 toSun = normalize(_OA_SunPosition.xyz - input.positionWS);
            #if defined(_OA_REAL)
                half3 real = RealLook(input, albedo, normal, toSun);
                float3 lookAlong = normalize(input.positionWS - _WorldSpaceCameraPos.xyz);
                return half4(lerp(real, OA_SkyColor(lookAlong), saturate(_OA_Veil.a * _Veil)), 1.0);
            #else
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
            #endif
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
