using NUnit.Framework;
using OrbitalAnte.UI;

namespace OrbitalAnte.Tests
{
    /// <summary>The numbers behind the "Hull stencil" player screen: blocks, the flip clock, the magazine, the pips.</summary>
    public class HullReadoutTests
    {
        [Test]
        public void TwoDigits_PadsLikePaintOnHardware()
        {
            Assert.AreEqual("00", HullReadout.TwoDigits(0));
            Assert.AreEqual("06", HullReadout.TwoDigits(6));
            Assert.AreEqual("30", HullReadout.TwoDigits(30));
            Assert.AreEqual("99", HullReadout.TwoDigits(99));
            Assert.AreEqual("120", HullReadout.TwoDigits(120));
            Assert.AreEqual("00", HullReadout.TwoDigits(-4));
            Assert.AreSame(HullReadout.TwoDigits(7), HullReadout.TwoDigits(7), "no new string each frame");
        }

        [Test]
        public void Blocks_ShowSomethingForAnythingLeft_AndAFullBarOnlyWhenThere()
        {
            Assert.AreEqual(0, HullReadout.Blocks(0f, 20));
            Assert.AreEqual(0, HullReadout.Blocks(-1f, 20));
            Assert.AreEqual(0, HullReadout.Blocks(float.NaN, 20));
            Assert.AreEqual(1, HullReadout.Blocks(0.001f, 20));
            Assert.AreEqual(10, HullReadout.Blocks(0.5f, 20));
            Assert.AreEqual(12, HullReadout.Blocks(0.6225f, 20));
            Assert.AreEqual(19, HullReadout.Blocks(0.999f, 20), "one point short is not a full bar");
            Assert.AreEqual(20, HullReadout.Blocks(1f, 20));
            Assert.AreEqual(20, HullReadout.Blocks(3f, 20));
            Assert.AreEqual(0, HullReadout.Blocks(0.5f, 0));
        }

        [Test]
        public void Blocks_NeverGoDown_AsTheShareRises()
        {
            int last = 0;
            for (float f = 0f; f <= 1f; f += 0.0007f)
            {
                int n = HullReadout.Blocks(f, HullReadout.PointBlocks);
                Assert.GreaterOrEqual(n, last);
                last = n;
            }
        }

        [Test]
        public void Clock_CountsDownRoundedUp()
        {
            ClockFace f = HullReadout.Clock(107f, false, 0f);
            Assert.AreEqual(1, f.Minutes);
            Assert.AreEqual(4, f.Tens);
            Assert.AreEqual(7, f.Ones);
            Assert.IsFalse(f.Over);

            f = HullReadout.Clock(0.2f, false, 0f);
            Assert.AreEqual(0, f.Minutes);
            Assert.AreEqual(0, f.Tens);
            Assert.AreEqual(1, f.Ones, "0:01 until it is really over");

            f = HullReadout.Clock(0f, false, 0f);
            Assert.AreEqual(0, f.Minutes * 100 + f.Tens * 10 + f.Ones);

            f = HullReadout.Clock(-5f, false, 0f);
            Assert.AreEqual(0, f.Minutes * 100 + f.Tens * 10 + f.Ones);

            f = HullReadout.Clock(300f, false, 0f);
            Assert.AreEqual(5, f.Minutes);
            Assert.AreEqual(0, f.Tens);
            Assert.AreEqual(0, f.Ones);
        }

        [Test]
        public void Clock_InOvertime_CountsUpFromZero()
        {
            ClockFace f = HullReadout.Clock(0f, true, 0.4f);
            Assert.IsTrue(f.Over);
            Assert.AreEqual(0, f.Minutes * 100 + f.Tens * 10 + f.Ones);

            f = HullReadout.Clock(0f, true, 75.9f);
            Assert.AreEqual(1, f.Minutes);
            Assert.AreEqual(1, f.Tens);
            Assert.AreEqual(5, f.Ones);
        }

        [Test]
        public void Clock_StaysOnItsTiles_HoweverLong()
        {
            ClockFace f = HullReadout.Clock(100000f, false, 0f);
            Assert.AreEqual(99, f.Minutes);
            Assert.AreEqual(5, f.Tens);
            Assert.AreEqual(9, f.Ones);
            f = HullReadout.Clock(float.NaN, false, 0f);
            Assert.AreEqual(0, f.Minutes);
        }

        [Test]
        public void SmallMagazine_IsOneBlockARound()
        {
            Assert.AreEqual(8, HullReadout.RoundBlockCount(8));
            Assert.AreEqual(5, HullReadout.RoundBlocksLit(5, 8));
            Assert.AreEqual(0, HullReadout.RoundBlocksLit(0, 8));
            Assert.AreEqual(8, HullReadout.RoundBlocksLit(8, 8));
            Assert.AreEqual(8, HullReadout.RoundBlocksLit(12, 8), "never more than the magazine");
            Assert.AreEqual(1, HullReadout.RoundBlockCount(1));
            Assert.AreEqual(1, HullReadout.RoundBlockCount(0));
        }

        [Test]
        public void LargeMagazine_IsTenBlocks_AndTheLastRoundStillShows()
        {
            Assert.AreEqual(10, HullReadout.RoundBlockCount(30));
            Assert.AreEqual(10, HullReadout.RoundBlocksLit(30, 30));
            Assert.AreEqual(9, HullReadout.RoundBlocksLit(29, 30), "one shot fired is not a full magazine");
            Assert.AreEqual(8, HullReadout.RoundBlocksLit(24, 30));
            Assert.AreEqual(1, HullReadout.RoundBlocksLit(1, 30));
            Assert.AreEqual(0, HullReadout.RoundBlocksLit(0, 30));
        }

        [Test]
        public void MagazineLow_WarnsNearTheEnd_ButNotOnAOneRoundGun()
        {
            Assert.IsFalse(HullReadout.MagazineLow(30, 30));
            Assert.IsFalse(HullReadout.MagazineLow(8, 30));
            Assert.IsTrue(HullReadout.MagazineLow(7, 30));
            Assert.IsTrue(HullReadout.MagazineLow(0, 30));
            Assert.IsTrue(HullReadout.MagazineLow(2, 8));
            Assert.IsFalse(HullReadout.MagazineLow(3, 8));
            Assert.IsFalse(HullReadout.MagazineLow(1, 1), "a loaded single shot is not low");
            Assert.IsTrue(HullReadout.MagazineLow(0, 1));
            Assert.IsFalse(HullReadout.MagazineLow(0, 0), "no gun, no warning");
        }

        [Test]
        public void Pips_AreOneAKill_UpToEight()
        {
            Assert.AreEqual(4, HullReadout.PipCount(4));
            Assert.AreEqual(8, HullReadout.PipCount(20));
            Assert.AreEqual(1, HullReadout.PipCount(0));
            Assert.AreEqual(0, HullReadout.PipsLit(0, 5));
            Assert.AreEqual(3, HullReadout.PipsLit(3, 5));
            Assert.AreEqual(5, HullReadout.PipsLit(5, 5));
            Assert.AreEqual(5, HullReadout.PipsLit(9, 5));
            Assert.AreEqual(1, HullReadout.PipsLit(1, 20), "the first kill of a long step shows");
            Assert.AreEqual(7, HullReadout.PipsLit(19, 20), "the last pip waits for the last kill");
            Assert.AreEqual(8, HullReadout.PipsLit(20, 20));
        }

        [Test]
        public void NoticeStrip_FitsItsWords_WithinTheScreen()
        {
            Assert.AreEqual(300f, HullReadout.NoticeWidth(40f));
            Assert.AreEqual(464f, HullReadout.NoticeWidth(400f));
            Assert.AreEqual(940f, HullReadout.NoticeWidth(2000f));
        }

        [Test]
        public void TheScreensFonts_AreInTheGame_WithTheirLicences()
        {
            string fonts = FontsFolder();
            if (fonts == null) Assert.Ignore("Resources/Fonts not found from the test folder.");
            foreach (string file in new[]
            {
                "SairaStencilOne-Regular.ttf", "SairaStencilOne-OFL.txt",
                "OverpassMono-SemiBold.ttf", "OverpassMono-Bold.ttf", "OverpassMono-OFL.txt",
            })
                Assert.IsTrue(System.IO.File.Exists(System.IO.Path.Combine(fonts, file)), file + " is missing from Resources/Fonts");
        }

        static string FontsFolder()
        {
            var dir = new System.IO.DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null)
            {
                string at = System.IO.Path.Combine(dir.FullName, "unity", "Assets", "OrbitalAnte", "Resources", "Fonts");
                if (System.IO.Directory.Exists(at)) return at;
                at = System.IO.Path.Combine(dir.FullName, "Assets", "OrbitalAnte", "Resources", "Fonts");
                if (System.IO.Directory.Exists(at)) return at;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
