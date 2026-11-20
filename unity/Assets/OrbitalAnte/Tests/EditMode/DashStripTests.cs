using NUnit.Framework;
using OrbitalAnte.Navigation;
using OrbitalAnte.UI;

namespace OrbitalAnte.Tests.EditMode
{
    public class DashStripTests
    {
        [Test]
        public void Speed_IsSplitIntoNumberAndUnit_TenthsOnlyWhenSlow()
        {
            DashStrip.SplitSpeed(4.26, out string n, out string u);
            Assert.AreEqual("4.3", n);
            Assert.AreEqual("M/S", u);
            DashStrip.SplitSpeed(212.4, out n, out u);
            Assert.AreEqual("212", n);
            DashStrip.SplitSpeed(12500, out n, out u);
            Assert.AreEqual("12.5", n);
            Assert.AreEqual("KM/S", u);
        }

        [Test]
        public void Distance_UsesMetresThenKilometres_WithCommasWhenLarge()
        {
            DashStrip.SplitDistance(412, out string n, out string u);
            Assert.AreEqual("412", n);
            Assert.AreEqual("M", u);
            DashStrip.SplitDistance(1240, out n, out u);
            Assert.AreEqual("1.2", n);
            Assert.AreEqual("KM", u);
            DashStrip.SplitDistance(1904000, out n, out u);
            Assert.AreEqual("1,904", n);
        }

        [Test]
        public void ALamp_FlashesAsItComesOn_ThenStaysLit()
        {
            Assert.IsFalse(DashStrip.LampLit(false, 5f));
            Assert.IsTrue(DashStrip.LampLit(true, 0f));
            Assert.IsFalse(DashStrip.LampLit(true, DashStrip.FlashPeriod * 0.75f));
            Assert.IsTrue(DashStrip.LampLit(true, DashStrip.FlashSeconds));
            Assert.IsTrue(DashStrip.LampLit(true, 30f));
        }

        [Test]
        public void OrbitBar_ReachesTheNotch_AtACirclesSpeed()
        {
            DashStrip.OrbitBar(3200, 3200, out float fill, out float notch);
            Assert.AreEqual(notch, fill, 1e-5f);
            DashStrip.OrbitBar(99999, 3200, out fill, out _);
            Assert.AreEqual(1f, fill);
            DashStrip.OrbitBar(500, 0, out fill, out _);
            Assert.AreEqual(0f, fill);
        }

        [Test]
        public void OnlyARealOrbit_IsGoodNews()
        {
            Assert.AreEqual(DashTone.Good, DashStrip.OrbitTone(OrbitKind.Orbit));
            Assert.AreEqual(DashTone.Watch, DashStrip.OrbitTone(OrbitKind.MeetsGround));
            Assert.AreEqual("FALLING", DashStrip.OrbitWord(OrbitKind.MeetsGround));
            Assert.AreEqual(DashTone.Act, DashStrip.LampTone(DashLamp.Brake, OrbitKind.None));
        }

        [Test]
        public void Keys_ShowAfterSittingDown_ThenFadeAway()
        {
            Assert.AreEqual(1f, DashStrip.KeysAlpha(0f));
            Assert.AreEqual(1f, DashStrip.KeysAlpha(DashStrip.KeysShownSeconds));
            Assert.AreEqual(0.5f, DashStrip.KeysAlpha(DashStrip.KeysShownSeconds + DashStrip.KeysFadeSeconds * 0.5f), 1e-4f);
            Assert.AreEqual(0f, DashStrip.KeysAlpha(DashStrip.KeysShownSeconds + DashStrip.KeysFadeSeconds));
        }

        [Test]
        public void Readings_RollTowardTheTruth_AndLandOnItExactly()
        {
            double shown = 100;
            for (int i = 0; i < 120; i++) shown = DashStrip.RollToward(shown, 212, 1f / 60f);
            Assert.AreEqual(212.0, shown);
            double step = DashStrip.RollToward(100, 212, 1f / 60f);
            Assert.That(step, Is.GreaterThan(100).And.LessThan(212));
            Assert.AreEqual(5e6, DashStrip.RollToward(3, 5e6, 1f / 60f), "a huge jump is taken at once");
            Assert.AreEqual(7.0, DashStrip.RollToward(double.NaN, 7, 0.1f));
        }

        [Test]
        public void Keys_ChangeExactlyWhenTheShownTextChanges()
        {
            double[] speeds = { 0.04, 0.06, 4.24, 4.26, 99.94, 99.96, 100.4, 100.6, 9999.4, 9999.6, 12440, 12460 };
            double[] heights = { 4.4, 4.6, 999.4, 999.6, 1240, 1260, 99940, 99960, 1904400, 1904600 };
            for (int i = 1; i < speeds.Length; i++)
            {
                DashStrip.SplitSpeed(speeds[i - 1], out string a, out string au);
                DashStrip.SplitSpeed(speeds[i], out string b, out string bu);
                Assert.AreEqual(a + au == b + bu, DashStrip.SpeedKey(speeds[i - 1]) == DashStrip.SpeedKey(speeds[i]), speeds[i].ToString());
            }
            for (int i = 1; i < heights.Length; i++)
            {
                DashStrip.SplitDistance(heights[i - 1], out string a, out string au);
                DashStrip.SplitDistance(heights[i], out string b, out string bu);
                Assert.AreEqual(a + au == b + bu, DashStrip.DistanceKey(heights[i - 1]) == DashStrip.DistanceKey(heights[i]), heights[i].ToString());
            }
        }
    }
}
