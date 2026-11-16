using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.Space;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>
    /// Cave networks (Matthew, 16 Nov 2026: "some of the planets have interesting and intricate cave systems where
    /// enemies will reside"): where they go, that every room can be reached, that they stay under a proper roof,
    /// that their walls are sound and face the right way, and that light reaches only where it should.
    /// </summary>
    public class CaveNetworkTests
    {
        static readonly int[] Seeds = { 4242, 7, 1999 };
        static Dictionary<string, BodySpec> _bodies;
        static List<PlanetProfile> _profiles;
        static readonly Dictionary<int, Dictionary<string, PlanetTerrain>> _worlds = new Dictionary<int, Dictionary<string, PlanetTerrain>>();

        [OneTimeSetUp]
        public void Load()
        {
            _profiles = PlanetProfiles.Defaults();
            _bodies = new Dictionary<string, BodySpec>();
            foreach (BodySpec b in SolarSystemDefaults.Bodies()) _bodies[b.id] = b;
        }

        static PlanetTerrain World(string id, int seed)
        {
            if (!_worlds.TryGetValue(seed, out var bySeed)) _worlds[seed] = bySeed = new Dictionary<string, PlanetTerrain>();
            if (bySeed.TryGetValue(id, out PlanetTerrain t)) return t;
            PlanetProfile p = _profiles.Find(x => x.bodyId == id);
            t = new PlanetTerrain(p, id, _bodies[id].radius, seed);
            bySeed[id] = t;
            return t;
        }

        static IEnumerable<CaveNetwork> Networks(PlanetTerrain t)
        {
            foreach (Cave c in t.Caves) if (c.Network != null) yield return c.Network;
        }

        static readonly string[] WithNetworks = { "Earth", "Moon", "Mars", "Ganymede" };

        [Test]
        public void SomeWorldsHaveNetworks_NotAll()
        {
            int worldsWith = 0;
            foreach (PlanetProfile p in _profiles)
            {
                bool listed = false;
                foreach (FormationUse use in p.formations)
                {
                    Formation f = FormationIndex.Get(use.formation);
                    if (f != null && f.CaveStyle != CaveStyle.None) listed = true;
                }
                if (listed) worldsWith++;
            }
            Assert.That(worldsWith, Is.InRange(3, 6), "Cave networks belong on some worlds, not all.");
            foreach (int seed in Seeds)
            {
                int found = 0;
                foreach (string id in WithNetworks) foreach (CaveNetwork _ in Networks(World(id, seed))) found++;
                Assert.GreaterOrEqual(found, 4, "Seed " + seed + ": too few cave networks were placed.");
            }
        }

        [Test]
        public void TheSameSeedGivesTheSameNetwork()
        {
            PlanetProfile p = _profiles.Find(x => x.bodyId == "Mars");
            var a = new PlanetTerrain(p, "Mars", _bodies["Mars"].radius, 31);
            var b = new PlanetTerrain(p, "Mars", _bodies["Mars"].radius, 31);
            var na = new List<CaveNetwork>(Networks(a));
            var nb = new List<CaveNetwork>(Networks(b));
            Assert.AreEqual(na.Count, nb.Count);
            for (int i = 0; i < na.Count; i++)
            {
                Assert.AreEqual(na[i].Chambers.Count, nb[i].Chambers.Count);
                Assert.AreEqual(na[i].Origin, nb[i].Origin);
                Assert.AreEqual(na[i].Air(na[i].Chambers[0].Centre), nb[i].Air(nb[i].Chambers[0].Centre));
            }
        }

        [Test]
        public void NetworksAreIntricate_RoomsOnSeveralLevels_PassagesThatBranchAndLoop()
        {
            foreach (int seed in Seeds)
                foreach (string id in WithNetworks)
                    foreach (CaveNetwork net in Networks(World(id, seed)))
                    {
                        string what = id + " seed " + seed;
                        Assert.GreaterOrEqual(net.Chambers.Count, 4, what + ": too few rooms.");
                        var tiers = new HashSet<int>();
                        foreach (CaveChamber c in net.Chambers) tiers.Add(c.Tier);
                        Assert.GreaterOrEqual(tiers.Count, 2, what + ": every room on one level.");
                        int passages = 0, deadEnds = 0;
                        foreach (CaveTunnel t in net.Tunnels)
                        {
                            if (t.From >= 0 && t.To >= 0 && !t.Shaft) passages++;
                            if (t.To < 0 && !t.Entrance) deadEnds++;
                        }
                        // More passages than a plain chain needs means there are loops.
                        Assert.GreaterOrEqual(passages, net.Chambers.Count, what + ": no loops.");
                        Assert.GreaterOrEqual(deadEnds, 2, what + ": no side passages.");
                        Assert.GreaterOrEqual(net.Openings.Count, 1);
                    }
        }

        [Test]
        public void EveryRoomCanBeWalkedTo_FromTheWayIn()
        {
            foreach (int seed in Seeds)
                foreach (string id in WithNetworks)
                    foreach (CaveNetwork net in Networks(World(id, seed)))
                    {
                        string what = id + " seed " + seed;
                        CaveTunnel entrance = null;
                        foreach (CaveTunnel t in net.Tunnels) if (t.Entrance && !t.Shaft) { entrance = t; break; }
                        Assert.IsNotNull(entrance, what + ": no walk-in entrance.");
                        int from = net.NearestWaypoint(net.Chambers[entrance.From].Floor);
                        for (int room = 0; room < net.Chambers.Count; room++)
                        {
                            int to = net.NearestWaypoint(net.Chambers[room].Floor);
                            Assert.IsNotNull(net.Route(from, to), what + ": room " + room + " cannot be walked to.");
                        }
                    }
        }

        [Test]
        public void RoomsHaveAProperRoof_AndAFloor()
        {
            foreach (int seed in Seeds)
                foreach (string id in WithNetworks)
                {
                    PlanetTerrain t = World(id, seed);
                    foreach (CaveNetwork net in Networks(t))
                    {
                        var pits = new HashSet<int>();
                        foreach (CaveTunnel tunnel in net.Tunnels) if (tunnel.Shaft && !tunnel.Entrance) pits.Add(tunnel.To);
                        for (int i = 0; i < net.Chambers.Count; i++)
                        {
                            CaveChamber c = net.Chambers[i];
                            // A room with a pit in its floor is measured beside the pit.
                            if (pits.Contains(i)) continue;
                            string what = id + " seed " + seed + " room " + i;
                            double roof = t.Radius + t.Height(c.Up) - (c.Centre.Magnitude + c.Height);
                            if (!c.Skylight) Assert.GreaterOrEqual(roof, 4.0, what + ": thin roof.");
                            Assert.Less(net.Air(c.Floor + c.Up * 1.5), -0.5, what + ": no room to stand.");
                            Assert.IsTrue(net.FloorBelow(c.Floor + c.Up * 1.5, out Vec3d floor), what + ": no floor.");
                            Assert.Less(System.Math.Abs(floor.Magnitude - c.Floor.Magnitude), 1.6, what + ": floor is not where the room says.");
                            Assert.IsTrue(TerrainCollision.InsideCave(net.AsCave(), c.Floor + c.Up, 0), what + ": not counted as in a cave.");
                        }
                        foreach (CaveOpening o in net.Openings) Assert.IsTrue(t.InHole(o.Direction, 0), id + ": a way in with no hole in the ground.");
                    }
                }
        }

        [Test]
        public void Walls_AreSound_FaceIntoTheCave_AndFitTheBudget()
        {
            foreach (string id in WithNetworks)
            {
                PlanetTerrain t = World(id, Seeds[0]);
                foreach (CaveNetwork net in Networks(t))
                {
                    var watch = Stopwatch.StartNew();
                    List<CaveChunk> chunks = CaveNetworkMesher.Build(net, t);
                    watch.Stop();
                    int triangles = 0, facing = 0, faces = 0;
                    foreach (CaveChunk chunk in chunks)
                    {
                        ShapeData s = chunk.Shape;
                        Assert.Less(s.VertexCount, 65000, "A block of cave wall too big for one mesh.");
                        Assert.AreEqual(s.Positions.Length, s.Normals.Length);
                        Assert.AreEqual(s.Positions.Length, s.Colors.Length);
                        triangles += s.Triangles.Length / 3;
                        for (int q = 0; q < s.Triangles.Length; q += 3)
                        {
                            Vec3d a = P(s.Positions, s.Triangles[q]), b = P(s.Positions, s.Triangles[q + 1]), c = P(s.Positions, s.Triangles[q + 2]);
                            Vec3d n = P(s.Normals, s.Triangles[q]) + P(s.Normals, s.Triangles[q + 1]) + P(s.Normals, s.Triangles[q + 2]);
                            Vec3d cross = Vec3d.Cross(b - a, c - a);
                            if (cross.SqrMagnitude < 1e-10) continue;
                            faces++;
                            if (Vec3d.Dot(cross, n) > 0) facing++;
                        }
                        for (int q = 0; q < s.Positions.Length; q++) Assert.IsFalse(float.IsNaN(s.Positions[q]) || float.IsNaN(s.Normals[q]));
                        for (int q = 0; q < s.Colors.Length; q++) Assert.That(s.Colors[q], Is.InRange(0f, 1f));
                    }
                    TestContext.Out.WriteLine($"{id}: {net.Style}, {net.Chambers.Count} rooms, {net.Tunnels.Count} passages, {net.Openings.Count} holes, {chunks.Count} blocks, {triangles} triangles, {watch.ElapsedMilliseconds} ms");
                    Assert.Greater(triangles, 2000, id + ": hardly any wall.");
                    Assert.Less(triangles, CaveBudget.MostTriangles, id + ": cave walls over the budget.");
                    Assert.Greater(facing, faces * 0.97, id + ": walls face into the rock and would not be drawn.");
                }
            }
        }

        [Test]
        public void Light_ReachesUnderTheHoles_NotDeepInside_AndLampsLightTheCamps()
        {
            foreach (string id in WithNetworks)
            {
                PlanetTerrain t = World(id, Seeds[0]);
                foreach (CaveNetwork net in Networks(t))
                {
                    var watch = Stopwatch.StartNew();
                    CaveLightVolume light = CaveLightVolume.Build(net, t);
                    watch.Stop();
                    TestContext.Out.WriteLine($"{id}: light {light.Nx}x{light.Ny}x{light.Nz}, {watch.ElapsedMilliseconds} ms");
                    Assert.LessOrEqual(light.Rgba.Length, CaveBudget.MostLightBytes);
                    // Just inside the walk-in entrance there is daylight; in the deepest room there is next to none.
                    foreach (CaveTunnel tunnel in net.Tunnels)
                    {
                        if (!tunnel.Entrance || tunnel.Shaft) continue;
                        Vec3d inside = tunnel.Points[tunnel.Points.Length - 4];
                        light.Sample(net, inside, out float sky, out _, out _, out _);
                        Assert.Greater(sky, 0.25f, id + ": no daylight just inside the way in.");
                    }
                    CaveChamber deepest = null;
                    foreach (CaveChamber c in net.Chambers)
                        if (!c.Skylight && (deepest == null || c.Floor.Magnitude < deepest.Floor.Magnitude)) deepest = c;
                    if (deepest != null)
                    {
                        light.Sample(net, deepest.Floor + deepest.Up * 1.5, out float deepSky, out _, out _, out _);
                        Assert.Less(deepSky, 0.12f, id + ": daylight deep in the cave.");
                    }
                    foreach (CaveTunnel tunnel in net.Tunnels)
                    {
                        if (!tunnel.Shaft || !tunnel.Entrance || tunnel.From < 0) continue;
                        CaveChamber c = net.Chambers[tunnel.From];
                        // On the floor under the hole, and something of it across the room.
                        Vec3d below = tunnel.Points[0].Normalized * (c.Floor.Magnitude + 1.5);
                        light.Sample(net, below, out float under, out _, out _, out _);
                        Assert.Greater(under, 0.3f, id + ": the hole in the roof lets no light in.");
                        light.Sample(net, c.Floor + c.Up * 1.5, out float middle, out _, out _, out _);
                        Assert.Greater(middle, 0.1f, id + ": the hole in the roof lights nothing of the room.");
                    }
                    for (int i = 0; i < net.Chambers.Count; i++)
                    {
                        if (!net.Chambers[i].Camp) continue;
                        light.Sample(net, net.Chambers[i].Floor + net.Chambers[i].Up * 1.5, out _, out float r, out float g, out float b);
                        Assert.Greater(r + g + b, 0.15f, id + ": a camp in the dark.");
                    }
                }
            }
        }


        [Test]
        public void SomeoneFollowingTheNextStep_WalksFromTheWayInToEveryCamp()
        {
            foreach (string id in WithNetworks)
                foreach (int seed in Seeds) foreach (CaveNetwork net in Networks(World(id, seed)))
                {
                    CaveTunnel entrance = null;
                    foreach (CaveTunnel t in net.Tunnels) if (t.Entrance && !t.Shaft) { entrance = t; break; }
                    for (int room = 0; room < net.Chambers.Count; room++)
                    {
                        if (!net.Chambers[room].Camp) continue;
                        Vec3d goal = net.Chambers[room].Floor;
                        Assert.IsTrue(net.FloorBelow(net.Chambers[entrance.From].Floor + net.Chambers[entrance.From].Up * 1.5, out Vec3d at));
                        bool arrived = false;
                        string why = "ran out of steps";
                        for (int step = 0; step < 600 && !arrived; step++)
                        {
                            Vec3d next = net.NextStep(at, goal);
                            Vec3d up = at.Normalized;
                            Vec3d level = next - at;
                            level = level - up * Vec3d.Dot(level, up);
                            if (level.Magnitude < 0.05) break;
                            Vec3d moved = at + level.Normalized * System.Math.Min(0.7, level.Magnitude);
                            // Stand on whatever floor is there, as an enemy's feet do.
                            if (!net.FeetAt(moved, out Vec3d floor, 4)) { why = "no floor at " + moved + " air " + net.Air(moved + moved.Normalized * 1.2).ToString("0.00") + " step " + step; break; }
                            at = floor;
                            if ((at - goal).Magnitude < 3) arrived = true;
                        }
                        Assert.IsTrue(arrived, id + " seed " + seed + ": following the passages never reached the camp in room " + room + ", stuck " + (at - goal).Magnitude.ToString("0.0") + " m away at waypoint " + net.NearestWaypoint(at) + " (room " + net.WaypointRoom[net.NearestWaypoint(at)] + "), heading for " + net.NextStep(at, goal) + " from " + at + ": " + why);
                    }
                }
        }

        [Test]
        public void EveryLivedInRoom_GetsACamp()
        {
            var roster = OrbitalAnte.Enemies.EnemyRoster.Defaults();
            foreach (string id in WithNetworks)
                foreach (CaveNetwork net in Networks(World(id, Seeds[0])))
                {
                    var camps = new List<OrbitalAnte.Enemies.EnemyCamp>();
                    OrbitalAnte.Enemies.EnemyField.AddCaveCamps(camps, roster, new SeededRandom(5), net);
                    int rooms = 0;
                    foreach (CaveChamber c in net.Chambers) if (c.Camp) rooms++;
                    Assert.AreEqual(rooms, camps.Count);
                    Assert.Greater(rooms, 0, id + ": nobody lives in this cave.");
                    foreach (var camp in camps)
                    {
                        Assert.AreSame(net, camp.Cave);
                        Assert.That(camp.Members.Length, Is.InRange(2, 5));
                        Assert.Less(System.Math.Abs(camp.HomeRadius - net.Chambers[camp.Room].Floor.Magnitude), 1e-6);
                        int lamps = 0;
                        foreach (CaveLamp lamp in net.Lamps) if (!lamp.Crystal && lamp.Chamber == camp.Room) lamps++;
                        Assert.Greater(lamps, 0, id + ": a camp with no lamp.");
                    }
                }
        }

        [Test]
        public void TheWorkLamp_FacesOutward_AndItsLensFacesForward()
        {
            ShapeData body = CaveProps.WorkLamp(out ShapeData lens);
            Assert.Greater(body.Triangles.Length, 0);
            // The lens is the face nearest +z: its front triangles must face +z (Unity draws only front faces).
            int forward = 0;
            for (int q = 0; q < lens.Triangles.Length; q += 3)
            {
                Vec3d a = P(lens.Positions, lens.Triangles[q]), b = P(lens.Positions, lens.Triangles[q + 1]), c = P(lens.Positions, lens.Triangles[q + 2]);
                Vec3d middle = (a + b + c) / 3 - new Vec3d(0, 1.72, 0.125);
                Vec3d cross = Vec3d.Cross(b - a, c - a);
                Assert.Greater(Vec3d.Dot(cross, middle), 0, "A face of the lens faces into it.");
                if (cross.Normalized.Z > 0.9) forward++;
            }
            Assert.AreEqual(2, forward);
        }

        static Vec3d P(float[] a, int i) => new Vec3d(a[i * 3], a[i * 3 + 1], a[i * 3 + 2]);
    }
}
