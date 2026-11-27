using UnityEngine;

namespace OrbitalAnte.Look
{
    public enum ToneMapping { None, Neutral, Filmic }
    public enum EdgeSmoothing { Off, Fast, Multisample2, Multisample4 }

    /// <summary>
    /// The dials for the game's look: the finish laid over the whole picture (bloom, tone mapping, colour grade,
    /// vignette, smoothed edges) and how strongly the painted style is applied. An asset is created at
    /// Assets/OrbitalAnte/Resources/Config/LookConfig. What each dial is for, and what it costs on a Steam Deck,
    /// is in docs/art_direction.md and docs/performance_budget.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Look", fileName = "LookConfig")]
    public sealed class LookConfig : ScriptableObject
    {
        public const string ResourcePath = "Config/LookConfig";

        [Header("Finish (laid over the whole picture)")]
        [Tooltip("Master switch. Off draws the picture raw: no bloom, tone mapping, colour grade or vignette.")]
        public bool finish = true;
        [Tooltip("Bright things (the Sun, engine glow, the entry ring, glitter on water) spill a soft glow. 0 switches it off.")]
        [Range(0f, 2f)] public float bloom = 0.55f;
        [Tooltip("Only things brighter than this glow. Unity measures this its own way (not in the same units as the game's light), with a soft start, so tune it by eye: raise it if pale sunlit ground glows, lower it if the Sun and lamps do not.")]
        [Range(0.5f, 3f)] public float bloomThreshold = 1.25f;
        [Tooltip("How very bright light is squeezed onto the screen. Neutral keeps colours true; Filmic is punchier and a little darker.")]
        public ToneMapping toneMapping = ToneMapping.Neutral;
        [Tooltip("Overall brightness, in camera stops. 0 is unchanged.")]
        [Range(-2f, 2f)] public float exposure = 0.12f;
        [Range(-50f, 50f)] public float contrast = GradeDefaults.Contrast;
        [Tooltip("How vivid colours are. 0 is unchanged.")]
        [Range(-50f, 50f)] public float saturation = GradeDefaults.Saturation;
        [Tooltip("Above 0 warms the whole picture, below cools it.")]
        [Range(-50f, 50f)] public float warmth = 5f;
        [Tooltip("Tints shadows toward teal and highlights toward amber. 0 switches it off. Kept low on purpose: a strong teal and amber grade is the stock look of generated pictures (22 Nov 2026).")]
        [Range(0f, 1f)] public float splitTone = GradeDefaults.SplitTone;
        [Tooltip("Darkens the corners of the screen a little. 0 switches it off.")]
        [Range(0f, 0.6f)] public float vignette = GradeDefaults.Vignette;

        [Header("Edges")]
        [Tooltip("Smooths jagged edges. Multisample4 looks best but costs the most; Multisample2 is the Steam Deck default; Fast is cheapest; Off is the raw picture.")]
        public EdgeSmoothing edgeSmoothing = EdgeSmoothing.Multisample2;

        [Header("Painted style")]
        [Tooltip("How strongly flat colours are broken up into painted patches and dabs. 0 is flat colour.")]
        [Range(0f, 1.5f)] public float paint = 1f;
        [Tooltip("The rim of sky colour on edges that turn away from you.")]
        [Range(0f, 1.5f)] public float rimLight = 0.5f;
        [Tooltip("Warm light bounced up from sunlit ground into shadows.")]
        [Range(0f, 2f)] public float bounceLight = 1f;

        [Header("Realism")]
        [Tooltip("How strongly smooth surfaces shine: the Sun's highlight and the sky seen in metal, visors and glass. 0 lights everything like chalk, as the game did before models said what they are made of, and draws models with the ground's shader again (after Play is stopped and started): use it if models turn magenta.")]
        [Range(0f, 2f)] public float shine = 1f;
        [Tooltip("How firmly the edge of a shadow is drawn. 1 is a clear band between light and dark, like an illustration. 0 is a soft fall-off, like a photograph. 0.6 is how the game looked before this dial existed.")]
        [Range(0f, 1f)] public float lightBand = 0.6f;

        [Header("Light and shadow")]
        [Tooltip("On: sunlight falls only on what faces the Sun, shade is lit by the sky alone (nearly black on a world with no air), and the ground far off goes dark where it is night there. Off: the earlier soft light that wraps round into the shade. While on, Light Band and the warm edge of the shadow are not used.")]
        public bool realLight = true;
        [Tooltip("On: the Sun casts shadows: the ground, rocks and trees, enemies and their guns, the lander, the rover and base buildings near you. Needs Real Light. Off: nothing casts a shadow (the cheapest). Takes effect at the next run.")]
        public bool sunShadows = true;
        [Tooltip("How dark a cast shadow is. 1 leaves only the sky's light in it; lower lets some sunlight through, which makes enemies in shade easier to see.")]
        [Range(0f, 1f)] public float shadowDarkness = 1f;
        [Tooltip("How far from you shadows are cast, in metres. Further costs more and makes the shadows at your feet less crisp. On these small worlds the horizon is only 20 to 40 m off on foot. Takes effect at the next run.")]
        [Range(30f, 250f)] public float shadowDistance = 70f;
        [Tooltip("How much sunlight the ground round about throws into shadows. It is all the light there is in a shadow on a world with no air; 0 makes those black.")]
        [Range(0f, 0.15f)] public float groundBounce = 0.06f;

        [Header("Sky")]
        [Tooltip("Brightness of the picture of deep space behind the stars: the Milky Way, its dust, the far galaxies and nebulae. 0 is plain black.")]
        [Range(0f, 1.5f)] public float spaceBackdrop = 1f;
        [Tooltip("On: the sky is the real one. The stars are the real stars at their true places, brightness and colour, and the Milky Way lies where it really does. Off: the earlier sky, a faint painted band and evenly scattered stars.")]
        public bool realSky = true;
        [Tooltip("On: planets seen from space have a black night side, relief that catches the light, a thin rim of air, clouds, painted gas giants and fine rings. Off: the earlier smooth balls with a wide glow.")]
        public bool realPlanets = true;
        [Tooltip("With Real Planets on: how much of space's faint fill light a far planet's night side keeps. 0 is pure black (a moon met from its dark side is only a hole in the stars); higher shows its ground faintly.")]
        [Range(0f, 1f)] public float planetNightSide = 0.15f;
        [Tooltip("How much the Sun's glare hides the stars and deep space near it, as it does for any eye or camera. 0 leaves the sky as bright beside the Sun as anywhere.")]
        [Range(0f, 1f)] public float sunHidesStars = 0.75f;
        [Tooltip("Size of the glow round the Sun.")]
        [Range(0f, 2f)] public float sunGlow = 1f;
        [Tooltip("How much brighter than white the Sun's disc is drawn, so that it blooms.")]
        [Range(1f, 12f)] public float sunBrightness = 5f;
        [Tooltip("How bright the stars are. 1.5 shows them as baked; higher makes every star bolder.")]
        [Range(1f, 4f)] public float starBrightness = 1.5f;

        [Header("Far copies (enemies and guns a long way off)")]
        [Tooltip("On: an enemy or a gun a long way off is drawn as a simpler copy of itself. Off draws everything in full, which is the costliest the game can be.")]
        public bool farCopies = true;
        [Tooltip("The most enemy bodies drawn in full at once, the nearest in view first. Past this number even a close enemy wears its simpler copy, so raise it if you can see that happen and the Budget line on F3 has room.")]
        [Range(0, 40)] public int fullBodies = DetailRule.FullBodies;
        [Tooltip("The most triangles of enemy bodies drawn in full at once. A finer enemy model uses this up sooner, so fewer of it are full: 180,000 is twelve of the first enemy model or eight of the realistic one. Raise it if close enemies wear their simpler copies and the Budget line on F3 has room. To switch far copies off altogether, untick Far Copies.")]
        [Range(20000, 600000)] public int fullTriangles = DetailRule.FullTriangles;
        [Tooltip("How far off things change to their simpler copies. 1 is 24 m for an enemy and 9 m for a gun; 2 is twice as far. Raise it if you can see the change happen.")]
        [Range(0.5f, 4f)] public float farCopyDistance = 1f;

        [Tooltip("Which set of built-in grade defaults this asset's finish came from. When the game's defaults move on, Contrast, Saturation, Split Tone and Vignette are set to the new defaults (your other dials are kept). Set this to 9999 to keep your own grade for good.")]
        public int gradeVersion;

        void OnEnable()
        {
            // An asset made before the grade was toned down still holds the old, stronger grade. Like the planets'
            // defaults, nothing here has been edited by hand in practice, and the alternative is a step for Matthew.
            if (gradeVersion >= GradeDefaults.Version) return;
            contrast = GradeDefaults.Contrast;
            saturation = GradeDefaults.Saturation;
            splitTone = GradeDefaults.SplitTone;
            vignette = GradeDefaults.Vignette;
            gradeVersion = GradeDefaults.Version;
        }

        static LookConfig _cached;

        public static LookConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<LookConfig>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<LookConfig>();
                _cached.name = "LookConfig (defaults)";
            }
            return _cached;
        }

        public static void ResetStatics() => _cached = null;
    }
}
