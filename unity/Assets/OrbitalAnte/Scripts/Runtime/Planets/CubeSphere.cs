using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// Maps a ball onto the six faces of a cube, so a planet's surface can be cut into square tiles that are all
    /// roughly the same size. Face coordinates (u, v) run from -1 to 1. No Unity types.
    /// </summary>
    public static class CubeSphere
    {
        public const int Faces = 6;

        static readonly Vec3d[] Normal =
        {
            new Vec3d(1, 0, 0), new Vec3d(-1, 0, 0), new Vec3d(0, 1, 0), new Vec3d(0, -1, 0), new Vec3d(0, 0, 1), new Vec3d(0, 0, -1),
        };

        static readonly Vec3d[] AxisU =
        {
            new Vec3d(0, 0, 1), new Vec3d(0, 0, -1), new Vec3d(1, 0, 0), new Vec3d(1, 0, 0), new Vec3d(-1, 0, 0), new Vec3d(1, 0, 0),
        };

        static readonly Vec3d[] AxisV =
        {
            new Vec3d(0, 1, 0), new Vec3d(0, 1, 0), new Vec3d(0, 0, 1), new Vec3d(0, 0, -1), new Vec3d(0, 1, 0), new Vec3d(0, 1, 0),
        };

        const double QuarterPi = Math.PI / 4.0;

        /// <summary>The direction from the centre of the ball for a point on a face.</summary>
        public static Vec3d Direction(int face, double u, double v)
        {
            // The tangent spreads the tiles evenly; without it the ones at a face's corners would be squashed.
            double a = Math.Tan(u * QuarterPi);
            double b = Math.Tan(v * QuarterPi);
            return (Normal[face] + AxisU[face] * a + AxisV[face] * b).Normalized;
        }

        /// <summary>Which face a direction falls on, and where on it.</summary>
        public static void Locate(Vec3d direction, out int face, out double u, out double v)
        {
            double ax = Math.Abs(direction.X), ay = Math.Abs(direction.Y), az = Math.Abs(direction.Z);
            if (ax >= ay && ax >= az) face = direction.X >= 0 ? 0 : 1;
            else if (ay >= az) face = direction.Y >= 0 ? 2 : 3;
            else face = direction.Z >= 0 ? 4 : 5;
            double along = Vec3d.Dot(direction, Normal[face]);
            if (along < 1e-12) along = 1e-12;
            u = Math.Atan(Vec3d.Dot(direction, AxisU[face]) / along) / QuarterPi;
            v = Math.Atan(Vec3d.Dot(direction, AxisV[face]) / along) / QuarterPi;
        }

        /// <summary>Length of one face's edge on a ball of the given radius, in metres.</summary>
        public static double FaceSize(double radius) => radius * Math.PI * 0.5;

        public static Vec3d FromLatLon(double latitudeDegrees, double longitudeDegrees)
        {
            double lat = latitudeDegrees * Math.PI / 180.0;
            double lon = longitudeDegrees * Math.PI / 180.0;
            return new Vec3d(Math.Cos(lat) * Math.Cos(lon), Math.Sin(lat), Math.Cos(lat) * Math.Sin(lon));
        }

        /// <summary>A unit vector along the ground pointing north from the given spot.</summary>
        public static Vec3d North(Vec3d direction)
        {
            Vec3d north = Vec3d.Up - direction * Vec3d.Dot(Vec3d.Up, direction);
            if (north.SqrMagnitude < 1e-8) north = Vec3d.Right - direction * Vec3d.Dot(Vec3d.Right, direction);
            return north.Normalized;
        }

        /// <summary>A unit vector along the ground in a compass direction (radians, 0 is north).</summary>
        public static Vec3d Heading(Vec3d direction, double bearing)
        {
            Vec3d north = North(direction);
            Vec3d east = Vec3d.Cross(north, direction);
            return (north * Math.Cos(bearing) + east * Math.Sin(bearing)).Normalized;
        }
    }
}
