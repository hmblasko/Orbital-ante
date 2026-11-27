using System;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;
using OrbitalAnte.Look;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>
    /// The walkers (the alien creatures on eight legs): the Strider's body rules, walk and brain, what the Sprinter
    /// does differently, and each model against its layout.
    /// </summary>
    public class WalkerRulesTests
    {
        static readonly WalkerLayout _l = WalkerLayout.Strider;
        static readonly WalkerLayout Fast = WalkerLayout.Sprinter;

        static int Leg(int side, int place) => WalkerLayout.Limb(side, place);

        [Test]
        public void APieceThatBreaks_TakesEverythingBeyondIt_AndNothingNearerTheBody()
        {
            var body = new WalkerBody(_l);
            int leg = Leg(WalkerLayout.Right, 2);
            WalkerHit graze = body.HitLimb(leg, 1, body.MaxHp(leg, 1) - 1);
            Assert.AreEqual(0, graze.Losses.Count);
            Assert.AreEqual(3, body.Kept(leg));
            WalkerHit hit = body.HitLimb(leg, 1, 5);
            Assert.AreEqual(1, hit.Losses.Count);
            Assert.AreEqual(leg, hit.Losses[0].Limb);
            Assert.AreEqual(1, hit.Losses[0].FromSegment);
            Assert.AreEqual(1, body.Kept(leg), "the upper piece stays on the body");
            Assert.AreEqual(0, body.HitLimb(leg, 2, 999).Losses.Count, "a piece that is gone cannot be hit again");
            Assert.IsTrue(body.Alive);
        }

        [Test]
        public void LimbsAreLow_AFewRifleHitsTakeOneOff()
        {
            // Part B, Milestone 1: limbs come off in one to three rifle hits. The rifle's body hit is 24.
            WalkerHp hp = WalkerHp.Strider;
            foreach (double piece in new[] { hp.Upper, hp.Lower, hp.Tip, hp.Arm })
                Assert.LessOrEqual(Math.Ceiling(piece / 24.0), 3, "a limb piece needs more than three rifle hits");
            Assert.Greater(hp.Carapace, hp.Belly * 1.4, "the lid is armour; the belly is the way in");
            Assert.LessOrEqual(hp.Carapace, 400, "but a slow way, never a wall (constitution rule 3: no inflated health)");
        }

        [Test]
        public void ItStandsOnFiveLegs_AndGoesDownOnFour_OrWithOneSideNearlyBare()
        {
            var body = new WalkerBody(_l);
            Assert.AreEqual(WalkerStance.Standing, body.Stance);
            Assert.AreEqual(1.0, body.SpeedShare, 1e-9);
            Assert.AreEqual(1.0, body.HeightShare, 1e-9);
            double speed = body.SpeedShare, height = body.HeightShare;
            // Lose three, spread over both sides: still up, slower and lower each time.
            foreach (int leg in new[] { Leg(0, 1), Leg(1, 2), Leg(0, 4) })
            {
                body.HitLimb(leg, 0, 0, sever: true);
                Assert.AreEqual(WalkerStance.Standing, body.Stance);
                Assert.Less(body.SpeedShare, speed);
                Assert.Less(body.HeightShare, height);
                speed = body.SpeedShare;
                height = body.HeightShare;
            }
            Assert.AreEqual(5, body.Legs);
            WalkerHit last = body.HitLimb(Leg(1, 3), 0, 0, sever: true);
            Assert.IsTrue(last.StanceChanged);
            Assert.AreEqual(WalkerStance.Down, body.Stance);
            Assert.Greater(body.SpeedShare, 0.0, "it still drags itself along");
            Assert.Less(body.SpeedShare, 0.3);

            var lopsided = new WalkerBody(_l);
            for (int place = 1; place <= 3; place++) lopsided.HitLimb(Leg(WalkerLayout.Right, place), 0, 0, sever: true);
            Assert.AreEqual(5, lopsided.Legs);
            Assert.AreEqual(WalkerStance.Down, lopsided.Stance, "one leg on a side cannot hold it up");
        }

        [Test]
        public void ALegStandsOnItsStump_WhenOnlyTheTipIsGone_ButNotOnItsUpperPieceAlone()
        {
            var body = new WalkerBody(_l);
            body.HitLimb(Leg(0, 1), 2, 0, sever: true);
            Assert.IsTrue(body.CarriesWeight(Leg(0, 1)));
            body.HitLimb(Leg(0, 1), 1, 0, sever: true);
            Assert.IsFalse(body.CarriesWeight(Leg(0, 1)));
            Assert.IsFalse(body.CarriesWeight(WalkerLayout.Limb(0, 0)), "claw arms never carry weight");
        }

        [Test]
        public void OnlyTheBellyOrTheLid_KillsIt_AndThenEveryLimbIsThrownOff()
        {
            var body = new WalkerBody(_l);
            for (int limb = 0; limb < WalkerLayout.Limbs; limb++) body.HitLimb(limb, 1, 0, sever: true);
            Assert.IsTrue(body.Alive, "losing limbs never kills it");
            Assert.IsFalse(body.CanStrike(WalkerLayout.Right));
            WalkerHit hit = body.HitBody(belly: true, body.Max.Belly - 1);
            Assert.IsFalse(hit.Killed);
            hit = body.HitBody(belly: true, 2);
            Assert.IsTrue(hit.Killed);
            Assert.AreEqual(WalkerStance.Dead, body.Stance);
            Assert.AreEqual(WalkerLayout.Limbs, hit.Losses.Count, "what was left of every limb comes off");
            foreach (WalkerLoss loss in hit.Losses) Assert.IsTrue(loss.Burst);
            Assert.IsFalse(body.HitBody(belly: false, 50).Killed, "it cannot die twice");

            var armoured = new WalkerBody(_l);
            Assert.IsFalse(armoured.HitBody(belly: false, armoured.Max.Belly).Killed, "from above it takes far more");
            Assert.IsTrue(armoured.HitBody(belly: false, 0, lethal: true).Killed, "a one-hit-kill gun kills wherever it lands");
        }

        static WalkerPose Run(WalkerBody body, WalkerAnimator animator, double seconds, Vec3d velocity, double turn = 0, Action<double, WalkerPose> each = null)
        {
            var pose = new WalkerPose();
            const double dt = 1.0 / 60.0;
            for (double t = 0; t < seconds; t += dt)
            {
                animator.Update(body, new WalkerAnimInput { Velocity = velocity, TurnRate = turn, StrikeSide = -1 }, dt, pose);
                each?.Invoke(t, pose);
            }
            return pose;
        }

        static void AssertSound(WalkerBody body, WalkerPose pose)
        {
            Assert.IsFalse(double.IsNaN(pose.BodyPosition.Y) || double.IsNaN(pose.BodyRotation.W));
            for (int limb = 0; limb < WalkerLayout.Limbs; limb++)
            {
                for (int s = 0; s < body.Kept(limb); s++)
                {
                    int i = limb * WalkerLayout.Segments + s;
                    Assert.IsFalse(double.IsNaN(pose.Position[i].X + pose.Position[i].Y + pose.Position[i].Z), "limb " + limb + " piece " + s);
                    QuatD q = pose.Rotation[i];
                    Assert.AreEqual(1.0, q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W, 1e-6);
                }
            }
        }

        /// <summary>Where a leg's last piece ends, from the pose.</summary>
        static Vec3d EndOf(WalkerBody body, WalkerPose pose, int limb)
        {
            int last = body.Kept(limb) - 1;
            int i = limb * WalkerLayout.Segments + last;
            Vec3d along = pose.Rotation[i].Rotate(Vec3d.Forward);
            return pose.Position[i] + along * _l.Length(limb, last);
        }

        [Test]
        public void StandingStill_EveryFootIsOnTheGround_AndEveryLegReachesItsFoot()
        {
            var body = new WalkerBody(_l);
            var animator = new WalkerAnimator(_l);
            WalkerPose pose = Run(body, animator, 3.0, Vec3d.Zero);
            AssertSound(body, pose);
            Assert.AreEqual(_l.StandHeight, pose.BodyPosition.Y, 0.12);
            for (int limb = 0; limb < WalkerLayout.Limbs; limb++)
            {
                if (WalkerLayout.IsArm(limb)) continue;
                Assert.IsTrue(pose.Planted[limb]);
                Assert.AreEqual(0.0, pose.End[limb].Y, 1e-6);
                // The modelled tip curves a little, so its straight length lands within a hand of the foot.
                Assert.Less(Vec3d.Distance(EndOf(body, pose, limb), pose.End[limb]), 0.12, "leg " + limb + " does not reach its foot");
                // The knee is the highest part of the leg, above the body: the spider crab's stance.
                Assert.Greater(pose.Position[limb * 3 + 1].Y, pose.BodyPosition.Y);
            }
        }

        [Test]
        public void Walking_FeetOnTheGroundStayPutInTheWorld_AndNeverTooManyAreInTheAir()
        {
            var body = new WalkerBody(_l);
            var animator = new WalkerAnimator(_l);
            var velocity = new Vec3d(0, 0, 1.6);
            var last = new Vec3d[WalkerLayout.Limbs];
            var wasDown = new bool[WalkerLayout.Limbs];
            int steps = 0, most = 0;
            const double dt = 1.0 / 60.0;
            Run(body, animator, 8.0, velocity, 0, (t, pose) =>
            {
                most = Math.Max(most, animator.Swinging);
                for (int limb = 0; limb < WalkerLayout.Limbs; limb++)
                {
                    if (WalkerLayout.IsArm(limb)) continue;
                    if (pose.Planted[limb] && wasDown[limb])
                    {
                        // In walker space a foot on the ground goes backward exactly as fast as the body goes forward.
                        Vec3d moved = pose.End[limb] - last[limb];
                        Assert.AreEqual(-velocity.Z * dt, moved.Z, 1e-6);
                        Assert.AreEqual(0.0, moved.X, 1e-6);
                    }
                    if (wasDown[limb] && !pose.Planted[limb]) steps++;
                    wasDown[limb] = pose.Planted[limb];
                    last[limb] = pose.End[limb];
                    Assert.Less(Vec3d.Distance(pose.End[limb], _l.FootHome(limb)), 2.2, "a foot was left behind");
                }
                AssertSound(body, pose);
            });
            Assert.Greater(steps, 24, "it has to keep stepping to keep up");
            Assert.LessOrEqual(most, 3, "a careful walk: most of its feet stay down");
            Assert.GreaterOrEqual(most, 2);
        }

        [Test]
        public void LosingLegs_ItSettlesLower_LeansOverTheLegsItHasLeft_AndGoesDownOnItsBelly()
        {
            var body = new WalkerBody(_l);
            var animator = new WalkerAnimator(_l);
            WalkerPose whole = Run(body, animator, 2.0, Vec3d.Zero);
            double full = whole.BodyPosition.Y;
            body.HitLimb(Leg(WalkerLayout.Right, 1), 0, 0, sever: true);
            body.HitLimb(Leg(WalkerLayout.Right, 3), 1, 0, sever: true);
            WalkerPose hurt = Run(body, animator, 3.0, new Vec3d(0, 0, 0.8));
            AssertSound(body, hurt);
            Assert.Less(hurt.BodyPosition.Y, full - 0.15);
            Assert.Greater(hurt.BodyPosition.Y, _l.BellyHeight + 0.5);
            Vec3d rightSide = hurt.BodyRotation.Rotate(Vec3d.Right);
            Assert.Less(rightSide.Y, -0.02, "it sags toward the side that lost legs");
            Assert.Less(hurt.BodyPosition.X, -0.02, "and shifts over the legs it has left");

            body.HitLimb(Leg(WalkerLayout.Left, 2), 0, 0, sever: true);
            body.HitLimb(Leg(WalkerLayout.Left, 4), 0, 0, sever: true);
            Assert.AreEqual(WalkerStance.Down, body.Stance);
            WalkerPose down = Run(body, animator, 3.0, new Vec3d(0, 0, 0.2));
            AssertSound(body, down);
            Assert.AreEqual(_l.BellyHeight, down.BodyPosition.Y, 0.05);
        }

        [Test]
        public void AStrike_RaisesTheClawFirst_ThenPutsItsPointWhereItWasThrown()
        {
            var body = new WalkerBody(_l);
            var animator = new WalkerAnimator(_l);
            var pose = Run(body, animator, 1.0, Vec3d.Zero);
            int arm = WalkerLayout.Limb(WalkerLayout.Right, 0);
            double restHeight = pose.End[arm].Y;
            var at = new Vec3d(0.4, 1.3, 3.2);
            const double dt = 1.0 / 60.0;
            double highest = 0, nearest = double.MaxValue;
            for (double t = 0; t <= 1.0; t += dt / 1.6)
            {
                animator.Update(body, new WalkerAnimInput { StrikeSide = WalkerLayout.Right, StrikeT = t, StrikeAt = at }, dt, pose);
                if (t < WalkerAnimator.StrikeStarts) highest = Math.Max(highest, pose.End[arm].Y);
                if (t >= WalkerAnimator.StrikeLands && t < 0.76) nearest = Math.Min(nearest, Vec3d.Distance(EndOf(body, pose, arm), at));
                AssertSound(body, pose);
            }
            Assert.Greater(highest, restHeight + 1.0, "the raise is the warning: it must be seen");
            Assert.Less(nearest, 0.25, "the claw's point arrives where it was thrown");
            Assert.Greater(WalkerAnimator.StrikeStarts * 1.6, 0.6, "the warning lasts long enough to step aside");
        }

        [Test]
        public void ManyLossesInAnyOrder_NeverBreakThePose()
        {
            var rng = new SeededRandom(1234);
            for (int run = 0; run < 12; run++)
            {
                var body = new WalkerBody(_l);
                var animator = new WalkerAnimator(_l);
                for (int loss = 0; loss < 14; loss++)
                {
                    int limb = rng.Range(0, WalkerLayout.Limbs), segment = rng.Range(0, WalkerLayout.Segments);
                    WalkerHit hit = body.HitLimb(limb, segment, 0, sever: true);
                    if (hit.Losses.Count > 0) animator.Flinch(new Vec3d(1, 1, 0));
                    WalkerPose pose = Run(body, animator, 0.7, new Vec3d(0.3, 0, 1.2 * body.SpeedShare), 0.4);
                    AssertSound(body, pose);
                    Assert.GreaterOrEqual(pose.BodyPosition.Y, _l.BellyHeight - 1e-6, "the body never sinks into the ground");
                    Assert.Less(pose.BodyPosition.Y, _l.StandHeight + 1.0);
                }
            }
        }

        // ---- what it decides

        static WalkerSenses Sees(double x, double z) => new WalkerSenses { SeesPlayer = true, Player = new Vec3d(x, 0, z), Chest = new Vec3d(x, 1.3, z) };

        [Test]
        public void SeeingThePlayer_ItTurnsToThem_WalksUp_AndStopsAtArmsLength()
        {
            var rules = new WalkerRules();
            var body = new WalkerBody(_l);
            var brain = new WalkerBrain(_l, rules, 1);
            WalkerOrders far = brain.Think(body, Sees(2, 20), 0.02);
            Assert.Greater(far.Velocity.Z, 2.0);
            Assert.Greater(far.TurnRate, 0.0, "the player is to its right");
            Assert.LessOrEqual(far.Velocity.Z, rules.pace);
            WalkerOrders behind = brain.Think(body, Sees(-3, -20), 0.02);
            Assert.AreEqual(0.0, behind.Velocity.Z, "it turns round before it walks");
            Assert.AreEqual(-rules.turnRate, behind.TurnRate, 1e-9);
            body.HitLimb(WalkerLayout.Limb(0, 1), 0, 0, sever: true);
            body.HitLimb(WalkerLayout.Limb(1, 2), 0, 0, sever: true);
            Assert.Less(brain.Think(body, Sees(0, 20), 0.02).Velocity.Z, far.Velocity.Z, "fewer legs, slower");
        }

        [Test]
        public void AStrike_FollowsThePlayerOnlyAtFirst_SoAStepAsideIsAMiss_AndLandsOnce()
        {
            var rules = new WalkerRules();
            var body = new WalkerBody(_l);
            var brain = new WalkerBrain(_l, rules, 1);
            WalkerOrders start = brain.Think(body, Sees(0.6, 3.6), 0.02);
            Assert.AreEqual(WalkerLayout.Right, start.StrikeSide, "the claw on the player's side");
            int landed = 0;
            Vec3d locked = Vec3d.Zero;
            for (double t = 0; t < rules.strikeSeconds + 0.1; t += 0.02)
            {
                // The player steps aside half way through the warning.
                bool stepped = t > rules.strikeSeconds * 0.4;
                WalkerOrders o = brain.Think(body, stepped ? Sees(2.4, 3.6) : Sees(0.6, 3.6), 0.02);
                if (o.StrikeSide < 0) break;
                Assert.AreEqual(0.0, o.Velocity.Z, "it stands still to strike");
                Assert.AreEqual(0.0, o.TurnRate);
                if (o.Landed) { landed++; locked = o.StrikeAt; }
            }
            Assert.AreEqual(1, landed);
            Assert.AreEqual(0.6, locked.X, 1e-6, "it lands where the player was when the claw was committed");
            Assert.Greater(Vec3d.Distance(locked, new Vec3d(2.4, 1.3, 3.6)), rules.strikeRadius, "the step aside was far enough");
            // The warning: from the lock to the landing there is time to move.
            Assert.Greater((WalkerAnimator.StrikeLands - WalkerBrain.LockAt) * rules.strikeSeconds, 0.5);
            Assert.IsFalse(brain.Striking);
            Assert.Less(brain.Think(body, Sees(0.6, 3.6), 0.02).StrikeSide, 0, "it rests between strikes");
        }

        [Test]
        public void WithOneClaw_ItUsesTheOther_AndWithNone_ItBacksAway()
        {
            var rules = new WalkerRules();
            var body = new WalkerBody(_l);
            body.HitLimb(WalkerLayout.Limb(WalkerLayout.Right, 0), 1, 0, sever: true);
            var brain = new WalkerBrain(_l, rules, 1);
            Assert.AreEqual(WalkerLayout.Left, brain.Think(body, Sees(0.6, 3.6), 0.02).StrikeSide);
            body.HitLimb(WalkerLayout.Limb(WalkerLayout.Left, 0), 2, 0, sever: true);
            WalkerOrders cut = brain.Think(body, Sees(0.6, 3.6), 0.02);
            Assert.Less(cut.StrikeSide, 0, "a claw shot off in mid swing ends the swing");
            WalkerOrders flee = brain.Think(body, Sees(0.0, 6.0), 0.02);
            Assert.IsTrue(brain.Fleeing);
            Assert.AreNotEqual(0.0, flee.TurnRate);
            WalkerOrders going = brain.Think(body, Sees(0.0, -6.0), 0.02);
            Assert.Greater(going.Velocity.Z, 0.0, "with the player behind it, it walks away");
            Assert.AreEqual(0.0, brain.Think(body, Sees(0.0, -(rules.fleeTo + 1)), 0.02).Velocity.Z, "far enough: it stops");
        }

        [Test]
        public void Alone_ItWandersNearHome_AndTheSameSeedWandersTheSameWay()
        {
            var rules = new WalkerRules();
            double Walked(int seed, out double x, out double z)
            {
                var body = new WalkerBody(_l);
                var brain = new WalkerBrain(_l, rules, seed);
                double yaw = 0, far = 0;
                x = 0; z = 0;
                for (int i = 0; i < 60 * 240; i++)
                {
                    const double dt = 1.0 / 60.0;
                    // Home in its own space: the world's origin turned by its facing.
                    double c = Math.Cos(-yaw), s = Math.Sin(-yaw);
                    var home = new Vec3d(-(x * c + z * s), 0, -(-x * s + z * c));
                    WalkerOrders o = brain.Think(body, new WalkerSenses { Home = home }, dt);
                    Assert.Less(o.StrikeSide, 0);
                    yaw += o.TurnRate * dt;
                    x += Math.Sin(yaw) * o.Velocity.Z * dt;
                    z += Math.Cos(yaw) * o.Velocity.Z * dt;
                    far = Math.Max(far, Math.Sqrt(x * x + z * z));
                }
                return far;
            }
            double first = Walked(7, out double x1, out double z1);
            Assert.Greater(first, 3.0, "it does go somewhere");
            Assert.Less(first, rules.wanderRadius + 8.0, "it comes back when it strays");
            Walked(7, out double x2, out double z2);
            Assert.AreEqual(x1, x2, 1e-9);
            Assert.AreEqual(z1, z2, 1e-9);
        }

        // ---- the Sprinter (the camel spider): what it does differently

        [Test]
        public void TheSprinter_IsLowLightAndQuick_AndStillFair()
        {
            WalkerRules rules = WalkerRules.ForSprinter(), slow = new WalkerRules();
            Assert.Greater(rules.pace, 5.0f, "it cannot be walked away from (the player walks at 5)");
            Assert.Less(rules.pace, 8.0f, "but it can be outrun (the player sprints at 8)");
            Assert.Less(Fast.StandHeight, 0.4 * _l.StandHeight, "it is a low animal");
            WalkerHp hp = rules.hp;
            foreach (double piece in new[] { hp.Upper, hp.Lower, hp.Tip, hp.Arm })
                Assert.LessOrEqual(Math.Ceiling(piece / 24.0), 2, "its limbs come off in one or two rifle hits");
            Assert.LessOrEqual(Math.Ceiling(hp.Jaw / 24.0), 2, "taking its jaws must cost less than killing it");
            Assert.Less(2 * Math.Ceiling(hp.Jaw / 24.0), Math.Ceiling(hp.Belly / 24.0));
            Assert.Less(hp.Belly, slow.hp.Belly);
            Assert.Less(rules.strikeDamage, slow.strikeDamage, "quick and often, not heavy");
            // The warning: from the moment the bite is aimed to the moment it lands there is time to step aside.
            Assert.Greater((WalkerAnimator.StrikeLands - WalkerBrain.LockAt) * rules.strikeSeconds, 0.35);
            Assert.Greater(5.0 * (WalkerAnimator.StrikeLands - WalkerBrain.LockAt) * rules.strikeSeconds, rules.strikeRadius + 0.6,
                "walking sideways from the lock carries the player clear of the bite");
        }

        [Test]
        public void TheSprinter_RunsWithItsFeetKeepingUp_HalfOfThemInTheAirAtMost()
        {
            var body = new WalkerBody(Fast);
            var animator = new WalkerAnimator(Fast);
            WalkerPose still = Run(body, animator, 2.0, Vec3d.Zero);
            AssertSound(body, still);
            Assert.AreEqual(Fast.StandHeight, still.BodyPosition.Y, 0.06);
            for (int limb = 0; limb < WalkerLayout.Limbs; limb++)
            {
                if (WalkerLayout.IsArm(limb)) continue;
                Assert.IsTrue(still.Planted[limb]);
                int last = limb * WalkerLayout.Segments + 2;
                Vec3d end = still.Position[last] + still.Rotation[last].Rotate(Vec3d.Forward) * Fast.Length(limb, 2);
                Assert.Less(Vec3d.Distance(end, still.End[limb]), 0.08, "leg " + limb + " does not reach its foot");
                Assert.Greater(still.Position[limb * 3 + 1].Y, still.BodyPosition.Y, "its knees stand above its back");
            }
            double pace = WalkerRules.ForSprinter().pace;
            int most = 0, steps = 0;
            var wasDown = new bool[WalkerLayout.Limbs];
            double worst = 0;
            Run(body, animator, 4.0, new Vec3d(0, 0, pace), 0, (t, pose) =>
            {
                most = Math.Max(most, animator.Swinging);
                for (int limb = 0; limb < WalkerLayout.Limbs; limb++)
                {
                    if (WalkerLayout.IsArm(limb)) continue;
                    if (wasDown[limb] && !pose.Planted[limb]) steps++;
                    wasDown[limb] = pose.Planted[limb];
                    if (t > 0.5) worst = Math.Max(worst, Vec3d.Distance(pose.End[limb], Fast.FootHome(limb)));
                }
                AssertSound(body, pose);
            });
            Assert.LessOrEqual(most, 4, "never more than half its feet in the air");
            Assert.Greater(steps, 8 * 4 * 4, "at a run every leg steps several times a second");
            Assert.Less(worst, 1.0, "a foot was dragged behind: the legs cannot keep up with this pace");
        }

        [Test]
        public void TheSprinter_DashesAndStops_OnTheWayIn_AndNeverStopsOnceItIsClose()
        {
            WalkerRules rules = WalkerRules.ForSprinter();
            var body = new WalkerBody(Fast);
            var brain = new WalkerBrain(Fast, rules, 3);
            double moving = 0, stopped = 0, longestStop = 0, stop = 0;
            double gap = 40.0;
            for (int i = 0; i < 60 * 30; i++)
            {
                // It closes on someone standing still; each time it gets near, they are put far off again.
                WalkerOrders o = brain.Think(body, new WalkerSenses { SeesPlayer = true, Player = new Vec3d(0, 0, gap), Chest = new Vec3d(0, 1.3, gap) }, 1.0 / 60.0);
                gap -= o.Velocity.Z / 60.0;
                if (gap < rules.pauseBeyond + 1.0) gap = 40.0;
                if (o.Velocity.Z > 0.1) { moving += 1.0 / 60.0; stop = 0; }
                else { stopped += 1.0 / 60.0; stop += 1.0 / 60.0; longestStop = Math.Max(longestStop, stop); }
            }
            Assert.Greater(stopped, 4.0, "it stops between dashes: that is the time to shoot");
            Assert.Greater(moving, 2.0 * stopped, "but it is mostly running");
            Assert.Less(longestStop, 1.0);
            var close = new WalkerBrain(Fast, rules, 3);
            for (int i = 0; i < 60 * 5; i++)
            {
                WalkerOrders o = close.Think(body, new WalkerSenses { SeesPlayer = true, Player = new Vec3d(0, 0, rules.pauseBeyond - 1.0), Chest = new Vec3d(0, 1.3, rules.pauseBeyond - 1.0) }, 1.0 / 60.0);
                Assert.IsFalse(close.Pausing);
                Assert.Greater(o.Velocity.Z, 0.1, "this close it comes straight in");
            }
            Assert.IsFalse(new WalkerBrain(_l, new WalkerRules(), 3).Pausing, "the Strider never dashes");
        }

        [Test]
        public void TheSprinter_CatchesSomeoneWalkingAway_ButNotSomeoneSprinting()
        {
            WalkerRules rules = WalkerRules.ForSprinter();
            double Chase(double playerSpeed, out bool bitten)
            {
                var body = new WalkerBody(Fast);
                var brain = new WalkerBrain(Fast, rules, 5);
                double gap = 10.0;
                bitten = false;
                const double dt = 1.0 / 60.0;
                for (double t = 0; t < 12.0; t += dt)
                {
                    WalkerOrders o = brain.Think(body, new WalkerSenses { SeesPlayer = true, Player = new Vec3d(0, 0, gap), Chest = new Vec3d(0, 1.3, gap) }, dt);
                    if (o.StrikeSide >= 0) { bitten = true; return t; }
                    gap += (playerSpeed - o.Velocity.Z) * dt;
                }
                return 12.0;
            }
            double walked = Chase(5.0, out bool caught);
            Assert.IsTrue(caught, "walking away at 5 does not shake it off");
            Assert.Less(walked, 10.0);
            Chase(8.0, out bool outrun);
            Assert.IsFalse(outrun, "a sprint does");
        }

        [Test]
        public void TheSprinter_PaysForWhatItLoses_HalfABiteOnOneJaw_AndABlindBiteWithNoFeelers()
        {
            WalkerRules rules = WalkerRules.ForSprinter();
            var body = new WalkerBody(Fast, rules.hp);
            var brain = new WalkerBrain(Fast, rules, 1);
            Assert.AreEqual(1.0, body.StrikeShare);
            Assert.AreEqual(WalkerBrain.LockAt, brain.LockShare(body));
            body.HitJaw(1, 0, sever: true);
            Assert.AreEqual(0.5, body.StrikeShare);
            body.HitLimb(WalkerLayout.Limb(0, 0), 2, 0, sever: true);
            Assert.AreEqual(WalkerBrain.LockAt, brain.LockShare(body), "one feeler still finds you");
            body.HitLimb(WalkerLayout.Limb(1, 0), 0, 0, sever: true);
            Assert.AreEqual(0.0, brain.LockShare(body));
            // With no feelers the bite goes where the player stood when it began, however early they move.
            WalkerOrders o = brain.Think(body, Sees(0.3, 2.0), 0.02);
            Vec3d first = o.StrikeAt;
            o = brain.Think(body, Sees(-1.5, 2.0), 0.02);
            Assert.AreEqual(first.X, o.StrikeAt.X, 1e-9);
            var strider = new WalkerBody(_l);
            Assert.AreEqual(1.0, strider.StrikeShare);
            Assert.AreEqual(WalkerBrain.LockAt, new WalkerBrain(_l, new WalkerRules(), 1).LockShare(strider));
        }

        [Test]
        public void TheSprintersBite_IsALungeOfTheWholeBody_ThatPutsItsJawsWhereItWasAimed()
        {
            WalkerRules rules = WalkerRules.ForSprinter();
            var body = new WalkerBody(Fast);
            var brain = new WalkerBrain(Fast, rules, 1);
            var animator = new WalkerAnimator(Fast);
            WalkerPose pose = Run(body, animator, 1.0, Vec3d.Zero);
            Vec3d rest = Fast.BiteRest(0);
            double palpRest = pose.End[WalkerLayout.Limb(WalkerLayout.Right, 0)].Y;
            var player = new WalkerSenses { SeesPlayer = true, Player = new Vec3d(0.3, 0, 2.0), Chest = new Vec3d(0.3, 1.3, 2.0) };
            WalkerOrders o = brain.Think(body, player, 1.0 / 60.0);
            Assert.GreaterOrEqual(o.StrikeSide, 0, "in range and in front: it bites");
            Assert.LessOrEqual(new Vec3d(o.StrikeAt.X - rest.X, 0, o.StrikeAt.Z - rest.Z).Magnitude, Fast.StrikeShift + 1e-9, "no further than it can throw itself");
            double furthest = double.MinValue, back = double.MaxValue, raised = 0, openWide = 0, shut = 1;
            int landed = 0;
            Vec3d at = o.StrikeAt;
            for (int i = 0; i < 200 && o.StrikeSide >= 0; i++)
            {
                animator.Update(body, new WalkerAnimInput { StrikeSide = o.StrikeSide, StrikeT = o.StrikeT, StrikeAt = o.StrikeAt }, 1.0 / 60.0, pose);
                AssertSound(body, pose);
                // How far the lower finger hangs open under the jaw.
                double gape = Math.Acos(Math.Max(-1.0, Math.Min(1.0, Vec3d.Dot(pose.JawRotation[0].Rotate(Vec3d.Forward), pose.FingerRotation[0].Rotate(Vec3d.Forward)))));
                if (o.StrikeT < WalkerAnimator.StrikeStarts)
                {
                    back = Math.Min(back, pose.BodyPosition.Z);
                    raised = Math.Max(raised, pose.End[WalkerLayout.Limb(WalkerLayout.Right, 0)].Y);
                    openWide = Math.Max(openWide, gape);
                }
                if (o.StrikeT >= WalkerAnimator.StrikeLands && o.StrikeT < 0.76)
                {
                    furthest = Math.Max(furthest, pose.BodyPosition.Z);
                    shut = Math.Min(shut, gape);
                    Vec3d tip = pose.JawPosition[0] + pose.JawRotation[0].Rotate(Vec3d.Forward) * Fast.JawLength;
                    Assert.Less(new Vec3d(tip.X - Fast.JawBase.X - at.X, 0, tip.Z - at.Z).Magnitude, 0.25, "the jaws arrive where the bite was aimed");
                }
                if (o.Landed) landed++;
                o = brain.Think(body, player, 1.0 / 60.0);
            }
            Assert.AreEqual(1, landed);
            Assert.Less(back, -0.15, "it draws back first: with the raised arms and the open jaws, that is the warning");
            Assert.Greater(raised, palpRest + 0.3, "both feeling arms go up");
            Assert.Greater(openWide, 0.7);
            Assert.Less(shut, 0.1, "the jaws shut as it lands");
            Assert.Greater(furthest, 0.6, "then the whole body is thrown forward");
        }

        [Test]
        public void TheSprintersJaws_ShootOff_AndWithNeitherItRunsAway()
        {
            WalkerRules rules = WalkerRules.ForSprinter();
            var body = new WalkerBody(Fast, rules.hp);
            Assert.IsTrue(body.JawOn(0) && body.JawOn(1));
            Assert.AreEqual(0, body.HitJaw(0, rules.hp.Jaw - 1).Losses.Count);
            WalkerHit hit = body.HitJaw(0, 5);
            Assert.AreEqual(1, hit.Losses.Count);
            Assert.AreEqual(WalkerBody.JawLimb(0), hit.Losses[0].Limb);
            Assert.IsFalse(hit.Killed, "a jaw never kills");
            Assert.AreEqual(0, body.HitJaw(0, 50).Losses.Count, "it cannot come off twice");
            Assert.IsTrue(body.CanStrike(WalkerLayout.Right), "one jaw still bites, whichever side");
            // Its feeling arms are not what it bites with.
            body.HitLimb(WalkerLayout.Limb(0, 0), 0, 0, sever: true);
            body.HitLimb(WalkerLayout.Limb(1, 0), 0, 0, sever: true);
            Assert.IsTrue(body.CanStrike(WalkerLayout.Left));
            var brain = new WalkerBrain(Fast, rules, 1);
            Assert.GreaterOrEqual(brain.Think(body, Sees(0.2, 2.0), 0.02).StrikeSide, 0);
            body.HitJaw(1, 0, sever: true);
            Assert.IsFalse(body.CanStrike(WalkerLayout.Right));
            Assert.Less(brain.Think(body, Sees(0.2, 2.0), 0.02).StrikeSide, 0, "a bite with no jaws left is called off");
            brain.Think(body, Sees(0.0, 6.0), 0.02);
            Assert.IsTrue(brain.Fleeing);
            // Death throws off whatever jaws are left, like the limbs.
            var whole = new WalkerBody(Fast, rules.hp);
            WalkerHit death = whole.HitBody(belly: true, rules.hp.Belly);
            Assert.IsTrue(death.Killed);
            Assert.AreEqual(WalkerLayout.Limbs + 2, death.Losses.Count);
            Assert.IsFalse(new WalkerBody(_l).JawOn(0), "the Strider has no jaws");
        }

        [Test]
        public void EveryKind_SurvivesAnyLossesInAnyOrder_WhileItRunsTurnsAndBites()
        {
            var rng = new SeededRandom(99);
            for (int run = 0; run < 10; run++)
            {
                var body = new WalkerBody(Fast);
                var animator = new WalkerAnimator(Fast);
                var pose = new WalkerPose();
                for (int loss = 0; loss < 14; loss++)
                {
                    int limb = rng.Range(0, WalkerLayout.Limbs + 2), segment = rng.Range(0, WalkerLayout.Segments);
                    if (limb >= WalkerLayout.Limbs) body.HitJaw(limb - WalkerLayout.Limbs, 0, sever: true);
                    else body.HitLimb(limb, segment, 0, sever: true);
                    for (int i = 0; i < 40; i++)
                    {
                        animator.Update(body, new WalkerAnimInput
                        {
                            Velocity = new Vec3d(0.4, 0, 6.2 * body.SpeedShare), TurnRate = 1.5,
                            StrikeSide = i > 10 ? 0 : -1, StrikeT = (i - 10) / 30.0, StrikeAt = new Vec3d(0.3, 0.6, 1.8),
                        }, 1.0 / 60.0, pose);
                        AssertSound(body, pose);
                        Assert.GreaterOrEqual(pose.BodyPosition.Y, Fast.BellyHeight - 1e-6);
                        Assert.Less(pose.BodyPosition.Y, Fast.StandHeight + 0.5);
                    }
                }
            }
        }

        // ---- the model

        static string ModelsFolder()
        {
            string env = Environment.GetEnvironmentVariable("OA_MODELS");
            if (!string.IsNullOrEmpty(env) && Directory.Exists(env)) return env;
            string dir = TestContext.CurrentContext.TestDirectory;
            for (int i = 0; i < 10 && dir != null; i++)
            {
                foreach (string candidate in new[] { Path.Combine(dir, "unity", "Assets", "OrbitalAnte", "Resources", "Models"), Path.Combine(dir, "Assets", "OrbitalAnte", "Resources", "Models") })
                    if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        static ModelFile Load(string id)
        {
            string folder = ModelsFolder();
            if (folder == null) Assert.Ignore("Resources/Models not found");
            string path = Path.Combine(folder, id + ".bytes");
            if (!File.Exists(path)) Assert.Ignore("this model is not in the game yet: " + path);
            ModelFile model = ModelFile.Parse(File.ReadAllBytes(path), out string problem);
            Assert.IsNotNull(model, problem);
            return model;
        }

        static void AssertSocket(ModelFile model, string name, Vec3d expected)
        {
            Assert.IsTrue(model.TrySocket(name, out ModelSocket s), "missing " + name);
            Assert.AreEqual(expected.X, s.X, 0.002, name + " x");
            Assert.AreEqual(expected.Y, s.Y, 0.002, name + " y");
            Assert.AreEqual(expected.Z, s.Z, 0.002, name + " z");
        }

        [Test]
        public void StriderModelFile_MatchesTheLayout() => ModelMatches(WalkerLayout.Strider);

        [Test]
        public void SprinterModelFile_MatchesTheLayout() => ModelMatches(WalkerLayout.Sprinter);

        static void ModelMatches(WalkerLayout k)
        {
            // tools/blender/build_creatures.py carries its own copy of each WalkerLayout. If either changes, this
            // fails until the script is changed to match and run again.
            ModelFile model = Load(k.Model);
            for (int limb = 0; limb < WalkerLayout.Limbs; limb++)
            {
                string tag = (WalkerLayout.SideOf(limb) == WalkerLayout.Right ? "R" : "L") + WalkerLayout.PlaceOf(limb);
                AssertSocket(model, "Hip" + tag, k.HipModelled(limb));
                AssertSocket(model, "Out" + tag, k.HipModelled(limb) + k.Out(limb));
                for (int s = 0; s < WalkerLayout.Segments; s++) Assert.IsNotNull(model.Part(k.Piece(limb, s)), "missing piece " + k.Piece(limb, s));
            }
            ModelSocket end;
            for (int s = 0; s < WalkerLayout.Segments; s++)
            {
                Assert.IsTrue(model.TrySocket(k.LegPieces[s] + "End", out end), k.LegPieces[s] + "End");
                Assert.AreEqual(k.Leg[s], end.Z, 0.002);
                Assert.IsTrue(model.TrySocket(k.ArmPieces[s] + "End", out end), k.ArmPieces[s] + "End");
                Assert.AreEqual(k.Arm[s], end.Z, 0.002);
            }
            Assert.IsTrue(model.TrySocket("FingerEnd", out end));
            Assert.AreEqual(k.Finger, end.Z, 0.002);
            AssertSocket(model, k.HingeSocket, k.FingerHinge);
            Assert.IsTrue(model.TrySocket("StumpRim", out end));
            Assert.AreEqual(k.StumpRadius, end.X, 0.0005);
            foreach (string piece in new[] { k.Armoured, k.Soft }) Assert.IsNotNull(model.Part(piece), "missing piece " + piece);
            foreach (string piece in k.Meat) Assert.IsNotNull(model.Part(piece), "missing piece " + piece);
            Assert.IsNotNull(model.Part(k.Stump));
            Assert.IsNotNull(model.Part(k.FingerPiece));
            Assert.IsTrue(model.Textured, "the " + k.Model + " is drawn with pictures");
            if (k.HasJaws)
            {
                Assert.IsNotNull(model.Part(k.JawPiece));
                AssertSocket(model, "JawR", k.JawAt(WalkerLayout.Right));
                Assert.IsTrue(model.TrySocket("JawEnd", out end));
                Assert.AreEqual(k.JawLength, end.Z, 0.002);
            }

            // As drawn: the body, eight legs of three pieces, two arms of three, and the fingers (and jaws).
            int Triangles(ModelFile m, string piece) => m.Part(piece).TriangleCount;
            int Drawn(ModelFile m) => Triangles(m, k.Armoured) + Triangles(m, k.Soft)
                                      + 8 * (Triangles(m, k.LegPieces[0]) + Triangles(m, k.LegPieces[1]) + Triangles(m, k.LegPieces[2]))
                                      + 2 * (Triangles(m, k.ArmPieces[0]) + Triangles(m, k.ArmPieces[1]) + Triangles(m, k.ArmPieces[2]) + Triangles(m, k.FingerPiece))
                                      + (k.HasJaws ? 2 * Triangles(m, k.JawPiece) : 0);
            int full = Drawn(model);
            Assert.LessOrEqual(full, 22000, "an enemy body's allowance (the trooper's)");
            ModelFile far = Load(k.Model + ModelFile.FarSuffix);
            Assert.AreEqual(model.TextureSet, far.TextureSet);
            Assert.AreEqual(model.Parts.Length, far.Parts.Length);
            Assert.LessOrEqual(Drawn(far), full * 0.45, "the far copy is not much lighter than the full model");
            // Every piece is modelled from its own joint, along +Z.
            foreach (string piece in new[] { k.LegPieces[0], k.LegPieces[1], k.LegPieces[2], k.ArmPieces[0], k.ArmPieces[1], k.ArmPieces[2], k.FingerPiece })
            {
                ModelPart part = model.Part(piece);
                float minZ = float.MaxValue, maxZ = float.MinValue, side = 0;
                for (int i = 0; i < part.VertexCount; i++)
                {
                    minZ = Math.Min(minZ, part.Positions[i * 3 + 2]);
                    maxZ = Math.Max(maxZ, part.Positions[i * 3 + 2]);
                    side = Math.Max(side, Math.Abs(part.Positions[i * 3]));
                }
                Assert.Greater(minZ, -0.12f, piece + " reaches behind its joint");
                Assert.Greater(maxZ, 0.25f, piece + " does not run along +Z");
                Assert.Less(side, 0.2f, piece + " is not a limb along +Z");
            }
        }
    }
}
