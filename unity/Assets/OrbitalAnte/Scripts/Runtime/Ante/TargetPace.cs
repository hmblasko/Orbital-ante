using System;

namespace OrbitalAnte.Ante
{
    public enum PaceStatus { Waiting = 0, Ahead = 1, OnPace = 2, Behind = 3, Met = 4, Overtime = 5 }

    /// <summary>What the target bar says about pace.</summary>
    public struct PaceReading
    {
        public PaceStatus status;
        /// <summary>Where on the bar (0..1) a steady pace would have reached by now: the tick.</summary>
        public float expectedShare;
        /// <summary>Points a minute still needed to make the target on time (0 once met).</summary>
        public int neededPerMinute;
        /// <summary>Points still to earn for the target (0 once met).</summary>
        public int remaining;
    }

    /// <summary>
    /// The target bar's pace (21 Nov 2026, fun pass; goal gradient, Kivetz et al. 2006: effort rises as a goal is seen
    /// to be near, so the bar shows exactly how near, never padded with a head start). A tick marks where a steady
    /// pace through the ante would be by now; the words say only what helps: how many points a minute are still
    /// needed when the player is behind, nothing when they are ahead or on pace. Plain C#.
    /// </summary>
    public static class TargetPace
    {
        /// <summary>Within this share of the target either side of the tick counts as on pace.</summary>
        public const float Slack = 0.08f;
        /// <summary>No call is made in the first seconds of an ante: nobody is behind yet.</summary>
        public const float SettleSeconds = 30f;

        /// <param name="anteSeconds">The ante's full clock (after the hazard, plus stopwatch time added).</param>
        /// <param name="held">The clock is waiting (ante 1 on the pad).</param>
        public static PaceReading Read(int earned, int target, float secondsLeft, float anteSeconds, bool inOvertime, bool held)
        {
            var r = new PaceReading();
            if (target <= 0) { r.status = PaceStatus.Met; return r; }
            float share = Math.Min(1f, Math.Max(0f, earned / (float)target));
            if (earned >= target) { r.status = PaceStatus.Met; r.expectedShare = 1f; return r; }
            if (inOvertime)
            {
                r.status = PaceStatus.Overtime;
                r.expectedShare = 1f;
                r.remaining = target - earned;
                return r;
            }
            r.remaining = target - earned;
            float total = Math.Max(Math.Max(1f, anteSeconds), secondsLeft);
            float used = total - Math.Max(0f, secondsLeft);
            r.expectedShare = Math.Min(1f, Math.Max(0f, used / total));
            if (held || used < SettleSeconds) { r.status = PaceStatus.Waiting; return r; }
            float minutesLeft = Math.Max(1f / 60f, secondsLeft / 60f);
            r.neededPerMinute = (int)Math.Ceiling((target - earned) / minutesLeft / 10.0) * 10;
            if (share >= r.expectedShare + Slack) r.status = PaceStatus.Ahead;
            else if (share >= r.expectedShare - Slack) r.status = PaceStatus.OnPace;
            else r.status = PaceStatus.Behind;
            return r;
        }

        /// <summary>
        /// The words beside the bar: "260/MIN BEATS THE CLOCK" when behind, "150 TO GO" in overtime, otherwise empty. Builds a
        /// string, so the readout calls it only when the reading's numbers change (Deck review, 21 Nov 2026).
        /// </summary>
        public static string Words(PaceReading r)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            // Not "NEED": with overtime, missing the clock is not a fail (design review, 21 Nov 2026).
            if (r.status == PaceStatus.Behind) return r.neededPerMinute.ToString("N0", inv) + "/MIN BEATS THE CLOCK";
            if (r.status == PaceStatus.Overtime) return r.remaining.ToString("N0", inv) + " TO GO";
            return string.Empty;
        }

        /// <summary>True when two readings would show the same words.</summary>
        public static bool SameWords(PaceReading a, PaceReading b)
        {
            if (a.status != b.status) return false;
            if (a.status == PaceStatus.Behind) return a.neededPerMinute == b.neededPerMinute;
            if (a.status == PaceStatus.Overtime) return a.remaining == b.remaining;
            return true;
        }
    }
}
