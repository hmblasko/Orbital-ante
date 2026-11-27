using OrbitalAnte.Core;
using OrbitalAnte.Creatures;
using OrbitalAnte.Player;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// Shows the player they have been hurt (Matthew, 15 Nov 2026), in the visor's style: a red arc round the
    /// crosshair pointing to the shooter, a red glow along that edge of the view, cracks in the glass for each
    /// suit plate lost, and the hurt sound panned toward the shot. The rules are <see cref="DamageCues"/>.
    ///
    /// Works for whichever <see cref="PlayerHealth"/> it is given: the arena's player, or the suit outside the
    /// ship. Colours and alphas are set on the renderers, so nothing makes the canvas lay itself out again.
    ///
    /// Every arc keeps pointing at what it warns of as the player turns (Ralph loop pass 2): a hit or a sighting keeps
    /// the way it came from, and a creature's wind-up (<see cref="WindupCues"/>) follows the creature itself, so an arc
    /// is followed round by turning toward it. An arc is turned only when it has moved a quarter of a degree. The amber
    /// arcs are hidden while the game is paused and once the player is dead: they are about what is coming.
    ///
    /// Late in the frame, after the player's own scripts have turned the eye (the arena's FpsController turns it in its
    /// LateUpdate), so an arc is never a frame behind the view.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class DamageFeedback : MonoBehaviour
    {
        static readonly Color Red = new Color(0.95f, 0.16f, 0.12f);
        // The "you have been seen" warning (UI/ThreatCues): amber, on a ring just outside the red one, so
        // the eye learns that amber comes before red.
        static readonly Color Amber = new Color(1f, 0.72f, 0.18f);
        const float MarkerRadius = 112f;
        const float MarkerSpan = 46f;
        const float WarnRadius = 140f;
        const float WarnSpan = 30f;

        Canvas _canvas;
        PlayerHealth _health;
        Transform _view;
        System.Func<bool> _visible;

        readonly Image[] _markers = new Image[DamageCues.MaxMarkers];
        readonly float[] _markerAngle = new float[DamageCues.MaxMarkers];
        readonly float[] _markerTime = new float[DamageCues.MaxMarkers];
        readonly Image[] _edges = new Image[4];
        readonly float[] _edgeShare = new float[4];
        readonly Image[] _cracks = new Image[DamageCues.MaxCracks];
        readonly Image[] _warns = new Image[ThreatCues.MaxMarkers];
        readonly float[] _warnAngle = new float[ThreatCues.MaxMarkers];
        readonly float[] _warnTime = new float[ThreatCues.MaxMarkers];
        readonly float[] _warnBeat = new float[ThreatCues.MaxMarkers];
        // Which way, in the world, each red and amber arc points: the arcs are turned to it every frame, so they keep
        // pointing at the danger as the player turns. Zero when the way is not known (the arc keeps its first angle).
        readonly Vector3[] _markerDir = new Vector3[DamageCues.MaxMarkers];
        readonly Vector3[] _warnDir = new Vector3[ThreatCues.MaxMarkers];
        // Creatures winding up (UI/WindupCues): each arc follows its creature and reads its stage every frame.
        readonly Image[] _beasts = new Image[WindupCues.MaxMarkers];
        readonly IWindup[] _beastSource = new IWindup[WindupCues.MaxMarkers];
        readonly WindupStage[] _beastStage = new WindupStage[WindupCues.MaxMarkers];
        readonly float[] _beastSince = new float[WindupCues.MaxMarkers];
        readonly float[] _beastEnded = new float[WindupCues.MaxMarkers];
        readonly float[] _beastAlpha = new float[WindupCues.MaxMarkers];
        readonly float[] _beastAngle = new float[WindupCues.MaxMarkers];
        readonly float[] _beastSpan = new float[WindupCues.MaxMarkers];
        float _lastSting = -100f;
        readonly PulseClock _pulse = new PulseClock();
        float _glowStrength, _glowTime = -100f, _lastSound = -100f;
        int _shownCracks = -1;
        Texture2D _edgeTexture, _crackTexture;
        Sprite _edgeSprite, _crackSprite;

        public static DamageFeedback Create(Transform uiRoot)
        {
            var go = new GameObject("DamageFeedback");
            go.transform.SetParent(uiRoot, false);
            var fb = go.AddComponent<DamageFeedback>();
            fb.Build();
            return fb;
        }

        /// <summary>
        /// Starts showing hits on <paramref name="health"/>, seen from <paramref name="view"/> (the eye). Null stops.
        /// <paramref name="visible"/>, if given, hides everything while it says false (looking at a screen, aboard).
        /// </summary>
        public void Watch(PlayerHealth health, Transform view, System.Func<bool> visible = null)
        {
            if (_health == health && _view == view) { _visible = visible; return; }
            if (_health != null) _health.Damaged -= OnDamaged;
            _health = health;
            _view = view;
            _visible = visible;
            if (_health != null) _health.Damaged += OnDamaged;
            for (int i = 0; i < _markerTime.Length; i++) _markerTime[i] = -100f;
            for (int i = 0; i < _warnTime.Length; i++) _warnTime[i] = -100f;
            for (int i = 0; i < _beastSource.Length; i++) _beastSource[i] = null;
            _glowTime = -100f;
            _shownCracks = -1;
            if (_canvas != null) _canvas.gameObject.SetActive(_health != null);
        }

        void OnEnable()
        {
            AI.CombatWorld.PlayerSpotted += OnSpotted;
            AI.CombatWorld.SquadSent += OnSquadSent;
            AI.CombatWorld.CreatureWindup += OnWindup;
        }

        void OnDisable()
        {
            AI.CombatWorld.PlayerSpotted -= OnSpotted;
            AI.CombatWorld.SquadSent -= OnSquadSent;
            AI.CombatWorld.CreatureWindup -= OnWindup;
            for (int i = 0; i < _beastSource.Length; i++) _beastSource[i] = null;
        }

        bool Showing => _health != null && _health.Alive && _view != null && (_visible == null || _visible());

        /// <summary>
        /// The eye's place and axes, read once: every read of a transform's axes or place walks up its parents, so a
        /// frame that turns ten arcs reads them once, not thirty times.
        /// </summary>
        readonly struct Eye
        {
            readonly Vector3 _at, _right, _up, _forward;

            Eye(Transform view)
            {
                view.GetPositionAndRotation(out _at, out Quaternion turn);
                _right = turn * Vector3.right;
                _up = turn * Vector3.up;
                _forward = turn * Vector3.forward;
            }

            public static Eye Of(Transform view) => new Eye(view);

            /// <summary>The screen angle of a way in the world (a unit vector), as the damage markers measure it.</summary>
            public float AngleOf(Vector3 way) =>
                DamageCues.MarkerAngle(Vector3.Dot(way, _right), Vector3.Dot(way, _up), Vector3.Dot(way, _forward));

            /// <summary>The screen angle toward a point in the world.</summary>
            public float AngleToward(Vector3 point)
            {
                Vector3 to = point - _at;
                return to.sqrMagnitude < 1e-6f ? 90f : AngleOf(to.normalized);
            }

            /// <summary>The way from the eye to a point, or zero if the point is at the eye.</summary>
            public Vector3 WayTo(Vector3 point)
            {
                Vector3 to = point - _at;
                return to.sqrMagnitude < 1e-6f ? Vector3.zero : to.normalized;
            }
        }

        void OnSpotted(Vector3 from, float beat, bool hidden)
        {
            if (!Showing) return;
            // Game time, like the enemy's beat: in Focus or hitstop the arc lasts as long as the beat really does.
            float now = Time.time;
            Eye eye = Eye.Of(_view);
            float angle = eye.AngleToward(from);
            // A squad seen together shares one arc; otherwise the oldest arc makes way.
            int slot = -1, oldest = 0;
            for (int i = 0; i < _warnTime.Length; i++)
            {
                if (now - _warnTime[i] < ThreatCues.Seconds(_warnBeat[i]) && Mathf.Abs(Mathf.DeltaAngle(_warnAngle[i], angle)) < ThreatCues.SameWayDegrees) { slot = i; break; }
                if (_warnTime[i] < _warnTime[oldest]) oldest = i;
            }
            if (slot < 0) slot = oldest;
            _warnAngle[slot] = angle;
            _warnDir[slot] = eye.WayTo(from);
            _warnTime[slot] = now;
            _warnBeat[slot] = beat;
            _warns[slot].rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle - WarnSpan * 0.5f);
            float real = Time.unscaledTime;
            if (ThreatCues.Sting(hidden, real - _lastSting))
            {
                _lastSting = real;
                Audio.AudioDirector.PlayLocal("enemy.spotted", 1f, 1f, DamageCues.SoundPan(angle));
            }
        }

        /// <summary>
        /// A creature has begun a warning: an arc on the amber ring that follows it until its strike is over. A creature
        /// already shown keeps its arc; otherwise a free arc, then one fading out, then the arc of the creature furthest
        /// away, if this one is nearer: the nearest dangers are the ones shown (each is heard in any case).
        /// </summary>
        void OnWindup(IWindup source)
        {
            if (!Showing || source == null) return;
            Vector3 eye = _view.position;
            float furthest = (source.WindupPoint - eye).sqrMagnitude;
            int slot = -1, free = -1, fading = -1, far = -1;
            for (int i = 0; i < _beastSource.Length; i++)
            {
                IWindup shown = _beastSource[i];
                if (ReferenceEquals(shown, source)) { slot = i; break; }
                if (shown == null) { if (free < 0) free = i; continue; }
                if (_beastStage[i] == WindupStage.None || (shown is Object o && o == null)) { if (fading < 0) fading = i; continue; }
                float d = (shown.WindupPoint - eye).sqrMagnitude;
                if (d > furthest) { furthest = d; far = i; }
            }
            if (slot < 0) slot = free >= 0 ? free : fading >= 0 ? fading : far;
            if (slot < 0) return;
            bool fresh = !ReferenceEquals(_beastSource[slot], source) || _beastStage[slot] == WindupStage.None;
            _beastSource[slot] = source;
            _beastStage[slot] = source.Windup;
            _beastSince[slot] = Time.time;
            if (fresh)
            {
                // Turned straight to it on its first frame, never swung round from where another arc was, and as bright
                // at once as its stage says: the first frame must be seen.
                _beastAngle[slot] = Eye.Of(_view).AngleToward(source.WindupPoint);
                _beastSpan[slot] = WindupCues.Span(_beastStage[slot], source.WindupProgress, 0f);
                _beastAlpha[slot] = WindupCues.Alpha(_beastStage[slot], true);
                SetArc(_beasts[slot], _beastAngle[slot], _beastSpan[slot]);
            }
        }

        static void SetArc(Image arc, float angle, float span)
        {
            arc.fillAmount = span / 360f;
            // Centre the arc on the angle: the fill runs anticlockwise from the rect's right.
            arc.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle - span * 0.5f);
        }

        void OnSquadSent(Vector3 from)
        {
            if (!Showing) return;
            Audio.AudioDirector.PlayLocal("enemy.radio.call", 0.9f, 1f, DamageCues.SoundPan(Eye.Of(_view).AngleToward(from)));
        }

        void OnDestroy()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
            if (_edgeSprite != null) Destroy(_edgeSprite);
            if (_edgeTexture != null) Destroy(_edgeTexture);
            if (_crackSprite != null) Destroy(_crackSprite);
            if (_crackTexture != null) Destroy(_crackTexture);
        }

        void Build()
        {
            _canvas = UiKit.CreateCanvas("DamageHUD", transform, 12);
            _canvas.GetComponent<GraphicRaycaster>().enabled = false;

            // Cracks first, so the glow and the markers draw over them.
            _crackTexture = CrackTexture(128, 7);
            _crackSprite = Sprite.Create(_crackTexture, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            for (int i = 0; i < _cracks.Length; i++)
            {
                RectTransform rt = UiKit.NewRect("Crack" + i, _canvas.transform);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = CrackPlaces[i];
                float size = CrackSizes[i];
                rt.sizeDelta = new Vector2(size, size);
                rt.localRotation = Quaternion.Euler(0f, 0f, 61f * i + 17f);
                if (i % 2 == 1) rt.localScale = new Vector3(-1f, 1f, 1f);
                _cracks[i] = UiKit.AddImage(rt, new Color(0.92f, 0.95f, 1f, 0.6f), _crackSprite);
                _cracks[i].type = Image.Type.Simple;
                _cracks[i].enabled = false;
            }

            // Edge glow: a soft band along each side, brightest at the edge (right, top, left, bottom).
            _edgeTexture = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "HurtEdge" };
            var px = new Color32[32];
            for (int i = 0; i < 32; i++)
            {
                float t = 1f - i / 31f;
                px[i] = new Color(1f, 1f, 1f, t * t);
            }
            _edgeTexture.SetPixels32(px);
            _edgeTexture.Apply(false, true);
            _edgeSprite = Sprite.Create(_edgeTexture, new Rect(0f, 0f, 1f, 32f), new Vector2(0.5f, 0f), 32f, 0, SpriteMeshType.FullRect);
            float[] turn = { 90f, 180f, -90f, 0f };
            Vector2[] edge = { new Vector2(1f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0f) };
            for (int i = 0; i < 4; i++)
            {
                RectTransform rt = UiKit.NewRect("HurtEdge" + i, _canvas.transform);
                bool sideways = i == 0 || i == 2;
                rt.anchorMin = rt.anchorMax = edge[i];
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = Vector2.zero;
                Vector2 screen = UiKit.ReferenceResolution;
                rt.sizeDelta = new Vector2((sideways ? screen.y : screen.x) * 1.6f, 150f);
                rt.localRotation = Quaternion.Euler(0f, 0f, turn[i]);
                _edges[i] = UiKit.AddImage(rt, Red, _edgeSprite);
                _edges[i].type = Image.Type.Simple;
                _edges[i].enabled = false;
            }

            // Direction markers: short red arcs on a ring round the crosshair.
            for (int i = 0; i < _markers.Length; i++)
            {
                RectTransform rt = UiKit.NewRect("HurtMarker" + i, _canvas.transform);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                // The ring sprite's band is an eighth of its outer radius: about 15 px here.
                float outer = MarkerRadius + 15f;
                rt.sizeDelta = new Vector2(2f * outer, 2f * outer);
                Image img = UiKit.AddImage(rt, Red, UiKit.Ring);
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Radial360;
                img.fillOrigin = (int)Image.Origin360.Right;
                img.fillClockwise = false;
                img.fillAmount = MarkerSpan / 360f;
                img.enabled = false;
                _markers[i] = img;
                _markerTime[i] = -100f;
            }
            // Warning arcs: shorter amber arcs on a ring outside the red one.
            for (int i = 0; i < _warns.Length; i++)
            {
                RectTransform rt = UiKit.NewRect("SeenMarker" + i, _canvas.transform);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                float outer = WarnRadius + 10f;
                rt.sizeDelta = new Vector2(2f * outer, 2f * outer);
                Image img = UiKit.AddImage(rt, Amber, UiKit.Ring);
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Radial360;
                img.fillOrigin = (int)Image.Origin360.Right;
                img.fillClockwise = false;
                img.fillAmount = WarnSpan / 360f;
                img.enabled = false;
                _warns[i] = img;
                _warnTime[i] = -100f;
            }
            // Creature wind-ups: on the same amber ring, because they mean the same (danger from there, coming).
            for (int i = 0; i < _beasts.Length; i++)
            {
                RectTransform rt = UiKit.NewRect("WindupMarker" + i, _canvas.transform);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                float outer = WarnRadius + 10f;
                rt.sizeDelta = new Vector2(2f * outer, 2f * outer);
                Image img = UiKit.AddImage(rt, Amber, UiKit.Ring);
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Radial360;
                img.fillOrigin = (int)Image.Origin360.Right;
                img.fillClockwise = false;
                img.fillAmount = WindupCues.StartSpan / 360f;
                img.enabled = false;
                _beasts[i] = img;
            }
            _canvas.gameObject.SetActive(false);
        }

        // Where the cracks sit, in the order they appear: around the edges of the visor, never over the
        // crosshair or the gauges' numbers.
        static readonly Vector2[] CrackPlaces =
        {
            new Vector2(-470f, 250f), new Vector2(430f, 270f), new Vector2(-540f, -20f),
            new Vector2(520f, 40f), new Vector2(-180f, 330f), new Vector2(200f, -330f),
        };
        static readonly float[] CrackSizes = { 230f, 200f, 180f, 210f, 170f, 190f };

        void OnDamaged(float amount, Vector3 bulletDirection)
        {
            float now = Time.unscaledTime;
            float angle = 270f;
            Vector3 toShooter = Vector3.zero;
            if (_view != null && bulletDirection.sqrMagnitude > 1e-8f)
            {
                toShooter = -bulletDirection.normalized;
                angle = Eye.Of(_view).AngleOf(toShooter);
            }

            // A hit from about the same way as one still showing refreshes it rather than taking a new slot.
            int slot = -1, oldest = 0;
            for (int i = 0; i < _markerTime.Length; i++)
            {
                if (now - _markerTime[i] < DamageCues.MarkerSeconds && Mathf.Abs(Mathf.DeltaAngle(_markerAngle[i], angle)) < 20f) { slot = i; break; }
                if (_markerTime[i] < _markerTime[oldest]) oldest = i;
            }
            if (slot < 0) slot = oldest;
            _markerAngle[slot] = angle;
            _markerDir[slot] = toShooter;
            _markerTime[slot] = now;
            // Centre the arc on the angle: the fill runs anticlockwise from the rect's right.
            _markers[slot].rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle - MarkerSpan * 0.5f);

            // Edge glow: a new hit replaces a fading one only if it is at least as strong as what is left.
            float strength = DamageCues.GlowStrength(amount);
            if (strength >= DamageCues.GlowFade(_glowStrength, now - _glowTime))
            {
                _glowStrength = strength;
                _glowTime = now;
                DamageCues.EdgeShares(angle, _edgeShare);
            }

            if (DamageCues.SoundAllowed(now, _lastSound))
            {
                _lastSound = now;
                Audio.AudioDirector.PlayLocal("player.hurt", 1f, Random.Range(0.95f, 1.05f), DamageCues.SoundPan(angle));
            }
        }

        void LateUpdate()
        {
            if (_health == null) return;
            bool show = _visible == null || _visible();
            float now = Time.unscaledTime;

            bool looking = _view != null;
            Eye eye = looking ? Eye.Of(_view) : default;
            for (int i = 0; i < _markers.Length; i++)
            {
                float a = show ? DamageCues.MarkerAlpha(now - _markerTime[i]) : 0f;
                bool on = a > 0.01f;
                if (_markers[i].enabled != on) _markers[i].enabled = on;
                if (!on) continue;
                _markers[i].canvasRenderer.SetAlpha(a);
                if (looking && _markerDir[i] != Vector3.zero) Follow(_markers[i], ref _markerAngle[i], eye.AngleOf(_markerDir[i]), MarkerSpan);
            }

            // The amber arcs warn of what is coming: none while paused (the game's clock has stopped) or once dead.
            bool amber = show && looking && _health.Alive && !GameSession.Paused;
            // Creatures first, so a "seen" arc knows whether a creature that has taken aim is beside it.
            for (int i = 0; i < _beasts.Length; i++) StepWindup(i, amber, in eye);

            for (int i = 0; i < _warns.Length; i++)
            {
                float a = amber ? ThreatCues.Alpha(Time.time - _warnTime[i], _warnBeat[i]) : 0f;
                bool on = a > 0.01f;
                if (_warns[i].enabled != on) _warns[i].enabled = on;
                if (!on) continue;
                if (_warnDir[i] != Vector3.zero) Follow(_warns[i], ref _warnAngle[i], eye.AngleOf(_warnDir[i]), WarnSpan);
                if (Crowded(_warnAngle[i])) a *= WindupCues.CrowdedShare;
                _warns[i].canvasRenderer.SetAlpha(a);
            }

            float glow = show ? DamageCues.GlowFade(_glowStrength, now - _glowTime) * 0.75f : 0f;
            for (int i = 0; i < 4; i++)
            {
                float a = glow * _edgeShare[i];
                bool on = a > 0.01f;
                if (_edges[i].enabled != on) _edges[i].enabled = on;
                if (on) _edges[i].canvasRenderer.SetAlpha(a);
            }

            // The pulse on the last plates (UI/VitalsCues): game time, so it stops with a pause and slows with Focus.
            if (_pulse.Step(Time.deltaTime, _health.Fraction, show && _health.Alive))
                Audio.AudioDirector.PlayLocal("player.armour.low", PulseClock.Volume(_health.Fraction));

            int cracks = show && _health.Alive ? DamageCues.Cracks(_health.Fraction) : 0;
            if (cracks != _shownCracks)
            {
                _shownCracks = cracks;
                for (int i = 0; i < _cracks.Length; i++)
                {
                    bool on = i < cracks;
                    if (_cracks[i].enabled != on) _cracks[i].enabled = on;
                }
            }
        }

        /// <summary>Turns an arc of fixed width to where it now points, if that has moved enough to see.</summary>
        static void Follow(Image arc, ref float shown, float angle, float span)
        {
            if (!WindupCues.Turned(shown, angle)) return;
            shown = angle;
            arc.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle - span * 0.5f);
        }

        /// <summary>
        /// One creature's arc for this frame: its stage, countdown and threat read from the creature, its brightness and
        /// width from <see cref="WindupCues"/>, turned toward it. When the warning is over (or the creature is gone) it
        /// fades where it last pointed, then lets the arc go.
        /// </summary>
        void StepWindup(int i, bool show, in Eye eye)
        {
            IWindup source = _beastSource[i];
            Image arc = _beasts[i];
            if (source == null)
            {
                if (arc.enabled) arc.enabled = false;
                return;
            }
            float now = Time.time;
            // A creature destroyed while it warned is a Unity null: its arc fades where it was.
            bool gone = source is Object o && o == null;
            WindupStage stage = gone ? WindupStage.None : source.Windup;
            if (stage != _beastStage[i])
            {
                if (stage == WindupStage.None) _beastEnded[i] = now;
                _beastStage[i] = stage;
                _beastSince[i] = now;
            }
            float a;
            if (stage != WindupStage.None)
            {
                bool threatens = stage != WindupStage.Striking || source.Threatens;
                a = _beastAlpha[i] = WindupCues.Ease(_beastAlpha[i], WindupCues.Alpha(stage, threatens), Time.deltaTime);
            }
            else
            {
                float since = now - _beastEnded[i];
                if (since >= WindupCues.FadeSeconds)
                {
                    _beastSource[i] = null;
                    if (arc.enabled) arc.enabled = false;
                    return;
                }
                a = WindupCues.Fade(_beastAlpha[i], since);
            }
            if (!show) a = 0f;
            bool on = a > 0.01f;
            if (arc.enabled != on) arc.enabled = on;
            if (!on) return;
            arc.canvasRenderer.SetAlpha(a);
            float span = stage == WindupStage.None ? _beastSpan[i] : WindupCues.Span(stage, source.WindupProgress, now - _beastSince[i]);
            float angle = gone ? _beastAngle[i] : eye.AngleToward(source.WindupPoint);
            if (Mathf.Abs(span - _beastSpan[i]) >= WindupCues.SpanStep || (stage == WindupStage.Striking && span != _beastSpan[i]) || WindupCues.Turned(_beastAngle[i], angle))
            {
                _beastSpan[i] = span;
                _beastAngle[i] = angle;
                SetArc(arc, angle, span);
            }
        }

        /// <summary>Is a "seen" arc pointing here beside a creature's arc that has taken aim (and is showing)?</summary>
        bool Crowded(float seenAngle)
        {
            for (int i = 0; i < _beasts.Length; i++)
                if (_beasts[i].enabled && _beastSource[i] != null && WindupCues.Crowds(_beastStage[i], _beastAngle[i], seenAngle)) return true;
            return false;
        }

        /// <summary>
        /// A crack in the glass, drawn once: a few jagged lines out from a small chipped point, with branches.
        /// The same every run (a fixed little number sequence, not the run's seed: it is decoration).
        /// </summary>
        static Texture2D CrackTexture(int size, int arms)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
                name = "VisorCrack",
            };
            var alpha = new float[size * size];
            uint state = 0x9E3779B9u;
            float Next()
            {
                state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                return (state & 0xFFFFFF) / (float)0x1000000;
            }
            void Line(float x0, float y0, float x1, float y1, float strength)
            {
                int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) * 2f);
                for (int s = 0; s <= steps; s++)
                {
                    float t = steps > 0 ? s / (float)steps : 0f;
                    float x = Mathf.Lerp(x0, x1, t), y = Mathf.Lerp(y0, y1, t);
                    int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
                    for (int dy = 0; dy <= 1; dy++)
                    for (int dx = 0; dx <= 1; dx++)
                    {
                        int px = ix + dx, py = iy + dy;
                        if (px < 0 || py < 0 || px >= size || py >= size) continue;
                        float w = (1f - Mathf.Abs(x - px)) * (1f - Mathf.Abs(y - py));
                        int k = py * size + px;
                        alpha[k] = Mathf.Max(alpha[k], Mathf.Clamp01(w * 1.6f) * strength);
                    }
                }
            }
            float c = size * 0.5f;
            for (int a = 0; a < arms; a++)
            {
                float dir = (a + Next() * 0.6f) / arms * Mathf.PI * 2f;
                float x = c, y = c, strength = 1f;
                float length = size * (0.28f + Next() * 0.2f);
                int segments = 6;
                for (int s = 0; s < segments; s++)
                {
                    dir += (Next() - 0.5f) * 0.7f;
                    float step = length / segments;
                    float nx = x + Mathf.Cos(dir) * step, ny = y + Mathf.Sin(dir) * step;
                    Line(x, y, nx, ny, strength);
                    if (Next() < 0.35f)
                    {
                        float bd = dir + (Next() < 0.5f ? -1f : 1f) * (0.5f + Next() * 0.5f);
                        Line(nx, ny, nx + Mathf.Cos(bd) * step * 0.8f, ny + Mathf.Sin(bd) * step * 0.8f, strength * 0.7f);
                    }
                    x = nx; y = ny;
                    strength *= 0.86f;
                }
            }
            // A small chipped point where it was struck.
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                if (d < 4f) alpha[y * size + x] = Mathf.Max(alpha[y * size + x], 0.8f * (1f - d / 4f));
            }
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(1f, 1f, 1f, alpha[i]);
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }
    }
}
