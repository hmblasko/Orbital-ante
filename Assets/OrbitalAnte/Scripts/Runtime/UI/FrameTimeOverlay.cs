using OrbitalAnte.Input;
using OrbitalAnte.Perf;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// Always-available frame-time readout so performance is measured, not guessed. The numbers
    /// come from <see cref="PerfMonitor"/> and are coloured against the performance budget asset.
    /// </summary>
    public class FrameTimeOverlay : MonoBehaviour
    {
        float refresh;
        Text text;
        GameObject panel;

        public static FrameTimeOverlay Create()
        {
            PerfMonitor.EnsureCreated();
            var canvas = UIFactory.CreateCanvas("FrameTimeOverlay", 50);
            var o = canvas.gameObject.AddComponent<FrameTimeOverlay>();
            o.Build(canvas.transform);
            return o;
        }

        void Build(Transform root)
        {
            var bg = UIFactory.Image("Panel", root, new Color(0, 0, 0, 0.55f));
            var rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(12, -12);
            rt.sizeDelta = new Vector2(360, 150);
            bg.raycastTarget = false;
            panel = bg.gameObject;
            text = UIFactory.Label(bg.transform, "", 16, Color.white, TextAnchor.UpperLeft);
            UIFactory.Stretch(text.rectTransform, 8f);
            text.supportRichText = true;
        }

        void Update()
        {
            if (GameInput.DebugOverlay.WasPressedThisFrame()) panel.SetActive(!panel.activeSelf);

            refresh -= Time.unscaledDeltaTime;
            var perf = PerfMonitor.Instance;
            if (refresh > 0f || !panel.activeSelf || perf == null) return;
            refresh = 0.25f;

            var s = perf.Stats;
            var b = perf.Budget;
            long dc = perf.DrawCalls;
            float mt = perf.MainThreadMs;
            string dcText = dc < 0 ? "n/a" : dc > b.drawCallBudget ? $"<color=#FF6B5A>{dc}</color>" : dc.ToString();
            string mtText = mt < 0f ? "n/a" : mt.ToString("0.0") + " ms";
            string load = PerfMonitor.DescribeLoad != null ? PerfMonitor.DescribeLoad() : "";
            text.text =
                $"<b>{1000f / Mathf.Max(0.01f, s.AverageMs):0} fps</b>  avg {Colour(s.AverageMs, b)}\n" +
                $"worst 1% {Colour(s.Worst1PercentMs, b)}  max {Colour(s.MaxMs, b)}\n" +
                $"main thread {mtText}   draw calls {dcText} / {b.drawCallBudget}\n" +
                $"{load}\n" +
                $"<size=13>budget {b.frameBudgetMs:0.0} ms / floor {b.frameFloorMs:0.0} ms</size>";
        }

        static string Colour(float ms, PerformanceBudgetConfig b)
        {
            var rating = FrameStats.Rate(ms, b.frameBudgetMs, b.frameFloorMs);
            string c = rating == BudgetRating.Good ? "#7CFC8A" : rating == BudgetRating.Warn ? "#FFD25A" : "#FF6B5A";
            return $"<color={c}>{ms:0.0} ms</color>";
        }
    }
}
