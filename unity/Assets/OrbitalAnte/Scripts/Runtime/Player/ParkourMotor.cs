using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Player
{
    /// <summary>
    /// Shared arena/suit traversal. Physics queries only propose bounded movement: the existing controller
    /// or dynamic rigidbody still performs it. Cabin furniture has no solid height model, so it is excluded.
    /// </summary>
    public sealed class ParkourMotor
    {
        const float SurfaceDot = 0.7f;
        // The plain state needs only a nonzero presence token: its budget is shared across every wall
        // until landing. Collider continuity is checked directly against _wall below.
        const int WallPresent = 1;
        const int ClearanceMask = Layers.WorldMask | (1 << Layers.CharacterBody);
        enum Clearance { All, Support, Other }
        readonly Collider[] _overlaps = new Collider[32];
        readonly RaycastHit[] _hits = new RaycastHit[32];
        public ParkourState State { get; } = new ParkourState();
        Collider _wall, _support;
        Rigidbody _supportBody;
        Vector3 _liftLocal, _topLocal, _climbUp, _entryRelative, _entryWorld, _entryReference;
        float _climbLeft;
        bool _across, _bodyAnchored;

        public bool Climbing => State.Climbing;
        public bool WallRunning => State.WallRunning;

        public void Reset() { State.Reset(); _wall = _support = null; _supportBody = null; _bodyAnchored = false; }
        public void Stop() { State.StopWall(); State.StopClimb(); }
        public void Tick(float dt)
        {
            State.Tick(dt);
            if (!Climbing) return;
            _climbLeft -= StanceRules.NonNegative(dt);
        }

        static bool Ours(Collider collider, Transform owner) => collider != null && owner != null &&
            (collider.transform == owner || collider.transform.IsChildOf(owner));

        bool Blocks(Collider collider, Transform owner, Clearance clearance)
        {
            if (Ours(collider, owner)) return false;
            if (clearance == Clearance.All) return true;
            bool support = _supportBody != null ? collider.attachedRigidbody == _supportBody : collider == _support;
            return clearance == Clearance.Support ? support : !support;
        }

        static bool Solid(Collider collider) => collider != null && collider.enabled &&
            collider.gameObject.activeInHierarchy && !collider.isTrigger &&
            (collider.attachedRigidbody == null || collider.attachedRigidbody.isKinematic || collider.attachedRigidbody.mass >= 20f);

        static Vector3 SurfaceVelocity(Collider collider, Vector3 point) => collider != null && collider.attachedRigidbody != null
            ? collider.attachedRigidbody.GetPointVelocity(point) : Vector3.zero;

        static float Skin => Mathf.Max(0.015f, Physics.defaultContactOffset * 2f);
        // CharacterController.Move permits contacts inside skinWidth. Use the same eroded capsule for
        // both overlaps and continuous casts, with a small tolerance for a resting contact's rounding.
        static float QueryRadius(float radius, float skinWidth) =>
            Mathf.Max(radius * 0.5f, radius - Mathf.Max(Skin, StanceRules.NonNegative(skinWidth)) - 0.001f);

        bool SupportValid => Solid(_support) && (_bodyAnchored
            ? _supportBody != null && _support.attachedRigidbody == _supportBody
            : _support.attachedRigidbody == null);

        // RaycastHit points and GetPointVelocity belong to the physics pose. An interpolated body's
        // Transform is a render pose, including its children. Body-local anchors already include the
        // collider's fixed child offset; never run these points through that rendered child transform.
        Vector3 SupportLocalPoint(Vector3 point) => _supportBody != null
            ? Quaternion.Inverse(_supportBody.rotation) * (point - _supportBody.position)
            : _support.transform.InverseTransformPoint(point);

        Vector3 SupportWorldPoint(Vector3 point) => _supportBody != null
            ? _supportBody.position + _supportBody.rotation * point
            : _support.transform.TransformPoint(point);

        Vector3 SupportLocalDirection(Vector3 direction) => _supportBody != null
            ? Quaternion.Inverse(_supportBody.rotation) * direction
            : _support.transform.InverseTransformDirection(direction);

        Vector3 SupportWorldDirection(Vector3 direction) => _supportBody != null
            ? _supportBody.rotation * direction
            : _support.transform.TransformDirection(direction);

        /// <summary>Full stance capsule, with only a contact-sized inset; saturation always blocks.</summary>
        public bool CanOccupy(Vector3 feet, Vector3 up, float height, float radius, Transform owner, float skinWidth = 0f) =>
            CanOccupy(feet, up, height, radius, owner, skinWidth, Clearance.All);

        bool CanOccupy(Vector3 feet, Vector3 up, float height, float radius, Transform owner, float skinWidth, Clearance clearance)
        {
            if (height < radius * 2f || radius <= 0f) return false;
            int count = Physics.OverlapCapsuleNonAlloc(feet + up * radius, feet + up * (height - radius),
                QueryRadius(radius, skinWidth), _overlaps, ClearanceMask, QueryTriggerInteraction.Ignore);
            if (count == _overlaps.Length) return false;
            for (int i = 0; i < count; i++) if (Blocks(_overlaps[i], owner, clearance)) return false;
            return true;
        }

        /// <summary>Reject initial overlap as well as every solid along the entire capsule path.</summary>
        public bool CanSweep(Vector3 from, Vector3 to, Vector3 up, float height, float radius, Transform owner, float skinWidth = 0f) =>
            CanSweep(from, to, up, height, radius, owner, skinWidth, Clearance.All);

        bool CanSweep(Vector3 from, Vector3 to, Vector3 up, float height, float radius, Transform owner, float skinWidth, Clearance clearance)
        {
            if (!CanOccupy(from, up, height, radius, owner, skinWidth, clearance) ||
                !CanOccupy(to, up, height, radius, owner, skinWidth, clearance)) return false;
            Vector3 travel = to - from;
            float distance = travel.magnitude;
            if (distance <= 0.00001f) return true;
            int count = Physics.CapsuleCastNonAlloc(from + up * radius, from + up * (height - radius),
                QueryRadius(radius, skinWidth), travel / distance, _hits, distance, ClearanceMask, QueryTriggerInteraction.Ignore);
            if (count == _hits.Length) return false;
            for (int i = 0; i < count; i++) if (Blocks(_hits[i].collider, owner, clearance)) return false;
            return true;
        }

        bool Probe(Vector3 from, Vector3 direction, float distance, float radius, Transform owner, out RaycastHit nearest)
        {
            nearest = default;
            int count = Physics.SphereCastNonAlloc(from, radius, direction, _hits, distance,
                Layers.WorldMask, QueryTriggerInteraction.Ignore);
            if (count == _hits.Length) return false;
            float closest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (Ours(_hits[i].collider, owner) || _hits[i].distance >= closest) continue;
                nearest = _hits[i]; closest = nearest.distance;
            }
            return closest < float.MaxValue && Solid(nearest.collider);
        }

        public bool TryBeginClimb(Vector3 feet, Vector3 up, Vector3 forward, float height, float radius,
            Transform owner, Vector3 velocity, PlayerConfig config, float skinWidth = 0f)
        {
            if (!State.ClimbAvailable || Climbing) return false;
            forward = Vector3.ProjectOnPlane(forward, up).normalized;
            if (forward.sqrMagnitude < 0.9f) return false;
            float least = Mathf.Max(Skin, StanceRules.NonNegative(config.climbMinimumRise));
            float highest = StanceRules.NonNegative(config.climbMaximumRise);
            float reach = StanceRules.NonNegative(config.climbReach);
            if (highest < least || reach <= 0f || StanceRules.NonNegative(config.climbSpeed) <= 0f || StanceRules.NonNegative(config.climbSeconds) <= 0f) return false;
            // Find the face below its lip, then the actual walkable top behind that same solid face.
            if (!Probe(feet + up * Mathf.Min(height * 0.45f, least), forward, radius + reach, 0.06f, owner, out RaycastHit face)) return false;
            if (Mathf.Abs(Vector3.Dot(face.normal, up)) > 0.2f || Vector3.Dot(face.normal, forward) > -0.55f) return false;
            Vector3 above = feet + forward * (face.distance + radius + Skin * 3f) + up * (highest + Skin * 3f);
            if (!Probe(above, -up, highest - least + Skin * 4f, 0.025f, owner, out RaycastHit top)) return false;
            if (top.collider != face.collider || Vector3.Dot(top.normal, up) < SurfaceDot) return false;
            Vector3 destination = top.point + up * Skin;
            float rise = Vector3.Dot(destination - feet, up);
            if (rise < least || rise > highest) return false;
            Vector3 lift = feet + up * rise;
            if (!CanSweep(feet, lift, up, height, radius, owner, skinWidth) ||
                !CanSweep(lift, destination, up, height, radius, owner, skinWidth)) return false;
            if (!State.BeginClimb()) return false;
            _support = top.collider;
            _supportBody = _support.attachedRigidbody;
            _bodyAnchored = _supportBody != null;
            _liftLocal = SupportLocalPoint(lift);
            _topLocal = SupportLocalPoint(destination);
            _climbUp = up;
            _entryWorld = velocity;
            _entryReference = SurfaceVelocity(_support, feet);
            _entryRelative = SupportLocalDirection(velocity - _entryReference);
            _climbLeft = config.climbSeconds;
            _across = false;
            return true;
        }

        /// <summary>Entry inertia relative to the support survives a climb; climbing never leaves upward thrust.</summary>
        public Vector3 ClimbExitVelocity
        {
            get
            {
                Vector3 reference = SupportValid ? SurfaceVelocity(_support, SupportWorldPoint(_topLocal)) : _entryReference;
                Vector3 relative = SupportValid ? SupportWorldDirection(_entryRelative) : _entryWorld - _entryReference;
                return reference + relative - _climbUp * Mathf.Max(0f, Vector3.Dot(relative, _climbUp));
            }
        }

        public bool TryClimbStep(Vector3 feet, Vector3 up, float height, float radius, Transform owner,
            PlayerConfig config, float dt, out Vector3 displacement, float skinWidth = 0f)
        {
            displacement = Vector3.zero;
            if (!Climbing) return false;
            if (!SupportValid || Vector3.Dot(up, _climbUp) < 0.98f || _climbLeft <= 0f)
            { State.StopClimb(); return false; }
            Vector3 destination = SupportWorldPoint(_topLocal);
            Vector3 lift = SupportWorldPoint(_liftLocal);
            if (!CanOccupy(destination, up, height, radius, owner, skinWidth)) { State.StopClimb(); return false; }
            if (!_across && Vector3.Distance(feet, lift) <= 0.015f) _across = true;
            Vector3 target = _across ? destination : lift;
            if (_across && Vector3.Distance(feet, destination) <= 0.015f) { State.StopClimb(); return false; }
            float step = StanceRules.NonNegative(dt);
            Vector3 carry = SurfaceVelocity(_support, feet) * step;
            Vector3 relativeNext = Vector3.MoveTowards(feet, target, StanceRules.NonNegative(config.climbSpeed) * step);
            Vector3 next = relativeNext + carry;
            // The support advances with its passenger. Test its whole compound shape against relative
            // motion, and every other solid against the complete carried world path. A support moving
            // along its face normal must not obstruct its own carry at the old physics pose.
            bool clear = _supportBody == null
                ? CanSweep(feet, next, up, height, radius, owner, skinWidth)
                : CanSweep(feet, relativeNext, up, height, radius, owner, skinWidth, Clearance.Support) &&
                  CanSweep(feet, next, up, height, radius, owner, skinWidth, Clearance.Other);
            if (!clear) { State.StopClimb(); return false; }
            displacement = next - feet;
            return true;
        }

        /// <summary>Called after ordinary gravity has been budgeted. Cancels only the configured share of it.</summary>
        public bool TryWallRun(Vector3 feet, Vector3 up, Vector3 forward, float height, float radius,
            Transform owner, PlayerConfig config, bool allowed, bool grounded, bool sprint, float forwardInput,
            bool crouched, bool jumpPressed, float gravity, float dt, ref Vector3 velocity)
        {
            if (!allowed || grounded || !sprint || forwardInput <= 0.3f || crouched || Climbing ||
                (!WallRunning && !State.WallAvailable(WallPresent)))
            { State.StopWall(); return false; }
            forward = Vector3.ProjectOnPlane(forward, up).normalized;
            Vector3 right = Vector3.Cross(up, forward);
            Vector3 chest = feet + up * (height * 0.6f);
            bool left = Probe(chest, -right, radius + StanceRules.NonNegative(config.wallReach), 0.08f, owner, out RaycastHit a);
            bool rightWall = Probe(chest, right, radius + StanceRules.NonNegative(config.wallReach), 0.08f, owner, out RaycastHit b);
            RaycastHit wall = a;
            bool found = left || rightWall;
            if (!left || (rightWall && (WallRunning ? b.collider == _wall : b.distance < a.distance))) wall = b;
            Vector3 reference = found ? SurfaceVelocity(wall.collider, feet) : Vector3.zero;
            Vector3 relative = velocity - reference;
            Vector3 flat = Vector3.ProjectOnPlane(relative, up);
            bool intent = found && !Climbing && forwardInput > 0.3f &&
                Mathf.Abs(Vector3.Dot(wall.normal, up)) <= 0.2f &&
                Vector3.Dot(flat.normalized, forward) > 0.5f &&
                ParkourRules.WallIntent(allowed, grounded, sprint, forwardInput, flat.magnitude,
                    config.wallMinimumSpeed, Vector3.Dot(forward, wall.normal), config.wallParallelDot,
                    gravity, config.parkourMinimumGravity, crouched);
            if (!intent || (WallRunning && wall.collider != _wall)) { State.StopWall(); return false; }
            if (!WallRunning)
            {
                if (!State.BeginWall(WallPresent, config.wallRunSeconds)) return false;
                _wall = wall.collider;
            }
            // Do not glue the body to a wall or add along-wall speed. Existing momentum carries the run.
            float inward = Vector3.Dot(relative, wall.normal);
            if (inward < 0f) relative -= wall.normal * inward;
            if (jumpPressed)
            {
                relative -= up * Vector3.Dot(relative, up);
                relative += wall.normal * StanceRules.NonNegative(config.wallJumpAwaySpeed) + up * StanceRules.NonNegative(config.wallJumpUpSpeed);
                State.StopWall();
            }
            else
            {
                relative += up * (gravity * (1f - Mathf.Clamp01(StanceRules.NonNegative(config.wallGravityShare))) * StanceRules.NonNegative(dt));
                float fall = Vector3.Dot(relative, up);
                if (fall < -StanceRules.NonNegative(config.wallFallSpeed)) relative -= up * (fall + StanceRules.NonNegative(config.wallFallSpeed));
            }
            velocity = reference + relative;
            return true;
        }
    }
}
