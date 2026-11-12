using System.Collections;
using NUnit.Framework;
using OrbitalAnte.Content;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.Space;
using OrbitalAnte.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Checks Milestone 5 with the real physics engine: the solar system builds, bodies keep moving, the ground
    /// of the body you are at stands still, an orbit stays an orbit, gravity is what the data says, and the
    /// player is brought back to zero when they wander far.
    /// Run in Unity from Window > General > Test Runner > PlayMode.
    /// </summary>
    public class SolarSystemTests
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
            PlanetSurface.PictureWidthForTests = 64;
            _root = new GameObject("SolarSystemTestRoot");
            _world = _root.AddComponent<SolarSystemWorld>();
            _world.Build(4242);
        }

        [TearDown]
        public void TearDown()
        {
            // Switched off at once: Destroy only happens at the end of the frame, and the next test must not share
            // the physics world with this one's planets.
            // Removed at once, not at the end of the frame, so nothing of this world is left when the next test builds its own.
            Object.DestroyImmediate(_root);
            SolarSystem.ResetStatics();
            PlanetSurface.ResetStatics();
            ContentDatabase.ResetStatics();
        }

        static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
        }

        [Test]
        public void BuildsTheSunEightPlanetsMoonsAndABelt()
        {
            SolarSystem system = _world.System;
            Assert.AreEqual("Sun", system.Model.Spec(0).id);
            int planets = 0, moons = 0, rocks = 0;
            for (int i = 0; i < system.Model.Count; i++)
            {
                BodyKind kind = system.Model.Spec(i).kind;
                if (kind == BodyKind.Planet || kind == BodyKind.GasGiant) planets++;
                if (kind == BodyKind.Moon) moons++;
                if (kind == BodyKind.Asteroid) rocks++;
                Assert.IsNotNull(system.Bodies[i].GetComponent<SphereCollider>(), system.Model.Spec(i).id + " has nothing to land on.");
                Assert.IsNotNull(system.Bodies[i].GetComponentInChildren<MeshRenderer>(), system.Model.Spec(i).id + " cannot be seen.");
            }
            Assert.AreEqual(8, planets);
            Assert.GreaterOrEqual(moons, 10);
            Assert.Greater(rocks, 20);
            Assert.AreEqual(0, ContentDatabase.Problems.Count, string.Join("; ", ContentDatabase.Problems));
        }

        [Test]
        public void StartsInOrbitRoundEarthAtUnityZero()
        {
            SolarSystem system = _world.System;
            int earth = system.Model.IndexOf("Earth");
            Assert.AreEqual(earth, system.Frame.Anchor);
            Assert.Less(_world.Craft.transform.position.magnitude, 1f, "The player starts at Unity's zero point.");
            double altitude = system.AltitudeAbove(earth, _world.Craft.transform.position);
            Assert.AreEqual(TestCraftConfig.Load().startAltitude, altitude, 2.0);
            float expected = (float)system.Model.CircularOrbitSpeed(earth, system.Model.Spec(earth).radius + altitude);
            Assert.AreEqual(expected, _world.Craft.Body.linearVelocity.magnitude, 0.5f);
        }

        [UnityTest]
        public IEnumerator EverythingMovesExceptTheBodyYouAreAt()
        {
            SolarSystem system = _world.System;
            int earth = system.Model.IndexOf("Earth");
            int moon = system.Model.IndexOf("Moon");
            int mars = system.Model.IndexOf("Mars");
            Vector3 earthBefore = system.Bodies[earth].transform.position;
            Vector3 moonBefore = system.Bodies[moon].transform.position;
            Vector3 marsBefore = system.Bodies[mars].transform.position;
            Quaternion earthFacing = system.Bodies[earth].transform.rotation;
            double timeBefore = system.SimTime;

            yield return Steps(100);

            Assert.Greater(system.SimTime, timeBefore + 1.0);
            Assert.Less(Vector3.Distance(earthBefore, system.Bodies[earth].transform.position), 0.05f, "Earth is the anchor and should not move.");
            Assert.Greater(Vector3.Distance(moonBefore, system.Bodies[moon].transform.position), 10f, "The Moon should be going round.");
            Assert.Greater(Vector3.Distance(marsBefore, system.Bodies[mars].transform.position), 10f, "Mars should be moving.");
            Assert.Greater(Quaternion.Angle(earthFacing, system.Bodies[earth].transform.rotation), 0.2f, "Earth should be turning.");
        }

        [UnityTest]
        public IEnumerator AnOrbitStaysAnOrbit()
        {
            SolarSystem system = _world.System;
            int earth = system.Model.IndexOf("Earth");
            double start = system.AltitudeAbove(earth, _world.Craft.transform.position);
            double lowest = start, highest = start;
            for (int i = 0; i < 20; i++)
            {
                yield return Steps(25);   // ten seconds in all, a good part of the way round
                double now = system.AltitudeAbove(earth, _world.Craft.Body.position);
                if (now < lowest) lowest = now;
                if (now > highest) highest = now;
            }
            Assert.Greater(lowest, start - 25, "The craft should coast round, not fall.");
            Assert.Less(highest, start + 25, "The craft should coast round, not fly off.");
        }

        [UnityTest]
        public IEnumerator GravityNearTheGroundMatchesTheData()
        {
            SolarSystem system = _world.System;
            int earth = system.Model.IndexOf("Earth");
            // 200 m up: clear of every mountain, and still close enough that the world turns with the planet.
            double distance = system.Model.Spec(earth).radius + 200;
            Vec3d above = system.States[earth].Position + system.Model.SpinAxis(earth).AnyPerpendicular() * distance;
            system.Place(_world.Craft.Gravity, above, system.States[earth].Velocity);
            yield return Steps(3);
            Assert.IsTrue(system.Frame.Rotating, "This close to the ground the world should turn with the planet.");
            float expected = (float)(9.8 * system.Model.Spec(earth).radius * system.Model.Spec(earth).radius / (distance * distance));
            Assert.AreEqual(expected, _world.Craft.Gravity.FeltGravity.magnitude, 1.5f);
            Vector3 down = (system.Bodies[earth].transform.position - _world.Craft.transform.position).normalized;
            Assert.Greater(Vector3.Dot(_world.Craft.Gravity.FeltGravity.normalized, down), 0.98f, "Down should be toward the centre.");
        }

        // Landing on real ground is checked in PlanetTests (Milestone 6 replaced the smooth ball with terrain).

        [UnityTest]
        public IEnumerator FarFromEverythingThePlayerIsBroughtBackToZero()
        {
            SolarSystem system = _world.System;
            // Somewhere empty, well above the flat of the solar system.
            system.Place(_world.Craft.Gravity, new Vec3d(30000, 9000, 5000), new Vec3d(0, 0, 0));
            yield return Steps(3);
            Assert.AreEqual(0, system.Frame.Anchor, "Open space is anchored to the Sun.");
            Assert.Less(_world.Craft.Body.position.magnitude, SolarSystem.RecenterDistance, "The player should be near Unity's zero point.");
            Vec3d truly = system.TruePosition(_world.Craft.Body.position);
            Assert.AreEqual(0, Vec3d.Distance(truly, new Vec3d(30000, 9000, 5000)), 50.0, "Recentring must not change where the player truly is.");
        }

        [Test]
        public void SpeedIsMeasuredAgainstThePlanetYouAreAt()
        {
            SolarSystem system = _world.System;
            int earth = system.Model.IndexOf("Earth");
            // In orbit the craft has speed against Earth; Earth's own (much larger) speed round the Sun is not counted.
            float speed = _world.Craft.Body.linearVelocity.magnitude;
            Assert.Less(speed, (float)system.Model.OrbitSpeed(earth) * 0.6f);
            Assert.Greater(speed, 20f);
        }

        [Test]
        public void TheMapOpensAndLocksTheFlyingControls()
        {
            SystemMap map = _world.Map;
            Assert.IsFalse(map.IsOpen);
            map.SetOpen(true);
            Assert.IsTrue(map.IsOpen);
            Assert.IsTrue(_world.Craft.ControlsLocked);
            map.SetOpen(false);
            Assert.IsFalse(_world.Craft.ControlsLocked);
        }

        [Test]
        public void TheSameSeedPutsEveryBodyInTheSamePlace()
        {
            SolarSystemData data = SolarSystemData.Load();
            var first = new SolarSystemModel(data.BuildBodies(4242), 4242);
            var second = new SolarSystemModel(data.BuildBodies(4242), 4242);
            var different = new SolarSystemModel(data.BuildBodies(99), 99);
            BodyState[] a = first.NewStateBuffer(), b = second.NewStateBuffer(), c = different.NewStateBuffer();
            first.Evaluate(100, a);
            second.Evaluate(100, b);
            different.Evaluate(100, c);
            int mars = first.IndexOf("Mars");
            Assert.AreEqual(0, Vec3d.Distance(a[mars].Position, b[mars].Position), 1e-9);
            Assert.Greater(Vec3d.Distance(a[mars].Position, c[different.IndexOf("Mars")].Position), 10);
        }

        [Test]
        public void FlightControlsAreRebindableAndSeparateFromOnFootControls()
        {
            GameInput.SetFlying(true);
            var flying = GameInput.GetEntries(GameInput.KeyboardMouseGroup);
            Assert.IsTrue(flying.Exists(e => e.Action == GameInput.MatchVelocity));
            Assert.IsFalse(flying.Exists(e => e.Action == GameInput.Reload));
            GameInput.SetFlying(false);
            var onFoot = GameInput.GetEntries(GameInput.KeyboardMouseGroup);
            Assert.IsTrue(onFoot.Exists(e => e.Action == GameInput.Reload));
            Assert.IsFalse(onFoot.Exists(e => e.Action == GameInput.MatchVelocity));
        }
    }
}
