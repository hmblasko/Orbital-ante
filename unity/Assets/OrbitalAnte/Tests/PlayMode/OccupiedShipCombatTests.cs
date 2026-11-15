using System.Reflection;
using NUnit.Framework;
using OrbitalAnte.AI;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.Ship;
using OrbitalAnte.Space;
using OrbitalAnte.Spacewalk;
using OrbitalAnte.Weapons;
using UnityEngine;

namespace OrbitalAnte.Tests
{
    /// <summary>Real ship/astronaut/physics regressions. Requires Unity; compilation does not execute these.</summary>
    public class OccupiedShipCombatTests
    {
        GameObject _root;
        OrbitalAnte.Ship.Ship _ship;
        Astronaut _crew;
        ShipConfig _shipConfig;
        TestCraftConfig _flightConfig;
        WeaponData _gun;

        [SetUp]
        public void SetUp()
        {
            Layers.Configure();
            GameInput.Initialize();
            CombatWorld.Reset();
            SolarSystem.ResetStatics();
            Astronaut.ResetStatics();
            ShipMeshes.ResetStatics();
            PlanetMaterials.Configure(true);
            _root = new GameObject("OccupiedShipCombatTest");
            _shipConfig = ScriptableObject.CreateInstance<ShipConfig>();
            _shipConfig.testPistol = false;
            _shipConfig.fallbackBakePaths = 8;
            _flightConfig = ScriptableObject.CreateInstance<TestCraftConfig>();
            _ship = NewShip(Vector3.zero);
            _crew = NewCrew(_ship);
            // Distinct damage values catch accidentally applying person damage to the vessel or vice versa.
            _gun = WeaponData.CreatePistolDefaults();
            _gun.torsoDamage = 13f;
            _gun.playerDamage = 7f;
            _gun.range = 100f;
            CombatWorld.SetPlayerTarget(_crew);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            CombatWorld.Reset();
            if (_root != null) Object.DestroyImmediate(_root);
            if (_gun != null) Object.DestroyImmediate(_gun);
            if (_shipConfig != null) Object.DestroyImmediate(_shipConfig);
            if (_flightConfig != null) Object.DestroyImmediate(_flightConfig);
            Astronaut.ResetStatics();
            SolarSystem.ResetStatics();
            ShipMeshes.ResetStatics();
            PlanetMaterials.ResetStatics();
        }

        OrbitalAnte.Ship.Ship NewShip(Vector3 position)
        {
            var ship = OrbitalAnte.Ship.Ship.Create(_root.transform, _shipConfig, _flightConfig);
            ship.transform.position = position;
            ship.Body.position = position;
            ship.Body.isKinematic = true;
            ship.Flight.enabled = false;
            ship.enabled = false;
            return ship;
        }

        Astronaut NewCrew(OrbitalAnte.Ship.Ship ship)
        {
            // No planets are needed to exercise a real cabin, suit, armour and hull.
            Astronaut crew = Astronaut.Create(_root.transform, ship, _shipConfig, null);
            crew.enabled = false;
            crew.Eye.enabled = false;
            crew.Eye.GetComponent<AudioListener>().enabled = false;
            crew.GetComponent<SuitFx>().enabled = false;
            return crew;
        }

        void StandUp()
        {
            typeof(Astronaut).GetMethod("StandUp", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_crew, null);
            Assert.AreEqual(AstronautMode.Aboard, _crew.Mode);
            Assert.IsTrue(_ship.CrewAboard);
            CombatWorld.SetPlayerTarget(_crew);
            Physics.SyncTransforms();
        }

        Vector3 Local(float x, float y, float z) => _ship.transform.TransformPoint(new Vector3(x, y, z));

        RaycastHit FirstHit(Vector3 origin, Vector3 target)
        {
            Physics.SyncTransforms();
            Vector3 delta = target - origin;
            Assert.IsTrue(Physics.Raycast(origin, delta.normalized, out RaycastHit hit, delta.magnitude,
                Layers.EnemyShootMask, QueryTriggerInteraction.Ignore), "The fixture must contain a real physical occluder.");
            return hit;
        }

        BoxCollider Blocker(Vector3 center, Vector3 size)
        {
            var go = new GameObject("WorldOccluder");
            go.layer = Layers.Default;
            go.transform.SetParent(_root.transform, false);
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            Physics.SyncTransforms();
            return box;
        }

        [Test]
        public void ExteriorEnemiesSeeTheOccupiedVesselThroughItsOwnFirstHullHit()
        {
            Vector3 origin = Local(0f, 0.6f, -12f);
            Vector3 target = CombatWorld.AimPointFrom(origin, _ship.transform.up);
            Assert.AreSame(_ship, CombatWorld.OccupiedShip);
            Assert.Greater(Vector3.Distance(target, _crew.Eye.transform.position), 0.5f);
            Assert.AreSame(_ship, FirstHit(origin, target).collider.GetComponentInParent<OrbitalAnte.Ship.Ship>());
            Assert.IsTrue(CombatWorld.VisibleFrom(origin, target));
        }

        [Test]
        public void InterveningWorldGeometryBlocksSightOfTheOccupiedVessel()
        {
            Vector3 origin = Local(0f, 0.6f, -12f);
            Vector3 target = CombatWorld.AimPointFrom(origin, Vector3.up);
            BoxCollider box = Blocker(Local(0f, 0.6f, -6f), new Vector3(2f, 3f, 1f));
            Assert.AreSame(box, FirstHit(origin, target).collider);
            Assert.IsFalse(CombatWorld.VisibleFrom(origin, target));
        }

        [Test]
        public void AnotherVesselBlocksSightOfTheOccupiedVessel()
        {
            var other = NewShip(Local(0f, 0f, -6f));
            Vector3 origin = Local(0f, 0.6f, -12f);
            Vector3 target = CombatWorld.AimPointFrom(origin, Vector3.up);
            Assert.AreSame(other, FirstHit(origin, target).collider.GetComponentInParent<OrbitalAnte.Ship.Ship>());
            Assert.IsFalse(CombatWorld.VisibleFrom(origin, target));
        }

        [Test]
        public void AnExteriorLegHitDamagesThatPartWhileThePlayerIsAboard()
        {
            StandUp();
            int leg = _ship.Model.Find(PartKind.Leg, 0);
            Vector3 foot = _ship.transform.TransformPoint(new Vector3(ShipLayout.Footpad(0).X,
                ShipLayout.GroundY + ShipLayout.FootpadRadius, ShipLayout.Footpad(0).Z));
            Vector3 outward = Vector3.ProjectOnPlane(foot - _ship.transform.position, _ship.transform.up).normalized;
            Vector3 origin = foot + outward * 4f;
            RaycastHit hit = FirstHit(origin, foot);
            Assert.AreEqual(leg, hit.collider.GetComponentInParent<ShipPart>().Index);
            float legBefore = _ship.Model.Health(leg);
            float cabinBefore = _ship.Model.Health(_ship.CabinPart);
            float armourBefore = _crew.Armour.Current;
            ShotResult shot = Ballistics.FireFromEnemy(_gun, origin, (foot - origin).normalized);
            Assert.IsTrue(shot.HitAnything);
            Assert.IsFalse(shot.HitPlayer);
            Assert.AreSame(hit.collider, shot.Collider);
            Assert.AreEqual(legBefore - _gun.torsoDamage, _ship.Model.Health(leg), 0.001f);
            Assert.AreEqual(cabinBefore, _ship.Model.Health(_ship.CabinPart));
            Assert.AreEqual(armourBefore, _crew.Armour.Current);
        }

        [Test]
        public void AnEnemyInsideTheCabinCanShootThePlayerWithTheSuitObjectInactive()
        {
            StandUp();
            Vector3 origin = Local(1.15f, 0.9f, 0.2f);
            Vector3 target = _crew.transform.position + _ship.transform.up * 0.9f;
            Vector3 direction = (target - origin).normalized;
            Assert.IsFalse(_crew.Suit.gameObject.activeInHierarchy);
            Assert.IsTrue(CombatWorld.InsideOccupiedCabin(origin));
            Assert.Less(Vector3.Distance(CombatWorld.AimPointFrom(origin, _ship.transform.up),
                _crew.Eye.transform.position - _ship.transform.up * 0.3f), 0.001f);
            Assert.IsTrue(CombatWorld.RaycastPlayer(origin, direction, _gun.range, out _, out _, out _));
            float before = _crew.Armour.Current;
            float cabinBefore = _ship.Model.Health(_ship.CabinPart);
            ShotResult shot = Ballistics.FireFromEnemy(_gun, origin, direction, _ship);
            Assert.IsTrue(shot.HitPlayer);
            Assert.AreEqual(before - _gun.playerDamage, _crew.Armour.Current, 0.001f);
            Assert.AreEqual(cabinBefore, _ship.Model.Health(_ship.CabinPart));
        }

        [Test]
        public void TheHullStopsAnExteriorShotBeforeTheLogicalCabinOccupant()
        {
            Vector3 origin = Local(0f, 1.03f, -12f);
            Vector3 target = _crew.Eye.transform.position - _ship.transform.up * 0.15f;
            Vector3 direction = (target - origin).normalized;
            Assert.IsTrue(CombatWorld.RaycastPlayer(origin, direction, _gun.range, out float personDistance, out _, out _));
            RaycastHit hull = FirstHit(origin, target);
            Assert.AreSame(_ship, hull.collider.GetComponentInParent<OrbitalAnte.Ship.Ship>());
            Assert.Less(hull.distance, personDistance);
            float before = _crew.Armour.Current;
            ShotResult shot = Ballistics.FireFromEnemy(_gun, origin, direction);
            Assert.IsTrue(shot.HitAnything);
            Assert.IsFalse(shot.HitPlayer);
            Assert.AreSame(hull.collider, shot.Collider);
            Assert.AreEqual(before, _crew.Armour.Current);
        }

        [Test]
        public void NearerCabinGeometryStopsAShotBeforeTheLogicalPlayerVolume()
        {
            Vector3 origin = Local(-1.5f, 1.03f, 1.28f);
            Vector3 target = _crew.Eye.transform.position - _ship.transform.up * 0.15f;
            BoxCollider box = Blocker((origin + target) * 0.5f, new Vector3(0.2f, 0.8f, 0.8f));
            Vector3 direction = (target - origin).normalized;
            Assert.IsTrue(CombatWorld.RaycastPlayer(origin, direction, _gun.range, out float personDistance, out _, out _));
            RaycastHit nearer = FirstHit(origin, target);
            Assert.AreSame(box, nearer.collider);
            Assert.Less(nearer.distance, personDistance);
            float before = _crew.Armour.Current;
            ShotResult shot = Ballistics.FireFromEnemy(_gun, origin, direction, _ship);
            Assert.AreSame(box, shot.Collider);
            Assert.IsFalse(shot.HitPlayer);
            Assert.AreEqual(before, _crew.Armour.Current);
        }

        [Test]
        public void AnEnemyShotFromInsideStopsAtItsSourceShipsInteriorWall()
        {
            Vector3 origin = Local(0f, 1.62f, -1.15f);
            Vector3 direction = -_ship.transform.forward;
            Assert.IsTrue(_ship.InteriorWall(origin, direction, out float distance, out Vector3 wall, out _));
            Assert.Greater(distance, 0f);
            float before = _ship.Model.Health(_ship.CabinPart);
            ShotResult shot = Ballistics.FireFromEnemy(_gun, origin, direction, _ship);
            Assert.IsTrue(shot.HitAnything);
            Assert.IsFalse(shot.HitPlayer);
            Assert.Less(Vector3.Distance(wall, shot.End), 0.02f);
            Assert.AreEqual(before - _gun.torsoDamage, _ship.Model.Health(_ship.CabinPart), 0.001f);
        }

        [Test]
        public void TargetRevisionChangesOncePerIdentityTransitionAndStaysStableWithinTheShip()
        {
            int seated = CombatWorld.TargetRevision;
            for (int i = 0; i < 20; i++) CombatWorld.SetPlayerTarget(_crew);
            Assert.AreEqual(seated, CombatWorld.TargetRevision);
            StandUp();
            Assert.AreEqual(seated, CombatWorld.TargetRevision, "Unbuckling must not grant another reaction delay.");
            _crew.StepOutsideForTests(Local(10f, 0f, 0f), Vector3.zero);
            CombatWorld.SetPlayerTarget(_crew);
            int outside = CombatWorld.TargetRevision;
            Assert.AreEqual(unchecked(seated + 1), outside);
            Assert.IsNull(CombatWorld.OccupiedShip);
            for (int i = 0; i < 20; i++) CombatWorld.SetPlayerTarget(_crew);
            Assert.AreEqual(outside, CombatWorld.TargetRevision);

            _crew.Die("Combat target regression");
            CombatWorld.SetPlayerTarget(_crew);
            int dead = CombatWorld.TargetRevision;
            Assert.AreEqual(unchecked(outside + 1), dead);
            Assert.IsFalse(CombatWorld.PlayerPresent);
            for (int i = 0; i < 20; i++) CombatWorld.SetPlayerTarget(_crew);
            Assert.AreEqual(dead, CombatWorld.TargetRevision, "A dead player must not change identity every frame.");

            _crew = NewCrew(_ship);
            CombatWorld.SetPlayerTarget(_crew);
            Assert.AreEqual(unchecked(dead + 1), CombatWorld.TargetRevision);
            Assert.AreSame(_ship, CombatWorld.OccupiedShip);
            Assert.IsTrue(CombatWorld.PlayerPresent);
            int restarted = CombatWorld.TargetRevision;
            for (int i = 0; i < 20; i++) CombatWorld.SetPlayerTarget(_crew);
            Assert.AreEqual(restarted, CombatWorld.TargetRevision);
        }
    }
}
