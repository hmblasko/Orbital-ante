using System;

namespace OrbitalAnte.Planets
{
    /// <summary>What the air (or water) is like at one spot around a body.</summary>
    public struct AirSample
    {
        /// <summary>How much of the body's sky colour has replaced the black of space. 0 to 1.</summary>
        public double Sky;
        /// <summary>How deep inside a gas giant's cloud deck this is. 0 clear, 1 in the thick of it.</summary>
        public double Cloud;
        /// <summary>How far you can see, in metres. Very large means no haze at all.</summary>
        public double Visibility;
        /// <summary>How much sunlight is lost on the way down. 0 none, 1 all.</summary>
        public double SunDimming;
        /// <summary>How strongly the surroundings slow things down. 0 is the vacuum of space.</summary>
        public double Drag;
        public bool Underwater;
        /// <summary>Metres below the surface of the sea. 0 when not under water.</summary>
        public double Depth;
        /// <summary>True anywhere inside the body's air, however thin.</summary>
        public bool InAtmosphere;
    }

    /// <summary>
    /// The rules for a body's air: how the sky fades in as you descend, how a gas giant's cloud deck swallows
    /// you and lets you out underneath, and what changes under water. Pure sums, no Unity types.
    /// </summary>
    public static class AtmosphereModel
    {
        public const double ClearVisibility = 1e9;
        /// <summary>How far you can see in the thick of a gas giant's cloud deck, in metres.</summary>
        public const double CloudVisibility = 38;
        /// <summary>Thickness of the layer where the cloud thins out underneath the deck, in metres.</summary>
        public const double CloudFade = 70;
        public const double WaterDrag = 0.9;

        /// <summary>Height above the ground's zero level at which the body's air ends, in metres.</summary>
        public static double Top(PlanetProfile profile, double surfaceRadius, double bodyRadius)
        {
            if (!profile.hasAtmosphere) return 0;
            return profile.cloudDeck ? Math.Max(50, bodyRadius - surfaceRadius) : profile.atmosphereHeight;
        }

        /// <param name="altitude">Height above the ground's zero level (the surface radius), in metres.</param>
        public static AirSample At(PlanetProfile profile, double surfaceRadius, double bodyRadius, double altitude)
        {
            var air = new AirSample { Visibility = ClearVisibility };

            if (profile.hasSea && altitude < profile.seaLevel)
            {
                air.Underwater = true;
                air.Depth = profile.seaLevel - altitude;
                air.InAtmosphere = true;
                air.Sky = 1;
                // It gets murkier and darker the deeper you go.
                double deep = Noise3.Smooth(0, 90, air.Depth);
                air.Visibility = profile.underwaterVisibility * (1 - 0.45 * deep);
                air.SunDimming = Math.Min(0.92, 0.25 + 0.6 * deep + profile.sunDimming * 0.5);
                air.Drag = WaterDrag;
                return air;
            }

            if (!profile.hasAtmosphere) return air;
            double top = Top(profile, surfaceRadius, bodyRadius);

            if (profile.cloudDeck)
            {
                // Haze begins a little above the cloud tops, so the deck closes round you as you sink into it.
                double lead = bodyRadius * 0.04;
                if (altitude >= top + lead) return air;
                air.InAtmosphere = true;
                double cloudBase = Math.Min(profile.cloudBase, top - CloudFade - 10);
                if (altitude >= top) air.Cloud = 1 - (altitude - top) / lead;
                else if (altitude >= cloudBase) air.Cloud = 1;
                else air.Cloud = 1 - Noise3.Smooth(0, CloudFade, cloudBase - altitude);

                bool inside = altitude < top;
                air.Sky = inside ? 1 : air.Cloud;
                air.SunDimming = profile.sunDimming * (inside ? 1 : air.Cloud);
                air.Drag = profile.airDrag * (inside ? 1 : air.Cloud);
                // Blend "one over distance", which is how haze actually thickens.
                double clear = inside ? 1.0 / profile.visibility : 0;
                air.Visibility = 1.0 / Math.Max(1e-9, clear + (1.0 / CloudVisibility - clear) * air.Cloud * air.Cloud);
                return air;
            }

            if (altitude >= top) return air;
            air.InAtmosphere = true;
            double density = 1 - Math.Max(0, altitude) / top;
            density *= Math.Sqrt(density);
            air.Sky = Math.Pow(density, 0.6) * profile.skyOpacity;
            air.SunDimming = profile.sunDimming * density;
            air.Drag = profile.airDrag * density;
            air.Visibility = profile.visibility / Math.Max(0.02, density);
            return air;
        }
    }
}
