using NUnit.Framework;
using OrbitalAnte.Spacewalk;

namespace OrbitalAnte.Tests
{
    /// <summary>Critique loop round 5: the suit's warning tone says how bad things are by how often it sounds.</summary>
    public class CritiqueRound5Tests
    {
        static int Tones(SuitAlarmClock clock, SuitAlarmLevel level, float seconds, float dt = 0.02f)
        {
            int tones = 0;
            for (float t = 0f; t < seconds; t += dt) if (clock.Step(dt, level, true)) tones++;
            return tones;
        }

        [Test]
        public void AirRunningOut_IsHeardAtOnce_EvenWhenAirWasAlreadyLow()
        {
            var clock = new SuitAlarmClock();
            Assert.IsTrue(clock.Step(0.02f, SuitAlarmLevel.AirLow, true));
            Assert.AreEqual(0, Tones(clock, SuitAlarmLevel.AirLow, 1f));
            Assert.IsTrue(clock.Step(0.02f, SuitAlarmLevel.AirOut, true), "the step from low air to none is the deadliest and must sound");
        }

        [Test]
        public void NoAir_CountsThroughTheGrace_AndLowAirOnlyNowAndThen()
        {
            var rules = new SuitRules();
            var clock = new SuitAlarmClock();
            Assert.IsTrue(clock.Step(0.02f, SuitAlarmLevel.AirOut, true));
            int inGrace = Tones(clock, SuitAlarmLevel.AirOut, rules.airGraceSeconds);
            Assert.GreaterOrEqual(inGrace, 5);
            Assert.LessOrEqual(inGrace, 10);

            var low = new SuitAlarmClock();
            low.Step(0.02f, SuitAlarmLevel.AirLow, true);
            float lowSeconds = rules.airLow * rules.airMinutes * 60f;
            int lowTones = Tones(low, SuitAlarmLevel.AirLow, lowSeconds);
            Assert.GreaterOrEqual(lowTones, 2);
            Assert.LessOrEqual(lowTones, 6, "low air lasts minutes: a tone every few seconds would be tuned out");
        }

        [Test]
        public void ACaution_IsSaidOnce_AndNeverNags()
        {
            var clock = new SuitAlarmClock();
            Assert.IsTrue(clock.Step(0.02f, SuitAlarmLevel.Caution, true));
            Assert.AreEqual(0, Tones(clock, SuitAlarmLevel.Caution, 600f, 0.1f));
        }

        [Test]
        public void GettingBetter_IsSilent_AndALongFrameOwesOneTone()
        {
            var clock = new SuitAlarmClock();
            clock.Step(0.02f, SuitAlarmLevel.AirOut, true);
            Assert.IsFalse(clock.Step(0.02f, SuitAlarmLevel.AirLow, true));
            Assert.IsFalse(clock.Step(0.02f, SuitAlarmLevel.None, true));
            clock.Step(0.02f, SuitAlarmLevel.AirOut, true);
            Assert.IsTrue(clock.Step(60f, SuitAlarmLevel.AirOut, true));
            Assert.IsFalse(clock.Step(0.02f, SuitAlarmLevel.AirOut, true));
            Assert.IsFalse(clock.Step(0.02f, SuitAlarmLevel.AirOut, false), "a dead player hears no alarm");
        }

        [Test]
        public void TheLevel_IsTheWorstThingTrue_AndLouderWhenWorse()
        {
            Assert.AreEqual(SuitAlarmLevel.AirOut, SuitAlarmClock.LevelOf(new SuitInputs { AirOut = true, AirLow = true, FuelOut = true }));
            Assert.AreEqual(SuitAlarmLevel.AirLow, SuitAlarmClock.LevelOf(new SuitInputs { AirLow = true, BurningAir = true }));
            Assert.AreEqual(SuitAlarmLevel.Caution, SuitAlarmClock.LevelOf(new SuitInputs { FuelOut = true }));
            Assert.AreEqual(SuitAlarmLevel.None, SuitAlarmClock.LevelOf(new SuitInputs { FuelLow = true }));
            Assert.Greater(SuitAlarmClock.Volume(SuitAlarmLevel.AirOut), SuitAlarmClock.Volume(SuitAlarmLevel.AirLow));
            Assert.Greater(SuitAlarmClock.Volume(SuitAlarmLevel.AirLow), SuitAlarmClock.Volume(SuitAlarmLevel.Caution));
        }
    }
}
