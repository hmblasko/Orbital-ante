using System;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.Space;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Planets seen from space (26 Nov 2026): the numbers behind each world's air and clouds, the lie of the land
    /// the far ball is lit by, the lights on its night side, and the painted pictures in Resources/Planets.
    /// </summary>
    public class OrbitLookTests
    {
        static PlanetProfile Profile(string id)
        {
            foreach (PlanetProfile p in PlanetProfiles.Defaults()) if (p.bodyId == id) return p;
            return null;
        }

        static double RadiusOf(string id)
        {
            foreach (BodySpec b in SolarSystemDefaults.Bodies()) if (b.id == id) return b.radius;
            return 0;
        }

        [Test]
        public void AWorldWithNoAir_HasAHardEdgeToItsNight_AndNoShell()
        {
            AirLook moon = OrbitLook.Air(Profile("Moon"), RadiusOf("Moon"), false);
            Assert.AreEqual(OrbitLook.AirlessSoft, moon.Soft);
            Assert.AreEqual(0, moon.Depth);
            Assert.AreEqual(1, moon.Shell);
            Assert.AreEqual(0, moon.ReddenB);
        }

        [Test]
        public void EveryWorldsAir_IsAThinShell_ThatHasFadedOutByItsEdge()
        {
            foreach (PlanetProfile profile in PlanetProfiles.Defaults())
            {
                double radius = RadiusOf(profile.bodyId);
                if (radius <= 0 || !profile.hasAtmosphere) continue;
                AirLook air = OrbitLook.Air(profile, radius, profile.cloudDeck);
                Assert.That(air.Depth, Is.GreaterThan(0).And.LessThanOrEqualTo(0.1), profile.bodyId);
                Assert.That(air.Scale, Is.InRange(0.01, 0.035), profile.bodyId + ": the rim is a thin line, not the old wide halo");
                Assert.That(air.Shell, Is.InRange(1.02, 1.3), profile.bodyId);
                Assert.That(Math.Exp(-(air.Shell - 1) / air.Scale), Is.LessThan(0.2), profile.bodyId + ": the air must be nearly gone where its ball ends");
                Assert.That(air.Soft, Is.InRange(OrbitLook.AirlessSoft, 0.08), profile.bodyId);
                Assert.That(air.ReddenB, Is.GreaterThan(air.ReddenR), profile.bodyId + ": low sunlight turns red, never blue");
            }
        }

        [Test]
        public void ThickerAir_ShowsMore_AndAGasGiantsEdgeDarkens()
        {
            AirLook earth = OrbitLook.Air(Profile("Earth"), RadiusOf("Earth"), false);
            AirLook mars = OrbitLook.Air(Profile("Mars"), RadiusOf("Mars"), false);
            AirLook jupiter = OrbitLook.Air(Profile("Jupiter"), RadiusOf("Jupiter"), true);
            Assert.That(earth.Depth, Is.GreaterThan(mars.Depth));
            Assert.That(earth.Soft, Is.GreaterThan(mars.Soft));
            Assert.AreEqual(1, jupiter.Hide);
            Assert.That(jupiter.Gain, Is.LessThan(earth.Gain));
        }

        [Test]
        public void AShell_ShowsWhollyFromAfar_AndIsGoneBeforeTheViewerReachesIt()
        {
            const double shell = 520;
            Assert.AreEqual(1, OrbitLook.ShellShown(shell * 3, shell, 0.25));
            Assert.AreEqual(0, OrbitLook.ShellShown(shell * 1.02, shell, 0.25));
            Assert.AreEqual(0, OrbitLook.ShellShown(shell * 0.5, shell, 0.25));
            double last = 0;
            for (double r = shell; r < shell * 1.5; r += 4)
            {
                double shown = OrbitLook.ShellShown(r, shell, 0.25);
                Assert.That(shown, Is.GreaterThanOrEqualTo(last).And.InRange(0, 1));
                last = shown;
            }
        }

        [Test]
        public void CloudShadowsOnTheFarBall_AreGone_BeforeTheRealGroundTakesItsPlace()
        {
            const double near = 1800;
            // The ground is put away again at 1.25 times the height it appears at: no shadow may show below that.
            Assert.AreEqual(0, OrbitLook.ShadowShown(near, near));
            Assert.AreEqual(0, OrbitLook.ShadowShown(near * 1.25, near));
            Assert.AreEqual(1, OrbitLook.ShadowShown(near * 2.5, near));
        }

        [Test]
        public void FlatGround_HasNoSlope_AndARiseToTheEastOrNorth_IsStoredAsOne()
        {
            const int width = 64, height = 32;
            const double radius = 200;
            var flat = new float[width * height];
            byte[] level = FarRelief.Paint(flat, width, radius, false, 0);
            for (int i = 0; i < width * height; i++)
            {
                Assert.AreEqual(0, FarRelief.Decode(level[i * 4]), 0.01);
                Assert.AreEqual(0, FarRelief.Decode(level[i * 4 + 1]), 0.01);
                Assert.AreEqual(0, level[i * 4 + 2]);
            }

            // Higher toward the north pole (the last rows): a slope to the north everywhere, none to the east.
            var north = new float[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) north[y * width + x] = y * 3f;
            byte[] tilted = FarRelief.Paint(north, width, radius, false, 0);
            int mid = (height / 2 * width + 10) * 4;
            double step = radius * Math.PI / height;
            Assert.AreEqual(3 / step, FarRelief.Decode(tilted[mid + 1]), 0.02);
            Assert.AreEqual(0, FarRelief.Decode(tilted[mid]), 0.01);

            // Higher toward the east (the way the picture's columns run) along the equator.
            var east = new float[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) east[y * width + x] = x * 2f;
            byte[] ramp = FarRelief.Paint(east, width, radius, false, 0);
            double across = radius * Math.PI * 2 / width * Math.Sin((1 - (height / 2 + 0.5) / height) * Math.PI);
            Assert.AreEqual(2 / across, FarRelief.Decode(ramp[mid]), 0.02);
            Assert.AreEqual(0, FarRelief.Decode(ramp[mid + 1]), 0.01);
        }

        [Test]
        public void TheSea_IsFlat_AndMarked()
        {
            const int width = 32, height = 16;
            var heights = new float[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) heights[y * width + x] = x < 16 ? -20f - x * 5f : 30f;
            byte[] lie = FarRelief.Paint(heights, width, 300, true, 0);
            int deep = (8 * width + 6) * 4, land = (8 * width + 24) * 4;
            Assert.AreEqual(255, lie[deep + 2]);
            Assert.AreEqual(0, FarRelief.Decode(lie[deep]), 0.01, "the sea bed's slope must not tilt the light on the water");
            Assert.AreEqual(0, lie[land + 2]);
        }

        [Test]
        public void ASlopeTooSteepToStore_IsHeldAtTheSteepest_AndStoringLosesLittle()
        {
            Assert.AreEqual(FarRelief.SteepestSlope, FarRelief.Decode(FarRelief.Encode(50)), 1e-9);
            Assert.AreEqual(-FarRelief.SteepestSlope, FarRelief.Decode(FarRelief.Encode(-50)), 1e-9);
            for (double s = -1.9; s < 1.9; s += 0.07) Assert.AreEqual(s, FarRelief.Decode(FarRelief.Encode(s)), 0.01);
        }

        [Test]
        public void TheFarPicture_HandsOverTheHeightsItWasPaintedFrom()
        {
            var terrain = new PlanetTerrain(Profile("Moon"), "Moon", RadiusOf("Moon"), 1);
            const int width = 64;
            var pixels = new byte[width * (width / 2) * 4];
            var heights = new float[width * (width / 2)];
            FarMap.Paint(terrain, width, pixels, 0, width / 2, heights);
            double low = double.MaxValue, high = double.MinValue;
            foreach (float h in heights) { low = Math.Min(low, h); high = Math.Max(high, h); }
            Assert.That(high - low, Is.GreaterThan(5), "the Moon is not a smooth ball");
            byte[] lie = FarRelief.Paint(heights, width, terrain.Radius, terrain.HasSea, terrain.SeaLevel);
            int tilted = 0;
            for (int i = 0; i < heights.Length; i++) if (lie[i * 4] != 128 || lie[i * 4 + 1] != 128) tilted++;
            Assert.That(tilted, Is.GreaterThan(heights.Length / 4), "its slopes must reach the far ball");
        }

        [Test]
        public void ANightLight_IsWhereItsBaseIs_EvenAcrossThePicturesSeam()
        {
            const int width = 256;
            var places = new[] { CubeSphere.FromLatLon(20, 40), new Vec3d(1, 0, -0.001).Normalized };
            byte[] lights = FarRelief.Lights(places, new[] { 0, 2 }, 2, width);
            FarMap.Locate(places[0], out double u, out double v);
            int at = (int)(v * (width / 2)) * width + (int)(u * width);
            Assert.That(lights[at], Is.GreaterThan(100));
            // The second sits on the seam (u just under 1): its glow must wrap to the first column, not be cut off.
            int row = width / 4 * width;
            Assert.That(lights[row + width - 1], Is.GreaterThan(100));
            Assert.That(lights[row], Is.GreaterThan(20));
            int lit = 0;
            foreach (byte b in lights) if (b > 0) lit++;
            Assert.That(lit, Is.LessThan(200), "two small dots, not a glow over the whole world");
        }

        // ------------------------------------------------------------------ the painted pictures and the shaders

        static string Assets(params string[] tail)
        {
            string folder = TestContext.CurrentContext.TestDirectory;
            for (int up = 0; up < 10 && folder != null; up++)
            {
                foreach (string head in new[] { Path.Combine("unity", "Assets", "OrbitalAnte"), Path.Combine("Assets", "OrbitalAnte") })
                {
                    string candidate = Path.Combine(folder, head, Path.Combine(tail));
                    if (Directory.Exists(candidate) || System.IO.File.Exists(candidate)) return candidate;
                }
                folder = Path.GetDirectoryName(folder);
            }
            string fromHere = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "OrbitalAnte", Path.Combine(tail));
            return Directory.Exists(fromHere) || System.IO.File.Exists(fromHere) ? fromHere : null;
        }

        static void AssertPicture(string resource, bool wantsAlpha)
        {
            string path = Assets("Resources", resource.Replace('/', Path.DirectorySeparatorChar) + ".png");
            Assert.IsNotNull(path, resource + ".png is missing: run tools/planet_pictures/bake_planets.py");
            byte[] head = new byte[26];
            using (FileStream stream = System.IO.File.OpenRead(path))
            {
                Assert.AreEqual(26, stream.Read(head, 0, 26));
                Assert.That(stream.Length, Is.LessThan(6L * 1024 * 1024), resource + " is too large to keep as an ordinary file");
            }
            Assert.AreEqual((byte)'P', head[1]);
            int width = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
            int height = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
            Assert.That(width, Is.InRange(512, 2048), resource);
            Assert.AreEqual(0, width & (width - 1), resource + ": the width must be a power of two");
            Assert.AreEqual(0, height & (height - 1), resource + ": the height must be a power of two");
            // PNG colour type 6 is red, green, blue and alpha.
            if (wantsAlpha) Assert.AreEqual(6, head[25], resource + " must carry alpha: it is how much cloud or ring there is");
        }

        [Test]
        public void EveryGasGiant_HasItsPaintedCloudTops()
        {
            int giants = 0;
            foreach (BodySpec body in SolarSystemDefaults.Bodies())
            {
                if (body.kind != BodyKind.GasGiant) { Assert.IsNull(OrbitLook.CloudTops(body.id), body.id); continue; }
                giants++;
                string tops = OrbitLook.CloudTops(body.id);
                Assert.IsNotNull(tops, body.id + " has no painted cloud tops");
                AssertPicture(tops, false);
            }
            Assert.That(giants, Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void EveryCloudLayer_HasItsPicture_AndBelongsToAWorldWithAir()
        {
            int layers = 0;
            foreach (PlanetProfile profile in PlanetProfiles.Defaults())
            {
                if (!OrbitLook.TryClouds(profile.bodyId, out CloudLook clouds)) continue;
                layers++;
                Assert.IsTrue(profile.hasAtmosphere && !profile.cloudDeck, profile.bodyId + ": clouds need air, and a gas giant's are its picture");
                Assert.That(clouds.Amount, Is.InRange(0.05, 1));
                Assert.That(clouds.Height, Is.InRange(0.05, 0.5), "the layer floats inside the air");
                Assert.That(clouds.Shadow, Is.InRange(0, 0.7));
                AssertPicture(clouds.Picture, clouds.Amount < 1);
            }
            Assert.AreEqual(3, layers);
            Assert.IsTrue(OrbitLook.TryClouds("Venus", out CloudLook venus) && venus.Amount >= 1, "Venus's deck hides its ground (Matthew, 26 Nov 2026)");
            AssertPicture(OrbitLook.RingPicture, true);
        }

        [Test]
        public void TheFarLooksShaders_KeepTheirSwitchesInABuild_AndUseNoReservedWords()
        {
            string body = System.IO.File.ReadAllText(Assets("Resources", "Shaders", "OA_Body.shader"));
            StringAssert.Contains("#pragma multi_compile_local _ _OA_REAL", body);
            StringAssert.Contains("#pragma multi_compile_local _ _OA_RELIEF", body);
            StringAssert.DoesNotContain("shader_feature_local", body.Replace("// multi_compile, not shader_feature", ""));
            foreach (string name in new[] { "OA_Body", "OA_Air", "OA_Clouds", "OA_Rings" })
            {
                string path = Assets("Resources", "Shaders", name + ".shader");
                Assert.IsNotNull(path, name + ".shader is missing");
                string text = System.IO.File.ReadAllText(path);
                StringAssert.Contains("OA_Look.hlsl", text, name + " must take its light from the shared look");
                foreach (string word in new[] { " smooth ", " smooth;", " smooth=", " lit ", " lit;", " point ", " sample " })
                    StringAssert.DoesNotContain("half" + word, text, name + ": '" + word.Trim() + "' is not a safe name once the shader is translated for the Deck");
            }
        }
    }
}
