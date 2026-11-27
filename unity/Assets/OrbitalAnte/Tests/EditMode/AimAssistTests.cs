using NUnit.Framework;
using OrbitalAnte.Player;

namespace OrbitalAnte.Tests
{
    /// <summary>Controller aim assist: it only ever slows the sticks over a target, never pulls, and never caught.</summary>
    public class AimAssistTests
    {
        static readonly AimAssistRules Rules = AimAssistRules.Default;

        [Test]
        public void RightOnATarget_TheSticksTurnAtTheSlowestShare()
        {
            Assert.That(AimAssist.Closeness(0f, 15f, 100f, Rules), Is.EqualTo(1f).Within(1e-5));
            var assist = new AimAssist();
            assist.Clear();
            for (int i = 0; i < 60; i++) assist.Step(1f / 60f, 1f, Rules);
            Assert.That(assist.Scale, Is.EqualTo(Rules.slowest).Within(0.01f));
        }

        [Test]
        public void OutsideTheBand_OrOutOfRange_ThereIsNoHelp()
        {
            float band = AimAssist.Band(20f, Rules);
            Assert.That(AimAssist.Closeness(band + 0.01f, 20f, 100f, Rules), Is.EqualTo(0f));
            Assert.That(AimAssist.Closeness(0f, Rules.range + 1f, 1000f, Rules), Is.EqualTo(0f));
            // A gun with a shorter reach than the assist is not helped beyond it.
            Assert.That(AimAssist.Closeness(0f, 30f, 25f, Rules), Is.EqualTo(0f));
            Assert.That(AimAssist.Closeness(float.NaN, 10f, 100f, Rules), Is.EqualTo(0f));
        }

        [Test]
        public void TheBand_IsTheTargetsReachSeenFromHere_WithinItsLimits()
        {
            // Close up the band is capped wide, far off it never shrinks below the floor.
            Assert.That(AimAssist.Band(1f, Rules), Is.EqualTo(Rules.maxDegrees).Within(1e-4));
            Assert.That(AimAssist.Band(500f, Rules), Is.EqualTo(Rules.minDegrees).Within(1e-4));
            float mid = AimAssist.Band(10f, Rules);
            Assert.That(mid, Is.EqualTo(System.Math.Atan2(Rules.reach, 10.0) * 180.0 / System.Math.PI).Within(1e-3));
        }

        [Test]
        public void Closeness_FallsSmoothly_FromTheMiddleToTheEdge()
        {
            float band = AimAssist.Band(12f, Rules);
            float last = 1.01f;
            for (int i = 0; i <= 20; i++)
            {
                float c = AimAssist.Closeness(band * i / 20f, 12f, 100f, Rules);
                Assert.That(c, Is.LessThanOrEqualTo(last));
                last = c;
            }
            Assert.That(last, Is.EqualTo(0f).Within(1e-5));
        }

        [Test]
        public void TheSlowdown_EasesInAndOut_AndNeverSpeedsTheSticksUp()
        {
            var assist = new AimAssist();
            float first = assist.Step(1f / 60f, 1f, Rules);
            Assert.That(first, Is.LessThan(1f));
            Assert.That(first, Is.GreaterThan(Rules.slowest), "one frame on a target must not drop straight to the slowest");
            for (int i = 0; i < 60; i++) assist.Step(1f / 60f, 1f, Rules);
            for (int i = 0; i < 120; i++)
            {
                float s = assist.Step(1f / 60f, 0f, Rules);
                Assert.That(s, Is.LessThanOrEqualTo(1f));
                Assert.That(s, Is.GreaterThanOrEqualTo(Rules.slowest));
            }
            Assert.That(assist.Scale, Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void BrokenDials_ArePutBackInRange()
        {
            var bad = new AimAssistRules { enabled = true, reach = float.NaN, minDegrees = 50f, maxDegrees = -3f, slowest = 0f, range = float.PositiveInfinity, ease = -1f }.Safe();
            Assert.That(bad.reach, Is.EqualTo(0.55f));
            Assert.That(bad.maxDegrees, Is.GreaterThanOrEqualTo(bad.minDegrees));
            Assert.That(bad.slowest, Is.GreaterThanOrEqualTo(0.1f));
            Assert.That(bad.range, Is.EqualTo(70f));
            Assert.That(bad.ease, Is.EqualTo(0f));
            // An all-zero struct (an asset saved before the field existed would load the initializer, but be safe)
            // never freezes the sticks.
            var zero = new AimAssistRules().Safe();
            Assert.That(zero.slowest, Is.GreaterThanOrEqualTo(0.1f));
        }
    }
}
