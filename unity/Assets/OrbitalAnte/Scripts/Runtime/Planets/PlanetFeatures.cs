using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// Stands rocks, hoodoos, arches, spires, crystals, trees and the rest on a body's ground near the viewer.
    /// Two sources: the fixed placements that belong to formations (a hoodoo field, a boulder-strewn crater),
    /// shown when you are within range; and the loose scatter that belongs to each piece of nearby ground.
    /// Objects are reused instead of created and destroyed, and there is a cap on how many exist at once.
    /// </summary>
    public sealed class PlanetFeatures
    {
        sealed class Item
        {
            public GameObject Go;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public MeshCollider Collider;
            /// <summary>A tree's trunk: one capsule on a child, so it can lean the way the trunk does.</summary>
            public CapsuleCollider Trunk;
        }

        readonly PlanetSurface _surface;
        readonly Transform _root;
        readonly Stack<Item> _spare = new Stack<Item>();
        readonly Dictionary<object, List<Item>> _groups = new Dictionary<object, List<Item>>();
        readonly Stack<List<Item>> _spareLists = new Stack<List<Item>>();
        PlanetTerrain _terrain;
        Vec3d[] _fixedPositions;
        Item[] _fixedItems;
        int _cursor;
        int _spawnsLeft;
        bool _active;

        /// <summary>How many rocks, trees and so on exist right now.</summary>
        public int Count { get; private set; }
        /// <summary>How many were left out because the cap was reached. For the debug readout.</summary>
        public int Refused { get; private set; }

        public PlanetFeatures(PlanetSurface surface, Transform root)
        {
            _surface = surface;
            _root = root;
            _root.gameObject.SetActive(false);
        }

        public void SetTerrain(PlanetTerrain terrain, Vec3d[] fixedPositions)
        {
            _terrain = terrain;
            _fixedPositions = fixedPositions;
            _fixedItems = new Item[fixedPositions.Length];
            // A world with Blender-made scenery reads its files while it is being got ready, not when the first tree shows.
            List<ScatterRule> rules = terrain != null && terrain.Profile != null ? terrain.Profile.scatter : null;
            for (int i = 0; rules != null && i < rules.Count; i++)
            {
                if (rules[i] != null && SceneryKinds.ModelOf(rules[i].feature) != null) { SceneryModels.Warm(); break; }
            }
        }

        Vec3d _clearCentre;
        double _clearRadius;

        /// <summary>
        /// Keeps rocks, trees and the like off a patch of ground (a built base). <paramref name="centre"/> is in
        /// metres from the body's centre. One patch per body is enough for now.
        /// </summary>
        public void KeepClear(Vec3d centre, double radius)
        {
            _clearCentre = centre;
            _clearRadius = radius;
        }

        readonly List<Vec3d> _alsoClear = new List<Vec3d>();
        readonly List<double> _alsoClearRadius = new List<double>();

        /// <summary>One more place nothing natural stands (a base's floor). Call before the ground near it is shown.</summary>
        public void AlsoKeepClear(Vec3d centre, double radius)
        {
            _alsoClear.Add(centre);
            _alsoClearRadius.Add(radius);
        }

        bool Cleared(Vec3d position)
        {
            if (_clearRadius > 0 && (position - _clearCentre).SqrMagnitude < _clearRadius * _clearRadius) return true;
            for (int i = 0; i < _alsoClear.Count; i++)
            {
                double r = _alsoClearRadius[i];
                if ((position - _alsoClear[i]).SqrMagnitude < r * r) return true;
            }
            return false;
        }

        public void SetActive(bool active)
        {
            if (_active == active) return;
            _active = active;
            _root.gameObject.SetActive(active);
            if (active) return;

            // Leaving the body: put everything away and let the objects go.
            foreach (KeyValuePair<object, List<Item>> group in _groups)
            {
                for (int i = 0; i < group.Value.Count; i++) Retire(group.Value[i]);
                group.Value.Clear();
                _spareLists.Push(group.Value);
            }
            _groups.Clear();
            if (_fixedItems != null)
            {
                for (int i = 0; i < _fixedItems.Length; i++)
                {
                    if (_fixedItems[i] == null) continue;
                    Retire(_fixedItems[i]);
                    _fixedItems[i] = null;
                }
            }
            while (_spare.Count > 0) Object.Destroy(_spare.Pop().Go);
            Count = 0;
        }

        public void Dispose()
        {
            // The objects are children of the body and go with it.
            _groups.Clear();
            _spare.Clear();
        }

        /// <summary>Call at the start of each frame: resets the count of things put out this frame.</summary>
        public void BeginFrame() => _spawnsLeft = Mathf.Max(1, _surface.Settings.featuresPerFrame);

        /// <summary>
        /// Puts out the loose scatter that belongs to one piece of ground. Returns false, having done nothing, if
        /// this frame's allowance is already used up (the caller tries again next frame).
        /// </summary>
        public bool ShowGroup(object key, List<Placement> placements, Vec3d[] positions)
        {
            if (!_active) return false;
            if (_groups.ContainsKey(key)) return true;
            if (_spawnsLeft <= 0) return false;
            _spawnsLeft -= placements.Count;
            List<Item> items = _spareLists.Count > 0 ? _spareLists.Pop() : new List<Item>();
            for (int i = 0; i < placements.Count; i++)
            {
                Item item = Spawn(placements[i], positions[i]);
                if (item != null) items.Add(item);
            }
            _groups[key] = items;
            return true;
        }

        public void HideGroup(object key)
        {
            if (!_groups.TryGetValue(key, out List<Item> items)) return;
            for (int i = 0; i < items.Count; i++) Retire(items[i]);
            items.Clear();
            _spareLists.Push(items);
            _groups.Remove(key);
        }

        /// <summary>
        /// Call once a frame. Looks at a slice of the fixed placements each time (all of them a few times a
        /// second), so the cost per frame stays small however many a body has.
        /// </summary>
        public void Tick(Vec3d viewer)
        {
            if (!_active || _fixedPositions == null || _fixedPositions.Length == 0) return;
            double range = _surface.Settings.featureRange;
            double show = range * range, hide = range * 1.15 * range * 1.15;
            int steps = Mathf.Min(_fixedPositions.Length, 160);
            for (int s = 0; s < steps; s++)
            {
                int i = _cursor;
                _cursor = (_cursor + 1) % _fixedPositions.Length;
                double d = (_fixedPositions[i] - viewer).SqrMagnitude;
                if (_fixedItems[i] == null)
                {
                    // Tall landmarks show from further off than small ones.
                    double tall = 1 + _terrain.Placements[i].Scale / 12.0;
                    if (d < show * tall && _spawnsLeft > 0 && !Cleared(_fixedPositions[i]))
                    {
                        _spawnsLeft--;
                        _fixedItems[i] = Spawn(_terrain.Placements[i], _fixedPositions[i]);
                    }
                }
                else if (d > hide * (1 + _terrain.Placements[i].Scale / 12.0))
                {
                    Retire(_fixedItems[i]);
                    _fixedItems[i] = null;
                }
            }
        }

        Item Spawn(in Placement p, Vec3d position)
        {
            if (Cleared(position)) return null;
            if (Count >= _surface.Settings.featureCap)
            {
                Refused++;
                return null;
            }

            Item item;
            if (_spare.Count > 0)
            {
                item = _spare.Pop();
            }
            else
            {
                item = new Item { Go = new GameObject("Feature") };
                item.Go.transform.SetParent(_root, false);
                item.Go.layer = Layers.Default;
                item.Filter = item.Go.AddComponent<MeshFilter>();
                item.Renderer = item.Go.AddComponent<MeshRenderer>();
                item.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                item.Renderer.receiveShadows = false;
            }

            Mesh mesh = FeatureMeshes.Get(p.Kind, p.Variant);
            item.Filter.sharedMesh = mesh;
            item.Renderer.sharedMaterial = PlanetMaterials.VertexColour(new Color(p.R, p.G, p.B, 1f), p.Absolute ? 0f : 1f, GlowOf(p.Kind));

            float scale = (float)p.Scale;
            Vector3 up = SpaceConvert.ToVector3(position.Normalized);
            // Stalagmites and crystals placed inside caves stand on the cave floor, which is not always "down".
            Transform t = item.Go.transform;
            t.localPosition = SpaceConvert.ToVector3(position);
            t.localRotation = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, (float)(p.Yaw * Mathf.Rad2Deg), 0f);
            t.localScale = Vector3.one * scale;

            // What is solid is not always what is drawn. A Blender-made tree is only its trunk (one capsule: the
            // ship lands on these), a Blender-made rock is a simpler closed shape, small plants are nothing at all.
            bool solid = scale >= _surface.Settings.solidFeatureHeight;
            Mesh shape = null;
            bool hasTrunk = false;
            SceneryModels.Trunk trunk = default;
            if (solid)
            {
                if (SceneryModels.Drawn(p.Kind, p.Variant) == null) shape = mesh;
                else if (!SceneryModels.TryTrunk(p.Kind, p.Variant, out trunk)) shape = SceneryModels.Solid(p.Kind, p.Variant);
                else hasTrunk = true;
            }

            if (shape != null)
            {
                if (item.Collider == null) item.Collider = item.Go.AddComponent<MeshCollider>();
                item.Collider.sharedMesh = shape;
                item.Collider.enabled = true;
            }
            else if (item.Collider != null)
            {
                item.Collider.enabled = false;
            }

            if (hasTrunk)
            {
                if (item.Trunk == null)
                {
                    var holder = new GameObject("Trunk");
                    holder.layer = Layers.Default;
                    holder.transform.SetParent(t, false);
                    item.Trunk = holder.AddComponent<CapsuleCollider>();
                    item.Trunk.direction = 1;
                }
                Vector3 along = trunk.B - trunk.A;
                Transform h = item.Trunk.transform;
                h.localPosition = (trunk.A + trunk.B) * 0.5f;
                h.localRotation = along.sqrMagnitude > 1e-8f ? Quaternion.FromToRotation(Vector3.up, along) : Quaternion.identity;
                item.Trunk.radius = trunk.Radius;
                item.Trunk.height = along.magnitude + trunk.Radius * 2f;
                item.Trunk.enabled = true;
            }
            else if (item.Trunk != null)
            {
                item.Trunk.enabled = false;
            }

            item.Go.SetActive(true);
            Count++;
            return item;
        }

        /// <summary>Crystals glow a little, so a crystal cave is not pitch black.</summary>
        static float GlowOf(FeatureKind kind)
        {
            return kind == FeatureKind.Crystal ? 0.55f : 0f;
        }

        void Retire(Item item)
        {
            if (item == null || item.Go == null) return;
            item.Go.SetActive(false);
            _spare.Push(item);
            Count--;
        }
    }
}
