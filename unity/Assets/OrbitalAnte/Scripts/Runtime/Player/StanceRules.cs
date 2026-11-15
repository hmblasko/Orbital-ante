using System;

namespace OrbitalAnte.Player
{
    /// <summary>Stance geometry and the shared crouch/descent input rule. No engine types.</summary>
    public static class StanceRules
    {
        public static float NonNegative(float value, float fallback = 0f) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(0f, value);

        public static float CrouchHeight(float requested, float standing, float radius) =>
            Math.Min(standing, Math.Max(2f * radius, NonNegative(requested, standing)));

        public static float EyeHeight(float height, float standing, float standingEye, float crouched, float crouchedEye)
        {
            float share = standing > crouched ? Math.Max(0f, Math.Min(1f, (height - crouched) / (standing - crouched))) : 1f;
            return Math.Min(height, NonNegative(crouchedEye, standingEye) * (1f - share) + standingEye * share);
        }

        public static int Lean(bool left, bool right) => (right ? 1 : 0) - (left ? 1 : 0);
    }

    /// <summary>A held ground crouch never becomes an unasked downward jet when jumping or leaving an edge.</summary>
    public sealed class DownJetLatch
    {
        bool _releaseRequired;
        public bool Read(bool onFoot, bool held)
        {
            if (!held) _releaseRequired = false;
            else if (onFoot) _releaseRequired = true;
            return held && !onFoot && !_releaseRequired;
        }
        public void Reset() => _releaseRequired = false;
    }
}
