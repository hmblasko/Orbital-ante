using OrbitalAnte.Look;
using UnityEngine;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// The handful of numbers every planet shader shares: where the Sun is, how bright it and the surroundings
    /// are right now, the colour of the sky you are inside (the "veil" far-off things fade into), and the lamp.
    /// Also the sky itself (colour overhead, colour at the horizon, sunset band, glow round the Sun) and the
    /// strength of the painted style. Set once a frame; read by all the game's shaders through OA_Look.hlsl.
    /// </summary>
    public static class SkyGlobals
    {
        public static readonly Color SunColor = new Color(1f, 0.96f, 0.88f);
        public const float SunIntensity = 1.35f;
        /// <summary>A little fill light, so the night side of a planet is dark but not a black hole.</summary>
        public static readonly Color SpaceAmbient = new Color(0.055f, 0.062f, 0.105f);

        static readonly int SunPositionId = Shader.PropertyToID("_OA_SunPosition");
        static readonly int SunColorId = Shader.PropertyToID("_OA_SunColor");
        static readonly int SunRawId = Shader.PropertyToID("_OA_SunRaw");
        static readonly int AmbientId = Shader.PropertyToID("_OA_Ambient");
        static readonly int SpaceAmbientId = Shader.PropertyToID("_OA_SpaceAmbient");
        static readonly int VeilId = Shader.PropertyToID("_OA_Veil");
        static readonly int LampPositionId = Shader.PropertyToID("_OA_LampPosition");
        static readonly int LampColorId = Shader.PropertyToID("_OA_LampColor");
        static readonly int SkyZenithId = Shader.PropertyToID("_OA_SkyZenith");
        static readonly int SkyHorizonId = Shader.PropertyToID("_OA_SkyHorizon");
        static readonly int SunsetId = Shader.PropertyToID("_OA_Sunset");
        static readonly int SunGlowId = Shader.PropertyToID("_OA_SunGlow");
        static readonly int SkyUpId = Shader.PropertyToID("_OA_SkyUp");
        static readonly int HazeId = Shader.PropertyToID("_OA_Haze");

        /// <summary>Open space: full sunlight, a touch of fill light, no sky, no lamp.</summary>
        public static void Reset()
        {
            Shader.SetGlobalVector(SunRawId, Linear(SunColor, SunIntensity));
            Shader.SetGlobalVector(SpaceAmbientId, Linear(SpaceAmbient, 1f));
            SetAir(SunColor * SunIntensity, SpaceAmbient, Color.black, 0f);
            FlareColor = Color.black;
            SetLamp(Vector3.zero, 1f, Color.black);
            SetSky(SkyLook.OpenSpace(1f), Vector3.zero, 0f);
        }

        public static Color ToColor(Rgb c) => new Color(c.R, c.G, c.B, 1f);
        public static Rgb ToRgb(Color c) => new Rgb(c.r, c.g, c.b);

        /// <summary>
        /// The sky the viewer is under, which way is up there (zero in open space), and how thick the haze is
        /// (one over the distance, in metres, at which things have all but faded out; 0 for none).
        /// The strengths of the painted style come from the Look asset.
        /// </summary>
        public static void SetSky(SkyPalette sky, Vector3 up, float hazeThickness)
        {
            LookConfig look = LookConfig.Load();
            Shader.SetGlobalVector(SkyZenithId, Linear(ToColor(sky.Zenith), 1f));
            Shader.SetGlobalVector(SkyHorizonId, Linear(ToColor(sky.Horizon), 1f));
            Shader.SetGlobalVector(SunsetId, Linear(ToColor(sky.Sunset), 1f));
            Vector4 glow = Linear(ToColor(sky.Glow), 1f);
            glow.w = sky.GlowStrength * look.sunGlow;
            Shader.SetGlobalVector(SunGlowId, glow);
            Shader.SetGlobalVector(SkyUpId, new Vector4(up.x, up.y, up.z, sky.SunsetAmount));
            Shader.SetGlobalVector(HazeId, new Vector4(Mathf.Max(0f, hazeThickness), look.paint, look.rimLight, look.bounceLight));
        }

        public static void SetSun(Vector3 position)
        {
            Shader.SetGlobalVector(SunPositionId, new Vector4(position.x, position.y, position.z, 1f));
        }

        /// <summary>
        /// What the surroundings are doing to the light where the viewer is. <paramref name="sunlight"/> is the
        /// sunlight that reaches the ground here; <paramref name="veil"/> is the sky's colour and
        /// <paramref name="veilAmount"/> how completely it hides things beyond the air.
        /// </summary>
        public static void SetAir(Color sunlight, Color ambient, Color veil, float veilAmount)
        {
            Shader.SetGlobalVector(SunColorId, Linear(sunlight, 1f));
            Shader.SetGlobalVector(AmbientId, Linear(ambient, 1f));
            Vector4 v = Linear(veil, 1f);
            v.w = Mathf.Clamp01(veilAmount);
            Shader.SetGlobalVector(VeilId, v);
        }

        /// <summary>
        /// A second light for the ground and everything on it, such as the ship's engines at lift-off. The shaders
        /// know one lamp, so whichever of this and the player's lamp is brighter is the one shown. Set the colour
        /// to black when it is out.
        /// </summary>
        public static Vector3 FlarePosition;
        public static float FlareReach = 1f;
        public static Color FlareColor = Color.black;

        public static void SetLamp(Vector3 position, float reach, Color color)
        {
            Shader.SetGlobalVector(LampPositionId, new Vector4(position.x, position.y, position.z, 1f / Mathf.Max(1f, reach)));
            Shader.SetGlobalVector(LampColorId, Linear(color, 1f));
        }

        /// <summary>
        /// Colours are chosen by eye on a screen. When the project draws in "linear" colour (the default for new
        /// Unity projects) they must be converted before a shader does sums with them, or everything looks washed out.
        /// </summary>
        public static Vector4 Linear(Color color, float multiply)
        {
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                // Above 1 the conversion curve is not defined, so convert the colour and scale afterwards.
                float peak = Mathf.Max(1f, Mathf.Max(color.r, Mathf.Max(color.g, color.b)));
                Color c = new Color(color.r / peak, color.g / peak, color.b / peak).linear;
                multiply *= peak;
                return new Vector4(c.r * multiply, c.g * multiply, c.b * multiply, color.a);
            }
            return new Vector4(color.r * multiply, color.g * multiply, color.b * multiply, color.a);
        }

        /// <summary>The same conversion for a single stored colour value between 0 and 1 (a close, fast approximation).</summary>
        public static float ToLinear(float x)
        {
            return x * (x * (x * 0.305306011f + 0.682171111f) + 0.012522878f);
        }
    }
}
