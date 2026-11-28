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
    /// still follows the player (<see cref="WindupStage.Winding"/>) the arc is wide, steady and dimmer, and narrows as
    /// the lock comes nearer: a countdown, read from the creature's own brain (<see cref="IWindup.WindupProgress"/>), so
    /// moving early is seen to be too early. When it takes aim (<see cref="WindupStage.Aimed"/>) the arc snaps to half
    /// that width at full brightness, the same frame as its sound's mark: now a step aside is a miss. Through the strike
    /// it holds, and dims once the strike can no longer reach the player (<see cref="IWindup.Threatens"/>), so a dodge
    /// that worked is seen to have worked. It fades when the strike is over or cut short. Game time throughout, like the
    /// creature (Focus slows both).
    /// </summary>
    public static class WindupCues
    {
        /// <summary>Creatures shown at once: the nearest. A pack of biters takes turns, so more than this are seldom all warning.</summary>
        public const int MaxMarkers = 3;
        /// <summary>Degrees of arc as a warning begins: wider than the gunman's 30, a near animal is a big thing.</summary>
        public const float StartSpan = 44f;
        /// <summary>Degrees just before it takes aim: the arc narrows to this as the lock comes nearer.</summary>
        public const float LockingSpan = 30f;
        /// <summary>Degrees once it has taken aim: half what it was a moment before, so the snap is seen out of the corner of an eye.</summary>
        public const float AimedSpan = 15f;
        /// <summary>Seconds the arc takes to snap narrow when it takes aim: quick enough to read as one snap.</summary>
        public const float SnapSeconds = 0.08f;
        /// <summary>How bright while it winds up: steady, and dimmer than aimed, so the snap reads in brightness as well as shape.</summary>
        public const float WindingAlpha = 0.6f;
        /// <summary>How bright once it has taken aim, and through a strike that can still reach the player.</summary>
        public const float AimedAlpha = 1f;
        /// <summary>How bright through a strike that can no longer reach the player: the dodge worked.</summary>
        public const float MissedAlpha = 0.35f;
        /// <summary>
        /// Seconds the arc takes to dim from aimed to missed. It brightens at once (danger is never shown late), and dims
        /// over this, so a strike judged beat by beat does not flicker.
        /// </summary>
        public const float EaseSeconds = 0.15f;
        public const float FadeSeconds = 0.35f;
        /// <summary>
        /// A gunman's "seen" arc this near a creature's arc that has taken aim is dimmed while the creature is aimed or
        /// striking, so the two amber arcs never run together and hide the snap.
        /// </summary>
        public const float CrowdDegrees = 25f;
        /// <summary>What is left of a crowded "seen" arc's brightness.</summary>
        public const float CrowdedShare = 0.35f;

        /// <summary>
        /// The arc is turned only when where it points has moved this many degrees, so a still view does not touch the
        /// canvas every frame (0.25 degrees on a 150 pixel ring is under a pixel).
        /// </summary>
        public const float TurnStep = 0.25f;
        /// <summary>The arc's width is redrawn only when it has changed this many degrees (a quarter of a degree at each end).</summary>
        public const float SpanStep = 0.5f;

        /// <summary>
        /// How wide the arc is, in degrees: narrowing from <see cref="StartSpan"/> to <see cref="LockingSpan"/> as the
        /// warning's <paramref name="progress"/> goes from 0 to 1, then snapping to <see cref="AimedSpan"/> over
        /// <see cref="SnapSeconds"/> from the moment it takes aim (<paramref name="inStage"/> seconds into the stage).
        /// </summary>
        public static float Span(WindupStage stage, float progress, float inStage)
        {
            switch (stage)
            {
                case WindupStage.Winding:
                    float p = !(progress > 0f) ? 0f : progress >= 1f ? 1f : progress;
                    return StartSpan + (LockingSpan - StartSpan) * p;
                case WindupStage.Aimed:
                    float k = !(inStage > 0f) ? 0f : inStage >= SnapSeconds ? 1f : inStage / SnapSeconds;
                    return LockingSpan + (AimedSpan - LockingSpan) * k;
                default:
                    return AimedSpan;
            }
        }

        /// <summary>How bright the arc is heading for, 0 to 1: <paramref name="threatens"/> matters only while it strikes.</summary>
        public static float Alpha(WindupStage stage, bool threatens)
        {
            switch (stage)
            {
                case WindupStage.Winding: return WindingAlpha;
                case WindupStage.Aimed: return AimedAlpha;
                case WindupStage.Striking: return threatens ? AimedAlpha : MissedAlpha;
                default: return 0f;
            }
        }

        /// <summary>One frame of <paramref name="dt"/> seconds from the brightness shown toward <paramref name="target"/>: up at once, down over <see cref="EaseSeconds"/>.</summary>
        public static float Ease(float shown, float target, float dt)
        {
            if (float.IsNaN(shown) || shown <= target) return target;
            if (!(dt > 0f)) return shown;
            float down = shown - dt / EaseSeconds * (AimedAlpha - MissedAlpha);
            return down > target ? down : target;
        }

        /// <summary>An arc fading out <paramref name="since"/> seconds after its warning ended, from the brightness it had.</summary>
        public static float Fade(float from, float since)
        {
            if (!(since >= 0f)) return from;
            if (since >= FadeSeconds) return 0f;
            float t = 1f - since / FadeSeconds;
            return from * t * t;
        }

        /// <summary>
        /// True if a gunman's "seen" arc at <paramref name="seenAngle"/> should be dimmed for a creature's arc at
        /// <paramref name="creatureAngle"/> in this stage: only once the creature has taken aim, and only close by.
        /// </summary>
        public static bool Crowds(WindupStage creatureStage, float creatureAngle, float seenAngle) =>
            (creatureStage == WindupStage.Aimed || creatureStage == WindupStage.Striking)
            && Math.Abs(DeltaDegrees(creatureAngle, seenAngle)) < CrowdDegrees;

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
