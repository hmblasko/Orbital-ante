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
        /// <summary>A kill from at least this far (metres from the eye) is a long shot.</summary>
        public float longShot = 60f;
        /// <summary>
        /// How slowed the world must be by Focus (<c>DeepBreathFx.Amount</c>, 0 to 1) for a kill to be a Focus kill: past
        /// half way, so a kill in the tail of the ease out after letting go does not count (a goal unlock, never a feat).
        /// </summary>
        public const float FocusKillAt = 0.5f;

        // ---------------------------------------------------------------- signal masts
        /// <summary>Share of bases that have a signal mast standing near them.</summary>
        public float mastShare = 0.5f;
        /// <summary>How far from the middle of its base a mast stands.</summary>
        public float mastNear = 40f, mastFar = 70f;
        /// <summary>Glowing cells on a mast; all must go to bring it down.</summary>
        public int mastCells = 3;
        /// <summary>Damage a cell takes before it breaks (a rifle round is about 20).</summary>
        public float mastCellHealth = 70f;
        /// <summary>While a mast stands and the player is in its reach, it calls everyone near onto the player this often.</summary>
        public float mastCallEvery = 45f;
        /// <summary>How near the player must be for the mast to call, and how far its call is heard.</summary>
        public float mastReach = 250f;
        /// <summary>A rover hitting a mast at least this fast breaks every cell (it takes a run-up). Ramming is never a feat.</summary>
        public float mastRamSpeed = 15f;

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
        /// <summary>Hold Interact this long at a pod to take gun-ladder kills instead of a Focus refill.</summary>
        public float dropHoldForGun = 0.6f;
        /// <summary>The share of the Focus meter a pod refills (Deep Breath otherwise only refills from limbs and kills).</summary>
        public float dropFocus = 0.5f;
        /// <summary>Kills a pod counts toward the gun ladder when held (once an ante; after that it is Focus only).</summary>
        public int dropLadderKills = 3;

        // ---------------------------------------------------------------- marked targets
        /// <summary>One marked target per world that has camps.</summary>
        public bool markedTargets = true;

        // ---------------------------------------------------------------- landings
        /// <summary>A touchdown at this speed or less is soft enough to count as a feat.</summary>
        public float softTouchdown = 0.7f;
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
        /// <summary>The game lights this many of the nearest tasks within <see cref="nearbyReach"/> of the player.</summary>
        public int liveNearby = 3;
        public float nearbyReach = 1500f;


        /// <summary>
        /// Distances are first guesses for a large world; the worlds are a few hundred metres across (Earth 500 m,
        /// the Moon 150 m), so every reach is held to a share of the world. Over the ground, not straight through it.
        /// </summary>
        public static float Fit(float value, double worldRadius, double share)
        {
            if (worldRadius <= 0) return value;
            return (float)Math.Min(value, worldRadius * share);
        }

        public float MastReachOn(double r) => Fit(mastReach, r, 0.5);
        public float DropHeardOn(double r) => Fit(dropHeardFrom, r, 0.5);
        public float NearbyOn(double r) => Fit(nearbyReach, r, Math.PI);
        /// <summary>A drop lands this far over the ground from the player: kept under a third of the way round.</summary>
        public float DropNearOn(double r) => Fit(dropNear, r, 0.35 * Math.PI * 0.55);
        public float DropFarOn(double r) => Fit(dropFar, r, 0.35 * Math.PI);

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
        /// <summary>
        /// The first hit that landed on that enemy killed it. (Not "headshot": the head is the only way to die, so
        /// nearly every kill would be one; the gold headshot marker stays as feedback only.)
        /// </summary>
        public bool oneShot;
        /// <summary>The killing hit came from at least <see cref="TaskRules.longShot"/> away.</summary>
        public bool longShot;
        /// <summary>The player was off the ground: jetpack, a jump, floating.</summary>
        public bool airborne;
        /// <summary>Run over with the rover.</summary>
        public bool rover;
        public bool melee;
        /// <summary>The first kill this ante with this tool, after a kill with another one (it rewards switching, once a tool).</summary>
        public bool freshGun;
        /// <summary>All four limbs were off before the kill.</summary>
        public bool takenApart;
        /// <summary>The player was crouched or lying prone (not a feat: it counts toward a goal unlock, Ante/Unlocks.cs).</summary>
        public bool lowStance;
        /// <summary>The player's armour was under the low-health line, as Last Stand reads it (not a feat: a goal unlock).</summary>
        public bool lowArmour;
        /// <summary>Focus had the world at least half slowed (<see cref="TaskRules.FocusKillAt"/>; not a feat: a goal unlock).</summary>
        public bool inFocus;
        /// <summary>A sea creature, killed from in the water with it (not a feat: a goal unlock).</summary>
        public bool seaCreature;
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
            // A splat is its own feat and only that: the rover is fast and easy, so nothing stacks on it.
            if (f.rover)
            {
                label = "SPLAT";
                return 1;
            }
            if (f.oneShot) Add(ref n, ref label, "ONE SHOT");
            if (f.longShot) Add(ref n, ref label, "LONG SHOT");
            if (f.airborne) Add(ref n, ref label, "AIRBORNE");
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

    /// <summary>
    /// The tools (each gun, melee, the rover) that have made a kill this ante, for the "fresh gun" feat. A tool is
    /// fresh once an ante, and only after a kill with another tool, so cycling can earn it at most once a tool.
    /// </summary>
    public sealed class GunFreshness
    {
        readonly HashSet<string> _used = new HashSet<string>();

        /// <summary>True when this tool has made no kill this ante and some other tool has.</summary>
        public bool IsFresh(string gunId) => !string.IsNullOrEmpty(gunId) && _used.Count > 0 && !_used.Contains(gunId);

        public void Remember(string gunId)
        {
            if (!string.IsNullOrEmpty(gunId)) _used.Add(gunId);
        }

        /// <summary>A new ante: every tool is fresh again.</summary>
        public void Clear() => _used.Clear();
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
        /// <summary>It has called for help at least once (bringing it down before that is the feat).</summary>
        public bool HasCalled { get; private set; }

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
            HasCalled = true;
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
        public bool AwaitingDelivery => State != CrateState.Delivered;
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

    /// <summary>The two choices on a landed pod. Gun credit stops at the next unlock. A tap always claims the pod.</summary>
    public readonly struct SupplyDropOffer
    {
        public readonly int GunKills;
        public readonly int FocusPercent;

        public SupplyDropOffer(TaskRules rules, float meter, int killsToNext)
        {
            GunKills = Math.Max(0, Math.Min(rules.dropLadderKills, killsToNext));
            FocusPercent = meter < 1f && rules.dropFocus > 0f
                ? Percent(Math.Min(1f, meter + rules.dropFocus) - meter) : 0;
        }

        // -1 means a positive gain below one percent. It must never be shown as zero.
        static int Percent(float share) => share <= 0f ? 0 : share < 0.01f ? -1
            : (int)Math.Round(share * 100f, MidpointRounding.AwayFromZero);
        static string Amount(int percent) => percent < 0 ? "<1%" : percent + "%";
        static string Kills(int count) => count + (count == 1 ? " gun kill" : " gun kills");

        public string Prompt(string key)
        {
            string tap = key + ": claim points";
            if (FocusPercent != 0) tap += " + " + Amount(FocusPercent) + " Focus";
            return GunKills > 0 ? tap + "     hold " + key + ": points + " + Kills(GunKills) : tap;
        }

        public static string Receipt(int killsCredited, float focusGained)
        {
            if (killsCredited > 0) return "SUPPLY DROP: +" + Kills(killsCredited).ToUpperInvariant();
            if (focusGained > 0f) return "SUPPLY DROP: +" + Amount(Percent(focusGained)) + " FOCUS";
            return "SUPPLY DROP";
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
    /// Which kill tier a creature scores as, by its kind (its model's name), on the same table as the camps: a
    /// creature fight counts toward the ante like any other fight, so the seas are somewhere to score, not a
    /// detour. Tiers follow how much it takes to bring one down and how long it keeps the player in danger.
    /// </summary>
    public static class CreatureTiers
    {
        /// <summary>Every creature kind there is, for the rule tests.</summary>
        public static readonly string[] Kinds = { "Strider", "Sprinter", "Roller", "Hunter", "Screamer", "Ambusher", "Stalker", "Maw", "Biter" };

        /// <summary>
        /// The note when a sea creature is killed from out of its reach (from the shore, or from over the sea): it could
        /// not fight back, so it pays nothing, and this says what does.
        /// </summary>
        public const string OutOfReach = "SEA KILLS PAY ONLY IN THE WATER WITH THEM";

        /// <summary>The creatures of the seas (<see cref="Creatures.SeaCreatures"/>): the Drifting maw and the Pack biter.</summary>
        public static bool Swims(string kind) => kind == "Maw" || kind == "Biter";

        public static ScoreEventKind KindFor(string kind)
        {
            switch (kind)
            {
                // A Pack biter dies to a shot or two in the head; the danger is the pack, and every fish pays.
                case "Biter": return ScoreEventKind.EnemyTier1;
                // The two big ones: hundreds of points of body to get through, armour over the rest, and a strike that
                // holds or flattens. Paid like a camp's heavy.
                case "Maw":
                case "Strider": return ScoreEventKind.EnemyTier3;
                // The rest (Sprinter, Roller, Cave hunter, Screamer, Ambusher, Blind stalker) and kinds added later:
                // one weak place to find and one strike to read, like a marksman.
                default: return ScoreEventKind.EnemyTier2;
            }
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
        /// <summary>A creature: its world, its haunt there and its place in the haunt (a fish's place in its pack; 0 for one alone).</summary>
        public static string Creature(string body, int haunt, int place) => "creature:" + body + ".h" + haunt + ".p" + place;
        public static string Mast(string body, int baseIndex) => "mast:" + body + "." + baseIndex;
        public static string Salvage(string body, int n) => "salvage:" + body + "." + n;
        public static string Drop(int ante, int n) => "drop:a" + ante + "." + n;
        public static string FirstLanding(string body) => "landing:" + body;
        public static string HardLanding(string body, string cell) => "hardlanding:" + body + "." + cell;
    }
}
