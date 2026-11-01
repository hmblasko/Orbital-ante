//! Enemy camps (vertical slice): a seeded spot on a body, a short walk from
//! the landing pole, where a handful of faction soldiers patrol. Section 4:
//! enemy bases are seeded; the full base pass (buildings, sizes) is Phase 8.
//! Each camp's soldiers share a group, so the last one down or tied counts
//! as a camp clear (a small base clear, section 17.5). Camps are re-manned
//! at the start of every ante (section 2: factions retake planets).
//!
//! Serves pillar 3 (every run counts: the camp moves with the seed) and fun
//! rule 1 (learning: ante 1's first fight is a small, readable one).

use godot::prelude::*;

use super::enemy::Enemy;
use crate::procgen::terrain::TerrainShape;
use crate::seed::SeededRng;

/// Group every soldier of every camp joins (for re-manning).
pub const CAMP_GROUP: &str = "camp_soldiers";

/// How far a soldier stands from the edge of a base piece when placed.
const SOLDIER_CLEAR_M: f32 = 0.9;

/// The group name shared by one camp's soldiers.
pub fn camp_key(body_id: &str, index: usize) -> String {
    format!("camp:{body_id}:{index}")
}

/// Where camp `ci` sits this ante (a direction from the body's centre),
/// and the seeded stream that goes on to place its soldiers.
pub fn camp_centre(body_id: &str, ci: usize, camp: &crate::combat::CampData, run_seed: u64, ante: u32) -> (Vector3, SeededRng) {
    // Ante 1 keeps its original stream; later antes re-man in new spots.
    let stream = if ante <= 1 { format!("camp_{body_id}") } else { format!("camp_{body_id}_a{ante}") };
    let mut rng = SeededRng::derive_indexed(run_seed, &stream, ci as u64);
    // A direction this many degrees from the landing pole (+Y).
    let tilt = rng.range_f32(camp.angle_deg.0, camp.angle_deg.1).to_radians();
    let around = rng.range_f32(0.0, std::f32::consts::TAU);
    let centre = Vector3::new(tilt.sin() * around.cos(), tilt.cos(), tilt.sin() * around.sin()).normalized();
    (centre, rng)
}

/// The enemies of the camps that `enemies.ron` mans on this body in this
/// ante, placed in the body's local space (the body adds them as children).
pub fn camp_enemies(body_id: &str, shape: &TerrainShape, run_seed: u64, ante: u32) -> Vec<Gd<Enemy>> {
    let mut out = Vec::new();
    let data = crate::combat::load_enemies();
    for (ci, camp) in data.camps.iter().enumerate() {
        if camp.body != body_id || camp.from_ante > ante.max(1) {
            continue;
        }
        let (centre, mut rng) = camp_centre(body_id, ci, camp, run_seed, ante);
        let pieces = super::base::camp_layout(body_id, ci, camp, run_seed, ante);
        let side_a = centre.cross(Vector3::RIGHT).normalized();
        let side_b = centre.cross(side_a).normalized();
        let r = shape.radius().max(1.0);
        for (i, id) in camp.enemies.iter().enumerate() {
            // Stand clear of the base's walls, pods and crates.
            let (mut a, mut dist) = (0.0, 0.0);
            for _ in 0..16 {
                a = i as f32 / camp.enemies.len().max(1) as f32 * std::f32::consts::TAU + rng.range_f32(-0.4, 0.4);
                dist = rng.range_f32(0.4, 1.0) * camp.spread_m;
                let (x, z) = (a.cos() * dist, a.sin() * dist);
                if pieces.iter().all(|p| ((p.x - x).powi(2) + (p.z - z).powi(2)).sqrt() >= p.clear_m + SOLDIER_CLEAR_M) {
                    break;
                }
            }
            let dir = (centre * r + (side_a * a.cos() + side_b * a.sin()) * dist).normalized();
            let ground = shape.surface_radius(dir);
            let mut e = Enemy::new_alloc();
            e.set_name(&format!("Enemy_{id}_{ci}_{i}"));
            e.bind_mut().set_id(id);
            let key = camp_key(body_id, ci);
            e.set_meta("camp", &GString::from(&key).to_variant());
            e.add_to_group(&key);
            e.add_to_group(CAMP_GROUP);
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
