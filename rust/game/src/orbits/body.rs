//! A celestial body: painted terrain surface, scattered rocks and point gravity.
//!
//! The surface comes from `procgen::terrain` (hills, ridges and craters) and
//! is drawn with the TF2-style painted shader. Phase 5 replaces it with
//! streamed terrain and the full feature sets; this is slice scope.

use godot::classes::mesh::{ArrayType, PrimitiveType};
use godot::classes::{
    ArrayMesh, CollisionShape3D, ConcavePolygonShape3D, ConvexPolygonShape3D, FastNoiseLite, IStaticBody3D,
    MeshInstance3D, NoiseTexture2D, Shader, ShaderMaterial, StaticBody3D, Texture2D,
};
use godot::prelude::*;
use serde::Deserialize;

use super::gravity::{self, GravitySettings};
use crate::procgen::rocks::rock_mesh;
use crate::procgen::terrain::{MeshData, TerrainData, TerrainShape};
use crate::seed::SeededRng;

/// Scene-tree group every gravity source joins, so other nodes can find them.
pub const GRAVITY_GROUP: &str = "gravity_wells";

const TERRAIN_SHADER: &str = "res://shaders/painted_terrain.gdshader";
const PROP_SHADER: &str = "res://shaders/painted_prop.gdshader";

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
}

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
        Self { data_path: GString::new(), data: None, shape: None, base }
    }

    fn ready(&mut self) {
        let path = self.data_path.to_string();
        let Some(data) = crate::data::try_load::<BodyData>(&path) else {
            godot_error!("CelestialBody {}: no data, body disabled", self.base().get_name());
            return;
        };
        let shape = TerrainShape::new(&data.terrain, data.radius);
        self.build_surface(&data, &shape);
        self.scatter_rocks(&data, &shape);
        self.base_mut().add_to_group(GRAVITY_GROUP);
        godot_print!("CelestialBody ready: {} (radius {} m)", data.name, data.radius);
        self.data = Some(data);
        self.shape = Some(shape);
    }
}

#[godot_api]
impl CelestialBody {
    /// Gravity acceleration this body applies at a world position.
    pub fn gravity_at(&self, point: Vector3) -> Vector3 {
        match &self.data {
            Some(d) => gravity::point_gravity(self.base().get_global_position(), d.radius, &d.gravity, point),
            None => Vector3::ZERO,
        }
    }

    /// True if `point` is inside this body's breathable air.
    pub fn breathable_at(&self, point: Vector3) -> bool {
        let Some(d) = &self.data else { return false };
        match &d.atmosphere {
            Some(a) if a.breathable => self.base().get_global_position().distance_to(point) <= self.surface_radius_toward(point) + a.height_m,
            _ => false,
        }
    }

    /// Nominal radius (hills and craters vary around it).
    #[func]
    pub fn radius(&self) -> f32 {
        self.data.as_ref().map_or(0.0, |d| d.radius)
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
        let height_range = (data.terrain.hill_height_m + data.terrain.ridge_height_m).max(0.5) * 2.0;
        let colors: Vec<Color> = mesh_data
            .samples
            .iter()
            .map(|s| Color::from_rgba(s.crater_floor, s.crater_rim, (s.height / height_range + 0.5).clamp(0.0, 1.0), 1.0))
            .collect();
        let mesh = array_mesh(&mesh_data, Some(&colors));

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
        material.set_shader_parameter("detail_noise", &noise_texture(0.012, 4, data.terrain.seed as i32).to_variant());
        material.set_shader_parameter("grain_noise", &noise_texture(0.09, 2, data.terrain.seed as i32 + 1).to_variant());

        let mut instance = MeshInstance3D::new_alloc();
        instance.set_name("SurfaceMesh");
        instance.set_mesh(&mesh);
        instance.set_material_override(&material);
        self.base_mut().add_child(&instance);

        let mut collision_shape = ConcavePolygonShape3D::new_gd();
        collision_shape.set_faces(&triangle_soup(&mesh_data));
        let mut collision = CollisionShape3D::new_alloc();
        collision.set_name("SurfaceCollision");
        collision.set_shape(&collision_shape);
        self.base_mut().add_child(&collision);
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
        material.set_shader_parameter("grain_noise", &noise_texture(0.08, 3, data.terrain.seed as i32 + 2).to_variant());
        material.set_shader_parameter("grain_scale", &0.6f32.to_variant());
        material.set_shader_parameter("grain_strength", &0.45f32.to_variant());

        // A few distinct rock shapes, reused (cheap to draw, still varied by scale and turn).
        let variants: Vec<(Gd<ArrayMesh>, PackedVector3Array)> = (0..6)
            .map(|i| {
                let m = rock_mesh(data.terrain.seed as u32 ^ (i * 7919), 2, r.roughness, rng.range_f32(0.5, 0.85));
                (array_mesh(&m, None), m.positions.iter().copied().collect())
            })
            .collect();

        let mut placed = 0;
        while placed < r.count {
            let center = crate::procgen::scatter_on_sphere(&mut rng, 1)[0];
            let in_cluster = rng.range_i64(r.cluster_size.0 as i64, r.cluster_size.1 as i64 + 1) as u32;
            for _ in 0..in_cluster.min(r.count - placed) {
                let offset = Vector3::new(rng.range_f32(-1.0, 1.0), rng.range_f32(-1.0, 1.0), rng.range_f32(-1.0, 1.0)) * (r.cluster_spread_m / data.radius);
                let dir = (center + offset).normalized();
                let t = rng.next_f32().powi(3);
                let size = r.size_m.0 + (r.size_m.1 - r.size_m.0) * t;
                let (mesh, points) = &variants[rng.below(variants.len() as u64) as usize];
                let up_basis = super::body::basis_with_up(dir);
                let turn = Basis::from_axis_angle(Vector3::UP, rng.range_f32(0.0, std::f32::consts::TAU));
                let basis = (up_basis * turn).scaled(Vector3::new(size, size, size));
                // Sink rocks a little so they sit in the ground, not on it.
                let origin = dir * (shape.surface_radius(dir) + size * 0.15);
                let transform = Transform3D::new(basis, origin);

                let mut instance = MeshInstance3D::new_alloc();
                instance.set_name(&format!("Rock{placed}"));
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
}

/// Godot mesh from our mesh data, optionally with per-vertex colours.
pub fn array_mesh(m: &MeshData, colors: Option<&[Color]>) -> Gd<ArrayMesh> {
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
    let reference = if up.x.abs() < 0.9 { Vector3::RIGHT } else { Vector3::FORWARD };
    let z = reference.cross(up).normalized();
    let x = up.cross(z);
    Basis::from_cols(x, up, z)
}
