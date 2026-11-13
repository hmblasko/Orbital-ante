using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Ante;
using OrbitalAnte.Core;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// What the player gets between antes: attributes bought with points, the pick-1-of-3 perk draft and the
    /// scratch-off. These run without Unity objects.
    /// </summary>
    public class AnteRewardsTests
    {
        static AnteRun ClearedFirstAnte(int seed = 5, int walletScore = 550)
        {
            var run = new AnteRun(new AnteBalance { anteTargets = new[] { 250, 250, 250 } }, seed);
            int n = 0;
            while (run.Ledger.SpendablePoints < walletScore)
                run.Report(new ScoreEvent { claimId = "m" + n++, kind = ScoreEventKind.OptionalMission, missionBasePoints = 550 });
            run.Director.ConfirmAdvance();
            return run;
        }

        // ---- Attributes ----

        [Test]
        public void AttributePricesAreTheHandoffPrices()
        {
            var balance = new AnteBalance();
            CollectionAssert.AreEqual(new[] { 250, 450, 700, 1000 }, balance.PricesFor(PlayerAttribute.Damage));
            CollectionAssert.AreEqual(new[] { 250, 450, 700, 1000 }, balance.PricesFor(PlayerAttribute.Movement));
            CollectionAssert.AreEqual(new[] { 200, 350, 550, 800 }, balance.PricesFor(PlayerAttribute.Oxygen));
            CollectionAssert.AreEqual(new[] { 250, 450, 700, 1000 }, balance.PricesFor(PlayerAttribute.Defense));
        }

        [Test]
        public void BuyingAnAttributeSpendsTheWalletAndNothingElse()
        {
            AnteRun run = ClearedFirstAnte();
            Assert.IsTrue(run.TryBuyAttribute(PlayerAttribute.Damage));
            Assert.AreEqual(1, run.Attributes.Level(PlayerAttribute.Damage));
            Assert.AreEqual(300, run.Ledger.SpendablePoints);
            Assert.AreEqual(550, run.Ledger.LifetimeRunScore);
            Assert.AreEqual(550, run.Ledger.AnteEarned);
            Assert.IsFalse(run.TryBuyAttribute(PlayerAttribute.Damage), "level 2 costs 450 and only 300 is left");
            Assert.AreEqual(300, run.Ledger.SpendablePoints);
        }

        [Test]
        public void AttributesStopAtLevelFour()
        {
            AnteRun run = ClearedFirstAnte(5, 5000);
            for (int i = 0; i < 4; i++) Assert.IsTrue(run.TryBuyAttribute(PlayerAttribute.Oxygen));
            Assert.IsFalse(run.TryBuyAttribute(PlayerAttribute.Oxygen));
            Assert.AreEqual(-1, run.Attributes.NextPrice(PlayerAttribute.Oxygen));
            Assert.AreEqual(4, run.Attributes.Level(PlayerAttribute.Oxygen));
        }

        [Test]
        public void AttributesCannotBeBoughtDuringAnAnte()
        {
            var run = new AnteRun(new AnteBalance(), 5);
            run.Report(new ScoreEvent { claimId = "m", kind = ScoreEventKind.OptionalMission, missionBasePoints = 550 });
            Assert.IsFalse(run.TryBuyAttribute(PlayerAttribute.Damage));
            Assert.AreEqual(550, run.Ledger.SpendablePoints);
        }

        [Test]
        public void AttributeEffectsMatchTheHandoff()
        {
            var balance = new AnteBalance();
            var shop = new AttributeShop(balance);
            shop.ImportLevels(new[] { 4, 4, 4, 4 });
            Assert.AreEqual(1.32f, shop.DamageMultiplier, 0.0001f);
            Assert.AreEqual(1.24f, shop.MovementMultiplier, 0.0001f);
            Assert.AreEqual(1.40f, shop.OxygenMultiplier, 0.0001f);
            Assert.AreEqual(0.94f * 0.94f * 0.94f * 0.94f, shop.IncomingDamageMultiplier, 0.0001f);

            shop.ImportLevels(new[] { 0, 0, 0, 0 });
            Assert.AreEqual(1f, shop.DamageMultiplier);
            Assert.AreEqual(1f, shop.IncomingDamageMultiplier);
        }

        [Test]
        public void AttributeLevelsSurviveSaveAndLoadAndBadSavesAreClamped()
        {
            var balance = new AnteBalance { anteTargets = new[] { 250, 250, 250 } };
            AnteRun run = ClearedFirstAnte();
            run.TryBuyAttribute(PlayerAttribute.Defense);
            AnteRunState state = run.Export();
            Assert.AreEqual(1, AnteRun.Import(balance, state).Attributes.Level(PlayerAttribute.Defense));

            state.attributeLevels = new[] { 99, -3, 2 };
            AnteRun odd = AnteRun.Import(balance, state);
            Assert.AreEqual(4, odd.Attributes.Level(PlayerAttribute.Damage));
            Assert.AreEqual(0, odd.Attributes.Level(PlayerAttribute.Movement));
            Assert.AreEqual(2, odd.Attributes.Level(PlayerAttribute.Oxygen));
            Assert.AreEqual(0, odd.Attributes.Level(PlayerAttribute.Defense));
        }

        // ---- Perk draft ----

        [Test]
        public void TheDraftOffersThreeDifferentPerks()
        {
            var balance = new AnteBalance();
            List<PerkDefinition> catalog = PerkCatalog.CreateDefaults();
            for (int seed = 0; seed < 500; seed++)
            {
                List<PerkDefinition> offers = PerkDraft.Roll(PerkDraft.StreamFor(seed, 1), catalog, new PerkLoadout(), balance);
                Assert.AreEqual(3, offers.Count);
                Assert.AreNotSame(offers[0], offers[1]);
                Assert.AreNotSame(offers[0], offers[2]);
                Assert.AreNotSame(offers[1], offers[2]);
            }
        }

        [Test]
        public void TheSameSeedAndAnteGiveTheSameDraft()
        {
            var balance = new AnteBalance();
            List<PerkDefinition> catalog = PerkCatalog.CreateDefaults();
            for (int ante = 1; ante <= 8; ante++)
            {
                List<PerkDefinition> a = PerkDraft.Roll(PerkDraft.StreamFor(123, ante), catalog, new PerkLoadout(), balance);
                List<PerkDefinition> b = PerkDraft.Roll(PerkDraft.StreamFor(123, ante), catalog, new PerkLoadout(), balance);
                CollectionAssert.AreEqual(a, b);
            }
        }

        [Test]
        public void ReloadingCannotRerollTheDraft()
        {
            var balance = new AnteBalance { anteTargets = new[] { 250, 250, 250 } };
            AnteRun run = ClearedFirstAnte(77);
            List<PerkDefinition> first = run.PerkOffers();
            AnteRun loaded = AnteRun.Import(balance, run.Export());
            List<PerkDefinition> again = loaded.PerkOffers();
            Assert.AreEqual(3, first.Count);
            for (int i = 0; i < first.Count; i++) Assert.AreEqual(first[i].id, again[i].id);
        }

        [Test]
        public void AUniquePerkAlreadyHeldIsNeverOfferedAgain()
        {
            var balance = new AnteBalance();
            List<PerkDefinition> catalog = PerkCatalog.CreateDefaults();
            var loadout = new PerkLoadout();
            loadout.Add(catalog[0]);
            loadout.Add(catalog[2]);
            for (int seed = 0; seed < 500; seed++)
            {
                List<PerkDefinition> offers = PerkDraft.Roll(PerkDraft.StreamFor(seed, 2), catalog, loadout, balance);
                CollectionAssert.DoesNotContain(offers, catalog[0]);
                CollectionAssert.DoesNotContain(offers, catalog[2]);
            }
            Assert.IsFalse(loadout.Add(catalog[0]), "and it cannot be held twice");
        }

        [Test]
        public void ThereIsAlwaysAUsefulChoice()
        {
            var balance = new AnteBalance();
            // A catalog where most perks need something the run has run out of.
            var catalog = new List<PerkDefinition>();
            for (int i = 0; i < 8; i++) catalog.Add(new PerkDefinition { id = "needs" + i, rarity = PerkRarity.Common, needs = "tieup" });
            catalog.Add(new PerkDefinition { id = "always", rarity = PerkRarity.Rare });
            var unavailable = new HashSet<string> { "tieup" };

            for (int seed = 0; seed < 500; seed++)
            {
                List<PerkDefinition> offers = PerkDraft.Roll(PerkDraft.StreamFor(seed, 1), catalog, new PerkLoadout(), balance, unavailable);
                Assert.AreEqual(3, offers.Count);
                Assert.IsTrue(offers.Exists(p => PerkDraft.IsUseful(p, unavailable)), "seed " + seed);
            }
        }

        [Test]
        public void RarityRollsFollowTheWeights()
        {
            var balance = new AnteBalance();
            var rng = new SeededRandom(2026);
            int[] counts = new int[3];
            const int rolls = 200000;
            for (int i = 0; i < rolls; i++) counts[(int)PerkDraft.RollRarity(rng, balance)]++;
            Assert.AreEqual(0.70, counts[0] / (double)rolls, 0.01);
            Assert.AreEqual(0.25, counts[1] / (double)rolls, 0.01);
            Assert.AreEqual(0.05, counts[2] / (double)rolls, 0.005);
        }

        [Test]
        public void OnePerkPerClearedAnteAndOnlyFromTheOffers()
        {
            AnteRun run = ClearedFirstAnte(9);
            List<PerkDefinition> offers = run.PerkOffers();
            PerkDefinition notOffered = null;
            foreach (PerkDefinition perk in run.Catalog) if (!offers.Contains(perk)) notOffered = perk;

            Assert.IsFalse(run.TakePerk(notOffered));
            Assert.IsTrue(run.TakePerk(offers[1]));
            Assert.IsFalse(run.TakePerk(offers[0]), "one pick only");
            Assert.AreEqual(1, run.Perks.Owned.Count);
            Assert.AreEqual(0, run.PerkOffers().Count);
        }

        [Test]
        public void NoPerkDraftDuringAnAnte()
        {
            var run = new AnteRun(new AnteBalance(), 9);
            Assert.AreEqual(0, run.PerkOffers().Count);
        }

        [Test]
        public void HeldPerksSurviveSaveAndLoad()
        {
            var balance = new AnteBalance { anteTargets = new[] { 250, 250, 250 } };
            AnteRun run = ClearedFirstAnte(9);
            PerkDefinition taken = run.PerkOffers()[0];
            run.TakePerk(taken);
            AnteRun loaded = AnteRun.Import(balance, run.Export());
            Assert.AreEqual(1, loaded.Perks.Owned.Count);
            Assert.AreEqual(taken.id, loaded.Perks.Owned[0].id);
            Assert.IsFalse(loaded.Director.PerkDraftPending);
        }

        [Test]
        public void PerkStackingIsCappedInBothDirections()
        {
            var balance = new AnteBalance();
            var loadout = new PerkLoadout();
            for (int i = 0; i < 6; i++)
            {
                loadout.Add(new PerkDefinition
                {
                    id = "stack" + i, unique = false,
                    effects = new[] { new PerkEffect(PerkStat.WeaponDamage, 0.20f), new PerkEffect(PerkStat.MaxHp, -0.15f) },
                });
            }
            Assert.AreEqual(0.40f, loadout.StatTotal(PerkStat.WeaponDamage, balance), 0.0001f);
            Assert.AreEqual(-0.40f, loadout.StatTotal(PerkStat.MaxHp, balance), 0.0001f);
            Assert.AreEqual(0f, loadout.StatTotal(PerkStat.EvaLateralThrust, balance));
        }

        [Test]
        public void EveryDefaultPerkSaysWhatItDoesAndHasATag()
        {
            var ids = new HashSet<string>();
            foreach (PerkDefinition perk in PerkCatalog.CreateDefaults())
            {
                Assert.IsTrue(ids.Add(perk.id), "duplicate id " + perk.id);
                Assert.IsNotEmpty(perk.Describe(), perk.id);
                Assert.Greater(perk.tags.Length, 0, perk.id);
            }
            Assert.AreEqual("+20% weapon damage, -15% max HP", PerkCatalog.CreateDefaults()[2].Describe());
        }

        // ---- Scratch-off ----

        [Test]
        public void TheOddsShownAreTheHandoffOdds()
        {
            var balance = new AnteBalance();
            Assert.AreEqual(50f, ScratchOff.OddsPercent(balance, ScratchOutcome.SmallCash), 0.001f);
            Assert.AreEqual(25f, ScratchOff.OddsPercent(balance, ScratchOutcome.BigCash), 0.001f);
            Assert.AreEqual(15f, ScratchOff.OddsPercent(balance, ScratchOutcome.RepairRefillVoucher), 0.001f);
            Assert.AreEqual(8f, ScratchOff.OddsPercent(balance, ScratchOutcome.TemporaryTacticalPerk), 0.001f);
            Assert.AreEqual(2f, ScratchOff.OddsPercent(balance, ScratchOutcome.CosmeticToken), 0.001f);
        }

        [Test]
        public void ScratchRollsFollowTheOddsShown()
        {
            var balance = new AnteBalance();
            var rng = new SeededRandom(1113);
            int[] counts = new int[5];
            const int rolls = 200000;
            for (int i = 0; i < rolls; i++) counts[(int)ScratchOff.Roll(rng, balance)]++;
            Assert.AreEqual(0.50, counts[0] / (double)rolls, 0.01);
            Assert.AreEqual(0.25, counts[1] / (double)rolls, 0.01);
            Assert.AreEqual(0.15, counts[2] / (double)rolls, 0.01);
            Assert.AreEqual(0.08, counts[3] / (double)rolls, 0.005);
            Assert.AreEqual(0.02, counts[4] / (double)rolls, 0.003);
        }

        [Test]
        public void ScratchCashGoesToTheWalletOnly()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                AnteRun run = ClearedFirstAnte(seed);
                ScratchOutcome? outcome = run.UseScratchOff();
                Assert.IsTrue(outcome.HasValue);
                Assert.AreEqual(550, run.Ledger.LifetimeRunScore);
                Assert.AreEqual(550, run.Ledger.AnteEarned);
                int expected = outcome == ScratchOutcome.SmallCash ? 150 : (outcome == ScratchOutcome.BigCash ? 300 : 0);
                Assert.AreEqual(550 + expected, run.Ledger.SpendablePoints);
                Assert.AreEqual(outcome == ScratchOutcome.RepairRefillVoucher ? 1 : 0, run.RepairRefillVouchers);
                Assert.AreEqual(outcome == ScratchOutcome.TemporaryTacticalPerk ? 1 : 0, run.TemporaryTacticalPerks);
                Assert.AreEqual(outcome == ScratchOutcome.CosmeticToken ? 1 : 0, run.CosmeticTokens);
            }
        }

        [Test]
        public void OneScratchOffPerClearedAnteAndNoneFromATicket()
        {
            AnteRun run = ClearedFirstAnte(3);
            Assert.IsTrue(run.UseScratchOff().HasValue);
            Assert.IsFalse(run.UseScratchOff().HasValue);
            foreach (string name in System.Enum.GetNames(typeof(ScratchOutcome)))
                Assert.IsFalse(name.ToLowerInvariant().Contains("scratch") || name.ToLowerInvariant().Contains("ticket"), name);
        }

        [Test]
        public void ReloadingCannotRerollAScratchOff()
        {
            var balance = new AnteBalance { anteTargets = new[] { 250, 250, 250 } };
            for (int seed = 0; seed < 50; seed++)
            {
                AnteRun run = ClearedFirstAnte(seed);
                AnteRunState before = run.Export();
                ScratchOutcome? first = run.UseScratchOff();
                ScratchOutcome? again = AnteRun.Import(balance, before).UseScratchOff();
                Assert.AreEqual(first, again);
            }
        }

        [Test]
        public void DraftAndScratchStreamsAreIndependent()
        {
            var balance = new AnteBalance();
            ScratchOutcome alone = ScratchOff.Roll(ScratchOff.StreamFor(42, 3), balance);
            SeededRandom draft = PerkDraft.StreamFor(42, 3);
            for (int i = 0; i < 100; i++) draft.Next();
            Assert.AreEqual(alone, ScratchOff.Roll(ScratchOff.StreamFor(42, 3), balance));
        }

        [Test]
        public void TheLastAnteGivesNoScratchOffUnlessTheSettingSaysSo()
        {
            foreach (FinalScratchTiming timing in new[] { FinalScratchTiming.BetweenAntesOnly, FinalScratchTiming.AlsoAtVictory })
            {
                var run = new AnteRun(new AnteBalance { anteTargets = new[] { 250 }, finalScratch = timing }, 1);
                run.Report(new ScoreEvent { claimId = "m", kind = ScoreEventKind.OptionalMission, missionBasePoints = 250 });
                run.Director.ConfirmAdvance();
                Assert.AreEqual(AntePhase.Victory, run.Director.Phase);
                Assert.AreEqual(timing == FinalScratchTiming.AlsoAtVictory, run.UseScratchOff().HasValue, timing.ToString());
                Assert.AreEqual(250, run.Ledger.LifetimeRunScore, "a victory ticket still cannot change the leaderboard score");
            }
        }

        [Test]
        public void UnusedFreePicksAreGivenUpWhenTheNextAnteStarts()
        {
            AnteRun run = ClearedFirstAnte(3);
            run.BeginNextAnte();
            Assert.IsFalse(run.UseScratchOff().HasValue);
            Assert.AreEqual(0, run.PerkOffers().Count);
        }
    }
}
