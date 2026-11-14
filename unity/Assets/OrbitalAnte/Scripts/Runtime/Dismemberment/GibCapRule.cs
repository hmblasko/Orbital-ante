using System;
using System.Collections.Generic;

namespace OrbitalAnte.Dismemberment
{
    /// <summary>
    /// The loose-chunk cap, as a plain rule with no Unity types (tested in GibCapRuleTests).
    ///
    /// <c>cap</c> is the most live chunks at once, fading ones included, because a fading chunk still has a
    /// rigidbody, a collider and a renderer. To keep overflow graceful, the oldest solid chunk starts its quick
    /// fade once the solid ones fill all but <c>fadeSlots</c> of the cap. Only if the cap is still full when a new
    /// chunk arrives is one removed at once: the one furthest through its fade, or the oldest if none is fading.
    /// </summary>
    public static class GibCapRule
    {
        /// <summary>One live chunk, in spawn order (index 0 is the oldest).</summary>
        public struct Slot
        {
            public bool Fading;
            /// <summary>0 when the fade starts, 1 when it ends. Ignored while not fading.</summary>
            public float FadeProgress;
        }

        /// <summary>
        /// Call before adding a chunk. <paramref name="removeNow"/> must remove that index from
        /// <paramref name="slots"/>; <paramref name="beginFade"/> must mark that index as fading.
        /// <paramref name="keep"/> is the index of a chunk that must survive this call (the chunk the new one is
        /// splitting off from, whose parts are about to move), or -1. Afterwards there is room for one more chunk
        /// within the cap, except when the cap is 1 and the only chunk is <paramref name="keep"/>.
        /// </summary>
        public static void MakeRoom(IReadOnlyList<Slot> slots, int cap, int fadeSlots, int keep, Action<int> removeNow, Action<int> beginFade)
        {
            cap = Math.Max(1, cap);
            fadeSlots = Math.Max(0, Math.Min(fadeSlots, cap - 1));
            if (keep >= slots.Count) keep = -1;

            // Hard limit: the new chunk must fit.
            while (slots.Count >= cap)
            {
                int pick = PickToRemove(slots, keep);
                if (pick < 0) break;
                removeNow(pick);
                if (keep > pick) keep--;
            }

            // Graceful overflow: keep solid chunks (counting the new one) at or below cap - fadeSlots.
            int solid = 1;
            for (int i = 0; i < slots.Count; i++) if (!slots[i].Fading) solid++;
            int solidLimit = cap - fadeSlots;
            for (int i = 0; i < slots.Count && solid > solidLimit; i++)
            {
                if (slots[i].Fading || i == keep) continue;
                beginFade(i);
                solid--;
            }
        }

        /// <summary>
        /// The chunk to remove at once: furthest through its fade, else the oldest. Never <paramref name="keep"/>.
        /// -1 if there is nothing else to remove.
        /// </summary>
        public static int PickToRemove(IReadOnlyList<Slot> slots, int keep = -1)
        {
            int best = -1;
            float bestProgress = -1f;
            for (int i = 0; i < slots.Count; i++)
            {
                if (i == keep) continue;
                if (slots[i].Fading && slots[i].FadeProgress > bestProgress)
                {
                    best = i;
                    bestProgress = slots[i].FadeProgress;
                }
            }
            if (best >= 0) return best;
            for (int i = 0; i < slots.Count; i++) if (i != keep) return i;
            return -1;
        }
    }
}
