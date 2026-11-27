using System;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// The player's own pulse, heard while the suit is on its last plates (critique loop round 1, 26 Nov 2026).
    /// Plain C#, no Unity types.
    ///
    /// Problem: how close the player is to dying was told only by sight (the plate gauge in a lower corner, the
    /// cracks at the visor's edge), and in a fight the eyes are on the enemy. The armour mends by itself out of
    /// danger, so the useful message is "get behind something now", and it has to arrive without a glance.
    /// Rule: a soft pulse starts at or below <see cref="StartAt"/> of the armour and stops at or above
    /// <see cref="StopAt"/> (two numbers, so it never flutters on the line). It quickens and firms a little as the
    /// armour falls, never past a resting-to-brisk heart rate, so it informs and does not nag. Taking cover is
    /// answered by the sound stopping: the relief is the reward, and nothing is added to play it.
    /// </summary>
    public sealed class PulseClock
    {
        public const float StartAt = 0.2f;
        public const float StopAt = 0.3f;
        /// <summary>Seconds between beats at <see cref="StartAt"/> and with no armour left (57 and 80 a minute).</summary>
        public const float SlowBeat = 1.05f, FastBeat = 0.75f;

        float _untilBeat;

        /// <summary>True while the pulse is sounding.</summary>
        public bool On { get; private set; }

        /// <summary>
        /// Moves the pulse on by <paramref name="dt"/> seconds of game time (so it stops with a pause and slows
        /// with Focus). True on the frame a beat should be played.
        /// </summary>
        public bool Step(float dt, float fraction, bool alive)
        {
            if (!alive || !(fraction >= 0f) || fraction >= StopAt || (!On && fraction > StartAt))
            {
                On = false;
                _untilBeat = 0f;
                return false;
            }
            On = true;
            _untilBeat -= Math.Max(0f, dt);
            if (_untilBeat > 0f) return false;
            _untilBeat = Interval(fraction);
            return true;
        }

        /// <summary>Seconds to the next beat at this much armour.</summary>
        public static float Interval(float fraction)
        {
            float t = Math.Max(0f, Math.Min(1f, fraction / StartAt));
            return FastBeat + (SlowBeat - FastBeat) * t;
        }

        /// <summary>A beat's volume: 0.6 where the pulse starts, 1 with no armour left.</summary>
        public static float Volume(float fraction)
        {
            float t = Math.Max(0f, Math.Min(1f, fraction / StartAt));
            return 1f - 0.4f * t;
        }
    }
}
