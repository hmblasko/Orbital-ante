using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    /// <summary>The pieces of a Blind stalker that can be hit: its skull, a length of body or tail, a leg or a gill.</summary>
    public enum StalkerPiece { Head, Seg, Leg, Gill }

    /// <summary>
    /// Every dimension and piece name of the Blind stalker (placeholder name; "Stalker" in code): an alien from the olm,
    /// the blind cave salamander (Matthew, 17 Nov 2026), at about fifteen times life size: 4.3 m long, a pale eel of a
    /// body carried low on four thin legs, a flat pear-shaped head with no eyes, a tuft of red gills behind each
    /// cheek, and a finned tail.
    ///
    /// Creature space: origin on the ground under its head, +Y up, +Z ahead, +X its right. The body is a chain: node 0
    /// is the head, nodes 1 to <see cref="Trunk"/> the trunk, the rest the tail. A piece is modelled round its own
    /// node on the line of the spine, +Z toward the head, +Y its back. A leg's two pieces are modelled from their own
    /// joints, +Z along, bending only upward (+Y), so they are the same either side; so is a gill. Nothing is mirrored.
    /// tools/blender/build_creatures.py carries a copy of these numbers; the rule test
    /// <c>StalkerModelFile_MatchesTheLayout</c> fails if they drift.
    /// </summary>
    public static class StalkerLayout
    {
        public const string Model = "Stalker";
        public const string HeadPiece = "Head", JawPiece = "Jaw", SegPiece = "Seg", TailPiece = "Tail", LegPiece = "Leg", ShinPiece = "Shin", GillPiece = "Gill", Stump = "Stump";
        public static readonly string[] Meat = { "Meat0", "Meat1" };
        /// <summary>
        /// The body is one shape, cut at its joints: every length has a piece of its own ("Seg0" to "Seg6", "Tail0" to
        /// "Tail3"), drawn at its modelled size, so the animal has one outline from neck to tail tip and not a row
        /// of like parts.
        /// </summary>
        public static string PieceOf(int seg) => IsTail(seg) ? TailPiece + (seg - Trunk) : SegPiece + seg;

        /// <summary>Lengths of trunk, and of tail behind them.</summary>
        public const int Trunk = 7, TailSegs = 4, Segs = Trunk + TailSegs;
        /// <summary>Nodes of the chain: the head and every length.</summary>
        public const int Nodes = Segs + 1;
        /// <summary>The distance from one node to the next along the body.</summary>
        public const double Spacing = 0.34;
        /// <summary>The head runs from half a spacing behind its node to the rest in front.</summary>
        public const double HeadLength = 0.62, HeadHalfWidth = 0.20, HeadHalfHeight = 0.11;
        public const double SegHalfWidth = 0.17, SegHalfHeight = 0.16;
        /// <summary>A length of tail is narrow and tall: a fin above and below.</summary>
        public const double TailHalfWidth = 0.09, TailHalfHeight = 0.21;
        /// <summary>How high the spine is carried when it walks.</summary>
        public const double RideHeight = 0.36;

        public const int Legs = 4, Gills = 2;
        /// <summary>The trunk lengths the front and the hind legs are on (0 is the one behind the head).</summary>
        public static readonly int[] LegSeg = { 1, 5 };
        /// <summary>A hip in its length's own frame (x is mirrored for the left side).</summary>
        public static readonly Vec3d Hip = new Vec3d(0.14, -0.07, 0.0);
        public const double LegLength = 0.34, ShinLength = 0.38, LegRadius = 0.045;
        /// <summary>Where a gill leaves the head, in the head's own frame (x is mirrored for the left side).</summary>
        public static readonly Vec3d GillRoot = new Vec3d(0.16, 0.03, -0.10);
        public const double GillLength = 0.36, GillSpread = 0.13;
        /// <summary>
        /// The lower jaw hinges here in the head's own frame and drops open (it is part of the head: a hit on it is a
        /// hit on the skull). Modelled from its hinge, +Z along it, the same either side.
        /// </summary>
        public static readonly Vec3d JawHinge = new Vec3d(0.0, -0.035, 0.0);
        public const double JawLength = 0.40, JawSwing = 0.75;
        /// <summary>Where its mouth is, ahead of the head's node: what a bite is measured from.</summary>
        public const double Mouth = 0.36;
        public const double StumpRadius = 0.05;

        public static double Length => HeadLength - Spacing * 0.5 + Segs * Spacing;

        public const int RightSide = 0, LeftSide = 1;
        /// <summary>Legs are numbered pair * 2 + side; pair 0 is the front.</summary>
        public static int Leg(int pair, int side) => pair * 2 + side;
        public static int PairOf(int leg) => leg / 2;
        public static int SideOf(int leg) => leg % 2;
        public static double Sign(int legOrSide) => legOrSide % 2 == RightSide ? 1.0 : -1.0;
        /// <summary>The node a leg hangs from.</summary>
        public static int NodeOf(int leg) => LegSeg[PairOf(leg)] + 1;
        public static bool IsTail(int seg) => seg >= Trunk;

        /// <summary>How wide a length is at its middle, as a share of SegHalfWidth (TailHalfWidth for the tail): the neck and the root of the tail are slimmer, the tail thins to its tip. The model follows these; hitboxes, hips and wounds are sized by them.</summary>
        public static double Scale(int seg)
        {
            if (seg == 0) return 0.86;
            if (seg < Trunk - 1) return 1.0;
            if (seg == Trunk - 1) return 0.88;
            return 1.0 - 0.17 * (seg - Trunk);
        }

        /// <summary>How tall a length is at its middle, as a share of SegHalfHeight (TailHalfHeight for the tail). The trunk is as tall as it is wide; the tail's fin rises from the trunk's height, is tallest a length or two back and falls to the tip.</summary>
        public static double Tall(int seg)
        {
            if (seg < Trunk) return Scale(seg);
            int k = seg - Trunk;
            return k == 0 ? 0.70 : k == 1 ? 0.90 : k == 2 ? 0.82 : 0.60;
        }

        /// <summary>A length's ends are round and reach into its neighbours: a wound at a cut sits this far beyond the joint, so it covers the end.</summary>
        public const double WoundOut = 0.062;
        /// <summary>How big the wound at a cut through this length is.</summary>
        public static double WoundSize(int seg) => IsTail(seg) ? TailHalfHeight * Tall(seg) * 0.72 : SegHalfWidth * Scale(seg) * 0.74;

        public static Vec3d HipOf(int leg)
        {
            double s = Scale(LegSeg[PairOf(leg)]);
            return new Vec3d(Sign(leg) * Hip.X * s, Hip.Y, Hip.Z);
        }

        public static Vec3d GillRootOf(int side) => new Vec3d(Sign(side) * GillRoot.X, GillRoot.Y, GillRoot.Z);
    }
}
