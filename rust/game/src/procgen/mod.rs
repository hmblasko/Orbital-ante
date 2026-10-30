//! Placement of surface features (CLAUDE.md section 4).
//!
//! Even spreads for test markers, and seeded scatter for pickups.

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

/// `count` random unit vectors, uniform over the sphere, from a seeded stream.
pub fn scatter_on_sphere(rng: &mut crate::seed::SeededRng, count: usize) -> Vec<Vector3> {
    (0..count)
        .map(|_| {
            let y = rng.range_f32(-1.0, 1.0);
            let theta = rng.range_f32(0.0, std::f32::consts::TAU);
            let ring = (1.0 - y * y).sqrt();
            Vector3::new(ring * theta.cos(), y, ring * theta.sin())
        })
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::seed::SeededRng;

    /// Determinism: same seed and body, same tank positions.
    #[test]
    fn determinism_scatter_is_seeded() {
        let a = scatter_on_sphere(&mut SeededRng::derive_indexed(9, "oxygen_tanks", 0), 4);
        let b = scatter_on_sphere(&mut SeededRng::derive_indexed(9, "oxygen_tanks", 0), 4);
        let c = scatter_on_sphere(&mut SeededRng::derive_indexed(10, "oxygen_tanks", 0), 4);
        assert_eq!(a, b);
        assert_ne!(a, c);
        assert!(a.iter().all(|p| (p.length() - 1.0).abs() < 1e-5));
    }

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
