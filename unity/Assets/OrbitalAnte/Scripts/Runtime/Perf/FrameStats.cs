using System;

namespace OrbitalAnte.Perf
{
    /// <summary>
    /// Keeps the last few seconds of frame times and answers the questions a performance budget asks: what is
    /// the average, how bad are the worst frames, and how often is the budget missed. No Unity types.
    /// </summary>
    public sealed class FrameStats
    {
        readonly float[] _samples;
        readonly float[] _scratch;
        int _count;
        int _next;

        /// <param name="capacity">How many frames to remember. 600 is ten seconds at 60 frames per second.</param>
        public FrameStats(int capacity = 600)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _samples = new float[capacity];
            _scratch = new float[capacity];
        }

        public int Count => _count;
        public int Capacity => _samples.Length;

        public void Add(float frameSeconds)
        {
            if (frameSeconds < 0f || float.IsNaN(frameSeconds)) return;
            _samples[_next] = frameSeconds;
            _next = (_next + 1) % _samples.Length;
            if (_count < _samples.Length) _count++;
        }

        public void Clear()
        {
            _count = 0;
            _next = 0;
        }

        /// <summary>Average frame time in seconds.</summary>
        public float Average
        {
            get
            {
                if (_count == 0) return 0f;
                double sum = 0;
                for (int i = 0; i < _count; i++) sum += _samples[i];
                return (float)(sum / _count);
            }
        }

        public float Worst
        {
            get
            {
                float worst = 0f;
                for (int i = 0; i < _count; i++) if (_samples[i] > worst) worst = _samples[i];
                return worst;
            }
        }

        /// <summary>
        /// The frame time that the given share of frames stay under. Percentile(0.99f) is the "1% low": only
        /// one frame in a hundred is slower than this. It is what a stutter feels like, which an average hides.
        /// </summary>
        public float Percentile(float fraction)
        {
            if (_count == 0) return 0f;
            Array.Copy(_samples, _scratch, _count);
            Array.Sort(_scratch, 0, _count);
            if (fraction <= 0f) return _scratch[0];
            if (fraction >= 1f) return _scratch[_count - 1];
            int index = (int)Math.Ceiling(fraction * _count) - 1;
            if (index < 0) index = 0;
            return _scratch[index];
        }

        /// <summary>Share of remembered frames, from 0 to 1, that took longer than the budget.</summary>
        public float FractionOver(float budgetSeconds)
        {
            if (_count == 0) return 0f;
            int over = 0;
            for (int i = 0; i < _count; i++) if (_samples[i] > budgetSeconds) over++;
            return over / (float)_count;
        }
    }
}
