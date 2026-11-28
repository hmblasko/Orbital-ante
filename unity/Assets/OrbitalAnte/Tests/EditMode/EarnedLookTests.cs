using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Ante;
using OrbitalAnte.Items;
using OrbitalAnte.Save;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>
    /// The looks a goal gives (Ralph loop pass 5): a skin, a paint or a suit piece earned in a run, kept out of the
    /// envelopes so the odds printed on every envelope stay true (constitution rule 9), and named as earned wherever
    /// the collection shows it.
    /// </summary>
    public sealed class EarnedLookTests
    {
        static List<ItemSpec> Catalog() => ItemCatalog.CreateDefaults();

        static List<ItemSpec> WithoutEarned()
        {
            List<ItemSpec> all = Catalog();
            all.RemoveAll(i => ItemCatalog.IsEarned(i.id));
            return all;
        }

        [Test]
        public void EveryEarnedLook_IsInTheGamesCatalogue_AndNoEnvelopeHoldsIt()
        {
            List<ItemSpec> catalog = Catalog();
            int earned = 0;
            foreach (string id in ItemCatalog.EarnedItemIds)
            {
                earned++;
                ItemSpec item = catalog.Find(i => i.id == id);
                Assert.IsNotNull(item, id + " is not made on a new machine");
                Assert.IsNotNull(ItemCatalog.Default(id), id);
                Assert.IsFalse(PrizeRoll.InEnvelopes(item), id);
                Assert.IsTrue(Locker.Usable(item), id + " goes to work somewhere");
            }
            Assert.Greater(earned, 0);
            foreach (ItemSpec item in catalog)
                if (!ItemCatalog.IsEarned(item.id)) Assert.IsTrue(PrizeRoll.InEnvelopes(item), item.id);
            Assert.IsFalse(PrizeRoll.InEnvelopes(null));
        }

        [Test]
        public void TheEnvelopesRoll_IsTheSame_WithOrWithoutTheEarnedLooks_AndNeverGivesOne()
        {
            List<ItemSpec> all = Catalog(), envelopes = WithoutEarned();
            List<ItemEffect> effects = ItemCatalog.CreateEffects();
            var odds = new PrizeOdds();
            var ownedSets = new List<ItemInventory> { null, new ItemInventory(new List<OwnedItem>()) };
            // A collection that holds every earned look and a few envelope prizes: what is owned must not move the roll either.
            var someOwned = new ItemInventory(new List<OwnedItem>());
            foreach (string id in ItemCatalog.EarnedItemIds) someOwned.Add(new Prize { itemId = id, effectId = "" }, 0, false);
            someOwned.Add(new Prize { itemId = "PaintMoonDust", effectId = "" }, 1, false);
            someOwned.Add(new Prize { itemId = "SkinHazard", effectId = "" }, 2, false);
            ownedSets.Add(someOwned);

            var rarities = new HashSet<ItemRarity>();
            foreach (ItemInventory owned in ownedSets)
                for (int serial = 1; serial <= 3000; serial++)
                {
                    Prize a = PrizeRoll.Roll(PrizeRoll.StreamFor(41, serial), odds, all, effects, owned);
                    Prize b = PrizeRoll.Roll(PrizeRoll.StreamFor(41, serial), odds, envelopes, effects, owned);
                    Assert.AreEqual(b.itemId, a.itemId, "envelope " + serial);
                    Assert.AreEqual(b.effectId, a.effectId, "envelope " + serial);
                    Assert.AreEqual(b.rarity, a.rarity, "envelope " + serial);
                    Assert.AreEqual(b.duplicate, a.duplicate, "envelope " + serial);
                    Assert.IsFalse(ItemCatalog.IsEarned(a.itemId), a.itemId + " came out of envelope " + serial);
                    rarities.Add(a.rarity);
                }
            // The earned looks are all Common, so the Common pool is the one they could have spoiled.
            Assert.IsTrue(rarities.Contains(ItemRarity.Common));
            Assert.IsTrue(rarities.Contains(ItemRarity.Celestial), "the Celestial roll ran too");
        }

        [Test]
        public void EarnedLooks_HaveTheirOwnPatternsAndStyles_AndChangeWhatTheySay()
        {
            ItemSpec butter = ItemCatalog.Default("SkinButterfingers"), shark = ItemCatalog.Default("SkinPistolSharkskin");
            // Plain enamel, not stripes: the first look most players earn goes on every gun, so it must not look like a toy.
            Assert.AreEqual(SkinPattern.Paint, PrizeLooks.Skin(butter).Pattern);
            Assert.AreEqual(SkinPattern.Flake, PrizeLooks.Skin(shark).Pattern);
            Assert.AreEqual("Pistol", shark.weaponId);
            Assert.IsTrue(string.IsNullOrEmpty(butter.weaponId), "on every gun");
            Ship.HullPaint roll = PrizeLooks.Paint(ItemCatalog.Default("PaintRollPattern"));
            Assert.IsNotNull(roll);
            Assert.AreEqual(Ship.HullPaintStyle.Band, roll.Style);

            SuitLook visor = PrizeLooks.Suit(null, ItemCatalog.Default("VisorTunnelVision"), null, null, null);
            Assert.IsTrue(visor.Tinted);
            // Smoke grey, never the blue the Focus rim means the slowed world by.
            ItemSpec tunnel = ItemCatalog.Default("VisorTunnelVision");
            Assert.Less(System.Math.Abs(tunnel.colour[2] - tunnel.colour[0]), 0.08f, "no colour cast to read as Focus");
            SuitLook pack = PrizeLooks.Suit(null, null, null, ItemCatalog.Default("PackAfterburner"), null);
            Assert.IsNotNull(pack.Jet);
            SuitLook badge = PrizeLooks.Suit(null, null, null, null, ItemCatalog.Default("BadgeLegDay"));
            Assert.IsTrue(badge.Badge);
            SuitLook helmet = PrizeLooks.Suit(ItemCatalog.Default("HelmetHardHat"), null, null, null, null);
            CollectionAssert.AreNotEqual(SuitLook.StandardShade, helmet.Shade);
            SuitLook suit = PrizeLooks.Suit(null, null, ItemCatalog.Default("SuitBountyHunter"), null, null);
            CollectionAssert.AreNotEqual(SuitLook.StandardFace, suit.PlateFace);
        }

        [Test]
        public void AnEarnedLook_IsNamedAsEarned_InTheCollection()
        {
            ItemSpec item = ItemCatalog.Default("SkinButterfingers");
            var line = new OwnedItem { itemId = item.id, effectId = "", count = 1 };
            Assert.AreEqual("Earned weapon skin (any gun)", ItemLook.KindLine(item, CollectionRows.RarityOf(line, item)));
            CollectionAssert.AreEqual(ItemLook.EarnedColour, CollectionRows.StripeColour(line, item));
            // Said in words on the row too, so the stripe's colour is never the only sign.
            StringAssert.EndsWith("  · earned", CollectionRows.RowWords(item, line, null));
            var save = new SaveData();
            string details = CollectionRows.Details(save, item, line, null, ItemCatalog.Default);
            StringAssert.StartsWith("Earned weapon skin (any gun)\n", details);
            StringAssert.Contains("Earned for a disarm", details);

            // An envelope's prize keeps its rarity's name and colour.
            ItemSpec hazard = ItemCatalog.Default("SkinHazard");
            var prize = new OwnedItem { itemId = hazard.id, effectId = "", count = 1 };
            StringAssert.StartsWith(ItemLook.RarityName(hazard.rarity) + " ", ItemLook.KindLine(hazard, hazard.rarity));
            CollectionAssert.AreEqual(ItemLook.RarityColour(hazard.rarity), CollectionRows.StripeColour(prize, hazard));
            StringAssert.DoesNotContain("Earned for", CollectionRows.Details(save, hazard, prize, null, ItemCatalog.Default));
        }

        [Test]
        public void TheEarnedColour_IsNoRaritysColour_NorTheInUseGreen()
        {
            for (int r = 0; r < ItemLook.RarityCount; r++)
                CollectionAssert.AreNotEqual(ItemLook.RarityColour((ItemRarity)r), ItemLook.EarnedColour, ((ItemRarity)r).ToString());
            // The Collection's IN USE words are the screen's green (HullKit.Good); an earned stripe in it read as "in use".
            CollectionAssert.AreNotEqual(new[] { 0.624f, 0.878f, 0.478f }, ItemLook.EarnedColour);
            for (int r = 0; r < ItemLook.RarityCount; r++)
                Assert.AreNotEqual(ItemLook.EarnedName, ItemLook.RarityName((ItemRarity)r));
        }

        [Test]
        public void AnEmptyCollection_NamesTheNextLookToEarn_BeforeTheEnvelope()
        {
            string words = CollectionRows.EmptyWords(new SaveData());
            StringAssert.Contains("The next: Roll Pattern (ship paint), for a landing away from Earth", words);
            StringAssert.Contains("clear Ante 9 and it opens", words);
            Assert.Less(words.IndexOf("Roll Pattern"), words.IndexOf("Ante 9"), "the near thing first");

            var save = new SaveData();
            UnlockBook.Grant(save, UnlockGoal.LandAwayFromEarth);
            StringAssert.Contains("The next: Butterfingers (gun skin), for a disarm", CollectionRows.EmptyWords(save));

            var done = new SaveData();
            foreach (UnlockDefinition d in UnlockCatalog.All) UnlockBook.Grant(done, d.goal);
            Assert.AreEqual("Nothing yet. Every run also starts with a sealed envelope: clear Ante 9 and it opens.", CollectionRows.EmptyWords(done));
        }

        [Test]
        public void ClearingTestItems_NeverTakesAnEarnedLook()
        {
            var save = new SaveData();
            UnlockBook.Grant(save, UnlockGoal.LegOff);
            var owned = new ItemInventory(save.items);
            owned.Add(new Prize { itemId = "PaintMoonDust", effectId = "" }, 9, true);
            Assert.AreEqual(2, owned.Items.Count);
            Assert.AreEqual(1, new EnvelopeBook(save).ClearTests());
            Locker.Tidy(save, ItemCatalog.Default);
            Assert.IsTrue(owned.Has("BadgeLegDay", ""));
            Assert.IsNotNull(Locker.Worn(save, LockerSlot.Badge));
        }
    }
}
