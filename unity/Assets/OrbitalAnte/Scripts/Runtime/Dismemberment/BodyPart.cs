using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Dismemberment
{
    /// <summary>
    /// One breakable piece of a character: its own hitbox, its own hit points (kept in the BodyModel),
    /// a wound cap on its own joint and one wound cap for each part that can come off it.
    /// </summary>
    public sealed class BodyPart : MonoBehaviour, IHittable
    {
        public BodyPartId Id { get; private set; }
        public Mannequin Owner { get; private set; }
        public Collider Hitbox { get; private set; }
        public float Mass { get; private set; }

        /// <summary>Shown on this part where it used to join its parent.</summary>
        public GameObject ProximalCap { get; private set; }

        /// <summary>Shown on this part where a child used to join it. Indexed by the child's BodyPartId.</summary>
        readonly GameObject[] _childCaps = new GameObject[BodyLayout.Count];

        public void Setup(BodyPartId id, Mannequin owner, Collider hitbox, float mass, GameObject proximalCap)
        {
            Id = id;
            Owner = owner;
            Hitbox = hitbox;
            Mass = mass;
            ProximalCap = proximalCap;
            if (proximalCap != null) proximalCap.SetActive(false);
        }

        public void SetChildCap(BodyPartId child, GameObject cap)
        {
            _childCaps[(int)child] = cap;
            if (cap != null) cap.SetActive(false);
        }

        public GameObject ChildCap(BodyPartId child) => _childCaps[(int)child];

        public HitOutcome Hit(in HitInfo hit)
        {
            if (Owner == null) return default;
            return Owner.ApplyHit(this, hit);
        }

        public string DescribeForDebug()
        {
            if (Owner == null) return BodyLayout.DisplayName(Id);
            var model = Owner.Model;
            string where = model.OnBody(Id) ? string.Empty : " (loose)";
            return BodyLayout.DisplayName(Id) + where + "  " +
                   Mathf.CeilToInt(model.Hp(Id)) + " / " + Mathf.CeilToInt(model.MaxHp(Id));
        }
    }
}
