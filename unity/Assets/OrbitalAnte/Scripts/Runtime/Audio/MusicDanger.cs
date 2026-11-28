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
        /// <summary>Someone armed is on their way to look for the player: a restless squad, an alarm, overtime's hunt.</summary>
        public bool Coming;
        /// <summary>The suit's armour as a share, 0 to 1 (1 where it is not known).</summary>
        public float Armour;
    }

    /// <summary>
    /// The steady danger the music follows (<see cref="IntensityModel.SetThreat"/>), from what the game knows rather
    /// than from gunfire alone (Ralph loop pass 3, 28 Nov 2026). Before this, in the solar system the music heard the
    /// player's own shots and enemies' shots near them, and nothing else: being seen, hunted, hurt (the suit's armour
    /// was not watched) or set on by a creature (which never shoots) left it where the place put it. Plain C#.
    ///
    /// Four steps, each a band of the music (<see cref="IntensityModel.BandOf"/>), so what the player hears says how
    /// things stand:
    /// - Calm: the place's own level, a little fuller each ante (Part B section 14: the music "intensifies at higher
    ///   antes"). On foot it never reaches Tension, which means someone is after you; flying hard reaches the flight
    ///   music's top layer but never the fight music.
    /// - <see cref="Hunted"/> (Tension): an armed enemy knows where the player is but has lost sight of them, or is on
    ///   its way to look.
    /// - <see cref="Fight"/> (Combat): an enemy has had them in sight within <see cref="FightHold"/> (the same rule as
    ///   the readout's fight, <see cref="Tasks.ReadoutFocus"/>), or a creature began a warning at them as recently.
    /// - <see cref="LastPlates"/> (Chaos): in a fight on the last plates of armour, on and off with the suit's pulse
    ///   (<see cref="UI.PulseClock"/>: on at 20%, off at 30%).
    /// Events (shots, hits, damage) still add heat over the top, in <see cref="IntensityModel"/>.
    /// </summary>
    public sealed class MusicDanger
    {
        /// <summary>Tension: the surface's tension layer, the unknown's pulse.</summary>
        public const float Hunted = 46f;
        /// <summary>Combat: over <c>combatEnter</c> (60), so the fight music takes over.</summary>
        public const float Fight = 64f;
        /// <summary>Chaos: the fight music's top layer (80 and over).</summary>
        public const float LastPlates = 86f;
        /// <summary>How long after the last sighting, or a creature's warning, it is still a fight.</summary>
        public const float FightHold = Tasks.ReadoutFocus.FightHold;
        /// <summary>The calm a run adds each ante past the first, and the most it adds.</summary>
        public const float PerAnte = 2f, AnteMost = 8f;
        /// <summary>The calm on foot (or in a cave or the sea), and its most with every ante's lift.</summary>
        public const float OnFoot = 22f;
        /// <summary>Flying: coasting, and how much the engines add at full burn; never over <see cref="FlyingMost"/>.</summary>
        public const float Coasting = 20f, Burning = 36f, FlyingMost = 56f;

        bool _plates;

        /// <summary>True while the last plates are lifting the music.</summary>
        public bool OnTheLastPlates => _plates;

        /// <summary>What a run adds to the calm at this ante: nothing at ante 1, then <see cref="PerAnte"/> an ante up to <see cref="AnteMost"/>.</summary>
        public static float AnteLift(int ante) => ante <= 1 ? 0f : Math.Min(AnteMost, PerAnte * (ante - 1));

        /// <summary>
        /// The place's own level: flying hard is its own excitement (the engines lift the flight music's layers, up to
        /// <see cref="FlyingMost"/>, still under the fight music); on foot it is steady. <paramref name="ante"/> is 0
        /// outside a run.
        /// </summary>
        public static float Calm(bool flying, float engine, int ante)
        {
            float lift = AnteLift(ante);
            if (!flying) return OnFoot + lift;
            float burn = engine < 0f ? 0f : engine > 1f ? 1f : engine;
            return Math.Min(FlyingMost, Coasting + lift + Burning * burn);
        }

        /// <summary>True while it is a fight: seen within <see cref="FightHold"/>, or a creature's warning as recent.</summary>
        public static bool InFight(in DangerSense s) => Tasks.ReadoutFocus.InFight(s.SinceSeen) || (s.SinceCreature >= 0f && s.SinceCreature < FightHold);

        /// <summary>The steady danger for <see cref="IntensityModel.SetThreat"/>. Call a few times a second; it keeps the last plates' state.</summary>
        public float Threat(in DangerSense s)
        {
            bool fight = InFight(s);
            float armour = float.IsNaN(s.Armour) ? 1f : s.Armour;
            if (!fight || armour >= UI.PulseClock.StopAt) _plates = false;
            else if (armour <= UI.PulseClock.StartAt) _plates = true;

            float threat = s.Calm;
            if (s.Aware > 0 || s.Coming) threat = Math.Max(threat, Hunted);
            if (fight) threat = Math.Max(threat, Fight);
            if (_plates) threat = Math.Max(threat, LastPlates);
            return threat;
        }

        public void Reset() => _plates = false;
    }
}
