using System;

namespace OrbitalAnte.Look
{
    /// <summary>
    /// Which copy of a model to draw: the full one, or the far copy (a file of the same name with "Far" on the
    /// end, about a fifth of the triangles, made by the same Blender script). Plain numbers, no engine.
    ///
    /// A thing goes over to its far copy a little beyond its range and comes back a little inside it, so one
    /// standing at the edge does not flicker between the two. Bodies are also counted: only a few are ever
    /// drawn in full, however many stand close, so that the worst frame of a big fight has a known size
    /// (docs/performance_budget.md; an estimate until it is measured on the Deck). The places go to the
    /// nearest, and to bodies in front of the player before ones behind, which are not drawn at all.
    /// </summary>
    public static class DetailRule
    {
        /// <summary>
        /// Metres at which an ordinary-sized body goes over to its far copy: about 36 pixels tall on the Deck's
        /// screen at the game's 80 degrees of view. Chosen to lie beyond where the common enemy stands to fight
        /// (18 m), so nobody stands on the line changing back and forth as the player steps about.
        /// </summary>
        public const float BodyRange = 24f;
        /// <summary>Metres at which a gun in the world goes over to its far copy. Smaller than a body, so sooner.</summary>
        public const float GunRange = 9f;
        /// <summary>The share of its range a thing must cross, either way, before it changes.</summary>
        public const float Slack = 0.12f;
        /// <summary>
        /// The most bodies drawn in full at once, unless the Look asset says otherwise (docs/performance_budget.md,
        /// "Enemies drawn, full and far copies"). Twelve is how many enemies there were at once before far
        /// copies existed, all full: so nothing close looks any simpler than it did, however many more arrive.
        /// </summary>
        public const int FullBodies = 12;
        /// <summary>
        /// The most triangles of full bodies drawn at once, unless the Look asset says otherwise. Twelve of the
        /// first enemy model (14,848 triangles each) came to 178,000, and that is the size of fight the budget
        /// was written for. A finer model costs more, so fewer of it are full at once: eight of the realistic
        /// trooper (21,176). Only pieces that can be swapped count (not the wound), and a body that has lost a limb
        /// counts only what it still holds. This holds the triangles of full bodies under 180,000; it does nothing about how
        /// small they are on screen, which <see cref="RangeFor"/> looks after.
        /// </summary>
        public const int FullTriangles = 180000;
        /// <summary>
        /// The most triangles a body can have and still be drawn in full out to the whole of its range: the cap
        /// for every kind but the trooper. A finer body goes over to its far copy sooner (<see cref="RangeFor"/>).
        /// </summary>
        public const int RangeTriangles = 16000;

        /// <summary>
        /// The range for a body of <paramref name="triangles"/> triangles. A body of up to
        /// <see cref="RangeTriangles"/> keeps <paramref name="range"/>. A finer one changes sooner, so that where it
        /// changes its triangles are no smaller on screen than those of a body at the cap where that one changes:
        /// the area a body covers falls with the square of distance, so the range shrinks with the square root of
        /// the extra triangles. The realistic trooper (21,176) changes at 20.9 m instead of 24. Graphics cards pay
        /// for triangles smaller than a pixel several times over, and a count of triangles does not see that.
        /// </summary>
        public static float RangeFor(float range, int triangles)
        {
            if (!(range > 0f) || triangles <= RangeTriangles) return range;
            return range * (float)Math.Sqrt(RangeTriangles / (double)triangles);
        }
        /// <summary>A body out of the player's view claims a full place as if it stood this many times further off.</summary>
        public const float Yield = 3f;
        /// <summary>Nearer than this a body always counts as in view: at arm's length the angle to its feet says little.</summary>
        public const float CloseBy = 3f;
        /// <summary>The cosine of 70 degrees: further than that from where the player looks is out of view (the corner of the screen is 58).</summary>
        public const float ViewEdge = 0.342f;

        /// <summary>
        /// True if a thing is out of the player's view. <paramref name="ahead"/> is how far in front of the eye
        /// it is (its offset measured along the way the eye looks; negative behind).
        /// </summary>
        public static bool Unseen(float ahead, float distance)
        {
            if (float.IsNaN(ahead) || float.IsNaN(distance)) return false;
            return distance > CloseBy && ahead < distance * ViewEdge;
        }

        /// <summary>By distance alone. A range of zero or less means "always the full copy".</summary>
        public static bool Far(float distance, float range, bool wasFar)
        {
            if (!(range > 0f)) return false;
            if (float.IsNaN(distance)) return wasFar;
            return distance > range * (wasFar ? 1f - Slack : 1f + Slack);
        }

        /// <summary>
        /// Decides for everything at once. <paramref name="far"/> holds what each was drawn as last time and is
        /// rewritten with what each should be drawn as now. Of the things that are <paramref name="counted"/>,
        /// at most <paramref name="fullCap"/> stay full: the nearest, with one already full keeping its place
        /// against one a little nearer, so two at the same distance do not swap every time, and with one that is
        /// <paramref name="unseen"/> (may be null: all seen) giving way to one in view.
        /// <paramref name="scratch"/> is working room at least <paramref name="count"/> long; nothing is allocated.
        /// </summary>
        public static void Pick(float[] distance, float[] range, bool[] counted, bool[] unseen, bool[] far, float[] scratch, int count, int fullCap)
        {
            Pick(distance, range, counted, unseen, far, scratch, count, fullCap, null, 0f);
        }

        /// <summary>
        /// As above, and the full ones are also held to a size: <paramref name="cost"/> is what each thing costs
        /// to draw in full (its triangles) and the counted full ones may not cost more than
        /// <paramref name="costCap"/> between them. The weakest claim gives way first, as with the count.
        /// No list of costs, or a cap of zero or less, means no limit by size.
        /// </summary>
        public static void Pick(float[] distance, float[] range, bool[] counted, bool[] unseen, bool[] far, float[] scratch, int count, int fullCap,
                                float[] cost, float costCap)
        {
            if (distance == null || range == null || counted == null || far == null || scratch == null) return;
            count = Math.Min(count, Math.Min(Math.Min(distance.Length, range.Length), Math.Min(Math.Min(counted.Length, far.Length), scratch.Length)));
            if (unseen != null && unseen.Length < count) unseen = null;
            if (cost != null && (cost.Length < count || !(costCap > 0f))) cost = null;
            int full = 0;
            float spent = 0f;
            for (int i = 0; i < count; i++)
            {
                bool was = far[i];
                far[i] = Far(distance[i], range[i], was);
                // How weak its claim to a full copy is: further is weaker, and so is not having one already,
                // and so is standing where the player is not looking.
                float d = float.IsNaN(distance[i]) ? float.MaxValue : distance[i];
                if (was) d *= 1f + Slack;
                if (unseen != null && unseen[i]) d *= Yield;
                scratch[i] = d;
                if (counted[i] && !far[i])
                {
                    full++;
                    if (cost != null && cost[i] > 0f) spent += cost[i];
                }
            }
            fullCap = Math.Max(0, fullCap);
            while (full > fullCap || (cost != null && spent > costCap))
            {
                int weakest = -1;
                for (int i = 0; i < count; i++)
                {
                    if (!counted[i] || far[i]) continue;
                    if (weakest < 0 || scratch[i] > scratch[weakest]) weakest = i;
                }
                if (weakest < 0) break;
                far[weakest] = true;
                full--;
                if (cost != null && cost[weakest] > 0f) spent -= cost[weakest];
            }
        }
    }
}
