using System;

namespace OrbitalAnte.Navigation
{
    public enum PilotFate
    {
        Fine,
        /// <summary>Felt it: the view kicks.</summary>
        Jolted,
        /// <summary>Not strapped in: thrown out of the seat or off their feet, and dazed for a moment.</summary>
        Thrown,
        Killed,
    }

    [Serializable]
    public sealed class CrashRules
    {
        /// <summary>A knock under this many metres per second is not felt at all.</summary>
        public float feltSpeed = 1.5f;
        /// <summary>Not strapped in, a knock this hard throws you.</summary>
        public float throwSpeed = 9f;
        /// <summary>Not strapped in, a knock this hard kills you.</summary>
        public float fatalLoose = 24f;
        /// <summary>Strapped in, it would take this much, if the belt could fail at all (see <see cref="beltAlwaysSaves"/>).</summary>
        public float fatalBuckled = 85f;
        /// <summary>
        /// Matthew, 14 Nov 2026: a broken ship never ends the game; only the player dying does. So a pilot who is
        /// buckled in lives through any crash, however much of the ship it costs. Not buckled in, a bad enough
        /// crash still kills (the build prompt, section 7).
        /// </summary>
        public bool beltAlwaysSaves = true;
        /// <summary>Seconds a thrown pilot cannot act.</summary>
        public float dazedSeconds = 1.3f;
        /// <summary>The most the view kicks, in degrees, at <see cref="fullKickSpeed"/>.</summary>
        public float kickDegrees = 9f;
        public float fullKickSpeed = 30f;
        /// <summary>How quickly the view springs back. Bigger is stiffer.</summary>
        public float spring = 90f;
        /// <summary>How quickly the wobble dies. 1 is no wobble at all.</summary>
        public float damping = 0.55f;
        /// <summary>Knocks within this long of the first are parts of one blow (footpads rarely land together).</summary>
        public float sameKnockSeconds = 0.2f;
    }

    /// <summary>
    /// What a knock does to the person inside. Plain C#, no Unity types. The measure is how much the ship's
    /// own velocity changed in the blow (metres per second), which is what a body in the cabin feels, not how
    /// fast the ship was going: a long skid along the ground is survivable, a dead stop is not.
    /// </summary>
    public sealed class CrashModel
    {
        public readonly CrashRules Rules;
        float _since = 100f, _counted;
        // The view kick: a spring on two axes (pitch, roll), in degrees.
        float _pitch, _roll, _pitchSpeed, _rollSpeed;

        public CrashModel(CrashRules rules = null) { Rules = rules ?? new CrashRules(); }

        public float KickPitch => _pitch;
        public float KickRoll => _roll;
        /// <summary>The hardest knock of the present blow, for the cause shown if it killed you.</summary>
        public float LastKnock { get; private set; }

        public PilotFate Fate(float knock, bool buckled)
        {
            if (knock < Rules.feltSpeed) return PilotFate.Fine;
            if (buckled && Rules.beltAlwaysSaves) return PilotFate.Jolted;
            if (knock >= (buckled ? Rules.fatalBuckled : Rules.fatalLoose)) return PilotFate.Killed;
            if (!buckled && knock >= Rules.throwSpeed) return PilotFate.Thrown;
            return PilotFate.Jolted;
        }

        /// <summary>
        /// A knock arrives. Several touches of one blow are added together.
        /// </summary>
        /// <param name="knock">Change in the ship's velocity, metres per second.</param>
        /// <param name="fromRight">-1 to 1: which side it came from, in the ship's own directions.</param>
        /// <param name="fromFront">-1 to 1: from ahead (1) or behind (-1). From below counts as from ahead: the head nods.</param>
        public PilotFate Hit(float knock, bool buckled, float fromRight, float fromFront)
        {
            // Touches that arrive together are one blow, and what the body feels is their sum: four footpads
            // landing on four pieces of ground are one landing. The window opens at the first touch and is not
            // stretched by later ones, so a long skid over bumps is many small knocks, not one huge one.
            float fresh = knock;
            if (_since < Rules.sameKnockSeconds) knock += _counted;
            else _since = 0f;
            _counted = knock;
            LastKnock = knock;

            PilotFate fate = Fate(knock, buckled);
            if (fate == PilotFate.Fine) return fate;
            float size = Math.Min(1f, fresh / Math.Max(1f, Rules.fullKickSpeed));
            // A belt holds the body, so the head moves less.
            float kick = Rules.kickDegrees * size * (buckled ? 1f : 1.8f);
            // Thrown at the spring as speed, so the view snaps away and swings back.
            float launch = (float)Math.Sqrt(Math.Max(1f, Rules.spring));
            _pitchSpeed += kick * launch * Clamp(fromFront, -1f, 1f);
            _rollSpeed += kick * launch * -Clamp(fromRight, -1f, 1f);
            return fate;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            _since += dt;
            // Fixed small steps keep the spring the same at any frame rate.
            int steps = Math.Max(1, (int)Math.Ceiling(dt / 0.005f));
            float h = dt / steps;
            float drag = 2f * Rules.damping * (float)Math.Sqrt(Math.Max(1f, Rules.spring));
            for (int i = 0; i < steps; i++)
            {
                _pitchSpeed += (-Rules.spring * _pitch - drag * _pitchSpeed) * h;
                _rollSpeed += (-Rules.spring * _roll - drag * _rollSpeed) * h;
                _pitch += _pitchSpeed * h;
                _roll += _rollSpeed * h;
            }
            if (Math.Abs(_pitch) < 0.01f && Math.Abs(_pitchSpeed) < 0.05f) { _pitch = 0f; _pitchSpeed = 0f; }
            if (Math.Abs(_roll) < 0.01f && Math.Abs(_rollSpeed) < 0.05f) { _roll = 0f; _rollSpeed = 0f; }
        }

        /// <summary>The plain words for a death, so the cause is never a mystery.</summary>
        public static string Cause(float knock, bool buckled)
        {
            string speed = Math.Round(knock).ToString("0") + " m/s";
            return buckled
                ? "The ship stopped dead from " + speed + ". No belt saves you from that."
                : "Thrown about the cabin by a " + speed + " knock. You were not buckled in.";
        }

        /// <summary>How loud a knock should sound, 0 to 1, and how deep (1 is normal pitch; harder is deeper).</summary>
        public static float Loudness(float normalSpeed) => Clamp((normalSpeed - 0.5f) / 14f, 0f, 1f);
        public static float Pitch(float normalSpeed) => 1.15f - 0.4f * Clamp(normalSpeed / 30f, 0f, 1f);

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
