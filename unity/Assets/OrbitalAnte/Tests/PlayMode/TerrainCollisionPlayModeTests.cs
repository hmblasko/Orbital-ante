using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using OrbitalAnte.Content;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.Ship;
using OrbitalAnte.Space;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitalAnte.Tests
{
    /// <summary>Collision regressions. These require Unity; a source compilation does not execute them.</summary>
    public class TerrainCollisionPlayModeTests
    {
        GameObject _root;
        SolarSystem _system;
        PlanetWorld _world;
        PlanetSurface _surface;
        SolarSystemData _systemData;
        PlanetData _planetData;
        OrbitalAnte.Ship.Ship _ship;
        Astronaut _astronaut;
        Camera _camera;

        [SetUp]
        public void SetUp()
        {
            Layers.Configure();
            GameInput.Initialize();
            ContentDatabase.ResetStatics();
            SolarSystem.ResetStatics();
            PlanetJobs.ResetStatics();
            ShipConfig.ResetStatics();
            ShipMeshes.ResetStatics();
            PlanetSurface.PictureWidthForTests = 32;
            _root = new GameObject("TerrainCollisionTest");
            _systemData = SolarSystemData.CreateDefaults();
            _systemData.bodies.RemoveAll(b => b.kind != BodyKind.Star && b.id != "Earth");
            _systemData.asteroidBelt.count = 0;
            _planetData = PlanetData.CreateDefaults();
            // Test-only geography: a known high formation at a tile junction, plus one real cave.
            var profile = new PlanetProfile
            {
                bodyId = "Earth", continentHeight = 0, hillHeight = 0, mountainHeight = 0,
                detailHeight = 0, erosion = 0, valleyDepth = 0, terraceStep = 0, cragHeight = 0,
            };
            profile.formations.Add(FormationUse.Landmark("stratovolcano", 0, 0, 200));
            profile.formations.Add(new FormationUse("cave", 1));
            _planetData.planets = new List<PlanetProfile> { profile };
            _planetData.settings.simpleShaders = true;
            _planetData.settings.uploadsPerFrame = 1;
            _planetData.settings.backgroundWorkers = 1;
            PlanetMaterials.Configure(true);
            _system = _root.AddComponent<SolarSystem>();
            _system.Build(_systemData, 4242, PlanetWorld.PaintedLater(_planetData));
            _world = new GameObject("Ground").AddComponent<PlanetWorld>();
            _world.transform.SetParent(_root.transform, false);
            _world.Build(_system, _planetData, 4242);
            int earth = _system.Model.IndexOf("Earth");
            _surface = _world.SurfaceOf(earth);
            _world.TerrainNow(earth);
            _ship = OrbitalAnte.Ship.Ship.Create(_root.transform, ShipConfig.Load(), TestCraftConfig.Load());
            _ship.Flight.System = _system;
            _astronaut = Astronaut.Create(_root.transform, _ship, ShipConfig.Load(), _system);
            _astronaut.enabled = false;
            _ship.enabled = false;
            _ship.Flight.enabled = false;
            _ship.Body.isKinematic = true;
            _astronaut.Suit.gameObject.SetActive(true);
            _astronaut.Suit.isKinematic = true;
            _system.Focus = _ship.Gravity;
            Place(_ship.Gravity, Vec3d.Right, -ShipLayout.GroundY + 0.3);
            Place(_astronaut.Suit.GetComponent<GravityBody>(), CubeSphere.FromLatLon(0, 3), 0.25);
            _system.SnapToFocus();
            _system.enabled = false; // Hold the reference frame still while background results are controlled.
            var cameraObject = new GameObject("ObservationPoint");
            cameraObject.transform.SetParent(_root.transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.enabled = false;
            _system.ViewCamera = _camera;
            _camera.transform.position = _ship.Body.position;
            _world.Begin();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_planetData != null) Object.DestroyImmediate(_planetData);
            if (_systemData != null) Object.DestroyImmediate(_systemData);
            SolarSystem.ResetStatics();
            PlanetSurface.ResetStatics();
            PlanetJobs.ResetStatics();
            ContentDatabase.ResetStatics();
            ShipMeshes.ResetStatics();
            PlanetSurface.PictureWidthForTests = 0;
        }

        void Place(GravityBody mover, Vec3d direction, double aboveGround)
        {
            int index = _surface.Body.Index;
            Vec3d offset = _system.Model.Orientation(index, _system.States[index]).Rotate(direction)
                * (_surface.GroundRadius(direction) + aboveGround);
            Vec3d spin = _system.Model.SpinAxis(index) * _system.Model.SpinRate(index);
            bool held = mover.Body.isKinematic;
            mover.Body.isKinematic = false;
            _system.Place(mover, _system.States[index].Position + offset,
                _system.States[index].Velocity + Vec3d.Cross(spin, offset));
            mover.Body.isKinematic = held;
            mover.Neighbourhood = index;
        }

        Vec3d PositionOnBody(Rigidbody body) => SpaceConvert.ToVec3d(
            Quaternion.Inverse(_surface.Body.Body.rotation) * (body.position - _surface.Body.Body.position));

        IEnumerator WaitForBothSupportingTiles()
        {
            float until = Time.realtimeSinceStartup + 60f;
            // Deliberately do not FinishAll: exercise one-upload streaming with both real collider shapes.
            while (Time.realtimeSinceStartup < until)
            {
                if (_surface.FineSolidAt(PositionOnBody(_ship.Body)) && _surface.FineSolidAt(PositionOnBody(_astronaut.Suit))) yield break;
                yield return null;
            }
            Assert.Fail("The ship and suit never received their finest solid supporting tiles.");
        }

        void AssertTerrainColliderBelow(Vec3d point)
        {
            Vec3d direction = point.Normalized;
            double radius = _surface.GroundRadius(direction);
            Vector3 from = _surface.Body.transform.TransformPoint(SpaceConvert.ToVector3(direction * (radius + 20)));
            Vector3 down = -_surface.Body.transform.TransformDirection(SpaceConvert.ToVector3(direction));
            Physics.SyncTransforms();
            foreach (MeshCollider collider in _surface.GetComponentsInChildren<MeshCollider>())
                if (collider.enabled && collider.Raycast(new Ray(from, down), out _, 40)) return;
            Assert.Fail("No active terrain mesh collider supports this surface direction.");
        }

        [UnityTest]
        public IEnumerator AHighFormationProvidesFineSolidSupportForTheActualShipAndSuit()
        {
            double relief = _surface.Terrain.Height(Vec3d.Right) - _surface.Terrain.CoarseHeight(Vec3d.Right);
            Assert.Greater(relief, TerrainMesher.TileSize(_surface.SurfaceRadius, _surface.MaxLevel) * 0.8,
                "This fixture must exceed the old centre-distance refinement bound.");
            yield return WaitForBothSupportingTiles();
            AssertTerrainColliderBelow(PositionOnBody(_ship.Body));
            AssertTerrainColliderBelow(PositionOnBody(_astronaut.Suit));
        }

        [UnityTest]
        public IEnumerator GroundRequestsFollowThePhysicsPoseWhenThePlanetRenderPoseInterpolates()
        {
            _world.enabled = false;
            Rigidbody planet = _surface.Body.Body;
            var gather = typeof(PlanetWorld).GetMethod("GatherGroundPoints", BindingFlags.Instance | BindingFlags.NonPublic);
            var pointsField = typeof(PlanetWorld).GetField("_groundPoints", BindingFlags.Instance | BindingFlags.NonPublic);
            Vec3d shipPoint = PositionOnBody(_ship.Body);
            Vec3d suitPoint = PositionOnBody(_astronaut.Suit);
            bool sawInterpolation = false;
            for (int frame = 0; frame < 8; frame++)
            {
                planet.MovePosition(planet.position + Vector3.right * 40);
                planet.MoveRotation(Quaternion.AngleAxis(17, Vector3.up) * planet.rotation);
                yield return new WaitForFixedUpdate();
                yield return null;
                sawInterpolation |= Vector3.Distance(planet.position, planet.transform.position) > 0.01f
                    || Quaternion.Angle(planet.rotation, planet.transform.rotation) > 0.01f;
                _ship.Body.position = planet.position + planet.rotation * SpaceConvert.ToVector3(shipPoint);
                _astronaut.Suit.position = planet.position + planet.rotation * SpaceConvert.ToVector3(suitPoint);
                object[] arguments = { _surface, _camera.transform.position, 0f };
                Assert.AreEqual(2, (int)gather.Invoke(_world, arguments));
                var points = (Vec3d[])pointsField.GetValue(_world);
                Assert.Less(Vec3d.Distance(shipPoint, points[0]), 0.002,
                    "The current ship physics pose must be measured against the current planet physics pose.");
                Assert.Less(Vec3d.Distance(suitPoint, points[1]), 0.002,
                    "The suit must receive the same coordinate-frame protection as the ship.");
            }
            Assert.IsTrue(sawInterpolation, "The fixture must exercise different render and physics poses.");
        }

        [UnityTest]
        public IEnumerator ARemoteViewKeepsTheShipAndSuitGroundAlive()
        {
            yield return WaitForBothSupportingTiles();
            _system.Focus = _astronaut.Suit.GetComponent<GravityBody>();
            _camera.transform.position = _surface.Body.transform.position + Vector3.up * (float)(_surface.BodyRadius * 20);
            for (int frame = 0; frame < 12; frame++) yield return null;
            Assert.IsTrue(_surface.Near);
            Assert.IsTrue(_surface.GroundShown);
            Assert.IsTrue(_surface.FineSolidAt(PositionOnBody(_ship.Body)));
            Assert.IsTrue(_surface.FineSolidAt(PositionOnBody(_astronaut.Suit)));
            AssertTerrainColliderBelow(PositionOnBody(_ship.Body));
            AssertTerrainColliderBelow(PositionOnBody(_astronaut.Suit));
        }

        [UnityTest]
        public IEnumerator ADelayedCaveColliderKeepsItsMouthCoveredAndItsRescueExemptionClosed()
        {
            _world.enabled = false;
            Assert.AreEqual(1, _surface.Terrain.Caves.Count);
            Cave cave = _surface.Terrain.Caves[0];
            _surface.SetNear(false);
            _surface.SetNear(true);
            // Failure injection: withhold just the cave result, leaving tile work free to finish.
            var jobs = (List<PlanetJobs.Handle>)typeof(PlanetSurface).GetField("_caveJobs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_surface);
            foreach (PlanetJobs.Handle job in jobs) job.Cancel();
            Vec3d mouth = cave.Mouths[0] * _surface.GroundRadius(cave.Mouths[0]);
            for (int pass = 0; pass <= _surface.MaxLevel + 2; pass++)
            {
                _surface.Tick(mouth + cave.Mouths[0] * 2, mouth, 0);
                PlanetJobs.FinishAll();
                yield return null;
            }
            Assert.IsTrue(_surface.SolidAt(mouth), "Coarser support must remain while the mouth has no wall collider.");
            Assert.IsFalse(_surface.FineSolidAt(mouth), "The cut mouth must not replace its support early.");
            Assert.IsFalse(_surface.InCave(cave.Centres[cave.Centres.Length / 2], 4));
            AssertTerrainColliderBelow(mouth);

            _surface.SetNear(false);
            _surface.SetNear(true);
            for (int pass = 0; pass <= _surface.MaxLevel + 2; pass++)
            {
                _surface.Tick(mouth + cave.Mouths[0] * 2, mouth, 0);
                PlanetJobs.FinishAll();
                yield return null;
            }
            Assert.IsTrue(_surface.FineSolidAt(mouth));
            Assert.IsTrue(_surface.InCave(cave.Centres[cave.Centres.Length / 2], 4));
            Assert.IsFalse(_surface.InCave(cave.Mouths[0] * (_surface.SurfaceRadius * 0.25), 4),
                "The same mouth direction deep below its real tunnel is not a cave.");
        }
    }
}
