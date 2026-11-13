using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Ante;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// The point system's rules: the award formula and its caps, the three ledgers, and the promise that
    /// nothing pays twice. These run without Unity objects.
    /// </summary>
    public class AnteScoringTests
    {
        static ScoreEvent Kill(string id, ScoreEventKind kind = ScoreEventKind.EnemyTier1)
        {
            return new ScoreEvent { claimId = id, kind = kind };
        }

        // ---- Formula ----

        [Test]
        public void ValueScaleMatchesTheHandoffFormula()
        {
            var balance = new AnteBalance();
            Assert.AreEqual(1.000m, ScoreMath.ValueScale(balance, 1));
            Assert.AreEqual(1.245m, ScoreMath.ValueScale(balance, 2));
            Assert.AreEqual(1.560m, ScoreMath.ValueScale(balance, 3));
            Assert.AreEqual(1.945m, ScoreMath.ValueScale(balance, 4));
            Assert.AreEqual(4.920m, ScoreMath.ValueScale(balance, 9));
        }

        [Test]
        public void PlainAwardIsBaseTimesScaleRounded()
        {
            var balance = new AnteBalance();
            Assert.AreEqual(45, ScoreMath.Award(balance, 45, 1, 0f, 1f, 1f));
            Assert.AreEqual(56, ScoreMath.Award(balance, 45, 2, 0f, 1f, 1f));      // 56.025
            Assert.AreEqual(292, ScoreMath.Award(balance, 150, 4, 0f, 1f, 1f));    // 291.75
            Assert.AreEqual(2066, ScoreMath.Award(balance, 420, 9, 0f, 1f, 1f));   // 2066.4
        }

        [Test]
        public void HalvesRoundAwayFromZeroEveryTime()
        {
            var balance = new AnteBalance { valueScaleLinear = 0.5f, valueScaleQuadratic = 0f };
            Assert.AreEqual(68, ScoreMath.Award(balance, 45, 2, 0f, 1f, 1f));      // 67.5 -> 68, never 67
            Assert.AreEqual(128, ScoreMath.Award(balance, 85, 2, 0f, 1f, 1f));     // 127.5 -> 128
        }

        [Test]
        public void PerkBonusStopsAtItsCap()
        {
            var balance = new AnteBalance();
            Assert.AreEqual(125, ScoreMath.Award(balance, 100, 1, 0.25f, 1f, 1f));
            Assert.AreEqual(125, ScoreMath.Award(balance, 100, 1, 0.90f, 1f, 1f));
            Assert.AreEqual(100, ScoreMath.Award(balance, 100, 1, -0.50f, 1f, 1f), "a negative bonus is ignored, never a penalty");
        }

        [Test]
        public void SkillAndRiskStopAtTheirCaps()
        {
            var balance = new AnteBalance();
            Assert.AreEqual(110, ScoreMath.Award(balance, 100, 1, 0f, 1.1f, 1f));
            Assert.AreEqual(120, ScoreMath.Award(balance, 100, 1, 0f, 5f, 1f));
            Assert.AreEqual(115, ScoreMath.Award(balance, 100, 1, 0f, 1f, 1.15f));
            Assert.AreEqual(125, ScoreMath.Award(balance, 100, 1, 0f, 1f, 5f));
            Assert.AreEqual(100, ScoreMath.Award(balance, 100, 1, 0f, 0.2f, 0.2f), "factors below 1 are treated as 1");
        }

        [Test]
        public void EverythingMaxedIsUnderTheOverallCeiling()
        {
            var balance = new AnteBalance();
            // 1.25 x 1.2 x 1.25 = 1.875: with the handoff numbers the 2.25 ceiling can never be reached.
            Assert.AreEqual(1.875m, ScoreMath.EventMultiplier(balance, 9f, 9f, 9f));
            Assert.AreEqual(188, ScoreMath.Award(balance, 100, 1, 9f, 9f, 9f));    // 187.5
        }

        [Test]
        public void OverallCeilingHoldsIfTheSmallerCapsAreLoosened()
        {
            var balance = new AnteBalance { perkScoreBonusCap = 1f, skillMax = 2f, riskMax = 2f };
            Assert.AreEqual(2.25m, ScoreMath.EventMultiplier(balance, 1f, 2f, 2f));
            Assert.AreEqual(225, ScoreMath.Award(balance, 100, 1, 1f, 2f, 2f));
        }

        [Test]
        public void EveryAwardIsTheSameWhenAskedAgain()
        {
            var balance = new AnteBalance();
            for (int ante = 1; ante <= 9; ante++)
            {
                for (int basePoints = 45; basePoints <= 550; basePoints += 5)
                {
                    int first = ScoreMath.Award(balance, basePoints, ante, 0.15f, 1.1f, 1.15f);
                    Assert.AreEqual(first, ScoreMath.Award(balance, basePoints, ante, 0.15f, 1.1f, 1.15f));
                }
            }
        }

        [Test]
        public void TheDefaultNumbersAreTheHandoffNumbers()
        {
            var balance = new AnteBalance();
            CollectionAssert.AreEqual(new[] { 850, 1100, 1450, 1850, 2350, 3000, 3700, 4550, 5500 }, balance.anteTargets);
            Assert.AreEqual(45, balance.BasePoints(ScoreEventKind.EnemyTier1));
            Assert.AreEqual(85, balance.BasePoints(ScoreEventKind.EnemyTier2));
            Assert.AreEqual(150, balance.BasePoints(ScoreEventKind.EnemyTier3));
            Assert.AreEqual(260, balance.BasePoints(ScoreEventKind.EnemyElite));
            Assert.AreEqual(180, balance.BasePoints(ScoreEventKind.ShipDestroyed));
            Assert.AreEqual(260, balance.BasePoints(ScoreEventKind.ShipHijacked));
            Assert.AreEqual(260, balance.BasePoints(ScoreEventKind.BountyDead));
            Assert.AreEqual(420, balance.BasePoints(ScoreEventKind.BountyAlive));
            Assert.AreEqual(110, balance.BasePoints(ScoreEventKind.NewMoon));
            Assert.AreEqual(150, balance.BasePoints(ScoreEventKind.NewPlanet));
            Assert.AreEqual(120, balance.BasePoints(ScoreEventKind.Landmark));
            Assert.AreEqual(170, balance.BasePoints(ScoreEventKind.CaveRuinObjective));
            Assert.AreEqual(180, balance.BasePoints(ScoreEventKind.SatelliteRepaired));
            Assert.AreEqual(260, balance.BasePoints(ScoreEventKind.DishNetworkCompleted));
            Assert.AreEqual(100, balance.BasePoints(ScoreEventKind.DifficultLanding));
            Assert.AreEqual(80, balance.BasePoints(ScoreEventKind.UnderThreatRecovery));
        }

        [Test]
        public void TheDefaultNumbersPassTheirOwnCheckApartFromSalvage()
        {
            var problems = new List<string>();
            new AnteBalance().Validate(problems);
            Assert.AreEqual(1, problems.Count, string.Join(" | ", problems));
            StringAssert.Contains("salvage", problems[0]);
        }

        // ---- Ledgers ----

        [Test]
        public void ScoreFeedsAllThreeLedgers()
        {
            var ledger = new RunLedger();
            ledger.AddScore(300);
            Assert.AreEqual(300, ledger.AnteEarned);
            Assert.AreEqual(300, ledger.LifetimeRunScore);
            Assert.AreEqual(300, ledger.SpendablePoints);
        }

        [Test]
        public void SpendingTouchesOnlyTheWallet()
        {
            var ledger = new RunLedger();
            ledger.AddScore(500);
            Assert.IsTrue(ledger.TrySpend(450));
            Assert.AreEqual(500, ledger.AnteEarned);
            Assert.AreEqual(500, ledger.LifetimeRunScore);
            Assert.AreEqual(50, ledger.SpendablePoints);
        }

        [Test]
        public void CannotSpendMoreThanTheWalletHolds()
        {
            var ledger = new RunLedger();
            ledger.AddScore(100);
            Assert.IsFalse(ledger.TrySpend(101));
            Assert.IsFalse(ledger.TrySpend(-5));
            Assert.AreEqual(100, ledger.SpendablePoints);
        }

        [Test]
        public void BankOnlyRewardsNeverReachTheAnteOrTheLeaderboard()
        {
            var ledger = new RunLedger();
            ledger.AddBankOnly(300);
            Assert.AreEqual(0, ledger.AnteEarned);
            Assert.AreEqual(0, ledger.LifetimeRunScore);
            Assert.AreEqual(300, ledger.SpendablePoints);
        }

        [Test]
        public void NewAnteClearsAnteScoreOnly()
        {
            var ledger = new RunLedger();
            ledger.AddScore(1200);
            ledger.TrySpend(200);
            ledger.StartNewAnte();
            Assert.AreEqual(0, ledger.AnteEarned, "score beyond the target does not carry into the next ante");
            Assert.AreEqual(1200, ledger.LifetimeRunScore);
            Assert.AreEqual(1000, ledger.SpendablePoints, "the wallet carries forward");
        }

        // ---- No double pay, no farms ----

        [Test]
        public void TheSameThingPaysOnce()
        {
            var run = new AnteRun(new AnteBalance(), 7);
            Assert.AreEqual(ScoreOutcome.Awarded, run.Report(Kill("enemy:a")).outcome);
            Assert.AreEqual(ScoreOutcome.AlreadyClaimed, run.Report(Kill("enemy:a")).outcome);
            Assert.AreEqual(45, run.Ledger.LifetimeRunScore);
        }

        [Test]
        public void AClaimSurvivesSaveAndLoad()
        {
            var balance = new AnteBalance();
            var run = new AnteRun(balance, 7);
            run.Report(Kill("enemy:a"));
            run.Report(new ScoreEvent { claimId = "landmark:mars.canyon", kind = ScoreEventKind.Landmark });

            AnteRun loaded = AnteRun.Import(balance, run.Export());
            Assert.AreEqual(ScoreOutcome.AlreadyClaimed, loaded.Report(Kill("enemy:a")).outcome);
            Assert.AreEqual(ScoreOutcome.AlreadyClaimed, loaded.Report(new ScoreEvent { claimId = "landmark:mars.canyon", kind = ScoreEventKind.Landmark }).outcome);
            Assert.AreEqual(165, loaded.Ledger.LifetimeRunScore);
            Assert.AreEqual(ScoreOutcome.Awarded, loaded.Report(Kill("enemy:b")).outcome);
        }

        [Test]
        public void AClaimSurvivesIntoLaterAntes()
        {
            var run = new AnteRun(new AnteBalance { anteTargets = new[] { 40, 40 } }, 7);
            run.Report(Kill("enemy:a"));
            Assert.IsTrue(run.Director.ConfirmAdvance());
            Assert.IsTrue(run.BeginNextAnte());
            Assert.AreEqual(ScoreOutcome.AlreadyClaimed, run.Report(Kill("enemy:a")).outcome);
        }

        [Test]
        public void RespawnedEnemiesNeverScore()
        {
            var run = new AnteRun(new AnteBalance(), 7);
            for (int i = 0; i < 50; i++)
            {
                ScoreEvent again = Kill("enemy:respawn" + i);
                again.isRespawn = true;
                Assert.AreEqual(ScoreOutcome.NotEligible, run.Report(again).outcome);
            }
            Assert.AreEqual(0, run.Ledger.LifetimeRunScore);
        }

        [Test]
        public void AnEventWithNoIdIsRefused()
        {
            var run = new AnteRun(new AnteBalance(), 7);
            Assert.AreEqual(ScoreOutcome.MissingClaimId, run.Report(Kill(null)).outcome);
            Assert.AreEqual(ScoreOutcome.MissingClaimId, run.Report(Kill("")).outcome);
            Assert.AreEqual(0, run.Ledger.LifetimeRunScore);
        }

        [Test]
        public void ThereIsNoEventForHitsLimbsOrDisarms()
        {
            foreach (string name in System.Enum.GetNames(typeof(ScoreEventKind)))
            {
                string lower = name.ToLowerInvariant();
                Assert.IsFalse(lower.Contains("limb") || lower.Contains("disarm") || lower.Contains("hit")
                               || lower.Contains("scan") || lower.Contains("surviv") || lower.Contains("travel"), name);
            }
        }

        [Test]
        public void UnderThreatRecoveryIsCappedPerAnte()
        {
            var run = new AnteRun(new AnteBalance { anteTargets = new[] { 100, 100 } }, 7);
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(ScoreOutcome.Awarded, run.Report(new ScoreEvent { claimId = "recover:" + i, kind = ScoreEventKind.UnderThreatRecovery }).outcome);
            Assert.AreEqual(ScoreOutcome.CapReached, run.Report(new ScoreEvent { claimId = "recover:3", kind = ScoreEventKind.UnderThreatRecovery }).outcome);
            Assert.AreEqual(240, run.Ledger.AnteEarned);

            run.Director.ConfirmAdvance();
            run.BeginNextAnte();
            Assert.AreEqual(ScoreOutcome.Awarded, run.Report(new ScoreEvent { claimId = "recover:4", kind = ScoreEventKind.UnderThreatRecovery }).outcome,
                "the allowance is per ante");
        }

        [Test]
        public void ARetakenBaseScoresOnlyThroughANewMissionAndOnlySoOften()
        {
            var run = new AnteRun(new AnteBalance(), 7);
            ScoreEvent Retake(string mission) => new ScoreEvent
            {
                claimId = mission, kind = ScoreEventKind.BaseRetakeMission, capGroup = "base:mars.2", missionBasePoints = 300,
            };
            Assert.AreEqual(ScoreOutcome.Awarded, run.Report(Retake("retake:mars.2:1")).outcome);
            Assert.AreEqual(ScoreOutcome.AlreadyClaimed, run.Report(Retake("retake:mars.2:1")).outcome);
            Assert.AreEqual(ScoreOutcome.Awarded, run.Report(Retake("retake:mars.2:2")).outcome);
            Assert.AreEqual(ScoreOutcome.CapReached, run.Report(Retake("retake:mars.2:3")).outcome);
            Assert.AreEqual(600, run.Ledger.LifetimeRunScore);
        }

        [Test]
        public void TheCapCountsSurviveSaveAndLoad()
        {
            var balance = new AnteBalance();
            var run = new AnteRun(balance, 7);
            for (int i = 0; i < 3; i++) run.Report(new ScoreEvent { claimId = "recover:" + i, kind = ScoreEventKind.UnderThreatRecovery });
            AnteRun loaded = AnteRun.Import(balance, run.Export());
            Assert.AreEqual(ScoreOutcome.CapReached, loaded.Report(new ScoreEvent { claimId = "recover:9", kind = ScoreEventKind.UnderThreatRecovery }).outcome);
        }

        [Test]
        public void MissionValueIsHeldInsideItsRange()
        {
            var run = new AnteRun(new AnteBalance(), 7);
            Assert.AreEqual(250, run.Report(new ScoreEvent { claimId = "m1", kind = ScoreEventKind.OptionalMission, missionBasePoints = 10 }).points);
            Assert.AreEqual(550, run.Report(new ScoreEvent { claimId = "m2", kind = ScoreEventKind.OptionalMission, missionBasePoints = 99999 }).points);
            Assert.AreEqual(400, run.Report(new ScoreEvent { claimId = "m3", kind = ScoreEventKind.OptionalMission, missionBasePoints = 400 }).points);
        }

        [Test]
        public void SalvageScoresNothingUntilItIsGivenAValue()
        {
            var run = new AnteRun(new AnteBalance(), 7);
            ScoreResult result = run.Report(new ScoreEvent { claimId = "salvage:1", kind = ScoreEventKind.SalvageDelivered });
            Assert.AreEqual(ScoreOutcome.NoValueSet, result.outcome);
            Assert.IsFalse(run.Claims.IsClaimed("salvage:1"), "a refused event does not use up its claim");
        }

        // ---- Bounties ----

        [Test]
        public void AliveBountyPaysMoreAndIsTheOnlyOneThatEarnsGoodwill()
        {
            var run = new AnteRun(new AnteBalance(), 7);
            ScoreResult alive = run.Report(new ScoreEvent { claimId = "bounty:1", kind = ScoreEventKind.BountyAlive, postingFaction = "A" });
            ScoreResult dead = run.Report(new ScoreEvent { claimId = "bounty:2", kind = ScoreEventKind.BountyDead, postingFaction = "B" });
            Assert.AreEqual(420, alive.points);
            Assert.AreEqual(260, dead.points);
            Assert.IsTrue(alive.grantsGoodwill);
            Assert.AreEqual("A", alive.goodwillFaction);
            Assert.IsFalse(dead.grantsGoodwill);
            Assert.IsNull(dead.goodwillFaction);
        }

        [Test]
        public void OneBountyCannotPayBothDeadAndAlive()
        {
            var run = new AnteRun(new AnteBalance(), 7);
            run.Report(new ScoreEvent { claimId = "bounty:1", kind = ScoreEventKind.BountyAlive, postingFaction = "A" });
            Assert.AreEqual(ScoreOutcome.AlreadyClaimed, run.Report(new ScoreEvent { claimId = "bounty:1", kind = ScoreEventKind.BountyDead, postingFaction = "A" }).outcome);
        }

        // ---- Skill, risk and perks through the service ----

        [Test]
        public void VerifiedSkillAndAcceptedRiskUseTheHandoffFactors()
        {
            var run = new AnteRun(new AnteBalance(), 7);
            Assert.AreEqual(110, run.Report(new ScoreEvent { claimId = "l1", kind = ScoreEventKind.DifficultLanding, verifiedSkill = true }).points);
            Assert.AreEqual(115, run.Report(new ScoreEvent { claimId = "l2", kind = ScoreEventKind.DifficultLanding, acceptedRisk = true }).points);
            Assert.AreEqual(127, run.Report(new ScoreEvent { claimId = "l3", kind = ScoreEventKind.DifficultLanding, verifiedSkill = true, acceptedRisk = true }).points); // 126.5
        }

        [Test]
        public void AScorePerkAppliesOnlyToItsOwnEvents()
        {
            var run = new AnteRun(new AnteBalance(), 7);
            run.Perks.Add(Find(run, "perk.wreck_hunter"));
            Assert.AreEqual(138, run.Report(new ScoreEvent { claimId = "w1", kind = ScoreEventKind.Landmark, tag = "wreck" }).points);
            Assert.AreEqual(120, run.Report(new ScoreEvent { claimId = "w2", kind = ScoreEventKind.Landmark }).points);
            Assert.AreEqual(45, run.Report(Kill("e1")).points);
        }

        [Test]
        public void ChangeOfPaceWorksTwiceAnAnteOnADifferentKindInsideTheWindow()
        {
            var run = new AnteRun(new AnteBalance(), 7);
            run.Perks.Add(Find(run, PerkCatalog.ChainPerkId));

            Assert.AreEqual(45, run.Report(Kill("e1")).points, "nothing came before it");
            Assert.AreEqual(45, run.Report(Kill("e2")).points, "same kind of play: no bonus");
            Assert.AreEqual(132, run.Report(new ScoreEvent { claimId = "lm1", kind = ScoreEventKind.Landmark }).points, "combat then exploration: +10%");
            Assert.AreEqual(50, run.Report(Kill("e3")).points, "exploration then combat: second use (49.5 rounds to 50)");
            Assert.AreEqual(120, run.Report(new ScoreEvent { claimId = "lm2", kind = ScoreEventKind.Landmark }).points, "used up for this ante");
            Assert.AreEqual(2, run.Score.ChainUsesThisAnte);
        }

        [Test]
        public void ChangeOfPaceNeedsTheSecondEventInsideThirtySeconds()
        {
            var run = new AnteRun(new AnteBalance(), 7);
            run.Perks.Add(Find(run, PerkCatalog.ChainPerkId));
            run.Report(Kill("e1"));
            run.Tick(31f, false);
            Assert.AreEqual(120, run.Report(new ScoreEvent { claimId = "lm1", kind = ScoreEventKind.Landmark }).points);
        }

        [Test]
        public void NothingScoresBetweenAntesOrAfterTheRunEnds()
        {
            var run = new AnteRun(new AnteBalance { anteTargets = new[] { 40, 40 } }, 7);
            run.Report(Kill("e1"));
            run.Director.ConfirmAdvance();
            Assert.AreEqual(ScoreOutcome.AnteNotRunning, run.Report(Kill("e2")).outcome);
            run.BeginNextAnte();
            run.Director.PlayerDied();
            Assert.AreEqual(ScoreOutcome.AnteNotRunning, run.Report(Kill("e3")).outcome);
        }

        [Test]
        public void TheLogKeepsRefusalsTooAndStaysBounded()
        {
            var run = new AnteRun(new AnteBalance(), 7);
            for (int i = 0; i < ScoreService.LogCapacity + 50; i++) run.Report(Kill("e" + (i % 10)));
            Assert.AreEqual(ScoreService.LogCapacity, run.Score.Log.Count);
            Assert.AreEqual(ScoreOutcome.AlreadyClaimed, run.Score.Log[run.Score.Log.Count - 1].result.outcome);
        }

        static PerkDefinition Find(AnteRun run, string id)
        {
            foreach (PerkDefinition perk in run.Catalog) if (perk.id == id) return perk;
            Assert.Fail("No perk called " + id);
            return null;
        }
    }
}
