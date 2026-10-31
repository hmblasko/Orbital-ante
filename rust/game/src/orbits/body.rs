//! A celestial body: painted terrain surface, scattered rocks and point gravity.
//!
//! The surface comes from `procgen::terrain` (hills, ridges and craters) and
//! is drawn with the TF2-style painted shader. Phase 5 replaces it with
//! streamed terrain and the full feature sets; this is slice scope.

use godot::classes::mesh::{ArrayType, PrimitiveType};
use godot::classes::{
    ArrayMesh, CollisionShape3D, ConcavePolygonShape3D, ConvexPolygonShape3D, CylinderShape3D,
    FastNoiseLite, IStaticBody3D, MeshInstance3D, NoiseTexture2D, OmniLight3D, Shader,
    ShaderMaterial, SphereShape3D, StaticBody3D, Texture2D,
};
use godot::prelude::*;
use serde::Deserialize;

use super::gravity::{self, GravitySettings};
use crate::procgen::hoodoo::hoodoo_mesh;
use crate::procgen::landforms::LandformKind;
use crate::procgen::rocks::rock_mesh;
use crate::procgen::terrain::{MeshData, TerrainData, TerrainShape, sphere_mesh};
use crate::seed::SeededRng;

/// Scene-tree group every gravity source joins, so other nodes can find them.
pub const GRAVITY_GROUP: &str = "gravity_wells";

const TERRAIN_SHADER: &str = "res://shaders/painted_terrain.gdshader";
const PROP_SHADER: &str = "res://shaders/painted_prop.gdshader";
const WATER_SHADER: &str = "res://shaders/painted_water.gdshader";
const ICE_SHADER: &str = "res://shaders/painted_ice.gdshader";

#[derive(Debug, Clone, Deserialize)]
pub struct BodyData {
    pub name: String,
    /// Nominal surface radius in metres (hills and craters vary around it).
    pub radius: f32,
    pub gravity: GravitySettings,
    /// Breathable air near the surface. Missing means vacuum (the suit uses oxygen).
    #[serde(default)]
    pub atmosphere: Option<AtmosphereData>,
    pub terrain: TerrainData,
    pub look: SurfaceLook,
    pub rocks: RockData,
    /// Colours for landform painting and water (defaults suit a dry grey moon).
    #[serde(default)]
    pub land_look: LandLook,
    /// Hoodoo spires clustered around mesas and canyon rims.
    #[serde(default)]
    pub hoodoos: Option<HoodooData>,
    /// The surface is drawn in this many tiles per cube-face edge, so pieces
    /// out of view are skipped (6 x tiles x tiles meshes in total).
    #[serde(default = "default_render_tiles")]
    pub render_tiles: u32,
    /// Optional detail levels for the drawn surface (collision is unaffected).
    #[serde(default)]
    pub lod: Option<SurfaceLod>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct SurfaceLod {
    /// Cells per cube-face edge for tiles near the camera.
    pub near_subdivisions: u32,
    /// Cells per cube-face edge for distant tiles.
    pub far_subdivisions: u32,
    /// Tiles per cube-face edge (both levels; must divide both counts).
    pub tiles: u32,
    /// Distance from the camera to a tile's centre where it switches level.
    pub near_range_m: f32,
}

fn default_render_tiles() -> u32 {
    2
}

#[derive(Debug, Clone, Deserialize)]
#[serde(default)]
pub struct LandLook {
    pub strata_a: (f32, f32, f32),
    pub strata_b: (f32, f32, f32),
    pub basalt: (f32, f32, f32),
    pub lava: (f32, f32, f32),
    pub lava_energy: f32,
    /// Range of the glow light over each volcano's lava lake, in metres (0 = none).
    pub lava_light_range_m: f32,
    pub snow: (f32, f32, f32),
    pub wet: (f32, f32, f32),
    pub mineral: (f32, f32, f32),
    pub water_shallow: (f32, f32, f32),
    pub water_deep: (f32, f32, f32),
    pub water_foam: (f32, f32, f32),
    /// Water surface grid cells per cube-face edge.
    pub water_subdivisions: u32,
    /// Lakes and pools are frozen solid: painted ice you can walk on.
    #[serde(default)]
    pub frozen: bool,
}

impl Default for LandLook {
    fn default() -> Self {
        Self {
            strata_a: (0.62, 0.55, 0.50),
            strata_b: (0.48, 0.43, 0.40),
            basalt: (0.18, 0.17, 0.19),
            lava: (1.0, 0.42, 0.08),
            lava_energy: 3.0,
            lava_light_range_m: 0.0,
            snow: (0.92, 0.94, 0.98),
            wet: (0.30, 0.28, 0.27),
            mineral: (0.93, 0.92, 0.86),
            water_shallow: (0.25, 0.75, 0.78),
            water_deep: (0.05, 0.22, 0.38),
            water_foam: (0.92, 0.97, 1.0),
            water_subdivisions: 48,
            frozen: false,
        }
    }
}

#[derive(Debug, Clone, Deserialize)]
pub struct HoodooData {
    /// Clusters around each mesa, and along each canyon.
    pub clusters_per_mesa: u32,
    pub clusters_per_canyon: u32,
    pub per_cluster: (u32, u32),
    pub cluster_spread_m: f32,
    pub height_m: (f32, f32),
    /// Distinct spire shapes built and reused.
    pub variants: u32,
    pub band_a: (f32, f32, f32),
    pub band_b: (f32, f32, f32),
    pub cap: (f32, f32, f32),
    /// Hidden beyond this many metres from the camera (0 = always drawn).
    #[serde(default)]
    pub visible_m: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct AtmosphereData {
    pub breathable: bool,
    /// Air reaches this far above the surface, in metres.
    pub height_m: f32,
}

/// Painted palette for the terrain shader (section 11: warm/cool
/// separation, moderate saturation, soft gradients).
#[derive(Debug, Clone, Deserialize)]
pub struct SurfaceLook {
    pub color_low: (f32, f32, f32),
    pub color_high: (f32, f32, f32),
    pub color_crater: (f32, f32, f32),
    pub color_rim: (f32, f32, f32),
    pub color_slope: (f32, f32, f32),
    pub shadow_tint: (f32, f32, f32),
    pub rim_color: (f32, f32, f32),
    pub rim_strength: f32,
    /// Broad paint variation repeats every 1/scale metres.
    pub detail_scale: f32,
    pub grain_scale: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct RockData {
    pub count: u32,
    /// Boulder size range in metres; most are small.
    pub size_m: (f32, f32),
    /// Rocks per cluster; clusters read more natural than an even scatter.
    pub cluster_size: (u32, u32),
    pub cluster_spread_m: f32,
    pub roughness: f32,
    pub color: (f32, f32, f32),
    pub color_dark: (f32, f32, f32),
    /// The smallest rocks hide beyond this many metres from the camera; the
    /// largest stay drawn up to twice as far (0 = always drawn).
    #[serde(default)]
    pub visible_m: f32,
}

/// Cells along one edge of a terrain collision tile (64 x 64 cells is
/// about 8k triangles per shape).
const COLLISION_TILE_CELLS: u32 = 64;

/// `Gd<T>` (used below) is a smart pointer to an object that lives inside
/// Godot; you call methods through it, and Godot owns the memory.
#[derive(GodotClass)]
#[class(base = StaticBody3D)]
pub struct CelestialBody {
    /// Which RON file describes this body.
    #[export(file = "*.ron")]
    data_path: GString,
    data: Option<BodyData>,
    shape: Option<TerrainShape>,
    base: Base<StaticBody3D>,
}

#[godot_api]
impl IStaticBody3D for CelestialBody {
    fn init(base: Base<StaticBody3D>) -> Self {
        Self {
            data_path: GString::new(),
            data: None,
            shape: None,
            base,
        }
    }

    fn ready(&mut self) {
        let path = self.data_path.to_string();
        let Some(data) = crate::data::try_load::<BodyData>(&path) else {
            godot_error!(
                "CelestialBody {}: no data, body disabled",
                self.base().get_name()
            );
            return;
        };
        let shape = TerrainShape::new(&data.terrain, data.radius);
        self.build_surface(&data, &shape);
        self.scatter_rocks(&data, &shape);
        self.build_water(&data, &shape);
        self.add_lava_lights(&data, &shape);
        self.scatter_hoodoos(&data, &shape);
        self.base_mut().add_to_group(GRAVITY_GROUP);
        godot_print!(
            "CelestialBody ready: {} (radius {} m)",
            data.name,
            data.radius
        );
        self.data = Some(data);
        self.shape = Some(shape);
    }
}

#[godot_api]
impl CelestialBody {
    /// Gravity acceleration this body applies at a world position.
    pub fn gravity_at(&self, point: Vector3) -> Vector3 {
        match &self.data {
            Some(d) => gravity::point_gravity(
                self.base().get_global_position(),
                d.radius,
                &d.gravity,
                point,
            ),
            None => Vector3::ZERO,
        }
    }

    /// True if `point` is inside this body's breathable air.
    pub fn breathable_at(&self, point: Vector3) -> bool {
        let Some(d) = &self.data else { return false };
        match &d.atmosphere {
            Some(a) if a.breathable => {
                self.base().get_global_position().distance_to(point)
                    <= self.surface_radius_toward(point) + a.height_m
            }
            _ => false,
        }
    }

    /// Nominal radius (hills and craters vary around it).
    #[func]
    pub fn radius(&self) -> f32 {
        self.data.as_ref().map_or(0.0, |d| d.radius)
    }

    /// The low-ground sand colour, for dust on props and wrecks.
    pub fn dust_color(&self) -> Color {
        self.data.as_ref().map_or(Color::from_rgb(0.6, 0.6, 0.6), |d| rgb(d.look.color_low))
    }

    /// Lowest the ground can be anywhere (for out-of-world checks).
    #[func]
    pub fn min_surface_radius(&self) -> f32 {
        self.shape.as_ref().map_or(0.0, |s| s.min_radius())
    }

    /// Distance from the centre to the ground in the direction of a world point.
    #[func]
    pub fn surface_radius_toward(&self, world_point: Vector3) -> f32 {
        let dir = world_point - self.base().get_global_position();
        match &self.shape {
            Some(s) if dir.length_squared() > 1e-8 => s.surface_radius(dir),
            _ => self.radius(),
        }
    }

    fn build_surface(&mut self, data: &BodyData, shape: &TerrainShape) {
        let mesh_data = shape.build_mesh();
        let height_range =
            (data.terrain.hill_height_m + data.terrain.ridge_height_m).max(0.5) * 2.0;
        let look = &data.look;
        let mut material = ShaderMaterial::new_gd();
        if let Ok(shader) = try_load::<Shader>(TERRAIN_SHADER) {
            material.set_shader(&shader);
        }
        for (name, c) in [
            ("color_low", look.color_low),
            ("color_high", look.color_high),
            ("color_crater", look.color_crater),
            ("color_rim", look.color_rim),
            ("color_slope", look.color_slope),
            ("shadow_tint", look.shadow_tint),
            ("rim_color", look.rim_color),
        ] {
            material.set_shader_parameter(name, &rgb(c).to_variant());
        }
        material.set_shader_parameter("rim_strength", &look.rim_strength.to_variant());
        material.set_shader_parameter("detail_scale", &look.detail_scale.to_variant());
        material.set_shader_parameter("grain_scale", &look.grain_scale.to_variant());
        material.set_shader_parameter("body_radius", &data.radius.to_variant());
        material.set_shader_parameter(
            "detail_noise",
            &noise_texture(0.012, 4, data.terrain.seed as i32).to_variant(),
        );
        material.set_shader_parameter(
            "grain_noise",
            &noise_texture(0.09, 2, data.terrain.seed as i32 + 1).to_variant(),
        );
        let land = &data.land_look;
        for (name, c) in [
            ("strata_a", land.strata_a),
            ("strata_b", land.strata_b),
            ("basalt_color", land.basalt),
            ("lava_color", land.lava),
            ("snow_color", land.snow),
            ("wet_color", land.wet),
            ("mineral_color", land.mineral),
        ] {
            material.set_shader_parameter(name, &rgb(c).to_variant());
        }
        material.set_shader_parameter("lava_energy", &land.lava_energy.to_variant());

        match &data.lod {
            // Fine tiles close to the camera, coarse ones beyond; collision
            // stays on the middle grid (`terrain.subdivisions`).
            Some(lod) => {
                let near = shape.build_mesh_with(lod.near_subdivisions);
                self.add_surface_tiles(&near, lod.near_subdivisions, lod.tiles, &material, height_range, "Near", (0.0, lod.near_range_m));
                let far = shape.build_mesh_with(lod.far_subdivisions);
                self.add_surface_tiles(&far, lod.far_subdivisions, lod.tiles, &material, height_range, "Far", (lod.near_range_m, 0.0));
            }
            None => self.add_surface_tiles(&mesh_data, data.terrain.subdivisions, data.render_tiles, &material, height_range, "", (0.0, 0.0)),
        }

        // Collision in tiles: Jolt numbers every triangle of a shape inside
        // one 32-bit id shared with the body's other shapes (rocks, hoodoos),
        // so one huge mesh on a large planet runs out of id bits.
        let collision_tiles = (data.terrain.subdivisions / COLLISION_TILE_CELLS).max(1);
        for (i, tile) in mesh_data
            .split_tiles(data.terrain.subdivisions, collision_tiles)
            .iter()
            .enumerate()
        {
            if tile.triangle_count() == 0 {
                continue;
            }
            let mut collision_shape = ConcavePolygonShape3D::new_gd();
            collision_shape.set_faces(&triangle_soup(tile));
            let mut collision = CollisionShape3D::new_alloc();
            collision.set_name(&format!("SurfaceCollision{i}"));
            collision.set_shape(&collision_shape);
            self.base_mut().add_child(&collision);
        }
    }

    /// Add one surface mesh as tiles. COLOR = (crater floor, crater rim,
    /// height, mineral), UV = (strata, lava), UV2 = (snow, wet).
    #[allow(clippy::too_many_arguments)]
    fn add_surface_tiles(&mut self, mesh: &MeshData, subdivisions: u32, tiles: u32, material: &Gd<ShaderMaterial>, height_range: f32, label: &str, range: (f32, f32)) {
        // Painting hints: COLOR = (crater floor, crater rim, height, mineral),
        // UV = (strata, lava), UV2 = (snow, wet).
        for (i, tile) in mesh.split_tiles(subdivisions, tiles).iter().enumerate() {
            if tile.triangle_count() == 0 {
                continue;
            }
            let hints = SurfaceHints {
                colors: tile
                    .samples
                    .iter()
                    .map(|s| {
                        Color::from_rgba(
                            s.crater_floor,
                            s.crater_rim,
                            (s.height / height_range + 0.5).clamp(0.0, 1.0),
                            s.land.mineral,
                        )
                    })
                    .collect(),
                uv: tile
                    .samples
                    .iter()
                    .map(|s| Vector2::new(s.land.strata, s.land.lava))
                    .collect(),
                uv2: tile
                    .samples
                    .iter()
                    .map(|s| Vector2::new(s.land.snow, s.land.wet))
                    .collect(),
            };
            let mut instance = MeshInstance3D::new_alloc();
            instance.set_name(&format!("SurfaceTile{label}{i}"));
            // Visibility ranges (metres from the camera to the tile's centre);
            // 0 means unlimited. No margins: overlapping levels would z-fight.
            if range.0 > 0.0 {
                instance.set_visibility_range_begin(range.0);
            }
            if range.1 > 0.0 {
                instance.set_visibility_range_end(range.1);
            }
            // Each tile sits at its own centre so visibility ranges measure
            // from the camera to the tile, not to the body's centre. The
            // shader adds `tile_offset` back to get body-space positions.
            let centre = tile.positions.iter().fold(Vector3::ZERO, |a, p| a + *p) / tile.positions.len() as f32;
            let mut local = tile.clone();
            for p in &mut local.positions {
                *p -= centre;
            }
            instance.set_mesh(&array_mesh_with_hints(&local, &hints));
            instance.set_position(centre);
            instance.set_instance_shader_parameter("tile_offset", &centre.to_variant());
            instance.set_material_override(material);
            self.base_mut().add_child(&instance);
        }
    }

    /// Boulder clusters sitting on the terrain, with simple convex colliders.
    fn scatter_rocks(&mut self, data: &BodyData, shape: &TerrainShape) {
        let r = &data.rocks;
        if r.count == 0 {
            return;
        }
        let mut rng = SeededRng::derive(data.terrain.seed, "rocks");
        let mut material = ShaderMaterial::new_gd();
        if let Ok(shader) = try_load::<Shader>(PROP_SHADER) {
            material.set_shader(&shader);
        }
        material.set_shader_parameter("albedo", &rgb(r.color).to_variant());
        material.set_shader_parameter("albedo_dark", &rgb(r.color_dark).to_variant());
        material.set_shader_parameter("shadow_tint", &rgb(data.look.shadow_tint).to_variant());
        material.set_shader_parameter(
            "grain_noise",
            &noise_texture(0.08, 3, data.terrain.seed as i32 + 2).to_variant(),
        );
        material.set_shader_parameter("grain_scale", &0.6f32.to_variant());
        material.set_shader_parameter("grain_strength", &0.45f32.to_variant());

        // A few distinct rock shapes, reused (cheap to draw, still varied by scale and turn).
        let variants: Vec<(Gd<ArrayMesh>, PackedVector3Array)> = (0..6)
            .map(|i| {
                let m = rock_mesh(
                    data.terrain.seed as u32 ^ (i * 7919),
                    2,
                    r.roughness,
                    rng.range_f32(0.5, 0.85),
                );
                (array_mesh(&m, None), m.positions.iter().copied().collect())
            })
            .collect();

        let water = shape.water_level().unwrap_or(f32::MIN);
        let mut placed = 0;
        let mut attempts = 0;
        while placed < r.count && attempts < r.count * 20 {
            attempts += 1;
            let center = crate::procgen::scatter_on_sphere(&mut rng, 1)[0];
            let in_cluster =
                rng.range_i64(r.cluster_size.0 as i64, r.cluster_size.1 as i64 + 1) as u32;
            for _ in 0..in_cluster.min(r.count - placed) {
                let offset = Vector3::new(
                    rng.range_f32(-1.0, 1.0),
                    rng.range_f32(-1.0, 1.0),
                    rng.range_f32(-1.0, 1.0),
                ) * (r.cluster_spread_m / data.radius);
                let dir = (center + offset).normalized();
                // Keep boulders out of water and off cliff faces.
                let ground = shape.surface_radius(dir);
                let probe = (dir + basis_with_up(dir).col_a() * (1.0 / data.radius)).normalized();
                if ground - data.radius < water + 0.3 || (shape.surface_radius(probe) - ground).abs() > 0.7 {
                    continue;
                }
                let t = rng.next_f32().powi(3);
                let size = r.size_m.0 + (r.size_m.1 - r.size_m.0) * t;
                let (mesh, points) = &variants[rng.below(variants.len() as u64) as usize];
                let up_basis = super::body::basis_with_up(dir);
                let turn =
                    Basis::from_axis_angle(Vector3::UP, rng.range_f32(0.0, std::f32::consts::TAU));
                let basis = (up_basis * turn).scaled(Vector3::new(size, size, size));
                // Sink rocks a little so they sit in the ground, not on it.
                let origin = dir * (shape.surface_radius(dir) + size * 0.15);
                let transform = Transform3D::new(basis, origin);

                let mut instance = MeshInstance3D::new_alloc();
                instance.set_name(&format!("Rock{placed}"));
                if r.visible_m > 0.0 {
                    instance.set_visibility_range_end(r.visible_m * (1.0 + t));
                }
                instance.set_mesh(mesh);
                instance.set_material_override(&material);
                instance.set_transform(transform);
                self.base_mut().add_child(&instance);

                let mut convex = ConvexPolygonShape3D::new_gd();
                convex.set_points(points);
                let mut collision = CollisionShape3D::new_alloc();
                collision.set_shape(&convex);
                collision.set_transform(transform);
                self.base_mut().add_child(&collision);
                placed += 1;
            }
        }
    }

    /// A water surface at the water table: it only shows where landforms
    /// carve below it (lakes, rivers, canyon streams, pools).
    fn build_water(&mut self, data: &BodyData, shape: &TerrainShape) {
        let Some(level) = shape.water_level() else {
            return;
        };
        let land = &data.land_look;
        let mesh = sphere_mesh(data.radius + level, land.water_subdivisions);
        let mut material = ShaderMaterial::new_gd();
        let shader_path = if land.frozen { ICE_SHADER } else { WATER_SHADER };
        if let Ok(shader) = try_load::<Shader>(shader_path) {
            material.set_shader(&shader);
        }
        material.set_shader_parameter("shallow_color", &rgb(land.water_shallow).to_variant());
        material.set_shader_parameter("deep_color", &rgb(land.water_deep).to_variant());
        let edge = if land.frozen { "frost_color" } else { "foam_color" };
        material.set_shader_parameter(edge, &rgb(land.water_foam).to_variant());
        material.set_shader_parameter(
            "wave_noise",
            &noise_texture(0.03, 3, data.terrain.seed as i32 + 5).to_variant(),
        );
        let mut instance = MeshInstance3D::new_alloc();
        instance.set_name(if land.frozen { "Ice" } else { "Water" });
        instance.set_mesh(&array_mesh(&mesh, None));
        instance.set_material_override(&material);
        instance.set_cast_shadows_setting(
            godot::classes::geometry_instance_3d::ShadowCastingSetting::OFF,
        );
        self.base_mut().add_child(&instance);
        if land.frozen {
            // One solid sphere at the ice level: wherever the ground is lower
            // you stand on the ice, and elsewhere the terrain is on top anyway.
            let mut sphere = SphereShape3D::new_gd();
            sphere.set_radius(data.radius + level);
            let mut collision = CollisionShape3D::new_alloc();
            collision.set_name("IceCollision");
            collision.set_shape(&sphere);
            self.base_mut().add_child(&collision);
        }
    }

    /// A warm glow over each volcano's lava lake.
    fn add_lava_lights(&mut self, data: &BodyData, shape: &TerrainShape) {
        let land = &data.land_look;
        if land.lava_light_range_m <= 0.0 {
            return;
        }
        let volcanoes: Vec<Vector3> = shape
            .landforms()
            .list
            .iter()
            .filter(|l| l.kind == LandformKind::Volcano)
            .map(|l| l.center)
            .collect();
        for (i, dir) in volcanoes.into_iter().enumerate() {
            let mut light = OmniLight3D::new_alloc();
            light.set_name(&format!("LavaGlow{i}"));
            light.set_color(rgb(land.lava));
            light.set_param(godot::classes::light_3d::Param::ENERGY, land.lava_energy);
            light.set_param(
                godot::classes::light_3d::Param::RANGE,
                land.lava_light_range_m,
            );
            light.set_position(dir * (shape.surface_radius(dir) + land.lava_light_range_m * 0.25));
            self.base_mut().add_child(&light);
        }
    }

    /// Hoodoo clusters on the badland skirts of mesas and along canyon rims.
    fn scatter_hoodoos(&mut self, data: &BodyData, shape: &TerrainShape) {
        let Some(h) = &data.hoodoos else { return };
        let mut rng = SeededRng::derive(data.terrain.seed, "hoodoos");
        let variants: Vec<Gd<ArrayMesh>> = (0..h.variants.max(1))
            .map(|i| {
                let (m, colors) = hoodoo_mesh(
                    data.terrain.seed ^ (i as u64 * 104729),
                    1.0,
                    rgb(h.band_a),
                    rgb(h.band_b),
                    rgb(h.cap),
                );
                array_mesh(&m, Some(&colors))
            })
            .collect();
        let mut material = ShaderMaterial::new_gd();
        if let Ok(shader) = try_load::<Shader>(PROP_SHADER) {
            material.set_shader(&shader);
        }
        material.set_shader_parameter("use_vertex_color", &true.to_variant());
        material.set_shader_parameter("shadow_tint", &rgb(data.look.shadow_tint).to_variant());
        material.set_shader_parameter("rim_color", &rgb(data.look.rim_color).to_variant());
        material.set_shader_parameter(
            "grain_noise",
            &noise_texture(0.08, 3, data.terrain.seed as i32 + 3).to_variant(),
        );
        material.set_shader_parameter("grain_scale", &0.5f32.to_variant());
        material.set_shader_parameter("grain_strength", &0.3f32.to_variant());

        // Cluster centres: just outside mesa cliffs, and beside canyon walls.
        let mut centers = Vec::new();
        for l in &shape.landforms().list {
            match l.kind {
                LandformKind::Mesa | LandformKind::Butte => {
                    for _ in 0..h.clusters_per_mesa {
                        let around = random_tangent(l.center, &mut rng);
                        let dist = l.angle * rng.range_f32(1.15, 1.6);
                        centers.push((l.center * dist.cos() + around * dist.sin()).normalized());
                    }
                }
                LandformKind::Canyon => {
                    if let Some((a, b)) = l.path_ends() {
                        for _ in 0..h.clusters_per_canyon {
                            let t = rng.range_f32(0.2, 0.8);
                            let on = (a * (1.0 - t) + b * t).normalized();
                            let side = if rng.next_f32() < 0.5 { -1.0 } else { 1.0 };
                            let normal = a.cross(b).normalized();
                            centers.push(
                                (on + normal * side * l.angle * rng.range_f32(2.2, 3.4))
                                    .normalized(),
                            );
                        }
                    }
                }
                _ => {}
            }
        }
        let water = shape.water_level().unwrap_or(f32::MIN);
        let mut placed = 0;
        for center in centers {
            let count = rng.range_i64(h.per_cluster.0 as i64, h.per_cluster.1 as i64 + 1);
            for _ in 0..count {
                let offset = random_tangent(center, &mut rng)
                    * (rng.next_f32().sqrt() * h.cluster_spread_m / data.radius);
                let dir = (center + offset).normalized();
                let ground = shape.surface_radius(dir);
                // Never in water, and never on steep ground (cliffs).
                let probe =
                    (dir + random_tangent(dir, &mut rng) * (1.0 / data.radius)).normalized();
                if ground - data.radius < water + 0.4
                    || (shape.surface_radius(probe) - ground).abs() > 0.6
                {
                    continue;
                }
                let height = rng.range_f32(h.height_m.0, h.height_m.1);
                let mesh = &variants[rng.below(variants.len() as u64) as usize];
                let turn =
                    Basis::from_axis_angle(Vector3::UP, rng.range_f32(0.0, std::f32::consts::TAU));
                let basis =
                    (basis_with_up(dir) * turn).scaled(Vector3::new(height, height, height));
                let transform = Transform3D::new(basis, dir * (ground - height * 0.04));
                let mut instance = MeshInstance3D::new_alloc();
                instance.set_name(&format!("Hoodoo{placed}"));
                if h.visible_m > 0.0 {
                    instance.set_visibility_range_end(h.visible_m);
                }
                instance.set_mesh(mesh);
                instance.set_material_override(&material);
                instance.set_transform(transform);
                self.base_mut().add_child(&instance);

                let mut cylinder = CylinderShape3D::new_gd();
                cylinder.set_radius(height * 0.13);
                cylinder.set_height(height * 0.95);
                let mut collision = CollisionShape3D::new_alloc();
                collision.set_shape(&cylinder);
                collision.set_transform(Transform3D::new(
                    basis_with_up(dir),
                    dir * (ground + height * 0.45),
                ));
                self.base_mut().add_child(&collision);
                placed += 1;
            }
        }
    }
}

/// A random unit vector tangent to the sphere at `dir`.
fn random_tangent(dir: Vector3, rng: &mut SeededRng) -> Vector3 {
    let b = basis_with_up(dir);
    let t = rng.range_f32(0.0, std::f32::consts::TAU);
    (b.col_a() * t.cos() + b.col_c() * t.sin()).normalized()
}

/// Per-vertex painting data for the terrain shader.
pub struct SurfaceHints {
    pub colors: Vec<Color>,
    pub uv: Vec<Vector2>,
    pub uv2: Vec<Vector2>,
}

/// Godot mesh with colours and two UV channels carrying painting hints.
pub fn array_mesh_with_hints(m: &MeshData, hints: &SurfaceHints) -> Gd<ArrayMesh> {
    mesh_from_arrays(m, Some(&hints.colors), Some((&hints.uv, &hints.uv2)))
}

/// Godot mesh from our mesh data, optionally with per-vertex colours.
pub fn array_mesh(m: &MeshData, colors: Option<&[Color]>) -> Gd<ArrayMesh> {
    mesh_from_arrays(m, colors, None)
}

fn mesh_from_arrays(
    m: &MeshData,
    colors: Option<&[Color]>,
    uvs: Option<(&[Vector2], &[Vector2])>,
) -> Gd<ArrayMesh> {
    let mut arrays = VarArray::new();
    arrays.resize(ArrayType::MAX.ord() as usize, &Variant::nil());
    let positions: PackedVector3Array = m.positions.iter().copied().collect();
    let normals: PackedVector3Array = m.normals.iter().copied().collect();
    let indices: PackedInt32Array = m.indices.iter().copied().collect();
    arrays.set(ArrayType::VERTEX.ord() as usize, &positions.to_variant());
    arrays.set(ArrayType::NORMAL.ord() as usize, &normals.to_variant());
    if let Some(colors) = colors {
        let colors: PackedColorArray = colors.iter().copied().collect();
        arrays.set(ArrayType::COLOR.ord() as usize, &colors.to_variant());
    }
    if let Some((uv, uv2)) = uvs {
        let uv: PackedVector2Array = uv.iter().copied().collect();
        let uv2: PackedVector2Array = uv2.iter().copied().collect();
        arrays.set(ArrayType::TEX_UV.ord() as usize, &uv.to_variant());
        arrays.set(ArrayType::TEX_UV2.ord() as usize, &uv2.to_variant());
    }
    arrays.set(ArrayType::INDEX.ord() as usize, &indices.to_variant());
    let mut mesh = ArrayMesh::new_gd();
    mesh.add_surface_from_arrays(PrimitiveType::TRIANGLES, &arrays);
    mesh
}

fn triangle_soup(m: &MeshData) -> PackedVector3Array {
    m.indices.iter().map(|&i| m.positions[i as usize]).collect()
}

/// Seamless painterly noise for the shaders.
fn noise_texture(frequency: f32, octaves: i32, seed: i32) -> Gd<Texture2D> {
    let mut noise = FastNoiseLite::new_gd();
    noise.set_frequency(frequency);
    noise.set_fractal_octaves(octaves);
    noise.set_seed(seed);
    let mut texture = NoiseTexture2D::new_gd();
    texture.set_width(512);
    texture.set_height(512);
    texture.set_seamless(true);
    texture.set_generate_mipmaps(true);
    texture.set_noise(&noise);
    texture.upcast()
}

fn rgb((r, g, b): (f32, f32, f32)) -> Color {
    Color::from_rgb(r, g, b)
}

/// An orthonormal basis whose Y axis points along `up`.
pub fn basis_with_up(up: Vector3) -> Basis {
    let up = up.normalized();
    let reference = if up.x.abs() < 0.9 {
        Vector3::RIGHT
    } else {
        Vector3::FORWARD
    };
    let z = reference.cross(up).normalized();
    let x = up.cross(z);
    Basis::from_cols(x, up, z)
}
