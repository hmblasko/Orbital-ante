using System.Collections.Generic;
using OrbitalAnte.Characters;
using OrbitalAnte.Input;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    public enum NoticeKind { Info, Combat, System }

    /// <summary>Crosshair, hit marker, playtest hints and the capped top-right notifications.</summary>
    public class Hud : MonoBehaviour
    {
        public static Hud Instance { get; private set; }

        const int MaxNotices = 3;
        const float NoticeSeconds = 3.5f;

        RectTransform hitMarker;
        Image[] hitMarkerBars;
        float hitMarkerTimer;
        Text hintText;
        Text stateText;
        RectTransform noticeColumn;
        readonly List<(GameObject go, float until)> notices = new List<(GameObject, float)>();
        float hintRefresh;

        public static Hud Create()
        {
            var canvas = UIFactory.CreateCanvas("HUD", 10);
            var hud = canvas.gameObject.AddComponent<Hud>();
            hud.Build(canvas.transform);
            return hud;
        }

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Build(Transform root)
        {
            // Crosshair: a small dot with four ticks.
            var cross = UIFactory.Rect("Crosshair", root);
            cross.sizeDelta = new Vector2(40, 40);
            var dot = UIFactory.Image("Dot", cross, new Color(1, 1, 1, 0.9f));
            dot.rectTransform.sizeDelta = new Vector2(4, 4);
            for (int i = 0; i < 4; i++)
            {
                var tick = UIFactory.Image("Tick", cross, new Color(1, 1, 1, 0.75f));
                bool vertical = i < 2;
                tick.rectTransform.sizeDelta = vertical ? new Vector2(2, 8) : new Vector2(8, 2);
                float o = 11f * (i % 2 == 0 ? 1 : -1);
                tick.rectTransform.anchoredPosition = vertical ? new Vector2(0, o) : new Vector2(o, 0);
            }

            // Hit marker: four diagonal bars, hidden until a hit lands.
            hitMarker = UIFactory.Rect("HitMarker", root);
            hitMarker.sizeDelta = new Vector2(44, 44);
            hitMarkerBars = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var bar = UIFactory.Image("Bar", hitMarker, Color.white);
                bar.rectTransform.sizeDelta = new Vector2(3, 12);
                float angle = 45f + 90f * i;
                Vector2 dir = Quaternion.Euler(0, 0, angle) * Vector2.up;
                bar.rectTransform.anchoredPosition = dir * 14f;
                bar.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
                hitMarkerBars[i] = bar;
            }
            hitMarker.gameObject.SetActive(false);

            // Bottom-left playtest hints.
            hintText = UIFactory.Label(root, "", UITheme.SmallSize, UITheme.Text, TextAnchor.LowerLeft);
            var hr = hintText.rectTransform;
            hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(0, 0);
            hr.anchoredPosition = new Vector2(18, 16);
            hr.sizeDelta = new Vector2(620, 120);

            stateText = UIFactory.Label(root, "", UITheme.SmallSize, UITheme.Accent, TextAnchor.LowerRight, FontStyle.Bold);
            var sr = stateText.rectTransform;
            sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(1, 0);
            sr.anchoredPosition = new Vector2(-18, 16);
            sr.sizeDelta = new Vector2(420, 60);

            // Top-right notifications.
            noticeColumn = UIFactory.Rect("Notices", root);
            noticeColumn.anchorMin = noticeColumn.anchorMax = noticeColumn.pivot = new Vector2(1, 1);
            noticeColumn.anchoredPosition = new Vector2(-18, -18);
            noticeColumn.sizeDelta = new Vector2(380, 300);
            var v = noticeColumn.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8;
            v.childAlignment = TextAnchor.UpperRight;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
        }

        public void ShowHitMarker(bool big)
        {
            hitMarkerTimer = big ? 0.2f : 0.11f;
            var c = big ? UITheme.Danger : Color.white;
            foreach (var b in hitMarkerBars) b.color = c;
            hitMarker.localScale = Vector3.one * (big ? 1.35f : 1f);
            hitMarker.gameObject.SetActive(true);
        }

        public void Notify(string message, NoticeKind kind = NoticeKind.Info)
        {
            Color edge = kind == NoticeKind.Combat ? UITheme.Danger : kind == NoticeKind.System ? UITheme.Teal : UITheme.Accent;
            var box = UIFactory.Image("Notice", noticeColumn, edge);
            UIFactory.Layout(box.gameObject, 48);
            var inner = UIFactory.Image("Inner", box.transform, UITheme.Card);
            UIFactory.Stretch(inner.rectTransform, 3f);
            var t = UIFactory.Label(inner.transform, message, UITheme.BodySize, UITheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(t.rectTransform, 6f);
            box.rectTransform.localRotation = Quaternion.Euler(0, 0, Random.Range(-1.5f, 1.5f));
            notices.Add((box.gameObject, Time.unscaledTime + NoticeSeconds));
            while (notices.Count > MaxNotices)
            {
                Destroy(notices[0].go);
                notices.RemoveAt(0);
            }
        }

        public void SetState(string text) => stateText.text = text;

        void Update()
        {
            if (hitMarkerTimer > 0f)
            {
                hitMarkerTimer -= Time.unscaledDeltaTime;
                if (hitMarkerTimer <= 0f) hitMarker.gameObject.SetActive(false);
            }

            for (int i = notices.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime > notices[i].until)
                {
                    Destroy(notices[i].go);
                    notices.RemoveAt(i);
                }
            }

            hintRefresh -= Time.unscaledDeltaTime;
            if (hintRefresh <= 0f)
            {
                hintRefresh = 0.4f;
                hintText.text =
                    $"<b>TEST ARENA</b>  shoot dummies with [{InputGlyphs.HintFor(GameInput.Fire)}]\n" +
                    $"[{InputGlyphs.HintFor(GameInput.DebugRespawn)}] respawn dummies   " +
                    $"[{InputGlyphs.HintFor(GameInput.DebugWalkMode)}] walk mode: <b>{MannequinBrain.GlobalMode}</b>\n" +
                    $"[{InputGlyphs.HintFor(GameInput.DebugOverlay)}] frame times   [{InputGlyphs.HintFor(GameInput.Pause)}] pause / controls";
            }
        }
    }
}
