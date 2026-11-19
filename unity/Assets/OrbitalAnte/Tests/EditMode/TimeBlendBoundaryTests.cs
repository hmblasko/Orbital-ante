using NUnit.Framework;
using OrbitalAnte.Core;

namespace OrbitalAnte.Tests
{
    public class TimeBlendBoundaryTests
    {
        [TestCase(float.NaN, 1f)]
        [TestCase(float.PositiveInfinity, 1f)]
        [TestCase(float.NegativeInfinity, 0f)]
        [TestCase(float.MinValue, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(float.MaxValue, 1f)]
        public void EachSpeedInputHasItsOwnFiniteUnitFallback(float input, float clamped)
        {
            Assert.AreEqual(clamped, TimeBlend.Scale(false, input, 1f, 1f), "Hitstop input");
            Assert.AreEqual(clamped, TimeBlend.Scale(false, 1f, input, 1f), "Headshot input");
            Assert.AreEqual(clamped, TimeBlend.Scale(false, 1f, 1f, input), "Breath input");
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(float.MaxValue)]
        public void PauseTakesPrecedenceEvenWithNonfiniteSpeeds(float input)
        {
            Assert.AreEqual(0f, TimeBlend.Scale(true, input, input, input));
        }

        [Test]
        public void ZeroHitstopWinsEvenOverUnspecifiedSmoothInputs()
        {
            Assert.AreEqual(0f, TimeBlend.Scale(false, 0f, float.NaN, float.NaN));
            Assert.AreEqual(0.25f, TimeBlend.Scale(false, 0.25f, 0f, 0f),
                "Hitstop is a replacement, so lower-priority frozen slows cannot multiply it to zero.");
        }

        [TestCase(0f)]
        [TestCase(0.02f)]
        [TestCase(1000000f)]
        public void PhysicsFloorAppliesAtAndBelowTheBoundary(float normalStep)
        {
            float floor = normalStep * TimeBlend.LeastStepShare;
            Assert.AreEqual(floor, TimeBlend.FixedStep(normalStep, TimeBlend.LeastStepShare, 1f));
            Assert.AreEqual(floor, TimeBlend.FixedStep(normalStep, 0f, 1f));
            Assert.AreEqual(floor, TimeBlend.FixedStep(normalStep, -1f, -1f));
            Assert.AreEqual(floor, TimeBlend.FixedStep(normalStep, float.NegativeInfinity, 1f));
            Assert.AreEqual(normalStep, TimeBlend.FixedStep(normalStep, float.NaN, float.PositiveInfinity));
        }

        [Test]
        public void SmoothInputsAreSymmetricAndPreviousCallsLeaveNoState()
        {
            float expectedScale = TimeBlend.Scale(false, 1f, 0.4f, 0.8f);
            float expectedStep = TimeBlend.FixedStep(0.02f, 0.4f, 0.8f);
            Assert.AreEqual(expectedScale, TimeBlend.Scale(false, 1f, 0.8f, 0.4f));
            Assert.AreEqual(expectedStep, TimeBlend.FixedStep(0.02f, 0.8f, 0.4f));
            for (int i = 0; i < 20; i++)
            {
                TimeBlend.Scale(true, 0f, float.NaN, float.NegativeInfinity);
                TimeBlend.FixedStep(0f, 0f, 0f);
                Assert.AreEqual(expectedScale, TimeBlend.Scale(false, 1f, 0.4f, 0.8f));
                Assert.AreEqual(expectedStep, TimeBlend.FixedStep(0.02f, 0.4f, 0.8f));
            }
        }
    }
}
