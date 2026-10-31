//! `SupplyPoint`: refills suit oxygen and jetpack fuel nearby (CLAUDE.md
//! section 6: the suit refills from the ship's supply). It has no model of its
//! own: it sits inside a ship, today the parked Apollo Lunar Module, so the
//! ship itself is what the player walks up to.

use godot::classes::{INode3D, Node3D};
use godot::prelude::*;

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
