using System;
using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Planets;
using OrbitalAnte.Space;

namespace OrbitalAnte.Bases
{
    public enum BaseSize { Small, Medium, Large }

    /// <summary>The dials of how many bases a world has and what ground they will accept.</summary>
    [Serializable]
    public sealed class BaseFieldRules
    {
        /// <summary>One base for every this many square metres of a world's surface.</summary>
        public float areaPerBase = 90000f;
        public int leastBases = 1, mostBases = 24;
        /// <summary>Worlds smaller than this (metres of radius) have no base: there is no room to land beside one.</summary>
        public float leastRadius = 60f;
        /// <summary>Bases are never nearer to each other than this over the ground, in metres.</summary>
        public float spacing = 180f;
        /// <summary>None nearer the launch pad than this, in metres.</summary>
        public float padClear = 260f;
        /// <summary>The radius of level ground each size stands on, in metres.</summary>
        public float smallFootprint = 10f, mediumFootprint = 18f, largeFootprint = 30f;
        /// <summary>The most the ground may rise and fall under each size before the place is refused, in metres.</summary>
        public float smallRoughness = 4f, mediumRoughness = 6f, largeRoughness = 9f;
        /// <summary>How often each size turns up, against the others.</summary>
        public float smallShare = 5f, mediumShare = 3f, largeShare = 1f;
        /// <summary>How many stand guard at each size.</summary>
        public int smallGuards = 4, mediumGuards = 6, largeGuards = 8;
        /// <summary>The base floor stands this far above the highest ground it covers, in metres.</summary>
        public float clearance = 1f;
        /// <summary>And at least this far above the sea, in metres.</summary>
        public float dryMargin = 2.5f;

        public float Footprint(BaseSize size) => size == BaseSize.Large ? largeFootprint : size == BaseSize.Medium ? mediumFootprint : smallFootprint;
        public float Roughness(BaseSize size) => size == BaseSize.Large ? largeRoughness : size == BaseSize.Medium ? mediumRoughness : smallRoughness;
        public int Guards(BaseSize size) => size == BaseSize.Large ? largeGuards : size == BaseSize.Medium ? mediumGuards : smallGuards;
    }

    /// <summary>One base's place on a world: a level floor with its own east, up and north, like the launch pad's.</summary>
    public struct BaseSite
    {
        public BaseSize Size;
        /// <summary>Which design of that size (the caller says how many there are to choose from).</summary>
        public int Kind;
        public int Seed;
        /// <summary>The direction of the middle of the floor from the body's centre.</summary>
        public Vec3d Up;
        public Vec3d North;
        public Vec3d East;
        /// <summary>How far the floor is from the body's centre, metres.</summary>
        public double FloorRadius;
        /// <summary>The radius of the floor, metres.</summary>
        public double Footprint;
        /// <summary>How far the lowest ground under the floor is below it: how deep the foundation has to reach.</summary>
        public double Depth;
        /// <summary>How much the ground rose and fell under the floor before the base was put there.</summary>
        public double Roughness;

        public Vec3d Centre => Up * FloorRadius;
        /// <summary>A place given in the base's own east, up and north, as metres from the body's centre.</summary>
        public Vec3d ToBody(double x, double y, double z) => Centre + East * x + Up * y + North * z;
    }

    /// <summary>
    /// Where the bases are on one world, for one seed. The ground is never reshaped (a world's terrain is fixed
    /// once built): a base looks for the most level dry ground it can find and stands on its own foundation,
    /// which reaches down to the lowest ground beneath it, exactly as the launch pad does. Plain C#, no engine.
    /// </summary>
    public static class BaseField
    {
        public static int BaseCount(BaseFieldRules rules, double radius)
        {
            if (radius < rules.leastRadius) return 0;
            double area = 4 * Math.PI * radius * radius;
            int wanted = (int)Math.Round(area / Math.Max(1000f, rules.areaPerBase));
            return Math.Max(rules.leastBases, Math.Min(rules.mostBases, wanted));
        }

        /// <param name="kinds">How many designs there are of each size (small, medium, large). Null: one of each.</param>
        /// <param name="keepClear">A direction to stay away from (the launch pad), or zero for none.</param>
        public static List<BaseSite> Build(BaseFieldRules rules, PlanetTerrain terrain, SeededRandom random, int[] kinds, Vec3d keepClear)
        {
            var sites = new List<BaseSite>();
            double radius = terrain.Radius;
            int count = BaseCount(rules, radius);
            if (count == 0) return sites;
            // On a small world the spacing asked for may not fit: never more than a third of the way round.
            double spacing = Math.Min(rules.spacing, radius * Math.PI / 3) / radius;
            double clear = keepClear.SqrMagnitude > 0.5 ? rules.padClear / radius : -1;

            for (int n = 0; n < count; n++)
            {
                // A world with three or more bases always has one big one; the rest are drawn by share.
                BaseSize size = n == 0 && count >= 3 ? BaseSize.Large : PickSize(rules, random.NextDouble());
                bool found = false;
                BaseSite best = default;
                // Look at a handful of places and keep the most level. If none will do, try one size smaller.
                for (int shrink = 0; shrink < 3 && !found; shrink++)
                {
                    double footprint = rules.Footprint(size);
                    for (int tries = 0; tries < 24; tries++)
                    {
                        Vec3d up = RandomDirection(random);
                        if (Math.Abs(up.Y) > 0.9) continue;   // not at the poles, where "north" stops meaning anything
                        if (clear > 0 && Angle(up, keepClear) < clear) continue;
                        bool crowded = false;
                        for (int i = 0; i < sites.Count && !crowded; i++) crowded = Angle(up, sites[i].Up) < spacing;
                        if (crowded) continue;
                        // No rougher than the size allows, and no rougher than the best place found so far.
                        double limit = found ? Math.Min(rules.Roughness(size), best.Roughness) : rules.Roughness(size);
                        if (!Measure(rules, terrain, up, footprint, out double low, out double high, limit)) continue;
                        double rough = high - low;
                        if (rough > rules.Roughness(size)) continue;
                        if (found && rough >= best.Roughness) continue;
                        found = true;
                        best = At(rules, terrain, up, footprint, low, high);
                        best.Size = size;
                    }
                    if (!found && size != BaseSize.Small) size = size - 1;
                    else if (!found) break;
                }
                int kindCount = kinds != null && (int)size < kinds.Length ? Math.Max(1, kinds[(int)size]) : 1;
                int kind = random.Range(0, kindCount);
                int seed = random.Next();
                if (!found) continue;
                best.Kind = kind;
                best.Seed = seed;
                sites.Add(best);
            }
            return sites;
        }

        static BaseSize PickSize(BaseFieldRules rules, double roll)
        {
            double s = Math.Max(0f, rules.smallShare), m = Math.Max(0f, rules.mediumShare), l = Math.Max(0f, rules.largeShare);
            double at = roll * (s + m + l);
            if (at < s) return BaseSize.Small;
            return at < s + m ? BaseSize.Medium : BaseSize.Large;
        }

        static BaseSite At(BaseFieldRules rules, PlanetTerrain terrain, Vec3d up, double footprint, double low, double high)
        {
            Vec3d north = (Vec3d.Up - up * Vec3d.Dot(Vec3d.Up, up)).Normalized;
            Vec3d east = Vec3d.Cross(up, north).Normalized;
            double top = Math.Max(high, terrain.HasSea ? terrain.SeaLevel + rules.dryMargin : high) + rules.clearance;
            return new BaseSite
            {
                Up = up, North = north, East = east,
                FloorRadius = terrain.Radius + top,
                Footprint = footprint,
                Depth = top - low,
                Roughness = high - low,
            };
        }

        /// <summary>
        /// The lowest and highest ground a floor of this size would cover, measured against the flat floor (so the
        /// world's curve counts). False if any of it is sea or a cave mouth.
        /// </summary>
        /// <param name="giveUpPast">Stop and say no as soon as the ground is known to rise and fall by more than this.</param>
        public static bool Measure(BaseFieldRules rules, PlanetTerrain terrain, Vec3d up, double footprint, out double low, out double high,
                                   double giveUpPast = double.MaxValue)
        {
            low = double.MaxValue;
            high = double.MinValue;
            Vec3d a = up.AnyPerpendicular().Normalized;
            Vec3d b = Vec3d.Cross(up, a).Normalized;
            // Close enough together that no bump between two looks can stand higher than the floor's clearance.
            const int Rings = 5;
            for (int ring = 0; ring <= Rings; ring++)
            {
                double distance = footprint * ring / (double)Rings;
                int count = ring == 0 ? 1 : 12;
                for (int k = 0; k < count; k++)
                {
                    double angle = k * 2.0 * Math.PI / count + ring * 0.4;
                    Vec3d flat = up * terrain.Radius + a * (Math.Cos(angle) * distance) + b * (Math.Sin(angle) * distance);
                    Vec3d direction = flat.Normalized;
                    if (terrain.InHole(direction, 12.0)) return false;
                    double height = terrain.Height(direction);
                    if (terrain.HasSea && height < terrain.SeaLevel + rules.dryMargin) return false;
                    double above = (terrain.Radius + height) * Vec3d.Dot(direction, up) - terrain.Radius;
                    if (above < low) low = above;
                    if (above > high) high = above;
                    if (high - low > giveUpPast) return false;
                }
            }
            return true;
        }

        static double Angle(Vec3d a, Vec3d b) => Math.Acos(Math.Max(-1, Math.Min(1, Vec3d.Dot(a, b))));

        static Vec3d RandomDirection(SeededRandom random)
        {
            double y = random.NextDouble() * 2 - 1, turn = random.NextDouble() * Math.PI * 2;
            double r = Math.Sqrt(Math.Max(0, 1 - y * y));
            return new Vec3d(Math.Cos(turn) * r, y, Math.Sin(turn) * r);
        }
    }
}
