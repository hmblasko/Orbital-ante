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
    /// <summary>One hitbox of a Pack biter: its head, its lower jaw, a length of body or a fin.</summary>
    public sealed class BiterPart : MonoBehaviour, IHittable
    {
        [System.NonSerialized] public Biter Owner;
        [System.NonSerialized] public BiterPiece Piece;
        [System.NonSerialized] public int Index;

        public HitOutcome Hit(in HitInfo hit) => Owner != null ? Owner.TakeHit(this, hit) : new HitOutcome { Hit = true };

        public string DescribeForDebug()
        {
            if (Owner == null) return "Creature piece";
            switch (Piece)
            {
                case BiterPiece.Head: return "Biter head " + Mathf.CeilToInt((float)Owner.Body.Head);
                case BiterPiece.Seg: return "Biter body " + Mathf.CeilToInt((float)Owner.Body.Trunk);
                case BiterPiece.Jaw: return "Biter jaw";
                default: return "Biter fin";
            }
        }
    }

    /// <summary>
    /// A pack of Pack biters, the one object that a sea or the arena makes: it owns the pack's turns
    /// (<see cref="BiterPack"/>) and steps every member after them, so a turn handed out is seen the same frame.
    /// </summary>
    public sealed class BiterShoal : MonoBehaviour
    {
        readonly List<Biter> _members = new List<Biter>();
        // Fish still to be made, one a frame, so a pack never arrives as a hitch.
        struct Unborn { public Vector3 At; public Quaternion Facing; public int Place; }
        readonly List<Unborn> _unborn = new List<Unborn>();
        Transform _parent;
        BiterRules _rules;
        string _name;
        System.Func<Vector3, MawWater> _water;
        System.Func<int, CreatureCredit> _credit;
        Vector3 _home;
        public BiterPack Pack { get; } = new BiterPack();
        /// <summary>Every member is dead.</summary>
        public event System.Action<BiterShoal> Gone;
        /// <summary>A member died: its place in the pack, so a sea can leave it out when the pack is woken again.</summary>
        public event System.Action<int> Lost;
        public int Living => Pack.Living;
        /// <summary>True while the pack has noticed the swimmer (one fish did, so they all know): for the music.</summary>
        public bool AwareOfPlayer => Pack.Alerted && Pack.Living > 0;

        /// <summary>The middle of the living fish (where the pack is, not where it was made), in world space.</summary>
        public Vector3 Middle
        {
            get
            {
                Vector3 sum = Vector3.zero;
                int n = 0;
                for (int i = 0; i < _members.Count; i++)
                    if (_members[i] != null) { sum += _members[i].transform.position; n++; }
                return n > 0 ? sum / n : transform.position;
            }
        }

        /// <summary>
        /// Makes a pack of <paramref name="count"/> round a place. Null if the model is not in the game, or if every place
        /// in the pack is dead.
        /// <paramref name="dead"/> are places in the pack already killed (a pack woken again): they are left out, but
        /// still count toward its size. <paramref name="credit"/> says who pays for the fish in each place.
        /// </summary>
        public static BiterShoal Create(Vector3 home, Quaternion rotation, Transform parent, CreatureConfig config, int count, string name = null,
                                        System.Func<Vector3, MawWater> water = null, ICollection<int> dead = null,
                                        System.Func<int, CreatureCredit> credit = null)
        {
            if (ModelLibrary.Find(BiterLayout.Model) == null)
            {
                Debug.LogWarning("Orbital Ante: the Pack biter's model (Resources/Models/" + BiterLayout.Model + ") is missing, so no pack was made.");
                return null;
            }
            int size = Mathf.Max(1, count), alive = 0;
            for (int i = 0; i < size; i++)
                if (dead == null || !dead.Contains(i)) alive++;
            // Every fish of this pack is dead: there is no pack to make (an empty one would never say it was gone).
            if (alive == 0) return null;
            var go = new GameObject(name ?? "Biter pack");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(home, rotation);
            var shoal = go.AddComponent<BiterShoal>();
            BiterRules rules = config != null ? config.biter : new BiterRules();
            shoal._parent = parent;
            shoal._rules = rules;
            shoal._name = name ?? "Biter";
            shoal._water = water;
            shoal._credit = credit;
            shoal._home = home;
            Vector3 up = rotation * Vector3.up;
            int killed = 0;
            for (int i = 0; i < size; i++)
            {
                if (dead != null && dead.Contains(i)) { killed++; continue; }
                // Round where they live, each a little higher or lower, all swimming the same way round.
                float a = i * Mathf.PI * 2f / size;
                Vector3 out_ = rotation * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 at = home + out_ * rules.shoalRadius + up * ((i % 3 - 1) * 0.5f);
                Quaternion facing = Quaternion.LookRotation(Vector3.Cross(up, out_), up);
                shoal._unborn.Add(new Unborn { At = at, Facing = facing, Place = i });
            }
            shoal.Pack.KilledBefore(killed);
            shoal.MakeOne();
            return shoal;
        }

        void MakeOne()
        {
            if (_unborn.Count == 0) return;
            Unborn u = _unborn[0];
            _unborn.RemoveAt(0);
            // Where the pack swam to meanwhile does not matter: a fish joins the ring from where it was to start.
            Biter fish = Biter.Make(u.At, u.Facing, _parent, _rules, this, u.Place, _name + " " + (u.Place + 1), _water, _home);
            fish.Credit = _credit != null ? _credit(u.Place) : null;
            _members.Add(fish);
            fish.Died += OnDied;
        }

        void OnDied(Biter fish)
        {
            _members.Remove(fish);
            Lost?.Invoke(fish.Place);
            if (_members.Count == 0 && _unborn.Count == 0)
            {
                Gone?.Invoke(this);
                Destroy(gameObject);
            }
        }

        void OnDestroy()
        {
            // Put away with its sea: the members go with it.
            for (int i = _members.Count - 1; i >= 0; i--)
                if (_members[i] != null) Destroy(_members[i].gameObject);
            _members.Clear();
        }

        void Update()
        {
            if (GameSession.Paused) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;
            MakeOne();
            Pack.Advance(dt);
            bool sync = false;
            for (int i = _members.Count - 1; i >= 0; i--)
            {
                Biter fish = _members[i];
                if (fish == null) { _members.RemoveAt(i); continue; }
                sync |= fish.Tick(dt);
            }
            // One sync for the whole pack, not one a fish.
            if (sync) Physics.SyncTransforms();
        }
    }

    /// <summary>
    /// The Pack biter in the world: an alien from the Goliath tigerfish (Matthew, 17 Nov 2026), the second creature
    /// that swims, and the first that hunts as a pack. The rules are plain C# (<see cref="BiterBody"/>,
    /// <see cref="BiterAnimator"/>, <see cref="BiterBrain"/>, <see cref="BiterPack"/>); this turns them into objects.
    ///
    /// - Its head, lower jaw, four lengths, two front fins, back fin and tail fin are each an object and a hitbox.
    ///   Fins and jaw come off whole (<see cref="WalkerChunk"/>).
    /// - It is not a physics object while alive; its <see cref="BiterShoal"/> steps it every frame. "Up" is what the
    ///   water says, never +Y; in a sea it is a child of its world.
    /// - A dart bites only what is at its mouth on the way: a step aside is a miss. It has no pull and no hold.
    /// - In the combat arena the pack swims in the air a little over the ground, as a stand-in for a sea.
    /// </summary>
    public sealed class Biter : MonoBehaviour, IWindup, ICreature
    {
        public BiterBody Body { get; private set; }
        public BiterBrain Brain { get; private set; }
        /// <summary>Its place in its pack.</summary>
        public int Place { get; private set; }
        /// <summary>Who pays for it, given by its shoal; null in the practice range.</summary>
        public CreatureCredit Credit { get; set; }
        /// <summary>Its kind, its model's name, for the aim line (<see cref="WhereToShoot"/>).</summary>
        public string Kind => "Biter";
        /// <summary>True while the player is in the water with it, where it can reach them (always in the practice range).</summary>
        public bool InReach => _inReach;
        bool _inReach = true;
        /// <summary>Where its strike comes from, for the visor (UI/WindupCues): its head.</summary>
        public Vector3 WindupPoint => transform.position;
        public WindupStage Windup => isActiveAndEnabled && Alive ? _windup.Stage : WindupStage.None;
        public float WindupProgress => _windup.Progress;
        /// <summary>A dart is over too soon to step out of part-way, so it always threatens.</summary>
        public bool Threatens => true;
        readonly WindupTrack _windup = new WindupTrack();
        public bool Alive => Body != null && Body.Alive;
        public event System.Action<Biter> Died;
        [System.NonSerialized] public System.Func<Vector3, MawWater> Water;

        static readonly List<Biter> Everyone = new List<Biter>();
        public static IReadOnlyList<Biter> All => Everyone;
        public static void ResetStatics()
        {
            Everyone.Clear();
            SeaCreatures.ResetStatics();
        }

        const string MarkSound = Windups.BiterWarning, AimSound = Windups.BiterAim, DartSound = "creature.biter.dart";
        /// <summary>In the arena it is told the floor is this much lower than it is, so its mouth comes down to someone's chest.</summary>
        const float ArenaSink = 0.6f;

        BiterRules _rules;
        readonly BiterAnimator _animator = new BiterAnimator();
        readonly BiterPose _pose = new BiterPose();
        readonly Transform[] _node = new Transform[BiterLayout.Nodes];
        readonly Transform[] _fin = new Transform[BiterLayout.Fins];
        Transform _jaw, _backFin, _tailFin, _homeParent;
        Vector3 _homeLocal;
        bool _blocked;
        // Where the player's chest was last think and how it is moving (smoothed), for the warning's lead.
        Vector3 _lastChest, _playerVelocity;
        bool _hadChest;
        float _unposed;
        BiterMode _lastMode = BiterMode.Shoal;
        Material _material;

        internal static Biter Make(Vector3 position, Quaternion rotation, Transform parent, BiterRules rules, BiterShoal shoal, int place, string name,
                                   System.Func<Vector3, MawWater> water, Vector3 home)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var fish = go.AddComponent<Biter>();
            fish.Water = water;
            fish.Build(rules ?? new BiterRules(), shoal != null ? shoal.Pack : null, place, home);
            return fish;
        }

        void Build(BiterRules rules, BiterPack pack, int place, Vector3 home)
        {
            _rules = rules;
            Place = place;
            Body = new BiterBody(rules.hp);
            Brain = new BiterBrain(rules, pack, place);
            _homeParent = transform.parent;
            _homeLocal = _homeParent != null ? _homeParent.InverseTransformPoint(home) : home;
            _material = ModelLibrary.MaterialFor(BiterLayout.Model);

            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            for (int i = 0; i < BiterLayout.Nodes; i++)
            {
                bool head = i == 0;
                var node = new GameObject(head ? "Head" : "Body " + (i - 1));
                node.layer = Layers.Hitbox;
                node.transform.SetParent(transform, false);
                _node[i] = node.transform;
                Mesh(head ? BiterLayout.HeadPiece : BiterLayout.PieceOf(i - 1), node.transform);
                var box = node.AddComponent<BoxCollider>();
                if (head)
                {
                    // Above its mouth only: the jaw has a box of its own.
                    float low = (float)BiterLayout.JawHinge.Y, top = (float)BiterLayout.HeadHalfHeight;
                    box.center = new Vector3(0f, (low + top) * 0.5f, ((float)BiterLayout.HeadLength - (float)BiterLayout.Spacing) * 0.5f);
                    box.size = new Vector3((float)BiterLayout.HeadHalfWidth * 2f, top - low, (float)BiterLayout.HeadLength);
                    Tag(node, BiterPiece.Head, 0);
                }
                else
                {
                    box.size = new Vector3((float)BiterLayout.HalfWidth[i - 1] * 2f, (float)BiterLayout.HalfHeight[i - 1] * 2f, (float)BiterLayout.Spacing);
                    Tag(node, BiterPiece.Seg, i - 1);
                }
            }
            _jaw = Mesh(BiterLayout.JawPiece, transform);
            _jaw.name = "Jaw";
            var jawBox = _jaw.gameObject.AddComponent<BoxCollider>();
            jawBox.center = new Vector3(0f, -0.04f, (float)BiterLayout.JawLength * 0.5f);
            jawBox.size = new Vector3((float)BiterLayout.JawHalfWidth * 2f, 0.12f, (float)BiterLayout.JawLength);
            Tag(_jaw.gameObject, BiterPiece.Jaw, 0);
            for (int side = 0; side < BiterLayout.Fins; side++)
            {
                Transform fin = Mesh(BiterLayout.FinPiece, transform);
                fin.name = side == BiterLayout.RightSide ? "Right fin" : "Left fin";
                var box = fin.gameObject.AddComponent<BoxCollider>();
                // Thicker than it is drawn, so a fin can be shot off on purpose.
                box.center = new Vector3(0f, 0f, (float)BiterLayout.FinLength * 0.5f);
                box.size = new Vector3((float)BiterLayout.FinChord, 0.07f, (float)BiterLayout.FinLength);
                Tag(fin.gameObject, BiterPiece.Fin, side);
                _fin[side] = fin;
            }
            _backFin = Mesh(BiterLayout.BackFinPiece, transform);
            var backBox = _backFin.gameObject.AddComponent<BoxCollider>();
            backBox.center = new Vector3(0f, (float)BiterLayout.BackFinHeight * 0.5f, -0.04f);
            backBox.size = new Vector3(0.07f, (float)BiterLayout.BackFinHeight, (float)BiterLayout.BackFinChord);
            Tag(_backFin.gameObject, BiterPiece.BackFin, 0);
            _tailFin = Mesh(BiterLayout.TailFinPiece, transform);
            var tailBox = _tailFin.gameObject.AddComponent<BoxCollider>();
            float tailUp = (float)BiterLayout.TailFinUp, tailDown = (float)BiterLayout.TailFinDown, tailBack = (float)BiterLayout.TailFinBack;
            tailBox.center = new Vector3(0f, (tailUp - tailDown) * 0.5f, -tailBack * 0.5f);
            tailBox.size = new Vector3(0.08f, tailUp + tailDown, tailBack);
            Tag(_tailFin.gameObject, BiterPiece.TailFin, 0);

            // A fish is about 2 m long: full detail only while it is drawn large (half a body's range).
            ModelDetail.Attach(gameObject, BiterLayout.Model, DetailRule.BodyRange * 0.5f, true);
            Everyone.Add(this);
            Pose(0.0001f, new BiterOrders(), Vector3.zero);
        }

        Transform Mesh(string piece, Transform parent)
        {
            var go = new GameObject(piece);
            go.layer = Layers.Hitbox;
            go.transform.SetParent(parent, false);
            UnityEngine.Mesh shape = ModelLibrary.PartMesh(BiterLayout.Model, piece);
            if (shape == null) Debug.LogWarning("The " + BiterLayout.Model + " model has no piece called " + piece + ". Build the model again (tools/blender/build_creatures.py).");
            go.AddComponent<MeshFilter>().sharedMesh = shape;
            Quiet(go.AddComponent<MeshRenderer>());
            return go.transform;
        }

        void Quiet(MeshRenderer renderer)
        {
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        void Tag(GameObject go, BiterPiece piece, int index)
        {
            var part = go.AddComponent<BiterPart>();
            part.Owner = this;
            part.Piece = piece;
            part.Index = index;
        }

        void OnDestroy()
        {
            Everyone.Remove(this);
            // Put away alive (its sea left behind): it gives up its turn, or the pack would wait for it for ever.
            if (Alive && Brain != null) Brain.Died();
        }

        static Vector3 V(Vec3d v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        static Quaternion Q(QuatD q) => new Quaternion((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);
        static Vec3d D(Vector3 v) => new Vec3d(v.x, v.y, v.z);

        Vector3 HomeWorld => _homeParent != null ? _homeParent.TransformPoint(_homeLocal) : _homeLocal;

        MawWater WaterAt(Vector3 at)
        {
            if (Water != null) return Water(at);
            Vector3 up = transform.up;
            float below = 30f;
            if (Physics.Raycast(at, -up, out RaycastHit ground, 30f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) below = ground.distance;
            return new MawWater { Above = 30f, Below = below + ArenaSink, Up = up, Flow = Vector3.zero };
        }

        OrbitalAnte.Ship.Astronaut _crew;
        float _lookForCrewAt;

        bool PlayerInWater(Vector3 chest)
        {
            if (Water == null) return true;
            if (_crew == null && Time.unscaledTime >= _lookForCrewAt) { _lookForCrewAt = Time.unscaledTime + 2f; _crew = SeaCreatures.FindCrew(); }
            if (_crew == null || _crew.Mode != OrbitalAnte.Ship.AstronautMode.Outside || _crew.Dead) return false;
            return Water(chest).Above > 0.3f;
        }

        BiterSenses Senses(in MawWater here, out Transform eye)
        {
            var senses = new BiterSenses
            {
                Blocked = _blocked, Home = D(transform.InverseTransformPoint(HomeWorld)),
                WaterAbove = here.Above, FloorBelow = here.Below,
            };
            eye = CombatWorld.PlayerPresent ? CombatWorld.PlayerEye : null;
            if (eye != null && !CombatWorld.EnemiesHoldFire)
            {
                Vector3 chest = eye.position - here.Up * 0.5f;
                senses.Hostile = true;
                senses.Player = D(transform.InverseTransformPoint(chest));
                senses.PlayerInWater = PlayerInWater(chest);
                float dt = Time.deltaTime;
                if (_hadChest && dt > 1e-4f)
                {
                    // Relative to the water, so a sea carried round by its world leads nothing.
                    Vector3 seen = (chest - _lastChest) / dt - here.Flow;
                    if (seen.sqrMagnitude > 30f * 30f) seen = Vector3.zero;   // a re-centre or a teleport, not a swim
                    _playerVelocity = Vector3.Lerp(_playerVelocity, seen, Mathf.Clamp01(dt * 6f));
                }
                _lastChest = chest;
                _hadChest = true;
                senses.PlayerVelocity = D(transform.InverseTransformDirection(_playerVelocity));
            }
            else _hadChest = false;
            _inReach = Water == null || senses.PlayerInWater;
            return senses;
        }

        /// <summary>One step, from its shoal (after the pack has handed out its turn).</summary>
        /// <summary>Steps it. True if it was posed near the player, so its hitboxes need syncing this frame.</summary>
        internal bool Tick(float dt)
        {
            if (!Alive) return false;
            bool posedNear = false;
            MawWater here = WaterAt(transform.position);
            if (here.Up.sqrMagnitude > 0.5f) transform.rotation = Quaternion.FromToRotation(transform.up, here.Up) * transform.rotation;

            BiterSenses senses = Senses(here, out Transform eye);
            BiterOrders orders = Brain.Think(dt, senses, Body);
            if (orders.Mode != _lastMode)
            {
                // The dart is heard, in its own sound (never another creature's or an enemy's).
                if (orders.Mode == BiterMode.Dart) Audio.AudioDirector.Play(DartSound, transform.position);
                _lastMode = orders.Mode;
            }
            // Its warning is heard and seen (UI/WindupCues) until the dart is over: its own wind-up as it marks the player,
            // and its mark the very frame it takes aim.
            WindupTell.Tell(this, _windup.Step(Windups.Biter(orders)), MarkSound, AimSound, transform.position);
            Vector3 moved = Move(orders, dt);
            Transform near = CombatWorld.PlayerEye;
            bool far = near == null || (near.position - transform.position).sqrMagnitude > 70f * 70f;
            _unposed += dt;
            if (!far || orders.Mode != BiterMode.Shoal || _unposed >= 0.2f)
            {
                Pose(_unposed, orders, moved / dt);
                _unposed = 0f;
                posedNear = near != null && (near.position - transform.position).sqrMagnitude < 900f;
            }
            if (orders.Bite > 0.0 && eye != null)
            {
                Vector3 chest = eye.position - transform.up * 0.35f;
                Audio.AudioDirector.Play("gore.splat", chest, 0.7f, 1.2f);
                PlayerHealth player = CombatWorld.Player;
                if (player != null) player.TakeDamage((float)orders.Bite, transform.forward);
            }
            return posedNear;
        }

        Vector3 Move(in BiterOrders orders, float dt)
        {
            Vector3 up = transform.up;
            if (orders.TurnRate != 0.0) transform.rotation = Quaternion.AngleAxis((float)orders.TurnRate * Mathf.Rad2Deg * dt, up) * transform.rotation;
            var local = new Vector3(0f, (float)orders.Climb, (float)orders.Speed) * dt;
            if (local.sqrMagnitude <= 0f) { _blocked = false; return Vector3.zero; }
            Vector3 step = transform.TransformDirection(local);
            Vector3 from = transform.position + transform.forward * 0.2f;
            if (Physics.SphereCast(from, 0.2f, step.normalized, out _, step.magnitude + 0.4f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                _blocked = true;
                transform.position += up * ((float)orders.Climb * dt);
                return new Vector3(0f, (float)orders.Climb * dt, 0f);
            }
            _blocked = false;
            transform.position += step;
            return local;
        }

        void Pose(float dt, in BiterOrders orders, Vector3 velocity)
        {
            var input = new BiterAnimInput { Velocity = D(velocity), TurnRate = orders.TurnRate, Mode = orders.Mode, T = orders.T, List = Body.List, BackFinOn = Body.BackFinOn };
            _animator.Evaluate(dt, input, _pose);
            for (int i = 0; i < BiterLayout.Nodes; i++)
                if (_node[i] != null) _node[i].SetLocalPositionAndRotation(V(_pose.NodePosition[i]), Q(_pose.NodeRotation[i]));
            if (_jaw != null) _jaw.SetLocalPositionAndRotation(V(_pose.JawPosition), Q(_pose.JawRotation));
            for (int side = 0; side < BiterLayout.Fins; side++)
                if (_fin[side] != null) _fin[side].SetLocalPositionAndRotation(V(_pose.FinPosition[side]), Q(_pose.FinRotation[side]));
            if (_backFin != null) _backFin.SetLocalPositionAndRotation(V(_pose.BackFinPosition), Q(_pose.BackFinRotation));
            if (_tailFin != null) _tailFin.SetLocalPositionAndRotation(V(_pose.TailFinPosition), Q(_pose.TailFinRotation));
        }

        // ------------------------------------------------------------------ being hit

        public HitOutcome TakeHit(BiterPart part, in HitInfo hit)
        {
            var outcome = new HitOutcome { Hit = true };
            if (!Alive) return outcome;
            outcome.Enemy = true;
            outcome.Target = this;
            BiterHit result;
            switch (part.Piece)
            {
                case BiterPiece.Fin:
                    result = Body.HitFin(part.Index, hit.Damage, hit.Sever);
                    outcome.Label = "Biter fin";
                    if (result.FinLost >= 0) LoseFin(result.FinLost, hit.Direction);
                    break;
                case BiterPiece.BackFin:
                    result = Body.HitBackFin(hit.Damage, hit.Sever);
                    outcome.Label = "Biter back fin";
                    if (result.BackFinLost) LoseBackFin(hit.Direction);
                    break;
                case BiterPiece.TailFin:
                    result = Body.HitTailFin(hit.Damage, hit.Sever);
                    outcome.Label = "Biter tail fin";
                    if (result.TailFinLost) LoseTailFin(hit.Direction);
                    break;
                case BiterPiece.Jaw:
                    result = Body.HitJaw(hit.Damage, hit.Sever);
                    outcome.Label = "Biter jaw";
                    if (result.JawLost) LoseJaw(hit.Direction);
                    break;
                case BiterPiece.Seg:
                    result = Body.HitTrunk(hit.Damage);
                    outcome.Label = "Biter body";
                    break;
                default:
                    result = Body.HitHead(hit.Damage);
                    outcome.Label = "Biter head";
                    break;
            }
            outcome.Severed = result.Lost;
            bool killed = result.Killed;
            if (hit.Lethal && !killed) killed = Body.HitHead(0, lethal: true).Killed;
            // Where it landed, for the hit marker: its head is its weak place and rings gold (it used to knock like armour,
            // which said the opposite), and the jaw shot off is its bite gone (WhereToShoot).
            outcome.Zone = WhereToShoot.Biter(part.Piece, part.Piece == BiterPiece.Jaw && result.JawLost);
            Vector3 push = transform.InverseTransformDirection(hit.Direction);
            if (!killed) Brain.Hurt();
            _animator.Flinch(new Vec3d(push.x, push.y, push.z) * (outcome.Severed ? 1.2 : 0.5));
            GoreFx gore = GoreFx.Instance;
            if (gore != null) gore.Burst(hit.Point, -hit.Direction, outcome.Severed ? 12 : 5, outcome.Severed ? 4.5f : 3f);
            if (killed)
            {
                outcome.Killed = true;
                Burst(hit);
            }
            // Hitstop and shake only when something came off or it died, as for people (Part B: on a limb popping): a hit
            // that takes nothing off is 0, or every shot on a creature froze the game for 50 ms (pass 7).
            outcome.Juice = outcome.Killed ? 0.8f : outcome.Severed ? 0.6f : 0f;
            return outcome;
        }

        Transform LooseParent => CombatWorld.DropParent != null ? CombatWorld.DropParent : transform.parent;

        void LoseFin(int side, Vector3 direction)
        {
            Transform fin = _fin[side];
            if (fin == null) return;
            _fin[side] = null;
            Transform on = _node[BiterLayout.FinSeg + 1];
            float s = (float)BiterLayout.Sign(side);
            Vector3 up = transform.up, outward = on != null ? on.TransformDirection(new Vector3(s, -0.2f, 0f).normalized) : up;
            if (on != null) Wound(on, V(BiterLayout.FinRootOf(side)), Quaternion.LookRotation(new Vector3(s, -0.3f, 0f), Vector3.up), 0.05f);
            WalkerChunk chunk = Throw(fin, "Biter fin", 0.4f, direction * 2f + outward * Random.Range(2f, 4f) + up * Random.Range(0.5f, 2f));
            Pop(chunk.transform.position, outward, 8, "gore.pop.limb");
        }

        void LoseBackFin(Vector3 direction)
        {
            Transform fin = _backFin;
            if (fin == null) return;
            _backFin = null;
            Transform seg = _node[BiterLayout.BackFinSeg + 1];
            Vector3 up = transform.up;
            if (seg != null) Wound(seg, V(BiterLayout.BackFinRoot), Quaternion.LookRotation(Vector3.up, Vector3.forward), 0.06f);
            WalkerChunk chunk = Throw(fin, "Biter back fin", 0.4f, direction * 2f + up * Random.Range(3f, 5f) + transform.right * Random.Range(-1.5f, 1.5f));
            Pop(chunk.transform.position, up, 8, "gore.pop.limb");
        }

        void LoseTailFin(Vector3 direction)
        {
            Transform fin = _tailFin;
            if (fin == null) return;
            _tailFin = null;
            Transform seg = _node[BiterLayout.Nodes - 1];
            Vector3 back = seg != null ? -seg.forward : -transform.forward;
            float size = (float)BiterLayout.WoundSize(BiterLayout.Segs - 1);
            if (seg != null) Wound(seg, V(BiterLayout.TailFinRoot) - new Vector3(0f, 0f, (float)BiterLayout.WoundOut), Quaternion.Euler(0f, 180f, 0f), size);
            WalkerChunk chunk = Throw(fin, "Biter tail fin", 0.8f, direction * 2f + back * Random.Range(2f, 4f) + transform.right * Random.Range(-2f, 2f));
            Wound(fin, Vector3.zero, Quaternion.identity, size);
            Pop(chunk.transform.position, back, 10, "gore.pop.limb");
        }

        void LoseJaw(Vector3 direction)
        {
            Transform jaw = _jaw;
            if (jaw == null) return;
            _jaw = null;
            Transform head = _node[0];
            Vector3 down = -transform.up;
            if (head != null) Wound(head, V(BiterLayout.JawHinge) + new Vector3(0f, -0.02f, 0.08f), Quaternion.LookRotation(new Vector3(0f, -1f, 0.35f), Vector3.forward), 0.08f);
            WalkerChunk chunk = Throw(jaw, "Biter jaw", 1.2f, direction * 2f + down * Random.Range(2f, 4f) + transform.forward * Random.Range(1f, 3f));
            Wound(jaw, new Vector3(0f, -0.03f, 0f), Quaternion.Euler(0f, 180f, 0f), 0.08f);
            Pop(chunk.transform.position, down, 12, "gore.pop.limb");
            _animator.Flinch(new Vec3d(0, 1.0, -1.0));
        }

        void Pop(Vector3 at, Vector3 way, int drops, string sound)
        {
            GoreFx gore = GoreFx.Instance;
            if (gore != null) gore.Burst(at, way, drops, 4f);
            Audio.AudioDirector.Play(sound, at, 0.8f, Random.Range(1.05f, 1.25f));
        }

        WalkerChunk Throw(Transform piece, string name, float mass, Vector3 velocity)
        {
            WalkerChunk chunk = WalkerChunk.Make(name, piece.position, piece.rotation, LooseParent, mass, _rules.pieceLife, _rules.maxLoosePieces);
            Loosen(piece, chunk.transform);
            chunk.Body.linearVelocity = WaterAt(piece.position).Flow + velocity;
            chunk.Body.angularVelocity = Random.onUnitSphere * Random.Range(2f, 6f);
            return chunk;
        }

        static void Loosen(Transform piece, Transform chunk, bool solid = true)
        {
            if (!solid) foreach (Collider collider in piece.GetComponentsInChildren<Collider>()) collider.enabled = false;
            piece.SetParent(chunk, true);
            foreach (Transform t in piece.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Gib;
            foreach (BiterPart part in piece.GetComponentsInChildren<BiterPart>(true))
            {
                part.Owner = null;
                Destroy(part);
            }
            GibManager gibs = GibManager.Instance;
            if (solid && gibs != null) foreach (Collider collider in piece.GetComponents<Collider>()) collider.sharedMaterial = gibs.BouncyMaterial;
        }

        void Wound(Transform on, Vector3 at, Quaternion facing, float size)
        {
            UnityEngine.Mesh mesh = ModelLibrary.PartMesh(BiterLayout.Model, BiterLayout.Stump);
            if (mesh == null) return;
            var go = new GameObject("Wound");
            go.layer = Layers.Gib;
            go.transform.SetParent(on, false);
            go.transform.SetLocalPositionAndRotation(at, facing);
            go.transform.localScale = Vector3.one * (size / (float)BiterLayout.StumpRadius);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Quiet(go.AddComponent<MeshRenderer>());
        }

        /// <summary>It dies: it comes apart at every joint, fins and jaw thrown off.</summary>
        void Burst(in HitInfo hit)
        {
            Vector3 up = transform.up;
            Vector3 middle = _node[2] != null ? _node[2].position : transform.position;
            for (int side = 0; side < BiterLayout.Fins; side++)
                if (_fin[side] != null) LoseFin(side, hit.Direction);
            if (_backFin != null) LoseBackFin(hit.Direction);
            if (_tailFin != null) LoseTailFin(hit.Direction);
            for (int i = 0; i < BiterLayout.Nodes; i++)
            {
                Transform node = _node[i];
                if (node == null) continue;
                _node[i] = null;
                Vector3 away = node.position - middle;
                away = away.sqrMagnitude > 0.0001f ? away.normalized : up;
                Vector3 velocity = away * Random.Range(2f, 5f) + node.right * Random.Range(-2f, 2f) + up * Random.Range(0.5f, 3f) + hit.Direction * 2.5f;
                WalkerChunk chunk = Throw(node, i == 0 ? "Biter head" : "Biter body", i == 0 ? 3f : 2f, velocity);
                float out_ = (float)(BiterLayout.Spacing * 0.5 + BiterLayout.WoundOut);
                if (i > 0)
                {
                    float size = (float)BiterLayout.WoundSize(i - 1);
                    Wound(node, new Vector3(0f, 0f, out_), Quaternion.identity, size);
                    if (i < BiterLayout.Nodes - 1) Wound(node, new Vector3(0f, 0f, -out_), Quaternion.Euler(0f, 180f, 0f), size);
                }
                else
                {
                    Wound(node, new Vector3(0f, 0f, -out_), Quaternion.Euler(0f, 180f, 0f), (float)BiterLayout.WoundSize(0));
                    if (_jaw != null)
                    {
                        Loosen(_jaw, chunk.transform, false);
                        _jaw = null;
                    }
                }
            }
            UnityEngine.Mesh meat = ModelLibrary.PartMesh(BiterLayout.Model, BiterLayout.Meat[0]);
            if (meat != null)
            {
                WalkerChunk chunk = WalkerChunk.Make("Biter meat", middle, Random.rotation, LooseParent, 0.5f, _rules.pieceLife, _rules.maxLoosePieces);
                chunk.gameObject.AddComponent<MeshFilter>().sharedMesh = meat;
                chunk.transform.localScale = Vector3.one * 0.4f;
                Quiet(chunk.gameObject.AddComponent<MeshRenderer>());
                chunk.gameObject.AddComponent<SphereCollider>().radius = 0.09f;
                chunk.Body.linearVelocity = WaterAt(middle).Flow + (Random.onUnitSphere + up * 0.5f).normalized * Random.Range(3f, 5f);
            }
            GoreFx gore = GoreFx.Instance;
            if (gore != null)
            {
                gore.Burst(middle, up, 26, 6f);
                gore.Burst(middle, hit.Direction, 14, 5f);
            }
            Audio.AudioDirector.Play("gore.pop.torso", middle, 0.9f, 1.2f);
            Brain.Died();
            ModelDetail.LetGo(gameObject);
            Died?.Invoke(this);
            Everyone.Remove(this);
            Destroy(gameObject);
        }
    }
}
