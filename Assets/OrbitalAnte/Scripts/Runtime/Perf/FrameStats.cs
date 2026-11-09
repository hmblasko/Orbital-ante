using System;

namespace OrbitalAnte.Perf
{
    public enum BudgetRating
    {
        /// <summary>At or under the target.</summary>
        Good,
        /// <summary>Over the target but above the floor.</summary>
        Warn,
        /// <summary>Slower than the floor.</summary>
        Over,
    }

    /// <summary>Summary of recent frame times in milliseconds.</summary>
    public struct FrameStats
    {
        public int Count;
        public float AverageMs;
        /// <summary>99th percentile: only 1 frame in 100 was slower than this.</summary>
        public float Worst1PercentMs;
        public float MaxMs;

        /// <summary>
        /// Summarises the first <paramref name="count"/> samples. <paramref name="scratch"/> must be
        /// at least as long; it is overwritten (so no memory is allocated per call).
        /// </summary>
        public static FrameStats Compute(float[] samples, int count, float[] scratch)
        {
            var stats = new FrameStats();
            if (samples == null || count <= 0) return stats;
            count = Math.Min(count, Math.Min(samples.Length, scratch.Length));
            float sum = 0f, max = 0f;
            for (int i = 0; i < count; i++)
            {
                float v = samples[i];
                sum += v;
                if (v > max) max = v;
                scratch[i] = v;
            }
            Array.Sort(scratch, 0, count);
            int idx = (int)Math.Floor(count * 0.99f);
            if (idx > count - 1) idx = count - 1;
            stats.Count = count;
            stats.AverageMs = sum / count;
            stats.MaxMs = max;
            stats.Worst1PercentMs = scratch[idx];
            return stats;
        }

        /// <summary>Small tolerance so a steady 60 fps (16.67 ms, jittering) still reads as Good.</summary>
        public static BudgetRating Rate(float ms, float budgetMs, float floorMs)
        {
            if (ms <= budgetMs + 0.3f) return BudgetRating.Good;
            return ms <= floorMs ? BudgetRating.Warn : BudgetRating.Over;
        }
    }
}
