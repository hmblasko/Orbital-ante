using NUnit.Framework;
using OrbitalAnte.Ante;
using OrbitalAnte.Enemies;
using OrbitalAnte.Player;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Rules for refinement pass 3 (24 Nov 2026): restless camps that end a long quiet, a gun's push in zero-G,
    /// and hazards that pay only while the ante's clock runs.
    /// </summary>
    public class RefinePassThreeTests
    {
        // ------------------------------------------------------------------ restless camps

        static LullAction Run(LullClock clock, LullRules rules, float seconds, bool open = true, bool fighting = false,
                              bool hiding = false, bool standing = true, int ante = 1)
        {
            LullAction last = LullAction.Wait;
            for (float t = 0f; t < seconds; t += 0.4f)
            {
                last = clock.Tick(rules, ante, 0.4f, open, fighting, hiding, standing);
                if (last != LullAction.Wait) return last;
            }
            return last;
        }

        [Test]
        public void AQuietLongEnough_CallsTheNearestSquad_OnceAndThenWaitsAgain()
        {
            var rules = new LullRules();
            var clock = new LullClock();
            Assert.AreEqual(LullAction.Wait, Run(clock, rules, rules.quietSeconds - 1f));
            Assert.AreEqual(LullAction.Call, Run(clock, rules, 2f));
            // The call starts a whole new quiet.
            Assert.AreEqual(0f, clock.Quiet, 1e-4f);
            Assert.AreEqual(LullAction.Wait, Run(clock, rules, rules.quietSeconds - 1f));
        }

        [Test]
        public void AFight_ResetsTheQuiet()
        {
            var rules = new LullRules();
            var clock = new LullClock();
            Run(clock, rules, rules.quietSeconds - 2f);
            clock.Tick(rules, 1, 0.4f, true, true, false, true);
            Assert.AreEqual(0f, clock.Quiet, 1e-4f);
            Assert.AreEqual(LullAction.Wait, Run(clock, rules, rules.quietSeconds - 1f));
        }

        [Test]
        public void Hiding_StandsTheClockStill_AndNeverCalls()
        {
            var rules = new LullRules();
            var clock = new LullClock();
            Run(clock, rules, 10f);
            float before = clock.Quiet;
            Assert.AreEqual(LullAction.Wait, Run(clock, rules, 600f, hiding: true));
            Assert.AreEqual(before, clock.Quiet, 1e-4f, "crouched, prone or sneaking: the quiet neither grows nor resets");
        }

        [Test]
        public void AboardOrOffTheWorld_TheQuietStartsAgain()
        {
            var rules = new LullRules();
            var clock = new LullClock();
            Run(clock, rules, 20f);
            clock.Tick(rules, 1, 0.4f, false, false, false, true);
            Assert.AreEqual(0f, clock.Quiet, 1e-4f);
        }

        [Test]
        public void NobodyStandingNear_AsksForACampFurtherOff_UntilSomeoneStands()
        {
            var rules = new LullRules();
            var clock = new LullClock();
            Assert.AreEqual(LullAction.WakeFurther, Run(clock, rules, rules.quietSeconds + 1f, standing: false));
            // The woken camp gets its grace: nothing is asked of it for a while, then it is called.
            Assert.AreEqual(LullAction.Wait, clock.Tick(rules, 1, 0.4f, true, false, false, true));
            Assert.AreEqual(LullAction.Call, Run(clock, rules, rules.wokenGrace + 1f));
            // With still nobody standing after the grace, another wake is asked for, never on the very next think.
            Assert.AreEqual(LullAction.WakeFurther, Run(clock, rules, rules.quietSeconds + 1f, standing: false));
            Assert.AreEqual(LullAction.Wait, clock.Tick(rules, 1, 0.4f, true, false, false, false));
        }

        [Test]
        public void TheQuiet_ShortensEachAnte_NeverUnderItsFloor()
        {
            var rules = new LullRules();
            float last = float.MaxValue;
            for (int ante = 1; ante <= 20; ante++)
            {
                float q = LullClock.QuietFor(rules, ante);
                Assert.LessOrEqual(q, last);
                Assert.GreaterOrEqual(q, rules.quietFloor);
                last = q;
            }
            Assert.AreEqual(rules.quietSeconds, LullClock.QuietFor(rules, 1), 1e-4f, "ante 1 is the dial as set");
        }

        [Test]
        public void SwitchedOff_NeverCalls()
        {
            var rules = new LullRules { enabled = false };
            Assert.AreEqual(LullAction.Wait, Run(new LullClock(), rules, 600f));
        }

        [Test]
        public void ALullWake_StaysInsideTheSleepRange()
        {
            // A camp woken for a lull further than the sleep range would be put back to sleep at once.
            var rules = new LullRules();
            var field = new EnemyFieldRules();
            Assert.Less(rules.wakeReach, field.sleepRange);
            Assert.Greater(rules.wakeReach, field.wakeRange);
            // And a squad woken for it must still measure inside the call reach from the player's eye.
            Assert.GreaterOrEqual(rules.callReach, rules.wakeReach + field.roam + 20f);
        }

        // ------------------------------------------------------------------ zero-G push

        [Test]
        public void AHeavierKick_PushesHarder_AndNoShotPushesPastItsCap()
        {
            var rules = new ZeroGKickRules();
            float smg = ZeroGKick.ShotPush(rules, 0.5f, 1f);
            float rifle = ZeroGKick.ShotPush(rules, 0.75f, 1f);
            float sniper = ZeroGKick.ShotPush(rules, 3.5f, 1f);
            Assert.Greater(smg, 0f);
            Assert.Greater(rifle, smg);
            Assert.GreaterOrEqual(sniper, rifle);
            Assert.LessOrEqual(sniper, rules.mostPerShot);
            Assert.LessOrEqual(ZeroGKick.ShotPush(rules, 100f, 5f), rules.mostPerShot);
        }

        [Test]
        public void HoldingTheTrigger_SettlesIntoADrift_NeverAJetpack()
        {
            var rules = new ZeroGKickRules();
            float speed = 0f;
            for (int shot = 0; shot < 1000; shot++) speed += ZeroGKick.Applied(rules, ZeroGKick.ShotPush(rules, 1.5f, 1f), speed);
            Assert.AreEqual(rules.mostDrift, speed, 1e-3f);
            // Already moving forward (against the push), every shot still pushes in full.
            Assert.AreEqual(0.1f, ZeroGKick.Applied(rules, 0.1f, -2f), 1e-5f);
        }

        [Test]
        public void ZeroGPush_Off_OrNoKick_IsNothing()
        {
            Assert.AreEqual(0f, ZeroGKick.ShotPush(new ZeroGKickRules { enabled = false }, 3f, 1f));
            Assert.AreEqual(0f, ZeroGKick.ShotPush(new ZeroGKickRules(), 0f, 1f));
            Assert.AreEqual(0f, ZeroGKick.ShotPush(new ZeroGKickRules(), float.NaN, 1f));
            Assert.AreEqual(0f, ZeroGKick.Applied(new ZeroGKickRules(), 0.2f, 99f));
        }

        // ------------------------------------------------------------------ the stakes and overtime

        [Test]
        public void AHazard_PaysOnTheClock_AndStopsInOvertime()
        {
            foreach (Hazard h in HazardRules.Offered)
            {
                Assert.IsTrue(HazardRules.PaysNow(h, false), h.ToString());
                Assert.IsFalse(HazardRules.PaysNow(h, true), h.ToString());
            }
            Assert.IsFalse(HazardRules.PaysNow(Hazard.None, false));
        }

        [Test]
        public void InOvertime_AHazardsPoints_AreThePlainPoints()
        {
            var balance = new AnteBalance();
            var paid = new AnteRun(balance, 7);
            var plain = new AnteRun(balance, 7);
            ScoreEvent Kill(int i, Hazard h, bool overtime)
            {
                var e = new ScoreEvent { claimId = "k" + i, kind = ScoreEventKind.EnemyTier1 };
                if (HazardRules.PaysNow(h, overtime)) { e.acceptedRisk = true; e.riskMultiplier = HazardRules.Multiplier(h, balance); }
                return e;
            }
            int onClock = paid.Report(Kill(1, Hazard.ShortFuse, false)).points;
            int inOvertime = paid.Report(Kill(2, Hazard.ShortFuse, true)).points;
            int none = plain.Report(Kill(3, Hazard.None, false)).points;
            Assert.Greater(onClock, none);
            Assert.AreEqual(none, inOvertime);
        }
    }
}
