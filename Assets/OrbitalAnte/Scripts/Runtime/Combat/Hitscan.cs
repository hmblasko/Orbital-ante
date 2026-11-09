using UnityEngine;

namespace OrbitalAnte.Combat
{
    public static class Hitscan
    {
        static readonly RaycastHit[] buffer = new RaycastHit[48];

        /// <summary>
        /// Nearest thing a bullet would hit. Attached body parts are trigger hitboxes, so
        /// triggers are queried, but only body-part triggers count. Character capsules are
        /// skipped so shots reach the hitboxes inside them. Held weapons are trigger targets too.
        /// </summary>
        public static bool Cast(Ray ray, float range, Transform ignoreRoot, out RaycastHit best)
        {
            best = default;
            Physics.SyncTransforms(); // hitboxes follow animated bones; make sure physics sees this frame's pose
            int n = Physics.RaycastNonAlloc(ray, buffer, range, ~0, QueryTriggerInteraction.Collide);
            float bestDistance = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = buffer[i];
                var c = h.collider;
                if (c == null) continue;
                if (ignoreRoot != null && c.transform.IsChildOf(ignoreRoot)) continue;
                if (c is CharacterController) continue;
                if (c.isTrigger && c.GetComponent<IShootable>() == null) continue;
                if (h.distance < bestDistance)
                {
                    bestDistance = h.distance;
                    best = h;
                    found = true;
                }
            }
            return found;
        }
    }
}
