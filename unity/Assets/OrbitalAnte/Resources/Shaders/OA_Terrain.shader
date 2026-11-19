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
        _VertexGlow ("Glow where the mesh says so (strength)", Range(0, 8)) = 0
        _Paint ("Painted break-up of colour", Range(0, 1)) = 1
        _PaintScale ("Paint patch size: 1 for ground, larger for small things", Range(1, 63)) = 1
        _Ground ("Fine ground relief: grit, stones and hummocks (ground only)", Range(0, 1.5)) = 0
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
            half _Paint;
            half _PaintScale;
            half _Ground;
        CBUFFER_END

        TEXTURE2D(_FarMap);
        SAMPLER(sampler_FarMap);

        #include "Assets/OrbitalAnte/Resources/Shaders/OA_Look.hlsl"

        // Ground only. The mesh is made of squares 1.7 m across at its finest, far too coarse for the grit, stones
        // and small hummocks that make ground look like ground, so they are worked out here for every dot of the
        // picture: "relief" is their height in metres, for the light to catch, and the colour is broken into
        // patches of soil, mottling and grain. Nothing is repeated, so there is no tiling to see from the air.
        // The fine layers fade out with distance, before they would shimmer.
        half3 OA_Ground(half3 albedo, float3 place, float range, half amount, out float relief)
        {
            relief = 0.0;
            // Nothing to do where the far-away picture has taken over.
            if (amount < 0.004) return albedo;
            // How much ground one dot of the picture covers. A layer fades out before its bumps get smaller than
            // that, or light would crawl over far ground seen at a low angle.
            float footprint = length(fwidth(place));
            half nearMid = saturate(1.0 - range / 150.0) * saturate(1.5 - footprint * 1.24);
            half nearFine = saturate(1.0 - range / 42.0);
            nearFine *= nearFine * saturate(1.5 - footprint * 6.8);
            // Soils a few tens of metres across, hummocks a couple of metres across, stones underfoot. The two
            // finer layers are only worked out where they are close enough to show.
            half broad = OA_Noise(place * 0.043);
            half mid = 0.5;
            half fine = 0.5;
            if (nearMid > 0.0) mid = OA_Noise(place * 0.62 + 7.0);
            if (nearFine > 0.0)
            {
                // The finest layer is looked up on a turned grid, so its pattern never lines up with the others.
                float3 turned = float3(dot(place, float3(0.36, 0.48, -0.8)), dot(place, float3(-0.8, 0.6, 0.0)), dot(place, float3(0.48, 0.64, 0.6)));
                fine = OA_Noise(turned * 3.4 + 23.0);
            }
            // Stones are rounded lumps with flatter ground between them, not an even ripple.
            half stone = saturate(fine * 1.6 - 0.55);
            stone *= stone;
            relief = ((mid - 0.5) * 0.16 * nearMid + stone * 0.05 * nearFine) * amount;

            half value = (broad - 0.5) * 0.12 + (mid - 0.5) * 0.11 * nearMid + (fine - 0.5) * 0.10 * nearFine;
            // Soil drifts a little toward brown or grey from place to place; stones are paler and greyer.
            half grey = dot(albedo, half3(0.3, 0.5, 0.2));
            half3 soil = lerp(albedo, albedo * half3(1.05, 1.0, 0.93), broad);
            soil = lerp(soil, grey * half3(1.12, 1.1, 1.06), stone * nearFine * 0.45);
            return lerp(albedo, soil * (1.0 + value), saturate(amount));
        }

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
                half3 normal = normalize(input.normalWS);
                if (_Ground > 0.001)
                {
                    // Ground has its own break-up of colour in place of the painted one, and fine relief.
                    float relief;
                    albedo = OA_Ground(albedo, input.positionOS, range, _Ground * (1.0 - blend), relief);
                    // Tilt the surface by the slope of that relief, found from how it changes across the screen.
                    float3 dpx = ddx(input.positionWS);
                    float3 dpy = ddy(input.positionWS);
                    float3 r1 = cross(dpy, normal);
                    float3 r2 = cross(normal, dpx);
                    float det = dot(dpx, r1);
                    float3 tilt = sign(det) * (ddx(relief) * r1 + ddy(relief) * r2) / max(abs(det), 1e-9);
                    // Never tip the surface right over: edge-on ground (and the curtains under tile edges) would
                    // otherwise give wild answers.
                    tilt *= min(1.0, 1.5 / max(length(tilt), 1e-6));
                    normal = normalize(normal - tilt);
                }
                else
                {
                    albedo = OA_Paint(albedo, input.positionOS, range * _PaintScale, _OA_Haze.y * _Paint * (1.0 - blend));
                }
                albedo = lerp(albedo, farColor, blend);

                float3 up = input.upWS / max(0.0001, length(input.upWS));
                half3 light = OA_Light(normal, input.positionWS, up, _SunAmount);
                // Lamps and lit windows on built things: the mesh marks them (in the colour's fourth number).
                half3 color = albedo * light + albedo * (_Glow + _VertexGlow * (1.0 - input.color.a));
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
