using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Ante
{
    /// <summary>
    /// Everything about a run's antes and points that must survive a save and load. Plain public fields so
    /// Unity's JsonUtility can write it. In Milestone 14 one of these becomes a field of SaveData (with a
    /// version bump and migration step); until then nothing saves it.
    /// </summary>
    [Serializable]
    public sealed class AnteRunState
    {
        public int seed;
        public int ante = 1;
        public int phase;
        public float secondsLeft;
        public float stopwatchSecondsAdded;
        public float runSeconds;
        public bool perkDraftPending;
        public bool scratchPending;

        public int anteEarned;
        public int lifetimeRunScore;
        public int spendablePoints;

        public List<string> claims = new List<string>();
        public List<string> counterKeys = new List<string>();
        public List<int> counterValues = new List<int>();

        public int[] attributeLevels = new int[4];
        public List<string> perkIds = new List<string>();

        public bool chainHasLast;
        public int chainLastCategory;
        public float chainLastSeconds;
        public int chainUsesThisAnte;

        /// <summary>Scratch-off prizes that are not cash, waiting for the systems that will honour them.</summary>
        public int repairRefillVouchers;
        public int temporaryTacticalPerks;
        public int cosmeticTokens;
        /// <summary>Keep going after the last ante, and overtime (21 Nov 2026).</summary>
        public bool endless;
        public bool inOvertime;
        public float overtimeSeconds;
        public bool clearedInOvertime;
        /// <summary>The upgrade screen (21 Nov 2026): this ante's pick taken in the field, and rerolls bought this break.</summary>
        public bool perkTakenEarly;
        public int rerolls;
    }

    /// <summary>
    /// One run's ante and point system, assembled: ledger, claims, scoring, director, attributes, perks and
    /// scratch-offs. This is the single object the game will talk to. Plain C#, no Unity types.
    /// </summary>
    public sealed class AnteRun
    {
        public AnteBalance Balance { get; }
        public int Seed { get; }
        public RunLedger Ledger { get; }
        public ClaimRegistry Claims { get; }
        public PerkLoadout Perks { get; }
        public ScoreService Score { get; }
        public AnteDirector Director { get; }
        public AttributeShop Attributes { get; }
        public IReadOnlyList<PerkDefinition> Catalog { get; }

        public int RepairRefillVouchers { get; private set; }
        public int TemporaryTacticalPerks { get; private set; }
        public int CosmeticTokens { get; private set; }

        public AnteRun(AnteBalance balance, int seed, IReadOnlyList<PerkDefinition> catalog = null)
        {
            Balance = balance ?? new AnteBalance();
            Seed = seed;
            Catalog = catalog ?? PerkCatalog.CreateDefaults();
            Ledger = new RunLedger();
            Claims = new ClaimRegistry();
            Perks = new PerkLoadout();
            Score = new ScoreService(Balance, Ledger, Claims, Perks);
            Director = new AnteDirector(Balance, Ledger);
            Attributes = new AttributeShop(Balance, () => Director.Ante);
            Director.Changed += DropTheDraftIfNothingIsLeftToOffer;
        }

        /// <summary>
        /// If every perk is already held there is nothing to draft, so no pick is left pending with an empty
        /// screen behind it. With fewer than three left, the draft simply shows what there is.
        /// </summary>
        void DropTheDraftIfNothingIsLeftToOffer()
        {
            if (Director.PerkDraftPending && PerkOffers().Count == 0) Director.MarkPerkDraftTaken();
        }

        /// <summary>The one door every world system reports through.</summary>
        public ScoreResult Report(ScoreEvent scoreEvent)
        {
            return Score.Submit(scoreEvent, Director.Ante, Director.IsRunning, Director.RunSeconds);
        }

        public void Tick(float deltaSeconds, bool paused) => Director.Tick(deltaSeconds, paused);

        /// <summary>Attributes can only be bought on the upgrade screen, between antes.</summary>
        public bool TryBuyAttribute(PlayerAttribute attribute)
        {
            return Director.Phase == AntePhase.Intermission && Attributes.TryBuy(attribute, Ledger);
        }

        /// <summary>
        /// The three perks on offer for this ante's pick: at the break, or in the field once the target is met.
        /// Asking twice gives the same three: the roll depends only on the seed, the ante, the rerolls bought and
        /// what the player already holds.
        /// </summary>
        public List<PerkDefinition> PerkOffers(ICollection<string> unavailable = null)
        {
            if (!Director.CanPickPerk) return new List<PerkDefinition>();
            SeededRandom rng = PerkDraft.StreamFor(Seed, Director.Ante);
            if (Rerolls > 0) rng = rng.Fork(1000 + Rerolls);
            return PerkDraft.Roll(rng, Catalog, Perks, Balance, unavailable);
        }

        /// <summary>Rerolls bought for this ante's pick (21 Nov 2026: a use for points once the shop is full). Back to 0 at the next ante.</summary>
        public int Rerolls { get; private set; }

        /// <summary>What the next reroll costs: the listed price times the ante's value, doubling with each one bought.</summary>
        public int RerollPrice
        {
            get
            {
                double price = Balance.perkRerollPrice * (double)ScoreMath.ValueScale(Balance, Director.Ante) * Math.Pow(2.0, Math.Min(Rerolls, 10));
                return (int)(Math.Round(price / 10.0, MidpointRounding.AwayFromZero) * 10);
            }
        }

        /// <summary>Pays for three new offers. Spends the wallet only; the score is never touched.</summary>
        public bool RerollPerks()
        {
            if (!Director.CanPickPerk || Balance.perkRerollPrice <= 0 || !Ledger.TrySpend(RerollPrice)) return false;
            Rerolls++;
            return true;
        }

        /// <summary>
        /// A listed cash prize (skip, scratch-off) at this ante's value, rounded to tens, so it keeps pace with the
        /// shop prices it is spent on (design review, 21 Nov 2026). Unscaled when prices do not follow value.
        /// </summary>
        public int ScaledCash(int listed)
        {
            if (!Balance.shopPricesFollowValue || listed <= 0) return Math.Max(0, listed);
            double v = listed * (double)ScoreMath.ValueScale(Balance, Director.Ante);
            return (int)(Math.Round(v / 10.0, MidpointRounding.AwayFromZero) * 10);
        }

        /// <summary>What skipping this ante's three perks pays into the wallet.</summary>
        public int SkipCash => ScaledCash(Balance.perkSkipCash);

        /// <summary>What a scratch-off outcome pays into the wallet at this ante.</summary>
        public int ScratchCash(ScratchOutcome outcome) => ScaledCash(ScratchOff.CashFor(Balance, outcome));

        /// <summary>Gives up this ante's pick for <see cref="SkipCash"/>. In the field or at the break.</summary>
        public bool SkipPerks()
        {
            if (!Director.CanPickPerk) return false;
            int cash = SkipCash;
            Director.MarkPerkDraftTaken();
            Ledger.AddBankOnly(cash);
            return true;
        }

        public bool TakePerk(PerkDefinition perk, ICollection<string> unavailable = null)
        {
            if (perk == null || !Director.CanPickPerk) return false;
            if (!PerkOffers(unavailable).Contains(perk)) return false;
            if (!Perks.Add(perk)) return false;
            Director.MarkPerkDraftTaken();
            return true;
        }

        /// <summary>Uses the free scratch-off. Cash goes to the wallet only. Returns null if there is no ticket to use.</summary>
        public ScratchOutcome? UseScratchOff()
        {
            if (!Director.ScratchPending) return null;
            ScratchOutcome outcome = ScratchOff.Roll(ScratchOff.StreamFor(Seed, Director.Ante), Balance);
            Director.MarkScratchUsed();
            Ledger.AddBankOnly(ScratchCash(outcome));
            if (outcome == ScratchOutcome.RepairRefillVoucher) RepairRefillVouchers++;
            else if (outcome == ScratchOutcome.TemporaryTacticalPerk) TemporaryTacticalPerks++;
            else if (outcome == ScratchOutcome.CosmeticToken) CosmeticTokens++;
            return outcome;
        }

        public bool BeginNextAnte()
        {
            if (!Director.BeginNextAnte()) return false;
            Score.StartNewAnte();
            Rerolls = 0;
            return true;
        }

        public AnteRunState Export()
        {
            var state = new AnteRunState
            {
                seed = Seed,
                ante = Director.Ante,
                phase = (int)Director.Phase,
                secondsLeft = Director.SecondsLeft,
                stopwatchSecondsAdded = Director.StopwatchSecondsAdded,
                runSeconds = Director.RunSeconds,
                perkDraftPending = Director.PerkDraftPending,
                scratchPending = Director.ScratchPending,
                anteEarned = Ledger.AnteEarned,
                lifetimeRunScore = Ledger.LifetimeRunScore,
                spendablePoints = Ledger.SpendablePoints,
                attributeLevels = Attributes.ExportLevels(),
                repairRefillVouchers = RepairRefillVouchers,
                temporaryTacticalPerks = TemporaryTacticalPerks,
                cosmeticTokens = CosmeticTokens,
                endless = Director.Endless,
                inOvertime = Director.InOvertime,
                overtimeSeconds = Director.OvertimeSeconds,
                clearedInOvertime = Director.ClearedInOvertime,
                perkTakenEarly = Director.PerkTakenEarly,
                rerolls = Rerolls,
            };
            Claims.Export(state.claims, state.counterKeys, state.counterValues);
            for (int i = 0; i < Perks.Owned.Count; i++) state.perkIds.Add(Perks.Owned[i].id);
            Score.ExportChain(out state.chainHasLast, out state.chainLastCategory, out state.chainLastSeconds, out state.chainUsesThisAnte);
            return state;
        }

        /// <summary>Rebuilds a run from saved state. Perks that no longer exist in the catalog are dropped quietly.</summary>
        public static AnteRun Import(AnteBalance balance, AnteRunState state, IReadOnlyList<PerkDefinition> catalog = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var run = new AnteRun(balance, state.seed, catalog);
            run.Ledger.Restore(state.anteEarned, state.lifetimeRunScore, state.spendablePoints);
            run.Claims.Import(state.claims, state.counterKeys, state.counterValues);
            run.Attributes.ImportLevels(state.attributeLevels);
            if (state.perkIds != null)
            {
                for (int i = 0; i < state.perkIds.Count; i++)
                {
                    for (int c = 0; c < run.Catalog.Count; c++)
                        if (run.Catalog[c].id == state.perkIds[i]) { run.Perks.Add(run.Catalog[c]); break; }
                }
            }
            AntePhase phase = Enum.IsDefined(typeof(AntePhase), state.phase) ? (AntePhase)state.phase : AntePhase.Running;
            run.Director.RestoreLater(state.endless, state.inOvertime, state.overtimeSeconds, state.clearedInOvertime, state.perkTakenEarly);
            run.Director.Restore(state.ante, phase, state.secondsLeft, state.stopwatchSecondsAdded, state.runSeconds,
                state.perkDraftPending, state.scratchPending);
            run.Score.ImportChain(state.chainHasLast, state.chainLastCategory, state.chainLastSeconds, state.chainUsesThisAnte);
            run.RepairRefillVouchers = Math.Max(0, state.repairRefillVouchers);
            run.TemporaryTacticalPerks = Math.Max(0, state.temporaryTacticalPerks);
            run.CosmeticTokens = Math.Max(0, state.cosmeticTokens);
            run.Rerolls = Math.Max(0, state.rerolls);
            return run;
        }
    }
}
