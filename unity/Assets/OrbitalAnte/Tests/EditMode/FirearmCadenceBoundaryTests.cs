using NUnit.Framework;
using OrbitalAnte.Weapons;

namespace OrbitalAnte.Tests
{
    public class FirearmCadenceBoundaryTests
    {
        static FirearmState Gun(int magazine = 20) => new FirearmState(new FirearmSpec
        {
            Automatic = true, ShotsPerSecond = 10f, MagazineSize = magazine, ReloadSeconds = 0f,
            BaseSpread = 0.35f, SpreadPerShot = 0.22f, MaxBloom = 2.6f, BloomRecovery = 7f,
            OneHandCanReload = true, OneHandSpreadMultiplier = 4f,
        });

        [TestCase("release", 0f)]
        [TestCase("release", 0.01f)]
        [TestCase("release", 0.09f)]
        [TestCase("fresh pull", 0f)]
        [TestCase("fresh pull", 0.09f)]
        [TestCase("no hands", 0.01f)]
        [TestCase("hands changed", 0.01f)]
        [TestCase("drop", 0.01f)]
        [TestCase("reload", 0.01f)]
        public void InterruptingAfterALateShotCannotBorrowItsAlreadyConsumedCarry(string how, float wait)
        {
            FirearmState gun = Gun();
            Assert.IsTrue(gun.TryFire(2, true, true).Fired);
            gun.Tick(0.125f);
            Assert.IsTrue(gun.TryFire(2, true, false).Fired);
            Assert.AreEqual(0.075f, gun.Cooldown, 0.000001f);
            if (wait > 0f) gun.Tick(wait);
            if (how == "release") gun.TryFire(2, false, false);
            else if (how == "fresh pull") gun.TryFire(2, true, true);
            else if (how == "no hands") gun.TryFire(0, true, false);
            else if (how == "hands changed") gun.HandsChanged(0);
            else if (how == "reload") Assert.IsTrue(gun.StartReload(2));
            else gun.CancelReload();

            float remaining = 0.1f - wait;
            Assert.AreEqual(remaining, gun.Cooldown, 0.000001f);
            gun.Tick(remaining - 0.001f);
            Assert.IsFalse(gun.TryFire(2, true, false).Fired, "The next actual interval has not elapsed.");
            gun.Tick(0.0011f);
            Assert.IsTrue(gun.TryFire(2, true, false).Fired);
        }

        [Test]
        public void QuietRecoveryAfterALateShotUsesTheActualShotTime()
        {
            FirearmState gun = Gun();
            gun.TryFire(2, true, true);
            gun.Tick(0.125f);
            gun.TryFire(2, true, false);
            float bloom = gun.Bloom;
            gun.Tick(0.09f); // Past the scheduled next shot, but before the actual full interval.
            Assert.AreEqual(bloom, gun.Bloom, 0.000001f);
            Assert.IsFalse(gun.TryFire(2, true, true).Fired);
            gun.Tick(0.02f);
            Assert.AreEqual(bloom - 0.07f, gun.Bloom, 0.00001f);
        }

        [Test]
        public void AnInstantReloadAfterTheLastLateRoundAlsoKeepsTheActualInterval()
        {
            FirearmState gun = Gun(2);
            gun.TryFire(2, true, true);
            gun.Tick(0.125f);
            Assert.IsTrue(gun.TryFire(2, true, false).Fired);
            Assert.AreEqual(0, gun.Ammo);
            Assert.IsTrue(gun.StartReload(2));
            Assert.AreEqual(0.1f, gun.Cooldown, 0.000001f);
            gun.Tick(0.075f);
            Assert.IsFalse(gun.TryFire(2, true, false).Fired);
            gun.Tick(0.0251f);
            Assert.IsTrue(gun.TryFire(2, true, false).Fired);
        }
    }
}
