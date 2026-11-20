using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Planets
{
    /// <summary>One square tile of ground, as plain lists of numbers ready to hand to the engine.</summary>
    public sealed class ChunkData
    {
        /// <summary>x, y, z per corner, in metres from the body's centre.</summary>
        public float[] Positions;
        public float[] Normals;
        /// <summary>r, g, b per corner, 0 to 1.</summary>
        public float[] Colors;
        public int[] Triangles;
        /// <summary>
        /// How many of <see cref="Triangles"/> (as indices, three a triangle) are the ground itself; the rest are
        /// the skirts. Only the ground goes in the collider: a skirt is an upright wall at a tile's edge, and where
        /// tiles of different detail meet it stands a little proud and catches the suit.
        /// </summary>
        public int GroundIndexCount;
        /// <summary>The middle of the tile on the ground, in metres from the body's centre.</summary>
        public Vec3d Centre;
        /// <summary>Lowest and highest ground in the tile, in metres above the body's surface radius.</summary>
        public double MinHeight, MaxHeight;
        /// <summary>Distance from one corner to the next, in metres.</summary>
        public double CellSize;
        public bool HasHole;
        public int VertexCount => Positions.Length / 3;
    }

    /// <summary>
    /// Builds one tile of a body's ground from the terrain rules. A body is six square faces (see
    /// <see cref="CubeSphere"/>); each face is cut in four again and again, and <paramref name="level"/> says how
    /// many times. Pure sums with no Unity types, so it runs on background threads and in tests.
    /// </summary>
    public static class TerrainMesher
    {
        /// <summary>Squares along one edge of a tile.</summary>
        public const int Quads = 32;

        [ThreadStatic] static Vec3d[] _points;
        [ThreadStatic] static Vec3d[] _dirs;
        [ThreadStatic] static TerrainPoint[] _samples;
        [ThreadStatic] static int[] _skirtOf;
        [ThreadStatic] static int[] _edgeOf;

        /// <summary>How strongly a dip or crest in the ground shows in its colour. See <see cref="PlanetTerrain.ColorAt"/>.</summary>
        public const double HollowGain = 8.5;

        /// <summary>Size of one tile's edge in metres.</summary>
        public static double TileSize(double radius, int level) => CubeSphere.FaceSize(radius) / (1 << level);

        public static double CellSize(double radius, int level) => TileSize(radius, level) / Quads;

        public static Vec3d TileCentre(int face, int level, int ix, int iy)
        {
            double size = 2.0 / (1 << level);
            return CubeSphere.Direction(face, -1 + (ix + 0.5) * size, -1 + (iy + 0.5) * size);
        }

        public static ChunkData Build(PlanetTerrain terrain, int face, int level, int ix, int iy, bool cutHoles)
        {
            const int n = Quads;
            // One extra ring of points all round, so the lighting at a tile's edge matches its neighbour's.
            const int g = n + 3;
            double size = 2.0 / (1 << level);
            double u0 = -1 + ix * size, v0 = -1 + iy * size;
            double cell = CellSize(terrain.Radius, level);
            // A grid cannot show bumps smaller than about two cells, so do not pay to work them out.
            double minWavelength = cell * 1.6;

            // Working space is kept per thread and reused, so building ground makes as little garbage as possible.
            Vec3d[] points = _points ?? (_points = new Vec3d[g * g]);
            Vec3d[] dirs = _dirs ?? (_dirs = new Vec3d[g * g]);
            TerrainPoint[] samples = _samples ?? (_samples = new TerrainPoint[g * g]);
            double low = double.MaxValue, high = double.MinValue;
            for (int j = 0; j < g; j++)
            {
                double v = v0 + (j - 1) / (double)n * size;
                for (int i = 0; i < g; i++)
                {
                    double u = u0 + (i - 1) / (double)n * size;
                    Vec3d dir = CubeSphere.Direction(face, u, v);
                    TerrainPoint s = terrain.Sample(dir, minWavelength);
                    int k = j * g + i;
                    dirs[k] = dir;
                    samples[k] = s;
                    points[k] = dir * (terrain.Radius + s.Height);
                    if (i >= 1 && i <= n + 1 && j >= 1 && j <= n + 1)
                    {
                        if (s.Height < low) low = s.Height;
                        if (s.Height > high) high = s.Height;
                    }
                }
            }

            int side = n + 1;
            int surface = side * side;
            int skirt = side * 4;
            var data = new ChunkData
            {
                Positions = new float[(surface + skirt) * 3],
                Normals = new float[(surface + skirt) * 3],
                Colors = new float[(surface + skirt) * 3],
                MinHeight = low, MaxHeight = high, CellSize = cell,
                Centre = TileCentre(face, level, ix, iy),
            };
            data.Centre = data.Centre * (terrain.Radius + terrain.Height(data.Centre, minWavelength));

            for (int j = 0; j < side; j++)
            {
                for (int i = 0; i < side; i++)
                {
                    int k = (j + 1) * g + (i + 1);
                    Vec3d across = points[k + 1] - points[k - 1];
                    Vec3d up = points[k + g] - points[k - g];
                    Vec3d normal = Vec3d.Cross(across, up).Normalized;
                    if (Vec3d.Dot(normal, dirs[k]) < 0) normal = -normal;
                    double cos = Vec3d.Dot(normal, dirs[k]);
                    double slope = Math.Sqrt(Math.Max(0, 1 - cos * cos));
                    // How far this corner sits below (or above) the middle of its four neighbours: a dip or a crest.
                    // Colour uses it, so creases read dark and edges pale. It is measured as a curve in the ground
                    // (per metre, squared spacing), so coarse far tiles and fine near ones agree about the same dip.
                    double around = (samples[k + 1].Height + samples[k - 1].Height + samples[k + g].Height + samples[k - g].Height) * 0.25;
                    double hollow = (around - samples[k].Height) / (cell * cell) * HollowGain;
                    hollow = hollow < -1 ? -1 : hollow > 1 ? 1 : hollow;
                    terrain.ColorAt(dirs[k], samples[k], slope, out float r, out float gr, out float b, hollow);
                    int o = (j * side + i) * 3;
                    data.Positions[o] = (float)points[k].X;
                    data.Positions[o + 1] = (float)points[k].Y;
                    data.Positions[o + 2] = (float)points[k].Z;
                    data.Normals[o] = (float)normal.X;
                    data.Normals[o + 1] = (float)normal.Y;
                    data.Normals[o + 2] = (float)normal.Z;
                    data.Colors[o] = r;
                    data.Colors[o + 1] = gr;
                    data.Colors[o + 2] = b;
                }
            }

            // The skirt: a curtain hanging down from every edge. Neighbouring tiles of different detail do not
            // line up exactly, and the curtain hides the hairline gaps that would otherwise show sky.
            double drop = cell * 2.0 + 0.5;
            int next = surface;
            int[] skirtOf = _skirtOf ?? (_skirtOf = new int[skirt]);
            int[] edgeOf = _edgeOf ?? (_edgeOf = new int[skirt]);
            for (int e = 0; e < 4; e++)
            {
                for (int t = 0; t < side; t++)
                {
                    int i = e == 0 ? t : e == 1 ? n : e == 2 ? n - t : 0;
                    int j = e == 0 ? 0 : e == 1 ? t : e == 2 ? n : n - t;
                    int from = j * side + i;
                    int k = (j + 1) * g + (i + 1);
                    Vec3d lowered = points[k] - dirs[k] * drop;
                    int o = next * 3;
                    data.Positions[o] = (float)lowered.X;
                    data.Positions[o + 1] = (float)lowered.Y;
                    data.Positions[o + 2] = (float)lowered.Z;
                    Array.Copy(data.Normals, from * 3, data.Normals, o, 3);
                    Array.Copy(data.Colors, from * 3, data.Colors, o, 3);
                    skirtOf[e * side + t] = next;
                    edgeOf[e * side + t] = from;
                    next++;
                }
            }

            // Which way round the corners of a triangle must go for it to face outward depends on the face.
            Vec3d a0 = points[g + 1], b0 = points[g + 2], c0 = points[2 * g + 1];
            bool flip = Vec3d.Dot(Vec3d.Cross(b0 - a0, c0 - a0), dirs[g + 1]) < 0;

            var triangles = new int[n * n * 6 + 4 * n * 12];
            int tri = 0;
            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    if (cutHoles && terrain.Holes.Count > 0)
                    {
                        int k = (j + 1) * g + (i + 1);
                        Vec3d middle = (dirs[k] + dirs[k + g + 1]).Normalized;
                        if (terrain.InHole(middle, cell))
                        {
                            data.HasHole = true;
                            continue;
                        }
                    }
                    int a = j * side + i, b = a + 1, c = a + side, d = c + 1;
                    if (flip)
                    {
                        triangles[tri++] = a; triangles[tri++] = c; triangles[tri++] = b;
                        triangles[tri++] = b; triangles[tri++] = c; triangles[tri++] = d;
                    }
                    else
                    {
                        triangles[tri++] = a; triangles[tri++] = b; triangles[tri++] = c;
                        triangles[tri++] = b; triangles[tri++] = d; triangles[tri++] = c;
                    }
                }
            }
            data.GroundIndexCount = tri;
            // Skirts are drawn from both sides: they are only ever glimpsed edge-on, and this cannot be got wrong.
            for (int e = 0; e < 4; e++)
            {
                for (int t = 0; t < n; t++)
                {
                    int a = edgeOf[e * side + t], b = edgeOf[e * side + t + 1];
                    int c = skirtOf[e * side + t], d = skirtOf[e * side + t + 1];
                    triangles[tri++] = a; triangles[tri++] = b; triangles[tri++] = c;
                    triangles[tri++] = b; triangles[tri++] = d; triangles[tri++] = c;
                    triangles[tri++] = a; triangles[tri++] = c; triangles[tri++] = b;
                    triangles[tri++] = b; triangles[tri++] = c; triangles[tri++] = d;
                }
            }
            if (tri != triangles.Length) Array.Resize(ref triangles, tri);
            data.Triangles = triangles;
            return data;
        }

        /// <summary>
        /// Distance from the body's centre to the ground a tile really has in a direction: the flat triangle of
        /// <see cref="Build"/> under it, with the same corners, the same diagonal and the same left-out fine bumps.
        /// That is the shape that is drawn and that the collider is made from, so this is where a foot really
        /// stands. <see cref="PlanetTerrain.Height"/> with no wavelength is the ideal ground with every bump in, and
        /// can lie a metre or more off it (fine bumps, cliffs cut across by a triangle). Directions outside the tile
        /// are measured on its nearest edge cell.
        /// </summary>
        public static double SurfaceRadius(PlanetTerrain terrain, int face, int level, int ix, int iy, Vec3d direction)
        {
            SurfaceCell(face, level, ix, iy, direction, out int i, out int j, out bool far);
            CellPlane(terrain, face, level, ix, iy, i, j, far, out Vec3d normal, out double offset);
            return RadiusOnPlane(normal, offset, direction);
        }

        /// <summary>
        /// Which of a tile's squares a direction falls in (clamped to the tile), and which of its two triangles:
        /// <paramref name="far"/> is the one with the square's far corner. Cheap: no ground is worked out.
        /// </summary>
        public static void SurfaceCell(int face, int level, int ix, int iy, Vec3d direction, out int i, out int j, out bool far)
        {
            const int n = Quads;
            double size = 2.0 / (1 << level);
            CubeSphere.LocateOn(direction, face, out double u, out double v);
            double fx = (u - (-1 + ix * size)) / size * n, fy = (v - (-1 + iy * size)) / size * n;
            i = Math.Min(n - 1, Math.Max(0, (int)Math.Floor(fx)));
            j = Math.Min(n - 1, Math.Max(0, (int)Math.Floor(fy)));
            far = (fx - i) + (fy - j) > 1;
        }

        /// <summary>
        /// The flat plane of one of a tile's triangles, as Build makes it: points x on it have dot(normal, x) =
        /// offset, in metres from the body's centre. Build's two triangles in a square are a-b-c and b-d-c (a the
        /// square's low corner, d its far one), so the diagonal is b-c.
        /// </summary>
        public static void CellPlane(PlanetTerrain terrain, int face, int level, int ix, int iy, int i, int j, bool far, out Vec3d normal, out double offset)
        {
            const int n = Quads;
            double size = 2.0 / (1 << level);
            double minWavelength = CellSize(terrain.Radius, level) * 1.6;
            double u0 = -1 + ix * size, v0 = -1 + iy * size;
            // Corners are placed exactly as Build places them (same sums), so the plane is the mesh's own.
            double ua = u0 + i / (double)n * size, ub = u0 + (i + 1) / (double)n * size;
            double va = v0 + j / (double)n * size, vb = v0 + (j + 1) / (double)n * size;
            Vec3d b = Corner(terrain, face, ub, va, minWavelength);
            Vec3d c = Corner(terrain, face, ua, vb, minWavelength);
            Vec3d other = far ? Corner(terrain, face, ub, vb, minWavelength) : Corner(terrain, face, ua, va, minWavelength);
            normal = Vec3d.Cross(c - b, other - b);
            double length = normal.Magnitude;
            if (length < 1e-12)
            {
                // A squashed triangle (never on real ground): a ball through its corners instead.
                normal = Vec3d.Zero;
                offset = (b.Magnitude + c.Magnitude + other.Magnitude) / 3.0;
                return;
            }
            normal = normal / length;
            offset = Vec3d.Dot(normal, b);
        }

        /// <summary>Distance from the centre to a plane from <see cref="CellPlane"/> along a direction.</summary>
        public static double RadiusOnPlane(Vec3d normal, double offset, Vec3d direction)
        {
            if (normal.SqrMagnitude < 0.5) return offset;
            double along = Vec3d.Dot(normal, direction.Normalized);
            if (Math.Abs(along) < 1e-9) return offset;
            return offset / along;
        }

        static Vec3d Corner(PlanetTerrain terrain, int face, double u, double v, double minWavelength)
        {
            Vec3d dir = CubeSphere.Direction(face, u, v);
            return dir * (terrain.Radius + terrain.Sample(dir, minWavelength).Height);
        }
    }
}
