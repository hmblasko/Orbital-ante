using NUnit.Framework;
using OrbitalAnte.Ante;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// The ante clock and the moves between antes: thresholds, the three ways an ante can end, the stopwatch,
    /// pausing, and that Select never ends anything. These run without Unity objects.
    /// </summary>
    public class AnteDirectorTests
    {
        static AnteRun Run(AnteEndCondition end, params int[] targets)
        {
            var balance = new AnteBalance { endCondition = end };
            if (targets.Length > 0) balance.anteTargets = targets;
            return new AnteRun(balance, 11);
        }

        static void Earn(AnteRun run, int points)
        {
            // Missions are worth 250 to 550, so this pays "points" exactly for values in that range.
            run.Report(new ScoreEvent { claimId = "mission:" + run.Claims.ClaimCount, kind = ScoreEventKind.OptionalMission, missionBasePoints = points });
        }

        [Test]
        public void ARunStartsInAnteOneWithAFullTimer()
        {
            AnteRun run = Run(AnteEndCondition.Hybrid);
            Assert.AreEqual(1, run.Director.Ante);
            Assert.AreEqual(AntePhase.Running, run.Director.Phase);
            Assert.AreEqual(300f, run.Director.SecondsLeft);
            Assert.AreEqual(850, run.Director.Target);
        }

        [Test]
        public void PauseStopsTheTimer()
        {
            AnteRun run = Run(AnteEndCondition.Hybrid);
            run.Tick(10f, false);
            run.Tick(500f, true);
            Assert.AreEqual(290f, run.Director.SecondsLeft, 0.001f);
            Assert.AreEqual(AntePhase.Running, run.Director.Phase);
        }

        [Test]
        public void TimerOutShortOfTheTargetEndsTheRunInEveryMode()
        {
            foreach (AnteEndCondition end in new[] { AnteEndCondition.OnTimer, AnteEndCondition.OnPlayerAdvance, AnteEndCondition.Hybrid })
            {
                AnteRun run = Run(end, 300, 300);
                Earn(run, 299);
                run.Tick(300f, false);
                Assert.AreEqual(AntePhase.Failed, run.Director.Phase, end.ToString());
            }
        }

        [Test]
        public void ExactlyTheTargetIsEnough()
        {
            AnteRun run = Run(AnteEndCondition.OnTimer, 300, 300);
            Earn(run, 300);
            run.Tick(300f, false);
            Assert.AreEqual(AntePhase.Intermission, run.Director.Phase);
        }

        [Test]
        public void OnTimerNeverLetsThePlayerLeaveEarly()
        {
            AnteRun run = Run(AnteEndCondition.OnTimer, 300, 300);
            Earn(run, 500);
            Assert.IsFalse(run.Director.CanAdvance);
            Assert.IsFalse(run.Director.ConfirmAdvance());
            Assert.AreEqual(AntePhase.Running, run.Director.Phase);
            run.Tick(300f, false);
            Assert.AreEqual(AntePhase.Intermission, run.Director.Phase);
        }

        [Test]
        public void HybridLetsThePlayerLeaveEarlyOrWaitForTheTimer()
        {
            AnteRun early = Run(AnteEndCondition.Hybrid, 300, 300);
            Assert.IsFalse(early.Director.ConfirmAdvance(), "not before the target is met");
            Earn(early, 300);
            Assert.IsTrue(early.Director.ConfirmAdvance());
            Assert.AreEqual(AntePhase.Intermission, early.Director.Phase);

            AnteRun late = Run(AnteEndCondition.Hybrid, 300, 300);
            Earn(late, 300);
            late.Tick(300f, false);
            Assert.AreEqual(AntePhase.Intermission, late.Director.Phase);
        }

        [Test]
        public void OnPlayerAdvanceWaitsForThePlayerOnceTheTargetIsMet()
        {
            AnteRun run = Run(AnteEndCondition.OnPlayerAdvance, 300, 300);
            Earn(run, 300);
            run.Tick(900f, false);
            Assert.AreEqual(AntePhase.Running, run.Director.Phase);
            Assert.IsTrue(run.Director.WaitingForPlayer);
            Earn(run, 250);
            Assert.AreEqual(550, run.Ledger.AnteEarned, "still scoring while waiting");
            Assert.IsTrue(run.Director.ConfirmAdvance());
            Assert.AreEqual(AntePhase.Intermission, run.Director.Phase);
        }

        [Test]
        public void SelectNeverEndsAnAnte()
        {
            foreach (AnteEndCondition end in new[] { AnteEndCondition.OnTimer, AnteEndCondition.OnPlayerAdvance, AnteEndCondition.Hybrid })
            {
                AnteRun run = Run(end, 300, 300);
                Earn(run, 400);
                run.Tick(5f, false);
                for (int i = 0; i < 20; i++) Assert.AreEqual(SelectView.RunStatus, run.Director.ViewForSelect());
                Assert.AreEqual(AntePhase.Running, run.Director.Phase, end.ToString());
                Assert.AreEqual(1, run.Director.Ante);
                Assert.AreEqual(295f, run.Director.SecondsLeft, 0.001f);
            }
        }

        [Test]
        public void SelectShowsTheUpgradeScreenOnlyBetweenAntes()
        {
            AnteRun run = Run(AnteEndCondition.Hybrid, 300, 300);
            Earn(run, 300);
            run.Director.ConfirmAdvance();
            Assert.AreEqual(SelectView.UpgradeScreen, run.Director.ViewForSelect());
            run.BeginNextAnte();
            Assert.AreEqual(SelectView.RunStatus, run.Director.ViewForSelect());
            run.Director.PlayerDied();
            Assert.AreEqual(SelectView.RunSummary, run.Director.ViewForSelect());
        }

        [Test]
        public void StopwatchAddsThirtySecondsUpToNinetyPerAnte()
        {
            AnteRun run = Run(AnteEndCondition.Hybrid, 300, 300);
            Assert.IsTrue(run.Director.UseStopwatch());
            Assert.IsTrue(run.Director.UseStopwatch());
            Assert.IsTrue(run.Director.UseStopwatch());
            Assert.IsFalse(run.Director.UseStopwatch(), "the fourth would go past +90");
            Assert.AreEqual(390f, run.Director.SecondsLeft, 0.001f);

            Earn(run, 300);
            run.Director.ConfirmAdvance();
            run.BeginNextAnte();
            Assert.AreEqual(300f, run.Director.SecondsLeft, 0.001f, "stopwatch time does not carry over");
            Assert.IsTrue(run.Director.UseStopwatch(), "the allowance is per ante");
        }

        [Test]
        public void TheNextAnteStartsFromZeroWithTheNextTarget()
        {
            AnteRun run = Run(AnteEndCondition.Hybrid);
            Earn(run, 550);
            Earn(run, 550);
            Assert.AreEqual(1100, run.Ledger.AnteEarned);
            run.Director.ConfirmAdvance();
            run.BeginNextAnte();
            Assert.AreEqual(2, run.Director.Ante);
            Assert.AreEqual(1100, run.Director.Target);
            Assert.AreEqual(0, run.Ledger.AnteEarned, "the 250 over the first target does not help with the second");
            Assert.IsFalse(run.Director.TargetMet);
            Assert.AreEqual(1100, run.Ledger.SpendablePoints);
        }

        [Test]
        public void TheTimerIsStoppedBetweenAntes()
        {
            AnteRun run = Run(AnteEndCondition.Hybrid, 300, 300);
            Earn(run, 300);
            run.Director.ConfirmAdvance();
            run.Tick(10000f, false);
            Assert.AreEqual(AntePhase.Intermission, run.Director.Phase);
            Assert.IsFalse(run.BeginNextAnte() && run.Director.SecondsLeft < 300f);
        }

        [Test]
        public void ClearingTheLastAnteIsVictory()
        {
            AnteRun run = Run(AnteEndCondition.Hybrid, 300, 300);
            Earn(run, 300);
            run.Director.ConfirmAdvance();
            run.BeginNextAnte();
            Earn(run, 300);     // worth more in ante 2, still enough
            run.Director.ConfirmAdvance();
            Assert.AreEqual(AntePhase.Victory, run.Director.Phase);
            Assert.IsFalse(run.BeginNextAnte());
        }

        [Test]
        public void AllNineAntesCanBePlayedThrough()
        {
            AnteRun run = Run(AnteEndCondition.Hybrid);
            for (int ante = 1; ante <= 9; ante++)
            {
                Assert.AreEqual(ante, run.Director.Ante);
                while (!run.Director.TargetMet) Earn(run, 550);
                Assert.IsTrue(run.Director.ConfirmAdvance());
                if (ante < 9) Assert.IsTrue(run.BeginNextAnte());
            }
            Assert.AreEqual(AntePhase.Victory, run.Director.Phase);
        }

        [Test]
        public void DyingEndsTheRun()
        {
            AnteRun run = Run(AnteEndCondition.Hybrid);
            run.Director.PlayerDied();
            Assert.AreEqual(AntePhase.Failed, run.Director.Phase);
            Assert.IsFalse(run.Director.ConfirmAdvance());
        }

        [Test]
        public void TheClockSurvivesSaveAndLoad()
        {
            var balance = new AnteBalance();
            var run = new AnteRun(balance, 11);
            run.Director.UseStopwatch();
            run.Tick(42.5f, false);
            Earn(run, 400);

            AnteRun loaded = AnteRun.Import(balance, run.Export());
            Assert.AreEqual(1, loaded.Director.Ante);
            Assert.AreEqual(287.5f, loaded.Director.SecondsLeft, 0.001f);
            Assert.AreEqual(30f, loaded.Director.StopwatchSecondsAdded, 0.001f);
            Assert.AreEqual(400, loaded.Ledger.AnteEarned);
            Assert.AreEqual(11, loaded.Seed);
        }
    }
}
