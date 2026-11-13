using NUnit.Framework;
using OrbitalAnte.Ship;

namespace OrbitalAnte.Tests
{
    /// <summary>Ground is optional for a belly-lift exit; the whole suit must clear the hull and be able to reboard.</summary>
    public class ShipExitRulesTests
    {
        static HatchLift OpenHatch(float reach = 4f)
        {
            var hatch = new HatchLift { Reach = reach };
            hatch.Set(true, hatchWorks: true);
            for (int i = 0; i < 120; i++) hatch.Tick(1f / 60f);
            Assert.IsTrue(hatch.Open);
            return hatch;
        }

        static void FinishDown(HatchLift hatch)
        {
            LiftRide result = LiftRide.None;
            for (int i = 0; i < 2000 && result == LiftRide.None; i++) result = hatch.Tick(1f / 60f);
            Assert.AreEqual(LiftRide.Down, result, "the rider must not wait forever for nonexistent ground");
        }

        [Test]
        public void MissingGroundReleasesBelowTheWholeSuit()
        {
            HatchLift hatch = OpenHatch();
            hatch.BeginDown(P3.Zero, float.NaN);
            FinishDown(hatch);
            Assert.IsFalse(float.IsNaN(hatch.Rider.Y));
            Assert.AreEqual(hatch.SpaceExitY, hatch.Rider.Y, 1e-4f);
            Assert.IsTrue(hatch.HasExitClearance(hatch.Rider.Y));
            Assert.IsTrue(hatch.WantOpen);
        }

        [TestCase(0.1f)]
        [TestCase(1.8f)]
        [TestCase(4f)]
        public void EvenAShortConfiguredReachClearsTheSuitAndAllowsExplicitReboarding(float reach)
        {
            HatchLift hatch = OpenHatch(reach);
            hatch.BeginDown(P3.Zero, float.NaN);
            FinishDown(hatch);
            Assert.Less(hatch.Rider.Y + ShipLayout.PersonHeight, ShipLayout.BellyY);
            Assert.IsTrue(hatch.HasExitClearance(hatch.Rider.Y));
            Assert.IsFalse(hatch.CanPickUp(hatch.Rider), "automatic pickup must not immediately undo the exit");
            Assert.IsTrue(hatch.CanPickUp(hatch.Rider, requested: true), "reboarding must not require a jetpack or stepping away");
        }

        [Test]
        public void MaximumReachStopCanExplicitlyReboardWithoutAutomaticRecapture()
        {
            HatchLift hatch = OpenHatch();
            float bottom = ShipLayout.BellyY - hatch.EffectiveReach;
            hatch.BeginDown(P3.Zero, bottom);
            FinishDown(hatch);
            Assert.AreEqual(bottom, hatch.Rider.Y, 1e-4f);
            Assert.IsFalse(hatch.CanPickUp(hatch.Rider));
            Assert.IsTrue(hatch.CanPickUp(hatch.Rider, requested: true),
                "A ground stop at maximum reach must remain boardable if that ground disappears.");
        }

        [Test]
        public void AReachableLandingStillUsesTheGroundHeight()
        {
            HatchLift hatch = OpenHatch();
            hatch.BeginDown(P3.Zero, ShipLayout.GroundY);
            FinishDown(hatch);
            Assert.AreEqual(ShipLayout.GroundY, hatch.Rider.Y, 1e-4f);
            Assert.IsTrue(hatch.HasExitClearance(hatch.Rider.Y));
        }

        [Test]
        public void DisappearingGroundContinuesTheDescentWithoutJumpingBackToTheFloor()
        {
            HatchLift hatch = OpenHatch();
            hatch.BeginDown(P3.Zero, ShipLayout.BellyY - 0.4f);
            FinishDown(hatch);
            float previous = hatch.Rider.Y;
            Assert.IsFalse(hatch.HasExitClearance(previous));
            hatch.BeginDown(hatch.Rider, float.NaN);
            Assert.AreEqual(previous, hatch.Rider.Y, 1e-4f);
            FinishDown(hatch);
            Assert.Less(hatch.Rider.Y, previous);
            Assert.IsTrue(hatch.HasExitClearance(hatch.Rider.Y));
        }

        [Test]
        public void SpaceExitCanMakeACompleteRoundTripThroughTheSameOpenHatch()
        {
            HatchLift hatch = OpenHatch();
            hatch.BeginDown(P3.Zero, float.NaN);
            FinishDown(hatch);
            Assert.IsTrue(hatch.CanPickUp(hatch.Rider, requested: true));
            hatch.BeginUp(hatch.Rider);
            LiftRide result = LiftRide.None;
            for (int i = 0; i < 2000 && result == LiftRide.None; i++) result = hatch.Tick(1f / 60f);
            Assert.AreEqual(LiftRide.Up, result);
            Assert.AreEqual(0f, hatch.Rider.Y, 1e-4f);
            Assert.IsFalse(hatch.WantOpen, "the doors close behind the returned player");
        }

        [Test]
        public void ExplicitReboardingStillRequiresTheOpenBeamAndItsReach()
        {
            HatchLift hatch = OpenHatch();
            Assert.IsFalse(hatch.CanPickUp(new P3(0f, ShipLayout.BellyY - hatch.EffectiveReach - 1f, 0f), requested: true));
            Assert.IsFalse(hatch.CanPickUp(new P3(3f, hatch.SpaceExitY, 0f), requested: true));
            hatch.Set(false, hatchWorks: true);
            Assert.IsFalse(hatch.CanPickUp(new P3(0f, hatch.SpaceExitY, 0f), requested: true), "a closing door must not start a new boarding ride");
            for (int i = 0; i < 120; i++) hatch.Tick(1f / 60f);
            Assert.IsFalse(hatch.CanPickUp(new P3(0f, hatch.SpaceExitY, 0f), requested: true));
        }
    }
}
