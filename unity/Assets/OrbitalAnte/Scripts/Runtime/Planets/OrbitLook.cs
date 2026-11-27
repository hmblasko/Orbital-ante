using System;
using OrbitalAnte.Core;

namespace OrbitalAnte.Planets
{
    /// <summary>How a body's air shows from space. All lengths are in the body's own radii.</summary>
    public struct AirLook
    {
        /// <summary>How much air a look straight down crosses: 0 none, 0.08 a thick sky.</summary>
        public double Depth;
        /// <summary>The height over which the air thins to about a third.</summary>
        public double Scale;
        /// <summary>How much the air hides what is under it (1 on a gas giant, whose edge darkens).</summary>
        public double Hide;
        /// <summary>How bright the air's own colour is drawn.</summary>
        public double Gain;
        /// <summary>The ball the air is drawn on: large enough that the air has thinned to nothing at its edge.</summary>
        public double Shell;
        /// <summary>How soft the edge of the night is: 0.008 with no air (only the Sun's width), up to 0.08.</summary>
        public double Soft;
        /// <summary>How much low sunlight loses of its red, green and blue.</summary>
        public double ReddenR, ReddenG, ReddenB;
    }

    /// <summary>A layer of cloud drawn round a body seen from space.</summary>
    public struct CloudLook
    {
        /// <summary>The picture in Resources (colour, and alpha how much cloud).</summary>
        public string Picture;
        /// <summary>How thick the picture's cloud is taken to be, 0 to 1.</summary>
        public double Amount;
        /// <summary>How high the layer floats, as a share of the way from the ground to the top of the air.</summary>
        public double Height;
        /// <summary>How dark the clouds' shadows on the far ball are, 0 to 1.</summary>
        public double Shadow;
        /// <summary>Seconds for the clouds to drift once round the body (0: they stay put).</summary>
        public double DriftSeconds;
    }

    /// <summary>
    /// The numbers behind a planet's look from space (Matthew, 26 Nov 2026: "Build all as shown"): how thick and
    /// how tall its air shows, how soft the edge of its night is, which worlds wear clouds and which pictures the
    /// gas giants' cloud tops are. Plain C#, no Unity types. The pictures are painted by
    /// <c>tools/planet_pictures/bake_planets.py</c>; what draws them is OA_Body, OA_Air, OA_Clouds and OA_Rings.
    /// </summary>
    public static class OrbitLook
    {
        public const string Folder = "Planets/";
        /// <summary>The rings' strip of picture: inner edge to outer, alpha how much they hide.</summary>
        public const string RingPicture = Folder + "SaturnRings";
        /// <summary>How dark the edge of the night is with no air at all: only the Sun's own width softens it.</summary>
        public const double AirlessSoft = 0.008;

        /// <summary>The air of a body as seen from space. <paramref name="interior"/> is a gas giant.</summary>
        public static AirLook Air(PlanetProfile profile, double bodyRadius, bool interior)
        {
            var look = new AirLook { Soft = AirlessSoft, Shell = 1 };
            if (profile == null || !profile.hasAtmosphere || bodyRadius <= 0) return look;
            double opacity = Math.Max(0, Math.Min(1, profile.skyOpacity));
            if (interior)
            {
                // Cloud tops under a little clear gas: the edge of the disc darkens, as the real giants' do.
                look.Depth = 0.05;
                look.Scale = 0.012;
                look.Hide = 1;
                look.Gain = 0.35;
                look.Soft = 0.06;
            }
            else
            {
                look.Depth = 0.08 * Math.Pow(opacity, 1.6);
                look.Scale = Math.Max(0.01, Math.Min(0.035, profile.atmosphereHeight / bodyRadius * 0.05));
                look.Hide = 0.55;
                look.Gain = 0.5 + 0.38 * opacity;
                look.Soft = 0.02 + 0.06 * Math.Max(0, Math.Min(1, (opacity - 0.9) * 10));
            }
            look.Shell = 1 + Math.Max(0.02, look.Scale * 7);
            // Blue skies take the blue out of low sunlight fastest; dusty and hazy ones take a little of everything.
            float[] sky = ColorUtil.Parse(profile.skyColor);
            bool blue = sky[2] > sky[0];
            look.ReddenR = (blue ? 0.012 : 0.02) * opacity;
            look.ReddenG = 0.03 * opacity;
            look.ReddenB = (blue ? 0.075 : 0.045) * opacity;
            return look;
        }

        /// <summary>The cloud layer a body wears from space, if it has one.</summary>
        public static bool TryClouds(string bodyId, out CloudLook clouds)
        {
            switch (bodyId)
            {
                case "Earth":
                    clouds = new CloudLook { Picture = Folder + "EarthClouds", Amount = 0.95, Height = 0.2, Shadow = 0.55, DriftSeconds = 2400 };
                    return true;
                case "Mars":
                    clouds = new CloudLook { Picture = Folder + "MarsClouds", Amount = 0.16, Height = 0.25, Shadow = 0.1, DriftSeconds = 3600 };
                    return true;
                case "Venus":
                    // An unbroken deck: the ground cannot be seen until you fly down through it.
                    clouds = new CloudLook { Picture = Folder + "VenusClouds", Amount = 1, Height = 0.25, Shadow = 0, DriftSeconds = 900 };
                    return true;
                default:
                    clouds = default;
                    return false;
            }
        }

        /// <summary>The painted cloud tops of a gas giant, or null for any other body.</summary>
        public static string CloudTops(string bodyId)
        {
            switch (bodyId)
            {
                case "Jupiter": return Folder + "JupiterTops";
                case "Saturn": return Folder + "SaturnTops";
                case "Uranus": return Folder + "UranusTops";
                case "Neptune": return Folder + "NeptuneTops";
                default: return null;
            }
        }

        /// <summary>True for the one world with polar lights on its night side.</summary>
        public static bool HasAurora(string bodyId) => bodyId == "Earth";

        /// <summary>
        /// How much of a shell (clouds, air) shows to a viewer this far from the body's centre: all of it from
        /// well above, none by the time they reach it, so it never vanishes in one frame as they pass through.
        /// </summary>
        public static double ShellShown(double viewerRadius, double shellRadius, double fadeOver)
        {
            if (shellRadius <= 0 || fadeOver <= 0) return 1;
            double t = (viewerRadius - shellRadius * 1.02) / (shellRadius * fadeOver);
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            return t * t * (3 - 2 * t);
        }

        /// <summary>
        /// How dark the clouds' shadows on the far ball are for a viewer this high: full from far off, gone by the
        /// height at which the real ground takes the ball's place (the ground does not show them), so nothing pops.
        /// </summary>
        public static double ShadowShown(double height, double nearHeight)
        {
            if (nearHeight <= 0) return 1;
            double t = (height / nearHeight - 1.3) / 0.7;
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            return t * t * (3 - 2 * t);
        }
    }

    /// <summary>
    /// The lie of the land for the far ball: from the heights behind a body's picture, the ground's slope east and
    /// north at every dot, and where the sea is. The far-body shader tilts the light by it, so mountains and crater
    /// rims catch the Sun and throw shade from orbit. Plain C#, safe on a background thread.
    /// </summary>
    public static class FarRelief
    {
        /// <summary>The steepest slope the picture can hold (rise over run); steeper is stored as this.</summary>
        public const double SteepestSlope = 2.0;

        /// <summary>
        /// Red: slope toward the east, green: toward the north (128 is level, see <see cref="Decode"/>), blue: 255
        /// over sea, alpha: the height of the ground (the sea's top over sea) from <paramref name="low"/> (0) to
        /// <paramref name="high"/> (255), which the cloud layer parts round. <paramref name="heights"/> are metres
        /// above the body's radius, bottom row (south) first, <paramref name="width"/> across and half that high.
        /// </summary>
        public static byte[] Paint(float[] heights, int width, double radius, bool hasSea, double seaLevel, out double low, out double high)
        {
            int height = width / 2;
            var pixels = new byte[width * height * 4];
            float sea = (float)seaLevel;
            low = double.MaxValue;
            high = double.MinValue;
            for (int i = 0; i < width * height; i++)
            {
                double level = Level(heights[i], hasSea, sea);
                if (level < low) low = level;
                if (level > high) high = level;
            }
            double span = Math.Max(1e-6, high - low);
            for (int y = 0; y < height; y++)
            {
                double polar = (1 - (y + 0.5) / height) * Math.PI;
                // One dot's width on the ground shrinks toward the poles; never divide by nothing there.
                double eastStep = Math.Max(1e-3, radius * Math.PI * 2 / width * Math.Max(0.05, Math.Sin(polar)));
                double northStep = radius * Math.PI / height;
                int below = Math.Max(0, y - 1), above = Math.Min(height - 1, y + 1);
                for (int x = 0; x < width; x++)
                {
                    int left = (x + width - 1) % width, right = (x + 1) % width;
                    float here = heights[y * width + x];
                    bool wet = hasSea && here < sea;
                    double east = (Level(heights[y * width + right], hasSea, sea) - Level(heights[y * width + left], hasSea, sea)) / (2 * eastStep);
                    double north = (Level(heights[above * width + x], hasSea, sea) - Level(heights[below * width + x], hasSea, sea)) / ((above - below) * northStep);
                    int o = (y * width + x) * 4;
                    pixels[o] = Encode(east);
                    pixels[o + 1] = Encode(north);
                    pixels[o + 2] = (byte)(wet ? 255 : 0);
                    pixels[o + 3] = (byte)Math.Round((Level(here, hasSea, sea) - low) / span * 255);
                }
            }
            return pixels;
        }

        static float Level(float height, bool hasSea, float sea) => hasSea && height < sea ? sea : height;

        public static byte Encode(double slope)
        {
            double t = slope / SteepestSlope * 0.5 + 0.5;
            int i = (int)Math.Round(t * 255);
            return (byte)(i < 0 ? 0 : i > 255 ? 255 : i);
        }

        /// <summary>The slope a stored byte stands for. The shader does the same sum.</summary>
        public static double Decode(byte stored) => (stored / 255.0 * 2 - 1) * SteepestSlope;

        /// <summary>
        /// A small picture of lights on the night side: one soft dot for every direction given, brighter for a
        /// larger <paramref name="sizes"/> entry (0 small to 2 large). One byte a dot, bottom row first.
        /// </summary>
        public static byte[] Lights(Vec3d[] directions, int[] sizes, int count, int width)
        {
            int height = width / 2;
            var pixels = new byte[width * height];
            for (int i = 0; i < count; i++)
            {
                FarMap.Locate(directions[i].Normalized, out double u, out double v);
                int cx = (int)(u * width), cy = (int)(v * height);
                int size = sizes != null && i < sizes.Length ? sizes[i] : 0;
                double peak = 150 + 50 * Math.Max(0, Math.Min(2, size));
                // Wider in the picture toward the poles, so the dot stays round on the ball.
                double stretch = 1 / Math.Max(0.15, Math.Sin((1 - v) * Math.PI));
                int reachX = (int)Math.Ceiling(2 * stretch) + 1;
                for (int dy = -2; dy <= 2; dy++)
                {
                    int y = cy + dy;
                    if (y < 0 || y >= height) continue;
                    for (int dx = -reachX; dx <= reachX; dx++)
                    {
                        int x = ((cx + dx) % width + width) % width;
                        double fx = dx / stretch;
                        double glow = peak * Math.Exp(-(fx * fx + dy * dy) / 1.1);
                        int o = y * width + x;
                        int sum = pixels[o] + (int)glow;
                        pixels[o] = (byte)(sum > 255 ? 255 : sum);
                    }
                }
            }
            return pixels;
        }
    }
}
