using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;
using OrbitalAnte.Look;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>The Blind stalker (the olm): what comes off and what kills it, what it can hear and feel, going where the sound was, the warning and the lunge, and how it moves.</summary>
    public class StalkerRulesTests
    {
        const double Dt = 1.0 / 60.0;

        /// <summary>A flat world: the Stalker's head and heading, a player who is as loud as the test says, and the senses that follow.</summary>
        sealed class World
        {
            public readonly StalkerRules Rules = new StalkerRules();
            public readonly StalkerBody Body;
            public readonly StalkerBrain Brain;
            public readonly StalkerAnimator Animator = new StalkerAnimator();
            public readonly StalkerPose Pose = new StalkerPose();
            public Vec3d Root = Vec3d.Zero, Player = new Vec3d(0, 0, 10), PlayerVelocity = Vec3d.Zero, Home = Vec3d.Zero;
            public double Yaw, Noise, Time, Hurt, FastestForward, LeastForward = 0.0, Sideways, Travelled, FurthestFromHome;
            public bool Hostile = true, Blocked;
            public int Bites;
            public StalkerOrders Last;
            public readonly List<StalkerMode> Modes = new List<StalkerMode> { StalkerMode.Still };
            public Action<World> Before;

            public World()
            {
                Body = new StalkerBody(Rules.hp);
                Brain = new StalkerBrain(Rules);
            }

            public double Distance => (Player - Root).Magnitude;

            public StalkerSenses Senses()
            {
                QuatD back = QuatD.AngleAxis(Vec3d.Up, -Yaw);
                return new StalkerSenses
                {
                    Hostile = Hostile, Player = back.Rotate(Player - Root), Noise = Noise, Home = back.Rotate(Home - Root), North = back.Rotate(Vec3d.Forward), Blocked = Blocked,
                };
            }

            public StalkerOrders Step()
            {
                Before?.Invoke(this);
                Player += PlayerVelocity * Dt;
                StalkerOrders orders = Brain.Think(Body, Senses(), Dt);
                Yaw += orders.TurnRate * Dt;
                Vec3d moved = QuatD.AngleAxis(Vec3d.Up, Yaw).Rotate(orders.Velocity) * Dt;
                Root += moved;
                Travelled += moved.Magnitude;
                FastestForward = Math.Max(FastestForward, orders.Velocity.Z);
                LeastForward = Math.Min(LeastForward, orders.Velocity.Z);
                Sideways = Math.Max(Sideways, Math.Abs(orders.Velocity.X));
                FurthestFromHome = Math.Max(FurthestFromHome, (Root - Home).Magnitude);
                if (orders.Bite > 0.0)
                {
                    Hurt += orders.Bite;
                    Bites++;
                }
                Animator.Update(Body, new StalkerAnimInput { Velocity = orders.Velocity, TurnRate = orders.TurnRate, Mode = orders.Mode, T = orders.T, HasPlace = orders.HasPlace, Place = orders.Place, Aware = orders.Aware, Took = orders.Took }, Dt, Pose);
                if (orders.Mode != Modes[Modes.Count - 1]) Modes.Add(orders.Mode);
                Time += Dt;
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
        }

        // ------------------------------------------------------------------ what comes off, what kills

        [Test]
        public void AGill_ComesOff_AndItHearsLessFar()
        {
            var body = new StalkerBody();
            var rules = new StalkerRules();
            double both = StalkerBrain.HearRange(rules, body, 1.0);
            Assert.AreEqual(-1, body.HitGill(0, 20).GillLost, "one rifle hit is not enough");
            Assert.AreEqual(0, body.HitGill(0, 20).GillLost);
            Assert.AreEqual(1, body.GillCount);
            double one = StalkerBrain.HearRange(rules, body, 1.0);
            Assert.Less(one, both);
            body.HitGill(1, 0, sever: true);
            Assert.Less(StalkerBrain.HearRange(rules, body, 1.0), one);
            Assert.Greater(StalkerBrain.HearRange(rules, body, 1.0), 0.0, "it still hears a little through its skull");
            Assert.IsTrue(body.Alive, "gills are not what kills it");
        }

        [Test]
        public void Legs_ComeOff_ItSlows_AndOnFewerThanTwoItSwims()
        {
            var body = new StalkerBody();
            double last = body.PaceShare;
            for (int leg = 0; leg < StalkerLayout.Legs; leg++)
            {
                Assert.AreEqual(leg, body.HitLeg(leg, 0, sever: true).LegLost);
                Assert.LessOrEqual(body.PaceShare, last);
                last = body.PaceShare;
                Assert.AreEqual(StalkerLayout.Legs - leg - 1 >= 2, body.Walks);
            }
            Assert.Greater(body.PaceShare, 0.3, "it is never left unable to move");
            Assert.IsTrue(body.Alive);
        }

        [Test]
        public void TheTail_ComesOffWhereItIsShotThrough_AndTheLungeIsShorter()
        {
            var body = new StalkerBody();
            Assert.AreEqual(1.0, body.LungeShare, 1e-9);
            int seg = StalkerLayout.Trunk + 2;
            Assert.AreEqual(-1, body.HitSeg(seg, 30).TailCutAt);
            Assert.AreEqual(seg, body.HitSeg(seg, 30).TailCutAt);
            Assert.AreEqual(seg, body.Kept, "everything from the cut back is gone");
            Assert.IsFalse(body.SegOn(seg + 1));
            Assert.AreEqual(2, body.TailKept);
            Assert.Less(body.LungeShare, 1.0);
            body.HitSeg(StalkerLayout.Trunk, 0, sever: true);
            Assert.AreEqual(0, body.TailKept);
            Assert.Greater(body.LungeShare, 0.5, "with no tail it still lunges, less far");
            Assert.IsTrue(body.Alive, "losing its tail does not kill it");
        }

        [Test]
        public void ItDies_ThroughTheTrunk_OrTheSkull_AndTheSkullIsTougher()
        {
            var body = new StalkerBody();
            double total = 0;
            // Hits anywhere on the trunk are one number.
            for (int k = 0; body.Alive; k++)
            {
                body.HitSeg(k % StalkerLayout.Trunk, 24);
                total += 24;
                Assert.Less(total, 1000);
            }
            Assert.AreEqual(Math.Ceiling(StalkerHp.Default.Trunk / 24) * 24, total, 1e-9);
            var other = new StalkerBody();
            Assert.Greater(StalkerHp.Default.Head, StalkerHp.Default.Trunk);
            Assert.IsFalse(other.HitHead(StalkerHp.Default.Head - 1).Killed);
            Assert.IsTrue(other.HitHead(1).Killed);
            Assert.AreEqual(-1, other.HitGill(0, 1000).GillLost, "nothing more comes off the dead");
        }

        // ------------------------------------------------------------------ hearing and feeling

        [Test]
        public void SomeoneSilent_OutsideItsSnoutsReach_IsNeverFound()
        {
            var w = new World { Noise = 0.0, Player = new Vec3d(1, 0, 3.5) };
            w.Run(30);
            Assert.AreEqual(StalkerMode.Still, w.Brain.Mode);
            Assert.AreEqual(0.0, w.Travelled, 1e-9, "it has not moved at all");
            Assert.AreEqual(0, w.Bites);
        }

        [Test]
        public void SomeoneWalking_IsHeard_FurtherTheLouderTheyAre()
        {
            var rules = new StalkerRules();
            var body = new StalkerBody();
            Assert.AreEqual(rules.hearRange, StalkerBrain.HearRange(rules, body, 1.0), 1e-6);
            Assert.Less(StalkerBrain.HearRange(rules, body, 0.4), StalkerBrain.HearRange(rules, body, 1.0), "sneaking is heard from nearer");
            Assert.Greater(StalkerBrain.HearRange(rules, body, 2.0), StalkerBrain.HearRange(rules, body, 1.0), "a shot from further");

            var near = new World { Noise = 1.0, Player = new Vec3d(0, 0, rules.hearRange - 1) };
            near.Run(0.2);
            Assert.AreEqual(StalkerMode.Listen, near.Brain.Mode, "heard: first it listens");
            var far = new World { Noise = 1.0, Player = new Vec3d(0, 0, rules.hearRange + 1) };
            far.Run(3);
            Assert.AreEqual(StalkerMode.Still, far.Brain.Mode);
        }

        [Test]
        public void ItListensBeforeItMoves_ThenWalks()
        {
            var w = new World { Noise = 1.0, Player = new Vec3d(6, 0, 10) };
            Assert.IsTrue(w.RunUntil(x => x.Brain.Mode == StalkerMode.Stalk, 3));
            Assert.AreEqual(w.Rules.listenSeconds, w.Time, 0.1, "the listening beat is its whole length");
            Assert.AreEqual(0.0, w.Travelled, 1e-9, "it does not move while it listens");
            CollectionAssert.AreEqual(new[] { StalkerMode.Still, StalkerMode.Listen, StalkerMode.Stalk }, w.Modes);
        }

        [Test]
        public void CloseUp_ItFeelsThem_HoweverStillAndQuiet()
        {
            var w = new World { Noise = 0.0 };
            w.Player = new Vec3d(0.5, 0, w.Rules.senseRange - 0.4);
            Assert.IsTrue(w.RunUntil(x => x.Bites > 0, 6), "standing silent under its nose is not safe");
        }

        [Test]
        public void ItGoesWhereTheSoundWas_AndSomeoneWhoSlipsAwayQuietlyIsLost()
        {
            var w = new World { Noise = 1.0, Player = new Vec3d(0, 0, 12) };
            w.Run(1.5);
            Assert.AreEqual(StalkerMode.Stalk, w.Brain.Mode);
            // They go quiet and step well off to the side.
            Vec3d heardAt = w.Player;
            w.Noise = 0.0;
            w.Player = new Vec3d(8, 0, 12);
            Assert.IsTrue(w.RunUntil(x => x.Brain.Mode == StalkerMode.Search, 12), "it arrives and finds nothing");
            Assert.Less((w.Root - heardAt).Magnitude, 1.6, "it went to where the sound was, not to where they are");
            Assert.IsTrue(w.RunUntil(x => x.Brain.Mode == StalkerMode.Still, w.Rules.searchSeconds + 1), "and gives up");
            Assert.AreEqual(0, w.Bites);
        }

        [Test]
        public void BetweenTimes_ItIsGoingToAnOlderPlace()
        {
            // Someone walking across its front: the place it has in mind is never newer than hearEvery.
            var w = new World { Noise = 1.0, Player = new Vec3d(-8, 0, 12), PlayerVelocity = new Vec3d(4, 0, 0) };
            w.Run(1.2);
            double worst = 0.0;
            for (int i = 0; i < 120; i++)
            {
                StalkerOrders o = w.Step();
                Vec3d place = w.Root + QuatD.AngleAxis(Vec3d.Up, w.Yaw).Rotate(o.Place);
                worst = Math.Max(worst, (place - w.Player).Magnitude);
            }
            Assert.Greater(worst, 4.0 * w.Rules.hearEvery * 0.8, "it lags behind someone on the move");
            Assert.Less(worst, 4.0 * w.Rules.hearEvery + 0.5, "by no more than its hearing's beat");
        }

        [Test]
        public void AShot_BringsItAtARun_AndTheRunDropsBackToAWalk()
        {
            var w = new World { Noise = 0.0, Player = new Vec3d(0, 0, 26) };
            w.Run(1);
            w.Noise = 2.0;
            w.Run(0.1);
            w.Noise = 0.0;
            Assert.IsTrue(w.RunUntil(x => x.Brain.Mode == StalkerMode.Rush, 2), "a shot is heard from further than footsteps, and it runs");
            w.Run(0.5);
            Assert.AreEqual(w.Rules.rushPace, w.FastestForward, 0.01);
            Assert.Less(w.Rules.rushPace, 8.0, "a sprint outruns it");
            Assert.Greater(w.Rules.rushPace, 5.0, "a walk does not");
            Assert.IsTrue(w.RunUntil(x => x.Brain.Mode == StalkerMode.Stalk, w.Rules.rushSeconds + 0.5), "with nothing more to hear it slows");
        }

        // ------------------------------------------------------------------ the warning and the lunge

        static World AtTheWarning(double side = 0.0)
        {
            var w = new World { Noise = 1.0, Player = new Vec3d(side, 0, 9) };
            Assert.IsTrue(w.RunUntil(x => x.Brain.Mode == StalkerMode.Gape, 10), "it comes within reach and warns");
            return w;
        }

        [Test]
        public void TheWarning_IsItsWholeLength_AndItStandsStillForIt()
        {
            World w = AtTheWarning();
            double began = w.Time, travelled = w.Travelled;
            Assert.IsTrue(w.RunUntil(x => x.Brain.Mode == StalkerMode.Lunge, 2));
            Assert.AreEqual(w.Rules.gapeSeconds, w.Time - began, 0.05);
            Assert.AreEqual(travelled, w.Travelled, 1e-9, "it does not creep forward while it warns");
            Assert.Less(w.Distance - StalkerLayout.Mouth, w.Rules.lungeReach, "it only warns inside its reach");
        }

        [Test]
        public void SomeoneWhoStaysPut_IsBittenOnce()
        {
            World w = AtTheWarning();
            w.Run(w.Rules.gapeSeconds + w.Rules.lungeSeconds + 0.3);
            Assert.AreEqual(1, w.Bites);
            Assert.AreEqual(w.Rules.biteDamage, w.Hurt, 1e-6);
            Assert.AreEqual(StalkerMode.Recover, w.Brain.Mode, "and it lies where it landed");
        }

        [Test]
        public void AStepAside_AfterItHasTakenAim_IsAMiss()
        {
            World w = AtTheWarning();
            w.Run(w.Rules.gapeSeconds * StalkerBrain.LockAt + 0.03);
            w.Player += new Vec3d(1.6, 0, 0);
            w.Run(w.Rules.gapeSeconds + w.Rules.lungeSeconds + 0.3);
            Assert.AreEqual(0, w.Bites, "it lunged at where they were");
            Assert.Contains(StalkerMode.Lunge, w.Modes);
        }

        [Test]
        public void AStepAside_BeforeItHasTakenAim_IsFollowed()
        {
            World w = AtTheWarning();
            w.Run(0.05);
            w.Player += new Vec3d(0.9, 0, 0);
            w.Run(w.Rules.gapeSeconds + w.Rules.lungeSeconds + 0.3);
            Assert.AreEqual(1, w.Bites, "it was still following");
        }

        [Test]
        public void TheLunge_GoesNoFurtherThanItsReach_AndLessWithTheTailCut()
        {
            double Lunged(int cutAt)
            {
                var w = new World { Noise = 1.0, Player = new Vec3d(0, 0, 9) };
                if (cutAt >= 0) w.Body.HitSeg(cutAt, 0, sever: true);
                Assert.IsTrue(w.RunUntil(x => x.Brain.Mode == StalkerMode.Gape, 10));
                // They run off as it warns, so it lunges as far as it can.
                w.PlayerVelocity = new Vec3d(0, 0, 8);
                double before = w.Travelled;
                w.RunUntil(x => x.Brain.Mode == StalkerMode.Recover, 2);
                return w.Travelled - before;
            }
            double whole = Lunged(-1), cut = Lunged(StalkerLayout.Trunk);
            Assert.LessOrEqual(whole, new StalkerRules().lungeReach + 0.15);
            Assert.Greater(whole, new StalkerRules().lungeReach * 0.8);
            Assert.Less(cut, whole * 0.7);
        }

        [Test]
        public void AfterALunge_ItLiesThere_ThenGoesOn()
        {
            World w = AtTheWarning();
            Assert.IsTrue(w.RunUntil(x => x.Brain.Mode == StalkerMode.Recover, 3));
            double began = w.Time, travelled = w.Travelled;
            Assert.IsTrue(w.RunUntil(x => x.Brain.Mode != StalkerMode.Recover, 3));
            Assert.AreEqual(w.Rules.recoverSeconds, w.Time - began, 0.05);
            Assert.AreEqual(travelled, w.Travelled, 1e-9, "the opening: it does not move");
        }

        [Test]
        public void ItNeverGoesBackwardOrSideways()
        {
            // Its body follows its head, so its head only ever goes forward.
            var w = new World { Noise = 1.0, Player = new Vec3d(-6, 0, -9), PlayerVelocity = new Vec3d(1.5, 0, 0.5) };
            w.Run(25);
            Assert.GreaterOrEqual(w.LeastForward, 0.0);
            Assert.AreEqual(0.0, w.Sideways, 1e-9);
            Assert.Greater(w.Travelled, 3.0);
        }

        [Test]
        public void SomethingSolidInFrontOfIt_StopsIt()
        {
            var w = new World { Noise = 1.0, Player = new Vec3d(0, 0, 12), Blocked = true };
            w.Run(6);
            Assert.AreEqual(0.0, w.Travelled, 1e-9);
        }

        [Test]
        public void KeptFromThePlaceBySomethingSolid_ItGivesItUp_AndCastsAbout()
        {
            // The game says "blocked" on the frames a step was refused: about every other one.
            int frame = 0;
            var w = new World { Noise = 1.0, Player = new Vec3d(0, 0, 12) };
            w.Run(1.0);
            w.Noise = 0.0;
            w.Before = x => x.Blocked = frame++ % 2 == 0;
            Assert.IsTrue(w.RunUntil(x => x.Brain.Mode == StalkerMode.Search, 4.0), "it does not stand at a wall for ever");
            Assert.IsTrue(w.RunUntil(x => x.Brain.Mode == StalkerMode.Still, w.Rules.searchSeconds + 1.0));
        }

        [Test]
        public void ASoundFromBeyondWhereItRoams_IsNeverLungedAt()
        {
            // Shot from far outside its ground, again and again: it comes as far as it will and casts about, and never
            // bites the air at the edge.
            var w = new World { Noise = 2.2, Player = new Vec3d(0, 0, 40) };
            w.Run(30);
            Assert.IsFalse(w.Modes.Contains(StalkerMode.Gape));
            Assert.IsTrue(w.Modes.Contains(StalkerMode.Search));
            Assert.LessOrEqual(w.FurthestFromHome, w.Rules.roamRadius + 4.0, "casting about takes it only a little further");
        }

        [Test]
        public void SomeoneWalkingCirclesRoundIt_UpClose_IsStillFaced()
        {
            // At one metre, round and round at a walk: it feels them, and turns quicker than they can circle.
            double angle = 0.0;
            var w = new World { Noise = 1.0 };
            w.Before = x =>
            {
                angle += 4.0 * Dt;   // a walk of 5 m/s at this distance
                x.Player = x.Root + new Vec3d(Math.Sin(angle), 0, Math.Cos(angle)) * (StalkerLayout.Mouth + 0.9);
            };
            bool got = w.RunUntil(x => x.Brain.Mode == StalkerMode.Lunge, 6.0);
            Assert.IsTrue(got, "there is no safe place at its shoulder: " + string.Join(",", w.Modes) + " yaw " + w.Yaw + " angle " + angle);
        }

        [Test]
        public void StandingStill_AwayFromWhereYouWereHeard_IsNotFound()
        {
            // Heard walking; then they step well aside between two of its hearing's beats and stand still.
            var w = new World { Noise = 1.0, Player = new Vec3d(0, 0, 12) };
            w.RunUntil(x => x.Last.Took, 2.0);
            w.Run(0.1);
            w.Noise = 0.0;
            w.Player = new Vec3d(4.0, 0, 12);
            w.Run(20);
            Assert.AreEqual(0, w.Bites);
            Assert.IsFalse(w.Modes.Contains(StalkerMode.Gape), "it went where the sound was, and nobody was there");
            Assert.IsTrue(w.Modes.Contains(StalkerMode.Search));
        }

        [Test]
        public void EachNewPlaceItTakes_IsTold_AtItsHearingsBeat()
        {
            var w = new World { Noise = 1.0, Player = new Vec3d(0, 0, 60), PlayerVelocity = new Vec3d(4.0, 0, 0) };
            w.Player = new Vec3d(-8, 0, 15);
            int told = 0;
            double first = -1, last = 0;
            for (int i = 0; i < 240; i++)
            {
                if (!w.Step().Took || w.Brain.Mode == StalkerMode.Gape) continue;
                told++;
                if (first < 0) first = w.Time;
                last = w.Time;
            }
            Assert.GreaterOrEqual(told, 3);
            Assert.AreEqual(w.Rules.hearEvery, (last - first) / (told - 1), 0.05);
        }

        // ------------------------------------------------------------------ being hurt

        [Test]
        public void AGillShotOff_MakesItReel_AndCallsOffTheWarning_ButNotTwiceRunning()
        {
            World w = AtTheWarning();
            w.Body.HitGill(0, 0, sever: true);
            Assert.IsTrue(w.Brain.Reel());
            w.Step();
            Assert.AreEqual(StalkerMode.Reel, w.Brain.Mode);
            w.Body.HitGill(1, 0, sever: true);
            Assert.IsFalse(w.Brain.Reel(), "it cannot be kept reeling");
            double began = w.Time;
            Assert.IsTrue(w.RunUntil(x => x.Brain.Mode != StalkerMode.Reel, 3));
            Assert.AreEqual(w.Rules.reelSeconds, w.Time - began, 0.06);
            Assert.AreEqual(0, w.Bites, "the lunge it was warning of never came");
        }

        [Test]
        public void Hurt_ItRunsAtWhoeverDidIt_EvenIfTheyAreSilent()
        {
            var w = new World { Noise = 0.0, Player = new Vec3d(5, 0, 14) };
            w.Run(1);
            Assert.AreEqual(StalkerMode.Still, w.Brain.Mode);
            StalkerSenses s = w.Senses();
            w.Brain.Hurt(s, s.Player);
            w.Step();
            Assert.AreEqual(StalkerMode.Rush, w.Brain.Mode);
            Assert.IsTrue(w.RunUntil(x => x.Distance < 4.5, 6), "it comes to where the hurt came from");
        }

        // ------------------------------------------------------------------ where it lives

        [Test]
        public void ItNeverGoesFurtherFromHomeThanItRoams()
        {
            var w = new World { Noise = 2.0, Player = new Vec3d(0, 0, 10), PlayerVelocity = new Vec3d(0, 0, 7.5) };
            w.Run(40);
            Assert.Less(w.FurthestFromHome, w.Rules.roamRadius + w.Rules.lungeReach + 1.0);
        }

        [Test]
        public void LeftAloneFarFromHome_ItWalksBack()
        {
            var w = new World { Noise = 1.0, Player = new Vec3d(0, 0, 18) };
            w.Run(4);
            w.Hostile = false;
            Assert.IsTrue(w.RunUntil(x => x.Brain.Mode == StalkerMode.Return, w.Rules.searchSeconds + w.Rules.homesickSeconds + 12));
            Assert.IsTrue(w.RunUntil(x => x.Brain.Mode == StalkerMode.Still, 30));
            Assert.Less((w.Root - w.Home).Magnitude, 2.5);
        }

        [Test]
        public void WithLegsLost_ItIsSlower()
        {
            double Pace(int lost)
            {
                var w = new World { Noise = 1.0, Player = new Vec3d(0, 0, 18) };
                for (int leg = 0; leg < lost; leg++) w.Body.HitLeg(leg, 0, sever: true);
                w.Run(3);
                return w.FastestForward;
            }
            Assert.AreEqual(new StalkerRules().stalkPace, Pace(0), 0.01);
            Assert.Less(Pace(2), Pace(0));
            Assert.Less(Pace(4), Pace(2));
            Assert.Greater(Pace(4), 0.5);
        }

        // ------------------------------------------------------------------ how it moves

        [Test]
        public void ItsBodyNeverStretches_AndFollowsItsHead()
        {
            var w = new World { Noise = 1.0, Player = new Vec3d(-7, 0, -6), PlayerVelocity = new Vec3d(2, 0, 1) };
            for (int i = 0; i < 60 * 15; i++)
            {
                w.Step();
                for (int n = 1; n < w.Pose.Nodes; n++)
                {
                    double gap = (w.Pose.NodePosition[n] - w.Pose.NodePosition[n - 1]).Magnitude;
                    Assert.AreEqual(StalkerLayout.Spacing, gap, 0.12, "node " + n + " at " + w.Time.ToString("0.00") + " s in " + w.Brain.Mode);
                }
                // (It turns its head at the neck, half a spacing back, so looking round carries the head's node a little aside.)
                Assert.AreEqual(0.0, w.Pose.NodePosition[0].X, 0.14, "its head is over its root");
                Assert.AreEqual(0.0, w.Pose.NodePosition[0].Z, 0.14);
            }
        }

        [Test]
        public void Still_ItLiesOnItsBelly_AndWalkingItCarriesItselfHigher()
        {
            var w = new World { Noise = 0.0 };
            w.Run(3);
            Assert.AreEqual(StalkerAnimator.BellyHeight, w.Pose.NodePosition[4].Y, 0.02);
            w.Noise = 1.0;
            w.Player = new Vec3d(0, 0, 16);
            w.Run(3);
            Assert.AreEqual(StalkerMode.Stalk, w.Brain.Mode);
            Assert.AreEqual(StalkerLayout.RideHeight, w.Pose.NodePosition[4].Y, 0.03);
            Assert.Greater(StalkerAnimator.BellyHeight, StalkerLayout.SegHalfHeight, "its belly is never under the ground");
        }

        [Test]
        public void ItsFeet_StandOnTheGround_StepADiagonalPairAtATime_AndItsLegsReachThem()
        {
            var w = new World { Noise = 1.0, Player = new Vec3d(5, 0, 16), PlayerVelocity = new Vec3d(0.5, 0, 1.0) };
            w.Run(1.5);
            int steps = 0;
            var wasUp = new bool[StalkerLayout.Legs];
            for (int i = 0; i < 60 * 8; i++)
            {
                w.Step();
                if (w.Brain.Mode != StalkerMode.Stalk) continue;
                var up = new bool[StalkerLayout.Legs];
                for (int leg = 0; leg < StalkerLayout.Legs; leg++)
                {
                    Vec3d foot = w.Pose.Foot[leg];
                    Assert.GreaterOrEqual(foot.Y, -0.02, "a foot under the ground");
                    Assert.LessOrEqual(foot.Y, 0.2);
                    up[leg] = foot.Y > 0.03;
                    if (up[leg] && !wasUp[leg]) steps++;
                    wasUp[leg] = up[leg];
                    Assert.AreEqual(StalkerLayout.LegLength, (w.Pose.ShinPosition[leg] - w.Pose.LegPosition[leg]).Magnitude, 1e-6, "the upper leg is its own length");
                    Assert.AreEqual(StalkerLayout.ShinLength, (foot - w.Pose.ShinPosition[leg]).Magnitude, 1e-6, "and the lower");
                }
                Assert.IsFalse((up[0] || up[3]) && (up[1] || up[2]), "both diagonals in the air at " + w.Time.ToString("0.00"));
            }
            Assert.Greater(steps, 12, "it does step");
        }

        [Test]
        public void OnFewerThanTwoLegs_ItStaysOnItsBelly()
        {
            var w = new World { Noise = 1.0, Player = new Vec3d(0, 0, 16) };
            for (int leg = 0; leg < 3; leg++) w.Body.HitLeg(leg, 0, sever: true);
            w.Run(4);
            Assert.Greater(w.Travelled, 1.0, "it still comes");
            Assert.AreEqual(StalkerAnimator.BellyHeight, w.Pose.NodePosition[3].Y, 0.03);
        }

        [Test]
        public void TheJaw_OpensSlowly_AndFallsWideWhenItTakesAim()
        {
            double before = StalkerAnimator.GapeOf(StalkerMode.Gape, StalkerBrain.LockAt - 0.01);
            double after = StalkerAnimator.GapeOf(StalkerMode.Gape, StalkerBrain.LockAt + 0.13);
            Assert.Less(before, 0.5);
            Assert.AreEqual(1.0, after, 1e-6);
            double last = 0.0;
            for (double t = 0; t <= 1.0; t += 0.01)
            {
                double g = StalkerAnimator.GapeOf(StalkerMode.Gape, t);
                Assert.GreaterOrEqual(g, last - 1e-9, "it never closes again during the warning");
                last = g;
            }
            Assert.AreEqual(0.0, StalkerAnimator.GapeOf(StalkerMode.Stalk, 0.5), 1e-9, "walking, its mouth is shut");
            World w = AtTheWarning();
            w.Run(w.Rules.gapeSeconds - 0.02);
            Assert.Greater(w.Pose.Gape, 0.85);
            // The jaw hangs below the head.
            Vec3d tip = w.Pose.JawPosition + w.Pose.JawRotation.Rotate(Vec3d.Forward) * StalkerLayout.JawLength;
            Vec3d shut = w.Pose.JawPosition + w.Pose.NodeRotation[0].Rotate(Vec3d.Forward) * StalkerLayout.JawLength;
            Assert.Less(tip.Y, shut.Y - 0.15);
        }

        [Test]
        public void ItsGills_StandOutWhenItHearsSomething_AndLieBackWhenItDoesNot()
        {
            var w = new World { Noise = 0.0 };
            w.Run(3);
            double lying = w.Pose.Flare;
            Assert.Less(lying, 0.1);
            w.Noise = 1.0;
            w.Run(0.5);
            Assert.Greater(w.Pose.Flare, 0.5);
            // Spread, a gill points further out to its own side.
            Vec3d right = w.Pose.GillRotation[StalkerLayout.RightSide].Rotate(Vec3d.Forward), left = w.Pose.GillRotation[StalkerLayout.LeftSide].Rotate(Vec3d.Forward);
            Vec3d headRight = w.Pose.NodeRotation[0].Rotate(Vec3d.Right);
            Assert.Greater(right.X * headRight.X + right.Y * headRight.Y + right.Z * headRight.Z, 0.5);
            Assert.Less(left.X * headRight.X + left.Y * headRight.Y + left.Z * headRight.Z, -0.5);
        }

        [Test]
        public void WithItsTailCut_ThePoseHasFewerNodes()
        {
            var w = new World { Noise = 0.0 };
            w.Run(0.2);
            Assert.AreEqual(StalkerLayout.Nodes, w.Pose.Nodes);
            w.Body.HitSeg(StalkerLayout.Trunk + 1, 0, sever: true);
            w.Run(0.2);
            Assert.AreEqual(StalkerLayout.Trunk + 2, w.Pose.Nodes);
        }

        [Test]
        public void TheLayout_AddsUp()
        {
            Assert.AreEqual(4.3, StalkerLayout.Length, 0.3, "about 4.3 m long");
            Assert.Greater(StalkerLayout.RideHeight, StalkerLayout.SegHalfHeight + 0.1, "its belly clears the ground when it walks");
            Assert.Greater(StalkerLayout.LegLength + StalkerLayout.ShinLength, StalkerLayout.RideHeight + 0.1, "its legs are long enough to reach the ground out to the side");
            for (int leg = 0; leg < StalkerLayout.Legs; leg++) Assert.Less(StalkerLayout.NodeOf(leg), StalkerLayout.Trunk + 1, "legs are on the trunk");
            for (int seg = 0; seg < StalkerLayout.Segs; seg++) Assert.Greater(StalkerLayout.Scale(seg), 0.3);
            Assert.Less(StalkerLayout.Scale(StalkerLayout.Segs - 1), StalkerLayout.Scale(StalkerLayout.Trunk), "the tail thins to its tip");
        }

        // ------------------------------------------------------------------ the model

        [Test]
        public void StalkerModelFile_MatchesTheLayout()
        {
            // tools/blender/build_creatures.py carries its own copy of StalkerLayout (STALKER). If either changes,
            // this fails until the script is changed to match and run again.
            ModelFile model = Load(StalkerLayout.Model);
            AssertSocket(model, "SegEnd", 0, 0, StalkerLayout.Spacing * 0.5);
            AssertSocket(model, "SegSide", StalkerLayout.SegHalfWidth, 0, 0);
            AssertSocket(model, "SegTop", 0, StalkerLayout.SegHalfHeight, 0);
            AssertSocket(model, "TailSide", StalkerLayout.TailHalfWidth, 0, 0);
            AssertSocket(model, "TailTop", 0, StalkerLayout.TailHalfHeight, 0);
            AssertSocket(model, "HeadEnd", 0, 0, StalkerLayout.HeadLength - StalkerLayout.Spacing * 0.5);
            AssertSocket(model, "HeadSide", StalkerLayout.HeadHalfWidth, 0, 0);
            AssertSocket(model, "HeadTop", 0, StalkerLayout.HeadHalfHeight, 0);
            AssertSocket(model, "JawHinge", StalkerLayout.JawHinge.X, StalkerLayout.JawHinge.Y, StalkerLayout.JawHinge.Z);
            AssertSocket(model, "JawEnd", 0, 0, StalkerLayout.JawLength);
            AssertSocket(model, "Mouth", 0, 0, StalkerLayout.Mouth);
            AssertSocket(model, "GillR", StalkerLayout.GillRoot.X, StalkerLayout.GillRoot.Y, StalkerLayout.GillRoot.Z);
            AssertSocket(model, "GillEnd", 0, 0, StalkerLayout.GillLength);
            AssertSocket(model, "GillSide", StalkerLayout.GillSpread, 0, 0);
            AssertSocket(model, "HipR", StalkerLayout.Hip.X, StalkerLayout.Hip.Y, StalkerLayout.Hip.Z);
            AssertSocket(model, "LegEnd", 0, 0, StalkerLayout.LegLength);
            AssertSocket(model, "ShinEnd", 0, 0, StalkerLayout.ShinLength);
            AssertSocket(model, "LegSide", StalkerLayout.LegRadius, 0, 0);
            AssertSocket(model, "StumpRim", StalkerLayout.StumpRadius, 0, 0);
            var pieces = new List<string> { StalkerLayout.HeadPiece, StalkerLayout.JawPiece, StalkerLayout.LegPiece, StalkerLayout.ShinPiece,
                                            StalkerLayout.GillPiece, StalkerLayout.Stump, StalkerLayout.Meat[0], StalkerLayout.Meat[1] };
            for (int seg = 0; seg < StalkerLayout.Segs; seg++) pieces.Add(StalkerLayout.PieceOf(seg));
            foreach (string piece in pieces) Assert.IsNotNull(model.Part(piece), "missing piece " + piece);
            Assert.IsTrue(model.Textured, "the Blind stalker is drawn with pictures");

            // The shapes are the size the rules (and the hitboxes) think they are. Along is +Z. `near` keeps only
            // the corners within that distance of the piece's own middle, for its size there.
            void Box(string piece, out float right, out float left, out float hi, out float lo, out float fore, out float aft, float near = float.MaxValue)
            {
                ModelPart part = model.Part(piece);
                right = float.MinValue; left = float.MaxValue; hi = float.MinValue; lo = float.MaxValue; fore = float.MinValue; aft = float.MaxValue;
                for (int i = 0; i < part.VertexCount; i++)
                {
                    if (Math.Abs(part.Positions[i * 3 + 2]) > near) continue;
                    right = Math.Max(right, part.Positions[i * 3]);
                    left = Math.Min(left, part.Positions[i * 3]);
                    hi = Math.Max(hi, part.Positions[i * 3 + 1]);
                    lo = Math.Min(lo, part.Positions[i * 3 + 1]);
                    fore = Math.Max(fore, part.Positions[i * 3 + 2]);
                    aft = Math.Min(aft, part.Positions[i * 3 + 2]);
                }
            }
            float right, left, hi, lo, fore, aft;
            // The body is one outline cut at its joints: every length has its own piece, as wide and tall at its
            // middle as the layout says, and its ends meet its neighbours' with no step.
            float lastWide = 0f, lastTall = 0f;
            for (int seg = 0; seg < StalkerLayout.Segs; seg++)
            {
                string piece = StalkerLayout.PieceOf(seg);
                bool tail = StalkerLayout.IsTail(seg), legs = Array.IndexOf(StalkerLayout.LegSeg, seg) >= 0;
                double wide = (tail ? StalkerLayout.TailHalfWidth : StalkerLayout.SegHalfWidth) * StalkerLayout.Scale(seg);
                double high = (tail ? StalkerLayout.TailHalfHeight : StalkerLayout.SegHalfHeight) * StalkerLayout.Tall(seg);
                Box(piece, out right, out left, out hi, out lo, out fore, out aft, 0.03f);
                // (A length with legs swells at the shoulder or hip.)
                Assert.AreEqual(wide, right, legs ? 0.05 : 0.015, piece + ": its width at its middle");
                Assert.AreEqual(-right, left, 0.006, piece + " is the same either side");
                Assert.AreEqual(high, hi, 0.015, piece + ": its upper side");
                Assert.AreEqual(-high, lo, legs ? 0.03 : 0.015, piece + ": its underside");

                Box(piece, out right, out left, out hi, out lo, out fore, out aft);
                Assert.Greater(fore, StalkerLayout.Spacing * 0.5 + 0.03, piece + " reaches into the one in front of it, so a bend opens no gap");
                Assert.Less(fore, StalkerLayout.Spacing * 0.5 + 0.10, piece + " without running far into it");
                if (seg < StalkerLayout.Segs - 1)
                {
                    Assert.Less(aft, -StalkerLayout.Spacing * 0.5 - 0.03, piece + " reaches into the one behind");
                    Assert.Greater(aft, -StalkerLayout.Spacing * 0.5 - 0.10, piece);
                }
                else Assert.AreEqual(-StalkerLayout.Spacing * 0.5, aft, 0.02, "the tail ends at its tip");

                // Where two lengths meet they are the same size: the corners at this one's front end against the
                // last one's back end.
                float frontWide = 0f, frontTall = 0f, backWide = 0f, backTall = 0f;
                ModelPart part = model.Part(piece);
                float joint = (float)(StalkerLayout.Spacing * 0.5);
                for (int i = 0; i < part.VertexCount; i++)
                {
                    float z = part.Positions[i * 3 + 2];
                    if (Math.Abs(z - joint) < 0.002f) { frontWide = Math.Max(frontWide, part.Positions[i * 3]); frontTall = Math.Max(frontTall, part.Positions[i * 3 + 1]); }
                    if (Math.Abs(z + joint) < 0.002f) { backWide = Math.Max(backWide, part.Positions[i * 3]); backTall = Math.Max(backTall, part.Positions[i * 3 + 1]); }
                }
                Assert.Greater(frontWide, 0f, piece + " has a ring of corners at its front joint");
                if (seg > 0)
                {
                    Assert.AreEqual(lastWide, frontWide, 0.003, piece + " is as wide at its front as the length before it is at its back");
                    Assert.AreEqual(lastTall, frontTall, 0.003, piece + " and as tall");
                }
                lastWide = backWide; lastTall = backTall;
            }

            Box(StalkerLayout.HeadPiece, out right, out left, out hi, out lo, out fore, out aft);
            Assert.AreEqual(StalkerLayout.HeadLength - StalkerLayout.Spacing * 0.5, fore, 0.03, "the snout's tip");
            Assert.AreEqual(StalkerLayout.HeadHalfWidth, right, 0.03, "the head's width");
            Assert.AreEqual(-right, left, 0.008, "the head is the same either side");
            Assert.Less(aft, -StalkerLayout.Spacing * 0.5, "the head reaches back into the neck");

            Box(StalkerLayout.JawPiece, out right, out left, out hi, out lo, out fore, out aft);
            Assert.AreEqual(StalkerLayout.JawLength, fore, 0.02, "the jaw's length from its hinge");
            Assert.AreEqual(-right, left, 0.008, "the jaw is the same either side");
            Assert.Less(hi, 0.02, "the jaw hangs under its hinge, so shut it lies under the snout");

            Box(StalkerLayout.LegPiece, out right, out left, out hi, out lo, out fore, out aft);
            Assert.AreEqual(StalkerLayout.LegLength, fore, 0.03, "the upper leg's length");
            Assert.AreEqual(-right, left, 0.006, "a leg is the same either side: nothing is mirrored");
            Assert.Less(right, StalkerLayout.LegRadius * 2.0, "its muscle is thickest at the hip, and still a thin leg");
            Box(StalkerLayout.ShinPiece, out right, out left, out hi, out lo, out fore, out aft);
            Assert.AreEqual(StalkerLayout.ShinLength, fore, 0.09, "the shin and its toes");
            Assert.AreEqual(-right, left, 0.006, "a shin is the same either side");

            Box(StalkerLayout.GillPiece, out right, out left, out hi, out lo, out fore, out aft);
            // Its three stalks are GillLength long and GillSpread apart; their threads stand out a little beyond, no two alike.
            Assert.Greater(fore, StalkerLayout.GillLength - 0.01, "a gill's length");
            Assert.Less(fore, StalkerLayout.GillLength + 0.10);
            Assert.Greater(right, StalkerLayout.GillSpread, "and spreads this wide");
            Assert.Less(right, StalkerLayout.GillSpread + 0.14);
            Assert.AreEqual(-right, left, 0.06, "a gill spreads as far either side: nothing is mirrored");

            ModelFile far = Load(StalkerLayout.Model + "Far");
            foreach (string piece in pieces) Assert.IsNotNull(far.Part(piece), "the far copy is missing " + piece);
            Assert.AreEqual(model.TextureSet, far.TextureSet);
        }

        static void AssertSocket(ModelFile model, string name, double x, double y, double z)
        {
            Assert.IsTrue(model.TrySocket(name, out ModelSocket s), "missing " + name);
            Assert.AreEqual(x, s.X, 0.002, name + " x");
            Assert.AreEqual(y, s.Y, 0.002, name + " y");
            Assert.AreEqual(z, s.Z, 0.002, name + " z");
        }

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
