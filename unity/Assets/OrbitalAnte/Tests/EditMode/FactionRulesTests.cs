using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Enemies;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>Faction A (Milestone 13): who it is, where it lives, and its three abilities.</summary>
    public class FactionRulesTests
    {
        static List<EnemyTypeSpec> Roster => EnemyRoster.Defaults();

        static EnemyTypeSpec Type(string id)
        {
            foreach (EnemyTypeSpec t in Roster) if (t.id == id) return t;
            Assert.Fail("no type " + id);
            return null;
        }

        [Test]
        public void FactionA_HasThreeTiers_OfClearlyDifferentSize_EachWithItsOwnAbility()
        {
            EnemyTypeSpec scout = Type(EnemyRoster.ScoutA), line = Type(EnemyRoster.LineA), heavy = Type(EnemyRoster.HeavyA);
            foreach (EnemyTypeSpec t in new[] { scout, line, heavy }) Assert.AreEqual(Factions.A, t.faction, t.id);
            Assert.Less(scout.scale, line.scale);
            Assert.Less(line.scale, heavy.scale);
            Assert.GreaterOrEqual(heavy.scale - scout.scale, 0.4f, "the tiers must read apart at a glance");
            Assert.AreEqual(FactionAbility.Flare, scout.ability);
            Assert.AreEqual(FactionAbility.Suppress, line.ability);
            Assert.AreEqual(FactionAbility.SpinUp, heavy.ability);
            Assert.AreNotEqual(scout.weapon, line.weapon);
            Assert.AreNotEqual(line.weapon, heavy.weapon);
        }

        [Test]
        public void FactionA_IsNoTougherThanTheUnalignedKindsOfTheSameSize()
        {
            // Constitution 3: tiers differ in size, gun and ability, never in inflated health.
            Assert.LessOrEqual(Type(EnemyRoster.ScoutA).toughness, Type("Runner").toughness);
            Assert.LessOrEqual(Type(EnemyRoster.LineA).toughness, Type("Trooper").toughness);
            Assert.LessOrEqual(Type(EnemyRoster.HeavyA).toughness, Type("Bruiser").toughness);
            Assert.LessOrEqual(Type(EnemyRoster.HeavyA).headToughness, Type("Bruiser").headToughness);
        }

        [Test]
        public void TheUnalignedPick_NeverReturnsAFactionType()
        {
            for (int i = 0; i <= 100; i++)
                Assert.AreEqual(Factions.None, Roster[EnemyRoster.Pick(Roster, i / 100.0)].faction);
        }

        [Test]
        public void WithNoFactionRules_CampsAreExactlyAsBefore()
        {
            var before = EnemyField.Build(new EnemyFieldRules(), Roster, new SeededRandom(5), 600, null, Vec3d.Zero);
            var again = EnemyField.Build(new EnemyFieldRules(), Roster, new SeededRandom(5), 600, null, Vec3d.Zero, null, "Venus");
            Assert.AreEqual(before.Count, again.Count);
            for (int i = 0; i < before.Count; i++)
            {
                CollectionAssert.AreEqual(before[i].Members, again[i].Members);
                Assert.AreEqual(Factions.None, again[i].Faction);
            }
        }

        [Test]
        public void EveryCampOnTheHomeWorld_IsFactionA_AndNoCampIsMixed()
        {
            var roster = Roster;
            var rules = new FactionFieldRules();
            var camps = EnemyField.Build(new EnemyFieldRules(), roster, new SeededRandom(11), 600, null, Vec3d.Zero, rules, rules.homeA);
            EnemyField.AddGarrison(camps, new EnemyFieldRules(), roster, new SeededRandom(12), 600, new Vec3d(0, 1, 0), 618, 8, 2, rules, rules.homeA);
            Assert.Greater(camps.Count, 0);
            foreach (EnemyCamp c in camps)
            {
                Assert.AreEqual(Factions.A, c.Faction);
                foreach (int m in c.Members) Assert.AreEqual(Factions.A, roster[m].faction);
            }
        }

        [Test]
        public void Elsewhere_ARoughShareOfCampsIsFactionA_AndEachCampIsOneSideOnly()
        {
            var roster = Roster;
            var rules = new FactionFieldRules();
            int faction = 0, total = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var camps = EnemyField.Build(new EnemyFieldRules(), roster, new SeededRandom(seed), 600, null, Vec3d.Zero, rules, "Mars");
                foreach (EnemyCamp c in camps)
                {
                    total++;
                    if (c.Faction == Factions.A) faction++;
                    foreach (int m in c.Members) Assert.AreEqual(c.Faction, roster[m].faction, "a camp is never mixed");
                }
            }
            double share = faction / (double)total;
            Assert.That(share, Is.InRange(rules.shareA - 0.12, rules.shareA + 0.12), "share " + share);
        }

        [Test]
        public void FactionCamps_AreTheSameForTheSameSeed()
        {
            var rules = new FactionFieldRules();
            var a = EnemyField.Build(new EnemyFieldRules(), Roster, new SeededRandom(77), 600, null, Vec3d.Zero, rules, "Moon");
            var b = EnemyField.Build(new EnemyFieldRules(), Roster, new SeededRandom(77), 600, null, Vec3d.Zero, rules, "Moon");
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Faction, b[i].Faction);
                CollectionAssert.AreEqual(a[i].Members, b[i].Members);
            }
        }

        [Test]
        public void AFactionCamp_NeverHasMoreThanOneHeavy_OrTwoScouts_AndASmallBaseHasNoHeavy()
        {
            var roster = Roster;
            int heavy = roster.FindIndex(t => t.id == EnemyRoster.HeavyA), scout = roster.FindIndex(t => t.id == EnemyRoster.ScoutA);
            var rules = new FactionFieldRules();
            for (int seed = 1; seed <= 30; seed++)
            {
                var camps = EnemyField.Build(new EnemyFieldRules(), roster, new SeededRandom(seed), 600, null, Vec3d.Zero, rules, rules.homeA);
                EnemyField.AddGarrison(camps, new EnemyFieldRules(), roster, new SeededRandom(seed + 100), 600, new Vec3d(1, 0, 0), 610, 5, 0, rules, rules.homeA);
                for (int c = 0; c < camps.Count; c++)
                {
                    int h = 0, s = 0;
                    foreach (int m in camps[c].Members) { if (m == heavy) h++; if (m == scout) s++; }
                    Assert.LessOrEqual(h, 1);
                    Assert.LessOrEqual(s, 2);
                    if (c == camps.Count - 1) Assert.AreEqual(0, h, "a small base has no Heavy");
                }
            }
        }

        [Test]
        public void ALargeFactionBase_AlwaysHasItsTwoHeavies()
        {
            var roster = Roster;
            int heavy = roster.FindIndex(t => t.id == EnemyRoster.HeavyA);
            var rules = new FactionFieldRules();
            var camps = new List<EnemyCamp>();
            EnemyField.AddGarrison(camps, new EnemyFieldRules(), roster, new SeededRandom(3), 600, new Vec3d(0, 0, 1), 630, 10, 2, rules, rules.homeA);
            int h = 0;
            foreach (int m in camps[0].Members) if (m == heavy) h++;
            Assert.AreEqual(2, h);
        }

        // ------------------------------------------------------------------ the Scout's flare

        [Test]
        public void TheFlare_GoesUpAfterTheRaise_AndOnlyOnce()
        {
            var rules = new FlareRules();
            var f = new FlareState();
            Assert.IsTrue(f.Begin(10f, rules, false));
            Assert.IsFalse(f.Tick(10f + rules.raiseSeconds * 0.5f, true));
            Assert.AreEqual(0.5f, f.RaiseAmount(10f + rules.raiseSeconds * 0.5f, rules), 0.01f);
            Assert.IsTrue(f.Tick(10f + rules.raiseSeconds, true));
            Assert.IsFalse(f.Tick(11f + rules.raiseSeconds, true));
            Assert.IsFalse(f.Begin(20f, rules, false), "one flare a life");
        }

        [Test]
        public void TheFlare_IsStoppedByLosingTheHand_AndNeverStartsWhenTheSquadIsAlreadyRoused()
        {
            var rules = new FlareRules();
            var f = new FlareState();
            f.Begin(0f, rules, false);
            Assert.IsFalse(f.Tick(0.4f, false));
            Assert.IsFalse(f.Tick(rules.raiseSeconds + 1f, true));
            Assert.AreEqual(FlareStep.Spent, f.Step);

            var g = new FlareState();
            Assert.IsFalse(g.Begin(0f, rules, true));
            Assert.AreEqual(FlareStep.Ready, g.Step, "kept for a later fresh sighting");
        }

        [Test]
        public void TheFlare_IsHeardWithinItsRadiusOnly()
        {
            var rules = new FlareRules();
            Assert.IsTrue(FlareState.Wakes(rules.wakeRadius - 1, rules));
            Assert.IsFalse(FlareState.Wakes(rules.wakeRadius + 1, rules));
        }

        // ------------------------------------------------------------------ the Heavy's spin-up

        [Test]
        public void TheHeavy_SpinsUp_ThenFiresForItsBurst_ThenStartsFromCold()
        {
            var rules = new SpinUpRules();
            var s = new SpinUpState();
            Assert.IsFalse(s.Want(0f, rules));
            Assert.IsTrue(s.Braced);
            Assert.IsFalse(s.Want(rules.spinSeconds * 0.9f, rules), "no shot during the spin");
            Assert.That(s.Glow(rules.spinSeconds * 0.5f, rules), Is.InRange(0.45f, 0.55f));
            Assert.IsTrue(s.Want(rules.spinSeconds, rules));
            Assert.AreEqual(1f, s.Glow(rules.spinSeconds + 0.1f, rules));
            Assert.IsTrue(s.Want(rules.spinSeconds + rules.burstSeconds * 0.9f, rules));
            Assert.IsFalse(s.Want(rules.spinSeconds + rules.burstSeconds + 0.01f, rules));
            Assert.AreEqual(SpinStep.Cold, s.Step);
        }

        [Test]
        public void TheHeavy_TurnsSlowlyOnlyWhileBraced_AndStoppingWindsItDown()
        {
            var rules = new SpinUpRules();
            var s = new SpinUpState();
            Assert.IsTrue(float.IsPositiveInfinity(s.MaxTurn(0.1f, rules)));
            s.Want(0f, rules);
            Assert.AreEqual(rules.bracedTurn * 0.1f, s.MaxTurn(0.1f, rules), 1e-4f);
            s.Stop();
            Assert.IsFalse(s.Braced);
            Assert.IsFalse(s.Want(5f, rules), "after a stop it has to spin up again");
        }

        [Test]
        public void CoveringFire_IsWiderAndLongerThanAimedFire()
        {
            var rules = new SuppressRules();
            Assert.Greater(rules.spreadScale, 1f);
            Assert.Greater(rules.burstSeconds, rules.pauseSeconds * 2f);
        }
    }
}
