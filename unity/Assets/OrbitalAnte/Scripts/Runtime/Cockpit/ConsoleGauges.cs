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
        /// <summary>0 (lever right back) to 1 (lever right forward): how hard the engines are being asked to push.</summary>
        public float Throttle;
        /// <summary>How quickly the lever follows the push: a hand's quick shove, not a jump.</summary>
        public const float ThrottleQuick = 12f;

        /// <summary>
        /// Where the lever should stand, 0 to 1, for what the engines are being asked to do. Everything is in shares
        /// of the ordinary push, and the lever's far end is the hardest ask the ship has, so each ask stops at its
        /// own place: with the shipped dials a full ordinary push is 0.4 of the way, the autopilot's hardest 0.6,
        /// Match Velocity 0.8 and the boost right forward.
        /// <paramref name="stick"/>: the pilot's push, 0 to 1. <paramref name="boostShare"/>: how many times harder
        /// the boost pushes. <paramref name="brakeShare"/>: how many times harder Match Velocity brakes.
        /// <paramref name="autopilotShare"/>: what the autopilot is asking for now, and
        /// <paramref name="autopilotMost"/> the most it can ask.
        /// </summary>
        public static float LeverFor(float stick, bool boosting, float boostShare, bool braking, float brakeShare, bool autopilot, float autopilotShare, float autopilotMost)
        {
            boostShare = Sound(boostShare, 1f); brakeShare = Sound(brakeShare, 1f); autopilotMost = Sound(autopilotMost, 1f);
            float most = Math.Max(1f, Math.Max(boostShare, Math.Max(brakeShare, autopilotMost)));
            float ask = autopilot ? Math.Min(Sound(autopilotShare, 0f), autopilotMost) : Math.Min(1f, Sound(stick, 0f)) * (boosting ? boostShare : 1f);
            if (braking) ask = Math.Max(ask, brakeShare);
            return Math.Max(0f, Math.Min(1f, ask / most));
        }

        static float Sound(float v, float otherwise) => float.IsNaN(v) || float.IsInfinity(v) || v < 0f ? otherwise : v;

        /// <summary>
        /// Moves the throttle lever toward how hard the engines are being asked to push (0 to 1), whoever is asking:
        /// the pilot, the autopilot or Match Velocity. It is the ask that is shown, not what a dry tank can give,
        /// so the lever never contradicts the pilot's own hand.
        /// </summary>
        public void Push(float share, float dt)
        {
            if (float.IsNaN(share)) share = 0f;
            float target = Math.Max(0f, Math.Min(1f, share));
            float step = dt > 0f ? 1f - (float)Math.Exp(-ThrottleQuick * dt) : 0f;
            Throttle += (target - Throttle) * step;
            if (Math.Abs(target - Throttle) < 0.002f) Throttle = target;
        }

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
        /// <summary>The throttle's lever (it does not shine: the bake tool lights it with the rest of the console) and the two ends of its slide.</summary>
        public const string ThrottlePiece = "throttle", ThrottleIdle = "ThrottleIdle", ThrottleFull = "ThrottleFull";
        public const string DashSocket = "Dash", FuelBase = "FuelBase", FuelTop = "FuelTop";
        public const string AirDial = "AirDial", AirDialOut = "AirDialOut", AirEmpty = "AirEmpty", AirHalf = "AirHalf", AirFull = "AirFull";
        /// <summary>The needle sweeps this far from empty to full, by way of the top of the dial.</summary>
        public const float NeedleSweep = 240f;
    }
}
