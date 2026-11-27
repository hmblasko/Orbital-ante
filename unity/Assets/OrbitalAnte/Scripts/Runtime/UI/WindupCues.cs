using System;
using OrbitalAnte.Creatures;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// A creature's wind-up on the visor (Ralph loop pass 2, 27 Nov 2026). It goes on the amber ring the gunman's
    /// "you have been seen" arc uses (<see cref="ThreatCues"/>), because it means the same thing: danger from there,
    /// coming. The arc points at the creature for as long as the warning lasts, following it as it moves and as the
    /// player turns, so an animal winding up behind them is found by turning toward the arc.
    ///
    /// The shape says the stage, not only the brightness, so it reads without telling colours apart. While the creature
    /// still follows the player (<see cref="WindupStage.Winding"/>) the arc is wide and flickers: moving does not help
    /// yet. When it takes aim (<see cref="WindupStage.Aimed"/>) the arc snaps narrow and holds steady: now a step aside
    /// is a miss, and its sound has its mark at the same moment. It holds through the strike and fades when the strike
    /// is over or cut short. Game time throughout, like the creature (Focus slows both).
    /// </summary>
    public static class WindupCues
    {
        /// <summary>Creatures shown at once. A pack of biters takes turns, so more than this are never all winding up.</summary>
        public const int MaxMarkers = 3;
        /// <summary>Degrees of arc while it still follows the player: wider than the gunman's 30, a near animal is a big thing.</summary>
        public const float FollowSpan = 40f;
        /// <summary>Degrees of arc once it has taken aim: under half as wide, so the snap is seen out of the corner of an eye.</summary>
        public const float AimedSpan = 18f;
        /// <summary>Seconds the arc takes to narrow when it takes aim: quick enough to read as one snap.</summary>
        public const float SnapSeconds = 0.08f;
        /// <summary>How fast the winding arc flickers. Quicker than the gunman's 3 a second: this one is close.</summary>
        public const float FlickerHz = 6f;
        public const float FlickerLow = 0.5f, FlickerHigh = 0.85f;
        public const float FadeSeconds = 0.35f;

        /// <summary>
        /// The arc is turned only when where it points has moved this many degrees, so a still view does not touch the
        /// canvas every frame (0.25 degrees on a 150 pixel ring is under a pixel).
        /// </summary>
        public const float TurnStep = 0.25f;

        /// <summary>How bright the arc is, 0 to 1, <paramref name="inStage"/> seconds into a stage.</summary>
        public static float Alpha(WindupStage stage, float inStage)
        {
            switch (stage)
            {
                case WindupStage.Winding:
                    if (!(inStage >= 0f)) inStage = 0f;
                    float wave = 0.5f + 0.5f * (float)Math.Cos(inStage * 2.0 * Math.PI * FlickerHz);
                    return FlickerLow + (FlickerHigh - FlickerLow) * wave;
                case WindupStage.Aimed:
                case WindupStage.Striking:
                    return 1f;
                default:
                    return 0f;
            }
        }

        /// <summary>How wide the arc is, in degrees, <paramref name="inStage"/> seconds into a stage.</summary>
        public static float Span(WindupStage stage, float inStage)
        {
            switch (stage)
            {
                case WindupStage.Winding:
                    return FollowSpan;
                case WindupStage.Aimed:
                    float k = !(inStage > 0f) ? 0f : inStage >= SnapSeconds ? 1f : inStage / SnapSeconds;
                    return FollowSpan + (AimedSpan - FollowSpan) * k;
                default:
                    return AimedSpan;
            }
        }

        /// <summary>An arc fading out <paramref name="since"/> seconds after its warning ended, from the brightness it had.</summary>
        public static float Fade(float from, float since)
        {
            if (!(since >= 0f)) return from;
            if (since >= FadeSeconds) return 0f;
            float t = 1f - since / FadeSeconds;
            return from * t * t;
        }

        /// <summary>True if an arc showing at <paramref name="shown"/> degrees should be turned to <paramref name="now"/>.</summary>
        public static bool Turned(float shown, float now) => Math.Abs(DeltaDegrees(shown, now)) >= TurnStep;

        /// <summary>The shortest turn from one angle to another, -180 to 180 degrees.</summary>
        public static float DeltaDegrees(float from, float to)
        {
            float d = (to - from) % 360f;
            if (d > 180f) d -= 360f;
            else if (d <= -180f) d += 360f;
            return d;
        }
    }
}
