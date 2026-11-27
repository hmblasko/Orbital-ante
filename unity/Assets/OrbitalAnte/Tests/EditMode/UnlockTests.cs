using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Ante;
using OrbitalAnte.Save;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Goal unlocks and starting kits (Ralph loop pass 1, 27 Nov 2026; Ante/Unlocks.cs): what each goal is, that it
    /// is met by its own feat and nothing else, that locked perks never reach a draft, that a kit's perks stand in
    /// for the first two picks, and that the save keeps all of it. Plain C#.
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
                Assert.IsNotEmpty(UnlockCatalog.GiftWords(d), d.id);
                Assert.IsNotEmpty(UnlockCatalog.KindWords(d), d.id);
                Assert.IsNotEmpty(UnlockCatalog.EarnedNote(d), d.id);
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
                else
                {
                    StartingKit k = StartingKits.Find(d.target);
                    Assert.IsNotNull(k, d.target + " is not a kit");
                    Assert.AreEqual(k.displayName, d.name, d.id);
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
                StringAssert.Contains(StartingKits.CostWords, StartingKits.Details(kit, perks, new AnteBalance()), kit.id + " must say what it costs");
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
        public void TheLongShotGoal_SaysTheTaskRulesDistance()
        {
            StringAssert.Contains("60 m", UnlockCatalog.GoalWords(UnlockGoal.LongShotKill, 60f));
            StringAssert.Contains("45 m", UnlockCatalog.GoalWords(UnlockGoal.LongShotKill, 45f));
            StringAssert.Contains("1:00", UnlockCatalog.GoalWords(UnlockGoal.BankEarly));
        }

        // ---- the goals in one run ----

        static List<UnlockGoal> Met(UnlockTracker t) => t.Take();

        [Test]
        public void APlainKill_MeetsNothing_AndEachStyleMeetsItsOwnGoal()
        {
            var t = new UnlockTracker();
            t.Kill(false, false, false, false);
            Assert.IsEmpty(Met(t));
            t.Kill(true, false, false, false);
            CollectionAssert.AreEqual(new[] { UnlockGoal.MeleeKill }, Met(t));
            t.Kill(false, true, false, false);
            CollectionAssert.AreEqual(new[] { UnlockGoal.RoverSplat }, Met(t));
            t.Kill(false, false, true, false);
            CollectionAssert.AreEqual(new[] { UnlockGoal.LongShotKill }, Met(t));
            t.Kill(false, false, false, true);
            CollectionAssert.AreEqual(new[] { UnlockGoal.LowStanceKill }, Met(t));
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
            t.AnteCleared(1, 0f, false, false);
            Assert.IsEmpty(Met(t));
            t.AnteCleared(2, 0f, false, false);
            CollectionAssert.AreEqual(new[] { UnlockGoal.ClearAnte2 }, Met(t));
        }

        [Test]
        public void BankingEarly_NeedsAMinuteOnTheClock_AndNeverCountsInOvertime()
        {
            var t = new UnlockTracker();
            t.AnteCleared(1, UnlockTracker.BankEarlySeconds - 0.5f, false, false);
            Assert.IsEmpty(Met(t));
            t.AnteCleared(1, 200f, false, true);
            Assert.IsEmpty(Met(t), "overtime is past the clock");
            t.AnteCleared(1, UnlockTracker.BankEarlySeconds, false, false);
            CollectionAssert.AreEqual(new[] { UnlockGoal.BankEarly }, Met(t));
        }

        [Test]
        public void AHazard_MustBeatTheClock()
        {
            var t = new UnlockTracker();
            t.AnteCleared(1, 0f, true, true);
            Assert.IsEmpty(Met(t), "cleared in overtime: the bet was lost");
            t.AnteCleared(1, 0f, false, false);
            Assert.IsEmpty(Met(t));
            t.AnteCleared(1, 0f, true, false);
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
            t.Landed("earth");
            t.Landed("earth");
            t.Landed("moon");
            Assert.IsEmpty(Met(t));
            t.AnteBegan();
            t.Landed("mars");
            Assert.IsEmpty(Met(t));
            t.Landed(null);
            t.Landed("");
            t.Landed("earth");
            Assert.IsEmpty(Met(t));
            t.Landed("moon");
            CollectionAssert.AreEqual(new[] { UnlockGoal.ThreeWorldsInOneAnte }, Met(t));
        }

        [Test]
        public void TheLadderTop_IsAGoal()
        {
            var t = new UnlockTracker();
            t.LadderTop();
            CollectionAssert.AreEqual(new[] { UnlockGoal.LadderTop }, Met(t));
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
            Assert.IsTrue(UnlockBook.IsOpen(save, "perk.road_rage"));
            Assert.IsFalse(UnlockBook.IsOpen(save, "perk.all_in"));
            Assert.IsTrue(UnlockBook.IsOpen(save, "perk.quick_mend"), "a perk nothing guards is open from the start");
            Assert.IsNull(UnlockBook.Grant(null, UnlockGoal.RoverSplat));
        }

        [Test]
        public void LockedPerks_AreTheSixUntilEarned()
        {
            HashSet<string> locked = UnlockBook.LockedPerkIds(new SaveData());
            CollectionAssert.AreEquivalent(new[]
            {
                "perk.patient_hunter", "perk.road_rage", PerkCatalog.ChainPerkId, "perk.trigger_discipline", "perk.in_your_face", "perk.all_in",
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
            UnlockBook.Grant(save, UnlockGoal.BankEarly);
            UnlockBook.Grant(save, UnlockGoal.ClearAnte2);
            CollectionAssert.AreEqual(new[] { StartingKits.StandardId, "kit.gunner", "kit.daredevil" }, UnlockBook.OpenKits(save).ConvertAll(k => k.id));
        }

        [Test]
        public void TheNextUnlock_IsTheFirstNotEarned_EasiestFirst()
        {
            var save = new SaveData();
            Assert.AreEqual(UnlockGoal.ClearAnte2, UnlockBook.Next(save).goal);
            UnlockBook.Grant(save, UnlockGoal.ClearAnte2);
            Assert.AreEqual(UnlockGoal.LowStanceKill, UnlockBook.Next(save).goal);
            UnlockBook.Grant(save, UnlockGoal.RoverSplat);
            Assert.AreEqual(UnlockGoal.LowStanceKill, UnlockBook.Next(save).goal, "a later one earned first does not move the next mark");
            Assert.IsNull(UnlockBook.Next(SaveWithEverything()));
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
        public void AVersion4Save_IsBroughtUpTo5_WithNothingUnlocked_AndStandardChosen()
        {
            var data = new SaveData { version = 4, runsEnded = 3, bestAnte = 3, unlocks = null, unlocksSeen = null, kit = null };
            Assert.IsTrue(SaveMigrations.Upgrade(data));
            Assert.AreEqual(5, data.version);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.IsNotNull(data.unlocks);
            Assert.IsEmpty(data.unlocks);
            Assert.IsNotNull(data.unlocksSeen);
            Assert.AreEqual(StartingKits.StandardId, data.kit);
            Assert.AreEqual(3, data.runsEnded, "records are kept");
            Assert.AreEqual(3, data.bestAnte);
        }

        [Test]
        public void ACurrentSave_WithMissingLists_IsMendedNotThrownAway()
        {
            var data = new SaveData { unlocks = null, unlocksSeen = null, kit = "" };
            Assert.IsTrue(SaveMigrations.Upgrade(data));
            Assert.IsNotNull(data.unlocks);
            Assert.IsNotNull(data.unlocksSeen);
            Assert.AreEqual(StartingKits.StandardId, data.kit);
        }

        // ---- locked perks in the drafts ----

        [Test]
        public void ALockedPerk_IsNeverOffered()
        {
            HashSet<string> locked = UnlockBook.LockedPerkIds(new SaveData());
            for (int seed = 1; seed <= 60; seed++)
            {
                AnteRun run = NewRun(seed);
                run.SetLocked(locked);
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

        // ---- starting kits ----

        [Test]
        public void AKit_GivesItsPerks_AndTheFirstTwoAntesHaveNoPickOfTheirOwn()
        {
            AnteRun run = NewRun(3);
            StartingKit ghost = StartingKits.Find("kit.ghost");
            Assert.IsTrue(run.StartWithKit(ghost));
            Assert.AreEqual("kit.ghost", run.KitId);
            Assert.IsTrue(run.Perks.Has("perk.eagle_eye"));
            Assert.IsTrue(run.Perks.Has("perk.cold_blood"));
            Assert.AreEqual(2, run.Director.PicksGivenAhead);

            for (int ante = 1; ante <= 2; ante++)
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

            MeetTarget(run, "k3");
            Assert.IsFalse(run.Director.PickGivenAhead);
            Assert.IsTrue(run.Director.PerkEarnedEarly, "ante 3 picks as usual");
            List<PerkDefinition> offers = run.PerkOffers();
            Assert.IsNotEmpty(offers);
            foreach (PerkDefinition p in offers)
                Assert.IsTrue(p.id != "perk.eagle_eye" && p.id != "perk.cold_blood", "a kit perk is held, so never offered again");
            Assert.IsTrue(run.Director.ConfirmAdvance());
            Assert.IsTrue(run.Director.PerkDraftPending, "ante 3's pick waits at the break if not taken in the field");
        }

        [Test]
        public void OverARun_AKitHoldsAsManyPerksAsPickingDoes()
        {
            AnteRun kit = NewRun(9), picks = NewRun(9);
            Assert.IsTrue(kit.StartWithKit(StartingKits.Find("kit.gunner")));
            for (int ante = 1; ante <= 4; ante++)
            {
                foreach (AnteRun run in new[] { kit, picks })
                {
                    MeetTarget(run, "r" + ante);
                    Assert.IsTrue(run.Director.ConfirmAdvance());
                    List<PerkDefinition> offers = run.PerkOffers();
                    if (run.Director.PerkDraftPending && offers.Count > 0) Assert.IsTrue(run.TakePerk(offers[0]));
                    run.BeginNextAnte();
                }
            }
            Assert.AreEqual(picks.Perks.Owned.Count, kit.Perks.Owned.Count, "a kit moves two picks to the start; it never adds one");
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
            Assert.AreEqual(2, state.picksGivenAhead);
            AnteRun back = AnteRun.Import(new AnteBalance(), state, RoguelitePerks.Create());
            Assert.AreEqual("kit.daredevil", back.KitId);
            Assert.AreEqual(2, back.Director.PicksGivenAhead);
            Assert.IsTrue(back.Perks.Has("perk.glass_cannon"));
            Assert.IsFalse(back.Director.PerkDraftPending);
            Assert.IsTrue(back.BeginNextAnte());
            MeetTarget(back, "i2");
            Assert.IsFalse(back.Director.PerkEarnedEarly, "ante 2's pick is still the kit's after a load");
        }

        [Test]
        public void KitWords_SayThePerksAndTheCost()
        {
            List<PerkDefinition> perks = Perks;
            StartingKit gunner = StartingKits.Find("kit.gunner");
            Assert.AreEqual("Start with Quick Study and Steady Hands.", StartingKits.Summary(gunner, perks));
            string details = StartingKits.Details(gunner, perks, new AnteBalance());
            StringAssert.StartsWith("Quick Study: ", details);
            StringAssert.Contains("\nSteady Hands: ", details);
            StringAssert.EndsWith(StartingKits.CostWords, details);
            StringAssert.Contains("every pick is yours", StartingKits.Summary(StartingKits.Standard, perks));
            Assert.AreEqual(string.Empty, StartingKits.Details(StartingKits.Standard, perks, new AnteBalance()));
        }
    }
}
