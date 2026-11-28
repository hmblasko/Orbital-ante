namespace OrbitalAnte.Creatures
{
    /// <summary>
    /// What the death page says when a creature's strike ends a run (from the design review of pass 8: a creature's kill
    /// said "Shot down outside the ship."). The strike in plain words, the way the player saw it, and never a creature's
    /// placeholder name. Plain C#.
    /// </summary>
    public static class StrikeWords
    {
        /// <summary>The Strider's claw.</summary>
        public const string Claw = "Caught in a giant claw.";
        /// <summary>The Sprinter's lunge.</summary>
        public const string Lunge = "Bitten in a lunge.";
        /// <summary>The Roller's ball.</summary>
        public const string Roll = "Run down by a rolling shell.";
        /// <summary>The Roller's butt, when it has too few plates to roll.</summary>
        public const string Butt = "Butted by a shell.";
        /// <summary>The Screamer's scream.</summary>
        public const string Scream = "Screamed down.";
        /// <summary>The Cave hunter's strike.</summary>
        public const string Rear = "Struck down by a rearing head.";
        /// <summary>The Ambusher's bite and hold.</summary>
        public const string Burrow = "Dragged toward a burrow.";
        /// <summary>The Blind stalker's lunge: it finds you by ear.</summary>
        public const string Heard = "Caught by something that heard you.";
        /// <summary>The Drifting maw's bite.</summary>
        public const string Mouth = "Drawn into a giant mouth.";
        /// <summary>A Pack biter's dart.</summary>
        public const string Shoal = "Bitten by a shoal.";

        public static readonly string[] All = { Claw, Lunge, Roll, Butt, Scream, Rear, Burrow, Heard, Mouth, Shoal };

        /// <summary>
        /// The kind (its model's name, <see cref="ICreature.Kind"/>) whose strike these words name, or null for a shot and
        /// anything else: how the playtest log (Ralph loop pass 9, <c>Playtest.PlaytestLog</c>) puts a hurt down to the
        /// warning that came before it.
        /// </summary>
        public static string KindOf(string words)
        {
            switch (words)
            {
                case Claw: return "Strider";
                case Lunge: return "Sprinter";
                case Roll:
                case Butt: return RollerLayout.Model;
                case Scream: return ScreamerLayout.Model;
                case Rear: return HunterLayout.Model;
                case Burrow: return AmbusherLayout.Model;
                case Heard: return StalkerLayout.Model;
                case Mouth: return MawLayout.Model;
                case Shoal: return BiterLayout.Model;
                default: return null;
            }
        }
    }
}
