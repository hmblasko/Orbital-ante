using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OrbitalAnte.Ante;

namespace OrbitalAnte.Tests
{
    /// <summary>The upgrade screen's star chart (21 Nov 2026, option A of the mockups): what it shows, in plain C#.</summary>
    public class StarChartTests
    {
        static readonly List<PerkDefinition> Catalog = RoguelitePerks.Create();
        static PerkDefinition Perk(string id) => Catalog.First(p => p.id == "perk." + id);

        [Test]
        public void EveryFamily_HasOneConstellation_WithAStarPerPerkOfItsSet_AllOnScreen()
        {
            var balance = new AnteBalance();
            var chart = StarChart.Constellations(new PerkLoadout(), null, -1, balance);
            CollectionAssert.AreEquivalent(PerkFamilies.All, chart.Select(c => c.family));
            foreach (var c in chart)
            {
                Assert.AreEqual(PerkFamilies.SetSize(balance), c.stars.Count);
                Assert.IsTrue(c.stars.All(s => s.state == StarState.Empty));
                foreach (var s in c.stars)
                {
                    Assert.That(s.x, Is.InRange(16f, StarChart.Width - 16f));
                    Assert.That(s.y, Is.InRange(16f, 480f), "the chart stays above the cards");
                }
            }
        }

        [Test]
        public void HeldPerksLightStars_AndTheCardUnderTheCursorShowsTheNextOnePending()
        {
            var loadout = new PerkLoadout();
            loadout.Add(Perk("quick_study"));
            loadout.Add(Perk("steady_hands"));
            var offers = new List<PerkDefinition> { Perk("cold_blood"), Perk("hot_brass"), Perk("thick_skin") };
            var chart = StarChart.Constellations(loadout, offers, 1, new AnteBalance());
            var gunner = chart.First(c => c.family == PerkFamily.Gunner);
            Assert.AreEqual(StarState.Lit, gunner.stars[0].state);
            Assert.AreEqual(StarState.Lit, gunner.stars[1].state);
            Assert.AreEqual(StarState.Pending, gunner.stars[2].state);
            Assert.AreEqual("Hot Brass", gunner.stars[2].label);
            Assert.IsTrue(gunner.completesWithPick);
            Assert.AreEqual("GUNNER 2/3", gunner.progress);
            Assert.IsTrue(chart.First(c => c.family == PerkFamily.Ghost).stars.All(s => s.state == StarState.Empty),
                "only the selected card shows a pending star");
        }

        [Test]
        public void Cards_PutGainsAndCostsOnSeparateLines()
        {
            var offers = new List<PerkDefinition> { Perk("glass_cannon"), Perk("fleet_foot"), Perk("steady_hands") };
            var cards = StarChart.Cards(new PerkLoadout(), offers, new AnteBalance());
            Assert.AreEqual(3, cards.Count);
            foreach (var card in cards.Take(2))
            {
                Assert.IsNotEmpty(card.gains, card.name);
                Assert.IsNotEmpty(card.costs, card.name + " has a cost and it must be written on it");
            }
            Assert.IsEmpty(cards[2].costs);
            StringAssert.Contains("damage taken", cards[1].costs[0]);
        }

        [Test]
        public void EveryPerkWithADownside_ShowsItAsACost()
        {
            // Constitution rule 5, legible risk: a card never hides its price among its benefits.
            foreach (var perk in Catalog)
            {
                var card = StarChart.Cards(null, new[] { perk }, new AnteBalance())[0];
                Assert.AreEqual(perk.effects.Length, card.gains.Count + card.costs.Count, perk.id);
                Assert.IsNotEmpty(card.gains, perk.id + " must give something");
            }
            Assert.IsNotEmpty(StarChart.Cards(null, new[] { Perk("thick_skin") }, null)[0].costs);
            Assert.IsNotEmpty(StarChart.Cards(null, new[] { Perk("no_brakes") }, null)[0].costs);
        }

        [Test]
        public void TheChartLine_SaysWhatThePickDoesForTheSet()
        {
            var loadout = new PerkLoadout();
            loadout.Add(Perk("quick_study"));
            loadout.Add(Perk("steady_hands"));
            var cards = StarChart.Cards(loadout, new List<PerkDefinition> { Perk("hot_brass"), Perk("cold_blood"), Perk("thick_skin") }, new AnteBalance());
            Assert.IsTrue(cards[0].completesSet);
            StringAssert.StartsWith("Completes GUNNER: ", cards[0].chartLine);
            Assert.AreEqual("GHOST 1 of 3", cards[1].chartLine);
            Assert.AreEqual("Wild: fits any build", cards[2].chartLine);
        }

        [Test]
        public void AFinishedSet_ReadsAsOn_AndGetsNoPendingStar()
        {
            var loadout = new PerkLoadout();
            foreach (var id in new[] { "quick_study", "steady_hands", "hot_brass" }) loadout.Add(Perk(id));
            var chart = StarChart.Constellations(loadout, new List<PerkDefinition> { Perk("trigger_discipline") }, 0, new AnteBalance());
            var gunner = chart.First(c => c.family == PerkFamily.Gunner);
            Assert.IsTrue(gunner.complete);
            Assert.AreEqual("GUNNER SET ON", gunner.progress);
            Assert.IsFalse(gunner.stars.Any(s => s.state == StarState.Pending));
        }

        [Test]
        public void StarPlaces_StretchToOtherSetSizes_AndStayInOrder()
        {
            foreach (int n in new[] { 2, 3, 4, 5 })
            {
                var places = StarChart.StarPlaces(PerkFamily.Pilot, n);
                Assert.AreEqual(n, places.Count);
                for (int i = 1; i < n; i++) Assert.AreNotEqual(places[i - 1], places[i]);
            }
            Assert.IsEmpty(StarChart.StarPlaces(PerkFamily.None, 3));
        }

        [Test]
        public void WildPerks_AreListedApart()
        {
            var loadout = new PerkLoadout();
            loadout.Add(Perk("thick_skin"));
            loadout.Add(Perk("steady_hands"));
            CollectionAssert.AreEqual(new[] { "Thick Skin" }, StarChart.WildHeld(loadout));
            Assert.AreEqual("12,450", StarChart.Points(12450));
        }
    }
}
