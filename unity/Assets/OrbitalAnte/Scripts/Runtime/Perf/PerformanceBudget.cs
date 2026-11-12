using UnityEngine;

namespace OrbitalAnte.Perf
{
    /// <summary>
    /// The Steam Deck performance budget, as numbers the game checks itself against while it runs. An asset is
    /// created at Assets/OrbitalAnte/Resources/Config/PerformanceBudget. The reasoning behind the numbers is in
    /// docs/performance_budget.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Performance Budget", fileName = "PerformanceBudget")]
    public sealed class PerformanceBudget : ScriptableObject
    {
        public const string ResourcePath = "Config/PerformanceBudget";

        [Header("Frame time on Steam Deck")]
        [Tooltip("Frames per second the game aims for. The Deck screen is 60 Hz.")]
        public int targetFrameRate = 60;
        [Tooltip("Time allowed per frame at the target, in milliseconds (1000 / 60).")]
        public float frameBudgetMs = 16.7f;
        [Tooltip("The floor. A frame slower than this is a visible hitch (25 ms is 40 frames per second, the Deck's other smooth mode).")]
        public float hitchMs = 25f;
        [Tooltip("Share of frames allowed over the frame budget before the readout says TIGHT (0.05 = 5%).")]
        [Range(0f, 1f)] public float allowedOverBudget = 0.05f;
        [Tooltip("Share of frames allowed over the hitch time before the readout says OVER.")]
        [Range(0f, 1f)] public float allowedHitches = 0.01f;

        [Header("Applied when the game starts")]
        [Tooltip("Wait for the screen refresh. Stops tearing and stops the Deck drawing frames nobody sees.")]
        public bool vSync = true;

        [Header("Scene budgets (targets to check in the Unity Profiler; not enforced by code)")]
        public int maxDrawCalls = 1500;
        public int maxTrianglesThousands = 600;
        public int maxRealtimeLights = 8;
        public float maxScriptMs = 4f;
        public float maxPhysicsMs = 3f;

        static PerformanceBudget _cached;

        public static PerformanceBudget Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<PerformanceBudget>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<PerformanceBudget>();
                _cached.name = "PerformanceBudget (defaults)";
            }
            return _cached;
        }

        /// <summary>Applies the settings the budget assumes.</summary>
        public void Apply()
        {
            QualitySettings.vSyncCount = vSync ? 1 : 0;
            // With vSync on the screen sets the pace. Without it, cap at the target so the Deck's fan and
            // battery are not spent on frames above 60.
            Application.targetFrameRate = vSync ? -1 : Mathf.Max(30, targetFrameRate);
        }
    }
}
