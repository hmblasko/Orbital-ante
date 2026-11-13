using System;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;

namespace OrbitalAnte.Tests
{
    /// <summary>The enemy's animation rules: feet that stay planted, legs that reach, knocks that settle.</summary>
    public class BodyAnimatorTests
    {
        const double Dt = 1.0 / 60.0;
        const double Walk = 2.0;
        const double Sprint = 4.6;

        static BodyAnimInput Standing(double speed, double turn = 0.0)
        {
            return new BodyAnimInput
            {
                State = MovementState.Intact, ForwardSpeed = speed, TurnRate = turn, Moving = speed > 0.01,
                WalkSpeed = Walk, SprintSpeed = Sprint, LeftArmWorks = true, RightArmWorks = true, GunSide = -1,
            };
        }

        static BodyAnimInput Hurt(MovementState state, double phase, bool moving = true)
        {
            return new BodyAnimInput
            {
                State = state, ForwardSpeed = 0.6, Phase = phase, Moving = moving, WalkSpeed = Walk, SprintSpeed = Sprint,
                LeftArmWorks = true, RightArmWorks = true, StandingLeg = BodySkeleton.Left, GunSide = -1,
            };
        }

        static Vec3d Ankle(BodyPose pose, int side)
        {
            pose.World(BodySkeleton.Foot(side), out Vec3d p, out _);
            return p;
        }

        static void AssertSound(BodyPose pose, string when)
        {
            Assert.IsFalse(double.IsNaN(pose.RootPosition.X + pose.RootPosition.Y + pose.RootPosition.Z), when + ": root position");
            for (int i = 0; i < pose.Local.Length; i++)
            {
                QuatD q = pose.Local[i];
                double length = Math.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W);
                Assert.AreEqual(1.0, length, 1e-6, when + ": " + (BodyBone)i + " is not a clean turn");
            }
        }

        /// <summary>
        /// Walks the body along a straight line or a turn and measures how far each foot drifts over the ground
        /// while it is down. Returns the worst drift in metres.
        /// </summary>
        static double WorstFootSlide(double speed, double turnDegrees, double seconds, out int steps)
        {
            var a = new BodyAnimator(3);
            // Settle into the gait first.
            for (int i = 0; i < 120; i++) a.Update(Dt, Standing(speed, turnDegrees));

            double x = 0, z = 0, heading = 0;
            var planted = new bool[2];
            var plantedAt = new Vec3d[2];
            double worst = 0;
            steps = 0;
            for (int i = 0; i < (int)(seconds / Dt); i++)
            {
                heading += turnDegrees * Math.PI / 180.0 * Dt;
                x += Math.Sin(heading) * speed * Dt;
                z += Math.Cos(heading) * speed * Dt;
                a.Update(Dt, Standing(speed, turnDegrees));
                for (int s = 0; s < 2; s++)
                {
                    Vec3d local = Ankle(a.Pose, s);
                    double c = Math.Cos(heading), sn = Math.Sin(heading);
                    var world = new Vec3d(x + local.X * c + local.Z * sn, local.Y, z - local.X * sn + local.Z * c);
                    bool down = local.Y < BodySkeleton.AnkleY + 0.004;
                    if (down && !planted[s])
                    {
                        plantedAt[s] = world;
                        steps++;
                    }
                    if (down)
                    {
                        double drift = Math.Sqrt(Math.Pow(world.X - plantedAt[s].X, 2) + Math.Pow(world.Z - plantedAt[s].Z, 2));
                        worst = Math.Max(worst, drift);
                    }
                    planted[s] = down;
                }
            }
            return worst;
        }

        [Test]
        public void Walking_APlantedFootStaysWhereItLanded()
        {
            double worst = WorstFootSlide(Walk, 0.0, 6.0, out int steps);
            Assert.Greater(steps, 12, "it should have taken a good many steps");
            Assert.Less(worst, 0.03, "feet slide over the ground while walking");
        }

        [Test]
        public void Running_APlantedFootStaysWhereItLanded()
        {
            double worst = WorstFootSlide(Sprint, 0.0, 5.0, out int steps);
            Assert.Greater(steps, 15);
            Assert.Less(worst, 0.05, "feet slide over the ground while running");
        }

        [Test]
        public void TurningOnTheSpot_ItStepsRound_AndPlantedFeetDoNotSkate()
        {
            double worst = WorstFootSlide(0.0, 200.0, 4.0, out int steps);
            Assert.Greater(steps, 6, "it should shuffle its feet round, not spin on the spot");
            Assert.Less(worst, 0.04, "feet skate while turning on the spot");
        }

        [Test]
        public void WalkingInACurve_PlantedFeetDoNotSkate()
        {
            double worst = WorstFootSlide(Walk, 70.0, 6.0, out _);
            Assert.Less(worst, 0.05);
        }

        [Test]
        public void StandingStill_TheFeetDoNotMove_ButTheBodyDoes()
        {
            var a = new BodyAnimator(5);
            for (int i = 0; i < 90; i++) a.Update(Dt, Standing(0));
            Vec3d left = Ankle(a.Pose, BodySkeleton.Left), right = Ankle(a.Pose, BodySkeleton.Right);
            a.Pose.World(BodyBone.Head, out Vec3d headStart, out _);
            double headTravel = 0;
            for (int i = 0; i < 600; i++)
            {
                a.Update(Dt, Standing(0));
                Assert.Less(Vec3d.Distance(left, Ankle(a.Pose, BodySkeleton.Left)), 0.004, "left foot crept at frame " + i);
                Assert.Less(Vec3d.Distance(right, Ankle(a.Pose, BodySkeleton.Right)), 0.004, "right foot crept at frame " + i);
                a.Pose.World(BodyBone.Head, out Vec3d head, out _);
                headTravel = Math.Max(headTravel, Vec3d.Distance(head, headStart));
            }
            Assert.Greater(headTravel, 0.008, "an idle character should breathe and shift its weight, not stand like a statue");
        }

        [Test]
        public void TheLegsAlwaysReachTheGround_AndNeverGoThroughIt([Values(0.0, 1.0, 2.0, 3.3, 4.6)] double speed)
        {
            var a = new BodyAnimator(9);
            for (int i = 0; i < 400; i++)
            {
                a.Update(Dt, Standing(speed));
                AssertSound(a.Pose, "speed " + speed + " frame " + i);
                bool oneDown = false;
                for (int s = 0; s < 2; s++)
                {
                    double y = Ankle(a.Pose, s).Y;
                    Assert.Greater(y, BodySkeleton.AnkleY - 0.012, "a foot sank into the ground at speed " + speed);
                    Assert.Less(y, BodySkeleton.AnkleY + 0.45, "a foot flew up at speed " + speed);
                    // A foot rolling off its toes has its ankle up to 6 cm higher and is still on the ground.
                    if (y < BodySkeleton.AnkleY + 0.065) oneDown = true;
                }
                // Walking always has a foot on the ground; only a run has both in the air.
                if (i > 120 && speed <= Walk) Assert.IsTrue(oneDown, "both feet off the ground at walking speed " + speed);
            }
        }

        [Test]
        public void SpeedingUp_QuickensTheStep()
        {
            var a = new BodyAnimator(2);
            for (int i = 0; i < 200; i++) a.Update(Dt, Standing(Walk));
            double walking = a.Cadence;
            for (int i = 0; i < 200; i++) a.Update(Dt, Standing(Sprint));
            Assert.Greater(a.Cadence, walking * 1.3);
            for (int i = 0; i < 200; i++) a.Update(Dt, Standing(0));
            Assert.Less(a.Cadence, 0.05, "the gait should stop when the character does");
        }

        [Test]
        public void AKnock_RocksTheBodyAwayFromTheShot_ThenSettles()
        {
            var a = new BodyAnimator(4);
            for (int i = 0; i < 120; i++) a.Update(Dt, Standing(0));
            a.Pose.World(BodyBone.Head, out Vec3d before, out _);
            // Shot from the front: the bullet travels toward -z.
            a.Flinch(0, -1, 0.8, 0, 1.0);
            double furthestBack = 0;
            for (int i = 0; i < 20; i++)
            {
                a.Update(Dt, Standing(0));
                a.Pose.World(BodyBone.Head, out Vec3d head, out _);
                furthestBack = Math.Min(furthestBack, head.Z - before.Z);
                AssertSound(a.Pose, "knock frame " + i);
            }
            Assert.Less(furthestBack, -0.03, "a hit from the front should rock the head back");
            Assert.Greater(furthestBack, -0.45, "but not fold the body in half");
            for (int i = 0; i < 240; i++) a.Update(Dt, Standing(0));
            var settled = new BodyAnimator(4);
            for (int i = 0; i < 380; i++) settled.Update(Dt, Standing(0));
            a.Pose.World(BodyBone.Spine, out Vec3d spineA, out _);
            settled.Pose.World(BodyBone.Spine, out Vec3d spineB, out _);
            Assert.Less(Vec3d.Distance(spineA, spineB), 0.01, "the knock never died away");
        }

        [Test]
        public void ChangingHowItMoves_IsBlended_NotSnapped()
        {
            var a = new BodyAnimator(6);
            for (int i = 0; i < 120; i++) a.Update(Dt, Standing(Walk));
            a.Pose.World(BodyBone.Head, out Vec3d last, out _);
            double worstJump = 0;
            for (int i = 0; i < 90; i++)
            {
                // A leg is lost, then the other: walk to hop to crawl.
                BodyAnimInput input = i < 40 ? Hurt(MovementState.OneLeg, i * 0.18) : Hurt(MovementState.Crawl, i * 0.09);
                a.Update(Dt, input);
                a.Pose.World(BodyBone.Head, out Vec3d head, out _);
                worstJump = Math.Max(worstJump, Vec3d.Distance(head, last));
                last = head;
                AssertSound(a.Pose, "blend frame " + i);
            }
            Assert.Less(worstJump, 0.16, "the head jumped in one frame when the way of moving changed");
        }

        [Test]
        public void Crawling_KeepsTheBodyOnTheGround_AndTheHeadUp()
        {
            var a = new BodyAnimator(8);
            for (int i = 0; i < 300; i++)
            {
                a.Update(Dt, Hurt(MovementState.Crawl, i * 0.09));
                if (i < 60) continue;
                a.Pose.World(BodyBone.Spine, out Vec3d spine, out _);
                a.Pose.World(BodyBone.Head, out Vec3d neck, out QuatD headTurn);
                Assert.Less(spine.Y, 0.45, "the chest lifted off the ground");
                Assert.Greater(spine.Y, 0.05, "the chest went into the ground");
                Vec3d facing = headTurn.Rotate(Vec3d.Forward);
                Assert.Greater(facing.Y, -0.8, "the face is buried in the ground");
            }
        }

        [Test]
        public void EveryWayOfMoving_GivesASoundPose([Values] MovementState state, [Values(true, false)] bool moving)
        {
            var a = new BodyAnimator(11);
            for (int i = 0; i < 200; i++)
            {
                BodyAnimInput input = Hurt(state, i * 0.13, moving);
                input.TurnRate = 220.0 * Math.Sin(i * 0.05);
                input.ForwardSpeed = moving ? 4.6 * Math.Abs(Math.Sin(i * 0.02)) : 0.0;
                input.Aim = i % 60 < 30 ? 1.0 : 0.0;
                input.GunSide = i % 2;
                if (i % 37 == 0) a.Flinch(Math.Sin(i), Math.Cos(i), 0.5, 0.3, 4.0);
                a.Update(i % 9 == 0 ? 0.2 : Dt, input);
                AssertSound(a.Pose, state + " frame " + i);
                Assert.Less(Math.Abs(a.Pose.RootPosition.Y), 2.5, state + ": the body left the scene");
            }
        }

        [Test]
        public void TwoCharacters_DoNotMoveInStep()
        {
            var a = new BodyAnimator(1);
            var b = new BodyAnimator(2);
            double apart = 0;
            for (int i = 0; i < 300; i++)
            {
                a.Update(Dt, Standing(Walk));
                b.Update(Dt, Standing(Walk));
                apart = Math.Max(apart, Vec3d.Distance(Ankle(a.Pose, BodySkeleton.Left), Ankle(b.Pose, BodySkeleton.Left)));
            }
            Assert.Greater(apart, 0.1, "a crowd would march in perfect step");
        }
    }
}
