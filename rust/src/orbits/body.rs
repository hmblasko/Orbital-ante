//! A celestial body: solid surface plus point gravity.
//!
//! Phase 0 builds a PLACEHOLDER sphere surface (default primitive, fails the
//! section 11 art bar on purpose). Phase 5 replaces it with real terrain.

use godot::classes::base_material_3d::{Flags, TextureParam};
use godot::classes::geometry_instance_3d::ShadowCastingSetting;
use godot::classes::{
    BoxMesh, CollisionShape3D, FastNoiseLite, IStaticBody3D, MeshInstance3D, NoiseTexture2D,
    SphereMesh, SphereShape3D, StandardMaterial3D, StaticBody3D,
};
use godot::prelude::*;
use serde::Deserialize;

use super::gravity::{self, GravitySettings};
use crate::procgen;

/// Scene-tree group every gravity source joins, so other nodes can find them.
pub const GRAVITY_GROUP: &str = "gravity_wells";

#[derive(Debug, Clone, Deserialize)]
pub struct BodyData {
    pub name: String,
    /// Surface radius in metres.
    pub radius: f32,
    pub gravity: GravitySettings,
    pub placeholder_look: PlaceholderLook,
}

/// How the Phase 0 placeholder surface looks. Goes away with real terrain.
#[derive(Debug, Clone, Deserialize)]
pub struct PlaceholderLook {
    pub base_color: (f32, f32, f32),
    pub noise_frequency: f32,
    pub texture_repeats: f32,
    pub radial_segments: i32,
    pub rings: i32,
    pub marker_count: usize,
    pub marker_size: (f32, f32, f32),
    pub marker_color: (f32, f32, f32),
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
    base: Base<StaticBody3D>,
}

#[godot_api]
impl IStaticBody3D for CelestialBody {
    fn init(base: Base<StaticBody3D>) -> Self {
        Self { data_path: GString::new(), data: None, base }
    }

    fn ready(&mut self) {
        let path = self.data_path.to_string();
        let Some(data) = crate::data::try_load::<BodyData>(&path) else {
            godot_error!("CelestialBody {}: no data, body disabled", self.base().get_name());
            return;
        };
        self.build_placeholder_surface(&data);
        self.base_mut().add_to_group(GRAVITY_GROUP);
        godot_print!("CelestialBody ready: {} (radius {} m)", data.name, data.radius);
        self.data = Some(data);
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

    pub fn radius(&self) -> f32 {
        self.data.as_ref().map_or(0.0, |d| d.radius)
    }

    fn build_placeholder_surface(&mut self, data: &BodyData) {
        let look = &data.placeholder_look;

        let mut sphere = SphereMesh::new_gd();
        sphere.set_radius(data.radius);
        sphere.set_height(data.radius * 2.0);
        sphere.set_radial_segments(look.radial_segments);
        sphere.set_rings(look.rings);

        let mut noise = FastNoiseLite::new_gd();
        noise.set_frequency(look.noise_frequency);
        let mut texture = NoiseTexture2D::new_gd();
        texture.set_noise(&noise);
        texture.set_seamless(true);

        let mut material = StandardMaterial3D::new_gd();
        material.set_albedo(rgb(look.base_color));
        material.set_texture(TextureParam::ALBEDO, &texture);
        material.set_flag(Flags::UV1_USE_TRIPLANAR, true);
        let repeat = look.texture_repeats / data.radius;
        material.set_uv1_scale(Vector3::new(repeat, repeat, repeat));
        sphere.set_material(&material);

        let mut mesh = MeshInstance3D::new_alloc();
        mesh.set_name("PLACEHOLDER_SurfaceMesh");
        mesh.set_mesh(&sphere);
        // Day/night shading comes from the light angle; letting the whole
        // planet shadow itself only adds acne at this scale.
        mesh.set_cast_shadows_setting(ShadowCastingSetting::OFF);
        self.base_mut().add_child(&mesh);

        let mut shape = SphereShape3D::new_gd();
        shape.set_radius(data.radius);
        let mut collision = CollisionShape3D::new_alloc();
        collision.set_shape(&shape);
        self.base_mut().add_child(&collision);

        self.add_placeholder_markers(data);
    }

    /// Evenly spaced pillars so the player can see themselves move.
    fn add_placeholder_markers(&mut self, data: &BodyData) {
        let look = &data.placeholder_look;
        let (w, h, d) = look.marker_size;
        let mut box_mesh = BoxMesh::new_gd();
        box_mesh.set_size(Vector3::new(w, h, d));
        let mut material = StandardMaterial3D::new_gd();
        material.set_albedo(rgb(look.marker_color));
        box_mesh.set_material(&material);

        let mut shape = godot::classes::BoxShape3D::new_gd();
        shape.set_size(Vector3::new(w, h, d));

        for (i, dir) in procgen::fibonacci_sphere(look.marker_count).into_iter().enumerate() {
            let basis = basis_with_up(dir);
            let origin = dir * (data.radius + h * 0.5);
            let mut marker = MeshInstance3D::new_alloc();
            marker.set_name(&format!("PLACEHOLDER_Marker{i}"));
            marker.set_mesh(&box_mesh);
            marker.set_transform(Transform3D::new(basis, origin));
            self.base_mut().add_child(&marker);

            let mut collision = CollisionShape3D::new_alloc();
            collision.set_shape(&shape);
            collision.set_transform(Transform3D::new(basis, origin));
            self.base_mut().add_child(&collision);
        }
    }
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
