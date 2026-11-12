using System.Collections;
using NUnit.Framework;
using OrbitalAnte.Content;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.Space;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitalAnte.Verification.Capture
{
    /// <summary>
    /// Captures of the solar system as the player sees it from the cockpit: every world from far off and from
    /// low orbit, and the ground of a chosen few from standing height. All taken through the craft's own
    /// camera, with the game's own sky, lighting and level of detail. Target ids: "planets", "planet-surface".
    /// </summary>
    public class WorldCaptures : CaptureSuite
    {
        const string SunLighting = "the game's own sun and sky for this seed and moment";
        SolarSystemWorld _world;

        protected override void Cleanup()
        {
            if (_world != null) Object.DestroyImmediate(_world.gameObject);
            _world = null;
            SolarSystem.ResetStatics();
            PlanetSurface.ResetStatics();
            PlanetJobs.ResetStatics();
            ContentDatabase.ResetStatics();
        }

        void BuildWorld()
        {
            Layers.Configure();
            GameInput.Initialize();
            ContentDatabase.ResetStatics();
            SolarSystem.ResetStatics();
            var go = new GameObject("CaptureSolarSystem");
            _world = go.AddComponent<SolarSystemWorld>();
            _world.Build(CaptureSession.Seed);
            _world.Hud.SetVisible(false);
            _world.Craft.ControlsLocked = true;
        }

        /// <summary>Lets background ground-building finish and the detail catch up with where the craft is.</summary>
        static IEnumerator Settle(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                PlanetJobs.FinishAll();
                yield return null;
            }
        }

        static bool HasGround(BodyKind kind) => kind != BodyKind.Star && kind != BodyKind.Asteroid;

        Vector3 BodyPosition(int body) => _world.System.Bodies[body].transform.position;

        void Aim(Vector3 forward, Vector3 up)
        {
            Quaternion rotation = Quaternion.LookRotation(forward, up);
            _world.Craft.transform.rotation = rotation;
            _world.Craft.Body.rotation = rotation;
            _world.Craft.Body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        /// <summary>Parks the craft in orbit on the sunlit side, turned so part of the night side shows too.</summary>
        void Orbit(int body, double altitude, double degreesFromSun)
        {
            SolarSystem system = _world.System;
            Vec3d toSun = (system.States[0].Position - system.States[body].Position).Normalized;
            Vec3d side = toSun.RotatedAround(system.Model.SpinAxis(body), degreesFromSun * System.Math.PI / 180.0);
            system.PlaceInOrbit(_world.Craft.Gravity, body, altitude, side);
            system.Focus = _world.Craft.Gravity;
            system.SnapToFocus();
        }

        [UnityTest, Category(Category), Timeout(1800000)]
        public IEnumerator PlanetsFromSpace()
        {
            Begin("planets");
            BuildWorld();
            yield return Settle(6);
            SolarSystem system = _world.System;
            Camera eye = _world.Craft.Eye;
            Session.CheckScene("solar system at start");

            for (int i = 0; i < system.Model.Count; i++)
            {
                BodySpec spec = system.Model.Spec(i);
                if (!HasGround(spec.kind)) continue;
                if (!Session.Full && spec.id != "Earth" && spec.id != "Moon") continue;
                PlanetSurface surface = _world.Planets.SurfaceOf(i);
                if (surface == null || !surface.Ready) { Session.Skip(spec.id, "its ground was never worked out"); continue; }

                // Far: the whole world in view, as on approach.
                Orbit(i, spec.radius * 3.0, 50);
                yield return Settle(6);
                Aim(BodyPosition(i) - eye.transform.position, _world.Craft.transform.up);
                yield return null;
                Aim(BodyPosition(i) - eye.transform.position, _world.Craft.transform.up);
                Session.Shoot(eye, "approach", "Wide", spec.id, "gameplay", SunLighting);

                // Low orbit: the horizon and the ground below, pitched down a little as when lining up a landing.
                Orbit(i, spec.radius * 0.22, 35);
                yield return Settle(10);
                Transform craft = _world.Craft.transform;
                Aim(Quaternion.AngleAxis(24f, craft.right) * craft.forward, craft.up);
                yield return Settle(2);
                Session.Shoot(eye, "low-orbit", "Medium", spec.id, "gameplay", SunLighting);
            }

            // The flight readouts over a real view, at both screen sizes.
            int earth = system.Model.IndexOf("Earth");
            if (earth > 0)
            {
                Orbit(earth, 450, 20);
                yield return Settle(8);
                _world.Hud.SetVisible(true);
                yield return Frames(3);
                Session.Shoot(eye, "cockpit-hud", "Medium", "Earth orbit", "gameplay", SunLighting, true);
                if (Session.Full)
                    Session.Shoot(eye, "cockpit-hud", "Medium", "Earth orbit", "gameplay", SunLighting, true, null, ScreenshotCapture.DesktopWidth, ScreenshotCapture.DesktopHeight);
                _world.Hud.SetVisible(false);
            }
            Session.CheckScene("solar system after visiting every world");
        }

        /// <summary>A sunlit, gently sloping, dry spot away from cave mouths, or null if none was found.</summary>
        Vec3d? SunlitGentleSpot(int body, PlanetSurface surface)
        {
            SolarSystem system = _world.System;
            Vec3d toSun = (system.States[0].Position - system.States[body].Position).Normalized;
            QuatD orientation = system.Model.Orientation(body, system.States[body]);
            PlanetTerrain t = surface.Terrain;
            var r = new SeededRandom(5);
            for (int i = 0; i < 6000; i++)
            {
                var d = new Vec3d(r.NextDouble() * 2 - 1, r.NextDouble() * 2 - 1, r.NextDouble() * 2 - 1);
                if (d.SqrMagnitude < 0.01) continue;
                d = d.Normalized;
                // Sun between about 25 and 65 degrees up: lit, with shadows long enough to show the shape of the land.
                double sunHeight = Vec3d.Dot(orientation.Rotate(d), toSun);
                if (sunHeight < 0.42 || sunHeight > 0.9) continue;
                if (t.HasSea && t.Height(d) < t.SeaLevel + 2) continue;
                if (t.InHole(d, 30)) continue;
                if (t.SlopeAt(d, 3) < 0.10 && t.SlopeAt(d, 12) < 0.14) return d;
            }
            return null;
        }

        void Stand(int body, PlanetSurface surface, Vec3d direction, double height)
        {
            SolarSystem system = _world.System;
            double radius = surface.GroundRadius(direction) + height;
            Vec3d offset = system.Model.Orientation(body, system.States[body]).Rotate(direction) * radius;
            Vec3d spin = system.Model.SpinAxis(body) * system.Model.SpinRate(body);
            system.Place(_world.Craft.Gravity, system.States[body].Position + offset, system.States[body].Velocity + Vec3d.Cross(spin, offset));
            system.Focus = _world.Craft.Gravity;
            system.SnapToFocus();
        }

        [UnityTest, Category(Category), Timeout(1800000)]
        public IEnumerator PlanetSurfaces()
        {
            Begin("planet-surface");
            BuildWorld();
            yield return Settle(6);
            SolarSystem system = _world.System;
            Camera eye = _world.Craft.Eye;
            string[] ids = Session.Full ? new[] { "Earth", "Moon", "Mars", "Venus", "Titan", "Io", "Europa" } : new[] { "Earth" };

            foreach (string id in ids)
            {
                int body = system.Model.IndexOf(id);
                PlanetSurface surface = body > 0 ? _world.Planets.SurfaceOf(body) : null;
                if (surface == null || !surface.Ready) { Session.Skip(id + " surface", "no ground for this body"); continue; }
                Vec3d? spot = SunlitGentleSpot(body, surface);
                if (spot == null) { Session.Skip(id + " surface", "found no sunlit, gentle, dry spot to stand on"); continue; }

                // Hold the craft at standing height for a while so the finest ground and the loose rocks appear.
                for (int i = 0; i < 24; i++)
                {
                    Stand(body, surface, spot.Value, 2.0);
                    PlanetJobs.FinishAll();
                    yield return null;
                }
                Stand(body, surface, spot.Value, 2.0);
                Vector3 up = (eye.transform.position - BodyPosition(body)).normalized;
                Vector3 toSun = (BodyPosition(0) - eye.transform.position).normalized;
                Vector3 sunward = Vector3.ProjectOnPlane(toSun, up).normalized;
                if (sunward.sqrMagnitude < 0.5f) sunward = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
                Vector3 across = Vector3.Cross(up, sunward);

                Aim(-sunward, up);
                Session.Shoot(eye, "ground-down-sun", "Wide", id, "gameplay", SunLighting);
                Aim(across, up);
                Session.Shoot(eye, "ground-across-sun", "Wide", id, "gameplay", SunLighting);
                if (Session.Full)
                {
                    Aim(Quaternion.AngleAxis(-25f, up) * sunward, up);
                    Session.Shoot(eye, "ground-toward-sun", "Wide", id, "gameplay", SunLighting);
                    Aim(Quaternion.AngleAxis(38f, across) * -sunward, up);
                    Session.Shoot(eye, "ground-at-feet", "Close", id, "gameplay", SunLighting);
                }
                Session.CheckScene(id + " surface");
            }
            Session.Skip("weather, caves, gas giant interiors, underwater Neptune", "not yet scripted in the capture suite; add states here when those are reviewed");
        }
    }
}
