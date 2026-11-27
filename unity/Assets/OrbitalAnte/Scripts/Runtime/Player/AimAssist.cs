using System;

namespace OrbitalAnte.Player
{
    /// <summary>
    /// The dials for controller aim assist (on <see cref="PlayerConfig"/>). Only the sticks are helped; a mouse
    /// never is.
    /// </summary>
    [Serializable]
    public struct AimAssistRules
    {
        /// <summary>Off here turns it off for everyone; the pause menu's Feel page lets each player turn it off too.</summary>
        public bool enabled;
        /// <summary>How far round a target's middle (head or chest) counts as "over it", in metres.</summary>
        public float reach;
        /// <summary>The band is never narrower than this, in degrees, however far off the target is.</summary>
        public float minDegrees;
        /// <summary>And never wider than this, in degrees, however close.</summary>
        public float maxDegrees;
        /// <summary>The share of the stick's turning speed left right on a target (1 = no help).</summary>
        public float slowest;
        /// <summary>No help past this distance, in metres (or the gun's own range if that is shorter).</summary>
        public float range;
        /// <summary>Seconds to ease in and out of the slowdown, so the stick never feels caught.</summary>
        public float ease;

        public static AimAssistRules Default => new AimAssistRules
        {
            enabled = true,
            reach = 0.55f,
            minDegrees = 1.5f,
            maxDegrees = 7f,
            slowest = 0.5f,
            range = 70f,
            ease = 0.06f,
        };

        /// <summary>The same dials with anything out of range put back in it (old assets, typing slips).</summary>
        public AimAssistRules Safe()
        {
            AimAssistRules r = this;
            r.reach = Finite(r.reach, 0.55f, 0.05f, 3f);
            r.minDegrees = Finite(r.minDegrees, 1.5f, 0.1f, 20f);
            r.maxDegrees = Math.Max(r.minDegrees, Finite(r.maxDegrees, 7f, 0.1f, 30f));
            r.slowest = Finite(r.slowest, 0.5f, 0.1f, 1f);
            r.range = Finite(r.range, 70f, 1f, 1000f);
            r.ease = Finite(r.ease, 0.06f, 0f, 1f);
            return r;
        }

        static float Finite(float value, float fallback, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
            return Math.Max(min, Math.Min(max, value));
        }
    }

    /// <summary>
    /// Controller aim assist in plain C#: while the crosshair passes over an enemy, the sticks turn the view a
    /// little slower, so a thumb can stop on a target as a mouse hand can. It never turns the view by itself and
    /// never pulls toward anything (no magnetism), so every hit is still the player's own aim (constitution 3).
    /// </summary>
    public sealed class AimAssist
    {
        float _scale = 1f;

        /// <summary>What the sticks' turning speed is multiplied by this frame (1 = no help).</summary>
        public float Scale => _scale;

        /// <summary>
        /// How much a target helps, 0 (not at all) to 1 (the crosshair is right on it). <paramref name="degreesOff"/>
        /// is the angle between the view and the target's middle; <paramref name="distance"/> is in metres.
        /// </summary>
        public static float Closeness(float degreesOff, float distance, float range, in AimAssistRules rules)
        {
            if (float.IsNaN(degreesOff) || float.IsNaN(distance) || !(distance > 0f)) return 0f;
            if (distance > Math.Min(range, rules.range)) return 0f;
            float band = Band(distance, rules);
            if (degreesOff >= band) return 0f;
            float t = 1f - Math.Max(0f, degreesOff) / band;
            return t * t * (3f - 2f * t);
        }

        /// <summary>How wide the band round a target is at this distance, in degrees: its reach as seen from here.</summary>
        public static float Band(float distance, in AimAssistRules rules)
        {
            double degrees = Math.Atan2(rules.reach, Math.Max(0.01f, distance)) * 180.0 / Math.PI;
            return (float)Math.Max(rules.minDegrees, Math.Min(rules.maxDegrees, degrees));
        }

        /// <summary>
        /// Moves the slowdown toward what the best target this frame asks for. <paramref name="closeness"/> is the
        /// best <see cref="Closeness"/> (0 when there is none, or when the stick is not what is turning the view).
        /// </summary>
        public float Step(float dt, float closeness, in AimAssistRules rules)
        {
            float c = float.IsNaN(closeness) ? 0f : Math.Max(0f, Math.Min(1f, closeness));
            float target = 1f - (1f - rules.slowest) * c;
            if (!(dt > 0f)) return _scale;
            if (rules.ease <= 0f) _scale = target;
            else _scale += (target - _scale) * (1f - (float)Math.Exp(-dt / rules.ease));
            return _scale;
        }

        /// <summary>No help at once (a change of view, a gun put away).</summary>
        public void Clear() => _scale = 1f;
    }
}
