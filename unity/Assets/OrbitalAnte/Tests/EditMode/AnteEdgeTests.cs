using System;
using NUnit.Framework;
using OrbitalAnte.Ante;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Edges of the point rules: every kind of event against the three ledgers, the exact moment the timer
    /// runs out, odd tick sizes, very large numbers, and the same seed twice. These run without Unity objects.
    /// </summary>
    public class AnteEdgeTests
    {
        [Test]
        public void EveryKindOfEventFeedsTheThreeLedgersEquallyOrNotAtAll()
        {
            foreach (ScoreEventKind kind in Enum.GetValues(typeof(ScoreEventKind)))
            {
                var run = new AnteRun(new AnteBalance(), 3);
                ScoreResult result = run.Report(new ScoreEvent { claimId = "one:" + kind, kind = kind, capGroup = "base", missionBasePoints = 300 });
                Assert.AreEqual(result.points, run.Ledger.AnteEarned, kind.ToString());
                Assert.AreEqual(result.points, run.Ledger.LifetimeRunScore, kind.ToString());
                Assert.AreEqual(result.points, run.Ledger.SpendablePoints, kind.ToString());
                Assert.Greater(result.points, 0, kind.ToString());
                Assert.AreEqual(result.Awarded, run.Claims.IsClaimed("one:" + kind), kind.ToString());
            }
        }

        [Test]
        public void EveryKindHasACategory()
        {
            foreach (ScoreEventKind kind in Enum.GetValues(typeof(ScoreEventKind)))
                Assert.IsTrue(Enum.IsDefined(typeof(ScoreCategory), AnteBalance.CategoryOf(kind)), kind.ToString());
            Assert.AreEqual(ScoreCategory.Combat, AnteBalance.CategoryOf(ScoreEventKind.EnemyElite));
            Assert.AreEqual(ScoreCategory.Salvage, AnteBalance.CategoryOf(ScoreEventKind.SalvageDelivered));
            Assert.AreEqual(ScoreCategory.Landing, AnteBalance.CategoryOf(ScoreEventKind.DifficultLanding));
        }

        [Test]
        public void JustBeforeTheEndTheAnteIsStillOpen()
        {
            var run = new AnteRun(new AnteBalance(), 3);
            run.Tick(299.5f, false);
            Assert.AreEqual(AntePhase.Running, run.Director.Phase);
            Assert.AreEqual(0.5f, run.Director.SecondsLeft, 0.001f);
            run.Tick(0.5f, false);
            Assert.AreEqual(AntePhase.Failed, run.Director.Phase, "exactly at the end, with the target not met");
            Assert.AreEqual(0f, run.Director.SecondsLeft);
        }

        [Test]
        public void ZeroAndNegativeTicksDoNothing()
        {
            var run = new AnteRun(new AnteBalance(), 3);
            run.Tick(0f, false);
            run.Tick(-50f, false);
            Assert.AreEqual(300f, run.Director.SecondsLeft);
            Assert.AreEqual(0f, run.Director.RunSeconds);
            Assert.AreEqual(AntePhase.Running, run.Director.Phase);
        }

        [Test]
        public void AHugeTickEndsTheAnteOnceAndNeverGoesBelowZero()
        {
            var run = new AnteRun(new AnteBalance(), 3);
            run.Tick(1e9f, false);
            Assert.AreEqual(AntePhase.Failed, run.Director.Phase);
            Assert.AreEqual(0f, run.Director.SecondsLeft);
            run.Tick(1e9f, false);
            Assert.AreEqual(AntePhase.Failed, run.Director.Phase);
        }

        [Test]
        public void AStopwatchUsedInTheLastMomentStillCounts()
        {
            var run = new AnteRun(new AnteBalance(), 3);
            run.Tick(299.9f, false);
            Assert.IsTrue(run.Director.UseStopwatch());
            run.Tick(20f, false);
            Assert.AreEqual(AntePhase.Running, run.Director.Phase);
            Assert.AreEqual(10.1f, run.Director.SecondsLeft, 0.01f);
        }

        [Test]
        public void NoStopwatchAfterTheRunHasEndedOrBetweenAntes()
        {
            var ended = new AnteRun(new AnteBalance(), 3);
            ended.Tick(300f, false);
            Assert.IsFalse(ended.Director.UseStopwatch());

            var between = new AnteRun(new AnteBalance { anteTargets = new[] { 250, 250 } }, 3);
            between.Report(new ScoreEvent { claimId = "m", kind = ScoreEventKind.OptionalMission, missionBasePoints = 250 });
            between.Director.ConfirmAdvance();
            Assert.IsFalse(between.Director.UseStopwatch());
        }

        [Test]
        public void LedgersStopAtTheLargestNumberInsteadOfWrappingRound()
        {
            var ledger = new RunLedger();
            ledger.AddScore(int.MaxValue);
            ledger.AddScore(1000);
            ledger.AddBankOnly(1000);
            Assert.AreEqual(int.MaxValue, ledger.AnteEarned);
            Assert.AreEqual(int.MaxValue, ledger.LifetimeRunScore);
            Assert.AreEqual(int.MaxValue, ledger.SpendablePoints);
        }

        [Test]
        public void ZeroAndNegativeAmountsNeverChangeALedger()
        {
            var ledger = new RunLedger();
            ledger.AddScore(100);
            ledger.AddScore(0);
            ledger.AddScore(-500);
            ledger.AddBankOnly(-500);
            Assert.AreEqual(100, ledger.LifetimeRunScore);
            Assert.AreEqual(100, ledger.SpendablePoints);
        }

        [Test]
        public void AnAbsurdAwardStopsAtTheLargestNumber()
        {
            var balance = new AnteBalance();
            Assert.AreEqual(int.MaxValue, ScoreMath.Award(balance, int.MaxValue, 9, 0.25f, 1.2f, 1.25f));
            Assert.AreEqual(0, ScoreMath.Award(balance, 0, 9, 0f, 1f, 1f));
            Assert.AreEqual(0, ScoreMath.Award(balance, -40, 9, 0f, 1f, 1f));
        }

        [Test]
        public void AnAnteNumberBelowOneIsTreatedAsTheFirst()
        {
            var balance = new AnteBalance();
            Assert.AreEqual(1m, ScoreMath.ValueScale(balance, 0));
            Assert.AreEqual(1m, ScoreMath.ValueScale(balance, -3));
            Assert.AreEqual(850, balance.TargetFor(0));
            Assert.AreEqual(5500, balance.TargetFor(99));
        }

        [Test]
        public void TheSameSeedAndTheSameActionsGiveTheSameRun()
        {
            AnteRunState a = PlayThrough(4242).Export();
            AnteRunState b = PlayThrough(4242).Export();
            Assert.AreEqual(a.lifetimeRunScore, b.lifetimeRunScore);
            Assert.AreEqual(a.spendablePoints, b.spendablePoints);
            Assert.AreEqual(a.ante, b.ante);
            CollectionAssert.AreEqual(a.perkIds, b.perkIds);
            CollectionAssert.AreEqual(a.claims, b.claims);
            Assert.AreEqual(a.repairRefillVouchers, b.repairRefillVouchers);
            Assert.AreEqual(a.cosmeticTokens, b.cosmeticTokens);
        }

        static AnteRun PlayThrough(int seed)
        {
            var run = new AnteRun(new AnteBalance(), seed);
            int n = 0;
            for (int ante = 1; ante <= 4; ante++)
            {
                while (!run.Director.TargetMet)
                    run.Report(new ScoreEvent { claimId = "m" + n++, kind = ScoreEventKind.OptionalMission, missionBasePoints = 400 });
                run.Director.ConfirmAdvance();
                run.TakePerk(run.PerkOffers()[0]);
                run.UseScratchOff();
                run.TryBuyAttribute(PlayerAttribute.Oxygen);
                run.BeginNextAnte();
            }
            return run;
        }
    }
}
