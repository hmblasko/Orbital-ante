using System;
using System.Collections.Generic;

namespace OrbitalAnte.Ante
{
    /// <summary>What ends an ante. This is an open design question: the value in the balance data is a placeholder until Matthew decides.</summary>
    public enum AnteEndCondition
    {
        /// <summary>The ante always runs to the end of its timer. Target met then: cleared. Not met: the run is over.</summary>
        OnTimer = 0,
        /// <summary>Once the target is met the player chooses when to move on, with no time pressure. Timer out with the target not met: the run is over.</summary>
        OnPlayerAdvance = 1,
        /// <summary>The player may move on early once the target is met; otherwise the timer decides, as in OnTimer.</summary>
        Hybrid = 2,
    }

    /// <summary>Whether clearing the last ante also gives a scratch-off. Open design question.</summary>
    public enum FinalScratchTiming
    {
        BetweenAntesOnly = 0,
        AlsoAtVictory = 1,
    }

    /// <summary>The broad kind of play a scoring event belongs to. Used for strategy parity and the "different category" perk.</summary>
    public enum ScoreCategory
    {
        Combat = 0,
        Exploration = 1,
        Bounty = 2,
        Objective = 3,
        Salvage = 4,
        Repair = 5,
        Landing = 6,
    }

    /// <summary>Every thing that can score. Saved by name, so never rename one; add new ones at the end.</summary>
    public enum ScoreEventKind
    {
        EnemyTier1 = 0,
        EnemyTier2 = 1,
        EnemyTier3 = 2,
        EnemyElite = 3,
        ShipDestroyed = 4,
        ShipHijacked = 5,
        BountyDead = 6,
        BountyAlive = 7,
        NewMoon = 8,
        NewPlanet = 9,
        Landmark = 10,
        CaveRuinObjective = 11,
        SatelliteRepaired = 12,
        DishNetworkCompleted = 13,
        DifficultLanding = 14,
        OptionalMission = 15,
        UnderThreatRecovery = 16,
        /// <summary>A newly generated, unique mission to retake a base the factions took back. Scored like an optional mission, with a cap per base.</summary>
        BaseRetakeMission = 17,
        /// <summary>Salvage brought home. No value has been given for this yet, so it scores nothing until one is set.</summary>
        SalvageDelivered = 18,
    }

    public enum PlayerAttribute
    {
        Damage = 0,
        Movement = 1,
        Oxygen = 2,
        Defense = 3,
    }

    public enum PerkRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
    }

    public enum ScratchOutcome
    {
        SmallCash = 0,
        BigCash = 1,
        RepairRefillVoucher = 2,
        TemporaryTacticalPerk = 3,
        CosmeticToken = 4,
    }

    /// <summary>
    /// Every number of the ante and point system in one place. PROVISIONAL: these are the starting values from
    /// Matthew's point-system handoff of 13 Nov 2026 and have not been balanced by play. Nothing in the rules
    /// hard-codes a number that is in here.
    ///
    /// Plain C# so the rules can be tested without Unity. <see cref="AnteBalanceData"/> is the asset that holds
    /// one of these for editing in the Inspector.
    /// </summary>
    [Serializable]
    public sealed class AnteBalance
    {
        // ---- Run structure ----
        /// <summary>Score needed to clear each ante. The number of entries is the number of antes.</summary>
        public int[] anteTargets = { 850, 1100, 1450, 1850, 2350, 3000, 3700, 4550, 5500 };
        /// <summary>How much more every action is worth each ante: K(a) = 1 + linear*(a-1) + quadratic*(a-1)^2.</summary>
        public float valueScaleLinear = 0.21f;
        public float valueScaleQuadratic = 0.035f;

        // ---- Timer (open question: length and end condition await Matthew) ----
        public float anteSeconds = 300f;
        public float stopwatchSeconds = 30f;
        public float stopwatchMaxPerAnte = 90f;
        public AnteEndCondition endCondition = AnteEndCondition.Hybrid;
        public FinalScratchTiming finalScratch = FinalScratchTiming.BetweenAntesOnly;

        // ---- Award multipliers ----
        public float perkScoreBonusCap = 0.25f;
        public float skillVerified = 1.10f;
        public float skillMax = 1.20f;
        public float riskAccepted = 1.15f;
        public float riskMax = 1.25f;
        /// <summary>Ceiling on perk x skill x risk together. The per-ante scale K is not part of it.</summary>
        public float eventMultiplierCap = 2.25f;

        // ---- Base points ----
        public int enemyTier1 = 45;
        public int enemyTier2 = 85;
        public int enemyTier3 = 150;
        public int enemyElite = 260;
        public int shipDestroyed = 180;
        public int shipHijacked = 260;
        public int bountyDead = 260;
        public int bountyAlive = 420;
        public int newMoon = 110;
        public int newPlanet = 150;
        public int landmark = 120;
        public int caveRuinObjective = 170;
        public int satelliteRepaired = 180;
        public int dishNetworkCompleted = 260;
        public int difficultLanding = 100;
        public int optionalMissionMin = 250;
        public int optionalMissionMax = 550;
        public int underThreatRecovery = 80;
        /// <summary>Not given in the handoff. 0 means "scores nothing yet".</summary>
        public int salvageDelivered = 0;

        // ---- Anti-farm caps (my provisional numbers; the handoff says "with caps" without giving them) ----
        public int underThreatRecoveriesPerAnte = 3;
        public int retakeMissionsPerBasePerRun = 2;

        // ---- Attributes ----
        public int maxAttributeLevel = 4;
        public int[] damagePrices = { 250, 450, 700, 1000 };
        public int[] movementPrices = { 250, 450, 700, 1000 };
        public int[] oxygenPrices = { 200, 350, 550, 800 };
        public int[] defensePrices = { 250, 450, 700, 1000 };
        /// <summary>Added per level: 1 + 0.08 x level.</summary>
        public float damagePerLevel = 0.08f;
        /// <summary>Player walking and jumping only. Never applied to the ship.</summary>
        public float movementPerLevel = 0.06f;
        public float oxygenPerLevel = 0.10f;
        /// <summary>Multiplied per level: incoming damage x (1 - 0.06)^level.</summary>
        public float defensePerLevel = 0.06f;

        // ---- Perk draft ----
        public int perkChoices = 3;
        public float rarityCommon = 70f;
        public float rarityUncommon = 25f;
        public float rarityRare = 5f;
        /// <summary>No perk stat can be pushed further than this in either direction by stacking (my provisional number).</summary>
        public float perkStatStackCap = 0.40f;
        public float chainWindowSeconds = 30f;
        public int chainUsesPerAnte = 2;

        // ---- Scratch-off (odds are shown to the player) ----
        public float scratchSmallCashWeight = 50f;
        public float scratchBigCashWeight = 25f;
        public float scratchVoucherWeight = 15f;
        public float scratchTacticalPerkWeight = 8f;
        public float scratchCosmeticWeight = 2f;
        public int scratchSmallCash = 150;
        public int scratchBigCash = 300;

        // ---- Starting resources (prototype values only; nothing reads these yet) ----
        public float playerHp = 100f;
        public float shipHull = 160f;
        public float fuelStart = 80f;
        public float fuelMax = 100f;
        public float cabinOxygenStart = 60f;
        public float cabinOxygenMax = 95f;
        public float suitOxygenStart = 55f;
        public float suitOxygenMax = 65f;
        public int tieUpShotsPerRun = 3;

        public int AnteCount => anteTargets == null ? 0 : anteTargets.Length;

        /// <summary>Target for ante 1, 2, 3... Out-of-range antes get the nearest one.</summary>
        public int TargetFor(int ante)
        {
            if (AnteCount == 0) return 0;
            int index = Math.Max(0, Math.Min(AnteCount - 1, ante - 1));
            return anteTargets[index];
        }

        public int BasePoints(ScoreEventKind kind)
        {
            switch (kind)
            {
                case ScoreEventKind.EnemyTier1: return enemyTier1;
                case ScoreEventKind.EnemyTier2: return enemyTier2;
                case ScoreEventKind.EnemyTier3: return enemyTier3;
                case ScoreEventKind.EnemyElite: return enemyElite;
                case ScoreEventKind.ShipDestroyed: return shipDestroyed;
                case ScoreEventKind.ShipHijacked: return shipHijacked;
                case ScoreEventKind.BountyDead: return bountyDead;
                case ScoreEventKind.BountyAlive: return bountyAlive;
                case ScoreEventKind.NewMoon: return newMoon;
                case ScoreEventKind.NewPlanet: return newPlanet;
                case ScoreEventKind.Landmark: return landmark;
                case ScoreEventKind.CaveRuinObjective: return caveRuinObjective;
                case ScoreEventKind.SatelliteRepaired: return satelliteRepaired;
                case ScoreEventKind.DishNetworkCompleted: return dishNetworkCompleted;
                case ScoreEventKind.DifficultLanding: return difficultLanding;
                case ScoreEventKind.UnderThreatRecovery: return underThreatRecovery;
                case ScoreEventKind.SalvageDelivered: return salvageDelivered;
                // Missions carry their own value, kept inside the allowed range by the score service.
                case ScoreEventKind.OptionalMission:
                case ScoreEventKind.BaseRetakeMission: return optionalMissionMin;
                default: return 0;
            }
        }

        public static ScoreCategory CategoryOf(ScoreEventKind kind)
        {
            switch (kind)
            {
                case ScoreEventKind.EnemyTier1:
                case ScoreEventKind.EnemyTier2:
                case ScoreEventKind.EnemyTier3:
                case ScoreEventKind.EnemyElite:
                case ScoreEventKind.ShipDestroyed:
                case ScoreEventKind.ShipHijacked: return ScoreCategory.Combat;
                case ScoreEventKind.BountyDead:
                case ScoreEventKind.BountyAlive: return ScoreCategory.Bounty;
                case ScoreEventKind.NewMoon:
                case ScoreEventKind.NewPlanet:
                case ScoreEventKind.Landmark: return ScoreCategory.Exploration;
                case ScoreEventKind.CaveRuinObjective:
                case ScoreEventKind.DishNetworkCompleted:
                case ScoreEventKind.OptionalMission:
                case ScoreEventKind.BaseRetakeMission: return ScoreCategory.Objective;
                case ScoreEventKind.SatelliteRepaired:
                case ScoreEventKind.UnderThreatRecovery: return ScoreCategory.Repair;
                case ScoreEventKind.DifficultLanding: return ScoreCategory.Landing;
                case ScoreEventKind.SalvageDelivered: return ScoreCategory.Salvage;
                default: return ScoreCategory.Objective;
            }
        }

        public int[] PricesFor(PlayerAttribute attribute)
        {
            switch (attribute)
            {
                case PlayerAttribute.Damage: return damagePrices;
                case PlayerAttribute.Movement: return movementPrices;
                case PlayerAttribute.Oxygen: return oxygenPrices;
                default: return defensePrices;
            }
        }

        /// <summary>Adds a plain-language line for anything wrong with these numbers.</summary>
        public void Validate(List<string> problems)
        {
            if (AnteCount == 0) problems.Add("Ante balance: there are no ante targets.");
            for (int i = 0; i < AnteCount; i++)
            {
                if (anteTargets[i] <= 0) problems.Add("Ante balance: the target for ante " + (i + 1) + " must be above zero.");
                if (i > 0 && anteTargets[i] < anteTargets[i - 1]) problems.Add("Ante balance: the target for ante " + (i + 1) + " is lower than the one before it.");
            }
            if (anteSeconds <= 0f) problems.Add("Ante balance: the ante timer must be longer than zero seconds.");
            if (stopwatchMaxPerAnte < 0f || stopwatchSeconds < 0f) problems.Add("Ante balance: stopwatch times cannot be negative.");
            if (skillMax < 1f || riskMax < 1f || eventMultiplierCap < 1f) problems.Add("Ante balance: multiplier ceilings cannot be below 1.");
            if (optionalMissionMax < optionalMissionMin) problems.Add("Ante balance: the optional mission range is upside down.");
            foreach (PlayerAttribute attribute in Enum.GetValues(typeof(PlayerAttribute)))
            {
                int[] prices = PricesFor(attribute);
                if (prices == null || prices.Length < maxAttributeLevel)
                    problems.Add("Ante balance: " + attribute + " needs a price for each of its " + maxAttributeLevel + " levels.");
            }
            if (rarityCommon + rarityUncommon + rarityRare <= 0f) problems.Add("Ante balance: perk rarity weights add up to nothing.");
            if (ScratchOff.TotalWeight(this) <= 0f) problems.Add("Ante balance: scratch-off weights add up to nothing.");
            if (salvageDelivered <= 0) problems.Add("Ante balance: salvage has no point value yet, so delivering salvage scores nothing.");
        }
    }
}
