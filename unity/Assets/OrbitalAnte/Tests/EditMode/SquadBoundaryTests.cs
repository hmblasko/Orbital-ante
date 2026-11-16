using NUnit.Framework;
using OrbitalAnte.Enemies;

namespace OrbitalAnte.Tests
{
    public class SquadBoundaryTests
    {
        [TestCase(int.MinValue, 1)]
        [TestCase(-1, 1)]
        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(6, 3)]
        [TestCase(int.MaxValue, 3)]
        public void SlotAllocationRemainsBoundedAtMemberCountEdges(int members, int slots)
        {
            // Nonpositive counts currently use the minimum allocation; no actor is created by this rule.
            var squad = new Squad(members);
            Assert.AreEqual(slots, squad.Shooters);
            Assert.AreEqual(0, squad.TurnsTaken(0f));
        }

        [TestCase(-100f)]
        [TestCase(0f)]
        [TestCase(1000000f)]
        public void ASlotIsFreeAtItsExactEndAndTheReplacementGetsItsOwnEnd(float start)
        {
            var squad = new Squad(1);
            Assert.IsTrue(squad.TakeTurn(start, 2f));
            Assert.AreEqual(1, squad.TurnsTaken(start + 1f));
            Assert.IsFalse(squad.TakeTurn(start + 1f, 50f));
            Assert.AreEqual(0, squad.TurnsTaken(start + 2f));
            Assert.IsTrue(squad.TakeTurn(start + 2f, 3f));
            Assert.AreEqual(1, squad.TurnsTaken(start + 4f));
            Assert.AreEqual(0, squad.TurnsTaken(start + 5f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(-1000000f)]
        public void NonpositiveDurationDoesNotReserveASlot(float seconds)
        {
            var squad = new Squad(1);
            for (int i = 0; i < 20; i++)
            {
                Assert.IsTrue(squad.TakeTurn(10f, seconds));
                Assert.AreEqual(0, squad.TurnsTaken(10f));
            }
            Assert.IsTrue(squad.TakeTurn(10f, 1f));
            Assert.IsFalse(squad.TakeTurn(10f, 0f), "A zero duration cannot evict someone else's active turn.");
        }

        [Test]
        public void FullSquadRefusalsDoNotExtendOrShortenExistingReservations()
        {
            var squad = new Squad(6);
            Assert.IsTrue(squad.TakeTurn(10f, 1f));
            Assert.IsTrue(squad.TakeTurn(10f, 2f));
            Assert.IsTrue(squad.TakeTurn(10f, 3f));
            for (int i = 0; i < 20; i++) Assert.IsFalse(squad.TakeTurn(10.5f, 1000f));
            Assert.AreEqual(2, squad.TurnsTaken(11f));
            Assert.AreEqual(1, squad.TurnsTaken(12f));
            Assert.AreEqual(0, squad.TurnsTaken(13f));
        }

        [Test]
        public void AnOlderOrRepeatedSightingCannotReplaceTheLatestOne()
        {
            var squad = new Squad(2);
            squad.Saw(20f);
            squad.Saw(10f);
            squad.Saw(20f);
            Assert.IsTrue(squad.SawWithin(20f, 0f));
            Assert.IsTrue(squad.SawWithin(23f, 3f), "The stated window includes its exact endpoint.");
            Assert.IsFalse(squad.SawWithin(23.25f, 3f));
            Assert.IsFalse(squad.SawWithin(20f, -1f));
        }

        [Test]
        public void ReservationsAndSightingsStayInTheirOwnSquad()
        {
            var active = new Squad(1);
            var untouched = new Squad(1);
            active.TakeTurn(0f, 1000f);
            active.Saw(0f);
            Assert.IsTrue(untouched.TakeTurn(0f, 1f));
            Assert.IsFalse(untouched.SawWithin(0f, float.MaxValue));
            Assert.IsTrue(active.SawWithin(0f, 0f));
        }
    }
}
