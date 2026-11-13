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
        /// <summary>The run is over: the timer ran out short of the target, or the player died.</summary>
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
    /// How an ante ends is configurable (<see cref="AnteEndCondition"/>) because Matthew has not decided it.
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
        /// <summary>The free scratch-off earned by clearing the last ante has not been used yet.</summary>
        public bool ScratchPending { get; private set; }

        public int Target => _balance.TargetFor(Ante);
        public bool TargetMet => _ledger.AnteEarned >= Target;
        public bool IsRunning => Phase == AntePhase.Running;
        public bool IsLastAnte => Ante >= _balance.AnteCount;
        /// <summary>The timer has run out but the ante is still open, waiting for the player to move on (OnPlayerAdvance only).</summary>
        public bool WaitingForPlayer => IsRunning && SecondsLeft <= 0f;

        /// <summary>Raised after the phase or the ante changes.</summary>
        public event Action Changed;

        /// <param name="paused">True while the game is paused: the timer does not move.</param>
        public void Tick(float deltaSeconds, bool paused)
        {
            if (paused || !IsRunning || deltaSeconds <= 0f) return;
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
            if (!TargetMet) End(AntePhase.Failed);
            else if (_balance.endCondition != AnteEndCondition.OnPlayerAdvance) Clear();
            else Changed?.Invoke();             // target met: stays open until the player advances
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

        public void PlayerDied()
        {
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
            ScratchPending = false;
            Phase = AntePhase.Running;
            Changed?.Invoke();
            return true;
        }

        public void MarkPerkDraftTaken() => PerkDraftPending = false;
        public void MarkScratchUsed() => ScratchPending = false;

        void Clear()
        {
            if (IsLastAnte)
            {
                // The perk draft would have nothing left to affect. The scratch-off at victory is an open question.
                PerkDraftPending = false;
                ScratchPending = _balance.finalScratch == FinalScratchTiming.AlsoAtVictory;
                End(AntePhase.Victory);
                return;
            }
            PerkDraftPending = true;
            ScratchPending = true;
            End(AntePhase.Intermission);
        }

        void End(AntePhase phase)
        {
            Phase = phase;
            Changed?.Invoke();
        }

        public void Restore(int ante, AntePhase phase, float secondsLeft, float stopwatchAdded, float runSeconds, bool perkPending, bool scratchPending)
        {
            Ante = Math.Max(1, Math.Min(Math.Max(1, _balance.AnteCount), ante));
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
