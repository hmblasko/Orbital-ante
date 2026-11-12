using OrbitalAnte.Core;

namespace OrbitalAnte.Planets
{
    public enum StampKind { Crater, Cone, Dome, Plateau, Curve, Region }

    /// <summary>The ground at one spot: how high it is, and what any formations there have done to it.</summary>
    public struct TerrainPoint
    {
        /// <summary>Height above the body's surface radius, in metres.</summary>
        public double Height;
        /// <summary>Colour laid over the ground by formations, and how strongly (0 none, 1 completely).</summary>
        public float TintR, TintG, TintB, TintAmount;
        /// <summary>How much formations have smoothed the ordinary hills here. 0 to 1.</summary>
        public double Flatten;
        /// <summary>
        /// The deepest canyon cut here (zero or negative). Kept apart from <see cref="Height"/> so that where
        /// two canyons meet the floor is as deep as the deeper one, not both depths added together.
        /// </summary>
        public double Cut;
    }

    /// <summary>One formation pressed into a body's surface at a particular spot.</summary>
    public sealed class Stamp
    {
        public Formation Formation;
        public StampKind Kind;
        /// <summary>Unit direction from the body's centre to the middle of the stamp.</summary>
        public Vec3d Center;
        /// <summary>Two directions along the ground at the centre, at right angles to each other.</summary>
        public Vec3d U, V;
        /// <summary>Radius in metres (half the width, for curves).</summary>
        public double Size;
        /// <summary>Beyond this distance from the centre (or the centre line) the stamp does nothing.</summary>
        public double Extent;
        /// <summary>Beyond this straight-line distance from <see cref="Center"/> the stamp does nothing. For quick skipping.</summary>
        public double BoundRadius;
        /// <summary>Main height in metres. For craters this is the depth.</summary>
        public double Height;
        public uint Seed;

        // Curves only.
        public Vec3d Start;
        public Vec3d Normal;
        public Vec3d Along;
        public double ArcAngle;
        public double Length;
        public double MeanderAmount;
        /// <summary>How far the whole line bows away from dead straight, in metres.</summary>
        public double Bow;

        public float TintR, TintG, TintB;
        public float TintStrength;
    }

    /// <summary>One ready-made shape stood on (or floating on, or hidden inside) a body.</summary>
    public struct Placement
    {
        public Vec3d Direction;
        /// <summary>True when <see cref="Position"/> is given outright (inside a cave) and the ground height is not used.</summary>
        public bool Absolute;
        /// <summary>Position measured from the body's centre, in metres. Only when <see cref="Absolute"/>.</summary>
        public Vec3d Position;
        public FeatureKind Kind;
        public int Variant;
        /// <summary>Height in metres.</summary>
        public double Scale;
        /// <summary>Turn about its own upright axis, in radians.</summary>
        public double Yaw;
        public float R, G, B;
        public bool Floats;
    }

    /// <summary>A pool of liquid sitting in a hollow, level with the horizon.</summary>
    public sealed class Pond
    {
        public Vec3d Direction;
        /// <summary>Height of the liquid's surface above the body's surface radius, in metres.</summary>
        public double Level;
        public double Radius;
        public Liquid Liquid;
        public string Formation;
    }

    /// <summary>A place where something rises from the ground: steam, a geyser, volcanic smoke.</summary>
    public sealed class PlumeSite
    {
        public Vec3d Direction;
        public double Height;
        public PlumeKind Kind;
        public string Formation;
    }

    /// <summary>A tunnel under the ground, described as a string of circles from one end to the other.</summary>
    public sealed class Cave
    {
        /// <summary>Centre of each circle, measured from the body's centre, in metres.</summary>
        public Vec3d[] Centres;
        public double[] Radii;
        /// <summary>Unit directions to each opening (one or two).</summary>
        public Vec3d[] Mouths;
        public double MouthRadius;
        public float R, G, B;
        public string Formation;
    }

    /// <summary>A hole cut in the ground so a cave can be entered.</summary>
    public struct TerrainHole
    {
        public Vec3d Direction;
        public double Radius;
    }
}
