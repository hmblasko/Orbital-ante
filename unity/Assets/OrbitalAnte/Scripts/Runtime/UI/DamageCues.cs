using System;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// What the visor shows when the player is hurt (Matthew, 15 Nov 2026: "The player should have some
    /// indication of when they take damage"). Plain C#, no Unity types.
    ///
    /// Three cues, all pointing the same way so they agree:
    /// - a red arc round the crosshair on the side the shot came from (seen from above, like a radar: top is
    ///   in front of you, bottom behind, left and right as they are), fading over about a second;
    /// - a red glow along the matching edge of the view, brighter for a heavier hit;
    /// - cracks in the visor glass, one for each suit plate lost, which close up again as the suit recovers,
    ///   so the glass never says the suit is worse (or better) than the plate gauge does.
    /// The hurt sound is panned toward the shot.
    /// </summary>
    public static class DamageCues
    {
        /// <summary>Seconds a direction marker stays up.</summary>
        public const float MarkerSeconds = 1.1f;
        /// <summary>Seconds the edge glow takes to fade.</summary>
        public const float GlowSeconds = 0.6f;
        /// <summary>Most direction markers on screen at once; a new hit takes the oldest's place.</summary>
        public const int MaxMarkers = 4;
        /// <summary>Most cracks the visor shows.</summary>
        public const int MaxCracks = 6;
        /// <summary>The hurt sound plays at most this often, so a burst of fire is not a burst of grunts.</summary>
        public const float SoundGap = 0.18f;
        /// <summary>Damage that gives a full-strength edge glow (a heavy single shot).</summary>
        public const float FullGlowDamage = 25f;

        /// <summary>
        /// Below this share of a direction lying along the ground (seen from the eye), the marker points up or down
        /// the screen; above <see cref="CompassShare"/> it points round the compass; between, it turns smoothly from one
        /// to the other, so an arc that follows something passing overhead never jumps (Ralph loop pass 2).
        /// </summary>
        public const float PoleShare = 0.15f, CompassShare = 0.35f;

        /// <summary>
        /// The screen angle of the marker, degrees anticlockwise from the right (0 right, 90 top = in front, 180
        /// left, 270 bottom = behind). Arguments are the direction toward the shooter, measured along the view's
        /// right, up and forward axes. A shot from straight above or below, where in front or behind means
        /// nothing, points up or down instead.
        /// </summary>
        public static float MarkerAngle(float right, float up, float forward)
        {
            float flat = (float)Math.Sqrt(right * right + forward * forward);
            float length = (float)Math.Sqrt(flat * flat + up * up);
            if (!(length > 1e-6f)) return 270f;
            float share = flat / length;
            float compass = Degrees(Math.Atan2(forward, right));
            if (share >= CompassShare) return compass;
            float pole = Degrees(Math.Atan2(up, right));
            if (share <= PoleShare) return pole;
            float k = (share - PoleShare) / (CompassShare - PoleShare);
            k = k * k * (3f - 2f * k);
            // The shorter way round from one to the other.
            float d = (compass - pole) % 360f;
            if (d > 180f) d -= 360f;
            else if (d <= -180f) d += 360f;
            return Wrap(pole + d * k);
        }

        static float Degrees(double radians) => Wrap((float)(radians * 180.0 / Math.PI));

        static float Wrap(float a)
        {
            a %= 360f;
            if (a < 0f) a += 360f;
            return a >= 360f ? a - 360f : a;
        }

        /// <summary>How visible a marker is, 1 to 0, by its age in seconds: full for a moment, then fading.</summary>
        public static float MarkerAlpha(float age)
        {
            if (!(age >= 0f) || age >= MarkerSeconds) return 0f;
            float hold = MarkerSeconds * 0.3f;
            if (age <= hold) return 1f;
            float t = (age - hold) / (MarkerSeconds - hold);
            return 1f - t * t;
        }

        /// <summary>Strength of the edge glow for a hit of <paramref name="damage"/>, 0.35 to 1.</summary>
        public static float GlowStrength(float damage)
        {
            if (!(damage > 0f)) return 0f;
            float s = 0.35f + 0.65f * Math.Min(1f, damage / FullGlowDamage);
            return s > 1f ? 1f : s;
        }

        /// <summary>The edge glow left after <paramref name="age"/> seconds.</summary>
        public static float GlowFade(float strength, float age)
        {
            if (!(age >= 0f) || age >= GlowSeconds) return 0f;
            float t = 1f - age / GlowSeconds;
            return strength * t * t;
        }

        /// <summary>
        /// How much of the glow goes on each edge for a marker angle: right, top, left, bottom (in that order),
        /// each 0 to 1. A shot straight from one side lights that edge alone; one from a diagonal shares it.
        /// </summary>
        public static void EdgeShares(float angle, float[] shares)
        {
            double a = angle * Math.PI / 180.0;
            float x = (float)Math.Cos(a), y = (float)Math.Sin(a);
            shares[0] = x > 0f ? x : 0f;
            shares[1] = y > 0f ? y : 0f;
            shares[2] = x < 0f ? -x : 0f;
            shares[3] = y < 0f ? -y : 0f;
        }

        /// <summary>
        /// Cracks in the visor for a share of the suit left: one per plate lost, up to <see cref="MaxCracks"/>.
        /// Counts the same plates as the suit gauge.
        /// </summary>
        public static int Cracks(float fraction)
        {
            int lost = VisorReadout.Plates - VisorReadout.PlatesLeft(fraction);
            return lost < 0 ? 0 : lost > MaxCracks ? MaxCracks : lost;
        }

        /// <summary>Stereo pan for the hurt sound, -1 left to 1 right, from the marker angle. Kept off the extremes.</summary>
        public static float SoundPan(float angle)
        {
            float pan = (float)Math.Cos(angle * Math.PI / 180.0) * 0.6f;
            return Math.Abs(pan) < 1e-4f ? 0f : pan;
        }

        /// <summary>True if the hurt sound may play now, given when it last played (unscaled seconds).</summary>
        public static bool SoundAllowed(float now, float lastPlayed)
        {
            return now - lastPlayed >= SoundGap;
        }
    }
}
