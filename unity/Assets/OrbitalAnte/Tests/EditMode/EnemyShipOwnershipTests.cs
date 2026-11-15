using NUnit.Framework;
using OrbitalAnte.Ship;

namespace OrbitalAnte.Tests.EditMode
{
    public class EnemyShipOwnershipTests
    {
        static HatchLift OpenHatch()
        {
            var hatch = new HatchLift();
            hatch.Set(true, true);
            hatch.Tick(1f);
            return hatch;
        }

        static P3 UnderRing => new P3(0f, ShipLayout.GroundY, 0f);

        [Test]
        public void AnOpenRequestDoesNotPermitEntryBeforeTheDoorsActuallyOpen()
        {
            var hatch = new HatchLift();
            object actor = new object();
            hatch.Set(true, true);
            Assert.IsFalse(hatch.TryBeginUp(actor, UnderRing));
            hatch.Tick(0.2f);
            Assert.IsFalse(hatch.TryBeginUp(actor, UnderRing));
            hatch.Tick(0.4f);
            Assert.IsTrue(hatch.TryBeginUp(actor, UnderRing));
        }

        [Test]
        public void AClosingHatchCannotAcceptANewRiderEvenBeforeTheDoorsHaveMoved()
        {
            HatchLift hatch = OpenHatch();
            hatch.Set(false, true);
            Assert.IsTrue(hatch.Open);
            Assert.IsFalse(hatch.TryBeginUp(new object(), UnderRing));
            Assert.IsFalse(hatch.TryBeginDown(new object(), new P3(), ShipLayout.GroundY));
        }

        [Test]
        public void ASecondActorCannotReplaceOrCancelTheCurrentRider()
        {
            HatchLift hatch = OpenHatch();
            object first = new object(), second = new object();
            Assert.IsTrue(hatch.TryBeginUp(first, UnderRing));
            Assert.IsFalse(hatch.TryBeginDown(second, new P3(), ShipLayout.GroundY));
            hatch.Release(second);
            Assert.AreSame(first, hatch.RiderActor);
            Assert.AreEqual(LiftRide.Up, hatch.Ride);
            hatch.Release(first);
            Assert.IsNull(hatch.RiderActor);
            Assert.AreEqual(LiftRide.None, hatch.Ride);
            Assert.IsTrue(hatch.TryBeginDown(second, new P3(), ShipLayout.GroundY));
        }

        [Test]
        public void ACompletedTransferStaysReservedUntilItsActorConsumesTheResult()
        {
            HatchLift hatch = OpenHatch();
            object first = new object(), second = new object();
            Assert.IsTrue(hatch.TryBeginDown(first, new P3(), ShipLayout.GroundY));
            Assert.AreEqual(LiftRide.Down, hatch.Tick(10f));
            Assert.IsFalse(hatch.TryBeginUp(second, UnderRing));
            Assert.IsFalse(hatch.FinishedFor(second, LiftRide.Down));
            Assert.IsTrue(hatch.FinishedFor(first, LiftRide.Down));
            Assert.IsFalse(hatch.FinishedFor(first, LiftRide.Down));
            Assert.IsTrue(hatch.TryBeginUp(second, UnderRing));
        }

        [Test]
        public void CancelingAFinishedTransferReleasesItsReservation()
        {
            HatchLift hatch = OpenHatch();
            object first = new object(), second = new object();
            hatch.TryBeginDown(first, new P3(), ShipLayout.GroundY);
            hatch.Tick(10f);
            hatch.Release(first);
            Assert.IsFalse(hatch.FinishedFor(first, LiftRide.Down));
            Assert.IsTrue(hatch.TryBeginUp(second, UnderRing));
        }

        [Test]
        public void EveryDisembarkingActorMustStepAwayEvenIfAnotherActorExitsLater()
        {
            HatchLift hatch = OpenHatch();
            object first = new object(), second = new object();
            hatch.TryBeginDown(first, new P3(), ShipLayout.GroundY);
            hatch.Tick(10f);
            Assert.IsTrue(hatch.FinishedFor(first, LiftRide.Down));
            hatch.TryBeginDown(second, new P3(), ShipLayout.GroundY);
            hatch.Tick(10f);
            Assert.IsTrue(hatch.FinishedFor(second, LiftRide.Down));
            Assert.IsFalse(hatch.CanPickUp(first, UnderRing));
            Assert.IsFalse(hatch.CanPickUp(second, UnderRing));
            hatch.CanPickUp(first, new P3(2f, ShipLayout.GroundY, 0f));
            Assert.IsTrue(hatch.CanPickUp(first, UnderRing));
            Assert.IsFalse(hatch.CanPickUp(second, UnderRing));
        }

        [Test]
        public void LosingThePilotDuringBucklingDoesNotLeaveAPhantomOccupiedSeat()
        {
            var belt = new SeatBelt();
            Assert.IsTrue(belt.Begin());
            belt.Tick(0.2f);
            Assert.IsTrue(belt.Release());
            belt.Tick(2f);
            Assert.IsFalse(belt.Seated);
            Assert.IsFalse(belt.Buckled);
            Assert.IsTrue(belt.Begin());
        }
    }
}
