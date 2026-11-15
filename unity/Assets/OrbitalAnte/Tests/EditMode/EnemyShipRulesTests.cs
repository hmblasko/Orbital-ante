using System;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Enemies;

namespace OrbitalAnte.Tests.EditMode
{
    public class EnemyShipRulesTests
    {
        static EnemyShipSample Sample() => new EnemyShipSample
        {
            Position = new Vec3d(0, 30, 0),
            TargetPoint = new Vec3d(25, 30, 0),
            GroundPoint = Vec3d.Zero,
            Up = Vec3d.Up,
            Forward = Vec3d.Forward,
            Gravity = new Vec3d(0, -9, 0),
            PilotPresent = true,
            CrewAboard = true,
        };

        static void Near(Vec3d expected, Vec3d actual, double tolerance = 1e-8)
        {
            Assert.LessOrEqual(Vec3d.Distance(expected, actual), tolerance);
        }

        static void Finite(Vec3d value)
        {
            Assert.IsFalse(double.IsNaN(value.X) || double.IsInfinity(value.X));
            Assert.IsFalse(double.IsNaN(value.Y) || double.IsInfinity(value.Y));
            Assert.IsFalse(double.IsNaN(value.Z) || double.IsInfinity(value.Z));
        }

        [Test]
        public void AtTheHoverTarget_EnginesOnlyCounterGravity()
        {
            EnemyShipSample sample = Sample();
            sample.TargetPoint = sample.Position;
            EnemyShipOrder order = EnemyShipRules.Guide(EnemyShipStage.Hover, sample);
            Assert.IsTrue(order.Valid);
            Assert.IsTrue(order.Arrived);
            Near(-sample.Gravity, order.Acceleration);
            Assert.IsFalse(order.CanDisembark);
        }

        [Test]
        public void ApproachBrakesExcessClosingSpeed_AndCancelsSidewaysDrift()
        {
            EnemyShipSample sample = Sample();
            sample.Velocity = new Vec3d(35, 0, 10);
            EnemyShipOrder order = EnemyShipRules.Guide(EnemyShipStage.Approach, sample);
            Assert.Less(order.Acceleration.X, 0);
            Assert.Less(order.Acceleration.Z, 0);
            Assert.LessOrEqual(order.Acceleration.Magnitude, new EnemyShipTuning().acceleration + 1e-8);
        }

        [Test]
        public void TargetsUseRealTerrainHeight_AndCannotBeSetBelowItsClearance()
        {
            EnemyShipSample sample = Sample();
            sample.GroundPoint = new Vec3d(0, 250, 0);
            sample.Position = new Vec3d(0, 254, 0);
            sample.TargetPoint = new Vec3d(0, 200, 0);
            var tuning = new EnemyShipTuning { groundClearance = 6 };
            EnemyShipOrder order = EnemyShipRules.Guide(EnemyShipStage.Land, sample, tuning);
            Assert.AreEqual(256, order.Target.Y, 1e-8);
            Assert.Greater(order.Acceleration.Y, -sample.Gravity.Y, "below clearance, climb as well as holding against gravity");
        }

        [Test]
        public void LandingBrakesDescentBeforeTheGround_EvenWithADistantHorizontalTarget()
        {
            EnemyShipSample sample = Sample();
            sample.Position = new Vec3d(0, 6, 0);
            sample.TargetPoint = new Vec3d(100, 0, 0);
            sample.Velocity = new Vec3d(0, -10, 0);
            EnemyShipOrder order = EnemyShipRules.Guide(EnemyShipStage.Land, sample);
            Assert.Greater(order.Acceleration.Y, -sample.Gravity.Y);
            Assert.IsFalse(order.Arrived);
        }

        [Test]
        public void ASharedMovingFrame_DoesNotChangeTheFlightOrder()
        {
            EnemyShipSample sample = Sample();
            sample.Velocity = new Vec3d(2, 1, 0);
            sample.TargetVelocity = new Vec3d(1, 0, 0);
            EnemyShipOrder first = EnemyShipRules.Guide(EnemyShipStage.Approach, sample);
            var offset = new Vec3d(5000, -4000, 3000);
            var velocity = new Vec3d(100, 200, -300);
            sample.Position += offset;
            sample.TargetPoint += offset;
            sample.GroundPoint += offset;
            sample.Velocity += velocity;
            sample.TargetVelocity += velocity;
            EnemyShipOrder moved = EnemyShipRules.Guide(EnemyShipStage.Approach, sample);
            Near(first.Acceleration, moved.Acceleration);
            Near(first.Target + offset, moved.Target);
            Near(first.Forward, moved.Forward);
        }

        [Test]
        public void GuidanceFollowsTheGroundNormal_OnASidewaysWorld()
        {
            EnemyShipSample sample = Sample();
            EnemyShipOrder first = EnemyShipRules.Guide(EnemyShipStage.Approach, sample);
            const double angle = 1.3;
            Vec3d axis = new Vec3d(1, 2, 3).Normalized;
            sample.Position = sample.Position.RotatedAround(axis, angle);
            sample.TargetPoint = sample.TargetPoint.RotatedAround(axis, angle);
            sample.Up = sample.Up.RotatedAround(axis, angle);
            sample.Gravity = sample.Gravity.RotatedAround(axis, angle);
            sample.Forward = sample.Forward.RotatedAround(axis, angle);
            EnemyShipOrder rotated = EnemyShipRules.Guide(EnemyShipStage.Approach, sample);
            Near(first.Acceleration.RotatedAround(axis, angle), rotated.Acceleration);
            Near(first.Forward.RotatedAround(axis, angle), rotated.Forward);
            Near(first.Up.RotatedAround(axis, angle), rotated.Up);
            Assert.AreEqual(0, Vec3d.Dot(rotated.Forward, rotated.Up), 1e-8);
        }

        [Test]
        public void NobodyInTheSeat_MeansNoEngineCommand()
        {
            EnemyShipSample sample = Sample();
            sample.PilotPresent = false;
            EnemyShipOrder order = EnemyShipRules.Guide(EnemyShipStage.Approach, sample);
            Assert.IsTrue(order.Valid);
            Near(Vec3d.Zero, order.Acceleration);
            Assert.IsFalse(order.Arrived);
        }

        [Test]
        public void LandingOnlyFinishesAfterGroundContact_AndSettling()
        {
            EnemyShipSample sample = Sample();
            sample.TargetPoint = sample.Position;
            Assert.IsFalse(EnemyShipRules.Guide(EnemyShipStage.Land, sample).Arrived);
            sample.Grounded = true;
            EnemyShipOrder grounded = EnemyShipRules.Guide(EnemyShipStage.Land, sample);
            Assert.IsTrue(grounded.Arrived);
            Near(Vec3d.Zero, grounded.Acceleration);
            sample.Velocity = Vec3d.Right * 8;
            Assert.IsFalse(EnemyShipRules.Guide(EnemyShipStage.Land, sample).Arrived);
        }

        [Test]
        public void DisembarkingNeedsOpenHatch_Crew_GroundContact_AndLowRelativeSpeed()
        {
            EnemyShipSample sample = Sample();
            sample.Grounded = true;
            Assert.IsFalse(EnemyShipRules.Guide(EnemyShipStage.Disembark, sample).CanDisembark);
            sample.HatchOpen = true;
            Assert.IsTrue(EnemyShipRules.Guide(EnemyShipStage.Disembark, sample).CanDisembark);
            sample.CrewAboard = false;
            Assert.IsFalse(EnemyShipRules.Guide(EnemyShipStage.Disembark, sample).CanDisembark);
            sample.CrewAboard = true;
            sample.Grounded = false;
            Assert.IsFalse(EnemyShipRules.Guide(EnemyShipStage.Disembark, sample).CanDisembark);
            sample.Grounded = true;
            sample.Velocity = Vec3d.Right * 3;
            Assert.IsFalse(EnemyShipRules.Guide(EnemyShipStage.Disembark, sample).CanDisembark);
            sample.TargetVelocity = sample.Velocity;
            Assert.IsTrue(EnemyShipRules.Guide(EnemyShipStage.Disembark, sample).CanDisembark);
        }

        [Test]
        public void InvalidSamplesCannotEmitAForceOrClaimArrival()
        {
            EnemyShipSample sample = Sample();
            sample.TargetPoint = new Vec3d(double.NaN, 0, 0);
            EnemyShipOrder invalid = EnemyShipRules.Guide(EnemyShipStage.Approach, sample);
            Assert.IsFalse(invalid.Valid);
            Assert.IsFalse(invalid.Arrived);
            Near(Vec3d.Zero, invalid.Acceleration);
            sample = Sample();
            sample.Velocity = new Vec3d(double.PositiveInfinity, 0, 0);
            Assert.IsFalse(EnemyShipRules.Guide(EnemyShipStage.Approach, sample).Valid);
        }

        [Test]
        public void BadTuningAndDegenerateLookDirections_StillProduceFiniteBoundedOrders()
        {
            EnemyShipSample sample = Sample();
            sample.TargetPoint = sample.Position;
            sample.Up = sample.Forward = sample.Gravity = Vec3d.Zero;
            var tuning = new EnemyShipTuning
            {
                acceleration = float.NaN, maxSpeed = float.PositiveInfinity, groundClearance = -4,
                settleSeconds = 0, settleSpeed = float.NaN, arrivedDistance = -1,
            };
            EnemyShipOrder order = EnemyShipRules.Guide(EnemyShipStage.Hover, sample, tuning);
            Assert.IsTrue(order.Valid);
            Finite(order.Acceleration);
            Assert.AreEqual(1, order.Up.Magnitude, 1e-8);
            Assert.AreEqual(1, order.Forward.Magnitude, 1e-8);
            Assert.AreEqual(0, Vec3d.Dot(order.Up, order.Forward), 1e-8);
        }

        [Test]
        public void LargeFiniteFrameErrors_AreBounded_AndOverflowIsRejected()
        {
            EnemyShipSample sample = Sample();
            sample.TargetPoint = new Vec3d(1e200, 30, 0);
            EnemyShipOrder large = EnemyShipRules.Guide(EnemyShipStage.Approach, sample);
            Assert.IsTrue(large.Valid);
            Finite(large.Acceleration);
            Assert.LessOrEqual(large.Acceleration.Magnitude, new EnemyShipTuning().acceleration + 1e-8);
            sample.Position = new Vec3d(-double.MaxValue, 30, 0);
            sample.TargetPoint = new Vec3d(double.MaxValue, 30, 0);
            EnemyShipOrder overflow = EnemyShipRules.Guide(EnemyShipStage.Approach, sample);
            Assert.IsFalse(overflow.Valid);
            Near(Vec3d.Zero, overflow.Acceleration);
        }

        [Test]
        public void HoverGuidanceSettlesInAPlainPhysicsIntegration()
        {
            EnemyShipSample sample = Sample();
            sample.Position = new Vec3d(-20, 50, 10);
            sample.TargetPoint = new Vec3d(0, 15, 0);
            const double dt = 0.02;
            for (int i = 0; i < 1500; i++)
            {
                EnemyShipOrder order = EnemyShipRules.Guide(EnemyShipStage.Hover, sample);
                Assert.IsTrue(order.Valid);
                sample.Velocity += (order.Acceleration + sample.Gravity) * dt;
                sample.Position += sample.Velocity * dt;
            }
            Assert.Less(Vec3d.Distance(sample.Position, sample.TargetPoint), 0.05);
            Assert.Less(sample.Velocity.Magnitude, 0.05);
        }

        [Test]
        public void LandingLetsGravityMakeContact_ThenSettlesInAPlainPhysicsIntegration()
        {
            EnemyShipSample sample = Sample();
            var tuning = new EnemyShipTuning();
            sample.Position = new Vec3d(0, 25, 0);
            sample.TargetPoint = new Vec3d(0, tuning.groundClearance, 0);
            const double dt = 0.02;
            bool touched = false, arrived = false;
            for (int i = 0; i < 2000; i++)
            {
                EnemyShipOrder order = EnemyShipRules.Guide(EnemyShipStage.Land, sample, tuning);
                Assert.IsTrue(order.Valid);
                arrived |= order.Arrived;
                sample.Velocity += (order.Acceleration + sample.Gravity) * dt;
                sample.Position += sample.Velocity * dt;
                if (sample.Position.Y <= tuning.groundClearance)
                {
                    touched = true;
                    sample.Grounded = true;
                    sample.Position = new Vec3d(sample.Position.X, tuning.groundClearance, sample.Position.Z);
                    sample.Velocity = new Vec3d(sample.Velocity.X, 0, sample.Velocity.Z);
                }
            }
            Assert.IsTrue(touched, "hover compensation must not suspend the feet above the ground");
            Assert.IsTrue(arrived, "actual contact and low relative speed finish the landing");
        }

        [Test]
        public void StableGroundContactCanFinishOffThePlannedPoint()
        {
            EnemyShipSample sample = Sample();
            sample.Position = new Vec3d(2, 5.6, 0);
            sample.TargetPoint = new Vec3d(0, 5.6, 0);
            sample.Grounded = true;
            EnemyShipOrder order = EnemyShipRules.Guide(EnemyShipStage.Land, sample);
            Assert.IsTrue(order.Arrived);
            Near(Vec3d.Zero, order.Acceleration);
        }

        [Test]
        public void FinalTouchdownStillBrakesAFastDescent_AndRecoversBelowGround()
        {
            EnemyShipSample sample = Sample();
            sample.Position = new Vec3d(0, 6, 0);
            sample.TargetPoint = new Vec3d(0, 5.6, 0);
            sample.Velocity = new Vec3d(0, -10, 0);
            Assert.Greater(EnemyShipRules.Guide(EnemyShipStage.Land, sample).Acceleration.Y, -sample.Gravity.Y);
            sample.Position = new Vec3d(0, 4, 0);
            sample.Velocity = Vec3d.Zero;
            Assert.Greater(EnemyShipRules.Guide(EnemyShipStage.Land, sample).Acceleration.Y, -sample.Gravity.Y);
        }

        [Test]
        public void PlacementUsesItsSeededStream_AndStaysInTheTangentPlane()
        {
            Vec3d up = new Vec3d(2, 3, -1).Normalized;
            SeededRandom a = SeededRandom.ForStream(5, "enemy.ships");
            SeededRandom b = SeededRandom.ForStream(5, "enemy.ships");
            for (int i = 0; i < 20; i++)
            {
                Vec3d offset = EnemyShipRules.SeededOffset(a, up, 40);
                Assert.AreEqual(offset, EnemyShipRules.SeededOffset(b, up, 40));
                Assert.AreEqual(40, offset.Magnitude, 1e-8);
                Assert.AreEqual(0, Vec3d.Dot(offset, up), 1e-8);
            }
            Assert.AreNotEqual(EnemyShipRules.SeededOffset(SeededRandom.ForStream(5, "enemy.ships"), up, 40),
                EnemyShipRules.SeededOffset(SeededRandom.ForStream(6, "enemy.ships"), up, 40));
            Near(Vec3d.Zero, EnemyShipRules.SeededOffset(a, Vec3d.Zero, double.NaN));
            Assert.Throws<ArgumentNullException>(() => EnemyShipRules.SeededOffset(null, up, 40));
        }
    }
}
