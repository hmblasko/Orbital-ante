using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OrbitalAnte.Items;
using OrbitalAnte.Save;
using OrbitalAnte.Ship;
using OrbitalAnte.UI;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// Celestial effects drawn (Ralph loop pass 6, 28 Nov 2026; Items/CelestialLooks.cs, OA_Fx in OA_Model.shader, the
    /// suit plate's beads in SuitHud): every effect is one the model shader draws, by the same sums as the C# the
    /// previews and these tests judge; an effect never lifts a pixel past white, is steady from one second to the next,
    /// meets itself round the back of the hull with no seam, and lights a star no faster than a quarter of a second;
    /// the plate's beads stay pale, slow, steady and off the plate; the Locker reads what is in use, and the words say
    /// where each effect is seen and fit the reveal. Never mistaken for damage, a warning or an aim tell (the rule the
    /// pass was given). Plain C#.
    /// </summary>
    public class CelestialLooksTests
    {
        static readonly List<ItemEffect> Effects = ItemCatalog.CreateEffects();
        static readonly List<ItemSpec> Catalog = ItemCatalog.CreateDefaults();

        static ItemSpec Find(string id) => Catalog.Find(i => i.id == id);

        // ------------------------------------------------------------------ the shader

        static string RepoFile(params string[] path)
        {
            string tail = Path.Combine(path);
            string dir = TestContext.CurrentContext.TestDirectory;
            for (int up = 0; up < 10 && dir != null; up++)
            {
                foreach (string head in new[] { Path.Combine("unity", "Assets", "OrbitalAnte"), Path.Combine("Assets", "OrbitalAnte") })
                {
                    string candidate = Path.Combine(dir, head, tail);
                    if (File.Exists(candidate)) return File.ReadAllText(candidate);
                }
                dir = Path.GetDirectoryName(dir);
            }
            string fromHere = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "OrbitalAnte", tail);
            return File.Exists(fromHere) ? File.ReadAllText(fromHere) : null;
        }

        static string ModelShader() => RepoFile("Resources", "Shaders", "OA_Model.shader");

        [Test]
        public void EveryEffect_HasItsOwnMotion_AndEveryMotion_IsOneTheModelShaderDraws_ByTheSameNumber()
        {
            var seen = new HashSet<CelestialMotion>();
            foreach (ItemEffect effect in Effects)
            {
                CelestialMotion motion = CelestialLooks.MotionOf(effect.id);
                Assert.AreNotEqual(CelestialMotion.None, motion, effect.id);
                Assert.IsTrue(seen.Add(motion), effect.id + ": two effects move the same way");
                Assert.AreEqual(motion, CelestialLooks.MotionFor(effect.id, CelestialPlace.Gun), effect.id + ": on the gun each moves its own way");
                // Nothing rises on the lander: every push and turn of it shows a flame, so a rising flame there would read
                // as a thruster firing or the ship on fire.
                Assert.AreNotEqual(CelestialMotion.Flames, CelestialLooks.MotionFor(effect.id, CelestialPlace.Hull), effect.id + " on the hull");
            }
            string text = ModelShader();
            Assert.IsNotNull(text, "OA_Model.shader is missing");
            foreach (CelestialMotion motion in Enum.GetValues(typeof(CelestialMotion)))
            {
                if (motion == CelestialMotion.None) continue;
                string name = "OA_FX_" + motion.ToString().ToUpperInvariant();
                Match m = Regex.Match(text, Regex.Escape(name) + @"\s+(\d+)");
                Assert.IsTrue(m.Success, "the shader does not list " + name);
                Assert.AreEqual((int)motion, int.Parse(m.Groups[1].Value), name);
            }
            // A branch on the material's own number, never a keyword (no new shader programs, no stall when a Celestial
            // gun first comes into the hand), declared in the one material buffer every variant shares.
            StringAssert.Contains("[branch] if (_FxSet.y > 0.0)", text);
            StringAssert.DoesNotContain("_OA_FX", text);
            int start = text.IndexOf("CBUFFER_START(UnityPerMaterial)", StringComparison.Ordinal);
            int end = text.IndexOf("CBUFFER_END", start, StringComparison.Ordinal);
            string buffer = text.Substring(start, end - start);
            foreach (string field in new[] { "_FxColour", "_FxSecond", "_FxSet", "_FxShape" })
                StringAssert.Contains(field, buffer, field + " must be declared for every variant");
            StringAssert.DoesNotContain("#if", buffer, "nothing in the material buffer may depend on a keyword");
            // Lit, shone on, then the effect, then the haze: the effect is light on the item, and never past white.
            int shine = text.IndexOf("color += OA_ShineIn(", StringComparison.Ordinal);
            int fx = text.IndexOf("color = OA_Fx(", StringComparison.Ordinal);
            int haze = text.IndexOf("color = OA_ApplyHaze(", StringComparison.Ordinal);
            Assert.IsTrue(shine > 0 && fx > shine && haze > fx, "the effect goes on after the shine and before the haze");
            StringAssert.Contains("return max(under, min(under + light * share, 1.0));", text, "never past white (CelestialLooks.Ceiling)");
            Assert.AreEqual(1f, CelestialLooks.Ceiling);
            // On the hull the effect fades with the part's damage shade, and a dark band takes the share its place says.
            StringAssert.Contains("half sound = saturate((_BaseColor.r - 0.5) * 2.0);", text, "CelestialLooks.HullDamageShare");
            Assert.AreEqual(0.5f, CelestialLooks.DamageOff);
            StringAssert.Contains("color * (1.0 - (half)dark * (half)_FxShape.z * share)", text, "CelestialLook.Dark");
            // Round the hull the run's patterns fade sooner nearer the axis (CelestialLooks.RunScale).
            StringAssert.Contains("saturate(length(q.xz) * (6.2831853 / 24.0))", text);
            StringAssert.Contains("if (hullRun > 0.5) face = nOS.y >= 0.7071 ? 1.0 : 3.0;", text, "CelestialLooks.Face on the hull");
            // A pixel the effect cannot show on (trim, a broken panel, too fine from afar) skips the sums and keeps its colour.
            StringAssert.Contains("[branch] if (share <= 0.0) return color;", text);
            // Every motion is a branch of its own, so a pixel pays for one motion, never all six.
            foreach (string limit in new[] { "2.5", "3.5", "4.5", "5.5" })
                StringAssert.Contains("else [branch] if (motion < " + limit + ")", text);
        }

        static readonly Regex Number = new Regex(@"(?<![\w.])\d+(?:\.\d+)?");

        static string WithoutComments(string code) => Regex.Replace(code, @"//[^\n]*", "");

        static SortedSet<double> NumbersIn(string code)
        {
            var found = new SortedSet<double>();
            foreach (Match m in Number.Matches(WithoutComments(code)))
                found.Add(Math.Round(double.Parse(m.Value, CultureInfo.InvariantCulture), 6));
            return found;
        }

        static string Between(string text, string from, string to)
        {
            int a = text.IndexOf(from, StringComparison.Ordinal);
            Assert.GreaterOrEqual(a, 0, "not found: " + from);
            int b = text.IndexOf(to, a + from.Length, StringComparison.Ordinal);
            Assert.Greater(b, a, "not found after " + from + ": " + to);
            return text.Substring(a, b - a);
        }

        /// <summary>
        /// The C# sums (which the previews draw and these tests judge) and the shader's are written twice: every number in
        /// one must be in the other, so a change made on one side only fails here. The shader's own extras are the motion
        /// branches' 2.5, 3.5 and 4.5, pi (Math.PI in C#) and the comet's lap of 6 (CometLap in C#).
        /// </summary>
        [Test]
        public void TheShadersSums_UseTheSameNumbersAsTheCSharpTheTestsJudge()
        {
            string shader = ModelShader();
            string code = RepoFile("Scripts", "Runtime", "Items", "CelestialLooks.cs");
            Assert.IsNotNull(shader);
            Assert.IsNotNull(code);
            SortedSet<double> gpu = NumbersIn(Between(shader, "float OA_FxTurned(", "half3 OA_Fx("));
            foreach (double extra in new[] { 2.5, 3.5, 4.5, 3.14159265, 6.0 }) gpu.Remove(Math.Round(extra, 6));
            SortedSet<double> cpu = NumbersIn(Between(code, "public static float Surface(", "public static float Lift("));
            cpu.UnionWith(NumbersIn(Between(code, "public static float TurnedNoise(", "// The game shader's own hash")));
            var onlyGpu = new SortedSet<double>(gpu);
            onlyGpu.ExceptWith(cpu);
            var onlyCpu = new SortedSet<double>(cpu);
            onlyCpu.ExceptWith(gpu);
            Assert.IsEmpty(onlyGpu, "numbers only the shader has: " + string.Join(", ", onlyGpu));
            Assert.IsEmpty(onlyCpu, "numbers only the C# has: " + string.Join(", ", onlyCpu));
            Assert.Greater(cpu.Count, 40, "the sums were found");
        }

        // ------------------------------------------------------------------ light

        [Test]
        public void AnEffect_NeverLiftsAPixelPastWhite_NorTakesMoreThanTheSwirlsShareAway()
        {
            var rng = new Random(6);
            for (int i = 0; i < 20000; i++)
            {
                float lit = (float)rng.NextDouble() * 2.2f, light = (float)rng.NextDouble() * 1.2f;
                float dark = (float)rng.NextDouble(), share = (float)rng.NextDouble();
                float darkShare = i % 2 == 0 ? CelestialLooks.GunDark : CelestialLooks.HullDark;
                float after = CelestialLooks.Lift(lit, light, dark, share, darkShare);
                Assert.LessOrEqual(after, Math.Max(lit, CelestialLooks.Ceiling) + 1e-6f, "never brighter than white, so nothing it draws blooms");
                Assert.GreaterOrEqual(after, lit * (1f - darkShare) - 1e-6f, "only a swirl's band takes light away, and only so much");
                Assert.GreaterOrEqual(CelestialLooks.Lift(lit, light, 0f, share, darkShare), lit - 1e-6f, "an effect with no dark band only ever adds light");
            }
            foreach (ItemEffect effect in Effects)
            {
                foreach (CelestialPlace place in new[] { CelestialPlace.Gun, CelestialPlace.Hull })
                {
                    CelestialLook look = CelestialLooks.For(effect.id, place).Value;
                    Assert.That(look.Strength, Is.InRange(0.05f, 0.6f), effect.id + ": strong enough to see, never a flood of colour");
                    Assert.LessOrEqual(look.Strength, place == CelestialPlace.Hull ? CelestialLooks.HullStrength : CelestialLooks.GunStrength);
                    Assert.Greater(look.Speed, 0f, effect.id);
                    foreach (float c in look.Colour) Assert.That(c, Is.InRange(0f, 1f), effect.id);
                    foreach (float c in look.Second) Assert.That(c, Is.InRange(0f, 1f), effect.id);
                    Assert.AreEqual(place == CelestialPlace.Hull ? CelestialLooks.HullDark : CelestialLooks.GunDark, look.Dark, effect.id);
                }
            }
            // A darkened patch on the lander is what damage looks like: the hull's dark bands barely darken it.
            Assert.LessOrEqual(CelestialLooks.HullDark, 0.25f);
            Assert.Less(CelestialLooks.HullDark, CelestialLooks.GunDark);
            Assert.IsNull(CelestialLooks.For("NoSuchEffect", CelestialPlace.Gun));
            Assert.IsNull(CelestialLooks.For((string)null, CelestialPlace.Hull));
        }

        /// <summary>The average light over a gun-sized box or round the hull, a quarter second at a time for four minutes.</summary>
        static List<float> AverageLight(CelestialLook fx)
        {
            var means = new List<float>();
            for (float t = 0f; t < 240f; t += 0.25f)
            {
                double sum = 0;
                int n = 0;
                for (int a = 0; a < 24; a++)
                {
                    for (int b = 0; b < 24; b++)
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            float x, y, z, nx, ny, nz;
                            if (!fx.RoundUpright)
                            {
                                // A gun 10 cm wide, 22 cm deep and 70 cm long: both sides and the top.
                                x = c == 0 ? 0.05f : c == 1 ? -0.05f : (a / 23f - 0.5f) * 0.1f;
                                y = c == 2 ? 0.11f : (b / 23f - 0.5f) * 0.22f;
                                z = (c == 2 ? b : a) / 23f * 0.7f - 0.1f;
                                nx = c == 0 ? 1f : c == 1 ? -1f : 0f;
                                ny = c == 2 ? 1f : 0f;
                                nz = 0f;
                            }
                            else
                            {
                                // The cabin's side, 1.6 m out and 2.4 m high, all the way round.
                                double turn = (a + c / 3.0) / 24.0 * Math.PI * 2.0;
                                x = (float)(Math.Sin(turn) * 1.6);
                                z = (float)(Math.Cos(turn) * 1.6);
                                y = b / 23f * 2.4f;
                                nx = (float)Math.Sin(turn);
                                ny = 0f;
                                nz = (float)Math.Cos(turn);
                            }
                            float qx = x / fx.Size, qy = y / fx.Size, qz = z / fx.Size;
                            float along = CelestialLooks.Along(qx, qz, fx.RoundUpright);
                            int face = CelestialLooks.Face(nx, ny, nz, fx.RoundUpright);
                            sum += CelestialLooks.Surface(fx.Motion, qx, qy, qz, along, face, fx.RoundUpright, t, fx.Speed, out float w, out float dark);
                            n++;
                        }
                    }
                }
                means.Add((float)(sum / n));
            }
            return means;
        }

        /// <summary>
        /// No effect swells and shrinks as a whole, which would read as something charging or a warning: the light on the
        /// whole gun or hull, taken second by second, changes typically by under a fifth of its average and never by half.
        /// Flames flicker most (a tongue is born and dies in about a second). Starfall is points of light that come and go
        /// one by one; its steadiness is the next test's.
        /// </summary>
        [Test]
        public void EveryEffect_IsSteady_TheItemsLightBarelyChangesFromOneSecondToTheNext()
        {
            foreach (ItemEffect effect in Effects)
            {
                if (CelestialLooks.MotionOf(effect.id) == CelestialMotion.Glints) continue;
                foreach (CelestialPlace place in new[] { CelestialPlace.Gun, CelestialPlace.Hull })
                {
                    List<float> means = AverageLight(CelestialLooks.For(effect.id, place).Value);
                    double total = 0;
                    foreach (float m in means) total += m;
                    double mean = total / means.Count;
                    Assert.Greater(mean, 0.01, effect.id + " " + place + ": an effect always shows something");
                    double worst = 0, squares = 0;
                    int steps = 0;
                    for (int k = 0; k + 8 <= means.Count; k += 4)
                    {
                        double a = 0, b = 0;
                        for (int j = 0; j < 4; j++)
                        {
                            a += means[k + j];
                            b += means[k + 4 + j];
                        }
                        double change = Math.Abs(a - b) / 4.0 / mean;
                        worst = Math.Max(worst, change);
                        squares += change * change;
                        steps++;
                    }
                    Assert.Less(Math.Sqrt(squares / steps), 0.2, effect.id + " " + place + ": typical change from one second to the next");
                    Assert.Less(worst, 0.5, effect.id + " " + place + ": the most it ever changes from one second to the next");
                }
            }
        }

        /// <summary>
        /// The one effect that lights in place: each star lights on its own beat and takes at least 0.48 s to come up (a
        /// smoothstep over d seconds rises at most 1.5 / d a second), so it is never a blink; and the stars of one item
        /// first light at moments spread over more than a second and a half, never together.
        /// </summary>
        [Test]
        public void AStar_NeverBlinks_AndTheStarsOfAnItemAreNeverInStep()
        {
            CelestialLook fx = CelestialLooks.For("Starfall", CelestialPlace.Gun).Value;
            Assert.AreEqual(CelestialMotion.Glints, fx.Motion);
            const float Step = 0.005f, ComesUpIn = 0.48f;
            int stars = 0;
            var firstLit = new List<float>();
            for (int cell = 0; cell < 40; cell++)
            {
                // One cell of a side face (face 0: across is z, up is y). The star's middle across is fixed; as it fades it
                // slides down, so each moment reads the brightest point of its column.
                float cu = cell, cv = 1f, ck = 3f;
                if (CelestialLooks.Hash(cu, cv, ck) < 0.5f) continue;
                stars++;
                float su = 0.25f + 0.5f * CelestialLooks.Hash(cu, cv, ck + 4.4f);
                float sv = 0.62f + 0.13f * CelestialLooks.Hash(cu + 5.5f, cv, ck);
                float last = 0f, fastest = 0f, peak = 0f, lit = -1f;
                for (int f = 0; f * Step < 12f; f++)
                {
                    float t = f * Step, best = 0f;
                    for (int i = 0; i <= 40; i++)
                    {
                        float qy = cv + sv - 0.355f + i * (0.36f / 40f);
                        float v = CelestialLooks.Surface(CelestialMotion.Glints, ck, qy, cu + su, -(cu + su), 0, false, t, fx.Speed, out float w, out float dark);
                        if (v > best) best = v;
                    }
                    if (f > 0) fastest = Math.Max(fastest, Math.Abs(best - last) / Step);
                    if (lit < 0f && best > 0.9f) lit = t;
                    peak = Math.Max(peak, best);
                    last = best;
                }
                Assert.Greater(peak, 0.9f, "the star in cell " + cell + " lights");
                Assert.LessOrEqual(fastest, 1.5f / ComesUpIn * 1.02f, "the star in cell " + cell + " comes up over at least " + ComesUpIn + " s");
                firstLit.Add(lit);
            }
            Assert.Greater(stars, 10, "about half the cells hold a star");
            firstLit.Sort();
            Assert.Greater(firstLit[firstLit.Count - 1] - firstLit[0], 1.5f, "the stars light on beats of their own");
        }

        [Test]
        public void TheHullsPatterns_MeetThemselvesRoundTheBack_WithNoSeam()
        {
            foreach (ItemEffect effect in Effects)
            {
                CelestialLook fx = CelestialLooks.For(effect.id, CelestialPlace.Hull).Value;
                float r = 1.6f / fx.Size;
                // Either side of straight behind the cabin the run round jumps from minus half a turn to half a turn; the
                // point itself is the same, so only the run may differ, and every pattern must not care. Starfall's cells
                // are a whole number round, so its seam is one more edge between two cells, where at most the far tail of a
                // star's spike (under 2%) is cut, as at every cell's edge.
                float a = CelestialLooks.Along(-0.0001f, -r, true), b = CelestialLooks.Along(0.0001f, -r, true);
                Assert.AreEqual(CelestialLooks.HullUnitsRound, Math.Abs(a - b), 0.01f, "the run really does jump here");
                int face = CelestialLooks.Face(0f, 0f, -1f, true);
                Assert.AreEqual(3, face, "round the side");
                float within = fx.Motion == CelestialMotion.Glints ? 0.02f : 0.002f;
                for (float y = 0.1f; y < 4f; y += 0.37f)
                {
                    for (float t = 0f; t < 20f; t += 1.3f)
                    {
                        float va = CelestialLooks.Surface(fx.Motion, 0f, y, -r, a, face, true, t, fx.Speed, out float wa, out float da);
                        float vb = CelestialLooks.Surface(fx.Motion, 0f, y, -r, b, face, true, t, fx.Speed, out float wb, out float db);
                        Assert.AreEqual(va, vb, within, effect.id + " at height " + y + ", time " + t);
                        if (fx.Motion != CelestialMotion.Glints) Assert.AreEqual(wa, wb, 0.002f, effect.id + " (second colour)");
                        Assert.AreEqual(da, db, 0.002f, effect.id + " (dark band)");
                    }
                }
            }
        }

        [Test]
        public void OnTheGun_ACometRunsFromTheMuzzleBackToTheHand_NeverTowardTheMuzzleLikeAShotCharging()
        {
            CelestialLook fx = CelestialLooks.For("CometTail", CelestialPlace.Gun).Value;
            // The gun's +Z is its muzzle. Find a lit lane, then follow its brightest point for a moment.
            for (float qy = 0.3f; qy < 6f; qy += 0.71f)
            {
                float lane = (float)Math.Floor(qy * 1.4f);
                if (CelestialLooks.Hash(lane, 1.7f, 3.3f) < 0.35f) continue;
                float y = (lane + 0.5f) / 1.4f;
                float first = Brightest(fx, y, 1.0f), later = Brightest(fx, y, 1.1f);
                float moved = later - first;
                // Heads are a lap of 6 units apart: a step of a tenth of a second moves one by well under a lap.
                if (Math.Abs(moved) > 3f) continue;
                Assert.Less(moved, 0f, "the head moves toward -Z, away from the muzzle");
                return;
            }
            Assert.Fail("no lane with comets was found");
        }

        static float Brightest(CelestialLook fx, float qy, float t)
        {
            float best = -1f, at = 0f;
            for (float qz = 0f; qz < 12f; qz += 0.01f)
            {
                float v = CelestialLooks.Surface(fx.Motion, 0f, qy, qz, CelestialLooks.Along(0f, qz, false), 0, false, t, fx.Speed, out float w, out float d);
                if (v > best) { best = v; at = qz; }
            }
            return at;
        }

        // ------------------------------------------------------------------ the suit plate's beads

        /// <summary>HullKit's colours that mean something on the readout (orange to act, green for met, blue), read from its code.</summary>
        static List<float[]> ReadoutColours()
        {
            string kit = RepoFile("Scripts", "Runtime", "UI", "HullKit.cs");
            Assert.IsNotNull(kit, "HullKit.cs is missing");
            var colours = new List<float[]>();
            foreach (string name in new[] { "Orange", "Good", "Cool" })
            {
                Match m = Regex.Match(kit, @"Color\s+" + name + @"\s*=\s*new Color\(([\d.]+)f?,\s*([\d.]+)f?,\s*([\d.]+)f?\)");
                Assert.IsTrue(m.Success, "HullKit." + name + " not found");
                colours.Add(new[]
                {
                    float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                    float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                    float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
                });
            }
            return colours;
        }

        [Test]
        public void ThePlatesBeads_ArePalerThanEveryColourThatMeansSomethingOnTheReadout()
        {
            foreach (float[] c in ReadoutColours())
                Assert.Greater(CelestialPlate.Saturation(c), CelestialPlate.MostSaturation, "a readout colour is stronger than any bead may be");
            foreach (ItemEffect effect in Effects)
            {
                CelestialPlate.Look look = CelestialPlate.For(effect.id).Value;
                Assert.LessOrEqual(CelestialPlate.Saturation(look.Colour), CelestialPlate.MostSaturation + 1e-5f, effect.id);
                Assert.LessOrEqual(CelestialPlate.Saturation(look.Second), CelestialPlate.MostSaturation + 1e-5f, effect.id);
            }
            // Even a deep colour an item asset is given.
            foreach (float[] deep in new[] { new[] { 1f, 0f, 0f }, new[] { 0f, 1f, 0f }, new[] { 0f, 0.2f, 1f }, new[] { 1f, 0.45f, 0f } })
                Assert.LessOrEqual(CelestialPlate.Saturation(CelestialPlate.Pale(deep)), CelestialPlate.MostSaturation + 1e-5f);
            Assert.IsNull(CelestialPlate.For("NoSuchEffect"));
        }

        [Test]
        public void ThePlatesBeads_SitJustOutsideThePlate_ClearOfItsEdgeAndCorners()
        {
            const float w = 400f, h = 122f;
            List<CelestialPlate.Bead> beads = CelestialPlate.Path(w, h);
            Assert.Greater(beads.Count, 40);
            float last = -1f;
            foreach (CelestialPlate.Bead b in beads)
            {
                float halfX = (b.Upright ? CelestialPlate.BeadWidth : CelestialPlate.BeadLength) * 0.5f;
                float halfY = (b.Upright ? CelestialPlate.BeadLength : CelestialPlate.BeadWidth) * 0.5f;
                bool overlaps = b.X + halfX > 0f && b.X - halfX < w && b.Y + halfY > 0f && b.Y - halfY < h;
                if (overlaps)
                {
                    // It may run along the plate's side (it is longer than its gap to the corner), but never over the plate.
                    bool clearAcross = b.Upright ? (b.X + halfX <= -1f || b.X - halfX >= w + 1f) : (b.Y + halfY <= -1f || b.Y - halfY >= h + 1f);
                    Assert.IsTrue(clearAcross, "a bead lies over the plate at " + b.X + ", " + b.Y);
                }
                float outside = Math.Max(Math.Max(-b.X, b.X - w), Math.Max(-b.Y, b.Y - h));
                Assert.AreEqual(CelestialPlate.Outside, outside, 0.01f, "every bead sits the same distance outside the edge");
                Assert.Greater(b.Along, last, "clockwise, in order");
                last = b.Along;
            }
            Assert.Greater(CelestialPlate.Outside - CelestialPlate.BeadWidth * 0.5f, 0.99f, "a pixel clear of the plate's own edge");
        }

        /// <summary>
        /// Slow and steady: no bead goes from dark to full in under a quarter of a second, none is brighter than
        /// <see cref="CelestialPlate.Most"/>, and the plate's overall light, second by second, changes by less than half.
        /// </summary>
        [Test]
        public void ThePlatesBeads_AreSlowAndSteady()
        {
            List<CelestialPlate.Bead> beads = CelestialPlate.Path(400f, 122f);
            const float round = 2f * (400f + 122f), dt = 0.01f;
            foreach (ItemEffect effect in Effects)
            {
                CelestialMotion motion = CelestialLooks.MotionOf(effect.id);
                var last = new float[beads.Count];
                var seconds = new List<double>();
                double second = 0, total = 0;
                int frames = 0;
                for (int f = 0; f <= 12000; f++)
                {
                    float t = f * dt;
                    double sum = 0;
                    for (int i = 0; i < beads.Count; i++)
                    {
                        float v = CelestialPlate.Light(motion, beads[i].Along, round, t, out float w);
                        Assert.That(v, Is.InRange(0f, CelestialPlate.Most), effect.id);
                        Assert.That(w, Is.InRange(-1e-5f, 1f + 1e-5f), effect.id);
                        if (f > 0) Assert.LessOrEqual(Math.Abs(v - last[i]) / dt, CelestialPlate.MostChange * CelestialPlate.Most + 1e-3f, effect.id + " bead " + i + " at " + t);
                        last[i] = v;
                        sum += v;
                    }
                    second += sum / beads.Count;
                    total += sum / beads.Count;
                    if (++frames % 100 == 0)
                    {
                        seconds.Add(second / 100.0);
                        second = 0;
                    }
                }
                double mean = total / frames;
                Assert.Greater(mean, 0.05, effect.id + ": the beads show");
                for (int k = 1; k < seconds.Count; k++)
                    Assert.Less(Math.Abs(seconds[k] - seconds[k - 1]) / mean, 0.5, effect.id + ": the plate's light from second " + (k - 1) + " to " + k);
            }
        }

        [Test]
        public void SeveralEffects_TakeTurnsRoundThePlate_TheNextEachTimeThePlateComesUp()
        {
            Assert.AreEqual(0, CelestialPlate.TurnFor(1, 7), "one effect is shown alone, always");
            Assert.AreEqual(0, CelestialPlate.TurnFor(0, 3));
            Assert.AreEqual(0, CelestialPlate.TurnFor(3, 0), "the first time out, the first");
            Assert.AreEqual(1, CelestialPlate.TurnFor(3, 1), "the next time, the next");
            Assert.AreEqual(2, CelestialPlate.TurnFor(3, 2));
            Assert.AreEqual(0, CelestialPlate.TurnFor(3, 3), "and round again");
            Assert.AreEqual(2, CelestialPlate.TurnFor(3, -1), "never out of range");
            // The turn changes only as the plate comes up (stepping outside), never while it shows.
            string hud = Runtime("Spacewalk", "SuitHud.cs");
            Assert.IsNotNull(hud);
            StringAssert.Contains("if (show && !_plateUp) { _timesUp++; _plateUpAt = now; }", hud);
            StringAssert.Contains("CelestialPlate.TurnFor(_beadLooks.Length, _timesUp)", hud);
        }

        /// <summary>
        /// As the plate comes up the beads show off at full strength, then settle slowly to under half of it and stay
        /// there: a border at full strength all the time pulls at the corner of the eye. Settling never moves a bead
        /// faster than a quarter of what the plate's own rule allows.
        /// </summary>
        [Test]
        public void ThePlatesBeads_ShowOffAsThePlateComesUp_ThenIdleAtUnderHalf()
        {
            Assert.AreEqual(1f, CelestialPlate.Strength(0f));
            Assert.AreEqual(1f, CelestialPlate.Strength(CelestialPlate.FlourishSeconds));
            Assert.AreEqual(CelestialPlate.Idle, CelestialPlate.Strength(CelestialPlate.FlourishSeconds + CelestialPlate.SettleSeconds), 1e-5f);
            Assert.AreEqual(CelestialPlate.Idle, CelestialPlate.Strength(600f), 1e-5f, "and stays there");
            Assert.AreEqual(1f, CelestialPlate.Strength(float.NaN));
            Assert.LessOrEqual(CelestialPlate.Idle, 0.5f, "under half");
            Assert.GreaterOrEqual(CelestialPlate.Idle, 0.3f, "still seen");
            Assert.GreaterOrEqual(CelestialPlate.FlourishSeconds, 2f, "long enough to be seen");
            float last = 1f;
            for (float t = 0f; t < 10f; t += 0.01f)
            {
                float now = CelestialPlate.Strength(t);
                Assert.LessOrEqual(Math.Abs(now - last) / 0.01f * CelestialPlate.Most, CelestialPlate.MostChange * CelestialPlate.Most * 0.25f, "at " + t);
                last = now;
            }
        }

        [Test]
        public void ThePlatesBeads_GoDarkBesideAWarning_AndInAFight_AndComeBackSlowly()
        {
            Assert.IsFalse(CelestialPlate.Hushed(false, false, false), "calm: the beads show");
            Assert.IsTrue(CelestialPlate.Hushed(true, false, false), "an alarm on the notice beside the plate");
            Assert.IsTrue(CelestialPlate.Hushed(false, true, false), "the plate itself blinking: air, jet fuel, a hit");
            Assert.IsTrue(CelestialPlate.Hushed(false, false, true), "a fight");
            float shown = 1f;
            for (int i = 0; i < 18; i++) shown = CelestialPlate.Shown(shown, true, 1f / 60f);
            Assert.AreEqual(0f, shown, 1e-4f, "dark within 0.3 s");
            shown = CelestialPlate.Shown(shown, false, 0.25f);
            Assert.Less(shown, 0.2f, "back slowly: never lit from dark in a quarter of a second");
            for (int i = 0; i < 90; i++) shown = CelestialPlate.Shown(shown, false, 1f / 60f);
            Assert.AreEqual(1f, shown, 1e-4f, "all back within 1.5 s");
            Assert.AreEqual(1f, CelestialPlate.Shown(1f, false, -5f), "a step back in time changes nothing");
            Assert.AreEqual(0.5f, CelestialPlate.Shown(0.5f, true, float.NaN));
            // What the suit's readout hands it: the notice's alarm, the plate's own blinks and the fight the top-right
            // readout keeps to, which since Ralph loop pass 8 holds a creature's warning and a creature after the player too.
            string hud = Runtime("Spacewalk", "SuitHud.cs");
            Assert.IsNotNull(hud);
            StringAssert.Contains("CelestialPlate.Hushed(alarm, plateBlinks, fight)", hud);
            StringAssert.Contains("bool fight = Tasks.AnteLoop.InFight;", hud);
            StringAssert.Contains("VisorReadout.AirTone(s.AirLow, s.AirOut, s.BurningAir) == GaugeTone.Alarm", hud);
        }

        static float ConstIn(string code, string name)
        {
            Match m = Regex.Match(code, Regex.Escape(name) + @"\s*=\s*([\d.]+)f");
            Assert.IsTrue(m.Success, name + " not found");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// On the Deck's 1280 canvas (the narrowest it is) the suit's notice strip at its widest sits in the middle of the gap
        /// between the lower plates, a pixel clear of the suit plate's beads and as far from the gun plate. The plates' sizes
        /// are read from their code.
        /// </summary>
        [Test]
        public void TheWidestNotice_SitsBetweenTheLowerPlates_ClearOfTheBeads()
        {
            string suitHud = Runtime("Spacewalk", "SuitHud.cs"), gunHud = Runtime("Combat", "GunLadderFx.cs"), kit = Runtime("UI", "HullKit.cs");
            Assert.IsNotNull(suitHud);
            Assert.IsNotNull(gunHud);
            Assert.IsNotNull(kit);
            Match suit = Regex.Match(suitHud, @"""SuitPlate"",[^;]*new Vector2\(([\d.]+)f,\s*([\d.]+)f\)\);");
            Assert.IsTrue(suit.Success, "the suit plate's size was found");
            float suitWidth = float.Parse(suit.Groups[1].Value, CultureInfo.InvariantCulture);
            float gunWidth = ConstIn(gunHud, "PlateWidth"), margin = ConstIn(kit, "public const float Margin");
            const float Canvas = 1280f;
            float suitRight = margin + suitWidth, gunLeft = Canvas - margin - gunWidth;
            float middle = Canvas * 0.5f + HullReadout.NoticeShift;
            float left = middle - HullReadout.NoticeMost * 0.5f, right = middle + HullReadout.NoticeMost * 0.5f;
            float beadsReach = CelestialPlate.Outside + CelestialPlate.BeadWidth * 0.5f;
            Assert.GreaterOrEqual(left - suitRight, beadsReach + 1f, "clear of the suit plate and its beads");
            Assert.GreaterOrEqual(gunLeft - right, beadsReach + 1f, "as clear of the gun plate");
            Assert.AreEqual(left - suitRight, gunLeft - right, 0.5f, "in the middle of the gap");
        }

        // ------------------------------------------------------------------ the Locker and the words

        static SaveData OwningWith(params string[] itemAndEffect)
        {
            var save = new SaveData();
            var owned = new ItemInventory(save.items);
            int serial = 1;
            for (int i = 0; i + 1 < itemAndEffect.Length; i += 2)
                owned.Add(new Prize { itemId = itemAndEffect[i], effectId = itemAndEffect[i + 1] }, serial++, false);
            return save;
        }

        static ItemSpec OfSlot(LockerSlot slot) => Catalog.Find(i => Locker.SlotOf(i) == slot);

        [Test]
        public void TheEffectsInUse_AreOnlyThoseOnItemsInUse_EachOnce_AndOnlyOnesTheGameDraws()
        {
            ItemSpec helmet = OfSlot(LockerSlot.Helmet), badge = OfSlot(LockerSlot.Badge), paint = OfSlot(LockerSlot.Paint), visor = OfSlot(LockerSlot.Visor);
            SaveData save = OwningWith(helmet.id, "Aurora", badge.id, "Aurora", paint.id, "SolarFlare", visor.id, "NoSuchEffect");
            LockerSlot[] suit = { LockerSlot.Helmet, LockerSlot.Visor, LockerSlot.Suit, LockerSlot.Pack, LockerSlot.Badge };
            Assert.IsEmpty(Locker.EffectsInUse(save, Find, suit), "owned is not in use");
            Assert.IsTrue(Locker.Use(save, helmet, "Aurora"));
            Assert.IsTrue(Locker.Use(save, badge, "Aurora"));
            Assert.IsTrue(Locker.Use(save, visor, "NoSuchEffect"));
            CollectionAssert.AreEqual(new[] { "Aurora" }, Locker.EffectsInUse(save, Find, suit), "the same effect twice shows once; an unknown one not at all");
            Assert.IsEmpty(Locker.EffectsInUse(save, Find, LockerSlot.Paint));
            Assert.IsTrue(Locker.Use(save, paint, "SolarFlare"));
            CollectionAssert.AreEqual(new[] { "SolarFlare" }, Locker.EffectsInUse(save, Find, LockerSlot.Paint));
            Assert.IsEmpty(Locker.EffectsInUse(save, id => null, LockerSlot.Paint), "an item whose asset has gone shows nothing");
            Locker.TakeOff(save, helmet, "Aurora");
            CollectionAssert.AreEqual(new[] { "Aurora" }, Locker.EffectsInUse(save, Find, suit), "the badge's is still there");
        }

        [Test]
        public void ASkinsEffect_IsPartOfItsLook_SoAGunWearsItInTheShader()
        {
            ItemSpec skin = Find("SkinRifleStarChart");
            SkinLook plain = PrizeLooks.Skin(skin), celestial = PrizeLooks.Skin(skin, "CometTail"), unknown = PrizeLooks.Skin(skin, "NoSuchEffect");
            Assert.IsFalse(plain.Effect.HasValue);
            Assert.IsTrue(celestial.Effect.HasValue);
            Assert.AreEqual(CelestialMotion.Comet, celestial.Effect.Value.Motion);
            Assert.IsFalse(celestial.Effect.Value.RoundUpright, "on a gun the run is the gun's length");
            Assert.AreEqual(plain.Pattern, celestial.Pattern, "the skin itself is the same");
            Assert.AreNotEqual(plain.Key, celestial.Key, "with and without its effect are two materials");
            Assert.AreNotEqual(celestial.Key, PrizeLooks.Skin(skin, "Aurora").Key);
            Assert.AreEqual(plain.Key, unknown.Key, "an effect the game does not know is no effect");
            Assert.IsTrue(CelestialLooks.For("Aurora", CelestialPlace.Hull).Value.RoundUpright);
        }

        [Test]
        public void TheWords_SayWhereEachEffectIsSeen_AndFitTheReveal()
        {
            foreach (ItemSpec skin in Catalog.FindAll(i => Locker.SlotOf(i) == LockerSlot.Skin))
                Assert.AreEqual(string.IsNullOrEmpty(skin.weaponId) ? "on every gun" : "on that gun", Locker.EffectWhere(skin), skin.id + ": one gun or all of them (the line above names the gun)");
            Assert.AreEqual("on your lander", Locker.EffectWhere(OfSlot(LockerSlot.Paint)));
            foreach (LockerSlot slot in new[] { LockerSlot.Helmet, LockerSlot.Visor, LockerSlot.Suit, LockerSlot.Pack, LockerSlot.Badge })
                Assert.AreEqual("round your suit's readout", Locker.EffectWhere(OfSlot(slot)), slot.ToString());
            Assert.AreEqual("", Locker.EffectWhere(OfSlot(LockerSlot.Upgrade)), "an upgrade never carries one");
            Assert.AreEqual("Effect: Aurora (on every gun)", Locker.EffectWords(Catalog.Find(i => Locker.SlotOf(i) == LockerSlot.Skin && string.IsNullOrEmpty(i.weaponId)), "Aurora"));
            Assert.AreEqual("", Locker.EffectWords(OfSlot(LockerSlot.Skin), ""));
            // The reveal's line is one line 540 pixels wide at 22 points (B612 bold measures the longest, 49 letters, at
            // about 520); a line over EffectLineLong letters drops to 20, so even the longest has room to spare.
            int longest = 0;
            foreach (ItemEffect effect in Effects)
            {
                foreach (ItemSpec item in Catalog)
                {
                    if (!item.CanCarryEffect) continue;
                    string line = Locker.EffectWords(item, effect.displayName);
                    Assert.LessOrEqual(line.Length, 50, line);
                    longest = Math.Max(longest, line.Length);
                    StringAssert.DoesNotContain("not shown", line);
                    StringAssert.EndsWith(")", line, item.id + ": every item that can carry an effect says where it is seen");
                }
            }
            Assert.Greater(longest, EnvelopeScene.EffectLineLong, "the longest lines are drawn a size smaller");
            StringAssert.Contains("line.Length > EffectLineLong ? 20 : 22", Runtime("Items", "EnvelopeScene.cs"));
        }

        /// <summary>
        /// The Collection's test row (editor and development builds): each press puts the next effect on an every-gun skin,
        /// the lander's paint and a suit piece, all in use, so all six can be checked in a run; clearing the tests takes
        /// every one of them away and out of use, and never touches what the player really owns.
        /// </summary>
        [Test]
        public void TryingTheNextEffect_BringsEachOfTheSixInTurn_OnTheGunTheLanderAndTheSuit_AsTestItems()
        {
            var save = new SaveData();
            ItemSpec realPaint = OfSlot(LockerSlot.Paint);
            new ItemInventory(save.items).Add(new Prize { itemId = realPaint.id, effectId = "Aurora" }, 7, false);
            LockerSlot[] suit = { LockerSlot.Helmet, LockerSlot.Visor, LockerSlot.Suit, LockerSlot.Pack, LockerSlot.Badge };
            for (int press = 0; press < Effects.Count + 1; press++)
            {
                string id = Locker.TryNextEffect(save, Catalog, Effects);
                Assert.AreEqual(Effects[press % Effects.Count].id, id, "press " + press + " brings the next, then round again");
                Assert.AreEqual(id, Locker.Worn(save, LockerSlot.Paint).effectId, "on the lander");
                LockerSkin skin = Locker.SkinFor(save, "Rifle");
                Assert.IsNotNull(skin);
                Assert.AreEqual("", skin.gunId, "an every-gun skin");
                Assert.AreEqual(id, skin.effectId, "on the gun");
                CollectionAssert.AreEqual(new[] { id }, Locker.EffectsInUse(save, Find, suit), "round the suit's readout");
                Assert.AreEqual(LockerSlot.Badge, Locker.SlotOf(Find(Locker.Worn(save, LockerSlot.Badge).itemId)), "on the badge, which shows on the plate");
            }
            OwnedItem real = new ItemInventory(save.items).Find(realPaint.id, "Aurora");
            Assert.AreEqual(1, real.count, "a line the player really owns is used as it is, never counted up");
            Assert.IsFalse(real.fromTest);

            new EnvelopeBook(save).ClearTests();
            Locker.Tidy(save, Find);
            foreach (OwnedItem line in save.items) Assert.IsFalse(line.fromTest, line.itemId + " " + line.effectId);
            Assert.AreEqual(1, save.items.Count, "only the real line is left");
            Assert.IsNull(Locker.SkinFor(save, "Rifle"), "the test skin is out of use");
            Assert.IsEmpty(Locker.EffectsInUse(save, Find, suit));
            // A test tool: the places the tests took are left empty, not given back what was there before.
            Assert.IsNull(Locker.Worn(save, LockerSlot.Paint));
            Assert.AreEqual("", Locker.TryNextEffect(save, new List<ItemSpec>(), Effects), "nothing to put it on");
        }

        // ------------------------------------------------------------------ the hull and the enemies' guns

        [Test]
        public void TheHullsPaintedCorners_TakeTheEffect_AndItsDarkTrimNever()
        {
            foreach (ItemSpec paintItem in Catalog.FindAll(i => Locker.SlotOf(i) == LockerSlot.Paint))
            {
                foreach (HullPaint paint in new[] { null, PrizeLooks.Paint(paintItem) })
                {
                    ShipMesh hull = ShipShell.Hull(paint);
                    float[] painted = ShipShell.PaintedCorners(hull);
                    Assert.AreEqual(hull.VertexCount, painted.Length);
                    int trim = 0, paintedCount = 0;
                    for (int i = 0; i < painted.Length; i++)
                    {
                        if (painted[i] > 0.5f) paintedCount++;
                        else trim++;
                    }
                    Assert.Greater(trim, 0, "the skirt, the window bands and the dark panels stay out of it");
                    Assert.Greater(paintedCount, trim, "most of the hull is painted");
                    Assert.AreEqual(ShipShell.PaintedCorners(ShipShell.Hull(null)).Length, painted.Length, "a paint changes colours, never shape");
                }
            }
            Assert.IsEmpty(ShipShell.PaintedCorners(null));
        }

        static string Runtime(string folder, string file) => RepoFile("Scripts", "Runtime", folder, file);

        /// <summary>
        /// A hull effect fades with the hull's damage (Ship shades a damaged part's renderers down through _BaseColor): all
        /// of it on a sound hull, about half on a damaged one, none on a broken one, so it never competes with the damage
        /// and a wrecked lander never shimmers. Ship's own shades are read from its code.
        /// </summary>
        [Test]
        public void AHullEffect_FadesWithTheHullsDamage_AndIsGoneFromABrokenHull()
        {
            string ship = Runtime("Ship", "Ship.cs");
            Assert.IsNotNull(ship, "Ship.cs is missing");
            Match m = Regex.Match(ship, @"state == PartState\.Sound \? 1f : state == PartState\.Damaged \? ([\d.]+)f : ([\d.]+)f");
            Assert.IsTrue(m.Success, "Ship's damage shades were found");
            float damaged = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            float broken = float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            Assert.AreEqual(1f, CelestialLooks.HullDamageShare(1f), "all of it on a sound hull");
            Assert.That(CelestialLooks.HullDamageShare(damaged), Is.InRange(0.4f, 0.6f), "about half on a damaged one");
            Assert.AreEqual(0f, CelestialLooks.HullDamageShare(broken), "none on a broken one");
            for (float tint = 0f; tint < 1f; tint += 0.01f)
                Assert.LessOrEqual(CelestialLooks.HullDamageShare(tint), CelestialLooks.HullDamageShare(tint + 0.01f) + 1e-6f, "the more damage, the less");
        }

        /// <summary>
        /// At the Look asset's default bloom threshold (read from LookConfig's code; Unity takes the dial as a gamma value),
        /// the brightest pixel an effect may draw gives under 1% of its light to the bloom, while a glow piece drawn at
        /// twice white gives a fifth or more: an effect never glows like a muzzle flash, an aim tell or a lamp.
        /// </summary>
        [Test]
        public void AnEffectsBrightestPixel_BarelyReachesTheBloom_WhileAGlowPieceGlows()
        {
            string look = Runtime("Look", "LookConfig.cs");
            Assert.IsNotNull(look, "LookConfig.cs is missing");
            float dial = ConstIn(look, "public float bloomThreshold");
            Assert.Less(CelestialLooks.BloomShare(CelestialLooks.Ceiling, dial), 0.01f, "an effect's brightest pixel");
            Assert.Greater(CelestialLooks.BloomShare(2f, dial), 0.15f, "a glow piece at twice white");
            Assert.AreEqual(0f, CelestialLooks.BloomShare(0.5f, dial), 1e-6f, "ordinary light none at all");
        }

        /// <summary>
        /// Round the hull a unit of the run is a unit of the pattern at the cabin's middle and finer nearer the axis, where
        /// the shader fades the pattern sooner, to nothing at the top of the dome; and the hull's faces are only the cap and
        /// round the side, so a round cabin is never cut along upright lines where its side turns.
        /// </summary>
        [Test]
        public void RoundTheHull_ThePatternFadesTowardTheTopOfTheDome_AndTheSideIsNeverCutIntoFour()
        {
            float middle = CelestialLooks.HullUnitsRound / (2f * (float)Math.PI);
            Assert.AreEqual(1f, CelestialLooks.RunScale(0f, middle, true), 1e-4f, "a unit of the run is a unit of the pattern at the cabin's middle");
            Assert.AreEqual(1f, CelestialLooks.RunScale(0f, middle * 2f, true), "never coarser than that counts");
            Assert.AreEqual(0.5f, CelestialLooks.RunScale(middle * 0.5f, 0f, true), 1e-4f);
            Assert.AreEqual(0f, CelestialLooks.RunScale(0f, 0f, true), "nothing at the axis");
            Assert.AreEqual(1f, CelestialLooks.RunScale(0f, 0f, false), "a gun's run is its length");
            for (int k = 0; k < 64; k++)
            {
                double turn = k / 64.0 * Math.PI * 2.0;
                float nx = (float)Math.Sin(turn), nz = (float)Math.Cos(turn);
                Assert.AreEqual(3, CelestialLooks.Face(nx, 0.2f, nz, true), "round the side at " + k);
                Assert.AreEqual(1, CelestialLooks.Face(nx * 0.5f, 0.87f, nz * 0.5f, true), "the cap at " + k);
            }
            Assert.AreEqual(2, CelestialLooks.Face(0f, 0.2f, 1f), "on a gun, the axis it faces most");
        }

        /// <summary>
        /// The one gun that can blow up in the player's hands (the Wildcard) never shows a Celestial effect, because moving
        /// light on it would read as a warning that it is about to: its skin's paint and pattern stay. Every other gun wears
        /// the skin as it is.
        /// </summary>
        [Test]
        public void AGunThatCanBlowUp_WearsTheSkinButNeverItsEffect()
        {
            SkinLook celestial = PrizeLooks.Skin(Find("SkinWoodPanel") ?? Find("SkinRifleStarChart"), "Aurora");
            Assert.IsTrue(celestial.Effect.HasValue);
            SkinLook? wildcard = PrizeLooks.OnGun(celestial, canBlowUp: true);
            Assert.IsTrue(wildcard.HasValue);
            Assert.IsFalse(wildcard.Value.Effect.HasValue, "no moving light on a gun that can blow up");
            Assert.AreEqual(celestial.Pattern, wildcard.Value.Pattern, "the skin itself stays");
            Assert.AreEqual(PrizeLooks.Skin(Find("SkinWoodPanel") ?? Find("SkinRifleStarChart")).Key, wildcard.Value.Key, "the plain skin's material");
            Assert.AreEqual(celestial.Key, PrizeLooks.OnGun(celestial, canBlowUp: false).Value.Key, "any other gun wears it as it is");
            Assert.IsFalse(PrizeLooks.OnGun(null, true).HasValue);
            string lockerLook = Runtime("Items", "LockerLook.cs");
            StringAssert.Contains("gun.backfireChance > 0f", lockerLook, "LockerLook.SkinFor asks the gun whether it can blow up");
            StringAssert.Contains("LockerLook.SkinFor(weapon.Data)", Runtime("Player", "PlayerWeapons.cs"));
        }

        /// <summary>
        /// Only the player's guns wear Locker skins: a gun an enemy picks up (the player's dropped one, say) goes back to its
        /// own paint, so a Celestial effect's moving light is never on an enemy's gun, where it could read as an aim tell.
        /// </summary>
        [Test]
        public void AGunAnEnemyPicksUp_TakesOffItsSkin_SoNoEffectIsEverOnAnEnemysGun()
        {
            string combat = Runtime("AI", "MannequinCombat.cs");
            Assert.IsNotNull(combat, "MannequinCombat.cs is missing");
            string pickUp = Between(combat, "public bool TryPickUp(Weapon weapon)", "_magazineOut = false;");
            StringAssert.Contains("ModelLibrary.Wear(weapon.gameObject, null);", pickUp);
            // And the only other place a skin is put on is the player's own hands.
            string weapons = Runtime("Player", "PlayerWeapons.cs");
            StringAssert.Contains("LockerLook.SkinFor(", weapons);
        }
    }
}
