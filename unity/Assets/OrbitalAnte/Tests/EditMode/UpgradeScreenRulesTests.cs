using System.Linq;
using NUnit.Framework;
using OrbitalAnte.Ante;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// The fun pass of 21 Nov 2026: the perk pick earned the moment the target is met (applied from the upgrade
    /// screen in the field), rerolls as a use for points, and shop prices that follow what points are worth.
    /// </summary>
    public class UpgradeScreenRulesTests
    {
        static AnteRun RunWithTarget(int target = 250, int seed = 5)
        {
            var catalog = RoguelitePerks.Create();
            return new AnteRun(new AnteBalance { anteTargets = new[] { target, target, target } }, seed, catalog);
        }

        static void Earn(AnteRun run, int points)
        {
            int n = 0;
            int goal = run.Ledger.AnteEarned + points;
            while (run.Ledger.AnteEarned < goal)
                run.Report(new ScoreEvent { claimId = "e" + run.Ledger.LifetimeRunScore + "." + n++, kind = ScoreEventKind.OptionalMission, missionBasePoints = 300 });
        }

        [Test]
        public void NoPickInTheField_UntilTheTargetIsMet()
        {
            AnteRun run = RunWithTarget(600);
            Assert.IsFalse(run.Director.CanPickPerk);
            Assert.IsEmpty(run.PerkOffers());
            Earn(run, 300);
            Assert.IsFalse(run.Director.CanPickPerk, "300 of 600 is not the target");
            Earn(run, 300);
            Assert.IsTrue(run.Director.IsRunning);
            Assert.IsTrue(run.Director.PerkEarnedEarly);
            Assert.AreEqual(3, run.PerkOffers().Count);
        }

        [Test]
        public void APickTakenInTheField_IsThisAntesPick_TheBreakHasNoneLeft()
        {
            AnteRun run = RunWithTarget();
            Earn(run, 300);
            PerkDefinition first = run.PerkOffers()[0];
            Assert.IsTrue(run.TakePerk(first));
            Assert.IsTrue(run.Perks.Has(first.id));
            Assert.IsFalse(run.Director.CanPickPerk, "one pick an ante");
            Assert.IsTrue(run.Director.ConfirmAdvance());
            Assert.AreEqual(AntePhase.Intermission, run.Director.Phase);
            Assert.IsFalse(run.Director.PerkDraftPending);
            Assert.IsTrue(run.Director.ScratchPending, "the ticket is still the break's");
            Assert.IsTrue(run.BeginNextAnte());
            Assert.IsFalse(run.Director.PerkTakenEarly, "the next ante earns its own pick");
        }

        [Test]
        public void APickNotTakenInTheField_WaitsForTheBreak_WithTheSameThreeOffers()
        {
            AnteRun run = RunWithTarget();
            Earn(run, 300);
            var field = run.PerkOffers().Select(p => p.id).ToList();
            run.Director.ConfirmAdvance();
            Assert.IsTrue(run.Director.PerkDraftPending);
            CollectionAssert.AreEqual(field, run.PerkOffers().Select(p => p.id).ToList());
        }

        [Test]
        public void NoPickInTheField_OnTheLastAnte()
        {
            var run = new AnteRun(new AnteBalance { anteTargets = new[] { 250 } }, 5, RoguelitePerks.Create());
            Earn(run, 300);
            Assert.IsFalse(run.Director.PerkEarnedEarly, "nothing left for a perk to change");
        }

        [Test]
        public void Rerolls_SpendTheWalletOnly_DoubleInPrice_AndGiveNewOffers()
        {
            AnteRun run = RunWithTarget();
            Earn(run, 1200);
            run.Director.ConfirmAdvance();
            var before = run.PerkOffers().Select(p => p.id).ToList();
            int wallet = run.Ledger.SpendablePoints, score = run.Ledger.LifetimeRunScore;
            int price = run.RerollPrice;
            Assert.AreEqual(150, price, "ante 1 value scale is 1");
            Assert.IsTrue(run.RerollPerks());
            Assert.AreEqual(wallet - 150, run.Ledger.SpendablePoints);
            Assert.AreEqual(score, run.Ledger.LifetimeRunScore, "spending never lowers the score");
            Assert.AreEqual(300, run.RerollPrice);
            CollectionAssert.AreNotEqual(before, run.PerkOffers().Select(p => p.id).ToList());
            Assert.AreEqual(3, run.PerkOffers().Count);
        }

        [Test]
        public void Rerolls_NeedSomethingToReroll_AndEnoughPoints()
        {
            AnteRun run = RunWithTarget();
            Assert.IsFalse(run.RerollPerks(), "no pick on offer during an ante short of the target");
            Earn(run, 300);
            run.Director.ConfirmAdvance();
            int wallet = run.Ledger.SpendablePoints;
            while (run.Ledger.SpendablePoints >= run.RerollPrice) Assert.IsTrue(run.RerollPerks());
            Assert.IsFalse(run.RerollPerks());
            Assert.Less(run.Ledger.SpendablePoints, run.RerollPrice);
            Assert.Less(run.Ledger.SpendablePoints, wallet);
        }

        [Test]
        public void ShopPrices_FollowTheAntesValue()
        {
            var balance = new AnteBalance();
            var run = new AnteRun(balance, 5, RoguelitePerks.Create());
            Assert.AreEqual(250, run.Attributes.NextPrice(PlayerAttribute.Damage), "ante 1 keeps the listed price");
            // Ante 3 is worth K = 1 + 0.21*2 + 0.035*4 = 1.56, so 250 becomes 390.
            var state = run.Export();
            state.ante = 3;
            state.phase = (int)AntePhase.Intermission;
            AnteRun later = AnteRun.Import(balance, state, RoguelitePerks.Create());
            Assert.AreEqual(390, later.Attributes.NextPrice(PlayerAttribute.Damage));
            balance.shopPricesFollowValue = false;
            Assert.AreEqual(250, later.Attributes.NextPrice(PlayerAttribute.Damage));
        }

        [TestCase(0, false, 250, 0)]
        [TestCase(249, false, 1, 249)]
        [TestCase(250, true, 0, 0)]
        [TestCase(int.MaxValue, true, 0, int.MaxValue - 250)]
        public void ShopQuote_MatchesPurchaseWithoutSpendingOrLevelling(int wallet, bool canBuy, int needed, int left)
        {
            var run = RunWithTarget();
            run.Ledger.AddScore(wallet);
            var quote = run.Attributes.Quote(PlayerAttribute.Damage, run.Ledger);
            Assert.AreEqual(250, quote.Price);
            Assert.AreEqual(canBuy, quote.CanBuy);
            Assert.AreEqual(needed, quote.PointsNeeded);
            Assert.AreEqual(left, quote.WalletAfter);
            Assert.AreEqual(0, run.Attributes.Level(PlayerAttribute.Damage));
            Assert.AreEqual(wallet, run.Ledger.SpendablePoints);
            Assert.AreEqual(canBuy, run.TryBuyAttribute(PlayerAttribute.Damage));
            Assert.AreEqual(left, run.Ledger.SpendablePoints);
            Assert.AreEqual(wallet, run.Ledger.AnteEarned);
            Assert.AreEqual(wallet, run.Ledger.LifetimeRunScore);
        }

        [Test]
        public void ShopQuote_WaitingUsesTheSameLevelAtTheActualNextAntePrice()
        {
            var run = RunWithTarget();
            run.Ledger.AddScore(300);
            Assert.IsTrue(run.TryBuyAttribute(PlayerAttribute.Damage));
            Assert.IsTrue(run.Director.ConfirmAdvance());
            var quote = run.Attributes.Quote(PlayerAttribute.Damage, run.Ledger);
            Assert.AreEqual(450, quote.Price);
            Assert.AreEqual(560, quote.PriceNextAnte);
            Assert.IsTrue(run.BeginNextAnte());
            Assert.AreEqual(quote.PriceNextAnte, run.Attributes.NextPrice(PlayerAttribute.Damage));
        }

        [Test]
        public void APriceAboveTheWalletLimitStaysPurchasableWithAFullWallet()
        {
            int ante = 1;
            var shop = new AttributeShop(new AnteBalance { damagePrices = new[] { int.MaxValue } }, () => ante);
            var ledger = new RunLedger();
            ledger.AddBankOnly(int.MaxValue);
            Assert.AreEqual(int.MaxValue, shop.Quote(PlayerAttribute.Damage, ledger).PriceNextAnte);
            ante++;
            Assert.IsTrue(shop.TryBuy(PlayerAttribute.Damage, ledger));
            Assert.AreEqual(0, ledger.SpendablePoints);
        }

        [Test]
        public void ShopQuote_HandlesUnchangedPricesFreeLevelsAndSoldOutUpgrades()
        {
            var balance = new AnteBalance { damagePrices = new[] { 0, 250 }, valueScaleLinear = 0f, valueScaleQuadratic = 0.001f };
            var shop = new AttributeShop(balance, () => 1);
            var ledger = new RunLedger();
            Assert.IsTrue(shop.Quote(PlayerAttribute.Damage, ledger).CanBuy, "a free level needs no points");
            Assert.IsTrue(shop.TryBuy(PlayerAttribute.Damage, ledger));
            var rounded = shop.Quote(PlayerAttribute.Damage, ledger);
            Assert.AreEqual(250, rounded.PriceNextAnte, "a rising scale can still round to the current price");
            balance.valueScaleLinear = 1f;
            balance.shopPricesFollowValue = false;
            Assert.AreEqual(250, shop.Quote(PlayerAttribute.Damage, ledger).PriceNextAnte);
            ledger.AddBankOnly(250);
            Assert.IsTrue(shop.TryBuy(PlayerAttribute.Damage, ledger));
            var soldOut = shop.Quote(PlayerAttribute.Damage, ledger);
            Assert.AreEqual(-1, soldOut.Price);
            Assert.AreEqual(-1, soldOut.PriceNextAnte);
            Assert.AreEqual(0, soldOut.PointsNeeded);
            Assert.IsFalse(soldOut.CanBuy);
        }

        [Test]
        public void TheFieldPickAndRerolls_SurviveASaveAndLoad()
        {
            AnteRun run = RunWithTarget();
            Earn(run, 1200);
            Assert.IsTrue(run.RerollPerks());
            Assert.IsTrue(run.TakePerk(run.PerkOffers()[1]));
            AnteRun back = AnteRun.Import(run.Balance, run.Export(), RoguelitePerks.Create());
            Assert.IsTrue(back.Director.PerkTakenEarly);
            Assert.AreEqual(1, back.Rerolls);
            Assert.IsFalse(back.Director.CanPickPerk);
        }
    }
}
