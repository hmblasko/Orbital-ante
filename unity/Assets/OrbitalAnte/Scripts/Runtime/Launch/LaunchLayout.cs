using System;
using OrbitalAnte.Ship;

namespace OrbitalAnte.Launch
{
    /// <summary>
    /// Every dimension of the launch base, in the pad's own space: the origin is the middle of the pad's top,
    /// +Y up, +Z north (the way the ship's nose points on the pad), +X east. Metres. The code-built stand-ins
    /// (<see cref="LaunchShapes"/>), the colliders and the Blender model (Resources/Models/LaunchPad, one part
    /// per name in <see cref="Pieces"/>, each modelled in this same space) all follow these numbers.
    /// </summary>
    public static class LaunchLayout
    {
        /// <summary>The model file's id in the model library.</summary>
        public const string Model = "LaunchPad";

        public const string Mound = "Mound", Tower = "Tower", WaterTower = "WaterTower", Hut = "Hut", PadLamps = "PadLamps", Arm = "Arm";
        /// <summary>Floodlight masts are "Floodlight0" to "Floodlight3".</summary>
        public const string Floodlight = "Floodlight";
        public const int Masts = 4;

        /// <summary>Every piece by name. All stand still except the arm, which is modelled with its hinge at its own origin, reaching along +X.</summary>
        /// <summary>
        /// A model keeps whatever shines (lamp glass, floodlight faces) in a separate piece named after its owner
        /// with this on the end ("PadLampsGlow", "Floodlight0Glow"). Model files hold no glow of their own, so the
        /// name is what makes it shine. A model without one simply has nothing lit there.
        /// </summary>
        public const string GlowSuffix = "Glow";

        public static readonly string[] Pieces =
        {
            Mound, Tower, WaterTower, Hut, PadLamps, Floodlight + "0", Floodlight + "1", Floodlight + "2", Floodlight + "3", Arm,
        };

        public const float PadRadius = 15f;
        /// <summary>The scorched blast dish under the engines.</summary>
        public const float DishRadius = 3.4f;
        public const float DishDepth = 0.28f;
        /// <summary>The ship's own origin (its cabin floor) when it stands on the pad.</summary>
        public const float ShipHeightOnPad = -ShipLayout.GroundY;

        public static readonly P3 TowerFoot = new P3(-10.2f, 0f, 0f);
        public const float TowerHalf = 1.2f;
        public const float TowerHeight = 15f;
        /// <summary>Where the swing arm is hinged: the tower's corner nearest the ship, at the height of the cabin's side window.</summary>
        public static readonly P3 ArmPivot = new P3(-9f, ShipHeightOnPad + 0.2f, 0f);
        public const float ArmLength = 6.2f;
        /// <summary>How far the arm swings clear of the ship at ignition, in degrees about its hinge.</summary>
        public const float ArmSwingDegrees = 78f;
        public static readonly P3 WaterTowerFoot = new P3(9.6f, 0f, -9.6f);
        public const float WaterTowerHeight = 9f;
        public const float WaterTowerRadius = 1.9f;
        public static readonly P3 HutCentre = new P3(10.2f, 0f, 7.2f);
        public static readonly P3 HutHalf = new P3(2.2f, 1.3f, 1.7f);
        public const float MastDistance = 11.6f;
        public const float MastHeight = 9.5f;

        /// <summary>The foot of floodlight mast <paramref name="i"/> (0 to 3): one at each corner, outside the pad's rim.</summary>
        public static P3 Mast(int i)
        {
            double a = (45 + 90 * i) * Math.PI / 180.0;
            return new P3(Math.Sin(a) * MastDistance * 1.2, 0, Math.Cos(a) * MastDistance * 1.2);
        }
    }
}
