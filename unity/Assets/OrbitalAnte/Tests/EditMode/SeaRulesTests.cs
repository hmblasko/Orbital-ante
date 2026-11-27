using System;
using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Core;
using OrbitalAnte.Creatures;

namespace OrbitalAnte.Tests.EditMode
{
    /// <summary>Where the big swimmers live: only over deep water, well apart, the same for a seed every time.</summary>
    public class SeaRulesTests
    {
        const double Radius = 900;

        // A world whose northern half is sea, deeper toward the pole.
        static double Depth(Vec3d d) => d.Y * 60.0;

        [Test]
        public void Haunts_AreOnlyOverDeepWater_AndWellApart()
        {
            var rules = new SeaFieldRules();
            List<SeaHaunt> haunts = SeaField.Build(rules, new SeededRandom(5), Radius, Depth);
            Assert.Greater(haunts.Count, 2, "a world that is half sea has several");
            Assert.LessOrEqual(haunts.Count, rules.mostHaunts);
            for (int i = 0; i < haunts.Count; i++)
            {
                Assert.AreEqual(1.0, haunts[i].Direction.Magnitude, 1e-9);
                Assert.GreaterOrEqual(Depth(haunts[i].Direction), rules.minDepth, "never over shallows or land");
                Assert.Greater(haunts[i].Under, 4.0, "its animal has water over its back");
                Assert.Less(haunts[i].Under, haunts[i].Depth - 2.0, "and under its belly");
                for (int j = 0; j < i; j++)
                {
                    double apart = Math.Acos(Math.Max(-1, Math.Min(1, Vec3d.Dot(haunts[i].Direction, haunts[j].Direction)))) * Radius;
                    Assert.GreaterOrEqual(apart, rules.spacing - 1e-6, "no two haunts crowd each other");
                }
            }
        }

        [Test]
        public void TheSameSeed_GivesTheSameHaunts_AndAnotherSeedOthers()
        {
            var rules = new SeaFieldRules();
            List<SeaHaunt> a = SeaField.Build(rules, new SeededRandom(9), Radius, Depth), b = SeaField.Build(rules, new SeededRandom(9), Radius, Depth);
            List<SeaHaunt> c = SeaField.Build(rules, new SeededRandom(10), Radius, Depth);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Direction.X, b[i].Direction.X, 0.0);
                Assert.AreEqual(a[i].Seed, b[i].Seed);
            }
            Assert.IsTrue(c.Count == 0 || a.Count == 0 || Math.Abs(a[0].Direction.X - c[0].Direction.X) > 1e-9, "another seed puts them elsewhere");
        }

        [Test]
        public void AWorldWithNoDeepWater_HasNone_AndASmallOneStillFits()
        {
            var rules = new SeaFieldRules();
            Assert.AreEqual(0, SeaField.Build(rules, new SeededRandom(3), Radius, d => 3.0).Count, "shallows everywhere: nobody");
            // A tiny ocean world: the spacing asked for cannot fit, and it still gets its least.
            List<SeaHaunt> small = SeaField.Build(rules, new SeededRandom(3), 60, d => 40.0);
            Assert.GreaterOrEqual(small.Count, rules.leastHaunts);
            Assert.AreEqual(Math.Max(5.0, Math.Min(20.0, 14.0)), SeaField.UnderFor(40.0), 1e-9);
            Assert.AreEqual(8.0, SeaField.UnderFor(16.0), 1e-9, "over the shallowest water it lives at half depth");
        }
    }
}
