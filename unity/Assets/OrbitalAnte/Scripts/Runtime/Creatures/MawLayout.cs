using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    /// <summary>
    /// Every dimension and piece name of the Drifting maw (placeholder name; "Maw" in code): an alien from the megamouth
    /// shark (Matthew, 17 Nov 2026), a little over life size: 7 m of soft dark body behind a huge round head whose
    /// whole front is mouth, pale rubbery lips, small eyes, two long front fins, a back fin, and a tail fin with a
    /// long upper blade. The first creature that swims.
    ///
    /// Creature space: origin at its head's node, +Y up (away from the world's middle), +Z ahead, +X its right; the
    /// creature's root only ever turns about +Y, and how it noses up and down is in the pose. The body is a chain:
    /// node 0 is the head, nodes 1 to <see cref="Segs"/> the lengths behind it. The body is one outline cut at its
    /// joints, so every length has a piece of its own, modelled round its own node, +Z toward the head, +Y its back,
    /// each end rounded into its neighbour. A fin is modelled from its root, +Z along it, flat, the same above as
    /// below, so the left front fin is the right one turned over. Nothing is mirrored.
    /// tools/blender/build_creatures.py carries a copy of these numbers; the rule test
    /// <c>MawModelFile_MatchesTheLayout</c> fails if they drift.
    /// </summary>
    public static class MawLayout
    {
        public const string Model = "Maw";
        public const string HeadPiece = "Head", JawPiece = "Jaw", SegPiece = "Seg", FinPiece = "Fin", BackFinPiece = "BackFin", TailFinPiece = "TailFin", Stump = "Stump";
        public static readonly string[] Meat = { "Meat0", "Meat1" };
        /// <summary>"Seg0" to "Seg7": one piece a length, drawn at its modelled size.</summary>
        public static string PieceOf(int seg) => SegPiece + seg;

        public const int Segs = 8;
        /// <summary>Nodes of the chain: the head and every length.</summary>
        public const int Nodes = Segs + 1;
        /// <summary>The distance from one node to the next along the body.</summary>
        public const double Spacing = 0.70;
        /// <summary>The head runs from half a spacing behind its node to the rest in front.</summary>
        public const double HeadLength = 1.90, HeadHalfWidth = 0.78, HeadHalfHeight = 0.66;
        /// <summary>Half the width and half the height of each length at its middle. The model's outline passes through these; hitboxes and wounds are sized by them.</summary>
        public static readonly double[] HalfWidth = { 0.70, 0.66, 0.58, 0.47, 0.36, 0.26, 0.19, 0.14 };
        public static readonly double[] HalfHeight = { 0.64, 0.62, 0.56, 0.47, 0.37, 0.28, 0.21, 0.17 };

        public static double Length => HeadLength - Spacing * 0.5 + Segs * Spacing;

        /// <summary>
        /// The lower jaw hinges here in the head's own frame and drops open. Modelled from its hinge, +Z along it,
        /// the same either side.
        /// </summary>
        public static readonly Vec3d JawHinge = new Vec3d(0.0, -0.20, 0.10);
        public const double JawLength = 1.38, JawHalfWidth = 0.70;
        /// <summary>Radians the jaw drops from shut to as wide as it goes.</summary>
        public const double JawSwing = 0.78;
        /// <summary>The middle of its open mouth in the head's own frame: what it pulls toward, and what a bite is measured from.</summary>
        public static readonly Vec3d Mouth = new Vec3d(0.0, -0.16, 1.30);
        /// <summary>How far from <see cref="Mouth"/> a hit counts as down its throat while its mouth is wide.</summary>
        public const double ThroatRadius = 0.60;

        public const int Fins = 2;
        public const int RightSide = 0, LeftSide = 1;
        public static double Sign(int side) => side == RightSide ? 1.0 : -1.0;
        /// <summary>The length the front fins are on (0 is the one behind the head), and where a fin's root is in that length's frame (x is mirrored for the left).</summary>
        public const int FinSeg = 0;
        public static readonly Vec3d FinRoot = new Vec3d(0.60, -0.30, 0.10);
        public const double FinLength = 1.45, FinChord = 0.55;
        public static Vec3d FinRootOf(int side) => new Vec3d(Sign(side) * FinRoot.X, FinRoot.Y, FinRoot.Z);

        /// <summary>The back fin stands on this length, its root here in that length's frame. Modelled from its root, +Y up, +Z ahead.</summary>
        public const int BackFinSeg = 2;
        public static readonly Vec3d BackFinRoot = new Vec3d(0.0, 0.50, 0.10);
        public const double BackFinHeight = 0.62, BackFinChord = 0.70;

        /// <summary>The tail fin is on the last length, its root at that length's rear joint. Modelled from its root, +Z toward the head, the long blade up and back.</summary>
        public static readonly Vec3d TailFinRoot = new Vec3d(0.0, 0.0, -Spacing * 0.5);
        public const double TailFinUp = 1.55, TailFinDown = 0.60, TailFinBack = 1.35;

        public const double StumpRadius = 0.16;
        /// <summary>A length's ends are round and reach into its neighbours: a wound at a cut sits this far beyond the joint.</summary>
        public const double WoundOut = 0.10;
        public static double WoundSize(int seg) => HalfWidth[seg] * 0.74;
    }
}
