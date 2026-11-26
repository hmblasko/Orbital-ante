using System;

namespace OrbitalAnte.Ante
{
    /// <summary>What the ante clock says by ear (critique loop round 1).</summary>
    public enum ClockCue
    {
        None,
        /// <summary>Half a minute left: one soft heads-up.</summary>
        Warn,
        /// <summary>One of the last ten seconds.</summary>
        Tick,
    }

    /// <summary>
    /// The ante clock's sounds (critique loop round 1, 26 Nov 2026). Plain C#, no Unity types.
    ///
    /// Problem: the clock turns red with 30 s left, but it sits in the top-right corner while the player's eyes are
    /// on a fight, so the change is easy to miss and a timer that runs out unseen feels unfair (constitution rule
    /// 5, legible risk). Hearing reaches attention without a visual search. Rules:
    /// - one heads-up at 30 s, then a dry tick each second for the last 10;
    /// - only while the ante is running, its target not yet met, and the clock not on hold;
    /// - never in overtime (overtime has its own alarm and note) and never outside a run (constitution rule 9: no
    ///   timer presses on the player outside play);
    /// - the tick grows a little louder each second, never faster, so it builds without nagging.
    /// </summary>
    public static class ClockCues
    {
        public const float WarnAt = 30f;
        public const int TickFrom = 10;

        /// <summary>
        /// The cue for the clock going from <paramref name="before"/> to <paramref name="now"/> seconds left (one
        /// frame's step). A frame that jumps over several marks plays only the latest.
        /// </summary>
        public static ClockCue Step(float before, float now, bool running, bool targetMet, bool held, bool overtime)
        {
            if (!running || targetMet || held || overtime) return ClockCue.None;
            if (!(now > 0f) || !(before > now)) return ClockCue.None;
            // The readout shows whole seconds rounded up, so a tick is the shown number changing to 10 or below.
            int b = (int)Math.Ceiling(before), n = (int)Math.Ceiling(now);
            if (n < b && n <= TickFrom && n >= 1) return ClockCue.Tick;
            if (before > WarnAt && now <= WarnAt) return ClockCue.Warn;
            return ClockCue.None;
        }

        /// <summary>The tick's volume, 0.55 at ten seconds left rising to 1 at the last second.</summary>
        public static float TickVolume(float secondsLeft)
        {
            float s = Math.Max(1f, Math.Min(TickFrom, (float)Math.Ceiling(secondsLeft)));
            return 0.55f + 0.45f * (TickFrom - s) / (TickFrom - 1);
        }
    }
}
