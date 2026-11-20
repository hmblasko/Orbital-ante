using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Ante;
using OrbitalAnte.Tasks;

namespace OrbitalAnte.Tests
{
    /// <summary>The ante tasks' rules with no Unity objects (20 Nov 2026): style, streak, bases, masts, salvage, drops, landings, ids.</summary>
    public class TaskRulesTests
    {
        static TaskRules Rules() => new TaskRules();

        [Test]
        public void TheDefaultDials_HaveNoProblems()
        {
            CollectionAssert.IsEmpty(Rules().Problems());
        }

        [Test]
        public void APlainKill_HasNoFeats_AndEachStyleAddsOne()
        {
            Assert.AreEqual(0, StyleKill.Feats(new KillFacts(), out string none));
            Assert.AreEqual(string.Empty, none);
            Assert.AreEqual(1, StyleKill.Feats(new KillFacts { headshot = true }, out string one));
            Assert.AreEqual("HEADSHOT", one);
            Assert.AreEqual(2, StyleKill.Feats(new KillFacts { headshot = true, airborne = true }, out string two));
            Assert.AreEqual("HEADSHOT + AIRBORNE", two);
        }

        [Test]
        public void Skill_IsOneForNothing_TheVerifiedBonusForOneFeat_AndTheCeilingForTwoOrMore()
        {
            var balance = new AnteBalance();
            Assert.AreEqual(1f, StyleKill.SkillFor(balance, 0));
            Assert.AreEqual(balance.skillVerified, StyleKill.SkillFor(balance, 1));
            Assert.AreEqual(balance.skillMax, StyleKill.SkillFor(balance, 2));
            Assert.AreEqual(balance.skillMax, StyleKill.SkillFor(balance, 5));
        }

        [Test]
        public void AFreshGun_IsOneNotAmongTheLastFewKills_AndTheFirstKillIsNeverFresh()
        {
            var fresh = new GunFreshness(3);
            Assert.IsFalse(fresh.IsFresh("Rifle"), "the run's first kill is not a switch, so it is not a feat");
            fresh.Remember("Rifle");
            Assert.IsFalse(fresh.IsFresh("Rifle"));
            fresh.Remember("Pistol");
            fresh.Remember("melee");
            fresh.Remember("rover");
            Assert.IsTrue(fresh.IsFresh("Rifle"), "three other kills later the rifle is fresh again");
        }

        [Test]
        public void AStreak_GrowsWithinItsWindow_AndStartsAgainAfter()
        {
            var streak = new Streak(8f);
            Assert.AreEqual(1, streak.Add(0f));
            Assert.AreEqual(2, streak.Add(5f));
            Assert.AreEqual(3, streak.Add(12f));
            Assert.AreEqual(3, streak.CountAt(19f));
            Assert.AreEqual(0, streak.CountAt(20.5f));
            Assert.AreEqual(1, streak.Add(30f));
            Assert.AreEqual(3, streak.Best);
        }

        [Test]
        public void SpendingAStreak_LeavesOneSoItMustBeBuiltAgain()
        {
            var streak = new Streak(8f);
            for (int i = 0; i < 5; i++) streak.Add(i);
            streak.Spend(5f);
            Assert.AreEqual(1, streak.CountAt(5f));
        }

        [Test]
        public void ABase_IsClearedByItsLastGuard_Once()
        {
            var b = new BaseClear(1, 3);
            Assert.IsFalse(b.GuardDown(1f));
            Assert.IsFalse(b.GuardDown(2f));
            Assert.IsTrue(b.GuardDown(3f));
            Assert.IsFalse(b.GuardDown(4f), "a base clears once");
            Assert.AreEqual(0, b.Left);
        }

        [Test]
        public void ABaseCleared_Unseen_AndFast_HasBothFeats()
        {
            var rules = Rules();
            var b = new BaseClear(0, 2);
            b.Engaged(10f);
            b.GuardDown(20f);
            b.GuardDown(30f);
            Assert.AreEqual(2, b.Feats(rules, 30f, out string label));
            Assert.AreEqual("QUIET + QUICK", label);
        }

        [Test]
        public void ABaseCleared_AfterBeingSeen_AndSlowly_HasNoFeats()
        {
            var rules = Rules();
            var b = new BaseClear(0, 1);
            b.Engaged(0f);
            b.Alarm();
            b.GuardDown(rules.baseQuickSeconds + 1f);
            Assert.AreEqual(0, b.Feats(rules, rules.baseQuickSeconds + 1f, out _));
        }

        [Test]
        public void BiggerBases_PayMore()
        {
            var rules = Rules();
            Assert.Less(rules.BaseClearPoints(0), rules.BaseClearPoints(1));
            Assert.Less(rules.BaseClearPoints(1), rules.BaseClearPoints(2));
        }

        [Test]
        public void AMast_FallsOnlyWhenEveryCellIsBroken()
        {
            var mast = new SignalMast(3, 45f, 45f);
            Assert.IsFalse(mast.Hit(0, 50f, 1f));
            Assert.IsFalse(mast.Hit(1, 50f, 2f));
            Assert.IsFalse(mast.Hit(1, 50f, 2.5f), "a broken cell takes no more");
            Assert.IsFalse(mast.Down);
            Assert.IsTrue(mast.Hit(2, 50f, 3f));
            Assert.IsTrue(mast.Down);
            Assert.AreEqual(2f, mast.BrokenWithin, 1e-4f);
        }

        [Test]
        public void ARammedMast_FallsAtOnce()
        {
            var mast = new SignalMast(3, 45f, 45f);
            Assert.IsTrue(mast.Rammed(5f));
            Assert.IsTrue(mast.Down);
            Assert.IsFalse(mast.Rammed(6f), "it only falls once");
        }

        [Test]
        public void AMast_NeverCallsTheMomentYouArrive_ThenCallsEveryInterval()
        {
            var mast = new SignalMast(3, 45f, 45f);
            Assert.IsFalse(mast.Calls(0f, true));
            Assert.IsFalse(mast.Calls(44f, true));
            Assert.IsTrue(mast.Calls(45f, true));
            Assert.IsFalse(mast.Calls(60f, true));
            Assert.IsTrue(mast.Calls(90f, true));
            Assert.IsFalse(mast.Calls(100f, false), "out of reach it stops counting");
            Assert.IsFalse(mast.Calls(101f, true), "and coming back starts the interval again");
            mast.Rammed(102f);
            Assert.IsFalse(mast.Calls(200f, true), "a fallen mast never calls");
        }

        [Test]
        public void Salvage_IsDeliveredOnlyWhileCarried_AndRemembersBeingHurt()
        {
            var crate = new SalvageCrate();
            Assert.IsFalse(crate.Deliver());
            Assert.IsTrue(crate.PickUp());
            crate.Hurt();
            Assert.IsTrue(crate.Drop());
            Assert.IsTrue(crate.PickUp());
            Assert.IsTrue(crate.Deliver());
            Assert.IsTrue(crate.HurtWhileCarried);
            Assert.IsFalse(crate.PickUp(), "delivered salvage is gone");
        }

        [Test]
        public void ADrop_FallsLandsAndTimesOut_AndCountsAsContestedWhenAnEnemyGetsThere()
        {
            var rules = Rules();
            var drop = new SupplyDrop(rules, 0f);
            Assert.IsFalse(drop.Open(), "it cannot be opened in the air");
            drop.EnemyNear(0f);
            Assert.IsFalse(drop.Reached, "an enemy under it in the air does not count");
            Assert.IsTrue(drop.Tick(rules.dropFallSeconds));
            Assert.AreEqual(DropState.Landed, drop.State);
            drop.EnemyNear(rules.dropReachedWithin + 1f);
            Assert.IsFalse(drop.Reached);
            drop.EnemyNear(1f);
            Assert.IsTrue(drop.Reached);
            Assert.IsTrue(drop.Open());
            Assert.IsFalse(drop.Open());

            var late = new SupplyDrop(rules, 0f);
            late.Tick(rules.dropFallSeconds);
            late.Tick(rules.dropFallSeconds + rules.dropLasts + 0.1f);
            Assert.AreEqual(DropState.Gone, late.State);
        }

        [Test]
        public void Drops_ComeAtTheirTimes_OncePerSlot_AndOnlyDuringAnAnte()
        {
            var rules = Rules();
            var clock = new DropClock(rules);
            Assert.IsFalse(clock.Due(1000f), "no ante, no drops");
            clock.AnteStarted(100f);
            Assert.IsFalse(clock.Due(100f + rules.dropFirstAfter - 0.1f));
            Assert.IsTrue(clock.Due(100f + rules.dropFirstAfter));
            Assert.IsFalse(clock.Due(100f + rules.dropFirstAfter + 1f));
            Assert.IsTrue(clock.Due(100f + rules.dropFirstAfter + rules.dropEvery));
            Assert.AreEqual(2, clock.Given);
            clock.AnteStarted(500f);
            Assert.AreEqual(0, clock.Given);
        }

        [Test]
        public void EnemyKinds_ScoreByHowHardTheyAre()
        {
            Assert.AreEqual(ScoreEventKind.EnemyTier1, EnemyTiers.KindFor("Trooper", 1f));
            Assert.AreEqual(ScoreEventKind.EnemyTier1, EnemyTiers.KindFor("Runner", 0.85f));
            Assert.AreEqual(ScoreEventKind.EnemyTier2, EnemyTiers.KindFor("Marksman", 1.2f));
            Assert.AreEqual(ScoreEventKind.EnemyTier3, EnemyTiers.KindFor("Bruiser", 1.3f));
            Assert.AreEqual(ScoreEventKind.EnemyTier1, EnemyTiers.KindFor("ScoutA", 0.85f));
            Assert.AreEqual(ScoreEventKind.EnemyTier2, EnemyTiers.KindFor("LineA", 1f));
            Assert.AreEqual(ScoreEventKind.EnemyTier3, EnemyTiers.KindFor("HeavyA", 1.3f));
            Assert.AreEqual(ScoreEventKind.EnemyTier3, EnemyTiers.KindFor("SomethingNew", 1.4f), "a new big kind scores as heavy");
        }

        [Test]
        public void AHardLanding_IsGroundWellAboveOrBelowWhatSurroundsIt()
        {
            var flat = new List<double> { 1000, 1001, 999, 1000 };
            Assert.IsFalse(LandingRelief.IsHard(1000.5, flat, 12f));
            Assert.IsTrue(LandingRelief.IsHard(1015, flat, 12f), "a tower top");
            Assert.IsTrue(LandingRelief.IsHard(985, flat, 12f), "a canyon floor");
            Assert.IsFalse(LandingRelief.IsHard(1015, new List<double>(), 12f));
        }

        [Test]
        public void LandingCells_AreTheSameNearby_AndDifferFarApart()
        {
            string a = LandingRelief.Cell(1000, 0, 0, 1000, 300f);
            string b = LandingRelief.Cell(1000, 1, 1, 1000, 300f);
            string c = LandingRelief.Cell(0, 1000, 0, 1000, 300f);
            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, c);
        }

        [Test]
        public void ClaimIds_AreDistinctPerThing()
        {
            var ids = new HashSet<string>
            {
                TaskIds.Enemy("Mars", 1, 2), TaskIds.Enemy("Mars", 2, 1), TaskIds.BaseClear("Mars", 3), TaskIds.Mast("Mars", 3),
                TaskIds.Salvage("Mars", 0), TaskIds.Drop(1, 0), TaskIds.Drop(2, 0), TaskIds.FirstLanding("Mars"), TaskIds.HardLanding("Mars", "1.2"),
            };
            Assert.AreEqual(9, ids.Count);
        }

        [Test]
        public void TwoFeats_ScoreAtTheSkillCeiling_InTheAnteRules()
        {
            var balance = new AnteBalance();
            var plain = new AnteRun(balance, 1);
            var styled = new AnteRun(balance, 1);
            int a = plain.Report(new ScoreEvent { claimId = "x", kind = ScoreEventKind.EnemyTier1 }).points;
            int b = styled.Report(new ScoreEvent { claimId = "x", kind = ScoreEventKind.EnemyTier1, skillFeats = 2 }).points;
            Assert.AreEqual(ScoreMath.Award(balance, balance.BasePoints(ScoreEventKind.EnemyTier1), 1, 0f, balance.skillMax, 1f), b);
            Assert.Greater(b, a);
        }

        [Test]
        public void HardLandings_AreCappedEachAnte()
        {
            var balance = new AnteBalance();
            var run = new AnteRun(balance, 1);
            for (int i = 0; i < balance.difficultLandingsPerAnte; i++)
                Assert.IsTrue(run.Report(new ScoreEvent { claimId = "hard" + i, kind = ScoreEventKind.DifficultLanding }).Awarded);
            Assert.IsFalse(run.Report(new ScoreEvent { claimId = "hard.more", kind = ScoreEventKind.DifficultLanding }).Awarded);
        }

        [Test]
        public void AMastDown_ScoresAsAnInstallation()
        {
            var balance = new AnteBalance();
            var run = new AnteRun(balance, 1);
            ScoreResult r = run.Report(new ScoreEvent { claimId = "mast:Mars.0", kind = ScoreEventKind.InstallationDestroyed });
            Assert.IsTrue(r.Awarded);
            Assert.AreEqual(balance.installationDestroyed, r.basePoints);
            Assert.AreEqual(ScoreCategory.Combat, r.category);
        }

        [Test]
        public void TheGunLadder_CanSkipAStepForASupplyDrop_ButNotPastTheTop()
        {
            var real = new HashSet<string> { "Pistol", "Smg", "Rifle", "Shotgun", "Sniper" };
            var ladder = new Combat.GunLadder(new Combat.GunLadderRules(), id => real.Contains(id));
            ladder.AddKill();
            Assert.IsTrue(ladder.SkipStep());
            Assert.AreEqual(1, ladder.Rung);
            Assert.AreEqual(0, ladder.KillsOnRung);
            while (ladder.SkipStep()) { }
            Assert.IsTrue(ladder.AtTop);
            Assert.IsFalse(ladder.SkipStep());
        }
    }
}
