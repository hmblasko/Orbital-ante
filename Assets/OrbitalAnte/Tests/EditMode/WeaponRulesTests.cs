using NUnit.Framework;
using OrbitalAnte.Characters;
using OrbitalAnte.Combat;
using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Tests
{
    public class WeaponRulesTests
    {
        WeaponDefinition rifle;
        WeaponDefinition pistol;

        [SetUp]
        public void SetUp()
        {
            rifle = WeaponDefinition.DefaultRifle();
            pistol = WeaponDefinition.DefaultPistol();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(rifle);
            Object.DestroyImmediate(pistol);
        }

        [Test]
        public void NoHandsMeansNoShooting()
        {
            Assert.IsFalse(WeaponRules.CanFire(0));
            Assert.IsTrue(WeaponRules.CanFire(1));
            Assert.IsTrue(WeaponRules.CanFire(2));
        }

        [Test]
        public void OneHandSpreadsMoreAndKicksHarder()
        {
            float two = WeaponRules.SpreadDegrees(rifle, false, 0f, 2);
            float one = WeaponRules.SpreadDegrees(rifle, false, 0f, 1);
            Assert.AreEqual(two * rifle.oneHandSpreadMultiplier, one, 1e-4f);
            Assert.Greater(WeaponRules.RecoilMultiplier(rifle, 1), WeaponRules.RecoilMultiplier(rifle, 2));
        }

        [Test]
        public void AimingTightensSpreadAndBloomIsCapped()
        {
            Assert.Less(WeaponRules.SpreadDegrees(rifle, true, 0f, 2), WeaponRules.SpreadDegrees(rifle, false, 0f, 2));
            float capped = WeaponRules.SpreadDegrees(rifle, false, 999f, 2);
            Assert.AreEqual(rifle.hipSpread + rifle.maxBloom, capped, 1e-4f);
        }

        [Test]
        public void PistolSuffersLessOneHandedThanRifle()
        {
            Assert.Less(pistol.oneHandSpreadMultiplier, rifle.oneHandSpreadMultiplier);
        }

        [Test]
        public void ReloadRulesFollowTheOneHandSetting()
        {
            Assert.IsTrue(WeaponRules.CanReload(2, OneHandReload.NoReload));
            Assert.IsTrue(WeaponRules.CanReload(1, OneHandReload.SlowReload));
            Assert.IsFalse(WeaponRules.CanReload(1, OneHandReload.NoReload));
            Assert.IsFalse(WeaponRules.CanReload(0, OneHandReload.SlowReload));
            Assert.AreEqual(rifle.reloadSeconds, WeaponRules.ReloadSeconds(rifle, 2), 1e-4f);
            Assert.AreEqual(rifle.reloadSeconds * rifle.oneHandReloadMultiplier, WeaponRules.ReloadSeconds(rifle, 1), 1e-4f);
        }

        [Test]
        public void HeadshotsHitHarderThanBodyShots()
        {
            Assert.Greater(rifle.DamageFor(BodyPartId.Head), rifle.DamageFor(BodyPartId.Torso));
            Assert.AreEqual(rifle.baseDamage * rifle.legMultiplier, rifle.DamageFor(BodyPartId.ShinL), 1e-4f);
        }

        [Test]
        public void SpreadNeverLeavesTheCone()
        {
            var rng = new SeededRandom(1234);
            var forward = new Vector3(0.3f, -0.2f, 1f).normalized;
            const float cone = 5f;
            for (int i = 0; i < 2000; i++)
            {
                var d = WeaponRules.ApplySpread(forward, cone, rng.NextFloat(), rng.NextFloat());
                Assert.LessOrEqual(Vector3.Angle(forward, d), cone + 0.01f);
                Assert.AreEqual(1f, d.magnitude, 1e-4f);
            }
            Assert.Less((forward - WeaponRules.ApplySpread(forward, 0f, 0.7f, 0.2f)).magnitude, 1e-5f, "no cone, no spread");
        }

        [Test]
        public void SameSeedSameShotPattern()
        {
            var a = new SeededRandom(99);
            var b = new SeededRandom(99);
            for (int i = 0; i < 50; i++)
            {
                var da = WeaponRules.ApplySpread(Vector3.forward, 3f, a.NextFloat(), a.NextFloat());
                var db = WeaponRules.ApplySpread(Vector3.forward, 3f, b.NextFloat(), b.NextFloat());
                Assert.AreEqual(da, db);
            }
        }
    }
}
