using OrbitalAnte.Characters;
using OrbitalAnte.Input;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// Always-available frame-time readout so performance is measured, not guessed.
    /// Budget lines: 16.7 ms (60 fps) and 25 ms (40 fps floor).
    /// </summary>
    public class FrameTimeOverlay : MonoBehaviour
    {
        const int Samples = 240;
        readonly float[] frameMs = new float[Samples];
        readonly float[] sorted = new float[Samples];
        int index;
        int filled;
        float refresh;
        Text text;
        GameObject panel;
        ProfilerRecorder drawCalls;
        ProfilerRecorder mainThread;

        public static FrameTimeOverlay Create()
        {
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
            rt.sizeDelta = new Vector2(300, 150);
            panel = bg.gameObject;
            text = UIFactory.Label(bg.transform, "", 16, Color.white, TextAnchor.UpperLeft);
            UIFactory.Stretch(text.rectTransform, 8f);
            text.supportRichText = true;
        }

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

        void Update()
        {
            if (GameInput.DebugOverlay.WasPressedThisFrame()) panel.SetActive(!panel.activeSelf);

            frameMs[index] = Time.unscaledDeltaTime * 1000f;
            index = (index + 1) % Samples;
            if (filled < Samples) filled++;

            refresh -= Time.unscaledDeltaTime;
            if (refresh > 0f || !panel.activeSelf) return;
            refresh = 0.25f;

            float sum = 0f, max = 0f;
            for (int i = 0; i < filled; i++)
            {
                sum += frameMs[i];
                if (frameMs[i] > max) max = frameMs[i];
                sorted[i] = frameMs[i];
            }
            float avg = sum / Mathf.Max(1, filled);
            System.Array.Sort(sorted, 0, filled);
            float worst1 = sorted[Mathf.Clamp(Mathf.FloorToInt(filled * 0.99f), 0, filled - 1)];

            string dc = drawCalls.Valid ? drawCalls.LastValue.ToString() : "n/a";
            string mt = mainThread.Valid ? (mainThread.LastValue / 1e6).ToString("0.0") + " ms" : "n/a";
            text.text =
                $"<b>{1000f / Mathf.Max(0.01f, avg):0} fps</b>  avg {Colour(avg)}\n" +
                $"worst 1% {Colour(worst1)}  max {Colour(max)}\n" +
                $"main thread {mt}   draw calls {dc}\n" +
                $"dummies {Mannequin.Alive.Count}   gib bodies {GibManager.ActiveCount}\n" +
                $"<size=13>budget 16.7 ms (60) / floor 25 ms (40)</size>";
        }

        static string Colour(float ms)
        {
            string c = ms <= 16.9f ? "#7CFC8A" : ms <= 25f ? "#FFD25A" : "#FF6B5A";
            return $"<color={c}>{ms:0.0} ms</color>";
        }
    }
}
