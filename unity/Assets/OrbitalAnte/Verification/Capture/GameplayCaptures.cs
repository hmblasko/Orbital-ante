using System.Collections;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.AI;
using OrbitalAnte.Arena;
using OrbitalAnte.Content;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Planets;
using OrbitalAnte.Save;
using OrbitalAnte.Space;
using OrbitalAnte.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitalAnte.Verification.Capture
{
    /// <summary>
    /// Captures of the game exactly as it starts for a player: the real bootstrap, menus, HUD and cameras.
    /// Target ids: "combat-arena", "menus". The save system is pointed at a scratch folder first, so the
    /// player's real save is never read or written.
    /// </summary>
    public class GameplayCaptures : CaptureSuite
    {
        const string ArenaLighting = "the arena's own lighting";
        GameBootstrap _game;

        IEnumerator StartGame()
        {
            Layers.Configure();
            SaveSystem.UseFolder(Path.Combine(Application.temporaryCachePath, "oa-verification-save"));
            _game = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            yield return Frames(3);
        }

        protected override void Cleanup()
        {
            if (_game != null)
            {
                _game.ShowMainMenu();
                Object.DestroyImmediate(_game.gameObject);
            }
            _game = null;
            GibManager.DestroyInstance();
            GameSession.ResetStatics();
            SolarSystem.ResetStatics();
            PlanetSurface.ResetStatics();
            PlanetJobs.ResetStatics();
            ContentDatabase.ResetStatics();
            SaveSystem.ResetStatics();
        }

        Camera MenuCamera()
        {
            Transform t = _game.transform.Find("MenuCamera");
            return t != null ? t.GetComponent<Camera>() : null;
        }

        [UnityTest, Category(Category), Timeout(600000)]
        public IEnumerator CombatArenaGameplay()
        {
            Begin("combat-arena");
            yield return StartGame();
            _game.StartRun(CaptureSession.Seed, Scenario.CombatArena);
            yield return Frames(20);
            TestArena arena = _game.Arena;
            Camera eye = arena.PlayerCamera;
            Session.CheckScene("combat arena at start");

            Session.Shoot(eye, "player-start", "Wide", "calm", "gameplay", ArenaLighting, true);
            if (Session.Full) Session.Shoot(eye, "player-start", "Wide", "calm", "gameplay", ArenaLighting, true, null, ScreenshotCapture.DesktopWidth, ScreenshotCapture.DesktopHeight);

            // Stand a few metres from the nearest enemy and look at it: the distance most fights happen at.
            Mannequin nearest = null;
            float best = float.MaxValue;
            foreach (Mannequin m in arena.Mannequins)
            {
                float d = (m.transform.position - arena.Player.transform.position).sqrMagnitude;
                if (m.Alive && d < best) { best = d; nearest = m; }
            }
            if (nearest == null) { Session.Skip("enemy views", "the arena spawned no mannequins"); yield break; }

            Vector3 from = nearest.transform.position + nearest.transform.forward * 5f;
            from.y = arena.Player.transform.position.y;
            Vector3 look = nearest.transform.position - from;
            arena.Player.Teleport(from, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg);
            yield return Frames(10);
            Session.Shoot(eye, "enemy-at-5m", "Medium", "calm", "gameplay", ArenaLighting, true, "enemy idle, armed");
            if (!Session.Full) yield break;

            // A limb coming off as the player sees it, then the mess it leaves.
            yield return Sequence("limb-pop-first-person", "enemy's left arm shot off on frame 2", null, 0f, Quaternion.identity, InspectionCameras.Front, 12, 1,
                "Gib launch, blood burst, hitstop and camera shake from the player's view.", eye,
                i => { if (i == 2) nearest.DamagePart(BodyPartId.LeftUpperArm, 100000f, -nearest.transform.forward); });
            yield return Frames(40);
            Session.Shoot(eye, "enemy-at-5m", "Medium", "after-limb-pop", "gameplay", ArenaLighting, true, "enemy wounded; gib and blood on the floor");

            // Let the fight start and watch how the enemies move and shoot.
            CombatWorld.EnemiesHoldFire = false;
            yield return Sequence("fight-first-person", "enemies released to attack", null, 0f, Quaternion.identity, InspectionCameras.Front, 16, 6,
                "About three seconds of the enemies reacting, moving and firing at a player who is standing still.", eye);
            Session.Shoot(eye, "under-fire", "Medium", "fight", "gameplay", ArenaLighting, true, "enemies attacking");
            CombatWorld.EnemiesHoldFire = true;
            Session.CheckScene("combat arena after a fight");
            Session.Skip("the player's own firing (recoil, tracer, muzzle flash, hit marker)", "needs simulated input; not yet scripted");
        }

        [UnityTest, Category(Category), Timeout(600000)]
        public IEnumerator MenusAndFlight()
        {
            Begin("menus");
            yield return StartGame();
            Camera menu = MenuCamera();
            if (menu == null) { Session.Skip("main menu", "the menu camera was not found"); yield break; }
            Session.CheckScene("main menu");
            Session.Shoot(menu, "main-menu", "Medium", "first launch", "gameplay", "none (interface only)", true);
            if (Session.Full) Session.Shoot(menu, "main-menu", "Medium", "first launch", "gameplay", "none (interface only)", true, null, ScreenshotCapture.DesktopWidth, ScreenshotCapture.DesktopHeight);

            _game.StartRun(CaptureSession.Seed, Scenario.SolarSystem);
            for (int i = 0; i < 12; i++) { PlanetJobs.FinishAll(); yield return null; }
            SolarSystemWorld space = _game.Space;
            Camera eye = space.Craft.Eye;
            Session.Shoot(eye, "flight-start", "Wide", "run start, Earth orbit", "gameplay", "the game's own sun and sky", true);

            space.Map.SetOpen(true);
            yield return Frames(4);
            Session.Shoot(eye, "system-map", "Medium", "map open", "gameplay", "none (interface only)", true);
            space.Map.SetOpen(false);
            yield return Frames(2);

            PauseMenu pause = Object.FindFirstObjectByType<PauseMenu>();
            if (pause != null)
            {
                pause.Open();
                // The game is paused now, so wait in real frames only.
                yield return Frames(3);
                Session.Shoot(eye, "pause-menu", "Medium", "paused", "gameplay", "none (interface only)", true);
                pause.Close();
                GameSession.SetPaused(false);
                yield return Frames(2);
            }
            else Session.Skip("pause menu", "no PauseMenu object found");
            Session.CheckScene("flight with menus");
            Session.Skip("controls page and rebinding screens", "not yet scripted");
        }
    }
}
