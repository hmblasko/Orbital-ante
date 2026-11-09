using System.IO;
using NUnit.Framework;
using OrbitalAnte.Save;
using UnityEngine;
using UnityEngine.TestTools;

namespace OrbitalAnte.Tests
{
    public class SaveSystemTests
    {
        string dir;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Application.temporaryCachePath, "oa-save-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            SaveSystem.UseDirectory(dir);
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.UseDirectory(null);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        [Test]
        public void TextRoundTripKeepsEveryField()
        {
            var file = new SaveFile();
            file.profile.headshotSlowMoChosen = true;
            file.profile.headshotSlowMo = true;
            file.profile.runsStarted = 7;
            file.run.active = true;
            file.run.seed = 123456;

            Assert.AreEqual(SaveReadResult.Ok, SaveCodec.Read(SaveCodec.Write(file), out var back));
            Assert.AreEqual(SaveCodec.CurrentVersion, back.version);
            Assert.IsTrue(back.profile.headshotSlowMoChosen);
            Assert.IsTrue(back.profile.headshotSlowMo);
            Assert.AreEqual(7, back.profile.runsStarted);
            Assert.IsTrue(back.run.active);
            Assert.AreEqual(123456, back.run.seed);
        }

        [Test]
        public void BadTextIsReportedNotLoaded()
        {
            Assert.AreEqual(SaveReadResult.Empty, SaveCodec.Read("", out _));
            Assert.AreEqual(SaveReadResult.Empty, SaveCodec.Read(null, out _));
            Assert.AreEqual(SaveReadResult.Corrupt, SaveCodec.Read("{ this is not json", out _));
            Assert.AreEqual(SaveReadResult.Corrupt, SaveCodec.Read("{}", out _), "no version means not a save");
        }

        [Test]
        public void SaveFromANewerBuildIsRefused()
        {
            string newer = "{\"version\":" + (SaveCodec.CurrentVersion + 1) + "}";
            Assert.AreEqual(SaveReadResult.TooNew, SaveCodec.Read(newer, out _));
        }

        [Test]
        public void MissingSectionsAreFilledIn()
        {
            Assert.AreEqual(SaveReadResult.Ok, SaveCodec.Read("{\"version\":1}", out var file));
            Assert.IsNotNull(file.profile);
            Assert.IsNotNull(file.run);
            Assert.IsFalse(file.run.active);
        }

        [Test]
        public void FirstLaunchStartsClean()
        {
            var s = SaveSystem.Load();
            Assert.IsFalse(s.run.active);
            Assert.AreEqual(0, s.profile.runsStarted);
            Assert.IsFalse(SaveSystem.ReadOnly);
        }

        [Test]
        public void SavedRunComesBackAfterReload()
        {
            SaveSystem.RecordRunStarted(4242);
            SaveSystem.UseDirectory(dir); // forget what is in memory
            var s = SaveSystem.Load();
            Assert.IsTrue(s.run.active);
            Assert.AreEqual(4242, s.run.seed);
            Assert.AreEqual(1, s.profile.runsStarted);
        }

        [Test]
        public void RestartingTheSameSeedIsNotCountedTwice()
        {
            SaveSystem.RecordRunStarted(5);
            SaveSystem.RecordRunStarted(5);
            SaveSystem.RecordRunStarted(6);
            Assert.AreEqual(2, SaveSystem.Current.profile.runsStarted);
        }

        [Test]
        public void DamagedSaveFallsBackToTheBackup()
        {
            SaveSystem.RecordRunStarted(111); // first save
            SaveSystem.RecordRunStarted(222); // second save; the first becomes the backup
            File.WriteAllText(SaveSystem.MainPath, "garbage that is not a save");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("could not be read"));
            SaveSystem.UseDirectory(dir);
            var s = SaveSystem.Load();
            Assert.AreEqual(111, s.run.seed, "the backup holds the previous good save");
            Assert.IsTrue(File.Exists(SaveSystem.MainPath + ".corrupt"), "the damaged file is kept, not deleted");
        }

        [Test]
        public void NewerSaveIsNeverOverwritten()
        {
            string newer = "{\"version\":" + (SaveCodec.CurrentVersion + 1) + ",\"run\":{\"active\":true,\"seed\":9}}";
            File.WriteAllText(SaveSystem.MainPath, newer);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("newer build"));
            SaveSystem.Load();
            Assert.IsTrue(SaveSystem.ReadOnly);
            Assert.IsFalse(SaveSystem.Save());
            Assert.AreEqual(newer, File.ReadAllText(SaveSystem.MainPath));
        }
    }
}
