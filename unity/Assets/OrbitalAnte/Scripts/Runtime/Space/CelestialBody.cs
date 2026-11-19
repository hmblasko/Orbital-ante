using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using UnityEngine;

namespace OrbitalAnte.Space
{
    /// <summary>
    /// One body in the world: its surface to look at and a solid ball to land on. It does not move itself;
    /// <see cref="SolarSystem"/> places every body each physics step.
    /// </summary>
    public sealed class CelestialBody : MonoBehaviour
    {
        static readonly Dictionary<Color32, Material> PlainMaterials = new Dictionary<Color32, Material>();

        public int Index { get; private set; }
        public BodySpec Spec { get; private set; }
        public Rigidbody Body { get; private set; }
        public Color MapColor { get; private set; }
        /// <summary>The solid ball. Bodies with real ground shrink it to sit under the ground as a safety net.</summary>
        public SphereCollider Ball { get; private set; }
        /// <summary>The smooth ball wearing the body's picture: what you see from far away.</summary>
        public MeshRenderer FarLook { get; private set; }

        public static void ResetStatics() => PlainMaterials.Clear();

        /// <summary>Gives the far-away ball a new picture (painted in the background from the body's real ground).</summary>
        public void SetPicture(Texture2D picture)
        {
            if (FarLook == null || picture == null) return;
            Material material = FarLook.sharedMaterial;
            PlanetMaterials.SetColor(material, Color.white);
            PlanetMaterials.SetPicture(material, picture);
        }

        /// <summary>One shared material per colour, for asteroids and ring bands.</summary>
        static Material Plain(Color color, bool twoSided)
        {
            if (!PlanetMaterials.OwnBody) return Mat.Lit(color, twoSided ? 0.1f : 0.05f);
            Color32 key = color;
            key.a = (byte)(twoSided ? 1 : 0);
            if (PlainMaterials.TryGetValue(key, out Material cached) && cached != null) return cached;
            Material material = PlanetMaterials.Body(twoSided ? "Ring" : "Rock", null, color, 0f, 1f, twoSided);
            PlainMaterials[key] = material;
            return material;
        }

        MeshFilter _surface;
        int _fullDetail;
        int _detail;

        /// <summary>
        /// Uses a simpler ball when the body is small on screen. A planet that is seven pixels wide does not need
        /// thirty-six thousand triangles. <paramref name="pixelRadius"/> is how big it looks, in pixels.
        /// </summary>
        public void SetDetailFor(float pixelRadius)
        {
            if (_surface == null) return;
            int wanted = pixelRadius > 140f ? _fullDetail : pixelRadius > 24f ? Mathf.Min(_fullDetail, 64) : 24;
            if (wanted == _detail) return;
            _detail = wanted;
            _surface.sharedMesh = SpaceMeshes.Sphere(wanted);
        }

        /// <param name="plainStart">
        /// True for a body whose picture will be painted in the background from its real ground: it starts as a
        /// plain-coloured ball and the picture is put on when it is ready, so starting a run never waits for it.
        /// </param>
        public static CelestialBody Create(Transform parent, int index, BodySpec spec, bool plainStart = false)
        {
            var go = new GameObject(spec.id);
            go.transform.SetParent(parent, false);
            go.layer = Layers.Default;
            var body = go.AddComponent<CelestialBody>();
            body.Index = index;
            body.Spec = spec;
            body.MapColor = SpaceConvert.ParseColor(spec.colorA, Color.gray);

            // Moved by the solar system, never by collisions: nothing a ship does will knock a planet off course.
            body.Body = go.AddComponent<Rigidbody>();
            body.Body.isKinematic = true;
            body.Body.useGravity = false;
            body.Body.interpolation = RigidbodyInterpolation.Interpolate;

            float radius = (float)spec.radius;
            body.Ball = go.AddComponent<SphereCollider>();
            body.Ball.radius = spec.kind == BodyKind.Asteroid ? radius * 0.9f : radius;

            var surface = new GameObject("Surface");
            surface.transform.SetParent(go.transform, false);
            var filter = surface.AddComponent<MeshFilter>();
            var renderer = surface.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = spec.kind != BodyKind.Star;

            if (spec.kind == BodyKind.Asteroid)
            {
                filter.sharedMesh = SpaceMeshes.Rock(index);
                // Squash each rock a little differently so no two look alike.
                var rng = new SeededRandom(index * 7919 + 13);
                surface.transform.localScale = new Vector3(rng.Range(0.8f, 1.15f), rng.Range(0.7f, 1.05f), rng.Range(0.8f, 1.15f)) * radius;
                surface.transform.localRotation = Quaternion.Euler(rng.Range(0f, 360f), rng.Range(0f, 360f), rng.Range(0f, 360f));
                renderer.sharedMaterial = Plain(body.MapColor, false);
            }
            else
            {
                body._surface = filter;
                body._fullDetail = spec.radius >= 800 ? 192 : spec.radius >= 200 ? 128 : 64;
                body._detail = body._fullDetail;
                filter.sharedMesh = SpaceMeshes.Sphere(body._fullDetail);
                surface.transform.localScale = Vector3.one * radius;
                body.FarLook = renderer;
                bool star = spec.kind == BodyKind.Star;
                Texture2D texture = plainStart ? null : PlanetTextures.Build(spec, spec.kind == BodyKind.Moon ? 192 : 384);
                // The Sun shines by itself and still shows, paler, through a daytime sky.
                renderer.sharedMaterial = PlanetMaterials.Body(spec.id, texture, plainStart ? body.MapColor : Color.white, star ? 1f : 0f, star ? 0.35f : 1f,
                    false, star ? OrbitalAnte.Look.LookConfig.Load().sunBrightness : 1f);
            }

            if (spec.ringOuterRadius > spec.ringInnerRadius && spec.ringInnerRadius > 0) body.BuildRings();
            return body;
        }

        /// <summary>
        /// Rings as a handful of solid bands with gaps between them. Solid rather than see-through because it is
        /// cheap, cannot go wrong, and reads clearly from far away.
        /// </summary>
        void BuildRings()
        {
            float inner = (float)Spec.ringInnerRadius;
            float outer = (float)Spec.ringOuterRadius;
            // Fractions of the ring's width: where each band starts and ends, and how bright it is.
            float[,] bands =
            {
                { 0.00f, 0.16f, 0.55f },
                { 0.19f, 0.52f, 0.95f },
                { 0.54f, 0.62f, 0.75f },
                { 0.70f, 0.92f, 0.85f },
                { 0.95f, 1.00f, 0.60f },
            };
            Color tint = SpaceConvert.ParseColor(Spec.colorA, Color.white);
            for (int i = 0; i < bands.GetLength(0); i++)
            {
                var band = new GameObject("Ring" + (i + 1));
                band.transform.SetParent(transform, false);
                band.AddComponent<MeshFilter>().sharedMesh = SpaceMeshes.RingBand(
                    Mathf.Lerp(inner, outer, bands[i, 0]), Mathf.Lerp(inner, outer, bands[i, 1]));
                var renderer = band.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                Color c = tint * bands[i, 2];
                c.a = 1f;
                renderer.sharedMaterial = Plain(c, true);
            }
        }
    }
}
