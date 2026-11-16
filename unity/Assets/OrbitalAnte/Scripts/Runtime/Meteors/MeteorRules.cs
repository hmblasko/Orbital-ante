using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Meteors
{
    /// <summary>
    /// Meteor strikes (Matthew, 16 Nov 2026: "Have random meteors hit planets (somewhat regularly but not too
    /// regularly). Have them break naturally into pieces in a satisfying way and leave impacts and explode").
    /// All the dials, on the MeteorConfig asset. Plain C#, no engine types.
    /// </summary>
    [Serializable]
    public sealed class MeteorRules
    {
        /// <summary>How often one world is struck, compared with the others. 1 is the usual rate, 0 never.</summary>
        [Serializable]
        public struct BodyRate
        {
            public string body;
            public float rate;
        }

        public bool enabled = true;

        // ------------------------------------------------------------------ when
        /// <summary>No strike in the first seconds of a run: the first minute is the player's.</summary>
        public float graceSeconds = 90f;
        /// <summary>On arriving at a world (landing, or flying down to it), at least this long before its first strike.</summary>
        public float arrivalGrace = 45f;
        /// <summary>The shortest gap between two strikes near the player, in seconds.</summary>
        public float minGap = 45f;
        /// <summary>The average gap. Gaps are drawn so most are near this, a few short and a few long.</summary>
        public float meanGap = 110f;
        /// <summary>The longest gap: a quiet spell never lasts longer than this.</summary>
        public float maxGap = 240f;
        /// <summary>Per-world rates. A world not listed has rate 1. Airless worlds are hit more often (nothing burns
        /// the small ones up), tiny moons less (they are small targets).</summary>
        public BodyRate[] rates =
        {
            new BodyRate { body = "Earth", rate = 0.8f },
            new BodyRate { body = "Moon", rate = 1.3f },
            new BodyRate { body = "Mercury", rate = 1.3f },
            new BodyRate { body = "Mars", rate = 1.1f },
            new BodyRate { body = "Phobos", rate = 0.7f },
            new BodyRate { body = "Deimos", rate = 0.7f },
        };
        /// <summary>The clock only runs while the player is this close above the ground, in metres.</summary>
        public float maxAltitude = 600f;

        // ------------------------------------------------------------------ where
        /// <summary>Where most strikes land, measured along the ground from the player, in metres.</summary>
        public float minDistance = 45f;
        public float maxDistance = 240f;
        /// <summary>A near miss now and then, close enough to feel, outside the blast unless it is a big one.</summary>
        public float nearMissChance = 0.12f;
        public float nearMissMin = 22f;
        public float nearMissMax = 34f;
        /// <summary>The chance a strike is aimed at a group of enemies, when there are any in range.</summary>
        public float enemyAimChance = 0.2f;
        /// <summary>How far from the chosen enemy it lands, at most, in metres.</summary>
        public float enemyAimSpread = 6f;
        /// <summary>A strike aimed at enemies never lands nearer the player than this.</summary>
        public float safeFromPlayer = 24f;
        /// <summary>Nothing lands this close to the launch pad, a base's edge or the lander, in metres.</summary>
        public float padClear = 70f;
        public float baseClear = 10f;
        public float shipClear = 25f;

        // ------------------------------------------------------------------ the rock
        /// <summary>The radius of the crater the whole rock would make, in metres. Small ones are commoner.</summary>
        public float minCrater = 2.2f;
        public float maxCrater = 5.5f;
        /// <summary>Speed coming in, in metres a second (slowed to something you can watch, not the real 20 km/s).</summary>
        public float entrySpeedMin = 150f;
        public float entrySpeedMax = 230f;
        /// <summary>Angle of the path above the horizon, in degrees.</summary>
        public float entryAngleMin = 30f;
        public float entryAngleMax = 65f;
        /// <summary>How high above the ground it is first drawn, in metres (a few seconds of warning).</summary>
        public float startAltitude = 650f;
        /// <summary>The fall from first sight to the ground never takes less than this, in seconds.</summary>
        public float warningSeconds = 4.5f;
        /// <summary>How quickly air slows it, per second (0 in vacuum).</summary>
        public float airDrag = 0.06f;

        // ------------------------------------------------------------------ breaking up
        /// <summary>The chance the rock bursts apart in the air on a world with air. Airless worlds have no air to
        /// break it, so their rocks come down whole and shatter on the ground instead.</summary>
        public float airBreakupChance = 0.8f;
        /// <summary>Height of the burst above the ground, in metres.</summary>
        public float breakupAltitudeMin = 120f;
        public float breakupAltitudeMax = 320f;
        public int minPieces = 3;
        public int maxPieces = 5;
        /// <summary>The most pieces in the air from one rock, counting second bursts.</summary>
        public int maxTotalPieces = 7;
        /// <summary>The chance the biggest piece bursts again, lower down.</summary>
        public float secondBreakChance = 0.4f;
        /// <summary>How fast the pieces fly apart, in metres a second.</summary>
        public float spreadSpeedMin = 5f;
        public float spreadSpeedMax = 8f;
        /// <summary>Of the rock's mass, how much is lost as flame and dust in a burst.</summary>
        public float burstLoss = 0.35f;
        /// <summary>No piece makes a crater smaller than this, in metres.</summary>
        public float smallestCrater = 0.9f;

        // ------------------------------------------------------------------ the blast
        /// <summary>The blast reaches this many crater radii. Inside the crater itself everything is torn apart.</summary>
        public float blastPerCrater = 2.6f;
        /// <summary>Of the crater's radius, how far the lethal core reaches.</summary>
        public float lethalCore = 0.6f;
        /// <summary>Damage to one body part at the edge of the lethal core (enemy limbs come off at much less).</summary>
        public float partDamage = 120f;
        /// <summary>Damage to the player's suit at the edge of the lethal core (the suit holds 100).</summary>
        public float playerDamage = 140f;
        /// <summary>Damage to one part of the lander at the edge of the lethal core.</summary>
        public float shipDamage = 60f;
        /// <summary>Speed given to loose things (limbs, guns, the rover) at the edge of the lethal core.</summary>
        public float knockSpeed = 11f;
        /// <summary>The view shakes out to this many crater radii.</summary>
        public float shakePerCrater = 28f;

        // ------------------------------------------------------------------ what it leaves
        /// <summary>The most craters kept on all worlds together; the oldest goes first.</summary>
        public int maxCraters = 24;
        /// <summary>Rim height as a share of the crater's radius.</summary>
        public float rimHeight = 0.17f;
        /// <summary>How far the thrown-out ground reaches, in crater radii.</summary>
        public float apron = 2.4f;
        /// <summary>How long a crater's floor glows, in seconds.</summary>
        public float glowSeconds = 25f;
        /// <summary>Rocks thrown out per crater metre of radius, and the most lying about at once.</summary>
        public float chunksPerMetre = 1.6f;
        public int maxChunks = 30;

        public float RateFor(string body)
        {
            if (rates != null)
                for (int i = 0; i < rates.Length; i++)
                    if (string.Equals(rates[i].body, body, StringComparison.OrdinalIgnoreCase)) return Math.Max(0f, rates[i].rate);
            return 1f;
        }
    }

    /// <summary>When the next strike comes.</summary>
    public static class MeteorClock
    {
        /// <summary>
        /// Seconds to the next strike for a fair roll from 0 to 1. A fixed shortest gap, then a memoryless wait
        /// (so the player cannot count down to it), clipped to the longest gap, then divided by the world's rate.
        /// Returns infinity for a rate of 0.
        /// </summary>
        public static double NextGap(MeteorRules r, double roll, float rate)
        {
            if (!(rate > 0f)) return double.PositiveInfinity;
            double min = Math.Max(1.0, r.minGap);
            double mean = Math.Max(min, r.meanGap);
            double max = Math.Max(mean, r.maxGap);
            double u = Math.Min(Math.Max(roll, 1e-9), 1 - 1e-9);
            double gap = min + (mean - min) * -Math.Log(1 - u);
            if (gap > max) gap = max;
            return gap / rate;
        }
    }

    public enum AimKind { Wide, NearMiss, AtEnemies }

    /// <summary>Where a strike lands.</summary>
    public static class MeteorAim
    {
        /// <summary>
        /// Picks a direction from the body's centre for the strike. <paramref name="player"/> is the unit direction to
        /// the player, <paramref name="enemies"/> unit directions to live enemies, <paramref name="allowed"/> says
        /// whether a direction may be struck (not the pad, a base, the lander or the sea). False if nowhere fits.
        /// </summary>
        public static bool Pick(MeteorRules r, SeededRandom rng, Vec3d player, double radius, IReadOnlyList<Vec3d> enemies,
                                Func<Vec3d, bool> allowed, out Vec3d target, out AimKind kind)
        {
            target = player;
            kind = AimKind.Wide;
            if (radius <= 1) return false;
            double maxArc = Math.Min(r.maxDistance, radius * 1.2);
            double minArc = Math.Min(r.minDistance, maxArc * 0.5);

            for (int attempt = 0; attempt < 8; attempt++)
            {
                if (enemies != null && enemies.Count > 0 && rng.Chance(r.enemyAimChance))
                {
                    Vec3d at = enemies[rng.Range(0, enemies.Count)];
                    if (ArcDistance(player, at, radius) < r.safeFromPlayer + r.enemyAimSpread) continue;
                    double spread = r.enemyAimSpread * Math.Sqrt(rng.NextDouble());
                    target = Along(at, rng.NextDouble() * Math.PI * 2, spread / radius);
                    if (ArcDistance(player, target, radius) < r.safeFromPlayer) continue;
                    kind = AimKind.AtEnemies;
                }
                else if (rng.Chance(r.nearMissChance))
                {
                    double d = Lerp(r.nearMissMin, r.nearMissMax, rng.NextDouble());
                    target = Along(player, rng.NextDouble() * Math.PI * 2, Math.Min(d, maxArc) / radius);
                    kind = AimKind.NearMiss;
                }
                else
                {
                    // Even over the ring's area, so far strikes are commoner than near ones, as they would be.
                    double d = Math.Sqrt(Lerp(minArc * minArc, maxArc * maxArc, rng.NextDouble()));
                    target = Along(player, rng.NextDouble() * Math.PI * 2, d / radius);
                    kind = AimKind.Wide;
                }
                if (allowed == null || allowed(target)) return true;
            }
            return false;
        }

        /// <summary>The direction an angle <paramref name="arc"/> (radians) away from <paramref name="from"/>, toward a bearing.</summary>
        public static Vec3d Along(Vec3d from, double bearing, double arc)
        {
            Vec3d up = from.Normalized;
            Vec3d tangent = up.AnyPerpendicular().RotatedAround(up, bearing);
            return (up * Math.Cos(arc) + tangent * Math.Sin(arc)).Normalized;
        }

        /// <summary>Distance along the ground between two directions on a ball of this radius.</summary>
        public static double ArcDistance(Vec3d a, Vec3d b, double radius)
        {
            double c = Vec3d.Dot(a.Normalized, b.Normalized);
            return Math.Acos(c < -1 ? -1 : c > 1 ? 1 : c) * radius;
        }

        internal static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }

    /// <summary>What the blast does at a distance from the point of impact.</summary>
    public static class BlastRules
    {
        /// <summary>How far the blast reaches, in metres.</summary>
        public static double Reach(MeteorRules r, double crater) => crater * Math.Max(1.0, r.blastPerCrater);

        /// <summary>1 inside the lethal core, falling smoothly to 0 at the blast's reach.</summary>
        public static double Strength(MeteorRules r, double crater, double distance)
        {
            double core = crater * Math.Max(0.1, r.lethalCore);
            double reach = Reach(r, crater);
            if (distance <= core) return 1;
            if (distance >= reach || reach <= core) return 0;
            double x = 1 - (distance - core) / (reach - core);
            return x * x;
        }

        /// <summary>True inside the lethal core: anyone there is killed outright.</summary>
        public static bool Lethal(MeteorRules r, double crater, double distance) => distance <= crater * Math.Max(0.1, r.lethalCore);

        public static float PartDamage(MeteorRules r, double crater, double distance) => (float)(r.partDamage * Strength(r, crater, distance));

        /// <summary>The suit's damage: gentler at the edge than a body part's, so the edge of a blast hurts, never kills.</summary>
        public static float PlayerDamage(MeteorRules r, double crater, double distance)
        {
            double s = Strength(r, crater, distance);
            return (float)(r.playerDamage * Math.Pow(s, 1.5));
        }

        public static float ShipDamage(MeteorRules r, double crater, double distance) => (float)(r.shipDamage * Strength(r, crater, distance));

        public static float Knock(MeteorRules r, double crater, double distance) => (float)(r.knockSpeed * Math.Sqrt(Strength(r, crater, distance)));

        /// <summary>How hard the view shakes, 0 to 1. Bigger rocks shake further and harder.</summary>
        public static float Shake(MeteorRules r, double crater, double distance)
        {
            double reach = crater * Math.Max(1.0, r.shakePerCrater);
            if (distance >= reach) return 0f;
            double x = 1 - distance / reach;
            double size = Math.Min(1.0, crater / 4.0);
            return (float)Math.Min(1.0, x * x * (0.35 + 0.65 * size));
        }

        /// <summary>Seconds before the bang is heard: sound in air is slow (343 m/s). In vacuum it comes through the
        /// ground, far faster, and arrives as a thump.</summary>
        public static double SoundDelay(double distance, bool air) => Math.Max(0, distance) / (air ? 343.0 : 2500.0);
    }

    /// <summary>The rocks thrown out of a crater.</summary>
    public static class EjectaRules
    {
        public static int Count(MeteorRules r, double crater) => Math.Max(1, (int)Math.Round(crater * Math.Max(0, r.chunksPerMetre)));

        /// <summary>A thrown rock's size across, in metres.</summary>
        public static double Size(double crater, double roll) => (0.18 + 0.5 * roll * roll) * Math.Max(0.5, crater / 4.0);

        /// <summary>Launch speed, in metres a second: bigger blasts throw further, slow worlds keep them up longer.</summary>
        public static double Speed(double crater, double roll) => (7 + 14 * roll) * Math.Sqrt(Math.Max(0.3, crater / 3.0));

        /// <summary>Angle above the ground, in degrees: thrown out in a cone, never flat along it.</summary>
        public static double Angle(double roll) => 35 + 35 * roll;
    }
}
