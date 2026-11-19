using System;
using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// The jointed body's rules (Matthew, 15 Nov 2026: "a very realistic ragdoll ... soft limits with tension"):
    /// how far each joint bends, that its stop is a spring and not a wall, that the muscles brace and then let go,
    /// and that only a few bodies are ever jointed at once.
    /// </summary>
    public class RagdollRulesTests
    {
        const double Deg = Math.PI / 180.0;

        static IEnumerable<BodyPartId> Jointed()
        {
            for (int i = 0; i < BodyLayout.Count; i++)
                if (RagdollRules.HasJoint((BodyPartId)i)) yield return (BodyPartId)i;
        }

        [Test]
        public void EveryPartButTheTorso_HasAJoint_WithARealRange()
        {
            Assert.IsFalse(RagdollRules.HasJoint(BodyPartId.Torso));
            int count = 0;
            foreach (BodyPartId id in Jointed())
            {
                count++;
                RagdollJoint j = RagdollRules.JointFor(id);
                Assert.Less(j.Low, j.High, id + " main");
                Assert.Less(j.SecondLow, j.SecondHigh, id + " second");
                Assert.Less(j.ThirdLow, j.ThirdHigh, id + " third");
                Assert.AreEqual(1.0, j.Axis.Magnitude, 1e-9);
                Assert.AreEqual(1.0, j.Second.Magnitude, 1e-9);
                Assert.AreEqual(0.0, Vec3d.Dot(j.Axis, j.Second), 1e-9, id + ": the two axes are at right angles");
                // No joint of a person turns right round: the widest (the shoulder forward and back) is under 180.
                Assert.LessOrEqual(j.High - j.Low, 175.0, id.ToString());
                Assert.LessOrEqual(j.SecondHigh - j.SecondLow, 175.0, id.ToString());
                Assert.LessOrEqual(j.ThirdHigh - j.ThirdLow, 175.0, id.ToString());
            }
            Assert.AreEqual(13, count);
        }

        [Test]
        public void LeftAndRight_AreMirrorImages()
        {
            var pairs = new[]
            {
                (BodyPartId.LeftUpperArm, BodyPartId.RightUpperArm), (BodyPartId.LeftForearm, BodyPartId.RightForearm),
                (BodyPartId.LeftHand, BodyPartId.RightHand), (BodyPartId.LeftThigh, BodyPartId.RightThigh),
                (BodyPartId.LeftShin, BodyPartId.RightShin), (BodyPartId.LeftFoot, BodyPartId.RightFoot),
            };
            foreach (var (left, right) in pairs)
            {
                // Mirror a turn in the plane x = 0: its X part stays, its Y and Z parts change sign.
                foreach (var probe in Probes())
                {
                    QuatD mirrored = new QuatD(probe.X, -probe.Y, -probe.Z, probe.W);
                    Assert.AreEqual(RagdollRules.Beyond(right, probe), RagdollRules.Beyond(left, mirrored), 1e-6, left + " against " + right);
                }
            }
        }

        static IEnumerable<QuatD> Probes()
        {
            var axes = new[] { Vec3d.Right, Vec3d.Up, Vec3d.Forward };
            foreach (Vec3d axis in axes)
                for (int d = -170; d <= 170; d += 17)
                    yield return QuatD.AngleAxis(axis, d * Deg);
        }

        [Test]
        public void TheAnimatorsOwnBends_AreInsideTheRange()
        {
            // The same turns BodyAnimator gives: a knee folds about X, an elbow about Y (the other way on each
            // side), a thigh swings forward with a negative turn about X, an arm comes down about Z.
            Assert.AreEqual(0, RagdollRules.Beyond(BodyPartId.LeftShin, QuatD.AngleAxis(Vec3d.Right, 95 * Deg)), 1e-9);
            Assert.AreEqual(0, RagdollRules.Beyond(BodyPartId.RightShin, QuatD.AngleAxis(Vec3d.Right, 60 * Deg)), 1e-9);
            Assert.AreEqual(0, RagdollRules.Beyond(BodyPartId.LeftForearm, QuatD.AngleAxis(Vec3d.Up, 92 * Deg)), 1e-9);
            Assert.AreEqual(0, RagdollRules.Beyond(BodyPartId.RightForearm, QuatD.AngleAxis(Vec3d.Up, -92 * Deg)), 1e-9);
            Assert.AreEqual(0, RagdollRules.Beyond(BodyPartId.RightThigh, QuatD.AngleAxis(Vec3d.Right, -70 * Deg)), 1e-9);
            Assert.AreEqual(0, RagdollRules.Beyond(BodyPartId.LeftUpperArm, QuatD.AngleAxis(Vec3d.Forward, 78 * Deg)), 1e-9);
            Assert.AreEqual(0, RagdollRules.Beyond(BodyPartId.RightUpperArm, QuatD.AngleAxis(Vec3d.Forward, -78 * Deg)), 1e-9);
            // Standing in the T the model is built in is inside every range too.
            foreach (BodyPartId id in Jointed()) Assert.AreEqual(0, RagdollRules.Beyond(id, QuatD.Identity), 1e-9, id.ToString());
        }

        [Test]
        public void AHinge_StopsExactlyAtTheEndsOfItsRange()
        {
            foreach (BodyPartId id in new[] { BodyPartId.LeftShin, BodyPartId.RightShin, BodyPartId.LeftForearm, BodyPartId.RightForearm })
            {
                RagdollJoint j = RagdollRules.JointFor(id);
                Assert.AreEqual(0, RagdollRules.Beyond(id, QuatD.AngleAxis(j.Axis, j.High * Deg)), 1e-6, id.ToString());
                Assert.AreEqual(0, RagdollRules.Beyond(id, QuatD.AngleAxis(j.Axis, j.Low * Deg)), 1e-6, id.ToString());
                Assert.AreEqual(10, RagdollRules.Beyond(id, QuatD.AngleAxis(j.Axis, (j.High + 10) * Deg)), 1e-6, id.ToString());
                Assert.AreEqual(10, RagdollRules.Beyond(id, QuatD.AngleAxis(j.Axis, (j.Low - 10) * Deg)), 1e-6, id.ToString());
            }
        }

        [Test]
        public void EveryPoseTheAnimatorMakes_IsNearlyInsideTheRange()
        {
            // A body is handed to its joints in whatever pose it was in. Walking, running and hunched, no joint
            // may be far outside its range, or the stops would yank it the moment it died.
            foreach (double speed in new[] { 0.0, 2.0, 4.6 })
            foreach (double hunch in new[] { 0.0, 1.0 })
            {
                var anim = new BodyAnimator();
                var input = new BodyAnimInput
                {
                    State = MovementState.Intact, ForwardSpeed = speed, Moving = speed > 0.01, WalkSpeed = 2.0, SprintSpeed = 4.6,
                    LeftArmWorks = true, RightArmWorks = true, GunSide = -1, Tactical = hunch,
                };
                for (int step = 0; step < 240; step++)
                {
                    anim.Update(1.0 / 60.0, input);
                    foreach (BodyPartId id in Jointed())
                    {
                        if (id == BodyPartId.Head) continue;
                        QuatD local = anim.Pose.Local[(int)BodySkeleton.BoneOf(id)];
                        if (step < 12) continue;   // the first frames ease in from standing
                        Assert.Less(RagdollRules.Beyond(id, local), 25.0, id + " at speed " + speed + ", hunch " + hunch + ", step " + step);
                    }
                }
            }
        }

        [Test]
        public void AKneeAndAnElbow_DoNotBendBackwards()
        {
            Assert.Greater(RagdollRules.Beyond(BodyPartId.RightShin, QuatD.AngleAxis(Vec3d.Right, -30 * Deg)), 20);
            Assert.Greater(RagdollRules.Beyond(BodyPartId.RightForearm, QuatD.AngleAxis(Vec3d.Up, 30 * Deg)), 20);
            Assert.Greater(RagdollRules.Beyond(BodyPartId.LeftForearm, QuatD.AngleAxis(Vec3d.Up, -30 * Deg)), 20);
            // And they do not twist or bend sideways by more than a little.
            Assert.Greater(RagdollRules.Beyond(BodyPartId.RightShin, QuatD.AngleAxis(Vec3d.Forward, 25 * Deg)), 15);
        }

        [Test]
        public void TheRelaxedPose_IsTheMiddleOfEveryRange()
        {
            foreach (BodyPartId id in Jointed())
            {
                RagdollRules.Measure(id, RagdollRules.Relaxed(id), out double twist, out double second, out double third);
                Assert.AreEqual(0, twist, 1e-6, id + " twist");
                Assert.AreEqual(0, second, 1e-6, id + " second");
                Assert.AreEqual(0, third, 1e-6, id + " third");
                // A turn about the main axis from there is all twist, and is allowed exactly half the range each way.
                RagdollJoint j = RagdollRules.JointFor(id);
                QuatD turned = RagdollRules.Relaxed(id) * QuatD.AngleAxis(j.Axis, j.Half * 0.5 * Deg);
                RagdollRules.Measure(id, turned, out twist, out second, out third);
                Assert.AreEqual(j.Half * 0.5, twist, 1e-6, id + " twist");
                Assert.AreEqual(0, RagdollRules.Beyond(id, turned), 1e-9);
                Assert.AreEqual(5, RagdollRules.Beyond(id, RagdollRules.Relaxed(id) * QuatD.AngleAxis(j.Axis, (j.Half + 5) * Deg)), 1e-6);
            }
        }

        [Test]
        public void TheStop_IsASpring_NotAWall()
        {
            var t = new RagdollTuning();
            BodyPartId knee = BodyPartId.RightShin;
            RagdollJoint j = RagdollRules.JointFor(knee);
            double spring = RagdollRules.LimitSpring(knee, t), damper = RagdollRules.LimitDamper(knee, t);

            // Inside the range with no muscle: nothing pushes at all.
            Assert.AreEqual(0, RagdollRules.StopTorque(60, 0, j.Low, j.High, spring, damper, 0, 0), 1e-12);
            // Past the end the push back grows with how far past: twice as far, twice as hard.
            double five = RagdollRules.StopTorque(j.High + 5, 0, j.Low, j.High, spring, damper, 0, 0);
            double ten = RagdollRules.StopTorque(j.High + 10, 0, j.Low, j.High, spring, damper, 0, 0);
            Assert.Less(five, 0);
            Assert.AreEqual(2.0, ten / five, 1e-9);
            double back = RagdollRules.StopTorque(j.Low - 5, 0, j.Low, j.High, spring, damper, 0, 0);
            Assert.Greater(back, 0, "and the other end pushes the other way");
            // Moving further into the stop is resisted more than resting in it.
            Assert.Less(RagdollRules.StopTorque(j.High + 5, 200, j.Low, j.High, spring, damper, 0, 0), five);
        }

        [Test]
        public void ALimbThrownAtItsStop_GoesALittlePast_ComesBack_AndSettles()
        {
            // One joint worked out step by step: a shin swung hard at full bend.
            var t = new RagdollTuning();
            BodyPartId knee = BodyPartId.RightShin;
            RagdollJoint j = RagdollRules.JointFor(knee);
            double inertia = RagdollRules.SwingInertia(knee);
            double spring = RagdollRules.LimitSpring(knee, t), damper = RagdollRules.LimitDamper(knee, t);
            double tone = RagdollRules.ToneSpring(knee, t);

            double angle = j.High - 10, rate = 600, furthest = angle;
            const double dt = 1.0 / 600.0;
            for (int i = 0; i < 6000; i++)
            {
                double drag = RagdollRules.JointDamper(knee, t, 10.0);
                double torque = RagdollRules.StopTorque(angle, rate, j.Low, j.High, spring, damper, tone, drag);
                rate += torque / inertia / Deg * dt;
                angle += rate * dt;
                furthest = Math.Max(furthest, angle);
            }
            Assert.Greater(furthest, j.High + 2, "a soft stop gives: it goes past the end");
            Assert.Less(furthest, j.High + 30, "but not far past");
            Assert.LessOrEqual(angle, j.High + 0.5, "and it comes back inside");
            Assert.GreaterOrEqual(angle, j.Low - 0.5);
            Assert.Less(Math.Abs(rate), 20, "and comes to rest");
        }

        [Test]
        public void TheMuscles_BraceAtFirst_ThenLetGo()
        {
            var t = new RagdollTuning();
            Assert.AreEqual(t.braceDamping, RagdollRules.Brace(t, 0), 1e-9);
            Assert.AreEqual(1.0, RagdollRules.Brace(t, t.braceSeconds), 1e-9);
            Assert.AreEqual(1.0, RagdollRules.Brace(t, 30), 1e-9);
            double last = double.MaxValue;
            for (int i = 0; i <= 20; i++)
            {
                double now = RagdollRules.Brace(t, t.braceSeconds * i / 20.0);
                Assert.LessOrEqual(now, last + 1e-12, "never tightens again");
                last = now;
            }
            foreach (BodyPartId id in Jointed())
                Assert.Greater(RagdollRules.JointDamper(id, t, 0), RagdollRules.JointDamper(id, t, 5), id.ToString());
        }

        [Test]
        public void AHeavierLimb_GetsAStifferStop_SoEveryJointFeelsAlike()
        {
            var t = new RagdollTuning();
            Assert.Greater(RagdollRules.LimitSpring(BodyPartId.LeftThigh, t), RagdollRules.LimitSpring(BodyPartId.LeftShin, t));
            Assert.Greater(RagdollRules.LimitSpring(BodyPartId.LeftShin, t), RagdollRules.LimitSpring(BodyPartId.LeftFoot, t));
            Assert.Greater(RagdollRules.LimitSpring(BodyPartId.LeftUpperArm, t), RagdollRules.LimitSpring(BodyPartId.LeftHand, t));
            // The muscle's pull is much weaker than the stop: it holds a pose, it does not stand a body up.
            foreach (BodyPartId id in Jointed())
                Assert.Less(RagdollRules.ToneSpring(id, t), RagdollRules.LimitSpring(id, t) * 0.05, id.ToString());
            // A softer dial is a softer stop.
            var soft = new RagdollTuning { limitHz = 2.5f };
            Assert.Less(RagdollRules.LimitSpring(BodyPartId.LeftShin, soft), RagdollRules.LimitSpring(BodyPartId.LeftShin, t));
        }

        [Test]
        public void OnlyAFewBodiesAreJointedAtOnce_AndEachGoesStiffAtRest()
        {
            var t = new RagdollTuning();
            var live = new List<RagdollBudget.Slot>();
            Assert.AreEqual(-1, RagdollBudget.MakeRoom(live, t));
            for (int i = 0; i < t.mostJointed; i++) live.Add(new RagdollBudget.Slot { Age = i == 1 ? 9f : 1f + i, Joints = 11 });
            Assert.AreEqual(1, RagdollBudget.MakeRoom(live, t), "the oldest goes first");
            live.RemoveAt(1);
            Assert.AreEqual(-1, RagdollBudget.MakeRoom(live, t));

            Assert.IsFalse(RagdollBudget.Done(new RagdollBudget.Slot { Age = 2f, Still = 0.2f, Joints = 5 }, t));
            Assert.IsTrue(RagdollBudget.Done(new RagdollBudget.Slot { Age = 2f, Still = t.stillSeconds, Joints = 5 }, t));
            Assert.IsTrue(RagdollBudget.Done(new RagdollBudget.Slot { Age = t.jointedSeconds, Still = 0f, Joints = 5 }, t));
            Assert.IsTrue(RagdollBudget.Done(new RagdollBudget.Slot { Age = 0f, Still = 0f, Joints = 0 }, t));
        }

        [Test]
        public void StiffBodies_AreTheDefault_AndOnlyTheDrifterIsJointed()
        {
            Assert.IsFalse(new RagdollTuning().jointed);
            foreach (var spec in OrbitalAnte.Enemies.EnemyRoster.Defaults())
                Assert.AreEqual(spec.id == "Drifter", spec.jointed, spec.id);
        }
    }
}
