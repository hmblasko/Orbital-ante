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
    public enum RollerPiece { Head, Soft, Plate, Tail, Leg }

    /// <summary>One hitbox of a Roller: its head, a segment's soft body, a plate, the tail fan or a leg.</summary>
    public sealed class RollerPart : MonoBehaviour, IHittable
    {
        [System.NonSerialized] public Roller Owner;
        [System.NonSerialized] public RollerPiece Piece;
        /// <summary>Which segment (0 to 6) or which leg (0 to 13).</summary>
        [System.NonSerialized] public int Index;

        public HitOutcome Hit(in HitInfo hit) => Owner != null ? Owner.TakeHit(this, hit) : new HitOutcome { Hit = true };

        public string DescribeForDebug()
        {
            if (Owner == null) return "Creature piece";
            RollerBody body = Owner.Body;
            switch (Piece)
            {
                case RollerPiece.Head: return "Roller head " + Mathf.CeilToInt((float)body.Head);
                case RollerPiece.Soft: return "Roller soft body " + Mathf.CeilToInt((float)body.Soft);
                case RollerPiece.Plate: return "Roller plate " + Mathf.CeilToInt((float)body.PlateHp(Index));
                case RollerPiece.Tail: return "Roller tail " + Mathf.CeilToInt((float)body.TailHp);
                default: return "Roller leg " + Mathf.CeilToInt((float)body.LegHp(Index));
            }
        }
    }

    /// <summary>
    /// The Roller in the world: an alien from the giant isopod (Matthew, 17 Nov 2026). The rules are plain C#
    /// (<see cref="RollerBody"/>, <see cref="RollerAnimator"/>, <see cref="RollerBrain"/>); this turns them into
    /// objects.
    ///
    /// - Its head, seven segments, seven plates, tail and fourteen legs are each an object and a hitbox. Plates, legs
    ///   and the tail come off as loose pieces (<see cref="WalkerChunk"/>, shared with the walkers) and leave a wound.
    /// - It is not a physics object while alive: its brain moves it over the ground, walking or as a ball. "Up" is its
    ///   own transform's up, never +Y.
    /// - It dies only when its soft body or its head gives way; then it bursts into its segments.
    /// </summary>
    public sealed class Roller : MonoBehaviour
    {
        public RollerBody Body { get; private set; }
        public RollerBrain Brain { get; private set; }
        public bool Alive => Body != null && Body.Alive;
        public event System.Action<Roller> Died;

        static readonly List<Roller> Everyone = new List<Roller>();
        public static IReadOnlyList<Roller> All => Everyone;
        public static void ResetStatics() => Everyone.Clear();

        RollerRules _rules;
        readonly RollerAnimator _animator = new RollerAnimator();
        readonly RollerPose _pose = new RollerPose();
        readonly Transform[] _link = new Transform[RollerLayout.Links];
        readonly Transform[] _plate = new Transform[RollerLayout.Segs];
        readonly Transform[] _leg = new Transform[RollerLayout.Legs];
        Vector3 _home;
        bool _blocked, _sees, _hitThisRoll;
        float _nextLook;
        RollerMode _lastMode = RollerMode.Walk;
        Material _material;

        /// <summary>Makes a Roller standing at a place. Null if its model is not in the game.</summary>
        public static Roller Create(Vector3 position, Quaternion rotation, Transform parent, CreatureConfig config, int seed, string name = null)
        {
            if (ModelLibrary.Find(RollerLayout.Model) == null)
            {
                Debug.LogWarning("Orbital Ante: the Roller's model (Resources/Models/" + RollerLayout.Model + ") is missing, so none was made.");
                return null;
            }
            var go = new GameObject(name ?? RollerLayout.Model);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var roller = go.AddComponent<Roller>();
            roller.Build(config != null ? config.roller : new RollerRules(), seed);
            return roller;
        }

        void Build(RollerRules rules, int seed)
        {
            _rules = rules;
            Body = new RollerBody(rules.hp);
            Brain = new RollerBrain(rules, seed);
            _home = transform.position;
            // Not every creature looks for the player on the same frame.
            _nextLook = Time.time + (seed & 0xff) / 1024f;
            _material = ModelLibrary.MaterialFor(RollerLayout.Model);

            // Its hitboxes move with it every frame: a kinematic body tells the physics so.
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            for (int i = 0; i < RollerLayout.Links; i++)
            {
                bool head = i == RollerLayout.HeadLink, tail = i == RollerLayout.TailLink;
                float w = (float)RollerLayout.Scale(i), length = (float)RollerLayout.Length(i);
                // The link itself is never scaled, so wounds and colliders on it keep their shape; its mesh is.
                var link = new GameObject(head ? "Head" : tail ? "Tail" : "Segment " + (i - 1));
                link.layer = Layers.Hitbox;
                link.transform.SetParent(transform, false);
                _link[i] = link.transform;
                Transform mesh = Mesh(head ? RollerLayout.HeadPiece : tail ? RollerLayout.TailPiece : RollerLayout.SegPiece, link.transform);
                mesh.localScale = new Vector3(w, w, 1f);
                var box = link.AddComponent<BoxCollider>();
                if (head)
                {
                    box.center = new Vector3(0f, 0.04f, 0f);
                    box.size = new Vector3((float)RollerLayout.HeadHalfWidth * 2f, 0.40f, length);
                    Tag(link, RollerPiece.Head, 0);
                }
                else if (tail)
                {
                    box.center = new Vector3(0f, 0.05f, 0f);
                    box.size = new Vector3((float)RollerLayout.TailHalfWidth * 1.8f, 0.26f, length);
                    Tag(link, RollerPiece.Tail, 0);
                }
                else
                {
                    // The soft body: narrower and lower than the plate over it, so from above and from the side the
                    // plate is what is hit, and on its back the soft body is.
                    float top = (float)RollerLayout.BackHeight * 0.45f * w, bottom = (float)RollerLayout.BellyDepth * w;
                    box.center = new Vector3(0f, (top - bottom) * 0.5f, 0f);
                    box.size = new Vector3((float)RollerLayout.SegHalfWidth * 1.8f * w, top + bottom, length);
                    Tag(link, RollerPiece.Soft, i - 1);

                    Transform plate = Mesh(RollerLayout.PlatePiece, transform);
                    plate.name = "Plate " + (i - 1);
                    plate.localScale = new Vector3(w, w, 1f);
                    // An arch as three boxes (in the plate's own size): the roof and the two skirts.
                    float back = -(float)RollerLayout.PlateOverlap * 0.5f, deep = length + (float)RollerLayout.PlateOverlap;
                    float half = (float)RollerLayout.PlateHalfWidth, high = (float)RollerLayout.BackHeight;
                    AddBox(plate.gameObject, new Vector3(0f, high * 0.72f, back), new Vector3(half * 1.7f, high * 0.6f, deep));
                    float skirt = (float)RollerLayout.PlateSkirt, side = high * 0.45f + skirt;
                    AddBox(plate.gameObject, new Vector3(half * 0.88f, side * 0.5f - skirt, back), new Vector3(half * 0.24f, side, deep));
                    AddBox(plate.gameObject, new Vector3(-half * 0.88f, side * 0.5f - skirt, back), new Vector3(half * 0.24f, side, deep));
                    Tag(plate.gameObject, RollerPiece.Plate, i - 1);
                    _plate[i - 1] = plate;
                }
            }
            for (int leg = 0; leg < RollerLayout.Legs; leg++)
            {
                Transform piece = Mesh(RollerLayout.LegPiece, transform);
                piece.name = "Leg " + leg;
                // A leg is the same either side (its upper side is turned outward by the pose) and the same size on
                // every segment, so nothing is mirrored or scaled.
                var capsule = piece.gameObject.AddComponent<CapsuleCollider>();
                capsule.direction = 2;
                capsule.radius = 0.075f;
                capsule.height = (float)RollerLayout.LegLength + 0.075f;
                capsule.center = new Vector3(0f, 0f, (float)RollerLayout.LegLength * 0.5f);
                Tag(piece.gameObject, RollerPiece.Leg, leg);
                _leg[leg] = piece;
            }
            ModelDetail.Attach(gameObject, RollerLayout.Model, DetailRule.BodyRange * 1.1f, true);
            Everyone.Add(this);
            Pose(0.0001f, new RollerOrders());
        }

        Transform Mesh(string piece, Transform parent)
        {
            var go = new GameObject(piece);
            go.layer = Layers.Hitbox;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = ModelLibrary.PartMesh(RollerLayout.Model, piece);
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

        static void AddBox(GameObject on, Vector3 centre, Vector3 size)
        {
            var box = on.AddComponent<BoxCollider>();
            box.center = centre;
            box.size = size;
        }

        void Tag(GameObject go, RollerPiece piece, int index)
        {
            var part = go.AddComponent<RollerPart>();
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
            // A long frame is taken as a short one, so a ball never steps past thin cover or past the player.
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;

            var senses = new RollerSenses { Blocked = _blocked, Home = D(transform.InverseTransformPoint(_home)) };
            Transform eye = CombatWorld.PlayerPresent ? CombatWorld.PlayerEye : null;
            Vector3 up = transform.up;
            if (eye != null && !CombatWorld.EnemiesHoldFire)
            {
                senses.Player = D(transform.InverseTransformPoint(eye.position - up * 1.6f));
                if (Time.time >= _nextLook)
                {
                    // Four looks a second: near enough, and nothing solid between it and the player.
                    _nextLook = Time.time + 0.25f;
                    Vector3 from = Middle();
                    _sees = Vector3.Distance(from, eye.position) < _rules.sightRange * CombatWorld.PlayerSeenFrom
                            && !Physics.Linecast(from, eye.position, Layers.WorldMask, QueryTriggerInteraction.Ignore);
                }
                senses.SeesPlayer = _sees;
            }
            else
            {
                _sees = false;
            }

            RollerOrders orders = Brain.Think(Body, senses, dt);
            if (orders.Mode != _lastMode)
            {
                Vector3 at = Middle();
                if (orders.Mode == RollerMode.Wind)
                {
                    // The warning can be heard as well as seen (its own sound once there is one).
                    _hitThisRoll = false;
                    if (!Audio.AudioDirector.Play("creature.roller.curl", at)) Audio.AudioDirector.Play("enemy.flare.raise", at, 0.8f, 0.5f);
                }
                else if (orders.Mode == RollerMode.Open && Brain.Crashed)
                {
                    Audio.AudioDirector.Play("impact.armor", at, 1f, 0.45f);
                    _animator.Flinch(new Vec3d(0, 1.5, -2.5));
                }
                _lastMode = orders.Mode;
            }
            Move(orders, dt);
            Pose(dt, orders);
            if (orders.Mode == RollerMode.Roll)
            {
                // A fast ball's hitboxes must be where it is drawn when the player's shot is traced this frame.
                Physics.SyncTransforms();
                if (!_hitThisRoll && eye != null && !CombatWorld.EnemiesHoldFire && Brain.RollHits(D(transform.InverseTransformPoint(eye.position - up * 1.6f)))) Hurt(eye, _rules.rollDamage, true);
            }
            if (orders.Landed && eye != null && !CombatWorld.EnemiesHoldFire)
            {
                // A butt reaches a little past its head, straight ahead.
                Vector3 feet = transform.InverseTransformPoint(eye.position - up * 1.6f);
                if (feet.z > 0f && feet.z < _rules.buttRange + 0.4f && Mathf.Abs(feet.x) < 0.9f && feet.y < 1.2f && feet.y > -1.5f) Hurt(eye, _rules.buttDamage, false);
            }
        }

        Vector3 Middle() => transform.TransformPoint(V(_pose.Centre));

        void Hurt(Transform eye, float damage, bool roll)
        {
            Vector3 from = Middle(), chest = eye.position - transform.up * 0.35f;
            // Never through a wall.
            if (Physics.Linecast(from, chest, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return;
            if (roll) _hitThisRoll = true;
            Audio.AudioDirector.Play("gore.splat", chest, 0.5f, roll ? 0.55f : 0.8f);
            PlayerHealth player = CombatWorld.Player;
            if (player != null) player.TakeDamage(damage, (chest - from).normalized);
        }

        void Move(in RollerOrders orders, float dt)
        {
            Vector3 up = transform.up;
            if (orders.TurnRate != 0.0) transform.rotation = Quaternion.AngleAxis((float)orders.TurnRate * Mathf.Rad2Deg * dt, up) * transform.rotation;
            Vector3 step = transform.TransformDirection(V(orders.Velocity)) * dt;
            _blocked = false;
            if (step.sqrMagnitude <= 0f) return;
            Vector3 to = transform.position + step;
            // A ball is stopped by anything its own size; on foot, by anything in front of its head. Both checks are
            // kept clear of the ground it is on.
            bool ball = orders.Mode == RollerMode.Roll;
            float radius = ball ? (float)RollerLayout.BallRadius * 0.8f : 0.3f;
            float height = ball ? (float)RollerLayout.BallRadius : 0.5f;
            float ahead = ball ? 0.25f : (float)RollerLayout.TotalLength * 0.5f * Mathf.Sign((float)orders.Velocity.Z);
            if (Physics.CheckSphere(to + up * height + transform.forward * ahead, radius, Layers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                _blocked = true;
                return;
            }
            // Stay on the ground: look for it from above where it is going.
            if (Physics.Raycast(to + up * 3f, -up, out RaycastHit ground, 9f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                // A step up or down of more than a shin is a wall to it for now (it is placed on level ground).
                if (Mathf.Abs(Vector3.Dot(ground.point - to, up)) > 0.3f)
                {
                    _blocked = true;
                    return;
                }
                to = ground.point;
            }
            transform.position = to;
        }

        void Pose(float dt, in RollerOrders orders)
        {
            var input = new RollerAnimInput { Velocity = orders.Velocity, TurnRate = orders.TurnRate, Mode = orders.Mode, T = orders.T };
            _animator.Update(Body, input, dt, _pose);
            for (int i = 0; i < RollerLayout.Links; i++)
            {
                if (_link[i] == null) continue;
                Vector3 p = V(_pose.LinkPosition[i]);
                Quaternion q = Q(_pose.LinkRotation[i]);
                _link[i].SetLocalPositionAndRotation(p, q);
                if (i >= 1 && i <= RollerLayout.Segs && _plate[i - 1] != null) _plate[i - 1].SetLocalPositionAndRotation(p, q);
            }
            for (int leg = 0; leg < RollerLayout.Legs; leg++)
                if (_leg[leg] != null) _leg[leg].SetLocalPositionAndRotation(V(_pose.LegPosition[leg]), Q(_pose.LegRotation[leg]));
        }

        // ------------------------------------------------------------------ being hit

        public HitOutcome TakeHit(RollerPart part, in HitInfo hit)
        {
            var outcome = new HitOutcome { Hit = true };
            if (!Alive) return outcome;
            outcome.Enemy = true;
            RollerHit result;
            bool armour = false;
            switch (part.Piece)
            {
                case RollerPiece.Leg:
                    result = Body.HitLeg(part.Index, hit.Damage, hit.Sever);
                    outcome.Label = "Roller leg";
                    break;
                case RollerPiece.Plate:
                    result = Body.HitPlate(part.Index, hit.Damage, hit.Sever);
                    outcome.Label = "Roller plate";
                    armour = true;
                    break;
                case RollerPiece.Tail:
                    result = Body.HitTail(hit.Damage, hit.Sever);
                    outcome.Label = "Roller tail";
                    armour = true;
                    break;
                case RollerPiece.Head:
                    result = Body.HitBody(false, hit.Damage, hit.Lethal);
                    outcome.Label = "Roller head";
                    armour = true;
                    break;
                default:
                    result = Body.HitBody(true, hit.Damage, hit.Lethal);
                    outcome.Label = "Roller soft body";
                    break;
            }
            for (int i = 0; i < result.Losses.Count; i++) Detach(result.Losses[i], hit.Direction);
            outcome.Severed = result.Losses.Count > 0;
            // A one-hit-kill gun kills wherever it lands, after the struck piece has taken its hit.
            if (hit.Lethal && !result.Killed) result = Body.HitBody(true, 0, lethal: true);
            Brain.Alert();
            Vector3 push = transform.InverseTransformDirection(hit.Direction);
            _animator.Flinch(new Vec3d(push.x, 0.4, push.z) * (outcome.Severed ? 1.5 : 0.35));
            GoreFx gore = GoreFx.Instance;
            // Armour says it is armour: no blood, a dull knock, until it breaks. Everything else bleeds.
            if (armour && !outcome.Severed && !result.Killed) Audio.AudioDirector.Play("impact.armor", hit.Point, 0.7f, 0.6f);
            else if (gore != null) gore.Burst(hit.Point, -hit.Direction, outcome.Severed ? 16 : 4, outcome.Severed ? 6f : 3f);
            if (result.Killed)
            {
                outcome.Killed = true;
                Burst(hit);
            }
            outcome.Juice = outcome.Killed ? 1f : outcome.Severed ? 0.6f : armour ? 0.04f : 0.12f;
            return outcome;
        }

        Transform LooseParent => CombatWorld.DropParent != null ? CombatWorld.DropParent : transform.parent;

        void Detach(in RollerLoss loss, Vector3 direction)
        {
            Vector3 up = transform.up;
            if (loss.Kind == RollerLossKind.Leg)
            {
                Transform leg = _leg[loss.Index];
                if (leg == null) return;
                _leg[loss.Index] = null;
                int seg = RollerLayout.SegOf(loss.Index);
                Vector3 hip = V(RollerLayout.HipOf(loss.Index));
                Vector3 outward = _link[seg + 1].TransformDirection(new Vector3((float)RollerLayout.Sign(loss.Index), -0.5f, 0f).normalized);
                // The wound on the body, looking out along where the leg was, and on the leg, looking back.
                Wound(_link[seg + 1], hip, Quaternion.LookRotation(new Vector3((float)RollerLayout.Sign(loss.Index), -0.6f, 0f), Vector3.up), (float)RollerLayout.LegRadius * 1.3f);
                WalkerChunk chunk = Throw(leg, "Roller leg", 2f, direction * 3f + outward * Random.Range(3f, 5f) + up * Random.Range(2f, 4f));
                Wound(leg, Vector3.zero, Quaternion.Euler(0f, 180f, 0f), (float)RollerLayout.LegRadius * 1.3f);
                Pop(chunk.transform.position, outward, 10);
            }
            else if (loss.Kind == RollerLossKind.Plate)
            {
                Transform plate = _plate[loss.Index];
                if (plate == null) return;
                _plate[loss.Index] = null;
                Vector3 off = plate.up;
                Throw(plate, "Roller plate", 6f, direction * 2f + off * Random.Range(4f, 7f) + up * 2f);
                Audio.AudioDirector.Play("impact.armor", plate.position, 1f, 0.8f);
                Pop(plate.position, off, 8);
            }
            else
            {
                Transform tail = _link[RollerLayout.TailLink];
                if (tail == null) return;
                _link[RollerLayout.TailLink] = null;
                Transform last = _link[RollerLayout.TailLink - 1];
                Vector3 back = -last.forward;
                Wound(last, new Vector3(0f, 0f, -(float)RollerLayout.SegLength * 0.5f), Quaternion.Euler(0f, 180f, 0f), 0.16f);
                Throw(tail, "Roller tail", 5f, direction * 2f + back * Random.Range(3f, 5f) + up * Random.Range(3f, 5f));
                Pop(tail.position, back, 14);
            }
        }

        void Pop(Vector3 at, Vector3 way, int drops)
        {
            GoreFx gore = GoreFx.Instance;
            if (gore != null) gore.Burst(at, way, drops, 5f);
            Audio.AudioDirector.Play("gore.pop.small", at, 0.9f, Random.Range(0.75f, 0.95f));
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
            piece.SetParent(chunk, true);
            foreach (Transform t in piece.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Gib;
            var part = piece.GetComponent<RollerPart>();
            if (part != null)
            {
                // Destroy waits for the end of the frame; a second pellet this frame must not find the animal through it.
                part.Owner = null;
                Destroy(part);
            }
            GibManager gibs = GibManager.Instance;
            foreach (Collider collider in piece.GetComponents<Collider>())
            {
                if (!solid) collider.enabled = false;
                else if (gibs != null) collider.sharedMaterial = gibs.BouncyMaterial;
            }
        }

        void Wound(Transform on, Vector3 at, Quaternion facing, float size)
        {
            Mesh mesh = ModelLibrary.PartMesh(RollerLayout.Model, RollerLayout.Stump);
            if (mesh == null) return;
            var go = new GameObject("Wound");
            go.layer = on.gameObject.layer;
            go.transform.SetParent(on, false);
            go.transform.SetLocalPositionAndRotation(at, facing);
            go.transform.localScale = Vector3.one * (size / (float)RollerLayout.StumpRadius);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Quiet(go.AddComponent<MeshRenderer>());
        }

        /// <summary>
        /// It dies: it comes apart at every joint. Each segment goes its own way with its legs still on; the head,
        /// the tail and every other plate are thrown clear, and what was inside comes out.
        /// </summary>
        void Burst(in HitInfo hit)
        {
            Vector3 up = transform.up, middle = Middle();
            for (int i = 0; i < RollerLayout.Links; i++)
            {
                Transform link = _link[i];
                if (link == null) continue;
                Vector3 away = (link.position - middle);
                away = away.sqrMagnitude > 0.0001f ? away.normalized : up;
                if (i >= 1 && i <= RollerLayout.Segs)
                {
                    int seg = i - 1;
                    Transform plate = _plate[seg];
                    // Every other plate flies off by itself; the rest stay on their segments.
                    if (plate != null && seg % 2 == 0) Throw(plate, "Roller plate", 6f, plate.up * Random.Range(5f, 8f) + away * 2f + hit.Direction * 1.5f);
                    WalkerChunk chunk = Throw(link, "Roller segment", 7f, away * Random.Range(3f, 6f) + up * Random.Range(3f, 6f) + hit.Direction * 2f);
                    // What rides on a loose segment is only drawn: the segment's own box is all the physics has to carry.
                    if (plate != null && seg % 2 == 1) Loosen(plate, chunk.transform, false);
                    for (int side = 0; side < 2; side++)
                    {
                        Transform leg = _leg[RollerLayout.Leg(seg, side)];
                        if (leg != null) Loosen(leg, chunk.transform, false);
                    }
                }
                else
                {
                    Throw(link, i == RollerLayout.HeadLink ? "Roller head" : "Roller tail", 6f, away * Random.Range(4f, 7f) + up * Random.Range(4f, 7f) + hit.Direction * 2f);
                }
            }
            for (int k = 0; k < RollerLayout.Meat.Length; k++)
            {
                Mesh mesh = ModelLibrary.PartMesh(RollerLayout.Model, RollerLayout.Meat[k]);
                if (mesh == null) continue;
                WalkerChunk chunk = WalkerChunk.Make("Roller meat", middle + Random.insideUnitSphere * 0.2f, Random.rotation, LooseParent, 4f, _rules.pieceLife, _rules.maxLoosePieces);
                chunk.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                Quiet(chunk.gameObject.AddComponent<MeshRenderer>());
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
            Audio.AudioDirector.Play("gore.pop.torso", middle, 1f, 0.75f);
            ModelDetail.LetGo(gameObject);
            Died?.Invoke(this);
            Everyone.Remove(this);
            Destroy(gameObject);
        }
    }
}
