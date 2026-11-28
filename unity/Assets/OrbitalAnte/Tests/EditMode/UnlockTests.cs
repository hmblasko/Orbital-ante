using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OrbitalAnte.Ante;
using OrbitalAnte.Save;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Goal unlocks and starting kits (Ralph loop pass 1, 27 Nov 2026; Ante/Unlocks.cs): what each goal is, that it
    /// is met by its own feat and nothing else, that locked perks never reach a draft and a new one is shown in the
    /// next, that a kit's perks stand in for the first picks and cost one more, the words the player reads, and that
    /// the save keeps all of it. Plain C#.
    /// </summary>
    public class UnlockTests
    {
        static List<PerkDefinition> Perks => RoguelitePerks.Create();

        static AnteRun NewRun(int seed = 7)
        {
            return new AnteRun(new AnteBalance(), seed, RoguelitePerks.Create());
        }

        static void MeetTarget(AnteRun run, string tag)
        {
            for (int i = 0; run.Ledger.AnteEarned < run.Director.Target; i++)
                run.Report(new ScoreEvent { claimId = "m" + tag + "." + i, kind = ScoreEventKind.OptionalMission, missionBasePoints = 550 });
        }

        static void ClearAnte(AnteRun run)
        {
            MeetTarget(run, run.Director.Ante.ToString());
            Assert.IsTrue(run.Director.ConfirmAdvance());
        }

        static SaveData SaveWith(params UnlockGoal[] goals)
        {
            var save = new SaveData();
            foreach (UnlockGoal g in goals) Assert.IsNotNull(UnlockBook.Grant(save, g), g.ToString());
            return save;
        }

        static SaveData SaveWithEverything()
        {
            var save = new SaveData();
            foreach (UnlockDefinition d in UnlockCatalog.All) UnlockBook.Grant(save, d.goal);
            return save;
        }

        // ---- the catalogue ----

        [Test]
        public void EveryGoal_UnlocksExactlyOneThing_AndEveryUnlockHasAGoal()
        {
            var goals = new HashSet<UnlockGoal>();
            var ids = new HashSet<string>();
            foreach (UnlockDefinition d in UnlockCatalog.All)
            {
                Assert.IsTrue(goals.Add(d.goal), "two unlocks for " + d.goal);
                Assert.IsTrue(ids.Add(d.id), "duplicate " + d.id);
                StringAssert.StartsWith("unlock.", d.id);
                Assert.AreEqual("unlock." + d.target, d.id, "an unlock's id is its target's");
                Assert.IsNotEmpty(d.name, d.id);
                Assert.AreNotEqual(d.target, d.name, d.id + " has no name of its own");
                string words = UnlockCatalog.GoalWords(d.goal);
                Assert.IsNotEmpty(words, d.id);
                StringAssert.EndsWith(".", words, d.id);
                Assert.AreNotEqual(d.goal.ToString(), words, d.goal + " has no words");
                foreach (string w in new[]
                {
                    UnlockCatalog.ShortGoal(d.goal), UnlockCatalog.FeatWords(d.goal), UnlockCatalog.GiftWords(d), UnlockCatalog.KindWords(d),
                    UnlockCatalog.EarnedNote(d, 3), UnlockCatalog.BannerWords(d), UnlockCatalog.BreakLine(d, 3), UnlockCatalog.NextWords(d),
                })
                {
                    Assert.IsNotEmpty(w, d.id);
                    StringAssert.DoesNotContain(d.goal.ToString(), w, d.id + ": the code's name leaked into the words");
                }
                Assert.AreEqual(UnlockCatalog.FeatWords(d.goal).ToUpperInvariant(), UnlockCatalog.FeatWords(d.goal), "the feat is said in capitals");
            }
            foreach (UnlockGoal g in System.Enum.GetValues(typeof(UnlockGoal))) Assert.IsTrue(goals.Contains(g), g + " unlocks nothing");
        }

        [Test]
        public void EveryUnlock_OpensSomethingThatExists()
        {
            List<PerkDefinition> perks = Perks;
            foreach (UnlockDefinition d in UnlockCatalog.All)
            {
                if (d.kind == UnlockKind.Perk)
                {
                    PerkDefinition p = perks.Find(x => x.id == d.target);
                    Assert.IsNotNull(p, d.target + " is not in the perk list");
                    Assert.AreEqual(p.displayName, d.name, d.id);
                }
                else if (d.kind == UnlockKind.Kit)
                {
                    StartingKit k = StartingKits.Find(d.target);
                    Assert.IsNotNull(k, d.target + " is not a kit");
                    Assert.AreEqual(k.displayName, d.name, d.id);
                }
                else
                {
                    Assert.AreEqual(UnlockKind.Look, d.kind, d.id);
                    Items.ItemSpec item = Items.ItemCatalog.Default(d.target);
                    Assert.IsNotNull(item, d.target + " is not an item");
                    Assert.AreEqual(item.displayName, d.name, d.id);
                    Assert.IsTrue(Items.ItemCatalog.IsEarned(d.target), d.target + " would also be in envelopes");
                }
            }
        }

        [Test]
        public void EveryKit_IsTwoOpenPerksOfItsOwnFamily_AndStandardIsNone()
        {
            List<PerkDefinition> perks = Perks;
            HashSet<string> locked = UnlockBook.LockedPerkIds(new SaveData());
            Assert.AreEqual(StartingKits.StandardId, StartingKits.Standard.id);
            Assert.AreEqual(0, StartingKits.Standard.perkIds.Length);
            Assert.IsNull(UnlockCatalog.ForTarget(StartingKits.StandardId), "Standard is never locked");
            Assert.AreEqual(string.Empty, StartingKits.Cost(StartingKits.Standard), "Standard costs nothing");
            var ids = new HashSet<string>();
            foreach (StartingKit kit in StartingKits.All)
            {
                Assert.IsTrue(ids.Add(kit.id), "duplicate kit " + kit.id);
                if (kit.id == StartingKits.StandardId) continue;
                Assert.IsNotNull(UnlockCatalog.ForTarget(kit.id), kit.id + " has no goal");
                Assert.AreEqual(2, kit.perkIds.Length, kit.id);
                Assert.AreNotEqual(kit.perkIds[0], kit.perkIds[1], kit.id);
                foreach (string id in kit.perkIds)
                {
                    PerkDefinition p = perks.Find(x => x.id == id);
                    Assert.IsNotNull(p, kit.id + ": " + id);
                    Assert.AreEqual(kit.family, p.family, kit.id + ": " + id);
                    Assert.IsFalse(locked.Contains(id), kit.id + " starts with a perk that is still locked: " + id);
                }
                string details = StartingKits.Details(kit, perks, new AnteBalance());
                StringAssert.Contains(StartingKits.Cost(kit), details, kit.id + " must say what it costs");
                StringAssert.Contains("ante 4", StartingKits.Cost(kit), kit.id);
                Assert.AreEqual(4, StartingKits.FirstPickAnte(kit), kit.id);
            }
        }

        [Test]
        public void WithEverythingLocked_EveryFamilyCanStillFillItsSet_AndDraftsStayFull()
        {
            var balance = new AnteBalance();
            HashSet<string> locked = UnlockBook.LockedPerkIds(new SaveData());
            Assert.AreEqual(6, locked.Count, "six perks start locked");
            var open = new List<PerkDefinition>();
            foreach (PerkDefinition p in Perks) if (!locked.Contains(p.id)) open.Add(p);
            foreach (PerkFamily f in PerkFamilies.All)
            {
                int n = 0;
                foreach (PerkDefinition p in open) if (p.family == f) n++;
                Assert.GreaterOrEqual(n, PerkFamilies.SetSize(balance), f + " cannot reach its set bonus with its locked perk out");
            }
            // A kit takes two more out of the drafts; a whole run of picks must still be offered in full.
            Assert.GreaterOrEqual(open.Count - 2, PerkCatalog.SizeNeededForFullDrafts(balance));
        }

        [Test]
        public void TheGoalWords_SayTheGamesOwnNumbers_AndThePlayersKeys()
        {
            StringAssert.Contains("60 m", UnlockCatalog.GoalWords(UnlockGoal.LongShotKill, new UnlockHints { longShotMetres = 60f }));
            StringAssert.Contains("45 m", UnlockCatalog.GoalWords(UnlockGoal.LongShotKill, new UnlockHints { longShotMetres = 45f }));

            string armour = UnlockCatalog.GoalWords(UnlockGoal.LowArmourKill, new UnlockHints { lowArmourLine = 0.30f });
            StringAssert.Contains("under 30%", armour);
            StringAssert.Contains("3 chevrons or fewer", armour, "the suit shows ten; one goes dark only when used up");
            StringAssert.Contains("under 25%", UnlockCatalog.GoalWords(UnlockGoal.LowArmourKill, new UnlockHints { lowArmourLine = 0.25f }));
            StringAssert.Contains("under 30%", UnlockCatalog.ShortGoal(UnlockGoal.LowArmourKill));

            StringAssert.Contains("tap C to crouch, hold it to lie prone",
                UnlockCatalog.GoalWords(UnlockGoal.LowStanceKill, new UnlockHints { crouchKey = "C" }));
            StringAssert.Contains("press E under it", UnlockCatalog.GoalWords(UnlockGoal.RoverSplat, new UnlockHints { interactKey = "E" }));
            StringAssert.Contains("moons count", UnlockCatalog.GoalWords(UnlockGoal.ThreeWorldsInOneAnte));
            StringAssert.Contains("before its clock runs out", UnlockCatalog.GoalWords(UnlockGoal.HazardBeatsTheClock));

            string ways = UnlockCatalog.GoalWords(UnlockGoal.FourWaysInOneAnte);
            StringAssert.Contains("4 of these " + ScoreWays.Paid.Length + " ways", ways);
            StringAssert.Contains(ScoreWays.List(), ways);
        }

        [Test]
        public void TheMeleeGoal_OpensAMeleePerk_AndRiskOpensTheDaredevilKit()
        {
            Assert.AreEqual("perk.heavy_swing", UnlockCatalog.ForGoal(UnlockGoal.MeleeKill).target, "only worth holding with a melee weapon");
            Assert.AreEqual("kit.daredevil", UnlockCatalog.ForGoal(UnlockGoal.LowArmourKill).target);
            Assert.IsTrue(UnlockBook.IsOpen(new SaveData(), "perk.in_your_face"), "close-range defence is open from the start");
        }

        // ---- the goals in one run ----

        static List<UnlockGoal> Met(UnlockTracker t) => t.Take();

        [Test]
        public void APlainKill_MeetsNothing_AndEachStyleMeetsItsOwnGoal()
        {
            var t = new UnlockTracker();
            t.Kill(false, false, false, false, false);
            Assert.IsEmpty(Met(t));
            t.Kill(true, false, false, false);
            CollectionAssert.AreEqual(new[] { UnlockGoal.MeleeKill }, Met(t));
            t.Kill(false, true, false, false);
            CollectionAssert.AreEqual(new[] { UnlockGoal.RoverSplat }, Met(t));
            t.Kill(false, false, true, false);
            CollectionAssert.AreEqual(new[] { UnlockGoal.LongShotKill }, Met(t));
            t.Kill(false, false, false, true);
            CollectionAssert.AreEqual(new[] { UnlockGoal.LowStanceKill }, Met(t));
            t.Kill(false, false, false, false, true);
            CollectionAssert.AreEqual(new[] { UnlockGoal.LowArmourKill }, Met(t));
        }

        [Test]
        public void EachGoal_IsHandedBackOnceARun()
        {
            var t = new UnlockTracker();
            t.Kill(true, false, false, false);
            t.Kill(true, false, false, false);
            CollectionAssert.AreEqual(new[] { UnlockGoal.MeleeKill }, Met(t));
            t.Kill(true, false, false, false);
            Assert.IsEmpty(Met(t));
            Assert.IsFalse(t.HasWaiting);
        }

        [Test]
        public void ALowStance_DoesNotCount_FromTheRoversSeat()
        {
            var t = new UnlockTracker();
            t.Kill(false, true, false, true);
            CollectionAssert.AreEqual(new[] { UnlockGoal.RoverSplat }, Met(t));
        }

        [Test]
        public void ClearingAnte2_IsAGoal_ButClearingAnte1IsNot()
        {
            var t = new UnlockTracker();
            t.AnteCleared(1, false, false);
            Assert.IsEmpty(Met(t));
            t.AnteCleared(2, false, false);
            CollectionAssert.AreEqual(new[] { UnlockGoal.ClearAnte2 }, Met(t));
        }

        [Test]
        public void AHazard_MustBeatTheClock()
        {
            var t = new UnlockTracker();
            t.AnteCleared(1, true, true);
            Assert.IsEmpty(Met(t), "cleared in overtime: the bet was lost");
            t.AnteCleared(1, false, false);
            Assert.IsEmpty(Met(t));
            t.AnteCleared(1, true, false);
            CollectionAssert.AreEqual(new[] { UnlockGoal.HazardBeatsTheClock }, Met(t));
        }

        [Test]
        public void FourWays_MustAllComeInOneAnte()
        {
            var t = new UnlockTracker();
            t.Scored(ScoreCategory.Combat);
            t.Scored(ScoreCategory.Combat);
            t.Scored(ScoreCategory.Exploration);
            t.Scored(ScoreCategory.Landing);
            Assert.IsEmpty(Met(t), "three ways, one of them twice");
            t.AnteBegan();
            t.Scored(ScoreCategory.Salvage);
            Assert.IsEmpty(Met(t), "the last ante's ways do not carry over");
            t.Scored(ScoreCategory.Combat);
            t.Scored(ScoreCategory.Exploration);
            t.Scored(ScoreCategory.Objective);
            CollectionAssert.AreEqual(new[] { UnlockGoal.FourWaysInOneAnte }, Met(t));
        }

        [Test]
        public void ThreeWorlds_MustBeDifferent_AndInOneAnte()
        {
            var t = new UnlockTracker();
            t.Landed("Earth");
            t.Landed("Earth");
            Assert.IsEmpty(Met(t), "Earth is home: landing back there meets nothing");
            t.Landed("Moon");
            CollectionAssert.AreEqual(new[] { UnlockGoal.LandAwayFromEarth }, Met(t), "the first landing anywhere else is a goal of its own");
            t.AnteBegan();
            t.Landed("Mars");
            Assert.IsEmpty(Met(t));
            t.Landed(null);
            t.Landed("");
            t.Landed("Earth");
            Assert.IsEmpty(Met(t));
            t.Landed("Moon");
            CollectionAssert.AreEqual(new[] { UnlockGoal.ThreeWorldsInOneAnte }, Met(t));
        }

        [Test]
        public void Home_IsTheWorldEveryRunStartsOn_ByTheIdTheGameKnowsItBy()
        {
            Assert.IsNotNull(Space.SolarSystemDefaults.Bodies().Find(b => b.id == UnlockTracker.HomeWorld), "a body with that id");
            Assert.AreEqual("Earth", UnlockTracker.HomeWorld, "the goal's words say Earth");
            StringAssert.Contains(UnlockTracker.HomeWorld, UnlockCatalog.GoalWords(UnlockGoal.LandAwayFromEarth));
        }

        [Test]
        public void TheLadderTop_IsAGoal()
        {
            var t = new UnlockTracker();
            t.LadderTop();
            CollectionAssert.AreEqual(new[] { UnlockGoal.LadderTop }, Met(t));
        }

        // ---- the looks (Ralph loop pass 5) ----

        [Test]
        public void ANewPlayersFirstThreeGoals_AreLooks_InThreeDifferentPlaces()
        {
            IReadOnlyList<UnlockDefinition> all = UnlockCatalog.All;
            // The landing first: the first tips point at the Moon, and it is the one goal the practice range cannot meet.
            CollectionAssert.AreEqual(new[] { UnlockGoal.LandAwayFromEarth, UnlockGoal.Disarm, UnlockGoal.LegOff },
                new[] { all[0].goal, all[1].goal, all[2].goal });
            for (int i = 0; i < 3; i++) Assert.IsTrue(UnlockCatalog.IsLook(all[i]), all[i].id);
            // Each one looks different and goes somewhere a new player sees: the lander, the guns, the suit plate.
            CollectionAssert.AreEqual(new[] { Items.LockerSlot.Paint, Items.LockerSlot.Skin, Items.LockerSlot.Badge },
                new[] { Items.Locker.SlotOf(UnlockCatalog.LookItem(all[0])), Items.Locker.SlotOf(UnlockCatalog.LookItem(all[1])),
                        Items.Locker.SlotOf(UnlockCatalog.LookItem(all[2])) });
        }

        [Test]
        public void EveryLook_IsACosmeticSkinOrPaint_InAPlaceOfItsOwn_AndEarnedOnlyThisWay()
        {
            var places = new HashSet<string>();
            int looks = 0;
            foreach (UnlockDefinition d in UnlockCatalog.All)
            {
                if (!UnlockCatalog.IsLook(d)) continue;
                looks++;
                Items.ItemSpec item = UnlockCatalog.LookItem(d);
                Assert.IsNotNull(item, d.id);
                Assert.IsTrue(Items.Locker.Usable(item), d.id + " has a place to go to work");
                Assert.AreNotEqual(Items.ItemCategory.Upgrade, item.category, d.id + ": an upgrade has a cost, so it is never a goal's gift");
                Assert.AreNotEqual(Items.ItemCategory.Artwork, item.category, d.id + ": paintings wait for frames");
                // Two looks for one place would leave the second waiting in the collection the first time.
                string place = Items.Locker.SlotOf(item) + ":" + item.weaponId;
                Assert.IsTrue(places.Add(place), d.id + " shares its place with another look");
            }
            Assert.AreEqual(8, looks);
            int earned = 0;
            foreach (string id in Items.ItemCatalog.EarnedItemIds)
            {
                earned++;
                UnlockDefinition d = UnlockCatalog.ForTarget(id);
                Assert.IsNotNull(d, id + " is earned by nothing");
                Assert.AreEqual(UnlockKind.Look, d.kind, id);
            }
            Assert.AreEqual(looks, earned, "every earned look is given by one goal, and every look given is earned");
        }

        [Test]
        public void EachNewGoal_IsMetByItsOwnFeat_AndNothingElse()
        {
            var t = new UnlockTracker();
            t.Disarmed();
            CollectionAssert.AreEqual(new[] { UnlockGoal.Disarm }, Met(t));
            t.LegOff();
            CollectionAssert.AreEqual(new[] { UnlockGoal.LegOff }, Met(t));
            t.SalvageHome();
            CollectionAssert.AreEqual(new[] { UnlockGoal.SalvageHome }, Met(t));
            t.BonusMissionDone();
            CollectionAssert.AreEqual(new[] { UnlockGoal.BonusMission }, Met(t));
            t.Kill(false, false, false, false, airborne: true);
            CollectionAssert.AreEqual(new[] { UnlockGoal.AirborneKill }, Met(t));
            t.Kill(false, false, false, false, inFocus: true);
            CollectionAssert.AreEqual(new[] { UnlockGoal.FocusKill }, Met(t));
            t.Kill(false, false, false, false, seaCreature: true);
            CollectionAssert.AreEqual(new[] { UnlockGoal.SeaKill }, Met(t));
            t.Disarmed();
            t.Kill(false, false, false, false, airborne: true, inFocus: true, seaCreature: true);
            Assert.IsEmpty(Met(t), "each once a run");
        }

        [Test]
        public void ARoverJump_IsNotAnAirborneKill()
        {
            var t = new UnlockTracker();
            t.Kill(false, true, false, false, airborne: true);
            CollectionAssert.AreEqual(new[] { UnlockGoal.RoverSplat }, Met(t), "the jump is the rover's");
        }

        [Test]
        public void GrantingALook_PutsItInTheCollection_AndToWorkWhereItsPlaceIsEmpty()
        {
            var save = new SaveData { items = null };
            UnlockDefinition d = UnlockBook.Grant(save, UnlockGoal.Disarm);
            Assert.IsNotNull(d);
            Assert.AreEqual("unlock.SkinButterfingers", d.id);
            var owned = new Items.ItemInventory(save.items);
            Items.OwnedItem line = owned.Find("SkinButterfingers", "");
            Assert.IsNotNull(line, "in the collection");
            Assert.AreEqual(1, line.count);
            Assert.AreEqual(0, line.firstSerial, "it came from no envelope");
            Assert.IsFalse(line.fromTest, "a real line: clearing test items never takes it");
            Assert.IsTrue(UnlockBook.LookInUse(save, d), "on every gun at once, nothing to press");
            Assert.IsNull(UnlockBook.Grant(save, UnlockGoal.Disarm), "earned once");
            Assert.AreEqual(1, owned.Find("SkinButterfingers", "").count);

            // A pistol skin has a place of its own beside the skin for every gun.
            UnlockBook.Grant(save, UnlockGoal.SeaKill);
            Assert.IsTrue(UnlockBook.LookInUse(save, UnlockCatalog.ForGoal(UnlockGoal.SeaKill)));
            Assert.AreEqual("SkinPistolSharkskin", Items.Locker.SkinFor(save, "Pistol").itemId);
            Assert.AreEqual("SkinButterfingers", Items.Locker.SkinFor(save, "Rifle").itemId);
            Assert.IsFalse(UnlockBook.LookInUse(save, UnlockCatalog.ForGoal(UnlockGoal.ClearAnte2)), "a kit is no look");
        }

        [Test]
        public void ALookWhosePlaceIsTaken_WaitsInTheCollection_AndSaysSo()
        {
            var save = new SaveData();
            Items.ItemSpec orange = Items.ItemCatalog.Default("PaintSafetyOrange");
            new Items.ItemInventory(save.items).Add(new Items.Prize { itemId = orange.id, effectId = "", rarity = orange.rarity }, 4, false);
            Assert.IsTrue(Items.Locker.Use(save, orange, ""));
            UnlockDefinition d = UnlockBook.Grant(save, UnlockGoal.LandAwayFromEarth);
            Assert.IsTrue(new Items.ItemInventory(save.items).Has("PaintRollPattern", ""));
            Assert.IsFalse(UnlockBook.LookInUse(save, d), "never over something the player put to use");
            Assert.AreEqual("PaintSafetyOrange", Items.Locker.Worn(save, Items.LockerSlot.Paint).itemId);
            Assert.AreEqual("LANDED AWAY FROM EARTH · A SHIP PAINT, IN YOUR COLLECTION", UnlockCatalog.EarnedNote(d, 3, null, false));
            StringAssert.EndsWith("In your Collection: put it to use there.", UnlockCatalog.GiftWords(d, null, false));
        }

        [Test]
        public void ALooksWords_SayWhatItIs_AndWhereItShows()
        {
            UnlockDefinition skin = UnlockCatalog.ForGoal(UnlockGoal.Disarm);
            Assert.AreEqual("UNLOCKED: BUTTERFINGERS", UnlockCatalog.BannerWords(skin));
            // A look goes on as its moment shows (the game refreshes the looks then), so the words say "now", not "next run".
            Assert.AreEqual("DISARMED · A GUN SKIN, NOW ON EVERY GUN", UnlockCatalog.EarnedNote(skin, 3));
            Assert.AreEqual("UNLOCKED: Butterfingers, for a disarm. A gun skin, now on every gun.", UnlockCatalog.BreakLine(skin, 3));
            Assert.AreEqual("Butterfingers (gun skin), for a disarm", UnlockCatalog.NextWords(skin), "a look's name says nothing of what it is");
            Assert.AreEqual("Patient Hunter, for a crouched or prone kill", UnlockCatalog.NextWords(UnlockCatalog.ForGoal(UnlockGoal.LowStanceKill)),
                "a perk's name is enough");
            Assert.AreEqual("LEG OFF · A BADGE, NOW ON YOUR SUIT'S READOUT", UnlockCatalog.EarnedNote(UnlockCatalog.ForGoal(UnlockGoal.LegOff), 0),
                "a look waits for no draft: the last ante says the same; and it is on the readout plate, where the player sees it");
            Assert.AreEqual("LANDED AWAY FROM EARTH · A SHIP PAINT, NOW ON YOUR LANDER",
                UnlockCatalog.EarnedNote(UnlockCatalog.ForGoal(UnlockGoal.LandAwayFromEarth), 3));
            Assert.AreEqual("SEA KILL · A PISTOL SKIN, NOW ON THE PISTOL", UnlockCatalog.EarnedNote(UnlockCatalog.ForGoal(UnlockGoal.SeaKill), 3));
            // What it changes, never a piece of gear: beside the upgrades "a jetpack" would read as new kit.
            Assert.AreEqual("a visor tint", UnlockCatalog.KindWords(UnlockCatalog.ForGoal(UnlockGoal.FocusKill)));
            Assert.AreEqual("a helmet shade", UnlockCatalog.KindWords(UnlockCatalog.ForGoal(UnlockGoal.SalvageHome)));
            Assert.AreEqual("a jet flame colour", UnlockCatalog.KindWords(UnlockCatalog.ForGoal(UnlockGoal.AirborneKill)));
            Assert.AreEqual("a readout plate colour", UnlockCatalog.KindWords(UnlockCatalog.ForGoal(UnlockGoal.BonusMission)));
            Assert.AreEqual("Paints your lander's hull. In use.", UnlockCatalog.GiftWords(UnlockCatalog.ForGoal(UnlockGoal.LandAwayFromEarth)));
        }

        [Test]
        public void TheNewGoalWords_SayThePlayersKeys()
        {
            var hints = new UnlockHints { focusKey = "Q", interactKey = "E" };
            StringAssert.Contains("hold Q", UnlockCatalog.GoalWords(UnlockGoal.FocusKill, hints));
            StringAssert.Contains("press E at one", UnlockCatalog.GoalWords(UnlockGoal.SalvageHome, hints));
            StringAssert.Contains("the gun out of its hands, or the hand or arm", UnlockCatalog.GoalWords(UnlockGoal.Disarm),
                "both ways to disarm, as How to play says them");
            StringAssert.Contains("in the water with it", UnlockCatalog.GoalWords(UnlockGoal.SeaKill), "the rule a sea kill pays by");
        }

        [Test]
        public void AnEarnedLookMissingFromTheCollection_ComesBack_WithoutGoingToWork()
        {
            SaveData save = SaveWith(UnlockGoal.Disarm, UnlockGoal.ClearAnte2);
            save.items.Clear();
            Items.Locker.Tidy(save, Items.ItemCatalog.Default);
            Assert.AreEqual(1, UnlockBook.KeepEarnedLooks(save));
            Assert.IsTrue(new Items.ItemInventory(save.items).Has("SkinButterfingers", ""));
            Assert.IsFalse(UnlockBook.LookInUse(save, UnlockCatalog.ForGoal(UnlockGoal.Disarm)), "mending never chooses for the player");
            Assert.AreEqual(0, UnlockBook.KeepEarnedLooks(save), "once");
            Assert.AreEqual(0, UnlockBook.KeepEarnedLooks(new SaveData { unlocks = null }));
            Assert.AreEqual(0, UnlockBook.KeepEarnedLooks(null));
        }

        [Test]
        public void NewLooks_AndNewPerksAndKits_AreSeenOnTheirOwnLists()
        {
            SaveData save = SaveWith(UnlockGoal.Disarm, UnlockGoal.LegOff, UnlockGoal.ClearAnte2);
            Assert.AreEqual(2, UnlockBook.NewCount(save, true));
            Assert.AreEqual(1, UnlockBook.NewCount(save, false));
            UnlockBook.MarkSeen(save, true);
            Assert.AreEqual(0, UnlockBook.NewCount(save, true));
            Assert.AreEqual(1, UnlockBook.NewCount(save, false), "the other list's new one is still new");
            Assert.AreEqual(1, UnlockBook.NewCount(save));
            UnlockBook.MarkSeen(save, false);
            Assert.AreEqual(0, UnlockBook.NewCount(save));
        }

        [Test]
        public void TheNextLook_IsTheEasiestLookNotEarned()
        {
            var save = new SaveData();
            Assert.AreEqual(UnlockGoal.LandAwayFromEarth, UnlockBook.NextLook(save).goal);
            UnlockBook.Grant(save, UnlockGoal.Disarm);
            UnlockBook.Grant(save, UnlockGoal.LegOff);
            UnlockBook.Grant(save, UnlockGoal.LandAwayFromEarth);
            Assert.AreEqual(UnlockGoal.FocusKill, UnlockBook.NextLook(save).goal, "kits and perks are passed over");
            Assert.IsNull(UnlockBook.NextLook(SaveWithEverything()));
        }

        // ---- the kinds of score, in the player's words ----

        static string RuntimeFolder()
        {
            string dir = TestContext.CurrentContext.TestDirectory;
            for (int i = 0; i < 10 && dir != null; i++)
            {
                foreach (string candidate in new[] { Path.Combine(dir, "Assets", "OrbitalAnte", "Scripts", "Runtime"), Path.Combine(dir, "unity", "Assets", "OrbitalAnte", "Scripts", "Runtime") })
                    if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            return Path.Combine(Directory.GetCurrentDirectory(), "Assets", "OrbitalAnte", "Scripts", "Runtime");
        }

        /// <summary>
        /// Every score kind the game itself names outside the rules (what the task director, the spawner and the
        /// bonus missions report) is one of the kinds the goal and the Change of Pace card list, and every kind they
        /// list is paid by something. When something new starts to pay, ScoreWays' words must say it.
        /// </summary>
        [Test]
        public void ScoreWays_NameWhatReallyPaysEachKind()
        {
            string folder = RuntimeFolder();
            Assume.That(Directory.Exists(folder), "runtime folder not found: " + folder);
            var reported = new HashSet<ScoreEventKind>();
            foreach (string file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Ante/")) continue;
                foreach (Match m in Regex.Matches(File.ReadAllText(file), @"ScoreEventKind\.(\w+)"))
                    if (System.Enum.TryParse(m.Groups[1].Value, out ScoreEventKind kind)) reported.Add(kind);
            }
            Assert.IsNotEmpty(reported);
            var listed = new HashSet<ScoreCategory>(ScoreWays.Paid);
            var paid = new HashSet<ScoreCategory>();
            foreach (ScoreEventKind kind in reported)
            {
                ScoreCategory c = AnteBalance.CategoryOf(kind);
                Assert.IsTrue(listed.Contains(c), kind + " pays " + c + ", which the goal and the card do not list");
                paid.Add(c);
            }
            foreach (ScoreCategory c in ScoreWays.Paid) Assert.IsTrue(paid.Contains(c), c + " is listed, but nothing in the game pays it");
            StringAssert.Contains("mast", ScoreWays.Things(AnteBalance.CategoryOf(ScoreEventKind.InstallationDestroyed)), "a mast scores as a fight");
            StringAssert.Contains("first landing", ScoreWays.Things(AnteBalance.CategoryOf(ScoreEventKind.NewMoon)));
            StringAssert.Contains("HARD LANDING", ScoreWays.Things(AnteBalance.CategoryOf(ScoreEventKind.DifficultLanding)));
            StringAssert.Contains("crate", ScoreWays.Things(AnteBalance.CategoryOf(ScoreEventKind.SalvageDelivered)));
            StringAssert.Contains("bonus mission", ScoreWays.Things(AnteBalance.CategoryOf(ScoreEventKind.OptionalMission)));
        }

        [Test]
        public void TheGoal_AndTheChangeOfPaceCard_ListTheSameKinds()
        {
            PerkDefinition chain = Perks.Find(p => p.id == PerkCatalog.ChainPerkId);
            string card = chain.Describe(new AnteBalance());
            StringAssert.Contains(ScoreWays.ShortList(), card);
            StringAssert.DoesNotContain("bounty", card, "nothing pays a bounty yet");
            StringAssert.DoesNotContain("repair", card, "nothing pays a repair yet");
            for (int i = 0; i < ScoreWays.Paid.Length; i++)
            {
                StringAssert.Contains(ScoreWays.Things(ScoreWays.Paid[i]), ScoreWays.List());
                StringAssert.Contains(ScoreWays.Plural(ScoreWays.Paid[i]), card);
            }
        }

        // ---- the moment ----

        [Test]
        public void TheUnlockMoment_WaitsOutAFight_ButNotForever()
        {
            Assert.IsTrue(UnlockMoment.MayShow(true, false, false, false, 0f));
            Assert.IsFalse(UnlockMoment.MayShow(true, false, false, true, 0f), "not in the middle of a fight");
            Assert.IsFalse(UnlockMoment.MayShow(true, false, false, true, UnlockMoment.FightWaitMost - 0.1f));
            Assert.IsTrue(UnlockMoment.MayShow(true, false, false, true, UnlockMoment.FightWaitMost), "a long fight does not swallow it");
            Assert.IsFalse(UnlockMoment.MayShow(false, false, false, false, 100f), "never while the readout is hidden");
            Assert.IsFalse(UnlockMoment.MayShow(true, true, false, false, 100f), "never over TARGET MET in its hold");
            Assert.IsFalse(UnlockMoment.MayShow(true, false, true, false, 100f), "never over the NEW GUN banner");
        }

        [Test]
        public void AnOwedUnlockSave_WaitsOutAFight_ButNotAPause_NorLongerThanTheBanner()
        {
            Assert.IsTrue(UnlockMoment.SaveNow(false, false, 0f), "with no fight on, the frame after the goal");
            Assert.IsFalse(UnlockMoment.SaveNow(true, false, 0f), "never in the middle of a fight");
            Assert.IsFalse(UnlockMoment.SaveNow(true, false, UnlockMoment.FightWaitMost - 0.1f));
            Assert.IsTrue(UnlockMoment.SaveNow(true, false, UnlockMoment.FightWaitMost), "a long fight never keeps it unwritten for long");
            Assert.IsTrue(UnlockMoment.SaveNow(true, true, 0f), "a pause is the cheapest moment there is");
        }

        [Test]
        public void ThePracticeRange_SaysOnceThatGoalsCountOnlyInARun()
        {
            Assert.IsTrue(PracticeNote.Shows(true, false, false, false, true, false, true), "a gun knocked out of a dummy's hands");
            Assert.IsTrue(PracticeNote.Shows(false, true, false, true, true, false, true), "a limb off a living one");
            Assert.IsTrue(PracticeNote.Shows(false, false, true, true, true, false, true), "a kill");
            Assert.IsFalse(PracticeNote.Shows(false, true, false, false, true, false, true), "splitting a loose limb is no fight");
            Assert.IsFalse(PracticeNote.Shows(false, false, false, true, true, false, true), "a plain hit meets no goal");
            Assert.IsFalse(PracticeNote.Shows(true, false, false, false, true, true, true), "once a session");
            Assert.IsFalse(PracticeNote.Shows(true, false, false, false, false, false, true), "tips off");
            Assert.IsFalse(PracticeNote.Shows(true, false, false, false, true, false, false), "nothing left to earn");
            Assert.IsFalse(PracticeNote.WouldCount(false, false, false, true), "the cheap check a plain hit stops at");
            Assert.IsTrue(PracticeNote.WouldCount(false, true, false, true));
            StringAssert.Contains("only in a run", PracticeNote.Words);
            Assert.LessOrEqual(PracticeNote.Words.Length, 60, "one line of the arena's banner");
        }

        [Test]
        public void TheUnlockMoment_SaysTheFeat_AndWhereTheUnlockTurnsUp()
        {
            UnlockDefinition hunter = UnlockCatalog.ForGoal(UnlockGoal.LowStanceKill);
            Assert.AreEqual("UNLOCKED: PATIENT HUNTER", UnlockCatalog.BannerWords(hunter));
            Assert.AreEqual("CROUCHED OR PRONE KILL · A GHOST PERK, IN YOUR DRAFTS FROM ANTE 3", UnlockCatalog.EarnedNote(hunter, 3));
            Assert.AreEqual("CROUCHED OR PRONE KILL · A GHOST PERK, IN YOUR NEXT RUN'S DRAFTS", UnlockCatalog.EarnedNote(hunter, 0),
                "unlocked in the last ante: there is no draft left this run");
            Assert.AreEqual("UNLOCKED: Patient Hunter, for a crouched or prone kill. A Ghost perk, in your drafts from ante 3.",
                UnlockCatalog.BreakLine(hunter, 3));

            UnlockDefinition gunner = UnlockCatalog.ForGoal(UnlockGoal.ClearAnte2);
            Assert.AreEqual("ANTE 2 CLEARED · A STARTING KIT, CHOOSE IT AT NEW RUN", UnlockCatalog.EarnedNote(gunner, 3));
            Assert.AreEqual("A wild-card perk", "A" + UnlockCatalog.KindWords(UnlockCatalog.ForGoal(UnlockGoal.FourWaysInOneAnte)).Substring(1));
        }

        [Test]
        public void TheRecap_NamesTheNextTwo_AndFoldsClearingAnte2IntoTheNextMark()
        {
            var save = new SaveData();
            Assert.AreEqual("Next unlocks: Roll Pattern (ship paint), for a landing away from Earth · Butterfingers (gun skin), for a disarm\n"
                            + "Next mark: clear ante 2, which unlocks the Gunner kit.", UnlockRecap.Lines(save, null, 1, 8));
            Assert.AreEqual("Next unlocks: Roll Pattern (ship paint), for a landing away from Earth · Butterfingers (gun skin), for a disarm\n"
                            + "Next mark: clear ante 1.", UnlockRecap.Lines(save, null, 0, 8), "ante 1 is not a goal: nothing to fold");
            SaveData firstRun = SaveWith(UnlockGoal.Disarm, UnlockGoal.LegOff, UnlockGoal.LandAwayFromEarth);
            Assert.AreEqual("Next unlocks: Patient Hunter, for a crouched or prone kill · Tunnel Vision (visor tint), for a kill in Focus\n"
                            + "Next mark: clear ante 2, which unlocks the Gunner kit.", UnlockRecap.Lines(firstRun, null, 1, 8));
            Assert.AreEqual("Next unlocks: Gunner kit, for clearing ante 2 · Patient Hunter, for a crouched or prone kill\n"
                            + "Next mark: clear ante 1.", UnlockRecap.Lines(firstRun, null, 0, 8));

            SaveData some = SaveWith(UnlockGoal.RoverSplat);
            string lines = UnlockRecap.Lines(some, new List<UnlockDefinition> { UnlockCatalog.ForGoal(UnlockGoal.RoverSplat) }, 1, 8);
            StringAssert.StartsWith("Unlocked this run: Road Rage. See Unlocks on the title screen.\n", lines);
            StringAssert.Contains("Next unlocks: Roll Pattern (ship paint), for a landing away from Earth · Butterfingers (gun skin), for a disarm\n", lines);

            Assert.AreEqual("Next mark: a higher run score.", UnlockRecap.Lines(SaveWithEverything(), null, 8, 8), "nothing left to unlock");
            Assert.AreEqual("Next unlock: All In, for beating the clock with the stakes raised\nNext mark: clear ante 4.",
                UnlockRecap.Lines(EverythingBut(UnlockGoal.HazardBeatsTheClock), null, 3, 8));
        }

        static SaveData EverythingBut(UnlockGoal left)
        {
            var save = new SaveData();
            foreach (UnlockDefinition d in UnlockCatalog.All) if (d.goal != left) UnlockBook.Grant(save, d.goal);
            return save;
        }

        // ---- the book in the save ----

        [Test]
        public void Granting_SavesTheUnlockOnce()
        {
            var save = new SaveData();
            UnlockDefinition d = UnlockBook.Grant(save, UnlockGoal.RoverSplat);
            Assert.IsNotNull(d);
            Assert.AreEqual("unlock.perk.road_rage", d.id);
            Assert.IsNull(UnlockBook.Grant(save, UnlockGoal.RoverSplat), "earned before");
            CollectionAssert.AreEqual(new[] { "unlock.perk.road_rage" }, save.unlocks);
            CollectionAssert.AreEqual(new[] { "perk.road_rage" }, save.perksToShow, "owed one showing in a draft");
            Assert.IsTrue(UnlockBook.IsOpen(save, "perk.road_rage"));
            Assert.IsFalse(UnlockBook.IsOpen(save, "perk.all_in"));
            Assert.IsTrue(UnlockBook.IsOpen(save, "perk.quick_mend"), "a perk nothing guards is open from the start");
            Assert.IsNull(UnlockBook.Grant(null, UnlockGoal.RoverSplat));
        }

        [Test]
        public void ANewPerk_IsOwedOneShowing_AndAKitIsOwedNone()
        {
            var save = new SaveData();
            Assert.IsNull(UnlockBook.PerkToShow(save));
            UnlockBook.Grant(save, UnlockGoal.ClearAnte2);
            Assert.IsNull(UnlockBook.PerkToShow(save), "a kit is chosen at New run, not drafted");
            UnlockBook.Grant(save, UnlockGoal.LowStanceKill);
            UnlockBook.Grant(save, UnlockGoal.MeleeKill);
            Assert.AreEqual("perk.patient_hunter", UnlockBook.PerkToShow(save), "the first earned is shown first");
            Assert.IsTrue(UnlockBook.MarkShown(save, "perk.patient_hunter"));
            Assert.IsFalse(UnlockBook.MarkShown(save, "perk.patient_hunter"), "shown once");
            Assert.AreEqual("perk.heavy_swing", UnlockBook.PerkToShow(save));
            UnlockBook.MarkShown(save, "perk.heavy_swing");
            Assert.IsNull(UnlockBook.PerkToShow(save));

            var odd = new SaveData { perksToShow = new List<string> { "perk.not_a_perk", "perk.all_in", "perk.road_rage" } };
            UnlockBook.Grant(odd, UnlockGoal.RoverSplat);
            Assert.AreEqual("perk.road_rage", UnlockBook.PerkToShow(odd), "an id it does not know, or a perk not earned, is passed over");
            Assert.IsNull(UnlockBook.PerkToShow(new SaveData { perksToShow = null }));
            Assert.IsFalse(UnlockBook.MarkShown(null, "perk.road_rage"));
        }

        [Test]
        public void LockedPerks_AreTheSixUntilEarned()
        {
            HashSet<string> locked = UnlockBook.LockedPerkIds(new SaveData());
            CollectionAssert.AreEquivalent(new[]
            {
                "perk.patient_hunter", "perk.road_rage", PerkCatalog.ChainPerkId, "perk.trigger_discipline", "perk.heavy_swing", "perk.all_in",
            }, locked);
            HashSet<string> after = UnlockBook.LockedPerkIds(SaveWith(UnlockGoal.LowStanceKill, UnlockGoal.ClearAnte2));
            Assert.AreEqual(5, after.Count);
            Assert.IsFalse(after.Contains("perk.patient_hunter"));
            Assert.AreEqual(0, UnlockBook.LockedPerkIds(SaveWithEverything()).Count);
            Assert.AreEqual(6, UnlockBook.LockedPerkIds(new SaveData { unlocks = null }).Count);
        }

        [Test]
        public void TheChosenKit_FallsBackToStandard_WhenItIsLockedOrUnknown()
        {
            var save = new SaveData();
            Assert.AreEqual(StartingKits.StandardId, UnlockBook.ChosenKit(save).id);
            save.kit = "kit.ghost";
            Assert.AreEqual(StartingKits.StandardId, UnlockBook.ChosenKit(save).id, "locked");
            Assert.IsFalse(UnlockBook.Choose(save, "kit.ghost"));
            Assert.AreEqual(StartingKits.StandardId, save.kit);
            UnlockBook.Grant(save, UnlockGoal.LongShotKill);
            Assert.IsTrue(UnlockBook.Choose(save, "kit.ghost"));
            Assert.AreEqual("kit.ghost", UnlockBook.ChosenKit(save).id);
            save.kit = "kit.gone_since";
            Assert.AreEqual(StartingKits.StandardId, UnlockBook.ChosenKit(save).id);
            save.kit = null;
            Assert.AreEqual(StartingKits.StandardId, UnlockBook.ChosenKit(save).id);
            Assert.AreEqual(StartingKits.StandardId, UnlockBook.ChosenKit(null).id);
        }

        [Test]
        public void OpenKits_AreStandardThenTheOnesEarned_InOrder()
        {
            var save = new SaveData();
            CollectionAssert.AreEqual(new[] { StartingKits.StandardId }, UnlockBook.OpenKits(save).ConvertAll(k => k.id));
            UnlockBook.Grant(save, UnlockGoal.LowArmourKill);
            UnlockBook.Grant(save, UnlockGoal.ClearAnte2);
            CollectionAssert.AreEqual(new[] { StartingKits.StandardId, "kit.gunner", "kit.daredevil" }, UnlockBook.OpenKits(save).ConvertAll(k => k.id));
        }

        [Test]
        public void TheNextUnlock_IsTheFirstNotEarned_EasiestFirst()
        {
            var save = new SaveData();
            Assert.AreEqual(UnlockGoal.LandAwayFromEarth, UnlockBook.Next(save).goal, "a new player's first goal is the first flight's");
            UnlockBook.Grant(save, UnlockGoal.Disarm);
            UnlockBook.Grant(save, UnlockGoal.LegOff);
            UnlockBook.Grant(save, UnlockGoal.LandAwayFromEarth);
            Assert.AreEqual(UnlockGoal.ClearAnte2, UnlockBook.Next(save).goal);
            UnlockBook.Grant(save, UnlockGoal.ClearAnte2);
            Assert.AreEqual(UnlockGoal.LowStanceKill, UnlockBook.Next(save).goal);
            UnlockBook.Grant(save, UnlockGoal.RoverSplat);
            Assert.AreEqual(UnlockGoal.LowStanceKill, UnlockBook.Next(save).goal, "a later one earned first does not move the next mark");
            CollectionAssert.AreEqual(new[] { UnlockGoal.LowStanceKill, UnlockGoal.FocusKill },
                UnlockBook.Next(save, 2, null).ConvertAll(d => d.goal));
            CollectionAssert.AreEqual(new[] { UnlockGoal.FocusKill, UnlockGoal.SalvageHome },
                UnlockBook.Next(save, 2, UnlockGoal.LowStanceKill).ConvertAll(d => d.goal));
            Assert.IsNull(UnlockBook.Next(SaveWithEverything()));
            Assert.IsEmpty(UnlockBook.Next(SaveWithEverything(), 3, null));
        }

        [Test]
        public void NewUnlocks_AreMarkedUntilTheUnlocksPageIsSeen()
        {
            SaveData save = SaveWith(UnlockGoal.ClearAnte2, UnlockGoal.RoverSplat);
            Assert.AreEqual(2, UnlockBook.NewCount(save));
            Assert.IsTrue(UnlockBook.IsNew(save, "unlock.kit.gunner"));
            UnlockBook.MarkAllSeen(save);
            Assert.AreEqual(0, UnlockBook.NewCount(save));
            UnlockBook.MarkAllSeen(save);
            Assert.AreEqual(2, save.unlocksSeen.Count, "marking twice adds nothing");
            UnlockBook.Grant(save, UnlockGoal.MeleeKill);
            Assert.AreEqual(1, UnlockBook.NewCount(save));
            Assert.IsFalse(UnlockBook.IsNew(save, "unlock.perk.all_in"), "not earned is not new");
        }

        [Test]
        public void AVersion4Save_IsBroughtUpToDate_WithNothingUnlocked_AndStandardChosen()
        {
            var data = new SaveData { version = 4, runsEnded = 3, bestAnte = 3, unlocks = null, unlocksSeen = null, kit = null, perksToShow = null };
            Assert.IsTrue(SaveMigrations.Upgrade(data));
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.IsNotNull(data.unlocks);
            Assert.IsEmpty(data.unlocks);
            Assert.IsNotNull(data.unlocksSeen);
            Assert.IsNotNull(data.perksToShow);
            Assert.IsEmpty(data.perksToShow);
            Assert.AreEqual(StartingKits.StandardId, data.kit);
            Assert.AreEqual(3, data.runsEnded, "records are kept");
            Assert.AreEqual(3, data.bestAnte);
        }

        [Test]
        public void ACurrentSave_WithMissingLists_IsMendedNotThrownAway()
        {
            var data = new SaveData { unlocks = null, unlocksSeen = null, kit = "", perksToShow = null };
            Assert.IsTrue(SaveMigrations.Upgrade(data));
            Assert.IsNotNull(data.unlocks);
            Assert.IsNotNull(data.unlocksSeen);
            Assert.IsNotNull(data.perksToShow);
            Assert.AreEqual(StartingKits.StandardId, data.kit);
        }

        // ---- locked perks in the drafts, and the new one shown ----

        [Test]
        public void ALockedPerk_IsNeverOffered()
        {
            HashSet<string> locked = UnlockBook.LockedPerkIds(new SaveData());
            for (int seed = 1; seed <= 60; seed++)
            {
                AnteRun run = NewRun(seed);
                // A featured perk that is still locked is ignored, never shown.
                run.SetLocked(locked, "perk.all_in");
                Assert.IsNull(run.FeaturedPerkId);
                for (int ante = 1; ante <= 4; ante++)
                {
                    MeetTarget(run, seed + "." + ante);
                    for (int reroll = 0; reroll < 3; reroll++)
                    {
                        foreach (PerkDefinition p in run.PerkOffers())
                            Assert.IsFalse(locked.Contains(p.id), "seed " + seed + " ante " + ante + " offered locked " + p.id);
                        run.Ledger.AddBankOnly(100000);
                        run.RerollPerks();
                    }
                    List<PerkDefinition> offers = run.PerkOffers();
                    if (offers.Count > 0) run.TakePerk(offers[0]);
                    Assert.IsTrue(run.Director.ConfirmAdvance());
                    if (run.Director.Phase == AntePhase.Intermission) run.BeginNextAnte();
                    run.SetLocked(locked);
                }
            }
        }

        [Test]
        public void LockingNothing_LeavesTheDraftsAsTheyWere()
        {
            AnteRun plain = NewRun(11), unlocked = NewRun(11), empty = NewRun(11);
            unlocked.SetLocked(UnlockBook.LockedPerkIds(SaveWithEverything()));
            empty.SetLocked(null);
            MeetTarget(plain, "a");
            MeetTarget(unlocked, "a");
            MeetTarget(empty, "a");
            List<string> a = plain.PerkOffers().ConvertAll(p => p.id);
            CollectionAssert.AreEqual(a, unlocked.PerkOffers().ConvertAll(p => p.id));
            CollectionAssert.AreEqual(a, empty.PerkOffers().ConvertAll(p => p.id));
        }

        [Test]
        public void ANewPerk_IsInTheNextDraft_InItsLastPlace_AndTheOtherOffersKeepTheirs()
        {
            SaveData save = SaveWith(UnlockGoal.LowStanceKill);
            HashSet<string> locked = UnlockBook.LockedPerkIds(save);
            string fresh = UnlockBook.PerkToShow(save);
            Assert.AreEqual("perk.patient_hunter", fresh);
            int replaced = 0;
            for (int seed = 1; seed <= 50; seed++)
            {
                AnteRun plain = NewRun(seed), shown = NewRun(seed);
                plain.SetLocked(locked);
                shown.SetLocked(locked, fresh);
                Assert.AreEqual(fresh, shown.FeaturedPerkId);
                MeetTarget(plain, "p" + seed);
                MeetTarget(shown, "p" + seed);
                List<string> before = plain.PerkOffers().ConvertAll(p => p.id);
                List<string> after = shown.PerkOffers().ConvertAll(p => p.id);
                Assert.AreEqual(before.Count, after.Count, "seed " + seed);
                Assert.Contains(fresh, after, "seed " + seed);
                CollectionAssert.AreEqual(after, shown.PerkOffers().ConvertAll(p => p.id), "asking twice gives the same three");
                if (before.Contains(fresh)) CollectionAssert.AreEqual(before, after, "seed " + seed + ": already offered, so nothing moves");
                else
                {
                    replaced++;
                    Assert.AreEqual(fresh, after[after.Count - 1], "seed " + seed);
                    CollectionAssert.AreEqual(before.GetRange(0, before.Count - 1), after.GetRange(0, after.Count - 1), "seed " + seed);
                }

                // A reroll is a fresh three, the same as with no new perk.
                plain.Ledger.AddBankOnly(100000);
                shown.Ledger.AddBankOnly(100000);
                Assert.IsTrue(plain.RerollPerks());
                Assert.IsTrue(shown.RerollPerks());
                CollectionAssert.AreEqual(plain.PerkOffers().ConvertAll(p => p.id), shown.PerkOffers().ConvertAll(p => p.id), "seed " + seed);
            }
            Assert.Greater(replaced, 0, "some draft must have needed the new perk put in");
        }

        [Test]
        public void ANewPerk_AlreadyHeld_OrUnknown_IsNotForcedIn()
        {
            SaveData save = SaveWith(UnlockGoal.LowStanceKill);
            AnteRun run = NewRun(5);
            PerkDefinition hunter = run.Catalog[0];
            foreach (PerkDefinition p in run.Catalog) if (p.id == "perk.patient_hunter") hunter = p;
            Assert.IsTrue(run.Perks.Add(hunter));
            run.SetLocked(UnlockBook.LockedPerkIds(save), "perk.patient_hunter");
            MeetTarget(run, "h");
            foreach (PerkDefinition p in run.PerkOffers()) Assert.AreNotEqual("perk.patient_hunter", p.id, "held, so never offered again");

            AnteRun other = NewRun(5);
            other.SetLocked(UnlockBook.LockedPerkIds(save), "perk.not_a_perk");
            Assert.IsNull(other.FeaturedPerkId);
        }

        // ---- starting kits ----

        [Test]
        public void AKit_GivesItsPerks_AndTheFirstThreeAntesHaveNoPickOfTheirOwn()
        {
            AnteRun run = NewRun(3);
            StartingKit ghost = StartingKits.Find("kit.ghost");
            Assert.IsTrue(run.StartWithKit(ghost));
            Assert.AreEqual("kit.ghost", run.KitId);
            Assert.IsTrue(run.Perks.Has("perk.eagle_eye"));
            Assert.IsTrue(run.Perks.Has("perk.cold_blood"));
            Assert.AreEqual(2 + StartingKits.ExtraPicks, run.Director.PicksGivenAhead);

            for (int ante = 1; ante <= 3; ante++)
            {
                MeetTarget(run, "k" + ante);
                Assert.IsTrue(run.Director.PickGivenAhead, "ante " + ante);
                Assert.IsFalse(run.Director.PerkEarnedEarly, "ante " + ante + ": no pick in the field");
                Assert.IsFalse(run.Director.CanPickPerk);
                Assert.IsEmpty(run.PerkOffers());
                Assert.IsTrue(run.Director.ConfirmAdvance());
                Assert.IsFalse(run.Director.PerkDraftPending, "ante " + ante + ": no pick at the break");
                Assert.IsTrue(run.Director.ScratchPending, "the ticket is not the kit's");
                Assert.IsTrue(run.BeginNextAnte());
            }

            MeetTarget(run, "k4");
            Assert.IsFalse(run.Director.PickGivenAhead);
            Assert.IsTrue(run.Director.PerkEarnedEarly, "ante 4 picks as usual");
            List<PerkDefinition> offers = run.PerkOffers();
            Assert.IsNotEmpty(offers);
            foreach (PerkDefinition p in offers)
                Assert.IsTrue(p.id != "perk.eagle_eye" && p.id != "perk.cold_blood", "a kit perk is held, so never offered again");
            Assert.IsTrue(run.Director.ConfirmAdvance());
            Assert.IsTrue(run.Director.PerkDraftPending, "ante 4's pick waits at the break if not taken in the field");
        }

        [Test]
        public void OverARun_AKitIsAheadEarly_AndOnePerkBehindLater()
        {
            AnteRun kit = NewRun(9), picks = NewRun(9);
            Assert.IsTrue(kit.StartWithKit(StartingKits.Find("kit.gunner")));
            for (int ante = 1; ante <= 5; ante++)
            {
                foreach (AnteRun run in new[] { kit, picks })
                {
                    MeetTarget(run, "r" + ante);
                    Assert.IsTrue(run.Director.ConfirmAdvance());
                    List<PerkDefinition> offers = run.PerkOffers();
                    if (run.Director.PerkDraftPending && offers.Count > 0) Assert.IsTrue(run.TakePerk(offers[0]));
                    run.BeginNextAnte();
                }
                if (ante == 1) Assert.Greater(kit.Perks.Owned.Count, picks.Perks.Owned.Count, "a kit starts ahead");
                if (ante >= 3)
                    Assert.AreEqual(picks.Perks.Owned.Count - StartingKits.ExtraPicks, kit.Perks.Owned.Count,
                        "after ante " + ante + ": a kit costs one perk, so it is a sidegrade, never an upgrade");
            }
        }

        [Test]
        public void Standard_AndLateKits_ChangeNothing()
        {
            AnteRun run = NewRun(4);
            Assert.IsFalse(run.StartWithKit(StartingKits.Standard));
            Assert.IsFalse(run.StartWithKit(null));
            Assert.IsNull(run.KitId);
            Assert.AreEqual(0, run.Director.PicksGivenAhead);

            AnteRun later = NewRun(4);
            ClearAnte(later);
            later.BeginNextAnte();
            Assert.IsFalse(later.StartWithKit(StartingKits.Find("kit.pilot")), "only at the start of a run");
            Assert.AreEqual(0, later.Perks.Owned.Count);

            AnteRun twice = NewRun(4);
            Assert.IsTrue(twice.StartWithKit(StartingKits.Find("kit.pilot")));
            Assert.IsFalse(twice.StartWithKit(StartingKits.Find("kit.gunner")), "one kit a run");
            Assert.AreEqual("kit.pilot", twice.KitId);
            Assert.AreEqual(2, twice.Perks.Owned.Count);
        }

        [Test]
        public void AKitWithPerksTheCatalogueLacks_GivesNothing()
        {
            var run = new AnteRun(new AnteBalance(), 2, PerkCatalog.CreateDefaults());
            var odd = new StartingKit { id = "kit.odd", displayName = "Odd", perkIds = new[] { "perk.not_here", "perk.nor_this" } };
            Assert.IsFalse(run.StartWithKit(odd));
            Assert.AreEqual(0, run.Director.PicksGivenAhead);
        }

        [Test]
        public void AKitRun_SurvivesExportAndImport()
        {
            AnteRun run = NewRun(13);
            Assert.IsTrue(run.StartWithKit(StartingKits.Find("kit.daredevil")));
            ClearAnte(run);
            AnteRunState state = run.Export();
            Assert.AreEqual("kit.daredevil", state.kit);
            Assert.AreEqual(3, state.picksGivenAhead);
            AnteRun back = AnteRun.Import(new AnteBalance(), state, RoguelitePerks.Create());
            Assert.AreEqual("kit.daredevil", back.KitId);
            Assert.AreEqual(3, back.Director.PicksGivenAhead);
            Assert.IsTrue(back.Perks.Has("perk.glass_cannon"));
            Assert.IsFalse(back.Director.PerkDraftPending);
            Assert.IsTrue(back.BeginNextAnte());
            MeetTarget(back, "i2");
            Assert.IsFalse(back.Director.PerkEarnedEarly, "ante 2's pick is still the kit's after a load");
            Assert.IsTrue(back.Director.ConfirmAdvance());
            Assert.IsTrue(back.BeginNextAnte());
            MeetTarget(back, "i3");
            Assert.IsFalse(back.Director.PerkEarnedEarly, "ante 3 has no pick: the kit's price");
        }

        [Test]
        public void KitWords_SayThePerks_TheSet_AndThePrice()
        {
            List<PerkDefinition> perks = Perks;
            var balance = new AnteBalance();
            StartingKit gunner = StartingKits.Find("kit.gunner");
            Assert.AreEqual("Start with Quick Study and Steady Hands.", StartingKits.Summary(gunner, perks));
            Assert.AreEqual("Two perks now, one fewer this run: your first perk pick is ante 4's.", StartingKits.Cost(gunner));
            string details = StartingKits.Details(gunner, perks, balance);
            StringAssert.StartsWith("Quick Study: ", details);
            StringAssert.Contains("\nSteady Hands: ", details);
            StringAssert.Contains("\nGunner 2 of 3: one more Gunner perk turns on its set (" + PerkFamilies.Describe(PerkFamily.Gunner, balance) + ").", details);
            StringAssert.EndsWith(StartingKits.Cost(gunner), details);
            StringAssert.Contains("every pick is yours", StartingKits.Summary(StartingKits.Standard, perks));
            Assert.AreEqual(string.Empty, StartingKits.Details(StartingKits.Standard, perks, balance));
            Assert.AreEqual("this ante's came with your Gunner kit", StartingKits.NoPickWords(gunner, 1));
            Assert.AreEqual("this ante's came with your Gunner kit", StartingKits.NoPickWords(gunner, 2));
            Assert.AreEqual("none this ante, the price of your Gunner kit", StartingKits.NoPickWords(gunner, 3));
            StringAssert.Contains(StartingKits.Cost(gunner), UnlockCatalog.GiftWords(UnlockCatalog.ForTarget("kit.gunner"), balance),
                "the Unlocks page says the price too");
        }
    }
}
