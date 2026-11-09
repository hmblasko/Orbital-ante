using System.Collections.Generic;
using System.Linq;
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
    /// <summary>
    /// Runs the test arena: spawns the player with the rifle, seeded armed dummies and a weapon
    /// table, and handles the playtest keys.
    /// </summary>
    public class ArenaDirector : MonoBehaviour
    {
        public int DummyCount = 6;

        static readonly Vector3 TablePos = new Vector3(4.5f, 0f, -12f);

        MannequinConfig mannequinConfig;
        readonly List<Mannequin> spawned = new List<Mannequin>();
        Transform dummyRoot;
        PlayerWeapons playerWeapons;

        void Start()
        {
            mannequinConfig = MannequinConfig.Load();
            GameFeel.EnsureCreated();
            TestArenaBuilder.Build();
            BuildWeaponTable();

            var player = FirstPersonController.Create(PlayerConfig.Load(), new Vector3(0, 0.05f, -12f), 0f);
            playerWeapons = player.gameObject.AddComponent<PlayerWeapons>();
            playerWeapons.Equip(WeaponFactory.Create(WeaponFactory.Rifle, player.transform.position, Quaternion.identity));
            FirstPersonController.SetCursorLocked(true);

            Hud.Create();
            FrameTimeOverlay.Create();
            PauseMenu.Create();

            dummyRoot = new GameObject("Dummies").transform;
            SpawnDummies();
            SpawnTableWeapons();
            Hud.Instance.Notify($"Test arena  ·  seed #{RunSeed.Current}", NoticeKind.System);
            UpdateStateLabel();
        }

        void BuildWeaponTable()
        {
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "WeaponTable";
            table.transform.position = TablePos + new Vector3(0, 0.45f, 0);
            table.transform.localScale = new Vector3(2.2f, 0.9f, 0.9f);
            table.GetComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Lit(new Color(0.35f, 0.4f, 0.5f));
            table.isStatic = true;
        }

        void SpawnTableWeapons()
        {
            // Loose guns not held by anyone get cleared and the table restocked.
            foreach (var w in Weapon.All.ToArray())
                if (w != null && w.IsLoose) Destroy(w.gameObject);
            var top = TablePos + new Vector3(0, 0.95f, 0);
            WeaponFactory.Create(WeaponFactory.Pistol, top + new Vector3(-0.5f, 0, 0), Quaternion.Euler(0, 90, 90));
            WeaponFactory.Create(WeaponFactory.Rifle, top + new Vector3(0.45f, 0, 0), Quaternion.Euler(0, 90, 90));
        }

        void SpawnDummies()
        {
            foreach (var m in spawned)
                if (m != null) Destroy(m.gameObject);
            spawned.Clear();

            // Same seed, same line-up: positions, facing, colour and gun all come from the run seed.
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

                var gunner = m.gameObject.AddComponent<DummyGunner>();
                bool pistol = rng.NextFloat() < 0.4f;
                var w = WeaponFactory.Create(pistol ? WeaponFactory.Pistol : WeaponFactory.Rifle, m.transform.position, Quaternion.identity);
                if (!gunner.Equip(w)) Destroy(w.gameObject);
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
                SpawnTableWeapons();
                Hud.Instance?.Notify("Fresh dummies!", NoticeKind.Info);
            }
            if (GameInput.DebugSlowMo.WasPressedThisFrame())
            {
                GameFeel.HeadshotSlowMo = !GameFeel.HeadshotSlowMo;
                Hud.Instance?.Notify("Headshot slow-mo: " + (GameFeel.HeadshotSlowMo ? "ON" : "OFF"), NoticeKind.Info);
            }
            if (GameInput.DebugOneHand.WasPressedThisFrame() && playerWeapons != null)
            {
                playerWeapons.OneHandedTest = !playerWeapons.OneHandedTest;
                Hud.Instance?.Notify(playerWeapons.OneHandedTest ? "One-handed: more spread and kick" : "Two hands again", NoticeKind.Info);
            }
            if (GameInput.DebugWalkMode.WasPressedThisFrame())
            {
                MannequinBrain.GlobalMode = (DummyWalkMode)(((int)MannequinBrain.GlobalMode + 1) % 4);
                string extra = MannequinBrain.GlobalMode == DummyWalkMode.Attack ? " (they shoot back!)" : "";
                Hud.Instance?.Notify("Dummies: " + MannequinBrain.GlobalMode + extra, NoticeKind.Info);
                UpdateStateLabel();
            }
        }

        void UpdateStateLabel() => Hud.Instance?.SetState($"seed #{RunSeed.Current}");
    }
}
