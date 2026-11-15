using NUnit.Framework;
using OrbitalAnte.Enemies;

namespace OrbitalAnte.Tests
{
    public class EnemyShipFitTests
    {
        [TestCase(1.8, 0.28, true)]
        [TestCase(1.53, 0.238, true)]
        [TestCase(2.34, 0.364, false)]
        [TestCase(2.16, 0.336, false)]
        [TestCase(1.8, 0.3, true)]
        [TestCase(1.8, 0.31, false)]
        [TestCase(0.4, 0.3, false)]
        [TestCase(0, 0.28, false)]
        [TestCase(1.8, 0, false)]
        [TestCase(double.NaN, 0.28, false)]
        [TestCase(1.8, double.PositiveInfinity, false)]
        public void OnlyCapsulesSupportedByTheExistingCabinAndLiftCanBoard(double height, double radius, bool fits)
        {
            Assert.AreEqual(fits, EnemyShipRules.FitsCabin(height, radius, 1.8, 0.3));
        }
    }
}
