using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Combat;
using OrbitalAnte.Content;
using OrbitalAnte.Perf;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitalAnte.Tests
{
    public class ContentTests
    {
        [Test]
        public void IdsMustBeLowercaseDottedNames()
        {
            Assert.IsTrue(ContentValidation.IsValidId("weapon.rifle_ak"));
            Assert.IsTrue(ContentValidation.IsValidId("item.tier2.stopwatch"));
            Assert.IsFalse(ContentValidation.IsValidId(null));
            Assert.IsFalse(ContentValidation.IsValidId(""));
            Assert.IsFalse(ContentValidation.IsValidId("rifle"), "needs a type prefix");
            Assert.IsFalse(ContentValidation.IsValidId("Weapon.Rifle"), "no capitals");
            Assert.IsFalse(ContentValidation.IsValidId("weapon rifle"));
            Assert.IsFalse(ContentValidation.IsValidId(".rifle"));
            Assert.IsFalse(ContentValidation.IsValidId("weapon."));
            Assert.IsFalse(ContentValidation.IsValidId("weapon..rifle"));
        }

        [Test]
        public void BuiltInWeaponsPassTheContentCheck()
        {
            var rifle = WeaponDefinition.DefaultRifle();
            var pistol = WeaponDefinition.DefaultPistol();
            var problems = ContentValidation.FindProblems(new ContentDefinition[] { rifle, pistol });
            Assert.IsEmpty(problems, string.Join("\n", problems));
            Object.DestroyImmediate(rifle);
            Object.DestroyImmediate(pistol);
        }

        [Test]
        public void RepeatedAndMissingIdsAreCaught()
        {
            var a = WeaponDefinition.DefaultRifle();
            var b = WeaponDefinition.DefaultRifle();
            b.name = "Copy";
            var c = WeaponDefinition.DefaultPistol();
            c.id = "";
            var problems = ContentValidation.FindProblems(new ContentDefinition[] { a, b, c });
            Assert.AreEqual(2, problems.Count, string.Join("\n", problems));
            StringAssert.Contains("already used", problems[0]);
            StringAssert.Contains("missing", problems[1]);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            Object.DestroyImmediate(c);
        }

        [Test]
        public void BadWeaponNumbersAreCaught()
        {
            var w = WeaponDefinition.DefaultRifle();
            w.magazineSize = 0;
            w.aimSpread = w.hipSpread + 1f;
            var problems = ContentValidation.FindProblems(new ContentDefinition[] { w });
            Assert.AreEqual(2, problems.Count, string.Join("\n", problems));
            Object.DestroyImmediate(w);
        }

        [Test]
        public void IndexSkipsBadIdsAndKeepsTheFirstOfARepeat()
        {
            var a = WeaponDefinition.DefaultRifle();
            var b = WeaponDefinition.DefaultRifle();
            var c = WeaponDefinition.DefaultPistol();
            c.id = "Not Valid";
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("repeats the id"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no valid id"));
            var map = ContentDatabase.Index(new List<WeaponDefinition> { a, b, c });
            Assert.AreEqual(1, map.Count);
            Assert.AreSame(a, map[ContentIds.RifleAK]);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            Object.DestroyImmediate(c);
        }

        [Test]
        public void TestWeaponsAreAlwaysAvailableById()
        {
            ContentDatabase.Reload();
            Assert.IsTrue(ContentDatabase.TryGetWeapon(ContentIds.RifleAK, out var rifle));
            Assert.IsTrue(ContentDatabase.TryGetWeapon(ContentIds.TestPistol, out var pistol));
            Assert.AreEqual(ContentIds.RifleAK, rifle.id);
            Assert.IsTrue(pistol.testOnly);
            Assert.IsFalse(ContentDatabase.TryGetWeapon("weapon.does_not_exist", out _));
        }
    }

    public class FrameStatsTests
    {
        [Test]
        public void SummarisesAverageMaxAndWorstOnePercent()
        {
            var samples = new float[200];
            for (int i = 0; i < samples.Length; i++) samples[i] = 10f;
            samples[17] = 50f; // one hitch
            samples[90] = 40f; // and a smaller one
            var stats = FrameStats.Compute(samples, samples.Length, new float[200]);
            Assert.AreEqual(200, stats.Count);
            Assert.AreEqual(50f, stats.MaxMs);
            Assert.AreEqual((198 * 10f + 90f) / 200f, stats.AverageMs, 1e-3f);
            Assert.AreEqual(40f, stats.Worst1PercentMs, "2 of 200 frames are the slowest 1%; the 99th percentile is the smaller");
        }

        [Test]
        public void OnlyCountsTheFilledPart()
        {
            var samples = new float[] { 5f, 15f, 999f, 999f };
            var stats = FrameStats.Compute(samples, 2, new float[4]);
            Assert.AreEqual(2, stats.Count);
            Assert.AreEqual(10f, stats.AverageMs, 1e-4f);
            Assert.AreEqual(15f, stats.MaxMs);
            Assert.AreEqual(0, FrameStats.Compute(samples, 0, new float[4]).Count);
        }

        [Test]
        public void RatesAgainstBudgetAndFloor()
        {
            Assert.AreEqual(BudgetRating.Good, FrameStats.Rate(16.7f, 16.7f, 25f));
            Assert.AreEqual(BudgetRating.Good, FrameStats.Rate(16.9f, 16.7f, 25f), "small jitter around 60 fps is fine");
            Assert.AreEqual(BudgetRating.Warn, FrameStats.Rate(20f, 16.7f, 25f));
            Assert.AreEqual(BudgetRating.Warn, FrameStats.Rate(25f, 16.7f, 25f));
            Assert.AreEqual(BudgetRating.Over, FrameStats.Rate(25.1f, 16.7f, 25f));
        }
    }
}
