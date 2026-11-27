using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Look;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Melee
{
    /// <summary>
    /// One melee item in the world: lying on the ground (a physics object anyone can see and the player can pick
    /// up) or in the player's hand. Enemies never pick these up: they only look for guns (<c>Weapon.All</c>).
    ///
    /// Until the Blender models exist it is a stand-in built from primitives, with two empty sockets the real
    /// model must also have: <c>Grip</c> (where the hand holds it, the item's origin) and <c>Tip</c> (the far end of
    /// the business end). The item lies along its own +Y, grip at the bottom.
    /// </summary>
    public sealed class MeleeItem : MonoBehaviour, IHittable
    {
        static readonly List<MeleeItem> Registry = new List<MeleeItem>();
        /// <summary>Every melee item that currently exists.</summary>
        public static IReadOnlyList<MeleeItem> All => Registry;
        public static void ResetStatics() => Registry.Clear();

        public MeleeData Data { get; private set; }
        public MeleeSpec Spec { get; private set; }
        public Rigidbody Body { get; private set; }
        public bool Held { get; private set; }
        public Transform Grip { get; private set; }
        public Transform Tip { get; private set; }
        /// <summary>The glowing blade (the laser sword), dimmed while it is overheated. Null for the others.</summary>
        public Renderer Glow { get; private set; }
        /// <summary>Unscaled time it last left the player's hand, so it is not grabbed straight back.</summary>
        public float DroppedAt { get; private set; } = -100f;
        /// <summary>Raised once when the player takes it (a base uses this to know it is gone for the run).</summary>
        public event System.Action<MeleeItem> Taken;

        BoxCollider _box;
        Renderer[] _renderers;
        GravityBody _pull;
        System.Action<Vector3, Transform> _layDown;
        bool _settling;
        Transform _settleOn;

        public static MeleeItem Create(MeleeData data, Transform parent)
        {
            var go = new GameObject(data.displayName);
            go.layer = Layers.Item;
            go.transform.SetParent(parent, false);
            var item = go.AddComponent<MeleeItem>();
            item.Initialize(data);
            return item;
        }

        void Initialize(MeleeData data)
        {
            Data = data;
            Spec = data.ToSpec();
            Grip = Socket("Grip", Vector3.zero);
            float tip = BuildFromModel(data.Id, out float fromModel) ? fromModel : BuildStandIn(data.Id);
            Tip = Socket("Tip", new Vector3(0f, tip, 0f));
            _renderers = GetComponentsInChildren<Renderer>(true);

            _box = gameObject.AddComponent<BoxCollider>();
            Bounds b = Weapons.WeaponModelFactory.MeasureLocalBounds(transform);
            _box.center = b.center;
            _box.size = Vector3.Max(b.size, new Vector3(0.04f, 0.04f, 0.04f));
            Body = gameObject.AddComponent<Rigidbody>();
            Body.mass = Mathf.Max(0.1f, Spec.Mass);
            Body.angularDamping = 0.4f;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            SetLoose();
        }

        void OnEnable() { if (!Registry.Contains(this)) Registry.Add(this); }
        void OnDisable() => Registry.Remove(this);

        Transform Socket(string name, Vector3 at)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, false);
            t.localPosition = at;
            return t;
        }

        // ------------------------------------------------------------------ changing hands

        /// <summary>In the player's hand: no collider, no physics, no shadow. The caller parents and places it.</summary>
        public void TakeByPlayer()
        {
            bool first = !Held;
            Held = true;
            _settling = false;
            Layers.SetLayerRecursively(gameObject, Layers.Player);
            _box.enabled = false;
            Body.isKinematic = true;
            Body.interpolation = RigidbodyInterpolation.None;
            UseOwnPull(false);
            SetShadows(false);
            transform.localScale = Vector3.one;
            if (first) Taken?.Invoke(this);
        }

        /// <summary>Let go: a physics object moving at this velocity, under <paramref name="parent"/>.</summary>
        public void Drop(Transform parent, Vector3 velocity, Vector3 spin)
        {
            transform.SetParent(parent, true);
            Held = false;
            SetLoose();
            DroppedAt = Time.unscaledTime;
            Body.linearVelocity = velocity;
            Body.angularVelocity = spin;
        }

        void SetLoose()
        {
            Layers.SetLayerRecursively(gameObject, Layers.Item);
            _box.enabled = true;
            Body.isKinematic = false;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            UseOwnPull(SolarSystem.Instance != null);
            SetShadows(true);
        }

        /// <summary>Lies still where it was put, riding with its parent (on show at a base). Picking it up ends that.</summary>
        public void HoldStill()
        {
            UseOwnPull(false);
            Body.useGravity = false;
            Body.isKinematic = true;
            Body.interpolation = RigidbodyInterpolation.None;
        }

        /// <summary>
        /// In the solar system it falls toward its own world like a dropped gun (never Unity's global gravity); if
        /// it sinks into ground that is only solid near the player it is laid on top and handed to that world.
        /// </summary>
        void UseOwnPull(bool on)
        {
            if (on && _pull == null)
            {
                _pull = gameObject.AddComponent<GravityBody>();
                _pull.SunkBelow = -0.8f;
                _pull.PutBackAt = 0.15f;
                _pull.Sank = _layDown ??= LayDown;
            }
            if (_pull != null && _pull.enabled != on) _pull.enabled = on;
            Body.useGravity = !on;
        }

        void LayDown(Vector3 onTop, Transform world)
        {
            // Called while the solar system walks its loose objects: only made still here, handed over next Update.
            Body.position = onTop;
            transform.position = onTop;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.isKinematic = true;
            _settling = true;
            _settleOn = world;
        }

        void Update()
        {
            if (!_settling) return;
            _settling = false;
            if (Held) return;
            UseOwnPull(false);
            Body.useGravity = false;
            Body.interpolation = RigidbodyInterpolation.None;
            Transform world = _settleOn != null ? _settleOn : AI.CombatWorld.GroundParent;
            _settleOn = null;
            if (world != null) transform.SetParent(world, true);
        }

        void SetShadows(bool on)
        {
            var mode = on ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            // What shines by itself (the sword's blade) never casts a shadow.
            for (int i = 0; i < _renderers.Length; i++) if (_renderers[i] != null && _renderers[i] != Glow) _renderers[i].shadowCastingMode = mode;
        }

        /// <summary>Dims the blade while the sword is locked by heat.</summary>
        public void SetGlow(float level)
        {
            if (Glow == null) return;
            bool on = level > 0.05f;
            if (Glow.enabled != on) Glow.enabled = on;
        }

        // ------------------------------------------------------------------ being shot or swatted

        public HitOutcome Hit(in HitInfo hit)
        {
            if (!Held && Body != null && !Body.isKinematic)
                Body.AddForceAtPosition(hit.Direction * 3f, hit.Point, ForceMode.VelocityChange);
            return new HitOutcome { Hit = true, Label = Data != null ? Data.displayName : "Melee item" };
        }

        public string DescribeForDebug() => (Data != null ? Data.displayName : "Melee item") + (Held ? " (held)" : " (on the ground)");

        // ------------------------------------------------------------------ the stand-in shapes

        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        static Material Paint(string key, Color color, float smoothness, bool glow = false)
        {
            if (Materials.TryGetValue(key, out Material m) && m != null) return m;
            // Drawn brighter than white so it blooms, like the game's other things that shine by themselves.
            m = glow ? Mat.Unlit(color * 2.2f) : Mat.Lit(color, smoothness);
            m.name = "OA_Melee_" + key;
            Materials[key] = m;
            return m;
        }

        /// <summary>The model the five weapons are drawn from (tools/blender/build_props.py): one piece per id, a Glow piece for what shines, a socket "idTip".</summary>
        public const string ModelId = "Melee";

        /// <summary>The weapon's real model, held at its origin and pointing up. False (and nothing added) if there is none.</summary>
        bool BuildFromModel(string id, out float tip)
        {
            tip = 0f;
            ModelFile file = ModelLibrary.Find(ModelId);
            Mesh mesh = ModelLibrary.PartMesh(ModelId, id);
            if (file == null || mesh == null || !file.TrySocket(id + "Tip", out ModelSocket socket)) return false;
            tip = socket.Y;
            ModelPiece(id, mesh);
            string shining = id + "Glow";
            Mesh lit = ModelLibrary.PartMesh(ModelId, shining);
            if (lit != null) Glow = ModelPiece(shining, lit);
            return true;
        }

        Renderer ModelPiece(string part, Mesh mesh)
        {
            var go = new GameObject(part);
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = ModelLibrary.PieceMaterial(ModelId, part);
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            // What shines by itself never casts a shadow (SetShadows leaves the Glow alone for the same reason).
            if (ModelFile.Shines(part)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        /// <summary>Builds the primitive stand-in for an id and returns how far up the tip is: the fallback when the model is missing.</summary>
        float BuildStandIn(string id)
        {
            Material dark = Paint("dark", new Color(0.14f, 0.14f, 0.16f), 0.4f);
            switch (id)
            {
                case MeleeCatalog.LaserSwordId:
                {
                    Material steel = Paint("steel", new Color(0.62f, 0.63f, 0.66f), 0.7f);
                    Rod(dark, 0f, 0.24f, 0.034f);
                    Rod(steel, 0.22f, 0.04f, 0.05f);
                    Glow = Rod(Paint("blade", new Color(0.45f, 0.85f, 1f), 0f, glow: true), 0.26f, 0.95f, 0.03f);
                    return MeleeCatalog.TipHeight(id);
                }
                case MeleeCatalog.BaseballBatId:
                {
                    Material wood = Paint("ash", new Color(0.78f, 0.62f, 0.42f), 0.35f);
                    Rod(dark, -0.02f, 0.03f, 0.05f);   // the knob
                    Rod(wood, 0f, 0.36f, 0.034f);
                    Rod(wood, 0.34f, 0.18f, 0.05f);
                    Rod(wood, 0.5f, 0.36f, 0.066f);
                    return MeleeCatalog.TipHeight(id);
                }
                case MeleeCatalog.MailboxId:
                {
                    Material post = Paint("post", new Color(0.30f, 0.26f, 0.22f), 0.2f);
                    Material tin = Paint("tin", new Color(0.36f, 0.42f, 0.50f), 0.55f);
                    Material flag = Paint("flag", new Color(0.85f, 0.55f, 0.15f), 0.3f);
                    Box(post, new Vector3(0f, 0.45f, 0f), new Vector3(0.07f, 0.9f, 0.07f));
                    Box(tin, new Vector3(0f, 0.99f, 0.04f), new Vector3(0.22f, 0.20f, 0.44f));
                    Rod(tin, 1.09f, 0.0f, 0.22f, new Vector3(0f, 0f, 0.04f), Quaternion.Euler(90f, 0f, 0f), 0.44f);
                    Box(flag, new Vector3(0.125f, 1.08f, -0.02f), new Vector3(0.02f, 0.16f, 0.05f));
                    return MeleeCatalog.TipHeight(id);
                }
                case MeleeCatalog.ButterflyKnifeId:
                {
                    Material steel = Paint("steel", new Color(0.62f, 0.63f, 0.66f), 0.7f);
                    Box(dark, new Vector3(-0.009f, 0.06f, 0f), new Vector3(0.012f, 0.12f, 0.022f));
                    Box(dark, new Vector3(0.009f, 0.06f, 0f), new Vector3(0.012f, 0.12f, 0.022f));
                    Box(steel, new Vector3(0f, 0.175f, 0.003f), new Vector3(0.004f, 0.11f, 0.02f));
                    return MeleeCatalog.TipHeight(id);
                }
                default:   // the frying pan
                {
                    Material iron = Paint("iron", new Color(0.12f, 0.12f, 0.13f), 0.45f);
                    Rod(dark, 0f, 0.24f, 0.026f);
                    // The pan's face points forward (+Z) when held, so a swing lands on its flat.
                    Rod(iron, 0.37f, 0f, 0.27f, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), 0.035f);
                    return MeleeCatalog.TipHeight(id);
                }
            }
        }

        /// <summary>A cylinder up the item from <paramref name="from"/>, <paramref name="length"/> long and <paramref name="width"/> across.</summary>
        Renderer Rod(Material m, float from, float length, float width) =>
            Rod(m, from + length * 0.5f, length, width, Vector3.zero, Quaternion.identity, length);

        Renderer Rod(Material m, float centreY, float length, float width, Vector3 offset, Quaternion turn, float height)
        {
            return Piece(PrimitiveType.Cylinder, m, new Vector3(0f, centreY, 0f) + offset, new Vector3(width, height * 0.5f, width), turn);
        }

        Renderer Box(Material m, Vector3 centre, Vector3 size) => Piece(PrimitiveType.Cube, m, centre, size, Quaternion.identity);

        Renderer Piece(PrimitiveType type, Material m, Vector3 centre, Vector3 scale, Quaternion turn)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = "StandIn";
            var col = go.GetComponent<Collider>();
            // At once, not at the end of the frame: a physics step in between would see it as part of the body.
            if (col != null) DestroyImmediate(col);
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = turn;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            return r;
        }
    }
}
