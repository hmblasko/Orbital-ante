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
    public enum StalkerPiece { Head, Seg, Leg, Gill }

    /// <summary>One hitbox of a Blind stalker: its head, a length of body or tail, a leg (either bone) or a gill.</summary>
    public sealed class StalkerPart : MonoBehaviour, IHittable
    {
        [System.NonSerialized] public Stalker Owner;
        [System.NonSerialized] public StalkerPiece Piece;
        /// <summary>Which length (0 to 10), which leg (0 to 3) or which gill (0 or 1).</summary>
        [System.NonSerialized] public int Index;

        public HitOutcome Hit(in HitInfo hit) => Owner != null ? Owner.TakeHit(this, hit) : new HitOutcome { Hit = true };

        public string DescribeForDebug()
        {
            if (Owner == null) return "Creature piece";
            StalkerBody body = Owner.Body;
            switch (Piece)
            {
                case StalkerPiece.Head: return "Stalker skull " + Mathf.CeilToInt((float)body.Head);
                case StalkerPiece.Seg: return StalkerLayout.IsTail(Index) ? "Stalker tail" : "Stalker body " + Mathf.CeilToInt((float)body.Trunk);
                case StalkerPiece.Gill: return "Stalker gill";
                default: return "Stalker leg";
            }
        }
    }

    /// <summary>
    /// The Blind stalker in the world: an alien from the olm (Matthew, 17 Nov 2026). The rules are plain C#
    /// (<see cref="StalkerBody"/>, <see cref="StalkerAnimator"/>, <see cref="StalkerBrain"/>); this turns them into
    /// objects, and works out how loud the player is.
    ///
    /// - Its head, eleven lengths, four legs (two bones each) and two gills are each an object and a hitbox. The tail
    ///   comes off where it is shot through; legs and gills come off whole (<see cref="WalkerChunk"/>, shared with the
    ///   other creatures).
    /// - It is not a physics object while alive: its brain moves its head over the ground and the body follows. "Up"
    ///   is its own transform's up, never +Y.
    /// - It is blind: no sight line is ever traced for it. What it gets is how loud the player is and where they are,
    ///   and its brain decides from those what it can hear and feel.
    /// </summary>
    public sealed class Stalker : MonoBehaviour
    {
        public StalkerBody Body { get; private set; }
        public StalkerBrain Brain { get; private set; }
        public bool Alive => Body != null && Body.Alive;
        public event System.Action<Stalker> Died;

        static readonly List<Stalker> Everyone = new List<Stalker>();
        public static IReadOnlyList<Stalker> All => Everyone;
        public static void ResetStatics() => Everyone.Clear();

        const string HearSound = "creature.stalker.hear", GapeSound = "creature.stalker.gape", LungeSound = "creature.stalker.lunge", ClickSound = "creature.stalker.click";

        StalkerRules _rules;
        readonly StalkerAnimator _animator = new StalkerAnimator();
        readonly StalkerPose _pose = new StalkerPose();
        readonly Transform[] _node = new Transform[StalkerLayout.Nodes];
        readonly Transform[] _leg = new Transform[StalkerLayout.Legs], _shin = new Transform[StalkerLayout.Legs];
        readonly Transform[] _gill = new Transform[StalkerLayout.Gills];
        Transform _jaw;
        Vector3 _home, _north, _playerWas;
        bool _blocked, _playerKnown, _lookedForPlayer;
        float _playerSpeed, _restedFor, _unposed;
        FpsController _walker;
        PlayerWeapons _guns;
        StalkerMode _lastMode = StalkerMode.Still;
        Material _material;

        /// <summary>Makes a Blind stalker with its head at a place. Null if its model is not in the game.</summary>
        public static Stalker Create(Vector3 position, Quaternion rotation, Transform parent, CreatureConfig config, string name = null)
        {
            if (ModelLibrary.Find(StalkerLayout.Model) == null)
            {
                Debug.LogWarning("Orbital Ante: the Blind stalker's model (Resources/Models/" + StalkerLayout.Model + ") is missing, so none was made.");
                return null;
            }
            var go = new GameObject(name ?? StalkerLayout.Model);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var stalker = go.AddComponent<Stalker>();
            stalker.Build(config != null ? config.stalker : new StalkerRules());
            return stalker;
        }

        void Build(StalkerRules rules)
        {
            _rules = rules;
            Body = new StalkerBody(rules.hp);
            Brain = new StalkerBrain(rules);
            _home = transform.position;
            // "North" for the places it keeps in mind: any fixed direction along the ground will do.
            _north = transform.forward;
            _material = ModelLibrary.MaterialFor(StalkerLayout.Model);

            // Its hitboxes move with it every frame: a kinematic body tells the physics so.
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            for (int i = 0; i < StalkerLayout.Nodes; i++)
            {
                bool head = i == 0, tail = !head && StalkerLayout.IsTail(i - 1);
                // The node itself is never scaled, so wounds and colliders on it keep their shape; its mesh is.
                var node = new GameObject(head ? "Head" : tail ? "Tail " + (i - 1) : "Body " + (i - 1));
                node.layer = Layers.Hitbox;
                node.transform.SetParent(transform, false);
                _node[i] = node.transform;
                Transform mesh = Mesh(head ? StalkerLayout.HeadPiece : tail ? StalkerLayout.TailPiece : StalkerLayout.SegPiece, node.transform);
                var box = node.AddComponent<BoxCollider>();
                if (head)
                {
                    box.center = new Vector3(0f, 0f, ((float)StalkerLayout.HeadLength - (float)StalkerLayout.Spacing) * 0.5f);
                    box.size = new Vector3((float)StalkerLayout.HeadHalfWidth * 2f, (float)StalkerLayout.HeadHalfHeight * 2f, (float)StalkerLayout.HeadLength);
                    Tag(node, StalkerPiece.Head, 0);
                }
                else
                {
                    float w = (float)StalkerLayout.Scale(i - 1);
                    float h = (float)StalkerLayout.Tall(i - 1);
                    mesh.localScale = new Vector3(w, h, 1f);
                    float halfWide = (float)(tail ? StalkerLayout.TailHalfWidth : StalkerLayout.SegHalfWidth), halfTall = (float)(tail ? StalkerLayout.TailHalfHeight : StalkerLayout.SegHalfHeight);
                    // A length of tail is thin: its box is a little wider than it is drawn, so it can be hit on purpose.
                    box.size = new Vector3(Mathf.Max(0.12f, halfWide * 2f * w), halfTall * 2f * h, (float)StalkerLayout.Spacing);
                    Tag(node, StalkerPiece.Seg, i - 1);
                }
            }
            for (int leg = 0; leg < StalkerLayout.Legs; leg++)
            {
                // Both bones are the same either side (their upper side is turned by the pose), so nothing is mirrored.
                _leg[leg] = Limb(StalkerLayout.LegPiece, "Leg " + leg, (float)StalkerLayout.LegLength, StalkerPiece.Leg, leg);
                _shin[leg] = Limb(StalkerLayout.ShinPiece, "Shin " + leg, (float)StalkerLayout.ShinLength, StalkerPiece.Leg, leg);
            }
            for (int side = 0; side < StalkerLayout.Gills; side++)
            {
                Transform gill = Mesh(StalkerLayout.GillPiece, transform);
                gill.name = side == StalkerLayout.RightSide ? "Right gill" : "Left gill";
                var ball = gill.gameObject.AddComponent<SphereCollider>();
                // The whole tuft, and a little more: it is what you shoot to deafen it.
                ball.center = new Vector3(0f, 0.04f, (float)StalkerLayout.GillLength * 0.55f);
                ball.radius = (float)StalkerLayout.GillLength * 0.5f;
                Tag(gill.gameObject, StalkerPiece.Gill, side);
                _gill[side] = gill;
            }
            // The jaw is only drawn: a hit on it lands on the skull behind it or passes.
            _jaw = Mesh(StalkerLayout.JawPiece, transform, false);
            ModelDetail.Attach(gameObject, StalkerLayout.Model, DetailRule.BodyRange, true);
            Everyone.Add(this);
            Pose(0.0001f, new StalkerOrders(), Vector3.zero);
        }

        Transform Limb(string piece, string name, float length, StalkerPiece kind, int index)
        {
            Transform limb = Mesh(piece, transform);
            limb.name = name;
            var capsule = limb.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = 2;
            // Thicker than the leg is drawn, so a leg can be picked off on purpose.
            capsule.radius = 0.075f;
            capsule.height = length + 0.08f;
            capsule.center = new Vector3(0f, 0f, length * 0.5f);
            Tag(limb.gameObject, kind, index);
            return limb;
        }

        Transform Mesh(string piece, Transform parent, bool hitbox = true)
        {
            var go = new GameObject(piece);
            go.layer = hitbox ? Layers.Hitbox : Layers.Gib;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = ModelLibrary.PartMesh(StalkerLayout.Model, piece);
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

        void Tag(GameObject go, StalkerPiece piece, int index)
        {
            var part = go.AddComponent<StalkerPart>();
            part.Owner = this;
            part.Piece = piece;
            part.Index = index;
        }

        void OnDestroy() => Everyone.Remove(this);

        static Vector3 V(Vec3d v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        static Quaternion Q(QuatD q) => new Quaternion((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);
        static Vec3d D(Vector3 v) => new Vec3d(v.x, v.y, v.z);

        // ------------------------------------------------------------------ every frame

        /// <summary>
        /// How loud the player is to it. In the suit that is the game's own figure (stance, jets, shots). The arena's
        /// player has no stances, so there it is worked out from how fast they move and whether they have just fired:
        /// standing still is silent, a walk is 1, a sprint more, a shot most.
        /// </summary>
        float Noise(Transform eye, float dt)
        {
            if (!_lookedForPlayer)
            {
                _lookedForPlayer = true;
                _walker = eye.GetComponentInParent<FpsController>();
                _guns = eye.GetComponentInChildren<PlayerWeapons>();
                if (_guns == null) _guns = eye.GetComponentInParent<PlayerWeapons>();
            }
            if (_walker == null) return CombatWorld.PlayerHeardFrom;
            Vector3 at = eye.position;
            if (_playerKnown)
            {
                // Along the ground only: a jump or a fall is not a footstep.
                float speed = Vector3.ProjectOnPlane(at - _playerWas, transform.up).magnitude / dt;
                // Eased, so a single odd frame is not a noise.
                _playerSpeed = Mathf.Lerp(_playerSpeed, speed, Mathf.Min(1f, dt * 10f));
            }
            _playerWas = at;
            _playerKnown = true;
            float noise = _playerSpeed < 0.4f ? 0f : Mathf.Clamp(_playerSpeed / 5f, 0.3f, 1.3f);   // a sprint carries further but is not "loud"
            if (_guns != null && Time.time - _guns.LastShotTime < 0.35f) noise = Mathf.Max(noise, 2.2f);
            return noise;
        }

        void Update()
        {
            if (!Alive || GameSession.Paused) return;
            // A long frame is taken as a short one, so a lunge never steps past thin cover or past the player.
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;

            // (Noise is worked out over the frame's real length, or a slow frame would make a walk a sprint.)
            StalkerSenses senses = Senses(Time.deltaTime, out Transform eye, out bool hostile);
            StalkerOrders orders = Brain.Think(Body, senses, dt);
            if (orders.Mode != _lastMode)
            {
                Vector3 at = HeadPoint();
                // Each change the player must notice is heard (other sounds stand in until its own are there).
                if (orders.Mode == StalkerMode.Listen && !Audio.AudioDirector.Play(HearSound, at)) Audio.AudioDirector.Play("enemy.flare.raise", at, 0.4f, 0.7f);
                if (orders.Mode == StalkerMode.Gape && !Audio.AudioDirector.Play(GapeSound, at)) Audio.AudioDirector.Play("enemy.flare.raise", at, 0.8f, 1.2f);
                if (orders.Mode == StalkerMode.Lunge) Audio.AudioDirector.Play(LungeSound, at);
                _lastMode = orders.Mode;
            }
            // Each time it takes a new place it clicks, quietly: where you are now is known.
            else if (orders.Took && orders.Mode != StalkerMode.Still) Audio.AudioDirector.Play(ClickSound, HeadPoint(), 0.5f, 1f);
            Vector3 moved = Move(orders, dt);
            // Lying still with nothing to hear (most of its life) it is posed five times a second, not every frame:
            // only the tip of its tail and its gills drift then.
            bool resting = orders.Mode == StalkerMode.Still && !orders.Aware;
            _restedFor = resting ? _restedFor + dt : 0f;
            _unposed += dt;
            bool pose = _restedFor < 3f || _unposed >= 0.2f;
            if (pose)
            {
                Pose(_unposed, orders, moved / dt);
                _unposed = 0f;
            }
            // Near the player, on a frame it has changed, its hitboxes must be where it is drawn when their shot is
            // traced: its legs and gills are thin.
            bool changed = moved != Vector3.zero || orders.TurnRate != 0.0 || (pose && !resting);
            // (Whether or not it is after them: in the arena's calm it can still be shot.)
            Transform near = CombatWorld.PlayerEye;
            if (changed && near != null && (near.position - transform.position).sqrMagnitude < 900f) Physics.SyncTransforms();
            if (orders.Bite > 0.0 && hostile) Bite(eye, (float)orders.Bite);
        }

        StalkerSenses Senses(float dt, out Transform eye, out bool hostile)
        {
            var senses = new StalkerSenses { Blocked = _blocked, Home = D(transform.InverseTransformPoint(_home)), North = D(transform.InverseTransformDirection(_north)) };
            eye = CombatWorld.PlayerPresent ? CombatWorld.PlayerEye : null;
            hostile = eye != null && !CombatWorld.EnemiesHoldFire;
            if (hostile)
            {
                senses.Hostile = true;
                senses.Player = D(transform.InverseTransformPoint(eye.position - transform.up * 1.6f));
                // (Asked with no time passed, by a hit, it is only wanted for where the player is.)
                if (dt > 0f) senses.Noise = Noise(eye, dt);
            }
            else if (dt > 0f)
            {
                _playerKnown = false;
                _playerSpeed = 0f;
            }
            return senses;
        }

        Vector3 HeadPoint() => _node[0] != null ? _node[0].position : transform.position;

        void Bite(Transform eye, float damage)
        {
            Vector3 from = HeadPoint() + transform.up * 0.2f, chest = eye.position - transform.up * 0.35f;
            // Never through a wall.
            if (Physics.Linecast(from, chest, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return;
            Audio.AudioDirector.Play("gore.splat", chest, 0.5f, 1.0f);
            PlayerHealth player = CombatWorld.Player;
            Vector3 way = chest - from;
            if (player != null) player.TakeDamage(damage, way.sqrMagnitude > 0.0001f ? way.normalized : transform.forward);
        }

        /// <summary>Moves the head; the answer is how far it really went, in its own frame after the turn.</summary>
        Vector3 Move(in StalkerOrders orders, float dt)
        {
            Vector3 up = transform.up;
            if (orders.TurnRate != 0.0) transform.rotation = Quaternion.AngleAxis((float)orders.TurnRate * Mathf.Rad2Deg * dt, up) * transform.rotation;
            Vector3 local = V(orders.Velocity) * dt;
            // What stopped it last frame is asked about again only when it next tries to move, so it can turn away.
            if (local.sqrMagnitude <= 0f) { _blocked = false; return Vector3.zero; }
            Vector3 to = transform.position + transform.TransformDirection(local);
            // Stopped by anything in front of its head; the check is kept clear of the ground it is on.
            // The whole step is swept, so a long frame of a lunge never carries it through thin cover.
            Vector3 step = to - transform.position, probe = up * 0.46f + transform.forward * 0.36f;
            if (Physics.CheckSphere(to + probe, 0.24f, Layers.WorldMask, QueryTriggerInteraction.Ignore)
                || Physics.SphereCast(transform.position + probe, 0.24f, step.normalized, out _, step.magnitude, Layers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                _blocked = true;
                return Vector3.zero;
            }
            // Stay on the ground: look for it from above where it is going.
            if (Physics.Raycast(to + up * 3f, -up, out RaycastHit ground, 9f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                // A step up or down of more than a shin is a wall to it for now (it is placed on level ground).
                if (Mathf.Abs(Vector3.Dot(ground.point - to, up)) > 0.3f)
                {
                    _blocked = true;
                    return Vector3.zero;
                }
                to = ground.point;
            }
            _blocked = false;
            transform.position = to;
            return local;
        }

        void Pose(float dt, in StalkerOrders orders, Vector3 velocity)
        {
            var input = new StalkerAnimInput { Velocity = D(velocity), TurnRate = orders.TurnRate, Mode = orders.Mode, T = orders.T, HasPlace = orders.HasPlace, Place = orders.Place, Aware = orders.Aware, Took = orders.Took };
            _animator.Update(Body, input, dt, _pose);
            for (int i = 0; i < _pose.Nodes; i++)
                if (_node[i] != null) _node[i].SetLocalPositionAndRotation(V(_pose.NodePosition[i]), Q(_pose.NodeRotation[i]));
            for (int leg = 0; leg < StalkerLayout.Legs; leg++)
            {
                if (_leg[leg] != null) _leg[leg].SetLocalPositionAndRotation(V(_pose.LegPosition[leg]), Q(_pose.LegRotation[leg]));
                if (_shin[leg] != null) _shin[leg].SetLocalPositionAndRotation(V(_pose.ShinPosition[leg]), Q(_pose.ShinRotation[leg]));
            }
            for (int side = 0; side < StalkerLayout.Gills; side++)
                if (_gill[side] != null) _gill[side].SetLocalPositionAndRotation(V(_pose.GillPosition[side]), Q(_pose.GillRotation[side]));
            if (_jaw != null) _jaw.SetLocalPositionAndRotation(V(_pose.JawPosition), Q(_pose.JawRotation));
        }

        // ------------------------------------------------------------------ being hit

        public HitOutcome TakeHit(StalkerPart part, in HitInfo hit)
        {
            var outcome = new HitOutcome { Hit = true };
            if (!Alive) return outcome;
            outcome.Enemy = true;
            StalkerHit result;
            bool armour = false, soft = false;
            switch (part.Piece)
            {
                case StalkerPiece.Leg:
                    result = Body.HitLeg(part.Index, hit.Damage, hit.Sever);
                    outcome.Label = "Stalker leg";
                    if (result.LegLost >= 0) LoseLeg(result.LegLost, hit.Direction);
                    break;
                case StalkerPiece.Gill:
                    result = Body.HitGill(part.Index, hit.Damage, hit.Sever);
                    outcome.Label = "Stalker gill";
                    if (result.GillLost >= 0)
                    {
                        LoseGill(result.GillLost, hit.Direction);
                        // Its ear torn off: it reels, and a warning under way is over (not twice running).
                        if (Brain.Reel()) _animator.Flinch(new Vec3d(-StalkerLayout.Sign(result.GillLost) * 2.0, 1.0, -1.5));
                    }
                    break;
                case StalkerPiece.Seg:
                    result = Body.HitSeg(part.Index, hit.Damage, hit.Sever);
                    outcome.Label = StalkerLayout.IsTail(part.Index) ? "Stalker tail" : "Stalker body";
                    soft = !StalkerLayout.IsTail(part.Index);
                    if (result.TailCutAt >= 0) CutTail(result.TailCutAt, hit);
                    break;
                default:
                    result = Body.HitHead(hit.Damage);
                    outcome.Label = "Stalker skull";
                    armour = true;
                    break;
            }
            outcome.Severed = result.Lost;
            bool killed = result.Killed;
            // A one-hit-kill gun kills wherever it lands, after the struck piece has taken its hit.
            if (hit.Lethal && !killed) killed = Body.HitHead(0, lethal: true).Killed;
            if (!killed)
            {
                // Pain tells it where, as a shot's noise would: it comes at whoever did it.
                StalkerSenses senses = Senses(0f, out Transform eye, out bool hostile);
                if (hostile) Brain.Hurt(senses, senses.Player);
            }
            Vector3 push = transform.InverseTransformDirection(hit.Direction);
            _animator.Flinch(new Vec3d(push.x, 0.4, push.z) * (outcome.Severed ? 1.5 : 0.4));
            _restedFor = 0f;
            GoreFx gore = GoreFx.Instance;
            // The skull says it is bone: no blood, a dull knock, until it breaks. Everything else bleeds.
            if (armour && !killed) Audio.AudioDirector.Play("impact.armor", hit.Point, 0.7f, 0.8f);
            else if (gore != null) gore.Burst(hit.Point, -hit.Direction, outcome.Severed ? 16 : soft ? 9 : 4, outcome.Severed ? 6f : soft ? 4.5f : 3f);
            if (killed)
            {
                outcome.Killed = true;
                Burst(hit);
            }
            outcome.Juice = outcome.Killed ? 1f : outcome.Severed ? (part.Piece == StalkerPiece.Seg ? 0.8f : 0.5f) : armour ? 0.04f : soft ? 0.2f : 0.12f;
            return outcome;
        }

        Transform LooseParent => CombatWorld.DropParent != null ? CombatWorld.DropParent : transform.parent;

        void LoseLeg(int index, Vector3 direction)
        {
            Transform leg = _leg[index], shin = _shin[index];
            if (leg == null) return;
            _leg[index] = _shin[index] = null;
            Transform seg = _node[StalkerLayout.NodeOf(index)];
            float s = (float)StalkerLayout.Sign(index);
            Vector3 up = transform.up, outward = seg != null ? seg.TransformDirection(new Vector3(s, 0.2f, 0f).normalized) : up;
            // The wound on the body, looking out along where the leg was, and on the leg, looking back.
            if (seg != null) Wound(seg, V(StalkerLayout.HipOf(index)), Quaternion.LookRotation(new Vector3(s, -0.3f, 0f), Vector3.up), (float)StalkerLayout.LegRadius * 1.4f);
            WalkerChunk chunk = Throw(leg, "Stalker leg", 2f, direction * 3f + outward * Random.Range(3f, 5f) + up * Random.Range(2f, 4f));
            // The lower leg goes with the upper, still joined at the knee.
            if (shin != null) Loosen(shin, chunk.transform);
            Wound(leg, Vector3.zero, Quaternion.Euler(0f, 180f, 0f), (float)StalkerLayout.LegRadius * 1.4f);
            Pop(chunk.transform.position, outward, 10, "gore.pop.limb");
        }

        void LoseGill(int side, Vector3 direction)
        {
            Transform gill = _gill[side];
            if (gill == null) return;
            _gill[side] = null;
            Transform head = _node[0];
            float s = (float)StalkerLayout.Sign(side);
            Vector3 up = transform.up, outward = head != null ? head.TransformDirection(new Vector3(s, 0.3f, -0.3f).normalized) : up;
            if (head != null) Wound(head, V(StalkerLayout.GillRootOf(side)), Quaternion.LookRotation(new Vector3(s, 0.2f, -0.6f), Vector3.up), 0.06f);
            WalkerChunk chunk = Throw(gill, "Stalker gill", 0.8f, direction * 2f + outward * Random.Range(3f, 5f) + up * Random.Range(2f, 4f));
            Pop(chunk.transform.position, outward, 12, "gore.pop.small");
        }

        /// <summary>The tail comes off at a length: everything from there back leaves in one stiff piece.</summary>
        void CutTail(int from, in HitInfo hit)
        {
            Vector3 up = transform.up;
            Transform kept = _node[from];
            float cutWide = (float)StalkerLayout.WoundSize(from), cutAt = (float)(StalkerLayout.Spacing * 0.5 + StalkerLayout.WoundOut);
            if (kept != null) Wound(kept, new Vector3(0f, 0f, -cutAt), Quaternion.Euler(0f, 180f, 0f), Mathf.Max(0.05f, from > 0 ? (float)StalkerLayout.WoundSize(from - 1) : cutWide));
            WalkerChunk chunk = null;
            for (int seg = from; seg < StalkerLayout.Segs; seg++)
            {
                Transform node = _node[seg + 1];
                if (node == null) continue;
                _node[seg + 1] = null;
                if (chunk == null)
                {
                    chunk = Throw(node, "Stalker tail", 4f, hit.Direction * 2f + node.right * Random.Range(-2.5f, 2.5f) + up * Random.Range(3f, 5f) - node.forward * Random.Range(1f, 2.5f));
                    Wound(node, new Vector3(0f, 0f, cutAt), Quaternion.identity, Mathf.Max(0.05f, cutWide));
                }
                else Loosen(node, chunk.transform);
            }
            Vector3 at = kept != null ? kept.position : transform.position;
            Pop(at, hit.Direction, 24, "gore.pop.limb");
        }

        void Pop(Vector3 at, Vector3 way, int drops, string sound)
        {
            GoreFx gore = GoreFx.Instance;
            if (gore != null) gore.Burst(at, way, drops, 5f);
            Audio.AudioDirector.Play(sound, at, 0.9f, Random.Range(0.85f, 1.05f));
        }

        /// <summary>A piece leaves the animal as a loose physics object.</summary>
        WalkerChunk Throw(Transform piece, string name, float mass, Vector3 velocity)
        {
            WalkerChunk chunk = WalkerChunk.Make(name, piece.position, piece.rotation, LooseParent, mass, _rules.pieceLife, _rules.maxLoosePieces);
            Loosen(piece, chunk.transform);
            chunk.Body.linearVelocity = velocity;
            chunk.Body.angularVelocity = Random.onUnitSphere * Random.Range(3f, 8f);
            return chunk;
        }

        static void Loosen(Transform piece, Transform chunk, bool solid = true)
        {
            // What only rides along has its collider switched off before it is moved, so the physics never takes it up.
            if (!solid) foreach (Collider collider in piece.GetComponents<Collider>()) collider.enabled = false;
            piece.SetParent(chunk, true);
            foreach (Transform t in piece.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Gib;
            var part = piece.GetComponent<StalkerPart>();
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
            Mesh mesh = ModelLibrary.PartMesh(StalkerLayout.Model, StalkerLayout.Stump);
            if (mesh == null) return;
            var go = new GameObject("Wound");
            go.layer = on.gameObject.layer;
            go.transform.SetParent(on, false);
            go.transform.SetLocalPositionAndRotation(at, facing);
            go.transform.localScale = Vector3.one * (size / (float)StalkerLayout.StumpRadius);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Quiet(go.AddComponent<MeshRenderer>());
        }

        /// <summary>It dies: it comes apart at every joint, legs and gills thrown off, and what was inside comes out.</summary>
        void Burst(in HitInfo hit)
        {
            Vector3 up = transform.up;
            int nodes = Body.Kept + 1;
            Vector3 middle = _node[Mathf.Min(nodes - 1, 3)] != null ? _node[Mathf.Min(nodes - 1, 3)].position : transform.position;
            for (int leg = 0; leg < StalkerLayout.Legs; leg++)
                if (_leg[leg] != null) LoseLeg(leg, hit.Direction);
            for (int side = 0; side < StalkerLayout.Gills; side++)
                if (_gill[side] != null) LoseGill(side, hit.Direction);
            for (int i = 0; i < nodes; i++)
            {
                Transform node = _node[i];
                if (node == null) continue;
                _node[i] = null;
                Vector3 away = node.position - middle;
                away = away.sqrMagnitude > 0.0001f ? away.normalized : up;
                Vector3 velocity = away * Random.Range(2f, 5f) + node.right * Random.Range(-3f, 3f) + up * Random.Range(3f, 7f) + hit.Direction * 2f;
                WalkerChunk chunk = Throw(node, i == 0 ? "Stalker head" : "Stalker body", i == 0 ? 8f : 6f, velocity);
                if (i > 0)
                {
                    // Torn apart at every joint: a length has a wound at each end.
                    float size = (float)StalkerLayout.WoundSize(i - 1), out_ = (float)(StalkerLayout.Spacing * 0.5 + StalkerLayout.WoundOut);
                    Wound(node, new Vector3(0f, 0f, out_), Quaternion.identity, size);
                    if (i < nodes - 1) Wound(node, new Vector3(0f, 0f, -out_), Quaternion.Euler(0f, 180f, 0f), size);
                }
                if (i == 0 && _jaw != null)
                {
                    Loosen(_jaw, chunk.transform, false);
                    _jaw = null;
                }
            }
            for (int k = 0; k < StalkerLayout.Meat.Length; k++)
            {
                Mesh mesh = ModelLibrary.PartMesh(StalkerLayout.Model, StalkerLayout.Meat[k]);
                if (mesh == null) continue;
                WalkerChunk chunk = WalkerChunk.Make("Stalker meat", middle + Random.insideUnitSphere * 0.15f, Random.rotation, LooseParent, 3f, _rules.pieceLife, _rules.maxLoosePieces);
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
                gore.Burst(middle, up, 50, 9f);
                gore.Burst(middle, hit.Direction, 25, 7f);
            }
            Audio.AudioDirector.Play("gore.pop.torso", middle, 1f, 0.9f);
            ModelDetail.LetGo(gameObject);
            Died?.Invoke(this);
            Everyone.Remove(this);
            Destroy(gameObject);
        }
    }
}
