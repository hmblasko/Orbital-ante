using System;
using System.Collections.Generic;
using System.Text;

namespace OrbitalAnte.Tasks
{
    /// <summary>
    /// When ante 1's clock may start (Matthew, 21 Nov 2026: "a fully functioning game with clear goals").
    /// A run starts strapped in on the launch pad; buckling in, the engine build-up and the climb should not eat
    /// the first ante. The clock waits until the ship is clear of the pad, but never longer than
    /// <see cref="MaxHoldSeconds"/> of play, and never once anything has scored (so the hold can't be farmed:
    /// fighting round the pad starts the clock at the first point). Stepping outside starts it too.
    /// Plain C#, no Unity types.
    /// </summary>
    public sealed class RunStartHold
    {
        /// <summary>Metres from the ship's feet to the ground that count as "off the pad".</summary>
        public const float LiftedAt = 25f;
        /// <summary>The longest the clock waits, in seconds of unpaused play.</summary>
        public const float MaxHoldSeconds = 60f;

        float _held;

        /// <summary>True while ante 1's clock is still waiting.</summary>
        public bool Holding { get; private set; } = true;

        /// <summary>Why the hold ended (for the note on the readout); empty while holding.</summary>
        public string Released { get; private set; } = string.Empty;

        public void Reset()
        {
            _held = 0f;
            Holding = true;
            Released = string.Empty;
        }

        /// <summary>
        /// One frame of play. <paramref name="heightAboveGround"/> is the ship's feet above the ground (a large number
        /// in space), <paramref name="outside"/> whether the player has left the ship, <paramref name="scored"/> whether
        /// any points have come in this ante. Paused frames should not be passed in.
        /// </summary>
        public void Step(float dt, float heightAboveGround, bool outside, bool scored)
        {
            if (!Holding) return;
            if (dt > 0f && !float.IsNaN(dt)) _held += dt;
            if (heightAboveGround >= LiftedAt) Release("LIFTOFF");
            else if (outside) Release("OUT ON FOOT");
            else if (scored) Release("FIRST POINTS");
            else if (_held >= MaxHoldSeconds) Release("TIME'S TICKING");
        }

        /// <summary>Starts the clock now (a run that does not begin on the pad).</summary>
        public void ReleaseNow(string why) { if (Holding) Release(why); }

        void Release(string why)
        {
            Holding = false;
            Released = why;
        }
    }

    /// <summary>The one-time tips a new player sees, in the order they are likely to need them.</summary>
    public enum GuideTip
    {
        None = 0,
        /// <summary>Shown as the run-start card on the first run: the goal in plain words.</summary>
        Goal = 1,
        /// <summary>Off the pad: open the map.</summary>
        OpenMap = 2,
        /// <summary>The map has been seen: lock a world and let the autopilot fly.</summary>
        LockAndFly = 3,
        /// <summary>Near a world: what scores there.</summary>
        WhatScores = 4,
        /// <summary>The first points have come in.</summary>
        FirstPoints = 5,
        /// <summary>The target is met: bank it or keep going.</summary>
        Bank = 6,
        /// <summary>The clock is low and the target is not met.</summary>
        HurryUp = 7,
    }

    /// <summary>What the guide can see this frame. Filled by <c>RunGuideFx</c>.</summary>
    public struct GuideState
    {
        public bool Lifted;
        /// <summary>Strapped into the pilot's seat (the map, lock and autopilot only work there).</summary>
        public bool AtTheControls;
        public bool MapOpenedThisRun;
        public bool NearAWorld;
        public bool WorldHasTasks;
        public bool Scored;
        public bool TargetMet;
        public float SecondsLeft;
        public bool Running;
    }

    /// <summary>
    /// Picks the next tip. Each tip is shown once per profile (the caller remembers which), at most one at a time,
    /// and never while the previous one is still on screen. Plain C#, no Unity types.
    /// </summary>
    public static class RunGuide
    {
        /// <summary>How long a tip stays up, in seconds.</summary>
        public const float TipSeconds = 7f;
        /// <summary>Quiet time after a tip before the next may show.</summary>
        public const float GapSeconds = 4f;
        /// <summary>The clock reading that brings up the hurry-up tip.</summary>
        public const float HurryAt = 60f;

        /// <summary>The tip that should show now, or <see cref="GuideTip.None"/>.</summary>
        public static GuideTip Next(GuideState s, ICollection<GuideTip> seen)
        {
            if (!s.Running) return GuideTip.None;
            // Most urgent first: a met target and a short clock are about this very moment.
            if (s.TargetMet && !Has(seen, GuideTip.Bank)) return GuideTip.Bank;
            if (!s.TargetMet && s.SecondsLeft > 0f && s.SecondsLeft <= HurryAt && !Has(seen, GuideTip.HurryUp)) return GuideTip.HurryUp;
            if (s.Scored && !s.TargetMet && !Has(seen, GuideTip.FirstPoints)) return GuideTip.FirstPoints;
            // The rest wait for liftoff: on the pad the goal card has just said what to do.
            if (!s.Lifted) return GuideTip.None;
            if (s.AtTheControls && !s.MapOpenedThisRun && !Has(seen, GuideTip.OpenMap)) return GuideTip.OpenMap;
            if (s.AtTheControls && s.MapOpenedThisRun && !Has(seen, GuideTip.LockAndFly)) return GuideTip.LockAndFly;
            if (s.NearAWorld && s.WorldHasTasks && !Has(seen, GuideTip.WhatScores)) return GuideTip.WhatScores;
            return GuideTip.None;
        }

        static bool Has(ICollection<GuideTip> seen, GuideTip tip) => seen != null && seen.Contains(tip);

        /// <summary>Key names the tips need, for whichever device is in use.</summary>
        public struct Keys
        {
            public string Map, Lock, Autopilot, Pause, LiftOff, Interact;
        }

        /// <summary>The words of a tip. Keys are filled in by the caller so the rule stays free of input code.</summary>
        public static string Text(GuideTip tip, Keys k, int target)
        {
            switch (tip)
            {
                case GuideTip.OpenMap:
                    return Key(k.Map) + ": open the map. Pick a world to fly to: every world has points to earn.";
                case GuideTip.LockAndFly:
                    return "Point the nose at a world and press " + Key(k.Lock) + " to lock it, then " + Key(k.Autopilot) + " for the autopilot.";
                case GuideTip.WhatScores:
                    return "Points here: enemy bases, signal masts and salvage crates. Stylish kills score extra.";
                case GuideTip.FirstPoints:
                    return "First points! Score " + N(target) + " before the clock runs out to clear the ante.";
                case GuideTip.Bank:
                    return "Target met! The ante clears when the clock runs out. Keep scoring, or end it now from the pause menu (" + Key(k.Pause) + ").";
                case GuideTip.HurryUp:
                    return "One minute left on the clock. Go for the nearest base, mast or crate.";
                default:
                    return string.Empty;
            }
        }

        /// <summary>The run-start card's body (first run, and "How to play" on the title screen).</summary>
        public static string GoalCard(int target, int anteSeconds, int antes, Keys k)
        {
            var s = new StringBuilder();
            s.Append("Ante 1: score ").Append(N(target)).Append(" points in ");
            int minutes = anteSeconds / 60, seconds = anteSeconds % 60;
            if (minutes > 0) s.Append(minutes).Append(minutes == 1 ? " minute" : " minutes");
            if (minutes > 0 && seconds > 0) s.Append(" ");
            if (minutes == 0 || seconds > 0) s.Append(seconds).Append(seconds == 1 ? " second" : " seconds");
            s.Append(". The target rises each ante.\n");
            s.Append("Points come from enemy bases, signal masts, salvage crates and stylish kills, on any world.\n");
            s.Append("Between antes, pick perks and spend points to get stronger. Clear ").Append(antes).Append(" antes to win.\n");
            s.Append("Miss the clock and it's overtime: enemies hunt you until you make the target.\n");
            s.Append("Your guns upgrade as you kill. The run ends only when you die.\n\n");
            s.Append("Hold ").Append(Key(k.LiftOff)).Append(" to lift off. The clock starts at liftoff, on stepping outside, ")
             .Append("on your first points, or after ").Append((int)RunStartHold.MaxHoldSeconds).Append(" seconds on the pad.");
            return s.ToString();
        }

        static string Key(string name) => string.IsNullOrEmpty(name) ? "?" : name;

        static string N(int value) => value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Every tip there is, for "has the player seen them all".</summary>
        public static readonly GuideTip[] All =
        {
            GuideTip.Goal, GuideTip.OpenMap, GuideTip.LockAndFly, GuideTip.WhatScores, GuideTip.FirstPoints, GuideTip.Bank, GuideTip.HurryUp,
        };

        /// <summary>Turns the stored list ("2,5") back into tips. Unknown numbers are dropped.</summary>
        public static HashSet<GuideTip> ParseSeen(string stored)
        {
            var seen = new HashSet<GuideTip>();
            if (string.IsNullOrEmpty(stored)) return seen;
            foreach (string part in stored.Split(','))
            {
                if (int.TryParse(part, out int n) && Enum.IsDefined(typeof(GuideTip), n) && n != 0) seen.Add((GuideTip)n);
            }
            return seen;
        }

        public static string StoreSeen(IEnumerable<GuideTip> seen)
        {
            var list = new List<int>();
            foreach (GuideTip t in seen) if (t != GuideTip.None) list.Add((int)t);
            list.Sort();
            return string.Join(",", list);
        }
    }

    /// <summary>
    /// The line under the ante readout that says what is left to score on the world you are at
    /// ("MARS: 2 BASES · 1 MAST · 3 CRATES"), so the goal is never only in the map. Plain C#.
    /// </summary>
    public static class WorldTaskLine
    {
        /// <summary>The carried crate has a destination even if there are other tasks or the current world's counts are unknown.</summary>
        public static string Objective(string localTasks, bool carryingSalvage) =>
            carryingSalvage ? "SALVAGE: RETURN TO YOUR SHIP'S LIFT" : localTasks;

        public static string Compose(string world, int bases, int masts, int crates)
        {
            string name = string.IsNullOrEmpty(world) ? "HERE" : world.ToUpperInvariant();
            if (bases < 0 || masts < 0 || crates < 0) return string.Empty;
            if (bases + masts + crates == 0) return name + ": NO MARKED TASKS LEFT";
            var parts = new List<string>(3);
            if (bases > 0) parts.Add(bases + (bases == 1 ? " BASE" : " BASES"));
            if (masts > 0) parts.Add(masts + (masts == 1 ? " MAST" : " MASTS"));
            if (crates > 0) parts.Add(crates + (crates == 1 ? " CRATE" : " CRATES"));
            return name + ": " + string.Join(" · ", parts);
        }
    }
}
