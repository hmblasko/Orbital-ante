using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.Space;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// The rules that make the ground look worn and natural (Matthew, 14 Nov 2026: "much more realistic ... much
    /// more geography"): the noise that knows its own slope, worn hills and mountains, valleys, stepped rock, the
    /// colours of the ground, and the extra landforms. No Unity objects.
    /// </summary>
    public class TerrainRealismTests
    {
        const int Seed = 4242;

        static PlanetTerrain Make(string body, Action<PlanetProfile> change = null)
        {
            PlanetProfile profile = PlanetProfiles.Defaults().First(p => p.bodyId == body);
            change?.Invoke(profile);
            return new PlanetTerrain(profile, body, SolarSystemDefaults.Bodies().First(b => b.id == body).radius, Seed);
        }

        static IEnumerable<Vec3d> Directions(int count, int seed = 11)
        {
            var r = new SeededRandom(seed);
            for (int i = 0; i < count; i++)
            {
                var d = new Vec3d(r.NextDouble() * 2 - 1, r.NextDouble() * 2 - 1, r.NextDouble() * 2 - 1);
                if (d.SqrMagnitude < 0.01) continue;
                yield return d.Normalized;
            }
        }

        [Test]
        public void NoiseWithItsSlope_GivesTheSameValue_AndTheRightSlope()
        {
            var r = new SeededRandom(3);
            for (int i = 0; i < 400; i++)
            {
                double x = r.NextDouble() * 40 - 20, y = r.NextDouble() * 40 - 20, z = r.NextDouble() * 40 - 20;
                uint seed = (uint)(i * 7 + 1);
                double value = Noise3.PerlinD(x, y, z, seed, out double dx, out double dy, out double dz);
                Assert.AreEqual(Noise3.Perlin(x, y, z, seed), value, 1e-9, "The value must be the plain noise exactly.");
                const double e = 1e-5;
                Assert.AreEqual((Noise3.Perlin(x + e, y, z, seed) - Noise3.Perlin(x - e, y, z, seed)) / (2 * e), dx, 1e-4);
                Assert.AreEqual((Noise3.Perlin(x, y + e, z, seed) - Noise3.Perlin(x, y - e, z, seed)) / (2 * e), dy, 1e-4);
                Assert.AreEqual((Noise3.Perlin(x, y, z + e, seed) - Noise3.Perlin(x, y, z - e, seed)) / (2 * e), dz, 1e-4);
            }
        }

        [Test]
        public void WornNoise_StaysInsideItsLimits_AndIsPlainNoiseWithNoWear()
        {
            var r = new SeededRandom(5);
            for (int i = 0; i < 3000; i++)
            {
                var p = new Vec3d(r.NextDouble() * 2000 - 1000, r.NextDouble() * 2000 - 1000, r.NextDouble() * 2000 - 1000);
                double worn = Noise3.Eroded(p, 90, 4, 77, 0.9);
                Assert.That(worn, Is.InRange(-1.06, 1.06));
                Assert.AreEqual(Noise3.Fbm(p, 90, 4, 77), Noise3.Eroded(p, 90, 4, 77, 0), 1e-9, "No wear must be the plain layered noise.");
                Assert.That(Noise3.ErodedRidged(p, 240, 5, 78, 0.6), Is.InRange(0.0, 1.0));
            }
        }

        [Test]
        public void Wear_SmoothsSteepGround_MoreThanGentleGround()
        {
            // The point of it: fine bumps are held back where the broad shape is steep.
            var r = new SeededRandom(8);
            double steepDiff = 0, gentleDiff = 0;
            int steep = 0, gentle = 0;
            for (int i = 0; i < 6000; i++)
            {
                var p = new Vec3d(r.NextDouble() * 2000 - 1000, r.NextDouble() * 2000 - 1000, r.NextDouble() * 2000 - 1000);
                Noise3.PerlinD(p.X / 90, p.Y / 90, p.Z / 90, 77, out double gx, out double gy, out double gz);
                double slope = Math.Sqrt(gx * gx + gy * gy + gz * gz);
                double fineWorn = Noise3.Eroded(p, 90, 4, 77, 1.0) - Noise3.Eroded(p, 90, 1, 77, 1.0) * (1.0 / 1.875);
                double finePlain = Noise3.Fbm(p, 90, 4, 77) - Noise3.Fbm(p, 90, 1, 77) * (1.0 / 1.875);
                if (Math.Abs(finePlain) < 0.02) continue;
                double kept = Math.Abs(fineWorn) / Math.Abs(finePlain);
                if (slope > 1.3) { steepDiff += kept; steep++; }
                else if (slope < 0.5) { gentleDiff += kept; gentle++; }
            }
            Assert.Greater(steep, 100);
            Assert.Greater(gentle, 100);
            Assert.Less(steepDiff / steep, gentleDiff / gentle, "Steep ground should keep less of its fine bumps than gentle ground.");
        }

        [Test]
        public void EveryWorldsGround_StaysBetweenItsStatedLowestAndHighest_WithTheNewShapes()
        {
            foreach (PlanetProfile profile in PlanetProfiles.Defaults())
            {
                PlanetTerrain t = Make(profile.bodyId);
                foreach (Vec3d d in Directions(1500))
                {
                    double h = t.Height(d);
                    Assert.GreaterOrEqual(h, t.MinHeight, profile.bodyId);
                    Assert.LessOrEqual(h, t.MaxHeight, profile.bodyId);
                }
            }
        }

        [Test]
        public void Valleys_OnlyEverCutDown_AndNoDeeperThanTheirDial()
        {
            PlanetTerrain with = Make("Mars");
            PlanetTerrain without = Make("Mars", p => p.valleyDepth = 0);
            double deepest = 0;
            int cut = 0, all = 0;
            foreach (Vec3d d in Directions(4000))
            {
                // Stepped rock is laid over the valleys, so compare with it switched off in both.
                all++;
            }
            with = Make("Mars", p => p.terraceStep = 0);
            without = Make("Mars", p => { p.terraceStep = 0; p.valleyDepth = 0; });
            foreach (Vec3d d in Directions(4000))
            {
                double drop = without.Height(d) - with.Height(d);
                Assert.GreaterOrEqual(drop, -1e-6, "A valley must never raise the ground.");
                Assert.LessOrEqual(drop, with.Profile.valleyDepth + 1e-6);
                if (drop > 0.5) cut++;
                deepest = Math.Max(deepest, drop);
            }
            Assert.Greater(all, 0);
            Assert.Greater(deepest, with.Profile.valleyDepth * 0.5, "Somewhere a valley should be cut to a good part of its depth.");
            Assert.That(cut / 4000.0, Is.InRange(0.02, 0.45), "Valleys should be lines in the land, neither missing nor everywhere.");
        }

        [Test]
        public void SteppedRock_MovesTheGroundLessThanOneStep_AndLeavesLevelBenches()
        {
            PlanetTerrain with = Make("Mars");
            PlanetTerrain without = Make("Mars", p => p.terraceStep = 0);
            double step = with.Profile.terraceStep;
            int changed = 0;
            foreach (Vec3d d in Directions(4000))
            {
                double moved = Math.Abs(with.Height(d) - without.Height(d));
                Assert.LessOrEqual(moved, step, "Stepping must never move the ground by more than one step.");
                if (moved > 0.05) changed++;
            }
            Assert.That(changed / 4000.0, Is.InRange(0.05, 0.9), "Stepped country should cover some of Mars, not none and not all.");
        }

        [Test]
        public void TheLandStillHasLevelGroundToLandOn_AndCliffsToLookAt()
        {
            foreach (string body in new[] { "Earth", "Mars", "Moon", "Venus", "Titan" })
            {
                PlanetTerrain t = Make(body);
                int land = 0, level = 0, cliffs = 0;
                foreach (Vec3d d in Directions(2500))
                {
                    if (t.HasSea && t.Height(d) < t.SeaLevel) continue;
                    land++;
                    double slope = t.SlopeAt(d, 3);
                    if (slope < 0.12) level++;
                    if (slope > 0.6) cliffs++;
                }
                Assert.Greater(level / (double)land, 0.22, body + ": too little ground level enough to land on or build a base on.");
                Assert.Less(cliffs / (double)land, 0.3, body + ": too much of the land is cliff.");
            }
        }

        [Test]
        public void GroundColours_AreNeverGarish_AndNothingButSnowAndIceIsBrighterThanPaleSand()
        {
            // The look thread's rule for the realism shift: natural, greyed colours; saturation comes from light
            // and air, not from the ground paint.
            foreach (string body in new[] { "Earth", "Mars", "Moon", "Mercury", "Venus", "Callisto", "Ganymede", "Titan" })
            {
                PlanetTerrain t = Make(body);
                double loud = 0;
                int n = 0;
                foreach (Vec3d d in Directions(1500))
                {
                    TerrainPoint point = t.Sample(d);
                    if (t.HasSea && point.Height < t.SeaLevel) continue;
                    t.ColorAt(d, point, 0.1, out float r, out float g, out float b);
                    Assert.That(r, Is.InRange(0f, 1f)); Assert.That(g, Is.InRange(0f, 1f)); Assert.That(b, Is.InRange(0f, 1f));
                    float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
                    double saturation = max <= 0 ? 0 : (max - min) / max;
                    loud += saturation;
                    n++;
                    bool white = saturation < 0.12;   // snow, ice, frost
                    if (!white) Assert.Less(max, 0.86f, body + ": coloured ground brighter than pale sand.");
                }
                Assert.Less(loud / n, 0.52, body + ": the ground is too strongly coloured on average.");
            }
        }

        [Test]
        public void Hollows_AreDarker_AndCrestsPaler_ThanLevelGround()
        {
            PlanetTerrain t = Make("Moon");
            foreach (Vec3d d in Directions(200))
            {
                TerrainPoint point = t.Sample(d);
                t.ColorAt(d, point, 0.1, out float r0, out float g0, out float b0);
                t.ColorAt(d, point, 0.1, out float r1, out float g1, out float b1, 1.0);
                t.ColorAt(d, point, 0.1, out float r2, out float g2, out float b2, -1.0);
                Assert.Less(r1 + g1 + b1, r0 + g0 + b0);
                Assert.Greater(r2 + g2 + b2, r0 + g0 + b0);
            }
        }

        [Test]
        public void RockFaces_AreNotOneFlatColour_AndNotRuledStripes()
        {
            // Going straight up a cliff the rock should change tone, by beds of unequal thickness.
            PlanetTerrain t = Make("Earth");
            Vec3d d = new Vec3d(0.3, 0.5, 0.81).Normalized;
            var tones = new List<double>();
            for (int i = 0; i < 240; i++)
            {
                var point = new TerrainPoint { Height = 10 + i * 0.25 };
                t.ColorAt(d, point, 0.9, out float r, out float g, out float b);
                tones.Add(r + g + b);
            }
            Assert.Greater(tones.Max() - tones.Min(), 0.08, "A rock face should show its beds.");
            // Count the gaps between turning points: ruled stripes would all be the same width.
            var turns = new List<int>();
            for (int i = 1; i < tones.Count - 1; i++)
            {
                if ((tones[i] - tones[i - 1]) * (tones[i + 1] - tones[i]) < 0) turns.Add(i);
            }
            Assert.Greater(turns.Count, 4);
            var gaps = new List<int>();
            for (int i = 1; i < turns.Count; i++) gaps.Add(turns[i] - turns[i - 1]);
            Assert.Greater(gaps.Max(), gaps.Min() * 2, "Beds should be of unequal thickness.");
        }

        [Test]
        public void EveryWorld_GetsMoreSeededLandforms_ButLandmarksSeasAndWoodsStayAsListed()
        {
            foreach (PlanetProfile profile in PlanetProfiles.Defaults())
            {
                foreach (FormationUse use in profile.formations)
                {
                    Formation f = FormationIndex.Get(use.formation);
                    Assert.IsNotNull(f, profile.bodyId + " lists an unknown formation: " + use.formation);
                    if (use.landmark) Assert.AreEqual(1, use.count, profile.bodyId + ": a landmark is one thing in one place.");
                }
            }
            PlanetProfile earth = PlanetProfiles.Defaults().First(p => p.bodyId == "Earth");
            Assert.AreEqual(1, earth.formations.First(u => u.formation == "ocean").count);
            Assert.AreEqual(8, earth.formations.First(u => u.formation == "conifer_forest").count, "Woods are not multiplied.");
            Assert.AreEqual(5, earth.formations.First(u => u.formation == "lake").count, "Lakes are not multiplied.");
            Assert.AreEqual((int)Math.Ceiling(6 * PlanetProfiles.MoreLandforms), earth.formations.First(u => u.formation == "butte").count);
            PlanetProfile moon = PlanetProfiles.Defaults().First(p => p.bodyId == "Moon");
            Assert.Greater(moon.formations.Where(u => !u.landmark).Sum(u => u.count), 150);
        }

        [Test]
        public void TheSameSeed_GivesTheSameGround_Twice()
        {
            PlanetTerrain a = Make("Earth"), b = Make("Earth");
            foreach (Vec3d d in Directions(300))
            {
                Assert.AreEqual(a.Height(d), b.Height(d));
                a.ColorAt(d, a.Sample(d), 0.3, out float r1, out float g1, out float b1, 0.2);
                b.ColorAt(d, b.Sample(d), 0.3, out float r2, out float g2, out float b2, 0.2);
                Assert.AreEqual(r1, r2); Assert.AreEqual(g1, g2); Assert.AreEqual(b1, b2);
            }
        }
    }
}
