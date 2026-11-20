using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;
using OrbitalAnte.Look;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>The Screamer (the cicada): what comes off and what kills it, the flight to a perch, the warning, the scream and the ways out of it.</summary>
    public class ScreamerRulesTests
    {
        const double Dt = 1.0 / 60.0;
        const double Rifle = 24.0;

        /// <summary>A flat world: the Screamer's place and heading, a player, and the senses that follow from them.</summary>
        sealed class World
        {
            public readonly ScreamerRules Rules = new ScreamerRules();
            public readonly ScreamerBody Body;
            public readonly ScreamerBrain Brain;
            public readonly ScreamerAnimator Animator = new ScreamerAnimator();
            public readonly ScreamerPose Pose = new ScreamerPose();
            public double X, Z, Heading, PlayerX, PlayerZ = 14.0, Time, Hurt;
            /// <summary>How the player moves by themselves, metres a second.</summary>
            public double PlayerVX, PlayerVZ;
            public bool Sees = true, Blocked;
            public ScreamerOrders Last;
            public readonly List<ScreamerMode> Modes = new List<ScreamerMode> { ScreamerMode.Perch };

            public World(int seed = 7)
            {
                Body = new ScreamerBody(Rules.hp);
                Brain = new ScreamerBrain(Rules, seed);
            }

            public Vec3d Local(double x, double z)
            {
                double dx = x - X, dz = z - Z, s = Math.Sin(Heading), c = Math.Cos(Heading);
                return new Vec3d(dx * c - dz * s, 0, dx * s + dz * c);
            }

            public double Distance => Math.Sqrt((PlayerX - X) * (PlayerX - X) + (PlayerZ - Z) * (PlayerZ - Z));

            /// <summary>The player, measured from its mouth.</summary>
            public Vec3d FromMouth => Local(PlayerX, PlayerZ) - new Vec3d(0, 0, ScreamerLayout.BodyFront);

            public ScreamerOrders Step(double dt = Dt)
            {
                PlayerX += PlayerVX * dt;
                PlayerZ += PlayerVZ * dt;
                double hs = Math.Sin(Heading), hc = Math.Cos(Heading);
                var senses = new ScreamerSenses
                {
                    SeesPlayer = Sees,
                    Player = Local(PlayerX, PlayerZ),
                    PlayerVelocity = new Vec3d(PlayerVX * hc - PlayerVZ * hs, 0, PlayerVX * hs + PlayerVZ * hc),
                    Home = Local(0, 0),
                    North = new Vec3d(-Math.Sin(Heading), 0, Math.Cos(Heading)),
                    Blocked = Blocked,
                };
                ScreamerOrders orders = Brain.Think(Body, senses, dt);
                Heading += orders.TurnRate * dt;
                double s = Math.Sin(Heading), c = Math.Cos(Heading);
                Vec3d moved = Blocked ? Vec3d.Zero : orders.Velocity;
                X += (moved.X * c + moved.Z * s) * dt;
                Z += (-moved.X * s + moved.Z * c) * dt;
                if (orders.Pulse) Hurt += ScreamerBrain.DamageRate(Rules, Body, FromMouth) * Rules.pulseSeconds;
                var input = new ScreamerAnimInput { Velocity = moved, TurnRate = orders.TurnRate, Height = orders.Height, Mode = orders.Mode, T = orders.T };
                Animator.Update(Body, input, dt, Pose);
                if (orders.Mode != Modes[Modes.Count - 1]) Modes.Add(orders.Mode);
                Time += dt;
                Last = orders;
                return orders;
            }

            /// <summary>Runs until it is in a mode; fails if that takes too long.</summary>
            public void Until(ScreamerMode mode, double atMost = 20.0, Action each = null)
            {
                double end = Time + atMost;
                while (Brain.Mode != mode)
                {
                    Assert.Less(Time, end, "never reached " + mode + " (went " + string.Join(", ", Modes) + ")");
                    Step();
                    each?.Invoke();
                }
            }
        }

        static void AssertSound(ScreamerPose pose, ScreamerBody body)
        {
            void Check(Vec3d p, QuatD q, string what)
            {
                Assert.IsFalse(double.IsNaN(p.X + p.Y + p.Z + q.X + q.Y + q.Z + q.W) || double.IsInfinity(p.X + p.Y + p.Z), what);
                Assert.AreEqual(1.0, Math.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W), 1e-6, what);
            }
            Check(pose.BodyPosition, pose.BodyRotation, "body");
            Check(pose.AbdomenPosition, pose.AbdomenRotation, "abdomen");
            for (int i = 0; i < ScreamerLayout.Drums; i++) Check(pose.DrumPosition[i], pose.DrumRotation[i], "drum " + i);
            for (int i = 0; i < ScreamerLayout.Wings; i++) if (body.WingOn(i)) Check(pose.WingPosition[i], pose.WingRotation[i], "wing " + i);
            for (int i = 0; i < ScreamerLayout.Legs; i++)
            {
                if (!body.LegOn(i)) continue;
                Check(pose.LegPosition[i], pose.LegRotation[i], "leg " + i);
                Check(pose.ShinPosition[i], pose.ShinRotation[i], "shin " + i);
            }
        }

        static Vec3d WingTip(ScreamerPose pose, int wing) => pose.WingPosition[wing] + pose.WingRotation[wing].Rotate(new Vec3d(0, 0, ScreamerLayout.WingLength(wing)));

        // ------------------------------------------------------------------ the body

        [Test]
        public void OnlyTheAbdomenOrTheForeBody_KillsIt()
        {
            var body = new ScreamerBody();
            for (int i = 0; i < ScreamerLayout.Wings; i++) Assert.IsFalse(body.HitWing(i, 1000).Killed);
            for (int i = 0; i < ScreamerLayout.Legs; i++) Assert.IsFalse(body.HitLeg(i, 1000).Killed);
            for (int i = 0; i < ScreamerLayout.Drums; i++) Assert.IsFalse(body.HitDrum(i, 1000).Killed);
            Assert.IsTrue(body.Alive, "stripped of everything it still lives");
            Assert.AreEqual(0, body.WingCount + body.LegCount + body.DrumCount);
            Assert.IsTrue(body.HitAbdomen(1000).Killed);
            Assert.IsFalse(body.Alive);

            var other = new ScreamerBody();
            Assert.IsTrue(other.HitBody(1000).Killed);
        }

        [Test]
        public void TheSoftAbdomen_IsTheQuickerKill_AndNothingTakesLongToShootOff()
        {
            ScreamerHp hp = ScreamerHp.Default;
            Assert.Less(hp.Abdomen, hp.Body * 0.6, "armour is tougher than the soft kill spot");
            Assert.LessOrEqual(Math.Ceiling(hp.ForeWing / Rifle), 2);
            Assert.LessOrEqual(Math.Ceiling(hp.HindWing / Rifle), 2);
            Assert.LessOrEqual(Math.Ceiling(hp.Leg / Rifle), 2);
            Assert.AreEqual(3, Math.Ceiling(hp.Drum / Rifle), "silencing it costs about what half a kill does");
            Assert.LessOrEqual(Math.Ceiling(hp.Abdomen / Rifle), 8, "learnable, not a bullet sponge");
        }

        [Test]
        public void DamageIsRemembered_AndAHitOnWhatIsGoneDoesNothing()
        {
            var body = new ScreamerBody();
            Assert.Less(body.HitWing(0, 25).WingLost, 0);
            Assert.AreEqual(0, body.HitWing(0, 25).WingLost);
            Assert.Less(body.HitWing(0, 25).WingLost, 0, "already gone");
            Assert.IsFalse(body.HitWing(0, 25).Lost);
            Assert.AreEqual(3, body.WingCount);
            Assert.AreEqual(2, body.HitLeg(2, 0, sever: true).LegLost, "a cut takes it at once");
        }

        [Test]
        public void ItsVoiceGoesWithItsDrums_ItsFlightWithItsWings_ItsWalkWithItsLegs()
        {
            var body = new ScreamerBody();
            Assert.AreEqual(1.0, body.Voice);
            Assert.AreEqual(1.0, body.FlightShare);
            Assert.AreEqual(1.0, body.WalkShare);

            body.HitDrum(0, 1000);
            Assert.AreEqual(0.6, body.Voice, 1e-9);
            Assert.IsTrue(body.CanScream);
            body.HitDrum(1, 1000);
            Assert.AreEqual(0.0, body.Voice);
            Assert.IsFalse(body.CanScream);

            body.HitWing(1, 1000);
            Assert.IsTrue(body.CanFly, "it flies on three");
            Assert.AreEqual(0.75, body.FlightShare, 1e-9);
            body.HitWing(2, 1000);
            Assert.IsFalse(body.CanFly);

            for (int i = 0; i < 3; i++) body.HitLeg(i, 1000);
            Assert.IsTrue(body.CanWalk, "it walks on three");
            Assert.AreEqual(0.5, body.WalkShare, 1e-9);
            body.HitLeg(3, 1000);
            Assert.IsFalse(body.CanWalk);
            Assert.AreEqual(0.0, body.WalkShare);
        }

        // ------------------------------------------------------------------ the scream

        [Test]
        public void TheScream_HurtsOnlyInFront_InRange_AndMostFromCloseUp()
        {
            var rules = new ScreamerRules();
            var body = new ScreamerBody();
            double near = ScreamerBrain.DamageRate(rules, body, new Vec3d(0, 0, 2));
            double far = ScreamerBrain.DamageRate(rules, body, new Vec3d(0, 0, 15));
            Assert.Greater(near, far * 1.8);
            Assert.Greater(far, 0.0);
            Assert.AreEqual(0.0, ScreamerBrain.DamageRate(rules, body, new Vec3d(0, 0, 17)), "out of range");
            Assert.AreEqual(0.0, ScreamerBrain.DamageRate(rules, body, new Vec3d(0, 0, -5)), "behind it");
            Assert.AreEqual(0.0, ScreamerBrain.DamageRate(rules, body, new Vec3d(6, 0, 6)), "off to the side");
            Assert.Greater(ScreamerBrain.DamageRate(rules, body, new Vec3d(2, 0, 6)), 0.0, "inside the cone");
            // The cone is the cone the rules say.
            double edge = rules.coneHalfAngle;
            Assert.Greater(ScreamerBrain.DamageRate(rules, body, new Vec3d(Math.Sin(edge - 0.02) * 8, 0, Math.Cos(edge - 0.02) * 8)), 0.0);
            Assert.AreEqual(0.0, ScreamerBrain.DamageRate(rules, body, new Vec3d(Math.Sin(edge + 0.02) * 8, 0, Math.Cos(edge + 0.02) * 8)));
        }

        [Test]
        public void WithOneDrum_TheScreamIsWeakerAndShorter_WithNone_Silent()
        {
            var rules = new ScreamerRules();
            var whole = new ScreamerBody();
            var one = new ScreamerBody();
            one.HitDrum(1, 1000);
            var none = new ScreamerBody();
            none.HitDrum(0, 1000);
            none.HitDrum(1, 1000);
            var at = new Vec3d(0, 0, 6);
            Assert.Less(ScreamerBrain.DamageRate(rules, one, at), ScreamerBrain.DamageRate(rules, whole, at) * 0.7);
            Assert.Greater(ScreamerBrain.DamageRate(rules, one, at), 0.0);
            Assert.Less(ScreamerBrain.Range(rules, one), ScreamerBrain.Range(rules, whole) - 2.0);
            Assert.AreEqual(rules.screamRange, ScreamerBrain.Range(rules, whole), 1e-6);
            Assert.AreEqual(0.0, ScreamerBrain.DamageRate(rules, none, at));
            Assert.AreEqual(0.0, ScreamerBrain.DamageRate(rules, none, new Vec3d(0, 0, 0.3)));
        }

        [Test]
        public void TheConeIsRound_AboveItIsAsSafeAsBesideIt_AndRightBehindItIsSafeHoweverNear()
        {
            var rules = new ScreamerRules();
            var body = new ScreamerBody();
            Assert.AreEqual(0.0, ScreamerBrain.DamageRate(rules, body, new Vec3d(0, 6, 6)), "overhead");
            Assert.Greater(ScreamerBrain.DamageRate(rules, body, new Vec3d(0, 2, 6)), 0.0, "a little above the middle is still in it");
            Assert.AreEqual(ScreamerBrain.DamageRate(rules, body, new Vec3d(2, 0, 6)), ScreamerBrain.DamageRate(rules, body, new Vec3d(0, 2, 6)), 1e-9);
            Assert.AreEqual(0.0, ScreamerBrain.DamageRate(rules, body, new Vec3d(0, 0, -0.3)), "no hidden rule close up");
            Assert.AreEqual(0.0, ScreamerBrain.DamageRate(rules, body, new Vec3d(0.4, 0, 0.1)));
        }

        [Test]
        public void StandingInIt_IsNeverMoreThanAFightCanBear()
        {
            // A whole scream from as near as it will let you stand must not kill a fresh player (100) outright.
            var rules = new ScreamerRules();
            Assert.Less(rules.damageNear * rules.screamSeconds, 50.0);
            Assert.Greater(rules.damageFar * rules.screamSeconds, 10.0, "nor is it nothing at the far edge");
        }

        // ------------------------------------------------------------------ the brain

        [Test]
        public void LeftAlone_ItNeverScreams_AndStaysNearHome()
        {
            var world = new World { Sees = false, PlayerX = 200, PlayerZ = 200 };
            for (int i = 0; i < 60 * 90; i++)
            {
                ScreamerOrders orders = world.Step();
                Assert.AreNotEqual(ScreamerMode.WindUp, orders.Mode);
                Assert.AreNotEqual(ScreamerMode.Scream, orders.Mode);
                Assert.Less(Math.Sqrt(world.X * world.X + world.Z * world.Z), world.Rules.wanderRadius + 3.0);
            }
            Assert.Contains(ScreamerMode.Fly, world.Modes, "it does take short flights");
        }

        [Test]
        public void Hunting_ItFliesToAPerch_Lands_Warns_ThenScreams()
        {
            var world = new World { PlayerZ = 30 };
            world.Until(ScreamerMode.Fly, 2.0);
            double top = 0;
            world.Until(ScreamerMode.Land, 15.0, () => top = Math.Max(top, world.Last.Height));
            Assert.AreEqual(world.Rules.cruiseHeight, top, 0.01, "it flies at its cruising height");
            world.Until(ScreamerMode.WindUp, 6.0);
            Assert.AreEqual(0.0, world.Last.Height, 1e-9, "it screams from the ground");
            Assert.AreEqual(world.Rules.perchRange, world.Distance, 3.0, "it lands about its perch range away");
            Assert.Less(world.Distance, ScreamerBrain.Range(world.Rules, world.Body));
            Assert.Less(Math.Abs(Math.Atan2(world.Local(world.PlayerX, world.PlayerZ).X, world.Local(world.PlayerX, world.PlayerZ).Z)), 0.3, "facing the player");

            double warned = world.Time;
            Assert.AreEqual(0.0, world.Hurt);
            world.Until(ScreamerMode.Scream, 3.0);
            Assert.AreEqual(world.Rules.windUpSeconds, world.Time - warned, 0.05, "the whole warning, every time");
            Assert.AreEqual(0.0, world.Hurt, "nothing hurts before the scream");
            double began = world.Time;
            world.Until(ScreamerMode.Spent, 5.0);
            Assert.AreEqual(world.Rules.screamSeconds, world.Time - began, 0.05);
            Assert.Greater(world.Hurt, 15.0, "someone who just stands there is hurt");
            Assert.Less(world.Hurt, 45.0);
        }

        [Test]
        public void AfterTheLock_ItNoLongerFollows_AndTheScreamOnlySwingsSlowly()
        {
            var world = new World { PlayerZ = 9 };
            world.Brain.Alert();
            world.Until(ScreamerMode.WindUp, 20.0);
            while (world.Brain.Mode == ScreamerMode.WindUp)
            {
                // The player runs round it the whole time.
                world.PlayerX += 8.0 * Dt;
                ScreamerOrders orders = world.Step();
                if (orders.Mode == ScreamerMode.WindUp && orders.T >= ScreamerBrain.LockAt + 0.02) Assert.AreEqual(0.0, orders.TurnRate, "locked");
            }
            while (world.Brain.Mode == ScreamerMode.Scream)
            {
                world.PlayerX += 8.0 * Dt;
                Assert.LessOrEqual(Math.Abs(world.Step().TurnRate), world.Rules.screamTurn + 1e-9);
            }
        }

        [Test]
        public void RunningOutOfTheCone_SavesMostOfTheHurt()
        {
            double stood = Dose(0.0), walked = Dose(5.0), ran = Dose(8.0);
            Assert.Greater(stood, 15.0);
            Assert.Less(ran, stood * 0.5, "a sprint to the side gets out of it");
            Assert.LessOrEqual(ran, walked + 1e-9);
        }

        /// <summary>The hurt of one scream to someone who moves sideways at a pace from the moment its wings jerk up (the lock).</summary>
        static double Dose(double pace)
        {
            var world = new World { PlayerZ = 9 };
            world.Brain.Alert();
            world.Until(ScreamerMode.WindUp, 20.0);
            double before = world.Hurt;
            while (world.Brain.Mode == ScreamerMode.WindUp || world.Brain.Mode == ScreamerMode.Scream)
            {
                if (world.Brain.Mode == ScreamerMode.Scream || world.Last.T >= ScreamerBrain.LockAt)
                {
                    // Sideways, as the Screamer sees it.
                    double s = Math.Sin(world.Heading), c = Math.Cos(world.Heading);
                    world.PlayerX += c * pace * Dt;
                    world.PlayerZ += -s * pace * Dt;
                }
                world.Step();
            }
            return world.Hurt - before;
        }

        [Test]
        public void SomeoneWalkingAway_IsStillScreamedAt()
        {
            // It lands where they are going, not where they were when it took off.
            foreach ((double vx, double vz) in new[] { (0.0, 5.0), (5.0, 0.0), (-3.5, 3.5) })
            {
                var world = new World { PlayerZ = 12, PlayerVX = vx, PlayerVZ = vz };
                world.Rules.roamRadius = 1000f;
                var log = new System.Text.StringBuilder();
                ScreamerMode was = ScreamerMode.Perch;
                for (int i = 0; i < 60 * 30 && world.Hurt <= 0.0; i++) { world.Step(); if (world.Brain.Mode != was) { was = world.Brain.Mode; Vec3d m = world.FromMouth; log.Append(was + "@" + world.Time.ToString("0.0") + " (" + m.X.ToString("0.0") + "," + m.Z.ToString("0.0") + ") "); } }
                if (world.Hurt <= 0.0) Assert.Fail(log.ToString());
                Assert.Greater(world.Hurt, 0.0, "walking " + vx + ", " + vz + ": " + string.Join(", ", world.Modes));
            }
        }

        [Test]
        public void ChasedFromItsPerch_ItFliesOnce_ThenStandsItsGroundAndScreams()
        {
            var world = new World { PlayerZ = 12 };
            int flights = 0;
            ScreamerMode last = ScreamerMode.Perch;
            for (int i = 0; i < 60 * 40 && !world.Modes.Contains(ScreamerMode.Scream); i++)
            {
                // The player sprints straight at it the whole time.
                double dx = world.X - world.PlayerX, dz = world.Z - world.PlayerZ, far = Math.Sqrt(dx * dx + dz * dz);
                world.PlayerVX = far > 0.8 ? dx / far * 8.0 : 0.0;
                world.PlayerVZ = far > 0.8 ? dz / far * 8.0 : 0.0;
                ScreamerOrders orders = world.Step();
                if (orders.Mode == ScreamerMode.Fly && last != ScreamerMode.Fly) flights++;
                last = orders.Mode;
            }
            Assert.Contains(ScreamerMode.Scream, world.Modes, "it cannot be chased out of ever screaming");
            Assert.LessOrEqual(flights, 3);
        }

        [Test]
        public void ShootingADrumOff_ChokesTheScream()
        {
            var world = new World { PlayerZ = 9 };
            world.Brain.Alert();
            world.Until(ScreamerMode.Scream, 25.0);
            for (int i = 0; i < 20; i++) world.Step();
            double hurt = world.Hurt;
            Assert.Greater(hurt, 0.0);
            Assert.AreEqual(0, world.Body.HitDrum(0, 1000).DrumLost);
            Assert.IsTrue(world.Brain.Choke());
            Assert.AreEqual(ScreamerMode.Spent, world.Brain.Mode);
            double began = world.Time;
            while (world.Brain.Mode == ScreamerMode.Spent) world.Step();
            Assert.AreEqual(hurt, world.Hurt, 1e-9, "the scream stopped at once");
            Assert.AreEqual(world.Rules.chokeSeconds, world.Time - began, 0.05);
            Assert.Greater(world.Rules.chokeSeconds, world.Rules.spentSeconds, "choked is a longer opening than out of breath");
        }

        [Test]
        public void ADrumShotOffInTheWarning_MeansTheScreamNeverComes_AndAtRestItChokesNothing()
        {
            var resting = new World { Sees = false };
            Assert.IsFalse(resting.Brain.Choke());
            Assert.AreEqual(ScreamerMode.Perch, resting.Brain.Mode);

            var world = new World { PlayerZ = 9 };
            world.Brain.Alert();
            world.Until(ScreamerMode.WindUp, 25.0);
            for (int i = 0; i < 10; i++) world.Step();
            world.Body.HitDrum(1, 1000);
            Assert.IsTrue(world.Brain.Choke());
            while (world.Brain.Mode == ScreamerMode.Spent) world.Step();
            Assert.AreEqual(0.0, world.Hurt);
            Assert.IsFalse(world.Modes.Contains(ScreamerMode.Scream));
        }

        [Test]
        public void AfterAScream_ItSags_ThenFliesSomewhereElse()
        {
            var world = new World { PlayerZ = 9 };
            world.Brain.Alert();
            world.Until(ScreamerMode.Spent, 30.0);
            double x = world.X, z = world.Z, began = world.Time;
            while (world.Brain.Mode == ScreamerMode.Spent)
            {
                ScreamerOrders orders = world.Step();
                Assert.AreEqual(0.0, orders.Velocity.Magnitude, 1e-9, "it lies where it is: the time to shoot it");
                Assert.AreEqual(0.0, orders.TurnRate, 1e-9);
            }
            Assert.AreEqual(world.Rules.spentSeconds, world.Time - began, 0.05);
            Assert.AreEqual(ScreamerMode.Fly, world.Brain.Mode);
            world.Until(ScreamerMode.WindUp, 20.0);
            double moved = Math.Sqrt((world.X - x) * (world.X - x) + (world.Z - z) * (world.Z - z));
            Assert.Greater(moved, 3.0, "the next scream comes from somewhere else");
        }

        [Test]
        public void ItComesBackAgainAndAgain_NeverStuck()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                var world = new World(seed) { PlayerX = 5 * seed, PlayerZ = 20 };
                int screams = 0;
                ScreamerMode last = ScreamerMode.Perch;
                for (int i = 0; i < 60 * 60; i++)
                {
                    ScreamerOrders orders = world.Step();
                    if (orders.Mode == ScreamerMode.Scream && last != ScreamerMode.Scream) screams++;
                    last = orders.Mode;
                    Assert.Less(Math.Sqrt(world.X * world.X + world.Z * world.Z), world.Rules.roamRadius + 2.0, "it keeps to its range");
                    AssertSound(world.Pose, world.Body);
                }
                Assert.GreaterOrEqual(screams, 4, "seed " + seed + ": " + string.Join(", ", world.Modes));
                Assert.LessOrEqual(screams, 12, "and there is room between screams (rule 8)");
            }
        }

        [Test]
        public void ComeUponOnTheGround_ItTakesOff_ButNotWhileItIsSpent()
        {
            var world = new World { PlayerZ = 9 };
            world.Brain.Alert();
            world.Until(ScreamerMode.Spent, 30.0);
            world.PlayerX = world.X + 1.0;
            world.PlayerZ = world.Z + 1.0;
            for (int i = 0; i < 30; i++)
            {
                world.Step();
                Assert.AreEqual(ScreamerMode.Spent, world.Brain.Mode, "spent, it cannot get away");
            }

            var perched = new World { Sees = false, PlayerX = 100 };
            for (int i = 0; i < 30; i++) perched.Step();
            Assert.AreEqual(ScreamerMode.Perch, perched.Brain.Mode);
            perched.Sees = true;
            perched.PlayerX = perched.X + 2.0;
            perched.PlayerZ = perched.Z + 1.0;
            for (int i = 0; i < 30 && perched.Brain.Mode == ScreamerMode.Perch; i++) perched.Step();
            Assert.AreEqual(ScreamerMode.Fly, perched.Brain.Mode);
        }

        [Test]
        public void ShotOutOfTheAir_ItFalls_LiesStunned_AndIsOnTheGroundForGood()
        {
            var world = new World { PlayerZ = 40 };
            world.Until(ScreamerMode.Fly, 2.0);
            while (world.Last.Height < world.Rules.cruiseHeight - 0.01) world.Step();
            world.Body.HitWing(0, 1000);
            world.Step();
            Assert.AreEqual(ScreamerMode.Fly, world.Brain.Mode, "three wings still fly");
            world.Body.HitWing(3, 1000);
            world.Step();
            Assert.AreEqual(ScreamerMode.Fall, world.Brain.Mode);
            int crashes = 0;
            double began = world.Time;
            while (world.Brain.Mode == ScreamerMode.Fall)
            {
                if (world.Step().Crashed) crashes++;
                Assert.Less(world.Time - began, 2.0);
            }
            Assert.AreEqual(1, crashes);
            Assert.Greater(world.Time - began, 0.6, "it falls, it is not put down");
            Assert.AreEqual(ScreamerMode.Spent, world.Brain.Mode);
            began = world.Time;
            while (world.Brain.Mode == ScreamerMode.Spent) world.Step();
            Assert.AreEqual(world.Rules.crashSeconds, world.Time - began, 0.05);

            // From now on it crawls: slower than the player walks, never off the ground, and it still screams.
            double far = world.Distance;
            for (int i = 0; i < 60 * 40 && world.Brain.Mode != ScreamerMode.Scream; i++)
            {
                double x = world.X, z = world.Z;
                ScreamerOrders orders = world.Step();
                Assert.AreEqual(0.0, orders.Height, 1e-9);
                Assert.AreNotEqual(ScreamerMode.Fly, orders.Mode);
                double pace = Math.Sqrt((world.X - x) * (world.X - x) + (world.Z - z) * (world.Z - z)) / Dt;
                Assert.LessOrEqual(pace, world.Rules.walkPace + 1e-6);
            }
            Assert.AreEqual(ScreamerMode.Scream, world.Brain.Mode, "it crawled into range and screamed");
            Assert.Less(world.Distance, far);
            Assert.Less(world.Rules.walkPace, 5.0);
        }

        [Test]
        public void WithNoDrums_ItNeverWarns_AndKeepsAway()
        {
            var world = new World { PlayerZ = 9 };
            world.Body.HitDrum(0, 1000);
            world.Body.HitDrum(1, 1000);
            for (int i = 0; i < 60 * 30; i++)
            {
                ScreamerOrders orders = world.Step();
                Assert.AreNotEqual(ScreamerMode.WindUp, orders.Mode);
                Assert.AreNotEqual(ScreamerMode.Scream, orders.Mode);
                Assert.IsFalse(orders.Pulse);
            }
            Assert.Greater(world.Distance, world.Rules.fleeRange - 2.0);
            Assert.AreEqual(ScreamerMode.Perch, world.Brain.Mode, "and settles there");
            // It made off once. Walked up to, it stays to be finished: no chase.
            double x = world.X, z = world.Z;
            world.PlayerX = world.X + 2.0;
            world.PlayerZ = world.Z;
            for (int i = 0; i < 60 * 5; i++) world.Step();
            Assert.AreEqual(x, world.X, 1e-9);
            Assert.AreEqual(z, world.Z, 1e-9);
            Assert.Less(world.Rules.fleeRange, world.Rules.screamRange, "a short way: a clean-up shot, not a hunt");
        }

        [Test]
        public void WithNoWingsAndNoLegs_ItStillTurnsAndScreamsAtSomeoneInRange()
        {
            var world = new World { PlayerX = 7, PlayerZ = -3 };
            for (int i = 0; i < ScreamerLayout.Wings; i++) world.Body.HitWing(i, 1000);
            for (int i = 0; i < ScreamerLayout.Legs; i++) world.Body.HitLeg(i, 1000);
            world.Until(ScreamerMode.Scream, 12.0);
            Assert.AreEqual(0.0, world.X, 1e-9, "it never moved");
            Assert.AreEqual(0.0, world.Z, 1e-9);
            Assert.AreEqual(ScreamerLayout.BellyHeight, world.Pose.BodyPosition.Y, 0.08, "on its belly");
        }

        [Test]
        public void BlockedInTheAir_ItComesDownRatherThanHang()
        {
            var world = new World { PlayerZ = 30, Blocked = true };
            for (int i = 0; i < 60 * 12; i++) world.Step();
            Assert.AreNotEqual(ScreamerMode.Fly, world.Brain.Mode);
            Assert.AreEqual(0.0, world.Last.Height, 1e-9);
        }

        [Test]
        public void APlaceKeptInMind_IsTheSamePlaceHoweverItHasTurned()
        {
            var world = new World { X = 3, Z = -4, Heading = 1.1 };
            var senses = new ScreamerSenses { Home = world.Local(0, 0), North = new Vec3d(-Math.Sin(world.Heading), 0, Math.Cos(world.Heading)) };
            Vec3d there = ScreamerBrain.ToWorld(senses, world.Local(10, 6));
            Assert.AreEqual(10.0, there.X, 1e-9);
            Assert.AreEqual(6.0, there.Z, 1e-9);
            Vec3d back = ScreamerBrain.FromWorld(senses, there);
            Assert.AreEqual(world.Local(10, 6).X, back.X, 1e-9);
            Assert.AreEqual(world.Local(10, 6).Z, back.Z, 1e-9);
        }

        [Test]
        public void TheSameSeed_IsTheSameFight()
        {
            var a = new World(11) { PlayerZ = 25 };
            var b = new World(11) { PlayerZ = 25 };
            for (int i = 0; i < 60 * 20; i++)
            {
                a.Step();
                b.Step();
            }
            Assert.AreEqual(a.X, b.X);
            Assert.AreEqual(a.Z, b.Z);
            Assert.AreEqual(a.Hurt, b.Hurt);
        }

        // ------------------------------------------------------------------ how it moves

        [Test]
        public void AtRest_ItsWingsLieBackOverItLikeARoof_TheSameBothSides()
        {
            var world = new World { Sees = false, PlayerX = 300 };
            for (int i = 0; i < 20; i++) world.Step();
            ScreamerPose pose = world.Pose;
            AssertSound(pose, world.Body);
            for (int side = 0; side < 2; side++)
            {
                Vec3d tip = WingTip(pose, ScreamerLayout.Wing(side, false));
                Assert.Less(tip.Z, -1.2, "the fore wing reaches back past the abdomen");
                Assert.Less(Math.Abs(tip.X), 0.6);
                Assert.Greater(tip.Y, 0.2, "and clear of the ground");
                // The trailing edge (the piece's +X) is toward the middle and up: a roof.
                Vec3d trailing = pose.WingRotation[ScreamerLayout.Wing(side, false)].Rotate(Vec3d.Right);
                Assert.Less(trailing.X * ScreamerLayout.Sign(side), -0.3);
                Assert.Greater(trailing.Y, 0.3);
            }
            Vec3d right = WingTip(pose, 0), left = WingTip(pose, 2);
            Assert.AreEqual(right.X, -left.X, 1e-6);
            Assert.AreEqual(right.Y, left.Y, 1e-6);
            Assert.AreEqual(right.Z, left.Z, 1e-6);
        }

        [Test]
        public void InTheAir_ItsWingsAreOutAndBeating_ItsLegsTucked_ItsNoseUp()
        {
            var world = new World { PlayerZ = 60 };
            world.Until(ScreamerMode.Fly, 2.0);
            while (world.Last.Height < world.Rules.cruiseHeight - 0.01) world.Step();
            double low = double.MaxValue, high = double.MinValue;
            for (int i = 0; i < 30; i++)
            {
                world.Step();
                ScreamerPose pose = world.Pose;
                AssertSound(pose, world.Body);
                Vec3d right = WingTip(pose, 0), left = WingTip(pose, 2);
                Assert.Greater(right.X, 0.9);
                Assert.Less(left.X, -0.9);
                low = Math.Min(low, right.Y - pose.WingPosition[0].Y);
                high = Math.Max(high, right.Y - pose.WingPosition[0].Y);
                // Either wing's trailing edge trails; the right one's upper side is up, the left one's down (the same piece turned over).
                Assert.Less(pose.WingRotation[0].Rotate(Vec3d.Right).Z, -0.5);
                Assert.Less(pose.WingRotation[2].Rotate(Vec3d.Right).Z, -0.5);
                Assert.Greater(pose.WingRotation[0].Rotate(Vec3d.Up).Y, 0.2);
                Assert.Less(pose.WingRotation[2].Rotate(Vec3d.Up).Y, -0.2);
                Assert.Greater(pose.BodyPosition.Y, world.Rules.cruiseHeight, "the body is carried at its height");
                Assert.Greater(pose.BodyRotation.Rotate(Vec3d.Forward).Y, 0.05, "nose up");
                for (int leg = 0; leg < ScreamerLayout.Legs; leg++)
                {
                    Assert.Greater(pose.FootPosition[leg].Y, world.Rules.cruiseHeight - 0.4, "feet off the ground");
                    Assert.Less((pose.FootPosition[leg] - pose.BodyPosition).Magnitude, 0.9, "tucked");
                }
            }
            Assert.Greater(high - low, 1.0, "the wings beat through a wide stroke");
        }

        [Test]
        public void ItBanksIntoItsTurns()
        {
            var body = new ScreamerBody();
            var pose = new ScreamerPose();
            foreach (double turn in new[] { 2.0, -2.0 })
            {
                var animator = new ScreamerAnimator();
                var input = new ScreamerAnimInput { Mode = ScreamerMode.Fly, Height = 3.4, Velocity = new Vec3d(0, 0, 8), TurnRate = turn };
                for (int i = 0; i < 60; i++) animator.Update(body, input, Dt, pose);
                // Turning right, its right side is the low one.
                Assert.Less(pose.BodyRotation.Rotate(Vec3d.Right).Y * turn, -0.1);
            }
        }

        [Test]
        public void TheWarning_LiftsItsAbdomenAndFansItsWings_AndTheLockIsAJerk()
        {
            Assert.AreEqual(0.0, ScreamerAnimator.FanOf(ScreamerMode.Perch, 0));
            double before = ScreamerAnimator.FanOf(ScreamerMode.WindUp, ScreamerBrain.LockAt - 0.01);
            double after = ScreamerAnimator.FanOf(ScreamerMode.WindUp, ScreamerBrain.LockAt + 0.12);
            Assert.Greater(after - before, 0.4, "the wings jump at the lock");
            Assert.AreEqual(1.0, after, 1e-9);
            Assert.AreEqual(1.0, ScreamerAnimator.FanOf(ScreamerMode.Scream, 0.5));
            Assert.Greater(ScreamerAnimator.LiftOf(ScreamerMode.WindUp, ScreamerBrain.LockAt + 0.12) - ScreamerAnimator.LiftOf(ScreamerMode.WindUp, ScreamerBrain.LockAt - 0.01), 0.3);
            Assert.Less(ScreamerAnimator.LiftOf(ScreamerMode.Spent, 0.5), 0.0, "spent, it sags");
            for (double t = 0.02; t <= 1.0; t += 0.02)
            {
                Assert.GreaterOrEqual(ScreamerAnimator.FanOf(ScreamerMode.WindUp, t), ScreamerAnimator.FanOf(ScreamerMode.WindUp, t - 0.02) - 1e-9, "never back down");
                Assert.GreaterOrEqual(ScreamerAnimator.LiftOf(ScreamerMode.WindUp, t), ScreamerAnimator.LiftOf(ScreamerMode.WindUp, t - 0.02) - 1e-9);
            }

            // In the pose: the abdomen's tip and the wing tips are far higher in the scream than at rest.
            var world = new World { PlayerZ = 9 };
            world.Brain.Alert();
            world.Until(ScreamerMode.WindUp, 25.0);
            Vec3d Tail() => world.Pose.AbdomenPosition + world.Pose.AbdomenRotation.Rotate(new Vec3d(0, 0, -ScreamerLayout.AbdomenLength));
            double tailAtRest = Tail().Y, tipAtRest = WingTip(world.Pose, 0).Y;
            world.Until(ScreamerMode.Scream, 3.0);
            for (int i = 0; i < 10; i++) world.Step();
            AssertSound(world.Pose, world.Body);
            Assert.Greater(Tail().Y - tailAtRest, 0.45);
            Assert.Greater(WingTip(world.Pose, 0).Y - tipAtRest, 0.8);
            Assert.Greater(WingTip(world.Pose, 0).X, 0.9, "fanned out to the side");
            Assert.AreEqual(1.0, world.Pose.Buzz, 1e-9);
            // No foot leaves the ground while it screams.
            for (int leg = 0; leg < ScreamerLayout.Legs; leg++) Assert.AreEqual(0.0, world.Pose.FootPosition[leg].Y, 0.01);
        }

        [Test]
        public void ItsDrumsStandOutPastItsSides_SoTheyCanBeShotFromInFront()
        {
            var world = new World { Sees = false, PlayerX = 300 };
            for (int i = 0; i < 5; i++) world.Step();
            ScreamerPose pose = world.Pose;
            Assert.Greater(pose.DrumPosition[0].X + ScreamerLayout.DrumRadius * 0.5, ScreamerLayout.BodyHalfWidth + 0.03);
            Assert.AreEqual(pose.DrumPosition[0].X, -pose.DrumPosition[1].X, 1e-9);
            Assert.AreEqual(pose.DrumPosition[0].Y, pose.DrumPosition[1].Y, 1e-9);
            // The left drum is the right one turned half a turn about its length: its bulge (+X of the piece) faces left.
            Assert.Greater(pose.DrumRotation[0].Rotate(Vec3d.Right).X, 0.9);
            Assert.Less(pose.DrumRotation[1].Rotate(Vec3d.Right).X, -0.9);
        }

        [Test]
        public void ItsLegsReachTheirFeet_KneesUp_AndItWalksOnAlternatingThrees()
        {
            var world = new World { PlayerZ = 40 };
            // Grounded for good, so it has to walk.
            world.Body.HitWing(0, 1000);
            world.Body.HitWing(1, 1000);
            int mostUp = 0;
            bool stepped = false;
            for (int i = 0; i < 60 * 6; i++)
            {
                world.Step();
                ScreamerPose pose = world.Pose;
                int up = 0;
                for (int leg = 0; leg < ScreamerLayout.Legs; leg++)
                {
                    Vec3d hip = pose.LegPosition[leg], knee = pose.ShinPosition[leg], foot = pose.FootPosition[leg];
                    Assert.AreEqual(ScreamerLayout.LegLength, (knee - hip).Magnitude, 1e-6);
                    Assert.AreEqual(ScreamerLayout.ShinLength, (foot - knee).Magnitude, 1e-6);
                    Assert.Greater(knee.Y, Math.Min(hip.Y, foot.Y), "knee up");
                    Assert.GreaterOrEqual(foot.Y, -1e-6);
                    // The pieces point where the pose says.
                    Vec3d along = pose.LegRotation[leg].Rotate(Vec3d.Forward);
                    Assert.AreEqual(0.0, (hip + along * ScreamerLayout.LegLength - knee).Magnitude, 1e-6);
                    if (foot.Y > 0.02) up++;
                }
                mostUp = Math.Max(mostUp, up);
                stepped |= up > 0;
                Assert.LessOrEqual(up, 3, "never more than one three in the air");
            }
            Assert.IsTrue(stepped);
            Assert.AreEqual(3, mostUp);
            Assert.Greater(Math.Sqrt(world.X * world.X + world.Z * world.Z), 5.0, "it got somewhere");
        }

        [Test]
        public void EveryPose_ThroughAFightWithLosses_IsSound()
        {
            var world = new World(3) { PlayerZ = 22 };
            for (int i = 0; i < 60 * 50; i++)
            {
                if (i == 400) world.Body.HitLeg(1, 1000);
                if (i == 900) world.Body.HitWing(2, 1000);
                if (i == 1400) { world.Body.HitDrum(0, 1000); world.Brain.Choke(); }
                if (i == 1900) world.Body.HitWing(1, 1000);
                if (i == 2300) for (int leg = 2; leg < 6; leg++) world.Body.HitLeg(leg, 1000);
                world.Step();
                AssertSound(world.Pose, world.Body);
                Assert.GreaterOrEqual(world.Pose.BodyPosition.Y, ScreamerLayout.BellyHeight - 0.08, "never through the ground");
            }
        }

        // ------------------------------------------------------------------ the model

        static ModelFile Load(string id)
        {
            string folder = Environment.GetEnvironmentVariable("OA_MODELS");
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                folder = null;
                string dir = TestContext.CurrentContext.TestDirectory;
                for (int i = 0; i < 10 && dir != null && folder == null; i++)
                {
                    foreach (string candidate in new[] { Path.Combine(dir, "unity", "Assets", "OrbitalAnte", "Resources", "Models"), Path.Combine(dir, "Assets", "OrbitalAnte", "Resources", "Models") })
                        if (Directory.Exists(candidate)) folder = candidate;
                    dir = Path.GetDirectoryName(dir);
                }
            }
            if (folder == null) Assert.Ignore("Resources/Models not found");
            string path = Path.Combine(folder, id + ".bytes");
            if (!File.Exists(path)) Assert.Ignore("this model is not in the game yet: " + path);
            ModelFile model = ModelFile.Parse(File.ReadAllBytes(path), out string problem);
            Assert.IsNotNull(model, problem);
            return model;
        }

        static void AssertSocket(ModelFile model, string name, double x, double y, double z)
        {
            Assert.IsTrue(model.TrySocket(name, out ModelSocket s), "missing " + name);
            Assert.AreEqual(x, s.X, 0.002, name + " x");
            Assert.AreEqual(y, s.Y, 0.002, name + " y");
            Assert.AreEqual(z, s.Z, 0.002, name + " z");
        }

        [Test]
        public void ScreamerModelFile_MatchesTheLayout()
        {
            // tools/blender/build_creatures.py carries its own copy of ScreamerLayout (SCREAMER). If either changes,
            // this fails until the script is changed to match and run again.
            ModelFile model = Load(ScreamerLayout.Model);
            AssertSocket(model, "BodyFront", 0, 0, ScreamerLayout.BodyFront);
            AssertSocket(model, "BodyBack", 0, 0, ScreamerLayout.BodyBack);
            AssertSocket(model, "BodySide", ScreamerLayout.BodyHalfWidth, 0, 0);
            AssertSocket(model, "BodyTop", 0, ScreamerLayout.BodyHalfHeight, 0);
            AssertSocket(model, "AbdomenJoint", ScreamerLayout.AbdomenJoint.X, ScreamerLayout.AbdomenJoint.Y, ScreamerLayout.AbdomenJoint.Z);
            AssertSocket(model, "AbdomenEnd", 0, 0, -ScreamerLayout.AbdomenLength);
            AssertSocket(model, "AbdomenSide", ScreamerLayout.AbdomenHalfWidth, 0, 0);
            AssertSocket(model, "DrumR", ScreamerLayout.DrumAt.X, ScreamerLayout.DrumAt.Y, ScreamerLayout.DrumAt.Z);
            AssertSocket(model, "DrumRim", 0, 0, ScreamerLayout.DrumRadius);
            AssertSocket(model, "ForeHingeR", ScreamerLayout.ForeHinge.X, ScreamerLayout.ForeHinge.Y, ScreamerLayout.ForeHinge.Z);
            AssertSocket(model, "HindHingeR", ScreamerLayout.HindHinge.X, ScreamerLayout.HindHinge.Y, ScreamerLayout.HindHinge.Z);
            AssertSocket(model, "ForeWingEnd", 0, 0, ScreamerLayout.ForeWingLength);
            AssertSocket(model, "ForeWingChord", ScreamerLayout.ForeWingChord, 0, 0);
            AssertSocket(model, "HindWingEnd", 0, 0, ScreamerLayout.HindWingLength);
            AssertSocket(model, "HindWingChord", ScreamerLayout.HindWingChord, 0, 0);
            AssertSocket(model, "LegEnd", 0, 0, ScreamerLayout.LegLength);
            AssertSocket(model, "ShinEnd", 0, 0, ScreamerLayout.ShinLength);
            AssertSocket(model, "LegSide", ScreamerLayout.LegRadius, 0, 0);
            AssertSocket(model, "StumpRim", ScreamerLayout.StumpRadius, 0, 0);
            for (int k = 0; k < 3; k++) AssertSocket(model, "Hip" + k + "R", ScreamerLayout.Hip[k].X, ScreamerLayout.Hip[k].Y, ScreamerLayout.Hip[k].Z);
            string[] pieces = { ScreamerLayout.BodyPiece, ScreamerLayout.AbdomenPiece, ScreamerLayout.DrumPiece, ScreamerLayout.ForeWingPiece, ScreamerLayout.HindWingPiece,
                                ScreamerLayout.LegPiece, ScreamerLayout.ShinPiece, ScreamerLayout.Stump, ScreamerLayout.Meat[0], ScreamerLayout.Meat[1] };
            foreach (string piece in pieces) Assert.IsNotNull(model.Part(piece), "missing piece " + piece);
            Assert.IsTrue(model.Textured, "the Screamer is drawn with pictures");

            // The shapes are the size the rules (and the hitboxes) think they are. Front and back are along +Z.
            void Box(string piece, out float right, out float left, out float hi, out float lo, out float fore, out float aft)
            {
                ModelPart part = model.Part(piece);
                right = float.MinValue; left = float.MaxValue; hi = float.MinValue; lo = float.MaxValue; fore = float.MinValue; aft = float.MaxValue;
                for (int i = 0; i < part.VertexCount; i++)
                {
                    right = Math.Max(right, part.Positions[i * 3]);
                    left = Math.Min(left, part.Positions[i * 3]);
                    hi = Math.Max(hi, part.Positions[i * 3 + 1]);
                    lo = Math.Min(lo, part.Positions[i * 3 + 1]);
                    fore = Math.Max(fore, part.Positions[i * 3 + 2]);
                    aft = Math.Min(aft, part.Positions[i * 3 + 2]);
                }
            }
            Box(ScreamerLayout.BodyPiece, out float right, out float left, out float hi, out float lo, out float fore, out float aft);
            Assert.AreEqual(ScreamerLayout.BodyHalfWidth, right, 0.03, "fore body half width");
            Assert.AreEqual(-right, left, 0.01, "the fore body is the same either side");
            Assert.AreEqual(ScreamerLayout.BodyHalfHeight, hi, 0.03, "fore body top");
            Assert.AreEqual(ScreamerLayout.BodyFront, fore, 0.09, "fore body front (its feelers reach a little past)");
            Assert.AreEqual(ScreamerLayout.BodyBack, aft, 0.03, "fore body back");
            Assert.Greater(lo, -ScreamerLayout.BodyHalfHeight - 0.07);

            Box(ScreamerLayout.AbdomenPiece, out right, out left, out hi, out lo, out fore, out aft);
            Assert.AreEqual(ScreamerLayout.AbdomenHalfWidth, right, 0.03, "abdomen half width");
            Assert.AreEqual(-ScreamerLayout.AbdomenLength, aft, 0.02, "the abdomen runs back from its joint");
            Assert.Less(fore, 0.2, "and only its waist is in front of the joint");

            // The drum: round, bulging toward +X, the same above as below so the left one can be the right one turned over.
            Box(ScreamerLayout.DrumPiece, out right, out left, out hi, out lo, out fore, out aft);
            Assert.AreEqual(ScreamerLayout.DrumRadius, hi, 0.012);
            Assert.AreEqual(hi, -lo, 0.006, "the same above as below");
            Assert.AreEqual(ScreamerLayout.DrumRadius, fore, 0.012);
            Assert.Greater(right, 0.045, "it bulges out");
            Assert.Less(-left, 0.08, "and its rim is sunk no deeper into the flank than that");

            // A wing reaches its length, is flat, trails toward +X, and is the same above as below.
            foreach ((string piece, double length, double chord) in new[] { (ScreamerLayout.ForeWingPiece, ScreamerLayout.ForeWingLength, ScreamerLayout.ForeWingChord), (ScreamerLayout.HindWingPiece, ScreamerLayout.HindWingLength, ScreamerLayout.HindWingChord) })
            {
                Box(piece, out right, out left, out hi, out lo, out fore, out aft);
                Assert.AreEqual(length, fore, 0.02, piece + " length");
                Assert.AreEqual(chord, right - left, 0.04, piece + " chord");
                Assert.Greater(right, -left * 3.0, piece + " trails toward +X");
                Assert.AreEqual(hi, -lo, 0.004, piece + " is the same above as below");
                Assert.Less(hi, 0.05, piece + " is flat");
            }
            // A leg and a shin reach their lengths and are the same either side.
            foreach ((string piece, double length) in new[] { (ScreamerLayout.LegPiece, ScreamerLayout.LegLength), (ScreamerLayout.ShinPiece, ScreamerLayout.ShinLength) })
            {
                Box(piece, out right, out left, out hi, out lo, out fore, out aft);
                Assert.AreEqual(length, fore, 0.02, piece + " length");
                Assert.AreEqual(right, -left, 0.006, piece + " is the same either side");
                Assert.Less(right, 0.06);
            }
        }
    }
}
