using System;
using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.Space;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// The rules behind Milestone 6: the index of natural formations, the shape of every body's ground, caves,
    /// the tiles the ground is drawn with, the far-away picture, air, cloud decks, seas and weather.
    /// These run without Unity objects.
    /// </summary>
    public class PlanetRulesTests
    {
        const int Seed = 4242;
        static Dictionary<string, PlanetTerrain> _terrains;
        static Dictionary<string, BodySpec> _bodies;
        static List<PlanetProfile> _profiles;

        [OneTimeSetUp]
        public void BuildEveryBodyOnce()
        {
            _profiles = PlanetProfiles.Defaults();
            _bodies = new Dictionary<string, BodySpec>();
            foreach (BodySpec b in SolarSystemDefaults.Bodies()) _bodies[b.id] = b;
            _terrains = new Dictionary<string, PlanetTerrain>();
            foreach (PlanetProfile p in _profiles) _terrains[p.bodyId] = new PlanetTerrain(p, p.bodyId, _bodies[p.bodyId].radius, Seed);
        }

        static IEnumerable<Vec3d> Directions(int count, int seed = 7)
        {
            var r = new SeededRandom(seed);
            for (int i = 0; i < count; i++)
            {
                var d = new Vec3d(r.NextDouble() * 2 - 1, r.NextDouble() * 2 - 1, r.NextDouble() * 2 - 1);
                if (d.SqrMagnitude < 0.01) continue;
                yield return d.Normalized;
            }
        }

        // ------------------------------------------------------------------ the index

        [Test]
        public void TheIndexHasOverAHundredFormationsWithUniqueIds()
        {
            Assert.Greater(FormationIndex.All.Count, 100);
            var ids = new HashSet<string>();
            foreach (Formation f in FormationIndex.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(f.Id));
                Assert.IsTrue(ids.Add(f.Id), f.Id + " is in the index twice.");
                Assert.IsFalse(string.IsNullOrEmpty(f.Name), f.Id + " has no name.");
                Assert.IsFalse(string.IsNullOrEmpty(f.Description), f.Id + " has no description.");
                Assert.AreSame(f, FormationIndex.Get(f.Id));
            }
        }

        [Test]
        public void EveryFormationIsUsedOnAtLeastOneBody()
        {
            var used = new HashSet<string>();
            foreach (PlanetProfile p in _profiles)
            {
                foreach (FormationUse use in p.formations)
                {
                    Formation f = FormationIndex.Get(use.formation);
                    Assert.IsNotNull(f, p.bodyId + " asks for a formation that is not in the index: " + use.formation);
                    used.Add(f.Id);
                    if (!string.IsNullOrEmpty(f.Companion)) used.Add(f.Companion);
                }
            }
            foreach (Formation f in FormationIndex.All) Assert.IsTrue(used.Contains(f.Id), f.Id + " is in the index but no body has it.");
        }

        [Test]
        public void EveryBodyWithGroundHasAProfileAndEveryProfileHasABody()
        {
            foreach (PlanetProfile p in _profiles) Assert.IsTrue(_bodies.ContainsKey(p.bodyId), p.bodyId + " is not a body in the solar system.");
            foreach (BodySpec b in _bodies.Values)
            {
                if (b.kind == BodyKind.Star) continue;
                Assert.IsTrue(_terrains.ContainsKey(b.id), b.id + " has no ground.");
            }
        }

        // ------------------------------------------------------------------ ground

        [Test]
        public void TheSameSeedGivesTheSameGroundAndADifferentSeedMovesTheFormations()
        {
            PlanetProfile mars = _profiles.Find(p => p.bodyId == "Mars");
            var again = new PlanetTerrain(mars, "Mars", _bodies["Mars"].radius, Seed);
            var other = new PlanetTerrain(mars, "Mars", _bodies["Mars"].radius, Seed + 1);
            PlanetTerrain first = _terrains["Mars"];
            Assert.AreEqual(first.Stamps.Count, again.Stamps.Count);
            int differs = 0;
            foreach (Vec3d d in Directions(300))
            {
                Assert.AreEqual(first.Height(d), again.Height(d), 0, "The same seed must give exactly the same ground.");
                if (Math.Abs(first.Height(d) - other.Height(d)) > 0.5) differs++;
            }
            Assert.Greater(differs, 10, "A different seed should move the craters and canyons about.");
        }

        [Test]
        public void LandmarksStayPutWhateverTheSeed()
        {
            PlanetProfile mars = _profiles.Find(p => p.bodyId == "Mars");
            var other = new PlanetTerrain(mars, "Mars", _bodies["Mars"].radius, Seed + 99);
            int landmarks = 0;
            for (int i = 0; i < mars.formations.Count; i++)
            {
                if (!mars.formations[i].landmark) continue;
                landmarks++;
                Vec3d where = CubeSphere.FromLatLon(mars.formations[i].latitude, mars.formations[i].longitude);
                bool foundHere = false, foundThere = false;
                foreach (Stamp s in _terrains["Mars"].Stamps) foundHere |= s.Formation.Id == mars.formations[i].formation && Vec3d.Distance(s.Kind == StampKind.Curve ? s.Start : s.Center, where) < 0.6;
                foreach (Stamp s in other.Stamps) foundThere |= s.Formation.Id == mars.formations[i].formation && Vec3d.Distance(s.Kind == StampKind.Curve ? s.Start : s.Center, where) < 0.6;
                Assert.IsTrue(foundHere && foundThere, "Mars's " + mars.formations[i].formation + " should be in the same place every run.");
            }
            Assert.Greater(landmarks, 0, "Mars should have its great canyon and volcano as fixed landmarks.");
        }

        [Test]
        public void GroundStaysInsideItsStatedLimitsAndIsNeverBroken()
        {
            foreach (KeyValuePair<string, PlanetTerrain> pair in _terrains)
            {
                PlanetTerrain t = pair.Value;
                foreach (Vec3d d in Directions(400, 11))
                {
                    double h = t.Height(d);
                    Assert.IsFalse(double.IsNaN(h) || double.IsInfinity(h), pair.Key + " has broken ground.");
                    Assert.GreaterOrEqual(h, t.MinHeight, pair.Key + " ground is below its stated lowest point.");
                    Assert.LessOrEqual(h, t.MaxHeight, pair.Key + " ground is above its stated highest point.");
                    Assert.Less(Math.Abs(h), t.Radius * 0.5, pair.Key + ": mountains or pits out of proportion to the body.");
                }
            }
        }

        [Test]
        public void NoBodyIsAFlatPlain()
        {
            foreach (KeyValuePair<string, PlanetTerrain> pair in _terrains)
            {
                PlanetTerrain t = pair.Value;
                double sum = 0, sumSquares = 0, steep = 0;
                int n = 0;
                foreach (Vec3d d in Directions(400, 23))
                {
                    double h = t.Height(d);
                    if (t.HasSea && h < t.SeaLevel) continue;
                    sum += h;
                    sumSquares += h * h;
                    if (t.SlopeAt(d, 3) > 0.05) steep++;
                    n++;
                }
                if (n < 20) continue;   // Nearly all sea (Neptune): its interest is under the water.
                double spread = Math.Sqrt(Math.Max(0, sumSquares / n - sum / n * (sum / n)));
                Assert.Greater(spread, Math.Min(2.0, t.Radius * 0.006), pair.Key + " is too flat overall.");
                Assert.Greater(steep / n, 0.25, pair.Key + " has too much level ground: hills should be everywhere.");
            }
        }

        [Test]
        public void EarthHasSeaAndLandAndNeptuneIsNearlyAllOcean()
        {
            Assert.That(SeaFraction(_terrains["Earth"]), Is.InRange(0.25, 0.7));
            Assert.Greater(SeaFraction(_terrains["Neptune"]), 0.8);
            Assert.Less(SeaFraction(_terrains["Neptune"]), 1.0, "Neptune should still have a few islands.");
            Assert.IsFalse(_terrains["Moon"].HasSea);
        }

        static double SeaFraction(PlanetTerrain t)
        {
            int sea = 0, n = 0;
            foreach (Vec3d d in Directions(1500, 31))
            {
                n++;
                if (t.Height(d) < t.SeaLevel) sea++;
            }
            return sea / (double)n;
        }

        [Test]
        public void GasGiantsHaveGroundFarBelowTheirCloudTops()
        {
            foreach (string id in new[] { "Jupiter", "Saturn", "Uranus", "Neptune" })
            {
                PlanetTerrain t = _terrains[id];
                Assert.IsTrue(t.Profile.cloudDeck, id);
                Assert.Less(t.Radius + t.MaxHeight, _bodies[id].radius * 0.75, id + ": the ground must be well inside the clouds.");
                // Gravity fades toward the middle, which is what makes these surfaces landable.
                double pull = _bodies[id].surfaceGravity * t.Radius / _bodies[id].radius;
                Assert.That(pull, Is.InRange(2.0, 8.0), id + " surface pull");
            }
        }

        [Test]
        public void PlacedThingsStandOnBodiesThatShouldHaveThem()
        {
            Assert.Greater(_terrains["Mars"].Placements.Count, 50);
            Assert.Greater(_terrains["Earth"].Placements.Count, 50);
            var list = new List<Placement>();
            int rocks = 0;
            for (int x = 0; x < 4; x++)
            {
                PlanetScatter.ForTile(_terrains["Moon"], 2, 2, x, 1, list);
                rocks += list.Count;
            }
            Assert.Greater(rocks, 5, "The Moon should have loose rocks lying about.");
            var again = new List<Placement>();
            PlanetScatter.ForTile(_terrains["Moon"], 2, 2, 3, 1, again);
            var third = new List<Placement>();
            PlanetScatter.ForTile(_terrains["Moon"], 2, 2, 3, 1, third);
            Assert.AreEqual(again.Count, third.Count);
            for (int i = 0; i < again.Count; i++) Assert.AreEqual(again[i].Direction, third[i].Direction, "The same tile must get the same rocks.");
        }

        // ------------------------------------------------------------------ caves

        [Test]
        public void CavesStayUnderGroundWithARoofOverThem()
        {
            int caves = 0;
            foreach (KeyValuePair<string, PlanetTerrain> pair in _terrains)
            {
                PlanetTerrain t = pair.Value;
                foreach (Cave cave in t.Caves)
                {
                    caves++;
                    Assert.GreaterOrEqual(cave.Mouths.Length, 1);
                    Assert.AreEqual(cave.Centres.Length, cave.Radii.Length);
                    int buried = 0, roomy = 0;
                    for (int k = 0; k < cave.Centres.Length; k++)
                    {
                        Vec3d c = cave.Centres[k];
                        double roof = t.Radius + t.Height(c.Normalized) - (c.Magnitude + cave.Radii[k]);
                        if (roof >= 2.5) buried++;
                        Assert.Greater(cave.Radii[k], 0.4, pair.Key + ": a cave pinched shut.");
                        if (cave.Radii[k] >= 2.6) roomy++;
                    }
                    // The test craft is a ball 1.6 m in radius. Only the tapering dead end may be narrower than it.
                    Assert.Greater(roomy, cave.Centres.Length * 0.8, pair.Key + " " + cave.Formation + ": too narrow to fly into.");
                    Assert.Greater(cave.MouthRadius, 2.6, pair.Key + ": cave mouth too small to enter.");
                    Assert.Greater(buried, cave.Centres.Length / 2, pair.Key + " " + cave.Formation + ": most of a cave should have a proper roof.");
                    foreach (Vec3d mouth in cave.Mouths) Assert.IsTrue(t.InHole(mouth, 0), pair.Key + ": a cave mouth with no hole cut in the ground.");

                    ShapeData walls = CaveMesher.Build(cave, t);
                    AssertWellFormed(walls.Positions, walls.Triangles, pair.Key + " cave walls");
                }
            }
            Assert.Greater(caves, 10, "There should be caves across the solar system.");
        }

        // ------------------------------------------------------------------ tiles

        static void AssertWellFormed(float[] positions, int[] triangles, string what)
        {
            for (int i = 0; i < positions.Length; i++) Assert.IsFalse(float.IsNaN(positions[i]) || float.IsInfinity(positions[i]), what + " has a broken point.");
            Assert.AreEqual(0, triangles.Length % 3, what);
            int count = positions.Length / 3;
            for (int i = 0; i < triangles.Length; i++) Assert.That(triangles[i], Is.InRange(0, count - 1), what);
        }

        [Test]
        public void GroundTilesAreWellFormedAndFaceOutward()
        {
            PlanetTerrain t = _terrains["Mars"];
            foreach (int level in new[] { 0, 2, 4 })
            {
                ChunkData tile = TerrainMesher.Build(t, 1, level, (1 << level) / 2, (1 << level) / 3, level >= 3);
                AssertWellFormed(tile.Positions, tile.Triangles, "Mars tile");
                Assert.AreEqual(tile.Positions.Length, tile.Normals.Length);
                Assert.AreEqual(tile.Positions.Length, tile.Colors.Length);
                int side = TerrainMesher.Quads + 1;
                for (int i = 0; i < side * side; i++)
                {
                    var p = new Vec3d(tile.Positions[i * 3], tile.Positions[i * 3 + 1], tile.Positions[i * 3 + 2]);
                    var n = new Vec3d(tile.Normals[i * 3], tile.Normals[i * 3 + 1], tile.Normals[i * 3 + 2]);
                    Assert.Greater(Vec3d.Dot(n, p.Normalized), 0, "A ground normal points into the planet.");
                }
                // The first triangle of the ground itself must face outward (Unity draws only front faces).
                var a = Point(tile, tile.Triangles[0]);
                var b = Point(tile, tile.Triangles[1]);
                var c = Point(tile, tile.Triangles[2]);
                Assert.Greater(Vec3d.Dot(Vec3d.Cross(b - a, c - a), a.Normalized), 0, "Ground triangles face inward.");
            }
        }

        static Vec3d Point(ChunkData tile, int index)
        {
            return new Vec3d(tile.Positions[index * 3], tile.Positions[index * 3 + 1], tile.Positions[index * 3 + 2]);
        }

        [Test]
        public void NeighbouringTilesMeetAlongTheirSharedEdge()
        {
            PlanetTerrain t = _terrains["Moon"];
            ChunkData left = TerrainMesher.Build(t, 0, 2, 1, 1, false);
            ChunkData right = TerrainMesher.Build(t, 0, 2, 2, 1, false);
            int side = TerrainMesher.Quads + 1;
            for (int j = 0; j < side; j++)
            {
                Vec3d a = Point(left, j * side + (side - 1));
                Vec3d b = Point(right, j * side);
                Assert.Less(Vec3d.Distance(a, b), 0.01, "Two tiles of the same detail must join without a gap.");
            }
        }

        [Test]
        public void DetailIsFineEnoughToLandOnAndSplitsNearTheViewer()
        {
            foreach (KeyValuePair<string, PlanetTerrain> pair in _terrains)
            {
                int max = TileRules.MaxLevel(pair.Value.Radius, 1.7);
                Assert.LessOrEqual(TerrainMesher.CellSize(pair.Value.Radius, max), 1.7, pair.Key);
                Assert.LessOrEqual(max, TileRules.DeepestLevel);
            }
            double radius = 500;
            int deepest = TileRules.MaxLevel(radius, 1.7);
            Vec3d centre = TerrainMesher.TileCentre(0, 1, 0, 0) * radius;
            Assert.IsTrue(TileRules.WantsSplit(radius, 1, deepest, centre, centre * 1.02, 1.7, false), "Standing on a tile, it should split.");
            Assert.IsFalse(TileRules.WantsSplit(radius, 1, deepest, centre, centre * 10, 1.7, false), "From far away, it should not.");
            Assert.IsFalse(TileRules.WantsSplit(radius, deepest, deepest, centre, centre, 1.7, false), "The finest tiles never split.");
        }

        // ------------------------------------------------------------------ the far-away picture

        [Test]
        public void ThePictureAndTheGroundAgreeAboutWhereThingsAre()
        {
            foreach (Vec3d d in Directions(50, 5))
            {
                FarMap.Locate(d, out double u, out double v);
                Assert.Less(Vec3d.Distance(d, FarMap.Direction(u, v)), 1e-6);
            }
            byte[] earth = FarMap.Paint(_terrains["Earth"], 128);
            byte[] again = FarMap.Paint(_terrains["Earth"], 128);
            CollectionAssert.AreEqual(earth, again, "The picture must be the same every time.");
            int blue = 0, other = 0;
            for (int i = 0; i < earth.Length; i += 4)
            {
                if (earth[i + 2] > earth[i] + 30 && earth[i + 2] > earth[i + 1]) blue++;
                else other++;
            }
            Assert.Greater(blue, earth.Length / 4 / 6, "Earth should show its oceans from space.");
            Assert.Greater(other, earth.Length / 4 / 6, "Earth should show its land from space.");

            byte[] mars = FarMap.Paint(_terrains["Mars"], 64);
            long red = 0, blueSum = 0;
            for (int i = 0; i < mars.Length; i += 4)
            {
                red += mars[i];
                blueSum += mars[i + 2];
            }
            Assert.Greater(red, blueSum * 1.3, "Mars should be red.");
        }

        // ------------------------------------------------------------------ air, clouds and sea

        [Test]
        public void AGasGiantsCloudsCloseRoundYouAndOpenAgainUnderneath()
        {
            PlanetProfile jupiter = _terrains["Jupiter"].Profile;
            double surface = _terrains["Jupiter"].Radius, body = _bodies["Jupiter"].radius;
            double top = AtmosphereModel.Top(jupiter, surface, body);
            Assert.AreEqual(body - surface, top, 1e-6);

            AirSample space = AtmosphereModel.At(jupiter, surface, body, top + body * 0.2);
            Assert.IsFalse(space.InAtmosphere);
            Assert.AreEqual(0, space.Sky);

            AirSample deck = AtmosphereModel.At(jupiter, surface, body, top - 50);
            Assert.IsTrue(deck.InAtmosphere);
            Assert.AreEqual(1, deck.Cloud, 1e-6);
            Assert.AreEqual(AtmosphereModel.CloudVisibility, deck.Visibility, 1.0, "Inside the deck you can see almost nothing.");

            AirSample below = AtmosphereModel.At(jupiter, surface, body, jupiter.cloudBase - AtmosphereModel.CloudFade - 20);
            Assert.AreEqual(0, below.Cloud, 1e-6);
            Assert.AreEqual(jupiter.visibility, below.Visibility, 1.0, "Under the deck the air is clear again.");
            Assert.AreEqual(1, below.Sky, "Under the deck the sky is the planet's gases, not space.");

            // Sinking in from above, it only ever gets murkier until you are in the deck.
            double last = double.MaxValue;
            for (double altitude = top + body * 0.04; altitude >= top; altitude -= body * 0.004)
            {
                double v = AtmosphereModel.At(jupiter, surface, body, altitude).Visibility;
                Assert.LessOrEqual(v, last + 1e-6);
                last = v;
            }
        }

        [Test]
        public void AirThinsWithHeightAndAirlessBodiesHaveNone()
        {
            PlanetProfile earth = _terrains["Earth"].Profile;
            double r = _terrains["Earth"].Radius;
            AirSample ground = AtmosphereModel.At(earth, r, r, 5);
            AirSample high = AtmosphereModel.At(earth, r, r, earth.atmosphereHeight * 0.8);
            AirSample space = AtmosphereModel.At(earth, r, r, earth.atmosphereHeight + 1);
            Assert.Greater(ground.Sky, high.Sky);
            Assert.Greater(high.Sky, 0);
            Assert.Less(ground.Visibility, high.Visibility);
            Assert.IsFalse(space.InAtmosphere);

            PlanetProfile moon = _terrains["Moon"].Profile;
            AirSample onMoon = AtmosphereModel.At(moon, 150, 150, 2);
            Assert.IsFalse(onMoon.InAtmosphere, "The Moon has no air: no tint, no haze.");
            Assert.AreEqual(0, onMoon.Sky);
            Assert.AreEqual(0, onMoon.Drag);
        }

        [Test]
        public void UnderTheSeaItIsMurkyDarkAndSlow()
        {
            PlanetProfile neptune = _terrains["Neptune"].Profile;
            double surface = _terrains["Neptune"].Radius, body = _bodies["Neptune"].radius;
            AirSample shallow = AtmosphereModel.At(neptune, surface, body, neptune.seaLevel - 2);
            AirSample deep = AtmosphereModel.At(neptune, surface, body, neptune.seaLevel - 80);
            Assert.IsTrue(shallow.Underwater && deep.Underwater);
            Assert.AreEqual(2, shallow.Depth, 1e-6);
            Assert.Less(deep.Visibility, shallow.Visibility);
            Assert.Greater(deep.SunDimming, shallow.SunDimming);
            Assert.AreEqual(AtmosphereModel.WaterDrag, deep.Drag);
            Assert.IsFalse(AtmosphereModel.At(neptune, surface, body, neptune.seaLevel + 5).Underwater);
        }

        // ------------------------------------------------------------------ weather

        [Test]
        public void WeatherIsSeededStaysInBoundsAndBlowsAlongTheGround()
        {
            int bodiesWithWeather = 0, lightningColours = 0;
            var colours = new HashSet<string>();
            foreach (KeyValuePair<string, PlanetTerrain> pair in _terrains)
            {
                PlanetProfile p = pair.Value.Profile;
                var weather = new WeatherModel(p, pair.Key, pair.Value.Radius, Seed);
                var same = new WeatherModel(p, pair.Key, pair.Value.Radius, Seed);
                if (!p.hasAtmosphere) Assert.IsFalse(weather.Any, pair.Key + " has no air, so no weather.");
                if (!weather.Any) continue;
                bodiesWithWeather++;
                foreach (WeatherSpec spec in p.weather)
                {
                    if (!string.IsNullOrEmpty(spec.lightningColor) && colours.Add(spec.lightningColor)) lightningColours++;
                }

                bool stormFound = false;
                for (int i = 0; i < weather.Count; i++)
                {
                    for (double time = 0; time < 1200; time += 97)
                    {
                        Vec3d centre = weather.Centre(i, time);
                        Assert.AreEqual(1, centre.Magnitude, 1e-9);
                        Assert.AreEqual(centre, same.Centre(i, time), "The same seed must give the same storms.");
                        WeatherSample at = weather.At(centre, time);
                        Assert.That(at.Intensity, Is.InRange(0.0, 1.0));
                        if (at.Intensity > 0.5)
                        {
                            stormFound = true;
                            Assert.Less(Math.Abs(Vec3d.Dot(at.Wind, centre)), 1e-6, "Wind should blow along the ground.");
                        }
                    }
                }
                Assert.IsTrue(stormFound, pair.Key + " should have a storm somewhere at some time.");
            }
            Assert.GreaterOrEqual(bodiesWithWeather, 6);
            Assert.GreaterOrEqual(lightningColours, 4, "Lightning should come in different colours on different worlds.");
        }
    }
}
