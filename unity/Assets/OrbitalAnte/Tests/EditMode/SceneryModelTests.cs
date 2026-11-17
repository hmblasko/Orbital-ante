using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Look;
using OrbitalAnte.Planets;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Earth's Blender-made scenery (Resources/Models/Earth*.bytes) against what the game expects of it: a piece
    /// for every kind and variant, the right size, inside its triangle cap, and something solid where there should be.
    /// </summary>
    public class SceneryModelTests
    {
        static readonly Dictionary<string, ModelFile> Files = new Dictionary<string, ModelFile>();

        static string ModelsFolder()
        {
            string dir = TestContext.CurrentContext.TestDirectory;
            for (int i = 0; i < 10 && dir != null; i++)
            {
                foreach (string candidate in new[] { Path.Combine(dir, "Assets", "OrbitalAnte", "Resources", "Models"), Path.Combine(dir, "unity", "Assets", "OrbitalAnte", "Resources", "Models") })
                {
                    if (Directory.Exists(candidate)) return candidate;
                }
                dir = Path.GetDirectoryName(dir);
            }
            string fromEnv = Environment.GetEnvironmentVariable("OA_MODELS");
            return !string.IsNullOrEmpty(fromEnv) && Directory.Exists(fromEnv) ? fromEnv : Path.Combine(Directory.GetCurrentDirectory(), "Assets", "OrbitalAnte", "Resources", "Models");
        }

        static ModelFile Load(string id)
        {
            if (Files.TryGetValue(id, out ModelFile cached)) return cached;
            string path = Path.Combine(ModelsFolder(), id + ".bytes");
            Assert.IsTrue(File.Exists(path), "Model file not found: " + path);
            ModelFile model = ModelFile.Parse(File.ReadAllBytes(path), out string problem);
            Assert.IsNotNull(model, problem);
            Files[id] = model;
            return model;
        }

        static IEnumerable<TestCaseData> AllScenery()
        {
            foreach (FeatureKind kind in Enum.GetValues(typeof(FeatureKind)))
            {
                if (SceneryKinds.ModelOf(kind) == null) continue;
                for (int v = 0; v < FeatureShapes.Variants; v++) yield return new TestCaseData(kind, v);
            }
        }

        [Test]
        public void EveryNewKind_HasAModel_AndAStandInShape()
        {
            foreach (FeatureKind kind in Enum.GetValues(typeof(FeatureKind)))
            {
                if (kind <= FeatureKind.Iceberg) continue;
                Assert.IsNotNull(SceneryKinds.ModelOf(kind), kind + " has no model file named for it.");
                Assert.AreNotEqual(kind, FeatureShapes.StandIn(kind), kind + " needs a stand-in shape for when its model is missing.");
                Assert.Greater(FeatureShapes.Build(kind, 0).Triangles.Length, 0);
                Assert.IsTrue(Feature.OwnColour(kind), kind + " comes painted and must not be tinted with the rock colour.");
            }
        }

        [TestCaseSource(nameof(AllScenery))]
        public void Piece_IsOneUnitTall_StandsOnTheGround_AndFitsItsCap(FeatureKind kind, int variant)
        {
            ModelFile file = Load(SceneryKinds.ModelOf(kind));
            string name = SceneryKinds.Piece(kind, variant);
            ModelPart part = file.Part(name);
            Assert.IsNotNull(part, name + " is missing from " + SceneryKinds.ModelOf(kind));
            Assert.LessOrEqual(part.TriangleCount, SceneryKinds.TriangleCap(kind), name + " is over its triangle cap.");

            float top = float.MinValue, bottom = float.MaxValue, reach = 0;
            for (int i = 0; i < part.VertexCount; i++)
            {
                float x = part.Positions[i * 3], y = part.Positions[i * 3 + 1], z = part.Positions[i * 3 + 2];
                top = Math.Max(top, y);
                bottom = Math.Min(bottom, y);
                reach = Math.Max(reach, (float)Math.Sqrt(x * x + z * z));
            }
            Assert.AreEqual(1f, top, 0.01f, name + " must be exactly 1 unit tall: the game scales it by its height in metres.");
            Assert.LessOrEqual(bottom, 0.001f, name + " must reach the ground (and may go a little below it, to sit well on a slope).");
            Assert.Greater(bottom, -0.6f, name + " goes too far underground.");
            Assert.Less(reach, 3.2f, name + " is far wider than it is tall.");

            // Colours are stored as seen on screen: none may be pure black or missing.
            float brightest = 0;
            for (int i = 0; i < part.Colors.Length; i++) brightest = Math.Max(brightest, part.Colors[i]);
            Assert.Greater(brightest, 0.3f, name + " has no paint.");
        }

        [TestCaseSource(nameof(AllScenery))]
        public void Piece_IsSolidTheWayItsKindSays(FeatureKind kind, int variant)
        {
            ModelFile file = Load(SceneryKinds.ModelOf(kind));
            string name = SceneryKinds.Piece(kind, variant);
            switch (SceneryKinds.SolidOf(kind))
            {
                case ScenerySolid.Trunk:
                    Assert.IsTrue(file.TrySocket(name + "A", out ModelSocket a), name + " has no trunk foot (socket A).");
                    Assert.IsTrue(file.TrySocket(name + "B", out ModelSocket b), name + " has no trunk top (socket B).");
                    Assert.IsTrue(file.TrySocket(name + "R", out ModelSocket r), name + " has no trunk radius (socket R).");
                    Assert.AreEqual(0f, a.Y, 0.02f, name + ": the trunk starts at the ground.");
                    Assert.Greater(b.Y, 0.3f, name + ": the trunk is too short to stop anything.");
                    Assert.LessOrEqual(b.Y, 1f);
                    Assert.That(r.X, Is.InRange(0.01f, 0.15f), name + ": trunk radius.");
                    break;
                case ScenerySolid.Shape:
                    ModelPart hit = file.Part(name + "Hit");
                    Assert.IsNotNull(hit, name + " has no solid shape (a piece called " + name + "Hit).");
                    Assert.LessOrEqual(hit.TriangleCount, SceneryKinds.SolidTriangles);
                    Assert.Greater(hit.TriangleCount, 8);
                    break;
                default:
                    Assert.IsNull(file.Part(name + "Hit"), name + " is a small plant: nothing should be solid.");
                    break;
            }
        }

        [Test]
        public void Earth_GrowsEveryKindOfScenery()
        {
            PlanetProfile earth = null;
            foreach (PlanetProfile p in PlanetProfiles.Defaults()) if (p.bodyId == "Earth") earth = p;
            Assert.IsNotNull(earth);
            var used = new HashSet<FeatureKind>();
            foreach (ScatterRule rule in earth.scatter) used.Add(rule.feature);
            foreach (FormationUse use in earth.formations)
            {
                Formation f = FormationIndex.Get(use.formation);
                Assert.IsNotNull(f, use.formation);
                if (f.Technique == Technique.Scatter) used.Add(f.Feature);
            }
            foreach (FeatureKind kind in Enum.GetValues(typeof(FeatureKind)))
            {
                if (SceneryKinds.ModelOf(kind) != null) Assert.IsTrue(used.Contains(kind), "Nothing on Earth grows " + kind + ".");
            }
        }

        [Test]
        public void Woods_ComeInPatches_WithClearingsBetween()
        {
            var terrain = new PlanetTerrain(PlanetProfilesForTests.Earth(), "Earth", 500, 12345);
            var rule = new ScatterRule(FeatureKind.Broadleaf, 4, 5, 9) { patch = 90, cover = 0.5 };
            int inside = 0, total = 0;
            var random = new OrbitalAnte.Core.SeededRandom(7);
            for (int i = 0; i < 4000; i++)
            {
                double y = random.NextDouble() * 2 - 1, turn = random.NextDouble() * Math.PI * 2, r = Math.Sqrt(1 - y * y);
                var dir = new OrbitalAnte.Core.Vec3d(Math.Cos(turn) * r, y, Math.Sin(turn) * r);
                total++;
                if (PlanetScatter.InPatch(terrain, 3, rule, dir)) inside++;
            }
            double share = inside / (double)total;
            Assert.That(share, Is.InRange(0.3, 0.7), "With cover 0.5 about half the ground should be wooded.");
        }
    }

    static class PlanetProfilesForTests
    {
        public static PlanetProfile Earth()
        {
            foreach (PlanetProfile p in PlanetProfiles.Defaults()) if (p.bodyId == "Earth") return p;
            return null;
        }
    }
}
