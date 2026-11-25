using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Ante;
using OrbitalAnte.Core;
using OrbitalAnte.Tasks;

namespace OrbitalAnte.Tests
{
    public class ContractRulesTests
    {
        static List<ContractWorld> Near() => new List<ContractWorld>
        {
            new ContractWorld { body = 3, name = "Earth", here = true, landedOn = true },
            new ContractWorld { body = 4, name = "Moon" },
            new ContractWorld { body = 5, name = "Mars", landedOn = true },
            new ContractWorld { body = 6, name = "Phobos" },
            new ContractWorld { body = 7, name = "Venus" },
            new ContractWorld { body = 9, name = "Neptune" },
        };

        [Test]
        public void TheBreakOffersTwoContracts_OfDifferentKinds_TheSameForTheSameSeed()
        {
            var rules = new ContractRules();
            for (int seed = 0; seed < 200; seed++)
            {
                List<Contract> a = ContractBoard.Offer(rules, 2, Near(), false, new SeededRandom(seed));
                List<Contract> b = ContractBoard.Offer(rules, 2, Near(), false, new SeededRandom(seed));
                Assert.AreEqual(2, a.Count);
                Assert.AreNotEqual(a[0].Kind, a[1].Kind, "two offers are two different jobs");
                for (int i = 0; i < 2; i++)
                {
                    Assert.AreEqual(a[i].Kind, b[i].Kind);
                    Assert.AreEqual(a[i].Body, b[i].Body);
                    Assert.AreEqual(a[i].Style, b[i].Style);
                    Assert.AreEqual(2, a[i].Ante);
                    Assert.Greater(a[i].BasePoints, 0);
                    Assert.IsNotEmpty(a[i].Task);
                }
            }
        }

        [Test]
        public void NamedWorlds_AreOnlyTheNearestFew_AndASurveyIsNeverWhereYouHaveLanded()
        {
            var rules = new ContractRules { nearestWorlds = 4 };
            var seen = new HashSet<ContractKind>();
            for (int seed = 0; seed < 400; seed++)
            {
                foreach (Contract c in ContractBoard.Offer(rules, 3, Near(), false, new SeededRandom(seed)))
                {
                    seen.Add(c.Kind);
                    if (c.Kind == ContractKind.Bounty) CollectionAssert.Contains(new[] { 3, 4, 5, 6 }, c.Body);
                    else if (c.Kind == ContractKind.Survey) CollectionAssert.Contains(new[] { 4, 6, 7 }, c.Body, "not here, not landed on, nearest four of those");
                    else Assert.AreEqual(-1, c.Body, "the other kinds are anywhere");
                    Assert.AreNotEqual(9, c.Body, "Neptune is too far to name");
                }
            }
            Assert.AreEqual(5, seen.Count, "every kind turns up");
        }

        [Test]
        public void WithNoWorldsKnown_OnlyTheAnywhereKindsAreOffered()
        {
            for (int seed = 0; seed < 50; seed++)
                foreach (Contract c in ContractBoard.Offer(new ContractRules(), 2, null, false, new SeededRandom(seed)))
                    Assert.AreEqual(-1, c.Body);
        }

        [Test]
        public void MeleeStyle_IsOnlyOfferedToSomeoneHoldingAMeleeWeapon()
        {
            bool sawMelee = false;
            for (int seed = 0; seed < 600; seed++)
            {
                foreach (Contract c in ContractBoard.Offer(new ContractRules(), 2, Near(), false, new SeededRandom(seed)))
                    Assert.AreNotEqual(ContractStyle.Melee, c.Style);
                foreach (Contract c in ContractBoard.Offer(new ContractRules(), 2, Near(), true, new SeededRandom(seed)))
                    sawMelee |= c.Style == ContractStyle.Melee;
            }
            Assert.IsTrue(sawMelee);
        }

        [Test]
        public void Disabled_OffersNothing()
        {
            Assert.IsEmpty(ContractBoard.Offer(new ContractRules { enabled = false }, 2, Near(), false, new SeededRandom(1)));
        }

        [Test]
        public void ABounty_CountsKillsOnItsWorldOnly_AndStopsAtDone()
        {
            var c = new Contract { Kind = ContractKind.Bounty, Body = 5, World = "Mars", Needed = 2, Ante = 3 };
            Assert.IsFalse(c.Kill(4, default));
            Assert.IsTrue(c.Kill(5, default));
            Assert.AreEqual("BOUNTY ON MARS  1/2", c.Progress);
            Assert.IsTrue(c.Kill(5, new KillFacts { rover = true }), "a splat is a kill");
            Assert.IsTrue(c.Complete);
            Assert.IsFalse(c.Kill(5, default));
            Assert.AreEqual(2, c.Done);
            Assert.AreEqual("BOUNTY ON MARS: DONE", c.Progress);
            Assert.AreEqual("contract.ante3", c.ClaimId);
        }

        [Test]
        public void AStyleContract_CountsOnlyItsFeat()
        {
            var c = new Contract { Kind = ContractKind.Style, Style = ContractStyle.LongShot, Needed = 3 };
            Assert.IsFalse(c.Kill(5, new KillFacts { oneShot = true }));
            Assert.IsTrue(c.Kill(5, new KillFacts { longShot = true }));
            Assert.IsTrue(c.Kill(2, new KillFacts { longShot = true, airborne = true }));
            Assert.AreEqual(2, c.Done);
            Assert.IsFalse(Contract.Matches(ContractStyle.OneShot, new KillFacts { oneShot = true, rover = true }), "the rover is not a shot");
            Assert.IsFalse(Contract.Matches(ContractStyle.Airborne, new KillFacts { airborne = true, rover = true }), "a rover jump is not airborne shooting");
            Assert.IsTrue(Contract.Matches(ContractStyle.TakenApart, new KillFacts { takenApart = true }));
            Assert.IsTrue(Contract.Matches(ContractStyle.Melee, new KillFacts { melee = true }));
        }

        [Test]
        public void ASurvey_IsDoneByLandingOnItsWorld()
        {
            var c = new Contract { Kind = ContractKind.Survey, Body = 7, World = "Venus", Needed = 1 };
            StringAssert.StartsWith("Land the ship on Venus", c.Task);
            Assert.IsFalse(c.Landed(5));
            Assert.IsFalse(c.Kill(7, default));
            Assert.IsTrue(c.Landed(7));
            Assert.IsTrue(c.Complete);
        }

        [Test]
        public void MastsAndSalvage_CountTheirOwnEventsOnly()
        {
            var m = new Contract { Kind = ContractKind.Masts, Needed = 1 };
            var s = new Contract { Kind = ContractKind.Salvage, Needed = 2 };
            Assert.IsFalse(m.CrateDelivered());
            Assert.IsFalse(s.MastDown());
            Assert.IsTrue(m.MastDown());
            Assert.IsFalse(m.MastDown(), "done");
            Assert.IsTrue(s.CrateDelivered());
            Assert.AreEqual("SALVAGE RUN  1/2", s.Progress);
            Assert.AreEqual("Bring 2 salvage crates aboard your ship, anywhere.", s.Task);
        }

        [Test]
        public void ABounty_NeverNamesAWorldWithFewerEnemiesLeftThanItAsks()
        {
            var near = new List<ContractWorld>
            {
                new ContractWorld { body = 5, name = "Mars", enemiesLeft = 40, enemiesCounted = true },
                new ContractWorld { body = 11, name = "Deimos", enemiesLeft = 3, enemiesCounted = true },
                new ContractWorld { body = 12, name = "Phobos", enemiesLeft = 5, enemiesCounted = true },
            };
            for (int seed = 0; seed < 400; seed++)
                foreach (Contract c in ContractBoard.Offer(new ContractRules(), 2, near, false, new SeededRandom(seed)))
                    if (c.Kind == ContractKind.Bounty)
                    {
                        Assert.AreNotEqual(11, c.Body, "Deimos has 3 left and the bounty asks 5");
                        Assert.LessOrEqual(c.Needed, c.Body == 12 ? 5 : 40);
                    }
        }

        [Test]
        public void ABiggerBountyPaysMore_AndOffersAreCappedToFitThePage()
        {
            var r = new ContractRules();
            Assert.AreEqual(r.huntPoints, r.HuntPoints(r.huntFirst));
            Assert.AreEqual(r.huntPoints + 4 * r.huntPointsPerExtra, r.HuntPoints(r.huntFirst + 4));
            var many = new ContractRules { offers = 9 };
            Assert.LessOrEqual(ContractBoard.Offer(many, 2, Near(), false, new SeededRandom(3)).Count, ContractBoard.MostOffers);
        }

        [Test]
        public void TheBountyGrowsSlowlyWithTheAnte_AndIsCapped()
        {
            var r = new ContractRules();
            Assert.AreEqual(5, r.HuntCount(2));
            Assert.AreEqual(5, r.HuntCount(4));
            Assert.AreEqual(6, r.HuntCount(5));
            Assert.AreEqual(7, r.HuntCount(8));
            Assert.AreEqual(9, r.HuntCount(40));
        }

        [Test]
        public void EveryContractPays_InsideTheMissionRange_SoNothingIsSilentlyClamped()
        {
            var r = new ContractRules();
            var b = new AnteBalance();
            foreach (int p in new[] { r.huntPoints, r.HuntPoints(r.huntMost), r.landPoints, r.stylePoints, r.takenApartPoints, r.mastPoints, r.salvagePoints })
            {
                Assert.GreaterOrEqual(p, b.optionalMissionMin);
                Assert.LessOrEqual(p, b.optionalMissionMax);
            }
        }
    }
}
