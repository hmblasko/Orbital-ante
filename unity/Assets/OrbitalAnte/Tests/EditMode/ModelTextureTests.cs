using System;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Look;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Models drawn with texture pictures ("OAM3"): the file layout, and every textured model in Resources/Models
    /// (its far copy names the same pictures, every corner has a place on them, the pictures are there).
    /// </summary>
    public class ModelTextureTests
    {
        static ModelFile TexturedTriangle()
        {
            return new ModelFile
            {
                TextureSet = "Rifle",
                Parts = new[]
                {
                    new ModelPart
                    {
                        Name = "Body",
                        Positions = new float[] { 0, 0, 0, 1, 0, 0, 0, 1, 0 },
                        Normals = new float[] { 0, 0, 1, 0, 0, 1, 0, 0, 1 },
                        Colors = new float[] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f },
                        Finish = new float[] { 0.2f, 0f, 0.2f, 0f, 0.8f, 1f },
                        TexCoords = new float[] { 0.1f, 0.2f, 0.9f, 0.2f, 0.1f, 0.95f },
                        Tangents = new float[] { 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, -1 },
                        Triangles = new[] { 0, 1, 2 },
                    },
                },
                Sockets = new[] { new ModelSocket { Name = "Muzzle", X = 0, Y = 0.5f, Z = 2 } },
            };
        }

        [Test]
        public void ATexturedModel_SurvivesBeingWrittenAndReadBack()
        {
            byte[] bytes = TexturedTriangle().ToBytes();
            Assert.AreEqual((byte)'3', bytes[3], "a model with texture pictures is written as OAM3");
            ModelFile back = ModelFile.Parse(bytes, out string problem);
            Assert.IsNull(problem);
            Assert.AreEqual("Rifle", back.TextureSet);
            Assert.IsTrue(back.Textured);
            ModelPart part = back.Parts[0];
            Assert.AreEqual(6, part.TexCoords.Length);
            Assert.AreEqual(0.9f, part.TexCoords[2], 1e-6f);
            Assert.AreEqual(0.95f, part.TexCoords[5], 1e-6f);
            Assert.AreEqual(12, part.Tangents.Length);
            Assert.AreEqual(-1f, part.Tangents[11], 1e-6f);
            Assert.AreEqual(0.8f, part.Finish[4], 0.003f, "the finish is still carried");
            Assert.IsTrue(back.TrySocket("Muzzle", out _));
        }

        [Test]
        public void AnUntexturedModel_StaysTheOldLayout_SoNothingAlreadyMadeChanges()
        {
            ModelFile model = TexturedTriangle();
            model.TextureSet = null;
            byte[] bytes = model.ToBytes();
            Assert.AreEqual((byte)'2', bytes[3]);
            ModelFile back = ModelFile.Parse(bytes, out string problem);
            Assert.IsNull(problem);
            Assert.IsFalse(back.Textured);
            Assert.IsNull(back.Parts[0].TexCoords);
            Assert.IsNull(back.Parts[0].Tangents);
        }

        [Test]
        public void ATexturedFile_ThatDoesNotNameItsPictures_IsRefused()
        {
            byte[] bytes = TexturedTriangle().ToBytes();
            // The name follows the four-byte header as one length byte and its letters: say it is empty and drop them.
            int length = bytes[4];
            var broken = new byte[bytes.Length - length];
            Array.Copy(bytes, 0, broken, 0, 4);
            broken[4] = 0;
            Array.Copy(bytes, 5 + length, broken, 5, bytes.Length - 5 - length);
            Assert.IsNull(ModelFile.Parse(broken, out string problem));
            StringAssert.Contains("does not name", problem);
        }

        [Test]
        public void ATexturedFile_CutShort_IsRefused()
        {
            byte[] bytes = TexturedTriangle().ToBytes();
            var cut = new byte[bytes.Length - 30];
            Array.Copy(bytes, cut, cut.Length);
            Assert.IsNull(ModelFile.Parse(cut, out _));
        }

        [Test]
        public void TheTexturePictures_AreLookedForWhereTheGameLoadsThem()
        {
            Assert.AreEqual("Models/Textures/Rifle_Color", ModelFile.TextureFile("Rifle", ModelFile.TextureMap.Color));
            Assert.AreEqual("Models/Textures/Rifle_Normal", ModelFile.TextureFile("Rifle", ModelFile.TextureMap.Normal));
            Assert.AreEqual("Models/Textures/Rifle_Mask", ModelFile.TextureFile("Rifle", ModelFile.TextureMap.Mask));
        }

        static string ResourcesFolder()
        {
            string dir = TestContext.CurrentContext.TestDirectory;
            for (int i = 0; i < 10 && dir != null; i++)
            {
                foreach (string candidate in new[] { Path.Combine(dir, "Assets", "OrbitalAnte", "Resources"), Path.Combine(dir, "unity", "Assets", "OrbitalAnte", "Resources") })
                {
                    if (Directory.Exists(Path.Combine(candidate, "Models"))) return candidate;
                }
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        /// <summary>
        /// Every textured model in the game: its far copy is drawn with the same material, so it must name the same
        /// pictures and carry coordinates too; every corner sits on the pictures; the picture directions are real
        /// directions; and all three pictures are in Resources, or the game falls back to corner colours.
        /// </summary>
        [Test]
        public void EveryTexturedModel_HasItsPictures_AndItsFarCopyUsesThemToo()
        {
            string resources = ResourcesFolder();
            if (resources == null) Assert.Ignore("Resources folder not found");
            string models = Path.Combine(resources, "Models");
            int textured = 0;
            foreach (string path in Directory.GetFiles(models, "*.bytes"))
            {
                string id = Path.GetFileNameWithoutExtension(path);
                ModelFile model = ModelFile.Parse(File.ReadAllBytes(path), out string problem);
                Assert.IsNotNull(model, id + ": " + problem);
                if (ModelFile.IsFar(id))
                {
                    string full = id.Substring(0, id.Length - ModelFile.FarSuffix.Length);
                    string fullPath = Path.Combine(models, full + ".bytes");
                    if (File.Exists(fullPath))
                    {
                        ModelFile near = ModelFile.Parse(File.ReadAllBytes(fullPath), out _);
                        Assert.AreEqual(near.TextureSet, model.TextureSet, id + " must name the same texture pictures as " + full);
                    }
                }
                if (!model.Textured) continue;
                textured++;
                foreach (ModelPart part in model.Parts)
                {
                    if (ModelFile.Shines(part.Name)) continue;
                    Assert.AreEqual(part.VertexCount * 2, part.TexCoords.Length, id + " " + part.Name);
                    for (int i = 0; i < part.TexCoords.Length; i++)
                    {
                        Assert.That(part.TexCoords[i], Is.InRange(-0.001f, 1.001f), id + " " + part.Name + ": a corner off the pictures");
                    }
                    for (int i = 0; i < part.VertexCount; i++)
                    {
                        float x = part.Tangents[i * 4], y = part.Tangents[i * 4 + 1], z = part.Tangents[i * 4 + 2], w = part.Tangents[i * 4 + 3];
                        float length = (float)Math.Sqrt(x * x + y * y + z * z);
                        Assert.That(length, Is.InRange(0.9f, 1.1f), id + " " + part.Name + ": a picture direction that is not one length");
                        Assert.That(Math.Abs(w), Is.EqualTo(1f).Within(1e-4f), id + " " + part.Name + ": the picture's up must be +1 or -1");
                    }
                }
                foreach (ModelFile.TextureMap map in new[] { ModelFile.TextureMap.Color, ModelFile.TextureMap.Normal, ModelFile.TextureMap.Mask })
                {
                    string picture = Path.Combine(resources, ModelFile.TextureFile(model.TextureSet, map).Replace('/', Path.DirectorySeparatorChar) + ".png");
                    Assert.IsTrue(File.Exists(picture), id + " names texture set '" + model.TextureSet + "' but " + picture + " is missing");
                }
            }
            TestContext.WriteLine(textured + " textured models");
        }

        /// <summary>Width and height from a PNG's header (the IHDR chunk always comes first), or (0, 0) if it is not one.</summary>
        static void PngSize(string path, out int width, out int height)
        {
            width = height = 0;
            byte[] head = new byte[24];
            using (FileStream f = File.OpenRead(path))
            {
                if (f.Read(head, 0, 24) < 24) return;
            }
            if (head[1] != 'P' || head[2] != 'N' || head[3] != 'G') return;
            width = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
            height = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
        }

        /// <summary>
        /// The Steam Deck budget for model pictures (docs/performance_budget.md, "Texture pictures on models"): square,
        /// a power of two, never over 2048, the mask no larger than half the colour picture, and about 140 MB in all
        /// once compressed with mip maps (one byte a pixel, a third more for the mips).
        /// </summary>
        [Test]
        public void ModelPictures_StayInsideTheDecksBudget()
        {
            string resources = ResourcesFolder();
            if (resources == null) Assert.Ignore("Resources folder not found");
            string folder = Path.Combine(resources, "Models", "Textures");
            if (!Directory.Exists(folder)) Assert.Pass("no model pictures yet");
            double bytes = 0;
            foreach (string path in Directory.GetFiles(folder, "*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                PngSize(path, out int w, out int h);
                Assert.Greater(w, 0, name + " is not a PNG");
                Assert.AreEqual(w, h, name + " must be square");
                Assert.AreEqual(0, w & (w - 1), name + " must be a power of two across");
                Assert.LessOrEqual(w, 2048, name + " is over 2048");
                if (name.EndsWith("_Mask"))
                {
                    string colour = Path.Combine(folder, name.Substring(0, name.Length - "_Mask".Length) + "_Color.png");
                    if (File.Exists(colour))
                    {
                        PngSize(colour, out int cw, out _);
                        Assert.LessOrEqual(w, cw / 2, name + " should be half the size of its colour picture (oa.save_texture_set does this)");
                    }
                }
                bytes += w * (double)h * 4.0 / 3.0;
            }
            TestContext.WriteLine("model pictures: " + (bytes / (1024 * 1024)).ToString("0") + " MB once compressed");
            Assert.LessOrEqual(bytes, 140.0 * 1024 * 1024, "model pictures are over the Deck budget");
        }
    }
}
