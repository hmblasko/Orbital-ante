using System;
using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.Space;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// The ground feet stand on and the sinking check measures (Matthew, 20 Nov 2026: "i keep no clipping through
    /// the ground, sometimes in the ground sometimes half way and it does this bounce ... its on every planet").
    /// The check used to measure the ideal ground, which no tile draws; it must measure the tile's own triangles.
    /// </summary>
    public class BuiltGroundTests
    {
        const int Seed = 4242;

        static readonly string[] Worlds = { "Earth", "Moon", "Mars", "Venus", "Europa", "Titan", "Enceladus", "Jupiter" };

        static PlanetTerrain Make(string id)
        {
            PlanetProfile profile = PlanetProfiles.Defaults().Find(p => p.bodyId == id);
            BodySpec spec = SolarSystemDefaults.Bodies().Find(b => b.id == id);
            return new PlanetTerrain(profile, id, spec.radius, Seed);
        }

        static void TileOf(Vec3d direction, int level, out int face, out int x, out int y)
        {
            CubeSphere.Locate(direction, out face, out double u, out double v);
            int side = 1 << level;
            x = Math.Min(side - 1, (int)((u + 1) * 0.5 * side));
            y = Math.Min(side - 1, (int)((v + 1) * 0.5 * side));
        }

        /// <summary>Where a line from the centre meets the tile's real triangles (the mesh and the collider).</summary>
        static double MeshRadius(ChunkData chunk, Vec3d direction)
        {
            double best = double.NaN;
            for (int t = 0; t < chunk.Triangles.Length; t += 3)
            {
                Vec3d a = Corner(chunk, chunk.Triangles[t]), b = Corner(chunk, chunk.Triangles[t + 1]), c = Corner(chunk, chunk.Triangles[t + 2]);
                Vec3d normal = Vec3d.Cross(b - a, c - a);
                double along = Vec3d.Dot(normal, direction);
                if (Math.Abs(along) < 1e-12) continue;
                double r = Vec3d.Dot(normal, a) / along;
                Vec3d p = direction * r;
                double slack = -1e-7 * normal.SqrMagnitude;
                if (Vec3d.Dot(Vec3d.Cross(b - a, p - a), normal) < slack) continue;
                if (Vec3d.Dot(Vec3d.Cross(c - b, p - b), normal) < slack) continue;
                if (Vec3d.Dot(Vec3d.Cross(a - c, p - c), normal) < slack) continue;
                // Skirts hang below the edges; the ground is the highest triangle on the line.
                if (double.IsNaN(best) || r > best) best = r;
            }
            return best;
        }

        static Vec3d Corner(ChunkData chunk, int i) => new Vec3d(chunk.Positions[i * 3], chunk.Positions[i * 3 + 1], chunk.Positions[i * 3 + 2]);

        [Test]
        public void TheBuiltGround_IsExactlyWhereTheTilesTrianglesAre_AtEveryDetail()
        {
            var random = new Random(11);
            foreach (string id in Worlds)
            {
                PlanetTerrain terrain = Make(id);
                var built = new BuiltGround(terrain);
                int finest = TileRules.MaxLevel(terrain.Radius, 1.7);
                for (int k = 0; k < 6; k++)
                {
                    Vec3d direction = new Vec3d(random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1).Normalized;
                    // The finest tiles and coarser ones: ground is drawn (and solid) at every detail on the way down.
                    foreach (int level in new[] { finest, Math.Max(0, finest - 2) })
                    {
                        TileOf(direction, level, out int face, out int x, out int y);
                        ChunkData chunk = TerrainMesher.Build(terrain, face, level, x, y, false);
                        double mesh = MeshRadius(chunk, direction);
                        Assert.IsFalse(double.IsNaN(mesh), id + ": the line should meet the tile it falls in.");
                        Assert.AreEqual(mesh, built.Radius(face, level, x, y, direction), 0.002,
                            id + " level " + level + ": the built ground must be the mesh's own surface.");
                    }
                }
            }
        }

        [Test]
        public void TheIdealHeight_IsNotTheGroundYouStandOn_OnRoughWorlds()
        {
            // Why the check cannot use PlanetTerrain.Height: on rough ground it lies well off the triangles, either
            // way. A sinking check against it lifts an honest stance (the bounce) or misses a real sink.
            var random = new Random(5);
            double above = 0, below = 0;
            foreach (string id in new[] { "Earth", "Europa", "Venus" })
            {
                PlanetTerrain terrain = Make(id);
                var built = new BuiltGround(terrain);
                int finest = TileRules.MaxLevel(terrain.Radius, 1.7);
                for (int k = 0; k < 1500; k++)
                {
                    Vec3d direction = new Vec3d(random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1).Normalized;
                    if (terrain.HasSea && terrain.Height(direction) < terrain.SeaLevel) continue;
                    TileOf(direction, finest, out int face, out int x, out int y);
                    double off = terrain.Radius + terrain.Height(direction) - built.Radius(face, finest, x, y, direction);
                    above = Math.Max(above, off);
                    below = Math.Max(below, -off);
                }
            }
            Assert.Greater(above, 0.7, "Somewhere the ideal ground stands knee high over the real ground.");
            Assert.Greater(below, 0.7, "Somewhere the real ground stands knee high over the ideal ground.");
        }

        [Test]
        public void AskingAgainWhereSomeoneStands_IsRemembered_AndGivesTheSameAnswer()
        {
            PlanetTerrain terrain = Make("Mars");
            var built = new BuiltGround(terrain);
            int level = TileRules.MaxLevel(terrain.Radius, 1.7);
            Vec3d direction = CubeSphere.FromLatLon(12, 40);
            TileOf(direction, level, out int face, out int x, out int y);
            double first = built.Radius(face, level, x, y, direction);
            int misses = built.Misses;
            // A few centimetres along: the same triangle, worked out from what is remembered.
            Vec3d step = (direction + CubeSphere.North(direction) * 1e-5).Normalized;
            double again = built.Radius(face, level, x, y, direction);
            built.Radius(face, level, x, y, step);
            Assert.AreEqual(first, again);
            Assert.LessOrEqual(built.Misses, misses + 1, "Standing still must not work the ground out again.");
            Assert.AreEqual(TerrainMesher.SurfaceRadius(terrain, face, level, x, y, step), built.Radius(face, level, x, y, step), 1e-9);
        }

        [Test]
        public void TheColliderTakesTheGroundOnly_NeverTheSkirts()
        {
            // A skirt is an upright wall hanging from a tile's edge. In the collider it stands proud where tiles of
            // different detail meet and catches the suit, so only the ground's own triangles go in.
            PlanetTerrain terrain = Make("Earth");
            int level = TileRules.MaxLevel(terrain.Radius, 1.7);
            TileOf(CubeSphere.FromLatLon(20, 60), level, out int face, out int x, out int y);
            ChunkData chunk = TerrainMesher.Build(terrain, face, level, x, y, false);
            int side = TerrainMesher.Quads + 1, groundCorners = side * side;
            Assert.AreEqual(TerrainMesher.Quads * TerrainMesher.Quads * 6, chunk.GroundIndexCount);
            for (int t = 0; t < chunk.Triangles.Length; t++)
            {
                bool skirt = chunk.Triangles[t] >= groundCorners;
                if (t < chunk.GroundIndexCount) Assert.IsFalse(skirt, "Ground triangles use ground corners only.");
                else if (t % 3 == 0) Assert.IsTrue(skirt || chunk.Triangles[t + 1] >= groundCorners || chunk.Triangles[t + 2] >= groundCorners,
                    "Everything after the ground is skirt.");
            }
        }
    }
}
