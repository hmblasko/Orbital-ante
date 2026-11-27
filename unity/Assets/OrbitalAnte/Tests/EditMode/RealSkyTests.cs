using System;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Look;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// The real sky (26 Nov 2026): the star file's layout, how a star's magnitude becomes a dot, and the two baked
    /// files in Resources/Sky (the stars really are where they are in the real sky, seen the right way round; the
    /// picture is the size the sky shader expects and small enough to keep in the repository).
    /// </summary>
    public class RealSkyTests
    {
        static byte[] StarFile(int count, int real, Action<BinaryWriter> stars)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(new[] { (byte)'O', (byte)'A', (byte)'S', (byte)'1' });
                writer.Write(count);
                writer.Write(real);
                stars(writer);
                writer.Flush();
                return stream.ToArray();
            }
        }

        static void Star(BinaryWriter writer, float x, float y, float z, float magnitude, byte r = 255, byte g = 255, byte b = 255)
        {
            writer.Write(x); writer.Write(y); writer.Write(z); writer.Write(magnitude);
            writer.Write(r); writer.Write(g); writer.Write(b); writer.Write((byte)0);
        }

        [Test]
        public void AStarFile_IsReadBack_AsItWasWritten()
        {
            byte[] bytes = StarFile(2, 1, w => { Star(w, 0, 1, 0, -1.46f, 200, 220, 255); Star(w, 0.6f, 0, 0.8f, 7.5f); });
            StarCatalogue catalogue = StarCatalogue.Parse(bytes, out string problem);
            Assert.IsNull(problem);
            Assert.AreEqual(2, catalogue.Stars.Length);
            Assert.AreEqual(1, catalogue.RealCount);
            Assert.AreEqual(1f, catalogue.Stars[0].Y, 1e-6f);
            Assert.AreEqual(-1.46f, catalogue.Stars[0].Magnitude, 1e-6f);
            Assert.AreEqual(200 / 255f, catalogue.Stars[0].R, 1e-6f);
            Assert.AreEqual(0.8f, catalogue.Stars[1].Z, 1e-6f);
        }

        [Test]
        public void ABrokenStarFile_IsRefusedWithAReason_NeverHalfRead()
        {
            Assert.IsNull(StarCatalogue.Parse(null, out string none));
            Assert.IsNotNull(none);
            Assert.IsNull(StarCatalogue.Parse(new byte[] { 1, 2, 3 }, out string shortFile));
            Assert.IsNotNull(shortFile);

            byte[] wrongLetters = StarFile(1, 1, w => Star(w, 0, 1, 0, 1f));
            wrongLetters[3] = (byte)'9';
            Assert.IsNull(StarCatalogue.Parse(wrongLetters, out string letters));
            StringAssert.Contains("OAS1", letters);

            byte[] cutShort = StarFile(2, 1, w => Star(w, 0, 1, 0, 1f));
            Assert.IsNull(StarCatalogue.Parse(cutShort, out string length));
            Assert.IsNotNull(length);

            byte[] tooMany = StarFile(StarCatalogue.MostStars + 1, 0, w => { });
            Assert.IsNull(StarCatalogue.Parse(tooMany, out string many));
            Assert.IsNotNull(many);

            byte[] moreRealThanStars = StarFile(1, 2, w => Star(w, 0, 1, 0, 1f));
            Assert.IsNull(StarCatalogue.Parse(moreRealThanStars, out string real));
            Assert.IsNotNull(real);

            byte[] noDirection = StarFile(1, 1, w => Star(w, 0, 0, 0, 1f));
            Assert.IsNull(StarCatalogue.Parse(noDirection, out string direction));
            Assert.IsNotNull(direction);

            byte[] noBrightness = StarFile(1, 1, w => Star(w, 0, 1, 0, float.NaN));
            Assert.IsNull(StarCatalogue.Parse(noBrightness, out string brightness));
            Assert.IsNotNull(brightness);
        }

        [Test]
        public void ABrighterStar_IsNeverDrawnDimmerOrSmaller()
        {
            float lastPeak = float.MaxValue, lastSize = float.MaxValue;
            for (float magnitude = -2f; magnitude <= 11f; magnitude += 0.25f)
            {
                float peak = StarLook.Peak(magnitude), size = StarLook.HalfSize(magnitude);
                Assert.That(peak, Is.LessThanOrEqualTo(lastPeak), "magnitude " + magnitude);
                Assert.That(size, Is.LessThanOrEqualTo(lastSize), "magnitude " + magnitude);
                Assert.That(peak, Is.GreaterThan(0f));
                Assert.That(peak, Is.LessThanOrEqualTo(StarLook.BrightestPeak));
                Assert.That(size, Is.InRange(StarLook.SmallestHalfSize, StarLook.LargestHalfSize));
                lastPeak = peak;
                lastSize = size;
            }
        }

        [Test]
        public void TheSkyKeepsARealNightsSpreadOfBrightness()
        {
            // The faintest star the eye sees is a faint dot, a first-magnitude star is brighter than white (it
            // blooms), and the brightest stars are far brighter still: not the even sprinkle the old sky had.
            Assert.That(StarLook.Peak(6.5f), Is.InRange(0.08f, 0.2f));
            Assert.That(StarLook.Peak(8.2f), Is.InRange(0.02f, 0.08f), "the faintest drawn star must still show on a dark sky");
            Assert.That(StarLook.Peak(1f), Is.GreaterThan(1.25f), "a first-magnitude star passes the bloom threshold");
            Assert.That(StarLook.Peak(-1.46f) / StarLook.Peak(6.5f), Is.GreaterThan(25f));
            // Only the few hundred brightest stars are drawn larger; the rest are the smallest crisp dot.
            Assert.AreEqual(StarLook.SmallestHalfSize, StarLook.HalfSize(4f));
            Assert.That(StarLook.HalfSize(0f), Is.GreaterThan(3f));
            Assert.That(StarLook.HalfSize(-1.46f), Is.LessThanOrEqualTo(4f), "no star is a lamp: a far planet must stay the bigger thing");
        }

        // ------------------------------------------------------------------ the baked files

        static string SkyFolder()
        {
            string given = Environment.GetEnvironmentVariable("OA_SKY");
            if (!string.IsNullOrEmpty(given) && Directory.Exists(given)) return given;
            string folder = TestContext.CurrentContext.TestDirectory;
            for (int up = 0; up < 10 && folder != null; up++)
            {
                foreach (string tail in new[] { Path.Combine("unity", "Assets", "OrbitalAnte", "Resources", "Sky"), Path.Combine("Assets", "OrbitalAnte", "Resources", "Sky") })
                {
                    string candidate = Path.Combine(folder, tail);
                    if (Directory.Exists(candidate)) return candidate;
                }
                folder = Path.GetDirectoryName(folder);
            }
            string fromHere = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "OrbitalAnte", "Resources", "Sky");
            return Directory.Exists(fromHere) ? fromHere : null;
        }

        static StarCatalogue Baked()
        {
            string folder = SkyFolder();
            Assert.IsNotNull(folder, "Resources/Sky was not found from the test folder");
            StarCatalogue catalogue = StarCatalogue.Parse(System.IO.File.ReadAllBytes(Path.Combine(folder, "Stars.bytes")), out string problem);
            Assert.IsNull(problem);
            return catalogue;
        }

        /// <summary>A place given the astronomers' way (right ascension in hours, declination in degrees) in the game's sky frame.</summary>
        static void GameDirection(double hours, double degrees, out double x, out double y, out double z)
        {
            double ra = hours * Math.PI / 12.0, dec = degrees * Math.PI / 180.0, tilt = 23.4392911 * Math.PI / 180.0;
            double ex = Math.Cos(dec) * Math.Cos(ra), ey = Math.Cos(dec) * Math.Sin(ra), ez = Math.Sin(dec);
            // Tip the Earth's equator onto the planets' plane, then the game's Y is that plane's north.
            x = ex;
            z = ey * Math.Cos(tilt) + ez * Math.Sin(tilt);
            y = -ey * Math.Sin(tilt) + ez * Math.Cos(tilt);
        }

        static SkyStar Nearest(StarCatalogue catalogue, double hours, double degrees, out double degreesOff)
        {
            GameDirection(hours, degrees, out double x, out double y, out double z);
            double best = -2;
            SkyStar found = default;
            for (int i = 0; i < catalogue.RealCount; i++)
            {
                SkyStar s = catalogue.Stars[i];
                double dot = s.X * x + s.Y * y + s.Z * z;
                if (dot > best) { best = dot; found = s; }
            }
            degreesOff = Math.Acos(Math.Min(1.0, best)) * 180.0 / Math.PI;
            return found;
        }

        [Test]
        public void TheBakedStars_AreTheRealOnes_WhereTheyReallyAre()
        {
            StarCatalogue catalogue = Baked();
            Assert.That(catalogue.RealCount, Is.InRange(9000, 9110), "the whole Bright Star Catalogue");
            Assert.That(catalogue.Stars.Length, Is.InRange(25000, StarCatalogue.MostStars));

            // The brightest star in the sky is Sirius, and it is where Sirius is.
            SkyStar first = catalogue.Stars[0];
            Assert.AreEqual(-1.46f, first.Magnitude, 0.01f);
            SkyStar sirius = Nearest(catalogue, 6.7525, -16.716, out double off);
            Assert.That(off, Is.LessThan(0.05));
            Assert.AreEqual(first.X, sirius.X, 1e-6f);

            // A few everyone knows, with their colours: Betelgeuse is orange, Rigel blue-white.
            SkyStar betelgeuse = Nearest(catalogue, 5.9195, 7.407, out off);
            Assert.That(off, Is.LessThan(0.05));
            Assert.That(betelgeuse.Magnitude, Is.LessThan(1f));
            Assert.That(betelgeuse.R, Is.GreaterThan(betelgeuse.B + 0.2f), "Betelgeuse is orange");
            SkyStar rigel = Nearest(catalogue, 5.2423, -8.202, out off);
            Assert.That(off, Is.LessThan(0.05));
            Assert.That(rigel.B, Is.GreaterThanOrEqualTo(rigel.R), "Rigel is blue-white");
            SkyStar polaris = Nearest(catalogue, 2.5303, 89.264, out off);
            Assert.That(off, Is.LessThan(0.05));
            Assert.That(polaris.Magnitude, Is.InRange(1.8f, 2.2f));
        }

        [Test]
        public void TheSkyIsNotMirrored_SiriusIsLeftOfOrionWithNorthUp()
        {
            // Looking at Betelgeuse from inside the sky with north up, Sirius is down and to the LEFT (east is left
            // on the sky). In Unity's left-handed frame "right" is up x forward. A mirrored sky would put it right.
            GameDirection(5.9195, 7.407, out double fx, out double fy, out double fz);
            GameDirection(6.7525, -16.716, out double sx, out double sy, out double sz);
            GameDirection(0, 90, out double nx, out double ny, out double nz);
            double rx = ny * fz - nz * fy, ry = nz * fx - nx * fz, rz = nx * fy - ny * fx;
            Assert.That(sx * rx + sy * ry + sz * rz, Is.LessThan(-0.1), "Sirius must be to the left");
            Assert.That(sx * nx + sy * ny + sz * nz, Is.LessThan(fx * nx + fy * ny + fz * nz), "and lower");

            // And the baked file agrees with that frame: the star nearest that place is Sirius itself.
            SkyStar sirius = Nearest(Baked(), 6.7525, -16.716, out double off);
            Assert.That(off, Is.LessThan(0.05));
            Assert.AreEqual(-1.46f, sirius.Magnitude, 0.01f);
        }

        [Test]
        public void ThePlanetsPlane_RunsThroughTheZodiac()
        {
            // The game's Y is the north of the planets' plane, so stars of the zodiac lie close to Y = 0 and the
            // Pole Star does not. Regulus sits almost on the plane; Polaris is 66 degrees above it.
            StarCatalogue catalogue = Baked();
            SkyStar regulus = Nearest(catalogue, 10.1395, 11.967, out _);
            Assert.That(Math.Abs(regulus.Y), Is.LessThan(0.02f));
            SkyStar polaris = Nearest(catalogue, 2.5303, 89.264, out _);
            Assert.That(polaris.Y, Is.GreaterThan(0.9f));
        }

        [Test]
        public void TheExtraFaintStars_AreAllFainterThanTheEyeSees_AndThickerAlongTheMilkyWay()
        {
            StarCatalogue catalogue = Baked();
            // The galaxy's north pole in the game's frame (right ascension 12 h 51.4 m, declination +27.13).
            GameDirection(12.8567, 27.13, out double px, out double py, out double pz);
            int near = 0, far = 0;
            for (int i = catalogue.RealCount; i < catalogue.Stars.Length; i++)
            {
                SkyStar s = catalogue.Stars[i];
                Assert.That(s.Magnitude, Is.GreaterThanOrEqualTo(6.5f), "an invented star must never pass for a naked-eye one");
                double height = Math.Abs(s.X * px + s.Y * py + s.Z * pz);
                // Two bands of the same area: within 14.5 degrees of the Milky Way's plane, and above 48.6 degrees.
                if (height < 0.25) near++;
                else if (height > 0.75) far++;
            }
            Assert.That(near, Is.GreaterThan(far * 1.5), "more faint stars along the Milky Way than toward its poles");
            Assert.That(far, Is.GreaterThan(500), "but the poles are not empty");
        }

        [Test]
        public void TheBakedPicture_IsTheSizeTheSkyExpects_AndSmallEnoughToKeep()
        {
            string folder = SkyFolder();
            Assert.IsNotNull(folder, "Resources/Sky was not found from the test folder");
            string path = Path.Combine(folder, "DeepSpace.png");
            Assert.IsTrue(System.IO.File.Exists(path), "the baked picture of deep space is missing: run tools/sky/bake_sky.py");
            byte[] head = new byte[24];
            long length;
            using (FileStream stream = System.IO.File.OpenRead(path))
            {
                length = stream.Length;
                Assert.AreEqual(24, stream.Read(head, 0, 24));
            }
            Assert.AreEqual(0x89, head[0]);
            Assert.AreEqual((byte)'P', head[1]);
            int width = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
            int height = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
            Assert.AreEqual(4096, width, "twice as wide as tall, wrapped once round the sky");
            Assert.AreEqual(2048, height);
            Assert.That(length, Is.LessThan(24L * 1024 * 1024), "the picture is an ordinary file in the repository: keep it under 24 MB");
        }
    }
}
