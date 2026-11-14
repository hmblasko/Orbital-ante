using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.Launch;
using OrbitalAnte.Look;
using OrbitalAnte.Navigation;
using OrbitalAnte.Planets;
using OrbitalAnte.Ship;
using OrbitalAnte.Space;
using OrbitalAnte.UI;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.Cockpit
{
    /// <summary>Draws a <see cref="DashDrawing"/>'s triangles as one piece of UI. The words are separate Text objects.</summary>
    public sealed class DashGraphic : MaskableGraphic
    {
        public DashDrawing Drawing;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            DashDrawing d = Drawing;
            if (d == null) return;
            Rect r = GetPixelAdjustedRect();
            float sx = r.width / DashDrawing.Width, sy = r.height / DashDrawing.Height;
            UIVertex v = UIVertex.simpleVert;
            int count = d.VertexCount - d.VertexCount % 3;
            for (int i = 0; i < count; i++)
            {
                DashColor c = d.Colors[i];
                v.position = new Vector3(r.xMin + d.X[i] * sx, r.yMin + d.Y[i] * sy, 0f);
                v.color = new Color(c.R, c.G, c.B, c.A) * color;
                vh.AddVert(v);
            }
            for (int i = 0; i < count; i += 3) vh.AddTriangle(i, i + 1, i + 2);
        }
    }

    /// <summary>
    /// The cockpit as you see it from the seat: the dash (in whichever design is chosen), the ship's fuel and
    /// air, and the three outside cameras. Lives beside the ship.
    ///
    /// The dash is painted by plain rules (<see cref="DashDesigns"/>) into a list of triangles and words, which
    /// this shows either on a panel standing on the desk or as a strip along the bottom of the screen. One
    /// button tips your head down to the dash's camera screen, and the camera's picture then fills your view;
    /// another steps through the cameras. You keep flying the whole time, and it is always first person: the
    /// pictures come from cameras bolted to your own ship.
    /// </summary>
    public sealed class CockpitDash : MonoBehaviour
    {
        /// <summary>True while a ship camera's picture fills the view (so other screens can keep out of the way).</summary>
        public static bool WatchingScreen { get; private set; }

        public CockpitConfig Config { get; private set; }
        public DashModel Model { get; private set; }
        public ShipSupplies Supplies { get; private set; }
        public ScreenView View { get; private set; }
        public DashDrawing Drawing { get; private set; }
        public DashStyle Style { get; private set; }
        public Camera[] Cameras { get; private set; }
        /// <summary>True while the dash is being shown at all (someone is in the cabin).</summary>
        public bool Shown { get; private set; }
        /// <summary>How many words the dash is showing. For tests.</summary>
        public int WordsShown { get; private set; }

        SolarSystemWorld _world;
        Ship.Ship _ship;
        Astronaut _crew;
        TestCraft _craft;
        Canvas _canvas, _overlay;
        DashGraphic _graphic;
        RawImage _feed;
        RenderTexture _feedTexture;
        Text _overlayText;
        readonly List<Text> _words = new List<Text>();
        readonly List<GameObject> _fittings = new List<GameObject>();
        /// <summary>The modelled console and its instruments, or null without the Console model.</summary>
        public ConsoleInstruments Console { get; private set; }
        /// <summary>What the console's instruments show.</summary>
        public readonly ConsoleGauges Gauges = new ConsoleGauges();
        float _nextDraw, _overlayUntil;
        /// <summary>Unity's built-in "UI" layer. The dash lives on it so the ship's own cameras can leave it out.</summary>
        public const int DashLayer = 5;
        int _pilotKey = -1;
        string _pilotWords = string.Empty;
        int _feedCamera = -1, _frame, _lastPicture;
        bool _drawnOnce;
        // 0 not told yet, 1 drawn plain (the little screen), 2 drawn with the full finish (the whole view).
        readonly int[] _finish = new int[CameraRig.Count];
        string _overlayWords = string.Empty;
        int _overlayFor = -1;

        public static CockpitDash Create(SolarSystemWorld world, CockpitConfig config)
        {
            var go = new GameObject("CockpitDash");
            go.transform.SetParent(world.transform, false);
            var dash = go.AddComponent<CockpitDash>();
            dash._world = world;
            dash._ship = world.Ship;
            dash._crew = world.Astronaut;
            dash._craft = world.Craft;
            dash.Config = config;
            dash.Model = new DashModel();
            dash.Supplies = new ShipSupplies(config.supplies);
            dash.View = new ScreenView();
            dash.Drawing = new DashDrawing();
            dash.Style = config.style;
            dash.BuildDash();
            dash.BuildCameras();
            if (config.placement == DashPlacement.OnDesk)
                foreach (Transform t in dash._canvas.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = DashLayer;
            return dash;
        }

        static Vector3 V(P3 p) => new Vector3(p.X, p.Y, p.Z);

        void BuildDash()
        {
            RectTransform panel;
            if (Config.placement == DashPlacement.OnDesk)
            {
                var go = new GameObject("DashPanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                go.transform.SetParent(_ship.transform, false);
                _canvas = go.GetComponent<Canvas>();
                _canvas.renderMode = RenderMode.WorldSpace;
                // Letters are drawn a little finer than the panel's own units. Much finer and they sparkle, because
                // they are then shrunk a long way to reach the screen.
                go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 1.5f;
                panel = (RectTransform)go.transform;
                panel.sizeDelta = new Vector2(DashDrawing.Width, DashDrawing.Height);
                panel.localPosition = Config.deskPosition;
                panel.localRotation = Quaternion.Euler(Config.deskLean, 0f, 0f);
                panel.localScale = Vector3.one * (Mathf.Max(0.2f, Config.deskWidth) / DashDrawing.Width);
                go.layer = DashLayer;
                BuildHousing(panel);
            }
            else
            {
                _canvas = UiKit.CreateCanvas("DashStrip", transform, 7);
                _canvas.GetComponent<GraphicRaycaster>().enabled = false;
                panel = UiKit.NewRect("Strip", _canvas.transform);
                panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
                panel.pivot = new Vector2(0.5f, 0f);
                panel.anchoredPosition = new Vector2(0f, 8f);
                panel.sizeDelta = new Vector2(DashDrawing.Width, DashDrawing.Height);
                panel.localScale = Vector3.one * (UiKit.ReferenceResolution.x * Mathf.Clamp(Config.screenShare, 0.3f, 1f) / DashDrawing.Width);
            }

            RectTransform face = UiKit.NewRect("Face", panel);
            UiKit.Stretch(face);
            _graphic = face.gameObject.AddComponent<DashGraphic>();
            _graphic.Drawing = Drawing;
            _graphic.raycastTarget = false;

            RectTransform feed = UiKit.NewRect("CameraFeed", panel);
            feed.anchorMin = feed.anchorMax = feed.pivot = Vector2.zero;
            _feed = feed.gameObject.AddComponent<RawImage>();
            _feed.raycastTarget = false;
            _feedTexture = new RenderTexture(Mathf.Clamp(Config.feedWidth, 64, 1024), Mathf.Clamp(Config.feedHeight, 40, 640), 24) { name = "ShipCameraFeed" };
            _feed.texture = _feedTexture;

            // What the buttons do while a camera fills the view. An ordinary screen overlay.
            _overlay = UiKit.CreateCanvas("ShipCameraWords", transform, 10);
            _overlay.GetComponent<GraphicRaycaster>().enabled = false;
            _overlayText = UiKit.Label(_overlay.transform, string.Empty, 20, UiKit.Cream, TextAnchor.LowerCenter);
            var shadow = _overlayText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            UiKit.Pin((RectTransform)_overlayText.transform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(1200f, 34f));
            _overlay.gameObject.SetActive(false);
        }

        void BuildCameras()
        {
            Cameras = new Camera[CameraRig.Count];
            ModelFile lander = ModelLibrary.Find("Lander");
            for (int i = 0; i < CameraRig.Count; i++)
            {
                CameraSpot spot = CameraRig.Spot(i);
                // The model says where its own lens is, if it has this camera.
                if (lander != null && lander.TrySocket(spot.Socket, out ModelSocket socket)) spot.Lens = new P3(socket.X, socket.Y, socket.Z);

                var go = new GameObject("ShipCamera " + spot.Name);
                go.transform.SetParent(_ship.transform, false);
                go.transform.localPosition = V(spot.Lens);
                go.transform.localRotation = Quaternion.LookRotation(V(spot.LookAt - spot.Lens).normalized, V(spot.Up));
                var cam = go.AddComponent<Camera>();
                cam.fieldOfView = spot.Landing ? Config.landingFieldOfView : Config.poleFieldOfView;
                cam.nearClipPlane = 0.15f;
                // The ship's cameras must not film the dash that shows their own picture.
                cam.cullingMask &= ~(1 << DashLayer);
                cam.farClipPlane = SolarSystem.FarClip;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.depth = 5f;
                cam.enabled = false;
                Cameras[i] = cam;

                // Its hardware, when the model has it. A camera with no hardware still works.
                AddFitting(spot.Piece, false);
                AddFitting(spot.Piece + "Glow", true);
            }
        }

        /// <summary>
        /// The modelled housing round the desk panel, when the model has one. The first choice is the copy with
        /// the cabin's baked light already in its colours (made by tools/bake), drawn as the cabin is drawn; the
        /// plain copy in the Lander model is the fallback. The model's socket says where the panel's middle goes.
        /// </summary>
        void BuildHousing(RectTransform panel)
        {
            // The console the screen sits in, with its real gauges. Without the model the screen stands on the bare desk.
            Console = ConsoleInstruments.Build(_ship.transform, _ship.LightBaked);
            if (Console != null && Console.HasScreenPlace) panel.localPosition = Console.ScreenPlace;
        }

        void AddFitting(string piece, bool lit)
        {
            Mesh mesh = ModelLibrary.PartMesh("Lander", piece);
            if (mesh == null) return;
            var go = new GameObject(piece);
            go.transform.SetParent(_ship.transform, false);
            go.layer = Layers.Default;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = lit ? PlanetMaterials.VertexColour(Color.white, 0f, 2f, false) : ModelLibrary.SharedMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            _fittings.Add(go);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_ship == null || _crew == null || _craft == null || dt <= 0f) return;
            _frame++;

            bool inCabin = _crew.Mode == AstronautMode.Seated || _crew.Mode == AstronautMode.Aboard;
            bool flying = _crew.Mode == AstronautMode.Seated && _ship.Belt.Buckled && !_craft.ControlsLocked && !_crew.Dead;

            // Fuel and air.
            LaunchFx engines = _ship.Launch;
            bool onPad = engines != null && engines.Settled && _world.Base != null && _world.Base.OverPad(_ship.transform.position);
            // The autopilot pushes harder than a pilot's ordinary push, and pays for it.
            // (It flies on, and goes on burning fuel, while the pilot's hands are off to read the map.)
            float push = _craft.PilotOn ? _craft.PilotPushShare : flying ? Mathf.Clamp01(_craft.ThrustInput.magnitude) : 0f;
            // Braking to match speed burns fuel like any other push. Nothing the engines do is free.
            Braking = flying && _craft.MatchingVelocity && _craft.RelativeVelocity().sqrMagnitude > 0.25f;
            // It brakes twice as hard as an ordinary push, and is charged for it.
            if (Braking) push = Mathf.Max(push, _craft.Config.matchVelocityThrust / Mathf.Max(1f, _craft.Config.thrust));
            bool landed = engines != null && engines.Settled;
            int broken = 0;
            ShipModel parts = _ship.Model;
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts.Parts[i].Kind != PartKind.Window) continue;
                PartState state = parts.State(i);
                if (state == PartState.Broken || state == PartState.Lost) broken++;
            }
            Supplies.Tick(dt, push, _craft.Boosting && !Braking, onPad, broken, landed);
            // The console's throttle lever shows what the engines are being asked for, whoever asks: not what a
            // dry tank can give, and not the autopilot's fuel surcharge (that is a bill, not a push).
            float ordinary = Mathf.Max(1f, _craft.Config.thrust);
            AutopilotRules pilot = _craft.PilotRules;
            Gauges.Push(ConsoleGauges.LeverFor(flying ? _craft.ThrustInput.magnitude : 0f, _craft.Boosting, _craft.Config.boostMultiplier,
                Braking, _craft.Config.matchVelocityThrust / ordinary, _craft.PilotOn, _craft.Order.Effort * pilot.push / ordinary, pilot.push / ordinary), dt);
            // The suit fills up from the ship's tank, and is told how much is left in it.
            Supplies.DrawAir(_crew.TakeAirDrawn());
            _crew.ShipAir = Supplies.Oxygen;

            // A hard knock while a camera fills the view puts you straight back behind your own eyes: a crash is
            // lived from the seat, never watched from outside.
            Vector3 velocity = _craft.Body.linearVelocity;
            bool knocked = (velocity - _lastVelocity).magnitude > 6f || _crew.Dead;
            _lastVelocity = velocity;
            _ship.FuelGate = Supplies.PushFactor;

            // Looking down at the camera screen.
            bool toggle = flying && GameInput.CockpitScreen != null && GameInput.CockpitScreen.WasPressedThisFrame();
            bool next = flying && GameInput.NextCamera != null && GameInput.NextCamera.WasPressedThisFrame();
            if (flying && GameInput.NextDash != null && GameInput.NextDash.WasPressedThisFrame())
            {
                Style = (DashStyle)(((int)Style + 1) % DashDesigns.Count);
                _nextDraw = 0f;
                Announce(DashDesigns.Name(Style).ToUpperInvariant() + " DASH");
            }
            View.Tick(dt, toggle, next, flying && !knocked, Config.lookDownSeconds);
            _crew.ScreenGlance = View.Glance * Config.lookDownDegrees;
            ShowCameras(inCabin);

            if (Shown != inCabin)
            {
                Shown = inCabin;
                _canvas.gameObject.SetActive(inCabin);
            }
            if (!inCabin)
            {
                // Seen through the glass from outside, the tube, needle and lamps still follow the tanks.
                if (Console != null)
                {
                    Gauges.Set(Supplies.Fuel, Supplies.Oxygen, Supplies.FuelLow, Supplies.FuelOut, Supplies.OxygenLow, Time.time % 1f < 0.5f, _craft.LockedTarget >= 0, _craft.PilotOn, _craft.MatchingVelocity);
                    Console.Show(Gauges);
                }
                return;
            }

            DashInputs input = Gather(engines);
            Model.Tick(dt, input);
            // The real instruments move every frame (they are cheap); only the screen is redrawn on a timer.
            if (Console != null)
            {
                Gauges.Set(Model.Readings, _craft.LockedTarget >= 0, _craft.PilotOn);
                Console.Show(Gauges);
            }
            _nextDraw -= dt;
            if (_nextDraw > 0f) return;
            _nextDraw = 1f / Mathf.Clamp(Config.redrawsPerSecond, 10, 60);
            // Nobody is looking at the dash while a camera fills the view.
            if (View.Watching) return;
            DashDesigns.Paint(Style, Model.Readings, Drawing);
            int print = Drawing.Fingerprint();
            if (print != _lastPicture || !_drawnOnce)
            {
                _lastPicture = print;
                _drawnOnce = true;
                _graphic.SetVerticesDirty();
            }
            ShowWords();
            bool window = Drawing.CameraWidth > 0f;
            if (_feed.gameObject.activeSelf != window) _feed.gameObject.SetActive(window);
            if (window)
            {
                var rt = (RectTransform)_feed.transform;
                rt.anchoredPosition = new Vector2(Drawing.CameraX, Drawing.CameraY);
                rt.sizeDelta = new Vector2(Drawing.CameraWidth, Drawing.CameraHeight);
            }
        }

        DashInputs Gather(LaunchFx engines)
        {
            var input = new DashInputs();
            Vector3 velocity = _craft.RelativeVelocity();
            Vector3 local = _ship.transform.InverseTransformDirection(velocity);
            input.Speed = velocity.magnitude;
            input.DriftX = local.x; input.DriftY = local.y; input.DriftZ = local.z;
            Vector3 thrust = _craft.ThrustInput;
            // Braking to match speed is a push too: the little ship shows it, pointing against your motion.
            if (Braking && thrust.sqrMagnitude < 0.01f) thrust = -local.normalized;
            input.ThrustX = thrust.x; input.ThrustY = thrust.y; input.ThrustZ = thrust.z;
            input.Boost = _craft.Boosting && thrust.sqrMagnitude > 0.01f;
            input.Matching = _craft.MatchingVelocity;
            input.Fuel = Supplies.Fuel;
            input.Oxygen = Supplies.Oxygen;
            input.FuelLow = Supplies.FuelLow;
            input.FuelOut = Supplies.FuelOut;
            input.OxygenLow = Supplies.OxygenLow;
            input.Filling = Supplies.Filling;
            input.Burn = engines != null ? engines.Model.Burn : 0f;
            input.Igniting = engines != null && engines.Model.State == EngineState.Igniting;
            input.CameraName = CameraRig.Spot(View.Camera).Name;
            if (Time.unscaledTime >= _hintUntil)
            {
                // The key can be rebound at any time, so the words are remade now and then, not every frame.
                _hintUntil = Time.unscaledTime + 1f;
                string key = GameInput.Describe(GameInput.CockpitScreen);
                if (key != _hintKey) { _hintKey = key; _hint = key.ToUpperInvariant() + ": LOOK DOWN"; }
            }
            input.ScreenHint = _hint;
            input.Loose = !_ship.Belt.Buckled;
            if (_craft.PilotOn)
            {
                int key = (int)_craft.Order.Phase * 10000 + (int)Mathf.Clamp(_craft.Order.SecondsLeft, 0f, 5999f);
                if (key != _pilotKey) { _pilotKey = key; _pilotWords = OrbitalAnte.Navigation.Autopilot.Words(_craft.Order.Phase, _craft.Order.SecondsLeft); }
                input.Note = _pilotWords;
            }
            else if (Time.unscaledTime - _craft.LastStopTime < 4f) input.Note = _craft.LastStopWords;

            input.Altitude = -1f;
            SolarSystem system = _craft.System;
            if (system != null && system.Model != null)
            {
                int target = _craft.Target;
                input.Against = _craft.AgainstGround ? GroundName(_craft.MatchBody) : UpperName(_craft.MatchBody);
                int anchor = system.Frame.Anchor;
                PlanetWorld planets = PlanetWorld.Instance;
                if (anchor > 0 && planets != null)
                {
                    GroundReading ground = planets.Read(anchor, _ship.transform.position);
                    if (ground.Known && !ground.InCave)
                    {
                        // Height above what you would land on: the ground, or the sea where there is one.
                        double height = ground.OverSea && !ground.Underwater ? ground.AboveSea : ground.AboveGround;
                        // Measured to the feet, not the cabin floor.
                        input.Altitude = Mathf.Max(0f, (float)height + ShipLayout.GroundY);
                        Vector3 up = (_ship.transform.position - system.Bodies[anchor].transform.position).normalized;
                        input.Vertical = Vector3.Dot(_craft.Body.linearVelocity, up);
                        input.SafeSink = _ship.Model.Rules.safeImpactSpeed;
                    }
                }
            }
            return input;
        }

        float _hintUntil;
        string _hintKey, _hint = string.Empty;
        Vector3 _lastVelocity;
        /// <summary>True while the match-velocity brake is really pushing.</summary>
        public bool Braking { get; private set; }

        // Names in capitals, made once each.
        string[] _upperNames;

        string[] _groundNames;

        string GroundName(int body)
        {
            SolarSystemModel model = _craft.System.Model;
            if (_groundNames == null || _groundNames.Length != model.Count) _groundNames = new string[model.Count];
            if (body < 0 || body >= _groundNames.Length) return string.Empty;
            return _groundNames[body] ?? (_groundNames[body] = UpperName(body) + " GROUND");
        }

        string UpperName(int body)
        {
            SolarSystemModel model = _craft.System.Model;
            if (_upperNames == null || _upperNames.Length != model.Count) _upperNames = new string[model.Count];
            if (body < 0 || body >= _upperNames.Length) return string.Empty;
            return _upperNames[body] ?? (_upperNames[body] = model.Spec(body).displayName.ToUpperInvariant());
        }

        void ShowWords()
        {
            List<DashText> texts = Drawing.Texts;
            Transform parent = _graphic.transform;
            while (_words.Count < texts.Count)
            {
                Text made = UiKit.Label(parent, string.Empty, 16, Color.white);
                made.raycastTarget = false;
                made.horizontalOverflow = HorizontalWrapMode.Overflow;
                made.verticalOverflow = VerticalWrapMode.Overflow;
                var rect = (RectTransform)made.transform;
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.sizeDelta = new Vector2(4f, 4f);
                made.gameObject.layer = _canvas.gameObject.layer;
                _words.Add(made);
            }
            for (int i = 0; i < _words.Count; i++)
            {
                Text word = _words[i];
                bool used = i < texts.Count;
                if (word.gameObject.activeSelf != used) word.gameObject.SetActive(used);
                if (!used) continue;
                DashText t = texts[i];
                // Only touch what changed: a Text rebuilds itself whenever anything is set.
                if (!ReferenceEquals(word.text, t.Text) && word.text != t.Text) word.text = t.Text;
                int size = Mathf.Max(6, Mathf.RoundToInt(t.Size));
                if (word.fontSize != size) word.fontSize = size;
                FontStyle style = t.Bold ? FontStyle.Bold : FontStyle.Normal;
                if (word.fontStyle != style) word.fontStyle = style;
                TextAnchor anchor = t.Align == DashAlign.Left ? TextAnchor.MiddleLeft : t.Align == DashAlign.Right ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
                if (word.alignment != anchor) word.alignment = anchor;
                var colour = new Color(t.Color.R, t.Color.G, t.Color.B, t.Color.A);
                if (word.color != colour) word.color = colour;
                var rect = (RectTransform)word.transform;
                var at = new Vector2(t.X, t.Y);
                if (rect.anchoredPosition != at) rect.anchoredPosition = at;
            }
            WordsShown = texts.Count;
        }

        void Announce(string words)
        {
            _overlayWords = words;
            _overlayText.text = words;
            _overlayUntil = Time.unscaledTime + 1.6f;
            _overlayFor = -1;
        }

        void ShowCameras(bool inCabin)
        {
            // The little screen is only kept live for someone sitting at it.
            bool seated = inCabin && _crew.Mode == AstronautMode.Seated;
            bool watching = View.Watching;
            WatchingScreen = watching;
            Camera eye = _crew.Eye;
            if (eye != null && eye.enabled == watching) eye.enabled = !watching;

            for (int i = 0; i < Cameras.Length; i++)
            {
                Camera cam = Cameras[i];
                bool chosen = i == View.Camera;
                if (watching && chosen)
                {
                    if (cam.targetTexture != null) cam.targetTexture = null;
                    cam.farClipPlane = SolarSystem.FarClip;
                    Finish(i, true);
                    if (!cam.enabled) cam.enabled = true;
                }
                else if (!watching && chosen && seated)
                {
                    // The little screen on the dash: a small picture, refreshed a few times a second.
                    if (cam.targetTexture != _feedTexture) cam.targetTexture = _feedTexture;
                    cam.farClipPlane = Mathf.Clamp(Config.feedFarClip, 500f, SolarSystem.FarClip);
                    Finish(i, false);
                    bool now = _frame % Mathf.Max(1, Config.feedEveryFrames) == 0 || _feedCamera != i;
                    if (cam.enabled != now) cam.enabled = now;
                    _feedCamera = i;
                }
                else if (cam.enabled) cam.enabled = false;
            }

            bool words = watching || Time.unscaledTime < _overlayUntil;
            if (watching && _overlayFor != View.Camera)
            {
                _overlayFor = View.Camera;
                _overlayUntil = 0f;
                _overlayText.text = CameraRig.Spot(View.Camera).Name + " CAMERA      " + GameInput.Describe(GameInput.NextCamera) + ": next camera      "
                    + GameInput.Describe(GameInput.CockpitScreen) + ": back to the windows";
            }
            else if (!watching && _overlayFor >= 0)
            {
                _overlayFor = -1;
                _overlayText.text = _overlayWords;
            }
            if (_overlay.gameObject.activeSelf != words) _overlay.gameObject.SetActive(words);
        }

        void Finish(int i, bool full)
        {
            int want = full ? 2 : 1;
            if (_finish[i] == want || OrbitalAnte.Look.PostFx.Instance == null) return;
            _finish[i] = want;
            OrbitalAnte.Look.PostFx.Instance.SetFinish(Cameras[i], full);
        }

        void OnDestroy()
        {
            WatchingScreen = false;
            if (_crew != null && _crew.Eye != null) _crew.Eye.enabled = true;
            if (Cameras != null)
            {
                for (int i = 0; i < Cameras.Length; i++)
                {
                    if (Cameras[i] == null) continue;
                    Cameras[i].targetTexture = null;
                    Destroy(Cameras[i].gameObject);
                }
            }
            if (_feedTexture != null)
            {
                _feedTexture.Release();
                Destroy(_feedTexture);
            }
            if (_canvas != null) Destroy(_canvas.gameObject);
            for (int i = 0; i < _fittings.Count; i++) if (_fittings[i] != null) Destroy(_fittings[i]);
            if (Console != null) Console.Destroy();
        }
    }
}
