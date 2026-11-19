using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    /// <summary>
    /// Every dimension of the Strider, the first alien creature (Matthew, 17 Nov 2026: alien assets "that look
    /// realistic", from the most unusual real animals, that "gib and lose limbs"; this one starts from the Japanese
    /// spider crab). No Unity types.
    ///
    /// Strider space: origin on the ground under the middle of the body, +Y up, +Z the way it faces, +X its right.
    /// Its ten limbs are numbered side * 5 + place: place 0 is the claw arm, 1 to 4 the walking legs from the front
    /// back. Each limb is three pieces from the body out, and each piece is modelled in its own space with its joint
    /// at the origin, +Z along the piece and +Y the side that faces up when the limb is held level.
    ///
    /// tools/blender/build_creatures.py carries its own copy of these numbers and writes them into the model as
    /// sockets; the rule test StriderModelFile_MatchesTheLayout fails if the two drift.
    /// </summary>
    public static class StriderLayout
    {
        public const string Model = "Strider";

        public const int Right = 0;
        public const int Left = 1;
        public const int PerSide = 5;
        public const int Limbs = 10;
        public const int Segments = 3;
        public const int WalkingLegs = 8;

        /// <summary>The game draws the whole animal this much larger than it is modelled.</summary>
        public const double Size = 1.3;

        // Where each right-hand limb joins the body, in the model's own size (x right, y up from the body's middle,
        // z forward), and the way it points when held straight out, in degrees round from the right toward the front.
        static readonly double[] HipX = { 0.215, 0.335, 0.385, 0.360, 0.265 };
        static readonly double[] HipY = { -0.075, -0.080, -0.080, -0.080, -0.075 };
        static readonly double[] HipZ = { 0.300, 0.150, -0.020, -0.180, -0.310 };
        static readonly double[] OutDegrees = { 64.0, 34.0, 6.0, -24.0, -54.0 };

        public const double LegUpper = 1.52;
        public const double LegLower = 1.60;
        public const double LegTip = 0.56;
        public const double ArmUpper = 1.28;
        public const double ArmLower = 0.368;
        public const double Claw = 1.20;
        public const double Finger = 0.592;
        /// <summary>Where the claw's moving finger turns, in the claw's own space.</summary>
        public static readonly Vec3d ClawHinge = new Vec3d(0.0, 0.050, 0.624);
        /// <summary>The radius the wound is modelled at; it is scaled to the joint it covers.</summary>
        public const double StumpRadius = 0.075;

        /// <summary>How much longer or shorter each walking leg is than the one that is modelled, front to back.</summary>
        static readonly double[] LegScale = { 1.0, 1.06, 1.0, 0.90 };

        /// <summary>How high the middle of the body is carried on all eight legs, and how far out the feet stand.</summary>
        public const double StandHeight = 1.55 * Size;
        public const double FootReach = 1.75 * Size;
        /// <summary>The middle of the body above the ground when it lies on its belly.</summary>
        public const double BellyHeight = 0.24 * Size;

        public static readonly string[] BodyPieces = { "Carapace", "Belly" };
        public const string Stump = "Stump";
        public const string ClawFinger = "ClawFinger";
        public static readonly string[] Meat = { "Meat0", "Meat1" };
        static readonly string[] LegPieces = { "LegUpper", "LegLower", "LegTip" };
        static readonly string[] ArmPieces = { "ArmUpper", "ArmLower", "Claw" };

        public static int Limb(int side, int place) => side * PerSide + place;
        public static int SideOf(int limb) => limb / PerSide;
        public static int PlaceOf(int limb) => limb % PerSide;
        public static bool IsArm(int limb) => limb % PerSide == 0;
        /// <summary>+1 for the right side, -1 for the left.</summary>
        public static double Sign(int limb) => limb < PerSide ? 1.0 : -1.0;

        public static string Piece(int limb, int segment) => (IsArm(limb) ? ArmPieces : LegPieces)[segment];

        /// <summary>How much larger a limb's pieces are drawn than modelled (the animal's size and the leg's own).</summary>
        public static double Scale(int limb) => Size * (IsArm(limb) ? 1.0 : LegScale[PlaceOf(limb) - 1]);

        /// <summary>A piece's length in the world.</summary>
        public static double Length(int limb, int segment)
        {
            double modelled = IsArm(limb)
                ? (segment == 0 ? ArmUpper : segment == 1 ? ArmLower : Claw)
                : (segment == 0 ? LegUpper : segment == 1 ? LegLower : LegTip);
            return modelled * Scale(limb);
        }

        /// <summary>A piece's thickness in the world, for hitboxes and wounds: the radius at its joint.</summary>
        public static double Radius(int limb, int segment)
        {
            double modelled = IsArm(limb) ? (segment == 0 ? 0.078 : segment == 1 ? 0.074 : 0.070) : (segment == 0 ? 0.066 : segment == 1 ? 0.060 : 0.040);
            return modelled * Scale(limb);
        }

        /// <summary>Where a limb joins the body, in the body's own space at the modelled size (the body's middle is its origin).</summary>
        public static Vec3d HipModelled(int limb)
        {
            int place = PlaceOf(limb);
            return new Vec3d(Sign(limb) * HipX[place], HipY[place], HipZ[place]);
        }

        /// <summary>The level direction a limb points when held straight out from the body.</summary>
        public static Vec3d Out(int limb)
        {
            double a = OutDegrees[PlaceOf(limb)] * Math.PI / 180.0;
            return new Vec3d(Sign(limb) * Math.Cos(a), 0.0, Math.Sin(a));
        }

        /// <summary>Where a walking leg's foot stands when the animal stands still, in strider space.</summary>
        public static Vec3d FootHome(int limb)
        {
            Vec3d hip = HipModelled(limb) * Size;
            Vec3d at = hip + Out(limb) * (FootReach * (IsArm(limb) ? 1.0 : LegScale[PlaceOf(limb) - 1]));
            return new Vec3d(at.X, 0.0, at.Z);
        }

        /// <summary>The limb on the other side at the same place.</summary>
        public static int Opposite(int limb) => (limb + PerSide) % Limbs;
    }
}
