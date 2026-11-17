using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// Paints a body's whole surface onto one flat picture, the way a world map shows the Earth. From far away
    /// a body is drawn as a smooth ball wearing this picture; close up, the real ground takes over. Because both
    /// come from the same rules, the canyon you saw from orbit is the canyon you land in. Pure sums, no Unity
    /// types, so it can run on a background thread.
    /// </summary>
    public static class FarMap
    {
        /// <summary>
        /// The direction (from the body's centre) that one spot on the picture shows. <paramref name="u"/> runs
        /// 0 to 1 round the equator; <paramref name="v"/> runs 0 at the south pole to 1 at the north.
        /// </summary>
        public static Vec3d Direction(double u, double v)
        {
            double polar = (1 - v) * Math.PI;
            double azimuth = u * Math.PI * 2;
            double ring = Math.Sin(polar);
            return new Vec3d(ring * Math.Cos(azimuth), Math.Cos(polar), ring * Math.Sin(azimuth));
        }

        /// <summary>Where on the picture a direction lands. The opposite of <see cref="Direction"/>.</summary>
        public static void Locate(Vec3d direction, out double u, out double v)
        {
            u = Math.Atan2(direction.Z, direction.X) / (Math.PI * 2);
            if (u < 0) u += 1;
            v = 1 - Math.Acos(Math.Max(-1, Math.Min(1, direction.Y))) / Math.PI;
        }

        /// <summary>
        /// The picture as red, green, blue, alpha bytes, bottom row first, <paramref name="width"/> across and
        /// half that high. Rows <paramref name="firstRow"/> up to (not including) <paramref name="endRow"/> are
        /// filled, so several threads can share the work or one can do it in slices.
        /// </summary>
        public static void Paint(PlanetTerrain terrain, int width, byte[] pixels, int firstRow, int endRow)
        {
            int height = width / 2;
            PlanetProfile profile = terrain.Profile;
            // One dot of the picture covers this much ground at the equator. Bumps smaller than that cannot show.
            double dot = terrain.Radius * Math.PI * 2 / width;
            double step = Math.Max(1.5, dot * 0.5);
            float[] sea = ColorUtil.Parse(profile.seaColor);
            float[] deepSea = ColorUtil.Parse(profile.seaDeepColor);

            for (int y = firstRow; y < endRow && y < height; y++)
            {
                double v = (y + 0.5) / height;
                for (int x = 0; x < width; x++)
                {
                    Vec3d dir = Direction((x + 0.5) / width, v);
                    TerrainPoint point = terrain.Sample(dir, dot * 1.6);
                    // Bumps too small for one dot of the picture cannot show in it, so they are not paid for.
                    double slope = terrain.SlopeAt(dir, step, step);
                    float r, g, b;
                    if (terrain.HasSea && point.Height < terrain.SeaLevel)
                    {
                        // Looking down on water: lighter over the shallows, darker over the deeps.
                        float deep = (float)Noise3.Smooth(0, 22, terrain.SeaLevel - point.Height);
                        r = sea[0] + (deepSea[0] - sea[0]) * deep;
                        g = sea[1] + (deepSea[1] - sea[1]) * deep;
                        b = sea[2] + (deepSea[2] - sea[2]) * deep;
                    }
                    else
                    {
                        terrain.ColorAt(dir, point, slope, out r, out g, out b);
                        // Steep ground reads a little darker, which is what makes mountains and crater walls
                        // show up from orbit.
                        float shade = (float)(1.04 - 0.3 * Noise3.Smooth(0.08, 0.7, slope));
                        r *= shade;
                        g *= shade;
                        b *= shade;
                    }
                    int o = (y * width + x) * 4;
                    pixels[o] = ToByte(r);
                    pixels[o + 1] = ToByte(g);
                    pixels[o + 2] = ToByte(b);
                    pixels[o + 3] = 255;
                }
            }
        }

        public static byte[] Paint(PlanetTerrain terrain, int width)
        {
            var pixels = new byte[width * (width / 2) * 4];
            Paint(terrain, width, pixels, 0, width / 2);
            return pixels;
        }

        static byte ToByte(float value)
        {
            int i = (int)(value * 255f + 0.5f);
            return (byte)(i < 0 ? 0 : i > 255 ? 255 : i);
        }
    }
}
