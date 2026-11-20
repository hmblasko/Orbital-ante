using System;
using OrbitalAnte.Navigation;

namespace OrbitalAnte.UI
{
    /// <summary>What a reading or lamp is telling you, which is the only thing its colour says.</summary>
    public enum DashTone
    {
        /// <summary>Nothing to do: cream.</summary>
        Plain,
        /// <summary>Worth watching: amber.</summary>
        Watch,
        /// <summary>Do something now: red.</summary>
        Act,
        /// <summary>About the body you aim at or have locked: blue.</summary>
        Target,
        /// <summary>A state you were trying to reach: green.</summary>
        Good,
    }

    /// <summary>The seat display's lamps, left to right.</summary>
    public enum DashLamp
    {
        Boost,
        Match,
        Lock,
        Auto,
        Orbit,
        Pull,
        Brake,
    }

    /// <summary>
    /// The rules behind the seat display (Matthew, 20 Nov 2026: the stats on screen should be "super interesting",
    /// simple, sharp and easily visible; option B of the mockups, a strip of readings along the dash under a row of
    /// lamps). Plain C#, no Unity types.
    ///
    /// Each reading is split into a big number, a small unit and one short note, so it reads at a glance and keeps
    /// one place on screen. Each state the old display spelled out in a sentence is a lamp that lights, flashing as
    /// it comes on so the change is noticed.
    /// </summary>
    public static class DashStrip
    {
        /// <summary>How long a lamp flashes when it comes on, seconds.</summary>
        public const float FlashSeconds = 0.6f;

        static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

        /// <summary>One on-and-off of the flash, seconds.</summary>
        public const float FlashPeriod = 0.2f;

        /// <summary>How long the list of keys shows after you sit down, before it starts to fade, seconds.</summary>
        public const float KeysShownSeconds = 10f;

        /// <summary>How long the list of keys takes to fade out, seconds.</summary>
        public const float KeysFadeSeconds = 2f;

        /// <summary>How far past a circle's speed the orbit bar reaches: the notch sits at 1 / this of the bar.</summary>
        public const double OrbitBarReach = 1.5;

        /// <summary>The words on each lamp. The orbit lamp's word changes with the path (see <see cref="OrbitWord"/>).</summary>
        public static string LampWord(DashLamp lamp)
        {
            switch (lamp)
            {
                case DashLamp.Boost: return "BOOST";
                case DashLamp.Match: return "MATCH";
                case DashLamp.Lock: return "LOCK";
                case DashLamp.Auto: return "AUTO";
                case DashLamp.Orbit: return "ORBIT";
                case DashLamp.Pull: return "PULL";
                case DashLamp.Brake: return "BRAKE";
                default: return string.Empty;
            }
        }

        /// <summary>The colour a lamp lights in.</summary>
        public static DashTone LampTone(DashLamp lamp, OrbitKind orbit)
        {
            switch (lamp)
            {
                case DashLamp.Lock: return DashTone.Target;
                case DashLamp.Orbit: return OrbitTone(orbit);
                case DashLamp.Pull:
                case DashLamp.Brake: return DashTone.Act;
                default: return DashTone.Watch;
            }
        }

        /// <summary>The orbit lamp's word for the path you are coasting on.</summary>
        public static string OrbitWord(OrbitKind kind)
        {
            switch (kind)
            {
                case OrbitKind.Orbit: return "IN ORBIT";
                case OrbitKind.Tugged: return "TUGGED";
                case OrbitKind.MeetsGround: return "FALLING";
                case OrbitKind.Leaves: return "LEAVING";
                default: return "ORBIT";
            }
        }

        /// <summary>A real orbit is the goal, so it is good news; every other path is worth watching.</summary>
        public static DashTone OrbitTone(OrbitKind kind) => kind == OrbitKind.Orbit ? DashTone.Good : DashTone.Watch;

        /// <summary>
        /// Whether a lamp shows lit this moment: dark when off, lit when on, but for its first
        /// <see cref="FlashSeconds"/> it blinks so a change is seen out of the corner of the eye.
        /// </summary>
        public static bool LampLit(bool on, float onForSeconds)
        {
            if (!on) return false;
            if (!(onForSeconds < FlashSeconds) || onForSeconds < 0f) return true;
            return onForSeconds % FlashPeriod < FlashPeriod * 0.5f;
        }

        /// <summary>How visible the list of keys is, 0 to 1, a while after sitting down.</summary>
        public static float KeysAlpha(float secondsSinceSeated)
        {
            if (!(secondsSinceSeated > KeysShownSeconds)) return 1f;
            float t = (secondsSinceSeated - KeysShownSeconds) / KeysFadeSeconds;
            return t >= 1f ? 0f : 1f - t;
        }

        /// <summary>
        /// The orbit bar: how far it is filled (your speed across the world's face) and where the notch is (the speed
        /// a circle at this height needs), both 0 to 1. Filling to the notch means a circle.
        /// </summary>
        public static void OrbitBar(double sideSpeed, double circleSpeed, out float fill, out float notch)
        {
            notch = (float)(1.0 / OrbitBarReach);
            if (!(circleSpeed > 0.0))
            {
                fill = 0f;
                return;
            }
            double f = Math.Abs(sideSpeed) / (circleSpeed * OrbitBarReach);
            fill = (float)(f < 0.0 ? 0.0 : f > 1.0 ? 1.0 : f);
        }

        /// <summary>A distance as a number and a unit, the same rounding as the rest of the game (m, then km).</summary>
        public static void SplitDistance(double metres, out string number, out string unit)
        {
            double size = Math.Abs(metres);
            if (size < 1000)
            {
                number = Math.Round(metres).ToString("0", Inv);
                unit = "M";
            }
            else if (size < 100000)
            {
                number = (metres / 1000.0).ToString("0.0", Inv);
                unit = "KM";
            }
            else
            {
                number = (metres / 1000.0).ToString("#,0", Inv);
                unit = "KM";
            }
        }

        /// <summary>A speed as a number and a unit: tenths when slow (landing), whole metres, then km/s when fast.</summary>
        public static void SplitSpeed(double metresPerSecond, out string number, out string unit)
        {
            double size = Math.Abs(metresPerSecond);
            if (size < 100)
            {
                number = metresPerSecond.ToString("0.0", Inv);
                unit = "M/S";
            }
            else if (size < 10000)
            {
                number = Math.Round(metresPerSecond).ToString("0", Inv);
                unit = "M/S";
            }
            else
            {
                number = (metresPerSecond / 1000.0).ToString("0.0", Inv);
                unit = "KM/S";
            }
        }

        /// <summary>
        /// A whole number that changes exactly when <see cref="SplitSpeed"/>'s text would, so the caller can skip
        /// building text while a reading holds still. The band (tenths, whole metres, km/s) is kept apart.
        /// </summary>
        public static long SpeedKey(double metresPerSecond)
        {
            if (double.IsNaN(metresPerSecond) || double.IsInfinity(metresPerSecond)) return long.MinValue + 1;
            double size = Math.Abs(metresPerSecond);
            if (size < 100) return (long)Math.Round(metresPerSecond * 10.0);
            if (size < 10000) return 1000000000000L + (long)Math.Round(metresPerSecond);
            return 2000000000000L + (long)Math.Round(metresPerSecond / 100.0);
        }

        /// <summary>A whole number that changes exactly when <see cref="SplitDistance"/>'s text would.</summary>
        public static long DistanceKey(double metres)
        {
            if (double.IsNaN(metres) || double.IsInfinity(metres)) return long.MinValue + 1;
            double size = Math.Abs(metres);
            if (size < 1000) return (long)Math.Round(metres);
            if (size < 100000) return 1000000000000L + (long)Math.Round(metres / 100.0);
            return 2000000000000L + (long)Math.Round(metres / 1000.0);
        }

        /// <summary>
        /// Moves a shown number toward the true one so readings roll rather than jump, and lands exactly once it is
        /// close. <paramref name="rate"/> is how fast, per second (12 covers most of the gap in a quarter second).
        /// </summary>
        public static double RollToward(double shown, double target, float deltaSeconds, float rate = 12f)
        {
            if (double.IsNaN(shown) || double.IsInfinity(shown)) return target;
            if (!(deltaSeconds > 0f)) return shown;
            double gap = target - shown;
            // A jump far bigger than the reading itself (a new body, a reset) is taken at once.
            if (Math.Abs(gap) > Math.Min(Math.Abs(target), Math.Abs(shown)) * 4.0 + 1000.0) return target;
            double next = shown + gap * (1.0 - Math.Exp(-deltaSeconds * rate));
            if (Math.Abs(target - next) < Math.Max(0.005, Math.Abs(target) * 0.0005)) return target;
            return next;
        }
    }
}
