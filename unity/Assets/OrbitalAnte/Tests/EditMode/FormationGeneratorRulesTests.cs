using System;
using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;

namespace OrbitalAnte.Tests
{
    public class FormationGeneratorRulesTests
    {
        const string Body = "FormationRuleTest";

        static PlanetProfile Profile(params FormationUse[] uses)
        {
            var profile = new PlanetProfile { bodyId = Body, rockColor = "#204060" };
            profile.formations.AddRange(uses);
            return profile;
        }

        static PlanetTerrain Build(int seed, params FormationUse[] uses) => new PlanetTerrain(Profile(uses), Body, 500, seed);

        static void AssertEmpty(PlanetTerrain terrain)
        {
            Assert.IsEmpty(terrain.Stamps);
            Assert.IsEmpty(terrain.Placements);
            Assert.IsEmpty(terrain.Ponds);
            Assert.IsEmpty(terrain.Plumes);
            Assert.IsEmpty(terrain.Caves);
            Assert.IsEmpty(terrain.Holes);
        }

        static void AssertSameStamp(Stamp a, Stamp b)
        {
            Assert.AreSame(a.Formation, b.Formation);
            Assert.AreEqual(a.Kind, b.Kind);
            Assert.AreEqual(a.Seed, b.Seed);
            Assert.AreEqual(a.Center, b.Center);
            Assert.AreEqual(a.U, b.U);
            Assert.AreEqual(a.V, b.V);
            Assert.AreEqual(a.Start, b.Start);
            Assert.AreEqual(a.Normal, b.Normal);
            Assert.AreEqual(a.Along, b.Along);
            CollectionAssert.AreEqual(
                new[] { a.Size, a.Extent, a.BoundRadius, a.Height, a.ArcAngle, a.Length, a.MeanderAmount, a.Bow,
                    a.TintR, a.TintG, a.TintB, a.TintStrength },
                new[] { b.Size, b.Extent, b.BoundRadius, b.Height, b.ArcAngle, b.Length, b.MeanderAmount, b.Bow,
                    b.TintR, b.TintG, b.TintB, b.TintStrength });
        }

        static List<Stamp> StampsFor(PlanetTerrain terrain, string id) => terrain.Stamps.FindAll(s => s.Formation.Id == id);

        [Test]
        public void EmptyFormationListCreatesNoLocalFeatures()
        {
            AssertEmpty(Build(0));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(int.MinValue)]
        public void NonpositiveUseCountCreatesNoStampScatterOrCave(int count)
        {
            Formation cave = null;
            foreach (Formation formation in FormationIndex.All)
                if (formation.Technique == Technique.Cave) { cave = formation; break; }
            Assert.IsNotNull(cave);
            AssertEmpty(Build(12, new FormationUse("simple_crater", count),
                new FormationUse("boulder_field", count), new FormationUse(cave.Id, count)));
        }

        [Test]
        public void UnknownGlobalAndSkyRequestsAreSkippedBeforeTheirCountsAreExpanded()
        {
            AssertEmpty(Build(3, new FormationUse("missing.formation", int.MaxValue),
                new FormationUse("ocean", int.MaxValue), new FormationUse("cloud_bands", int.MaxValue)));
        }

        [TestCase(0)]
        [TestCase(-104729)]
        [TestCase(int.MaxValue)]
        public void IncreasingCountKeepsEveryExistingFormationInstanceUnchanged(int seed)
        {
            PlanetTerrain first = Build(seed, new FormationUse("simple_crater", 3));
            PlanetTerrain more = Build(seed, new FormationUse("simple_crater", 7));
            Assert.AreEqual(3, first.Stamps.Count);
            Assert.AreEqual(7, more.Stamps.Count);
            for (int i = 0; i < first.Stamps.Count; i++) AssertSameStamp(first.Stamps[i], more.Stamps[i]);
        }

        [Test]
        public void InsertingOrReorderingOtherFormationKindsDoesNotMoveExistingOrdinaryStamps()
        {
            PlanetTerrain first = Build(42, new FormationUse("simple_crater", 3));
            PlanetTerrain before = Build(42, new FormationUse("mesa", 2), new FormationUse("simple_crater", 3));
            PlanetTerrain after = Build(42, new FormationUse("simple_crater", 3), new FormationUse("mesa", 2));
            List<Stamp> a = StampsFor(first, "simple_crater"), b = StampsFor(before, "simple_crater"), c = StampsFor(after, "simple_crater");
            Assert.AreEqual(3, a.Count);
            Assert.AreEqual(a.Count, b.Count);
            Assert.AreEqual(a.Count, c.Count);
            for (int i = 0; i < a.Count; i++) { AssertSameStamp(a[i], b[i]); AssertSameStamp(a[i], c[i]); }
        }

        [TestCase("boulder_field")]
        [TestCase("conifer_forest")]
        public void ScatterOutputsRepeatAfterAnUnrelatedBuildAndKeepTheirDefinitionBounds(string id)
        {
            Formation formation = FormationIndex.Get(id);
            PlanetTerrain first = Build(71, new FormationUse(id, 1));
            Build(-900, new FormationUse("simple_crater", 2));
            PlanetTerrain again = Build(71, new FormationUse(id, 1));
            Assert.That(first.Placements.Count, Is.InRange(formation.MinCount, formation.MaxCount));
            CollectionAssert.AreEqual(first.Placements, again.Placements);
            double shrink = Math.Max(0.6, Math.Min(1, first.Scale));
            float[] tint = Feature.OwnColour(formation.Feature) ? new[] { 1f, 1f, 1f } : ColorUtil.Parse(first.Profile.rockColor);
            foreach (Placement placement in first.Placements)
            {
                Assert.AreEqual(formation.Feature, placement.Kind);
                Assert.IsFalse(placement.Absolute);
                Assert.AreEqual(formation.Floats, placement.Floats);
                Assert.AreEqual(1, placement.Direction.Magnitude, 1e-12);
                Assert.That(placement.Scale, Is.InRange(formation.MinScale * shrink, formation.MaxScale * shrink));
                Assert.That(placement.Variant, Is.InRange(0, 1023));
                Assert.That(placement.Yaw, Is.InRange(0, Math.PI * 2));
                CollectionAssert.AreEqual(tint, new[] { placement.R, placement.G, placement.B });
            }
        }

        [TestCase("simple_crater")]
        [TestCase("shield_volcano")]
        [TestCase("inselberg")]
        [TestCase("mesa")]
        [TestCase("barchan_dunes")]
        public void HugeAuthoredRadialLandmarkSizeIsBoundedByBodyRadius(string id)
        {
            PlanetTerrain terrain = Build(25, FormationUse.Landmark(id, 0, 0, 1e200));
            Assert.AreEqual(1, terrain.Stamps.Count);
            Stamp stamp = terrain.Stamps[0];
            Assert.Greater(stamp.Size, 0);
            Assert.LessOrEqual(stamp.Size, terrain.Radius * 0.5);
            Assert.LessOrEqual(Math.Abs(stamp.Height), terrain.Radius * 0.16);
            Assert.IsFalse(double.IsNaN(stamp.BoundRadius) || double.IsInfinity(stamp.BoundRadius));
            Assert.AreEqual(1, stamp.Center.Magnitude, 1e-12);
        }

        [TestCase(0)]
        [TestCase(-100)]
        public void NonpositiveAuthoredLandmarkSizeUsesTheDefinitionRange(double size)
        {
            Formation formation = FormationIndex.Get("simple_crater");
            PlanetTerrain terrain = Build(21, FormationUse.Landmark(formation.Id, -90, 0, size));
            Stamp stamp = terrain.Stamps[0];
            Assert.That(stamp.Size, Is.InRange(formation.MinSize * terrain.Scale, formation.MaxSize * terrain.Scale));
            Assert.Less(Vec3d.Distance(new Vec3d(0, -1, 0), stamp.Center), 1e-12);
        }

        [TestCase(Where.Any)]
        [TestCase(Where.Polar)]
        [TestCase(Where.SouthPole)]
        [TestCase(Where.Equatorial)]
        public void LocationConstraintsUseRepeatableNamedStreamsAndProduceUnitDirections(Where where)
        {
            PlanetTerrain terrain = Build(0);
            for (int i = 0; i < 32; i++)
            {
                string stream = "tests.formation.location." + where + "." + i;
                Vec3d a = FormationGenerator.PickLocation(terrain, where, SeededRandom.ForStream(104729, stream));
                Vec3d b = FormationGenerator.PickLocation(terrain, where, SeededRandom.ForStream(104729, stream));
                Assert.AreEqual(a, b);
                Assert.AreEqual(1, a.Magnitude, 1e-12);
                if (where == Where.Polar) Assert.That(Math.Abs(a.Y), Is.InRange(0.8, 1));
                if (where == Where.SouthPole) Assert.Less(a.Y, -0.9);
                if (where == Where.Equatorial) Assert.Less(Math.Abs(a.Y), 0.35);
            }
        }

        [Test]
        public void ImpossibleLocationConstraintReturnsAFiniteFallbackInsteadOfLoopingForever()
        {
            PlanetProfile profile = Profile();
            profile.hasSea = true;
            profile.seaLevel = 1e9;
            PlanetTerrain terrain = new PlanetTerrain(profile, Body, 500, 23);
            Vec3d at = FormationGenerator.PickLocation(terrain, Where.Land, SeededRandom.ForStream(23, "tests.formation.impossible"));
            Assert.AreEqual(1, at.Magnitude, 1e-12);
            Assert.Less(terrain.CoarseHeight(at), profile.seaLevel);
        }

        [Test]
        public void FinishedSecondPassDoesNotAccumulateFeaturesWhenCalledAgain()
        {
            PlanetTerrain terrain = Build(16, new FormationUse("cryovolcano", 2), new FormationUse("boulder_field", 1));
            Assert.Greater(terrain.Plumes.Count, 0, "The completed second pass must contain real pending output.");
            int stamps = terrain.Stamps.Count, placements = terrain.Placements.Count, ponds = terrain.Ponds.Count,
                plumes = terrain.Plumes.Count, caves = terrain.Caves.Count, holes = terrain.Holes.Count;
            FormationGenerator.PlaceTheRest(terrain);
            FormationGenerator.PlaceTheRest(terrain);
            CollectionAssert.AreEqual(new[] { stamps, placements, ponds, plumes, caves, holes },
                new[] { terrain.Stamps.Count, terrain.Placements.Count, terrain.Ponds.Count, terrain.Plumes.Count, terrain.Caves.Count, terrain.Holes.Count });
        }
    }
}
