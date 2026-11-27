using System.Collections.Generic;
using System.Text;
using OrbitalAnte.Ante;
using OrbitalAnte.Combat;
using UnityEngine;

namespace OrbitalAnte.Tasks
{
    /// <summary>
    /// Goal unlocks in a run (Ralph loop pass 1, 27 Nov 2026; the rules are Ante/Unlocks.cs): the starting kit chosen
    /// on the title screen, the perks still locked kept out of the drafts, and every goal met saved the moment it is
    /// met, with a sound of its own (<c>reward.unlock</c>) and a note nothing writes over. Only solar-system runs
    /// count: the practice range has no ante loop.
    /// </summary>
    public sealed partial class AnteLoop
    {
        UnlockTracker _unlocks;
        readonly List<UnlockDefinition> _unlockedThisRun = new List<UnlockDefinition>();
        /// <summary>Goals met in the field, each shown as a note in turn once nothing important holds the line.</summary>
        readonly List<UnlockDefinition> _unlockNotes = new List<UnlockDefinition>();
        /// <summary>What the break page says about goals met when the ante was cleared.</summary>
        string _unlockBreakLines;
        float _unlockSoundAt = -1f;
        string _kitNote;

        /// <summary>
        /// After the ante clear's own fanfare (reward.tier3 is about this long), so the two never land together.
        /// </summary>
        const float UnlockAfterFanfare = 1.6f;

        /// <summary>A run has begun: a fresh count, the chosen kit's perks, and the locked perks out of the drafts.</summary>
        void BeginUnlocks()
        {
            _unlocks = new UnlockTracker();
            _unlockedThisRun.Clear();
            _unlockNotes.Clear();
            _unlockBreakLines = null;
            _unlockSoundAt = -1f;
            _kitNote = null;
            Save.SaveData save = Save.SaveSystem.Data;
            StartingKit kit = UnlockBook.ChosenKit(save);
            if (Run.StartWithKit(kit))
            {
                var names = new List<string>();
                for (int i = 0; i < Run.Perks.Owned.Count; i++) names.Add(Run.Perks.Owned[i].displayName.ToUpperInvariant());
                _kitNote = kit.displayName.ToUpperInvariant() + ": " + string.Join(" AND ", names) + " FROM THE START";
            }
            Run.SetLocked(UnlockBook.LockedPerkIds(save));
        }

        /// <summary>A new ante: the in-one-ante goals count from nothing, and perks unlocked last ante join the drafts.</summary>
        void BeginAnteUnlocks()
        {
            _unlockBreakLines = null;
            _unlockNotes.Clear();
            if (_unlocks != null) _unlocks.AnteBegan();
            if (Run != null) Run.SetLocked(UnlockBook.LockedPerkIds(Save.SaveSystem.Data));
        }

        bool CountsForUnlocks => _unlocks != null && Run != null && Run.Director.IsRunning;

        /// <summary>The task director saw the player kill an enemy.</summary>
        public static void UnlockKill(in KillFacts f)
        {
            AnteLoop loop = Current;
            if (loop == null || !loop.CountsForUnlocks) return;
            loop._unlocks.Kill(f.melee, f.rover, f.longShot, f.lowStance);
            loop.TakeUnlocks(false);
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
            // Field unlocks whose note has not been shown yet (the ante was banked within a few seconds) go on the break page.
            bool owed = _unlockNotes.Count > 0;
            for (int i = 0; i < _unlockNotes.Count; i++) AddBreakLine(_unlockNotes[i]);
            _unlockNotes.Clear();
            _unlocks.AnteCleared(ante, Run.Director.SecondsLeft, CurrentHazard != Hazard.None, Run.Director.ClearedInOvertime);
            TakeUnlocks(true);
            if (owed && _unlockSoundAt < 0f) _unlockSoundAt = Time.unscaledTime + UnlockAfterFanfare;
        }

        void AddBreakLine(UnlockDefinition d)
        {
            string line = "UNLOCKED: " + d.name + ", " + UnlockCatalog.KindWords(d) + ". "
                          + (d.kind == UnlockKind.Kit ? "Choose it when you start a run." : "In your perk drafts from the next ante.");
            _unlockBreakLines = string.IsNullOrEmpty(_unlockBreakLines) ? line : _unlockBreakLines + "\n" + line;
        }

        /// <summary>
        /// Saves whatever the goals just met unlock, at once (a run can end a second later). In the field each gets
        /// a note in turn; at a cleared ante the break page says it instead, since the readout is hidden there.
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
                _unlockedThisRun.Add(d);
                if (atTheBreak) AddBreakLine(d);
                else _unlockNotes.Add(d);
            }
            if (!any) return;
            Save.SaveSystem.Save();
            if (atTheBreak) _unlockSoundAt = Time.unscaledTime + UnlockAfterFanfare;
        }

        /// <summary>
        /// Once a frame, after the target-met note: the next unlock note shows when nothing important holds the
        /// line, with its sound, so TARGET MET and an unlock both get read.
        /// </summary>
        void UpdateUnlocks()
        {
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
            if (_unlockNotes.Count == 0 || _notes.Holding || Run == null || !Run.Director.IsRunning) return;
            Note(UnlockCatalog.EarnedNote(_unlockNotes[0]), true);
            _unlockNotes.RemoveAt(0);
            Audio.AudioDirector.PlayLocal("reward.unlock");
        }

        /// <summary>For the break and the victory page: what clearing the ante unlocked, or nothing.</summary>
        string UnlockBreakLines => _unlockBreakLines;

        /// <summary>The run recap's unlock lines: what this run unlocked, and the next goal to aim at.</summary>
        string UnlockRecapLines()
        {
            var s = new StringBuilder();
            if (_unlockedThisRun.Count > 0)
            {
                var names = new List<string>();
                for (int i = 0; i < _unlockedThisRun.Count; i++) names.Add(_unlockedThisRun[i].name);
                s.Append("Unlocked this run: ").Append(string.Join(", ", names)).Append(". See Unlocks on the title screen.\n");
            }
            UnlockDefinition next = UnlockBook.Next(Save.SaveSystem.Data);
            if (next != null)
                s.Append("Next unlock, ").Append(next.name).Append(": ").Append(UnlockCatalog.GoalWords(next.goal, Config.rules.longShot)).Append('\n');
            return s.ToString();
        }

        /// <summary>On the break's perk row in antes 1 and 2 of a kit run: the pick came with the kit.</summary>
        string KitPickWords()
        {
            StartingKit kit = Run != null ? StartingKits.Find(Run.KitId) : null;
            return "from your " + (kit != null ? kit.displayName : "kit");
        }
    }
}
