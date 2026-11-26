using System;

namespace OrbitalAnte.Ante
{
    /// <summary>What the ante clock says by ear (critique loop round 1).</summary>
    public enum ClockCue
    {
        None,
        /// <summary>A minute left: one soft heads-up, while there is still time to do something about it.</summary>
        Warn,
        /// <summary>One of the last five seconds.</summary>
        Tick,
    }

    /// <summary>
    /// The ante clock's sounds (critique loop round 1, 26 Nov 2026). Plain C#, no Unity types.
    ///
    /// Problem: the clock turns red with 30 s left, but it sits in the top-right corner while the player's eyes are
    /// on a fight, so the change is easy to miss and a timer that runs out unseen feels unfair (constitution rule
    /// 5, legible risk). Hearing reaches attention without a visual search. Rules:
    /// - one heads-up with a minute left (the same moment as the guide's "hurry up" tip): a warning is only worth
    ///   giving while the player can still act on it;
    /// - a dry tick for each of the last five seconds, at one steady volume. Running out only starts overtime, so
    ///   the countdown must not sound like a bomb: no rising volume, no quickening, and only five;
    /// - no ticks in the pilot's seat, where nothing the player can do in five seconds earns a point;
    /// - only while the ante is running, its target not yet met, and the clock not on hold;
    /// - never in overtime (which has its own sound) and never outside a run (constitution rule 9: no timer
    ///   presses on the player outside play).
    /// </summary>
    public static class ClockCues
    {
        public const float WarnAt = 60f;
        public const int TickFrom = 5;
        public const float TickVolume = 0.8f;

        /// <summary>
        /// The cue for the clock going from <paramref name="before"/> to <paramref name="now"/> seconds left (one
        /// frame's step). A frame that jumps over several marks plays only the latest.
        /// </summary>
        public static ClockCue Step(float before, float now, bool running, bool targetMet, bool held, bool overtime, bool seated = false)
        {
            if (!running || targetMet || held || overtime) return ClockCue.None;
            if (!(now > 0f) || !(before > now)) return ClockCue.None;
            // The readout shows whole seconds rounded up, so a tick is the shown number changing to 5 or below.
            int b = (int)Math.Ceiling(before), n = (int)Math.Ceiling(now);
            if (n < b && n <= TickFrom && n >= 1) return seated ? ClockCue.None : ClockCue.Tick;
            if (before > WarnAt && now <= WarnAt) return ClockCue.Warn;
            return ClockCue.None;
        }
    }
}
