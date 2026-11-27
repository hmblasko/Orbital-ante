using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    /// <summary>How a walker hurts: a claw thrown from one arm, or a lunge of the whole body that ends in a bite.</summary>
    public enum WalkerAttack
    {
        Claw,
        Lunge,
    }

    /// <summary>
    /// Every dimension of one kind of walker: an alien creature that goes on eight legs with two arms in front
    /// (Matthew, 17 Nov 2026: alien assets "that look realistic", from the most unusual real animals, that "gib and
    /// lose limbs"). The kinds are <see cref="Strider"/> (from the Japanese spider crab) and <see cref="Sprinter"/>
    /// (from the camel spider). No Unity types.
    ///
    /// Walker space: origin on the ground under the middle of the body, +Y up, +Z the way it faces, +X its right.
    /// Its ten limbs are numbered side * 5 + place: place 0 is the arm, 1 to 4 the walking legs from the front back.
    /// Each limb is three pieces from the body out, and each piece is modelled in its own space with its joint at the
    /// origin, +Z along the piece and +Y the side that faces up when the limb is held level.
    ///
    /// tools/blender/build_creatures.py carries its own copy of each kind's numbers and writes them into the model as
    /// sockets; the rule tests ...ModelFile_MatchesTheLayout fail if the two drift.
    /// </summary>
    public sealed class WalkerLayout
    {
        // ---- what every walker shares
        public const int Right = 0;
        public const int Left = 1;
        public const int PerSide = 5;
        public const int Limbs = 10;
        public const int Segments = 3;
        public const int WalkingLegs = 8;

        public static int Limb(int side, int place) => side * PerSide + place;
        public static int SideOf(int limb) => limb / PerSide;
        public static int PlaceOf(int limb) => limb % PerSide;
        public static bool IsArm(int limb) => limb % PerSide == 0;
        /// <summary>+1 for the right side, -1 for the left.</summary>
        public static double Sign(int limb) => limb < PerSide ? 1.0 : -1.0;
        /// <summary>The limb on the other side at the same place.</summary>
        public static int Opposite(int limb) => (limb + PerSide) % Limbs;

        // ---- one kind
        /// <summary>The model's file name, and the kind's (placeholder) name.</summary>
        public string Model;
        /// <summary>The game draws the whole animal this much larger than it is modelled.</summary>
        public double Size = 1.0;

        // Where each right-hand limb joins the body, in the model's own size (x right, y up from the body's middle,
        // z forward), and the way it points when held straight out, in degrees round from the right toward the front.
        public double[] HipX, HipY, HipZ, OutDegrees;

        /// <summary>The modelled lengths of a leg's and an arm's three pieces, and their radius at each joint.</summary>
        public double[] Leg, Arm, LegRadius, ArmRadius;
        /// <summary>How much longer or shorter each walking leg is than the one that is modelled, front to back.</summary>
        public double[] LegScale = { 1.0, 1.0, 1.0, 1.0 };
        /// <summary>The length of the moving finger (of a claw, or of a jaw), and where it turns in its hand's own space.</summary>
        public double Finger;
        public Vec3d FingerHinge;
        /// <summary>The radius the wound is modelled at; it is scaled to the joint it covers.</summary>
        public double StumpRadius = 0.075;

        /// <summary>How high the middle of the body is carried on all eight legs, how far out the feet stand, and the
        /// middle of the body above the ground when it lies on its belly: all in the world's size.</summary>
        public double StandHeight, FootReach, BellyHeight;

        /// <summary>The body's two pieces: the armoured one, then the soft one that is the way to kill it.</summary>
        public string Armoured = "Carapace", Soft = "Belly";
        public string[] LegPieces = { "LegUpper", "LegLower", "LegTip" };
        public string[] ArmPieces = { "ArmUpper", "ArmLower", "Claw" };
        public string FingerPiece = "ClawFinger";
        /// <summary>The model's socket at <see cref="FingerHinge"/>.</summary>
        public string HingeSocket = "ClawHinge";
        public string Stump = "Stump";
        public string[] Meat = { "Meat0", "Meat1" };
        /// <summary>Hitboxes of the two body pieces in the body's own modelled space: centre, then size.</summary>
        public Vec3d ArmouredCentre, ArmouredSize, SoftCentre, SoftSize;

        // ---- how it walks
        /// <summary>How far a foot may be left from where it would like to stand before it steps, and how high it lifts.</summary>
        public double StrideRadius = 0.62, StepHeight = 0.55;
        /// <summary>A step's time standing still and at speed, in seconds, and how much each metre a second shortens it.</summary>
        public double StepSlow = 0.46, StepFast = 0.22, StepQuicken = 0.09;
        /// <summary>The bend between a leg's lower piece and its tip, and between an arm's lower piece and its last.</summary>
        public double TipBend = 0.42, ArmBend = 0.38;
        /// <summary>How much the body dips for each foot in the air, and how far it breathes up and down.</summary>
        public double Dip = 0.06, Breath = 0.035;

        // ---- how it hurts
        public WalkerAttack Attack = WalkerAttack.Claw;
        /// <summary>Where an arm's end is carried: x out to its side, y below the body's middle (never under
        /// <see cref="ArmFloor"/>), z ahead. Then where it is lifted to in the warning (y above the body's middle),
        /// and where it lies when the animal is down.</summary>
        public Vec3d ArmRest, ArmRaised, ArmDown;
        public double ArmFloor = 0.5, ArmSway = 0.12;
        /// <summary>A strike pushes the body this far forward (a lunge is all of this) and tips its nose down this much.</summary>
        public double StrikeShift = 0.25, StrikePitch = 0.10, StrikeDip = 0.10;
        /// <summary>Jaws (a <see cref="WalkerAttack.Lunge"/> kind only): where each is hinged on the armoured piece,
        /// in its modelled space (the right one; the left is mirrored), its piece, length and thickness.</summary>
        public Vec3d JawBase;
        public string JawPiece;
        public double JawLength, JawRadius;
        public bool HasJaws => JawPiece != null;
        /// <summary>Which way the finger opens about its hinge: -1 lifts (a claw's upper finger), +1 drops (a jaw's lower one).</summary>
        public double FingerOpens = -1.0;
        /// <summary>How much quicker than a Strider's the arms feel about.</summary>
        public double ArmQuick = 1.0;
        /// <summary>True for a kind that may have half its feet in the air at once (it runs); otherwise three of eight at most.</summary>
        public bool Trot;
        /// <summary>The soft piece swings from here on the armoured one (modelled space), by this many radians at a run.</summary>
        public Vec3d SoftPivot;
        public double SoftSway;

        /// <summary>Where the points of a lunging kind's jaws are when it stands still, in walker space.</summary>
        public Vec3d BiteRest(double bodyHeight) =>
            new Vec3d(0.0, (bodyHeight > 0.0 ? bodyHeight : StandHeight) + JawBase.Y * Size, (JawBase.Z + JawLength) * Size);

        public string Piece(int limb, int segment) => (IsArm(limb) ? ArmPieces : LegPieces)[segment];

        /// <summary>How much larger a limb's pieces are drawn than modelled (the animal's size and the leg's own).</summary>
        public double Scale(int limb) => Size * (IsArm(limb) ? 1.0 : LegScale[PlaceOf(limb) - 1]);

        /// <summary>A piece's length in the world.</summary>
        public double Length(int limb, int segment) => (IsArm(limb) ? Arm : Leg)[segment] * Scale(limb);

        /// <summary>A piece's thickness in the world, for hitboxes and wounds: the radius at its joint.</summary>
        public double Radius(int limb, int segment) => (IsArm(limb) ? ArmRadius : LegRadius)[segment] * Scale(limb);

        /// <summary>Where a limb joins the body, in the body's own space at the modelled size (the body's middle is its origin).</summary>
        public Vec3d HipModelled(int limb)
        {
            int place = PlaceOf(limb);
            return new Vec3d(Sign(limb) * HipX[place], HipY[place], HipZ[place]);
        }

        /// <summary>The level direction a limb points when held straight out from the body.</summary>
        public Vec3d Out(int limb)
        {
            double a = OutDegrees[PlaceOf(limb)] * Math.PI / 180.0;
            return new Vec3d(Sign(limb) * Math.Cos(a), 0.0, Math.Sin(a));
        }

        /// <summary>Where a walking leg's foot stands when the animal stands still, in walker space.</summary>
        public Vec3d FootHome(int limb)
        {
            Vec3d hip = HipModelled(limb) * Size;
            Vec3d at = hip + Out(limb) * (FootReach * (IsArm(limb) ? 1.0 : LegScale[PlaceOf(limb) - 1]));
            return new Vec3d(at.X, 0.0, at.Z);
        }

        /// <summary>Where a jaw is hinged, in the body's modelled space (side 0 right, 1 left).</summary>
        public Vec3d JawAt(int side) => new Vec3d(side == Right ? JawBase.X : -JawBase.X, JawBase.Y, JawBase.Z);

        /// <summary>
        /// The Strider: the Japanese spider crab at about 7 m across, its body carried 2 m up on stilt legs, two long
        /// claws. Armoured lid on top, soft underneath.
        /// </summary>
        public static readonly WalkerLayout Strider = new WalkerLayout
        {
            Model = "Strider",
            Size = 1.3,
            HipX = new[] { 0.215, 0.335, 0.385, 0.360, 0.265 },
            HipY = new[] { -0.075, -0.080, -0.080, -0.080, -0.075 },
            HipZ = new[] { 0.300, 0.150, -0.020, -0.180, -0.310 },
            OutDegrees = new[] { 64.0, 34.0, 6.0, -24.0, -54.0 },
            Leg = new[] { 1.52, 1.60, 0.56 },
            Arm = new[] { 1.28, 0.368, 1.20 },
            LegRadius = new[] { 0.066, 0.060, 0.040 },
            ArmRadius = new[] { 0.078, 0.074, 0.070 },
            LegScale = new[] { 1.0, 1.06, 1.0, 0.90 },
            Finger = 0.592,
            FingerHinge = new Vec3d(0.0, 0.050, 0.624),
            StumpRadius = 0.075,
            StandHeight = 1.55 * 1.3,
            FootReach = 1.75 * 1.3,
            BellyHeight = 0.24 * 1.3,
            ArmouredCentre = new Vec3d(0, 0.075, 0.01), ArmouredSize = new Vec3d(0.74, 0.21, 0.86),
            SoftCentre = new Vec3d(0, -0.105, 0.0), SoftSize = new Vec3d(0.70, 0.15, 0.80),
            ArmRest = new Vec3d(0.72, 1.35, 2.05), ArmRaised = new Vec3d(1.35, 1.15, 1.05), ArmDown = new Vec3d(0.9, 0.12, 2.3),
        };

        /// <summary>
        /// The Sprinter: the camel spider at 2.6 m long, low and fast, hairy, with two great vertical jaws and a pair
        /// of feeling arms held out in front. Armoured head in front, soft abdomen behind: it is killed from the side
        /// or from behind.
        /// </summary>
        public static readonly WalkerLayout Sprinter = new WalkerLayout
        {
            Model = "Sprinter",
            Size = 1.0,
            HipX = new[] { 0.150, 0.215, 0.235, 0.225, 0.190 },
            HipY = new[] { -0.060, -0.085, -0.085, -0.085, -0.080 },
            HipZ = new[] { 0.560, 0.400, 0.250, 0.100, -0.040 },
            OutDegrees = new[] { 72.0, 50.0, 18.0, -22.0, -52.0 },
            Leg = new[] { 0.55, 0.62, 0.36 },
            Arm = new[] { 0.56, 0.56, 0.32 },
            LegRadius = new[] { 0.058, 0.046, 0.028 },
            ArmRadius = new[] { 0.060, 0.050, 0.040 },
            LegScale = new[] { 0.86, 0.94, 1.06, 1.30 },
            Finger = 0.36,
            FingerHinge = new Vec3d(0.0, -0.036, 0.130),
            StumpRadius = 0.075,
            StandHeight = 0.56,
            FootReach = 1.12,
            BellyHeight = 0.20,
            Armoured = "Head", Soft = "Abdomen",
            ArmPieces = new[] { "PalpUpper", "PalpLower", "PalpTip" },
            FingerPiece = "JawFinger", HingeSocket = "JawHinge",
            ArmouredCentre = new Vec3d(0, 0.02, 0.28), ArmouredSize = new Vec3d(0.58, 0.32, 0.74),
            SoftCentre = new Vec3d(0, 0.0, -0.52), SoftSize = new Vec3d(0.52, 0.40, 1.10),
            StrideRadius = 0.42, StepHeight = 0.16,
            StepSlow = 0.20, StepFast = 0.08, StepQuicken = 0.02,
            TipBend = 0.50, ArmBend = 0.30,
            Dip = 0.012, Breath = 0.012,
            Attack = WalkerAttack.Lunge,
            ArmRest = new Vec3d(0.30, 0.04, 1.42), ArmRaised = new Vec3d(0.62, 0.70, 0.95), ArmDown = new Vec3d(0.45, 0.08, 1.5),
            ArmFloor = 0.10, ArmSway = 0.07, ArmQuick = 3.0,
            FingerOpens = 1.0, Trot = true,
            SoftPivot = new Vec3d(0, 0.0, 0.06), SoftSway = 0.05,
            StrikeShift = 0.85, StrikePitch = -0.16, StrikeDip = 0.06,
            JawBase = new Vec3d(0.105, 0.030, 0.640),
            JawPiece = "Jaw",
            JawLength = 0.52, JawRadius = 0.095,
        };

        /// <summary>The kind whose model has this name, or null.</summary>
        public static WalkerLayout For(string model) => model == Strider.Model ? Strider : model == Sprinter.Model ? Sprinter : null;
    }
}
