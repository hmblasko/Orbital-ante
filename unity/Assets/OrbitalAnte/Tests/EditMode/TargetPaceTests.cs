using NUnit.Framework;
using OrbitalAnte.Ante;

namespace OrbitalAnte.Tests
{
    public class TargetPaceTests
    {
        [Test]
        public void TheTick_IsWhereASteadyPaceWouldBe()
        {
            PaceReading r = TargetPace.Read(0, 850, 150f, 300f, false, false);
            Assert.AreEqual(0.5f, r.expectedShare, 1e-4);
        }

        [Test]
        public void NobodyIsCalledBehind_InTheFirstHalfMinute_OrWhileTheClockWaits()
        {
            Assert.AreEqual(PaceStatus.Waiting, TargetPace.Read(0, 850, 280f, 300f, false, false).status);
            Assert.AreEqual(PaceStatus.Waiting, TargetPace.Read(0, 850, 100f, 300f, false, true).status);
        }

        [Test]
        public void Behind_SaysThePointsAMinuteStillNeeded_AheadSaysNothing()
        {
            PaceReading behind = TargetPace.Read(200, 850, 150f, 300f, false, false);
            Assert.AreEqual(PaceStatus.Behind, behind.status);
            Assert.AreEqual(260, behind.neededPerMinute, "650 to go in 2.5 minutes, rounded up to tens");
            Assert.AreEqual("260/MIN BEATS THE CLOCK", TargetPace.Words(behind));
            PaceReading ahead = TargetPace.Read(700, 850, 150f, 300f, false, false);
            Assert.AreEqual(PaceStatus.Ahead, ahead.status);
            Assert.IsEmpty(TargetPace.Words(ahead));
            Assert.AreEqual(PaceStatus.OnPace, TargetPace.Read(430, 850, 150f, 300f, false, false).status);
        }

        [Test]
        public void MetAndOvertime_ReadAsSuch()
        {
            Assert.AreEqual(PaceStatus.Met, TargetPace.Read(900, 850, 100f, 300f, false, false).status);
            PaceReading over = TargetPace.Read(700, 850, 0f, 300f, true, false);
            Assert.AreEqual(PaceStatus.Overtime, over.status);
            Assert.AreEqual("150 TO GO", TargetPace.Words(over));
        }

        [Test]
        public void TimeAddedPastTheListedClock_NeverPutsTheTickBelowZero()
        {
            PaceReading r = TargetPace.Read(0, 850, 340f, 300f, false, false);
            Assert.AreEqual(0f, r.expectedShare, 1e-4);
        }
    }
}
