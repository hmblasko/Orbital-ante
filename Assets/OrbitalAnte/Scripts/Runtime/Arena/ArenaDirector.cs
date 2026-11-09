using System.Collections.Generic;
using OrbitalAnte.Characters;
using OrbitalAnte.Combat;
using OrbitalAnte.Core;
using OrbitalAnte.Feel;
using OrbitalAnte.Input;
using OrbitalAnte.Player;
using OrbitalAnte.UI;
using UnityEngine;

namespace OrbitalAnte.Arena
{
    /// <summary>Runs the test arena: spawns the player and seeded dummies, handles playtest keys.</summary>
    public class ArenaDirector : MonoBehaviour
    {
        public int DummyCount = 6;

        MannequinConfig mannequinConfig;
        readonly List<Mannequin> spawned = new List<Mannequin>();
        Transform dummyRoot;

        void Start()
        {
            mannequinConfig = MannequinConfig.Load();
            GameFeel.EnsureCreated();
            TestArenaBuilder.Build();

            var player = FirstPersonController.Create(PlayerConfig.Load(), new Vector3(0, 0.05f, -12f), 0f);
            player.gameObject.AddComponent<DebugHitscanGun>();
            FirstPersonController.SetCursorLocked(true);

            Hud.Create();
            FrameTimeOverlay.Create();
            PauseMenu.Create();

            dummyRoot = new GameObject("Dummies").transform;
            SpawnDummies();
            Hud.Instance.Notify($"Test arena  ·  seed #{RunSeed.Current}", NoticeKind.System);
            UpdateStateLabel();
        }

        void SpawnDummies()
        {
            foreach (var m in spawned)
                if (m != null) Destroy(m.gameObject);
            spawned.Clear();

            // Same seed, same line-up: positions, facing and colour all come from the run seed.
            var rng = RunSeed.CreateStream("arena.dummies");
            for (int i = 0; i < DummyCount; i++)
            {
                float x = -10f + i * 4f + rng.Range(-0.8f, 0.8f);
                float z = rng.Range(0f, 10f);
                float yaw = 180f + rng.Range(-35f, 35f);
                float j = mannequinConfig.colorJitter;
                var c = mannequinConfig.bodyColor;
                var color = new Color(
                    Mathf.Clamp01(c.r + rng.Range(-j, j)),
                    Mathf.Clamp01(c.g + rng.Range(-j, j)),
                    Mathf.Clamp01(c.b + rng.Range(-j, j)));
                var m = MannequinBuilder.Build(mannequinConfig, new Vector3(x, 0.02f, z), yaw, color, dummyRoot);
                m.name = "Dummy_" + i;
                m.GetComponent<MannequinBrain>().SpawnIndex = i;
                m.StateChanged += OnDummyState;
                spawned.Add(m);
            }
        }

        void OnDummyState(Mannequin m, LocomotionState s)
        {
            string msg = s switch
            {
                LocomotionState.Limp => "Leg off! It's hopping.",
                LocomotionState.Crawl => "No legs. It's crawling.",
                LocomotionState.Wriggle => "No limbs. It's wriggling.",
                LocomotionState.HeadOnly => "Just a head now. Still alive!",
                _ => null,
            };
            if (msg != null) Hud.Instance?.Notify(msg, NoticeKind.Combat);
        }

        void Update()
        {
            if (PauseMenu.IsPaused) return;
            if (GameInput.DebugRespawn.WasPressedThisFrame())
            {
                SpawnDummies();
                Hud.Instance?.Notify("Fresh dummies!", NoticeKind.Info);
            }
            if (GameInput.DebugSlowMo.WasPressedThisFrame())
            {
                GameFeel.HeadshotSlowMo = !GameFeel.HeadshotSlowMo;
                Hud.Instance?.Notify("Headshot slow-mo: " + (GameFeel.HeadshotSlowMo ? "ON" : "OFF"), NoticeKind.Info);
            }
            if (GameInput.DebugWalkMode.WasPressedThisFrame())
            {
                MannequinBrain.GlobalMode = (DummyWalkMode)(((int)MannequinBrain.GlobalMode + 1) % 3);
                Hud.Instance?.Notify("Dummies: " + MannequinBrain.GlobalMode, NoticeKind.Info);
                UpdateStateLabel();
            }
        }

        void UpdateStateLabel() => Hud.Instance?.SetState($"seed #{RunSeed.Current}");
    }
}
