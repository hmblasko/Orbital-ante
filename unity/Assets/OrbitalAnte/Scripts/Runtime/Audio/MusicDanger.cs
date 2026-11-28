using System;

namespace OrbitalAnte.Audio
{
    /// <summary>What the game knows about the danger the player is in, a few times a second, for <see cref="MusicDanger"/>.</summary>
    public struct DangerSense
    {
        /// <summary>The place's own level with nobody after the player (<see cref="MusicDanger.Calm"/>).</summary>
        public float Calm;
        /// <summary>Seconds since an enemy last had the player in sight (<c>CombatWorld.PlayerLastSeenAt</c>); large if never.</summary>
        public float SinceSeen;
        /// <summary>Seconds since a creature last began a warning at the player; large if never.</summary>
        public float SinceCreature;
        /// <summary>Armed enemies present who know where the player is.</summary>
        public int Aware;
        /// <summary>
        /// Someone armed holds a place to look for the player: one they heard them at, an alarm's (a Scout's flare, a
        /// mast's call, a supply drop, overtime's hunt) or one a restless squad was sent to.
        /// </summary>
        public bool Coming;
        /// <summary>A creature has the player in mind: a Drifting maw or a pack of Pack biters that has noticed the swimmer.</summary>
        public bool CreatureAware;
        /// <summary>
        /// True where <see cref="Aware"/>, <see cref="Coming"/> and <see cref="CreatureAware"/> are counted (the solar
        /// system). The practice range counts none of them, so there a sighting alone is a fight.
        /// </summary>
        public bool Counted;
        /// <summary>The suit's armour as a share, 0 to 1 (1 where it is not known).</summary>
        public float Armour;
    }

    /// <summary>
    /// The steady danger the music follows (<see cref="IntensityModel.SetThreat"/>), from what the game knows rather
    /// than from gunfire alone (Ralph loop pass 3, 28 Nov 2026). Before this, in the solar system the music heard the
    /// player's own shots and enemies' shots near them, and nothing else: being seen, hunted, hurt (the suit's armour
    /// was not watched) or set on by a creature (which never shoots) left it where the place put it. Plain C#.
    ///
    /// Three steps, each a band of the music (<see cref="IntensityModel.BandOf"/>), so what the player hears says how
    /// things stand:
    /// - Calm: the place's own level, a little fuller each ante (Part B section 14: the music "intensifies at higher
    ///   antes"). On foot it never reaches Tension, which means someone is after you; flying hard reaches the flight
    ///   music's top layer but never the fight music.
    /// - <see cref="Hunted"/> (Tension): an armed enemy knows where the player is but has lost sight of them, or holds a
    ///   place to look for them, or a sea creature has them in mind. Below the combat music's exit (48), so a search
    ///   after a fight is heard as tension, not as the fight going on.
    /// - <see cref="Fight"/> (Combat): an armed enemy has had them in sight within <see cref="FightHold"/> (the same
    ///   rule as the readout's fight, <see cref="Tasks.ReadoutFocus"/>), or a creature began a warning at them as
    ///   recently, or within <see cref="CreatureHold"/> while it still has them in mind.
    /// On the last plates of armour in a fight (<see cref="OnTheLastPlates"/>, on and off with the suit's pulse,
    /// <see cref="UI.PulseClock"/>) the music thins rather than grows: its top layers are held off and it dips 4 dB
    /// (<see cref="MusicBrain.Thin"/>, <see cref="ThinDb"/>), so the pulse is heard.
    /// Events (shots, hits, damage) still add heat over the top, in <see cref="IntensityModel"/>.
    /// </summary>
    public sealed class MusicDanger
    {
        /// <summary>Tension: the surface's tension layer, the unknown's pulse.</summary>
        public const float Hunted = 46f;
        /// <summary>Combat: over <c>combatEnter</c> (60), so the fight music takes over.</summary>
        public const float Fight = 64f;
        /// <summary>How long after the last sighting, or a creature's warning, it is still a fight.</summary>
        public const float FightHold = Tasks.ReadoutFocus.FightHold;
        /// <summary>
        /// While a creature still has the player in mind, how long after its last warning it is still a fight: the
        /// Drifting maw comes again every 12 to 20 s, so the fight music holds between its strikes instead of ending
        /// and starting again.
        /// </summary>
        public const float CreatureHold = 24f;
        /// <summary>The calm a run adds each ante past the first, and the most it adds (reached at ante 3, where most first runs end).</summary>
        public const float PerAnte = 4f, AnteMost = 8f;
        /// <summary>How far the music dips while it thins on the last plates in a fight, so the suit's pulse is heard.</summary>
        public const float ThinDb = -4f;
        /// <summary>The calm on foot (or in a cave or the sea), before an ante's lift.</summary>
        public const float OnFoot = 22f;
        /// <summary>Flying: coasting, and how much the engines add at full burn; never over <see cref="FlyingMost"/>.</summary>
        public const float Coasting = 20f, Burning = 36f, FlyingMost = 56f;

        bool _plates, _after;

        /// <summary>True while the player is on the last plates in a fight: the music thins so the suit's pulse is heard.</summary>
        public bool OnTheLastPlates => _plates;

        /// <summary>True while someone or something is after the player (hunted, or a fight): a piece does not rest through it.</summary>
        public bool SomeoneAfter => _after;

        /// <summary>What a run adds to the calm at this ante: nothing at ante 1, then <see cref="PerAnte"/> an ante up to <see cref="AnteMost"/>.</summary>
        public static float AnteLift(int ante) => ante <= 1 ? 0f : Math.Min(AnteMost, PerAnte * (ante - 1));

        /// <summary>
        /// The place's own level. On foot it is steady, fuller with each ante's lift. Flying hard is its own excitement
        /// (the engines lift the flight music's layers, up to <see cref="FlyingMost"/>, still under the fight music), and
        /// the ante's lift goes with the engines, so coasting stays quiet at every ante and a burn still stands out
        /// (constitution 8). <paramref name="ante"/> is 0 outside a run.
        /// </summary>
        public static float Calm(bool flying, float engine, int ante)
        {
            float lift = AnteLift(ante);
            if (!flying) return OnFoot + lift;
            float burn = engine < 0f ? 0f : engine > 1f ? 1f : engine;
            return Math.Min(FlyingMost, Coasting + (Burning + lift) * burn);
        }

        /// <summary>
        /// True while it is a fight: seen within <see cref="FightHold"/> (where hunters are counted, only while an armed
        /// enemy knows where the player is: a disarmed one hiding is no fight), or a creature's warning as recent, or
        /// within <see cref="CreatureHold"/> while a creature still has the player in mind.
        /// </summary>
        public static bool InFight(in DangerSense s)
        {
            bool seen = Tasks.ReadoutFocus.InFight(s.SinceSeen) && (!s.Counted || s.Aware > 0);
            bool creature = s.SinceCreature >= 0f && (s.SinceCreature < FightHold || (s.CreatureAware && s.SinceCreature < CreatureHold));
            return seen || creature;
        }

        /// <summary>True while someone is after the player without a fight: they know where the player is, are coming to look, or a creature has them in mind.</summary>
        public static bool BeingHunted(in DangerSense s) => s.Aware > 0 || s.Coming || s.CreatureAware;

        /// <summary>The steady danger for <see cref="IntensityModel.SetThreat"/>. Call a few times a second; it keeps the last plates' state.</summary>
        public float Threat(in DangerSense s)
        {
            bool fight = InFight(s);
            bool hunted = BeingHunted(s);
            float armour = float.IsNaN(s.Armour) ? 1f : s.Armour;
            if (!fight || armour >= UI.PulseClock.StopAt) _plates = false;
            else if (armour <= UI.PulseClock.StartAt) _plates = true;
            _after = fight || hunted;

            float threat = s.Calm;
            if (hunted) threat = Math.Max(threat, Hunted);
            if (fight) threat = Math.Max(threat, Fight);
            return threat;
        }

        public void Reset()
        {
            _plates = false;
            _after = false;
        }
    }
}
