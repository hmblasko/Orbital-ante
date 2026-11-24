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

        sealed class Group
        {
            public List<Item> Items;
            public List<Placement> Placements;
            public Vec3d[] Positions;
            public SpawnBudget.Group Progress;
        }

        readonly PlanetSurface _surface;
        readonly Transform _root;
        readonly Stack<Item> _spare = new Stack<Item>();
        readonly Dictionary<object, Group> _groups = new Dictionary<object, Group>();
        readonly List<Group> _pending = new List<Group>();
        readonly Stack<List<Item>> _spareLists = new Stack<List<Item>>();
        readonly SpawnBudget _spawnBudget = new SpawnBudget();
        PlanetTerrain _terrain;
        Vec3d[] _fixedPositions;
        Item[] _fixedItems;
        int _cursor;
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
            // Anything already standing there (the ground near a base can be shown before the base is placed) goes now.
            Vector3 c = SpaceConvert.ToVector3(centre);
            float r2 = (float)(radius * radius);
            foreach (Group group in _groups.Values)
            {
                List<Item> items = group.Items;
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    Item item = items[i];
                    if (item == null || item.Go == null || (item.Go.transform.localPosition - c).sqrMagnitude >= r2) continue;
                    Retire(item);
                    items.RemoveAt(i);
                }
            }
            if (_fixedItems == null) return;
            for (int i = 0; i < _fixedItems.Length; i++)
            {
                Item item = _fixedItems[i];
                if (item == null || item.Go == null || (item.Go.transform.localPosition - c).sqrMagnitude >= r2) continue;
                Retire(item);
                _fixedItems[i] = null;
            }
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
            foreach (Group group in _groups.Values)
            {
                group.Progress.Cancel();
                for (int i = 0; i < group.Items.Count; i++) Retire(group.Items[i]);
                group.Items.Clear();
                _spareLists.Push(group.Items);
            }
            _groups.Clear();
            _pending.Clear();
            _spawnBudget.BeginFrame(0);
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
            _pending.Clear();
            _spare.Clear();
        }

        /// <summary>Call at the start of each frame: resets the count of things put out this frame.</summary>
        public void BeginFrame() => _spawnBudget.BeginFrame(Mathf.Max(1, _surface.Settings.featuresPerFrame));

        /// <summary>
        /// Accepts the loose scatter for one piece of ground. True includes a partly shown group: Tick carries
        /// on next frame, and the caller must HideGroup if it is no longer wanted. False leaves it unaccepted.
        /// </summary>
        public bool ShowGroup(object key, List<Placement> placements, Vec3d[] positions)
        {
            if (!_active) return false;
            if (_groups.ContainsKey(key)) return true;
            // Finish accepted groups first; otherwise a stream of new tiles could starve a partial group.
            if (placements.Count > 0 && (_pending.Count > 0 || _spawnBudget.Remaining <= 0)) return false;
            var group = new Group
            {
                Items = _spareLists.Count > 0 ? _spareLists.Pop() : new List<Item>(),
                Placements = placements,
                Positions = positions,
                Progress = new SpawnBudget.Group(placements.Count),
            };
            _groups.Add(key, group);
            ShowMore(group);
            if (!group.Progress.Complete) _pending.Add(group);
            return true;
        }

        void ShowMore(Group group)
        {
            int count = group.Progress.Take(_spawnBudget, out int first);
            for (int i = first; i < first + count; i++)
            {
                Item item = Spawn(group.Placements[i], group.Positions[i]);
                if (item != null) group.Items.Add(item);
            }
            if (group.Progress.Complete)
            {
                group.Placements = null;
                group.Positions = null;
            }
        }

        public void HideGroup(object key)
        {
            if (!_groups.TryGetValue(key, out Group group)) return;
            group.Progress.Cancel();
            _pending.Remove(group);
            for (int i = 0; i < group.Items.Count; i++) Retire(group.Items[i]);
            group.Items.Clear();
            _spareLists.Push(group.Items);
            _groups.Remove(key);
        }

        /// <summary>
        /// Call once a frame. Looks at a slice of the fixed placements each time (all of them a few times a
        /// second), so the cost per frame stays small however many a body has.
        /// </summary>
        public void Tick(Vec3d viewer)
        {
            if (!_active) return;
            // The surface has now hidden unwanted groups, so only still-visible partial groups continue.
            for (int i = 0; i < _pending.Count && _spawnBudget.Remaining > 0;)
            {
                Group group = _pending[i];
                ShowMore(group);
                if (group.Progress.Complete) _pending.RemoveAt(i);
                else i++;
            }
            if (_fixedPositions == null || _fixedPositions.Length == 0) return;
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
                    if (d < show * tall && _spawnBudget.Remaining > 0 && !Cleared(_fixedPositions[i]))
                    {
                        _spawnBudget.Take(1);
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
            // Bedded, tipped and shaded one by one, so the scatter does not look stamped out (NaturalPose).
            NaturalPose.Pose pose = NaturalPose.Of(p.Kind, p.Variant, p.Yaw, p.Floats);
            NaturalPose.Tint(pose, p.R, p.G, p.B, out float tr, out float tg, out float tb);
            item.Renderer.sharedMaterial = PlanetMaterials.VertexColour(new Color(tr, tg, tb, 1f), p.Absolute ? 0f : 1f, GlowOf(p.Kind));

            float scale = (float)p.Scale;
            Vector3 up = SpaceConvert.ToVector3(position.Normalized);
            // Stalagmites and crystals placed inside caves stand on the cave floor, which is not always "down".
            Transform t = item.Go.transform;
            // Pushed into the ground by a share of its height, so no base shows where the ground dips under it.
            // Not inside caves, where the floor is not along "up".
            float sink = p.Absolute ? 0f : pose.Sink * scale * pose.StretchY;
            t.localPosition = SpaceConvert.ToVector3(position) - up * sink;
            t.localRotation = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, (float)(p.Yaw * Mathf.Rad2Deg), 0f)
                              * Quaternion.AngleAxis(pose.TiltDegrees, Quaternion.Euler(0f, pose.TiltHeading, 0f) * Vector3.right);
            t.localScale = new Vector3(pose.StretchX, pose.StretchY, pose.StretchZ) * scale;

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
