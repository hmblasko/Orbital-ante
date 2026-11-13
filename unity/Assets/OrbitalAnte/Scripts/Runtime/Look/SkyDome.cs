using System.Threading.Tasks;
using OrbitalAnte.Planets;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Look
{
    /// <summary>
    /// The sky: a big ball round the viewer, drawn before everything else. It shows the gradient sky of whatever
    /// air you are in, the glow round the Sun, and the picture of deep space behind the stars. The picture is
    /// painted on a background thread when a run starts, so the sky is plain black for a moment and then fills in.
    /// </summary>
    public sealed class SkyDome : MonoBehaviour
    {
        const int PictureWidth = 512;
        const int PictureHeight = 256;
        /// <summary>The same picture every run, like the stars: the sky is the one thing that never moves.</summary>
        const uint PictureSeed = 4104;

        Material _material;
        Texture2D _picture;
        Task<byte[]> _painting;

        /// <summary>
        /// Makes the sky as a child of <paramref name="parent"/>. Returns null if the sky shader cannot be used on
        /// this machine (the camera's plain background colour is the stand-in). <paramref name="radius"/> only has
        /// to be inside the camera's far limit: the ball is drawn behind everything whatever its size.
        /// </summary>
        public static SkyDome Build(Transform parent, float radius, bool deepSpace)
        {
            LookConfig config = LookConfig.Load();
            Material material = PlanetMaterials.Sky(config.spaceBackdrop);
            if (material == null) return null;

            var go = new GameObject("SkyDome");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * radius;
            go.AddComponent<MeshFilter>().sharedMesh = SpaceMeshes.Sphere(32);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var dome = go.AddComponent<SkyDome>();
            dome._material = material;
            if (deepSpace && config.spaceBackdrop > 0.001f)
            {
                dome._painting = Task.Run(() => SpaceBackdrop.Paint(PictureWidth, PictureHeight, PictureSeed));
            }
            return dome;
        }

        void Update()
        {
            if (_painting == null || !_painting.IsCompleted) return;
            Task<byte[]> done = _painting;
            _painting = null;
            if (done.IsFaulted || done.IsCanceled)
            {
                Debug.LogWarning("Orbital Ante: the picture of deep space could not be painted, so space stays plain black. " + done.Exception);
                return;
            }
            _picture = new Texture2D(PictureWidth, PictureHeight, TextureFormat.RGBA32, false)
            {
                name = "DeepSpace",
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            _picture.SetPixelData(done.Result, 0);
            _picture.Apply(false, true);
            PlanetMaterials.SetBackdrop(_material, _picture);
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            if (_picture != null) Destroy(_picture);
        }
    }
}
