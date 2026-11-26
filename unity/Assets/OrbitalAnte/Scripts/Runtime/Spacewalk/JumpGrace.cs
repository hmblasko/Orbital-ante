namespace OrbitalAnte.Spacewalk
{
    /// <summary>
    /// A little forgiveness for the jump on foot (critique loop round 1, 26 Nov 2026). Plain C#, no Unity types.
    ///
    /// Problem: the suit only jumped if Jump went down on the very physics step its feet were found on the ground.
    /// A press a moment before landing, or a moment after stepping off an edge, did nothing (off the ground Jump is
    /// the jets, so the press became a short jet burst instead). On rough ground the feet leave and touch it many
    /// times a second, so jumps were eaten at random. When a control sometimes ignores the player they stop
    /// trusting it and blame themselves (competence, the strongest predictor of playing on). Two standard graces:
    /// - <see cref="BufferSeconds"/>: a press is kept that long and used on the first step with ground;
    /// - <see cref="CoyoteSeconds"/>: ground counts for that long after the feet leave it, so a jump off an edge
    ///   still goes, but only once, and never again until the feet have touched down after it.
    /// Both are short enough that nobody can feel them as anything but "it did what I pressed".
    /// </summary>
    public sealed class JumpGrace
    {
        public const float BufferSeconds = 0.12f;
        public const float CoyoteSeconds = 0.12f;
        /// <summary>After a jump, ground found this soon is the jump's own take-off step, not a landing.</summary>
        public const float TakeOffSeconds = 0.2f;

        float _pressedAt = float.NegativeInfinity;
        float _groundAt = float.NegativeInfinity;
        float _jumpedAt = float.NegativeInfinity;

        /// <summary>Jump went down at <paramref name="now"/> (and the stance allows a jump).</summary>
        public void Press(float now) => _pressedAt = now;

        /// <summary>Forgets a kept press and the ground (seated, dead, floating free, a new run).</summary>
        public void Clear()
        {
            _pressedAt = float.NegativeInfinity;
            _groundAt = float.NegativeInfinity;
        }

        /// <summary>
        /// Called once a physics step with whether the feet are on the ground now. True when the suit should jump
        /// on this step; the press and the coyote time are then used up.
        /// </summary>
        public bool Take(float now, bool grounded)
        {
            if (grounded && now - _jumpedAt > TakeOffSeconds) _groundAt = now;
            bool wanted = now - _pressedAt <= BufferSeconds;
            bool footing = now - _groundAt <= CoyoteSeconds;
            if (!wanted || !footing) return false;
            _pressedAt = float.NegativeInfinity;
            _groundAt = float.NegativeInfinity;
            _jumpedAt = now;
            return true;
        }
    }
}
