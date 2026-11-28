using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OrbitalAnte.AI;
using OrbitalAnte.Ante;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;
using OrbitalAnte.Dismemberment;
using OrbitalAnte.Player;
using OrbitalAnte.Space;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OrbitalAnte.Playtest
{
    /// <summary>
    /// The game's side of the playtest log (Ralph loop pass 9; <see cref="PlaytestLog"/>): the few places in the game that
    /// know something the playtests count call these, and they hand it on as plain facts. Only the editor and development
    /// builds have a <see cref="PlaytestRecorder"/>; without one <see cref="Log"/> stays null and every hook returns at
    /// once, so a release build pays one null check where a hook sits and keeps nothing.
    ///
    /// The log is written when its run ends (a death, the run-over page, a restart, back to the title screen, the game
    /// closing), on pausing (at most every 30 s) and whenever F7 is pressed, to <see cref="Folder"/>: one file a run, and
    /// the newest as latest.txt.
    /// </summary>
    public static class PlaytestHooks
    {
        /// <summary>The log of the run being played; null in a release build, outside a run, and once a run has ended.</summary>
        public static PlaytestLog Log { get; private set; }
        /// <summary>The recorder on the game's root, which polls what no hook says (the antes, the music, the creatures' notice).</summary>
        public static PlaytestRecorder Recorder { get; internal set; }
        /// <summary>Where the last log was written, for the debug readout (F3).</summary>
        public static string LastWritten { get; private set; }

        /// <summary>The editor and development builds keep a log; a release build never does.</summary>
        public static bool Allowed => Application.isEditor || Debug.isDebugBuild;

        static float _start, _gameStart;
        static string _fileName;
        static Transform _arenaRoot;
        static Transform _arenaEye;
        static Camera _arenaCamera;
        static readonly Dictionary<int, string> WorldNames = new Dictionary<int, string>();
        static readonly Unity.Profiling.ProfilerMarker WriteMarker = new Unity.Profiling.ProfilerMarker("OA.Playtest.Write");

        /// <summary>Real seconds since the run began, pauses included (to line up with a screen recording).</summary>
        public static float Now => Time.unscaledTime - _start;

        /// <summary>Game seconds since the run began: they stop for pauses and slow with Focus, like the creatures' clocks.</summary>
        public static float GameNow => Time.time - _gameStart;

        /// <summary>Now, on both of the log's clocks.</summary>
        public static Moment At => new Moment(Now, GameNow);

        /// <summary>In the editor, the project's Logs/Playtest (beside the check-in script's Logs/oa-check); in a development build, the player's data folder.</summary>
        public static string Folder => Application.isEditor
            ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Playtest"))
            : Path.Combine(Application.persistentDataPath, "Playtest");

        public static void ResetStatics()
        {
            Log = null;
            Recorder = null;
            LastWritten = null;
            _fileName = null;
            _arenaRoot = null;
            _arenaEye = null;
            _arenaCamera = null;
            WorldNames.Clear();
        }

        // ------------------------------------------------------------------ the run

        /// <summary>A run has begun (the end of <c>GameBootstrap.StartRun</c>): a fresh log.</summary>
        public static void RunBegan(Scenario scenario, int seed)
        {
            if (Recorder == null) return;
            End(RunEnd.Left, null);
            DateTime now = DateTime.Now;
            Tasks.AnteLoop loop = Tasks.AnteLoop.Current;
            AnteRun run = loop != null ? loop.Run : null;
            StartingKit kit = run != null && run.KitId != null ? StartingKits.Find(run.KitId) : null;
            string upgrade = loop != null && loop.LockerUpgrade != null ? loop.LockerUpgrade.displayName : null;
            string where = scenario == Scenario.SolarSystem ? "solar system" : "practice range";
            var log = new PlaytestLog();
            log.Begin(where, seed, now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Application.version + ", Unity " + Application.unityVersion + (Application.isEditor ? ", editor" : ", development build"),
                kit != null ? kit.displayName : null, upgrade, FeelSettings.GuideTips);
            Save.SaveData save = Save.SaveSystem.Data;
            if (save != null) log.Profile(save.runsStarted, save.unlocks != null ? save.unlocks.Count : 0, LooksInUse(save));
            _start = Time.unscaledTime;
            _gameStart = Time.time;
            _fileName = PlaytestFiles.NameFor(now, where, seed);
            WorldNames.Clear();
            Log = log;
            Recorder.RunBegan();
            // The practice range laid its creatures out while it was built, before there was a log to meet them.
            if (scenario == Scenario.CombatArena) MeetArena(log);
        }

        /// <summary>The run is over: the log is finished, written, and let go. A second call does nothing.</summary>
        public static void End(RunEnd end, string words)
        {
            PlaytestLog log = Log;
            if (log == null) return;
            Moment t = At;
            // A restart or a quit during the death burst, before the death page came up, is still the death (Unity review).
            OrbitalAnte.Ship.Astronaut astronaut = OrbitalAnte.Ship.Astronaut.Current;
            if (end != RunEnd.Died && astronaut != null && astronaut.Dead && log.Deaths.Count == 0)
            {
                string cause = string.IsNullOrEmpty(astronaut.DeathCause) ? null : astronaut.DeathCause;
                log.Died(t, cause);
                words = (cause ?? "died") + "; " + (string.IsNullOrEmpty(words) ? "left" : words) + " before the death page came up";
                end = RunEnd.Died;
            }
            if (Recorder != null) Recorder.BeforeEnd(log, t);
            Tasks.AnteLoop loop = Tasks.AnteLoop.Current;
            AnteRun run = loop != null ? loop.Run : null;
            log.Finish(t, end, words, run != null ? run.Director.Ante : 0, run != null ? run.Ledger.LifetimeRunScore : 0);
            Write(log);
            Log = null;
        }

        /// <summary>The player died in a run, in the death page's words: the run's end.</summary>
        public static void Died(string cause)
        {
            if (Log == null) return;
            Log.Died(At, cause);
            End(RunEnd.Died, cause);
        }

        /// <summary>The run-over page (all antes cleared, or the clock under the old rule): the run's end.</summary>
        public static void RunOver(string title, string cause)
        {
            if (Log == null) return;
            End(RunEnd.RunOver, string.IsNullOrEmpty(title) ? cause : title + ": " + cause);
        }

        /// <summary>For the debug readout (F3): the log's size, the key that writes it and where it goes. Empty without a recorder.</summary>
        public static string Describe()
        {
            if (Recorder == null) return string.Empty;
            string kept = Log != null ? Log.Lines.Count.ToString(CultureInfo.InvariantCulture) + " events kept this run, F7 writes it now" : "no run under way";
            return "\nPlaytest log: " + kept + "\n  " + (LastWritten ?? Path.Combine(Folder, PlaytestFiles.Latest));
        }

        /// <summary>F7, or a pause: writes the log so far without ending it.</summary>
        public static void WriteNow()
        {
            PlaytestLog log = Log;
            if (log == null) return;
            log.Snapshot(At);
            Write(log);
        }

        static void Write(PlaytestLog log)
        {
            if (string.IsNullOrEmpty(_fileName)) return;
            using (WriteMarker.Auto())
            {
                try
                {
                    string path = PlaytestFiles.Write(Folder, _fileName, PlaytestReport.Write(log));
                    LastWritten = path;
                    Debug.Log("Orbital Ante playtest log written: " + path);
                }
                catch (Exception e)
                {
                    // A log that cannot be written must never stop the game.
                    Debug.LogWarning("Orbital Ante: the playtest log could not be written (" + e.Message + ").");
                }
            }
            // A frame that wrote the log is never a creature's wake frame (deck-performance review).
            if (Recorder != null) Recorder.Wrote();
        }

        /// <summary>The looks in use, by name, for the log's header.</summary>
        static string LooksInUse(Save.SaveData save)
        {
            Items.Loadout l = Items.Locker.Of(save);
            var names = new List<string>();
            Add(names, l.paint);
            Add(names, l.helmet);
            Add(names, l.visor);
            Add(names, l.suit);
            Add(names, l.pack);
            Add(names, l.badge);
            for (int i = 0; i < l.skins.Count; i++)
            {
                Items.ItemSpec skin = Items.LockerLook.Find(l.skins[i].itemId);
                string name = skin != null ? skin.displayName : l.skins[i].itemId;
                names.Add(string.IsNullOrEmpty(l.skins[i].gunId) ? name + " (every gun)" : name + " (" + l.skins[i].gunId + ")");
            }
            return string.Join(", ", names);
        }

        static void Add(List<string> names, Items.LockerItem place)
        {
            if (place == null || string.IsNullOrEmpty(place.itemId)) return;
            Items.ItemSpec item = Items.LockerLook.Find(place.itemId);
            names.Add(item != null ? item.displayName : place.itemId);
        }

        // ------------------------------------------------------------------ what the game tells it

        /// <summary>A creature's warning changed stage (<c>WindupTell.Tell</c>, every creature).</summary>
        public static void Warning(IWindup who, in WindupChange change)
        {
            PlaytestLog log = Log;
            if (log == null || !change.Changed) return;
            var c = who as Component;
            if (c == null) return;
            Vector3 point = who.WindupPoint;
            Facing(point, out float facing, out float range);
            bool inFrame = InFrame(ViewCamera(), point, out _);
            var creature = who as ICreature;
            log.Warning(At, c, creature != null ? creature.Kind : c.GetType().Name, WorldOf(creature), AnteNow, GroupOf(c), change.Was, change.Now, facing, range, inFrame);
            if (Recorder != null) Recorder.Warned(c);
        }

        /// <summary>A world's spawner woke an animal, or a pack (<c>LandCreatures</c>, <c>SeaCreatures</c>).</summary>
        public static void Woke(Component who, string kind, string world, int body)
        {
            PlaytestLog log = Log;
            if (log == null || who == null) return;
            if (body > 0 && !string.IsNullOrEmpty(world)) WorldNames[body] = world;
            Transform eye = Eye();
            float from = eye != null ? Vector3.Distance(eye.position, PositionOf(who)) : -1f;
            bool pack = who is BiterShoal;
            log.Woke(At, who, kind, world, AnteNow, from, pack);
            if (who is BiterShoal shoal) shoal.Gone += ShoalGone;
            if (Recorder != null) Recorder.Woke(who, kind);
        }

        /// <summary>The practice range laid its creatures out (at its start, and each time it lays them out again).</summary>
        public static void LaidOut(Transform root)
        {
            if (Recorder == null) return;
            _arenaRoot = root;
            if (Log != null) MeetArena(Log);
        }

        static void MeetArena(PlaytestLog log)
        {
            Transform root = _arenaRoot;
            if (root == null || Recorder == null || GameSession.Scenario != Scenario.CombatArena) return;
            Moment t = At;
            // Those laid out before are gone with their folder.
            Recorder.ClearedAway(log, t);
            Transform eye = Eye();
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                Component who;
                string kind;
                BiterShoal shoal = child.GetComponent<BiterShoal>();
                if (shoal != null)
                {
                    who = shoal;
                    kind = PlaytestLog.PackOf(BiterLayout.Model);
                    shoal.Gone += ShoalGone;
                }
                else
                {
                    var creature = child.GetComponent<ICreature>();
                    // A pack's fish are met through it.
                    if (creature == null || creature is Biter) continue;
                    who = creature as Component;
                    kind = creature.Kind;
                }
                if (who == null) continue;
                float from = eye != null ? Vector3.Distance(eye.position, PositionOf(who)) : -1f;
                log.Woke(t, who, kind, "practice range", 0, from, shoal != null, true);
                Recorder.Watch(who);
            }
        }

        static void ShoalGone(BiterShoal shoal)
        {
            PlaytestLog log = Log;
            if (log != null) log.GroupGone(At, shoal);
        }

        /// <summary>A world's spawner is putting an animal away (the player left it behind).</summary>
        public static void PutAway(Component who)
        {
            PlaytestLog log = Log;
            if (log == null || who == null) return;
            if (who is Walker walker) log.Slips(who, walker.FootSlips);
            log.PutAway(At, who);
        }

        /// <summary>The player fired (<c>PlayerWeapons</c>).</summary>
        public static void Shot(string gun)
        {
            PlaytestLog log = Log;
            if (log != null) log.Shot(At, gun);
        }

        /// <summary>A submission to the ante's score (<c>AnteLoop.Report</c>).</summary>
        public static void Points(int ante, in ScoreEvent e, string label, in ScoreResult result)
        {
            PlaytestLog log = Log;
            if (log == null) return;
            int feats = Math.Max(e.skillFeats, e.verifiedSkill ? 1 : 0);
            log.Points(At, ante, e.kind.ToString(), label, result.points, result.Awarded, result.outcome.ToString(), feats, e.claimId);
        }

        /// <summary>A note offered to the ante readout (<c>AnteLoop.Note</c>): it may wait behind an important one, and be replaced.</summary>
        public static void Note(string text)
        {
            PlaytestLog log = Log;
            if (log != null) log.Note(At, text);
        }

        /// <summary>A note the ante readout really showed (<c>AnteLoop.ShowNote</c>).</summary>
        public static void NoteShown(string text)
        {
            PlaytestLog log = Log;
            if (log != null) log.NoteShown(At, text);
        }

        public static void UnlockMet(string name, bool inAFight, bool atTheBreak)
        {
            PlaytestLog log = Log;
            if (log != null) log.UnlockMet(At, name, inAFight, atTheBreak);
        }

        public static void UnlockShown(string name, float waited)
        {
            PlaytestLog log = Log;
            if (log != null) log.UnlockShown(At, name, waited);
        }

        /// <summary>
        /// The line over the crosshair named a creature's armour and where to aim (<c>HitMarker</c>), after a hit on
        /// <paramref name="target"/>: the recorder looks along the line's words while it shows.
        /// </summary>
        public static void AimLine(string kind, Component target, UI.HitMarker marker)
        {
            PlaytestLog log = Log;
            if (log == null) return;
            var creature = target as ICreature;
            log.AimLine(At, kind, target, WorldOf(creature), AnteNow, GroupOf(target));
            if (Recorder != null) Recorder.AimLineShown(marker, target);
        }

        // ------------------------------------------------------------------ helpers

        internal static int AnteNow
        {
            get
            {
                Tasks.AnteLoop loop = Tasks.AnteLoop.Current;
                return loop != null && loop.Run != null ? loop.Run.Director.Ante : 0;
            }
        }

        /// <summary>The player's eye: the astronaut's in the solar system, the test player's in the practice range.</summary>
        internal static Transform Eye()
        {
            OrbitalAnte.Ship.Astronaut a = OrbitalAnte.Ship.Astronaut.Current;
            if (a != null && a.Eye != null) return a.Eye.transform;
            return CombatWorld.PlayerEye;
        }

        /// <summary>The camera the player sees through.</summary>
        internal static Camera ViewCamera()
        {
            OrbitalAnte.Ship.Astronaut a = OrbitalAnte.Ship.Astronaut.Current;
            if (a != null && a.Eye != null) return a.Eye;
            Transform eye = CombatWorld.PlayerEye;
            if (eye == null) return null;
            if (eye != _arenaEye)
            {
                _arenaEye = eye;
                _arenaCamera = eye.GetComponent<Camera>();
            }
            return _arenaCamera;
        }

        /// <summary>Whether a point is inside the edges of the view (in front of it); <paramref name="distance"/> from the camera.</summary>
        internal static bool InFrame(Camera view, Vector3 at, out float distance)
        {
            distance = -1f;
            if (view == null) return false;
            Vector3 v = view.WorldToViewportPoint(at);
            distance = Vector3.Distance(view.transform.position, at);
            return v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
        }

        internal static void Facing(Vector3 at, out float degrees, out float range)
        {
            Transform eye = Eye();
            if (eye == null)
            {
                degrees = -1f;
                range = -1f;
                return;
            }
            Vector3 to = at - eye.position;
            range = to.magnitude;
            degrees = range > 1e-3f ? Vector3.Angle(eye.forward, to) : 0f;
        }

        /// <summary>Where a creature is: the point its warning comes from, a pack's middle, or its root.</summary>
        internal static Vector3 PositionOf(Component who)
        {
            if (who is IWindup windup) return windup.WindupPoint;
            if (who is BiterShoal shoal) return shoal.Middle;
            return who.transform.position;
        }

        internal static float RangeTo(Component who)
        {
            Transform eye = Eye();
            return eye != null && who != null ? Vector3.Distance(eye.position, PositionOf(who)) : -1f;
        }

        /// <summary>The world a creature lives on, as its spawner named it, or the practice range.</summary>
        internal static string WorldOf(ICreature creature)
        {
            if (GameSession.Scenario == Scenario.CombatArena) return "practice range";
            CreatureCredit credit = creature != null ? creature.Credit : null;
            if (credit == null) return "a world";
            return WorldNames.TryGetValue(credit.Body, out string name) ? name : "world " + credit.Body.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A Pack biter's shoal, which its record adds to; null for anything else. Asked of the fish, never of its parent:
        /// a fish is made beside its shoal, not under it (Unity review, pass 9).
        /// </summary>
        internal static Component GroupOf(Component who) => who is Biter fish && fish.Shoal != null ? fish.Shoal : null;

        /// <summary>What a creature stands on, for the ones that live on the worlds' ground; null for the rest.</summary>
        internal static Footing FootingOf(Component who)
        {
            switch (who)
            {
                case Walker walker: return walker.Footing;
                case Roller roller: return roller.Footing;
                case Screamer screamer: return screamer.Footing;
                default: return null;
            }
        }
    }

    /// <summary>
    /// Polls for the playtest log what no hook says (Ralph loop pass 9): the antes as they go, the music, which creatures
    /// are after the player and whether an armed enemy sees them meanwhile, when a creature first comes into view, the
    /// frames after a wake, the player's motion across a warning creature's line, and where an aim line lay; and it hears
    /// the player's hits, hurts, sightings, alarms and pauses from the game's own events. Added to the game's root only in
    /// the editor and development builds. F7 writes the log so far.
    /// </summary>
    public sealed class PlaytestRecorder : MonoBehaviour
    {
        /// <summary>Seconds between looks at the creatures, the music, enemy sight and an aim line.</summary>
        const float PollEvery = 0.25f;
        /// <summary>Seconds after an animal wakes over which the longest frame is kept.</summary>
        const float WakeWatch = 0.5f;
        /// <summary>Further off than this, a creature in the view's frame is not counted as in view.</summary>
        const float MostInView = 400f;
        /// <summary>Metres a sight line may pass under the ground near its ends (a creature's own footing, the eye's ground).</summary>
        const double SightGive = 0.5;
        /// <summary>Rays cast along an aim line's words at each look.</summary>
        const int AimRays = 5;
        /// <summary>
        /// Real seconds between writes on pausing: the Deck has no F7, so pausing (Start) keeps the log so far, in case
        /// the run never reaches its end (a crash, the Deck put to sleep).
        /// </summary>
        const float PauseWriteEvery = 30f;
        float _lastPauseWrite = -1000f;

        readonly List<Component> _watched = new List<Component>();
        PlayerHealth _player;
        float _armourBefore;
        float _nextPoll;
        float _lastSeenAt = -1000f;

        // The frames after a wake: its own (read the frame after), the longest of the half second after, and the middle
        // frame of the second before.
        readonly FrameTimes _frames = new FrameTimes();
        readonly HashSet<string> _wokenKinds = new HashSet<string>();
        object _waking;
        int _wokeFrame, _lastWriteFrame = -1;
        float _wakeUntil, _wakeWorst, _wakeOwn, _wakeBaseline;
        bool _wakeFirst, _wakeSpoilt;

        int _ante, _clearedAnte, _perks;
        bool _targetMet, _overtime, _clockStarted;
        AntePhase _phase;

        // The player's motion across each warning creature's line (DodgeRules), every frame while it warns.
        struct DodgeTrack
        {
            public Component Who;
            public Vector3 Rel;
            public int Frame;
        }
        readonly List<DodgeTrack> _dodging = new List<DodgeTrack>();

        // The aim line on screen, and the creature it named.
        UI.HitMarker _aimMarker;
        Component _aimTarget;

        // The ground between the eye and a creature on a world (SightLine), without a delegate made per look.
        sealed class GroundRadius
        {
            public LandFooting Land;
            public double Of(Vec3d dir) => Land.GroundPoint(dir).Magnitude;
        }
        readonly GroundRadius _ground = new GroundRadius();
        Func<Vec3d, double> _groundRadius;

        // The music as last heard.
        Audio.MusicTrackDef _musicTrack;
        ulong _stems;
        bool _musicHeard, _wasCombat, _entryPending;

        void Awake()
        {
            PlaytestHooks.Recorder = this;
            _groundRadius = _ground.Of;
        }

        void OnDestroy()
        {
            SetPlayer(null);
            if (PlaytestHooks.Recorder == this) PlaytestHooks.Recorder = null;
        }

        void OnEnable()
        {
            CombatEvents.PlayerHitSomething += OnHit;
            CombatWorld.PlayerSpotted += OnSpotted;
            CombatWorld.SquadSent += OnSquadSent;
            CombatWorld.Alarm += OnAlarm;
            GameSession.PauseChanged += OnPauseChanged;
        }

        void OnDisable()
        {
            CombatEvents.PlayerHitSomething -= OnHit;
            CombatWorld.PlayerSpotted -= OnSpotted;
            CombatWorld.SquadSent -= OnSquadSent;
            CombatWorld.Alarm -= OnAlarm;
            GameSession.PauseChanged -= OnPauseChanged;
        }

        void OnPauseChanged(bool paused)
        {
            PlaytestLog log = PlaytestHooks.Log;
            if (log == null) return;
            log.Paused(PlaytestHooks.At, paused);
            if (paused && _waking != null) _wakeSpoilt = true;
            if (!paused || Time.unscaledTime - _lastPauseWrite < PauseWriteEvery) return;
            _lastPauseWrite = Time.unscaledTime;
            PlaytestHooks.WriteNow();
        }

        /// <summary>A fresh run: nothing watched yet.</summary>
        public void RunBegan()
        {
            _watched.Clear();
            _dodging.Clear();
            _ante = 0;
            _clearedAnte = 0;
            _targetMet = false;
            _overtime = false;
            _clockStarted = false;
            _phase = AntePhase.Running;
            Tasks.AnteLoop loop = Tasks.AnteLoop.Current;
            // A starting kit's perks are in the header: only those taken from here on are listed.
            _perks = loop != null && loop.Run != null ? loop.Run.Perks.Owned.Count : 0;
            _lastSeenAt = CombatWorld.PlayerLastSeenAt;
            _waking = null;
            _nextPoll = 0f;
            _aimMarker = null;
            _aimTarget = null;
            _musicTrack = null;
            _stems = 0;
            _musicHeard = false;
            _wasCombat = false;
            _entryPending = false;
            _frames.Clear();
            SetPlayer(null);
        }

        /// <summary>Keeps an eye on a creature from now on (its notice, when it first comes into view).</summary>
        public void Watch(Component who)
        {
            if (who != null && !_watched.Contains(who)) _watched.Add(who);
        }

        /// <summary>A creature began a warning or moved it on: watched, and the player's motion across its line followed every frame.</summary>
        public void Warned(Component who)
        {
            Watch(who);
            for (int i = 0; i < _dodging.Count; i++) if (_dodging[i].Who == who) return;
            if (!Relative(who, out Vector3 rel, out _)) return;
            _dodging.Add(new DodgeTrack { Who = who, Rel = rel, Frame = Time.frameCount });
        }

        /// <summary>An animal woke: watched, and its frame and the half second after are timed against the second before.</summary>
        public void Woke(Component who, string kind)
        {
            Watch(who);
            PlaytestLog log = PlaytestHooks.Log;
            if (_waking != null && log != null) FinishWake(log);
            _waking = who;
            _wokeFrame = Time.frameCount;
            _wakeUntil = Time.unscaledTime + WakeWatch;
            _wakeWorst = 0f;
            _wakeOwn = -1f;
            _wakeBaseline = _frames.Median();
            // Its model and pictures may have been read in for the first time in its frame.
            _wakeFirst = _wokenKinds.Add(kind ?? "creature");
            _wakeSpoilt = false;
        }

        /// <summary>The practice range laid its creatures out again: those before were cleared with their folder.</summary>
        public void ClearedAway(PlaytestLog log, Moment t)
        {
            for (int i = _watched.Count - 1; i >= 0; i--)
            {
                Component who = _watched[i];
                if (who != null && who.gameObject.activeInHierarchy) continue;
                log.PutAway(t, who, "the practice range was laid out again");
                _watched.RemoveAt(i);
            }
        }

        /// <summary>The log was just written (this frame is long for it).</summary>
        public void Wrote() => _lastWriteFrame = Time.frameCount;

        /// <summary>An aim line came up: looked along while it shows.</summary>
        public void AimLineShown(UI.HitMarker marker, Component target)
        {
            _aimMarker = marker;
            _aimTarget = target;
        }

        /// <summary>Just before the log is finished: the slips of the walkers still about, and a wake still being timed.</summary>
        public void BeforeEnd(PlaytestLog log, Moment t)
        {
            for (int i = 0; i < _watched.Count; i++)
                if (_watched[i] is Walker walker && walker != null) log.Slips(walker, walker.FootSlips);
            if (_waking != null) FinishWake(log);
        }

        void Update()
        {
            PlaytestLog log = PlaytestHooks.Log;
            if (log == null) return;
            Moment t = PlaytestHooks.At;
            WatchPlayer();
            Antes(log, t);
            Dodges(log, t);
            WakeFrames(log);
            if (Time.unscaledTime >= _nextPoll)
            {
                _nextPoll = Time.unscaledTime + PollEvery;
                PollCreatures(log, t);
                PollAimLine(log);
                PollMusic(log, t);
                PollEnemySight(log, t);
            }
            Keyboard keys = Keyboard.current;
            if (keys != null && keys.f7Key.wasPressedThisFrame) PlaytestHooks.WriteNow();
        }

        // ------------------------------------------------------------------ a wake's frames

        void WakeFrames(PlaytestLog log)
        {
            float ms = Time.unscaledDeltaTime * 1000f;
            if (_waking != null && Time.frameCount > _wokeFrame)
            {
                // A frame's length shows in the next frame's time: the frame after the wake reads the wake's own.
                if (Time.frameCount == _wokeFrame + 1) _wakeOwn = ms;
                else _wakeWorst = Mathf.Max(_wakeWorst, ms);
                if (Time.unscaledTime >= _wakeUntil) FinishWake(log);
            }
            _frames.Add(ms);
        }

        void FinishWake(PlaytestLog log)
        {
            // A pause, or a write of this log, inside the watch makes its frames say nothing about the wake.
            if (_wakeSpoilt || _lastWriteFrame >= _wokeFrame || _wakeOwn < 0f) log.WakeUntimed(_waking);
            else log.WakeFrame(_waking, _wakeOwn, _wakeWorst, _wakeBaseline, _wakeFirst);
            _waking = null;
        }

        // ------------------------------------------------------------------ the player

        void WatchPlayer()
        {
            PlayerHealth p = CombatWorld.Player;
            if (p == null)
            {
                OrbitalAnte.Ship.Astronaut a = OrbitalAnte.Ship.Astronaut.Current;
                if (a != null) p = a.Armour;
            }
            if (p != _player) SetPlayer(p);
            if (_player != null) _armourBefore = _player.Current;
        }

        void SetPlayer(PlayerHealth p)
        {
            if (!ReferenceEquals(_player, null))
            {
                _player.Damaged -= OnHurt;
                _player.Died -= OnKnockedDown;
            }
            _player = p;
            if (p == null) return;
            _armourBefore = p.Current;
            p.Damaged += OnHurt;
            // In a run the death page's own words come from the game's death (PlaytestHooks.Died); in the practice range
            // there is no death page, so the knock-down is heard here.
            if (GameSession.Scenario == Scenario.CombatArena) p.Died += OnKnockedDown;
        }

        void OnHurt(float amount, Vector3 direction)
        {
            PlaytestLog log = PlaytestHooks.Log;
            if (log == null || _player == null) return;
            // Armour really lost: a hit past the last plate takes only what was left (design review, pass 9).
            float lost = _player.Current > 0f ? amount : Mathf.Min(amount, _armourBefore);
            _armourBefore = _player.Current;
            log.Hurt(PlaytestHooks.At, lost, _player.LastCause);
        }

        void OnKnockedDown()
        {
            PlaytestLog log = PlaytestHooks.Log;
            if (log == null || _player == null) return;
            log.Died(PlaytestHooks.At, !string.IsNullOrEmpty(_player.LastCause) ? _player.LastCause : "Knocked down by a shot (practice range).");
        }

        void OnHit(HitOutcome o)
        {
            PlaytestLog log = PlaytestHooks.Log;
            if (log == null || !o.Hit) return;
            Moment t = PlaytestHooks.At;
            string gun = o.Source == HitSource.Melee ? "melee" : o.Source == HitSource.Rover ? "rover" : string.IsNullOrEmpty(o.SourceId) ? "gun" : o.SourceId;
            Component target = o.Target;
            float range = target != null ? PlaytestHooks.RangeTo(target) : -1f;
            if (target != null && target is ICreature creature)
            {
                // What it pays on and whether it could answer the player, read in the frame the task director reads them.
                CreatureCredit credit = creature.Credit;
                log.CreatureHit(t, target, creature.Kind, PlaytestHooks.WorldOf(creature), PlaytestHooks.AnteNow, PlaytestHooks.GroupOf(target),
                    o.Label, o.Zone, o.Severed, o.Killed, gun, range, credit != null ? credit.Claim : null, creature.InReach);
                Watch(target);
                if (o.Killed && target is Walker walker) log.Slips(target, walker.FootSlips);
                return;
            }
            if (o.Enemy || o.Disarmed) log.PersonHit(t, gun, o.Label, o.Head, o.Severed, o.Disarmed, o.Killed, o.HeadshotKill, range);
        }

        void OnSpotted(Vector3 from, float beat, bool hidden)
        {
            PlaytestLog log = PlaytestHooks.Log;
            if (log != null) log.Spotted(PlaytestHooks.At, hidden);
        }

        void OnSquadSent(Vector3 from)
        {
            PlaytestLog log = PlaytestHooks.Log;
            if (log != null) log.SquadSent(PlaytestHooks.At);
        }

        void OnAlarm(Vector3 at, float radius, Vector3 playerAt, string faction)
        {
            PlaytestLog log = PlaytestHooks.Log;
            if (log != null) log.Alarm(PlaytestHooks.At, radius, Vector3.Distance(at, playerAt));
        }

        // ------------------------------------------------------------------ the dodge, every frame

        void Dodges(PlaytestLog log, Moment t)
        {
            if (_dodging.Count == 0) return;
            float dt = Time.deltaTime;
            int frame = Time.frameCount;
            for (int i = _dodging.Count - 1; i >= 0; i--)
            {
                DodgeTrack d = _dodging[i];
                if (d.Who == null)
                {
                    _dodging.RemoveAt(i);
                    continue;
                }
                // Begun this frame: its first reading comes next frame, over a whole frame.
                if (frame <= d.Frame || !Relative(d.Who, out Vector3 rel, out Vector3 up)) continue;
                if (dt > 0f)
                {
                    double speed = DodgeRules.AcrossSpeed(SpaceConvert.ToVec3d(d.Rel), SpaceConvert.ToVec3d(rel), SpaceConvert.ToVec3d(up), dt);
                    if (!log.Across(t, d.Who, (float)speed, dt))
                    {
                        _dodging.RemoveAt(i);
                        continue;
                    }
                }
                d.Rel = rel;
                d.Frame = frame;
                _dodging[i] = d;
            }
        }

        /// <summary>
        /// Where the player's eye is against a creature, in the frame of what it lives in (its parent's turn taken out, so
        /// the turning of a world and a re-centre cancel), and which way is up there: none for a swimmer in a sea, where
        /// moving across counts in every direction.
        /// </summary>
        static bool Relative(Component who, out Vector3 rel, out Vector3 up)
        {
            rel = up = default;
            Transform eye = PlaytestHooks.Eye();
            if (eye == null || who == null) return false;
            Vector3 at = who.transform.position;
            Transform space = who.transform.parent;
            Quaternion undo = space != null ? Quaternion.Inverse(space.rotation) : Quaternion.identity;
            rel = undo * (eye.position - at);
            if (GameSession.Scenario == Scenario.CombatArena) up = undo * Vector3.up;
            else if (who is Maw || who is Biter) up = Vector3.zero;
            else
            {
                Footing footing = PlaytestHooks.FootingOf(who);
                up = undo * (footing != null ? footing.Up(at) : who.transform.up);
            }
            return true;
        }

        // ------------------------------------------------------------------ what is polled

        void Antes(PlaytestLog log, Moment t)
        {
            Tasks.AnteLoop loop = Tasks.AnteLoop.Current;
            AnteRun run = loop != null ? loop.Run : null;
            if (run == null) return;
            AnteDirector d = run.Director;
            if (d.Ante != _ante)
            {
                _ante = d.Ante;
                _targetMet = false;
                _overtime = false;
                _clockStarted = false;
                _phase = d.Phase;
                log.AnteBegan(t, d.Ante, d.Target);
            }
            // Ante 1's clock waits on the pad until liftoff (RunStartHold); every other ante's runs from its start.
            if (!_clockStarted && d.IsRunning && !(Tasks.AnteLoop.HoldClock != null && Tasks.AnteLoop.HoldClock()))
            {
                _clockStarted = true;
                log.ClockStarted(t, d.Ante);
            }
            if (!_targetMet && d.TargetMet)
            {
                _targetMet = true;
                log.TargetMet(t, d.Ante, run.Ledger.AnteEarned, d.InOvertime ? 0f : Mathf.Max(0f, d.SecondsLeft));
            }
            if (d.InOvertime != _overtime)
            {
                _overtime = d.InOvertime;
                log.Overtime(t, d.Ante, _overtime);
            }
            IReadOnlyList<PerkDefinition> owned = run.Perks.Owned;
            if (owned.Count > _perks)
                for (int i = _perks; i < owned.Count; i++) log.PerkTaken(t, d.Ante, owned[i] != null ? owned[i].displayName : null);
            _perks = owned.Count;
            if (d.Phase == _phase) return;
            _phase = d.Phase;
            // Victory then Keep going passes through the break for the same ante: it is cleared once.
            if ((_phase == AntePhase.Intermission || _phase == AntePhase.Victory) && d.Ante != _clearedAnte)
            {
                _clearedAnte = d.Ante;
                log.AnteCleared(t, d.Ante, run.Ledger.AnteEarned, d.InOvertime ? 0f : Mathf.Max(0f, d.SecondsLeft));
            }
        }

        void PollCreatures(PlaytestLog log, Moment t)
        {
            Camera view = PlaytestHooks.ViewCamera();
            for (int i = _watched.Count - 1; i >= 0; i--)
            {
                Component who = _watched[i];
                if (who == null)
                {
                    _watched.RemoveAt(i);
                    continue;
                }
                AnimalRecord a = log.Find(who);
                if (a == null || a.End != AnimalEnd.StillThere)
                {
                    _watched.RemoveAt(i);
                    continue;
                }
                if (AwareOfPlayer(who)) log.Noticed(t, who);
                if (a.SeenAt >= 0f || view == null) continue;
                Vector3 at = PlaytestHooks.PositionOf(who);
                if (!PlaytestHooks.InFrame(view, at, out float from) || from > MostInView) continue;
                if (GroundBetween(who, view.transform.position, at)) continue;
                log.Seen(t, who, from);
            }
        }

        /// <summary>True when a hill or the curve of a world hides a creature on it from the eye (rocks, trees and buildings are not checked).</summary>
        bool GroundBetween(Component who, Vector3 eye, Vector3 at)
        {
            if (!(PlaytestHooks.FootingOf(who) is LandFooting land) || land.World == null) return false;
            Transform body = land.World;
            _ground.Land = land;
            bool clear = SightLine.ClearOfGround(SpaceConvert.ToVec3d(body.InverseTransformPoint(eye)), SpaceConvert.ToVec3d(body.InverseTransformPoint(at)),
                _groundRadius, SightGive);
            _ground.Land = null;
            return !clear;
        }

        /// <summary>The creatures that say whether they are after the player. The Cave hunter has no such state, and is not read.</summary>
        static bool AwareOfPlayer(Component who)
        {
            switch (who)
            {
                case Walker walker: return walker.AwareOfPlayer;
                case Roller roller: return roller.AwareOfPlayer;
                case Screamer screamer: return screamer.AwareOfPlayer;
                case Maw maw: return maw.AwareOfPlayer;
                case BiterShoal pack: return pack.AwareOfPlayer;
                case Ambusher ambusher: return ambusher.Alive && ambusher.Brain != null && AmbusherAfter(ambusher.Brain.Mode);
                case Stalker stalker: return stalker.Alive && stalker.Brain != null && StalkerAfter(stalker.Brain.Mode);
                default: return false;
            }
        }

        /// <summary>Rearing, striking, holding, or coming after footsteps under the ground: the Ambusher knows where someone is.</summary>
        static bool AmbusherAfter(AmbusherMode mode) =>
            mode == AmbusherMode.Coil || mode == AmbusherMode.Strike || mode == AmbusherMode.Hold || mode == AmbusherMode.Tunnel || mode == AmbusherMode.Rise;

        /// <summary>Going to a place it heard, rushing, gaping or lunging: the Blind stalker is after someone.</summary>
        static bool StalkerAfter(StalkerMode mode) =>
            mode == StalkerMode.Stalk || mode == StalkerMode.Rush || mode == StalkerMode.Gape || mode == StalkerMode.Lunge;

        /// <summary>
        /// While an aim line shows, a look along its words: a ray through each of a few points across them, and whether
        /// any found the creature it named, and its weak place (pass 7's question: does the line cover what it names?).
        /// </summary>
        void PollAimLine(PlaytestLog log)
        {
            if (_aimMarker == null) return;
            if (!_aimMarker.AimLineSpan(out Vector2 left, out Vector2 right) || _aimTarget == null)
            {
                _aimMarker = null;
                _aimTarget = null;
                return;
            }
            Camera view = PlaytestHooks.ViewCamera();
            if (view == null) return;
            bool overIt = false, overWeak = false;
            for (int k = 0; k < AimRays; k++)
            {
                Vector2 p = Vector2.Lerp(left, right, (k + 0.5f) / AimRays);
                Ray ray = view.ScreenPointToRay(p);
                if (!Physics.Raycast(ray, out RaycastHit hit, MostInView, Layers.ShootMask, QueryTriggerInteraction.Ignore)) continue;
                if (!PartOf(hit.collider, _aimTarget, out bool weak)) continue;
                overIt = true;
                if (weak) overWeak = true;
            }
            log.AimLineLook(overIt, overWeak);
        }

        /// <summary>Whether a collider is a piece of <paramref name="target"/>, and its weak place (from <see cref="WhereToShoot"/>).</summary>
        static bool PartOf(Collider c, Component target, out bool weak)
        {
            weak = false;
            if (c == null) return false;
            if (c.TryGetComponent(out WalkerPart walker))
            {
                weak = WhereToShoot.Walker(walker.Limb == WalkerPart.Belly, walker.Limb == WalkerPart.Carapace, false) == HitZone.Vital;
                return walker.Owner == target;
            }
            if (c.TryGetComponent(out RollerPart roller))
            {
                weak = WhereToShoot.Roller(roller.Piece, false, false) == HitZone.Vital;
                return roller.Owner == target;
            }
            if (c.TryGetComponent(out HunterPart hunter))
            {
                weak = WhereToShoot.Hunter(hunter.Piece, hunter.Index) == HitZone.Vital;
                return hunter.Owner == target;
            }
            if (c.TryGetComponent(out ScreamerPart screamer))
            {
                weak = WhereToShoot.Screamer(screamer.Piece, false) == HitZone.Vital;
                return screamer.Owner == target;
            }
            if (c.TryGetComponent(out AmbusherPart ambusher))
            {
                weak = WhereToShoot.Ambusher(ambusher.Piece, false) == HitZone.Vital;
                return ambusher.Owner == target;
            }
            if (c.TryGetComponent(out StalkerPart stalker))
            {
                weak = WhereToShoot.Stalker(stalker.Piece, stalker.Index) == HitZone.Vital;
                return stalker.Owner == target;
            }
            if (c.TryGetComponent(out MawPart maw))
            {
                weak = WhereToShoot.Maw(maw.Piece, false) == HitZone.Vital;
                return maw.Owner == target;
            }
            if (c.TryGetComponent(out BiterPart biter))
            {
                weak = WhereToShoot.Biter(biter.Piece, false) == HitZone.Vital;
                return biter.Owner == target;
            }
            return false;
        }

        void PollMusic(PlaytestLog log, Moment t)
        {
            Audio.AudioDirector director = Audio.AudioDirector.Instance;
            if (director == null || !director.Ready || director.Music == null) return;
            Audio.MusicBrain brain = director.Music.Brain;
            if (brain == null) return;
            Audio.MusicTrackDef track = brain.Track;
            int layers = 0;
            ulong stems = 0;
            if (track != null && track.Stems != null)
                for (int i = 0; i < track.Stems.Length; i++)
                {
                    if (!brain.StemOn(i)) continue;
                    layers++;
                    if (i < 64) stems |= 1UL << i;
                }
            bool paused = GameSession.Paused;
            log.Music(t, director.Intensity.Band, track != null ? track.Title : null, layers, brain.Thin, paused);
            if (paused) return;
            // The layers by name, built only when they change.
            if (!_musicHeard || track != _musicTrack || stems != _stems)
            {
                _musicHeard = true;
                _musicTrack = track;
                _stems = stems;
                log.MusicLayers(t, StemWords(track, stems));
            }
            log.MusicState(t, brain.InCombat, brain.Hunted);
            if (brain.InCombat && !_wasCombat) _entryPending = true;
            if (!brain.InCombat) _entryPending = false;
            _wasCombat = brain.InCombat;
            // The fight piece comes in on a bar line, which may be a moment off: the entry is read once it plays.
            if (_entryPending && track != null && brain.Position(AudioSettings.dspTime, out int bar, out _))
            {
                _entryPending = false;
                log.MusicEntry(t, track.Title, bar + 1);
            }
        }

        static string StemWords(Audio.MusicTrackDef track, ulong stems)
        {
            if (track == null || track.Stems == null || stems == 0) return null;
            var sb = new StringBuilder();
            for (int i = 0; i < track.Stems.Length && i < 64; i++)
            {
                if ((stems & (1UL << i)) == 0) continue;
                if (sb.Length > 0) sb.Append(" + ");
                sb.Append(string.IsNullOrEmpty(track.Stems[i].Id) ? "layer " + (i + 1).ToString(CultureInfo.InvariantCulture) : track.Stems[i].Id);
            }
            return sb.ToString();
        }

        void PollEnemySight(PlaytestLog log, Moment t)
        {
            float seen = CombatWorld.PlayerLastSeenAt;
            if (seen <= _lastSeenAt) return;
            _lastSeenAt = seen;
            log.EnemySawYou(t);
        }
    }
}
