using System.Collections;
using System.Reflection;
using NUnit.Framework;
using OrbitalAnte.Cockpit;
using OrbitalAnte.Content;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Launch;
using OrbitalAnte.Planets;
using OrbitalAnte.Ship;
using OrbitalAnte.Space;
using OrbitalAnte.Spacewalk;
using OrbitalAnte.Weapons;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Milestone 12 on real objects: in the solar system, limbs and dropped guns fall toward their own world (in
    /// orbit, they fall along with the ship instead of along Unity's -Y), and a bullet mark on the ship stays on
    /// the ship as it flies. How a fight looks and feels on each world is on the playtest checklist.
    /// Run in Unity from Window > General > Test Runner > PlayMode.
    /// </summary>
    public class SolarCombatTests
    {
        GameObject _root;
        SolarSystemWorld _world;

        [SetUp]
        public void SetUp()
        {
            Layers.Configure();
            GameInput.Initialize();
            ContentDatabase.ResetStatics();
            SolarSystem.ResetStatics();
            ShipConfig.ResetStatics();
            ShipMeshes.ResetStatics();
            LaunchConfig.ResetStatics();
            CockpitConfig.ResetStatics();
            SuitConfig.ResetStatics();
            PlanetSurface.PictureWidthForTests = 64;
            _root = new GameObject("SolarCombatTestRoot");
            _world = _root.AddComponent<SolarSystemWorld>();
            _world.Build(4242, StartPlace.Orbit);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
            PlanetSurface.PictureWidthForTests = 0;
            SuitConfig.ResetStatics();
            TimeFlow.Reset();
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
        }

        Vector3 BesideTheShip(float metres)
        {
            Transform ship = _world.Ship.transform;
            return ship.position + ship.right * metres;
        }

        [UnityTest]
        public IEnumerator InOrbit_ALooseLimb_FallsAlongWithTheShip()
        {
            yield return Frames(4);
            GibManager gibs = GibManager.Instance;
            Assert.IsNotNull(gibs, "the solar world should have its own gib manager");
            Rigidbody ship = _world.Ship.Body;
            Gib gib = gibs.Spawn(null, 0, BesideTheShip(8f), Quaternion.identity);
            gib.Body.linearVelocity = ship.linearVelocity;
            Assert.IsTrue(gib.OwnPull, "in the solar system a limb is pulled toward its own world");
            Assert.IsFalse(gib.Body.useGravity, "and never by Unity's flat gravity");

            Vector3 startOffset = gib.Body.position - ship.position;
            yield return Steps(100);
            Vector3 drift = (gib.Body.position - ship.position) - startOffset;
            // Two seconds of Unity's -9.81 would be about 20 m; falling with the ship it is next to nothing.
            Assert.Less(drift.magnitude, 1f, "beside a ship in orbit, a limb falls with the ship");
            gibs.Release(gib);
        }

        [UnityTest]
        public IEnumerator InOrbit_ADroppedGun_FallsAlongWithTheShip_AndIsPulledOnlyWhileLoose()
        {
            yield return Frames(4);
            Rigidbody ship = _world.Ship.Body;
            Weapon gun = Weapon.Create(WeaponData.Rifle, _root.transform, true);
            gun.transform.position = BesideTheShip(-8f);
            gun.Drop(_root.transform, ship.linearVelocity, Vector3.zero);
            GravityBody pull = gun.GetComponent<GravityBody>();
            Assert.IsNotNull(pull, "a dropped gun in the solar system is pulled toward its world");
            Assert.IsTrue(pull.enabled);
            Assert.IsFalse(gun.Body.useGravity);

            Vector3 startOffset = gun.Body.position - ship.position;
            yield return Steps(100);
            Vector3 drift = (gun.Body.position - ship.position) - startOffset;
            Assert.Less(drift.magnitude, 1f, "beside a ship in orbit, a dropped gun falls with the ship");

            gun.TakeByPlayer();
            Assert.IsFalse(pull.enabled, "a gun in someone's hands is not a loose object");
            Object.Destroy(gun.gameObject);
        }

        [UnityTest]
        public IEnumerator ABulletMarkOnTheShip_StaysOnTheShip_AsItFlies()
        {
            yield return Frames(4);
            WeaponFx fx = WeaponFx.Instance;
            Assert.IsNotNull(fx, "the solar world should have its own gunfire effects");
            OrbitalAnte.Ship.Ship ship = _world.Ship;
            Vector3 local = new Vector3(0f, ShipLayout.BellyY, 0f);
            fx.Impact(ship.transform.TransformPoint(local), -ship.transform.up, ship.transform);

            ship.Body.linearVelocity += ship.transform.forward * 20f;
            yield return Steps(50);
            // At the end of a frame both the ship (interpolated before Update) and the mark (put back in
            // LateUpdate) show this frame's pose, so they can be compared exactly.
            yield return new WaitForEndOfFrame();

            var marks = (Transform[])typeof(WeaponFx).GetField("_marks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fx);
            Transform mark = null;
            foreach (Transform m in marks) if (m != null && m.gameObject.activeSelf) mark = m;
            Assert.IsNotNull(mark, "the mark should still be showing");
            Vector3 onShip = ship.transform.InverseTransformPoint(mark.position);
            Assert.Less(Vector3.Distance(onShip, local), 0.05f, "the mark should still be where it was made on the hull");
        }
    }
}
