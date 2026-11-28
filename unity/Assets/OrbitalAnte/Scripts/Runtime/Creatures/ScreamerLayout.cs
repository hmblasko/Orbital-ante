using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    /// <summary>The pieces of a Screamer that can be hit: its armoured fore body, its abdomen, a drum, a wing or a leg.</summary>
    public enum ScreamerPiece { Body, Abdomen, Drum, Wing, Leg }

    /// <summary>
    /// Every dimension and piece name of the Screamer (placeholder name): an alien from the cicada (Matthew,
    /// 17 Nov 2026), 1.7 m long with wings that span 3.6 m: a broad armoured fore body with wide-set eyes, a hollow
    /// abdomen that is its sounding box, a ribbed drum on each flank of it, four wings and six legs.
    ///
    /// Creature space: origin on the ground under the middle of its fore body, +Y up, +Z ahead, +X its right.
    /// - <see cref="BodyPiece"/> (head and thorax) is modelled round its own middle.
    /// - <see cref="AbdomenPiece"/> is modelled from its joint with the fore body and runs back along -Z.
    /// - A drum is modelled for the right flank, bulging toward +X, the same above as below, so the left one is the
    ///   same piece turned half a turn about Z.
    /// - A wing is modelled from its hinge, +Z along it, flat, its trailing edge toward +X, the same above as below:
    ///   the left wing is the same piece with its upper side down.
    /// - A leg is two pieces (<see cref="LegPiece"/> from the hip, <see cref="ShinPiece"/> from the knee), each +Z
    ///   along it, +Y its upper side, bending only upward, so they are the same either side.
    /// tools/blender/build_creatures.py carries a copy of these numbers; the rule test
    /// <c>ScreamerModelFile_MatchesTheLayout</c> fails if they drift.
    /// </summary>
    public static class ScreamerLayout
    {
        public const string Model = "Screamer";
        public const string BodyPiece = "Body", AbdomenPiece = "Abdomen", DrumPiece = "Drum", ForeWingPiece = "ForeWing", HindWingPiece = "HindWing",
            LegPiece = "Leg", ShinPiece = "Shin", Stump = "Stump";
        public static readonly string[] Meat = { "Meat0", "Meat1" };

        public const int Wings = 4, Legs = 6, Drums = 2;
        public const int RightSide = 0, LeftSide = 1;

        /// <summary>The fore body: from its back end to the front of its head, and its half width and half height.</summary>
        public const double BodyBack = -0.34, BodyFront = 0.52, BodyHalfWidth = 0.30, BodyHalfHeight = 0.24;
        /// <summary>The abdomen's joint, in the fore body's frame, and its length and greatest half width.</summary>
        public static readonly Vec3d AbdomenJoint = new Vec3d(0.0, 0.0, -0.30);
        public const double AbdomenLength = 0.86, AbdomenHalfWidth = 0.25;
        /// <summary>
        /// The middle of the right drum in the abdomen's frame, and its radius. It stands out past the fore body's
        /// side, so it can be seen (and shot) from in front.
        /// </summary>
        public static readonly Vec3d DrumAt = new Vec3d(0.27, 0.02, -0.16);
        public const double DrumRadius = 0.15;

        /// <summary>The right wings' hinges in the fore body's frame (x is mirrored for the left side).</summary>
        public static readonly Vec3d ForeHinge = new Vec3d(0.20, 0.17, 0.10);
        public static readonly Vec3d HindHinge = new Vec3d(0.19, 0.14, -0.12);
        public const double ForeWingLength = 1.62, ForeWingChord = 0.56, HindWingLength = 0.95, HindWingChord = 0.50;

        /// <summary>The right hips in the fore body's frame, front to back (x is mirrored for the left side).</summary>
        public static readonly Vec3d[] Hip =
        {
            new Vec3d(0.15, -0.17, 0.26),
            new Vec3d(0.17, -0.18, 0.04),
            new Vec3d(0.15, -0.17, -0.18),
        };
        /// <summary>Where the right feet stand when it is perched, in creature space.</summary>
        public static readonly Vec3d[] Foot =
        {
            new Vec3d(0.50, 0.0, 0.62),
            new Vec3d(0.66, 0.0, 0.02),
            new Vec3d(0.52, 0.0, -0.56),
        };
        public const double LegLength = 0.40, ShinLength = 0.48, LegRadius = 0.03;
        /// <summary>How high the middle of its fore body is carried when it stands, and when it lies on its belly.</summary>
        public const double StandHeight = 0.44, BellyHeight = 0.25;
        public const double StumpRadius = 0.05;

        /// <summary>Wings are numbered side * 2 + (0 fore, 1 hind); legs side * 3 + place (0 front).</summary>
        public static int Wing(int side, bool hind) => side * 2 + (hind ? 1 : 0);
        public static bool IsHind(int wing) => wing % 2 == 1;
        public static int WingSide(int wing) => wing / 2;
        public static int Leg(int side, int place) => side * 3 + place;
        public static int LegSide(int leg) => leg / 3;
        public static int LegPlace(int leg) => leg % 3;
        public static double Sign(int side) => side == RightSide ? 1.0 : -1.0;

        public static Vec3d HingeOf(int wing)
        {
            Vec3d h = IsHind(wing) ? HindHinge : ForeHinge;
            return new Vec3d(h.X * Sign(WingSide(wing)), h.Y, h.Z);
        }

        public static double WingLength(int wing) => IsHind(wing) ? HindWingLength : ForeWingLength;

        public static Vec3d HipOf(int leg)
        {
            Vec3d h = Hip[LegPlace(leg)];
            return new Vec3d(h.X * Sign(LegSide(leg)), h.Y, h.Z);
        }

        public static Vec3d FootOf(int leg)
        {
            Vec3d f = Foot[LegPlace(leg)];
            return new Vec3d(f.X * Sign(LegSide(leg)), f.Y, f.Z);
        }

        public static Vec3d DrumOf(int side) => new Vec3d(DrumAt.X * Sign(side), DrumAt.Y, DrumAt.Z);
    }
}
