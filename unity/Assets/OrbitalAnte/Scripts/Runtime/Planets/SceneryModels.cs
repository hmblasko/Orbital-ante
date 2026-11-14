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

        public static void ResetStatics()
        {
            foreach (Mesh mesh in Solids.Values)
            {
                if (mesh != null) Object.Destroy(mesh);
            }
            Solids.Clear();
        }

        /// <summary>The mesh to draw for a kind and variant, or null if it has no model (or the file is missing).</summary>
        public static Mesh Drawn(FeatureKind kind, int variant)
        {
            string model = SceneryKinds.ModelOf(kind);
            return model == null ? null : ModelLibrary.PartMesh(model, SceneryKinds.Piece(kind, variant));
        }

        /// <summary>The trunk of a tree. False if this kind has none or the model does not say.</summary>
        public static bool TryTrunk(FeatureKind kind, int variant, out Trunk trunk)
        {
            trunk = default;
            if (SceneryKinds.SolidOf(kind) != ScenerySolid.Trunk) return false;
            ModelFile file = ModelLibrary.Find(SceneryKinds.ModelOf(kind));
            if (file == null) return false;
            string piece = SceneryKinds.Piece(kind, variant);
            if (!file.TrySocket(piece + "A", out ModelSocket a) || !file.TrySocket(piece + "B", out ModelSocket b) ||
                !file.TrySocket(piece + "R", out ModelSocket r)) return false;
            trunk = new Trunk { A = new Vector3(a.X, a.Y, a.Z), B = new Vector3(b.X, b.Y, b.Z), Radius = Mathf.Max(0.005f, r.X) };
            return true;
        }

        /// <summary>
        /// The simple closed shape that makes a rock solid, or null. Kept readable (the drawn meshes are not), since
        /// physics has to read it.
        /// </summary>
        public static Mesh Solid(FeatureKind kind, int variant)
        {
            if (SceneryKinds.SolidOf(kind) != ScenerySolid.Shape) return null;
            string piece = SceneryKinds.Piece(kind, variant) + "Hit";
            if (Solids.TryGetValue(piece, out Mesh cached) && cached != null) return cached;
            ModelFile file = ModelLibrary.Find(SceneryKinds.ModelOf(kind));
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
