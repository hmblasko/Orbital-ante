//! Hoodoos: thin rock spires of soft banded stone under a harder, wider
//! caprock (like Bryce Canyon). Built from stacked, dented rock lumps so
//! the silhouette pinches and bulges instead of reading as a cylinder
//! (section 11: no plain primitives).

use godot::builtin::{Color, Vector3};

use super::noise::fbm3;
use super::terrain::MeshData;
use crate::seed::SeededRng;

/// Rings up the column and segments around it.
const RINGS: usize = 44;
const SEGMENTS: usize = 22;

fn smoothstep(e0: f32, e1: f32, x: f32) -> f32 {
    let t = ((x - e0) / (e1 - e0)).clamp(0.0, 1.0);
    t * t * (3.0 - 2.0 * t)
}

/// One hoodoo about `height` metres tall, with per-vertex colours:
/// alternating `band_a`/`band_b` layers and a darker `cap` on top.
///
/// Built as one continuous eroded column (a lathe profile pushed around by
/// noise): a flared base, soft bulges and necks where weaker layers wore
/// back, stepped ledges at the layer lines, then a thin neck under a wider,
/// rounded caprock. Reads as one rock, never as stacked balls.
pub fn hoodoo_mesh(
    seed: u64,
    height: f32,
    band_a: Color,
    band_b: Color,
    cap: Color,
) -> (MeshData, Vec<Color>) {
    let mut rng = SeededRng::derive(seed, "hoodoo");
    let noise_seed = rng.next_u64() as u32;
    let base_r = height * rng.range_f32(0.11, 0.14);
    let bulges = rng.range_f32(2.0, 3.2);
    let bulge_phase = rng.range_f32(0.0, std::f32::consts::TAU);
    let layers = 4.0 + rng.below(3) as f32;
    let neck_at = rng.range_f32(0.84, 0.88);
    let cap_r = base_r * rng.range_f32(1.35, 1.7);
    let cap_center = neck_at + 0.08;
    // The caprock is a flat slab sitting slightly tilted, not a dome.
    let cap_tilt = Vector3::new(rng.range_f32(-0.12, 0.12), 0.0, rng.range_f32(-0.12, 0.12));
    let lean = Vector3::new(rng.range_f32(-0.04, 0.04), 0.0, rng.range_f32(-0.04, 0.04)) * height;

    // Radius of the column at height fraction t, before noise.
    let profile = |t: f32| -> f32 {
        let taper = 1.25 - 0.5 * t;
        let flare = 0.55 * (-t * 9.0).exp();
        let wave = 1.0 + 0.16 * (t * bulges * std::f32::consts::TAU + bulge_phase).sin();
        let neck = 1.0 - 0.25 * smoothstep(neck_at - 0.12, neck_at, t);
        let shaft = base_r * (taper + flare) * wave * neck;
        // Caprock: a squashed ellipse sitting on the neck, wider than the shaft.
        let below = 0.035;
        let above = 1.0 - cap_center;
        let h = if t < cap_center { below } else { above };
        let e = 1.0 - ((t - cap_center) / h).powi(2);
        let cap_shape = if e > 0.0 { cap_r * e.powf(0.18) } else { 0.0 };
        shaft.max(cap_shape)
    };

    let mut mesh = MeshData::default();
    let mut colors = Vec::new();
    let axis = |t: f32| lean * t * t;
    for i in 0..RINGS {
        // Slightly below ground at the bottom so it never floats.
        let t = i as f32 / (RINGS - 1) as f32;
        let y = -0.04 * height + t * 1.04 * height;
        let ring_r = profile(t);
        // Ledges: harder layers stick out a little at each layer line.
        let layer = t * layers;
        let ledge = 1.0 + 0.07 * smoothstep(0.75, 0.95, layer.fract()) * (t < neck_at) as i32 as f32;
        let is_cap_ring = t > neck_at;
        for j in 0..SEGMENTS {
            let a = j as f32 / SEGMENTS as f32 * std::f32::consts::TAU;
            let (sa, ca) = a.sin_cos();
            // Vertical flutes: noise stretched up the column, like rain-cut runnels.
            let n = fbm3([ca * 1.8, y / height * 2.6, sa * 1.8], 4, 0.55, noise_seed);
            let r = ring_r * ledge * (1.0 + 0.24 * n);
            let mut p = Vector3::new(ca * r, y, sa * r) + axis(t);
            if is_cap_ring {
                p.y += (cap_tilt.x * p.x + cap_tilt.z * p.z) * smoothstep(neck_at, cap_center, t);
            }
            mesh.positions.push(p);
            mesh.samples.push(Default::default());
            let is_cap = t > neck_at + 0.01;
            let base = if is_cap {
                cap
            } else if (layer + n * 0.3).floor() as i32 % 2 == 0 {
                band_a
            } else {
                band_b
            };
            // Darker toward the foot (shade and dust), lighter on the layer lips.
            let shade = (0.82 + 0.18 * t.min(0.6) / 0.6) * (1.0 + 0.06 * (ledge - 1.0) / 0.07);
            colors.push(Color::from_rgb(base.r * shade, base.g * shade, base.b * shade));
        }
    }
    let ring = |i: usize, j: usize| (i * SEGMENTS + j % SEGMENTS) as i32;
    for i in 0..RINGS - 1 {
        for j in 0..SEGMENTS {
            mesh.indices.extend([ring(i, j), ring(i + 1, j), ring(i, j + 1)]);
            mesh.indices.extend([ring(i, j + 1), ring(i + 1, j), ring(i + 1, j + 1)]);
        }
    }
    // Close the top and bottom with a centre vertex each.
    for (i, y, color) in [(RINGS - 1, height, cap), (0, -0.04 * height, band_a)] {
        let center = mesh.positions.len() as i32;
        let t = y / height;
        mesh.positions.push(Vector3::new(0.0, y, 0.0) + axis(t.max(0.0)));
        mesh.samples.push(Default::default());
        colors.push(color);
        for j in 0..SEGMENTS {
            mesh.indices.extend([center, ring(i, j), ring(i, j + 1)]);
        }
    }
    orient_and_shade(&mut mesh, height);
    (mesh, colors)
}

/// Wind every triangle to face away from the column's axis (front faces
/// are clockwise from outside), then average smooth normals.
fn orient_and_shade(mesh: &mut MeshData, height: f32) {
    let p = &mesh.positions;
    let mut normals = vec![Vector3::ZERO; p.len()];
    for t in mesh.indices.chunks_exact_mut(3) {
        let (a, b, c) = (p[t[0] as usize], p[t[1] as usize], p[t[2] as usize]);
        let mid = (a + b + c) / 3.0;
        let axis_y = mid.y.clamp(0.15 * height, 0.85 * height);
        let outward = mid - Vector3::new(0.0, axis_y, 0.0);
        let mut n = (c - a).cross(b - a);
        if n.dot(outward) < 0.0 {
            t.swap(1, 2);
            n = -n;
        }
        for &i in t.iter() {
            normals[i as usize] += n;
        }
    }
    mesh.normals = normals
        .into_iter()
        .map(|n| if n.length_squared() > 0.0 { n.normalized() } else { Vector3::UP })
        .collect();
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
        // The caprock is wider than the neck under it.
        let width_at = |lo: f32, hi: f32| {
            a.positions
                .iter()
                .filter(|p| p.y > lo && p.y < hi)
                .map(|p| Vector3::new(p.x, 0.0, p.z).length())
                .fold(0.0, f32::max)
        };
        assert!(width_at(6.9, 7.6) > width_at(5.9, 6.2), "cap overhangs its neck");
    }
}
