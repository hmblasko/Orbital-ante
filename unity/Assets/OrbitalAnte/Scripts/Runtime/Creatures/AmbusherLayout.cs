using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    /// <summary>
    /// Every dimension and piece name of the Ambusher (placeholder name): an alien from the bobbit worm (Matthew,
    /// 17 Nov 2026). A worm as thick as a thigh and nearly six metres long that lives down a hole of its own, with
    /// only its head at the mouth of it: two sickle jaws lying open on the ground and five striped feelers standing
    /// up between them.
    ///
    /// Creature space: origin on the ground at the mouth of its burrow, +Y up, +Z the way it is aiming, +X its right.
    /// The body is a chain: node 0 is the head, nodes 1 to 15 the segments. A piece is modelled round its own node on
    /// the line of the body, +Z toward the head, +Y its back.
    /// - A jaw is modelled for the right side from its hinge, +Z along it, curving in toward -X, the same above as
    ///   below, so the left one is the same piece rolled half a turn.
    /// - A feeler is modelled from its root, +Z along it, bending only upward (+Y), so all five are one piece.
    /// - <see cref="RimPiece"/> is the ring of thrown-up ground round the mouth of the burrow, modelled round the
    ///   origin with +Y up.
    /// tools/blender/build_creatures.py carries a copy of these numbers; the rule test
    /// <c>AmbusherModelFile_MatchesTheLayout</c> fails if they drift.
    /// </summary>
    public static class AmbusherLayout
    {
        public const string Model = "Ambusher";
        public const string HeadPiece = "Head", JawPiece = "Jaw", FeelerPiece = "Feeler", SegPiece = "Seg", RimPiece = "Rim", Stump = "Stump";
        public static readonly string[] Meat = { "Meat0", "Meat1" };

        public const int Segs = 15, Jaws = 2, Feelers = 5;
        /// <summary>Nodes of the chain: the head and every segment.</summary>
        public const int Nodes = Segs + 1;
        /// <summary>The distance from one node to the next along the body.</summary>
        public const double Spacing = 0.36;
        public const double HeadLength = 0.50, HeadHalfWidth = 0.20, HeadHalfHeight = 0.15;
        public const double SegHalfWidth = 0.17, SegHalfHeight = 0.15;
        /// <summary>How far the bristles on a segment's flanks stand out past its side.</summary>
        public const double Bristle = 0.07;

        /// <summary>The right jaw's hinge in the head's own frame (x is mirrored for the left side), and a jaw's length.</summary>
        public static readonly Vec3d JawHinge = new Vec3d(0.15, -0.02, 0.14);
        public const double JawLength = 0.70, JawThick = 0.05;
        /// <summary>How far a jaw swings out from shut to as wide as it goes, in radians.</summary>
        public const double JawSwing = 1.45;

        /// <summary>
        /// Where the feelers leave the head: on an arc over its back, this far from its middle line and this far
        /// forward of its node, spread evenly over <see cref="FeelerArc"/> radians either side of straight up.
        /// </summary>
        public const double FeelerRing = 0.13, FeelerAhead = 0.10, FeelerArc = 1.25;
        public const double FeelerLength = 0.80, FeelerRadius = 0.02;

        /// <summary>The ring of ground round the hole: its outer radius, the hole's radius and how high it stands.</summary>
        public const double RimRadius = 0.75, HoleRadius = 0.26, RimHeight = 0.14;
        public const double StumpRadius = 0.05;

        /// <summary>The length of body behind the head's node.</summary>
        public static double Length => Segs * Spacing;

        public const int RightSide = 0, LeftSide = 1;
        public static double Sign(int side) => side == RightSide ? 1.0 : -1.0;

        /// <summary>How wide a segment is drawn beside the widest: the first few behind the head are a little slimmer.</summary>
        public static double Scale(int seg) => seg >= 3 ? 1.0 : 0.82 + 0.06 * seg;

        public static Vec3d HingeOf(int side) => new Vec3d(JawHinge.X * Sign(side), JawHinge.Y, JawHinge.Z);

        /// <summary>The angle of feeler <paramref name="feeler"/> round the head's line, from straight up (its back), toward its right.</summary>
        public static double FeelerAngle(int feeler) => FeelerArc * (feeler - (Feelers - 1) * 0.5) / ((Feelers - 1) * 0.5);

        public static Vec3d FeelerRoot(int feeler)
        {
            double a = FeelerAngle(feeler);
            return new Vec3d(System.Math.Sin(a) * FeelerRing, System.Math.Cos(a) * FeelerRing, FeelerAhead);
        }
    }
}
