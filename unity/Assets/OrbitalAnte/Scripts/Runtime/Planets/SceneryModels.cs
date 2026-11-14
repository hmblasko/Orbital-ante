using System.Collections.Generic;
using OrbitalAnte.Look;
using UnityEngine;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// Earth's Blender-made scenery as Unity meshes: what is drawn, and what is solid. Everything is shared and
    /// kept for the whole run; nothing here may be destroyed by its users. If a model file is missing, callers
    /// get null and fall back to the shapes worked out in code (<see cref="FeatureShapes"/>).
    /// </summary>
    public static class SceneryModels
    {
        /// <summary>The trunk of a tree in the model's own space (1 unit tall): a capsule from A to B.</summary>
        public struct Trunk
        {
            public Vector3 A, B;
            public float Radius;
        }

        static readonly Dictionary<string, Mesh> Solids = new Dictionary<string, Mesh>();

        /// <summary>What was found for one kind and variant, looked up once and kept: putting a tree out must cost no searching and make no garbage.</summary>
        sealed class Slot
        {
            public bool HasModel, HasTrunk;
            public Mesh Drawn, Solid;
            public Trunk Trunk;
        }

        static Slot[] _slots;
        static bool _warned;

        public static void ResetStatics()
        {
            foreach (Mesh mesh in Solids.Values)
            {
                if (mesh != null) Object.Destroy(mesh);
            }
            Solids.Clear();
            _slots = null;
            _warned = false;
        }

        /// <summary>
        /// Reads the scenery files now (a few milliseconds each on a desktop, more on a Deck), so the cost falls
        /// while a world is being got ready and not on the frame the first tree comes into view.
        /// </summary>
        public static void Warm()
        {
            ModelLibrary.Find(SceneryKinds.Trees);
            ModelLibrary.Find(SceneryKinds.Plants);
            ModelLibrary.Find(SceneryKinds.Rocks);
        }

        static Slot Look(FeatureKind kind, int variant)
        {
            int v = ((variant % FeatureShapes.Variants) + FeatureShapes.Variants) % FeatureShapes.Variants;
            int index = (int)kind * FeatureShapes.Variants + v;
            if (_slots == null) _slots = new Slot[System.Enum.GetValues(typeof(FeatureKind)).Length * FeatureShapes.Variants];
            if (index < 0 || index >= _slots.Length) return null;
            Slot slot = _slots[index];
            // "== null" is also true for a mesh Unity has destroyed (the library was emptied): look again then.
            if (slot != null && (!slot.HasModel || slot.Drawn != null)) return slot;

            slot = new Slot();
            _slots[index] = slot;
            string model = SceneryKinds.ModelOf(kind);
            if (model == null) return slot;
            string piece = SceneryKinds.Piece(kind, v);
            slot.Drawn = ModelLibrary.PartMesh(model, piece);
            slot.HasModel = slot.Drawn != null;
            if (!slot.HasModel) return slot;

            ModelFile file = ModelLibrary.Find(model);
            ScenerySolid solid = SceneryKinds.SolidOf(kind);
            if (solid == ScenerySolid.Trunk)
            {
                slot.HasTrunk = true;
                if (file.TrySocket(piece + "A", out ModelSocket a) && file.TrySocket(piece + "B", out ModelSocket b) && file.TrySocket(piece + "R", out ModelSocket r))
                {
                    slot.Trunk = new Trunk { A = new Vector3(a.X, a.Y, a.Z), B = new Vector3(b.X, b.Y, b.Z), Radius = Mathf.Max(0.005f, r.X) };
                }
                else
                {
                    // Still something to bump into, and a word in the Console, rather than a tree you fall through.
                    slot.Trunk = new Trunk { A = Vector3.zero, B = new Vector3(0f, 0.6f, 0f), Radius = 0.03f };
                    if (!_warned) Debug.LogWarning("Orbital Ante: the scenery model '" + model + "' does not say where the trunk of " + piece + " is. A plain upright trunk is used. Run tools/blender/build_scenery.py again.");
                    _warned = true;
                }
            }
            else if (solid == ScenerySolid.Shape)
            {
                slot.Solid = BuildSolid(file, piece + "Hit");
            }
            return slot;
        }

        /// <summary>The mesh to draw for a kind and variant, or null if it has no model (or the file is missing).</summary>
        public static Mesh Drawn(FeatureKind kind, int variant)
        {
            Slot slot = Look(kind, variant);
            return slot != null ? slot.Drawn : null;
        }

        /// <summary>The trunk of a tree. False if this kind has none or its model is missing.</summary>
        public static bool TryTrunk(FeatureKind kind, int variant, out Trunk trunk)
        {
            Slot slot = Look(kind, variant);
            trunk = slot != null ? slot.Trunk : default;
            return slot != null && slot.HasTrunk;
        }

        /// <summary>
        /// The simple closed shape that makes a rock solid, or null. Kept readable (the drawn meshes are not), since
        /// physics has to read it.
        /// </summary>
        public static Mesh Solid(FeatureKind kind, int variant)
        {
            Slot slot = Look(kind, variant);
            return slot != null ? slot.Solid : null;
        }

        static Mesh BuildSolid(ModelFile file, string piece)
        {
            if (Solids.TryGetValue(piece, out Mesh cached) && cached != null) return cached;
            ModelPart part = file != null ? file.Part(piece) : null;
            if (part == null) return null;
            var vertices = new Vector3[part.VertexCount];
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = new Vector3(part.Positions[i * 3], part.Positions[i * 3 + 1], part.Positions[i * 3 + 2]);
            var mesh = new Mesh { name = piece };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(part.Triangles, 0);
            mesh.RecalculateBounds();
            Solids[piece] = mesh;
            return mesh;
        }
    }
}
