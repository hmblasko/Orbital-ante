using UnityEngine;

namespace OrbitalAnte.Perf
{
    /// <summary>
    /// The Steam Deck performance budget in one place. These are targets to measure against,
    /// not measurements. Edit the asset in Resources/Config.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Performance Budget", fileName = "PerformanceBudgetConfig")]
    public class PerformanceBudgetConfig : ScriptableObject
    {
        [Header("Frame time (milliseconds)")]
        [Tooltip("Target: 16.7 ms is 60 frames per second.")]
        public float frameBudgetMs = 16.7f;
        [Tooltip("Floor: 25 ms is 40 frames per second, the Deck's lowest comfortable refresh rate.")]
        public float frameFloorMs = 25f;

        [Header("Frame pacing")]
        [Tooltip("1 = sync to the screen (recommended on Deck). 0 = off, then Target Frame Rate applies.")]
        [Range(0, 2)] public int vSyncCount = 1;
        [Tooltip("Only used when vSync is 0. -1 = uncapped.")]
        public int targetFrameRate = -1;

        [Header("Scene budgets (the overlay turns these red when exceeded)")]
        public int drawCallBudget = 1500;

        [Header("Warnings")]
        [Tooltip("Log a warning when the average frame stays over the floor for this long (seconds).")]
        public float warnAfterSeconds = 3f;
        [Tooltip("Minimum gap between warnings (seconds).")]
        public float warnRepeatSeconds = 30f;

        static PerformanceBudgetConfig cached;

        public static PerformanceBudgetConfig Get()
        {
            if (cached == null)
            {
                cached = Resources.Load<PerformanceBudgetConfig>("Config/PerformanceBudgetConfig");
                if (cached == null) cached = CreateInstance<PerformanceBudgetConfig>();
            }
            return cached;
        }
    }
}
