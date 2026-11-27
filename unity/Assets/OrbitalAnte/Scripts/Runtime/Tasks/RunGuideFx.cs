using System.Collections.Generic;
using OrbitalAnte.Core;
using OrbitalAnte.UI;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.Tasks
{
    /// <summary>
    /// Makes the run's goal clear (Matthew, 21 Nov 2026: "a fully functioning game with clear goals"):
    /// the goal card on a player's first run, ante 1's clock waiting until liftoff (<see cref="RunStartHold"/>),
    /// one-time tips at the moment they are needed (<see cref="RunGuide"/>), and the line under the ante readout
    /// saying what is left to score on the world you are at (<see cref="WorldTaskLine"/>).
    /// Lives on the game root next to <see cref="AnteLoop"/>. Tips are remembered per profile in PlayerPrefs and
    /// can be switched off on the pause menu's Feel page (<see cref="FeelSettings.GuideTips"/>).
    /// </summary>
    // After LaunchFx (100), so the ship's height under it is this frame's reading.
    [DefaultExecutionOrder(150)]
    public sealed class RunGuideFx : MonoBehaviour
    {
        const string SeenKey = "oa.guide.seen";
        static RunGuideFx _live;

        [System.NonSerialized] public AnteLoop Loop;
        [System.NonSerialized] public PauseMenu Menu;
        [System.NonSerialized] public Items.EnvelopeDirector Envelopes;

        Space.SolarSystemWorld _world;
        TaskDirector _tasks;
        readonly RunStartHold _hold = new RunStartHold();
        HashSet<GuideTip> _seen;
        bool _active, _cardDue, _mapOpened, _sawGround, _unsaved;
        int _lineBody = -1, _lineMasts = -1, _lineCrates = -1;
        float _tipLeft, _gapLeft, _lineEvery;

        Canvas _canvas;
        Text _tip, _line;
        RectTransform _plate;
        string _lineText = string.Empty;

        /// <summary>Forgets which tips have been seen (title screen > Extras), so they and the goal card come back.</summary>
        public static void ForgetTips()
        {
            PlayerPrefs.DeleteKey(SeenKey);
            PlayerPrefs.Save();
            if (_live != null)
            {
                _live._seen.Clear();
                _live._unsaved = false;
            }
        }

        void Awake()
        {
            _live = this;
            _seen = RunGuide.ParseSeen(PlayerPrefs.GetString(SeenKey, string.Empty));
            AnteLoop.HoldClock = () => _active && _hold.Holding;
            Build();
        }

        void OnDestroy()
        {
            if (AnteLoop.HoldClock != null) AnteLoop.HoldClock = null;
            if (_live == this) _live = null;
            SaveSeen();
        }

        /// <summary>A solar-system run has started (after the ante loop has begun its run).</summary>
        public void BeginRun(Space.SolarSystemWorld world, TaskDirector tasks)
        {
            _world = world;
            _tasks = tasks;
            _active = Loop != null && Loop.Run != null;
            _hold.Reset();
            _mapOpened = false;
            _sawGround = false;
            _lineBody = _lineMasts = _lineCrates = -1;
            _tipLeft = 0f;
            if (_tip != null) _tip.gameObject.SetActive(false);
            _gapLeft = 2f;
            _lineText = string.Empty;
            _cardDue = _active && FeelSettings.GuideTips && !_seen.Contains(GuideTip.Goal);
            // Only a run that starts on the launch pad has a liftoff to wait for (tests start in orbit).
            if (world == null || world.Ship == null || !world.StartedOnPad) _hold.ReleaseNow("UNDER WAY");
        }

        public void EndRun()
        {
            _active = false;
            _world = null;
            _tasks = null;
            _cardDue = false;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            SaveSeen();
        }

        void SaveSeen()
        {
            if (!_unsaved) return;
            _unsaved = false;
            PlayerPrefs.SetString(SeenKey, RunGuide.StoreSeen(_seen));
            PlayerPrefs.Save();
        }

        void Update()
        {
            if (!_active || Loop == null || Loop.Run == null)
            {
                if (_canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(false);
                return;
            }

            bool paused = GameSession.Paused || TimeFlow.Held;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            Ante.AnteDirector d = Loop.Run.Director;
            var astronaut = _world != null ? _world.Astronaut : null;
            var ship = _world != null ? _world.Ship : null;

            bool mapOpen = _world != null && _world.Map != null && _world.Map.IsOpen;
            bool show = GameSession.Mode == GameMode.Playing && !paused && !Cockpit.CockpitDash.WatchingScreen && !mapOpen;

            // The first run's goal card, once the run-start envelope has gone and nothing else is on screen.
            if (_cardDue && show && (Menu == null || !Menu.IsOpen) && (Envelopes == null || !Envelopes.Showing))
            {
                _cardDue = false;
                ShowGoalCard();
                return;
            }

            if (!paused)
            {
                // The launch effects only know the height once they have looked down (it reads "very high" before
                // that), so liftoff counts only after the ship has been seen standing on the ground.
                var launch = ship != null ? ship.Launch : null;
                if (launch != null && launch.HeightAboveGround < RunStartHold.LiftedAt) _sawGround = true;
                float height = _sawGround && launch != null ? launch.HeightAboveGround : 0f;
                bool outside = astronaut != null && astronaut.InTheOpen;
                bool wasHolding = _hold.Holding;
                _hold.Step(dt, height, outside, Loop.Run.Ledger.AnteEarned > 0);
                if (wasHolding && !_hold.Holding) AnteLoop.Note("CLOCK STARTED: " + _hold.Released);
                if (mapOpen) _mapOpened = true;
            }

            // The world line, refreshed twice a second (counting a world's tasks walks short lists).
            _lineEvery -= dt;
            if (_lineEvery <= 0f)
            {
                _lineEvery = 0.5f;
                // Words are made only when the world or a count changes.
                int body = -1, m = 0, c = 0;
                string name = null;
                if (_tasks != null && _tasks.TryCountsHere(out name, out m, out c) && _world != null && _world.System.Focus != null)
                    body = _world.System.Focus.Neighbourhood;
                if (body != _lineBody || m != _lineMasts || c != _lineCrates)
                {
                    _lineBody = body;
                    _lineMasts = m;
                    _lineCrates = c;
                    _lineText = body >= 0 ? WorldTaskLine.Compose(name, m, c) : string.Empty;
                }
            }

            // One-time tips are remembered on display. Keep their reading time while the map or camera covers the HUD.
            if (show) UpdateTips(dt, d);
            if (_canvas.gameObject.activeSelf != show) _canvas.gameObject.SetActive(show);
            // Aboard, the ship's damage list has this corner (as the "+points" lines do); the map shows tasks per world.
            string objective = WorldTaskLine.Objective(_lineText, _tasks != null && _tasks.CarryingSalvage);
            if (_line.text != objective) _line.text = objective;
            bool lineOn = objective.Length > 0 && d.IsRunning && !Ship.Astronaut.PlayerInShip
                          && ReadoutFocus.ShowsTaskLine(AnteLoop.InFight);
            if (_plate.gameObject.activeSelf != lineOn) _plate.gameObject.SetActive(lineOn);
        }

        void UpdateTips(float dt, Ante.AnteDirector d)
        {
            if (_tipLeft > 0f)
            {
                _tipLeft -= dt;
                Color c = _tip.color;
                c.a = Mathf.Clamp01(_tipLeft / 0.6f);
                _tip.color = c;
                if (_tipLeft <= 0f)
                {
                    _tip.gameObject.SetActive(false);
                    _gapLeft = RunGuide.GapSeconds;
                }
                return;
            }
            if (_gapLeft > 0f) { _gapLeft -= dt; return; }
            if (!FeelSettings.GuideTips || _seen.Count >= RunGuide.All.Length) return;

            // The world line's counts, at most half a second old, are enough here.
            bool near = _lineBody >= 0;
            bool hasTasks = near && _lineMasts + _lineCrates > 0;
            var astronaut = _world != null ? _world.Astronaut : null;
            var ship = _world != null ? _world.Ship : null;
            bool atControls = astronaut != null && ship != null && astronaut.Mode == Ship.AstronautMode.Seated
                              && ship.Belt != null && ship.Belt.Buckled;
            var state = new GuideState
            {
                Running = d.IsRunning,
                Lifted = !_hold.Holding,
                AtTheControls = atControls,
                MapOpenedThisRun = _mapOpened,
                NearAWorld = near,
                WorldHasTasks = hasTasks,
                Scored = Loop.Run.Ledger.AnteEarned > 0,
                TargetMet = d.TargetMet,
                SecondsLeft = d.SecondsLeft,
            };
            GuideTip tip = RunGuide.Next(state, _seen);
            if (tip == GuideTip.None) return;
            // Mid-fight only the tips about the clock cut in; the rest wait, unseen, for a quiet moment.
            if (!ReadoutFocus.TipMayStart(tip, AnteLoop.InFight)) return;
            Remember(tip);
            _tip.text = RunGuide.Text(tip, Keys(), d.Target);
            Color col = _tip.color;
            col.a = 1f;
            _tip.color = col;
            _tip.gameObject.SetActive(true);
            _tipLeft = RunGuide.TipSeconds;
            Audio.AudioDirector.PlayLocal("ui.select", 0.5f);
        }

        void ShowGoalCard()
        {
            if (Menu == null) return;
            Remember(GuideTip.Goal);
            Ante.AnteBalance balance = Loop.Run.Balance;
            string body = RunGuide.GoalCard(balance.TargetFor(1), Mathf.RoundToInt(balance.anteSeconds), balance.AnteCount, Keys());
            Menu.ShowChoices("YOUR GOAL", body, new List<PauseMenu.MenuChoice>
            {
                new PauseMenu.MenuChoice("Let's go", Menu.Close),
                new PauseMenu.MenuChoice("Let's go, and no more tips", () =>
                {
                    FeelSettings.GuideTips = false;
                    Menu.Close();
                }),
            }, Menu.Close, 260f);
        }

        void Remember(GuideTip tip)
        {
            // Written at the end of the run, not mid-flight (a disk write can hitch on the Deck).
            if (_seen.Add(tip)) _unsaved = true;
        }

        static RunGuide.Keys Keys()
        {
            return new RunGuide.Keys
            {
                Map = GameInput.Describe(GameInput.ToggleMap),
                Lock = GameInput.Describe(GameInput.LockTarget),
                Autopilot = GameInput.Describe(GameInput.Autopilot),
                Pause = GameInput.Describe(GameInput.Pause),
                LiftOff = GameInput.Describe(GameInput.FlyUp),
                Interact = GameInput.Describe(GameInput.Interact),
                Upgrades = GameInput.Describe(GameInput.Upgrades),
            };
        }

        void Build()
        {
            _canvas = UiKit.CreateCanvas("RunGuide", transform, 11);
            _canvas.GetComponent<GraphicRaycaster>().enabled = false;

            // Top right: what is left to do on the world you are at, on its own plate (the ante plate is top left).
            _plate = HullKit.Plate(_canvas.transform, "TaskPlate", new Vector2(1f, 1f), new Vector2(-HullKit.Margin, -20f), new Vector2(380f, 66f));
            HullKit.At(HullKit.Mono(_plate, "TASK", 14, HullKit.Dim), 18f, 22f, 80f, 18f);
            _line = HullKit.At(HullKit.Mono(_plate, string.Empty, 17, HullKit.Paint), 18f, 45f, 344f, 24f);
            // A long line of counts is shrunk to fit the plate, never cut off.
            _line.horizontalOverflow = HorizontalWrapMode.Wrap;
            _line.resizeTextForBestFit = true;
            _line.resizeTextMinSize = 11;
            _line.resizeTextMaxSize = 17;
            _plate.gameObject.SetActive(false);

            // Low in the middle, between the two visor gauges and clear of the crosshair.
            _tip = HullKit.AsMono(UiKit.Label(_canvas.transform, string.Empty, 18, HullKit.Paint, TextAnchor.MiddleCenter));
            UiKit.Pin((RectTransform)_tip.transform, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(760f, 56f));
            _tip.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.Halo(_tip, true);
            _tip.gameObject.SetActive(false);

            _canvas.gameObject.SetActive(false);
        }
    }
}
