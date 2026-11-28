using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Ante;
using OrbitalAnte.Items;
using OrbitalAnte.Save;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// The Locker (Ralph loop pass 4, 28 Nov 2026; Items/Locker.cs, Items/CollectionRows.cs): where every prize goes
    /// to work, that only an owned prize can be in use, that one place holds one prize (skins: one for every gun and
    /// one per gun), that a won prize goes to work by itself only where nothing is in its place and never as an
    /// upgrade, that the save keeps all of it, and the words the Collection and the reveal show. Plain C#.
    /// </summary>
    public class LockerTests
    {
        static readonly List<ItemSpec> Catalog = ItemCatalog.CreateDefaults();

        static ItemSpec Find(string id) => Catalog.Find(i => i.id == id);

        static SaveData Owning(params string[] ids)
        {
            var save = new SaveData();
            var owned = new ItemInventory(save.items);
            int serial = 1;
            foreach (string id in ids) owned.Add(new Prize { itemId = id, effectId = "" }, serial++, false);
            return save;
        }

        // ------------------------------------------------------------------ places

        [Test]
        public void EveryPrize_ButAPainting_HasAPlaceToGoToWork()
        {
            foreach (ItemSpec item in Catalog)
            {
                LockerSlot slot = Locker.SlotOf(item);
                if (item.category == ItemCategory.Artwork) Assert.AreEqual(LockerSlot.None, slot, item.id + ": paintings wait for the cabin's frames");
                else Assert.AreNotEqual(LockerSlot.None, slot, item.id + " has nowhere to go to work");
                Assert.IsNotEmpty(Locker.DoesWords(item), item.id);
                if (slot != LockerSlot.None) Assert.IsNotEmpty(Locker.WhereWords(item), item.id);
            }
            Assert.AreEqual(LockerSlot.Helmet, Locker.SlotOf(Find("HelmetFishbowl")));
            Assert.AreEqual(LockerSlot.Badge, Locker.SlotOf(Find("BadgeMissionPatch")));
            Assert.AreEqual(LockerSlot.Pack, Locker.SlotOf(Find("PackRacingStripes")));
            Assert.AreEqual(LockerSlot.None, Locker.SlotOf(null));
        }

        [Test]
        public void OnlyAnOwnedPrize_CanBePutToUse_WithTheEffectItWasWonWith()
        {
            SaveData save = Owning("HelmetFishbowl");
            Assert.IsFalse(Locker.Use(save, Find("HelmetCrown"), ""), "not owned");
            Assert.IsFalse(Locker.Use(save, Find("HelmetFishbowl"), "Aurora"), "owned, but not with that effect");
            Assert.IsTrue(Locker.Use(save, Find("HelmetFishbowl"), ""));
            Assert.IsTrue(Locker.InUse(save, Find("HelmetFishbowl"), ""));
            Assert.IsFalse(Locker.InUse(save, Find("HelmetFishbowl"), "Aurora"), "a line is the item and its effect together");
            Assert.IsFalse(Locker.Use(save, Find("ArtStarryNight"), ""), "a painting has no place yet");
        }

        [Test]
        public void PuttingAPrizeToUse_TakesOffWhateverWasInItsPlace_AndNothingElse()
        {
            SaveData save = Owning("HelmetFishbowl", "HelmetCrown", "VisorSunset");
            Locker.Use(save, Find("HelmetFishbowl"), "");
            Locker.Use(save, Find("VisorSunset"), "");
            Locker.Use(save, Find("HelmetCrown"), "");
            Assert.IsFalse(Locker.InUse(save, Find("HelmetFishbowl"), ""), "one helmet at a time");
            Assert.IsTrue(Locker.InUse(save, Find("HelmetCrown"), ""));
            Assert.IsTrue(Locker.InUse(save, Find("VisorSunset"), ""), "the visor is a place of its own");
        }

        [Test]
        public void OneUpgradeAtATime()
        {
            SaveData save = Owning("UpgradeBigLungs", "UpgradeHotLoads");
            Locker.Use(save, Find("UpgradeBigLungs"), "");
            Locker.Use(save, Find("UpgradeHotLoads"), "");
            Assert.AreEqual("UpgradeHotLoads", Locker.UpgradeInUse(save, Find).id);
            Assert.IsFalse(Locker.InUse(save, Find("UpgradeBigLungs"), ""));
        }

        [Test]
        public void AGunsOwnSkin_AndTheSkinForEveryGun_AreKeptApart()
        {
            SaveData save = Owning("SkinHazard", "SkinWoodPanel", "SkinPistolPearl", "SkinShotgunCandy");
            Locker.Use(save, Find("SkinHazard"), "");
            Locker.Use(save, Find("SkinPistolPearl"), "");
            Locker.Use(save, Find("SkinShotgunCandy"), "");
            Assert.AreEqual("SkinPistolPearl", Locker.SkinFor(save, "Pistol").itemId, "a gun's own skin wins on that gun");
            Assert.AreEqual("SkinShotgunCandy", Locker.SkinFor(save, "Shotgun").itemId);
            Assert.AreEqual("SkinHazard", Locker.SkinFor(save, "Rifle").itemId, "every other gun wears the skin for every gun");
            Assert.AreEqual("SkinHazard", Locker.SkinFor(save, "SmgA").itemId, "a gun taken off an enemy too");
            Assert.AreEqual("SkinHazard", Locker.SkinFor(save, null).itemId);

            Locker.Use(save, Find("SkinWoodPanel"), "");
            Assert.AreEqual("SkinWoodPanel", Locker.SkinFor(save, "Rifle").itemId, "one skin for every gun at a time");
            Assert.AreEqual("SkinPistolPearl", Locker.SkinFor(save, "Pistol").itemId, "and it leaves a gun's own skin where it is");
            Assert.AreEqual(3, Locker.Of(save).skins.Count);

            Locker.TakeOff(save, Find("SkinWoodPanel"), "");
            Assert.IsNull(Locker.SkinFor(save, "Rifle"));
            Assert.AreEqual("SkinPistolPearl", Locker.SkinFor(save, "Pistol").itemId);
        }

        [Test]
        public void AWonPrize_GoesToWorkByItself_OnlyWhereItsPlaceIsEmpty_AndNeverAsAnUpgrade()
        {
            SaveData save = Owning("PaintMoonDust", "PaintSafetyOrange", "UpgradeBigLungs", "SkinPistolPearl", "ArtHaywain");
            Assert.IsTrue(Locker.AutoUse(save, Find("PaintMoonDust"), ""));
            Assert.IsFalse(Locker.AutoUse(save, Find("PaintSafetyOrange"), ""), "never over something already in use");
            Assert.IsTrue(Locker.InUse(save, Find("PaintMoonDust"), ""));
            Assert.IsFalse(Locker.AutoUse(save, Find("UpgradeBigLungs"), ""), "every upgrade has a cost: taking one on is the player's choice");
            Assert.IsNull(Locker.UpgradeInUse(save, Find));
            Assert.IsTrue(Locker.AutoUse(save, Find("SkinPistolPearl"), ""));
            Assert.IsFalse(Locker.AutoUse(save, Find("ArtHaywain"), ""));
            Assert.IsFalse(Locker.AutoUse(save, null, ""));
        }

        [Test]
        public void AnEnvelopesPrize_TakesAnEarnedLooksPlace_ButAnEarnedLookNeverTakesAPrizes()
        {
            // The earned looks fill their places early; the ante 9 reveal must still go to work (Unity review, pass 5).
            SaveData save = Owning("PaintMoonDust", "SkinHazard", "VisorSunset");
            UnlockBook.Grant(save, UnlockGoal.LandAwayFromEarth);
            UnlockBook.Grant(save, UnlockGoal.Disarm);
            Assert.AreEqual("PaintRollPattern", Locker.Worn(save, LockerSlot.Paint).itemId, "the earned paint went on the empty lander");
            Assert.AreEqual("Roll Pattern", Locker.InPlaceOf(save, Find("PaintMoonDust"), Find), "what the reveal names");
            Assert.IsTrue(Locker.AutoUse(save, Find("PaintMoonDust"), ""), "the envelope's paint goes to work");
            Assert.AreEqual("PaintMoonDust", Locker.Worn(save, LockerSlot.Paint).itemId);
            Assert.IsTrue(new ItemInventory(save.items).Has("PaintRollPattern", ""), "the earned look stays in the collection");
            Assert.IsTrue(Locker.AutoUse(save, Find("SkinHazard"), ""), "and a skin for every gun takes the earned one's place");
            Assert.AreEqual("SkinHazard", Locker.SkinFor(save, "Rifle").itemId);

            // The other way round: what the player put to use is never replaced by a look earned later.
            Assert.IsTrue(Locker.Use(save, Find("VisorSunset"), ""));
            UnlockBook.Grant(save, UnlockGoal.FocusKill);
            Assert.AreEqual("VisorSunset", Locker.Worn(save, LockerSlot.Visor).itemId);
            Assert.IsFalse(Locker.AutoUse(save, Find("PaintRollPattern"), ""), "an earned look never takes an envelope prize's place");
            Assert.AreEqual("PaintMoonDust", Locker.Worn(save, LockerSlot.Paint).itemId);
        }

        [Test]
        public void Toggle_PutsOnAndTakesOff()
        {
            SaveData save = Owning("VisorSunset");
            Assert.IsTrue(Locker.Toggle(save, Find("VisorSunset"), ""));
            Assert.IsTrue(Locker.InUse(save, Find("VisorSunset"), ""));
            Assert.IsFalse(Locker.Toggle(save, Find("VisorSunset"), ""));
            Assert.IsNull(Locker.Worn(save, LockerSlot.Visor));
        }

        [Test]
        public void Tidy_TakesOutOfUse_WhatIsNoLongerOwned_KnownOrFitting()
        {
            SaveData save = Owning("PaintMoonDust", "HelmetCrown", "SkinHazard", "UpgradeBigLungs");
            Locker.Use(save, Find("PaintMoonDust"), "");
            Locker.Use(save, Find("HelmetCrown"), "");
            Locker.Use(save, Find("SkinHazard"), "");
            Locker.Use(save, Find("UpgradeBigLungs"), "");
            Assert.AreEqual(0, Locker.Tidy(save, Find), "nothing to tidy");

            // Test items cleared: the paint is no longer owned.
            save.items.RemoveAll(i => i.itemId == "PaintMoonDust");
            // An asset changed in the Inspector: the crown is now a visor.
            ItemSpec changed = Find("HelmetCrown").Copy();
            changed.slot = CosmeticSlot.Visor;
            // An asset removed: the skin is no longer in the game.
            System.Func<string, ItemSpec> find = id => id == "HelmetCrown" ? changed : id == "SkinHazard" ? null : Find(id);
            Assert.AreEqual(3, Locker.Tidy(save, find));
            Assert.IsNull(Locker.Worn(save, LockerSlot.Paint));
            Assert.IsNull(Locker.Worn(save, LockerSlot.Helmet));
            Assert.IsEmpty(Locker.Of(save).skins);
            Assert.IsNotNull(Locker.UpgradeInUse(save, Find), "what still fits stays in use");
        }

        // ------------------------------------------------------------------ the save

        [Test]
        public void AVersion5Save_IsBroughtUpToDate_WithNothingInUse_AndItsPrizesKept()
        {
            SaveData data = Owning("PaintMoonDust", "SkinHazard");
            data.version = 5;
            data.locker = null;
            Assert.IsTrue(SaveMigrations.Upgrade(data));
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.IsNotNull(data.locker);
            Assert.IsNull(Locker.Worn(data, LockerSlot.Paint), "an older save had nothing in use");
            Assert.IsEmpty(data.locker.skins);
            Assert.AreEqual(2, data.items.Count, "the prizes are kept");
        }

        [Test]
        public void ALoadoutWithHoles_IsMended_NotThrownAway()
        {
            var data = new SaveData
            {
                locker = new Loadout
                {
                    upgrade = null, paint = null, helmet = null, visor = null, suit = null, pack = null, badge = null,
                    skins = new List<LockerSkin> { null, new LockerSkin { gunId = null, itemId = "SkinHazard", effectId = null }, new LockerSkin { itemId = "" } },
                },
            };
            Assert.IsTrue(SaveMigrations.Upgrade(data));
            Loadout l = data.locker;
            Assert.IsNotNull(l.upgrade);
            Assert.IsNotNull(l.paint);
            Assert.IsNotNull(l.badge);
            Assert.AreEqual(1, l.skins.Count, "empty and missing skins go");
            Assert.AreEqual("", l.skins[0].gunId);
            Assert.AreEqual("", l.skins[0].effectId);
            Assert.AreEqual(6, SaveData.CurrentVersion, "the Locker is layout 6 (bump this test with the next layout)");
        }

        // ------------------------------------------------------------------ words

        [Test]
        public void TheReveal_SaysWhereAPrizeWentToWork_OrHowToPutItToUse()
        {
            Assert.AreEqual("In use on your lander.", Locker.RevealWords(Find("PaintMoonDust"), true));
            // A look put to use in the middle of a run goes on there and then, so the words never say "next run".
            Assert.AreEqual("In use on every gun.", Locker.RevealWords(Find("SkinHazard"), true));
            Assert.AreEqual("In use on your lander, in place of Roll Pattern (still in your Collection).",
                Locker.RevealWords(Find("PaintMoonDust"), true, null, "Roll Pattern"), "it names the earned look it took the place of");
            Assert.AreEqual("In use on the Test Pistol.", Locker.RevealWords(Find("SkinPistolPearl"), true, "Test Pistol"));
            string upgrade = Locker.RevealWords(Find("UpgradeBigLungs"), false);
            StringAssert.Contains("cost", upgrade);
            StringAssert.Contains("Collection", upgrade);
            StringAssert.Contains("Collection", Locker.RevealWords(Find("HelmetCrown"), false));
            Assert.AreEqual("Your Fishbowl Dome is in its place: swap this in from Collection on the title screen.",
                Locker.RevealWords(Find("HelmetCrown"), false, null, "Fishbowl Dome"), "it names what is in the way");
            Assert.AreEqual("", Locker.RevealWords(Find("ArtStarryNight"), false), "a painting has no place to go yet");
        }

        [Test]
        public void TheCollection_SaysWhatPressingAPrizeDoes()
        {
            SaveData save = Owning("VisorSunset", "HelmetFishbowl", "HelmetCrown");
            Assert.AreEqual("Press to put it to use.", Locker.StateWords(save, Find("HelmetCrown"), "", Find));
            Locker.Use(save, Find("HelmetFishbowl"), "");
            Assert.AreEqual("Press to use it instead of Fishbowl Dome.", Locker.StateWords(save, Find("HelmetCrown"), "", Find));
            Assert.AreEqual("In use. Press to take it off.", Locker.StateWords(save, Find("HelmetFishbowl"), "", Find));
            Assert.AreEqual("", Locker.StateWords(save, Find("ArtHaywain"), "", Find));
        }

        [Test]
        public void AnUpgrade_IsAlwaysNamedWithItsGainAndItsCost()
        {
            Assert.AreEqual("Big Lungs (+25% suit air, -4% walking speed)", Locker.UpgradeWords(Find("UpgradeBigLungs")));
            Assert.AreEqual("Heavy Hands (+45% melee damage, -15% Focus from kills and limbs)", Locker.UpgradeWords(Find("UpgradeHeavyHands")));
            foreach (ItemSpec item in Catalog)
            {
                if (item.category != ItemCategory.Upgrade) continue;
                string does = Locker.DoesWords(item);
                StringAssert.Contains("+", does, item.id);
                StringAssert.Contains("-", does, item.id + ": the cost is said wherever the gain is");
            }
        }

        // ------------------------------------------------------------------ the Collection's rows

        [Test]
        public void TheCollection_IsGroupedByWhereThingsGo_RarestFirst_NewestFirst()
        {
            SaveData save = Owning("ArtStarryNight", "HelmetTapeRepair", "PaintMoonDust", "HelmetCrown", "UpgradeBigLungs", "SkinHazard", "HelmetFishbowl");
            List<OwnedItem> rows = CollectionRows.Sorted(new ItemInventory(save.items), Find);
            var order = rows.ConvertAll(r => r.itemId);
            CollectionAssert.AreEqual(new[] { "UpgradeBigLungs", "PaintMoonDust", "SkinHazard", "HelmetCrown", "HelmetFishbowl", "HelmetTapeRepair", "ArtStarryNight" }, order);
        }

        [Test]
        public void ALineWithAnEffect_IsShownAsCelestial_AndAnUnknownItemIsLeftOut()
        {
            var save = new SaveData();
            var owned = new ItemInventory(save.items);
            owned.Add(new Prize { itemId = "HelmetTapeRepair", effectId = "Aurora" }, 1, false);
            owned.Add(new Prize { itemId = "HelmetCrown", effectId = "" }, 2, false);
            owned.Add(new Prize { itemId = "NoSuchThing", effectId = "" }, 3, false);
            List<OwnedItem> rows = CollectionRows.Sorted(owned, Find);
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("HelmetTapeRepair", rows[0].itemId, "Celestial beats Legendary");
            Assert.AreEqual(ItemRarity.Celestial, CollectionRows.RarityOf(rows[0], Find("HelmetTapeRepair")));
            Assert.AreEqual("Aurora Duct Tape Halo  · helmet", CollectionRows.RowWords(Find("HelmetTapeRepair"), rows[0], "Aurora"));
        }

        [Test]
        public void Pages_HoldSixRows()
        {
            Assert.AreEqual(1, CollectionRows.Pages(0));
            Assert.AreEqual(1, CollectionRows.Pages(6));
            Assert.AreEqual(2, CollectionRows.Pages(7));
            Assert.AreEqual(1, CollectionRows.ClampPage(5, 7));
            Assert.AreEqual(0, CollectionRows.ClampPage(-2, 7));
        }

        [Test]
        public void TheDetails_SayWhatAPrizeDoes_AndWhatPressingItDoes()
        {
            SaveData save = Owning("UpgradeHotLoads", "ArtGreatWave", "SkinRifleStarChart");
            var owned = new ItemInventory(save.items);
            string upgrade = CollectionRows.Details(save, Find("UpgradeHotLoads"), owned.Find("UpgradeHotLoads", ""), "", Find);
            StringAssert.Contains("+10% gun damage and -12% reload speed", upgrade);
            StringAssert.Contains("Press to put it to use", upgrade);
            string painting = CollectionRows.Details(save, Find("ArtGreatWave"), owned.Find("ArtGreatWave", ""), "", Find);
            StringAssert.Contains("cabin wall", painting);
            StringAssert.DoesNotContain("Press", painting, "pressing a painting does nothing yet");
            string celestial = CollectionRows.Details(save, Find("SkinRifleStarChart"), owned.Find("SkinRifleStarChart", ""), "Aurora", Find, "Rifle");
            StringAssert.Contains("Effect: Aurora (not shown yet)", celestial, "an effect is never promised as if it were drawn");
            string skin = CollectionRows.Details(save, Find("SkinRifleStarChart"), owned.Find("SkinRifleStarChart", ""), "", Find, "Rifle");
            StringAssert.Contains("Goes on the Rifle whenever you carry one.", skin);
            Locker.Use(save, Find("SkinRifleStarChart"), "");
            Assert.AreEqual(1, CollectionRows.InUseCount(save, CollectionRows.Sorted(owned, Find), Find));
        }
    }
}
