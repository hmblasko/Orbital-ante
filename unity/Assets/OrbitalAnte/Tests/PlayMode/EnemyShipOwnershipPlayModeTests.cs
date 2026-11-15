using System.Collections;
using System.Reflection;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Enemies;
using OrbitalAnte.Ship;
using OrbitalAnte.Space;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OrbitalAnte.Tests.PlayMode
{
    /// <summary>Engine regression drafts. Reflection supplies small fixtures; production methods own each transition.</summary>
    public sealed class EnemyShipOwnershipPlayModeTests
    {
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        GameObject _root;
        SolarSystemWorld _world;
        Ship.Ship _ship;
        TestCraftConfig _flightConfig;
        EnemyConfig _enemyConfig;
        MannequinConfig _actorConfig;

        [SetUp]
        public void SetUp()
        {
            Layers.Configure();
            GameSession.SetPaused(false);
            _root = new GameObject("EnemyShipOwnershipTests");
            _world = _root.AddComponent<SolarSystemWorld>();
            _flightConfig = ScriptableObject.CreateInstance<TestCraftConfig>();
            _enemyConfig = ScriptableObject.CreateInstance<EnemyConfig>();
            _actorConfig = ScriptableObject.CreateInstance<MannequinConfig>();
            var hull = Child("Ship");
            var body = hull.AddComponent<Rigidbody>();
            body.useGravity = false;
            var gravity = hull.AddComponent<GravityBody>();
            var flight = hull.AddComponent<TestCraft>();
            flight.enabled = false;
            Set(flight, "Body", body);
            Set(flight, "Gravity", gravity);
            Set(flight, "Config", _flightConfig);
            _ship = hull.AddComponent<Ship.Ship>();
            _ship.enabled = false;
            Set(_ship, "Body", body);
            Set(_ship, "Gravity", gravity);
            Set(_ship, "Flight", flight);
            Set(_ship, "Hatch", new HatchLift());
            Set(_ship, "Belt", new SeatBelt());
            _ship.SetNpcShip();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_flightConfig);
            Object.DestroyImmediate(_enemyConfig);
            Object.DestroyImmediate(_actorConfig);
        }

        [Test]
        public void PilotClaimsHaveOneOwner_AndAnotherActorCannotClearItsCommand()
        {
            object first = new object(), second = new object();
            Assert.IsTrue(_ship.TryClaimPilot(first, true));
            _ship.Flight.SetExternalCommand(Vector3.right * 6f, Quaternion.identity);
            Assert.IsFalse(_ship.TryClaimPilot(second));
            _ship.ReleasePilot(second);
            Assert.AreSame(first, _ship.PilotOwner);
            Assert.Greater(_ship.Flight.ThrustInput.magnitude, 0f);
            _ship.ReleasePilot(first);
            Assert.IsNull(_ship.PilotOwner);
            Assert.IsTrue(_ship.Flight.Unmanned);
            Assert.AreEqual(Vector3.zero, _ship.Flight.ThrustInput);
            Assert.IsTrue(_ship.TryClaimPilot(second));
            Assert.IsFalse(_ship.Flight.UseExternalControl, "a player claim switches this ship to manual authority");
        }

        [Test]
        public void DisablingAPilotReleasesBothSeatAndLiftReservation()
        {
            EnemyShipCrew crew = Crew();
            Assert.IsTrue(crew.StartAsPilot(_ship));
            _ship.Flight.SetExternalCommand(Vector3.up * 5f, Quaternion.identity);
            _ship.Hatch.Set(true, true);
            _ship.Hatch.Tick(1f);
            Assert.IsTrue(_ship.Hatch.TryBeginDown(crew, new P3(), ShipLayout.GroundY));
            crew.enabled = false;
            Assert.IsFalse(crew.CanPilot);
            Assert.IsFalse(crew.Piloting);
            Assert.IsNull(_ship.PilotOwner);
            Assert.IsNull(_ship.Hatch.RiderActor);
            Assert.AreEqual(LiftRide.None, _ship.Hatch.Ride);
            Assert.IsTrue(_ship.Flight.Unmanned);
            Assert.AreEqual(Vector3.zero, _ship.Flight.ThrustInput);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CancelingAnUnfinishedAscentLeavesTheActorAtTheLiftPointBelowTheHull(bool disableActor)
        {
            EnemyShipCrew crew = Crew();
            _ship.Body.position = new Vector3(12f, 34f, 56f);
            _ship.Body.rotation = Quaternion.Euler(15f, 70f, 5f);
            _ship.Hatch.Set(true, true);
            _ship.Hatch.Tick(1f);
            var feet = new P3(0f, ShipLayout.GroundY, 0f);
            Assert.IsTrue(_ship.Hatch.TryBeginUp(crew, feet));
            Call(crew, "EnterCabin", _ship, new Vector3(feet.X, feet.Y, feet.Z));
            Set(crew, "Transition", CrewTransition.LiftUp);
            _ship.Hatch.Tick(0.2f);
            P3 rider = _ship.Hatch.Rider;
            Assert.Less(rider.Y, ShipLayout.BellyY);
            Vector3 point = _ship.Body.position + _ship.Body.rotation * new Vector3(rider.X, rider.Y, rider.Z);
            if (disableActor) crew.enabled = false;
            else
            {
                _ship.Hatch.Release(crew);
                Call(crew, "UpdateLift", LiftRide.Up);
            }
            Assert.IsNull(crew.CurrentShip);
            Assert.AreEqual(CrewTransition.Exterior, crew.Transition);
            Assert.Less(Vector3.Distance(point, crew.GetComponent<Rigidbody>().position), 0.001f);
            Assert.IsFalse(crew.GetComponent<Rigidbody>().isKinematic);
            Assert.IsNull(_ship.Hatch.RiderActor);
            Assert.AreEqual(LiftRide.None, _ship.Hatch.Ride);
        }

        [UnityTest]
        public IEnumerator LosingBothHandsReleasesThePilotEvenWithoutAMovementStateChange()
        {
            EnemyShipCrew crew = Crew();
            Assert.IsTrue(crew.StartAsPilot(_ship));
            Mannequin actor = crew.GetComponent<Mannequin>();
            actor.Model.ApplyDamage(BodyPartId.LeftHand, 1000f);
            actor.Model.ApplyDamage(BodyPartId.RightHand, 1000f);
            Assert.AreEqual(MovementState.Intact, actor.State);
            yield return null;
            Assert.IsFalse(crew.CanPilot);
            Assert.IsNull(_ship.PilotOwner);
            Assert.AreEqual(CrewTransition.Cabin, crew.Transition);
            Assert.IsTrue(_ship.Flight.Unmanned);
        }

        [Test]
        public void DisembarkingReusesTheActorsBody_AndInheritsVelocityAtTheActualExitPoint()
        {
            EnemyShipCrew crew = Crew();
            Rigidbody actorBody = crew.GetComponent<Rigidbody>();
            _ship.Body.position = new Vector3(10f, 20f, 30f);
            _ship.Body.rotation = Quaternion.Euler(20f, 45f, 10f);
            _ship.Body.centerOfMass = new Vector3(0.4f, -1f, 0.2f);
            _ship.Body.linearVelocity = new Vector3(3f, -2f, 4f);
            _ship.Body.angularVelocity = new Vector3(0.3f, 0.6f, 0.8f);
            Assert.IsTrue(crew.StartAsPilot(_ship));
            _ship.Hatch.Set(true, true);
            _ship.Hatch.Tick(1f);
            Assert.IsTrue(_ship.Hatch.TryBeginDown(crew, new P3(), ShipLayout.GroundY));
            _ship.Hatch.Tick(10f);
            P3 rider = _ship.Hatch.Rider;
            Vector3 exit = _ship.Body.position + _ship.Body.rotation * new Vector3(rider.X, rider.Y, rider.Z);
            Vector3 inherited = _ship.Body.GetPointVelocity(exit);
            Call(crew, "UpdateLift", LiftRide.Down);
            Assert.AreSame(actorBody, crew.GetComponent<Rigidbody>());
            Assert.AreEqual(1, crew.GetComponents<Rigidbody>().Length);
            Assert.IsFalse(actorBody.isKinematic);
            Assert.Less(Vector3.Distance(exit, actorBody.position), 0.001f);
            Assert.Less(Vector3.Distance(inherited, actorBody.linearVelocity), 0.001f);
            Assert.IsNull(crew.CurrentShip);
            Assert.AreEqual(CrewTransition.Exterior, crew.Transition);
            Assert.IsFalse(crew.GetComponent<GravityBody>().NeedsGround, "the four reservations belong to ships and suit");
        }

        [TestCase(1.2f)]
        [TestCase(1.3f)]
        public void AnOversizedActorDoesNotEnterThePersonSizedLiftEvenWhenItIsOpen(float size)
        {
            EnemyShipCrew crew = Crew(size);
            Assert.IsFalse(crew.FitsStarterCabin);
            Assert.IsFalse(crew.StartAsPilot(_ship));
            crew.transform.position = _ship.transform.TransformPoint(new Vector3(0f, ShipLayout.GroundY, 0f));
            Call(crew, "UpdateExterior");
            Assert.IsNull(crew.CurrentShip);
            _ship.Hatch.Set(true, true);
            _ship.Hatch.Tick(1f);
            Assert.IsTrue(_ship.Hatch.Open);
            Call(crew, "UpdateExterior");
            Assert.IsNull(crew.CurrentShip);
            Assert.IsNull(_ship.PilotOwner);
            Assert.AreEqual(LiftRide.None, _ship.Hatch.Ride);
        }

        EnemyShipCrew Crew(float size = 1f)
        {
            var actor = Child("Actor");
            actor.transform.localScale = Vector3.one * size;
            actor.AddComponent<Rigidbody>().isKinematic = true;
            var capsule = Child("CharacterBody");
            capsule.transform.SetParent(actor.transform, false);
            capsule.layer = Layers.CharacterBody;
            var shape = capsule.AddComponent<CapsuleCollider>();
            shape.height = ShipLayout.PersonHeight;
            shape.center = Vector3.up * (ShipLayout.PersonHeight * 0.5f);
            shape.radius = 0.28f;
            var mannequin = actor.AddComponent<Mannequin>();
            Set(mannequin, "Config", _actorConfig);
            Set(mannequin, "Model", new BodyModel(_actorConfig.BuildHpTable()));
            var crew = actor.AddComponent<EnemyShipCrew>();
            crew.Initialize(_world, _enemyConfig, mannequin);
            return crew;
        }

        GameObject Child(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(_root.transform, false);
            return child;
        }

        static void Set(object target, string name, object value) => target.GetType().GetProperty(name, Members).SetValue(target, value);
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Members).Invoke(target, args);
    }
}
