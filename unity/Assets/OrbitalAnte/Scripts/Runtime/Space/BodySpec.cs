using System;

namespace OrbitalAnte.Space
{
    public enum BodyKind
    {
        Star,
        Planet,
        GasGiant,
        Moon,
        Asteroid,
    }

    /// <summary>
    /// The numbers that define one body in the solar system. Distances are metres, times are seconds and angles
    /// are degrees. Sizes and distances are shrunk a great deal from the real thing (Outer Wilds style) so that
    /// travel takes a minute or two, not months.
    ///
    /// Orbit speed is not listed because it is not a choice: it follows from the parent's gravity and the orbit's
    /// size, exactly as in real life. That is what makes "match speed with the planet and stay with it" work.
    /// </summary>
    [Serializable]
    public sealed class BodySpec
    {
        /// <summary>Stable name used by code and saves, for example "Mars".</summary>
        public string id;
        /// <summary>Name shown to the player.</summary>
        public string displayName;
        public BodyKind kind = BodyKind.Planet;
        /// <summary>The id of the body this one orbits. Empty for the Sun.</summary>
        public string parentId = "";

        /// <summary>Radius of the surface, in metres.</summary>
        public double radius = 300;
        /// <summary>Pull at the surface, in metres per second squared. Earth's real value is 9.8.</summary>
        public double surfaceGravity = 5;

        /// <summary>Distance from the centre of the parent, in metres.</summary>
        public double orbitRadius;
        /// <summary>Tilt of the orbit against the flat of the solar system.</summary>
        public double orbitInclination;
        /// <summary>Which way the orbit's tilt faces.</summary>
        public double orbitNode;
        /// <summary>True to go round the other way (Triton does).</summary>
        public bool retrogradeOrbit;

        /// <summary>Length of one full spin, in seconds. 0 means it does not spin.</summary>
        public double dayLength = 600;
        /// <summary>Tilt of the spin axis against the orbit. Earth's is 23.4.</summary>
        public double axialTilt;
        public bool retrogradeSpin;
        /// <summary>True to keep one face toward the parent, like the Moon. Overrides day length and tilt.</summary>
        public bool tidallyLocked;

        /// <summary>Main and secondary surface colours, as web colours such as "#C1440E".</summary>
        public string colorA = "#888888";
        public string colorB = "#666666";
        /// <summary>Outer edge of a ring system, in metres from the centre. 0 means no rings.</summary>
        public double ringOuterRadius;
        public double ringInnerRadius;

        public BodySpec Clone() => (BodySpec)MemberwiseClone();
    }
}
