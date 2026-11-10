using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Dismemberment
{
    /// <summary>
    /// Moves and animates a mannequin procedurally. How it moves depends only on which parts are left:
    /// walk/sprint, one-legged hop, arm crawl, torso wriggle, or a rolling head.
    ///
    /// For Milestone 1 the "AI" is just wandering between random points so the movement states can be judged.
    /// </summary>
    public sealed class MannequinLocomotion : MonoBehaviour
    {
        Mannequin _owner;
        MannequinRig _rig;
        MannequinConfig _cfg;
        CapsuleCollider _blocker;

        System.Random _rng = new System.Random(1);
        Bounds _wanderArea = new Bounds(Vector3.zero, new Vector3(20f, 0f, 20f));
        Vector3 _target;
        float _repathTimer;
        bool _sprinting;

        float _phase;
        float _headRoll;
        MovementState _appliedState = (MovementState)(-1);
        Vector3 _lastPosition;

        /// <summary>Set false to keep the character on the spot (used by tests and for target practice).</summary>
        public bool Wander = true;

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
            ApplyStandingPose(0f, false);
        }

        public void SetWander(Bounds area, int seed)
        {
            _wanderArea = area;
            _rng = new System.Random(seed);
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
                Steer(state, dt);
                speed = SpeedFor(state, _sprinting);
            }
            CurrentSpeed = speed;

            // Gait: how fast the cycle runs, and how the forward motion pulses within one cycle.
            float cadence = CadenceFor(state, _sprinting);
            _phase += dt * cadence * Mathf.PI * 2f;
            if (_phase > Mathf.PI * 2000f) _phase -= Mathf.PI * 2000f;

            float pulse = PulseFor(state);
            Vector3 step = transform.forward * (speed * pulse * dt);
            Vector3 position = transform.position + step;

            // Stay on the ground.
            if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit ground, 6f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                position.y = ground.point.y;
            }
            transform.position = position;

            _owner.Velocity = (position - _lastPosition) / dt;
            _lastPosition = position;

            ApplyPose(state, speed > 0.01f, dt, speed * pulse);
        }

        // ------------------------------------------------------------------ steering

        void Steer(MovementState state, float dt)
        {
            _repathTimer -= dt;
            Vector3 to = _target - transform.position;
            to.y = 0f;

            float probeHeight = (state == MovementState.Intact || state == MovementState.OneLeg) ? 0.9f : 0.2f;
            bool blocked = Physics.Raycast(transform.position + Vector3.up * probeHeight, transform.forward, 0.9f,
                Layers.WorldMask, QueryTriggerInteraction.Ignore);

            if (to.sqrMagnitude < 0.5f * 0.5f || _repathTimer <= 0f || blocked)
            {
                PickTarget();
                to = _target - transform.position;
                to.y = 0f;
            }

            if (to.sqrMagnitude > 0.0001f)
            {
                Quaternion want = Quaternion.LookRotation(to.normalized, Vector3.up);
                float turn = _cfg.turnSpeed * TurnScaleFor(state);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turn * dt);
            }
        }

        void PickTarget()
        {
            Vector3 min = _wanderArea.min;
            Vector3 max = _wanderArea.max;
            float x = Mathf.Lerp(min.x, max.x, (float)_rng.NextDouble());
            float z = Mathf.Lerp(min.z, max.z, (float)_rng.NextDouble());
            _target = new Vector3(x, transform.position.y, z);
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

        void ApplyPose(MovementState state, bool moving, float dt, float instantSpeed)
        {
            _rig.ResetBones();
            // The head part can be rolled by the head-only state, so always put it back first.
            BodyPart head = _owner.Part(BodyPartId.Head);
            if (head != null && _owner.Model.OnBody(BodyPartId.Head)) head.transform.localRotation = Quaternion.identity;

            switch (state)
            {
                case MovementState.Intact:
                    ApplyStandingPose(moving ? 1f : 0f, _sprinting);
                    break;
                case MovementState.OneLeg:
                    ApplyOneLegPose(moving);
                    break;
                case MovementState.Crawl:
                    ApplyCrawlPose(moving);
                    break;
                case MovementState.Wriggle:
                    ApplyWrigglePose(moving);
                    break;
                case MovementState.HeadOnly:
                    ApplyHeadOnlyPose(head, dt, instantSpeed);
                    break;
            }
        }

        // Arm rotation conventions (rest pose is a T-pose facing +Z):
        //  - Rotating an upper arm about Z lowers it: +angle for the left arm, -angle for the right.
        //  - Rotating about X swings a lowered arm or a leg: negative = forward.
        //  - A forearm bends forward about Y: +angle for the left arm, -angle for the right.
        static Quaternion ArmDown(int side, float downDegrees, float swingDegrees)
        {
            float z = side == MannequinRig.Left ? downDegrees : -downDegrees;
            return Quaternion.Euler(swingDegrees, 0f, 0f) * Quaternion.Euler(0f, 0f, z);
        }

        static Quaternion ElbowBend(int side, float degrees)
        {
            return Quaternion.Euler(0f, side == MannequinRig.Left ? degrees : -degrees, 0f);
        }

        void ApplyStandingPose(float moveAmount, bool sprinting)
        {
            float s = Mathf.Sin(_phase);
            float c = Mathf.Cos(_phase);
            float legSwing = (sprinting ? 44f : 28f) * moveAmount;
            float knee = (sprinting ? 65f : 38f) * moveAmount;
            float armSwing = (sprinting ? 48f : 22f) * moveAmount;
            float bob = (sprinting ? 0.05f : 0.025f) * moveAmount * Mathf.Abs(c);
            float lean = sprinting ? 12f : 3f * moveAmount;

            _rig.Pose.localPosition = new Vector3(0f, bob, 0f);
            _rig.Pose.localRotation = Quaternion.Euler(lean, 0f, 0f);

            // Left leg swings forward while the right swings back, and the swinging leg bends its knee.
            _rig.Thigh[MannequinRig.Left].localRotation = Quaternion.Euler(-legSwing * s, 0f, 0f);
            _rig.Thigh[MannequinRig.Right].localRotation = Quaternion.Euler(legSwing * s, 0f, 0f);
            _rig.Shin[MannequinRig.Left].localRotation = Quaternion.Euler(knee * Mathf.Max(0f, c), 0f, 0f);
            _rig.Shin[MannequinRig.Right].localRotation = Quaternion.Euler(knee * Mathf.Max(0f, -c), 0f, 0f);

            // Arms swing opposite to the legs.
            float elbow = sprinting ? 70f : 18f;
            _rig.UpperArm[MannequinRig.Left].localRotation = ArmDown(MannequinRig.Left, 78f, armSwing * s);
            _rig.UpperArm[MannequinRig.Right].localRotation = ArmDown(MannequinRig.Right, 78f, -armSwing * s);
            _rig.Forearm[MannequinRig.Left].localRotation = ElbowBend(MannequinRig.Left, elbow);
            _rig.Forearm[MannequinRig.Right].localRotation = ElbowBend(MannequinRig.Right, elbow);
        }

        void ApplyOneLegPose(bool moving)
        {
            int standing = _owner.Model.LegWorks(true) ? MannequinRig.Left : MannequinRig.Right;
            int missing = 1 - standing;
            float air = moving ? Mathf.Max(0f, Mathf.Sin(_phase)) : 0f;       // 0 on the ground, 1 at the top of the hop
            float crouch = moving ? Mathf.Max(0f, -Mathf.Sin(_phase)) : 0f;   // winding up for the next hop

            // Lean over the good leg to stay balanced, and dip while winding up.
            float roll = standing == MannequinRig.Left ? 9f : -9f;
            _rig.Pose.localPosition = new Vector3(0f, air * 0.20f - crouch * 0.07f, 0f);
            _rig.Pose.localRotation = Quaternion.Euler(6f + 6f * air, 0f, roll);

            _rig.Thigh[standing].localRotation = Quaternion.Euler(-22f * crouch - 8f * air, 0f, 0f);
            _rig.Shin[standing].localRotation = Quaternion.Euler(40f * crouch + 12f * air, 0f, 0f);
            // Whatever is left of the other leg flails.
            _rig.Thigh[missing].localRotation = Quaternion.Euler(-25f + 30f * Mathf.Sin(_phase + 1f), 0f, 0f);
            _rig.Shin[missing].localRotation = Quaternion.Euler(30f, 0f, 0f);

            // Arms out and flapping for balance.
            float down = 48f - 26f * air + 10f * crouch;
            _rig.UpperArm[MannequinRig.Left].localRotation = ArmDown(MannequinRig.Left, down, -10f);
            _rig.UpperArm[MannequinRig.Right].localRotation = ArmDown(MannequinRig.Right, down, -10f);
            _rig.Forearm[MannequinRig.Left].localRotation = ElbowBend(MannequinRig.Left, 25f);
            _rig.Forearm[MannequinRig.Right].localRotation = ElbowBend(MannequinRig.Right, 25f);
        }

        /// <summary>Lays the body face down along its forward direction, centred on the character root.</summary>
        void SetProne(float yawDegrees, float rollDegrees, float pitchBumpDegrees)
        {
            Quaternion yaw = Quaternion.Euler(0f, yawDegrees, 0f);
            _rig.Pose.localRotation = yaw * Quaternion.Euler(86f - pitchBumpDegrees, 0f, rollDegrees);
            // After tipping over, a point at height h on the standing body ends up roughly h metres forward.
            float lift = _rig.TorsoHalfDepth * 0.5f + 0.02f;
            _rig.Pose.localPosition = yaw * new Vector3(0f, lift, -_rig.TorsoCentreHeight);
        }

        void ApplyCrawlPose(bool moving)
        {
            float s = moving ? Mathf.Sin(_phase) : 0f;
            SetProne(5f * s, 7f * s, 0f);

            // Arms reach past the head and pull back alternately.
            // Raising an arm above the shoulder is a negative "down" angle.
            float leftReach = -48f - 30f * s;
            float rightReach = -48f + 30f * s;
            _rig.UpperArm[MannequinRig.Left].localRotation = ArmDown(MannequinRig.Left, leftReach, 12f);
            _rig.UpperArm[MannequinRig.Right].localRotation = ArmDown(MannequinRig.Right, rightReach, 12f);
            _rig.Forearm[MannequinRig.Left].localRotation = ElbowBend(MannequinRig.Left, 28f + 22f * s);
            _rig.Forearm[MannequinRig.Right].localRotation = ElbowBend(MannequinRig.Right, 28f - 22f * s);

            // Leg stumps drag and twitch behind.
            _rig.Thigh[MannequinRig.Left].localRotation = Quaternion.Euler(8f * s, 0f, 0f);
            _rig.Thigh[MannequinRig.Right].localRotation = Quaternion.Euler(-8f * s, 0f, 0f);
            // Head up, looking where it is going.
            _rig.Head.localRotation = Quaternion.Euler(-35f, 0f, 0f);
        }

        void ApplyWrigglePose(bool moving)
        {
            float s = moving ? Mathf.Sin(_phase) : 0f;
            float bump = moving ? Mathf.Max(0f, Mathf.Sin(_phase + 0.8f)) : 0f;
            // Snake from side to side and hump up just before each lunge.
            SetProne(14f * s, 10f * s, 9f * bump);
            _rig.Spine.localRotation = Quaternion.Euler(0f, 0f, 6f * s);
            _rig.Thigh[MannequinRig.Left].localRotation = Quaternion.Euler(10f * s, 0f, 0f);
            _rig.Thigh[MannequinRig.Right].localRotation = Quaternion.Euler(-10f * s, 0f, 0f);
            _rig.Head.localRotation = Quaternion.Euler(-30f, -12f * s, 0f);
        }

        void ApplyHeadOnlyPose(BodyPart head, float dt, float instantSpeed)
        {
            // Drop the (now empty) skeleton so the head sits on the ground, then roll and hop it along.
            float hop = 0.07f * Mathf.Abs(Mathf.Sin(_phase));
            _rig.Pose.localRotation = Quaternion.identity;
            _rig.Pose.localPosition = new Vector3(0f, -_rig.HeadCentreHeight + _rig.HeadRadius + hop, 0f);

            _headRoll += Mathf.Rad2Deg * (instantSpeed * dt / Mathf.Max(0.01f, _rig.HeadRadius));
            if (_headRoll > 360f) _headRoll -= 360f;
            if (head != null) head.transform.localRotation = Quaternion.Euler(_headRoll, 0f, 0f);
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
