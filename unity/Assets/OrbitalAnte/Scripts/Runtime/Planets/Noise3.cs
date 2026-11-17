using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// Smooth random patterns in 3D, used to shape and colour every planet. The same input always gives the same
    /// output on every machine (whole-number hashing, ordinary arithmetic), and nothing here keeps any state, so
    /// it is safe to call from several threads at once. No Unity types.
    ///
    /// Points are given in metres. "Wavelength" is roughly the distance from one bump to the next.
    /// </summary>
    public static class Noise3
    {
        static uint Hash(int x, int y, int z, uint seed)
        {
            unchecked
            {
                uint h = seed + (uint)x * 374761393u + (uint)y * 668265263u + (uint)z * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                h *= 2654435761u;
                return h ^ (h >> 15);
            }
        }

        static double Dot(uint hash, double x, double y, double z)
        {
            // One of twelve directions: the middles of a cube's edges.
            switch (hash % 12u)
            {
                case 0: return x + y;
                case 1: return -x + y;
                case 2: return x - y;
                case 3: return -x - y;
                case 4: return x + z;
                case 5: return -x + z;
                case 6: return x - z;
                case 7: return -x - z;
                case 8: return y + z;
                case 9: return -y + z;
                case 10: return y - z;
                default: return -y - z;
            }
        }

        static double Fade(double t) => t * t * t * (t * (t * 6.0 - 15.0) + 10.0);

        /// <summary>Smooth noise from about -1 to 1. One bump per unit of distance.</summary>
        public static double Perlin(double x, double y, double z, uint seed)
        {
            double fx = Math.Floor(x), fy = Math.Floor(y), fz = Math.Floor(z);
            int xi = (int)fx, yi = (int)fy, zi = (int)fz;
            double dx = x - fx, dy = y - fy, dz = z - fz;
            double u = Fade(dx), v = Fade(dy), w = Fade(dz);

            double n000 = Dot(Hash(xi, yi, zi, seed), dx, dy, dz);
            double n100 = Dot(Hash(xi + 1, yi, zi, seed), dx - 1, dy, dz);
            double n010 = Dot(Hash(xi, yi + 1, zi, seed), dx, dy - 1, dz);
            double n110 = Dot(Hash(xi + 1, yi + 1, zi, seed), dx - 1, dy - 1, dz);
            double n001 = Dot(Hash(xi, yi, zi + 1, seed), dx, dy, dz - 1);
            double n101 = Dot(Hash(xi + 1, yi, zi + 1, seed), dx - 1, dy, dz - 1);
            double n011 = Dot(Hash(xi, yi + 1, zi + 1, seed), dx, dy - 1, dz - 1);
            double n111 = Dot(Hash(xi + 1, yi + 1, zi + 1, seed), dx - 1, dy - 1, dz - 1);

            double x00 = n000 + (n100 - n000) * u;
            double x10 = n010 + (n110 - n010) * u;
            double x01 = n001 + (n101 - n001) * u;
            double x11 = n011 + (n111 - n011) * u;
            double y0 = x00 + (x10 - x00) * v;
            double y1 = x01 + (x11 - x01) * v;
            return (y0 + (y1 - y0) * w) * 1.05;
        }

        public static double Perlin(Vec3d p, double wavelength, uint seed)
        {
            double f = 1.0 / wavelength;
            return Perlin(p.X * f, p.Y * f, p.Z * f, seed);
        }

        /// <summary>
        /// Several layers of noise, each half the size and half the strength of the last. About -1 to 1.
        /// Layers finer than <paramref name="minWavelength"/> are left out, which is how far-away ground is
        /// worked out cheaply: the broad shape is identical, only the fine bumps are missing.
        /// </summary>
        public static double Fbm(Vec3d p, double wavelength, int layers, uint seed, double minWavelength = 0)
        {
            double total = 0, weight = 1, sum = 0;
            for (int i = 0; i < layers; i++)
            {
                sum += weight;
                if (wavelength >= minWavelength) total += Perlin(p, wavelength, seed + (uint)i * 101u) * weight;
                wavelength *= 0.49;
                weight *= 0.5;
            }
            return total / sum;
        }

        /// <summary>Layered noise folded into sharp ridges, like mountain crests. 0 to about 1.</summary>
        public static double Ridged(Vec3d p, double wavelength, int layers, uint seed, double minWavelength = 0)
        {
            double total = 0, weight = 1, sum = 0, previous = 1;
            for (int i = 0; i < layers; i++)
            {
                sum += weight;
                if (wavelength >= minWavelength)
                {
                    double n = 1.0 - Math.Abs(Perlin(p, wavelength, seed + (uint)i * 131u));
                    n *= n;
                    total += n * weight * previous;
                    previous = n;
                }
                wavelength *= 0.49;
                weight *= 0.5;
            }
            return total / sum;
        }

        static void Grad(uint hash, out double gx, out double gy, out double gz)
        {
            // The same twelve directions as Dot, as vectors.
            switch (hash % 12u)
            {
                case 0: gx = 1; gy = 1; gz = 0; break;
                case 1: gx = -1; gy = 1; gz = 0; break;
                case 2: gx = 1; gy = -1; gz = 0; break;
                case 3: gx = -1; gy = -1; gz = 0; break;
                case 4: gx = 1; gy = 0; gz = 1; break;
                case 5: gx = -1; gy = 0; gz = 1; break;
                case 6: gx = 1; gy = 0; gz = -1; break;
                case 7: gx = -1; gy = 0; gz = -1; break;
                case 8: gx = 0; gy = 1; gz = 1; break;
                case 9: gx = 0; gy = -1; gz = 1; break;
                case 10: gx = 0; gy = 1; gz = -1; break;
                default: gx = 0; gy = -1; gz = -1; break;
            }
        }

        /// <summary>
        /// The same value as <see cref="Perlin(double,double,double,uint)"/>, and also which way it climbs and how
        /// fast (per unit of distance), worked out exactly instead of by sampling nearby.
        /// </summary>
        public static double PerlinD(double x, double y, double z, uint seed, out double ddx, out double ddy, out double ddz)
        {
            double fx = Math.Floor(x), fy = Math.Floor(y), fz = Math.Floor(z);
            int xi = (int)fx, yi = (int)fy, zi = (int)fz;
            double dx = x - fx, dy = y - fy, dz = z - fz;
            double u = Fade(dx), v = Fade(dy), w = Fade(dz);
            double du = 30.0 * dx * dx * (dx - 1) * (dx - 1), dv = 30.0 * dy * dy * (dy - 1) * (dy - 1), dw = 30.0 * dz * dz * (dz - 1) * (dz - 1);

            Grad(Hash(xi, yi, zi, seed), out double ax, out double ay, out double az);
            Grad(Hash(xi + 1, yi, zi, seed), out double bx, out double by, out double bz);
            Grad(Hash(xi, yi + 1, zi, seed), out double cx, out double cy, out double cz);
            Grad(Hash(xi + 1, yi + 1, zi, seed), out double ex, out double ey, out double ez);
            Grad(Hash(xi, yi, zi + 1, seed), out double gx, out double gy, out double gz);
            Grad(Hash(xi + 1, yi, zi + 1, seed), out double hx, out double hy, out double hz);
            Grad(Hash(xi, yi + 1, zi + 1, seed), out double ix, out double iy, out double iz);
            Grad(Hash(xi + 1, yi + 1, zi + 1, seed), out double jx, out double jy, out double jz);

            double n000 = ax * dx + ay * dy + az * dz;
            double n100 = bx * (dx - 1) + by * dy + bz * dz;
            double n010 = cx * dx + cy * (dy - 1) + cz * dz;
            double n110 = ex * (dx - 1) + ey * (dy - 1) + ez * dz;
            double n001 = gx * dx + gy * dy + gz * (dz - 1);
            double n101 = hx * (dx - 1) + hy * dy + hz * (dz - 1);
            double n011 = ix * dx + iy * (dy - 1) + iz * (dz - 1);
            double n111 = jx * (dx - 1) + jy * (dy - 1) + jz * (dz - 1);

            double k1 = n100 - n000, k2 = n010 - n000, k3 = n001 - n000;
            double k4 = n000 - n100 - n010 + n110, k5 = n000 - n010 - n001 + n011, k6 = n000 - n100 - n001 + n101;
            double k7 = -n000 + n100 + n010 - n110 + n001 - n101 - n011 + n111;

            // The corners' own directions, blended the same way as the values.
            double w000 = (1 - u) * (1 - v) * (1 - w), w100 = u * (1 - v) * (1 - w), w010 = (1 - u) * v * (1 - w), w110 = u * v * (1 - w);
            double w001 = (1 - u) * (1 - v) * w, w101 = u * (1 - v) * w, w011 = (1 - u) * v * w, w111 = u * v * w;
            ddx = ax * w000 + bx * w100 + cx * w010 + ex * w110 + gx * w001 + hx * w101 + ix * w011 + jx * w111
                + du * (k1 + k4 * v + k6 * w + k7 * v * w);
            ddy = ay * w000 + by * w100 + cy * w010 + ey * w110 + gy * w001 + hy * w101 + iy * w011 + jy * w111
                + dv * (k2 + k5 * w + k4 * u + k7 * w * u);
            ddz = az * w000 + bz * w100 + cz * w010 + ez * w110 + gz * w001 + hz * w101 + iz * w011 + jz * w111
                + dw * (k3 + k6 * u + k5 * v + k7 * u * v);
            ddx *= 1.05; ddy *= 1.05; ddz *= 1.05;
            return (n000 + k1 * u + k2 * v + k3 * w + k4 * u * v + k5 * v * w + k6 * w * u + k7 * u * v * w) * 1.05;
        }

        /// <summary>
        /// Layered noise shaped the way running water shapes land: where the broad layers are steep the finer ones
        /// are held back, so slopes run clean down to smooth valley floors while crests and gentle ground keep
        /// their small bumps. <paramref name="erosion"/> 0 is plain <see cref="Fbm"/>; about 1 is strongly worn.
        /// About -1 to 1. Layers finer than <paramref name="minWavelength"/> are left out.
        /// </summary>
        public static double Eroded(Vec3d p, double wavelength, int layers, uint seed, double erosion, double minWavelength = 0)
        {
            double total = 0, weight = 1, sum = 0;
            double sx = 0, sy = 0, sz = 0;
            for (int i = 0; i < layers; i++)
            {
                sum += weight;
                if (wavelength >= minWavelength)
                {
                    double f = 1.0 / wavelength;
                    double n = PerlinD(p.X * f, p.Y * f, p.Z * f, seed + (uint)i * 101u, out double gx, out double gy, out double gz);
                    // Slopes are compared in the first layer's units, so the answer does not depend on the size chosen.
                    // Each layer is held back by the slope of the broader ones above it, so the broadest keeps its
                    // full height and only the detail thins out on the flanks.
                    total += n * weight / (1.0 + erosion * (sx * sx + sy * sy + sz * sz));
                    sx += gx * weight; sy += gy * weight; sz += gz * weight;
                }
                wavelength *= 0.49;
                weight *= 0.5;
            }
            return total / sum;
        }

        /// <summary>
        /// Mountain crests with spurs running down from them: sharp ridges like <see cref="Ridged"/>, with the
        /// finer layers held back on steep flanks (see <see cref="Eroded"/>) so the sides fall in clean ribs and
        /// gullies instead of an even crumple. 0 to about 1.
        /// </summary>
        public static double ErodedRidged(Vec3d p, double wavelength, int layers, uint seed, double erosion, double minWavelength = 0)
        {
            double total = 0, weight = 1, sum = 0, previous = 1;
            double sx = 0, sy = 0, sz = 0;
            for (int i = 0; i < layers; i++)
            {
                sum += weight;
                if (wavelength >= minWavelength)
                {
                    double f = 1.0 / wavelength;
                    double n = PerlinD(p.X * f, p.Y * f, p.Z * f, seed + (uint)i * 131u, out double gx, out double gy, out double gz);
                    double fold = 1.0 - Math.Abs(n);
                    // The fold turns the slope round on the far side of a crest; squaring steepens it toward the top.
                    double k = (n > 0 ? -2.0 : 2.0) * fold * weight;
                    fold *= fold;
                    // Held back by the slope of the broader layers only, so the main crests keep their full height.
                    total += fold * weight * previous / (1.0 + erosion * (sx * sx + sy * sy + sz * sz));
                    sx += gx * k; sy += gy * k; sz += gz * k;
                    previous = 0.35 + 0.65 * fold;
                }
                wavelength *= 0.49;
                weight *= 0.5;
            }
            return total / sum;
        }

        /// <summary>
        /// A pattern of cells, like cracked mud or a honeycomb. Returns the distance to the nearest cell centre
        /// (0 at the centre, about 1 at the furthest corner). <paramref name="edge"/> is 0 on the border between
        /// two cells and grows toward the middle. <paramref name="id"/> is a random number that is the same
        /// everywhere inside one cell.
        /// </summary>
        public static double Cells(Vec3d p, double cellSize, uint seed, out double edge, out uint id)
        {
            double f = 1.0 / cellSize;
            double x = p.X * f, y = p.Y * f, z = p.Z * f;
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y), zi = (int)Math.Floor(z);
            double best = 1e9, second = 1e9;
            id = 0;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        uint h = Hash(xi + dx, yi + dy, zi + dz, seed);
                        double cx = xi + dx + (h & 1023u) / 1023.0;
                        double cy = yi + dy + ((h >> 10) & 1023u) / 1023.0;
                        double cz = zi + dz + ((h >> 20) & 1023u) / 1023.0;
                        double d = (cx - x) * (cx - x) + (cy - y) * (cy - y) + (cz - z) * (cz - z);
                        if (d < best)
                        {
                            second = best;
                            best = d;
                            id = h;
                        }
                        else if (d < second)
                        {
                            second = d;
                        }
                    }
                }
            }
            best = Math.Sqrt(best);
            edge = Math.Sqrt(second) - best;
            return best;
        }

        /// <summary>A random number from 0 to 1 that depends only on the numbers given.</summary>
        public static double Hash01(int a, int b, int c, uint seed) => (Hash(a, b, c, seed) & 0xFFFFFFu) / 16777215.0;

        public static uint SeedFrom(string text, int seed = 0)
        {
            unchecked
            {
                uint h = 2166136261u ^ (uint)seed * 16777619u;
                if (text != null)
                {
                    for (int i = 0; i < text.Length; i++) h = (h ^ text[i]) * 16777619u;
                }
                return h;
            }
        }

        public static double Smooth(double from, double to, double x)
        {
            if (from == to) return x < from ? 0 : 1;
            double t = (x - from) / (to - from);
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            return t * t * (3.0 - 2.0 * t);
        }

        public static double Clamp01(double x) => x < 0 ? 0 : x > 1 ? 1 : x;
        public static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }
}
