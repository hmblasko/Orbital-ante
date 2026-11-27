using System;

namespace OrbitalAnte.Creatures
{
    /// <summary>
    /// How far a creature's warning has got (Ralph loop pass 2, 27 Nov 2026). Every creature that strikes warns the
    /// same way, whatever it looks like: it winds up while it still follows the player, takes aim at its brain's lock
    /// (from then on a step aside is a miss), then strikes. The visor shows the stage as an amber arc (UI/WindupCues)
    /// and the warning's sound has one hard mark at the lock.
    /// </summary>
    public enum WindupStage
    {
        /// <summary>No warning, or it is over.</summary>
        None,
        /// <summary>Winding up, still turning to follow the player: moving now does not help yet.</summary>
        Winding,
        /// <summary>It has taken aim at a place: a step aside now is a miss.</summary>
        Aimed,
        /// <summary>The strike itself (a roll, a scream, a draw-in, a dart), until it is over.</summary>
        Striking,
    }

    /// <summary>
    /// Each creature's warning read from its brain's own orders, the seconds into the warning at which it takes aim at
    /// the dials it is given, and the speed to play its warning at so the sound's mark lands on that moment. Plain C#,
    /// so the rule tests hold every stage to the brain that makes it.
    ///
    /// The stages follow the brains' own tests for aiming: a walker's claw at <see cref="WalkerBrain.LockShare"/> of its
    /// strike, the Roller, Cave hunter, Screamer and Ambusher at their <c>LockAt</c> share of the warning, the Drifting
    /// maw and Pack biter by the <c>Locked</c> they report, and the Blind stalker at <see cref="StalkerBrain.LockAt"/>.
    /// </summary>
    public static class Windups
    {
        // The warning sounds, one per creature. Each one's mark is the manifest's "lockAt" for it, which must be the
        // creature's default lock time (rule test EveryCreaturesWarning_HasItsOwnSound_WithItsMarkWhereItTakesAim).
        public const string StriderWarning = "creature.strider.raise";
        public const string SprinterWarning = "creature.sprinter.raise";
        public const string RollerWarning = "creature.roller.curl";
        public const string HunterWarning = "creature.hunter.rear";
        public const string MawWarning = "creature.maw.gape";
        public const string BiterWarning = "creature.biter.mark";
        public const string ScreamerWarning = "creature.screamer.windup";
        public const string AmbusherWarning = "creature.ambusher.coil";
        public const string StalkerWarning = "creature.stalker.gape";

        /// <summary>The warning sound of a walker kind, by its model's name ("Strider", "Sprinter").</summary>
        public static string WalkerWarning(string model) => "creature." + (model ?? string.Empty).ToLowerInvariant() + ".raise";

        // ------------------------------------------------------------------ stages

        /// <summary>
        /// A walker: from the first lift of the claw (or the arms' reach, for a kind that lunges) until it lands at
        /// <see cref="WalkerAnimator.StrikeLands"/> of the strike. <paramref name="lockShare"/> is
        /// <see cref="WalkerBrain.LockShare"/> for its body now (0 for a lunging kind with both arms gone, which bites
        /// where the player stood when it began, so it is aimed from the start).
        /// </summary>
        public static WindupStage Walker(in WalkerOrders o, double lockShare)
        {
            if (o.StrikeSide < 0 || o.StrikeT >= WalkerAnimator.StrikeLands) return WindupStage.None;
            return o.StrikeT < lockShare ? WindupStage.Winding : WindupStage.Aimed;
        }

        /// <summary>The Roller: the curl is the warning (it snaps shut at the lock); the roll is the strike.</summary>
        public static WindupStage Roller(in RollerOrders o) => o.Mode switch
        {
            RollerMode.Wind => o.T < RollerBrain.LockAt ? WindupStage.Winding : WindupStage.Aimed,
            RollerMode.Roll => WindupStage.Striking,
            _ => WindupStage.None,
        };

        /// <summary>The Cave hunter: rearing is the warning; the whip of its head is the strike.</summary>
        public static WindupStage Hunter(in HunterOrders o) => o.Mode switch
        {
            HunterMode.Rear => o.T < HunterBrain.LockAt ? WindupStage.Winding : WindupStage.Aimed,
            HunterMode.Strike => WindupStage.Striking,
            _ => WindupStage.None,
        };

        /// <summary>The Drifting maw: the gape is the warning; drawing in is the strike (sideways is the way out).</summary>
        public static WindupStage Maw(in MawOrders o) => o.Mode switch
        {
            MawMode.Gape => o.Locked ? WindupStage.Aimed : WindupStage.Winding,
            MawMode.Inhale => WindupStage.Striking,
            _ => WindupStage.None,
        };

        /// <summary>A Pack biter: the mark is the warning; the dart is the strike.</summary>
        public static WindupStage Biter(in BiterOrders o) => o.Mode switch
        {
            BiterMode.Mark => o.Locked ? WindupStage.Aimed : WindupStage.Winding,
            BiterMode.Dart => WindupStage.Striking,
            _ => WindupStage.None,
        };

        /// <summary>The Screamer: the wind-up is the warning; the scream is the strike.</summary>
        public static WindupStage Screamer(in ScreamerOrders o) => o.Mode switch
        {
            ScreamerMode.WindUp => o.T < ScreamerBrain.LockAt ? WindupStage.Winding : WindupStage.Aimed,
            ScreamerMode.Scream => WindupStage.Striking,
            _ => WindupStage.None,
        };

        /// <summary>The Ambusher: rearing (the coil) is the warning; the strike is the strike. Once it holds someone they know.</summary>
        public static WindupStage Ambusher(in AmbusherOrders o) => o.Mode switch
        {
            AmbusherMode.Coil => o.T < AmbusherBrain.LockAt ? WindupStage.Winding : WindupStage.Aimed,
            AmbusherMode.Strike => WindupStage.Striking,
            _ => WindupStage.None,
        };

        /// <summary>The Blind stalker: the gape is the warning; the lunge is the strike.</summary>
        public static WindupStage Stalker(in StalkerOrders o) => o.Mode switch
        {
            StalkerMode.Gape => o.T < StalkerBrain.LockAt ? WindupStage.Winding : WindupStage.Aimed,
            StalkerMode.Lunge => WindupStage.Striking,
            _ => WindupStage.None,
        };

        // ------------------------------------------------------------------ when each takes aim, in seconds

        // Each uses the same floor on its dial as the brain does, so a tiny dial gives the brain's real timing.
        public static double WalkerLock(WalkerRules r, double lockShare) => lockShare * Math.Max(0.2, (r ?? new WalkerRules()).strikeSeconds);
        public static double RollerLock(RollerRules r) => RollerBrain.LockAt * Math.Max(0.2, (r ?? new RollerRules()).windSeconds);
        public static double HunterLock(HunterRules r) => HunterBrain.LockAt * Math.Max(0.2, (r ?? new HunterRules()).rearSeconds);
        public static double MawLock(MawRules r) => MawBrain.LockAt * (r ?? new MawRules()).gapeSeconds;
        public static double BiterLock(BiterRules r) => BiterBrain.LockAt * (r ?? new BiterRules()).markSeconds;
        public static double ScreamerLock(ScreamerRules r) => ScreamerBrain.LockAt * Math.Max(0.2, (r ?? new ScreamerRules()).windUpSeconds);
        public static double AmbusherLock(AmbusherRules r) => AmbusherBrain.LockAt * Math.Max(0.2, (r ?? new AmbusherRules()).coilSeconds);
        public static double StalkerLock(StalkerRules r) => StalkerBrain.LockAt * (r ?? new StalkerRules()).gapeSeconds;

        // ------------------------------------------------------------------ the sound's speed

        /// <summary>The slowest a warning is played to keep its mark on the lock: about four semitones down.</summary>
        public const float LeastPitch = 0.8f;
        /// <summary>The quickest: about four semitones up. Past these the sound would stop sounding like the animal.</summary>
        public const float MostPitch = 1.25f;

        /// <summary>
        /// The speed to play a warning at so its mark, <paramref name="markSeconds"/> into the clip, lands
        /// <paramref name="lockSeconds"/> after it starts: 1 at the default dials. A sound with no mark, or a warning
        /// aimed from its very start (a Sprinter with no arms), plays as it is.
        /// </summary>
        public static float PitchFor(double markSeconds, double lockSeconds)
        {
            if (!(markSeconds > 0.0) || !(lockSeconds > 0.0)) return 1f;
            double p = markSeconds / lockSeconds;
            return (float)(p < LeastPitch ? LeastPitch : p > MostPitch ? MostPitch : p);
        }
    }
}
