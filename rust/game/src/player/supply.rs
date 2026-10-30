//! `SupplyPoint`: refills suit oxygen and jetpack fuel nearby. A PLACEHOLDER
//! stand-in for the ship's supply until the starter lander exists (Phase 4).
//! Builds its own placeholder mesh so it is visible in test scenes.

use godot::classes::{CylinderMesh, INode3D, MeshInstance3D, Node3D, OmniLight3D, StandardMaterial3D};
use godot::prelude::*;

pub const SUPPLY_GROUP: &str = "supply_points";

#[derive(GodotClass)]
#[class(base = Node3D, init)]
pub struct SupplyPoint {
    base: Base<Node3D>,
}

#[godot_api]
impl INode3D for SupplyPoint {
    fn ready(&mut self) {
        self.base_mut().add_to_group(SUPPLY_GROUP);
        let mut mesh = CylinderMesh::new_gd();
        mesh.set_top_radius(0.6);
        mesh.set_bottom_radius(0.9);
        mesh.set_height(2.0);
        let mut material = StandardMaterial3D::new_gd();
        material.set_albedo(Color::from_rgb(0.25, 0.55, 0.85));
        mesh.set_material(&material);
        let mut instance = MeshInstance3D::new_alloc();
        instance.set_name("PLACEHOLDER_ShipSupplyStandIn");
        instance.set_mesh(&mesh);
        instance.set_position(Vector3::new(0.0, 1.0, 0.0));
        self.base_mut().add_child(&instance);
        let mut light = OmniLight3D::new_alloc();
        light.set_color(Color::from_rgb(0.4, 0.75, 1.0));
        light.set_param(godot::classes::light_3d::Param::RANGE, 6.0);
        light.set_position(Vector3::new(0.0, 2.4, 0.0));
        self.base_mut().add_child(&light);
    }
}
