using OrbitalAnte.Core;
using OrbitalAnte.UI;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.Combat
{
    /// <summary>
    /// Deep Breath in the game: reads the button, runs the rule (<see cref="DeepBreath"/>), hands the slowing to
    /// <see cref="TimeFlow"/> (the one place the clock is set), gives breath back for limbs and kills, and draws
    /// a small meter and a cool rim round the view.
    ///
    /// One of these lives on the game's root object, not on a player, so it works wherever the on-foot controls
    /// are live (the arena, the cabin, outside the ship) and stops by itself in the pilot's seat, where those
    /// controls are switched off.
    /// </summary>
    public sealed class DeepBreathFx : MonoBehaviour
    {
        // The meter is drawn in steps this fine; a smaller change is not worth rebuilding the picture for.
        const int Steps = 200;
        const float BarWidth = 200f;

        static DeepBreathFx _instance;

        /// <summary>
        /// How slowed the world is by a deep breath right now, 0 (not at all) to 1 (fully). 0 when there is
        /// no Deep Breath in the scene. For sound and anything else that wants to follow it.
        /// </summary>
        public static float Amount { get; private set; }

        /// <summary>The rule itself, for readouts and tests.</summary>
        public DeepBreath Breath { get; private set; }
        /// <summary>Holds the breath as if the button were down. For tests.</summary>
        public bool ForceHold;
        /// <summary>True while the meter is on screen.</summary>
        public bool MeterShown => _panel != null && _panel.gameObject.activeSelf;

        DeepBreathConfig _config;
        Canvas _canvas;
        RectTransform _panel;
        Image _rim, _back, _fill, _mark;
        Text _label;
        Texture2D _rimTexture, _whiteTexture;
        Sprite _rimSprite, _whiteSprite;
        int _shownStep = -1;
        float _alpha;

        void Awake()
        {
            _config = DeepBreathConfig.Load();
            Breath = new DeepBreath(_config.rules);
            BuildHud();
        }

        void OnEnable()
        {
            _instance = this;
            CombatEvents.PlayerHitSomething += OnPlayerHit;
        }

        void OnDisable()
        {
            CombatEvents.PlayerHitSomething -= OnPlayerHit;
            // Never leave the world slowed behind us.
            TimeFlow.Breath = 1f;
            if (_instance == this)
            {
                _instance = null;
                Amount = 0f;
            }
        }

        /// <summary>A new run: full meter, world at normal speed.</summary>
        public void ResetForNewRun()
        {
            if (Breath != null) Breath.Reset();
            TimeFlow.Breath = 1f;
            if (_instance == this) Amount = 0f;
        }

        public static void ResetStatics()
        {
            _instance = null;
            Amount = 0f;
        }

        void OnPlayerHit(HitOutcome outcome)
        {
            // Only living enemies give breath back: shooting bits off your own ship does not count.
            if (!outcome.Enemy) return;
            if (outcome.Killed) Breath.Earn(Breath.Rules.perKill);
            else if (outcome.Severed) Breath.Earn(Breath.Rules.perLimb);
        }

        void Update()
        {
            // The on-foot controls are switched off in the pilot's seat, in menus and while paused, so the
            // button reads "not held" there and the breath lets go by itself.
            bool onFoot = ForceHold || (GameInput.Gameplay != null && GameInput.Gameplay.enabled);

            if (!GameSession.Paused)
            {
                bool held = ForceHold || (GameInput.DeepBreath != null && GameInput.DeepBreath.IsPressed());
                // Real time, because the breath is what slows game time. Capped so a hitch cannot eat the meter.
                Breath.Tick(Mathf.Min(Time.unscaledDeltaTime, 0.1f), held);
                TimeFlow.Breath = Breath.TimeScale;
                Amount = Breath.Amount;
            }

            DrawHud(onFoot && !GameSession.Paused && !OrbitalAnte.Cockpit.CockpitDash.WatchingScreen);
        }

        // ------------------------------------------------------------------ what you see

        void BuildHud()
        {
            _canvas = UiKit.CreateCanvas("DeepBreathHUD", transform, 8);
            _canvas.GetComponent<GraphicRaycaster>().enabled = false;

            // The rim: clear in the middle, cool colour toward the corners. One stretched picture, only drawn
            // while the world is slowed.
            const int size = 64;
            _rimTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "BreathRim" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v) / 1.25f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, r));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a * a);
                }
            }
            _rimTexture.SetPixels32(pixels);
            _rimTexture.Apply(false, true);
            _rimSprite = Sprite.Create(_rimTexture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            RectTransform rim = UiKit.NewRect("Rim", _canvas.transform);
            UiKit.Stretch(rim);
            _rim = UiKit.AddImage(rim, _config.colour, _rimSprite);
            _rim.type = Image.Type.Simple;
            _rim.enabled = false;

            // The meter: a thin bar low in the middle, above the health bar and below the suit's notice.
            _panel = UiKit.NewRect("Breath", _canvas.transform);
            UiKit.Pin(_panel, new Vector2(0.5f, 0f), new Vector2(0f, 112f), new Vector2(BarWidth, 12f));
            _back = UiKit.AddImage(_panel, new Color(0f, 0f, 0f, 0.55f), UiKit.Rounded);

            RectTransform fill = UiKit.NewRect("Fill", _panel);
            UiKit.Stretch(fill);
            fill.offsetMin = new Vector2(2f, 2f);
            fill.offsetMax = new Vector2(-2f, -2f);
            // White underneath; its colour is set on the renderer, which does not rebuild the canvas.
            _fill = UiKit.AddImage(fill, Color.white);
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Horizontal;
            _fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            // A filled image needs a sprite to cut.
            _whiteTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "BreathBar" };
            _whiteTexture.SetPixels32(new[] { (Color32)Color.white, (Color32)Color.white, (Color32)Color.white, (Color32)Color.white });
            _whiteTexture.Apply(false, true);
            _whiteSprite = Sprite.Create(_whiteTexture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
            _fill.sprite = _whiteSprite;

            // A notch at the least you need to begin, so the rule can be seen and not only learnt.
            RectTransform mark = UiKit.NewRect("Least", _panel);
            mark.anchorMin = new Vector2(0f, 0f);
            mark.anchorMax = new Vector2(0f, 1f);
            mark.pivot = new Vector2(0.5f, 0.5f);
            mark.anchoredPosition = new Vector2(2f + (BarWidth - 4f) * Mathf.Clamp01(_config.rules.leastToStart), 0f);
            mark.sizeDelta = new Vector2(2f, 0f);
            _mark = UiKit.AddImage(mark, UiKit.Ink);

            _label = UiKit.Label(_panel, "BREATH", 13, UiKit.Muted, TextAnchor.MiddleRight);
            UiKit.Pin((RectTransform)_label.transform, new Vector2(0f, 0.5f), new Vector2(-70f, 0f), new Vector2(62f, 16f));

            _panel.gameObject.SetActive(false);
        }

        void DrawHud(bool allowed)
        {
            if (_panel == null) return;

            // The rim follows the slowing itself, so it fades in and out with the world.
            float rim = allowed ? Breath.Amount * _config.rimStrength : 0f;
            bool rimOn = rim > 0.004f;
            if (_rim.enabled != rimOn) _rim.enabled = rimOn;
            if (rimOn) _rim.canvasRenderer.SetAlpha(rim);

            // The meter only shows when there is something to say: not full, or in use.
            bool want = allowed && (Breath.Meter < 0.999f || Breath.Active || Breath.Amount > 0f);
            _alpha = Mathf.MoveTowards(_alpha, want ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            bool show = _alpha > 0f;
            if (_panel.gameObject.activeSelf != show)
            {
                _panel.gameObject.SetActive(show);
                _shownStep = -1;
            }
            if (!show) return;

            int step = Mathf.RoundToInt(Breath.Meter * Steps);
            if (step != _shownStep)
            {
                _shownStep = step;
                _fill.fillAmount = step / (float)Steps;
            }

            // Cool when a breath can be taken, grey when there is too little, a red blink if you try anyway.
            Color colour = Breath.Active || Breath.CanStart ? _config.colour : UiKit.Muted;
            if (Breath.Refused && Mathf.Repeat(Time.unscaledTime, 0.4f) < 0.2f) colour = UiKit.Danger;
            colour.a = _alpha;
            _fill.canvasRenderer.SetColor(colour);
            _back.canvasRenderer.SetAlpha(_alpha);
            _mark.canvasRenderer.SetAlpha(_alpha);
            _label.canvasRenderer.SetAlpha(_alpha);
        }

        void OnDestroy()
        {
            if (_rimSprite != null) Destroy(_rimSprite);
            if (_rimTexture != null) Destroy(_rimTexture);
            if (_whiteSprite != null) Destroy(_whiteSprite);
            if (_whiteTexture != null) Destroy(_whiteTexture);
        }
    }
}
