using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Player
{
    /// <summary>Allocation-free solid clearance for the arena and the outside suit, never the cabin's convex hull.</summary>
    public sealed class CameraClearance
    {
        const int ClearanceMask = Layers.WorldMask | (1 << Layers.CharacterBody);
        readonly Collider[] _overlaps = new Collider[32];
        readonly RaycastHit[] _hits = new RaycastHit[32];

        static bool Ours(Collider collider, Transform owner) => collider != null && owner != null &&
            (collider.transform == owner || collider.transform.IsChildOf(owner));

        public static float Radius(Camera eye, float headRadius)
        {
            float near = eye.nearClipPlane;
            float halfHeight = near * Mathf.Tan(eye.fieldOfView * 0.5f * Mathf.Deg2Rad);
            // A sphere at the eye encloses all four near-plane corners, including when leaning/rolling.
            float corners = Mathf.Sqrt(near * near + halfHeight * halfHeight * (1f + eye.aspect * eye.aspect));
            return Mathf.Max(StanceRules.NonNegative(headRadius), corners) + Physics.defaultContactOffset;
        }

        public bool CanOccupy(Vector3 feet, Vector3 up, float height, float radius, Transform owner)
        {
            // A contact-sized inset keeps the existing floor/wall contacts from vetoing a stand forever.
            float queryRadius = Mathf.Max(radius * 0.5f, radius - Physics.defaultContactOffset);
            int count = Physics.OverlapCapsuleNonAlloc(feet + up * radius, feet + up * (height - radius),
                queryRadius, _overlaps, ClearanceMask, QueryTriggerInteraction.Ignore);
            if (count == _overlaps.Length) return false;
            for (int i = 0; i < count; i++) if (!Ours(_overlaps[i], owner)) return false;
            return true;
        }

        public Vector3 Clip(Vector3 from, Vector3 desired, float radius, Transform owner)
        {
            int count = Physics.OverlapSphereNonAlloc(from, radius, _overlaps, ClearanceMask, QueryTriggerInteraction.Ignore);
            if (count == _overlaps.Length) return from;
            for (int i = 0; i < count; i++) if (!Ours(_overlaps[i], owner)) return from;
            Vector3 travel = desired - from;
            float distance = travel.magnitude;
            if (distance <= 0.00001f) return from;
            int hits = Physics.SphereCastNonAlloc(from, radius, travel / distance, _hits, distance,
                ClearanceMask, QueryTriggerInteraction.Ignore);
            if (hits == _hits.Length) return from;
            float allowed = distance;
            for (int i = 0; i < hits; i++)
            {
                if (Ours(_hits[i].collider, owner)) continue;
                allowed = Mathf.Min(allowed, Mathf.Max(0f, _hits[i].distance - Physics.defaultContactOffset));
            }
            return from + travel * (allowed / distance);
        }
    }
}
