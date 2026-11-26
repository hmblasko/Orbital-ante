using System;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// The sums behind real light on the ground (the Look asset's "Real Light"), kept in plain C# so they can be
    /// tested with no engine. OA_LightAt in OA_Look.hlsl does the same sums for every dot of the picture; if one
    /// changes, change the other. The far ball's light (RealLook in OA_Body.shader) uses the same edge of night
    /// and the same reddening, which is what lets the real ground take over from it without a jump.
    /// </summary>
    public static class GroundLight
    {
        /// <summary>The least a sky's brightness falls to at night, as a share of day.</summary>
        public const double NightLeast = 0.035;
        /// <summary>Under an unbroken deck of cloud it never gets fully dark: the clouds glow faintly.</summary>
        public const double NightLeastUnderDeck = 0.12;
        /// <summary>How much of space's fill light the ground of a world with air keeps (the sky gives the rest).</summary>
        public const double SpaceShareInAir = 0.5;
        /// <summary>Heights, in the body's radii, between which the eye goes from "used to the dark" to "set for sunlight".</summary>
        public const double AdaptFrom = 0.5, AdaptTo = 3;

        static double Smooth(double from, double to, double x)
        {
            double t = (x - from) / (to - from);
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            return t * t * (3 - 2 * t);
        }

        /// <summary>How bright a world's sky is with the Sun this high (1 overhead, 0 on the horizon): 1 by day, <paramref name="least"/> at night.</summary>
        public static double SkyBrightness(double sunUp, double least)
        {
            return least + (1 - least) * Smooth(-0.1, 0.25, sunUp);
        }

        /// <summary>How much of the Sun's light reaches ground where the Sun is this high: the edge of night, as soft as the world's air makes it.</summary>
        public static double Reached(double sunUp, double soft)
        {
            return Smooth(-soft, soft * 2.5, sunUp);
        }

        /// <summary>What is left of one colour of sunlight that arrives this low, having lost <paramref name="redden"/> to the air straight down.</summary>
        public static double Reddened(double sunUp, double redden)
        {
            return Math.Exp(-redden * Math.Min(1 / Math.Max(sunUp + 0.06, 0.03), 14));
        }

        /// <summary>Sunlight on a face: none unless it faces the Sun, and none in something's shadow (<paramref name="open"/> 0).</summary>
        public static double Direct(double facing, double open)
        {
            double lit = (facing + 0.04) / 1.04;
            return (lit < 0 ? 0 : lit > 1 ? 1 : lit) * open;
        }

        /// <summary>
        /// The fill light on a world's own ground where its sky is <paramref name="here"/> bright, given the fill
        /// worked out for the viewer (<paramref name="viewerFill"/>, under a sky <paramref name="viewerBright"/>
        /// bright with <paramref name="skyOver"/> of the sky over them), what a full day's sky gives the ground
        /// (<paramref name="dayFill"/>) and space's own faint light.
        /// </summary>
        public static double Fill(double viewerFill, double viewerBright, double skyOver, double dayFill, double spaceLight, double spaceShare, double here)
        {
            double scaled = viewerFill * Math.Min(here / Math.Max(viewerBright, 0.001), 1) + dayFill * Math.Max(here - viewerBright, 0) * skyOver;
            double own = dayFill * here + spaceLight * spaceShare;
            return own + (scaled - own) * skyOver;
        }

        /// <summary>
        /// How much of its fill night ground keeps: all of it for someone standing on it, whose eyes are used to
        /// the dark, falling to the far ball's share (<paramref name="nightSide"/>) from high above, where the eye
        /// is set for sunlight. So the ground and the ball agree where one takes over from the other.
        /// </summary>
        public static double FarNight(double heightAboveGround, double bodyRadius, double nightSide)
        {
            if (bodyRadius <= 0) return 1;
            double k = Smooth(AdaptFrom, AdaptTo, heightAboveGround / bodyRadius);
            return 1 + (Math.Max(0, Math.Min(1, nightSide)) - 1) * k;
        }
    }

    /// <summary>What the Sun's cast shadows are drawn with (URP's settings for its one shadow-casting light).</summary>
    public static class SunShadowRules
    {
        /// <summary>Two pictures: a sharp one close by, a coarser one out to the full distance.</summary>
        public const int Cascades = 2;
        /// <summary>The share of the distance the sharp picture covers.</summary>
        public const float NearShare = 0.3f;
        /// <summary>Both pictures share one of this size.</summary>
        public const int Resolution = 2048;
        /// <summary>URP's cheapest soft edge (its SoftShadowQuality.Low): four reads of the picture a dot.</summary>
        public const int SoftQuality = 1;
        public const float LeastDistance = 30f, MostDistance = 250f;

        public static float Distance(float asked)
        {
            return asked < LeastDistance ? LeastDistance : asked > MostDistance ? MostDistance : asked;
        }

        /// <summary>How much ground one dot of the sharp picture covers, in metres: what decides how crisp a shadow at your feet is.</summary>
        public static float NearTexel(float distance)
        {
            return 2f * Distance(distance) * NearShare / (Resolution / 2f);
        }
    }
}
