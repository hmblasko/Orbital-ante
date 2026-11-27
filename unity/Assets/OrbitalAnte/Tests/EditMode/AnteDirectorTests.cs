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
        public void TimerOutShortOfTheTargetEndsTheRunInEveryMode_UnderTheOldRule()
        {
            foreach (AnteEndCondition end in new[] { AnteEndCondition.OnTimer, AnteEndCondition.OnPlayerAdvance, AnteEndCondition.Hybrid })
            {
                AnteRun run = Run(end, 300, 300);
                run.Balance.missedTarget = MissedTarget.EndRun;
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
        public void ATickThatCrossesTheEndStillCountsAllOfItsPlayTime()
        {
            AnteRun run = Run(AnteEndCondition.OnPlayerAdvance, 300, 300);
            run.Report(new ScoreEvent { claimId = "perkless", kind = ScoreEventKind.OptionalMission, missionBasePoints = 300 });
            run.Tick(299f, false);
            run.Tick(40f, false);       // one second of timer left, forty seconds of play
            Assert.IsTrue(run.Director.WaitingForPlayer);
            Assert.AreEqual(339f, run.Director.RunSeconds, 0.001f);
            run.Tick(10f, false);
            Assert.AreEqual(349f, run.Director.RunSeconds, 0.001f);
        }

        [Test]
        public void ChangeOfPaceCannotReachAcrossATickThatCrossesTheEnd()
        {
            AnteRun run = Run(AnteEndCondition.OnPlayerAdvance, 300, 300);
            foreach (PerkDefinition perk in run.Catalog) if (perk.id == PerkCatalog.ChainPerkId) run.Perks.Add(perk);
            run.Tick(299f, false);
            run.Report(new ScoreEvent { claimId = "m", kind = ScoreEventKind.OptionalMission, missionBasePoints = 300 });
            run.Tick(40f, false);
            ScoreResult later = run.Report(new ScoreEvent { claimId = "e", kind = ScoreEventKind.EnemyTier1 });
            Assert.IsFalse(later.chainBonusUsed, "forty seconds have passed, the window is thirty");
            Assert.AreEqual(45, later.points);
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
            Assert.AreEqual(1370, run.Director.Target);
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

        // ---- Overtime (Matthew, 21 Nov 2026: the game ends only when the player dies) ----

        [Test]
        public void TimerOutShortOfTheTarget_GoesIntoOvertime_InEveryMode()
        {
            foreach (AnteEndCondition end in new[] { AnteEndCondition.OnTimer, AnteEndCondition.OnPlayerAdvance, AnteEndCondition.Hybrid })
            {
                AnteRun run = Run(end, 300, 300);
                Earn(run, 299);
                run.Tick(310f, false);
                Assert.AreEqual(AntePhase.Running, run.Director.Phase, end.ToString());
                Assert.IsTrue(run.Director.InOvertime, end.ToString());
                Assert.AreEqual(10f, run.Director.OvertimeSeconds, 0.001f, "the rest of the tick is overtime");
                Assert.AreEqual(0f, run.Director.SecondsLeft);
            }
        }

        [Test]
        public void Overtime_EndsTheMomentTheTargetIsMet_AndTheBreakStillHasItsPerk()
        {
            AnteRun run = Run(AnteEndCondition.Hybrid, 300, 300);
            run.Tick(300f, false);
            Assert.IsTrue(run.Director.InOvertime);
            run.Tick(40f, false);
            Assert.AreEqual(40f, run.Director.OvertimeSeconds, 0.001f);
            Earn(run, 300);
            run.Tick(0.1f, false);
            Assert.AreEqual(AntePhase.Intermission, run.Director.Phase);
            Assert.IsFalse(run.Director.InOvertime);
            Assert.IsTrue(run.Director.ClearedInOvertime);
            Assert.IsTrue(run.Director.PerkDraftPending, "Matthew: perks continue even in overtime");
            Assert.IsTrue(run.Director.ScratchPending);
        }

        [Test]
        public void Overtime_DoesNotCarryIntoTheNextAnte()
        {
            AnteRun run = Run(AnteEndCondition.Hybrid, 300, 300);
            run.Tick(300f, false);
            Earn(run, 300);
            run.Tick(1f, false);
            Assert.IsTrue(run.BeginNextAnte());
            Assert.IsFalse(run.Director.InOvertime);
            Assert.IsFalse(run.Director.ClearedInOvertime);
            Assert.AreEqual(0f, run.Director.OvertimeSeconds);
            Assert.AreEqual(300f, run.Director.SecondsLeft);
        }

        [Test]
        public void Overtime_PausedTicksAndTimeBackDoNothing_AndDyingStillEndsTheRun()
        {
            AnteRun run = Run(AnteEndCondition.Hybrid, 300, 300);
            run.Tick(300f, false);
            run.Tick(50f, true);
            Assert.AreEqual(0f, run.Director.OvertimeSeconds);
            run.Director.AdjustSeconds(30f);
            Assert.AreEqual(0f, run.Director.SecondsLeft, "no time back once the clock has run out");
            Assert.IsFalse(run.Director.UseStopwatch());
            Assert.IsFalse(run.Director.ConfirmAdvance(), "can't end an ante whose target isn't met");
            Assert.IsTrue(run.Director.InOvertime);
            run.Director.PlayerDied();
            Assert.AreEqual(AntePhase.Failed, run.Director.Phase);
            Assert.IsFalse(run.Director.InOvertime, "a death ends overtime too");
        }

        [Test]
        public void TheLastAnte_ClearedInOvertime_IsStillAVictory()
        {
            AnteRun run = Run(AnteEndCondition.Hybrid, 300);
            run.Tick(300f, false);
            Earn(run, 300);
            run.Tick(1f, false);
            Assert.AreEqual(AntePhase.Victory, run.Director.Phase);
        }

        [Test]
        public void KeepGoing_AfterTheLastAnte_GivesABreakThenBiggerTargets_UntilDeath()
        {
            AnteRun run = Run(AnteEndCondition.Hybrid, 300, 400);
            Earn(run, 300);
            Assert.IsTrue(run.Director.ConfirmAdvance());
            Assert.IsTrue(run.BeginNextAnte());
            Earn(run, 400);
            Assert.IsTrue(run.Director.ConfirmAdvance());
            Assert.AreEqual(AntePhase.Victory, run.Director.Phase);

            Assert.IsTrue(run.Director.ContinueEndless());
            Assert.IsFalse(run.Director.ContinueEndless(), "only from the victory page");
            Assert.AreEqual(AntePhase.Intermission, run.Director.Phase);
            Assert.IsTrue(run.Director.PerkDraftPending);
            Assert.IsTrue(run.BeginNextAnte());
            Assert.AreEqual(3, run.Director.Ante);
            Assert.AreEqual(500, run.Director.Target, "400 x 1.25");
            Assert.IsFalse(run.Director.IsLastAnte);
            Earn(run, 500);
            Assert.IsTrue(run.Director.ConfirmAdvance());
            Assert.AreEqual(AntePhase.Intermission, run.Director.Phase, "endless antes never end in victory");
            Assert.IsTrue(run.BeginNextAnte());
            Assert.AreEqual(630, run.Director.Target, "625 rounded to 10");
            run.Director.PlayerDied();
            Assert.AreEqual(AntePhase.Failed, run.Director.Phase);
        }

        [Test]
        public void KeepGoingAndOvertime_SurviveASaveRoundTrip()
        {
            AnteRun run = Run(AnteEndCondition.Hybrid, 300);
            Earn(run, 300);
            Assert.IsTrue(run.Director.ConfirmAdvance());
            Assert.IsTrue(run.Director.ContinueEndless());
            Assert.IsTrue(run.BeginNextAnte());
            Assert.IsTrue(run.BeginNextAnte() == false);
            run.Tick(300f, false);
            run.Tick(12f, false);
            Assert.IsTrue(run.Director.InOvertime);

            AnteRun back = AnteRun.Import(run.Balance, run.Export());
            Assert.AreEqual(2, back.Director.Ante, "an endless ante is not clamped to the last listed one");
            Assert.IsTrue(back.Director.Endless);
            Assert.IsTrue(back.Director.InOvertime);
            Assert.AreEqual(12f, back.Director.OvertimeSeconds, 0.001f);
            Assert.AreEqual(run.Director.Target, back.Director.Target);
        }

        [Test]
        public void EndlessTargets_KeepTheListedOnes_AndNeverOverflow()
        {
            var b = new AnteBalance();
            for (int a = 1; a <= b.AnteCount; a++) Assert.AreEqual(b.TargetFor(a), b.EndlessTargetFor(a));
            Assert.Greater(b.EndlessTargetFor(10), b.TargetFor(9));
            Assert.Greater(b.EndlessTargetFor(500), 0);
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
