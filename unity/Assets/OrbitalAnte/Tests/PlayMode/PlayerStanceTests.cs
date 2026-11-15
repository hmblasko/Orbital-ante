using System.Collections;
using System.Reflection;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitalAnte.Tests.PlayMode
{
    /// <summary>Engine regression drafts: require the Unity editor; reference compilation does not execute physics.</summary>
    public sealed class PlayerStanceTests
    {
        GameObject _root;
        PlayerConfig _config;

        [SetUp]
        public void SetUp()
        {
            GameInput.Initialize();
            GameSession.SetPaused(false);
            _root = new GameObject("StanceTestRoot");
            _config = ScriptableObject.CreateInstance<PlayerConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        GameObject Solid(Vector3 position, Vector3 scale)
        {
            var solid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            solid.transform.SetParent(_root.transform, false);
            solid.transform.position = position;
            solid.transform.localScale = scale;
            return solid;
        }

        [Test]
        public void FullCapsuleExpansionSeesAnOffsetBeam_WithGravityRelativeUp()
        {
            // Boots at the origin, up toward +X: this catches a shoulder obstruction that an eye ray misses.
            Solid(new Vector3(1.55f, 0.2f, 0f), new Vector3(0.2f, 0.2f, 1f));
            Physics.SyncTransforms();
            var clearance = new CameraClearance();
            Assert.IsTrue(clearance.CanOccupy(Vector3.zero, Vector3.right, 1.1f, 0.3f, _root.transform.Find("NoPlayer")));
            Assert.IsFalse(clearance.CanOccupy(Vector3.zero, Vector3.right, 1.8f, 0.3f, null));
        }

        [Test]
        public void LeanSweepKeepsTheWholeNearPlaneOnTheNearSideOfAWall()
        {
            Solid(new Vector3(0.7f, 1f, 0f), new Vector3(0.1f, 3f, 3f));
            var eyeGo = new GameObject("Eye");
            eyeGo.transform.SetParent(_root.transform, false);
            Camera eye = eyeGo.AddComponent<Camera>();
            eye.nearClipPlane = 0.12f;
            eye.fieldOfView = 80f;
            eye.aspect = 1.6f;
            Physics.SyncTransforms();
            float radius = CameraClearance.Radius(eye, 0.14f);
            Vector3 clipped = new CameraClearance().Clip(Vector3.up, new Vector3(1.2f, 1f, 0f), radius, null);
            Assert.LessOrEqual(clipped.x + radius, 0.65f);
            Assert.Greater(clipped.x, 0f);
        }

        [Test]
        public void AnInitiallyOverlappingCameraCannotSweepOutThroughTheBackOfAWall()
        {
            Solid(Vector3.up, new Vector3(0.2f, 2f, 2f));
            Physics.SyncTransforms();
            Vector3 from = Vector3.up;
            Assert.AreEqual(from, new CameraClearance().Clip(from, from + Vector3.right, 0.2f, null));
        }

        [UnityTest]
        public IEnumerator FrozenArenaControllerLeavesTheDeathSlumpPositionAlone()
        {
            var player = new GameObject("Player");
            player.transform.SetParent(_root.transform, false);
            player.AddComponent<CharacterController>();
            var eyeGo = new GameObject("Eye");
            eyeGo.transform.SetParent(player.transform, false);
            eyeGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            var controller = player.AddComponent<FpsController>();
            controller.Initialize(_config, eyeGo.AddComponent<Camera>());
            controller.Frozen = true;
            eyeGo.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            yield return null;
            Assert.AreEqual(0.4f, eyeGo.transform.localPosition.y, 0.0001f);
        }

        [Test]
        public void ReleasingUnderABeamKeepsTheBootsFixed_AndRetriesStandingWhenClear()
        {
            var player = new GameObject("Player");
            player.transform.SetParent(_root.transform, false);
            CharacterController capsule = player.AddComponent<CharacterController>();
            capsule.height = 1.8f; capsule.radius = 0.3f; capsule.center = Vector3.up * 0.9f;
            var eyeGo = new GameObject("Eye");
            eyeGo.transform.SetParent(player.transform, false);
            eyeGo.transform.localPosition = Vector3.up * 1.62f;
            var controller = player.AddComponent<FpsController>();
            controller.Initialize(_config, eyeGo.AddComponent<Camera>());
            capsule.height = 1.15f; capsule.center = Vector3.up * 0.575f;
            GameObject beam = Solid(new Vector3(0.2f, 1.5f, 0f), new Vector3(0.2f, 0.2f, 1f));
            Physics.SyncTransforms();
            MethodInfo stance = typeof(FpsController).GetMethod("UpdateStance", BindingFlags.Instance | BindingFlags.NonPublic);
            stance.Invoke(controller, new object[] { 1f });
            Assert.IsTrue(controller.Crouched);
            Assert.AreEqual(0f, capsule.center.y - capsule.height * 0.5f, 0.0001f);
            Object.DestroyImmediate(beam);
            Physics.SyncTransforms();
            stance.Invoke(controller, new object[] { 1f });
            Assert.IsFalse(controller.Crouched);
            Assert.AreEqual(0f, capsule.center.y - capsule.height * 0.5f, 0.0001f);
        }
    }
}
