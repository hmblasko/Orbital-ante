using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OrbitalAnte.Ante;
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
    ///
    /// Goal unlocks (Ralph loop pass 1, 27 Nov 2026): Unlocks lists every goal and what it opens, earned or not, and
    /// New run asks which starting kit to take once one has been earned (the choice is kept for the next runs).
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

        enum Page { Main, HowTo, Records, Extras, Kit, Unlocks }

        Button _replayButton;
        Button _collectionButton;
        Button _unlocksButton;
        Text _bestLine, _howBody, _recordsBody, _extrasNote;
        Text _kitDetails, _kitLocked, _unlocksBody;
        readonly List<Button> _kitButtons = new List<Button>();
        /// <summary>The perk list, built once with the kit page (the details are written from it as the cursor moves).</summary>
        List<PerkDefinition> _perks;
        RectTransform _kitCard, _unlocksCard;
        GameObject _kitBack, _unlocksBack, _shownKitFor;

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
            var title = UiKit.Heading(card, "ORBITAL ANTE", 64, 84f);
            _bestLine = UiKit.LayoutLabel(card, string.Empty, 18, UiKit.Muted, 24f, TextAnchor.MiddleCenter, FontStyle.Normal);
            // Nine buttons must fit a 16:9 screen (759 units of the 800 the Deck gives): 48 px under the 60 px New run.
            const float Row = 48f;
            _playButton = UiKit.Button(card, "New run", NewRunPressed, 60f, 30).gameObject;
            _replayButton = UiKit.Button(card, "Play the last seed again", () =>
            {
                if (SaveSystem.Data.hasLastSeed) ReplayRequested?.Invoke(SaveSystem.Data.lastSeed, LastScenario());
            }, Row, 24);
            _howButton = UiKit.Button(card, "How to play", () => Show(Page.HowTo), Row, 24).gameObject;
            _unlocksButton = UiKit.Button(card, "Unlocks", () => Show(Page.Unlocks), Row, 24);
            _recordsButton = UiKit.Button(card, "Records", () => Show(Page.Records), Row, 24).gameObject;
            _collectionButton = UiKit.Button(card, "Collection", () => CollectionRequested?.Invoke(), Row, 24);
            UiKit.Button(card, "Practice range", () => PlayRequested?.Invoke(Scenario.CombatArena), Row, 24);
            _extrasButton = UiKit.Button(card, "Extras", () => Show(Page.Extras), Row, 24).gameObject;
            UiKit.Button(card, "Quit to desktop", () => QuitRequested?.Invoke(), Row, 24);

            RectTransform how = UiKit.Card(_canvas.transform, "HowToPlayCard", 760f);
            _howCard = (RectTransform)how.parent;
            UiKit.Heading(how, "HOW TO PLAY", 40, 56f);
            _howBody = UiKit.LayoutLabel(how, string.Empty, 17, UiKit.Cream, 410f, TextAnchor.UpperLeft, FontStyle.Normal);
            _howBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _howBack = UiKit.Button(how, "Back", () => Show(Page.Main)).gameObject;

            RectTransform rec = UiKit.Card(_canvas.transform, "RecordsCard", 560f);
            _recordsCard = (RectTransform)rec.parent;
            UiKit.Heading(rec, "RECORDS", 40, 56f);
            _recordsBody = UiKit.LayoutLabel(rec, string.Empty, 20, UiKit.Cream, 190f, TextAnchor.UpperLeft, FontStyle.Normal);
            _recordsBack = UiKit.Button(rec, "Back", () => Show(Page.Main)).gameObject;

            RectTransform ex = UiKit.Card(_canvas.transform, "ExtrasCard", 560f);
            _extrasCard = (RectTransform)ex.parent;
            UiKit.Heading(ex, "EXTRAS", 40, 56f);
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

            BuildKitPage();
            BuildUnlocksPage();
            Show(Page.Main);
        }

        // ------------------------------------------------------------------ starting kits and unlocks

        void BuildKitPage()
        {
            RectTransform kit = UiKit.Card(_canvas.transform, "KitCard", 760f);
            _kitCard = (RectTransform)kit.parent;
            UiKit.Heading(kit, "CHOOSE A KIT", 40, 56f);
            Text intro = UiKit.LayoutLabel(kit, "A kit starts the run with two perks of one family. They are the perk picks of antes 1 "
                + "and 2, so those antes have no pick of their own.", 16, UiKit.Muted, 44f, TextAnchor.MiddleCenter, FontStyle.Normal);
            intro.horizontalOverflow = HorizontalWrapMode.Wrap;
            _perks = RoguelitePerks.Create();
            IReadOnlyList<StartingKit> kits = StartingKits.All;
            for (int i = 0; i < kits.Count; i++)
            {
                StartingKit k = kits[i];
                Button b = UiKit.Button(kit, KitLabel(k, _perks), () => StartWithKit(k), 50f, 21);
                b.gameObject.AddComponent<SelectOnHover>();
                _kitButtons.Add(b);
            }
            _kitDetails = UiKit.LayoutLabel(kit, string.Empty, 16, UiKit.Cream, 92f, TextAnchor.UpperLeft, FontStyle.Normal);
            _kitDetails.horizontalOverflow = HorizontalWrapMode.Wrap;
            _kitLocked = UiKit.LayoutLabel(kit, string.Empty, 15, UiKit.Muted, 66f, TextAnchor.UpperLeft, FontStyle.Normal);
            _kitLocked.horizontalOverflow = HorizontalWrapMode.Wrap;
            _kitBack = UiKit.Button(kit, "Back", () => Show(Page.Main), 48f, 22).gameObject;
        }

        void BuildUnlocksPage()
        {
            RectTransform un = UiKit.Card(_canvas.transform, "UnlocksCard", 760f);
            _unlocksCard = (RectTransform)un.parent;
            UiKit.Heading(un, "UNLOCKS", 40, 56f);
            Text intro = UiKit.LayoutLabel(un, "Each is one goal to meet in a single run. Meet it and it is yours for every run after: "
                + "perks join your drafts, kits are chosen at New run.", 16, UiKit.Muted, 44f, TextAnchor.MiddleCenter, FontStyle.Normal);
            intro.horizontalOverflow = HorizontalWrapMode.Wrap;
            _unlocksBody = UiKit.LayoutLabel(un, string.Empty, 15, UiKit.Cream, 440f, TextAnchor.UpperLeft, FontStyle.Normal);
            _unlocksBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _unlocksBody.supportRichText = true;
            _unlocksBack = UiKit.Button(un, "Back", () => Show(Page.Main), 48f, 22).gameObject;
        }

        static string KitLabel(StartingKit kit, List<PerkDefinition> perks)
        {
            if (kit.perkIds.Length == 0) return kit.displayName + ":  no perks to start";
            var names = new List<string>();
            for (int i = 0; i < kit.perkIds.Length; i++)
            {
                PerkDefinition p = perks.Find(x => x.id == kit.perkIds[i]);
                names.Add(p != null ? p.displayName : kit.perkIds[i]);
            }
            return kit.displayName + ":  " + string.Join(" + ", names);
        }

        /// <summary>New run: straight in, or first the kit page once a kit has been earned.</summary>
        void NewRunPressed()
        {
            if (UnlockBook.OpenKits(SaveSystem.Data).Count > 1) Show(Page.Kit);
            else PlayRequested?.Invoke(Scenario.SolarSystem);
        }

        void StartWithKit(StartingKit kit)
        {
            // Kept for the runs after (Play the last seed again, Try again); the save is written as the run starts.
            UnlockBook.Choose(SaveSystem.Data, kit.id);
            PlayRequested?.Invoke(Scenario.SolarSystem);
        }

        void RefreshKitPage()
        {
            SaveData save = SaveSystem.Data;
            IReadOnlyList<StartingKit> kits = StartingKits.All;
            var locked = new StringBuilder();
            float longShot = Tasks.TaskConfig.Load().rules.longShot;
            for (int i = 0; i < kits.Count && i < _kitButtons.Count; i++)
            {
                bool open = UnlockBook.IsOpen(save, kits[i].id);
                _kitButtons[i].gameObject.SetActive(open);
                UnlockDefinition d = UnlockCatalog.ForTarget(kits[i].id);
                if (!open && d != null)
                    locked.Append(locked.Length == 0 ? "Still to earn:\n" : "\n").Append(kits[i].displayName).Append(": ")
                          .Append(UnlockCatalog.GoalWords(d.goal, longShot));
            }
            _kitLocked.text = locked.ToString();
            _shownKitFor = null;
        }

        /// <summary>The kit page's details follow the chosen button (D-pad, stick or mouse).</summary>
        void ShowKitDetails(GameObject chosen)
        {
            if (chosen == _shownKitFor) return;
            _shownKitFor = chosen;
            IReadOnlyList<StartingKit> kits = StartingKits.All;
            for (int i = 0; i < _kitButtons.Count && i < kits.Count; i++)
            {
                if (_kitButtons[i].gameObject != chosen) continue;
                StartingKit k = kits[i];
                _kitDetails.text = k.perkIds.Length == 0
                    ? "No perks to start: every pick is yours to choose, from three offers each ante."
                    : StartingKits.Details(k, _perks, AnteBalanceData.Load().balance);
                return;
            }
        }

        static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>Every unlock, easiest goal first: its goal while it is still to earn, what it gives once it is earned.</summary>
        static string UnlocksText(SaveData save)
        {
            string dim = Hex(HullKit.Dim), good = Hex(HullKit.Good), act = Hex(HullKit.Orange);
            float longShot = Tasks.TaskConfig.Load().rules.longShot;
            AnteBalance balance = AnteBalanceData.Load().balance;
            var s = new StringBuilder();
            IReadOnlyList<UnlockDefinition> all = UnlockCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                UnlockDefinition d = all[i];
                bool earned = UnlockBook.Has(save, d.id);
                if (i > 0) s.Append('\n');
                // The name in the label's own cream, the rest dimmed: no <b>, which would fake a bold B612 (UiKit.SetStyle).
                s.Append(d.name).Append("  <color=").Append(dim).Append(">· ").Append(UnlockCatalog.KindWords(d)).Append("</color>");
                if (earned)
                {
                    s.Append("  <color=").Append(good).Append(">UNLOCKED</color>");
                    if (UnlockBook.IsNew(save, d.id)) s.Append("  <color=").Append(act).Append(">NEW</color>");
                    s.Append("\n<color=").Append(dim).Append('>').Append(UnlockCatalog.GiftWords(d, balance)).Append("</color>");
                }
                else s.Append("\n<color=").Append(dim).Append(">Goal:</color> ").Append(UnlockCatalog.GoalWords(d.goal, longShot));
            }
            return s.ToString();
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
                int fresh = UnlockBook.NewCount(save);
                UiKit.ButtonText(_unlocksButton).text = fresh > 0 ? "Unlocks (" + fresh + " new)" : "Unlocks";
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
            else if (page == Page.Kit) _returnTo = _playButton;
            else if (page == Page.Unlocks) _returnTo = _unlocksButton.gameObject;
            else if (_page == Page.Main) _returnTo = null;
            _page = page;
            _mainCard.gameObject.SetActive(page == Page.Main);
            _howCard.gameObject.SetActive(page == Page.HowTo);
            _recordsCard.gameObject.SetActive(page == Page.Records);
            _extrasCard.gameObject.SetActive(page == Page.Extras);
            _kitCard.gameObject.SetActive(page == Page.Kit);
            _unlocksCard.gameObject.SetActive(page == Page.Unlocks);
            if (page == Page.Kit) RefreshKitPage();
            if (page == Page.Unlocks)
            {
                SaveData save = SaveSystem.Data;
                _unlocksBody.text = UnlocksText(save);
                // Seen now: NEW goes once the page has been shown (the words above still say it this time).
                if (UnlockBook.NewCount(save) > 0)
                {
                    UnlockBook.MarkAllSeen(save);
                    SaveSystem.Save();
                }
                UiKit.ButtonText(_unlocksButton).text = "Unlocks";
            }
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
                case Page.Unlocks: return _unlocksBack;
                case Page.Kit:
                {
                    // The kit chosen last time, so Try again and New run start the same way by default.
                    StartingKit chosen = UnlockBook.ChosenKit(SaveSystem.Data);
                    IReadOnlyList<StartingKit> kits = StartingKits.All;
                    for (int i = 0; i < kits.Count && i < _kitButtons.Count; i++)
                        if (kits[i] == chosen && _kitButtons[i].gameObject.activeSelf) return _kitButtons[i].gameObject;
                    return _kitButtons.Count > 0 ? _kitButtons[0].gameObject : _kitBack;
                }
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
                Upgrades = GameInput.Asset != null ? GameInput.Describe(GameInput.Upgrades) : "Tab",
            };
            string goal = Tasks.RunGuide.GoalCard(balance.TargetFor(1), Mathf.RoundToInt(balance.anteSeconds), balance.AnteCount, keys);
            string map = GameInput.Asset != null ? GameInput.Describe(GameInput.ToggleMap) : "M";
            string lockKey = GameInput.Asset != null ? GameInput.Describe(GameInput.LockTarget) : "?";
            string auto = GameInput.Asset != null ? GameInput.Describe(GameInput.Autopilot) : "?";
            return goal + "\n\nFlying: " + map + " opens the map (worlds you have visited show the tasks left there). Point the nose at a world, "
                   + lockKey + " locks it and " + auto + " flies you there.\nOn foot: limbs come off, so shoot the gun or the arm to disarm, a leg to drop them. "
                   + "Only a destroyed head kills, yours too.\nUnlocks: one goal met in a run opens a new perk or starting kit for every run after. "
                   + "The Unlocks page lists each goal.";
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
            if (_page == Page.Kit && selected != null) ShowKitDetails(selected);
            if (selected != null && selected.activeInHierarchy) return;
            bool navigating = GameInput.UiNavigate.ReadValue<Vector2>().sqrMagnitude > 0.25f
                              || GameInput.UiSubmit.WasPressedThisFrame();
            if (navigating) es.SetSelectedGameObject(Default());
        }
    }
}
