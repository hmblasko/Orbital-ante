using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OrbitalAnte.Bases;
using OrbitalAnte.Look;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// The Blender-made bases (Resources/Models/Base*.bytes) against what the game expects of them: the right
    /// pieces and points, inside their floor and their triangle caps, every structure breakable, a room to walk
    /// into with an air station, the middle left open, nowhere to get stuck.
    /// </summary>
    public class BaseModelTests
    {
        static readonly Dictionary<string, ModelFile> Files = new Dictionary<string, ModelFile>();

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
            string fromEnv = Environment.GetEnvironmentVariable("OA_MODELS");
            return !string.IsNullOrEmpty(fromEnv) && Directory.Exists(fromEnv) ? fromEnv : Path.Combine(Directory.GetCurrentDirectory(), "Assets", "OrbitalAnte", "Resources", "Models");
        }

        static ModelFile Load(string id)
        {
            if (Files.TryGetValue(id, out ModelFile cached)) return cached;
            string path = Path.Combine(ModelsFolder(), id + ".bytes");
            Assert.IsTrue(File.Exists(path), "Model file not found: " + path);
            ModelFile model = ModelFile.Parse(File.ReadAllBytes(path), out string problem);
            Assert.IsNotNull(model, problem);
            Files[id] = model;
            return model;
        }

        static IEnumerable<TestCaseData> AllBases()
        {
            foreach (BaseKind kind in BaseKinds.All()) yield return new TestCaseData(kind).SetName("{m}(" + kind.Model + ")");
        }

        static int Count(ModelFile file, string point)
        {
            int n = 0;
            while (file.TrySocket(point + n, out _)) n++;
            return n;
        }

        [Test]
        public void ThereIsMoreThanOneDesign_AndEverySizeHasOne()
        {
            int[] counts = BaseKinds.Counts;
            Assert.AreEqual(3, counts.Length);
            foreach (int c in counts) Assert.GreaterOrEqual(c, 1);
            Assert.GreaterOrEqual(BaseKinds.All().Length, 5);
            var seen = new HashSet<string>();
            foreach (BaseKind kind in BaseKinds.All()) Assert.IsTrue(seen.Add(kind.Model), kind.Model + " is listed twice.");
        }

        [Test]
        public void AnyKindNumber_GivesADesignOfTheRightSize()
        {
            foreach (BaseSize size in Enum.GetValues(typeof(BaseSize)))
                for (int k = -3; k < 9; k++) Assert.AreEqual(size, BaseKinds.Of(size, k).Size);
        }

        static int Tris(ModelFile file, string piece) => file.Part(piece) != null ? file.Part(piece).TriangleCount : 0;

        /// <summary>Every solid piece of a base while everything stands: the ground's and each structure's.</summary>
        static List<ModelPart> Solids(ModelFile file)
        {
            var list = new List<ModelPart>();
            if (file.Part(BaseStructures.GroundHit) != null) list.Add(file.Part(BaseStructures.GroundHit));
            int count = BaseStructures.Count(file);
            for (int i = 0; i < count; i++)
            {
                ModelPart hit = file.Part(BaseStructures.Tag(i) + BaseStructures.Hit);
                if (hit != null) list.Add(hit);
            }
            return list;
        }

        [TestCaseSource(nameof(AllBases))]
        public void HasEveryPiece_AndIsInsideItsCaps(BaseKind kind)
        {
            ModelFile file = Load(kind.Model);
            int count = BaseStructures.Count(file);
            Assert.GreaterOrEqual(count, 3, kind.Model + " should be a few structures, each of which can fall.");
            var known = new HashSet<string> { BaseStructures.Ground, BaseStructures.GroundGlow, BaseStructures.GroundHit };
            int outside = Tris(file, BaseStructures.Ground) + Tris(file, BaseStructures.GroundGlow);
            int inside = 0, solid = Tris(file, BaseStructures.GroundHit), glow = Tris(file, BaseStructures.GroundGlow);
            for (int i = 0; i < count; i++)
            {
                string tag = BaseStructures.Tag(i);
                Assert.IsNotNull(file.Part(tag), kind.Model + " " + tag + " has nothing to draw.");
                Assert.IsNotNull(file.Part(tag + BaseStructures.Hit), kind.Model + " " + tag + " has nothing solid: you would walk through it and shots would never bring it down.");
                foreach (string piece in new[] { "", BaseStructures.Glow, BaseStructures.Hit, BaseStructures.Inside, BaseStructures.Door, BaseStructures.Ruin, BaseStructures.RuinHit }) known.Add(tag + piece);
                int bits = BaseStructures.Bits(file, i);
                for (int k = 0; k < bits; k++) known.Add(tag + BaseStructures.Bit + k);
                outside += Tris(file, tag) + Tris(file, tag + BaseStructures.Glow);
                glow += Tris(file, tag + BaseStructures.Glow);
                inside += Tris(file, tag + BaseStructures.Inside) + Tris(file, tag + BaseStructures.Door);
                solid += Tris(file, tag + BaseStructures.Hit);
            }
            Assert.Greater(glow, 0, kind.Model + ": a base with no lights is a dull one.");
            Assert.LessOrEqual(outside, BaseKinds.TriangleCap(kind.Size), kind.Model + " draws too much from outside.");
            Assert.LessOrEqual(inside, BaseKinds.InsideTriangleCap(kind.Size), kind.Model + "'s rooms are over their cap.");
            Assert.LessOrEqual(solid, BaseKinds.SolidTriangleCap(kind.Size), kind.Model + "'s solid shapes are over their cap.");
            foreach (ModelPart part in file.Parts) Assert.IsTrue(known.Contains(part.Name), kind.Model + " has a piece the game does not know about (" + part.Name + "): it would never be drawn.");
        }

        [TestCaseSource(nameof(AllBases))]
        public void EveryStructureCanFall_IntoPiecesNoHeavierThanItWas_AndLeavesALowerRuin(BaseKind kind)
        {
            ModelFile file = Load(kind.Model);
            int count = BaseStructures.Count(file);
            for (int i = 0; i < count; i++)
            {
                string tag = BaseStructures.Tag(i);
                int bits = BaseStructures.Bits(file, i);
                Assert.GreaterOrEqual(bits, 1, kind.Model + " " + tag + " cannot fall: Matthew asked for every building on a base to be destructible.");
                Assert.IsNotNull(file.Part(tag + BaseStructures.Ruin), kind.Model + " " + tag + " leaves nothing behind when it falls.");
                int whole = Tris(file, tag) + Tris(file, tag + BaseStructures.Glow) + Tris(file, tag + BaseStructures.Inside) + Tris(file, tag + BaseStructures.Door);
                int pieces = 0;
                for (int k = 0; k < bits; k++) pieces += Tris(file, tag + BaseStructures.Bit + k);
                Assert.LessOrEqual(pieces, whole * 2.4 + 400, kind.Model + " " + tag + " breaks into far more than it was made of.");
                ModelPart drawn = file.Part(tag);
                float wholeTop = float.MinValue;
                for (int v = 0; v < drawn.VertexCount; v++) wholeTop = Math.Max(wholeTop, drawn.Positions[v * 3 + 1]);
                ModelPart ruin = file.Part(tag + BaseStructures.Ruin);
                float ruinTop = float.MinValue;
                for (int v = 0; v < ruin.VertexCount; v++) ruinTop = Math.Max(ruinTop, ruin.Positions[v * 3 + 1]);
                Assert.LessOrEqual(ruinTop, Math.Max(wholeTop, 1.3f) + 0.01f, kind.Model + " " + tag + "'s ruin stands taller than it did.");
            }
        }

        [TestCaseSource(nameof(AllBases))]
        public void HasARoomToWalkInto_WithADoorAndAnAirStation(BaseKind kind)
        {
            ModelFile file = Load(kind.Model);
            int count = BaseStructures.Count(file), rooms = 0, stations = 0;
            List<ModelPart> solids = Solids(file);
            for (int i = 0; i < count; i++)
            {
                string tag = BaseStructures.Tag(i);
                if (!BaseStructures.Enterable(file, i)) continue;
                rooms++;
                Assert.IsNotNull(file.Part(tag + BaseStructures.Door), kind.Model + " " + tag + " has a room but no door.");
                Assert.IsTrue(file.TrySocket(tag + BaseStructures.DoorOpen, out ModelSocket open), kind.Model + " " + tag + ": where does its door slide to?");
                file.TrySocket(tag + BaseStructures.DoorShut, out ModelSocket shut);
                double slide = Math.Sqrt((open.X - shut.X) * (open.X - shut.X) + (open.Y - shut.Y) * (open.Y - shut.Y) + (open.Z - shut.Z) * (open.Z - shut.Z));
                Assert.That(slide, Is.InRange(0.9, 1.6), kind.Model + " " + tag + "'s door should slide about its own width.");
                // the doorway itself is open: nothing solid where the door stands
                foreach (ModelPart hit in solids) Assert.IsFalse(Across(hit, shut.X, shut.Y, shut.Z), kind.Model + " " + tag + ": something solid stands in its doorway.");
                if (file.TrySocket(tag + BaseStructures.Air, out ModelSocket air))
                {
                    stations++;
                    file.TrySocket(tag + BaseStructures.Mid, out ModelSocket mid);
                    Assert.Less(Math.Sqrt((air.X - mid.X) * (air.X - mid.X) + (air.Z - mid.Z) * (air.Z - mid.Z)), 8.0, kind.Model + " " + tag + "'s air station is not in its room.");
                    Assert.That(air.Y, Is.InRange(0.5f, 2.2f), kind.Model + " " + tag + "'s air station should be at chest height.");
                }
            }
            Assert.GreaterOrEqual(rooms, 1, kind.Model + " has no building you can go into.");
            Assert.GreaterOrEqual(stations, 1, kind.Model + " has nowhere to fill the suit's air.");
        }

        [TestCaseSource(nameof(AllBases))]
        public void StandsOnItsFloor_AndInsideIt(BaseKind kind)
        {
            ModelFile file = Load(kind.Model);
            float radius = BaseKinds.FloorRadius(kind.Size);
            float highest = 0f;
            foreach (ModelPart part in file.Parts)
            {
                for (int i = 0; i < part.VertexCount; i++)
                {
                    float x = part.Positions[i * 3], y = part.Positions[i * 3 + 1], z = part.Positions[i * 3 + 2];
                    Assert.Less(Math.Sqrt(x * x + z * z), radius - 0.25, kind.Model + " " + part.Name + " hangs off the edge of the floor.");
                    Assert.Greater(y, -0.4f, kind.Model + " " + part.Name + " hangs below the floor.");
                    highest = Math.Max(highest, y);
                }
            }
            Assert.Greater(highest, 5f, "A base should stand out against the sky from a long way off.");
        }

        [TestCaseSource(nameof(AllBases))]
        public void HasThePointsItPromises(BaseKind kind)
        {
            ModelFile file = Load(kind.Model);
            Assert.AreEqual(kind.Spawns, Count(file, BaseKinds.Spawn));
            Assert.AreEqual(kind.Guards, Count(file, BaseKinds.Guard));
            Assert.AreEqual(kind.Loot, Count(file, BaseKinds.Loot));
            Assert.IsTrue(file.TrySocket(BaseKinds.Door, out _), "Every base has a door.");
            Assert.AreEqual(kind.Pad, file.TrySocket(BaseKinds.Pad, out _));
            Assert.GreaterOrEqual(kind.Spawns, 4, "Room for the smallest garrison.");
            for (int i = 0; i < kind.Guards; i++)
            {
                file.TrySocket(BaseKinds.Guard + i, out ModelSocket g);
                Assert.Greater(g.Y, 4f, "A lookout stands up on something.");
            }
        }

        /// <summary>True if a point (seen from above) lies inside any solid triangle that is low enough to walk into.</summary>
        static bool Blocked(ModelPart hit, float px, float pz, float reach)
        {
            for (int t = 0; t < hit.TriangleCount; t++)
            {
                int a = hit.Triangles[t * 3] * 3, b = hit.Triangles[t * 3 + 1] * 3, c = hit.Triangles[t * 3 + 2] * 3;
                float lowest = Math.Min(hit.Positions[a + 1], Math.Min(hit.Positions[b + 1], hit.Positions[c + 1]));
                if (lowest > 2.2f) continue;   // overhead: a belt, a deck on legs
                float ax = hit.Positions[a], az = hit.Positions[a + 2], bx = hit.Positions[b], bz = hit.Positions[b + 2], cx = hit.Positions[c], cz = hit.Positions[c + 2];
                if (px < Math.Min(ax, Math.Min(bx, cx)) - reach || px > Math.Max(ax, Math.Max(bx, cx)) + reach ||
                    pz < Math.Min(az, Math.Min(bz, cz)) - reach || pz > Math.Max(az, Math.Max(bz, cz)) + reach) continue;
                float d1 = (px - bx) * (az - bz) - (ax - bx) * (pz - bz);
                float d2 = (px - cx) * (bz - cz) - (bx - cx) * (pz - cz);
                float d3 = (px - ax) * (cz - az) - (cx - ax) * (pz - az);
                bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                if (!(neg && pos)) return true;
                // close to an edge counts too
                if (Near(px, pz, ax, az, bx, bz) < reach || Near(px, pz, bx, bz, cx, cz) < reach || Near(px, pz, cx, cz, ax, az) < reach) return true;
            }
            return false;
        }

        /// <summary>True if something solid crosses the middle of a doorway (a point at the door's mid-height).</summary>
        static bool Across(ModelPart hit, float px, float py, float pz)
        {
            for (int t = 0; t < hit.TriangleCount; t++)
            {
                int a = hit.Triangles[t * 3] * 3, b = hit.Triangles[t * 3 + 1] * 3, c = hit.Triangles[t * 3 + 2] * 3;
                float low = Math.Min(hit.Positions[a + 1], Math.Min(hit.Positions[b + 1], hit.Positions[c + 1]));
                float high = Math.Max(hit.Positions[a + 1], Math.Max(hit.Positions[b + 1], hit.Positions[c + 1]));
                if (high < py - 0.6f || low > py + 0.6f) continue;   // the floor under it, the lintel over it
                float ax = hit.Positions[a], az = hit.Positions[a + 2], bx = hit.Positions[b], bz = hit.Positions[b + 2], cx = hit.Positions[c], cz = hit.Positions[c + 2];
                float d1 = (px - bx) * (az - bz) - (ax - bx) * (pz - bz);
                float d2 = (px - cx) * (bz - cz) - (bx - cx) * (pz - cz);
                float d3 = (px - ax) * (cz - az) - (cx - ax) * (pz - az);
                bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                if (!(neg && pos)) return true;
                // a wall seen from above is a line: closer than a hand's width to it counts
                if (Near(px, pz, ax, az, bx, bz) < 0.15f || Near(px, pz, bx, bz, cx, cz) < 0.15f || Near(px, pz, cx, cz, ax, az) < 0.15f) return true;
            }
            return false;
        }

        static float Near(float px, float pz, float ax, float az, float bx, float bz)
        {
            float dx = bx - ax, dz = bz - az, len = dx * dx + dz * dz;
            float t = len > 1e-9f ? Math.Min(1f, Math.Max(0f, ((px - ax) * dx + (pz - az) * dz) / len)) : 0f;
            float ex = px - (ax + dx * t), ez = pz - (az + dz * t);
            return (float)Math.Sqrt(ex * ex + ez * ez);
        }

        [TestCaseSource(nameof(AllBases))]
        public void TheMiddleIsOpen_AndNobodySpawnsInsideAWall(BaseKind kind)
        {
            ModelFile file = Load(kind.Model);
            List<ModelPart> solids = Solids(file);
            for (int ring = 0; ring <= 4; ring++)
            {
                float r = kind.Yard * ring / 4f;
                for (int k = 0; k < 24; k++)
                {
                    double a = Math.PI * 2 * k / 24;
                    foreach (ModelPart hit in solids) Assert.IsFalse(Blocked(hit, (float)Math.Cos(a) * r, (float)Math.Sin(a) * r, 0.05f), kind.Model + ": something solid stands in the yard, " + r + " m from the middle (" + hit.Name + " at " + (k * 15) + " degrees).");
                }
            }
            foreach (string point in new[] { BaseKinds.Spawn, BaseKinds.Loot })
            {
                for (int i = 0; file.TrySocket(point + i, out ModelSocket s); i++)
                {
                    Assert.AreEqual(0f, s.Y, 0.01f, point + i + " should be on the floor.");
                    foreach (ModelPart hit in solids) Assert.IsFalse(Blocked(hit, s.X, s.Z, 0.6f), kind.Model + " " + point + i + " is inside or right against something solid (" + hit.Name + ").");
                }
            }
            Assert.IsTrue(file.TrySocket(BaseKinds.Door, out ModelSocket door));
            foreach (ModelPart hit in solids) Assert.IsFalse(Blocked(hit, door.X, door.Z, 0.4f), kind.Model + ": the door point is inside something solid.");
        }

        [TestCaseSource(nameof(AllBases))]
        public void NobodyIsStoodOnTheLandingCircle(BaseKind kind)
        {
            if (!kind.Pad) return;
            ModelFile file = Load(kind.Model);
            Assert.IsTrue(file.TrySocket(BaseKinds.Pad, out ModelSocket pad));
            Assert.Greater(kind.PadRadius, 3f);
            for (int i = 0; file.TrySocket(BaseKinds.Spawn + i, out ModelSocket s); i++)
            {
                double d = Math.Sqrt((s.X - pad.X) * (s.X - pad.X) + (s.Z - pad.Z) * (s.Z - pad.Z));
                Assert.Greater(d, kind.PadRadius + 0.3, kind.Model + " Spawn" + i + " is on the landing circle: the pad invites a landing onto a guard.");
            }
        }

        [Test]
        public void TurningABase_IsTheSameForASeed_AndKeepsDistances()
        {
            var seen = new HashSet<int>();
            for (int seed = 0; seed < 200; seed++)
            {
                int turns = BaseKinds.QuarterTurns(seed);
                Assert.AreEqual(turns, BaseKinds.QuarterTurns(seed));
                Assert.IsTrue(turns >= 0 && turns < 4);
                seen.Add(turns);
                BaseKinds.Turned(turns, 3f, 4f, out float x, out float z);
                Assert.AreEqual(25f, x * x + z * z, 1e-4f);
            }
            Assert.AreEqual(4, seen.Count, "Bases should face every way.");
            BaseKinds.Turned(1, 0f, 1f, out float ex, out float ez);   // a quarter turn clockwise from above: north goes to east
            Assert.AreEqual(1f, ex, 1e-6f);
            Assert.AreEqual(0f, ez, 1e-6f);
        }
    }
}
