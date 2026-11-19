using System;
using NUnit.Framework;
using OrbitalAnte.Weapons;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Additional pure-rule edge cases for the existing gun state machine.
    /// Test-only fixtures deliberately use binary-exact timers; these are not tuning proposals.
    /// Written against main 96264e13187659d90dba198703bba188721f4e23; not run in Unity.
    /// </summary>
    public class FirearmBoundaryTests
    {
        static FirearmSpec Spec()
        {
            return new FirearmSpec
            {
                Automatic = true,
                ShotsPerSecond = 4f,
                MagazineSize = 4,
                ReloadSeconds = 1f,
                BaseSpread = 0.25f,
                SpreadPerShot = 0.5f,
                MaxBloom = 2f,
                BloomRecovery = 1f,
                OneHandSpreadMultiplier = 3f,
                OneHandRecoilMultiplier = 2f,
                OneHandReloadMultiplier = 2f,
                OneHandCanReload = true,
            };
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonpositiveMagazineCapacityIsRejected(int capacity)
        {
            FirearmSpec spec = Spec();
            spec.MagazineSize = capacity;
            Assert.Throws<ArgumentException>(() => new FirearmState(spec));
        }

        [TestCase(0f)]
        [TestCase(-4f)]
        public void NonpositiveFireRateIsRejected(float rate)
        {
            FirearmSpec spec = Spec();
            spec.ShotsPerSecond = rate;
            Assert.Throws<ArgumentException>(() => new FirearmState(spec));
        }

        [Test]
        public void StartingAmmoCannotExceedCapacityAndZeroMeansEmpty()
        {
            Assert.AreEqual(4, new FirearmState(Spec(), int.MaxValue).Ammo);
            Assert.AreEqual(0, new FirearmState(Spec(), 0).Ammo);
            Assert.AreEqual(4, new FirearmState(Spec(), -1).Ammo,
                "The negative/default starting-ammo sentinel starts with a full magazine.");
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void NonpositiveTickDoesNotAdvanceShotOrReloadState(float dt)
        {
            var gun = new FirearmState(Spec());
            Assert.IsTrue(gun.TryFire(2, true, true).Fired);
            Assert.IsTrue(gun.StartReload(2));
            float cooldown = gun.Cooldown;
            float bloom = gun.Bloom;
            float progress = gun.ReloadProgress;

            Assert.IsFalse(gun.Tick(dt));
            Assert.AreEqual(cooldown, gun.Cooldown);
            Assert.AreEqual(bloom, gun.Bloom);
            Assert.AreEqual(progress, gun.ReloadProgress);
            Assert.AreEqual(3, gun.Ammo);
            Assert.IsTrue(gun.Reloading);
        }

        [Test]
        public void ExactShotIntervalAllowsNextShotWithoutSettlingBurstBloom()
        {
            var gun = new FirearmState(Spec());
            Assert.IsTrue(gun.TryFire(2, true, true).Fired);
            Assert.IsFalse(gun.TryFire(2, true, false).Fired,
                "A second trigger request cannot bypass the cooldown.");
            gun.Tick(0.125f);
            Assert.IsFalse(gun.TryFire(2, true, false).Fired);
            gun.Tick(0.125f);

            FireResult next = gun.TryFire(2, true, false);
            Assert.IsTrue(next.Fired, "The exact cooldown endpoint must not lose an extra frame.");
            Assert.AreEqual(0.75f, next.SpreadDegrees, 0.000001f,
                "The previous shot's bloom must survive the time occupied by its cooldown.");
            Assert.AreEqual(2, gun.Ammo);
        }

        [Test]
        public void RepeatedReloadRequestDoesNotRestartItsTimer()
        {
            var gun = new FirearmState(Spec(), 1);
            Assert.IsTrue(gun.StartReload(2));
            Assert.IsFalse(gun.Tick(0.5f));
            Assert.IsFalse(gun.StartReload(2));
            Assert.AreEqual(0.5f, gun.ReloadProgress, 0.000001f);
            Assert.IsTrue(gun.Tick(0.5f));
            Assert.AreEqual(4, gun.Ammo);
        }

        [Test]
        public void ReloadCompletionIsReportedOnceEvenWhenATickOvershoots()
        {
            var gun = new FirearmState(Spec(), 0);
            Assert.IsTrue(gun.StartReload(2));
            Assert.IsTrue(gun.Tick(100f));
            Assert.IsFalse(gun.Reloading);
            Assert.AreEqual(0f, gun.ReloadProgress,
                "Progress is documented as zero outside an active reload.");
            Assert.IsFalse(gun.Tick(1f), "Completion is an event, not a latched state.");
            Assert.AreEqual(4, gun.Ammo);
        }

        [Test]
        public void CancelledReloadCanRestartButCannotCompleteUsingOldProgress()
        {
            var gun = new FirearmState(Spec(), 1);
            gun.StartReload(2);
            gun.Tick(0.75f);
            gun.CancelReload();
            gun.CancelReload();
            Assert.IsFalse(gun.Tick(5f));
            Assert.AreEqual(1, gun.Ammo);

            Assert.IsTrue(gun.StartReload(2));
            Assert.AreEqual(0f, gun.ReloadProgress);
            Assert.IsFalse(gun.Tick(0.25f));
            Assert.AreEqual(1, gun.Ammo);
            Assert.IsTrue(gun.Tick(0.75f));
        }

        [Test]
        public void LosingAllHandsCancelsEvenAnAllowedOneHandReload()
        {
            var gun = new FirearmState(Spec(), 2);
            Assert.IsTrue(gun.StartReload(1));
            gun.Tick(0.5f);
            gun.HandsChanged(0);
            Assert.IsFalse(gun.Reloading);
            Assert.IsFalse(gun.Tick(10f));
            Assert.AreEqual(2, gun.Ammo);
            Assert.IsTrue(gun.StartReload(2), "Regaining hands allows a fresh reload.");
        }

        [Test]
        public void OutOfRangeHandsCannotCreateExtraBenefitsOrConsumeAmmoWithoutHands()
        {
            var noHands = new FirearmState(Spec(), 0);
            FireResult blocked = noHands.TryFire(-1, true, true);
            Assert.IsFalse(blocked.Fired);
            Assert.IsFalse(blocked.DryFire);
            Assert.IsFalse(noHands.StartReload(-1));

            var ordinary = new FirearmState(Spec());
            var tooMany = new FirearmState(Spec());
            FireResult a = ordinary.TryFire(2, true, true);
            FireResult b = tooMany.TryFire(int.MaxValue, true, true);
            Assert.AreEqual(a.Fired, b.Fired);
            Assert.AreEqual(a.SpreadDegrees, b.SpreadDegrees);
            Assert.AreEqual(a.RecoilScale, b.RecoilScale);
            Assert.AreEqual(ordinary.Ammo, tooMany.Ammo);
            Assert.AreEqual(ordinary.Cooldown, tooMany.Cooldown);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void InstantReloadDoesNotLeaveAStuckActiveTimer(float seconds)
        {
            FirearmSpec spec = Spec();
            spec.ReloadSeconds = seconds;
            var gun = new FirearmState(spec, 0);
            Assert.IsTrue(gun.StartReload(2));
            Assert.AreEqual(4, gun.Ammo);
            Assert.IsFalse(gun.Reloading);
            Assert.AreEqual(0f, gun.ReloadProgress);
            Assert.IsFalse(gun.Tick(1f), "No later completion event remains queued.");
            Assert.IsTrue(gun.TryFire(2, true, true).Fired);
        }
    }
}
