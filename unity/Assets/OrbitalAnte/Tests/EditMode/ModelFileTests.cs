using System.IO;
using NUnit.Framework;
using OrbitalAnte.Look;
using OrbitalAnte.Ship;

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
            ModelFile lostMuzzle = Triangle();
            lostMuzzle.Sockets[0].Z = float.NaN;
            Assert.IsNull(ModelFile.Parse(lostMuzzle.ToBytes(), out string p6)); Assert.IsNotNull(p6);
            ModelFile farMuzzle = Triangle();
            farMuzzle.Sockets[0].X = float.PositiveInfinity;
            Assert.IsNull(ModelFile.Parse(farMuzzle.ToBytes(), out string p7)); Assert.IsNotNull(p7);
            ModelFile noFacing = Triangle();
            noFacing.Parts[0].Normals[4] = float.NaN;
            Assert.IsNull(ModelFile.Parse(noFacing.ToBytes(), out string p8)); Assert.IsNotNull(p8);
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

        // ------------------------------------------------------------------ the lander's outside fittings

        static void AssertSocket(ModelFile model, string name, P3 expected)
        {
            Assert.IsTrue(model.TrySocket(name, out ModelSocket s), "missing " + name);
            Assert.AreEqual(expected.X, s.X, 0.002f, name + " x");
            Assert.AreEqual(expected.Y, s.Y, 0.002f, name + " y");
            Assert.AreEqual(expected.Z, s.Z, 0.002f, name + " z");
        }

        [Test]
        public void ShipModelFile_MatchesTheShipLayout()
        {
            // The Blender script carries its own copy of these numbers. If ShipLayout changes, this fails until
            // tools/blender/build_ship.py is changed to match and run again.
            ModelFile model = LoadReal("Lander");
            for (int i = 0; i < ShipLayout.Legs; i++)
            {
                AssertSocket(model, "LegRoot" + i, ShipLayout.LegRoot(i));
                AssertSocket(model, "Footpad" + i, ShipLayout.Footpad(i));
                AssertSocket(model, "Engine" + i, ShipLayout.Engine(i));
                AssertSocket(model, "Thruster" + i, ShipLayout.Thruster(i));
            }
            AssertSocket(model, "DishCentre", ShipLayout.DishCentre);
            AssertSocket(model, "DishMastFoot", ShipLayout.DishMastFoot);
            foreach (string piece in new[] { "base", "leg0", "leg1", "leg2", "leg3", "engines", "thruster0", "thruster1", "thruster2", "thruster3", "dish" })
            {
                Assert.IsNotNull(model.Part(piece), "missing piece " + piece);
            }
            Assert.LessOrEqual(model.TriangleCount, 34000, "docs/performance_budget.md caps the lander's fittings at 34,000 triangles");
        }

        [Test]
        public void ShipModelFile_FootpadsSitOnTheGround_AndNothingGoesBelowIt([Values(0, 1, 2, 3)] int index)
        {
            ModelFile model = LoadReal("Lander");
            ModelPart leg = model.Part("leg" + index);
            P3 pad = ShipLayout.Footpad(index), root = ShipLayout.LegRoot(index);
            float lowest = float.MaxValue, widest = 0f, nearestRoot = float.MaxValue;
            for (int i = 0; i < leg.Positions.Length; i += 3)
            {
                float x = leg.Positions[i], y = leg.Positions[i + 1], z = leg.Positions[i + 2];
                lowest = System.Math.Min(lowest, y);
                float flat = (float)System.Math.Sqrt((x - pad.X) * (x - pad.X) + (z - pad.Z) * (z - pad.Z));
                if (y < ShipLayout.GroundY + 0.14f) widest = System.Math.Max(widest, flat);
                nearestRoot = System.Math.Min(nearestRoot, P3.Distance(new P3(x, y, z), root));
            }
            // The pad's collider is a ball of FootpadRadius resting on the ground; the picture must agree with it.
            Assert.AreEqual(ShipLayout.GroundY, lowest, 0.002f);
            Assert.AreEqual(ShipLayout.FootpadRadius, widest, 0.02f);
            Assert.Less(nearestRoot, 0.3f, "leg " + index + " does not reach its root");
            foreach (ModelPart part in model.Parts)
            {
                for (int i = 1; i < part.Positions.Length; i += 3) Assert.GreaterOrEqual(part.Positions[i], ShipLayout.GroundY - 0.002f, part.Name);
            }
        }

        [Test]
        public void ShipModelFile_PiecesStayWhereTheirCollidersAre()
        {
            ModelFile model = LoadReal("Lander");
            // Each Blender piece must cover about the same space as the shape it replaces, because the
            // colliders, repair anchors and tear-off pivots were all placed for those shapes.
            var standIns = new System.Collections.Generic.Dictionary<string, ShipMesh>();
            foreach (var pair in ShipProps.Exterior()) standIns[pair.Key] = pair.Value;
            foreach (ModelPart part in model.Parts)
            {
                Assert.IsTrue(standIns.ContainsKey(part.Name), "no stand-in for " + part.Name);
                ShipMesh old = standIns[part.Name];
                float[] lo = { float.MaxValue, float.MaxValue, float.MaxValue }, hi = { float.MinValue, float.MinValue, float.MinValue };
                float[] oldLo = { float.MaxValue, float.MaxValue, float.MaxValue }, oldHi = { float.MinValue, float.MinValue, float.MinValue };
                for (int i = 0; i < part.Positions.Length; i++) { lo[i % 3] = System.Math.Min(lo[i % 3], part.Positions[i]); hi[i % 3] = System.Math.Max(hi[i % 3], part.Positions[i]); }
                for (int i = 0; i < old.Positions.Length; i++) { oldLo[i % 3] = System.Math.Min(oldLo[i % 3], old.Positions[i]); oldHi[i % 3] = System.Math.Max(oldHi[i % 3], old.Positions[i]); }
                for (int axis = 0; axis < 3; axis++)
                {
                    Assert.AreEqual(oldLo[axis], lo[axis], 0.4f, part.Name + " low edge, axis " + axis);
                    Assert.AreEqual(oldHi[axis], hi[axis], 0.4f, part.Name + " high edge, axis " + axis);
                }
            }
            // The engine bells end where the game says the flame starts.
            ModelPart engines = model.Part("engines");
            float lowest = float.MaxValue;
            for (int i = 1; i < engines.Positions.Length; i += 3) lowest = System.Math.Min(lowest, engines.Positions[i]);
            Assert.AreEqual(ShipLayout.EngineExitY, lowest, 0.01f);
        }

        // ------------------------------------------------------------------ round shading

        [Test]
        public void RoundShading_JoinsPanelsOnACurve_AndKeepsRealCreases()
        {
            // Two quads with corners of their own, meeting at a 20 degree bend along the Z axis.
            float c = (float)System.Math.Cos(20 * System.Math.PI / 180), s = (float)System.Math.Sin(20 * System.Math.PI / 180);
            float[] positions =
            {
                -1, 0, 0,   0, 0, 0,   0, 0, 1,   -1, 0, 1,
                 0, 0, 0,   c, -s, 0,  c, -s, 1,   0, 0, 1,
            };
            int[] triangles = { 0, 2, 1, 0, 3, 2, 4, 6, 5, 4, 7, 6 };
            float[] round = MeshSmoothing.Normals(positions, triangles, 42f);
            // The two corners at (0,0,0) now face the same way, half way between the two panels.
            Assert.AreEqual(round[3], round[12], 1e-4f);
            Assert.AreEqual(round[4], round[13], 1e-4f);
            Assert.Greater(round[4], 0.9f);
            Assert.Greater(round[3], 0.05f);
            // An outer corner is untouched: straight up.
            Assert.AreEqual(1f, round[1], 1e-4f);

            // With a crease limit below the bend, each panel keeps its own facing.
            float[] crisp = MeshSmoothing.Normals(positions, triangles, 10f);
            Assert.AreEqual(1f, crisp[4], 1e-4f);
            Assert.AreEqual(c, crisp[13], 1e-3f);
        }

        [Test]
        public void RoundShading_OnTheRealHull_FacesOutward_AndIsRounderThanFlatPanels()
        {
            ShipMesh hull = ShipShell.Hull();
            float[] round = MeshSmoothing.Normals(hull.Positions, hull.Triangles, 42f);
            float[] flat = MeshSmoothing.Normals(hull.Positions, hull.Triangles, 0.01f);
            Assert.AreEqual(hull.Positions.Length, round.Length);
            int outward = 0, changed = 0, count = hull.VertexCount;
            for (int i = 0; i < count; i++)
            {
                int o = i * 3;
                float length = round[o] * round[o] + round[o + 1] * round[o + 1] + round[o + 2] * round[o + 2];
                Assert.That(length, Is.InRange(0.98f, 1.02f));
                // Measured from a point low in the middle of the cabin, the dome's skin faces away.
                float away = round[o] * hull.Positions[o] + round[o + 1] * (hull.Positions[o + 1] - 0.3f) + round[o + 2] * hull.Positions[o + 2];
                if (away > 0f) outward++;
                float same = round[o] * flat[o] + round[o + 1] * flat[o + 1] + round[o + 2] * flat[o + 2];
                Assert.Greater(same, 0.7f, "round shading turned a corner more than 45 degrees");
                if (same < 0.99999f) changed++;
            }
            Assert.Greater(outward, count * 0.9f);
            Assert.Greater(changed, count / 4, "round shading changed almost nothing");
        }
    }
}
