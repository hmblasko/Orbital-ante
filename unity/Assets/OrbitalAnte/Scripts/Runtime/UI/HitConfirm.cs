using System;

namespace OrbitalAnte.UI
{
    /// <summary>What a hit on an enemy was, from least to most, so the marker and the ding can say it.</summary>
    public enum HitKind
    {
        /// <summary>Nothing to confirm: a miss, a rock, the ship, a loose limb, a gun lying on the ground.</summary>
        None,
        Body,
        /// <summary>A limb came off a living enemy.</summary>
        Limb,
        /// <summary>A gun was knocked out of an enemy's hands.</summary>
        Disarm,
        /// <summary>A hit on the head that did not kill yet (a head takes a few rifle hits).</summary>
        Head,
        Kill,
        /// <summary>Killed by destroying a head still on its body.</summary>
        HeadshotKill,
    }

    /// <summary>The colour family of a marker. The drawing turns these into the HUD's colours.</summary>
    public enum MarkerTone { White, Gold, Cyan, Red }

    /// <summary>
    /// How a hit on an enemy is confirmed (Matthew, 16 Nov 2026: visual hit markers "just like" a tactical shooter
    /// and a "super satisfying dinging noise (not annoying)"). Plain C#, no Unity types.
    ///
    /// The marker is a small X of four ticks round the crosshair that pops out a little and settles in a few
    /// hundredths of a second, holds, and fades. Its size and colour say what the hit was: white for the body,
    /// gold for the head, cyan for a disarm, red for a kill, and a second gold X outside the red for a headshot
    /// kill. The ding is our own small bell (tools/audio, `hit.marker`), a fifth higher for the head and a fourth
    /// higher for a disarm; kills have their own two- and three-bell chimes. Only hits on living enemies count,
    /// so shooting rocks, the ship or loose limbs never rings.
    /// </summary>
    public static class HitConfirm
    {
        /// <summary>Seconds the X takes to settle from its popped-out size.</summary>
        public const float PopSeconds = 0.06f;
        /// <summary>Dings closer together than this are played quieter, so automatic fire never nags.</summary>
        public const float QuietGap = 0.25f;
        /// <summary>Loudness of a ding that follows the last one at once (1 = full).</summary>
        public const float QuietestDing = 0.6f;

        /// <summary>Pitch of the hit bell for a head (a fifth up: D6 to A6), in tune with the music.</summary>
        public const float HeadPitch = 1.4983f;
        /// <summary>Pitch of the hit bell for a disarm (a fourth up: D6 to G6).</summary>
        public const float DisarmPitch = 1.3348f;

        public const string BodyDing = "hit.marker";
        public const string KillDing = "hit.kill";
        public const string HeadshotDing = "hit.headshot";

        /// <summary>
        /// What a hit was, from the flags a hit reports. <paramref name="enemy"/> means the living body of an
        /// enemy was hit (not a corpse or a loose piece); a disarm comes from hitting a gun in someone's hands.
        /// </summary>
        public static HitKind Classify(bool hit, bool enemy, bool head, bool severed, bool disarmed, bool killed, bool headshotKill)
        {
            if (!hit) return HitKind.None;
            if (killed) return headshotKill ? HitKind.HeadshotKill : HitKind.Kill;
            if (disarmed) return HitKind.Disarm;
            if (!enemy) return HitKind.None;
            if (head) return HitKind.Head;
            return severed ? HitKind.Limb : HitKind.Body;
        }

        /// <summary>How much a kind outranks another: a lesser hit never cuts a bigger one's marker short.</summary>
        public static int Rank(HitKind kind)
        {
            switch (kind)
            {
                case HitKind.Body: return 1;
                case HitKind.Limb:
                case HitKind.Disarm: return 2;
                case HitKind.Head: return 3;
                case HitKind.Kill: return 4;
                case HitKind.HeadshotKill: return 5;
                default: return 0;
            }
        }

        /// <summary>Seconds a marker is up in all, hold and fade.</summary>
        public static float Seconds(HitKind kind)
        {
            switch (kind)
            {
                case HitKind.Body: return 0.22f;
                case HitKind.Head: return 0.28f;
                case HitKind.Limb:
                case HitKind.Disarm: return 0.32f;
                case HitKind.Kill: return 0.5f;
                case HitKind.HeadshotKill: return 0.65f;
                default: return 0f;
            }
        }

        /// <summary>Seconds a marker stays at full strength before it fades: about half its time.</summary>
        public static float Hold(HitKind kind) => Seconds(kind) * 0.5f;

        /// <summary>Length of each tick of the X, in reference pixels (1280 x 800).</summary>
        public static float TickLength(HitKind kind)
        {
            switch (kind)
            {
                case HitKind.Body: return 10f;
                case HitKind.Head: return 11f;
                case HitKind.Limb:
                case HitKind.Disarm: return 13f;
                case HitKind.Kill:
                case HitKind.HeadshotKill: return 15f;
                default: return 0f;
            }
        }

        /// <summary>Distance from the middle of the screen to the inner end of each tick, reference pixels.</summary>
        public static float TickGap(HitKind kind) => kind >= HitKind.Kill ? 9f : kind == HitKind.Body ? 7f : 8f;

        /// <summary>Thickness of each tick, reference pixels.</summary>
        public static float TickWidth(HitKind kind) => kind >= HitKind.Kill ? 3.5f : kind == HitKind.Body ? 2.5f : 3f;

        public static MarkerTone Tone(HitKind kind)
        {
            switch (kind)
            {
                case HitKind.Head: return MarkerTone.Gold;
                case HitKind.Disarm: return MarkerTone.Cyan;
                case HitKind.Kill:
                case HitKind.HeadshotKill: return MarkerTone.Red;
                default: return MarkerTone.White;
            }
        }

        /// <summary>True when a headshot kill's second, outer X is drawn (in gold, outside the red one).</summary>
        public static bool OuterRing(HitKind kind) => kind == HitKind.HeadshotKill;

        /// <summary>
        /// True if a new hit of <paramref name="next"/> should take over from the marker showing
        /// <paramref name="current"/>, which is <paramref name="age"/> seconds old. An equal or bigger hit always
        /// does (and starts it again, so a burst keeps the X up); a smaller one waits until the bigger one starts
        /// to fade.
        /// </summary>
        public static bool Replaces(HitKind current, float age, HitKind next)
        {
            if (next == HitKind.None) return false;
            if (current == HitKind.None || !(age >= 0f) || age >= Seconds(current)) return true;
            if (Rank(next) >= Rank(current)) return true;
            return age >= Hold(current);
        }

        /// <summary>How visible a marker is, 1 to 0, by its age: full while it holds, then fading quickly.</summary>
        public static float Alpha(HitKind kind, float age)
        {
            float total = Seconds(kind);
            if (!(age >= 0f) || age >= total) return 0f;
            float hold = Hold(kind);
            if (age <= hold) return 1f;
            float t = 1f - (age - hold) / (total - hold);
            return t * t;
        }

        /// <summary>
        /// Size of the X against its resting size: it starts popped out and settles in <see cref="PopSeconds"/>
        /// (eased, so it snaps rather than slides). Kills pop out further.
        /// </summary>
        public static float Scale(HitKind kind, float age)
        {
            float pop = kind >= HitKind.Kill ? 1.5f : 1.3f;
            if (!(age >= 0f)) return pop;
            if (age >= PopSeconds) return 1f;
            float t = 1f - age / PopSeconds;
            return 1f + (pop - 1f) * t * t;
        }

        /// <summary>The sound event for a kind, or null for none.</summary>
        public static string Ding(HitKind kind)
        {
            switch (kind)
            {
                case HitKind.None: return null;
                case HitKind.Kill: return KillDing;
                case HitKind.HeadshotKill: return HeadshotDing;
                default: return BodyDing;
            }
        }

        /// <summary>Pitch the ding is played at: the head a fifth up, a disarm a fourth up, everything else as made.</summary>
        public static float DingPitch(HitKind kind)
        {
            switch (kind)
            {
                case HitKind.Head: return HeadPitch;
                case HitKind.Disarm: return DisarmPitch;
                default: return 1f;
            }
        }

        /// <summary>
        /// Loudness of a ding (0 to 1) given the seconds since the last one: full when hits are spaced out,
        /// down to <see cref="QuietestDing"/> in a burst. Kills are always full, so they never get lost.
        /// </summary>
        public static float DingVolume(HitKind kind, float sinceLast)
        {
            if (kind >= HitKind.Kill) return 1f;
            if (!(sinceLast < QuietGap)) return 1f;
            float t = Math.Max(0f, sinceLast) / QuietGap;
            return QuietestDing + (1f - QuietestDing) * t;
        }
    }
}
