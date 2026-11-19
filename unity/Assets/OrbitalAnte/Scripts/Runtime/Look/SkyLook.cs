using System;

namespace OrbitalAnte.Look
{
    /// <summary>A colour as three numbers from 0 to 1 (as picked by eye on a screen). No Unity types, so the rules can be tested anywhere.</summary>
    public struct Rgb
    {
        public float R, G, B;

        public Rgb(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        public static readonly Rgb Black = new Rgb(0f, 0f, 0f);
        public static readonly Rgb White = new Rgb(1f, 1f, 1f);

        public static Rgb operator *(Rgb c, float k) => new Rgb(c.R * k, c.G * k, c.B * k);
        public static Rgb operator *(Rgb a, Rgb b) => new Rgb(a.R * b.R, a.G * b.G, a.B * b.B);
        public static Rgb operator +(Rgb a, Rgb b) => new Rgb(a.R + b.R, a.G + b.G, a.B + b.B);

        public static Rgb Lerp(Rgb a, Rgb b, float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
        }

        /// <summary>How bright the colour looks.</summary>
        public float Brightness => R * 0.3f + G * 0.55f + B * 0.15f;

        /// <summary>Pushes the colour away from grey (amount above 1) or toward it (below 1). Never goes below zero.</summary>
        public Rgb Saturated(float amount)
        {
            float grey = Brightness;
            return new Rgb(Math.Max(0f, grey + (R - grey) * amount), Math.Max(0f, grey + (G - grey) * amount), Math.Max(0f, grey + (B - grey) * amount));
        }
    }

    /// <summary>Everything the sky shaders need to draw one moment of one sky.</summary>
    public struct SkyPalette
    {
        /// <summary>The colour straight overhead.</summary>
        public Rgb Zenith;
        /// <summary>The colour along the horizon.</summary>
        public Rgb Horizon;
        /// <summary>Extra warm colour along the horizon at sunrise and sunset. Black at midday and at night.</summary>
        public Rgb Sunset;
        /// <summary>The colour of the glow round the Sun.</summary>
        public Rgb Glow;
        public float GlowStrength;
        /// <summary>What the air does to the colour of sunlight on the ground: white at midday, amber at sunset.</summary>
        public Rgb SunTint;
        /// <summary>0 at midday and at night, 1 when the Sun sits on the horizon.</summary>
        public float SunsetAmount;
    }

    /// <summary>
    /// The rules that turn a body's two sky colours (sky and haze, set per planet) into a painted sky: deeper
    /// overhead, paler at the horizon, warm at sunrise and sunset, with a glow round the Sun. The art direction
    /// behind the numbers is in docs/art_direction.md. Pure sums, no Unity types.
    /// </summary>
    public static class SkyLook
    {
        public static readonly Rgb SunsetWarm = new Rgb(1f, 0.52f, 0.28f);
        public static readonly Rgb SunGlow = new Rgb(1f, 0.93f, 0.78f);
        public static readonly Rgb SunsetLight = new Rgb(1f, 0.8f, 0.62f);
        /// <summary>The glow round the Sun in open space, before the scale for how far away the Sun is.</summary>
        public const float SpaceGlow = 0.4f;
        public const float AirGlow = 0.9f;

        static float Smooth(float from, float to, float x)
        {
            float t = (x - from) / (to - from);
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// How much of a sunset is showing. <paramref name="sunHeight"/> is 1 with the Sun overhead, 0 with it on
        /// the horizon and negative when it has set. Strongest just as the Sun touches the horizon; gone by the
        /// time it is a quarter of the way up the sky, and soon after it has set.
        /// </summary>
        public static float SunsetAmount(float sunHeight)
        {
            const float peak = 0.03f;
            return sunHeight >= peak ? 1f - Smooth(peak, 0.42f, sunHeight) : Smooth(-0.22f, peak, sunHeight);
        }

        /// <summary>The sky in open space: black, with a glow round the Sun that shrinks as the Sun gets further away.</summary>
        public static SkyPalette OpenSpace(float glowScale)
        {
            return new SkyPalette
            {
                Zenith = Rgb.Black, Horizon = Rgb.Black, Sunset = Rgb.Black,
                Glow = SunGlow, GlowStrength = SpaceGlow * glowScale, SunTint = Rgb.White,
            };
        }

        /// <summary>
        /// The sky from inside a body's air.
        /// </summary>
        /// <param name="sky">The body's sky colour.</param>
        /// <param name="haze">The body's haze colour (what the horizon and far-off ground fade into).</param>
        /// <param name="sunHeight">1 with the Sun overhead, 0 on the horizon, negative below it.</param>
        /// <param name="light">How bright the sky is right now: 1 by day, close to 0 at night.</param>
        /// <param name="amount">How much sky there is: 0 at the top of the air (black space), 1 on the ground.</param>
        /// <param name="uniform">True inside cloud or under water, where the surroundings are one colour all round.</param>
        /// <param name="clear">1 in clear weather, toward 0 in a storm (storms have no sunsets and no Sun glow).</param>
        /// <param name="glowScale">The glow round the Sun in space here (see <see cref="OpenSpace"/>).</param>
        public static SkyPalette InAir(Rgb sky, Rgb haze, float sunHeight, float light, float amount, bool uniform, float clear, float glowScale)
        {
            amount = amount < 0f ? 0f : amount > 1f ? 1f : amount;
            clear = clear < 0f ? 0f : clear > 1f ? 1f : clear;
            float sunset = uniform ? 0f : SunsetAmount(sunHeight) * clear;

            Rgb zenith, horizon;
            if (uniform)
            {
                zenith = sky;
                horizon = sky;
            }
            else
            {
                // Deeper and richer overhead; pale and hazy at the horizon.
                zenith = sky.Saturated(1.45f) * 0.78f;
                // At sunset the top of the sky leans toward violet.
                zenith = Rgb.Lerp(zenith, zenith * new Rgb(0.78f, 0.74f, 1.08f), sunset * 0.7f);
                horizon = Rgb.Lerp(Rgb.Lerp(sky, haze, 0.55f), Rgb.White, 0.08f);
            }

            // The Sun's glow is hidden once the Sun is under the horizon (the ground is in the way).
            float sunUp = Smooth(-0.12f, 0.05f, sunHeight);
            float airGlow = AirGlow * sunUp * clear * (uniform ? 0.4f : 1f);

            return new SkyPalette
            {
                Zenith = zenith * (light * amount),
                Horizon = horizon * (light * amount),
                Sunset = Rgb.Lerp(SunsetWarm, haze, 0.2f) * (sunset * amount * 0.85f),
                Glow = Rgb.Lerp(SunGlow, SunsetWarm * 1.1f, sunset),
                GlowStrength = SpaceGlow * glowScale * (1f - amount) + airGlow * amount,
                SunTint = Rgb.Lerp(Rgb.White, SunsetLight, sunset * amount),
                SunsetAmount = sunset,
            };
        }
    }
}
