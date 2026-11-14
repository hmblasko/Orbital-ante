using UnityEngine;

namespace OrbitalAnte.Core
{
    /// <summary>
    /// The one place game speed is set. Nothing else may write Time.timeScale or Time.fixedDeltaTime.
    ///
    /// Each thing that wants to change the speed of time hands in its own number here (the pause menu, hitstop,
    /// the headshot slow-mo, Deep Breath) and this class puts them together with <see cref="TimeBlend"/>, so
    /// they can never fight over the clock or leave it stuck when one of them ends.
    /// </summary>
    public static class TimeFlow
    {
        static bool _paused;
        static float _hitstop = 1f, _headshot = 1f, _breath = 1f;
        // The physics step the project runs at when nothing is slowing, and the last value written here.
        static float _normalStep = -1f, _wroteStep = -1f;

        public static bool Paused
        {
            get => _paused;
            set { if (_paused != value) { _paused = value; Apply(); } }
        }

        /// <summary>Speed during a hitstop freeze; 1 when there is none.</summary>
        public static float Hitstop
        {
            get => _hitstop;
            set { if (_hitstop != value) { _hitstop = value; Apply(); } }
        }

        /// <summary>Speed during the slow-mo after a headshot kill; 1 when there is none.</summary>
        public static float Headshot
        {
            get => _headshot;
            set { if (_headshot != value) { _headshot = value; Apply(); } }
        }

        /// <summary>Speed while a deep breath is held; 1 when there is none.</summary>
        public static float Breath
        {
            get => _breath;
            set { if (_breath != value) { _breath = value; Apply(); } }
        }

        /// <summary>
        /// The time step for things the player does with their own body that must not slow with the world
        /// (turning the view with a stick). At normal speed this is just <paramref name="scaledDt"/>.
        /// </summary>
        public static float PlayerDelta(float scaledDt)
        {
            float scale = Time.timeScale;
            if (scale <= 0f || scale >= 0.999f) return scaledDt;
            // Real time, but never more than the longest frame the game allows, so a hitch cannot spin the view.
            return Mathf.Min(Time.unscaledDeltaTime, Time.maximumDeltaTime);
        }

        /// <summary>Puts the numbers together and writes the clock. Safe to call at any time.</summary>
        public static void Apply()
        {
            float scale = TimeBlend.Scale(_paused, _hitstop, _headshot, _breath);
            if (Time.timeScale != scale) Time.timeScale = scale;

            // If the step is not what was last written here, something else set it (project settings, a test):
            // that is the new normal.
            float stepNow = Time.fixedDeltaTime;
            if (_normalStep <= 0f || Mathf.Abs(stepNow - _wroteStep) > _wroteStep * 0.0005f) _normalStep = stepNow;
            float step = TimeBlend.FixedStep(_normalStep, _headshot, _breath);
            if (Mathf.Abs(stepNow - step) > step * 0.0005f) Time.fixedDeltaTime = step;
            _wroteStep = step;
        }

        /// <summary>Everything back to normal speed (start of a Play session, and after tests).</summary>
        public static void Reset()
        {
            _paused = false;
            _hitstop = _headshot = _breath = 1f;
            Apply();
        }
    }
}
