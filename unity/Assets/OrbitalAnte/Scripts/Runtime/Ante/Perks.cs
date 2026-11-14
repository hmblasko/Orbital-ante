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

        static string Describe(PerkEffect effect, AnteBalance balance)
        {
            int percent = (int)Math.Round(Math.Abs(effect.value) * 100.0, MidpointRounding.AwayFromZero);
            string amount = (effect.value >= 0f ? "+" : "-") + percent + "%";
            switch (effect.stat)
            {
                case PerkStat.ScoreBonus:
                    return amount + " score: " + (string.IsNullOrEmpty(effect.tag) ? effect.kind.ToString() : effect.tag + " " + effect.kind);
                case PerkStat.ChainScoreBonus:
                    return amount + " score when you score in a different category (combat, exploration, bounty, objective, salvage, repair, landing) within "
                           + ScoreMath.Exact(balance.chainWindowSeconds).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                           + " seconds of your last score; works " + balance.chainUsesPerAnte + " times per ante";
                case PerkStat.ShipFuelPerImpulse: return amount + " ship fuel per thrust";
                case PerkStat.WeaponDamage: return amount + " weapon damage";
                case PerkStat.MaxHp: return amount + " max HP";
                case PerkStat.EvaLateralThrust: return amount + " EVA sideways thrust";
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
                    if (effect.stat != PerkStat.ScoreBonus || effect.kind != kind) continue;
                    if (!string.IsNullOrEmpty(effect.tag) && effect.tag != tag) continue;
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
                PerkDefinition pick = PickOfRarity(rng, catalog, loadout, offers, wanted, null);
                if (pick == null) break;      // nothing left to offer
                offers.Add(pick);
            }

            if (offers.Count > 0 && !AnyUseful(offers, unavailable))
            {
                // Swap the last offer for a useful one, cheapest rarity first, if there is one.
                var withoutLast = new List<PerkDefinition>(offers);
                withoutLast.RemoveAt(withoutLast.Count - 1);
                PerkDefinition useful = PickOfRarity(rng, catalog, loadout, withoutLast, PerkRarity.Common, unavailable);
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
            List<PerkDefinition> alreadyOffered, PerkRarity wanted, ICollection<string> mustBeUsefulGiven)
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
                }
                if (candidates.Count > 0) return rng.Pick(candidates);
            }
            return null;
        }
    }
}
