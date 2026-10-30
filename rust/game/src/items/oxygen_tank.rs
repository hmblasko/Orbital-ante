//! Oxygen tanks (CLAUDE.md section 6): seeded pickups, collected with
//! Interact and used from the D-pad. Hauling tanks to the ship for value
//! needs the ship and points (Phases 4 and 7).
//!
//! Serves pillar 3 (every run counts: placement changes with the seed) and
//! fun rule 4 (curiosity: exploring finds air).

use godot::classes::light_3d::Param;
use godot::classes::{BoxMesh, CapsuleMesh, CylinderMesh, INode3D, Node3D, OmniLight3D, TorusMesh};
use godot::prelude::*;
use serde::Deserialize;

use super::look::{PaintSpec, at, paint, part};
use crate::orbits::CelestialBody;
use crate::orbits::body::basis_with_up;
use crate::procgen;
use crate::seed::SeededRng;

pub const TANK_GROUP: &str = "oxygen_tanks";

#[derive(GodotClass)]
#[class(base = Node3D, init)]
pub struct OxygenTankPickup {
    base: Base<Node3D>,
}

#[godot_api]
impl INode3D for OxygenTankPickup {
    fn ready(&mut self) {
        self.base_mut().add_to_group(TANK_GROUP);
        self.build_model();
    }
}

impl OxygenTankPickup {
    /// PLACEHOLDER model: a stubby teal cylinder with rubber bands, a brass
    /// valve and a glowing fill strip, lying on its side so it reads as
    /// "dropped gear" from a distance.
    fn build_model(&mut self) {
        let mut root = Node3D::new_alloc();
        root.set_name("PLACEHOLDER_OxygenTankModel");
        // Lying down, slightly sunk into the dust.
        root.set_transform(Transform3D::new(Basis::from_axis_angle(Vector3::BACK, std::f32::consts::FRAC_PI_2), Vector3::new(0.0, 0.17, 0.0)));
        let shell = paint(&PaintSpec::metal(Color::from_rgb(0.22, 0.72, 0.74), Color::from_rgb(0.10, 0.40, 0.45)));
        let rubber = paint(&PaintSpec::matte(Color::from_rgb(0.16, 0.16, 0.18), Color::from_rgb(0.08, 0.08, 0.1)));
        let brass = paint(&PaintSpec::metal(Color::from_rgb(0.92, 0.70, 0.32), Color::from_rgb(0.55, 0.38, 0.15)));
        let glow = paint(&PaintSpec::glow(Color::from_rgb(0.55, 1.0, 0.95), 2.5));

        let mut body = CapsuleMesh::new_gd();
        body.set_radius(0.17);
        body.set_height(0.72);
        body.set_radial_segments(24);
        body.set_rings(6);
        part(&mut root, "Shell", body, &shell, at(0.0, 0.0, 0.0));
        for (i, y) in [-0.18f32, 0.14].iter().enumerate() {
            let mut band = TorusMesh::new_gd();
            band.set_inner_radius(0.16);
            band.set_outer_radius(0.2);
            band.set_rings(24);
            part(&mut root, &format!("Band{i}"), band, &rubber, at(0.0, *y, 0.0));
        }
        let mut neck = CylinderMesh::new_gd();
        neck.set_top_radius(0.05);
        neck.set_bottom_radius(0.07);
        neck.set_height(0.1);
        part(&mut root, "Neck", neck, &brass, at(0.0, 0.4, 0.0));
        let mut handle = TorusMesh::new_gd();
        handle.set_inner_radius(0.05);
        handle.set_outer_radius(0.08);
        part(&mut root, "Valve", handle, &brass, Transform3D::new(Basis::from_axis_angle(Vector3::RIGHT, std::f32::consts::FRAC_PI_2), Vector3::new(0.0, 0.47, 0.0)));
        let mut strip = BoxMesh::new_gd();
        strip.set_size(Vector3::new(0.03, 0.22, 0.02));
        part(&mut root, "FillGauge", strip, &glow, at(0.0, -0.02, 0.172));
        self.base_mut().add_child(&root);

        let mut light = OmniLight3D::new_alloc();
        light.set_color(Color::from_rgb(0.5, 1.0, 0.95));
        light.set_param(Param::RANGE, 1.4);
        light.set_param(Param::ENERGY, 0.2);
        light.set_position(Vector3::new(0.0, 0.5, 0.0));
        self.base_mut().add_child(&light);
    }
}

#[derive(Debug, Clone, Deserialize)]
pub struct ScatterData {
    pub tanks_per_body: u32,
}

const DATA_PATH: &str = "res://data/items/oxygen_tanks.ron";
const DATA_EMBEDDED: &str = include_str!("../../../../godot/data/items/oxygen_tanks.ron");

/// Child of a `CelestialBody`: scatters that body's oxygen tanks from the run seed.
#[derive(GodotClass)]
#[class(base = Node3D, init)]
pub struct OxygenTankScatter {
    /// Separates this body's stream from other bodies' (same seed, different tanks).
    #[export]
    body_index: i64,
    base: Base<Node3D>,
}

#[godot_api]
impl INode3D for OxygenTankScatter {
    fn ready(&mut self) {
        // Wait one frame so the parent body has loaded its data and radius.
        self.base_mut().call_deferred("scatter", &[]);
    }
}

#[godot_api]
impl OxygenTankScatter {
    #[func]
    fn scatter(&mut self) {
        let Some(body) = self.base().get_parent().and_then(|p| p.try_cast::<CelestialBody>().ok()) else {
            godot_error!("OxygenTankScatter must be a child of a CelestialBody");
            return;
        };
        let data: ScatterData = crate::data::load(DATA_PATH, DATA_EMBEDDED);
        let center = body.get_global_position();
        let mut rng = SeededRng::derive_indexed(crate::seed::singleton::run_seed(), "oxygen_tanks", self.body_index as u64);
        for (i, dir) in procgen::scatter_on_sphere(&mut rng, data.tanks_per_body as usize).into_iter().enumerate() {
            let mut tank = OxygenTankPickup::new_alloc();
            tank.set_name(&format!("OxygenTank{i}"));
            let ground = body.bind().surface_radius_toward(center + dir);
            tank.set_transform(Transform3D::new(basis_with_up(dir), dir * ground));
            self.base_mut().add_child(&tank);
        }
    }
}
