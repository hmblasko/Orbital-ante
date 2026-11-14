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

        float _avoidUntil;
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
            // Stand properly from the first frame rather than in the skeleton's T-pose.
            ApplyPose(_owner.State, false, 1f / 60f, 0f);
        }

        void Start()
        {
            _combat = GetComponent<AI.MannequinCombat>();
        }

        void OnDestroy()
        {
            if (_owner != null) _owner.Struck -= OnStruck;
        }

        public void SetWander(Bounds area, int seed)
        {
            _wanderArea = area;
            _rng = new SeededRandom(seed);
            // How this one carries itself (its stance, its arm swing, when it glances about) differs per character.
            _anim = new BodyAnimator(seed);
            _phase = (float)_rng.NextDouble() * Mathf.PI * 2f;
            PickTarget();
        }

        /// <summary>Top speed for a movement state.</summary>
        public float SpeedFor(MovementState state, bool sprinting)
        {
            switch (state)
            {
                case MovementState.Intact: return sprinting ? _cfg.sprintSpeed : _cfg.walkSpeed;
                case MovementState.OneLeg: return _cfg.oneLegSpeed;
                case MovementState.Crawl: return _cfg.crawlSpeed;
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
                if (FacePoint.HasValue)
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
            CurrentSpeed = speed;

            // Gait: how fast the cycle runs, and how the forward motion pulses within one cycle.
            float cadence = CadenceFor(state, _sprinting);
            _phase += dt * cadence * Mathf.PI * 2f;
            if (_phase > Mathf.PI * 2000f) _phase -= Mathf.PI * 2000f;

            float pulse = PulseFor(state);
            Vector3 step = transform.forward * (speed * pulse * dt);
            Vector3 position = transform.position + step;

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
            bool blocked = Physics.Raycast(transform.position + transform.up * probeHeight, transform.forward, 0.9f,
                Layers.WorldMask, QueryTriggerInteraction.Ignore);

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
            bool blocked = Physics.Raycast(transform.position + transform.up * probeHeight, transform.forward, 1.0f,
                Layers.WorldMask, QueryTriggerInteraction.Ignore);
            if (blocked && Time.time >= _avoidUntil)
            {
                // Something in the way: veer to one side for a moment, then try again.
                _avoidSign = _rng.NextDouble() < 0.5 ? -1f : 1f;
                _avoidUntil = Time.time + 0.9f;
            }

            Vector3 heading = to.normalized;
            if (Time.time < _avoidUntil) heading = Quaternion.AngleAxis(80f * _avoidSign, _owner.Up) * heading;
            TurnToward(heading, state, dt);

            _sprinting = Hurry && _owner.Model.CanSprint && to.sqrMagnitude > 4f * 4f;
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
            };
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
                _rig.Pose.localPosition = new Vector3(0f, -_rig.HeadCentreHeight + 0.19f + hop, 0f);
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
                    _blocker.height = 1.2f;
                    _blocker.center = new Vector3(0f, 0.2f, 0f);
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
    }
}
