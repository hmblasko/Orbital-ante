using System;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;
using OrbitalAnte.Look;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>The Roller (the giant isopod): what comes off, the roll and its warning, lying on its back, the chain that curls.</summary>
    public class RollerRulesTests
    {
        const double Dt = 1.0 / 60.0;
        const double Rifle = 24.0;

        static void AssertSound(RollerPose pose)
        {
            for (int i = 0; i < RollerLayout.Links; i++)
            {
                Vec3d p = pose.LinkPosition[i];
                QuatD q = pose.LinkRotation[i];
                Assert.IsFalse(double.IsNaN(p.X + p.Y + p.Z + q.X + q.Y + q.Z + q.W) || double.IsInfinity(p.X + p.Y + p.Z), "link " + i);
                Assert.AreEqual(1.0, Math.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W), 1e-6);
            }
            for (int i = 0; i < RollerLayout.Legs; i++)
            {
                Vec3d p = pose.LegPosition[i];
                QuatD q = pose.LegRotation[i];
                Assert.IsFalse(double.IsNaN(p.X + p.Y + p.Z + q.X + q.Y + q.Z + q.W), "leg " + i);
            }
        }

        static RollerPose Settle(RollerAnimator animator, RollerBody body, RollerAnimInput input, double seconds)
        {
            var pose = new RollerPose();
            for (double t = 0; t < seconds; t += Dt) animator.Update(body, input, Dt, pose);
            return pose;
        }

        // ---- the body

        [Test]
        public void LegsAreLow_PlatesAreArmour_AndOnlyTheSoftBodyOrTheHeadKills()
        {
            var body = new RollerBody();
            int hits = 0;
            while (body.LegOn(3)) { body.HitLeg(3, Rifle); hits++; }
            Assert.LessOrEqual(hits, 2, "a leg comes off in one or two rifle hits");
            hits = 0;
            while (body.PlateOn(2)) { body.HitPlate(2, Rifle); hits++; }
            Assert.AreEqual(3, hits, "a plate is armour, and still comes off in three rifle hits");
            for (int leg = 0; leg < RollerLayout.Legs; leg++) body.HitLeg(leg, 1000);
            for (int seg = 0; seg < RollerLayout.Segs; seg++) body.HitPlate(seg, 1000);
            body.HitTail(1000);
            Assert.IsTrue(body.Alive, "nothing that comes off kills it");
            Assert.AreEqual(0, body.Legs);
            Assert.AreEqual(0, body.Plates);
            hits = 0;
            while (body.Alive) { body.HitBody(true, Rifle); hits++; }
            Assert.That(hits, Is.InRange(6, 10), "the soft body: under half a magazine");

            var head = new RollerBody();
            hits = 0;
            while (head.Alive) { head.HitBody(false, Rifle); hits++; }
            Assert.That(hits, Is.InRange(11, 14), "the armoured head is the slow way, never a wall");
            Assert.IsTrue(new RollerBody().HitBody(false, 0, lethal: true).Killed);
        }

        [Test]
        public void ALossIsReportedOnce_AndAPieceThatIsGoneTakesNothing()
        {
            var body = new RollerBody();
            Assert.AreEqual(1, body.HitPlate(4, 0, sever: true).Losses.Count);
            Assert.AreEqual(0, body.HitPlate(4, 50).Losses.Count);
            RollerHit hit = body.HitLeg(7, 1000);
            Assert.AreEqual(RollerLossKind.Leg, hit.Losses[0].Kind);
            Assert.AreEqual(7, hit.Losses[0].Index);
            Assert.AreEqual(0, body.HitLeg(7, 1000).Losses.Count);
            Assert.AreEqual(RollerLossKind.Tail, body.HitTail(1000).Losses[0].Kind);
            Assert.AreEqual(0, body.HitTail(1000).Losses.Count);
        }

        [Test]
        public void LosingPlatesStopsTheRoll_LosingLegsSlowsIt_LosingTheTailKeepsItOnItsBack()
        {
            var body = new RollerBody();
            Assert.IsTrue(body.CanRoll);
            body.HitPlate(0, 1000);
            body.HitPlate(1, 1000);
            Assert.IsTrue(body.CanRoll, "two plates gone and it still closes");
            body.HitPlate(2, 1000);
            Assert.IsFalse(body.CanRoll, "three gone and it cannot make a ball");

            var slow = new RollerBody();
            double before = slow.SpeedShare;
            for (int leg = 0; leg < 6; leg++) slow.HitLeg(leg, 1000);
            Assert.Less(slow.SpeedShare, before);
            Assert.Greater(slow.SpeedShare, 0.3, "it can always drag itself along");
            Assert.AreEqual(1.3, slow.OnBackShare, 1e-9, "a little longer on its back for each leg lost");
            Assert.AreEqual(1.0, new RollerBody().OnBackShare, 1e-9);
            slow.HitTail(1000);
            Assert.Greater(slow.OnBackShare, 2.0);
        }

        // ---- the chain

        [Test]
        public void StandingStill_ItIsLevel_AtItsStandingHeight_WithItsFeetOnTheGround()
        {
            var body = new RollerBody();
            RollerPose pose = Settle(new RollerAnimator(), body, new RollerAnimInput(), 1.0);
            AssertSound(pose);
            for (int i = 0; i < RollerLayout.Links; i++)
            {
                Assert.AreEqual(RollerLayout.StandHeight, pose.LinkPosition[i].Y, 0.02, "link " + i);
                Assert.AreEqual(0.0, pose.LinkPosition[i].X, 1e-6);
            }
            Assert.Greater(pose.LinkPosition[RollerLayout.HeadLink].Z, pose.LinkPosition[RollerLayout.TailLink].Z + 1.8, "head in front, tail behind");
            for (int leg = 0; leg < RollerLayout.Legs; leg++)
            {
                Vec3d foot = pose.LegPosition[leg] + pose.LegRotation[leg].Rotate(Vec3d.Forward) * RollerLayout.LegLength;
                Assert.That(foot.Y, Is.InRange(-0.06, 0.12), "foot " + leg);
                Assert.Greater(foot.X * RollerLayout.Sign(leg), 0.3, "a leg stands out to its own side");
            }
        }

        [Test]
        public void Curled_ItIsAClosedBall_OfItsOwnRadius_WithItsBackOutside()
        {
            var body = new RollerBody();
            RollerPose pose = Settle(new RollerAnimator(), body, new RollerAnimInput { Mode = RollerMode.Roll, T = 0.5 }, 1.0);
            AssertSound(pose);
            Assert.AreEqual(1.0, pose.Curl, 0.01);
            Assert.AreEqual(RollerLayout.BallRadius, pose.Centre.Y, 0.01);
            for (int i = 0; i < RollerLayout.Links; i++)
            {
                Vec3d from = pose.LinkPosition[i] - pose.Centre;
                Assert.AreEqual(RollerLayout.SpineRadius, from.Magnitude, 0.06, "link " + i + " is on the ring");
                Vec3d back = pose.LinkRotation[i].Rotate(Vec3d.Up);
                Assert.Greater(Vec3d.Dot(back, from.Normalized), 0.9, "link " + i + " has its back outward");
                Assert.Greater(pose.LinkPosition[i].Y + 1e-6, 0.0);
            }
            // The head and the tail meet.
            Vec3d nose = pose.LinkPosition[0] + pose.LinkRotation[0].Rotate(Vec3d.Forward) * (0.5 * RollerLayout.HeadLength);
            Vec3d end = pose.LinkPosition[8] - pose.LinkRotation[8].Rotate(Vec3d.Forward) * (0.5 * RollerLayout.TailLength);
            Assert.Less(Vec3d.Distance(nose, end), 0.45);
            // Legs are folded inside the ball.
            for (int leg = 0; leg < RollerLayout.Legs; leg++)
            {
                Vec3d foot = pose.LegPosition[leg] + pose.LegRotation[leg].Rotate(Vec3d.Forward) * RollerLayout.LegLength;
                Assert.Less((foot - pose.Centre).Magnitude, RollerLayout.BallRadius - 0.1, "leg " + leg + " sticks out of the ball");
            }
        }

        [Test]
        public void TheBall_TurnsByExactlyWhatItRolls_SoItNeverSlides()
        {
            var body = new RollerBody();
            var animator = new RollerAnimator();
            RollerPose pose = Settle(animator, body, new RollerAnimInput { Mode = RollerMode.Roll, T = 0.5 }, 0.5);
            double rolled = 0.0;
            for (int i = 0; i < 90; i++)
            {
                animator.Update(body, new RollerAnimInput { Mode = RollerMode.Roll, Velocity = new Vec3d(0, 0, 11.0), T = 0.5 }, Dt, pose);
                rolled += 11.0 * Dt;
                Assert.AreEqual(RollerLayout.BallRadius, pose.Centre.Y, 0.012, "a ball does not bob");
            }
            Assert.AreEqual(rolled, animator.Spin * RollerLayout.BallRadius, 1e-6);
            // It turns forward: the top of the ball moves the way it is going.
            var one = new RollerAnimator();
            RollerPose start = Settle(one, body, new RollerAnimInput { Mode = RollerMode.Roll, T = 0.5 }, 0.5);
            double topZ = start.LinkPosition[RollerLayout.MiddleLink].Z;
            one.Update(body, new RollerAnimInput { Mode = RollerMode.Roll, Velocity = new Vec3d(0, 0, 11.0), T = 0.5 }, Dt, start);
            Assert.Greater(start.LinkPosition[RollerLayout.MiddleLink].Z, topZ);
        }

        [Test]
        public void AfterARoll_ItLiesOnItsBack_SoftSideUp_ThenRocksBackOntoItsFeet()
        {
            var body = new RollerBody();
            var animator = new RollerAnimator();
            var pose = new RollerPose();
            for (int i = 0; i < 37; i++) animator.Update(body, new RollerAnimInput { Mode = RollerMode.Roll, Velocity = new Vec3d(0, 0, 11.0), T = 0.5 }, Dt, pose);
            for (int i = 0; i < 30; i++) animator.Update(body, new RollerAnimInput { Mode = RollerMode.Open, T = i / 30.0 }, Dt, pose);
            for (int i = 0; i < 60; i++) animator.Update(body, new RollerAnimInput { Mode = RollerMode.OnBack, T = i / 60.0 }, Dt, pose);
            AssertSound(pose);
            Assert.Less(pose.Curl, 0.02);
            for (int i = 1; i <= RollerLayout.Segs; i++)
            {
                Assert.Less(pose.LinkRotation[i].Rotate(Vec3d.Up).Y, -0.95, "segment " + i + " is belly up");
                Assert.That(pose.LinkPosition[i].Y, Is.InRange(RollerLayout.Top(i) - 0.01, RollerLayout.BackHeight + 0.02), "it rests on its plates");
            }
            for (int leg = 0; leg < RollerLayout.Legs; leg++)
            {
                Vec3d foot = pose.LegPosition[leg] + pose.LegRotation[leg].Rotate(Vec3d.Forward) * RollerLayout.LegLength;
                Assert.Greater(foot.Y, pose.LegPosition[leg].Y, "legs in the air");
            }
            for (int i = 0; i <= 36; i++)
            {
                animator.Update(body, new RollerAnimInput { Mode = RollerMode.Righting, T = i / 36.0 }, Dt, pose);
                AssertSound(pose);
                for (int k = 0; k < RollerLayout.Links; k++) Assert.Greater(pose.LinkPosition[k].Y, 0.0);
            }
            RollerPose up = Settle(animator, body, new RollerAnimInput(), 0.5);
            for (int i = 1; i <= RollerLayout.Segs; i++) Assert.Greater(up.LinkRotation[i].Rotate(Vec3d.Up).Y, 0.98, "back on its feet");
            Assert.Greater(up.LinkPosition[0].Z, up.LinkPosition[8].Z, "and facing the way it was");
        }

        [Test]
        public void WithLegsLost_ItRidesLower_AndAnyLossesInAnyModeGiveASoundPose()
        {
            var whole = Settle(new RollerAnimator(), new RollerBody(), new RollerAnimInput(), 0.5);
            var hurt = new RollerBody();
            for (int leg = 0; leg < 10; leg++) hurt.HitLeg(leg, 1000);
            var low = Settle(new RollerAnimator(), hurt, new RollerAnimInput(), 0.5);
            Assert.Less(low.LinkPosition[4].Y, whole.LinkPosition[4].Y - 0.05);
            Assert.Greater(low.LinkPosition[4].Y, RollerLayout.BellyDepth - 0.01, "never through the ground");

            var rng = new SeededRandom(7);
            var body = new RollerBody();
            var animator = new RollerAnimator();
            var pose = new RollerPose();
            var modes = (RollerMode[])Enum.GetValues(typeof(RollerMode));
            for (int run = 0; run < 400; run++)
            {
                if (rng.Chance(0.1)) body.HitLeg(rng.Range(0, RollerLayout.Legs), 1000);
                if (rng.Chance(0.05)) body.HitPlate(rng.Range(0, RollerLayout.Segs), 1000);
                RollerMode mode = modes[rng.Range(0, modes.Length)];
                for (int i = 0; i < 12; i++)
                {
                    animator.Update(body, new RollerAnimInput { Mode = mode, T = i / 12.0, Velocity = new Vec3d(0, 0, mode == RollerMode.Roll ? 11 : 2), TurnRate = 1.2 }, Dt, pose);
                    AssertSound(pose);
                    for (int k = 0; k < RollerLayout.Links; k++) Assert.Greater(pose.LinkPosition[k].Y, -0.02);
                }
            }
        }

        // ---- the brain

        sealed class Field
        {
            public readonly RollerRules Rules = new RollerRules();
            public readonly RollerBody Body = new RollerBody();
            public RollerBrain Brain;
            public Vec3d At = Vec3d.Zero;
            public double Yaw;
            public Vec3d Player;
            public bool Blocked, Stopped, WasHit, Butted;
            public double Exposed, Time;

            public Field(Vec3d player, int seed = 3)
            {
                Player = player;
                Brain = new RollerBrain(Rules, seed);
            }

            public Vec3d Local(Vec3d world) => QuatD.AngleAxis(Vec3d.Up, -Yaw).Rotate(world - At);

            public RollerOrders Step()
            {
                var senses = new RollerSenses { SeesPlayer = true, Player = Local(Player), Home = Local(Vec3d.Zero), Blocked = Blocked, Stopped = Stopped };
                RollerOrders orders = Brain.Think(Body, senses, Dt);
                Yaw += orders.TurnRate * Dt;
                At += QuatD.AngleAxis(Vec3d.Up, Yaw).Rotate(orders.Velocity) * Dt;
                if (orders.Mode == RollerMode.Roll && Brain.RollHits(Local(Player))) WasHit = true;
                if (orders.Landed) Butted = true;
                if (Brain.Exposed) Exposed += Dt;
                Time += Dt;
                return orders;
            }
        }

        [Test]
        public void SeeingSomeoneWithRoom_ItCurlsUpFirst_ThenBowlsStraightThroughWhereTheyStood()
        {
            var field = new Field(new Vec3d(0, 0, 10));
            double wound = 0, firstRoll = -1;
            Vec3d rollStart = Vec3d.Zero;
            for (int i = 0; i < 600 && field.Brain.Mode != RollerMode.Open; i++)
            {
                RollerOrders orders = field.Step();
                if (orders.Mode == RollerMode.Wind) wound += Dt;
                if (orders.Mode == RollerMode.Roll && firstRoll < 0) { firstRoll = field.Time; rollStart = field.At; }
                if (orders.Mode == RollerMode.Wind) Assert.AreEqual(0.0, orders.Velocity.Magnitude, 1e-9, "it does not creep forward as it curls");
            }
            Assert.GreaterOrEqual(wound, field.Rules.windSeconds - 0.05, "the whole curl is the warning");
            Assert.IsTrue(field.WasHit, "someone who stands still is bowled over");
            Assert.AreEqual(0.0, field.At.X, 0.3, "a straight line");
            Assert.Greater(field.At.Z, 10 + field.Rules.overshoot - 1.0, "on past where they stood");
        }

        [Test]
        public void AStepAsideAfterItTakesAim_IsAMiss_AndItCannotFollow()
        {
            foreach (double from in new[] { 6.0, 10.0, 15.0 })
            {
                var field = new Field(new Vec3d(0, 0, from));
                bool aimed = false;
                for (int i = 0; i < 900 && field.Brain.Mode != RollerMode.Open; i++)
                {
                    RollerOrders orders = field.Step();
                    if (orders.Mode == RollerMode.Wind && orders.T >= RollerBrain.LockAt) aimed = true;
                    // Once it has taken aim the player walks (5 m/s, not a sprint) to the side.
                    if (aimed && field.Player.X < 2.5) field.Player += new Vec3d(5.0 * Dt, 0, 0);
                }
                Assert.IsTrue(aimed);
                Assert.IsFalse(field.WasHit, "from " + from + " m a walk to the side clears the ball");
            }
        }

        [Test]
        public void ARollEndsOnItsBack_ForLongEnoughToShoot_ThenItGetsUpAndComesAgain()
        {
            var field = new Field(new Vec3d(40, 0, 10));     // far off to the side: it will miss
            field.Player = new Vec3d(0, 0, 10);
            int rolls = 0;
            RollerMode last = RollerMode.Walk;
            double firstExposure = 0;
            for (int i = 0; i < 60 * 30; i++)
            {
                RollerOrders orders = field.Step();
                if (orders.Mode == RollerMode.Roll) field.Player = new Vec3d(30, 0, -30);   // gone from its path
                if (orders.Mode == RollerMode.Roll && last != RollerMode.Roll) rolls++;
                if (rolls == 1 && field.Brain.Exposed) firstExposure += Dt;
                if (field.Brain.Exposed) Assert.AreEqual(0.0, orders.Velocity.Magnitude + Math.Abs(orders.TurnRate), 1e-9, "on its back it goes nowhere");
                if (orders.Mode == RollerMode.Wind && orders.T < RollerBrain.LockAt) Assert.Less(RollerAnimator.CurlOf(orders.Mode, orders.T), 0.5, "it is not closed until it has taken aim");
                last = orders.Mode;
            }
            Assert.GreaterOrEqual(rolls, 2, "it gets up and rolls again");
            Assert.GreaterOrEqual(firstExposure, field.Rules.onBackSeconds + field.Rules.openSeconds, "soft side up for a couple of seconds");
            Assert.Less(firstExposure, 3.2);
        }

        [Test]
        public void RollingIntoAWall_LeavesItOnItsBackLonger()
        {
            double Exposure(bool wall)
            {
                var field = new Field(new Vec3d(0, 0, 12));
                bool rolled = false;
                for (int i = 0; i < 60 * 12; i++)
                {
                    RollerOrders orders = field.Step();
                    if (orders.Mode == RollerMode.Roll) { rolled = true; field.Player = new Vec3d(50, 0, 0); }
                    field.Blocked = wall && orders.Mode == RollerMode.Roll && field.At.Z > 6;
                    if (rolled && orders.Mode == RollerMode.Walk) break;
                }
                Assert.AreEqual(wall, field.Brain.Crashed);
                return field.Exposed;
            }
            Assert.Greater(Exposure(true), Exposure(false) + 1.0);
        }

        /// <summary>
        /// On a world the ball can meet ground it cannot roll up or off (steeper than its roll, a drop, the water's edge).
        /// That ends the roll as any roll ends, on its back for the usual time: only something solid that stands up is a
        /// crash (from the design review of pass 8: a slope a player cannot see the limit of gave the long crash).
        /// </summary>
        [Test]
        public void GroundItCannotRollUp_EndsTheRollLikeAnyOther_OnlySomethingSolidIsACrash()
        {
            double Exposure(bool stopped, bool blocked, out bool crashed, out double fellBack)
            {
                var field = new Field(new Vec3d(0, 0, 12));
                bool rolled = false;
                double stoppedAt = -1, endedAt = -1;
                for (int i = 0; i < 60 * 12; i++)
                {
                    RollerOrders orders = field.Step();
                    if (orders.Mode == RollerMode.Roll) { rolled = true; field.Player = new Vec3d(50, 0, 0); }
                    bool there = orders.Mode == RollerMode.Roll && field.At.Z > 6;
                    field.Stopped = stopped && there;
                    field.Blocked = blocked && there;
                    if (orders.Mode == RollerMode.Open && stoppedAt < 0) stoppedAt = field.At.Z;
                    if (orders.Mode == RollerMode.OnBack && endedAt < 0) endedAt = field.At.Z;
                    if (rolled && orders.Mode == RollerMode.Walk) break;
                }
                crashed = field.Brain.Crashed;
                fellBack = stoppedAt - endedAt;
                return field.Exposed;
            }
            double plain = Exposure(false, false, out bool plainCrash, out _);
            double ground = Exposure(true, false, out bool groundCrash, out double groundBack);
            double wall = Exposure(false, true, out bool wallCrash, out _);
            Assert.IsFalse(plainCrash);
            Assert.IsFalse(groundCrash, "the ground stopping it is no crash");
            Assert.IsTrue(wallCrash);
            Assert.AreEqual(plain, ground, 0.35, "on its back for the usual time");
            Assert.Greater(wall, ground + 1.0, "only a crash leaves it longer");
            Assert.That(groundBack, Is.InRange(0.6, 1.1), "it falls open back the way it came, not into the slope");
        }

        [Test]
        public void TooNearToRoll_ItBacksOffForRoom_AndButtsSomeoneWhoCrowdsIt()
        {
            var far = new Field(new Vec3d(0, 0, 3.5));
            RollerOrders orders = default;
            for (int i = 0; i < 20; i++) orders = far.Step();
            Assert.Less(orders.Velocity.Z, 0.0, "it backs away to make room");

            var near = new Field(new Vec3d(0, 0, 1.4));
            int landed = 0;
            for (int i = 0; i < 55; i++) if (near.Step().Landed) landed++;
            Assert.AreEqual(1, landed, "one butt, landing once");
            Assert.AreNotEqual(RollerMode.Wind, near.Brain.Mode);
        }

        [Test]
        public void WithTooFewPlates_ItNeverCurls_ItMakesOff_AndButtsOnlySomeoneWhoCrowdsIt()
        {
            var field = new Field(new Vec3d(0, 0, 10));
            for (int seg = 0; seg < 3; seg++) field.Body.HitPlate(seg, 1000);
            for (int i = 0; i < 60 * 8; i++)
            {
                RollerOrders orders = field.Step();
                Assert.AreNotEqual(RollerMode.Wind, orders.Mode);
                Assert.AreNotEqual(RollerMode.Roll, orders.Mode);
            }
            Assert.IsTrue(field.Brain.Fleeing);
            Assert.Greater(Vec3d.Distance(field.At, field.Player), 14.0, "it has put ground between them");

            var crowded = new Field(new Vec3d(0, 0, 1.4));
            for (int seg = 0; seg < 3; seg++) crowded.Body.HitPlate(seg, 1000);
            for (int i = 0; i < 60 && !crowded.Butted; i++) crowded.Step();
            Assert.IsTrue(crowded.Butted, "cornered, it still has something to hurt with");
            Assert.Less(crowded.Rules.buttDamage, crowded.Rules.rollDamage * 0.5f, "and it is much the weaker blow");
        }

        [Test]
        public void RollingIntoAWall_ItFallsOpenBackTheWayItCame()
        {
            var field = new Field(new Vec3d(0, 0, 12));
            double stoppedAt = -1, endedAt = 0;
            for (int i = 0; i < 60 * 8; i++)
            {
                RollerOrders orders = field.Step();
                field.Blocked = orders.Mode == RollerMode.Roll && field.At.Z > 6;
                if (orders.Mode == RollerMode.Open && stoppedAt < 0) stoppedAt = field.At.Z;
                if (orders.Mode == RollerMode.OnBack) { endedAt = field.At.Z; break; }
            }
            Assert.Greater(stoppedAt, 0);
            Assert.That(stoppedAt - endedAt, Is.InRange(0.6, 1.1), "room for the length of it between the ball and the wall");
        }

        [Test]
        public void APlateShotOffAsItCurls_CanStopTheRoll()
        {
            var field = new Field(new Vec3d(0, 0, 10));
            field.Body.HitPlate(0, 1000);
            field.Body.HitPlate(1, 1000);
            for (int i = 0; i < 200 && field.Brain.Mode != RollerMode.Wind; i++) field.Step();
            Assert.AreEqual(RollerMode.Wind, field.Brain.Mode);
            field.Body.HitPlate(2, 1000);
            RollerOrders orders = field.Step();
            Assert.AreEqual(RollerMode.Walk, orders.Mode);
        }

        [Test]
        public void TheRoll_OutrunsASprint_ButOnFootItIsSlowerThanAWalk()
        {
            var rules = new RollerRules();
            Assert.Greater(rules.rollSpeed, 8f, "running straight away from the ball does not work");
            Assert.Less(rules.pace, 5f, "on its feet it never catches anyone");
            Assert.Greater(rules.hitRadius, (float)RollerLayout.BallRadius, "the ball is as wide as it looks");
            Assert.Less(rules.hitRadius, (float)RollerLayout.BallRadius + 0.6f, "and no wider than it looks plus a person");
        }

        [Test]
        public void Wandering_IsTheSameForTheSameSeed()
        {
            double Run(int seed)
            {
                var brain = new RollerBrain(new RollerRules(), seed);
                var body = new RollerBody();
                double turned = 0;
                for (int i = 0; i < 1200; i++) turned += brain.Think(body, new RollerSenses(), Dt).TurnRate * Dt;
                return turned;
            }
            Assert.AreEqual(Run(5), Run(5), 0.0);
            Assert.AreNotEqual(Run(5), Run(6));
        }

        // ---- the model

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
        public void RollerModelFile_MatchesTheLayout()
        {
            // tools/blender/build_creatures.py carries its own copy of RollerLayout (ROLLER). If either changes, this
            // fails until the script is changed to match and run again.
            ModelFile model = Load(RollerLayout.Model);
            AssertSocket(model, "HeadEnd", 0, 0, RollerLayout.HeadLength * 0.5);
            AssertSocket(model, "SegEnd", 0, 0, RollerLayout.SegLength * 0.5);
            AssertSocket(model, "TailEnd", 0, 0, -RollerLayout.TailLength * 0.5);
            AssertSocket(model, "PlateTop", 0, RollerLayout.BackHeight, 0);
            AssertSocket(model, "PlateSide", RollerLayout.PlateHalfWidth, 0, 0);
            AssertSocket(model, "PlateRear", 0, -RollerLayout.PlateSkirt, -RollerLayout.SegLength * 0.5 - RollerLayout.PlateOverlap);
            AssertSocket(model, "SegSide", RollerLayout.SegHalfWidth, -RollerLayout.BellyDepth, 0);
            AssertSocket(model, "HeadCorner", RollerLayout.HeadHalfWidth, RollerLayout.HeadTop, 0);
            AssertSocket(model, "TailCorner", RollerLayout.TailHalfWidth, RollerLayout.TailTop, 0);
            AssertSocket(model, "HipR", RollerLayout.Hip.X, RollerLayout.Hip.Y, RollerLayout.Hip.Z);
            AssertSocket(model, "LegEnd", 0, 0, RollerLayout.LegLength);
            AssertSocket(model, "StumpRim", RollerLayout.StumpRadius, 0, 0);
            string[] pieces = { RollerLayout.HeadPiece, RollerLayout.SegPiece, RollerLayout.PlatePiece, RollerLayout.TailPiece, RollerLayout.LegPiece, RollerLayout.Stump, RollerLayout.Meat[0], RollerLayout.Meat[1] };
            foreach (string piece in pieces) Assert.IsNotNull(model.Part(piece), "missing piece " + piece);
            Assert.IsTrue(model.Textured, "the Roller is drawn with pictures");

            // The shapes are the size the rules think they are.
            void Fits(string piece, double halfWidth, double top, double bottom, double front, double back)
            {
                ModelPart part = model.Part(piece);
                float x = 0, hi = float.MinValue, lo = float.MaxValue, fore = float.MinValue, aft = float.MaxValue;
                for (int i = 0; i < part.VertexCount; i++)
                {
                    x = Math.Max(x, Math.Abs(part.Positions[i * 3]));
                    hi = Math.Max(hi, part.Positions[i * 3 + 1]);
                    lo = Math.Min(lo, part.Positions[i * 3 + 1]);
                    fore = Math.Max(fore, part.Positions[i * 3 + 2]);
                    aft = Math.Min(aft, part.Positions[i * 3 + 2]);
                }
                Assert.AreEqual(halfWidth, x, 0.04, piece + " half width");
                Assert.AreEqual(top, hi, 0.04, piece + " top");
                Assert.AreEqual(-bottom, lo, 0.04, piece + " bottom");
                Assert.AreEqual(front, fore, 0.04, piece + " front");
                Assert.AreEqual(-back, aft, 0.04, piece + " back");
            }
            double half = RollerLayout.SegLength * 0.5;
            Fits(RollerLayout.PlatePiece, RollerLayout.PlateHalfWidth, RollerLayout.BackHeight, RollerLayout.PlateSkirt, half, half + RollerLayout.PlateOverlap);
            Fits(RollerLayout.SegPiece, RollerLayout.SegHalfWidth, 0.26, RollerLayout.BellyDepth, half + 0.015, half + 0.015);
            ModelPart leg = model.Part(RollerLayout.LegPiece);
            for (int i = 0; i < leg.VertexCount; i++)
            {
                Assert.Greater(leg.Positions[i * 3 + 2], -0.06f, "the leg reaches behind its hip");
                Assert.Less(Math.Abs(leg.Positions[i * 3]), 0.07f, "the leg is the same either side");
            }

            // As drawn: the head, the tail, seven segments under seven plates, fourteen legs.
            int Drawn(ModelFile m) => m.Part(RollerLayout.HeadPiece).TriangleCount + m.Part(RollerLayout.TailPiece).TriangleCount
                                      + RollerLayout.Segs * (m.Part(RollerLayout.SegPiece).TriangleCount + m.Part(RollerLayout.PlatePiece).TriangleCount)
                                      + RollerLayout.Legs * m.Part(RollerLayout.LegPiece).TriangleCount;
            int full = Drawn(model);
            Assert.LessOrEqual(full, 16000, "an enemy body's allowance");
            ModelFile far = Load(RollerLayout.Model + ModelFile.FarSuffix);
            Assert.AreEqual(model.TextureSet, far.TextureSet);
            Assert.AreEqual(model.Parts.Length, far.Parts.Length);
            Assert.LessOrEqual(Drawn(far), 4000, "the far copy");
            Assert.LessOrEqual(Drawn(far), full * 0.65, "the far copy is not much lighter than the full model");
        }
    }
}
