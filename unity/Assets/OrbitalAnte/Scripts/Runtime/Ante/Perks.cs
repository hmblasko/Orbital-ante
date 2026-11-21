using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Ante
{
    /// <summary>What a perk can change. Saved by name; add new ones at the end.</summary>
    public enum PerkStat
    {
        /// <summary>More score for one kind of event (optionally only events carrying a tag, such as "wreck").</summary>
        ScoreBonus = 0,
        /// <summary>More score for the next event of a different category inside a short window, a few times per ante.</summary>
        ChainScoreBonus = 1,
        ShipFuelPerImpulse = 2,
        WeaponDamage = 3,
        MaxHp = 4,
        EvaLateralThrust = 5,
        // ---- Added 20 Nov 2026 (roguelite fun pass). Fractions unless the line says otherwise. ----
        /// <summary>Damage the player takes, as a fraction (-0.10 is 10% less).</summary>
        IncomingDamage = 6,
        /// <summary>Health given back by any kill (absolute points of health, not a fraction).</summary>
        HealOnKill = 7,
        /// <summary>Health given back by a melee kill (absolute).</summary>
        HealOnMeleeKill = 8,
        MeleeDamage = 9,
        /// <summary>Extra Focus from kills and limbs, as a fraction of what they give.</summary>
        FocusGain = 10,
        /// <summary>Every Nth kill counts twice on the gun ladder (absolute N; the smallest held wins).</summary>
        LadderDoubleEvery = 11,
        /// <summary>Extra damage while the player's health is under the low-health line.</summary>
        LowHealthDamage = 12,
        /// <summary>Damage taken from shooters within the close range, as a fraction.</summary>
        CloseRangeDefence = 13,
        /// <summary>Metres taken off the LONG SHOT distance (absolute).</summary>
        LongShotCloser = 14,
        /// <summary>Health given back by a rover splat (absolute).</summary>
        HealOnSplat = 15,
        /// <summary>Seconds put back on the ante clock by a landing that scores (absolute).</summary>
        LandingTimeBack = 16,
        /// <summary>How fast health comes back after a quiet spell, as a fraction.</summary>
        RegenRate = 17,
        /// <summary>Walking and running speed on foot, as a fraction.</summary>
        MoveSpeed = 18,
        /// <summary>Rounds put back in the magazine by a kill (absolute).</summary>
        RoundsOnKill = 19,
        /// <summary>Gun damage only (not melee or the rover).</summary>
        GunDamage = 20,
        /// <summary>Gun damage while crouched or prone.</summary>
        LowStanceDamage = 21,
        /// <summary>Share of a full Focus meter given by a melee kill (absolute, 0.25 is a quarter).</summary>
        FocusOnMeleeKill = 22,
        /// <summary>Seconds put back on the ante clock by a LONG SHOT kill (absolute).</summary>
        TimeOnLongShot = 23,
        /// <summary>Health given back by a kill made under the low-health line (absolute).</summary>
        HealOnLowHealthKill = 24,
    }

    /// <summary>
    /// The five families a perk can belong to (20 Nov 2026). Holding <see cref="PerkFamilies.SetSize"/> of one
    /// family turns on that family's bonus. Saved by name; add new ones at the end.
    /// </summary>
    public enum PerkFamily
    {
        None = 0,
        Gunner = 1,
        Brawler = 2,
        Ghost = 3,
        Pilot = 4,
        Daredevil = 5,
    }

    [Serializable]
    public sealed class PerkEffect
    {
        public PerkStat stat;
        /// <summary>A fraction: 0.15 is +15%, -0.12 is -12%.</summary>
        public float value;
        /// <summary>For ScoreBonus: which kind of event it applies to.</summary>
        public ScoreEventKind kind;
        /// <summary>For ScoreBonus: if set, only events with this tag.</summary>
        public string tag;

        public PerkEffect() { }

        public PerkEffect(PerkStat stat, float value)
        {
            this.stat = stat;
            this.value = value;
        }

        public PerkEffect(ScoreEventKind kind, string tag, float value)
        {
            stat = PerkStat.ScoreBonus;
            this.kind = kind;
            this.tag = tag;
            this.value = value;
        }
    }

    /// <summary>One perk, as plain data. The text shown to the player is written from the effects, so it cannot disagree with them.</summary>
    [Serializable]
    public sealed class PerkDefinition
    {
        /// <summary>Stable id used by saves.</summary>
        public string id;
        public string displayName;
        public PerkRarity rarity;
        /// <summary>Synergy tags shown on the card ("Explorer", "Pilot", "Gunner"...).</summary>
        public string[] tags = new string[0];
        /// <summary>The family whose set bonus this perk counts toward (None for wild cards).</summary>
        public PerkFamily family;
        /// <summary>A unique perk can be held once and is never offered again.</summary>
        public bool unique = true;
        /// <summary>Something the run must still have for this perk to do anything ("tieup" = tie-up shots left). Empty = always useful.</summary>
        public string needs;
        public PerkEffect[] effects = new PerkEffect[0];

        /// <param name="balance">Where the limits quoted on the card come from. Null uses the default numbers.</param>
        public string Describe(AnteBalance balance = null)
        {
            if (balance == null) balance = new AnteBalance();
            var parts = new List<string>();
            for (int i = 0; i < effects.Length; i++) parts.Add(Describe(effects[i], balance));
            return string.Join(", ", parts);
        }

        /// <summary>One effect in words, for a card that shows gains and costs on separate lines (21 Nov 2026).</summary>
        public static string DescribeEffect(PerkEffect effect, AnteBalance balance = null) => Describe(effect, balance ?? new AnteBalance());

        /// <summary>True for an effect that makes the player worse off (shown as the card's cost).</summary>
        public static bool IsCost(PerkEffect effect)
        {
            if (effect == null) return false;
            switch (effect.stat)
            {
                case PerkStat.IncomingDamage:
                case PerkStat.CloseRangeDefence:
                    return effect.value > 0f;
                case PerkStat.LadderDoubleEvery:
                case PerkStat.LongShotCloser:
                    return false;
                default:
                    return effect.value < 0f;
            }
        }

        /// <summary>Plain words for what a score bonus pays on.</summary>
        static string PointsFor(ScoreEventKind kind, string tag)
        {
            if (tag == "base") return "clearing a base";
            if (tag == "kill") return "kills";
            switch (kind)
            {
                case ScoreEventKind.EnemyTier1: return "light enemies";
                case ScoreEventKind.EnemyTier2: return "mid enemies";
                case ScoreEventKind.EnemyTier3: return "heavy enemies";
                case ScoreEventKind.EnemyElite: return "marked targets";
                case ScoreEventKind.NewMoon: return "first landings on moons";
                case ScoreEventKind.NewPlanet: return "first landings on planets";
                case ScoreEventKind.DifficultLanding: return "hard landings";
                case ScoreEventKind.SalvageDelivered: return "salvage";
                case ScoreEventKind.InstallationDestroyed: return "signal masts";
                case ScoreEventKind.BountyAlive: return "bounties brought in alive";
                default: return string.IsNullOrEmpty(tag) ? kind.ToString() : tag + " " + kind;
            }
        }

        static int Whole(float v) => (int)Math.Round(Math.Abs(v), MidpointRounding.AwayFromZero);
        static string Pct(float v) => (int)Math.Round(v * 100.0, MidpointRounding.AwayFromZero) + "%";

        static string Ordinal(int n)
        {
            if (n % 100 >= 11 && n % 100 <= 13) return n + "th";
            switch (n % 10) { case 1: return n + "st"; case 2: return n + "nd"; case 3: return n + "rd"; default: return n + "th"; }
        }

        static string Describe(PerkEffect effect, AnteBalance balance)
        {
            int percent = (int)Math.Round(Math.Abs(effect.value) * 100.0, MidpointRounding.AwayFromZero);
            string amount = (effect.value >= 0f ? "+" : "-") + percent + "%";
            switch (effect.stat)
            {
                case PerkStat.ScoreBonus:
                    return amount + " points for " + PointsFor(effect.kind, effect.tag);
                case PerkStat.ChainScoreBonus:
                    return amount + " score when you score in a different category (combat, exploration, bounty, objective, salvage, repair, landing) within "
                           + ScoreMath.Exact(balance.chainWindowSeconds).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                           + " seconds of your last score; works " + balance.chainUsesPerAnte + " times per ante";
                case PerkStat.ShipFuelPerImpulse: return amount + " ship fuel per thrust";
                case PerkStat.WeaponDamage: return amount + " weapon damage";
                case PerkStat.MaxHp: return amount + " max HP";
                case PerkStat.EvaLateralThrust: return amount + " EVA sideways thrust";
                case PerkStat.IncomingDamage: return amount + " damage taken";
                case PerkStat.HealOnKill: return "kills heal " + Whole(effect.value);
                case PerkStat.HealOnMeleeKill: return "melee kills heal " + Whole(effect.value);
                case PerkStat.MeleeDamage: return amount + " melee damage";
                case PerkStat.FocusGain: return amount + " Focus from kills and limbs";
                case PerkStat.LadderDoubleEvery: return "every " + Ordinal(Whole(effect.value)) + " kill counts twice on the gun ladder";
                case PerkStat.LowHealthDamage: return amount + " damage while under " + Pct(balance.lowHealthLine) + " health";
                case PerkStat.CloseRangeDefence: return amount + " damage taken from shooters within " + Whole(balance.closeRange) + " m";
                case PerkStat.LongShotCloser: return "LONG SHOT counts from " + Whole(balance.longShotMetres - effect.value) + " m instead of " + Whole(balance.longShotMetres);
                case PerkStat.HealOnSplat: return "rover splats heal " + Whole(effect.value);
                case PerkStat.LandingTimeBack: return "landings that score put " + Whole(effect.value) + " s back on the clock";
                case PerkStat.RegenRate: return amount + " health regeneration";
                case PerkStat.MoveSpeed: return amount + " walking speed";
                case PerkStat.RoundsOnKill: return "kills put " + Whole(effect.value) + " rounds back in the magazine";
                case PerkStat.GunDamage: return amount + " gun damage";
                case PerkStat.LowStanceDamage: return amount + " gun damage while crouched or prone";
                case PerkStat.FocusOnMeleeKill: return "melee kills fill " + Pct(effect.value) + " of Focus";
                case PerkStat.TimeOnLongShot: return "LONG SHOT kills put " + Whole(effect.value) + " s back on the clock";
                case PerkStat.HealOnLowHealthKill: return "kills under " + Pct(balance.lowHealthLine) + " health heal " + Whole(effect.value);
                default: return amount + " " + effect.stat;
            }
        }
    }

    public static class PerkCatalog
    {
        public const string ChainPerkId = "perk.change_of_pace";

        /// <summary>
        /// How many unique perks a catalog needs for every draft of a full run to show a full set of choices:
        /// one is taken after each cleared ante except the last, and the final draft still needs a full hand.
        /// The six example perks are fewer than this, so late drafts in a full run come up short until the
        /// real list is written.
        /// </summary>
        public static int SizeNeededForFullDrafts(AnteBalance balance)
        {
            return Math.Max(0, balance.AnteCount - 2) + balance.perkChoices;
        }

        /// <summary>
        /// The six example perks from the point-system handoff, and only those. Names, tags and rarities are
        /// placeholders of mine; the effects are Matthew's. The real list is content for Milestone 14.
        /// </summary>
        public static List<PerkDefinition> CreateDefaults()
        {
            return new List<PerkDefinition>
            {
                new PerkDefinition
                {
                    id = "perk.wreck_hunter", displayName = "Wreck Hunter", rarity = PerkRarity.Common, tags = new[] { "Explorer" },
                    effects = new[] { new PerkEffect(ScoreEventKind.Landmark, "wreck", 0.15f) },
                },
                new PerkDefinition
                {
                    id = "perk.light_touch", displayName = "Light Touch", rarity = PerkRarity.Common, tags = new[] { "Pilot" },
                    effects = new[] { new PerkEffect(PerkStat.ShipFuelPerImpulse, -0.12f) },
                },
                new PerkDefinition
                {
                    id = "perk.glass_cannon", displayName = "Glass Cannon", rarity = PerkRarity.Uncommon, tags = new[] { "Gunner", "Risk" },
                    effects = new[] { new PerkEffect(PerkStat.WeaponDamage, 0.20f), new PerkEffect(PerkStat.MaxHp, -0.15f) },
                },
                new PerkDefinition
                {
                    id = "perk.gentle_hands", displayName = "Gentle Hands", rarity = PerkRarity.Uncommon, tags = new[] { "Hunter", "Diplomat" }, needs = "tieup",
                    effects = new[] { new PerkEffect(ScoreEventKind.BountyAlive, null, 0.20f) },
                },
                new PerkDefinition
                {
                    id = "perk.side_step", displayName = "Side Step", rarity = PerkRarity.Common, tags = new[] { "Spacewalker" },
                    effects = new[] { new PerkEffect(PerkStat.EvaLateralThrust, 0.15f) },
                },
                new PerkDefinition
                {
                    id = ChainPerkId, displayName = "Change of Pace", rarity = PerkRarity.Rare, tags = new[] { "Explorer", "Gunner", "Hunter" },
                    effects = new[] { new PerkEffect(PerkStat.ChainScoreBonus, 0.10f) },
                },
            };
        }
    }

    /// <summary>The perks the player holds this run, and what they add up to.</summary>
    public sealed class PerkLoadout
    {
        readonly List<PerkDefinition> _owned = new List<PerkDefinition>();

        public IReadOnlyList<PerkDefinition> Owned => _owned;

        public bool Has(string id)
        {
            for (int i = 0; i < _owned.Count; i++) if (_owned[i].id == id) return true;
            return false;
        }

        /// <returns>False if it is a unique perk the player already holds.</returns>
        public bool Add(PerkDefinition perk)
        {
            if (perk == null) return false;
            if (perk.unique && Has(perk.id)) return false;
            _owned.Add(perk);
            return true;
        }

        public void Clear() => _owned.Clear();

        /// <summary>The total change to a stat from all held perks, held inside the stacking cap in both directions.</summary>
        public float StatTotal(PerkStat stat, AnteBalance balance)
        {
            float total = 0f;
            for (int i = 0; i < _owned.Count; i++)
            {
                PerkEffect[] effects = _owned[i].effects;
                for (int e = 0; e < effects.Length; e++) if (effects[e].stat == stat) total += effects[e].value;
            }
            float cap = Math.Abs(balance.perkStatStackCap);
            return Math.Max(-cap, Math.Min(cap, total));
        }

        /// <summary>The plain total of a stat over every held perk, with no cap (for amounts such as health or seconds).</summary>
        public float Sum(PerkStat stat)
        {
            float total = 0f;
            for (int i = 0; i < _owned.Count; i++)
            {
                PerkEffect[] effects = _owned[i].effects;
                for (int e = 0; e < effects.Length; e++) if (effects[e].stat == stat) total += effects[e].value;
            }
            return total;
        }

        /// <summary>The smallest value of a stat among held perks that have it, or 0 if none has it.</summary>
        public float Smallest(PerkStat stat)
        {
            float best = 0f;
            for (int i = 0; i < _owned.Count; i++)
            {
                PerkEffect[] effects = _owned[i].effects;
                for (int e = 0; e < effects.Length; e++)
                    if (effects[e].stat == stat && effects[e].value > 0f && (best <= 0f || effects[e].value < best)) best = effects[e].value;
            }
            return best;
        }

        /// <summary>How many held perks belong to a family.</summary>
        public int FamilyCount(PerkFamily family)
        {
            if (family == PerkFamily.None) return 0;
            int n = 0;
            for (int i = 0; i < _owned.Count; i++) if (_owned[i].family == family) n++;
            return n;
        }

        /// <summary>Score bonus from held perks for one event, before the chain bonus and before the overall perk cap.</summary>
        public float ScoreBonusFor(ScoreEventKind kind, string tag)
        {
            float total = 0f;
            for (int i = 0; i < _owned.Count; i++)
            {
                PerkEffect[] effects = _owned[i].effects;
                for (int e = 0; e < effects.Length; e++)
                {
                    PerkEffect effect = effects[e];
                    if (effect.stat != PerkStat.ScoreBonus) continue;
                    if (effect.tag == "kill")
                    {
                        // Any enemy kill, whatever its tier (20 Nov 2026).
                        if (kind > ScoreEventKind.EnemyElite) continue;
                    }
                    else
                    {
                        if (effect.kind != kind) continue;
                        if (!string.IsNullOrEmpty(effect.tag) && effect.tag != tag) continue;
                    }
                    total += effect.value;
                }
            }
            return Math.Max(0f, total);
        }

        public float ChainBonus()
        {
            float total = 0f;
            for (int i = 0; i < _owned.Count; i++)
            {
                PerkEffect[] effects = _owned[i].effects;
                for (int e = 0; e < effects.Length; e++) if (effects[e].stat == PerkStat.ChainScoreBonus) total += effects[e].value;
            }
            return Math.Max(0f, total);
        }
    }

    /// <summary>
    /// The pick-1-of-3 draft. Each slot rolls a rarity (70 / 25 / 5 by default) and then a perk of that rarity.
    /// The three are always different, a unique perk already held is never offered, and at least one offer is
    /// something the player can actually use right now whenever such a perk exists.
    /// </summary>
    public static class PerkDraft
    {
        public const string StreamName = "ante.perkdraft";

        /// <summary>The generator for one ante's draft. Depends only on the run seed and the ante, so reloading cannot re-roll it.</summary>
        public static SeededRandom StreamFor(int runSeed, int ante) => SeededRandom.ForStream(runSeed, StreamName).Fork(ante);

        /// <param name="unavailable">Things the run no longer has (see <see cref="PerkDefinition.needs"/>). May be null.</param>
        public static List<PerkDefinition> Roll(SeededRandom rng, IReadOnlyList<PerkDefinition> catalog, PerkLoadout loadout,
            AnteBalance balance, ICollection<string> unavailable = null)
        {
            var offers = new List<PerkDefinition>();
            if (catalog == null) return offers;

            for (int slot = 0; slot < balance.perkChoices; slot++)
            {
                PerkRarity wanted = RollRarity(rng, balance);
                PerkDefinition pick = PickOfRarity(rng, catalog, loadout, offers, wanted, null, balance.heldFamilyWeight);
                if (pick == null) break;      // nothing left to offer
                offers.Add(pick);
            }

            if (offers.Count > 0 && !AnyUseful(offers, unavailable))
            {
                // Swap the last offer for a useful one, cheapest rarity first, if there is one.
                var withoutLast = new List<PerkDefinition>(offers);
                withoutLast.RemoveAt(withoutLast.Count - 1);
                PerkDefinition useful = PickOfRarity(rng, catalog, loadout, withoutLast, PerkRarity.Common, unavailable, balance.heldFamilyWeight);
                if (useful != null) offers[offers.Count - 1] = useful;
            }
            return offers;
        }

        public static bool IsUseful(PerkDefinition perk, ICollection<string> unavailable)
        {
            return string.IsNullOrEmpty(perk.needs) || unavailable == null || !unavailable.Contains(perk.needs);
        }

        public static PerkRarity RollRarity(SeededRandom rng, AnteBalance balance)
        {
            double common = Math.Max(0f, balance.rarityCommon);
            double uncommon = Math.Max(0f, balance.rarityUncommon);
            double rare = Math.Max(0f, balance.rarityRare);
            double roll = rng.NextDouble() * (common + uncommon + rare);
            if (roll < common) return PerkRarity.Common;
            return roll < common + uncommon ? PerkRarity.Uncommon : PerkRarity.Rare;
        }

        static bool AnyUseful(List<PerkDefinition> offers, ICollection<string> unavailable)
        {
            for (int i = 0; i < offers.Count; i++) if (IsUseful(offers[i], unavailable)) return true;
            return false;
        }

        // Rarities to try, nearest to the wanted one first.
        static readonly PerkRarity[][] Fallback =
        {
            new[] { PerkRarity.Common, PerkRarity.Uncommon, PerkRarity.Rare },
            new[] { PerkRarity.Uncommon, PerkRarity.Common, PerkRarity.Rare },
            new[] { PerkRarity.Rare, PerkRarity.Uncommon, PerkRarity.Common },
        };

        static PerkDefinition PickOfRarity(SeededRandom rng, IReadOnlyList<PerkDefinition> catalog, PerkLoadout loadout,
            List<PerkDefinition> alreadyOffered, PerkRarity wanted, ICollection<string> mustBeUsefulGiven, float heldFamilyWeight = 1f)
        {
            var candidates = new List<PerkDefinition>();
            PerkRarity[] order = Fallback[(int)wanted];
            for (int r = 0; r < order.Length; r++)
            {
                candidates.Clear();
                for (int i = 0; i < catalog.Count; i++)
                {
                    PerkDefinition perk = catalog[i];
                    if (perk == null || perk.rarity != order[r]) continue;
                    if (alreadyOffered.Contains(perk)) continue;
                    if (perk.unique && loadout != null && loadout.Has(perk.id)) continue;
                    if (mustBeUsefulGiven != null && !IsUseful(perk, mustBeUsefulGiven)) continue;
                    candidates.Add(perk);
                    // A family the player has started is more likely to come up, so a set is reachable in a short
                    // run (design review, 20 Nov 2026). Perks with no family are never weighted.
                    if (perk.family != PerkFamily.None && loadout != null && loadout.FamilyCount(perk.family) > 0)
                        for (int extra = 1; extra < Math.Max(1, (int)Math.Round(heldFamilyWeight)); extra++) candidates.Add(perk);
                }
                if (candidates.Count > 0) return rng.Pick(candidates);
            }
            return null;
        }
    }
}
