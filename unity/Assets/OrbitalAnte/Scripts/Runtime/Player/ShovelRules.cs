using System;

namespace OrbitalAnte.Player
{
    /// <summary>
    /// The shovel's timing (Matthew, 17 Nov 2026: "give the player the ability to shovel holes and dig in the
    /// ground"). Hold Dig and it bites again and again: a short thrust, the bite (where the ground is taken), then
    /// the pull back. A press in the last part of the pull back is kept, so tapping keeps the rhythm. Plain C#.
    /// </summary>
    public sealed class ShovelSwing
    {
        /// <summary>From the press to the blade biting, in seconds.</summary>
        public const float Thrust = 0.16f;
        /// <summary>From the bite to being ready again, in seconds.</summary>
        public const float PullBack = 0.55f;
        /// <summary>How far in front of the eye the blade reaches, in metres.</summary>
        public const float Reach = 2.7f;
        /// <summary>How far off digging is heard, as a share of an enemy's hearing range (walking about is 1, jets 1.6, a shot 2): loud work that hides you once it is done.</summary>
        public const float HeardFrom = 1.4f;
        /// <summary>How long after a bite it is still heard, in seconds.</summary>
        public const float HeardFor = 0.7f;
        /// <summary>A press this close to the end of the pull back starts the next bite as soon as it can.</summary>
        const float KeepPress = 0.12f;

        float _t = -1f;
        bool _bitten;
        bool _queued;

        public bool Busy => _t >= 0f;
        public float Length => Thrust + PullBack;

        /// <summary>
        /// Moves the swing on by <paramref name="dt"/>. <paramref name="held"/> is the Dig button (held or pressed).
        /// True on exactly the step the blade bites.
        /// </summary>
        public bool Tick(float dt, bool held)
        {
            if (float.IsNaN(dt) || dt < 0f) dt = 0f;
            if (!Busy)
            {
                if (!held && !_queued) return false;
                _queued = false;
                _t = 0f;
                _bitten = false;
            }
            else if (held && _t > Length - KeepPress) _queued = true;
            _t += dt;
            bool bite = false;
            if (!_bitten && _t >= Thrust)
            {
                _bitten = true;
                bite = true;
            }
            if (_t >= Length)
            {
                _t = -1f;
                if (held) _queued = true;
            }
            return bite;
        }

        /// <summary>How far forward the blade is, 0 (resting) to 1 (biting), for the stand-in's pose.</summary>
        public float Pose
        {
            get
            {
                if (!Busy) return 0f;
                if (_t < Thrust)
                {
                    float a = _t / Thrust;
                    return a * a * (3f - 2f * a);
                }
                float b = Math.Min(1f, (_t - Thrust) / PullBack);
                return 1f - b * b * (3f - 2f * b);
            }
        }

        /// <summary>The suit display's words for a bite the ground refused (constitution rule 3: never a hidden rule).</summary>
        public static string Words(OrbitalAnte.Planets.DigRefusal why)
        {
            switch (why)
            {
                case OrbitalAnte.Planets.DigRefusal.Core: return "THE CORE: NOWHERE DEEPER TO DIG";
                case OrbitalAnte.Planets.DigRefusal.NearWater: return "CAN'T DIG: THE SEA WOULD COME IN";
                case OrbitalAnte.Planets.DigRefusal.OldCave: return "CAN'T DIG: GROUND TOO LOOSE HERE";
                case OrbitalAnte.Planets.DigRefusal.Full: return "CAN'T DIG: THIS WORLD IS DUG OUT";
                default: return null;
            }
        }

        public void Cancel()
        {
            _t = -1f;
            _queued = false;
        }
    }
}
