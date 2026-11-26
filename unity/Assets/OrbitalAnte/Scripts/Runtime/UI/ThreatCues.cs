using System;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// What the visor shows when an enemy has just picked the player out, before it shoots (critique loop round 1,
    /// 26 Nov 2026). Plain C#, no Unity types.
    ///
    /// Problem: every armed enemy already waits a beat between spotting the player and firing (its reaction time),
    /// but nothing showed that beat, so being seen felt like being shot out of nowhere and sneaking could not be
    /// learned (constitution rules 3 and 5). Two cues make the beat readable:
    /// - an amber arc on a ring just outside the red damage ring, pointing at the enemy, held for the beat and
    ///   then fading, so "amber is about to be red" is learned in a fight or two;
    /// - the suit's threat sensor sound ("enemy.spotted"), panned toward the enemy, but only when a quiet stretch
    ///   ends (nobody had the player in sight for <see cref="QuietBeforeSting"/> seconds) and never more often
    ///   than <see cref="StingGap"/>. In a running fight new arcs still show, but the sound stays out of it: it
    ///   is news only when the player was hidden.
    /// </summary>
    public static class ThreatCues
    {
        /// <summary>Seconds nobody may have had the player in sight before a sighting plays the sting.</summary>
        public const float QuietBeforeSting = 6f;
        /// <summary>Least seconds between two stings.</summary>
        public const float StingGap = 4f;
        /// <summary>Most warning arcs on screen at once; a new sighting takes the oldest's place.</summary>
        public const int MaxMarkers = 3;
        /// <summary>The shortest and longest the arc is held at full strength, whatever the enemy's reaction time.</summary>
        public const float LeastHold = 0.35f, MostHold = 1.6f;
        /// <summary>Seconds the arc takes to fade after the hold.</summary>
        public const float FadeSeconds = 0.45f;

        /// <summary>
        /// True when this sighting should play the sting: the player had gone unseen for long enough, and the last
        /// sting was long enough ago. <paramref name="unseenFor"/> is seconds since any enemy last had the player
        /// in sight, measured before this sighting.
        /// </summary>
        public static bool Sting(float unseenFor, float sinceLastSting)
        {
            return unseenFor >= QuietBeforeSting && sinceLastSting >= StingGap;
        }

        /// <summary>How long the arc is held at full strength for an enemy that fires after <paramref name="beat"/> seconds.</summary>
        public static float Hold(float beat)
        {
            if (!(beat > 0f)) return LeastHold;
            return Math.Max(LeastHold, Math.Min(MostHold, beat));
        }

        /// <summary>Total seconds an arc is on screen.</summary>
        public static float Seconds(float beat) => Hold(beat) + FadeSeconds;

        /// <summary>
        /// The arc's strength, 0 to 1, by its age. While held it pulses gently (never below 0.65), so it reads as a
        /// warning, not a wound; then it fades.
        /// </summary>
        public static float Alpha(float age, float beat)
        {
            float hold = Hold(beat);
            if (!(age >= 0f) || age >= hold + FadeSeconds) return 0f;
            if (age <= hold) return 0.825f + 0.175f * (float)Math.Cos(age * Math.PI * 2.0 * 3.0);
            float t = (age - hold) / FadeSeconds;
            return 0.65f * (1f - t) * (1f - t);
        }

        /// <summary>
        /// Two sightings from about the same way (within this many degrees) share one arc, so a squad seen together
        /// is one warning, not a fan of them.
        /// </summary>
        public const float SameWayDegrees = 25f;
    }
}
