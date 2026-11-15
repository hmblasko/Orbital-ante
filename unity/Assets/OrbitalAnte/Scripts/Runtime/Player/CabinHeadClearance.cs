using System;
using OrbitalAnte.Ship;

namespace OrbitalAnte.Player
{
    /// <summary>
    /// Conservative head/capsule clearance against the cabin's faceted inner profile and floor-plan fittings.
    /// The outside physics hull encloses the room, so it cannot answer queries made from inside. No Unity types.
    /// </summary>
    public static class CabinHeadClearance
    {
        static readonly double FacetInset = Math.Cos(Math.PI / ShipLayout.WallColumns);
        struct ProfilePlane { public double A, B, Limit, Normal; }
        static readonly ProfilePlane[] Profile = BuildProfile();

        static ProfilePlane[] BuildProfile()
        {
            var profile = new ProfilePlane[ShipLayout.WallRows + 1];
            float r0, y0;
            ShipLayout.InnerProfile(0, out r0, out y0);
            for (int row = 1; row <= ShipLayout.WallRows + 1; row++)
            {
                float r1, y1;
                if (row < ShipLayout.WallRows) ShipLayout.InnerProfile(row, out r1, out y1);
                else
                {
                    ShipLayout.InnerProfile(ShipLayout.WallRows - 1, out float rim, out float high);
                    r1 = row == ShipLayout.WallRows ? rim * 0.5f : 0f;
                    y1 = row == ShipLayout.WallRows ? high + (ShipLayout.CeilingPeak - high) * 0.72f : ShipLayout.CeilingPeak;
                }
                double a = y1 - y0, b = (r0 - r1) * FacetInset;
                double limit = a * r0 * FacetInset + b * y0;
                profile[row - 1] = new ProfilePlane { A = a, B = b, Limit = limit, Normal = Math.Sqrt(a * a + b * b) };
                r0 = r1; y0 = y1;
            }
            return profile;
        }

        public static bool Contains(P3 point, float radius)
        {
            radius = StanceRules.NonNegative(radius);
            if (point.Y < radius - 0.00001 || point.Y + radius > ShipLayout.CeilingPeak) return false;
            double radial = Math.Sqrt(point.X * point.X + point.Z * point.Z);
            foreach (ProfilePlane plane in Profile)
                if (plane.A * radial + plane.B * point.Y + radius * plane.Normal > plane.Limit + 0.00001) return false;
            if (point.Z + radius > ShipLayout.DeskZ || point.Z - radius < ShipLayout.LockerZ) return false;
            foreach (FloorObstacle obstacle in ShipLayout.Obstacles)
            {
                double x = point.X - obstacle.X, z = point.Z - obstacle.Z, keep = obstacle.Radius + radius;
                if (x * x + z * z < keep * keep - 0.00001) return false;
            }
            return true;
        }

        public static bool Fits(P3 feet, float height, float radius) => height >= 2f * radius &&
            Contains(feet + new P3(0f, radius, 0f), radius) &&
            Contains(feet + new P3(0f, height - radius, 0f), radius);

        public static P3 Clip(P3 from, P3 desired, float radius)
        {
            if (!Contains(from, radius)) return from;
            P3 travel = desired - from;
            double maximum = 1.0;
            // Prevent crossing a fitting even when the destination emerges clear on its other side.
            foreach (FloorObstacle obstacle in ShipLayout.Obstacles)
            {
                double x = from.X - obstacle.X, z = from.Z - obstacle.Z;
                double a = travel.X * travel.X + travel.Z * travel.Z;
                double b = 2.0 * (x * travel.X + z * travel.Z), keep = obstacle.Radius + radius;
                double c = x * x + z * z - keep * keep, disc = b * b - 4.0 * a * c;
                if (a <= 0.00000001 || disc <= 0.0) continue;
                double enter = (-b - Math.Sqrt(disc)) / (2.0 * a);
                if (enter >= 0.0 && enter < maximum) maximum = Math.Max(0.0, enter - 0.00001);
            }
            // The shrunken cabin and desk/locker half-spaces are convex; bisection stays on the safe side.
            if (Contains(from + travel * (float)maximum, radius)) return from + travel * (float)maximum;
            double low = 0.0, high = maximum;
            for (int i = 0; i < 18; i++)
            {
                double middle = (low + high) * 0.5;
                if (Contains(from + travel * (float)middle, radius)) low = middle; else high = middle;
            }
            return from + travel * (float)low;
        }
    }
}
