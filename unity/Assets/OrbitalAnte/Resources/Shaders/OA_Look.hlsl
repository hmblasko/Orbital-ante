// The game's look, shared by every one of its own shaders so they all agree: the gradient sky, the soft
// warm-and-cool lighting, the painted break-up of flat colour, and distant things melting into the sky.
// The art direction this follows is written down in docs/art_direction.md.
//
// Include after Core.hlsl. Everything here is set once a frame by the game (SkyGlobals / PlanetSky).
#ifndef OA_LOOK_INCLUDED
#define OA_LOOK_INCLUDED

float4 _OA_SunPosition;
half4 _OA_SunColor;        // sunlight reaching the ground where the viewer is
half4 _OA_SunRaw;          // sunlight in open space
half4 _OA_Ambient;         // fill light where the viewer is, already tinted by the sky
half4 _OA_SpaceAmbient;
half4 _OA_Veil;            // a is how completely the sky hides what is beyond the air
float4 _OA_LampPosition;   // w is one over the lamp's reach
half4 _OA_LampColor;

half4 _OA_SkyZenith;       // sky colour straight up
half4 _OA_SkyHorizon;      // sky colour at the horizon
half4 _OA_Sunset;          // extra warm colour along the horizon at sunrise and sunset (already scaled)
half4 _OA_SunGlow;         // rgb colour of the glow round the Sun, a its strength
float4 _OA_SkyUp;          // xyz is "up" where the viewer is (zero in open space)
float4 _OA_Haze;           // x haze thickness (one over metres), y painted look 0..1, z rim light, w bounce light
half4 _OA_Finish;          // x how strongly smooth surfaces shine, y how firmly the edge of a shadow is drawn (0 soft, 1 a band)

// ---------------------------------------------------------------------------------------------- sky

float3 OA_ToSunFromEye()
{
    return normalize(_OA_SunPosition.xyz - _WorldSpaceCameraPos.xyz);
}

// The sky without the Sun's tight core: what haze, water reflections and veiled bodies take their colour from.
half3 OA_SkyBase(float3 view)
{
    half height = dot(view, _OA_SkyUp.xyz);
    half above = saturate(height);
    // The horizon colour hugs the horizon; most of the sky is the deeper zenith colour.
    half low = 1.0 - above;
    half rise = 1.0 - low * low * low;
    half3 sky = lerp(_OA_SkyHorizon.rgb, _OA_SkyZenith.rgb, rise);
    // Below the horizon the air thickens and dims a little.
    sky *= 1.0 - 0.25 * saturate(-height * 3.0);

    half toward = saturate(dot(view, OA_ToSunFromEye()));
    half band = 1.0 - abs(height);
    band = band * band;
    band = band * band * band;
    sky += _OA_Sunset.rgb * (band * (0.3 + 0.7 * toward * toward));

    // The broad glow of air lit from behind.
    half glow = toward * toward;
    glow = glow * glow;
    glow = glow * glow;
    sky += _OA_SunGlow.rgb * (glow * 0.13 * _OA_SunGlow.a);
    return sky;
}

// The whole sky, with the bright bloom close round the Sun. Only the sky dome draws this.
half3 OA_SkyColor(float3 view)
{
    half3 sky = OA_SkyBase(view);
    half toward = max(0.0001, saturate(dot(view, OA_ToSunFromEye())));
    half core = pow(toward, 48.0) * 0.35 + pow(toward, 600.0) * 1.6;
    return sky + _OA_SunGlow.rgb * (core * _OA_SunGlow.a);
}

// ---------------------------------------------------------------------------------------------- paint

float OA_Hash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

// Smooth lumpy noise between 0 and 1. No texture needed.
float OA_Noise(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    float a = lerp(OA_Hash(i), OA_Hash(i + float3(1.0, 0.0, 0.0)), f.x);
    float b = lerp(OA_Hash(i + float3(0.0, 1.0, 0.0)), OA_Hash(i + float3(1.0, 1.0, 0.0)), f.x);
    float c = lerp(OA_Hash(i + float3(0.0, 0.0, 1.0)), OA_Hash(i + float3(1.0, 0.0, 1.0)), f.x);
    float d = lerp(OA_Hash(i + float3(0.0, 1.0, 1.0)), OA_Hash(i + float3(1.0, 1.0, 1.0)), f.x);
    return lerp(lerp(a, b, f.y), lerp(c, d, f.y), f.z);
}

// Breaks a flat colour up the way a painter would: big patches that drift warm and cool, and smaller dabs
// of lighter and darker paint. The small dabs fade out with distance so far ground does not shimmer.
// "place" is a position that stays fixed to the thing being painted, in metres.
half3 OA_Paint(half3 albedo, float3 place, float range, half amount)
{
    // Baked interiors (and the painted style switched off) pay nothing for it.
    if (amount < 0.004) return albedo;
    half nearDab = saturate(1.0 - range / 320.0);
    half nearFine = saturate(1.0 - range / 45.0);
    // The smaller dabs are only worked out where they are close enough to show.
    half broad = OA_Noise(place * 0.043);
    half dab = 0.5;
    half fine = 0.5;
    if (nearDab > 0.0) dab = OA_Noise(place * 0.37 + 17.0);
    if (nearFine > 0.0) fine = OA_Noise(place * 2.3 + 41.0);
    // Sharpen the edges of the patches a little: paint goes down in strokes, not in smooth gradients.
    broad = smoothstep(0.25, 0.75, broad);
    dab = smoothstep(0.3, 0.7, dab);

    half value = (broad - 0.5) * 0.16 + (dab - 0.5) * 0.13 * nearDab + (fine - 0.5) * 0.07 * nearFine;

    half3 cool = albedo * half3(0.93, 0.98, 1.08);
    half3 warm = albedo * half3(1.07, 1.0, 0.91);
    half3 painted = lerp(cool, warm, broad) * (1.0 + value);
    return lerp(albedo, painted, amount);
}

// ---------------------------------------------------------------------------------------------- light

// Soft light with a warm edge to the shadow, sky light from above, warm light bounced up from sunlit ground,
// the lamp, and a rim of sky colour where a surface turns away from the eye. Returns the light, to be
// multiplied by the surface's colour.
half3 OA_Light(half3 normal, float3 positionWS, float3 up, half sunAmount)
{
    float3 toSun = normalize(_OA_SunPosition.xyz - positionWS);
    float3 toEye = normalize(_WorldSpaceCameraPos.xyz - positionWS);
    half facing = dot(normal, toSun);

    // Light wraps a little way round into the shadow. How firmly the edge of the shadow is then drawn is a dial
    // on the Look asset: a firm band reads as illustration, a soft fall-off as a photograph.
    half wrap = saturate((facing + 0.2) / 1.2);
    half lit = lerp(wrap, smoothstep(0.05, 0.6, wrap), _OA_Finish.y);
    // The edge of the shadow glows warm, as light does coming through air or skimming rough ground.
    half edge = saturate(1.0 - abs(wrap - 0.15) * 6.0);
    half3 sun = _OA_SunColor.rgb * (lit + half3(1.0, 0.36, 0.14) * (edge * 0.2));

    // More sky light on faces that look up than on faces that look down.
    half upward = dot(normal, up);
    half3 light = _OA_Ambient.rgb * (0.85 + 0.3 * upward);

    // Sunlit ground throws warm light back up onto overhangs and shadowed walls.
    half dayHere = saturate(dot(up, toSun) * 2.0 + 0.2);
    half3 bounce = _OA_SunColor.rgb * half3(1.0, 0.8, 0.6) * (dayHere * saturate(0.6 - 0.6 * upward) * 0.14 * _OA_Haze.w);

    half grazing = 1.0 - saturate(dot(normal, toEye));
    grazing = grazing * grazing;
    grazing = grazing * grazing;
    half3 rim = (_OA_SkyHorizon.rgb * 0.6 + _OA_SunColor.rgb * 0.1) * (grazing * saturate(facing * 0.7 + 0.5) * _OA_Haze.z);

    // On a planet's night side the planet itself is in the way: nothing down here is sunlit, whichever way it
    // faces (a ship's panels would otherwise catch the Sun through the ground). In open space "up" is zero.
    half sunUp = dot(up, toSun);
    half reached = dot(up, up) > 0.25 ? smoothstep(-0.14, 0.02, sunUp) : 1.0;

    light += (sun + bounce + rim) * (sunAmount * reached);

    float3 toLamp = _OA_LampPosition.xyz - positionWS;
    float lampDistance = max(0.001, length(toLamp));
    half lamp = saturate(1.0 - lampDistance * _OA_LampPosition.w);
    lamp = lamp * lamp * saturate(dot(normal, toLamp / lampDistance) * 0.7 + 0.3);
    light += _OA_LampColor.rgb * lamp;
    return light;
}

// ---------------------------------------------------------------------------------------------- shine

// Tells the graphics card to really skip the work inside an "if". Unity's own library defines it; this is for
// a shader that includes this file without that library.
#ifndef UNITY_BRANCH
#define UNITY_BRANCH
#endif

// How much daylit sky there is over a place for a surface to mirror: 1 by day on a world, falling to 0 through
// dusk, and 0 in open space, where the sky is black.
half OA_DaylitSky(float3 positionWS, float3 up)
{
    float3 toSun = normalize(_OA_SunPosition.xyz - positionWS);
    return dot(up, up) > 0.25 ? smoothstep(-0.14, 0.02, dot(up, toSun)) : 0.0;
}

// How much of the shine a surface of this smoothness gets: none at all up to 0.1 (cloth, webbing, rubber, dust:
// most of any model, and those pixels skip the work), all of it from 0.3 (satin paint) upward.
half OA_ShineAmount(half gloss)
{
    return saturate((gloss - 0.1) * 5.0) * _OA_Finish.x;
}

// The bright spot a light makes on a smooth surface: small and fierce on a mirror, wide and faint on a rough
// one. It is capped low on purpose: a glint must never be as bright as a muzzle flash or an enemy's aim tell
// (those are drawn many times brighter than white), and a spot smaller than a pixel would sparkle.
half OA_Highlight(half3 normal, float3 toLight, float3 toEye, half spread)
{
    float3 halfway = normalize(toLight + toEye + float3(0.0, 0.00001, 0.0));
    float toHalf = saturate(dot(normal, halfway));
    float fall = toHalf * toHalf * (spread - 1.0) + 1.0;
    return min(spread / (3.14159 * fall * fall) * 0.25, 2.0) * saturate(dot(normal, toLight));
}

// What a smooth surface adds to the light it scatters: a highlight from the Sun and from the lamp, and a
// reflection of the sky. gloss is 0 for chalk and 1 for a mirror; metal is 1 for bare metal, which reflects in
// its own colour. widen is how fast the surface turns from one pixel to the next (0 for a flat face): a
// highlight is spread by that much, so one on a thin barrel far away is a soft gleam, not a flicker. Returns
// light to add (not to multiply by the surface's colour). A surface that is not smooth (see OA_ShineAmount) gets
// exactly nothing, so models that say nothing about their finish look as they always did.
half3 OA_Shine(half3 normal, float3 positionWS, float3 up, half sunAmount, half gloss, half metal, half3 albedo, half widen)
{
    half amount = OA_ShineAmount(gloss);
    half3 shine = half3(0.0, 0.0, 0.0);
    UNITY_BRANCH
    if (amount > 0.0)
    {
        float3 toSun = normalize(_OA_SunPosition.xyz - positionWS);
        float3 toEye = normalize(_WorldSpaceCameraPos.xyz - positionWS);
        half facingEye = saturate(dot(normal, toEye));

        // How much a surface mirrors: a few percent face on for paint, cloth and rubber, its whole colour for bare
        // metal, and more for anything seen at a glancing angle.
        half3 mirror = lerp(half3(0.04, 0.04, 0.04), albedo, metal);
        half glance = 1.0 - facingEye;
        half glance5 = glance * glance;
        glance5 = glance5 * glance5 * glance;
        mirror += (max(half3(gloss, gloss, gloss), mirror) - mirror) * glance5;

        half rough = 1.0 - gloss;
        half spread = min(1.0, max(0.002, rough * rough * rough * rough) + widen);

        // The Sun's highlight. The planet is in the way of the Sun on its night side (the same rule as in OA_Light).
        half reached = dot(up, up) > 0.25 ? smoothstep(-0.14, 0.02, dot(up, toSun)) : 1.0;
        half3 light = _OA_SunColor.rgb * (OA_Highlight(normal, toSun, toEye, spread) * reached);

        // The sky, seen in the surface. A rough surface sees all of it at once, which is the fill light; a smooth
        // one sees the part the mirror direction points at. Below the horizon it sees ground, lit by the same fill.
        float3 seen = reflect(-toEye, normal);
        half3 sky = OA_SkyBase(seen);
        half ground = dot(up, up) > 0.25 ? saturate(-dot(seen, up) * 3.0) : 0.0;
        sky = lerp(sky, _OA_Ambient.rgb * 0.7, ground);
        light += lerp(_OA_Ambient.rgb, sky, gloss * gloss);
        // Neither the Sun nor the sky reaches a thing inside the ship.
        light *= sunAmount;

        // The lamp's highlight, wherever the lamp reaches (the same reach as in OA_Light).
        float3 toLamp = _OA_LampPosition.xyz - positionWS;
        float lampDistance = max(0.001, length(toLamp));
        half lamp = saturate(1.0 - lampDistance * _OA_LampPosition.w);
        light += _OA_LampColor.rgb * (lamp * lamp * OA_Highlight(normal, toLamp / lampDistance, toEye, spread));

        shine = mirror * light * amount;
    }
    return shine;
}

// ---------------------------------------------------------------------------------------------- haze

// Far-off things fade into the colour of the sky behind them, so hills melt into the horizon.
half3 OA_ApplyHaze(half3 color, float3 positionWS)
{
    float3 away = positionWS - _WorldSpaceCameraPos.xyz;
    float range = max(0.001, length(away));
    float thickness = range * _OA_Haze.x;
    half hidden = 1.0 - exp(-thickness * thickness);
    return lerp(color, OA_SkyBase(away / range), hidden);
}

#endif
