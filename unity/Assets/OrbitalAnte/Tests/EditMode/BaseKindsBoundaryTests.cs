using NUnit.Framework;
using OrbitalAnte.Bases;

namespace OrbitalAnte.Tests
{
    public class BaseKindsBoundaryTests
    {
        [TestCase(BaseSize.Small, int.MinValue, 0)]
        [TestCase(BaseSize.Small, int.MaxValue, 1)]
        [TestCase(BaseSize.Medium, -1000001, 1)]
        [TestCase(BaseSize.Medium, 1000000, 0)]
        [TestCase(BaseSize.Large, int.MinValue, 0)]
        [TestCase(BaseSize.Large, int.MaxValue, 0)]
        public void ExtremeKindNumbersWrapToTheExpectedExistingDesign(BaseSize size, int kind, int wrapped)
        {
            Assert.AreSame(BaseKinds.Of(size, wrapped), BaseKinds.Of(size, kind));
            Assert.AreSame(BaseKinds.Of(size, wrapped), BaseKinds.Of(new BaseSite { Size = size, Kind = kind }));
        }

        [TestCase(int.MinValue, BaseSize.Small)]
        [TestCase(-1, BaseSize.Small)]
        [TestCase(3, BaseSize.Large)]
        [TestCase(int.MaxValue, BaseSize.Large)]
        public void UnknownSizeSelectorsClampToAnExistingRow(int size, BaseSize clamped)
        {
            Assert.AreSame(BaseKinds.Of(clamped, 0), BaseKinds.Of((BaseSize)size, 0));
        }

        [Test]
        public void ReturnedContainersCannotOverwriteTheCatalogue()
        {
            int[] counts = BaseKinds.Counts;
            int smallCount = counts[0];
            counts[0] = 0;
            Assert.AreEqual(smallCount, BaseKinds.Counts[0]);

            BaseKind[] all = BaseKinds.All();
            BaseKind first = all[0];
            all[0] = null;
            Assert.AreSame(first, BaseKinds.All()[0]);
            Assert.AreSame(first, BaseKinds.Of(BaseSize.Small, 0));
        }

        [Test]
        public void FloorContractMatchesTheDefaultFoundationForEverySize()
        {
            var field = new BaseFieldRules();
            foreach (BaseSize size in new[] { BaseSize.Small, BaseSize.Medium, BaseSize.Large })
            {
                Assert.AreEqual(field.Footprint(size), BaseKinds.FloorRadius(size));
                Assert.Greater(BaseKinds.SolidTriangleCap(size), 0);
                Assert.Greater(BaseKinds.TriangleCap(size), BaseKinds.SolidTriangleCap(size));
            }
        }

        [TestCase(0, 3f, -4f)]
        [TestCase(1, -4f, -3f)]
        [TestCase(2, -3f, 4f)]
        [TestCase(3, 4f, 3f)]
        [TestCase(-1, 4f, 3f)]
        [TestCase(-4, 3f, -4f)]
        [TestCase(int.MinValue, 3f, -4f)]
        [TestCase(int.MaxValue, 4f, 3f)]
        public void TurningSocketCoordinatesWrapsAndKeepsTheClockwiseConvention(int turns, float x, float z)
        {
            BaseKinds.Turned(turns, 3f, -4f, out float actualX, out float actualZ);
            Assert.AreEqual(x, actualX);
            Assert.AreEqual(z, actualZ);
            BaseKinds.Turned(turns, 0f, 0f, out actualX, out actualZ);
            Assert.AreEqual(0f, actualX);
            Assert.AreEqual(0f, actualZ);
        }

        [TestCase(int.MinValue)]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(int.MaxValue)]
        public void SeedTurnsRemainValidAndIndependentOfCallHistory(int seed)
        {
            int first = BaseKinds.QuarterTurns(seed);
            Assert.That(first, Is.InRange(0, 3));
            for (int other = -50; other <= 50; other++) BaseKinds.QuarterTurns(other);
            Assert.AreEqual(first, BaseKinds.QuarterTurns(seed));
        }
    }
}
