// The air round a planet seen from space, as a thin shell that thins out with height: a narrow bright line along
// the sunlit edge, a paler and bluer planet toward its limb, a red band where day meets night, and a ring of
// light when the Sun is behind the planet. Drawn on a ball a little larger than the planet; for every dot the
// shader works out how close that line of sight passes to the planet and what air it crosses. This replaces the
// wide soft halo of OA_Atmosphere (which stays as the stand-in when "Real Planets" is off).
Shader "OrbitalAnte/Air"
{
    Properties
    {
        _BaseColor ("Colour of the air", Color) = (0.4, 0.6, 1, 1)
        _Strength ("How much of it shows (faded as the viewer nears the shell)", Range(0, 1)) = 1
        _Depth ("How much air a look straight down crosses", Range(0, 0.5)) = 0.075
        _Scale ("Height over which the air thins, in planet radii", Range(0.002, 0.1)) = 0.016
        _Hide ("How much the air hides what is under it", Range(0, 1)) = 0.55
        _Shell ("This ball's radius in planet radii", Range(1, 2)) = 1.1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            // The colour is already multiplied through: alpha only says how much of what is behind is hidden.
            Blend One OneMinusSrcAlpha, Zero One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Strength;
                half _Depth;
                half _Scale;
                half _Hide;
                half _Shell;
            CBUFFER_END

            #include "Assets/OrbitalAnte/Resources/Shaders/OA_Look.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 centreWS : TEXCOORD1;
                float planetRadius : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.centreWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                // The ball is a unit sphere scaled to the shell, so its scale over _Shell is the planet's radius.
                output.planetRadius = length(TransformObjectToWorldDir(float3(1.0, 0.0, 0.0), false)) / max(1.0, _Shell);
                return output;
            }

            // The air's own colour, turned to sunset where the Sun is on the horizon.
            half3 Tint(half dusk)
            {
                half brightness = dot(_BaseColor.rgb, half3(0.3, 0.5, 0.2));
                return lerp(_BaseColor.rgb, half3(1.0, 0.45, 0.2) * (brightness * 1.4), dusk);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 eye = _WorldSpaceCameraPos.xyz;
                float3 view = normalize(input.positionWS - eye);
                float3 toCentre = input.centreWS - eye;
                float along = dot(toCentre, view);
                float3 nearest = eye + view * along;                    // where this line of sight passes closest
                float radius = max(0.001, input.planetRadius);
                float miss = length(nearest - input.centreWS) / radius; // in planet radii: under 1 it meets the planet
                float3 toSun = normalize(_OA_SunPosition.xyz - input.centreWS);
                half3 color;
                half alpha;
                if (miss < 1.0)
                {
                    // Over the planet's face: a look near the edge crosses much more air than a look straight down.
                    float inside = sqrt(saturate(1.0 - miss * miss));
                    float3 place = normalize(nearest - view * (inside * radius) - input.centreWS);
                    half mu = dot(place, toSun);
                    half amount = 1.0 - exp(-_Depth / max(inside, 0.05));
                    half day = smoothstep(-0.12, 0.25, mu);
                    half dusk = (mu - 0.02) / 0.07;
                    color = Tint(0.6 * exp(-dusk * dusk)) * (amount * day) * _OA_SunRaw.rgb;
                    alpha = amount * _Hide;
                }
                else
                {
                    // Past the edge: the shell alone, thinning with height, brightest looking toward the Sun.
                    float3 place = normalize(nearest - input.centreWS);
                    half mu = dot(place, toSun);
                    half forward = saturate(dot(view, toSun));
                    half glow = exp(-(miss - 1.0) / _Scale) * (1.0 - exp(-_Depth * 9.0));
                    // Nothing may show at the ball's own edge, however thick the air.
                    glow *= saturate((_Shell - miss) / max(0.002, (_Shell - 1.0) * 0.3));
                    half day = smoothstep(-0.10 - 0.6 * forward, 0.2, mu);
                    half dusk = (mu + 0.02) / 0.10;
                    color = Tint(0.55 * exp(-dusk * dusk)) * (glow * day * (1.0 + 5.0 * forward * forward * forward)) * _OA_SunRaw.rgb;
                    alpha = 0.0;
                }
                half shown = _Strength * (1.0 - _OA_Veil.a);
                return half4(color * shown, alpha * shown);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
