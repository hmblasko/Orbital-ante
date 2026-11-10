using System;
using System.Collections.Generic;
using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Dismemberment
{
    /// <summary>
    /// A breakable character. Holds the rules (BodyModel), the 14 part objects and any loose chunks (gibs)
    /// that have come off it, and turns rule results into physics objects.
    /// </summary>
    public sealed class Mannequin : MonoBehaviour
    {
        public BodyModel Model { get; private set; }
        public MannequinConfig Config { get; private set; }
        public MannequinRig Rig { get; private set; }

        /// <summary>Current movement of the character, set by its locomotion. Loose parts inherit it.</summary>
        public Vector3 Velocity { get; set; }

        public bool Alive => Model != null && Model.Alive;
        public MovementState State => Model != null ? Model.State : MovementState.Dead;

        /// <summary>A chunk of parts just came off (from the body or from an older gib).</summary>
        public event Action<Mannequin, Detachment, Gib, HitInfo> PartDetached;
        public event Action<Mannequin, MovementState> StateChanged;
        public event Action<Mannequin> Died;

        /// <summary>Raised for every chunk that comes off any character. Effects (blood, sound) listen here.</summary>
        public static event Action<Mannequin, Detachment, Gib, HitInfo> AnyPartDetached;

        public static void ResetStatics() => AnyPartDetached = null;

        readonly BodyPart[] _parts = new BodyPart[BodyLayout.Count];
        readonly Dictionary<int, Gib> _gibs = new Dictionary<int, Gib>();
        Collider _bodyBlocker;

        public int ActiveGibCount => _gibs.Count;

        public void Initialize(MannequinConfig config, MannequinRig rig, BodyPart[] parts, Collider bodyBlocker)
        {
            Config = config;
            Rig = rig;
            _bodyBlocker = bodyBlocker;
            for (int i = 0; i < BodyLayout.Count; i++) _parts[i] = parts[i];
            Model = new BodyModel(config.BuildHpTable());
        }

        public BodyPart Part(BodyPartId id) => _parts[(int)id];

        /// <summary>The gib a group id belongs to, or null if it has already been cleaned up.</summary>
        public Gib GibForGroup(int group)
        {
            return _gibs.TryGetValue(group, out var gib) ? gib : null;
        }

        /// <summary>Damages a part as if a bullet hit its centre travelling in <paramref name="direction"/>.</summary>
        public HitOutcome DamagePart(BodyPartId id, float damage, Vector3 direction)
        {
            var part = _parts[(int)id];
            if (part == null) return default;
            var hit = new HitInfo
            {
                Damage = damage,
                Point = part.transform.position,
                Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward,
                Normal = -direction,
            };
            return ApplyHit(part, hit);
        }

        public HitOutcome ApplyHit(BodyPart part, in HitInfo hit)
        {
            if (Model == null || part == null) return default;

            MovementState before = Model.State;
            int groupBefore = Model.Group(part.Id);
            bool torsoWasOnBody = Model.OnBody(BodyPartId.Torso);
            DamageResult result = Model.ApplyDamage(part.Id, hit.Damage);

            for (int i = 0; i < result.Detachments.Count; i++)
            {
                var detachment = result.Detachments[i];
                Gib gib = SpawnGib(detachment, hit);
                PartDetached?.Invoke(this, detachment, gib, hit);
                AnyPartDetached?.Invoke(this, detachment, gib, hit);
            }

            if (result.Detachments.Count > 0)
            {
                RefreshCaps();
            }
            else if (groupBefore != 0)
            {
                // A loose chunk that did not split still gets knocked about by the bullet.
                Gib loose = GibForGroup(groupBefore);
                if (loose != null && loose.Body != null)
                {
                    loose.Body.AddForceAtPosition(hit.Direction * Config.gibPushSpeed, hit.Point, ForceMode.VelocityChange);
                }
            }

            if (result.Killed)
            {
                if (_bodyBlocker != null) _bodyBlocker.enabled = false;
                Died?.Invoke(this);
            }

            MovementState after = Model.State;
            if (after != before) StateChanged?.Invoke(this, after);

            return new HitOutcome
            {
                Hit = true,
                Severed = result.Detachments.Count > 0,
                Killed = result.Killed,
                HeadshotKill = result.Killed && torsoWasOnBody,
                Juice = JuiceFor(result, groupBefore),
                Label = BodyLayout.DisplayName(part.Id),
            };
        }

        /// <summary>How big a moment a hit was, for hitstop and camera shake. 0 when nothing came off.</summary>
        static float JuiceFor(DamageResult result, int groupBefore)
        {
            if (result.Killed) return 1f;
            if (result.Detachments.Count == 0) return 0f;
            // Splitting a chunk that is already loose is a smaller moment than taking a limb off a body.
            if (groupBefore != 0) return 0.3f;

            switch (result.Detachments[result.Detachments.Count - 1].Root)
            {
                case BodyPartId.Torso:
                    return 0.9f;
                case BodyPartId.LeftThigh:
                case BodyPartId.RightThigh:
                case BodyPartId.LeftUpperArm:
                case BodyPartId.RightUpperArm:
                    return 0.7f;
                case BodyPartId.LeftShin:
                case BodyPartId.RightShin:
                case BodyPartId.LeftForearm:
                case BodyPartId.RightForearm:
                    return 0.55f;
                default:
                    return 0.4f;
            }
        }

        Gib SpawnGib(Detachment detachment, in HitInfo hit)
        {
            BodyPart rootPart = _parts[(int)detachment.Root];
            if (rootPart == null) return null;

            // Work out what the chunk was riding on, so it keeps that motion (real inertia).
            Vector3 inheritedVelocity = Velocity;
            Gib source = detachment.SourceGroup != 0 ? GibForGroup(detachment.SourceGroup) : null;
            if (source != null && source.Body != null)
            {
                inheritedVelocity = source.Body.GetPointVelocity(rootPart.transform.position);
            }

            Vector3 torsoPosition = _parts[(int)BodyPartId.Torso] != null
                ? _parts[(int)BodyPartId.Torso].transform.position
                : transform.position;

            GibManager manager = GibManager.GetOrCreate();
            Gib gib = manager.Spawn(this, detachment.Group, rootPart.transform.position, rootPart.transform.rotation);
            Rigidbody body = gib.Body;

            for (int i = 0; i < detachment.Members.Length; i++)
            {
                BodyPart member = _parts[(int)detachment.Members[i]];
                if (member == null) continue;
                member.transform.SetParent(gib.transform, true);
                manager.PreparePart(member);
                gib.Parts.Add(member);
                if (source != null) source.Parts.Remove(member);
            }

            gib.RecalculateMass();
            if (source != null && source.Body != null) source.RecalculateMass();
            _gibs[detachment.Group] = gib;

            // Launch. Deliberately over the top.
            Vector3 dir = hit.Direction.sqrMagnitude > 0.0001f ? hit.Direction.normalized : transform.forward;
            Vector3 launch;
            float spin = Config.gibSpin;
            switch (detachment.Reason)
            {
                case DetachReason.TorsoDestroyed:
                {
                    Vector3 outward = rootPart.transform.position - torsoPosition;
                    outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : UnityEngine.Random.onUnitSphere;
                    launch = outward * Config.torsoBurstSpeed + dir * (Config.gibLaunchSpeed * 0.4f) + Vector3.up * Config.gibLift;
                    break;
                }
                case DetachReason.HeadDestroyed:
                    launch = dir * (Config.gibLaunchSpeed * 1.2f) + Vector3.up * (Config.gibLift * 1.5f);
                    break;
                case DetachReason.Corpse:
                    launch = dir * (Config.gibLaunchSpeed * 0.35f) + Vector3.up * 0.6f;
                    spin *= 0.2f;
                    break;
                default:
                    launch = dir * Config.gibLaunchSpeed + Vector3.up * Config.gibLift;
                    break;
            }

            body.linearVelocity = inheritedVelocity + launch;
            body.angularVelocity = UnityEngine.Random.onUnitSphere * spin;
            return gib;
        }

        /// <summary>Shows a wound cap on both sides of every joint that has been broken.</summary>
        void RefreshCaps()
        {
            for (int i = 0; i < BodyLayout.Count; i++)
            {
                var id = (BodyPartId)i;
                if (!BodyLayout.HasParent(id)) continue;
                bool separated = Model.SeparatedFromParent(id);

                BodyPart part = _parts[i];
                if (part != null && part.ProximalCap != null) part.ProximalCap.SetActive(separated);

                BodyPart parent = _parts[(int)BodyLayout.Parent(id)];
                if (parent != null)
                {
                    GameObject cap = parent.ChildCap(id);
                    if (cap != null) cap.SetActive(separated);
                }
            }
        }

        /// <summary>Called by the gib pool when one of this character's loose chunks has been cleaned up.</summary>
        public void NotifyGibGone(int group, Gib gib)
        {
            if (_gibs.TryGetValue(group, out var known) && ReferenceEquals(known, gib))
            {
                _gibs.Remove(group);
            }
            // Once a dead character's last loose part has been cleaned up, nothing of it is left.
            if (Model != null && !Model.Alive && _gibs.Count == 0 && this != null)
            {
                Destroy(gameObject);
            }
        }
    }
}
