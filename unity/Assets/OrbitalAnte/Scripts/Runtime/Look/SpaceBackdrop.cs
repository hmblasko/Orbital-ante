using System;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;

namespace OrbitalAnte.Look
{
    /// <summary>
    /// Paints the picture of deep space that sits behind the stars: a soft band of galaxy light crossed by dark
    /// dust, and a few faint coloured clouds (teal, violet, rose). Deliberately dim, so the stars and planets
    /// stay the brightest things in the sky. Plain sums with no Unity types, safe to run on a background thread.
    /// </summary>
    public static class SpaceBackdrop
    {
        /// <summary>
        /// One colour per dot, four bytes each (red, green, blue, 255), wrapped round the whole sky: left to right
        /// is once round the sky; the first row is straight up and the last row straight down.
        /// </summary>
        public static byte[] Paint(int width, int height, uint seed)
        {
            var pixels = new byte[width * height * 4];
            // The galaxy's band lies along a tilted circle round the sky.
            Vec3d tilt = new Vec3d(0.35, 0.87, 0.35).Normalized;
            for (int py = 0; py < height; py++)
            {
                double polar = (py + 0.5) / height * Math.PI;
                double ring = Math.Sin(polar), y = Math.Cos(polar);
                for (int px = 0; px < width; px++)
                {
                    double around = ((px + 0.5) / width - 0.5) * Math.PI * 2.0;
                    var d = new Vec3d(ring * Math.Cos(around), y, ring * Math.Sin(around));
                    Sample(d, tilt, seed, out double r, out double g, out double b);
                    // A sprinkle of noise smaller than one colour step hides the stripes dark gradients would show.
                    double dither = (Noise3.Hash01(px, py, 7, seed) - 0.5) / 255.0;
                    int o = (py * width + px) * 4;
                    pixels[o] = ToByte(r + dither);
                    pixels[o + 1] = ToByte(g + dither);
                    pixels[o + 2] = ToByte(b + dither);
                    pixels[o + 3] = 255;
                }
            }
            return pixels;
        }

        /// <summary>The colour of deep space in one direction (a direction of length 1). Each part is 0 to 1.</summary>
        public static void Sample(Vec3d d, Vec3d tilt, uint seed, out double r, out double g, out double b)
        {
            double off = Vec3d.Dot(d, tilt);
            double band = Math.Exp(-off * off / 0.07);
            double dust = 0.5 + 0.5 * Noise3.Fbm(d, 0.9, 5, seed);
            double lanes = Noise3.Ridged(d, 0.55, 4, seed + 7u);
            double galaxy = band * (0.3 + 0.7 * dust) * (1.0 - 0.55 * lanes);

            double teal = Cloud(d, 1.3, seed + 21u, 0.52, 0.86);
            double violet = Cloud(d, 1.1, seed + 43u, 0.55, 0.88);
            double rose = Cloud(d, 0.8, seed + 65u, 0.62, 0.9);

            r = 0.006 + galaxy * 0.095 + teal * 0.016 + violet * 0.07 + rose * 0.1;
            g = 0.007 + galaxy * 0.082 + teal * 0.075 + violet * 0.03 + rose * 0.036;
            b = 0.014 + galaxy * 0.12 + teal * 0.09 + violet * 0.115 + rose * 0.055;
        }

        static double Cloud(Vec3d d, double size, uint seed, double from, double to)
        {
            return Noise3.Smooth(from, to, 0.5 + 0.5 * Noise3.Fbm(d, size, 5, seed));
        }

        static byte ToByte(double x)
        {
            return (byte)Math.Max(0, Math.Min(255, (int)Math.Round(x * 255.0)));
        }
    }
}
