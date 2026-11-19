using System.Collections.Generic;
using OrbitalAnte.AI;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Gore;
using OrbitalAnte.Look;
using OrbitalAnte.Player;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Creatures
{
    /// <summary>One hitbox of a Strider: a piece of a limb, the belly or the lid.</summary>
    public sealed class StriderPart : MonoBehaviour, IHittable
    {
        public const int Belly = -1;
        public const int Carapace = -2;

        [System.NonSerialized] public Strider Owner;
        [System.NonSerialized] public int Limb;
        [System.NonSerialized] public int Segment;

        public HitOutcome Hit(in HitInfo hit) => Owner != null ? Owner.TakeHit(this, hit) : new HitOutcome { Hit = true };

        public string DescribeForDebug()
        {
            if (Owner == null) return "Strider piece";
            if (Limb == Belly) return "Strider belly " + Mathf.CeilToInt((float)Owner.Body.Belly);
            if (Limb == Carapace) return "Strider lid " + Mathf.CeilToInt((float)Owner.Body.Carapace);
            return "Strider " + StriderLayout.Piece(Limb, Segment) + " " + Mathf.CeilToInt((float)Owner.Body.Hp(Limb, Segment));
        }
    }

    /// <summary>A piece that has come off: it lies where it lands, then shrinks away.</summary>
    public sealed class StriderChunk : MonoBehaviour
    {
        static readonly List<StriderChunk> Live = new List<StriderChunk>();

        float _age, _life;
        Rigidbody _body;
        Vector3 _size;

        public static int Count => Live.Count;

        public static void ResetStatics() => Live.Clear();

        /// <summary>Removes every loose piece at once (the arena's restart).</summary>
        public static void ClearAll()
        {
            for (int i = Live.Count - 1; i >= 0; i--)
                if (Live[i] != null) Destroy(Live[i].gameObject);
            Live.Clear();
        }

        public static StriderChunk Make(string name, Vector3 position, Quaternion rotation, Transform parent, float mass, float life, int cap)
        {
            // Over the cap the oldest piece goes first.
            while (Live.Count >= Mathf.Max(16, cap))
            {
                StriderChunk oldest = Live[0];
                Live.RemoveAt(0);
                if (oldest != null) Destroy(oldest.gameObject);
            }
            var go = new GameObject(name);
            go.layer = Layers.Gib;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var chunk = go.AddComponent<StriderChunk>();
            chunk._life = life;
            Rigidbody body = go.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.maxAngularVelocity = 30f;
            // Pieces of one burst start touching: they are thrown by their given speeds, not shoved apart by the physics.
            body.maxDepenetrationVelocity = 2f;
            chunk._body = body;
            if (SolarSystem.Instance != null)
            {
                // No rigidbody in the solar system uses Unity's gravity: the piece falls toward its own world.
                body.useGravity = false;
                body.angularDamping = 0.03f;
                var pull = go.AddComponent<GravityBody>();
                pull.SunkBelow = -1f;
                pull.PutBackAt = 0.3f;
                pull.Sank = chunk.SankInto;
            }
            else
            {
                body.angularDamping = 0.2f;
            }
            Live.Add(chunk);
            return chunk;
        }

        public Rigidbody Body => _body;

        void SankInto(Vector3 onTop, Transform world)
        {
            if (_body.isKinematic) return;
            _body.position = onTop;
            transform.position = onTop;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.isKinematic = true;
        }

        void Start() => _size = transform.localScale;

        void Update()
        {
            _age += Time.deltaTime;
            if (_age < _life) return;
            float left = 1f - (_age - _life) / 1.5f;
            if (left <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            transform.localScale = _size * left;
        }

        void OnDestroy() => Live.Remove(this);
    }

    /// <summary>
    /// The Strider in the world: the first alien creature (Matthew, 17 Nov 2026), a spider crab the height of a
    /// doorway on eight stilt legs with two long claws. The rules are plain C# (<see cref="StriderBody"/>,
    /// <see cref="StriderAnimator"/>, <see cref="StriderBrain"/>); this turns them into objects.
    ///
    /// - Every piece of every limb is its own object and hitbox and comes off with everything beyond it, leaving a
    ///   wound on what is left. A loose chain is a physics object that fades after a while.
    /// - It is not a physics object while alive: it is walked over the ground by its brain, and its feet are placed
    ///   by the animator. "Up" is its own transform's up, never +Y.
    /// - It dies only when the belly or the lid gives way; then it bursts.
    /// </summary>
    public sealed class Strider : MonoBehaviour
    {
        public StriderBody Body { get; private set; }
        public StriderBrain Brain { get; private set; }
        public bool Alive => Body != null && Body.Alive;
        public event System.Action<Strider> Died;

        static readonly List<Strider> Everyone = new List<Strider>();
        public static IReadOnlyList<Strider> All => Everyone;
        public static void ResetStatics() => Everyone.Clear();

        StriderRules _rules;
        readonly StriderAnimator _animator = new StriderAnimator();
        readonly StriderPose _pose = new StriderPose();
        readonly Transform[] _piece = new Transform[StriderLayout.Limbs * StriderLayout.Segments];
        readonly Transform[] _finger = new Transform[2];
        readonly int[] _shown = new int[StriderLayout.Limbs];
        Transform _carapace, _belly, _bodyRoot;
        Vector3 _home;
        bool _blocked, _sees, _striking;
        float _nextLook;
        Material _material;

        /// <summary>Makes a Strider standing at a place. Null if its model is not in the game.</summary>
        public static Strider Create(Vector3 position, Quaternion rotation, Transform parent, CreatureConfig config, int seed, string name = "Strider")
        {
            if (ModelLibrary.Find(StriderLayout.Model) == null)
            {
                Debug.LogWarning("Orbital Ante: the Strider's model (Resources/Models/Strider) is missing, so none was made.");
                return null;
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var strider = go.AddComponent<Strider>();
            strider.Build(config != null ? config.strider : new StriderRules(), seed);
            return strider;
        }

        void Build(StriderRules rules, int seed)
        {
            _rules = rules;
            Body = new StriderBody(rules.hp);
            Brain = new StriderBrain(rules, seed);
            _home = transform.position;
            _material = ModelLibrary.MaterialFor(StriderLayout.Model);

            // Its hitboxes move with it every frame: a kinematic body tells the physics so.
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            _bodyRoot = new GameObject("Body").transform;
            _bodyRoot.SetParent(transform, false);
            _bodyRoot.localScale = Vector3.one * (float)StriderLayout.Size;
            _carapace = Piece("Carapace", _bodyRoot);
            _belly = Piece("Belly", _bodyRoot);
            // Boxes in the body's own size: the lid above the rim, the belly below it.
            Box(_carapace, new Vector3(0f, 0.075f, 0.01f), new Vector3(0.74f, 0.21f, 0.86f), StriderPart.Carapace, 0);
            Box(_belly, new Vector3(0f, -0.105f, 0.0f), new Vector3(0.70f, 0.15f, 0.80f), StriderPart.Belly, 0);

            for (int limb = 0; limb < StriderLayout.Limbs; limb++)
            {
                _shown[limb] = StriderLayout.Segments;
                float scale = (float)StriderLayout.Scale(limb);
                for (int s = 0; s < StriderLayout.Segments; s++)
                {
                    Transform piece = Piece(StriderLayout.Piece(limb, s), transform);
                    piece.name = StriderLayout.Piece(limb, s) + " " + limb;
                    piece.localScale = Vector3.one * scale;
                    _piece[limb * StriderLayout.Segments + s] = piece;
                    // A capsule along the piece, a little fatter than the shell so thin legs can be hit.
                    var capsule = piece.gameObject.AddComponent<CapsuleCollider>();
                    float length = (float)StriderLayout.Length(limb, s) / scale;
                    float radius = Mathf.Max(0.085f, (float)StriderLayout.Radius(limb, s) / scale * 1.45f);
                    capsule.direction = 2;
                    capsule.radius = radius;
                    capsule.height = length + radius;
                    capsule.center = new Vector3(0f, 0f, length * 0.5f);
                    Tag(piece.gameObject, limb, s);
                }
                if (StriderLayout.IsArm(limb))
                {
                    Transform finger = Piece(StriderLayout.ClawFinger, transform);
                    finger.localScale = Vector3.one * (float)StriderLayout.Size;
                    _finger[StriderLayout.SideOf(limb)] = finger;
                }
            }
            ModelDetail.Attach(gameObject, StriderLayout.Model, DetailRule.BodyRange * 1.6f, true);
            Everyone.Add(this);
            Pose(0.0001f, default);
        }

        Transform Piece(string piece, Transform parent)
        {
            var go = new GameObject(piece);
            go.layer = Layers.Hitbox;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = ModelLibrary.PartMesh(StriderLayout.Model, piece);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            // Like the other enemies, it casts no real-time shadow: on a Deck that would draw it again per cascade.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return go.transform;
        }

        void Box(Transform on, Vector3 centre, Vector3 size, int limb, int segment)
        {
            var box = on.gameObject.AddComponent<BoxCollider>();
            box.center = centre;
            box.size = size;
            Tag(on.gameObject, limb, segment);
        }

        void Tag(GameObject go, int limb, int segment)
        {
            var part = go.AddComponent<StriderPart>();
            part.Owner = this;
            part.Limb = limb;
            part.Segment = segment;
        }

        void OnDestroy() => Everyone.Remove(this);

        static Vector3 V(Vec3d v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        static Quaternion Q(QuatD q) => new Quaternion((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);
        static Vec3d D(Vector3 v) => new Vec3d(v.x, v.y, v.z);

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            if (!Alive || GameSession.Paused) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            var senses = new StriderSenses { Blocked = _blocked, Home = D(transform.InverseTransformPoint(_home)), BodyHeight = _pose.BodyPosition.Y };
            Transform eye = CombatWorld.PlayerPresent ? CombatWorld.PlayerEye : null;
            if (eye != null && !CombatWorld.EnemiesHoldFire)
            {
                Vector3 up = transform.up;
                Vector3 chest = eye.position - up * 0.35f;
                Vector3 feet = eye.position - up * 1.6f;
                senses.Player = D(transform.InverseTransformPoint(feet));
                senses.Chest = D(transform.InverseTransformPoint(chest));
                if (Time.time >= _nextLook)
                {
                    // Four looks a second: near enough, and nothing solid between its eyes and the player.
                    _nextLook = Time.time + 0.25f;
                    Vector3 from = _bodyRoot.position;
                    float far = Vector3.Distance(from, eye.position);
                    _sees = far < _rules.sightRange * CombatWorld.PlayerSeenFrom
                            && !Physics.Linecast(from, eye.position, Layers.WorldMask, QueryTriggerInteraction.Ignore);
                }
                senses.SeesPlayer = _sees;
            }
            else
            {
                _sees = false;
            }

            StriderOrders orders = Brain.Think(Body, senses, dt);
            if (orders.StrikeSide >= 0 && !_striking)
            {
                // The warning can be heard as well as seen (its own sound once there is one).
                if (!Audio.AudioDirector.Play("creature.strider.raise", _bodyRoot.position)) Audio.AudioDirector.Play("enemy.flare.raise", _bodyRoot.position, 0.8f, 0.6f);
            }
            _striking = orders.StrikeSide >= 0;
            Walk(orders, dt);
            Pose(dt, orders);
            if (orders.Landed && eye != null) ClawLanded(orders, eye);
        }

        void Walk(in StriderOrders orders, float dt)
        {
            Vector3 up = transform.up;
            if (orders.TurnRate != 0.0) transform.rotation = Quaternion.AngleAxis((float)orders.TurnRate * Mathf.Rad2Deg * dt, up) * transform.rotation;
            Vector3 step = transform.TransformDirection(V(orders.Velocity)) * dt;
            _blocked = false;
            if (step.sqrMagnitude <= 0f) return;
            Vector3 to = transform.position + step;
            // Its legs step over things; its body does not go through them.
            // (The ball is kept clear of the ground when it is down on its belly, or the ground itself would stop it.)
            float bodyHeight = _bodyRoot.localPosition.y;
            if (Physics.CheckSphere(to + up * Mathf.Max(0.8f, bodyHeight) + step.normalized * 0.6f, 0.55f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                _blocked = true;
                return;
            }
            // Stay on the ground: look for it from above where it is going.
            if (Physics.Raycast(to + up * 3f, -up, out RaycastHit ground, 9f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                // A step up or down of more than a shin is a wall to it for now (its feet are placed on level ground).
                if (Mathf.Abs(Vector3.Dot(ground.point - to, up)) > 0.3f)
                {
                    _blocked = true;
                    return;
                }
                to = ground.point;
            }
            transform.position = to;
        }

        void Pose(float dt, in StriderOrders orders)
        {
            var input = new StriderAnimInput
            {
                Velocity = orders.Velocity, TurnRate = orders.TurnRate,
                StrikeSide = orders.StrikeSide, StrikeT = orders.StrikeT, StrikeAt = orders.StrikeAt,
            };
            _animator.Update(Body, input, dt, _pose);
            _bodyRoot.SetLocalPositionAndRotation(V(_pose.BodyPosition), Q(_pose.BodyRotation));
            for (int limb = 0; limb < StriderLayout.Limbs; limb++)
            {
                for (int s = 0; s < _shown[limb]; s++)
                {
                    int i = limb * StriderLayout.Segments + s;
                    _piece[i].SetLocalPositionAndRotation(V(_pose.Position[i]), Q(_pose.Rotation[i]));
                }
                if (StriderLayout.IsArm(limb) && _shown[limb] == StriderLayout.Segments)
                {
                    int side = StriderLayout.SideOf(limb);
                    _finger[side].SetLocalPositionAndRotation(V(_pose.FingerPosition[side]), Q(_pose.FingerRotation[side]));
                }
            }
        }

        void ClawLanded(in StriderOrders orders, Transform eye)
        {
            Vector3 at = transform.TransformPoint(V(orders.StrikeAt));
            Vector3 chest = eye.position - transform.up * 0.35f;
            Audio.AudioDirector.Play("gore.splat", at, 0.5f, 0.7f);
            if (Vector3.Distance(at, chest) > _rules.strikeRadius) return;
            PlayerHealth player = CombatWorld.Player;
            if (player != null) player.TakeDamage(_rules.strikeDamage, (chest - _bodyRoot.position).normalized);
        }

        // ------------------------------------------------------------------ being hit

        public HitOutcome TakeHit(StriderPart part, in HitInfo hit)
        {
            var outcome = new HitOutcome { Hit = true };
            if (!Alive) return outcome;
            // A piece that has already come off is not the animal any more.
            if (part.Limb >= 0 && part.Segment >= _shown[part.Limb]) return outcome;
            outcome.Enemy = true;
            StriderHit result;
            if (part.Limb >= 0)
            {
                result = Body.HitLimb(part.Limb, part.Segment, hit.Damage, hit.Sever);
                outcome.Label = "Strider " + StriderLayout.Piece(part.Limb, part.Segment);
                for (int i = 0; i < result.Losses.Count; i++) Detach(result.Losses[i], hit.Direction, hit.Point);
                outcome.Severed = result.Losses.Count > 0;
                if (hit.Lethal)
                {
                    // A one-hit-kill gun kills wherever it lands, after the struck piece has taken its hit.
                    result = Body.HitBody(true, 0, lethal: true);
                }
            }
            else
            {
                result = Body.HitBody(part.Limb == StriderPart.Belly, hit.Damage, hit.Lethal);
                outcome.Label = part.Limb == StriderPart.Belly ? "Strider belly" : "Strider lid";
            }
            Brain.Alert();
            Vector3 push = transform.InverseTransformDirection(hit.Direction);
            _animator.Flinch(new Vec3d(push.x, 0.4, push.z) * (outcome.Severed ? 1.5 : 0.35));
            bool lid = part.Limb == StriderPart.Carapace;
            GoreFx gore = GoreFx.Instance;
            // The lid is armour and says so: no blood, a dull knock. Everything else bleeds.
            if (lid && !result.Killed) Audio.AudioDirector.Play("impact.armor", hit.Point, 0.7f, 0.55f);
            else if (gore != null) gore.Burst(hit.Point, -hit.Direction, outcome.Severed ? 18 : 4, outcome.Severed ? 6f : 3f);
            if (result.Killed)
            {
                outcome.Killed = true;
                Burst(result, hit);
            }
            outcome.Juice = outcome.Killed ? 1f : outcome.Severed ? 0.6f : lid ? 0.04f : 0.12f;
            return outcome;
        }

        /// <summary>A limb's pieces from one joint outward come off as one loose chain, and a wound is left behind.</summary>
        void Detach(in StriderLoss loss, Vector3 direction, Vector3 point)
        {
            int limb = loss.Limb, from = loss.FromSegment, had = _shown[limb];
            if (from >= had) return;
            _shown[limb] = from;
            Transform first = _piece[limb * StriderLayout.Segments + from];
            Transform loose = CombatWorld.DropParent != null ? CombatWorld.DropParent : transform.parent;
            StriderChunk chunk = StriderChunk.Make("Strider " + first.name, first.position, first.rotation, loose, 3f + 2.5f * (had - from), _rules.pieceLife, _rules.maxLoosePieces);
            for (int s = from; s < had; s++) Loosen(_piece[limb * StriderLayout.Segments + s], chunk.transform);
            if (StriderLayout.IsArm(limb) && had == StriderLayout.Segments)
            {
                Transform finger = _finger[StriderLayout.SideOf(limb)];
                finger.SetParent(chunk.transform, true);
                finger.gameObject.layer = Layers.Gib;
            }
            float radius = (float)StriderLayout.Radius(limb, from);
            // The wound on the piece that came off, looking back at where it was joined.
            Wound(first, Vector3.zero, Quaternion.Euler(0f, 180f, 0f), radius / first.lossyScale.x);
            // And on what is left: the end of the piece before it, or the body.
            if (from == 0)
            {
                Vector3 hip = V(StriderLayout.HipModelled(limb));
                Wound(_bodyRoot, hip, Quaternion.LookRotation(V(StriderLayout.Out(limb)), Vector3.up), radius / (float)StriderLayout.Size);
            }
            else
            {
                Transform before = _piece[limb * StriderLayout.Segments + from - 1];
                float scale = (float)StriderLayout.Scale(limb);
                Wound(before, new Vector3(0f, 0f, (float)StriderLayout.Length(limb, from - 1) / scale), Quaternion.identity, radius / scale);
            }

            Vector3 up = transform.up;
            Vector3 out_ = transform.TransformDirection(V(StriderLayout.Out(limb)));
            Rigidbody body = chunk.Body;
            float kick = loss.Burst ? Random.Range(5f, 9f) : Random.Range(3f, 5f);
            body.linearVelocity = direction * (loss.Burst ? 1.5f : 4f) + out_ * kick + up * Random.Range(3f, 6f);
            body.angularVelocity = Random.onUnitSphere * Random.Range(3f, 9f);
            GoreFx gore = GoreFx.Instance;
            if (gore != null) gore.Burst(first.position, out_, 14, 5f);
            Audio.AudioDirector.Play("gore.pop.small", first.position, 0.9f, Random.Range(0.7f, 0.9f));
        }

        static void Loosen(Transform piece, Transform chunk)
        {
            piece.SetParent(chunk, true);
            piece.gameObject.layer = Layers.Gib;
            var part = piece.GetComponent<StriderPart>();
            if (part != null)
            {
                // Destroy waits for the end of the frame; a second pellet this frame must not find the animal through it.
                part.Owner = null;
                Destroy(part);
            }
            var collider = piece.GetComponent<Collider>();
            GibManager gibs = GibManager.Instance;
            if (collider != null && gibs != null) collider.sharedMaterial = gibs.BouncyMaterial;
        }

        void Wound(Transform on, Vector3 at, Quaternion facing, float size)
        {
            Mesh mesh = ModelLibrary.PartMesh(StriderLayout.Model, StriderLayout.Stump);
            if (mesh == null) return;
            var go = new GameObject("Wound");
            go.layer = on.gameObject.layer;
            go.transform.SetParent(on, false);
            go.transform.SetLocalPositionAndRotation(at, facing);
            go.transform.localScale = Vector3.one * (size / (float)StriderLayout.StumpRadius);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            // Like the other enemies, it casts no real-time shadow: on a Deck that would draw it again per cascade.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        /// <summary>It dies: every limb left is thrown off, the lid and the belly fly apart, and what was inside comes out.</summary>
        void Burst(StriderHit result, in HitInfo hit)
        {
            for (int i = 0; i < result.Losses.Count; i++) Detach(result.Losses[i], hit.Direction, hit.Point);
            Vector3 up = transform.up, middle = _bodyRoot.position;
            Transform loose = CombatWorld.DropParent != null ? CombatWorld.DropParent : transform.parent;
            ThrowBody(_carapace, loose, up * Random.Range(6f, 9f) + hit.Direction * 2f, 14f);
            ThrowBody(_belly, loose, -up * 1.5f + hit.Direction * 3f, 10f);
            for (int k = 0; k < StriderLayout.Meat.Length; k++)
            {
                Mesh mesh = ModelLibrary.PartMesh(StriderLayout.Model, StriderLayout.Meat[k]);
                if (mesh == null) continue;
                StriderChunk chunk = StriderChunk.Make("Strider meat", middle + Random.insideUnitSphere * 0.2f, Random.rotation, loose, 4f, _rules.pieceLife, _rules.maxLoosePieces);
                chunk.transform.localScale = Vector3.one * (float)StriderLayout.Size;
                chunk.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                chunk.gameObject.AddComponent<MeshRenderer>().sharedMaterial = _material;
                var ball = chunk.gameObject.AddComponent<SphereCollider>();
                ball.radius = 0.11f;
                chunk.Body.linearVelocity = (Random.onUnitSphere + up).normalized * Random.Range(4f, 8f);
                chunk.Body.angularVelocity = Random.onUnitSphere * 8f;
            }
            GoreFx gore = GoreFx.Instance;
            if (gore != null)
            {
                gore.Burst(middle, up, 60, 9f);
                gore.Burst(middle, hit.Direction, 30, 7f);
            }
            Audio.AudioDirector.Play("gore.pop.torso", middle, 1f, 0.7f);
            ModelDetail.LetGo(gameObject);
            Died?.Invoke(this);
            Everyone.Remove(this);
            Destroy(gameObject);
        }

        void ThrowBody(Transform half, Transform loose, Vector3 velocity, float mass)
        {
            StriderChunk chunk = StriderChunk.Make("Strider " + half.name, half.position, half.rotation, loose, mass, _rules.pieceLife, _rules.maxLoosePieces);
            chunk.transform.localScale = Vector3.one * (float)StriderLayout.Size;
            // The wounds where limbs were go with whichever half they sit nearer; they are children of the body's
            // frame, so hand them to the belly's chunk.
            if (half == _belly)
            {
                for (int i = _bodyRoot.childCount - 1; i >= 0; i--)
                {
                    Transform child = _bodyRoot.GetChild(i);
                    if (child != _carapace && child != _belly) child.SetParent(chunk.transform, true);
                }
            }
            half.SetParent(chunk.transform, true);
            Loosen(half, chunk.transform);
            chunk.Body.linearVelocity = velocity;
            chunk.Body.angularVelocity = Random.onUnitSphere * Random.Range(2f, 6f);
        }
    }
}
