using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Perf
{
    public enum PerfVerdict
    {
        /// <summary>Not enough frames measured yet.</summary>
        Measuring,
        WithinBudget,
        /// <summary>Too many frames miss the 60 fps budget, but there are no real hitches.</summary>
        Tight,
        /// <summary>Visible hitches. Something needs cutting or fixing.</summary>
        Over,
    }

    /// <summary>
    /// Measures the game against the performance budget while it is being played, and puts a verdict on screen.
    /// Only counts frames while a run is live: menus and pauses would flatter the numbers.
    /// </summary>
    public sealed class PerfMonitor : MonoBehaviour
    {
        public static PerfMonitor Instance { get; private set; }

        public PerformanceBudget Budget { get; private set; }
        public FrameStats Stats { get; } = new FrameStats(600);

        public static void ResetStatics() => Instance = null;

        void Awake()
        {
            Instance = this;
            Budget = PerformanceBudget.Load();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (GameSession.Mode != GameMode.Playing || GameSession.Paused) return;
            Stats.Add(Time.unscaledDeltaTime);
        }

        public PerfVerdict Verdict
        {
            get
            {
                if (Stats.Count < 120) return PerfVerdict.Measuring;
                if (Stats.FractionOver(Budget.hitchMs / 1000f) > Budget.allowedHitches) return PerfVerdict.Over;
                if (Stats.FractionOver(Budget.frameBudgetMs / 1000f) > Budget.allowedOverBudget) return PerfVerdict.Tight;
                return PerfVerdict.WithinBudget;
            }
        }

        /// <summary>One line for the on-screen readout and the log.</summary>
        public string Describe()
        {
            PerfVerdict verdict = Verdict;
            if (verdict == PerfVerdict.Measuring) return "Budget " + Budget.frameBudgetMs.ToString("0.0") + " ms: measuring...";
            string label = verdict == PerfVerdict.WithinBudget ? "OK" : verdict == PerfVerdict.Tight ? "TIGHT" : "OVER";
            return "Budget " + Budget.frameBudgetMs.ToString("0.0") + " ms: " + label +
                   "   1% worst " + (Stats.Percentile(0.99f) * 1000f).ToString("0.0") + " ms" +
                   "   " + Mathf.RoundToInt(Stats.FractionOver(Budget.frameBudgetMs / 1000f) * 100f) + "% of frames over";
        }

        /// <summary>
        /// Writes the numbers for the run that just ended to the Console and starts measuring afresh. These lines
        /// are the evidence to send back after a playtest.
        /// </summary>
        public void LogAndReset(string what)
        {
            if (Stats.Count >= 120)
            {
                Debug.Log("Orbital Ante performance (" + what + ", " + Screen.width + "x" + Screen.height + ", last " + Stats.Count + " frames): " +
                          "average " + (Stats.Average * 1000f).ToString("0.0") + " ms, " +
                          "worst " + (Stats.Worst * 1000f).ToString("0.0") + " ms. " + Describe());
            }
            Stats.Clear();
        }
    }
}
