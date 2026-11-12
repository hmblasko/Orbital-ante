using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// Works out the route of a cave: a sloping shaft down from a hole in the ground, a wandering tunnel that
    /// stays safely under the surface, and then either a dead end, a great hall, or a second shaft back up to
    /// daylight. Pure rules, no Unity types. <see cref="CaveMesher"/> turns the route into walls.
    /// </summary>
    public static class CavePath
    {
        public const double Step = 2.0;
        /// <summary>Thickness of rock kept between the tunnel roof and the open air, in metres.</summary>
        public const double Roof = 4.5;
        const double ThinnestRoof = 3.0;

        public static Cave Build(PlanetTerrain terrain, Formation f, SeededRandom r)
        {
            double bodyRadius = terrain.Radius;
            double tube = Noise3.Lerp(f.MinWidth, f.MaxWidth, r.NextDouble());
            double length = Noise3.Lerp(f.MinSize, f.MaxSize, r.NextDouble()) * Math.Max(0.6, terrain.Scale);
            bool through = f.Parallel >= 2;
            bool hall = f.Rugged > 0;
            double depth = tube + Roof;
            // A cave needs room. On the very smallest moons there is none.
            if (bodyRadius < depth * 4) return null;

            Vec3d mouth = Vec3d.Up;
            bool found = false;
            for (int attempt = 0; attempt < 40 && !found; attempt++)
            {
                Vec3d c = FormationGenerator.PickLocation(terrain, Where.Land, r);
                if (terrain.SlopeAt(c, 3) > 0.3) continue;
                // Prefer ground high enough that the tunnel stays dry; give up on that after a while.
                if (terrain.HasSea && attempt < 30 && terrain.Height(c) < terrain.SeaLevel + depth + 4) continue;
                if (terrain.InHole(c, 40)) continue;
                mouth = c;
                found = true;
            }
            if (!found) return null;

            uint seed = (uint)r.Next();
            var centres = new List<Vec3d>();
            var radii = new List<double>();
            Vec3d heading = CubeSphere.Heading(mouth, r.NextDouble() * Math.PI * 2);
            double tilt = r.Range(0.3f, 0.6f);
            Vec3d pos = mouth * (bodyRadius + terrain.Height(mouth));
            Vec3d dir = (mouth * -Math.Cos(tilt) + heading * Math.Sin(tilt)).Normalized;
            centres.Add(pos);
            radii.Add(tube);

            // The shaft: straight down at a slant until there is a full roof overhead.
            for (int i = 0; i < 40; i++)
            {
                pos += dir * Step;
                centres.Add(pos);
                radii.Add(tube);
                if (bodyRadius + terrain.Height(pos.Normalized) - pos.Magnitude >= depth) break;
            }

            // The tunnel.
            int steps = Math.Max(6, (int)(length / Step));
            int hallStart = -1, hallEnd = -1;
            for (int i = 0; i < steps; i++)
            {
                Vec3d up = pos.Normalized;
                heading = (heading - up * Vec3d.Dot(heading, up)).Normalized;
                heading = heading.RotatedAround(up, Noise3.Perlin(i * 0.13, 0.5, seed * 1e-6, seed) * 0.24);
                Vec3d next = pos + heading * Step;
                Vec3d nextDir = next.Normalized;
                double surface = bodyRadius + terrain.Height(nextDir);
                double along = i / (double)steps;
                double radius = tube * (0.9 + 0.25 * Noise3.Perlin(i * 0.21, 3.3, seed * 1e-6, seed + 1));
                if (hall)
                {
                    double swell = Noise3.Smooth(0.45, 0.68, along) * (1 - Noise3.Smooth(0.9, 1.0, along));
                    radius = Noise3.Lerp(radius, tube * 2.4, swell);
                    if (swell > 0.6)
                    {
                        if (hallStart < 0) hallStart = centres.Count;
                        hallEnd = centres.Count;
                    }
                }
                // Follow the ground above, but only climb or dive gently, and never let the roof get thin.
                double current = pos.Magnitude;
                double want = surface - (radius + Roof);
                double target = Math.Max(current - Step * 0.6, Math.Min(current + Step * 0.6, want));
                target = Math.Min(target, surface - (radius + ThinnestRoof));
                pos = nextDir * target;
                centres.Add(pos);
                radii.Add(radius);
            }

            Vec3d[] mouths;
            bool surfaced = false;
            if (through)
            {
                // A second slanting shaft, back up to the surface.
                double rise = r.Range(0.3f, 0.6f);
                for (int i = 0; i < 40; i++)
                {
                    Vec3d up = pos.Normalized;
                    heading = (heading - up * Vec3d.Dot(heading, up)).Normalized;
                    pos += (up * Math.Cos(rise) + heading * Math.Sin(rise)) * Step;
                    double surface = bodyRadius + terrain.Height(pos.Normalized);
                    if (pos.Magnitude >= surface)
                    {
                        pos = pos.Normalized * surface;
                        centres.Add(pos);
                        radii.Add(tube);
                        surfaced = true;
                        break;
                    }
                    centres.Add(pos);
                    radii.Add(tube);
                }
            }
            if (surfaced)
            {
                mouths = new[] { mouth, pos.Normalized };
            }
            else
            {
                // A dead end: the tunnel pinches shut.
                Vec3d up = pos.Normalized;
                heading = (heading - up * Vec3d.Dot(heading, up)).Normalized;
                double last = radii[radii.Count - 1];
                centres.Add(pos + heading * (last * 0.6));
                radii.Add(last * 0.6);
                centres.Add(pos + heading * (last * 1.0));
                radii.Add(last * 0.2);
                mouths = new[] { mouth };
            }

            float[] tint = ColorUtil.Parse(string.IsNullOrEmpty(f.Tint) ? terrain.Profile.cliffColor : f.Tint);
            var cave = new Cave
            {
                Centres = centres.ToArray(), Radii = radii.ToArray(), Mouths = mouths, MouthRadius = tube,
                R = tint[0], G = tint[1], B = tint[2], Formation = f.Id,
            };

            if (hall && hallStart >= 0 && f.MaxCount > 0)
            {
                // Things standing on the floor of the hall.
                float[] colour = f.Feature == FeatureKind.Crystal
                    ? ColorUtil.Parse("#BFE8F4")
                    : ColorUtil.Parse(terrain.Profile.rockColor);
                int count = r.Range(f.MinCount, f.MaxCount + 1);
                for (int i = 0; i < count; i++)
                {
                    int k = r.Range(hallStart, hallEnd + 1);
                    Vec3d c = cave.Centres[k];
                    Vec3d up = c.Normalized;
                    Vec3d side = up.AnyPerpendicular().RotatedAround(up, r.NextDouble() * Math.PI * 2);
                    double reach = cave.Radii[k];
                    double out_ = r.NextDouble() * 0.6 * reach;
                    // The floor curves up toward the walls, like the inside of a pipe.
                    double drop = Math.Sqrt(Math.Max(0, reach * reach - out_ * out_)) * 0.92;
                    Vec3d at = c + side * out_ - up * drop;
                    double t = r.NextDouble();
                    terrain.Placements.Add(new Placement
                    {
                        Direction = at.Normalized, Absolute = true, Position = at, Kind = f.Feature, Variant = r.Range(0, 1024),
                        Scale = Math.Min(Noise3.Lerp(f.MinScale, f.MaxScale, t * t), reach * 1.2), Yaw = r.NextDouble() * Math.PI * 2,
                        R = colour[0], G = colour[1], B = colour[2],
                    });
                }
            }
            return cave;
        }
    }
}
