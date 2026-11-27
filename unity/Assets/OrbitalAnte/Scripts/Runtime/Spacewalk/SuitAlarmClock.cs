using System;

namespace OrbitalAnte.Spacewalk
{
    /// <summary>How bad the suit's trouble is, least to worst.</summary>
    public enum SuitAlarmLevel { None = 0, Caution = 1, AirLow = 2, AirOut = 3 }

    /// <summary>
    /// When the suit's warning tone sounds (critique loop round 5, 27 Nov 2026). Plain C#, game time.
    /// It used to be one tone when any trouble began and again every 12 s whatever the trouble: air running out
    /// (14 s from death) made no new sound if air was already low, and an empty jet tank beeped for as long as you
    /// stayed out. Now every step up is heard at once, and the gap between tones says how bad it is: a caution is
    /// said once (its words stay on the visor), low air comes back now and then, no air counts the seconds.
    /// </summary>
    public sealed class SuitAlarmClock
    {
        /// <summary>Seconds between tones while air is low (two minutes of air is about four tones).</summary>
        public const float LowEvery = 30f;
        /// <summary>Seconds between tones with no air (the grace is 14 s: about seven tones).</summary>
        public const float OutEvery = 2f;

        SuitAlarmLevel _level;
        float _wait;

        public SuitAlarmLevel Level => _level;

        public static SuitAlarmLevel LevelOf(SuitInputs s)
        {
            if (s.AirOut) return SuitAlarmLevel.AirOut;
            if (s.AirLow) return SuitAlarmLevel.AirLow;
            if (s.BurningAir || s.FuelOut) return SuitAlarmLevel.Caution;
            return SuitAlarmLevel.None;
        }

        /// <summary>Seconds between tones at a level; 0 means it is said once and not again.</summary>
        public static float Every(SuitAlarmLevel level)
        {
            switch (level)
            {
                case SuitAlarmLevel.AirOut: return OutEvery;
                case SuitAlarmLevel.AirLow: return LowEvery;
                default: return 0f;
            }
        }

        /// <summary>How loud a tone is at a level (the worst is the loudest; never silent).</summary>
        public static float Volume(SuitAlarmLevel level) => level == SuitAlarmLevel.AirOut ? 1f : level == SuitAlarmLevel.AirLow ? 0.8f : 0.6f;

        /// <summary>One frame. True when the tone should sound now.</summary>
        public bool Step(float dt, SuitAlarmLevel level, bool alive)
        {
            if (!alive) level = SuitAlarmLevel.None;
            if (level != _level)
            {
                bool worse = level > _level;
                _level = level;
                _wait = Every(level);
                return worse;
            }
            float every = Every(level);
            if (every <= 0f) return false;
            _wait -= Math.Max(0f, dt);
            if (_wait > 0f) return false;
            // A long frame never owes more than one tone.
            _wait = every;
            return true;
        }
    }
}
