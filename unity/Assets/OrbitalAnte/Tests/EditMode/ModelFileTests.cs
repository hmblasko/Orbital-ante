using System;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Look;
using OrbitalAnte.Ship;
using OrbitalAnte.Dismemberment;

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
            Assert.LessOrEqual(model.TriangleCount, 36000, "docs/performance_budget.md caps the lander's fittings at 36,000 triangles");
        }

        [Test]
        public void LaunchPadModelFile_MatchesTheLaunchLayout()
        {
            // The Blender script carries its own copy of these numbers. If LaunchLayout changes, this fails until
            // tools/blender/build_launchpad.py is changed to match and run again.
            ModelFile model = LoadReal(OrbitalAnte.Launch.LaunchLayout.Model);
            AssertSocket(model, "TowerFoot", OrbitalAnte.Launch.LaunchLayout.TowerFoot);
            AssertSocket(model, "WaterTowerFoot", OrbitalAnte.Launch.LaunchLayout.WaterTowerFoot);
            AssertSocket(model, "HutCentre", OrbitalAnte.Launch.LaunchLayout.HutCentre);
            AssertSocket(model, "ArmPivot", OrbitalAnte.Launch.LaunchLayout.ArmPivot);
            for (int i = 0; i < 4; i++) AssertSocket(model, "Mast" + i, OrbitalAnte.Launch.LaunchLayout.Mast(i));
            foreach (string piece in OrbitalAnte.Launch.LaunchLayout.Pieces)
            {
                Assert.IsNotNull(model.Part(piece), "missing piece " + piece);
            }
            // Whatever shines must belong to a piece the game knows, or it would never be drawn.
            foreach (ModelPart part in model.Parts)
            {
                string name = part.Name;
                if (name.EndsWith(OrbitalAnte.Launch.LaunchLayout.GlowSuffix))
                    name = name.Substring(0, name.Length - OrbitalAnte.Launch.LaunchLayout.GlowSuffix.Length);
                CollectionAssert.Contains(OrbitalAnte.Launch.LaunchLayout.Pieces, name, "the game does not know the piece " + part.Name);
            }
            // The arm is modelled with its hinge at its own origin, reaching along +X.
            ModelPart arm = model.Part(OrbitalAnte.Launch.LaunchLayout.Arm);
            float reach = 0f, back = 0f;
            for (int i = 0; i < arm.Positions.Length; i += 3)
            {
                reach = Math.Max(reach, arm.Positions[i]);
                back = Math.Min(back, arm.Positions[i]);
            }
            Assert.AreEqual(OrbitalAnte.Launch.LaunchLayout.ArmLength, reach, 0.6f, "the arm does not reach the ship");
            Assert.Greater(back, -1.2f, "the arm is not hinged at its own origin");
            Assert.LessOrEqual(model.TriangleCount, 36000, "docs/performance_budget.md caps the launch base at 36,000 triangles");
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
        public void ShipModelFile_HasTheCockpitCameras_LookingAtTheShip()
        {
            ModelFile model = LoadReal("Lander");
            foreach (string piece in new[] { "pole0", "pole1", "landingcam", "pole0Glow", "pole1Glow", "landingcamGlow" })
            {
                Assert.IsNotNull(model.Part(piece), "missing piece " + piece);
            }
            Assert.IsTrue(model.TrySocket("PoleCam0", out ModelSocket left), "missing PoleCam0");
            Assert.IsTrue(model.TrySocket("PoleCam1", out ModelSocket right), "missing PoleCam1");
            Assert.IsTrue(model.TrySocket("LandingCam", out ModelSocket belly), "missing LandingCam");
            Assert.Less(left.X, -3f, "the left pole's camera is on the pilot's left, well clear of the hull");
            Assert.Greater(right.X, 3f, "the right pole's camera is on the pilot's right, well clear of the hull");
            Assert.AreEqual(-left.X, right.X, 0.01f, "the two poles mirror each other");
            Assert.Less(belly.Y, ShipLayout.BellyY, "the landing camera's lens is under the belly");
            Assert.Greater(belly.Y, ShipLayout.GroundY + 0.5f, "and well clear of the ground when landed");
        }

        [Test]
        public void CabinFittingsFile_IsTheConsole_WithTheCabinsLightBakedIn()
        {
            // tools/bake copies the console's body out of the Console model and stores it lit by the cabin's lamps.
            ModelFile console = LoadReal(ShipAssembly.FittingsSource), cabin = LoadReal(ShipAssembly.BakedFittingsModel);
            ModelPart plain = console.Part(ShipAssembly.FittingsPiece), lit = cabin.Part(ShipAssembly.FittingsPiece);
            Assert.IsNotNull(plain, "the Console model has no body");
            Assert.IsNotNull(lit, "run tools/bake again: the baked console is missing");
            CollectionAssert.AreEqual(plain.Positions, lit.Positions, "run tools/bake again: the console's shape has changed since its light was baked");
            CollectionAssert.AreEqual(plain.Triangles, lit.Triangles);
            // Lit, not just copied: the light differs from the paint, and the console is not left in the dark.
            float brightness = 0f, change = 0f;
            for (int i = 0; i < lit.Colors.Length; i += 3)
            {
                float v = System.Math.Max(lit.Colors[i], System.Math.Max(lit.Colors[i + 1], lit.Colors[i + 2]));
                brightness += v;
                change += System.Math.Abs(lit.Colors[i] - plain.Colors[i]);
            }
            Assert.Greater(change / lit.VertexCount, 0.02f);
            Assert.Greater(brightness / lit.VertexCount, 0.15f, "the console got next to no light");
        }

        [Test]
        public void BakedLightFile_FitsTheCabin()
        {
            // The same check the PlayMode test TheBakedLightFileFitsTheCabin makes, run here without the engine.
            string folder = System.Environment.GetEnvironmentVariable("OA_MODELS");
            if (string.IsNullOrEmpty(folder)) Assert.Ignore("OA_MODELS is not set");
            string path = Path.Combine(folder, "..", "Ship", "LanderBake.bytes");
            var pieces = ShipAssembly.InteriorPieces();
            Assert.IsNotNull(ShipAssembly.Unpack(File.ReadAllBytes(path), pieces), "the cabin has changed since its light was baked: run tools/bake again");
        }

        [Test]
        public void ConsoleModelFile_FramesTheScreen_StaysLow_AndLeavesTheScreenOpen()
        {
            // The cockpit's desk panel (CockpitConfig's defaults): 1.1 m by 0.33 m, leaned back 24 degrees.
            const float cx = 0f, cy = 0.9f, cz = 1.94f, halfWidth = 0.55f, halfHeight = 0.165f;
            double lean = 24.0 * System.Math.PI / 180.0;
            float c = (float)System.Math.Cos(lean), s = (float)System.Math.Sin(lean);
            ModelFile model = LoadReal(OrbitalAnte.Cockpit.ConsoleGauges.Model);
            Assert.LessOrEqual(model.TriangleCount, 9000, "docs/performance_budget.md caps the console at 9,000 triangles");
            Assert.IsNotNull(model.Part(OrbitalAnte.Cockpit.ConsoleGauges.Body), "missing the console's body");
            Assert.IsTrue(model.TrySocket(OrbitalAnte.Cockpit.ConsoleGauges.DashSocket, out ModelSocket dash), "missing socket Dash");
            Assert.AreEqual(cx, dash.X, 0.001f);
            Assert.AreEqual(cy, dash.Y, 0.001f);
            Assert.AreEqual(cz, dash.Z, 0.001f);

            float left = 0f, right = 0f, top = 0f, bottom = 0f, lowest = float.MaxValue, widest = 0f;
            foreach (ModelPart part in model.Parts)
            {
                for (int v = 0; v < part.VertexCount; v++)
                {
                    float px = part.Positions[v * 3], py = part.Positions[v * 3 + 1], pz = part.Positions[v * 3 + 2];
                    float x = px - cx, y = py - cy, z = pz - cz;
                    float up = y * c + z * s;              // up the panel
                    float towardPilot = y * s - z * c;     // out of the panel's face
                    left = System.Math.Min(left, x); right = System.Math.Max(right, x);
                    if (System.Math.Abs(x) < halfWidth) { top = System.Math.Max(top, up); bottom = System.Math.Min(bottom, up); }
                    lowest = System.Math.Min(lowest, py);
                    widest = System.Math.Max(widest, System.Math.Abs(px));
                    bool overScreen = System.Math.Abs(x) < halfWidth - 0.005f && System.Math.Abs(up) < halfHeight - 0.005f;
                    // The pilot (eye at 1.18) must see the front window over the console: the hood round the screen
                    // may stand to 1.08, and the wings either side stay under 0.84.
                    Assert.LessOrEqual(py, 1.08f, part.Name + " stands too high");
                    if (System.Math.Abs(px) > 0.705f) Assert.LessOrEqual(py, 0.84f, part.Name + ": the wings stay low");
                    if (overScreen) Assert.LessOrEqual(towardPilot, -0.005f, "nothing of the console may stand in front of the screen");
                    Assert.IsTrue(ShipLayout.InsideCabin(new P3(px, System.Math.Max(0.01f, py), pz)), part.Name + " pokes through the cabin wall");
                    // And none of it reaches back over the edge of the desk toward the pilot's knees.
                    Assert.GreaterOrEqual(pz, 1.74f, part.Name + " hangs over the front of the desk");
                }
            }
            Assert.Less(left, -halfWidth - 0.03f, "the bezel frames the left edge");
            Assert.Greater(right, halfWidth + 0.03f, "the bezel frames the right edge");
            Assert.Greater(top, halfHeight + 0.01f, "the brow frames the top edge");
            Assert.Less(bottom, -halfHeight - 0.03f, "the bezel frames the bottom edge");
            Assert.Less(lowest, 0.675f, "the foot reaches down into the desk top");
            Assert.Greater(widest, 1.2f, "the console has a wing either side of the screen");
        }

        [Test]
        public void ConsoleModelFile_HasItsInstruments_WhereTheGameCanMoveThem()
        {
            ModelFile model = LoadReal(OrbitalAnte.Cockpit.ConsoleGauges.Model);
            for (int i = 0; i < OrbitalAnte.Cockpit.ConsoleGauges.LampCount; i++)
            {
                string name = OrbitalAnte.Cockpit.ConsoleGauges.PieceOf((OrbitalAnte.Cockpit.ConsoleLamp)i);
                Assert.IsNotNull(model.Part(name), "missing lamp piece " + name);
            }
            ModelPart fuel = model.Part(OrbitalAnte.Cockpit.ConsoleGauges.FuelPiece), needle = model.Part(OrbitalAnte.Cockpit.ConsoleGauges.NeedlePiece);
            Assert.IsNotNull(fuel);
            Assert.IsNotNull(needle);

            // Fuel: the liquid lies between its two sockets, so squeezing it toward FuelBase empties the tube.
            P3 fuelBase = Socket(model, OrbitalAnte.Cockpit.ConsoleGauges.FuelBase), fuelTop = Socket(model, OrbitalAnte.Cockpit.ConsoleGauges.FuelTop);
            P3 along = fuelTop - fuelBase;
            float length = along.Length;
            Assert.Greater(length, 0.2f, "the fuel tube is long enough to read");
            Assert.Less(fuelBase.X, 0f, "fuel is on the pilot's left");
            for (int v = 0; v < fuel.VertexCount; v++)
            {
                P3 p = new P3(fuel.Positions[v * 3], fuel.Positions[v * 3 + 1], fuel.Positions[v * 3 + 2]) - fuelBase;
                float t = P3.Dot(p, along) / (length * length);
                Assert.That(t, Is.InRange(-0.02f, 1.02f), "the liquid reaches past its sockets");
            }

            // Air: the needle points at empty, and empty, half and full are 120 degrees apart round the pivot.
            P3 pivot = Socket(model, OrbitalAnte.Cockpit.ConsoleGauges.AirDial), outward = (Socket(model, OrbitalAnte.Cockpit.ConsoleGauges.AirDialOut) - pivot).Unit();
            P3 empty = Socket(model, OrbitalAnte.Cockpit.ConsoleGauges.AirEmpty) - pivot, half = Socket(model, OrbitalAnte.Cockpit.ConsoleGauges.AirHalf) - pivot, full = Socket(model, OrbitalAnte.Cockpit.ConsoleGauges.AirFull) - pivot;
            Assert.AreEqual(-0.5f, P3.Dot(empty.Unit(), half.Unit()), 0.01f);
            Assert.AreEqual(-0.5f, P3.Dot(half.Unit(), full.Unit()), 0.01f);
            Assert.AreEqual(0f, P3.Dot(empty.Unit(), outward), 0.01f, "the needle turns in the plane of the dial");
            Assert.Greater(outward.Y, 0.5f, "the dial faces up toward the pilot");
            Assert.Less(outward.Z, 0f, "the dial faces back toward the pilot");
            P3 tip = P3.Zero;
            float far = 0f;
            for (int v = 0; v < needle.VertexCount; v++)
            {
                P3 p = new P3(needle.Positions[v * 3], needle.Positions[v * 3 + 1], needle.Positions[v * 3 + 2]) - pivot;
                if (p.Length > far) { far = p.Length; tip = p; }
            }
            Assert.Greater(P3.Dot(tip.Unit(), empty.Unit()), 0.99f, "the needle is modelled pointing at empty");
        }

        static P3 Socket(ModelFile model, string name)
        {
            Assert.IsTrue(model.TrySocket(name, out ModelSocket s), "missing socket " + name);
            return new P3(s.X, s.Y, s.Z);
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
                // The cockpit's cameras (and their record lamps) are extra hardware with no stand-in shape and no collider.
                if (part.Name.StartsWith("pole") || part.Name.StartsWith("landingcam")) continue;
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

        [Test]
        public void EnemyModelFile_MatchesTheSkeleton([Values("Enemy", "EnemyRunner", "EnemyBruiser", "EnemyMarksman")] string name)
        {
            // The Blender script carries its own copy of the joint positions. If BodySkeleton changes, this fails
            // until tools/blender/build_enemy.py is changed to match and run again (and build_enemy_types.py, which
            // builds the other three on the same joints: every kind is the same skeleton, drawn at its own size).
            ModelFile model = LoadReal(name);
            for (int i = 0; i < BodyLayout.Count; i++)
            {
                var id = (BodyPartId)i;
                Assert.IsNotNull(model.Part(id.ToString()), "missing piece " + id);
                Assert.IsTrue(model.TrySocket(id.ToString(), out ModelSocket s), "missing socket " + id);
                OrbitalAnte.Core.Vec3d origin = BodySkeleton.PartOrigin(id);
                Assert.AreEqual(origin.X, s.X, 0.002, id + " x");
                Assert.AreEqual(origin.Y, s.Y, 0.002, id + " y");
                Assert.AreEqual(origin.Z, s.Z, 0.002, id + " z");
            }
            Assert.IsNotNull(model.Part("Stump"), "missing the wound stump");
            Assert.LessOrEqual(model.TriangleCount, 16000, "docs/performance_budget.md caps one enemy at 16,000 triangles");
        }

        [Test]
        public void EnemyModelFile_EveryPieceSitsOnItsOwnJoint([Values("Enemy", "EnemyRunner", "EnemyBruiser", "EnemyMarksman")] string name)
        {
            // Each piece is measured from its joint, so it must start there and reach the way the part points.
            ModelFile model = LoadReal(name);
            for (int i = 0; i < BodyLayout.Count; i++)
            {
                var id = (BodyPartId)i;
                ModelPart piece = model.Part(id.ToString());
                float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
                for (int v = 0; v < piece.VertexCount; v++)
                {
                    float x = piece.Positions[v * 3], y = piece.Positions[v * 3 + 1], z = piece.Positions[v * 3 + 2];
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                    if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
                }
                Assert.Less(maxX - minX, 0.75f, id + " is too wide");
                Assert.Less(maxY - minY, 0.75f, id + " is too tall");
                Assert.Less(maxZ - minZ, 0.75f, id + " is too deep");
                if (id == BodyPartId.Head || id == BodyPartId.Torso) continue;
                OrbitalAnte.Core.Vec3d d = BodySkeleton.PartDirection(id);
                float along = (float)(d.X * (d.X > 0 ? maxX : minX) + d.Y * (d.Y > 0 ? maxY : minY));
                float behind = (float)(-d.X * (d.X > 0 ? minX : maxX) - d.Y * (d.Y > 0 ? minY : maxY));
                Assert.Greater(along, 0.085f, id + " does not reach away from its joint");
                Assert.Less(behind, 0.13f, id + " sticks out behind its joint");
                Assert.Greater(behind, 0.03f, id + " has no ball over its joint, so a gap would open when it bends");
            }
        }

        [Test]
        public void EnemyModelFile_BootsStandOnTheGround([Values("Enemy", "EnemyRunner", "EnemyBruiser", "EnemyMarksman")] string name)
        {
            ModelFile model = LoadReal(name);
            foreach (BodyPartId id in new[] { BodyPartId.LeftFoot, BodyPartId.RightFoot })
            {
                ModelPart piece = model.Part(id.ToString());
                float lowest = float.MaxValue;
                for (int v = 0; v < piece.VertexCount; v++) lowest = System.Math.Min(lowest, piece.Positions[v * 3 + 1]);
                Assert.AreEqual(-BodySkeleton.AnkleY, lowest, 0.012f, id + " sole against the ground");
            }
        }
    }
}
