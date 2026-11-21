using System;
using System.Globalization;
using OrbitalAnte.Core;
using OrbitalAnte.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// The title screen (21 Nov 2026: "a fully functioning game with clear goals"). The real run comes first
    /// (New run, Continue), then How to play and Records, so a new player can find out what the game is before
    /// starting, and the developer things (the audio showcase, the build label) sit on an Extras page. The
    /// combat arena stays as Practice range. The notebook (Part B section 13) comes later.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        public Action<Scenario> PlayRequested;
        public Action QuitRequested;
        /// <summary>Play the five-minute audio showcase.</summary>
        public Action AudioShowcaseRequested;
        /// <summary>Play the most recent seed again. The argument is that seed.</summary>
        public Action<int, Scenario> ReplayRequested;
        /// <summary>Show the collection and envelopes waiting to be opened.</summary>
        public Action CollectionRequested;

        enum Page { Main, HowTo, Records, Extras }

        Button _replayButton;
        Button _collectionButton;
        Text _bestLine, _howBody, _recordsBody, _extrasNote;

        Canvas _canvas;
        GameObject _playButton;
        RectTransform _mainCard, _howCard, _recordsCard, _extrasCard;
        GameObject _howBack, _recordsBack, _extrasFirst;
        GameObject _howButton, _recordsButton, _extrasButton;
        GameObject _returnTo;
        Page _page = Page.Main;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public void Build(Transform uiRoot)
        {
            _canvas = UiKit.CreateCanvas("MainMenu", uiRoot, 50);

            RectTransform card = UiKit.Card(_canvas.transform, "TitleCard", 620f);
            _mainCard = (RectTransform)card.parent;
            var title = UiKit.LayoutLabel(card, "ORBITAL ANTE", 64, UiKit.PanelEdge, 84f);
            title.transform.localRotation = Quaternion.Euler(0f, 0f, 2f);
            _bestLine = UiKit.LayoutLabel(card, string.Empty, 18, UiKit.Muted, 24f, TextAnchor.MiddleCenter, FontStyle.Normal);
            // Nine rows must fit the Deck's 800 px: buttons 48 px under the 60 px New run.
            const float Row = 48f;
            _playButton = UiKit.Button(card, "New run", () => PlayRequested?.Invoke(Scenario.SolarSystem), 60f, 30).gameObject;
            _replayButton = UiKit.Button(card, "Play the last seed again", () =>
            {
                if (SaveSystem.Data.hasLastSeed) ReplayRequested?.Invoke(SaveSystem.Data.lastSeed, LastScenario());
            }, Row, 24);
            _howButton = UiKit.Button(card, "How to play", () => Show(Page.HowTo), Row, 24).gameObject;
            _recordsButton = UiKit.Button(card, "Records", () => Show(Page.Records), Row, 24).gameObject;
            _collectionButton = UiKit.Button(card, "Collection", () => CollectionRequested?.Invoke(), Row, 24);
            UiKit.Button(card, "Practice range", () => PlayRequested?.Invoke(Scenario.CombatArena), Row, 24);
            _extrasButton = UiKit.Button(card, "Extras", () => Show(Page.Extras), Row, 24).gameObject;
            UiKit.Button(card, "Quit to desktop", () => QuitRequested?.Invoke(), Row, 24);

            RectTransform how = UiKit.Card(_canvas.transform, "HowToPlayCard", 760f);
            _howCard = (RectTransform)how.parent;
            UiKit.LayoutLabel(how, "HOW TO PLAY", 40, UiKit.PanelEdge, 56f);
            _howBody = UiKit.LayoutLabel(how, string.Empty, 17, UiKit.Cream, 360f, TextAnchor.UpperLeft, FontStyle.Normal);
            _howBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _howBack = UiKit.Button(how, "Back", () => Show(Page.Main)).gameObject;

            RectTransform rec = UiKit.Card(_canvas.transform, "RecordsCard", 560f);
            _recordsCard = (RectTransform)rec.parent;
            UiKit.LayoutLabel(rec, "RECORDS", 40, UiKit.PanelEdge, 56f);
            _recordsBody = UiKit.LayoutLabel(rec, string.Empty, 20, UiKit.Cream, 190f, TextAnchor.UpperLeft, FontStyle.Normal);
            _recordsBack = UiKit.Button(rec, "Back", () => Show(Page.Main)).gameObject;

            RectTransform ex = UiKit.Card(_canvas.transform, "ExtrasCard", 560f);
            _extrasCard = (RectTransform)ex.parent;
            UiKit.LayoutLabel(ex, "EXTRAS", 40, UiKit.PanelEdge, 56f);
            _extrasFirst = UiKit.Button(ex, "Audio showcase (5 min)", () => AudioShowcaseRequested?.Invoke()).gameObject;
            UiKit.Button(ex, "Show the new-player tips again", () =>
            {
                Tasks.RunGuideFx.ForgetTips();
                FeelSettings.GuideTips = true;
                _extrasNote.text = "Tips are back on. The goal card shows at the start of your next run.";
            });
            _extrasNote = UiKit.LayoutLabel(ex, string.Empty, 16, UiKit.Muted, 44f, TextAnchor.MiddleCenter, FontStyle.Normal);
            _extrasNote.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.LayoutLabel(ex, GameSession.BuildLabel, 16, UiKit.Muted, 24f, TextAnchor.MiddleCenter, FontStyle.Normal);
            UiKit.Button(ex, "Back", () => Show(Page.Main));

            Show(Page.Main);
        }

        public void SetVisible(bool visible)
        {
            _canvas.gameObject.SetActive(visible);
            if (visible)
            {
                // Only offered once there is a seed to go back to (it is remembered between sessions).
                bool has = SaveSystem.Data.hasLastSeed;
                _replayButton.gameObject.SetActive(has);
                if (has)
                {
                    UiKit.ButtonText(_replayButton).text = "Seed " + SaveSystem.Data.lastSeed + " again (" +
                        (LastScenario() == Scenario.SolarSystem ? "solar system" : "practice range") + ")";
                }
                int waiting = SaveSystem.Data.unopenedEnvelopes != null ? SaveSystem.Data.unopenedEnvelopes.Count : 0;
                UiKit.ButtonText(_collectionButton).text = waiting > 0
                    ? "Collection (" + waiting + (waiting == 1 ? " envelope" : " envelopes") + " to open)"
                    : "Collection";
                SaveData save = SaveSystem.Data;
                _bestLine.text = save.runsEnded > 0
                    ? "Best ante " + save.bestAnte + "   Best score " + save.bestRunScore.ToString("N0", Inv)
                    : "A run is " + Ante.AnteBalanceData.Load().balance.AnteCount + " antes. How far can you get?";
                Show(Page.Main);
            }
        }

        void Show(Page page)
        {
            // Back returns to the button that opened the page.
            if (page == Page.HowTo) _returnTo = _howButton;
            else if (page == Page.Records) _returnTo = _recordsButton;
            else if (page == Page.Extras) _returnTo = _extrasButton;
            else if (_page == Page.Main) _returnTo = null;
            _page = page;
            _mainCard.gameObject.SetActive(page == Page.Main);
            _howCard.gameObject.SetActive(page == Page.HowTo);
            _recordsCard.gameObject.SetActive(page == Page.Records);
            _extrasCard.gameObject.SetActive(page == Page.Extras);
            if (page == Page.HowTo) _howBody.text = HowToPlay();
            if (page == Page.Records) _recordsBody.text = Records(SaveSystem.Data);
            if (page == Page.Extras) _extrasNote.text = string.Empty;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(Default());
        }

        GameObject Default()
        {
            switch (_page)
            {
                case Page.HowTo: return _howBack;
                case Page.Records: return _recordsBack;
                case Page.Extras: return _extrasFirst;
                default:
                    GameObject back = _returnTo;
                    _returnTo = null;
                    return back != null && back.activeInHierarchy ? back : _playButton;
            }
        }

        static string HowToPlay()
        {
            Ante.AnteBalance balance = Ante.AnteBalanceData.Load().balance;
            var keys = new Tasks.RunGuide.Keys
            {
                LiftOff = GameInput.Asset != null ? GameInput.Describe(GameInput.FlyUp) : "Space",
            };
            string goal = Tasks.RunGuide.GoalCard(balance.TargetFor(1), Mathf.RoundToInt(balance.anteSeconds), balance.AnteCount, keys);
            string map = GameInput.Asset != null ? GameInput.Describe(GameInput.ToggleMap) : "M";
            string lockKey = GameInput.Asset != null ? GameInput.Describe(GameInput.LockTarget) : "?";
            string auto = GameInput.Asset != null ? GameInput.Describe(GameInput.Autopilot) : "?";
            return goal + "\n\nFlying: " + map + " opens the map (worlds you have visited show the tasks left there). Point the nose at a world, "
                   + lockKey + " locks it and " + auto + " flies you there.\nOn foot: limbs come off, so shoot the gun or the arm to disarm, a leg to drop them. "
                   + "Only a destroyed head kills, yours too.";
        }

        static string Records(SaveData save)
        {
            if (save == null || save.runsEnded == 0) return "No runs finished yet. Your best ante and score will show here.";
            double hours = save.secondsPlayed / 3600.0;
            return "Best ante reached   " + save.bestAnte + "\n" +
                   "Best run score   " + save.bestRunScore.ToString("N0", Inv) + "\n" +
                   "Antes cleared   " + save.antesCleared.ToString("N0", Inv) + "\n" +
                   "Runs finished   " + save.runsEnded.ToString("N0", Inv) + "\n" +
                   "Time played   " + (hours >= 1.0 ? hours.ToString("0.0", Inv) + " h" : Mathf.RoundToInt((float)(save.secondsPlayed / 60.0)) + " min");
        }

        static Scenario LastScenario()
        {
            return SaveSystem.Data.lastScenario == (int)Scenario.SolarSystem ? Scenario.SolarSystem : Scenario.CombatArena;
        }

        void Update()
        {
            if (_canvas == null || !_canvas.gameObject.activeSelf || GameInput.Asset == null) return;
            // B / Esc on a sub-page goes back to the title.
            if (_page != Page.Main && GameInput.UiCancel.WasPressedThisFrame())
            {
                Show(Page.Main);
                return;
            }
            EventSystem es = EventSystem.current;
            if (es == null) return;
            GameObject selected = es.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy) return;
            bool navigating = GameInput.UiNavigate.ReadValue<Vector2>().sqrMagnitude > 0.25f
                              || GameInput.UiSubmit.WasPressedThisFrame();
            if (navigating) es.SetSelectedGameObject(Default());
        }
    }
}
