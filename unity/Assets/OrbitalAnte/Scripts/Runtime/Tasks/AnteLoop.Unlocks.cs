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
    /// Goal unlocks in a run (Ralph loop pass 1, 27 Nov 2026; the rules are Ante/Unlocks.cs): the starting kit chosen
    /// on the title screen, the perks still locked kept out of the drafts and a newly unlocked one shown in the next
    /// draft, and every goal met saved the moment it is met. The moment itself is a green banner, UNLOCKED: and the
    /// name, over a note that says what earned it and where it turns up, with a sound of its own (<c>reward.unlock</c>).
    /// It waits out a fight (at most <see cref="UnlockMoment.FightWaitMost"/> s), an important note and the gun ladder's
    /// banner, so it is read rather than lost in a firefight. Only solar-system runs count: the practice range has no
    /// ante loop. Since Ralph loop pass 5 a goal can give a look (a paint, a skin, a suit piece): it is put in the
    /// collection and, where its place is empty, into use by <see cref="UnlockBook.Grant"/>, and the note says which.
    /// </summary>
    public sealed partial class AnteLoop
    {
        UnlockTracker _unlocks;
        readonly List<UnlockDefinition> _unlockedThisRun = new List<UnlockDefinition>();
        /// <summary>Goals met in the field, each shown in turn once nothing holds the middle of the screen.</summary>
        readonly List<UnlockDefinition> _unlockNotes = new List<UnlockDefinition>();
        /// <summary>What the break page says about goals met when the ante was cleared.</summary>
        string _unlockBreakLines;
        float _unlockSoundAt = -1f;
        string _kitNote;
        /// <summary>
        /// An unlock waits to be written to the save: once, the frame after the goal, so a kill that meets three
        /// goals writes one save and not on the kill's own frame (deck-performance review, 27 Nov 2026). A goal met
        /// in a fight waits for the fight to end, a pause, or <see cref="UnlockMoment.FightWaitMost"/> s, so a new
        /// player's first fight (a disarm, then a leg) gets one write after it, not two in it (deck-performance
        /// review, 28 Nov 2026). Every way a run ends saves anyway (<see cref="RecapAndRecord"/>), so only a crash
        /// before then loses it.
        /// </summary>
        bool _unlockSaveOwed;
        int _unlockSaveFrame;
        float _unlockSaveOwedAt;

        Text _unlockBanner;
        float _unlockBannerLeft;
        /// <summary>Unpaused seconds the first waiting unlock has waited for a fight to end.</summary>
        float _unlockWaited;

        /// <summary>
        /// After the ante clear's own fanfare (reward.tier3 is about this long), so the two never land together.
        /// </summary>
        const float UnlockAfterFanfare = 1.6f;
        /// <summary>How long the banner shows, the last half second fading.</summary>
        const float UnlockBannerSeconds = 3.5f;

        /// <summary>A run has begun: a fresh count, the chosen kit's perks, and the locked perks out of the drafts.</summary>
        void BeginUnlocks()
        {
            _unlocks = new UnlockTracker();
            _unlockedThisRun.Clear();
            _unlockNotes.Clear();
            _unlockBreakLines = null;
            _unlockSoundAt = -1f;
            _unlockBannerLeft = 0f;
            _unlockWaited = 0f;
            if (_unlockBanner != null) _unlockBanner.gameObject.SetActive(false);
            _kitNote = null;
            Save.SaveData save = Save.SaveSystem.Data;
            StartingKit kit = UnlockBook.ChosenKit(save);
            if (Run.StartWithKit(kit))
            {
                var names = new List<string>();
                for (int i = 0; i < Run.Perks.Owned.Count; i++) names.Add(Run.Perks.Owned[i].displayName.ToUpperInvariant());
                _kitNote = kit.displayName.ToUpperInvariant() + ": " + string.Join(" AND ", names) + " FROM THE START · FIRST PERK PICK AT ANTE "
                           + StartingKits.FirstPickAnte(kit);
            }
            Run.SetLocked(UnlockBook.LockedPerkIds(save), UnlockBook.PerkToShow(save));
        }

        /// <summary>A new ante: the in-one-ante goals count from nothing, and perks unlocked last ante join the drafts.</summary>
        void BeginAnteUnlocks()
        {
            _unlockBreakLines = null;
            _unlockNotes.Clear();
            _unlockWaited = 0f;
            if (_unlocks != null) _unlocks.AnteBegan();
            Save.SaveData save = Save.SaveSystem.Data;
            if (Run != null) Run.SetLocked(UnlockBook.LockedPerkIds(save), UnlockBook.PerkToShow(save));
        }

        bool CountsForUnlocks => _unlocks != null && Run != null && Run.Director.IsRunning;

        /// <summary>The task director saw the player kill an enemy or a creature.</summary>
        public static void UnlockKill(in KillFacts f)
        {
            AnteLoop loop = Current;
            if (loop == null || !loop.CountsForUnlocks) return;
            loop._unlocks.Kill(f.melee, f.rover, f.longShot, f.lowStance, f.lowArmour, airborne: f.airborne, inFocus: f.inFocus, seaCreature: f.seaCreature);
            loop.TakeUnlocks(false);
        }

        /// <summary>The task director saw the player knock a gun out of a living enemy's hands.</summary>
        public static void UnlockDisarm()
        {
            AnteLoop loop = Current;
            if (loop == null || !loop.CountsForUnlocks) return;
            loop._unlocks.Disarmed();
            loop.TakeUnlocks(false);
        }

        /// <summary>The task director saw the player take a leg off an enemy standing on two.</summary>
        public static void UnlockLegOff()
        {
            AnteLoop loop = Current;
            if (loop == null || !loop.CountsForUnlocks) return;
            loop._unlocks.LegOff();
            loop.TakeUnlocks(false);
        }

        /// <summary>The task director saw a salvage crate come aboard.</summary>
        public static void UnlockSalvageHome()
        {
            AnteLoop loop = Current;
            if (loop == null || !loop.CountsForUnlocks) return;
            loop._unlocks.SalvageHome();
            loop.TakeUnlocks(false);
        }

        /// <summary>From the bonus mission's own payout (AnteLoop.Contracts): the ante's bonus mission is done.</summary>
        void UnlockBonusMission()
        {
            if (!CountsForUnlocks) return;
            _unlocks.BonusMissionDone();
            TakeUnlocks(false);
        }

        /// <summary>The task director saw the ship touch down on a world (its body id, "moon").</summary>
        public static void UnlockLanded(string worldId)
        {
            AnteLoop loop = Current;
            if (loop == null || !loop.CountsForUnlocks) return;
            loop._unlocks.Landed(worldId);
            loop.TakeUnlocks(false);
        }

        /// <summary>From <see cref="Report"/>: points of some kind were paid in a running ante.</summary>
        void UnlockScored(ScoreResult result)
        {
            if (!CountsForUnlocks || !result.Awarded || result.points <= 0) return;
            _unlocks.Scored(result.category);
            TakeUnlocks(false);
        }

        void OnLadderTop()
        {
            if (!CountsForUnlocks) return;
            _unlocks.LadderTop();
            TakeUnlocks(false);
        }

        /// <summary>An ante was cleared (before the break or the victory page is shown).</summary>
        void UnlockAnteCleared(int ante)
        {
            if (_unlocks == null || Run == null) return;
            // Field unlocks not shown yet (the ante was banked mid-fight, or within a few seconds) go on the break page.
            bool owed = _unlockNotes.Count > 0;
            for (int i = 0; i < _unlockNotes.Count; i++) AddBreakLine(_unlockNotes[i]);
            _unlockNotes.Clear();
            _unlockWaited = 0f;
            _unlocks.AnteCleared(ante, CurrentHazard != Hazard.None, Run.Director.ClearedInOvertime);
            TakeUnlocks(true);
            if (owed && _unlockSoundAt < 0f) _unlockSoundAt = Time.unscaledTime + UnlockAfterFanfare;
        }

        void AddBreakLine(UnlockDefinition d)
        {
            bool inUse = UnlockBook.LookInUse(Save.SaveSystem.Data, d);
            string line = UnlockCatalog.BreakLine(d, FirstDraftAnte, Hints(), inUse);
            _unlockBreakLines = string.IsNullOrEmpty(_unlockBreakLines) ? line : _unlockBreakLines + "\n" + line;
            PutOnNow(d, inUse);
        }

        /// <summary>
        /// A look that went to work goes on as its moment shows (Ralph loop pass 5, design review: a look is seen when it
        /// is earned, not a run later): the hull, the suit and the gun in hand read the Locker again, on the next frame
        /// that writes no save, once however many looks one page names.
        /// </summary>
        static void PutOnNow(UnlockDefinition d, bool inUse)
        {
            if (inUse && UnlockCatalog.IsLook(d)) Items.LockerLook.RefreshSoon();
        }

        /// <summary>
        /// The first ante whose draft can offer a perk unlocked now: the next one, or the first a kit run picks in;
        /// 0 when this run has no draft left (the last ante, not carried on past).
        /// </summary>
        int FirstDraftAnte
        {
            get
            {
                if (Run == null) return 2;
                AnteDirector d = Run.Director;
                if (d.IsLastAnte && !d.Endless) return 0;
                return Mathf.Max(d.Ante + 1, d.PicksGivenAhead + 1);
            }
        }

        /// <summary>The goal words' numbers and keys: the task rules' LONG SHOT, the low-health line, the player's own bindings.</summary>
        UnlockHints Hints()
        {
            var hints = new UnlockHints
            {
                longShotMetres = Config != null ? Config.rules.longShot : 60f,
                lowArmourLine = Run != null ? Run.Balance.lowHealthLine : 0.30f,
            };
            string crouch = KeyName(GameInput.JetDown), interact = KeyName(GameInput.Interact), focus = KeyName(GameInput.DeepBreath);
            if (crouch != null) hints.crouchKey = crouch;
            if (interact != null) hints.interactKey = interact;
            if (focus != null) hints.focusKey = focus;
            return hints;
        }

        /// <summary>The key or button an action is on, or null before the input is set up.</summary>
        internal static string KeyName(UnityEngine.InputSystem.InputAction action)
        {
            if (action == null) return null;
            string name = GameInput.Describe(action);
            return string.IsNullOrEmpty(name) ? null : name;
        }

        /// <summary>
        /// Grants whatever the goals just met unlock and owes the save a write, made next frame (a crash or a pulled
        /// plug a minute later must not lose it). In the field each gets its moment in turn; at a cleared ante the
        /// break page says it instead, since the readout is hidden there.
        /// </summary>
        void TakeUnlocks(bool atTheBreak)
        {
            if (_unlocks == null || !_unlocks.HasWaiting) return;
            List<UnlockGoal> met = _unlocks.Take();
            Save.SaveData save = Save.SaveSystem.Data;
            bool any = false;
            for (int i = 0; i < met.Count; i++)
            {
                UnlockDefinition d = UnlockBook.Grant(save, met[i]);
                if (d == null) continue;
                any = true;
                if (Playtest.PlaytestHooks.Log != null) Playtest.PlaytestHooks.UnlockMet(d.name, InFight, atTheBreak);
                _unlockedThisRun.Add(d);
                if (atTheBreak) AddBreakLine(d);
                else _unlockNotes.Add(d);
            }
            if (!any) return;
            OweUnlockSave();
            if (atTheBreak) _unlockSoundAt = Time.unscaledTime + UnlockAfterFanfare;
        }

        void OweUnlockSave()
        {
            // A second goal before the write keeps the first one's wait: one write for both, never later than the first allows.
            if (!_unlockSaveOwed) _unlockSaveOwedAt = Time.unscaledTime;
            _unlockSaveOwed = true;
            _unlockSaveFrame = Time.frameCount;
        }

        /// <summary>First thing every frame, run or no run: the owed write, the frame after the goal, once no fight is on.</summary>
        void WriteOwedUnlockSave()
        {
            if (!_unlockSaveOwed || Time.frameCount <= _unlockSaveFrame) return;
            bool paused = GameSession.Paused || TimeFlow.Held;
            if (!UnlockMoment.SaveNow(InFight, paused, Time.unscaledTime - _unlockSaveOwedAt)) return;
            _unlockSaveOwed = false;
            Save.SaveSystem.Save();
        }

        /// <summary>
        /// The draft on screen holds the perk unlocked since the last draft: its card says NEW, here and if the page
        /// opens again this ante, and the save owes it no showing more. Null when it is not among the offers.
        /// </summary>
        string ShowingNewPerk(List<PerkDefinition> offers)
        {
            string id = Run != null ? Run.FeaturedPerkId : null;
            if (string.IsNullOrEmpty(id) || offers == null) return null;
            for (int i = 0; i < offers.Count; i++)
            {
                if (offers[i] == null || offers[i].id != id) continue;
                if (UnlockBook.MarkShown(Save.SaveSystem.Data, id)) OweUnlockSave();
                return id;
            }
            return null;
        }

        /// <summary>The unlock moment's banner: under the note line, where the gun ladder's NEW GUN shows.</summary>
        void BuildUnlockHud()
        {
            _unlockBanner = HullKit.Stencil(_canvas.transform, string.Empty, 36, HullKit.Good, TextAnchor.MiddleCenter);
            UiKit.Pin((RectTransform)_unlockBanner.transform, new Vector2(0.5f, 1f), new Vector2(0f, -178f), new Vector2(900f, 50f));
            UiKit.Halo(_unlockBanner, true);
            _unlockBanner.gameObject.SetActive(false);
        }

        static bool LadderBannerUp => GunLadderFx.Instance != null && GunLadderFx.Instance.BannerShowing;

        void UpdateUnlocks(float dt, bool paused)
        {
            // At victory the envelope's reveal plays first (ante 9 is the envelope's ante): the sound waits until it is done.
            if (_unlockSoundAt >= 0f && Envelopes != null && Envelopes.Showing) _unlockSoundAt = Mathf.Max(_unlockSoundAt, Time.unscaledTime + 0.4f);
            if (_unlockSoundAt >= 0f && Time.unscaledTime >= _unlockSoundAt)
            {
                _unlockSoundAt = -1f;
                Audio.AudioDirector.PlayLocal("reward.unlock");
            }
            if (_kitNote != null && Run != null && Run.Director.IsRunning && !_notes.Holding)
            {
                Note(_kitNote, false);
                _kitNote = null;
            }
            // The Locker's upgrade is said once the kit's note (or anything else) has finished, never over it.
            if (_kitNote == null && _upgradeNote != null && Run != null && Run.Director.IsRunning && !_notes.Holding && _notes.Text == null)
            {
                Note(_upgradeNote, false);
                _upgradeNote = null;
            }

            bool shown = _canvas != null && _canvas.gameObject.activeSelf;
            if (_unlockBannerLeft > 0f && !paused && shown)
            {
                _unlockBannerLeft -= dt;
                // The gun ladder's banner takes the same place: if a new gun comes while this shows, it gives way (the
                // unlock's note stays on its own line above).
                if (LadderBannerUp) _unlockBannerLeft = 0f;
                if (_unlockBannerLeft <= 0f) _unlockBanner.gameObject.SetActive(false);
                else _unlockBanner.canvasRenderer.SetAlpha(Mathf.Clamp01(_unlockBannerLeft / 0.5f));
            }

            if (_unlockNotes.Count == 0 || Run == null || !Run.Director.IsRunning) return;
            if (!paused) _unlockWaited += dt;
            if (!UnlockMoment.MayShow(shown, _notes.Holding, LadderBannerUp || _unlockBannerLeft > 0f, InFight, _unlockWaited)) return;
            ShowUnlock(_unlockNotes[0]);
            _unlockNotes.RemoveAt(0);
            _unlockWaited = 0f;
        }

        void ShowUnlock(UnlockDefinition d)
        {
            if (Playtest.PlaytestHooks.Log != null) Playtest.PlaytestHooks.UnlockShown(d.name, _unlockWaited);
            _unlockBanner.text = UnlockCatalog.BannerWords(d);
            _unlockBanner.canvasRenderer.SetAlpha(1f);
            _unlockBanner.gameObject.SetActive(true);
            _unlockBannerLeft = UnlockBannerSeconds;
            bool inUse = UnlockBook.LookInUse(Save.SaveSystem.Data, d);
            Note(UnlockCatalog.EarnedNote(d, FirstDraftAnte, Hints(), inUse), true);
            Audio.AudioDirector.PlayLocal("reward.unlock");
            PutOnNow(d, inUse);
        }

        /// <summary>For the break and the victory page: what clearing the ante unlocked, or nothing.</summary>
        string UnlockBreakLines => _unlockBreakLines;

        /// <summary>The end of the run recap: this run's unlocks, the next two to earn, and the next mark.</summary>
        string UnlockRecapLines(int cleared)
        {
            return UnlockRecap.Lines(Save.SaveSystem.Data, _unlockedThisRun, cleared, Run.Balance.AnteCount, Hints());
        }

        /// <summary>On the perk row in the antes a kit run does not pick: the kit's perks, or its price.</summary>
        string KitPickWords()
        {
            StartingKit kit = Run != null ? StartingKits.Find(Run.KitId) : null;
            return StartingKits.NoPickWords(kit, Run != null ? Run.Director.Ante : 1);
        }
    }
}
