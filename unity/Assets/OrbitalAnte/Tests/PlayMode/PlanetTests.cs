using System.Collections;
using NUnit.Framework;
using OrbitalAnte.Content;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.Space;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Checks Milestone 6 with real Unity objects and the real physics engine: every body gets ground, the
    /// ground gets finer and solid as you come down, the craft lands and rests on it, nothing falls through,
    /// a gas giant's clouds close and open, Neptune's sea slows you, storms reach you, and leaving a body
    /// puts its detail away again.
    /// Run in Unity from Window > General > Test Runner > PlayMode.
    /// </summary>
    public class PlanetTests
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
            _root = new GameObject("PlanetTestRoot");
            _world = _root.AddComponent<SolarSystemWorld>();
            _world.Build(4242);
        }

        [TearDown]
        public void TearDown()
        {
            // Removed at once, not at the end of the frame, so nothing of this world is left when the next test builds its own.
            Object.DestroyImmediate(_root);
            SolarSystem.ResetStatics();
            PlanetSurface.ResetStatics();
            PlanetJobs.ResetStatics();
            ContentDatabase.ResetStatics();
        }

        static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
        }

        /// <summary>Lets the background work finish and the ground catch up with where the craft is.</summary>
        static IEnumerator Settle(int frames = 10)
        {
            for (int i = 0; i < frames; i++)
            {
                PlanetJobs.FinishAll();
                yield return null;
            }
        }

        PlanetSurface SurfaceOf(string id) => _world.Planets.SurfaceOf(_world.System.Model.IndexOf(id));

        /// <summary>Puts the craft above a spot on a body, moving with the ground there (so it is standing still).</summary>
        void PlaceAbove(string id, Vec3d directionOnBody, double heightAboveGround)
        {
            SolarSystem system = _world.System;
            int body = system.Model.IndexOf(id);
            PlanetSurface surface = _world.Planets.SurfaceOf(body);
            double radius = surface.GroundRadius(directionOnBody) + heightAboveGround;
            Vec3d offset = system.Model.Orientation(body, system.States[body]).Rotate(directionOnBody) * radius;
            Vec3d spin = system.Model.SpinAxis(body) * system.Model.SpinRate(body);
            system.Place(_world.Craft.Gravity, system.States[body].Position + offset, system.States[body].Velocity + Vec3d.Cross(spin, offset));
            system.Focus = _world.Craft.Gravity;
            system.SnapToFocus();
        }

        /// <summary>A gently sloping, dry spot with no cave mouth nearby: somewhere a ball will stay put.</summary>
        static Vec3d GentleSpot(PlanetSurface surface)
        {
            var r = new SeededRandom(5);
            PlanetTerrain t = surface.Terrain;
            for (int i = 0; i < 4000; i++)
            {
                Vec3d d = new Vec3d(r.NextDouble() * 2 - 1, r.NextDouble() * 2 - 1, r.NextDouble() * 2 - 1);
                if (d.SqrMagnitude < 0.01) continue;
                d = d.Normalized;
                if (t.HasSea && t.Height(d) < t.SeaLevel + 2) continue;
                if (t.InHole(d, 30)) continue;
                if (t.SlopeAt(d, 3) < 0.06 && t.SlopeAt(d, 12) < 0.08) return d;
            }
            Assert.Fail("Found nowhere gentle to land on " + surface.Body.Spec.id);
            return Vec3d.Up;
        }

        [UnityTest]
        public IEnumerator EveryPlanetAndMoonGetsGroundAndAPicture()
        {
            yield return Settle(3);
            SolarSystem system = _world.System;
            int withGround = 0;
            for (int i = 0; i < system.Model.Count; i++)
            {
                BodyKind kind = system.Model.Spec(i).kind;
                PlanetSurface s = _world.Planets.SurfaceOf(i);
                if (kind == BodyKind.Star || kind == BodyKind.Asteroid)
                {
                    Assert.IsNull(s);
                    continue;
                }
                Assert.IsNotNull(s, system.Model.Spec(i).id + " has no ground.");
                Assert.IsTrue(s.Ready, system.Model.Spec(i).id + " never finished working out its ground.");
                Assert.IsTrue(s.Interior || s.PictureReady, system.Model.Spec(i).id + " never got its far-away picture.");
                withGround++;
            }
            Assert.AreEqual(19, withGround);
            Assert.AreEqual(0, ContentDatabase.Problems.Count, string.Join("; ", ContentDatabase.Problems));
        }

        [UnityTest]
        public IEnumerator TheRunStartsWithRealGroundBelow()
        {
            PlanetSurface earth = SurfaceOf("Earth");
            Assert.IsTrue(earth.Ready, "The starting planet's ground must be there from the first frame.");
            Assert.IsTrue(earth.Near);
            Assert.IsTrue(earth.GroundShown, "Real ground should be showing at once, not a smooth ball.");
            Assert.GreaterOrEqual(earth.ShownTiles, 6);
            Assert.IsFalse(earth.Body.FarLook.enabled, "The smooth ball should be hidden while real ground shows.");
            yield return Settle(4);
            Assert.Greater(earth.ShownTiles, 6, "From 450 m up, the ground below should already be in finer tiles.");
        }

        [UnityTest]
        public IEnumerator TheCraftLandsAndRestsOnRealGround()
        {
            yield return Settle(3);
            PlanetSurface moon = SurfaceOf("Moon");
            PlaceAbove("Moon", GentleSpot(moon), 6);
            yield return Settle(12);
            Assert.IsTrue(moon.GroundShown);
            Assert.GreaterOrEqual(moon.SolidTiles, 1, "The ground under the craft should be solid.");
            yield return Steps(500);   // ten seconds: fall a few metres in weak gravity, bounce, settle
            GroundReading ground = _world.Planets.Read(moon.Body.Index, _world.Craft.Body.position);
            Assert.AreEqual(1.6, ground.AboveGround, 0.6, "Resting on its hull (a ball 1.6 m in radius) on the real ground.");
            Vector3 drift = _world.Craft.Body.linearVelocity - _world.System.GroundVelocityAt(moon.Body.Index, _world.Craft.Body.position);
            Assert.Less(drift.magnitude, 0.4f, "A landed craft should sit still on the ground.");
            Assert.AreEqual(0, _world.Planets.Rescues, "It should have landed on solid ground, not been rescued by the guard.");
        }

        [UnityTest]
        public IEnumerator TheGroundGetsFinerAsYouComeDown()
        {
            yield return Settle(3);
            PlanetSurface mars = SurfaceOf("Mars");
            Vec3d spot = GentleSpot(mars);
            PlaceAbove("Mars", spot, 900);
            _world.Craft.Body.isKinematic = true;   // hold it still while we look
            yield return Settle(10);
            int high = mars.ShownTiles;
            Assert.IsTrue(mars.GroundShown);
            Assert.AreEqual(0, mars.SolidTiles, "Nothing needs to be solid from 900 m up.");

            PlaceAbove("Mars", spot, 8);
            yield return Settle(14);
            Assert.Greater(mars.ShownTiles, high, "Close to the ground there should be more, finer tiles.");
            Assert.GreaterOrEqual(mars.SolidTiles, 1);
            Assert.Less(mars.ShownTiles, 400, "The ground must not split without limit.");
            Assert.LessOrEqual(mars.Features.Count, _world.Planets.Settings.featureCap);
            Assert.Greater(mars.Features.Count, 0, "Mars should have rocks lying about near where you land.");
        }

        [UnityTest]
        public IEnumerator NothingFallsThroughTheWorld()
        {
            yield return Settle(3);
            PlanetSurface moon = SurfaceOf("Moon");
            Vec3d spot = GentleSpot(moon);
            PlaceAbove("Moon", spot, 6);
            yield return Settle(10);
            // Put the craft well under the ground, as if it had arrived before the ground was solid.
            PlaceAbove("Moon", spot, -15);
            yield return Steps(5);
            GroundReading ground = _world.Planets.Read(moon.Body.Index, _world.Craft.Body.position);
            Assert.Greater(ground.AboveGround, 0, "The guard should have put the craft back above the ground.");
            Assert.Greater(_world.Planets.Rescues, 0);
        }

        [UnityTest]
        public IEnumerator AGasGiantsCloudsCloseRoundYouAndOpenUnderneath()
        {
            yield return Settle(3);
            PlanetSurface jupiter = SurfaceOf("Jupiter");
            Assert.IsTrue(jupiter.Interior);
            Assert.Less(jupiter.Body.Ball.radius, (float)jupiter.BodyRadius * 0.5f, "The cloud tops must not be solid.");
            _world.Craft.Body.isKinematic = true;
            Vec3d spot = GentleSpot(jupiter);
            double top = jupiter.BodyRadius - jupiter.SurfaceRadius;

            PlaceAbove("Jupiter", spot, top + jupiter.BodyRadius * 0.3);
            yield return Settle(3);
            Assert.IsFalse(_world.Planets.Sky.FogOn, "Well above the clouds it is clear space.");
            Assert.Less(_world.Planets.Sky.Veil, 0.01f);

            PlaceAbove("Jupiter", spot, top - 60);
            yield return Settle(3);
            Assert.IsTrue(_world.Planets.Sky.FogOn);
            Assert.Less(_world.Planets.Sky.Visibility, 60f, "Inside the cloud deck you can see almost nothing.");
            Assert.AreEqual(1f, _world.Planets.Sky.Veil, 0.01f);
            Assert.Greater(_world.Craft.Eye.backgroundColor.maxColorComponent, 0.05f, "The sky should be the colour of the planet's gases, not black.");

            PlaceAbove("Jupiter", spot, 60);
            yield return Settle(12);
            Assert.Greater(_world.Planets.Sky.Visibility, 300f, "Under the deck the air clears and the ground shows.");
            Assert.IsTrue(jupiter.GroundShown, "There is ground to land on inside a gas giant.");
            Assert.IsTrue(_world.Planets.Sky.Ground.UnderClouds, "The readout should say you are under the clouds, not in them.");
            Assert.IsTrue(_world.Planets.Sky.Ground.Known);
            Assert.AreEqual(60, _world.Planets.Sky.Ground.AboveGround, 3);
        }

        [UnityTest]
        public IEnumerator NeptunesSeaIsMurkyAndSlowsYouDown()
        {
            yield return Settle(3);
            PlanetSurface neptune = SurfaceOf("Neptune");
            PlanetTerrain t = neptune.Terrain;
            // Find deep water.
            var r = new SeededRandom(9);
            Vec3d spot = Vec3d.Up;
            for (int i = 0; i < 2000; i++)
            {
                Vec3d d = new Vec3d(r.NextDouble() * 2 - 1, r.NextDouble() * 2 - 1, r.NextDouble() * 2 - 1).Normalized;
                if (t.Height(d) < t.SeaLevel - 25)
                {
                    spot = d;
                    break;
                }
            }
            double depth = t.SeaLevel - t.Height(spot);
            Assert.Greater(depth, 25, "Neptune should have deep water.");

            PlaceAbove("Neptune", spot, depth - 10);   // ten metres under the surface
            yield return Settle(6);
            Assert.IsTrue(_world.Planets.Sky.Air.Underwater);
            Assert.IsTrue(_world.Planets.Sky.FogOn);
            Assert.Less(_world.Planets.Sky.Visibility, 200f);
            Assert.IsTrue(_world.Planets.Sky.Ground.Underwater);

            // Give it a shove sideways: water should take most of that speed away within a few seconds.
            Vector3 ground = _world.System.GroundVelocityAt(neptune.Body.Index, _world.Craft.Body.position);
            Vector3 up = (_world.Craft.Body.position - neptune.Body.transform.position).normalized;
            Vector3 sideways = Vector3.Cross(up, Vector3.right).normalized;
            _world.Craft.Body.linearVelocity = ground + sideways * 30f;
            yield return Steps(150);
            ground = _world.System.GroundVelocityAt(neptune.Body.Index, _world.Craft.Body.position);
            float along = Vector3.Dot(_world.Craft.Body.linearVelocity - ground, sideways);
            Assert.Less(along, 8f, "Water should slow the craft down.");
        }

        [UnityTest]
        public IEnumerator FlyingIntoAStormBringsWeather()
        {
            yield return Settle(3);
            PlanetSurface jupiter = SurfaceOf("Jupiter");
            WeatherModel weather = jupiter.Weather;
            Assert.IsTrue(weather.Any);
            // Find a storm that is blowing right now.
            int storm = -1;
            for (int i = 0; i < weather.Count; i++)
            {
                if (weather.Strength(i, _world.System.SimTime) > 0.8) storm = i;
            }
            if (storm < 0) Assert.Inconclusive("No storm happens to be blowing on Jupiter at the start of this seed.");
            _world.Craft.Body.isKinematic = true;
            PlaceAbove("Jupiter", weather.Centre(storm, _world.System.SimTime), 80);
            yield return Settle(6);
            Assert.Greater(_world.Planets.Sky.Weather.Intensity, 0.3, "In the middle of a storm there should be weather.");
            Assert.Greater(_world.Planets.WeatherFx.Intensity, 0.3f);
            Assert.Less(_world.Planets.Sky.Visibility, (float)jupiter.Profile.visibility, "A storm should cut how far you can see.");
        }

        [UnityTest]
        public IEnumerator LeavingABodyPutsItsDetailAway()
        {
            yield return Settle(3);
            PlanetSurface earth = SurfaceOf("Earth");
            Assert.IsTrue(earth.Near);
            _world.Craft.Body.isKinematic = true;
            PlaceAbove("Earth", Vec3d.Right, earth.BodyRadius * 12);
            yield return Settle(4);
            Assert.IsFalse(earth.Near);
            Assert.IsFalse(earth.GroundShown);
            Assert.IsTrue(earth.Body.FarLook.enabled, "From far away the smooth ball with its picture is back.");
            Assert.AreEqual((float)earth.BodyRadius, earth.Body.Ball.radius, 0.01f);
            Assert.AreEqual(0, earth.Features.Count);
            Assert.IsFalse(_world.Planets.Sky.FogOn, "No haze in open space.");
            Assert.AreEqual(Color.black, _world.Craft.Eye.backgroundColor);
        }

        [UnityTest]
        public IEnumerator TheHeightReadoutMeasuresToTheRealGround()
        {
            yield return Settle(3);
            PlanetSurface mars = SurfaceOf("Mars");
            _world.Craft.Body.isKinematic = true;
            PlaceAbove("Mars", GentleSpot(mars), 40);
            yield return Settle(4);
            GroundReading ground = _world.Planets.Read(mars.Body.Index, _world.Craft.Body.position);
            Assert.IsTrue(ground.Known);
            Assert.AreEqual(40, ground.AboveGround, 1.0);
            Assert.IsFalse(ground.Underwater);
            Assert.IsFalse(ground.InCave);
        }

        [UnityTest]
        public IEnumerator SimpleShadersStillBuildAWorkingWorld()
        {
            // Rebuild the world with Unity's standard shaders standing in for the game's own.
            _root.SetActive(false);
            Object.Destroy(_root);
            yield return null;
            SolarSystem.ResetStatics();
            PlanetJobs.ResetStatics();
            PlanetMaterials.ResetStatics();
            PlanetMaterials.Configure(true);
            Assert.IsFalse(PlanetMaterials.OwnTerrain);
            _root = new GameObject("PlanetTestRoot");
            var system = new GameObject("SolarSystem").AddComponent<SolarSystem>();
            system.transform.SetParent(_root.transform, false);
            PlanetData planets = PlanetData.Load();
            system.Build(SolarSystemData.Load(), 4242, PlanetWorld.PaintedLater(planets));
            var world = new GameObject("Planets").AddComponent<PlanetWorld>();
            world.transform.SetParent(_root.transform, false);
            world.Build(system, planets, 4242);
            world.Begin();
            PlanetJobs.FinishAll();
            yield return null;
            Assert.IsNotNull(world.SurfaceOf(system.Model.IndexOf("Mars")));
            Assert.IsTrue(world.SurfaceOf(system.Model.IndexOf("Mars")).Ready);
            PlanetMaterials.ResetStatics();
        }
    }
}
