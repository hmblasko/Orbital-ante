using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Planets
{
    /// <summary>One box of dug ground, ready to hand to the engine. Positions are metres from the body's centre.</summary>
    public sealed class DigChunkData
    {
        public long Key;
        public float[] Positions;
        public float[] Normals;
        /// <summary>r, g, b per corner, 0 to 1 (not yet linear).</summary>
        public float[] Colors;
        /// <summary>How much open sky each corner sees, 0 (deep in a tunnel) to 1 (open ground).</summary>
        public float[] Sky;
        public int[] Triangles;
        public Vec3d Min, Max;
        public int VertexCount => Positions == null ? 0 : Positions.Length / 3;
        public bool Empty => Triangles == null || Triangles.Length == 0;
    }

    /// <summary>
    /// The scoops near one box, sorted into buckets so the shape at a point only looks at the few that can matter.
    /// A scoop is put in every bucket it comes within <see cref="Pad"/> of, so the distance read at any point near
    /// a scoop's surface is exact, and far from every scoop it is at least positive (which is all that matters there).
    /// </summary>
    public sealed class DigCarveSet
    {
        const double Bucket = 1.2;
        const double Pad = 0.8;
        readonly DigCarve[] _carves;
        readonly Dictionary<long, List<int>> _buckets = new Dictionary<long, List<int>>();
        static readonly List<int> NoneList = new List<int>();

        public DigCarveSet(DigCarve[] carves)
        {
            _carves = carves ?? new DigCarve[0];
            for (int i = 0; i < _carves.Length; i++)
            {
                DigCarve c = _carves[i];
                double r = c.Radius + Pad;
                int x0 = Cell(c.Centre.X - r), x1 = Cell(c.Centre.X + r);
                int y0 = Cell(c.Centre.Y - r), y1 = Cell(c.Centre.Y + r);
                int z0 = Cell(c.Centre.Z - r), z1 = Cell(c.Centre.Z + r);
                for (int x = x0; x <= x1; x++)
                    for (int y = y0; y <= y1; y++)
                        for (int z = z0; z <= z1; z++)
                        {
                            long key = DigRules.Key(x, y, z);
                            if (!_buckets.TryGetValue(key, out List<int> list)) _buckets[key] = list = new List<int>(4);
                            list.Add(i);
                        }
            }
        }

        public int Count => _carves.Length;

        static int Cell(double v) => (int)Math.Floor(v / Bucket);

        /// <summary>Signed distance to the nearest scoop's air: negative inside a scoop. A large number where there is none near.</summary>
        public double Distance(Vec3d p)
        {
            if (_carves.Length == 0) return 1e9;
            if (!_buckets.TryGetValue(DigRules.Key(Cell(p.X), Cell(p.Y), Cell(p.Z)), out List<int> list)) return 1e9;
            double best = 1e9;
            for (int i = 0; i < list.Count; i++)
            {
                DigCarve c = _carves[list[i]];
                double d = (p - c.Centre).Magnitude - c.Radius;
                if (d < best) best = d;
            }
            return best;
        }
    }

    /// <summary>
    /// Works out the shape of one box of dug ground: rock is wherever the point is under the ground (as the finest
    /// tiles draw it), not in the air of a cave network and not in a scoop. The surface between rock and air is
    /// found on a 0.4 m grid and joined into a smooth surface with one corner per cell ("surface nets", as the cave
    /// networks are), pushed <see cref="DigRules.Recess"/> into the rock. A grid edge's face belongs to the box its
    /// first end is in, so neighbouring boxes join with no gap and no face drawn twice.
    ///
    /// Each corner is coloured by the air it faces: the ground's own colour on untouched ground, darker and greyer
    /// fresh-dug earth in a scoop (bedrock the deeper it goes), the cave's rock where a cave was dug into. Each also
    /// gets how much open sky it sees (rays up through the dug shape), which the ground shader multiplies into the
    /// daylight, so a tunnel is dark without any shadows. Pure sums, no Unity types; safe on a background thread.
    /// </summary>
    public static class DigMesher
    {
        enum Air : byte { Sky, Cave, Dug }

        sealed class Space
        {
            public DigGround Ground;
            public PlanetTerrain Terrain;
            public DigCarveSet Carves;
            public CaveNetwork[] Networks;
            public uint Seed;

            public double Field(Vec3d p, out Air air, out CaveNetwork net)
            {
                double r = p.Magnitude;
                double v = r > 1e-6 ? Ground.RadiusAt(p / r) - r : 1e9;
                air = Air.Sky;
                net = null;
                for (int i = 0; i < Networks.Length; i++)
                {
                    double a = Networks[i].Air(p);
                    if (a < v) { v = a; air = Air.Cave; net = Networks[i]; }
                }
                double d = Carves.Distance(p);
                if (d < v) { v = d; air = Air.Dug; net = null; }
                return v - DigRules.Recess;
            }

            public double Field(Vec3d p) => Field(p, out _, out _);
        }

        /// <summary>
        /// Builds one box. <paramref name="carves"/> must hold every scoop within about 8 m of it (the sky rays look
        /// that far); <paramref name="networks"/> the cave networks that come near it (may be empty).
        /// </summary>
        public static DigChunkData Build(long key, DigCarve[] carves, DigGround ground, IList<CaveNetwork> networks)
        {
            var space = new Space
            {
                Ground = ground,
                Terrain = ground.Terrain,
                Carves = new DigCarveSet(carves),
                Seed = (uint)ground.Terrain.Seed + 9137u,
            };
            Vec3d origin = DigRules.ChunkMin(key);
            const int n = DigRules.ChunkCells;
            const double cell = DigRules.Cell;
            Vec3d boxMin = origin - new Vec3d(cell * 3, cell * 3, cell * 3);
            Vec3d boxMax = origin + new Vec3d(DigRules.ChunkSize + cell * 3, DigRules.ChunkSize + cell * 3, DigRules.ChunkSize + cell * 3);
            var nets = new List<CaveNetwork>();
            if (networks != null)
            {
                for (int i = 0; i < networks.Count; i++)
                    if (networks[i] != null && networks[i].Touches(boxMin - new Vec3d(8, 8, 8), boxMax + new Vec3d(8, 8, 8))) nets.Add(networks[i]);
            }
            space.Networks = nets.ToArray();
            var result = new DigChunkData { Key = key };

            // The space at every grid point of the box, one point over each side (and one more on the high side).
            int m = n + 3;
            var value = new double[m * m * m];
            var kind = new Air[m * m * m];
            int Index(int a, int b, int c) => ((a + 1) * m + (b + 1)) * m + (c + 1);
            bool anyAir = false, anyRock = false;
            for (int a = -1; a <= n + 1; a++)
                for (int b = -1; b <= n + 1; b++)
                    for (int c = -1; c <= n + 1; c++)
                    {
                        Vec3d p = origin + new Vec3d(a * cell, b * cell, c * cell);
                        int at = Index(a, b, c);
                        value[at] = space.Field(p, out kind[at], out _);
                        if (value[at] < 0) anyAir = true; else anyRock = true;
                    }
            if (!anyAir || !anyRock) return result;

            // One corner per cell the surface passes through.
            int cells = n + 2;
            var cornerOf = new int[cells * cells * cells];
            for (int q = 0; q < cornerOf.Length; q++) cornerOf[q] = -1;
            int CellIndex(int a, int b, int c) => ((a + 1) * cells + (b + 1)) * cells + (c + 1);
            var positions = new List<Vec3d>();
            var airOf = new List<Air>();
            for (int a = -1; a <= n; a++)
                for (int b = -1; b <= n; b++)
                    for (int c = -1; c <= n; c++)
                    {
                        int mask = 0;
                        for (int q = 0; q < 8; q++)
                            if (value[Index(a + (q & 1), b + ((q >> 1) & 1), c + ((q >> 2) & 1))] < 0) mask |= 1 << q;
                        if (mask == 0 || mask == 255) continue;
                        Vec3d sum = Vec3d.Zero;
                        int crossings = 0;
                        int sky = 0, cave = 0, dug = 0;
                        for (int q = 0; q < 8; q++)
                        {
                            for (int axis = 0; axis < 3; axis++)
                            {
                                int bit = 1 << axis;
                                if ((q & bit) != 0) continue;
                                int q2 = q | bit;
                                bool inA = (mask & (1 << q)) != 0, inB = (mask & (1 << q2)) != 0;
                                if (inA == inB) continue;
                                int ia = Index(a + (q & 1), b + ((q >> 1) & 1), c + ((q >> 2) & 1));
                                int ib = Index(a + (q2 & 1), b + ((q2 >> 1) & 1), c + ((q2 >> 2) & 1));
                                double va = value[ia], vb = value[ib];
                                double t = va / (va - vb);
                                Vec3d pa = new Vec3d(q & 1, (q >> 1) & 1, (q >> 2) & 1);
                                Vec3d pb = new Vec3d(q2 & 1, (q2 >> 1) & 1, (q2 >> 2) & 1);
                                sum += pa + (pb - pa) * t;
                                crossings++;
                                Air end = kind[inA ? ia : ib];
                                if (end == Air.Sky) sky++; else if (end == Air.Cave) cave++; else dug++;
                            }
                        }
                        Vec3d inCell = sum / crossings;
                        cornerOf[CellIndex(a, b, c)] = positions.Count;
                        positions.Add(origin + new Vec3d(a, b, c) * cell + inCell * cell);
                        airOf.Add(dug >= sky && dug >= cave ? Air.Dug : cave > sky ? Air.Cave : Air.Sky);
                    }

            // Faces: one square for each grid edge the surface crosses whose first end is in this box.
            var triangles = new List<int>();
            var used = new bool[positions.Count];
            for (int a = 0; a < n; a++)
                for (int b = 0; b < n; b++)
                    for (int c = 0; c < n; c++)
                    {
                        double v0 = value[Index(a, b, c)];
                        for (int axis = 0; axis < 3; axis++)
                        {
                            int da = axis == 0 ? 1 : 0, db = axis == 1 ? 1 : 0, dc = axis == 2 ? 1 : 0;
                            double v1 = value[Index(a + da, b + db, c + dc)];
                            if ((v0 < 0) == (v1 < 0)) continue;
                            int c0, c1, c2, c3;
                            if (axis == 0)
                            {
                                c0 = cornerOf[CellIndex(a, b - 1, c - 1)]; c1 = cornerOf[CellIndex(a, b, c - 1)];
                                c2 = cornerOf[CellIndex(a, b, c)]; c3 = cornerOf[CellIndex(a, b - 1, c)];
                            }
                            else if (axis == 1)
                            {
                                c0 = cornerOf[CellIndex(a - 1, b, c - 1)]; c1 = cornerOf[CellIndex(a - 1, b, c)];
                                c2 = cornerOf[CellIndex(a, b, c)]; c3 = cornerOf[CellIndex(a, b, c - 1)];
                            }
                            else
                            {
                                c0 = cornerOf[CellIndex(a - 1, b - 1, c)]; c1 = cornerOf[CellIndex(a, b - 1, c)];
                                c2 = cornerOf[CellIndex(a, b, c)]; c3 = cornerOf[CellIndex(a - 1, b, c)];
                            }
                            if (c0 < 0 || c1 < 0 || c2 < 0 || c3 < 0) continue;
                            // Faces look into the air. The grid here is the body's own axes (right-handed), the
                            // mirror of the cave networks' (east, up, north) frame, so the turn is the other way.
                            bool flip = v0 < 0;
                            if (flip)
                            {
                                triangles.Add(c0); triangles.Add(c2); triangles.Add(c1);
                                triangles.Add(c0); triangles.Add(c3); triangles.Add(c2);
                            }
                            else
                            {
                                triangles.Add(c0); triangles.Add(c1); triangles.Add(c2);
                                triangles.Add(c0); triangles.Add(c2); triangles.Add(c3);
                            }
                            used[c0] = used[c1] = used[c2] = used[c3] = true;
                        }
                    }
            if (triangles.Count == 0) return result;

            var remap = new int[positions.Count];
            int kept = 0;
            for (int q = 0; q < positions.Count; q++) remap[q] = used[q] ? kept++ : -1;
            result.Positions = new float[kept * 3];
            result.Normals = new float[kept * 3];
            result.Colors = new float[kept * 3];
            result.Sky = new float[kept];
            Vec3d cmin = new Vec3d(double.MaxValue, double.MaxValue, double.MaxValue), cmax = -cmin;
            for (int q = 0; q < positions.Count; q++)
            {
                int o = remap[q];
                if (o < 0) continue;
                Vec3d p = positions[q];
                Vec3d normal = Normal(space, p);
                Paint(space, p, normal, airOf[q], out float red, out float green, out float blue);
                result.Positions[o * 3] = (float)p.X; result.Positions[o * 3 + 1] = (float)p.Y; result.Positions[o * 3 + 2] = (float)p.Z;
                result.Normals[o * 3] = (float)normal.X; result.Normals[o * 3 + 1] = (float)normal.Y; result.Normals[o * 3 + 2] = (float)normal.Z;
                result.Colors[o * 3] = red; result.Colors[o * 3 + 1] = green; result.Colors[o * 3 + 2] = blue;
                result.Sky[o] = (float)SkyReach(space, p, normal);
                cmin = new Vec3d(Math.Min(cmin.X, p.X), Math.Min(cmin.Y, p.Y), Math.Min(cmin.Z, p.Z));
                cmax = new Vec3d(Math.Max(cmax.X, p.X), Math.Max(cmax.Y, p.Y), Math.Max(cmax.Z, p.Z));
            }
            result.Triangles = new int[triangles.Count];
            for (int q = 0; q < triangles.Count; q++) result.Triangles[q] = remap[triangles[q]];
            result.Min = cmin;
            result.Max = cmax;
            return result;
        }

        /// <summary>The way out of the rock at a point on the surface, from the slope of the space there.</summary>
        static Vec3d Normal(Space s, Vec3d p)
        {
            const double h = 0.18;
            double dx = s.Field(p + new Vec3d(h, 0, 0)) - s.Field(p - new Vec3d(h, 0, 0));
            double dy = s.Field(p + new Vec3d(0, h, 0)) - s.Field(p - new Vec3d(0, h, 0));
            double dz = s.Field(p + new Vec3d(0, 0, h)) - s.Field(p - new Vec3d(0, 0, h));
            Vec3d grad = new Vec3d(dx, dy, dz);
            return grad.SqrMagnitude > 1e-12 ? -grad.Normalized : p.Normalized;
        }

        // Straight up, and three more leaning 45 degrees out, evenly round.
        static readonly double[] RayLean = { 0, 0.785, 0.785, 0.785 };
        static readonly double[] RayTurn = { 0, 0, 2.094, 4.189 };

        /// <summary>
        /// How much open sky a point on the surface sees: rays go up through the dug shape until they come out
        /// above the ground (open) or meet rock (shut). 1 on open ground, little in a tunnel, part way down a pit.
        /// </summary>
        public static double SkyReachForTests(DigCarve[] carves, DigGround ground, Vec3d p, Vec3d normal)
        {
            var s = new Space { Ground = ground, Terrain = ground.Terrain, Carves = new DigCarveSet(carves), Networks = new CaveNetwork[0] };
            return SkyReach(s, p, normal);
        }

        static double SkyReach(Space s, Vec3d p, Vec3d normal)
        {
            double r = p.Magnitude;
            Vec3d up = p / r;
            // Already out on the ground's surface (or above it): sees the sky, as the tiles round it do.
            double depth = s.Ground.RadiusAt(up) - r;
            if (depth < 0.25) return 1;
            // Far down a shaft only a speck of sky is left overhead: daylight fades out over the first tens of metres,
            // whichever way the rays found open (they only look 9 m), and deeper still it is dark but for your lamp.
            double fade = Math.Max(0, Math.Min(1, 1 - (depth - DaylightDepth) / DaylightFade));
            if (fade <= 0) return 0;
            Vec3d side = up.AnyPerpendicular().Normalized;
            Vec3d start = p + normal * 0.25;
            double open = 0;
            for (int k = 0; k < RayLean.Length; k++)
            {
                Vec3d dir = up;
                if (RayLean[k] > 0)
                {
                    Vec3d across = side.RotatedAround(up, RayTurn[k]);
                    dir = (up * Math.Cos(RayLean[k]) + across * Math.Sin(RayLean[k])).Normalized;
                }
                // A ray that starts into the rock (the surface faces away from it) sees nothing that way.
                if (Vec3d.Dot(dir, normal) < -0.2) continue;
                double step = 0.35, along = 0;
                bool shut = false;
                while (along < 9)
                {
                    along += step;
                    step = Math.Min(1.2, step * 1.35);
                    Vec3d q = start + dir * along;
                    double qr = q.Magnitude;
                    if (s.Ground.RadiusAt(q / qr) - qr < -0.3) break;   // out in the open air above the ground
                    if (s.Field(q) > 0) { shut = true; break; }
                }
                if (!shut) open += k == 0 ? 0.4 : 0.2;
            }
            return Math.Max(0, Math.Min(1, open)) * fade;
        }

        /// <summary>Daylight down a dug hole starts to fade this far under the old ground, in metres ...</summary>
        public const double DaylightDepth = 8;
        /// <summary>... and is gone this much further down.</summary>
        public const double DaylightFade = 40;

        static void Paint(Space s, Vec3d p, Vec3d normal, Air air, out float red, out float green, out float blue)
        {
            Vec3d up = p.Normalized;
            if (air == Air.Cave)
            {
                CaveNetwork nearest = null;
                double best = double.MaxValue;
                for (int i = 0; i < s.Networks.Length; i++)
                {
                    double a = Math.Abs(s.Networks[i].Air(p));
                    if (a < best) { best = a; nearest = s.Networks[i]; }
                }
                if (nearest != null)
                {
                    CaveNetworkMesher.WallColour(nearest, s.Terrain, p, normal, out red, out green, out blue);
                    return;
                }
            }
            PlanetTerrain t = s.Terrain;
            TerrainPoint point = t.Sample(up, s.Ground.MinWavelength);
            double cos = Math.Max(-1, Math.Min(1, Vec3d.Dot(normal, up)));
            double slope = Math.Sqrt(Math.Max(0, 1 - cos * cos));
            t.ColorAt(up, point, air == Air.Sky ? slope : 0.2, out float gr, out float gg, out float gb);
            if (air == Air.Sky)
            {
                red = gr; green = gg; blue = gb;
                return;
            }
            // Fresh-dug earth: darker than the weathered top, losing its colour toward grey bedrock the deeper it
            // goes, in faint level bands with lumps of stone.
            double depth = Math.Max(0, s.Ground.RadiusAt(up) - p.Magnitude);
            double deep = Noise3.Smooth(0.3, 7.0, depth);
            double band = Noise3.Perlin(new Vec3d(depth / 0.8, 0.31, 0.77) + p * 0.05, 1.0, s.Seed) * 0.07;
            double lumps = Noise3.Perlin(p, 0.9, s.Seed + 3) * 0.07;
            double shade = (0.7 - 0.12 * deep) * (1 + band + lumps);
            double grey = gr * 0.3 + gg * 0.5 + gb * 0.2;
            double r = Noise3.Lerp(gr, grey * 1.04, 0.4 * deep) * shade;
            double g = Noise3.Lerp(gg, grey * 1.0, 0.4 * deep) * shade;
            double b = Noise3.Lerp(gb, grey * 0.95, 0.4 * deep) * shade;
            red = (float)Noise3.Clamp01(r);
            green = (float)Noise3.Clamp01(g);
            blue = (float)Noise3.Clamp01(b);
        }
    }
}
