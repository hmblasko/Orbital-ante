using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Bases;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.Space;

namespace OrbitalAnte.Tests
{
    public class BaseFieldTests
    {
        const int Seed = 4242;

        static readonly Dictionary<string, PlanetTerrain> Built = new Dictionary<string, PlanetTerrain>();

        static PlanetTerrain Terrain(string id)
        {
            if (Built.TryGetValue(id, out PlanetTerrain made)) return made;
            foreach (PlanetProfile p in PlanetProfiles.Defaults())
            {
                if (p.bodyId != id) continue;
                foreach (BodySpec body in SolarSystemDefaults.Bodies())
                    if (body.id == id) return Built[id] = new PlanetTerrain(p, id, body.radius, Seed);
            }
            Assert.Fail("no such body: " + id);
            return null;
        }

        static List<BaseSite> Build(PlanetTerrain terrain, int seed)
        {
            return BaseField.Build(new BaseFieldRules(), terrain, new SeededRandom(seed), new[] { 2, 2, 1 }, Vec3d.Zero);
        }

        [Test]
        public void TheSameSeedPutsTheBasesInTheSamePlaces()
        {
            PlanetTerrain mars = Terrain("Mars");
            List<BaseSite> a = Build(mars, 7), b = Build(mars, 7), c = Build(mars, 8);
            Assert.Greater(a.Count, 0, "Mars has bases");
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Up, b[i].Up);
                Assert.AreEqual(a[i].Size, b[i].Size);
                Assert.AreEqual(a[i].Kind, b[i].Kind);
            }
            Assert.AreNotEqual(a[0].Up, c[0].Up, "another seed should move them");
        }

        [Test]
        public void EveryBaseStandsOnDryLevelGround_ClearOfItsNeighbours()
        {
            var rules = new BaseFieldRules();
            foreach (string id in new[] { "Earth", "Mars", "Moon" })
            {
                PlanetTerrain terrain = Terrain(id);
                List<BaseSite> sites = Build(terrain, 11);
                Assert.Greater(sites.Count, 0, id + " has at least one base");
                for (int i = 0; i < sites.Count; i++)
                {
                    BaseSite s = sites[i];
                    Assert.LessOrEqual(s.Roughness, rules.Roughness(s.Size) + 1e-6, id + ": the ground under a base is level enough for its size");
                    Assert.Greater(s.Depth, 0, "the floor is above all the ground it covers");
                    Assert.AreEqual(rules.Footprint(s.Size), s.Footprint, 1e-6);
                    if (terrain.HasSea) Assert.Greater(s.FloorRadius - terrain.Radius, terrain.SeaLevel, id + ": never in the sea");
                    Assert.AreEqual(0, Vec3d.Dot(s.Up, s.North), 1e-9);
                    Assert.AreEqual(0, Vec3d.Dot(s.Up, s.East), 1e-9);
                    for (int k = 0; k < i; k++)
                    {
                        double apart = System.Math.Acos(System.Math.Max(-1, System.Math.Min(1, Vec3d.Dot(s.Up, sites[k].Up)))) * terrain.Radius;
                        Assert.GreaterOrEqual(apart, System.Math.Min(rules.spacing, terrain.Radius * System.Math.PI / 3) - 1e-6, id + ": bases keep their distance");
                    }
                }
            }
        }

        [Test]
        public void ABigWorldHasMoreBasesThanASmallOne_AndOneBigBase()
        {
            var rules = new BaseFieldRules();
            Assert.Greater(BaseField.BaseCount(rules, 500), BaseField.BaseCount(rules, 150));
            Assert.AreEqual(0, BaseField.BaseCount(rules, 30), "a pebble has no room for a base");
            List<BaseSite> earth = Build(Terrain("Earth"), 3);
            Assert.GreaterOrEqual(earth.Count, 3);
            bool three = false;
            var seen = new HashSet<BaseSize>();
            foreach (BaseSite s in earth) seen.Add(s.Size);
            three = seen.Count >= 2;
            Assert.IsTrue(three, "Earth's bases are not all one size");
        }

        [Test]
        public void NoBaseCrowdsTheLaunchPad()
        {
            PlanetTerrain earth = Terrain("Earth");
            var rules = new BaseFieldRules();
            Vec3d pad = new Vec3d(1, 0.2, 0.3).Normalized;
            List<BaseSite> sites = BaseField.Build(rules, earth, new SeededRandom(5), null, pad);
            foreach (BaseSite s in sites)
            {
                double apart = System.Math.Acos(System.Math.Max(-1, System.Math.Min(1, Vec3d.Dot(s.Up, pad)))) * earth.Radius;
                Assert.GreaterOrEqual(apart, rules.padClear - 1e-6);
            }
        }
    }
}
