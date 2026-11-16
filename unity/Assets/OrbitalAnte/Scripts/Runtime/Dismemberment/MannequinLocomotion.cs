using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Dismemberment
{
    /// <summary>
    /// Moves and animates a mannequin procedurally. How it moves depends only on which parts are left:
    /// walk/sprint, one-legged hop, arm crawl, torso wriggle, or a rolling head.
    ///
    /// Left alone it wanders between random points. The combat brain (MannequinCombat) can instead give it
    /// somewhere to go (Destination) or something to stand and face (FacePoint).
    /// </summary>
    public sealed class MannequinLocomotion : MonoBehaviour
    {
        Mannequin _owner;
        MannequinRig _rig;
        MannequinConfig _cfg;
        CapsuleCollider _blocker;

        SeededRandom _rng = new SeededRandom(1);
        Bounds _wanderArea = new Bounds(Vector3.zero, new Vector3(20f, 0f, 20f));
        Vector3 _target;
        float _repathTimer;
        bool _sprinting;

        float _phase;
        float _headRoll;
        Vector3 _lastForward = Vector3.forward;
        BodyAnimator _anim = new BodyAnimator(1);
        AI.MannequinCombat _combat;
        MovementState _appliedState = (MovementState)(-1);
        Vector3 _lastPosition;

        /// <summary>Set false to keep the character on the spot (used by tests and for target practice).</summary>
        public bool Wander = true;

        /// <summary>When set, walk here instead of wandering, and stop on arrival.</summary>
        public Vector3? Destination;
        /// <summary>When set, stand still and turn to face this point (used to aim). Wins over Destination.</summary>
        public Vector3? FacePoint;
        /// <summary>Sprint to the destination if the legs allow it.</summary>
        public bool Hurry;
        /// <summary>
        /// Scrambling after something (a dropped gun): bent low, sprinting even over a few metres, quick short
        /// steps, and faster, wilder hops and crawls for a body that has lost its legs.
        /// </summary>
        public bool Scramble;
        /// <summary>0 to 1: bend down to take something off the ground with the hand on <see cref="ReachSide"/>.</summary>
        public float Reach;
        public int ReachSide = MannequinRig.Right;
        /// <summary>Something to keep the head turned toward, if anything (a point in the world).</summary>
        public Vector3? LookAt;
        /// <summary>Stay on the spot (startled, or bending to pick something up), turning slowly toward <see cref="LookAt"/>.</summary>
        public bool Hold;
        /// <summary>0 to 1: move and stand in the tactical hunch (knees bent, low, gun up, head level). Set by the fight.</summary>
        public float Tactical;
        /// <summary>How far into the hunch the body really is (it eases in and out), for posing the gun.</summary>
        public float TacticalShown => _anim != null ? (float)_anim.Tactical : 0f;

        // The hobble is not a metronome: each hop has its own length, and now and then it lands badly and
        // has to catch its balance. It changes where the body is, so it draws from its own seeded stream.
        SeededRandom _hobble = new SeededRandom(2);
        float _hopRate = 1f;
        int _hopIndex;
        float _teeterUntil;

        float _avoidUntil;
        bool _teetering;
        float _avoidSign = 1f;

        /// <summary>Current forward speed in metres per second, averaged over the gait.</summary>
        public float CurrentSpeed { get; private set; }

        public void Initialize(Mannequin owner, CapsuleCollider blocker)
        {
            _owner = owner;
            _rig = owner.Rig;
            _cfg = owner.Config;
            _blocker = blocker;
            _lastPosition = transform.position;
            _target = transform.position;
            _lastForward = transform.forward;
            _owner.Struck += OnStruck;
            _owner.PartDetached += OnPartDetached;
            // Stand properly from the first frame rather than in the skeleton's T-pose.
            ApplyPose(_owner.State, false, 1f / 60f, 0f);
        }

        void Start()
        {
            _combat = GetComponent<AI.MannequinCombat>();
        }

        void OnDestroy()
        {
            if (_owner != null)
            {
                _owner.Struck -= OnStruck;
                _owner.PartDetached -= OnPartDetached;
            }
        }

        /// <summary>How thick this kind's helmet is beside the ordinary one, so a lone head rests on the ground and not in it.</summary>
        float HeadFit() => _cfg != null && !string.IsNullOrEmpty(_cfg.modelId) ? BodyFit.Thickness(_cfg.modelId, BodyPartId.Head) : 1f;

        public void SetWander(Bounds area, int seed)
        {
            _wanderArea = area;
            _rng = new SeededRandom(seed);
            // How this one carries itself (its stance, its arm swing, when it glances about) differs per character.
            // Each kind carries itself its own way, and a body drawn larger takes longer strides.
            string model = _cfg != null ? _cfg.modelId : null;
            _anim = new BodyAnimator(seed, string.IsNullOrEmpty(model) ? null : BodyStyle.For(model)) { Size = transform.lossyScale.y };
            _phase = (float)_rng.NextDouble() * Mathf.PI * 2f;
            _hobble = new SeededRandom(seed * 31 + 7);
            PickTarget();
        }

        /// <summary>The leg still on the body of one that is down on the ground (BodySkeleton side), or -1 for none.</summary>
        int CrawlLeg()
        {
            if (_owner == null || _owner.Model == null) return -1;
            if (_owner.Model.LegWorks(true)) return MannequinRig.Left;
            if (_owner.Model.LegWorks(false)) return MannequinRig.Right;
            return -1;
        }

        /// <summary>Top speed for a movement state.</summary>
        public float SpeedFor(MovementState state, bool sprinting)
        {
            switch (state)
            {
                case MovementState.Intact: return sprinting ? _cfg.sprintSpeed : _cfg.walkSpeed;
                case MovementState.OneLeg: return _cfg.oneLegSpeed;
                case MovementState.Crawl: return CrawlLeg() >= 0 ? Mathf.Max(_cfg.crawlSpeed, _cfg.oneLegCrawlSpeed) : _cfg.crawlSpeed;
                case MovementState.Wriggle: return _cfg.wriggleSpeed;
                case MovementState.HeadOnly: return _cfg.headRollSpeed;
                default: return 0f;
            }
        }

        void Update()
        {
            if (_owner == null || _owner.Model == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            MovementState state = _owner.State;
            if (state == MovementState.Dead)
            {
                _owner.Velocity = Vector3.zero;
                enabled = false;
                return;
            }

            if (state != _appliedState)
            {
                _appliedState = state;
                ResizeBlocker(state);
                if (state != MovementState.Intact) _sprinting = false;
            }

            float speed = 0f;
            if (Wander)
            {
                if (Hold)
                {
                    _sprinting = false;
                    if (LookAt.HasValue) TurnToward(LookAt.Value - transform.position, state, dt * 0.6f);
                }
                else if (FacePoint.HasValue)
                {
                    _sprinting = false;
                    TurnToward(FacePoint.Value - transform.position, state, dt);
                }
                else if (Destination.HasValue)
                {
                    speed = SteerTo(Destination.Value, state, dt);
                }
                else
                {
                    Steer(state, dt);
                    speed = SpeedFor(state, _sprinting);
                }
            }

            // Gait: how fast the cycle runs, and how the forward motion pulses within one cycle.
            float cadence = CadenceFor(state, _sprinting);
            bool teetering = false;
            if (state == MovementState.OneLeg && speed > 0.01f)
            {
                // Scrambling hops come faster and further; each hop has its own length; a bad landing holds it
                // on the spot, arms out, before the next.
                if (Scramble) { cadence *= 1.3f; speed *= 1.25f; }
                cadence *= _hopRate;
                int hop = Mathf.FloorToInt(_phase / (Mathf.PI * 2f));
                if (hop != _hopIndex)
                {
                    _hopIndex = hop;
                    _hopRate = 0.82f + 0.36f * (float)_hobble.NextDouble();
                    float wobbly = Scramble ? 0.16f : 0.10f;
                    if (_hobble.NextDouble() < wobbly) _teeterUntil = Time.time + 0.35f + 0.4f * (float)_hobble.NextDouble();
                    else if (_hobble.NextDouble() < (Scramble ? 0.07f : 0.03f)) _anim.Stumble(_hobble.NextDouble() < 0.5 ? -1.0 : 1.0);
                }
                teetering = Time.time < _teeterUntil;
                if (teetering) { cadence = 0f; speed = 0f; }
            }
            else if (state == MovementState.Crawl || state == MovementState.Wriggle)
            {
                if (Scramble) { cadence *= 1.35f; speed *= 1.3f; }
            }
            // A stumbling body hardly gets anywhere until it has its feet again.
            if (_anim.Stumbling) speed *= 0.3f;
            CurrentSpeed = speed;
            _teetering = teetering;
            _phase += dt * cadence * Mathf.PI * 2f;
            if (_phase > Mathf.PI * 2000f) _phase -= Mathf.PI * 2000f;

            float pulse = PulseFor(state);
            Vector3 step = transform.forward * (speed * pulse * dt);
            Vector3 position = transform.position + step + Slide(dt);

            // Stay on the ground, and upright on it (on a round world "up" changes as you walk).
            Footing footing = _owner.Footing;
            Vector3 up = footing.Up(position);
            if (footing.Ground(position, up, out Vector3 ground)) position = ground;
            transform.position = position;
            if (!footing.IsFlat)
            {
                Vector3 ahead = Vector3.ProjectOnPlane(transform.forward, up);
                if (ahead.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(ahead.normalized, up);
            }

            // If the whole world was moved under the player this frame, that was not this character running.
            Vector3 moved = position - _lastPosition;
            _owner.Velocity = moved.sqrMagnitude > 25f ? Vector3.zero : moved / dt;
            _lastPosition = position;

            ApplyPose(state, speed > 0.01f, dt, speed * pulse);
        }

        // ------------------------------------------------------------------ steering

        void Steer(MovementState state, float dt)
        {
            _repathTimer -= dt;
            Vector3 to = Level(_owner.Footing.Recall(_target) - transform.position);

            float probeHeight = (state == MovementState.Intact || state == MovementState.OneLeg) ? 0.9f : 0.2f;
            bool blocked = WallAhead(probeHeight, 0.9f);

            if (to.sqrMagnitude < 0.5f * 0.5f || _repathTimer <= 0f || blocked)
            {
                PickTarget();
                to = Level(_owner.Footing.Recall(_target) - transform.position);
            }

            if (to.sqrMagnitude > 0.0001f)
            {
                Quaternion want = Quaternion.LookRotation(to.normalized, _owner.Up);
                float turn = _cfg.turnSpeed * TurnScaleFor(state);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turn * dt);
            }
        }

        /// <summary>Heads for a point, stepping around walls. Returns the speed to move at (0 on arrival).</summary>
        float SteerTo(Vector3 destination, MovementState state, float dt)
        {
            Vector3 to = Level(destination - transform.position);
            if (to.sqrMagnitude < 0.45f * 0.45f)
            {
                _sprinting = false;
                return 0f;
            }

            float probeHeight = (state == MovementState.Intact || state == MovementState.OneLeg) ? 0.9f : 0.2f;
            bool blocked = WallAhead(probeHeight, 1.0f);
            if (blocked && Time.time >= _avoidUntil)
            {
                // Something in the way: veer to one side for a moment, then try again.
                _avoidSign = _rng.NextDouble() < 0.5 ? -1f : 1f;
                _avoidUntil = Time.time + 0.9f;
            }

            // In a cave the way there follows the passages, not a straight line through the rock.
            Vector3 via = Level(_owner.Footing.Toward(transform.position, destination) - transform.position);
            Vector3 heading = via.sqrMagnitude > 0.01f ? via.normalized : to.normalized;
            if (Time.time < _avoidUntil) heading = Quaternion.AngleAxis(80f * _avoidSign, _owner.Up) * heading;
            TurnToward(heading, state, dt);

            // Scrambling for a gun, it sprints even the last few metres.
            _sprinting = Hurry && _owner.Model.CanSprint && to.sqrMagnitude > (Scramble ? 1.2f * 1.2f : 4f * 4f);
            float speed = SpeedFor(state, _sprinting);
            // Do not march off sideways while still turning to face the way.
            if (Vector3.Angle(transform.forward, heading) > 70f) speed *= 0.25f;
            return speed;
        }

        /// <summary>A direction with its up-and-down part taken out, so it lies along the ground.</summary>
        Vector3 Level(Vector3 v)
        {
            Vector3 up = _owner.Up;
            return v - up * Vector3.Dot(v, up);
        }

        void TurnToward(Vector3 direction, MovementState state, float dt)
        {
            direction = Level(direction);
            if (direction.sqrMagnitude < 0.0001f) return;
            Quaternion want = Quaternion.LookRotation(direction.normalized, _owner.Up);
            float turn = _cfg.turnSpeed * TurnScaleFor(state);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turn * dt);
        }

        void PickTarget()
        {
            _target = _owner.Footing.Hold(_owner.Footing.WanderPoint(_rng, _wanderArea, transform.position));
            _repathTimer = 4f + (float)_rng.NextDouble() * 6f;
            // An intact character sometimes breaks into a sprint, so "can't sprint" is visible once a leg is gone.
            _sprinting = _owner != null && _owner.Model != null && _owner.Model.CanSprint && _rng.NextDouble() < 0.35;
        }

        static float TurnScaleFor(MovementState state)
        {
            switch (state)
            {
                case MovementState.Intact: return 1f;
                case MovementState.OneLeg: return 0.6f;
                case MovementState.Crawl: return 0.3f;
                case MovementState.Wriggle: return 0.15f;
                default: return 0.5f;
            }
        }

        // ------------------------------------------------------------------ gait

        float CadenceFor(MovementState state, bool sprinting)
        {
            switch (state)
            {
                case MovementState.Intact: return sprinting ? 2.6f : 1.5f; // stride cycles per second
                case MovementState.OneLeg: return 1.7f;                    // hops per second
                case MovementState.Crawl: return 0.9f;
                case MovementState.Wriggle: return 1.3f;
                case MovementState.HeadOnly: return 1.6f;
                default: return 0f;
            }
        }

        /// <summary>Multiplier on forward speed through the cycle. Averages 1, so the configured speeds stay true.</summary>
        float PulseFor(MovementState state)
        {
            float s = Mathf.Sin(_phase);
            switch (state)
            {
                case MovementState.OneLeg:
                    // Only travels while in the air.
                    return Mathf.Max(0f, s) * Mathf.PI;
                case MovementState.Crawl:
                {
                    bool left = _owner.Model.ArmCanCrawl(true);
                    bool right = _owner.Model.ArmCanCrawl(false);
                    if (left && right) return 1f + 0.75f * Mathf.Cos(_phase * 2f); // one pull per arm
                    return Mathf.Max(0f, left ? s : -s) * Mathf.PI;                // one arm: one pull per cycle
                }
                case MovementState.Wriggle:
                    return Mathf.Max(0f, s) * Mathf.PI;
                case MovementState.HeadOnly:
                    return 1f + 0.5f * s;
                default:
                    return 1f;
            }
        }

        // ------------------------------------------------------------------ poses

        /// <summary>
        /// The pose itself is worked out by <see cref="BodyAnimator"/> (plain rules, tested without Unity);
        /// this only tells it what the character is doing and copies the answer onto the bones.
        /// </summary>
        void ApplyPose(MovementState state, bool moving, float dt, float instantSpeed)
        {
            // The head part can be rolled by the head-only state, so always put it back first.
            BodyPart head = _owner.Part(BodyPartId.Head);
            if (head != null && _owner.Model.OnBody(BodyPartId.Head)) head.transform.localRotation = Quaternion.identity;

            // Capped, so being placed or spun round by something else does not whip the feet about.
            float turned = Vector3.SignedAngle(_lastForward, transform.forward, transform.up);
            float turnRate = dt > 0f ? Mathf.Clamp(turned / dt, -400f, 400f) : 0f;
            _lastForward = transform.forward;

            bool armed = _combat != null && _combat.Weapon != null;
            var input = new BodyAnimInput
            {
                State = state,
                ForwardSpeed = state == MovementState.Intact ? CurrentSpeed : instantSpeed,
                TurnRate = turnRate,
                Phase = _phase,
                Moving = moving,
                WalkSpeed = _cfg.walkSpeed,
                SprintSpeed = _cfg.sprintSpeed,
                StandingLeg = _owner.Model.LegWorks(true) ? MannequinRig.Left : MannequinRig.Right,
                LeftArmWorks = _owner.Model.ArmCanCrawl(true),
                RightArmWorks = _owner.Model.ArmCanCrawl(false),
                Aim = armed && FacePoint.HasValue ? 1.0 : 0.0,
                GunSide = armed ? _combat.GunSide : -1,
                ArmLossLeft = ArmLoss(true),
                ArmLossRight = ArmLoss(false),
                StumpLength = StumpLength(),
                Scramble = Scramble ? 1.0 : 0.0,
                Reach = Mathf.Clamp01(Reach),
                ReachSide = ReachSide,
                Teeter = _teetering ? 1.0 : 0.0,
                // Down on the ground with a leg left: it pushes with it (StandingLeg says which).
                PushingLeg = (state == MovementState.Crawl || state == MovementState.Wriggle) && CrawlLeg() >= 0,
                Tactical = Mathf.Clamp01(Tactical),
            };
            if (LookAt.HasValue)
            {
                // In the body's own space at the skeleton's size. Kinds are drawn wider or narrower than they are
                // tall, so only the height scale is taken out, or the angles would be skewed.
                Vector3 local = transform.InverseTransformDirection(LookAt.Value - transform.position) / Mathf.Max(0.01f, transform.lossyScale.y);
                input.HasLook = true;
                input.LookX = local.x;
                input.LookY = local.y;
                input.LookZ = local.z;
            }
            _anim.Update(dt, input);
            _rig.Apply(_anim.Pose);

            if (state == MovementState.HeadOnly) ApplyHeadOnlyPose(head, dt, instantSpeed);
        }

        /// <summary>Knocks the body about when a part still on it is hit. Only for show: nothing here changes the rules.</summary>
        void OnStruck(Mannequin mannequin, BodyPartId part, HitInfo hit, bool severed)
        {
            if (_anim == null) return;
            Vector3 direction = transform.InverseTransformDirection(hit.Direction);
            Vector3 point = transform.InverseTransformPoint(hit.Point);
            _anim.Flinch(direction.x, direction.z, point.y / 1.8f, Mathf.Clamp(point.x / 0.3f, -1f, 1f), severed ? 2.5f : 1f);
        }

        /// <summary>How much of an arm's weight is gone: nothing, a hand, a forearm (the upper arm left as a stump), or all of it.</summary>
        double ArmLoss(bool left)
        {
            BodyModel m = _owner.Model;
            if (!m.OnBody(left ? BodyPartId.LeftUpperArm : BodyPartId.RightUpperArm)) return 1.0;
            if (!m.OnBody(left ? BodyPartId.LeftForearm : BodyPartId.RightForearm)) return 0.5;
            if (!m.OnBody(left ? BodyPartId.LeftHand : BodyPartId.RightHand)) return 0.15;
            return 0.0;
        }

        /// <summary>How much of the lost leg is left on a one-legged body: 0 none, 0.45 the thigh, 1 all but the foot.</summary>
        double StumpLength()
        {
            BodyModel m = _owner.Model;
            bool left = !m.LegWorks(true);
            if (!m.OnBody(left ? BodyPartId.LeftThigh : BodyPartId.RightThigh)) return 0.0;
            if (!m.OnBody(left ? BodyPartId.LeftShin : BodyPartId.RightShin)) return 0.45;
            return 1.0;
        }

        /// <summary>A part came off the living body: it reels from the wound. Only for show.</summary>
        void OnPartDetached(Mannequin mannequin, Detachment detachment, Gib gib, HitInfo hit)
        {
            if (_anim == null || detachment.SourceGroup != 0) return;
            // The lying capsule is longer while a leg is left; losing that leg too keeps the state but not the length.
            if (_owner != null && _appliedState == _owner.State) ResizeBlocker(_appliedState);
            double severity;
            int side;
            switch (detachment.Root)
            {
                case BodyPartId.LeftUpperArm: case BodyPartId.LeftThigh: severity = 1.0; side = MannequinRig.Left; break;
                case BodyPartId.RightUpperArm: case BodyPartId.RightThigh: severity = 1.0; side = MannequinRig.Right; break;
                case BodyPartId.LeftForearm: case BodyPartId.LeftShin: severity = 0.85; side = MannequinRig.Left; break;
                case BodyPartId.RightForearm: case BodyPartId.RightShin: severity = 0.85; side = MannequinRig.Right; break;
                case BodyPartId.LeftHand: case BodyPartId.LeftFoot: severity = 0.6; side = MannequinRig.Left; break;
                case BodyPartId.RightHand: case BodyPartId.RightFoot: severity = 0.6; side = MannequinRig.Right; break;
                default: return;
            }
            BodyPartId root = detachment.Root;
            bool arm = root == BodyPartId.LeftUpperArm || root == BodyPartId.RightUpperArm || root == BodyPartId.LeftForearm || root == BodyPartId.RightForearm;
            _anim.Wounded(side, severity, arm);
        }

        /// <summary>Startled: something was just knocked out of its hands toward this point in the world.</summary>
        public void Startle(Vector3 toward)
        {
            if (_anim == null) return;
            Vector3 local = transform.InverseTransformDirection(toward - transform.position);
            _anim.Startle(local.x, local.z);
        }

        // ------------------------------------------------------------------ knocked down by a melee hit

        Vector3 _shove;
        float _shoveFriction = 5f;

        /// <summary>True while a shove is still sliding the body along.</summary>
        public bool Shoved => _shove.sqrMagnitude > 0.0025f;

        /// <summary>
        /// Knocked off its feet by a melee hit (the bat, the mailbox): it slides along the ground the way it was hit,
        /// slowing by <paramref name="friction"/> m/s each second, lurching and startled. A cheap slide over the
        /// ground with the animator's stumble on top, not a jointed ragdoll: the body keeps standing on its footing,
        /// so it can get up again by itself. Nothing here changes the rules (MannequinCombat stops it shooting).
        /// </summary>
        public void Shove(Vector3 velocity, float friction)
        {
            Vector3 up = _owner != null ? _owner.Up : transform.up;
            _shove = Vector3.ProjectOnPlane(velocity, up);
            _shoveFriction = Mathf.Max(0.5f, friction);
            if (_anim == null) return;
            Vector3 local = transform.InverseTransformDirection(_shove);
            _anim.Stumble(local.x < 0f ? -1.0 : 1.0);
            _anim.Startle(local.x, local.z);
        }

        /// <summary>How far the shove carries the body this frame. Stops dead at a wall rather than going through it.</summary>
        Vector3 Slide(float dt)
        {
            if (!Shoved) { _shove = Vector3.zero; return Vector3.zero; }
            Vector3 move = _shove * dt;
            Vector3 up = _owner != null ? _owner.Up : transform.up;
            if (Physics.Raycast(transform.position + up * 0.6f, _shove.normalized, move.magnitude + 0.4f,
                    Core.Layers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                _shove = Vector3.zero;
                return Vector3.zero;
            }
            float speed = Melee.MeleeRules.SlideAfter(_shove.magnitude, dt, _shoveFriction);
            _shove = _shove.normalized * speed;
            return move;
        }

        /// <summary>The gun's kick, passed through the shoulder into the body.</summary>
        public void Kick(float strength, int gunSide)
        {
            if (_anim == null) return;
            _anim.Flinch(0.0, -1.0, 0.8, gunSide == MannequinRig.Left ? -0.6 : 0.6, 0.10 * strength);
        }

        void ApplyHeadOnlyPose(BodyPart head, float dt, float instantSpeed)
        {
            // Drop the (now empty) skeleton so the head sits on the ground, then roll and hop it along.
            float hop = 0.07f * Mathf.Abs(Mathf.Sin(_phase));
            _rig.Pose.localRotation = Quaternion.identity;
            _rig.Pose.localPosition = new Vector3(0f, -_rig.HeadCentreHeight + _rig.HeadRadius + hop, 0f);
            _rig.ResetBones();

            _headRoll += Mathf.Rad2Deg * (instantSpeed * dt / Mathf.Max(0.01f, _rig.HeadRadius));
            if (_headRoll > 360f) _headRoll -= 360f;
            if (head == null) return;
            if (_rig.Modelled)
            {
                // The helmet is not a ball (neck ring, snout, crest), so it rocks and waddles along on its neck ring
                // instead of rolling right over and dipping into the floor.
                float roll = _headRoll * Mathf.Deg2Rad;
                head.transform.localRotation = Quaternion.Euler(22f * Mathf.Sin(roll), 0f, 9f * Mathf.Cos(roll));
                _rig.Pose.localPosition = new Vector3(0f, -_rig.HeadCentreHeight + 0.19f * HeadFit() + hop, 0f);
            }
            else
            {
                head.transform.localRotation = Quaternion.Euler(_headRoll, 0f, 0f);
            }
        }

        // ------------------------------------------------------------------ collision capsule

        void ResizeBlocker(MovementState state)
        {
            if (_blocker == null) return;
            switch (state)
            {
                case MovementState.Intact:
                case MovementState.OneLeg:
                    _blocker.direction = 1;
                    _blocker.radius = 0.28f;
                    _blocker.height = 1.8f;
                    _blocker.center = new Vector3(0f, 0.9f, 0f);
                    break;
                case MovementState.Crawl:
                case MovementState.Wriggle:
                    _blocker.direction = 2;
                    _blocker.radius = 0.2f;
                    // A leg that is left trails about 0.9 m behind the hips: the capsule reaches back over it.
                    bool leg = CrawlLeg() >= 0;
                    _blocker.height = leg ? 1.6f : 1.2f;
                    _blocker.center = new Vector3(0f, 0.2f, leg ? -0.2f : 0f);
                    break;
                case MovementState.HeadOnly:
                    _blocker.direction = 1;
                    _blocker.radius = _rig.HeadRadius;
                    _blocker.height = _rig.HeadRadius * 2f;
                    _blocker.center = new Vector3(0f, _rig.HeadRadius, 0f);
                    break;
                default:
                    _blocker.enabled = false;
                    break;
            }
        }

        /// <summary>Something upright in the way. A hillside is not a wall: ground you could walk up does not count.</summary>
        bool WallAhead(float height, float reach)
        {
            Vector3 up = transform.up;
            return Physics.Raycast(transform.position + up * height, transform.forward, out RaycastHit hit, reach,
                       Layers.WorldMask, QueryTriggerInteraction.Ignore)
                   && Vector3.Dot(hit.normal, up) <= 0.5f;
        }
    }
}
