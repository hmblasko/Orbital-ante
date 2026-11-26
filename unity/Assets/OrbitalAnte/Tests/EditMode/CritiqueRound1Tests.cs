using NUnit.Framework;
using OrbitalAnte.Ante;
using OrbitalAnte.Spacewalk;
using OrbitalAnte.UI;

namespace OrbitalAnte.Tests
{
    /// <summary>Critique loop round 1 (26 Nov 2026): the "you have been seen" warning, the clock by ear, the jump's grace.</summary>
    public class CritiqueRound1Tests
    {
        [Test]
        public void TheSting_PlaysOnlyWhenAQuietStretchEnds_AndNeverInQuickSuccession()
        {
            Assert.IsTrue(ThreatCues.Sting(ThreatCues.QuietBeforeSting, 100f));
            Assert.IsFalse(ThreatCues.Sting(1f, 100f), "in a running fight a new sighting is not news");
            Assert.IsFalse(ThreatCues.Sting(60f, 1f), "two hidden stretches a second apart give one sting");
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
        public void TheWarningHold_StaysReadable_ForAnySharpness()
        {
            Assert.AreEqual(ThreatCues.LeastHold, ThreatCues.Hold(0.05f), 1e-5);
            Assert.AreEqual(ThreatCues.MostHold, ThreatCues.Hold(5f), 1e-5);
            Assert.AreEqual(0.8f, ThreatCues.Hold(0.8f), 1e-5);
            Assert.AreEqual(ThreatCues.LeastHold, ThreatCues.Hold(float.NaN), 1e-5);
        }

        [Test]
        public void TheClock_WarnsOnceAtHalfAMinute_ThenTicksEachOfTheLastTen()
        {
            Assert.AreEqual(ClockCue.Warn, ClockCues.Step(30.02f, 29.99f, true, false, false, false));
            Assert.AreEqual(ClockCue.None, ClockCues.Step(29.99f, 29.97f, true, false, false, false));
            int ticks = 0, warns = 0;
            for (float t = 40f; t > 0f; )
            {
                float next = t - 1f / 60f;
                ClockCue c = ClockCues.Step(t, next, true, false, false, false);
                if (c == ClockCue.Tick) ticks++;
                if (c == ClockCue.Warn) warns++;
                t = next;
            }
            Assert.AreEqual(1, warns);
            Assert.AreEqual(10, ticks, "shown 10, 9, ... 1");
        }

        [Test]
        public void TheClock_IsQuiet_OnceTheTargetIsMet_OnHold_InOvertime_OrBetweenAntes()
        {
            Assert.AreEqual(ClockCue.None, ClockCues.Step(5.01f, 4.99f, true, true, false, false));
            Assert.AreEqual(ClockCue.None, ClockCues.Step(5.01f, 4.99f, true, false, true, false));
            Assert.AreEqual(ClockCue.None, ClockCues.Step(5.01f, 4.99f, true, false, false, true));
            Assert.AreEqual(ClockCue.None, ClockCues.Step(5.01f, 4.99f, false, false, false, false));
            Assert.AreEqual(ClockCue.None, ClockCues.Step(0f, 300f, true, false, false, false), "a new ante's clock is no cue");
        }

        [Test]
        public void TheTick_GetsLouder_NeverOver1()
        {
            float last = 0f;
            for (int s = 10; s >= 1; s--)
            {
                float v = ClockCues.TickVolume(s - 0.5f);
                Assert.GreaterOrEqual(v, last);
                Assert.LessOrEqual(v, 1f);
                last = v;
            }
            Assert.AreEqual(1f, ClockCues.TickVolume(0.5f), 1e-5);
            Assert.AreEqual(0.55f, ClockCues.TickVolume(9.5f), 1e-5);
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
