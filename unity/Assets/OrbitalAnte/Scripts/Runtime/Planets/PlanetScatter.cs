using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// Sprinkles a body's own loose rocks (and Earth's trees and bushes) over one tile of ground, following the
    /// body's scatter rules. The same tile always gets the same rocks for the same run seed. Pure rules, no
    /// Unity types.
    /// </summary>
    public static class PlanetScatter
    {
        /// <summary>Fills <paramref name="results"/> with the loose things on one tile.</summary>
        public static void ForTile(PlanetTerrain terrain, int face, int level, int ix, int iy, List<Placement> results)
        {
            PlanetProfile profile = terrain.Profile;
            if (profile.scatter.Count == 0) return;
            double tile = TerrainMesher.TileSize(terrain.Radius, level);
            double hectares = tile * tile / 10000.0;
            double size = 2.0 / (1 << level);
            float[] rock = ColorUtil.Parse(profile.rockColor);

            for (int ruleIndex = 0; ruleIndex < profile.scatter.Count; ruleIndex++)
            {
                ScatterRule rule = profile.scatter[ruleIndex];
                var r = SeededRandom.ForStream(terrain.Seed,
                    "scatter." + terrain.BodyId + "." + ruleIndex + "." + face + "." + level + "." + ix + "." + iy);
                double expected = rule.perHectare * hectares;
                int count = (int)expected + (r.Chance(expected - Math.Floor(expected)) ? 1 : 0);
                bool plant = Feature.IsPlant(rule.feature);
                float[] colour = plant && string.IsNullOrEmpty(rule.color)
                    ? new[] { 1f, 1f, 1f }
                    : string.IsNullOrEmpty(rule.color) ? rock : ColorUtil.Parse(rule.color);

                for (int i = 0; i < count; i++)
                {
                    // Every number is drawn before any test, so one rock being rejected never moves the others.
                    double u = -1 + (ix + r.NextDouble()) * size;
                    double v = -1 + (iy + r.NextDouble()) * size;
                    double t = r.NextDouble();
                    double yaw = r.NextDouble() * Math.PI * 2;
                    int variant = r.Range(0, 1024);

                    Vec3d dir = CubeSphere.Direction(face, u, v);
                    TerrainPoint point = terrain.Sample(dir);
                    if (point.Height < rule.minHeight || point.Height > rule.maxHeight) continue;
                    if (terrain.HasSea && point.Height < terrain.SeaLevel + 0.3) continue;
                    // Plants keep off dunes, salt, lava and the like.
                    if (plant && point.TintAmount > 0.35) continue;
                    if (terrain.InHole(dir, 8)) continue;
                    if (terrain.SlopeAt(dir, 1.5) > rule.maxSlope) continue;

                    results.Add(new Placement
                    {
                        Direction = dir, Kind = rule.feature, Variant = variant,
                        Scale = Noise3.Lerp(rule.minScale, rule.maxScale, t * t), Yaw = yaw,
                        R = colour[0], G = colour[1], B = colour[2],
                    });
                }
            }
        }
    }
}
