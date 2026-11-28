using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    /// <summary>The pieces of a Cave hunter that can be hit: its armoured head, a segment (the first few are its soft fore body) or a leg.</summary>
    public enum HunterPiece { Head, Seg, Leg }

    /// <summary>
    /// Every dimension and piece name of the Cave hunter (placeholder name; "Hunter" in code): an alien from the giant
    /// centipede (Matthew, 17 Nov 2026), six metres long, a flat head with two curved fangs and long feelers, sixteen
    /// body segments each with a pair of legs, and two long trailing legs on the last.
    ///
    /// Creature space: origin on the ground under its head, +Y up, +Z ahead, +X its right. The body is a chain: node 0
    /// is the head, nodes 1 to 16 the segments. A piece is modelled round its own node on the line of the spine, +Z
    /// toward the head, +Y its back. A leg is modelled from its hip, +Z along it, +Y its upper side.
    /// tools/blender/build_creatures.py carries a copy of these numbers; the rule test
    /// <c>HunterModelFile_MatchesTheLayout</c> fails if they drift.
    /// </summary>
    public static class HunterLayout
    {
        public const string Model = "Hunter";
        public const string HeadPiece = "Head", FangPiece = "Fang", FeelerPiece = "Feeler", SegPiece = "Seg", LegPiece = "Leg", TailPiece = "TailLeg", Stump = "Stump";
        public static readonly string[] Meat = { "Meat0", "Meat1" };

        public const int Segs = 16, Legs = Segs * 2;
        /// <summary>The distance from one node to the next along the body.</summary>
        public const double Spacing = 0.34;
        public const double HeadLength = 0.46, HeadHalfWidth = 0.23, SegHalfWidth = 0.21, SegTop = 0.09, SegBottom = 0.09;
        /// <summary>How high the spine is carried when it runs.</summary>
        public const double RideHeight = 0.30;
        /// <summary>A hip in its segment's own frame (x is mirrored for the left side).</summary>
        public static readonly Vec3d Hip = new Vec3d(0.19, -0.04, 0.0);
        public const double LegLength = 0.52, LegRadius = 0.03;
        public const double TailLegLength = 0.85;
        public const double StumpRadius = 0.05;
        /// <summary>Where a fang and a feeler leave the head, in the head's own frame (x is mirrored for the left side).</summary>
        public static readonly Vec3d FangRoot = new Vec3d(0.10, -0.05, 0.16);
        public static readonly Vec3d FeelerRoot = new Vec3d(0.10, 0.05, 0.24);
        /// <summary>
        /// A fang is modelled for the right side, +Z along it, curving in toward -X, the same above as below, so the
        /// left one is the same piece rolled half a turn. A feeler, a leg and a tail leg bend only upward (+Y), so they
        /// are the same either side.
        /// </summary>
        public const double FangLength = 0.30, FeelerLength = 0.95;
        /// <summary>Where a tail leg leaves the last segment.</summary>
        public static readonly Vec3d TailRoot = new Vec3d(0.10, 0.0, -0.14);
        /// <summary>Nodes of the chain: the head and every segment.</summary>
        public const int Nodes = Segs + 1;

        public static double Length => HeadLength * 0.5 + Segs * Spacing;

        /// <summary>How wide a segment is drawn beside the widest: it narrows a little to the tail.</summary>
        public static double Scale(int seg) => seg < 10 ? 1.0 : 1.0 - 0.035 * (seg - 9);

        public const int RightSide = 0, LeftSide = 1;
        /// <summary>Legs are numbered segment * 2 + side.</summary>
        public static int Leg(int seg, int side) => seg * 2 + side;
        public static int SegOf(int leg) => leg / 2;
        public static int SideOf(int leg) => leg % 2;
        public static double Sign(int leg) => SideOf(leg) == RightSide ? 1.0 : -1.0;

        public static Vec3d HipOf(int leg)
        {
            double s = Scale(SegOf(leg));
            return new Vec3d(Sign(leg) * Hip.X * s, Hip.Y, Hip.Z);
        }
    }
}
