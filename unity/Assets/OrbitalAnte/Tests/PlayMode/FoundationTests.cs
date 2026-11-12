using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Content;
using OrbitalAnte.Core;
using OrbitalAnte.Perf;
using OrbitalAnte.Save;
using OrbitalAnte.Weapons;
using UnityEngine;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Checks Milestone 4 with the real engine: the save file round trip through Unity's JSON, recovery from a
    /// damaged save, the content database, and the performance verdict.
    /// The save tests use a throwaway folder, never the real save.
    /// Run in Unity from Window > General > Test Runner > PlayMode.
    /// </summary>
    public class FoundationTests
    {
        string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Application.temporaryCachePath, "save_tests_" + Guid.NewGuid().ToString("N"));
            SaveSystem.ResetStatics();
            SaveSystem.UseFolder(_folder);
            ContentDatabase.ResetStatics();
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.ResetStatics();
            ContentDatabase.ResetStatics();
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        [Test]
        public void WithNoSaveTheGameStartsFresh()
        {
            SaveSystem.Load();
            Assert.AreEqual(SaveSource.None, SaveSystem.LoadedFrom);
            Assert.IsFalse(SaveSystem.Data.hasLastSeed);
            Assert.AreEqual(0, SaveSystem.Data.runsStarted);
        }

        [Test]
        public void SaveRoundTrip()
        {
            SaveSystem.Load();
            SaveSystem.Data.hasLastSeed = true;
            SaveSystem.Data.lastSeed = -123456;
            SaveSystem.Data.runsStarted = 7;
            SaveSystem.Data.secondsPlayed = 321.5;
            SaveSystem.Save();

            SaveSystem.ResetStatics();
            SaveSystem.UseFolder(_folder);
            SaveSystem.Load();
            Assert.AreEqual(SaveSource.Main, SaveSystem.LoadedFrom);
            Assert.IsTrue(SaveSystem.Data.hasLastSeed);
            Assert.AreEqual(-123456, SaveSystem.Data.lastSeed);
            Assert.AreEqual(7, SaveSystem.Data.runsStarted);
            Assert.AreEqual(321.5, SaveSystem.Data.secondsPlayed, 0.001);
        }

        [Test]
        public void ADamagedSaveRecoversFromTheBackup()
        {
            SaveSystem.Load();
            SaveSystem.Data.runsStarted = 1;
            SaveSystem.Save();
            SaveSystem.Data.runsStarted = 2;
            SaveSystem.Save();
            File.WriteAllText(SaveSystem.FilePath, "{ \"version\": 1, \"runsSt");

            SaveSystem.Load();
            Assert.AreEqual(SaveSource.Backup, SaveSystem.LoadedFrom);
            Assert.AreEqual(1, SaveSystem.Data.runsStarted);
        }

        [Test]
        public void ASaveFromANewerBuildIsNotMisread()
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(Path.Combine(_folder, "orbital_ante_save.json"),
                "{ \"version\": " + (SaveData.CurrentVersion + 5) + ", \"runsStarted\": 99 }");
            SaveSystem.Load();
            Assert.AreEqual(SaveSource.None, SaveSystem.LoadedFrom);
            Assert.AreEqual(0, SaveSystem.Data.runsStarted);
        }

        [Test]
        public void ContentDatabaseHasBothGunsAndNoProblems()
        {
            ContentDatabase.Load();
            WeaponData rifle = ContentDatabase.Get<WeaponData>(WeaponData.RifleId);
            WeaponData pistol = ContentDatabase.Get<WeaponData>(WeaponData.PistolId);
            Assert.IsNotNull(rifle);
            Assert.IsNotNull(pistol);
            Assert.AreSame(rifle, WeaponData.Rifle);
            Assert.AreEqual(WeaponData.RifleId, rifle.Id);
            Assert.IsNull(ContentDatabase.Get<WeaponData>("NoSuchGun"));
            Assert.GreaterOrEqual(ContentDatabase.All<WeaponData>().Count, 2);
            Assert.AreEqual(0, ContentDatabase.Problems.Count, string.Join("; ", ContentDatabase.Problems));
        }

        [Test]
        public void ContentIsListedInTheSameOrderEveryTime()
        {
            List<WeaponData> first = ContentDatabase.All<WeaponData>();
            ContentDatabase.Load();
            List<WeaponData> second = ContentDatabase.All<WeaponData>();
            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++) Assert.AreEqual(first[i].Id, second[i].Id);
            for (int i = 1; i < first.Count; i++) Assert.Less(string.CompareOrdinal(first[i - 1].Id, first[i].Id), 0);
        }

        [Test]
        public void ValidationCatchesABrokenGun()
        {
            WeaponData broken = WeaponData.CreateRifleDefaults();
            broken.magazineSize = 0;
            broken.shotsPerSecond = 0f;
            broken.handDamage = 0f;
            var problems = new List<string>();
            broken.Validate(problems);
            Assert.AreEqual(3, problems.Count, string.Join("; ", problems));
            UnityEngine.Object.Destroy(broken);

            WeaponData fine = WeaponData.CreatePistolDefaults();
            problems.Clear();
            fine.Validate(problems);
            Assert.AreEqual(0, problems.Count, string.Join("; ", problems));
            UnityEngine.Object.Destroy(fine);
        }

        [Test]
        public void PerformanceVerdictFollowsTheBudget()
        {
            var go = new GameObject("PerfTest");
            var monitor = go.AddComponent<PerfMonitor>();
            float budget = monitor.Budget.frameBudgetMs / 1000f;
            float hitch = monitor.Budget.hitchMs / 1000f;

            Assert.AreEqual(PerfVerdict.Measuring, monitor.Verdict);

            for (int i = 0; i < 600; i++) monitor.Stats.Add(budget * 0.8f);
            Assert.AreEqual(PerfVerdict.WithinBudget, monitor.Verdict);

            monitor.Stats.Clear();
            for (int i = 0; i < 600; i++) monitor.Stats.Add(i % 4 == 0 ? budget * 1.2f : budget * 0.8f);
            Assert.AreEqual(PerfVerdict.Tight, monitor.Verdict);

            monitor.Stats.Clear();
            for (int i = 0; i < 600; i++) monitor.Stats.Add(i % 10 == 0 ? hitch * 2f : budget * 0.8f);
            Assert.AreEqual(PerfVerdict.Over, monitor.Verdict);
            StringAssert.Contains("OVER", monitor.Describe());

            UnityEngine.Object.Destroy(go);
        }

        [Test]
        public void SessionStreamsAreRepeatableForASeed()
        {
            GameSession.SetSeed(777);
            SeededRandom a = GameSession.Stream("test.stream");
            SeededRandom b = GameSession.Stream("test.stream");
            Assert.AreEqual(a.NextUInt64(), b.NextUInt64());
            Assert.AreNotEqual(GameSession.Stream("one").NextUInt64(), GameSession.Stream("two").NextUInt64());
        }
    }
}
