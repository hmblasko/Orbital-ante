using System;
using System.Collections.Generic;
using OrbitalAnte.Combat;
using UnityEngine;

namespace OrbitalAnte.Characters
{
    [Serializable]
    public class MannequinRig
    {
        public Transform Hips, Spine, Chest, Neck, Head;
        // Index 0 = left, 1 = right.
        public Transform[] UpperArm = new Transform[2];
        public Transform[] Forearm = new Transform[2];
        public Transform[] Hand = new Transform[2];
        public Transform[] Thigh = new Transform[2];
        public Transform[] Shin = new Transform[2];
        public Transform[] Foot = new Transform[2];
        public Vector3 HipsRestLocal;
        /// <summary>Head-sphere centre measured from the hips in the rest pose.</summary>
        public float HeadCentreAboveHips;
    }

    /// <summary>
    /// A living breakable character. Owns its parts, decides what falls off, and tracks how the
    /// remaining body can move. The only death is losing the head.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class Mannequin : MonoBehaviour
    {
        public static readonly List<Mannequin> Alive = new List<Mannequin>();

        public MannequinConfig Config;
        public MannequinRig Rig;
        public readonly Dictionary<BodyPartId, BodyPart> Parts = new Dictionary<BodyPartId, BodyPart>();

        public PartSet Attached { get; private set; } = PartSet.All;
        public LocomotionState State { get; private set; } = LocomotionState.Normal;
        public bool IsAlive => State != LocomotionState.Dead;
        public bool CanShoot => MannequinAnatomy.CanShoot(Attached);
        public int UsableHands => MannequinAnatomy.UsableHands(Attached);

        public event Action<Mannequin, LocomotionState> StateChanged;
        public event Action<Mannequin, BodyPartId> PartLost;
        public event Action<Mannequin> Died;

        CharacterController cc;

        void Awake() => cc = GetComponent<CharacterController>();
        void OnEnable() => Alive.Add(this);
        void OnDisable() => Alive.Remove(this);

        public void Register(BodyPart part)
        {
            Parts[part.Id] = part;
            part.Owner = this;
        }

        public HitResult OnPartBroken(BodyPart part, in HitInfo hit)
        {
            if (!IsAlive || !Attached.Has(part.Id)) return HitResult.None;

            if (part.Id == BodyPartId.Head)
            {
                Die(hit);
                return HitResult.Killed;
            }

            var lost = MannequinAnatomy.PartsLostWhenBroken(part.Id, Attached);
            if (part.Id == BodyPartId.Torso)
                Parts[BodyPartId.Head].ShowSeverCaps(); // head separates from the torso
            else
                part.ShowSeverCaps();

            DetachAsGib(lost, hit, 1f);
            foreach (var id in lost) PartLost?.Invoke(this, id);
            RefreshState();
            return HitResult.Severed;
        }

        void Die(in HitInfo hit)
        {
            var head = Parts[BodyPartId.Head];
            head.ShowSeverCaps();
            var headHit = hit;
            headHit.ForceScale *= Config.headshotImpulseMultiplier;
            DetachAsGib(new List<BodyPartId> { BodyPartId.Head }, headHit, 1f);

            // Whatever body is left collapses as one ragdoll chain, keeping its momentum.
            var rest = MannequinAnatomy.AttachedSubtree(BodyPartId.Torso, Attached);
            if (rest.Count > 0) DetachAsGib(rest, hit, Config.deathBodyImpulseShare);

            Attached = PartSet.None;
            State = LocomotionState.Dead;
            StateChanged?.Invoke(this, State);
            Died?.Invoke(this);
            Destroy(gameObject);
        }

        void DetachAsGib(List<BodyPartId> ids, in HitInfo hit, float impulseShare)
        {
            var chain = new List<BodyPart>(ids.Count);
            var set = Attached;
            foreach (var id in ids)
            {
                chain.Add(Parts[id]);
                set = set.Without(id);
            }
            Attached = set;
            Vector3 inherit = cc != null ? cc.velocity : Vector3.zero;
            GibManager.SpawnChain(chain, hit, inherit, Config, impulseShare);
        }

        void RefreshState()
        {
            var next = MannequinAnatomy.Evaluate(Attached);
            if (next == State) return;
            State = next;
            ApplyCapsuleFor(next);
            StateChanged?.Invoke(this, next);
        }

        void ApplyCapsuleFor(LocomotionState s)
        {
            switch (s)
            {
                case LocomotionState.Normal:
                case LocomotionState.Limp:
                    SetCapsule(1.8f, 0.3f);
                    break;
                case LocomotionState.Crawl:
                case LocomotionState.Wriggle:
                    SetCapsule(0.5f, 0.25f);
                    break;
                case LocomotionState.HeadOnly:
                    SetCapsule(0.26f, 0.13f);
                    break;
            }
        }

        void SetCapsule(float height, float radius)
        {
            cc.radius = radius;
            cc.height = Mathf.Max(height, radius * 2f);
            cc.center = new Vector3(0, cc.height * 0.5f, 0);
            cc.stepOffset = Mathf.Min(0.35f, cc.height * 0.4f);
        }
    }
}
