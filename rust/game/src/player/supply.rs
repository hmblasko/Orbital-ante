//! `SupplyPoint`: refills suit oxygen and jetpack fuel nearby. A PLACEHOLDER
//! stand-in for the ship's supply until the starter lander exists (Phase 4).
//! Builds its own placeholder mesh so it is visible in test scenes.

use godot::classes::light_3d::Param;
use godot::classes::{BoxMesh, CylinderMesh, INode3D, Node3D, OmniLight3D, SphereMesh, TorusMesh};
use godot::prelude::*;

use crate::items::look::{PaintSpec, at, paint, part};
use crate::orbits::CelestialBody;
use crate::orbits::body::basis_with_up;

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
        self.build_model();
        // The parent body builds its terrain after its children are ready.
        self.base_mut().call_deferred("snap_to_ground", &[]);
    }
}

#[godot_api]
impl SupplyPoint {
    /// Stand on the terrain under this point (it was placed by hand on a
    /// nominal-radius sphere in the scene).
    #[func]
    fn snap_to_ground(&mut self) {
        let Some(body) = self.base().get_parent().and_then(|p| p.try_cast::<CelestialBody>().ok()) else { return };
        let center = body.get_global_position();
        let dir = (self.base().get_global_position() - center).normalized();
        let ground = body.bind().surface_radius_toward(center + dir);
        // Sink the base a little so it never floats over a dip.
        let origin = center + dir * (ground - 0.15);
        self.base_mut().set_global_transform(Transform3D::new(basis_with_up(dir), origin));
    }
}

impl SupplyPoint {
    /// PLACEHOLDER model: a squat, rounded supply post with hazard-orange
    /// trim, a glowing teal ring (the "refills here" read) and a hose coil.
    fn build_model(&mut self) {
        let mut root = Node3D::new_alloc();
        root.set_name("PLACEHOLDER_ShipSupplyStandIn");
        let hull = paint(&PaintSpec::metal(Color::from_rgb(0.86, 0.86, 0.82), Color::from_rgb(0.55, 0.56, 0.58)));
        let trim = paint(&PaintSpec::matte(Color::from_rgb(0.98, 0.52, 0.18), Color::from_rgb(0.70, 0.30, 0.10)));
        let dark = paint(&PaintSpec::matte(Color::from_rgb(0.20, 0.22, 0.26), Color::from_rgb(0.10, 0.11, 0.14)));
        let glow = paint(&PaintSpec::glow(Color::from_rgb(0.35, 0.95, 1.0), 3.0));

        let cyl = |top: f32, bottom: f32, h: f32| {
            let mut m = CylinderMesh::new_gd();
            m.set_top_radius(top);
            m.set_bottom_radius(bottom);
            m.set_height(h);
            m.set_radial_segments(32);
            m
        };
        let torus = |inner: f32, outer: f32| {
            let mut m = TorusMesh::new_gd();
            m.set_inner_radius(inner);
            m.set_outer_radius(outer);
            m.set_rings(32);
            m.set_ring_segments(12);
            m
        };
        part(&mut root, "Foot", cyl(0.95, 1.1, 0.25), &dark, at(0.0, 0.12, 0.0));
        part(&mut root, "FootTrim", torus(0.92, 1.06), &trim, at(0.0, 0.26, 0.0));
        part(&mut root, "Column", cyl(0.42, 0.62, 1.3), &hull, at(0.0, 0.9, 0.0));
        part(&mut root, "Collar", torus(0.38, 0.5), &dark, at(0.0, 1.55, 0.0));
        let mut dome = SphereMesh::new_gd();
        dome.set_radius(0.45);
        dome.set_height(0.6);
        dome.set_is_hemisphere(true);
        part(&mut root, "Cap", dome, &hull, at(0.0, 1.58, 0.0));
        part(&mut root, "GlowRing", torus(0.62, 0.7), &glow, at(0.0, 2.25, 0.0));
        part(&mut root, "HoseCoil", torus(0.08, 0.2), &dark, Transform3D::new(Basis::from_axis_angle(Vector3::BACK, std::f32::consts::FRAC_PI_2), Vector3::new(0.55, 0.85, 0.0)));
        let mut panel = BoxMesh::new_gd();
        panel.set_size(Vector3::new(0.32, 0.22, 0.04));
        part(&mut root, "StatusLight", panel, &glow, at(0.0, 1.05, 0.56));
        self.base_mut().add_child(&root);

        let mut light = OmniLight3D::new_alloc();
        light.set_color(Color::from_rgb(0.4, 0.9, 1.0));
        light.set_param(Param::RANGE, 7.0);
        light.set_param(Param::ENERGY, 1.2);
        light.set_position(Vector3::new(0.0, 2.3, 0.0));
        self.base_mut().add_child(&light);
    }
}
