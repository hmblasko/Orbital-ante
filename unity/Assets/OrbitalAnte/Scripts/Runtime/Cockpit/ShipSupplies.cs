using System;

namespace OrbitalAnte.Cockpit
{
    /// <summary>The numbers behind the ship's fuel and air. Held in the CockpitConfig asset.</summary>
    [Serializable]
    public sealed class SupplyRules
    {
        /// <summary>Seconds of steady full push (no boost) a full tank gives. Coasting costs nothing.</summary>
        public float fuelSeconds = 420f;
        /// <summary>How many times faster boost drinks fuel. More than the extra push it gives, so boost is quick but wasteful.</summary>
        public float boostCost = 3.5f;
        /// <summary>Below this share of a tank the dash warns you.</summary>
        public float fuelLow = 0.2f;
        /// <summary>With the tank dry the engines still give this share of their push, so you are slowed, never stranded.</summary>
        public float reservePush = 0.3f;
        /// <summary>Seconds to fill an empty tank while standing on the launch pad with the engines off.</summary>
        public float refuelSeconds = 6f;
        /// <summary>Seconds to fill an empty tank standing still on any other ground, engines off. Slow, but it means you are never stuck.</summary>
        public float fieldRefuelSeconds = 45f;
        /// <summary>Whether a sealed cabin uses up air. Off until the suit arrives (Milestone 11), so the dash never warns about something that cannot hurt you.</summary>
        public bool sealedCabinUsesAir = false;
        /// <summary>Minutes of air in the ship's tank with the cabin sealed.</summary>
        public float oxygenMinutes = 30f;
        /// <summary>How many times faster the air goes for each broken window.</summary>
        public float leakPerBrokenWindow = 4f;
        /// <summary>Below this share of a tank the dash warns you.</summary>
        public float oxygenLow = 0.2f;
    }

    /// <summary>
    /// The ship's fuel and air, each as a share of a full tank. Plain C#, no Unity types.
    ///
    /// Kept deliberately small for now: enough for the dash to have something true to show. Fuel goes down
    /// only while you push (braking to match speed counts) and comes back whenever you stand still on the
    /// ground with the engines off: quickly on the launch pad, slowly anywhere else. Air only escapes through
    /// broken glass for now, and comes back the same way. Running out of air does nothing yet (the suit
    /// arrives with the jetpack); running out of fuel leaves a weak reserve push, and the dash says so.
    /// </summary>
    public sealed class ShipSupplies
    {
        public readonly SupplyRules Rules;
        public float Fuel { get; private set; } = 1f;
        public float Oxygen { get; private set; } = 1f;
        public bool FuelLow => Fuel <= Rules.fuelLow;
        public bool FuelOut => Fuel <= 0f;
        public bool OxygenLow => Oxygen <= Rules.oxygenLow;
        /// <summary>True while the tanks are being filled.</summary>
        public bool Filling { get; private set; }
        /// <summary>The share of push the engines can give for want of fuel: 1, or the reserve when dry.</summary>
        public float PushFactor => FuelOut ? Clamp01(Rules.reservePush) : 1f;

        public ShipSupplies(SupplyRules rules = null)
        {
            Rules = rules ?? new SupplyRules();
        }

        /// <param name="push">How hard the pilot is pushing in all directions together, 0 to 1.</param>
        /// <param name="onPad">Standing still on the launch pad.</param>
        /// <param name="landed">Standing still on any ground (the pad included).</param>
        public void Tick(float dt, float push, bool boost, bool onPad, int brokenWindows, bool landed = false)
        {
            Filling = false;
            if (dt <= 0f) return;
            // More than 1 is allowed: the autopilot pushes harder than a pilot's ordinary push and pays for it.
            push = push < 0f ? 0f : push > 3f ? 3f : push;
            if (push > 0.01f)
            {
                float use = push * (boost ? Math.Max(1f, Rules.boostCost) : 1f) / Math.Max(1f, Rules.fuelSeconds);
                Fuel = Math.Max(0f, Fuel - use * dt);
            }
            float leak = (Rules.sealedCabinUsesAir ? 1f : 0f) + Math.Max(0, brokenWindows) * Math.Max(0f, Rules.leakPerBrokenWindow);
            Oxygen = Math.Max(0f, Oxygen - leak * dt / Math.Max(1f, Rules.oxygenMinutes * 60f));

            if ((onPad || landed) && push <= 0.01f)
            {
                float fill = dt / Math.Max(0.1f, onPad ? Rules.refuelSeconds : Rules.fieldRefuelSeconds);
                // (The air used in this very step does not count: a full ship on the pad is simply full.)
                Filling = Fuel < 0.999f || Oxygen < 0.999f;
                Fuel = Math.Min(1f, Fuel + fill);
                Oxygen = Math.Min(1f, Oxygen + fill);
            }
        }

        public void Set(float fuel, float oxygen)
        {
            Fuel = Clamp01(fuel);
            Oxygen = Clamp01(oxygen);
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
