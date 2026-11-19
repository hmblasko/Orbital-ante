using NUnit.Framework;
using OrbitalAnte.Navigation;
using OrbitalAnte.Ship;
using OrbitalAnte.UI;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>The HUD clean-up's rules (17 Nov 2026): what shows when, and the words and numbers Run Stats uses.</summary>
    public sealed class HudRulesTests
    {
        [TestCase(0L, "0")]
        [TestCase(999L, "999")]
        [TestCase(1000L, "1,000")]
        [TestCase(1234567L, "1,234,567")]
        public void Count_PutsCommasInBigNumbers(long n, string expected)
        {
            Assert.AreEqual(expected, StatsFormat.Count(n));
        }

        [Test]
        public void Percent_NeverSaysFullWhenItIsNot_OrEmptyWhenItIsNot()
        {
            Assert.AreEqual("100%", StatsFormat.Percent(1f));
            Assert.AreEqual("99%", StatsFormat.Percent(0.999f));
            Assert.AreEqual("1%", StatsFormat.Percent(0.001f));
            Assert.AreEqual("0%", StatsFormat.Percent(0f));
            Assert.AreEqual("0%", StatsFormat.Percent(-0.2f));
            Assert.AreEqual("54%", StatsFormat.Percent(0.54f));
            Assert.AreEqual("0%", StatsFormat.Percent(float.NaN));
        }

        [TestCase(0.0, "0:00")]
        [TestCase(7.9, "0:07")]
        [TestCase(761.0, "12:41")]
        [TestCase(3725.0, "1:02:05")]
        [TestCase(-5.0, "0:00")]
        public void Clock_ReadsLikeARunTimer(double seconds, string expected)
        {
            Assert.AreEqual(expected, StatsFormat.Clock(seconds));
        }

        [TestCase(30.0, "under a minute")]
        [TestCase(720.0, "12 min")]
        [TestCase(22320.0, "6 h 12 min")]
        public void Played_IsHoursAndMinutes(double seconds, string expected)
        {
            Assert.AreEqual(expected, StatsFormat.Played(seconds));
        }

        [Test]
        public void Speed_HasTenthsOnlyWhereTheyMatter()
        {
            Assert.AreEqual("2.4 m/s", StatsFormat.Speed(2.44));
            Assert.AreEqual("99.9 m/s", StatsFormat.Speed(99.9));
            Assert.AreEqual("212 m/s", StatsFormat.Speed(212.3));
            Assert.AreEqual("3,204 m/s", StatsFormat.Speed(3204));
            Assert.AreEqual("5.0 m/s", StatsFormat.Speed(-5), "a speed is never negative");
        }

        [Test]
        public void Distance_SplitsIntoANumberAndItsUnit()
        {
            StatsFormat.Distance(640, out string n, out string u);
            Assert.AreEqual("640", n);
            Assert.AreEqual("m", u);
            StatsFormat.Distance(8100, out n, out u);
            Assert.AreEqual("8.1", n);
            Assert.AreEqual("km", u);
            StatsFormat.Distance(1904000, out n, out u);
            Assert.AreEqual("1,904", n);
            Assert.AreEqual("km", u);
            Assert.AreEqual("1.2 km", StatsFormat.Distance(1240));
        }

        [Test]
        public void KeyLists_StayUp_ThenFade_ThenGo()
        {
            Assert.AreEqual(1f, HudRules.HelpAlpha(0f));
            Assert.AreEqual(1f, HudRules.HelpAlpha(HudRules.HelpSeconds));
            float half = HudRules.HelpAlpha(HudRules.HelpSeconds + HudRules.HelpFade / 2f);
            Assert.That(half, Is.GreaterThan(0.4f).And.LessThan(0.6f));
            Assert.AreEqual(0f, HudRules.HelpAlpha(HudRules.HelpSeconds + HudRules.HelpFade));
            Assert.AreEqual(0f, HudRules.HelpAlpha(600f));
        }

        [Test]
        public void OrbitAid_ShowsOnlyWhileAnOrbitIsWhatYouAreDoing()
        {
            // Hovering over the pad or landing: nothing to say about orbits.
            Assert.IsFalse(HudRules.ShowOrbit(OrbitKind.MeetsGround, 5, 3200));
            Assert.IsFalse(HudRules.ShowOrbit(OrbitKind.None, 3000, 3200));
            // Going across fast enough that an orbit is in reach.
            Assert.IsTrue(HudRules.ShowOrbit(OrbitKind.MeetsGround, 1100, 3200));
            Assert.IsTrue(HudRules.ShowOrbit(OrbitKind.Leaves, 4500, 3200));
            // On a loop, always.
            Assert.IsTrue(HudRules.ShowOrbit(OrbitKind.Orbit, 3200, 3200));
            Assert.IsTrue(HudRules.ShowOrbit(OrbitKind.Tugged, 3600, 3200));
        }

        [Test]
        public void OrbitBar_PutsACirclesSpeedAtTheNotch_AndStaysOnTheBar()
        {
            Assert.AreEqual(HudRules.CircleMark, HudRules.OrbitBar(3200, 3200), 1e-5f);
            Assert.AreEqual(0f, HudRules.OrbitBar(0, 3200));
            Assert.AreEqual(1f, HudRules.OrbitBar(9000, 3200));
            Assert.AreEqual(0f, HudRules.OrbitBar(100, 0), "no world, no bar");
            Assert.AreEqual(HudRules.OrbitBar(1000, 3200), HudRules.OrbitBar(-1000, 3200), "either way across counts");
        }

        [Test]
        public void OrbitWords_SayWhatThePathDoes()
        {
            StringAssert.StartsWith("IN ORBIT", HudRules.OrbitWords(OrbitKind.Orbit, 12000, 40000, "Sun"));
            StringAssert.Contains("12.0 km", HudRules.OrbitWords(OrbitKind.Orbit, 12000, 40000, "Sun"));
            StringAssert.Contains("Mars", HudRules.OrbitWords(OrbitKind.Tugged, 0, 0, "Mars"));
            StringAssert.Contains("faster", HudRules.OrbitWords(OrbitKind.MeetsGround, 0, 0, "Sun"));
            StringAssert.Contains("slow down", HudRules.OrbitWords(OrbitKind.Leaves, 0, 0, "Sun"));
            Assert.AreEqual(string.Empty, HudRules.OrbitWords(OrbitKind.None, 0, 0, "Sun"));
        }

        [Test]
        public void SinkRate_ShowsNearTheGround_AndWarnsWhenTheDashDoes()
        {
            Assert.IsTrue(HudRules.ShowSink(300, -4f));
            Assert.IsFalse(HudRules.ShowSink(5000, -40f), "high up the climb rate is not news");
            Assert.IsFalse(HudRules.ShowSink(-1, -4f), "no ground known");
            Assert.IsFalse(HudRules.ShowSink(300, 0.1f), "hovering");
            Assert.AreEqual("down 4.2 m/s", HudRules.SinkWords(-4.2f));
            Assert.AreEqual("up 3.0 m/s", HudRules.SinkWords(3f));
            // 12 m/s down from 50 m is 4 s from the ground: too fast for a 4 m/s touchdown, inside 6 s.
            Assert.IsTrue(HudRules.SinkTooFast(50, -12f, 4f, 6f));
            Assert.IsFalse(HudRules.SinkTooFast(500, -12f, 4f, 6f), "still far enough to slow down");
            Assert.IsFalse(HudRules.SinkTooFast(50, -3f, 4f, 6f), "slow enough to land");
            Assert.IsFalse(HudRules.SinkTooFast(-1, -12f, 4f, 6f));
        }

        [Test]
        public void Ladder_ShowsPipsForShortSteps_AndWordsForLongOnes()
        {
            Assert.IsTrue(HudRules.LadderAsPips(4));
            Assert.IsTrue(HudRules.LadderAsPips(HudRules.MaxPips));
            Assert.IsFalse(HudRules.LadderAsPips(HudRules.MaxPips + 1));
            Assert.IsFalse(HudRules.LadderAsPips(0));
            Assert.AreEqual("SHOTGUN", HudRules.LadderWords(false, "Shotgun", 3, 4));
            Assert.AreEqual("12 KILLS TO SHOTGUN", HudRules.LadderWords(false, "Shotgun", 12, 20));
            Assert.AreEqual("1 KILL TO SHOTGUN", HudRules.LadderWords(false, "Shotgun", 1, 20));
            Assert.AreEqual("LAST GUN", HudRules.LadderWords(true, null, 0, 0));
        }

        [Test]
        public void ShipParts_AreBarsWhileTheyWork_AndRedWordsOnceTheyDoNot()
        {
            HudRules.PartRow dented = HudRules.ShipPart(PartState.Damaged, 0.81f, 0f);
            Assert.IsNull(dented.Tag);
            Assert.AreEqual(0.81f, dented.Bar, 1e-5f);
            Assert.AreEqual(StatTone.Plain, dented.Tone);
            Assert.AreEqual(StatTone.Warn, HudRules.ShipPart(PartState.Damaged, 0.3f, 0f).Tone);
            Assert.AreEqual("BROKEN", HudRules.ShipPart(PartState.Broken, 0f, 0.2f).Tag);
            Assert.AreEqual("COMING LOOSE", HudRules.ShipPart(PartState.Broken, 0f, 0.8f).Tag);
            Assert.AreEqual("GONE", HudRules.ShipPart(PartState.Lost, 0f, 0f).Tag);
            Assert.AreEqual(StatTone.Bad, HudRules.ShipPart(PartState.Lost, 0f, 0f).Tone);
            Assert.AreEqual("54%", HudRules.ShipPartStat("Thruster 2", PartState.Damaged, 0.54f, 0f).Value);
            Assert.AreEqual("BROKEN", HudRules.ShipPartStat("Leg 1", PartState.Broken, 0f, 0f).Value);
        }

        [Test]
        public void StatsSheet_FitsThreeColumns_AndSaysWhatItLeftOut()
        {
            var sheet = new StatsSheet();
            sheet.Section("EMPTY");
            StatSection big = sheet.Section("SHIP");
            for (int i = 0; i < StatsSheet.MaxRows + 3; i++) big.Add("Part " + i, "50%");
            sheet.Section("A").Add("x", "1");
            sheet.Section("B").Add("y", "2");
            sheet.Section("C").Add("z", "3");

            var columns = sheet.Columns();
            Assert.AreEqual(StatsSheet.MaxColumns, columns.Count, "three at most, empty ones skipped");
            Assert.AreEqual("SHIP", columns[0].Title);
            Assert.AreEqual(StatsSheet.MaxRows, columns[0].Rows.Count, "a long section is cut to fit");
            Assert.AreEqual("and 4 more", columns[0].Rows[StatsSheet.MaxRows - 1].Label);
            Assert.AreEqual("A", columns[1].Title);
            Assert.AreEqual("B", columns[2].Title);
        }
    }
}
