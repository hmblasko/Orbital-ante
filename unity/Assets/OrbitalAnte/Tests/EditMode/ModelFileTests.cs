using System.IO;
using NUnit.Framework;
using OrbitalAnte.Look;

namespace OrbitalAnte.Tests
{
    /// <summary>The game's Blender-made models: the file reader, and the real gun files in Resources/Models.</summary>
    public class ModelFileTests
    {
        static ModelFile Triangle()
        {
            return new ModelFile
            {
                Parts = new[]
                {
                    new ModelPart
                    {
                        Name = "Body",
                        Positions = new float[] { 0, 0, 0, 1, 0, 0, 0, 1, 0 },
                        Normals = new float[] { 0, 0, 1, 0, 0, 1, 0, 0, 1 },
                        Colors = new float[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 },
                        Triangles = new[] { 0, 1, 2 },
                    },
                },
                Sockets = new[] { new ModelSocket { Name = "Muzzle", X = 0, Y = 0.5f, Z = 2 } },
            };
        }

        static string ModelsFolder()
        {
            string dir = TestContext.CurrentContext.TestDirectory;
            for (int i = 0; i < 10 && dir != null; i++)
            {
                foreach (string candidate in new[] { Path.Combine(dir, "Assets", "OrbitalAnte", "Resources", "Models"), Path.Combine(dir, "unity", "Assets", "OrbitalAnte", "Resources", "Models") })
                {
                    if (Directory.Exists(candidate)) return candidate;
                }
                dir = Path.GetDirectoryName(dir);
            }
            string fromEnv = System.Environment.GetEnvironmentVariable("OA_MODELS");
            return !string.IsNullOrEmpty(fromEnv) && Directory.Exists(fromEnv) ? fromEnv : Path.Combine(Directory.GetCurrentDirectory(), "Assets", "OrbitalAnte", "Resources", "Models");
        }

        static ModelFile LoadReal(string id)
        {
            string path = Path.Combine(ModelsFolder(), id + ".bytes");
            Assert.IsTrue(File.Exists(path), "Model file not found: " + path);
            ModelFile model = ModelFile.Parse(File.ReadAllBytes(path), out string problem);
            Assert.IsNotNull(model, problem);
            return model;
        }

        [Test]
        public void AModelSurvivesBeingWrittenAndReadBack()
        {
            ModelFile back = ModelFile.Parse(Triangle().ToBytes(), out string problem);
            Assert.IsNull(problem);
            Assert.AreEqual(1, back.Parts.Length);
            Assert.AreEqual("Body", back.Parts[0].Name);
            Assert.AreEqual(3, back.Parts[0].VertexCount);
            Assert.AreEqual(1f, back.Parts[0].Positions[3], 1e-6f);
            Assert.AreEqual(1f, back.Parts[0].Colors[4], 1e-6f);
            Assert.IsTrue(back.TrySocket("Muzzle", out ModelSocket muzzle));
            Assert.AreEqual(2f, muzzle.Z, 1e-6f);
            Assert.IsFalse(back.TrySocket("Nope", out _));
        }

        [Test]
        public void DamagedFilesAreRefusedWithAReasonAndNeverThrow()
        {
            byte[] good = Triangle().ToBytes();
            Assert.IsNull(ModelFile.Parse(null, out string p0)); Assert.IsNotNull(p0);
            Assert.IsNull(ModelFile.Parse(new byte[3], out string p1)); Assert.IsNotNull(p1);
            byte[] wrongStart = (byte[])good.Clone(); wrongStart[0] = (byte)'X';
            Assert.IsNull(ModelFile.Parse(wrongStart, out string p2)); Assert.IsNotNull(p2);
            for (int length = 8; length < good.Length; length += 3)
            {
                byte[] cut = new byte[length];
                System.Array.Copy(good, cut, length);
                Assert.IsNull(ModelFile.Parse(cut, out string p3), "a file cut to " + length + " bytes was accepted");
                Assert.IsNotNull(p3);
            }
            ModelFile bad = Triangle();
            bad.Parts[0].Triangles[2] = 7;
            Assert.IsNull(ModelFile.Parse(bad.ToBytes(), out string p4)); Assert.IsNotNull(p4);
            byte[] hugeCount = (byte[])good.Clone(); hugeCount[7] = 0x7f;
            Assert.IsNull(ModelFile.Parse(hugeCount, out string p5)); Assert.IsNotNull(p5);
        }

        [TestCase("Rifle", 0.69f, 0.045f, 0.9f, 1.15f)]
        [TestCase("Pistol", 0.155f, 0.062f, 0.17f, 0.24f)]
        public void TheGunsFitTheirAttachmentPoints(string id, float muzzleZ, float muzzleY, float minLength, float maxLength)
        {
            ModelFile model = LoadReal(id);
            Assert.IsNotNull(model.Part("Body"));
            Assert.IsNotNull(model.Part("Magazine"), "the magazine must be its own piece so a reload can hide it");
            Assert.IsTrue(model.TrySocket("Muzzle", out ModelSocket muzzle));
            Assert.IsTrue(model.TrySocket("SupportHand", out _));
            Assert.IsTrue(model.TrySocket("Magazine", out ModelSocket magazine));
            // The same places the stand-in guns used, so hands, tracers and reloads still line up.
            Assert.AreEqual(muzzleZ, muzzle.Z, 0.001f);
            Assert.AreEqual(muzzleY, muzzle.Y, 0.001f);
            Assert.AreEqual(0f, muzzle.X, 0.001f);

            model.Extent(out float minX, out float minY, out float minZ, out float maxX, out float maxY, out float maxZ);
            Assert.That(maxZ - minZ, Is.InRange(minLength, maxLength), "length");
            // The barrel ends at the muzzle (within a centimetre), and the hand hold (the origin) is inside the gun.
            Assert.AreEqual(muzzle.Z, maxZ, 0.012f);
            Assert.Less(minZ, 0f); Assert.Greater(maxZ, 0f);
            Assert.Less(minY, 0f); Assert.Greater(maxY, 0f);
            // Guns are the same on both sides to within a handle's width.
            Assert.Less(System.Math.Abs(maxX + minX), 0.04f);
            // The magazine piece sits near its socket.
            ModelPart mag = model.Part("Magazine");
            float nearest = float.MaxValue;
            for (int i = 0; i < mag.Positions.Length; i += 3)
            {
                float dx = mag.Positions[i] - magazine.X, dy = mag.Positions[i + 1] - magazine.Y, dz = mag.Positions[i + 2] - magazine.Z;
                nearest = System.Math.Min(nearest, (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz));
            }
            Assert.Less(nearest, 0.06f);
        }

        [TestCase("Rifle")]
        [TestCase("Pistol")]
        public void TheGunsStayInsideTheirBudgetAndAreSoundMeshes(string id)
        {
            ModelFile model = LoadReal(id);
            Assert.LessOrEqual(model.TriangleCount, 9000, "docs/performance_budget.md caps a gun at 9,000 triangles");
            foreach (ModelPart part in model.Parts)
            {
                Assert.AreEqual(part.Positions.Length, part.Normals.Length);
                Assert.AreEqual(part.Positions.Length, part.Colors.Length);
                for (int i = 0; i < part.Normals.Length; i += 3)
                {
                    float n = part.Normals[i] * part.Normals[i] + part.Normals[i + 1] * part.Normals[i + 1] + part.Normals[i + 2] * part.Normals[i + 2];
                    Assert.That(n, Is.InRange(0.9f, 1.1f), part.Name + " has a corner with no facing");
                }
                // Every triangle's corner order must agree with the facing stored at its corners.
                model.Extent(out float minX, out float minY, out float minZ, out float maxX, out float maxY, out float maxZ);
                int outward = 0, total = 0;
                for (int t = 0; t < part.Triangles.Length; t += 3)
                {
                    int a = part.Triangles[t] * 3, b = part.Triangles[t + 1] * 3, c = part.Triangles[t + 2] * 3;
                    float ux = part.Positions[b] - part.Positions[a], uy = part.Positions[b + 1] - part.Positions[a + 1], uz = part.Positions[b + 2] - part.Positions[a + 2];
                    float vx = part.Positions[c] - part.Positions[a], vy = part.Positions[c + 1] - part.Positions[a + 1], vz = part.Positions[c + 2] - part.Positions[a + 2];
                    // Unity works a triangle's facing out as (b - a) x (c - a); it must agree with the stored facing.
                    float fx = uy * vz - uz * vy, fy = uz * vx - ux * vz, fz = ux * vy - uy * vx;
                    float agree = fx * part.Normals[a] + fy * part.Normals[a + 1] + fz * part.Normals[a + 2];
                    if (System.Math.Abs(agree) < 1e-12f) continue;
                    total++;
                    if (agree > 0f) outward++;
                }
                Assert.Greater(outward, total * 0.98f, part.Name + ": triangles are wound the wrong way for Unity");
            }
        }
    }
}
