using NUnit.Framework;
using OrbitalAnte.Ante;
using OrbitalAnte.Spacewalk;
using OrbitalAnte.UI;

namespace OrbitalAnte.Tests
{
    /// <summary>Critique loop round 1 (26 Nov 2026): the "you have been seen" warning, the clock by ear, the pulse on the last plates, notes that are not written over, the recap's last line, the jump's grace.</summary>
    public class CritiqueRound1Tests
    {
        [Test]
        public void TheSting_PlaysOnlyWhenThePlayerWasHidden_AndNeverInQuickSuccession()
        {
            Assert.IsTrue(ThreatCues.Sting(true, 100f));
            Assert.IsFalse(ThreatCues.Sting(false, 100f), "while anyone still hunts the player a new sighting is not news");
            Assert.IsFalse(ThreatCues.Sting(true, 1f), "two sightings a second apart give one sting");
        }

        [Test]
        public void TheWarningArc_IsHeldForTheBeat_PulsesButNeverDims_ThenFades()
        {
            float beat = 0.9f;
            for (float age = 0f; age <= beat; age += 0.01f)
                Assert.GreaterOrEqual(ThreatCues.Alpha(age, beat), 0.65f - 1e-4f, "held at " + age);
            Assert.Less(ThreatCues.Alpha(beat + 0.3f, beat), 0.65f);
            Assert.AreEqual(0f, ThreatCues.Alpha(ThreatCues.Seconds(beat), beat));
            Assert.AreEqual(0f, ThreatCues.Alpha(-1f, beat));
        }

        [Test]
        public void TheWarningHold_FollowsTheRealBeat_WithinReadableLimits()
        {
            Assert.AreEqual(ThreatCues.LeastHold, ThreatCues.Hold(0.05f), 1e-5);
            Assert.AreEqual(ThreatCues.MostHold, ThreatCues.Hold(5f), 1e-5);
            Assert.AreEqual(0.8f, ThreatCues.Hold(0.8f), 1e-5);
            Assert.AreEqual(ThreatCues.LeastHold, ThreatCues.Hold(float.NaN), 1e-5);
        }

        static void CountClock(bool seated, out int warns, out int ticks)
        {
            warns = ticks = 0;
            for (float t = 90f; t > 0f; )
            {
                float next = t - 1f / 60f;
                ClockCue c = ClockCues.Step(t, next, true, false, false, false, seated);
                if (c == ClockCue.Tick) ticks++;
                if (c == ClockCue.Warn) warns++;
                t = next;
            }
        }

        [Test]
        public void TheClock_WarnsOnceWithAMinuteLeft_ThenTicksEachOfTheLastFive()
        {
            Assert.AreEqual(ClockCue.Warn, ClockCues.Step(60.02f, 59.99f, true, false, false, false));
            Assert.AreEqual(ClockCue.None, ClockCues.Step(59.99f, 59.97f, true, false, false, false));
            CountClock(false, out int warns, out int ticks);
            Assert.AreEqual(1, warns);
            Assert.AreEqual(5, ticks, "shown 5, 4, 3, 2, 1");
        }

        [Test]
        public void InTheSeat_TheClockStillWarns_ButNeverTicks()
        {
            CountClock(true, out int warns, out int ticks);
            Assert.AreEqual(1, warns);
            Assert.AreEqual(0, ticks);
        }

        [Test]
        public void TheClock_IsQuiet_OnceTheTargetIsMet_OnHold_InOvertime_OrBetweenAntes()
        {
            Assert.AreEqual(ClockCue.None, ClockCues.Step(3.01f, 2.99f, true, true, false, false));
            Assert.AreEqual(ClockCue.None, ClockCues.Step(3.01f, 2.99f, true, false, true, false));
            Assert.AreEqual(ClockCue.None, ClockCues.Step(3.01f, 2.99f, true, false, false, true));
            Assert.AreEqual(ClockCue.None, ClockCues.Step(3.01f, 2.99f, false, false, false, false));
            Assert.AreEqual(ClockCue.None, ClockCues.Step(0f, 300f, true, false, false, false), "a new ante's clock is no cue");
            Assert.AreEqual(ClockCue.Tick, ClockCues.Step(3.01f, 2.99f, true, false, false, false));
        }

        [Test]
        public void ThePulse_StartsOnTheLastPlates_StopsOnceMended_AndNeverFlutters()
        {
            var p = new PulseClock();
            Assert.IsFalse(p.Step(0.1f, 0.5f, true));
            Assert.IsFalse(p.Step(0.1f, 0.25f, true), "between the two marks it does not start");
            Assert.IsTrue(p.Step(0.1f, 0.2f, true), "the first beat comes at once");
            Assert.IsTrue(p.On);
            Assert.IsFalse(p.Step(0.1f, 0.25f, true), "mending a little does not stop it");
            Assert.IsTrue(p.On);
            Assert.IsFalse(p.Step(0.1f, 0.3f, true));
            Assert.IsFalse(p.On, "mended: the sound going away is the answer to taking cover");
            Assert.IsTrue(p.Step(0.1f, 0.1f, true));
            Assert.IsFalse(p.Step(0.1f, 0.1f, false), "never for the dead");
            Assert.IsFalse(p.On);
        }

        [Test]
        public void ThePulse_IsAHeartNotAnAlarm_ItsRateStaysHuman()
        {
            var p = new PulseClock();
            int beats = 0;
            for (int i = 0; i < 600; i++) if (p.Step(0.1f, 0f, true)) beats++;
            Assert.LessOrEqual(beats, 85, "a minute at the worst: no more than a brisk heart");
            Assert.GreaterOrEqual(beats, 70);
            Assert.AreEqual(PulseClock.SlowBeat, PulseClock.Interval(PulseClock.StartAt), 1e-5);
            Assert.AreEqual(1f, PulseClock.Volume(0f), 1e-5);
            Assert.AreEqual(0.6f, PulseClock.Volume(PulseClock.StartAt), 1e-5);
        }

        [Test]
        public void AnImportantNote_IsNotWrittenOver_AndThePlainOneWaitsItsTurn()
        {
            var b = new NoteBoard();
            b.Offer("TARGET MET", true);
            b.Offer("SUPPLY DROP COMING", false);
            b.Offer("MAST FOUND", false);
            Assert.AreEqual("TARGET MET", b.Text);
            Assert.IsFalse(b.Tick(NoteBoard.HoldSeconds - 0.5f));
            Assert.AreEqual("TARGET MET", b.Text);
            Assert.IsTrue(b.Tick(0.6f));
            Assert.AreEqual("MAST FOUND", b.Text, "only the latest plain note waits");
            Assert.IsTrue(b.Tick(NoteBoard.PlainSeconds + 0.1f));
            Assert.IsNull(b.Text);
        }

        [Test]
        public void PlainNotes_ReplaceEachOtherAtOnce_AndAnImportantOneAlwaysShows()
        {
            var b = new NoteBoard();
            b.Offer("ONE", false);
            b.Offer("TWO", false);
            Assert.AreEqual("TWO", b.Text);
            b.Offer("OVERTIME", true);
            Assert.AreEqual("OVERTIME", b.Text);
            b.Offer("TARGET MET", true);
            Assert.AreEqual("TARGET MET", b.Text);
            Assert.IsTrue(b.Tick(NoteBoard.ImportantSeconds + 0.1f));
            Assert.IsNull(b.Text);
            b.Offer(null, true);
            Assert.IsNull(b.Text);
            b.Clear();
            Assert.IsFalse(b.Tick(1f));
        }

        [Test]
        public void TheRecapsLastLine_LooksForward_AndNeverDressesUpANearMiss()
        {
            Assert.AreEqual("Next mark: clear ante 1.", RunRecap.NextLine(0, 9));
            Assert.AreEqual("Next mark: clear ante 4.", RunRecap.NextLine(3, 9));
            Assert.AreEqual("Next mark: a higher run score.", RunRecap.NextLine(9, 9));
            Assert.AreEqual("Next mark: a higher run score.", RunRecap.NextLine(12, 9));
            for (int c = -1; c < 12; c++)
            {
                string line = RunRecap.NextLine(c, 9).ToLowerInvariant();
                Assert.IsNotEmpty(line);
                StringAssert.DoesNotContain("close", line);
                StringAssert.DoesNotContain("almost", line);
                StringAssert.DoesNotContain("only", line);
            }
        }

        [Test]
        public void AJumpPressedJustBeforeLanding_GoesOnLanding()
        {
            var g = new JumpGrace();
            g.Press(1.00f);
            Assert.IsFalse(g.Take(1.02f, false), "still in the air");
            Assert.IsTrue(g.Take(1.08f, true), "lands inside the grace");
            Assert.IsFalse(g.Take(1.10f, true), "one press, one jump");
        }

        [Test]
        public void APressTooEarly_IsForgotten()
        {
            var g = new JumpGrace();
            g.Press(1.00f);
            Assert.IsFalse(g.Take(1.00f + JumpGrace.BufferSeconds + 0.05f, true));
        }

        [Test]
        public void AJumpJustAfterSteppingOffAnEdge_StillGoes_ButOnlyOnce()
        {
            var g = new JumpGrace();
            Assert.IsFalse(g.Take(2.00f, true));
            g.Press(2.06f);
            Assert.IsTrue(g.Take(2.08f, false), "coyote time");
            g.Press(2.10f);
            Assert.IsFalse(g.Take(2.12f, false), "no second jump from the air");
        }

        [Test]
        public void TheJumpsOwnTakeOffStep_IsNotALanding()
        {
            var g = new JumpGrace();
            g.Take(3.00f, true);
            g.Press(3.00f);
            Assert.IsTrue(g.Take(3.02f, true));
            // The feet are still found under the suit on the next step; a second press then must not jump again.
            g.Press(3.05f);
            Assert.IsFalse(g.Take(3.06f, true));
            Assert.IsFalse(g.Take(3.15f, false));
        }

        [Test]
        public void LongAfterLeavingTheGround_APressIsJetsNotAJump()
        {
            var g = new JumpGrace();
            g.Take(4.00f, true);
            g.Press(4.50f);
            Assert.IsFalse(g.Take(4.52f, false));
        }
    }
}
