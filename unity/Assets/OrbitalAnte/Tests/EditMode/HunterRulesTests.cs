using System;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;
using OrbitalAnte.Look;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>The Cave hunter (the giant centipede): cutting it in two, what kills it, the rear and the strike, the chain that follows its head.</summary>
    public class HunterRulesTests
    {
        const double Dt = 1.0 / 60.0;
        const double Rifle = 24.0;

        static void AssertSound(HunterPose pose)
        {
            for (int i = 0; i < pose.Nodes; i++)
            {
                Vec3d p = pose.NodePosition[i];
                QuatD q = pose.NodeRotation[i];
                Assert.IsFalse(double.IsNaN(p.X + p.Y + p.Z + q.X + q.Y + q.Z + q.W) || double.IsInfinity(p.X + p.Y + p.Z), "node " + i);
                Assert.AreEqual(1.0, Math.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W), 1e-6);
            }
            for (int i = 0; i < (pose.Nodes - 1) * 2; i++)
            {
                Vec3d p = pose.LegPosition[i];
                QuatD q = pose.LegRotation[i];
                Assert.IsFalse(double.IsNaN(p.X + p.Y + p.Z + q.X + q.Y + q.Z + q.W), "leg " + i);
            }
        }

        static HunterPose Run(HunterAnimator animator, HunterBody body, HunterAnimInput input, double seconds, HunterPose pose = null)
        {
            pose = pose ?? new HunterPose();
            for (double t = 0; t < seconds; t += Dt) animator.Update(body, input, Dt, pose);
            return pose;
        }

        // ---- the body

        [Test]
        public void ASegment_IsCutThroughInThreeRifleHits_AndALegInOne()
        {
            var body = new HunterBody();
            Assert.AreEqual(-1, body.HitSeg(8, Rifle).FromSeg);
            Assert.AreEqual(-1, body.HitSeg(8, Rifle).FromSeg);
            HunterHit hit = body.HitSeg(8, Rifle);
            Assert.AreEqual(8, hit.FromSeg);
            Assert.AreEqual(HunterLayout.Segs, hit.HadSegs);
            Assert.AreEqual(8, body.Kept);
            Assert.IsTrue(body.Alive, "a cut never kills");

            Assert.AreEqual(2, body.HitLeg(2, Rifle).LegLost);
            Assert.IsFalse(body.LegOn(2));
        }

        [Test]
        public void ACut_TakesEverythingBehindIt_AndAHitOnWhatIsGoneIsNothing()
        {
            var body = new HunterBody();
            body.HitSeg(10, 999);
            Assert.IsFalse(body.SegOn(10));
            Assert.IsFalse(body.SegOn(15));
            Assert.IsTrue(body.SegOn(9));
            Assert.IsFalse(body.LegOn(HunterLayout.Leg(12, 0)));
            Assert.IsFalse(body.HasTail);
            HunterHit again = body.HitSeg(12, 999);
            Assert.AreEqual(-1, again.FromSeg);
            Assert.AreEqual(-1, body.HitLeg(HunterLayout.Leg(12, 1), 999).LegLost);
            Assert.AreEqual(10, body.Kept);
        }

        [Test]
        public void DamageToASegment_IsRemembered_SoACutCanBeFinishedLater()
        {
            var body = new HunterBody();
            body.HitSeg(6, 40);
            body.HitSeg(12, 999);
            Assert.AreEqual(6, body.HitSeg(6, 30).FromSeg);
        }

        [Test]
        public void OnlyTheHeadOrTheForeBody_Kills()
        {
            // Cut as short as it goes, it is still alive.
            var body = new HunterBody();
            for (int seg = HunterLayout.Segs - 1; seg >= HunterBody.ForeSegs; seg--) body.HitSeg(seg, 999);
            Assert.AreEqual(HunterBody.ForeSegs, body.Kept);
            Assert.IsTrue(body.Alive);
            for (int leg = 0; leg < HunterLayout.Legs; leg++) body.HitLeg(leg, 999);
            Assert.IsTrue(body.Alive);
            Assert.AreEqual(0.5, body.SpeedShare, 1e-9);

            // The fore body is one number, wherever on it the hits land, and one enormous hit does not cut it.
            HunterHp hp = HunterHp.Default;
            int hits = 0;
            while (body.Alive && hits < 100)
            {
                HunterHit hit = body.HitSeg(hits % HunterBody.ForeSegs, Rifle);
                Assert.AreEqual(-1, hit.FromSeg);
                hits++;
            }
            Assert.AreEqual((int)Math.Ceiling(hp.Fore / Rifle), hits);

            var other = new HunterBody();
            hits = 0;
            while (other.Alive && hits < 100) { other.HitHead(Rifle); hits++; }
            Assert.AreEqual((int)Math.Ceiling(hp.Head / Rifle), hits);
            Assert.Less(hp.Fore, hp.Head, "the soft fore body is the quick kill; the armoured head the slow one");
        }

        [Test]
        public void AOneHitKillGun_KillsWhereverItLands_AndTheDeadTakeNothingMore()
        {
            var body = new HunterBody();
            Assert.IsTrue(body.HitSeg(11, 10, lethal: true).Killed);
            Assert.IsFalse(body.Alive);
            Assert.IsFalse(body.HitHead(999).Killed);
            Assert.AreEqual(-1, body.HitSeg(5, 999).FromSeg);
            Assert.AreEqual(-1, body.HitLeg(0, 999).LegLost);
        }

        [Test]
        public void Shorter_ItRearsLower_AndReachesLessFar()
        {
            var body = new HunterBody();
            Assert.AreEqual(5, body.RearSegs);
            double whole = HunterBrain.Reach(body);
            body.HitSeg(9, 999);
            Assert.AreEqual(3, body.RearSegs);
            body.HitSeg(3, 999);
            Assert.AreEqual(2, body.RearSegs, "cut right down it still lifts enough to show the warning");
            Assert.Less(HunterBrain.Reach(body), whole - 1.0);
            Assert.Greater(HunterBrain.Reach(body), 0.9, "even cut right down it can still bite someone standing on it");
        }

        [Test]
        public void EveryLegLost_SlowsIt_ByTheLegsOnWhatIsLeft()
        {
            var body = new HunterBody();
            Assert.AreEqual(1.0, body.SpeedShare, 1e-9);
            double last = 1.0;
            for (int leg = 0; leg < 10; leg++)
            {
                body.HitLeg(leg, 999);
                Assert.Less(body.SpeedShare, last);
                last = body.SpeedShare;
            }
            // Cutting away a length with all its legs does not slow it: what is left has fewer legs to miss.
            double before = body.SpeedShare;
            body.HitSeg(12, 999);
            Assert.LessOrEqual(body.SpeedShare, before + 1e-9);
            Assert.AreEqual(HunterHp.Default.Leg, body.LegHp(20), 1e-9);
        }

        [Test]
        public void BadHitPoints_FallBackToTheDefaults()
        {
            var body = new HunterBody(new HunterHp());
            Assert.AreEqual(HunterHp.Default.Head, body.Head);
            Assert.AreEqual(HunterHp.Default.Fore, body.Fore);
        }

        [Test]
        public void EverySegmentCutAway_ShortensItsReach_AndTheTailIsCheaperToCutThanTheFront()
        {
            var body = new HunterBody();
            double last = HunterBrain.Reach(body);
            for (int seg = HunterLayout.Segs - 1; seg >= HunterBody.ForeSegs; seg--)
            {
                if (seg > HunterBody.ForeSegs) Assert.Less(body.SegHp(seg), new HunterBody().SegHp(seg - 1) + 1e-9);
                body.HitSeg(seg, 999);
                Assert.Less(HunterBrain.Reach(body), last, "no cut is wasted: segment " + seg);
                last = HunterBrain.Reach(body);
            }
            var fresh = new HunterBody();
            Assert.LessOrEqual(fresh.SegHp(HunterLayout.Segs - 1), Rifle, "the last segment goes in one rifle hit");
            Assert.Greater(fresh.SegHp(HunterBody.ForeSegs), Rifle * 2, "the deepest cut takes three");
        }

        [Test]
        public void ItCannotBeHeldStillByCutAfterCut()
        {
            var body = new HunterBody();
            var rules = new HunterRules();
            var brain = new HunterBrain(rules, 2);
            Assert.IsTrue(brain.Stagger());
            Assert.IsFalse(brain.Stagger(), "not while it thrashes");
            for (double t = 0; t < rules.writheSeconds + 0.1; t += Dt) brain.Think(body, Sees(0, 6), Dt);
            Assert.AreEqual(HunterMode.Run, brain.Mode);
            Assert.IsFalse(brain.Stagger(), "nor straight after");
            for (double t = 0; t < rules.steadySeconds + 0.1; t += Dt) brain.Think(body, Sees(0, 6), Dt);
            Assert.IsTrue(brain.Stagger());
        }

        [Test]
        public void SomeoneCirclingInsideItsReach_IsStillRearedAt()
        {
            // The player walks a circle 1.5 m from its head at walking pace; it must come round and rear all the same.
            var body = new HunterBody();
            var brain = new HunterBrain(new HunterRules(), 6);
            double px = 1.5, pz = 0.0;
            bool reared = false;
            for (double t = 0; t < 4.0 && !reared; t += Dt)
            {
                HunterOrders orders = brain.Think(body, Sees(px, pz), Dt);
                double a = orders.TurnRate * Dt, c = Math.Cos(a), s = Math.Sin(a);
                double x = c * px - s * pz, z = s * px + c * pz;
                px = x - orders.Velocity.X * Dt;
                pz = z - orders.Velocity.Z * Dt;
                // The player steps along the circle about the creature's head.
                double r = Math.Sqrt(px * px + pz * pz), step = 5.0 * Dt / r;
                x = Math.Cos(step) * px + Math.Sin(step) * pz;
                z = -Math.Sin(step) * px + Math.Cos(step) * pz;
                px = x * 1.5 / r;
                pz = z * 1.5 / r;
                reared = orders.Mode == HunterMode.Rear;
            }
            Assert.IsTrue(reared);
        }

        [Test]
        public void BlockedWithSomeoneInReach_ItStillRears()
        {
            var brain = new HunterBrain(new HunterRules(), 6);
            var senses = Sees(0, 1.4);
            senses.Blocked = true;
            Assert.AreEqual(HunterMode.Rear, brain.Think(new HunterBody(), senses, Dt).Mode);
        }

        [Test]
        public void TheMomentItTakesAim_ItDrawsBackAndItsFangsFlyWide()
        {
            double before = HunterBrain.LockAt - 0.01, after = HunterBrain.LockAt + 0.13;
            Assert.Less(HunterAnimator.FangsOf(HunterMode.Rear, before), 0.5);
            Assert.AreEqual(1.0, HunterAnimator.FangsOf(HunterMode.Rear, after), 1e-9);
            Assert.Less(HunterAnimator.RearOf(HunterMode.Rear, before), 0.9);
            Assert.AreEqual(1.0, HunterAnimator.RearOf(HunterMode.Rear, after), 1e-9);
            Assert.Greater(HunterAnimator.RearOf(HunterMode.Rear, HunterBrain.LockAt * 0.5), 0.4, "it is well up long before");
        }

        // ---- the brain

        static HunterSenses Sees(double x, double z) => new HunterSenses { SeesPlayer = true, Player = new Vec3d(x, 0, z) };

        /// <summary>Runs the brain with the player standing still in the world; gives back how far it lunged.</summary>
        static double PlayOut(HunterBrain brain, HunterBody body, ref double px, ref double pz, Func<HunterOrders, bool> until, double seconds = 20.0)
        {
            double moved = 0.0;
            for (double t = 0; t < seconds; t += Dt)
            {
                HunterOrders orders = brain.Think(body, Sees(px, pz), Dt);
                // The world as the creature sees it: turn, then step.
                double a = orders.TurnRate * Dt, c = Math.Cos(a), s = Math.Sin(a);
                double x = c * px - s * pz, z = s * px + c * pz;
                px = x - orders.Velocity.X * Dt;
                pz = z - orders.Velocity.Z * Dt;
                moved += orders.Velocity.Z * Dt;
                if (until(orders)) return moved;
            }
            Assert.Fail("it never got there");
            return moved;
        }

        [Test]
        public void ItComesIn_Rears_AndStrikesWhereThePlayerStood()
        {
            var body = new HunterBody();
            var brain = new HunterBrain(new HunterRules(), 7);
            double px = 0.5, pz = 12.0;
            PlayOut(brain, body, ref px, ref pz, o => o.Mode == HunterMode.Rear);
            Assert.Less(Math.Sqrt(px * px + pz * pz), HunterBrain.Reach(body));
            PlayOut(brain, body, ref px, ref pz, o => o.Landed);
            Assert.AreEqual(HunterMode.Recover, brain.Mode);
            Assert.IsTrue(brain.BiteHits(new Vec3d(px, 0, pz)), "someone who stood still is bitten: " + px + ", " + pz);
        }

        [Test]
        public void AStepAsideAfterItHasTakenAim_IsAMiss_AndBeforeIsNot()
        {
            var rules = new HunterRules();
            foreach (bool late in new[] { true, false })
            {
                var body = new HunterBody();
                var brain = new HunterBrain(rules, 3);
                double px = 0.0, pz = 1.8;
                PlayOut(brain, body, ref px, ref pz, o => o.Mode == HunterMode.Rear);
                // Early: step aside at once, and it follows. Late: step after it has stopped following.
                if (late) PlayOut(brain, body, ref px, ref pz, o => o.T >= HunterBrain.LockAt + 0.02);
                px += 1.3;
                PlayOut(brain, body, ref px, ref pz, o => o.Landed);
                Assert.AreEqual(!late, brain.BiteHits(new Vec3d(px, 0, pz)), late ? "a late step aside is a miss" : "an early one is followed");
            }
        }

        [Test]
        public void TheWarning_IsLongEnoughToStepOutOfTheBite()
        {
            var rules = new HunterRules();
            // What is left of the rear after it stops following, and the strike: the player's time to move.
            double window = rules.rearSeconds * (1.0 - HunterBrain.LockAt) + rules.strikeSeconds;
            Assert.Greater(window, 0.4);
            // Walking (5 m/s) for that long takes them clear of the bite.
            Assert.Greater(5.0 * window, rules.biteRadius + 0.4);
            Assert.GreaterOrEqual(rules.rearSeconds * HunterBrain.LockAt, 0.4, "and they can see where it is aimed before it locks");
        }

        [Test]
        public void AfterAStrike_ItsHeadLiesStill_ThenItRunsALoopBeforeComingBack()
        {
            var body = new HunterBody();
            var rules = new HunterRules();
            var brain = new HunterBrain(rules, 5);
            double px = 0.0, pz = 1.5;
            PlayOut(brain, body, ref px, ref pz, o => o.Landed);
            double still = 0.0;
            while (brain.Mode == HunterMode.Recover)
            {
                HunterOrders orders = brain.Think(body, Sees(px, pz), Dt);
                if (brain.Mode != HunterMode.Recover) break;
                Assert.AreEqual(0.0, orders.Velocity.Magnitude);
                Assert.AreEqual(0.0, orders.TurnRate);
                still += Dt;
            }
            Assert.AreEqual(rules.recoverSeconds, still, 0.05);
            Assert.IsTrue(brain.Looping);
            // While it loops it does not rear, even with the player in reach in front of it.
            HunterOrders looping = brain.Think(body, Sees(0, 1.0), Dt);
            Assert.AreEqual(HunterMode.Run, looping.Mode);
            Assert.Greater(looping.Velocity.Z, 2.0);
        }

        [Test]
        public void ACutDuringTheWarning_StopsTheStrike()
        {
            var body = new HunterBody();
            var brain = new HunterBrain(new HunterRules(), 2);
            double px = 0.0, pz = 1.6;
            PlayOut(brain, body, ref px, ref pz, o => o.Mode == HunterMode.Rear);
            body.HitSeg(8, 999);
            brain.Stagger();
            bool landed = false;
            for (double t = 0; t < 1.2; t += Dt)
            {
                HunterOrders orders = brain.Think(body, Sees(px, pz), Dt);
                landed |= orders.Landed;
                if (orders.Mode == HunterMode.Writhe) Assert.AreEqual(0.0, orders.Velocity.Magnitude);
            }
            Assert.IsFalse(landed);
            Assert.AreEqual(HunterMode.Run, brain.Mode);
        }

        [Test]
        public void ItNeverBacksUp_AndTurnsOnTheSpotWhenBlocked()
        {
            var body = new HunterBody();
            var brain = new HunterBrain(new HunterRules(), 9);
            var random = new SeededRandom(4);
            for (int i = 0; i < 4000; i++)
            {
                var senses = new HunterSenses
                {
                    SeesPlayer = random.NextDouble() < 0.8,
                    Player = new Vec3d(random.NextDouble() * 30 - 15, 0, random.NextDouble() * 30 - 15),
                    Blocked = random.NextDouble() < 0.2,
                };
                bool wasRunning = brain.Mode == HunterMode.Run;
                HunterOrders orders = brain.Think(body, senses, Dt);
                Assert.GreaterOrEqual(orders.Velocity.Z, 0.0);
                if (senses.Blocked && wasRunning && orders.Mode == HunterMode.Run && senses.SeesPlayer)
                {
                    Assert.Less(orders.Velocity.Z, 1.0);
                    Assert.AreNotEqual(0.0, orders.TurnRate);
                }
            }
        }

        [Test]
        public void UnseenAndUnalerted_ItOnlyWanders()
        {
            var body = new HunterBody();
            var rules = new HunterRules();
            var brain = new HunterBrain(rules, 1);
            for (double t = 0; t < 20; t += Dt)
            {
                HunterOrders orders = brain.Think(body, new HunterSenses { Player = new Vec3d(0, 0, 1.2) }, Dt);
                Assert.AreEqual(HunterMode.Run, orders.Mode);
                Assert.LessOrEqual(orders.Velocity.Z, rules.wanderPace + 1e-9);
            }
            brain.Alert();
            HunterOrders alerted = brain.Think(body, new HunterSenses { Player = new Vec3d(0, 0, 1.2) }, Dt);
            Assert.AreEqual(HunterMode.Rear, alerted.Mode, "shot from out of sight, it knows where from");
        }

        [Test]
        public void ItIsQuickerThanAWalk_AndSlowerThanASprint()
        {
            var rules = new HunterRules();
            Assert.Greater(rules.pace, 4.0);
            Assert.Less(rules.pace, 7.0);
        }

        // ---- the animator

        [Test]
        public void TheBody_FollowsTheHead_AtAFixedSpacing()
        {
            var body = new HunterBody();
            var animator = new HunterAnimator();
            var pose = new HunterPose();
            // Run, turn hard, run: links stay their length whatever the path.
            var inputs = new[]
            {
                new HunterAnimInput { Velocity = new Vec3d(0, 0, 4.6) },
                new HunterAnimInput { Velocity = new Vec3d(0, 0, 4.6), TurnRate = 2.4 },
                new HunterAnimInput { Velocity = new Vec3d(0, 0, 2.0), TurnRate = -2.4 },
                new HunterAnimInput(),
            };
            foreach (HunterAnimInput input in inputs)
            {
                for (double t = 0; t < 1.5; t += Dt)
                {
                    animator.Update(body, input, Dt, pose);
                    AssertSound(pose);
                    for (int i = 1; i < pose.Nodes; i++)
                        Assert.AreEqual(HunterLayout.Spacing, Vec3d.Distance(pose.NodePosition[i], pose.NodePosition[i - 1]), 0.06, "link " + i);
                }
            }
            Assert.AreEqual(HunterLayout.Nodes, pose.Nodes);
        }

        [Test]
        public void Standing_ItLiesStraightOutBehindItsHead_OnTheGround()
        {
            var body = new HunterBody();
            HunterPose pose = Run(new HunterAnimator(), body, new HunterAnimInput { Mode = HunterMode.Recover, T = 0.5 }, 1.0);
            for (int i = 2; i < pose.Nodes; i++)
            {
                Assert.AreEqual(0.0, pose.NodePosition[i].X, 1e-6);
                Assert.AreEqual(-i * HunterLayout.Spacing, pose.NodePosition[i].Z, 1e-6);
                Assert.AreEqual(HunterLayout.RideHeight, pose.NodePosition[i].Y, 1e-6);
            }
            Assert.Less(pose.NodePosition[0].Y, HunterLayout.RideHeight - 0.1, "after a strike its head is down on the ground");
            Assert.Greater(pose.NodePosition[0].Y - HunterLayout.SegBottom, 0.0, "but not under it");
        }

        [Test]
        public void ATurn_RunsDownTheBody_TheTailStillWhereTheHeadWas()
        {
            var body = new HunterBody();
            var animator = new HunterAnimator();
            Run(animator, body, new HunterAnimInput { Velocity = new Vec3d(0, 0, 4.0) }, 2.0);
            // A quarter turn to the right in half a second.
            HunterPose pose = Run(animator, body, new HunterAnimInput { Velocity = new Vec3d(0, 0, 4.0), TurnRate = Math.PI }, 0.5);
            // The front of the body has come round behind the head; the tail still lies off along the old line,
            // which is now to the animal's right.
            Assert.Less(Math.Abs(pose.NodePosition[2].X), 0.35);
            Assert.Greater(pose.NodePosition[HunterLayout.Segs].X, 1.5);
        }

        [Test]
        public void Reared_TheHeadIsHighAndPulledBack_AndTheStrikeLaysItFlatAgain()
        {
            var body = new HunterBody();
            var animator = new HunterAnimator();
            HunterPose pose = Run(animator, body, new HunterAnimInput { Mode = HunterMode.Rear, T = 1.0 }, 1.0);
            AssertSound(pose);
            Assert.Greater(pose.NodePosition[0].Y, 1.3, "taller than a crouching person");
            Assert.Less(pose.NodePosition[0].Y, 2.0);
            Assert.Less(pose.NodePosition[0].Z, -0.5, "lifting pulls the head back");
            Assert.Greater(pose.Fangs, 0.9);
            for (int i = 1; i < pose.Nodes; i++)
                Assert.AreEqual(HunterLayout.Spacing, Vec3d.Distance(pose.NodePosition[i], pose.NodePosition[i - 1]), 1e-6);
            // Nothing behind what it lifts leaves the ground.
            for (int i = body.RearSegs + 1; i < pose.Nodes; i++) Assert.AreEqual(HunterLayout.RideHeight, pose.NodePosition[i].Y, 1e-6);
            // Its head looks down at whoever it is about to bite.
            Assert.Less(pose.NodeRotation[0].Rotate(Vec3d.Forward).Y, -0.2);

            pose = Run(animator, body, new HunterAnimInput { Mode = HunterMode.Strike, T = 1.0 }, 0.5, pose);
            Assert.Less(pose.NodePosition[0].Y, HunterLayout.RideHeight);
            Assert.AreEqual(0.0, pose.NodePosition[0].Z, 0.02);
        }

        [Test]
        public void Shorter_ItRearsLower()
        {
            var whole = new HunterBody();
            var cut = new HunterBody();
            cut.HitSeg(4, 999);
            var input = new HunterAnimInput { Mode = HunterMode.Rear, T = 1.0 };
            double high = Run(new HunterAnimator(), whole, input, 1.0).NodePosition[0].Y;
            HunterPose pose = Run(new HunterAnimator(), cut, input, 1.0);
            AssertSound(pose);
            Assert.AreEqual(5, pose.Nodes);
            Assert.Less(pose.NodePosition[0].Y, high - 0.5);
            Assert.Greater(pose.NodePosition[0].Y, HunterLayout.RideHeight + 0.15);
        }

        [Test]
        public void CutRightDown_EveryPoseIsStillSound()
        {
            var body = new HunterBody();
            body.HitSeg(HunterBody.ForeSegs, 999);
            var animator = new HunterAnimator();
            var pose = new HunterPose();
            foreach (HunterMode mode in Enum.GetValues(typeof(HunterMode)))
            {
                for (double t = 0; t <= 1.0; t += 0.05)
                {
                    animator.Update(body, new HunterAnimInput { Mode = mode, T = t, Velocity = new Vec3d(0, 0, 3), TurnRate = 1 }, Dt, pose);
                    AssertSound(pose);
                }
            }
            Assert.AreEqual(HunterBody.ForeSegs + 1, pose.Nodes);
        }

        [Test]
        public void RunningLegs_ReachTheGround_AndNeverGoThroughIt()
        {
            var body = new HunterBody();
            var animator = new HunterAnimator();
            var pose = new HunterPose();
            double lowest = double.MaxValue, highest = double.MinValue;
            for (double t = 0; t < 2.0; t += Dt)
            {
                animator.Update(body, new HunterAnimInput { Velocity = new Vec3d(0, 0, 4.6) }, Dt, pose);
                if (t < 1.0) continue;
                for (int leg = 0; leg < HunterLayout.Legs; leg++)
                {
                    Vec3d tip = pose.LegPosition[leg] + pose.LegRotation[leg].Rotate(new Vec3d(0, 0, HunterLayout.LegLength));
                    lowest = Math.Min(lowest, tip.Y);
                    highest = Math.Max(highest, tip.Y);
                    // A foot is out to its own side.
                    Assert.Greater(tip.X * HunterLayout.Sign(leg) + 0.3, pose.NodePosition[HunterLayout.SegOf(leg) + 1].X * HunterLayout.Sign(leg));
                }
            }
            Assert.AreEqual(0.0, lowest, 0.04, "feet come down to the ground");
            Assert.Greater(highest, 0.1, "and are picked up to step");
        }

        [Test]
        public void TheLeftFang_IsTheRightOneMirrored()
        {
            var body = new HunterBody();
            HunterPose pose = Run(new HunterAnimator(), body, new HunterAnimInput { Mode = HunterMode.Rear, T = 0.5 }, 0.01);
            pose = Run(new HunterAnimator(), body, new HunterAnimInput { Mode = HunterMode.Recover, T = 0.5 }, 1.0);
            QuatD head = pose.NodeRotation[0].Inverse;
            // Along the fang, and the way it curves (the model curves toward -X), in the head's frame.
            Vec3d r = (head * pose.FangRotation[0]).Rotate(Vec3d.Forward), l = (head * pose.FangRotation[1]).Rotate(Vec3d.Forward);
            Vec3d rc = (head * pose.FangRotation[0]).Rotate(-1.0 * Vec3d.Right), lc = (head * pose.FangRotation[1]).Rotate(-1.0 * Vec3d.Right);
            Assert.AreEqual(r.X, -l.X, 1e-6);
            Assert.AreEqual(r.Z, l.Z, 1e-6);
            Assert.Less(rc.X, 0.0, "the right fang curves in to the left");
            Assert.Greater(lc.X, 0.0, "and the left one in to the right");
            Assert.AreEqual(-(pose.FangPosition[0] - pose.NodePosition[0]).X, (pose.FangPosition[1] - pose.NodePosition[0]).X, 1e-6);
        }

        [Test]
        public void JustCut_ItThrashesFromSideToSide()
        {
            var body = new HunterBody();
            body.HitSeg(10, 999);
            var animator = new HunterAnimator();
            var pose = new HunterPose();
            double widest = 0.0;
            for (double t = 0; t < 0.9; t += Dt)
            {
                animator.Update(body, new HunterAnimInput { Mode = HunterMode.Writhe, T = t / 0.9 }, Dt, pose);
                AssertSound(pose);
                for (int i = 2; i < pose.Nodes; i++) widest = Math.Max(widest, Math.Abs(pose.NodePosition[i].X));
            }
            Assert.Greater(widest, 0.12);
            Assert.Less(widest, 0.3);
            Assert.AreEqual(11, pose.Nodes);
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
        public void HunterModelFile_MatchesTheLayout()
        {
            // tools/blender/build_creatures.py carries its own copy of HunterLayout (HUNTER). If either changes, this
            // fails until the script is changed to match and run again.
            ModelFile model = Load(HunterLayout.Model);
            double half = HunterLayout.Spacing * 0.5;
            AssertSocket(model, "SegEnd", 0, 0, half);
            AssertSocket(model, "SegSide", HunterLayout.SegHalfWidth, -HunterLayout.SegBottom, 0);
            AssertSocket(model, "SegTop", 0, HunterLayout.SegTop, 0);
            AssertSocket(model, "HeadEnd", 0, 0, HunterLayout.HeadLength - half);
            AssertSocket(model, "HeadSide", HunterLayout.HeadHalfWidth, 0, 0);
            AssertSocket(model, "HipR", HunterLayout.Hip.X, HunterLayout.Hip.Y, HunterLayout.Hip.Z);
            AssertSocket(model, "LegEnd", 0, 0, HunterLayout.LegLength);
            AssertSocket(model, "LegSide", HunterLayout.LegRadius, 0, 0);
            AssertSocket(model, "FangRootR", HunterLayout.FangRoot.X, HunterLayout.FangRoot.Y, HunterLayout.FangRoot.Z);
            AssertSocket(model, "FangEnd", 0, 0, HunterLayout.FangLength);
            AssertSocket(model, "FeelerRootR", HunterLayout.FeelerRoot.X, HunterLayout.FeelerRoot.Y, HunterLayout.FeelerRoot.Z);
            AssertSocket(model, "FeelerEnd", 0, 0, HunterLayout.FeelerLength);
            AssertSocket(model, "TailRootR", HunterLayout.TailRoot.X, HunterLayout.TailRoot.Y, HunterLayout.TailRoot.Z);
            AssertSocket(model, "TailLegEnd", 0, 0, HunterLayout.TailLegLength);
            AssertSocket(model, "StumpRim", HunterLayout.StumpRadius, 0, 0);
            string[] pieces = { HunterLayout.HeadPiece, HunterLayout.FangPiece, HunterLayout.FeelerPiece, HunterLayout.SegPiece, HunterLayout.LegPiece, HunterLayout.TailPiece,
                                HunterLayout.Stump, HunterLayout.Meat[0], HunterLayout.Meat[1] };
            foreach (string piece in pieces) Assert.IsNotNull(model.Part(piece), "missing piece " + piece);
            Assert.IsTrue(model.Textured, "the Cave hunter is drawn with pictures");

            // The shapes are the size the rules (and the hitboxes) think they are. Front and back are along +Z.
            void Box(string piece, out float x, out float hi, out float lo, out float fore, out float aft, out float left)
            {
                ModelPart part = model.Part(piece);
                x = 0; hi = float.MinValue; lo = float.MaxValue; fore = float.MinValue; aft = float.MaxValue; left = float.MaxValue;
                for (int i = 0; i < part.VertexCount; i++)
                {
                    x = Math.Max(x, part.Positions[i * 3]);
                    left = Math.Min(left, part.Positions[i * 3]);
                    hi = Math.Max(hi, part.Positions[i * 3 + 1]);
                    lo = Math.Min(lo, part.Positions[i * 3 + 1]);
                    fore = Math.Max(fore, part.Positions[i * 3 + 2]);
                    aft = Math.Min(aft, part.Positions[i * 3 + 2]);
                }
            }
            Box(HunterLayout.SegPiece, out float x, out float hi, out float lo, out float fore, out float aft, out float left);
            Assert.AreEqual(HunterLayout.SegHalfWidth, x, 0.02, "segment half width");
            Assert.AreEqual(-HunterLayout.SegHalfWidth, left, 0.02, "segment half width, left");
            Assert.AreEqual(HunterLayout.SegTop, hi, 0.015, "segment top");
            Assert.AreEqual(-HunterLayout.SegBottom, lo, 0.02, "segment bottom");
            Assert.AreEqual(half, fore, 0.02, "segment front");
            Assert.Less(aft, -half - 0.02, "a segment's back plate overlaps the next");
            Assert.Greater(aft, -half - 0.12);

            Box(HunterLayout.HeadPiece, out x, out hi, out lo, out fore, out aft, out left);
            Assert.AreEqual(HunterLayout.HeadHalfWidth, x, 0.02, "head half width");
            Assert.AreEqual(HunterLayout.HeadLength - half, fore, 0.04, "head front");
            Assert.AreEqual(-half, aft, 0.03, "head back");
            Assert.Less(hi, 0.11);
            Assert.Greater(lo, -0.13);

            // A leg, a feeler and a tail leg reach their length, bend only upward, and are the same either side.
            foreach ((string piece, double length, double high) in new[] { (HunterLayout.LegPiece, HunterLayout.LegLength, 0.14), (HunterLayout.FeelerPiece, HunterLayout.FeelerLength, 0.10), (HunterLayout.TailPiece, HunterLayout.TailLegLength, 0.13) })
            {
                Box(piece, out x, out hi, out lo, out fore, out aft, out left);
                Assert.AreEqual(length, fore, 0.01, piece + " length");
                Assert.AreEqual(x, -left, 0.012, piece + " is the same either side");
                Assert.Less(hi, high, piece + " arch");
                Assert.Greater(lo, -0.06, piece + " never hangs below its line");
            }
            // The fang reaches its length, hooks in toward -X, and is the same above as below.
            Box(HunterLayout.FangPiece, out x, out hi, out lo, out fore, out aft, out left);
            Assert.AreEqual(HunterLayout.FangLength, fore, 0.01);
            Assert.Less(left, -0.07, "the fang hooks in");
            Assert.AreEqual(hi, -lo, 0.008, "the same above as below, so the left one can be the right one rolled over");
        }
    }
}
