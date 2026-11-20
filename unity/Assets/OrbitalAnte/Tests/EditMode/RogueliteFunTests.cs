using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Ante;
using OrbitalAnte.Save;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// The roguelite fun pass (20 Nov 2026): the full perk list and its families, what the build adds up to,
    /// "raise the stakes" hazards, tougher antes without inflated health, time put back on the clock, and the
    /// run records in the save. Plain C#.
    /// </summary>
    public class RogueliteFunTests
    {
        static PerkDefinition Perk(string id)
        {
            foreach (PerkDefinition p in RoguelitePerks.Create()) if (p.id == id) return p;
            Assert.Fail("no perk " + id);
            return null;
        }

        static PerkLoadout Holding(params string[] ids)
        {
            var l = new PerkLoadout();
            foreach (string id in ids) Assert.IsTrue(l.Add(Perk(id)), id);
            return l;
        }

        static AnteRun ClearedAnte(AnteRun run, int n)
        {
            for (int i = 0; run.Ledger.AnteEarned < run.Director.Target; i++)
                run.Report(new ScoreEvent { claimId = "m" + n + "." + i, kind = ScoreEventKind.OptionalMission, missionBasePoints = 550 });
            Assert.IsTrue(run.Director.ConfirmAdvance());
            return run;
        }

        // ---- the perk list ----

        [Test]
        public void ThePerkList_HasUniqueIds_Words_AndNothingWaitingOnUnbuiltSystems()
        {
            List<PerkDefinition> list = RoguelitePerks.Create();
            var ids = new HashSet<string>();
            foreach (PerkDefinition p in list)
            {
                Assert.IsTrue(ids.Add(p.id), "duplicate " + p.id);
                Assert.IsNotEmpty(p.displayName, p.id);
                Assert.IsNotEmpty(p.Describe(), p.id);
                StringAssert.DoesNotContain("OptionalMission", p.Describe(), p.id);
                Assert.IsTrue(string.IsNullOrEmpty(p.needs), p.id + " needs " + p.needs);
                foreach (PerkEffect e in p.effects)
                {
                    // Bounties, wreck landmarks and EVA thrust are not wired yet: no perk in the game may lean on them.
                    Assert.AreNotEqual(PerkStat.EvaLateralThrust, e.stat, p.id);
                    Assert.AreNotEqual(PerkStat.ShipFuelPerImpulse, e.stat, p.id);
                    if (e.stat == PerkStat.ScoreBonus)
                    {
                        Assert.AreNotEqual(ScoreEventKind.BountyAlive, e.kind, p.id);
                        Assert.AreNotEqual(ScoreEventKind.Landmark, e.kind, p.id);
                    }
                }
            }
        }

        [Test]
        public void ThePerkList_IsLongEnoughForAFullDraftEveryAnte()
        {
            var balance = new AnteBalance();
            Assert.GreaterOrEqual(RoguelitePerks.Create().Count, PerkCatalog.SizeNeededForFullDrafts(balance));
        }

        [Test]
        public void EveryFamily_CanBeCompleted_AndHasABonus()
        {
            var balance = new AnteBalance();
            List<PerkDefinition> list = RoguelitePerks.Create();
            foreach (PerkFamily f in PerkFamilies.All)
            {
                int n = 0;
                foreach (PerkDefinition p in list) if (p.family == f) n++;
                Assert.GreaterOrEqual(n, PerkFamilies.SetSize(balance) + 1, f + " needs more perks than its set");
                Assert.IsNotEmpty(PerkFamilies.Describe(f, balance), f.ToString());
            }
        }

        [Test]
        public void PerkWords_AreWrittenFromTheNumbers()
        {
            Assert.AreEqual("every 3rd kill counts twice on the gun ladder", Perk("perk.quick_study").Describe());
            Assert.AreEqual("melee kills heal 15", Perk("perk.bloodthirst").Describe());
            Assert.AreEqual("LONG SHOT counts from 45 m instead of 60", Perk("perk.eagle_eye").Describe());
            Assert.AreEqual("+20% points for clearing a base", Perk("perk.quiet_professional").Describe());
            Assert.AreEqual("+20% points for kills, -40% health regeneration", Perk("perk.all_in").Describe());
            Assert.AreEqual("+15% walking speed, +10% damage taken", Perk("perk.no_brakes").Describe());
        }

        [Test]
        public void AFullRun_OffersThreePerksAfterEveryAnte()
        {
            var run = new AnteRun(new AnteBalance(), 77, RoguelitePerks.Create());
            for (int ante = 1; ante < run.Balance.AnteCount; ante++)
            {
                ClearedAnte(run, ante);
                List<PerkDefinition> offers = run.PerkOffers();
                Assert.AreEqual(3, offers.Count, "after ante " + ante);
                Assert.IsTrue(run.TakePerk(offers[0]));
                Assert.IsTrue(run.BeginNextAnte());
            }
        }

        // ---- what the build adds up to ----

        [Test]
        public void NoPerks_NoAttributes_NoHazard_ChangesNothing()
        {
            RunMods m = RunMods.From(new PerkLoadout(), new AttributeShop(new AnteBalance()), Hazard.None, new AnteBalance());
            Assert.AreEqual(1f, m.GunDamage, 1e-5f);
            Assert.AreEqual(1f, m.MeleeDamage, 1e-5f);
            Assert.AreEqual(1f, m.Incoming, 1e-5f);
            Assert.AreEqual(1f, m.MaxHealth, 1e-5f);
            Assert.AreEqual(1f, m.Move, 1e-5f);
            Assert.AreEqual(1f, m.Oxygen, 1e-5f);
            Assert.IsFalse(m.NoRegen);
            Assert.AreEqual(0f, m.HealFor(true, true, 0.1f));
            Assert.IsFalse(m.LadderDouble(3));
            Assert.AreEqual(10f, m.Taken(10f, 2f), 1e-5f);
        }

        [Test]
        public void GlassCannon_IsASidegrade_MoreDamage_LessHealth()
        {
            RunMods m = RunMods.From(Holding("perk.glass_cannon"), null, Hazard.None, new AnteBalance());
            Assert.AreEqual(1.20f, m.GunDamage, 1e-5f);
            Assert.AreEqual(1.20f, m.MeleeDamage, 1e-5f);
            Assert.AreEqual(0.85f, m.MaxHealth, 1e-5f);
        }

        [Test]
        public void AttributesBought_FoldIntoTheBuild()
        {
            var balance = new AnteBalance();
            var run = ClearedAnte(new AnteRun(balance, 3, RoguelitePerks.Create()), 1);
            run.Ledger.AddBankOnly(5000);
            Assert.IsTrue(run.TryBuyAttribute(PlayerAttribute.Damage));
            Assert.IsTrue(run.TryBuyAttribute(PlayerAttribute.Oxygen));
            Assert.IsTrue(run.TryBuyAttribute(PlayerAttribute.Defense));
            RunMods m = RunMods.From(run.Perks, run.Attributes, Hazard.None, balance);
            Assert.AreEqual(1f + balance.damagePerLevel, m.GunDamage, 1e-5f);
            Assert.AreEqual(1f + balance.oxygenPerLevel, m.Oxygen, 1e-5f);
            Assert.AreEqual(1f - balance.defensePerLevel, m.Incoming, 1e-5f);
        }

        [Test]
        public void AFamilyBonus_TurnsOnAtThree_NotBefore()
        {
            var balance = new AnteBalance();
            RunMods two = RunMods.From(Holding("perk.quick_study", "perk.hot_brass"), null, Hazard.None, balance);
            RunMods three = RunMods.From(Holding("perk.quick_study", "perk.hot_brass", "perk.steady_hands"), null, Hazard.None, balance);
            Assert.AreEqual(8, two.RoundsOnKill);
            Assert.AreEqual(1f, two.GunDamage, 1e-5f);
            // Steady Hands +10% and the Gunner set's +15%.
            Assert.AreEqual(1.25f, three.GunDamage, 1e-5f);
            Assert.AreEqual(13, three.RoundsOnKill);
            Assert.AreEqual("GUNNER 2/3", PerkFamilies.Progress(Holding("perk.quick_study", "perk.hot_brass"), PerkFamily.Gunner, balance));
        }

        [Test]
        public void StackingNeverPassesTheCap()
        {
            var balance = new AnteBalance();
            RunMods m = RunMods.From(Holding("perk.steady_hands", "perk.trigger_discipline", "perk.quick_study", "perk.glass_cannon"), null, Hazard.None, balance);
            // Gun damage perks: 0.10 + 0.22 + set 0.15 + weapon 0.20 = 0.67, held to 0.40 per stat (weapon is its own stat).
            Assert.LessOrEqual(m.GunDamage, 1f + balance.perkStatStackCap * 2f + 1e-4f);
            Assert.AreEqual(1f + 0.20f + balance.perkStatStackCap, m.GunDamage, 1e-4f);
        }

        [Test]
        public void QuickStudy_CountsEveryThirdKillTwice()
        {
            RunMods m = RunMods.From(Holding("perk.quick_study"), null, Hazard.None, new AnteBalance());
            Assert.IsFalse(m.LadderDouble(1));
            Assert.IsFalse(m.LadderDouble(2));
            Assert.IsTrue(m.LadderDouble(3));
            Assert.IsTrue(m.LadderDouble(6));
            Assert.IsFalse(m.LadderDouble(7));
        }

        [Test]
        public void Heals_AddUp_AndLowHealthOnesOnlyWorkWhenLow()
        {
            RunMods m = RunMods.From(Holding("perk.second_wind", "perk.bloodthirst", "perk.road_rage",
                "perk.glass_cannon", "perk.last_stand", "perk.no_brakes"), null, Hazard.None, new AnteBalance());
            Assert.AreEqual(6f, m.HealFor(false, false, 0.9f), 1e-5f);
            Assert.AreEqual(21f, m.HealFor(true, false, 0.9f), 1e-5f);
            Assert.AreEqual(16f, m.HealFor(false, true, 0.9f), 1e-5f);
            // Daredevil set: +20 under 30% health.
            Assert.AreEqual(26f, m.HealFor(false, false, 0.2f), 1e-5f);
            Assert.Greater(m.GunShot(false, 0.2f), m.GunShot(false, 0.9f));
        }

        [Test]
        public void InYourFace_OnlyCutsDamageFromCloseShooters()
        {
            RunMods m = RunMods.From(Holding("perk.in_your_face"), null, Hazard.None, new AnteBalance());
            Assert.AreEqual(8f, m.Taken(10f, 5f), 1e-4f);
            Assert.AreEqual(10f, m.Taken(10f, 30f), 1e-4f);
        }

        [Test]
        public void AllIn_PaysOnEveryKillTier_ButNotOnLandings()
        {
            PerkLoadout l = Holding("perk.all_in");
            Assert.AreEqual(0.20f, l.ScoreBonusFor(ScoreEventKind.EnemyTier1, null), 1e-5f);
            Assert.AreEqual(0.20f, l.ScoreBonusFor(ScoreEventKind.EnemyTier3, null), 1e-5f);
            Assert.AreEqual(0.20f, l.ScoreBonusFor(ScoreEventKind.EnemyElite, null), 1e-5f);
            Assert.AreEqual(0f, l.ScoreBonusFor(ScoreEventKind.NewPlanet, null), 1e-5f);
        }

        [Test]
        public void QuietProfessional_PaysOnBaseClearsOnly()
        {
            PerkLoadout l = Holding("perk.quiet_professional");
            Assert.AreEqual(0.20f, l.ScoreBonusFor(ScoreEventKind.OptionalMission, "base"), 1e-5f);
            Assert.AreEqual(0f, l.ScoreBonusFor(ScoreEventKind.OptionalMission, null), 1e-5f);
        }

        // ---- raise the stakes ----

        [Test]
        public void Hazards_PayTheChosenRiskMultiplier_AndSayWhatTheyDo()
        {
            var b = new AnteBalance();
            Assert.AreEqual(1f, HazardRules.Multiplier(Hazard.None, b));
            foreach (Hazard h in HazardRules.Offered)
            {
                Assert.AreEqual(b.riskAccepted, HazardRules.Multiplier(h, b), 1e-5f);
                Assert.IsNotEmpty(HazardRules.Describe(h, b));
            }
            Assert.AreEqual(240f, HazardRules.AnteSeconds(Hazard.ShortFuse, b), 1e-4f);
            Assert.AreEqual(300f, HazardRules.AnteSeconds(Hazard.Hunted, b), 1e-4f);
            StringAssert.Contains("4:00 instead of 5:00", HazardRules.Describe(Hazard.ShortFuse, b));
            Assert.IsTrue(RunMods.From(null, null, Hazard.ThinAir, b).NoRegen);
        }

        [Test]
        public void AChosenRisk_PaysMoreForTheSameEvent()
        {
            var plain = new AnteRun(new AnteBalance(), 1);
            var risky = new AnteRun(new AnteBalance(), 1);
            int a = plain.Report(new ScoreEvent { claimId = "x", kind = ScoreEventKind.SalvageDelivered }).points;
            int b = risky.Report(new ScoreEvent { claimId = "x", kind = ScoreEventKind.SalvageDelivered, acceptedRisk = true }).points;
            Assert.Greater(b, a);
        }

        // ---- tougher antes ----

        [Test]
        public void AnteOne_IsExactlyAsBefore()
        {
            EnemyPressure p = EnemyPressure.For(1, Hazard.None, new AnteBalance());
            Assert.AreEqual(1f, p.Sharpness, 1e-5f);
            Assert.AreEqual(0f, p.HeavyShare, 1e-5f);
        }

        [Test]
        public void Enemies_GetSharperEachAnte_NeverPastTheFloor_AndHeaviesComeFromAnteFour()
        {
            var b = new AnteBalance();
            float last = 1.01f;
            for (int ante = 1; ante <= 9; ante++)
            {
                EnemyPressure p = EnemyPressure.For(ante, Hazard.None, b);
                Assert.LessOrEqual(p.Sharpness, last, "ante " + ante);
                Assert.GreaterOrEqual(p.Sharpness, b.sharpnessFloor - 1e-5f);
                Assert.LessOrEqual(p.HeavyShare, b.heavyShareMax + 1e-5f);
                if (ante < b.heavyFromAnte) Assert.AreEqual(0f, p.HeavyShare, "ante " + ante);
                else Assert.Greater(p.HeavyShare, 0f, "ante " + ante);
                last = p.Sharpness;
            }
        }

        [Test]
        public void Hunted_IsSharperAndHeavierThanTheSameAnteWithout()
        {
            var b = new AnteBalance();
            EnemyPressure plain = EnemyPressure.For(2, Hazard.None, b), hunted = EnemyPressure.For(2, Hazard.Hunted, b);
            Assert.Less(hunted.Sharpness, plain.Sharpness);
            Assert.Greater(hunted.HeavyShare, plain.HeavyShare);
        }

        [Test]
        public void WakingHeavy_IsStableForTheSameEnemy_AndRoughlyTheShare()
        {
            var p = new EnemyPressure { Sharpness = 1f, HeavyShare = 0.25f };
            int heavy = 0;
            for (int i = 0; i < 4000; i++)
            {
                string who = "3." + (i / 8) + "." + (i % 8);
                bool a = EnemyPressure.WakesHeavy(p, 99, who, 5);
                Assert.AreEqual(a, EnemyPressure.WakesHeavy(p, 99, who, 5));
                if (a) heavy++;
            }
            Assert.AreEqual(0.25, heavy / 4000.0, 0.03);
            Assert.IsFalse(EnemyPressure.WakesHeavy(new EnemyPressure { Sharpness = 1f }, 99, "1.1.1", 5));
        }

        // ---- the clock ----

        [Test]
        public void TimeBack_OnlyWhileAnAnteRuns_AndTimeTakenNeverEndsIt()
        {
            var run = new AnteRun(new AnteBalance(), 4);
            run.Director.AdjustSeconds(12f);
            Assert.AreEqual(312f, run.Director.SecondsLeft, 1e-3f);
            run.Director.AdjustSeconds(-1000f);
            Assert.AreEqual(1f, run.Director.SecondsLeft, 1e-3f);
            ClearedAnte(new AnteRun(new AnteBalance(), 4), 1).Director.AdjustSeconds(50f);
        }

        // ---- records ----

        [Test]
        public void AVersion3Save_IsBroughtUpTo4_WithNoRecords()
        {
            var data = new SaveData { version = 3, runsStarted = 5, bestAnte = 99 };
            Assert.IsTrue(SaveMigrations.Upgrade(data));
            Assert.AreEqual(4, data.version);
            Assert.AreEqual(0, data.bestAnte);
            Assert.AreEqual(0, data.runsEnded);
            Assert.AreEqual(5, data.runsStarted);
        }
    }
}
