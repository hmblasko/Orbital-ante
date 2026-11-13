using NUnit.Framework;
using OrbitalAnte.Core;

namespace OrbitalAnte.Tests
{
    public class InputHintRefreshTests
    {
        [Test]
        public void FirstUseRefreshesEvenAtTimeZero()
        {
            var refresh = new InputHintRefresh();
            Assert.IsTrue(refresh.TryRefresh(0, 0, false));
            Assert.IsFalse(refresh.TryRefresh(0, 0, false));
        }

        [Test]
        public void StableInputDoesNotRebuildEveryFrame()
        {
            var refresh = new InputHintRefresh();
            Assert.IsTrue(refresh.TryRefresh(10, 2, false));
            for (int frame = 1; frame < 120; frame++)
                Assert.IsFalse(refresh.TryRefresh(10 + frame / 60.0, 2, false));
            Assert.IsTrue(refresh.TryRefresh(12, 2, false));
        }

        [Test]
        public void AcceptedRebindBypassesTheDeviceRateLimit()
        {
            var refresh = new InputHintRefresh();
            Assert.IsTrue(refresh.TryRefresh(10, 3, false));
            Assert.IsTrue(refresh.TryRefresh(10.01, 4, false));
            Assert.IsFalse(refresh.TryRefresh(10.02, 4, false));
        }

        [Test]
        public void ADeviceSwitchAppearsAfterAtMostAQuarterSecond()
        {
            var refresh = new InputHintRefresh();
            Assert.IsTrue(refresh.TryRefresh(10, 0, false));
            Assert.IsFalse(refresh.TryRefresh(10.1, 0, true));
            Assert.IsTrue(refresh.TryRefresh(10.25, 0, true));
            Assert.IsFalse(refresh.TryRefresh(10.26, 0, true));
        }

        [Test]
        public void AlternatingDevicesCannotRebuildHintsAtFrameRate()
        {
            var refresh = new InputHintRefresh();
            int rebuilds = 0;
            for (int frame = 0; frame < 600; frame++)
                if (refresh.TryRefresh(frame / 60.0, 0, frame % 2 == 0)) rebuilds++;
            Assert.LessOrEqual(rebuilds, 40, "Device-only rebuilds must be capped at four per second.");
        }

        [Test]
        public void AReopenedViewRefreshesWithoutWaitingForTheOldDeadline()
        {
            var refresh = new InputHintRefresh();
            Assert.IsTrue(refresh.TryRefresh(10, 0, true));
            refresh.Invalidate();
            Assert.IsTrue(refresh.TryRefresh(10.01, 0, true));
            Assert.IsFalse(refresh.TryRefresh(10.02, 0, true));
        }

        [Test]
        public void UnchangedRevisionStillRefreshesPeriodicallyForLayoutChanges()
        {
            var refresh = new InputHintRefresh();
            Assert.IsTrue(refresh.TryRefresh(0, 0, true));
            Assert.IsFalse(refresh.TryRefresh(1.99, 0, true));
            Assert.IsTrue(refresh.TryRefresh(2, 0, true));
        }
    }
}
