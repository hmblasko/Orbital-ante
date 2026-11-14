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
        [Range(-50f, 50f)] public float contrast = 10f;
        [Tooltip("How vivid colours are. 0 is unchanged.")]
        [Range(-50f, 50f)] public float saturation = 10f;
        [Tooltip("Above 0 warms the whole picture, below cools it.")]
        [Range(-50f, 50f)] public float warmth = 5f;
        [Tooltip("Tints shadows toward teal and highlights toward amber. 0 switches it off.")]
        [Range(0f, 1f)] public float splitTone = 0.55f;
        [Tooltip("Darkens the corners of the screen a little. 0 switches it off.")]
        [Range(0f, 0.6f)] public float vignette = 0.2f;

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

        [Header("Sky")]
        [Tooltip("Brightness of the coloured clouds of deep space behind the stars. 0 is plain black.")]
        [Range(0f, 1.5f)] public float spaceBackdrop = 1f;
        [Tooltip("Size of the glow round the Sun.")]
        [Range(0f, 2f)] public float sunGlow = 1f;
        [Tooltip("How much brighter than white the Sun's disc is drawn, so that it blooms.")]
        [Range(1f, 12f)] public float sunBrightness = 5f;
        [Range(1f, 4f)] public float starBrightness = 1.5f;

        [Header("Far copies (enemies and guns a long way off)")]
        [Tooltip("On: an enemy or a gun a long way off is drawn as a simpler copy of itself. Off draws everything in full, which is the costliest the game can be.")]
        public bool farCopies = true;
        [Tooltip("The most enemy bodies drawn in full at once, the nearest in view first. Past this number even a close enemy wears its simpler copy, so raise it if you can see that happen and the Budget line on F3 has room.")]
        [Range(0, 40)] public int fullBodies = DetailRule.FullBodies;
        [Tooltip("How far off things change to their simpler copies. 1 is 24 m for an enemy and 9 m for a gun; 2 is twice as far. Raise it if you can see the change happen.")]
        [Range(0.5f, 4f)] public float farCopyDistance = 1f;

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
