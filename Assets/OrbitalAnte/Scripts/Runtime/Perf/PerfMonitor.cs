using System;
using Unity.Profiling;
using UnityEngine;

namespace OrbitalAnte.Perf
{
    /// <summary>
    /// Measures every frame against the performance budget. The overlay shows its numbers; it
    /// also writes a warning to the log when the game stays under the floor, with whatever
    /// scene counts the game has registered, so slow spots can be found from a log alone.
    /// </summary>
    public class PerfMonitor : MonoBehaviour
    {
        const int Samples = 240;

        static PerfMonitor instance;

        /// <summary>Optional: returns a short description of the scene load (dummies, gibs...) for warnings.</summary>
        public static Func<string> DescribeLoad;

        readonly float[] frameMs = new float[Samples];
        readonly float[] scratch = new float[Samples];
        int index;
        int filled;
        float refresh;
        float overFloorSeconds;
        float nextWarnAt;
        PerformanceBudgetConfig budget;
        ProfilerRecorder drawCalls;
        ProfilerRecorder mainThread;

        public static PerfMonitor Instance => instance;
        public FrameStats Stats { get; private set; }
        public PerformanceBudgetConfig Budget => budget;
        /// <summary>-1 when the counter is not available on this platform.</summary>
        public long DrawCalls => drawCalls.Valid ? drawCalls.LastValue : -1;
        /// <summary>Main thread time in ms, or -1 when not available.</summary>
        public float MainThreadMs => mainThread.Valid ? mainThread.LastValue / 1e6f : -1f;
        /// <summary>How many warnings have been logged since this monitor was created.</summary>
        public int WarningCount { get; private set; }

        public static PerfMonitor EnsureCreated()
        {
            if (instance == null)
            {
                var go = new GameObject("PerfMonitor");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<PerfMonitor>();
            }
            return instance;
        }

        void Awake() => budget = PerformanceBudgetConfig.Get();

        void OnEnable()
        {
            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
        }

        void OnDisable()
        {
            drawCalls.Dispose();
            mainThread.Dispose();
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void Update()
        {
            // A paused game (time scale 0) still renders, so it still counts.
            float dt = Time.unscaledDeltaTime;
            frameMs[index] = dt * 1000f;
            index = (index + 1) % Samples;
            if (filled < Samples) filled++;

            refresh -= dt;
            if (refresh > 0f) return;
            refresh = 0.25f;
            Stats = FrameStats.Compute(frameMs, filled, scratch);
            Watch(0.25f);
        }

        void Watch(float elapsed)
        {
            if (filled < Samples / 2) return; // ignore the first second or two of loading hitches
            bool over = FrameStats.Rate(Stats.AverageMs, budget.frameBudgetMs, budget.frameFloorMs) == BudgetRating.Over;
            overFloorSeconds = over ? overFloorSeconds + elapsed : 0f;
            if (overFloorSeconds < budget.warnAfterSeconds || Time.unscaledTime < nextWarnAt) return;

            nextWarnAt = Time.unscaledTime + budget.warnRepeatSeconds;
            WarningCount++;
            string load = DescribeLoad != null ? DescribeLoad() : "";
            Debug.LogWarning($"[OA] Over the frame budget: average {Stats.AverageMs:0.0} ms, worst 1% {Stats.Worst1PercentMs:0.0} ms " +
                             $"(floor {budget.frameFloorMs:0.0} ms). Draw calls {DrawCalls}. {load}");
        }
    }
}
