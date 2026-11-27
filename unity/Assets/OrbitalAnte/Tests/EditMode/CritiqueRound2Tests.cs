using NUnit.Framework;
using OrbitalAnte.Tasks;
using OrbitalAnte.UI;

namespace OrbitalAnte.Tests
{
    /// <summary>Critique loop round 2 (26 Nov 2026): the readout in a fight, and one press from dying to retrying.</summary>
    public class CritiqueRound2Tests
    {
        [Test]
        public void AFight_IsAnEnemyHavingSeenYouLately_AndEndsByItself()
        {
            Assert.IsTrue(ReadoutFocus.InFight(0f));
            Assert.IsTrue(ReadoutFocus.InFight(ReadoutFocus.FightHold - 0.01f));
            Assert.IsFalse(ReadoutFocus.InFight(ReadoutFocus.FightHold));
            Assert.IsFalse(ReadoutFocus.InFight(1000f), "a run where nobody has seen the player is never a fight");
            Assert.IsFalse(ReadoutFocus.InFight(-1f), "a sighting stamped after now (a re-used clock) is not a fight");
        }

        [Test]
        public void InAFight_TheReadoutKeepsTwoPointLines_AndTheTaskLineWaits()
        {
            Assert.AreEqual(2, ReadoutFocus.Pops(true));
            Assert.AreEqual(4, ReadoutFocus.Pops(false));
            Assert.IsFalse(ReadoutFocus.ShowsTaskLine(true));
            Assert.IsTrue(ReadoutFocus.ShowsTaskLine(false));
        }

        [Test]
        public void InAFight_OnlyTheTipsAboutTheClockCutIn()
        {
            foreach (GuideTip tip in RunGuide.All)
            {
                Assert.IsTrue(ReadoutFocus.TipMayStart(tip, false), tip + " may start at rest");
                bool urgent = tip == GuideTip.Bank || tip == GuideTip.HurryUp;
                Assert.AreEqual(urgent, ReadoutFocus.TipMayStart(tip, true), tip + " in a fight");
            }
        }

        [Test]
        public void OneAward_IsOneLine_WithThePerksShareOnTheEndOfIt()
        {
            Assert.AreEqual("+132  MOON LANDING", OrbitalAnte.Ante.PopFeedback.Line(132, "MOON LANDING", 0));
            Assert.AreEqual("+138  MOON LANDING  (6 CHANGE OF PACE)", OrbitalAnte.Ante.PopFeedback.Line(138, "MOON LANDING", 6));
            Assert.AreEqual("+1,250  SPLAT", OrbitalAnte.Ante.PopFeedback.Line(1250, "SPLAT", -3), "a share that is not there is not shown");
            StringAssert.DoesNotContain("\n", OrbitalAnte.Ante.PopFeedback.Line(138, "X", 6));
        }

        [Test]
        public void TheDefault_SelectsItself_OnceTheHoldIsOver_AndTheButtonIsAtRest()
        {
            var hold = new SelectionHold();
            hold.Start(0.8f);
            float t = 0f;
            bool fired = false;
            while (t < 0.79f) { fired |= hold.Step(0.02f, false); t += 0.02f; }
            Assert.IsFalse(fired, "nothing is selected during the hold");
            int times = 0;
            for (int i = 0; i < 50; i++) if (hold.Step(0.02f, false)) times++;
            Assert.AreEqual(1, times, "selected once, and only once");
            Assert.IsFalse(hold.Waiting);
        }

        [Test]
        public void AButtonHeldOrMashedThroughTheHold_PicksNothing_UntilTheHandsAreStill()
        {
            var hold = new SelectionHold();
            hold.Start(0.8f);
            // Held from the crash for two seconds.
            for (int i = 0; i < 100; i++) Assert.IsFalse(hold.Step(0.02f, true));
            // Mashed: down one frame in five, never up for a quarter of a second.
            for (int i = 0; i <= 200; i++) Assert.IsFalse(hold.Step(0.02f, i % 5 == 0));
            // Let go: the cursor appears a quarter of a second later, not sooner.
            float waited = 0f;
            bool fired = false;
            while (!fired && waited < 2f) { fired = hold.Step(0.02f, false); waited += 0.02f; }
            Assert.IsTrue(fired);
            Assert.GreaterOrEqual(waited, SelectionHold.Quiet - 0.021f);
            Assert.LessOrEqual(waited, SelectionHold.Quiet + 0.041f);
        }

        [Test]
        public void ACancelledHold_NeverSelects_AndALongFrameDoesNotSkipTheQuiet()
        {
            var hold = new SelectionHold();
            hold.Start(0.8f);
            hold.Cancel();
            for (int i = 0; i < 100; i++) Assert.IsFalse(hold.Step(0.02f, false));

            hold.Start(0.8f);
            Assert.IsFalse(hold.Step(5f, false), "a slow frame as the page opens does not eat the hold");
            Assert.IsFalse(hold.Step(5f, false), "nor do two");
            int frames = 0;
            while (!hold.Step(0.02f, false) && frames < 200) frames++;
            Assert.GreaterOrEqual(frames, 30, "most of the hold is still to run");
            Assert.IsFalse(new SelectionHold().Step(0.02f, false), "never started, never selects");
        }
    }
}
