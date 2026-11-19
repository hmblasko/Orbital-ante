using System;
using System.Collections.Generic;
using OrbitalAnte.Core;

namespace OrbitalAnte.Planets
{
    /// <summary>The dials for felling trees. Plain numbers, no Unity types.</summary>
    [Serializable]
    public sealed class FellingRules
    {
        /// <summary>Toughness: a base amount plus so much per square metre of the trunk's cross-section, with a ceiling.</summary>
        public float baseToughness = 40f, toughnessPerSquareMetre = 600f, mostToughness = 2000f;
        /// <summary>A heavy thing driving into a trunk: nothing below this speed (m/s straight in), then so much per (m/s over)^2 per tonne.</summary>
        public float ramSafeSpeed = 4f, ramDamage = 4f;
        /// <summary>How hard the last blow tips the top over, in radians per second, and the most a falling top may weigh, kg.</summary>
        public float tipSpin = 0.5f, mostMass = 4000f;
        /// <summary>A fallen top comes to rest (and stops costing physics) after this long, seconds.</summary>
        public float settleSeconds = 10f;
        /// <summary>The most tops falling at once; the oldest is made still when another tree comes down.</summary>
        public int mostFalling = 12;

        /// <summary>How much damage a trunk of this radius (metres) takes before the tree comes down.</summary>
        public float Toughness(float trunkRadius)
        {
            float r = Math.Max(0.02f, trunkRadius);
            return Math.Min(mostToughness, baseToughness + toughnessPerSquareMetre * (float)Math.PI * r * r);
        }

        public float RamDamage(float speed, float mass)
        {
            float over = speed - ramSafeSpeed;
            if (over <= 0f || mass <= 0f) return 0f;
            return ramDamage * over * over * (mass / 1000f);
        }

        /// <summary>The weight of a falling top: wood is about 600 kg a cubic metre; the trunk is most of it.</summary>
        public float TopMass(float trunkRadius, float height)
        {
            double trunk = Math.PI * trunkRadius * trunkRadius * Math.Max(0.5f, height) * 0.5;
            return (float)Math.Min(mostMass, Math.Max(40.0, trunk * 600.0));
        }
    }

    /// <summary>
    /// Which trees on one world have been felled this run, by where they stood (to the centimetre), so a tree
    /// shown again after the ground near it was put away comes back as its stump. Plain C#.
    /// </summary>
    public sealed class FelledTrees
    {
        readonly HashSet<long> _felled = new HashSet<long>();

        public static long Key(Vec3d position)
        {
            unchecked
            {
                long h = (long)Math.Round(position.X * 100.0);
                h = h * 1000003L ^ (long)Math.Round(position.Y * 100.0);
                h = h * 1000003L ^ (long)Math.Round(position.Z * 100.0);
                return h;
            }
        }

        public bool IsFelled(Vec3d position) => _felled.Contains(Key(position));
        public void Fell(Vec3d position) => _felled.Add(Key(position));
        public int Count => _felled.Count;
    }
}
