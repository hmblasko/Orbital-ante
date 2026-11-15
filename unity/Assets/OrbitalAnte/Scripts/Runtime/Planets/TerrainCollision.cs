using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Planets
{
    /// <summary>Ground coverage and cave volumes, independent of terrain height and Unity objects.</summary>
    public static class TerrainCollision
    {
        /// <summary>
        /// True for the one tile containing a point's direction. Cube-face seams and tile edges have one
        /// canonical owner, so the supporting branch can be followed without comparing terrain heights.
        /// </summary>
        public static bool ContainsDirection(int face, int level, int x, int y, Vec3d point)
        {
            if (!ValidTile(face, level, x, y) || !TryDirection(point, out Vec3d direction)) return false;
            CubeSphere.Locate(direction, out int locatedFace, out double u, out double v);
            if (locatedFace != face) return false;
            int side = 1 << level;
            int locatedX = Math.Min(side - 1, Math.Max(0, (int)Math.Floor((u + 1) * 0.5 * side)));
            int locatedY = Math.Min(side - 1, Math.Max(0, (int)Math.Floor((v + 1) * 0.5 * side)));
            return x == locatedX && y == locatedY;
        }

        /// <summary>
        /// Approximate distance to a tile's footprint on the reference sphere. Elevation is deliberately left
        /// out: a hill or a deep valley must not move the ground beneath someone outside its collision range.
        /// A zero point has no direction and returns positive infinity.
        /// </summary>
        public static double FootprintDistance(double radius, int face, int level, int x, int y, Vec3d point)
        {
            if (!Finite(radius) || radius <= 0 || !ValidTile(face, level, x, y)
                || !TryDirection(point, out Vec3d direction)) return double.PositiveInfinity;
            Vec3d centre = TerrainMesher.TileCentre(face, level, x, y);
            return Vec3d.Distance(centre * radius, direction * radius) - TileRules.Reach(radius, level);
        }

        /// <summary>
        /// True within a finite mouth funnel or the tube between its circles. Mouths use their real endpoint
        /// positions, never a projection to the point's radius, so ground far below a cave is not part of it.
        /// </summary>
        public static bool InsideCave(Cave cave, Vec3d point, double margin)
        {
            if (cave == null || cave.Centres == null || cave.Centres.Length == 0 || !Finite(point)
                || !Finite(margin)) return false;
            margin = Math.Max(0, margin);
            Vec3d[] centres = cave.Centres;
            if (cave.Mouths != null && cave.Mouths.Length > 0 && Finite(cave.MouthRadius))
            {
                double reach = Math.Max(0, cave.MouthRadius) + CaveMesher.CollarReach + margin;
                if (InSphere(point, centres[0], reach)) return true;
                if (cave.Mouths.Length > 1 && InSphere(point, centres[centres.Length - 1], reach)) return true;
            }

            if (cave.Radii == null) return false;
            int count = Math.Min(centres.Length, cave.Radii.Length);
            for (int k = 0; k < count; k++)
            {
                if (!Finite(cave.Radii[k]) || !Finite(centres[k])) continue;
                double radius = Math.Max(0, cave.Radii[k]) * 1.2 + margin;
                if (InSphere(point, centres[k], radius)) return true;
                if (k + 1 >= count || !Finite(cave.Radii[k + 1]) || !Finite(centres[k + 1])) continue;
                Vec3d along = centres[k + 1] - centres[k];
                double lengthSquared = along.SqrMagnitude;
                double t = lengthSquared > 0 ? Vec3d.Dot(point - centres[k], along) / lengthSquared : 0;
                t = Math.Max(0, Math.Min(1, t));
                double reach = Math.Max(Math.Max(0, cave.Radii[k]), Math.Max(0, cave.Radii[k + 1])) * 1.2 + margin;
                if (InSphere(point, centres[k] + along * t, reach)) return true;
            }
            return false;
        }

        static bool ValidTile(int face, int level, int x, int y)
        {
            if (face < 0 || face >= CubeSphere.Faces || level < 0 || level > 30) return false;
            int side = 1 << level;
            return x >= 0 && x < side && y >= 0 && y < side;
        }

        static bool TryDirection(Vec3d point, out Vec3d direction)
        {
            direction = Vec3d.Zero;
            if (!Finite(point)) return false;
            double scale = Math.Max(Math.Abs(point.X), Math.Max(Math.Abs(point.Y), Math.Abs(point.Z)));
            if (scale == 0) return false;
            direction = (point / scale).Normalized;
            return true;
        }

        static bool InSphere(Vec3d point, Vec3d centre, double radius)
        {
            return Finite(centre) && (point - centre).SqrMagnitude <= radius * radius;
        }

        static bool Finite(Vec3d point) => Finite(point.X) && Finite(point.Y) && Finite(point.Z);
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
