using System.Collections.Generic;
using OrbitalAnte.Combat;
using UnityEngine;

namespace OrbitalAnte.Characters
{
    /// <summary>
    /// One breakable piece of a character. While attached it is a trigger hitbox parented to
    /// its bone. Once it comes off it becomes a rigidbody joined to the rest of its chain, and
    /// it can be shot again to split the chain further.
    /// </summary>
    public class BodyPart : MonoBehaviour
    {
        public BodyPartId Id;
        public float MaxHp;
        public float Hp;
        public float Radius;

        /// <summary>The living character this part belongs to; null once it is a gib.</summary>
        public Mannequin Owner;
        public BodyPart Parent;
        public readonly List<BodyPart> Children = new List<BodyPart>();

        public Collider Collider;
        /// <summary>Joint position in this part's local space (where it meets its parent).</summary>
        public Vector3 ProximalLocal;
        /// <summary>Stump cap that lives on the parent part and shows when this part is lost.</summary>
        public GameObject StumpCapOnParent;
        /// <summary>Cap on this part's own cut end.</summary>
        public GameObject ProximalCap;

        public Rigidbody Body { get; private set; }
        public CharacterJoint Joint { get; private set; }
        public bool IsGib => Body != null;
        public float GibBornTime { get; private set; }
        MannequinConfig config;

        public void Init(BodyPartId id, PartTuning tuning, float radius, Collider col, MannequinConfig cfg)
        {
            Id = id;
            MaxHp = tuning.maxHp;
            Hp = MaxHp;
            Radius = radius;
            Collider = col;
            config = cfg;
        }

        public HitResult TakeHit(in HitInfo hit)
        {
            if (!IsGib && Owner == null) return HitResult.None;
            Hp -= hit.Damage;
            if (Hp > 0f)
            {
                if (IsGib) Body.AddForceAtPosition(hit.Direction * hit.ForceScale * 1.5f, hit.Point, ForceMode.VelocityChange);
                return HitResult.Damaged;
            }
            if (!IsGib) return Owner.OnPartBroken(this, hit);
            BreakWhileGib(hit);
            return HitResult.GibSplit;
        }

        /// <summary>Turns this part into a physics body. Called by GibManager.</summary>
        public void BecomeGib(Rigidbody body, CharacterJoint joint, float now)
        {
            Owner = null;
            Body = body;
            Joint = joint;
            GibBornTime = now;
            Hp = MaxHp * config.gibHpFactor;
        }

        /// <summary>
        /// Safely destroys this gib. Joints of children hanging off it are released first,
        /// otherwise Unity would pin those children to a fixed point in the world.
        /// </summary>
        public void DestroyGib()
        {
            foreach (var c in Children)
            {
                if (c == null || c.Joint == null || c.Joint.connectedBody != Body) continue;
                Destroy(c.Joint);
                c.Joint = null;
                c.ShowSeverCaps();
            }
            Destroy(gameObject);
        }

        public void ShowSeverCaps()
        {
            if (StumpCapOnParent != null) StumpCapOnParent.SetActive(true);
            if (ProximalCap != null) ProximalCap.SetActive(true);
        }

        void BreakWhileGib(in HitInfo hit)
        {
            Vector3 kick = hit.Direction * config.gibSplitImpulse * hit.ForceScale + Vector3.up * config.detachLift * 0.5f;
            if (Joint != null)
            {
                // Split the chain here: this part and its children fly off on their own.
                Destroy(Joint);
                Joint = null;
                ShowSeverCaps();
                Hp = MaxHp * config.gibHpFactor;
                Body.AddForceAtPosition(kick, hit.Point, ForceMode.VelocityChange);
                Body.angularVelocity += Random.insideUnitSphere * config.detachSpin * 0.5f;
                return;
            }

            // Top of its own chunk: it bursts, and anything hanging off it is freed.
            ShowSeverCaps();
            foreach (var c in Children)
            {
                if (c == null || !c.IsGib || c.Joint == null || c.Joint.connectedBody != Body) continue;
                c.Body.AddForce(kick * 0.6f, ForceMode.VelocityChange);
            }
            GibManager.Unregister(this);
            DestroyGib();
        }
    }
}
