namespace OrbitalAnte.Creatures
{
    /// <summary>
    /// How far a creature's warning has got (Ralph loop pass 2, 27 Nov 2026). Every creature that strikes warns the
    /// same way, whatever it looks like: it winds up while it still follows the player, takes aim at its brain's lock
    /// (from then on a step aside is a miss), then strikes. The visor shows the stage as an amber arc (UI/WindupCues),
    /// and the moment it takes aim has a sound of its own: one hard mark.
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

    /// <summary>A creature's warning as read from its brain this frame: the stage and, while it winds up, how near the lock it is.</summary>
    public readonly struct WindupRead
    {
        public readonly WindupStage Stage;
        /// <summary>0 as the warning begins, 1 at the lock (and from then on). The visor's countdown.</summary>
        public readonly float Progress;

        public WindupRead(WindupStage stage, double progress)
        {
            Stage = stage;
            Progress = stage == WindupStage.Winding ? (float)(progress > 0.0 ? (progress < 1.0 ? progress : 1.0) : 0.0) : stage == WindupStage.None ? 0f : 1f;
        }

        public static readonly WindupRead None = new WindupRead(WindupStage.None, 0.0);
    }

    /// <summary>What a change of stage asks for, worked out by <see cref="Windups.Change"/>.</summary>
    public readonly struct WindupChange
    {
        /// <summary>A warning has begun: tell the visor (<c>CombatWorld.RaiseCreatureWindup</c>), once.</summary>
        public readonly bool Began;
        /// <summary>Play the creature's wind-up sound.</summary>
        public readonly bool Warn;
        /// <summary>Play the creature's aim sound: it has taken aim this very frame.</summary>
        public readonly bool Aim;
        /// <summary>
        /// The stage before and after (both <see cref="WindupStage.None"/> when nothing changed), for the development
        /// build's playtest log (Ralph loop pass 9), which counts each warning from its start to its end.
        /// </summary>
        public readonly WindupStage Was, Now;

        public WindupChange(WindupStage was, WindupStage now, bool began, bool warn, bool aim)
        {
            Was = was;
            Now = now;
            Began = began;
            Warn = warn;
            Aim = aim;
        }

        /// <summary>True when the stage changed this frame (every frame with something to say, and some with nothing to play).</summary>
        public bool Changed => Was != Now;
    }

    /// <summary>
    /// One creature's warning from frame to frame: the stage and countdown it showed last, and what each new reading asks
    /// for (<see cref="Windups.Change"/>). A class, so a creature holds it in a readonly field and nothing is copied.
    /// </summary>
    public sealed class WindupTrack
    {
        public WindupStage Stage { get; private set; }
        public float Progress { get; private set; }

        /// <summary>Takes this frame's reading and says what it asks for.</summary>
        public WindupChange Step(in WindupRead now)
        {
            WindupChange change = Windups.Change(Stage, now.Stage);
            Stage = now.Stage;
            Progress = now.Progress;
            return change;
        }
    }

    /// <summary>
    /// Each creature's warning read from its brain's own orders, and what each change of stage asks for. Plain C#, so
    /// the rule tests hold every stage to the brain that makes it.
    ///
    /// The stages follow the brains' own tests for aiming: a walker's claw at <see cref="WalkerBrain.LockShare"/> of its
    /// strike, the Roller, Cave hunter, Screamer and Ambusher at their <c>LockAt</c> share of the warning, the Drifting
    /// maw and Pack biter by the <c>Locked</c> they report, and the Blind stalker at <see cref="StalkerBrain.LockAt"/>.
    ///
    /// The sounds follow the stages, not a clock of their own: the wind-up plays as the stage turns to Winding, and the
    /// aim (a hard mark at the very front of its file) as it turns to Aimed. So the mark is on the lock whatever a dial,
    /// Focus or a hitstop does, a warning called off before it takes aim never marks, and a creature aimed from the start
    /// (a Sprinter with no arms) gives the mark at once with no wind-up before it.
    /// </summary>
    public static class Windups
    {
        // The sounds, two per creature (tools/audio/sfx/creature.py): its wind-up and its aim.
        public const string StriderWarning = "creature.strider.raise", StriderAim = "creature.strider.aim";
        public const string SprinterWarning = "creature.sprinter.raise", SprinterAim = "creature.sprinter.aim";
        public const string RollerWarning = "creature.roller.curl", RollerAim = "creature.roller.aim";
        public const string HunterWarning = "creature.hunter.rear", HunterAim = "creature.hunter.aim";
        public const string MawWarning = "creature.maw.gape", MawAim = "creature.maw.aim";
        public const string BiterWarning = "creature.biter.mark", BiterAim = "creature.biter.aim";
        public const string ScreamerWarning = "creature.screamer.windup", ScreamerAim = "creature.screamer.aim";
        public const string AmbusherWarning = "creature.ambusher.coil", AmbusherAim = "creature.ambusher.aim";
        public const string StalkerWarning = "creature.stalker.gape", StalkerAim = "creature.stalker.aim";

        /// <summary>The wind-up sound of a walker kind, by its model's name ("Strider", "Sprinter").</summary>
        public static string WalkerWarning(string model) => "creature." + Kind(model) + ".raise";

        /// <summary>The aim sound of a walker kind, by its model's name.</summary>
        public static string WalkerAim(string model) => "creature." + Kind(model) + ".aim";

        static string Kind(string model) => (model ?? string.Empty).ToLowerInvariant();

        // ------------------------------------------------------------------ what a change asks for

        /// <summary>
        /// What going from one stage to the next asks for. The wind-up sounds as a warning starts winding; the aim as it
        /// takes aim, from winding or straight from nothing (a creature aimed from the start). A frame long enough to pass
        /// the whole aimed stretch still gives the mark, once, as the strike begins. Nothing sounds as a warning is called
        /// off, and the strike has its own sound. The visor is told each time a warning begins.
        /// </summary>
        public static WindupChange Change(WindupStage was, WindupStage now)
        {
            if (now == was) return default;
            bool warn = now == WindupStage.Winding;
            bool aim = now == WindupStage.Aimed || (now == WindupStage.Striking && (was == WindupStage.Winding || was == WindupStage.None));
            bool began = warn || (was == WindupStage.None && now != WindupStage.None);
            return new WindupChange(was, now, began, warn, aim);
        }

        // ------------------------------------------------------------------ stages

        /// <summary>
        /// A walker: from the first lift of the claw (or the arms' reach, for a kind that lunges) until it lands at
        /// <see cref="WalkerAnimator.StrikeLands"/> of the strike. <paramref name="lockShare"/> is
        /// <see cref="WalkerBrain.LockShare"/> for its body now (0 for a lunging kind with both arms gone, which bites
        /// where the player stood when it began, so it is aimed from the start).
        /// </summary>
        public static WindupRead Walker(in WalkerOrders o, double lockShare)
        {
            if (o.StrikeSide < 0 || o.StrikeT >= WalkerAnimator.StrikeLands) return WindupRead.None;
            if (o.StrikeT < lockShare) return new WindupRead(WindupStage.Winding, o.StrikeT / lockShare);
            return new WindupRead(WindupStage.Aimed, 1.0);
        }

        /// <summary>The Roller: the curl is the warning (it snaps shut at the lock); the roll is the strike.</summary>
        public static WindupRead Roller(in RollerOrders o) => o.Mode switch
        {
            RollerMode.Wind => Warning(o.T, RollerBrain.LockAt),
            RollerMode.Roll => new WindupRead(WindupStage.Striking, 1.0),
            _ => WindupRead.None,
        };

        /// <summary>The Cave hunter: rearing is the warning; the whip of its head is the strike.</summary>
        public static WindupRead Hunter(in HunterOrders o) => o.Mode switch
        {
            HunterMode.Rear => Warning(o.T, HunterBrain.LockAt),
            HunterMode.Strike => new WindupRead(WindupStage.Striking, 1.0),
            _ => WindupRead.None,
        };

        /// <summary>The Drifting maw: the gape is the warning; drawing in is the strike (sideways is the way out).</summary>
        public static WindupRead Maw(in MawOrders o) => o.Mode switch
        {
            MawMode.Gape => o.Locked ? new WindupRead(WindupStage.Aimed, 1.0) : new WindupRead(WindupStage.Winding, o.T / MawBrain.LockAt),
            MawMode.Inhale => new WindupRead(WindupStage.Striking, 1.0),
            _ => WindupRead.None,
        };

        /// <summary>A Pack biter: the mark is the warning; the dart is the strike.</summary>
        public static WindupRead Biter(in BiterOrders o) => o.Mode switch
        {
            BiterMode.Mark => o.Locked ? new WindupRead(WindupStage.Aimed, 1.0) : new WindupRead(WindupStage.Winding, o.T / BiterBrain.LockAt),
            BiterMode.Dart => new WindupRead(WindupStage.Striking, 1.0),
            _ => WindupRead.None,
        };

        /// <summary>The Screamer: the wind-up is the warning; the scream is the strike.</summary>
        public static WindupRead Screamer(in ScreamerOrders o) => o.Mode switch
        {
            ScreamerMode.WindUp => Warning(o.T, ScreamerBrain.LockAt),
            ScreamerMode.Scream => new WindupRead(WindupStage.Striking, 1.0),
            _ => WindupRead.None,
        };

        /// <summary>The Ambusher: rearing (the coil) is the warning; the strike is the strike. Once it holds someone they know.</summary>
        public static WindupRead Ambusher(in AmbusherOrders o) => o.Mode switch
        {
            AmbusherMode.Coil => Warning(o.T, AmbusherBrain.LockAt),
            AmbusherMode.Strike => new WindupRead(WindupStage.Striking, 1.0),
            _ => WindupRead.None,
        };

        /// <summary>The Blind stalker: the gape is the warning; the lunge is the strike.</summary>
        public static WindupRead Stalker(in StalkerOrders o) => o.Mode switch
        {
            StalkerMode.Gape => Warning(o.T, StalkerBrain.LockAt),
            StalkerMode.Lunge => new WindupRead(WindupStage.Striking, 1.0),
            _ => WindupRead.None,
        };

        /// <summary>A warning <paramref name="t"/> of the way through (0 to 1) that takes aim at <paramref name="lockAt"/> of it.</summary>
        static WindupRead Warning(double t, double lockAt) =>
            t < lockAt ? new WindupRead(WindupStage.Winding, t / lockAt) : new WindupRead(WindupStage.Aimed, 1.0);

        // ------------------------------------------------------------------ a sound's speed

        /// <summary>The slowest a sound is played to last as long as what it goes with: about four semitones down.</summary>
        public const float LeastPitch = 0.8f;
        /// <summary>The quickest: about four semitones up. Past these the sound would stop sounding like the animal.</summary>
        public const float MostPitch = 1.25f;

        /// <summary>
        /// The speed to play a sound made <paramref name="madeSeconds"/> long at, so it lasts <paramref name="lastSeconds"/>
        /// (the Drifting maw's draw-in, whose rising tells how long is left): 1 at the default dials, kept between
        /// <see cref="LeastPitch"/> and <see cref="MostPitch"/>.
        /// </summary>
        public static float PitchFor(double madeSeconds, double lastSeconds)
        {
            if (!(madeSeconds > 0.0) || !(lastSeconds > 0.0)) return 1f;
            double p = madeSeconds / lastSeconds;
            return (float)(p < LeastPitch ? LeastPitch : p > MostPitch ? MostPitch : p);
        }
    }
}
