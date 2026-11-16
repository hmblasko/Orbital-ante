using NUnit.Framework;
using OrbitalAnte.Look;

namespace OrbitalAnte.Tests
{
    public class FinishSettingsTests
    {
        // Different values for every constructor position make accidental swaps/omissions visible.
        static FinishSettings Settings(int changed = -1) => new FinishSettings(
            changed == 0 ? false : true,
            changed == 1 ? 0.7f : 0.6f,
            changed == 2 ? 1.4f : 1.3f,
            changed == 3 ? 2 : 1,
            changed == 4 ? -0.3f : -0.2f,
            changed == 5 ? 18f : 17f,
            changed == 6 ? -12f : -13f,
            changed == 7 ? -6f : -7f,
            changed == 8 ? 0.3f : 0.2f,
            changed == 9 ? 0.5f : 0.4f,
            changed == 10 ? 1 : 2);

        [Test]
        public void IdenticalSnapshotsNeedNoReapplyAfterRepeatedComparisons()
        {
            FinishSettings previous = Settings();
            for (int i = 0; i < 10000; i++) Assert.IsTrue(previous.Equals(Settings()));
            Assert.AreEqual(previous.GetHashCode(), Settings().GetHashCode());
        }

        [TestCase(0, "finish")]
        [TestCase(1, "bloom")]
        [TestCase(2, "bloom threshold")]
        [TestCase(3, "tone mapping")]
        [TestCase(4, "exposure")]
        [TestCase(5, "contrast")]
        [TestCase(6, "saturation")]
        [TestCase(7, "warmth")]
        [TestCase(8, "split tone")]
        [TestCase(9, "vignette")]
        [TestCase(10, "edge smoothing")]
        public void EveryFinishDialIndependentlyInvalidatesTheSnapshot(int changed, string dial)
        {
            FinishSettings previous = Settings(), edited = Settings(changed);
            Assert.IsFalse(previous.Equals(edited), dial);
            Assert.IsFalse(edited.Equals(previous), "Equality must be symmetric.");
            Assert.IsTrue(edited.Equals(Settings(changed)), "An applied edit must settle.");
            Assert.IsFalse(edited.Equals(Settings()), "Reverting the dial must also be detected.");
        }

        [Test]
        public void ZeroedAndDefaultSnapshotsAgree()
        {
            var zero = new FinishSettings(false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            Assert.IsTrue(default(FinishSettings).Equals(zero));
            Assert.AreEqual(default(FinishSettings).GetHashCode(), zero.GetHashCode());
            Assert.IsFalse(zero.Equals(Settings()));
        }

        [Test]
        public void SmallRepresentableEditsAreNotHiddenByAnApproximateComparison()
        {
            var first = new FinishSettings(true, 1f, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            var next = new FinishSettings(true, 1.0000001f, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            Assert.IsFalse(first.Equals(next));
        }

        [TestCase(-100f)]
        [TestCase(float.MaxValue)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void UnusualStoredNumbersSettleWithoutReapplyingForever(float value)
        {
            // Comparison records raw inputs; validation and effect clamping belong to the existing pipeline.
            var a = new FinishSettings(true, value, value, 0, value, value, value, value, value, value, 0);
            var b = new FinishSettings(true, value, value, 0, value, value, value, value, value, value, 0);
            Assert.IsTrue(a.Equals(b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.IsFalse(a.Equals(default(FinishSettings)));
        }

        [TestCase(-1)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void UnrecognizedEnumIntegersAreStillComparedExactly(int value)
        {
            var normal = new FinishSettings(true, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            var tone = new FinishSettings(true, 0, 0, value, 0, 0, 0, 0, 0, 0, 0);
            var edge = new FinishSettings(true, 0, 0, 0, 0, 0, 0, 0, 0, 0, value);
            Assert.IsFalse(normal.Equals(tone));
            Assert.IsFalse(normal.Equals(edge));
            Assert.IsFalse(tone.Equals(edge));
        }

        [Test]
        public void BoxedEqualityRejectsNullAndOtherTypes()
        {
            FinishSettings value = Settings();
            Assert.IsTrue(value.Equals((object)Settings()));
            Assert.IsFalse(value.Equals((object)null));
            Assert.IsFalse(value.Equals((object)0));
        }

        [Test]
        public void PositiveAndNegativeZeroDoNotCreateASpuriousEdit()
        {
            var a = new FinishSettings(true, 0f, 0f, 0, 0f, 0f, 0f, 0f, 0f, 0f, 0);
            var b = new FinishSettings(true, -0f, -0f, 0, -0f, -0f, -0f, -0f, -0f, -0f, 0);
            Assert.IsTrue(a.Equals(b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }
    }
}
