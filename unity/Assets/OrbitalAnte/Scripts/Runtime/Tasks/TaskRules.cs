using System;
using System.Collections.Generic;
using OrbitalAnte.Ante;

namespace OrbitalAnte.Tasks
{
    /// <summary>
    /// The tasks that earn ante points (Matthew, 20 Nov 2026: "there needs to be a purpose ... independent choices on
    /// how the task gets done, what tasks gets done ... seamless to engage"). Plan: the "Ante tasks plan" doc and
    /// docs/reports/ante-tasks.md. Every task is a thing in the world that starts when you go to it; nothing here
    /// is a menu. All numbers are first guesses, kept on the TaskConfig asset so they can be tuned from play.
    ///
    /// Plain C#, no Unity types: the rules below are what the rule tests check. TaskDirector turns them into
    /// objects, lights and sounds.
    /// </summary>
    [Serializable]
    public sealed class TaskRules
    {
        // ---------------------------------------------------------------- style kills and the streak
        /// <summary>Kills and finished task steps closer together than this build the streak.</summary>
        public float streakWindow = 8f;
        /// <summary>At this streak the next finished task counts as a feat (the ante rules' skill bonus).</summary>
        public int streakForFeat = 5;
        /// <summary>A kill with a gun that made none of the last this-many kills counts as a "fresh gun" feat.</summary>
        public int freshGunMemory = 3;

        // ---------------------------------------------------------------- bases
        /// <summary>Points for clearing a base's last guard, by size (small, medium, large). Scored as an optional mission.</summary>
        public int baseClearSmall = 250, baseClearMedium = 400, baseClearLarge = 550;
        /// <summary>Clearing a base this soon after the first shot there counts as a feat.</summary>
        public float baseQuickSeconds = 60f;

        // ---------------------------------------------------------------- signal masts
        /// <summary>Share of bases that have a signal mast standing near them.</summary>
        public float mastShare = 0.5f;
        /// <summary>How far from the middle of its base a mast stands.</summary>
        public float mastNear = 40f, mastFar = 70f;
        /// <summary>Glowing cells on a mast; all must go to bring it down.</summary>
        public int mastCells = 3;
        /// <summary>Damage a cell takes before it breaks (a rifle round is about 20).</summary>
        public float mastCellHealth = 45f;
        /// <summary>While a mast stands and the player is in its reach, it calls everyone near onto the player this often.</summary>
        public float mastCallEvery = 45f;
        /// <summary>How near the player must be for the mast to call, and how far its call is heard.</summary>
        public float mastReach = 250f;
        /// <summary>All cells broken this soon after the first counts as a feat.</summary>
        public float mastQuickSeconds = 5f;
        /// <summary>A rover hitting a mast at least this fast breaks every cell.</summary>
        public float mastRamSpeed = 7f;

        // ---------------------------------------------------------------- salvage
        /// <summary>Crates of salvage per world, each near a different base.</summary>
        public int salvagePerWorld = 2;
        public float salvageNear = 70f, salvageFar = 140f;
        /// <summary>Walking speed while carrying a crate, as a share of normal. No sprinting.</summary>
        public float carrySpeed = 0.6f;

        // ---------------------------------------------------------------- supply drops
        /// <summary>A drop falls this long after each ante starts, and then again every <see cref="dropEvery"/> seconds.</summary>
        public float dropFirstAfter = 4f, dropEvery = 150f;
        /// <summary>How far from the player a drop lands.</summary>
        public float dropNear = 350f, dropFar = 900f;
        /// <summary>Seconds from the streak appearing in the sky to the pod landing.</summary>
        public float dropFallSeconds = 12f;
        /// <summary>Points for opening a pod (optional mission range 250-550).</summary>
        public int dropPoints = 250;
        /// <summary>How far away enemies hear a pod land and come to look.</summary>
        public float dropHeardFrom = 450f;
        /// <summary>An enemy this close to the pod before it is opened has reached it first.</summary>
        public float dropReachedWithin = 6f;
        /// <summary>A pod nobody opens is gone this long after it lands.</summary>
        public float dropLasts = 240f;
        /// <summary>Hold Interact this long at a pod to take the next gun instead of a Focus refill.</summary>
        public float dropHoldForGun = 0.6f;

        // ---------------------------------------------------------------- marked targets
        /// <summary>One marked target per world that has camps.</summary>
        public bool markedTargets = true;

        // ---------------------------------------------------------------- landings
        /// <summary>A touchdown at this speed or less is soft enough to count as a feat.</summary>
        public float softTouchdown = 1.5f;
        /// <summary>
        /// A landing counts as hard when the ground under the ship is at least this many metres above or below the
        /// average of the ground around it (a rock-tower top, a ridge, a canyon floor).
        /// </summary>
        public float hardLandingRelief = 12f;
        /// <summary>How far out the ground around the ship is sampled.</summary>
        public float hardLandingSample = 25f;
        /// <summary>Spots a hard landing is counted in: one per square of this size on each world.</summary>
        public float hardLandingCell = 300f;

        // ---------------------------------------------------------------- how many to show
        /// <summary>The game keeps at least this many tasks lit within <see cref="nearbyReach"/> of the player.</summary>
        public int liveNearby = 3;
        public float nearbyReach = 1500f;
        /// <summary>Task lights are seen from this far (they fade out closer than a few metres).</summary>
        public float beaconSeenFrom = 4000f;

        public int BaseClearPoints(int size) => size >= 2 ? baseClearLarge : size == 1 ? baseClearMedium : baseClearSmall;

        public List<string> Problems()
        {
            var p = new List<string>();
            if (streakWindow <= 0f) p.Add("Tasks: the streak window must be above zero.");
            if (mastCells < 1) p.Add("Tasks: a mast needs at least one cell.");
            if (mastNear > mastFar || salvageNear > salvageFar || dropNear > dropFar) p.Add("Tasks: a near distance is beyond its far one.");
            if (dropEvery < 10f) p.Add("Tasks: supply drops closer than 10 s apart would flood the world.");
            return p;
        }
    }

    /// <summary>What the game saw about one kill, for the style check. Set by the game, never by the player.</summary>
    public struct KillFacts
    {
        public bool headshot;
        /// <summary>The player was off the ground: jetpack, a jump, floating.</summary>
        public bool airborne;
        /// <summary>Run over with the rover.</summary>
        public bool rover;
        public bool melee;
        /// <summary>The gun that made the kill made none of the player's last few kills.</summary>
        public bool freshGun;
        /// <summary>For a marked target: all four limbs were off before the kill.</summary>
        public bool takenApart;
    }

    /// <summary>
    /// Style feats on a kill. Following ULTRAKILL and Doom Eternal (the research report), style pays a bonus, but
    /// playing plain never costs anything (Bulletstorm chose bonuses over repeat penalties).
    /// </summary>
    public static class StyleKill
    {
        /// <summary>How many feats a kill has, and their names joined for the pop-up ("HEADSHOT + AIR").</summary>
        public static int Feats(in KillFacts f, out string label)
        {
            int n = 0;
            label = string.Empty;
            if (f.headshot) Add(ref n, ref label, "HEADSHOT");
            if (f.airborne) Add(ref n, ref label, "AIRBORNE");
            if (f.rover) Add(ref n, ref label, "SPLAT");
            if (f.melee) Add(ref n, ref label, "MELEE");
            if (f.freshGun) Add(ref n, ref label, "FRESH GUN");
            if (f.takenApart) Add(ref n, ref label, "TAKEN APART");
            return n;
        }

        static void Add(ref int n, ref string label, string word)
        {
            n++;
            label = label.Length == 0 ? word : label + " + " + word;
        }

        /// <summary>The ante rules' skill multiplier for this many feats: none 1, one the verified bonus, two or more the ceiling.</summary>
        public static float SkillFor(AnteBalance balance, int feats)
        {
            if (feats <= 0) return 1f;
            return feats == 1 ? balance.skillVerified : balance.skillMax;
        }
    }

    /// <summary>Which guns made the player's last few kills, for the "fresh gun" feat.</summary>
    public sealed class GunFreshness
    {
        readonly int _memory;
        readonly List<string> _last = new List<string>();

        public GunFreshness(int memory) { _memory = Math.Max(1, memory); }

        /// <summary>True when this gun made none of the remembered kills (and some kill is remembered).</summary>
        public bool IsFresh(string gunId)
        {
            if (string.IsNullOrEmpty(gunId) || _last.Count == 0) return false;
            for (int i = 0; i < _last.Count; i++) if (_last[i] == gunId) return false;
            return true;
        }

        /// <summary>Remembers a kill. Melee and the rover count as their own "guns", so they freshen the next shot.</summary>
        public void Remember(string gunId)
        {
            if (string.IsNullOrEmpty(gunId)) return;
            _last.Add(gunId);
            while (_last.Count > _memory) _last.RemoveAt(0);
        }

        public void Clear() => _last.Clear();
    }

    /// <summary>Kills and finished task steps in quick succession. Lapses if you stop; never takes points away.</summary>
    public sealed class Streak
    {
        readonly float _window;
        float _last = float.NegativeInfinity;

        public Streak(float window) { _window = window; }

        public int Count { get; private set; }
        public int Best { get; private set; }

        /// <summary>The streak as it stands at this time (0 once the window has passed).</summary>
        public int CountAt(float now) => now - _last <= _window ? Count : 0;

        /// <summary>Seconds left before the streak lapses (0 if it has).</summary>
        public float LeftAt(float now) => Count == 0 ? 0f : Math.Max(0f, _window - (now - _last));

        public int Add(float now)
        {
            Count = now - _last <= _window ? Count + 1 : 1;
            _last = now;
            if (Count > Best) Best = Count;
            return Count;
        }

        /// <summary>Spends the streak on a task (the feat it earned is used up) and starts it again from this step.</summary>
        public void Spend(float now)
        {
            Count = 1;
            _last = now;
        }

        public void Clear()
        {
            Count = 0;
            _last = float.NegativeInfinity;
        }
    }

    /// <summary>One base's clearing: its guards, the first shot there, and whether it was done quietly.</summary>
    public sealed class BaseClear
    {
        public readonly int Size;
        public readonly int Guards;
        public int Down { get; private set; }
        public float FirstShot { get; private set; } = float.NaN;
        /// <summary>A guard saw the player (or a flare went up) before the base was cleared: it was not done quietly.</summary>
        public bool AlarmRaised { get; private set; }
        public bool Cleared => Down >= Guards && Guards > 0;
        public int Left => Math.Max(0, Guards - Down);

        public BaseClear(int size, int guards)
        {
            Size = size;
            Guards = Math.Max(0, guards);
        }

        public void Engaged(float now) { if (float.IsNaN(FirstShot)) FirstShot = now; }
        public void Alarm() => AlarmRaised = true;

        /// <summary>One guard is down. True when that was the last one.</summary>
        public bool GuardDown(float now)
        {
            if (Cleared) return false;
            Engaged(now);
            Down++;
            return Cleared;
        }

        /// <summary>A guard that was already gone (from before the base was looked at again) counts without starting the clock.</summary>
        public void AlreadyGone() { if (!Cleared) Down++; }

        /// <summary>Feats for the clear: quiet (never seen) and quick (within the window of the first shot).</summary>
        public int Feats(TaskRules rules, float now, out string label)
        {
            int n = 0;
            label = string.Empty;
            if (!AlarmRaised) { n++; label = "QUIET"; }
            if (!float.IsNaN(FirstShot) && now - FirstShot <= rules.baseQuickSeconds) { n++; label = label.Length == 0 ? "QUICK" : label + " + QUICK"; }
            return n;
        }
    }

    /// <summary>A signal mast: cells that must all break. While it stands it calls the enemies near it onto the player.</summary>
    public sealed class SignalMast
    {
        readonly float[] _cell;
        readonly float _callEvery;
        float _firstBreak = float.NaN;
        float _nextCall = float.NaN;

        public SignalMast(int cells, float cellHealth, float callEvery)
        {
            _cell = new float[Math.Max(1, cells)];
            for (int i = 0; i < _cell.Length; i++) _cell[i] = Math.Max(1f, cellHealth);
            _callEvery = Math.Max(1f, callEvery);
        }

        public int Cells => _cell.Length;
        public bool CellBroken(int i) => i < 0 || i >= _cell.Length || _cell[i] <= 0f;
        public int CellsLeft { get { int n = 0; for (int i = 0; i < _cell.Length; i++) if (_cell[i] > 0f) n++; return n; } }
        public bool Down => CellsLeft == 0;
        public float BrokenWithin { get; private set; } = float.NaN;

        /// <summary>Damages one cell. True when this hit brought the whole mast down.</summary>
        public bool Hit(int cell, float damage, float now)
        {
            if (Down || cell < 0 || cell >= _cell.Length || _cell[cell] <= 0f || damage <= 0f) return false;
            _cell[cell] -= damage;
            if (_cell[cell] > 0f) return false;
            if (float.IsNaN(_firstBreak)) _firstBreak = now;
            if (!Down) return false;
            BrokenWithin = now - _firstBreak;
            return true;
        }

        /// <summary>A rover ramming it breaks every cell at once.</summary>
        public bool Rammed(float now)
        {
            if (Down) return false;
            for (int i = 0; i < _cell.Length; i++) _cell[i] = 0f;
            if (float.IsNaN(_firstBreak)) _firstBreak = now;
            BrokenWithin = now - _firstBreak;
            return true;
        }

        /// <summary>
        /// True when the mast calls now. It only calls while standing and while the player is in reach; the
        /// first call comes a full interval after the player arrives, so walking up to one is never punished at once.
        /// </summary>
        public bool Calls(float now, bool playerInReach)
        {
            if (Down || !playerInReach) { _nextCall = float.NaN; return false; }
            if (float.IsNaN(_nextCall)) { _nextCall = now + _callEvery; return false; }
            if (now < _nextCall) return false;
            _nextCall = now + _callEvery;
            return true;
        }

        /// <summary>Seconds until the next call while the player stays in reach (NaN when it is not counting).</summary>
        public float NextCallIn(float now) => float.IsNaN(_nextCall) ? float.NaN : Math.Max(0f, _nextCall - now);
    }

    public enum CrateState { Lying, Carried, Delivered }

    /// <summary>A crate of salvage: picked up, carried (or driven) back, delivered aboard the ship.</summary>
    public sealed class SalvageCrate
    {
        public CrateState State { get; private set; } = CrateState.Lying;
        /// <summary>The player was hurt at some point while carrying it (no clean-delivery feat).</summary>
        public bool HurtWhileCarried { get; private set; }

        public bool PickUp()
        {
            if (State != CrateState.Lying) return false;
            State = CrateState.Carried;
            return true;
        }

        public bool Drop()
        {
            if (State != CrateState.Carried) return false;
            State = CrateState.Lying;
            return true;
        }

        public void Hurt() { if (State == CrateState.Carried) HurtWhileCarried = true; }

        public bool Deliver()
        {
            if (State != CrateState.Carried) return false;
            State = CrateState.Delivered;
            return true;
        }
    }

    public enum DropState { Falling, Landed, Opened, Gone }

    /// <summary>A supply pod: falls, lands, is raced for, and opened (or left to time out).</summary>
    public sealed class SupplyDrop
    {
        readonly TaskRules _rules;
        public readonly float LandsAt;
        public DropState State { get; private set; } = DropState.Falling;
        /// <summary>An enemy got to it before the player opened it.</summary>
        public bool Reached { get; private set; }

        public SupplyDrop(TaskRules rules, float now)
        {
            _rules = rules;
            LandsAt = now + Math.Max(0.5f, rules.dropFallSeconds);
        }

        /// <summary>How far down its fall it is, 0 at the top of the sky to 1 on the ground.</summary>
        public float Fallen(float now) => Math.Min(1f, Math.Max(0f, 1f - (LandsAt - now) / Math.Max(0.5f, _rules.dropFallSeconds)));

        /// <summary>Moves it on. True on the frame it lands.</summary>
        public bool Tick(float now)
        {
            if (State == DropState.Falling && now >= LandsAt) { State = DropState.Landed; return true; }
            if (State == DropState.Landed && now >= LandsAt + _rules.dropLasts) State = DropState.Gone;
            return false;
        }

        public void EnemyNear(float distance)
        {
            if (State == DropState.Landed && distance <= _rules.dropReachedWithin) Reached = true;
        }

        public bool Open()
        {
            if (State != DropState.Landed) return false;
            State = DropState.Opened;
            return true;
        }
    }

    /// <summary>When the supply drops of an ante fall. Plain timing; where they land is the director's (seeded) choice.</summary>
    public sealed class DropClock
    {
        readonly TaskRules _rules;
        float _anteStart = float.NaN;
        int _given;

        public DropClock(TaskRules rules) { _rules = rules; }

        public void AnteStarted(float now) { _anteStart = now; _given = 0; }
        public void Stop() { _anteStart = float.NaN; }

        /// <summary>The number of this ante's drops (0 is the first). Kept so claim ids are stable.</summary>
        public int Given => _given;

        /// <summary>True once each time a drop is due.</summary>
        public bool Due(float now)
        {
            if (float.IsNaN(_anteStart)) return false;
            float at = _anteStart + _rules.dropFirstAfter + _given * Math.Max(10f, _rules.dropEvery);
            if (now < at) return false;
            _given++;
            return true;
        }
    }

    /// <summary>Which ante-table tier an enemy type scores as. The tier is in the points and never looked at again.</summary>
    public static class EnemyTiers
    {
        public static ScoreEventKind KindFor(string typeId, float scale)
        {
            switch (typeId)
            {
                case "Bruiser":
                case "HeavyA": return ScoreEventKind.EnemyTier3;
                case "Marksman":
                case "LineA": return ScoreEventKind.EnemyTier2;
                case "Runner":
                case "Trooper":
                case "ScoutA": return ScoreEventKind.EnemyTier1;
            }
            // Kinds added later: big bodies are heavy, small ones light.
            if (scale >= 1.25f) return ScoreEventKind.EnemyTier3;
            return scale >= 1.1f ? ScoreEventKind.EnemyTier2 : ScoreEventKind.EnemyTier1;
        }
    }

    /// <summary>
    /// How bumpy the ground is under a landing, from the ground radius under the ship and at points around it.
    /// A rock-tower top stands well above its surroundings; a canyon floor sits well below.
    /// </summary>
    public static class LandingRelief
    {
        public static bool IsHard(double here, IReadOnlyList<double> around, float relief)
        {
            if (around == null || around.Count == 0) return false;
            double sum = 0;
            for (int i = 0; i < around.Count; i++) sum += around[i];
            return Math.Abs(here - sum / around.Count) >= relief;
        }

        /// <summary>A coarse square on the world for one claim: longitude and latitude cells of about <paramref name="cell"/> metres.</summary>
        public static string Cell(double x, double y, double z, double radius, float cell)
        {
            double len = Math.Sqrt(x * x + y * y + z * z);
            if (len <= 0 || radius <= 0 || cell <= 0) return "0.0";
            double lat = Math.Asin(Math.Max(-1, Math.Min(1, y / len)));
            double lon = Math.Atan2(z, x);
            double step = cell / radius;
            return (int)Math.Floor(lat / step) + "." + (int)Math.Floor(lon / step);
        }
    }

    /// <summary>The claim ids tasks pay under. Built from seeded world ids so they are the same after a reload.</summary>
    public static class TaskIds
    {
        public static string Enemy(string body, int camp, int member) => "enemy:" + body + ".c" + camp + ".m" + member;
        public static string BaseClear(string body, int baseIndex) => "base:" + body + "." + baseIndex;
        public static string Mast(string body, int baseIndex) => "mast:" + body + "." + baseIndex;
        public static string Salvage(string body, int n) => "salvage:" + body + "." + n;
        public static string Drop(int ante, int n) => "drop:a" + ante + "." + n;
        public static string FirstLanding(string body) => "landing:" + body;
        public static string HardLanding(string body, string cell) => "hardlanding:" + body + "." + cell;
    }
}
