using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Space;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Engine-boundary checks for actual input actions, Rigidbody state, and reference-frame conversions.
    /// These synchronous cases do not replace a Unity flight/landing rehearsal or controller feel pass.
    /// </summary>
    public class FlightNavigationPlayModeTests
    {
        GameObject _root;
        SolarSystem _system;
        TestCraft _craft;
        SolarSystemData _data;
        TestCraftConfig _config;
        Keyboard _keyboard;
        int _earth;
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
            GameInput.SetFlying(true);
            GameInput.Flight.Enable();
            GameInput.Flight.RemoveAllBindingOverrides();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            SolarSystem.ResetStatics();
            _root = new GameObject("NavigationBoundaryTests");
            var systemObject = new GameObject("System");
            systemObject.transform.SetParent(_root.transform, false);
            _system = systemObject.AddComponent<SolarSystem>();
            _data = ScriptableObject.CreateInstance<SolarSystemData>();
            List<BodySpec> defaults = SolarSystemDefaults.Bodies();
            _data.bodies = defaults.FindAll(b => b.id == "Sun" || b.id == "Earth" || b.id == "Moon");
            _data.asteroidBelt.count = 0;
            _system.Build(_data, 4242);
            _earth = _system.Model.IndexOf("Earth");
            _config = ScriptableObject.CreateInstance<TestCraftConfig>();
            var craftObject = new GameObject("Craft");
            craftObject.transform.SetParent(_root.transform, false);
            craftObject.AddComponent<Rigidbody>();
            _craft = TestCraft.Attach(craftObject, _config);
            var eyeObject = new GameObject("Eye");
            eyeObject.transform.SetParent(craftObject.transform, false);
            _craft.Eye = eyeObject.AddComponent<Camera>();
            _craft.Eye.enabled = false;
            _craft.System = _system;
            PlaceAboveEarth(2000, Vec3d.Zero);
            AimAt(_earth);
            Assert.IsTrue(_craft.ToggleTargetLock(), "Setup could not lock Earth.");
        }

        [TearDown]
        public void TearDown()
        {
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_data);
            SolarSystem.ResetStatics();
            GameInput.Asset.LoadBindingOverridesFromJson(_bindings);
            GameInput.SetFlying(_wasFlying);
            if (_wasEnabled) GameInput.Play.Enable(); else GameInput.Play.Disable();
            GameSession.SetPaused(_wasPaused);
        }

        void PlaceAboveEarth(double radius, Vec3d extraVelocity)
        {
            Vec3d offset = _system.Model.SpinAxis(_earth).AnyPerpendicular() * radius;
            _system.Place(_craft.Gravity, _system.States[_earth].Position + offset,
                _system.States[_earth].Velocity + extraVelocity);
        }

        void AimAt(int body)
        {
            _craft.Eye.transform.rotation = Quaternion.LookRotation(_system.Bodies[body].transform.position - _craft.Eye.transform.position);
        }

        void Keys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
            InputSystem.Update();
            _craft.SendMessage("Update", SendMessageOptions.RequireReceiver);
        }

        [Test]
        public void LookingAwayKeepsTheLockAndEngagingAssistCannotMoveTheCraftOrCamera()
        {
            Vector3 position = _craft.Body.position;
            Vector3 velocity = _craft.Body.linearVelocity;
            Quaternion rotation = _craft.Body.rotation;
            Vector3 toEarth = _system.Bodies[_earth].transform.position - _craft.Eye.transform.position;
            Vector3 toMoon = _system.Bodies[_system.Model.IndexOf("Moon")].transform.position - _craft.Eye.transform.position;
            _craft.Eye.transform.rotation = Quaternion.LookRotation(Vector3.Cross(toEarth, toMoon).normalized);
            _craft.SendMessage("Update", SendMessageOptions.RequireReceiver);
            Assert.AreEqual(-1, _craft.AimedTarget);
            Assert.AreEqual(_earth, _craft.Target);
            Assert.IsTrue(_craft.ToggleAutopilot());
            Assert.AreEqual(position, _craft.Body.position);
            Assert.AreEqual(velocity, _craft.Body.linearVelocity);
            Assert.AreEqual(rotation, _craft.Body.rotation);
        }

        [Test]
        public void GuidanceRetainsTheSameStartOfStepSnapshotUsedForGravity()
        {
            BodyState before = _system.States[_earth];
            Assert.AreEqual(before.Position, _system.PhysicsStates[_earth].Position, "Build initializes the physics snapshot.");
            _system.Step(0.02f); // Captures the craft's current moment before publishing the planets' next poses.
            Assert.AreEqual(before.Position, _system.PhysicsStates[_earth].Position);
            Assert.AreEqual(before.Velocity, _system.PhysicsStates[_earth].Velocity);
            Assert.Greater(Vec3d.Distance(before.Position, _system.States[_earth].Position), 0.1);
            Assert.AreNotSame(_system.States, _system.PhysicsStates);
        }

        [Test]
        public void MatchingOrbitalVelocityIsMeasuredAtTheCraftEvenInARotatingFrame()
        {
            _system.Frame.SetAnchor(_earth, _system.States);
            _system.Frame.StartRotating(_system.States);
            PlaceAboveEarth(2000, Vec3d.Zero); // Outside the ground-motion region, so match orbital translation.
            Assert.Less(_craft.RelativeVelocity().magnitude, 0.01f,
                "Subtracting the body's local centre velocity would invent spin-relative drift here.");
        }

        [Test]
        public void NearTheSurfaceGroundMotionMatchesInBothInertialAndRotatingFrames()
        {
            foreach (bool rotating in new[] { false, true })
            {
                _system.Frame.StopRotating(_system.States);
                _system.Frame.SetAnchor(rotating ? _earth : 0, _system.States);
                if (rotating) _system.Frame.StartRotating(_system.States);
                PlaceAboveEarth(600, Vec3d.Zero);
                _craft.Body.linearVelocity = _system.GroundVelocityAt(_earth, _craft.Body.position);
                Assert.Less(_craft.RelativeVelocity().magnitude, 0.01f);
            }
        }

        [Test]
        public void BeingInsideANominalBodyRadiusStillAllowsLookingDownToLockItOrOutwardToLockAMoon()
        {
            Assert.IsFalse(_craft.ToggleTargetLock()); // Clear the setup lock.
            PlaceAboveEarth(_system.Model.Spec(_earth).radius - 10, Vec3d.Zero);
            AimAt(_earth);
            Assert.IsTrue(_craft.ToggleTargetLock());
            Assert.AreEqual(_earth, _craft.Target);
            Assert.IsFalse(_craft.ToggleTargetLock());
            int moon = _system.Model.IndexOf("Moon");
            Vec3d outward = (_system.States[moon].Position - _system.States[_earth].Position).Normalized;
            _system.Place(_craft.Gravity, _system.States[_earth].Position + outward * (_system.Model.Spec(_earth).radius - 10), Vec3d.Zero);
            AimAt(moon);
            Assert.IsTrue(_craft.ToggleTargetLock());
            Assert.AreEqual(moon, _craft.Target);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MapAndLeavingTheSeatCancelImmediatelyAndKeepTheTarget(bool map)
        {
            Assert.IsTrue(_craft.ToggleAutopilot());
            if (map) _craft.ControlsLocked = true; else _craft.Unmanned = true;
            Assert.AreEqual(FlightAssistMode.Manual, _craft.AssistMode);
            Assert.AreEqual(Vector3.zero, _craft.ThrustInput);
            Assert.AreEqual(_earth, _craft.Target);
            if (map) _craft.ControlsLocked = false; else _craft.Unmanned = false;
            Assert.AreEqual(FlightAssistMode.Manual, _craft.AssistMode);
        }

        [Test]
        public void RealManualTranslationCancelsAPersistentHold()
        {
            Assert.IsTrue(_craft.ToggleVelocityLock());
            Keys(Key.W);
            Assert.AreEqual(FlightAssistMode.Manual, _craft.AssistMode);
            Assert.Greater(_craft.ThrustInput.z, 0.9f);
            StringAssert.Contains("manual thrust", _craft.NavigationNotice);
        }

        [Test]
        public void ExistingHoldMatchTemporarilyBrakesAndReleaseLeavesManualFlight()
        {
            Assert.IsTrue(_craft.ToggleAutopilot());
            Keys(Key.F);
            Assert.AreEqual(FlightAssistMode.Manual, _craft.AssistMode);
            Assert.IsTrue(_craft.MatchingVelocity);
            Assert.AreEqual(_earth, _craft.MatchBody);
            Keys();
            Assert.IsFalse(_craft.MatchingVelocity);
            Assert.IsFalse(_craft.VelocityLocked);
            Assert.AreEqual(_earth, _craft.Target);
        }

        [Test]
        public void ClearingTheTargetDuringAHoldRequiresReleaseBeforeAnotherMatchEvenAcrossPauseAndMap()
        {
            Keys(Key.F);
            Assert.IsTrue(_craft.MatchingVelocity);
            Assert.IsFalse(_craft.ToggleTargetLock());
            Keys(Key.F);
            Assert.IsFalse(_craft.MatchingVelocity);
            GameSession.SetPaused(true);
            Keys(Key.F);
            GameSession.SetPaused(false);
            Keys(Key.F);
            Assert.IsFalse(_craft.MatchingVelocity);
            _craft.ControlsLocked = true;
            _craft.ControlsLocked = false;
            Keys(Key.F);
            Assert.IsFalse(_craft.MatchingVelocity);
            Keys();
            Keys(Key.F);
            Assert.IsTrue(_craft.MatchingVelocity);
            Assert.AreEqual(_earth, _craft.MatchBody);
        }

        [Test]
        public void PauseClearsAnAlreadySampledPushBeforeTheFirstResumedPhysicsStep()
        {
            Keys(Key.W);
            Assert.Greater(_craft.ThrustInput.z, 0.9f);
            GameSession.SetPaused(true);
            Assert.AreEqual(Vector3.zero, _craft.ThrustInput, "Pause event must clear input even before another Update.");
            Keys();
            GameSession.SetPaused(false);
            _craft.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
            Assert.AreEqual(Vector3.zero, _craft.ThrustInput);
        }

        [Test]
        public void ANewlyObstructedRouteIsRefusedWithTheBlockersName()
        {
            Vec3d outward = _system.States[_earth].Position.Normalized;
            _system.Place(_craft.Gravity, -outward * 6000, Vec3d.Zero);
            Assert.AreEqual(_earth, _craft.Target, "The persistent lock must survive moving behind an occluder.");
            Assert.IsFalse(_craft.ToggleAutopilot());
            Assert.AreEqual(FlightAssistMode.Manual, _craft.AssistMode);
            StringAssert.Contains("Sun", _craft.NavigationNotice);
        }

        [Test]
        public void LostAuthorityAndMissingNavigationCannotKeepSteering()
        {
            Assert.IsTrue(_craft.ToggleAutopilot());
            _craft.SideScale = 0;
            _craft.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
            Assert.AreEqual(FlightAssistMode.Manual, _craft.AssistMode);
            Assert.AreEqual(Vector3.zero, _craft.ThrustInput);
            _craft.SideScale = 1;
            Assert.IsTrue(_craft.ToggleVelocityLock());
            _craft.System = null;
            _craft.SendMessage("Update", SendMessageOptions.RequireReceiver);
            Assert.AreEqual(-1, _craft.Target);
            Assert.AreEqual(FlightAssistMode.Manual, _craft.AssistMode);
        }

        [Test]
        public void NavigationActionsAreRebindableInTheFlightMapAndLeaveSelectUnassigned()
        {
            var actions = new[] { GameInput.LockTarget, GameInput.Autopilot, GameInput.VelocityLock, GameInput.MatchVelocity };
            foreach (InputAction action in actions)
            {
                Assert.AreSame(GameInput.Flight, action.actionMap);
                Assert.IsTrue(GameInput.GetEntries(GameInput.KeyboardMouseGroup).Exists(e => e.Action == action && e.Rebindable));
                Assert.IsTrue(GameInput.GetEntries(GameInput.GamepadGroup).Exists(e => e.Action == action && e.Rebindable));
                foreach (InputBinding binding in action.bindings) Assert.AreNotEqual("<Gamepad>/select", binding.path);
            }
            Assert.AreEqual("<Keyboard>/t", GameInput.LockTarget.bindings[0].path);
            Assert.AreEqual("<Gamepad>/rightStickPress", GameInput.LockTarget.bindings[1].path);
            Assert.AreEqual("<Gamepad>/buttonWest", GameInput.Autopilot.bindings[1].path);
            Assert.AreEqual("<Gamepad>/dpad/down", GameInput.VelocityLock.bindings[1].path);
        }
    }
}
