using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Planets
{
    /// <summary>The raw numbers of one shape: corners, a colour per corner, and which corners make each triangle.</summary>
    public sealed class ShapeData
    {
        public float[] Positions;   // x,y,z per vertex
        public float[] Colors;      // r,g,b per vertex, 0..1
        public int[] Triangles;     // three indices per triangle
        public int VertexCount => Positions.Length / 3;
    }

    /// <summary>
    /// The library of ready-made shapes that get stood on planets: rocks, hoodoos, arches, trees and so on.
    /// Every shape is built in code, so there is nothing to import, and is the same every time it is built.
    ///
    /// Every shape is 1 unit tall: the ground is y = 0, the top is y = 1, and the base carries on down to about
    /// y = -0.15 so it stays buried on a slope. (An iceberg floats, so it goes down to y = -0.6.) The game
    /// scales the whole thing by its height in metres.
    ///
    /// Each shape is made of one or more closed lumps (no holes), so the same triangles work as a collider.
    /// Every triangle has its own three corners, which gives the flat, chipped look.
    ///
    /// Rock colours are near-white shades that the game multiplies by the planet's rock colour. Plant colours
    /// are real colours and are not tinted. No Unity types, so this can be tested anywhere.
    /// </summary>
    public static class FeatureShapes
    {
        public const int Variants = 4;

        /// <summary>Rough half-width of the footprint divided by the height. Used to space shapes apart.</summary>
        public static float WidthToHeight(FeatureKind kind)
        {
            switch (kind)
            {
                case FeatureKind.Boulder: return 0.7f;
                case FeatureKind.Slab: return 1.5f;
                case FeatureKind.RockPile: return 1.0f;
                case FeatureKind.Hoodoo: return 0.3f;
                case FeatureKind.Spire: return 0.35f;
                case FeatureKind.Arch: return 1.05f;
                case FeatureKind.BalancedRock: return 0.4f;
                case FeatureKind.Tor: return 0.75f;
                case FeatureKind.BasaltColumns: return 0.75f;
                case FeatureKind.Crystal: return 0.5f;
                case FeatureKind.IceShard: return 0.4f;
                case FeatureKind.Stalagmite: return 0.3f;
                case FeatureKind.Conifer: return 0.3f;
                case FeatureKind.Broadleaf: return 0.5f;
                case FeatureKind.Palm: return 0.6f;
                case FeatureKind.Bush: return 0.7f;
                case FeatureKind.Cactus: return 0.4f;
                case FeatureKind.VentChimney: return 0.4f;
                case FeatureKind.Iceberg: return 1.1f;
                case FeatureKind.Birch: return 0.3f;
                case FeatureKind.Maple: return 0.55f;
                case FeatureKind.Blossom: return 0.65f;
                case FeatureKind.Pine: return 0.35f;
                case FeatureKind.Acacia: return 0.75f;
                case FeatureKind.GiantTree: return 0.3f;
                case FeatureKind.Snag: return 0.3f;
                case FeatureKind.SnowConifer: return 0.3f;
                case FeatureKind.Meadow: return 1.2f;
                case FeatureKind.Reeds: return 0.4f;
                case FeatureKind.Fern: return 1.2f;
                case FeatureKind.Log: return 1.9f;
                case FeatureKind.Crag: return 0.45f;
                case FeatureKind.SnowCrag: return 0.45f;
                case FeatureKind.Outcrop: return 1.1f;
                default: return 0.5f;
            }
        }

        /// <summary>Builds one shape. The same kind and variant always give exactly the same result.</summary>
        /// <summary>
        /// The shape worked out in code that stands in for a kind whose real model is made in Blender, for when
        /// that model's file is missing. Kinds that are worked out in code anyway come back unchanged.
        /// </summary>
        public static FeatureKind StandIn(FeatureKind kind)
        {
            switch (kind)
            {
                case FeatureKind.Birch: case FeatureKind.Maple: case FeatureKind.Blossom: case FeatureKind.Acacia:
                    return FeatureKind.Broadleaf;
                case FeatureKind.Pine: case FeatureKind.GiantTree: case FeatureKind.SnowConifer: case FeatureKind.Snag:
                    return FeatureKind.Conifer;
                case FeatureKind.Meadow: case FeatureKind.Reeds: case FeatureKind.Fern:
                    return FeatureKind.Bush;
                case FeatureKind.Log: return FeatureKind.Slab;
                case FeatureKind.Crag: case FeatureKind.SnowCrag: return FeatureKind.Spire;
                case FeatureKind.Outcrop: return FeatureKind.Tor;
                default: return kind;
            }
        }

        public static ShapeData Build(FeatureKind kind, int variant)
        {
            kind = StandIn(kind);
            int v = ((variant % Variants) + Variants) % Variants;
            var b = new Builder(new SeededRandom(7000 + (int)kind * 131 + v * 17));
            switch (kind)
            {
                case FeatureKind.Boulder: Boulder(b, v); break;
                case FeatureKind.Slab: Slab(b, v); break;
                case FeatureKind.RockPile: RockPile(b, v); break;
                case FeatureKind.Hoodoo: Hoodoo(b, v); break;
                case FeatureKind.Spire: Spire(b, v); break;
                case FeatureKind.Arch: Arch(b, v); break;
                case FeatureKind.BalancedRock: BalancedRock(b, v); break;
                case FeatureKind.Tor: Tor(b, v); break;
                case FeatureKind.BasaltColumns: BasaltColumns(b, v); break;
                case FeatureKind.Crystal: Crystal(b, v); break;
                case FeatureKind.IceShard: IceShard(b, v); break;
                case FeatureKind.Stalagmite: Stalagmite(b, v); break;
                case FeatureKind.Conifer: Conifer(b, v); break;
                case FeatureKind.Broadleaf: Broadleaf(b, v); break;
                case FeatureKind.Palm: Palm(b, v); break;
                case FeatureKind.Bush: Bush(b, v); break;
                case FeatureKind.Cactus: Cactus(b, v); break;
                case FeatureKind.VentChimney: VentChimney(b, v); break;
                case FeatureKind.Iceberg: Iceberg(b, v); break;
                default: Boulder(b, v); break;
            }
            return b.Finish(kind == FeatureKind.Iceberg ? -0.6 : double.NaN);
        }

        // ------------------------------------------------------------------ rocks

        static void Boulder(Builder b, int v)
        {
            double[] rx = { 0.72, 0.6, 0.85, 0.64 };
            double[] rz = { 0.6, 0.74, 0.58, 0.5 };
            var cuts = new List<Cut>();
            int chips = 3 + v % 3;
            for (int i = 0; i < chips; i++) cuts.Add(b.RandomCut(-0.1, 0.9, 0.68, 0.88));
            cuts.Add(new Cut(new V(0, -1, 0), 0.95));
            b.Blob(b.Rock(), new V(0, 0.4, 0), new V(rx[v], 0.6, rz[v]), 11, 7, 0.2, b.Rand(0, 6.28), b.Rand(-0.15, 0.15), cuts);
        }

        static void Slab(Builder b, int v)
        {
            double[] pitch = { 0.1, -0.16, 0.24, 0.0 };
            double[] rx = { 1.5, 1.35, 1.55, 1.25 };
            double[] rz = { 1.0, 0.85, 0.9, 1.1 };
            var cuts = new List<Cut>
            {
                new Cut(new V(0.08, 1, 0.05).Unit(), 0.68),
                new Cut(new V(0, -1, 0), 0.5),
            };
            for (int i = 0; i < 3; i++) cuts.Add(b.RandomCut(-0.1, 0.25, 0.7, 0.86));
            b.Blob(b.Rock(), new V(0, 0.32, 0), new V(rx[v], 1.0, rz[v]), 12, 7, 0.14, b.Rand(0, 6.28), pitch[v], cuts);
            if (v == 1 || v == 3)
            {
                // A second, smaller slab leaning on the first.
                var lean = new List<Cut>
                {
                    new Cut(new V(0, 1, 0), 0.6),
                    new Cut(new V(0, -1, 0), 0.6),
                    b.RandomCut(-0.1, 0.2, 0.7, 0.85),
                };
                double side = v == 1 ? 1 : -1;
                b.Blob(b.Rock(), new V(0.45 * side, 0.72, 0.15), new V(0.8, 0.42, 0.6), 10, 6, 0.14, b.Rand(0, 6.28), 0.3 * side, lean);
            }
        }

        static void RockPile(Builder b, int v)
        {
            int baseCount = 3 + v % 2;
            double turn = b.Rand(0, 6.28);
            for (int i = 0; i < baseCount; i++)
            {
                double a = turn + i * 6.2832 / baseCount + b.Rand(-0.25, 0.25);
                double d = b.Rand(0.36, 0.48);
                PileRock(b, new V(Math.Cos(a) * d, 0.2, Math.Sin(a) * d), b.Rand(0.42, 0.52), 0.4);
            }
            int topCount = v < 2 ? 1 : 2;
            for (int i = 0; i < topCount; i++)
            {
                double a = turn + 1.0 + i * 3.0;
                double d = topCount == 1 ? 0.05 : 0.22;
                PileRock(b, new V(Math.Cos(a) * d, 0.66 - i * 0.14, Math.Sin(a) * d), 0.34 - i * 0.05, 0.32 - i * 0.05);
            }
            for (int i = 0; i < 2; i++)
            {
                double a = turn + 0.6 + i * (2.4 + v * 0.4);
                PileRock(b, new V(Math.Cos(a) * 0.86, 0.03, Math.Sin(a) * 0.86), b.Rand(0.16, 0.22), 0.17);
            }
        }

        static void PileRock(Builder b, V centre, double radius, double radiusY)
        {
            var cuts = new List<Cut> { b.RandomCut(0.0, 0.9, 0.7, 0.88), b.RandomCut(-0.2, 0.6, 0.7, 0.88) };
            b.Blob(b.Rock(), centre, new V(radius * b.Rand(0.9, 1.15), radiusY, radius * b.Rand(0.85, 1.1)), 8, 6, 0.2,
                b.Rand(0, 6.28), b.Rand(-0.25, 0.25), cuts);
        }

        static void Hoodoo(Builder b, int v)
        {
            switch (v)
            {
                case 0:
                    HoodooColumn(b, new V(0, 0, 0), 1.0, 0.2, 0.085, 0.2, 10, 16, 0.05);
                    break;
                case 1:
                    HoodooColumn(b, new V(-0.14, 0, 0), 1.0, 0.17, 0.075, 0.17, 9, 13, 0.04);
                    HoodooColumn(b, new V(0.2, 0, 0.06), 0.66, 0.13, 0.06, 0.14, 8, 10, 0.03);
                    break;
                case 2:
                    // Fat and stubby with a big hat.
                    HoodooColumn(b, new V(0, 0, 0), 1.0, 0.27, 0.12, 0.3, 10, 14, 0.03);
                    break;
                default:
                    // Wobbly, leaning, three in a row like a family photo.
                    HoodooColumn(b, new V(0, 0, 0), 1.0, 0.15, 0.07, 0.16, 8, 12, 0.1);
                    HoodooColumn(b, new V(0.27, 0, -0.08), 0.55, 0.11, 0.055, 0.12, 7, 8, 0.04);
                    HoodooColumn(b, new V(-0.24, 0, 0.1), 0.4, 0.1, 0.05, 0.11, 7, 7, 0.03);
                    break;
            }
        }

        /// <summary>A layered column that narrows to a neck, with a darker, wider cap stone sat on top.</summary>
        static void HoodooColumn(Builder b, V foot, double height, double baseRadius, double neckRadius, double capRadius,
            int sides, int rings, double lean)
        {
            double neckY = height * 0.84;
            var centres = new V[rings];
            var radii = new double[rings];
            var ao = new double[rings];
            uint seed = b.Seed();
            double leanTurn = b.Rand(0, 6.28);
            for (int i = 0; i < rings; i++)
            {
                double t = i / (double)(rings - 1);
                double y = -0.15 + (neckY + 0.15) * t;
                if (i > 0 && i < rings - 1) y += b.Rand(-0.25, 0.25) * (neckY + 0.15) / (rings - 1);
                double sway = lean * Math.Sin(t * 3.4 + leanTurn) * t;
                centres[i] = new V(foot.X + sway, y, foot.Z + sway * 0.6);
                bool pinch = i % 2 == 1;
                double r = Noise3.Lerp(baseRadius, neckRadius, Math.Pow(t, 0.75));
                radii[i] = r * (pinch ? 0.84 : 1.12);
                ao[i] = pinch ? 0.45 : 1.0;
            }
            V top = centres[rings - 1];
            var body = b.Rock(true);
            b.Lathe(body, centres, radii, null, sides, 0, 0.1, seed, centres[0], new V(top.X, top.Y + 0.02, top.Z), ao);

            var cuts = new List<Cut> { new Cut(new V(0.1, 1, 0.06).Unit(), 0.8), new Cut(new V(0, -1, 0), 0.75), b.RandomCut(-0.1, 0.2, 0.75, 0.9) };
            var cap = b.Rock(false, -0.07);
            b.Blob(cap, new V(top.X, height * 0.915, top.Z), new V(capRadius, height * 0.105, capRadius * 0.92), 10, 5, 0.14,
                b.Rand(0, 6.28), b.Rand(-0.1, 0.1), cuts);
        }

        static void Spire(Builder b, int v)
        {
            switch (v)
            {
                case 0:
                    Pinnacle(b, new V(0, 0, 0), 1.0, 0.24, 1.0, 8, 13, 0.1);
                    break;
                case 1:
                    Pinnacle(b, new V(-0.08, 0, 0), 1.0, 0.2, 0.9, 7, 12, 0.08);
                    Pinnacle(b, new V(0.2, 0, 0.06), 0.6, 0.14, 1.0, 6, 9, -0.06);
                    break;
                case 2:
                    // A thin fin, like a shark's.
                    Pinnacle(b, new V(0, 0, 0), 1.0, 0.3, 0.42, 8, 13, 0.16);
                    break;
                default:
                    Pinnacle(b, new V(0, 0, 0), 1.0, 0.18, 1.0, 7, 12, 0.05);
                    Pinnacle(b, new V(0.22, 0, -0.1), 0.72, 0.13, 0.8, 6, 9, 0.08);
                    Pinnacle(b, new V(-0.2, 0, 0.12), 0.45, 0.12, 1.0, 6, 8, -0.05);
                    break;
            }
        }

        /// <summary>A tall tapering needle of rock with ledges, ending in a point.</summary>
        static void Pinnacle(Builder b, V foot, double height, double baseRadius, double flatten, int sides, int rings, double lean)
        {
            var centres = new V[rings];
            var ru = new double[rings];
            var rw = new double[rings];
            var ao = new double[rings];
            double top = height * 0.9;
            for (int i = 0; i < rings; i++)
            {
                double t = i / (double)(rings - 1);
                double y = -0.15 + (top + 0.15) * t;
                bool ledge = i % 3 == 1;
                double r = Math.Max(0.035, baseRadius * Math.Pow(1.0 - t * 0.86, 0.8)) * (ledge ? 1.16 : 0.92);
                centres[i] = new V(foot.X + lean * t * t, y, foot.Z + lean * 0.5 * t * t);
                ru[i] = r;
                rw[i] = r * flatten;
                ao[i] = ledge ? 1.0 : 0.6;
            }
            V last = centres[rings - 1];
            var tip = new V(last.X + lean * 0.3, height, last.Z);
            b.Lathe(b.Rock(true), centres, ru, rw, sides, b.Rand(0, 6.28), 0.12, b.Seed(), centres[0], tip, ao);
        }

        static void Arch(Builder b, int v)
        {
            double[] skew = { 0.0, 0.12, -0.1, 0.05 };
            double[] topRadius = { 0.17, 0.14, 0.2, 0.16 };
            double[] leftLeg = { 0.2, 0.22, 0.19, 0.18 };
            double[] rightLeg = { 0.2, 0.18, 0.21, 0.22 };
            double[] depth = { 0.24, 0.2, 0.28, 0.22 };
            const double half = 0.78;                 // centre line of each leg; the opening is about 1.0 wide
            double rise = 1.0 - topRadius[v];

            const int steps = 21;
            var path = new V[steps + 2];
            var ru = new double[steps + 2];
            var rw = new double[steps + 2];
            path[0] = new V(-half, -0.15, 0);
            ru[0] = leftLeg[v] * 1.1;
            rw[0] = depth[v] * 1.1;
            for (int k = 0; k < steps; k++)
            {
                double u = -1.0 + 2.0 * k / (steps - 1);
                double th = Math.PI * 0.5 - Math.PI * 0.5 * Math.Sin(u * Math.PI * 0.5);
                double c = Math.Cos(th), s = Math.Max(0, Math.Sin(th));
                double x = half * Math.Sign(c) * Math.Pow(Math.Abs(c), 0.7);
                double y = rise * Math.Pow(s, 0.7) * (1.0 + skew[v] * x);
                path[k + 1] = new V(x, y, 0);
                double leg = u < 0 ? leftLeg[v] : rightLeg[v];
                double along = Math.Pow(Math.Abs(u), 1.5);
                // Alternate thick and thin, so the arch is built of beds of rock and not bent pipe.
                ru[k + 1] = Noise3.Lerp(topRadius[v], leg, along) * (k % 2 == 0 ? 1.07 : 0.93);
                rw[k + 1] = Noise3.Lerp(depth[v] * 0.8, depth[v], along);
            }
            path[steps + 1] = new V(half, -0.15, 0);
            ru[steps + 1] = rightLeg[v] * 1.1;
            rw[steps + 1] = depth[v] * 1.1;
            b.Sweep(b.Rock(true), path, ru, rw, 8, new V(0, 0, 1), 0.11, b.Seed(), 0, 0, null, 0, 0.5);

            if (v == 1 || v == 3)
            {
                // A fallen block resting against the outside of one leg.
                double side = v == 1 ? -1 : 1;
                var cuts = new List<Cut> { b.RandomCut(0.1, 0.9, 0.7, 0.85), b.RandomCut(-0.1, 0.5, 0.7, 0.85) };
                b.Blob(b.Rock(), new V(side * (half + 0.3), 0.06, 0.12), new V(0.2, 0.2, 0.18), 8, 6, 0.2, b.Rand(0, 6.28), 0.2, cuts);
            }
        }

        static void BalancedRock(Builder b, int v)
        {
            double[] neckY = { 0.5, 0.42, 0.6, 0.36 };
            double[] baseRadius = { 0.26, 0.3, 0.2, 0.32 };
            double[] rockX = { 0.34, 0.4, 0.27, 0.36 };
            double[] rockY = { 0.26, 0.3, 0.21, 0.33 };
            double[] offset = { 0.05, -0.09, 0.07, 0.12 };

            const int rings = 9;
            var centres = new V[rings];
            var radii = new double[rings];
            var ao = new double[rings];
            for (int i = 0; i < rings; i++)
            {
                double t = i / (double)(rings - 1);
                centres[i] = new V(offset[v] * 0.5 * t, -0.15 + (neckY[v] + 0.15) * t, 0);
                radii[i] = Noise3.Lerp(baseRadius[v], 0.075, Math.Pow(t, 0.6)) * (i % 2 == 1 ? 0.88 : 1.1);
                ao[i] = i % 2 == 1 ? 0.5 : 1.0;
            }
            V neck = centres[rings - 1];
            b.Lathe(b.Rock(true), centres, radii, null, 8, b.Rand(0, 6.28), 0.1, b.Seed(), centres[0], new V(neck.X, neck.Y + 0.03, neck.Z), ao);

            double centreY = neckY[v] + rockY[v] * 0.88;
            var cuts = new List<Cut> { b.RandomCut(0.2, 0.9, 0.72, 0.88), b.RandomCut(-0.3, 0.3, 0.72, 0.88), b.RandomCut(-0.3, 0.3, 0.75, 0.9) };
            b.Blob(b.Rock(true), new V(offset[v], centreY, 0.02), new V(rockX[v], rockY[v], rockX[v] * 0.85), 11, 7, 0.17,
                b.Rand(0, 6.28), b.Rand(-0.22, 0.22), cuts);
        }

        static void Tor(Builder b, int v)
        {
            int[] mainCount = { 4, 5, 3, 4 };
            int[] sideCount = { 2, 2, 3, 3 };
            double[] sideX = { 0.5, -0.46, 0.44, -0.52 };
            TorStack(b, new V(sideX[v] * -0.22, 0, 0), mainCount[v], 1.0, 0.46);
            TorStack(b, new V(sideX[v], 0, b.Rand(-0.15, 0.15)), sideCount[v], v >= 2 ? 0.62 : 0.45, 0.36);
        }

        /// <summary>A pile of flattened, rounded slabs like a stack of pancakes: a granite tor.</summary>
        static void TorStack(Builder b, V foot, int count, double height, double radius)
        {
            double thick = (height + 0.12) / count;
            for (int i = 0; i < count; i++)
            {
                double t = count == 1 ? 0 : i / (double)(count - 1);
                double y = -0.12 + thick * (i + 0.5);
                double r = radius * Noise3.Lerp(1.0, 0.68, t) * b.Rand(0.9, 1.1);
                var cuts = new List<Cut> { new Cut(new V(0, 1, 0), 0.74), new Cut(new V(0, -1, 0), 0.74), b.RandomCut(-0.1, 0.1, 0.78, 0.9) };
                var centre = new V(foot.X + b.Rand(-0.07, 0.07), y, foot.Z + b.Rand(-0.07, 0.07));
                b.Blob(b.Rock(false, i % 2 == 0 ? 0 : -0.03), centre, new V(r, thick * 0.78, r * b.Rand(0.78, 0.95)), 9, 5, 0.16,
                    b.Rand(0, 6.28), b.Rand(-0.07, 0.07), cuts);
            }
        }

        static void BasaltColumns(Builder b, int v)
        {
            // Six-sided posts on a honeycomb grid, tallest near one spot and stepping down away from it.
            const double cell = 0.17;
            double[] peakX = { 0.0, 0.28, -0.2, 0.1 };
            double[] peakZ = { 0.0, -0.1, 0.2, 0.3 };
            double[] falloff = { 1.1, 0.9, 1.3, 0.75 };
            var rock = b.Rock();
            int made = 0;
            for (int q = -3; q <= 3 && made < 28; q++)
            {
                for (int r = -3; r <= 3 && made < 28; r++)
                {
                    double x = cell * 1.5 * q;
                    double z = cell * 1.7320508 * (r + q * 0.5);
                    double fromMiddle = Math.Sqrt(x * x + z * z);
                    if (fromMiddle > 0.62) continue;
                    double dx = x - peakX[v], dz = z - peakZ[v];
                    double h = 1.0 - falloff[v] * Math.Sqrt(dx * dx + dz * dz) + b.Rand(-0.14, 0.14);
                    h = Math.Round(h / 0.07) * 0.07;
                    if (h > 1.0) h = 1.0;
                    if (h < 0.07)
                    {
                        if (fromMiddle > 0.4) continue;
                        h = 0.07;
                    }
                    double radius = cell * b.Rand(0.9, 0.96);
                    var bottom = new V(x + b.Rand(-0.008, 0.008), -0.15, z + b.Rand(-0.008, 0.008));
                    var top = new V(bottom.X, h - 0.02, bottom.Z);
                    var centres = new[] { bottom, top };
                    var radii = new[] { radius, radius * 0.97 };
                    var ao = new[] { 0.35, 0.9 };
                    b.Lathe(rock, centres, radii, null, 6, b.Rand(-0.06, 0.06), 0, 0, bottom, new V(top.X, h, top.Z), ao);
                    made++;
                }
            }
        }

        static void Crystal(Builder b, int v)
        {
            int[] count = { 5, 7, 4, 6 };
            double[] spread = { 0.5, 0.7, 0.35, 0.8 };
            double[] thick = { 0.1, 0.075, 0.14, 0.09 };
            var mat = b.Rock(false, 0.04, 0.96, 0.98, 1.0);
            double turn = b.Rand(0, 6.28);
            for (int i = 0; i < count[v]; i++)
            {
                bool main = i == 0;
                double a = turn + i * 6.2832 / (count[v] - 1) + b.Rand(-0.3, 0.3);
                double tilt = main ? b.Rand(0.03, 0.12) : spread[v] * b.Rand(0.6, 1.0);
                double length = main ? 1.0 : b.Rand(0.35, 0.62);
                double r = thick[v] * (main ? 1.25 : b.Rand(0.7, 1.0));
                var dir = new V(Math.Cos(a) * Math.Sin(tilt), Math.Cos(tilt), Math.Sin(a) * Math.Sin(tilt));
                var foot = new V(Math.Cos(a) * (main ? 0.0 : 0.08), -0.15, Math.Sin(a) * (main ? 0.0 : 0.08));
                double shaft = (length + 0.15) / Math.Max(0.3, dir.Y) - r * 1.8;
                var path = new[] { foot, foot + dir * (shaft * 0.5), foot + dir * shaft };
                var ru = new[] { r, r * 0.97, r * 0.92 };
                var ao = new[] { 0.3, 0.8, 1.0 };
                V reference = Math.Abs(dir.Z) > 0.8 ? new V(1, 0, 0) : new V(0, 0, 1);
                b.Sweep(mat, path, ru, ru, 6, reference, 0, 0, 0, r * 1.8, ao, 0, b.Rand(0, 1));
            }
        }

        static void IceShard(Builder b, int v)
        {
            int[] count = { 3, 5, 4, 4 };
            double[] spread = { 0.3, 0.5, 0.2, 0.62 };
            var mat = b.Rock(false, 0.05, 0.95, 0.98, 1.0);
            double turn = b.Rand(0, 6.28);
            for (int i = 0; i < count[v]; i++)
            {
                bool main = i == 0;
                double a = turn + i * 6.2832 / Math.Max(1, count[v] - 1) + b.Rand(-0.3, 0.3);
                double tilt = main ? b.Rand(0.05, 0.16) : spread[v] * b.Rand(0.55, 1.0);
                double length = main ? 1.0 : b.Rand(0.35, 0.72);
                double r = main ? 0.17 : b.Rand(0.09, 0.13);
                var dir = new V(Math.Cos(a) * Math.Sin(tilt), Math.Cos(tilt), Math.Sin(a) * Math.Sin(tilt));
                var foot = new V(Math.Cos(a) * (main ? 0.0 : 0.11), -0.15, Math.Sin(a) * (main ? 0.0 : 0.11));
                double full = (length + 0.15) / Math.Max(0.3, dir.Y);
                // A slight kink half way up, so it reads as a snapped blade and not a party hat.
                V kink = new V(dir.Z, 0, -dir.X) * (r * 0.5);
                var path = new[] { foot, foot + dir * (full * 0.3) + kink, foot + dir * (full * 0.62) };
                var ru = new[] { r, r * 0.86, r * 0.5 };
                var rw = new[] { r * 0.5, r * 0.4, r * 0.22 };
                var ao = new[] { 0.3, 0.8, 1.0 };
                V reference = Math.Abs(dir.Z) > 0.8 ? new V(1, 0, 0) : new V(0, 0, 1);
                b.Sweep(mat, path, ru, rw, 5, reference, 0, 0, 0, full * 0.38, ao, 0, b.Rand(0, 1));
            }
        }

        static void Stalagmite(Builder b, int v)
        {
            switch (v)
            {
                case 0:
                    Drip(b, new V(0, 0, 0), 1.0, 0.24, 9, 12);
                    Drip(b, new V(0.26, 0, 0.08), 0.38, 0.12, 6, 6);
                    break;
                case 1:
                    Drip(b, new V(0, 0, 0), 1.0, 0.17, 8, 11);
                    Drip(b, new V(-0.2, 0, 0.1), 0.55, 0.11, 6, 7);
                    break;
                case 2:
                    Drip(b, new V(0, 0, 0), 1.0, 0.33, 9, 13);
                    break;
                default:
                    Drip(b, new V(-0.05, 0, 0), 1.0, 0.2, 8, 10);
                    Drip(b, new V(0.22, 0, -0.1), 0.6, 0.12, 6, 6);
                    Drip(b, new V(-0.24, 0, 0.14), 0.3, 0.1, 6, 5);
                    break;
            }
        }

        /// <summary>A cone built from drips: bulging tiers like melted candle wax, with a rounded tip.</summary>
        static void Drip(Builder b, V foot, double height, double baseRadius, int sides, int rings)
        {
            var centres = new V[rings];
            var radii = new double[rings];
            var ao = new double[rings];
            double top = height - 0.035;
            for (int i = 0; i < rings; i++)
            {
                double t = i / (double)(rings - 1);
                bool bulge = i % 2 == 0;
                centres[i] = new V(foot.X, -0.15 + (top + 0.15) * t, foot.Z);
                radii[i] = Math.Max(0.03, baseRadius * Math.Pow(1.0 - t * 0.9, 1.15)) * (bulge ? 1.14 : 0.88);
                ao[i] = bulge ? 1.0 : 0.5;
            }
            b.Lathe(b.Rock(), centres, radii, null, sides, b.Rand(0, 6.28), 0.12, b.Seed(), centres[0], new V(foot.X, height, foot.Z), ao);
        }

        static void VentChimney(Builder b, int v)
        {
            switch (v)
            {
                case 0:
                    Chimney(b, new V(0, 0, 0), 1.0, 0.3, 0.12, 10, 12);
                    Chimney(b, new V(0.3, 0, 0.1), 0.5, 0.16, 0.08, 7, 8);
                    break;
                case 1:
                    Chimney(b, new V(-0.12, 0, 0), 1.0, 0.22, 0.1, 9, 12);
                    Chimney(b, new V(0.2, 0, -0.08), 0.72, 0.18, 0.09, 8, 9);
                    Chimney(b, new V(0.02, 0, 0.26), 0.4, 0.13, 0.07, 7, 6);
                    break;
                case 2:
                    // One fat stack, like a termite mound with a hole in the top.
                    Chimney(b, new V(0, 0, 0), 1.0, 0.4, 0.17, 10, 13);
                    break;
                default:
                    Chimney(b, new V(0.1, 0, 0), 1.0, 0.2, 0.085, 9, 12);
                    Chimney(b, new V(-0.22, 0, 0.1), 0.62, 0.17, 0.08, 8, 8);
                    break;
            }
        }

        /// <summary>A knobbly pipe of rock with an open bore in the top, as on a sea-floor vent or a fumarole.</summary>
        static void Chimney(Builder b, V foot, double height, double baseRadius, double topRadius, int sides, int rings)
        {
            int total = rings + 2;
            var centres = new V[total];
            var radii = new double[total];
            var ao = new double[total];
            double bend = b.Rand(-0.07, 0.07);
            for (int i = 0; i < rings; i++)
            {
                double t = i / (double)(rings - 1);
                bool knob = i % 3 == 1;
                centres[i] = new V(foot.X + bend * Math.Sin(t * 3.0), -0.15 + (height + 0.15) * t, foot.Z + bend * 0.5 * t);
                double r = Noise3.Lerp(baseRadius, topRadius, Math.Pow(t, 0.55));
                radii[i] = i == rings - 1 ? topRadius * 1.12 : r * (knob ? 1.15 : 0.92);
                ao[i] = knob || i == rings - 1 ? 1.0 : 0.55;
            }
            V lip = centres[rings - 1];
            double bore = Math.Min(0.22, height * 0.25);
            centres[rings] = new V(lip.X, lip.Y - 0.025, lip.Z);
            radii[rings] = topRadius * 0.68;
            ao[rings] = 0.25;
            centres[rings + 1] = new V(lip.X, lip.Y - bore, lip.Z);
            radii[rings + 1] = topRadius * 0.55;
            ao[rings + 1] = 0.0;
            b.Lathe(b.Rock(), centres, radii, null, sides, b.Rand(0, 6.28), 0.14, b.Seed(), centres[0],
                new V(lip.X, lip.Y - bore - 0.01, lip.Z), ao);
        }

        static void Iceberg(Builder b, int v)
        {
            var mat = b.Rock(false, 0.05, 0.95, 0.985, 1.0);
            var cuts = new List<Cut>();
            for (int i = 0; i < 4; i++) cuts.Add(b.RandomCut(-0.5, 0.5, 0.7, 0.9));
            switch (v)
            {
                case 0:
                    // Flat-topped, like a slice of cake.
                    cuts.Add(new Cut(new V(0.04, 1, 0.02).Unit(), 0.6));
                    b.Blob(mat, new V(0, 0.1, 0), new V(1.25, 1.1, 0.95), 14, 9, 0.16, b.Rand(0, 6.28), 0, cuts);
                    break;
                case 1:
                    b.Blob(mat, new V(0, -0.05, 0), new V(1.0, 0.6, 0.8), 13, 8, 0.18, b.Rand(0, 6.28), 0, cuts);
                    IcePeak(b, mat, new V(0.25, 0.5, 0.05), 0.55, 0.85);
                    break;
                case 2:
                    b.Blob(mat, new V(0, -0.08, 0), new V(1.1, 0.58, 0.75), 13, 8, 0.18, b.Rand(0, 6.28), 0, cuts);
                    IcePeak(b, mat, new V(-0.42, 0.5, 0.05), 0.46, 0.85);
                    IcePeak(b, mat, new V(0.45, 0.3, -0.08), 0.4, 0.6);
                    break;
                default:
                    // A wedge: one long ramp up to a cliff.
                    cuts.Add(new Cut(new V(0.55, 1, 0.1).Unit(), 0.5));
                    b.Blob(mat, new V(0, 0.1, 0), new V(1.15, 1.0, 0.85), 14, 9, 0.16, 0, 0, cuts);
                    break;
            }
        }

        static void IcePeak(Builder b, Lump mat, V centre, double radius, double radiusY)
        {
            var cuts = new List<Cut> { b.RandomCut(-0.2, 0.5, 0.6, 0.8), b.RandomCut(-0.2, 0.5, 0.6, 0.8), b.RandomCut(0.3, 0.8, 0.7, 0.9) };
            b.Blob(mat, centre, new V(radius, radiusY, radius * 0.8), 10, 7, 0.2, b.Rand(0, 6.28), b.Rand(-0.2, 0.2), cuts);
        }

        // ------------------------------------------------------------------ plants

        static Lump Bark(Builder b) => b.Plant(0.45, 0.31, 0.19);

        static void Conifer(Builder b, int v)
        {
            int[] tiers = { 4, 5, 3, 4 };
            double[] width = { 0.3, 0.22, 0.36, 0.27 };
            double[] trunkTop = { 0.22, 0.3, 0.16, 0.26 };
            double[] bend = { 0.0, 0.0, 0.03, 0.09 };

            var trunkPath = new[] { new V(0, -0.15, 0), new V(0, 0.1, 0), new V(0, trunkTop[v] + 0.2, 0) };
            var trunkRadius = new[] { 0.075, 0.055, 0.04 };
            b.Lathe(Bark(b), trunkPath, trunkRadius, null, 6, 0, 0.06, b.Seed(), trunkPath[0], trunkPath[2], null);

            int n = tiers[v];
            double step = (1.0 - trunkTop[v]) / (n + 0.9);
            for (int i = 0; i < n; i++)
            {
                double t = n == 1 ? 0 : i / (double)(n - 1);
                double y = trunkTop[v] + step * i;
                double h = i == n - 1 ? 1.0 - y : step * 1.9;
                double r = width[v] * Noise3.Lerp(1.0, 0.5, t);
                double x = bend[v] * Math.Sin(i * 1.7), z = bend[v] * Math.Cos(i * 2.3);
                var green = b.Plant(Noise3.Lerp(0.13, 0.22, t), Noise3.Lerp(0.36, 0.5, t), Noise3.Lerp(0.24, 0.26, t));
                var centres = new[] { new V(x, y, z), new V(x, y + h * 0.42, z) };
                var radii = new[] { r, r * 0.5 };
                var ao = new[] { 0.7, 1.0 };
                // The underside dips up into the cone, the top comes to a point.
                b.Lathe(green, centres, radii, null, 10, i * 0.9, 0.08, b.Seed(), new V(x, y + h * 0.14, z),
                    new V(x + bend[v] * 0.5, y + h, z), ao, 0.2);
            }
        }

        static void Broadleaf(Builder b, int v)
        {
            double[] crownY = { 0.66, 0.6, 0.55, 0.64 };
            double[] lean = { 0.04, -0.08, 0.0, 0.12 };
            var path = new V[5];
            var radii = new double[5];
            double[] flare = { 0.115, 0.075, 0.062, 0.055, 0.05 };
            for (int i = 0; i < 5; i++)
            {
                double t = i / 4.0;
                path[i] = new V(lean[v] * t * t, -0.15 + (crownY[v] + 0.15) * t, lean[v] * 0.4 * Math.Sin(t * 3.0));
                radii[i] = flare[i] * (v == 2 ? 0.85 : 1.0);
            }
            b.Sweep(Bark(b), path, radii, radii, 7, new V(0, 0, 1), 0.08, b.Seed(), 0, 0.02);

            V crown = path[4];
            switch (v)
            {
                case 0:
                    // A lollipop: one big ball and two helpers.
                    Leaves(b, crown + new V(0, 0.06, 0), 0.34, 0.28, 0);
                    Leaves(b, crown + new V(0.24, -0.06, 0.1), 0.2, 0.17, 1);
                    Leaves(b, crown + new V(-0.2, -0.02, -0.14), 0.21, 0.18, 2);
                    break;
                case 1:
                    // A wide oak.
                    Leaves(b, crown + new V(0, 0.14, 0), 0.3, 0.24, 0);
                    Leaves(b, crown + new V(0.3, 0.0, 0.06), 0.25, 0.2, 1);
                    Leaves(b, crown + new V(-0.3, 0.02, -0.04), 0.26, 0.2, 2);
                    Leaves(b, crown + new V(0.02, 0.0, 0.28), 0.22, 0.18, 1);
                    Leaves(b, crown + new V(-0.04, 0.02, -0.28), 0.22, 0.18, 2);
                    break;
                case 2:
                    // Tall and narrow: three balls stacked like a snowman.
                    Leaves(b, crown + new V(0, -0.14, 0), 0.24, 0.2, 2);
                    Leaves(b, crown + new V(0.03, 0.08, 0.02), 0.21, 0.2, 0);
                    Leaves(b, crown + new V(-0.02, 0.28, 0), 0.16, 0.17, 1);
                    break;
                default:
                    // Windswept: everything pushed off to one side.
                    Leaves(b, crown + new V(0.12, 0.08, 0), 0.3, 0.24, 0);
                    Leaves(b, crown + new V(0.4, -0.02, 0.06), 0.22, 0.17, 1);
                    Leaves(b, crown + new V(-0.14, 0.0, -0.1), 0.18, 0.15, 2);
                    Leaves(b, crown + new V(0.3, 0.16, -0.12), 0.18, 0.15, 1);
                    break;
            }
        }

        static void Leaves(Builder b, V centre, double radius, double radiusY, int shade)
        {
            Lump green = shade == 0 ? b.Plant(0.36, 0.6, 0.24) : shade == 1 ? b.Plant(0.45, 0.66, 0.27) : b.Plant(0.27, 0.5, 0.22);
            var cuts = new List<Cut> { b.RandomCut(-0.9, -0.5, 0.6, 0.75) };
            b.Blob(green, centre, new V(radius, radiusY, radius * b.Rand(0.88, 1.05)), 10, 6, 0.22, b.Rand(0, 6.28), b.Rand(-0.15, 0.15), cuts);
        }

        static void Palm(Builder b, int v)
        {
            double[] lean = { 0.22, -0.3, 0.08, 0.4 };
            double[] crownY = { 0.8, 0.78, 0.84, 0.72 };
            int[] fronds = { 7, 6, 8, 7 };
            double[] reach = { 0.42, 0.46, 0.36, 0.44 };

            const int rings = 8;
            var path = new V[rings];
            var radii = new double[rings];
            var ao = new double[rings];
            for (int i = 0; i < rings; i++)
            {
                double t = i / (double)(rings - 1);
                path[i] = new V(lean[v] * t * t, -0.15 + (crownY[v] + 0.15) * t, 0.03 * Math.Sin(t * 5.0));
                radii[i] = Noise3.Lerp(0.075, 0.045, Math.Sqrt(t)) * (i % 2 == 0 ? 1.1 : 0.9);
                ao[i] = i % 2 == 0 ? 1.0 : 0.6;
            }
            b.Sweep(b.Plant(0.55, 0.42, 0.27), path, radii, radii, 6, new V(0, 0, 1), 0.04, b.Seed(), 0, 0.03, ao);

            V crown = path[rings - 1];
            double turn = b.Rand(0, 6.28);
            for (int i = 0; i < fronds[v]; i++)
            {
                double a = turn + i * 6.2832 / fronds[v] + b.Rand(-0.2, 0.2);
                var outward = new V(Math.Cos(a), 0, Math.Sin(a));
                double length = reach[v] * b.Rand(0.85, 1.1);
                double lift = i % 2 == 0 ? 0.2 : 0.11;
                const int points = 5;
                var frond = new V[points];
                var wide = new double[points];
                var thin = new double[points];
                double[] profile = { 0.45, 1.0, 0.95, 0.7, 0.4 };
                for (int k = 0; k < points; k++)
                {
                    double s = k / (double)(points - 1);
                    double y = lift * Math.Sin(s * 2.2) - 0.36 * s * s * s;
                    frond[k] = crown + outward * (length * (0.04 + s * 0.9)) + new V(0, y - 0.02, 0);
                    wide[k] = 0.075 * profile[k];
                    thin[k] = 0.016;
                }
                var green = i % 2 == 0 ? b.Plant(0.3, 0.6, 0.25) : b.Plant(0.38, 0.66, 0.26);
                b.Sweep(green, frond, wide, thin, 4, new V(0, 1, 0), 0, 0, 0.01, length * 0.14);
            }

            int nuts = 2 + v % 2;
            for (int i = 0; i < nuts; i++)
            {
                double a = turn + 0.5 + i * 2.2;
                b.Blob(b.Plant(0.36, 0.24, 0.14), crown + new V(Math.Cos(a) * 0.06, -0.06, Math.Sin(a) * 0.06), new V(0.045, 0.05, 0.045),
                    6, 4, 0.08, b.Rand(0, 6.28), 0, null);
            }
        }

        static void Bush(Builder b, int v)
        {
            int[] count = { 3, 4, 3, 4 };
            double[] spread = { 0.36, 0.42, 0.3, 0.46 };
            double turn = b.Rand(0, 6.28);
            for (int i = 0; i < count[v]; i++)
            {
                bool main = i == 0;
                double a = turn + i * 6.2832 / (count[v] - 1) + b.Rand(-0.3, 0.3);
                double d = main ? 0.05 : spread[v];
                double radius = main ? (v == 2 ? 0.46 : 0.4) : b.Rand(0.26, 0.34);
                double radiusY = main ? 0.58 : radius * 0.95;
                var centre = new V(Math.Cos(a) * d, main ? 0.42 : radiusY * 0.62, Math.Sin(a) * d);
                Lump green = i % 3 == 0 ? b.Plant(0.3, 0.52, 0.22) : i % 3 == 1 ? b.Plant(0.39, 0.6, 0.25) : b.Plant(0.25, 0.45, 0.24);
                b.Blob(green, centre, new V(radius, radiusY, radius * b.Rand(0.9, 1.1)), 8, 5, 0.24, b.Rand(0, 6.28), b.Rand(-0.2, 0.2), null);
            }
        }

        static void Cactus(Builder b, int v)
        {
            var green = b.Plant(0.34, 0.56, 0.31);
            switch (v)
            {
                case 0:
                    CactusTrunk(b, green, 0.12);
                    CactusArm(b, green, 0.0, 0.38, 0.3, 0.085);
                    CactusArm(b, green, 3.3, 0.5, 0.22, 0.075);
                    break;
                case 1:
                    CactusTrunk(b, green, 0.135);
                    CactusArm(b, green, 0.4, 0.3, 0.42, 0.09);
                    break;
                case 2:
                {
                    // A barrel cactus: a fat ribbed ball with a flower for a hat.
                    const int rings = 7;
                    var centres = new V[rings];
                    var radii = new double[rings];
                    double[] profile = { 0.62, 0.85, 1.0, 1.0, 0.88, 0.66, 0.36 };
                    for (int i = 0; i < rings; i++)
                    {
                        double t = i / (double)(rings - 1);
                        centres[i] = new V(0, -0.15 + 1.03 * t, 0);
                        radii[i] = 0.46 * profile[i];
                    }
                    b.Lathe(green, centres, radii, null, 12, 0, 0.03, b.Seed(), centres[0], new V(0, 0.92, 0), null, 0.16);
                    b.Blob(b.Plant(0.93, 0.42, 0.56), new V(0.02, 0.94, 0), new V(0.12, 0.07, 0.12), 7, 4, 0.2, 0, 0.1, null);
                    break;
                }
                default:
                {
                    // Prickly pear: flat paddles stuck on each other.
                    Paddle(b, green, new V(0, 0.2, 0), 0.3, 0.38, 0.3, 0.05);
                    Paddle(b, green, new V(-0.2, 0.58, 0.02), 0.2, 0.27, 0.9, -0.3);
                    Paddle(b, green, new V(0.2, 0.68, -0.02), 0.22, 0.32, -0.2, 0.25);
                    Paddle(b, b.Plant(0.4, 0.62, 0.32), new V(0.42, 0.36, 0.05), 0.13, 0.17, 0.5, 0.5);
                    break;
                }
            }
        }

        static void CactusTrunk(Builder b, Lump green, double radius)
        {
            const int rings = 7;
            var centres = new V[rings];
            var radii = new double[rings];
            double[] profile = { 0.92, 1.0, 1.0, 1.0, 0.97, 0.86, 0.58 };
            for (int i = 0; i < rings; i++)
            {
                double t = i / (double)(rings - 1);
                centres[i] = new V(0.015 * Math.Sin(t * 4.0), -0.15 + 1.1 * t, 0);
                radii[i] = radius * profile[i];
            }
            b.Lathe(green, centres, radii, null, 10, 0, 0.03, b.Seed(), centres[0], new V(centres[rings - 1].X, 1.0, 0), null, 0.14);
        }

        static void CactusArm(Builder b, Lump green, double angle, double startY, double rise, double radius)
        {
            var d = new V(Math.Cos(angle), 0, Math.Sin(angle));
            var up = new V(0, 1, 0);
            var start = new V(0, startY, 0);
            var path = new[]
            {
                start + d * 0.04,
                start + d * 0.17 + up * 0.0,
                start + d * 0.25 + up * 0.06,
                start + d * 0.27 + up * 0.16,
                start + d * 0.27 + up * (0.16 + rise * 0.5),
                start + d * 0.27 + up * (0.16 + rise),
            };
            var radii = new[] { radius, radius, radius * 1.05, radius * 1.05, radius, radius * 0.7 };
            b.Sweep(green, path, radii, radii, 6, new V(d.Z, 0, -d.X), 0.03, b.Seed(), 0, radius * 0.55);
        }

        static void Paddle(Builder b, Lump green, V centre, double radius, double radiusY, double yaw, double roll)
        {
            // Roll is faked by sliding the top sideways: a paddle that looks stuck on at an angle.
            b.Blob(green, centre, new V(radius, radiusY, 0.07), 9, 5, 0.1, yaw, roll * 0.3, null);
        }

        // ------------------------------------------------------------------ the builder

        struct V
        {
            public double X, Y, Z;
            public V(double x, double y, double z) { X = x; Y = y; Z = z; }
            public static V operator +(V a, V b) => new V(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
            public static V operator -(V a, V b) => new V(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
            public static V operator *(V a, double s) => new V(a.X * s, a.Y * s, a.Z * s);
            public static double Dot(V a, V b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
            public static V Cross(V a, V b) => new V(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
            public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
            public V Unit()
            {
                double l = Length;
                return l < 1e-12 ? new V(0, 1, 0) : new V(X / l, Y / l, Z / l);
            }
        }

        /// <summary>A flat chip taken off a lump: everything further out than the plane is pulled back to it.</summary>
        struct Cut
        {
            public V Normal;
            public double Distance;
            public Cut(V normal, double distance) { Normal = normal; Distance = distance; }
        }

        /// <summary>One closed piece of a shape, with the colour it should be given.</summary>
        sealed class Lump
        {
            public readonly List<V> Points = new List<V>();
            public readonly List<double> Shadow = new List<double>();   // 1 = open, 0 = deep in a crease
            public readonly List<int> Triangles = new List<int>();
            public bool IsRock = true;
            public bool Layers;
            public double Tone;
            public double R = 1, G = 1, B = 1;
        }

        sealed class Builder
        {
            readonly SeededRandom _rng;
            readonly List<Lump> _lumps = new List<Lump>();
            readonly uint _layerSeed;

            public Builder(SeededRandom rng)
            {
                _rng = rng;
                _layerSeed = Seed();
            }

            public double Rand(double min, double max) => min + (max - min) * _rng.NextDouble();
            public uint Seed() => (uint)(_rng.NextUInt64() >> 32);

            public Lump Rock(bool layers = false, double tone = 0, double r = 1, double g = 1, double bl = 1)
            {
                var lump = new Lump { IsRock = true, Layers = layers, Tone = tone, R = r, G = g, B = bl };
                _lumps.Add(lump);
                return lump;
            }

            public Lump Plant(double r, double g, double bl)
            {
                var lump = new Lump { IsRock = false, R = r, G = g, B = bl };
                _lumps.Add(lump);
                return lump;
            }

            /// <summary>A chip in a random direction. <paramref name="minY"/>..<paramref name="maxY"/> is how far up or down it may face.</summary>
            public Cut RandomCut(double minY, double maxY, double minDistance, double maxDistance)
            {
                double a = Rand(0, Math.PI * 2);
                double y = Rand(minY, maxY);
                double flat = Math.Sqrt(Math.Max(0.0, 1.0 - y * y));
                return new Cut(new V(Math.Cos(a) * flat, y, Math.Sin(a) * flat).Unit(), Rand(minDistance, maxDistance));
            }

            /// <summary>
            /// A lumpy ball: a sphere pushed in and out by smooth noise, with flat chips cut off it, then stretched
            /// to <paramref name="radii"/>, tipped by <paramref name="pitch"/> and turned by <paramref name="yaw"/>.
            /// </summary>
            public void Blob(Lump lump, V centre, V radii, int around, int down, double lumpiness, double yaw, double pitch, List<Cut> cuts)
            {
                uint seed = Seed();
                double ox = Rand(-40, 40), oy = Rand(-40, 40), oz = Rand(-40, 40);
                double cy = Math.Cos(yaw), sy = Math.Sin(yaw), cp = Math.Cos(pitch), sp = Math.Sin(pitch);

                V Place(V dir, out double shadow)
                {
                    double n = Noise3.Perlin(dir.X * 1.3 + ox, dir.Y * 1.3 + oy, dir.Z * 1.3 + oz, seed);
                    double r = 1.0 + lumpiness * n;
                    shadow = n < 0 ? Noise3.Clamp01(1.0 + 1.4 * n) : 1.0;
                    if (cuts != null)
                    {
                        for (int i = 0; i < cuts.Count; i++)
                        {
                            double t = V.Dot(dir, cuts[i].Normal);
                            if (t > 1e-6 && cuts[i].Distance / t < r)
                            {
                                r = cuts[i].Distance / t;
                                shadow = 1.0;
                            }
                        }
                    }
                    double x = dir.X * r * radii.X, y = dir.Y * r * radii.Y, z = dir.Z * r * radii.Z;
                    double y2 = y * cp - z * sp, z2 = y * sp + z * cp;      // tip about X
                    double x3 = x * cy + z2 * sy, z3 = -x * sy + z2 * cy;   // turn about Y
                    return new V(centre.X + x3, centre.Y + y2, centre.Z + z3);
                }

                var rings = new List<V[]>();
                var ringShadow = new List<double[]>();
                for (int k = 1; k < down; k++)
                {
                    double polar = Math.PI * k / down;
                    var ring = new V[around];
                    var shadow = new double[around];
                    for (int j = 0; j < around; j++)
                    {
                        // Every other ring is turned half a step, so the faces come out as irregular chips.
                        double az = Math.PI * 2 * (j + (k % 2) * 0.5) / around;
                        var dir = new V(Math.Sin(polar) * Math.Cos(az), Math.Cos(polar), Math.Sin(polar) * Math.Sin(az));
                        ring[j] = Place(dir, out shadow[j]);
                    }
                    rings.Add(ring);
                    ringShadow.Add(shadow);
                }
                V top = Place(new V(0, 1, 0), out double topShadow);
                V bottom = Place(new V(0, -1, 0), out double bottomShadow);
                Tube(lump, rings, ringShadow, top, topShadow, bottom, bottomShadow);
            }

            /// <summary>
            /// A column built from rings that all lie flat (each ring is level, at its own centre and radius).
            /// The rings may go up and then back down, which is how a hollow top is made.
            /// </summary>
            public void Lathe(Lump lump, V[] centres, double[] radiiU, double[] radiiW, int sides, double yaw, double wobble, uint seed,
                V startTip, V endTip, double[] shadow, double star = 0)
            {
                var u = new V(Math.Cos(yaw), 0, Math.Sin(yaw));
                var w = new V(-Math.Sin(yaw), 0, Math.Cos(yaw));
                var rings = new List<V[]>();
                var ringShadow = new List<double[]>();
                for (int i = 0; i < centres.Length; i++)
                {
                    double rw = radiiW != null ? radiiW[i] : radiiU[i];
                    rings.Add(Ring(centres[i], u, w, radiiU[i], rw, sides, 0, wobble, seed, star));
                    ringShadow.Add(Fill(sides, shadow != null ? shadow[i] : 1.0));
                }
                Tube(lump, rings, ringShadow, startTip, shadow != null ? shadow[0] : 1.0, endTip, shadow != null ? shadow[centres.Length - 1] : 1.0);
            }

            /// <summary>
            /// A tube that follows a bent path: an arch, a palm trunk, a cactus arm. <paramref name="reference"/> must
            /// never point along the path. <paramref name="startTip"/> and <paramref name="endTip"/> are how far the
            /// ends stick out past the last ring (0 = cut off flat).
            /// </summary>
            public void Sweep(Lump lump, V[] path, double[] radiiU, double[] radiiW, int sides, V reference, double wobble, uint seed,
                double startTip, double endTip, double[] shadow = null, double star = 0, double phase = 0)
            {
                int n = path.Length;
                var rings = new List<V[]>();
                var ringShadow = new List<double[]>();
                for (int i = 0; i < n; i++)
                {
                    V tangent = (path[Math.Min(i + 1, n - 1)] - path[Math.Max(i - 1, 0)]).Unit();
                    V u = V.Cross(reference, tangent).Unit();
                    V w = V.Cross(tangent, u);
                    rings.Add(Ring(path[i], u, w, radiiU[i], radiiW[i], sides, phase, wobble, seed, star));
                    ringShadow.Add(Fill(sides, shadow != null ? shadow[i] : 1.0));
                }
                V first = (path[1] - path[0]).Unit();
                V last = (path[n - 1] - path[n - 2]).Unit();
                Tube(lump, rings, ringShadow, path[0] - first * startTip, shadow != null ? shadow[0] : 1.0,
                    path[n - 1] + last * endTip, shadow != null ? shadow[n - 1] : 1.0);
            }

            static double[] Fill(int count, double value)
            {
                var a = new double[count];
                for (int i = 0; i < count; i++) a[i] = value;
                return a;
            }

            static V[] Ring(V centre, V u, V w, double radiusU, double radiusW, int sides, double phase, double wobble, uint seed, double star)
            {
                var ring = new V[sides];
                for (int j = 0; j < sides; j++)
                {
                    double a = Math.PI * 2 * (j + phase) / sides;
                    double c = Math.Cos(a), s = Math.Sin(a);
                    double k = 1.0;
                    if (wobble != 0) k += wobble * Noise3.Perlin(centre.X * 4 + c * 1.3, centre.Y * 4, centre.Z * 4 + s * 1.3, seed);
                    if (star != 0 && j % 2 == 1) k *= 1.0 - star;
                    ring[j] = centre + u * (c * radiusU * k) + w * (s * radiusW * k);
                }
                return ring;
            }

            /// <summary>Joins a row of rings into a closed skin, with a fan of triangles to a single point at each end.</summary>
            static void Tube(Lump lump, List<V[]> rings, List<double[]> shadow, V start, double startShadow, V end, double endShadow)
            {
                int n = rings[0].Length;
                int first = lump.Points.Count;
                for (int i = 0; i < rings.Count; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        lump.Points.Add(rings[i][j]);
                        lump.Shadow.Add(shadow[i][j]);
                    }
                }
                int startIndex = lump.Points.Count;
                lump.Points.Add(start);
                lump.Shadow.Add(startShadow);
                int endIndex = lump.Points.Count;
                lump.Points.Add(end);
                lump.Shadow.Add(endShadow);

                var t = lump.Triangles;
                for (int j = 0; j < n; j++)
                {
                    int j1 = (j + 1) % n;
                    t.Add(startIndex); t.Add(first + j1); t.Add(first + j);
                    int lastRing = first + (rings.Count - 1) * n;
                    t.Add(endIndex); t.Add(lastRing + j); t.Add(lastRing + j1);
                }
                for (int i = 0; i < rings.Count - 1; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        int j1 = (j + 1) % n;
                        int a = first + i * n + j, b = first + i * n + j1;
                        int c = first + (i + 1) * n + j, d = first + (i + 1) * n + j1;
                        t.Add(a); t.Add(b); t.Add(c);
                        t.Add(b); t.Add(d); t.Add(c);
                    }
                }
            }

            /// <summary>
            /// Turns every lump the right way out, sets the top to exactly y = 1, works out the colours and gives
            /// each triangle its own three corners.
            /// </summary>
            public ShapeData Finish(double floatDepth)
            {
                // Each piece is closed but may have been built inside out; the sign of its volume says which.
                foreach (Lump lump in _lumps) FaceOutward(lump);

                double top = double.MinValue, bottom = double.MaxValue;
                foreach (Lump lump in _lumps)
                {
                    foreach (V p in lump.Points)
                    {
                        if (p.Y > top) top = p.Y;
                        if (p.Y < bottom) bottom = p.Y;
                    }
                }
                double scaleUp = top > 1e-6 ? 1.0 / top : 1.0;
                double scaleDown = !double.IsNaN(floatDepth) && bottom < -1e-6 ? floatDepth / bottom : 1.0;
                double lowest = !double.IsNaN(floatDepth) ? floatDepth : 0.0;

                int triangleCount = 0;
                foreach (Lump lump in _lumps) triangleCount += lump.Triangles.Count / 3;
                var data = new ShapeData
                {
                    Positions = new float[triangleCount * 9],
                    Colors = new float[triangleCount * 9],
                    Triangles = new int[triangleCount * 3],
                };

                int o = 0;
                var corner = new V[3];
                foreach (Lump lump in _lumps)
                {
                    for (int i = 0; i < lump.Points.Count; i++)
                    {
                        V p = lump.Points[i];
                        p.Y *= p.Y > 0 ? scaleUp : scaleDown;
                        // Through float and back, so the face direction below is worked out from what is stored.
                        lump.Points[i] = new V((float)p.X, (float)p.Y, (float)p.Z);
                    }
                    for (int t = 0; t < lump.Triangles.Count; t += 3)
                    {
                        for (int k = 0; k < 3; k++) corner[k] = lump.Points[lump.Triangles[t + k]];
                        V normal = V.Cross(corner[1] - corner[0], corner[2] - corner[0]).Unit();
                        for (int k = 0; k < 3; k++)
                        {
                            V p = corner[k];
                            double shadow = lump.Shadow[lump.Triangles[t + k]];
                            double height = Noise3.Clamp01((p.Y - lowest) / (1.0 - lowest));
                            double shade;
                            if (lump.IsRock)
                            {
                                shade = 0.8 + 0.1 * height + 0.07 * normal.Y + lump.Tone;
                                shade -= 0.05 * (1.0 - Noise3.Smooth(lowest, lowest + 0.15, p.Y));
                                shade -= 0.12 * (1.0 - shadow);
                                if (lump.Layers) shade -= 0.07 * Stripe(p);
                                shade = shade < 0.7 ? 0.7 : shade > 1.0 ? 1.0 : shade;
                            }
                            else
                            {
                                shade = 0.84 + 0.16 * height + 0.08 * normal.Y - 0.18 * (1.0 - shadow);
                                shade = shade < 0.6 ? 0.6 : shade > 1.15 ? 1.15 : shade;
                            }
                            data.Positions[o * 3] = (float)p.X;
                            data.Positions[o * 3 + 1] = (float)p.Y;
                            data.Positions[o * 3 + 2] = (float)p.Z;
                            data.Colors[o * 3] = (float)Noise3.Clamp01(lump.R * shade);
                            data.Colors[o * 3 + 1] = (float)Noise3.Clamp01(lump.G * shade);
                            data.Colors[o * 3 + 2] = (float)Noise3.Clamp01(lump.B * shade);
                            data.Triangles[o] = o;
                            o++;
                        }
                    }
                }
                return data;
            }

            /// <summary>Bands of darker rock at set heights, bent a little so they do not look ruled.</summary>
            double Stripe(V p)
            {
                double q = p.Y * 7.0 + 0.35 * Noise3.Perlin(p.X * 1.5, p.Y * 1.5, p.Z * 1.5, _layerSeed);
                double f = q - Math.Floor(q);
                return Noise3.Smooth(0.0, 0.1, f) * (1.0 - Noise3.Smooth(0.4, 0.5, f));
            }

            /// <summary>
            /// Flips any separate piece of a lump that was built inside out. A closed piece that faces outward has
            /// a positive volume when you add up the little pyramids from the origin to each triangle.
            /// </summary>
            static void FaceOutward(Lump lump)
            {
                int count = lump.Points.Count;
                var group = new int[count];
                for (int i = 0; i < count; i++) group[i] = i;
                int Find(int i)
                {
                    while (group[i] != i)
                    {
                        group[i] = group[group[i]];
                        i = group[i];
                    }
                    return i;
                }
                var t = lump.Triangles;
                for (int i = 0; i < t.Count; i += 3)
                {
                    int a = Find(t[i]);
                    group[Find(t[i + 1])] = a;
                    group[Find(t[i + 2])] = a;
                }
                var volume = new double[count];
                for (int i = 0; i < t.Count; i += 3)
                {
                    V a = lump.Points[t[i]], b = lump.Points[t[i + 1]], c = lump.Points[t[i + 2]];
                    volume[Find(t[i])] += V.Dot(a, V.Cross(b, c));
                }
                for (int i = 0; i < t.Count; i += 3)
                {
                    if (volume[Find(t[i])] >= 0) continue;
                    int swap = t[i + 1];
                    t[i + 1] = t[i + 2];
                    t[i + 2] = swap;
                }
            }
        }
    }
}
