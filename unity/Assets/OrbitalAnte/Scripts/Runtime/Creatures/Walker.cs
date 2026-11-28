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
    /// <summary>One hitbox of a walker: a piece of a limb, a jaw (limb 10 or 11), the soft piece or the armoured one.</summary>
    public sealed class WalkerPart : MonoBehaviour, IHittable
    {
        public const int Belly = -1;
        public const int Carapace = -2;

        [System.NonSerialized] public Walker Owner;
        [System.NonSerialized] public int Limb;
        [System.NonSerialized] public int Segment;

        public HitOutcome Hit(in HitInfo hit) => Owner != null ? Owner.TakeHit(this, hit) : new HitOutcome { Hit = true };

        public string DescribeForDebug()
        {
            if (Owner == null) return "Creature piece";
            WalkerLayout kind = Owner.Kind;
            if (Limb == Belly) return kind.Model + " " + kind.Soft + " " + Mathf.CeilToInt((float)Owner.Body.Belly);
            if (Limb == Carapace) return kind.Model + " " + kind.Armoured + " " + Mathf.CeilToInt((float)Owner.Body.Carapace);
            if (Limb >= WalkerLayout.Limbs) return kind.Model + " " + kind.JawPiece + " " + Mathf.CeilToInt((float)Owner.Body.JawHp(Limb - WalkerLayout.Limbs));
            return kind.Model + " " + kind.Piece(Limb, Segment) + " " + Mathf.CeilToInt((float)Owner.Body.Hp(Limb, Segment));
        }
    }

    /// <summary>A piece that has come off: it lies where it lands, then shrinks away.</summary>
    public sealed class WalkerChunk : MonoBehaviour
    {
        static readonly List<WalkerChunk> Live = new List<WalkerChunk>();

        float _age, _life;
        bool _fading;
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

        public static WalkerChunk Make(string name, Vector3 position, Quaternion rotation, Transform parent, float mass, float life, int cap)
        {
            // Over the cap the oldest piece goes first.
            while (Live.Count >= Mathf.Max(16, cap))
            {
                WalkerChunk oldest = Live[0];
                Live.RemoveAt(0);
                if (oldest != null) Destroy(oldest.gameObject);
            }
            var go = new GameObject(name);
            go.layer = Layers.Gib;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var chunk = go.AddComponent<WalkerChunk>();
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
            if (!_fading)
            {
                // Shrinking a live collider makes the physics rebuild it every frame: a fading piece is only drawn.
                _fading = true;
                _body.isKinematic = true;
                foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            }
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
    /// A walker in the world: an alien creature on eight legs (Matthew, 17 Nov 2026). The kinds are the Strider (a
    /// spider crab the height of a doorway on stilt legs, with two long claws) and the Sprinter (a camel spider the
    /// length of a small car, low and quick, that lunges and bites); <see cref="WalkerLayout"/> is what differs. The rules are plain C# (<see cref="WalkerBody"/>,
    /// <see cref="WalkerAnimator"/>, <see cref="WalkerBrain"/>); this turns them into objects.
    ///
    /// - Every piece of every limb is its own object and hitbox and comes off with everything beyond it, leaving a
    ///   wound on what is left. A loose chain is a physics object that fades after a while.
    /// - It is not a physics object while alive: it is walked over the ground by its brain, and its feet are placed
    ///   by the animator. "Up" is its own transform's up, never +Y.
    /// - It dies only when its soft piece or its armoured one gives way; then it bursts.
    /// </summary>
    public sealed class Walker : MonoBehaviour, IWindup, ICreature
    {
        /// <summary>Who pays for it, given by whatever woke it; null in the practice range.</summary>
        public CreatureCredit Credit { get; set; }
        /// <summary>Its kind's name, its model's ("Strider", "Sprinter"), for the aim line (<see cref="WhereToShoot"/>). <see cref="Kind"/> is its layout.</summary>
        string ICreature.Kind => _l != null ? _l.Model : null;
        /// <summary>On land it can always come to the player, so a kill always pays.</summary>
        public bool InReach => true;
        public WalkerBody Body { get; private set; }
        public WalkerBrain Brain { get; private set; }
        /// <summary>Where its strike comes from, for the visor (UI/WindupCues): the middle of its body.</summary>
        public Vector3 WindupPoint => _bodyRoot != null ? _bodyRoot.position : transform.position;
        public WindupStage Windup => isActiveAndEnabled && Alive ? _windup.Stage : WindupStage.None;
        public float WindupProgress => _windup.Progress;
        /// <summary>Its warning ends as the claw lands, so it never shows a strike to step out of.</summary>
        public bool Threatens => true;
        public bool Alive => Body != null && Body.Alive;
        public event System.Action<Walker> Died;
        public WalkerLayout Kind => _l;

        static readonly List<Walker> Everyone = new List<Walker>();
        public static IReadOnlyList<Walker> All => Everyone;
        public static void ResetStatics() => Everyone.Clear();

        WalkerRules _rules;
        WalkerLayout _l;
        WalkerAnimator _animator;
        readonly Transform[] _jaw = new Transform[2];
        string _raiseSound, _aimSound;
        readonly WindupTrack _windup = new WindupTrack();
        readonly WalkerPose _pose = new WalkerPose();
        readonly Transform[] _piece = new Transform[WalkerLayout.Limbs * WalkerLayout.Segments];
        readonly Transform[] _finger = new Transform[2];
        readonly int[] _shown = new int[WalkerLayout.Limbs];
        Transform _carapace, _belly, _bodyRoot;
        Vector3 _home;
        bool _blocked, _sees;
        float _nextLook;
        Material _material;

        /// <summary>Makes a walker of a kind standing at a place. Null if its model is not in the game.</summary>
        public static Walker Create(WalkerLayout kind, Vector3 position, Quaternion rotation, Transform parent, CreatureConfig config, int seed, string name = null)
        {
            if (ModelLibrary.Find(kind.Model) == null)
            {
                Debug.LogWarning("Orbital Ante: the " + kind.Model + "'s model (Resources/Models/" + kind.Model + ") is missing, so none was made.");
                return null;
            }
            var go = new GameObject(name ?? kind.Model);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var walker = go.AddComponent<Walker>();
            walker.Build(kind, config != null ? config.RulesFor(kind) : kind == WalkerLayout.Sprinter ? WalkerRules.ForSprinter() : new WalkerRules(), seed);
            return walker;
        }

        void Build(WalkerLayout kind, WalkerRules rules, int seed)
        {
            _l = kind;
            _rules = rules;
            _animator = new WalkerAnimator(kind);
            Body = new WalkerBody(kind, rules.hp);
            Brain = new WalkerBrain(kind, rules, seed);
            _home = transform.position;
            // Not every walker looks for the player on the same frame.
            _nextLook = Time.time + (seed & 0xff) / 1024f;
            _material = ModelLibrary.MaterialFor(kind.Model);
            _raiseSound = Windups.WalkerWarning(kind.Model);
            _aimSound = Windups.WalkerAim(kind.Model);

            // Its hitboxes move with it every frame: a kinematic body tells the physics so.
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            _bodyRoot = new GameObject("Body").transform;
            _bodyRoot.SetParent(transform, false);
            _bodyRoot.localScale = Vector3.one * (float)_l.Size;
            _carapace = Piece(kind.Armoured, _bodyRoot);
            _belly = Piece(kind.Soft, _bodyRoot);
            // Boxes in the body's own size.
            Box(_carapace, V(kind.ArmouredCentre), V(kind.ArmouredSize), WalkerPart.Carapace, 0);
            Box(_belly, V(kind.SoftCentre), V(kind.SoftSize), WalkerPart.Belly, 0);
            if (kind.HasJaws)
            {
                for (int side = 0; side < 2; side++)
                {
                    Transform jaw = Piece(kind.JawPiece, transform);
                    jaw.name = kind.JawPiece + " " + side;
                    jaw.localScale = Vector3.one * (float)kind.Size;
                    var capsule = jaw.gameObject.AddComponent<CapsuleCollider>();
                    capsule.direction = 2;
                    capsule.radius = (float)kind.JawRadius * 1.25f;
                    capsule.height = (float)kind.JawLength + capsule.radius;
                    capsule.center = new Vector3(0f, 0f, (float)kind.JawLength * 0.5f);
                    Tag(jaw.gameObject, WalkerBody.JawLimb(side), 0);
                    _jaw[side] = jaw;
                    Transform finger = Piece(kind.FingerPiece, transform);
                    finger.localScale = Vector3.one * (float)kind.Size;
                    _finger[side] = finger;
                }
            }

            for (int limb = 0; limb < WalkerLayout.Limbs; limb++)
            {
                _shown[limb] = WalkerLayout.Segments;
                float scale = (float)_l.Scale(limb);
                for (int s = 0; s < WalkerLayout.Segments; s++)
                {
                    Transform piece = Piece(_l.Piece(limb, s), transform);
                    piece.name = _l.Piece(limb, s) + " " + limb;
                    piece.localScale = Vector3.one * scale;
                    _piece[limb * WalkerLayout.Segments + s] = piece;
                    // A capsule along the piece, a little fatter than the shell so thin legs can be hit.
                    var capsule = piece.gameObject.AddComponent<CapsuleCollider>();
                    float length = (float)_l.Length(limb, s) / scale;
                    float radius = Mathf.Max(0.085f, (float)_l.Radius(limb, s) / scale * 1.45f);
                    capsule.direction = 2;
                    capsule.radius = radius;
                    capsule.height = length + radius;
                    capsule.center = new Vector3(0f, 0f, length * 0.5f);
                    Tag(piece.gameObject, limb, s);
                }
                if (WalkerLayout.IsArm(limb) && !kind.HasJaws)
                {
                    Transform finger = Piece(kind.FingerPiece, transform);
                    finger.localScale = Vector3.one * (float)_l.Size;
                    _finger[WalkerLayout.SideOf(limb)] = finger;
                }
            }
            ModelDetail.Attach(gameObject, kind.Model, DetailRule.BodyRange * (kind == WalkerLayout.Strider ? 1.6f : 1.1f), true);
            Everyone.Add(this);
            Pose(0.0001f, new WalkerOrders { StrikeSide = -1 });
        }

        Transform Piece(string piece, Transform parent)
        {
            var go = new GameObject(piece);
            go.layer = Layers.Hitbox;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = ModelLibrary.PartMesh(_l.Model, piece);
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
            var part = go.AddComponent<WalkerPart>();
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

            var senses = new WalkerSenses { Blocked = _blocked, Home = D(transform.InverseTransformPoint(_home)), BodyHeight = _pose.BodyPosition.Y };
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

            WalkerOrders orders = Brain.Think(Body, senses, dt);
            // Its warning is heard and seen (UI/WindupCues) from the moment the claw starts up until it lands: its own
            // wind-up as it starts, and its mark the very frame it takes aim.
            WindupRead read = orders.StrikeSide >= 0 ? Windups.Walker(orders, Brain.LockShare(Body)) : WindupRead.None;
            WindupTell.Tell(this, _windup.Step(read), _raiseSound, _aimSound, _bodyRoot.position);
            Walk(orders, dt);
            Pose(dt, orders);
            // A quick animal's hitboxes must be where it is drawn when the player's shot is traced this frame.
            if (_l.Trot) Physics.SyncTransforms();
            if (orders.Landed && eye != null) ClawLanded(orders, eye);
        }

        void Walk(in WalkerOrders orders, float dt)
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

        void Pose(float dt, in WalkerOrders orders)
        {
            var input = new WalkerAnimInput
            {
                Velocity = orders.Velocity, TurnRate = orders.TurnRate,
                StrikeSide = orders.StrikeSide, StrikeT = orders.StrikeT, StrikeAt = orders.StrikeAt,
            };
            _animator.Update(Body, input, dt, _pose);
            _bodyRoot.SetLocalPositionAndRotation(V(_pose.BodyPosition), Q(_pose.BodyRotation));
            if (_l.SoftSway > 0.0)
            {
                // The soft piece swings about its own joint on the armoured one.
                Quaternion turn = Q(_pose.SoftTurn);
                Vector3 pivot = V(_l.SoftPivot);
                _belly.SetLocalPositionAndRotation(pivot - turn * pivot, turn);
            }
            for (int side = 0; side < 2 && _l.HasJaws; side++)
            {
                if (_jaw[side] == null) continue;
                _jaw[side].SetLocalPositionAndRotation(V(_pose.JawPosition[side]), Q(_pose.JawRotation[side]));
                _finger[side].SetLocalPositionAndRotation(V(_pose.FingerPosition[side]), Q(_pose.FingerRotation[side]));
            }
            for (int limb = 0; limb < WalkerLayout.Limbs; limb++)
            {
                for (int s = 0; s < _shown[limb]; s++)
                {
                    int i = limb * WalkerLayout.Segments + s;
                    _piece[i].SetLocalPositionAndRotation(V(_pose.Position[i]), Q(_pose.Rotation[i]));
                }
                if (WalkerLayout.IsArm(limb) && !_l.HasJaws && _shown[limb] == WalkerLayout.Segments)
                {
                    int side = WalkerLayout.SideOf(limb);
                    _finger[side].SetLocalPositionAndRotation(V(_pose.FingerPosition[side]), Q(_pose.FingerRotation[side]));
                }
            }
        }

        void ClawLanded(in WalkerOrders orders, Transform eye)
        {
            Vector3 at = transform.TransformPoint(V(orders.StrikeAt));
            Vector3 chest = eye.position - transform.up * 0.35f;
            Audio.AudioDirector.Play("gore.splat", at, 0.5f, 0.7f);
            if (_l.Attack == WalkerAttack.Lunge)
            {
                // Where the jaws really are (on its belly it cannot throw itself as far as it aimed).
                Transform jaw = _jaw[0] != null ? _jaw[0] : _jaw[1];
                if (jaw == null) return;
                at = jaw.TransformPoint(0f, 0f, (float)_l.JawLength);
                at -= transform.right * Vector3.Dot(at - _bodyRoot.position, transform.right);
                // A bite is low: it counts by how near it lands along the ground, and a player well above it is missed.
                Vector3 up = transform.up, apart = eye.position - up * 1.6f - at;
                float high = Vector3.Dot(apart, up);
                if ((apart - up * high).magnitude > _rules.strikeRadius || high > 0.9f || high < -2.2f) return;
            }
            else if (Vector3.Distance(at, chest) > _rules.strikeRadius) return;
            // Never through a wall.
            if (Physics.Linecast(_bodyRoot.position, chest, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return;
            PlayerHealth player = CombatWorld.Player;
            if (player != null) player.TakeDamage(_rules.strikeDamage * (float)Body.StrikeShare, (chest - _bodyRoot.position).normalized);
        }

        // ------------------------------------------------------------------ being hit

        public HitOutcome TakeHit(WalkerPart part, in HitInfo hit)
        {
            var outcome = new HitOutcome { Hit = true };
            if (!Alive) return outcome;
            // A piece that has already come off is not the animal any more.
            bool isJaw = part.Limb >= WalkerLayout.Limbs;
            if (!isJaw && part.Limb >= 0 && part.Segment >= _shown[part.Limb]) return outcome;
            if (isJaw && _jaw[part.Limb - WalkerLayout.Limbs] == null) return outcome;
            outcome.Enemy = true;
            outcome.Target = this;
            WalkerHit result;
            bool weaponLost = false;
            if (isJaw)
            {
                result = Body.HitJaw(part.Limb - WalkerLayout.Limbs, hit.Damage, hit.Sever);
                outcome.Label = _l.Model + " " + _l.JawPiece;
                for (int i = 0; i < result.Losses.Count; i++)
                {
                    weaponLost |= TakesWeapon(result.Losses[i]);
                    Detach(result.Losses[i], hit.Direction, hit.Point);
                }
                outcome.Severed = result.Losses.Count > 0;
                if (hit.Lethal) result = Body.HitBody(true, 0, lethal: true);
            }
            else if (part.Limb >= 0)
            {
                result = Body.HitLimb(part.Limb, part.Segment, hit.Damage, hit.Sever);
                outcome.Label = _l.Model + " " + _l.Piece(part.Limb, part.Segment);
                for (int i = 0; i < result.Losses.Count; i++)
                {
                    weaponLost |= TakesWeapon(result.Losses[i]);
                    Detach(result.Losses[i], hit.Direction, hit.Point);
                }
                outcome.Severed = result.Losses.Count > 0;
                if (hit.Lethal)
                {
                    // A one-hit-kill gun kills wherever it lands, after the struck piece has taken its hit.
                    result = Body.HitBody(true, 0, lethal: true);
                }
            }
            else
            {
                result = Body.HitBody(part.Limb == WalkerPart.Belly, hit.Damage, hit.Lethal);
                outcome.Label = _l.Model + " " + (part.Limb == WalkerPart.Belly ? _l.Soft : _l.Armoured);
            }
            // Where it landed, for the hit marker: the soft piece rings gold, the lid only knocks (WhereToShoot).
            outcome.Zone = WhereToShoot.Walker(part.Limb == WalkerPart.Belly, part.Limb == WalkerPart.Carapace, weaponLost);
            Brain.Alert();
            Vector3 push = transform.InverseTransformDirection(hit.Direction);
            _animator.Flinch(new Vec3d(push.x, 0.4, push.z) * (outcome.Severed ? 1.5 : 0.35));
            bool lid = outcome.Zone == HitZone.Armour;
            GoreFx gore = GoreFx.Instance;
            // The lid is armour and says so: no blood, a dull knock. Everything else bleeds.
            if (lid && !result.Killed) Audio.AudioDirector.Play("impact.armor", hit.Point, 0.7f, 0.55f);
            else if (gore != null) gore.Burst(hit.Point, -hit.Direction, outcome.Severed ? 18 : 4, outcome.Severed ? 6f : 3f);
            if (result.Killed)
            {
                outcome.Killed = true;
                Burst(result, hit);
            }
            // Hitstop and shake only when something came off or it died, as for people (Part B: on a limb popping): a hit
            // that takes nothing off is 0, or every shot on a creature froze the game for 50 ms (pass 7).
            outcome.Juice = outcome.Killed ? 1f : outcome.Severed ? 0.6f : 0f;
            return outcome;
        }

        /// <summary>
        /// True if this loss takes a piece it still strikes with: a jaw still on, or a claw arm still whole (the claw is
        /// its tip, so an arm already short has lost it before). Asked before the loss is carried out.
        /// </summary>
        bool TakesWeapon(in WalkerLoss loss)
        {
            if (loss.Burst || !WhereToShoot.WalkerWeapon(_l, loss.Limb)) return false;
            if (loss.Limb >= WalkerLayout.Limbs) return _jaw[loss.Limb - WalkerLayout.Limbs] != null;
            return _shown[loss.Limb] == WalkerLayout.Segments;
        }

        /// <summary>A limb's pieces from one joint outward come off as one loose chain, and a wound is left behind.</summary>
        void Detach(in WalkerLoss loss, Vector3 direction, Vector3 point)
        {
            if (loss.Limb >= WalkerLayout.Limbs)
            {
                DetachJaw(loss.Limb - WalkerLayout.Limbs, loss.Burst, direction);
                return;
            }
            int limb = loss.Limb, from = loss.FromSegment, had = _shown[limb];
            if (from >= had) return;
            _shown[limb] = from;
            Transform first = _piece[limb * WalkerLayout.Segments + from];
            Transform loose = CombatWorld.DropParent != null ? CombatWorld.DropParent : transform.parent;
            WalkerChunk chunk = WalkerChunk.Make(_l.Model + " " + first.name, first.position, first.rotation, loose, 3f + 2.5f * (had - from), _rules.pieceLife, _rules.maxLoosePieces);
            for (int s = from; s < had; s++) Loosen(_piece[limb * WalkerLayout.Segments + s], chunk.transform);
            if (WalkerLayout.IsArm(limb) && !_l.HasJaws && had == WalkerLayout.Segments)
            {
                Transform finger = _finger[WalkerLayout.SideOf(limb)];
                finger.SetParent(chunk.transform, true);
                finger.gameObject.layer = Layers.Gib;
            }
            float radius = (float)_l.Radius(limb, from);
            // The wound on the piece that came off, looking back at where it was joined.
            Wound(first, Vector3.zero, Quaternion.Euler(0f, 180f, 0f), radius / first.lossyScale.x);
            // And on what is left: the end of the piece before it, or the body.
            if (from == 0)
            {
                Vector3 hip = V(_l.HipModelled(limb));
                Wound(_bodyRoot, hip, Quaternion.LookRotation(V(_l.Out(limb)), Vector3.up), radius / (float)_l.Size);
            }
            else
            {
                Transform before = _piece[limb * WalkerLayout.Segments + from - 1];
                float scale = (float)_l.Scale(limb);
                Wound(before, new Vector3(0f, 0f, (float)_l.Length(limb, from - 1) / scale), Quaternion.identity, radius / scale);
            }

            Vector3 up = transform.up;
            Vector3 out_ = transform.TransformDirection(V(_l.Out(limb)));
            Rigidbody body = chunk.Body;
            float kick = loss.Burst ? Random.Range(5f, 9f) : Random.Range(3f, 5f);
            body.linearVelocity = direction * (loss.Burst ? 1.5f : 4f) + out_ * kick + up * Random.Range(3f, 6f);
            body.angularVelocity = Random.onUnitSphere * Random.Range(3f, 9f);
            GoreFx gore = GoreFx.Instance;
            if (gore != null) gore.Burst(first.position, out_, 14, 5f);
            Audio.AudioDirector.Play("gore.pop.small", first.position, 0.9f, Random.Range(0.7f, 0.9f));
        }

        /// <summary>A jaw comes off with its finger, leaving a wound on the head.</summary>
        void DetachJaw(int side, bool burst, Vector3 direction)
        {
            Transform jaw = _jaw[side];
            if (jaw == null) return;
            _jaw[side] = null;
            Transform loose = CombatWorld.DropParent != null ? CombatWorld.DropParent : transform.parent;
            WalkerChunk chunk = WalkerChunk.Make(_l.Model + " " + jaw.name, jaw.position, jaw.rotation, loose, 4f, _rules.pieceLife, _rules.maxLoosePieces);
            Loosen(jaw, chunk.transform);
            Transform finger = _finger[side];
            finger.SetParent(chunk.transform, true);
            finger.gameObject.layer = Layers.Gib;
            float radius = (float)_l.JawRadius;
            Wound(jaw, Vector3.zero, Quaternion.Euler(0f, 180f, 0f), radius);
            Wound(_carapace, V(_l.JawAt(side)), Quaternion.identity, radius);
            Vector3 up = transform.up, ahead = transform.forward;
            chunk.Body.linearVelocity = direction * (burst ? 1.5f : 3f) + ahead * Random.Range(2f, 5f) + up * Random.Range(3f, 5f);
            chunk.Body.angularVelocity = Random.onUnitSphere * Random.Range(4f, 10f);
            GoreFx gore = GoreFx.Instance;
            if (gore != null) gore.Burst(jaw.position, ahead, 12, 5f);
            Audio.AudioDirector.Play("gore.pop.small", jaw.position, 0.9f, Random.Range(0.85f, 1.05f));
        }

        static void Loosen(Transform piece, Transform chunk)
        {
            piece.SetParent(chunk, true);
            piece.gameObject.layer = Layers.Gib;
            var part = piece.GetComponent<WalkerPart>();
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
            Mesh mesh = ModelLibrary.PartMesh(_l.Model, _l.Stump);
            if (mesh == null) return;
            var go = new GameObject("Wound");
            go.layer = on.gameObject.layer;
            go.transform.SetParent(on, false);
            go.transform.SetLocalPositionAndRotation(at, facing);
            go.transform.localScale = Vector3.one * (size / (float)_l.StumpRadius);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            // Like the other enemies, it casts no real-time shadow: on a Deck that would draw it again per cascade.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        /// <summary>It dies: every limb left is thrown off, the lid and the belly fly apart, and what was inside comes out.</summary>
        void Burst(WalkerHit result, in HitInfo hit)
        {
            for (int i = 0; i < result.Losses.Count; i++) Detach(result.Losses[i], hit.Direction, hit.Point);
            Vector3 up = transform.up, middle = _bodyRoot.position;
            Transform loose = CombatWorld.DropParent != null ? CombatWorld.DropParent : transform.parent;
            ThrowBody(_carapace, loose, up * Random.Range(6f, 9f) + hit.Direction * 2f, 14f);
            ThrowBody(_belly, loose, -up * 1.5f + hit.Direction * 3f, 10f);
            for (int k = 0; k < _l.Meat.Length; k++)
            {
                Mesh mesh = ModelLibrary.PartMesh(_l.Model, _l.Meat[k]);
                if (mesh == null) continue;
                WalkerChunk chunk = WalkerChunk.Make(_l.Model + " meat", middle + Random.insideUnitSphere * 0.2f, Random.rotation, loose, 4f, _rules.pieceLife, _rules.maxLoosePieces);
                chunk.transform.localScale = Vector3.one * (float)_l.Size;
                chunk.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var meat = chunk.gameObject.AddComponent<MeshRenderer>();
                meat.sharedMaterial = _material;
                meat.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
            WalkerChunk chunk = WalkerChunk.Make(_l.Model + " " + half.name, half.position, half.rotation, loose, mass, _rules.pieceLife, _rules.maxLoosePieces);
            chunk.transform.localScale = Vector3.one * (float)_l.Size;
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
