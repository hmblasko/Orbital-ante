//! Procedural boulders: a subdivided icosahedron, squashed and dented by
//! noise so no two read as the same primitive (section 11: no plain
//! spheres or cubes). Each rock gets its own seed.

use godot::builtin::Vector3;

use super::noise;
use super::terrain::{MeshData, SurfaceSample};

/// Build one rock of roughly unit size. `squash` flattens it vertically.
pub fn rock_mesh(seed: u32, subdivisions: u32, roughness: f32, squash: f32) -> MeshData {
    let (mut positions, mut indices) = icosahedron();
    for _ in 0..subdivisions {
        subdivide(&mut positions, &mut indices);
    }
    let mut mesh = MeshData::default();
    for p in &positions {
        let d = p.normalized();
        // Big lumps plus flat facets (the ridged term), so it reads as chipped stone.
        let lump = noise::fbm3([d.x * 1.3, d.y * 1.3, d.z * 1.3], 3, 0.5, seed);
        let facet = noise::ridged3(
            [d.x * 2.2 + 5.0, d.y * 2.2, d.z * 2.2],
            2,
            0.5,
            seed.wrapping_add(9),
        );
        let r = 1.0 + lump * roughness - facet * roughness * 0.6;
        let mut v = d * r.max(0.35);
        v.y *= squash;
        mesh.positions.push(v);
        mesh.samples.push(SurfaceSample {
            height: v.y,
            crater_rim: facet,
            ..Default::default()
        });
    }
    mesh.indices = indices;
    mesh.recompute_flat_ish_normals();
    mesh
}

fn icosahedron() -> (Vec<Vector3>, Vec<i32>) {
    let t = (1.0 + 5f32.sqrt()) / 2.0;
    let positions = [
        (-1.0, t, 0.0),
        (1.0, t, 0.0),
        (-1.0, -t, 0.0),
        (1.0, -t, 0.0),
        (0.0, -1.0, t),
        (0.0, 1.0, t),
        (0.0, -1.0, -t),
        (0.0, 1.0, -t),
        (t, 0.0, -1.0),
        (t, 0.0, 1.0),
        (-t, 0.0, -1.0),
        (-t, 0.0, 1.0),
    ]
    .iter()
    .map(|&(x, y, z)| Vector3::new(x, y, z).normalized())
    .collect();
    let indices = vec![
        0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7,
        1, 8, 3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9,
        8, 1,
    ];
    (positions, indices)
}

fn subdivide(positions: &mut Vec<Vector3>, indices: &mut Vec<i32>) {
    let mut cache = std::collections::HashMap::new();
    let mut mid = |a: i32, b: i32, positions: &mut Vec<Vector3>| -> i32 {
        let key = (a.min(b), a.max(b));
        *cache.entry(key).or_insert_with(|| {
            positions.push(((positions[a as usize] + positions[b as usize]) * 0.5).normalized());
            positions.len() as i32 - 1
        })
    };
    let mut out = Vec::with_capacity(indices.len() * 4);
    for t in indices.chunks_exact(3) {
        let (a, b, c) = (t[0], t[1], t[2]);
        let ab = mid(a, b, positions);
        let bc = mid(b, c, positions);
        let ca = mid(c, a, positions);
        out.extend_from_slice(&[a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca]);
    }
    *indices = out;
}

impl MeshData {
    /// Smooth normals blended toward face normals: keeps the stone looking
    /// chiselled without hard 90-degree faceting.
    pub fn recompute_flat_ish_normals(&mut self) {
        // Outward winding first.
        for t in self.indices.chunks_exact_mut(3) {
            let (a, b, c) = (
                self.positions[t[0] as usize],
                self.positions[t[1] as usize],
                self.positions[t[2] as usize],
            );
            if (c - a).cross(b - a).dot(a + b + c) < 0.0 {
                t.swap(1, 2);
            }
        }
        let mut normals = vec![Vector3::ZERO; self.positions.len()];
        for t in self.indices.chunks_exact(3) {
            let (a, b, c) = (
                self.positions[t[0] as usize],
                self.positions[t[1] as usize],
                self.positions[t[2] as usize],
            );
            let n = (c - a).cross(b - a);
            for &i in t {
                normals[i as usize] += n;
            }
        }
        self.normals = normals.into_iter().map(|n| n.normalized()).collect();
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn determinism_rocks_are_seeded_and_closed() {
        let a = rock_mesh(5, 2, 0.3, 0.7);
        let b = rock_mesh(5, 2, 0.3, 0.7);
        let c = rock_mesh(6, 2, 0.3, 0.7);
        assert_eq!(a.positions, b.positions);
        assert_ne!(a.positions, c.positions);
        assert_eq!(a.triangle_count(), 20 * 16);
        assert!(a.normals.iter().all(|n| n.length() > 0.99));
    }
}
