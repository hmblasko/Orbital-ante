using System;
using System.Collections.Generic;
using System.Globalization;
using OrbitalAnte.Save;

namespace OrbitalAnte.Ante
{
    /// <summary>
    /// One clear thing to do in one run that opens something for every run after (Ralph loop pass 1, 27 Nov 2026;
    /// Part B section 5, "Balatro-style goal unlocks"). Every goal is a single feat inside one run, never a count
    /// across runs, so nothing is a grind (constitution rule 9), and each one teaches a part of the game its unlock
    /// is about: crouching opens the perk for shooting low, a rover splat the perk that heals on one.
    /// The order here is the order they are listed and suggested in, easiest first.
    /// </summary>
    public enum UnlockGoal
    {
        ClearAnte2 = 0,
        LowStanceKill = 1,
        RoverSplat = 2,
        LongShotKill = 3,
        ThreeWorldsInOneAnte = 4,
        FourWaysInOneAnte = 5,
        BankEarly = 6,
        LadderTop = 7,
        MeleeKill = 8,
        HazardBeatsTheClock = 9,
    }

    public enum UnlockKind
    {
        /// <summary>A perk that joins the drafts.</summary>
        Perk = 0,
        /// <summary>A starting kit to choose on the title screen.</summary>
        Kit = 1,
    }

    public sealed class UnlockDefinition
    {
        /// <summary>Kept in <see cref="SaveData.unlocks"/>: never rename one.</summary>
        public string id;
        public UnlockKind kind;
        /// <summary>The perk id ("perk.road_rage") or kit id ("kit.ghost") it opens.</summary>
        public string target;
        public UnlockGoal goal;
        /// <summary>What it is called on screen ("Road Rage", "Ghost kit"), taken from the perk or kit itself.</summary>
        public string name;
    }

    /// <summary>
    /// A starting kit: two perks of one family held from the first second of a run, in place of the perk picks of
    /// antes 1 and 2 (<see cref="AnteDirector.PicksGivenAhead"/>). A sidegrade, as Part B section 4 asks of every
    /// unlock: the run starts with a direction and its perks two antes early, and gives up choosing them from
    /// offers (which can be rarer, and fit the run as it goes). Standard is no kit: every pick is a choice.
    /// </summary>
    public sealed class StartingKit
    {
        /// <summary>Kept in <see cref="SaveData.kit"/>: never rename one.</summary>
        public string id;
        public string displayName;
        public PerkFamily family;
        public string[] perkIds = new string[0];
    }

    public static class StartingKits
    {
        public const string StandardId = "kit.standard";

        static readonly StartingKit[] _all =
        {
            new StartingKit { id = StandardId, displayName = "Standard", family = PerkFamily.None },
            new StartingKit { id = "kit.gunner", displayName = "Gunner kit", family = PerkFamily.Gunner, perkIds = new[] { "perk.quick_study", "perk.steady_hands" } },
            new StartingKit { id = "kit.ghost", displayName = "Ghost kit", family = PerkFamily.Ghost, perkIds = new[] { "perk.eagle_eye", "perk.cold_blood" } },
            new StartingKit { id = "kit.pilot", displayName = "Pilot kit", family = PerkFamily.Pilot, perkIds = new[] { "perk.frequent_flyer", "perk.fleet_foot" } },
            new StartingKit { id = "kit.daredevil", displayName = "Daredevil kit", family = PerkFamily.Daredevil, perkIds = new[] { "perk.glass_cannon", "perk.no_brakes" } },
        };

        /// <summary>Standard first, then the kits in the order their goals are listed.</summary>
        public static IReadOnlyList<StartingKit> All => _all;
        public static StartingKit Standard => _all[0];

        public static StartingKit Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < _all.Length; i++) if (_all[i].id == id) return _all[i];
            return null;
        }

        /// <summary>What a kit costs, said the same way wherever it is offered.</summary>
        public const string CostWords = "No perk pick in antes 1 and 2: these two are those picks.";

        /// <summary>"Start with Quick Study and Steady Hands." or, for Standard, "No perks to start: every pick is yours to choose."</summary>
        public static string Summary(StartingKit kit, IReadOnlyList<PerkDefinition> catalog)
        {
            if (kit == null || kit.perkIds.Length == 0) return "No perks to start: every pick is yours to choose.";
            var names = new List<string>();
            for (int i = 0; i < kit.perkIds.Length; i++) names.Add(PerkName(kit.perkIds[i], catalog));
            return "Start with " + JoinAnd(names) + ".";
        }

        /// <summary>One line per perk, "Quick Study: ...", then the cost. Empty for Standard.</summary>
        public static string Details(StartingKit kit, IReadOnlyList<PerkDefinition> catalog, AnteBalance balance)
        {
            if (kit == null || kit.perkIds.Length == 0) return string.Empty;
            var lines = new List<string>();
            for (int i = 0; i < kit.perkIds.Length; i++)
            {
                PerkDefinition p = FindPerk(kit.perkIds[i], catalog);
                lines.Add(p == null ? kit.perkIds[i] : p.displayName + ": " + p.Describe(balance));
            }
            lines.Add(CostWords);
            return string.Join("\n", lines);
        }

        internal static PerkDefinition FindPerk(string id, IReadOnlyList<PerkDefinition> catalog)
        {
            if (catalog == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < catalog.Count; i++) if (catalog[i] != null && catalog[i].id == id) return catalog[i];
            return null;
        }

        static string PerkName(string id, IReadOnlyList<PerkDefinition> catalog)
        {
            PerkDefinition p = FindPerk(id, catalog);
            return p != null ? p.displayName : id;
        }

        internal static string JoinAnd(List<string> parts)
        {
            if (parts.Count == 0) return string.Empty;
            if (parts.Count == 1) return parts[0];
            return string.Join(", ", parts.GetRange(0, parts.Count - 1)) + " and " + parts[parts.Count - 1];
        }
    }

    /// <summary>The ten unlocks, easiest goal first. Six perks start locked and four kits wait to be earned.</summary>
    public static class UnlockCatalog
    {
        static readonly List<PerkDefinition> Perks = RoguelitePerks.Create();

        static readonly UnlockDefinition[] _all =
        {
            Kit("kit.gunner", UnlockGoal.ClearAnte2),
            Perk("perk.patient_hunter", UnlockGoal.LowStanceKill),
            Perk("perk.road_rage", UnlockGoal.RoverSplat),
            Kit("kit.ghost", UnlockGoal.LongShotKill),
            Kit("kit.pilot", UnlockGoal.ThreeWorldsInOneAnte),
            Perk(PerkCatalog.ChainPerkId, UnlockGoal.FourWaysInOneAnte),
            Kit("kit.daredevil", UnlockGoal.BankEarly),
            Perk("perk.trigger_discipline", UnlockGoal.LadderTop),
            Perk("perk.in_your_face", UnlockGoal.MeleeKill),
            Perk("perk.all_in", UnlockGoal.HazardBeatsTheClock),
        };

        static UnlockDefinition Perk(string perkId, UnlockGoal goal)
        {
            PerkDefinition p = StartingKits.FindPerk(perkId, Perks);
            return new UnlockDefinition { id = "unlock." + perkId, kind = UnlockKind.Perk, target = perkId, goal = goal, name = p != null ? p.displayName : perkId };
        }

        static UnlockDefinition Kit(string kitId, UnlockGoal goal)
        {
            StartingKit k = StartingKits.Find(kitId);
            return new UnlockDefinition { id = "unlock." + kitId, kind = UnlockKind.Kit, target = kitId, goal = goal, name = k != null ? k.displayName : kitId };
        }

        public static IReadOnlyList<UnlockDefinition> All => _all;

        public static UnlockDefinition Find(string unlockId)
        {
            for (int i = 0; i < _all.Length; i++) if (_all[i].id == unlockId) return _all[i];
            return null;
        }

        /// <summary>The unlock that guards a perk or kit, or null when nothing does (it is open from the start).</summary>
        public static UnlockDefinition ForTarget(string target)
        {
            if (string.IsNullOrEmpty(target)) return null;
            for (int i = 0; i < _all.Length; i++) if (_all[i].target == target) return _all[i];
            return null;
        }

        public static UnlockDefinition ForGoal(UnlockGoal goal)
        {
            for (int i = 0; i < _all.Length; i++) if (_all[i].goal == goal) return _all[i];
            return null;
        }

        /// <summary>
        /// The goal in plain words, said the same way everywhere (title screen, kit page, recap). The LONG SHOT
        /// distance is the task rules' own number.
        /// </summary>
        public static string GoalWords(UnlockGoal goal, float longShotMetres = 60f)
        {
            switch (goal)
            {
                case UnlockGoal.ClearAnte2: return "Clear ante 2.";
                case UnlockGoal.LowStanceKill: return "Kill an enemy while crouched or lying prone.";
                case UnlockGoal.RoverSplat: return "Splat an enemy with the rover.";
                case UnlockGoal.LongShotKill:
                    return "Kill an enemy from " + Math.Round(Math.Max(1f, longShotMetres)).ToString(CultureInfo.InvariantCulture) + " m or more (a LONG SHOT).";
                case UnlockGoal.ThreeWorldsInOneAnte: return "Land on " + UnlockTracker.WorldsInOneAnte + " different worlds in one ante.";
                case UnlockGoal.FourWaysInOneAnte:
                    return "Score " + UnlockTracker.WaysInOneAnte + " different ways in one ante: fighting, exploring, landings, tasks, salvage.";
                case UnlockGoal.BankEarly: return "Bank an ante with " + Clock(UnlockTracker.BankEarlySeconds) + " or more still on its clock.";
                case UnlockGoal.LadderTop: return "Climb the gun ladder to its last gun.";
                case UnlockGoal.MeleeKill: return "Kill an enemy with a melee weapon (some bases have one).";
                case UnlockGoal.HazardBeatsTheClock: return "Raise the stakes, then clear that ante before its clock runs out.";
                default: return goal.ToString();
            }
        }

        /// <summary>"a Pilot perk" or "a starting kit": what kind of thing an unlock is.</summary>
        public static string KindWords(UnlockDefinition d)
        {
            if (d == null) return string.Empty;
            if (d.kind == UnlockKind.Kit) return "a starting kit";
            PerkDefinition p = StartingKits.FindPerk(d.target, Perks);
            return p == null || p.family == PerkFamily.None ? "a wild-card perk" : "a " + p.family + " perk";
        }

        /// <summary>What it gives once earned: a perk's effect in words, or a kit's two perks.</summary>
        public static string GiftWords(UnlockDefinition d, AnteBalance balance = null)
        {
            if (d == null) return string.Empty;
            if (d.kind == UnlockKind.Kit) return StartingKits.Summary(StartingKits.Find(d.target), Perks);
            PerkDefinition p = StartingKits.FindPerk(d.target, Perks);
            return p == null ? string.Empty : Capital(p.Describe(balance ?? new AnteBalance())) + ".";
        }

        /// <summary>The note said the moment one is earned: what it is and where it now turns up.</summary>
        public static string EarnedNote(UnlockDefinition d)
        {
            if (d == null) return string.Empty;
            return d.kind == UnlockKind.Kit
                ? "UNLOCKED: " + d.name.ToUpperInvariant() + ". CHOOSE IT WHEN YOU START A RUN"
                : "UNLOCKED: " + d.name.ToUpperInvariant() + ". IN YOUR PERK DRAFTS FROM THE NEXT ANTE";
        }

        static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        static string Clock(float seconds)
        {
            int s = Math.Max(0, (int)Math.Ceiling(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// The goals met in one run, fed by the ante loop and the task director. Plain C#: it only counts what it is
    /// told, and hands each goal back once (<see cref="Take"/>). Per-ante goals start again at each new ante.
    /// </summary>
    public sealed class UnlockTracker
    {
        public const int WorldsInOneAnte = 3;
        public const int WaysInOneAnte = 4;
        /// <summary>Banking with this much on the clock or more is banking early.</summary>
        public const float BankEarlySeconds = 60f;

        readonly HashSet<string> _worlds = new HashSet<string>();
        /// <summary>One bit per <see cref="ScoreCategory"/> scored this ante.</summary>
        int _ways;
        readonly HashSet<UnlockGoal> _metThisRun = new HashSet<UnlockGoal>();
        readonly List<UnlockGoal> _waiting = new List<UnlockGoal>();

        /// <summary>A new ante has begun: the in-one-ante goals count from nothing again.</summary>
        public void AnteBegan()
        {
            _worlds.Clear();
            _ways = 0;
        }

        /// <summary>Points were awarded in a running ante, of this kind.</summary>
        public void Scored(ScoreCategory category)
        {
            int bit = 1 << Math.Max(0, Math.Min(30, (int)category));
            if ((_ways & bit) != 0) return;
            _ways |= bit;
            if (Ways(_ways) >= WaysInOneAnte) Met(UnlockGoal.FourWaysInOneAnte);
        }

        static int Ways(int bits)
        {
            int n = 0;
            for (; bits != 0; bits &= bits - 1) n++;
            return n;
        }

        /// <summary>The ship touched down on a world (by its id) in a running ante.</summary>
        public void Landed(string worldId)
        {
            if (string.IsNullOrEmpty(worldId)) return;
            if (_worlds.Add(worldId) && _worlds.Count >= WorldsInOneAnte) Met(UnlockGoal.ThreeWorldsInOneAnte);
        }

        /// <summary>The player killed an enemy in a running ante.</summary>
        public void Kill(bool melee, bool rover, bool longShot, bool lowStance)
        {
            if (melee) Met(UnlockGoal.MeleeKill);
            if (rover) Met(UnlockGoal.RoverSplat);
            if (longShot) Met(UnlockGoal.LongShotKill);
            // From the rover's seat nobody is crouching.
            if (lowStance && !rover) Met(UnlockGoal.LowStanceKill);
        }

        /// <summary>An ante was cleared (banked by choice, or its clock ran out with the target met, or met in overtime).</summary>
        public void AnteCleared(int ante, float secondsLeft, bool hazardOn, bool inOvertime)
        {
            if (ante >= 2) Met(UnlockGoal.ClearAnte2);
            if (!inOvertime && secondsLeft >= BankEarlySeconds) Met(UnlockGoal.BankEarly);
            if (hazardOn && !inOvertime) Met(UnlockGoal.HazardBeatsTheClock);
        }

        /// <summary>The gun ladder handed over its last gun.</summary>
        public void LadderTop() => Met(UnlockGoal.LadderTop);

        void Met(UnlockGoal goal)
        {
            if (_metThisRun.Add(goal)) _waiting.Add(goal);
        }

        /// <summary>Goals met since the last call, in the order they were met. Each goal comes back once a run.</summary>
        public List<UnlockGoal> Take()
        {
            var taken = new List<UnlockGoal>(_waiting);
            _waiting.Clear();
            return taken;
        }

        public bool HasWaiting => _waiting.Count > 0;
    }

    /// <summary>The unlocks kept in the save, and the kit chosen for the next run. Plain C#.</summary>
    public static class UnlockBook
    {
        static List<string> Earned(SaveData save)
        {
            if (save.unlocks == null) save.unlocks = new List<string>();
            return save.unlocks;
        }

        public static bool Has(SaveData save, string unlockId)
        {
            return save != null && save.unlocks != null && !string.IsNullOrEmpty(unlockId) && save.unlocks.Contains(unlockId);
        }

        /// <summary>A perk or kit is open when no unlock guards it, or its unlock has been earned.</summary>
        public static bool IsOpen(SaveData save, string target)
        {
            UnlockDefinition d = UnlockCatalog.ForTarget(target);
            return d == null || Has(save, d.id);
        }

        /// <summary>Records what a goal unlocks. Returns it when it is new, or null when it was earned before.</summary>
        public static UnlockDefinition Grant(SaveData save, UnlockGoal goal)
        {
            if (save == null) return null;
            UnlockDefinition d = UnlockCatalog.ForGoal(goal);
            if (d == null || Has(save, d.id)) return null;
            Earned(save).Add(d.id);
            return d;
        }

        /// <summary>Perks the drafts must not offer yet.</summary>
        public static HashSet<string> LockedPerkIds(SaveData save)
        {
            var locked = new HashSet<string>();
            IReadOnlyList<UnlockDefinition> all = UnlockCatalog.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].kind == UnlockKind.Perk && !Has(save, all[i].id)) locked.Add(all[i].target);
            return locked;
        }

        /// <summary>Standard and every kit earned, in the catalogue's order.</summary>
        public static List<StartingKit> OpenKits(SaveData save)
        {
            var open = new List<StartingKit>();
            IReadOnlyList<StartingKit> all = StartingKits.All;
            for (int i = 0; i < all.Count; i++) if (IsOpen(save, all[i].id)) open.Add(all[i]);
            return open;
        }

        /// <summary>The kit the next run starts with: the one chosen, if it is still known and open, else Standard.</summary>
        public static StartingKit ChosenKit(SaveData save)
        {
            StartingKit kit = save != null ? StartingKits.Find(save.kit) : null;
            return kit != null && IsOpen(save, kit.id) ? kit : StartingKits.Standard;
        }

        /// <summary>Chooses the kit for the next runs. A kit that is not open is refused (the choice stays Standard).</summary>
        public static bool Choose(SaveData save, string kitId)
        {
            if (save == null) return false;
            StartingKit kit = StartingKits.Find(kitId);
            bool ok = kit != null && IsOpen(save, kit.id);
            save.kit = ok ? kit.id : StartingKits.StandardId;
            return ok;
        }

        /// <summary>The first unlock not earned yet, in the catalogue's order (easiest first), or null when all are.</summary>
        public static UnlockDefinition Next(SaveData save)
        {
            IReadOnlyList<UnlockDefinition> all = UnlockCatalog.All;
            for (int i = 0; i < all.Count; i++) if (!Has(save, all[i].id)) return all[i];
            return null;
        }

        /// <summary>Earned and not yet looked at on the Unlocks page.</summary>
        public static bool IsNew(SaveData save, string unlockId)
        {
            return Has(save, unlockId) && (save.unlocksSeen == null || !save.unlocksSeen.Contains(unlockId));
        }

        public static int NewCount(SaveData save)
        {
            if (save == null || save.unlocks == null) return 0;
            int n = 0;
            for (int i = 0; i < save.unlocks.Count; i++) if (IsNew(save, save.unlocks[i])) n++;
            return n;
        }

        /// <summary>The Unlocks page has been shown: nothing on it is new any more.</summary>
        public static void MarkAllSeen(SaveData save)
        {
            if (save == null || save.unlocks == null) return;
            if (save.unlocksSeen == null) save.unlocksSeen = new List<string>();
            for (int i = 0; i < save.unlocks.Count; i++)
                if (!save.unlocksSeen.Contains(save.unlocks[i])) save.unlocksSeen.Add(save.unlocks[i]);
        }
    }
}
