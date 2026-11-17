using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Boundary and query-order cases for existing planet rules. No Unity objects.
    /// These test drafts have not been run in Unity or a standalone C# runner.
    /// </summary>
    public class PlanetBoundaryRulesTests
    {
        static PlanetProfile DryAir()
        {
            return new PlanetProfile
            {
                hasAtmosphere = true,
                atmosphereHeight = 100,
                skyOpacity = 0.8,
                visibility = 1200,
                airDrag = 0.3,
                sunDimming = 0.2,
            };
        }

        static PlanetProfile CloudDeck()
        {
            return new PlanetProfile
            {
                hasAtmosphere = true,
                cloudDeck = true,
                cloudBase = 300,
                visibility = 900,
                airDrag = 0.2,
                sunDimming = 0.4,
            };
        }

        static void AssertVacuum(AirSample sample)
        {
            Assert.IsFalse(sample.InAtmosphere);
            Assert.IsFalse(sample.Underwater);
            Assert.AreEqual(0, sample.Sky);
            Assert.AreEqual(0, sample.Cloud);
            Assert.AreEqual(0, sample.Drag);
            Assert.AreEqual(0, sample.SunDimming);
            Assert.AreEqual(AtmosphereModel.ClearVisibility, sample.Visibility);
        }

        [Test]
        public void OrdinaryAtmosphere_EndsExactlyAtItsStatedTop()
        {
            PlanetProfile profile = DryAir();
            const double radius = 500;

            AssertVacuum(AtmosphereModel.At(profile, radius, radius, profile.atmosphereHeight));

            AirSample justInside = AtmosphereModel.At(profile, radius, radius, profile.atmosphereHeight - 0.001);
            Assert.IsTrue(justInside.InAtmosphere);
            Assert.Greater(justInside.Sky, 0);
            Assert.Greater(justInside.Drag, 0);
        }

        [Test]
        public void DryGroundBelowZero_DoesNotCreateDenserThanGroundLevelAir()
        {
            PlanetProfile profile = DryAir();
            AirSample ground = AtmosphereModel.At(profile, 500, 500, 0);
            AirSample valley = AtmosphereModel.At(profile, 500, 500, -100);

            Assert.IsTrue(valley.InAtmosphere);
            Assert.IsFalse(valley.Underwater);
            Assert.AreEqual(ground.Sky, valley.Sky);
            Assert.AreEqual(ground.SunDimming, valley.SunDimming);
            Assert.AreEqual(ground.Drag, valley.Drag);
            Assert.AreEqual(ground.Visibility, valley.Visibility);
        }

        [Test]
        public void SeaSurface_IsDryAtTheBoundary_AndWetImmediatelyBelow_EvenWithoutAir()
        {
            var profile = new PlanetProfile
            {
                hasAtmosphere = false,
                hasSea = true,
                seaLevel = 0,
                underwaterVisibility = 80,
            };

            AssertVacuum(AtmosphereModel.At(profile, 500, 500, 0));

            AirSample below = AtmosphereModel.At(profile, 500, 500, -0.001);
            Assert.IsTrue(below.Underwater);
            Assert.IsTrue(below.InAtmosphere, "The water path supplies the surrounding medium even on an airless body.");
            Assert.AreEqual(0.001, below.Depth, 1e-12);
            Assert.AreEqual(AtmosphereModel.WaterDrag, below.Drag);
            Assert.AreEqual(1, below.Sky);
        }

        [Test]
        public void GasGiant_OuterLeadBoundaryIsVacuum_AndCloudTopIsFullyOpaque()
        {
            PlanetProfile profile = CloudDeck();
            const double surfaceRadius = 1000;
            const double bodyRadius = 2000;
            double top = AtmosphereModel.Top(profile, surfaceRadius, bodyRadius);

            AssertVacuum(AtmosphereModel.At(profile, surfaceRadius, bodyRadius, top + bodyRadius * 0.04));

            AirSample atTop = AtmosphereModel.At(profile, surfaceRadius, bodyRadius, top);
            Assert.IsTrue(atTop.InAtmosphere);
            Assert.AreEqual(1, atTop.Cloud);
            Assert.AreEqual(1, atTop.Sky);
            Assert.AreEqual(AtmosphereModel.CloudVisibility, atTop.Visibility, 1e-9);
        }

        [Test]
        public void GasGiant_AtEndOfLowerCloudFade_ReturnsToItsClearAirVisibility()
        {
            PlanetProfile profile = CloudDeck();
            AirSample below = AtmosphereModel.At(profile, 1000, 2000, profile.cloudBase - AtmosphereModel.CloudFade);

            Assert.IsTrue(below.InAtmosphere);
            Assert.AreEqual(0, below.Cloud);
            Assert.AreEqual(1, below.Sky);
            Assert.AreEqual(profile.visibility, below.Visibility, 1e-9);
        }

        [TestCase(0)]
        [TestCase(-100)]
        public void WeatherWithoutCells_IsClearAtAnyQueryTime(double time)
        {
            var profile = new PlanetProfile();
            // A spec with no cells must also remain empty, not only an empty weather list.
            profile.weather.Add(new WeatherSpec { cells = 0 });
            var weather = new WeatherModel(profile, "BoundaryTest", 500, 4242);

            WeatherSample sample = weather.At(Vec3d.Up, time);
            Assert.AreEqual(0, weather.Count);
            Assert.IsFalse(weather.Any);
            Assert.AreEqual(0, sample.Intensity);
            Assert.IsNull(sample.Spec);
            Assert.AreEqual(Vec3d.Zero, sample.Wind);
        }

        [Test]
        public void PermanentStorm_QueryingTimesOutOfOrderDoesNotAdvanceItsState()
        {
            var profile = new PlanetProfile();
            var spec = new WeatherSpec { cells = 1, permanent = true, latitude = 30, cellRadius = 100, windSpeed = 12 };
            profile.weather.Add(spec);
            var weather = new WeatherModel(profile, "BoundaryTest", 1000, 4242);
            var same = new WeatherModel(profile, "BoundaryTest", 1000, 4242);

            const double time = 50;
            Vec3d centre = weather.Centre(0, time);
            WeatherSample before = weather.At(centre, time);
            weather.At(weather.Centre(0, 10000), 10000);
            weather.At(weather.Centre(0, -100), -100);
            WeatherSample after = weather.At(centre, time);

            Assert.AreEqual(1, weather.Strength(0, -100));
            Assert.AreEqual(1, weather.Strength(0, 10000));
            Assert.AreEqual(centre, weather.Centre(0, time));
            Assert.AreEqual(centre, same.Centre(0, time));
            Assert.AreEqual(1, before.Intensity);
            Assert.AreSame(spec, before.Spec);
            Assert.AreEqual(before.Intensity, after.Intensity);
            Assert.AreEqual(before.Wind, after.Wind);
            Assert.AreEqual(before.Wind, same.At(centre, time).Wind);
        }

        [Test]
        public void QueryingFarOutsideAStorm_DoesNotLeakItsSpecOrWind()
        {
            var profile = new PlanetProfile();
            profile.weather.Add(new WeatherSpec { cells = 1, permanent = true, cellRadius = 100, windSpeed = 10 });
            var weather = new WeatherModel(profile, "BoundaryTest", 1000, 4242);
            Vec3d centre = weather.Centre(0, 0);

            Assert.AreEqual(1, weather.At(centre, 0).Intensity);
            WeatherSample farAway = weather.At(-centre, 0);
            Assert.AreEqual(0, farAway.Intensity);
            Assert.IsNull(farAway.Spec);
            Assert.AreEqual(Vec3d.Zero, farAway.Wind);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonpositiveFinestCell_CannotSplitPastTheSafetyCap(double finestCell)
        {
            int level = TileRules.MaxLevel(500, finestCell);
            Assert.AreEqual(TileRules.DeepestLevel, level);
            Assert.IsFalse(TileRules.WantsSplit(500, level, level, Vec3d.Zero, Vec3d.Zero, 1.2, true));
        }

        [Test]
        public void TileHysteresis_KeepsAnExistingSplitWhereANewTileWouldNotSplit()
        {
            const double radius = 500;
            const int level = 1;
            const double splitDistance = 1.2;
            double size = TerrainMesher.TileSize(radius, level);
            Vec3d centre = Vec3d.Forward * radius;
            // Just outside a new split's reach, but still inside the retained-split margin.
            Vec3d viewer = centre + Vec3d.Right * (size * (0.5 + splitDistance * 1.05));

            Assert.IsFalse(TileRules.WantsSplit(radius, level, level + 1, centre, viewer, splitDistance, false));
            Assert.IsTrue(TileRules.WantsSplit(radius, level, level + 1, centre, viewer, splitDistance, true));
        }
    }
}
