using NUnit.Framework;
using OrbitalAnte.UI;

namespace OrbitalAnte.Tests
{
    /// <summary>The visor gauges' numbers: suit plates, per cents and the air clock.</summary>
    public class VisorReadoutTests
    {
        [Test]
        public void Plates_GoDarkOnlyWhenUsedUp()
        {
            Assert.AreEqual(10, VisorReadout.PlatesLeft(1f));
            Assert.AreEqual(10, VisorReadout.PlatesLeft(0.95f));
            Assert.AreEqual(9, VisorReadout.PlatesLeft(0.9f));
            Assert.AreEqual(7, VisorReadout.PlatesLeft(0.62f));
            Assert.AreEqual(3, VisorReadout.PlatesLeft(0.3f));
            Assert.AreEqual(1, VisorReadout.PlatesLeft(0.001f));
            Assert.AreEqual(0, VisorReadout.PlatesLeft(0f));
            Assert.AreEqual(0, VisorReadout.PlatesLeft(-1f));
            Assert.AreEqual(0, VisorReadout.PlatesLeft(float.NaN));
        }

        [Test]
        public void PlatesNeverRise_AsTheSuitIsWornDown()
        {
            int last = VisorReadout.Plates;
            for (float f = 1f; f >= 0f; f -= 0.0013f)
            {
                int n = VisorReadout.PlatesLeft(f);
                Assert.LessOrEqual(n, last);
                last = n;
            }
        }

        [Test]
        public void TheLastPlate_TurnsAmberOnlyOnceItIsWornInto()
        {
            Assert.IsFalse(VisorReadout.LastPlateWorn(1f));
            Assert.IsFalse(VisorReadout.LastPlateWorn(0.7f), "seven whole plates");
            Assert.IsTrue(VisorReadout.LastPlateWorn(0.62f));
            Assert.AreEqual(0.2f, VisorReadout.LastPlate(0.62f), 1e-4f);
            Assert.IsFalse(VisorReadout.LastPlateWorn(0f));
        }

        [Test]
        public void SuitTone_IsADangerAtAThirdOrLess()
        {
            Assert.AreEqual(GaugeTone.Calm, VisorReadout.SuitTone(1f));
            Assert.AreEqual(GaugeTone.Warn, VisorReadout.SuitTone(0.62f));
            Assert.AreEqual(GaugeTone.Alarm, VisorReadout.SuitTone(0.35f));
            Assert.AreEqual(GaugeTone.Alarm, VisorReadout.SuitTone(0f));
        }

        [Test]
        public void Percent_NeverReadsZeroWhileAnythingIsLeft()
        {
            Assert.AreEqual(100, VisorReadout.Percent(1f));
            Assert.AreEqual(38, VisorReadout.Percent(0.38f));
            Assert.AreEqual(1, VisorReadout.Percent(0.0001f));
            Assert.AreEqual(0, VisorReadout.Percent(0f));
            Assert.AreEqual(100, VisorReadout.Percent(1.5f));
        }

        [Test]
        public void AirLine_GivesMinutesAndSeconds_ThenTheLastBreathCountdown()
        {
            // Eight minutes of air: 38% is 182.4 s, shown as 3:02 (never rounded up).
            Assert.AreEqual("3:02 left", VisorReadout.AirLine(0.38f, 8f, false, -1f));
            Assert.AreEqual("8:00 left", VisorReadout.AirLine(1f, 8f, false, -1f));
            Assert.AreEqual("0:04 left", VisorReadout.AirLine(0.01f, 8f, false, -1f));
            Assert.AreEqual("NO AIR  9", VisorReadout.AirLine(0f, 8f, true, 8.2f));
            Assert.AreEqual("NO AIR  0", VisorReadout.AirLine(0f, 8f, true, -3f));
            // Burning air on the jets: no resting-breath clock, which would be far too long.
            Assert.AreEqual(VisorReadout.JetsOnAir, VisorReadout.AirLine(1f, 8f, false, -1f, true));
        }

        [Test]
        public void AirTone_FollowsTheSuitsOwnWarnings()
        {
            Assert.AreEqual(GaugeTone.Calm, VisorReadout.AirTone(false, false, false));
            Assert.AreEqual(GaugeTone.Warn, VisorReadout.AirTone(false, false, true));
            Assert.AreEqual(GaugeTone.Alarm, VisorReadout.AirTone(true, false, false));
            Assert.AreEqual(GaugeTone.Alarm, VisorReadout.AirTone(false, true, false));
        }

        [Test]
        public void SuitLine_OnlySpeaksUpOnceItIsADanger()
        {
            Assert.AreEqual(string.Empty, VisorReadout.SuitLine(1f));
            Assert.AreEqual(string.Empty, VisorReadout.SuitLine(0.62f));
            Assert.AreEqual(VisorReadout.ArmourFailing, VisorReadout.SuitLine(0.3f));
            Assert.AreEqual(VisorReadout.NoArmour, VisorReadout.SuitLine(0f));
        }
    }
}
