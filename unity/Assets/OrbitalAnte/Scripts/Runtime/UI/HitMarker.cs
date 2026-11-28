using OrbitalAnte.Core;
using OrbitalAnte.Creatures;
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
    ///
    /// Creatures say where a hit landed (<see cref="HitZone"/>): a glance off armour is a small grey X, the weak place
    /// gold. The first time a kind's armour turns the player's shots, until they have hit its weak place, one line over
    /// the crosshair says where to aim instead (<see cref="WhereToShoot.Line"/>; when, <see cref="AimLines"/>; remembered
    /// in PlayerPrefs, written only in memory here so a fight never waits on the disk; the end of a run and quitting save
    /// it).
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
        // A glance: duller than the body's white and drawn smaller, so it reads as "that did little", not as a hit to repeat.
        static readonly Color Grey = new Color(0.62f, 0.62f, 0.60f);
        const string GreyHex = "#9E9E99";

        /// <summary>
        /// Where the aim line sits: over the crosshair, inside the damage ring (112), clear of the biggest X. Not under it:
        /// there the line covered what it names (a belly under a lid, a reared body under a head) and met the pick-up
        /// prompts (-92, -96) and the melee TAKEDOWN (-52) (the specialist reviews, pass 7).
        /// </summary>
        const float AimLineY = 70f;
        const string AimKey = "oa.aim.lines";
        static AimLines _aimLines;

        Canvas _canvas;
        RectTransform _root;
        readonly RectTransform[] _inner = new RectTransform[Ticks], _innerHalo = new RectTransform[Ticks];
        readonly RectTransform[] _outer = new RectTransform[Ticks], _outerHalo = new RectTransform[Ticks];
        readonly Image[] _innerImg = new Image[Ticks], _innerHaloImg = new Image[Ticks];
        readonly Image[] _outerImg = new Image[Ticks], _outerHaloImg = new Image[Ticks];

        HitKind _kind = HitKind.None;
        float _shownAt = -100f;
        bool _showing;
        Text _aim;
        float _aimAge = -1f;
        // The alpha last handed to the line's renderer: set only when it changes (it holds at 1 for most of its time up).
        float _aimAlpha = -1f;

        /// <summary>The kind of the marker showing now, for tests and the debug readout.</summary>
        public HitKind Showing => _showing ? _kind : HitKind.None;

        /// <summary>The aim line on screen now, or null, for tests and the debug readout.</summary>
        public string AimLineShowing => _aim != null && _aim.enabled ? _aim.text : null;

        static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>
        /// For the playtest log: the screen points (in pixels) at either end of the aim line's words, along their middle.
        /// False when no line shows. The marker's canvas lays itself over the screen, so its corners are pixels.
        /// </summary>
        public bool AimLineSpan(out Vector2 left, out Vector2 right)
        {
            left = right = default;
            if (_aim == null || !_aim.enabled || _canvas == null || !_canvas.gameObject.activeInHierarchy) return false;
            var rect = (RectTransform)_aim.transform;
            rect.GetWorldCorners(Corners);
            Vector3 middle = (Corners[0] + Corners[2]) * 0.5f;
            float half = Mathf.Min(_aim.preferredWidth, rect.rect.width) * 0.5f * rect.lossyScale.x;
            left = new Vector2(middle.x - half, middle.y);
            right = new Vector2(middle.x + half, middle.y);
            return true;
        }

        /// <summary>Forgets which creatures' aim lines have been shown, with the new-player tips (title screen, Extras).</summary>
        public static void ForgetAimLines()
        {
            _aimLines = new AimLines();
            PlayerPrefs.DeleteKey(AimKey);
        }

        /// <summary>
        /// Drops the memory read from PlayerPrefs (Play mode with domain reload off): its clock is the play session's, and
        /// PlayerPrefs may have been cleared in between.
        /// </summary>
        public static void ResetStatics() => _aimLines = null;

        static AimLines Lines => _aimLines ?? (_aimLines = AimLines.Parse(PlayerPrefs.GetString(AimKey, string.Empty)));

        public static HitMarker Create(Transform uiRoot)
        {
            var go = new GameObject("HitMarker");
            go.transform.SetParent(uiRoot, false);
            var marker = go.AddComponent<HitMarker>();
            marker.Build();
            return marker;
        }

        /// <summary>
        /// Shows or hides the marker's canvas along with the rest of the HUD. This is called at every run's start and on
        /// the title screen, so a line held still under the death page never shows again on the launch pad.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_canvas != null) _canvas.gameObject.SetActive(visible);
            StopAimLine();
        }

        void StopAimLine()
        {
            _aimAge = -1f;
            _aimAlpha = -1f;
            if (_aim != null) _aim.enabled = false;
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

            _aim = HullKit.AsMono(UiKit.Label(_canvas.transform, string.Empty, 15, UiKit.Cream, TextAnchor.MiddleCenter));
            UiKit.Pin((RectTransform)_aim.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, AimLineY), new Vector2(620f, 22f));
            _aim.supportRichText = true;
            UiKit.Halo(_aim, true);
            _aim.enabled = false;
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
                outcome.Disarmed, outcome.Killed, outcome.HeadshotKill, outcome.Zone);
            if (outcome.Zone != HitZone.Plain && outcome.Target is ICreature creature) Learn(creature.Kind, outcome.Zone, kind, outcome.Target);
            float now = Time.unscaledTime;
            if (!HitConfirm.Replaces(_showing ? _kind : HitKind.None, now - _shownAt, kind)) return;
            bool reshape = !_showing || kind != _kind;
            _kind = kind;
            _shownAt = now;
            _showing = true;
            if (reshape) Shape(kind);
            Animate(0f);
        }

        /// <summary>
        /// A hit on a creature's weak place: that kind is known. A glance off its armour: its aim line, if it is due. Both
        /// are rare, so the save in memory and the words are only touched then.
        /// </summary>
        void Learn(string kind, HitZone zone, HitKind shown, Component target)
        {
            if (string.IsNullOrEmpty(kind)) return;
            if (zone == HitZone.Vital)
            {
                if (Lines.Found(kind)) PlayerPrefs.SetString(AimKey, Lines.Store());
                return;
            }
            if (shown != HitKind.Glance || Lines.HasFound(kind) || Lines.Told(kind) >= AimLines.MostTold) return;
            if (!FeelSettings.GuideTips || !Lines.Glanced(kind, Time.unscaledTime)) return;
            PlayerPrefs.SetString(AimKey, Lines.Store());
            _aim.text = "<color=" + GreyHex + ">" + WhereToShoot.ShellWord(kind) + "</color> · " + WhereToShoot.AimLine(kind);
            _aim.enabled = true;
            _aimAge = 0f;
            _aimAlpha = 1f;
            _aim.canvasRenderer.SetAlpha(1f);
            // The playtest log (editor and development builds only) looks along the line while it shows.
            if (Playtest.PlaytestHooks.Log != null) Playtest.PlaytestHooks.AimLine(kind, target, this);
        }

        void Update()
        {
            if (_aimAge >= 0f)
            {
                // Stands still while the game is paused, so a line is never lost under the menu. (A hitstop leaves the time
                // scale above 0, so it runs on through one: 120 ms at most.)
                if (Time.timeScale > 0f) _aimAge += Time.unscaledDeltaTime;
                float a = AimLines.Alpha(_aimAge);
                if (a <= 0f) StopAimLine();
                else if (a != _aimAlpha)
                {
                    _aimAlpha = a;
                    _aim.canvasRenderer.SetAlpha(a);
                }
            }
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
                case MarkerTone.Grey: return Grey;
                default: return White;
            }
        }
    }
}
