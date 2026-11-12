using System;
using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Planets;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Checks every ready-made shape (rocks, arches, trees...) in every variant: right size, closed skin,
    /// facing outward, inside its triangle budget, and the same every time it is built.
    /// </summary>
    public class FeatureShapesTests
    {
        static IEnumerable<TestCaseData> AllShapes()
        {
            foreach (FeatureKind kind in Enum.GetValues(typeof(FeatureKind)))
            {
                for (int v = 0; v < FeatureShapes.Variants; v++) yield return new TestCaseData(kind, v);
            }
        }

        static int Budget(FeatureKind kind)
        {
            switch (kind)
            {
                case FeatureKind.Boulder:
                case FeatureKind.Slab:
                case FeatureKind.Bush:
                case FeatureKind.Crystal:
                case FeatureKind.IceShard:
                case FeatureKind.Stalagmite:
                case FeatureKind.Cactus:
                    return 320;
                default:
                    return 720;
            }
        }

        static double[] Corner(ShapeData s, int index)
        {
            return new double[] { s.Positions[index * 3], s.Positions[index * 3 + 1], s.Positions[index * 3 + 2] };
        }

        static double[] Cross(double[] a, double[] b)
        {
            return new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };
        }

        static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        static double[] Sub(double[] a, double[] b) => new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };

        /// <summary>Gives corners at the same place the same number, so separate triangles can be joined up.</summary>
        static int[] Weld(ShapeData s, out int count)
        {
            var seen = new Dictionary<(long, long, long), int>();
            var ids = new int[s.VertexCount];
            for (int i = 0; i < s.VertexCount; i++)
            {
                var key = ((long)Math.Round(s.Positions[i * 3] * 100000.0), (long)Math.Round(s.Positions[i * 3 + 1] * 100000.0),
                    (long)Math.Round(s.Positions[i * 3 + 2] * 100000.0));
                if (!seen.TryGetValue(key, out int id))
                {
                    id = seen.Count;
                    seen.Add(key, id);
                }
                ids[i] = id;
            }
            count = seen.Count;
            return ids;
        }

        [TestCaseSource(nameof(AllShapes))]
        public void Arrays_AreWellFormed(FeatureKind kind, int variant)
        {
            ShapeData s = FeatureShapes.Build(kind, variant);
            Assert.That(s.Positions.Length, Is.GreaterThan(0));
            Assert.That(s.Positions.Length % 3, Is.EqualTo(0));
            Assert.That(s.Colors.Length, Is.EqualTo(s.Positions.Length));
            Assert.That(s.Triangles.Length % 3, Is.EqualTo(0));
            foreach (float p in s.Positions) Assert.That(float.IsNaN(p) || float.IsInfinity(p), Is.False, "position is not a number");
            foreach (float c in s.Colors)
            {
                Assert.That(float.IsNaN(c), Is.False, "colour is not a number");
                Assert.That(c, Is.InRange(0f, 1f));
            }
            foreach (int index in s.Triangles) Assert.That(index, Is.InRange(0, s.VertexCount - 1));
        }

        [TestCaseSource(nameof(AllShapes))]
        public void Shape_IsOneUnitTall_AndBuried(FeatureKind kind, int variant)
        {
            ShapeData s = FeatureShapes.Build(kind, variant);
            double top = double.MinValue, bottom = double.MaxValue;
            for (int i = 0; i < s.VertexCount; i++)
            {
                top = Math.Max(top, s.Positions[i * 3 + 1]);
                bottom = Math.Min(bottom, s.Positions[i * 3 + 1]);
            }
            Assert.That(top, Is.EqualTo(1.0).Within(0.02), "top should be at y = 1");
            Assert.That(top, Is.LessThanOrEqualTo(1.0001), "nothing above y = 1");
            Assert.That(bottom, Is.LessThan(-0.05), "base should carry on below the ground");
            if (kind == FeatureKind.Iceberg) Assert.That(bottom, Is.EqualTo(-0.6).Within(0.02), "an iceberg hangs 0.6 under the water");
            else Assert.That(bottom, Is.GreaterThan(-0.4), "base should not go far underground");
        }

        [TestCaseSource(nameof(AllShapes))]
        public void TriangleBudget_IsRespected(FeatureKind kind, int variant)
        {
            ShapeData s = FeatureShapes.Build(kind, variant);
            Assert.That(s.Triangles.Length / 3, Is.LessThanOrEqualTo(Budget(kind)));
            Assert.That(s.Triangles.Length / 3, Is.GreaterThanOrEqualTo(20), "too few triangles to be anything but a plain primitive");
        }

        [TestCaseSource(nameof(AllShapes))]
        public void NoDegenerateTriangles(FeatureKind kind, int variant)
        {
            ShapeData s = FeatureShapes.Build(kind, variant);
            for (int t = 0; t < s.Triangles.Length; t += 3)
            {
                double[] a = Corner(s, s.Triangles[t]), b = Corner(s, s.Triangles[t + 1]), c = Corner(s, s.Triangles[t + 2]);
                double[] n = Cross(Sub(b, a), Sub(c, a));
                double area = 0.5 * Math.Sqrt(Dot(n, n));
                Assert.That(area, Is.GreaterThan(1e-6), "triangle " + t / 3 + " has no area");
            }
        }

        [TestCaseSource(nameof(AllShapes))]
        public void Surface_IsClosed(FeatureKind kind, int variant)
        {
            ShapeData s = FeatureShapes.Build(kind, variant);
            int[] ids = Weld(s, out _);
            var edges = new Dictionary<(int, int), int>();
            for (int t = 0; t < s.Triangles.Length; t += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    int from = ids[s.Triangles[t + k]], to = ids[s.Triangles[t + (k + 1) % 3]];
                    Assert.That(from, Is.Not.EqualTo(to), "a triangle has two corners in the same place");
                    edges.TryGetValue((from, to), out int n);
                    edges[(from, to)] = n + 1;
                }
            }
            foreach (KeyValuePair<(int, int), int> edge in edges)
            {
                Assert.That(edge.Value, Is.EqualTo(1), "an edge is used twice in the same direction");
                Assert.That(edges.ContainsKey((edge.Key.Item2, edge.Key.Item1)), Is.True, "an edge has no triangle on its other side (a hole)");
            }
        }

        [TestCaseSource(nameof(AllShapes))]
        public void Winding_FacesOutward_ForEverySeparateLump(FeatureKind kind, int variant)
        {
            ShapeData s = FeatureShapes.Build(kind, variant);
            int[] ids = Weld(s, out int count);

            // Group corners into separate lumps.
            var group = new int[count];
            for (int i = 0; i < count; i++) group[i] = i;
            int Find(int i)
            {
                while (group[i] != i) i = group[i] = group[group[i]];
                return i;
            }
            for (int t = 0; t < s.Triangles.Length; t += 3)
            {
                int a = Find(ids[s.Triangles[t]]);
                group[Find(ids[s.Triangles[t + 1]])] = a;
                group[Find(ids[s.Triangles[t + 2]])] = a;
            }

            var volume = new Dictionary<int, double>();
            double total = 0;
            for (int t = 0; t < s.Triangles.Length; t += 3)
            {
                double[] a = Corner(s, s.Triangles[t]), b = Corner(s, s.Triangles[t + 1]), c = Corner(s, s.Triangles[t + 2]);
                double v = Dot(a, Cross(b, c)) / 6.0;
                total += v;
                int g = Find(ids[s.Triangles[t]]);
                volume.TryGetValue(g, out double sum);
                volume[g] = sum + v;
            }
            Assert.That(total, Is.GreaterThan(0), "the whole shape is inside out");
            foreach (KeyValuePair<int, double> lump in volume) Assert.That(lump.Value, Is.GreaterThan(1e-6), "one lump is inside out");
        }

        [TestCaseSource(nameof(AllShapes))]
        public void EveryTriangle_HasItsOwnCorners(FeatureKind kind, int variant)
        {
            // Flat shading needs unshared corners; it also means the index list is simply 0, 1, 2, 3...
            ShapeData s = FeatureShapes.Build(kind, variant);
            Assert.That(s.Triangles.Length, Is.EqualTo(s.VertexCount));
            var used = new bool[s.VertexCount];
            foreach (int index in s.Triangles)
            {
                Assert.That(used[index], Is.False);
                used[index] = true;
            }
        }

        [TestCaseSource(nameof(AllShapes))]
        public void SameKindAndVariant_BuildsIdentically(FeatureKind kind, int variant)
        {
            ShapeData a = FeatureShapes.Build(kind, variant);
            ShapeData b = FeatureShapes.Build(kind, variant);
            Assert.That(b.Positions, Is.EqualTo(a.Positions));
            Assert.That(b.Colors, Is.EqualTo(a.Colors));
            Assert.That(b.Triangles, Is.EqualTo(a.Triangles));
        }

        [Test]
        public void Variants_AreDifferent_AndWrapAround()
        {
            foreach (FeatureKind kind in Enum.GetValues(typeof(FeatureKind)))
            {
                for (int i = 0; i < FeatureShapes.Variants; i++)
                {
                    for (int j = i + 1; j < FeatureShapes.Variants; j++)
                    {
                        Assert.That(FeatureShapes.Build(kind, i).Positions, Is.Not.EqualTo(FeatureShapes.Build(kind, j).Positions),
                            kind + " variants " + i + " and " + j + " are the same");
                    }
                }
                Assert.That(FeatureShapes.Build(kind, FeatureShapes.Variants + 1).Positions, Is.EqualTo(FeatureShapes.Build(kind, 1).Positions));
                Assert.That(FeatureShapes.Build(kind, -1).Positions, Is.EqualTo(FeatureShapes.Build(kind, FeatureShapes.Variants - 1).Positions));
            }
        }

        [Test]
        public void RockShades_StayNearWhite_SoThePlanetColourShowsThrough()
        {
            FeatureKind[] rocks =
            {
                FeatureKind.Boulder, FeatureKind.Slab, FeatureKind.RockPile, FeatureKind.Hoodoo, FeatureKind.Spire, FeatureKind.Arch,
                FeatureKind.BalancedRock, FeatureKind.Tor, FeatureKind.BasaltColumns, FeatureKind.Stalagmite, FeatureKind.VentChimney,
                FeatureKind.IceShard, FeatureKind.Iceberg, FeatureKind.Crystal,
            };
            foreach (FeatureKind kind in rocks)
            {
                for (int v = 0; v < FeatureShapes.Variants; v++)
                {
                    ShapeData s = FeatureShapes.Build(kind, v);
                    float low = 1f, high = 0f;
                    foreach (float c in s.Colors)
                    {
                        low = Math.Min(low, c);
                        high = Math.Max(high, c);
                    }
                    Assert.That(low, Is.GreaterThanOrEqualTo(0.64f), kind + " " + v + " has a shade that is too dark");
                    Assert.That(high - low, Is.GreaterThan(0.08f), kind + " " + v + " is one flat shade");
                }
            }
        }

        [Test]
        public void WidthToHeight_IsSensible_AndCoversTheShape()
        {
            foreach (FeatureKind kind in Enum.GetValues(typeof(FeatureKind)))
            {
                float ratio = FeatureShapes.WidthToHeight(kind);
                Assert.That(ratio, Is.InRange(0.1f, 2f));
                for (int v = 0; v < FeatureShapes.Variants; v++)
                {
                    ShapeData s = FeatureShapes.Build(kind, v);
                    double reach = 0;
                    for (int i = 0; i < s.VertexCount; i++)
                    {
                        double x = s.Positions[i * 3], z = s.Positions[i * 3 + 2];
                        reach = Math.Max(reach, Math.Sqrt(x * x + z * z));
                    }
                    // "Rough" half-width: the furthest corner may poke out, but not by more than about half again.
                    Assert.That(reach, Is.LessThan(ratio * 1.6 + 0.05), kind + " " + v + " is much wider than WidthToHeight says");
                    Assert.That(reach, Is.GreaterThan(ratio * 0.45), kind + " " + v + " is much narrower than WidthToHeight says");
                }
            }
        }

        /// <summary>True if a line along Z at (x, y) passes through any triangle of the shape.</summary>
        static bool LineAlongZHits(ShapeData s, double x, double y)
        {
            for (int t = 0; t < s.Triangles.Length; t += 3)
            {
                double[] a = Corner(s, s.Triangles[t]), b = Corner(s, s.Triangles[t + 1]), c = Corner(s, s.Triangles[t + 2]);
                // Looking along Z, the line is a point: is it inside the triangle's outline?
                double d1 = (b[0] - a[0]) * (y - a[1]) - (b[1] - a[1]) * (x - a[0]);
                double d2 = (c[0] - b[0]) * (y - b[1]) - (c[1] - b[1]) * (x - b[0]);
                double d3 = (a[0] - c[0]) * (y - c[1]) - (a[1] - c[1]) * (x - c[0]);
                bool negative = d1 < 0 || d2 < 0 || d3 < 0;
                bool positive = d1 > 0 || d2 > 0 || d3 > 0;
                if (!(negative && positive)) return true;
            }
            return false;
        }

        [Test]
        public void Arch_CanBeFlownThroughAlongZ()
        {
            for (int v = 0; v < FeatureShapes.Variants; v++)
            {
                ShapeData s = FeatureShapes.Build(FeatureKind.Arch, v);
                Assert.That(LineAlongZHits(s, 0, 0.3), Is.False, "variant " + v + ": the middle of the opening is blocked");
                // Opening at least 0.55 tall in the middle and about 1.0 wide near the ground.
                foreach (double y in new[] { 0.02, 0.15, 0.45, 0.55 })
                {
                    Assert.That(LineAlongZHits(s, 0, y), Is.False, "variant " + v + ": blocked at height " + y);
                }
                foreach (double x in new[] { -0.48, -0.3, 0.3, 0.48 })
                {
                    Assert.That(LineAlongZHits(s, x, 0.05), Is.False, "variant " + v + ": blocked at x = " + x);
                    Assert.That(LineAlongZHits(s, x * 0.8, 0.4), Is.False, "variant " + v + ": blocked at x = " + x * 0.8 + ", y = 0.4");
                }
                // And it really is an arch: solid legs either side, and a span over the top.
                Assert.That(LineAlongZHits(s, -0.75, 0.2), Is.True, "variant " + v + ": no left leg");
                Assert.That(LineAlongZHits(s, 0.75, 0.2), Is.True, "variant " + v + ": no right leg");
                Assert.That(LineAlongZHits(s, 0, 0.9), Is.True, "variant " + v + ": no span over the top");
            }
        }
    }
}
