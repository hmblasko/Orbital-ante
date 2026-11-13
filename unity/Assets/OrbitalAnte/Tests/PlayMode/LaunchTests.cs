using System.Collections;
using NUnit.Framework;
using OrbitalAnte.Content;
using OrbitalAnte.Core;
using OrbitalAnte.Launch;
using OrbitalAnte.Planets;
using OrbitalAnte.Ship;
using OrbitalAnte.Space;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Checks Milestone 8 on real objects: the launch base stands on Earth, a run starts with the ship at rest on
    /// its pad, and holding "up" gives the build-up, the smoke and the swing of the service arm before the engines
    /// are let off the leash. How it looks, sounds and feels is on the playtest checklist instead.
    /// Run in Unity from Window > General > Test Runner > PlayMode.
    /// </summary>
    public class LaunchTests
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
            PlanetSurface.PictureWidthForTests = 64;
            _root = new GameObject("LaunchTestRoot");
            _world = _root.AddComponent<SolarSystemWorld>();
            _world.Build(4242, StartPlace.Pad);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            SolarSystem.ResetStatics();
            PlanetSurface.ResetStatics();
            ContentDatabase.ResetStatics();
            ShipMeshes.ResetStatics();
            LaunchConfig.ResetStatics();
            SkyGlobals.Reset();
        }

        static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
        }

        Vector3 ShipOnPad() => _world.Base.transform.InverseTransformPoint(_world.Ship.transform.position);

        [UnityTest]
        public IEnumerator ARunStartsWithTheShipAtRestOnThePad()
        {
            Assert.IsTrue(_world.StartedOnPad);
            Assert.IsNotNull(_world.Base);
            Assert.AreEqual(AstronautMode.Seated, _world.Astronaut.Mode);
            Vector3 at = ShipOnPad();
            Assert.Less(new Vector2(at.x, at.z).magnitude, 0.2f, "the ship is not over the middle of the pad");
            Assert.AreEqual(LaunchLayout.ShipHeightOnPad, at.y, 0.3f);

            yield return Steps(120);
            at = ShipOnPad();
            Assert.Less(new Vector2(at.x, at.z).magnitude, 0.5f, "the ship slid off its mark");
            Assert.AreEqual(LaunchLayout.ShipHeightOnPad, at.y, 0.35f, "the ship sank into the pad or was thrown off it");
            Assert.Greater(Vector3.Dot(_world.Ship.transform.up, _world.Base.transform.up), 0.995f, "the ship fell over");
            Vector3 ground = _world.System.GroundVelocityAt(_world.System.Frame.Anchor, _world.Ship.transform.position);
            Assert.Less((_world.Craft.Body.linearVelocity - ground).magnitude, 0.3f, "the ship is not at rest");
            Assert.IsTrue(_world.Engines.OnGround);
            for (int i = 0; i < _world.Ship.Model.Count; i++)
                Assert.AreEqual(PartState.Sound, _world.Ship.Model.State(i), _world.Ship.Model.Parts[i].Name + " was hurt just by standing on the pad");
        }

        [UnityTest]
        public IEnumerator TheBaseHasEveryPiece_SolidGround_AndNoRocksOnThePad()
        {
            yield return null;
            foreach (string piece in LaunchLayout.Pieces)
                Assert.IsNotNull(_world.Base.transform.Find(piece), "the base has no " + piece);
            Assert.IsNotNull(_world.Base.GetComponentInChildren<MeshCollider>());
            // The nose points north along the pad, and the pad's "up" is away from the planet's middle.
            Vector3 fromCentre = (_world.Base.transform.position - _world.Base.Surface.Body.transform.position).normalized;
            Assert.Greater(Vector3.Dot(_world.Base.transform.up, fromCentre), 0.999f);
            Assert.Greater(Vector3.Dot(_world.Ship.transform.forward, _world.Base.transform.forward), 0.99f);
            Assert.IsTrue(_world.Base.OverPad(_world.Ship.transform.position));
        }

        [UnityTest]
        public IEnumerator HoldingUp_BuildsUp_ThrowsSmoke_SwingsTheArm_ThenLetsGo()
        {
            yield return Steps(60);
            LaunchFx engines = _world.Engines;
            Assert.IsTrue(engines.Settled, "the ship is not standing still on the pad");
            Assert.AreEqual(EngineState.Off, engines.Model.State);
            Assert.AreEqual(0f, _world.Base.ArmSwing, 0.001f);
            Assert.AreEqual(1f, _world.Ship.LiftGate, 0.001f);

            engines.UpOverride = 1f;
            float pressed = Time.time;
            yield return null;
            yield return null;
            Assert.AreEqual(EngineState.Igniting, engines.Model.State, "pressing up on the ground should start the build-up");
            Assert.Less(_world.Ship.LiftGate, 0.5f, "the engines must be held back during the build-up");
            Assert.Greater(engines.Air, 0.5f, "Earth has air");

            float waited = 0f;
            while (engines.Model.State == EngineState.Igniting && waited < 4f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(EngineState.Running, engines.Model.State);
            // Frames in a batch run can be long, so this only says "about a second, not instant and not stuck".
            Assert.AreEqual(engines.Config.rules.ignitionSeconds, Time.time - pressed, 0.6f, "the build-up should take its stated time");
            Assert.AreEqual(1f, _world.Ship.LiftGate, 0.001f);
            yield return new WaitForSeconds(0.4f);
            Assert.Greater(engines.SmokeAlive, 0, "no smoke at lift-off");
            Assert.Greater(engines.DebrisAlive, 0, "no grit at lift-off");
            Assert.Greater(_world.Base.ArmSwing, 0.2f, "the service arm did not swing clear");
            Assert.Greater(SkyGlobals.FlareColor.maxColorComponent, 0f, "the engines do not light the ground");

            engines.UpOverride = 0f;
            yield return new WaitForSeconds(engines.Config.rules.fadeSeconds + 0.3f);
            Assert.AreEqual(EngineState.Off, engines.Model.State);
            Assert.AreEqual(0f, engines.Model.Burn, 0.02f);
            Assert.AreEqual(0f, SkyGlobals.FlareColor.maxColorComponent, 0.001f);
        }

        [UnityTest]
        public IEnumerator InOrbit_ThereIsNoBuildUp_AndNoSmoke()
        {
            Object.DestroyImmediate(_root);
            SolarSystem.ResetStatics();
            PlanetSurface.ResetStatics();
            _root = new GameObject("LaunchTestRoot");
            _world = _root.AddComponent<SolarSystemWorld>();
            _world.Build(4242, StartPlace.Orbit);
            yield return Steps(5);
            Assert.IsFalse(_world.StartedOnPad);
            LaunchFx engines = _world.Engines;
            engines.UpOverride = 1f;
            yield return null;
            yield return null;
            Assert.AreEqual(EngineState.Running, engines.Model.State, "in flight the engines answer at once");
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0, engines.SmokeAlive);
            Assert.AreEqual(1f, _world.Ship.LiftGate, 0.001f);
        }
    }
}
