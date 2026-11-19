using System;
using System.Collections.Generic;

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
            Attributes = new AttributeShop(Balance);
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
        /// The three perks on offer after the ante just cleared. Asking twice gives the same three: the roll
        /// depends only on the seed, the ante and what the player already holds.
        /// </summary>
        public List<PerkDefinition> PerkOffers(ICollection<string> unavailable = null)
        {
            if (!Director.PerkDraftPending) return new List<PerkDefinition>();
            return PerkDraft.Roll(PerkDraft.StreamFor(Seed, Director.Ante), Catalog, Perks, Balance, unavailable);
        }

        public bool TakePerk(PerkDefinition perk, ICollection<string> unavailable = null)
        {
            if (perk == null || !Director.PerkDraftPending) return false;
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
            Ledger.AddBankOnly(ScratchOff.CashFor(Balance, outcome));
            if (outcome == ScratchOutcome.RepairRefillVoucher) RepairRefillVouchers++;
            else if (outcome == ScratchOutcome.TemporaryTacticalPerk) TemporaryTacticalPerks++;
            else if (outcome == ScratchOutcome.CosmeticToken) CosmeticTokens++;
            return outcome;
        }

        public bool BeginNextAnte()
        {
            if (!Director.BeginNextAnte()) return false;
            Score.StartNewAnte();
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
            run.Director.Restore(state.ante, phase, state.secondsLeft, state.stopwatchSecondsAdded, state.runSeconds,
                state.perkDraftPending, state.scratchPending);
            run.Score.ImportChain(state.chainHasLast, state.chainLastCategory, state.chainLastSeconds, state.chainUsesThisAnte);
            run.RepairRefillVouchers = Math.Max(0, state.repairRefillVouchers);
            run.TemporaryTacticalPerks = Math.Max(0, state.temporaryTacticalPerks);
            run.CosmeticTokens = Math.Max(0, state.cosmeticTokens);
            return run;
        }
    }
}
