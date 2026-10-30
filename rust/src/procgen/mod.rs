//! Placement of surface features (CLAUDE.md section 4).
//!
//! Phase 0 only has an even, seed-free spread used for test-scene markers.
//! Seeded placement arrives with the RunSeed system in Phase 1.

use godot::builtin::Vector3;

/// `count` unit vectors spread evenly over a sphere (Fibonacci lattice).
pub fn fibonacci_sphere(count: usize) -> Vec<Vector3> {
    let golden_angle = std::f32::consts::PI * (3.0 - 5f32.sqrt());
    (0..count)
        .map(|i| {
            let y = 1.0 - 2.0 * (i as f32 + 0.5) / count as f32;
            let ring = (1.0 - y * y).sqrt();
            let theta = golden_angle * i as f32;
            Vector3::new(ring * theta.cos(), y, ring * theta.sin())
        })
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn points_are_unit_length_and_counted() {
        let points = fibonacci_sphere(24);
        assert_eq!(points.len(), 24);
        for p in points {
            assert!((p.length() - 1.0).abs() < 1e-5);
        }
    }

    #[test]
    fn same_count_gives_same_points() {
        assert_eq!(fibonacci_sphere(10), fibonacci_sphere(10));
    }
}
