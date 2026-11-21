using System;

namespace OrbitalAnte.Ante
{
    public enum AntePhase
    {
        /// <summary>An ante is being played: the timer runs and events score.</summary>
        Running = 0,
        /// <summary>The ante was cleared. The upgrade screen is available; nothing scores; the timer is stopped.</summary>
        Intermission = 1,
        /// <summary>The last ante was cleared.</summary>
        Victory = 2,
        /// <summary>The run is over: the player died (or, with <see cref="MissedTarget.EndRun"/>, the timer ran out short of the target).</summary>
        Failed = 3,
    }

    /// <summary>What pressing Select should show. Pressing Select never changes the state of the run.</summary>
    public enum SelectView
    {
        /// <summary>During an ante: a read-only look at progress, attributes and perks. Advancing early is a separate, confirmed action.</summary>
        RunStatus = 0,
        /// <summary>Between antes: the upgrade screen (attributes, perk draft, scratch-off).</summary>
        UpgradeScreen = 1,
        /// <summary>After the run has ended: the summary.</summary>
        RunSummary = 2,
    }

    /// <summary>
    /// Which ante the run is in, its timer, and the moves between antes. Plain C#, no Unity types: a
    /// MonoBehaviour will call <see cref="Tick"/> once a frame in Milestone 14.
    ///
    /// How an ante ends is configurable (<see cref="AnteEndCondition"/>) so the other modes can be tried in playtests; Matthew chose Hybrid on 14 Nov 2026.
    /// </summary>
    public sealed class AnteDirector
    {
        readonly AnteBalance _balance;
        readonly RunLedger _ledger;

        public AnteDirector(AnteBalance balance, RunLedger ledger)
        {
            _balance = balance;
            _ledger = ledger;
            Ante = 1;
            Phase = AntePhase.Running;
            SecondsLeft = balance.anteSeconds;
        }

        /// <summary>1 for the first ante.</summary>
        public int Ante { get; private set; }
        public AntePhase Phase { get; private set; }
        public float SecondsLeft { get; private set; }
        /// <summary>Seconds added by stopwatches this ante.</summary>
        public float StopwatchSecondsAdded { get; private set; }
        /// <summary>Play time over the whole run, not counting pauses or time between antes.</summary>
        public float RunSeconds { get; private set; }
        /// <summary>The free perk pick earned by clearing the last ante has not been taken yet.</summary>
        public bool PerkDraftPending { get; private set; }
        /// <summary>
        /// The perk pick of this ante was already taken mid-ante (21 Nov 2026, Matthew: apply rewarded upgrades from a
        /// Select menu during the game). Meeting the target earns the pick at once; the break then has none left.
        /// </summary>
        public bool PerkTakenEarly { get; private set; }
        /// <summary>The pick can be taken now, in the field, because this ante's target is met.</summary>
        public bool PerkEarnedEarly => IsRunning && TargetMet && !PerkTakenEarly && !IsLastAnte;
        /// <summary>A perk can be picked now: at the break, or in the field once the target is met.</summary>
        public bool CanPickPerk => PerkDraftPending || PerkEarnedEarly;
        /// <summary>The free scratch-off earned by clearing the last ante has not been used yet.</summary>
        public bool ScratchPending { get; private set; }

        public int Target => Endless ? _balance.EndlessTargetFor(Ante) : _balance.TargetFor(Ante);
        /// <summary>The player chose to keep going after clearing the last ante: antes go on, each asking more, until they die.</summary>
        public bool Endless { get; private set; }
        public bool TargetMet => _ledger.AnteEarned >= Target;
        public bool IsRunning => Phase == AntePhase.Running;
        public bool IsLastAnte => !Endless && Ante >= _balance.AnteCount;
        /// <summary>The timer has run out but the ante is still open: waiting for the player to move on (OnPlayerAdvance), or in overtime.</summary>
        public bool WaitingForPlayer => IsRunning && SecondsLeft <= 0f;
        /// <summary>
        /// The clock ran out short of the target and the ante goes on until it is met (Matthew, 21 Nov 2026).
        /// Nothing else changes in the rules here: the ante loop makes the enemies hunt; the break that follows is the usual one.
        /// </summary>
        public bool InOvertime { get; private set; }
        /// <summary>Seconds spent in overtime this ante.</summary>
        public float OvertimeSeconds { get; private set; }
        /// <summary>The ante just cleared was cleared in overtime (said on the break page).</summary>
        public bool ClearedInOvertime { get; private set; }

        /// <summary>Raised after the phase or the ante changes.</summary>
        public event Action Changed;

        /// <param name="paused">True while the game is paused: the timer does not move.</param>
        public void Tick(float deltaSeconds, bool paused)
        {
            if (paused || !IsRunning || deltaSeconds <= 0f) return;
            if (InOvertime)
            {
                RunSeconds += deltaSeconds;
                OvertimeSeconds += deltaSeconds;
                if (TargetMet) Clear();
                return;
            }
            if (SecondsLeft <= 0f)
            {
                RunSeconds += deltaSeconds;     // already waiting for the player; play time still counts
                return;
            }

            float used = Math.Min(deltaSeconds, SecondsLeft);
            SecondsLeft -= used;
            RunSeconds += used;
            if (SecondsLeft > 0f) return;

            SecondsLeft = 0f;
            if (!TargetMet && _balance.missedTarget == MissedTarget.Overtime)
            {
                InOvertime = true;
                float over = deltaSeconds - used;
                RunSeconds += over;
                OvertimeSeconds += over;
                Changed?.Invoke();
            }
            else if (!TargetMet) End(AntePhase.Failed);
            else if (_balance.endCondition != AnteEndCondition.OnPlayerAdvance) Clear();
            else
            {
                // Target met: the ante stays open until the player advances, and the rest of this tick is still play time.
                RunSeconds += deltaSeconds - used;
                Changed?.Invoke();
            }
        }

        /// <summary>True when the player is allowed to end the ante now by choice.</summary>
        public bool CanAdvance => IsRunning && TargetMet && _balance.endCondition != AnteEndCondition.OnTimer;

        /// <summary>
        /// The player has chosen, and confirmed, to end the ante now. This is the only player action that ends an
        /// ante. It is not what Select does: Select only opens a screen (<see cref="ViewForSelect"/>).
        /// </summary>
        public bool ConfirmAdvance()
        {
            if (!CanAdvance) return false;
            Clear();
            return true;
        }

        public SelectView ViewForSelect()
        {
            if (Phase == AntePhase.Running) return SelectView.RunStatus;
            return Phase == AntePhase.Intermission ? SelectView.UpgradeScreen : SelectView.RunSummary;
        }

        /// <summary>Adds stopwatch time, up to the limit per ante. Returns false (and uses nothing up) if it would go over.</summary>
        public bool UseStopwatch()
        {
            if (!IsRunning || WaitingForPlayer) return false;
            if (StopwatchSecondsAdded + _balance.stopwatchSeconds > _balance.stopwatchMaxPerAnte + 0.0001f) return false;
            StopwatchSecondsAdded += _balance.stopwatchSeconds;
            SecondsLeft += _balance.stopwatchSeconds;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Puts time back on the running ante (a perk's reward) or takes it away (the Short Fuse hazard; 20 Nov 2026).
        /// Never leaves less than one second, and does nothing outside a running ante.
        /// </summary>
        public void AdjustSeconds(float delta)
        {
            if (!IsRunning || WaitingForPlayer || delta == 0f || float.IsNaN(delta)) return;
            SecondsLeft = Math.Max(1f, SecondsLeft + delta);
            Changed?.Invoke();
        }

        public void PlayerDied()
        {
            InOvertime = false;
            if (IsRunning) End(AntePhase.Failed);
        }

        /// <summary>Leaves the upgrade screen and starts the next ante. Unused free picks are given up.</summary>
        public bool BeginNextAnte()
        {
            if (Phase != AntePhase.Intermission) return false;
            Ante++;
            _ledger.StartNewAnte();
            SecondsLeft = _balance.anteSeconds;
            StopwatchSecondsAdded = 0f;
            PerkDraftPending = false;
            PerkTakenEarly = false;
            ScratchPending = false;
            InOvertime = false;
            OvertimeSeconds = 0f;
            ClearedInOvertime = false;
            Phase = AntePhase.Running;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// From the victory page: keep going. The run gets the usual break (perk pick and ticket) and then antes past
        /// the last, each with a bigger target (<see cref="AnteBalance.EndlessTargetFor"/>), until the player dies.
        /// </summary>
        public bool ContinueEndless()
        {
            if (Phase != AntePhase.Victory) return false;
            Endless = true;
            PerkDraftPending = true;
            ScratchPending = true;
            End(AntePhase.Intermission);
            return true;
        }

        public void MarkPerkDraftTaken()
        {
            if (IsRunning && PerkEarnedEarly) PerkTakenEarly = true;
            PerkDraftPending = false;
            Changed?.Invoke();
        }
        public void MarkScratchUsed() => ScratchPending = false;

        void Clear()
        {
            ClearedInOvertime = InOvertime;
            InOvertime = false;
            if (IsLastAnte)
            {
                // The perk draft would have nothing left to affect. No scratch-off at victory unless the setting says so (Matthew chose none, 14 Nov 2026).
                PerkDraftPending = false;
                ScratchPending = _balance.finalScratch == FinalScratchTiming.AlsoAtVictory;
                End(AntePhase.Victory);
                return;
            }
            // Cleared in overtime still gives the perk pick (Matthew, 21 Nov 2026: "okay with perks continuing even in overtime"),
            // unless it was already taken in the field.
            PerkDraftPending = !PerkTakenEarly;
            ScratchPending = true;
            End(AntePhase.Intermission);
        }

        void End(AntePhase phase)
        {
            Phase = phase;
            Changed?.Invoke();
        }

        /// <summary>Keep going and overtime from a saved run. Call before <see cref="Restore"/> (endless antes go past the last).</summary>
        public void RestoreLater(bool endless, bool inOvertime, float overtimeSeconds, bool clearedInOvertime, bool perkTakenEarly = false)
        {
            PerkTakenEarly = perkTakenEarly;
            Endless = endless;
            InOvertime = inOvertime;
            OvertimeSeconds = Math.Max(0f, overtimeSeconds);
            ClearedInOvertime = clearedInOvertime;
        }

        public void Restore(int ante, AntePhase phase, float secondsLeft, float stopwatchAdded, float runSeconds, bool perkPending, bool scratchPending)
        {
            Ante = Endless ? Math.Max(1, ante) : Math.Max(1, Math.Min(Math.Max(1, _balance.AnteCount), ante));
            Phase = phase;
            SecondsLeft = Math.Max(0f, secondsLeft);
            StopwatchSecondsAdded = Math.Max(0f, stopwatchAdded);
            RunSeconds = Math.Max(0f, runSeconds);
            PerkDraftPending = perkPending;
            ScratchPending = scratchPending;
            Changed?.Invoke();
        }
    }
}
