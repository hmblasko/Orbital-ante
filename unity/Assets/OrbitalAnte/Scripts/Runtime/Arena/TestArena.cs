using System.Collections.Generic;
using OrbitalAnte.AI;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Gore;
using OrbitalAnte.Player;
using OrbitalAnte.Weapons;
using UnityEngine;

namespace OrbitalAnte.Arena
{
    /// <summary>
    /// The simple combat test arena: flat ground, a few walls, the player and a handful of wandering mannequins.
    /// Everything is built in code from the run seed, so restarting with the same seed gives the same layout.
    /// </summary>
    public sealed class TestArena : MonoBehaviour
    {
        const float HalfSize = 30f;
        const float WallHeight = 4f;
        const int MannequinCount = 8;

        public FpsController Player { get; private set; }
        public PlayerWeapons Weapons { get; private set; }
        public PlayerHealth Health { get; private set; }
        public Camera PlayerCamera { get; private set; }
        public IReadOnlyList<Mannequin> Mannequins => _mannequins;

        int _seed;
        Transform _mannequinRoot;
        Transform _weaponRoot;
        Transform _eye;
        GibManager _gibs;
        GoreFx _gore;
        WeaponFx _weaponFx;
        PlayerConfig _playerConfig;
        float _respawnAt = -1f;

        static readonly Vector3 PlayerStart = new Vector3(0f, 0.1f, -HalfSize + 5f);
        const float EyeHeight = 1.65f;
        readonly List<Mannequin> _mannequins = new List<Mannequin>();
        readonly List<Bounds> _obstacles = new List<Bounds>();

        public void Build(int seed)
        {
            _seed = seed;
            BuildLighting();
            BuildGeometry();
            BuildGore();
            BuildPlayer();

            CombatWorld.Reset();
            CombatWorld.PlayerEye = _eye;
            CombatWorld.Player = Health;
            CombatWorld.FindCover = FindCover;
            // The arena starts calm so there is time to look around. H (or D-pad down) starts the fight.
            CombatWorld.EnemiesHoldFire = true;

            SpawnMannequins();
            Weapons.Equip(Weapon.Create(WeaponData.Rifle, _weaponRoot, MannequinConfig.Load().oneHandedReloadAllowed));
            PlaceSpareWeapon();
        }

        void OnDestroy()
        {
            if (CombatWorld.PlayerEye == _eye) CombatWorld.Reset();
        }

        void Update()
        {
            if (GameSession.Paused || GameInput.Asset == null) return;

            if (_respawnAt >= 0f)
            {
                UpdateKnockedDown();
                return;
            }

            if (GameInput.RespawnDummies.WasPressedThisFrame())
            {
                SpawnMannequins();
                PlaceSpareWeapon();
            }
            if (GameInput.HoldFire.WasPressedThisFrame())
            {
                CombatWorld.EnemiesHoldFire = !CombatWorld.EnemiesHoldFire;
            }
        }

        // ------------------------------------------------------------------ being shot down

        void OnPlayerDied()
        {
            _respawnAt = Time.time + Mathf.Max(0.5f, _playerConfig.respawnDelay);
            Player.Frozen = true;
        }

        /// <summary>
        /// First person the whole way: the view sinks to the floor, then the player is put back at the start
        /// with a fresh set of mannequins. (A stand-in for real death, which arrives with the ante loop.)
        /// </summary>
        void UpdateKnockedDown()
        {
            Vector3 eye = _eye.localPosition;
            eye.y = Mathf.MoveTowards(eye.y, 0.3f, Time.deltaTime * 3.5f);
            _eye.localPosition = eye;

            if (Time.time < _respawnAt) return;
            _respawnAt = -1f;
            _eye.localPosition = new Vector3(0f, EyeHeight, 0f);
            Player.Teleport(PlayerStart, 0f);
            Player.Frozen = false;
            Health.Revive();
            CombatWorld.EnemiesHoldFire = true;
            SpawnMannequins();
            PlaceSpareWeapon();
        }

        // ------------------------------------------------------------------ cover

        /// <summary>A spot behind the nearest piece of cover, on the far side from the threat.</summary>
        Vector3? FindCover(Vector3 from, Vector3 threat)
        {
            Vector3? best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _obstacles.Count; i++)
            {
                Bounds b = _obstacles[i];
                // Low walls and crates do not hide a standing character well, but they are better than nothing.
                Vector3 away = b.center - threat;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) continue;
                away.Normalize();
                // Far enough behind the obstacle to clear its thickest side.
                float reach = Mathf.Abs(away.x) * b.extents.x + Mathf.Abs(away.z) * b.extents.z + 0.9f;
                Vector3 spot = new Vector3(b.center.x, 0f, b.center.z) + away * reach;
                if (Mathf.Abs(spot.x) > HalfSize - 1f || Mathf.Abs(spot.z) > HalfSize - 1f) continue;

                float sqr = (spot - from).sqrMagnitude;
                // Prefer tall cover by treating low cover as if it were further away.
                if (b.size.y < 1.6f) sqr += 8f * 8f;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = spot;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ world

        void BuildLighting()
        {
            var lightGo = new GameObject("Sun");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            var sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.66f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.50f, 0.50f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.26f, 0.24f);
            RenderSettings.fog = false;
        }

        void BuildGeometry()
        {
            _obstacles.Clear();

            // Ground with a 2 m checker so speed and distance are easy to judge.
            Material ground = new Material(Mat.Lit(new Color(0.74f, 0.70f, 0.62f), 0.1f)) { name = "ArenaGround" };
            ground.mainTexture = MakeChecker(new Color(0.80f, 0.76f, 0.68f), new Color(0.66f, 0.62f, 0.55f));
            ground.mainTextureScale = new Vector2(HalfSize, HalfSize);
            Box("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(HalfSize * 2f, 1f, HalfSize * 2f), ground, false);

            Material wall = Mat.Lit(new Color(0.42f, 0.47f, 0.55f), 0.15f);
            float t = 1f;
            Box("WallNorth", new Vector3(0f, WallHeight * 0.5f, HalfSize + t * 0.5f), new Vector3(HalfSize * 2f + t * 2f, WallHeight, t), wall, false);
            Box("WallSouth", new Vector3(0f, WallHeight * 0.5f, -HalfSize - t * 0.5f), new Vector3(HalfSize * 2f + t * 2f, WallHeight, t), wall, false);
            Box("WallEast", new Vector3(HalfSize + t * 0.5f, WallHeight * 0.5f, 0f), new Vector3(t, WallHeight, HalfSize * 2f), wall, false);
            Box("WallWest", new Vector3(-HalfSize - t * 0.5f, WallHeight * 0.5f, 0f), new Vector3(t, WallHeight, HalfSize * 2f), wall, false);

            // A few pieces of cover: tall walls to bounce gibs off, low walls to shoot over.
            Material cover = Mat.Lit(new Color(0.80f, 0.36f, 0.20f), 0.15f);
            Material coverAlt = Mat.Lit(new Color(0.24f, 0.56f, 0.58f), 0.15f);
            Box("CoverTallA", new Vector3(-9f, 1.5f, 6f), new Vector3(8f, 3f, 0.6f), cover, true);
            Box("CoverTallB", new Vector3(11f, 1.5f, -2f), new Vector3(0.6f, 3f, 9f), coverAlt, true);
            Box("CoverLowA", new Vector3(3f, 0.55f, 12f), new Vector3(6f, 1.1f, 0.6f), coverAlt, true);
            Box("CoverLowB", new Vector3(-13f, 0.55f, -8f), new Vector3(0.6f, 1.1f, 6f), cover, true);
            Box("Crate", new Vector3(0f, 0.75f, 2f), new Vector3(1.5f, 1.5f, 1.5f), cover, true);
        }

        void Box(string name, Vector3 centre, Vector3 size, Material material, bool isObstacle)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = Layers.Default;
            go.isStatic = true;
            go.transform.SetParent(transform, false);
            go.transform.position = centre;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (isObstacle) _obstacles.Add(new Bounds(centre, size));
        }

        static Texture2D MakeChecker(Color a, Color b)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "ArenaChecker",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool dark = (x < size / 2) ^ (y < size / 2);
                    pixels[y * size + x] = dark ? b : a;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            return tex;
        }

        // ------------------------------------------------------------------ player

        void BuildPlayer()
        {
            PlayerConfig config = PlayerConfig.Load();
            _playerConfig = config;

            var playerGo = new GameObject("Player");
            playerGo.layer = Layers.Player;
            playerGo.transform.SetParent(transform, false);
            playerGo.transform.SetPositionAndRotation(PlayerStart, Quaternion.identity);

            var controller = playerGo.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 50f;

            var eyeGo = new GameObject("Eye");
            eyeGo.layer = Layers.Player;
            eyeGo.transform.SetParent(playerGo.transform, false);
            eyeGo.transform.localPosition = new Vector3(0f, EyeHeight, 0f);
            _eye = eyeGo.transform;
            PlayerCamera = eyeGo.AddComponent<Camera>();
            PlayerCamera.fieldOfView = config.fieldOfView;
            PlayerCamera.nearClipPlane = 0.05f;
            PlayerCamera.farClipPlane = 500f;
            PlayerCamera.clearFlags = CameraClearFlags.SolidColor;
            PlayerCamera.backgroundColor = new Color(0.56f, 0.71f, 0.88f);
            eyeGo.AddComponent<AudioListener>();

            Player = playerGo.AddComponent<FpsController>();
            Player.Initialize(config, PlayerCamera);
            Player.Shake = eyeGo.AddComponent<CameraShake>();

            Health = playerGo.AddComponent<PlayerHealth>();
            Health.Initialize(config);
            Health.Died += OnPlayerDied;

            Weapons = eyeGo.AddComponent<PlayerWeapons>();
            // Dropped guns are parented to the arena's weapon folder, which is (re)made with the mannequins.
            Weapons.Initialize(config, PlayerCamera, Player, Health, null);
        }

        // ------------------------------------------------------------------ gore

        /// <summary>The pooled loose chunks and the blood, splats and pop sounds that go with them.</summary>
        void BuildGore()
        {
            GoreConfig gore = GoreConfig.Load();
            _gibs = GibManager.Create(transform, gore);
            _gore = GoreFx.Create(transform, gore);
            _weaponFx = WeaponFx.Create(transform);
        }

        // ------------------------------------------------------------------ mannequins

        /// <summary>Removes every mannequin and loose part, then spawns a fresh set from the seed.</summary>
        public void SpawnMannequins()
        {
            // Loose chunks live in the pool, not under the mannequins, so clear them and the mess first.
            if (_gibs != null) _gibs.ReleaseAll();
            if (_gore != null) _gore.Clear();
            if (_weaponFx != null) _weaponFx.Clear();

            // Guns lying around go too. The one in the player's hands is not in this folder, so it stays.
            if (_weaponRoot != null)
            {
                _weaponRoot.gameObject.SetActive(false);
                Destroy(_weaponRoot.gameObject);
            }
            _weaponRoot = new GameObject("Weapons").transform;
            _weaponRoot.SetParent(transform, false);
            CombatWorld.DropParent = _weaponRoot;
            if (Weapons != null) Weapons.SetDropParent(_weaponRoot);

            if (_mannequinRoot != null)
            {
                _mannequinRoot.gameObject.SetActive(false);
                Destroy(_mannequinRoot.gameObject);
            }
            _mannequins.Clear();

            _mannequinRoot = new GameObject("Mannequins").transform;
            _mannequinRoot.SetParent(transform, false);

            MannequinConfig config = MannequinConfig.Load();
            // Its own named stream: other seeded systems can come and go without moving these mannequins.
            SeededRandom rng = SeededRandom.ForStream(_seed, "arena.mannequins");
            float inner = HalfSize - 4f;
            var wanderArea = new Bounds(Vector3.zero, new Vector3(inner * 2f, 0.1f, inner * 2f));

            for (int i = 0; i < MannequinCount; i++)
            {
                Vector3 position = Vector3.zero;
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    float x = Mathf.Lerp(-inner, inner, (float)rng.NextDouble());
                    // Keep the first spawns out of the player's lap: nothing in the southern strip.
                    float z = Mathf.Lerp(-inner + 12f, inner, (float)rng.NextDouble());
                    position = new Vector3(x, 0f, z);
                    if (IsClear(position, 1.2f)) break;
                }
                Quaternion rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

                Mannequin mannequin = MannequinFactory.Create(position, rotation, _mannequinRoot, config, "Mannequin " + (i + 1));
                var locomotion = mannequin.GetComponent<MannequinLocomotion>();
                locomotion.SetWander(wanderArea, rng.Next());

                // Everyone starts armed: mostly rifles, a couple of pistols.
                var combat = mannequin.GetComponent<MannequinCombat>();
                combat.SetSeed(rng.Next());
                WeaponData data = (i % 4 == 3) ? WeaponData.Pistol : WeaponData.Rifle;
                Weapon weapon = Weapon.Create(data, _weaponRoot, config.oneHandedReloadAllowed);
                weapon.transform.position = position + Vector3.up * 1.2f;
                combat.TryPickUp(weapon);

                _mannequins.Add(mannequin);
            }
        }

        /// <summary>
        /// Leaves the gun the player is not holding on the ground near the start, so swapping can be tried.
        /// </summary>
        void PlaceSpareWeapon()
        {
            bool holdingPistol = Weapons != null && Weapons.Current != null
                                 && Weapons.Current.Data.placeholderShape == WeaponShape.Pistol;
            WeaponData spare = holdingPistol ? WeaponData.Rifle : WeaponData.Pistol;
            Weapon weapon = Weapon.Create(spare, _weaponRoot, MannequinConfig.Load().oneHandedReloadAllowed);
            weapon.transform.SetPositionAndRotation(PlayerStart + new Vector3(1.6f, 0.4f, 2.5f), Quaternion.Euler(0f, 60f, 90f));
        }

        bool IsClear(Vector3 position, float margin)
        {
            for (int i = 0; i < _obstacles.Count; i++)
            {
                Bounds b = _obstacles[i];
                b.Expand(margin * 2f);
                if (position.x > b.min.x && position.x < b.max.x && position.z > b.min.z && position.z < b.max.z) return false;
            }
            return true;
        }
    }
}
