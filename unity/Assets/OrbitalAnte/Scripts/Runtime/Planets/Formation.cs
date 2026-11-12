using System;

namespace OrbitalAnte.Planets
{
    public enum FormationCategory
    {
        Impact,
        Volcanic,
        Tectonic,
        WindAndErosion,
        RiversAndWater,
        Ice,
        Caves,
        Sky,
        Vegetation,
    }

    /// <summary>How a formation is made in the game. Many formations share one technique with different numbers.</summary>
    public enum Technique
    {
        /// <summary>A round pit with a raised rim, pressed into the ground.</summary>
        Crater,
        /// <summary>A cone-shaped rise, optionally with a pit at the top.</summary>
        Cone,
        /// <summary>A smooth rounded rise, or a hollow when its height is negative.</summary>
        Dome,
        /// <summary>A flat-topped block with steep sides.</summary>
        Plateau,
        /// <summary>Something long and narrow that follows a line: a canyon, a ridge, a cliff.</summary>
        Curve,
        /// <summary>A patch of ground with its own surface pattern: dunes, cracks, blocks.</summary>
        Region,
        /// <summary>A tunnel under the ground with one or two openings.</summary>
        Cave,
        /// <summary>A group of ready-made shapes (rocks, spires, trees) stood on the ground.</summary>
        Scatter,
        /// <summary>Covers the whole body and is switched on by the body's own settings: an ocean, an ice cap.</summary>
        Global,
        /// <summary>Lives in the sky or the weather, not the ground.</summary>
        Sky,
    }

    public enum CurveProfile { V, U, Ridge, DoubleRidge, Scarp, Block }

    public enum RegionTexture
    {
        Flat, Dunes, StarDunes, Yardangs, Badlands, Chaos, Pits, Polygons, Cracks, Spikes, Hummocks, Grooves,
        Crosshatch, Streaks,
    }

    public enum Liquid { None, Water, Lava, Methane, Brine, Sulfur }

    public enum PlumeKind { None, Steam, Geyser, Smoke, Fountain, BlackSmoker, DarkGeyser }

    /// <summary>The ready-made shapes that can be stood on a planet. Each has several variants.</summary>
    public enum FeatureKind
    {
        Boulder, Slab, RockPile, Hoodoo, Spire, Arch, BalancedRock, Tor, BasaltColumns, Crystal, IceShard,
        Stalagmite, Conifer, Broadleaf, Palm, Bush, Cactus, VentChimney, Iceberg,
    }

    /// <summary>Where on a body a formation is allowed to appear.</summary>
    public enum Where { Any, Land, Sea, Coast, Lowland, Highland, Polar, SouthPole, Equatorial }

    /// <summary>
    /// One kind of natural formation, described as data: what it is in the real world, and the numbers that
    /// shape it here. Sizes are metres on an Earth-sized body and shrink on smaller ones. The full list is
    /// <see cref="FormationIndex"/>. No Unity types.
    /// </summary>
    public sealed class Formation
    {
        public readonly string Id;
        public readonly string Name;
        public readonly FormationCategory Category;
        public readonly Technique Technique;
        /// <summary>What it is and where it is found for real, in one line.</summary>
        public readonly string Description;

        /// <summary>Radius in metres. For curves and caves this is the length.</summary>
        public double MinSize = 10, MaxSize = 20;
        /// <summary>
        /// Main height as a fraction of size. Craters: depth (positive). Cones, domes and plateaus: height,
        /// negative for a hollow. Curves: height as a fraction of width, negative for a cut.
        /// </summary>
        public double Relief;

        // Craters.
        /// <summary>Rim height as a fraction of radius.</summary>
        public double Rim = 0.06;
        /// <summary>How much of the radius is flat floor.</summary>
        public double Floor;
        /// <summary>Central peak (or peak ring) height as a fraction of depth.</summary>
        public double Peak;
        /// <summary>Where the peak ring sits, as a fraction of radius. 0 means a single central peak.</summary>
        public double Ring;
        public int OuterRings;
        public int Rays;
        public double Elongation = 1;
        public double Pedestal;
        public double Rampart;

        // Cones.
        /// <summary>1 is a straight-sided cone, below 1 bulges like a shield, above 1 sweeps up like a classic volcano.</summary>
        public double Power = 1;
        /// <summary>Radius of the pit at the top, as a fraction of the base radius.</summary>
        public double Summit;
        public double SummitDepth;

        // Plateaus, domes, regions.
        /// <summary>How much of the radius the side wall takes up. Small is a cliff.</summary>
        public double Steepness = 0.15;
        /// <summary>How wobbly the outline is. 0 is a perfect circle.</summary>
        public double EdgeNoise = 0.3;
        /// <summary>Adds sharp ridges to the shape, for mountains. 0 to 1.</summary>
        public double Rugged;
        /// <summary>How much the ordinary hills are smoothed away inside the formation. 0 to 1.</summary>
        public double Flatten;

        // Curves.
        public double MinWidth = 10, MaxWidth = 20;
        public CurveProfile Profile = CurveProfile.V;
        /// <summary>How far it wanders from a straight line, in widths.</summary>
        public double Meander = 0.5;
        public int Branches;
        public int Parallel = 1;

        // Regions.
        public RegionTexture Texture = RegionTexture.Flat;
        /// <summary>Distance from one bump of the pattern to the next, in metres.</summary>
        public double TextureScale = 20;
        /// <summary>Height of the pattern, in metres.</summary>
        public double TextureHeight = 2;

        /// <summary>Colour laid over the ground, as a web colour. Empty for none.</summary>
        public string Tint = "";
        public double TintStrength = 0.6;

        public Liquid Liquid = Liquid.None;
        /// <summary>How full the hollow is, as a fraction of its depth.</summary>
        public double Fill = 0.5;

        public PlumeKind Plume = PlumeKind.None;
        public int Plumes = 1;

        // Scatters.
        public FeatureKind Feature;
        public int MinCount = 5, MaxCount = 10;
        /// <summary>Height of each piece in metres.</summary>
        public double MinScale = 1, MaxScale = 3;
        /// <summary>True for things that float on the sea instead of standing on the ground.</summary>
        public bool Floats;

        /// <summary>Makes a line of this many smaller copies (crater chains).</summary>
        public int Chain;
        /// <summary>Makes a loose group of this many smaller copies.</summary>
        public int Cluster;

        public Where Where = Where.Any;

        /// <summary>Another formation always placed with this one, at the same spot.</summary>
        public string Companion = "";
        public double CompanionSize = 0.55;

        public Formation(string id, string name, FormationCategory category, Technique technique, string description)
        {
            Id = id;
            Name = name;
            Category = category;
            Technique = technique;
            Description = description;
        }
    }

    /// <summary>One line of a body's shopping list: which formation, and how many.</summary>
    [Serializable]
    public sealed class FormationUse
    {
        public string formation = "";
        public int count = 1;
        /// <summary>
        /// True for a landmark that is in the same place every run (Mars always has its great canyon).
        /// False for things placed by the run seed.
        /// </summary>
        public bool landmark;
        /// <summary>For landmarks: where it is, in degrees. Latitude 90 is the north pole.</summary>
        public double latitude;
        public double longitude;
        /// <summary>For landmarks: size in metres. 0 uses the formation's own range.</summary>
        public double size;
        /// <summary>For long landmarks: compass direction it runs in, in degrees.</summary>
        public double bearing;

        public FormationUse()
        {
        }

        public FormationUse(string formation, int count)
        {
            this.formation = formation;
            this.count = count;
        }

        public static FormationUse Landmark(string formation, double latitude, double longitude, double size = 0, double bearing = 90)
        {
            return new FormationUse
            {
                formation = formation, count = 1, landmark = true, latitude = latitude, longitude = longitude,
                size = size, bearing = bearing,
            };
        }
    }
}
