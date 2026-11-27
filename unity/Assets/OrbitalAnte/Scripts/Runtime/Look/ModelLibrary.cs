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
            /// <summary>Its texture set's open-air material, once asked for (so a body's fifteen pieces ask once).</summary>
            public Material Textured;
        }

        static readonly Dictionary<string, Entry> Loaded = new Dictionary<string, Entry>();

        /// <summary>A texture set's three pictures, read once. Null when any of them is missing.</summary>
        sealed class TextureSet
        {
            public Texture2D Color, Normal, Mask;
        }

        static readonly Dictionary<string, TextureSet> Pictures = new Dictionary<string, TextureSet>();
        /// <summary>Each open-air material with its cabin twin, both ways round, for <see cref="SetIndoors"/>.</summary>
        static readonly Dictionary<Material, Material> IndoorOf = new Dictionary<Material, Material>();
        static readonly Dictionary<Material, Material> OutdoorOf = new Dictionary<Material, Material>();

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
            // Pictures loaded from Resources belong to Unity, which unloads them; only the references are dropped.
            Pictures.Clear();
            IndoorOf.Clear();
            OutdoorOf.Clear();
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
            Material material = MaterialFor(id);
            for (int i = 0; i < entry.File.Parts.Length; i++)
            {
                var go = new GameObject(entry.File.Parts[i].Name);
                go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = entry.Meshes[i];
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = ModelFile.Shines(entry.File.Parts[i].Name) ? Shining() : material;
                // A piece that shines by itself never casts a shadow.
                if (ModelFile.Shines(entry.File.Parts[i].Name)) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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

        /// <summary>
        /// The one material every shining piece (<see cref="ModelFile.Shines"/>) is drawn with by <see cref="Build"/>:
        /// its own colours, brighter than white so it blooms. Shared: never change or destroy it.
        /// </summary>
        static Material Shining() => PlanetMaterials.VertexColour(Color.white, 0f, 2f, false);

        /// <summary>
        /// The material for one piece taken by <see cref="PartMesh"/> and drawn by hand: the shining one for a piece
        /// whose name ends in Glow, the model's own otherwise. Shared: never change or destroy it.
        /// </summary>
        public static Material PieceMaterial(string id, string part) => ModelFile.Shines(part) ? Shining() : MaterialFor(id);

        static Material Outdoor() => PlanetMaterials.Model(1f, 0f, PropPaintScale);

        /// <summary>The one material every untextured library model is drawn with in the open. Shared: never change or destroy it.</summary>
        public static Material SharedMaterial() => Outdoor();

        /// <summary>For inside a hull: no sunlight, an even glow in its place (the cabin's own light is baked, so there is none to borrow).</summary>
        static Material Indoor() => PlanetMaterials.Model(0f, IndoorGlow, PropPaintScale);

        /// <summary>
        /// The material a model's pieces (not its glowing ones) are drawn with in the open: its own texture set's
        /// when it names one and the pictures are there, otherwise the shared one. A model and its far copy name
        /// the same set, so swapping meshes for distance never needs a different material. Shared: never change or
        /// destroy it.
        /// </summary>
        public static Material MaterialFor(string id)
        {
            Entry entry = Get(id);
            if (entry == null || !entry.File.Textured) return Outdoor();
            TextureSet pictures = PicturesOf(entry.File.TextureSet);
            if (pictures == null) return Outdoor();
            // Kept on the model so each piece of each new body is one lookup with no new strings; a new world
            // destroys the materials, which makes the kept one null and it is asked for again.
            if (entry.Textured != null) return entry.Textured;
            Material outdoor = PlanetMaterials.TexturedModel(entry.File.TextureSet, pictures.Color, pictures.Normal, pictures.Mask, 1f, 0f, PropPaintScale);
            if (outdoor != Outdoor() && !IndoorOf.ContainsKey(outdoor))
            {
                // A new world destroys the old materials (PlanetMaterials.Configure); their entries go too.
                ForgetDestroyed(IndoorOf);
                ForgetDestroyed(OutdoorOf);
                Material indoor = PlanetMaterials.TexturedModel(entry.File.TextureSet, pictures.Color, pictures.Normal, pictures.Mask, 0f, IndoorGlow, PropPaintScale);
                IndoorOf[outdoor] = indoor;
                OutdoorOf[indoor] = outdoor;
            }
            if (outdoor != Outdoor()) entry.Textured = outdoor;
            return outdoor;
        }

        static readonly List<Material> Dead = new List<Material>();

        static void ForgetDestroyed(Dictionary<Material, Material> twins)
        {
            Dead.Clear();
            foreach (KeyValuePair<Material, Material> pair in twins)
            {
                if (pair.Key == null || pair.Value == null) Dead.Add(pair.Key);
            }
            foreach (Material m in Dead) twins.Remove(m);
            Dead.Clear();
        }

        static TextureSet PicturesOf(string set)
        {
            if (Pictures.TryGetValue(set, out TextureSet known)) return known;
            var pictures = new TextureSet
            {
                Color = Resources.Load<Texture2D>(ModelFile.TextureFile(set, ModelFile.TextureMap.Color)),
                Normal = Resources.Load<Texture2D>(ModelFile.TextureFile(set, ModelFile.TextureMap.Normal)),
                Mask = Resources.Load<Texture2D>(ModelFile.TextureFile(set, ModelFile.TextureMap.Mask)),
            };
            if (pictures.Color == null || pictures.Normal == null || pictures.Mask == null)
            {
                Debug.LogWarning("Orbital Ante: the texture set '" + set + "' is missing a picture (looked for " + ModelFile.TextureFile(set, ModelFile.TextureMap.Color) +
                                 ", _Normal and _Mask), so models that use it are drawn from their corner colours.");
                pictures = null;
            }
            // A missing set is remembered too, so the warning is given once.
            Pictures[set] = pictures;
            return pictures;
        }

        /// <summary>
        /// Switches every library model under <paramref name="root"/> between open-air light and the even light of
        /// a ship's cabin. Both are shared materials, so nothing is created and batching is kept.
        /// </summary>
        public static void SetIndoors(GameObject root, bool indoors)
        {
            if (root == null) return;
            Material outdoor = Outdoor();
            Material indoor = Indoor();
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material now = renderer.sharedMaterial;
                if (now == null) continue;
                if (outdoor != indoor && now == (indoors ? outdoor : indoor)) renderer.sharedMaterial = indoors ? indoor : outdoor;
                else if (indoors && IndoorOf.TryGetValue(now, out Material twin)) renderer.sharedMaterial = twin;
                else if (!indoors && OutdoorOf.TryGetValue(now, out Material back)) renderer.sharedMaterial = back;
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
            // Its far copy is read at the same moment, so reading a model early (the enemy spawner does, for its
            // whole roster) also spares the hitch of reading the far copy when the first one walks away.
            if (entry != null) Get(ModelFile.FarOf(id));
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
            // What each corner is made of (how smooth, whether bare metal), for models that say. OA_Model.shader
            // reads it; a mesh without it is chalk all over.
            if (part.Finish != null && part.Finish.Length == count * 2)
            {
                var finish = new Vector2[count];
                for (int i = 0; i < count; i++) finish[i] = new Vector2(part.Finish[i * 2], part.Finish[i * 2 + 1]);
                mesh.SetUVs(1, finish);
            }
            // Where each corner sits on the model's texture pictures, in the third pair (the first is the ground
            // shader's "up", the second the finish), and which way the pictures run across the surface, exactly as
            // Blender worked it out when the normal map was baked.
            if (part.TexCoords != null && part.TexCoords.Length == count * 2 && part.Tangents != null && part.Tangents.Length == count * 4)
            {
                var uv = new Vector2[count];
                var tangents = new Vector4[count];
                for (int i = 0; i < count; i++)
                {
                    uv[i] = new Vector2(part.TexCoords[i * 2], part.TexCoords[i * 2 + 1]);
                    tangents[i] = new Vector4(part.Tangents[i * 4], part.Tangents[i * 4 + 1], part.Tangents[i * 4 + 2], part.Tangents[i * 4 + 3]);
                }
                mesh.SetUVs(2, uv);
                mesh.SetTangents(tangents);
            }
            mesh.SetTriangles(part.Triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
