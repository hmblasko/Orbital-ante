using System;
using System.Collections.Generic;
using System.Globalization;
using OrbitalAnte.Audio;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;

namespace OrbitalAnte.Playtest
{
    /// <summary>How a run's log ended.</summary>
    public enum RunEnd : byte
    {
        /// <summary>Still going: the log was written early (F7, or a pause).</summary>
        StillGoing = 0,
        /// <summary>The player died (a restart or leaving before the death page came up still counts as the death).</summary>
        Died = 1,
        /// <summary>The run ended without a death (the run-over page: all antes cleared, or the clock under the old rule).</summary>
        RunOver = 2,
        /// <summary>The player left it: a restart, or back to the title screen.</summary>
        Left = 3,
        /// <summary>The game was closed, or Play mode stopped in the editor.</summary>
        Closed = 4,
    }

    /// <summary>How a creature's part in the run ended.</summary>
    public enum AnimalEnd : byte
    {
        /// <summary>Alive when the log was written.</summary>
        StillThere = 0,
        KilledByYou = 1,
        /// <summary>Its strike ended the player's run (or knocked them down in the practice range).</summary>
        KilledYou = 2,
        /// <summary>The player left it behind and it was put away (out of range, or off its world).</summary>
        PutAway = 3,
        /// <summary>A pack: every fish of it is dead.</summary>
        AllDead = 4,
    }

    /// <summary>How one warning ended, as counted for the playtests of pass 2 ("move on the mark").</summary>
    public enum WarningEnd : byte
    {
        /// <summary>Still under way when the log was written.</summary>
        Open = 0,
        /// <summary>Over before it took aim: it never gave its mark.</summary>
        CalledOff = 1,
        /// <summary>It took aim and struck, and the strike did not hurt the player.</summary>
        Missed = 2,
        /// <summary>The strike hurt the player.</summary>
        Hurt = 3,
        /// <summary>The player killed it while it warned or struck, and it had not hurt them.</summary>
        Killed = 4,
        /// <summary>It took aim but never struck: something stopped it after its mark (a piece shot off, it was put away).</summary>
        Stopped = 5,
    }

    /// <summary>What the player did across the line to a creature from its mark (pass 2's dodge; <see cref="DodgeRules"/>).</summary>
    public enum MoveOnMark : byte
    {
        /// <summary>It gave no mark: called off before it, or still winding up.</summary>
        NoMark = 0,
        /// <summary>Already moving across the line as it took aim.</summary>
        AlreadyMoving = 1,
        /// <summary>Started moving across after its mark, before it struck: the dodge on the mark.</summary>
        OnTheMark = 2,
        /// <summary>Started moving across only once it struck.</summary>
        AfterTheStrike = 3,
        /// <summary>Did not move across from its mark to its end (or to the end of the log).</summary>
        NotAcross = 4,
    }

    /// <summary>One creature the run met: a land animal, a sea animal, a pack, or a creature of the practice range.</summary>
    public sealed class AnimalRecord
    {
        /// <summary>Counted per kind, in the order met: the Strider #1, the Strider #2.</summary>
        public int Number;
        /// <summary>Its model's name ("Strider"), or "Biter pack" for a shoal.</summary>
        public string Kind = "";
        /// <summary>The world it lives on ("Moon"), or "practice range".</summary>
        public string World = "";
        public int Ante;
        /// <summary>The pack it swims in, for a Pack biter; null otherwise.</summary>
        public AnimalRecord Group;
        /// <summary>A pack of Pack biters: its fish have records of their own, which add to this one.</summary>
        public bool IsGroup;
        /// <summary>It was laid out with the practice range, not woken by a world's spawner.</summary>
        public bool InTheRange;
        /// <summary>When it was woken (or laid out), and from how far. Real seconds, as every "at" here.</summary>
        public float WokeAt = -1f, WokeFrom = -1f;
        /// <summary>
        /// When it first came into the frame of the player's view with the ground not between them (hills and the horizon
        /// are checked on the worlds; rocks, trees, buildings, night and water are not), and from how far.
        /// </summary>
        public float SeenAt = -1f, SeenFrom = -1f;
        public float NoticedAt = -1f;
        /// <summary>The last time it was after the player (it knew where they were, warned, was hit or hurt them), in game seconds.</summary>
        public float ActiveAt = -1f;
        /// <summary>When its fight began: the first of noticing the player, its first warning, the first hit on it and its first hurt.</summary>
        public float FightFrom = -1f, FightFromGame = -1f;
        public float FirstHitAt = -1f, FirstHitGame = -1f;
        /// <summary>The player's hits on it by <see cref="HitZone"/> (Plain, Armour, Vital, WeaponOff).</summary>
        public readonly int[] Hits = new int[4];
        public int PiecesOff;
        public int Warnings;
        public float DamageToYou;
        /// <summary>The line over the crosshair named its kind's armour after a hit on it.</summary>
        public bool AimLineShown;
        /// <summary>An enemy had the player in sight while its fight was on.</summary>
        public bool EnemySawYou;
        /// <summary>An enemy had seen the player within the fight hold before its fight began: a fight with people was already on.</summary>
        public bool PeopleFirst;
        /// <summary>A walker's feet that slipped toward their legs, out of reach of the ground (WalkerAnimator.Slips).</summary>
        public int Slips = -1;
        /// <summary>The wake frame's own length, the longest frame in the half second after it, and the middle frame of the second before, in ms.</summary>
        public float WakeFrameOwnMs = -1f, WakeFrameMs = -1f, WakeBaselineMs = -1f;
        /// <summary>The first of its kind woken this session: its model and pictures may have been loaded as it woke.</summary>
        public bool WakeFirstOfKind;
        /// <summary>A pause or a write of the log fell in the half second after it woke, so its frames were not timed.</summary>
        public bool WakeUntimed;
        public AnimalEnd End;
        public float EndAt = -1f, EndGame = -1f;
        public HitZone KillZone;
        public string KillPiece = "";
        /// <summary>For a kill: the claim it pays on (null where nothing pays for it, as in the practice range).</summary>
        public string Claim;
        /// <summary>For a kill: it could not answer the player there, so the game paid nothing for it (<c>ICreature.InReach</c>).</summary>
        public bool OutOfReach;
        /// <summary>For a kill: what the ante paid on its claim, or why not; -1 and null until (or unless) its answer came.</summary>
        public int PaidPoints = -1;
        public string PayRefused;
        /// <summary>For a pack: its fish the player killed.</summary>
        public int Kills;

        public bool Killed => End == AnimalEnd.KilledByYou;
        public bool NoCredit => Killed && Claim == null;
        public bool Paid => PaidPoints > 0;
        public int TotalHits => Hits[0] + Hits[1] + Hits[2] + Hits[3];
        public string Name => Kind + " #" + Number.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>One creature's warning, from its first stage to its last.</summary>
    public sealed class WarningRecord
    {
        public AnimalRecord Animal;
        /// <summary>Real seconds from the run's start, to find it in the event lines and a recording.</summary>
        public float BeganAt;
        public float AimedAt = -1f, StruckAt = -1f, OverAt = -1f;
        /// <summary>The same in game seconds (the creature's own clock), for how long things took and for the graces.</summary>
        public float BeganGame, AimedGame = -1f, StruckGame = -1f, OverGame = -1f;
        /// <summary>Degrees between where the player looked and the creature, as it began and at its mark.</summary>
        public float FacingAtBegin = -1f, FacingAtMark = -1f;
        public float RangeAtMark = -1f;
        /// <summary>It was inside the edges of the player's view as it took aim.</summary>
        public bool InFrameAtMark;
        public float Damage;
        public int Beats;
        /// <summary>The player killed it while this warning was under way (a dead creature never ends its warning itself).</summary>
        public bool KilledDuring;
        /// <summary>Shots the player fired while it was under way.</summary>
        public int Shots;
        /// <summary>The player's speed across the line to it, along the ground, smoothed (<see cref="DodgeRules"/>).</summary>
        public float Across;
        internal bool Sampled;
        /// <summary>The player was already moving across the line as it took aim.</summary>
        public bool AcrossAtMark;
        /// <summary>When the player first moved across the line after its mark (never if they were already moving): real and game seconds.</summary>
        public float MovedAt = -1f, MovedGame = -1f;

        /// <summary>It gave its mark: it took aim (or struck straight away).</summary>
        public bool Marked => AimedAt >= 0f;

        public WarningEnd End => Damage > 0f ? WarningEnd.Hurt
                               : KilledDuring ? WarningEnd.Killed
                               : OverAt < 0f ? WarningEnd.Open
                               : !Marked ? WarningEnd.CalledOff
                               : StruckAt >= 0f ? WarningEnd.Missed : WarningEnd.Stopped;

        public MoveOnMark Move => !Marked ? MoveOnMark.NoMark
                                : AcrossAtMark ? MoveOnMark.AlreadyMoving
                                : MovedGame < 0f ? MoveOnMark.NotAcross
                                : StruckGame < 0f || MovedGame <= StruckGame ? MoveOnMark.OnTheMark
                                : MoveOnMark.AfterTheStrike;
    }

    /// <summary>One ante of the run, as it went.</summary>
    public sealed class AnteRecord
    {
        public int Ante, Target;
        public float BeganAt;
        /// <summary>When its clock began to run (ante 1's waits on the pad until liftoff); -1 if it was never held.</summary>
        public float ClockFrom = -1f;
        public float TargetMetAt = -1f, OvertimeAt = -1f, ClearedAt = -1f;
        /// <summary>Seconds left on the ante's own clock (which stops for pauses) when its target was met and when it was cleared.</summary>
        public float LeftAtTarget = -1f, LeftAtClear = -1f;
        public int Banked = -1;
        /// <summary>The perks taken during it or at its break, in order.</summary>
        public readonly List<string> Perks = new List<string>();
    }

    /// <summary>A count and what it came to (points, damage).</summary>
    public sealed class Tally
    {
        public int Count;
        public double Total;
    }

    /// <summary>One event line: when (real seconds from the run's start) and what. The clock is written with the report.</summary>
    public readonly struct LogLine
    {
        public readonly float At;
        public readonly string Text;

        public LogLine(float at, string text)
        {
            At = at;
            Text = text;
        }

        public override string ToString() => PlaytestLog.Clock(At) + "  " + Text;
    }

    /// <summary>One gun (or the melee slot, or the rover): shots fired, hits and kills.</summary>
    public sealed class GunTally
    {
        public int Shots, Hits, Kills;
    }

    /// <summary>
    /// One time the line over the crosshair named a creature's armour (pass 7), and what lay under the line while it
    /// showed, looked at a few times a second: the creature it named, and that creature's weak place (pass 7's open
    /// question: does the line ever cover the place it names?).
    /// </summary>
    public sealed class AimLineRecord
    {
        public float At;
        public string Kind = "";
        public string Name = "";
        public int Looks, OverIt, OverWeak;
    }

    /// <summary>
    /// What one run was like, kept in memory in the editor and development builds and written out when the run ends, so the
    /// Deck playtests the Ralph loop's reports ask for can be counted from a file instead of from a screen recording
    /// (Ralph loop pass 9, 28 Nov 2026; report <c>docs/reports/playtest-log.md</c>). Plain C#: the game hands it plain
    /// facts through <c>PlaytestHooks</c>, and <see cref="PlaytestReport"/> turns it into the text.
    ///
    /// - Every event comes with a <see cref="Moment"/>: real seconds from the run's start (pauses included, so lines line
    ///   up with a recording) and game seconds (stopped by pauses, slowed by Focus). Graces, holds and how long things took
    ///   are measured in game seconds.
    /// - It says what happened, never whether it was fun, and counts, never percentages (the protocols ask for counts).
    /// - It is bounded: at most <see cref="MostLines"/> event lines, <see cref="MostAnimals"/> creatures and
    ///   <see cref="MostWarnings"/> warnings, and what is dropped is counted.
    /// - A hurt from a creature's strike is put down to the newest marked warning of that kind under way or just over
    ///   (<see cref="HurtGrace"/>), so each warning says whether it hurt; a hold's later bites (the Ambusher's, the
    ///   maw's) go to the creature that last struck (<see cref="HoldGrace"/>). The words of the hurt name the kind
    ///   (<see cref="StrikeWords.KindOf"/>).
    /// </summary>
    public sealed class PlaytestLog
    {
        public const int MostLines = 6000;
        public const int MostAnimals = 400;
        public const int MostWarnings = 2000;
        /// <summary>Distinct point labels, notes and hurt causes kept by name; the rest are counted as "other".</summary>
        public const int MostNames = 60;
        public const int MostAimLines = 40;
        /// <summary>Game seconds after a warning is over that a hurt of its kind is still its strike's (a bite landing as the stage ends).</summary>
        public const float HurtGrace = 1f;
        /// <summary>Game seconds after its last warning ended that a hold's bites still count against it (the Ambusher, the maw: <see cref="HoldsOn"/>).</summary>
        public const float HoldGrace = 15f;
        /// <summary>Game seconds after it was last after the player that a creature's fight is still on (the readout's fight hold).</summary>
        public const float FightHold = Tasks.ReadoutFocus.FightHold;
        /// <summary>A creature at its mark within this many degrees of where the player looked was ahead of them; at or past <see cref="BehindLeast"/>, behind.</summary>
        public const float AheadMost = 45f, BehindLeast = 135f;
        /// <summary>What a hurt with no words is filed under: an enemy's shot, mostly.</summary>
        public const string ShotsAndOther = "shots and anything else";
        /// <summary>The claims creatures pay on (<c>TaskIds.Creature</c>, <c>TaskIds.LandCreature</c>).</summary>
        public const string CreatureClaim = "creature:";
        /// <summary>Kept in the order the protocols name them, with the animal each is drawn from (the alien creatures' brief).</summary>
        public static readonly string[] Kinds = { "Strider", "Sprinter", "Roller", "Screamer", "Hunter", "Ambusher", "Stalker", "Maw", "Biter" };

        public static string AnimalOf(string kind)
        {
            switch (kind)
            {
                case "Strider": return "Japanese spider crab";
                case "Sprinter": return "camel spider";
                case "Roller": return "giant isopod";
                case "Screamer": return "cicada";
                case "Hunter": return "giant centipede, the Cave hunter";
                case "Ambusher": return "bobbit worm";
                case "Stalker": return "olm, the Blind stalker";
                case "Maw": return "megamouth shark, the Drifting maw";
                case "Biter": return "Goliath tigerfish, the Pack biter";
                default: return null;
            }
        }

        /// <summary>The kinds that hold on and bite again after their strike: the Ambusher drags, the maw bites in beats.</summary>
        public static bool HoldsOn(string kind) => kind == "Ambusher" || kind == "Maw";

        // ------------------------------------------------------------------ the run

        public string Scenario { get; private set; } = "";
        public int Seed { get; private set; }
        public string StartedAt { get; private set; } = "";
        public string Build { get; private set; } = "";
        public string Kit { get; private set; } = "";
        public string Upgrade { get; private set; } = "";
        public bool TipsOn { get; private set; }
        /// <summary>The profile's runs started, this one included; unlocks earned; the looks in use. -1 and empty when not given.</summary>
        public int RunNumber { get; private set; } = -1;
        public int UnlocksHeld { get; private set; } = -1;
        public string LooksInUse { get; private set; } = "";
        public float Length { get; private set; }
        public RunEnd End { get; private set; }
        public string EndWords { get; private set; } = "";
        public int AnteReached { get; private set; }
        public int Score { get; private set; }
        public bool Finished { get; private set; }
        public bool InTheRange => Scenario == "practice range";

        readonly List<LogLine> _lines = new List<LogLine>();
        public IReadOnlyList<LogLine> Lines => _lines;
        public int LinesDropped { get; private set; }

        float _pausedTotal, _pausedAt = -1f;
        public int Pauses { get; private set; }
        /// <summary>Real seconds spent paused, up to the log's length.</summary>
        public float PausedSeconds => _pausedTotal + (_pausedAt >= 0f && Length > _pausedAt ? Length - _pausedAt : 0f);

        readonly List<AnteRecord> _antes = new List<AnteRecord>();
        public IReadOnlyList<AnteRecord> Antes => _antes;

        readonly Dictionary<string, Tally> _paid = new Dictionary<string, Tally>();
        readonly Dictionary<string, Tally> _refused = new Dictionary<string, Tally>();
        public IReadOnlyDictionary<string, Tally> Paid => _paid;
        public IReadOnlyDictionary<string, Tally> Refused => _refused;
        public int Awards { get; private set; }
        public long PointsAwarded { get; private set; }

        readonly Dictionary<string, Tally> _notes = new Dictionary<string, Tally>();
        /// <summary>The notes the readout really showed, by their words.</summary>
        public IReadOnlyDictionary<string, Tally> Notes => _notes;
        public int NotesShown { get; private set; }
        /// <summary>Notes handed to the readout; a plain one offered during an important one's hold may be replaced before it shows.</summary>
        public int NotesOffered { get; private set; }

        readonly List<string> _unlocks = new List<string>();
        public IReadOnlyList<string> Unlocks => _unlocks;

        readonly Dictionary<string, GunTally> _guns = new Dictionary<string, GunTally>();
        public IReadOnlyDictionary<string, GunTally> Guns => _guns;
        public int PeopleHits { get; private set; }
        public int PeopleHeads { get; private set; }
        public int PeopleLimbsOff { get; private set; }
        public int PeopleDisarmed { get; private set; }
        public int PeopleKills { get; private set; }
        public int PeopleHeadKills { get; private set; }
        public int Sightings { get; private set; }
        public int SightingsWhileHidden { get; private set; }
        public int SquadsSent { get; private set; }
        public int Alarms { get; private set; }

        readonly List<AnimalRecord> _animals = new List<AnimalRecord>();
        readonly Dictionary<object, AnimalRecord> _byWho = new Dictionary<object, AnimalRecord>();
        readonly Dictionary<string, int> _numbers = new Dictionary<string, int>();
        readonly Dictionary<string, AnimalRecord> _byClaim = new Dictionary<string, AnimalRecord>();
        // A creature's pay can be heard before its kill (whoever hears the hit first): kept until the kill comes.
        readonly Dictionary<string, int> _claimPaid = new Dictionary<string, int>();
        readonly Dictionary<string, string> _claimRefused = new Dictionary<string, string>();
        readonly List<object> _letGo = new List<object>();
        public IReadOnlyList<AnimalRecord> Animals => _animals;
        public int AnimalsDropped { get; private set; }

        readonly List<WarningRecord> _warnings = new List<WarningRecord>();
        readonly Dictionary<AnimalRecord, WarningRecord> _open = new Dictionary<AnimalRecord, WarningRecord>();
        public IReadOnlyList<WarningRecord> Warnings => _warnings;
        public int WarningsDropped { get; private set; }

        readonly Dictionary<string, int> _aimLineCount = new Dictionary<string, int>();
        public IReadOnlyDictionary<string, int> AimLines => _aimLineCount;
        readonly List<AimLineRecord> _aimLines = new List<AimLineRecord>();
        public IReadOnlyList<AimLineRecord> AimLineRecords => _aimLines;

        readonly Dictionary<string, Tally> _hurt = new Dictionary<string, Tally>();
        public IReadOnlyDictionary<string, Tally> HurtBy => _hurt;
        /// <summary>Hurts in a creature's words with no marked warning of that kind under way or just over: a strike that warned nobody.</summary>
        public int UnwarnedHurts { get; private set; }
        readonly List<string> _deaths = new List<string>();
        public IReadOnlyList<string> Deaths => _deaths;
        AnimalRecord _lastHurtBy;
        float _lastHurtGame = -1000f;
        float _enemySawGame = -1000f;

        readonly double[] _bandSeconds = new double[8];
        public double SecondsIn(IntensityBand band) => (int)band >= 0 && (int)band < _bandSeconds.Length ? _bandSeconds[(int)band] : 0.0;
        public int BandChanges { get; private set; }
        public int ThinTimes { get; private set; }
        public int MostLayers { get; private set; }
        public int LayerChanges { get; private set; }
        public int FightMusicTimes { get; private set; }
        public int SomeoneAfterTimes { get; private set; }
        readonly List<string> _pieces = new List<string>();
        public IReadOnlyList<string> Pieces => _pieces;
        readonly List<string> _entries = new List<string>();
        /// <summary>Where each fight's music came in: "0:41.2 Night Shift, bar 17".</summary>
        public IReadOnlyList<string> FightEntries => _entries;
        int _band = -1;
        float _bandSince;
        bool _musicHeld;
        string _piece;
        bool _thin, _fightMusic, _someoneAfter;

        // ------------------------------------------------------------------ beginning and end

        /// <summary>Starts the log of a run afresh.</summary>
        public void Begin(string scenario, int seed, string startedAt, string build, string kit, string upgrade, bool tipsOn)
        {
            Scenario = scenario ?? "";
            Seed = seed;
            StartedAt = startedAt ?? "";
            Build = build ?? "";
            Kit = kit ?? "";
            Upgrade = upgrade ?? "";
            TipsOn = tipsOn;
            Line(0f, "run begins: " + Scenario + ", seed " + Seed.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>The player's profile as the run began (design review, pass 9: which run of theirs it was, and what they had).</summary>
        public void Profile(int runNumber, int unlocksHeld, string looksInUse)
        {
            RunNumber = runNumber;
            UnlocksHeld = unlocksHeld;
            LooksInUse = looksInUse ?? "";
        }

        /// <summary>The run is over (once: a second call is ignored). What is still under way is closed at <paramref name="t"/>.</summary>
        public void Finish(Moment t, RunEnd end, string words, int anteReached, int score)
        {
            if (Finished) return;
            Finished = true;
            Length = t.Real;
            End = end;
            EndWords = words ?? "";
            AnteReached = anteReached;
            Score = score;
            CloseBand(t.Real);
            Line(t.Real, "run ends: " + EndPhrase(end) + (string.IsNullOrEmpty(EndWords) ? "" : " (" + EndWords + ")"));
        }

        /// <summary>
        /// The log so far, for writing early (F7, a pause) without ending it: <see cref="Length"/> and the time in the
        /// current music band are counted up to <paramref name="t"/>, and go on counting after.
        /// </summary>
        public void Snapshot(Moment t)
        {
            if (Finished) return;
            Length = t.Real;
            CloseBand(t.Real);
        }

        public static string EndPhrase(RunEnd end)
        {
            switch (end)
            {
                case RunEnd.Died: return "died";
                case RunEnd.RunOver: return "run over";
                case RunEnd.Left: return "left the run";
                case RunEnd.Closed: return "game closed";
                default: return "still going";
            }
        }

        /// <summary>The game was paused (<paramref name="on"/>) or play went on.</summary>
        public void Paused(Moment t, bool on)
        {
            if (on)
            {
                if (_pausedAt >= 0f) return;
                _pausedAt = t.Real;
                Pauses++;
                Line(t.Real, "paused");
                return;
            }
            if (_pausedAt < 0f) return;
            float held = Math.Max(0f, t.Real - _pausedAt);
            _pausedTotal += held;
            _pausedAt = -1f;
            Line(t.Real, "play goes on (paused " + Seconds(held) + ")");
        }

        // ------------------------------------------------------------------ antes and points

        public void AnteBegan(Moment t, int ante, int target)
        {
            _antes.Add(new AnteRecord { Ante = ante, Target = target, BeganAt = t.Real });
            Line(t.Real, "ante " + N(ante) + " begins: " + N(target) + " to bank");
        }

        AnteRecord AnteOf(int ante)
        {
            for (int i = _antes.Count - 1; i >= 0; i--) if (_antes[i].Ante == ante) return _antes[i];
            return null;
        }

        /// <summary>The ante's clock began to run (ante 1's waits on the pad until liftoff, the first point or a minute).</summary>
        public void ClockStarted(Moment t, int ante)
        {
            AnteRecord a = AnteOf(ante);
            if (a == null || a.ClockFrom >= 0f) return;
            a.ClockFrom = t.Real;
            Line(t.Real, "ante " + N(ante) + ": its clock starts");
        }

        public void TargetMet(Moment t, int ante, int banked, float secondsLeft = -1f)
        {
            AnteRecord a = AnteOf(ante);
            if (a != null && a.TargetMetAt < 0f)
            {
                a.TargetMetAt = t.Real;
                a.LeftAtTarget = secondsLeft;
            }
            Line(t.Real, "ante " + N(ante) + ": target met (" + N(banked) + " banked" + (secondsLeft >= 0f ? ", " + Minutes(secondsLeft) + " left on its clock" : "") + ")");
        }

        public void Overtime(Moment t, int ante, bool on)
        {
            AnteRecord a = AnteOf(ante);
            if (on && a != null && a.OvertimeAt < 0f) a.OvertimeAt = t.Real;
            Line(t.Real, "ante " + N(ante) + (on ? ": overtime begins" : ": overtime ends"));
        }

        public void AnteCleared(Moment t, int ante, int banked, float secondsLeft = -1f)
        {
            AnteRecord a = AnteOf(ante);
            if (a != null && a.ClearedAt < 0f)
            {
                a.ClearedAt = t.Real;
                a.Banked = banked;
                a.LeftAtClear = secondsLeft;
            }
            Line(t.Real, "ante " + N(ante) + " cleared: " + N(banked) + " banked" + (secondsLeft >= 0f ? ", " + Minutes(secondsLeft) + " left on its clock" : ""));
        }

        /// <summary>A perk was taken (in the field or at the break).</summary>
        public void PerkTaken(Moment t, int ante, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            AnteRecord a = AnteOf(ante);
            if (a != null && a.Perks.Count < MostNames) a.Perks.Add(name);
            Line(t.Real, "perk taken: " + name);
        }

        /// <summary>One submission to the ante's score: awarded, or refused and why (<paramref name="outcome"/>).</summary>
        public void Points(Moment t, int ante, string kind, string label, int points, bool awarded, string outcome, int feats, string claim)
        {
            label = string.IsNullOrEmpty(label) ? kind ?? "points" : label;
            bool creature = claim != null && claim.StartsWith(CreatureClaim, StringComparison.Ordinal);
            if (creature) Answer(claim, awarded ? points : 0, awarded ? null : outcome ?? "refused");
            // A creature's kill and a camp's can carry the same label: kept apart (design review, pass 9).
            string key = creature ? "creature " + label : label;
            if (awarded)
            {
                Awards++;
                PointsAwarded += points;
                Tally paid = Named(_paid, key);
                paid.Count++;
                paid.Total += points;
                if (!NoRoom())
                    _lines.Add(new LogLine(t.Real, "+" + N(points) + " " + label + " (" + kind + (feats > 0 ? ", " + N(feats) + (feats == 1 ? " feat" : " feats") : "") + ", claim " + claim + ")"));
            }
            else
            {
                Tally refused = Named(_refused, outcome ?? "refused");
                refused.Count++;
                if (!NoRoom()) _lines.Add(new LogLine(t.Real, "not paid: " + label + " (" + kind + ", " + outcome + ", claim " + claim + ")"));
            }
        }

        /// <summary>The ante's answer for a creature's claim, put on its record now or when its kill is heard.</summary>
        void Answer(string claim, int points, string refused)
        {
            if (_byClaim.TryGetValue(claim, out AnimalRecord a))
            {
                a.PaidPoints = refused == null ? points : 0;
                a.PayRefused = refused;
                return;
            }
            if (_claimPaid.Count >= MostAnimals) return;
            if (refused == null) _claimPaid[claim] = points;
            else _claimRefused[claim] = refused;
        }

        /// <summary>A line handed to the ante readout's note (it may wait, and be replaced, behind an important one).</summary>
        public void Note(Moment t, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            NotesOffered++;
        }

        /// <summary>A note the readout really showed.</summary>
        public void NoteShown(Moment t, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            NotesShown++;
            Named(_notes, text).Count++;
            Line(t.Real, "note: " + text);
        }

        public void UnlockMet(Moment t, string name, bool inAFight, bool atTheBreak)
        {
            string line = "unlock earned: " + name + (atTheBreak ? " (said at the ante break)" : inAFight ? " (in a fight: its moment waits)" : "");
            _unlocks.Add(Clock(t.Real) + "  " + line);
            Line(t.Real, line);
        }

        public void UnlockShown(Moment t, string name, float waited)
        {
            string line = "unlock moment shown: " + name + " (after " + Seconds(waited) + ")";
            _unlocks.Add(Clock(t.Real) + "  " + line);
            Line(t.Real, line);
        }

        // ------------------------------------------------------------------ guns and people

        public GunTally Gun(string gun)
        {
            gun = string.IsNullOrEmpty(gun) ? "gun" : gun;
            if (!_guns.TryGetValue(gun, out GunTally g))
            {
                g = new GunTally();
                _guns[gun] = g;
            }
            return g;
        }

        /// <summary>The player fired: counted for the gun, and for every creature's warning under way.</summary>
        public void Shot(Moment t, string gun)
        {
            Gun(gun).Shots++;
            foreach (KeyValuePair<AnimalRecord, WarningRecord> w in _open) w.Value.Shots++;
        }

        /// <summary>A hit on a living enemy person (a camp's): counted, and a line only when something came of it.</summary>
        public void PersonHit(Moment t, string gun, string part, bool head, bool severed, bool disarmed, bool killed, bool headKill, float range)
        {
            GunTally g = Gun(gun);
            g.Hits++;
            PeopleHits++;
            if (head) PeopleHeads++;
            if (severed) PeopleLimbsOff++;
            if (disarmed) PeopleDisarmed++;
            if (killed)
            {
                g.Kills++;
                PeopleKills++;
                if (headKill) PeopleHeadKills++;
            }
            if (!severed && !disarmed && !killed) return;
            if (NoRoom()) return;
            string what = killed ? (headKill ? "killed a person through the head" : "killed a person") : disarmed ? "shot a gun out of a person's hands" : "shot a person's " + Lower(part) + " off";
            _lines.Add(new LogLine(t.Real, what + " at " + Metres(range) + ", " + (string.IsNullOrEmpty(gun) ? "gun" : gun)));
        }

        /// <summary>An armed enemy picked the player out (critique loop round 1's sting plays only when they were hidden).</summary>
        public void Spotted(Moment t, bool hidden)
        {
            Sightings++;
            if (hidden) SightingsWhileHidden++;
            if (hidden) Line(t.Real, "an enemy picks you out (you were hidden: its sting plays)");
        }

        public void SquadSent(Moment t)
        {
            SquadsSent++;
            Line(t.Real, "a restless squad is sent to look for you");
        }

        /// <summary>An alarm called enemies (a mast's call, a supply drop, a flare, an overtime hunt): <paramref name="from"/> metres from the player.</summary>
        public void Alarm(Moment t, float radius, float from)
        {
            Alarms++;
            if (!NoRoom()) _lines.Add(new LogLine(t.Real, "an alarm calls enemies within " + Metres(radius) + " of a place " + Metres(from) + " from you"));
        }

        // ------------------------------------------------------------------ creatures

        /// <summary>The record of <paramref name="who"/>, or null if it has none.</summary>
        public AnimalRecord Find(object who) => who != null && _byWho.TryGetValue(who, out AnimalRecord a) ? a : null;

        /// <summary>What a group of <paramref name="kind"/> is called in the log: "Biter pack".</summary>
        public static string PackOf(string kind) => kind + " pack";

        /// <summary>The kind a pack's fish are (<see cref="PackOf"/> the other way round).</summary>
        public static string MemberOf(string packKind)
        {
            const string Pack = " pack";
            return packKind != null && packKind.EndsWith(Pack, StringComparison.Ordinal) ? packKind.Substring(0, packKind.Length - Pack.Length) : packKind;
        }

        /// <summary>
        /// The record of <paramref name="who"/>, made the first time it is met. Null past <see cref="MostAnimals"/>. A
        /// <paramref name="group"/> not met yet (a fish made after its pack was laid out) is met with it.
        /// </summary>
        public AnimalRecord Meet(object who, string kind, string world, int ante, object group)
        {
            if (who == null) return null;
            if (_byWho.TryGetValue(who, out AnimalRecord known)) return known;
            kind = string.IsNullOrEmpty(kind) ? "creature" : kind;
            AnimalRecord pack = null;
            if (group != null)
            {
                pack = Find(group);
                if (pack == null)
                {
                    pack = Meet(group, PackOf(kind), world, ante, null);
                    if (pack != null) pack.IsGroup = true;
                }
            }
            if (_animals.Count >= MostAnimals)
            {
                AnimalsDropped++;
                return null;
            }
            _numbers.TryGetValue(kind, out int n);
            _numbers[kind] = ++n;
            var a = new AnimalRecord { Number = n, Kind = kind, World = world ?? "", Ante = ante, Group = pack, InTheRange = pack != null && pack.InTheRange };
            _animals.Add(a);
            _byWho[who] = a;
            return a;
        }

        /// <summary>
        /// A world's spawner woke an animal (or a pack) <paramref name="from"/> metres from the player, or the practice
        /// range laid it out (<paramref name="inTheRange"/>).
        /// </summary>
        public void Woke(Moment t, object who, string kind, string world, int ante, float from, bool group, bool inTheRange = false)
        {
            AnimalRecord a = Meet(who, kind, world, ante, null);
            if (a == null) return;
            a.IsGroup = group;
            a.InTheRange = inTheRange;
            a.WokeAt = t.Real;
            a.WokeFrom = from;
            Line(t.Real, a.Name + (inTheRange ? " laid out in the practice range" : " woke on " + a.World) + ", " + Metres(from) + " off");
        }

        /// <summary>It came into the frame of the player's view, the ground not between them.</summary>
        public void Seen(Moment t, object who, float from)
        {
            AnimalRecord a = Find(who);
            if (a == null || a.SeenAt >= 0f) return;
            a.SeenAt = t.Real;
            a.SeenFrom = from;
            // A pack is in view when any fish of it is.
            AnimalRecord pack = a.Group;
            if (pack != null && pack.SeenAt < 0f)
            {
                pack.SeenAt = t.Real;
                pack.SeenFrom = from;
            }
            Line(t.Real, a.Name + " comes into your view, " + Metres(from) + " off");
        }

        /// <summary>It knows where the player is and can hurt them (said a few times a second while it does).</summary>
        public void Noticed(Moment t, object who)
        {
            AnimalRecord a = Find(who);
            if (a == null || a.End != AnimalEnd.StillThere) return;
            Active(a, t);
            if (a.Group != null && a.Group.NoticedAt < 0f) a.Group.NoticedAt = t.Real;
            if (a.NoticedAt >= 0f) return;
            a.NoticedAt = t.Real;
            Line(t.Real, a.Name + " is after you");
        }

        void Active(AnimalRecord a, Moment t)
        {
            Begin(a, t);
            if (a.Group != null) Begin(a.Group, t);
        }

        void Begin(AnimalRecord a, Moment t)
        {
            a.ActiveAt = t.Game;
            if (a.FightFrom >= 0f) return;
            a.FightFrom = t.Real;
            a.FightFromGame = t.Game;
            // Which came first, people or the creature (design review, pass 9): a camp drawn by the creature's noise reads
            // differently from a creature walking into a fight already on.
            a.PeopleFirst = t.Game - _enemySawGame <= FightHold;
        }

        /// <summary>
        /// A hit by the player on a creature. For a kill, <paramref name="claim"/> is what it pays on (null where nothing
        /// pays for it) and <paramref name="inReach"/> whether it could answer the player then (the game's own test).
        /// </summary>
        public void CreatureHit(Moment t, object who, string kind, string world, int ante, object group, string piece, HitZone zone,
            bool pieceOff, bool killed, string gun, float range, string claim = null, bool inReach = true)
        {
            GunTally g = Gun(gun);
            g.Hits++;
            AnimalRecord a = Meet(who, kind, world, ante, group);
            if (a == null)
            {
                if (killed) g.Kills++;
                return;
            }
            int z = (int)zone;
            if (z < 0 || z >= a.Hits.Length) z = 0;
            if (a.FirstHitAt < 0f)
            {
                a.FirstHitAt = t.Real;
                a.FirstHitGame = t.Game;
            }
            Active(a, t);
            a.Hits[z]++;
            if (pieceOff) a.PiecesOff++;
            AnimalRecord pack = a.Group;
            if (pack != null)
            {
                pack.Hits[z]++;
                if (pack.FirstHitAt < 0f)
                {
                    pack.FirstHitAt = t.Real;
                    pack.FirstHitGame = t.Game;
                }
            }
            // A line for a hit that did something, and for the first on each animal; the rest are only counted, so
            // full-auto into a lid neither fills the log nor makes garbage (deck-performance review, pass 9).
            bool worthALine = killed || pieceOff || zone == HitZone.Vital || zone == HitZone.WeaponOff || a.TotalHits == 1;
            if (!worthALine) return;
            piece = PieceWords(piece, a.Kind);
            if (!NoRoom())
                _lines.Add(new LogLine(t.Real, "hit " + a.Name + "'s " + piece + " (" + ZoneWords(zone) + ") at " + Metres(range) + ", "
                                               + (string.IsNullOrEmpty(gun) ? "gun" : gun) + (pieceOff && !killed ? ", it came off" : "")));
            if (!killed || a.End == AnimalEnd.KilledByYou) return;
            g.Kills++;
            a.End = AnimalEnd.KilledByYou;
            a.EndAt = t.Real;
            a.EndGame = t.Game;
            // A creature that dies stops thinking, so its warning would stay under way to the end of the log.
            if (_open.TryGetValue(a, out WarningRecord during))
            {
                during.KilledDuring = true;
                during.OverAt = t.Real;
                during.OverGame = t.Game;
                _open.Remove(a);
                Line(t.Real, a.Name + " is killed " + (during.Marked ? "after its mark" : "before its mark"));
            }
            a.KillZone = zone;
            a.KillPiece = piece;
            a.Claim = claim;
            a.OutOfReach = claim != null && !inReach;
            if (claim != null)
            {
                _byClaim[claim] = a;
                if (_claimPaid.TryGetValue(claim, out int points))
                {
                    a.PaidPoints = points;
                    _claimPaid.Remove(claim);
                }
                else if (_claimRefused.TryGetValue(claim, out string refused))
                {
                    a.PaidPoints = 0;
                    a.PayRefused = refused;
                    _claimRefused.Remove(claim);
                }
            }
            if (pack != null) pack.Kills++;
            Line(t.Real, a.Name + " killed through its " + piece + " (" + ZoneWords(zone) + "), " + Seconds(t.Game - a.FirstHitGame) + " of game time after your first hit on it"
                         + (claim == null ? "; nothing pays for it here" : a.OutOfReach ? "; it could not answer you there" : ""));
        }

        /// <summary>
        /// A creature's warning changed stage (<see cref="WindupChange"/>). <paramref name="facing"/> is the degrees between
        /// where the player looked and the creature; <paramref name="range"/> its distance in metres; <paramref name="inFrame"/>
        /// whether it was inside the edges of the view.
        /// </summary>
        public void Warning(Moment t, object who, string kind, string world, int ante, object group, WindupStage was, WindupStage now,
            float facing, float range, bool inFrame = false)
        {
            if (was == now) return;
            AnimalRecord a = Meet(who, kind, world, ante, group);
            if (a == null) return;
            Active(a, t);
            _open.TryGetValue(a, out WarningRecord w);
            // A wind-up straight after a strike (or after an aim it let go of) is a new warning, as the visor tells it
            // (WindupChange.Began): the one before is over.
            if (w != null && Windups.Change(was, now).Began)
            {
                Close(w, t);
                w = null;
            }
            if (w == null)
            {
                if (now == WindupStage.None) return;
                if (_warnings.Count >= MostWarnings)
                {
                    WarningsDropped++;
                    return;
                }
                w = new WarningRecord { Animal = a, BeganAt = t.Real, BeganGame = t.Game, FacingAtBegin = facing };
                _warnings.Add(w);
                _open[a] = w;
                a.Warnings++;
                if (!NoRoom()) _lines.Add(new LogLine(t.Real, a.Name + " begins a warning, " + Degrees(facing) + " off where you look, " + Metres(range) + " off"));
            }
            if ((now == WindupStage.Aimed || now == WindupStage.Striking) && w.AimedAt < 0f)
            {
                w.AimedAt = t.Real;
                w.AimedGame = t.Game;
                w.FacingAtMark = facing;
                w.RangeAtMark = range;
                w.InFrameAtMark = inFrame;
                w.AcrossAtMark = w.Sampled && w.Across >= DodgeRules.SidestepSpeed;
                if (!NoRoom())
                    _lines.Add(new LogLine(t.Real, a.Name + " takes aim (its mark), " + Degrees(facing) + " off where you look" + (inFrame ? ", in your view" : "") + ", " + Metres(range) + " off"
                                                   + (w.AcrossAtMark ? "; you are already moving across" : "")));
            }
            if (now == WindupStage.Striking && w.StruckAt < 0f)
            {
                w.StruckAt = t.Real;
                w.StruckGame = t.Game;
                if (!NoRoom()) _lines.Add(new LogLine(t.Real, a.Name + " strikes"));
            }
            if (now != WindupStage.None) return;
            Close(w, t);
        }

        void Close(WarningRecord w, Moment t)
        {
            w.OverAt = t.Real;
            w.OverGame = t.Game;
            _open.Remove(w.Animal);
            if (!NoRoom()) _lines.Add(new LogLine(t.Real, w.Animal.Name + (!w.Marked ? " calls its warning off before its mark" : w.StruckAt >= 0f ? "'s strike is over" : " stops after its mark without striking")));
        }

        /// <summary>
        /// The player's speed across the line to <paramref name="who"/>, along the ground, this frame of <paramref name="dt"/>
        /// game seconds (told every frame while it warns: the Sprinter's bite comes 0.39 s after its mark). False once it has
        /// no warning under way, so the game can stop telling.
        /// </summary>
        public bool Across(Moment t, object who, float speed, float dt)
        {
            AnimalRecord a = Find(who);
            if (a == null || !_open.TryGetValue(a, out WarningRecord w)) return false;
            if (!(dt > 0f)) return true;
            w.Across = w.Sampled ? DodgeRules.Smooth(w.Across, speed, dt) : speed;
            w.Sampled = true;
            if (w.Across < DodgeRules.SidestepSpeed || !w.Marked || w.AcrossAtMark || w.MovedGame >= 0f) return true;
            w.MovedAt = t.Real;
            w.MovedGame = t.Game;
            if (!NoRoom())
                _lines.Add(new LogLine(t.Real, "you move across " + a.Name + "'s line, " + Seconds(t.Game - w.AimedGame) + " after its mark"
                                               + (w.StruckGame >= 0f ? " (" + Seconds(t.Game - w.StruckGame) + " after it struck)" : "")));
            return true;
        }

        /// <summary>
        /// The line over the crosshair named <paramref name="kind"/>'s armour and where to aim (pass 7), after a hit on
        /// <paramref name="who"/> (heard before the hit itself: the marker hears hits first).
        /// </summary>
        public void AimLine(Moment t, string kind, object who = null, string world = null, int ante = 0, object group = null)
        {
            if (string.IsNullOrEmpty(kind)) return;
            _aimLineCount.TryGetValue(kind, out int n);
            _aimLineCount[kind] = n + 1;
            AnimalRecord a = who != null ? Meet(who, kind, world, ante, group) : null;
            if (a != null) a.AimLineShown = true;
            if (_aimLines.Count < MostAimLines) _aimLines.Add(new AimLineRecord { At = t.Real, Kind = kind, Name = a != null ? a.Name : kind });
            Line(t.Real, "aim line shown for the " + kind + (a != null ? " (" + a.Name + ")" : ""));
        }

        /// <summary>One look under the newest aim line while it shows: was the creature it named under it, and its weak place.</summary>
        public void AimLineLook(bool overIt, bool overWeak)
        {
            if (_aimLines.Count == 0) return;
            AimLineRecord r = _aimLines[_aimLines.Count - 1];
            r.Looks++;
            if (overIt) r.OverIt++;
            if (overWeak) r.OverWeak++;
        }

        /// <summary>The player lost armour: <paramref name="cause"/> is the death page's words for it (null for a shot).</summary>
        public void Hurt(Moment t, float amount, string cause)
        {
            if (!(amount > 0f)) return;
            Tally hurt = Named(_hurt, string.IsNullOrEmpty(cause) ? ShotsAndOther : cause);
            hurt.Count++;
            hurt.Total += amount;
            string kind = StrikeWords.KindOf(cause);
            if (kind == null) return;
            WarningRecord w = WarningFor(kind, t);
            AnimalRecord a = w != null ? w.Animal : HoldsOn(kind) ? LastWarned(kind, t) : null;
            if (w != null)
            {
                w.Damage += amount;
                w.Beats++;
            }
            if (a == null)
            {
                UnwarnedHurts++;
                if (!NoRoom()) _lines.Add(new LogLine(t.Real, "hurt " + Amount(amount) + " by a " + kind + " with no warning under way (\"" + cause + "\")"));
                return;
            }
            a.DamageToYou += amount;
            if (a.Group != null) a.Group.DamageToYou += amount;
            Active(a, t);
            _lastHurtBy = a;
            _lastHurtGame = t.Game;
            if (!NoRoom()) _lines.Add(new LogLine(t.Real, "hurt " + Amount(amount) + " by " + a.Name + (w != null ? "'s strike" : ", holding on") + " (\"" + cause + "\")"));
        }

        /// <summary>
        /// The newest warning of <paramref name="kind"/> that has given its mark and is still under way or over within
        /// <see cref="HurtGrace"/>. Only a marked one: a strike always comes after its mark, so a fresh wind-up begun the
        /// frame the last strike landed never takes that strike's hurt.
        /// </summary>
        WarningRecord WarningFor(string kind, Moment t)
        {
            for (int i = _warnings.Count - 1; i >= 0; i--)
            {
                WarningRecord w = _warnings[i];
                if (t.Game - w.BeganGame > 120f) break;
                if (w.Animal.Kind != kind || !w.Marked) continue;
                if (w.OverAt < 0f || t.Game - w.OverGame <= HurtGrace) return w;
            }
            return null;
        }

        /// <summary>The creature of <paramref name="kind"/> whose marked warning ended last, within <see cref="HoldGrace"/>.</summary>
        AnimalRecord LastWarned(string kind, Moment t)
        {
            for (int i = _warnings.Count - 1; i >= 0; i--)
            {
                WarningRecord w = _warnings[i];
                if (t.Game - w.BeganGame > 120f) break;
                if (w.Animal.Kind == kind && w.Marked && w.OverAt >= 0f && t.Game - w.OverGame <= HoldGrace) return w.Animal;
            }
            return null;
        }

        /// <summary>The player died (in the practice range, was knocked down), in the death page's words.</summary>
        public void Died(Moment t, string cause)
        {
            cause = string.IsNullOrEmpty(cause) ? "no cause given" : cause;
            _deaths.Add(Clock(t.Real) + "  " + cause);
            string kind = StrikeWords.KindOf(cause);
            if (kind != null && _lastHurtBy != null && _lastHurtBy.Kind == kind && t.Game - _lastHurtGame <= 5f && _lastHurtBy.End == AnimalEnd.StillThere)
            {
                _lastHurtBy.End = AnimalEnd.KilledYou;
                _lastHurtBy.EndAt = t.Real;
                _lastHurtBy.EndGame = t.Game;
                AnimalRecord pack = _lastHurtBy.Group;
                if (pack != null && pack.End == AnimalEnd.StillThere)
                {
                    pack.End = AnimalEnd.KilledYou;
                    pack.EndAt = t.Real;
                    pack.EndGame = t.Game;
                }
            }
            Line(t.Real, (InTheRange ? "you are knocked down: \"" : "you died: \"") + cause + "\"");
        }

        /// <summary>An enemy has the player in sight: every creature whose fight is on has had people join it.</summary>
        public void EnemySawYou(Moment t)
        {
            _enemySawGame = t.Game;
            for (int i = _animals.Count - 1; i >= 0; i--)
            {
                AnimalRecord a = _animals[i];
                if (a.EnemySawYou || a.End != AnimalEnd.StillThere || a.ActiveAt < 0f || t.Game - a.ActiveAt > FightHold) continue;
                a.EnemySawYou = true;
                if (a.Group != null) a.Group.EnemySawYou = true;
                Line(t.Real, "an enemy has you in sight during " + a.Name + "'s fight");
            }
        }

        /// <summary>
        /// The player left it behind and its spawner put it away: its record is kept, its warning ends, and the log lets go
        /// of the object (it is destroyed straight after), and of a pack's fish with it, so a long run holds no destroyed
        /// creatures.
        /// </summary>
        public void PutAway(Moment t, object who, string why = null)
        {
            AnimalRecord a = Find(who);
            if (a == null) return;
            if (_open.TryGetValue(a, out WarningRecord w)) Close(w, t);
            LetGo(who, a, t);
            if (a.End != AnimalEnd.StillThere) return;
            a.End = AnimalEnd.PutAway;
            a.EndAt = t.Real;
            a.EndGame = t.Game;
            Line(t.Real, a.Name + " is put away (" + (string.IsNullOrEmpty(why) ? "you left it behind" : why) + ")");
        }

        /// <summary>A pack's last fish died (the pack then goes): the pack's record ends, and the log lets go of it.</summary>
        public void GroupGone(Moment t, object who)
        {
            AnimalRecord a = Find(who);
            if (a == null) return;
            LetGo(who, a, t);
            if (a.End != AnimalEnd.StillThere) return;
            a.End = AnimalEnd.AllDead;
            a.EndAt = t.Real;
            a.EndGame = t.Game;
            Line(t.Real, a.Name + ": every fish of it is dead");
        }

        /// <summary>Lets go of a creature's object, and of a pack's fish, ending any warning still under way.</summary>
        void LetGo(object who, AnimalRecord a, Moment t)
        {
            _byWho.Remove(who);
            if (!a.IsGroup) return;
            _letGo.Clear();
            foreach (KeyValuePair<object, AnimalRecord> k in _byWho) if (k.Value.Group == a) _letGo.Add(k.Key);
            for (int i = 0; i < _letGo.Count; i++)
            {
                if (_byWho.TryGetValue(_letGo[i], out AnimalRecord fish) && _open.TryGetValue(fish, out WarningRecord w)) Close(w, t);
                _byWho.Remove(_letGo[i]);
            }
            _letGo.Clear();
        }

        /// <summary>
        /// The frames after it woke: <paramref name="ownMs"/> the wake frame's own length, <paramref name="worstMs"/> the
        /// longest in the half second after, <paramref name="baselineMs"/> the middle frame of the second before;
        /// <paramref name="firstOfKind"/> when it was the first of its kind this session (its model and pictures may have
        /// been loaded in that frame).
        /// </summary>
        public void WakeFrame(object who, float ownMs, float worstMs, float baselineMs, bool firstOfKind)
        {
            AnimalRecord a = Find(who);
            if (a == null) return;
            a.WakeFrameOwnMs = ownMs;
            a.WakeFrameMs = worstMs;
            a.WakeBaselineMs = baselineMs;
            a.WakeFirstOfKind = firstOfKind;
        }

        /// <summary>The half second after it woke could not be timed: a pause, or a write of this log, fell in it.</summary>
        public void WakeUntimed(object who)
        {
            AnimalRecord a = Find(who);
            if (a != null) a.WakeUntimed = true;
        }

        public void Slips(object who, int slips)
        {
            AnimalRecord a = Find(who);
            if (a != null) a.Slips = slips;
        }

        // ------------------------------------------------------------------ music

        /// <summary>
        /// The music's intensity band (what it aims for; the layers heard follow a bar or two later), piece, layers playing
        /// and whether it thins on the last plates, read a few times a second. While <paramref name="paused"/> (the pause
        /// menu stops the music) no band's time is counted.
        /// </summary>
        public void Music(Moment t, IntensityBand band, string piece, int layers, bool thin, bool paused = false)
        {
            float now = t.Real;
            if (paused)
            {
                if (!_musicHeld) CloseBand(now);
                _musicHeld = true;
                return;
            }
            if (_musicHeld)
            {
                _musicHeld = false;
                _bandSince = now;
            }
            int b = (int)band;
            if (b < 0 || b >= _bandSeconds.Length) return;
            if (layers > MostLayers) MostLayers = layers;
            if (b != _band)
            {
                CloseBand(now);
                if (_band >= 0) BandChanges++;
                _band = b;
                Line(now, "music aims for " + band + " (" + N(layers) + (layers == 1 ? " layer playing)" : " layers playing)"));
            }
            if (!string.IsNullOrEmpty(piece) && piece != _piece)
            {
                _piece = piece;
                if (_pieces.Count < MostNames) _pieces.Add(piece);
                Line(now, "music piece: " + piece);
            }
            if (thin && !_thin)
            {
                ThinTimes++;
                Line(now, "music thins: the suit's pulse on the last plates");
            }
            _thin = thin;
        }

        /// <summary>The layers heard changed: <paramref name="on"/> names those playing now.</summary>
        public void MusicLayers(Moment t, string on)
        {
            LayerChanges++;
            Line(t.Real, "music layers now: " + (string.IsNullOrEmpty(on) ? "none" : on));
        }

        /// <summary>The fight music, and the music's sense of someone after the player (it keeps a piece from resting), as heard.</summary>
        public void MusicState(Moment t, bool fightMusic, bool someoneAfter)
        {
            if (fightMusic != _fightMusic)
            {
                _fightMusic = fightMusic;
                if (fightMusic) FightMusicTimes++;
                Line(t.Real, fightMusic ? "the fight music comes in" : "the fight music lets go");
            }
            if (someoneAfter != _someoneAfter)
            {
                _someoneAfter = someoneAfter;
                if (someoneAfter) SomeoneAfterTimes++;
                Line(t.Real, someoneAfter ? "the music hears someone after you" : "the music hears nobody after you");
            }
        }

        /// <summary>Where a fight's music came in: its piece and bar (counted from 1).</summary>
        public void MusicEntry(Moment t, string piece, int bar)
        {
            string words = (string.IsNullOrEmpty(piece) ? "the fight piece" : piece) + ", bar " + N(bar);
            if (_entries.Count < MostNames) _entries.Add(Clock(t.Real) + " " + words);
            Line(t.Real, "the fight music came in at " + words);
        }

        void CloseBand(float now)
        {
            if (_band >= 0 && !_musicHeld && now > _bandSince) _bandSeconds[_band] += now - _bandSince;
            _bandSince = now;
        }

        // ------------------------------------------------------------------ words

        void Line(float t, string text)
        {
            if (!NoRoom()) _lines.Add(new LogLine(t, text));
        }

        /// <summary>
        /// True when the log has no room for another line, counting the line it drops. The events that come many times a
        /// second (hits, a warning's stages, hurts) ask before their words are built, so a full log builds nothing.
        /// </summary>
        bool NoRoom()
        {
            if (_lines.Count < MostLines) return false;
            LinesDropped++;
            return true;
        }

        static Tally Named(Dictionary<string, Tally> tallies, string name)
        {
            if (!tallies.TryGetValue(name, out Tally t))
            {
                if (tallies.Count >= MostNames) name = "other";
                if (!tallies.TryGetValue(name, out t))
                {
                    t = new Tally();
                    tallies[name] = t;
                }
            }
            return t;
        }

        /// <summary>A piece's words without its kind in front: "Strider claw" is the claw.</summary>
        public static string PieceWords(string label, string kind)
        {
            if (string.IsNullOrEmpty(label)) return "body";
            if (!string.IsNullOrEmpty(kind) && label.StartsWith(kind + " ", StringComparison.Ordinal)) label = label.Substring(kind.Length + 1);
            return Lower(label);
        }

        static string Lower(string s) => string.IsNullOrEmpty(s) ? "" : s.ToLowerInvariant();

        /// <summary>What a hit zone means, in the words the report uses.</summary>
        public static string ZoneWords(HitZone zone)
        {
            switch (zone)
            {
                case HitZone.Armour: return "armour";
                case HitZone.Vital: return "weak place";
                case HitZone.WeaponOff: return "weapon off";
                default: return "plain";
            }
        }

        /// <summary>Where a creature stood against the player's view at its mark: ahead, to the side or behind.</summary>
        public static string FacingWords(float degrees)
        {
            if (degrees < 0f) return "unknown";
            if (degrees <= AheadMost) return "ahead";
            if (degrees >= BehindLeast) return "behind";
            return "side";
        }

        /// <summary>Minutes, seconds and tenths: 65.25 s is "1:05.2". Tenths are cut, never rounded up into the next second.</summary>
        public static string Clock(float t)
        {
            if (!(t > 0f)) t = 0f;
            // The small nudge keeps a float like 0.7 (0.69999...) from showing as 0.6.
            long tenths = (long)Math.Floor(t * 10.0 + 1e-4);
            long minutes = tenths / 600;
            long rest = tenths % 600;
            return minutes.ToString(CultureInfo.InvariantCulture) + ":" + (rest / 10).ToString("00", CultureInfo.InvariantCulture) + "." + (rest % 10).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Minutes and seconds: 65.25 s is "1:05".</summary>
        public static string Minutes(double t)
        {
            if (!(t > 0.0)) t = 0.0;
            long s = (long)Math.Floor(t);
            return (s / 60).ToString(CultureInfo.InvariantCulture) + ":" + (s % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        public static string Seconds(float s) => (s > 0f ? s : 0f).ToString("0.0", CultureInfo.InvariantCulture) + " s";
        public static string Metres(float m) => m < 0f ? "? m" : m.ToString("0", CultureInfo.InvariantCulture) + " m";
        public static string Degrees(float d) => d < 0f ? "? degrees" : d.ToString("0", CultureInfo.InvariantCulture) + " degrees";
        public static string Amount(float a) => a.ToString("0.#", CultureInfo.InvariantCulture);
        public static string N(long n) => n.ToString("N0", CultureInfo.InvariantCulture);
    }
}
