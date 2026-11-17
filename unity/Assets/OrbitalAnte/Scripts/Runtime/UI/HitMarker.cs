using OrbitalAnte.Core;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// The hit marker round the crosshair (Matthew, 16 Nov 2026), drawn in the visor's style: four thin ticks in
    /// an X, each over a soft dark edge so it reads against a bright sky, popping out and settling, then fading.
    /// A headshot kill adds a second gold X outside the red one. The rules (what a hit was, sizes, colours,
    /// times) are <see cref="HitConfirm"/>; the ding is played by <c>GameAudioHooks</c> from the same rules.
    ///
    /// It has its own canvas so it shows in the solar system as well as the arena (the arena HUD's parts are
    /// hidden there). Only hits by the player on living enemies show.
    /// </summary>
    public sealed class HitMarker : MonoBehaviour
    {
        const int Ticks = 4;
        // Halo: the dark edge under each tick, this many pixels wider on every side.
        const float HaloPad = 1.5f;
        static readonly Color HaloColor = new Color(0f, 0f, 0f, 0.55f);

        static readonly Color White = new Color(1f, 0.97f, 0.92f);
        static readonly Color Gold = new Color(1f, 0.80f, 0.30f);
        static readonly Color Cyan = new Color(0.45f, 0.85f, 1f);
        static readonly Color Red = new Color(1f, 0.22f, 0.16f);

        Canvas _canvas;
        RectTransform _root;
        readonly RectTransform[] _inner = new RectTransform[Ticks], _innerHalo = new RectTransform[Ticks];
        readonly RectTransform[] _outer = new RectTransform[Ticks], _outerHalo = new RectTransform[Ticks];
        readonly Image[] _innerImg = new Image[Ticks], _innerHaloImg = new Image[Ticks];
        readonly Image[] _outerImg = new Image[Ticks], _outerHaloImg = new Image[Ticks];

        HitKind _kind = HitKind.None;
        float _shownAt = -100f;
        bool _showing;

        /// <summary>The kind of the marker showing now, for tests and the debug readout.</summary>
        public HitKind Showing => _showing ? _kind : HitKind.None;

        public static HitMarker Create(Transform uiRoot)
        {
            var go = new GameObject("HitMarker");
            go.transform.SetParent(uiRoot, false);
            var marker = go.AddComponent<HitMarker>();
            marker.Build();
            return marker;
        }

        /// <summary>Shows or hides the marker's canvas along with the rest of the HUD.</summary>
        public void SetVisible(bool visible)
        {
            if (_canvas != null) _canvas.gameObject.SetActive(visible);
        }

        void Build()
        {
            // Above the HUD (10) and the damage cues (12), so the confirm is never under a red arc.
            _canvas = UiKit.CreateCanvas("HitMarkerHUD", transform, 13);
            _canvas.GetComponent<GraphicRaycaster>().enabled = false;
            _root = UiKit.NewRect("X", _canvas.transform);
            _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(0.5f, 0.5f);
            _root.anchoredPosition = Vector2.zero;
            _root.sizeDelta = new Vector2(80f, 80f);

            for (int i = 0; i < Ticks; i++)
            {
                // Halos first, so every tick draws over every halo.
                _outerHalo[i] = Tick("OuterHalo", HaloColor, out _outerHaloImg[i]);
                _innerHalo[i] = Tick("Halo", HaloColor, out _innerHaloImg[i]);
            }
            for (int i = 0; i < Ticks; i++)
            {
                _outer[i] = Tick("OuterTick", Gold, out _outerImg[i]);
                _inner[i] = Tick("Tick", White, out _innerImg[i]);
            }
            Hide();
        }

        RectTransform Tick(string name, Color color, out Image image)
        {
            var rt = UiKit.NewRect(name, _root);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            image = UiKit.AddImage(rt, color);
            return rt;
        }

        void OnEnable() => CombatEvents.PlayerHitSomething += OnPlayerHit;

        void OnDisable() => CombatEvents.PlayerHitSomething -= OnPlayerHit;

        void OnPlayerHit(HitOutcome outcome)
        {
            HitKind kind = HitConfirm.Classify(outcome.Hit, outcome.Enemy, outcome.Head, outcome.Severed,
                outcome.Disarmed, outcome.Killed, outcome.HeadshotKill);
            float now = Time.unscaledTime;
            if (!HitConfirm.Replaces(_showing ? _kind : HitKind.None, now - _shownAt, kind)) return;
            bool reshape = !_showing || kind != _kind;
            _kind = kind;
            _shownAt = now;
            _showing = true;
            if (reshape) Shape(kind);
            Animate(0f);
        }

        void Update()
        {
            if (!_showing) return;
            float age = Time.unscaledTime - _shownAt;
            if (age >= HitConfirm.Seconds(_kind)) { Hide(); return; }
            Animate(age);
        }

        /// <summary>Lays out the ticks for a kind: lengths, widths, gap and colours. Only when the kind changes.</summary>
        void Shape(HitKind kind)
        {
            float length = HitConfirm.TickLength(kind), width = HitConfirm.TickWidth(kind), gap = HitConfirm.TickGap(kind);
            Color color = ColorOf(HitConfirm.Tone(kind));
            bool outer = HitConfirm.OuterRing(kind);
            for (int i = 0; i < Ticks; i++)
            {
                float angle = 45f + 90f * i;
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                Place(_inner[i], dir, angle, gap, length, width, 0f);
                Place(_innerHalo[i], dir, angle, gap, length, width, HaloPad);
                _innerImg[i].color = color;
                _innerImg[i].enabled = _innerHaloImg[i].enabled = true;

                // The outer X sits just outside the inner one, a little shorter and thinner.
                Place(_outer[i], dir, angle, gap + length + 4f, length * 0.6f, width * 0.8f, 0f);
                Place(_outerHalo[i], dir, angle, gap + length + 4f, length * 0.6f, width * 0.8f, HaloPad);
                _outerImg[i].enabled = _outerHaloImg[i].enabled = outer;
            }
        }

        static void Place(RectTransform rt, Vector2 dir, float angle, float gap, float length, float width, float pad)
        {
            rt.anchoredPosition = dir * (gap + length * 0.5f);
            rt.sizeDelta = new Vector2(length + 2f * pad, width + 2f * pad);
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>The pop and the fade. Alpha goes on the renderers, so the canvas does not lay itself out again.</summary>
        void Animate(float age)
        {
            float s = HitConfirm.Scale(_kind, age);
            _root.localScale = new Vector3(s, s, 1f);
            float a = HitConfirm.Alpha(_kind, age);
            for (int i = 0; i < Ticks; i++)
            {
                _innerImg[i].canvasRenderer.SetAlpha(a);
                _innerHaloImg[i].canvasRenderer.SetAlpha(a);
                _outerImg[i].canvasRenderer.SetAlpha(a);
                _outerHaloImg[i].canvasRenderer.SetAlpha(a);
            }
        }

        void Hide()
        {
            _showing = false;
            for (int i = 0; i < Ticks; i++)
            {
                _innerImg[i].enabled = _innerHaloImg[i].enabled = false;
                _outerImg[i].enabled = _outerHaloImg[i].enabled = false;
            }
        }

        static Color ColorOf(MarkerTone tone)
        {
            switch (tone)
            {
                case MarkerTone.Gold: return Gold;
                case MarkerTone.Cyan: return Cyan;
                case MarkerTone.Red: return Red;
                default: return White;
            }
        }
    }
}
