using NUnit.Framework;
using OrbitalAnte.Perf;

namespace OrbitalAnte.Tests
{
    /// <summary>The sums behind the on-screen performance readout. These run without Unity objects.</summary>
    public class FrameStatsTests
    {
        const float Frame60 = 1f / 60f;

        [Test]
        public void EmptyStatsAreAllZero()
        {
            var stats = new FrameStats(10);
            Assert.AreEqual(0, stats.Count);
            Assert.AreEqual(0f, stats.Average);
            Assert.AreEqual(0f, stats.Worst);
            Assert.AreEqual(0f, stats.Percentile(0.99f));
            Assert.AreEqual(0f, stats.FractionOver(Frame60));
        }

        [Test]
        public void AverageAndWorst()
        {
            var stats = new FrameStats(10);
            stats.Add(0.010f);
            stats.Add(0.020f);
            stats.Add(0.030f);
            Assert.AreEqual(0.020f, stats.Average, 1e-6f);
            Assert.AreEqual(0.030f, stats.Worst, 1e-6f);
        }

        [Test]
        public void OnlyTheNewestFramesAreRemembered()
        {
            var stats = new FrameStats(4);
            stats.Add(1f);                                  // an old hitch...
            for (int i = 0; i < 4; i++) stats.Add(0.016f);  // ...pushed out by four good frames
            Assert.AreEqual(4, stats.Count);
            Assert.AreEqual(0.016f, stats.Worst, 1e-6f);
        }

        [Test]
        public void OnePercentWorstIgnoresASingleFreakFrame()
        {
            var stats = new FrameStats(600);
            for (int i = 0; i < 599; i++) stats.Add(0.016f);
            stats.Add(0.200f);
            Assert.AreEqual(0.016f, stats.Percentile(0.99f), 1e-6f);
            Assert.AreEqual(0.200f, stats.Percentile(1f), 1e-6f);
            Assert.AreEqual(0.200f, stats.Worst, 1e-6f);
        }

        [Test]
        public void OnePercentWorstShowsRegularStutter()
        {
            var stats = new FrameStats(600);
            for (int i = 0; i < 600; i++) stats.Add(i % 20 == 0 ? 0.050f : 0.016f);   // a hitch every 20 frames
            Assert.AreEqual(0.050f, stats.Percentile(0.99f), 1e-6f);
            Assert.Less(stats.Average, 0.018f, "The average hides it, which is why the readout shows both.");
        }

        [Test]
        public void FractionOverCountsFramesThatMissTheBudget()
        {
            var stats = new FrameStats(100);
            for (int i = 0; i < 90; i++) stats.Add(0.016f);
            for (int i = 0; i < 10; i++) stats.Add(0.030f);
            Assert.AreEqual(0.10f, stats.FractionOver(Frame60), 1e-6f);
            Assert.AreEqual(0f, stats.FractionOver(0.040f), 1e-6f);
        }

        [Test]
        public void ClearAndBadValues()
        {
            var stats = new FrameStats(10);
            stats.Add(-1f);
            stats.Add(float.NaN);
            Assert.AreEqual(0, stats.Count, "Nonsense values are ignored.");
            stats.Add(0.02f);
            stats.Clear();
            Assert.AreEqual(0, stats.Count);
            Assert.AreEqual(0f, stats.Worst);
        }
    }
}
