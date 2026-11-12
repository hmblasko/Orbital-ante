using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// Turns a cave's route (see <see cref="CavePath"/>) into walls: a bumpy tube seen from the inside, with a
    /// funnel at each opening that joins it to the hole cut in the ground. Pure sums, no Unity types.
    /// Positions are metres from the body's centre.
    /// </summary>
    public static class CaveMesher
    {
        /// <summary>How far beyond the tunnel's own radius the funnel's rim reaches under the ground, in metres.</summary>
        public const double CollarReach = 7.0;
        const double CollarDepth = 0.5;

        public static ShapeData Build(Cave cave, PlanetTerrain terrain, int around = 14)
        {
            int count = cave.Centres.Length;
            double bodyRadius = terrain.Radius;
            uint seed = Noise3.SeedFrom(cave.Formation, count);

            // Leave out the circles at each opening that would poke up through the ground. The funnel takes over there.
            int first = 0;
            while (first < count - 2 && !Buried(cave, terrain, first)) first++;
            int last = count - 1;
            bool exit = cave.Mouths.Length > 1;
            if (exit)
            {
                while (last > first + 1 && !Buried(cave, terrain, last)) last--;
            }

            // A steady "up" for each circle, carried along the tunnel so the walls do not twist.
            var tangents = new Vec3d[count];
            for (int k = 0; k < count; k++)
            {
                Vec3d a = cave.Centres[Math.Max(0, k - 1)], b = cave.Centres[Math.Min(count - 1, k + 1)];
                tangents[k] = (b - a).Normalized;
            }
            var e = new Vec3d[count];
            var f = new Vec3d[count];
            e[first] = tangents[first].AnyPerpendicular();
            for (int k = first; k <= last; k++)
            {
                if (k > first)
                {
                    Vec3d carried = e[k - 1] - tangents[k] * Vec3d.Dot(e[k - 1], tangents[k]);
                    e[k] = carried.SqrMagnitude > 1e-9 ? carried.Normalized : tangents[k].AnyPerpendicular();
                }
                f[k] = Vec3d.Cross(tangents[k], e[k]).Normalized;
            }

            var positions = new List<float>();
            var colors = new List<float>();
            var triangles = new List<int>();
            var ringStart = new List<int>();
            var ringCentre = new List<Vec3d>();

            void AddRing(Func<double, Vec3d> pointAt, Vec3d centre, double shade)
            {
                ringStart.Add(positions.Count / 3);
                ringCentre.Add(centre);
                for (int j = 0; j < around; j++)
                {
                    Vec3d p = pointAt(j / (double)around * Math.PI * 2);
                    positions.Add((float)p.X);
                    positions.Add((float)p.Y);
                    positions.Add((float)p.Z);
                    double tone = shade * (0.85 + 0.25 * Noise3.Perlin(p, 2.7, seed + 3));
                    colors.Add((float)Math.Min(1, cave.R * tone));
                    colors.Add((float)Math.Min(1, cave.G * tone));
                    colors.Add((float)Math.Min(1, cave.B * tone));
                }
            }

            void AddCollar(Vec3d mouth, int ring)
            {
                // A wide circle lying just under the ground round the opening.
                Vec3d u = e[ring] - mouth * Vec3d.Dot(e[ring], mouth);
                u = u.SqrMagnitude > 1e-6 ? u.Normalized : mouth.AnyPerpendicular();
                Vec3d v = Vec3d.Cross(mouth, u).Normalized;
                // Keep the same handedness as the tunnel's own circles, or the funnel would be twisted inside out.
                if (Vec3d.Dot(v, f[ring]) < 0) v = -v;
                Vec3d top = mouth * (bodyRadius + terrain.Height(mouth));
                double reach = cave.MouthRadius + CollarReach;
                AddRing(angle =>
                {
                    Vec3d dir = (top + (u * Math.Cos(angle) + v * Math.Sin(angle)) * reach).Normalized;
                    return dir * (bodyRadius + terrain.Height(dir) - CollarDepth);
                }, top, 1.0);
            }

            void AddTube(int k)
            {
                Vec3d c = cave.Centres[k];
                double radius = cave.Radii[k];
                double depth = bodyRadius + terrain.Height(c.Normalized) - c.Magnitude;
                // Darker the further you are from daylight.
                double shade = 1.0 - 0.45 * Noise3.Smooth(1, 9, depth);
                Vec3d eu = e[k], fv = f[k];
                AddRing(angle =>
                {
                    Vec3d outward = eu * Math.Cos(angle) + fv * Math.Sin(angle);
                    double bump = 1.0 + 0.14 * Noise3.Perlin(c + outward * radius, 3.5, seed);
                    return c + outward * (radius * bump);
                }, c, shade);
            }

            AddCollar(cave.Mouths[0], first);
            for (int k = first; k <= last; k++) AddTube(k);
            if (exit) AddCollar(cave.Mouths[1], last);

            for (int r = 0; r + 1 < ringStart.Count; r++)
            {
                int a0 = ringStart[r], b0 = ringStart[r + 1];
                // The walls must face inward, toward the middle of the tunnel.
                Vec3d pa = Point(positions, a0), pb = Point(positions, a0 + 1), pc = Point(positions, b0);
                Vec3d inward = (ringCentre[r] + ringCentre[r + 1]) * 0.5 - pa;
                bool flip = Vec3d.Dot(Vec3d.Cross(pb - pa, pc - pa), inward) < 0;
                for (int j = 0; j < around; j++)
                {
                    int j1 = (j + 1) % around;
                    int a = a0 + j, b = a0 + j1, c = b0 + j, d = b0 + j1;
                    if (flip)
                    {
                        triangles.Add(a); triangles.Add(c); triangles.Add(b);
                        triangles.Add(b); triangles.Add(c); triangles.Add(d);
                    }
                    else
                    {
                        triangles.Add(a); triangles.Add(b); triangles.Add(c);
                        triangles.Add(b); triangles.Add(d); triangles.Add(c);
                    }
                }
            }

            if (!exit)
            {
                // Close the dead end with a fan of triangles.
                int ring = ringStart[ringStart.Count - 1];
                Vec3d tip = cave.Centres[last] + tangents[last] * (cave.Radii[last] * 0.5);
                int tipIndex = positions.Count / 3;
                positions.Add((float)tip.X);
                positions.Add((float)tip.Y);
                positions.Add((float)tip.Z);
                colors.Add(cave.R * 0.5f);
                colors.Add(cave.G * 0.5f);
                colors.Add(cave.B * 0.5f);
                Vec3d pa = Point(positions, ring), pb = Point(positions, ring + 1);
                bool flip = Vec3d.Dot(Vec3d.Cross(pb - pa, tip - pa), cave.Centres[last] - tangents[last] * 2 - pa) < 0;
                for (int j = 0; j < around; j++)
                {
                    int j1 = (j + 1) % around;
                    if (flip)
                    {
                        triangles.Add(ring + j); triangles.Add(tipIndex); triangles.Add(ring + j1);
                    }
                    else
                    {
                        triangles.Add(ring + j); triangles.Add(ring + j1); triangles.Add(tipIndex);
                    }
                }
            }

            return new ShapeData { Positions = positions.ToArray(), Colors = colors.ToArray(), Triangles = triangles.ToArray() };
        }

        static Vec3d Point(List<float> positions, int index)
        {
            return new Vec3d(positions[index * 3], positions[index * 3 + 1], positions[index * 3 + 2]);
        }

        /// <summary>True if the whole circle is under the ground with a little to spare.</summary>
        static bool Buried(Cave cave, PlanetTerrain terrain, int k)
        {
            Vec3d c = cave.Centres[k];
            double surface = terrain.Radius + terrain.Height(c.Normalized);
            return surface - c.Magnitude - cave.Radii[k] >= 1.2;
        }
    }
}
