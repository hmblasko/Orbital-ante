using System.Collections;
using NUnit.Framework;
using OrbitalAnte.AI;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Player;
using OrbitalAnte.Weapons;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Checks Milestone 3 on real objects: weapon sockets and data, disarming (lost hand, lost forearm, shot gun),
    /// picking a gun back up with the remaining hand, the one-hand and no-hands rules, and the player's swap.
    /// Run in Unity from Window > General > Test Runner > PlayMode.
    /// </summary>
    public class WeaponTests
    {
        const float Big = 100000f;

        GameObject _root;
        Mannequin _mannequin;
        MannequinCombat _combat;

        [SetUp]
        public void SetUp()
        {
            Layers.Configure();
            CombatWorld.Reset();
            _root = new GameObject("WeaponTestRoot");

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(_root.transform, false);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(50f, 1f, 50f);

            _mannequin = MannequinFactory.Create(Vector3.zero, Quaternion.identity, _root.transform, MannequinConfig.Load(), "TestMannequin");
            _mannequin.GetComponent<MannequinLocomotion>().Wander = false;
            _combat = _mannequin.GetComponent<MannequinCombat>();
        }

        [TearDown]
        public void TearDown()
        {
            CombatWorld.Reset();
            GibManager.DestroyInstance();
            if (WeaponFx.Instance != null) Object.Destroy(WeaponFx.Instance.gameObject);
            if (_root != null) Object.Destroy(_root);
            Time.timeScale = 1f;
        }

        Weapon NewWeapon(WeaponData data, Vector3 position, bool oneHandCanReload = true)
        {
            Weapon weapon = Weapon.Create(data, _root.transform, oneHandCanReload);
            weapon.transform.position = position;
            return weapon;
        }

        Weapon Arm(WeaponData data)
        {
            Weapon weapon = NewWeapon(data, new Vector3(0f, 1.2f, 0.3f));
            Assert.IsTrue(_combat.TryPickUp(weapon), "an intact character can take a gun");
            return weapon;
        }

        [UnityTest]
        public IEnumerator BothGuns_HaveTheFourSockets_AndSensibleData()
        {
            yield return null;
            foreach (WeaponData data in new[] { WeaponData.Rifle, WeaponData.Pistol })
            {
                Weapon w = NewWeapon(data, new Vector3(3f, 1f, 3f));
                Assert.IsNotNull(w.Grip, data.name + " grip");
                Assert.IsNotNull(w.SupportHand, data.name + " support hand");
                Assert.IsNotNull(w.Muzzle, data.name + " muzzle");
                Assert.IsNotNull(w.Magazine, data.name + " magazine");
                Assert.Greater(w.Muzzle.localPosition.z, w.Grip.localPosition.z, data.name + " muzzle is in front of the grip");
                Assert.AreEqual(data.magazineSize, w.State.Ammo);
                Assert.AreEqual(WeaponHold.Dropped, w.Hold);
                Assert.IsFalse(w.Body.isKinematic, "a loose gun is a physics object");
                Object.Destroy(w.gameObject);
            }

            // Limbs come off in one to three hits with either gun.
            MannequinConfig cfg = MannequinConfig.Load();
            foreach (WeaponData data in new[] { WeaponData.Rifle, WeaponData.Pistol })
            {
                for (int i = 0; i < BodyLayout.Count; i++)
                {
                    var id = (BodyPartId)i;
                    if (id == BodyPartId.Head || id == BodyPartId.Torso) continue;
                    int hits = Mathf.CeilToInt(cfg.HpFor(id) / data.DamageFor(id));
                    Assert.That(hits, Is.InRange(1, 3), data.name + " on " + id);
                }
            }
            Assert.IsTrue(WeaponData.Rifle.automatic);
            Assert.IsFalse(WeaponData.Pistol.automatic);
        }

        [UnityTest]
        public IEnumerator HeldGun_SitsInTheHands_AndCanFire()
        {
            yield return null;
            Weapon weapon = Arm(WeaponData.Rifle);
            yield return null;
            yield return null;
            Assert.AreSame(weapon, _combat.Weapon);
            Assert.AreEqual(WeaponHold.HeldByCharacter, weapon.Hold);
            Assert.IsTrue(weapon.Body.isKinematic);
            Assert.AreEqual(Layers.Hitbox, weapon.gameObject.layer, "only bullets can touch a held gun");
            Assert.AreEqual(2, _combat.Hands);
            Assert.IsTrue(_combat.CanFire);
            Assert.Less(Vector3.Distance(weapon.transform.position, _mannequin.Rig.UpperArm[MannequinRig.Right].position), 0.9f,
                "the gun is held near the shoulder");
        }

        [UnityTest]
        public IEnumerator LosingTheGunHand_DropsTheGunAsAPhysicsObject()
        {
            yield return null;
            Weapon weapon = Arm(WeaponData.Rifle);
            yield return null;

            _mannequin.DamagePart(BodyPartId.RightHand, Big, Vector3.forward);
            Assert.IsNull(_combat.Weapon);
            Assert.AreEqual(WeaponHold.Dropped, weapon.Hold);
            Assert.IsFalse(weapon.Body.isKinematic);
            Assert.AreEqual(Layers.Item, weapon.gameObject.layer);
            Assert.IsFalse(_combat.CanFire);

            for (int i = 0; i < 150; i++) yield return new WaitForFixedUpdate();
            Assert.Less(weapon.transform.position.y, 0.6f, "it fell to the floor");
            Assert.Greater(weapon.transform.position.y, -0.2f, "and stayed on it");
        }

        [UnityTest]
        public IEnumerator LosingTheForearm_AlsoDropsTheGun()
        {
            yield return null;
            Weapon weapon = Arm(WeaponData.Pistol);
            yield return null;
            _mannequin.DamagePart(BodyPartId.RightForearm, Big, Vector3.forward);
            Assert.IsNull(_combat.Weapon);
            Assert.AreEqual(WeaponHold.Dropped, weapon.Hold);
        }

        [UnityTest]
        public IEnumerator ShootingTheGun_KnocksItOutOfTheHands()
        {
            yield return null;
            Weapon weapon = Arm(WeaponData.Rifle);
            yield return null;

            var hit = new HitInfo { Damage = 34f, Point = weapon.transform.position, Direction = Vector3.forward, Normal = Vector3.back };
            HitOutcome outcome = weapon.Hit(hit);
            Assert.IsTrue(outcome.Hit);
            Assert.IsTrue(outcome.Disarmed);
            Assert.IsNull(_combat.Weapon);
            Assert.AreEqual(WeaponHold.Dropped, weapon.Hold);
            Assert.Greater(weapon.Body.linearVelocity.z, 2f, "knocked away along the bullet");
            Assert.AreEqual(2, _combat.Hands, "the character is unharmed");
        }

        [UnityTest]
        public IEnumerator ShootingTheGun_IsARealRaycastTarget()
        {
            yield return null;
            Weapon weapon = Arm(WeaponData.Rifle);
            yield return null;
            yield return new WaitForFixedUpdate();

            Vector3 target = weapon.transform.TransformPoint(new Vector3(0f, 0.04f, 0.3f));
            Vector3 origin = target + new Vector3(4f, 0f, 0f);
            ShotResult shot = Ballistics.FireFromPlayer(WeaponData.Rifle, origin, (target - origin).normalized);
            Assert.IsTrue(shot.HitAnything);
            Assert.IsTrue(shot.Outcome.Disarmed, "a bullet from the side hits the gun and knocks it away");
            Assert.IsNull(_combat.Weapon);
        }

        [UnityTest]
        public IEnumerator ADroppedGun_CanBePickedUpWithTheRemainingHand()
        {
            yield return null;
            Weapon weapon = Arm(WeaponData.Rifle);
            yield return null;
            _mannequin.DamagePart(BodyPartId.RightHand, Big, Vector3.forward);
            Assert.IsNull(_combat.Weapon);
            Assert.AreEqual(1, _combat.Hands);

            Assert.IsTrue(_combat.TryPickUp(weapon));
            Assert.AreSame(weapon, _combat.Weapon);
            Assert.AreEqual(MannequinRig.Left, _combat.GunSide, "held in the hand that is left");
            Assert.IsTrue(_combat.CanFire);

            // One hand on a two-handed gun: big spread and recoil penalty.
            FireResult shot = weapon.State.TryFire(_combat.Hands, true, true);
            Assert.IsTrue(shot.Fired);
            Assert.Greater(shot.SpreadDegrees, WeaponData.Rifle.baseSpread * 3f);
            Assert.Greater(shot.RecoilScale, 2f);
            Assert.Greater(weapon.State.ReloadSecondsFor(1), weapon.State.ReloadSecondsFor(2) * 1.5f);
        }

        [UnityTest]
        public IEnumerator NoHands_CannotPickUpOrFire_ButStillMoves()
        {
            yield return null;
            Weapon weapon = Arm(WeaponData.Rifle);
            yield return null;
            _mannequin.DamagePart(BodyPartId.RightHand, Big, Vector3.forward);
            _mannequin.DamagePart(BodyPartId.LeftHand, Big, Vector3.forward);
            Assert.AreEqual(0, _combat.Hands);
            Assert.IsFalse(_combat.TryPickUp(weapon));
            Assert.IsFalse(_combat.CanFire);
            Assert.IsFalse(weapon.State.TryFire(_combat.Hands, true, true).Fired);

            // Legs are fine, so it can still get about.
            Assert.AreEqual(MovementState.Intact, _mannequin.State);
            var locomotion = _mannequin.GetComponent<MannequinLocomotion>();
            locomotion.Wander = true;
            locomotion.Destination = new Vector3(0f, 0f, 6f);
            Vector3 start = _mannequin.transform.position;
            yield return new WaitForSeconds(1.5f);
            Assert.Greater(Vector3.Distance(start, _mannequin.transform.position), 0.5f);
        }

        [UnityTest]
        public IEnumerator OneHandedReload_ObeysTheToggle()
        {
            yield return null;
            Weapon weapon = NewWeapon(WeaponData.Rifle, new Vector3(0f, 1.2f, 0.3f), oneHandCanReload: false);
            Assert.IsTrue(_combat.TryPickUp(weapon));
            _mannequin.DamagePart(BodyPartId.LeftHand, Big, Vector3.forward);
            Assert.AreSame(weapon, _combat.Weapon, "losing the support hand keeps the gun");
            Assert.AreEqual(1, _combat.Hands);
            Assert.IsFalse(weapon.State.CanReload(_combat.Hands) && weapon.State.Ammo < weapon.State.Spec.MagazineSize);
            while (weapon.State.Ammo > 0)
            {
                weapon.State.TryFire(1, true, true);
                weapon.State.Tick(1f);
            }
            Assert.IsFalse(weapon.State.StartReload(_combat.Hands), "with the toggle off, one hand cannot reload");
        }

        [UnityTest]
        public IEnumerator Death_DropsTheGun()
        {
            yield return null;
            Weapon weapon = Arm(WeaponData.Rifle);
            yield return null;
            _mannequin.DamagePart(BodyPartId.Head, Big, Vector3.forward);
            Assert.IsNull(_combat.Weapon);
            Assert.AreEqual(WeaponHold.Dropped, weapon.Hold);
            Assert.IsTrue(weapon != null, "the gun outlives its owner");
        }

        [UnityTest]
        public IEnumerator UnarmedCharacter_FetchesANearbyGun()
        {
            yield return null;
            var locomotion = _mannequin.GetComponent<MannequinLocomotion>();
            locomotion.Wander = true;
            Weapon weapon = NewWeapon(WeaponData.Pistol, new Vector3(0f, 0.3f, 4f));

            float deadline = Time.time + 8f;
            while (_combat.Weapon == null && Time.time < deadline) yield return null;
            Assert.AreSame(weapon, _combat.Weapon, "it walked over and picked the gun up");
        }

        [UnityTest]
        public IEnumerator ArmedCharacter_ShootsAtThePlayer_AfterABeat_AndHoldFireStopsIt()
        {
            // A stand-in player: a capsule on the Player layer with health, eight metres in front.
            var player = new GameObject("FakePlayer");
            player.layer = Layers.Player;
            player.transform.SetParent(_root.transform, false);
            player.transform.position = new Vector3(0f, 0f, 8f);
            var capsule = player.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.35f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            var health = player.AddComponent<PlayerHealth>();
            health.Initialize(PlayerConfig.Load());
            var eye = new GameObject("Eye").transform;
            eye.SetParent(player.transform, false);
            eye.localPosition = new Vector3(0f, 1.65f, 0f);

            CombatWorld.PlayerEye = eye;
            CombatWorld.Player = health;
            CombatWorld.EnemiesHoldFire = true;

            _mannequin.GetComponent<MannequinLocomotion>().Wander = true;
            Arm(WeaponData.Rifle);
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(0, _combat.ShotsFired, "holding fire");

            CombatWorld.EnemiesHoldFire = false;
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0, _combat.ShotsFired, "never an instant shot");

            yield return new WaitForSeconds(3.5f);
            Assert.Greater(_combat.ShotsFired, 0, "then it opens fire");
            Assert.Less(health.Current, health.Max, "and at eight metres some of it lands");
        }

        [UnityTest]
        public IEnumerator Player_SwapsGuns_OneAtATime()
        {
            yield return null;
            GameInput.Initialize();
            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(_root.transform, false);
            playerGo.transform.position = new Vector3(10f, 0f, 10f);
            playerGo.AddComponent<CharacterController>();
            var eyeGo = new GameObject("Eye");
            eyeGo.transform.SetParent(playerGo.transform, false);
            eyeGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            var camera = eyeGo.AddComponent<Camera>();
            var controller = playerGo.AddComponent<FpsController>();
            controller.Initialize(PlayerConfig.Load(), camera);
            var weapons = eyeGo.AddComponent<PlayerWeapons>();
            weapons.Initialize(PlayerConfig.Load(), camera, controller, null, _root.transform);

            Weapon rifle = NewWeapon(WeaponData.Rifle, new Vector3(10f, 1f, 11f));
            Weapon pistol = NewWeapon(WeaponData.Pistol, new Vector3(10f, 1f, 11f));

            weapons.Equip(rifle);
            Assert.AreSame(rifle, weapons.Current);
            Assert.AreEqual(WeaponHold.HeldByPlayer, rifle.Hold);

            weapons.Equip(pistol);
            Assert.AreSame(pistol, weapons.Current);
            Assert.AreEqual(WeaponHold.Dropped, rifle.Hold, "taking a second gun drops the first");
            Assert.IsFalse(rifle.Body.isKinematic);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Recoil_KicksTheViewUp_AndPartlySettles()
        {
            yield return null;
            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(_root.transform, false);
            playerGo.transform.position = new Vector3(-10f, 0f, -10f);
            playerGo.AddComponent<CharacterController>();
            var eyeGo = new GameObject("Eye");
            eyeGo.transform.SetParent(playerGo.transform, false);
            var camera = eyeGo.AddComponent<Camera>();
            GameInput.Initialize();
            var controller = playerGo.AddComponent<FpsController>();
            controller.Initialize(PlayerConfig.Load(), camera);
            yield return null;

            controller.AddRecoil(4f, 0f, 0.5f, 40f);
            yield return null;
            float kicked = Mathf.DeltaAngle(0f, eyeGo.transform.localEulerAngles.x);
            Assert.Less(kicked, -2.5f, "the view jumped upward");

            yield return new WaitForSeconds(0.5f);
            float settled = Mathf.DeltaAngle(0f, eyeGo.transform.localEulerAngles.x);
            Assert.AreEqual(-2f, settled, 0.3f, "half the kick came back, half stayed");
        }
    }
}
