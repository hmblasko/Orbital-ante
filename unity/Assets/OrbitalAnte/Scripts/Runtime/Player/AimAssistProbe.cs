using OrbitalAnte.Core;
using OrbitalAnte.Dismemberment;
using UnityEngine;

namespace OrbitalAnte.Player
{
    /// <summary>
    /// Finds how close the crosshair is to a living enemy's head or chest, for <see cref="AimAssist"/>. One line
    /// of sight is cast a frame at most, to the best candidate only, so help never comes through a wall.
    /// </summary>
    public static class AimAssistProbe
    {
        /// <summary>The best <see cref="AimAssist.Closeness"/> from <paramref name="eye"/> this frame, 0 when none.</summary>
        public static float BestCloseness(Transform eye, float gunRange, in AimAssistRules rules)
        {
            if (eye == null) return 0f;
            Vector3 from = eye.position, forward = eye.forward;
            float best = 0f;
            Vector3 bestPoint = default;
            Mannequin bestOwner = null;
            var all = Mannequin.Present;
            float reach = Mathf.Min(gunRange, rules.range);
            for (int i = 0; i < all.Count; i++)
            {
                Mannequin m = all[i];
                // The player's own remains are a Mannequin too (they keep the head clean for the view).
                if (m == null || !m.Alive || m.KeepsHeadClean) continue;
                Vector3 rough = m.transform.position - from;
                if (rough.sqrMagnitude > (reach + 3f) * (reach + 3f)) continue;
                Consider(m, BodyPartId.Head, from, forward, gunRange, rules, ref best, ref bestPoint, ref bestOwner);
                Consider(m, BodyPartId.Torso, from, forward, gunRange, rules, ref best, ref bestPoint, ref bestOwner);
            }
            if (bestOwner == null) return 0f;

            // Only if nothing solid is in the way (the target's own hitboxes, held gun and gibs do not count).
            Vector3 to = bestPoint - from;
            float distance = to.magnitude;
            if (distance > 0.01f && Physics.Raycast(from, to / distance, out RaycastHit hit, distance - 0.05f, Layers.ShootMask, QueryTriggerInteraction.Ignore))
            {
                // The target's own gun hangs from its root, not from a body part, so ask whose it is.
                var who = hit.collider.GetComponentInParent<Mannequin>();
                if (who != bestOwner) return 0f;
            }
            return best;
        }

        static void Consider(Mannequin m, BodyPartId id, Vector3 from, Vector3 forward, float gunRange, in AimAssistRules rules,
            ref float best, ref Vector3 bestPoint, ref Mannequin bestOwner)
        {
            if (!m.Model.OnBody(id)) return;
            BodyPart part = m.Part(id);
            if (part == null || part.Hitbox == null || !part.Hitbox.enabled) return;
            Vector3 point = part.Hitbox.bounds.center;
            Vector3 to = point - from;
            float distance = to.magnitude;
            if (distance < 0.01f) return;
            float degrees = Vector3.Angle(forward, to);
            float c = AimAssist.Closeness(degrees, distance, gunRange, rules);
            if (c > best)
            {
                best = c;
                bestPoint = point;
                bestOwner = m;
            }
        }
    }
}
