using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Enemies;

namespace OrbitalAnte.Tests.EditMode
{
    public class EnemyRulesTests
    {
        static List<EnemyCamp> Build(int seed, double radius, System.Func<Vec3d, bool> dry = null, Vec3d keepClear = default)
        {
            return EnemyField.Build(new EnemyFieldRules(), EnemyRoster.Defaults(), new SeededRandom(seed), radius, dry, keepClear);
        }

        [Test]
        public void TheSameSeedGivesTheSameCamps()
        {
            List<EnemyCamp> a = Build(11, 150), b = Build(11, 150), c = Build(12, 150);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Direction, b[i].Direction);
                CollectionAssert.AreEqual(a[i].Members, b[i].Members);
            }
            Assert.AreNotEqual(a[0].Direction, c[0].Direction, "another seed should move the camps");
        }

        [Test]
        public void EveryWorldIsPopulated_AndBiggerWorldsHaveMoreCamps()
        {
            var rules = new EnemyFieldRules();
            Assert.GreaterOrEqual(EnemyField.CampCount(rules, 30), rules.leastCamps, "even the smallest moon has someone on it");
            Assert.Greater(EnemyField.CampCount(rules, 500), EnemyField.CampCount(rules, 150));
            Assert.LessOrEqual(EnemyField.CampCount(rules, 5000), rules.mostCamps);
        }

        [Test]
        public void OnTheMoon_ACampIsNeverFarAway()
        {
            // "Fighting near constantly": wherever you land, a camp should wake before long.
            var rules = new EnemyFieldRules();
            List<EnemyCamp> camps = Build(3, 150);
            var r = new SeededRandom(99);
            for (int i = 0; i < 200; i++)
            {
                double y = r.NextDouble() * 2 - 1, a = r.NextDouble() * 6.283185, w = System.Math.Sqrt(1 - y * y);
                var here = new Vec3d(w * System.Math.Cos(a), y, w * System.Math.Sin(a));
                double nearest = double.MaxValue;
                for (int c = 0; c < camps.Count; c++) nearest = System.Math.Min(nearest, EnemyField.GroundDistance(here, camps[c].Direction, 150));
                Assert.Less(nearest, rules.wakeRange, "a landing spot with nobody within waking range");
            }
        }

        [Test]
        public void CampsKeepOffTheSea_AndAwayFromThePad_AndApartFromEachOther()
        {
            var rules = new EnemyFieldRules();
            var pad = new Vec3d(0, 1, 0);
            List<EnemyCamp> camps = Build(5, 500, d => d.X > -0.2, pad);
            Assert.Greater(camps.Count, 20);
            for (int i = 0; i < camps.Count; i++)
            {
                Assert.Greater(camps[i].Direction.X, -0.2, "a camp in the sea");
                Assert.Greater(EnemyField.GroundDistance(camps[i].Direction, pad, 500), rules.padClear - 0.01, "a camp on the launch pad");
                for (int k = i + 1; k < camps.Count; k++)
                {
                    Assert.Greater(EnemyField.GroundDistance(camps[i].Direction, camps[k].Direction, 500), rules.campSpacing - 0.01);
                }
            }
        }

        [Test]
        public void TheFourTypesReallyDiffer()
        {
            List<EnemyTypeSpec> roster = EnemyRoster.Defaults();
            Assert.AreEqual(4, roster.Count);
            var weapons = new HashSet<string>();
            var sizes = new HashSet<float>();
            for (int i = 0; i < roster.Count; i++)
            {
                weapons.Add(roster[i].weapon);
                sizes.Add(roster[i].scale);
                Assert.Greater(roster[i].reactionTime, 0.3f, roster[i].id + " must never shoot the instant it sees you");
            }
            Assert.AreEqual(4, weapons.Count, "each type carries its own gun");
            Assert.AreEqual(4, sizes.Count, "each type is its own size, to be told apart at a glance");
        }

        [Test]
        public void SquadsAreMixed_ByShare()
        {
            List<EnemyTypeSpec> roster = EnemyRoster.Defaults();
            var seen = new int[roster.Count];
            List<EnemyCamp> camps = Build(8, 500);
            for (int i = 0; i < camps.Count; i++)
            {
                Assert.GreaterOrEqual(camps[i].Members.Length, new EnemyFieldRules().squadLeast);
                for (int m = 0; m < camps[i].Members.Length; m++) seen[camps[i].Members[m]]++;
            }
            for (int t = 0; t < seen.Length; t++) Assert.Greater(seen[t], 0, roster[t].id + " never turns up");
            Assert.Greater(seen[0], seen[3], "the ordinary type should be the commonest, the marksman the rarest");
        }

        [Test]
        public void NoCampEverHasTwoMarksmen()
        {
            // One slow, heavy shot you were warned about is fair. Two at once from the same hill is not.
            var roster = EnemyRoster.Defaults();
            int marksman = roster.FindIndex(t => t.id == "Marksman");
            Assert.GreaterOrEqual(marksman, 0);
            Assert.AreEqual(1, roster[marksman].mostPerCamp);
            Assert.Greater(roster[marksman].aimTell, 0.5f, "every shot is announced");
            for (int seed = 1; seed <= 20; seed++)
            {
                var camps = EnemyField.Build(new EnemyFieldRules(), roster, new SeededRandom(seed), 500, null, Vec3d.Zero);
                foreach (EnemyCamp camp in camps)
                {
                    int count = 0;
                    foreach (int member in camp.Members) if (member == marksman) count++;
                    Assert.LessOrEqual(count, 1);
                }
            }
        }

        [Test]
        public void ABasesGuards_StandOnItsFloor_AndNoCampCrowdsThem()
        {
            var rules = new EnemyFieldRules();
            var roster = EnemyRoster.Defaults();
            var camps = EnemyField.Build(rules, roster, new SeededRandom(9), 500, null, Vec3d.Zero);
            Vec3d at = camps[0].Direction;   // right on top of an ordinary camp
            EnemyField.AddGarrison(camps, rules, roster, new SeededRandom(10), 500, at, 512.5, 8);
            EnemyCamp guards = camps[camps.Count - 1];
            Assert.AreEqual(8, guards.Members.Length);
            Assert.AreEqual(512.5, guards.HomeRadius, 1e-9);
            int marksman = roster.FindIndex(t => t.id == "Marksman"), count = 0;
            foreach (int m in guards.Members) if (m == marksman) count++;
            Assert.LessOrEqual(count, 1);
            for (int i = 0; i < camps.Count - 1; i++)
                Assert.GreaterOrEqual(EnemyField.GroundDistance(camps[i].Direction, at, 500), rules.campSpacing - 1e-6, "the camp that was there has gone");
        }
    }
}
