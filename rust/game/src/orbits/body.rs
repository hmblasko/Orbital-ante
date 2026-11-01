//! A celestial body: painted terrain surface, scattered rocks and point gravity.
//! Gas giants add a banded cloud deck over a deep inner surface, stars are a
//! glowing sphere, and air shows as a soft glow shell.
//!
//! Far-away bodies can start as a cheap preview and build full detail (fine
//! meshes, collision, rocks, hoodoos) on a worker thread when asked, so a
//! whole solar system loads quickly and never stalls play (section 15:
//! "no loading during play").
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
use std::sync::Arc;
use std::thread::JoinHandle;

use super::gravity::{self, GravitySettings};
use crate::procgen::caves::{Cave, CaveData, caves_for, cut_ground};
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
const CLOUD_SHADER: &str = "res://shaders/gas_giant.gdshader";
const STAR_SHADER: &str = "res://shaders/star.gdshader";
const AIR_SHADER: &str = "res://shaders/air_glow.gdshader";
const RING_SHADER: &str = "res://shaders/planet_ring.gdshader";

#[derive(Debug, Clone, Deserialize)]
pub struct BodyData {
    pub name: String,
    /// Nominal surface radius in metres (hills and craters vary around it).
    pub radius: f32,
    pub gravity: GravitySettings,
    /// Breathable air near the surface. Missing means vacuum (the suit uses oxygen).
    #[serde(default)]
    pub atmosphere: Option<AtmosphereData>,
    /// Stars have no terrain (leave terrain, look and rocks out).
    #[serde(default)]
    pub terrain: TerrainData,
    #[serde(default)]
    pub look: SurfaceLook,
    #[serde(default)]
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
    /// Cells per cube-face edge of the cheap preview shown before full detail.
    #[serde(default = "default_preview_subdivisions")]
    pub preview_subdivisions: u32,
    /// A gas giant's cloud deck, high above its inner surface.
    #[serde(default)]
    pub clouds: Option<CloudLook>,
    /// A star: a glowing sphere with no surface to stand on.
    #[serde(default)]
    pub star: Option<StarLook>,
    /// A soft glow of air around the body seen from space (section 4:
    /// "orange haze on Mars, light blue on Earth; none on moons").
    #[serde(default)]
    pub air_glow: Option<AirGlow>,
    /// Lava tubes opening from the sides of this body's sinkholes.
    #[serde(default)]
    pub caves: Option<CaveData>,
    /// Storms, rain, haze and wind (bodies with air only).
    #[serde(default)]
    pub weather: Option<super::weather::WeatherData>,
    /// Erupting CO2 vents (Mars's south polar cap).
    #[serde(default)]
    pub geysers: Option<super::geysers::GeyserData>,
    /// Swimming and the underwater look, below the water table
    /// (`terrain.landforms.water_level_m`). Without it, water is scenery.
    #[serde(default)]
    pub ocean: Option<OceanData>,
}

/// How water feels and looks from inside (Neptune's ocean, Earth's lakes).
#[derive(Debug, Clone, Deserialize)]
pub struct OceanData {
    /// Share of gravity the water holds you up against (1 = you float).
    pub buoyancy: f32,
    /// Share of your speed the water takes away each second.
    pub drag_per_s: f32,
    /// Walking on the bottom is this much slower than on land.
    pub walk_scale: f32,
    /// Water colour and murk just under the surface, and in the deep.
    pub fog: (f32, f32, f32),
    pub deep_fog: (f32, f32, f32),
    pub fog_density: f32,
    pub deep_fog_density: f32,
    /// Depth (metres) where the deep colour, murk and darkness take over.
    pub dark_depth_m: f32,
    /// Share of sunlight gone at `dark_depth_m` and below.
    pub max_sun_dim: f32,
}

fn default_preview_subdivisions() -> u32 {
    48
}

/// Banded clouds of a gas giant, painted from four colours.
#[derive(Debug, Clone, Deserialize)]
pub struct CloudLook {
    /// Cloud-top radius in metres (the inner surface uses `radius`).
    pub radius_m: f32,
    pub band_a: (f32, f32, f32),
    pub band_b: (f32, f32, f32),
    pub band_c: (f32, f32, f32),
    pub band_d: (f32, f32, f32),
    /// Bands from pole to pole.
    pub bands: f32,
    /// How much the bands swirl and wobble (0 = straight stripes).
    pub turbulence: f32,
    /// A big storm oval (like Jupiter's Great Red Spot): latitude in
    /// degrees, size as a fraction of the radius; 0 size = none.
    #[serde(default)]
    pub storm_latitude_deg: f32,
    #[serde(default)]
    pub storm_size: f32,
    #[serde(default)]
    pub storm_color: (f32, f32, f32),
    /// Saturn-style rings: inner and outer radius in metres; (0, 0) = none.
    #[serde(default)]
    pub ring_m: (f32, f32),
    #[serde(default)]
    pub ring_color: (f32, f32, f32),
}

#[derive(Debug, Clone, Deserialize)]
pub struct StarLook {
    pub core: (f32, f32, f32),
    pub edge: (f32, f32, f32),
    /// Emission strength (drives the bloom).
    pub energy: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct AirGlow {
    pub color: (f32, f32, f32),
    /// Shell height above the surface (or cloud tops), in metres.
    pub height_m: f32,
    pub strength: f32,
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
    /// Dark mare basalt plains.
    #[serde(default = "default_mare")]
    pub mare: (f32, f32, f32),
    /// Bright fresh ejecta rays.
    #[serde(default = "default_rays")]
    pub rays: (f32, f32, f32),
    /// Frost in shadowed crater floors.
    #[serde(default = "default_frost")]
    pub frost: (f32, f32, f32),
    /// Glassy impact melt.
    #[serde(default = "default_melt")]
    pub melt: (f32, f32, f32),
    /// How much snow breaks into drifts: 1 = patchy, 0 = a solid cap.
    #[serde(default = "default_snow_drift")]
    pub snow_drift: f32,
}

fn default_snow_drift() -> f32 {
    1.0
}

fn default_mare() -> (f32, f32, f32) {
    (0.24, 0.24, 0.26)
}
fn default_rays() -> (f32, f32, f32) {
    (0.92, 0.91, 0.88)
}
fn default_frost() -> (f32, f32, f32) {
    (0.80, 0.86, 0.95)
}
fn default_melt() -> (f32, f32, f32) {
    (0.10, 0.11, 0.14)
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
            mare: default_mare(),
            rays: default_rays(),
            frost: default_frost(),
            melt: default_melt(),
            snow_drift: default_snow_drift(),
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
#[derive(Debug, Clone, Deserialize, Default)]
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

#[derive(Debug, Clone, Deserialize, Default)]
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
    /// Start with a cheap preview and build full detail only when
    /// `request_detail` is called (the solar system does this as you
    /// approach). Off: build everything at once (test scenes).
    #[export]
    stream_detail: bool,
    data: Option<BodyData>,
    shape: Option<Arc<TerrainShape>>,
    caves: Arc<Vec<Cave>>,
    material: Option<Gd<ShaderMaterial>>,
    detail: Detail,
    /// Nodes that make up the full detail, freed by `release_detail`.
    detail_nodes: Vec<Gd<Node>>,
    /// This body's moons (or planets, for the Sun) and how close to one
    /// (in its radii) this body's pull is gone entirely.
    shields: Vec<Gd<CelestialBody>>,
    shield_inner_radii: f32,
    base: Base<StaticBody3D>,
}

/// Where a body's full detail is.
enum Detail {
    None,
    Building(JoinHandle<SurfaceMeshes>),
    Built,
}

/// Meshes for a body's full detail, built off the main thread.
struct SurfaceMeshes {
    collision: MeshData,
    near: Option<MeshData>,
    far: Option<MeshData>,
    /// One rock-wall mesh per lava tube.
    caves: Vec<MeshData>,
}

impl SurfaceMeshes {
    fn build(data: &BodyData, shape: &TerrainShape, caves: &[Cave]) -> Self {
        let mut collision = shape.build_mesh();
        let (mut near, mut far) = match &data.lod {
            Some(lod) => (
                Some(shape.build_mesh_with(lod.near_subdivisions)),
                Some(shape.build_mesh_with(lod.far_subdivisions)),
            ),
            None => (None, None),
        };
        // Open the cave mouths in every grid that is walked or seen up close.
        cut_ground(&mut collision, caves);
        for m in [near.as_mut(), far.as_mut()].into_iter().flatten() {
            cut_ground(m, caves);
        }
        let caves = match &data.caves {
            Some(c) => caves.iter().map(|cave| cave.build_mesh(c)).collect(),
            None => Vec::new(),
        };
        Self { collision, near, far, caves }
    }
}

#[godot_api]
impl IStaticBody3D for CelestialBody {
    fn init(base: Base<StaticBody3D>) -> Self {
        Self {
            data_path: GString::new(),
            stream_detail: false,
            data: None,
            shape: None,
            caves: Arc::new(Vec::new()),
            material: None,
            detail: Detail::None,
            detail_nodes: Vec::new(),
            shields: Vec::new(),
            shield_inner_radii: 0.0,
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
        let run_seed = if godot::classes::Engine::singleton().has_singleton("RunSeed") { crate::seed::singleton::run_seed() } else { 0 };
        let shape = Arc::new(TerrainShape::with_run_seed(&data.terrain, data.radius, run_seed));
        self.caves = Arc::new(match &data.caves {
            Some(c) => caves_for(&shape, c, run_seed ^ data.terrain.seed),
            None => Vec::new(),
        });
        self.base_mut().add_to_group(GRAVITY_GROUP);
        self.data = Some(data.clone());
        self.shape = Some(shape.clone());
        if let Some(star) = &data.star {
            self.build_star(&data, star);
        } else {
            self.material = Some(surface_material(&data));
            self.build_water(&data, &shape);
            if let Some(clouds) = &data.clouds {
                self.build_clouds(clouds);
            }
            if let Some(g) = &data.geysers {
                if let Some(node) = super::geysers::Geysers::build(self, g, run_seed) {
                    self.base_mut().add_child(&node);
                }
            }
            // Faction camps (vertical slice; full bases are Phase 8).
            let body_id = self.base().get_name().to_string();
            for e in crate::ai::camp::camp_enemies(&body_id, &shape, run_seed, 1) {
                self.base_mut().add_child(&e);
            }
            for piece in crate::ai::base::base_structures(&body_id, &shape, run_seed, 1) {
                self.base_mut().add_child(&piece);
            }
            // Dishes to switch on and satellites to repair (Phase 8 slice).
            for site in crate::items::sites::sites_for_body(&body_id, &shape, run_seed) {
                self.base_mut().add_child(&site);
            }
            if self.stream_detail {
                self.build_preview(&data, &shape);
            } else {
                let meshes = SurfaceMeshes::build(&data, &shape, &self.caves.clone());
                self.finish_detail(meshes);
            }
        }
        if let Some(air) = &data.air_glow {
            let top = data.clouds.as_ref().map_or(data.radius, |c| c.radius_m);
            self.build_air_glow(air, top);
        }
        godot_print!(
            "CelestialBody ready: {} (radius {} m)",
            data.name,
            data.radius
        );
    }

    fn process(&mut self, _delta: f64) {
        if matches!(&self.detail, Detail::Building(job) if job.is_finished()) {
            let Detail::Building(job) = std::mem::replace(&mut self.detail, Detail::Built) else {
                return;
            };
            match job.join() {
                Ok(meshes) => self.finish_detail(meshes),
                Err(_) => godot_error!("CelestialBody {}: detail build failed", self.base().get_name()),
            }
        }
    }
}

#[godot_api]
impl CelestialBody {
    /// Start building full detail in the background (no effect if it is
    /// already built or building, or the body is a star).
    /// Re-man this body's camps for `ante`: soldiers still standing (or
    /// tied up and left behind) are replaced by a fresh seeded squad.
    #[func]
    pub fn respawn_camps(&mut self, ante: i64) {
        let Some(shape) = self.shape.clone() else { return };
        for n in self.base().get_children().iter_shared() {
            if n.is_in_group(crate::ai::camp::CAMP_GROUP) || n.is_in_group(crate::ai::base::BASE_GROUP) {
                let mut n = n;
                n.queue_free();
            }
        }
        let body_id = self.base().get_name().to_string();
        let run_seed = if godot::classes::Engine::singleton().has_singleton("RunSeed") { crate::seed::singleton::run_seed() } else { 0 };
        for e in crate::ai::camp::camp_enemies(&body_id, &shape, run_seed, ante.max(1) as u32) {
            self.base_mut().add_child(&e);
        }
        for piece in crate::ai::base::base_structures(&body_id, &shape, run_seed, ante.max(1) as u32) {
            self.base_mut().add_child(&piece);
        }
    }

    #[func]
    pub fn request_detail(&mut self) {
        if !matches!(self.detail, Detail::None) {
            return;
        }
        let (Some(data), Some(shape)) = (self.data.clone(), self.shape.clone()) else {
            return;
        };
        if data.star.is_some() {
            self.detail = Detail::Built;
            return;
        }
        let caves = self.caves.clone();
        self.detail = Detail::Building(std::thread::spawn(move || SurfaceMeshes::build(&data, &shape, &caves)));
    }

    /// True where sunlight can't reach: inside a lava tube, or down in a
    /// permanently shadowed crater. The helmet lamp switches on here.
    #[func]
    pub fn is_dark_at(&self, world_point: Vector3) -> bool {
        let Some(shape) = &self.shape else { return false };
        let local = self.base().get_global_transform().affine_inverse() * world_point;
        if self.caves.iter().any(|c| c.contains(local)) {
            return true;
        }
        let dir = local.normalized();
        shape.landforms().list.iter().any(|l| {
            l.kind == LandformKind::ColdTrap
                && dir.angle_to(l.center) < l.angle * 0.8
                && local.length() < shape.radius() - l.height * 0.35
        })
    }

    /// Lava tubes on this body: each is a dictionary with the mouth and a
    /// point on the floor deep inside (body space), for the tests and for
    /// placing tanks and chests later.
    #[func]
    pub fn get_caves(&self) -> VarArray {
        let mut out = VarArray::new();
        for c in self.caves.iter() {
            let mut d = VarDictionary::new();
            d.set("mouth", &c.floor_point(0.0).to_variant());
            d.set("inside", &c.floor_point(0.6).to_variant());
            d.set("chamber", &c.floor_point(0.8).to_variant());
            out.push(&d.to_variant());
        }
        out
    }

    /// Every landform on this body (kind name, direction from the centre in
    /// body space, footprint radius in metres). Later systems (bases, dishes,
    /// tanks, the horror zone) and the tests read features from here.
    #[func]
    pub fn get_landforms(&self) -> VarArray {
        let mut out = VarArray::new();
        let Some(shape) = &self.shape else { return out };
        for l in &shape.landforms().list {
            let mut d = VarDictionary::new();
            d.set("kind", &GString::from(&format!("{:?}", l.kind)).to_variant());
            d.set("dir", &l.center.to_variant());
            d.set("radius_m", &l.radius_m.to_variant());
            d.set("height_m", &l.height.to_variant());
            out.push(&d.to_variant());
        }
        out
    }

    /// Drop full detail and go back to the cheap preview (frees memory for
    /// bodies far behind you). Only once the detail has finished building.
    #[func]
    pub fn release_detail(&mut self) {
        if !self.stream_detail || !matches!(self.detail, Detail::Built) {
            return;
        }
        for mut node in std::mem::take(&mut self.detail_nodes) {
            if node.is_instance_valid() {
                node.queue_free();
            }
        }
        self.detail = Detail::None;
        if let (Some(data), Some(shape)) = (self.data.clone(), self.shape.clone()) {
            self.build_preview(&data, &shape);
        }
    }

    /// True once fine meshes, collision, rocks and hoodoos are in place.
    #[func]
    pub fn has_detail(&self) -> bool {
        matches!(self.detail, Detail::Built) && self.base().get_node_or_null("Preview").is_none()
    }

    /// Set the data file before the body enters the tree (for spawning from code).
    pub fn configure(&mut self, data_path: &str, stream_detail: bool) {
        self.data_path = GString::from(data_path);
        self.stream_detail = stream_detail;
    }

    /// Gravity acceleration this body applies at a world position.
    pub fn gravity_at(&self, point: Vector3) -> Vector3 {
        let Some(d) = &self.data else { return Vector3::ZERO };
        let g = gravity::point_gravity(self.base().get_global_position(), d.radius, &d.gravity, point);
        if g == Vector3::ZERO {
            return g;
        }
        // Near one of our moons, its own pull takes over.
        let mut w = 1.0f32;
        for moon in &self.shields {
            let m = moon.bind();
            let Some(md) = &m.data else { continue };
            let dist = (point - moon.get_global_position()).length();
            w = w.min(gravity::shield_weight(dist, md.radius * self.shield_inner_radii, md.gravity.influence_radius));
        }
        g * w
    }

    /// The bodies orbiting this one; this body's pull fades out near them,
    /// gone entirely within `inner_radii` of their radius.
    pub fn set_shields(&mut self, moons: Vec<Gd<CelestialBody>>, inner_radii: f32) {
        self.shields = moons;
        self.shield_inner_radii = inner_radii;
    }

    /// True if `point` is inside this body's breathable air.
    pub fn breathable_at(&self, point: Vector3) -> bool {
        let Some(d) = &self.data else { return false };
        if d.ocean.is_some() && self.water_depth_at(point) > 0.0 {
            return false;
        }
        match &d.atmosphere {
            Some(a) if a.breathable => {
                self.base().get_global_position().distance_to(point)
                    <= self.surface_radius_toward(point) + a.height_m
            }
            _ => false,
        }
    }

    /// Map shape: visible radius (cloud tops for gas giants), outer ring
    /// radius (0 = none), and whether it is a star.
    pub fn map_shape(&self) -> (f32, f32, bool) {
        let Some(d) = &self.data else { return (0.0, 0.0, false) };
        let radius = d.clouds.as_ref().map_or(d.radius, |c| c.radius_m);
        let ring = d.clouds.as_ref().map_or(0.0, |c| if c.ring_m.1 > c.ring_m.0 { c.ring_m.1 } else { 0.0 });
        (radius, ring, d.star.is_some())
    }

    /// Water table above the nominal radius (None on a dry body).
    pub fn water_level(&self) -> Option<f32> {
        self.shape.as_ref().and_then(|s| s.water_level())
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
        let t = self.base().get_global_transform();
        let dir = t.basis.inverse() * (world_point - t.origin);
        self.surface_radius_local(dir)
    }

    /// The height function (None before ready or for a missing data file).
    pub fn shape(&self) -> Option<Arc<TerrainShape>> {
        self.shape.clone()
    }

    /// Swimming and underwater look, if this body has them.
    pub fn ocean(&self) -> Option<OceanData> {
        self.data.as_ref().and_then(|d| d.ocean.clone())
    }

    /// How far below the water surface `world_point` is, in metres
    /// (negative above it, or no water at all).
    #[func]
    pub fn water_depth_at(&self, world_point: Vector3) -> f32 {
        let Some(level) = self.water_level() else { return -1.0e9 };
        let r = self.base().get_global_position().distance_to(world_point);
        self.radius() + level - r
    }

    /// This body's weather, if it has air.
    pub fn weather_data(&self) -> Option<super::weather::WeatherData> {
        self.data.as_ref().and_then(|d| d.weather.clone())
    }

    /// The body's display name from its data file.
    pub fn body_name(&self) -> String {
        self.data.as_ref().map_or_else(String::new, |d| d.name.clone())
    }

    /// Distance from the centre to the ground along a direction in the
    /// body's own (rotating) frame.
    pub fn surface_radius_local(&self, dir: Vector3) -> f32 {
        match &self.shape {
            Some(s) if dir.length_squared() > 1e-8 => s.surface_radius(dir),
            _ => self.radius(),
        }
    }

    /// Full detail: fine surface tiles, tiled collision, rocks, hoodoos and lava glow.
    fn finish_detail(&mut self, meshes: SurfaceMeshes) {
        let (Some(data), Some(shape), Some(material)) = (self.data.clone(), self.shape.clone(), self.material.clone()) else {
            return;
        };
        let data = &data;
        let shape = &*shape;
        let height_range = height_range(data);
        let mesh_data = &meshes.collision;
        let before: std::collections::HashSet<_> = self.base().get_children().iter_shared().map(|n| n.instance_id()).collect();
        match &data.lod {
            // Fine tiles close to the camera, coarse ones beyond; collision
            // stays on the middle grid (`terrain.subdivisions`).
            Some(lod) => {
                if let Some(near) = &meshes.near {
                    self.add_surface_tiles(near, lod.near_subdivisions, lod.tiles, &material, height_range, "Near", (0.0, lod.near_range_m));
                }
                if let Some(far) = &meshes.far {
                    self.add_surface_tiles(far, lod.far_subdivisions, lod.tiles, &material, height_range, "Far", (lod.near_range_m, 0.0));
                }
            }
            None => self.add_surface_tiles(mesh_data, data.terrain.subdivisions, data.render_tiles, &material, height_range, "", (0.0, 0.0)),
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
        self.add_caves(data, &meshes.caves, &material, height_range);
        self.scatter_rocks(data, shape);
        self.add_lava_lights(data, shape);
        self.scatter_hoodoos(data, shape);
        self.detail_nodes = self
            .base()
            .get_children()
            .iter_shared()
            .filter(|n| !before.contains(&n.instance_id()))
            .collect();
        for name in ["Preview", "PreviewCollision"] {
            if let Some(mut node) = self.base().get_node_or_null(name) {
                // Rename first so `has_detail` is true straight away.
                node.set_name(&format!("{name}Old"));
                node.queue_free();
            }
        }
        self.detail = Detail::Built;
    }

    /// A cheap, coarse surface (no collision detail, no props) shown until
    /// full detail arrives. A smooth sphere at the lowest ground keeps
    /// anything from falling into the body meanwhile.
    fn build_preview(&mut self, data: &BodyData, shape: &TerrainShape) {
        let Some(material) = self.material.clone() else { return };
        let mut holder = Node3D::new_alloc();
        holder.set_name("Preview");
        self.base_mut().add_child(&holder);
        let n = data.preview_subdivisions.max(4);
        let mesh = shape.build_mesh_with(n);
        let hints = surface_hints(&mesh, height_range(data));
        let mut instance = MeshInstance3D::new_alloc();
        instance.set_name("PreviewSurface");
        instance.set_mesh(&array_mesh_with_hints(&mesh, &hints));
        instance.set_instance_shader_parameter("tile_offset", &Vector3::ZERO.to_variant());
        instance.set_material_override(&material);
        holder.add_child(&instance);
        let mut sphere = SphereShape3D::new_gd();
        sphere.set_radius(shape.min_radius());
        let mut collision = CollisionShape3D::new_alloc();
        collision.set_name("PreviewCollision");
        collision.set_shape(&sphere);
        // Collision shapes must be direct children of the body.
        self.base_mut().add_child(&collision);
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
            let hints = surface_hints(tile, height_range);
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
    /// Lava tube walls (painted like the ground, walkable from inside) and
    /// the rubble piled around each mouth.
    fn add_caves(&mut self, data: &BodyData, meshes: &[MeshData], material: &Gd<ShaderMaterial>, height_range: f32) {
        let Some(cave_data) = &data.caves else { return };
        let caves = self.caves.clone();
        let rock_material = rock_material(data);
        // Same paint as the ground, but no sunlight in full shadow.
        let mut material = material.duplicate_resource();
        material.set_shader_parameter("sun_shadow_floor", &0.0f32.to_variant());
        // Softer grain: the lamp grazes the floor and strong bumps read as ripples.
        material.set_shader_parameter("bump_strength", &cave_data.bump_strength.to_variant());
        for (i, (cave, mesh)) in caves.iter().zip(meshes).enumerate() {
            let hints = surface_hints(mesh, height_range);
            let mut walls = MeshInstance3D::new_alloc();
            walls.set_name(&format!("Cave{i}"));
            walls.set_mesh(&array_mesh_with_hints(mesh, &hints));
            walls.set_material_override(&material);
            walls.set_instance_shader_parameter("tile_offset", &Vector3::ZERO.to_variant());
            self.base_mut().add_child(&walls);
            let mut faces = ConcavePolygonShape3D::new_gd();
            faces.set_faces(&triangle_soup(mesh));
            faces.set_backface_collision_enabled(true);
            let mut collision = CollisionShape3D::new_alloc();
            collision.set_name(&format!("CaveCollision{i}"));
            collision.set_shape(&faces);
            self.base_mut().add_child(&collision);
            for (k, (at, size)) in cave.rubble(cave_data).into_iter().enumerate() {
                let m = rock_mesh(data.terrain.seed as u32 ^ (k as u32 * 131 + i as u32), 2, data.rocks.roughness, 0.7);
                let basis = (basis_with_up(at.normalized()) * Basis::from_axis_angle(Vector3::UP, k as f32 * 2.3)).scaled(Vector3::new(size, size * 0.8, size));
                let transform = Transform3D::new(basis, at);
                let mut rock = MeshInstance3D::new_alloc();
                rock.set_name(&format!("Cave{i}Rubble{k}"));
                rock.set_mesh(&array_mesh(&m, None));
                rock.set_material_override(&rock_material);
                rock.set_transform(transform);
                self.base_mut().add_child(&rock);
                let mut convex = ConvexPolygonShape3D::new_gd();
                convex.set_points(&m.positions.iter().copied().collect::<PackedVector3Array>());
                let mut shape = CollisionShape3D::new_alloc();
                shape.set_shape(&convex);
                shape.set_transform(transform);
                self.base_mut().add_child(&shape);
            }
        }
    }

    fn scatter_rocks(&mut self, data: &BodyData, shape: &TerrainShape) {
        let r = &data.rocks;
        if r.count == 0 {
            return;
        }
        let mut rng = SeededRng::derive(data.terrain.seed, "rocks");
        let material = rock_material(data);
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

    /// A star: a glowing sphere that never casts shadows.
    fn build_star(&mut self, data: &BodyData, star: &StarLook) {
        let mut material = ShaderMaterial::new_gd();
        if let Ok(shader) = try_load::<Shader>(STAR_SHADER) {
            material.set_shader(&shader);
        }
        material.set_shader_parameter("core_color", &rgb(star.core).to_variant());
        material.set_shader_parameter("edge_color", &rgb(star.edge).to_variant());
        material.set_shader_parameter("energy", &star.energy.to_variant());
        material.set_shader_parameter(
            "granules",
            &noise_texture(0.02, 4, data.terrain.seed as i32 + 7).to_variant(),
        );
        self.add_shell("Star", data.radius, 64, &material);
    }

    /// A gas giant's cloud deck (and rings, if any).
    fn build_clouds(&mut self, clouds: &CloudLook) {
        let mut material = ShaderMaterial::new_gd();
        if let Ok(shader) = try_load::<Shader>(CLOUD_SHADER) {
            material.set_shader(&shader);
        }
        for (name, c) in [
            ("band_a", clouds.band_a),
            ("band_b", clouds.band_b),
            ("band_c", clouds.band_c),
            ("band_d", clouds.band_d),
            ("storm_color", clouds.storm_color),
        ] {
            material.set_shader_parameter(name, &rgb(c).to_variant());
        }
        material.set_shader_parameter("bands", &clouds.bands.to_variant());
        material.set_shader_parameter("turbulence", &clouds.turbulence.to_variant());
        material.set_shader_parameter("storm_latitude", &clouds.storm_latitude_deg.to_radians().sin().to_variant());
        material.set_shader_parameter("storm_size", &clouds.storm_size.to_variant());
        material.set_shader_parameter(
            "swirl_noise",
            &noise_texture(0.015, 4, (clouds.radius_m * 7.0) as i32).to_variant(),
        );
        let mut shell = self.add_shell("Clouds", clouds.radius_m, 96, &material);
        shell.set_cast_shadows_setting(godot::classes::geometry_instance_3d::ShadowCastingSetting::ON);
        if clouds.ring_m.1 > clouds.ring_m.0 && clouds.ring_m.0 > 0.0 {
            let mut ring = ShaderMaterial::new_gd();
            if let Ok(shader) = try_load::<Shader>(RING_SHADER) {
                ring.set_shader(&shader);
            }
            ring.set_shader_parameter("ring_color", &rgb(clouds.ring_color).to_variant());
            ring.set_shader_parameter("inner_m", &clouds.ring_m.0.to_variant());
            ring.set_shader_parameter("outer_m", &clouds.ring_m.1.to_variant());
            ring.set_shader_parameter(
                "grain",
                &noise_texture(0.05, 3, (clouds.radius_m * 3.0) as i32).to_variant(),
            );
            let mut instance = MeshInstance3D::new_alloc();
            instance.set_name("Rings");
            instance.set_mesh(&array_mesh(&annulus(clouds.ring_m.0, clouds.ring_m.1, 256), None));
            instance.set_material_override(&ring);
            self.base_mut().add_child(&instance);
        }
    }

    /// A soft shell of air: a bright rim seen from space, a tinted sky from inside.
    fn build_air_glow(&mut self, air: &AirGlow, top: f32) {
        let mut material = ShaderMaterial::new_gd();
        if let Ok(shader) = try_load::<Shader>(AIR_SHADER) {
            material.set_shader(&shader);
        }
        material.set_shader_parameter("air_color", &rgb(air.color).to_variant());
        material.set_shader_parameter("strength", &air.strength.to_variant());
        material.set_shader_parameter("inner_radius", &top.to_variant());
        material.set_shader_parameter("outer_radius", &(top + air.height_m).to_variant());
        let mut shell = self.add_shell("AirGlow", top + air.height_m, 64, &material);
        shell.set_cast_shadows_setting(godot::classes::geometry_instance_3d::ShadowCastingSetting::OFF);
    }

    /// A smooth sphere mesh child with no shadow, returned for tweaks.
    fn add_shell(&mut self, name: &str, radius: f32, subdivisions: u32, material: &Gd<ShaderMaterial>) -> Gd<MeshInstance3D> {
        let mut instance = MeshInstance3D::new_alloc();
        instance.set_name(name);
        instance.set_mesh(&array_mesh(&sphere_mesh(radius, subdivisions), None));
        instance.set_material_override(material);
        instance.set_cast_shadows_setting(godot::classes::geometry_instance_3d::ShadowCastingSetting::OFF);
        self.base_mut().add_child(&instance);
        instance
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

/// Painted boulder material for a body.
fn rock_material(data: &BodyData) -> Gd<ShaderMaterial> {
    let r = &data.rocks;
    let mut material = ShaderMaterial::new_gd();
    if let Ok(shader) = try_load::<Shader>(PROP_SHADER) {
        material.set_shader(&shader);
    }
    material.set_shader_parameter("albedo", &rgb(r.color).to_variant());
    material.set_shader_parameter("albedo_dark", &rgb(r.color_dark).to_variant());
    material.set_shader_parameter("shadow_tint", &rgb(data.look.shadow_tint).to_variant());
    material.set_shader_parameter("grain_noise", &noise_texture(0.08, 3, data.terrain.seed as i32 + 2).to_variant());
    material.set_shader_parameter("grain_scale", &0.6f32.to_variant());
    material.set_shader_parameter("grain_strength", &0.45f32.to_variant());
    material
}

/// The painted terrain material for one body (shared by all its tiles).
fn surface_material(data: &BodyData) -> Gd<ShaderMaterial> {
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
        ("mare_color", land.mare),
        ("ray_color", land.rays),
        ("frost_color", land.frost),
        ("melt_color", land.melt),
    ] {
        material.set_shader_parameter(name, &rgb(c).to_variant());
    }
    material.set_shader_parameter("lava_energy", &land.lava_energy.to_variant());
    material.set_shader_parameter("snow_drift", &land.snow_drift.to_variant());
    material
}

/// Painting hints per vertex: COLOR = (crater floor, crater rim, height,
/// mineral), UV = (strata, lava), UV2 = (snow, wet), CUSTOM0 = (mare, rays,
/// frost, melt) as bytes.
fn surface_hints(tile: &MeshData, height_range: f32) -> SurfaceHints {
    SurfaceHints {
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
        custom: tile
            .samples
            .iter()
            .flat_map(|s| [s.land.mare, s.land.rays, s.land.ice, s.land.melt].map(|v| (v.clamp(0.0, 1.0) * 255.0).round() as u8))
            .collect(),
    }
}

/// A flat ring in the body's equator plane, UV.x running from inner (0) to outer (1).
fn annulus(inner: f32, outer: f32, segments: u32) -> MeshData {
    let mut m = MeshData::default();
    for i in 0..=segments {
        let a = i as f32 / segments as f32 * std::f32::consts::TAU;
        let (s, c) = a.sin_cos();
        for r in [inner, outer] {
            m.positions.push(Vector3::new(c * r, 0.0, s * r));
            m.normals.push(Vector3::UP);
            m.samples.push(Default::default());
        }
    }
    for i in 0..segments as i32 {
        let k = i * 2;
        m.indices.extend_from_slice(&[k, k + 1, k + 2, k + 1, k + 3, k + 2]);
    }
    m
}

fn height_range(data: &BodyData) -> f32 {
    (data.terrain.hill_height_m + data.terrain.ridge_height_m).max(0.5) * 2.0
}

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
    /// Four bytes per vertex (CUSTOM0, RGBA8 unorm).
    pub custom: Vec<u8>,
}

/// Godot mesh with colours and two UV channels carrying painting hints.
pub fn array_mesh_with_hints(m: &MeshData, hints: &SurfaceHints) -> Gd<ArrayMesh> {
    mesh_from_arrays(m, Some(&hints.colors), Some((&hints.uv, &hints.uv2)), Some(&hints.custom))
}

/// Godot mesh from our mesh data, optionally with per-vertex colours.
pub fn array_mesh(m: &MeshData, colors: Option<&[Color]>) -> Gd<ArrayMesh> {
    mesh_from_arrays(m, colors, None, None)
}

fn mesh_from_arrays(
    m: &MeshData,
    colors: Option<&[Color]>,
    uvs: Option<(&[Vector2], &[Vector2])>,
    custom: Option<&[u8]>,
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
    if let Some(custom) = custom {
        // RGBA8 unorm is custom format 0, the default, so no format flag is needed.
        let bytes: PackedByteArray = custom.iter().copied().collect();
        arrays.set(ArrayType::CUSTOM0.ord() as usize, &bytes.to_variant());
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
