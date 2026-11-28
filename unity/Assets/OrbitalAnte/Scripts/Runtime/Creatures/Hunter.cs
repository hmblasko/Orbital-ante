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
    /// <summary>One hitbox of a Cave hunter: its head, a segment or a leg.</summary>
    public sealed class HunterPart : MonoBehaviour, IHittable
    {
        [System.NonSerialized] public Hunter Owner;
        [System.NonSerialized] public HunterPiece Piece;
        /// <summary>Which segment (0 to 15) or which leg (0 to 31).</summary>
        [System.NonSerialized] public int Index;

        public HitOutcome Hit(in HitInfo hit) => Owner != null ? Owner.TakeHit(this, hit) : new HitOutcome { Hit = true };

        public string DescribeForDebug()
        {
            if (Owner == null) return "Creature piece";
            HunterBody body = Owner.Body;
            switch (Piece)
            {
                case HunterPiece.Head: return "Hunter head " + Mathf.CeilToInt((float)body.Head);
                case HunterPiece.Seg: return (Index < HunterBody.ForeSegs ? "Hunter fore body " : "Hunter segment ") + Mathf.CeilToInt((float)body.SegHp(Index));
                default: return "Hunter leg " + Mathf.CeilToInt((float)body.LegHp(Index));
            }
        }
    }

    /// <summary>
    /// The Cave hunter in the world: an alien from the giant centipede (Matthew, 17 Nov 2026). The rules are plain C#
    /// (<see cref="HunterBody"/>, <see cref="HunterAnimator"/>, <see cref="HunterBrain"/>); this turns them into
    /// objects.
    ///
    /// - Its head, sixteen segments and thirty-two legs are each an object and a hitbox. A segment that gives way cuts
    ///   it in two: everything from there back comes away as loose pieces (<see cref="WalkerChunk"/>, shared with the
    ///   other creatures) and the head end fights on, shorter.
    /// - It is not a physics object while alive: its brain moves its head over the ground and the body follows. "Up"
    ///   is its own transform's up, never +Y.
    /// - It dies only when its head or its fore body gives way; then it comes apart at every joint.
    /// </summary>
    public sealed class Hunter : MonoBehaviour, IWindup, ICreature
    {
        /// <summary>Who pays for it, given by whatever woke it; null in the practice range.</summary>
        public CreatureCredit Credit { get; set; }
        /// <summary>Its kind, its model's name, for the aim line (<see cref="WhereToShoot"/>).</summary>
        public string Kind => "Hunter";
        /// <summary>On land it can always come to the player, so a kill always pays.</summary>
        public bool InReach => true;
        public HunterBody Body { get; private set; }
        public HunterBrain Brain { get; private set; }
        /// <summary>Where its strike comes from, for the visor (UI/WindupCues): its head.</summary>
        public Vector3 WindupPoint => HeadPoint();
        public WindupStage Windup => isActiveAndEnabled && Alive ? _windup.Stage : WindupStage.None;
        public float WindupProgress => _windup.Progress;
        /// <summary>The whip of its head is over too soon to step out of part-way, so it always threatens.</summary>
        public bool Threatens => true;
        readonly WindupTrack _windup = new WindupTrack();
        public bool Alive => Body != null && Body.Alive;
        public event System.Action<Hunter> Died;

        static readonly List<Hunter> Everyone = new List<Hunter>();
        public static IReadOnlyList<Hunter> All => Everyone;
        public static void ResetStatics() => Everyone.Clear();

        HunterRules _rules;
        readonly HunterAnimator _animator = new HunterAnimator();
        readonly HunterPose _pose = new HunterPose();
        readonly Transform[] _node = new Transform[HunterLayout.Nodes];
        readonly Transform[] _leg = new Transform[HunterLayout.Legs];
        readonly Transform[] _fang = new Transform[2], _feeler = new Transform[2], _tail = new Transform[2];
        Vector3 _home;
        bool _blocked, _sees;
        float _nextLook;
        Material _material;

        /// <summary>Makes a Cave hunter with its head at a place. Null if its model is not in the game.</summary>
        public static Hunter Create(Vector3 position, Quaternion rotation, Transform parent, CreatureConfig config, int seed, string name = null)
        {
            if (ModelLibrary.Find(HunterLayout.Model) == null)
            {
                Debug.LogWarning("Orbital Ante: the Cave hunter's model (Resources/Models/" + HunterLayout.Model + ") is missing, so none was made.");
                return null;
            }
            var go = new GameObject(name ?? HunterLayout.Model);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var hunter = go.AddComponent<Hunter>();
            hunter.Build(config != null ? config.hunter : new HunterRules(), seed);
            return hunter;
        }

        void Build(HunterRules rules, int seed)
        {
            _rules = rules;
            Body = new HunterBody(rules.hp);
            Brain = new HunterBrain(rules, seed);
            _home = transform.position;
            // Not every creature looks for the player on the same frame.
            _nextLook = Time.time + (seed & 0xff) / 1024f;
            _material = ModelLibrary.MaterialFor(HunterLayout.Model);

            // Its hitboxes move with it every frame: a kinematic body tells the physics so.
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            for (int i = 0; i < HunterLayout.Nodes; i++)
            {
                bool head = i == 0;
                // The node itself is never scaled, so wounds and colliders on it keep their shape; its mesh is.
                var node = new GameObject(head ? "Head" : "Segment " + (i - 1));
                node.layer = Layers.Hitbox;
                node.transform.SetParent(transform, false);
                _node[i] = node.transform;
                Transform mesh = Mesh(head ? HunterLayout.HeadPiece : HunterLayout.SegPiece, node.transform);
                var box = node.AddComponent<BoxCollider>();
                if (head)
                {
                    box.center = new Vector3(0f, 0f, ((float)HunterLayout.HeadLength - (float)HunterLayout.Spacing) * 0.5f);
                    box.size = new Vector3((float)HunterLayout.HeadHalfWidth * 2f, 0.20f, (float)HunterLayout.HeadLength);
                    Tag(node, HunterPiece.Head, 0);
                }
                else
                {
                    float w = (float)HunterLayout.Scale(i - 1);
                    mesh.localScale = new Vector3(w, w, 1f);
                    box.center = new Vector3(0f, ((float)HunterLayout.SegTop - (float)HunterLayout.SegBottom) * 0.5f * w, 0f);
                    box.size = new Vector3((float)HunterLayout.SegHalfWidth * 2f * w, ((float)HunterLayout.SegTop + (float)HunterLayout.SegBottom) * w, (float)HunterLayout.Spacing);
                    Tag(node, HunterPiece.Seg, i - 1);
                }
            }
            for (int leg = 0; leg < HunterLayout.Legs; leg++)
            {
                // A leg is the same either side (its upper side is turned by the pose), so nothing is mirrored.
                Transform piece = Mesh(HunterLayout.LegPiece, transform);
                piece.name = "Leg " + leg;
                var capsule = piece.gameObject.AddComponent<CapsuleCollider>();
                capsule.direction = 2;
                // Thicker than the leg is drawn, so a leg can be picked off on purpose.
                capsule.radius = 0.06f;
                capsule.height = (float)HunterLayout.LegLength + 0.06f;
                capsule.center = new Vector3(0f, 0.04f, (float)HunterLayout.LegLength * 0.5f);
                Tag(piece.gameObject, HunterPiece.Leg, leg);
                _leg[leg] = piece;
            }
            for (int side = 0; side < 2; side++)
            {
                // Fangs, feelers and the trailing legs are only drawn: hits on them land on the head or pass.
                _fang[side] = Mesh(HunterLayout.FangPiece, transform, false);
                _feeler[side] = Mesh(HunterLayout.FeelerPiece, transform, false);
                _tail[side] = Mesh(HunterLayout.TailPiece, transform, false);
            }
            ModelDetail.Attach(gameObject, HunterLayout.Model, DetailRule.BodyRange * 1.1f, true);
            Everyone.Add(this);
            Pose(0.0001f, new HunterOrders(), Vector3.zero);
        }

        Transform Mesh(string piece, Transform parent, bool hitbox = true)
        {
            var go = new GameObject(piece);
            go.layer = hitbox ? Layers.Hitbox : Layers.Gib;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = ModelLibrary.PartMesh(HunterLayout.Model, piece);
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

        void Tag(GameObject go, HunterPiece piece, int index)
        {
            var part = go.AddComponent<HunterPart>();
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
            // A long frame is taken as a short one, so a lunge never steps past thin cover or past the player.
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;

            var senses = new HunterSenses { Blocked = _blocked, Home = D(transform.InverseTransformPoint(_home)) };
            Transform eye = CombatWorld.PlayerPresent ? CombatWorld.PlayerEye : null;
            Vector3 up = transform.up;
            bool hostile = eye != null && !CombatWorld.EnemiesHoldFire;
            if (hostile)
            {
                senses.Player = D(transform.InverseTransformPoint(eye.position - up * 1.6f));
                if (Time.time >= _nextLook)
                {
                    // Four looks a second: near enough, and nothing solid between its head and the player.
                    _nextLook = Time.time + 0.25f;
                    Vector3 from = HeadPoint() + up * 0.15f;
                    _sees = Vector3.Distance(from, eye.position) < _rules.sightRange * CombatWorld.PlayerSeenFrom
                            && !Physics.Linecast(from, eye.position, Layers.WorldMask, QueryTriggerInteraction.Ignore);
                }
                senses.SeesPlayer = _sees;
            }
            else
            {
                _sees = false;
            }

            HunterOrders orders = Brain.Think(Body, senses, dt);
            // Its warning is heard and seen (UI/WindupCues) until the strike is over: its own wind-up as it rears, and its
            // mark (the fangs flying wide) the very frame it takes aim.
            WindupTell.Tell(this, _windup.Step(Windups.Hunter(orders)), Windups.HunterWarning, Windups.HunterAim, HeadPoint());
            Vector3 moved = Move(orders, dt);
            Pose(dt, orders, moved / dt);
            // While the player can see it, its hitboxes must be where it is drawn when their shot is traced: thin legs
            // and a head that whips about are missed otherwise.
            if (_sees || orders.Mode == HunterMode.Strike || orders.Mode == HunterMode.Rear) Physics.SyncTransforms();
            if (orders.Landed && hostile && Brain.BiteHits(D(transform.InverseTransformPoint(eye.position - up * 1.6f)))) Bite(eye);
        }

        Vector3 HeadPoint() => _node[0] != null ? _node[0].position : transform.position;

        void Bite(Transform eye)
        {
            Vector3 from = HeadPoint() + transform.up * 0.2f, chest = eye.position - transform.up * 0.35f;
            // Never through a wall.
            if (Physics.Linecast(from, chest, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return;
            Audio.AudioDirector.Play("gore.splat", chest, 0.5f, 1.1f);
            PlayerHealth player = CombatWorld.Player;
            if (player != null) player.TakeDamage(_rules.biteDamage, (chest - from).normalized);
        }

        /// <summary>Moves the head; the answer is how far it really went, in its own frame after the turn.</summary>
        Vector3 Move(in HunterOrders orders, float dt)
        {
            Vector3 up = transform.up;
            if (orders.TurnRate != 0.0) transform.rotation = Quaternion.AngleAxis((float)orders.TurnRate * Mathf.Rad2Deg * dt, up) * transform.rotation;
            Vector3 local = V(orders.Velocity) * dt;
            _blocked = false;
            if (local.sqrMagnitude <= 0f) return Vector3.zero;
            Vector3 to = transform.position + transform.TransformDirection(local);
            // Stopped by anything in front of its head; the check is kept clear of the ground it is on.
            // The whole step is swept, so a long frame of a lunge never carries it through thin cover.
            Vector3 step = to - transform.position, probe = up * 0.42f + transform.forward * 0.30f;
            if (Physics.CheckSphere(to + probe, 0.26f, Layers.WorldMask, QueryTriggerInteraction.Ignore)
                || Physics.SphereCast(transform.position + probe, 0.26f, step.normalized, out _, step.magnitude, Layers.WorldMask, QueryTriggerInteraction.Ignore))
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
            transform.position = to;
            return local;
        }

        void Pose(float dt, in HunterOrders orders, Vector3 velocity)
        {
            var input = new HunterAnimInput { Velocity = D(velocity), TurnRate = orders.TurnRate, Mode = orders.Mode, T = orders.T };
            _animator.Update(Body, input, dt, _pose);
            for (int i = 0; i < _pose.Nodes; i++)
                if (_node[i] != null) _node[i].SetLocalPositionAndRotation(V(_pose.NodePosition[i]), Q(_pose.NodeRotation[i]));
            int legs = (_pose.Nodes - 1) * 2;
            for (int leg = 0; leg < legs; leg++)
                if (_leg[leg] != null) _leg[leg].SetLocalPositionAndRotation(V(_pose.LegPosition[leg]), Q(_pose.LegRotation[leg]));
            for (int side = 0; side < 2; side++)
            {
                if (_fang[side] != null) _fang[side].SetLocalPositionAndRotation(V(_pose.FangPosition[side]), Q(_pose.FangRotation[side]));
                if (_feeler[side] != null) _feeler[side].SetLocalPositionAndRotation(V(_pose.FeelerPosition[side]), Q(_pose.FeelerRotation[side]));
                if (_tail[side] != null && Body.HasTail) _tail[side].SetLocalPositionAndRotation(V(_pose.TailPosition[side]), Q(_pose.TailRotation[side]));
            }
        }

        // ------------------------------------------------------------------ being hit

        public HitOutcome TakeHit(HunterPart part, in HitInfo hit)
        {
            var outcome = new HitOutcome { Hit = true };
            if (!Alive) return outcome;
            outcome.Enemy = true;
            outcome.Target = this;
            HunterHit result;
            bool soft = false;
            switch (part.Piece)
            {
                case HunterPiece.Leg:
                    result = Body.HitLeg(part.Index, hit.Damage, hit.Sever);
                    outcome.Label = "Hunter leg";
                    if (result.LegLost >= 0) LoseLeg(result.LegLost, hit.Direction);
                    outcome.Severed = result.LegLost >= 0;
                    break;
                case HunterPiece.Seg:
                    result = Body.HitSeg(part.Index, hit.Damage, hit.Sever);
                    outcome.Label = part.Index < HunterBody.ForeSegs ? "Hunter fore body" : "Hunter segment";
                    // The fore body is where it is killed: a hit there bleeds more and counts for more.
                    soft = part.Index < HunterBody.ForeSegs;
                    if (result.FromSeg >= 0)
                    {
                        Cut(result.FromSeg, result.HadSegs, hit);
                        outcome.Severed = true;
                    }
                    break;
                default:
                    result = Body.HitHead(hit.Damage);
                    outcome.Label = "Hunter head";
                    break;
            }
            bool killed = result.Killed;
            // A one-hit-kill gun kills wherever it lands, after the struck piece has taken its hit.
            if (hit.Lethal && !killed) killed = Body.HitHead(0, lethal: true).Killed;
            // Where it landed, for the hit marker: the fore body rings gold, the head only knocks (WhereToShoot).
            outcome.Zone = WhereToShoot.Hunter(part.Piece, part.Index);
            bool armour = outcome.Zone == HitZone.Armour;
            Brain.Alert();
            Vector3 push = transform.InverseTransformDirection(hit.Direction);
            _animator.Flinch(new Vec3d(push.x, 0.4, push.z) * (outcome.Severed ? 1.5 : 0.4));
            GoreFx gore = GoreFx.Instance;
            // The head's armour says it is armour: no blood, a dull knock, until it breaks. Everything else bleeds.
            if (armour && !killed) Audio.AudioDirector.Play("impact.armor", hit.Point, 0.7f, 0.9f);
            else if (gore != null) gore.Burst(hit.Point, -hit.Direction, outcome.Severed ? 16 : soft ? 9 : 4, outcome.Severed ? 6f : soft ? 4.5f : 3f);
            if (killed)
            {
                outcome.Killed = true;
                Burst(hit);
            }
            outcome.Juice = outcome.Killed ? 1f : outcome.Severed ? (part.Piece == HunterPiece.Seg ? 0.8f : 0.5f) : armour ? 0.04f : soft ? 0.2f : 0.12f;
            return outcome;
        }

        Transform LooseParent => CombatWorld.DropParent != null ? CombatWorld.DropParent : transform.parent;

        void LoseLeg(int index, Vector3 direction)
        {
            Transform leg = _leg[index];
            if (leg == null) return;
            _leg[index] = null;
            Transform seg = _node[HunterLayout.SegOf(index) + 1];
            float s = (float)HunterLayout.Sign(index);
            Vector3 up = transform.up, outward = seg != null ? seg.TransformDirection(new Vector3(s, 0.2f, 0f).normalized) : up;
            // The wound on the body, looking out along where the leg was, and on the leg, looking back.
            if (seg != null) Wound(seg, V(HunterLayout.HipOf(index)), Quaternion.LookRotation(new Vector3(s, -0.3f, 0f), Vector3.up), (float)HunterLayout.LegRadius * 1.4f);
            WalkerChunk chunk = Throw(leg, "Hunter leg", 1.5f, direction * 3f + outward * Random.Range(3f, 5f) + up * Random.Range(2f, 4f));
            Wound(leg, Vector3.zero, Quaternion.Euler(0f, 180f, 0f), (float)HunterLayout.LegRadius * 1.4f);
            Pop(chunk.transform.position, outward, 8, "gore.pop.small");
        }

        /// <summary>
        /// Cut in two: every segment from <paramref name="from"/> back leaves as a loose piece with its legs still on,
        /// thrown apart a little so the length comes to bits as it lands. What is left thrashes.
        /// </summary>
        void Cut(int from, int had, in HitInfo hit)
        {
            Vector3 up = transform.up;
            Transform kept = _node[from];
            float cutWide = (float)(HunterLayout.SegHalfWidth * HunterLayout.Scale(from)) * 0.62f;
            if (kept != null) Wound(kept, new Vector3(0f, 0f, -(float)HunterLayout.Spacing * 0.5f), Quaternion.Euler(0f, 180f, 0f), cutWide);
            for (int seg = from; seg < had; seg++)
            {
                Transform node = _node[seg + 1];
                if (node == null) continue;
                _node[seg + 1] = null;
                Vector3 side = node.right * Random.Range(-2.5f, 2.5f);
                WalkerChunk chunk = Throw(node, "Hunter segment", 5f, hit.Direction * 2f + side + up * Random.Range(2.5f, 5.5f) - node.forward * Random.Range(0.5f, 2.5f));
                if (seg == from) Wound(node, new Vector3(0f, 0f, (float)HunterLayout.Spacing * 0.5f), Quaternion.identity, cutWide);
                Carry(seg, chunk.transform, seg == HunterLayout.Segs - 1);
            }
            Vector3 at = kept != null ? kept.position : transform.position;
            Pop(at, hit.Direction, 30, "gore.pop.torso");
            // The thrash that stops it (not twice running) is seen and felt: a harder knock.
            if (Brain.Stagger()) _animator.Flinch(new Vec3d(Random.Range(-1f, 1f), 1.2, -1.5));
        }

        /// <summary>A loose segment takes its legs with it (and the last one the two trailing legs): only drawn, not solid.</summary>
        void Carry(int seg, Transform chunk, bool tail)
        {
            for (int side = 0; side < 2; side++)
            {
                int index = HunterLayout.Leg(seg, side);
                if (_leg[index] != null) Loosen(_leg[index], chunk, false);
                _leg[index] = null;
                if (!tail || _tail[side] == null) continue;
                Loosen(_tail[side], chunk, false);
                _tail[side] = null;
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
            var part = piece.GetComponent<HunterPart>();
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
            Mesh mesh = ModelLibrary.PartMesh(HunterLayout.Model, HunterLayout.Stump);
            if (mesh == null) return;
            var go = new GameObject("Wound");
            go.layer = on.gameObject.layer;
            go.transform.SetParent(on, false);
            go.transform.SetLocalPositionAndRotation(at, facing);
            go.transform.localScale = Vector3.one * (size / (float)HunterLayout.StumpRadius);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Quiet(go.AddComponent<MeshRenderer>());
        }

        /// <summary>It dies: it comes apart at every joint, each segment with its legs still on, and what was inside comes out.</summary>
        void Burst(in HitInfo hit)
        {
            Vector3 up = transform.up;
            int nodes = Body.Kept + 1;
            Vector3 middle = _node[Mathf.Min(nodes - 1, 2)] != null ? _node[Mathf.Min(nodes - 1, 2)].position : transform.position;
            for (int i = 0; i < nodes; i++)
            {
                Transform node = _node[i];
                if (node == null) continue;
                _node[i] = null;
                Vector3 away = node.position - middle;
                away = away.sqrMagnitude > 0.0001f ? away.normalized : up;
                Vector3 velocity = away * Random.Range(2f, 5f) + node.right * Random.Range(-3f, 3f) + up * Random.Range(3f, 7f) + hit.Direction * 2f;
                WalkerChunk chunk = Throw(node, i == 0 ? "Hunter head" : "Hunter segment", i == 0 ? 6f : 5f, velocity);
                if (i == 0)
                {
                    for (int side = 0; side < 2; side++)
                    {
                        if (_fang[side] != null) Loosen(_fang[side], chunk.transform, false);
                        if (_feeler[side] != null) Loosen(_feeler[side], chunk.transform, false);
                        _fang[side] = _feeler[side] = null;
                    }
                }
                else
                {
                    Carry(i - 1, chunk.transform, i - 1 == HunterLayout.Segs - 1);
                }
            }
            for (int k = 0; k < HunterLayout.Meat.Length; k++)
            {
                Mesh mesh = ModelLibrary.PartMesh(HunterLayout.Model, HunterLayout.Meat[k]);
                if (mesh == null) continue;
                WalkerChunk chunk = WalkerChunk.Make("Hunter meat", middle + Random.insideUnitSphere * 0.15f, Random.rotation, LooseParent, 3f, _rules.pieceLife, _rules.maxLoosePieces);
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
