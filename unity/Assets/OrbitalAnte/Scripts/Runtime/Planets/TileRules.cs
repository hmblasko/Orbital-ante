using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// Decides how finely the ground is drawn. The ground is cut into square tiles; a tile near you is swapped
    /// for four smaller, more detailed ones, and those for four more, until the detail is fine enough to walk
    /// on. Pure rules, no Unity types.
    /// </summary>
    public static class TileRules
    {
        public const int DeepestLevel = 7;

        /// <summary>How many times the six starting tiles are allowed to split for a body of this size.</summary>
        public static int MaxLevel(double radius, double finestCell)
        {
            int level = 0;
            while (level < DeepestLevel && TerrainMesher.CellSize(radius, level) > finestCell) level++;
            return level;
        }

        /// <summary>
        /// True if a tile is close enough to the viewer to be swapped for four finer ones.
        /// <paramref name="centre"/> is the tile's middle and <paramref name="viewer"/> the viewer, both in metres
        /// from the body's centre. <paramref name="alreadySplit"/> makes a split tile a little reluctant to merge
        /// again, so the ground does not flicker as you hover on the boundary.
        /// </summary>
        public static bool WantsSplit(double radius, int level, int maxLevel, Vec3d centre, Vec3d viewer, double splitDistance, bool alreadySplit)
        {
            if (level >= maxLevel) return false;
            double size = TerrainMesher.TileSize(radius, level);
            double distance = Math.Max(0, Vec3d.Distance(centre, viewer) - size * 0.5);
            return distance < size * splitDistance * (alreadySplit ? 1.15 : 1.0);
        }

        /// <summary>How far a viewer can be from the middle of a tile and still be over it or right beside it.</summary>
        public static double Reach(double radius, int level) => TerrainMesher.TileSize(radius, level) * 0.75;
    }
}
