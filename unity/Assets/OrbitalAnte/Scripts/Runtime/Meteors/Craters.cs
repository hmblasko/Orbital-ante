using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Meteors
{
    /// <summary>One crater left by a strike, in the body's space.</summary>
    public struct CraterSite
    {
        /// <summary>Unit direction from the body's centre to the crater's middle.</summary>
        public Vec3d Up;
        /// <summary>Radius to the top of the rim, in metres.</summary>
        public double Radius;
        /// <summary>Height of the rim above the plain ground, in metres.</summary>
        public double Rim;
        /// <summary>How far the thrown-out ground reaches, in crater radii.</summary>
        public double Apron;
        /// <summary>Picks the lumps and rays, and names the crater (unique per run).</summary>
        public int Seed;
        /// <summary>Made on a world with air: scorched dark. Without air: bright fresh rays, like the Moon's young craters.</summary>
        public bool Air;

        public static CraterSite Make(MeteorRules r, Vec3d up, double radius, int seed, bool air)
        {
            return new CraterSite
            {
                Up = up.Normalized,
                Radius = radius,
                Rim = radius * Math.Max(0.02, r.rimHeight),
                Apron = Math.Max(1.6, r.apron),
                Seed = seed,
                Air = air,
            };
        }
    }

    /// <summary>
    /// The shape and colour of a crater. The game's ground cannot be dug into (it is worked out from rules and
    /// shared by many threads), so a crater is built on top of it: a floor laid just over the ground, a raised
    /// rim, and thrown-out ground that sinks under the plain ground at its edge so no seam shows. Young craters on
    /// real airless worlds look like this, a raised ring round a shallow floor.
    /// </summary>
    public static class CraterShape
    {
        /// <summary>The floor sits this far over the ground, in metres, so the ground never shows through it.</summary>
        public const double FloorLift = 0.07;
        /// <summary>The edge of the thrown-out ground sinks this far under the ground, in metres.</summary>
        public const double Sink = 0.4;

        /// <summary>Height over the plain ground at a distance from the middle (metres along the ground) and a bearing.</summary>
        public static double Height(in CraterSite c, double distance, double bearing, bool lumps = true)
        {
            double rho = distance / Math.Max(0.1, c.Radius);
            double h = c.Rim;
            double y;
            if (rho < 0.5) y = FloorLift;
            else if (rho < 1)
            {
                // The inner wall: curved, steepest just under the rim.
                double x = (rho - 0.5) / 0.5;
                y = FloorLift + (h - FloorLift) * Math.Pow(Smooth(x), 1.6);
            }
            else
            {
                y = h * Math.Exp(-3.2 * (rho - 1));
                if (lumps && rho < 1.7) y += h * 0.28 * Math.Sin(Math.PI * (rho - 1) / 0.7) * Wave(c.Seed, bearing, 5, 9, 14);
            }
            double edge = c.Apron - 0.6;
            if (rho > edge) y -= Sink * Smooth((rho - edge) / 0.6);
            return y;
        }

        /// <summary>How much a crater lifts the ground at a direction (0 outside it). For anything that stands on the ground by rule.</summary>
        public static double Lift(in CraterSite c, Vec3d direction, double surfaceRadius)
        {
            double dot = Vec3d.Dot(c.Up, direction);
            // Cheap test first (enemies ask this every step): cos(a) >= 1 - a^2/2, so anything below that is
            // certainly outside the apron and needs no arc cosine.
            double reach = c.Radius * (c.Apron - 0.6) / Math.Max(1e-6, surfaceRadius);
            if (dot <= 0 || dot < 1 - reach * reach * 0.5) return 0;
            double d = Math.Acos(Math.Min(1, dot)) * surfaceRadius;
            if (d >= c.Radius * (c.Apron - 0.6)) return 0;
            return Math.Max(0, Height(c, d, 0, false));
        }

        /// <summary>Streaks of thrown-out ground, 0 to 1, fading with distance from the rim.</summary>
        public static double Ray(in CraterSite c, double rho, double bearing)
        {
            if (rho < 0.95 || rho >= c.Apron) return 0;
            double w = Wave(c.Seed + 17, bearing, 7, 13, 23);
            double ray = Math.Max(0, w + 0.15);
            ray *= ray;
            double fade = rho < 1.15 ? 1 : 1 - (rho - 1.15) / Math.Max(0.1, c.Apron - 1.15);
            return Math.Min(1, ray * 1.6) * Math.Max(0, fade);
        }

        /// <summary>
        /// The crater's colour at a point, from the plain ground's colour there. <paramref name="heat"/> is how hot
        /// the floor is when fresh, 0 to 1 (it glows, then cools).
        /// </summary>
        public static void Colour(in CraterSite c, double rho, double bearing, float gr, float gg, float gb,
                                  out float r, out float g, out float b, out float heat)
        {
            double lum = 0.3 * gr + 0.59 * gg + 0.11 * gb;
            double fresh = Math.Min(0.72, lum * 1.45 + 0.05);
            // The floor: scorched (air) or dark glassy melt (no air), warmer toward the middle where it is hottest.
            double t = Smooth((0.75 - rho) / 0.75);
            double fr, fg, fb;
            if (c.Air)
            {
                fr = Lerp(gr * 0.5, 0.075, 0.5 + 0.5 * t);
                fg = Lerp(gg * 0.5, 0.062, 0.5 + 0.5 * t);
                fb = Lerp(gb * 0.5, 0.055, 0.5 + 0.5 * t);
            }
            else
            {
                fr = Lerp(gr * 0.62, 0.13, 0.4 * t);
                fg = Lerp(gg * 0.62, 0.12, 0.4 * t);
                fb = Lerp(gb * 0.62, 0.11, 0.4 * t);
            }
            double warm = t * 0.65;
            fr = Lerp(fr, 0.17, warm);
            fg = Lerp(fg, 0.075, warm);
            fb = Lerp(fb, 0.035, warm);

            // The inner wall: burnt (air) or freshly dug, paler rock (no air).
            double wr, wg, wb;
            if (c.Air) { wr = gr * 0.55; wg = gg * 0.55; wb = gb * 0.55; }
            else { wr = Lerp(gr, fresh, 0.55); wg = Lerp(gg, fresh, 0.55); wb = Lerp(gb, fresh * 0.98, 0.55); }

            // Thrown-out ground: dark soot streaks, or pale rays.
            double ray = Ray(c, rho, bearing);
            double near = rho < 1 ? 1 : Math.Max(0, 1 - (rho - 1) / 0.5);
            double er, eg, eb;
            if (c.Air)
            {
                double k = 1 - 0.45 * ray - 0.25 * near;
                er = gr * k; eg = gg * k; eb = gb * k;
            }
            else
            {
                double k = Math.Min(1, 0.7 * ray + 0.35 * near);
                er = Lerp(gr, fresh, k); eg = Lerp(gg, fresh, k); eb = Lerp(gb, fresh * 0.98, k);
            }
            // The far edge fades back into the plain ground.
            double home = Smooth((rho - (c.Apron - 0.7)) / 0.7);
            er = Lerp(er, gr, home); eg = Lerp(eg, gg, home); eb = Lerp(eb, gb, home);

            double toWall = Smooth((rho - 0.6) / 0.25);
            double toEjecta = Smooth((rho - 0.95) / 0.15);
            double cr = Lerp(Lerp(fr, wr, toWall), er, toEjecta);
            double cg = Lerp(Lerp(fg, wg, toWall), eg, toEjecta);
            double cb = Lerp(Lerp(fb, wb, toWall), eb, toEjecta);
            // Never brighter than pale sand, like the rest of the ground.
            r = (float)Math.Min(0.78, cr);
            g = (float)Math.Min(0.78, cg);
            b = (float)Math.Min(0.78, cb);
            heat = rho < 0.6 ? (float)Math.Pow(1 - rho / 0.6, 1.5) : 0f;
        }

        /// <summary>A smooth wave round the crater from three sines (-1 to 1), fixed by the seed.</summary>
        static double Wave(int seed, double bearing, int k1, int k2, int k3)
        {
            uint h = (uint)seed * 2654435761u;
            double p1 = (h & 1023) / 1023.0 * 6.283, p2 = ((h >> 10) & 1023) / 1023.0 * 6.283, p3 = ((h >> 20) & 1023) / 1023.0 * 6.283;
            return 0.5 * Math.Sin(k1 * bearing + p1) + 0.3 * Math.Sin(k2 * bearing + p2) + 0.2 * Math.Sin(k3 * bearing + p3);
        }

        internal static double Smooth(double x)
        {
            x = x < 0 ? 0 : x > 1 ? 1 : x;
            return x * x * (3 - 2 * x);
        }

        static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }

    /// <summary>The ground's height and colour in a direction. Called from a background thread.</summary>
    public delegate void GroundSampler(Vec3d direction, out double radius, out float r, out float g, out float b);

    /// <summary>A crater's mesh, ready for the engine: corners relative to <see cref="Origin"/>, in the body's space.</summary>
    public sealed class CraterMeshData
    {
        public CraterSite Site;
        /// <summary>The crater's middle on the ground, in the body's space.</summary>
        public Vec3d Origin;
        public float[] Positions;
        /// <summary>"Up" at each corner (the ground shader reads it from the first texture channel).</summary>
        public float[] Directions;
        /// <summary>Colours, four numbers a corner. The fourth is 1 minus the heat (the shader glows where it is low).</summary>
        public float[] Colours;
        public int[] Triangles;
        public int Corners => Positions.Length / 3;
    }

    /// <summary>Builds a crater's mesh over the ground. Plain C#, safe on a background thread.</summary>
    public static class CraterMesher
    {
        public static CraterMeshData Build(CraterSite site, GroundSampler ground, double surfaceRadius)
        {
            CraterSite c = site;
            var rings = new List<double>();
            for (double rho = 0.12; rho < c.Apron;)
            {
                rings.Add(rho);
                rho += rho > 0.4 && rho < 1.45 ? 0.075 : 0.17;
            }
            rings.Add(c.Apron);
            int spokes = Math.Max(24, Math.Min(48, (int)Math.Round(c.Radius * 9)));
            int corners = 1 + rings.Count * spokes;

            Vec3d up = c.Up;
            Vec3d e1 = up.AnyPerpendicular();
            Vec3d e2 = Vec3d.Cross(up, e1);
            ground(up, out double centreRadius, out float cr0, out float cg0, out float cb0);
            var origin = up * centreRadius;

            var positions = new float[corners * 3];
            var dirs = new float[corners * 3];
            var colours = new float[corners * 4];
            var places = new Vec3d[corners];

            Put(0, up, centreRadius + CraterShape.Height(c, 0, 0), 0, 0, cr0, cg0, cb0);
            for (int k = 0; k < rings.Count; k++)
            {
                double rho = rings[k];
                double arc = rho * c.Radius / surfaceRadius;
                for (int s = 0; s < spokes; s++)
                {
                    double bearing = s * Math.PI * 2 / spokes;
                    Vec3d dir = (up * Math.Cos(arc) + (e1 * Math.Cos(bearing) + e2 * Math.Sin(bearing)) * Math.Sin(arc)).Normalized;
                    ground(dir, out double gr, out float r, out float g, out float b);
                    double h = CraterShape.Height(c, rho * c.Radius, bearing);
                    Put(1 + k * spokes + s, dir, gr + h, rho, bearing, r, g, b);
                }
            }

            var tris = new List<int>(spokes * 3 + rings.Count * spokes * 6);
            for (int s = 0; s < spokes; s++) Tri(tris, places, up, 0, 1 + s, 1 + (s + 1) % spokes);
            for (int k = 0; k + 1 < rings.Count; k++)
                for (int s = 0; s < spokes; s++)
                {
                    int a = 1 + k * spokes + s, b = 1 + k * spokes + (s + 1) % spokes;
                    int c2 = a + spokes, d = b + spokes;
                    Tri(tris, places, up, a, c2, b);
                    Tri(tris, places, up, b, c2, d);
                }

            return new CraterMeshData { Site = c, Origin = origin, Positions = positions, Directions = dirs, Colours = colours, Triangles = tris.ToArray() };

            void Put(int i, Vec3d dir, double radius, double rho, double bearing, float r, float g, float b)
            {
                Vec3d p = dir * radius;
                places[i] = p;
                Vec3d local = p - origin;
                positions[i * 3] = (float)local.X;
                positions[i * 3 + 1] = (float)local.Y;
                positions[i * 3 + 2] = (float)local.Z;
                dirs[i * 3] = (float)dir.X;
                dirs[i * 3 + 1] = (float)dir.Y;
                dirs[i * 3 + 2] = (float)dir.Z;
                CraterShape.Colour(c, rho, bearing, r, g, b, out float cr, out float cg, out float cb, out float heat);
                colours[i * 4] = cr;
                colours[i * 4 + 1] = cg;
                colours[i * 4 + 2] = cb;
                colours[i * 4 + 3] = 1f - 0.9f * heat;
            }
        }

        /// <summary>Adds a triangle facing up out of the ground (the engine draws the side whose corners run clockwise).</summary>
        static void Tri(List<int> tris, Vec3d[] p, Vec3d up, int a, int b, int c)
        {
            Vec3d n = Vec3d.Cross(p[b] - p[a], p[c] - p[a]);
            tris.Add(a);
            if (Vec3d.Dot(n, up) >= 0) { tris.Add(b); tris.Add(c); }
            else { tris.Add(c); tris.Add(b); }
        }
    }

    /// <summary>
    /// The craters on each world, for anything that stands on the ground by rule rather than by collider (the
    /// enemies' footing), so they walk over a rim instead of through it. Main thread only.
    /// </summary>
    public static class CraterRegistry
    {
        static readonly Dictionary<string, List<CraterSite>> Sites = new Dictionary<string, List<CraterSite>>();

        public static int Count
        {
            get
            {
                int n = 0;
                foreach (var list in Sites.Values) n += list.Count;
                return n;
            }
        }

        public static void Add(string body, in CraterSite site)
        {
            if (body == null) return;
            if (!Sites.TryGetValue(body, out List<CraterSite> list)) Sites[body] = list = new List<CraterSite>();
            list.Add(site);
        }

        public static void Remove(string body, int seed)
        {
            if (body == null || !Sites.TryGetValue(body, out List<CraterSite> list)) return;
            list.RemoveAll(s => s.Seed == seed);
        }

        public static void Clear() => Sites.Clear();

        /// <summary>How much the craters lift the ground in a direction on a body, in metres.</summary>
        public static double LiftAt(string body, Vec3d direction, double surfaceRadius)
        {
            if (body == null || !Sites.TryGetValue(body, out List<CraterSite> list) || list.Count == 0) return 0;
            double lift = 0;
            for (int i = 0; i < list.Count; i++) lift = Math.Max(lift, CraterShape.Lift(list[i], direction, surfaceRadius));
            return lift;
        }
    }
}
