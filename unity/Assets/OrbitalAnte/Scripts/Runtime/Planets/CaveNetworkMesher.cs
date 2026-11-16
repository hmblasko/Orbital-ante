using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Planets
{
    /// <summary>One block of a cave network's walls, small enough to be skipped when it is out of view.</summary>
    public sealed class CaveChunk
    {
        public ShapeData Shape;
        /// <summary>Box round the block, metres from the body's centre.</summary>
        public Vec3d Min, Max;
    }

    /// <summary>
    /// Turns a <see cref="CaveNetwork"/> into walls. The air of the cave and the open air above the ground make one
    /// space; its edge is found on a grid of points (a little under a metre apart) and joined into a smooth surface
    /// with one corner per grid cell ("surface nets"). Only the cave's own walls are kept, plus a lip of ground just
    /// under the real ground round each hole, so the holes cut in the terrain join the cave with no gap.
    ///
    /// The walls are coloured here, corner by corner: bedding in the rock, mud or frost on the floors, streaks of
    /// flowstone, and darker hollows where little of the cave can be seen (worked out from the space itself).
    /// Light is not baked in here: that is <see cref="CaveLightVolume"/>'s, so the walls and anything standing in the
    /// cave are lit alike. Pure sums, no Unity types; safe to run on a background thread.
    /// </summary>
    public static class CaveNetworkMesher
    {
        /// <summary>Distance between grid points, in metres.</summary>
        public const double Cell = 0.9;
        /// <summary>Grid cells along each side of a block.</summary>
        public const int Block = 18;
        /// <summary>How far under the real ground the lip round each hole lies, in metres.</summary>
        public const double LipDepth = 0.5;
        /// <summary>How far beyond each hole the lip reaches, in metres.</summary>
        public const double LipReach = 7.0;

        sealed class Grid
        {
            public CaveNetwork Net;
            public PlanetTerrain Terrain;
            public Vec3d Origin;     // local position of grid point (0,0,0)
            public int Nx, Ny, Nz;
            public readonly Dictionary<long, double> Ground = new Dictionary<long, double>();

            public Vec3d Local(int i, int j, int k) => new Vec3d(Origin.X + i * Cell, Origin.Y + j * Cell, Origin.Z + k * Cell);
            public Vec3d Body(int i, int j, int k) => Net.FromLocal(Local(i, j, k));

            /// <summary>Distance from the body's centre to the ground over a grid column.</summary>
            public double GroundOver(int i, int k)
            {
                long key = ((long)i << 32) ^ (uint)k;
                lock (Ground)
                {
                    if (Ground.TryGetValue(key, out double g)) return g;
                }
                Vec3d local = Local(i, 0, k);
                Vec3d dir = (Net.Origin + Net.East * local.X + Net.North * local.Z).Normalized;
                double value = Terrain.Radius + Terrain.Height(dir);
                lock (Ground) Ground[key] = value;
                return value;
            }
        }

        /// <summary>The space the walls are the edge of: negative in air (the cave, or the sky over the lip), positive in rock.</summary>
        static double Field(Grid g, Vec3d p, double groundRadius, out bool cave)
        {
            double air = g.Net.Air(p);
            double sky = (groundRadius - LipDepth) - p.Magnitude;
            cave = air <= sky;
            return Math.Min(air, sky);
        }

        static double FieldAt(Grid g, Vec3d p)
        {
            double r = p.Magnitude;
            double ground = r < g.Net.GroundLow - 3 ? g.Net.GroundLow : g.Terrain.Radius + g.Terrain.Height(p.Normalized);
            return Field(g, p, ground, out _);
        }

        /// <summary>Builds every block of walls. Blocks with nothing in them are left out.</summary>
        public static List<CaveChunk> Build(CaveNetwork net, PlanetTerrain terrain)
        {
            var g = new Grid { Net = net, Terrain = terrain };
            Vec3d min = net.LocalMin, max = net.LocalMax;
            // Room for the lips round the holes.
            foreach (CaveOpening o in net.Openings)
            {
                Vec3d mouth = net.ToLocal(o.Direction * (terrain.Radius + terrain.Height(o.Direction)));
                double reach = o.Radius + LipReach + 2;
                min = new Vec3d(Math.Min(min.X, mouth.X - reach), Math.Min(min.Y, mouth.Y - 4), Math.Min(min.Z, mouth.Z - reach));
                max = new Vec3d(Math.Max(max.X, mouth.X + reach), Math.Max(max.Y, mouth.Y + 4), Math.Max(max.Z, mouth.Z + reach));
            }
            g.Origin = min - new Vec3d(Cell * 2, Cell * 2, Cell * 2);
            g.Nx = (int)Math.Ceiling((max.X - g.Origin.X) / Cell) + 2;
            g.Ny = (int)Math.Ceiling((max.Y - g.Origin.Y) / Cell) + 2;
            g.Nz = (int)Math.Ceiling((max.Z - g.Origin.Z) / Cell) + 2;

            var chunks = new List<CaveChunk>();
            for (int bx = 0; bx * Block < g.Nx; bx++)
                for (int by = 0; by * Block < g.Ny; by++)
                    for (int bz = 0; bz * Block < g.Nz; bz++)
                    {
                        CaveChunk chunk = BuildBlock(g, bx * Block, by * Block, bz * Block);
                        if (chunk != null) chunks.Add(chunk);
                    }
            return chunks;
        }

        static bool NearLip(Grid g, Vec3d p)
        {
            Vec3d dir = p.Normalized;
            foreach (CaveOpening o in g.Net.Openings)
            {
                double reach = (o.Radius + LipReach) / g.Terrain.Radius;
                if ((dir - o.Direction).SqrMagnitude < reach * reach) return true;
            }
            return false;
        }

        static CaveChunk BuildBlock(Grid g, int i0, int j0, int k0)
        {
            int n = Block;
            // Is there anything here at all?
            Vec3d bmin = new Vec3d(double.MaxValue, double.MaxValue, double.MaxValue), bmax = -bmin;
            for (int c = 0; c < 8; c++)
            {
                Vec3d p = g.Body(i0 + ((c & 1) == 0 ? -1 : n + 1), j0 + ((c & 2) == 0 ? -1 : n + 1), k0 + ((c & 4) == 0 ? -1 : n + 1));
                bmin = new Vec3d(Math.Min(bmin.X, p.X), Math.Min(bmin.Y, p.Y), Math.Min(bmin.Z, p.Z));
                bmax = new Vec3d(Math.Max(bmax.X, p.X), Math.Max(bmax.Y, p.Y), Math.Max(bmax.Z, p.Z));
            }
            bool lip = false;
            foreach (CaveOpening o in g.Net.Openings)
            {
                Vec3d top = o.Direction * (g.Terrain.Radius + g.Terrain.Height(o.Direction));
                double reach = o.Radius + LipReach + 3;
                if (top.X > bmin.X - reach && top.Y > bmin.Y - reach && top.Z > bmin.Z - reach
                    && top.X < bmax.X + reach && top.Y < bmax.Y + reach && top.Z < bmax.Z + reach) lip = true;
            }
            if (!lip && !g.Net.Touches(bmin, bmax)) return null;

            // The space at every grid point of the block, one point over each side.
            int m = n + 3;
            var value = new double[m * m * m];
            var isCave = new bool[m * m * m];
            int Index(int a, int b, int c) => ((a + 1) * m + (b + 1)) * m + (c + 1);
            bool any = false, anyAir = false, anyRock = false;
            for (int a = -1; a <= n + 1; a++)
                for (int c = -1; c <= n + 1; c++)
                {
                    double ground = double.NaN;
                    for (int b = -1; b <= n + 1; b++)
                    {
                        Vec3d p = g.Body(i0 + a, j0 + b, k0 + c);
                        double r = p.Magnitude;
                        double groundRadius;
                        if (r < g.Net.GroundLow - 3) groundRadius = g.Net.GroundLow;
                        else
                        {
                            if (double.IsNaN(ground)) ground = g.GroundOver(i0 + a, k0 + c);
                            groundRadius = ground;
                        }
                        int at = Index(a, b, c);
                        value[at] = Field(g, p, groundRadius, out isCave[at]);
                        if (value[at] < 0) anyAir = true; else anyRock = true;
                    }
                }
            any = anyAir && anyRock;
            if (!any) return null;

            // One corner per cell the surface passes through.
            var cornerOf = new int[(n + 2) * (n + 2) * (n + 2)];
            for (int q = 0; q < cornerOf.Length; q++) cornerOf[q] = -1;
            int CellIndex(int a, int b, int c) => ((a + 1) * (n + 2) + (b + 1)) * (n + 2) + (c + 1);
            var positions = new List<Vec3d>();
            var caveCorner = new List<bool>();
            for (int a = -1; a <= n; a++)
                for (int b = -1; b <= n; b++)
                    for (int c = -1; c <= n; c++)
                    {
                        int mask = 0;
                        for (int q = 0; q < 8; q++)
                            if (value[Index(a + (q & 1), b + ((q >> 1) & 1), c + ((q >> 2) & 1))] < 0) mask |= 1 << q;
                        if (mask == 0 || mask == 255) continue;
                        // The average of where the surface crosses the cell's twelve edges.
                        Vec3d sum = Vec3d.Zero;
                        int crossings = 0, caveCount = 0, edgeCount = 0;
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
                                // The air end decides what kind of wall it is: cave air, or the sky over the lip.
                                int airEnd = inA ? ia : ib;
                                if (isCave[airEnd]) caveCount++;
                                edgeCount++;
                            }
                        }
                        Vec3d inCell = sum / crossings;
                        Vec3d local = g.Local(i0 + a, j0 + b, k0 + c) + inCell * Cell;
                        cornerOf[CellIndex(a, b, c)] = positions.Count;
                        positions.Add(g.Net.FromLocal(local));
                        caveCorner.Add(caveCount * 2 >= edgeCount);
                    }

            // Faces: one square for each grid edge the surface crosses, owned by the block its first end is in.
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
                            // The four cells round this edge.
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
                            // Keep the cave's own walls, under the ground; and the lip round the holes.
                            int keep = 0;
                            foreach (int corner in new[] { c0, c1, c2, c3 })
                            {
                                if (caveCorner[corner]) keep++;
                            }
                            Vec3d middle = (positions[c0] + positions[c1] + positions[c2] + positions[c3]) * 0.25;
                            bool caveWall = keep >= 2;
                            if (!NearLip(g, middle))
                            {
                                if (!caveWall) continue;
                                // A cave wall poking up above the real ground would float in the open air.
                                Vec3d local = g.Net.ToLocal(middle);
                                int ci = (int)Math.Round((local.X - g.Origin.X) / Cell), ck = (int)Math.Round((local.Z - g.Origin.Z) / Cell);
                                if (middle.Magnitude > g.Net.GroundLow - 3 && middle.Magnitude > g.GroundOver(ci, ck) - 0.1) continue;
                            }
                            // Faces look into the air.
                            // The four cells go round the edge anticlockwise seen from its far end, in the grid's
                            // frame, which is mirrored in the body's (east, up, north). Air at the near end turns it.
                            bool flip = v0 >= 0;
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
            if (triangles.Count == 0) return null;

            // Only the corners that are used, with their normals and colours.
            var remap = new int[positions.Count];
            int kept = 0;
            for (int q = 0; q < positions.Count; q++) remap[q] = used[q] ? kept++ : -1;
            var outPositions = new float[kept * 3];
            var outNormals = new float[kept * 3];
            var outColors = new float[kept * 3];
            Vec3d cmin = new Vec3d(double.MaxValue, double.MaxValue, double.MaxValue), cmax = -cmin;
            for (int q = 0; q < positions.Count; q++)
            {
                int o = remap[q];
                if (o < 0) continue;
                Vec3d p = positions[q];
                Vec3d normal = Normal(g, p);
                Paint(g, p, normal, caveCorner[q], out float red, out float green, out float blue);
                outPositions[o * 3] = (float)p.X; outPositions[o * 3 + 1] = (float)p.Y; outPositions[o * 3 + 2] = (float)p.Z;
                outNormals[o * 3] = (float)normal.X; outNormals[o * 3 + 1] = (float)normal.Y; outNormals[o * 3 + 2] = (float)normal.Z;
                outColors[o * 3] = red; outColors[o * 3 + 1] = green; outColors[o * 3 + 2] = blue;
                cmin = new Vec3d(Math.Min(cmin.X, p.X), Math.Min(cmin.Y, p.Y), Math.Min(cmin.Z, p.Z));
                cmax = new Vec3d(Math.Max(cmax.X, p.X), Math.Max(cmax.Y, p.Y), Math.Max(cmax.Z, p.Z));
            }
            var outTriangles = new int[triangles.Count];
            for (int q = 0; q < triangles.Count; q++) outTriangles[q] = remap[triangles[q]];
            return new CaveChunk
            {
                Shape = new ShapeData { Positions = outPositions, Normals = outNormals, Colors = outColors, Triangles = outTriangles },
                Min = cmin, Max = cmax,
            };
        }

        /// <summary>The way out of the rock at a point on the wall, from the slope of the space there.</summary>
        static Vec3d Normal(Grid g, Vec3d p)
        {
            const double h = 0.35;
            double dx = FieldAt(g, p + new Vec3d(h, 0, 0)) - FieldAt(g, p - new Vec3d(h, 0, 0));
            double dy = FieldAt(g, p + new Vec3d(0, h, 0)) - FieldAt(g, p - new Vec3d(0, h, 0));
            double dz = FieldAt(g, p + new Vec3d(0, 0, h)) - FieldAt(g, p - new Vec3d(0, 0, h));
            Vec3d grad = new Vec3d(dx, dy, dz);
            return grad.SqrMagnitude > 1e-12 ? -grad.Normalized : p.Normalized;
        }

        /// <summary>How much of the cave a point on the wall can see, 0 deep in a crack to 1 out in the open.</summary>
        static double Open(Grid g, Vec3d p, Vec3d normal)
        {
            double near = Noise3.Clamp01(-FieldAt(g, p + normal * 0.9) / 0.9);
            double far = Noise3.Clamp01(-FieldAt(g, p + normal * 2.6) / 2.6);
            return near * 0.55 + far * 0.45;
        }

        static void Paint(Grid g, Vec3d p, Vec3d normal, bool cave, out float red, out float green, out float blue)
        {
            CaveNetwork net = g.Net;
            Vec3d up = p.Normalized;
            if (!cave)
            {
                // The lip round a hole is the ground itself, a little lower: it takes the ground's colour.
                PlanetTerrain t = g.Terrain;
                TerrainPoint point = t.Sample(up);
                t.ColorAt(up, point, t.SlopeAt(up, 2), out red, out green, out blue);
                return;
            }
            double facing = Vec3d.Dot(normal, up);
            double depth = p.Magnitude;
            uint seed = net.Seed;
            // Bedding: the rock was laid down in layers, so faint bands run level through it.
            double warp = Noise3.Perlin(p, 9, seed + 11) * 1.4;
            double band = Noise3.Perlin(depth / 1.15 + warp, 0.37, 0.71, seed + 12);
            double tone = 1 + band * 0.09 + Noise3.Perlin(p, 3.1, seed + 13) * 0.06;
            double r = net.R * tone, gr = net.G * tone, b = net.B * tone;

            switch (net.Style)
            {
                case CaveStyle.Karst:
                {
                    // Flowstone: streaks of warmer, paler calcite running down the walls.
                    Vec3d level = p - up * Vec3d.Dot(p, up);
                    double streak = Noise3.Perlin(level, 1.3, seed + 14) * 0.5 + Noise3.Perlin(p, 6, seed + 15) * 0.5;
                    double flow = Noise3.Smooth(0.15, 0.55, streak) * (1 - Noise3.Smooth(0.2, 0.7, Math.Abs(facing)));
                    r = Noise3.Lerp(r, 0.71, flow * 0.5); gr = Noise3.Lerp(gr, 0.61, flow * 0.5); b = Noise3.Lerp(b, 0.47, flow * 0.5);
                    break;
                }
                case CaveStyle.LavaTube:
                {
                    // Glassy, ropy basalt with rusty patches where water once seeped.
                    double rust = Noise3.Smooth(0.35, 0.7, Noise3.Perlin(p, 4.5, seed + 16));
                    r = Noise3.Lerp(r, r * 1.25 + 0.03, rust * 0.4); gr = Noise3.Lerp(gr, gr * 1.02, rust * 0.4); b = Noise3.Lerp(b, b * 0.85, rust * 0.4);
                    break;
                }
                case CaveStyle.Ice:
                {
                    // Clear ice looks deeper blue the thicker it is: darker and bluer where the wall turns in.
                    double deep = Noise3.Smooth(-0.2, 0.6, Noise3.Perlin(p, 5, seed + 17));
                    r = Noise3.Lerp(r, 0.36, deep * 0.4); gr = Noise3.Lerp(gr, 0.58, deep * 0.4); b = Noise3.Lerp(b, 0.78, deep * 0.4);
                    break;
                }
            }

            // Floors are mud, dust or frost; roofs are darker and stained.
            double floor = Noise3.Smooth(0.5, 0.82, facing) * (0.8 + 0.2 * Noise3.Perlin(p, 2.3, seed + 18));
            r = Noise3.Lerp(r, net.FloorR, floor); gr = Noise3.Lerp(gr, net.FloorG, floor); b = Noise3.Lerp(b, net.FloorB, floor);
            double roof = Noise3.Smooth(-0.4, -0.85, facing);
            r *= 1 - roof * 0.14; gr *= 1 - roof * 0.15; b *= 1 - roof * 0.12;

            // Hollows and cracks see little of the cave and catch little light.
            double open = Open(g, p, normal);
            double shade = 0.42 + 0.58 * open;
            if (net.Style == CaveStyle.Ice)
            {
                r *= shade * 0.9; gr *= shade * 0.97; b *= 0.55 + 0.45 * shade;
            }
            else
            {
                r *= shade; gr *= shade; b *= shade;
            }
            red = (float)Noise3.Clamp01(r);
            green = (float)Noise3.Clamp01(gr);
            blue = (float)Noise3.Clamp01(b);
        }
    }
}
