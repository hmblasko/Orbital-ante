using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;
using OrbitalAnte.Look;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>The Ambusher (the bobbit worm): what comes off and what kills it, the wait, the warning, the strike, the hold and the ways out of it, and tunnelling.</summary>
    public class AmbusherRulesTests
    {
        const double Dt = 1.0 / 60.0;
        const double Rifle = 24.0;

        /// <summary>A flat world: the Ambusher's hole and heading, a player, and the senses that follow from them.</summary>
        sealed class World
        {
            public readonly AmbusherRules Rules = new AmbusherRules();
            public readonly AmbusherBody Body;
            public readonly AmbusherBrain Brain;
            public readonly AmbusherAnimator Animator = new AmbusherAnimator();
            public readonly AmbusherPose Pose = new AmbusherPose();
            public double X, Z, Heading, PlayerX, PlayerZ = 3.5, PlayerY, Time, Hurt, Noise = 1.0, HomeX, HomeZ;
            /// <summary>How the player moves by themselves, metres a second.</summary>
            public double PlayerVX, PlayerVZ;
            public bool ClearLine = true, ClearAbove = true, Hostile = true;
            public int Bites;
            public double Dragged, FastestMove, MovedOutOfTunnel;
            public AmbusherOrders Last;
            public readonly List<AmbusherMode> Modes = new List<AmbusherMode> { AmbusherMode.Lurk };
            /// <summary>Called before every step, to move the player by a plan.</summary>
            public Action<World> Before;

            public World()
            {
                Body = new AmbusherBody(Rules.hp);
                Brain = new AmbusherBrain(Rules);
            }

            public Vec3d Local(double x, double z, double y = 0.0)
            {
                double dx = x - X, dz = z - Z, s = Math.Sin(Heading), c = Math.Cos(Heading);
                return new Vec3d(dx * c - dz * s, y, dx * s + dz * c);
            }

            Vec3d Turned(double x, double z)
            {
                double s = Math.Sin(Heading), c = Math.Cos(Heading);
                return new Vec3d(x * c - z * s, 0, x * s + z * c);
            }

            public double Distance => Math.Sqrt((PlayerX - X) * (PlayerX - X) + (PlayerZ - Z) * (PlayerZ - Z));
            public double FromHome => Math.Sqrt((HomeX - X) * (HomeX - X) + (HomeZ - Z) * (HomeZ - Z));

            public AmbusherOrders Step(double dt = Dt)
            {
                Before?.Invoke(this);
                bool held = Last.Holding;
                var senses = new AmbusherSenses
                {
                    Hostile = Hostile,
                    Player = Local(PlayerX, PlayerZ, PlayerY),
                    PlayerVelocity = held ? Vec3d.Zero : Turned(PlayerVX, PlayerVZ),
                    Noise = Noise,
                    ClearLine = ClearLine,
                    ClearAbove = ClearAbove,
                    Home = Local(HomeX, HomeZ),
                };
                AmbusherOrders orders = Brain.Think(Body, senses, dt);
                Heading += orders.TurnRate * dt;
                double s = Math.Sin(Heading), c = Math.Cos(Heading);
                double mx = (orders.Velocity.X * c + orders.Velocity.Z * s) * dt, mz = (-orders.Velocity.X * s + orders.Velocity.Z * c) * dt;
                X += mx;
                Z += mz;
                double moved = Math.Sqrt(mx * mx + mz * mz);
                FastestMove = Math.Max(FastestMove, moved / dt);
                if (orders.Mode != AmbusherMode.Tunnel) MovedOutOfTunnel += moved;
                if (orders.Holding)
                {
                    double px = (orders.Pull.X * c + orders.Pull.Z * s) * dt, pz = (-orders.Pull.X * s + orders.Pull.Z * c) * dt;
                    PlayerX += px;
                    PlayerZ += pz;
                    Dragged += Math.Sqrt(px * px + pz * pz);
                }
                else
                {
                    PlayerX += PlayerVX * dt;
                    PlayerZ += PlayerVZ * dt;
                }
                if (orders.Bite > 0.0)
                {
                    Hurt += orders.Bite;
                    Bites++;
                }
                var input = new AmbusherAnimInput { Mode = orders.Mode, T = orders.T, Out = orders.Out, Feels = true, Player = Local(PlayerX, PlayerZ), Slack = orders.Slack };
                Animator.Update(Body, input, dt, Pose);
                if (orders.Mode != Modes[Modes.Count - 1]) Modes.Add(orders.Mode);
                Time += dt;
                Last = orders;
                return orders;
            }

            public bool RunUntil(Func<World, bool> done, double seconds)
            {
                for (double t = 0; t < seconds; t += Dt)
                {
                    Step();
                    if (done(this)) return true;
                }
                return false;
            }

            public void Run(double seconds)
            {
                for (double t = 0; t < seconds; t += Dt) Step();
            }

            public void ShootOffJaw(int jaw)
            {
                AmbusherHit hit = Body.HitJaw(jaw, 1000);
                Assert.AreEqual(jaw, hit.JawLost);
                Brain.Flinch(Body);
            }
        }

        // ------------------------------------------------------------------ what comes off, what kills

        [Test]
        public void AFeelerComesOffInOneRifleHit_AJawInThree()
        {
            var body = new AmbusherBody();
            Assert.AreEqual(2, body.HitFeeler(2, Rifle).FeelerLost);
            Assert.AreEqual(4, body.FeelerCount);
            Assert.AreEqual(-1, body.HitJaw(0, Rifle).JawLost);
            Assert.AreEqual(-1, body.HitJaw(0, Rifle).JawLost);
            Assert.AreEqual(0, body.HitJaw(0, Rifle).JawLost);
            Assert.AreEqual(1, body.JawCount);
            Assert.IsTrue(body.Alive, "nothing that comes off kills it");
        }

        [Test]
        public void ShotThroughAnywhereAlongItsBody_ItIsDead_AndTheDamageIsRemembered()
        {
            var body = new AmbusherBody();
            for (int i = 0; i < 2; i++) Assert.IsFalse(body.HitSeg(6, Rifle).Killed);
            // Hits on another length do not count toward this one.
            Assert.IsFalse(body.HitSeg(7, Rifle).Killed);
            Assert.IsFalse(body.HitSeg(6, Rifle).Killed);
            AmbusherHit hit = body.HitSeg(6, Rifle);
            Assert.IsTrue(hit.Killed, "four rifle hits on one length");
            Assert.AreEqual(6, hit.CutAt);
            Assert.AreEqual(6, body.CutAt);
            Assert.IsFalse(body.Alive);
        }

        [Test]
        public void TheArmouredHead_IsTheSlowWay()
        {
            var body = new AmbusherBody();
            int hits = 0;
            while (body.Alive && hits < 100)
            {
                body.HitHead(Rifle);
                hits++;
            }
            Assert.AreEqual(9, hits);
            Assert.Greater(AmbusherHp.Default.Head, AmbusherHp.Default.Seg * 2, "the armour is much the tougher");
        }

        [Test]
        public void ItHoldsWithTwoJaws_BitesWithOne_AndIsHarmlessWithNone()
        {
            var body = new AmbusherBody();
            Assert.IsTrue(body.CanHold);
            Assert.AreEqual(1.0, body.BiteShare);
            body.HitJaw(1, 1000);
            Assert.IsFalse(body.CanHold);
            Assert.IsTrue(body.CanBite);
            Assert.AreEqual(0.5, body.BiteShare);
            body.HitJaw(0, 1000);
            Assert.IsFalse(body.CanBite);
            Assert.AreEqual(0.0, body.BiteShare);
            Assert.IsTrue(body.Alive);
        }

        [Test]
        public void EveryFeelerLost_ShortensHowFarItFeels()
        {
            var body = new AmbusherBody();
            var rules = new AmbusherRules();
            double last = AmbusherBrain.FeelRange(rules, body, 1.0);
            Assert.AreEqual(rules.feelRange, last, 1e-6);
            for (int i = 0; i < AmbusherLayout.Feelers; i++)
            {
                body.HitFeeler(i, 1000);
                double now = AmbusherBrain.FeelRange(rules, body, 1.0);
                Assert.Less(now, last, "every one counts");
                last = now;
            }
            Assert.AreEqual(rules.feelRange * 0.4, last, 1e-6);
            Assert.AreEqual(rules.closeRange, AmbusherBrain.FeelRange(rules, body, 0.0), 1e-6, "right on top of it, even silence is felt");
        }

        // ------------------------------------------------------------------ the wait and the warning

        [Test]
        public void WithNobodyNear_ItWaitsAtTheMouthOfItsHole()
        {
            var w = new World { PlayerZ = 60.0 };
            w.Run(15.0);
            Assert.AreEqual(1, w.Modes.Count, "it never left its wait");
            Assert.AreEqual(0.0, w.MovedOutOfTunnel, 1e-9);
            // Its head stands in the hole looking up, its jaws lie open, the rest of it is down the burrow.
            Assert.AreEqual(0.10, w.Pose.NodePosition[0].Y, 0.03);
            Assert.AreEqual(0.0, w.Pose.NodePosition[0].Z, 0.03);
            Assert.Greater(w.Pose.Gape, 0.8);
            for (int i = 1; i < AmbusherLayout.Nodes; i++) Assert.Less(w.Pose.NodePosition[i].Y, 0.0, "length " + i + " is under the ground");
            Assert.AreEqual(1.0, w.Pose.Mound, 0.01);
        }

        [Test]
        public void SomeoneWhoStandsWithinReach_GetsAWarning_ThenIsSeizedAndHurt()
        {
            var w = new World();
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == AmbusherMode.Coil, 2.0), "it felt them");
            double began = w.Time;
            Assert.AreEqual(0.0, w.Hurt);
            Assert.IsTrue(w.RunUntil(x => x.Last.Landed, 2.0));
            Assert.AreEqual(w.Rules.coilSeconds + w.Rules.strikeSeconds, w.Time - began, 0.06, "the whole warning, then the strike");
            Assert.GreaterOrEqual(w.Rules.coilSeconds, 0.5, "a tell of at least half a second");
            Assert.AreEqual(AmbusherMode.Hold, w.Last.Mode);
            Assert.AreEqual(w.Rules.biteDamage, w.Hurt, 1e-6);
        }

        [Test]
        public void ItSettlesBeforeItStrikes_SoComingUpIsNeverAStrikeAtOnce()
        {
            var w = new World();
            w.Step();
            Assert.AreEqual(AmbusherMode.Lurk, w.Last.Mode);
            w.Run(AmbusherBrain.Settle - 0.1);
            Assert.AreEqual(AmbusherMode.Lurk, w.Last.Mode);
        }

        [Test]
        public void AStepAside_AfterItsJawsFlyWide_IsAMiss()
        {
            var w = new World();
            w.Before = x =>
            {
                // They stand until it has taken aim, then walk off to the side.
                bool aimed = x.Last.Mode == AmbusherMode.Coil && x.Last.T >= AmbusherBrain.LockAt + 0.02 || x.Last.Mode == AmbusherMode.Strike;
                if (aimed) x.PlayerVX = 5.0;
            };
            Assert.IsTrue(w.RunUntil(x => x.Last.Landed, 4.0));
            Assert.AreEqual(AmbusherMode.Spent, w.Last.Mode, "it missed");
            Assert.AreEqual(0.0, w.Hurt);
        }

        [Test]
        public void SomeoneWhoWalksStraightOn_IsCaught_ItAimsWhereTheyAreGoing()
        {
            // Walking across in front of it at a steady pace, never changing direction.
            var w = new World { PlayerX = -6.0, PlayerZ = 3.0, PlayerVX = 4.0 };
            Assert.IsTrue(w.RunUntil(x => x.Last.Landed, 6.0));
            Assert.AreEqual(AmbusherMode.Hold, w.Last.Mode, "keeping on as you were is not a dodge");
        }

        [Test]
        public void SomeoneWalking_WhoTurnsBackAfterItsJawsFlyWide_IsMissed()
        {
            var w = new World { PlayerX = -6.0, PlayerZ = 3.0, PlayerVX = 4.0 };
            w.Before = x =>
            {
                if (x.Last.Mode == AmbusherMode.Coil && x.Last.T >= AmbusherBrain.LockAt + 0.02) x.PlayerVX = -5.0;
            };
            Assert.IsTrue(w.RunUntil(x => x.Last.Landed, 6.0));
            Assert.AreEqual(AmbusherMode.Spent, w.Last.Mode);
            Assert.AreEqual(0.0, w.Hurt);
        }

        [Test]
        public void OnceAimed_ItTurnsToThatOnePlaceAndNoFurther()
        {
            var w = new World { PlayerX = 2.0, PlayerZ = 3.0 };
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == AmbusherMode.Coil && x.Last.T >= AmbusherBrain.LockAt + 0.05, 3.0));
            // The player runs round it; its heading must end where it aimed, not follow them.
            double aimed = Math.Atan2(2.0, 3.0);
            w.PlayerVX = -8.0;
            Assert.IsTrue(w.RunUntil(x => x.Last.Landed, 2.0));
            Assert.AreEqual(aimed, w.Heading, 0.06);
        }

        [Test]
        public void TheMomentItTakesAim_CanBeSeen()
        {
            double before = AmbusherBrain.LockAt - 0.02, after = AmbusherBrain.LockAt + 0.13;
            Assert.LessOrEqual(AmbusherAnimator.GapeOf(AmbusherMode.Coil, before), 0.55, "its jaws half close as it rises");
            Assert.GreaterOrEqual(AmbusherAnimator.GapeOf(AmbusherMode.Coil, after), 0.99, "and fly wide when it has aimed");
            Assert.Greater(AmbusherAnimator.CoilHeightOf(after) - AmbusherAnimator.CoilHeightOf(before), 0.15, "and it rears to full height");
            Assert.Greater(AmbusherAnimator.CoilHeightOf(before), 0.7, "the warning itself stands well out of the ground");
        }

        [Test]
        public void ItCannotReachSomeoneOnAWallOrBehindOne_OrFeelSomeoneSilent()
        {
            var high = new World { PlayerY = 1.4 };
            high.Run(3.0);
            Assert.AreEqual(AmbusherMode.Lurk, high.Last.Mode, "someone standing on something is out of its reach");

            var walled = new World { ClearLine = false };
            walled.Run(3.0);
            Assert.AreEqual(AmbusherMode.Lurk, walled.Last.Mode);

            var silent = new World { Noise = 0.0, PlayerZ = 3.0 };
            silent.Run(3.0);
            Assert.AreEqual(AmbusherMode.Lurk, silent.Last.Mode, "it feels footsteps, and there are none");
            silent.PlayerZ = silent.Rules.closeRange - 0.2;
            Assert.IsTrue(silent.RunUntil(x => x.Last.Mode == AmbusherMode.Coil, 2.0), "but right on top of it anyone is felt");
        }

        [Test]
        public void WithNoFeelers_ItOnlyFeelsSomeoneMuchNearer()
        {
            var w = new World { PlayerZ = 4.1 };
            for (int i = 0; i < AmbusherLayout.Feelers; i++) w.Body.HitFeeler(i, 1000);
            w.Run(3.0);
            Assert.AreEqual(AmbusherMode.Lurk, w.Last.Mode);
            var whole = new World { PlayerZ = 4.1 };
            Assert.IsTrue(whole.RunUntil(x => x.Last.Mode == AmbusherMode.Coil, 2.0));
        }

        // ------------------------------------------------------------------ the hold

        [Test]
        public void HeldSomeone_IsDraggedToItsHole_HurtInBeats_AndLetGoInTheEnd()
        {
            var w = new World { PlayerZ = 4.0 };
            Assert.IsTrue(w.RunUntil(x => x.Last.Holding, 4.0));
            double began = w.Time;
            Assert.IsTrue(w.RunUntil(x => !x.Last.Holding && x.Last.Mode != AmbusherMode.Hold, 10.0), "it lets go by itself");
            Assert.AreEqual(w.Rules.holdSeconds, w.Time - began, 0.1);
            Assert.AreEqual(w.Rules.holdAt, w.Distance, 0.1, "hauled to the mouth of its hole and no nearer");
            Assert.AreEqual(4.0 - w.Rules.holdAt, w.Dragged, 0.15);
            double most = w.Rules.biteDamage + w.Rules.holdDamage * w.Rules.holdSeconds;
            Assert.LessOrEqual(w.Hurt, most + 1e-6);
            Assert.Greater(w.Hurt, most - w.Rules.holdDamage * w.Rules.holdBeat - 1e-6);
            Assert.Less(most, 60.0, "a whole hold never kills from full health");
            Assert.AreEqual(AmbusherMode.Withdraw, w.Last.Mode);
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == AmbusherMode.Under, 2.0));
        }

        [Test]
        public void ItsHeadStaysOnWhoeverItHolds()
        {
            var w = new World { PlayerZ = 4.0 };
            Assert.IsTrue(w.RunUntil(x => x.Last.Holding, 4.0));
            for (int i = 0; i < 120; i++)
            {
                w.Step();
                if (!w.Last.Holding) break;
                if (i < 12) continue;
                Vec3d player = w.Local(w.PlayerX, w.PlayerZ);
                Assert.AreEqual(player.Z - AmbusherBrain.GripGap, w.Pose.NodePosition[0].Z, 0.15, "step " + i);
                Assert.AreEqual(AmbusherAnimator.GripHeight, w.Pose.NodePosition[0].Y, 0.08);
                Assert.Less(Math.Abs(w.Pose.NodePosition[0].X), 0.12);
            }
        }

        [Test]
        public void ShootingAJawOff_MakesItLetGo()
        {
            var w = new World { PlayerZ = 4.0 };
            Assert.IsTrue(w.RunUntil(x => x.Last.Holding, 4.0));
            w.Run(0.4);
            double hurt = w.Hurt;
            w.ShootOffJaw(0);
            w.Step();
            Assert.IsFalse(w.Last.Holding);
            Assert.AreEqual(AmbusherMode.Withdraw, w.Last.Mode);
            w.Run(1.0);
            Assert.AreEqual(hurt, w.Hurt, 1e-6, "and nothing more is done to them");
        }

        [Test]
        public void EnoughDamageWhileItHolds_MakesItLetGo()
        {
            var w = new World { PlayerZ = 4.0 };
            Assert.IsTrue(w.RunUntil(x => x.Last.Holding, 4.0));
            w.Brain.Hurt(w.Rules.letGoDamage - 1, w.Body);
            w.Step();
            Assert.IsTrue(w.Last.Holding);
            w.Brain.Hurt(2, w.Body);
            w.Step();
            Assert.IsFalse(w.Last.Holding);
            Assert.Less(w.Rules.letGoDamage, w.Rules.hp.Seg, "it lets go for less than it takes to shoot it through");
        }

        [Test]
        public void AJawShotOffDuringTheWarning_CallsTheStrikeOff()
        {
            var w = new World();
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == AmbusherMode.Coil, 2.0));
            w.Run(0.2);
            w.ShootOffJaw(1);
            w.Run(1.5);
            Assert.IsFalse(w.Modes.Contains(AmbusherMode.Strike), "the strike never came");
            Assert.AreEqual(0.0, w.Hurt);
        }

        [Test]
        public void WithOneJaw_ItBitesAtHalfStrength_AndCannotHold()
        {
            var w = new World();
            w.Body.HitJaw(0, 1000);
            Assert.IsTrue(w.RunUntil(x => x.Last.Landed, 4.0));
            Assert.AreEqual(w.Rules.biteDamage * 0.5, w.Hurt, 1e-6);
            Assert.IsFalse(w.Last.Holding);
            w.Run(1.0);
            Assert.IsFalse(w.Modes.Contains(AmbusherMode.Hold));
            Assert.AreEqual(0.0, w.Dragged);
        }

        [Test]
        public void WithNoJaws_ItIsHarmless_ButStaysWhereItCanBeFinished()
        {
            var w = new World();
            w.Run(0.2);
            w.Body.HitJaw(0, 1000);
            w.ShootOffJaw(1);
            w.Step();
            // Its last jaw gone, it is thrown out along the ground and lies thrashing.
            Assert.AreEqual(AmbusherMode.Spent, w.Last.Mode);
            Assert.GreaterOrEqual(w.Last.Out, 2.5);
            w.Run(w.Rules.maimedSeconds - 0.2);
            Assert.AreEqual(AmbusherMode.Spent, w.Last.Mode, "long enough to finish it");
            w.Run(30.0);
            Assert.AreEqual(AmbusherMode.Lurk, w.Last.Mode, "then it can only wait at the mouth of its hole");
            Assert.IsFalse(w.Brain.Buried, "never out of reach for good");
            Assert.AreEqual(0.0, w.Hurt);
            Assert.AreEqual(0.0, w.FastestMove, 1e-9, "it does not follow");
            Assert.IsFalse(w.Modes.Contains(AmbusherMode.Coil));
            // Shot there, it does not duck away.
            w.Brain.Hurt(Rifle, w.Body);
            w.Step();
            Assert.AreEqual(AmbusherMode.Lurk, w.Last.Mode);
        }

        [Test]
        public void BackingOutOfItsReachDuringTheWarning_CallsItOff_ItDoesNotThrowItselfAtNothing()
        {
            var w = new World { PlayerZ = 4.2 };
            w.Before = x =>
            {
                if (x.Last.Mode == AmbusherMode.Coil) x.PlayerVZ = 5.0;
            };
            w.Run(3.0);
            Assert.IsFalse(w.Modes.Contains(AmbusherMode.Strike));
            Assert.IsFalse(w.Modes.Contains(AmbusherMode.Spent), "no free kill for walking backwards");
        }

        [Test]
        public void HurtWhileItHolds_ItsJawsSlacken_BeforeItLetsGo()
        {
            var w = new World { PlayerZ = 4.0 };
            Assert.IsTrue(w.RunUntil(x => x.Last.Holding, 4.0));
            w.Run(0.3);
            double tight = w.Pose.Gape;
            w.Brain.Hurt(w.Rules.letGoDamage * 0.6, w.Body);
            w.Run(0.3);
            Assert.IsTrue(w.Last.Holding);
            Assert.Greater(w.Pose.Gape, tight + 0.15);
        }

        [Test]
        public void ShotAsItComesUp_ItDucksBackUnder()
        {
            var w = new World { PlayerZ = 15.0 };
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == AmbusherMode.Rise, 20.0));
            w.Run(0.6);
            w.Brain.Hurt(Rifle, w.Body);
            w.Step();
            Assert.AreEqual(AmbusherMode.Withdraw, w.Last.Mode, "its jaws cannot be picked off while it rises");
        }

        // ------------------------------------------------------------------ a miss

        [Test]
        public void AMiss_LeavesItStretchedOutOnTheGround_ThenItPullsBackUnder()
        {
            var w = new World();
            w.Before = x =>
            {
                if (x.Last.Mode == AmbusherMode.Coil && x.Last.T >= AmbusherBrain.LockAt + 0.02) x.PlayerVX = 5.0;
            };
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == AmbusherMode.Spent, 4.0));
            double began = w.Time;
            w.Run(0.5);
            int outOfTheHole = 0;
            for (int i = 0; i < AmbusherLayout.Nodes; i++)
            {
                if (w.Pose.NodePosition[i].Y <= 0.05) continue;
                outOfTheHole++;
                Assert.Less(w.Pose.NodePosition[i].Y, 0.4, "it lies on the ground");
            }
            Assert.GreaterOrEqual(outOfTheHole, 7, "lengths of it lie out in the open to be shot through");
            Assert.Greater(w.Pose.OutLength, 2.0);
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == AmbusherMode.Withdraw, 2.0));
            Assert.AreEqual(w.Rules.spentSeconds, w.Time - began, 0.05);
            Assert.GreaterOrEqual(w.Rules.spentSeconds, 0.8, "long enough to use");
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == AmbusherMode.Under, 2.0));
            w.Run(0.5);
            for (int i = 0; i < AmbusherLayout.Nodes; i++) Assert.Less(w.Pose.NodePosition[i].Y, -0.3, "all of it is under");
            Assert.AreEqual(0.0, w.Hurt);
        }

        [Test]
        public void ItsBodyIsOneLine_EveryPieceAtItsOwnDistanceBehindTheHead()
        {
            var w = new World { PlayerZ = 4.0 };
            bool struck = false;
            for (int step = 0; step < 60 * 9; step++)
            {
                w.Step();
                struck |= w.Last.Landed;
                // In the few frames of the strike itself the line is redrawn faster than it can be checked.
                if (w.Last.Mode == AmbusherMode.Strike) continue;
                for (int i = 0; i < AmbusherLayout.Nodes; i++)
                {
                    Vec3d p = w.Pose.NodePosition[i];
                    QuatD r = w.Pose.NodeRotation[i];
                    Assert.IsFalse(double.IsNaN(p.X + p.Y + p.Z + r.X + r.Y + r.Z + r.W), "step " + step + " node " + i);
                    if (i == 0) continue;
                    double gap = (p - w.Pose.NodePosition[i - 1]).Magnitude;
                    Assert.Less(gap, AmbusherLayout.Spacing * 1.25, "step " + step + " node " + i + " in " + w.Last.Mode);
                    Assert.Greater(gap, AmbusherLayout.Spacing * 0.55, "step " + step + " node " + i + " in " + w.Last.Mode);
                }
            }
            Assert.IsTrue(struck);
        }

        [Test]
        public void ItsJawsOpenTheSameEitherSide()
        {
            var w = new World { PlayerZ = 60.0 };
            w.Run(1.0);
            Vec3d Tip(int side) => w.Pose.JawPosition[side] + w.Pose.JawRotation[side].Rotate(new Vec3d(0, 0, AmbusherLayout.JawLength));
            Vec3d right = Tip(AmbusherLayout.RightSide), left = Tip(AmbusherLayout.LeftSide);
            Assert.AreEqual(right.X, -left.X, 0.01);
            Assert.AreEqual(right.Y, left.Y, 0.01);
            Assert.AreEqual(right.Z, left.Z, 0.01);
            Assert.Greater(right.X, 0.6, "lying wide open either side of the hole");
            // The left jaw is the right one rolled half a turn: its own upper side faces down.
            Vec3d upRight = w.Pose.JawRotation[AmbusherLayout.RightSide].Rotate(Vec3d.Up), upLeft = w.Pose.JawRotation[AmbusherLayout.LeftSide].Rotate(Vec3d.Up);
            Assert.Less(Vec3d.Dot(upRight, upLeft), -0.9);
        }

        // ------------------------------------------------------------------ under the ground

        [Test]
        public void LeftFeelingSomeoneItCannotReach_ItTunnelsAndComesUpShortOfThem()
        {
            var w = new World { PlayerZ = 15.0 };
            w.Run(w.Rules.restSeconds - 0.3);
            Assert.AreEqual(AmbusherMode.Lurk, w.Last.Mode, "it waits a while first");
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == AmbusherMode.Tunnel, 4.0));
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == AmbusherMode.Rise, 10.0));
            Assert.AreEqual(w.Rules.ambushOff, w.Distance, 0.6, "it comes up short of someone standing still, never under them");
            Assert.LessOrEqual(w.FastestMove, w.Rules.tunnelPace + 1e-6);
            Assert.Less(w.Rules.tunnelPace, 5.0, "slower than the player walks");
            Assert.AreEqual(0.0, w.MovedOutOfTunnel, 1e-9, "it only ever moves under the ground");
            // Coming up is slow, and then it must settle: nobody is struck by ground that gave no sign.
            double began = w.Time;
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == AmbusherMode.Coil, 4.0));
            Assert.GreaterOrEqual(w.Time - began, w.Rules.riseSeconds + AmbusherBrain.Settle - 0.05);
        }

        [Test]
        public void UnderTheGround_NothingOfItShows_ButTheMound()
        {
            var w = new World { PlayerZ = 15.0 };
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == AmbusherMode.Tunnel, 8.0));
            w.Run(0.5);
            Assert.IsTrue(w.Brain.Buried);
            for (int i = 0; i < AmbusherLayout.Nodes; i++) Assert.Less(w.Pose.NodePosition[i].Y, -0.3);
            Assert.Greater(w.Pose.Mound, 0.3, "a low mound shows where it is");
            Assert.Less(w.Pose.Mound, 0.9);
        }

        [Test]
        public void ItComesUpAheadOfSomeoneOnTheMove()
        {
            // Someone walking steadily past, out of its reach.
            var w = new World { PlayerX = -20.0, PlayerZ = 8.0, PlayerVX = 2.5 };
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == AmbusherMode.Rise, 20.0));
            // It is in front of them on their line, not behind.
            Assert.Greater(w.X, w.PlayerX, "ahead of them");
            Assert.AreEqual(8.0, w.Z, 0.8, "on the line they are walking");
        }

        [Test]
        public void ItOnlyComesUpWhereTheGroundIsOpen()
        {
            var w = new World { PlayerZ = 15.0, ClearAbove = false };
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == AmbusherMode.Tunnel, 8.0));
            w.Run(8.0);
            Assert.IsFalse(w.Modes.Contains(AmbusherMode.Rise), "something stands over it everywhere it tries");
            w.ClearAbove = true;
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == AmbusherMode.Rise, 6.0));
        }

        [Test]
        public void ShotAtWhileItWaits_ItDucksUnder_AndGoesAfterWhoeverDidIt()
        {
            var w = new World { PlayerZ = 18.0 };
            w.Run(0.5);
            w.Brain.Hurt(Rifle, w.Body);
            w.Step();
            Assert.AreEqual(AmbusherMode.Withdraw, w.Last.Mode);
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == AmbusherMode.Tunnel, 1.5), "sooner than it would have gone by itself");
        }

        [Test]
        public void ItNeverGoesFurtherFromHomeThanItRoams_AndDoesNotFollowSomeoneTooFarOffToFeel()
        {
            var w = new World { PlayerZ = 22.0 };
            w.Rules.roamRadius = 6f;
            w.Run(30.0);
            Assert.LessOrEqual(w.FromHome, 6.05);
            Assert.Greater(w.FromHome, 5.0, "it went as far as it may");

            var far = new World { PlayerZ = 40.0 };
            far.Run(20.0);
            Assert.AreEqual(0.0, far.FastestMove, 1e-9);
        }

        [Test]
        public void AfterAMiss_WithThemStillInReach_ItComesUpWhereItIs()
        {
            var w = new World { PlayerZ = 2.5 };
            w.Before = x =>
            {
                if (x.Last.Mode == AmbusherMode.Coil && x.Last.T >= AmbusherBrain.LockAt + 0.02 && x.Modes.Count < 4) x.PlayerVX = 5.0;
                if (x.Last.Mode == AmbusherMode.Spent) x.PlayerVX = 0.0;
            };
            Assert.IsTrue(w.RunUntil(x => x.Last.Mode == AmbusherMode.Rise, 8.0));
            Assert.AreEqual(0.0, w.FastestMove, 1e-9, "it did not tunnel");
            Assert.IsFalse(w.Modes.Contains(AmbusherMode.Tunnel));
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
    }
}
