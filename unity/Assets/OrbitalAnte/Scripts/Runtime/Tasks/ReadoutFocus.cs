namespace OrbitalAnte.Tasks
{
    /// <summary>
    /// What the readout leaves out while the player is in a fight (critique loop round 2, 26 Nov 2026).
    /// Plain C#, no Unity types.
    ///
    /// Problem: the corner could hold up to ten rows of words at once (ante and clock, score and bar, the world's
    /// task line, a tip, the pace words, four "+points" lines). In a fight the eyes are on the enemy and can take
    /// in a glance at most, so the rows that matter were lost among the ones that could wait (constitution rule 7,
    /// cognitive load). Rule: while an enemy has had the player in sight within <see cref="FightHold"/> seconds,
    /// the readout keeps only what a fight needs (ante, clock, score, bar, PERK READY, the two newest "+points"
    /// lines); the world's task line waits, and so does any tip that is not about the clock. Nothing is lost:
    /// what waited comes back the moment the fight is over.
    /// </summary>
    public static class ReadoutFocus
    {
        /// <summary>Seconds after the last sighting that still count as the fight (long enough not to flicker
        /// while enemies duck behind cover to reload and look out again).</summary>
        public const float FightHold = 6f;
        public const int PopsInAFight = 2;
        public const int PopsAtRest = 4;

        /// <summary>True while a fight is on: an enemy saw the player within the hold.</summary>
        public static bool InFight(float sinceSeen) => sinceSeen >= 0f && sinceSeen < FightHold;

        /// <summary>
        /// How long after a creature last knew where the player is it still counts as the fight (its spawner says so every
        /// frame it does, so this only bridges a frame or two).
        /// </summary>
        public const float AwareHold = 1f;

        /// <summary>
        /// True while a fight is on, for everything that waits out a fight (the readout, the tips, an unlock's moment and
        /// its save, the restless camps): an armed enemy saw the player within the hold, a creature began a warning within
        /// it, or a creature knows where the player is and can still hurt them. (Ralph loop pass 8, from the design review:
        /// a creature's fight did not count, so tips, an unlock banner or a squad's radio call could come in the middle of one.)
        /// </summary>
        public static bool InFight(float sinceSeen, float sinceCreatureWarned, float sinceCreatureAware)
            => InFight(sinceSeen) || CreatureFight(sinceCreatureWarned, sinceCreatureAware);

        /// <summary>A creature's fight: a warning begun within the hold, or one that knows where the player is.</summary>
        public static bool CreatureFight(float sinceWarned, float sinceAware)
            => InFight(sinceWarned) || (sinceAware >= 0f && sinceAware < AwareHold);

        /// <summary>How many "+points" lines show.</summary>
        public static int Pops(bool inFight) => inFight ? PopsInAFight : PopsAtRest;

        /// <summary>
        /// Whether PERK READY shows (critique loop round 6). It says which button opens the pick, so it shows wherever
        /// that button works: outside and walking the cabin. Only in the seat, where Select is the floodlights and the
        /// pick cannot open, does it wait (the "+points" lines still wait aboard: the seat's own readout is there).
        /// </summary>
        public static bool ShowsPerkReady(bool earned, bool seated) => earned && !seated;

        /// <summary>The world's task line ("MARS: 1 MAST") is for choosing where to go next, never mid-fight.</summary>
        public static bool ShowsTaskLine(bool inFight) => !inFight;

        /// <summary>
        /// Whether a tip may start now. In a fight only the two about the clock and the target may (they are the
        /// ones that cannot wait); the rest keep for the next quiet moment and are not marked as seen.
        /// </summary>
        public static bool TipMayStart(GuideTip tip, bool inFight)
        {
            if (!inFight) return true;
            return tip == GuideTip.Bank || tip == GuideTip.HurryUp;
        }
    }
}
