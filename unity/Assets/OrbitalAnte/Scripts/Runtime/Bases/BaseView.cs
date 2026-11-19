using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Look;
using OrbitalAnte.Planets;
using UnityEngine;

namespace OrbitalAnte.Bases
{
    /// <summary>
    /// Stands a base's buildings on its floor: the Blender-made model for the site's design
    /// (<see cref="BaseKinds"/>), structure by structure (<see cref="BaseStructures"/>), each a
    /// <see cref="BaseStructure"/> that can be shot or rammed down, with its lamps and windows shining, its rooms
    /// drawn with their own baked light and its door sliding open as you come. A structure that fell earlier in
    /// the run is built as its ruin.
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
            if (_room != null) Object.Destroy(_room);
            _room = null;
            _paint = _shine = null;
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
                ModelFile file = ModelLibrary.Find(all[i].Model);
                if (file == null) continue;
                Solid(all[i].Model, BaseStructures.GroundHit);
                int count = BaseStructures.Count(file);
                for (int k = 0; k < count; k++)
                {
                    Solid(all[i].Model, BaseStructures.Tag(k) + BaseStructures.Hit);
                    // ready before anything falls, so a collapse does not also build its ruin's shape
                    Solid(all[i].Model, BaseStructures.Tag(k) + BaseStructures.RuinHit);
                }
            }
        }

        /// <summary>Builds the buildings of a site under its parent. False (and nothing built) if the model is missing.</summary>
        public static bool Build(BaseSite site, Transform parent)
        {
            BaseKind kind = BaseKinds.Of(site);
            ModelFile file = ModelLibrary.Find(kind.Model);
            if (file == null) return false;

            var root = new GameObject(kind.Name);
            root.layer = Layers.Default;
            root.transform.SetParent(parent, false);
            root.transform.localRotation = Quaternion.Euler(0f, 90f * BaseKinds.QuarterTurns(site.Seed), 0f);

            // What cannot be broken: the landing circle, spoil heaps, cables.
            Add(root.transform, kind.Model, BaseStructures.Ground, Paint());
            Add(root.transform, kind.Model, BaseStructures.GroundGlow, Shine());
            AddSolid(root.transform, kind.Model, BaseStructures.GroundHit);

            long key = BaseWrecks.Key(site);
            int count = BaseStructures.Count(file);
            for (int i = 0; i < count; i++) BuildStructure(root.transform, file, kind.Model, i, key);
            return true;
        }

        static Material _paint, _shine, _room;
        static Material Paint() => _paint != null ? _paint : (_paint = ModelLibrary.SharedMaterial());
        static Material Shine() => _shine != null ? _shine : (_shine = PlanetMaterials.VertexColour(Color.white, 0f, Glow, false));
        /// <summary>Rooms: their lamps' light is baked into their colours, so they are drawn with no sunlight at all.</summary>
        static Material Room()
        {
            if (_room == null) _room = PlanetMaterials.Baked(RoomBrightness);
            return _room;
        }

        /// <summary>How bright the baked rooms are drawn (1 is as baked).</summary>
        const float RoomBrightness = 1f;

        static void BuildStructure(Transform root, ModelFile file, string model, int index, long site)
        {
            string tag = BaseStructures.Tag(index);
            var go = new GameObject(tag);
            go.layer = Layers.Default;
            go.transform.SetParent(root, false);
            var structure = go.AddComponent<BaseStructure>();
            file.TrySocket(tag + BaseStructures.Mid, out ModelSocket mid);
            bool enterable = BaseStructures.Enterable(file, index);
            ModelPart hitPart = file.Part(tag + BaseStructures.Hit);
            structure.Set(model, index, site, enterable ? "Building" : "Structure", BoundsOf(hitPart), new Vector3(mid.X, mid.Y, mid.Z), enterable);

            if (PlanetBases.Wrecks.IsDown(site, index))
            {
                // Fell earlier this run: a ruin, which nothing can bring down again.
                structure.Health.Take(structure.Health.Left + 1f);
                structure.enabled = false;
                BuildRuin(structure, model, index);
                return;
            }

            structure.AddWhole(Add(go.transform, model, tag, Paint()));
            structure.AddWhole(Add(go.transform, model, tag + BaseStructures.Glow, Shine()));
            structure.AddWhole(AddSolid(go.transform, model, tag + BaseStructures.Hit));
            GameObject inside = Add(go.transform, model, tag + BaseStructures.Inside, Room());
            if (inside != null)
            {
                structure.AddWhole(inside);
                structure.AddInside(inside.GetComponent<Renderer>());
            }
            GameObject door = Add(go.transform, model, tag + BaseStructures.Door, Room());
            if (door != null && file.TrySocket(tag + BaseStructures.DoorShut, out ModelSocket shut) && file.TrySocket(tag + BaseStructures.DoorOpen, out ModelSocket open))
            {
                structure.AddWhole(door);
                // The door is solid where it is (a box round it, moving with it); the room's walls are in the Hit piece.
                Bounds b = door.GetComponent<MeshFilter>().sharedMesh.bounds;
                var box = door.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = Vector3.Max(b.size, new Vector3(0.08f, 0.08f, 0.08f));
                structure.SetDoor(door.transform, new Vector3(shut.X, shut.Y, shut.Z), new Vector3(open.X, open.Y, open.Z));
            }
            if (file.TrySocket(tag + BaseStructures.Air, out ModelSocket air)) structure.SetAir(new Vector3(air.X, air.Y, air.Z));
        }

        /// <summary>What is left standing of a fallen structure: stubs, footings and a heap of rubble, solid where it should be.</summary>
        public static void BuildRuin(BaseStructure structure, string model, int index)
        {
            string tag = BaseStructures.Tag(index);
            Add(structure.transform, model, tag + BaseStructures.Ruin, Paint());
            AddSolid(structure.transform, model, tag + BaseStructures.RuinHit);
        }

        /// <summary>
        /// Lets the pieces of a fallen structure go: each a loose object under the world's gravity, kicked out from
        /// the middle and the way the last blow came from.
        /// </summary>
        public static void Scatter(BaseStructure structure, string model, int index, Vector3 middle, Vector3 push)
        {
            ModelFile file = ModelLibrary.Find(model);
            int bits = BaseStructures.Bits(file, index);
            string tag = BaseStructures.Tag(index) + BaseStructures.Bit;
            Transform home = structure.transform;
            Vector3 up = home.up;
            StructureRules rules = BaseStructure.Rules;
            for (int k = 0; k < bits; k++)
            {
                Mesh mesh = ModelLibrary.PartMesh(model, tag + k);
                if (mesh == null) continue;
                Bounds b = mesh.bounds;
                var go = new GameObject(tag + k);
                go.layer = Layers.Gib;
                // Loose at the top of the scene while it falls, like the ship's wreckage (never under the moving body).
                go.transform.SetPositionAndRotation(home.TransformPoint(b.center), home.rotation);
                var shape = new GameObject("Shape");
                shape.layer = Layers.Gib;
                shape.transform.SetParent(go.transform, false);
                shape.transform.localPosition = -b.center;
                shape.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = shape.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Paint();
                Plain(renderer);

                var box = go.AddComponent<BoxCollider>();
                box.size = Vector3.Max(b.size * 0.9f, new Vector3(0.1f, 0.1f, 0.1f));
                var rb = go.AddComponent<Rigidbody>();
                float volume = Mathf.Max(0.02f, b.size.x * b.size.y * b.size.z);
                rb.mass = Mathf.Clamp(volume * 110f, 15f, 1500f);
                rb.useGravity = false;
                rb.linearDamping = 0.05f;
                rb.angularDamping = 0.3f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                Vector3 outward = go.transform.position - middle;
                outward -= up * Vector3.Dot(outward, up);
                outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : Random.onUnitSphere;
                float kick = rules.fallKick * Random.Range(0.6f, 1.2f);
                rb.linearVelocity = outward * kick + push.normalized * kick * 0.5f + up * Random.Range(0f, 1.2f);
                rb.angularVelocity = Random.insideUnitSphere * 1.6f;
                go.AddComponent<GravityBody>();
                go.AddComponent<LoosePiece>().Begin(home);
            }
        }

        /// <summary>One piece of the model as a drawn object under the parent, or null if the model has no such piece.</summary>
        static GameObject Add(Transform parent, string model, string piece, Material material)
        {
            Mesh mesh = ModelLibrary.PartMesh(model, piece);
            if (mesh == null) return null;
            var go = new GameObject(piece);
            go.layer = Layers.Default;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            Plain(renderer);
            return go;
        }

        static void Plain(MeshRenderer renderer)
        {
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            // Like everything else on a world: lit from the sky globals, no Unity shadows.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static GameObject AddSolid(Transform parent, string model, string piece)
        {
            Mesh solid = Solid(model, piece);
            if (solid == null) return null;
            var hit = new GameObject(piece);
            hit.layer = Layers.Default;
            hit.transform.SetParent(parent, false);
            hit.AddComponent<MeshCollider>().sharedMesh = solid;
            return hit;
        }

        static Bounds BoundsOf(ModelPart part)
        {
            if (part == null || part.VertexCount == 0) return new Bounds(Vector3.zero, Vector3.one);
            var b = new Bounds(new Vector3(part.Positions[0], part.Positions[1], part.Positions[2]), Vector3.zero);
            for (int i = 1; i < part.VertexCount; i++) b.Encapsulate(new Vector3(part.Positions[i * 3], part.Positions[i * 3 + 1], part.Positions[i * 3 + 2]));
            return b;
        }

        /// <summary>A solid piece of the model as a mesh physics can read (the drawn meshes are not kept readable).</summary>
        static Mesh Solid(string model, string piece)
        {
            string key = model + "/" + piece;
            if (Solids.TryGetValue(key, out Mesh cached)) return cached;
            ModelFile file = ModelLibrary.Find(model);
            ModelPart part = file != null ? file.Part(piece) : null;
            if (part == null) { Solids[key] = null; return null; }
            var vertices = new Vector3[part.VertexCount];
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = new Vector3(part.Positions[i * 3], part.Positions[i * 3 + 1], part.Positions[i * 3 + 2]);
            var mesh = new Mesh { name = key };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(part.Triangles, 0);
            mesh.RecalculateBounds();
            Solids[key] = mesh;
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
