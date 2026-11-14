using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OrbitalAnte.Ship;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Checks the starter lander's fittings by measuring them: sound triangles, inside their budgets, standing
    /// on the ground, clear of the windows and the pilot's eyes, inside the cabin, and facing the right way.
    /// A triangle (a, b, c) is taken to face the side that cross(b - a, c - a) points to.
    /// </summary>
    public class ShipPropsTests
    {
        const int ExteriorBudget = 14000, InteriorBudget = 26000;

        static readonly Dictionary<string, ShipMesh> Meshes = ShipProps.All().ToDictionary(p => p.Key, p => p.Value);
        static readonly string[] ExteriorNames = ShipProps.Exterior().Select(p => p.Key).ToArray();
        static readonly string[] InteriorNames = ShipProps.Interior().Select(p => p.Key).ToArray();
        /// <summary>Interior meshes that are built in their final place (the rest are built at the origin for the game to move).</summary>
        static readonly string[] PlacedInterior = InteriorNames.Where(n => n != "belt_strap" && n != "belt_buckle").ToArray();

        static IEnumerable<string> AllNames() => ShipProps.All().Select(p => p.Key);

        // ---------------------------------------------------------------- measuring helpers

        static P3 Normal(ShipMesh m, int t)
        {
            P3 a = m.Position(m.Triangles[t * 3]), b = m.Position(m.Triangles[t * 3 + 1]), c = m.Position(m.Triangles[t * 3 + 2]);
            return P3.Cross(b - a, c - a);
        }

        static P3 Centroid(ShipMesh m, int t)
        {
            return (m.Position(m.Triangles[t * 3]) + m.Position(m.Triangles[t * 3 + 1]) + m.Position(m.Triangles[t * 3 + 2])) * (1f / 3f);
        }

        static float Flat(P3 p) => (float)Math.Sqrt(p.X * p.X + p.Z * p.Z);

        static float MaxEdge(ShipMesh m)
        {
            float longest = 0f;
            for (int t = 0; t < m.TriangleCount; t++)
            {
                for (int e = 0; e < 3; e++)
                {
                    P3 a = m.Position(m.Triangles[t * 3 + e]), b = m.Position(m.Triangles[t * 3 + (e + 1) % 3]);
                    longest = Math.Max(longest, P3.Distance(a, b));
                }
            }
            return longest;
        }

        /// <summary>The inside of the wall at a height, as it is built (straight steps between the layout's rows).</summary>
        static float WallRadius(float y)
        {
            ShipLayout.InnerProfile(0, out float r0, out float h0);
            if (y <= h0) return r0;
            for (int row = 1; row < ShipLayout.WallRows; row++)
            {
                ShipLayout.InnerProfile(row, out float r1, out float h1);
                if (y <= h1) return r0 + (r1 - r0) * (y - h0) / Math.Max(1e-6f, h1 - h0);
                r0 = r1;
                h0 = h1;
            }
            return r0;
        }

        /// <summary>
        /// Where a point outside the cabin sits against the hull: which column and row of the wall grid it is
        /// over, and how far through the wall (0 inside face, 1 outside face, more than 1 standing off it).
        /// </summary>
        static void AgainstHull(P3 p, out float column, out float row, out float through)
        {
            double angle = Math.Atan2(p.X, p.Z);
            if (angle < 0) angle += 2.0 * Math.PI;
            column = (float)(angle / (2.0 * Math.PI) * ShipLayout.WallColumns);
            float r = Flat(p);
            float best = float.MaxValue;
            row = 0f;
            through = 0f;
            for (float candidate = 0f; candidate <= ShipLayout.WallRows - 1; candidate += 0.02f)
            {
                P3 inner = ShipLayout.SurfacePoint(0f, candidate, 0f), outer = ShipLayout.SurfacePoint(0f, candidate, 1f);
                float dr = outer.Z - inner.Z, dh = outer.Y - inner.Y;
                float t = ((r - inner.Z) * dr + (p.Y - inner.Y) * dh) / (dr * dr + dh * dh);
                float mr = inner.Z + dr * t - r, mh = inner.Y + dh * t - p.Y;
                float miss = mr * mr + mh * mh;
                if (miss < best)
                {
                    best = miss;
                    row = candidate;
                    through = t;
                }
            }
        }

        // ---------------------------------------------------------------- every mesh

        [TestCaseSource(nameof(AllNames))]
        public void Mesh_IsSound(string name)
        {
            ShipMesh m = Meshes[name];
            Assert.Greater(m.TriangleCount, 0);
            Assert.AreEqual(m.Positions.Length, m.Colors.Length);
            Assert.AreEqual(m.VertexCount, m.Glow.Length);
            Assert.AreEqual(0, m.Triangles.Length % 3);
            foreach (float v in m.Positions) Assert.IsTrue(!float.IsNaN(v) && !float.IsInfinity(v), name + " has a broken position");
            foreach (float v in m.Colors) Assert.IsTrue(v >= 0f && v <= 1f, name + " has a colour out of range");
            foreach (float v in m.Glow) Assert.IsTrue(v >= 0f && v <= 8f, name + " has glow out of range");
            foreach (int i in m.Triangles) Assert.IsTrue(i >= 0 && i < m.VertexCount, name + " has a triangle pointing at no corner");
            for (int t = 0; t < m.TriangleCount; t++)
            {
                Assert.Greater(Normal(m, t).Length, 1e-9f, name + " triangle " + t + " has no area");
            }
        }

        [TestCaseSource(nameof(AllNames))]
        public void Mesh_IsTheSameEveryTime(string name)
        {
            ShipMesh again = ShipProps.All().First(p => p.Key == name).Value;
            CollectionAssert.AreEqual(Meshes[name].Positions, again.Positions);
            CollectionAssert.AreEqual(Meshes[name].Triangles, again.Triangles);
        }

        /// <summary>
        /// Wherever a mesh has a piece that is sealed all round (every edge shared by exactly two triangles
        /// running opposite ways), that piece must enclose a positive volume: its skin faces outward.
        /// </summary>
        [TestCaseSource(nameof(AllNames))]
        public void Mesh_SealedPiecesFaceOutward(string name)
        {
            ShipMesh m = Meshes[name];
            var parent = new int[m.VertexCount];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            Func<int, int> find = null;
            find = i => parent[i] == i ? i : (parent[i] = find(parent[i]));
            for (int t = 0; t < m.TriangleCount; t++)
            {
                int a = find(m.Triangles[t * 3]);
                parent[find(m.Triangles[t * 3 + 1])] = a;
                parent[find(m.Triangles[t * 3 + 2])] = a;
            }
            var edges = new Dictionary<int, Dictionary<long, int>>();
            var open = new HashSet<int>();
            var volume = new Dictionary<int, double>();
            for (int t = 0; t < m.TriangleCount; t++)
            {
                int piece = find(m.Triangles[t * 3]);
                if (!edges.ContainsKey(piece)) { edges[piece] = new Dictionary<long, int>(); volume[piece] = 0; }
                for (int e = 0; e < 3; e++)
                {
                    long from = m.Triangles[t * 3 + e], to = m.Triangles[t * 3 + (e + 1) % 3];
                    long key = from * 1000000L + to;
                    if (edges[piece].ContainsKey(key)) open.Add(piece);
                    edges[piece][key] = 1;
                }
                P3 pa = m.Position(m.Triangles[t * 3]), pb = m.Position(m.Triangles[t * 3 + 1]), pc = m.Position(m.Triangles[t * 3 + 2]);
                P3 first = m.Position(piece);
                volume[piece] += P3.Dot(pa - first, P3.Cross(pb - first, pc - first)) / 6.0;
            }
            int sealedPieces = 0;
            foreach (var pair in edges)
            {
                bool isSealed = !open.Contains(pair.Key);
                foreach (long key in pair.Value.Keys)
                {
                    long back = (key % 1000000L) * 1000000L + key / 1000000L;
                    if (!pair.Value.ContainsKey(back)) { isSealed = false; break; }
                }
                if (!isSealed) continue;
                sealedPieces++;
                Assert.Greater(volume[pair.Key], 0.0, name + " has a sealed piece that is inside out (near " + m.Position(pair.Key) + ")");
            }
            TestContext.Out.WriteLine(name + ": " + sealedPieces + " sealed pieces of " + edges.Count);
        }

        // ---------------------------------------------------------------- budgets

        [Test]
        public void TriangleBudgets_AreKept()
        {
            int outside = 0, inside = 0;
            foreach (string n in ExteriorNames)
            {
                outside += Meshes[n].TriangleCount;
                TestContext.Out.WriteLine(n + ": " + Meshes[n].TriangleCount + " triangles, " + Meshes[n].VertexCount + " corners");
            }
            foreach (string n in InteriorNames)
            {
                inside += Meshes[n].TriangleCount;
                TestContext.Out.WriteLine(n + ": " + Meshes[n].TriangleCount + " triangles, " + Meshes[n].VertexCount + " corners, longest edge " + MaxEdge(Meshes[n]).ToString("0.000"));
            }
            TestContext.Out.WriteLine("exterior total " + outside + ", interior total " + inside);
            Assert.Less(outside, ExteriorBudget);
            Assert.Less(inside, InteriorBudget);
        }

        // ---------------------------------------------------------------- exterior

        [Test]
        public void Exterior_NothingBelowTheGround()
        {
            foreach (string n in ExteriorNames)
            {
                ShipMesh m = Meshes[n];
                for (int i = 0; i < m.VertexCount; i++) Assert.GreaterOrEqual(m.Position(i).Y, ShipLayout.GroundY - 1e-4f, n);
            }
        }

        [Test]
        public void Footpads_TouchTheGround_AndFaceOutward([Values(0, 1, 2, 3)] int index)
        {
            ShipMesh m = ShipProps.Leg(index);
            P3 pad = ShipLayout.Footpad(index);
            float lowest = float.MaxValue, widest = 0f;
            for (int i = 0; i < m.VertexCount; i++)
            {
                P3 p = m.Position(i);
                lowest = Math.Min(lowest, p.Y);
                if (p.Y < ShipLayout.GroundY + 0.14f) widest = Math.Max(widest, Flat(p - pad));
            }
            Assert.AreEqual(ShipLayout.GroundY, lowest, 1e-4f);
            Assert.AreEqual(ShipLayout.FootpadRadius, widest, 1e-3f);

            int under = 0, rim = 0;
            for (int t = 0; t < m.TriangleCount; t++)
            {
                P3 c = Centroid(m, t), n = Normal(m, t).Unit();
                if (Flat(c - pad) > ShipLayout.FootpadRadius + 0.01f) continue;
                if (c.Y < ShipLayout.GroundY + 0.005f)
                {
                    Assert.Less(n.Y, -0.9f, "underside of footpad " + index + " should face down");
                    under++;
                }
                else if (c.Y < ShipLayout.GroundY + 0.1f && Flat(c - pad) > 0.41f)
                {
                    P3 away = new P3(c.X - pad.X, 0f, c.Z - pad.Z).Unit();
                    Assert.Greater(P3.Dot(n, away), 0.1f, "outside of footpad " + index + " should face outward");
                    rim++;
                }
            }
            Assert.Greater(under, 0);
            Assert.Greater(rim, 0);
        }

        [Test]
        public void Legs_RunFromTheBaseToTheFootpad_AndOnlyLegZeroHasALadder()
        {
            for (int index = 0; index < ShipLayout.Legs; index++)
            {
                ShipMesh m = ShipProps.Leg(index);
                P3 root = ShipLayout.LegRoot(index);
                float nearest = float.MaxValue;
                for (int i = 0; i < m.VertexCount; i++) nearest = Math.Min(nearest, P3.Distance(m.Position(i), root));
                Assert.Less(nearest, 0.25f, "leg " + index + " does not reach its root");
            }
            Assert.Greater(ShipProps.Leg(0).TriangleCount, ShipProps.Leg(1).TriangleCount);
            Assert.AreEqual(ShipProps.Leg(1).TriangleCount, ShipProps.Leg(2).TriangleCount);
        }

        [Test]
        public void Base_HasShaftHoles_AndFacesOutward()
        {
            ShipMesh m = Meshes["base"];
            float corner = ShipLayout.BaseInradius / (float)Math.Cos(Math.PI / 8.0);
            int tops = 0, bottoms = 0, sides = 0;
            bool holeTop = false, holeBottom = false;
            for (int i = 0; i < m.VertexCount; i++)
            {
                P3 p = m.Position(i);
                Assert.GreaterOrEqual(Flat(p), ShipLayout.ShaftRadius - 1e-4f, "something covers the lift shaft");
                Assert.LessOrEqual(Flat(p), corner + 1e-3f);
                Assert.GreaterOrEqual(p.Y, ShipLayout.BellyY - 1e-5f);
                Assert.LessOrEqual(p.Y, ShipLayout.BaseTop + 1e-5f);
                if (Math.Abs(Flat(p) - ShipLayout.ShaftRadius) < 1e-4f)
                {
                    if (Math.Abs(p.Y - ShipLayout.BaseTop) < 1e-5f) holeTop = true;
                    if (Math.Abs(p.Y - ShipLayout.BellyY) < 1e-5f) holeBottom = true;
                }
            }
            Assert.IsTrue(holeTop && holeBottom, "the base needs the shaft's edge on both its top and its bottom");
            for (int t = 0; t < m.TriangleCount; t++)
            {
                P3 c = Centroid(m, t), n = Normal(m, t).Unit();
                if (Math.Abs(c.Y - ShipLayout.BaseTop) < 1e-4f) { Assert.Greater(n.Y, 0.99f); tops++; }
                else if (Math.Abs(c.Y - ShipLayout.BellyY) < 1e-4f) { Assert.Less(n.Y, -0.99f); bottoms++; }
                else { Assert.Greater(P3.Dot(n, new P3(c.X, 0f, c.Z).Unit()), 0.5f, "a side panel faces inward"); sides++; }
            }
            Assert.Greater(tops, 0);
            Assert.Greater(bottoms, 0);
            Assert.Greater(sides, 0);
            // The flat sides really are flat and at the stated distance: the furthest any corner is along a side's own direction.
            for (int face = 0; face < 8; face++)
            {
                double a = face * Math.PI / 4.0;
                float reach = 0f;
                for (int i = 0; i < m.VertexCount; i++) reach = Math.Max(reach, m.Position(i).X * (float)Math.Sin(a) + m.Position(i).Z * (float)Math.Cos(a));
                Assert.AreEqual(ShipLayout.BaseInradius, reach, 1e-3f);
            }
        }

        [Test]
        public void Engines_HangFromTheBelly_FlareToTheirExit_AndShowBothFaces()
        {
            ShipMesh m = Meshes["engines"];
            float lowest = float.MaxValue, highest = float.MinValue;
            var widest = new float[4];
            for (int i = 0; i < m.VertexCount; i++)
            {
                P3 p = m.Position(i);
                lowest = Math.Min(lowest, p.Y);
                highest = Math.Max(highest, p.Y);
                int nearest = 0;
                for (int e = 1; e < 4; e++)
                {
                    if (Flat(p - ShipLayout.Engine(e)) < Flat(p - ShipLayout.Engine(nearest))) nearest = e;
                }
                widest[nearest] = Math.Max(widest[nearest], Flat(p - ShipLayout.Engine(nearest)));
            }
            Assert.AreEqual(ShipLayout.EngineExitY, lowest, 1e-4f);
            Assert.AreEqual(ShipLayout.BellyY, highest, 1e-4f);
            foreach (float w in widest) Assert.AreEqual(ShipLayout.EngineExitRadius, w, 1e-3f);

            int outward = 0, inward = 0;
            for (int t = 0; t < m.TriangleCount; t++)
            {
                P3 c = Centroid(m, t), n = Normal(m, t).Unit();
                int nearest = 0;
                for (int e = 1; e < 4; e++)
                {
                    if (Flat(c - ShipLayout.Engine(e)) < Flat(c - ShipLayout.Engine(nearest))) nearest = e;
                }
                P3 away = c - ShipLayout.Engine(nearest);
                float d = P3.Dot(n, new P3(away.X, 0f, away.Z).Unit());
                if (d > 0.2f) outward++;
                if (d < -0.2f) inward++;
            }
            Assert.Greater(outward, 100);
            Assert.Greater(inward, 100);
        }

        [Test]
        public void Thrusters_ReachTheirPlace_FromTheHull([Values(0, 1, 2, 3)] int index)
        {
            ShipMesh m = ShipProps.Thruster(index);
            P3 centre = ShipLayout.Thruster(index);
            float nearestAxis = float.MaxValue, furthest = 0f, top = float.MinValue, bottom = float.MaxValue;
            for (int i = 0; i < m.VertexCount; i++)
            {
                P3 p = m.Position(i);
                nearestAxis = Math.Min(nearestAxis, Flat(p));
                furthest = Math.Max(furthest, P3.Distance(p, centre));
                if (P3.Distance(p, centre) < 0.4f) { top = Math.Max(top, p.Y); bottom = Math.Min(bottom, p.Y); }
                Assert.Greater(Flat(p), WallRadius(p.Y) + 0.02f, "a thruster pokes into the cabin");
            }
            Assert.Less(nearestAxis, ShipLayout.CabinRadius + ShipLayout.HullThickness + 0.02f, "the stalk does not reach the hull");
            Assert.Greater(top - centre.Y, 0.25f, "no nozzle pointing up");
            Assert.Greater(centre.Y - bottom, 0.25f, "no nozzle pointing down");
        }

        [Test]
        public void Dish_SitsOnItsMast_WithBothFaces()
        {
            ShipMesh m = Meshes["dish"];
            float nearFoot = float.MaxValue, widest = 0f;
            for (int i = 0; i < m.VertexCount; i++)
            {
                P3 p = m.Position(i);
                nearFoot = Math.Min(nearFoot, P3.Distance(p, ShipLayout.DishMastFoot));
                // Everything well above the mast's foot is the dish itself, its yoke or its feed.
                if (p.Y > ShipLayout.DishMastFoot.Y + 0.6f) widest = Math.Max(widest, P3.Distance(p, ShipLayout.DishCentre));
                Assert.Greater(p.Y, ShipLayout.DishMastFoot.Y, "the dish dips below its own mast foot");
            }
            Assert.Less(nearFoot, 0.15f);
            Assert.That(widest, Is.InRange(ShipLayout.DishRadius - 0.02f, ShipLayout.DishRadius + 0.12f));

            // The bowl shows a face both toward its feed and away from it.
            P3 axis = new P3(-0.25f, 0.8f, -0.55f).Unit();
            int front = 0, back = 0;
            for (int t = 0; t < m.TriangleCount; t++)
            {
                float d = P3.Dot(Normal(m, t).Unit(), axis);
                if (d > 0.7f) front++;
                if (d < -0.7f) back++;
            }
            Assert.Greater(front, 50);
            Assert.Greater(back, 50);
        }

        [Test]
        public void HullDetails_StayOutsideTheHull_AndOffTheWindows()
        {
            ShipMesh m = Meshes["hull_details"];
            ShipLayout.OuterProfile(ShipLayout.WallRows - 1, out float capRadius, out float capHeight);
            float docking = 0f;
            for (int i = 0; i < m.VertexCount; i++)
            {
                P3 p = m.Position(i);
                float r = Flat(p);
                // Never inside the cabin itself.
                Assert.IsTrue(r > WallRadius(p.Y) || p.Y > ShipLayout.CeilingAt(r), "hull detail inside the cabin at " + p);
                if (r < capRadius + 0.02f && p.Y >= capHeight)
                {
                    // Over the roof cap: this is the docking ring.
                    docking = Math.Max(docking, r);
                    continue;
                }
                AgainstHull(p, out float column, out float row, out float through);
                Assert.GreaterOrEqual(through, 1f - 0.02f, "hull detail sunk into the hull at " + p);
                for (int dc = -1; dc <= 1; dc++)
                {
                    for (int dr = -1; dr <= 1; dr++)
                    {
                        int c = (int)Math.Floor(column + dc * 0.05f), w = (int)Math.Floor(row + dr * 0.05f);
                        Assert.IsFalse(ShipLayout.InWindow(c, w), "hull detail over a window at " + p + " (column " + column + ", row " + row + ")");
                    }
                }
            }
            Assert.That(docking, Is.InRange(0.4f, 0.55f + 1e-4f), "docking ring radius");
        }

        [Test]
        public void HullDetails_HaveABayOnTheBack()
        {
            ShipMesh m = Meshes["hull_details"];
            float proud = 0f;
            for (int i = 0; i < m.VertexCount; i++)
            {
                P3 p = m.Position(i);
                if (p.Z < -2f && Math.Abs(p.X) < 0.5f && p.Y < 1.6f) proud = Math.Max(proud, Flat(p));
            }
            Assert.Greater(proud, ShipLayout.CabinRadius + ShipLayout.HullThickness + 0.2f);
        }

        [Test]
        public void Ring_IsABlueGlowingHoopUnderTheBelly()
        {
            ShipMesh m = Meshes["ring"];
            for (int i = 0; i < m.VertexCount; i++)
            {
                P3 p = m.Position(i);
                Assert.That(Flat(p), Is.InRange(ShipLayout.RingRadius - 0.0701f, ShipLayout.RingRadius + 0.0701f));
                Assert.That(p.Y, Is.InRange(ShipLayout.BellyY - 0.04f - 0.0701f, ShipLayout.BellyY - 0.04f + 0.0701f));
                Assert.AreEqual(3f, m.Glow[i]);
                Assert.Greater(m.Color(i).Z, m.Color(i).X);
            }
        }

        // ---------------------------------------------------------------- interior

        [TestCaseSource(nameof(PlacedInterior))]
        public void Interior_StaysInsideTheCabin(string name)
        {
            ShipMesh m = Meshes[name];
            const float slack = 0.005f;
            for (int i = 0; i < m.VertexCount; i++)
            {
                P3 p = m.Position(i);
                float r = Flat(p);
                Assert.LessOrEqual(r, WallRadius(p.Y) + slack, name + " goes through the wall at " + p);
                Assert.LessOrEqual(p.Y, ShipLayout.CeilingPeak, name + " goes through the ceiling at " + p);
                if (name.StartsWith("hatch_door")) Assert.GreaterOrEqual(p.Y, -0.06f - 1e-5f);
                else Assert.GreaterOrEqual(p.Y, -1e-5f, name + " goes through the floor at " + p);
            }
        }

        [Test]
        public void Floor_FacesUp_IsCutSmall_AndHasItsGlowingStrip()
        {
            ShipMesh m = Meshes["floor"];
            bool strip = false;
            for (int i = 0; i < m.VertexCount; i++)
            {
                P3 p = m.Position(i);
                Assert.AreEqual(0f, p.Y, 1e-6f);
                Assert.That(Flat(p), Is.InRange(ShipLayout.ShaftRadius - 1e-4f, ShipLayout.FloorRadius + 1e-4f));
                if (m.Glow[i] > 0f)
                {
                    strip = true;
                    Assert.AreEqual(1.5f, m.Glow[i]);
                    Assert.That(Flat(p), Is.InRange(ShipLayout.ShaftRadius + 0.04f - 1e-4f, ShipLayout.ShaftRadius + 0.1f + 1e-4f));
                }
            }
            Assert.IsTrue(strip);
            double area = 0;
            for (int t = 0; t < m.TriangleCount; t++)
            {
                P3 n = Normal(m, t);
                Assert.Greater(n.Y, 0f, "a floor triangle faces down");
                area += n.Length * 0.5;
            }
            // The whole ring is covered once (the corners sit on circles, so the straight edges lose a little).
            double ring = Math.PI * (ShipLayout.FloorRadius * ShipLayout.FloorRadius - ShipLayout.ShaftRadius * ShipLayout.ShaftRadius);
            Assert.That(area, Is.InRange(ring * 0.99, ring));
            Assert.Less(MaxEdge(m), 0.3f);
            // Two close greys.
            var greys = new HashSet<string>();
            for (int i = 0; i < m.VertexCount; i++)
            {
                if (Flat(m.Position(i)) > ShipLayout.ShaftRadius + 0.11f) greys.Add(m.Color(i).ToString());
            }
            Assert.AreEqual(2, greys.Count);
        }

        [Test]
        public void InteriorSurfaces_AreCutSmallEnoughForBakedLight()
        {
            foreach (string n in PlacedInterior)
            {
                Assert.Less(MaxEdge(Meshes[n]), 0.3f, n + " has an edge too long for per-corner light");
            }
        }

        [Test]
        public void HatchDoors_MeetInTheMiddle_AndFillTheWell([Values(0, 1)] int half)
        {
            ShipMesh m = ShipProps.HatchDoor(half);
            float sign = half == 0 ? -1f : 1f;
            float widest = 0f, top = float.MinValue, bottom = float.MaxValue, nearMiddle = float.MaxValue;
            for (int i = 0; i < m.VertexCount; i++)
            {
                P3 p = m.Position(i);
                Assert.GreaterOrEqual(p.X * sign, -1e-6f, "half " + half + " is on the wrong side");
                widest = Math.Max(widest, Flat(p));
                top = Math.Max(top, p.Y);
                bottom = Math.Min(bottom, p.Y);
                nearMiddle = Math.Min(nearMiddle, Math.Abs(p.X));
            }
            Assert.AreEqual(ShipLayout.ShaftRadius, widest, 1e-4f);
            Assert.AreEqual(0f, top, 1e-6f);
            Assert.AreEqual(-0.06f, bottom, 1e-6f);
            Assert.AreEqual(0f, nearMiddle, 1e-6f);

            double topArea = 0;
            bool yellow = false, black = false;
            for (int t = 0; t < m.TriangleCount; t++)
            {
                P3 c = Centroid(m, t), n = Normal(m, t);
                if (Math.Abs(c.Y) < 1e-5f)
                {
                    Assert.Greater(n.Y, 0f, "top of the hatch should face up");
                    topArea += n.Length * 0.5;
                    if (Math.Abs(c.X) < 0.08f)
                    {
                        P3 colour = m.Color(m.Triangles[t * 3]);
                        if (colour.X > 0.8f && colour.Z < 0.4f) yellow = true;
                        if (colour.X < 0.2f) black = true;
                    }
                }
                else if (Math.Abs(c.Y + 0.06f) < 1e-5f) Assert.Less(n.Y, 0f, "underside of the hatch should face down");
                else if (Math.Abs(c.X) < 1e-5f) Assert.Less(n.X * sign, 0f, "meeting face should face the other half");
                else Assert.Greater(P3.Dot(n, new P3(c.X, 0f, c.Z)), 0f, "rim should face outward");
            }
            double halfDisc = Math.PI * ShipLayout.ShaftRadius * ShipLayout.ShaftRadius * 0.5;
            Assert.That(topArea, Is.InRange(halfDisc * 0.97, halfDisc));
            Assert.IsTrue(yellow && black, "hazard edge needs both yellow and black");
        }

        [Test]
        public void Seat_KeepsClearOfThePilotsEyes_AndTheViewAhead()
        {
            ShipMesh m = Meshes["seat"];
            P3 eye = ShipLayout.SeatedEye;
            float nearest = float.MaxValue;
            for (int i = 0; i < m.VertexCount; i++)
            {
                P3 p = m.Position(i);
                nearest = Math.Min(nearest, P3.Distance(p, eye));
                // Nothing ahead of the eyes stands higher than the desk.
                if (p.Z > eye.Z - 0.1f) Assert.Less(p.Y, 0.8f, "seat blocks the view ahead at " + p);
                // It fits the space the walking rules keep clear for it.
                Assert.LessOrEqual(Flat(p - ShipLayout.SeatPosition), ShipLayout.Obstacles[0].Radius + 1e-3f, "seat is wider than its obstacle circle at " + p);
            }
            for (int t = 0; t < m.TriangleCount; t++) nearest = Math.Min(nearest, P3.Distance(Centroid(m, t), eye));
            TestContext.Out.WriteLine("nearest part of the seat to the eyes: " + nearest.ToString("0.000") + " m");
            Assert.GreaterOrEqual(nearest, 0.35f);

            // It has a headrest behind and above the eyes, and orange cushions.
            bool headrest = false, orange = false;
            for (int i = 0; i < m.VertexCount; i++)
            {
                P3 p = m.Position(i);
                if (p.Y > eye.Y && p.Z < eye.Z) headrest = true;
                if (P3.Distance(m.Color(i), MeshBuilder.Web("#D9772B")) < 1e-3f) orange = true;
            }
            Assert.IsTrue(headrest && orange);
        }

        [Test]
        public void SeatPedestal_FacesOutward()
        {
            ShipMesh m = Meshes["seat"];
            P3 axis = P3.Zero;
            int count = 0;
            for (int i = 0; i < m.VertexCount; i++)
            {
                if (m.Position(i).Y < 0.3f) { axis += m.Position(i); count++; }
            }
            axis *= 1f / count;
            for (int t = 0; t < m.TriangleCount; t++)
            {
                P3 c = Centroid(m, t);
                if (c.Y > 0.3f) continue;
                P3 away = new P3(c.X - axis.X, 0f, c.Z - axis.Z).Unit();
                Assert.Greater(P3.Dot(Normal(m, t).Unit(), away), 0.05f, "pedestal faces inward at " + c);
            }
        }

        [Test]
        public void Desk_StaysBelowTheFrontWindow()
        {
            ShipMesh m = Meshes["desk"];
            float lowTop = 0f;
            for (int i = 0; i < m.VertexCount; i++)
            {
                P3 p = m.Position(i);
                Assert.GreaterOrEqual(p.Z, ShipLayout.DeskZ, "desk comes back past its line at " + p);
                // Nothing on the desk may stand up into the front window, whose sill is at 0.85.
                Assert.LessOrEqual(p.Y, 0.84f, "the desk stands up into the front window at " + p);
                lowTop = Math.Max(lowTop, p.Y);
                // The desk carries no screens of its own any more: every instrument is on the console that stands on it.
                Assert.AreEqual(0f, m.Glow[i], "the desk should not glow");
            }
            Assert.Greater(lowTop, 0.7f);
            TestContext.Out.WriteLine("desk top reaches " + lowTop.ToString("0.000") + " m");
        }

        [Test]
        public void Lockers_StandBehindTheirLine_AndTheirFrontsFaceTheCabin()
        {
            ShipMesh m = Meshes["lockers"];
            for (int i = 0; i < m.VertexCount; i++)
            {
                P3 p = m.Position(i);
                Assert.LessOrEqual(p.Z, ShipLayout.LockerZ, "locker in front of its line at " + p);
                Assert.LessOrEqual(p.Y, 1.5f + 1e-5f);
            }
            int fronts = 0;
            float frontPlane = ShipLayout.LockerZ - 0.08f;
            for (int t = 0; t < m.TriangleCount; t++)
            {
                P3 c = Centroid(m, t);
                // Everything standing proud of the carcass front (doors, latches, vents) and the carcass front itself.
                bool carcassFront = Math.Abs(c.Z - frontPlane) < 1e-4f && c.Y < 1.4f && Math.Abs(c.X) < 1.14f;
                // The latches are little sealed lumps half sunk into the doors; the sealed-piece test covers them.
                bool latch = P3.Distance(m.Color(m.Triangles[t * 3]), MeshBuilder.Web("#D9772B")) < 1e-3f;
                // So are the fittings of the ship's computer, in its bay.
                float bay = 2f * 1.15f / 5f, bayMiddle = -1.15f + bay * (ShipProps.ComputerBay + 0.5f);
                bool computer = Math.Abs(c.X - bayMiddle) < bay * 0.5f && c.Z > frontPlane + 0.03f;
                if ((c.Z > frontPlane + 0.03f && !latch && !computer) || carcassFront)
                {
                    Assert.Greater(Normal(m, t).Unit().Z, -0.05f, "locker front faces the wall at " + c);
                    fronts++;
                }
            }
            Assert.Greater(fronts, 100);
        }

        [Test]
        public void NoLever_StandsInTheCabin_AndTheSeatIsTheOnlyThingToWalkRound()
        {
            // Matthew, 14 Nov 2026: no hatch lever; you look at the hatch and press the button.
            Assert.IsFalse(InteriorNames.Any(n => n.Contains("lever")));
            Assert.IsFalse(ShipAssembly.InteriorPieces().Any(p => p.Name.Contains("Lever")));
            Assert.AreEqual(1, ShipLayout.Obstacles.Length);
        }

        [Test]
        public void Lamps_AreWhereTheLayoutSays_AndGlow()
        {
            ShipMesh ring = Meshes["ceiling_light"];
            int lit = 0;
            for (int i = 0; i < ring.VertexCount; i++)
            {
                P3 p = ring.Position(i);
                if (ring.Glow[i] <= 0f) continue;
                lit++;
                Assert.AreEqual(ShipProps.RingLampGlow, ring.Glow[i]);
                Assert.AreEqual(ShipLayout.CeilingLightRadius, Flat(p), 0.07f);
                Assert.LessOrEqual(p.Y, ShipLayout.CeilingLightY + 1e-4f, "the lamp should shine from the underside");
            }
            Assert.Greater(lit, 0);
            // The housing hangs below the ceiling rather than through it.
            for (int i = 0; i < ring.VertexCount; i++)
            {
                Assert.LessOrEqual(ring.Position(i).Y, ShipLayout.CeilingAt(Flat(ring.Position(i))) + 1e-4f);
            }

            ShipMesh lamps = Meshes["wall_lamps"];
            var perLeg = new int[ShipLayout.Legs];
            for (int i = 0; i < lamps.VertexCount; i++)
            {
                P3 p = lamps.Position(i);
                Assert.That(p.Y, Is.InRange(1.6f, 1.9f));
                if (lamps.Glow[i] <= 0f) continue;
                Assert.AreEqual(ShipProps.WallLampGlow, lamps.Glow[i]);
                for (int k = 0; k < ShipLayout.Legs; k++)
                {
                    double a = ShipLayout.LegAngle(k);
                    if (p.X * Math.Sin(a) + p.Z * Math.Cos(a) > Flat(p) * 0.99f) perLeg[k]++;
                }
            }
            foreach (int n in perLeg) Assert.Greater(n, 0, "a wall lamp is missing");
        }

        [Test]
        public void Comforts_NeverStandWhereYouWalk_OrInTheFrontWindow()
        {
            // You walk inside WalkRadius, between the lockers and the desk; your body is PersonRadius wide and
            // PersonHeight tall. Nothing soft may stand in that space, because nothing there would stop you.
            float reach = ShipLayout.WalkRadius + ShipLayout.PersonRadius;
            foreach (string name in new[] { "comforts", "fairy_lights", "hardware" })
            {
                ShipMesh m = Meshes[name];
                for (int i = 0; i < m.VertexCount; i++)
                {
                    P3 p = m.Position(i);
                    bool rug = p.Y < 0.02f;
                    bool behindTheLockers = p.Z <= ShipLayout.LockerZ + 0.02f, pastTheDesk = p.Z >= ShipLayout.DeskZ;
                    bool overhead = p.Y > ShipLayout.PersonHeight + 0.03f;
                    // Where the dome leans in over your head, a rib or a pipe that hugs it is part of the roof.
                    bool hugsTheDome = name == "hardware" && p.Y > ShipLayout.WallTop && WallRadius(p.Y) - Flat(p) < 0.1f;
                    if (!rug && !behindTheLockers && !pastTheDesk && !overhead && !hugsTheDome)
                        Assert.GreaterOrEqual(Flat(p), reach, name + " stands where you can walk at " + p);
                    // The front window's sill is at 0.85: nothing on the nose may stand up into the view
                    // (the roof is another matter: see Hardware_LeavesTheFrontAndRoofWindowsClear_SeenFromTheSeat).
                    if (pastTheDesk && !overhead) Assert.LessOrEqual(p.Y, 0.84f, name + " stands up into the front window at " + p);
                }
            }
        }

        [Test]
        public void Hardware_LeavesTheFrontAndRoofWindowsClear_SeenFromTheSeat()
        {
            // Look from the pilot's eyes through every corner of the hardware: the line must never go on to
            // meet the glass of the front window or the roof window.
            ShipMesh m = Meshes["hardware"];
            for (int i = 0; i < m.VertexCount; i++)
            {
                P3 p = m.Position(i);
                Assert.IsTrue(ShipLayout.CabinExit(ShipLayout.SeatedEye, p - ShipLayout.SeatedEye, out _, out _, out ShipLayout.CabinExitKind kind, out int window));
                if (kind != ShipLayout.CabinExitKind.Window) continue;
                string id = ShipLayout.Windows[window].Id;
                Assert.IsFalse(id == "window_front" || id == "window_overhead", "hardware is in front of the " + ShipLayout.Windows[window].Name + " at " + p);
            }
        }

        [Test]
        public void Hardware_HasItsRibs_ItsOrrery_AndOnlyGlowsThatLightNothing()
        {
            ShipMesh m = Meshes["hardware"];
            float lowestOverTheWell = float.MaxValue;
            int sun = 0, lights = 0, crown = 0;
            for (int i = 0; i < m.VertexCount; i++)
            {
                P3 p = m.Position(i);
                Assert.Less(m.Glow[i], LightBake.CastsLightFrom, "hardware must not be a lamp to the baker at " + p);
                // Over the lift well and the middle of the floor nothing may hang down to head height.
                if (Flat(p) < ShipLayout.ShaftRadius) lowestOverTheWell = Math.Min(lowestOverTheWell, p.Y);
                if (m.Glow[i] > 0f && Flat(p) < 0.06f) sun++;
                else if (m.Glow[i] > 0f) lights++;
                if (p.Y > 2.5f && Flat(p) > 0.45f) crown++;
            }
            Assert.Greater(lowestOverTheWell, ShipLayout.PersonHeight + 0.2f, "the orrery hangs too low over the lift");
            Assert.Greater(sun, 20, "the orrery's sun");
            Assert.Greater(lights, 40, "pilot lights on the roof panels and the gauge");
            Assert.Greater(crown, 50, "the crown ring the ribs run up to");
            foreach (float column in ShipProps.RibColumns)
            {
                for (int w = 0; w < ShipLayout.Windows.Length; w++)
                {
                    float from = ShipLayout.Windows[w].Column, to = from + ShipLayout.Windows[w].Columns;
                    float c = column < from ? column + ShipLayout.WallColumns : column;
                    Assert.IsFalse(c > from - 0.3f && c < to + 0.3f, "a rib runs across the " + ShipLayout.Windows[w].Name);
                }
            }
        }

        [Test]
        public void FairyLights_GlowRoundTheDome_AndLeaveTheFrontWindowClear()
        {
            ShipMesh m = Meshes["fairy_lights"];
            int lit = 0;
            for (int i = 0; i < m.VertexCount; i++)
            {
                P3 p = m.Position(i);
                double fromNose = Math.Abs(Math.Atan2(p.X, p.Z)) * 180.0 / Math.PI;
                Assert.GreaterOrEqual(fromNose, 50.0, "the string of lights crosses the front window at " + p);
                Assert.That(p.Y, Is.InRange(ShipProps.FairyHookY - ShipProps.FairySag - 0.08f, ShipProps.FairyHookY + 0.03f));
                if (m.Glow[i] <= 0f) continue;
                Assert.AreEqual(ShipProps.FairyGlow, m.Glow[i]);
                lit++;
            }
            Assert.Greater(lit, 300, "there should be dozens of bulbs");
        }

        [Test]
        public void Comforts_HaveTheirLantern_TheirRug_AndStarsOnTheCeiling()
        {
            ShipMesh m = Meshes["comforts"];
            int lantern = 0, stars = 0, rug = 0;
            for (int i = 0; i < m.VertexCount; i++)
            {
                P3 p = m.Position(i);
                if (p.Y < 0.02f) { rug++; Assert.Less(p.Z, 0f, "the rug lies behind the lift well, clear of the hatch doors' slide"); }
                if (m.Glow[i] == ShipProps.LanternGlow) { lantern++; Assert.Less(p.Z, ShipLayout.LockerZ, "the lantern stands on the lockers"); }
                else if (m.Glow[i] > 0f && p.Y > 2f) stars++;
            }
            Assert.Greater(lantern, 0);
            Assert.Greater(rug, 100);
            Assert.GreaterOrEqual(stars / 8, 20, "at least twenty stars (eight corners each)");
        }

        [Test]
        public void Belt_IsTheStatedSize()
        {
            ShipMesh strap = Meshes["belt_strap"];
            float[] lo = { float.MaxValue, float.MaxValue, float.MaxValue }, hi = { float.MinValue, float.MinValue, float.MinValue };
            for (int i = 0; i < strap.VertexCount; i++)
            {
                for (int k = 0; k < 3; k++)
                {
                    lo[k] = Math.Min(lo[k], strap.Positions[i * 3 + k]);
                    hi[k] = Math.Max(hi[k], strap.Positions[i * 3 + k]);
                }
            }
            Assert.AreEqual(0f, lo[0], 1e-6f);
            Assert.AreEqual(1f, hi[0], 1e-6f);
            Assert.AreEqual(0.008f, hi[1] - lo[1], 1e-6f);
            Assert.AreEqual(0.05f, hi[2] - lo[2], 1e-6f);

            ShipMesh buckle = Meshes["belt_buckle"];
            float widest = 0f;
            for (int i = 0; i < buckle.VertexCount; i++) widest = Math.Max(widest, Math.Abs(buckle.Position(i).X));
            Assert.AreEqual(0.035f, widest, 0.003f);
        }
    }
}
