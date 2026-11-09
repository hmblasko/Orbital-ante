using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Arena
{
    /// <summary>Builds the flat playtest arena: floor, walls, cover, a ramp and lighting.</summary>
    public static class TestArenaBuilder
    {
        public const float HalfSize = 30f;

        public static void Build()
        {
            var root = new GameObject("Arena").transform;

            // Lighting: warm key light plus a cool sky so shapes read clearly.
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.94f, 0.84f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            sun.transform.SetParent(root, true);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.45f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.22f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.62f, 0.7f, 0.82f);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 140f;

            // Floor with a 2 m checker so distance and speed are easy to judge.
            var floor = Block("Floor", root, new Vector3(0, -0.5f, 0), new Vector3(HalfSize * 2f + 20f, 1f, HalfSize * 2f + 20f), Color.white);
            floor.GetComponent<MeshRenderer>().sharedMaterial =
                MaterialLibrary.LitTextured(CheckerTexture(), new Vector2((HalfSize * 2f + 20f) / 4f, (HalfSize * 2f + 20f) / 4f));

            var wall = new Color(0.42f, 0.5f, 0.66f);
            var cover = new Color(0.84f, 0.5f, 0.3f);
            var trim = new Color(0.95f, 0.8f, 0.4f);

            // Perimeter walls.
            float h = 4f, t = 1f, L = HalfSize * 2f;
            Block("WallN", root, new Vector3(0, h / 2, HalfSize), new Vector3(L, h, t), wall);
            Block("WallS", root, new Vector3(0, h / 2, -HalfSize), new Vector3(L, h, t), wall);
            Block("WallE", root, new Vector3(HalfSize, h / 2, 0), new Vector3(t, h, L), wall);
            Block("WallW", root, new Vector3(-HalfSize, h / 2, 0), new Vector3(t, h, L), wall);

            // A few interior walls and cover blocks at varied heights (gibs bounce off these).
            Block("Wall_A", root, new Vector3(-9, 1.5f, 6), new Vector3(8, 3, 0.6f), wall);
            Block("Wall_B", root, new Vector3(10, 1.25f, 12), new Vector3(0.6f, 2.5f, 9), wall);
            Block("Wall_C", root, new Vector3(2, 2f, 20), new Vector3(12, 4, 0.6f), wall);
            Block("Cover_1", root, new Vector3(-3, 0.5f, 2), new Vector3(2, 1, 1), cover);
            Block("Cover_2", root, new Vector3(5, 0.6f, 4), new Vector3(1.2f, 1.2f, 1.2f), cover);
            Block("Cover_3", root, new Vector3(-14, 0.75f, 14), new Vector3(3, 1.5f, 1), cover);
            Block("Cover_4", root, new Vector3(16, 0.45f, -6), new Vector3(1.5f, 0.9f, 3), cover);

            // Ramp and a ledge to test falling gibs and steps.
            var ramp = Block("Ramp", root, new Vector3(-18, 1f, -8), new Vector3(4, 0.4f, 9), trim);
            ramp.transform.rotation = Quaternion.Euler(-14f, 0, 0);
            Block("Ledge", root, new Vector3(-18, 1.1f, -1.5f), new Vector3(4, 2.2f, 4), wall);
            for (int i = 0; i < 4; i++)
                Block("Step_" + i, root, new Vector3(18, 0.15f + i * 0.3f, 10 + i * 0.8f), new Vector3(3, 0.3f + i * 0.6f, 0.8f), trim);

            // Spawn pad marker.
            Block("SpawnPad", root, new Vector3(0, 0.01f, -12), new Vector3(3, 0.02f, 3), new Color(0.25f, 0.72f, 0.69f));
        }

        static GameObject Block(string name, Transform parent, Vector3 pos, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Lit(color, 0.2f);
            go.isStatic = true;
            return go;
        }

        static Texture2D CheckerTexture()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "ArenaChecker" };
            var a = new Color(0.58f, 0.6f, 0.64f);
            var b = new Color(0.48f, 0.5f, 0.55f);
            var line = new Color(0.36f, 0.38f, 0.42f);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool cell = (x < size / 2) ^ (y < size / 2);
                bool edge = x % (size / 2) < 2 || y % (size / 2) < 2;
                px[y * size + x] = edge ? line : cell ? a : b;
            }
            tex.SetPixels(px);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4;
            tex.Apply(true);
            return tex;
        }
    }
}
