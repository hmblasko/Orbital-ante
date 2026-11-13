using System;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Space;

namespace OrbitalAnte.Tests
{
    /// <summary>Production guidance in a closed loop, with no Unity, physics stand-ins, or snapped velocity.</summary>
    public class FlightNavigationTests
    {
        static void Close(Vec3d expected, Vec3d actual, double tolerance = 1e-8)
            => Assert.LessOrEqual(Vec3d.Distance(expected, actual), tolerance);

        [Test]
        public void LookingElsewhereAndCancelingAnAssistKeepTheExplicitTarget()
        {
            var navigation = new FlightNavigation();
            Assert.IsFalse(navigation.ToggleAutopilot());
            navigation.ToggleTarget(3);
            Assert.IsTrue(navigation.ToggleAutopilot());
            navigation.CancelAssist();
            Assert.AreEqual(3, navigation.Target);
            Assert.IsTrue(navigation.ToggleVelocityLock(7));
            Assert.AreEqual(3, navigation.ReferenceBody);
            navigation.Validate(10);
            Assert.AreEqual(3, navigation.Target);
            Assert.AreEqual(3, navigation.ReferenceBody);
        }

        [Test]
        public void ASecondLockPressOrEmptySpaceClearsTheTargetAndAssist()
        {
            var navigation = new FlightNavigation();
            navigation.ToggleTarget(3);
            navigation.ToggleAutopilot();
            navigation.ToggleTarget(3);
            Assert.AreEqual(-1, navigation.Target);
            Assert.AreEqual(FlightAssistMode.Manual, navigation.Mode);
            navigation.ToggleTarget(4);
            navigation.ToggleVelocityLock(1);
            navigation.ToggleTarget(-1);
            Assert.AreEqual(-1, navigation.Target);
            Assert.AreEqual(-1, navigation.ReferenceBody);
            Assert.AreEqual(FlightAssistMode.Manual, navigation.Mode);
        }

        [Test]
        public void ChangingTheLockCancelsThePreviousApproach()
        {
            var navigation = new FlightNavigation();
            navigation.ToggleTarget(3);
            navigation.ToggleAutopilot();
            navigation.ToggleTarget(4);
            Assert.AreEqual(4, navigation.Target);
            Assert.AreEqual(FlightAssistMode.Manual, navigation.Mode);
            Assert.AreEqual(-1, navigation.ReferenceBody);
        }

        [Test]
        public void VelocityLockCapturesItsFallbackOnceAndArrivalBecomesAHold()
        {
            var navigation = new FlightNavigation();
            navigation.ToggleVelocityLock(2);
            navigation.Validate(8);
            Assert.AreEqual(2, navigation.ReferenceBody);
            Assert.IsFalse(navigation.ToggleVelocityLock(5));
            Assert.AreEqual(FlightAssistMode.Manual, navigation.Mode);
            navigation.ToggleTarget(4);
            navigation.ToggleAutopilot();
            navigation.CompleteApproach();
            Assert.AreEqual(FlightAssistMode.VelocityLock, navigation.Mode);
            Assert.AreEqual(4, navigation.ReferenceBody);
            Assert.IsTrue(navigation.Arrived);
            navigation.CancelAssist();
            Assert.IsFalse(navigation.Arrived);
            Assert.AreEqual(4, navigation.Target);
        }

        [Test]
        public void RemovedBodiesCancelWithoutChoosingAnotherReference()
        {
            var navigation = new FlightNavigation();
            navigation.ToggleTarget(5);
            navigation.ToggleAutopilot();
            navigation.Validate(5);
            Assert.AreEqual(-1, navigation.Target);
            Assert.AreEqual(FlightAssistMode.Manual, navigation.Mode);
            navigation.ToggleVelocityLock(4);
            navigation.Validate(4);
            Assert.AreEqual(FlightAssistMode.Manual, navigation.Mode);
            Assert.AreEqual(-1, navigation.ReferenceBody);
        }

        [Test]
        public void VelocityHoldCountersGravityAndTargetAccelerationWithoutLosingItsThrustCap()
        {
            Vec3d velocity = new Vec3d(90, -40, 12);
            var disturbance = new Vec3d(0, -9.8, 0);
            const double dt = 0.02;
            for (int i = 0; i < 500; i++)
            {
                FlightCommand command = FlightGuidance.Match(velocity, disturbance, 30, 0.5, dt);
                Assert.IsTrue(command.Valid);
                Assert.LessOrEqual(command.Acceleration.Magnitude, 30.00000001);
                velocity += (command.Acceleration + disturbance) * dt;
            }
            Assert.Less(velocity.Magnitude, 0.001);
            FlightCommand settled = FlightGuidance.Match(velocity, disturbance, 30, 0.5, dt);
            Close(-disturbance, settled.Acceleration, 0.002);
        }

        [TestCase(false, 0.02)]
        [TestCase(true, 0.02)]
        [TestCase(true, 0.05)]
        public void ApproachBrakesSideslipAndArrivesOutsideTheSurfaceOfAMovingAcceleratingBody(bool moving, double dt)
        {
            const double standOff = 650, surface = 500, authority = 30;
            var targetPosition = new Vec3d(20000, -10000, 3000);
            Vec3d targetVelocity = moving ? new Vec3d(220, 35, -70) : Vec3d.Zero;
            Vec3d craftPosition = targetPosition + new Vec3d(-5000, 220, -80);
            Vec3d craftVelocity = targetVelocity + new Vec3d(250, 65, -25);
            bool arrived = false, braking = false;
            double closest = double.MaxValue;
            int heldSteps = 0;
            for (int i = 0; i < (int)(120 / dt); i++)
            {
                double time = i * dt;
                Vec3d targetAcceleration = moving ? new Vec3d(1.5 * Math.Cos(time * 0.07), -0.4, 0.7) : Vec3d.Zero;
                // The craft's real pull and the destination's on-rails acceleration need not be identical.
                Vec3d craftGravity = targetAcceleration + new Vec3d(0, -7, 0);
                Vec3d relative = craftVelocity - targetVelocity;
                FlightCommand command = arrived
                    ? FlightGuidance.Match(relative, craftGravity - targetAcceleration, authority, 0.5, dt)
                    : FlightGuidance.Approach(targetPosition - craftPosition, relative, craftGravity - targetAcceleration,
                        standOff, authority, 300, 0.5, 3, 0.5, dt);
                Assert.IsTrue(command.Valid, "step " + i);
                Assert.LessOrEqual(command.Acceleration.Magnitude, authority + 1e-8);
                braking |= command.Phase == AutopilotPhase.Braking;
                if (command.Phase == AutopilotPhase.Arrived)
                {
                    Assert.LessOrEqual(Math.Abs(Vec3d.Distance(targetPosition, craftPosition) - standOff), 3);
                    Assert.LessOrEqual(relative.Magnitude, 0.5);
                    arrived = true;
                }
                // Both objects use the same semi-implicit integration; only bounded acceleration moves the craft.
                targetVelocity += targetAcceleration * dt;
                craftVelocity += (craftGravity + command.Acceleration) * dt;
                targetPosition += targetVelocity * dt;
                craftPosition += craftVelocity * dt;
                closest = Math.Min(closest, Vec3d.Distance(targetPosition, craftPosition));
                if (arrived && ++heldSteps >= (int)(10 / dt)) break;
            }
            Assert.IsTrue(braking, "An initial fast approach with sideslip must enter braking.");
            Assert.IsTrue(arrived, "Approach did not settle within two simulated minutes.");
            Assert.Greater(closest, surface + 100, "The approach consumed the surface-clearance buffer.");
            Assert.Less((craftVelocity - targetVelocity).Magnitude, 0.001, "Arrival hold should continue tracking acceleration.");
            Assert.Less(Math.Abs(Vec3d.Distance(targetPosition, craftPosition) - standOff), 4);
        }

        [Test]
        public void AnInitialPositionInsideTheApproachRadiusRequestsOutwardThrust()
        {
            FlightCommand command = FlightGuidance.Approach(new Vec3d(600, 0, 0), Vec3d.Zero, Vec3d.Zero,
                650, 30, 300, 0.5, 3, 0.5, 0.02);
            Assert.IsTrue(command.Valid);
            Assert.Less(command.Acceleration.X, 0);
        }

        [Test]
        public void BrakingStartsBeforeTheFullPowerStoppingDistanceAndCannotTeleportVelocity()
        {
            var relative = new Vec3d(180, 80, 0);
            FlightCommand command = FlightGuidance.Approach(new Vec3d(1300, 0, 0), relative, Vec3d.Zero,
                650, 30, 300, 0.5, 3, 0.5, 0.02);
            Assert.IsTrue(command.Valid);
            Assert.AreEqual(AutopilotPhase.Braking, command.Phase);
            Assert.Less(command.Acceleration.X, 0);
            Assert.Less(command.Acceleration.Y, 0);
            Assert.LessOrEqual((command.Acceleration * 0.02).Magnitude, 0.60000001);
        }

        [Test]
        public void RotatingTheCoordinateBasisOrAddingACommonVelocityDoesNotChangeGuidance()
        {
            var toBody = new Vec3d(3400, -300, 900);
            var craftVelocity = new Vec3d(500, 80, -40);
            var targetVelocity = new Vec3d(300, -10, 20);
            var disturbance = new Vec3d(2, -6, 1);
            QuatD rotation = QuatD.AngleAxis(new Vec3d(1, 2, -1).Normalized, 1.7);
            var commonVelocity = new Vec3d(-900, 300, 800);
            FlightCommand original = FlightGuidance.Approach(toBody, craftVelocity - targetVelocity, disturbance, 650, 30, 300, 0.5, 3, 0.5, 0.02);
            FlightCommand changed = FlightGuidance.Approach(rotation.Rotate(toBody),
                rotation.Rotate(craftVelocity + commonVelocity) - rotation.Rotate(targetVelocity + commonVelocity),
                rotation.Rotate(disturbance), 650, 30, 300, 0.5, 3, 0.5, 0.02);
            Assert.IsTrue(original.Valid && changed.Valid);
            Close(rotation.Rotate(original.Acceleration), changed.Acceleration);
            Assert.AreEqual(original.Phase, changed.Phase);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void MissingOrInvalidAuthorityReturnsNoForce(double authority)
        {
            FlightCommand command = FlightGuidance.Match(new Vec3d(10, 0, 0), Vec3d.Zero, authority, 0.5, 0.02);
            Assert.IsFalse(command.Valid);
            Close(Vec3d.Zero, command.Acceleration);
        }

        [Test]
        public void UncounterableGravityAndNonFiniteMotionRefuseGuidance()
        {
            FlightCommand command = FlightGuidance.Match(Vec3d.Zero, new Vec3d(31, 0, 0), 30, 0.5, 0.02);
            Assert.IsFalse(command.Valid);
            Assert.IsTrue(command.InsufficientThrust);
            Close(Vec3d.Zero, command.Acceleration);
            Assert.IsFalse(FlightGuidance.Approach(new Vec3d(double.NaN, 0, 0), Vec3d.Zero, Vec3d.Zero,
                650, 30, 300, 0.5, 3, 0.5, 0.02).Valid);
        }

        [Test]
        public void OnlyBodiesTouchingTheTravelSegmentBlockTheRoute()
        {
            var start = new Vec3d(0, 0, 0);
            var end = new Vec3d(1000, 0, 0);
            Assert.IsTrue(FlightGuidance.SegmentBlocked(start, end, new Vec3d(500, 0, 0), 100));
            Assert.IsTrue(FlightGuidance.SegmentBlocked(start, end, new Vec3d(500, 100, 0), 100));
            Assert.IsFalse(FlightGuidance.SegmentBlocked(start, end, new Vec3d(500, 101, 0), 100));
            Assert.IsFalse(FlightGuidance.SegmentBlocked(start, end, new Vec3d(-200, 0, 0), 100));
            Assert.IsFalse(FlightGuidance.SegmentBlocked(start, end, new Vec3d(1200, 0, 0), 100));
            Assert.IsTrue(FlightGuidance.SegmentBlocked(start, start, start, 100));
        }
    }
}
