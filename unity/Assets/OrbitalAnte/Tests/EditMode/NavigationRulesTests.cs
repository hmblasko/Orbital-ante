using System;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Navigation;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Milestone 10 rules with no Unity objects: the autopilot (flown here against a simple moving target with
    /// gravity), the path check, the target lock, and what a knock does to the person inside.
    /// </summary>
    public class NavigationRulesTests
    {
        // ------------------------------------------------------------------ autopilot

        struct Trip
        {
            public bool Arrived;
            public double Seconds, Closest, EndSpeed, EndHeight, FuelSeconds;
            public int Flips;
        }

        /// <summary>Flies the autopilot at a body of the given radius and pull, starting from rest unless told otherwise.</summary>
        static Trip Fly(AutopilotRules rules, Vec3d start, Vec3d startVelocity, double radius, double surfaceGravity, Vec3d targetVelocity, double pushShare = 1.0, double limit = 900.0)
        {
            var trip = new Trip { Closest = double.MaxValue };
            Vec3d ship = start, velocity = startVelocity + targetVelocity, target = Vec3d.Zero;
            const double dt = 0.02;
            PilotPhase last = PilotPhase.Off;
            for (double t = 0; t < limit; t += dt)
            {
                Vec3d to = target - ship;
                double range = to.Magnitude;
                // The pull of the target, falling off with the square of the distance.
                Vec3d gravity = to / range * (surfaceGravity * radius * radius / (range * range));
                PilotOrder order = Autopilot.Fly(rules, to, velocity - targetVelocity, radius, gravity, pushShare);
                trip.Closest = Math.Min(trip.Closest, range - radius);
                if (order.Arrived)
                {
                    trip.Arrived = true;
                    trip.Seconds = t;
                    trip.EndSpeed = (velocity - targetVelocity).Magnitude;
                    trip.EndHeight = range - radius;
                    return trip;
                }
                Assert.LessOrEqual(order.Push.Magnitude, rules.push * pushShare + 1e-6, "it pushed harder than the ship can");
                if (order.Phase != last && (order.Phase == PilotPhase.Burning || order.Phase == PilotPhase.Braking)) trip.Flips++;
                last = order.Phase;
                trip.FuelSeconds += order.Effort * dt;
                velocity += (order.Push + gravity) * dt;
                ship += velocity * dt;
                target += targetVelocity * dt;
            }
            trip.Seconds = limit;
            return trip;
        }

        [TestCase(8000.0, 400.0, 6.0)]
        [TestCase(60000.0, 900.0, 9.0)]
        [TestCase(3000.0, 150.0, 1.6)]
        public void TheAutopilot_ArrivesAboveTheTarget_MovingWithIt(double distance, double radius, double gravity)
        {
            var rules = new AutopilotRules();
            Trip trip = Fly(rules, new Vec3d(0, 0, -distance), Vec3d.Zero, radius, gravity, new Vec3d(40, 0, 15));
            Assert.IsTrue(trip.Arrived, "it never arrived");
            double standOff = Autopilot.StandOff(rules, radius);
            Assert.LessOrEqual(trip.EndSpeed, rules.arriveSpeed + 1e-6);
            Assert.That(trip.EndHeight, Is.InRange(standOff * 0.9, standOff * 1.1), "it should stop at its standing-off height");
            Assert.Greater(trip.Closest, standOff * 0.6, "it dipped far below its stopping place on the way in");
            TestContext.Out.WriteLine(distance + " m: " + trip.Seconds.ToString("0") + " s, engines on for " + trip.FuelSeconds.ToString("0") + " s, " + trip.Flips + " changes of mind");
        }

        [Test]
        public void TheAutopilot_CancelsSidewaysDrift_AndCopesWithAWeakEngine()
        {
            var rules = new AutopilotRules();
            // Crossing the target's bows at 200 m/s to begin with.
            Trip drifting = Fly(rules, new Vec3d(0, 0, -20000), new Vec3d(200, -50, 0), 500, 5, Vec3d.Zero);
            Assert.IsTrue(drifting.Arrived);
            Assert.Greater(drifting.Closest, Autopilot.StandOff(rules, 500) * 0.6);

            // Flying toward it far too fast already.
            Trip hot = Fly(rules, new Vec3d(0, 0, -20000), new Vec3d(0, 0, 600), 500, 5, Vec3d.Zero);
            Assert.IsTrue(hot.Arrived);
            Assert.Greater(hot.Closest, 0, "it flew into the target");

            // Damaged thrusters: slower, but it still gets there and still stops.
            Trip weak = Fly(rules, new Vec3d(0, 0, -20000), Vec3d.Zero, 500, 5, Vec3d.Zero, 0.6);
            Trip strong = Fly(rules, new Vec3d(0, 0, -20000), Vec3d.Zero, 500, 5, Vec3d.Zero);
            Assert.IsTrue(weak.Arrived);
            Assert.Greater(weak.Seconds, strong.Seconds);
            Assert.Greater(weak.Closest, Autopilot.StandOff(rules, 500) * 0.6);
        }

        [Test]
        public void TheAutopilot_DoesNotDither_AndItsTimeGuessIsHonest()
        {
            var rules = new AutopilotRules();
            Vec3d start = new Vec3d(0, 0, -30000);
            Trip trip = Fly(rules, start, Vec3d.Zero, 600, 0, Vec3d.Zero);
            Assert.IsTrue(trip.Arrived);
            Assert.LessOrEqual(trip.Flips, 4, "it should speed up once and slow down once, not flutter between them");
            double left = 30000 - 600 - Autopilot.StandOff(rules, 600);
            double guess = Autopilot.SecondsFor(left, 0, rules.push, rules.push * rules.brakeShare, rules.topSpeed);
            Assert.That(trip.Seconds, Is.InRange(guess * 0.8, guess * 1.5), "the time shown on the dash should be about right");
            // A by-hand pilot who brakes at the last moment can beat it: it plans gentle braking on purpose.
            double flatOut = 2.0 * Math.Sqrt(left / rules.push);
            Assert.Greater(trip.Seconds, flatOut);
        }

        [Test]
        public void TheSafeClosingSpeed_AlwaysLeavesRoomToStop()
        {
            var rules = new AutopilotRules();
            for (double d = 10; d < 1e6; d *= 3)
            {
                double v = Autopilot.SafeClosingSpeed(rules, d, rules.push);
                Assert.LessOrEqual(v, rules.topSpeed);
                // Stopping distance at full push is shorter than the room there is.
                Assert.Less(v * v / (2.0 * rules.push), d);
            }
            Assert.LessOrEqual(Autopilot.SafeClosingSpeed(rules, -50, rules.push), 0, "too close: back away, gently");
            Assert.GreaterOrEqual(Autopilot.SafeClosingSpeed(rules, -5000, rules.push), -20.0001);
            Assert.AreEqual(250.0, Autopilot.StandOff(rules, 100), 1e-9);
            Assert.AreEqual(600.0, Autopilot.StandOff(rules, 1000), 1e-3);
            Assert.AreEqual(1800.0, Autopilot.StandOff(rules, 9000), 1e-9);
        }

        [Test]
        public void ABodyInTheWay_BlocksThePath_ButTheGroundYouAreLeavingDoesNot()
        {
            var rules = new AutopilotRules();
            // 0: the target, far ahead. 1: a planet square in the way. 2: one well off to the side.
            // 3: the ground under the ship (behind the direction of travel). 4: one beyond the target.
            var positions = new[] { new Vec3d(0, 0, 50000), new Vec3d(100, 0, 20000), new Vec3d(9000, 0, 20000), new Vec3d(0, -501, 0), new Vec3d(0, 0, 90000) };
            var radii = new[] { 400.0, 800.0, 800.0, 500.0, 3000.0 };
            Assert.AreEqual(1, Autopilot.Blocker(rules, positions, radii, 5, 0));

            positions[1] = new Vec3d(1100, 0, 20000);
            Assert.AreEqual(-1, Autopilot.Blocker(rules, positions, radii, 5, 0), "a clear miss is not a block, nor the ground below, nor a body past the target");
            positions[1] = new Vec3d(900, 0, 20000);
            Assert.AreEqual(1, Autopilot.Blocker(rules, positions, radii, 5, 0), "a near miss is a block: it keeps a margin");

            // Standing on a body with the target on its far side: the ground is in the way.
            positions[3] = new Vec3d(0, 0, 501);
            Assert.AreEqual(3, Autopilot.Blocker(rules, positions, radii, 5, 0));
            // Of two blockers, it names the first one met.
            positions[1] = new Vec3d(0, 0, 20000);
            Assert.AreEqual(3, Autopilot.Blocker(rules, positions, radii, 5, 0));
            Assert.AreEqual(-1, Autopilot.Blocker(rules, positions, radii, 5, -1));
        }

        [Test]
        public void TheAutopilot_AlwaysSaysWhyItStopped()
        {
            foreach (PilotStop stop in Enum.GetValues(typeof(PilotStop)))
            {
                string words = Autopilot.Describe(stop, "THE SUN");
                if (stop == PilotStop.None) Assert.AreEqual(string.Empty, words);
                else StringAssert.StartsWith("AUTOPILOT", words, stop.ToString());
                Assert.LessOrEqual(words.Length, 40, "too long for the dash's notice line");
            }
            StringAssert.Contains("THE SUN IS IN THE WAY", Autopilot.Describe(PilotStop.Blocked, "THE SUN"));
            Assert.AreEqual("AUTOPILOT: SLOWING  1:05", Autopilot.Words(PilotPhase.Braking, 65.9f));
            Assert.AreEqual("AUTOPILOT: COASTING  0:00", Autopilot.Words(PilotPhase.Coasting, -3f));
        }

        [Test]
        public void LettingGoUnasked_IsAnAlarm_FinishingOrBeingTakenOverIsNot()
        {
            Assert.IsTrue(Autopilot.IsAlarm(PilotStop.Blocked));
            Assert.IsTrue(Autopilot.IsAlarm(PilotStop.NoFuel));
            Assert.IsTrue(Autopilot.IsAlarm(PilotStop.Knocked));
            Assert.IsFalse(Autopilot.IsAlarm(PilotStop.Arrived));
            Assert.IsFalse(Autopilot.IsAlarm(PilotStop.PilotTookOver));
            Assert.IsFalse(Autopilot.IsAlarm(PilotStop.NoTarget), "a refusal on the ground or with no lock is words, not an alarm");
            var rules = new AutopilotRules();
            Assert.Greater(rules.fuelSurcharge, 1f, "the easy way must not also be the thrifty way");
            Assert.Less(rules.push, 30f * 2.5f, "the autopilot must push less than a pilot on boost (TestCraftConfig: thrust 30, boost 2.5)");
        }

        // ------------------------------------------------------------------ what the dash says about landing

        static OrbitalAnte.Cockpit.DashModel Dash(OrbitalAnte.Cockpit.DashInputs input)
        {
            var m = new OrbitalAnte.Cockpit.DashModel();
            for (int i = 0; i < 30; i++) m.Tick(0.02f, input);
            return m;
        }

        [Test]
        public void TheDash_NamesTheSafeLandingSpeed_WhenYouAreAboutToBreakIt()
        {
            var falling = new OrbitalAnte.Cockpit.DashInputs { Fuel = 1f, Oxygen = 1f, Speed = 12f, Altitude = 40f, Vertical = -12f, SafeSink = 4f };
            var m = Dash(falling);
            Assert.IsTrue(m.Readings.SinkTooFast);
            Assert.AreEqual("TOO FAST TO LAND: OVER 4 m/s", m.Readings.Notice);

            falling.Altitude = 400f;
            Assert.IsFalse(Dash(falling).Readings.SinkTooFast, "six seconds from the ground is soon enough; higher up it would only nag");
            falling.Altitude = 10f; falling.Vertical = -3f;
            Assert.IsFalse(Dash(falling).Readings.SinkTooFast, "a safe descent is not warned about");
            falling.Vertical = 9f;
            Assert.IsFalse(Dash(falling).Readings.SinkTooFast, "climbing away is not warned about");
            falling.Vertical = -12f; falling.Altitude = -1f;
            Assert.IsFalse(Dash(falling).Readings.SinkTooFast, "no ground known, no warning");
        }

        [Test]
        public void TheDash_SaysWhenNobodyIsStrappedIn_ButOnlyWhenItMatters()
        {
            var loose = new OrbitalAnte.Cockpit.DashInputs { Fuel = 1f, Oxygen = 1f, Speed = 40f, DriftZ = 40f, Altitude = -1f, Loose = true, FuelLow = true };
            Assert.AreEqual("NOT BUCKLED IN", Dash(loose).Readings.Notice, "this outranks even low fuel: it is what kills you");
            loose.Speed = 2f;
            Assert.AreEqual("FUEL LOW", Dash(loose).Readings.Notice, "standing on the pad unbuckled is not a warning");
            loose.Loose = false; loose.Speed = 40f; loose.FuelLow = false; loose.Note = "AUTOPILOT: COASTING  0:12";
            Assert.AreEqual("AUTOPILOT: COASTING  0:12", Dash(loose).Readings.Notice);
        }

        // ------------------------------------------------------------------ target lock

        [Test]
        public void Lock_HoldsItsTarget_UntilPressedAgainOrPressedOnEmptySky()
        {
            Assert.AreEqual(3, TargetLock.Press(-1, 3), "lock what is under the nose");
            Assert.AreEqual(-1, TargetLock.Press(3, 3), "pressing on the locked body lets go");
            Assert.AreEqual(5, TargetLock.Press(3, 5), "pressing on another body moves the lock");
            Assert.AreEqual(-1, TargetLock.Press(3, -1), "pressing on empty sky lets go");
            Assert.AreEqual(3, TargetLock.Target(3, 5), "a lock wins over whatever the nose is on");
            Assert.AreEqual(5, TargetLock.Target(-1, 5));
            Assert.AreEqual(-1, TargetLock.Target(-1, -1));
        }

        [Test]
        public void UnderTheNose_AnywhereOnADiscCounts_AndTheNearerBodyWins()
        {
            // A big planet filling 20 degrees: 8 degrees off its centre is still dead on.
            double radius = 1000, distance = radius / Math.Sin(10 * Math.PI / 180);
            Assert.Less(TargetLock.Score(8, distance, radius), 0.01);
            Assert.AreEqual(2.0, TargetLock.Score(12, distance, radius), 0.01);
            Assert.Less(TargetLock.Score(0, 5000, 100), TargetLock.Score(0, 50000, 100));
            Assert.AreEqual(double.MaxValue, TargetLock.Score(0, 50, 100), "inside a body, it is not a target");
        }

        // ------------------------------------------------------------------ knocks

        [Test]
        public void TheBeltIsWorthWearing()
        {
            var crash = new CrashModel();
            CrashRules r = crash.Rules;
            Assert.AreEqual(PilotFate.Fine, crash.Fate(r.feltSpeed * 0.5f, false));
            Assert.AreEqual(PilotFate.Jolted, crash.Fate(r.throwSpeed - 1f, false));
            Assert.AreEqual(PilotFate.Thrown, crash.Fate(r.throwSpeed + 1f, false));
            Assert.AreEqual(PilotFate.Jolted, crash.Fate(r.throwSpeed + 1f, true), "strapped in, you are never thrown");
            Assert.AreEqual(PilotFate.Killed, crash.Fate(r.fatalLoose, false));
            Assert.AreEqual(PilotFate.Jolted, crash.Fate(r.fatalLoose, true), "the knock that kills a loose pilot only jolts a strapped one");
            Assert.AreEqual(PilotFate.Killed, crash.Fate(r.fatalBuckled, true));
            Assert.Greater(r.fatalBuckled, r.fatalLoose * 3f);
            // A landing gentle enough not to hurt the ship never hurts the pilot either.
            Assert.Less(new OrbitalAnte.Ship.DamageRules().safeImpactSpeed, r.throwSpeed);
        }

        [Test]
        public void TouchesOfOneBlow_AddUp_ButASkidDoesNot()
        {
            var crash = new CrashModel();
            float part = crash.Rules.fatalLoose * 0.3f;
            // Four footpads within a tenth of a second: one landing, felt as the sum.
            Assert.AreNotEqual(PilotFate.Killed, crash.Hit(part, false, 0, 1));
            crash.Tick(0.03f);
            Assert.AreNotEqual(PilotFate.Killed, crash.Hit(part, false, 0, 1));
            crash.Tick(0.03f);
            Assert.AreNotEqual(PilotFate.Killed, crash.Hit(part, false, 0, 1));
            crash.Tick(0.03f);
            Assert.AreEqual(PilotFate.Killed, crash.Hit(part, false, 0, 1));
            Assert.AreEqual(part * 4f, crash.LastKnock, 1e-3f);

            // The same four knocks a third of a second apart: a rough skid, never fatal.
            var skid = new CrashModel();
            for (int i = 0; i < 12; i++)
            {
                Assert.AreNotEqual(PilotFate.Killed, skid.Hit(part, false, 0, 1));
                skid.Tick(0.33f);
            }
            // And a run of touches closer together than the window cannot chain into one endless blow.
            var rattle = new CrashModel();
            float small = rattle.Rules.fatalLoose * 0.2f;
            PilotFate worst = PilotFate.Fine;
            for (int i = 0; i < 40; i++)
            {
                PilotFate f = rattle.Hit(small, false, 0, 1);
                if (f > worst) worst = f;
                rattle.Tick(0.15f);
            }
            Assert.AreNotEqual(PilotFate.Killed, worst);
        }

        [Test]
        public void TheView_KicksAndSettles_TheSameAtAnyFrameRate()
        {
            float PeakAt(float step, out float end)
            {
                var crash = new CrashModel();
                crash.Hit(20f, true, 0.5f, 1f);
                float peak = 0f;
                for (float t = 0f; t < 2.5f; t += step)
                {
                    crash.Tick(step);
                    peak = Math.Max(peak, Math.Abs(crash.KickPitch));
                    Assert.LessOrEqual(Math.Abs(crash.KickPitch), crash.Rules.kickDegrees * 2f, "the view should never be thrown further than its limit");
                }
                end = Math.Abs(crash.KickPitch) + Math.Abs(crash.KickRoll);
                return peak;
            }
            float fast = PeakAt(1f / 120f, out float endFast);
            float slow = PeakAt(1f / 30f, out float endSlow);
            Assert.Greater(fast, 1f, "a 20 m/s knock should be seen");
            Assert.AreEqual(fast, slow, fast * 0.15f);
            Assert.AreEqual(0f, endFast, 1e-4f, "the view must come to rest exactly");
            Assert.AreEqual(0f, endSlow, 1e-4f);

            // Loose in the cabin, the same knock throws the head further.
            var strapped = new CrashModel();
            var loose = new CrashModel();
            strapped.Hit(8f, true, 0f, 1f);
            loose.Hit(8f, false, 0f, 1f);
            float a = 0f, b = 0f;
            for (int i = 0; i < 60; i++) { strapped.Tick(0.01f); loose.Tick(0.01f); a = Math.Max(a, Math.Abs(strapped.KickPitch)); b = Math.Max(b, Math.Abs(loose.KickPitch)); }
            Assert.Greater(b, a * 1.4f);
            // A knock from the right rolls the view; a soft one does nothing.
            var side = new CrashModel();
            side.Hit(15f, true, 1f, 0f);
            side.Tick(0.05f);
            Assert.AreNotEqual(0f, side.KickRoll);
            Assert.AreEqual(0f, side.KickPitch, 1e-5f);
            var soft = new CrashModel();
            Assert.AreEqual(PilotFate.Fine, soft.Hit(0.5f, false, 0f, 1f));
            soft.Tick(0.05f);
            Assert.AreEqual(0f, soft.KickPitch);
        }

        [Test]
        public void ADeath_AlwaysSaysWhatKilledYou_AndCrashesSoundDeeperTheHarderTheyAre()
        {
            StringAssert.Contains("not buckled in", CrashModel.Cause(31.4f, false));
            StringAssert.Contains("31 m/s", CrashModel.Cause(31.4f, false));
            StringAssert.Contains("belt", CrashModel.Cause(90f, true));
            Assert.AreEqual(0f, CrashModel.Loudness(0.3f));
            Assert.AreEqual(1f, CrashModel.Loudness(40f));
            Assert.Greater(CrashModel.Loudness(8f), CrashModel.Loudness(3f));
            Assert.Less(CrashModel.Pitch(25f), CrashModel.Pitch(3f));
            Assert.That(CrashModel.Pitch(100f), Is.InRange(0.7f, 0.8f));
        }
    }
}
