using System;
using System.Collections.Generic;

namespace OrbitalAnte.Items
{
    // Celestial effects drawn (Ralph loop pass 6, 28 Nov 2026). Part B section 12 asks for "TF2-style rarity tiers for
    // cosmetics on clothing and weapons, with an Unusual-like top tier featuring special effects (e.g. fire)". A Celestial
    // prize is an ordinary gun skin, ship paint or suit piece that also carries one of six effects, and the effect is
    // drawn where the item shows: as light moving over the gun in the player's hands and over the lander's hull (the
    // model shader's OA_Fx), and as beads of light round the suit's readout plate for a suit piece (SuitHud). Plain C#:
    // the numbers, the words, and the same sums the shader does, so they can be tested and previewed with no engine.
    //
    // What an effect must never be mistaken for (the check-in's rule): damage (sparks, smoke, red), a warning (a blink, an
    // orange or green readout, anything that grows) or an enemy's aim tell (a glow that grows, anything as bright as a
    // muzzle flash). So an effect is a steady loop of light on the item's own surface: no loose sparks or smoke, its
    // average brightness the same from one second to the next, never brighter than white (nothing that blooms), never
    // reacting to a shot, a hit or a reload, and on the readout drawn pale and slow, outside the plate, never over a
    // number.

    /// <summary>
    /// The way a Celestial effect moves. The numbers are the model shader's (<c>OA_FX_&lt;NAME&gt;</c> lines in
    /// OA_Model.shader; the rule test reads them).
    /// </summary>
    public enum CelestialMotion
    {
        None = 0,
        /// <summary>Tongues of flame-coloured light rising up the paint, white-hot at their cores (Solar Flare).</summary>
        Flames = 1,
        /// <summary>Thin curtains of light that sway and ripple, their tops shading into a second colour (Aurora).</summary>
        Curtains = 2,
        /// <summary>Bright heads running along the item in lanes, each with a fading tail (Comet Tail).</summary>
        Comet = 3,
        /// <summary>Soft clouds of two colours drifting slowly (Nebula Drift).</summary>
        Clouds = 4,
        /// <summary>Star points that light one at a time and slide down as they fade (Starfall).</summary>
        Glints = 5,
        /// <summary>Bands of darkness turning round the item, each with a thin bright rim (Event Horizon).</summary>
        Swirl = 6,
    }

    /// <summary>The two places in the world an effect is drawn by the model shader.</summary>
    public enum CelestialPlace
    {
        /// <summary>The gun in the player's hands (a gun skin's effect): small pattern, along the gun's length.</summary>
        Gun = 0,
        /// <summary>The lander's outside hull (a ship paint's effect): large pattern, round the cabin's upright axis.</summary>
        Hull = 1,
    }

    /// <summary>How one effect is drawn in one place: the shader's numbers (<c>PlanetMaterials.SetCelestial</c>).</summary>
    public struct CelestialLook
    {
        public string EffectId;
        public CelestialMotion Motion;
        /// <summary>The effect's colour and its second colour (a flame's core, an aurora's top, a comet's head), sRGB 0 to 1.</summary>
        public float[] Colour, Second;
        /// <summary>The most of its colour the effect adds to a point (0 to 1), before the white ceiling.</summary>
        public float Strength;
        /// <summary>Pattern units a second.</summary>
        public float Speed;
        /// <summary>Metres of the item per pattern unit.</summary>
        public float Size;
        /// <summary>The finest thing in the pattern, in pattern units: the effect fades out before that would be under a few pixels.</summary>
        public float Finest;
        /// <summary>True on the hull: the pattern runs round the upright axis, and only the painted panels take it.</summary>
        public bool RoundUpright;
        /// <summary>How much of the paint's own light a swirl's dark band takes away here (0 to 1; the shader's _FxShape.z).</summary>
        public float Dark;

        /// <summary>The same effect in the same place is always the same key (the material cache's).</summary>
        public string Key => (EffectId ?? "") + "@" + (RoundUpright ? "hull" : "gun");
    }

    /// <summary>Each effect's look, and the sums the model shader does with it.</summary>
    public static class CelestialLooks
    {
        /// <summary>A pixel an effect lights is never lifted past white (linear 1), so an effect never blooms like a muzzle flash or an aim tell.</summary>
        public const float Ceiling = 1f;
        /// <summary>How much of its colour an effect adds at most, on a gun and on the hull (the hull is big: less, or it is gaudy).</summary>
        public const float GunStrength = 0.55f, HullStrength = 0.42f;
        /// <summary>Metres per pattern unit on a gun and on the hull.</summary>
        public const float GunSize = 0.06f, HullSize = 0.6f;
        /// <summary>
        /// How much light a swirl's dark band takes from the paint, on a gun and on the hull. The hull takes far less: a
        /// darkened patch on the lander is what damage looks like (Ship shades a damaged part's renderers down).
        /// </summary>
        public const float GunDark = 0.6f, HullDark = 0.2f;
        /// <summary>
        /// The hull's tint (the red of _BaseColor, which Ship sets per part: 1 sound, 0.78 damaged, 0.5 broken) at or
        /// under which a hull effect is gone (<see cref="HullDamageShare"/>).
        /// </summary>
        public const float DamageOff = 0.5f;
        /// <summary>
        /// Pattern units once round the hull. Every pattern that runs round it repeats a whole number of times in this
        /// (a comet's lap 4 times, a swirl's bands and an aurora's curtains 12), so there is no seam where the run meets
        /// itself. 24 units of 0.6 m is the round of a cabin 2.3 m across the middle, near enough the lander's own.
        /// </summary>
        public const float HullUnitsRound = 24f;
        /// <summary>The length of a comet's lap, in pattern units: two heads are never closer than this in one lane.</summary>
        public const float CometLap = 6f;

        static readonly List<ItemEffect> Known = ItemCatalog.CreateEffects();

        /// <summary>The built-in effect with this id, or null.</summary>
        public static ItemEffect Effect(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Known.Count; i++) if (Known[i].id == id) return Known[i];
            return null;
        }

        /// <summary>The motion each effect has. An effect this table does not know moves like clouds, in its own colour.</summary>
        public static CelestialMotion MotionOf(string effectId)
        {
            switch (effectId)
            {
                case "SolarFlare": return CelestialMotion.Flames;
                case "Aurora": return CelestialMotion.Curtains;
                case "CometTail": return CelestialMotion.Comet;
                case "NebulaDrift": return CelestialMotion.Clouds;
                case "Starfall": return CelestialMotion.Glints;
                case "EventHorizon": return CelestialMotion.Swirl;
                default: return CelestialMotion.Clouds;
            }
        }

        /// <summary>
        /// The motion an effect has in one place. The hull never wears rising flames: every push and turn of the lander
        /// shows a flame (ShipJets, LaunchFx), and orange in the game means something to shoot, so flames on the hull
        /// would read as a thruster firing or the ship on fire. There Solar Flare is a slow churn of its colours, like the
        /// Sun's own surface, that never rises.
        /// </summary>
        public static CelestialMotion MotionFor(string effectId, CelestialPlace place)
        {
            CelestialMotion motion = MotionOf(effectId);
            return place == CelestialPlace.Hull && motion == CelestialMotion.Flames ? CelestialMotion.Clouds : motion;
        }

        /// <summary>
        /// How much of a hull effect shows on a part Ship has shaded for damage (<paramref name="tint"/> is the red of the
        /// part's _BaseColor): all of it on a sound hull, about half on a damaged one, none on a broken one, so an effect
        /// never hides or competes with the hull's damage and a wrecked lander never shimmers. The shader's sum is the same.
        /// </summary>
        public static float HullDamageShare(float tint) => Clamp01((tint - DamageOff) * 2f);

        /// <summary>Each effect's second colour (sRGB): the white-hot core of a flame, the violet top of an aurora, and so on.</summary>
        public static float[] SecondOf(string effectId, float[] colour)
        {
            switch (effectId)
            {
                case "SolarFlare": return new[] { 1.00f, 0.90f, 0.60f };
                case "Aurora": return new[] { 0.62f, 0.48f, 1.00f };
                case "CometTail": return new[] { 1.00f, 1.00f, 1.00f };
                case "NebulaDrift": return new[] { 1.00f, 0.58f, 0.80f };
                case "Starfall": return new[] { 1.00f, 1.00f, 1.00f };
                case "EventHorizon": return new[] { 0.94f, 0.82f, 1.00f };
                default:
                {
                    float[] c = Safe(colour);
                    return new[] { 0.5f + 0.5f * c[0], 0.5f + 0.5f * c[1], 0.5f + 0.5f * c[2] };
                }
            }
        }

        /// <summary>Pattern units a second, on a gun. The hull's units are ten times larger, so the same number is ten times faster there in metres.</summary>
        public static float SpeedOf(CelestialMotion motion)
        {
            switch (motion)
            {
                case CelestialMotion.Flames: return 2.2f;
                case CelestialMotion.Curtains: return 0.6f;
                case CelestialMotion.Comet: return 2.4f;
                case CelestialMotion.Clouds: return 0.25f;
                // Slow enough that a star takes half a second to come up: quicker, and a star on the gun reads as a blink.
                case CelestialMotion.Glints: return 0.5f;
                case CelestialMotion.Swirl: return 0.45f;
                default: return 0f;
            }
        }

        /// <summary>The finest thing each pattern draws, in pattern units (see <see cref="CelestialLook.Finest"/>).</summary>
        public static float FinestOf(CelestialMotion motion)
        {
            switch (motion)
            {
                case CelestialMotion.Flames: return 0.5f;
                case CelestialMotion.Curtains: return 0.4f;
                case CelestialMotion.Comet: return 0.4f;
                case CelestialMotion.Clouds: return 1f;
                case CelestialMotion.Glints: return 0.12f;
                case CelestialMotion.Swirl: return 0.25f;
                default: return 1f;
            }
        }

        /// <summary>How an effect is drawn in one place, or null for no effect.</summary>
        public static CelestialLook? For(ItemEffect effect, CelestialPlace place)
        {
            if (effect == null) return null;
            CelestialMotion motion = MotionFor(effect.id, place);
            bool hull = place == CelestialPlace.Hull;
            return new CelestialLook
            {
                EffectId = effect.id,
                Motion = motion,
                Colour = (float[])Safe(effect.colour).Clone(),
                Second = SecondOf(effect.id, effect.colour),
                Strength = hull ? HullStrength : GunStrength,
                Speed = SpeedOf(motion),
                Size = hull ? HullSize : GunSize,
                Finest = FinestOf(motion),
                RoundUpright = hull,
                Dark = hull ? HullDark : GunDark,
            };
        }

        /// <summary>The look for the effect with this id in one place, or null for none (or an id no effect has).</summary>
        public static CelestialLook? For(string effectId, CelestialPlace place) => For(Effect(effectId), place);

        static float[] Safe(float[] c) => c != null && c.Length == 3 ? c : new[] { 1f, 1f, 1f };

        // ------------------------------------------------------------------ the shader's sums, in C#

        // OA_FxAt in OA_Model.shader does these same sums on the GPU: change the two together. q is a point in pattern
        // units (the item's own metres over Size); along is how far along the item's run it is, in the same units (minus
        // its length on a gun, so a comet runs from the muzzle back toward the hand and never reads as a shot charging;
        // round the upright axis on the hull, Along); face picks the two axes the star points sit on (0 x, 1 y, 2 z: the
        // one the surface faces most); round is true on the hull. Returns how brightly the point is lit (0 to 1); w is how
        // far toward the second colour, and dark how much of the paint's own light a swirl takes away (0 to 1).

        /// <summary>How far along the item's run a point is (pattern units): back from the muzzle on a gun, round the upright axis on the hull.</summary>
        public static float Along(float qx, float qz, bool round)
        {
            if (!round) return -qz;
            return (float)(Math.Atan2(qx, qz) / (2.0 * Math.PI)) * HullUnitsRound;
        }

        /// <summary>
        /// The axis a surface faces most (0 x, 1 y, 2 z), from its normal in the item's own space. On the hull
        /// (<paramref name="round"/>) only two: 1 for the cap (facing up or down within 45 degrees) and 3 round the side,
        /// so a round cabin is never cut into four along upright lines where its side turns from facing x to facing z.
        /// </summary>
        public static int Face(float nx, float ny, float nz, bool round = false)
        {
            float ax = Math.Abs(nx), ay = Math.Abs(ny), az = Math.Abs(nz);
            if (round) return ay >= 0.7071f ? 1 : 3;
            if (ay >= ax && ay >= az) return 1;
            return ax >= az ? 0 : 2;
        }

        /// <summary>
        /// How large a unit of the run is against a unit of the pattern at this point: 1 on a gun, and round the hull the
        /// distance from the upright axis over the cabin's middle radius (24 units round), at most 1. Nearer the axis the
        /// run's patterns are finer, so the shader fades them sooner there (OA_Fx's squeeze), to nothing at the top of the
        /// dome, where 24 units round meet in a point and would crawl.
        /// </summary>
        public static float RunScale(float qx, float qz, bool round)
        {
            if (!round) return 1f;
            return Clamp01((float)Math.Sqrt(qx * qx + qz * qz) * (float)(2.0 * Math.PI / HullUnitsRound));
        }

        public static float Surface(CelestialMotion motion, float qx, float qy, float qz, float along, int face, bool round, float t, float speed,
            out float w, out float dark)
        {
            w = 0f;
            dark = 0f;
            switch (motion)
            {
                case CelestialMotion.Flames:
                {
                    // Tongues stretched upward (a third of the noise's pace up as across), licking sideways as a slower
                    // noise bends them, rising at the effect's speed; the brightest part of each is its white-hot core.
                    // Each noise is read on a grid turned against the item (TurnedNoise): rising straight up through an
                    // upright grid carried the whole item through the grid's cells in step, and its flames swelled and
                    // shrank together about once a second. Guns only: the hull's Solar Flare churns (MotionFor).
                    float rise = t * speed;
                    float warp = TurnedNoise(qx * 0.7f, qy * 0.7f - rise * 0.6f, qz * 0.7f) - 0.5f;
                    float n = TurnedNoise(qx * 1.5f + warp * 1.6f, qy * 0.55f - rise, qz * 1.5f + warp * 1.6f);
                    float m = TurnedNoise(qx * 3.1f + 7.1f, qy * 1.2f - rise * 1.8f, qz * 3.1f);
                    float f = n * 0.72f + m * 0.28f;
                    w = Smooth(0.64f, 0.84f, f);
                    return Smooth(0.48f, 0.76f, f);
                }
                case CelestialMotion.Curtains:
                {
                    // Folds of light every two units along the run, bent by height and time; whole curtains fade in and
                    // out in patches; fine rays shimmer in them; the tops shade into the second colour. Along-run numbers
                    // go a whole number of times round the hull's 24 units (once, 12 and 36 times); the slow noises read
                    // the point itself, which has no seam.
                    float sway = 0.9f * (float)Math.Sin(qy * 0.6f + t * speed * 0.8f + along * 0.2617994f)
                                 + 2.4f * (Noise(qx * 0.25f, t * speed * 0.15f, qz * 0.25f) - 0.5f);
                    float xx = along + sway;
                    float fold = 0.5f + 0.5f * (float)Math.Sin(xx * Math.PI);
                    fold *= fold;
                    fold *= fold;
                    // Never quite out anywhere: a small item must always show some of its aurora.
                    float clump = 0.3f + 0.7f * Smooth(0.25f, 0.62f, Noise(qx * 0.45f + t * 0.04f, qy * 0.2f, qz * 0.45f - t * 0.03f));
                    float rays = 0.7f + 0.3f * (float)Math.Sin(xx * 3f * Math.PI + t * speed * 3f);
                    // Bright folds in the effect's own colour, their faint fringes in the second.
                    w = Clamp01(1f - fold * 1.3f);
                    return fold * clump * rays;
                }
                case CelestialMotion.Comet:
                {
                    // Lanes across the run, each with its own heads; a lane fades to nothing at its edges, so two lanes
                    // never meet along a hard line. A narrow white core at the head, a wider tail fading behind it.
                    float laneAt = qy * 1.4f + (round ? 0f : qx * 0.6f);
                    float lane = (float)Math.Floor(laneAt);
                    float edge = (float)Math.Sin(Frac(laneAt) * Math.PI);
                    // Not every lane has comets, and each lane runs at its own pace, so they never read as ruled lines.
                    if (Hash(lane, 1.7f, 3.3f) < 0.35f) return 0f;
                    float offset = Hash(lane, 3.1f, 7.7f);
                    float pace = 0.75f + 0.5f * Hash(lane, 5.9f, 1.1f);
                    float u = Frac((t * speed * pace - along) / CometLap + offset) * CometLap;
                    float rise = Clamp01(u / 0.12f);
                    float e2 = edge * edge;
                    float core = (float)Math.Exp(-u * 5f) * e2 * e2 * e2;
                    float tail = (float)Math.Exp(-u * 1.3f) * edge * 0.55f;
                    float streak = 0.6f + 0.4f * Noise(qx * 3f, qy * 3f, qz * 0.5f);
                    w = (float)Math.Exp(-u * 3f);
                    return Math.Min(1f, rise * (core + tail * streak));
                }
                case CelestialMotion.Clouds:
                {
                    // Both layers on grids turned against the item, and against each other (the second reads its point's
                    // axes in another order), so no edge of the noise's cells lines up with the item's own lines.
                    float m = t * speed;
                    float a = TurnedNoise(qx * 0.6f + m * 0.3f, qy * 0.6f + m * 0.1f, qz * 0.6f - m * 0.2f);
                    float b = TurnedNoise(qz * 1.5f + 1.7f, qx * 1.5f - m * 0.15f + 3.3f, qy * 1.5f + m * 0.25f);
                    w = b;
                    return Smooth(0.42f, 0.78f, a * 0.65f + b * 0.35f) * 0.85f;
                }
                case CelestialMotion.Glints:
                {
                    // On the two axes the surface lies along, so every face of the item has its stars: a four-pointed
                    // star in about half the cells, each lighting on its own beat and sliding down as it fades, with a
                    // short trail above it. On the hull: round the side by the run (a whole number of cells round, so no
                    // seam and no edge where the side turns), the cap from above, and one layer of cells (a sloping dome
                    // would otherwise cross from one layer into the next along a ring).
                    float u0 = face == 0 ? qz : qx, v0 = qy, k0 = face == 0 ? qx : qz;
                    if (face == 1) { u0 = qx; v0 = qz; k0 = qy; }
                    if (face == 3) { u0 = along; v0 = qy; }
                    if (round) k0 = 0f;
                    float cu = (float)Math.Floor(u0), cv = (float)Math.Floor(v0), ck = (float)Math.Floor(k0);
                    if (Hash(cu, cv, ck) < 0.5f) return 0f;
                    float period = (2f + 2f * Hash(cu + 1.3f, cv, ck)) / Math.Max(speed, 0.05f);
                    float life = Frac(t / period + Hash(cu, cv + 2.1f, ck));
                    float bright = Smooth(0f, 0.12f, life) * (1f - Smooth(0.12f, 1f, life));
                    float su = 0.25f + 0.5f * Hash(cu, cv, ck + 4.4f);
                    float sv = 0.62f + 0.13f * Hash(cu + 5.5f, cv, ck) - 0.35f * life;
                    float du = Frac(u0) - su, dv = Frac(v0) - sv;
                    float core = (float)Math.Exp(-(du * du + dv * dv) / 0.004f);
                    float spikes = ((float)Math.Exp(-du * du / 0.0012f) * (float)Math.Exp(-Math.Abs(dv) / 0.07f)
                                    + (float)Math.Exp(-dv * dv / 0.0012f) * (float)Math.Exp(-Math.Abs(du) / 0.07f)) * 0.6f;
                    float trail = (float)Math.Exp(-du * du / 0.0015f) * Smooth(0f, 0.03f, dv) * (1f - Smooth(0.03f, 0.3f, dv)) * 0.4f;
                    w = core;
                    return bright * Math.Min(1f, core + spikes + trail);
                }
                case CelestialMotion.Swirl:
                {
                    // Bands of darkness sweeping round (six round the hull), bent by a slow noise; a bright rim along each
                    // band's leading edge and a fainter one behind it, as if light were bending round it.
                    float s = along * 0.25f + qy * 0.3f - t * speed + (Noise(qx * 0.6f, qy * 0.6f, qz * 0.6f + t * 0.1f) - 0.5f) * 1.2f;
                    float off = Frac(s) - 0.5f;
                    dark = 1f - Smooth(0.10f, 0.17f, Math.Abs(off));
                    float a = off - 0.19f, b = off + 0.19f;
                    float lead = (float)Math.Exp(-(a * a) / 0.003f);
                    float trail = (float)Math.Exp(-(b * b) / 0.006f) * 0.35f;
                    w = lead;
                    return Math.Min(1f, lead + trail);
                }
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// One point's colour after the effect (linear, as the shader has it): the lit colour with the swirl's darkness taken
        /// out, then the effect's light added, but never past <see cref="Ceiling"/> and never below what was there before
        /// the light was added. <paramref name="share"/> is how much of the effect this point takes (painted, near enough,
        /// sound); <paramref name="darkShare"/> is how much light a dark band takes here (<see cref="CelestialLook.Dark"/>).
        /// </summary>
        public static float Lift(float lit, float light, float dark, float share, float darkShare)
        {
            float under = lit * (1f - dark * darkShare * share);
            return Math.Max(under, Math.Min(under + light * share, Ceiling));
        }

        /// <summary>
        /// The share of a pixel's light URP's bloom takes, for a pixel whose brightest channel is <paramref name="brightness"/>
        /// (linear) and a threshold as the Look asset's dial sets it (<paramref name="thresholdDial"/>: Unity takes it as a
        /// gamma value, makes it linear the sRGB way, and starts softly at half of it). URP's own prefilter sums, so the
        /// tests can say how far an effect is from glowing like a muzzle flash or an aim tell.
        /// </summary>
        public static float BloomShare(float brightness, float thresholdDial)
        {
            float d = Math.Max(0f, thresholdDial);
            float t = d <= 0.04045f ? d / 12.92f : (float)Math.Pow((d + 0.055f) / 1.055f, 2.4f);
            float knee = t * 0.5f;
            float soft = Math.Min(Math.Max(brightness - t + knee, 0f), 2f * knee);
            soft = soft * soft / (4f * knee + 1e-4f);
            return Math.Max(brightness - t, soft) / Math.Max(brightness, 1e-4f);
        }

        /// <summary>
        /// The noise read on a grid turned against the item (an even turn, the shader's OA_FxTurn), so a pattern moving
        /// along one of the item's own axes never crosses the grid's cells everywhere at once.
        /// </summary>
        public static float TurnedNoise(float x, float y, float z) =>
            Noise(0.80f * y + 0.60f * z, -0.80f * x + 0.36f * y - 0.48f * z, -0.60f * x - 0.48f * y + 0.64f * z);

        // The game shader's own hash and noise (OA_Hash and OA_Noise in OA_Look.hlsl), in C#.
        public static float Hash(float x, float y, float z)
        {
            x = Frac(x * 0.3183099f + 0.1f) * 17f;
            y = Frac(y * 0.3183099f + 0.1f) * 17f;
            z = Frac(z * 0.3183099f + 0.1f) * 17f;
            return Frac(x * y * z * (x + y + z));
        }

        public static float Noise(float x, float y, float z)
        {
            float ix = (float)Math.Floor(x), iy = (float)Math.Floor(y), iz = (float)Math.Floor(z);
            float fx = x - ix, fy = y - iy, fz = z - iz;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            fz = fz * fz * (3f - 2f * fz);
            float a = Lerp(Hash(ix, iy, iz), Hash(ix + 1f, iy, iz), fx);
            float b = Lerp(Hash(ix, iy + 1f, iz), Hash(ix + 1f, iy + 1f, iz), fx);
            float c = Lerp(Hash(ix, iy, iz + 1f), Hash(ix + 1f, iy, iz + 1f), fx);
            float d = Lerp(Hash(ix, iy + 1f, iz + 1f), Hash(ix + 1f, iy + 1f, iz + 1f), fx);
            return Lerp(Lerp(a, b, fy), Lerp(c, d, fy), fz);
        }

        public static float Frac(float v) => v - (float)Math.Floor(v);
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>The shader's smoothstep.</summary>
        public static float Smooth(float a, float b, float v)
        {
            float t = Clamp01((v - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>
    /// A suit piece's effect, drawn as beads of light round the outside of the suit's readout plate (SuitHud). The suit's
    /// readout is the one place on screen the suit is, so all five pieces (helmet, visor, suit, jet pack, badge) show their
    /// effects there; with more than one, the next takes its turn each time the plate comes up. Kept calm on purpose,
    /// because the plate holds the air and armour readouts: pale colours (paler than every colour that means something on
    /// the readout), at most a little over half bright, never a bead lit from dark in under a quarter of a second, the
    /// plate's overall light steady, and the beads outside the plate's edge, never over a number. They show off for a few
    /// seconds as the plate comes up (stepping outside) and then idle at under half that, and they go dark while the plate
    /// or the notice beside it is warning and in a fight. Plain C#.
    /// </summary>
    public static class CelestialPlate
    {
        /// <summary>Pixels between beads along the plate's edge. Each bead is longer than this, so they run together into one line of light.</summary>
        public const float Spacing = 12f;
        /// <summary>
        /// How far outside the plate's edge the middle of each bead sits (pixels), and how close to a corner they go. A bead
        /// is <see cref="BeadWidth"/> across, so it stays a pixel clear of the plate's own edge.
        /// </summary>
        public const float Outside = 4f, CornerClear = 7f;
        /// <summary>A bead's size along the edge and across it (pixels).</summary>
        public const float BeadLength = 24f, BeadWidth = 6f;
        /// <summary>The brightest a bead is drawn (alpha).</summary>
        public const float Most = 0.7f;
        /// <summary>How far an effect's colours go toward the readout's cream on the plate.</summary>
        public const float PaleShare = 0.62f;
        /// <summary>The most saturated a colour on the plate may be: under every colour the readout uses to mean something.</summary>
        public const float MostSaturation = 0.38f;
        /// <summary>The readout's cream (HullKit.Paint).</summary>
        public static readonly float[] Cream = { 0.937f, 0.902f, 0.824f };
        /// <summary>
        /// As the plate comes up (stepping outside), the beads show off at full strength for <see cref="FlourishSeconds"/>,
        /// then settle over <see cref="SettleSeconds"/> to <see cref="Idle"/> of it, where they stay: a border that moves at
        /// full strength all the time pulls at the corner of the eye (rule 7).
        /// </summary>
        public const float FlourishSeconds = 3f, SettleSeconds = 1.5f, Idle = 0.45f;
        /// <summary>Pixels of the plate's edge per pattern unit.</summary>
        public const float Unit = 26f;
        /// <summary>The most a bead's brightness may change in a second (0 to full in no less than a quarter of a second).</summary>
        public const float MostChange = 4f;
        /// <summary>Seconds the beads take to go dark when they are hushed (<see cref="Hushed"/>), and to come back once it is over.</summary>
        public const float HushOutSeconds = 0.3f, HushBackSeconds = 1.5f;

        /// <summary>One bead: its middle (pixels from the plate's top left corner, y down), whether it lies along a side, and how far round the edge it is.</summary>
        public struct Bead
        {
            public float X, Y;
            public bool Upright;
            public float Along;
        }

        /// <summary>
        /// The beads round a plate of this size, clockwise from its top left corner, just outside its edge and clear of
        /// its corners.
        /// </summary>
        public static List<Bead> Path(float width, float height)
        {
            var beads = new List<Bead>();
            float along = 0f;
            // Top (left to right), right (down), bottom (right to left), left (up).
            AddSide(beads, ref along, width, (s) => new[] { s, -Outside }, false);
            AddSide(beads, ref along, height, (s) => new[] { width + Outside, s }, true);
            AddSide(beads, ref along, width, (s) => new[] { width - s, height + Outside }, false);
            AddSide(beads, ref along, height, (s) => new[] { -Outside, height - s }, true);
            return beads;
        }

        static void AddSide(List<Bead> beads, ref float along, float length, Func<float, float[]> at, bool upright)
        {
            float run = length - 2f * CornerClear;
            int n = Math.Max(1, (int)Math.Floor(run / Spacing) + 1);
            float step = n > 1 ? run / (n - 1) : 0f;
            for (int i = 0; i < n; i++)
            {
                float s = CornerClear + step * i;
                float[] p = at(s);
                beads.Add(new Bead { X = p[0], Y = p[1], Upright = upright, Along = along + s });
            }
            along += length;
        }

        /// <summary>
        /// The plate's version of an effect's colour: most of the way to the readout's cream, and further still if it would
        /// be more saturated than <see cref="MostSaturation"/> (a deep colour an item asset is given, say), so no bead is
        /// ever as strong a colour as the readout's orange, green or blue.
        /// </summary>
        public static float[] Pale(float[] colour)
        {
            float[] c = colour != null && colour.Length == 3 ? colour : Cream;
            float[] o = Toward(c, PaleShare);
            if (Saturation(o) <= MostSaturation) return o;
            // Halving between here and the cream itself (well under the limit); only a share that meets the limit is ever
            // kept, so the answer always does.
            float low = PaleShare, high = 1f;
            for (int i = 0; i < 16; i++)
            {
                float mid = 0.5f * (low + high);
                if (Saturation(Toward(c, mid)) > MostSaturation) low = mid;
                else high = mid;
            }
            return Toward(c, high);
        }

        static float[] Toward(float[] c, float share)
        {
            var o = new float[3];
            for (int i = 0; i < 3; i++) o[i] = CelestialLooks.Clamp01(c[i]) + (Cream[i] - CelestialLooks.Clamp01(c[i])) * share;
            return o;
        }

        /// <summary>How one effect is drawn round the plate: its motion and its two colours, made pale.</summary>
        public struct Look
        {
            public string EffectId;
            public CelestialMotion Motion;
            public float[] Colour, Second;
        }

        /// <summary>The plate's look for the effect with this id, or null for none (or an id no effect has).</summary>
        public static Look? For(string effectId)
        {
            ItemEffect effect = CelestialLooks.Effect(effectId);
            if (effect == null) return null;
            return new Look
            {
                EffectId = effect.id,
                Motion = CelestialLooks.MotionOf(effect.id),
                Colour = Pale(effect.colour),
                Second = Pale(CelestialLooks.SecondOf(effect.id, effect.colour)),
            };
        }

        /// <summary>HSV saturation of an sRGB colour (0 grey to 1 pure).</summary>
        public static float Saturation(float[] c)
        {
            float max = Math.Max(c[0], Math.Max(c[1], c[2])), min = Math.Min(c[0], Math.Min(c[1], c[2]));
            return max <= 0f ? 0f : (max - min) / max;
        }

        /// <summary>
        /// How bright one bead is (0 to <see cref="Most"/>) and how far toward the effect's second colour (w), for a bead
        /// <paramref name="along"/> pixels round the edge of a plate <paramref name="round"/> pixels round, at time t.
        /// </summary>
        public static float Light(CelestialMotion motion, float along, float round, float t, out float w)
        {
            float s = along / Unit;
            w = 0f;
            float v;
            switch (motion)
            {
                case CelestialMotion.Flames:
                {
                    // Flickering tongues travelling slowly round the edge. Time runs across the noise's grid at a slant,
                    // so the beads never all pass through its cells together (which pulsed the whole plate).
                    float x = s * 0.8f, y = t * 0.45f, x2 = s * 1.9f + 3f, y2 = t * 0.8f;
                    float n = 0.6f * CelestialLooks.Noise(0.8f * x - 0.6f * y, 0.6f * x + 0.8f * y, 0.5f)
                              + 0.4f * CelestialLooks.Noise(0.8f * x2 - 0.6f * y2, 0.6f * x2 + 0.8f * y2, 2.5f);
                    w = CelestialLooks.Smooth(0.62f, 0.85f, n);
                    v = CelestialLooks.Smooth(0.42f, 0.80f, n);
                    break;
                }
                case CelestialMotion.Curtains:
                {
                    float sway = (float)Math.Sin(s * 0.08f + t * 0.24f) * 1.4f;
                    float b = 0.5f + 0.5f * (float)Math.Sin((s * 0.35f + sway + t * 0.09f) * Math.PI);
                    v = b * b * b * (0.7f + 0.3f * (float)Math.Sin(s * 1.3f + t * 0.66f));
                    w = CelestialLooks.Clamp01(0.5f + 0.5f * (float)Math.Sin(s * 0.05f + t * 0.25f));
                    break;
                }
                case CelestialMotion.Comet:
                {
                    // One head going slowly round the plate (60 px a second: about 17 s round the suit's plate).
                    const float Pace = 60f;
                    float lap = Math.Max(1f, round);
                    float u = CelestialLooks.Frac((t * Pace - along) / lap) * lap;
                    float head = CelestialLooks.Clamp01(u / 40f);
                    float tail = (float)Math.Exp(-Math.Max(u - 40f, 0f) / 90f);
                    w = (float)Math.Exp(-u / 60f);
                    v = head * tail;
                    break;
                }
                case CelestialMotion.Clouds:
                {
                    float a = CelestialLooks.Noise(s * 0.35f + t * 0.05f, t * 0.03f, 1.5f);
                    float b = CelestialLooks.Noise(s * 0.9f - t * 0.04f + 3.3f, t * 0.05f, 4.5f);
                    w = b;
                    v = CelestialLooks.Smooth(0.38f, 0.74f, a * 0.65f + b * 0.35f);
                    break;
                }
                case CelestialMotion.Glints:
                {
                    // Some beads twinkle, each on its own slow beat.
                    float cell = (float)Math.Floor(along / Spacing + 0.5f);
                    if (CelestialLooks.Hash(cell, 1.7f, 9.1f) < 0.45f) { v = 0f; break; }
                    float period = 2.6f + 2.4f * CelestialLooks.Hash(cell, 4.2f, 0.3f);
                    float life = CelestialLooks.Frac(t / period + CelestialLooks.Hash(cell, 8.8f, 2.2f));
                    v = CelestialLooks.Smooth(0f, 0.16f, life) * (1f - CelestialLooks.Smooth(0.16f, 1f, life));
                    w = v;
                    break;
                }
                case CelestialMotion.Swirl:
                {
                    // Pairs of lights (a band's two rims) running round, the dark between them.
                    float x = CelestialLooks.Frac(s * 0.22f - t * 0.16f + CelestialLooks.Noise(s * 0.3f, t * 0.05f, 6f) * 0.4f);
                    float r = Math.Abs(x - 0.5f) - 0.24f;
                    v = (float)Math.Exp(-(r * r) / 0.005f);
                    w = v;
                    break;
                }
                default:
                    v = 0f;
                    break;
            }
            return CelestialLooks.Clamp01(v) * Most;
        }

        /// <summary>
        /// Whether the beads are hushed (go dark): while the suit's notice beside the plate is an alarm (hazard stripes),
        /// while the plate itself is warning by blinking (air at alarm, low jet fuel, a hit's flash on the armour), and in a
        /// fight (an enemy has had the player in sight lately, or a creature has begun a warning at them). Nothing moves next
        /// to a warning, and nothing pulls at the eye while there is shooting to do.
        /// </summary>
        public static bool Hushed(bool alarm, bool plateBlinks, bool inFight) => alarm || plateBlinks || inFight;

        /// <summary>
        /// How much of the beads' light shows (0 to 1), one step on from <paramref name="shown"/>: going dark while hushed
        /// (<see cref="Hushed"/>) and coming back slowly once it is over.
        /// </summary>
        public static float Shown(float shown, bool hushed, float dt)
        {
            if (dt < 0f || float.IsNaN(dt)) dt = 0f;
            return hushed ? Math.Max(0f, shown - dt / HushOutSeconds) : Math.Min(1f, shown + dt / HushBackSeconds);
        }

        /// <summary>
        /// The beads' strength <paramref name="sinceUp"/> seconds after the plate came up: 1 through the flourish, then
        /// easing to <see cref="Idle"/>.
        /// </summary>
        public static float Strength(float sinceUp)
        {
            if (float.IsNaN(sinceUp) || sinceUp <= FlourishSeconds) return 1f;
            return 1f + (Idle - 1f) * CelestialLooks.Smooth(0f, 1f, (sinceUp - FlourishSeconds) / SettleSeconds);
        }

        /// <summary>
        /// Which effect (of <paramref name="count"/> worn) shows on the plate's <paramref name="timesUp"/>-th time up (0
        /// the first): the next one each time the player steps outside, never changing while the plate shows.
        /// </summary>
        public static int TurnFor(int count, int timesUp)
        {
            if (count <= 1) return 0;
            return ((timesUp % count) + count) % count;
        }
    }
}
