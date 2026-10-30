//! Hoodoos: thin rock spires of soft banded stone under a harder, wider
//! caprock (like Bryce Canyon). Built from stacked, dented rock lumps so
//! the silhouette pinches and bulges instead of reading as a cylinder
//! (section 11: no plain primitives).

use godot::builtin::{Color, Vector3};

use super::rocks::rock_mesh;
use super::terrain::MeshData;
use crate::seed::SeededRng;

/// One hoodoo about `height` metres tall, with per-vertex colours:
/// alternating `band_a`/`band_b` layers and a darker `cap` on top.
pub fn hoodoo_mesh(
    seed: u64,
    height: f32,
    band_a: Color,
    band_b: Color,
    cap: Color,
) -> (MeshData, Vec<Color>) {
    let mut rng = SeededRng::derive(seed, "hoodoo");
    let segments = 5 + rng.below(3) as usize;
    let base_r = height * rng.range_f32(0.11, 0.14);
    let seg_h = height * 0.86 / segments as f32;
    let mut mesh = MeshData::default();
    let mut colors = Vec::new();
    let mut y = 0.0;
    for i in 0..=segments {
        let is_cap = i == segments;
        // Wide base tapering up, with pinched necks between bulges.
        let t = i as f32 / segments as f32;
        let (r, h) = if is_cap {
            (base_r * rng.range_f32(1.3, 1.6), height * 0.13)
        } else {
            (base_r * (1.0 - t * 0.5) * rng.range_f32(0.88, 1.12), seg_h)
        };
        let lump = rock_mesh(
            rng.next_u64() as u32,
            2,
            if is_cap { 0.2 } else { 0.1 },
            1.0,
        );
        // Tall, overlapping lumps merge into one eroded column with soft ribs.
        let scale = Vector3::new(
            r,
            h * if is_cap { 0.5 } else { 0.9 },
            r * rng.range_f32(0.85, 1.1),
        );
        let center_y = y + h * if is_cap { 0.3 } else { 0.5 };
        let lean =
            Vector3::new(rng.range_f32(-0.03, 0.03), 0.0, rng.range_f32(-0.03, 0.03)) * height * t;
        let base = mesh.positions.len() as i32;
        let color = if is_cap {
            cap
        } else if i % 2 == 0 {
            band_a
        } else {
            band_b
        };
        for (p, n) in lump.positions.iter().zip(&lump.normals) {
            mesh.positions
                .push(Vector3::new(p.x * scale.x, p.y * scale.y + center_y, p.z * scale.z) + lean);
            // Non-uniform scale: normals scale by the inverse.
            mesh.normals
                .push(Vector3::new(n.x / scale.x, n.y / scale.y, n.z / scale.z).normalized());
            mesh.samples.push(Default::default());
            // Soft vertical gradient inside each band so layers blend, not stripe.
            let shade = 0.88 + 0.12 * (p.y * 0.5 + 0.5);
            colors.push(Color::from_rgb(
                color.r * shade,
                color.g * shade,
                color.b * shade,
            ));
        }
        mesh.indices.extend(lump.indices.iter().map(|i| i + base));
        y += h;
    }
    (mesh, colors)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn determinism_hoodoos_are_seeded_and_about_the_right_height() {
        let c = Color::from_rgb(0.8, 0.5, 0.3);
        let (a, ca) = hoodoo_mesh(3, 8.0, c, c, c);
        let (b, _) = hoodoo_mesh(3, 8.0, c, c, c);
        assert_eq!(a.positions, b.positions);
        assert_eq!(a.positions.len(), ca.len());
        let top = a.positions.iter().map(|p| p.y).fold(f32::MIN, f32::max);
        assert!(top > 5.5 && top < 10.0, "top {top}");
        assert!(a.normals.iter().all(|n| (n.length() - 1.0).abs() < 1e-3));
    }
}
