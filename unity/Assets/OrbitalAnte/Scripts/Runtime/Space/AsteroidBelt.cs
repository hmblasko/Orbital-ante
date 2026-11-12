using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Space
{
    /// <summary>
    /// Makes the belt of drifting rocks from the run seed. Each rock is an ordinary body on its own orbit round
    /// the Sun, so it moves, tumbles, pulls (very weakly) and can be landed on like anything else.
    /// </summary>
    public static class AsteroidBelt
    {
        public static List<BodySpec> Generate(int seed, AsteroidBeltSpec belt, string sunId)
        {
            var rocks = new List<BodySpec>();
            if (belt == null || belt.count <= 0) return rocks;
            var rng = SeededRandom.ForStream(seed, "solar.asteroids");
            for (int i = 0; i < belt.count; i++)
            {
                // One generator per rock, so changing the count never reshuffles the rocks that already exist.
                SeededRandom r = rng.Fork(i);
                double size = belt.minSize + (belt.maxSize - belt.minSize) * r.NextDouble() * r.NextDouble();
                rocks.Add(new BodySpec
                {
                    id = "Asteroid" + (i + 1).ToString("000"),
                    displayName = "Asteroid " + (i + 1),
                    kind = BodyKind.Asteroid,
                    parentId = sunId,
                    radius = size,
                    // Far more than a real rock this size, so you can land on one and stay put.
                    surfaceGravity = 0.15 + size * 0.006,
                    orbitRadius = belt.innerRadius + (belt.outerRadius - belt.innerRadius) * r.NextDouble(),
                    orbitInclination = (r.NextDouble() * 2.0 - 1.0) * belt.maxInclination,
                    orbitNode = r.NextDouble() * 360.0,
                    dayLength = belt.minDayLength + (belt.maxDayLength - belt.minDayLength) * r.NextDouble(),
                    axialTilt = r.NextDouble() * 180.0,
                    retrogradeSpin = r.Chance(0.5),
                    colorA = r.Chance(0.5) ? "#6E655C" : "#857A6C",
                    colorB = "#4E4740",
                });
            }
            return rocks;
        }
    }
}
