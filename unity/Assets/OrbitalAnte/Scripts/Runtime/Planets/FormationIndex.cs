using System;
using System.Collections.Generic;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// The index of natural formations: every kind of crater, volcano, canyon, dune, lake, ice field, cave and
    /// sky feature the game knows how to make. Each body picks from this list (see <see cref="PlanetProfiles"/>).
    /// To add a formation, add a line here and list it on at least one body.
    /// docs/formation_index.md is the same list written out for reading.
    /// </summary>
    public static class FormationIndex
    {
        const FormationCategory Impact = FormationCategory.Impact;
        const FormationCategory Volcanic = FormationCategory.Volcanic;
        const FormationCategory Tectonic = FormationCategory.Tectonic;
        const FormationCategory Wind = FormationCategory.WindAndErosion;
        const FormationCategory Water = FormationCategory.RiversAndWater;
        const FormationCategory Ice = FormationCategory.Ice;
        const FormationCategory Caves = FormationCategory.Caves;
        const FormationCategory Sky = FormationCategory.Sky;
        const FormationCategory Plants = FormationCategory.Vegetation;

        static List<Formation> _all;
        static Dictionary<string, Formation> _byId;

        public static IReadOnlyList<Formation> All
        {
            get
            {
                if (_all == null) Build();
                return _all;
            }
        }

        public static Formation Get(string id)
        {
            if (_all == null) Build();
            return id != null && _byId.TryGetValue(id, out Formation f) ? f : null;
        }

        static void Build()
        {
            var list = new List<Formation>();
            AddImpact(list);
            AddVolcanic(list);
            AddTectonic(list);
            AddWind(list);
            AddWater(list);
            AddIce(list);
            AddCaves(list);
            AddSky(list);
            AddPlants(list);
            var byId = new Dictionary<string, Formation>(StringComparer.Ordinal);
            foreach (Formation f in list) byId[f.Id] = f;
            _byId = byId;
            _all = list;
        }

        static Formation F(string id, string name, FormationCategory category, Technique technique, string description)
        {
            return new Formation(id, name, category, technique, description);
        }

        // ------------------------------------------------------------------ impact

        static void AddImpact(List<Formation> l)
        {
            l.Add(new Formation("simple_crater", "Simple crater", Impact, Technique.Crater,
                "A plain bowl with a raised rim. The commonest landform in the solar system.")
                { MinSize = 7, MaxSize = 24, Relief = 0.42, Rim = 0.09 });
            l.Add(new Formation("complex_crater", "Complex crater", Impact, Technique.Crater,
                "A big crater with a flat floor, stepped walls and a central peak, like Tycho or Copernicus.")
                { MinSize = 30, MaxSize = 58, Relief = 0.24, Rim = 0.07, Floor = 0.45, Peak = 0.55 });
            l.Add(new Formation("peak_ring_basin", "Peak-ring basin", Impact, Technique.Crater,
                "Larger still: the central peak becomes a ring of mountains, like Schrodinger on the Moon.")
                { MinSize = 60, MaxSize = 105, Relief = 0.15, Rim = 0.05, Floor = 0.62, Peak = 0.45, Ring = 0.5 });
            l.Add(new Formation("multi_ring_basin", "Multi-ring basin", Impact, Technique.Crater,
                "A giant impact scar ringed by several mountain rims, like Orientale or Caloris.")
                { MinSize = 110, MaxSize = 190, Relief = 0.09, Rim = 0.04, Floor = 0.7, OuterRings = 3, Flatten = 0.6 });
            l.Add(new Formation("rayed_crater", "Rayed crater", Impact, Technique.Crater,
                "A young crater with bright streaks of thrown-out dust reaching far across the ground.")
                { MinSize = 12, MaxSize = 28, Relief = 0.4, Rim = 0.09, Rays = 11, Tint = "#F2EEE4", TintStrength = 0.55 });
            l.Add(new Formation("ghost_crater", "Ghost crater", Impact, Technique.Crater,
                "An old crater drowned by lava so only a faint ring of rim still shows.")
                { MinSize = 25, MaxSize = 55, Relief = 0.02, Rim = 0.035, Floor = 0.8 });
            l.Add(new Formation("pedestal_crater", "Pedestal crater", Impact, Technique.Crater,
                "A crater left standing on a raised platform after the softer ground around it wore away. Common on Mars.")
                { MinSize = 10, MaxSize = 22, Relief = 0.3, Rim = 0.08, Pedestal = 0.7 });
            l.Add(new Formation("rampart_crater", "Rampart crater", Impact, Technique.Crater,
                "A crater surrounded by a lobed apron, as if it splashed into mud. A sign of buried ice on Mars.")
                { MinSize = 14, MaxSize = 28, Relief = 0.36, Rim = 0.08, Rampart = 0.3 });
            l.Add(new Formation("crater_chain", "Crater chain", Impact, Technique.Crater,
                "A neat line of small craters, made when a broken-up comet hits one piece after another.")
                { MinSize = 5, MaxSize = 9, Relief = 0.4, Rim = 0.08, Chain = 7 });
            l.Add(new Formation("secondary_craters", "Secondary crater field", Impact, Technique.Crater,
                "A scatter of small pits dug by lumps thrown out of a bigger impact.")
                { MinSize = 3, MaxSize = 6, Relief = 0.35, Rim = 0.06, Cluster = 12 });
            l.Add(new Formation("dark_halo_crater", "Dark-halo crater", Impact, Technique.Crater,
                "A small crater that dug up darker rock from below and spread it around itself.")
                { MinSize = 8, MaxSize = 16, Relief = 0.4, Rim = 0.08, Tint = "#2E2A28", TintStrength = 0.6 });
            l.Add(new Formation("palimpsest", "Palimpsest", Impact, Technique.Region,
                "The pale flat ghost of an ancient crater on an icy moon, its shape long since relaxed away.")
                { MinSize = 40, MaxSize = 80, Texture = RegionTexture.Flat, Flatten = 0.8, Tint = "#D8D2C6", TintStrength = 0.5, EdgeNoise = 0.15 });
            l.Add(new Formation("fractured_floor_crater", "Floor-fractured crater", Impact, Technique.Crater,
                "A crater whose floor was pushed up from below and cracked into plates.")
                { MinSize = 30, MaxSize = 50, Relief = 0.16, Rim = 0.07, Floor = 0.7, Companion = "polygon_cracks", CompanionSize = 0.6 });
            l.Add(new Formation("elongated_crater", "Elongated crater", Impact, Technique.Crater,
                "A stretched crater left by something that came in at a very shallow angle.")
                { MinSize = 10, MaxSize = 22, Relief = 0.35, Rim = 0.08, Elongation = 2.3 });
            l.Add(new Formation("concentric_crater", "Concentric crater", Impact, Technique.Crater,
                "A crater with a second ring inside it, like a doughnut. Seen on the Moon.")
                { MinSize = 14, MaxSize = 24, Relief = 0.3, Rim = 0.08, Floor = 0.3, Peak = 0.5, Ring = 0.55 });
            l.Add(new Formation("regolith_garden", "Micro-crater garden", Impact, Technique.Region,
                "Ground churned by countless tiny impacts into a field of overlapping dimples.")
                { MinSize = 40, MaxSize = 90, Texture = RegionTexture.Pits, TextureScale = 5, TextureHeight = 0.7, EdgeNoise = 0.5 });
            l.Add(new Formation("valhalla_rings", "Ringed impact scar", Impact, Technique.Crater,
                "A bright patch surrounded by many concentric ripples frozen into ice, like Valhalla on Callisto.")
                { MinSize = 45, MaxSize = 70, Relief = 0.01, Rim = 0.03, Floor = 0.9, OuterRings = 6, Tint = "#CFC8BA", TintStrength = 0.5 });
        }

        // ------------------------------------------------------------------ volcanic

        static void AddVolcanic(List<Formation> l)
        {
            l.Add(new Formation("shield_volcano", "Shield volcano", Volcanic, Technique.Cone,
                "A huge, gently sloping volcano built from runny lava, like Olympus Mons or Mauna Loa.")
                { MinSize = 110, MaxSize = 190, Relief = 0.36, Power = 0.7, Summit = 0.16, SummitDepth = 0.13, Flatten = 0.8 });
            l.Add(new Formation("stratovolcano", "Stratovolcano", Volcanic, Technique.Cone,
                "The classic steep, pointed volcano, like Mount Fuji.")
                { MinSize = 50, MaxSize = 85, Relief = 0.8, Power = 1.7, Summit = 0.09, SummitDepth = 0.14, Flatten = 0.7 });
            l.Add(new Formation("cinder_cone", "Cinder cone", Volcanic, Technique.Cone,
                "A small steep heap of volcanic gravel with a bowl at the top.")
                { MinSize = 10, MaxSize = 20, Relief = 0.6, Summit = 0.32, SummitDepth = 0.4, Tint = "#3A2A26", TintStrength = 0.5 });
            l.Add(new Formation("caldera", "Caldera", Volcanic, Technique.Cone,
                "A wide cauldron left where a volcano's top collapsed into its emptied chamber.")
                { MinSize = 40, MaxSize = 75, Relief = 0.22, Power = 0.8, Summit = 0.55, SummitDepth = 0.9, Flatten = 0.6 });
            l.Add(new Formation("lava_dome", "Lava dome", Volcanic, Technique.Dome,
                "A rounded mound of lava too thick to flow away.")
                { MinSize = 12, MaxSize = 26, Relief = 0.45, EdgeNoise = 0.2, Flatten = 0.6 });
            l.Add(new Formation("pancake_dome", "Pancake dome", Volcanic, Technique.Plateau,
                "A wide, flat, steep-edged disc of thick lava. Found on Venus.")
                { MinSize = 28, MaxSize = 55, Relief = 0.13, Steepness = 0.1, EdgeNoise = 0.08, Flatten = 0.9 });
            l.Add(new Formation("corona", "Corona", Volcanic, Technique.Crater,
                "A great ring of ridges pushed up by hot rock rising underneath. Found on Venus.")
                { MinSize = 55, MaxSize = 95, Relief = 0.03, Rim = 0.07, Floor = 0.75, OuterRings = 1 });
            l.Add(new Formation("tessera", "Tessera", Volcanic, Technique.Region,
                "Old crumpled highland crossed by ridges in two directions, like a tiled floor. Found on Venus.")
                { MinSize = 70, MaxSize = 130, Texture = RegionTexture.Crosshatch, TextureScale = 15, TextureHeight = 4.5, Relief = 0.05 });
            l.Add(new Formation("lava_plain", "Lava plain (mare)", Volcanic, Technique.Dome,
                "A dark, low, smooth plain of flooded lava. The dark patches on the Moon are these.")
                { MinSize = 80, MaxSize = 170, Relief = -0.07, Flatten = 0.85, EdgeNoise = 0.5, Tint = "#4A4A50", TintStrength = 0.7 });
            l.Add(new Formation("sinuous_rille", "Sinuous rille", Volcanic, Technique.Curve,
                "A winding channel cut by flowing lava, like Hadley Rille where Apollo 15 landed.")
                { MinSize = 160, MaxSize = 380, MinWidth = 6, MaxWidth = 10, Relief = -0.55, Profile = CurveProfile.U, Floor = 0.4, Meander = 2.6 });
            l.Add(new Formation("lava_tube_skylight", "Lava tube skylight", Caves, Technique.Cave,
                "A hole where the roof of an old lava tunnel fell in. Real ones are known on the Moon and Mars.")
                { MinSize = 55, MaxSize = 100, MinWidth = 4, MaxWidth = 5.5 });
            l.Add(new Formation("lava_lake", "Lava lake", Volcanic, Technique.Cone,
                "A pool of molten rock held in a low volcanic crater.")
                { MinSize = 24, MaxSize = 42, Relief = 0.14, Power = 0.8, Summit = 0.62, SummitDepth = 0.6, Liquid = Liquid.Lava, Fill = 0.45, Tint = "#241A18", TintStrength = 0.6 });
            l.Add(new Formation("patera", "Patera", Volcanic, Technique.Plateau,
                "A steep-walled, flat-floored volcanic pit with a ragged outline. Io is covered in them.")
                { MinSize = 22, MaxSize = 45, Relief = -0.22, Steepness = 0.1, EdgeNoise = 0.6, Flatten = 0.9, Liquid = Liquid.Sulfur, Fill = 0.3, Tint = "#2A2420", TintStrength = 0.7 });
            l.Add(new Formation("sulfur_vent_field", "Sulphur vent field", Volcanic, Technique.Region,
                "Lumpy yellow ground around vents that breathe out sulphur.")
                { MinSize = 25, MaxSize = 50, Texture = RegionTexture.Hummocks, TextureScale = 7, TextureHeight = 1.6, Tint = "#E8D23A", TintStrength = 0.7, Plume = PlumeKind.Smoke, Plumes = 3 });
            l.Add(new Formation("volcanic_plume", "Volcanic plume", Volcanic, Technique.Cone,
                "A vent throwing a tall umbrella of gas and dust high above the ground, as on Io.")
                { MinSize = 14, MaxSize = 24, Relief = 0.3, Summit = 0.35, SummitDepth = 0.6, Plume = PlumeKind.Fountain, Tint = "#C8452A", TintStrength = 0.5 });
            l.Add(new Formation("cryovolcano", "Cryovolcano", Volcanic, Technique.Cone,
                "A volcano that erupts slushy ice in place of lava.")
                { MinSize = 38, MaxSize = 65, Relief = 0.32, Power = 0.9, Summit = 0.25, SummitDepth = 0.4, Plume = PlumeKind.Steam, Tint = "#E4ECF2", TintStrength = 0.4 });
            l.Add(new Formation("geyser_field", "Geyser field", Volcanic, Technique.Region,
                "A patch of vents that shoot jets of water and steam.")
                { MinSize = 18, MaxSize = 35, Texture = RegionTexture.Hummocks, TextureScale = 6, TextureHeight = 0.8, Plume = PlumeKind.Geyser, Plumes = 4, Tint = "#D9D2C4", TintStrength = 0.4 });
            l.Add(new Formation("lava_flow_field", "Lava flow field", Volcanic, Technique.Region,
                "Dark, ropy, lobed ground where lava spread out and froze.")
                { MinSize = 45, MaxSize = 90, Texture = RegionTexture.Hummocks, TextureScale = 16, TextureHeight = 2.2, Tint = "#26211F", TintStrength = 0.75, EdgeNoise = 0.7 });
            l.Add(new Formation("columnar_basalt", "Columnar basalt", Volcanic, Technique.Scatter,
                "Six-sided stone pillars formed as thick lava cooled and cracked, like the Giant's Causeway.")
                { MinSize = 10, MaxSize = 18, Feature = FeatureKind.BasaltColumns, MinCount = 5, MaxCount = 9, MinScale = 3, MaxScale = 8 });
            l.Add(new Formation("maar", "Maar", Volcanic, Technique.Crater,
                "A low-rimmed crater blasted out by a steam explosion and often filled with a lake.")
                { MinSize = 14, MaxSize = 26, Relief = 0.3, Rim = 0.03, Floor = 0.5, Liquid = Liquid.Water, Fill = 0.6 });
            l.Add(new Formation("fumarole_field", "Fumarole field", Volcanic, Technique.Region,
                "Pitted ground dotted with steaming vents.")
                { MinSize = 14, MaxSize = 28, Texture = RegionTexture.Pits, TextureScale = 6, TextureHeight = 1, Plume = PlumeKind.Steam, Plumes = 4, Tint = "#CBB98A", TintStrength = 0.4 });
            l.Add(new Formation("volcanic_plug", "Volcanic plug", Volcanic, Technique.Plateau,
                "The hard core of a dead volcano left standing as a tower after the cone eroded, like Devils Tower.")
                { MinSize = 7, MaxSize = 12, Relief = 2.3, Steepness = 0.22, EdgeNoise = 0.15, Flatten = 0.5 });
            l.Add(new Formation("arachnoid", "Arachnoid", Volcanic, Technique.Crater,
                "A shallow ring with cracks radiating from it like a spider's web. Found on Venus.")
                { MinSize = 22, MaxSize = 40, Relief = 0.04, Rim = 0.04, Floor = 0.6, Rays = 16, Tint = "#3B2A20", TintStrength = 0.45 });
            l.Add(new Formation("shield_field", "Shield field", Volcanic, Technique.Cone,
                "A crowd of small, low volcanoes.")
                { MinSize = 8, MaxSize = 14, Relief = 0.2, Power = 0.8, Summit = 0.2, SummitDepth = 0.3, Cluster = 8 });
        }

        // ------------------------------------------------------------------ tectonic

        static void AddTectonic(List<Formation> l)
        {
            l.Add(new Formation("rift_valley", "Rift valley", Tectonic, Technique.Curve,
                "A long, wide valley where the crust is pulling apart, like the East African Rift.")
                { MinSize = 400, MaxSize = 850, MinWidth = 40, MaxWidth = 65, Relief = -0.45, Profile = CurveProfile.U, Floor = 0.5, Meander = 0.4, Branches = 2 });
            l.Add(new Formation("graben", "Graben", Tectonic, Technique.Curve,
                "A straight trench with a flat floor, dropped down between two faults.")
                { MinSize = 150, MaxSize = 330, MinWidth = 14, MaxWidth = 24, Relief = -0.45, Profile = CurveProfile.U, Floor = 0.6, Meander = 0.1 });
            l.Add(new Formation("chasma", "Chasma", Tectonic, Technique.Curve,
                "A vast, deep, branching canyon system, like Valles Marineris on Mars. Wide enough to fly through.")
                { MinSize = 500, MaxSize = 1000, MinWidth = 55, MaxWidth = 95, Relief = -0.8, Profile = CurveProfile.U, Floor = 0.35, Meander = 0.9, Branches = 4 });
            l.Add(new Formation("fossae", "Fossae", Tectonic, Technique.Curve,
                "A set of long, narrow, parallel troughs.")
                { MinSize = 180, MaxSize = 380, MinWidth = 7, MaxWidth = 11, Relief = -0.6, Profile = CurveProfile.V, Meander = 0.15, Parallel = 3 });
            l.Add(new Formation("wrinkle_ridge", "Wrinkle ridge", Tectonic, Technique.Curve,
                "A low, winding ridge where a lava plain buckled as it cooled and shrank.")
                { MinSize = 150, MaxSize = 380, MinWidth = 9, MaxWidth = 15, Relief = 0.3, Profile = CurveProfile.Ridge, Meander = 1.3 });
            l.Add(new Formation("lobate_scarp", "Lobate scarp", Tectonic, Technique.Curve,
                "A long curving cliff formed as a whole planet shrank. Mercury is wrapped in them.")
                { MinSize = 220, MaxSize = 480, MinWidth = 16, MaxWidth = 24, Relief = 0.55, Profile = CurveProfile.Scarp, Meander = 0.8 });
            l.Add(new Formation("fault_scarp", "Fault scarp", Tectonic, Technique.Curve,
                "A sharp step in the ground where one side slipped up past the other.")
                { MinSize = 80, MaxSize = 190, MinWidth = 6, MaxWidth = 9, Relief = 0.9, Profile = CurveProfile.Scarp, Meander = 0.2 });
            l.Add(new Formation("mountain_range", "Mountain range", Tectonic, Technique.Curve,
                "A long chain of jagged peaks, like the Andes or the Himalaya.")
                { MinSize = 300, MaxSize = 650, MinWidth = 55, MaxWidth = 90, Relief = 0.75, Profile = CurveProfile.Ridge, Meander = 1.1, Rugged = 1 });
            l.Add(new Formation("massif", "Massif", Tectonic, Technique.Dome,
                "A compact block of mountains standing above the land around it.")
                { MinSize = 55, MaxSize = 100, Relief = 0.6, Rugged = 0.9, EdgeNoise = 0.5 });
            l.Add(new Formation("isolated_peak", "Isolated peak", Tectonic, Technique.Cone,
                "A single enormous mountain with nothing around it. Io has them taller than Everest.")
                { MinSize = 28, MaxSize = 50, Relief = 1.15, Power = 1.3, Rugged = 0.7 });
            l.Add(new Formation("linea", "Linea", Tectonic, Technique.Curve,
                "A long, straight double ridge with a groove down the middle. They criss-cross Europa.")
                { MinSize = 300, MaxSize = 850, MinWidth = 5, MaxWidth = 8, Relief = 0.55, Profile = CurveProfile.DoubleRidge, Meander = 0.25, Tint = "#8A4B32", TintStrength = 0.6 });
            l.Add(new Formation("tiger_stripes", "Tiger stripes", Tectonic, Technique.Curve,
                "Four warm parallel cracks at the south pole of Enceladus that spray water into space.")
                { MinSize = 70, MaxSize = 100, MinWidth = 4, MaxWidth = 6, Relief = -0.9, Profile = CurveProfile.V, Meander = 0.3, Parallel = 4, Plume = PlumeKind.Geyser, Plumes = 3, Tint = "#7FB6C9", TintStrength = 0.6, Where = Where.SouthPole });
            l.Add(new Formation("grooved_terrain", "Grooved terrain", Tectonic, Technique.Region,
                "Bright bands of parallel ridges and furrows. They cover much of Ganymede.")
                { MinSize = 60, MaxSize = 120, Texture = RegionTexture.Grooves, TextureScale = 11, TextureHeight = 3, Tint = "#B9B2A6", TintStrength = 0.5 });
            l.Add(new Formation("horst", "Horst", Tectonic, Technique.Curve,
                "A long raised block left standing between two faults.")
                { MinSize = 120, MaxSize = 260, MinWidth = 24, MaxWidth = 36, Relief = 0.4, Profile = CurveProfile.Block, Meander = 0.15 });
            l.Add(new Formation("crustal_swell", "Crustal swell", Tectonic, Technique.Dome,
                "A very broad, gentle bulge in a planet's crust, like the Tharsis rise on Mars.")
                { MinSize = 200, MaxSize = 300, Relief = 0.1, EdgeNoise = 0.3 });
            l.Add(new Formation("grooves", "Grooves", Tectonic, Technique.Curve,
                "Long shallow scratches running in families across a small moon. Phobos is covered in them.")
                { MinSize = 40, MaxSize = 90, MinWidth = 2.5, MaxWidth = 4, Relief = -0.4, Profile = CurveProfile.V, Meander = 0.2, Parallel = 5 });
        }

        // ------------------------------------------------------------------ wind and erosion

        static void AddWind(List<Formation> l)
        {
            l.Add(new Formation("mesa", "Mesa", Wind, Technique.Plateau,
                "A wide, flat-topped hill with cliffs all round.")
                { MinSize = 24, MaxSize = 48, Relief = 0.5, Steepness = 0.1, EdgeNoise = 0.4, Flatten = 0.85 });
            l.Add(new Formation("butte", "Butte", Wind, Technique.Plateau,
                "A mesa worn down until it is taller than it is wide.")
                { MinSize = 8, MaxSize = 15, Relief = 1.5, Steepness = 0.16, EdgeNoise = 0.3, Flatten = 0.7 });
            l.Add(new Formation("hoodoo_field", "Hoodoo field", Wind, Technique.Scatter,
                "A crowd of tall thin rock chimneys with harder caps, like Bryce Canyon.")
                { MinSize = 25, MaxSize = 45, Feature = FeatureKind.Hoodoo, MinCount = 12, MaxCount = 26, MinScale = 4, MaxScale = 11 });
            l.Add(new Formation("rock_spires", "Rock spires", Wind, Technique.Scatter,
                "Sharp stone pinnacles standing alone or in small groups.")
                { MinSize = 20, MaxSize = 40, Feature = FeatureKind.Spire, MinCount = 5, MaxCount = 11, MinScale = 8, MaxScale = 20 });
            l.Add(new Formation("natural_arch", "Natural arch", Wind, Technique.Scatter,
                "A stone arch worn through a fin of rock. Big enough to fly under.")
                { MinSize = 6, MaxSize = 12, Feature = FeatureKind.Arch, MinCount = 1, MaxCount = 2, MinScale = 11, MaxScale = 19 });
            l.Add(new Formation("yardang_field", "Yardang field", Wind, Technique.Region,
                "Long streamlined ridges carved by steady wind, all pointing the same way.")
                { MinSize = 50, MaxSize = 100, Texture = RegionTexture.Yardangs, TextureScale = 20, TextureHeight = 4.5 });
            l.Add(new Formation("barchan_dunes", "Barchan dunes", Wind, Technique.Region,
                "Crescent-shaped dunes that march downwind with their horns leading.")
                { MinSize = 50, MaxSize = 100, Texture = RegionTexture.Dunes, TextureScale = 30, TextureHeight = 4, Flatten = 0.5, Tint = "#C9A26B", TintStrength = 0.45 });
            l.Add(new Formation("linear_dunes", "Linear dunes", Wind, Technique.Region,
                "Long straight dunes running side by side for great distances. Titan's equator is wrapped in them.")
                { MinSize = 80, MaxSize = 160, Texture = RegionTexture.Grooves, TextureScale = 24, TextureHeight = 5, Flatten = 0.6, Tint = "#3A2E22", TintStrength = 0.6 });
            l.Add(new Formation("star_dunes", "Star dunes", Wind, Technique.Region,
                "Tall pyramid dunes with several arms, built where the wind keeps changing direction.")
                { MinSize = 60, MaxSize = 110, Texture = RegionTexture.StarDunes, TextureScale = 42, TextureHeight = 9, Flatten = 0.6, Tint = "#D2A765", TintStrength = 0.45 });
            l.Add(new Formation("sand_sea", "Sand sea (erg)", Wind, Technique.Region,
                "A great expanse of nothing but dunes.")
                { MinSize = 130, MaxSize = 220, Texture = RegionTexture.Dunes, TextureScale = 36, TextureHeight = 6.5, Flatten = 0.75, Tint = "#CFA05E", TintStrength = 0.55 });
            l.Add(new Formation("badlands", "Badlands", Wind, Technique.Region,
                "Soft ground cut into a maze of sharp little ridges and gullies.")
                { MinSize = 40, MaxSize = 80, Texture = RegionTexture.Badlands, TextureScale = 17, TextureHeight = 7 });
            l.Add(new Formation("slot_canyon", "Slot canyon", Wind, Technique.Curve,
                "A canyon far deeper than it is wide, twisting through the rock.")
                { MinSize = 120, MaxSize = 240, MinWidth = 5, MaxWidth = 7, Relief = -2.6, Profile = CurveProfile.U, Floor = 0.5, Meander = 3 });
            l.Add(new Formation("inselberg", "Inselberg", Wind, Technique.Dome,
                "A lone rounded rock hill rising straight out of a plain, like Uluru.")
                { MinSize = 20, MaxSize = 38, Relief = 0.75, EdgeNoise = 0.2, Flatten = 0.6 });
            l.Add(new Formation("tors", "Tors", Wind, Technique.Scatter,
                "Stacks of rounded boulders left on hilltops.")
                { MinSize = 14, MaxSize = 26, Feature = FeatureKind.Tor, MinCount = 3, MaxCount = 6, MinScale = 4, MaxScale = 8, Where = Where.Highland });
            l.Add(new Formation("balanced_rocks", "Balanced rocks", Wind, Technique.Scatter,
                "Big rocks perched on narrow stems that look ready to fall.")
                { MinSize = 12, MaxSize = 24, Feature = FeatureKind.BalancedRock, MinCount = 2, MaxCount = 5, MinScale = 4, MaxScale = 8 });
            l.Add(new Formation("boulder_field", "Boulder field", Wind, Technique.Scatter,
                "Ground strewn with big loose rocks.")
                { MinSize = 22, MaxSize = 40, Feature = FeatureKind.Boulder, MinCount = 22, MaxCount = 40, MinScale = 0.9, MaxScale = 3.6 });
            l.Add(new Formation("talus_apron", "Talus apron", Wind, Technique.Region,
                "A rubbly skirt of broken rock.")
                { MinSize = 16, MaxSize = 30, Texture = RegionTexture.Hummocks, TextureScale = 3, TextureHeight = 0.6, Tint = "#6E6862", TintStrength = 0.35 });
            l.Add(new Formation("desert_pavement", "Desert pavement", Wind, Technique.Region,
                "A hard, dark, level floor of close-packed stones left when the wind took the sand.")
                { MinSize = 40, MaxSize = 80, Texture = RegionTexture.Polygons, TextureScale = 3, TextureHeight = 0.12, Flatten = 0.85, Tint = "#5A4636", TintStrength = 0.5 });
            l.Add(new Formation("hollows", "Hollows", Wind, Technique.Region,
                "Bright, shallow, rimless pits where the ground itself is evaporating into space. Unique to Mercury.")
                { MinSize = 18, MaxSize = 36, Texture = RegionTexture.Pits, TextureScale = 8, TextureHeight = 1.6, Tint = "#C9D6E2", TintStrength = 0.6 });
            l.Add(new Formation("dust_devil_tracks", "Dust devil tracks", Wind, Technique.Region,
                "Dark scribbles left where whirlwinds swept the bright dust off the ground.")
                { MinSize = 50, MaxSize = 100, Texture = RegionTexture.Streaks, TextureScale = 14, Tint = "#4E2A1C", TintStrength = 0.55 });
            l.Add(new Formation("ventifacts", "Ventifacts", Wind, Technique.Scatter,
                "Stones sandblasted by the wind into flat, sharp-edged faces.")
                { MinSize = 16, MaxSize = 30, Feature = FeatureKind.Slab, MinCount = 10, MaxCount = 20, MinScale = 0.8, MaxScale = 2.4 });
            l.Add(new Formation("gullies", "Gullies", Wind, Technique.Region,
                "Small fresh channels running down a slope.")
                { MinSize = 20, MaxSize = 40, Texture = RegionTexture.Badlands, TextureScale = 7, TextureHeight = 2 });
            l.Add(new Formation("tower_karst", "Tower karst", Wind, Technique.Plateau,
                "A forest of steep limestone towers, like Guilin in China.")
                { MinSize = 6, MaxSize = 11, Relief = 2.1, Steepness = 0.3, EdgeNoise = 0.25, Flatten = 0.4, Cluster = 9 });
            l.Add(new Formation("rubble_pile", "Rubble piles", Wind, Technique.Scatter,
                "Heaps of broken rock.")
                { MinSize = 14, MaxSize = 26, Feature = FeatureKind.RockPile, MinCount = 5, MaxCount = 10, MinScale = 1.5, MaxScale = 4 });
        }

        // ------------------------------------------------------------------ rivers, lakes and seas

        static void AddWater(List<Formation> l)
        {
            l.Add(new Formation("ocean", "Ocean", Water, Technique.Global,
                "Open water covering most of a world."));
            l.Add(new Formation("inland_sea", "Inland sea", Water, Technique.Dome,
                "A broad basin that dips below sea level and floods.")
                { MinSize = 90, MaxSize = 160, Relief = -0.12, EdgeNoise = 0.7, Flatten = 0.4 });
            l.Add(new Formation("lake", "Lake", Water, Technique.Dome,
                "Fresh water held in a hollow in the land.")
                { MinSize = 22, MaxSize = 55, Relief = -0.2, EdgeNoise = 0.7, Flatten = 0.6, Liquid = Liquid.Water, Fill = 0.7, Where = Where.Land });
            l.Add(new Formation("crater_lake", "Crater lake", Water, Technique.Crater,
                "A lake filling an old crater.")
                { MinSize = 16, MaxSize = 32, Relief = 0.3, Rim = 0.07, Floor = 0.4, Liquid = Liquid.Water, Fill = 0.6, Where = Where.Land });
            l.Add(new Formation("tarn", "Tarn", Water, Technique.Dome,
                "A small lake high in the mountains.")
                { MinSize = 9, MaxSize = 18, Relief = -0.3, EdgeNoise = 0.5, Flatten = 0.7, Liquid = Liquid.Water, Fill = 0.75, Where = Where.Highland });
            l.Add(new Formation("river_valley", "River valley", Water, Technique.Curve,
                "A winding V-shaped valley cut by running water, with side valleys joining it.")
                { MinSize = 300, MaxSize = 650, MinWidth = 14, MaxWidth = 22, Relief = -0.6, Profile = CurveProfile.V, Meander = 2.4, Branches = 3, Where = Where.Land });
            l.Add(new Formation("outflow_channel", "Outflow channel", Water, Technique.Curve,
                "A broad, shallow channel scoured by an ancient flood. Mars has several.")
                { MinSize = 380, MaxSize = 750, MinWidth = 38, MaxWidth = 58, Relief = -0.25, Profile = CurveProfile.U, Floor = 0.6, Meander = 0.8, Branches = 1 });
            l.Add(new Formation("dry_riverbed", "Dry riverbed", Water, Technique.Curve,
                "A small winding channel that only ever held water briefly.")
                { MinSize = 140, MaxSize = 300, MinWidth = 6, MaxWidth = 10, Relief = -0.4, Profile = CurveProfile.U, Floor = 0.5, Meander = 3 });
            l.Add(new Formation("delta", "Delta", Water, Technique.Dome,
                "A low fan of silt built where a river meets still water.")
                { MinSize = 24, MaxSize = 45, Relief = 0.04, Flatten = 0.85, EdgeNoise = 0.5, Tint = "#A89872", TintStrength = 0.5, Where = Where.Coast });
            l.Add(new Formation("alluvial_fan", "Alluvial fan", Water, Technique.Dome,
                "A sloping apron of gravel spread out at the mouth of a canyon.")
                { MinSize = 22, MaxSize = 40, Relief = 0.09, Flatten = 0.7, EdgeNoise = 0.4 });
            l.Add(new Formation("fjord", "Fjord", Water, Technique.Curve,
                "A deep, steep-walled inlet carved by a glacier and flooded by the sea.")
                { MinSize = 150, MaxSize = 300, MinWidth = 16, MaxWidth = 26, Relief = -1.2, Profile = CurveProfile.U, Floor = 0.45, Meander = 0.9, Where = Where.Coast });
            l.Add(new Formation("island", "Island", Water, Technique.Dome,
                "Land standing alone in the sea.")
                { MinSize = 30, MaxSize = 65, Relief = 0.5, Rugged = 0.5, EdgeNoise = 0.6, Where = Where.Sea });
            l.Add(new Formation("archipelago", "Archipelago", Water, Technique.Dome,
                "A scatter of small islands.")
                { MinSize = 10, MaxSize = 24, Relief = 0.9, Rugged = 0.3, EdgeNoise = 0.6, Cluster = 7, Where = Where.Sea });
            l.Add(new Formation("atoll", "Atoll", Water, Technique.Crater,
                "A ring of low sandy islands around a shallow lagoon.")
                { MinSize = 22, MaxSize = 40, Relief = 0.1, Rim = 0.08, Floor = 0.7, Pedestal = 3.2, Tint = "#E6D9A8", TintStrength = 0.6, Where = Where.Sea });
            l.Add(new Formation("lagoon", "Lagoon", Water, Technique.Dome,
                "A sheltered bight of shallow water cut into the coast.")
                { MinSize = 35, MaxSize = 70, Relief = -0.1, EdgeNoise = 0.7, Flatten = 0.5, Where = Where.Coast });
            l.Add(new Formation("hot_spring", "Hot spring", Water, Technique.Crater,
                "A small steaming pool stained bright colours by minerals.")
                { MinSize = 4, MaxSize = 8, Relief = 0.3, Rim = 0.06, Floor = 0.5, Liquid = Liquid.Brine, Fill = 0.7, Plume = PlumeKind.Steam, Tint = "#E08A2E", TintStrength = 0.5, Where = Where.Land });
            l.Add(new Formation("salt_flat", "Salt flat", Water, Technique.Region,
                "A dead-level white plain of salt, cracked into polygons, where a lake dried up.")
                { MinSize = 45, MaxSize = 90, Texture = RegionTexture.Polygons, TextureScale = 5, TextureHeight = 0.15, Flatten = 0.95, Tint = "#EDE8DC", TintStrength = 0.75, Where = Where.Land });
            l.Add(new Formation("sinkhole", "Sinkhole", Water, Technique.Crater,
                "A sheer-sided round pit where the ground fell into a hollow below, often with water at the bottom.")
                { MinSize = 6, MaxSize = 11, Relief = 1.2, Rim = 0, Floor = 0.78, Liquid = Liquid.Water, Fill = 0.25, Where = Where.Land });
            l.Add(new Formation("methane_lake", "Methane lake", Water, Technique.Dome,
                "A lake of liquid natural gas. Titan's poles are dotted with them.")
                { MinSize = 18, MaxSize = 45, Relief = -0.22, EdgeNoise = 0.8, Flatten = 0.7, Liquid = Liquid.Methane, Fill = 0.7, Where = Where.Polar });
            l.Add(new Formation("methane_sea", "Methane sea", Water, Technique.Global,
                "A sea of liquid natural gas, like Kraken Mare on Titan."));
            l.Add(new Formation("seamount", "Seamount", Water, Technique.Cone,
                "An underwater volcano that never reached the surface.")
                { MinSize = 40, MaxSize = 75, Relief = 0.55, Power = 1.2, Where = Where.Sea });
            l.Add(new Formation("guyot", "Guyot", Water, Technique.Plateau,
                "A seamount with its top planed flat.")
                { MinSize = 30, MaxSize = 52, Relief = 0.5, Steepness = 0.3, Flatten = 0.8, Where = Where.Sea });
            l.Add(new Formation("ocean_trench", "Ocean trench", Water, Technique.Curve,
                "A long, very deep gash in the sea floor.")
                { MinSize = 400, MaxSize = 850, MinWidth = 40, MaxWidth = 65, Relief = -0.9, Profile = CurveProfile.V, Meander = 0.5, Where = Where.Sea });
            l.Add(new Formation("hydrothermal_vents", "Hydrothermal vents", Water, Technique.Scatter,
                "Chimneys on the sea floor pouring out hot, dark, mineral-rich water.")
                { MinSize = 10, MaxSize = 20, Feature = FeatureKind.VentChimney, MinCount = 4, MaxCount = 8, MinScale = 3, MaxScale = 9, Plume = PlumeKind.BlackSmoker, Plumes = 3, Where = Where.Sea });
            l.Add(new Formation("abyssal_hills", "Abyssal hills", Water, Technique.Region,
                "Low rolling hills covering the deep sea floor.")
                { MinSize = 80, MaxSize = 150, Texture = RegionTexture.Hummocks, TextureScale = 38, TextureHeight = 6, Where = Where.Sea });
            l.Add(new Formation("sea_stacks", "Sea stacks", Water, Technique.Scatter,
                "Pillars of rock left standing just offshore.")
                { MinSize = 14, MaxSize = 26, Feature = FeatureKind.Spire, MinCount = 3, MaxCount = 6, MinScale = 7, MaxScale = 15, Where = Where.Coast });
            l.Add(new Formation("icebergs", "Icebergs", Water, Technique.Scatter,
                "Blocks of ice adrift on the sea.")
                { MinSize = 40, MaxSize = 80, Feature = FeatureKind.Iceberg, MinCount = 6, MaxCount = 12, MinScale = 4, MaxScale = 14, Floats = true, Where = Where.Sea });
            l.Add(new Formation("submarine_canyon", "Submarine canyon", Water, Technique.Curve,
                "A winding canyon cut into the sea floor.")
                { MinSize = 250, MaxSize = 500, MinWidth = 22, MaxWidth = 34, Relief = -0.8, Profile = CurveProfile.V, Meander = 1.6, Branches = 2, Where = Where.Sea });
        }

        // ------------------------------------------------------------------ ice

        static void AddIce(List<Formation> l)
        {
            l.Add(new Formation("polar_ice_cap", "Polar ice cap", Ice, Technique.Global,
                "A permanent sheet of ice over a pole."));
            l.Add(new Formation("ice_shelf", "Ice shelf", Ice, Technique.Global,
                "A floating sheet of ice over a polar sea."));
            l.Add(new Formation("glacier", "Glacier", Ice, Technique.Dome,
                "A slow river of ice filling a high valley.")
                { MinSize = 35, MaxSize = 70, Relief = 0.06, Flatten = 0.8, EdgeNoise = 0.6, Tint = "#DCE9F2", TintStrength = 0.8, Where = Where.Highland, Companion = "crevasse_field", CompanionSize = 0.5 });
            l.Add(new Formation("crevasse_field", "Crevasse field", Ice, Technique.Region,
                "Ice split by deep, narrow cracks.")
                { MinSize = 25, MaxSize = 50, Texture = RegionTexture.Cracks, TextureScale = 10, TextureHeight = 4.5, Tint = "#CFE2F0", TintStrength = 0.5 });
            l.Add(new Formation("penitentes", "Penitentes", Ice, Technique.Region,
                "Fields of tall thin ice blades, all leaning toward the Sun.")
                { MinSize = 20, MaxSize = 40, Texture = RegionTexture.Spikes, TextureScale = 2.4, TextureHeight = 1.8, Tint = "#EEF4F8", TintStrength = 0.5 });
            l.Add(new Formation("pressure_ridges", "Pressure ridges", Ice, Technique.Region,
                "Jumbled ridges where sheets of ice have been pushed together.")
                { MinSize = 35, MaxSize = 70, Texture = RegionTexture.Badlands, TextureScale = 11, TextureHeight = 2.6, Tint = "#DDE8EE", TintStrength = 0.35 });
            l.Add(new Formation("chaos_terrain", "Chaos terrain", Ice, Technique.Region,
                "A jumble of tilted blocks where the surface broke up, shifted and refroze. Seen on Europa and Mars.")
                { MinSize = 45, MaxSize = 90, Texture = RegionTexture.Chaos, TextureScale = 22, TextureHeight = 7, Tint = "#9A6A4E", TintStrength = 0.3 });
            l.Add(new Formation("cantaloupe_terrain", "Cantaloupe terrain", Ice, Technique.Region,
                "Ground dimpled all over like the skin of a melon. Found only on Triton.")
                { MinSize = 60, MaxSize = 110, Texture = RegionTexture.Pits, TextureScale = 26, TextureHeight = 5 });
            l.Add(new Formation("nitrogen_ice_plain", "Nitrogen ice plain", Ice, Technique.Region,
                "A smooth bright plain of frozen nitrogen divided into slowly churning cells.")
                { MinSize = 70, MaxSize = 120, Texture = RegionTexture.Polygons, TextureScale = 38, TextureHeight = 1.2, Flatten = 0.9, Tint = "#F0E6DC", TintStrength = 0.7 });
            l.Add(new Formation("ice_spires", "Ice spires", Ice, Technique.Scatter,
                "Sharp towers of ice.")
                { MinSize = 20, MaxSize = 40, Feature = FeatureKind.IceShard, MinCount = 10, MaxCount = 22, MinScale = 3, MaxScale = 12 });
            l.Add(new Formation("patterned_ground", "Patterned ground", Ice, Technique.Region,
                "Frozen soil cracked into a net of polygons by freezing and thawing.")
                { MinSize = 35, MaxSize = 70, Texture = RegionTexture.Polygons, TextureScale = 6, TextureHeight = 0.4 });
            l.Add(new Formation("swiss_cheese_terrain", "Swiss cheese terrain", Ice, Technique.Region,
                "Flat-floored round pits eaten into dry ice at the south pole of Mars.")
                { MinSize = 35, MaxSize = 60, Texture = RegionTexture.Pits, TextureScale = 13, TextureHeight = 2.6, Tint = "#F1EDE6", TintStrength = 0.6, Where = Where.Polar });
            l.Add(new Formation("polar_spiders", "Polar spiders", Ice, Technique.Region,
                "Dark branching marks left by gas bursting out from under spring ice on Mars.")
                { MinSize = 25, MaxSize = 45, Texture = RegionTexture.Streaks, TextureScale = 7, Tint = "#3A2620", TintStrength = 0.5, Where = Where.Polar });
            l.Add(new Formation("ice_cliff", "Ice cliff", Ice, Technique.Curve,
                "The sheer edge of an ice sheet.")
                { MinSize = 120, MaxSize = 260, MinWidth = 7, MaxWidth = 11, Relief = 1.1, Profile = CurveProfile.Scarp, Meander = 0.9, Tint = "#DDEBF5", TintStrength = 0.6, Where = Where.Polar });
            l.Add(new Formation("ice_knobs", "Ice knobs", Ice, Technique.Scatter,
                "Bright frosty pinnacles left behind as darker ground wore away around them. Seen on Callisto.")
                { MinSize = 20, MaxSize = 40, Feature = FeatureKind.IceShard, MinCount = 8, MaxCount = 16, MinScale = 2, MaxScale = 6, Tint = "#F4F6F8" });
            l.Add(new Formation("nitrogen_geyser", "Nitrogen geyser", Ice, Technique.Region,
                "A jet of dark dust and gas that rises several kilometres on Triton, leaving a long streak downwind.")
                { MinSize = 22, MaxSize = 40, Texture = RegionTexture.Streaks, TextureScale = 9, Tint = "#2C2422", TintStrength = 0.6, Plume = PlumeKind.DarkGeyser, Plumes = 1 });
            l.Add(new Formation("polygon_cracks", "Polygon cracks", Ice, Technique.Region,
                "Ground broken into plates by a net of cracks.")
                { MinSize = 15, MaxSize = 35, Texture = RegionTexture.Cracks, TextureScale = 8, TextureHeight = 1.2 });
            l.Add(new Formation("diamond_field", "Diamond field", Ice, Technique.Scatter,
                "Boulder-sized diamonds, thought to hail out of the sky deep inside the ice giants.")
                { MinSize = 18, MaxSize = 34, Feature = FeatureKind.Crystal, MinCount = 8, MaxCount = 16, MinScale = 1, MaxScale = 4.5, Tint = "#BFF4F0" });
            l.Add(new Formation("ice_boulders", "Ice boulders", Ice, Technique.Scatter,
                "House-sized blocks of ice lying on the surface.")
                { MinSize = 18, MaxSize = 34, Feature = FeatureKind.Boulder, MinCount = 14, MaxCount = 28, MinScale = 0.8, MaxScale = 3.2, Tint = "#E6EEF4" });
        }

        // ------------------------------------------------------------------ caves

        static void AddCaves(List<Formation> l)
        {
            l.Add(new Formation("cave", "Cave", Caves, Technique.Cave,
                "A tunnel leading down from a hole in the ground to a dead end.")
                { MinSize = 45, MaxSize = 80, MinWidth = 3.6, MaxWidth = 5 });
            l.Add(new Formation("through_cave", "Through cave", Caves, Technique.Cave,
                "A tunnel with two ways in. Go down one hole and come up from another.")
                { MinSize = 80, MaxSize = 140, MinWidth = 4, MaxWidth = 5.5, Parallel = 2 });
            l.Add(new Formation("cavern", "Cavern", Caves, Technique.Cave,
                "A tunnel that opens into a great underground hall, with stone spikes rising from its floor.")
                { MinSize = 50, MaxSize = 90, MinWidth = 4, MaxWidth = 5, Rugged = 1, Feature = FeatureKind.Stalagmite, MinCount = 6, MaxCount = 12, MinScale = 1.5, MaxScale = 4.5 });
            l.Add(new Formation("ice_cave", "Ice cave", Caves, Technique.Cave,
                "A blue-walled tunnel melted through ice.")
                { MinSize = 45, MaxSize = 85, MinWidth = 3.6, MaxWidth = 5, Tint = "#9CCBE8", TintStrength = 0.8 });
            l.Add(new Formation("crystal_grotto", "Crystal grotto", Caves, Technique.Cave,
                "An underground hall lined with giant crystals, like the Cave of the Crystals in Mexico.")
                { MinSize = 45, MaxSize = 75, MinWidth = 4, MaxWidth = 5, Rugged = 1, Feature = FeatureKind.Crystal, MinCount = 8, MaxCount = 14, MinScale = 1.5, MaxScale = 5 });
            l.Add(new Formation("natural_bridge", "Natural bridge", Caves, Technique.Scatter,
                "A great span of rock left when the cave around it collapsed.")
                { MinSize = 4, MaxSize = 8, Feature = FeatureKind.Arch, MinCount = 1, MaxCount = 1, MinScale = 22, MaxScale = 30 });
        }

        // ------------------------------------------------------------------ sky

        static void AddSky(List<Formation> l)
        {
            l.Add(new Formation("cloud_bands", "Cloud bands", Sky, Technique.Sky,
                "Light and dark stripes of cloud running round a giant planet."));
            l.Add(new Formation("great_storm", "Great storm", Sky, Technique.Sky,
                "A single enormous storm that lasts for centuries, like Jupiter's Great Red Spot."));
            l.Add(new Formation("polar_hexagon", "Polar hexagon", Sky, Technique.Sky,
                "A six-sided jet stream circling the north pole of Saturn."));
            l.Add(new Formation("thunderstorms", "Thunderstorms", Sky, Technique.Sky,
                "Drifting storms with rain and lightning."));
            l.Add(new Formation("dust_storms", "Dust storms", Sky, Technique.Sky,
                "Walls of blowing dust that can hide the ground for days."));
            l.Add(new Formation("methane_rain", "Methane rain", Sky, Technique.Sky,
                "Slow, heavy drops of liquid natural gas falling through a thick orange sky."));
            l.Add(new Formation("diamond_rain", "Diamond rain", Sky, Technique.Sky,
                "Deep inside the ice giants, the pressure is thought to squeeze carbon into diamonds that fall like hail."));
            l.Add(new Formation("acid_storms", "Acid storms", Sky, Technique.Sky,
                "Storms of sulphuric acid cloud with yellow lightning."));
            l.Add(new Formation("blizzards", "Blizzards", Sky, Technique.Sky,
                "Driving snow."));
        }

        // ------------------------------------------------------------------ vegetation

        static void AddPlants(List<Formation> l)
        {
            l.Add(new Formation("conifer_forest", "Conifer forest", Plants, Technique.Scatter,
                "Dark, pointed evergreen trees. Earth only.")
                { MinSize = 35, MaxSize = 70, Feature = FeatureKind.Conifer, MinCount = 30, MaxCount = 60, MinScale = 5, MaxScale = 11, Where = Where.Land });
            l.Add(new Formation("broadleaf_forest", "Broadleaf forest", Plants, Technique.Scatter,
                "Round-topped leafy trees. Earth only.")
                { MinSize = 35, MaxSize = 70, Feature = FeatureKind.Broadleaf, MinCount = 26, MaxCount = 50, MinScale = 5, MaxScale = 10, Where = Where.Land });
            l.Add(new Formation("palm_grove", "Palm grove", Plants, Technique.Scatter,
                "Palms along a warm shore. Earth only.")
                { MinSize = 18, MaxSize = 34, Feature = FeatureKind.Palm, MinCount = 8, MaxCount = 18, MinScale = 5, MaxScale = 9, Where = Where.Coast });
            l.Add(new Formation("scrubland", "Scrubland", Plants, Technique.Scatter,
                "Low tough bushes. Earth only.")
                { MinSize = 30, MaxSize = 55, Feature = FeatureKind.Bush, MinCount = 20, MaxCount = 40, MinScale = 0.8, MaxScale = 2, Where = Where.Land });
            l.Add(new Formation("cactus_stand", "Cactus stand", Plants, Technique.Scatter,
                "Tall branching cacti. Earth only.")
                { MinSize = 25, MaxSize = 45, Feature = FeatureKind.Cactus, MinCount = 8, MaxCount = 18, MinScale = 2.5, MaxScale = 6, Where = Where.Land });
        }
    }
}
