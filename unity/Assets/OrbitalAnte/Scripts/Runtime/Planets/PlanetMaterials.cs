using System.Collections.Generic;
using OrbitalAnte.Core;
using UnityEngine;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// Hands out the materials planets are drawn with. The game has four small shaders of its own (ground, far-off
    /// bodies, the rim of air, water). If any of them is missing or will not run on this machine, or the
    /// "simple shaders" box is ticked on the Planets asset, Unity's standard shaders are used instead: plainer,
    /// but nothing turns pink.
    /// </summary>
    public static class PlanetMaterials
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int FarMapId = Shader.PropertyToID("_FarMap");
        static readonly int FarBlendId = Shader.PropertyToID("_FarBlend");
        static readonly int SunAmountId = Shader.PropertyToID("_SunAmount");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int OutdoorsId = Shader.PropertyToID("_Outdoors");
        static readonly int EmissionId = Shader.PropertyToID("_Emission");
        static readonly int VeilId = Shader.PropertyToID("_Veil");
        static readonly int TwoSidedId = Shader.PropertyToID("_TwoSided");
        static readonly int DeepColorId = Shader.PropertyToID("_DeepColor");
        static readonly int RippleId = Shader.PropertyToID("_Ripple");
        static readonly int StrengthId = Shader.PropertyToID("_Strength");
        static readonly int LimbId = Shader.PropertyToID("_Limb");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int BoostId = Shader.PropertyToID("_Boost");
        static readonly int PaintId = Shader.PropertyToID("_Paint");
        static readonly int PaintScaleId = Shader.PropertyToID("_PaintScale");
        static readonly int ColorMapId = Shader.PropertyToID("_ColorMap");
        static readonly int NormalMapId = Shader.PropertyToID("_NormalMap");
        static readonly int MaskMapId = Shader.PropertyToID("_MaskMap");
        /// <summary>The model shader's switch for drawing from texture pictures (OA_Model.shader).</summary>
        public const string TexturedKeyword = "_OA_TEXTURED";
        static readonly int SkinColourId = Shader.PropertyToID("_SkinColour");
        static readonly int SkinAccentId = Shader.PropertyToID("_SkinAccent");
        static readonly int SkinSetId = Shader.PropertyToID("_SkinSet");
        static readonly int GroundId = Shader.PropertyToID("_Ground");
        static readonly int BackdropId = Shader.PropertyToID("_Backdrop");
        static readonly int BackdropStrengthId = Shader.PropertyToID("_BackdropStrength");
        static readonly int SunGlareId = Shader.PropertyToID("_SunGlare");
        static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        static readonly int SoftId = Shader.PropertyToID("_Soft");
        static readonly int NightId = Shader.PropertyToID("_Night");
        static readonly int GroundMapId = Shader.PropertyToID("_GroundMap");
        static readonly int GroundVectorId = Shader.PropertyToID("_Ground");
        static readonly int ReddenId = Shader.PropertyToID("_Redden");
        static readonly int ReliefMapId = Shader.PropertyToID("_ReliefMap");
        static readonly int ReliefId = Shader.PropertyToID("_Relief");
        static readonly int LightsMapId = Shader.PropertyToID("_LightsMap");
        static readonly int LightsColorId = Shader.PropertyToID("_LightsColor");
        static readonly int CloudMapId = Shader.PropertyToID("_CloudMap");
        static readonly int CloudShadowId = Shader.PropertyToID("_CloudShadow");
        static readonly int AuroraId = Shader.PropertyToID("_Aurora");
        static readonly int RingMapId = Shader.PropertyToID("_RingMap");
        static readonly int RingRadiiId = Shader.PropertyToID("_RingRadii");
        static readonly int DepthId = Shader.PropertyToID("_Depth");
        static readonly int ScaleId = Shader.PropertyToID("_Scale");
        static readonly int HideId = Shader.PropertyToID("_Hide");
        static readonly int ShellId = Shader.PropertyToID("_Shell");
        static readonly int AmountId = Shader.PropertyToID("_Amount");
        static readonly int DriftId = Shader.PropertyToID("_Drift");
        static readonly int PlanetRadiusId = Shader.PropertyToID("_PlanetRadius");
        const string RealKeyword = "_OA_REAL";
        const string ReliefKeyword = "_OA_RELIEF";

        static bool _looked;
        static bool _simple;
        static Shader _terrain, _body, _atmosphere, _water, _sky, _model, _stars, _air, _clouds, _rings;
        static bool _realPlanets;
        static Material _vertexLit, _vertexUnlit;
        static readonly Dictionary<long, Material> VertexCache = new Dictionary<long, Material>();
        static readonly Dictionary<int, Material> ModelCache = new Dictionary<int, Material>();
        static readonly Dictionary<string, Material> TexturedCache = new Dictionary<string, Material>();
        /// <summary>Skinned copies of model materials, by the material they copy and the skin's own key.</summary>
        static readonly Dictionary<SkinKey, Material> SkinCache = new Dictionary<SkinKey, Material>();

        readonly struct SkinKey : System.IEquatable<SkinKey>
        {
            readonly Material _source;
            readonly string _look;
            public SkinKey(Material source, string look) { _source = source; _look = look ?? ""; }
            public bool Equals(SkinKey other) => ReferenceEquals(_source, other._source) && _look == other._look;
            public override bool Equals(object obj) => obj is SkinKey other && Equals(other);
            public override int GetHashCode() => (_source != null ? _source.GetHashCode() : 0) * 397 ^ _look.GetHashCode();
        }

        /// <summary>True when the ground is drawn with the game's own shader (far-picture blending, lamp, cave darkness).</summary>
        public static bool OwnTerrain { get { Look(); return _terrain != null; } }
        /// <summary>True when far-off bodies are drawn with the game's own shader (true phases, sky veil).</summary>
        public static bool OwnBody { get { Look(); return _body != null; } }
        public static bool OwnWater { get { Look(); return _water != null; } }

        /// <summary>Call before building a world. <paramref name="simple"/> forces Unity's standard shaders.</summary>
        public static void Configure(bool simple)
        {
            _simple = simple;
            _looked = false;
            ClearVertexCache();
        }

        public static void ResetStatics()
        {
            _simple = false;
            _looked = false;
            _terrain = _body = _atmosphere = _water = _sky = _model = _stars = _air = _clouds = _rings = null;
            _realPlanets = false;
            _vertexLit = _vertexUnlit = null;
            ClearVertexCache();
        }

        static void ClearVertexCache()
        {
            foreach (Material m in VertexCache.Values)
            {
                if (m != null) Object.Destroy(m);
            }
            VertexCache.Clear();
            foreach (Material m in ModelCache.Values)
            {
                if (m != null) Object.Destroy(m);
            }
            ModelCache.Clear();
            foreach (Material m in TexturedCache.Values)
            {
                if (m != null) Object.Destroy(m);
            }
            TexturedCache.Clear();
            foreach (Material m in SkinCache.Values)
            {
                if (m != null) Object.Destroy(m);
            }
            SkinCache.Clear();
        }

        public static void SetStrength(Material material, float strength)
        {
            if (material != null) material.SetFloat(StrengthId, strength);
        }

        static void Look()
        {
            if (_looked) return;
            _looked = true;
            _terrain = _simple ? null : Find("OA_Terrain", "OrbitalAnte/Terrain");
            _body = _simple ? null : Find("OA_Body", "OrbitalAnte/Body");
            _atmosphere = _simple ? null : Find("OA_Atmosphere", "OrbitalAnte/Atmosphere");
            _water = _simple ? null : Find("OA_Water", "OrbitalAnte/Water");
            _sky = _simple ? null : Find("OA_Sky", "OrbitalAnte/Sky");
            // The real stars need the sky's own shader too: both fade behind a daytime sky by the same sums.
            _stars = _simple || _sky == null ? null : Find("OA_Stars", "OrbitalAnte/Stars");
            // The realistic far planets: each piece stands by itself, and the earlier look stands in for any that is missing.
            _realPlanets = !_simple && _body != null && OrbitalAnte.Look.LookConfig.Load().realPlanets;
            _air = _realPlanets ? Find("OA_Air", "OrbitalAnte/Air") : null;
            _clouds = _realPlanets ? Find("OA_Clouds", "OrbitalAnte/Clouds") : null;
            _rings = _realPlanets ? Find("OA_Rings", "OrbitalAnte/Rings") : null;
            // Models fall back to the ground's shader (no shine) if theirs cannot be used, and go back to it when
            // Shine is turned right down on the Look asset: the way out if the model shader misbehaves on a machine.
            bool shineOff = OrbitalAnte.Look.LookConfig.Load().shine <= 0f;
            _model = _simple || _terrain == null || shineOff ? null : Find("OA_Model", "OrbitalAnte/Model");
            if (!_simple && _terrain != null && _model == null && !shineOff)
            {
                Debug.LogWarning("Orbital Ante: the model shader (OA_Model) could not be used on this machine, so models are drawn with the ground's shader and nothing shines.");
            }
            if (!_simple && (_terrain == null || _body == null || _atmosphere == null || _water == null))
            {
                Debug.LogWarning("Orbital Ante: one or more planet shaders could not be used on this machine, so Unity's standard shaders are standing in for them. " +
                                 "Ground " + Describe(_terrain) + ", far bodies " + Describe(_body) + ", air rim " + Describe(_atmosphere) + ", water " + Describe(_water) + ".");
            }
            if (!_simple && _sky == null)
            {
                Debug.LogWarning("Orbital Ante: the sky shader could not be used on this machine, so the sky is a plain colour.");
            }
        }

        static string Describe(Shader shader) => shader != null ? "ok" : "standing in";

        static Shader Find(string file, string shaderName)
        {
            Shader shader = Resources.Load<Shader>("Shaders/" + file);
            if (shader == null) shader = Shader.Find(shaderName);
#if UNITY_EDITOR
            // A shader that failed to compile can still say it is supported; in the editor ask about errors too.
            if (shader != null && UnityEditor.ShaderUtil.ShaderHasError(shader)) return null;
#endif
            return shader != null && shader.isSupported ? shader : null;
        }

        // ------------------------------------------------------------------ ground, rocks, caves

        /// <summary>
        /// The material for a body's ground tiles. With a far picture, distant ground blends into it, so what you
        /// see from orbit and what you see from a hilltop always agree.
        /// </summary>
        public static Material Ground(string bodyId, float relief = 1f)
        {
            Look();
            if (_terrain != null)
            {
                var ground = new Material(_terrain) { name = bodyId + " ground" };
                // Ground gets fine relief and its own break-up of colour; rocks and trees keep the painted one.
                if (ground.HasProperty(GroundId)) ground.SetFloat(GroundId, Mathf.Clamp(relief, 0f, 1.5f));
                return ground;
            }
            return new Material(VertexLitBase()) { name = bodyId + " ground" };
        }

        public static void SetFarPicture(Material ground, Texture2D picture, float blendStart, float blendEnd)
        {
            if (ground == null || picture == null || !ground.HasProperty(FarMapId)) return;
            ground.SetTexture(FarMapId, picture);
            ground.SetVector(FarBlendId, new Vector4(blendStart, blendEnd, 1f, picture.width));
        }

        /// <summary>
        /// A shared material that shows a mesh's own colours multiplied by <paramref name="tint"/>. Used for rocks,
        /// trees and cave walls. <paramref name="sunlight"/> 0 is for things the Sun never reaches (cave walls).
        /// </summary>
        /// <summary>
        /// For surfaces whose light was worked out beforehand and stored in the mesh colours (the ship's cabin):
        /// shows those colours times <paramref name="brightness"/> and nothing else. No sky light, lamp or fog.
        /// </summary>
        public static Material Baked(float brightness = 1f)
        {
            Material m = new Material(VertexColour(Color.white, 0f, Mathf.Clamp(brightness, 0f, 2f)));
            m.name = "OA_Baked";
            if (m.HasProperty(OutdoorsId)) m.SetFloat(OutdoorsId, 0f);
            return m;
        }

        /// <param name="paintScale">1 for ground and rocks. Larger makes the painted patches smaller, for things held in the hand.</param>
        public static Material VertexColour(Color tint, float sunlight = 1f, float glow = 0f, bool painted = true, float paintScale = 1f)
        {
            Look();
            Color32 c = tint;
            int scaleKey = Mathf.Clamp(Mathf.RoundToInt(paintScale), 1, 63);
            long key = ((long)c.r << 40) | ((long)c.g << 32) | ((long)c.b << 24) | ((long)Mathf.RoundToInt(Mathf.Clamp01(sunlight) * 100f) << 12) | (long)Mathf.RoundToInt(Mathf.Clamp(glow, 0f, 2f) * 100f) | (painted ? 0L : 1L << 50) | ((long)scaleKey << 52);
            if (VertexCache.TryGetValue(key, out Material cached) && cached != null) return cached;

            Material m;
            if (_terrain != null)
            {
                m = new Material(_terrain);
                m.SetColor(BaseColorId, tint);
                m.SetFloat(SunAmountId, sunlight);
                m.SetFloat(GlowId, glow);
                m.SetFloat(PaintId, painted ? 1f : 0f);
                m.SetFloat(PaintScaleId, scaleKey);
            }
            else if (sunlight < 0.5f || glow > 0.3f)
            {
                // No sunlight to switch off in the standard shaders, so cave walls are drawn unlit and dim instead.
                m = new Material(VertexUnlitBase());
                Color dim = glow > 0.3f ? tint : tint * 0.3f;
                dim.a = 1f;
                SetColor(m, dim);
            }
            else
            {
                m = new Material(VertexLitBase());
                SetColor(m, tint);
            }
            m.name = "OA_Vertex_" + ColorUtility.ToHtmlStringRGB(tint);
            VertexCache[key] = m;
            return m;
        }

        /// <summary>
        /// The shared material the game's own models are drawn with: the mesh's colours under the shared light,
        /// like <see cref="VertexColour"/>, plus shine where the model says a surface is smooth or bare metal
        /// (see OA_Model.shader). If that shader cannot be used the ground's stands in, without the shine.
        /// Shared: never change or destroy it.
        /// </summary>
        public static Material Model(float sunlight = 1f, float glow = 0f, float paintScale = 1f)
        {
            Look();
            if (_model == null) return VertexColour(Color.white, sunlight, glow, true, paintScale);
            int scaleKey = Mathf.Clamp(Mathf.RoundToInt(paintScale), 1, 63);
            int key = (Mathf.RoundToInt(Mathf.Clamp01(sunlight) * 100f) << 16) | (Mathf.RoundToInt(Mathf.Clamp(glow, 0f, 2f) * 100f) << 6) | scaleKey;
            if (ModelCache.TryGetValue(key, out Material cached) && cached != null) return cached;
            var m = new Material(_model) { name = "OA_Model" };
            m.SetColor(BaseColorId, Color.white);
            m.SetFloat(SunAmountId, Mathf.Clamp01(sunlight));
            m.SetFloat(GlowId, Mathf.Clamp(glow, 0f, 2f));
            m.SetFloat(PaintId, 1f);
            m.SetFloat(PaintScaleId, scaleKey);
            ModelCache[key] = m;
            return m;
        }

        /// <summary>
        /// The model material drawn from a texture set (<see cref="OrbitalAnte.Look.ModelFile.TextureSet"/>): colour,
        /// baked bumps, and a mask of hollows, smoothness and bare metal, lit by the shared look exactly as
        /// <see cref="Model"/> is. One per set and light, shared by every copy of every model that names the set.
        /// Where the model shader cannot be used (simple shaders, Shine at 0, a machine that rejects it) this is
        /// <see cref="Model"/>'s material, which draws the corner colours every model also carries.
        /// </summary>
        public static Material TexturedModel(string set, Texture2D color, Texture2D normal, Texture2D mask, float sunlight = 1f, float glow = 0f, float paintScale = 1f)
        {
            Look();
            if (_model == null || color == null || normal == null || mask == null) return Model(sunlight, glow, paintScale);
            int scaleKey = Mathf.Clamp(Mathf.RoundToInt(paintScale), 1, 63);
            string key = set + "|" + Mathf.RoundToInt(Mathf.Clamp01(sunlight) * 100f) + "|" + Mathf.RoundToInt(Mathf.Clamp(glow, 0f, 2f) * 100f) + "|" + scaleKey;
            if (TexturedCache.TryGetValue(key, out Material cached) && cached != null) return cached;
            var m = new Material(_model) { name = "OA_Model " + set };
            m.SetColor(BaseColorId, Color.white);
            m.SetFloat(SunAmountId, Mathf.Clamp01(sunlight));
            m.SetFloat(GlowId, Mathf.Clamp(glow, 0f, 2f));
            m.SetFloat(PaintId, 1f);
            m.SetFloat(PaintScaleId, scaleKey);
            m.SetTexture(ColorMapId, color);
            m.SetTexture(NormalMapId, normal);
            m.SetTexture(MaskMapId, mask);
            m.EnableKeyword(TexturedKeyword);
            TexturedCache[key] = m;
            return m;
        }

        /// <summary>
        /// A copy of one of the model materials (<see cref="Model"/> or <see cref="TexturedModel"/>) wearing a Locker gun
        /// skin: the skin's pattern, cover and two colours (sRGB; Unity makes them linear) for the shader's OA_Skin, which
        /// runs only where the cover is above 0. Not a keyword: the copy uses its source's own shader program, so a skin
        /// never waits for a new one to be built. One per material and skin, shared by every gun that wears it, destroyed with the others when
        /// a world is built. Null when <paramref name="source"/> is not drawn with the model shader (simple shaders,
        /// Shine at 0): the gun then keeps its own look.
        /// </summary>
        public static Material SkinnedModel(Material source, string look, Color colour, Color accent, int pattern, float cover = 1f)
        {
            Look();
            if (source == null || _model == null || source.shader != _model || string.IsNullOrEmpty(look)) return null;
            var key = new SkinKey(source, look);
            if (SkinCache.TryGetValue(key, out Material cached) && cached != null) return cached;
            var m = new Material(source) { name = source.name + " skin " + look };
            m.SetColor(SkinColourId, colour);
            m.SetColor(SkinAccentId, accent);
            // A cover of 0 would switch the skin off; anything put on is at least a trace.
            m.SetVector(SkinSetId, new Vector4(pattern, Mathf.Clamp(cover, 0.01f, 1f), 0f, 0f));
            SkinCache[key] = m;
            return m;
        }

        // Unity's particle shaders are the standard ones that show a mesh's own colours.
        static Material VertexLitBase()
        {
            if (_vertexLit != null) return _vertexLit;
            _vertexLit = Resources.Load<Material>("Materials/OA_VertexLit");
            if (_vertexLit == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Lit");
                if (shader == null) shader = Shader.Find("Particles/Standard Surface");
                _vertexLit = shader != null ? new Material(shader) { name = "OA_VertexLit (runtime)" } : new Material(Mat.Lit(Color.white, 0.05f));
            }
            return _vertexLit;
        }

        static Material VertexUnlitBase()
        {
            if (_vertexUnlit != null) return _vertexUnlit;
            _vertexUnlit = Resources.Load<Material>("Materials/OA_VertexUnlit");
            if (_vertexUnlit == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
                _vertexUnlit = shader != null ? new Material(shader) { name = "OA_VertexUnlit (runtime)" } : new Material(Mat.Unlit(Color.white));
            }
            return _vertexUnlit;
        }

        // ------------------------------------------------------------------ far bodies

        /// <summary>
        /// A material for a whole body seen from afar. <paramref name="emission"/> 1 is for the Sun;
        /// <paramref name="veil"/> is how completely a daytime sky hides it.
        /// </summary>
        /// <param name="boost">For things that shine by themselves: how much brighter than white, so they bloom.</param>
        public static Material Body(string name, Texture picture, Color tint, float emission = 0f, float veil = 1f, bool twoSided = false, float boost = 1f)
        {
            Look();
            Material m;
            if (_body != null)
            {
                m = new Material(_body);
                m.SetColor(BaseColorId, tint);
                m.SetFloat(EmissionId, emission);
                m.SetFloat(VeilId, veil);
                m.SetFloat(TwoSidedId, twoSided ? 1f : 0f);
                m.SetFloat(BoostId, Mathf.Clamp(boost, 1f, 12f));
            }
            else
            {
                m = new Material(emission > 0.5f ? Mat.Unlit(tint) : Mat.Lit(tint, 0.05f));
            }
            m.name = name;
            SetPicture(m, picture);
            return m;
        }

        public static void SetPicture(Material material, Texture picture)
        {
            if (material == null || picture == null) return;
            if (material.HasProperty(BaseMapId)) material.SetTexture(BaseMapId, picture);
            else if (material.HasProperty(MainTexId)) material.SetTexture(MainTexId, picture);
        }

        public static void SetColor(Material material, Color color)
        {
            if (material == null) return;
            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, color);
            if (material.HasProperty(ColorId)) material.SetColor(ColorId, color);
        }

        /// <summary>The sky ball's material, or null if the sky shader is not available (the camera's plain background stands in).</summary>
        public static Material Sky(float backdropStrength, float sunGlare = 0.75f)
        {
            Look();
            if (_sky == null) return null;
            var m = new Material(_sky) { name = "OA_Sky (runtime)" };
            m.SetFloat(BackdropStrengthId, backdropStrength);
            m.SetFloat(SunGlareId, Mathf.Clamp01(sunGlare));
            return m;
        }

        /// <summary>
        /// The material for the real stars (round dots sized in screen dots, each with its own colour and
        /// brightness), or null if its shader is not available: the earlier square stars stand in then.
        /// </summary>
        public static Material Stars(float brightness, float sunGlare)
        {
            Look();
            if (_stars == null) return null;
            var m = new Material(_stars) { name = "OA_Stars (runtime)" };
            m.SetFloat(BrightnessId, Mathf.Clamp(brightness, 0f, 4f));
            m.SetFloat(SunGlareId, Mathf.Clamp01(sunGlare));
            return m;
        }

        public static void SetBackdrop(Material sky, Texture picture)
        {
            if (sky != null && picture != null && sky.HasProperty(BackdropId)) sky.SetTexture(BackdropId, picture);
        }

        // ------------------------------------------------------------------ realistic far planets

        /// <summary>True when far bodies take the realistic look ("Real Planets" on the Look asset, own shaders in use).</summary>
        public static bool RealPlanets { get { Look(); return _realPlanets; } }

        /// <summary>
        /// Switches a far body's material to the realistic look: a black night side with an edge as soft as its
        /// air makes it, and low sunlight reddened by that air.
        /// </summary>
        /// <param name="willHaveRelief">
        /// True for a body whose slopes will arrive later (<see cref="SetRelief"/>): the shader is switched to the
        /// relief version now, with level ground, so that version is made ready at the start of a run and not in
        /// the middle of a flight.
        /// </param>
        public static void SetRealBody(Material material, AirLook air, bool willHaveRelief = false)
        {
            if (material == null || !RealPlanets || material.shader != _body) return;
            material.EnableKeyword(RealKeyword);
            if (willHaveRelief)
            {
                material.EnableKeyword(ReliefKeyword);
                material.SetTexture(ReliefMapId, LevelGround());
            }
            material.SetFloat(SoftId, (float)air.Soft);
            material.SetVector(ReddenId, new Vector4((float)air.ReddenR, (float)air.ReddenG, (float)air.ReddenB, 0f));
            material.SetFloat(NightId, Mathf.Clamp01(OrbitalAnte.Look.LookConfig.Load().planetNightSide));
        }

        static Texture2D _levelGround;

        /// <summary>One dot of "no slope, no sea": what a far body is lit by until its own slopes are worked out.</summary>
        static Texture2D LevelGround()
        {
            if (_levelGround != null) return _levelGround;
            _levelGround = new Texture2D(1, 1, TextureFormat.RGBA32, false, true) { name = "Level ground", hideFlags = HideFlags.HideAndDontSave };
            _levelGround.SetPixelData(new byte[] { FarRelief.Encode(0), FarRelief.Encode(0), 0, 255 }, 0);
            _levelGround.Apply(false, true);
            return _levelGround;
        }

        /// <summary>Gives a far body the lie of its land (slopes and sea, see FarRelief), so its relief catches the light.</summary>
        public static void SetRelief(Material material, Texture relief, float strength)
        {
            if (material == null || relief == null || !RealPlanets || material.shader != _body) return;
            material.EnableKeyword(ReliefKeyword);
            material.SetTexture(ReliefMapId, relief);
            material.SetFloat(ReliefId, strength);
        }

        public static void SetNightLights(Material material, Texture lights, Color color)
        {
            if (material == null || lights == null || !material.HasProperty(LightsMapId)) return;
            material.SetTexture(LightsMapId, lights);
            material.SetColor(LightsColorId, color);
        }

        /// <summary>The clouds whose shadows fall on a far body. <paramref name="driftTurns"/> is turns a second, as on the cloud layer.</summary>
        public static void SetCloudShadows(Material material, Texture clouds, float darkness, float amount, float driftTurns)
        {
            if (material == null || !material.HasProperty(CloudShadowId)) return;
            if (clouds != null) material.SetTexture(CloudMapId, clouds);
            material.SetVector(CloudShadowId, new Vector4(darkness, amount, driftTurns, 0f));
        }

        public static void SetAurora(Material material, Color color)
        {
            if (material != null && material.HasProperty(AuroraId)) material.SetColor(AuroraId, color);
        }

        /// <summary>The rings whose shadow falls on a far body: their strip of picture and their edges in body radii.</summary>
        public static void SetRingShadow(Material material, Texture strip, float inner, float outer)
        {
            if (material == null || strip == null || !material.HasProperty(RingRadiiId)) return;
            material.SetTexture(RingMapId, strip);
            material.SetVector(RingRadiiId, new Vector4(inner, outer, 0f, 0f));
        }

        /// <summary>The thin shell of air round a planet seen from space, or null if it cannot be used (the earlier rim stands in).</summary>
        public static Material Air(string name, Color color, AirLook air)
        {
            Look();
            if (_air == null) return null;
            var m = new Material(_air) { name = name };
            color *= (float)air.Gain;
            color.a = 1f;
            m.SetColor(BaseColorId, color);
            m.SetFloat(StrengthId, 1f);
            m.SetFloat(DepthId, (float)air.Depth);
            m.SetFloat(ScaleId, (float)air.Scale);
            m.SetFloat(HideId, (float)air.Hide);
            m.SetFloat(ShellId, (float)air.Shell);
            return m;
        }

        /// <summary>A layer of cloud round a planet seen from space, or null if it cannot be used.</summary>
        public static Material Clouds(string name, Texture picture, CloudLook clouds, AirLook air)
        {
            Look();
            if (_clouds == null || picture == null) return null;
            var m = new Material(_clouds) { name = name };
            m.SetTexture(BaseMapId, picture);
            m.SetFloat(StrengthId, 1f);
            m.SetFloat(AmountId, (float)clouds.Amount);
            m.SetFloat(DriftId, clouds.DriftSeconds > 0 ? (float)(1.0 / clouds.DriftSeconds) : 0f);
            m.SetFloat(SoftId, (float)air.Soft);
            m.SetVector(ReddenId, new Vector4((float)air.ReddenR, (float)air.ReddenG, (float)air.ReddenB, 0f));
            return m;
        }

        /// <summary>
        /// Tells a cloud layer the ground under it (the relief picture's alpha, <paramref name="low"/> to
        /// <paramref name="high"/> metres), so it parts round ground as high as itself.
        /// </summary>
        public static void SetCloudGround(Material clouds, Texture relief, float low, float high, float layerHeight, float band)
        {
            if (clouds == null || relief == null || !clouds.HasProperty(GroundVectorId)) return;
            clouds.SetTexture(GroundMapId, relief);
            clouds.SetVector(GroundVectorId, new Vector4(low, high, layerHeight, Mathf.Max(0.01f, band)));
        }

        /// <summary>Rings as one see-through band, or null if they cannot be used (the earlier solid bands stand in).</summary>
        public static Material Rings(string name, Texture strip, float planetRadius)
        {
            Look();
            if (_rings == null || strip == null) return null;
            var m = new Material(_rings) { name = name };
            m.SetTexture(BaseMapId, strip);
            m.SetFloat(PlanetRadiusId, planetRadius);
            m.SetFloat(VeilId, 1f);
            // After the air, which would otherwise dim rings that pass in front of the planet's edge.
            m.renderQueue = 3020;
            return m;
        }

        /// <summary>The glowing rim of air seen from space, or null if the shader for it is not available.</summary>
        public static Material Atmosphere(string name, Color color, float strength, float limb)
        {
            Look();
            if (_atmosphere == null) return null;
            var m = new Material(_atmosphere) { name = name };
            m.SetColor(BaseColorId, color);
            m.SetFloat(StrengthId, strength);
            m.SetFloat(LimbId, Mathf.Clamp(limb, 0.05f, 0.98f));
            return m;
        }

        // ------------------------------------------------------------------ liquids

        /// <summary>The surface of a sea, lake or pool. <paramref name="glow"/> above zero is for lava.</summary>
        public static Material Water(string name, Color color, Color deep, float opacity, float ripple, float glow = 0f)
        {
            Look();
            Material m;
            if (_water != null)
            {
                m = new Material(_water);
                color.a = opacity;
                m.SetColor(BaseColorId, color);
                m.SetColor(DeepColorId, deep);
                m.SetFloat(RippleId, ripple);
                m.SetFloat(GlowId, glow);
            }
            else if (glow > 0.3f)
            {
                color.a = 1f;
                m = new Material(Mat.Unlit(color));
            }
            else
            {
                // The standard shader stand-in is solid, not see-through: simple and cannot go wrong.
                color.a = 1f;
                m = new Material(Mat.Lit(color, 0.85f));
                if (m.HasProperty(SmoothnessId)) m.SetFloat(SmoothnessId, 0.85f);
            }
            m.name = name;
            return m;
        }
    }
}
