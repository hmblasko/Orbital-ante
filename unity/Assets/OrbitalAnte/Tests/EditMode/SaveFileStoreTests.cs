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
            _store.DeleteAll();
            Assert.IsFalse(File.Exists(_store.MainPath));
            Assert.IsFalse(File.Exists(_store.BackupPath));
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
    }
}
