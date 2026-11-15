using System;
using NUnit.Framework;
using OrbitalAnte.Planets;

namespace OrbitalAnte.Tests
{
    public class FormationDefinitionRulesTests
    {
        [Test]
        public void FeatureKindValuesAlreadyStoredInAssetsCannotBeRenumbered()
        {
            // New kinds may be appended. Inserting or reordering an old kind would reinterpret saved assets.
            string[] stored =
            {
                "Boulder", "Slab", "RockPile", "Hoodoo", "Spire", "Arch", "BalancedRock", "Tor",
                "BasaltColumns", "Crystal", "IceShard", "Stalagmite", "Conifer", "Broadleaf", "Palm",
                "Bush", "Cactus", "VentChimney", "Iceberg", "Birch", "Maple", "Blossom", "Pine",
                "Acacia", "GiantTree", "Snag", "SnowConifer", "Meadow", "Reeds", "Fern", "Log",
                "Crag", "SnowCrag", "Outcrop",
            };
            for (int i = 0; i < stored.Length; i++)
                Assert.AreEqual(stored[i], Enum.GetName(typeof(FeatureKind), i), "Stored feature value " + i);
        }

        [TestCase(-90, -180, 0, -720)]
        [TestCase(0, 0, 0, 0)]
        [TestCase(90, 180, 1000000000, 1080)]
        public void LandmarkRequestsKeepTheirAuthoredCoordinatesSizeAndBearing(double latitude, double longitude, double size, double bearing)
        {
            FormationUse use = FormationUse.Landmark("simple_crater", latitude, longitude, size, bearing);
            Assert.AreEqual("simple_crater", use.formation);
            Assert.IsTrue(use.landmark);
            Assert.AreEqual(1, use.count);
            Assert.AreEqual(latitude, use.latitude);
            Assert.AreEqual(longitude, use.longitude);
            Assert.AreEqual(size, use.size);
            Assert.AreEqual(bearing, use.bearing);
        }

        [Test]
        public void EditingOneLandmarkRequestCannotChangeAnother()
        {
            FormationUse first = FormationUse.Landmark("simple_crater", 20, 30);
            FormationUse second = FormationUse.Landmark("simple_crater", 20, 30);
            first.formation = "complex_crater";
            first.count = 0;
            first.landmark = false;
            first.latitude = -40;
            first.longitude = -50;
            first.size = 100;
            first.bearing = -90;

            Assert.AreEqual("simple_crater", second.formation);
            Assert.AreEqual(1, second.count);
            Assert.IsTrue(second.landmark);
            Assert.AreEqual(20, second.latitude);
            Assert.AreEqual(30, second.longitude);
            Assert.AreEqual(0, second.size);
            Assert.AreEqual(90, second.bearing);
        }
    }
}
