using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Player;
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
        public TestGun Gun { get; private set; }
        public Camera PlayerCamera { get; private set; }
        public IReadOnlyList<Mannequin> Mannequins => _mannequins;

        int _seed;
        Transform _mannequinRoot;
        readonly List<Mannequin> _mannequins = new List<Mannequin>();
        readonly List<Bounds> _obstacles = new List<Bounds>();

        public void Build(int seed)
        {
            _seed = seed;
            BuildLighting();
            BuildGeometry();
            BuildPlayer();
            SpawnMannequins();
        }

        void Update()
        {
            if (GameSession.Paused || GameInput.Asset == null) return;
            if (GameInput.RespawnDummies.WasPressedThisFrame())
            {
                SpawnMannequins();
            }
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

            var playerGo = new GameObject("Player");
            playerGo.layer = Layers.Player;
            playerGo.transform.SetParent(transform, false);
            playerGo.transform.SetPositionAndRotation(new Vector3(0f, 0.1f, -HalfSize + 5f), Quaternion.identity);

            var controller = playerGo.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 50f;

            var eyeGo = new GameObject("Eye");
            eyeGo.layer = Layers.Player;
            eyeGo.transform.SetParent(playerGo.transform, false);
            eyeGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            PlayerCamera = eyeGo.AddComponent<Camera>();
            PlayerCamera.fieldOfView = config.fieldOfView;
            PlayerCamera.nearClipPlane = 0.05f;
            PlayerCamera.farClipPlane = 500f;
            PlayerCamera.clearFlags = CameraClearFlags.SolidColor;
            PlayerCamera.backgroundColor = new Color(0.56f, 0.71f, 0.88f);
            eyeGo.AddComponent<AudioListener>();

            Player = playerGo.AddComponent<FpsController>();
            Player.Initialize(config, PlayerCamera);

            Gun = eyeGo.AddComponent<TestGun>();
            Gun.Initialize(config, PlayerCamera);
        }

        // ------------------------------------------------------------------ mannequins

        /// <summary>Removes every mannequin and loose part, then spawns a fresh set from the seed.</summary>
        public void SpawnMannequins()
        {
            if (_mannequinRoot != null)
            {
                _mannequinRoot.gameObject.SetActive(false);
                Destroy(_mannequinRoot.gameObject);
            }
            _mannequins.Clear();

            _mannequinRoot = new GameObject("Mannequins").transform;
            _mannequinRoot.SetParent(transform, false);

            MannequinConfig config = MannequinConfig.Load();
            var rng = new System.Random(_seed);
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
                _mannequins.Add(mannequin);
            }
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
