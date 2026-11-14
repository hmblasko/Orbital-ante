using System;

namespace OrbitalAnte.Cockpit
{
    /// <summary>The picture lamps on the console's right wing, in the order they stand.</summary>
    public enum ConsoleLamp { Lock, Auto, Match, Fuel, Air }

    /// <summary>
    /// What the console's real instruments show: how full the fuel tube is, how far round the air needle has
    /// turned, and which picture lamps are lit. Plain C#, no Unity types. <see cref="ConsoleInstruments"/>
    /// moves the model's pieces to match.
    ///
    /// The numbers come from the dash's own readings, so the needle glides exactly as the screen's did and a
    /// warning blinks in step with the notice on the screen.
    /// </summary>
    public sealed class ConsoleGauges
    {
        public const int LampCount = 5;
        /// <summary>The tube never shows quite empty: a sliver stays, so a dry tank and a missing gauge look different.</summary>
        public const float FuelSliver = 0.02f;
        /// <summary>At or under this much air the Air lamp stops blinking and burns.</summary>
        public const float AirGone = 0.002f;

        /// <summary>0 (empty) to 1 (full): the length of the liquid in the tube.</summary>
        public float FuelLevel = 1f;
        /// <summary>0 (needle at empty) to 1 (needle at full).</summary>
        public float AirTurn = 1f;
        public readonly bool[] Lamps = new bool[LampCount];

        public bool Lit(ConsoleLamp lamp) => Lamps[(int)lamp];

        /// <summary>
        /// <paramref name="locked"/>: a body is locked. <paramref name="autopilot"/>: the autopilot is flying.
        /// Matching speed, fuel and air come from the readings.
        /// </summary>
        public void Set(DashReadings r, bool locked, bool autopilot)
        {
            if (r == null) return;
            Set(r.Fuel, r.Oxygen, r.FuelLow, r.FuelOut, r.OxygenLow, r.Blink, locked, autopilot, r.Matching);
        }

        /// <summary>
        /// The same from raw numbers, for when nobody is aboard to need the eased readings: seen through the
        /// glass from outside, the instruments still tell the truth.
        /// </summary>
        public void Set(float fuel, float air, bool fuelLow, bool fuelOut, bool airLow, bool blink, bool locked, bool autopilot, bool matching)
        {
            FuelLevel = Math.Max(FuelSliver, Math.Min(1f, fuel));
            AirTurn = Math.Max(0f, Math.Min(1f, air));
            Lamps[(int)ConsoleLamp.Lock] = locked;
            Lamps[(int)ConsoleLamp.Auto] = autopilot;
            Lamps[(int)ConsoleLamp.Match] = matching;
            // Out burns steadily; low blinks. A lamp that only ever blinked could be caught dark at a glance.
            Lamps[(int)ConsoleLamp.Fuel] = fuelOut || (fuelLow && blink);
            Lamps[(int)ConsoleLamp.Air] = air <= AirGone || (airLow && blink);
        }

        /// <summary>The model's piece for a lamp's lit face.</summary>
        public static string PieceOf(ConsoleLamp lamp) => "lamp" + lamp + "Glow";

        public const string Model = "Console";
        public const string Body = "console";
        public const string FuelPiece = "fuelLevelGlow", NeedlePiece = "airNeedleGlow";
        public const string DashSocket = "Dash", FuelBase = "FuelBase", FuelTop = "FuelTop";
        public const string AirDial = "AirDial", AirDialOut = "AirDialOut", AirEmpty = "AirEmpty", AirHalf = "AirHalf", AirFull = "AirFull";
        /// <summary>The needle sweeps this far from empty to full, by way of the top of the dial.</summary>
        public const float NeedleSweep = 240f;
    }
}
