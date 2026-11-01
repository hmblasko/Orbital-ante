//! Enemy camps (vertical slice): a seeded spot on a body, a short walk from
//! the landing pole, where a handful of faction soldiers patrol. Section 4:
//! enemy bases are seeded; the full base pass (buildings, sizes, base
//! clears) is Phase 8.
//!
//! Serves pillar 3 (every run counts: the camp moves with the seed) and fun
//! rule 1 (learning: ante 1's first fight is a small, readable one).

use godot::prelude::*;

use super::enemy::Enemy;
use crate::procgen::terrain::TerrainShape;
use crate::seed::SeededRng;

/// The enemies of the camps that `enemies.ron` puts on this body, placed in
/// the body's local space (the body adds them as children).
pub fn camp_enemies(body_id: &str, shape: &TerrainShape, run_seed: u64) -> Vec<Gd<Enemy>> {
    let mut out = Vec::new();
    let data = crate::combat::load_enemies();
    for (ci, camp) in data.camps.iter().enumerate() {
        if camp.body != body_id {
            continue;
        }
        let mut rng = SeededRng::derive_indexed(run_seed, &format!("camp_{body_id}"), ci as u64);
        // A direction this many degrees from the landing pole (+Y).
        let tilt = rng.range_f32(camp.angle_deg.0, camp.angle_deg.1).to_radians();
        let around = rng.range_f32(0.0, std::f32::consts::TAU);
        let centre = Vector3::new(tilt.sin() * around.cos(), tilt.cos(), tilt.sin() * around.sin()).normalized();
        let side_a = centre.cross(Vector3::RIGHT).normalized();
        let side_b = centre.cross(side_a).normalized();
        let r = shape.radius().max(1.0);
        for (i, id) in camp.enemies.iter().enumerate() {
            let a = i as f32 / camp.enemies.len().max(1) as f32 * std::f32::consts::TAU + rng.range_f32(-0.4, 0.4);
            let dist = rng.range_f32(0.4, 1.0) * camp.spread_m;
            let dir = (centre * r + (side_a * a.cos() + side_b * a.sin()) * dist).normalized();
            let ground = shape.surface_radius(dir);
            let mut e = Enemy::new_alloc();
            e.set_name(&format!("Enemy_{id}_{ci}_{i}"));
            e.bind_mut().set_id(id);
            let up = dir;
            let fwd = (centre - dir).normalized_or_zero();
            let fwd = if fwd.length_squared() < 0.5 { side_a } else { fwd };
            let x = fwd.cross(up).normalized();
            e.set_transform(Transform3D::new(Basis::from_cols(x, up, -fwd).orthonormalized(), dir * (ground + 0.3)));
            out.push(e);
        }
    }
    out
}
