using System;
using System.Collections.Generic;

namespace OrbitalAnte.Planets
{
    public enum WeatherKind { Rain, Snow, Dust, Acid, Methane, Diamond, Thunder }

    /// <summary>A patch of different-coloured ground: the Moon's dark seas, Earth's deserts and forests.</summary>
    [Serializable]
    public sealed class ColorZone
    {
        public string color = "#808080";
        /// <summary>Size of the patches, in metres.</summary>
        public double wavelength = 200;
        /// <summary>How much of the ground the patches cover. -1 is everywhere, 1 is nowhere.</summary>
        public double threshold = 0.1;
        public double strength = 0.8;
        /// <summary>Only between these latitudes. 0 is the equator, 1 is a pole.</summary>
        public double minLatitude;
        public double maxLatitude = 1;
        /// <summary>Only between these heights, in metres.</summary>
        public double minHeight = -100000;
        public double maxHeight = 100000;

        public ColorZone()
        {
        }

        public ColorZone(string color, double wavelength, double threshold, double strength = 0.8)
        {
            this.color = color;
            this.wavelength = wavelength;
            this.threshold = threshold;
            this.strength = strength;
        }

        public ColorZone Latitudes(double min, double max)
        {
            minLatitude = min;
            maxLatitude = max;
            return this;
        }

        public ColorZone Heights(double min, double max)
        {
            minHeight = min;
            maxHeight = max;
            return this;
        }
    }

    /// <summary>One kind of weather on a body: drifting storm cells of a given size, with wind and maybe lightning.</summary>
    [Serializable]
    public sealed class WeatherSpec
    {
        public WeatherKind kind = WeatherKind.Rain;
        /// <summary>How many storms of this kind are on the body at once.</summary>
        public int cells = 3;
        /// <summary>Radius of one storm, in metres.</summary>
        public double cellRadius = 160;
        /// <summary>Wind at the heart of the storm, in metres per second.</summary>
        public double windSpeed = 10;
        /// <summary>Colour of the lightning as a web colour. Empty for no lightning.</summary>
        public string lightningColor = "";
        /// <summary>Colour of the rain, snow or dust.</summary>
        public string color = "#B8C4D0";
        /// <summary>How far you can see in the thick of it, in metres.</summary>
        public double visibility = 70;
        /// <summary>True for a storm that never ends and stays at one latitude (the Great Red Spot).</summary>
        public bool permanent;
        public double latitude;

        public WeatherSpec()
        {
        }

        public WeatherSpec(WeatherKind kind, int cells, double cellRadius, double windSpeed, string color, string lightningColor = "", double visibility = 70)
        {
            this.kind = kind;
            this.cells = cells;
            this.cellRadius = cellRadius;
            this.windSpeed = windSpeed;
            this.color = color;
            this.lightningColor = lightningColor;
            this.visibility = visibility;
        }
    }

    /// <summary>Loose things sprinkled everywhere the ground suits them: this planet's own rocks.</summary>
    [Serializable]
    public sealed class ScatterRule
    {
        public FeatureKind feature = FeatureKind.Boulder;
        /// <summary>How many per 100 by 100 metres of suitable ground.</summary>
        public double perHectare = 12;
        public double minScale = 0.4;
        public double maxScale = 1.6;
        /// <summary>Steepest ground it will sit on. 0 is flat, 1 is a wall.</summary>
        public double maxSlope = 0.6;
        /// <summary>Lowest and highest ground it appears on, in metres.</summary>
        public double minHeight = -100000;
        public double maxHeight = 100000;
        /// <summary>Colour as a web colour. Empty uses the body's rock colour.</summary>
        public string color = "";
        /// <summary>Where it grows between the equator (0) and the poles (1). The same on both halves of the world.</summary>
        public double minLatitude;
        public double maxLatitude = 1;
        /// <summary>
        /// Above 0, it grows in patches this many metres across with open ground between, instead of evenly
        /// everywhere: woods and clearings, not a lawn of trees.
        /// </summary>
        public double patch;
        /// <summary>How much of the ground the patches cover, 0 to 1.</summary>
        public double cover = 0.5;

        public ScatterRule()
        {
        }

        public ScatterRule(FeatureKind feature, double perHectare, double minScale, double maxScale, string color = "")
        {
            this.feature = feature;
            this.perHectare = perHectare;
            this.minScale = minScale;
            this.maxScale = maxScale;
            this.color = color;
        }
    }

    /// <summary>
    /// Everything that makes one body's surface its own: the shape of its ground, its colours, its sea, its sky,
    /// its weather, and which formations from the index it has. Distances are metres. Colours are web colours.
    /// An asset holding all of these is created at Assets/OrbitalAnte/Resources/Planets/Planets. No Unity types.
    /// </summary>
    [Serializable]
    public sealed class PlanetProfile
    {
        public string bodyId = "";
        /// <summary>
        /// Radius of the ground, in metres. 0 means the body's own radius. Gas giants set this far below their
        /// cloud tops: it is the surface you find after flying down through the clouds.
        /// </summary>
        public double surfaceRadius;

        // ---- shape of the ground
        /// <summary>The broadest ups and downs: continents and basins.</summary>
        public double continentHeight = 20;
        public double continentWavelength = 500;
        /// <summary>Added to every height. Negative sinks most of the land under the sea.</summary>
        public double continentBias;
        /// <summary>Rolling hills, the scale you drive over.</summary>
        public double hillHeight = 6;
        public double hillWavelength = 80;
        /// <summary>Sharp-crested mountains, only in some places.</summary>
        public double mountainHeight = 30;
        public double mountainWavelength = 240;
        /// <summary>How much of the body has mountains. 0 none, 1 everywhere.</summary>
        public double mountainCoverage = 0.25;
        /// <summary>Small bumps underfoot.</summary>
        public double detailHeight = 0.4;
        public double detailWavelength = 8;

        // ---- how the ground has been worn
        /// <summary>
        /// How much the hills and mountains look worn by running water or ages of falling dust: steep flanks run
        /// clean down to smooth valley floors and the bumps gather on crests. 0 is evenly lumpy, 1 strongly worn.
        /// </summary>
        public double erosion = 0.7;
        /// <summary>How calm the calmest country is, 0 to 1. The roughest is always a little rougher than before.</summary>
        public double plains = 0.6;
        /// <summary>Winding valleys cut into the high ground, in metres deep. 0 for none.</summary>
        public double valleyDepth;
        /// <summary>Roughly how far apart the valleys run, in metres.</summary>
        public double valleyWavelength = 280;
        /// <summary>How wide a valley is next to how far apart they are. 0.05 a gorge, 0.2 a broad dale.</summary>
        public double valleyWidth = 0.1;
        /// <summary>
        /// In some regions the land steps down in flat benches with cliffs between, like layered rock: the height
        /// of one step, in metres. 0 for none.
        /// </summary>
        public double terraceStep;
        /// <summary>How much of the body is stepped country. 0 none, 1 all of it.</summary>
        public double terraceCoverage = 0.3;
        /// <summary>Broken, craggy rock on high and mountainous ground, in metres.</summary>
        public double cragHeight;

        // ---- sea
        public bool hasSea;
        public double seaLevel;
        public Liquid seaLiquid = Liquid.Water;
        public string seaColor = "#1F6FA8";
        public string seaDeepColor = "#0B2E55";
        /// <summary>How far you can see under the surface, in metres.</summary>
        public double underwaterVisibility = 90;

        // ---- colours
        public string lowColor = "#6E6A64";
        public string midColor = "#8A857D";
        public string highColor = "#A39E96";
        /// <summary>Colour of steep ground: cliffs and canyon walls.</summary>
        public string cliffColor = "#5C5852";
        /// <summary>Colour of the highest ground (snow), above <see cref="peakHeight"/>.</summary>
        public string peakColor = "#F2F4F6";
        public string shoreColor = "";
        public double midHeight = 8;
        public double highHeight = 30;
        public double peakHeight = 100000;
        /// <summary>Colour of the polar caps. Empty for none.</summary>
        public string poleColor = "";
        /// <summary>Where the caps begin. 0 is the equator, 1 is the pole itself.</summary>
        public double poleLatitude = 0.85;
        /// <summary>Thickness of the coloured layers that show on cliffs, in metres. 0 for none.</summary>
        public double strata;
        public List<ColorZone> zones = new List<ColorZone>();
        /// <summary>Colour of this body's loose rocks.</summary>
        public string rockColor = "#77726B";

        // ---- sky
        public bool hasAtmosphere;
        /// <summary>Colour of the daytime sky seen from the ground.</summary>
        public string skyColor = "#000000";
        /// <summary>Colour of distant haze.</summary>
        public string hazeColor = "#000000";
        /// <summary>How far above the ground the air reaches, in metres.</summary>
        public double atmosphereHeight = 120;
        /// <summary>How far you can see through the air at ground level on a clear day, in metres.</summary>
        public double visibility = 1500;
        /// <summary>How completely the daytime sky hides the stars. 0 not at all, 1 completely.</summary>
        public double skyOpacity = 0.9;
        /// <summary>How much sunlight the air swallows before it reaches the ground. 0 none, 1 all.</summary>
        public double sunDimming;
        /// <summary>
        /// True for a gas giant: an unbroken deck of cloud fills the top of the atmosphere, and you fly down
        /// through it to reach the clear air and the surface beneath.
        /// </summary>
        public bool cloudDeck;
        /// <summary>For cloud decks: how far above the ground the underside of the clouds is, in metres.</summary>
        public double cloudBase = 300;
        /// <summary>How much the air slows things down at ground level. 0 is none.</summary>
        public double airDrag;

        public List<WeatherSpec> weather = new List<WeatherSpec>();
        public List<FormationUse> formations = new List<FormationUse>();
        public List<ScatterRule> scatter = new List<ScatterRule>();

        public PlanetProfile Clone()
        {
            var copy = (PlanetProfile)MemberwiseClone();
            copy.zones = new List<ColorZone>(zones);
            copy.weather = new List<WeatherSpec>(weather);
            copy.formations = new List<FormationUse>(formations);
            copy.scatter = new List<ScatterRule>(scatter);
            return copy;
        }
    }
}
