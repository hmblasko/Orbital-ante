using NUnit.Framework;

namespace OrbitalAnte.Verification.Tests
{
    /// <summary>Picture comparison on made-up pictures whose differences are known exactly.</summary>
    public class ImageCompareTests
    {
        const int W = 64, H = 48;

        static byte[] Flat(byte r, byte g, byte b)
        {
            var data = new byte[W * H * 3];
            for (int i = 0; i < data.Length; i += 3) { data[i] = r; data[i + 1] = g; data[i + 2] = b; }
            return data;
        }

        /// <summary>A grey backdrop with a brighter textured box on it.</summary>
        static byte[] Scene(int boxX, int boxY, int boxW = 20, int boxH = 16)
        {
            byte[] data = Flat(82, 82, 82);
            for (int y = boxY; y < boxY + boxH; y++)
                for (int x = boxX; x < boxX + boxW; x++)
                {
                    int i = (y * W + x) * 3;
                    byte v = (byte)(150 + (x * 7 + y * 13) % 60);
                    data[i] = v; data[i + 1] = (byte)(v - 30); data[i + 2] = (byte)(v - 60);
                }
            return data;
        }

        [Test]
        public void IdenticalPicturesShowNoChange()
        {
            ImageDifference d = ImageCompare.Compare(Scene(10, 10), Scene(10, 10), W, H);
            Assert.IsTrue(d.SameSize);
            Assert.AreEqual(0, d.Rmse, 1e-12);
            Assert.AreEqual(0, d.ChangedFraction, 1e-12);
            Assert.AreEqual(1, d.Ssim, 1e-9);
            Assert.AreEqual("no visible change", ImageCompare.Describe(d));
        }

        [Test]
        public void AMovedShapeIsFoundAndBoxed()
        {
            byte[] bg = { 82, 82, 82 };
            ImageDifference d = ImageCompare.Compare(Scene(10, 10), Scene(30, 10), W, H, 8, bg);
            Assert.Greater(d.ChangedFraction, 0.1);
            Assert.Less(d.Ssim, 0.9);
            Assert.AreEqual(10, d.MinX);
            Assert.AreEqual(49, d.MaxX);
            Assert.AreEqual(10, d.MinY);
            Assert.AreEqual(25, d.MaxY);
            // The box moved its full width: 2 * 20 * 16 pixels swap between shape and backdrop.
            Assert.AreEqual(2 * 20 * 16 / (double)(W * H), d.SilhouetteChange, 1e-9);
        }

        [Test]
        public void AColourChangeInsideTheSameOutlineLeavesTheSilhouetteAlone()
        {
            byte[] a = Scene(10, 10), b = Scene(10, 10);
            for (int y = 10; y < 26; y++)
                for (int x = 10; x < 30; x++) b[(y * W + x) * 3 + 2] += 40;
            ImageDifference d = ImageCompare.Compare(a, b, W, H, 8, new byte[] { 82, 82, 82 });
            Assert.AreEqual(20 * 16 / (double)(W * H), d.ChangedFraction, 1e-9);
            Assert.AreEqual(0, d.SilhouetteChange, 1e-12);
        }

        [Test]
        public void ChangesWithinToleranceAreNotCounted()
        {
            byte[] a = Flat(100, 100, 100), b = Flat(104, 100, 97);
            ImageDifference d = ImageCompare.Compare(a, b, W, H, 8);
            Assert.AreEqual(0, d.ChangedFraction, 1e-12);
            Assert.Greater(d.Rmse, 0);
        }

        [Test]
        public void DifferentSizesAreNotCompared()
        {
            ImageDifference d = ImageCompare.Compare(new byte[30], new byte[60], 5, 2);
            Assert.IsFalse(d.SameSize);
            Assert.AreEqual("different size: cannot compare", ImageCompare.Describe(d));
        }

        [Test]
        public void ABlankFrameAndAPinkFrameAreFlagged()
        {
            Assert.IsTrue(ImageCompare.Stats(Flat(0, 0, 0), W, H).LooksBlank);
            Assert.AreEqual(1, ImageCompare.Stats(Flat(0, 0, 0), W, H).BlackFraction, 1e-12);
            ImageStats pink = ImageCompare.Stats(Flat(255, 0, 255), W, H);
            Assert.IsTrue(pink.LooksPink);
            ImageStats scene = ImageCompare.Stats(Scene(10, 10), W, H);
            Assert.IsFalse(scene.LooksBlank);
            Assert.IsFalse(scene.LooksPink);
        }

        [Test]
        public void TheDifferencePictureMarksChangedPixelsRed()
        {
            byte[] diff = ImageCompare.DifferenceImage(Scene(10, 10), Scene(30, 10), W, H);
            int changed = (12 * W + 12) * 3, same = (40 * W + 5) * 3;
            Assert.Greater(diff[changed], 120);
            Assert.Less(diff[changed + 1], 60);
            Assert.AreEqual(diff[same], diff[same + 1]);
            Assert.AreEqual(diff[same], diff[same + 2]);
        }
    }
}
