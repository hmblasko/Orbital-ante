using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Ship;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Engine-only checks at the release boundary. These do not simulate walking, input, or the whole lift ride.
    /// Run in Unity's PlayMode Test Runner; a standalone C# compilation does not execute these cases.
    /// </summary>
    public class ShipExitTests
    {
        GameObject _root;
        Ship.Ship _ship;
        Astronaut _astronaut;
        ShipConfig _shipConfig;
        TestCraftConfig _flightConfig;
        bool _wasFlying, _wasEnabled;

        [SetUp]
        public void SetUp()
        {
            Layers.Configure();
            GameInput.Initialize();
            _wasFlying = GameInput.Play == GameInput.Flight;
            _wasEnabled = GameInput.Play.enabled;
            _root = new GameObject("SpaceExitTest");
            _shipConfig = ScriptableObject.CreateInstance<ShipConfig>();
            _shipConfig.testPistol = false;
            _flightConfig = ScriptableObject.CreateInstance<TestCraftConfig>();
            _ship = Ship.Ship.Create(_root.transform, _shipConfig, _flightConfig);
            _astronaut = Astronaut.Create(_root.transform, _ship, _shipConfig, null);
            // Keep a synchronous boundary check away from an opened scene's ground or launch pad.
            Vector3 position = new Vector3(10000f, 10000f, 10000f);
            Quaternion rotation = Quaternion.Euler(15f, 35f, -25f);
            _ship.transform.SetPositionAndRotation(position, rotation);
            _ship.Body.position = position;
            _ship.Body.rotation = rotation;
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_shipConfig);
            Object.DestroyImmediate(_flightConfig);
            GameInput.SetFlying(_wasFlying);
            if (_wasEnabled) GameInput.Play.Enable();
            else GameInput.Play.Disable();
        }

        [TestCase(0f, 0f, 0f)]
        [TestCase(0.8f, -1.2f, 0.4f)]
        public void ReleasePreservesMotionAtTheCapsulesMassCentre(float spinX, float spinY, float spinZ)
        {
            Vector3 velocity = new Vector3(170f, -25f, 80f);
            Vector3 angular = new Vector3(spinX, spinY, spinZ);
            _ship.Body.linearVelocity = velocity;
            _ship.Body.angularVelocity = angular;
            Vector3 feet = _ship.Body.position + _ship.Body.rotation * new Vector3(0f, _ship.Hatch.SpaceExitY, 0f);
            // Invoke the existing private MonoBehaviour transition at its boundary, without adding a gameplay
            // teleport/debug API or pretending this exercises the preceding player-input sequence.
            _astronaut.SendMessage("GoOutside", feet, SendMessageOptions.RequireReceiver);
            Vector3 expected = velocity + Vector3.Cross(angular, _astronaut.Suit.worldCenterOfMass - _ship.Body.worldCenterOfMass);
            Assert.AreEqual(AstronautMode.Outside, _astronaut.Mode);
            Assert.IsTrue(_astronaut.Suit.gameObject.activeSelf);
            Assert.Less(Vector3.Distance(expected, _astronaut.Suit.linearVelocity), 0.01f);
            Vector3 localFeet = Quaternion.Inverse(_ship.Body.rotation) * (_astronaut.Suit.position - _ship.Body.position);
            Assert.IsTrue(_ship.Hatch.HasExitClearance(localFeet.y));
        }

        [Test]
        public void AnEmptyGroundRayStillHasAClearSpaceReleasePoint()
        {
            Assert.IsTrue(float.IsNaN(_ship.FindLiftBottom()));
            _ship.Hatch.Set(true, hatchWorks: true);
            for (int i = 0; i < 120; i++) _ship.Hatch.Tick(1f / 60f);
            _ship.Hatch.BeginDown(P3.Zero, _ship.FindLiftBottom());
            LiftRide result = LiftRide.None;
            for (int i = 0; i < 2000 && result == LiftRide.None; i++) result = _ship.Hatch.Tick(1f / 60f);
            Assert.AreEqual(LiftRide.Down, result);
            Assert.IsTrue(_ship.Hatch.HasExitClearance(_ship.Hatch.Rider.Y));
            Assert.IsTrue(_ship.Hatch.CanPickUp(_ship.Hatch.Rider, requested: true));
        }

        [Test]
        public void AWallBesideTheGroundRayCannotReceiveAnIntersectingSuit()
        {
            Vector3 localFeet = new Vector3(0f, _ship.Hatch.SpaceExitY, 0f);
            Vector3 feet = _ship.Body.position + _ship.Body.rotation * localFeet;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(_root.transform, false);
            wall.transform.SetPositionAndRotation(_ship.Body.position + _ship.Body.rotation
                * (localFeet + new Vector3(0.24f, ShipLayout.PersonHeight * 0.5f, 0f)), _ship.Body.rotation);
            wall.transform.localScale = new Vector3(0.08f, 1f, 0.5f);
            Physics.SyncTransforms();
            Assert.IsTrue(float.IsNaN(_ship.FindLiftBottom()), "The off-axis obstacle intentionally misses the ground ray.");

            _astronaut.SendMessage("GoOutside", feet, SendMessageOptions.RequireReceiver);

            Assert.AreEqual(AstronautMode.Lift, _astronaut.Mode);
            Assert.AreEqual(LiftRide.Up, _ship.Hatch.Ride);
            Assert.IsFalse(_astronaut.Suit.gameObject.activeSelf);
        }
    }
}
