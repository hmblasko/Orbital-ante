//! Oxygen tanks (CLAUDE.md section 6): seeded pickups, collected with
//! Interact and used from the D-pad. Hauling tanks to the ship for value
//! needs the ship and points (Phases 4 and 7).
//!
//! Serves pillar 3 (every run counts: placement changes with the seed) and
//! fun rule 4 (curiosity: exploring finds air).

use godot::classes::{CapsuleMesh, INode3D, MeshInstance3D, Node3D, StandardMaterial3D};
use godot::prelude::*;
use serde::Deserialize;

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
        let mut mesh = CapsuleMesh::new_gd();
        mesh.set_radius(0.18);
        mesh.set_height(0.7);
        let mut material = StandardMaterial3D::new_gd();
        material.set_albedo(Color::from_rgb(0.3, 0.85, 0.95));
        material.set_feature(godot::classes::base_material_3d::Feature::EMISSION, true);
        material.set_emission(Color::from_rgb(0.1, 0.4, 0.5));
        mesh.set_material(&material);
        let mut instance = MeshInstance3D::new_alloc();
        instance.set_name("PLACEHOLDER_OxygenTank");
        instance.set_mesh(&mesh);
        instance.set_position(Vector3::new(0.0, 0.35, 0.0));
        self.base_mut().add_child(&instance);
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
        let radius = body.bind().radius();
        let mut rng = SeededRng::derive_indexed(crate::seed::singleton::run_seed(), "oxygen_tanks", self.body_index as u64);
        for (i, dir) in procgen::scatter_on_sphere(&mut rng, data.tanks_per_body as usize).into_iter().enumerate() {
            let mut tank = OxygenTankPickup::new_alloc();
            tank.set_name(&format!("OxygenTank{i}"));
            tank.set_transform(Transform3D::new(basis_with_up(dir), dir * radius));
            self.base_mut().add_child(&tank);
        }
    }
}
