namespace OrbitalAnte.Core
{
    /// <summary>
    /// How the things that change game speed are put together into one number. Plain C# so it can be tested
    /// with no engine. <see cref="TimeFlow"/> is the only code that uses it on the real clock.
    ///
    /// The order, strongest first:
    /// 1. Paused: time stands still, whatever else is going on.
    /// 2. Hitstop: a near-freeze for a few hundredths of a second. It replaces the slows below, it is not
    ///    multiplied by them, so a pop feels the same in and out of a deep breath.
    /// 3. Deep Breath times the headshot slow-mo: two smooth slows that stack.
    /// </summary>
    public static class TimeBlend
    {
        /// <summary>The slowest the smooth slows may take the physics step. Below this the step stays put.</summary>
        public const float LeastStepShare = 0.1f;

        /// <summary>The time scale to use. Each input is a speed where 1 means "leave time alone".</summary>
        public static float Scale(bool paused, float hitstop, float headshot, float breath)
        {
            if (paused) return 0f;
            hitstop = Unit(hitstop);
            if (hitstop < 1f) return hitstop;
            return Unit(headshot) * Unit(breath);
        }

        /// <summary>
        /// The physics step to use. It shrinks with the smooth slows so that things still move every frame
        /// (a fixed step in slowed time would make them move in jerks), and is exactly
        /// <paramref name="normalStep"/> again when nothing is slowing. Hitstop and pause leave it alone: they
        /// are freezes, there is nothing to keep smooth.
        /// </summary>
        public static float FixedStep(float normalStep, float headshot, float breath)
        {
            float slow = Unit(headshot) * Unit(breath);
            if (slow >= 1f) return normalStep;
            if (slow < LeastStepShare) slow = LeastStepShare;
            return normalStep * slow;
        }

        static float Unit(float v) => float.IsNaN(v) ? 1f : v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
