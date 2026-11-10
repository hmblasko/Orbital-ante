using System.Collections.Generic;

namespace OrbitalAnte.Space
{
    /// <summary>
    /// The built-in solar system: the Sun, eight planets and eleven moons, in the real order with real relative
    /// character (gravity, tilt, which way things spin), shrunk to a playable size. These are first-guess numbers
    /// for tuning; the asset at Resources/SolarSystem/SolarSystem overrides them.
    /// </summary>
    public static class SolarSystemDefaults
    {
        public const string SunId = "Sun";

        public static List<BodySpec> Bodies()
        {
            var list = new List<BodySpec>();

            list.Add(new BodySpec
            {
                id = SunId, displayName = "Sun", kind = BodyKind.Star, radius = 3000, surfaceGravity = 75,
                dayLength = 1800, colorA = "#FFD27A", colorB = "#FF9A2E",
            });

            // A slow turner, like the real one. (Much faster and its spin would make you noticeably lighter at the equator.)
            list.Add(Planet("Mercury", 250, 3.7, 9000, 5.0, 300, 0.03, "#8C8782", "#5E5A57"));
            var venus = Planet("Venus", 450, 8.9, 15000, 3.4, 2400, 2.6, "#E3C27A", "#C99A4B");
            venus.retrogradeSpin = true;
            venus.orbitNode = 76;
            list.Add(venus);
            list.Add(Planet("Earth", 500, 9.8, 22000, 0.0, 600, 23.4, "#2F6DB5", "#3E8E4A"));
            list.Add(Moon("Moon", "Earth", 150, 1.62, 2200, 5.1, "#B9B6AF", "#8A8780"));
            var mars = Planet("Mars", 350, 3.7, 31000, 1.9, 620, 25.2, "#C1440E", "#8E3B1B");
            mars.orbitNode = 49;
            list.Add(mars);
            list.Add(Moon("Phobos", "Mars", 40, 0.30, 800, 1.1, "#7A6E63", "#5B5148"));
            list.Add(Moon("Deimos", "Mars", 30, 0.20, 1300, 1.8, "#9A8F82", "#746A5F"));

            var jupiter = Planet("Jupiter", 1600, 16.0, 60000, 1.3, 300, 3.1, "#D9B38C", "#A8683F");
            jupiter.kind = BodyKind.GasGiant;
            jupiter.orbitNode = 100;
            list.Add(jupiter);
            list.Add(Moon("Io", "Jupiter", 140, 1.80, 3400, 0.1, "#E6D25A", "#C97F2A"));
            list.Add(Moon("Europa", "Jupiter", 120, 1.30, 4400, 0.5, "#D9D2C0", "#A77B55"));
            list.Add(Moon("Ganymede", "Jupiter", 190, 1.40, 5600, 0.2, "#9C9388", "#6F665D"));
            list.Add(Moon("Callisto", "Jupiter", 170, 1.20, 7200, 0.3, "#6B6258", "#4A433C"));

            var saturn = Planet("Saturn", 1300, 10.4, 82000, 2.5, 330, 26.7, "#E3CF9A", "#BFA36B");
            saturn.kind = BodyKind.GasGiant;
            saturn.orbitNode = 113;
            saturn.ringInnerRadius = 1700;
            saturn.ringOuterRadius = 2900;
            list.Add(saturn);
            list.Add(Moon("Enceladus", "Saturn", 60, 0.40, 3400, 0.0, "#F2F5F7", "#C9D6DE"));
            list.Add(Moon("Titan", "Saturn", 190, 1.35, 5000, 0.3, "#D9A441", "#A8742A"));

            var uranus = Planet("Uranus", 900, 8.9, 102000, 0.8, 500, 97.8, "#A6E3E0", "#7CC7C9");
            uranus.kind = BodyKind.GasGiant;
            // The 97.8 degree tilt already makes it roll round on its side "backwards"; no retrograde flag as well.
            uranus.orbitNode = 74;
            list.Add(uranus);
            list.Add(Moon("Titania", "Uranus", 90, 0.40, 3000, 0.1, "#A39A92", "#7B736C"));

            var neptune = Planet("Neptune", 850, 11.2, 120000, 1.8, 480, 28.3, "#3457D5", "#2A3FA0");
            neptune.kind = BodyKind.GasGiant;
            neptune.orbitNode = 131;
            list.Add(neptune);
            var triton = Moon("Triton", "Neptune", 130, 0.78, 2800, 23.0, "#C9B8B0", "#9E8C86");
            triton.retrogradeOrbit = true;
            list.Add(triton);

            return list;
        }

        static BodySpec Planet(string id, double radius, double gravity, double orbit, double inclination,
            double day, double tilt, string colorA, string colorB)
        {
            return new BodySpec
            {
                id = id, displayName = id, kind = BodyKind.Planet, parentId = SunId, radius = radius,
                surfaceGravity = gravity, orbitRadius = orbit, orbitInclination = inclination, dayLength = day,
                axialTilt = tilt, colorA = colorA, colorB = colorB,
            };
        }

        static BodySpec Moon(string id, string parent, double radius, double gravity, double orbit,
            double inclination, string colorA, string colorB)
        {
            return new BodySpec
            {
                id = id, displayName = id, kind = BodyKind.Moon, parentId = parent, radius = radius,
                surfaceGravity = gravity, orbitRadius = orbit, orbitInclination = inclination,
                tidallyLocked = true, dayLength = 0, colorA = colorA, colorB = colorB,
            };
        }
    }

    /// <summary>Settings for the belt of drifting rocks between Mars and Jupiter.</summary>
    [System.Serializable]
    public sealed class AsteroidBeltSpec
    {
        public int count = 110;
        public double innerRadius = 40000;
        public double outerRadius = 44000;
        public double minSize = 10;
        public double maxSize = 70;
        /// <summary>Most the orbits tilt out of the flat of the solar system, in degrees.</summary>
        public double maxInclination = 6;
        // Any faster and a big rock spins hard enough to throw a landed craft off its equator.
        public double minDayLength = 90;
        public double maxDayLength = 240;
    }
}
