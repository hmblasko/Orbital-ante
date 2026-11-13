using System;
using System.Collections;
using NUnit.Framework;
using OrbitalAnte.Cockpit;
using OrbitalAnte.Content;
using OrbitalAnte.Core;
using OrbitalAnte.Launch;
using OrbitalAnte.Navigation;
using OrbitalAnte.Planets;
using OrbitalAnte.Ship;
using OrbitalAnte.Space;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OrbitalAnte.Tests
{
    /// <summary>Regression boundaries for Fable's navigation controller; no replacement planner or tuning.</summary>
    public class NavigationCorrectnessTests
    {
        GameObject _root;
        SolarSystemWorld _world;
        Keyboard _keyboard;
        bool _wasFlying, _wasEnabled, _wasPaused;
        string _bindings;

        [SetUp]
        public void SetUp()
        {
            Layers.Configure();
            GameInput.Initialize();
            _wasFlying = GameInput.Play == GameInput.Flight;
            _wasEnabled = GameInput.Play.enabled;
            _wasPaused = GameSession.Paused;
            _bindings = GameInput.Asset.SaveBindingOverridesAsJson();
            GameSession.SetPaused(false);
            GameInput.Flight.RemoveAllBindingOverrides();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            ContentDatabase.ResetStatics();
            SolarSystem.ResetStatics();
            ShipConfig.ResetStatics();
            ShipMeshes.ResetStatics();
            LaunchConfig.ResetStatics();
            CockpitConfig.ResetStatics();
            PlanetSurface.PictureWidthForTests = 64;
            _root = new GameObject("NavigationCorrectnessRoot");
            _world = _root.AddComponent<SolarSystemWorld>();
            _world.Build(4242, StartPlace.Orbit);
            GameInput.SetFlying(true);
            GameInput.Flight.Enable();
        }

        [TearDown]
        public void TearDown()
        {
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            if (_root != null) Object.DestroyImmediate(_root);
            PlanetSurface.PictureWidthForTests = 0;
            GameInput.Asset.LoadBindingOverridesFromJson(_bindings);
            GameInput.SetFlying(_wasFlying);
            if (_wasEnabled) GameInput.Play.Enable(); else GameInput.Play.Disable();
            GameSession.SetPaused(_wasPaused);
        }

        int PrepareEarthLock()
        {
            SolarSystem system = _world.System;
            int earth = system.Model.IndexOf("Earth");
            Vec3d offset = system.Model.SpinAxis(earth).AnyPerpendicular() * (system.Model.Spec(earth).radius + 2000);
            system.Place(_world.Craft.Gravity, system.States[earth].Position + offset, system.States[earth].Velocity);
            Vector3 toward = system.Bodies[earth].transform.position - _world.Craft.Body.position;
            Quaternion rotation = Quaternion.LookRotation(toward.normalized);
            _world.Craft.transform.rotation = rotation;
            _world.Craft.Body.rotation = rotation;
            _world.Craft.Unmanned = false;
            _world.Craft.SendMessage("UpdateTarget", SendMessageOptions.RequireReceiver);
            Assert.AreEqual(earth, _world.Craft.UnderNose);
            _world.Craft.PressLock();
            _world.Craft.SendMessage("UpdateTarget", SendMessageOptions.RequireReceiver);
            Assert.AreEqual(earth, _world.Craft.Target);
            return earth;
        }

        static Vector3 QueuedForce(Rigidbody body)
        {
            // Invoke Unity 6's real queued-force diagnostic. Reflection also lets the source compile against
            // older reference assemblies which lack its metadata; it supplies no simulated engine behavior.
            var method = typeof(Rigidbody).GetMethod("GetAccumulatedForce", new[] { typeof(float) });
            Assert.IsNotNull(method, "This engine-boundary case requires Unity 6's GetAccumulatedForce.");
            return (Vector3)method.Invoke(body, new object[] { Time.fixedDeltaTime });
        }

        [Test]
        public void AutomaticStopDropsCachedJetsAcrossCatchUpStepsButManualTakeoverKeepsFreshThrust()
        {
            PrepareEarthLock();
            TestCraft craft = _world.Craft;
            Assert.IsTrue(craft.StartPilot());
            craft.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
            craft.SendMessage("Update", SendMessageOptions.RequireReceiver);
            Assert.Greater(craft.ThrustInput.sqrMagnitude, 0.01f, "The previous autopilot jets must be represented on the dash.");
            Vector3 before = QueuedForce(craft.Body);
            craft.PushScale = 0.3f;
            craft.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
            Assert.IsFalse(craft.PilotOn);
            Assert.AreEqual(PilotStop.NoFuel, craft.LastStop);
            Assert.AreEqual(Vector3.zero, craft.ThrustInput);
            craft.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
            craft.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
            Assert.Less(Vector3.Distance(before, QueuedForce(craft.Body)), 0.001f,
                "No stopped autopilot jets may leak into a later manual/catch-up physics step.");

            craft.PushScale = 1f;
            Assert.IsTrue(craft.StartPilot());
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W));
            InputSystem.Update();
            craft.SendMessage("Update", SendMessageOptions.RequireReceiver);
            Assert.AreEqual(PilotStop.PilotTookOver, craft.LastStop);
            Assert.Greater(craft.ThrustInput.z, 0.9f, "A real manual takeover must retain its freshly sampled thrust.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LeavingTheSeatOrDyingCancelsAssistBeforeAnotherPhysicsStep(bool dying)
        {
            PrepareEarthLock();
            TestCraft craft = _world.Craft;
            Assert.IsTrue(craft.StartPilot());
            craft.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
            craft.SendMessage("Update", SendMessageOptions.RequireReceiver);
            Vector3 before = QueuedForce(craft.Body);

            if (dying) _world.Astronaut.Die("Regression boundary");
            else
            {
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.B));
                InputSystem.Update();
                _world.Astronaut.SendMessage("UpdateSeated", SendMessageOptions.RequireReceiver);
                Assert.IsFalse(_world.Ship.Belt.Buckled);
            }

            Assert.IsTrue(craft.Unmanned);
            Assert.IsFalse(craft.PilotOn);
            Assert.AreEqual(PilotStop.Unbuckled, craft.LastStop);
            Assert.AreEqual(Vector3.zero, craft.ThrustInput);
            craft.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
            craft.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
            Assert.Less(Vector3.Distance(before, QueuedForce(craft.Body)), 0.001f);
        }

        [Test]
        public void TargetTranslationIsMeasuredAtTheCraftInRotatingFramesAndGroundMatchingIsRetained()
        {
            int earth = PrepareEarthLock();
            SolarSystem system = _world.System;
            TestCraft craft = _world.Craft;
            Vec3d offset = system.Model.SpinAxis(earth).AnyPerpendicular() * (system.Model.Spec(earth).radius + 2000);
            var drift = new Vec3d(23, -4, 2);
            foreach (bool rotating in new[] { false, true })
            {
                system.Frame.StopRotating(system.States);
                system.Frame.SetAnchor(earth, system.States);
                if (rotating) system.Frame.StartRotating(system.States);
                system.Place(craft.Gravity, system.States[earth].Position + offset, system.States[earth].Velocity + drift);
                Vector3 expected = SpaceConvert.ToVector3(system.Frame.ToLocalDirection(drift, system.States));
                Assert.Less(Vector3.Distance(expected, craft.RelativeVelocity()), 0.01f);
                Assert.IsFalse(craft.AgainstGround);
            }
            offset = offset.Normalized * (system.Model.Spec(earth).radius + 100);
            system.Place(craft.Gravity, system.States[earth].Position + offset, system.States[earth].Velocity);
            craft.Body.linearVelocity = system.GroundVelocityAt(earth, craft.Body.position);
            Assert.Less(craft.RelativeVelocity().magnitude, 0.01f);
            Assert.IsTrue(craft.AgainstGround);
        }

        [Test]
        public void PhysicsSnapshotRetainsTheStartMomentWhilePublishedBodiesAdvance()
        {
            int earth = PrepareEarthLock();
            SolarSystem system = _world.System;
            TestCraft craft = _world.Craft;
            Vec3d position = system.TruePosition(craft.Body.position);
            // Hold an inertial frame so the destination visibly advances while the craft is still at the
            // start moment. Following Earth would hide this phase error by keeping its centre still.
            system.Focus = null;
            system.Frame.SetAnchor(0, system.States);
            system.Frame.StopRotating(system.States);
            system.Place(craft.Gravity, position, system.States[earth].Velocity);
            Assert.IsTrue(craft.StartPilot());
            BodyState before = system.States[earth];
            Assert.AreEqual(before.Position, system.PhysicsStates[earth].Position);
            system.Step(0.02f);
            Assert.AreEqual(before.Position, system.PhysicsStates[earth].Position);
            Assert.AreEqual(before.Velocity, system.PhysicsStates[earth].Velocity);
            Assert.Greater(Vec3d.Distance(before.Position, system.States[earth].Position), 0.1);
            Assert.AreNotSame(system.States, system.PhysicsStates);
            BodyState[] start = system.PhysicsStates;
            Vector3 toward = SpaceConvert.ToVector3(system.Frame.ToLocalPosition(start[earth].Position, start)) - craft.Body.position;
            Vector3 reference = SpaceConvert.ToVector3(system.Frame.ToLocalVelocity(position, start[earth].Velocity, start));
            PilotOrder expected = Autopilot.Fly(craft.PilotRules, SpaceConvert.ToVec3d(toward),
                SpaceConvert.ToVec3d(craft.Body.linearVelocity - reference), system.Model.Spec(earth).radius,
                SpaceConvert.ToVec3d(craft.Gravity.FeltGravity));
            craft.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
            Assert.Less(Vec3d.Distance(expected.Push, craft.Order.Push), 1e-6,
                "The real controller must guide from the same start moment used by gravity.");
        }

        [UnityTest]
        public IEnumerator ReadingTheMapChargesTheSameAutopilotPushWhileManualControlsStayLocked()
        {
            yield return null; // Obtain a real positive engine delta time for the dash's Update boundary.
            PrepareEarthLock();
            TestCraft craft = _world.Craft;
            CockpitDash dash = _world.Dash;
            Assert.IsTrue(craft.StartPilot());
            craft.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
            Assert.Greater(craft.PilotPushShare, 0.01f);
            dash.Supplies.Set(0.8f, 1f);
            _world.Map.SetOpen(true);
            Assert.IsTrue(craft.ControlsLocked);
            craft.SendMessage("Update", SendMessageOptions.RequireReceiver);
            float before = dash.Supplies.Fuel;
            float expectedUse = Mathf.Clamp(craft.PilotPushShare, 0f, 3f) * Time.deltaTime / Mathf.Max(1f, dash.Supplies.Rules.fuelSeconds);
            dash.SendMessage("Update", SendMessageOptions.RequireReceiver);
            Assert.IsTrue(craft.PilotOn, "Fable's autopilot continues while the pilot reads the map.");
            Assert.AreEqual(Vector3.zero, craft.ThrustInput, "Opening the map still blocks manual flight input.");
            Assert.Less(dash.Supplies.Fuel, before);
            Assert.AreEqual(expectedUse, before - dash.Supplies.Fuel, 1e-6f);
        }
    }
}
