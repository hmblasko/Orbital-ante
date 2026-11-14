using NUnit.Framework;
using OrbitalAnte.Combat;
using OrbitalAnte.Core;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Deep Breath's rule with no Unity objects (the meter, when it may begin, what fighting gives back, the
    /// easing) and the rule for putting every change of game speed together (<see cref="TimeBlend"/>).
    /// </summary>
    public class DeepBreathRulesTests
    {
        const float Dt = 1f / 60f;

        static float HoldUntilOut(DeepBreath breath)
        {
            float held = 0f;
            for (int i = 0; i < 100000 && (breath.Active || i == 0); i++)
            {
                breath.Tick(Dt, true);
                if (breath.Active) held += Dt;
            }
            return held;
        }

        [Test]
        public void ARunStartsFull_AndTheWorldAtNormalSpeed()
        {
            var breath = new DeepBreath();
            Assert.AreEqual(1f, breath.Meter);
            Assert.AreEqual(0f, breath.Amount);
            Assert.IsFalse(breath.Active);
            Assert.AreEqual(1f, breath.TimeScale);
        }

        [Test]
        public void HeldFromFull_ItLastsTheStatedRealSeconds_ThenStops()
        {
            var breath = new DeepBreath();
            float held = HoldUntilOut(breath);
            Assert.AreEqual(breath.Rules.seconds, held, 0.05f);
            Assert.AreEqual(0f, breath.Meter);
            Assert.IsFalse(breath.Active);
            Assert.IsTrue(breath.Refused, "still holding with nothing left");
        }

        [Test]
        public void NotHeld_TheMeterNeverMoves_ThereIsNoRefillOverTime()
        {
            var breath = new DeepBreath();
            for (int i = 0; i < 120; i++) breath.Tick(Dt, true);
            for (int i = 0; i < 30; i++) breath.Tick(Dt, false);
            float left = breath.Meter;
            Assert.Less(left, 1f);
            for (int i = 0; i < 60 * 600; i++) breath.Tick(Dt, false);
            Assert.AreEqual(left, breath.Meter);
        }

        [Test]
        public void LettingGo_Stops_AndPressingAgainCarriesOn()
        {
            var breath = new DeepBreath();
            breath.Tick(Dt, true);
            Assert.IsTrue(breath.Active);
            breath.Tick(Dt, false);
            Assert.IsFalse(breath.Active);
            Assert.IsFalse(breath.Refused);
            breath.Tick(Dt, true);
            Assert.IsTrue(breath.Active);
        }

        [Test]
        public void RunOut_ItCannotBeginAgainUntilTheLeastShareIsBack()
        {
            var breath = new DeepBreath();
            HoldUntilOut(breath);

            // One limb is less than the least share: still not enough.
            breath.Earn(breath.Rules.perLimb);
            Assert.Less(breath.Meter, breath.Rules.leastToStart);
            breath.Tick(Dt, true);
            Assert.IsFalse(breath.Active);
            Assert.IsTrue(breath.Refused);
            Assert.AreEqual(breath.Rules.perLimb, breath.Meter, 1e-6f, "a refused breath costs nothing");

            // A second limb takes it over the line.
            breath.Earn(breath.Rules.perLimb);
            Assert.IsTrue(breath.CanStart);
            breath.Tick(Dt, true);
            Assert.IsTrue(breath.Active);
        }

        [Test]
        public void OnceBegun_ItRunsBelowTheLeastShare_RightDownToEmpty()
        {
            var breath = new DeepBreath();
            bool sawBelow = false;
            for (int i = 0; i < 100000; i++)
            {
                breath.Tick(Dt, true);
                if (!breath.Active) break;
                if (breath.Meter < breath.Rules.leastToStart) sawBelow = true;
            }
            Assert.IsTrue(sawBelow);
        }

        [Test]
        public void Earning_NeverGoesOverFull_AndIgnoresNonsense()
        {
            var breath = new DeepBreath();
            breath.Earn(0.5f);
            Assert.AreEqual(1f, breath.Meter);
            for (int i = 0; i < 180; i++) breath.Tick(Dt, true);
            float before = breath.Meter;
            breath.Earn(-1f);
            breath.Earn(float.NaN);
            Assert.AreEqual(before, breath.Meter);
            breath.Earn(breath.Rules.perKill);
            Assert.AreEqual(before + breath.Rules.perKill, breath.Meter, 1e-6f);
            breath.Earn(5f);
            Assert.AreEqual(1f, breath.Meter);
        }

        [Test]
        public void TheSlowing_EasesInAndOut_InTheStatedTimes()
        {
            var breath = new DeepBreath();
            const float step = 0.01f;

            breath.Tick(step, true);
            Assert.Greater(breath.Amount, 0f);
            Assert.Less(breath.Amount, 1f, "not all at once");
            Assert.Less(breath.TimeScale, 1f);
            Assert.Greater(breath.TimeScale, breath.Rules.slowTo);

            float t = step;
            while (breath.Amount < 1f && t < 5f) { breath.Tick(step, true); t += step; }
            Assert.AreEqual(breath.Rules.easeIn, t, step * 1.5f);
            Assert.AreEqual(breath.Rules.slowTo, breath.TimeScale, 1e-6f);

            float last = breath.TimeScale;
            t = 0f;
            while (breath.Amount > 0f && t < 5f)
            {
                breath.Tick(step, false);
                t += step;
                Assert.GreaterOrEqual(breath.TimeScale, last, "only ever speeds back up");
                last = breath.TimeScale;
            }
            Assert.AreEqual(breath.Rules.easeOut, t, step * 1.5f);
        }

        [Test]
        public void AfterLettingGo_TheTimeScaleEndsAtExactlyOne()
        {
            var breath = new DeepBreath();
            for (int i = 0; i < 90; i++) breath.Tick(Dt, true);
            for (int i = 0; i < 90; i++) breath.Tick(Dt, false);
            Assert.AreEqual(0f, breath.Amount);
            Assert.AreEqual(1f, breath.TimeScale, "exactly 1, not nearly 1");

            // And the same after running dry.
            HoldUntilOut(breath);
            for (int i = 0; i < 90; i++) breath.Tick(Dt, true);
            Assert.AreEqual(1f, breath.TimeScale);
        }

        [Test]
        public void Reset_GivesAFullMeterAndNormalSpeed()
        {
            var breath = new DeepBreath();
            for (int i = 0; i < 200; i++) breath.Tick(Dt, true);
            breath.Reset();
            Assert.AreEqual(1f, breath.Meter);
            Assert.AreEqual(0f, breath.Amount);
            Assert.IsFalse(breath.Active);
            Assert.AreEqual(1f, breath.TimeScale);
        }

        [Test]
        public void ABadFrameTime_DoesNoHarm()
        {
            var breath = new DeepBreath();
            breath.Tick(-1f, true);
            breath.Tick(float.NaN, true);
            Assert.AreEqual(1f, breath.Meter);
            Assert.IsFalse(float.IsNaN(breath.Amount));
        }

        // ------------------------------------------------------------------ putting game speed together

        [Test]
        public void GameSpeed_PauseWins_ThenHitstop_ThenTheSlowsMultiply()
        {
            Assert.AreEqual(1f, TimeBlend.Scale(false, 1f, 1f, 1f));
            Assert.AreEqual(0f, TimeBlend.Scale(true, 0.02f, 0.3f, 0.35f), "paused is always stopped");
            Assert.AreEqual(0.02f, TimeBlend.Scale(false, 0.02f, 0.3f, 0.35f), "hitstop replaces the slows");
            Assert.AreEqual(0.35f, TimeBlend.Scale(false, 1f, 1f, 0.35f));
            Assert.AreEqual(0.3f * 0.35f, TimeBlend.Scale(false, 1f, 0.3f, 0.35f), 1e-6f);
            Assert.AreEqual(1f, TimeBlend.Scale(false, 2f, 5f, float.NaN), "nothing can speed time up");
        }

        [Test]
        public void ThePhysicsStep_ShrinksWithTheSlows_AndComesBackExactly()
        {
            const float normal = 0.02f;
            Assert.AreEqual(normal, TimeBlend.FixedStep(normal, 1f, 1f), "exactly, not nearly");
            Assert.AreEqual(normal * 0.35f, TimeBlend.FixedStep(normal, 1f, 0.35f), 1e-7f);
            Assert.AreEqual(normal * TimeBlend.LeastStepShare, TimeBlend.FixedStep(normal, 0.1f, 0.1f), 1e-7f, "never smaller than the floor");
        }
    }
}
