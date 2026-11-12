using System;
using System.Collections.Generic;

namespace OrbitalAnte.Ship
{
    /// <summary>A point or direction in the ship's own space, in metres. Plain numbers, no Unity types.</summary>
    public struct P3
    {
        public float X, Y, Z;
        public P3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public P3(double x, double y, double z) { X = (float)x; Y = (float)y; Z = (float)z; }
        public static P3 operator +(P3 a, P3 b) => new P3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static P3 operator -(P3 a, P3 b) => new P3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static P3 operator -(P3 a) => new P3(-a.X, -a.Y, -a.Z);
        public static P3 operator *(P3 a, float s) => new P3(a.X * s, a.Y * s, a.Z * s);
        public static P3 operator *(P3 a, P3 b) => new P3(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
        public static float Dot(P3 a, P3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static P3 Cross(P3 a, P3 b) => new P3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        public P3 Unit()
        {
            float l = Length;
            return l > 1e-12f ? new P3(X / l, Y / l, Z / l) : new P3(0, 1, 0);
        }
        public static P3 Lerp(P3 a, P3 b, float t) => a + (b - a) * t;
        public static float Distance(P3 a, P3 b) => (a - b).Length;
        public static readonly P3 Zero = new P3(0, 0, 0);
        public static readonly P3 Up = new P3(0, 1, 0);
        public override string ToString() => "(" + X.ToString("0.###") + ", " + Y.ToString("0.###") + ", " + Z.ToString("0.###") + ")";
    }

    /// <summary>
    /// One finished mesh in plain arrays. Besides where each corner is, it carries the surface's own colour and
    /// how much light it gives off, which is what the light baker needs.
    /// </summary>
    public sealed class ShipMesh
    {
        public float[] Positions;   // x,y,z per corner
        public float[] Colors;      // r,g,b per corner, 0..1: the paint colour (as seen on screen, not linear)
        public float[] Glow;        // per corner: light given off, 0 for ordinary surfaces
        public int[] Triangles;
        public int VertexCount => Positions.Length / 3;
        public int TriangleCount => Triangles.Length / 3;

        public P3 Position(int i) => new P3(Positions[i * 3], Positions[i * 3 + 1], Positions[i * 3 + 2]);
        public P3 Color(int i) => new P3(Colors[i * 3], Colors[i * 3 + 1], Colors[i * 3 + 2]);
    }

    /// <summary>
    /// Builds meshes a piece at a time. Corners that are shared between triangles give a smooth, rounded look;
    /// pieces added separately keep a crisp edge between them. Triangles (a, b, c) face the side from which
    /// a, b, c run anticlockwise... in Unity's left-handed space that is the side where
    /// cross(b - a, c - a) points toward you.
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<P3> _points = new List<P3>();
        readonly List<P3> _colors = new List<P3>();
        readonly List<float> _glow = new List<float>();
        readonly List<int> _triangles = new List<int>();

        /// <summary>Colour given to corners added from now on.</summary>
        public P3 Paint = new P3(0.8f, 0.8f, 0.8f);
        /// <summary>Light given off by corners added from now on (0 for none).</summary>
        public float Glow;

        public int VertexCount => _points.Count;
        public int TriangleCount => _triangles.Count / 3;

        public MeshBuilder Color(double r, double g, double b, double glow = 0)
        {
            Paint = new P3(r, g, b);
            Glow = (float)glow;
            return this;
        }

        /// <summary>Web colour such as "#C8A24A".</summary>
        public MeshBuilder Color(string web, double glow = 0)
        {
            Paint = Web(web);
            Glow = (float)glow;
            return this;
        }

        public static P3 Web(string web)
        {
            string s = web.TrimStart('#');
            int v = Convert.ToInt32(s, 16);
            return new P3(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);
        }

        public int Add(P3 p)
        {
            _points.Add(p);
            _colors.Add(Paint);
            _glow.Add(Glow);
            return _points.Count - 1;
        }

        public P3 PointAt(int index) => _points[index];

        /// <summary>
        /// Two triangles for four corners, turned so the panel is seen from <paramref name="eye"/>
        /// (or, with <paramref name="away"/>, from the far side).
        /// </summary>
        public void QuadSeenFrom(int a, int b, int c, int d, P3 eye, bool away = false)
        {
            P3 pa = _points[a], pb = _points[b], pc = _points[c], pd = _points[d];
            P3 normal = P3.Cross(pb - pa, pc - pa) + P3.Cross(pc - pa, pd - pa);
            P3 middle = (pa + pb + pc + pd) * 0.25f;
            bool seen = P3.Dot(normal, eye - middle) > 0f;
            Quad(a, b, c, d, flip: seen == away);
        }

        public void TriSeenFrom(int a, int b, int c, P3 eye, bool away = false)
        {
            P3 pa = _points[a], pb = _points[b], pc = _points[c];
            bool seen = P3.Dot(P3.Cross(pb - pa, pc - pa), eye - (pa + pb + pc) * (1f / 3f)) > 0f;
            if (seen == away) Tri(a, c, b);
            else Tri(a, b, c);
        }

        public void Tri(int a, int b, int c)
        {
            _triangles.Add(a);
            _triangles.Add(b);
            _triangles.Add(c);
        }

        /// <summary>Two triangles for the four corners a, b, c, d given in order round the edge.</summary>
        public void Quad(int a, int b, int c, int d, bool flip = false)
        {
            if (flip) { Tri(a, c, b); Tri(a, d, c); }
            else { Tri(a, b, c); Tri(a, c, d); }
        }

        /// <summary>A flat four-cornered panel with its own corners (crisp edges). Corners in order round the edge.</summary>
        public void Panel(P3 a, P3 b, P3 c, P3 d, bool flip = false)
        {
            Quad(Add(a), Add(b), Add(c), Add(d), flip);
        }

        /// <summary>
        /// A sheet of columns by rows corners from a rule, joined into quads. Returns the index of the first
        /// corner; corner (i, j) is first + j * columns + i. <paramref name="skip"/> leaves holes (for windows).
        /// With <paramref name="wrap"/> the last column joins back to the first.
        /// </summary>
        public int Sheet(int columns, int rows, Func<int, int, P3> at, bool flip = false, bool wrap = false, Func<int, int, bool> skip = null)
        {
            int first = _points.Count;
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < columns; i++) Add(at(i, j));
            }
            int across = wrap ? columns : columns - 1;
            for (int j = 0; j < rows - 1; j++)
            {
                for (int i = 0; i < across; i++)
                {
                    if (skip != null && skip(i, j)) continue;
                    int i1 = (i + 1) % columns;
                    int a = first + j * columns + i, b = first + j * columns + i1;
                    int c = first + (j + 1) * columns + i1, d = first + (j + 1) * columns + i;
                    Quad(a, b, c, d, flip);
                }
            }
            return first;
        }

        /// <summary>Joins two closed loops of the same number of corners with a band of quads.</summary>
        public void Band(IList<P3> outer, IList<P3> inner, bool flip = false)
        {
            int n = outer.Count;
            int o = _points.Count;
            for (int i = 0; i < n; i++) Add(outer[i]);
            int k = _points.Count;
            for (int i = 0; i < n; i++) Add(inner[i]);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Quad(o + i, o + j, k + j, k + i, flip);
            }
        }

        /// <summary>Fills a closed loop with triangles that meet at its middle.</summary>
        public void Fan(IList<P3> loop, bool flip = false)
        {
            P3 centre = P3.Zero;
            for (int i = 0; i < loop.Count; i++) centre += loop[i];
            centre *= 1f / loop.Count;
            int c = Add(centre);
            int first = _points.Count;
            for (int i = 0; i < loop.Count; i++) Add(loop[i]);
            for (int i = 0; i < loop.Count; i++)
            {
                int j = (i + 1) % loop.Count;
                if (flip) Tri(c, first + j, first + i);
                else Tri(c, first + i, first + j);
            }
        }

        /// <summary>
        /// A shape turned on a lathe round an upright axis through <paramref name="centre"/>: a list of
        /// (radius, height) pairs from bottom to top. Faces outward unless <paramref name="inside"/> is set.
        /// </summary>
        public void Lathe(P3 centre, IList<float> radii, IList<float> heights, int sides, bool inside = false, bool capBottom = false, bool capTop = false)
        {
            int rows = radii.Count;
            Sheet(sides, rows, (i, j) =>
            {
                double a = i * 2.0 * Math.PI / sides;
                return centre + new P3(Math.Sin(a) * radii[j], heights[j], Math.Cos(a) * radii[j]);
            }, flip: inside, wrap: true);
            if (capBottom) Disc(centre + new P3(0, heights[0], 0), radii[0], sides, faceUp: inside);
            if (capTop) Disc(centre + new P3(0, heights[rows - 1], 0), radii[rows - 1], sides, faceUp: !inside);
        }

        /// <summary>A flat round plate lying level.</summary>
        public void Disc(P3 centre, float radius, int sides, bool faceUp = true, float innerRadius = 0f)
        {
            if (innerRadius <= 0f)
            {
                var loop = new List<P3>(sides);
                for (int i = 0; i < sides; i++)
                {
                    double a = i * 2.0 * Math.PI / sides;
                    loop.Add(centre + new P3(Math.Sin(a) * radius, 0, Math.Cos(a) * radius));
                }
                Fan(loop, flip: !faceUp);
                return;
            }
            Sheet(sides, 2, (i, j) =>
            {
                double a = i * 2.0 * Math.PI / sides;
                float r = j == 0 ? innerRadius : radius;
                return centre + new P3(Math.Sin(a) * r, 0, Math.Cos(a) * r);
            }, flip: faceUp, wrap: true);
        }

        /// <summary>
        /// A round bar or pipe from <paramref name="a"/> to <paramref name="b"/>, optionally a different
        /// thickness at each end (a cone), with flat ends.
        /// </summary>
        public void Tube(P3 a, P3 b, float radiusA, float radiusB, int sides = 10, bool caps = true)
        {
            P3 axis = (b - a).Unit();
            P3 side = Math.Abs(axis.Y) < 0.9f ? P3.Cross(axis, P3.Up).Unit() : P3.Cross(axis, new P3(1, 0, 0)).Unit();
            P3 other = P3.Cross(axis, side);
            Sheet(sides, 2, (i, j) =>
            {
                double t = i * 2.0 * Math.PI / sides;
                float r = j == 0 ? radiusA : radiusB;
                return (j == 0 ? a : b) + side * ((float)Math.Cos(t) * r) + other * ((float)Math.Sin(t) * r);
            }, flip: false, wrap: true);
            if (!caps) return;
            var loopA = new List<P3>(sides);
            var loopB = new List<P3>(sides);
            for (int i = 0; i < sides; i++)
            {
                double t = i * 2.0 * Math.PI / sides;
                P3 d = side * (float)Math.Cos(t) + other * (float)Math.Sin(t);
                loopA.Add(a + d * radiusA);
                loopB.Add(b + d * radiusB);
            }
            Fan(loopA, flip: true);
            Fan(loopB, flip: false);
        }

        /// <summary>A box with crisp edges, given its middle, its three half-sizes and optionally its own axes.</summary>
        public void Box(P3 centre, P3 half, P3? right = null, P3? up = null)
        {
            P3 r = (right ?? new P3(1, 0, 0)).Unit();
            P3 u = (up ?? P3.Up).Unit();
            P3 f = P3.Cross(r, u).Unit();
            P3 rx = r * half.X, uy = u * half.Y, fz = f * half.Z;
            Face(centre, rx, uy, fz);
            Face(centre, -rx, uy, fz);
            Face(centre, uy, fz, rx);
            Face(centre, -uy, fz, rx);
            Face(centre, fz, rx, uy);
            Face(centre, -fz, rx, uy);
        }

        void Face(P3 centre, P3 outward, P3 a, P3 b)
        {
            P3 middle = centre + outward;
            PanelFacing(middle - a - b, middle + a - b, middle + a + b, middle - a + b, outward);
        }

        /// <summary>A flat panel with its own corners, turned so that it faces along <paramref name="toward"/>.</summary>
        public void PanelFacing(P3 a, P3 b, P3 c, P3 d, P3 toward)
        {
            bool faces = P3.Dot(P3.Cross(b - a, c - a), toward) > 0f;
            Panel(a, b, c, d, flip: !faces);
        }

        /// <summary>A ring like a doughnut lying level round <paramref name="centre"/>.</summary>
        public void Torus(P3 centre, float ringRadius, float tubeRadius, int around = 32, int sides = 10)
        {
            Sheet(around, sides, (i, j) =>
            {
                double a = i * 2.0 * Math.PI / around;
                double t = j * 2.0 * Math.PI / sides;
                double r = ringRadius + Math.Cos(t) * tubeRadius;
                return centre + new P3(Math.Sin(a) * r, Math.Sin(t) * tubeRadius, Math.Cos(a) * r);
            }, flip: false, wrap: true);
            // Close the tube round its small circle.
            int first = _points.Count - around * sides;
            for (int i = 0; i < around; i++)
            {
                int i1 = (i + 1) % around;
                int a0 = first + (sides - 1) * around + i, b0 = first + (sides - 1) * around + i1;
                Quad(a0, b0, first + i1, first + i, false);
            }
        }

        /// <summary>A ball, optionally squashed.</summary>
        public void Ball(P3 centre, P3 radii, int around = 16, int down = 10)
        {
            Sheet(around, down + 1, (i, j) =>
            {
                double a = i * 2.0 * Math.PI / around;
                double p = Math.PI * j / down;
                return centre + new P3(Math.Sin(a) * Math.Sin(p) * radii.X, -Math.Cos(p) * radii.Y, Math.Cos(a) * Math.Sin(p) * radii.Z);
            }, flip: false, wrap: true);
        }

        /// <summary>Adds everything from another builder, moved and turned (turn is about the upright axis, in degrees).</summary>
        public void Append(MeshBuilder other, P3 offset, float yawDegrees = 0f)
        {
            double a = yawDegrees * Math.PI / 180.0;
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            int first = _points.Count;
            for (int i = 0; i < other._points.Count; i++)
            {
                P3 p = other._points[i];
                _points.Add(new P3(p.X * c + p.Z * s, p.Y, -p.X * s + p.Z * c) + offset);
                _colors.Add(other._colors[i]);
                _glow.Add(other._glow[i]);
            }
            for (int i = 0; i < other._triangles.Count; i++) _triangles.Add(first + other._triangles[i]);
        }

        public ShipMesh Finish()
        {
            var mesh = new ShipMesh
            {
                Positions = new float[_points.Count * 3],
                Colors = new float[_points.Count * 3],
                Glow = _glow.ToArray(),
                Triangles = _triangles.ToArray(),
            };
            for (int i = 0; i < _points.Count; i++)
            {
                mesh.Positions[i * 3] = _points[i].X;
                mesh.Positions[i * 3 + 1] = _points[i].Y;
                mesh.Positions[i * 3 + 2] = _points[i].Z;
                mesh.Colors[i * 3] = _colors[i].X;
                mesh.Colors[i * 3 + 1] = _colors[i].Y;
                mesh.Colors[i * 3 + 2] = _colors[i].Z;
            }
            return mesh;
        }
    }
}
