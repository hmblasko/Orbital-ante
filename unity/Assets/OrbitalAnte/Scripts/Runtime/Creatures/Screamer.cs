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
    /// <summary>One hitbox of a Screamer: its fore body, its abdomen, a drum, a wing, or one of the two pieces of a leg.</summary>
    public sealed class ScreamerPart : MonoBehaviour, IHittable
    {
        [System.NonSerialized] public Screamer Owner;
        [System.NonSerialized] public ScreamerPiece Piece;
        /// <summary>Which drum (0, 1), wing (0 to 3) or leg (0 to 5).</summary>
        [System.NonSerialized] public int Index;

        public HitOutcome Hit(in HitInfo hit) => Owner != null ? Owner.TakeHit(this, hit) : new HitOutcome { Hit = true };

        public string DescribeForDebug()
        {
            if (Owner == null) return "Creature piece";
            ScreamerBody body = Owner.Body;
            switch (Piece)
            {
                case ScreamerPiece.Body: return "Screamer fore body " + Mathf.CeilToInt((float)body.Body);
                case ScreamerPiece.Abdomen: return "Screamer abdomen " + Mathf.CeilToInt((float)body.Abdomen);
                case ScreamerPiece.Drum: return "Screamer drum " + Mathf.CeilToInt((float)body.DrumHp(Index));
                case ScreamerPiece.Wing: return "Screamer wing " + Mathf.CeilToInt((float)body.WingHp(Index));
                default: return "Screamer leg " + Mathf.CeilToInt((float)body.LegHp(Index));
            }
        }
    }

    /// <summary>
    /// The Screamer in the world: an alien from the cicada (Matthew, 17 Nov 2026). The rules are plain C#
    /// (<see cref="ScreamerBody"/>, <see cref="ScreamerAnimator"/>, <see cref="ScreamerBrain"/>); this turns them into
    /// objects.
    ///
    /// - Its fore body, abdomen, two drums, four wings and six legs are each an object and a hitbox. Wings, legs and
    ///   drums come off as loose pieces (<see cref="WalkerChunk"/>, shared with the other creatures).
    /// - It is not a physics object while alive. Its root stays on the ground under it, even in the air: how high it
    ///   is carried is the pose's business. "Up" is its own transform's up, never +Y.
    /// - Its scream hurts whoever the rules say is in front of it and in range, unless something solid is between
    ///   them. The rings that race out of it show where that is.
    /// </summary>
    public sealed class Screamer : MonoBehaviour, IWindup, ICreature
    {
        /// <summary>Who pays for it, given by whatever woke it; null in the practice range.</summary>
        public CreatureCredit Credit { get; set; }
        /// <summary>Its kind, its model's name, for the aim line (<see cref="WhereToShoot"/>).</summary>
        public string Kind => "Screamer";
        /// <summary>On land it can always come to the player, so a kill always pays.</summary>
        public bool InReach => true;
        public const string WarnSound = Windups.ScreamerWarning, AimSound = Windups.ScreamerAim, ScreamSound = "creature.screamer.scream";

        public ScreamerBody Body { get; private set; }
        public ScreamerBrain Brain { get; private set; }
        /// <summary>Where its strike comes from, for the visor (UI/WindupCues): its body, in the air or on the ground.</summary>
        public Vector3 WindupPoint => Chest();
        public WindupStage Windup => isActiveAndEnabled && Alive ? _windup.Stage : WindupStage.None;
        public float WindupProgress => _windup.Progress;
        /// <summary>While it screams: its last beat reached the player. Out of its cone or behind cover, it no longer threatens.</summary>
        public bool Threatens => _threatens;
        readonly WindupTrack _windup = new WindupTrack();
        bool _threatens = true;
        public bool Alive => Body != null && Body.Alive;
        public event System.Action<Screamer> Died;

        static readonly List<Screamer> Everyone = new List<Screamer>();
        public static IReadOnlyList<Screamer> All => Everyone;
        static int _screaming;

        public static void ResetStatics()
        {
            Everyone.Clear();
            _screaming = 0;
        }

        ScreamerRules _rules;
        readonly ScreamerAnimator _animator = new ScreamerAnimator();
        readonly ScreamerPose _pose = new ScreamerPose();
        Transform _body, _abdomen;
        readonly Transform[] _drum = new Transform[ScreamerLayout.Drums];
        readonly Transform[] _wing = new Transform[ScreamerLayout.Wings];
        readonly Transform[] _leg = new Transform[ScreamerLayout.Legs], _shin = new Transform[ScreamerLayout.Legs];
        /// <summary>Where it started and a direction along the ground, both in its parent's space, so they hold on a world that turns.</summary>
        Vector3 _home, _north;
        bool _blocked, _sees, _loud, _unsettled;
        Vector3 _playerWas;
        bool _playerKnown;
        float _nextLook;
        ScreamerMode _lastMode = ScreamerMode.Perch;
        Material _material;
        ScreamRings _rings;

        /// <summary>Makes a Screamer standing at a place. Null if its model is not in the game.</summary>
        public static Screamer Create(Vector3 position, Quaternion rotation, Transform parent, CreatureConfig config, int seed, string name = null)
        {
            if (ModelLibrary.Find(ScreamerLayout.Model) == null)
            {
                Debug.LogWarning("Orbital Ante: the Screamer's model (Resources/Models/" + ScreamerLayout.Model + ") is missing, so none was made.");
                return null;
            }
            var go = new GameObject(name ?? ScreamerLayout.Model);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var screamer = go.AddComponent<Screamer>();
            screamer.Build(config != null ? config.screamer : new ScreamerRules(), seed);
            return screamer;
        }

        void Build(ScreamerRules rules, int seed)
        {
            _rules = rules;
            Body = new ScreamerBody(rules.hp);
            Brain = new ScreamerBrain(rules, seed);
            Transform parent = transform.parent;
            _home = parent != null ? parent.InverseTransformPoint(transform.position) : transform.position;
            _north = parent != null ? parent.InverseTransformDirection(transform.forward) : transform.forward;
            // Not every creature looks for the player on the same frame.
            _nextLook = Time.time + (seed & 0xff) / 1024f;
            _material = ModelLibrary.MaterialFor(ScreamerLayout.Model);

            // Its hitboxes move with it every frame: a kinematic body tells the physics so.
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            _body = Mesh(ScreamerLayout.BodyPiece);
            var box = _body.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0f, (float)(ScreamerLayout.BodyFront + ScreamerLayout.BodyBack) * 0.5f);
            box.size = new Vector3((float)ScreamerLayout.BodyHalfWidth * 2f, (float)ScreamerLayout.BodyHalfHeight * 2f, (float)(ScreamerLayout.BodyFront - ScreamerLayout.BodyBack));
            Tag(_body.gameObject, ScreamerPiece.Body, 0);

            _abdomen = Mesh(ScreamerLayout.AbdomenPiece);
            var tube = _abdomen.gameObject.AddComponent<CapsuleCollider>();
            tube.direction = 2;
            tube.radius = (float)ScreamerLayout.AbdomenHalfWidth * 0.86f;
            tube.height = (float)ScreamerLayout.AbdomenLength;
            tube.center = new Vector3(0f, 0f, -(float)ScreamerLayout.AbdomenLength * 0.5f);
            Tag(_abdomen.gameObject, ScreamerPiece.Abdomen, 0);

            for (int side = 0; side < ScreamerLayout.Drums; side++)
            {
                _drum[side] = Mesh(ScreamerLayout.DrumPiece);
                _drum[side].name = "Drum " + side;
                var ball = _drum[side].gameObject.AddComponent<SphereCollider>();
                // A little larger than it is drawn, and standing out from the abdomen, so it can be picked off on purpose.
                ball.radius = (float)ScreamerLayout.DrumRadius * 1.1f;
                ball.center = new Vector3(0.04f, 0f, 0f);
                Tag(_drum[side].gameObject, ScreamerPiece.Drum, side);
            }
            for (int w = 0; w < ScreamerLayout.Wings; w++)
            {
                bool hind = ScreamerLayout.IsHind(w);
                _wing[w] = Mesh(hind ? ScreamerLayout.HindWingPiece : ScreamerLayout.ForeWingPiece);
                _wing[w].name = (hind ? "Hind wing " : "Fore wing ") + w;
                float length = (float)ScreamerLayout.WingLength(w), chord = (float)(hind ? ScreamerLayout.HindWingChord : ScreamerLayout.ForeWingChord);
                var plate = _wing[w].gameObject.AddComponent<BoxCollider>();
                plate.center = new Vector3(chord * 0.42f, 0f, length * 0.52f);
                plate.size = new Vector3(chord * 0.8f, 0.05f, length * 0.9f);
                Tag(_wing[w].gameObject, ScreamerPiece.Wing, w);
            }
            for (int leg = 0; leg < ScreamerLayout.Legs; leg++)
            {
                _leg[leg] = Limb(ScreamerLayout.LegPiece, "Leg " + leg, (float)ScreamerLayout.LegLength, leg);
                _shin[leg] = Limb(ScreamerLayout.ShinPiece, "Shin " + leg, (float)ScreamerLayout.ShinLength, leg);
            }
            _rings = ScreamRings.Make(transform);
            ModelDetail.Attach(gameObject, ScreamerLayout.Model, DetailRule.BodyRange, true);
            Everyone.Add(this);
            Pose(0.0001f, new ScreamerOrders(), Vector3.zero);
        }

        Transform Limb(string piece, string name, float length, int leg)
        {
            Transform t = Mesh(piece);
            t.name = name;
            var capsule = t.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = 2;
            // Thicker than the leg is drawn, so a leg can be picked off on purpose.
            capsule.radius = 0.06f;
            capsule.height = length + 0.06f;
            capsule.center = new Vector3(0f, 0f, length * 0.5f);
            Tag(t.gameObject, ScreamerPiece.Leg, leg);
            return t;
        }

        Transform Mesh(string piece)
        {
            var go = new GameObject(piece);
            go.layer = Layers.Hitbox;
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = ModelLibrary.PartMesh(ScreamerLayout.Model, piece);
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

        void Tag(GameObject go, ScreamerPiece piece, int index)
        {
            var part = go.AddComponent<ScreamerPart>();
            part.Owner = this;
            part.Piece = piece;
            part.Index = index;
        }

        void OnDestroy()
        {
            Quieten();
            Everyone.Remove(this);
        }

        static Vector3 V(Vec3d v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        static Quaternion Q(QuatD q) => new Quaternion((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);
        static Vec3d D(Vector3 v) => new Vec3d(v.x, v.y, v.z);

        static bool InAir(ScreamerMode mode) => mode == ScreamerMode.Fly || mode == ScreamerMode.Land || mode == ScreamerMode.Fall;

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            if (!Alive || GameSession.Paused) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;

            Transform parent = transform.parent;
            Vector3 home = parent != null ? parent.TransformPoint(_home) : _home, north = parent != null ? parent.TransformDirection(_north) : _north;
            var senses = new ScreamerSenses
            {
                Blocked = _blocked,
                Home = D(transform.InverseTransformPoint(home)),
                North = D(transform.InverseTransformDirection(north)),
            };
            Transform eye = CombatWorld.PlayerPresent ? CombatWorld.PlayerEye : null;
            Vector3 up = transform.up;
            bool hostile = eye != null && !CombatWorld.EnemiesHoldFire;
            Vector3 feet = hostile ? eye.position - up * 1.6f : Vector3.zero;
            if (hostile)
            {
                senses.Player = D(transform.InverseTransformPoint(feet));
                // How the player is moving, so it can land where they are going.
                if (_playerKnown) senses.PlayerVelocity = D(transform.InverseTransformDirection((feet - _playerWas) / dt));
                _playerWas = feet;
                _playerKnown = true;
                if (Time.time >= _nextLook)
                {
                    // Four looks a second: near enough, and nothing solid between it and the player.
                    _nextLook = Time.time + 0.25f;
                    Vector3 from = Chest();
                    _sees = Vector3.Distance(from, eye.position) < _rules.sightRange * CombatWorld.PlayerSeenFrom
                            && !Physics.Linecast(from, eye.position, Layers.WorldMask, QueryTriggerInteraction.Ignore);
                }
                senses.SeesPlayer = _sees;
            }
            else
            {
                _sees = false;
                _playerKnown = false;
            }

            ScreamerOrders orders = Brain.Think(Body, senses, dt);
            if (orders.Mode != _lastMode)
            {
                if (orders.Mode == ScreamerMode.Scream)
                {
                    Loud(true);
                    _threatens = true;
                }
                else if (_lastMode == ScreamerMode.Scream) Loud(false);
                _lastMode = orders.Mode;
            }
            // Its warning is heard and seen (UI/WindupCues) until the scream is over: its own wind-up as it lifts its
            // abdomen, and its mark (both jerking to full height) the very frame it takes aim.
            WindupTell.Tell(this, _windup.Step(Windups.Screamer(orders)), WarnSound, AimSound, Chest());
            if (orders.Crashed)
            {
                Audio.AudioDirector.Play("impact.dirt", transform.position, 1f, 0.7f);
                _animator.Flinch(new Vec3d(0, -3.0, 0));
            }
            Vector3 moved = Move(orders, dt);
            // In the air, and after landing until it is down, its root is eased onto the ground below it.
            if (InAir(orders.Mode) || _unsettled) _unsettled = Settle((float)orders.Height, dt);
            Pose(dt, orders, moved / dt);
            // While the player can see it, its hitboxes must be where it is drawn when their shot is traced.
            if (_sees) Physics.SyncTransforms();
            if (_rings != null) _rings.Run(orders.Mode == ScreamerMode.Scream, orders.Pulse, RingsFrom(), transform.forward, up, (float)ScreamerBrain.Range(_rules, Body), _rules.coneHalfAngle, dt);
            if (orders.Pulse && hostile) Hurt(eye, feet);
        }

        Vector3 Chest() => _body != null ? _body.position : transform.position + transform.up * 0.5f;
        Vector3 RingsFrom() => Chest() + transform.forward * (float)ScreamerLayout.BodyFront;

        /// <summary>The scream is one sound however many are screaming; it follows the one that began last.</summary>
        void Loud(bool on)
        {
            if (on == _loud) return;
            _loud = on;
            _screaming = Mathf.Max(0, _screaming + (on ? 1 : -1));
            Audio.AudioDirector audio = Audio.AudioDirector.Instance;
            if (audio == null) return;
            if (on)
            {
                audio.SetLoop(ScreamSound, 1f, 0.05f, _body != null ? _body : transform);
                return;
            }
            if (_screaming == 0)
            {
                audio.SetLoop(ScreamSound, 0f, 0.12f);
                return;
            }
            // Another is still screaming: the sound goes to it.
            for (int i = 0; i < Everyone.Count; i++)
            {
                Screamer other = Everyone[i];
                if (other == null || other == this || !other._loud) continue;
                audio.SetLoop(ScreamSound, 1f, 0.05f, other._body != null ? other._body : other.transform);
                break;
            }
        }

        void Quieten() => Loud(false);

        /// <summary>One beat of the scream: whoever is in the cone, in range and in plain sight of it is hurt.</summary>
        void Hurt(Transform eye, Vector3 feet)
        {
            // Measured from its mouth to the player's chest, so the cone that hurts is the round one the rings draw.
            Vector3 mouth = RingsFrom(), chest = eye.position - transform.up * 0.4f;
            double rate = ScreamerBrain.DamageRate(_rules, Body, D(transform.InverseTransformDirection(chest - mouth)));
            // Whether this beat reaches them is what the visor shows: out of the cone or behind cover, the arc dims.
            _threatens = false;
            if (rate <= 0.0) return;
            // (From inside its body, so a wall its mouth has been pushed into still stops the scream.)
            Vector3 from = Chest(), head = eye.position;
            // Anything solid between them stops it: cover is the answer.
            if (Physics.Linecast(from, head, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return;
            _threatens = true;
            PlayerHealth player = CombatWorld.Player;
            if (player != null) player.TakeDamage((float)(rate * _rules.pulseSeconds), (head - from).normalized);
        }

        /// <summary>Turns it and moves it along the ground; the answer is how far it really went, in its own frame after the turn.</summary>
        Vector3 Move(in ScreamerOrders orders, float dt)
        {
            Vector3 up = transform.up;
            if (orders.TurnRate != 0.0) transform.rotation = Quaternion.AngleAxis((float)orders.TurnRate * Mathf.Rad2Deg * dt, up) * transform.rotation;
            Vector3 local = V(orders.Velocity) * dt;
            _blocked = false;
            if (local.sqrMagnitude <= 0f) return Vector3.zero;
            Vector3 to = transform.position + transform.TransformDirection(local);
            bool air = InAir(orders.Mode);
            float height = (float)orders.Height;
            // Stopped by anything solid where its body is: at its height in the air, just off the ground on foot.
            float radius = air ? 0.55f : 0.36f;
            // (Never lower than a body length, so taking off is not blocked by the ground it leaves.)
            Vector3 step = to - transform.position, probe = up * (air ? Mathf.Max(height, 0.3f) + 0.5f : 0.5f);
            if (Physics.CheckSphere(to + probe, radius, Layers.WorldMask, QueryTriggerInteraction.Ignore)
                || Physics.SphereCast(transform.position + probe, radius, step.normalized, out _, step.magnitude, Layers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                _blocked = true;
                return Vector3.zero;
            }
            if (!air && Physics.Raycast(to + up * 3f, -up, out RaycastHit ground, 9f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                // On foot a step up or down of more than a shin is a wall to it for now (it is placed on level ground).
                if (Mathf.Abs(Vector3.Dot(ground.point - to, up)) > 0.3f)
                {
                    _blocked = true;
                    return Vector3.zero;
                }
                to = ground.point;
            }
            transform.position = to;
            return local;
        }

        /// <summary>In the air its root follows the ground below it, eased, so flying over a wall is not a jump.</summary>
        bool Settle(float height, float dt)
        {
            Vector3 up = transform.up, at = transform.position;
            if (!Physics.Raycast(at + up * (height + 3f), -up, out RaycastHit ground, height + 15f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return false;
            float rise = Vector3.Dot(ground.point - at, up), step = Mathf.Clamp(rise, -7f * dt, 7f * dt);
            transform.position = at + up * step;
            // The answer: is there still some way to go.
            return Mathf.Abs(rise - step) > 0.02f;
        }

        void Pose(float dt, in ScreamerOrders orders, Vector3 velocity)
        {
            var input = new ScreamerAnimInput { Velocity = D(velocity), TurnRate = orders.TurnRate, Height = orders.Height, Mode = orders.Mode, T = orders.T };
            _animator.Update(Body, input, dt, _pose);
            if (_body != null) _body.SetLocalPositionAndRotation(V(_pose.BodyPosition), Q(_pose.BodyRotation));
            if (_abdomen != null) _abdomen.SetLocalPositionAndRotation(V(_pose.AbdomenPosition), Q(_pose.AbdomenRotation));
            for (int i = 0; i < ScreamerLayout.Drums; i++)
                if (_drum[i] != null) _drum[i].SetLocalPositionAndRotation(V(_pose.DrumPosition[i]), Q(_pose.DrumRotation[i]));
            for (int i = 0; i < ScreamerLayout.Wings; i++)
                if (_wing[i] != null) _wing[i].SetLocalPositionAndRotation(V(_pose.WingPosition[i]), Q(_pose.WingRotation[i]));
            for (int i = 0; i < ScreamerLayout.Legs; i++)
            {
                if (_leg[i] != null) _leg[i].SetLocalPositionAndRotation(V(_pose.LegPosition[i]), Q(_pose.LegRotation[i]));
                if (_shin[i] != null) _shin[i].SetLocalPositionAndRotation(V(_pose.ShinPosition[i]), Q(_pose.ShinRotation[i]));
            }
        }

        // ------------------------------------------------------------------ being hit

        public HitOutcome TakeHit(ScreamerPart part, in HitInfo hit)
        {
            var outcome = new HitOutcome { Hit = true };
            if (!Alive) return outcome;
            outcome.Enemy = true;
            outcome.Target = this;
            ScreamerHit result;
            bool soft = false;
            switch (part.Piece)
            {
                case ScreamerPiece.Wing:
                    result = Body.HitWing(part.Index, hit.Damage, hit.Sever);
                    outcome.Label = "Screamer wing";
                    if (result.WingLost >= 0) LoseWing(result.WingLost, hit.Direction);
                    break;
                case ScreamerPiece.Leg:
                    result = Body.HitLeg(part.Index, hit.Damage, hit.Sever);
                    outcome.Label = "Screamer leg";
                    if (result.LegLost >= 0) LoseLeg(result.LegLost, hit.Direction);
                    break;
                case ScreamerPiece.Drum:
                    result = Body.HitDrum(part.Index, hit.Damage, hit.Sever);
                    outcome.Label = "Screamer drum";
                    if (result.DrumLost >= 0) LoseDrum(result.DrumLost, hit.Direction);
                    break;
                case ScreamerPiece.Abdomen:
                    result = Body.HitAbdomen(hit.Damage);
                    outcome.Label = "Screamer abdomen";
                    soft = true;
                    break;
                default:
                    result = Body.HitBody(hit.Damage);
                    outcome.Label = "Screamer fore body";
                    break;
            }
            outcome.Severed = result.Lost;
            bool killed = result.Killed;
            // Where it landed, for the hit marker: the abdomen rings gold, the fore body only knocks, and a drum shot off
            // takes its scream down a voice, like a disarm (WhereToShoot). Read before the one-hit-kill line below, which
            // starts the shared result afresh.
            outcome.Zone = WhereToShoot.Screamer(part.Piece, part.Piece == ScreamerPiece.Drum && result.DrumLost >= 0);
            // A one-hit-kill gun kills wherever it lands, after the struck piece has taken its hit.
            if (hit.Lethal && !killed) killed = Body.HitBody(0, lethal: true).Killed;
            bool armour = outcome.Zone == HitZone.Armour;
            Brain.Alert();
            Vector3 push = transform.InverseTransformDirection(hit.Direction);
            _animator.Flinch(new Vec3d(push.x, 0.3, push.z) * (outcome.Severed ? 1.5 : 0.5));
            GoreFx gore = GoreFx.Instance;
            // The fore body's armour says it is armour: no blood, a dull knock, until it breaks. Everything else bleeds.
            if (armour && !killed) Audio.AudioDirector.Play("impact.armor", hit.Point, 0.7f, 1.0f);
            else if (gore != null) gore.Burst(hit.Point, -hit.Direction, outcome.Severed ? 12 : soft ? 9 : 4, outcome.Severed ? 5f : soft ? 4.5f : 3f);
            if (killed)
            {
                outcome.Killed = true;
                Burst(hit);
            }
            // Hitstop and shake only when something came off or it died, as for people (Part B: on a limb popping): a hit
            // that takes nothing off is 0, or every shot on a creature froze the game for 50 ms (pass 7).
            outcome.Juice = outcome.Killed ? 1f : outcome.Severed ? (part.Piece == ScreamerPiece.Drum ? 0.8f : 0.5f) : 0f;
            return outcome;
        }

        Transform LooseParent => CombatWorld.DropParent != null ? CombatWorld.DropParent : transform.parent;

        void LoseWing(int index, Vector3 direction, bool loud = true)
        {
            Transform wing = _wing[index];
            if (wing == null) return;
            _wing[index] = null;
            float s = (float)ScreamerLayout.Sign(ScreamerLayout.WingSide(index));
            Vector3 up = transform.up, outward = _body != null ? _body.TransformDirection(new Vector3(s, 0.4f, 0f).normalized) : up;
            if (_body != null) Wound(_body, V(ScreamerLayout.HingeOf(index)), Quaternion.LookRotation(new Vector3(s, 0.5f, 0f), Vector3.up), 0.05f);
            // A wing is light and broad: it is thrown less hard than a leg and tumbles.
            WalkerChunk chunk = Throw(wing, "Screamer wing", 1.2f, direction * 2f + outward * Random.Range(2f, 4f) + up * Random.Range(1.5f, 3f));
            // (Not at its death: a dozen wounds at once is a dozen objects nobody looks at.)
            if (loud) Wound(wing, Vector3.zero, Quaternion.Euler(0f, 180f, 0f), 0.05f);
            if (loud) Pop(chunk.transform.position, outward, 8, "gore.pop.small");
        }

        void LoseLeg(int index, Vector3 direction, bool loud = true)
        {
            Transform leg = _leg[index], shin = _shin[index];
            _leg[index] = _shin[index] = null;
            if (leg == null) return;
            float s = (float)ScreamerLayout.Sign(ScreamerLayout.LegSide(index));
            Vector3 up = transform.up, outward = _body != null ? _body.TransformDirection(new Vector3(s, 0.1f, 0f).normalized) : up;
            if (_body != null) Wound(_body, V(ScreamerLayout.HipOf(index)), Quaternion.LookRotation(new Vector3(s, -0.4f, 0f), Vector3.up), (float)ScreamerLayout.LegRadius * 1.4f);
            WalkerChunk chunk = Throw(leg, "Screamer leg", 1.5f, direction * 3f + outward * Random.Range(3f, 5f) + up * Random.Range(2f, 4f));
            // The shin goes with it, still bent as it was.
            if (shin != null) Loosen(shin, chunk.transform);
            if (loud) Wound(leg, Vector3.zero, Quaternion.Euler(0f, 180f, 0f), (float)ScreamerLayout.LegRadius * 1.4f);
            if (loud) Pop(chunk.transform.position, outward, 8, "gore.pop.small");
        }

        void LoseDrum(int index, Vector3 direction)
        {
            Transform drum = _drum[index];
            if (drum == null) return;
            _drum[index] = null;
            float s = (float)ScreamerLayout.Sign(index);
            Vector3 up = transform.up, outward = _abdomen != null ? _abdomen.TransformDirection(new Vector3(s, 0f, 0f)) : up;
            if (_abdomen != null) Wound(_abdomen, V(ScreamerLayout.DrumOf(index)) - new Vector3(s * 0.05f, 0f, 0f), Quaternion.LookRotation(new Vector3(s, 0f, 0f), Vector3.up), (float)ScreamerLayout.DrumRadius * 0.8f);
            WalkerChunk chunk = Throw(drum, "Screamer drum", 1.5f, direction * 3f + outward * Random.Range(4f, 6f) + up * Random.Range(2f, 4f));
            Pop(chunk.transform.position, outward, 14, "gore.pop.medium");
            // Shot off while it warns or screams, the scream stops there: seen and felt as a harder knock.
            if (Brain.Choke())
            {
                Loud(false);
                _animator.Flinch(new Vec3d(-s * 1.5, 1.0, -1.0));
            }
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
            var part = piece.GetComponent<ScreamerPart>();
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
            Mesh mesh = ModelLibrary.PartMesh(ScreamerLayout.Model, ScreamerLayout.Stump);
            if (mesh == null) return;
            var go = new GameObject("Wound");
            go.layer = on.gameObject.layer;
            go.transform.SetParent(on, false);
            go.transform.SetLocalPositionAndRotation(at, facing);
            go.transform.localScale = Vector3.one * (size / (float)ScreamerLayout.StumpRadius);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Quiet(go.AddComponent<MeshRenderer>());
        }

        /// <summary>It dies: the fore body and the abdomen part, everything still on it is thrown off, and what was inside comes out.</summary>
        void Burst(in HitInfo hit)
        {
            Loud(false);
            if (_rings != null) _rings.Stop();
            Vector3 up = transform.up, middle = _abdomen != null ? _abdomen.position : Chest();
            for (int i = 0; i < ScreamerLayout.Wings; i++) if (_wing[i] != null) LoseWing(i, hit.Direction, false);
            for (int i = 0; i < ScreamerLayout.Legs; i++) if (_leg[i] != null) LoseLeg(i, hit.Direction, false);
            if (_abdomen != null)
            {
                WalkerChunk rear = Throw(_abdomen, "Screamer abdomen", 8f, hit.Direction * 2f - transform.forward * Random.Range(2f, 4f) + up * Random.Range(3f, 6f));
                Wound(_abdomen, Vector3.zero, Quaternion.identity, (float)ScreamerLayout.AbdomenHalfWidth * 0.7f);
                // Drums still on it go with it.
                for (int i = 0; i < ScreamerLayout.Drums; i++)
                {
                    if (_drum[i] != null) Loosen(_drum[i], rear.transform, false);
                    _drum[i] = null;
                }
                _abdomen = null;
            }
            if (_body != null)
            {
                Throw(_body, "Screamer fore body", 10f, hit.Direction * 2f + transform.forward * Random.Range(1f, 3f) + up * Random.Range(3f, 5f));
                Wound(_body, V(ScreamerLayout.AbdomenJoint), Quaternion.Euler(0f, 180f, 0f), (float)ScreamerLayout.AbdomenHalfWidth * 0.7f);
                _body = null;
            }
            for (int k = 0; k < ScreamerLayout.Meat.Length; k++)
            {
                Mesh mesh = ModelLibrary.PartMesh(ScreamerLayout.Model, ScreamerLayout.Meat[k]);
                if (mesh == null) continue;
                WalkerChunk chunk = WalkerChunk.Make("Screamer meat", middle + Random.insideUnitSphere * 0.15f, Random.rotation, LooseParent, 3f, _rules.pieceLife, _rules.maxLoosePieces);
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
            Audio.AudioDirector.Play("gore.pop.torso", middle, 1f, 1.0f);
            ModelDetail.LetGo(gameObject);
            Died?.Invoke(this);
            Everyone.Remove(this);
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// The scream made visible: thin pale rings that race out from the Screamer along the cone it hurts in, growing
    /// as they go and gone at the end of its range. Where the rings pass is where it hurts (unless something
    /// solid is in the way), so the edge of the cone can be seen and run out of. A handful of objects, drawn only
    /// while it screams; solid colour, so nothing see-through is drawn over the view.
    /// </summary>
    public sealed class ScreamRings : MonoBehaviour
    {
        const int Count = 5;
        const float Speed = 26f;

        static Mesh _mesh;
        static Material _material;

        readonly Transform[] _ring = new Transform[Count];
        readonly float[] _far = new float[Count];
        bool _on;
        /// <summary>The rings start at the far end and race in to where they come from (something drawing water in), instead of out.</summary>
        [System.NonSerialized] public bool Inward;

        public static void ResetStatics()
        {
            _mesh = null;
            _material = null;
        }

        public static ScreamRings Make(Transform owner)
        {
            var go = new GameObject("Scream rings");
            go.transform.SetParent(owner, false);
            var rings = go.AddComponent<ScreamRings>();
            for (int i = 0; i < Count; i++)
            {
                var ring = new GameObject("Ring " + i);
                ring.transform.SetParent(go.transform, false);
                ring.AddComponent<MeshFilter>().sharedMesh = RingMesh();
                var renderer = ring.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = RingMaterial();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                ring.SetActive(false);
                rings._ring[i] = ring.transform;
                rings._far[i] = -1f;
            }
            return rings;
        }

        /// <summary>A thin flat ring of radius 1 facing +Z, seen from both sides.</summary>
        static Mesh RingMesh()
        {
            if (_mesh != null) return _mesh;
            const int steps = 40;
            const float inner = 0.94f;
            var points = new Vector3[steps * 2];
            var triangles = new int[steps * 12];
            for (int i = 0; i < steps; i++)
            {
                float a = i * Mathf.PI * 2f / steps;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                points[i * 2] = dir;
                points[i * 2 + 1] = dir * inner;
                int j = (i + 1) % steps, t = i * 12;
                triangles[t] = i * 2; triangles[t + 1] = j * 2; triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = j * 2; triangles[t + 4] = j * 2 + 1; triangles[t + 5] = i * 2 + 1;
                triangles[t + 6] = i * 2; triangles[t + 7] = i * 2 + 1; triangles[t + 8] = j * 2;
                triangles[t + 9] = j * 2; triangles[t + 10] = i * 2 + 1; triangles[t + 11] = j * 2 + 1;
            }
            _mesh = new Mesh { name = "ScreamRing", vertices = points, triangles = triangles };
            _mesh.RecalculateBounds();
            return _mesh;
        }

        static Material RingMaterial()
        {
            if (_material == null) _material = new Material(Mat.Unlit(new Color(0.86f, 0.92f, 0.96f))) { name = "ScreamRing" };
            return _material;
        }

        public void Stop()
        {
            _on = false;
            for (int i = 0; i < Count; i++)
            {
                _far[i] = -1f;
                if (_ring[i] != null) _ring[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Called every frame by its Screamer: starts a ring on each beat of the scream, and moves the ones on their way.</summary>
        public void Run(bool screaming, bool beat, Vector3 from, Vector3 forward, Vector3 up, float range, float halfAngle, float dt)
        {
            if (!screaming && !_on) return;
            bool any = false;
            for (int i = 0; i < Count; i++)
            {
                if (_far[i] < 0f)
                {
                    // A new ring sets off on each beat of the scream: the ring is the hurt.
                    if (!beat) continue;
                    beat = false;
                    _far[i] = 0.4f;
                    _ring[i].gameObject.SetActive(true);
                }
                _far[i] += Speed * dt;
                if (_far[i] >= range)
                {
                    _far[i] = -1f;
                    _ring[i].gameObject.SetActive(false);
                    continue;
                }
                any = true;
                float at = Inward ? Mathf.Max(0.4f, range - _far[i]) : _far[i];
                float radius = Mathf.Max(0.5f, at * Mathf.Tan(halfAngle));
                _ring[i].SetPositionAndRotation(from + forward * at, Quaternion.LookRotation(forward, up));
                _ring[i].localScale = new Vector3(radius, radius, 1f);
            }
            _on = any || screaming;
        }
    }
}
