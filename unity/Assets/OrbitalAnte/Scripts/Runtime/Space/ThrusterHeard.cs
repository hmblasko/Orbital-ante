using System;

namespace OrbitalAnte.Space
{
    /// <summary>
    /// What the ship's thrusters sound like for a given push, so you can hear which way you are being pushed
    /// with your eyes shut. Plain numbers, no engine.
    ///
    /// The lander has two kinds of engine. The big ones underneath lift it: a deep rumble felt through the
    /// seat. The small ones round the hull push it sideways, fore, aft and down, and turn it: a sharper hiss
    /// that comes from the side of the cabin the firing thruster is on, and has its own note for each way.
    /// </summary>
    public struct ThrusterHeard
    {
        /// <summary>The big engines underneath, 0 to 1.</summary>
        public float Main;
        /// <summary>The small thrusters, 0 to 1.</summary>
        public float Jets;
        /// <summary>Where the hiss comes from: -1 the left ear, +1 the right.</summary>
        public float Pan;
        /// <summary>The note of the hiss (1 is the recording as made).</summary>
        public float Pitch;

        public const float SideNote = 1.2f, FrontNote = 1.07f, RearNote = 0.93f, DownNote = 0.8f, TurnNote = 1.35f;
        /// <summary>How loud turning alone is, next to a full sideways push.</summary>
        public const float TurnShare = 0.3f;

        /// <summary>
        /// <paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>: the push in the ship's own
        /// directions (right, up, nose), each -1 to 1, already scaled by how hard the engines are working.
        /// <paramref name="turn"/>: how hard it is being turned, 0 to 1.
        /// </summary>
        public static ThrusterHeard For(float x, float y, float z, float turn)
        {
            float up = Math.Max(0f, y), down = Math.Max(0f, -y);
            float side = Math.Abs(x), front = Math.Max(0f, -z), rear = Math.Max(0f, z);
            turn = Math.Max(0f, Math.Min(1f, turn));
            var mix = new ThrusterHeard { Main = Math.Min(1f, up) };

            float push = (float)Math.Sqrt(x * x + down * down + z * z);
            mix.Jets = Math.Min(1f, Math.Max(push, turn * TurnShare));
            // A push to the right comes from the thruster on the left wall, and the other way about.
            mix.Pan = Math.Max(-0.85f, Math.Min(0.85f, -x * 0.85f));

            float heard = side + front + rear + down + turn * TurnShare;
            mix.Pitch = heard > 1e-4f
                ? (side * SideNote + front * FrontNote + rear * RearNote + down * DownNote + turn * TurnShare * TurnNote) / heard
                : 1f;
            return mix;
        }
    }
}
