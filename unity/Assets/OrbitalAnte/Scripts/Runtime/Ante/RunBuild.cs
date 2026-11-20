using System;
using System.Collections.Generic;

namespace OrbitalAnte.Ante
{
    /// <summary>
    /// The perk list the game uses (20 Nov 2026, roguelite fun pass; research doc "Roguelite fun: research and plan").
    /// Every perk here is wired into the game: none waits on a system that does not exist yet. Most change how a
    /// fight goes (heals, refills, time back, ladder speed) rather than adding a percentage. Names and numbers are
    /// mine and not playtested. The six examples in <see cref="PerkCatalog.CreateDefaults"/> stay as the point
    /// system's own test set; the two of them that are wired (Glass Cannon, Change of Pace) are in this list too.
    /// </summary>
    public static class RoguelitePerks
    {
        static PerkDefinition P(string id, string name, PerkRarity rarity, PerkFamily family, params PerkEffect[] effects)
        {
            return new PerkDefinition
            {
                id = "perk." + id, displayName = name, rarity = rarity, family = family,
                tags = family == PerkFamily.None ? new string[0] : new[] { family.ToString() },
                effects = effects,
            };
        }

        static PerkEffect E(PerkStat stat, float value) => new PerkEffect(stat, value);

        public static List<PerkDefinition> Create()
        {
            return new List<PerkDefinition>
            {
                // ---- Gunner: guns and the gun ladder ----
                P("quick_study", "Quick Study", PerkRarity.Common, PerkFamily.Gunner, E(PerkStat.LadderDoubleEvery, 3f)),
                P("hot_brass", "Hot Brass", PerkRarity.Uncommon, PerkFamily.Gunner, E(PerkStat.RoundsOnKill, 8f)),
                P("steady_hands", "Steady Hands", PerkRarity.Common, PerkFamily.Gunner, E(PerkStat.GunDamage, 0.10f)),
                P("trigger_discipline", "Trigger Discipline", PerkRarity.Rare, PerkFamily.Gunner,
                    E(PerkStat.GunDamage, 0.22f), E(PerkStat.MoveSpeed, -0.08f)),

                // ---- Brawler: melee and close range ----
                P("bloodthirst", "Bloodthirst", PerkRarity.Common, PerkFamily.Brawler, E(PerkStat.HealOnMeleeKill, 15f)),
                P("heavy_swing", "Heavy Swing", PerkRarity.Common, PerkFamily.Brawler, E(PerkStat.MeleeDamage, 0.25f)),
                P("in_your_face", "In Your Face", PerkRarity.Uncommon, PerkFamily.Brawler, E(PerkStat.CloseRangeDefence, -0.20f)),
                P("second_wind", "Second Wind", PerkRarity.Uncommon, PerkFamily.Brawler, E(PerkStat.HealOnKill, 6f)),

                // ---- Ghost: quiet, patient, long range ----
                P("eagle_eye", "Eagle Eye", PerkRarity.Common, PerkFamily.Ghost, E(PerkStat.LongShotCloser, 15f)),
                P("patient_hunter", "Patient Hunter", PerkRarity.Uncommon, PerkFamily.Ghost, E(PerkStat.LowStanceDamage, 0.20f)),
                P("cold_blood", "Cold Blood", PerkRarity.Common, PerkFamily.Ghost, E(PerkStat.FocusGain, 0.30f)),
                P("quiet_professional", "Quiet Professional", PerkRarity.Uncommon, PerkFamily.Ghost,
                    new PerkEffect(ScoreEventKind.OptionalMission, "base", 0.20f)),

                // ---- Pilot: the ship, the rover, getting about ----
                P("road_rage", "Road Rage", PerkRarity.Common, PerkFamily.Pilot, E(PerkStat.HealOnSplat, 10f)),
                P("frequent_flyer", "Frequent Flyer", PerkRarity.Common, PerkFamily.Pilot,
                    new PerkEffect(ScoreEventKind.NewPlanet, null, 0.20f), new PerkEffect(ScoreEventKind.NewMoon, null, 0.20f),
                    new PerkEffect(ScoreEventKind.DifficultLanding, null, 0.20f)),
                P("fleet_foot", "Fleet Foot", PerkRarity.Common, PerkFamily.Pilot, E(PerkStat.MoveSpeed, 0.10f)),
                P("on_the_clock", "On the Clock", PerkRarity.Rare, PerkFamily.Pilot, E(PerkStat.LandingTimeBack, 12f)),

                // ---- Daredevil: risk for reward ----
                new PerkDefinition
                {
                    id = "perk.glass_cannon", displayName = "Glass Cannon", rarity = PerkRarity.Uncommon, family = PerkFamily.Daredevil,
                    tags = new[] { "Daredevil" },
                    effects = new[] { E(PerkStat.WeaponDamage, 0.20f), E(PerkStat.MaxHp, -0.15f) },
                },
                P("last_stand", "Last Stand", PerkRarity.Uncommon, PerkFamily.Daredevil, E(PerkStat.LowHealthDamage, 0.30f)),
                P("no_brakes", "No Brakes", PerkRarity.Common, PerkFamily.Daredevil, E(PerkStat.MoveSpeed, 0.15f), E(PerkStat.IncomingDamage, 0.10f)),
                P("all_in", "All In", PerkRarity.Rare, PerkFamily.Daredevil,
                    new PerkEffect(ScoreEventKind.EnemyTier1, "kill", 0.20f), E(PerkStat.RegenRate, -0.40f)),

                // ---- Wild cards: no family ----
                new PerkDefinition
                {
                    id = PerkCatalog.ChainPerkId, displayName = "Change of Pace", rarity = PerkRarity.Rare,
                    effects = new[] { new PerkEffect(PerkStat.ChainScoreBonus, 0.10f) },
                },
                P("thick_skin", "Thick Skin", PerkRarity.Common, PerkFamily.None, E(PerkStat.MaxHp, 0.15f)),
                P("quick_mend", "Quick Mend", PerkRarity.Common, PerkFamily.None, E(PerkStat.RegenRate, 0.30f)),
            };
        }
    }

    /// <summary>What holding three perks of one family adds. Written as perk effects so the text comes from the numbers.</summary>
    public static class PerkFamilies
    {
        public static readonly PerkFamily[] All = { PerkFamily.Gunner, PerkFamily.Brawler, PerkFamily.Ghost, PerkFamily.Pilot, PerkFamily.Daredevil };

        public static int SetSize(AnteBalance balance) => Math.Max(2, balance.familySetSize);

        public static PerkEffect[] Bonus(PerkFamily family)
        {
            switch (family)
            {
                case PerkFamily.Gunner: return new[] { new PerkEffect(PerkStat.GunDamage, 0.15f), new PerkEffect(PerkStat.RoundsOnKill, 5f) };
                case PerkFamily.Brawler: return new[] { new PerkEffect(PerkStat.FocusOnMeleeKill, 0.25f) };
                case PerkFamily.Ghost: return new[] { new PerkEffect(PerkStat.TimeOnLongShot, 5f) };
                case PerkFamily.Pilot: return new[] { new PerkEffect(PerkStat.LandingTimeBack, 15f), new PerkEffect(PerkStat.MoveSpeed, 0.10f) };
                case PerkFamily.Daredevil: return new[] { new PerkEffect(PerkStat.HealOnLowHealthKill, 20f) };
                default: return new PerkEffect[0];
            }
        }

        /// <summary>The family bonus in words ("+15% gun damage, kills put 5 rounds back in the magazine").</summary>
        public static string Describe(PerkFamily family, AnteBalance balance)
        {
            var perk = new PerkDefinition { effects = Bonus(family) };
            return perk.Describe(balance);
        }

        public static bool Active(PerkLoadout loadout, PerkFamily family, AnteBalance balance)
        {
            return loadout != null && loadout.FamilyCount(family) >= SetSize(balance);
        }

        /// <summary>For a card: "GUNNER 2/3" with what the third brings, or "GUNNER SET ON".</summary>
        public static string Progress(PerkLoadout loadout, PerkFamily family, AnteBalance balance)
        {
            if (family == PerkFamily.None) return "WILD CARD";
            int have = loadout == null ? 0 : loadout.FamilyCount(family), need = SetSize(balance);
            string name = family.ToString().ToUpperInvariant();
            return have >= need ? name + " SET ON" : name + " " + have + "/" + need;
        }
    }

    /// <summary>
    /// "Raise the stakes" (20 Nov 2026): one optional hazard chosen before an ante. Taking one marks every score
    /// event of that ante as a risk the player chose beforehand, which the score formula already pays at x1.15.
    /// Saved by name; add new ones at the end.
    /// </summary>
    public enum Hazard
    {
        None = 0,
        /// <summary>Enemies react and aim sharper, and more of them are the heavy kind.</summary>
        Hunted = 1,
        /// <summary>Health does not come back by itself (kills, perks and the ship still heal).</summary>
        ThinAir = 2,
        /// <summary>The ante is a minute shorter.</summary>
        ShortFuse = 3,
    }

    public static class HazardRules
    {
        public static readonly Hazard[] Offered = { Hazard.Hunted, Hazard.ThinAir, Hazard.ShortFuse };

        public static string Name(Hazard hazard)
        {
            switch (hazard)
            {
                case Hazard.Hunted: return "Hunted";
                case Hazard.ThinAir: return "Thin Air";
                case Hazard.ShortFuse: return "Short Fuse";
                default: return "No hazard";
            }
        }

        public static string Describe(Hazard hazard, AnteBalance balance)
        {
            switch (hazard)
            {
                case Hazard.Hunted:
                    return "enemies react and aim " + Percent(1f - balance.huntedSharpness) + " sharper, and " + Percent(balance.huntedHeavyShare)
                           + " more of them are heavies";
                case Hazard.ThinAir: return "health does not come back by itself; kills, perks and the ship still heal";
                case Hazard.ShortFuse: return "the ante is " + Clock(balance.anteSeconds - balance.shortFuseSeconds) + " instead of " + Clock(balance.anteSeconds);
                default: return "play the ante as it comes";
            }
        }

        /// <summary>The score multiplier a hazard earns (the formula's accepted-risk number).</summary>
        public static float Multiplier(Hazard hazard, AnteBalance balance) => hazard == Hazard.None ? 1f : balance.riskAccepted;

        /// <summary>Seconds the ante starts with under a hazard.</summary>
        public static float AnteSeconds(Hazard hazard, AnteBalance balance)
        {
            float full = balance.anteSeconds;
            return hazard == Hazard.ShortFuse ? Math.Max(60f, full - balance.shortFuseSeconds) : full;
        }

        static string Percent(float v) => (int)Math.Round(v * 100.0, MidpointRounding.AwayFromZero) + "%";

        static string Clock(float seconds)
        {
            int s = Math.Max(0, (int)Math.Ceiling(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }
    }

    /// <summary>
    /// How much harder enemies are this ante (20 Nov 2026). The design constitution forbids inflated health
    /// (principle 3), so an ante never adds health: enemies react and aim sharper, and more of them wake as the
    /// heavy kind, which is seen and learnt. Ante 1 is exactly as before.
    /// </summary>
    public struct EnemyPressure
    {
        /// <summary>Multiplies reaction time and aim error (under 1 is sharper).</summary>
        public float Sharpness;
        /// <summary>Share of plain enemies (tier 1) that wake as their side's heavy kind instead.</summary>
        public float HeavyShare;

        public static EnemyPressure For(int ante, Hazard hazard, AnteBalance balance)
        {
            ante = Math.Max(1, ante);
            float sharp = (float)Math.Pow(Math.Max(0.5, balance.sharpnessPerAnte), ante - 1);
            sharp = Math.Max(balance.sharpnessFloor, sharp);
            float heavy = ante >= balance.heavyFromAnte ? balance.heavySharePerAnte * (ante - balance.heavyFromAnte + 1) : 0f;
            heavy = Math.Min(balance.heavyShareMax, heavy);
            if (hazard == Hazard.Hunted)
            {
                sharp *= balance.huntedSharpness;
                heavy = Math.Min(0.75f, heavy + balance.huntedHeavyShare);
            }
            return new EnemyPressure { Sharpness = Math.Max(0.4f, sharp), HeavyShare = Math.Max(0f, heavy) };
        }

        /// <summary>
        /// Whether one enemy wakes as a heavy. A fair, stable roll from who it is and the ante, so the same enemy
        /// is the same kind every time it wakes in an ante, and the run's seeded streams are never touched.
        /// </summary>
        public static bool WakesHeavy(EnemyPressure pressure, int runSeed, string who, int ante)
        {
            if (pressure.HeavyShare <= 0f || string.IsNullOrEmpty(who)) return false;
            unchecked
            {
                uint h = 2166136261u ^ (uint)runSeed;
                for (int i = 0; i < who.Length; i++) h = (h ^ who[i]) * 16777619u;
                h = (h ^ (uint)ante) * 16777619u;
                h ^= h >> 15; h *= 0x2c1b3c6du; h ^= h >> 12;
                return (h % 10000u) / 10000f < pressure.HeavyShare;
            }
        }
    }

    /// <summary>
    /// Everything the run's build adds up to, in numbers the game systems read (20 Nov 2026). Perks, their family
    /// bonuses, bought attributes, the chosen hazard and a scratch-off boost are all folded in here, so each game
    /// system asks one place. Plain C#; <see cref="Neutral"/> outside a run.
    /// </summary>
    public sealed class RunMods
    {
        public float GunDamage = 1f;
        public float MeleeDamage = 1f;
        public float Incoming = 1f;
        public float CloseIncoming = 1f;
        public float MaxHealth = 1f;
        public float Regen = 1f;
        public bool NoRegen;
        public float Move = 1f;
        /// <summary>How many times longer the suit's air lasts.</summary>
        public float Oxygen = 1f;
        public float FocusGain = 1f;
        public float FocusOnMeleeKill;
        public float HealOnKill, HealOnMeleeKill, HealOnSplat, HealOnLowHealthKill;
        public int LadderDoubleEvery;
        public float LowHealthDamage = 1f;
        public float LowStanceDamage = 1f;
        public int RoundsOnKill;
        public float LongShotCloser;
        public float LandingTimeBack;
        public float TimeOnLongShot;
        public float LowHealthLine = 0.3f;
        public float CloseRange = 8f;
        public Hazard Hazard;

        public static readonly RunMods Neutral = new RunMods();

        /// <param name="tacticalBoost">A scratch-off's temporary tactical perk: +10% damage for one ante.</param>
        public static RunMods From(PerkLoadout perks, AttributeShop attributes, Hazard hazard, AnteBalance balance, bool tacticalBoost = false)
        {
            balance = balance ?? new AnteBalance();
            var m = new RunMods { Hazard = hazard, LowHealthLine = balance.lowHealthLine, CloseRange = balance.closeRange };
            var all = new PerkLoadout();
            if (perks != null) for (int i = 0; i < perks.Owned.Count; i++) all.Add(perks.Owned[i]);
            // Family bonuses go in as extra "perks" so they sum the same way and obey the same caps.
            for (int f = 0; f < PerkFamilies.All.Length; f++)
            {
                PerkFamily family = PerkFamilies.All[f];
                if (PerkFamilies.Active(perks, family, balance))
                    all.Add(new PerkDefinition { id = "set." + family, unique = false, effects = PerkFamilies.Bonus(family) });
            }

            float weapon = all.StatTotal(PerkStat.WeaponDamage, balance);
            float attributeDamage = attributes != null ? attributes.DamageMultiplier : 1f;
            float boost = tacticalBoost ? 1.10f : 1f;
            m.GunDamage = Math.Max(0.2f, (1f + weapon + all.StatTotal(PerkStat.GunDamage, balance)) * attributeDamage * boost);
            m.MeleeDamage = Math.Max(0.2f, (1f + weapon + all.StatTotal(PerkStat.MeleeDamage, balance)) * attributeDamage * boost);
            m.Incoming = Math.Max(0.2f, (1f + all.StatTotal(PerkStat.IncomingDamage, balance)) * (attributes != null ? attributes.IncomingDamageMultiplier : 1f));
            m.CloseIncoming = Math.Max(0.2f, 1f + all.StatTotal(PerkStat.CloseRangeDefence, balance));
            m.MaxHealth = Math.Max(0.3f, 1f + all.StatTotal(PerkStat.MaxHp, balance));
            m.Regen = Math.Max(0f, 1f + all.StatTotal(PerkStat.RegenRate, balance));
            m.NoRegen = hazard == Hazard.ThinAir;
            m.Move = Math.Max(0.5f, (1f + all.StatTotal(PerkStat.MoveSpeed, balance)) * (attributes != null ? attributes.MovementMultiplier : 1f));
            m.Oxygen = attributes != null ? Math.Max(1f, attributes.OxygenMultiplier) : 1f;
            m.FocusGain = Math.Max(0f, 1f + all.StatTotal(PerkStat.FocusGain, balance));
            m.FocusOnMeleeKill = Math.Max(0f, all.Sum(PerkStat.FocusOnMeleeKill));
            m.HealOnKill = Math.Max(0f, all.Sum(PerkStat.HealOnKill));
            m.HealOnMeleeKill = Math.Max(0f, all.Sum(PerkStat.HealOnMeleeKill));
            m.HealOnSplat = Math.Max(0f, all.Sum(PerkStat.HealOnSplat));
            m.HealOnLowHealthKill = Math.Max(0f, all.Sum(PerkStat.HealOnLowHealthKill));
            m.LadderDoubleEvery = (int)Math.Round(all.Smallest(PerkStat.LadderDoubleEvery));
            if (m.LadderDoubleEvery == 1) m.LadderDoubleEvery = 2;   // every kill twice would be a straight upgrade
            m.LowHealthDamage = 1f + Math.Max(0f, all.StatTotal(PerkStat.LowHealthDamage, balance));
            m.LowStanceDamage = 1f + Math.Max(0f, all.StatTotal(PerkStat.LowStanceDamage, balance));
            m.RoundsOnKill = Math.Max(0, (int)Math.Round(all.Sum(PerkStat.RoundsOnKill)));
            m.LongShotCloser = Math.Max(0f, Math.Min(balance.longShotMetres * 0.5f, all.Sum(PerkStat.LongShotCloser)));
            m.LandingTimeBack = Math.Max(0f, all.Sum(PerkStat.LandingTimeBack));
            m.TimeOnLongShot = Math.Max(0f, all.Sum(PerkStat.TimeOnLongShot));
            return m;
        }

        /// <summary>Gun damage for one shot, given how the player stands and how hurt they are.</summary>
        public float GunShot(bool lowStance, float healthShare)
        {
            float d = GunDamage;
            if (lowStance) d *= LowStanceDamage;
            if (healthShare < LowHealthLine) d *= LowHealthDamage;
            return d;
        }

        public float MeleeBlow(float healthShare) => MeleeDamage * (healthShare < LowHealthLine ? LowHealthDamage : 1f);

        /// <summary>What a shot that would do <paramref name="damage"/> does to the player, from a shooter this far away.</summary>
        public float Taken(float damage, float shooterDistance)
        {
            float d = damage * Incoming;
            if (shooterDistance >= 0f && shooterDistance <= CloseRange) d *= CloseIncoming;
            return d;
        }

        /// <summary>Whether this kill (the Nth of the run, counting from 1) counts twice on the gun ladder.</summary>
        public bool LadderDouble(int killNumber) => LadderDoubleEvery >= 2 && killNumber > 0 && killNumber % LadderDoubleEvery == 0;

        /// <summary>Health a kill gives back.</summary>
        public float HealFor(bool melee, bool splat, float healthShare)
        {
            float h = HealOnKill;
            if (melee) h += HealOnMeleeKill;
            if (splat) h += HealOnSplat;
            if (healthShare < LowHealthLine) h += HealOnLowHealthKill;
            return h;
        }
    }
}
