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
        static double WorstFootSlide(double speed, double turnDegrees, double seconds, out int steps) =>
            WorstFootSlide(new BodyAnimator(3), 1.0, Standing(speed, turnDegrees), seconds, out steps);

        /// <summary>
        /// The same for any animator, drawn at any size. The pose is at the skeleton's own size, so it is
        /// scaled up to the world before the feet are measured against the ground really covered.
        /// </summary>
        static double WorstFootSlide(BodyAnimator a, double size, BodyAnimInput input, double seconds, out int steps)
        {
            double speed = input.ForwardSpeed, turnDegrees = input.TurnRate;
            BodyAnimInput Standing(double ignoredSpeed, double ignoredTurn) => input;
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
                    Vec3d local = Ankle(a.Pose, s) * size;
                    double c = Math.Cos(heading), sn = Math.Sin(heading);
                    var world = new Vec3d(x + local.X * c + local.Z * sn, local.Y, z - local.X * sn + local.Z * c);
                    bool down = local.Y < (BodySkeleton.AnkleY + 0.004) * size;
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

        [Test]
        public void ASlowFrameRate_LosesNoTime_SoTheStrideKeepsUpWithTheGround()
        {
            // The character is moved by the whole frame time, so the animation must use the whole of it too.
            var smooth = new BodyAnimator(3);
            var choppy = new BodyAnimator(3);
            BodyAnimInput input = Standing(Walk);
            for (int i = 0; i < 180; i++) smooth.Update(Dt, input);     // three seconds at 60 frames a second
            for (int i = 0; i < 30; i++) choppy.Update(0.1, input);     // the same three seconds at 10
            double lag = Math.Abs(smooth.Gait - choppy.Gait);
            lag = Math.Min(lag, 1.0 - lag);                             // the stride goes round from 1 back to 0
            Assert.Less(lag, 0.06, "at 10 frames a second the stride fell behind the ground covered");
            AssertSound(choppy.Pose, "at 10 frames a second");
        }

        // ------------------------------------------------------------------ one skeleton, four ways of carrying it

        /// <summary>The four kinds: the style, the size the body is drawn at, and how fast it moves beside the trooper.</summary>
        static readonly object[] Kinds =
        {
            new object[] { "Enemy", 1.0, 1.0 },
            new object[] { "EnemyRunner", 0.85, 1.5 },
            new object[] { "EnemyBruiser", 1.3, 0.8 },
            new object[] { "EnemyMarksman", 1.2, 0.9 },
        };

        static BodyAnimInput KindInput(double speedShare, double quick)
        {
            BodyAnimInput input = Standing(0);
            input.WalkSpeed = Walk * quick;
            input.SprintSpeed = Sprint * quick;
            input.ForwardSpeed = Lerp(input.WalkSpeed, input.SprintSpeed, speedShare);
            input.Moving = true;
            return input;
        }

        static double Lerp(double a, double b, double t) => a + (b - a) * t;

        [Test]
        public void TheTrooperStyle_ChangesNothing()
        {
            var plain = new BodyAnimator(3);
            var styled = new BodyAnimator(3, BodyStyle.Trooper) { Size = 1.0 };
            Assert.AreSame(BodyStyle.Trooper, plain.Style);
            Assert.AreSame(BodyStyle.Trooper, BodyStyle.For("Enemy"));
            Assert.AreSame(BodyStyle.Trooper, BodyStyle.For("something nobody made"));
            foreach (double speed in new[] { 0.0, Walk, Sprint })
            {
                for (int i = 0; i < 200; i++)
                {
                    plain.Update(Dt, Standing(speed));
                    styled.Update(Dt, Standing(speed));
                }
                Assert.AreEqual(plain.Gait, styled.Gait, 0.0);
                for (int b = 0; b < plain.Pose.Local.Length; b++)
                {
                    Assert.AreEqual(plain.Pose.Local[b].W, styled.Pose.Local[b].W, 0.0, ((BodyBone)b).ToString());
                    Assert.AreEqual(plain.Pose.Local[b].X, styled.Pose.Local[b].X, 0.0, ((BodyBone)b).ToString());
                }
            }
        }

        [TestCaseSource(nameof(Kinds))]
        public void EveryKind_KeepsItsPlantedFeetStill_AtItsOwnSizeAndSpeed(string model, double size, double quick)
        {
            foreach (double share in new[] { 0.0, 0.5, 1.0 })
            {
                var a = new BodyAnimator(3, BodyStyle.For(model)) { Size = size };
                double worst = WorstFootSlide(a, size, KindInput(share, quick), 5.0, out int steps);
                Assert.Greater(steps, 8, model + " should have taken a good many steps");
                Assert.Less(worst, 0.06 * size, model + " slides its feet at " + share + " of the way from walk to sprint");
                AssertSound(a.Pose, model);
            }
        }

        [TestCaseSource(nameof(Kinds))]
        public void EveryKind_ReachesTheGround_WithoutSquatting(string model, double size, double quick)
        {
            foreach (double share in new[] { 0.0, 1.0 })
            {
                var a = new BodyAnimator(4, BodyStyle.For(model)) { Size = size };
                BodyAnimInput input = KindInput(share, quick);
                double lowest = double.MaxValue;
                for (int i = 0; i < 400; i++)
                {
                    a.Update(Dt, input);
                    if (i < 120) continue;
                    a.Pose.World(BodyBone.Hips, out Vec3d hips, out _);
                    lowest = Math.Min(lowest, hips.Y);
                    for (int s = 0; s < 2; s++) Assert.Greater(Ankle(a.Pose, s).Y, BodySkeleton.AnkleY - 0.004, model + " put a foot through the ground");
                }
                // Legs that are asked to reach too far pull the hips down to the knees. None of them may.
                Assert.Greater(lowest, BodySkeleton.HipsY - 0.17, model + " squats to reach its own stride at " + share);
            }
        }

        [Test]
        public void TheQuickSmallOne_MovesItsLegsFaster_AndTheBigSlowOne_Slower()
        {
            double Cadence(string model, double size, double quick)
            {
                var a = new BodyAnimator(3, BodyStyle.For(model)) { Size = size };
                BodyAnimInput input = KindInput(1.0, quick);
                for (int i = 0; i < 240; i++) a.Update(Dt, input);
                return a.Cadence;
            }
            double trooper = Cadence("Enemy", 1.0, 1.0);
            Assert.Greater(Cadence("EnemyRunner", 0.85, 1.5), trooper * 1.1);
            Assert.Less(Cadence("EnemyBruiser", 1.3, 0.8), trooper * 0.8);
            Assert.Less(Cadence("EnemyMarksman", 1.2, 0.9), trooper * 0.9);
        }

        [Test]
        public void EachKind_StandsItsOwnWay()
        {
            BodyPose Settled(BodyStyle style, out double fidget)
            {
                var a = new BodyAnimator(5, style);
                for (int i = 0; i < 120; i++) a.Update(Dt, Standing(0));
                a.Pose.World(BodyBone.Head, out Vec3d start, out _);
                fidget = 0;
                for (int i = 0; i < 600; i++)
                {
                    a.Update(Dt, Standing(0));
                    a.Pose.World(BodyBone.Head, out Vec3d head, out _);
                    fidget = Math.Max(fidget, Vec3d.Distance(head, start));
                }
                return a.Pose;
            }
            double Feet(BodyPose p) => Math.Abs(Ankle(p, BodySkeleton.Right).X - Ankle(p, BodySkeleton.Left).X);
            double Hands(BodyPose p)
            {
                p.World(BodyBone.LeftHand, out Vec3d l, out _);
                p.World(BodyBone.RightHand, out Vec3d r, out _);
                return Math.Abs(r.X - l.X);
            }
            double Hips(BodyPose p) { p.World(BodyBone.Hips, out Vec3d h, out _); return h.Y; }
            double HeadAhead(BodyPose p)
            {
                p.World(BodyBone.Head, out Vec3d head, out _);
                p.World(BodyBone.Hips, out Vec3d hips, out _);
                return head.Z - hips.Z;
            }

            BodyPose trooper = Settled(BodyStyle.Trooper, out double trooperFidget);
            double feet = Feet(trooper), hands = Hands(trooper), hips = Hips(trooper), ahead = HeadAhead(trooper);

            BodyPose bruiser = Settled(BodyStyle.Bruiser, out _);
            Assert.Greater(Feet(bruiser), feet * 1.35, "the bruiser plants its feet wide");
            Assert.Greater(Hands(bruiser), hands + 0.2, "and carries its arms out round its bulk");

            BodyPose runner = Settled(BodyStyle.Runner, out _);
            Assert.Less(Hips(runner), hips - 0.02, "the runner stands with its knees bent");
            Assert.Greater(HeadAhead(runner), ahead + 0.04, "and leans the way it means to go");

            BodyPose marksman = Settled(BodyStyle.Marksman, out double marksmanFidget);
            Assert.Less(Feet(marksman), feet, "the marksman stands with its feet together");
            Assert.Less(marksmanFidget, trooperFidget * 0.5, "and hardly moves");
            Assert.Greater(marksmanFidget, 0.0005, "but is not a statue");
        }

        [Test]
        public void AStyle_KeepsItsNumbersWithinReason()
        {
            var wild = new BodyStyle(lean: 400, hunch: double.NaN, armsOut: -50, stance: 0, stride: 99, idle: -1, crouch: 5);
            Assert.LessOrEqual(wild.Lean, 20.0);
            Assert.GreaterOrEqual(wild.ArmsOut, 0.0);
            Assert.GreaterOrEqual(wild.Stance, 0.5);
            Assert.LessOrEqual(wild.Stride, 1.5);
            Assert.GreaterOrEqual(wild.Idle, 0.0);
            Assert.LessOrEqual(wild.Crouch, 0.12);
            var a = new BodyAnimator(1, wild) { Size = -3 };
            Assert.Greater(a.Size, 0.0);
            for (int i = 0; i < 200; i++) a.Update(Dt, Standing(Sprint));
            AssertSound(a.Pose, "a wild style");
        }
    }
}
