using System.Collections;
using NUnit.Framework;
using OrbitalAnte.Content;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.Ship;
using OrbitalAnte.Space;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitalAnte.Verification.Capture
{
    /// <summary>
    /// Captures of the starter ship in the real game: from outside in the game's own sunlight (the standard
    /// viewpoints, parked in Earth orbit), from the pilot's seat, with the hatch open, and after damage.
    /// Target id: "ship".
    ///
    /// The outside views use an inspection camera, because the player's own eyes start inside the cabin. They
    /// are still the game's ship, shaders and light; only the backdrop is plain, so the outline can be judged.
    /// </summary>
    public class ShipCaptures : CaptureSuite
    {
        const string SunLighting = "the game's own sun and sky for this seed and moment, Earth orbit, sunlit side";
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

        static IEnumerator Settle(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                PlanetJobs.FinishAll();
                yield return null;
            }
        }

        /// <summary>The ship's own size, leaving out flames, beams and anything else far larger than a lander.</summary>
        static Bounds ShipBounds(Transform ship)
        {
            Bounds bounds = InspectionCameras.BoundsOf(ship);
            if (bounds.size.magnitude > 45f || bounds.size.magnitude < 2f)
                bounds = new Bounds(ship.position + ship.up * 1.5f, new Vector3(11f, 11f, 11f));
            return bounds;
        }

        void ShootOutside(string state, System.Collections.Generic.IEnumerable<InspectionCameras.Shot> shots)
        {
            Transform ship = _world.Ship.transform;
            Bounds bounds = ShipBounds(ship);
            float aspect = ScreenshotCapture.DeckWidth / (float)ScreenshotCapture.DeckHeight;
            foreach (InspectionCameras.Shot shot in shots)
            {
                InspectionCameras.Place(Cam, bounds, ship.rotation, shot.View, shot.Distance, aspect);
                Session.Shoot(Cam, shot.View.Name, shot.Distance.ToString(), state, "outside, game light", SunLighting);
            }
        }

        [UnityTest, Category(Category), Timeout(1800000)]
        public IEnumerator ShipInsideAndOut()
        {
            Begin("ship");
            Layers.Configure();
            GameInput.Initialize();
            ContentDatabase.ResetStatics();
            SolarSystem.ResetStatics();
            _world = new GameObject("CaptureSolarSystem").AddComponent<SolarSystemWorld>();
            _world.Build(CaptureSession.Seed, StartPlace.Orbit);
            _world.Craft.ControlsLocked = true;
            _world.Hud.SetVisible(false);
            yield return Settle(10);

            OrbitalAnte.Ship.Ship ship = _world.Ship;
            Camera eye = _world.Astronaut.Eye;
            Cam = MakeCamera("ShipInspectionCamera");
            Cam.backgroundColor = Color.black;
            Cam.farClipPlane = 400f;
            Session.CheckScene("ship at the start of a run");

            // Outside, whole and undamaged.
            ShootOutside("undamaged", Session.Full ? InspectionCameras.HeroSet() : InspectionCameras.QuickSet());
            if (Session.Full)
            {
                Bounds bounds = ShipBounds(ship.transform);
                float aspect = ScreenshotCapture.DeckWidth / (float)ScreenshotCapture.DeckHeight;
                InspectionCameras.Place(Cam, bounds, ship.transform.rotation, InspectionCameras.Underside, ViewDistance.Medium, aspect);
                Session.Shoot(Cam, "underside", "Medium", "undamaged, hatch closed", "outside, game light", SunLighting);
            }

            // From the seat: what the player looks at for most of a run. Once bare, once with the readouts.
            Session.Shoot(eye, "seat", "Medium", "buckled in, readouts hidden", "gameplay", SunLighting);
            _world.Hud.SetVisible(true);
            yield return Frames(3);
            Session.Shoot(eye, "seat-hud", "Medium", "buckled in", "gameplay", SunLighting, true);
            if (Session.Full)
                Session.Shoot(eye, "seat-hud", "Medium", "buckled in", "gameplay", SunLighting, true, null, ScreenshotCapture.DesktopWidth, ScreenshotCapture.DesktopHeight);
            _world.Hud.SetVisible(false);

            // Hatch: the lift ring under the belly.
            if (ship.ToggleHatch())
            {
                yield return Sequence("hatch-opening", "hatch opening", () => ship.transform.position - ship.transform.up * 2f, 5f, ship.transform.rotation,
                    InspectionCameras.Underside, 8, 4, "The belly hatch opening, about two seconds.");
                Bounds bounds = ShipBounds(ship.transform);
                float aspect = ScreenshotCapture.DeckWidth / (float)ScreenshotCapture.DeckHeight;
                InspectionCameras.Place(Cam, bounds, ship.transform.rotation, InspectionCameras.Underside, ViewDistance.Medium, aspect);
                Session.Shoot(Cam, "underside", "Medium", "hatch open", "outside, game light", SunLighting);
                ship.ToggleHatch();
                yield return Frames(45);
            }
            else Session.Skip("hatch open", "the hatch refused to move");

            // Damage: glass broken as seen from the seat, then a leg and a thruster knocked about, seen from outside.
            if (Session.Full)
            {
                int glass = ship.Model.Find(PartKind.Window, 0);
                if (glass >= 0)
                {
                    ship.DamagePart(glass, ship.Model.Parts[glass].MaxHealth * 0.6f, ship.PartPosition(glass));
                    yield return Frames(4);
                    Session.Shoot(eye, "seat", "Medium", "front glass damaged", "gameplay", SunLighting);
                    ship.DamagePart(glass, ship.Model.Parts[glass].MaxHealth, ship.PartPosition(glass));
                    yield return Frames(4);
                    Session.Shoot(eye, "seat", "Medium", "front glass broken", "gameplay", SunLighting);
                }
                else Session.Skip("broken glass", "the ship has no window part");

                int leg = ship.Model.Find(PartKind.Leg, 0);
                int thruster = ship.Model.Find(PartKind.Thruster, 1);
                if (leg >= 0) ship.DamagePart(leg, ship.Model.Parts[leg].MaxHealth * 0.6f, ship.PartPosition(leg));
                if (thruster >= 0) ship.DamagePart(thruster, ship.Model.Parts[thruster].MaxHealth * 0.6f, ship.PartPosition(thruster));
                yield return Frames(4);
                ShootOutside("damaged: front glass broken, one leg and one thruster hurt", InspectionCameras.BackgroundSet());
                if (leg >= 0) ship.DamagePart(leg, ship.Model.Parts[leg].MaxHealth * 20f, ship.PartPosition(leg));
                yield return Frames(20);
                ShootOutside("one leg torn off", InspectionCameras.QuickSet());
            }
            Session.CheckScene("ship after damage");

            Session.Skip("walking round the cabin, looking aft, the lockers and lamps", "the view only turns with look input, which the capture suite cannot give yet");
            Session.Skip("the lift ride, the seat belt close up, repair, glass shot from outside", "need simulated input; not yet scripted");
            Session.Skip("the ship on the launch pad and the launch itself", "a separate target (launch) not yet scripted");
            Session.Skip("the moon rover on its davit", "a separate target not yet scripted");
        }
    }
}
