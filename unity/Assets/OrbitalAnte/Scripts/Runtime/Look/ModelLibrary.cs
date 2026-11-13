using System.Collections.Generic;
using OrbitalAnte.Planets;
using OrbitalAnte.Ship;
using UnityEngine;

namespace OrbitalAnte.Look
{
    /// <summary>
    /// The game's own models (made in Blender, kept in Resources/Models as ".bytes" files), read once and shared.
    /// Every copy of a model uses the same meshes and the same material, so twenty dropped rifles cost the
    /// memory of one.
    /// </summary>
    public static class ModelLibrary
    {
        public const string Folder = "Models";
        /// <summary>How much smaller the painted patches are on a hand-held thing than on the ground.</summary>
        public const float PropPaintScale = 14f;
        /// <summary>How brightly a model shows inside a ship's cabin, where sunlight is switched off for it.</summary>
        public const float IndoorGlow = 0.7f;

        sealed class Entry
        {
            public ModelFile File;
            public Mesh[] Meshes;
        }

        static readonly Dictionary<string, Entry> Loaded = new Dictionary<string, Entry>();

        public static void ResetStatics()
        {
            foreach (Entry entry in Loaded.Values)
            {
                if (entry == null || entry.Meshes == null) continue;
                foreach (Mesh mesh in entry.Meshes)
                {
                    if (mesh != null) Object.Destroy(mesh);
                }
            }
            Loaded.Clear();
        }

        /// <summary>The model's data, or null if there is no such model (or its file is damaged; the Console says why).</summary>
        public static ModelFile Find(string id)
        {
            Entry entry = Get(id);
            return entry != null ? entry.File : null;
        }

        /// <summary>
        /// Adds the model's pieces under <paramref name="parent"/>, one child per piece, named after the piece.
        /// Returns false (and adds nothing) if the model is missing.
        /// </summary>
        public static bool Build(string id, Transform parent, Dictionary<string, GameObject> pieces = null)
        {
            Entry entry = Get(id);
            if (entry == null) return false;
            Material material = Outdoor();
            for (int i = 0; i < entry.File.Parts.Length; i++)
            {
                var go = new GameObject(entry.File.Parts[i].Name);
                go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = entry.Meshes[i];
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                if (pieces != null) pieces[entry.File.Parts[i].Name] = go;
            }
            return true;
        }

        /// <summary>One piece of a model as a shared mesh, or null if the model or the piece is missing. Do not destroy it.</summary>
        public static Mesh PartMesh(string id, string part)
        {
            Entry entry = Get(id);
            if (entry == null) return null;
            for (int i = 0; i < entry.File.Parts.Length; i++)
            {
                if (entry.File.Parts[i].Name == part) return entry.Meshes[i];
            }
            return null;
        }

        static Material Outdoor() => PlanetMaterials.VertexColour(Color.white, 1f, 0f, true, PropPaintScale);

        /// <summary>The one material every library model is drawn with in the open. Shared: never change or destroy it.</summary>
        public static Material SharedMaterial() => Outdoor();

        /// <summary>For inside a hull: no sunlight, an even glow in its place (the cabin's own light is baked, so there is none to borrow).</summary>
        static Material Indoor() => PlanetMaterials.VertexColour(Color.white, 0f, IndoorGlow, true, PropPaintScale);

        /// <summary>
        /// Switches every library model under <paramref name="root"/> between open-air light and the even light of
        /// a ship's cabin. Both are shared materials, so nothing is created and batching is kept.
        /// </summary>
        public static void SetIndoors(GameObject root, bool indoors)
        {
            if (root == null) return;
            Material outdoor = Outdoor();
            Material indoor = Indoor();
            if (outdoor == indoor) return;
            Material from = indoors ? outdoor : indoor;
            Material to = indoors ? indoor : outdoor;
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.sharedMaterial == from) renderer.sharedMaterial = to;
            }
        }

        static Entry Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (Loaded.TryGetValue(id, out Entry cached)) return cached;

            Entry entry = null;
            TextAsset asset = Resources.Load<TextAsset>(Folder + "/" + id);
            if (asset != null)
            {
                ModelFile file = ModelFile.Parse(asset.bytes, out string problem);
                if (file == null)
                {
                    Debug.LogWarning("Orbital Ante: the model '" + id + "' could not be read (" + problem + "). A stand-in shape is used instead.");
                }
                else
                {
                    entry = new Entry { File = file, Meshes = new Mesh[file.Parts.Length] };
                    for (int i = 0; i < file.Parts.Length; i++) entry.Meshes[i] = ToMesh(file.Parts[i], id + " " + file.Parts[i].Name);
                }
                Resources.UnloadAsset(asset);
            }
            // A missing model is remembered too, so it is not looked for again every time a gun is made.
            Loaded[id] = entry;
            return entry;
        }

        public static Mesh ToMesh(ModelPart part, string name)
        {
            int count = part.VertexCount;
            var vertices = new Vector3[count];
            var normals = new Vector3[count];
            var colors = new Color[count];
            bool linear = ShipMeshes.LinearColours;
            for (int i = 0; i < count; i++)
            {
                int o = i * 3;
                vertices[i] = new Vector3(part.Positions[o], part.Positions[o + 1], part.Positions[o + 2]);
                normals[i] = new Vector3(part.Normals[o], part.Normals[o + 1], part.Normals[o + 2]);
                float r = part.Colors[o], g = part.Colors[o + 1], b = part.Colors[o + 2];
                if (linear) { r = LightBake.ToLinear(r); g = LightBake.ToLinear(g); b = LightBake.ToLinear(b); }
                colors[i] = new Color(r, g, b, 1f);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(part.Triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
