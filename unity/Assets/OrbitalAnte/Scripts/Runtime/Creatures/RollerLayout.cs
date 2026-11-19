using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    /// <summary>
    /// Every dimension and piece name of the Roller (placeholder name): an alien from the giant isopod (Matthew,
    /// 17 Nov 2026), 2.4 m long, armoured in seven overlapping plates, on fourteen short legs, that rolls itself into
    /// a ball chest high and bowls at you.
    ///
    /// Creature space: origin on the ground under the middle of it, +Y up, +Z ahead, +X its right. The body is a chain
    /// of nine links from the head to the tail: link 0 is the head, links 1 to 7 the seven segments (each wears a
    /// plate and carries a pair of legs), link 8 the tail fan. A link's piece is modelled from the middle of its own
    /// length on the line of the spine, +Z toward the head, +Y its back. A leg is modelled from its hip, +Z along it,
    /// +Y its upper side. tools/blender/build_creatures.py carries a copy of these numbers; the rule test
    /// <c>RollerModelFile_MatchesTheLayout</c> fails if they drift.
    /// </summary>
    public static class RollerLayout
    {
        public const string Model = "Roller";
        public const string HeadPiece = "Head", SegPiece = "Seg", PlatePiece = "Plate", TailPiece = "Tail", LegPiece = "Leg", Stump = "Stump";
        public static readonly string[] Meat = { "Meat0", "Meat1" };

        public const int Links = 9, Segs = 7, Legs = 14;
        public const int HeadLink = 0, TailLink = 8, MiddleLink = 4;

        public const double HeadLength = 0.30, SegLength = 0.24, TailLength = 0.42;
        /// <summary>How wide and tall each segment is drawn beside the widest (its plate, its soft ring and its legs' hips follow).</summary>
        public static readonly double[] SegScale = { 0.84, 0.94, 1.0, 1.0, 0.96, 0.88, 0.78 };

        /// <summary>The top of a plate above the spine and the bottom of the soft body below it, at a scale of 1.</summary>
        public const double BackHeight = 0.32, BellyDepth = 0.22;
        public const double PlateHalfWidth = 0.56, SegHalfWidth = 0.44;
        /// <summary>A plate reaches back over the next one by this much, like roof tiles.</summary>
        public const double PlateOverlap = 0.07;
        public const double HeadHalfWidth = 0.40, HeadTop = 0.24, TailHalfWidth = 0.42, TailTop = 0.20;

        /// <summary>A hip in its segment's own frame at a scale of 1 (x is mirrored for the left side).</summary>
        public static readonly Vec3d Hip = new Vec3d(0.30, -0.16, 0.0);
        public const double LegLength = 0.36, LegRadius = 0.035;
        /// <summary>The spine's height when it stands on all its legs.</summary>
        public const double StandHeight = 0.44;
        public const double StumpRadius = 0.05;

        /// <summary>Rolled up, the chain bends through this much of a full turn (head and tail just meet).</summary>
        public const double CurlTurns = 0.92;

        public static double Length(int link) => link == HeadLink ? HeadLength : link == TailLink ? TailLength : SegLength;

        public static double TotalLength => HeadLength + Segs * SegLength + TailLength;

        /// <summary>
        /// How far the middle of a link is from the middle of the ball, and the radius of the ball itself (the plates'
        /// backs). The chain is nine straight links bent at eight joints, so this is a many-sided ring's inner radius,
        /// a little less because the links are not all one length.
        /// </summary>
        public static double SpineRadius => 0.95 * (TotalLength / Links) / (2.0 * Math.Tan(Math.PI * CurlTurns / (Links - 1)));
        public static double BallRadius => SpineRadius + BackHeight;

        /// <summary>How much a link's piece is widened and heightened (never lengthened).</summary>
        public static double Scale(int link) => link == HeadLink || link == TailLink ? 1.0 : SegScale[link - 1];

        public static double Top(int link) => link == HeadLink ? HeadTop : link == TailLink ? TailTop : BackHeight * SegScale[link - 1];
        public static double Bottom(int link) => link == HeadLink ? 0.16 : link == TailLink ? 0.08 : BellyDepth * SegScale[link - 1];
        public static double HalfWidth(int link) => link == HeadLink ? HeadHalfWidth : link == TailLink ? TailHalfWidth : PlateHalfWidth * SegScale[link - 1];

        public const int RightSide = 0, LeftSide = 1;
        /// <summary>Legs are numbered segment * 2 + side.</summary>
        public static int Leg(int seg, int side) => seg * 2 + side;
        public static int SegOf(int leg) => leg / 2;
        public static int SideOf(int leg) => leg % 2;
        public static double Sign(int leg) => SideOf(leg) == RightSide ? 1.0 : -1.0;

        /// <summary>A leg's hip in its segment's frame.</summary>
        public static Vec3d HipOf(int leg)
        {
            double s = SegScale[SegOf(leg)];
            return new Vec3d(Sign(leg) * Hip.X * s, Hip.Y * s, Hip.Z);
        }
    }
}
