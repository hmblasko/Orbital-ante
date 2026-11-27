namespace OrbitalAnte.UI
{
    /// <summary>
    /// When a page that opened with nothing selected (the death page, the ante break, a page of choices) selects
    /// its default by itself (critique loop round 2, 26 Nov 2026). Plain C#, no Unity types.
    ///
    /// Problem: after dying, the page held input for a moment (so a button still held from the crash cannot skip
    /// it), and then waited for one press only to put the cursor somewhere, and a second to pick: two presses and
    /// no visible cursor between a failure and the next try. The time from failing to trying again is where a
    /// player decides whether to have another go, so it should be one press. Rule: once the hold is over and the
    /// confirm button has been up for <see cref="Quiet"/> seconds in a row, the default is selected. A button held
    /// or mashed through the hold therefore still picks nothing: the cursor only appears once the hands are still.
    /// </summary>
    public sealed class SelectionHold
    {
        /// <summary>Seconds the confirm button must have been up, in a row, before the default is selected.</summary>
        public const float Quiet = 0.25f;

        /// <summary>The most one frame may count for.</summary>
        public const float MaxStep = 0.05f;

        float _left, _quiet;
        bool _waiting;

        /// <summary>True from <see cref="Start"/> until the default has been handed over.</summary>
        public bool Waiting => _waiting;

        /// <summary>A page has opened with nothing selected, to stay so for at least <paramref name="hold"/> seconds.</summary>
        public void Start(float hold)
        {
            _left = hold > 0f ? hold : 0f;
            _quiet = 0f;
            _waiting = true;
        }

        /// <summary>The page was left, or something was selected another way.</summary>
        public void Cancel() => _waiting = false;

        /// <summary>
        /// One frame of unscaled time. True on the one frame the default should be selected.
        /// </summary>
        public bool Step(float dt, bool confirmHeld)
        {
            if (!_waiting || !(dt > 0f)) return false;
            // A slow frame as the page opens (a save, building the page) must not eat the hold in one lump.
            if (dt > MaxStep) dt = MaxStep;
            if (_left > 0f) _left -= dt;
            _quiet = confirmHeld ? 0f : _quiet + dt;
            if (_left > 0f || _quiet < Quiet) return false;
            _waiting = false;
            return true;
        }
    }
}
