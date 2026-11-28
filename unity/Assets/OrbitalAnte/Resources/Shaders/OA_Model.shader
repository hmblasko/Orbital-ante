// The game's own models (enemies, guns, the ship's fittings): colour from the colours stored in the mesh, lit
// by the shared look (OA_Look.hlsl) exactly as the ground is, plus what the ground does not have: shine.
// A model made for the realistic look says, at every corner, how smooth its surface is and whether it is bare
// metal (the mesh's second pair of extra numbers: x how smooth, y metal; the first pair is left alone, because
// the ground's shader reads it as "up"). Smooth surfaces show the Sun's highlight
// and the sky. A model that says nothing is chalk all over and looks as it did before this shader existed.
// A model with a texture set ("OAM3", ModelLibrary.MaterialFor) is drawn with the keyword _OA_TEXTURED: its colour,
// smoothness, metal and hollows come from three pictures instead of the corners (the third pair of extra numbers
// says where a corner sits on them), and the baked bumps of the normal map bend the light. Everything else, the
// sky, the Sun, the lamp, the cave light, the shine and the haze, is the same OA_Look.hlsl as every other model.
// The player's own guns can wear a prize skin from the Locker (Ralph loop pass 4): OA_Skin below repaints the painted
// parts (and, for some patterns, the bare metal) before the light is worked out. It runs only for a material whose
// _SkinSet.y is above 0, behind a branch on that number, never a keyword, so a skin needs no shader program of its own
// (no first-use stall when a skinned gun comes into the hand, and no more programs in the build).
// A Celestial prize (Ralph loop pass 6) adds a moving light on top: OA_Fx below, the same sums as Items.CelestialLooks,
// on the gun in the player's hands (a skin's effect) and on the lander's painted hull panels (a paint's effect). It
// runs only for a material whose _FxSet.y is above 0, behind a branch on that number like the skin, adds light but never
// lifts a pixel past white (so nothing it draws blooms like a muzzle flash or an aim tell), and fades a pattern out
// before it would be finer than a few pixels.
Shader "OrbitalAnte/Model"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _SunAmount ("Sunlight reaching it", Range(0, 1)) = 1
        _Glow ("Glows by itself", Range(0, 2)) = 0
        _Paint ("Painted break-up of colour", Range(0, 1)) = 1
        _PaintScale ("Paint patch size: 1 for ground, larger for small things", Range(1, 63)) = 1
        [NoScaleOffset] _ColorMap ("Colour (sRGB)", 2D) = "white" {}
        [NoScaleOffset][Normal] _NormalMap ("Baked bumps (tangent space, green up)", 2D) = "bump" {}
        [NoScaleOffset] _MaskMap ("Hollows (R), smoothness (G), bare metal (B), linear", 2D) = "red" {}
        _SkinColour ("Skin colour", Color) = (1, 1, 1, 1)
        _SkinAccent ("Skin accent", Color) = (1, 1, 1, 1)
        _SkinSet ("Skin pattern (x), how much of the paint it covers (y)", Vector) = (0, 0, 0, 0)
        _FxColour ("Celestial effect colour", Color) = (1, 1, 1, 1)
        _FxSecond ("Celestial effect second colour", Color) = (1, 1, 1, 1)
        _FxSet ("Effect motion (x), strength (y), speed (z), metres per pattern unit (w)", Vector) = (0, 0, 0, 1)
        _FxShape ("Effect finest detail in pattern units (x), 1 on the hull (y), dark band share (z)", Vector) = (1, 0, 0.6, 0)
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
            // Declared for every variant, so all of them keep one layout (the SRP batcher needs that).
            half4 _SkinColour;
            half4 _SkinAccent;
            half4 _SkinSet;
            half4 _FxColour;
            half4 _FxSecond;
            float4 _FxSet;
            float4 _FxShape;
        CBUFFER_END

        #define OA_RECEIVES_SHADOWS
        #include "Assets/OrbitalAnte/Resources/Shaders/OA_Look.hlsl"

        TEXTURE2D(_ColorMap); SAMPLER(sampler_ColorMap);
        TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
        TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            half4 color : COLOR;
            float2 finish : TEXCOORD1;
            float2 uv : TEXCOORD2;
            float4 tangentOS : TANGENT;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half4 color : COLOR;
            half2 finish : TEXCOORD2;
            float3 positionOS : TEXCOORD3;
        #if defined(_OA_TEXTURED)
            float2 uv : TEXCOORD4;
            half4 tangentWS : TEXCOORD5;
        #endif
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
        #if defined(_OA_TEXTURED)
            output.uv = input.uv;
            // The sign says which way "up" on the pictures runs; a body mirrored by its scale turns it over.
            // Left at its own length, as Blender's bake (MikkTSpace) leaves it until the very end.
            output.tangentWS = half4(TransformObjectToWorldDir(input.tangentOS.xyz, false), input.tangentOS.w * GetOddNegativeScale());
        #endif
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
            // Every model material is made in code at run time, so the textured variant must be kept in the
            // build whether or not any saved material uses it (shader_feature would strip it).
            #pragma multi_compile_local _ _OA_TEXTURED
            // URP's switches for the Sun's cast shadows (OA_SunShade in OA_Look.hlsl reads them).
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            // Pattern numbers, as Items.SkinPattern has them (the rule test
            // EverySkinPattern_IsOneTheModelShaderDraws_ByTheSameNumber reads these lines): OA_SKIN_PAINT 1, OA_SKIN_STRIPES 2, OA_SKIN_GRAIN 3, OA_SKIN_PEARL 4, OA_SKIN_FLAKE 5,
            // OA_SKIN_STARS 6, OA_SKIN_GILT 7.
            half OA_SkinHash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            // A prize skin on the player's gun. p is the gun's own space in metres; range is how far off the eye is,
            // so fine patterns settle to their average before they would shimmer. Painted parts (everything that is
            // not bare metal) take the skin, with the pictures' own light and dark (wear, grime, edges) kept in it.
            // How strongly a pattern of a given fineness (cycles per metre of the gun) may show, from how far the gun's
            // own space moves across one pixel: whole at 4 pixels a cycle or more, gone at 1.5, so a fine pattern
            // fades out before it could crawl, on a face seen edge-on as much as at range.
            half OA_SkinFine(float pixel, float cyclesPerMetre)
            {
                return saturate((1.0 / max(pixel * cyclesPerMetre, 1e-4) - 1.5) / 2.5);
            }

            void OA_Skin(inout half3 albedo, inout half gloss, inout half metal, float3 p, half3 normal, half3 toEye, float range, float pixel)
            {
                half pattern = _SkinSet.x;
                half cover = saturate(_SkinSet.y);
                half painted = saturate(1.0 - metal * 1.25) * cover;
                half detail = saturate(dot(albedo, half3(0.299, 0.587, 0.114)) * 2.2);
                half shading = lerp(0.55, 1.15, detail);
                half3 colour = _SkinColour.rgb;
                half3 accent = _SkinAccent.rgb;
                half3 skin = colour;
                half skinGloss = 0.35;
                [branch] if (pattern > 1.5 && pattern < 2.5)
                {
                    // Stripes: diagonal bands 2.5 cm wide, evened out to their average from 4 to 10 m off.
                    half band = step(0.5, frac(dot(p, float3(0.0, 0.7071, 0.7071)) * 20.0));
                    band = lerp(band, 0.5, saturate((range - 4.0) / 6.0));
                    skin = lerp(colour, accent, band);
                    skinGloss = 0.3;
                }
                else if (pattern > 2.5 && pattern < 3.5)
                {
                    // Wood: grain lines along the gun's length, bent a little so they never look ruled, with a
                    // finer grain inside them close up (gone by 3 m, before it could shimmer).
                    half wave = sin(p.y * 90.0 + sin(p.z * 9.0) * 2.0 + p.x * 30.0);
                    half grain = sin(p.y * 420.0 + sin(p.z * 23.0) * 3.0) * saturate(1.0 - range / 3.0) * OA_SkinFine(pixel, 67.0);
                    skin = lerp(colour, accent, saturate(smoothstep(-0.2, 0.9, wave) * 0.55 + grain * 0.12));
                    skinGloss = 0.45;
                }
                else if (pattern > 3.5 && pattern < 4.5)
                {
                    // Mother of pearl: pale, with a colour that moves as the gun turns.
                    half facing = saturate(dot(normal, toEye));
                    half sheen = sin(facing * 6.0 + dot(p, float3(11.0, 7.0, 5.0))) * 0.5 + 0.5;
                    skin = lerp(colour, accent, sheen * (1.0 - facing * 0.5));
                    skinGloss = 0.75;
                }
                else if (pattern > 4.5 && pattern < 5.5)
                {
                    // Candy over chrome: deep colour with fine flake (gone by 3 m, or sooner where a flake would be under
                    // a few pixels), and the bare metal under it tinted.
                    half flake = step(0.94, OA_SkinHash(floor(p * 400.0))) * saturate(1.0 - range / 3.0) * OA_SkinFine(pixel, 400.0);
                    skin = lerp(colour, accent, flake * 0.7);
                    skinGloss = 0.85;
                    albedo = lerp(albedo, saturate(albedo * colour * 1.6), metal * cover);
                }
                else if (pattern > 5.5 && pattern < 6.5)
                {
                    // Star chart: night blue scattered with gold points (gone by 4 m, or sooner where a point would be
                    // under a few pixels).
                    half star = step(0.985, OA_SkinHash(floor(p * 220.0))) * saturate(1.0 - range / 4.0) * OA_SkinFine(pixel, 220.0);
                    skin = lerp(colour, accent, star);
                    skinGloss = 0.4;
                }
                else if (pattern > 6.5)
                {
                    // Gilded: gold leaf over every part, bare metal included.
                    painted = cover;
                    skinGloss = 0.7;
                    metal = lerp(metal, 1.0, cover * 0.8);
                }
                albedo = lerp(albedo, skin * shading, painted);
                gloss = lerp(gloss, skinGloss, painted);
            }

            // Celestial effect motions, as Items.CelestialMotion has them (the rule test
            // EveryEffect_HasItsOwnMotion_AndEveryMotion_IsOneTheModelShaderDraws_ByTheSameNumber reads these lines):
            // OA_FX_FLAMES 1, OA_FX_CURTAINS 2, OA_FX_COMET 3, OA_FX_CLOUDS 4, OA_FX_GLINTS 5, OA_FX_SWIRL 6.
            // The same sums as Items.CelestialLooks.Surface: change the two together (the C# side is what the tests and the
            // offline previews judge). q is the point in pattern units, along how far along the item's run it is, face the
            // axis its surface faces most (0 x, 1 y, 2 z; on the hull 1 the cap, 3 round the side), hullRun 1 on the hull.
            // Returns how brightly the point is lit (0 to 1); w is how far toward the second colour, dark how much of the
            // paint's own light a swirl takes away. Every motion is its own branch, so a pixel pays for one of them only.
            // The noise read on a grid turned against the item (Items.CelestialLooks.TurnedNoise), so a pattern moving along
            // one of the item's own axes never crosses the grid's cells everywhere at once.
            float OA_FxTurned(float3 p)
            {
                return OA_Noise(float3(dot(p, float3(0.0, 0.80, 0.60)), dot(p, float3(-0.80, 0.36, -0.48)), dot(p, float3(-0.60, -0.48, 0.64))));
            }

            float OA_FxAt(float motion, float3 q, float along, float face, float hullRun, float t, float speed, out float w, out float dark)
            {
                w = 0.0;
                dark = 0.0;
                [branch] if (motion < 1.5)
                {
                    // Flames: tongues stretched upward, licking sideways, rising. Guns only: on the hull Solar Flare
                    // churns instead (Items.CelestialLooks.MotionFor), because a flame there reads as a thruster firing.
                    float rise = t * speed;
                    float warp = OA_FxTurned(float3(q.x * 0.7, q.y * 0.7 - rise * 0.6, q.z * 0.7)) - 0.5;
                    float n = OA_FxTurned(float3(q.x * 1.5 + warp * 1.6, q.y * 0.55 - rise, q.z * 1.5 + warp * 1.6));
                    float m = OA_FxTurned(float3(q.x * 3.1 + 7.1, q.y * 1.2 - rise * 1.8, q.z * 3.1));
                    float f = n * 0.72 + m * 0.28;
                    w = smoothstep(0.64, 0.84, f);
                    return smoothstep(0.48, 0.76, f);
                }
                else [branch] if (motion < 2.5)
                {
                    // Curtains: folds of light along the run, swaying, in clumps that never go quite out, with fine rays.
                    float sway = 0.9 * sin(q.y * 0.6 + t * speed * 0.8 + along * 0.2617994)
                                 + 2.4 * (OA_Noise(float3(q.x * 0.25, t * speed * 0.15, q.z * 0.25)) - 0.5);
                    float xx = along + sway;
                    float fold = 0.5 + 0.5 * sin(xx * 3.14159265);
                    fold *= fold;
                    fold *= fold;
                    float clump = 0.3 + 0.7 * smoothstep(0.25, 0.62, OA_Noise(float3(q.x * 0.45 + t * 0.04, q.y * 0.2, q.z * 0.45 - t * 0.03)));
                    float rays = 0.7 + 0.3 * sin(xx * 3.0 * 3.14159265 + t * speed * 3.0);
                    w = saturate(1.0 - fold * 1.3);
                    return fold * clump * rays;
                }
                else [branch] if (motion < 3.5)
                {
                    // Comets: heads running down lanes across the run, each lane at its own pace, some lanes empty.
                    float laneAt = q.y * 1.4 + (1.0 - hullRun) * q.x * 0.6;
                    float lane = floor(laneAt);
                    float edge = sin(frac(laneAt) * 3.14159265);
                    if (OA_Hash(float3(lane, 1.7, 3.3)) < 0.35) return 0.0;
                    float offset = OA_Hash(float3(lane, 3.1, 7.7));
                    float pace = 0.75 + 0.5 * OA_Hash(float3(lane, 5.9, 1.1));
                    float u = frac((t * speed * pace - along) / 6.0 + offset) * 6.0;
                    float rise = saturate(u / 0.12);
                    float e2 = edge * edge;
                    float core = exp(-u * 5.0) * e2 * e2 * e2;
                    float tail = exp(-u * 1.3) * edge * 0.55;
                    float streak = 0.6 + 0.4 * OA_Noise(float3(q.x * 3.0, q.y * 3.0, q.z * 0.5));
                    w = exp(-u * 3.0);
                    return min(1.0, rise * (core + tail * streak));
                }
                else [branch] if (motion < 4.5)
                {
                    // Clouds: two colours drifting slowly, both layers on turned grids (the second reads the point's axes
                    // in another order), so no edge of the noise's cells lines up with the item's own lines.
                    float m = t * speed;
                    float a = OA_FxTurned(float3(q.x * 0.6 + m * 0.3, q.y * 0.6 + m * 0.1, q.z * 0.6 - m * 0.2));
                    float b = OA_FxTurned(float3(q.z * 1.5 + 1.7, q.x * 1.5 - m * 0.15 + 3.3, q.y * 1.5 + m * 0.25));
                    w = b;
                    return smoothstep(0.42, 0.78, a * 0.65 + b * 0.35) * 0.85;
                }
                else [branch] if (motion < 5.5)
                {
                    // Glints: four-pointed stars on the two axes the surface lies along, each on its own slow beat,
                    // sliding down as it fades. On the hull: round the side by the run (a whole number of cells round, so
                    // no seam and no edge where the side turns), the cap from above, and one layer of cells (a sloping dome
                    // would otherwise cross from one layer into the next along a ring).
                    float u0 = face < 0.5 ? q.z : q.x;
                    float v0 = q.y;
                    float k0 = face < 0.5 ? q.x : q.z;
                    if (face > 0.5 && face < 1.5) { u0 = q.x; v0 = q.z; k0 = q.y; }
                    if (face > 2.5) { u0 = along; v0 = q.y; }
                    k0 *= 1.0 - hullRun;
                    float cu = floor(u0), cv = floor(v0), ck = floor(k0);
                    if (OA_Hash(float3(cu, cv, ck)) < 0.5) return 0.0;
                    float period = (2.0 + 2.0 * OA_Hash(float3(cu + 1.3, cv, ck))) / max(speed, 0.05);
                    float life = frac(t / period + OA_Hash(float3(cu, cv + 2.1, ck)));
                    float bright = smoothstep(0.0, 0.12, life) * (1.0 - smoothstep(0.12, 1.0, life));
                    float su = 0.25 + 0.5 * OA_Hash(float3(cu, cv, ck + 4.4));
                    float sv = 0.62 + 0.13 * OA_Hash(float3(cu + 5.5, cv, ck)) - 0.35 * life;
                    float du = frac(u0) - su, dv = frac(v0) - sv;
                    float core = exp(-(du * du + dv * dv) / 0.004);
                    float spikes = (exp(-du * du / 0.0012) * exp(-abs(dv) / 0.07) + exp(-dv * dv / 0.0012) * exp(-abs(du) / 0.07)) * 0.6;
                    float trail = exp(-du * du / 0.0015) * smoothstep(0.0, 0.03, dv) * (1.0 - smoothstep(0.03, 0.3, dv)) * 0.4;
                    w = core;
                    return bright * min(1.0, core + spikes + trail);
                }
                else
                {
                    // Swirl: bands of darkness sweeping round, a bright rim along each band's leading edge.
                    float s = along * 0.25 + q.y * 0.3 - t * speed + (OA_Noise(float3(q.x * 0.6, q.y * 0.6, q.z * 0.6 + t * 0.1)) - 0.5) * 1.2;
                    float off = frac(s) - 0.5;
                    dark = 1.0 - smoothstep(0.10, 0.17, abs(off));
                    float a = off - 0.19, b = off + 0.19;
                    float lead = exp(-(a * a) / 0.003);
                    float trail = exp(-(b * b) / 0.006) * 0.35;
                    w = lead;
                    return min(1.0, lead + trail);
                }
            }

            // The effect on one pixel, after its light is worked out and before the haze: the swirl's darkness taken out
            // of the paint, then the effect's light added, never past white and never below what was there before.
            // Items.CelestialLooks.Lift is the same sum. p is the item's own space in metres (the skin's), pixel how far
            // that moves across one pixel, normalWS the surface's own facing (before any baked bumps), painted 1 where the
            // hull is painted (the mesh says so; a gun is painted all over). On the hull the effect also fades with the
            // part's damage shade (Ship sets _BaseColor per part: 1 sound, 0.78 damaged, 0.5 broken; the same sum as
            // Items.CelestialLooks.HullDamageShare), so it never competes with damage and a wrecked hull never shimmers.
            half3 OA_Fx(half3 color, float3 p, float pixel, half3 normalWS, half painted)
            {
                float size = max(_FxSet.w, 1e-4);
                float hullRun = _FxShape.y;
                float3 q = p / size;
                half sound = saturate((_BaseColor.r - 0.5) * 2.0);
                // Round the hull a unit of the run is a unit of the pattern only at the cabin's middle (24 units round); nearer
                // the upright axis it is finer (Items.CelestialLooks.RunScale), so the pattern fades sooner there, to nothing
                // at the top of the dome, rather than crawl.
                float squeeze = lerp(1.0, saturate(length(q.xz) * (6.2831853 / 24.0)), hullRun);
                half share = lerp(1.0, painted * sound, hullRun) * OA_SkinFine(pixel, 1.0 / max(size * _FxShape.x * squeeze, 1e-4));
                // Nothing to draw here (the hull's dark trim, a broken panel, a pattern too fine to show from this far): the
                // colour is left exactly as it was and the sums are skipped.
                [branch] if (share <= 0.0) return color;
                // Back from the muzzle on a gun, so a comet never runs toward it like a shot charging; round the upright
                // axis on the hull, 24 units once round (Items.CelestialLooks.HullUnitsRound).
                float along = -q.z;
                [branch] if (hullRun > 0.5) along = atan2(q.x, q.z) * (24.0 / 6.2831853);
                float3 nOS = abs(TransformWorldToObjectDir(normalWS, false));
                float face = (nOS.y >= nOS.x && nOS.y >= nOS.z) ? 1.0 : (nOS.x >= nOS.z ? 0.0 : 2.0);
                // On the hull: the cap (facing up or down within 45 degrees) or round the side (Items.CelestialLooks.Face).
                if (hullRun > 0.5) face = nOS.y >= 0.7071 ? 1.0 : 3.0;
                float w, dark;
                float v = OA_FxAt(_FxSet.x, q, along, face, hullRun, _Time.y, _FxSet.z, w, dark);
                half3 light = lerp(_FxColour.rgb, _FxSecond.rgb, (half)w) * (half)(v * _FxSet.y);
                half3 under = color * (1.0 - (half)dark * (half)_FxShape.z * share);
                return max(under, min(under + light * share, 1.0));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float range = length(_WorldSpaceCameraPos.xyz - input.positionWS);
                half3 normal = normalize(input.normalWS);
                // How fast the surface itself turns across this pixel, to spread a highlight too small to draw;
                // taken before any baked bumps, which would otherwise blur every highlight on a textured model.
                half3 turnX = ddx(normal);
                half3 turnY = ddy(normal);
            #if defined(_OA_TEXTURED)
                // The pictures already hold the wear, dust and grime the paint pass imitates on plain colours,
                // so it is left out here.
                half3 albedo = SAMPLE_TEXTURE2D(_ColorMap, sampler_ColorMap, input.uv).rgb * _BaseColor.rgb;
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv);
                half3 bump = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));
                // The frame Blender baked with (MikkTSpace): the interpolated normal and tangent as they come, the
                // other direction across them, and only the result made one long. SafeNormalize keeps a corner with
                // no usable direction (a squashed triangle on the pictures) from turning into a black or glittering
                // pixel.
                half3 smoothNormal = input.normalWS;
                half3 across = input.tangentWS.xyz;
                half3 along = cross(smoothNormal, across) * (input.tangentWS.w > 0.0 ? 1.0 : -1.0);
                normal = SafeNormalize(across * bump.x + along * bump.y + smoothNormal * bump.z);
                half hollow = mask.r;
                half gloss = mask.g;
                half metal = mask.b;
            #else
                half3 albedo = input.color.rgb * _BaseColor.rgb;
                albedo = OA_Paint(albedo, input.positionOS, range * _PaintScale, _OA_Haze.y * _Paint);
                half hollow = 1.0;
                half gloss = input.finish.x;
                half metal = input.finish.y;
            #endif
                // A Locker gun skin: only a skinned material (PlanetMaterials.SkinnedModel) has _SkinSet.y above 0, so
                // every other model skips this on a branch every pixel of the draw takes the same way. positionOS
                // carries the paint's patch size; the skin measures in the gun's own metres. How far that moves
                // across a pixel is taken here, outside the branch, because a derivative inside one is not safe.
                float3 skinAt = input.positionOS / max((float)_PaintScale, 1.0);
                float3 skinStep = fwidth(skinAt);
                float skinPixel = max(max(skinStep.x, skinStep.y), skinStep.z);
                [branch] if (_SkinSet.y > 0.0)
                {
                    OA_Skin(albedo, gloss, metal, skinAt, normal,
                        normalize(_WorldSpaceCameraPos.xyz - input.positionWS), range, skinPixel);
                }
                float3 up = _OA_SkyUp.xyz;

                // The cave's light is read once here and handed to everything below that needs it.
                half4 cave = OA_CaveLight(input.positionWS);
                // The baked hollows darken a crease, but not all the way: the bake saw only the model itself, and
                // the Sun can still shine straight into a hollow that faces it.
                half shade = OA_SunShade(input.positionWS);
                half3 light = OA_LightAt(normal, input.positionWS, up, _SunAmount, cave, shade, 0.0) * lerp(1.0, hollow, 0.6);
                // Bare metal scatters almost no light: nearly all of what you see of it is reflection. It only
                // gives that light up where there is a daylit sky to mirror in its place. Inside the ship, at
                // night, in open space and with shine turned off it is lit like everything else, so it never
                // goes black.
                half mirrored = metal * 0.85 * saturate(OA_ShineAmount(gloss)) * _SunAmount;
                half scattered = 1.0 - mirrored * OA_DaylitSkyIn(input.positionWS, up, cave.a);
                half widen = min(0.5 * (dot(turnX, turnX) + dot(turnY, turnY)), 0.18);
                half3 color = albedo * light * scattered + albedo * _Glow;
                // A highlight from the Sun does not show in something's shadow (the sky's reflection goes with it, which is
                // close enough: in shadow a glint would read as a light of its own).
                color += OA_ShineIn(normal, input.positionWS, up, _SunAmount * lerp(1.0, shade, _OA_Shade.y), gloss, metal, albedo, widen, cave.a);
                // A Celestial effect: only a material made by PlanetMaterials.SetCelestial has _FxSet.y above 0, so every
                // other model skips this on a branch every pixel of the draw takes the same way.
                [branch] if (_FxSet.y > 0.0)
                {
                    color = OA_Fx(color, skinAt, skinPixel, input.normalWS, input.color.a);
                }
                color = OA_ApplyHaze(color, input.positionWS);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            // Draws the thing into the picture URP keeps of what the Sun can see, which is where cast shadows come
            // from. Only drawn while the Sun's light is set to cast them (the Look asset's Sun Shadows).
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow

            float3 _LightDirection;

            float4 vertShadow(Attributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                // Pushed a little way from the light and into the surface, so a face does not shadow itself.
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                // Kept in front of the Sun's near plane, so a caster behind it still casts (done here, not with URP's
                // helper, which not every URP 17 has).
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 fragShadow() : SV_Target
            {
                return 0;
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
