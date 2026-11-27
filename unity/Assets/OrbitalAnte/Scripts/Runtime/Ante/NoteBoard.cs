namespace OrbitalAnte.Ante
{
    /// <summary>
    /// The one line of notes on the ante readout (critique loop round 1, 26 Nov 2026). Plain C#, no Unity types.
    ///
    /// Problem: every note wrote straight over the last one, so the note that matters most in an ante, TARGET MET
    /// with what to do next, could be wiped a moment later by "supply drop coming". An instruction the player
    /// never got to read is a hidden rule (constitution rule 3). Rule: an important note is kept on screen for
    /// <see cref="HoldSeconds"/> whatever arrives; a plain note that arrives meanwhile waits its turn (only the
    /// latest one waits, older ones are dropped: stale news is noise) and is shown when the hold ends.
    /// </summary>
    public sealed class NoteBoard
    {
        /// <summary>Seconds an important note cannot be replaced by a plain one.</summary>
        public const float HoldSeconds = 3f;
        /// <summary>Seconds a plain note shows, and an important one in all.</summary>
        public const float PlainSeconds = 3.5f, ImportantSeconds = 5f;

        string _waiting;
        float _held;

        /// <summary>The note showing now, or null.</summary>
        public string Text { get; private set; }
        /// <summary>Seconds the showing note has left.</summary>
        public float Left { get; private set; }
        /// <summary>An important note is still in its hold: anything offered now waits, or (if important) cuts it short.</summary>
        public bool Holding => _held > 0f;

        public void Clear()
        {
            Text = null;
            _waiting = null;
            Left = 0f;
            _held = 0f;
        }

        /// <summary>A new note. An important one always shows at once; a plain one waits out an important one's hold.</summary>
        public void Offer(string text, bool important)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!important && _held > 0f)
            {
                _waiting = text;
                return;
            }
            Text = text;
            Left = important ? ImportantSeconds : PlainSeconds;
            _held = important ? HoldSeconds : 0f;
            if (important) _waiting = null;
        }

        /// <summary>Moves time on. True when the text or whether it shows has changed.</summary>
        public bool Tick(float dt)
        {
            if (!(dt > 0f)) return false;
            if (_held > 0f) _held -= dt;
            if (_held <= 0f && _waiting != null)
            {
                Text = _waiting;
                _waiting = null;
                Left = PlainSeconds;
                return true;
            }
            if (Left <= 0f) return false;
            Left -= dt;
            if (Left > 0f) return false;
            Text = null;
            return true;
        }
    }
}
