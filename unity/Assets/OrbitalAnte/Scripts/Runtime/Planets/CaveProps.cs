using System;
using System.Collections.Generic;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// Stand-in shapes for the things enemies bring into a cave: a site work lamp on a tripod with a cable to a
    /// small generator case. Built in code until the real model is made in Blender (it should then be a model with
    /// a "Glow" piece for the lamp's lens). Metres, y up, the lamp head facing +z, standing on y = 0. Pure sums.
    /// </summary>
    public static class CaveProps
    {
        static readonly float[] Steel = { 0.26f, 0.27f, 0.28f };
        static readonly float[] Paint = { 0.62f, 0.50f, 0.18f };   // worn safety yellow, greyed
        static readonly float[] Rubber = { 0.09f, 0.09f, 0.09f };

        /// <summary>The lamp, its tripod and its generator; the lens is <see cref="WorkLampGlow"/>.</summary>
        public static ShapeData WorkLamp(out ShapeData glow)
        {
            var b = new Box();
            // Tripod: three splayed legs to a collar.
            for (int i = 0; i < 3; i++)
            {
                double a = i * Math.PI * 2 / 3 + 0.5;
                b.Rod(Math.Cos(a) * 0.55, 0.0, Math.Sin(a) * 0.55, 0, 0.9, 0, 0.022, Steel);
            }
            // Mast up to the head.
            b.Rod(0, 0.85, 0, 0, 1.62, 0, 0.026, Steel);
            // The head: a case with a deep rim round the lens.
            b.Cube(0, 1.72, 0.02, 0.24, 0.17, 0.09, Paint);
            b.Cube(0, 1.72, 0.12, 0.26, 0.19, 0.02, Rubber);
            b.Cube(0, 1.72, -0.1, 0.18, 0.12, 0.04, Steel);   // heat fins at the back
            // A cable down the mast and across to the generator case.
            b.Rod(0.03, 1.6, 0, 0.05, 0.05, 0.05, 0.012, Rubber);
            b.Rod(0.05, 0.03, 0.05, 0.75, 0.03, -0.55, 0.012, Rubber);
            b.Cube(0.85, 0.2, -0.65, 0.28, 0.2, 0.18, Paint);
            b.Cube(0.85, 0.42, -0.65, 0.22, 0.03, 0.14, Steel);
            glow = WorkLampGlow();
            return b.Shape();
        }

        /// <summary>The lamp's lens: drawn glowing.</summary>
        public static ShapeData WorkLampGlow()
        {
            var b = new Box();
            b.Cube(0, 1.72, 0.125, 0.21, 0.14, 0.012, new[] { 1.0f, 0.86f, 0.62f });
            return b.Shape();
        }

        sealed class Box
        {
            readonly List<float> _p = new List<float>(), _c = new List<float>();
            readonly List<int> _t = new List<int>();

            public ShapeData Shape() => new ShapeData { Positions = _p.ToArray(), Colors = _c.ToArray(), Triangles = _t.ToArray() };

            /// <summary>An upright box: middle, half-sizes.</summary>
            public void Cube(double x, double y, double z, double hx, double hy, double hz, float[] colour)
            {
                Quad(colour, x, y, z, hx, hy, hz, 0, 1, 2, 1);
                Quad(colour, x, y, z, hx, hy, hz, 0, 1, 2, -1);
                Quad(colour, x, y, z, hx, hy, hz, 1, 2, 0, 1);
                Quad(colour, x, y, z, hx, hy, hz, 1, 2, 0, -1);
                Quad(colour, x, y, z, hx, hy, hz, 2, 0, 1, 1);
                Quad(colour, x, y, z, hx, hy, hz, 2, 0, 1, -1);
            }

            // One face of a box: "out" is the axis it faces along (with sign), u and v the axes across it.
            void Quad(float[] colour, double x, double y, double z, double hx, double hy, double hz, int u, int v, int outAxis, int sign)
            {
                double[] c = { x, y, z }, h = { hx, hy, hz };
                int start = _p.Count / 3;
                double[,] corners = { { -1, -1 }, { 1, -1 }, { 1, 1 }, { -1, 1 } };
                for (int k = 0; k < 4; k++)
                {
                    var p = new double[3];
                    p[outAxis] = c[outAxis] + h[outAxis] * sign;
                    p[u] = c[u] + h[u] * corners[k, 0];
                    p[v] = c[v] + h[v] * corners[k, 1];
                    _p.Add((float)p[0]); _p.Add((float)p[1]); _p.Add((float)p[2]);
                    float shade = 0.92f + 0.08f * sign;
                    _c.Add(colour[0] * shade); _c.Add(colour[1] * shade); _c.Add(colour[2] * shade);
                }
                // Clockwise seen from outside, as Unity draws front faces.
                if (sign < 0) { _t.Add(start); _t.Add(start + 2); _t.Add(start + 1); _t.Add(start); _t.Add(start + 3); _t.Add(start + 2); }
                else { _t.Add(start); _t.Add(start + 1); _t.Add(start + 2); _t.Add(start); _t.Add(start + 2); _t.Add(start + 3); }
            }

            /// <summary>A thin square rod from one point to another.</summary>
            public void Rod(double ax, double ay, double az, double bx, double by, double bz, double half, float[] colour)
            {
                var a = new Core.Vec3d(ax, ay, az);
                var b = new Core.Vec3d(bx, by, bz);
                Core.Vec3d along = (b - a).Normalized;
                Core.Vec3d s1 = along.AnyPerpendicular(), s2 = Core.Vec3d.Cross(along, s1).Normalized;
                int start = _p.Count / 3;
                Core.Vec3d[] ring = { s1 + s2, s1 - s2, -s1 - s2, -s1 + s2 };
                foreach (Core.Vec3d end in new[] { a, b })
                    foreach (Core.Vec3d r in ring)
                    {
                        Core.Vec3d p = end + r * half;
                        _p.Add((float)p.X); _p.Add((float)p.Y); _p.Add((float)p.Z);
                        _c.Add(colour[0]); _c.Add(colour[1]); _c.Add(colour[2]);
                    }
                for (int k = 0; k < 4; k++)
                {
                    int k1 = (k + 1) % 4;
                    int a0 = start + k, a1 = start + k1, b0 = start + 4 + k, b1 = start + 4 + k1;
                    // Both ways round, so the rod shows from every side whichever way its corners turn.
                    _t.Add(a0); _t.Add(b0); _t.Add(a1); _t.Add(a1); _t.Add(b0); _t.Add(b1);
                    _t.Add(a0); _t.Add(a1); _t.Add(b0); _t.Add(a1); _t.Add(b1); _t.Add(b0);
                }
            }
        }
    }
}
