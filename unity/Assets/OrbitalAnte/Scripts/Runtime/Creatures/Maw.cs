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
    public enum MawPiece { Head, Jaw, Seg, Fin, BackFin, TailFin, Throat }

    /// <summary>One hitbox of a Drifting maw: its skull, its lower jaw, a length of body, a fin, or the way down its open throat.</summary>
    public sealed class MawPart : MonoBehaviour, IHittable
    {
        [System.NonSerialized] public Maw Owner;
        [System.NonSerialized] public MawPiece Piece;
        /// <summary>Which length (0 to 7) or which front fin (0 or 1).</summary>
        [System.NonSerialized] public int Index;

        public HitOutcome Hit(in HitInfo hit) => Owner != null ? Owner.TakeHit(this, hit) : new HitOutcome { Hit = true };

        public string DescribeForDebug()
        {
            if (Owner == null) return "Creature piece";
            MawBody body = Owner.Body;
            switch (Piece)
            {
                case MawPiece.Head: return "Maw skull " + Mathf.CeilToInt((float)body.Head);
                case MawPiece.Seg: return "Maw body " + Mathf.CeilToInt((float)body.Trunk);
                case MawPiece.Throat: return "Maw throat " + Mathf.CeilToInt((float)body.Trunk);
                case MawPiece.Jaw: return "Maw jaw";
                default: return "Maw fin";
            }
        }
    }

    /// <summary>What the water is like at a place: asked by a Drifting maw of whoever put it in a sea.</summary>
    public struct MawWater
    {
        /// <summary>Metres of water over the place (0 or less: it is out of the water), and from it down to the sea floor.</summary>
        public float Above, Below;
        /// <summary>Which way is up there.</summary>
        public Vector3 Up;
        /// <summary>How the water itself is moving there, in Unity's space (a spinning world carries its sea round).</summary>
        public Vector3 Flow;
    }

    /// <summary>
    /// The Drifting maw in the world: an alien from the megamouth shark (Matthew, 17 Nov 2026), the first creature
    /// that swims. The rules are plain C# (<see cref="MawBody"/>, <see cref="MawAnimator"/>, <see cref="MawBrain"/>);
    /// this turns them into objects.
    ///
    /// - Its head, lower jaw, eight lengths, two front fins, back fin and tail fin are each an object and a hitbox.
    ///   Fins and jaw come off whole (<see cref="WalkerChunk"/>, shared with the other creatures). While its mouth
    ///   is wide there is one more hitbox between its jaws: the way down its throat.
    /// - It is not a physics object while alive: its brain moves its head through the water and the body follows.
    ///   "Up" is what <see cref="Water"/> says, never +Y; in a sea it is a child of its world.
    /// - Its pull is a push on the player toward its mouth, shown as pale rings racing in along the cone it draws
    ///   from. In the suit that is a force on the suit; the arena's player (who has no water) is slid over the ground.
    /// - In the combat arena it swims in the air a little over the ground, as a stand-in for a sea.
    /// </summary>
    public sealed class Maw : MonoBehaviour
    {
        public MawBody Body { get; private set; }
        public MawBrain Brain { get; private set; }
        public bool Alive => Body != null && Body.Alive;
        public event System.Action<Maw> Died;
        /// <summary>What the water is like at a place in Unity's space. Null: the combat arena, where the air stands in for a sea.</summary>
        [System.NonSerialized] public System.Func<Vector3, MawWater> Water;

        static readonly List<Maw> Everyone = new List<Maw>();
        public static IReadOnlyList<Maw> All => Everyone;
        public static void ResetStatics() => Everyone.Clear();

        const string GapeSound = "creature.maw.gape", InhaleSound = "creature.maw.inhale", ChokeSound = "creature.maw.choke";
        /// <summary>In the arena it is told the floor is this much lower than it is, so its mouth comes down to someone standing on it.</summary>
        const float ArenaSink = 1.3f;

        MawRules _rules;
        readonly MawAnimator _animator = new MawAnimator();
        readonly MawPose _pose = new MawPose();
        readonly Transform[] _node = new Transform[MawLayout.Nodes];
        readonly Transform[] _fin = new Transform[MawLayout.Fins];
        Transform _jaw, _backFin, _tailFin;
        SphereCollider _throat;
        ScreamRings _rings;
        Vector3 _homeLocal, _thrown;
        bool _blocked, _lookedForPlayer;
        float _unposed, _ringIn;
        FpsController _walker;
        CharacterController _walkerBody;
        OrbitalAnte.Ship.Astronaut _crew;
        MawMode _lastMode = MawMode.Drift;
        Material _material;

        /// <summary>Makes a Drifting maw with its head at a place. Null if its model is not in the game.</summary>
        public static Maw Create(Vector3 position, Quaternion rotation, Transform parent, CreatureConfig config, string name = null, System.Func<Vector3, MawWater> water = null)
        {
            if (ModelLibrary.Find(MawLayout.Model) == null)
            {
                Debug.LogWarning("Orbital Ante: the Drifting maw's model (Resources/Models/" + MawLayout.Model + ") is missing, so none was made.");
                return null;
            }
            var go = new GameObject(name ?? MawLayout.Model);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var maw = go.AddComponent<Maw>();
            maw.Water = water;
            maw.Build(config != null ? config.maw : new MawRules());
            return maw;
        }

        void Build(MawRules rules)
        {
            _rules = rules;
            Body = new MawBody(rules.hp);
            Brain = new MawBrain(rules);
            _homeLocal = HomeSpace(transform.position);
            _material = ModelLibrary.MaterialFor(MawLayout.Model);

            // Its hitboxes move with it every frame: a kinematic body tells the physics so.
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            for (int i = 0; i < MawLayout.Nodes; i++)
            {
                bool head = i == 0;
                var node = new GameObject(head ? "Head" : "Body " + (i - 1));
                node.layer = Layers.Hitbox;
                node.transform.SetParent(transform, false);
                _node[i] = node.transform;
                Mesh(head ? MawLayout.HeadPiece : MawLayout.PieceOf(i - 1), node.transform);
                var box = node.AddComponent<BoxCollider>();
                if (head)
                {
                    // Only what is above its mouth: the jaw has a box of its own, and between them is its throat.
                    float tall = (float)MawLayout.HeadHalfHeight + 0.05f;
                    box.center = new Vector3(0f, (float)MawLayout.HeadHalfHeight - tall * 0.5f, ((float)MawLayout.HeadLength - (float)MawLayout.Spacing) * 0.5f);
                    box.size = new Vector3((float)MawLayout.HeadHalfWidth * 2f, tall, (float)MawLayout.HeadLength);
                    Tag(node, MawPiece.Head, 0);
                    // The way down its throat: there only while its mouth is wide.
                    var throat = new GameObject("Throat");
                    throat.layer = Layers.Hitbox;
                    throat.transform.SetParent(node.transform, false);
                    _throat = throat.AddComponent<SphereCollider>();
                    _throat.center = new Vector3(0f, (float)MawLayout.Mouth.Y - 0.14f, (float)MawLayout.Mouth.Z - 0.45f);
                    _throat.radius = (float)MawLayout.ThroatRadius * 0.85f;
                    _throat.enabled = false;
                    Tag(throat, MawPiece.Throat, 0);
                }
                else
                {
                    box.size = new Vector3((float)MawLayout.HalfWidth[i - 1] * 2f, (float)MawLayout.HalfHeight[i - 1] * 2f, (float)MawLayout.Spacing);
                    Tag(node, MawPiece.Seg, i - 1);
                }
            }
            _jaw = Mesh(MawLayout.JawPiece, transform);
            _jaw.name = "Jaw";
            var jawBox = _jaw.gameObject.AddComponent<BoxCollider>();
            jawBox.center = new Vector3(0f, -0.20f, (float)MawLayout.JawLength * 0.5f);
            jawBox.size = new Vector3((float)MawLayout.JawHalfWidth * 2f, 0.46f, (float)MawLayout.JawLength);
            Tag(_jaw.gameObject, MawPiece.Jaw, 0);
            for (int side = 0; side < MawLayout.Fins; side++)
            {
                // The same piece either side: the pose turns the right one over.
                Transform fin = Mesh(MawLayout.FinPiece, transform);
                fin.name = side == MawLayout.RightSide ? "Right fin" : "Left fin";
                var box = fin.gameObject.AddComponent<BoxCollider>();
                // A little thicker than it is drawn, so a fin can be shot off on purpose.
                box.center = new Vector3(-0.10f, 0f, (float)MawLayout.FinLength * 0.5f);
                box.size = new Vector3((float)MawLayout.FinChord, 0.16f, (float)MawLayout.FinLength);
                Tag(fin.gameObject, MawPiece.Fin, side);
                _fin[side] = fin;
            }
            _backFin = Mesh(MawLayout.BackFinPiece, transform);
            var backBox = _backFin.gameObject.AddComponent<BoxCollider>();
            backBox.center = new Vector3(0f, (float)MawLayout.BackFinHeight * 0.5f, -0.17f);
            backBox.size = new Vector3(0.16f, (float)MawLayout.BackFinHeight, (float)MawLayout.BackFinChord);
            Tag(_backFin.gameObject, MawPiece.BackFin, 0);
            _tailFin = Mesh(MawLayout.TailFinPiece, transform);
            var tailBox = _tailFin.gameObject.AddComponent<BoxCollider>();
            float tailUp = (float)MawLayout.TailFinUp, tailDown = (float)MawLayout.TailFinDown, tailBack = (float)MawLayout.TailFinBack;
            tailBox.center = new Vector3(0f, (tailUp - tailDown) * 0.5f, -tailBack * 0.5f);
            tailBox.size = new Vector3(0.18f, tailUp + tailDown, tailBack);
            Tag(_tailFin.gameObject, MawPiece.TailFin, 0);

            _rings = ScreamRings.Make(transform);
            _rings.Inward = true;
            ModelDetail.Attach(gameObject, MawLayout.Model, DetailRule.BodyRange, true);
            Everyone.Add(this);
            Pose(0.0001f, new MawOrders(), Vector3.zero);
        }

        Transform Mesh(string piece, Transform parent, bool hitbox = true)
        {
            var go = new GameObject(piece);
            go.layer = hitbox ? Layers.Hitbox : Layers.Gib;
            go.transform.SetParent(parent, false);
            UnityEngine.Mesh shape = ModelLibrary.PartMesh(MawLayout.Model, piece);
            // An old model file has no piece of this name: say so, or the body would be hittable and not drawn.
            if (shape == null) Debug.LogWarning("The " + MawLayout.Model + " model has no piece called " + piece + ". Build the model again (tools/blender/build_creatures.py).");
            go.AddComponent<MeshFilter>().sharedMesh = shape;
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

        void Tag(GameObject go, MawPiece piece, int index)
        {
            var part = go.AddComponent<MawPart>();
            part.Owner = this;
            part.Piece = piece;
            part.Index = index;
        }

        void OnDestroy() => Everyone.Remove(this);

        static Vector3 V(Vec3d v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        static Quaternion Q(QuatD q) => new Quaternion((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);
        static Vec3d D(Vector3 v) => new Vec3d(v.x, v.y, v.z);

        // Where it lives is kept in its parent's space: in a sea its world turns and moves under it.
        Vector3 HomeSpace(Vector3 world) => transform.parent != null ? transform.parent.InverseTransformPoint(world) : world;
        Vector3 HomeWorld => transform.parent != null ? transform.parent.TransformPoint(_homeLocal) : _homeLocal;

        // ------------------------------------------------------------------ every frame

        MawWater WaterAt(Vector3 at)
        {
            if (Water != null) return Water(at);
            // The arena: its air stands in for a sea, and the ground is the sea floor.
            Vector3 up = transform.up;
            float below = 40f;
            if (Physics.Raycast(at, -up, out RaycastHit ground, 40f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) below = ground.distance;
            return new MawWater { Above = 30f, Below = below + ArenaSink, Up = up, Flow = Vector3.zero };
        }

        void FindPlayer(Transform eye)
        {
            if (_lookedForPlayer) return;
            _lookedForPlayer = true;
            _walker = eye.GetComponentInParent<FpsController>();
            if (_walker != null) _walkerBody = _walker.GetComponent<CharacterController>();
            else _crew = FindAnyObjectByType<OrbitalAnte.Ship.Astronaut>();
        }

        /// <summary>The suit, when the player is floating free in it (not aboard, not driving).</summary>
        Rigidbody Suit => _crew != null && _crew.Mode == OrbitalAnte.Ship.AstronautMode.Outside && !_crew.Dead ? _crew.Suit : null;

        MawSenses Senses(in MawWater here, out Transform eye, out bool hostile)
        {
            var senses = new MawSenses
            {
                Blocked = _blocked, Home = D(transform.InverseTransformPoint(HomeWorld)),
                WaterAbove = here.Above, FloorBelow = here.Below,
            };
            eye = CombatWorld.PlayerPresent ? CombatWorld.PlayerEye : null;
            hostile = eye != null && !CombatWorld.EnemiesHoldFire;
            if (hostile)
            {
                FindPlayer(eye);
                Vector3 chest = eye.position - here.Up * 0.5f;
                senses.Hostile = true;
                senses.Player = D(transform.InverseTransformPoint(chest));
                // It takes no notice of anyone who is not in the water with it (in the arena, everyone is).
                senses.PlayerInWater = Water == null || (Suit != null && Water(chest).Above > 0.3f);
            }
            return senses;
        }

        void Update()
        {
            if (!Alive || GameSession.Paused) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;

            MawWater here = WaterAt(transform.position);
            // "Up" changes as it swims round its world.
            if (here.Up.sqrMagnitude > 0.5f) transform.rotation = Quaternion.FromToRotation(transform.up, here.Up) * transform.rotation;

            MawSenses senses = Senses(here, out Transform eye, out bool hostile);
            MawOrders orders = Brain.Think(dt, senses, Body);
            if (orders.Mode != _lastMode)
            {
                Vector3 at = MouthPoint();
                // Each change the player must notice is heard (other sounds stand in until its own are there).
                if (orders.Mode == MawMode.Gape && !Audio.AudioDirector.Play(GapeSound, at)) Audio.AudioDirector.Play("enemy.flare.raise", at, 0.9f, 0.5f);
                if (orders.Mode == MawMode.Inhale && !Audio.AudioDirector.Play(InhaleSound, at)) Audio.AudioDirector.Play("enemy.heavy.spinup", at, 0.9f, 0.6f);
                if (orders.Mode == MawMode.Reel && !Audio.AudioDirector.Play(ChokeSound, at)) Audio.AudioDirector.Play("gore.splat", at, 1f, 0.6f);
                _lastMode = orders.Mode;
            }
            Vector3 moved = Move(orders, dt);
            // Cruising far from anyone it is posed five times a second, not every frame.
            Transform near = CombatWorld.PlayerEye;
            bool far = near == null || (near.position - transform.position).sqrMagnitude > 90f * 90f;
            _unposed += dt;
            if (!far || orders.Mode != MawMode.Drift || _unposed >= 0.2f)
            {
                Pose(_unposed, orders, moved / dt);
                _unposed = 0f;
                // Near the player its hitboxes must be where it is drawn when their shot is traced: its fins are thin.
                if (near != null && (near.position - transform.position).sqrMagnitude < 1600f) Physics.SyncTransforms();
            }
            Rings(orders, dt);
            if (hostile) Player(orders, here, eye, dt);
        }

        Vector3 MouthPoint() => transform.TransformPoint(V(_pose.Mouth));

        /// <summary>Its pull made visible: a pale ring sets off from the edge of its reach every beat and races in to its mouth.</summary>
        void Rings(in MawOrders orders, float dt)
        {
            if (_rings == null) return;
            bool drawing = orders.Mode == MawMode.Inhale;
            bool beat = false;
            if (drawing)
            {
                _ringIn -= dt;
                if (_ringIn <= 0f) { beat = true; _ringIn = 0.3f; }
            }
            else _ringIn = 0f;
            Transform head = _node[0];
            Vector3 forward = head != null ? head.forward : transform.forward;
            _rings.Run(drawing, beat, MouthPoint(), forward, transform.up, _rules.pullReach * (float)Body.PullShare, _rules.pullCone, dt);
        }

        /// <summary>What it does to the player this frame: draws them in, holds them in its mouth, bites, throws them out.</summary>
        void Player(in MawOrders orders, in MawWater here, Transform eye, float dt)
        {
            Rigidbody suit = Suit;
            Vector3 pull = transform.TransformDirection(V(orders.Pull));
            if (orders.Holding)
            {
                // They go where its mouth goes.
                Vector3 want = MouthPoint() - transform.up * 0.3f;
                if (suit != null)
                {
                    Vector3 to = (want - suit.position) / Mathf.Max(dt, 0.02f);
                    suit.linearVelocity = here.Flow + Vector3.ClampMagnitude(to, 14f);
                }
                else if (_walker != null) _walker.Carry(Vector3.ClampMagnitude((want - _walker.transform.position) / Mathf.Max(dt, 0.02f), 10f));
            }
            else
            {
                if (suit != null)
                {
                    if (pull != Vector3.zero) suit.AddForce(pull * dt, ForceMode.VelocityChange);
                    if (orders.Spit.SqrMagnitude > 0.0) suit.linearVelocity = here.Flow + transform.TransformDirection(V(orders.Spit));
                }
                else if (_walkerBody != null)
                {
                    // The arena's player has no water to be drawn through: they are slid over the ground, and can still walk against it.
                    if (orders.Spit.SqrMagnitude > 0.0) _thrown = transform.TransformDirection(V(orders.Spit));
                    Vector3 slide = Vector3.ProjectOnPlane(pull * 0.5f + _thrown, transform.up);
                    if (slide.sqrMagnitude > 0.0001f && _walkerBody.enabled) _walkerBody.Move(slide * dt);
                    _thrown *= Mathf.Max(0f, 1f - 2.5f * dt);
                }
            }
            if (orders.Bite > 0.0)
            {
                Vector3 chest = eye.position - transform.up * 0.35f;
                Audio.AudioDirector.Play("gore.splat", chest, 0.6f, 0.8f);
                PlayerHealth player = CombatWorld.Player;
                Transform head = _node[0];
                if (player != null) player.TakeDamage((float)orders.Bite, head != null ? head.forward : transform.forward);
            }
        }

        /// <summary>Moves the head; the answer is how far it really went, in its own frame after the turn.</summary>
        Vector3 Move(in MawOrders orders, float dt)
        {
            Vector3 up = transform.up;
            if (orders.TurnRate != 0.0) transform.rotation = Quaternion.AngleAxis((float)orders.TurnRate * Mathf.Rad2Deg * dt, up) * transform.rotation;
            var local = new Vector3(0f, (float)orders.Climb, (float)orders.Speed) * dt;
            if (local.sqrMagnitude <= 0f) { _blocked = false; return Vector3.zero; }
            Vector3 step = transform.TransformDirection(local);
            // Stopped by anything solid in front of its head (where the sea floor has no collider, its brain keeps it off the bottom by depth).
            Vector3 from = transform.position + transform.forward * 0.6f;
            if (Physics.SphereCast(from, 0.6f, step.normalized, out _, step.magnitude + 1.2f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                _blocked = true;
                // It may still rise or sink away from what it met.
                Vector3 rise = up * ((float)orders.Climb * dt);
                transform.position += rise;
                return new Vector3(0f, (float)orders.Climb * dt, 0f);
            }
            _blocked = false;
            transform.position += step;
            return local;
        }

        void Pose(float dt, in MawOrders orders, Vector3 velocity)
        {
            var input = new MawAnimInput { Velocity = D(velocity), TurnRate = orders.TurnRate, Mode = orders.Mode, T = orders.T, List = Body.List, BackFinOn = Body.BackFinOn };
            _animator.Evaluate(dt, input, _pose);
            for (int i = 0; i < MawLayout.Nodes; i++)
                if (_node[i] != null) _node[i].SetLocalPositionAndRotation(V(_pose.NodePosition[i]), Q(_pose.NodeRotation[i]));
            if (_jaw != null) _jaw.SetLocalPositionAndRotation(V(_pose.JawPosition), Q(_pose.JawRotation));
            for (int side = 0; side < MawLayout.Fins; side++)
                if (_fin[side] != null) _fin[side].SetLocalPositionAndRotation(V(_pose.FinPosition[side]), Q(_pose.FinRotation[side]));
            if (_backFin != null) _backFin.SetLocalPositionAndRotation(V(_pose.BackFinPosition), Q(_pose.BackFinRotation));
            if (_tailFin != null) _tailFin.SetLocalPositionAndRotation(V(_pose.TailFinPosition), Q(_pose.TailFinRotation));
            // The way down its throat is open only while its mouth is wide.
            if (_throat != null) _throat.enabled = _pose.Gape > 0.5 && Body.JawOn;
        }

        // ------------------------------------------------------------------ being hit

        public HitOutcome TakeHit(MawPart part, in HitInfo hit)
        {
            var outcome = new HitOutcome { Hit = true };
            if (!Alive) return outcome;
            outcome.Enemy = true;
            MawHit result;
            bool armour = false, soft = false, choked = false;
            switch (part.Piece)
            {
                case MawPiece.Fin:
                    result = Body.HitFin(part.Index, hit.Damage, hit.Sever);
                    outcome.Label = "Maw fin";
                    if (result.FinLost >= 0) LoseFin(result.FinLost, hit.Direction);
                    break;
                case MawPiece.BackFin:
                    result = Body.HitBackFin(hit.Damage, hit.Sever);
                    outcome.Label = "Maw back fin";
                    if (result.BackFinLost) LoseBackFin(hit.Direction);
                    break;
                case MawPiece.TailFin:
                    result = Body.HitTailFin(hit.Damage, hit.Sever);
                    outcome.Label = "Maw tail fin";
                    if (result.TailFinLost) LoseTailFin(hit.Direction);
                    break;
                case MawPiece.Jaw:
                    result = Body.HitJaw(hit.Damage, hit.Sever);
                    outcome.Label = "Maw jaw";
                    if (result.JawLost) LoseJaw(hit.Direction);
                    break;
                case MawPiece.Throat:
                    result = Body.HitTrunk(hit.Damage, throat: true);
                    outcome.Label = "Maw throat";
                    soft = true;
                    // Enough down its throat and it chokes: whatever it was doing is over.
                    choked = !result.Killed && Brain.Throat(result.Throat);
                    break;
                case MawPiece.Seg:
                    result = Body.HitTrunk(hit.Damage);
                    outcome.Label = "Maw body";
                    soft = true;
                    break;
                default:
                    result = Body.HitHead(hit.Damage);
                    outcome.Label = "Maw skull";
                    armour = true;
                    break;
            }
            outcome.Severed = result.Lost;
            bool killed = result.Killed;
            // A one-hit-kill gun kills wherever it lands, after the struck piece has taken its hit.
            if (hit.Lethal && !killed) killed = Body.HitHead(0, lethal: true).Killed;
            Vector3 push = transform.InverseTransformDirection(hit.Direction);
            if (!killed)
            {
                MawSenses senses = Senses(WaterAt(transform.position), out _, out bool hostile);
                // Hurt by someone it can reach it comes for them; by someone on the shore it makes off and down.
                if (hostile) Brain.Hurt(senses.PlayerInWater);
                if (choked)
                {
                    _animator.Flinch(new Vec3d(0, 1.0, -2.5));
                    if (!Audio.AudioDirector.Play(ChokeSound, hit.Point)) Audio.AudioDirector.Play("gore.splat", hit.Point, 1f, 0.6f);
                }
            }
            _animator.Flinch(new Vec3d(push.x, push.y, push.z) * (outcome.Severed ? 1.5 : 0.3));
            GoreFx gore = GoreFx.Instance;
            // The skull says it is bone: no blood, a dull knock, until it breaks. Everything else bleeds.
            if (armour && !killed) Audio.AudioDirector.Play("impact.armor", hit.Point, 0.7f, 0.7f);
            else if (gore != null) gore.Burst(hit.Point, -hit.Direction, outcome.Severed ? 18 : soft ? 9 : 5, outcome.Severed ? 6f : soft ? 4.5f : 3f);
            if (killed)
            {
                outcome.Killed = true;
                Burst(hit);
            }
            outcome.Juice = outcome.Killed ? 1f : outcome.Severed ? 0.7f : choked ? 0.6f : armour ? 0.04f : part.Piece == MawPiece.Throat ? 0.3f : soft ? 0.15f : 0.12f;
            return outcome;
        }

        Transform LooseParent => CombatWorld.DropParent != null ? CombatWorld.DropParent : transform.parent;

        void LoseFin(int side, Vector3 direction)
        {
            Transform fin = _fin[side];
            if (fin == null) return;
            _fin[side] = null;
            Transform seg = _node[MawLayout.FinSeg + 1];
            float s = (float)MawLayout.Sign(side);
            Vector3 up = transform.up, outward = seg != null ? seg.TransformDirection(new Vector3(s, -0.2f, 0f).normalized) : up;
            if (seg != null) Wound(seg, V(MawLayout.FinRootOf(side)), Quaternion.LookRotation(new Vector3(s, -0.3f, 0f), Vector3.up), 0.22f);
            WalkerChunk chunk = Throw(fin, "Maw fin", 8f, direction * 2f + outward * Random.Range(2f, 4f) + up * Random.Range(0.5f, 2f));
            Wound(fin, Vector3.zero, Quaternion.Euler(0f, 180f, 0f), 0.20f);
            Pop(chunk.transform.position, outward, 18, "gore.pop.limb");
        }

        void LoseBackFin(Vector3 direction)
        {
            Transform fin = _backFin;
            if (fin == null) return;
            _backFin = null;
            Transform seg = _node[MawLayout.BackFinSeg + 1];
            Vector3 up = transform.up;
            if (seg != null) Wound(seg, V(MawLayout.BackFinRoot), Quaternion.LookRotation(Vector3.up, Vector3.forward), 0.22f);
            WalkerChunk chunk = Throw(fin, "Maw back fin", 6f, direction * 2f + up * Random.Range(3f, 5f) + transform.right * Random.Range(-1.5f, 1.5f));
            Pop(chunk.transform.position, up, 16, "gore.pop.limb");
        }

        void LoseTailFin(Vector3 direction)
        {
            Transform fin = _tailFin;
            if (fin == null) return;
            _tailFin = null;
            Transform seg = _node[MawLayout.Nodes - 1];
            Vector3 back = seg != null ? -seg.forward : -transform.forward;
            if (seg != null) Wound(seg, V(MawLayout.TailFinRoot) - new Vector3(0f, 0f, (float)MawLayout.WoundOut), Quaternion.Euler(0f, 180f, 0f), (float)MawLayout.WoundSize(MawLayout.Segs - 1));
            WalkerChunk chunk = Throw(fin, "Maw tail fin", 10f, direction * 2f + back * Random.Range(2f, 4f) + transform.right * Random.Range(-2f, 2f));
            Wound(fin, Vector3.zero, Quaternion.identity, (float)MawLayout.WoundSize(MawLayout.Segs - 1));
            Pop(chunk.transform.position, back, 22, "gore.pop.limb");
        }

        void LoseJaw(Vector3 direction)
        {
            Transform jaw = _jaw;
            if (jaw == null) return;
            _jaw = null;
            Transform head = _node[0];
            Vector3 down = -transform.up;
            if (head != null) Wound(head, V(MawLayout.JawHinge) + new Vector3(0f, -0.08f, 0.25f), Quaternion.LookRotation(new Vector3(0f, -1f, 0.35f), Vector3.forward), 0.34f);
            WalkerChunk chunk = Throw(jaw, "Maw jaw", 14f, direction * 2f + down * Random.Range(2f, 4f) + transform.forward * Random.Range(1f, 3f));
            Wound(jaw, new Vector3(0f, -0.12f, 0f), Quaternion.Euler(0f, 180f, 0f), 0.34f);
            Pop(chunk.transform.position, down, 26, "gore.pop.limb");
            // Whoever it holds is free, and it makes off.
            Brain.JawLost();
            _animator.Flinch(new Vec3d(0, 1.5, -1.5));
        }

        void Pop(Vector3 at, Vector3 way, int drops, string sound)
        {
            GoreFx gore = GoreFx.Instance;
            if (gore != null) gore.Burst(at, way, drops, 5f);
            Audio.AudioDirector.Play(sound, at, 0.9f, Random.Range(0.75f, 0.95f));
        }

        /// <summary>A piece leaves the animal as a loose physics object.</summary>
        WalkerChunk Throw(Transform piece, string name, float mass, Vector3 velocity)
        {
            WalkerChunk chunk = WalkerChunk.Make(name, piece.position, piece.rotation, LooseParent, mass, _rules.pieceLife, _rules.maxLoosePieces);
            Loosen(piece, chunk.transform);
            // Thrown from the water it was in: a spinning world's sea is moving.
            chunk.Body.linearVelocity = WaterAt(piece.position).Flow + velocity;
            chunk.Body.angularVelocity = Random.onUnitSphere * Random.Range(1.5f, 4f);
            return chunk;
        }

        static void Loosen(Transform piece, Transform chunk, bool solid = true)
        {
            // What only rides along has its collider switched off before it is moved, so the physics never takes it up.
            if (!solid) foreach (Collider collider in piece.GetComponentsInChildren<Collider>()) collider.enabled = false;
            piece.SetParent(chunk, true);
            foreach (Transform t in piece.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Gib;
            foreach (MawPart part in piece.GetComponentsInChildren<MawPart>(true))
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
            UnityEngine.Mesh mesh = ModelLibrary.PartMesh(MawLayout.Model, MawLayout.Stump);
            if (mesh == null) return;
            var go = new GameObject("Wound");
            go.layer = Layers.Gib;
            go.transform.SetParent(on, false);
            go.transform.SetLocalPositionAndRotation(at, facing);
            go.transform.localScale = Vector3.one * (size / (float)MawLayout.StumpRadius);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Quiet(go.AddComponent<MeshRenderer>());
        }

        /// <summary>It dies: it comes apart at every joint, fins and jaw thrown off, and what was inside comes out.</summary>
        void Burst(in HitInfo hit)
        {
            Vector3 up = transform.up;
            Vector3 middle = _node[3] != null ? _node[3].position : transform.position;
            if (_rings != null) _rings.Stop();
            if (_throat != null) _throat.enabled = false;
            for (int side = 0; side < MawLayout.Fins; side++)
                if (_fin[side] != null) LoseFin(side, hit.Direction);
            if (_backFin != null) LoseBackFin(hit.Direction);
            if (_tailFin != null) LoseTailFin(hit.Direction);
            for (int i = 0; i < MawLayout.Nodes; i++)
            {
                Transform node = _node[i];
                if (node == null) continue;
                _node[i] = null;
                Vector3 away = node.position - middle;
                away = away.sqrMagnitude > 0.0001f ? away.normalized : up;
                Vector3 velocity = away * Random.Range(1.5f, 4f) + node.right * Random.Range(-2f, 2f) + up * Random.Range(0.5f, 3f) + hit.Direction * 2f;
                WalkerChunk chunk = Throw(node, i == 0 ? "Maw head" : "Maw body", i == 0 ? 40f : 20f, velocity);
                if (i > 0)
                {
                    // Torn apart at every joint: a length has a wound at each end.
                    float size = (float)MawLayout.WoundSize(i - 1), out_ = (float)(MawLayout.Spacing * 0.5 + MawLayout.WoundOut);
                    Wound(node, new Vector3(0f, 0f, out_), Quaternion.identity, size);
                    if (i < MawLayout.Nodes - 1) Wound(node, new Vector3(0f, 0f, -out_), Quaternion.Euler(0f, 180f, 0f), size);
                }
                else
                {
                    Wound(node, new Vector3(0f, 0f, -(float)(MawLayout.Spacing * 0.5 + MawLayout.WoundOut)), Quaternion.Euler(0f, 180f, 0f), (float)MawLayout.WoundSize(0));
                    if (_jaw != null)
                    {
                        Loosen(_jaw, chunk.transform, false);
                        _jaw = null;
                    }
                }
            }
            for (int k = 0; k < MawLayout.Meat.Length; k++)
            {
                UnityEngine.Mesh mesh = ModelLibrary.PartMesh(MawLayout.Model, MawLayout.Meat[k]);
                if (mesh == null) continue;
                for (int n = 0; n < 2; n++)
                {
                    Vector3 at = middle + Random.insideUnitSphere * 0.5f;
                    WalkerChunk chunk = WalkerChunk.Make("Maw meat", at, Random.rotation, LooseParent, 3f, _rules.pieceLife, _rules.maxLoosePieces);
                    chunk.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    Quiet(chunk.gameObject.AddComponent<MeshRenderer>());
                    var ball = chunk.gameObject.AddComponent<SphereCollider>();
                    ball.radius = 0.09f;
                    chunk.Body.linearVelocity = WaterAt(at).Flow + (Random.onUnitSphere + up * 0.5f).normalized * Random.Range(3f, 6f);
                    chunk.Body.angularVelocity = Random.onUnitSphere * 6f;
                }
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
    }
}
