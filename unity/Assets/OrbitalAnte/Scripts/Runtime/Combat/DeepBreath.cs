using System;

namespace OrbitalAnte.Combat
{
    /// <summary>
    /// The numbers behind Deep Breath. Shares are parts of a full meter, from 0 to 1. Seconds are real seconds
    /// (a watch on your wrist), not slowed game seconds.
    /// </summary>
    [Serializable]
    public sealed class DeepBreathRules
    {
        /// <summary>How long a full meter lasts when held without a break, in real seconds.</summary>
        public float seconds = 6f;
        /// <summary>How fast the world runs while the breath is held (1 is normal speed).</summary>
        public float slowTo = 0.35f;
        /// <summary>Real seconds for the world to slow down once the breath starts.</summary>
        public float easeIn = 0.12f;
        /// <summary>Real seconds for the world to come back up to speed once it stops.</summary>
        public float easeOut = 0.35f;
        /// <summary>The share of the meter needed before a breath can begin.</summary>
        public float leastToStart = 0.15f;
        /// <summary>The share of the meter given back for popping a limb off an enemy.</summary>
        public float perLimb = 0.10f;
        /// <summary>The share of the meter given back for a kill.</summary>
        public float perKill = 0.25f;
    }

    /// <summary>
    /// Deep Breath: the astronaut takes a deep breath and the world slows. Hold to use, let go to stop.
    ///
    /// The whole rule, so nothing is hidden from the player:
    /// - A run starts with a full meter. Holding the breath empties it in <see cref="DeepBreathRules.seconds"/>.
    /// - It never refills by waiting. Only fighting gives it back (a limb, a kill), so there is no clock to watch.
    /// - It cannot begin with less than <see cref="DeepBreathRules.leastToStart"/> in the meter, so a nearly
    ///   empty meter cannot be flickered on and off.
    ///
    /// Plain C# with no engine types, so it can be tested anywhere.
    /// </summary>
    public sealed class DeepBreath
    {
        public readonly DeepBreathRules Rules;

        /// <summary>How much breath is left, 0 to 1.</summary>
        public float Meter { get; private set; } = 1f;
        /// <summary>How slowed the world is right now, 0 (normal) to 1 (fully slowed), eased both ways.</summary>
        public float Amount { get; private set; }
        /// <summary>True while the breath is being held and the meter is draining.</summary>
        public bool Active { get; private set; }
        /// <summary>True when the button is held but there is too little in the meter to begin.</summary>
        public bool Refused { get; private set; }
        /// <summary>True when there is enough in the meter to begin a breath.</summary>
        public bool CanStart => Meter >= Rules.leastToStart;

        public DeepBreath(DeepBreathRules rules = null)
        {
            Rules = rules ?? new DeepBreathRules();
        }

        /// <summary>
        /// The speed the world should run at: exactly 1 when no breath is held, <see cref="DeepBreathRules.slowTo"/>
        /// at the bottom of one, and a smooth curve between.
        /// </summary>
        public float TimeScale
        {
            get
            {
                if (Amount <= 0f) return 1f;
                float to = Clamp(Rules.slowTo, 0.05f, 1f);
                if (Amount >= 1f) return to;
                // Smoothstep: no sudden start or stop to the slowing.
                float t = Amount * Amount * (3f - 2f * Amount);
                return 1f + (to - 1f) * t;
            }
        }

        /// <summary>Moves the rule on by a stretch of real time. <paramref name="held"/> is the button.</summary>
        public void Tick(float realDt, bool held)
        {
            if (realDt < 0f || float.IsNaN(realDt)) realDt = 0f;

            if (Active && !held) Active = false;
            else if (!Active && held && CanStart) Active = true;

            if (Active)
            {
                Meter -= realDt / Math.Max(0.01f, Rules.seconds);
                if (Meter <= 0f)
                {
                    // Out of breath. It cannot begin again until the least share has been earned back.
                    Meter = 0f;
                    Active = false;
                }
            }
            Refused = held && !Active;

            if (Active) Amount = Rules.easeIn > 0f ? Math.Min(1f, Amount + realDt / Rules.easeIn) : 1f;
            else Amount = Rules.easeOut > 0f ? Math.Max(0f, Amount - realDt / Rules.easeOut) : 0f;
        }

        /// <summary>Gives back a share of the meter (for a limb or a kill). Never goes over full.</summary>
        public void Earn(float share)
        {
            if (share <= 0f || float.IsNaN(share)) return;
            Meter = Math.Min(1f, Meter + share);
        }

        /// <summary>A new run: full meter, world at normal speed.</summary>
        public void Reset()
        {
            Meter = 1f;
            Amount = 0f;
            Active = false;
            Refused = false;
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
