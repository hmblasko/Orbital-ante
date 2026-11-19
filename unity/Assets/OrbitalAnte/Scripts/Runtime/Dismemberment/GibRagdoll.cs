using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Space;
using UnityEngine;

namespace OrbitalAnte.Dismemberment
{
    /// <summary>
    /// The joints of a loose chunk whose parts move against each other: a corpse that crumples, an arm that folds
    /// at the elbow as it flies (Matthew, 15 Nov 2026: "a very realistic ragdoll ... soft limits with tension").
    /// It sits on the chunk's container next to <see cref="Gib"/>.
    ///
    /// The top part of the chunk rides on the container's own body, as in a stiff chunk. Every other part gets a
    /// weight of its own and one joint to the part above it. What a joint allows, how soft its stop is and how the
    /// muscles hold it all come from <see cref="RagdollRules"/>; nothing is decided here.
    ///
    /// Nothing about the rules of breaking changes: the chunk is still one group in the <see cref="BodyModel"/>,
    /// shooting a part of it still splits it (the piece that comes away takes its own joints with it), and it is
    /// still pooled, capped, faded and cleared by <see cref="GibManager"/>. A jointed chunk costs a weight and a
    /// joint per part, so only a few are jointed at once and each goes stiff again once it has come to rest
    /// (<see cref="RagdollBudget"/>).
    /// </summary>
    public sealed class GibRagdoll : MonoBehaviour
    {
        static readonly List<GibRagdoll> Live = new List<GibRagdoll>();
        static readonly List<RagdollBudget.Slot> Slots = new List<RagdollBudget.Slot>();

        /// <summary>Chunks being worked out joint by joint right now (shown on F3 and checked by tests).</summary>
        public static int LiveCount => Live.Count;

        public static void ResetStatics() => Live.Clear();

        Gib _gib;
        RagdollTuning _tuning;
        readonly List<BodyPart> _jointed = new List<BodyPart>();
        float _still;
        float _nextDriveAt;
        bool _sank;

        /// <summary>How many parts of this chunk have a weight and a joint of their own.</summary>
        public int JointCount => _jointed.Count;
        public bool Jointed => _jointed.Count > 0;

        /// <summary>
        /// Called once a chunk's parts have been gathered under its container. Parts that were jointed in the chunk
        /// they came from keep their joints where the part above came with them; the new top part gives its own
        /// weight up to the container. Then, if this kind of body is jointed, the rest are given joints.
        /// </summary>
        public static void Fit(Gib gib, Detachment detachment, BodyPart[] parts, Gib source, RagdollTuning tuning)
        {
            if (gib == null) return;
            BodyPartId[] members = detachment.Members;

            // The parts have left the chunk they came from.
            GibRagdoll from = source != null ? source.Ragdoll : null;
            bool hadJoints = false;
            for (int i = 0; i < members.Length; i++)
            {
                BodyPart part = parts[(int)members[i]];
                if (part == null) continue;
                if (from != null) from._jointed.Remove(part);
                hadJoints |= part.OwnBody != null;
            }
            if (from != null && !from.Jointed) from.Rest();

            // The top part rides on the container.
            BodyPart top = parts[(int)detachment.Root];
            if (top != null && top.OwnBody != null) Strip(top);

            bool wanted = tuning != null && tuning.jointed && members.Length > 1;
            if (!wanted && !hadJoints) return;

            GibRagdoll ragdoll = gib.Ragdoll;
            if (ragdoll == null)
            {
                ragdoll = gib.gameObject.AddComponent<GibRagdoll>();
                ragdoll._gib = gib;
                gib.Ragdoll = ragdoll;
            }
            ragdoll._tuning = tuning ?? new RagdollTuning();
            ragdoll._jointed.Clear();
            ragdoll._still = 0f;
            ragdoll._sank = false;
            ragdoll._nextDriveAt = 0f;

            if (!wanted)
            {
                // A jointed chunk's piece on a body that is no longer to be jointed: all one stiff piece.
                for (int i = 0; i < members.Length; i++)
                {
                    BodyPart part = parts[(int)members[i]];
                    if (part != null && part.OwnBody != null) Strip(part);
                }
                ragdoll.Rest();
                return;
            }

            MakeRoom(ragdoll, ragdoll._tuning);

            // Members come top first, so the part above is always ready before the part below is hung from it.
            bool ownPull = gib.OwnPull;
            // First every part that needs a joint made is turned to its joint's relaxed pose against the part above
            // (the joint measures its range from how the two stand when it is made), then the joints are made,
            // then every part is put back as it was. The physics engine is told of the poses in between.
            var made = ragdoll._made;
            var kept = ragdoll._kept;
            made.Clear();
            kept.Clear();
            for (int i = 0; i < members.Length; i++)
            {
                BodyPartId id = members[i];
                if (id == detachment.Root) continue;
                BodyPart part = parts[(int)id];
                BodyPart above = parts[(int)BodyLayout.Parent(id)];
                if (part == null || above == null) continue;
                ragdoll._jointed.Add(part);
                // One that came with its joint, and whose part above still has its own weight, is left alone.
                bool aboveIsTop = BodyLayout.Parent(id) == detachment.Root;
                if (part.Joint != null && !aboveIsTop) continue;
                made.Add(part);
                kept.Add(part.transform.rotation);
            }
            for (int i = 0; i < made.Count; i++)
            {
                BodyPart part = made[i];
                BodyPart above = parts[(int)BodyLayout.Parent(part.Id)];
                part.transform.rotation = above.transform.rotation * ToQuaternion(RagdollRules.Relaxed(part.Id));
                if (part.OwnBody == null) part.OwnBody = NewBody(part, ownPull, ragdoll);
            }
            // A part that gave its weight up earlier in this same frame still has the old one until the frame
            // ends, and Unity will not give it a second: that part, and whatever hangs below it, stays stiff.
            for (int i = 0; i < made.Count; i++)
            {
                BodyPart part = made[i];
                BodyPart above = parts[(int)BodyLayout.Parent(part.Id)];
                bool aboveIsTop = BodyLayout.Parent(part.Id) == detachment.Root;
                if (part.OwnBody != null && (aboveIsTop || above.OwnBody != null)) continue;
                part.transform.rotation = kept[i];
                if (part.OwnBody != null) Strip(part);
                ragdoll._jointed.Remove(part);
                made.RemoveAt(i);
                kept.RemoveAt(i);
                i--;
            }
            // Nothing may be left hanging from a part that has no weight of its own (top first, so one pass does).
            for (int i = 0; i < ragdoll._jointed.Count; i++)
            {
                BodyPart part = ragdoll._jointed[i];
                BodyPartId aboveId = BodyLayout.Parent(part.Id);
                if (aboveId == detachment.Root || parts[(int)aboveId].OwnBody != null) continue;
                int at = made.IndexOf(part);
                if (at >= 0)
                {
                    part.transform.rotation = kept[at];
                    made.RemoveAt(at);
                    kept.RemoveAt(at);
                }
                Strip(part);
                ragdoll._jointed.RemoveAt(i);
                i--;
            }
            Physics.SyncTransforms();
            for (int i = 0; i < made.Count; i++)
            {
                BodyPart part = made[i];
                BodyPartId aboveId = BodyLayout.Parent(part.Id);
                BodyPart above = parts[(int)aboveId];
                Rigidbody holder = aboveId == detachment.Root ? gib.Body : above.OwnBody;
                Join(part, above, holder, ragdoll._tuning, 0f);
            }
            for (int i = made.Count - 1; i >= 0; i--) made[i].transform.rotation = kept[i];
            Physics.SyncTransforms();
            made.Clear();
            kept.Clear();

            if (ragdoll.Jointed)
            {
                if (!Live.Contains(ragdoll)) Live.Add(ragdoll);
                ragdoll.enabled = true;
            }
            else ragdoll.Rest();
        }

        readonly List<BodyPart> _made = new List<BodyPart>();
        readonly List<Quaternion> _kept = new List<Quaternion>();

        /// <summary>Makes the oldest jointed chunk stiff if another would be one too many.</summary>
        static void MakeRoom(GibRagdoll arriving, RagdollTuning tuning)
        {
            for (int guard = 0; guard < 8; guard++)
            {
                Slots.Clear();
                for (int i = Live.Count - 1; i >= 0; i--)
                {
                    if (Live[i] == null || Live[i] == arriving) Live.RemoveAt(i);
                }
                for (int i = 0; i < Live.Count; i++) Slots.Add(Live[i].Slot());
                int pick = RagdollBudget.MakeRoom(Slots, tuning);
                if (pick < 0) return;
                Live[pick].MakeStiff();
            }
        }

        RagdollBudget.Slot Slot()
        {
            return new RagdollBudget.Slot { Age = _gib != null ? _gib.Age : 0f, Still = _still, Joints = _jointed.Count };
        }

        static Rigidbody NewBody(BodyPart part, bool ownPull, GibRagdoll ragdoll)
        {
            if (part.GetComponent<Rigidbody>() != null) return null;
            var body = part.gameObject.AddComponent<Rigidbody>();
            if (body == null) return null;
            body.mass = Mathf.Max(0.3f, part.Mass);
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.maxAngularVelocity = 25f;
            body.angularDamping = 0.05f;
            // Joints in a chain want more passes than a lone tumbling limb, and parts that start a little inside
            // each other must ease apart, not spring apart.
            body.solverIterations = 12;
            body.solverVelocityIterations = 4;
            body.maxDepenetrationVelocity = 3f;
            body.useGravity = !ownPull;
            if (ownPull)
            {
                // In the solar system each weight falls toward its own world, like the chunk it belongs to.
                var pull = part.gameObject.AddComponent<GravityBody>();
                pull.SunkBelow = -1f;
                pull.PutBackAt = 0.3f;
                pull.Sank = ragdoll.PartSank;
            }
            return body;
        }

        void PartSank(Vector3 onTop, Transform world) => _sank = true;

        /// <summary>Called from the solar system's walk of loose things: only noted here, acted on at the next step.</summary>
        public void Sank() => _sank = true;

        /// <summary>
        /// Hangs a part from the body above it. The part must be standing in its joint's relaxed pose against the
        /// part above when this is called: the range is given as so far either side of that.
        /// </summary>
        static void Join(BodyPart part, BodyPart above, Rigidbody holder, RagdollTuning tuning, float age)
        {
            RagdollJoint spec = RagdollRules.JointFor(part.Id);
            ConfigurableJoint joint = part.Joint;
            if (joint == null)
            {
                joint = part.gameObject.AddComponent<ConfigurableJoint>();
                part.Joint = joint;
            }
            else
            {
                // Letting go first makes the joint measure itself again when it is given its new holder.
                joint.connectedBody = null;
            }

            joint.autoConfigureConnectedAnchor = false;
            joint.axis = ToVector(spec.Axis);
            joint.secondaryAxis = ToVector(spec.Second);
            // A part's origin is on its joint, except the head's, which is the middle of the head.
            Vec3d own = BodySkeleton.RestPosition(BodySkeleton.BoneOf(part.Id)) - BodySkeleton.PartOrigin(part.Id);
            joint.anchor = ToVector(own);
            // The same point on the part above, in the space of whatever body carries that part.
            Vec3d onAbove = BodySkeleton.RestPosition(BodySkeleton.BoneOf(part.Id)) - BodySkeleton.PartOrigin(above.Id);
            Vector3 world = above.transform.TransformPoint(ToVector(onAbove));
            joint.connectedBody = holder;
            joint.connectedAnchor = holder.transform.InverseTransformPoint(world);

            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Limited;

            // So far either side of the relaxed pose: the same number both ways, so no sign can be the wrong way round.
            joint.lowAngularXLimit = new SoftJointLimit { limit = -(float)spec.Half };
            joint.highAngularXLimit = new SoftJointLimit { limit = (float)spec.Half };
            joint.angularYLimit = new SoftJointLimit { limit = Mathf.Max(3f, (float)spec.SecondHalf) };
            joint.angularZLimit = new SoftJointLimit { limit = Mathf.Max(3f, (float)spec.ThirdHalf) };

            // The soft stop: past the end of the range a spring pulls back, harder the further past.
            var stop = new SoftJointLimitSpring
            {
                spring = (float)RagdollRules.LimitSpring(part.Id, tuning),
                damper = (float)RagdollRules.LimitDamper(part.Id, tuning),
            };
            joint.angularXLimitSpring = stop;
            joint.angularYZLimitSpring = stop;

            // The muscle: a light pull toward the relaxed pose, and a drag that starts braced and goes limp.
            joint.rotationDriveMode = RotationDriveMode.Slerp;
            joint.targetRotation = Quaternion.identity;
            joint.slerpDrive = Drive(part.Id, tuning, age);

            joint.enableCollision = false;
            joint.enablePreprocessing = false;
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = 0.03f;
            joint.projectionAngle = 180f;
        }

        static JointDrive Drive(BodyPartId id, RagdollTuning tuning, float age)
        {
            return new JointDrive
            {
                positionSpring = (float)RagdollRules.ToneSpring(id, tuning),
                positionDamper = (float)RagdollRules.JointDamper(id, tuning, age),
                maximumForce = float.MaxValue,
            };
        }

        /// <summary>
        /// Sends every part off with the chunk: the same flight and spin the container was given, and a knock to
        /// the part the bullet struck, so a body folds round the hit instead of leaving as a statue.
        /// </summary>
        public void Launch(BodyPart struck, in HitInfo hit, float knockSpeed)
        {
            if (_gib == null || _gib.Body == null) return;
            Rigidbody root = _gib.Body;
            Vector3 linear = root.linearVelocity;
            Vector3 spin = root.angularVelocity;
            Vector3 about = root.worldCenterOfMass;
            for (int i = 0; i < _jointed.Count; i++)
            {
                Rigidbody body = _jointed[i] != null ? _jointed[i].OwnBody : null;
                if (body == null) continue;
                body.linearVelocity = linear + Vector3.Cross(spin, body.worldCenterOfMass - about);
                body.angularVelocity = spin;
            }
            if (struck != null && struck.OwnBody != null && _jointed.Contains(struck) && hit.Direction.sqrMagnitude > 0.0001f)
            {
                struck.OwnBody.AddForceAtPosition(hit.Direction.normalized * knockSpeed, hit.Point, ForceMode.VelocityChange);
            }
        }

        void FixedUpdate()
        {
            if (!Jointed || _gib == null || !_gib.InUse)
            {
                Rest();
                return;
            }
            if (_sank)
            {
                MakeStiff();
                return;
            }

            float age = _gib.Age;
            // The brace lets go over the first half second: ten changes a second is plenty.
            if (age <= _tuning.braceSeconds + 0.2f && Time.time >= _nextDriveAt)
            {
                _nextDriveAt = Time.time + 0.1f;
                for (int i = 0; i < _jointed.Count; i++)
                {
                    BodyPart part = _jointed[i];
                    if (part != null && part.Joint != null) part.Joint.slerpDrive = Drive(part.Id, _tuning, age);
                }
            }

            // At rest once nothing is moving against the container any more.
            Rigidbody root = _gib.Body;
            bool moving = false;
            for (int i = 0; i < _jointed.Count && !moving; i++)
            {
                Rigidbody body = _jointed[i] != null ? _jointed[i].OwnBody : null;
                if (body == null || body.IsSleeping()) continue;
                Vector3 against = body.linearVelocity - root.GetPointVelocity(body.worldCenterOfMass);
                moving = against.sqrMagnitude > 0.03f || (body.angularVelocity - root.angularVelocity).sqrMagnitude > 0.12f;
            }
            _still = moving ? 0f : _still + Time.fixedDeltaTime;

            if (RagdollBudget.Done(Slot(), _tuning)) MakeStiff();
        }

        /// <summary>
        /// Makes the chunk one stiff piece again, in the pose it has now: every part gives up its own weight and
        /// joint and rides on the container, exactly as chunks did before there were joints.
        /// </summary>
        public void MakeStiff()
        {
            for (int i = 0; i < _jointed.Count; i++)
            {
                if (_jointed[i] != null) Strip(_jointed[i]);
            }
            _jointed.Clear();
            if (_gib != null && _gib.Body != null && _gib.InUse) _gib.RecalculateMass();
            Rest();
        }

        /// <summary>The chunk is being put away: its parts are destroyed with their joints, so only forget them.</summary>
        public void Forget()
        {
            for (int i = 0; i < _jointed.Count; i++)
            {
                BodyPart part = _jointed[i];
                if (part == null) continue;
                part.OwnBody = null;
                part.Joint = null;
            }
            _jointed.Clear();
            Rest();
        }

        void Rest()
        {
            _sank = false;
            _still = 0f;
            Live.Remove(this);
            enabled = false;
        }

        void OnDestroy() => Live.Remove(this);

        /// <summary>The arena slid everything along: the parts' own bodies go too.</summary>
        public void Shift(Vector3 by)
        {
            for (int i = 0; i < _jointed.Count; i++)
            {
                Rigidbody body = _jointed[i] != null ? _jointed[i].OwnBody : null;
                if (body != null) body.position += by;
            }
        }

        /// <summary>
        /// Takes a part's own weight and joint away, so it rides on whatever body its container has. Components go
        /// at the end of the frame; until then the part is held still against the container and pulls on nothing.
        /// </summary>
        static void Strip(BodyPart part)
        {
            Rigidbody body = part.OwnBody;
            ConfigurableJoint joint = part.Joint;
            part.OwnBody = null;
            part.Joint = null;
            if (body == null) return;

            var pull = part.GetComponent<GravityBody>();
            if (pull != null)
            {
                pull.Sank = null;
                pull.enabled = false;
                Destroy(pull);
            }
            if (joint != null)
            {
                joint.connectedBody = null;
                Destroy(joint);
            }
            body.isKinematic = true;
            Destroy(body);
        }

        static Vector3 ToVector(Vec3d v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        static Quaternion ToQuaternion(QuatD q) => new Quaternion((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);
    }
}
