using System;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Look;
using OrbitalAnte.Melee;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>
    /// The Props and Melee models (tools/blender/build_props.py) against what the game expects of them: the pieces
    /// it asks for by name, shapes that fit the plain colliders they are drawn over, and triangle counts.
    /// The script carries its own copy of these sizes: if this fails, change the script and run it again.
    /// </summary>
    public class PropsModelTests
    {
        static string ModelsFolder()
        {
            string env = Environment.GetEnvironmentVariable("OA_MODELS");
            if (!string.IsNullOrEmpty(env) && Directory.Exists(env)) return env;
            string dir = TestContext.CurrentContext.TestDirectory;
            for (int i = 0; i < 12 && dir != null; i++)
            {
                string candidate = Path.Combine(dir, "unity", "Assets", "OrbitalAnte", "Resources", "Models");
                if (Directory.Exists(candidate)) return candidate;
                candidate = Path.Combine(dir, "Assets", "OrbitalAnte", "Resources", "Models");
                if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        static ModelFile Load(string id)
        {
            string folder = ModelsFolder();
            if (folder == null) Assert.Ignore("Resources/Models not found");
            string path = Path.Combine(folder, id + ".bytes");
            Assert.IsTrue(File.Exists(path), "Model file not found: " + path);
            ModelFile model = ModelFile.Parse(File.ReadAllBytes(path), out string problem);
            Assert.IsNotNull(model, problem);
            return model;
        }

        static void Extent(ModelPart part, out float[] min, out float[] max)
        {
            min = new[] { float.MaxValue, float.MaxValue, float.MaxValue };
            max = new[] { float.MinValue, float.MinValue, float.MinValue };
            for (int i = 0; i < part.Positions.Length; i++)
            {
                int axis = i % 3;
                min[axis] = Math.Min(min[axis], part.Positions[i]);
                max[axis] = Math.Max(max[axis], part.Positions[i]);
            }
        }

        static ModelPart Piece(ModelFile model, string name, int mostTriangles)
        {
            ModelPart part = model.Part(name);
            Assert.IsNotNull(part, "missing piece " + name);
            Assert.Greater(part.TriangleCount, 0, name);
            Assert.LessOrEqual(part.TriangleCount, mostTriangles, name + " triangles");
            return part;
        }

        [Test]
        public void PropsModelFile_FitsWhatTheGameExpects()
        {
            ModelFile model = Load("Props");
            const float slack = 0.06f;

            // The footing is the 2.4 x 0.6 x 2.4 block that is solid; the cabinet may stand above it, nothing beside it.
            Extent(Piece(model, "mastBase", 4500), out float[] lo, out float[] hi);
            Assert.GreaterOrEqual(lo[0], -1.2f - slack); Assert.LessOrEqual(hi[0], 1.2f + slack);
            Assert.GreaterOrEqual(lo[2], -1.2f - slack); Assert.LessOrEqual(hi[2], 1.2f + slack);
            Assert.GreaterOrEqual(lo[1], -0.02f); Assert.LessOrEqual(hi[1], 2.0f);

            // The mast: its pole is the 9 m capsule that the rover rams, its dish about where the old disc was.
            Extent(Piece(model, "mast", 5000), out lo, out hi);
            Assert.GreaterOrEqual(lo[1], 0.5f, "the mast starts on the footing");
            Assert.That(hi[1], Is.InRange(9.5f, 10.6f), "the mast's top");
            Assert.LessOrEqual(Math.Max(hi[0], -lo[0]), 1.2f, "the mast stays over its footing sideways");

            // A cell is drawn over a 0.6 x 0.9 x 0.6 box round its own middle (the clamp may reach back to the pole).
            foreach (string name in new[] { "cell", "cellBurnt" })
            {
                Extent(Piece(model, name, 2600), out lo, out hi);
                Assert.GreaterOrEqual(lo[0], -0.3f - slack, name); Assert.LessOrEqual(hi[0], 0.3f + slack, name);
                Assert.GreaterOrEqual(lo[1], -0.45f - slack, name); Assert.LessOrEqual(hi[1], 0.45f + slack, name);
                Assert.LessOrEqual(hi[2], 0.3f + slack, name);
                Assert.GreaterOrEqual(lo[2], -0.55f, name);
            }
            Extent(Piece(model, "cellGlow", 300), out lo, out hi);
            Assert.Greater(lo[2], 0.2f, "the lit window is on the cell's outward face");

            // The crate: 0.8 across, 0.6 tall, 0.55 deep, standing on its origin.
            Extent(Piece(model, "crate", 5000), out lo, out hi);
            Assert.That(hi[0] - lo[0], Is.InRange(0.74f, 0.8f + 2 * slack));
            Assert.That(hi[1], Is.InRange(0.56f, 0.6f + slack));
            Assert.That(hi[2] - lo[2], Is.InRange(0.5f, 0.55f + 2 * slack));
            Assert.GreaterOrEqual(lo[1], -0.01f);
            Piece(model, "crateGlow", 300);

            // The pod: a 0.5 m barrel 1.4 m tall (legs may splay past it), its beacon at about 1.5 m.
            Extent(Piece(model, "pod", 4500), out lo, out hi);
            Assert.That(hi[1], Is.InRange(1.25f, 1.6f));
            Assert.LessOrEqual(Math.Max(Math.Max(hi[0], -lo[0]), Math.Max(hi[2], -lo[2])), 0.85f);
            Extent(Piece(model, "podGlow", 600), out lo, out hi);
            Assert.That(hi[1], Is.InRange(1.4f, 1.65f), "the beacon");

            // The cave lamp: stands on the ground, its lens facing forward at head height, as the code-made one did.
            Extent(Piece(model, "lamp", 4000), out lo, out hi);
            Assert.That(lo[1], Is.InRange(-0.01f, 0.03f));
            Extent(Piece(model, "lampGlow", 100), out lo, out hi);
            Assert.That((lo[1] + hi[1]) * 0.5f, Is.InRange(1.6f, 1.85f));
            Assert.Greater(lo[2], 0.05f, "the lens faces forward");
        }

        [Test]
        public void MeleeModelFile_HasEveryWeapon_HeldAtTheHand_WithItsTipWhereTheRulesSay()
        {
            ModelFile model = Load("Melee");
            foreach (string id in MeleeCatalog.Ids)
            {
                ModelPart part = Piece(model, id, 3000);
                Assert.IsTrue(model.TrySocket(id + "Tip", out ModelSocket tip), "missing socket " + id + "Tip");
                Assert.AreEqual(MeleeCatalog.TipHeight(id), tip.Y, 0.002f, id + " tip");
                Assert.AreEqual(0f, tip.X, 0.002f);
                Assert.AreEqual(0f, tip.Z, 0.002f);
                Extent(part, out float[] lo, out float[] hi);
                ModelPart glow = model.Part(id + "Glow");
                float top = hi[1];
                if (glow != null)
                {
                    Extent(glow, out float[] glo, out float[] ghi);
                    top = Math.Max(top, ghi[1]);
                }
                Assert.AreEqual(MeleeCatalog.TipHeight(id), top, 0.03f, id + " reaches its tip");
                Assert.That(lo[1], Is.InRange(-0.04f, 0.01f), id + " starts at the hand");
            }
            Assert.IsNotNull(model.Part(MeleeCatalog.LaserSwordId + "Glow"), "the sword's blade shines");
        }
    }
}
