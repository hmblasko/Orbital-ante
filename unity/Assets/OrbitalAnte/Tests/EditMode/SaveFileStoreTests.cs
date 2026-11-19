using System;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Save;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// The save file must survive bad luck: a crash mid-write, a damaged file, a save from a newer build.
    /// These use a throwaway folder and run without Unity objects.
    /// </summary>
    public class SaveFileStoreTests
    {
        string _folder;
        SaveFileStore _store;

        static bool LooksValid(string text) => text.StartsWith("save:", StringComparison.Ordinal);

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "orbital_ante_tests_" + Guid.NewGuid().ToString("N"));
            _store = new SaveFileStore(_folder, "save.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        [Test]
        public void NothingSavedYetReadsAsNone()
        {
            Assert.AreEqual(SaveSource.None, _store.TryRead(LooksValid, out string content));
            Assert.IsNull(content);
        }

        [Test]
        public void WhatIsWrittenIsReadBack()
        {
            _store.Write("save:one");
            Assert.AreEqual(SaveSource.Main, _store.TryRead(LooksValid, out string content));
            Assert.AreEqual("save:one", content);
            Assert.IsFalse(File.Exists(_store.MainPath + ".tmp"), "No temporary file is left behind.");
        }

        [Test]
        public void ThePreviousSaveBecomesTheBackup()
        {
            _store.Write("save:one");
            _store.Write("save:two");
            _store.Write("save:three");
            Assert.AreEqual("save:three", File.ReadAllText(_store.MainPath));
            Assert.AreEqual("save:two", File.ReadAllText(_store.BackupPath));
        }

        [Test]
        public void ADamagedSaveFallsBackToTheBackup()
        {
            _store.Write("save:good");
            _store.Write("save:newer");
            File.WriteAllText(_store.MainPath, "{ half a fi");
            Assert.AreEqual(SaveSource.Backup, _store.TryRead(LooksValid, out string content));
            Assert.AreEqual("save:good", content);
        }

        [Test]
        public void ARecoveredBackupSurvivesTheNextSaveAndNormalRotationResumes()
        {
            _store.Write("save:good");
            _store.Write("save:newer");
            File.WriteAllText(_store.MainPath, "{ half a fi");
            Assert.AreEqual(SaveSource.Backup, _store.TryRead(LooksValid, out _));

            _store.Write("save:recovered");
            Assert.AreEqual("save:recovered", File.ReadAllText(_store.MainPath));
            Assert.AreEqual("save:good", File.ReadAllText(_store.BackupPath));
            Assert.IsFalse(File.Exists(_store.MainPath + ".tmp"));

            _store.Write("save:next");
            Assert.AreEqual("save:next", File.ReadAllText(_store.MainPath));
            Assert.AreEqual("save:recovered", File.ReadAllText(_store.BackupPath));
        }

        [Test]
        public void RepeatedRecoveryAndAFreshInstancePreserveTheGoodBackup()
        {
            _store.Write("save:good");
            _store.Write("save:newer");
            File.WriteAllText(_store.MainPath, "junk");
            Assert.AreEqual(SaveSource.Backup, _store.TryRead(LooksValid, out _));
            Assert.AreEqual(SaveSource.Backup, _store.TryRead(LooksValid, out _));

            var reopened = new SaveFileStore(_folder, "save.json");
            Assert.AreEqual(SaveSource.Backup, reopened.TryRead(LooksValid, out string content));
            Assert.AreEqual("save:good", content);
            reopened.Write("save:recovered");
            Assert.AreEqual("save:good", File.ReadAllText(reopened.BackupPath));
        }

        [Test]
        public void AMissingMainKeepsTheRecoveredBackupOnWrite()
        {
            _store.Write("save:good");
            _store.Write("save:newer");
            File.Delete(_store.MainPath);
            Assert.AreEqual(SaveSource.Backup, _store.TryRead(LooksValid, out _));

            _store.Write("save:recovered");
            Assert.AreEqual("save:recovered", File.ReadAllText(_store.MainPath));
            Assert.AreEqual("save:good", File.ReadAllText(_store.BackupPath));
        }

        [Test]
        public void AFailedPromotionCanBeRetriedWithoutLosingTheRecoveredBackup()
        {
            _store.Write("save:good");
            _store.Write("save:newer");
            File.WriteAllText(_store.MainPath, "junk");
            Assert.AreEqual(SaveSource.Backup, _store.TryRead(LooksValid, out _));

            // A directory occupying the destination permits staging but prevents promotion on both platforms.
            File.Delete(_store.MainPath);
            Directory.CreateDirectory(_store.MainPath);
            AssertFileOperationFails(() => _store.Write("save:failed"));
            Assert.AreEqual("save:failed", File.ReadAllText(_store.MainPath + ".tmp"));
            Assert.AreEqual("save:good", File.ReadAllText(_store.BackupPath));
            var reopened = new SaveFileStore(_folder, "save.json");
            Assert.AreEqual(SaveSource.Backup, reopened.TryRead(LooksValid, out string content));
            Assert.AreEqual("save:good", content);

            Directory.Delete(_store.MainPath);
            File.WriteAllText(_store.MainPath, "junk");
            _store.Write("save:retried");
            Assert.AreEqual("save:retried", File.ReadAllText(_store.MainPath));
            Assert.AreEqual("save:good", File.ReadAllText(_store.BackupPath));
        }

        [Test]
        public void AWriteCanValidateAnExistingMainWithoutLoadingFirst()
        {
            _store.Write("save:good");
            _store.Write("save:newer");
            File.WriteAllText(_store.MainPath, "junk");

            var reopened = new SaveFileStore(_folder, "save.json");
            reopened.Write("save:recovered", LooksValid);
            Assert.AreEqual("save:recovered", File.ReadAllText(reopened.MainPath));
            Assert.AreEqual("save:good", File.ReadAllText(reopened.BackupPath));
            reopened.Write("save:next", LooksValid);
            Assert.AreEqual("save:recovered", File.ReadAllText(reopened.BackupPath));
        }

        [Test]
        public void AWriteRechecksAMainDamagedSinceLoading()
        {
            _store.Write("save:good");
            _store.Write("save:newer");
            Assert.AreEqual(SaveSource.Main, _store.TryRead(LooksValid, out _));
            File.WriteAllText(_store.MainPath, "junk");

            _store.Write("save:recovered", LooksValid);
            Assert.AreEqual("save:good", File.ReadAllText(_store.BackupPath));
        }

        [Test]
        public void AnyFileMatchesChecksTheBackupEvenWhenTheMainIsUsable()
        {
            Assert.IsFalse(_store.AnyFileMatches(text => true));
            _store.Write("future:one");
            _store.Write("save:two");

            Assert.IsTrue(_store.AnyFileMatches(text => text.StartsWith("future:", StringComparison.Ordinal)));
            Assert.IsFalse(_store.AnyFileMatches(text => text == "absent"));
            Assert.AreEqual("save:two", File.ReadAllText(_store.MainPath));
            Assert.AreEqual("future:one", File.ReadAllText(_store.BackupPath));
        }

        [Test]
        public void AnyFileMatchesDoesNotChangeRecoveryState()
        {
            _store.Write("save:good");
            _store.Write("save:newer");
            File.WriteAllText(_store.MainPath, "junk");
            Assert.AreEqual(SaveSource.Backup, _store.TryRead(LooksValid, out _));

            Assert.IsTrue(_store.AnyFileMatches(text => true));
            _store.Write("save:recovered");
            Assert.AreEqual("save:good", File.ReadAllText(_store.BackupPath));
        }

        [Test]
        public void AnyFileMatchesReportsUnreadablePaths()
        {
            _store.Write("save:good");
            Directory.CreateDirectory(_store.BackupPath);
            AssertFileOperationFails(() => _store.AnyFileMatches(text => false));
            Assert.AreEqual("save:good", File.ReadAllText(_store.MainPath));
        }

        static void AssertFileOperationFails(Action operation)
        {
            bool failed = false;
            try { operation(); }
            catch (IOException) { failed = true; }
            catch (UnauthorizedAccessException) { failed = true; }
            Assert.IsTrue(failed, "The filesystem must refuse the occupied directory path.");
        }

        [Test]
        public void AnEmptyOrMissingSaveFallsBackToTheBackup()
        {
            _store.Write("save:good");
            _store.Write("save:newer");
            File.WriteAllText(_store.MainPath, string.Empty);
            Assert.AreEqual(SaveSource.Backup, _store.TryRead(LooksValid, out _));
            File.Delete(_store.MainPath);
            Assert.AreEqual(SaveSource.Backup, _store.TryRead(LooksValid, out string content));
            Assert.AreEqual("save:good", content);
        }

        [Test]
        public void WhenBothAreDamagedNothingIsLoaded()
        {
            _store.Write("save:good");
            _store.Write("save:newer");
            File.WriteAllText(_store.MainPath, "junk");
            File.WriteAllText(_store.BackupPath, "junk");
            Assert.AreEqual(SaveSource.None, _store.TryRead(LooksValid, out string content));
            Assert.IsNull(content);
        }

        [Test]
        public void ACrashMidWriteLeavesTheOldSaveIntact()
        {
            _store.Write("save:good");
            // What a crash during the next write leaves behind: a half-written temporary file.
            File.WriteAllText(_store.MainPath + ".tmp", "save:ha");
            Assert.AreEqual(SaveSource.Main, _store.TryRead(LooksValid, out string content));
            Assert.AreEqual("save:good", content);
            _store.Write("save:after");
            Assert.AreEqual("save:after", File.ReadAllText(_store.MainPath));
            Assert.AreEqual("save:good", File.ReadAllText(_store.BackupPath));
        }

        [Test]
        public void DeleteAllRemovesEverything()
        {
            _store.Write("save:one");
            _store.Write("save:two");
            File.WriteAllText(_store.MainPath, "junk");
            Assert.AreEqual(SaveSource.Backup, _store.TryRead(LooksValid, out _));
            File.WriteAllText(_store.MainPath + ".tmp", "leftover");
            _store.DeleteAll();
            Assert.IsFalse(File.Exists(_store.MainPath));
            Assert.IsFalse(File.Exists(_store.BackupPath));
            Assert.IsFalse(File.Exists(_store.MainPath + ".tmp"));
            // A newly supplied main must rotate normally after the recovery state was deleted.
            File.WriteAllText(_store.MainPath, "save:new-one");
            _store.Write("save:new-two");
            Assert.AreEqual("save:new-one", File.ReadAllText(_store.BackupPath));
            _store.DeleteAll();
            Assert.AreEqual(SaveSource.None, _store.TryRead(LooksValid, out _));
        }

        [Test]
        public void TheFolderIsCreatedWhenMissing()
        {
            Assert.IsFalse(Directory.Exists(_folder));
            _store.Write("save:one");
            Assert.IsTrue(File.Exists(_store.MainPath));
        }

        [Test]
        public void MigrationsAcceptCurrentSavesAndRefuseNewerOnes()
        {
            Assert.IsTrue(SaveMigrations.Upgrade(new SaveData()));
            Assert.IsFalse(SaveMigrations.Upgrade(new SaveData { version = SaveData.CurrentVersion + 1 }),
                "A save from a newer build is left alone rather than misread.");
            Assert.IsFalse(SaveMigrations.Upgrade(new SaveData { version = 0 }));
            Assert.IsFalse(SaveMigrations.Upgrade(null));
        }

        [Test]
        public void AnOlderSaveIsBroughtUpToDateWithoutLosingAnything()
        {
            // What a save from the Milestone 4 build looks like once read: version 1, no idea about test worlds.
            var old = new SaveData { version = 1, hasLastSeed = true, lastSeed = 4242, runsStarted = 9, secondsPlayed = 100 };
            Assert.IsTrue(SaveMigrations.Upgrade(old));
            Assert.AreEqual(SaveData.CurrentVersion, old.version);
            Assert.AreEqual(4242, old.lastSeed);
            Assert.AreEqual(9, old.runsStarted);
            Assert.AreEqual(0, old.lastScenario, "Older builds only had the combat arena.");
        }
    }
}
