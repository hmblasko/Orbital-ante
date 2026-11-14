using System;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Save;
using UnityEngine;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Exercises newer-save protection with Unity's JSON and real files in a throwaway cache folder.
    /// Draft PlayMode tests: run in Unity's Test Runner before merging; no real player saves are touched.
    /// </summary>
    public class SaveProtectionTests
    {
        const string FileName = "orbital_ante_save.json";
        const string CorruptJson = "{ \"version\": 2, \"runsSt";
        string _folder;
        int _newerSaveWarnings;

        string MainPath => Path.Combine(_folder, FileName);
        string BackupPath => MainPath + ".bak";
        string TempPath => MainPath + ".tmp";

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Application.temporaryCachePath, "save_protection_tests_" + Guid.NewGuid().ToString("N"));
            SaveSystem.ResetStatics();
            SaveSystem.UseFolder(_folder);
            _newerSaveWarnings = 0;
            Application.logMessageReceived += CountNewerSaveWarnings;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= CountNewerSaveWarnings;
            SaveSystem.ResetStatics();
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        [TestCase("future", "current", SaveSource.Backup, 7)]
        [TestCase("current", "future", SaveSource.Main, 11)]
        [TestCase("future", "missing", SaveSource.None, 0)]
        [TestCase("missing", "future", SaveSource.None, 0)]
        [TestCase("future", "future", SaveSource.None, 0)]
        [TestCase("corrupt", "future", SaveSource.None, 0)]
        public void EitherFutureSlotProtectsAllFilesWhileCompatibleDataCanStillLoad(
            string main, string backup, SaveSource expectedSource, int expectedRuns)
        {
            WriteSlots(SlotJson(main, 11), SlotJson(backup, 7));
            WriteLeftoverTemp();
            byte[][] before = CaptureFiles(_folder);

            SaveSystem.Load();

            Assert.IsTrue(SaveSystem.WriteProtectedByNewerSave);
            Assert.AreEqual(expectedSource, SaveSystem.LoadedFrom);
            Assert.AreEqual(expectedRuns, SaveSystem.Data.runsStarted);
            Assert.AreEqual(SaveData.CurrentVersion, SaveSystem.Data.version);
            AssertFilesUnchanged(before, _folder);
            AssertRepeatedSavesAreBlocked(before);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AFutureFileAppearingAfterLoadIsDetectedBeforeSaving(bool futureInBackup)
        {
            WriteSlots(SaveJson(SaveData.CurrentVersion, 11), SaveJson(SaveData.CurrentVersion, 7));
            SaveSystem.Load();
            Assert.IsFalse(SaveSystem.WriteProtectedByNewerSave);
            Assert.AreEqual(SaveSource.Main, SaveSystem.LoadedFrom);
            Assert.AreEqual(11, SaveSystem.Data.runsStarted);

            File.WriteAllText(futureInBackup ? BackupPath : MainPath, SaveJson(SaveData.CurrentVersion + 1, 99));
            WriteLeftoverTemp();
            byte[][] before = CaptureFiles(_folder);

            AssertRepeatedSavesAreBlocked(before);
            Assert.AreEqual(SaveSource.Main, SaveSystem.LoadedFrom);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SaveBeforeLoadChecksBothSlots(bool futureInBackup)
        {
            string current = SaveJson(SaveData.CurrentVersion, 7);
            string future = SaveJson(SaveData.CurrentVersion + 1, 99);
            WriteSlots(futureInBackup ? current : future, futureInBackup ? future : current);
            WriteLeftoverTemp();
            byte[][] before = CaptureFiles(_folder);
            Assert.IsFalse(SaveSystem.WriteProtectedByNewerSave);
            Assert.AreEqual(SaveSource.None, SaveSystem.LoadedFrom);

            AssertRepeatedSavesAreBlocked(before);
            Assert.AreEqual(SaveSource.None, SaveSystem.LoadedFrom);
        }

        [Test]
        public void AFutureHeaderIsProtectedWhenKnownPayloadFieldsHaveDifferentTypes()
        {
            string future = "{ \"version\": " + (SaveData.CurrentVersion + 1)
                + ", \"runsStarted\": { \"futureCount\": 99 }, \"lastSeed\": [1, 2, 3],"
                + " \"unknownFutureField\": { \"keep\": true } }";
            WriteSlots(future, SaveJson(SaveData.CurrentVersion, 7));
            WriteLeftoverTemp();
            byte[][] before = CaptureFiles(_folder);

            SaveSystem.Load();

            Assert.IsTrue(SaveSystem.WriteProtectedByNewerSave);
            Assert.AreEqual(SaveSource.Backup, SaveSystem.LoadedFrom);
            Assert.AreEqual(7, SaveSystem.Data.runsStarted);
            AssertRepeatedSavesAreBlocked(before);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RemovingTheFutureFileRequiresReloadingBeforeWritesResume(bool futureInBackup)
        {
            string current = SaveJson(SaveData.CurrentVersion, 7);
            string future = SaveJson(SaveData.CurrentVersion + 1, 99);
            WriteSlots(futureInBackup ? current : future, futureInBackup ? future : current);
            byte[] compatibleBytes = File.ReadAllBytes(futureInBackup ? MainPath : BackupPath);
            SaveSystem.Load();
            Assert.IsTrue(SaveSystem.WriteProtectedByNewerSave);

            // Unsaved session changes must not silently replace the compatible disk save after removal.
            SaveSystem.Data.runsStarted = 999;
            File.Delete(futureInBackup ? BackupPath : MainPath);
            WriteLeftoverTemp();
            byte[][] beforeReload = CaptureFiles(_folder);
            SaveSystem.Save();
            Assert.IsTrue(SaveSystem.WriteProtectedByNewerSave);
            Assert.AreEqual(999, SaveSystem.Data.runsStarted);
            AssertFilesUnchanged(beforeReload, _folder);

            SaveSystem.Load();

            Assert.IsFalse(SaveSystem.WriteProtectedByNewerSave);
            Assert.AreEqual(futureInBackup ? SaveSource.Main : SaveSource.Backup, SaveSystem.LoadedFrom);
            Assert.AreEqual(7, SaveSystem.Data.runsStarted);
            SaveSystem.Data.runsStarted = 8;
            SaveSystem.Save();
            Assert.AreEqual(8, ReadSave(MainPath).runsStarted);
            CollectionAssert.AreEqual(compatibleBytes, File.ReadAllBytes(BackupPath));
            Assert.IsFalse(File.Exists(TempPath));
            Assert.AreEqual(1, _newerSaveWarnings);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResettingProtectionRechecksFutureFilesStillInTheFolder(bool resetStatics)
        {
            WriteSlots(SaveJson(SaveData.CurrentVersion, 7), SaveJson(SaveData.CurrentVersion + 1, 99));
            WriteLeftoverTemp();
            byte[][] before = CaptureFiles(_folder);
            SaveSystem.Load();
            Assert.IsTrue(SaveSystem.WriteProtectedByNewerSave);
            Assert.AreEqual(1, _newerSaveWarnings);

            if (resetStatics)
            {
                SaveSystem.ResetStatics();
                Assert.IsFalse(SaveSystem.WriteProtectedByNewerSave);
                Assert.AreEqual(SaveSource.None, SaveSystem.LoadedFrom);
            }
            SaveSystem.UseFolder(_folder);
            Assert.IsFalse(SaveSystem.WriteProtectedByNewerSave);

            SaveSystem.Save();
            SaveSystem.Save();

            Assert.IsTrue(SaveSystem.WriteProtectedByNewerSave);
            AssertFilesUnchanged(before, _folder);
            Assert.AreEqual(2, _newerSaveWarnings);
        }

        [Test]
        public void SwitchingToACompatibleFolderClearsProtectionAndLoadsThatFoldersData()
        {
            WriteSlots(SaveJson(SaveData.CurrentVersion + 1, 99), SaveJson(SaveData.CurrentVersion, 7));
            WriteLeftoverTemp();
            byte[][] protectedFiles = CaptureFiles(_folder);
            SaveSystem.Load();
            Assert.IsTrue(SaveSystem.WriteProtectedByNewerSave);

            string otherFolder = Path.Combine(_folder, "compatible");
            Directory.CreateDirectory(otherFolder);
            string otherMain = Path.Combine(otherFolder, FileName);
            string compatible = SaveJson(SaveData.CurrentVersion, 23);
            File.WriteAllText(otherMain, compatible);
            byte[] compatibleBytes = File.ReadAllBytes(otherMain);
            SaveSystem.UseFolder(otherFolder);
            Assert.IsFalse(SaveSystem.WriteProtectedByNewerSave);
            SaveSystem.Load();
            Assert.IsFalse(SaveSystem.WriteProtectedByNewerSave);
            Assert.AreEqual(SaveSource.Main, SaveSystem.LoadedFrom);
            Assert.AreEqual(23, SaveSystem.Data.runsStarted);

            SaveSystem.Data.runsStarted = 24;
            SaveSystem.Save();

            Assert.AreEqual(24, ReadSave(otherMain).runsStarted);
            CollectionAssert.AreEqual(compatibleBytes, File.ReadAllBytes(otherMain + ".bak"));
            AssertFilesUnchanged(protectedFiles, _folder);
            Assert.AreEqual(1, _newerSaveWarnings);
        }

        [Test]
        public void CorruptionRecoveryKeepsTheGoodBackupUntilANewGoodMainCanReplaceIt()
        {
            WriteSlots(CorruptJson, SaveJson(SaveData.CurrentVersion, 7));
            byte[] goodBackup = File.ReadAllBytes(BackupPath);

            SaveSystem.Load();

            Assert.IsFalse(SaveSystem.WriteProtectedByNewerSave);
            Assert.AreEqual(SaveSource.Backup, SaveSystem.LoadedFrom);
            Assert.AreEqual(7, SaveSystem.Data.runsStarted);
            SaveSystem.Data.runsStarted = 8;
            SaveSystem.Save();
            Assert.AreEqual(8, ReadSave(MainPath).runsStarted);
            CollectionAssert.AreEqual(goodBackup, File.ReadAllBytes(BackupPath));

            byte[] recoveredMain = File.ReadAllBytes(MainPath);
            SaveSystem.Data.runsStarted = 9;
            SaveSystem.Save();
            Assert.AreEqual(9, ReadSave(MainPath).runsStarted);
            CollectionAssert.AreEqual(recoveredMain, File.ReadAllBytes(BackupPath));
            Assert.IsFalse(File.Exists(TempPath));
            Assert.AreEqual(0, _newerSaveWarnings);
        }

        [Test]
        public void AVersionOneSaveStillMigratesAndCanBeSavedAndReloaded()
        {
            const string versionOne = "{ \"version\": 1, \"hasLastSeed\": true, \"lastSeed\": -1234,"
                + " \"runsStarted\": 4, \"secondsPlayed\": 12.5 }";
            WriteSlots(versionOne, null);
            byte[] original = File.ReadAllBytes(MainPath);

            SaveSystem.Load();

            Assert.IsFalse(SaveSystem.WriteProtectedByNewerSave);
            Assert.AreEqual(SaveSource.Main, SaveSystem.LoadedFrom);
            Assert.AreEqual(SaveData.CurrentVersion, SaveSystem.Data.version);
            Assert.IsTrue(SaveSystem.Data.hasLastSeed);
            Assert.AreEqual(-1234, SaveSystem.Data.lastSeed);
            Assert.AreEqual(0, SaveSystem.Data.lastScenario);
            Assert.AreEqual(4, SaveSystem.Data.runsStarted);
            Assert.AreEqual(12.5, SaveSystem.Data.secondsPlayed, 0.001);
            SaveSystem.Data.runsStarted = 5;
            SaveSystem.Save();
            CollectionAssert.AreEqual(original, File.ReadAllBytes(BackupPath));

            SaveSystem.ResetStatics();
            SaveSystem.UseFolder(_folder);
            SaveSystem.Load();
            Assert.IsFalse(SaveSystem.WriteProtectedByNewerSave);
            Assert.AreEqual(SaveSource.Main, SaveSystem.LoadedFrom);
            Assert.AreEqual(SaveData.CurrentVersion, SaveSystem.Data.version);
            Assert.AreEqual(5, SaveSystem.Data.runsStarted);
            Assert.AreEqual(0, SaveSystem.Data.lastScenario);
            Assert.AreEqual(0, _newerSaveWarnings);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AReadableCompatibleSlotStillLoadsWhenTheOtherSlotIsADirectory(bool mainIsDirectory)
        {
            string compatible = SaveJson(SaveData.CurrentVersion, 7);
            WriteSlots(mainIsDirectory ? null : compatible, mainIsDirectory ? compatible : null);
            string obstacle = mainIsDirectory ? MainPath : BackupPath;
            Directory.CreateDirectory(obstacle);
            WriteLeftoverTemp();
            byte[][] before = CaptureFiles(_folder);
            byte[] goodBytes = File.ReadAllBytes(mainIsDirectory ? BackupPath : MainPath);

            SaveSystem.Load();

            Assert.IsFalse(SaveSystem.WriteProtectedByNewerSave);
            Assert.AreEqual(mainIsDirectory ? SaveSource.Backup : SaveSource.Main, SaveSystem.LoadedFrom);
            Assert.AreEqual(7, SaveSystem.Data.runsStarted);
            AssertFilesUnchanged(before, _folder);
            SaveSystem.Data.version = -17;
            SaveSystem.Data.runsStarted = 8;
            for (int i = 0; i < 2; i++)
            {
                SaveSystem.Save();
                Assert.AreEqual(-17, SaveSystem.Data.version);
                AssertFilesUnchanged(before, _folder);
                Assert.IsTrue(Directory.Exists(obstacle));
            }

            Directory.Delete(obstacle);
            SaveSystem.Save();

            Assert.IsFalse(SaveSystem.WriteProtectedByNewerSave);
            Assert.AreEqual(SaveData.CurrentVersion, SaveSystem.Data.version);
            Assert.AreEqual(8, ReadSave(MainPath).runsStarted);
            CollectionAssert.AreEqual(goodBytes, File.ReadAllBytes(BackupPath));
            Assert.IsFalse(File.Exists(TempPath));
            Assert.AreEqual(0, _newerSaveWarnings);
        }

        [Test]
        public void AnUnreadableLoadWithoutUsableDataRequiresReloadBeforeWritingAfterTheObstacleClears()
        {
            Directory.CreateDirectory(MainPath);
            WriteLeftoverTemp();
            byte[][] obstructed = CaptureFiles(_folder);

            SaveSystem.Load();

            Assert.IsFalse(SaveSystem.WriteProtectedByNewerSave);
            Assert.AreEqual(SaveSource.None, SaveSystem.LoadedFrom);
            Assert.AreEqual(0, SaveSystem.Data.runsStarted);
            AssertFilesUnchanged(obstructed, _folder);
            Assert.IsTrue(Directory.Exists(MainPath));

            // A later readable save must survive until Load replaces the fresh, unsaved session data.
            Directory.Delete(MainPath);
            File.WriteAllText(MainPath, SaveJson(SaveData.CurrentVersion, 23));
            byte[] appearedSave = File.ReadAllBytes(MainPath);
            byte[][] beforeReload = CaptureFiles(_folder);
            SaveSystem.Data.version = -17;
            SaveSystem.Data.runsStarted = 999;
            for (int i = 0; i < 2; i++)
            {
                SaveSystem.Save();
                Assert.AreEqual(-17, SaveSystem.Data.version);
                Assert.AreEqual(999, SaveSystem.Data.runsStarted);
                AssertFilesUnchanged(beforeReload, _folder);
            }

            SaveSystem.Load();

            Assert.IsFalse(SaveSystem.WriteProtectedByNewerSave);
            Assert.AreEqual(SaveSource.Main, SaveSystem.LoadedFrom);
            Assert.AreEqual(23, SaveSystem.Data.runsStarted);
            SaveSystem.Data.runsStarted = 24;
            SaveSystem.Save();
            Assert.AreEqual(24, ReadSave(MainPath).runsStarted);
            CollectionAssert.AreEqual(appearedSave, File.ReadAllBytes(BackupPath));
            Assert.IsFalse(File.Exists(TempPath));
            Assert.AreEqual(0, _newerSaveWarnings);
        }

        void AssertRepeatedSavesAreBlocked(byte[][] before)
        {
            SaveSystem.Data.version = -17;
            SaveSystem.Data.runsStarted = 1234;
            for (int i = 0; i < 3; i++)
            {
                SaveSystem.Save();
                Assert.IsTrue(SaveSystem.WriteProtectedByNewerSave);
                Assert.AreEqual(-17, SaveSystem.Data.version, "A blocked Save must return before changing Data.version.");
                Assert.AreEqual(1234, SaveSystem.Data.runsStarted);
                AssertFilesUnchanged(before, _folder);
            }
            Assert.AreEqual(1, _newerSaveWarnings, "Repeated blocked saves must not repeat the newer-build warning.");
        }

        void CountNewerSaveWarnings(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Warning && message.IndexOf("newer", StringComparison.OrdinalIgnoreCase) >= 0)
                _newerSaveWarnings++;
        }

        void WriteSlots(string main, string backup)
        {
            Directory.CreateDirectory(_folder);
            if (main != null) File.WriteAllText(MainPath, main);
            if (backup != null) File.WriteAllText(BackupPath, backup);
        }

        void WriteLeftoverTemp()
        {
            File.WriteAllBytes(TempPath, new byte[] { 0, 255, 13, 10, 42, 127 });
        }

        static string SlotJson(string kind, int runs)
        {
            if (kind == "missing") return null;
            if (kind == "corrupt") return CorruptJson;
            return SaveJson(kind == "future" ? SaveData.CurrentVersion + 1 : SaveData.CurrentVersion, runs);
        }

        static string SaveJson(int version, int runs)
        {
            return JsonUtility.ToJson(new SaveData
            {
                version = version,
                hasLastSeed = true,
                lastSeed = -4567,
                lastScenario = 1,
                runsStarted = runs,
                secondsPlayed = 321.5,
            }, true);
        }

        static SaveData ReadSave(string path)
        {
            return JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
        }

        static byte[][] CaptureFiles(string folder)
        {
            string main = Path.Combine(folder, FileName);
            return new[] { ReadBytesOrNull(main), ReadBytesOrNull(main + ".bak"), ReadBytesOrNull(main + ".tmp") };
        }

        static byte[] ReadBytesOrNull(string path)
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        static void AssertFilesUnchanged(byte[][] expected, string folder)
        {
            byte[][] actual = CaptureFiles(folder);
            string[] slots = { "main", "backup", "temporary" };
            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] == null)
                    Assert.IsNull(actual[i], "The absent " + slots[i] + " file must stay absent.");
                else
                    CollectionAssert.AreEqual(expected[i], actual[i], "The " + slots[i] + " file must stay byte-for-byte unchanged.");
            }
        }
    }
}
