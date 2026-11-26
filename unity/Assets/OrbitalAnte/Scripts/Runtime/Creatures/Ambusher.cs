using System.Collections.Generic;
using OrbitalAnte.AI;
using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Gore;
using OrbitalAnte.Look;
using OrbitalAnte.Player;
using UnityEngine;

namespace OrbitalAnte.Creatures
{
    public enum AmbusherPiece { Head, Seg, Jaw, Feeler }

    /// <summary>One hitbox of an Ambusher: its head, a length of its body, a jaw or a feeler.</summary>
    public sealed class AmbusherPart : MonoBehaviour, IHittable
    {
        [System.NonSerialized] public Ambusher Owner;
        [System.NonSerialized] public AmbusherPiece Piece;
        /// <summary>Which length of body (0 to 14), which jaw (0 right, 1 left) or which feeler (0 to 4).</summary>
        [System.NonSerialized] public int Index;

        public HitOutcome Hit(in HitInfo hit) => Owner != null ? Owner.TakeHit(this, hit) : new HitOutcome { Hit = true };

        public string DescribeForDebug()
        {
            if (Owner == null) return "Creature piece";
            AmbusherBody body = Owner.Body;
            switch (Piece)
            {
                case AmbusherPiece.Head: return "Ambusher head " + Mathf.CeilToInt((float)body.Head);
                case AmbusherPiece.Seg: return "Ambusher body " + Mathf.CeilToInt((float)body.SegHp(Index));
                case AmbusherPiece.Jaw: return "Ambusher jaw " + Mathf.CeilToInt((float)body.JawHp(Index));
                default: return "Ambusher feeler " + Mathf.CeilToInt((float)body.FeelerHp(Index));
            }
        }
    }

    /// <summary>
    /// The Ambusher in the world: an alien from the bobbit worm (Matthew, 17 Nov 2026). The rules are plain C#
    /// (<see cref="AmbusherBody"/>, <see cref="AmbusherAnimator"/>, <see cref="AmbusherBrain"/>); this turns them into
    /// objects.
    ///
    /// - Its root is the mouth of its burrow, on the ground. Its head, fifteen lengths of body, two jaws and five
    ///   feelers are each an object and a hitbox; whatever is not out of the hole is under the ground, where the
    ///   ground itself keeps shots off it. Wholly under, its hitboxes are switched off.
    /// - It is not a physics object while alive, and under the ground it goes where it likes: nothing is in its way
    ///   there. It only comes up where the ground above it is clear. "Up" is its own transform's up, never +Y.
    /// - Someone in its jaws is dragged through <see cref="FpsController.Carry"/> (the arena's player; elsewhere it
    ///   bites but cannot drag yet).
    /// - It dies when its head gives way or any length of its body is shot through; then whatever is out of the
    ///   ground comes apart.
    /// </summary>
    public sealed class Ambusher : MonoBehaviour
    {
        public AmbusherBody Body { get; private set; }
        public AmbusherBrain Brain { get; private set; }
        public bool Alive => Body != null && Body.Alive;
        public event System.Action<Ambusher> Died;

        static readonly List<Ambusher> Everyone = new List<Ambusher>();
        public static IReadOnlyList<Ambusher> All => Everyone;
        public static void ResetStatics() => Everyone.Clear();

        const string CoilSound = "creature.ambusher.coil", StrikeSound = "creature.ambusher.strike", RiseSound = "creature.ambusher.rise";

        AmbusherRules _rules;
        readonly AmbusherAnimator _animator = new AmbusherAnimator();
        readonly AmbusherPose _pose = new AmbusherPose();
        readonly Transform[] _node = new Transform[AmbusherLayout.Nodes];
        readonly Transform[] _jaw = new Transform[AmbusherLayout.Jaws];
        readonly Transform[] _feeler = new Transform[AmbusherLayout.Feelers];
        readonly List<Collider> _colliders = new List<Collider>();
        readonly List<MeshRenderer> _drawn = new List<MeshRenderer>();
        readonly MeshRenderer[] _nodeDrawn = new MeshRenderer[AmbusherLayout.Nodes];
        bool _lookedForWalker;
        Transform _rim;
        Vector3 _home, _playerWas;
        bool _playerKnown, _clearLine, _clearAbove, _solid = true, _felt;
        float _nextLook;
        AmbusherMode _lastMode = AmbusherMode.Lurk;
        Material _material;
        FpsController _walker;

        /// <summary>Makes an Ambusher with the mouth of its burrow at a place. Null if its model is not in the game.</summary>
        public static Ambusher Create(Vector3 position, Quaternion rotation, Transform parent, CreatureConfig config, string name = null)
        {
            if (ModelLibrary.Find(AmbusherLayout.Model) == null)
            {
                Debug.LogWarning("Orbital Ante: the Ambusher's model (Resources/Models/" + AmbusherLayout.Model + ") is missing, so none was made.");
                return null;
            }
            var go = new GameObject(name ?? AmbusherLayout.Model);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var ambusher = go.AddComponent<Ambusher>();
            ambusher.Build(config != null ? config.ambusher : new AmbusherRules());
            return ambusher;
        }

        void Build(AmbusherRules rules)
        {
            _rules = rules;
            Body = new AmbusherBody(rules.hp);
            Brain = new AmbusherBrain(rules);
            // Kept in its parent's space, so it stays true if the world is moved under it.
            _home = transform.parent != null ? transform.parent.InverseTransformPoint(transform.position) : transform.position;
            // Not every creature looks for the player on the same frame.
            _nextLook = Time.time + (Everyone.Count % 8) / 32f;
            _material = ModelLibrary.MaterialFor(AmbusherLayout.Model);

            // Its hitboxes move with it every frame: a kinematic body tells the physics so.
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            for (int i = 0; i < AmbusherLayout.Nodes; i++)
            {
                bool head = i == 0;
                // The node itself is never scaled, so wounds and colliders on it keep their shape; its mesh is.
                var node = new GameObject(head ? "Head" : "Body " + (i - 1));
                node.layer = Layers.Hitbox;
                node.transform.SetParent(transform, false);
                _node[i] = node.transform;
                Transform mesh = Mesh(head ? AmbusherLayout.HeadPiece : AmbusherLayout.SegPiece, node.transform);
                _nodeDrawn[i] = mesh.GetComponent<MeshRenderer>();
                var box = node.AddComponent<BoxCollider>();
                if (head)
                {
                    box.center = new Vector3(0f, 0f, ((float)AmbusherLayout.HeadLength - (float)AmbusherLayout.Spacing) * 0.5f);
                    box.size = new Vector3((float)AmbusherLayout.HeadHalfWidth * 2f, (float)AmbusherLayout.HeadHalfHeight * 2f, (float)AmbusherLayout.HeadLength);
                    Tag(node, AmbusherPiece.Head, 0);
                }
                else
                {
                    float w = (float)AmbusherLayout.Scale(i - 1);
                    mesh.localScale = new Vector3(w, w, 1f);
                    box.size = new Vector3((float)AmbusherLayout.SegHalfWidth * 2f * w, (float)AmbusherLayout.SegHalfHeight * 2f * w, (float)AmbusherLayout.Spacing);
                    Tag(node, AmbusherPiece.Seg, i - 1);
                }
                _colliders.Add(box);
            }
            for (int side = 0; side < AmbusherLayout.Jaws; side++)
            {
                // The left jaw is the right one rolled half a turn by the pose: nothing is mirrored.
                Transform jaw = Mesh(AmbusherLayout.JawPiece, transform);
                jaw.name = side == AmbusherLayout.RightSide ? "Right jaw" : "Left jaw";
                var box = jaw.gameObject.AddComponent<BoxCollider>();
                // Thicker than the jaw is drawn, so one can be shot off on purpose.
                box.center = new Vector3(-0.06f, 0f, (float)AmbusherLayout.JawLength * 0.5f);
                box.size = new Vector3(0.24f, 0.14f, (float)AmbusherLayout.JawLength);
                Tag(jaw.gameObject, AmbusherPiece.Jaw, side);
                _jaw[side] = jaw;
                _colliders.Add(box);
            }
            for (int feeler = 0; feeler < AmbusherLayout.Feelers; feeler++)
            {
                Transform piece = Mesh(AmbusherLayout.FeelerPiece, transform);
                piece.name = "Feeler " + feeler;
                var capsule = piece.gameObject.AddComponent<CapsuleCollider>();
                capsule.direction = 2;
                capsule.radius = 0.055f;
                capsule.height = (float)AmbusherLayout.FeelerLength;
                capsule.center = new Vector3(0f, 0.05f, (float)AmbusherLayout.FeelerLength * 0.5f);
                Tag(piece.gameObject, AmbusherPiece.Feeler, feeler);
                _feeler[feeler] = piece;
                _colliders.Add(capsule);
            }
            // The ring of ground round its hole is only drawn.
            foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>()) _drawn.Add(renderer);
            _rim = Mesh(AmbusherLayout.RimPiece, transform, false);
            ModelDetail.Attach(gameObject, AmbusherLayout.Model, DetailRule.BodyRange, true);
            Everyone.Add(this);
            Pose(0.0001f, new AmbusherOrders(), new AmbusherSenses());
        }

        Transform Mesh(string piece, Transform parent, bool hitbox = true)
        {
            var go = new GameObject(piece);
            go.layer = hitbox ? Layers.Hitbox : Layers.Gib;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = ModelLibrary.PartMesh(AmbusherLayout.Model, piece);
            Quiet(go.AddComponent<MeshRenderer>());
            return go.transform;
        }

        void Quiet(MeshRenderer renderer)
        {
            renderer.sharedMaterial = _material;
            // Like the other enemies, it casts no real-time shadow: on a Deck that would draw it again per cascade.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        void Tag(GameObject go, AmbusherPiece piece, int index)
        {
            var part = go.AddComponent<AmbusherPart>();
            part.Owner = this;
            part.Piece = piece;
            part.Index = index;
        }

        void OnDestroy() => Everyone.Remove(this);

        static Vector3 V(Vec3d v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        static Quaternion Q(QuatD q) => new Quaternion((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);
        static Vec3d D(Vector3 v) => new Vec3d(v.x, v.y, v.z);

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            if (!Alive || GameSession.Paused) return;
            // A long frame is taken as a short one, so a strike is never over between two frames.
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;

            Transform parent = transform.parent;
            Vector3 home = parent != null ? parent.TransformPoint(_home) : _home;
            Vector3 up = transform.up;
            var senses = new AmbusherSenses { Home = D(transform.InverseTransformPoint(home)), ClearAbove = _clearAbove, Noise = CombatWorld.PlayerHeardFrom };
            Transform eye = CombatWorld.PlayerPresent ? CombatWorld.PlayerEye : null;
            bool hostile = eye != null && !CombatWorld.EnemiesHoldFire;
            Vector3 feet = hostile ? eye.position - up * 1.6f : Vector3.zero;
            if (hostile)
            {
                senses.Hostile = true;
                senses.Player = D(transform.InverseTransformPoint(feet));
                // How the player is moving, so it can strike, and come up, where they are going.
                if (_playerKnown) senses.PlayerVelocity = D(transform.InverseTransformDirection((feet - _playerWas) / dt));
                _playerWas = feet;
                _playerKnown = true;
            }
            else
            {
                _playerKnown = false;
            }
            bool striking = Brain.Mode == AmbusherMode.Strike;
            if (Time.time >= _nextLook || striking)
            {
                // Four looks a second (and every frame of a strike, which is over in a few).
                _nextLook = Time.time + 0.25f;
                Vector3 mouth = transform.position + up * 0.5f;
                _clearLine = hostile && !Physics.Linecast(mouth, feet + up * 1.1f, Layers.WorldMask, QueryTriggerInteraction.Ignore);
            }
            // Under the ground (every frame, it travels fast): is there ground here to come up through, with nothing
            // solid standing on it? People and creatures do not count; it comes up under them gladly.
            if (Brain.Buried)
                _clearAbove = !Physics.CheckSphere(transform.position + up * 0.75f, 0.55f, Layers.WorldMask, QueryTriggerInteraction.Ignore)
                    && Physics.Raycast(transform.position + up * 0.3f, -up, 0.6f, Layers.WorldMask, QueryTriggerInteraction.Ignore);
            senses.ClearLine = _clearLine;

            AmbusherOrders orders = Brain.Think(Body, senses, dt);
            if (orders.Mode != _lastMode)
            {
                Vector3 at = transform.position + up * 0.3f;
                // The warning is heard as well as seen (an enemy's warning stands in until its own sound is there).
                if (orders.Mode == AmbusherMode.Coil && !Audio.AudioDirector.Play(CoilSound, at)) Audio.AudioDirector.Play("enemy.flare.raise", at, 0.8f, 1.3f);
                if (orders.Mode == AmbusherMode.Strike) Audio.AudioDirector.Play(StrikeSound, at);
                if (orders.Mode == AmbusherMode.Rise && !Audio.AudioDirector.Play(RiseSound, at)) Audio.AudioDirector.Play("impact.dirt", at, 0.8f, 0.7f);
                _lastMode = orders.Mode;
            }
            Move(orders, dt);
            _felt = hostile && senses.Player.Magnitude < AmbusherBrain.FeelRange(_rules, Body, senses.Noise);
            Solid(!Brain.Buried);
            Pose(dt, orders, senses);
            // Out of the ground it moves fast: its hitboxes must be where it is drawn when a shot is traced.
            if (_solid && (orders.Mode == AmbusherMode.Coil || orders.Mode == AmbusherMode.Strike || (_clearLine && orders.Mode != AmbusherMode.Lurk))) Physics.SyncTransforms();
            if (hostile && orders.Bite > 0.0) Bite(eye, (float)orders.Bite, orders.Landed);
            if (hostile && orders.Holding) Drag(eye, transform.TransformDirection(V(orders.Pull)));
        }

        /// <summary>Wholly under the ground it cannot be hit, and its hitboxes cost nothing.</summary>
        void Solid(bool solid)
        {
            if (solid == _solid) return;
            _solid = solid;
            for (int i = 0; i < _colliders.Count; i++)
                if (_colliders[i] != null) _colliders[i].enabled = solid;
            // Nor is any of it drawn down there.
            for (int i = 0; i < _drawn.Count; i++)
                if (_drawn[i] != null) _drawn[i].enabled = solid;
        }

        Vector3 HeadPoint() => _node[0] != null ? _node[0].position : transform.position;

        void Bite(Transform eye, float damage, bool first)
        {
            Vector3 from = HeadPoint(), chest = eye.position - transform.up * 0.35f;
            if (first) Audio.AudioDirector.Play("gore.splat", chest, 0.6f, 0.9f);
            PlayerHealth player = CombatWorld.Player;
            Vector3 way = chest - from;
            if (player != null) player.TakeDamage(damage, way.sqrMagnitude > 0.0001f ? way.normalized : transform.forward);
        }

        /// <summary>Whoever it holds goes where it drags them; they can still look round and shoot.</summary>
        void Drag(Transform eye, Vector3 velocity)
        {
            if (_walker == null && !_lookedForWalker)
            {
                _walker = eye.GetComponentInParent<FpsController>();
                _lookedForWalker = true;
            }
            if (_walker != null) _walker.Carry(velocity);
        }

        /// <summary>It only ever moves under the ground, where nothing is in its way. It keeps to the level it is on.</summary>
        void Move(in AmbusherOrders orders, float dt)
        {
            Vector3 up = transform.up;
            if (orders.TurnRate != 0.0) transform.rotation = Quaternion.AngleAxis((float)orders.TurnRate * Mathf.Rad2Deg * dt, up) * transform.rotation;
            Vector3 local = V(orders.Velocity) * dt;
            if (local.sqrMagnitude <= 0f) return;
            Vector3 step = transform.TransformDirection(local);
            transform.position += step - up * Vector3.Dot(step, up);
        }

        void Pose(float dt, in AmbusherOrders orders, in AmbusherSenses senses)
        {
            var input = new AmbusherAnimInput { Mode = orders.Mode, T = orders.T, Out = orders.Out, Feels = _felt, Player = senses.Player, Slack = orders.Slack };
            _animator.Update(Body, input, dt, _pose);
            Mound();
            // Wholly under the ground nothing of it is drawn or can be hit: its pieces are left where they are.
            if (Brain.Buried && !_solid) return;
            for (int i = 0; i < AmbusherLayout.Nodes; i++)
            {
                if (_node[i] == null) continue;
                _node[i].SetLocalPositionAndRotation(V(_pose.NodePosition[i]), Q(_pose.NodeRotation[i]));
                // What is down the burrow is not drawn.
                bool shown = _pose.NodePosition[i].Y > -0.3;
                if (_nodeDrawn[i] != null && _nodeDrawn[i].enabled != shown) _nodeDrawn[i].enabled = shown;
            }
            for (int side = 0; side < AmbusherLayout.Jaws; side++)
                if (_jaw[side] != null) _jaw[side].SetLocalPositionAndRotation(V(_pose.JawPosition[side]), Q(_pose.JawRotation[side]));
            for (int feeler = 0; feeler < AmbusherLayout.Feelers; feeler++)
                if (_feeler[feeler] != null) _feeler[feeler].SetLocalPositionAndRotation(V(_pose.FeelerPosition[feeler]), Q(_pose.FeelerRotation[feeler]));
        }

        void Mound()
        {
            if (_rim != null)
            {
                // The ring of ground sinks flat when it has gone, swells as it comes up, and runs low while it tunnels.
                float mound = Mathf.Clamp01((float)_pose.Mound);
                bool shown = mound > 0.02f;
                if (_rim.gameObject.activeSelf != shown) _rim.gameObject.SetActive(shown);
                if (shown) _rim.localScale = new Vector3(0.75f + 0.25f * mound, mound, 0.75f + 0.25f * mound);
            }
        }

        // ------------------------------------------------------------------ being hit

        public HitOutcome TakeHit(AmbusherPart part, in HitInfo hit)
        {
            var outcome = new HitOutcome { Hit = true };
            // Under the ground, and until its head is out of it again, nothing reaches it.
            if (!Alive || Brain.Buried || (Brain.Mode == AmbusherMode.Rise && _pose.NodePosition[0].Y < 0.0)) return outcome;
            outcome.Enemy = true;
            AmbusherHit result;
            bool armour = false;
            switch (part.Piece)
            {
                case AmbusherPiece.Jaw:
                    result = Body.HitJaw(part.Index, hit.Damage, hit.Sever);
                    outcome.Label = "Ambusher jaw";
                    armour = result.JawLost < 0;
                    if (result.JawLost >= 0)
                    {
                        LoseJaw(result.JawLost, hit.Direction);
                        // A jaw gone calls off a warning and lets go of whoever it holds.
                        if (Brain.Flinch(Body)) _animator.Flinch(new Vec3d(Random.Range(-2f, 2f), 0, -3.0));
                    }
                    outcome.Severed = result.JawLost >= 0;
                    break;
                case AmbusherPiece.Feeler:
                    result = Body.HitFeeler(part.Index, hit.Damage, hit.Sever);
                    outcome.Label = "Ambusher feeler";
                    if (result.FeelerLost >= 0) LoseFeeler(result.FeelerLost, hit.Direction);
                    outcome.Severed = result.FeelerLost >= 0;
                    break;
                case AmbusherPiece.Seg:
                    result = Body.HitSeg(part.Index, hit.Damage, hit.Sever);
                    outcome.Label = "Ambusher body";
                    outcome.Severed = result.CutAt >= 0;
                    break;
                default:
                    result = Body.HitHead(hit.Damage);
                    outcome.Label = "Ambusher head";
                    armour = true;
                    break;
            }
            bool killed = result.Killed;
            // A one-hit-kill gun kills wherever it lands, after the struck piece has taken its hit.
            if (hit.Lethal && !killed) killed = Body.HitHead(0, lethal: true).Killed;
            if (!killed) Brain.Hurt(hit.Damage, Body);
            Vector3 push = transform.InverseTransformDirection(hit.Direction);
            _animator.Flinch(new Vec3d(push.x, 0, push.z) * (outcome.Severed ? 1.5 : 0.5));
            GoreFx gore = GoreFx.Instance;
            // Its head and jaws are armour and say so: no blood, a dull knock, until they break. Its body bleeds.
            if (armour && !killed) Audio.AudioDirector.Play("impact.armor", hit.Point, 0.7f, 0.85f);
            else if (gore != null) gore.Burst(hit.Point, -hit.Direction, outcome.Severed ? 16 : 8, outcome.Severed ? 6f : 4.5f);
            if (killed)
            {
                outcome.Killed = true;
                Burst(hit);
            }
            outcome.Juice = outcome.Killed ? 1f : outcome.Severed ? 0.6f : armour ? 0.04f : 0.2f;
            return outcome;
        }

        Transform LooseParent => CombatWorld.DropParent != null ? CombatWorld.DropParent : transform.parent;

        void LoseJaw(int side, Vector3 direction)
        {
            Transform jaw = _jaw[side];
            if (jaw == null) return;
            _jaw[side] = null;
            Transform head = _node[0];
            float s = (float)AmbusherLayout.Sign(side);
            Vector3 up = transform.up, outward = head != null ? head.TransformDirection(new Vector3(s, 0f, 0.3f).normalized) : up;
            // The wound on the head, looking out along where the jaw was, and on the jaw, looking back.
            if (head != null) Wound(head, V(AmbusherLayout.HingeOf(side)), Quaternion.LookRotation(new Vector3(s * 0.5f, 0f, 1f), Vector3.up), (float)AmbusherLayout.JawThick * 1.3f);
            WalkerChunk chunk = Throw(jaw, "Ambusher jaw", 3f, direction * 3f + outward * Random.Range(3f, 5f) + up * Random.Range(3f, 5f));
            Wound(jaw, Vector3.zero, Quaternion.Euler(0f, 180f, 0f), (float)AmbusherLayout.JawThick * 1.3f);
            Pop(chunk.transform.position, outward, 14, "gore.pop.limb");
        }

        void LoseFeeler(int index, Vector3 direction)
        {
            Transform feeler = _feeler[index];
            if (feeler == null) return;
            _feeler[index] = null;
            Vector3 up = transform.up;
            WalkerChunk chunk = Throw(feeler, "Ambusher feeler", 0.6f, direction * 3f + feeler.forward * Random.Range(2f, 4f) + up * Random.Range(2f, 4f));
            Wound(feeler, Vector3.zero, Quaternion.Euler(0f, 180f, 0f), (float)AmbusherLayout.FeelerRadius * 1.6f);
            Pop(chunk.transform.position, feeler.forward, 5, "gore.pop.small");
        }

        void Pop(Vector3 at, Vector3 way, int drops, string sound)
        {
            GoreFx gore = GoreFx.Instance;
            if (gore != null) gore.Burst(at, way, drops, 5f);
            if (!Audio.AudioDirector.Play(sound, at, 0.9f, Random.Range(0.85f, 1.05f))) Audio.AudioDirector.Play("gore.pop.small", at, 0.9f, 0.8f);
        }

        /// <summary>A piece leaves the animal as a loose physics object.</summary>
        WalkerChunk Throw(Transform piece, string name, float mass, Vector3 velocity)
        {
            WalkerChunk chunk = WalkerChunk.Make(name, piece.position, piece.rotation, LooseParent, mass, _rules.pieceLife, _rules.maxLoosePieces);
            Forget(piece);
            Loosen(piece, chunk.transform);
            chunk.Body.linearVelocity = velocity;
            chunk.Body.angularVelocity = Random.onUnitSphere * Random.Range(3f, 8f);
            return chunk;
        }

        /// <summary>A piece that has left is no longer switched off and on with the animal.</summary>
        void Forget(Transform piece)
        {
            foreach (Collider collider in piece.GetComponents<Collider>()) _colliders.Remove(collider);
            foreach (MeshRenderer renderer in piece.GetComponentsInChildren<MeshRenderer>())
            {
                _drawn.Remove(renderer);
                renderer.enabled = true;
            }
        }

        static void Loosen(Transform piece, Transform chunk, bool solid = true)
        {
            foreach (Collider collider in piece.GetComponents<Collider>())
            {
                // What only rides along has its collider switched off before it is moved, so the physics never takes
                // it up; what is thrown has it switched on, in case it was under the ground a moment ago.
                collider.enabled = solid;
            }
            piece.SetParent(chunk, true);
            foreach (Transform t in piece.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Gib;
            var part = piece.GetComponent<AmbusherPart>();
            if (part != null)
            {
                // Destroy waits for the end of the frame; a second pellet this frame must not find the animal through it.
                part.Owner = null;
                Destroy(part);
            }
            GibManager gibs = GibManager.Instance;
            if (solid && gibs != null) foreach (Collider collider in piece.GetComponents<Collider>()) collider.sharedMaterial = gibs.BouncyMaterial;
        }

        void Wound(Transform on, Vector3 at, Quaternion facing, float size)
        {
            Mesh mesh = ModelLibrary.PartMesh(AmbusherLayout.Model, AmbusherLayout.Stump);
            if (mesh == null) return;
            var go = new GameObject("Wound");
            go.layer = on.gameObject.layer;
            go.transform.SetParent(on, false);
            go.transform.SetLocalPositionAndRotation(at, facing);
            go.transform.localScale = Vector3.one * (size / (float)AmbusherLayout.StumpRadius);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            Quiet(renderer);
            // A wound on a piece still part of the animal goes under the ground with it.
            if (on.IsChildOf(transform)) { _drawn.Add(renderer); renderer.enabled = _solid; }
        }

        /// <summary>
        /// It dies: whatever is out of the ground comes apart at every joint, and what was inside comes out. What is
        /// still down the burrow stays there. Shot through, the two ends at the cut show the wound.
        /// </summary>
        void Burst(in HitInfo hit)
        {
            Vector3 up = transform.up;
            int cut = Body.CutAt;
            Vector3 middle = cut >= 0 && _node[cut + 1] != null ? _node[cut + 1].position : HeadPoint();
            if (Vector3.Dot(middle - transform.position, up) < 0.1f) middle = transform.position + up * 0.3f;
            float cutWide = (float)AmbusherLayout.SegHalfWidth * 0.62f;
            for (int i = 0; i < AmbusherLayout.Nodes; i++)
            {
                Transform node = _node[i];
                if (node == null) continue;
                _node[i] = null;
                // Under the ground: it stays there.
                if (Vector3.Dot(node.position - transform.position, up) < 0.02f) continue;
                Vector3 away = node.position - middle;
                away = away.sqrMagnitude > 0.0001f ? away.normalized : up;
                Vector3 velocity = away * Random.Range(1.5f, 4f) + node.right * Random.Range(-3f, 3f) + up * Random.Range(3f, 6.5f) + hit.Direction * 2f;
                WalkerChunk chunk = Throw(node, i == 0 ? "Ambusher head" : "Ambusher body", i == 0 ? 7f : 5f, velocity);
                if (cut >= 0 && i == cut + 1)
                {
                    // The length that was shot through shows the wound at both its ends.
                    Wound(node, new Vector3(0f, 0f, (float)AmbusherLayout.Spacing * 0.5f), Quaternion.identity, cutWide);
                    Wound(node, new Vector3(0f, 0f, -(float)AmbusherLayout.Spacing * 0.5f), Quaternion.Euler(0f, 180f, 0f), cutWide);
                }
                if (i != 0) continue;
                for (int side = 0; side < AmbusherLayout.Jaws; side++)
                {
                    if (_jaw[side] != null) Loosen(_jaw[side], chunk.transform, false);
                    _jaw[side] = null;
                }
                for (int feeler = 0; feeler < AmbusherLayout.Feelers; feeler++)
                {
                    if (_feeler[feeler] != null) Loosen(_feeler[feeler], chunk.transform, false);
                    _feeler[feeler] = null;
                }
            }
            for (int k = 0; k < AmbusherLayout.Meat.Length; k++)
            {
                Mesh mesh = ModelLibrary.PartMesh(AmbusherLayout.Model, AmbusherLayout.Meat[k]);
                if (mesh == null) continue;
                WalkerChunk chunk = WalkerChunk.Make("Ambusher meat", middle + Random.insideUnitSphere * 0.1f + up * 0.15f, Random.rotation, LooseParent, 3f, _rules.pieceLife, _rules.maxLoosePieces);
                chunk.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                Quiet(chunk.gameObject.AddComponent<MeshRenderer>());
                var ball = chunk.gameObject.AddComponent<SphereCollider>();
                ball.radius = 0.09f;
                chunk.Body.linearVelocity = (Random.onUnitSphere + up).normalized * Random.Range(4f, 8f);
                chunk.Body.angularVelocity = Random.onUnitSphere * 8f;
            }
            GoreFx gore = GoreFx.Instance;
            if (gore != null)
            {
                gore.Burst(middle, up, 45, 9f);
                gore.Burst(middle, hit.Direction, 25, 7f);
            }
            Audio.AudioDirector.Play("gore.pop.torso", middle, 1f, 0.85f);
            // The ring of ground stays where it was for as long as the pieces lie.
            if (_rim != null && _rim.gameObject.activeSelf)
            {
                _rim.SetParent(LooseParent, true);
                Destroy(_rim.gameObject, _rules.pieceLife);
                _rim = null;
            }
            ModelDetail.LetGo(gameObject);
            Died?.Invoke(this);
            Everyone.Remove(this);
            Destroy(gameObject);
        }
    }
}
