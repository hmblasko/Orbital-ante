using System.Collections.Generic;

namespace OrbitalAnte.Planets
{
    /// <summary>
    /// The built-in surface of every body: what makes Mars Mars and Europa Europa. These are the starting
    /// values; the Planets asset holds a copy that can be changed in the Inspector.
    ///
    /// Each body lists formations from <see cref="FormationIndex"/>. "Landmark" entries are in the same place
    /// every run, so a world is recognisable. The rest are placed by the run seed.
    /// </summary>
    public static class PlanetProfiles
    {
        static FormationUse U(string id, int count) => new FormationUse(id, count);

        static FormationUse L(string id, double latitude, double longitude, double size = 0, double bearing = 90)
        {
            return FormationUse.Landmark(id, latitude, longitude, size, bearing);
        }

        public static List<PlanetProfile> Defaults()
        {
            return new List<PlanetProfile>
            {
                Mercury(), Venus(), Earth(), Moon(), Mars(), Phobos(), Deimos(), Jupiter(), Io(), Europa(),
                Ganymede(), Callisto(), Saturn(), Enceladus(), Titan(), Uranus(), Titania(), Neptune(), Triton(),
            };
        }

        static PlanetProfile Mercury() => new PlanetProfile
        {
            bodyId = "Mercury",
            continentHeight = 12, continentWavelength = 320, hillHeight = 5, hillWavelength = 70,
            mountainHeight = 16, mountainWavelength = 150, mountainCoverage = 0.2, detailHeight = 0.4, detailWavelength = 7,
            lowColor = "#5E5A57", midColor = "#857F78", highColor = "#A39B90", cliffColor = "#4A4643", rockColor = "#6F6963",
            midHeight = 4, highHeight = 16,
            zones = { new ColorZone("#6B5A4C", 260, 0.05, 0.5), new ColorZone("#9A948C", 120, 0.25, 0.4) },
            formations =
            {
                L("multi_ring_basin", 30, -160, 95), U("lobate_scarp", 6), U("hollows", 6), U("rayed_crater", 4),
                U("simple_crater", 70), U("complex_crater", 6), U("peak_ring_basin", 1), U("ghost_crater", 3),
                U("secondary_craters", 5), U("wrinkle_ridge", 3), U("elongated_crater", 2), U("lava_plain", 2),
                U("boulder_field", 6), U("regolith_garden", 3), U("crater_chain", 2), U("lava_tube_skylight", 1),
            },
            scatter = { new ScatterRule(FeatureKind.Boulder, 9, 0.3, 1.4), new ScatterRule(FeatureKind.Slab, 4, 0.4, 1.2) },
        };

        static PlanetProfile Venus() => new PlanetProfile
        {
            bodyId = "Venus",
            continentHeight = 16, continentWavelength = 520, hillHeight = 6, hillWavelength = 100,
            mountainHeight = 30, mountainWavelength = 240, mountainCoverage = 0.18, detailHeight = 0.5, detailWavelength = 9,
            lowColor = "#4A3F36", midColor = "#6E5A44", highColor = "#9A8460", cliffColor = "#2E2620", rockColor = "#4E4238",
            midHeight = 4, highHeight = 22, strata = 3,
            zones = { new ColorZone("#2A2420", 300, 0.1, 0.6), new ColorZone("#B89A5A", 180, 0.3, 0.4).Heights(10, 1000) },
            hasAtmosphere = true, skyColor = "#E6D27A", hazeColor = "#D9C46A", atmosphereHeight = 240, visibility = 280,
            skyOpacity = 1, sunDimming = 0.5, airDrag = 0.12,
            weather = { new WeatherSpec(WeatherKind.Acid, 4, 200, 14, "#D9C24A", "#F4E04A", 60) },
            formations =
            {
                L("shield_volcano", 2, 165, 150), U("pancake_dome", 7), U("corona", 4), U("tessera", 3), U("arachnoid", 4),
                U("shield_field", 3), U("lava_flow_field", 6), U("sinuous_rille", 3), U("rift_valley", 1),
                U("simple_crater", 6), U("lava_dome", 5), U("fumarole_field", 3), U("wrinkle_ridge", 4),
                U("rubble_pile", 6), U("volcanic_plug", 2), U("caldera", 2), U("acid_storms", 1),
            },
            scatter = { new ScatterRule(FeatureKind.Slab, 10, 0.4, 1.6), new ScatterRule(FeatureKind.Boulder, 5, 0.4, 1.3) },
        };

        static PlanetProfile Earth() => new PlanetProfile
        {
            bodyId = "Earth",
            continentHeight = 30, continentWavelength = 620, continentBias = 0.5, hillHeight = 7, hillWavelength = 90,
            mountainHeight = 55, mountainWavelength = 260, mountainCoverage = 0.28, detailHeight = 0.45, detailWavelength = 8,
            hasSea = true, seaLevel = 0, seaColor = "#2274B8", seaDeepColor = "#0A2C5C", underwaterVisibility = 110,
            lowColor = "#4E8A3A", midColor = "#6E8E46", highColor = "#8A8474", cliffColor = "#6A6258", rockColor = "#7B756C",
            shoreColor = "#D8C68E", midHeight = 14, highHeight = 38, peakHeight = 52, peakColor = "#F4F6F8",
            poleColor = "#F2F5F8", poleLatitude = 0.9, strata = 2.5,
            zones =
            {
                new ColorZone("#D2B078", 260, -0.05, 0.85).Latitudes(0.18, 0.5).Heights(1, 30),   // deserts
                new ColorZone("#2F6A30", 180, 0.0, 0.7).Latitudes(0, 0.75).Heights(2, 30),        // forest green
                new ColorZone("#9A9C82", 150, 0.1, 0.6).Latitudes(0.68, 1).Heights(1, 60),        // tundra
            },
            hasAtmosphere = true, skyColor = "#6EB5FF", hazeColor = "#BFD9F2", atmosphereHeight = 170, visibility = 1500,
            skyOpacity = 0.93,
            weather =
            {
                new WeatherSpec(WeatherKind.Rain, 5, 170, 10, "#AEB9C6", "#FFFFFF", 80),
                new WeatherSpec(WeatherKind.Snow, 2, 150, 12, "#F2F5F8", "", 50) { latitude = 75 },
            },
            formations =
            {
                U("ocean", 1), U("polar_ice_cap", 1), U("ice_shelf", 1), U("inland_sea", 2), L("mountain_range", 35, 80, 600, 100), U("mountain_range", 2),
                L("rift_valley", -4, 36, 520, 10), U("river_valley", 4), U("fjord", 3), U("island", 4), U("archipelago", 2),
                U("atoll", 2), U("lagoon", 2), U("lake", 5), U("tarn", 3), U("crater_lake", 1), U("maar", 1),
                U("stratovolcano", 2), U("cinder_cone", 3), U("mesa", 5), U("butte", 6), U("hoodoo_field", 3),
                U("natural_arch", 3), U("natural_bridge", 1), U("slot_canyon", 2), U("sand_sea", 1), U("barchan_dunes", 2),
                U("star_dunes", 1), U("badlands", 2), U("inselberg", 2), U("tors", 3), U("balanced_rocks", 3),
                U("boulder_field", 6), U("tower_karst", 2), U("sinkhole", 4), U("cave", 3), U("through_cave", 2),
                U("cavern", 2), U("crystal_grotto", 1), U("glacier", 3), U("hot_spring", 4), U("geyser_field", 2),
                U("salt_flat", 1), U("delta", 3), U("alluvial_fan", 3), U("sea_stacks", 3), U("columnar_basalt", 2),
                U("seamount", 3), U("ocean_trench", 1), U("submarine_canyon", 1), U("guyot", 1), U("icebergs", 3),
                U("conifer_forest", 8), U("broadleaf_forest", 8), U("palm_grove", 4), U("scrubland", 5), U("cactus_stand", 3),
                U("desert_pavement", 1), U("talus_apron", 3), U("patterned_ground", 2), U("ice_cliff", 1),
                U("fault_scarp", 2), U("dry_riverbed", 3), U("simple_crater", 2), U("thunderstorms", 1), U("blizzards", 1),
            },
            scatter =
            {
                new ScatterRule(FeatureKind.Boulder, 6, 0.3, 1.5) { minHeight = 1 },
                new ScatterRule(FeatureKind.Bush, 8, 0.6, 1.5, "#4C7A34") { minHeight = 2, maxHeight = 34, maxSlope = 0.4 },
                new ScatterRule(FeatureKind.Conifer, 2.5, 4, 9) { minHeight = 12, maxHeight = 44, maxSlope = 0.45 },
                new ScatterRule(FeatureKind.Broadleaf, 2.5, 4, 8) { minHeight = 2, maxHeight = 22, maxSlope = 0.35 },
            },
        };

        static PlanetProfile Moon() => new PlanetProfile
        {
            bodyId = "Moon",
            continentHeight = 10, continentWavelength = 220, hillHeight = 5, hillWavelength = 60,
            mountainHeight = 14, mountainWavelength = 120, mountainCoverage = 0.22, detailHeight = 0.35, detailWavelength = 7,
            lowColor = "#7E7C78", midColor = "#A9A7A1", highColor = "#CFCDC6", cliffColor = "#77756F", rockColor = "#8E8C86",
            midHeight = 0, highHeight = 10,
            zones = { new ColorZone("#DAD8D0", 90, 0.3, 0.35), new ColorZone("#56565C", 200, 0.12, 0.6).Heights(-1000, 1) },
            formations =
            {
                L("lava_plain", 22, -12, 70), U("lava_plain", 3), L("rayed_crater", -43, -11, 16), U("simple_crater", 80),
                U("complex_crater", 6), U("peak_ring_basin", 1), U("concentric_crater", 3), U("crater_chain", 3),
                U("secondary_craters", 6), U("ghost_crater", 4), U("dark_halo_crater", 5), U("sinuous_rille", 4),
                U("wrinkle_ridge", 5), U("lava_tube_skylight", 3), U("lava_dome", 4), U("fractured_floor_crater", 2),
                U("massif", 2), U("boulder_field", 8), U("regolith_garden", 4), U("graben", 2), U("cave", 1),
                U("elongated_crater", 2),
            },
            scatter = { new ScatterRule(FeatureKind.Boulder, 10, 0.25, 1.3), new ScatterRule(FeatureKind.RockPile, 1.5, 0.8, 2) },
        };

        static PlanetProfile Mars() => new PlanetProfile
        {
            bodyId = "Mars",
            continentHeight = 22, continentWavelength = 450, hillHeight = 6, hillWavelength = 80,
            mountainHeight = 30, mountainWavelength = 220, mountainCoverage = 0.18, detailHeight = 0.45, detailWavelength = 8,
            lowColor = "#9A4A22", midColor = "#C1440E", highColor = "#D98A5A", cliffColor = "#6E2E14", rockColor = "#7A3A1E",
            midHeight = 4, highHeight = 26, poleColor = "#F4EFE8", poleLatitude = 0.94, strata = 2.2,
            zones = { new ColorZone("#4E3226", 300, 0.12, 0.65), new ColorZone("#E0A070", 140, 0.3, 0.4) },
            hasAtmosphere = true, skyColor = "#D9A066", hazeColor = "#C98F5E", atmosphereHeight = 120, visibility = 1000,
            skyOpacity = 0.6, sunDimming = 0.1,
            weather = { new WeatherSpec(WeatherKind.Dust, 3, 230, 22, "#C27A4A", "", 35) },
            formations =
            {
                L("chasma", -10, -62, 800, 96), L("crustal_swell", 16, -128, 240), L("shield_volcano", 18, -133, 150),
                U("polar_ice_cap", 1), U("shield_volcano", 2), U("rampart_crater", 8), U("pedestal_crater", 5),
                U("simple_crater", 30), U("complex_crater", 3), U("outflow_channel", 2), U("dry_riverbed", 4),
                U("chaos_terrain", 2), U("mesa", 5), U("butte", 5), U("yardang_field", 3), U("barchan_dunes", 3),
                U("sand_sea", 1), U("dust_devil_tracks", 4), U("gullies", 4), U("swiss_cheese_terrain", 2),
                U("polar_spiders", 3), U("fossae", 2), U("graben", 2), U("lava_tube_skylight", 3), U("through_cave", 1),
                U("boulder_field", 6), U("ventifacts", 5), U("rock_spires", 2), U("alluvial_fan", 2),
                U("patterned_ground", 2), U("ice_cliff", 2), U("natural_arch", 1), U("hoodoo_field", 1),
                U("lava_flow_field", 2), U("dust_storms", 1),
            },
            scatter = { new ScatterRule(FeatureKind.Boulder, 9, 0.3, 1.5), new ScatterRule(FeatureKind.Slab, 6, 0.4, 1.4) },
        };

        static PlanetProfile Phobos() => new PlanetProfile
        {
            bodyId = "Phobos",
            continentHeight = 4, continentWavelength = 50, hillHeight = 1.5, hillWavelength = 18,
            mountainHeight = 0, mountainCoverage = 0, detailHeight = 0.25, detailWavelength = 5,
            lowColor = "#5B5148", midColor = "#7A6E63", highColor = "#8F8377", cliffColor = "#4A423B", rockColor = "#6A6057",
            midHeight = 0, highHeight = 4,
            formations =
            {
                L("simple_crater", 0, 50, 14), U("grooves", 4), U("simple_crater", 14), U("boulder_field", 3),
                U("regolith_garden", 2), U("crater_chain", 1),
            },
            scatter = { new ScatterRule(FeatureKind.Boulder, 14, 0.2, 1.1) },
        };

        static PlanetProfile Deimos() => new PlanetProfile
        {
            bodyId = "Deimos",
            continentHeight = 2.5, continentWavelength = 40, hillHeight = 1, hillWavelength = 15,
            mountainHeight = 0, mountainCoverage = 0, detailHeight = 0.2, detailWavelength = 5,
            lowColor = "#746A5F", midColor = "#9A8F82", highColor = "#ADA295", cliffColor = "#5C544B", rockColor = "#857B70",
            midHeight = 0, highHeight = 3,
            formations =
            {
                L("simple_crater", -20, 60, 20), U("simple_crater", 5), U("ghost_crater", 3), U("regolith_garden", 3), U("boulder_field", 2), U("rubble_pile", 2),
            },
            scatter = { new ScatterRule(FeatureKind.Boulder, 8, 0.2, 0.9) },
        };

        static PlanetProfile Jupiter() => new PlanetProfile
        {
            bodyId = "Jupiter", surfaceRadius = 520,
            continentHeight = 20, continentWavelength = 500, hillHeight = 8, hillWavelength = 100,
            mountainHeight = 50, mountainWavelength = 260, mountainCoverage = 0.35, detailHeight = 0.5, detailWavelength = 9,
            lowColor = "#3A2A26", midColor = "#6B4636", highColor = "#A8683F", cliffColor = "#2A1E1C", rockColor = "#4A342C",
            midHeight = 6, highHeight = 40, strata = 3.5,
            zones = { new ColorZone("#8A2E1E", 240, 0.15, 0.6), new ColorZone("#D9B38C", 140, 0.3, 0.35).Heights(20, 1000) },
            hasAtmosphere = true, cloudDeck = true, skyColor = "#C98B4E", hazeColor = "#B8743C", cloudBase = 650,
            visibility = 800, skyOpacity = 1, sunDimming = 0.5, airDrag = 0.05,
            weather =
            {
                new WeatherSpec(WeatherKind.Thunder, 6, 220, 26, "#B8743C", "#FF5A3C", 90),
                new WeatherSpec(WeatherKind.Thunder, 1, 420, 40, "#A8402A", "#FF2E1E", 60) { permanent = true, latitude = -22 },
            },
            formations =
            {
                U("cloud_bands", 1), U("great_storm", 1), U("thunderstorms", 1), U("mountain_range", 3), U("isolated_peak", 5),
                U("rock_spires", 8), U("hoodoo_field", 4), U("mesa", 6), U("butte", 8), U("chasma", 1), U("slot_canyon", 3),
                U("natural_arch", 4), U("natural_bridge", 2), U("volcanic_plug", 5), U("lava_lake", 3),
                U("columnar_basalt", 5), U("tower_karst", 3), U("cavern", 2), U("through_cave", 2), U("balanced_rocks", 4),
                U("badlands", 3), U("rift_valley", 1), U("boulder_field", 6),
            },
            scatter = { new ScatterRule(FeatureKind.Boulder, 7, 0.4, 1.8), new ScatterRule(FeatureKind.Slab, 5, 0.5, 1.6) },
        };

        static PlanetProfile Io() => new PlanetProfile
        {
            bodyId = "Io",
            continentHeight = 6, continentWavelength = 200, hillHeight = 3, hillWavelength = 50,
            mountainHeight = 0, mountainCoverage = 0, detailHeight = 0.35, detailWavelength = 7,
            lowColor = "#C9A22E", midColor = "#E6D25A", highColor = "#F0E6A8", cliffColor = "#8A5A1E", rockColor = "#9A7A2A",
            midHeight = 0, highHeight = 6,
            zones =
            {
                new ColorZone("#D9702A", 160, 0.1, 0.7), new ColorZone("#F4F0DC", 110, 0.3, 0.6),
                new ColorZone("#3A2E22", 220, 0.35, 0.7),
            },
            formations =
            {
                L("lava_lake", 13, -50, 40), U("patera", 8), U("lava_lake", 3), U("volcanic_plume", 3),
                U("sulfur_vent_field", 6), U("lava_flow_field", 8), U("isolated_peak", 4), U("shield_field", 2),
                U("caldera", 2), U("fumarole_field", 4), U("horst", 1), U("talus_apron", 3), U("rubble_pile", 3),
            },
            scatter = { new ScatterRule(FeatureKind.Boulder, 6, 0.3, 1.2), new ScatterRule(FeatureKind.Slab, 4, 0.4, 1.2, "#3A2E22") },
        };

        static PlanetProfile Europa() => new PlanetProfile
        {
            bodyId = "Europa",
            continentHeight = 3, continentWavelength = 180, hillHeight = 2.5, hillWavelength = 45,
            mountainHeight = 0, mountainCoverage = 0, detailHeight = 0.3, detailWavelength = 6,
            lowColor = "#C2B8A6", midColor = "#DCD6C8", highColor = "#F0EEE8", cliffColor = "#9AB4C8", rockColor = "#D4DEE6",
            midHeight = 0, highHeight = 4,
            zones = { new ColorZone("#A77B55", 150, 0.15, 0.5), new ColorZone("#E8F0F6", 80, 0.2, 0.4) },
            formations =
            {
                U("linea", 14), U("chaos_terrain", 5), L("rayed_crater", -25, 90, 12), U("penitentes", 4),
                U("crevasse_field", 4), U("pressure_ridges", 3), U("ice_spires", 3), U("polygon_cracks", 4),
                U("ice_boulders", 4), U("ice_cave", 2), U("palimpsest", 1), U("simple_crater", 3),
            },
            scatter = { new ScatterRule(FeatureKind.IceShard, 3, 0.5, 2.2), new ScatterRule(FeatureKind.Boulder, 5, 0.3, 1.2) },
        };

        static PlanetProfile Ganymede() => new PlanetProfile
        {
            bodyId = "Ganymede",
            continentHeight = 8, continentWavelength = 260, hillHeight = 4, hillWavelength = 60,
            mountainHeight = 8, mountainWavelength = 120, mountainCoverage = 0.15, detailHeight = 0.35, detailWavelength = 7,
            lowColor = "#5E564E", midColor = "#8C8378", highColor = "#B4ADA2", cliffColor = "#8FA9BC", rockColor = "#9AA8B2",
            midHeight = 1, highHeight = 10,
            // Dark old ground with bright, bluish sheets of newer ice: an ice moon, not a second Mercury.
            zones = { new ColorZone("#3E3832", 280, 0.05, 0.6), new ColorZone("#C8D4DC", 150, 0.08, 0.75) },
            formations =
            {
                L("grooved_terrain", 12, 40, 150), U("grooved_terrain", 6), U("palimpsest", 3), U("rayed_crater", 3), U("simple_crater", 35),
                U("complex_crater", 3), U("dark_halo_crater", 3), U("fossae", 2), U("ice_boulders", 4), U("crater_chain", 2),
                U("pedestal_crater", 2), U("secondary_craters", 3),
            },
            scatter = { new ScatterRule(FeatureKind.Boulder, 6, 0.3, 1.3), new ScatterRule(FeatureKind.IceShard, 4, 0.4, 1.6, "#C8D4DC") },
        };

        static PlanetProfile Callisto() => new PlanetProfile
        {
            bodyId = "Callisto",
            continentHeight = 6, continentWavelength = 240, hillHeight = 4, hillWavelength = 55,
            mountainHeight = 0, mountainCoverage = 0, detailHeight = 0.35, detailWavelength = 7,
            lowColor = "#3E3832", midColor = "#5A5148", highColor = "#766C60", cliffColor = "#2E2A26", rockColor = "#4E463E",
            midHeight = 0, highHeight = 6,
            zones = { new ColorZone("#8A8072", 90, 0.35, 0.4) },
            formations =
            {
                L("valhalla_rings", 15, -55, 55), U("simple_crater", 90), U("complex_crater", 5), U("ice_knobs", 6),
                U("crater_chain", 3), U("palimpsest", 2), U("multi_ring_basin", 1), U("secondary_craters", 4),
                U("ice_boulders", 3),
            },
            scatter = { new ScatterRule(FeatureKind.Boulder, 8, 0.3, 1.3), new ScatterRule(FeatureKind.IceShard, 1.5, 0.6, 1.8) },
        };

        static PlanetProfile Saturn() => new PlanetProfile
        {
            bodyId = "Saturn", surfaceRadius = 430,
            continentHeight = 14, continentWavelength = 450, hillHeight = 6, hillWavelength = 90,
            mountainHeight = 25, mountainWavelength = 220, mountainCoverage = 0.2, detailHeight = 0.45, detailWavelength = 8,
            lowColor = "#A88A52", midColor = "#CDB277", highColor = "#EADFB8", cliffColor = "#7A6238", rockColor = "#9A8456",
            midHeight = 4, highHeight = 24, strata = 2.8,
            zones = { new ColorZone("#F2EBD2", 160, 0.2, 0.5), new ColorZone("#8A6E3E", 260, 0.2, 0.45) },
            hasAtmosphere = true, cloudDeck = true, skyColor = "#E3CF9A", hazeColor = "#D4BC80", cloudBase = 520,
            visibility = 900, skyOpacity = 1, sunDimming = 0.45, airDrag = 0.05,
            weather = { new WeatherSpec(WeatherKind.Thunder, 4, 240, 30, "#D4BC80", "#FFE9A0", 100) },
            formations =
            {
                U("cloud_bands", 1), U("polar_hexagon", 1), U("thunderstorms", 1), U("ice_spires", 8), U("yardang_field", 5),
                U("star_dunes", 3), U("sand_sea", 2), U("mesa", 4), U("inselberg", 4), U("massif", 2), U("fossae", 2),
                U("ice_cave", 2), U("barchan_dunes", 3), U("ventifacts", 5), U("horst", 2), U("linear_dunes", 2),
            },
            scatter = { new ScatterRule(FeatureKind.Slab, 6, 0.4, 1.5), new ScatterRule(FeatureKind.IceShard, 2, 0.6, 2.4, "#F4F0E0") },
        };

        static PlanetProfile Enceladus() => new PlanetProfile
        {
            bodyId = "Enceladus",
            continentHeight = 3, continentWavelength = 80, hillHeight = 1.6, hillWavelength = 25,
            mountainHeight = 0, mountainCoverage = 0, detailHeight = 0.25, detailWavelength = 5,
            lowColor = "#DCE6EC", midColor = "#F2F5F7", highColor = "#FFFFFF", cliffColor = "#A8C4D6", rockColor = "#E2ECF2",
            midHeight = 0, highHeight = 3,
            formations =
            {
                L("tiger_stripes", -86, 0, 85, 0), U("geyser_field", 2), U("simple_crater", 10), U("pressure_ridges", 2),
                U("crevasse_field", 2), U("ice_boulders", 4), U("polygon_cracks", 2), U("ice_cave", 1),
            },
            scatter = { new ScatterRule(FeatureKind.Boulder, 8, 0.2, 1), new ScatterRule(FeatureKind.IceShard, 2, 0.4, 1.5) },
        };

        static PlanetProfile Titan() => new PlanetProfile
        {
            bodyId = "Titan",
            continentHeight = 9, continentWavelength = 260, continentBias = 1, hillHeight = 4, hillWavelength = 60,
            mountainHeight = 12, mountainWavelength = 130, mountainCoverage = 0.15, detailHeight = 0.35, detailWavelength = 7,
            hasSea = true, seaLevel = -3, seaLiquid = Liquid.Methane, seaColor = "#3A2A14", seaDeepColor = "#1A1208",
            underwaterVisibility = 40,
            lowColor = "#7A5A2A", midColor = "#A8742A", highColor = "#D9A441", cliffColor = "#5A3E1C", rockColor = "#C8B48A",
            shoreColor = "#5E4622", midHeight = 0, highHeight = 10,
            zones = { new ColorZone("#3A2E22", 220, 0.05, 0.6).Latitudes(0, 0.35), new ColorZone("#E0C68A", 120, 0.3, 0.4) },
            hasAtmosphere = true, skyColor = "#D98A2B", hazeColor = "#C97A22", atmosphereHeight = 220, visibility = 320,
            skyOpacity = 1, sunDimming = 0.55, airDrag = 0.1,
            weather = { new WeatherSpec(WeatherKind.Methane, 3, 170, 6, "#D9A85A", "", 70) },
            formations =
            {
                U("methane_sea", 1), U("methane_lake", 8), U("linear_dunes", 5), U("river_valley", 3), U("cryovolcano", 2),
                U("mountain_range", 1), U("simple_crater", 3), U("dry_riverbed", 3), U("delta", 1), U("ice_boulders", 5),
                U("island", 2), U("lagoon", 1), U("cave", 1), U("methane_rain", 1),
            },
            scatter = { new ScatterRule(FeatureKind.Boulder, 9, 0.2, 0.9) { minHeight = -4 } },
        };

        static PlanetProfile Uranus() => new PlanetProfile
        {
            bodyId = "Uranus", surfaceRadius = 320,
            continentHeight = 12, continentWavelength = 350, hillHeight = 5, hillWavelength = 70,
            mountainHeight = 20, mountainWavelength = 160, mountainCoverage = 0.2, detailHeight = 0.4, detailWavelength = 7,
            lowColor = "#6FA8B4", midColor = "#A6D6DA", highColor = "#E4F6F6", cliffColor = "#4E8A9A", rockColor = "#BFE4E6",
            midHeight = 3, highHeight = 18,
            zones = { new ColorZone("#F4FBFB", 130, 0.2, 0.5), new ColorZone("#3E7A8C", 240, 0.25, 0.4) },
            hasAtmosphere = true, cloudDeck = true, skyColor = "#9EDFDC", hazeColor = "#86CFCF", cloudBase = 280,
            visibility = 650, skyOpacity = 1, sunDimming = 0.5, airDrag = 0.05,
            weather = { new WeatherSpec(WeatherKind.Diamond, 4, 180, 16, "#DFFBF8", "#7CF5E0", 90) },
            formations =
            {
                U("cloud_bands", 1), U("diamond_rain", 1), U("diamond_field", 8), U("crystal_grotto", 2), U("penitentes", 4),
                U("crevasse_field", 3), U("glacier", 3), U("ice_cliff", 2), U("chaos_terrain", 2), U("mountain_range", 1),
                U("ice_spires", 5), U("patterned_ground", 3), U("nitrogen_ice_plain", 1), U("cave", 1),
            },
            scatter = { new ScatterRule(FeatureKind.IceShard, 4, 0.5, 2.2), new ScatterRule(FeatureKind.Crystal, 1.2, 0.4, 1.2, "#BFF4F0") },
        };

        static PlanetProfile Titania() => new PlanetProfile
        {
            bodyId = "Titania",
            continentHeight = 5, continentWavelength = 120, hillHeight = 2.5, hillWavelength = 35,
            mountainHeight = 0, mountainCoverage = 0, detailHeight = 0.3, detailWavelength = 6,
            lowColor = "#6E7880", midColor = "#98A4AC", highColor = "#BCC6CC", cliffColor = "#525C64", rockColor = "#828C94",
            midHeight = 0, highHeight = 5,
            formations =
            {
                L("chasma", -15, 20, 300, 60), U("fault_scarp", 4), U("simple_crater", 30), U("complex_crater", 2),
                U("graben", 2), U("ice_boulders", 2),
            },
            scatter = { new ScatterRule(FeatureKind.Boulder, 8, 0.2, 1.1) },
        };

        static PlanetProfile Neptune() => new PlanetProfile
        {
            bodyId = "Neptune", surfaceRadius = 420,
            continentHeight = 30, continentWavelength = 450, continentBias = -30, hillHeight = 7, hillWavelength = 90,
            mountainHeight = 40, mountainWavelength = 240, mountainCoverage = 0.25, detailHeight = 0.45, detailWavelength = 8,
            hasSea = true, seaLevel = 0, seaColor = "#1E4FB8", seaDeepColor = "#061A4A", underwaterVisibility = 130,
            lowColor = "#3A5A6A", midColor = "#5E7A70", highColor = "#8A9488", cliffColor = "#2E4450", rockColor = "#4E6470",
            shoreColor = "#B8C4B0", midHeight = 6, highHeight = 22,
            zones = { new ColorZone("#2A3A5A", 200, 0.1, 0.5).Heights(-1000, -8), new ColorZone("#7AA890", 120, 0.2, 0.5).Heights(1, 1000) },
            hasAtmosphere = true, cloudDeck = true, skyColor = "#3F66D9", hazeColor = "#3458C4", cloudBase = 230,
            visibility = 750, skyOpacity = 1, sunDimming = 0.5, airDrag = 0.05,
            weather =
            {
                new WeatherSpec(WeatherKind.Rain, 5, 200, 26, "#9AB4E6", "#5AA0FF", 70),
                new WeatherSpec(WeatherKind.Thunder, 1, 320, 36, "#1E2E6A", "#8A6AFF", 60) { permanent = true, latitude = -20 },
            },
            formations =
            {
                U("ocean", 1), U("great_storm", 1), U("thunderstorms", 1), U("island", 9), U("archipelago", 6), U("atoll", 4),
                U("seamount", 6), U("guyot", 3), U("ocean_trench", 2), U("submarine_canyon", 3), U("hydrothermal_vents", 6),
                U("abyssal_hills", 5), U("icebergs", 8), U("sea_stacks", 4), U("lagoon", 1), U("cave", 1),
            },
            scatter = { new ScatterRule(FeatureKind.Boulder, 7, 0.3, 1.6) },
        };

        static PlanetProfile Triton() => new PlanetProfile
        {
            bodyId = "Triton",
            continentHeight = 4, continentWavelength = 180, hillHeight = 2.5, hillWavelength = 45,
            mountainHeight = 0, mountainCoverage = 0, detailHeight = 0.3, detailWavelength = 6,
            lowColor = "#9E8C86", midColor = "#C9B8B0", highColor = "#E6DAD4", cliffColor = "#7A6A66", rockColor = "#B0A09A",
            midHeight = 0, highHeight = 4, poleColor = "#F2D6D0", poleLatitude = 0.55,
            zones = { new ColorZone("#8FA6A8", 160, 0.15, 0.4) },
            formations =
            {
                U("cantaloupe_terrain", 4), U("nitrogen_geyser", 5), U("nitrogen_ice_plain", 2), U("polar_ice_cap", 1),
                U("cryovolcano", 2), U("wrinkle_ridge", 3), U("simple_crater", 4), U("pressure_ridges", 2), U("ice_spires", 2),
            },
            scatter = { new ScatterRule(FeatureKind.Boulder, 6, 0.2, 1), new ScatterRule(FeatureKind.IceShard, 1.5, 0.4, 1.6) },
        };
    }
}
