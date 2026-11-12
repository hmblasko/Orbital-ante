using System;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;

namespace OrbitalAnte.Launch
{
    /// <summary>Where the launch base stands on its planet. Directions are in the planet's own turning frame (north pole is +Y).</summary>
    public struct LaunchSpot
    {
        /// <summary>Unit direction from the planet's centre to the middle of the pad.</summary>
        public Vec3d Up;
        /// <summary>Unit direction along the ground toward the north pole: the way the ship's nose points on the pad.</summary>
        public Vec3d North;
        /// <summary>Unit direction along the ground to the east (the pilot's right).</summary>
        public Vec3d East;
        /// <summary>Distance from the planet's centre to the top of the pad, in metres.</summary>
        public double PadRadius;
        /// <summary>How far the highest ground under the base stands above the lowest, in metres.</summary>
        public double Roughness;
        /// <summary>How far the pad's top stands above the lowest ground under the base, in metres.</summary>
        public double MoundHeight;

        /// <summary>The middle of the pad's top, in metres from the planet's centre.</summary>
        public Vec3d PadCentre => Up * PadRadius;

        /// <summary>A point given in the pad's own space (x east, y up, z north; origin the middle of the pad's top).</summary>
        public Vec3d ToBody(double x, double y, double z) => PadCentre + East * x + Up * y + North * z;
    }

    /// <summary>
    /// Chooses the spot for the launch base. The planet's ground is made from the run's seed, so the spot cannot
    /// be fixed by hand: this looks round a preferred place (roughly where Cape Canaveral is on the real Earth)
    /// for the most level piece of dry land. The same ground always gives the same spot. Plain C#, safe on any thread.
    /// </summary>
    public static class LaunchSite
    {
        /// <summary>Cape Canaveral, near enough: 28.5 degrees north, 80.6 degrees west.</summary>
        public const double PreferredLatitude = 28.5;
        public const double PreferredLongitude = -80.6;
        /// <summary>The base and its mound fit inside this distance from the pad's centre, in metres.</summary>
        public const double Footprint = 30.0;
        /// <summary>The pad's top stands at least this far above the highest ground it covers, in metres.</summary>
        public const double Clearance = 0.8;
        /// <summary>How far above the sea the lowest ground under the base must be, in metres.</summary>
        public const double DryMargin = 2.5;

        public static Vec3d Direction(double latitudeDegrees, double longitudeDegrees)
        {
            double lat = latitudeDegrees * Math.PI / 180.0, lon = longitudeDegrees * Math.PI / 180.0;
            return new Vec3d(Math.Cos(lat) * Math.Cos(lon), Math.Sin(lat), Math.Cos(lat) * Math.Sin(lon));
        }

        public static LaunchSpot Find(PlanetTerrain terrain)
        {
            Vec3d preferred = Direction(PreferredLatitude, PreferredLongitude);
            Vec3d best = preferred;
            double bestScore = double.MaxValue;
            // Rings of candidates spreading out from the preferred place: about 25 m apart on the ground.
            double step = 25.0 / terrain.Radius;
            Vec3d side = Vec3d.Cross(Vec3d.Up, preferred).Normalized;
            for (int ring = 0; ring <= 40; ring++)
            {
                int count = ring == 0 ? 1 : ring * 6;
                Vec3d tilted = preferred.RotatedAround(side, ring * step);
                for (int k = 0; k < count; k++)
                {
                    Vec3d candidate = tilted.RotatedAround(preferred, k * 2.0 * Math.PI / count).Normalized;
                    // Not at the poles, where "north" stops meaning anything.
                    if (Math.Abs(candidate.Y) > 0.9) continue;
                    if (!Measure(terrain, candidate, out double low, out double high)) continue;
                    // Level ground first; among equals, the nearer to the preferred place.
                    double score = (high - low) + ring * 0.15;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                    }
                }
                // Good enough, close enough: stop looking.
                if (bestScore < 3.0 + ring * 0.15) break;
            }
            return At(terrain, best);
        }

        /// <summary>The spot description for a given direction, whatever the ground there is like.</summary>
        public static LaunchSpot At(PlanetTerrain terrain, Vec3d up)
        {
            up = up.Normalized;
            Measure(terrain, up, out double low, out double high);
            Vec3d north = (Vec3d.Up - up * Vec3d.Dot(Vec3d.Up, up)).Normalized;
            // x = cross(y, z), so the three make an ordinary set of axes for the pad (x east, y up, z north).
            Vec3d east = Vec3d.Cross(up, north).Normalized;
            // The pad is flat and the planet is round: at the edge of the footprint the ground has curved away
            // by this much, which the mound has to make up as well.
            double top = Math.Max(high, terrain.HasSea ? terrain.SeaLevel + DryMargin : high) + Clearance;
            return new LaunchSpot
            {
                Up = up,
                North = north,
                East = east,
                PadRadius = terrain.Radius + top,
                Roughness = high - low,
                MoundHeight = top - low,
            };
        }

        /// <summary>
        /// Looks at the ground under a would-be base. Gives the lowest and highest ground it would cover, measured
        /// against the flat pad (so the planet's curve counts), and says no if any of it is sea or a cave mouth.
        /// </summary>
        static bool Measure(PlanetTerrain terrain, Vec3d up, out double low, out double high)
        {
            low = double.MaxValue;
            high = double.MinValue;
            Vec3d a = up.AnyPerpendicular().Normalized;
            Vec3d b = Vec3d.Cross(up, a).Normalized;
            bool usable = true;
            for (int ring = 0; ring <= 3; ring++)
            {
                double distance = Footprint * ring / 3.0;
                int count = ring == 0 ? 1 : 8;
                for (int k = 0; k < count; k++)
                {
                    double angle = k * 2.0 * Math.PI / count + ring * 0.4;
                    Vec3d flat = up * terrain.Radius + a * (Math.Cos(angle) * distance) + b * (Math.Sin(angle) * distance);
                    Vec3d direction = flat.Normalized;
                    double height = terrain.Height(direction);
                    // Height of that ground above the flat plane through the surface radius at the centre.
                    double above = (terrain.Radius + height) * Vec3d.Dot(direction, up) - terrain.Radius;
                    if (above < low) low = above;
                    if (above > high) high = above;
                    if (terrain.HasSea && height < terrain.SeaLevel + DryMargin) usable = false;
                    if (terrain.InHole(direction, 12.0)) usable = false;
                }
            }
            return usable;
        }
    }
}
