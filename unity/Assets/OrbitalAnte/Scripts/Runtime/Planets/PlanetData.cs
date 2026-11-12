using System;
using System.Collections.Generic;
using OrbitalAnte.Content;
using UnityEngine;

namespace OrbitalAnte.Planets
{
    /// <summary>Numbers that decide how much ground is drawn and how hard the machine works to draw it.</summary>
    [Serializable]
    public sealed class PlanetSettings
    {
        [Tooltip("Tick this if planets show up bright pink. It swaps the game's own planet shaders for Unity's standard ones. Skies and far-off planets look plainer, but everything works.")]
        public bool simpleShaders;

        [Header("Ground detail")]
        [Tooltip("Size of the smallest squares of ground right under you, in metres. Smaller is smoother and costs more.")]
        [Range(1f, 4f)] public float finestCell = 1.7f;
        [Tooltip("How far away ground switches to more detail, measured in tile widths. Bigger means more detail at a distance and more to draw.")]
        [Range(0.8f, 3f)] public float splitDistance = 1.2f;
        [Tooltip("How many pieces of ground may be worked out at the same time on spare processor cores.")]
        [Range(1, 6)] public int backgroundWorkers = 3;
        [Tooltip("How many finished pieces of ground may be handed to the graphics card in one frame.")]
        [Range(1, 8)] public int uploadsPerFrame = 2;
        [Tooltip("Ground this close to the ship is solid, in metres. The distance grows with speed.")]
        public float solidGroundRange = 90f;
        [Tooltip("However fast the ship is going, ground further than this is not made solid, in metres.")]
        public float solidGroundLimit = 260f;

        [Header("Look from far away")]
        [Tooltip("Width of a planet's far-away picture in dots. The height is half this.")]
        public int planetPictureWidth = 1024;
        public int moonPictureWidth = 512;
        [Tooltip("A body's detailed ground switches on when you are within this many of its radiuses.")]
        public float nearInRadiuses = 4f;

        [Header("Rocks, trees and other placed things")]
        [Tooltip("The most rocks, trees and so on that may exist at once.")]
        public int featureCap = 500;
        [Tooltip("How far away placed landmarks (hoodoos, arches, spires) appear, in metres.")]
        public float featureRange = 420f;
        [Tooltip("How far away loose rocks, bushes and trees appear, in metres.")]
        public float scatterRange = 170f;
        [Tooltip("The most rocks, trees and so on that may be put out in one frame. Keeps arriving somewhere from causing a stutter.")]
        public int featuresPerFrame = 24;
        [Tooltip("Things at least this tall, in metres, are solid. Smaller ones you pass through.")]
        public float solidFeatureHeight = 1.2f;

        [Header("Air and water")]
        [Tooltip("How much of the ship's weight water holds up. Above 1 it floats (and must thrust down to dive); below 1 it sinks slowly.")]
        [Range(0f, 1.5f)] public float buoyancy = 1.05f;
        [Tooltip("How quickly storm wind drags a flying ship up to the wind's own speed. 0 switches wind pushing off.")]
        [Range(0f, 1f)] public float windPush = 0.08f;
        [Tooltip("Brightness of the lamp that comes on in the dark (night, caves, deep water). 0 switches it off.")]
        [Range(0f, 3f)] public float lampBrightness = 1.15f;
        public float lampReach = 55f;

        [Header("Weather")]
        [Tooltip("Scales how much rain, snow or dust is drawn. 0 switches the particles off (storm haze and lightning stay).")]
        [Range(0f, 1.5f)] public float weatherParticles = 1f;
        [Range(0f, 1f)] public float thunderVolume = 0.6f;
    }

    /// <summary>
    /// Every body's surface as data: ground shape, colours, sea, sky, weather and formations. An asset is created
    /// at Assets/OrbitalAnte/Resources/Planets/Planets so all of it can be changed in the Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Planets", fileName = "Planets")]
    public sealed class PlanetData : ContentDefinition
    {
        public const string ResourceFolder = "Planets";
        public const string DefaultId = "Planets";

        public PlanetSettings settings = new PlanetSettings();
        [Tooltip("One entry per body that has ground to land on. bodyId must match the body's id in the Solar System asset.")]
        public List<PlanetProfile> planets = PlanetProfiles.Defaults();

        public override string DisplayName => "Planets";

        public static PlanetData CreateDefaults()
        {
            var data = CreateInstance<PlanetData>();
            data.name = DefaultId;
            return data;
        }

        /// <summary>The asset if it exists, otherwise the built-in defaults.</summary>
        public static PlanetData Load() => ContentDatabase.Get<PlanetData>(DefaultId);

        public PlanetProfile Find(string bodyId)
        {
            for (int i = 0; i < planets.Count; i++)
            {
                if (planets[i].bodyId == bodyId) return planets[i];
            }
            return null;
        }

        public override void Validate(List<string> problems)
        {
            if (planets == null || planets.Count == 0)
            {
                problems.Add("has no planets.");
                return;
            }
            var seen = new HashSet<string>();
            for (int i = 0; i < planets.Count; i++)
            {
                PlanetProfile p = planets[i];
                string who = string.IsNullOrEmpty(p.bodyId) ? "planet " + (i + 1) : p.bodyId;
                if (string.IsNullOrEmpty(p.bodyId)) problems.Add(who + " has no bodyId.");
                else if (!seen.Add(p.bodyId)) problems.Add(who + " is listed twice.");
                if (p.surfaceRadius < 0) problems.Add(who + ": surface radius cannot be negative.");
                if (p.hillWavelength <= 0 || p.continentWavelength <= 0 || p.mountainWavelength <= 0 || p.detailWavelength <= 0)
                    problems.Add(who + ": every wavelength must be above zero.");
                if (p.hasAtmosphere && p.visibility <= 0) problems.Add(who + ": visibility must be above zero.");
                if (p.hasSea && p.underwaterVisibility <= 0) problems.Add(who + ": underwater visibility must be above zero.");
                if (p.cloudDeck && !p.hasAtmosphere) problems.Add(who + ": a cloud deck needs an atmosphere.");
                for (int f = 0; f < p.formations.Count; f++)
                {
                    if (FormationIndex.Get(p.formations[f].formation) == null)
                        problems.Add(who + ": there is no formation called '" + p.formations[f].formation + "' in the index.");
                }
                for (int w = 0; w < p.weather.Count; w++)
                {
                    if (p.weather[w].cells < 0 || p.weather[w].cells > 24) problems.Add(who + ": storm count should be between 0 and 24.");
                    if (p.weather[w].cellRadius <= 0) problems.Add(who + ": storm size must be above zero.");
                }
            }
            if (settings.planetPictureWidth < 64 || settings.planetPictureWidth > 4096) problems.Add("planet picture width should be between 64 and 4096.");
            if (settings.moonPictureWidth < 64 || settings.moonPictureWidth > 4096) problems.Add("moon picture width should be between 64 and 4096.");
            if (settings.featureCap < 0 || settings.featureCap > 3000) problems.Add("feature cap should be between 0 and 3000.");
        }
    }
}
