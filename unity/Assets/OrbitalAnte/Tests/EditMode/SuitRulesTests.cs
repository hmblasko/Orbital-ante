using System;
using NUnit.Framework;
using OrbitalAnte.Ship;
using OrbitalAnte.Spacewalk;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Milestone 11 rules with no Unity objects: the suit's air and jet fuel, the jets and which nozzles light,
    /// velocity matching, the tether, standing or floating, knocks, and the suit's words.
    /// </summary>
    public class SuitRulesTests
    {
        // ------------------------------------------------------------------ air and fuel

        [Test]
        public void Drifting_CostsNoFuel_AndPushingDrainsTheTankInTheStatedTime()
        {
            var tanks = new SuitSupplies();
            for (int i = 0; i < 500; i++) tanks.Tick(0.1f, true, false, 0f, 1f);
            Assert.AreEqual(1f, tanks.Fuel, 1e-6f);

            float seconds = 0f;
            while (!tanks.FuelOut && seconds < 1000f) { tanks.Tick(0.1f, true, false, 1f, 1f); seconds += 0.1f; }
            Assert.AreEqual(tanks.Rules.fuelSeconds, seconds, 0.5f);
        }

        [Test]
        public void Outside_TheAirLastsTheStatedMinutes()
        {
            var tanks = new SuitSupplies();
            float seconds = 0f;
            while (!tanks.AirOut && seconds < 5000f) { tanks.Tick(0.5f, true, false, 0f, 1f); seconds += 0.5f; }
            Assert.AreEqual(tanks.Rules.airMinutes * 60f, seconds, 2f);
        }

        [Test]
        public void WithTheJetTankDry_TheJetsStillPush_ButBurnAir()
        {
            var tanks = new SuitSupplies();
            tanks.Set(1f, 0f);
            Assert.AreEqual(1f, tanks.PushFactor, "an empty jet tank must not leave you unable to move");
            float before = tanks.Air;
            tanks.Tick(1f, true, false, 1f, 1f);
            Assert.IsTrue(tanks.BurningAir);
            float breathing = 1f / (tanks.Rules.airMinutes * 60f);
            Assert.Greater(before - tanks.Air, breathing * 3f, "pushing on air should cost far more than breathing");

            tanks.Tick(1f, true, false, 0f, 1f);
            Assert.IsFalse(tanks.BurningAir, "not pushing, nothing is burned");
        }

        [Test]
        public void WithBothTanksEmpty_TheJetsStop()
        {
            var tanks = new SuitSupplies();
            tanks.Set(0f, 0f);
            Assert.AreEqual(0f, tanks.PushFactor);
            Assert.AreEqual(0f, Jetpack.Push(tanks.Rules, new P3(1f, 0f, 0f), tanks.PushFactor).Length);
        }

        [Test]
        public void NoAir_GivesAGraceCountdown_ThenKills_AndAirPutsItRight()
        {
            var tanks = new SuitSupplies();
            tanks.Set(0f, 1f);
            tanks.Tick(1f, true, false, 0f, 1f);
            Assert.IsFalse(tanks.Suffocated);
            Assert.AreEqual(tanks.Rules.airGraceSeconds - 1f, tanks.SecondsLeft, 1e-3f);

            // Getting aboard a ship with air in time saves you.
            tanks.Tick(1f, false, true, 0f, 1f);
            Assert.IsFalse(tanks.AirOut);
            Assert.AreEqual(-1f, tanks.SecondsLeft);
            Assert.AreEqual(0f, tanks.Starved);

            tanks.Set(0f, 1f);
            for (int i = 0; i < 200 && !tanks.Suffocated; i++) tanks.Tick(0.1f, true, false, 0f, 1f);
            Assert.IsTrue(tanks.Suffocated);
        }

        [Test]
        public void Aboard_TheSuitFillsInSeconds_AndTheAirComesOutOfTheShip()
        {
            var tanks = new SuitSupplies();
            tanks.Set(0f, 0f);
            float drawn = 0f, seconds = 0f;
            while ((tanks.Air < 1f || tanks.Fuel < 1f) && seconds < 60f) { drawn += tanks.Tick(0.1f, false, true, 0f, 1f); seconds += 0.1f; }
            Assert.AreEqual(tanks.Rules.refillSeconds, seconds, 0.3f);
            Assert.AreEqual(tanks.Rules.suitShareOfShipAir, drawn, 1e-3f, "one full suit tank should cost the ship its stated share");
        }

        [Test]
        public void AboardAShipWithNoAir_TheSuitDoesNotFill_AndYouBreatheFromIt()
        {
            var tanks = new SuitSupplies();
            tanks.Set(0.5f, 0.5f);
            float drawn = tanks.Tick(1f, true, true, 0f, 0f);
            Assert.AreEqual(0f, drawn);
            Assert.Less(tanks.Air, 0.5f);
            Assert.Greater(tanks.Fuel, 0.5f, "jet fuel still fills: it does not come from the air tank");
        }

        // ------------------------------------------------------------------ jets

        [Test]
        public void PushingTwoWaysAtOnce_IsNoStrongerThanOne()
        {
            var rules = new SuitRules();
            Assert.AreEqual(rules.jetPush, Jetpack.Push(rules, new P3(1f, 0f, 0f), 1f).Length, 1e-4f);
            Assert.AreEqual(rules.jetPush, Jetpack.Push(rules, new P3(1f, 1f, 1f), 1f).Length, 1e-4f);
            Assert.AreEqual(0f, Jetpack.Push(rules, new P3(0.01f, 0f, 0f), 1f).Length, "a stick at rest must not leak a push");
        }

        [Test]
        public void TheJets_LiftYouOnTheMoonAndMars_ButNotOnEarth()
        {
            var rules = new SuitRules();
            Assert.Greater(rules.jetPush, 3.72f, "Mars");
            Assert.Less(rules.jetPush, 9.81f, "Earth: the jets only soften a fall");
        }

        [Test]
        public void MatchVelocity_BringsYouToRest_WithoutOvershooting()
        {
            var rules = new SuitRules();
            var velocity = new P3(12f, -5f, 3f);
            const float dt = 0.02f;
            float last = velocity.Length;
            int steps = 0;
            while (velocity.Length > 0.02f && steps < 2000)
            {
                P3 push = Jetpack.Match(rules, velocity, dt, 1f);
                Assert.LessOrEqual(push.Length, rules.jetPush + 1e-3f);
                velocity = velocity + push * dt;
                Assert.LessOrEqual(velocity.Length, last + 1e-4f, "matching must only ever slow you");
                last = velocity.Length;
                steps++;
            }
            Assert.Less(velocity.Length, 0.03f);
            // 13.3 m/s at 6.5 m/s per second is about two seconds.
            Assert.AreEqual(13.34f / rules.jetPush, steps * dt, 0.1f);
        }

        [Test]
        public void MatchVelocity_PicksTheShipBesideYou_OrTheOneYouLookAt_OtherwiseTheGround()
        {
            var rules = new SuitRules();
            Assert.AreEqual(MatchTarget.Ship, Jetpack.Choose(rules, 20f, 170f), "beside the ship it is the ship, whatever you look at");
            Assert.AreEqual(MatchTarget.Ship, Jetpack.Choose(rules, 800f, 5f), "further off, looking at it");
            Assert.AreEqual(MatchTarget.Ground, Jetpack.Choose(rules, 800f, 60f));
            Assert.AreEqual(MatchTarget.Ground, Jetpack.Choose(rules, rules.matchShipFar + 1f, 0f));
        }

        [Test]
        public void EachPush_LightsTheNozzleOnTheOppositeSide_AndOnlyThatOne()
        {
            var jets = new JetGlow();
            P3[] pushes = { new P3(1, 0, 0), new P3(-1, 0, 0), new P3(0, 1, 0), new P3(0, -1, 0), new P3(0, 0, 1), new P3(0, 0, -1) };
            int[] lit = { JetGlow.Left, JetGlow.Right, JetGlow.Down, JetGlow.Up, JetGlow.Back, JetGlow.Front };
            for (int p = 0; p < pushes.Length; p++)
            {
                jets.Reset();
                for (int i = 0; i < 20; i++) jets.Tick(0.02f, pushes[p]);
                for (int n = 0; n < JetGlow.Count; n++)
                {
                    if (n == lit[p]) Assert.AreEqual(1f, jets.Level(n), 1e-3f, "push " + p + " nozzle " + n);
                    else Assert.AreEqual(0f, jets.Level(n), 1e-3f, "push " + p + " nozzle " + n);
                }
                Assert.AreEqual(1f, jets.Loudness, 1e-3f);
            }
        }

        [Test]
        public void AJet_PuffsOnceWhenItLights_AndDiesAwayQuicklyButNotInstantly()
        {
            var jets = new JetGlow();
            int puffs = 0;
            for (int i = 0; i < 25; i++) { jets.Tick(0.02f, new P3(0, 1, 0)); if (jets.Lit) puffs++; }
            Assert.AreEqual(1, puffs);
            jets.Tick(0.02f, P3.Zero);
            Assert.Greater(jets.Level(JetGlow.Down), 0.5f, "one step after letting go it should still be glowing");
            for (int i = 0; i < 15; i++) jets.Tick(0.02f, P3.Zero);
            Assert.AreEqual(0f, jets.Level(JetGlow.Down));
            Assert.AreEqual(0f, jets.Loudness);
        }

        // ------------------------------------------------------------------ tether

        struct Drift { public float Furthest, Nearest, EndDistance, EndOutward; public int TautSteps; }

        static Drift DriftOnTether(SuitRules rules, P3 start, P3 velocity, float seconds)
        {
            var d = new Drift { Nearest = float.MaxValue };
            const float dt = 0.02f;
            P3 at = start;
            for (float t = 0f; t < seconds; t += dt)
            {
                P3 change = Tether.Pull(rules, at, velocity, out bool taut, out P3 back);
                if (taut) { velocity = velocity + change; at = at + back; d.TautSteps++; }
                at = at + velocity * dt;
                d.Furthest = Math.Max(d.Furthest, at.Length);
                d.Nearest = Math.Min(d.Nearest, at.Length);
            }
            d.EndDistance = at.Length;
            d.EndOutward = P3.Dot(velocity, at.Unit());
            return d;
        }

        [Test]
        public void ASlackTether_DoesNothingAtAll()
        {
            var rules = new SuitRules();
            P3 change = Tether.Pull(rules, new P3(rules.tetherLength - 1f, 0f, 0f), new P3(9f, 2f, 0f), out bool taut, out _);
            Assert.IsFalse(taut);
            Assert.AreEqual(0f, change.Length);
        }

        [Test]
        public void DriftingAway_TheTetherStopsYouAtItsLength_AndNeverReelsYouIn()
        {
            var rules = new SuitRules();
            foreach (float speed in new[] { 0.5f, 3f, 10f, 20f })
            {
                Drift d = DriftOnTether(rules, new P3(rules.tetherLength - 5f, 0f, 0f), new P3(speed, 0f, 0f), 60f);
                Assert.Greater(d.TautSteps, 0);
                Assert.Less(d.Furthest, rules.tetherLength + 0.5f, "at " + speed + " m/s it let you go too far");
                Assert.AreEqual(rules.tetherLength, d.EndDistance, 0.2f, "at " + speed + " m/s it should leave you at the end of the line");
                Assert.AreEqual(0f, d.EndOutward, 1e-3f, "it must leave you still, not drifting back in");
            }
        }

        [Test]
        public void AtTheEndOfTheLine_SidewaysMotionIsKept()
        {
            var rules = new SuitRules();
            P3 change = Tether.Pull(rules, new P3(rules.tetherLength + 0.01f, 0f, 0f), new P3(4f, 3f, 0f), out bool taut, out _);
            Assert.IsTrue(taut);
            Assert.AreEqual(0f, change.Y, 1e-5f);
            Assert.AreEqual(0f, change.Z, 1e-5f);
            Assert.AreEqual(-4f, change.X, 1e-4f, "only the speed away from the ship is taken");
        }

        [Test]
        public void ComingBackIn_TheTetherLeavesYouAlone()
        {
            var rules = new SuitRules();
            Tether.Pull(rules, new P3(rules.tetherLength + 0.3f, 0f, 0f), new P3(-3f, 0f, 0f), out bool taut, out P3 back);
            Assert.IsFalse(taut, "a line cannot push, and does not pull on someone already coming home");
            Assert.AreEqual(0f, back.Length);
        }

        [Test]
        public void TheDrawnLine_HangsWhenSlack_AndIsStraightWhenTaut()
        {
            var rules = new SuitRules();
            Assert.Greater(Tether.Slack(rules, 10f), 0.5f);
            Assert.AreEqual(0f, Tether.Slack(rules, rules.tetherLength));
            Assert.AreEqual(0f, Tether.Slack(rules, rules.tetherLength + 1f));
            Assert.LessOrEqual(Tether.Slack(rules, 1f), 3f);
            Assert.IsTrue(Tether.InReach(rules, rules.tetherLength - 1f));
            Assert.IsFalse(Tether.InReach(rules, rules.tetherLength + 1f));
        }

        // ------------------------------------------------------------------ standing or floating

        [Test]
        public void OnAPlanetsGround_YouStand_AndHighAboveIt_YouFloat()
        {
            var rules = new SuitRules();
            const float radius = 2000f;
            Assert.AreEqual(Posture.Upright, SuitPosture.Next(rules, Posture.Upright, 9.8f, 2f, radius, true));
            Assert.AreEqual(Posture.Upright, SuitPosture.Next(rules, Posture.Upright, 9.8f, 100f, radius, false), "a jump or a short flight is still head-up");
            Assert.AreEqual(Posture.Free, SuitPosture.Next(rules, Posture.Upright, 6f, radius * rules.freeHeightShare + 10f, radius, false), "high up, in orbit, you float");
            Assert.AreEqual(Posture.Free, SuitPosture.Next(rules, Posture.Upright, 0.01f, 5f, radius, false), "where almost nothing pulls, you float");
        }

        [Test]
        public void TheChangeBetweenStandingAndFloating_DoesNotFlickerOnTheLine()
        {
            var rules = new SuitRules();
            const float radius = 2000f;
            float line = radius * rules.freeHeightShare;
            Posture p = Posture.Upright;
            int changes = 0;
            // Bobbing a few metres either side of the line.
            for (int i = 0; i < 200; i++)
            {
                float height = line + (i % 2 == 0 ? 4f : -4f);
                Posture next = SuitPosture.Next(rules, p, 6f, height, radius, false);
                if (next != p) changes++;
                p = next;
            }
            Assert.LessOrEqual(changes, 1);
            Assert.AreEqual(Posture.Upright, SuitPosture.Next(rules, Posture.Free, 6f, line * 0.5f, radius, false), "well inside, you are stood up again");
        }

        [Test]
        public void TouchingGroundThatPulls_AlwaysStandsYouUp_HoweverHighThatGroundIs()
        {
            var rules = new SuitRules();
            // A mountain top far above the body's nominal radius: floating by height alone, but your feet are on rock.
            Assert.AreEqual(Posture.Upright, SuitPosture.Next(rules, Posture.Free, 1.6f, 400f, 150f, true));
            Assert.AreEqual(Posture.Free, SuitPosture.Next(rules, Posture.Free, 1.6f, 400f, 150f, false));
            Assert.AreEqual(Posture.Free, SuitPosture.Next(rules, Posture.Free, 0.01f, 1f, 30f, true), "where nothing pulls, touching a rock does not make a floor of it");
        }

        // ------------------------------------------------------------------ knocks and words

        [Test]
        public void AKnock_IsNothing_ThenWinds_ThenKills_AndSaysSo()
        {
            var rules = new SuitRules();
            Assert.AreEqual(SuitKnock.Result.Nothing, SuitKnock.For(rules, rules.hurtSpeed - 1f));
            Assert.AreEqual(SuitKnock.Result.Winded, SuitKnock.For(rules, rules.hurtSpeed + 1f));
            Assert.AreEqual(SuitKnock.Result.Killed, SuitKnock.For(rules, rules.fatalSpeed + 1f));
            StringAssert.Contains("27 m/s", SuitKnock.Cause(27.2f));
            // An ordinary jump on Earth (a 0.7 m hop lands at under 4 m/s) must never hurt.
            Assert.Greater(rules.hurtSpeed, 5f);
        }

        [Test]
        public void TheSuitSaysOneThing_TheMostUrgentFirst()
        {
            var s = new SuitInputs { GroundName = "MOON", AgainstGround = true, MatchTarget = MatchTarget.Ground };
            Assert.AreEqual(string.Empty, SuitReadout.Notice(s));
            s.Matching = true;
            Assert.AreEqual("MATCHING MOON GROUND", SuitReadout.Notice(s));
            Assert.IsFalse(SuitReadout.IsAlarm(s));
            s.TetherTaut = true;
            StringAssert.Contains("TETHER", SuitReadout.Notice(s));
            s.FuelLow = true;
            Assert.AreEqual("JET FUEL LOW", SuitReadout.Notice(s));
            s.FuelOut = true;
            StringAssert.Contains("BURN YOUR AIR", SuitReadout.Notice(s), "the cost must be said before the first push on air");
            Assert.IsTrue(SuitReadout.IsAlarm(s));
            s.AirLow = true;
            StringAssert.Contains("AIR LOW", SuitReadout.Notice(s));
            s.AirOut = true; s.SecondsLeft = 8.2f;
            Assert.AreEqual("NO AIR: GET ABOARD  9", SuitReadout.Notice(s));
        }

        [Test]
        public void TheSpeedReading_SaysWhatItIsMeasuredAgainst()
        {
            var s = new SuitInputs { GroundName = "MARS", AgainstGround = true, MatchTarget = MatchTarget.Ground };
            Assert.AreEqual("vs MARS GROUND", SuitReadout.SpeedAgainst(s));
            s.AgainstGround = false;
            Assert.AreEqual("vs MARS", SuitReadout.SpeedAgainst(s));
            s.MatchTarget = MatchTarget.Ship;
            Assert.AreEqual("vs SHIP", SuitReadout.SpeedAgainst(s));
        }
    }
}
