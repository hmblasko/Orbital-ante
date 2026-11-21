using System;
using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Ante;
using OrbitalAnte.Core;
using OrbitalAnte.Items;
using OrbitalAnte.Save;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>Rules of the prize envelopes: the odds, the roll, the collection, the save and the Ante 9 lock.</summary>
    public sealed class EnvelopeRulesTests
    {
        static List<ItemSpec> Catalog() => ItemCatalog.CreateDefaults();
        static List<ItemEffect> Effects() => ItemCatalog.CreateEffects();

        // ------------------------------------------------------------------ catalog

        [Test]
        public void TheBuiltInCatalog_IsValid_WithUniqueIds()
        {
            var ids = new HashSet<string>();
            foreach (ItemSpec item in Catalog())
            {
                var problems = new List<string>();
                item.Validate(problems);
                Assert.IsEmpty(problems, item.id + ": " + string.Join(" ", problems));
                Assert.IsTrue(ids.Add(item.id), "two items are called " + item.id);
            }
        }

        [Test]
        public void EveryCategory_AndEveryRarityBelowCelestial_HasItems()
        {
            List<ItemSpec> catalog = Catalog();
            for (int c = 0; c < ItemLook.CategoryCount; c++)
                Assert.IsTrue(catalog.Exists(i => (int)i.category == c), "no " + (ItemCategory)c);
            for (int r = 0; r < (int)ItemRarity.Celestial; r++)
                Assert.IsTrue(catalog.Exists(i => (int)i.rarity == r), "no " + (ItemRarity)r + " items");
            Assert.IsFalse(catalog.Exists(i => i.rarity == ItemRarity.Celestial), "Celestial comes from an effect, never from the item");
        }

        [Test]
        public void EveryUpgrade_IsASidegrade()
        {
            foreach (ItemSpec item in Catalog())
            {
                if (item.category != ItemCategory.Upgrade) continue;
                Assert.Greater(item.upgradeUpAmount, 0f, item.id);
                Assert.Greater(item.upgradeDownAmount, 0f, item.id + " has no downside");
                Assert.AreNotEqual(item.upgradeUp, item.upgradeDown, item.id);
            }
        }

        [Test]
        public void AnItemWithoutADownside_IsReported()
        {
            var item = new ItemSpec { id = "X", displayName = "X", category = ItemCategory.Upgrade, upgradeUp = UpgradeStat.GunDamage, upgradeUpAmount = 0.2f };
            var problems = new List<string>();
            item.Validate(problems);
            Assert.IsNotEmpty(problems);
        }

        // ------------------------------------------------------------------ odds

        [Test]
        public void TheOdds_AddUpTo100_AndPrintCleanly()
        {
            var odds = new PrizeOdds();
            float sum = 0f;
            for (int r = 0; r < ItemLook.RarityCount; r++) sum += odds.Percent((ItemRarity)r);
            Assert.AreEqual(100f, sum, 0.001f);
            Assert.AreEqual("40%", odds.PercentText(ItemRarity.Common));
            Assert.AreEqual("3.5%", odds.PercentText(ItemRarity.Legendary));
            Assert.AreEqual("0.5%", odds.PercentText(ItemRarity.Celestial));
        }

        [Test]
        public void RarerTiers_AreNeverMoreLikely_ThanCommonerOnes()
        {
            var odds = new PrizeOdds();
            for (int r = 1; r < ItemLook.RarityCount; r++)
                Assert.LessOrEqual(odds.Weight((ItemRarity)r), odds.Weight((ItemRarity)(r - 1)));
        }

        [Test]
        public void TheRarityRoll_MatchesThePrintedOdds()
        {
            var odds = new PrizeOdds();
            var rng = new SeededRandom(1234);
            const int draws = 400000;
            var counts = new int[ItemLook.RarityCount];
            for (int i = 0; i < draws; i++) counts[(int)PrizeRoll.RollRarity(rng, odds)]++;
            for (int r = 0; r < ItemLook.RarityCount; r++)
            {
                double p = odds.Percent((ItemRarity)r) / 100.0;
                double sd = Math.Sqrt(p * (1 - p) / draws);
                Assert.AreEqual(p, counts[r] / (double)draws, 5 * sd + 1e-4, (ItemRarity)r + " drifts from its printed odds");
            }
        }

        [Test]
        public void ARarityWithNoWeight_NeverComesUp()
        {
            var odds = new PrizeOdds { celestial = 0f, legendary = 0f };
            var rng = new SeededRandom(9);
            for (int i = 0; i < 20000; i++)
            {
                ItemRarity r = PrizeRoll.RollRarity(rng, odds);
                Assert.Less((int)r, (int)ItemRarity.Legendary);
            }
        }

        // ------------------------------------------------------------------ the roll

        [Test]
        public void ThePrize_DependsOnlyOnTheSaltAndTheEnvelopeNumber()
        {
            var catalog = Catalog();
            var effects = Effects();
            Prize a = PrizeRoll.Roll(PrizeRoll.StreamFor(77, 5), new PrizeOdds(), catalog, effects, null);
            Prize b = PrizeRoll.Roll(PrizeRoll.StreamFor(77, 5), new PrizeOdds(), catalog, effects, null);
            Assert.AreEqual(a.itemId, b.itemId);
            Assert.AreEqual(a.effectId, b.effectId);

            var seen = new HashSet<string>();
            for (int serial = 1; serial <= 40; serial++)
                seen.Add(PrizeRoll.Roll(PrizeRoll.StreamFor(77, serial), new PrizeOdds(), catalog, effects, null).itemId);
            Assert.Greater(seen.Count, 8, "forty envelopes should not all hold the same few items");
        }

        [Test]
        public void ACelestialPrize_AlwaysCarriesAnEffect_OnAnItemThatCanHaveOne()
        {
            var odds = new PrizeOdds { common = 0, uncommon = 0, rare = 0, exotic = 0, legendary = 0, celestial = 1 };
            var catalog = Catalog();
            for (int serial = 1; serial < 200; serial++)
            {
                Prize p = PrizeRoll.Roll(PrizeRoll.StreamFor(3, serial), odds, catalog, Effects(), null);
                Assert.AreEqual(ItemRarity.Celestial, p.rarity);
                Assert.IsNotEmpty(p.effectId);
                Assert.IsTrue(catalog.Find(i => i.id == p.itemId).CanCarryEffect, p.itemId + " cannot carry an effect");
            }
        }

        [Test]
        public void AnOrdinaryPrize_HasNoEffect_AndItsItemsOwnRarity()
        {
            var catalog = Catalog();
            var odds = new PrizeOdds { celestial = 0 };
            for (int serial = 1; serial < 300; serial++)
            {
                Prize p = PrizeRoll.Roll(PrizeRoll.StreamFor(11, serial), odds, catalog, Effects(), null);
                Assert.IsEmpty(p.effectId);
                Assert.AreEqual(catalog.Find(i => i.id == p.itemId).rarity, p.rarity);
            }
        }

        [Test]
        public void TheRoll_PrefersItemsNotOwnedYet()
        {
            var catalog = Catalog();
            var odds = new PrizeOdds { common = 1, uncommon = 0, rare = 0, exotic = 0, legendary = 0, celestial = 0 };
            List<ItemSpec> commons = catalog.FindAll(i => i.rarity == ItemRarity.Common);
            var inventory = new ItemInventory(new List<OwnedItem>());
            for (int i = 1; i < commons.Count; i++) inventory.Add(new Prize { itemId = commons[i].id, effectId = "" }, i, false);
            for (int serial = 1; serial < 50; serial++)
            {
                Prize p = PrizeRoll.Roll(PrizeRoll.StreamFor(5, serial), odds, catalog, Effects(), inventory);
                Assert.AreEqual(commons[0].id, p.itemId);
                Assert.IsFalse(p.duplicate);
            }
        }

        [Test]
        public void WhenEverythingOfARarityIsOwned_ADuplicateComes_AndSaysSo()
        {
            var catalog = Catalog();
            var odds = new PrizeOdds { common = 1, uncommon = 0, rare = 0, exotic = 0, legendary = 0, celestial = 0 };
            var inventory = new ItemInventory(new List<OwnedItem>());
            foreach (ItemSpec s in catalog.FindAll(i => i.rarity == ItemRarity.Common)) inventory.Add(new Prize { itemId = s.id, effectId = "" }, 1, false);
            Prize p = PrizeRoll.Roll(PrizeRoll.StreamFor(5, 1), odds, catalog, Effects(), inventory);
            Assert.IsTrue(p.duplicate);
            Assert.AreEqual(ItemRarity.Common, p.rarity);
        }

        [Test]
        public void AnEmptyRarity_GivesWayToTheNearestOneBelow_ThenAbove()
        {
            var catalog = new List<ItemSpec>
            {
                new ItemSpec { id = "A", displayName = "A", rarity = ItemRarity.Uncommon, category = ItemCategory.ShipPaint },
                new ItemSpec { id = "B", displayName = "B", rarity = ItemRarity.Exotic, category = ItemCategory.ShipPaint },
            };
            Assert.AreEqual(ItemRarity.Uncommon, PrizeRoll.NearestStocked(catalog, ItemRarity.Rare));
            Assert.AreEqual(ItemRarity.Exotic, PrizeRoll.NearestStocked(catalog, ItemRarity.Legendary));
            Assert.AreEqual(ItemRarity.Uncommon, PrizeRoll.NearestStocked(catalog, ItemRarity.Common));
        }

        [Test]
        public void AnEmptyCatalog_GivesNoPrize()
        {
            Prize p = PrizeRoll.Roll(PrizeRoll.StreamFor(1, 1), new PrizeOdds(), new List<ItemSpec>(), Effects(), null);
            Assert.IsFalse(p.IsValid);
        }

        // ------------------------------------------------------------------ the book and the save

        static SaveData FreshSave()
        {
            var data = new SaveData();
            new EnvelopeBook(data).EnsureSalt(424242);
            return data;
        }

        [Test]
        public void EnvelopeNumbers_CountUp_AndAreNeverReused()
        {
            var book = new EnvelopeBook(FreshSave());
            Assert.AreEqual(1, book.SealNext());
            Assert.AreEqual(2, book.SealNext());
            Assert.AreEqual(3, RunEnvelope.Seal(book).Serial);
        }

        [Test]
        public void TheSalt_IsSetOnce()
        {
            var data = new SaveData();
            var book = new EnvelopeBook(data);
            book.EnsureSalt(5);
            book.EnsureSalt(9);
            Assert.AreEqual(5, data.profileSalt);
        }

        [Test]
        public void OpeningAnEnvelope_AddsThePrize_AndUsesTheEnvelopeUp()
        {
            SaveData data = FreshSave();
            var book = new EnvelopeBook(data);
            EnvelopeTicket ticket = book.Keep(book.SealNext(), false);
            Prize peek = book.Peek(ticket, new PrizeOdds(), Catalog(), Effects());
            Prize prize = book.Open(ticket, new PrizeOdds(), Catalog(), Effects());
            Assert.IsTrue(prize.IsValid);
            Assert.AreEqual(peek.itemId, prize.itemId, "opening gives what was inside, not a new roll");
            Assert.AreEqual(0, data.unopenedEnvelopes.Count);
            Assert.AreEqual(1, data.envelopesOpened);
            Assert.IsTrue(book.Inventory.Has(prize.itemId, prize.effectId));
            Assert.IsFalse(book.Open(ticket, new PrizeOdds(), Catalog(), Effects()).IsValid, "an envelope opens once");
        }

        [Test]
        public void KeepingTheSameEnvelopeTwice_KeepsItOnce()
        {
            var book = new EnvelopeBook(FreshSave());
            book.Keep(7, false);
            book.Keep(7, false);
            Assert.AreEqual(1, book.Unopened.Count);
        }

        [Test]
        public void ADuplicate_RaisesTheCount_OnOneLine()
        {
            var inv = new ItemInventory(new List<OwnedItem>());
            inv.Add(new Prize { itemId = "X", effectId = "" }, 1, false);
            inv.Add(new Prize { itemId = "X", effectId = "" }, 2, false);
            inv.Add(new Prize { itemId = "X", effectId = "Aurora" }, 3, false);
            Assert.AreEqual(2, inv.Lines, "an item with an effect is its own line");
            Assert.AreEqual(2, inv.Find("X", "").count);
            Assert.AreEqual(3, inv.TotalCount);
        }

        [Test]
        public void ClearingTests_RemovesOnlyTestThings()
        {
            SaveData data = FreshSave();
            var book = new EnvelopeBook(data);
            book.KeepTestEnvelope();
            book.Keep(book.SealNext(), false);
            book.Inventory.Add(new Prize { itemId = "Real", effectId = "" }, 1, false);
            book.Inventory.Add(new Prize { itemId = "Test", effectId = "" }, 2, true);
            // A test item later won for real stays.
            book.Inventory.Add(new Prize { itemId = "Both", effectId = "" }, 3, true);
            book.Inventory.Add(new Prize { itemId = "Both", effectId = "" }, 4, false);
            book.ClearTests();
            Assert.AreEqual(1, data.unopenedEnvelopes.Count);
            Assert.IsFalse(data.unopenedEnvelopes[0].fromTest);
            Assert.IsTrue(book.Inventory.Has("Real", ""));
            Assert.IsTrue(book.Inventory.Has("Both", ""));
            Assert.IsFalse(book.Inventory.Has("Test", ""));
        }

        [Test]
        public void AVersion2Save_IsBroughtUpTo3_WithAnEmptyCollection()
        {
            var data = new SaveData { version = 2, runsStarted = 12, unopenedEnvelopes = null, items = null };
            Assert.IsTrue(SaveMigrations.Upgrade(data));
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(12, data.runsStarted);
            Assert.IsNotNull(data.items);
            Assert.IsNotNull(data.unopenedEnvelopes);
            Assert.AreEqual(0, data.items.Count);
        }

        [Test]
        public void AVersion1Save_IsBroughtUpTo3()
        {
            var data = new SaveData { version = 1, lastScenario = 1 };
            Assert.IsTrue(SaveMigrations.Upgrade(data));
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(0, data.lastScenario);
        }

        // ------------------------------------------------------------------ the Ante 9 lock

        [Test]
        public void TheEnvelope_StaysSealed_UntilAnte9IsCleared()
        {
            SaveData data = FreshSave();
            var book = new EnvelopeBook(data);
            RunEnvelope run = RunEnvelope.Seal(book);
            int unlocks = 0;
            run.Unlocked += _ => unlocks++;
            for (int ante = 1; ante < 9; ante++)
            {
                Assert.IsFalse(run.OnAnteCleared(ante, book));
                Assert.AreEqual(EnvelopeState.Sealed, run.State);
                Assert.AreEqual(ante, run.AntesCleared);
            }
            Assert.AreEqual(0, data.unopenedEnvelopes.Count);
            Assert.IsTrue(run.OnAnteCleared(9, book));
            Assert.AreEqual(EnvelopeState.Unlocked, run.State);
            Assert.IsFalse(run.OnAnteCleared(9, book), "it unlocks once");
            Assert.AreEqual(1, unlocks);
            Assert.AreEqual(1, data.unopenedEnvelopes.Count);
            Assert.AreEqual(run.Serial, data.unopenedEnvelopes[0].serial);
        }

        [Test]
        public void EndingTheRunEarly_LosesTheEnvelope_ButAWonOneIsKept()
        {
            SaveData data = FreshSave();
            var book = new EnvelopeBook(data);
            RunEnvelope lost = RunEnvelope.Seal(book);
            lost.OnAnteCleared(4, book);
            lost.OnRunEnded();
            Assert.AreEqual(EnvelopeState.Lost, lost.State);
            Assert.IsFalse(lost.OnAnteCleared(9, book), "a lost envelope cannot be won back");

            RunEnvelope won = RunEnvelope.Seal(book);
            won.OnAnteCleared(9, book);
            won.OnRunEnded();
            Assert.AreEqual(EnvelopeState.Unlocked, won.State);
            Assert.AreEqual(1, data.unopenedEnvelopes.Count);
        }

        [Test]
        public void FollowingTheAnteDirector_UnlocksAtVictory_NotAtEarlierAntes()
        {
            SaveData data = FreshSave();
            var book = new EnvelopeBook(data);
            RunEnvelope run = RunEnvelope.Seal(book);
            var balance = new AnteBalance();
            var director = new AnteDirector(balance, new RunLedger());
            run.Follow(director, book);
            director.Restore(5, AntePhase.Intermission, 0f, 0f, 100f, true, true);
            Assert.AreEqual(EnvelopeState.Sealed, run.State);
            Assert.AreEqual(5, run.AntesCleared);
            director.Restore(9, AntePhase.Failed, 0f, 0f, 100f, false, false);
            Assert.AreEqual(EnvelopeState.Sealed, run.State, "failing Ante 9 does not open it");
            director.Restore(9, AntePhase.Victory, 0f, 0f, 100f, false, false);
            Assert.AreEqual(EnvelopeState.Unlocked, run.State);
        }

        [Test]
        public void TheUnlockAnte_IsTheLastAnteOfTheDefaultBalance()
        {
            Assert.AreEqual(new AnteBalance().AnteCount, RunEnvelope.UnlockAnte,
                "the envelope opens after Ante 9, which is the last ante; if the ante count changes, ask Matthew which ante opens it");
        }
    }
}
