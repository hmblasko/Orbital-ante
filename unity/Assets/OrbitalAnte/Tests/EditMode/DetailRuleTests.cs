using NUnit.Framework;
using OrbitalAnte.Look;

namespace OrbitalAnte.Tests
{
    /// <summary>Which copy of a model is drawn: the full one near, the far copy beyond its range, and never too many full bodies.</summary>
    public class DetailRuleTests
    {
        [Test]
        public void AThingGoesFar_BeyondItsRange_AndComesBack_InsideIt()
        {
            const float range = DetailRule.BodyRange;
            Assert.IsFalse(DetailRule.Far(0f, range, false));
            Assert.IsFalse(DetailRule.Far(range, range, false), "at the range itself nothing changes yet");
            Assert.IsTrue(DetailRule.Far(range * 1.2f, range, false));
            Assert.IsTrue(DetailRule.Far(range, range, true), "and one already far stays far there");
            Assert.IsFalse(DetailRule.Far(range * 0.8f, range, true));
        }

        [Test]
        public void AThingStandingAtTheEdge_DoesNotFlicker()
        {
            // Stepping back and forth across the range by less than the slack never changes what is drawn.
            const float range = DetailRule.GunRange;
            foreach (bool start in new[] { false, true })
            {
                bool far = start;
                for (int i = 0; i < 100; i++)
                {
                    float d = range * (1f + (i % 2 == 0 ? 1f : -1f) * DetailRule.Slack * 0.9f);
                    far = DetailRule.Far(d, range, far);
                    Assert.AreEqual(start, far, "changed on step " + i);
                }
            }
        }

        [Test]
        public void NoRange_MeansAlwaysFull_AndABrokenDistanceChangesNothing()
        {
            Assert.IsFalse(DetailRule.Far(1000f, 0f, true));
            Assert.IsFalse(DetailRule.Far(1000f, -5f, false));
            Assert.IsFalse(DetailRule.Far(1000f, float.NaN, true));
            Assert.IsTrue(DetailRule.Far(float.NaN, 10f, true));
            Assert.IsFalse(DetailRule.Far(float.NaN, 10f, false));
        }

        static void Pick(float[] distance, bool[] counted, bool[] far, int cap, float range = DetailRule.BodyRange, bool[] unseen = null)
        {
            var ranges = new float[distance.Length];
            for (int i = 0; i < ranges.Length; i++) ranges[i] = range;
            DetailRule.Pick(distance, ranges, counted, unseen, far, new float[distance.Length], distance.Length, cap);
        }

        [Test]
        public void OnlyTheNearestFewBodies_AreEverFull()
        {
            // Twenty bodies all well inside the range: only so many stay full, and they are the nearest.
            var distance = new float[20];
            var counted = new bool[20];
            var far = new bool[20];
            for (int i = 0; i < 20; i++) { distance[i] = 2f + i; counted[i] = true; }
            Pick(distance, counted, far, DetailRule.FullBodies);
            for (int i = 0; i < 20; i++) Assert.AreEqual(i >= DetailRule.FullBodies, far[i], "body " + i);
            Assert.That(DetailRule.FullBodies, Is.InRange(4, 16), "the worst frame in docs/performance_budget.md is worked out from this number");
        }

        [Test]
        public void ThingsThatAreNotBodies_AreNotCounted()
        {
            // Twelve guns close by all stay full, and they take no body's place.
            var distance = new float[14];
            var counted = new bool[14];
            var far = new bool[14];
            for (int i = 0; i < 12; i++) distance[i] = 1f + i * 0.5f;
            distance[12] = 15f; counted[12] = true;
            distance[13] = 16f; counted[13] = true;
            Pick(distance, counted, far, 1, range: 30f);
            for (int i = 0; i < 12; i++) Assert.IsFalse(far[i], "gun " + i);
            Assert.IsFalse(far[12], "the nearer body keeps the one full place");
            Assert.IsTrue(far[13]);
        }

        [Test]
        public void TwoBodiesAtTheSameDistance_DoNotSwapPlaces()
        {
            // One full place, two bodies taking turns being a hair nearer: whoever holds the place keeps it.
            var counted = new[] { true, true };
            var far = new[] { false, true };
            for (int i = 0; i < 50; i++)
            {
                float wobble = i % 2 == 0 ? 0.3f : -0.3f;
                Pick(new[] { 10f + wobble, 10f - wobble }, counted, far, 1);
                Assert.IsFalse(far[0], "step " + i);
                Assert.IsTrue(far[1], "step " + i);
            }
            // But one that comes clearly nearer does take it.
            Pick(new[] { 10f, 6f }, counted, far, 1);
            Assert.IsTrue(far[0]);
            Assert.IsFalse(far[1]);
        }

        [Test]
        public void TheCapNeverMakesAFarBodyFull_AndOddInputBreaksNothing()
        {
            var far = new[] { false, false, false };
            Pick(new[] { 100f, 200f, float.NaN }, new[] { true, true, true }, far, 8);
            Assert.IsTrue(far[0]);
            Assert.IsTrue(far[1]);
            Assert.IsFalse(far[2], "a broken distance leaves a thing as it was");

            // No full places at all: every counted body is far, however near.
            far = new[] { false, false };
            Pick(new[] { 1f, 2f }, new[] { true, false }, far, 0);
            Assert.IsTrue(far[0]);
            Assert.IsFalse(far[1]);
            Pick(new[] { 1f, 2f }, new[] { true, false }, far, -3);
            Assert.IsTrue(far[0]);

            // Missing or short arrays are left alone, not read past their ends.
            Assert.DoesNotThrow(() => DetailRule.Pick(null, null, null, null, null, null, 5, 8));
            Assert.DoesNotThrow(() => DetailRule.Pick(new float[2], new float[1], new bool[2], null, new bool[2], new float[2], 9, 8));
            Assert.DoesNotThrow(() => DetailRule.Pick(new float[2], new float[2], new bool[2], new bool[1], new bool[2], new float[2], 2, 8), "a short list of who is unseen is ignored");
        }

        [Test]
        public void ABodyBehindThePlayer_GivesItsPlace_ToOneInView()
        {
            // Two full places. Two bodies close behind the player, two a little further off in front.
            var distance = new[] { 4f, 5f, 8f, 9f };
            var counted = new[] { true, true, true, true };
            var unseen = new[] { true, true, false, false };
            var far = new bool[4];
            Pick(distance, counted, far, 2, unseen: unseen);
            Assert.IsTrue(far[0]);
            Assert.IsTrue(far[1]);
            Assert.IsFalse(far[2], "the ones being looked at are drawn in full");
            Assert.IsFalse(far[3]);

            // With places to spare nobody gives anything up: turning round shows them as they were.
            far = new bool[4];
            Pick(distance, counted, far, 8, unseen: unseen);
            for (int i = 0; i < 4; i++) Assert.IsFalse(far[i], "body " + i);

            // And being unseen never makes a far body full or changes the distance rule.
            far = new bool[1];
            Pick(new[] { 100f }, new[] { true }, far, 8, unseen: new[] { false });
            Assert.IsTrue(far[0]);
        }

        [Test]
        public void WhatIsInView_IsDecidedGenerously()
        {
            // Straight ahead, and out to 70 degrees either side, a body is in view.
            Assert.IsFalse(DetailRule.Unseen(10f, 10f));
            Assert.IsFalse(DetailRule.Unseen(10f * 0.5f, 10f), "60 degrees off: just past the corner of the screen, still counted as seen");
            Assert.IsTrue(DetailRule.Unseen(10f * 0.2f, 10f), "78 degrees off");
            Assert.IsTrue(DetailRule.Unseen(-10f, 10f), "behind");
            // Close by, always: the angle to something at your feet says little.
            Assert.IsFalse(DetailRule.Unseen(-2f, 2f));
            Assert.IsFalse(DetailRule.Unseen(float.NaN, 10f));
        }

        [Test]
        public void TheCommonEnemy_DoesNotStandOnTheLine()
        {
            // The trooper holds at 18 m. Its body must already be full there, with room to step back a little.
            Assert.IsFalse(DetailRule.Far(18f + 2f, DetailRule.BodyRange, true), "a trooper two steps beyond where it stands is still full");
            Assert.Less(DetailRule.GunRange, DetailRule.BodyRange);
        }

        [Test]
        public void AFarCopysName_IsTheModelsWithFarOnTheEnd()
        {
            Assert.AreEqual("EnemyFar", ModelFile.FarOf("Enemy"));
            Assert.AreEqual("SniperFar", ModelFile.FarOf("Sniper"));
            Assert.IsNull(ModelFile.FarOf("EnemyFar"), "a far copy has no far copy of its own");
            Assert.IsNull(ModelFile.FarOf(""));
            Assert.IsNull(ModelFile.FarOf(null));
            Assert.IsTrue(ModelFile.IsFar("RifleFar"));
            Assert.IsFalse(ModelFile.IsFar("Rifle"));
            Assert.IsFalse(ModelFile.IsFar("Far"), "a model simply called Far is not a far copy of nothing");
        }
    }
}
