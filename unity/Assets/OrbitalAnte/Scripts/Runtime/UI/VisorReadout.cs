using System;

namespace OrbitalAnte.UI
{
    /// <summary>How a visor gauge should look: calm, worth a glance, or a danger.</summary>
    public enum GaugeTone
    {
        Calm,
        Warn,
        Alarm,
    }

    /// <summary>
    /// The numbers behind the two visor gauges (Matthew, 15 Nov 2026: "more interesting display of health,
    /// oxygen"). Plain C#, no Unity types.
    ///
    /// Air is an arc in the lower left with the per cent and the time left at a resting breath. The suit's
    /// armour is an arc of ten plates in the lower right: a plate goes dark only when it is used up, so a
    /// plate still showing always has something left in it, and the last one still on turns amber while it is
    /// being worn through.
    /// </summary>
    public static class VisorReadout
    {
        /// <summary>How many plates the suit gauge is cut into.</summary>
        public const int Plates = 10;

        /// <summary>The share of a full suit at or below which the suit gauge is a danger.</summary>
        public const float SuitDanger = 0.35f;

        /// <summary>Plates still showing for a share of the suit left, 0 to <see cref="Plates"/>. Zero only at zero.</summary>
        public static int PlatesLeft(float fraction)
        {
            if (!(fraction > 0f)) return 0;
            if (fraction >= 1f) return Plates;
            // A whisker of float error must not show a plate that is not there (0.3 * 10 is 3.0000001).
            int n = (int)Math.Ceiling(fraction * Plates - 1e-4f);
            return n < 1 ? 1 : n > Plates ? Plates : n;
        }

        /// <summary>How much of the last plate still showing is left, 0 to 1 (1 when it is whole, or none show).</summary>
        public static float LastPlate(float fraction)
        {
            int n = PlatesLeft(fraction);
            if (n == 0) return 1f;
            float part = fraction * Plates - (n - 1);
            return part < 0f ? 0f : part > 1f ? 1f : part;
        }

        /// <summary>True when the last plate showing has been worn into: that plate is drawn amber.</summary>
        public static bool LastPlateWorn(float fraction)
        {
            return PlatesLeft(fraction) > 0 && fraction < 1f && LastPlate(fraction) < 0.999f;
        }

        /// <summary>A share as a whole per cent, rounded up so that anything left never reads 0.</summary>
        public static int Percent(float fraction)
        {
            if (!(fraction > 0f)) return 0;
            if (fraction >= 1f) return 100;
            int p = (int)Math.Ceiling(fraction * 100f - 1e-3f);
            return p < 1 ? 1 : p > 100 ? 100 : p;
        }

        /// <summary>Seconds of air left at a resting breath (the jets burning air make it shorter).</summary>
        public static int AirSeconds(float air, float airMinutes)
        {
            if (!(air > 0f) || !(airMinutes > 0f)) return 0;
            return (int)Math.Floor(Math.Min(air, 1f) * airMinutes * 60f);
        }

        /// <summary>
        /// The line under the air number: "4:12 left" at a resting breath, "NO AIR 9" while the last breath lasts.
        /// While the jets burn air the resting time would be far too long (a full tank lasts well under a minute
        /// of full push), so it says that instead of a time.
        /// </summary>
        public static string AirLine(float air, float airMinutes, bool airOut, float graceLeft, bool burningAir = false)
        {
            if (airOut || !(air > 0f)) return "NO AIR  " + Math.Ceiling(Math.Max(0f, graceLeft)).ToString("0");
            if (burningAir) return JetsOnAir;
            int s = AirSeconds(air, airMinutes);
            return (s / 60).ToString("0") + ":" + (s % 60).ToString("00") + " left";
        }

        public static GaugeTone AirTone(bool airLow, bool airOut, bool burningAir)
        {
            if (airOut || airLow) return GaugeTone.Alarm;
            return burningAir ? GaugeTone.Warn : GaugeTone.Calm;
        }

        public static GaugeTone SuitTone(float fraction)
        {
            if (fraction <= SuitDanger) return GaugeTone.Alarm;
            return LastPlateWorn(fraction) ? GaugeTone.Warn : GaugeTone.Calm;
        }

        public const string JetsOnAir = "jets burning air";
        public const string ArmourFailing = "armour failing";
        public const string NoArmour = "no armour left";

        /// <summary>
        /// The line under the suit number: empty while the plates say it all, words only once it is a danger.
        /// Always one of three fixed strings, so it never allocates.
        /// </summary>
        public static string SuitLine(float fraction)
        {
            if (PlatesLeft(fraction) == 0) return NoArmour;
            return fraction <= SuitDanger ? ArmourFailing : string.Empty;
        }
    }
}
