using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// Turns a body's list of formations into actual things at actual places: shapes pressed into the ground,
    /// rocks stood on it, ponds, plumes and caves. Landmarks go where the body's settings say. Everything else
    /// is placed by the run seed, each formation from its own random stream, so adding one never moves another.
    /// No Unity types.
    /// </summary>
    public static class FormationGenerator
    {
        sealed class PendingCave
        {
            public Formation Formation;
            public SeededRandom Random;
        }

        sealed class PendingPond
        {
            public Stamp Stamp;
        }

        sealed class PendingPlume
        {
            public Vec3d Direction;
            public PlumeKind Kind;
            public string Formation;
        }

        sealed class Work
        {
            public readonly List<PendingCave> Caves = new List<PendingCave>();
            public readonly List<PendingPond> Ponds = new List<PendingPond>();
            public readonly List<PendingPlume> Plumes = new List<PendingPlume>();
        }

        [ThreadStatic] static Work _work;

        // ------------------------------------------------------------------ first pass: shapes in the ground

        public static void PlaceStamps(PlanetTerrain terrain)
        {
            var work = new Work();
            _work = work;
            PlanetProfile profile = terrain.Profile;
            for (int u = 0; u < profile.formations.Count; u++)
            {
                FormationUse use = profile.formations[u];
                Formation f = FormationIndex.Get(use.formation);
                if (f == null || f.Technique == Technique.Global || f.Technique == Technique.Sky) continue;
                SeededRandom stream = use.landmark
                    ? SeededRandom.ForStream(0, "landmark." + terrain.BodyId + "." + f.Id + "." + u)
                    : SeededRandom.ForStream(terrain.Seed, "planet." + terrain.BodyId + "." + f.Id);
                for (int i = 0; i < use.count; i++) PlaceOne(terrain, work, f, use, stream.Fork(i));
            }
        }

        static void PlaceOne(PlanetTerrain terrain, Work work, Formation f, FormationUse use, SeededRandom r)
        {
            Vec3d where = use.landmark ? CubeSphere.FromLatLon(use.latitude, use.longitude) : PickLocation(terrain, f.Where, r);
            double t = r.NextDouble();
            double size = use.size > 0 ? use.size : Noise3.Lerp(f.MinSize, f.MaxSize, t * t) * terrain.Scale;
            double bearing = use.landmark ? use.bearing * Math.PI / 180.0 : r.NextDouble() * Math.PI * 2;

            if (f.Technique == Technique.Cave)
            {
                work.Caves.Add(new PendingCave { Formation = f, Random = r });
                return;
            }

            if (f.Chain > 0)
            {
                // A line of copies, each a little different in size.
                Vec3d heading = CubeSphere.Heading(where, bearing);
                Vec3d axis = Vec3d.Cross(where, heading).Normalized;
                double step = size * 2.5 / terrain.Radius;
                for (int i = 0; i < f.Chain; i++)
                {
                    Vec3d at = where.RotatedAround(axis, step * (i - f.Chain * 0.5)).Normalized;
                    EmitSingle(terrain, work, f, at, size * r.Range(0.75f, 1.15f), r.NextDouble() * Math.PI * 2, r.Fork(100 + i));
                }
            }
            else if (f.Cluster > 0)
            {
                double spread = size * Math.Sqrt(f.Cluster) * 1.9;
                for (int i = 0; i < f.Cluster; i++)
                {
                    Vec3d at = Offset(where, r, spread, terrain.Radius);
                    EmitSingle(terrain, work, f, at, size * r.Range(0.6f, 1.2f), r.NextDouble() * Math.PI * 2, r.Fork(100 + i));
                }
            }
            else
            {
                EmitSingle(terrain, work, f, where, size, bearing, r);
            }

            if (!string.IsNullOrEmpty(f.Companion))
            {
                Formation companion = FormationIndex.Get(f.Companion);
                if (companion != null && companion.Technique != Technique.Cave)
                    EmitSingle(terrain, work, companion, where, size * f.CompanionSize, bearing, r.Fork("companion"));
            }
        }

        static Vec3d Offset(Vec3d centre, SeededRandom r, double radius, double bodyRadius)
        {
            // Square root spreads the points evenly over the disc; without it they bunch in the middle.
            double distance = Math.Sqrt(r.NextDouble()) * radius / bodyRadius;
            double angle = r.NextDouble() * Math.PI * 2;
            Vec3d u = centre.AnyPerpendicular();
            Vec3d v = Vec3d.Cross(centre, u);
            return (centre + u * (Math.Cos(angle) * distance) + v * (Math.Sin(angle) * distance)).Normalized;
        }

        static Vec3d RandomDirection(SeededRandom r)
        {
            double y = r.NextDouble() * 2 - 1;
            double a = r.NextDouble() * Math.PI * 2;
            double ring = Math.Sqrt(Math.Max(0, 1 - y * y));
            return new Vec3d(ring * Math.Cos(a), y, ring * Math.Sin(a));
        }

        /// <summary>Picks somewhere on the body that suits the formation. Tries a number of spots and takes the first that fits.</summary>
        public static Vec3d PickLocation(PlanetTerrain terrain, Where where, SeededRandom r)
        {
            PlanetProfile f = terrain.Profile;
            Vec3d candidate = Vec3d.Up;
            for (int attempt = 0; attempt < 48; attempt++)
            {
                candidate = RandomDirection(r);
                switch (where)
                {
                    case Where.Any:
                        return candidate;
                    case Where.SouthPole:
                        return (candidate * 0.3 + new Vec3d(0, -1, 0)).Normalized;
                    case Where.Polar:
                    {
                        double y = (0.8 + 0.2 * r.NextDouble()) * (r.Chance(0.5) ? 1 : -1);
                        double ring = Math.Sqrt(Math.Max(0, 1 - y * y));
                        double a = r.NextDouble() * Math.PI * 2;
                        return new Vec3d(ring * Math.Cos(a), y, ring * Math.Sin(a));
                    }
                    case Where.Equatorial:
                        if (Math.Abs(candidate.Y) < 0.35) return candidate;
                        continue;
                }

                double height = terrain.CoarseHeight(candidate);
                bool fits;
                switch (where)
                {
                    case Where.Land: fits = !f.hasSea || height > f.seaLevel + 2.5; break;
                    case Where.Sea: fits = !f.hasSea || height < f.seaLevel - 6; break;
                    case Where.Coast: fits = !f.hasSea || Math.Abs(height - f.seaLevel) < 3.5; break;
                    case Where.Lowland: fits = height < f.continentBias - 0.2 * f.continentHeight; break;
                    default: fits = height > f.continentBias + 0.45 * f.continentHeight && (!f.hasSea || height > f.seaLevel + 4); break;
                }
                if (fits) return candidate;
            }
            return candidate;
        }

        static void SetTint(Stamp s, Formation f)
        {
            if (string.IsNullOrEmpty(f.Tint)) return;
            float[] c = ColorUtil.Parse(f.Tint);
            s.TintR = c[0];
            s.TintG = c[1];
            s.TintB = c[2];
            s.TintStrength = (float)f.TintStrength;
        }

        static void EmitSingle(PlanetTerrain terrain, Work work, Formation f, Vec3d where, double size, double bearing, SeededRandom r)
        {
            double radius = terrain.Radius;
            Vec3d heading = CubeSphere.Heading(where, bearing);
            uint seed = (uint)r.Next() + 17u;

            switch (f.Technique)
            {
                case Technique.Crater:
                case Technique.Cone:
                case Technique.Dome:
                case Technique.Plateau:
                case Technique.Region:
                {
                    size = Math.Min(size, radius * 0.5);
                    var s = new Stamp
                    {
                        Formation = f, Center = where, U = heading, V = Vec3d.Cross(where, heading).Normalized, Size = size, Seed = seed,
                    };
                    switch (f.Technique)
                    {
                        case Technique.Crater:
                            s.Kind = StampKind.Crater;
                            s.Height = f.Relief * size;
                            s.Extent = size * (f.Rays > 0 ? 5.0 : f.OuterRings > 0 ? 1.45 + 0.45 * f.OuterRings : f.Rampart > 0 ? 2.9 : 2.3);
                            if (f.Elongation > 1) s.Extent *= f.Elongation;
                            break;
                        case Technique.Cone:
                            s.Kind = StampKind.Cone;
                            s.Height = f.Relief * size;
                            s.Extent = size * 1.08;
                            break;
                        case Technique.Dome:
                            s.Kind = StampKind.Dome;
                            s.Height = f.Relief * size;
                            s.Extent = size / (1 - 0.5 * Math.Min(0.9, f.EdgeNoise)) + 1;
                            break;
                        case Technique.Plateau:
                            s.Kind = StampKind.Plateau;
                            s.Height = f.Relief * size;
                            s.Extent = size / (1 - 0.5 * Math.Min(0.9, f.EdgeNoise)) + 1;
                            break;
                        default:
                            s.Kind = StampKind.Region;
                            s.Height = f.Relief * size;
                            s.Extent = size / (1 - 0.5 * Math.Min(0.9, f.EdgeNoise)) + 1;
                            break;
                    }
                    s.Height = Limit(s.Height, radius);
                    s.BoundRadius = s.Extent;
                    SetTint(s, f);
                    terrain.Stamps.Add(s);
                    if (f.Liquid != Liquid.None) work.Ponds.Add(new PendingPond { Stamp = s });
                    AddPlumes(work, f, where, size * 0.55, radius, r);
                    break;
                }
                case Technique.Curve:
                {
                    double width = Math.Min(radius * 0.3, Noise3.Lerp(f.MinWidth, f.MaxWidth, r.NextDouble()) * Math.Max(0.5, terrain.Scale));
                    double length = Math.Min(size, radius * 2.6);
                    Vec3d side = Vec3d.Cross(where, heading).Normalized;
                    for (int lane = 0; lane < Math.Max(1, f.Parallel); lane++)
                    {
                        double shift = (lane - (Math.Max(1, f.Parallel) - 1) * 0.5) * width * 2.8 / radius;
                        Vec3d start = (where + side * shift).Normalized;
                        Stamp main = AddCurve(terrain, f, start, CubeSphere.Heading(start, bearing), length * r.Range(0.85f, 1f), width, f.Relief * width, r.Fork(200 + lane), f.Parallel <= 1);
                        for (int k = 0; k < f.Plumes && f.Plume != PlumeKind.None; k++)
                        {
                            work.Plumes.Add(new PendingPlume
                            {
                                Direction = PlanetTerrain.CurvePoint(main, (k + 0.5) / f.Plumes, radius), Kind = f.Plume, Formation = f.Id,
                            });
                        }
                        // Side branches leave the main line at an angle and are shorter, narrower and shallower.
                        for (int b = 0; b < f.Branches; b++)
                        {
                            SeededRandom br = r.Fork(300 + lane * 16 + b);
                            double along = br.Range(0.2f, 0.8f);
                            Vec3d from = PlanetTerrain.CurvePoint(main, along, radius);
                            double turn = br.Range(0.6f, 1.2f) * (br.Chance(0.5) ? 1 : -1);
                            Vec3d tangent = CurveTangent(main, along);
                            Vec3d branchHeading = tangent.RotatedAround(from, turn).Normalized;
                            AddCurve(terrain, f, from, branchHeading, length * br.Range(0.28f, 0.48f), width * 0.65, f.Relief * width * 0.7, br, true);
                        }
                    }
                    break;
                }
                case Technique.Scatter:
                {
                    int count = r.Range(f.MinCount, f.MaxCount + 1);
                    float[] tint = Feature.OwnColour(f.Feature)
                        ? new[] { 1f, 1f, 1f }
                        : ColorUtil.Parse(string.IsNullOrEmpty(f.Tint) ? terrain.Profile.rockColor : f.Tint);
                    double shrink = Math.Max(0.6, Math.Min(1.0, terrain.Scale));
                    for (int i = 0; i < count; i++)
                    {
                        double k = r.NextDouble();
                        Vec3d at = count == 1 ? where : Offset(where, r, size, radius);
                        terrain.Placements.Add(new Placement
                        {
                            Direction = at, Kind = f.Feature, Variant = r.Range(0, 1024),
                            Scale = Noise3.Lerp(f.MinScale, f.MaxScale, k * k) * shrink, Yaw = r.NextDouble() * Math.PI * 2,
                            R = tint[0], G = tint[1], B = tint[2], Floats = f.Floats,
                        });
                        if (f.Plume != PlumeKind.None && i < f.Plumes)
                            work.Plumes.Add(new PendingPlume { Direction = at, Kind = f.Plume, Formation = f.Id });
                    }
                    break;
                }
            }
        }

        /// <summary>No single formation may be taller or deeper than about a sixth of the body's radius.</summary>
        static double Limit(double height, double bodyRadius)
        {
            double most = bodyRadius * 0.16;
            return height > most ? most : height < -most ? -most : height;
        }

        static void AddPlumes(Work work, Formation f, Vec3d where, double spread, double radius, SeededRandom r)
        {
            if (f.Plume == PlumeKind.None) return;
            for (int i = 0; i < f.Plumes; i++)
            {
                Vec3d at = f.Plumes == 1 ? where : Offset(where, r, spread, radius);
                work.Plumes.Add(new PendingPlume { Direction = at, Kind = f.Plume, Formation = f.Id });
            }
        }

        static Vec3d CurveTangent(Stamp s, double along)
        {
            double angle = along * s.ArcAngle;
            return (s.Along * Math.Cos(angle) - s.Start * Math.Sin(angle)).Normalized;
        }

        static Stamp AddCurve(PlanetTerrain terrain, Formation f, Vec3d start, Vec3d heading, double length, double width, double height, SeededRandom r, bool bow)
        {
            double radius = terrain.Radius;
            double half = width * 0.5;
            Vec3d normal = Vec3d.Cross(start, heading).Normalized;
            Vec3d along = Vec3d.Cross(normal, start).Normalized;
            double arc = Math.Max(0.02, Math.Min(Math.PI * 0.9, length / radius));
            double reach;
            switch (f.Profile)
            {
                case CurveProfile.Scarp: reach = half * 8; break;
                case CurveProfile.Ridge: reach = half * 1.6; break;
                case CurveProfile.DoubleRidge: reach = half * 1.5; break;
                default: reach = half; break;
            }
            var s = new Stamp
            {
                Formation = f, Kind = StampKind.Curve, Start = start, Normal = normal, Along = along, ArcAngle = arc,
                Length = arc * radius, Size = half, Height = Limit(height, radius), Extent = reach, MeanderAmount = f.Meander * width,
                Seed = (uint)r.Next() + 29u, U = heading, V = normal,
            };
            // Lines that run in parallel families stay straight, or they would cross each other.
            if (bow) s.Bow = Math.Min(s.Length * 0.06, width * 2.5);
            s.Center = (start * Math.Cos(arc * 0.5) + along * Math.Sin(arc * 0.5)).Normalized;
            // Straight-line distance from the middle to an end, plus everything that can stick out sideways.
            s.BoundRadius = 2 * radius * Math.Sin(arc * 0.25) + reach * 1.3 + s.MeanderAmount * 1.7 + s.Bow * 1.9 + 2;
            SetTint(s, f);
            terrain.Stamps.Add(s);
            return s;
        }

        // ------------------------------------------------------------------ second pass: things that need the finished ground

        public static void PlaceTheRest(PlanetTerrain terrain)
        {
            Work work = _work;
            _work = null;
            if (work == null) return;

            for (int i = 0; i < work.Ponds.Count; i++) AddPond(terrain, work.Ponds[i].Stamp);

            for (int i = 0; i < work.Plumes.Count; i++)
            {
                PendingPlume p = work.Plumes[i];
                double height = terrain.Height(p.Direction);
                if (terrain.HasSea && height < terrain.SeaLevel && p.Kind != PlumeKind.BlackSmoker) continue;
                terrain.Plumes.Add(new PlumeSite { Direction = p.Direction, Height = height, Kind = p.Kind, Formation = p.Formation });
            }

            for (int i = 0; i < work.Caves.Count; i++)
            {
                Cave cave = CavePath.Build(terrain, work.Caves[i].Formation, work.Caves[i].Random);
                if (cave == null) continue;
                terrain.Caves.Add(cave);
                for (int m = 0; m < cave.Mouths.Length; m++)
                    terrain.Holes.Add(new TerrainHole { Direction = cave.Mouths[m], Radius = cave.MouthRadius });
            }
        }

        static void AddPond(PlanetTerrain terrain, Stamp s)
        {
            Formation f = s.Formation;
            double fraction, depth;
            switch (s.Kind)
            {
                case StampKind.Crater:
                    fraction = 0.96;
                    depth = s.Height;
                    break;
                case StampKind.Cone:
                    fraction = f.Summit * 0.96;
                    depth = f.SummitDepth * s.Height;
                    break;
                case StampKind.Plateau:
                    fraction = (1 - 2 * f.Steepness) * 0.9;
                    depth = Math.Abs(s.Height);
                    break;
                default:
                    fraction = 0.7;
                    depth = Math.Abs(s.Height);
                    break;
            }
            double pondRadius = s.Size * fraction;
            if (pondRadius < 1.5 || depth < 0.6) return;

            // The lowest point of the floor, and the lowest point of the lip the liquid must stay below.
            double floor = terrain.Height(s.Center);
            double lip = double.MaxValue;
            const int samples = 16;
            for (int i = 0; i < samples; i++)
            {
                double a = i / (double)samples * Math.PI * 2;
                Vec3d round = s.U * Math.Cos(a) + s.V * Math.Sin(a);
                floor = Math.Min(floor, terrain.Height((s.Center + round * (pondRadius * 0.45 / terrain.Radius)).Normalized));
                lip = Math.Min(lip, terrain.Height((s.Center + round * (pondRadius * 1.03 / terrain.Radius)).Normalized));
            }
            double level = Math.Min(floor + f.Fill * depth, lip - 0.35);
            if (level < floor + 0.4) return;
            if (terrain.HasSea && level < terrain.SeaLevel + 0.3) return;
            terrain.Ponds.Add(new Pond { Direction = s.Center, Level = level, Radius = pondRadius, Liquid = f.Liquid, Formation = f.Id });
        }
    }

    public static class Feature
    {
        public static bool IsPlant(FeatureKind kind)
        {
            switch (kind)
            {
                case FeatureKind.Conifer: case FeatureKind.Broadleaf: case FeatureKind.Palm: case FeatureKind.Bush:
                case FeatureKind.Cactus: case FeatureKind.Birch: case FeatureKind.Maple: case FeatureKind.Blossom:
                case FeatureKind.Pine: case FeatureKind.Acacia: case FeatureKind.GiantTree: case FeatureKind.Snag:
                case FeatureKind.SnowConifer: case FeatureKind.Meadow: case FeatureKind.Reeds: case FeatureKind.Fern:
                case FeatureKind.Log:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// True for things that come painted (plants, and the rocks made in Blender): they are drawn in their own
        /// colours and never tinted with the body's rock colour.
        /// </summary>
        public static bool OwnColour(FeatureKind kind)
        {
            return IsPlant(kind) || kind == FeatureKind.Crag || kind == FeatureKind.SnowCrag || kind == FeatureKind.Outcrop;
        }
    }
}
