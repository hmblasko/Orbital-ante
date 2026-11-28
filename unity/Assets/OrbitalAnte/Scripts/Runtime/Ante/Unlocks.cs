using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OrbitalAnte.Items;
using OrbitalAnte.Save;

namespace OrbitalAnte.Ante
{
    /// <summary>
    /// One clear thing to do in one run that opens something for every run after (Ralph loop pass 1, 27 Nov 2026;
    /// Part B section 5, "Balatro-style goal unlocks"). Every goal is a single feat inside one run, never a count
    /// across runs, so nothing is a grind (constitution rule 9), and each one teaches a part of the game its unlock
    /// is about: crouching opens the perk for shooting low, a rover splat the perk that heals on one, a melee kill
    /// the perk that hits harder with one. The order they are listed and suggested in, easiest first, is the
    /// catalogue's (<see cref="UnlockCatalog.All"/>), not this. Nothing saves these numbers (the save keeps unlock
    /// ids), but keep them, and add a new goal at the end.
    /// </summary>
    public enum UnlockGoal
    {
        ClearAnte2 = 0,
        LowStanceKill = 1,
        RoverSplat = 2,
        LongShotKill = 3,
        ThreeWorldsInOneAnte = 4,
        FourWaysInOneAnte = 5,
        LowArmourKill = 6,
        LadderTop = 7,
        MeleeKill = 8,
        HazardBeatsTheClock = 9,
        // Ralph loop pass 5 (28 Nov 2026): goals that give a look, the first three within a new player's first run.
        /// <summary>A gun knocked out of an enemy's hands: shot out, or the hand or arm holding it shot off.</summary>
        Disarm = 10,
        /// <summary>A leg shot off an enemy standing on two.</summary>
        LegOff = 11,
        /// <summary>A touchdown on any world but Earth.</summary>
        LandAwayFromEarth = 12,
        /// <summary>A salvage crate carried (or driven) back aboard the lander.</summary>
        SalvageHome = 13,
        /// <summary>A kill while Focus has the world slowed.</summary>
        FocusKill = 14,
        /// <summary>A kill while the player is off the ground (the AIRBORNE feat).</summary>
        AirborneKill = 15,
        /// <summary>A bonus mission finished.</summary>
        BonusMission = 16,
        /// <summary>A sea creature killed from in the water with it.</summary>
        SeaKill = 17,
    }

    public enum UnlockKind
    {
        /// <summary>A perk that joins the drafts.</summary>
        Perk = 0,
        /// <summary>A starting kit to choose on the title screen.</summary>
        Kit = 1,
        /// <summary>
        /// A look (Ralph loop pass 5): a ship paint, a gun skin or a suit piece that goes into the collection and, where
        /// its place is empty, into use (<see cref="Locker.AutoUse"/>). Purely how things look: it changes no number.
        /// Earned only by its goal, never found in an envelope (<see cref="ItemCatalog.IsEarned"/>).
        /// </summary>
        Look = 2,
    }

    public sealed class UnlockDefinition
    {
        /// <summary>Kept in <see cref="SaveData.unlocks"/>: never rename one.</summary>
        public string id;
        public UnlockKind kind;
        /// <summary>The perk id ("perk.road_rage"), kit id ("kit.ghost") or item id ("PaintRollPattern") it opens.</summary>
        public string target;
        public UnlockGoal goal;
        /// <summary>What it is called on screen ("Road Rage", "Ghost kit", "Roll Pattern"), taken from the perk, kit or item itself.</summary>
        public string name;
    }

    /// <summary>What the goal words need from the game: its own numbers, and the player's own keys as bound.</summary>
    public sealed class UnlockHints
    {
        /// <summary>The task rules' LONG SHOT distance.</summary>
        public float longShotMetres = 60f;
        /// <summary>The balance's low-health line, which Last Stand uses too.</summary>
        public float lowArmourLine = 0.30f;
        /// <summary>The Jets Down key or button: on the ground a tap crouches and a hold lies prone.</summary>
        public string crouchKey = "Jets Down";
        public string interactKey = "Interact";
        /// <summary>The Focus key or button (held).</summary>
        public string focusKey = "Focus";

        public static UnlockHints Default => new UnlockHints();
    }

    /// <summary>
    /// A starting kit: two perks of one family held from the first second of a run, in place of the perk picks of
    /// the first antes, one more than it gives (<see cref="ExtraPicks"/>, <see cref="AnteDirector.PicksGivenAhead"/>).
    /// A sidegrade, as Part B section 4 asks of every unlock (design review, 27 Nov 2026): the run is stronger early,
    /// with a direction, and one perk weaker for the rest of it, and the perks are chosen for you rather than from
    /// offers. Standard is no kit: every pick is a choice.
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

        /// <summary>A kit's price: the antes with no pick of their own are this many more than the perks it gives.</summary>
        public const int ExtraPicks = 1;

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

        /// <summary>The antes a kit run has no pick in: its perks, plus the price.</summary>
        public static int PicksTaken(int perksGiven) => perksGiven > 0 ? perksGiven + ExtraPicks : 0;

        /// <summary>The first ante a kit run picks a perk in (ante 4 for a kit of two).</summary>
        public static int FirstPickAnte(StartingKit kit) => PicksTaken(kit != null ? kit.perkIds.Length : 0) + 1;

        /// <summary>"Two perks now, one fewer this run: your first perk pick is ante 4's." Empty for Standard.</summary>
        public static string Cost(StartingKit kit)
        {
            if (kit == null || kit.perkIds.Length == 0) return string.Empty;
            return CountWord(kit.perkIds.Length) + (kit.perkIds.Length == 1 ? " perk" : " perks") + " now, one fewer this run: your first perk pick is ante "
                   + FirstPickAnte(kit).ToString(CultureInfo.InvariantCulture) + "'s.";
        }

        /// <summary>"Start with Quick Study and Steady Hands." or, for Standard, "No perks to start: every pick is yours to choose."</summary>
        public static string Summary(StartingKit kit, IReadOnlyList<PerkDefinition> catalog)
        {
            if (kit == null || kit.perkIds.Length == 0) return "No perks to start: every pick is yours to choose.";
            var names = new List<string>();
            for (int i = 0; i < kit.perkIds.Length; i++) names.Add(PerkName(kit.perkIds[i], catalog));
            return "Start with " + JoinAnd(names) + ".";
        }

        /// <summary>One line per perk, "Quick Study: ...", then what it does for its set, then the cost. Empty for Standard.</summary>
        public static string Details(StartingKit kit, IReadOnlyList<PerkDefinition> catalog, AnteBalance balance)
        {
            if (kit == null || kit.perkIds.Length == 0) return string.Empty;
            balance = balance ?? new AnteBalance();
            var lines = new List<string>();
            for (int i = 0; i < kit.perkIds.Length; i++)
            {
                PerkDefinition p = FindPerk(kit.perkIds[i], catalog);
                lines.Add(p == null ? kit.perkIds[i] : p.displayName + ": " + p.Describe(balance));
            }
            string set = SetWords(kit, balance);
            if (set.Length > 0) lines.Add(set);
            lines.Add(Cost(kit));
            return string.Join("\n", lines);
        }

        /// <summary>"Gunner 2 of 3: one more Gunner perk turns on its set (...)."</summary>
        public static string SetWords(StartingKit kit, AnteBalance balance)
        {
            if (kit == null || kit.family == PerkFamily.None || kit.perkIds.Length == 0) return string.Empty;
            balance = balance ?? new AnteBalance();
            int size = PerkFamilies.SetSize(balance), have = kit.perkIds.Length, need = size - have;
            string family = kit.family.ToString(), bonus = PerkFamilies.Describe(kit.family, balance);
            if (need <= 0) return family + " set on from the start: " + bonus + ".";
            return family + " " + have + " of " + size + ": " + (need == 1 ? "one more " + family + " perk turns" : need + " more " + family + " perks turn")
                   + " on its set (" + bonus + ").";
        }

        /// <summary>
        /// What the break and the field page say on the perk row in the antes a kit run does not pick:
        /// "this ante's came with your Gunner kit", then "none this ante, the price of your Gunner kit".
        /// </summary>
        public static string NoPickWords(StartingKit kit, int ante)
        {
            string name = kit != null ? kit.displayName : "kit";
            int given = kit != null ? kit.perkIds.Length : 0;
            return ante <= given ? "this ante's came with your " + name : "none this ante, the price of your " + name;
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

        static string CountWord(int n)
        {
            switch (n)
            {
                case 1: return "One";
                case 2: return "Two";
                case 3: return "Three";
                default: return n.ToString(CultureInfo.InvariantCulture);
            }
        }

        internal static string JoinAnd(List<string> parts)
        {
            if (parts.Count == 0) return string.Empty;
            if (parts.Count == 1) return parts[0];
            return string.Join(", ", parts.GetRange(0, parts.Count - 1)) + " and " + parts[parts.Count - 1];
        }
    }

    /// <summary>
    /// The eighteen unlocks, easiest goal first: six perks start locked, four kits wait to be earned, and eight looks
    /// (Ralph loop pass 5) are earned by goals instead of envelopes. Envelopes open only after ante 9 (Matthew's rule,
    /// 16 Nov 2026), while a first-time player is meant to die around ante 3 or 4, so without these a new player's
    /// collection stayed empty for many runs. The first three looks come from what a first run does anyway (a disarm,
    /// a leg shot off, a landing away from Earth), each naming one of the game's verbs, and they show from the next run
    /// (on the lander at the pad, in the hand, on the suit plate).
    /// </summary>
    public static class UnlockCatalog
    {
        static readonly List<PerkDefinition> Perks = RoguelitePerks.Create();

        static readonly UnlockDefinition[] _all =
        {
            // A first fight does these by itself; each one says what the game wants you to know: shoot the gun or the
            // arm to disarm, a leg to drop them.
            Look("SkinButterfingers", UnlockGoal.Disarm),
            Look("BadgeLegDay", UnlockGoal.LegOff),
            Look("PaintRollPattern", UnlockGoal.LandAwayFromEarth),
            Kit("kit.gunner", UnlockGoal.ClearAnte2),
            Perk("perk.patient_hunter", UnlockGoal.LowStanceKill),
            // Teaches the Focus key, which nothing else asks for.
            Look("VisorTunnelVision", UnlockGoal.FocusKill),
            Perk("perk.road_rage", UnlockGoal.RoverSplat),
            Look("HelmetHardHat", UnlockGoal.SalvageHome),
            Look("PackAfterburner", UnlockGoal.AirborneKill),
            Kit("kit.ghost", UnlockGoal.LongShotKill),
            Look("SuitBountyHunter", UnlockGoal.BonusMission),
            Kit("kit.pilot", UnlockGoal.ThreeWorldsInOneAnte),
            Perk(PerkCatalog.ChainPerkId, UnlockGoal.FourWaysInOneAnte),
            Look("SkinPistolSharkskin", UnlockGoal.SeaKill),
            // Risk for reward: the Daredevil family's own way to play (design review, 27 Nov 2026).
            Kit("kit.daredevil", UnlockGoal.LowArmourKill),
            Perk("perk.trigger_discipline", UnlockGoal.LadderTop),
            // Only worth holding with a melee weapon in hand, so it waits until the player has used one.
            Perk("perk.heavy_swing", UnlockGoal.MeleeKill),
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

        static UnlockDefinition Look(string itemId, UnlockGoal goal)
        {
            ItemSpec item = ItemCatalog.Default(itemId);
            return new UnlockDefinition { id = "unlock." + itemId, kind = UnlockKind.Look, target = itemId, goal = goal, name = item != null ? item.displayName : itemId };
        }

        public static IReadOnlyList<UnlockDefinition> All => _all;

        /// <summary>True for an unlock that gives a look (the Unlocks page lists them on their own).</summary>
        public static bool IsLook(UnlockDefinition d) => d != null && d.kind == UnlockKind.Look;

        /// <summary>The item a look unlock gives (the built-in one), or null for anything else.</summary>
        public static ItemSpec LookItem(UnlockDefinition d) => IsLook(d) ? ItemCatalog.Default(d.target) : null;

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
        /// The goal in full, with how to do it, said the same way wherever there is room (the Unlocks page): the
        /// game's own distances and the player's own keys come in through <paramref name="hints"/>.
        /// </summary>
        public static string GoalWords(UnlockGoal goal, UnlockHints hints = null)
        {
            hints = hints ?? UnlockHints.Default;
            switch (goal)
            {
                case UnlockGoal.ClearAnte2: return "Clear ante 2.";
                case UnlockGoal.LowStanceKill:
                    return "Kill an enemy while crouched or lying prone (tap " + hints.crouchKey + " to crouch, hold it to lie prone).";
                case UnlockGoal.RoverSplat:
                    return "Splat an enemy with the rover (it hangs at the back of the lander: once landed, press " + hints.interactKey + " under it).";
                case UnlockGoal.LongShotKill: return "Kill an enemy from " + Metres(hints.longShotMetres) + " or more (a LONG SHOT).";
                case UnlockGoal.ThreeWorldsInOneAnte: return "Land on " + UnlockTracker.WorldsInOneAnte + " different worlds in one ante (moons count).";
                case UnlockGoal.FourWaysInOneAnte:
                    return "Score " + UnlockTracker.WaysInOneAnte + " of these " + ScoreWays.Paid.Length + " ways in one ante: " + ScoreWays.List() + ".";
                case UnlockGoal.LowArmourKill:
                    return "Kill an enemy while your armour is under " + Percent(hints.lowArmourLine) + " (" + Chevrons(hints.lowArmourLine) + " chevrons or fewer).";
                case UnlockGoal.LadderTop: return "Climb the gun ladder to its last gun.";
                case UnlockGoal.MeleeKill: return "Kill an enemy with a melee weapon (some bases have one).";
                case UnlockGoal.HazardBeatsTheClock: return "Raise the stakes at a break, then meet that ante's target before its clock runs out.";
                case UnlockGoal.Disarm: return "Disarm an enemy: shoot the gun out of its hands, or the hand or arm that holds it.";
                case UnlockGoal.LegOff: return "Shoot a leg off an enemy that is standing (a thigh, shin or foot): it drops and has to crawl.";
                case UnlockGoal.LandAwayFromEarth: return "Land on any world but Earth (the Moon is the nearest).";
                case UnlockGoal.SalvageHome:
                    return "Carry a salvage crate back aboard your lander (they lie near bases: press " + hints.interactKey + " at one to pick it up).";
                case UnlockGoal.FocusKill: return "Kill an enemy while Focus slows the world (hold " + hints.focusKey + "; kills and limbs fill it again).";
                case UnlockGoal.AirborneKill: return "Kill an enemy while you are off the ground (an AIRBORNE kill: jump, or fire your jets).";
                case UnlockGoal.BonusMission: return "Finish a bonus mission (pick one at a break, then do it in that ante).";
                case UnlockGoal.SeaKill: return "Kill a sea creature while you are in the water with it (from the shore or the air it does not count).";
                default: return goal.ToString();
            }
        }

        /// <summary>The goal in a few words, for the recap, the title screen and the kit page: "a crouched or prone kill".</summary>
        public static string ShortGoal(UnlockGoal goal, UnlockHints hints = null)
        {
            hints = hints ?? UnlockHints.Default;
            switch (goal)
            {
                case UnlockGoal.ClearAnte2: return "clearing ante 2";
                case UnlockGoal.LowStanceKill: return "a crouched or prone kill";
                case UnlockGoal.RoverSplat: return "a rover splat";
                case UnlockGoal.LongShotKill: return "a LONG SHOT kill";
                case UnlockGoal.ThreeWorldsInOneAnte: return "landing on " + UnlockTracker.WorldsInOneAnte + " worlds in one ante";
                case UnlockGoal.FourWaysInOneAnte: return "scoring " + UnlockTracker.WaysInOneAnte + " ways in one ante";
                case UnlockGoal.LowArmourKill: return "a kill under " + Percent(hints.lowArmourLine) + " armour";
                case UnlockGoal.LadderTop: return "the gun ladder's last gun";
                case UnlockGoal.MeleeKill: return "a melee kill";
                case UnlockGoal.HazardBeatsTheClock: return "beating the clock with the stakes raised";
                case UnlockGoal.Disarm: return "a disarm";
                case UnlockGoal.LegOff: return "shooting a leg off";
                case UnlockGoal.LandAwayFromEarth: return "a landing away from Earth";
                case UnlockGoal.SalvageHome: return "a salvage crate brought home";
                case UnlockGoal.FocusKill: return "a kill in Focus";
                case UnlockGoal.AirborneKill: return "an AIRBORNE kill";
                case UnlockGoal.BonusMission: return "a bonus mission";
                case UnlockGoal.SeaKill: return "a kill in the sea";
                default: return goal.ToString();
            }
        }

        /// <summary>What the player just did, in capitals, as the unlock moment says it: "CROUCHED OR PRONE KILL".</summary>
        public static string FeatWords(UnlockGoal goal, UnlockHints hints = null)
        {
            hints = hints ?? UnlockHints.Default;
            switch (goal)
            {
                case UnlockGoal.ClearAnte2: return "ANTE 2 CLEARED";
                case UnlockGoal.LowStanceKill: return "CROUCHED OR PRONE KILL";
                case UnlockGoal.RoverSplat: return "ROVER SPLAT";
                case UnlockGoal.LongShotKill: return "LONG SHOT";
                case UnlockGoal.ThreeWorldsInOneAnte: return UnlockTracker.WorldsInOneAnte + " WORLDS IN ONE ANTE";
                case UnlockGoal.FourWaysInOneAnte: return UnlockTracker.WaysInOneAnte + " WAYS TO SCORE IN ONE ANTE";
                case UnlockGoal.LowArmourKill: return "KILL UNDER " + Percent(hints.lowArmourLine) + " ARMOUR";
                case UnlockGoal.LadderTop: return "LAST GUN ON THE LADDER";
                case UnlockGoal.MeleeKill: return "MELEE KILL";
                case UnlockGoal.HazardBeatsTheClock: return "STAKES RAISED, CLOCK BEATEN";
                case UnlockGoal.Disarm: return "DISARMED";
                case UnlockGoal.LegOff: return "LEG OFF";
                case UnlockGoal.LandAwayFromEarth: return "LANDED AWAY FROM EARTH";
                case UnlockGoal.SalvageHome: return "SALVAGE BROUGHT HOME";
                case UnlockGoal.FocusKill: return "FOCUS KILL";
                case UnlockGoal.AirborneKill: return "AIRBORNE KILL";
                case UnlockGoal.BonusMission: return "BONUS MISSION DONE";
                case UnlockGoal.SeaKill: return "SEA KILL";
                default: return goal.ToString().ToUpperInvariant();
            }
        }

        /// <summary>"a Pilot perk", "a starting kit", "a ship paint", "a gun skin": what kind of thing an unlock is.</summary>
        public static string KindWords(UnlockDefinition d)
        {
            if (d == null) return string.Empty;
            if (d.kind == UnlockKind.Kit) return "a starting kit";
            if (d.kind == UnlockKind.Look) return LookKindWords(LookItem(d));
            PerkDefinition p = StartingKits.FindPerk(d.target, Perks);
            return p == null || p.family == PerkFamily.None ? "a wild-card perk" : "a " + p.family + " perk";
        }

        static string LookKindWords(ItemSpec item)
        {
            if (item == null) return "a look";
            switch (item.category)
            {
                case ItemCategory.ShipPaint: return "a ship paint";
                case ItemCategory.WeaponSkin:
                    return string.IsNullOrEmpty(item.weaponId) ? "a gun skin" : "a " + item.weaponId.ToLowerInvariant() + " skin";
                case ItemCategory.Cosmetic:
                    string slot = ItemLook.SlotName(item.slot);
                    return slot.Length > 0 ? "a " + slot : "a look";
                default: return "a look";
            }
        }

        /// <summary>
        /// What it gives once earned: a perk's effect in words, a kit's two perks and its price, or what a look does and
        /// whether it is in use (<paramref name="lookInUse"/>, from <see cref="UnlockBook.LookInUse"/>).
        /// </summary>
        public static string GiftWords(UnlockDefinition d, AnteBalance balance = null, bool lookInUse = true)
        {
            if (d == null) return string.Empty;
            if (d.kind == UnlockKind.Kit)
            {
                StartingKit kit = StartingKits.Find(d.target);
                return StartingKits.Summary(kit, Perks) + " " + StartingKits.Cost(kit);
            }
            if (d.kind == UnlockKind.Look)
            {
                ItemSpec item = LookItem(d);
                if (item == null) return string.Empty;
                return Locker.DoesWords(item) + (lookInUse ? " In use." : " In your Collection: put it to use there.");
            }
            PerkDefinition p = StartingKits.FindPerk(d.target, Perks);
            return p == null ? string.Empty : Capital(p.Describe(balance ?? new AnteBalance())) + ".";
        }

        /// <summary>
        /// Where it now turns up: "in your drafts from ante 3", "in your next run's drafts" (when this run has no draft
        /// left, <paramref name="firstDraftAnte"/> 0 or less), "choose it at New run", or for a look "on your lander from
        /// your next run" (it went into use by itself, <paramref name="lookInUse"/>) or "in your Collection" (its place was
        /// taken, so it waits there to be swapped in).
        /// </summary>
        public static string WhereWords(UnlockDefinition d, int firstDraftAnte, bool lookInUse = true)
        {
            if (d == null) return string.Empty;
            if (d.kind == UnlockKind.Kit) return "choose it at New run";
            if (d.kind == UnlockKind.Look)
            {
                ItemSpec item = LookItem(d);
                string where = item != null ? Locker.WhereWords(item) : string.Empty;
                // A run's lander, suit and guns are built as it starts, so a look earned in one shows from the next.
                return lookInUse && where.Length > 0 ? where + " from your next run" : "in your Collection";
            }
            return firstDraftAnte <= 0 ? "in your next run's drafts"
                : "in your drafts from ante " + firstDraftAnte.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>"Patient Hunter, for a crouched or prone kill": an unlock still to earn, in a few words.</summary>
        public static string NextWords(UnlockDefinition d, UnlockHints hints = null)
        {
            return d == null ? string.Empty : d.name + ", for " + ShortGoal(d.goal, hints);
        }

        /// <summary>The unlock moment's banner: "UNLOCKED: PATIENT HUNTER".</summary>
        public static string BannerWords(UnlockDefinition d) => d == null ? string.Empty : "UNLOCKED: " + d.name.ToUpperInvariant();

        /// <summary>
        /// The note under the banner, which says why and where: "CROUCHED OR PRONE KILL · A GHOST PERK, IN YOUR DRAFTS
        /// FROM ANTE 3", "DISARMED · A GUN SKIN, ON EVERY GUN FROM YOUR NEXT RUN".
        /// </summary>
        public static string EarnedNote(UnlockDefinition d, int firstDraftAnte, UnlockHints hints = null, bool lookInUse = true)
        {
            if (d == null) return string.Empty;
            return (FeatWords(d.goal, hints) + " · " + KindWords(d) + ", " + WhereWords(d, firstDraftAnte, lookInUse)).ToUpperInvariant();
        }

        /// <summary>The line on the break, victory or recap page: "UNLOCKED: Patient Hunter, for a crouched or prone kill. A Ghost perk, in your drafts from ante 3."</summary>
        public static string BreakLine(UnlockDefinition d, int firstDraftAnte, UnlockHints hints = null, bool lookInUse = true)
        {
            if (d == null) return string.Empty;
            return "UNLOCKED: " + d.name + ", for " + ShortGoal(d.goal, hints) + ". " + Capital(KindWords(d)) + ", " + WhereWords(d, firstDraftAnte, lookInUse) + ".";
        }

        static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        static string Metres(float metres) => Math.Round(Math.Max(1f, metres)).ToString(CultureInfo.InvariantCulture) + " m";

        static string Percent(float share) => Math.Round(Math.Max(0f, Math.Min(1f, share)) * 100.0).ToString(CultureInfo.InvariantCulture) + "%";

        /// <summary>Lit armour chevrons just under the line: the suit shows ten, and one goes dark only when used up.</summary>
        static int Chevrons(float share) => Math.Max(1, (int)Math.Ceiling(Math.Max(0f, Math.Min(1f, share)) * 10.0 - 1e-4));
    }

    /// <summary>
    /// When the unlock moment may take the middle of the screen (design review, 27 Nov 2026). A goal met in a fight is
    /// usually a kill, and a banner then is missed or in the way, so it waits for the fight to end, but never longer
    /// than <see cref="FightWaitMost"/> unpaused seconds; it never covers an important note in its hold or another
    /// banner, and only shows while the readout does. Plain C#.
    /// </summary>
    public static class UnlockMoment
    {
        public const float FightWaitMost = 20f;

        /// <param name="readoutShown">The readout is on screen (not paused, not looking at the dash or the map).</param>
        /// <param name="noteHolding">An important note (TARGET MET) is still in its hold.</param>
        /// <param name="bannerUp">A banner already holds the place: the gun ladder's, or the last unlock's.</param>
        /// <param name="inFight">An enemy has had the player in sight lately.</param>
        /// <param name="waited">Unpaused seconds this unlock has waited.</param>
        public static bool MayShow(bool readoutShown, bool noteHolding, bool bannerUp, bool inFight, float waited)
        {
            if (!readoutShown || noteHolding || bannerUp) return false;
            return !inFight || waited >= FightWaitMost;
        }
    }

    /// <summary>
    /// The unlock lines at the end of the run recap (design review, 27 Nov 2026): what this run unlocked, the next two
    /// to earn in a few words each, then the recap's own next mark, which names the Gunner kit when that mark is
    /// clearing ante 2. Plain C#, so the page a run ends on can be read in a test.
    /// </summary>
    public static class UnlockRecap
    {
        /// <summary>How many unlocks still to earn the recap names.</summary>
        public const int NextShown = 2;

        public static string Lines(SaveData save, IList<UnlockDefinition> unlockedThisRun, int cleared, int lastAnte, UnlockHints hints = null)
        {
            var s = new StringBuilder();
            if (unlockedThisRun != null && unlockedThisRun.Count > 0)
            {
                var names = new List<string>();
                for (int i = 0; i < unlockedThisRun.Count; i++) if (unlockedThisRun[i] != null) names.Add(unlockedThisRun[i].name);
                s.Append("Unlocked this run: ").Append(string.Join(", ", names)).Append(". See Unlocks on the title screen.\n");
            }
            string mark = RunRecap.NextLine(cleared, lastAnte);
            UnlockDefinition gunner = UnlockCatalog.ForGoal(UnlockGoal.ClearAnte2);
            // The next mark is clearing ante 2, which is a goal: say so once, there, and not again in the list.
            bool fold = Math.Max(0, cleared) == 1 && gunner != null && !UnlockBook.Has(save, gunner.id);
            List<UnlockDefinition> next = UnlockBook.Next(save, NextShown, fold ? UnlockGoal.ClearAnte2 : (UnlockGoal?)null);
            if (next.Count > 0)
            {
                var parts = new List<string>();
                for (int i = 0; i < next.Count; i++) parts.Add(UnlockCatalog.NextWords(next[i], hints));
                s.Append(next.Count == 1 ? "Next unlock: " : "Next unlocks: ").Append(string.Join(" · ", parts)).Append('\n');
            }
            if (fold) mark = mark.TrimEnd('.') + ", which unlocks the " + gunner.name + ".";
            s.Append(mark);
            return s.ToString();
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
        /// <summary>Where every run starts (the launch pad): landing anywhere else is <see cref="UnlockGoal.LandAwayFromEarth"/>.</summary>
        public const string HomeWorld = Launch.LaunchSite.BodyId;

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
            if (worldId != HomeWorld) Met(UnlockGoal.LandAwayFromEarth);
            if (_worlds.Add(worldId) && _worlds.Count >= WorldsInOneAnte) Met(UnlockGoal.ThreeWorldsInOneAnte);
        }

        /// <summary>
        /// The player killed an enemy or a creature in a running ante. <paramref name="airborne"/>: the AIRBORNE feat
        /// (off the ground, not in the sea). <paramref name="inFocus"/>: Focus had the world at least half slowed.
        /// <paramref name="seaCreature"/>: a swimmer, killed from in the water with it (a kill from out of its reach pays
        /// nothing and is never reported).
        /// </summary>
        public void Kill(bool melee, bool rover, bool longShot, bool lowStance, bool lowArmour = false,
            bool airborne = false, bool inFocus = false, bool seaCreature = false)
        {
            if (melee) Met(UnlockGoal.MeleeKill);
            if (rover) Met(UnlockGoal.RoverSplat);
            if (longShot) Met(UnlockGoal.LongShotKill);
            // From the rover's seat nobody is crouching, and its jumps are the rover's, not the player's.
            if (lowStance && !rover) Met(UnlockGoal.LowStanceKill);
            if (lowArmour) Met(UnlockGoal.LowArmourKill);
            if (airborne && !rover) Met(UnlockGoal.AirborneKill);
            if (inFocus) Met(UnlockGoal.FocusKill);
            if (seaCreature) Met(UnlockGoal.SeaKill);
        }

        /// <summary>The player knocked a gun out of a living enemy's hands (shot it out, or shot off the hand or arm holding it).</summary>
        public void Disarmed() => Met(UnlockGoal.Disarm);

        /// <summary>The player's hit took a leg off a living enemy that was standing on two.</summary>
        public void LegOff() => Met(UnlockGoal.LegOff);

        /// <summary>A salvage crate came aboard the lander.</summary>
        public void SalvageHome() => Met(UnlockGoal.SalvageHome);

        /// <summary>The ante's bonus mission was finished (and paid).</summary>
        public void BonusMissionDone() => Met(UnlockGoal.BonusMission);

        /// <summary>
        /// An ante was cleared: banked by choice, or its clock ran out with the target met, or the target was met in
        /// overtime. With raised stakes it counts only when the target was met before the clock ran out.
        /// </summary>
        public void AnteCleared(int ante, bool hazardOn, bool inOvertime)
        {
            if (ante >= 2) Met(UnlockGoal.ClearAnte2);
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

        /// <summary>
        /// Records what a goal unlocks. Returns it when it is new, or null when it was earned before. A perk is also
        /// owed a place in a draft (<see cref="PerkToShow"/>), so the player meets it soon after earning it. A look goes
        /// into the collection as a line of its own (no envelope number) and into use where its place is empty
        /// (<see cref="Locker.AutoUse"/>, the same rule as a won prize), so it shows from the next run with nothing to press.
        /// </summary>
        public static UnlockDefinition Grant(SaveData save, UnlockGoal goal)
        {
            if (save == null) return null;
            UnlockDefinition d = UnlockCatalog.ForGoal(goal);
            if (d == null || Has(save, d.id)) return null;
            Earned(save).Add(d.id);
            if (d.kind == UnlockKind.Perk)
            {
                if (save.perksToShow == null) save.perksToShow = new List<string>();
                if (!save.perksToShow.Contains(d.target)) save.perksToShow.Add(d.target);
            }
            if (d.kind == UnlockKind.Look) KeepLook(save, d);
            return d;
        }

        /// <summary>Puts a look into the collection (once) and into use if its place is empty. True if it went into use.</summary>
        static bool KeepLook(SaveData save, UnlockDefinition d)
        {
            ItemSpec item = UnlockCatalog.LookItem(d);
            if (item == null) return false;
            if (save.items == null) save.items = new List<OwnedItem>();
            var owned = new ItemInventory(save.items);
            if (!owned.Has(item.id, "")) owned.Add(new Prize { itemId = item.id, effectId = "", rarity = item.rarity }, 0, false);
            return Locker.AutoUse(save, item, "");
        }

        /// <summary>
        /// Mends a save whose earned looks have gone missing from the collection (a test clear, an item dropped and
        /// then restored): every earned look is owned again. Never puts one into use. Returns how many came back.
        /// </summary>
        public static int KeepEarnedLooks(SaveData save)
        {
            if (save == null || save.unlocks == null) return 0;
            int back = 0;
            IReadOnlyList<UnlockDefinition> all = UnlockCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (!UnlockCatalog.IsLook(all[i]) || !Has(save, all[i].id)) continue;
                ItemSpec item = UnlockCatalog.LookItem(all[i]);
                if (item == null) continue;
                if (save.items == null) save.items = new List<OwnedItem>();
                var owned = new ItemInventory(save.items);
                if (owned.Has(item.id, "")) continue;
                owned.Add(new Prize { itemId = item.id, effectId = "", rarity = item.rarity }, 0, false);
                back++;
            }
            return back;
        }

        /// <summary>Whether an earned look is in use now (false for anything that is not a look).</summary>
        public static bool LookInUse(SaveData save, UnlockDefinition d)
        {
            ItemSpec item = UnlockCatalog.LookItem(d);
            return save != null && item != null && Locker.InUse(save, item, "");
        }

        /// <summary>
        /// The perk the next draft shows in its last place, marked NEW: the first unlocked and not shown in a draft
        /// yet (design review, 27 Nov 2026: drafts roll rarity first, so a rare might otherwise take runs to appear).
        /// Null when none is owed.
        /// </summary>
        public static string PerkToShow(SaveData save)
        {
            if (save == null || save.perksToShow == null) return null;
            for (int i = 0; i < save.perksToShow.Count; i++)
            {
                string id = save.perksToShow[i];
                UnlockDefinition d = UnlockCatalog.ForTarget(id);
                if (d != null && d.kind == UnlockKind.Perk && Has(save, d.id)) return id;
            }
            return null;
        }

        /// <summary>A draft has shown this perk: it is owed no more. True when it was owed.</summary>
        public static bool MarkShown(SaveData save, string perkId)
        {
            return save != null && save.perksToShow != null && !string.IsNullOrEmpty(perkId) && save.perksToShow.Remove(perkId);
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
            List<UnlockDefinition> next = Next(save, 1, null);
            return next.Count > 0 ? next[0] : null;
        }

        /// <summary>
        /// Up to <paramref name="count"/> unlocks not earned yet, easiest first, leaving out <paramref name="leaveOut"/>
        /// (the recap folds "clear ante 2" into its own next mark).
        /// </summary>
        public static List<UnlockDefinition> Next(SaveData save, int count, UnlockGoal? leaveOut)
        {
            var next = new List<UnlockDefinition>();
            IReadOnlyList<UnlockDefinition> all = UnlockCatalog.All;
            for (int i = 0; i < all.Count && next.Count < count; i++)
                if (!Has(save, all[i].id) && (!leaveOut.HasValue || all[i].goal != leaveOut.Value)) next.Add(all[i]);
            return next;
        }

        /// <summary>The first look not earned yet, easiest first, or null when every look is earned.</summary>
        public static UnlockDefinition NextLook(SaveData save)
        {
            IReadOnlyList<UnlockDefinition> all = UnlockCatalog.All;
            for (int i = 0; i < all.Count; i++)
                if (UnlockCatalog.IsLook(all[i]) && !Has(save, all[i].id)) return all[i];
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

        /// <summary>New ones on one list of the Unlocks page: the looks, or the perks and kits.</summary>
        public static int NewCount(SaveData save, bool looks)
        {
            if (save == null || save.unlocks == null) return 0;
            int n = 0;
            for (int i = 0; i < save.unlocks.Count; i++)
                if (IsNew(save, save.unlocks[i]) && UnlockCatalog.IsLook(UnlockCatalog.Find(save.unlocks[i])) == looks) n++;
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

        /// <summary>One list of the Unlocks page has been shown (the looks, or the perks and kits): its new ones are seen.</summary>
        public static void MarkSeen(SaveData save, bool looks)
        {
            if (save == null || save.unlocks == null) return;
            if (save.unlocksSeen == null) save.unlocksSeen = new List<string>();
            for (int i = 0; i < save.unlocks.Count; i++)
            {
                string id = save.unlocks[i];
                if (UnlockCatalog.IsLook(UnlockCatalog.Find(id)) == looks && !save.unlocksSeen.Contains(id)) save.unlocksSeen.Add(id);
            }
        }
    }
}
