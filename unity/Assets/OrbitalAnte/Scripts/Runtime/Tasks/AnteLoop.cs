using System;
using System.Collections.Generic;
using OrbitalAnte.Ante;
using OrbitalAnte.Combat;
using OrbitalAnte.Core;
using OrbitalAnte.UI;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.Tasks
{
    /// <summary>
    /// The ante loop switched on (20 Nov 2026): one <see cref="AnteRun"/> per solar-system run, its timer ticked
    /// once a frame, everything that scores handed to it through <see cref="Report"/>, and the readout in the top
    /// right of the visor: ante, time left, points banked against the target (counted up like a slot machine), the
    /// streak and the last few "+points" lines.
    ///
    /// Lives on the game's root object next to Focus and the gun ladder. Between antes the pause menu shows the
    /// ante break (perk draft, shop, scratch-off, raise the stakes: AnteLoop.Break.cs, 20 Nov 2026). The arena
    /// has no ante loop.
    /// </summary>
    public sealed partial class AnteLoop : MonoBehaviour
    {
        /// <summary>The live loop while a solar-system run is on, otherwise null.</summary>
        public static AnteLoop Current { get; private set; }

        public AnteRun Run { get; private set; }
        public TaskConfig Config { get; private set; }
        /// <summary>The streak of quick kills (shown on the readout; the task director feeds it).</summary>
        public Streak Streak { get; private set; }

        /// <summary>Set by the game root.</summary>
        [NonSerialized] public PauseMenu Menu;
        [NonSerialized] public Items.EnvelopeDirector Envelopes;

        /// <summary>A new ante has started (1 for the first). The task director restarts its supply-drop clock.</summary>
        public event Action<int> AnteBegan;

        /// <summary>The time the loop counts in: real seconds of play, not slowed by Focus or hitstop, stopped by pause.</summary>
        public float PlayTime { get; private set; }

        AntePhase _seenPhase;
        bool _diedFirst;
        bool _toldTargetMet;

        // ------------------------------------------------------------------ what you see
        Canvas _canvas;
        Text _anteLine, _scoreLine, _streakLine, _noteLine;
        Image _barFill;
        readonly Text[] _pops = new Text[4];
        readonly float[] _popLeft = new float[4];
        float _shown;
        float _tickAt;
        int _shownSeconds = -1, _shownAnte = -1, _shownTarget = -1, _shownStreak = -1, _shownPoints = -1;
        float _noteLeft;
        bool _counting;

        static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

        public static void ResetStatics()
        {
            Current = null;
            HoldClock = null;
        }

        /// <summary>
        /// True while the ante clock should wait though the game is not paused: ante 1 waits on the pad until
        /// liftoff (<see cref="RunStartHold"/>, set by <c>RunGuideFx</c>). Points still count while it waits.
        /// </summary>
        public static Func<bool> HoldClock;
        bool _shownHeld, _wasOvertime, _fromVictory;
        float _huntIn;

        void Awake()
        {
            Config = TaskConfig.Load();
            Streak = new Streak(Config.rules.streakWindow);
            BuildHud();
            RunPerksFx.Note += OnPerkNote;
        }

        void OnDestroy()
        {
            RunPerksFx.Note -= OnPerkNote;
            if (Current == this) Current = null;
        }

        void OnPerkNote(string text)
        {
            if (Run != null) Pop(text);
        }

        /// <summary>A solar-system run has begun: ante 1, a full timer, nothing banked.</summary>
        public void BeginRun(int seed)
        {
            EndRun();
            if (!Config.enabled) return;
            AnteBalance balance = AnteBalanceData.Load().balance;
            // The full perk list (20 Nov 2026): every perk in it is wired into the game.
            Run = new AnteRun(balance, seed, RoguelitePerks.Create());
            Current = this;
            ResetBreak();
            _skippedPerk = false;
            PublishBuild();
            PlayTime = 0f;
            _seenPhase = Run.Director.Phase;
            _diedFirst = false;
            _wasOvertime = false;
            _toldTargetMet = false;
            Streak.Clear();
            _shown = 0f;
            _shownSeconds = _shownAnte = _shownTarget = _shownStreak = _shownPoints = -1;
            for (int i = 0; i < _pops.Length; i++) { _popLeft[i] = 0f; _pops[i].gameObject.SetActive(false); }
            _noteLeft = 0f;
            AnteBegan?.Invoke(1);
        }

        /// <summary>The run's world is gone. Nothing scores until the next run.</summary>
        public void EndRun()
        {
            // A run left mid-ante (restart or quit) still counts toward the records.
            if (Run != null && !_recorded) RecapAndRecord();
            Run = null;
            Enemies.EnemySpawner.Pressure = new EnemyPressure { Sharpness = 1f };
            Enemies.EnemySpawner.PressureAnte = 1;
            if (Current == this) Current = null;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
        }

        /// <summary>The player died: the run is over (the death page is shown by the astronaut, not here).</summary>
        public void PlayerDied()
        {
            if (Run == null) return;
            _diedFirst = true;
            Run.Director.PlayerDied();
            _seenPhase = Run.Director.Phase;
        }

        // ------------------------------------------------------------------ scoring

        /// <summary>
        /// Hands a scoring event to the live run. <paramref name="label"/> is the short line shown with the points
        /// ("HEADSHOT", "BASE CLEARED"). Returns what it was worth (nothing outside a running ante).
        /// </summary>
        public static ScoreResult Report(ScoreEvent scoreEvent, string label)
        {
            AnteLoop loop = Current;
            if (loop == null || loop.Run == null) return default;
            loop.BeforeReport(ref scoreEvent);
            ScoreResult result = loop.Run.Report(scoreEvent);
            if (result.Awarded && result.points > 0) loop.Pop("+" + result.points.ToString("N0", Inv) + "  " + label);
            loop.AfterReport(scoreEvent, result, label);
            return result;
        }

        /// <summary>A line on the readout with no points (a task found, a supply drop coming).</summary>
        public static void Note(string text)
        {
            AnteLoop loop = Current;
            if (loop == null || loop._noteLine == null) return;
            loop._noteLine.text = text;
            loop._noteLeft = 3.5f;
        }

        public bool Running => Run != null && Run.Director.IsRunning;

        // ------------------------------------------------------------------ the loop

        void Update()
        {
            if (Run == null) return;
            // The run-start envelope holds the world without pausing it: the clock waits too.
            bool paused = GameSession.Paused || TimeFlow.Held;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (!paused && Run.Director.IsRunning) PlayTime += dt;
            bool held = HoldClock != null && HoldClock();
            Run.Tick(dt, paused || held);
            UpdateOvertime(dt, paused);

            AntePhase phase = Run.Director.Phase;
            if (phase != _seenPhase)
            {
                _seenPhase = phase;
                OnPhase(phase);
            }
            // Restart, then Back out of its confirm page, closes the menu: the card between antes comes back.
            if (phase == AntePhase.Intermission && !paused && Menu != null && !Menu.IsOpen) ShowCard();
            // The same for the victory page (Keep going or End the run), once the envelope's reveal is done.
            if (phase == AntePhase.Victory && !_recorded && !paused && Menu != null && !Menu.IsOpen
                && (Envelopes == null || !Envelopes.Showing)) ShowVictory();

            if (Run.Director.IsRunning && Run.Director.TargetMet && !_toldTargetMet)
            {
                _toldTargetMet = true;
                Audio.AudioDirector.PlayLocal("reward.tier2");
                Note("TARGET MET: bank it from the pause menu, or keep scoring");
            }
            UpdateHud(dt, paused);
        }

        /// <summary>
        /// Overtime (Matthew, 21 Nov 2026): the clock ran out short of the target, the ante goes on, and the enemies
        /// hunt. They get Hunted's sharpness and heavies, and every <see cref="AnteBalance.overtimeHuntEvery"/> seconds
        /// those near are told where the player is (a place to go and look, as a flare gives, never a live track).
        /// </summary>
        void UpdateOvertime(float dt, bool paused)
        {
            bool ot = Run != null && Run.Director.IsRunning && Run.Director.InOvertime;
            if (ot != _wasOvertime)
            {
                _wasOvertime = ot;
                PublishBuild();
                if (ot)
                {
                    _huntIn = 1f;
                    Audio.AudioDirector.PlayLocal("player.suit.alarm", 0.8f);
                    Note("OVERTIME: REACH " + Run.Director.Target.ToString("N0", Inv) + ". THEY'RE HUNTING YOU");
                }
            }
            if (!ot || paused) return;
            _huntIn -= dt;
            if (_huntIn > 0f) return;
            Transform eye = AI.CombatWorld.PlayerEye;
            if (eye == null) return;
            _huntIn = Mathf.Max(3f, Run.Balance.overtimeHuntEvery);
            float reach = Mathf.Max(20f, Run.Balance.overtimeHuntRadius);
            AI.CombatWorld.RaiseAlarm(eye.position, reach, eye.position, Enemies.Factions.None);
            AI.CombatWorld.RaiseAlarm(eye.position, reach, eye.position, Enemies.Factions.A);
        }

        void OnPhase(AntePhase phase)
        {
            int ante = Run.Director.Ante;
            switch (phase)
            {
                case AntePhase.Intermission:
                    // Keep going after the last ante comes here too: its sounds were played on the victory page.
                    if (_fromVictory) { _fromVictory = false; ShowCard(); break; }
                    Audio.AudioDirector.PlayLocal("reward.tier3");
                    // Ante 9 is the envelope's; clearing earlier antes changes nothing there yet, but it is told.
                    if (Envelopes != null) Envelopes.AnteCleared(ante);
                    ShowCard();
                    break;
                case AntePhase.Victory:
                    Audio.AudioDirector.PlayLocal("reward.tier4");
                    if (Envelopes != null) Envelopes.AnteCleared(ante, ShowVictory);
                    else ShowVictory();
                    break;
                case AntePhase.Failed:
                    // A death has its own page with its own cause; only the timer needs one here.
                    if (!_diedFirst && Menu != null)
                        Menu.ShowRunOver("TIME'S UP", "The clock ran out on ante " + ante + ".", RecapAndRecord());
                    break;
            }
        }

        void ShowCard()
        {
            // The ante break (20 Nov 2026) replaces the simple card.
            ShowBreak();
        }

        void ShowVictory()
        {
            if (Menu == null || Run == null) return;
            // Nothing is recorded yet: a run that keeps going is recorded when it really ends.
            int score = Run.Ledger.LifetimeRunScore;
            Menu.ShowChoices("ALL ANTES CLEARED",
                "You cleared all " + Run.Director.Ante + " antes. Run score " + score.ToString("N0", Inv) + ".\n"
                + "Keep going for endless antes, each one asking more, until you die. Your best ante counts on the records.",
                new List<PauseMenu.MenuChoice>
                {
                    new PauseMenu.MenuChoice("Keep going", () =>
                    {
                        if (Run == null) return;
                        _fromVictory = true;
                        if (Run.Director.ContinueEndless()) Audio.AudioDirector.PlayLocal("reward.multiplier");
                        else _fromVictory = false;
                    }),
                    new PauseMenu.MenuChoice("End the run here", () =>
                    {
                        if (Run == null) return;
                        Menu.ShowRunOver("ALL ANTES CLEARED", "That is the whole run.", RecapAndRecord());
                    }),
                }, null, 110f, -1, true);
        }

        /// <summary>For the pause menu: may the player end the ante now (target met, Hybrid)?</summary>
        public bool CanEndAnte() => Run != null && Run.Director.CanAdvance;

        /// <summary>For the pause menu: the player confirmed ending the ante now.</summary>
        public void EndAnteNow()
        {
            if (Run == null || !Run.Director.ConfirmAdvance()) return;
            // The phase change is picked up next frame; the menu's own page closes first.
        }

        /// <summary>For the card between antes: on to the next one.</summary>
        public void StartNextAnte()
        {
            if (Run == null || !Run.BeginNextAnte()) return;
            _seenPhase = Run.Director.Phase;
            _toldTargetMet = false;
            Streak.Clear();
            _shown = 0f;
            _wasOvertime = false;
            Audio.AudioDirector.PlayLocal("reward.multiplier");
            Note("ANTE " + Run.Director.Ante + ": " + Run.Director.Target.ToString("N0", Inv) + " TO BANK");
            _skippedPerk = false;
            BeginAnteEffects();
            AnteBegan?.Invoke(Run.Director.Ante);
        }

        static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        // ------------------------------------------------------------------ readout

        void BuildHud()
        {
            _canvas = UiKit.CreateCanvas("AnteHUD", transform, 11);
            _canvas.GetComponent<GraphicRaycaster>().enabled = false;
            Color gold = Config.scoreColour;

            // Top right, the corner Part B section 13 keeps for notices. The ship's damage list starts at -110.
            _anteLine = UiKit.Label(_canvas.transform, string.Empty, 16, UiKit.Cream, TextAnchor.MiddleRight);
            UiKit.Pin((RectTransform)_anteLine.transform, new Vector2(1f, 1f), new Vector2(-22f, -16f), new Vector2(460f, 22f));
            UiKit.Halo(_anteLine);

            _scoreLine = UiKit.Label(_canvas.transform, string.Empty, 26, gold, TextAnchor.MiddleRight);
            UiKit.SetStyle(_scoreLine, FontStyle.Bold);
            UiKit.Pin((RectTransform)_scoreLine.transform, new Vector2(1f, 1f), new Vector2(-22f, -42f), new Vector2(320f, 32f));
            UiKit.Halo(_scoreLine, true);

            var back = UiKit.NewRect("Bar", _canvas.transform);
            UiKit.Pin(back, new Vector2(1f, 1f), new Vector2(-22f, -62f), new Vector2(200f, 5f));
            back.pivot = new Vector2(1f, 0.5f);
            UiKit.AddImage(back, new Color(0f, 0f, 0f, 0.45f));
            var fill = UiKit.NewRect("Fill", back);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = Vector2.zero;
            fill.sizeDelta = new Vector2(0f, 0f);
            _barFill = UiKit.AddImage(fill, gold);

            _streakLine = UiKit.Label(_canvas.transform, string.Empty, 15, Config.styleColour, TextAnchor.MiddleRight);
            UiKit.Pin((RectTransform)_streakLine.transform, new Vector2(1f, 1f), new Vector2(-22f, -78f), new Vector2(320f, 20f));
            UiKit.Halo(_streakLine);

            for (int i = 0; i < _pops.Length; i++)
            {
                _pops[i] = UiKit.Label(_canvas.transform, string.Empty, 17, gold, TextAnchor.MiddleRight);
                UiKit.Pin((RectTransform)_pops[i].transform, new Vector2(1f, 1f), new Vector2(-22f, -122f - i * 21f), new Vector2(420f, 21f));
                UiKit.Halo(_pops[i]);
                _pops[i].gameObject.SetActive(false);
            }

            // A note in the middle, under the flight readout: target met, a drop coming, a task found.
            _noteLine = UiKit.Label(_canvas.transform, string.Empty, 18, gold, TextAnchor.MiddleCenter);
            UiKit.Pin((RectTransform)_noteLine.transform, new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(900f, 26f));
            UiKit.Halo(_noteLine, true);
            _noteLine.gameObject.SetActive(false);

            _canvas.gameObject.SetActive(false);
        }

        void Pop(string text)
        {
            // Newest at the top: the others move down one line and the oldest falls off.
            ShiftPops();
            _pops[0].text = text;
            _popLeft[0] = Mathf.Max(0.5f, Config.popSeconds);
            _pops[0].gameObject.SetActive(true);
        }

        void ShiftPops()
        {
            for (int k = _pops.Length - 1; k > 0; k--)
            {
                _pops[k].text = _pops[k - 1].text;
                _popLeft[k] = _popLeft[k - 1];
                _pops[k].gameObject.SetActive(_popLeft[k] > 0f);
            }
        }

        void UpdateHud(float dt, bool paused)
        {
            bool show = Run != null && GameSession.Mode == GameMode.Playing && !paused
                        && !OrbitalAnte.Cockpit.CockpitDash.WatchingScreen;
            if (_canvas.gameObject.activeSelf != show) _canvas.gameObject.SetActive(show);
            if (!show) return;

            AnteDirector d = Run.Director;
            int seconds = Mathf.CeilToInt(d.SecondsLeft);
            bool held = HoldClock != null && HoldClock();
            // In overtime the line counts up how long you have been over instead.
            bool over = d.IsRunning && d.InOvertime;
            if (over) seconds = -1 - Mathf.FloorToInt(d.OvertimeSeconds);
            if (seconds != _shownSeconds || d.Ante != _shownAnte || held != _shownHeld)
            {
                _shownSeconds = seconds;
                _shownAnte = d.Ante;
                _shownHeld = held;
                _anteLine.text = over
                    ? "ANTE " + d.Ante + "   OVERTIME +" + Clock(Mathf.Floor(d.OvertimeSeconds))
                    : "ANTE " + d.Ante + "   " + Clock(d.SecondsLeft) + (held ? "  STARTS AT LIFTOFF" : string.Empty);
                // The last half minute, and overtime, are in the warning colour.
                _anteLine.color = d.IsRunning && (over || d.SecondsLeft <= 30f) && !d.TargetMet ? UiKit.Danger : UiKit.Cream;
            }

            // The slot-machine count-up: the shown number runs to the real one, never taking more than 1.2 s.
            int earned = Run.Ledger.AnteEarned;
            if (_shown > earned) _shown = earned;
            if (_shown < earned)
            {
                float rate = Mathf.Max(Config.countUpPerSecond, (earned - _shown) / 1.2f);
                _shown = Mathf.Min(earned, _shown + rate * dt);
                _counting = true;
                if (Time.unscaledTime >= _tickAt)
                {
                    _tickAt = Time.unscaledTime + 0.055f;
                    Audio.AudioDirector.PlayLocal("reward.points.tick", 0.5f);
                }
            }
            else if (_counting)
            {
                _counting = false;
                Audio.AudioDirector.PlayLocal("reward.points.rack_end", 0.7f);
            }
            int points = Mathf.FloorToInt(_shown);
            int target = d.Target;
            if (points != _shownPoints || target != _shownTarget)
            {
                _shownPoints = points;
                _shownTarget = target;
                _scoreLine.text = points.ToString("N0", Inv) + " / " + target.ToString("N0", Inv);
                float share = target > 0 ? Mathf.Clamp01(points / (float)target) : 0f;
                ((RectTransform)_barFill.transform).sizeDelta = new Vector2(200f * share, 0f);
                _barFill.color = points >= target ? new Color(0.55f, 1f, 0.55f) : Config.scoreColour;
            }

            int streak = Streak.CountAt(PlayTime);
            if (streak != _shownStreak)
            {
                _shownStreak = streak;
                _streakLine.text = streak >= 2 ? "STREAK x" + streak + (streak >= Config.rules.streakForFeat ? "  (next task +style)" : string.Empty) : string.Empty;
            }

            // Aboard, the ship's damage list takes this corner: the lines wait (the score still counts up).
            bool popsShown = OrbitalAnte.Ship.Ship.Boarded == null;
            for (int i = 0; i < _pops.Length; i++)
            {
                if (_popLeft[i] <= 0f) continue;
                if (_pops[i].gameObject.activeSelf != popsShown) _pops[i].gameObject.SetActive(popsShown);
                _popLeft[i] -= dt;
                if (_popLeft[i] <= 0f) _pops[i].gameObject.SetActive(false);
                else
                {
                    Color c = _pops[i].color;
                    c.a = Mathf.Clamp01(_popLeft[i] / 0.5f);
                    _pops[i].color = c;
                }
            }

            if (_noteLeft > 0f)
            {
                _noteLeft -= dt;
                bool on = _noteLeft > 0f;
                if (_noteLine.gameObject.activeSelf != on) _noteLine.gameObject.SetActive(on);
            }
        }
    }
}
