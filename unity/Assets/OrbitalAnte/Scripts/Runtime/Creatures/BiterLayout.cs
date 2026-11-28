using OrbitalAnte.Core;

namespace OrbitalAnte.Creatures
{
    /// <summary>The pieces of a Pack biter that can be hit: its head (its weak place), its lower jaw, its body, or a fin.</summary>
    public enum BiterPiece { Head, Jaw, Seg, Fin, BackFin, TailFin }

    /// <summary>
    /// Every dimension and piece name of the Pack biter (placeholder name; "Biter" in code): an alien from the Goliath
    /// tigerfish (Matthew, 17 Nov 2026), a little over life size: 1.9 m of hard, deep-flanked silver body with dark bars,
    /// a blunt head whose jaws carry long dagger teeth that close outside the lips, red-tipped fins, a forked tail.
    /// It hunts in packs (<see cref="BiterPack"/>).
    ///
    /// Creature space is the Drifting maw's (<see cref="MawLayout"/>): origin at its head's node, +Y up, +Z ahead,
    /// +X its right; the body is a chain whose node 0 is the head and nodes 1 to <see cref="Segs"/> the lengths
    /// behind it, one outline cut at its joints, each length a piece of its own modelled round its own node. A fin is
    /// modelled from its root, +Z along it, flat, the same above as below. Nothing is mirrored.
    /// tools/blender/build_creatures.py carries a copy of these numbers; the rule test
    /// <c>BiterModelFile_MatchesTheLayout</c> fails if they drift.
    /// </summary>
    public static class BiterLayout
    {
        public const string Model = "Biter";
        public const string HeadPiece = "Head", JawPiece = "Jaw", SegPiece = "Seg", FinPiece = "Fin", BackFinPiece = "BackFin", TailFinPiece = "TailFin", Stump = "Stump";
        public static readonly string[] Meat = { "Meat0", "Meat1" };
        public static string PieceOf(int seg) => SegPiece + seg;

        public const int Segs = 4;
        public const int Nodes = Segs + 1;
        public const double Spacing = 0.30;
        /// <summary>The head runs from half a spacing behind its node to the rest in front.</summary>
        public const double HeadLength = 0.52, HeadHalfWidth = 0.13, HeadHalfHeight = 0.23;
        /// <summary>Half the width and half the height of each length at its middle: deep and narrow, like a tigerfish.</summary>
        public static readonly double[] HalfWidth = { 0.13, 0.12, 0.095, 0.060 };
        public static readonly double[] HalfHeight = { 0.24, 0.215, 0.165, 0.095 };

        public static double Length => HeadLength - Spacing * 0.5 + Segs * Spacing + TailFinBack;

        /// <summary>The lower jaw hinges here in the head's own frame and drops open.</summary>
        public static readonly Vec3d JawHinge = new Vec3d(0.0, -0.07, 0.06);
        public const double JawLength = 0.31, JawHalfWidth = 0.10;
        /// <summary>Radians the jaw drops from shut to as wide as it goes.</summary>
        public const double JawSwing = 0.85;
        /// <summary>The front of its mouth in the head's own frame: what a bite is measured from.</summary>
        public static readonly Vec3d Mouth = new Vec3d(0.0, -0.06, 0.38);

        public const int Fins = 2;
        public const int RightSide = 0, LeftSide = 1;
        public static double Sign(int side) => side == RightSide ? 1.0 : -1.0;
        /// <summary>The front fins are on the head (node 0; <see cref="FinSeg"/> is -1), low, behind the gill cover, in the head's frame (x is mirrored for the left).</summary>
        public const int FinSeg = -1;
        public static readonly Vec3d FinRoot = new Vec3d(0.10, -0.12, -0.08);
        public const double FinLength = 0.26, FinChord = 0.11;
        public static Vec3d FinRootOf(int side) => new Vec3d(Sign(side) * FinRoot.X, FinRoot.Y, FinRoot.Z);

        /// <summary>The back fin stands on this length. Modelled from its root, +Y up, +Z ahead.</summary>
        public const int BackFinSeg = 0;
        public static readonly Vec3d BackFinRoot = new Vec3d(0.0, 0.22, 0.04);
        public const double BackFinHeight = 0.24, BackFinChord = 0.22;

        /// <summary>The forked tail fin is on the last length, its root at that length's rear joint, +Z toward the head.</summary>
        public static readonly Vec3d TailFinRoot = new Vec3d(0.0, 0.0, -Spacing * 0.5);
        public const double TailFinUp = 0.34, TailFinDown = 0.32, TailFinBack = 0.36;

        public const double StumpRadius = 0.16;
        public const double WoundOut = 0.04;
        public static double WoundSize(int seg) => HalfHeight[seg] * 0.70;
    }
}
