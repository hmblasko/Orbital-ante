using System.Collections;
using NUnit.Framework;
using OrbitalAnte.Cockpit;
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
    /// Checks Milestone 9 on real objects: the dash is there and showing words from the seat, each design can be
    /// drawn, the three ship cameras exist and take over the view when asked, and a dry tank weakens the engines.
    /// How it looks and reads is on the playtest checklist instead.
    /// Run in Unity from Window > General > Test Runner > PlayMode.
    /// </summary>
    public class CockpitTests
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
            PlanetSurface.PictureWidthForTests = 64;
            _root = new GameObject("CockpitTestRoot");
            _world = _root.AddComponent<SolarSystemWorld>();
            _world.Build(4242, StartPlace.Orbit);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
            PlanetSurface.PictureWidthForTests = 0;
            OrbitalAnte.Core.TimeFlow.Reset();
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator FromTheSeat_TheDashIsShown_WithWordsOnIt()
        {
            yield return Frames(8);
            CockpitDash dash = _world.Dash;
            Assert.IsNotNull(dash, "the world built no dash");
            Assert.IsTrue(dash.Shown, "the dash is hidden from the pilot's seat");
            Assert.Greater(dash.Drawing.VertexCount, 300);
            Assert.Greater(dash.WordsShown, 6);
            Assert.AreEqual(1f, dash.Supplies.Fuel, 0.01f);
            Assert.AreEqual(1f, _world.Ship.FuelGate, 1e-4f);
        }

        [UnityTest]
        public IEnumerator ThereAreThreeShipCameras_OutsideTheHull_AndOnlyOneRunsAtATime()
        {
            yield return Frames(8);
            CockpitDash dash = _world.Dash;
            Assert.AreEqual(3, dash.Cameras.Length);
            int running = 0;
            foreach (Camera cam in dash.Cameras)
            {
                Assert.IsNotNull(cam);
                Assert.AreSame(_world.Ship.transform, cam.transform.parent, "a ship camera must ride on the ship");
                Assert.IsFalse(ShipLayout.InsideCabin(new P3(cam.transform.localPosition.x, cam.transform.localPosition.y, cam.transform.localPosition.z)));
                if (cam.enabled) running++;
            }
            Assert.LessOrEqual(running, 1, "more than one ship camera is drawing at once");
            Assert.IsTrue(_world.Astronaut.Eye.enabled, "your own eyes should be the view until you look down");
            Assert.IsFalse(CockpitDash.WatchingScreen);
        }

        [UnityTest]
        public IEnumerator GlancingDown_NeverChangesWhatTheShipIsAimedAt()
        {
            yield return Frames(8);
            TestCraft craft = _world.Craft;
            // Aim the nose at a body that is in plain view from here (which ones are depends on the seed).
            int moon = -1;
            foreach (string id in new[] { "Moon", "Mars", "Venus", "Jupiter", "Mercury", "Saturn" })
            {
                int body = -1;
                for (int i = 0; i < craft.System.Bodies.Length; i++)
                {
                    if (craft.System.Bodies[i].Spec.id == id) body = i;
                }
                if (body < 0) continue;
                Vector3 to = craft.System.Bodies[body].transform.position - craft.Body.position;
                craft.Body.rotation = Quaternion.LookRotation(to.normalized, craft.Body.rotation * Vector3.up);
                yield return new WaitForFixedUpdate();
                yield return Frames(3);
                if (craft.Target == body) { moon = body; break; }
            }
            Assert.GreaterOrEqual(moon, 0, "no body could be brought under the nose");

            // The pilot's head tips right down (the camera-screen glance, a launch shake, a knock): same target.
            _world.Astronaut.ScreenGlance = 60f;
            yield return Frames(3);
            Assert.Greater(Vector3.Angle(_world.Astronaut.Eye.transform.forward, craft.transform.forward), 30f, "the test did not move the eyes");
            Assert.AreEqual(moon, craft.Target, "looking down changed what Match Velocity would match");
            _world.Astronaut.ScreenGlance = 0f;
        }

        [UnityTest]
        public IEnumerator ADryTank_WeakensThePush_ButNeverRemovesIt()
        {
            yield return Frames(4);
            CockpitDash dash = _world.Dash;
            dash.Model.TextInterval = 0f;
            dash.Supplies.Set(0f, 1f);
            yield return Frames(3);
            float reserve = dash.Config.supplies.reservePush;
            Assert.AreEqual(reserve, _world.Ship.FuelGate, 1e-4f);
            Assert.AreEqual(reserve, _world.Craft.PushScale, 1e-4f, "a dry tank should leave only the reserve push");
            Assert.Greater(_world.Craft.SideScale, reserve + 0.1f, "a dry tank must never take your steering");
            StringAssert.Contains("FUEL OUT", dash.Model.Readings.Notice);
        }
    }
}
