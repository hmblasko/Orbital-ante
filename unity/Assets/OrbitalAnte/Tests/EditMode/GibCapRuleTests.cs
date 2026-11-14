using System.Collections.Generic;
using NUnit.Framework;
using OrbitalAnte.Dismemberment;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// The loose-chunk cap. maxActiveGibs is the most live chunks (physics bodies) at once, fading ones included.
    /// Found by the Codex Deck audit (PR #18, finding 1): a burst used to grow past the cap because fading chunks
    /// were not counted.
    /// </summary>
    public class GibCapRuleTests
    {
        /// <summary>Plays the manager's spawn sequence on plain data: make room, then add a solid chunk.</summary>
        static void Spawn(List<GibCapRule.Slot> slots, int cap, int fadeSlots, int keep = -1)
        {
            GibCapRule.MakeRoom(slots, cap, fadeSlots, keep, i => slots.RemoveAt(i), i =>
            {
                var s = slots[i];
                s.Fading = true;
                s.FadeProgress = 0f;
                slots[i] = s;
            });
            slots.Add(new GibCapRule.Slot());
        }

        static int Count(List<GibCapRule.Slot> slots, bool fading)
        {
            int n = 0;
            foreach (var s in slots) if (s.Fading == fading) n++;
            return n;
        }

        [Test]
        public void Burst_NeverExceedsCapInLiveChunks()
        {
            var slots = new List<GibCapRule.Slot>();
            for (int i = 0; i < 500; i++)
            {
                Spawn(slots, 40, 4);
                Assert.LessOrEqual(slots.Count, 40, "live chunks after spawn " + i);
            }
        }

        [Test]
        public void BelowCap_NothingFadesOrIsRemoved()
        {
            var slots = new List<GibCapRule.Slot>();
            for (int i = 0; i < 36; i++) Spawn(slots, 40, 4);
            Assert.AreEqual(36, slots.Count);
            Assert.AreEqual(0, Count(slots, true));
        }

        [Test]
        public void NearCap_OldestSolidStartsFadingFirst_SoOverflowStaysGraceful()
        {
            var slots = new List<GibCapRule.Slot>();
            for (int i = 0; i < 37; i++) Spawn(slots, 40, 4);
            Assert.AreEqual(37, slots.Count);
            Assert.IsTrue(slots[0].Fading, "the oldest chunk is the one that fades");
            Assert.AreEqual(1, Count(slots, true));
        }

        [Test]
        public void AtCap_TheChunkFurthestThroughItsFadeGoesFirst()
        {
            var slots = new List<GibCapRule.Slot>();
            for (int i = 0; i < 36; i++) slots.Add(new GibCapRule.Slot());
            slots.Add(new GibCapRule.Slot { Fading = true, FadeProgress = 0.2f });
            slots.Add(new GibCapRule.Slot { Fading = true, FadeProgress = 0.9f });
            slots.Add(new GibCapRule.Slot { Fading = true, FadeProgress = 0.5f });
            slots.Add(new GibCapRule.Slot { Fading = true, FadeProgress = 0.1f });
            Spawn(slots, 40, 4);
            Assert.AreEqual(40, slots.Count);
            foreach (var s in slots) Assert.AreNotEqual(0.9f, s.FadeProgress);
        }

        [Test]
        public void AtCap_WithNothingFading_OldestSolidIsRemoved()
        {
            var slots = new List<GibCapRule.Slot>();
            for (int i = 0; i < 40; i++) slots.Add(new GibCapRule.Slot { FadeProgress = i });
            Spawn(slots, 40, 0);
            Assert.AreEqual(40, slots.Count);
            Assert.AreEqual(1f, slots[0].FadeProgress, "chunk 0 (oldest) went; chunk 1 is now first");
        }

        [TestCase(0, 0)]
        [TestCase(1, 4)]
        [TestCase(-5, 4)]
        [TestCase(3, 10)]
        public void TinyOrOddCaps_StillHoldAtLeastOneChunk(int cap, int fadeSlots)
        {
            var slots = new List<GibCapRule.Slot>();
            for (int i = 0; i < 20; i++)
            {
                Spawn(slots, cap, fadeSlots);
                Assert.LessOrEqual(slots.Count, System.Math.Max(1, cap));
                Assert.GreaterOrEqual(slots.Count, 1);
            }
        }
    
        [Test]
        public void TheChunkBeingSplit_IsNeverRemovedOrFaded()
        {
            // A flying leg is shot apart: the new chunk takes parts from the leg, so the leg must survive the spawn
            // even when it is the oldest chunk and the furthest through a fade.
            var slots = new List<GibCapRule.Slot>();
            slots.Add(new GibCapRule.Slot { Fading = true, FadeProgress = 0.99f });
            for (int i = 1; i < 40; i++) slots.Add(new GibCapRule.Slot { FadeProgress = i });
            Spawn(slots, 40, 4, keep: 0);
            Assert.AreEqual(40, slots.Count);
            Assert.AreEqual(0.99f, slots[0].FadeProgress, "the split chunk is still first");
            foreach (var s in slots) Assert.AreNotEqual(1f, s.FadeProgress, "the next oldest (chunk 1) went instead");

            var solid = new List<GibCapRule.Slot>();
            for (int i = 0; i < 37; i++) solid.Add(new GibCapRule.Slot { FadeProgress = i });
            Spawn(solid, 40, 4, keep: 0);
            Assert.IsFalse(solid[0].Fading, "the split chunk does not start fading");
            Assert.IsTrue(solid[1].Fading);
        }

        [Test]
        public void CapOfOne_SplittingTheOnlyChunk_AllowsOneOver()
        {
            var slots = new List<GibCapRule.Slot> { new GibCapRule.Slot() };
            Spawn(slots, 1, 0, keep: 0);
            Assert.AreEqual(2, slots.Count);
        }
    }
}
