using System;

namespace OrbitalAnte.Enemies
{
    /// <summary>
    /// Restless camps (24 Nov 2026 refinement; Matthew, 13 Nov 2026: "near-constant combat"). Camps sit still
    /// until the player walks within their wake range, so a player who lands between camps, or clears the
    /// ones near the ship, can walk for minutes with nobody to fight. When it has been quiet for a while, the
    /// nearest squad that is still standing comes looking: it is given the place the player was (the same as
    /// a flare's alarm, a place to go and look, never a live track, and up to <see cref="LullRules.placeSpread"/> off).
    /// A camp woken for a lull is left alone for <see cref="LullRules.wokenGrace"/> first, so it can be seen
    /// coming; base and cave guards keep their posts; nobody is called to a player driving past.
    ///
    /// A lull is still allowed, because a fight is better for the breath before it (constitution 8, varied
    /// intensity): the quiet has to last <see cref="quietSeconds"/>, a little less each ante. Hiding is
    /// respected: the clock stands still while the player is crouched, prone, sneaking or in a dark cave, so stealth stays a
    /// choice and is never undone by a patrol that knew where to go. Plain C#, no engine.
    /// </summary>
    [Serializable]
    public sealed class LullRules
    {
        /// <summary>Untick to leave camps waiting where they are, as before.</summary>
        public bool enabled = true;
        /// <summary>Seconds of quiet (no enemy aware of the player) before a squad is sent.</summary>
        public float quietSeconds = 35f;
        /// <summary>Each ante after the first takes this share off the quiet, down to <see cref="quietFloor"/>.</summary>
        public float quieterPerAnte = 0.06f;
        /// <summary>The shortest the quiet ever gets, seconds.</summary>
        public float quietFloor = 20f;
        /// <summary>
        /// How far (metres over the ground) a camp may be to wake for a lull, past the ordinary wake range. Kept
        /// under the sleep range, or a camp woken for it would be put back to sleep on the next think.
        /// </summary>
        public float wakeReach = 200f;
        /// <summary>
        /// Furthest a standing squad is called from, metres in a straight line. Kept well past <see cref="wakeReach"/>
        /// (members stand round their camp and roam, hills add height), or a squad woken for a lull could measure
        /// too far to call, and the next think would wake another camp.
        /// </summary>
        public float callReach = 240f;
        /// <summary>Radius round the called enemy that hears the call, so its own squad comes and nobody else.</summary>
        public float callRadius = 28f;
        /// <summary>
        /// Seconds a camp woken for a lull is left alone before it can be called: long enough to see it and decide
        /// (crouch and go round, or take it on), and for the whole squad to have stood up.
        /// </summary>
        public float wokenGrace = 12f;
        /// <summary>Within this many metres of the place it was sent, a called squad stops running and comes on carefully.</summary>
        public float walkInFrom = 50f;
        /// <summary>The place a squad is sent is up to this many metres off where the player was: a patrol drifts your way, it does not know.</summary>
        public float placeSpread = 15f;
    }

    /// <summary>What the lull clock asks of the spawner this think.</summary>
    public enum LullAction
    {
        /// <summary>Nothing: it is not quiet for long enough yet, or the clock is standing still.</summary>
        Wait = 0,
        /// <summary>Quiet long enough and nobody standing near: let the nearest sleeping camp wake from further off.</summary>
        WakeFurther = 1,
        /// <summary>Quiet long enough: call the nearest standing squad to where the player is.</summary>
        Call = 2,
    }

    /// <summary>
    /// The quiet clock. Fed once a think with what the spawner sees; answers what to do. Plain C#.
    /// </summary>
    public sealed class LullClock
    {
        /// <summary>Seconds it has been quiet, counting only time the clock was running.</summary>
        public float Quiet { get; private set; }

        /// <summary>How long the quiet must last at an ante.</summary>
        public static float QuietFor(LullRules rules, int ante)
        {
            if (rules == null) return float.MaxValue;
            float share = 1f - Math.Max(0f, rules.quieterPerAnte) * Math.Max(0, ante - 1);
            float seconds = rules.quietSeconds * Math.Max(0f, share);
            return Math.Max(Math.Min(rules.quietFloor, rules.quietSeconds), seconds);
        }

        public void Reset() => Quiet = 0f;

        /// <param name="dt">Seconds since the last tick (game time, so a pause stops it).</param>
        /// <param name="inTheOpen">The player is alive and out on a world, on foot or driving.</param>
        /// <param name="fighting">Any enemy is aware of the player (a shot that reaches one makes it aware).</param>
        /// <param name="hiding">Crouched, prone, sneaking or in a dark cave (<c>Astronaut.Hiding</c>).</param>
        /// <param name="anyoneStanding">An enemy is awake, fighting fit, not a base's or a cave's guard, and within call reach.</param>
        public LullAction Tick(LullRules rules, int ante, float dt, bool inTheOpen, bool fighting, bool hiding, bool anyoneStanding)
        {
            if (rules == null || !rules.enabled || !inTheOpen)
            {
                // Aboard, in the seat, dead or away from any world: nothing to break, and the next landing starts fresh.
                Quiet = 0f;
                return LullAction.Wait;
            }
            if (fighting)
            {
                Quiet = 0f;
                return LullAction.Wait;
            }
            // Hiding stands the clock still rather than resetting it: getting up again picks up where it was.
            if (hiding) return LullAction.Wait;
            if (dt > 0f && dt < 5f) Quiet += dt;
            float need = QuietFor(rules, ante);
            if (Quiet < need) return LullAction.Wait;
            if (!anyoneStanding)
            {
                // One camp wakes further off; it gets a grace before it can be called (and another wake asked for).
                Quiet = Math.Max(0f, need - Math.Max(0f, rules.wokenGrace));
                return LullAction.WakeFurther;
            }
            // Called: the squad is on its way, and the next call waits for a whole new quiet.
            Quiet = 0f;
            return LullAction.Call;
        }
    }
}
