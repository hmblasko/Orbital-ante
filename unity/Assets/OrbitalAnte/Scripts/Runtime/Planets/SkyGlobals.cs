using UnityEngine;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// The handful of numbers every planet shader shares: where the Sun is, how bright it and the surroundings
    /// are right now, the colour of the sky you are inside (the "veil" far-off things fade into), and the lamp.
    /// Set once a frame; read by all four shaders.
    /// </summary>
    public static class SkyGlobals
    {
        public static readonly Color SunColor = new Color(1f, 0.96f, 0.88f);
        public const float SunIntensity = 1.35f;
        /// <summary>A little fill light, so the night side of a planet is dark but not a black hole.</summary>
        public static readonly Color SpaceAmbient = new Color(0.07f, 0.075f, 0.1f);

        static readonly int SunPositionId = Shader.PropertyToID("_OA_SunPosition");
        static readonly int SunColorId = Shader.PropertyToID("_OA_SunColor");
        static readonly int SunRawId = Shader.PropertyToID("_OA_SunRaw");
        static readonly int AmbientId = Shader.PropertyToID("_OA_Ambient");
        static readonly int SpaceAmbientId = Shader.PropertyToID("_OA_SpaceAmbient");
        static readonly int VeilId = Shader.PropertyToID("_OA_Veil");
        static readonly int LampPositionId = Shader.PropertyToID("_OA_LampPosition");
        static readonly int LampColorId = Shader.PropertyToID("_OA_LampColor");

        /// <summary>Open space: full sunlight, a touch of fill light, no sky, no lamp.</summary>
        public static void Reset()
        {
            Shader.SetGlobalVector(SunRawId, Linear(SunColor, SunIntensity));
            Shader.SetGlobalVector(SpaceAmbientId, Linear(SpaceAmbient, 1f));
            SetAir(SunColor * SunIntensity, SpaceAmbient, Color.black, 0f);
            SetLamp(Vector3.zero, 1f, Color.black);
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
