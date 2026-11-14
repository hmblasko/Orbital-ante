using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Look;
using OrbitalAnte.Planets;
using UnityEngine;

namespace OrbitalAnte.Bases
{
    /// <summary>
    /// Stands a base's buildings on its floor: the Blender-made model for the site's design
    /// (<see cref="BaseKinds"/>), its lamps and windows shining, and one solid shape to walk into.
    ///
    /// <see cref="PlanetBases"/> decides where bases are and builds the foundation; this is handed the site and
    /// the object to build under. Every mesh and material used here is shared and kept for the whole run, so a
    /// base can be put away and built again for nothing: never destroy them.
    /// </summary>
    public static class BaseView
    {
        /// <summary>How brightly lamps and windows shine (above 1 so they bloom).</summary>
        const float Glow = 2f;

        static readonly Dictionary<string, Mesh> Solids = new Dictionary<string, Mesh>();

        /// <summary>Runs after <see cref="PlanetBases.ResetStatics"/> (which the game calls as its statics are cleared) and before any scene.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            foreach (Mesh mesh in Solids.Values)
            {
                if (mesh != null) Object.Destroy(mesh);
            }
            Solids.Clear();
            PlanetBases.BuildView = Build;
            PlanetBases.Kinds = BaseKinds.Counts;
        }

        /// <summary>
        /// Reads every base model now (and makes its solid shape), so the cost falls while a run is being got
        /// ready and not on the frame a base first comes into range. Safe to call more than once.
        /// </summary>
        public static void Warm()
        {
            BaseKind[] all = BaseKinds.All();
            for (int i = 0; i < all.Length; i++)
            {
                if (ModelLibrary.Find(all[i].Model) != null) Solid(all[i].Model);
            }
        }

        /// <summary>Builds the buildings of a site under its parent. False (and nothing built) if the model is missing.</summary>
        public static bool Build(BaseSite site, Transform parent)
        {
            BaseKind kind = BaseKinds.Of(site);
            if (ModelLibrary.Find(kind.Model) == null) return false;

            var root = new GameObject(kind.Name);
            root.layer = Layers.Default;
            root.transform.SetParent(parent, false);
            root.transform.localRotation = Quaternion.Euler(0f, 90f * BaseKinds.QuarterTurns(site.Seed), 0f);

            Material paint = ModelLibrary.SharedMaterial();
            for (int i = 0; i < BaseKinds.Drawn.Length; i++) Add(root.transform, kind.Model, BaseKinds.Drawn[i], paint);
            Material shine = PlanetMaterials.VertexColour(Color.white, 0f, Glow, false);
            for (int i = 0; i < BaseKinds.Lit.Length; i++) Add(root.transform, kind.Model, BaseKinds.Lit[i], shine);

            Mesh solid = Solid(kind.Model);
            if (solid != null)
            {
                var hit = new GameObject(BaseKinds.Hit);
                hit.layer = Layers.Default;
                hit.transform.SetParent(root.transform, false);
                hit.AddComponent<MeshCollider>().sharedMesh = solid;
            }
            return true;
        }

        static void Add(Transform parent, string model, string piece, Material material)
        {
            Mesh mesh = ModelLibrary.PartMesh(model, piece);
            if (mesh == null) return;
            var go = new GameObject(piece);
            go.layer = Layers.Default;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            // Like everything else on a world: lit from the sky globals, no Unity shadows.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>The model's solid piece as a mesh physics can read (the drawn meshes are not kept readable).</summary>
        static Mesh Solid(string model)
        {
            if (Solids.TryGetValue(model, out Mesh cached) && cached != null) return cached;
            ModelFile file = ModelLibrary.Find(model);
            ModelPart part = file != null ? file.Part(BaseKinds.Hit) : null;
            if (part == null) return null;
            var vertices = new Vector3[part.VertexCount];
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = new Vector3(part.Positions[i * 3], part.Positions[i * 3 + 1], part.Positions[i * 3 + 2]);
            var mesh = new Mesh { name = model + " Hit" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(part.Triangles, 0);
            mesh.RecalculateBounds();
            Solids[model] = mesh;
            return mesh;
        }

        /// <summary>
        /// The places of one kind of attachment point ("Spawn", "Guard", "Loot") on a site, in the site's own
        /// space (the space of the parent <see cref="PlanetBases"/> builds under: x east, y up, z north), with
        /// the base's turn already applied. Empty if the model is missing.
        /// </summary>
        public static void Places(BaseSite site, string point, List<Vector3> into)
        {
            into.Clear();
            ModelFile file = ModelLibrary.Find(BaseKinds.Of(site).Model);
            if (file == null) return;
            int turns = BaseKinds.QuarterTurns(site.Seed);
            for (int i = 0; i < 64; i++)
            {
                if (!file.TrySocket(point + i, out ModelSocket s)) break;
                BaseKinds.Turned(turns, s.X, s.Z, out float x, out float z);
                into.Add(new Vector3(x, s.Y, z));
            }
        }

        /// <summary>A single named point ("Door", "Pad") on a site, as <see cref="Places"/> gives them. False if the design has none.</summary>
        public static bool TryPlace(BaseSite site, string point, out Vector3 place)
        {
            place = default;
            ModelFile file = ModelLibrary.Find(BaseKinds.Of(site).Model);
            if (file == null || !file.TrySocket(point, out ModelSocket s)) return false;
            BaseKinds.Turned(BaseKinds.QuarterTurns(site.Seed), s.X, s.Z, out float x, out float z);
            place = new Vector3(x, s.Y, z);
            return true;
        }
    }
}
