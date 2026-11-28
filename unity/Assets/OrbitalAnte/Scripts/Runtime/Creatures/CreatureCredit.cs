namespace OrbitalAnte.Creatures
{
    /// <summary>
    /// Who pays for a creature the player kills, handed to it by whatever woke it (<see cref="SeaCreatures"/> for the
    /// swimmers), so a fight with a world's animals counts toward the ante like a fight with its camps. Plain C#.
    ///
    /// - The kind sets the kill's tier (<see cref="Tasks.CreatureTiers"/>); the claim pays once a run, like every
    ///   claim, so an animal put away and woken again can never be killed for points twice.
    /// - The practice range gives none, and nothing there pays.
    /// </summary>
    public sealed class CreatureCredit
    {
        /// <summary>The creature's kind: its model's name ("Biter", "Maw", "Strider").</summary>
        public readonly string Kind;
        /// <summary>The world it lives on (its body number), for a bonus mission's bounty.</summary>
        public readonly int Body;
        /// <summary>What it pays under (<see cref="Tasks.TaskIds.Creature"/>).</summary>
        public readonly string Claim;
        /// <summary>
        /// The player's hits that have landed on it so far, counted by the task director for ONE SHOT. Kept here so the
        /// count goes when the animal does.
        /// </summary>
        public int Hits;

        public CreatureCredit(string kind, int body, string claim)
        {
            Kind = kind;
            Body = body;
            Claim = claim;
        }
    }

    /// <summary>A creature, as the task director sees one it was hit by the player (<see cref="Core.HitOutcome.Target"/>).</summary>
    public interface ICreature
    {
        /// <summary>Who pays for it; null where nothing does (the practice range).</summary>
        CreatureCredit Credit { get; }

        /// <summary>Its kind: its model's name ("Strider", "Maw"), the same as <see cref="CreatureCredit.Kind"/>, but known in the practice range too.</summary>
        string Kind { get; }

        /// <summary>
        /// True while the player is where it can strike them. A kill made from out of its reach pays nothing, because it
        /// could not fight back: a swimmer reaches only someone in the water with it (shot from the shore or from over
        /// the sea, it pays nothing, and the note says why); a land creature on a world, until it has been kept from the
        /// player for <see cref="LandReach.KeptSeconds"/> without gaining ground while it hunts them (on a ledge it cannot
        /// climb). The creatures that only live in the practice range always say true.
        /// </summary>
        bool InReach { get; }
    }
}
