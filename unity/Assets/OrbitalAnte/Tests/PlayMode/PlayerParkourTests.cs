using System.Collections;
using System.Reflection;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitalAnte.Tests.PlayMode
{
    /// <summary>Engine regression drafts. Reference compilation does not execute Unity physics.</summary>
    public sealed class PlayerParkourTests
    {
        GameObject _root;
        PlayerConfig _config;
        ParkourMotor _motor;

        [SetUp]
        public void SetUp()
        {
            GameSession.SetPaused(false);
            _root = new GameObject("ParkourTestRoot");
            _config = ScriptableObject.CreateInstance<PlayerConfig>();
            _motor = new ParkourMotor();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_config);
        }

        GameObject Solid(Vector3 position, Vector3 scale)
        {
            var solid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            solid.transform.SetParent(_root.transform, false);
            solid.transform.position = position;
            solid.transform.localScale = scale;
            return solid;
        }

        GameObject Ledge() => Solid(new Vector3(0f, 0.5f, 1f), new Vector3(3f, 1f, 1f));

        bool StartClimb(Vector3 feet) => _motor.TryBeginClimb(feet, Vector3.up, Vector3.forward,
            1.8f, 0.3f, null, new Vector3(0f, -1f, 4f), _config);

        [Test]
        public void InitialOverlapRefusesEvenAClearDestination()
        {
            Solid(Vector3.up, new Vector3(0.1f, 1f, 1f));
            Physics.SyncTransforms();
            Assert.IsFalse(_motor.CanSweep(Vector3.zero, Vector3.right * 2f, Vector3.up, 1.8f, 0.3f, null));
        }

        [TestCase(0f)]
        [TestCase(0.08f)]
        public void CapsuleCannotCrossAThinBlockerBetweenClearEndpoints(float skinWidth)
        {
            Solid(Vector3.up, new Vector3(0.04f, 2f, 2f));
            Physics.SyncTransforms();
            Assert.IsTrue(_motor.CanOccupy(Vector3.left, Vector3.up, 1.8f, 0.3f, null));
            Assert.IsTrue(_motor.CanOccupy(Vector3.right, Vector3.up, 1.8f, 0.3f, null));
            Assert.IsFalse(_motor.CanSweep(Vector3.left, Vector3.right, Vector3.up, 1.8f, 0.3f, null, skinWidth));
        }

        CharacterController Controller(Vector3 feet)
        {
            var player = new GameObject("Player");
            player.transform.SetParent(_root.transform, false);
            player.transform.position = feet;
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.center = Vector3.up * 0.9f;
            controller.skinWidth = 0.08f;
            controller.stepOffset = 0f;
            controller.minMoveDistance = 0f;
            return controller;
        }

        [Test]
        public void MovingARealControllerIntoALedgeStillAllowsItsInitialClimbSweep()
        {
            Ledge();
            CharacterController controller = Controller(new Vector3(0f, 0.2f, -1f));
            Physics.SyncTransforms();
            CollisionFlags contact = controller.Move(Vector3.forward * 2f);
            Assert.IsTrue((contact & CollisionFlags.Sides) != 0, "exercise actual CharacterController wall contact");
            Vector3 feet = controller.transform.position;
            Assert.IsTrue(_motor.TryBeginClimb(feet, Vector3.up, Vector3.forward, controller.height,
                controller.radius, controller.transform, Vector3.forward * 4f, _config, controller.skinWidth));
            Assert.IsTrue(_motor.TryClimbStep(feet, Vector3.up, controller.height, controller.radius,
                controller.transform, _config, 0.02f, out Vector3 step, controller.skinWidth));
            controller.Move(step);
            Assert.Greater(controller.transform.position.y, feet.y);
        }

        [Test]
        public void StandingAtRealControllerWallContactStillChecksTheFullNewHeadVolume()
        {
            GameInput.Initialize();
            Solid(new Vector3(0f, 1.5f, 1f), new Vector3(3f, 3f, 1f));
            CharacterController capsule = Controller(new Vector3(0f, 0.2f, -1f));
            var eye = new GameObject("Eye");
            eye.transform.SetParent(capsule.transform, false);
            eye.transform.localPosition = Vector3.up * 1.62f;
            FpsController player = capsule.gameObject.AddComponent<FpsController>();
            player.Initialize(_config, eye.AddComponent<Camera>());
            capsule.height = 1.05f;
            capsule.center = Vector3.up * 0.525f;
            Physics.SyncTransforms();
            Assert.IsTrue((capsule.Move(Vector3.forward * 2f) & CollisionFlags.Sides) != 0);
            Vector3 feet = capsule.transform.position;
            GameObject beam = Solid(feet + new Vector3(0.16f, 1.5f, 0f), new Vector3(0.15f, 0.15f, 0.4f));
            Physics.SyncTransforms();
            MethodInfo stance = typeof(FpsController).GetMethod("UpdateStance", BindingFlags.Instance | BindingFlags.NonPublic);
            stance.Invoke(player, new object[] { 1f });
            Assert.IsTrue(player.Crouched, "the new shoulder/head volume remains blocked");
            Object.DestroyImmediate(beam);
            Physics.SyncTransforms();
            stance.Invoke(player, new object[] { 1f });
            Assert.IsFalse(player.Crouched, "a legal contact at the side must not prevent standing");
            Assert.AreEqual(feet, capsule.transform.position);
            Assert.AreEqual(0f, capsule.center.y - capsule.height * 0.5f, 0.0001f);
        }

        [Test]
        public void CharacterBodiesBlockSweepsButAreNeverWallRunSupports()
        {
            GameObject character = Solid(new Vector3(0.6f, 1.5f, 0f), new Vector3(0.2f, 3f, 6f));
            character.layer = Layers.CharacterBody;
            Physics.SyncTransforms();
            Assert.IsFalse(_motor.CanSweep(Vector3.zero, Vector3.right, Vector3.up, 1.8f, 0.3f, null, 0.08f));
            Vector3 velocity = Vector3.forward * 5f;
            Assert.IsFalse(WallStep(true, false, ref velocity));
        }

        [Test]
        public void FullCapsuleSweepUsesGravityRelativeUp()
        {
            Solid(new Vector3(1.4f, 0f, 0.5f), new Vector3(0.3f, 0.3f, 0.04f));
            Physics.SyncTransforms();
            Assert.IsFalse(_motor.CanSweep(Vector3.zero, Vector3.forward, Vector3.right, 1.8f, 0.3f, null));
        }

        [Test]
        public void ShoulderObstructionAllowsCrouchButRefusesStandingDestination()
        {
            Solid(new Vector3(0.2f, 1.5f, 0f), new Vector3(0.15f, 0.15f, 1f));
            Physics.SyncTransforms();
            Assert.IsTrue(_motor.CanOccupy(Vector3.zero, Vector3.up, 1.05f, 0.3f, null));
            Assert.IsFalse(_motor.CanOccupy(Vector3.zero, Vector3.up, 1.8f, 0.3f, null));
        }

        [Test]
        public void SaturatedOverlapBufferBlocksEvenWhenCollectedCollidersBelongToOwner()
        {
            for (int i = 0; i < 40; i++) Solid(Vector3.up, Vector3.one * 0.05f);
            Physics.SyncTransforms();
            Assert.IsFalse(_motor.CanOccupy(Vector3.zero, Vector3.up, 1.8f, 0.3f, _root.transform));
        }

        [Test]
        public void SaturatedCastBufferCannotHideAnUncollectedBlocker()
        {
            for (int i = 0; i < 40; i++) Solid(Vector3.up, new Vector3(0.02f, 2f, 1f));
            Physics.SyncTransforms();
            Assert.IsTrue(_motor.CanOccupy(Vector3.left * 2f, Vector3.up, 1.8f, 0.3f, _root.transform));
            Assert.IsTrue(_motor.CanOccupy(Vector3.right * 2f, Vector3.up, 1.8f, 0.3f, _root.transform));
            Assert.IsFalse(_motor.CanSweep(Vector3.left * 2f, Vector3.right * 2f, Vector3.up, 1.8f, 0.3f, _root.transform));
        }

        [Test]
        public void AClimbRefusesACeilingAcrossItsLiftPath()
        {
            Ledge();
            Solid(new Vector3(0f, 2.3f, 0f), new Vector3(1f, 0.1f, 0.5f));
            Physics.SyncTransforms();
            Assert.IsFalse(StartClimb(Vector3.up * 0.2f));
        }

        [Test]
        public void ABlockerEnteringTheNextStepAbortsWithoutRefundingTheClimb()
        {
            Ledge();
            Physics.SyncTransforms();
            Vector3 feet = Vector3.up * 0.2f;
            Assert.IsTrue(StartClimb(feet));
            Assert.IsTrue(_motor.TryClimbStep(feet, Vector3.up, 1.8f, 0.3f, null, _config, 0.05f, out Vector3 first));
            feet += first;
            GameObject beam = Solid(new Vector3(0f, 2.25f, 0f), new Vector3(1f, 0.08f, 0.6f));
            Physics.SyncTransforms();
            Assert.IsFalse(_motor.TryClimbStep(feet, Vector3.up, 1.8f, 0.3f, null, _config, 0.05f, out Vector3 blocked));
            Assert.AreEqual(Vector3.zero, blocked);
            Assert.IsFalse(_motor.Climbing);
            Object.DestroyImmediate(beam);
            Physics.SyncTransforms();
            Assert.IsFalse(StartClimb(feet), "a blocked attempt cannot repeatedly pump the player upward");
        }

        [Test]
        public void ANewDestinationBlockerCancelsBeforeTheNextLiftStep()
        {
            Ledge();
            Physics.SyncTransforms();
            Vector3 feet = Vector3.up * 0.2f;
            Assert.IsTrue(StartClimb(feet));
            Solid(new Vector3(0f, 2f, 0.8f), new Vector3(0.2f, 0.2f, 0.2f));
            Physics.SyncTransforms();
            Assert.IsFalse(_motor.TryClimbStep(feet, Vector3.up, 1.8f, 0.3f, null, _config, 0.02f, out _));
        }

        [Test]
        public void StoredSupportCoordinatesSurviveAFloatingOriginShift()
        {
            GameObject ledge = Ledge();
            Physics.SyncTransforms();
            Vector3 feet = Vector3.up * 0.2f;
            Assert.IsTrue(StartClimb(feet));
            Vector3 shift = new Vector3(1000f, -300f, 700f);
            ledge.transform.position += shift;
            feet += shift;
            Physics.SyncTransforms();
            Assert.IsTrue(_motor.TryClimbStep(feet, Vector3.up, 1.8f, 0.3f, null, _config, 0.02f, out Vector3 step));
            Assert.AreEqual(0f, step.x, 0.0001f);
            Assert.AreEqual(0f, step.z, 0.0001f);
            Assert.Greater(step.y, 0f);
        }

        [Test]
        public void ClimbStepAndExitKeepTheMovingSupportsInertia()
        {
            GameObject ledge = Ledge();
            Rigidbody support = ledge.AddComponent<Rigidbody>();
            support.useGravity = false;
            support.mass = 1000f;
            support.linearVelocity = Vector3.right * 20f + Vector3.up * 4f;
            Physics.SyncTransforms();
            Vector3 feet = Vector3.up * 0.2f;
            Assert.IsTrue(_motor.TryBeginClimb(feet, Vector3.up, Vector3.forward, 1.8f, 0.3f,
                null, support.linearVelocity + Vector3.forward * 3f, _config));
            Assert.IsTrue(_motor.TryClimbStep(feet, Vector3.up, 1.8f, 0.3f, null, _config, 0.02f, out Vector3 step));
            Assert.AreEqual(0.4f, step.x, 0.0001f, "climbing is measured relative to the moving support");
            Assert.AreEqual(20f, _motor.ClimbExitVelocity.x, 0.0001f);
            Assert.AreEqual(4f, _motor.ClimbExitVelocity.y, 0.0001f, "the support's upward inertia is not climb thrust");
            Assert.AreEqual(3f, _motor.ClimbExitVelocity.z, 0.0001f);
        }

        [TestCase(20f)]
        [TestCase(-20f)]
        public void MovingSupportCannotBlockItsOwnCarry_ButOtherSolidsStillDo(float speed)
        {
            Rigidbody support = Ledge().AddComponent<Rigidbody>();
            support.useGravity = false;
            support.mass = 1000f;
            support.linearVelocity = Vector3.forward * speed;
            Physics.SyncTransforms();
            Vector3 feet = Vector3.up * 0.2f;
            Assert.IsTrue(StartClimb(feet));
            Assert.IsTrue(_motor.TryClimbStep(feet, Vector3.up, 1.8f, 0.3f, null, _config, 0.02f, out Vector3 step));
            Assert.AreEqual(speed * 0.02f, step.z, 0.0001f);
            Solid(new Vector3(0f, 1f, Mathf.Sign(speed) * 0.35f), new Vector3(1f, 2f, 0.04f));
            Physics.SyncTransforms();
            Assert.IsFalse(_motor.TryClimbStep(feet, Vector3.up, 1.8f, 0.3f, null, _config, 0.02f, out _),
                "support carry must retain a continuous world sweep against other solids");
        }

        [Test]
        public void RemovingOnlyTheSupportingBodyDoesNotReinterpretItsAnchorsAsColliderLocal()
        {
            Rigidbody support = Ledge().AddComponent<Rigidbody>();
            support.useGravity = false;
            support.mass = 1000f;
            Physics.SyncTransforms();
            Vector3 feet = Vector3.up * 0.2f;
            Assert.IsTrue(StartClimb(feet));
            Object.DestroyImmediate(support);
            Assert.IsFalse(_motor.TryClimbStep(feet, Vector3.up, 1.8f, 0.3f, null, _config, 0.02f, out _));
            Assert.IsFalse(_motor.Climbing);
        }

        [UnityTest]
        public IEnumerator InterpolatedChildSupportUsesItsPhysicsPoseAcrossRenderFrames()
        {
            var ship = new GameObject("InterpolatedShip");
            ship.transform.SetParent(_root.transform, false);
            ship.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
            GameObject ledge = Ledge();
            ledge.transform.SetParent(ship.transform, false);
            ledge.transform.localPosition = new Vector3(0f, 0.5f, 1f);
            Rigidbody support = ship.AddComponent<Rigidbody>();
            support.useGravity = false;
            support.mass = 1000f;
            support.interpolation = RigidbodyInterpolation.Interpolate;
            support.linearVelocity = Vector3.right * 20f;
            Physics.SyncTransforms();

            // Unity owns interpolation. Observe a real physics/render disagreement before acquiring,
            // then wait for that disagreement to change so a render-local anchor cannot pass by chance.
            for (int i = 0; i < 10 && (support.position - ship.transform.position).sqrMagnitude < 0.0001f; i++)
                yield return new WaitForFixedUpdate();
            Vector3 initialGap = support.position - ship.transform.position;
            Assert.Greater(initialGap.sqrMagnitude, 0.0001f, "the case must exercise an interpolated render pose");
            Vector3 feet = support.position + Vector3.up * 0.2f;
            Vector3 forward = support.rotation * Vector3.forward;
            Assert.IsTrue(_motor.TryBeginClimb(feet, Vector3.up, forward, 1.8f, 0.3f, null,
                support.GetPointVelocity(feet) + forward * 3f, _config));

            // Stopping forces the interpolation lag to disappear, even when render and fixed rates
            // happen to match; this cannot rely on frame-time jitter to expose a render-local anchor.
            support.linearVelocity = Vector3.zero;

            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
                if ((support.position - ship.transform.position - initialGap).sqrMagnitude > 0.0001f) break;
            }
            Assert.Greater((support.position - ship.transform.position - initialGap).sqrMagnitude, 0.0001f);
            feet = support.position + Vector3.up * 0.2f;
            const float dt = 0.02f;
            Assert.IsTrue(_motor.TryClimbStep(feet, Vector3.up, 1.8f, 0.3f, null, _config, dt, out Vector3 step));
            Vector3 expected = support.GetPointVelocity(feet) * dt + Vector3.up * (_config.climbSpeed * dt);
            Assert.Less(Vector3.Distance(expected, step), 0.0001f, "render interpolation must not steer the climb");
            Assert.Less(Vector3.Distance(support.linearVelocity + forward * 3f, _motor.ClimbExitVelocity), 0.0001f);

            Vector3 shift = new Vector3(1000f, -300f, 700f);
            support.position += shift;
            feet += shift;
            Assert.IsTrue(_motor.TryClimbStep(feet, Vector3.up, 1.8f, 0.3f, null, _config, dt, out Vector3 shiftedStep));
            Assert.Less(Vector3.Distance(expected, shiftedStep), 0.001f, "physics-local anchors survive an origin shift");
        }

        [Test]
        public void WallRunKeepsAlongWallMomentum_AndReleaseCannotRefreshIt()
        {
            Solid(new Vector3(0.6f, 1.5f, 0f), new Vector3(0.2f, 3f, 6f));
            Physics.SyncTransforms();
            Vector3 velocity = new Vector3(0f, -4f, 5f);
            Assert.IsTrue(WallStep(true, false, ref velocity));
            Assert.AreEqual(5f, velocity.z, 0.0001f);
            Assert.AreEqual(-_config.wallFallSpeed, velocity.y, 0.0001f);
            Assert.IsFalse(WallStep(false, false, ref velocity));
            Assert.IsFalse(WallStep(true, false, ref velocity));
        }

        [Test]
        public void AFreshJumpLeavesTheWallOnce_WithoutAnAirborneRegrab()
        {
            Solid(new Vector3(0.6f, 1.5f, 0f), new Vector3(0.2f, 3f, 6f));
            Physics.SyncTransforms();
            Vector3 velocity = Vector3.forward * 5f;
            Assert.IsTrue(WallStep(true, false, ref velocity));
            Assert.IsTrue(WallStep(true, true, ref velocity));
            Assert.IsFalse(_motor.WallRunning);
            Assert.Less(velocity.x, 0f);
            Assert.AreEqual(_config.wallJumpUpSpeed, velocity.y, 0.0001f);
            Assert.IsFalse(WallStep(true, false, ref velocity));
        }

        bool WallStep(bool sprint, bool jump, ref Vector3 velocity) => _motor.TryWallRun(Vector3.zero,
            Vector3.up, Vector3.forward, 1.8f, 0.3f, null, _config, true, false, sprint, 1f, false,
            jump, 22f, 0.02f, ref velocity);
    }
}
