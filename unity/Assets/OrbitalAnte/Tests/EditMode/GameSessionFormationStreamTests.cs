// GameSession is Unity-side. The standalone rules project still runs the equivalent ForStream cases
// in FormationGeneratorRulesTests; these additional cases require the real session type in Unity.
#if UNITY_5_3_OR_NEWER || UNITY_2021_3_OR_NEWER
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;

namespace OrbitalAnte.Tests
{
    [NonParallelizable]
    public class GameSessionFormationStreamTests
    {
        [TestCase(0)]
        [TestCase(-104729)]
        [TestCase(int.MaxValue)]
        public void SessionStreamsReproduceFormationLocationsWhenTheRunSeedIsRestored(int seed)
        {
            int originalSeed = GameSession.Seed;
            try
            {
                GameSession.SetSeed(seed);
                var terrain = new PlanetTerrain(new PlanetProfile(), "SessionStreamRuleTest", 500, seed);
                SeededRandom first = GameSession.Stream("tests.formation.locations");
                var locations = new Vec3d[16];
                for (int i = 0; i < locations.Length; i++)
                    locations[i] = FormationGenerator.PickLocation(terrain, Where.Any, first);

                GameSession.SetSeed(unchecked(seed + 1));
                SeededRandom otherRun = GameSession.Stream("tests.formation.locations");
                for (int i = 0; i < 20; i++) otherRun.Next();
                GameSession.SetSeed(seed);
                SeededRandom replay = GameSession.Stream("tests.formation.locations");
                for (int i = 0; i < locations.Length; i++)
                    Assert.AreEqual(locations[i], FormationGenerator.PickLocation(terrain, Where.Any, replay));
            }
            finally { GameSession.SetSeed(originalSeed); }
        }

        [Test]
        public void UnrelatedSessionStreamsCannotShiftTheNextFormationLocation()
        {
            int originalSeed = GameSession.Seed;
            try
            {
                GameSession.SetSeed(4242);
                var terrain = new PlanetTerrain(new PlanetProfile(), "SessionStreamRuleTest", 500, GameSession.Seed);
                SeededRandom first = GameSession.Stream("tests.formation.locations");
                Vec3d expected = FormationGenerator.PickLocation(terrain, Where.Equatorial, first);
                SeededRandom unrelated = GameSession.Stream("tests.base.orientation");
                for (int i = 0; i < 1000; i++) BaseKindsSeedTurn(unrelated);
                SeededRandom replay = GameSession.Stream("tests.formation.locations");
                Assert.AreEqual(expected, FormationGenerator.PickLocation(terrain, Where.Equatorial, replay));
            }
            finally { GameSession.SetSeed(originalSeed); }
        }

        static void BaseKindsSeedTurn(SeededRandom random) => OrbitalAnte.Bases.BaseKinds.QuarterTurns(random.Next());
    }
}
#endif
