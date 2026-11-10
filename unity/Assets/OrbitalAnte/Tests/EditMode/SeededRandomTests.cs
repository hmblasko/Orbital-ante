using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Core;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// The promise behind "same seed, same run": the generator gives the same numbers every time and everywhere,
    /// and each named stream is independent of the others. These run without Unity objects.
    /// </summary>
    public class SeededRandomTests
    {
        [Test]
        public void SameSeedGivesTheSameNumbers()
        {
            var a = new SeededRandom(1234);
            var b = new SeededRandom(1234);
            for (int i = 0; i < 200; i++) Assert.AreEqual(a.NextUInt64(), b.NextUInt64());
        }

        [Test]
        public void DifferentSeedsGiveDifferentNumbers()
        {
            var a = new SeededRandom(1);
            var b = new SeededRandom(2);
            int same = 0;
            for (int i = 0; i < 100; i++) if (a.Next() == b.Next()) same++;
            Assert.Less(same, 3);
        }

        /// <summary>
        /// Pins the exact sequence. If this ever fails, old seeds no longer produce the worlds they used to, and
        /// that must be a deliberate decision, never an accident.
        /// </summary>
        [Test]
        public void TheSequenceIsPinned()
        {
            var rng = SeededRandom.ForStream(20261110, "pinned.stream");
            Assert.AreEqual(7543108853204343658UL, rng.NextUInt64());
            Assert.AreEqual(4098551760057243845UL, rng.NextUInt64());
            Assert.AreEqual(10409942294790977737UL, rng.NextUInt64());
            Assert.AreEqual(13309476754707697221UL, new SeededRandom(7).NextUInt64());
        }

        [Test]
        public void StreamsDependOnlyOnSeedAndName()
        {
            var first = SeededRandom.ForStream(42, "mars.rocks");
            var unrelated = SeededRandom.ForStream(42, "arena.mannequins");
            for (int i = 0; i < 50; i++) unrelated.Next();     // another system drawing numbers...
            var second = SeededRandom.ForStream(42, "mars.rocks");
            for (int i = 0; i < 50; i++) Assert.AreEqual(first.Next(), second.Next());   // ...changes nothing here
        }

        [Test]
        public void DifferentStreamNamesGiveDifferentNumbers()
        {
            var a = SeededRandom.ForStream(42, "mars.rocks");
            var b = SeededRandom.ForStream(42, "mars.craters");
            int same = 0;
            for (int i = 0; i < 100; i++) if (a.Next() == b.Next()) same++;
            Assert.Less(same, 3);
        }

        [Test]
        public void ForkDoesNotDependOnHowMuchTheParentWasUsed()
        {
            var fresh = new SeededRandom(99);
            var used = new SeededRandom(99);
            for (int i = 0; i < 30; i++) used.Next();
            Assert.AreEqual(fresh.Fork("child").NextUInt64(), used.Fork("child").NextUInt64());
            Assert.AreEqual(fresh.Fork(5).NextUInt64(), used.Fork(5).NextUInt64());
            Assert.AreNotEqual(fresh.Fork(5).NextUInt64(), fresh.Fork(6).NextUInt64());
            Assert.AreNotEqual(fresh.Fork("a").NextUInt64(), fresh.Fork("b").NextUInt64());
        }

        [Test]
        public void IntRangeStaysInsideAndReachesBothEnds()
        {
            var rng = new SeededRandom(5);
            var seen = new HashSet<int>();
            for (int i = 0; i < 2000; i++)
            {
                int value = rng.Range(-3, 4);
                Assert.GreaterOrEqual(value, -3);
                Assert.Less(value, 4);
                seen.Add(value);
            }
            Assert.AreEqual(7, seen.Count);
            Assert.AreEqual(9, rng.Range(9, 9), "An empty range gives its start.");
            int extreme = rng.Range(int.MinValue, int.MaxValue);
            Assert.Less(extreme, int.MaxValue);
        }

        [Test]
        public void FloatsStayBetweenZeroAndOne()
        {
            var rng = new SeededRandom(11);
            double sum = 0;
            for (int i = 0; i < 5000; i++)
            {
                float f = rng.NextFloat();
                double d = rng.NextDouble();
                Assert.GreaterOrEqual(f, 0f);
                Assert.Less(f, 1f);
                Assert.GreaterOrEqual(d, 0.0);
                Assert.Less(d, 1.0);
                sum += f;
            }
            Assert.AreEqual(0.5, sum / 5000, 0.03, "Numbers should be spread evenly.");
            float ranged = rng.Range(2f, 3f);
            Assert.GreaterOrEqual(ranged, 2f);
            Assert.Less(ranged, 3f);
        }

        [Test]
        public void ChanceMatchesItsProbability()
        {
            var rng = new SeededRandom(3);
            int hits = 0;
            for (int i = 0; i < 10000; i++) if (rng.Chance(0.25)) hits++;
            Assert.AreEqual(2500, hits, 200);
            Assert.IsFalse(rng.Chance(0));
            Assert.IsTrue(rng.Chance(1));
        }

        [Test]
        public void ShuffleKeepsEveryItemAndIsRepeatable()
        {
            var a = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };
            var b = new List<int>(a);
            new SeededRandom(8).Shuffle(a);
            new SeededRandom(8).Shuffle(b);
            CollectionAssert.AreEqual(a, b);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, a);
            CollectionAssert.AreNotEqual(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, a);
        }

        [Test]
        public void PickReturnsAnItemFromTheList()
        {
            var rng = new SeededRandom(21);
            var items = new[] { "rock", "mesa", "hoodoo" };
            var seen = new HashSet<string>();
            for (int i = 0; i < 100; i++) seen.Add(rng.Pick(items));
            Assert.AreEqual(3, seen.Count);
            Assert.Throws<System.ArgumentException>(() => rng.Pick(new string[0]));
        }
    }
}
