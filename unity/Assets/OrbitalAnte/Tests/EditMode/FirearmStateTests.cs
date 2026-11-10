using NUnit.Framework;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Weapons;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Rules for a gun: fire rate, magazine, reload, spread building up, and what one hand or no hands changes.
    /// These run without Unity objects.
    /// </summary>
    public class FirearmStateTests
    {
        static FirearmSpec Rifle(bool oneHandCanReload = true)
        {
            return new FirearmSpec
            {
                Automatic = true,
                ShotsPerSecond = 10f,
                MagazineSize = 30,
                ReloadSeconds = 2f,
                BaseSpread = 0.4f,
                SpreadPerShot = 0.2f,
                MaxBloom = 2f,
                BloomRecovery = 5f,
                OneHandSpreadMultiplier = 4f,
                OneHandRecoilMultiplier = 2.5f,
                OneHandReloadMultiplier = 2f,
                OneHandCanReload = oneHandCanReload,
            };
        }

        static FirearmSpec Pistol()
        {
            FirearmSpec spec = Rifle();
            spec.Automatic = false;
            spec.ShotsPerSecond = 5f;
            spec.MagazineSize = 12;
            return spec;
        }

        [Test]
        public void StartsWithAFullMagazine()
        {
            var gun = new FirearmState(Rifle());
            Assert.AreEqual(30, gun.Ammo);
            Assert.IsFalse(gun.Reloading);
        }

        [Test]
        public void AutomaticFire_IsLimitedByFireRate()
        {
            var gun = new FirearmState(Rifle());
            int shots = 0;
            // One second at 100 frames per second with the trigger held.
            for (int i = 0; i < 100; i++)
            {
                if (gun.TryFire(2, true, i == 0).Fired) shots++;
                gun.Tick(0.01f);
            }
            Assert.That(shots, Is.InRange(9, 11), "about ten shots in one second");
            Assert.AreEqual(30 - shots, gun.Ammo);
        }

        [Test]
        public void NonAutomatic_FiresOncePerPull()
        {
            var gun = new FirearmState(Pistol());
            Assert.IsTrue(gun.TryFire(2, true, true).Fired);
            for (int i = 0; i < 100; i++)
            {
                gun.Tick(0.01f);
                Assert.IsFalse(gun.TryFire(2, true, false).Fired, "holding the trigger does nothing");
            }
            Assert.IsTrue(gun.TryFire(2, true, true).Fired, "a fresh pull fires again");
        }

        [Test]
        public void EmptyMagazine_ClicksOncePerPull_AndDoesNotFire()
        {
            var gun = new FirearmState(Rifle(), 1);
            Assert.IsTrue(gun.TryFire(2, true, true).Fired);
            gun.Tick(1f);
            FireResult empty = gun.TryFire(2, true, true);
            Assert.IsFalse(empty.Fired);
            Assert.IsTrue(empty.DryFire);
            Assert.IsFalse(gun.TryFire(2, true, false).DryFire, "no click while the trigger is just held");
        }

        [Test]
        public void Reload_TakesItsTime_ThenFillsTheMagazine()
        {
            var gun = new FirearmState(Rifle(), 3);
            Assert.IsTrue(gun.StartReload(2));
            Assert.IsTrue(gun.Reloading);
            Assert.IsFalse(gun.TryFire(2, true, true).Fired, "cannot fire while reloading");

            Assert.IsFalse(gun.Tick(1.9f));
            Assert.AreEqual(3, gun.Ammo);
            Assert.That(gun.ReloadProgress, Is.InRange(0.9f, 1f));

            Assert.IsTrue(gun.Tick(0.2f), "reports the moment the reload finishes");
            Assert.AreEqual(30, gun.Ammo);
            Assert.IsFalse(gun.Reloading);
        }

        [Test]
        public void FullMagazine_DoesNotReload()
        {
            var gun = new FirearmState(Rifle());
            Assert.IsFalse(gun.StartReload(2));
        }

        [Test]
        public void SustainedFire_BuildsSpread_UpToACap_AndItSettles()
        {
            var gun = new FirearmState(Rifle());
            float first = gun.TryFire(2, true, true).SpreadDegrees;
            Assert.AreEqual(0.4f, first, 0.0001f, "first shot uses the base spread");

            float last = first;
            for (int i = 0; i < 25; i++)
            {
                gun.Tick(0.1f); // the gap between shots
                FireResult r = gun.TryFire(2, true, false);
                if (r.Fired) last = r.SpreadDegrees;
            }
            Assert.Greater(last, first, "spread grows under sustained fire");
            Assert.LessOrEqual(last, 0.4f + 2f + 0.0001f, "but never past the cap");

            gun.Tick(5f);
            Assert.AreEqual(0f, gun.Bloom, 0.0001f, "and settles when the trigger is released");
        }

        [Test]
        public void OneHand_HasBigSpreadAndRecoilPenalty()
        {
            var two = new FirearmState(Rifle());
            var one = new FirearmState(Rifle());
            FireResult a = two.TryFire(2, true, true);
            FireResult b = one.TryFire(1, true, true);
            Assert.IsTrue(a.Fired);
            Assert.IsTrue(b.Fired);
            Assert.AreEqual(a.SpreadDegrees * 4f, b.SpreadDegrees, 0.0001f);
            Assert.AreEqual(1f, a.RecoilScale, 0.0001f);
            Assert.AreEqual(2.5f, b.RecoilScale, 0.0001f);
        }

        [Test]
        public void OneHand_ReloadsSlower()
        {
            var gun = new FirearmState(Rifle(), 0);
            Assert.AreEqual(4f, gun.ReloadSecondsFor(1), 0.0001f);
            Assert.IsTrue(gun.StartReload(1));
            gun.Tick(2.5f);
            Assert.IsTrue(gun.Reloading, "still reloading after the two-handed time");
            gun.Tick(1.6f);
            Assert.AreEqual(30, gun.Ammo);
        }

        [Test]
        public void OneHand_CannotReload_WhenTheToggleIsOff()
        {
            var gun = new FirearmState(Rifle(oneHandCanReload: false), 0);
            Assert.IsFalse(gun.CanReload(1));
            Assert.IsFalse(gun.StartReload(1));
            Assert.IsTrue(gun.StartReload(2), "two hands still can");
        }

        [Test]
        public void LosingAHandMidReload_StopsIt_WhenTheToggleIsOff()
        {
            var gun = new FirearmState(Rifle(oneHandCanReload: false), 5);
            gun.StartReload(2);
            gun.Tick(1f);
            gun.HandsChanged(1);
            Assert.IsFalse(gun.Reloading);
            Assert.AreEqual(5, gun.Ammo, "the magazine keeps what it had");

            var allowed = new FirearmState(Rifle(oneHandCanReload: true), 5);
            allowed.StartReload(2);
            allowed.HandsChanged(1);
            Assert.IsTrue(allowed.Reloading, "with the toggle on, the reload carries on");
        }

        [Test]
        public void NoHands_CannotFireOrReload()
        {
            var gun = new FirearmState(Rifle(), 10);
            Assert.IsFalse(gun.TryFire(0, true, true).Fired);
            Assert.IsFalse(gun.TryFire(0, true, true).DryFire);
            Assert.IsFalse(gun.StartReload(0));
            Assert.AreEqual(10, gun.Ammo);
        }

        [Test]
        public void CancelReload_KeepsTheOldMagazine()
        {
            var gun = new FirearmState(Rifle(), 7);
            gun.StartReload(2);
            gun.Tick(1f);
            gun.CancelReload();
            gun.Tick(5f);
            Assert.AreEqual(7, gun.Ammo);
        }

        [Test]
        public void UsableHands_FollowTheArms()
        {
            var hp = new float[BodyLayout.Count];
            for (int i = 0; i < hp.Length; i++) hp[i] = 10f;
            var body = new BodyModel(hp);
            Assert.AreEqual(2, body.UsableHands);

            body.ApplyDamage(BodyPartId.RightHand, 100f);
            Assert.AreEqual(1, body.UsableHands, "a lost hand");

            body.ApplyDamage(BodyPartId.LeftForearm, 100f);
            Assert.AreEqual(0, body.UsableHands, "a lost forearm takes its hand with it");
        }
    }
}
