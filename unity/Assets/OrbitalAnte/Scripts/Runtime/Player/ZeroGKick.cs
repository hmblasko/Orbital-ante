using System;

namespace OrbitalAnte.Player
{
    /// <summary>
    /// A gun pushes the shooter back while floating free (24 Nov 2026 refinement; Part B section 4: "real space
    /// inertia throughout"; Matthew, Milestone 11: zero-G should feel "free, precise and a little scary"). A
    /// melee hit already does this (<c>MeleeRules.PushBack</c>); a gun did not, so firing in zero-G felt like
    /// standing on a floor. The push is scaled from the gun's own kick, so a sniper shot is a shove and a burst
    /// from the SMG a steady drift, and the jetpack's Match Velocity takes it off like any other drift.
    ///
    /// Only while floating free (no pull to stand against): on the ground and aboard nothing changes. Never a
    /// turn of the view, only the suit's speed. Plain C#, no engine.
    /// </summary>
    [Serializable]
    public sealed class ZeroGKickRules
    {
        /// <summary>Untick to float as still as before while shooting.</summary>
        public bool enabled = true;
        /// <summary>Metres per second of push for each degree of the gun's upward kick.</summary>
        public float perKickDegree = 0.08f;
        /// <summary>The most one shot can push, metres per second.</summary>
        public float mostPerShot = 0.4f;
        /// <summary>
        /// Shooting never adds speed past this (metres per second, measured along the push): a held trigger
        /// settles into a drift rather than turning the gun into a jetpack.
        /// </summary>
        public float mostDrift = 3f;

        public static ZeroGKickRules Default => new ZeroGKickRules();
    }

    public static class ZeroGKick
    {
        /// <summary>The push of one shot, metres per second, before the drift cap.</summary>
        /// <param name="recoilPitch">The gun's upward kick in degrees (<c>WeaponData.recoilPitch</c>).</param>
        /// <param name="recoilScale">The shot's kick against the gun's usual (one hand, perks), 1 normally.</param>
        public static float ShotPush(ZeroGKickRules rules, float recoilPitch, float recoilScale)
        {
            if (rules == null || !rules.enabled) return 0f;
            if (!(recoilPitch > 0f) || !(recoilScale > 0f)) return 0f;
            float push = rules.perKickDegree * recoilPitch * recoilScale;
            return Math.Max(0f, Math.Min(rules.mostPerShot, push));
        }

        /// <summary>
        /// How much of a shot's push to actually give, metres per second: all of it until the shooter is already
        /// drifting backward (along the push) at <see cref="ZeroGKickRules.mostDrift"/>, then only what reaches it.
        /// </summary>
        /// <param name="alongPush">The shooter's speed along the push's direction now (positive is already backward).</param>
        public static float Applied(ZeroGKickRules rules, float push, float alongPush)
        {
            if (rules == null || !(push > 0f)) return 0f;
            float room = rules.mostDrift - alongPush;
            if (!(room > 0f)) return 0f;
            return Math.Min(push, room);
        }
    }
}
