using System;

namespace OrbitalAnte.Audio
{
    /// <summary>The thrusters a ship can fire, named by where on the hull they sit.</summary>
    public enum Thruster
    {
        /// <summary>Main engine at the back: pushes the ship forwards.</summary>
        Main,
        /// <summary>Nose thrusters: push the ship backwards.</summary>
        Nose,
        /// <summary>Thrusters on the left side: push the ship to the right.</summary>
        Left,
        /// <summary>Thrusters on the right side: push the ship to the left.</summary>
        Right,
        /// <summary>Belly thrusters: push the ship up.</summary>
        Belly,
        /// <summary>Roof thrusters: push the ship down.</summary>
        Roof,
        /// <summary>Attitude jets: turn the ship.</summary>
        Turn,
    }

    /// <summary>
    /// Decides which thruster sounds play, and how loud, from what the pilot is asking the ship to do. Plain rules:
    /// no Unity types. A thruster fires on the side OPPOSITE to the way the ship is pushed, and that is the side
    /// it is heard from: push right and the left thrusters roar in the left ear.
    /// </summary>
    public sealed class ThrusterMix
    {
        public const int Count = 7;

        /// <summary>Below this much command a thruster is off (a resting stick is never exactly zero).</summary>
        public const float DeadZone = 0.06f;
        /// <summary>How fast a thruster's sound rises and falls, per second. Fast: these are valves, not turbines.</summary>
        public const float AttackPerSecond = 9f;
        public const float ReleasePerSecond = 5f;
        /// <summary>The main engine is a bigger machine and takes longer to speak and to die away.</summary>
        public const float MainAttackPerSecond = 3.5f;
        public const float MainReleasePerSecond = 1.8f;
        /// <summary>A valve does not clack again within this many seconds, however the stick is waggled.</summary>
        public const float ClackGap = 0.22f;

        static readonly string[] Loops =
        {
            "ship.engine.loop", "ship.thruster.nose.loop", "ship.thruster.left.loop", "ship.thruster.right.loop",
            "ship.thruster.belly.loop", "ship.thruster.roof.loop", "ship.thruster.turn.loop",
        };

        /// <summary>Where each thruster's valve clack is heard, left (-1) to right (+1).</summary>
        static readonly float[] Pans = { 0f, 0f, -0.75f, 0.75f, 0f, 0f, 0f };

        readonly float[] _level = new float[Count];
        readonly float[] _target = new float[Count];
        readonly bool[] _open = new bool[Count];
        readonly float[] _sinceClack = new float[Count];
        readonly bool[] _opened = new bool[Count];
        readonly bool[] _closed = new bool[Count];

        public ThrusterMix()
        {
            for (int i = 0; i < Count; i++) _sinceClack[i] = 10f;
        }

        public static string LoopEvent(Thruster t) => Loops[(int)t];
        public static float Pan(Thruster t) => Pans[(int)t];

        /// <summary>How loud this thruster's loop is now, 0 to 1.</summary>
        public float Level(Thruster t) => _level[(int)t];
        /// <summary>True for the one update in which this thruster's valve opened (play the start sound).</summary>
        public bool Opened(Thruster t) => _opened[(int)t];
        /// <summary>True for the one update in which this thruster's valve closed (play the stop sound).</summary>
        public bool Closed(Thruster t) => _closed[(int)t];

        /// <summary>The main engine's pitch: it climbs a little as it is pushed harder.</summary>
        public float MainPitch => 0.85f + 0.35f * _level[(int)Thruster.Main];

        /// <summary>
        /// Call every frame. <paramref name="right"/>, <paramref name="up"/> and <paramref name="forward"/> are the
        /// push the pilot is asking for along the ship's own axes, each -1 to 1 (not the ship's speed).
        /// <paramref name="turn"/> is how hard the ship is being turned, 0 to 1. <paramref name="boost"/> scales
        /// the main engine only (1 = full burn).
        /// </summary>
        public void Update(float right, float up, float forward, float turn, float boost, float dt)
        {
            _target[(int)Thruster.Main] = Shape(forward) * Clamp01(boost);
            _target[(int)Thruster.Nose] = Shape(-forward);
            _target[(int)Thruster.Left] = Shape(right);
            _target[(int)Thruster.Right] = Shape(-right);
            _target[(int)Thruster.Belly] = Shape(up);
            _target[(int)Thruster.Roof] = Shape(-up);
            _target[(int)Thruster.Turn] = Shape(turn);

            for (int i = 0; i < Count; i++)
            {
                _opened[i] = false;
                _closed[i] = false;
                _sinceClack[i] += Math.Max(0f, dt);
                bool main = i == (int)Thruster.Main;
                float rate = _target[i] > _level[i] ? (main ? MainAttackPerSecond : AttackPerSecond) : (main ? MainReleasePerSecond : ReleasePerSecond);
                _level[i] = MoveTowards(_level[i], _target[i], rate * Math.Max(0f, dt));

                bool open = _target[i] > 0f;
                if (open != _open[i])
                {
                    _open[i] = open;
                    // The main engine has its own ignition sound and the attitude jets are nothing but clacks
                    // already, so neither gets a valve sound from here.
                    if (!main && i != (int)Thruster.Turn && _sinceClack[i] >= ClackGap)
                    {
                        if (open) _opened[i] = true; else _closed[i] = true;
                        _sinceClack[i] = 0f;
                    }
                }
            }
        }

        /// <summary>Everything off at once (pause, leaving the seat). No valve sounds.</summary>
        public void Silence()
        {
            for (int i = 0; i < Count; i++)
            {
                _level[i] = 0f;
                _target[i] = 0f;
                _open[i] = false;
                _opened[i] = false;
                _closed[i] = false;
            }
        }

        /// <summary>Dead zone, then a curve that makes a light touch clearly audible without full stick being deafening.</summary>
        static float Shape(float command)
        {
            if (command <= DeadZone) return 0f;
            float x = Clamp01((command - DeadZone) / (1f - DeadZone));
            return 0.35f + 0.65f * x;
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        static float MoveTowards(float from, float to, float step)
        {
            if (Math.Abs(to - from) <= step) return to;
            return from + Math.Sign(to - from) * step;
        }
    }
}
