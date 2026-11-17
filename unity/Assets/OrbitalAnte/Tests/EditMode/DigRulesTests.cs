using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.Player;
using OrbitalAnte.Space;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>
    /// Diggable ground (Matthew, 17 Nov 2026: shovel holes, shoot holes, "cave around your enemies"): the lattice of
    /// boxes, the dug ground's shape and how it meets the ordinary ground tiles and the cave walls, the light down a
    /// tunnel, and the limits on what can be dug.
    /// </summary>
    public class DigRulesTests
    {
        static Dictionary<string, BodySpec> _bodies;
        static List<PlanetProfile> _profiles;
        static readonly Dictionary<string, PlanetTerrain> Worlds = new Dictionary<string, PlanetTerrain>();
        const double FinestCell = 1.7;   // PlanetSettings.finestCell's default

        [OneTimeSetUp]
        public void Load()
        {
            _profiles = PlanetProfiles.Defaults();
            _bodies = new Dictionary<string, BodySpec>();
            foreach (BodySpec b in SolarSystemDefaults.Bodies()) _bodies[b.id] = b;
        }

        static PlanetTerrain World(string id)
        {
            if (Worlds.TryGetValue(id, out PlanetTerrain t)) return t;
            PlanetProfile p = _profiles.Find(x => x.bodyId == id);
            t = new PlanetTerrain(p, id, _bodies[id].radius, 4242);
            Worlds[id] = t;
            return t;
        }

        static int Level(PlanetTerrain t) => TileRules.MaxLevel(t.Radius, FinestCell);

        static DigGround Ground(PlanetTerrain t) => new DigGround(t, Level(t));

        /// <summary>A direction on dry land, well clear of the old single caves.</summary>
        static Vec3d Land(PlanetTerrain t, int pick = 0)
        {
            var rng = new Random(77 + pick);
            for (int tries = 0; tries < 4000; tries++)
            {
                Vec3d dir = new Vec3d(rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1);
                if (dir.SqrMagnitude < 0.05) continue;
                dir = dir.Normalized;
                double h = t.Height(dir);
                if (t.HasSea && h < t.SeaLevel + 30) continue;
                var probe = new DigCarve(dir * (t.Radius + h), 1.0);
                if (!DigRules.Fit(t, 0, ref probe)) continue;
                if (t.SlopeAt(dir) > 0.5) continue;
                return dir;
            }
            Assert.Fail("no dry land found");
            return Vec3d.Up;
        }

        static List<DigChunkData> BuildAll(DigField field, DigGround ground, IList<CaveNetwork> nets = null)
        {
            var list = new List<DigChunkData>();
            foreach (long key in field.Chunks) list.Add(DigMesher.Build(key, field.CarvesNear(key, 9), ground, nets));
            return list;
        }

        static Vec3d P(DigChunkData d, int v) => new Vec3d(d.Positions[v * 3], d.Positions[v * 3 + 1], d.Positions[v * 3 + 2]);
        static Vec3d N(DigChunkData d, int v) => new Vec3d(d.Normals[v * 3], d.Normals[v * 3 + 1], d.Normals[v * 3 + 2]);

        // ------------------------------------------------------------------ the lattice

        [Test]
        public void ChunkKeys_RoundTrip_EvenBelowZero()
        {
            int[] values = { 0, 1, -1, 77, -77, 900, -900, 100000, -100000 };
            foreach (int x in values)
                foreach (int y in values)
                    foreach (int z in values)
                    {
                        DigRules.Unpack(DigRules.Key(x, y, z), out int a, out int b, out int c);
                        Assert.AreEqual((x, y, z), (a, b, c));
                    }
            Vec3d p = new Vec3d(-0.01, 6.41, -12.8);
            DigRules.Unpack(DigRules.KeyOf(p), out int kx, out int ky, out int kz);
            Assert.AreEqual((-1, 1, -2), (kx, ky, kz));
        }

        [Test]
        public void AScoop_PutsBoxesAllRoundIt_FarEnoughForTheTilesSquares()
        {
            var field = new DigField();
            var added = new List<long>();
            var carve = new DigCarve(new Vec3d(3.1, 500.2, -1.7), 0.7);
            Assert.IsTrue(field.TryCarve(carve, null, added));
            Assert.Greater(added.Count, 0);
            var cover = new DigCover(field.Chunks, 1);
            // Every point within the margin of the scoop is inside the boxes.
            var rng = new Random(3);
            for (int i = 0; i < 2000; i++)
            {
                Vec3d dir = new Vec3d(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5).Normalized;
                Vec3d p = carve.Centre + dir * ((carve.Radius + DigRules.Margin - DigRules.Clearance - 0.01) * rng.NextDouble());
                Assert.IsTrue(cover.Covers(p, DigRules.Clearance), "a point near the scoop is not covered: " + p);
            }
            // Far away, nothing is.
            Assert.IsFalse(cover.Covers(carve.Centre + new Vec3d(30, 0, 0), DigRules.Clearance));
            // The margin is wide enough that a tile's square (1.7 m) with all four corners left standing never reaches the scoop.
            Assert.Greater(DigRules.Margin, DigRules.Clearance + FinestCell * Math.Sqrt(2) + 0.2);
        }

        [Test]
        public void Cover_NeedsTheNeighbour_NearABoxsFace()
        {
            long a = DigRules.Key(0, 0, 0), b = DigRules.Key(1, 0, 0);
            var one = new DigCover(new[] { a }, 1);
            var two = new DigCover(new[] { a, b }, 2);
            Vec3d nearFace = new Vec3d(DigRules.ChunkSize - 0.1, 3, 3);
            Assert.IsFalse(one.Covers(nearFace, 0.3));
            Assert.IsTrue(two.Covers(nearFace, 0.3));
            Assert.IsTrue(one.Covers(new Vec3d(3, 3, 3), 0.3));
            Assert.IsFalse(DigCover.None.Covers(new Vec3d(3, 3, 3), 0.3));
        }

        [Test]
        public void AScoopInsideAnother_ChangesNothing_AndIsRefused()
        {
            var field = new DigField();
            Assert.IsTrue(field.TryCarve(new DigCarve(new Vec3d(0, 500, 0), 1.0), null, null));
            Assert.IsFalse(field.TryCarve(new DigCarve(new Vec3d(0.2, 500, 0), 0.5), null, null));
            Assert.IsTrue(field.TryCarve(new DigCarve(new Vec3d(0.9, 500, 0), 0.5), null, null));
            Assert.AreEqual(2, field.CarveCount);
            Assert.IsTrue(field.InDugAir(new Vec3d(0, 500.5, 0)));
            Assert.IsFalse(field.InDugAir(new Vec3d(0, 502, 0)));
            Assert.IsTrue(field.InDugAir(new Vec3d(0, 502, 0), 1.1));
        }

        [Test]
        public void ABodyCanOnlyBeDugSoMuch()
        {
            var field = new DigField();
            int taken = 0;
            for (int i = 0; i < 2000; i++)
                if (field.TryCarve(new DigCarve(new Vec3d(i * 3.0, 500, 0), 0.5), null, null)) taken++;
            Assert.LessOrEqual(field.ChunkCount, DigRules.MaxChunks);
            Assert.Less(taken, 2000, "boxes must run out before this many scoops far apart");
        }

        [Test]
        public void CarvesNear_HoldsEveryScoopThatReachesTheBox()
        {
            var field = new DigField();
            var rng = new Random(9);
            for (int i = 0; i < 300; i++)
                field.TryCarve(new DigCarve(new Vec3d(rng.NextDouble() * 30, 500 + rng.NextDouble() * 4, rng.NextDouble() * 30), 0.3 + rng.NextDouble()), null, null);
            foreach (long key in field.Chunks)
            {
                DigCarve[] near = field.CarvesNear(key, 9);
                Vec3d min = DigRules.ChunkMin(key) - new Vec3d(9, 9, 9);
                Vec3d max = DigRules.ChunkMin(key) + new Vec3d(DigRules.ChunkSize + 9, DigRules.ChunkSize + 9, DigRules.ChunkSize + 9);
                int expected = 0;
                for (int i = 0; i < field.CarveCount; i++)
                {
                    DigCarve c = field.CarveAt(i);
                    double dx = Math.Max(Math.Max(min.X - c.Centre.X, 0), c.Centre.X - max.X);
                    double dy = Math.Max(Math.Max(min.Y - c.Centre.Y, 0), c.Centre.Y - max.Y);
                    double dz = Math.Max(Math.Max(min.Z - c.Centre.Z, 0), c.Centre.Z - max.Z);
                    if (dx * dx + dy * dy + dz * dz <= c.Radius * c.Radius) expected++;
                }
                Assert.AreEqual(expected, near.Length);
            }
        }

        // ------------------------------------------------------------------ what may be dug

        [Test]
        public void Scoops_StayOutOfTheSea_AndAboveTheSafetyNet_AndShallowEnough()
        {
            PlanetTerrain earth = World("Earth");
            Vec3d dir = Land(earth);
            double ground = earth.Radius + earth.Height(dir);
            var deep = new DigCarve(dir * (ground - DigRules.MaxDepth - 2), 1.0);
            Assert.IsFalse(DigRules.Fit(earth, 0, ref deep), "too deep");
            var nearLimit = new DigCarve(dir * (ground - DigRules.MaxDepth + 0.5), 1.0);
            Assert.IsTrue(DigRules.Fit(earth, 0, ref nearLimit));
            Assert.Less(nearLimit.Radius, 1.0, "a scoop just over the limit is shrunk to fit");
            var overNet = new DigCarve(dir * (ground - 2), 1.0);
            Assert.IsFalse(DigRules.Fit(earth, ground - 1.5, ref overNet), "never through the safety net");

            // A spot whose ground is near the sea: never dug below it.
            Assert.IsTrue(earth.HasSea);
            var rng = new Random(5);
            for (int i = 0; i < 4000; i++)
            {
                Vec3d d = new Vec3d(rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1).Normalized;
                double h = earth.Height(d);
                if (h < earth.SeaLevel + 0.3 || h > earth.SeaLevel + 1.5) continue;
                var c = new DigCarve(d * (earth.Radius + h), 1.2);
                if (DigRules.Fit(earth, 0, ref c))
                    Assert.GreaterOrEqual(c.Centre.Magnitude - c.Radius, earth.Radius + earth.SeaLevel + DigRules.AboveSea - 1e-6);
            }
        }

        [Test]
        public void HoleSizes_AreSensible()
        {
            Assert.AreEqual(DigRules.SmallestBullet, DigRules.BulletRadius(0));
            Assert.AreEqual(DigRules.BiggestBullet, DigRules.BulletRadius(10000));
            Assert.Less(DigRules.BulletRadius(20), DigRules.BulletRadius(90));
            Assert.Greater(DigRules.ShovelRadius, DigRules.BiggestBullet, "a shovel bite is more than any bullet");
            DigCarve blast = DigRules.Blast(new Vec3d(0, 500, 0), 3);
            Assert.Less(blast.Centre.Magnitude, 500, "a blast's bowl is centred under the point");
            Assert.AreEqual(DigRules.BiggestBlast, DigRules.Blast(new Vec3d(0, 500, 0), 100).Radius);
        }

        // ------------------------------------------------------------------ the shape

        [Test]
        public void DigGround_IsTheTilesOwnSurface()
        {
            foreach (string id in new[] { "Earth", "Mars", "Moon" })
            {
                PlanetTerrain t = World(id);
                int level = Level(t);
                var ground = new DigGround(t, level);
                Vec3d dir = Land(t);
                CubeSphere.Locate(dir, out int face, out double u, out double v);
                int side = 1 << level;
                int ix = Math.Min(side - 1, (int)((u + 1) * 0.5 * side)), iy = Math.Min(side - 1, (int)((v + 1) * 0.5 * side));
                ChunkData tile = TerrainMesher.Build(t, face, level, ix, iy, false);
                var rng = new Random(1);
                double worst = 0;
                for (int tri = 0; tri < 600; tri++)
                {
                    int k = rng.Next(TerrainMesher.Quads * TerrainMesher.Quads * 2) * 3;
                    Vec3d a = V(tile, tile.Triangles[k]), b = V(tile, tile.Triangles[k + 1]), c = V(tile, tile.Triangles[k + 2]);
                    double s1 = rng.NextDouble(), s2 = rng.NextDouble();
                    if (s1 + s2 > 1) { s1 = 1 - s1; s2 = 1 - s2; }
                    Vec3d p = a + (b - a) * s1 + (c - a) * s2;
                    worst = Math.Max(worst, Math.Abs(ground.RadiusAt(p.Normalized) - p.Magnitude));
                }
                Assert.Less(worst, 0.02, id + ": dug ground's idea of the surface is off the tile by " + worst.ToString("0.000") + " m");
            }
        }

        static Vec3d V(ChunkData c, int i) => new Vec3d(c.Positions[i * 3], c.Positions[i * 3 + 1], c.Positions[i * 3 + 2]);

        [Test]
        public void UntouchedDugGround_LiesJustUnderTheTile()
        {
            PlanetTerrain t = World("Mars");
            DigGround ground = Ground(t);
            Vec3d dir = Land(t, 1);
            Vec3d top = dir * ground.RadiusAt(dir);
            var field = new DigField();
            field.TryCarve(new DigCarve(top, 0.5), null, null);
            int checkedCorners = 0;
            foreach (DigChunkData d in BuildAll(field, ground))
            {
                for (int v = 0; v < d.VertexCount; v++)
                {
                    Vec3d p = P(d, v);
                    if (field.InDugAir(p, 0.8)) continue;
                    double below = ground.RadiusAt(p.Normalized) - p.Magnitude;
                    Assert.That(below, Is.InRange(0.0, DigRules.Recess + 0.06), "dug ground away from the scoop must sit just under the tile");
                    checkedCorners++;
                }
            }
            Assert.Greater(checkedCorners, 200);
        }

        [Test]
        public void AShovelBite_DigsAHole()
        {
            PlanetTerrain t = World("Moon");
            DigGround ground = Ground(t);
            Vec3d dir = Land(t);
            Vec3d top = dir * ground.RadiusAt(dir);
            var field = new DigField();
            Vec3d down = -dir;
            DigCarve bite = DigRules.Shovel(top, down);
            Assert.IsTrue(field.TryCarve(bite, null, null));
            double deepest = 0;
            foreach (DigChunkData d in BuildAll(field, ground))
                for (int v = 0; v < d.VertexCount; v++)
                    deepest = Math.Max(deepest, ground.RadiusAt(P(d, v).Normalized) - P(d, v).Magnitude);
            Assert.Greater(deepest, 0.7, "one bite should leave a hole most of a metre deep");
            // Dig straight down for a while: a shaft.
            Vec3d at = top;
            for (int i = 0; i < 12; i++)
            {
                at -= dir * 0.6;
                field.TryCarve(DigRules.Shovel(at, down), null, null);
            }
            deepest = 0;
            foreach (DigChunkData d in BuildAll(field, ground))
                for (int v = 0; v < d.VertexCount; v++)
                    deepest = Math.Max(deepest, ground.RadiusAt(P(d, v).Normalized) - P(d, v).Magnitude);
            Assert.Greater(deepest, 7.5);
        }

        [Test]
        public void DugGround_IsSealed_AndFacesTheAir_AcrossBoxes()
        {
            PlanetTerrain t = World("Earth");
            DigGround ground = Ground(t);
            Vec3d dir = Land(t, 2);
            Vec3d top = dir * ground.RadiusAt(dir);
            Vec3d east = Vec3d.Cross(dir, Vec3d.Up).Normalized;
            var field = new DigField();
            // A crater and a sloping tunnel running off sideways, through several boxes.
            field.TryCarve(DigRules.Blast(top, 2.5), null, null);
            for (int i = 0; i < 14; i++) field.TryCarve(new DigCarve(top - dir * (1.2 + i * 0.25) + east * (i * 0.6), 0.9), null, null);
            List<DigChunkData> chunks = BuildAll(field, ground);

            // Every edge is shared by two triangles (or four, at a pinch), except on the outside of the boxes.
            var edges = new Dictionary<((long, long, long), (long, long, long)), int>();
            var cover = new DigCover(field.Chunks, 1);
            int triangles = 0, wrongWay = 0;
            foreach (DigChunkData d in chunks)
            {
                if (d.Empty) continue;
                for (int k = 0; k < d.Triangles.Length; k += 3)
                {
                    Vec3d a = P(d, d.Triangles[k]), b = P(d, d.Triangles[k + 1]), c = P(d, d.Triangles[k + 2]);
                    Count(edges, Q(a), Q(b)); Count(edges, Q(b), Q(c)); Count(edges, Q(c), Q(a));
                    // The triangle's own turn agrees with the corners' normals: it faces the air.
                    Vec3d face = Vec3d.Cross(b - a, c - a);
                    Vec3d normal = N(d, d.Triangles[k]) + N(d, d.Triangles[k + 1]) + N(d, d.Triangles[k + 2]);
                    if (face.SqrMagnitude > 1e-10 && Vec3d.Dot(face, normal) <= 0) wrongWay++;
                    triangles++;
                }
            }
            Assert.Greater(triangles, 1000);
            // A few slivers in sharp creases can lean past their corners' normals; the turn itself must be right.
            Assert.LessOrEqual(wrongWay, triangles / 200, "triangles must face the air (and their corners' normals)");
            int open = 0;
            var why = new System.Text.StringBuilder();
            foreach (var e in edges)
            {
                // Two sheets can pinch together where rock thinner than a cell is left (an edge shared four times):
                // still closed. An odd count is a crack.
                if (e.Value % 2 == 0) continue;
                var q = e.Key.Item1;
                Vec3d p = new Vec3d(q.Item1 / 1000.0, q.Item2 / 1000.0, q.Item3 / 1000.0);
                // Open edges are allowed only at the outside of the boxes.
                if (cover.Covers(p, DigRules.Cell * 1.5))
                {
                    open++;
                    Vec3d local = p - DigRules.ChunkMin(DigRules.KeyOf(p));
                    why.AppendLine("open edge at " + p + " local " + local + " count " + e.Value + " in air " + field.InDugAir(p) + " below " + (ground.RadiusAt(p.Normalized) - p.Magnitude).ToString("0.000"));
                }
            }
            Assert.AreEqual(0, open, "dug ground has a crack inside its boxes\n" + why);
        }

        // Snapped to a millimetre (and welded to a neighbour already seen), so two boxes' copies of a corner meet.
        static readonly HashSet<(long, long, long)> Seen = new HashSet<(long, long, long)>();
        static (long, long, long) Q(Vec3d p)
        {
            var k = ((long)Math.Round(p.X * 1000), (long)Math.Round(p.Y * 1000), (long)Math.Round(p.Z * 1000));
            if (Seen.Contains(k)) return k;
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                    {
                        var n = (k.Item1 + x, k.Item2 + y, k.Item3 + z);
                        if (Seen.Contains(n)) return n;
                    }
            Seen.Add(k);
            return k;
        }

        static void Count(Dictionary<((long, long, long), (long, long, long)), int> edges, (long, long, long) a, (long, long, long) b)
        {
            var key = a.CompareTo(b) < 0 ? (a, b) : (b, a);
            edges.TryGetValue(key, out int n);
            edges[key] = n + 1;
        }

        [Test]
        public void TheTile_LeavesOutItsSquaresOverTheHole_AndOnlyThere()
        {
            PlanetTerrain t = World("Mars");
            int level = Level(t);
            DigGround ground = new DigGround(t, level);
            Vec3d dir = Land(t, 3);
            Vec3d top = dir * ground.RadiusAt(dir);
            var field = new DigField();
            field.TryCarve(DigRules.Blast(top, 2.2), null, null);
            field.TryCarve(DigRules.Shovel(top + Vec3d.Cross(dir, Vec3d.Right).Normalized * 2.5, -dir), null, null);
            var cover = new DigCover(field.Chunks, 1);
            CubeSphere.Locate(dir, out int face, out double u, out double v);
            int side = 1 << level;
            int ix = Math.Min(side - 1, (int)((u + 1) * 0.5 * side)), iy = Math.Min(side - 1, (int)((v + 1) * 0.5 * side));
            ChunkData plain = TerrainMesher.Build(t, face, level, ix, iy, false);
            ChunkData dug = TerrainMesher.Build(t, face, level, ix, iy, false, cover);
            Assert.IsTrue(dug.DugOut);
            Assert.Less(dug.Triangles.Length, plain.Triangles.Length);
            // Nothing left of the tile over a scoop, curtains included.
            for (int k = 0; k < dug.Triangles.Length; k += 3)
            {
                Vec3d a = V(dug, dug.Triangles[k]), b = V(dug, dug.Triangles[k + 1]), c = V(dug, dug.Triangles[k + 2]);
                for (int s = 0; s <= 6; s++)
                    for (int r = 0; r + s <= 6; r++)
                    {
                        Vec3d p = a + (b - a) * (s / 6.0) + (c - a) * (r / 6.0);
                        Assert.IsFalse(field.InDugAir(p, 0.05), "a tile face is left standing over a hole");
                    }
            }
            // Away from the hole the tile is untouched.
            ChunkData far = TerrainMesher.Build(t, face, level, ix, iy, false, new DigCover(new long[0], 2));
            Assert.AreEqual(plain.Triangles.Length, far.Triangles.Length);
        }

        [Test]
        public void ATunnel_IsDark_AndOpenGround_SeesTheSky()
        {
            PlanetTerrain t = World("Moon");
            DigGround ground = Ground(t);
            Vec3d dir = Land(t, 4);
            Vec3d top = dir * ground.RadiusAt(dir);
            Vec3d east = Vec3d.Cross(dir, Vec3d.Up).Normalized;
            var carves = new List<DigCarve>();
            // A tunnel 4 m down running sideways.
            for (int i = 0; i < 20; i++) carves.Add(new DigCarve(top - dir * 4 + east * (i * 0.5), 1.0));
            DigCarve[] all = carves.ToArray();
            Vec3d floor = top - dir * 4.9 + east * 5;
            Assert.Less(DigMesher.SkyReachForTests(all, ground, floor, dir), 0.25, "the floor of a covered tunnel should be dark");
            Vec3d away = (top + east * 30).Normalized;
            Assert.AreEqual(1.0, DigMesher.SkyReachForTests(all, ground, away * ground.RadiusAt(away), away), 1e-9);
            // A shallow pit open to the sky is mostly lit.
            Vec3d there = (top + east * 40).Normalized;
            Vec3d pitTop = there * ground.RadiusAt(there);
            var pit = new[] { DigRules.Blast(pitTop, 2.0) };
            Vec3d pitFloor = pitTop - there * 1.0;
            Assert.Greater(DigMesher.SkyReachForTests(pit, ground, pitFloor, there), 0.55);
        }

        [Test]
        public void DiggingIntoACave_OpensIt_AndTheCaveWallsMakeWay()
        {
            PlanetTerrain t = World("Moon");
            DigGround ground = Ground(t);
            CaveNetwork net = null;
            foreach (Cave c in t.Caves) if (c.Network != null) { net = c.Network; break; }
            Assume.That(net, Is.Not.Null, "the Moon should have a lava tube network for seed 4242");
            // A point in the cave's air, well under the ground: dig a scoop that reaches it from the side.
            Vec3d inside = Vec3d.Zero;
            bool found = false;
            foreach (CaveChamber room in net.Chambers)
            {
                double r = room.Centre.Magnitude;
                if (ground.RadiusAt(room.Centre / r) - r > 6 && net.Air(room.Centre) < -1) { inside = room.Centre; found = true; break; }
            }
            Assume.That(found, "a room deep enough");
            var field = new DigField();
            Vec3d side = inside.AnyPerpendicular().Normalized;
            // A short tunnel that ends in the room.
            for (int i = 0; i < 8; i++) field.TryCarve(new DigCarve(inside + side * (2 + i * 0.6), 1.0), null, null);
            var cover = new DigCover(field.Chunks, 1);
            var nets = new List<CaveNetwork> { net };
            int caveColoured = 0, faces = 0;
            foreach (DigChunkData d in BuildAll(field, ground, nets))
            {
                if (d.Empty) continue;
                faces += d.Triangles.Length / 3;
                for (int v = 0; v < d.VertexCount; v++)
                {
                    Vec3d p = P(d, v);
                    // No dug ground stands in the cave's air.
                    Assert.Greater(net.Air(p), -0.2, "dug ground drawn inside the cave's air");
                    if (Math.Abs(net.Air(p)) < 0.3) caveColoured++;
                }
            }
            Assert.Greater(faces, 100);
            Assert.Greater(caveColoured, 10, "the dug ground should draw the cave's walls inside its boxes");
            // The cave's own blocks leave their walls inside the boxes to the dug ground.
            List<CaveChunk> blocks = CaveNetworkMesher.Build(net, t, cover);
            foreach (CaveChunk block in blocks)
            {
                float[] pos = block.Shape.Positions;
                int[] tris = block.Shape.Triangles;
                for (int k = 0; k < tris.Length; k += 3)
                {
                    Vec3d middle = Vec3d.Zero;
                    for (int q = 0; q < 3; q++)
                    {
                        int i = tris[k + q] * 3;
                        middle += new Vec3d(pos[i], pos[i + 1], pos[i + 2]) / 3.0;
                    }
                    Assert.IsFalse(cover.Covers(middle, 1.2), "a cave wall is still drawn deep inside dug ground");
                }
                // And one block rebuilt by itself is the same as in the whole build.
                CaveChunk again = CaveNetworkMesher.BuildBlockAt(net, t, block.Bx, block.By, block.Bz, cover);
                Assert.IsNotNull(again);
                Assert.AreEqual(block.Shape.Triangles.Length, again.Shape.Triangles.Length);
            }
        }

        [Test]
        public void OneBox_IsQuickToBuild()
        {
            PlanetTerrain t = World("Earth");
            DigGround ground = Ground(t);
            Vec3d dir = Land(t, 5);
            Vec3d top = dir * ground.RadiusAt(dir);
            var field = new DigField();
            field.TryCarve(DigRules.Blast(top, 2.5), null, null);
            var rng = new Random(2);
            for (int i = 0; i < 200; i++)
            {
                Vec3d off = new Vec3d(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5) * 6;
                field.TryCarve(new DigCarve(top + off - dir * 0.5, DigRules.BulletRadius(30)), null, null);
            }
            long key = DigRules.KeyOf(top);
            DigMesher.Build(key, field.CarvesNear(key, 9), ground, null);   // warm up
            var watch = Stopwatch.StartNew();
            const int runs = 5;
            for (int i = 0; i < runs; i++) DigMesher.Build(key, field.CarvesNear(key, 9), new DigGround(t, Level(t)), null);
            double ms = watch.Elapsed.TotalMilliseconds / runs;
            TestContext.WriteLine("one box of dug ground: " + ms.ToString("0.0") + " ms");
            Assert.Less(ms, 60, "one box is built on a background thread, but must still be quick");
        }

        // ------------------------------------------------------------------ the shovel

        [Test]
        public void TheShovel_BitesOncePerSwing_AndKeepsGoingWhileHeld()
        {
            var swing = new ShovelSwing();
            int bites = 0;
            for (int i = 0; i < 120; i++) if (swing.Tick(1f / 60f, true)) bites++;   // two seconds held
            int expected = (int)(2f / swing.Length);
            Assert.That(bites, Is.InRange(expected - 1, expected + 1));
            // Let go: it finishes the swing and stops.
            for (int i = 0; i < 60; i++) swing.Tick(1f / 60f, false);
            Assert.IsFalse(swing.Busy);
            bites = 0;
            for (int i = 0; i < 60; i++) if (swing.Tick(1f / 60f, false)) bites++;
            Assert.AreEqual(0, bites);
            // One tap, one bite.
            Assert.IsFalse(swing.Tick(1f / 60f, true));
            for (int i = 0; i < 60; i++) if (swing.Tick(1f / 60f, false)) bites++;
            Assert.AreEqual(1, bites);
            // A long frame never skips the bite.
            swing.Tick(0.001f, true);
            Assert.IsTrue(swing.Tick(1f, false));
        }

        [Test]
        public void DiggingSideways_MakesATunnelYouCanWalkDown()
        {
            Vec3d up = new Vec3d(0, 1, 0);
            Vec3d hit = up * 500;
            DigCarve[] down = DigRules.ShovelBites(hit, -up);
            Assert.AreEqual(1, down.Length);
            DigCarve[] ahead = DigRules.ShovelBites(hit, new Vec3d(1, 0, 0));
            Assert.AreEqual(2, ahead.Length);
            double top = ahead[0].Centre.Magnitude + ahead[0].Radius;
            double bottom = ahead[1].Centre.Magnitude - ahead[1].Radius;
            // Taller than someone standing (1.8 m) and wide enough to turn round crouched.
            Assert.Greater(top - bottom, 1.95);
            Assert.Greater(ahead[0].Radius * 2, 1.3);
        }

        [Test]
        public void ATunnelDugAhead_KeepsItsFloorLevelWithYourFeet()
        {
            // Flat enough to call level: a big world, a few metres of tunnel.
            const double R = 6.0e6;
            Vec3d feet = new Vec3d(0, R, 0);
            Vec3d ahead = new Vec3d(1, 0, 0);
            for (int i = 0; i < 15; i++)
            {
                Vec3d eye = feet + new Vec3d(0, 1.62, 0);
                Vec3d hit = eye + ahead * 0.9;   // the face of the tunnel, a little in front of the eye
                DigCarve[] bites = DigRules.ShovelBites(hit, ahead, feet);
                Assert.AreEqual(2, bites.Length);
                DigCarve low = bites[1];
                double floor = low.Centre.Y - low.Radius;
                Assert.AreEqual(R, floor, 0.2, "bite " + i + ": the floor climbs or sinks");
                Assert.Greater(bites[0].Centre.Y + bites[0].Radius - floor, 1.8 + 0.25, "room over a standing head");
                feet = new Vec3d(low.Centre.X, floor, 0);   // step into the new hole
            }
            Assert.Greater(feet.X, 5, "the tunnel went forward");
        }

        [Test]
        public void Digging_IsSlowerThanWalking_ButQuickEnoughToCaveAroundSomeone()
        {
            double perSecond = (DigRules.ShovelBite + DigRules.ShovelRadius) / (ShovelSwing.Thrust + ShovelSwing.PullBack);
            Assert.That(perSecond, Is.InRange(0.8, 1.6), "metres of tunnel a second");
            Assert.GreaterOrEqual(ShovelSwing.HeardFrom, 1.3f, "digging is loud work");
        }

        [Test]
        public void Bullets_StopDigging_LongBeforeTheShovelHasToStop()
        {
            var field = new DigField();
            var rng = new Random(5);
            int refused = 0;
            for (int i = 0; i < 3000 && refused < 50; i++)
            {
                var c = new DigCarve(new Vec3d(rng.NextDouble() * 900, 500, rng.NextDouble() * 900), DigRules.BulletRadius(30));
                if (!field.TryCarve(c, null, null, DigRules.ChunkCapFor(DigSource.Bullet))) refused++;
            }
            Assert.Greater(refused, 0, "a long firefight reaches the bullets' share");
            Assert.LessOrEqual(field.ChunkCount, DigRules.BulletChunks);
            // The shovel still has room.
            Assert.IsTrue(field.TryCarve(DigRules.Shovel(new Vec3d(2000, 500, 0), new Vec3d(0, -1, 0)), null, null, DigRules.ChunkCapFor(DigSource.Shovel)));
            Assert.Less(DigRules.BulletChunks, DigRules.MaxChunks * 0.7);
        }

        [Test]
        public void SomeoneDugIn_IsGoneAfter_ThroughTheMouthOfTheirTunnel()
        {
            var field = new DigField();
            Func<Vec3d, double> ground = dir => 500.0;
            // A way in at x = 0, a tunnel running 8 m under the ground, and a bullet pock nearer the player.
            field.TryCarve(new DigCarve(new Vec3d(0, 499.6, 0), 0.72), null, null);
            for (int i = 1; i <= 12; i++) field.TryCarve(new DigCarve(new Vec3d(i * 0.7, 498.3, 0), 0.72), null, null);
            field.TryCarve(new DigCarve(new Vec3d(7.5, 499.95, 1.0), DigRules.BulletRadius(30)), null, null);
            Vec3d player = new Vec3d(8.2, 498.1, 0);
            Assert.IsTrue(field.WayInNear(player, 30, ground, out Vec3d mouth));
            Assert.Less(Vec3d.Distance(mouth, new Vec3d(0, 500, 0)), 0.5, "the mouth, not the bullet hole and not the roof");
            Assert.IsFalse(new DigField().WayInNear(player, 30, ground, out _));
        }

        [Test]
        public void ARefusedBite_SaysWhy()
        {
            PlanetTerrain moon = World("Moon");
            Vec3d dir = new Vec3d(0.3, 0.8, 0.5).Normalized;
            double groundR = moon.Radius + moon.Height(dir);
            var deep = new DigCarve(dir * (groundR - DigRules.MaxDepth - 3), 0.7);
            Assert.IsFalse(DigRules.Fit(moon, 0, ref deep, out DigRefusal why));
            Assert.AreEqual(DigRefusal.TooDeep, why);
            var fine = new DigCarve(dir * (groundR - 1), 0.7);
            Assert.IsTrue(DigRules.Fit(moon, 0, ref fine, out why));
            Assert.AreEqual(DigRefusal.None, why);
            foreach (DigRefusal r in Enum.GetValues(typeof(DigRefusal)))
            {
                bool said = !string.IsNullOrEmpty(ShovelSwing.Words(r));
                Assert.AreEqual(r != DigRefusal.None && r != DigRefusal.AlreadyDug, said, r.ToString());
            }
        }
    }
}
