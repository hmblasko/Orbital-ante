//! Body surface shape: rolling hills, ridges and craters on a sphere
//! (CLAUDE.md section 4: "hilly and fun to drive, never flat plains;
//! varied craters, more on moons").
//!
//! The height function is pure and deterministic, so the mesh, the
//! collision and the test scripts all agree on where the ground is.
//! Phase 5 replaces this with streamed, chunked terrain and the preset
//! feature sets; this is the slice-scope version.

use godot::builtin::Vector3;
use serde::Deserialize;

use super::landforms::{LandHints, LandformData, Landforms};
use super::noise;
use crate::seed::SeededRng;

#[derive(Debug, Clone, Deserialize)]
pub struct TerrainData {
    /// Fixed per body (the body roster is fixed every run, section 4).
    pub seed: u64,
    /// Grid cells along each edge of each of the 6 cube faces.
    pub subdivisions: u32,
    pub hill_height_m: f32,
    /// Hill features per radian of arc.
    pub hill_frequency: f32,
    pub hill_octaves: u32,
    pub ridge_height_m: f32,
    pub ridge_frequency: f32,
    pub crater_count: u32,
    /// Crater radius range, in metres along the surface.
    pub crater_radius_m: (f32, f32),
    /// Bowl depth as a fraction of crater radius.
    pub crater_depth_ratio: f32,
    /// Rim height as a fraction of crater depth.
    pub crater_rim_ratio: f32,
    /// Mountains, mesas, canyons, water and the rest (see `landforms`).
    #[serde(default)]
    pub landforms: LandformData,
}

#[derive(Debug, Clone)]
struct Crater {
    center: Vector3,
    /// Angular radius in radians.
    angle: f32,
    depth: f32,
}

/// Painting hints carried per vertex to the terrain shader.
#[derive(Debug, Clone, Copy, PartialEq, Default)]
pub struct SurfaceSample {
    /// Height above the nominal radius, in metres.
    pub height: f32,
    /// 1.0 on a crater floor, fading to 0.0 at its rim.
    pub crater_floor: f32,
    /// 1.0 on a crater rim crest.
    pub crater_rim: f32,
    pub land: LandHints,
}

#[derive(Debug, Clone)]
pub struct TerrainShape {
    data: TerrainData,
    radius: f32,
    craters: Vec<Crater>,
    landforms: Landforms,
    seed: u32,
}

impl TerrainShape {
    pub fn new(data: &TerrainData, radius: f32) -> Self {
        let mut rng = SeededRng::from_u64(data.seed);
        let craters = (0..data.crater_count)
            .map(|_| {
                let y = rng.range_f32(-1.0, 1.0);
                let theta = rng.range_f32(0.0, std::f32::consts::TAU);
                let ring = (1.0 - y * y).sqrt();
                // Small craters are more common than big ones.
                let t = rng.next_f32().powi(2);
                let r =
                    data.crater_radius_m.0 + (data.crater_radius_m.1 - data.crater_radius_m.0) * t;
                Crater {
                    center: Vector3::new(ring * theta.cos(), y, ring * theta.sin()),
                    angle: r / radius,
                    depth: r * data.crater_depth_ratio,
                }
            })
            .collect();
        let landforms = Landforms::new(&data.landforms, radius, data.seed);
        Self {
            data: data.clone(),
            radius,
            craters,
            landforms,
            seed: (data.seed ^ (data.seed >> 32)) as u32,
        }
    }

    pub fn radius(&self) -> f32 {
        self.radius
    }

    /// Lowest the surface can possibly be (for out-of-world checks).
    pub fn min_radius(&self) -> f32 {
        let deepest = self.craters.iter().map(|c| c.depth).fold(0.0, f32::max);
        let base = -self.data.hill_height_m - deepest;
        self.radius + base.min(self.landforms.lowest())
    }

    pub fn landforms(&self) -> &Landforms {
        &self.landforms
    }

    pub fn water_level(&self) -> Option<f32> {
        self.landforms.water_level
    }

    pub fn sample(&self, dir: Vector3) -> SurfaceSample {
        let d = dir.normalized();
        let p = [d.x, d.y, d.z];
        let f = self.data.hill_frequency;
        let hills = noise::fbm3(
            [p[0] * f, p[1] * f, p[2] * f],
            self.data.hill_octaves,
            0.5,
            self.seed,
        ) * self.data.hill_height_m;
        let rf = self.data.ridge_frequency;
        let ridges = (noise::ridged3(
            [p[0] * rf + 11.0, p[1] * rf, p[2] * rf - 7.0],
            3,
            0.5,
            self.seed.wrapping_add(17),
        ) - 0.45)
            .max(0.0)
            * self.data.ridge_height_m;
        let mut height = hills + ridges;
        let (mut floor, mut rim) = (0.0f32, 0.0f32);
        for c in &self.craters {
            let a = d.angle_to(c.center);
            let x = a / c.angle;
            if x >= 1.6 {
                continue;
            }
            let rim_h = c.depth * self.data.crater_rim_ratio;
            let delta = if x < 1.0 {
                // Bowl: a smooth dish that meets the rim at x = 1.
                let bowl = -c.depth * (1.0 - x * x) + rim_h * x.powi(6);
                floor = floor.max(smoothstep(0.85, 0.2, x));
                bowl
            } else {
                // Rim falls off outside the crater.
                let t = (x - 1.0) / 0.6;
                rim_h * (1.0 - smoothstep(0.0, 1.0, t))
            };
            rim = rim.max(1.0 - ((x - 1.0).abs() / 0.18).min(1.0));
            height += delta;
        }
        let (height, land) = self.landforms.apply(d, height, self.radius);
        SurfaceSample {
            height,
            crater_floor: floor,
            crater_rim: rim,
            land,
        }
    }

    pub fn surface_radius(&self, dir: Vector3) -> f32 {
        self.radius + self.sample(dir).height
    }

    /// Build the surface as a subdivided cube projected onto the sphere
    /// (more even triangles than a UV sphere, no pinched poles).
    pub fn build_mesh(&self) -> MeshData {
        grid_sphere(self.data.subdivisions, |dir| {
            let s = self.sample(dir);
            (self.radius + s.height, s)
        })
    }
}

/// A plain sphere on the same cube grid (used for water).
pub fn sphere_mesh(radius: f32, subdivisions: u32) -> MeshData {
    grid_sphere(subdivisions, |_| (radius, SurfaceSample::default()))
}

/// Subdivided cube projected onto a sphere, with the radius and painting
/// hints for each direction supplied by `at`. Triangles are emitted face by
/// face, row by row, two per cell (`split_tiles` relies on that order).
fn grid_sphere(subdivisions: u32, at: impl Fn(Vector3) -> (f32, SurfaceSample)) -> MeshData {
    let n = subdivisions.max(2) as usize;
    let faces: [(Vector3, Vector3, Vector3); 6] = [
        (Vector3::RIGHT, Vector3::UP, Vector3::BACK),
        (Vector3::LEFT, Vector3::UP, Vector3::FORWARD),
        (Vector3::UP, Vector3::BACK, Vector3::RIGHT),
        (Vector3::DOWN, Vector3::FORWARD, Vector3::RIGHT),
        (Vector3::BACK, Vector3::UP, Vector3::LEFT),
        (Vector3::FORWARD, Vector3::UP, Vector3::RIGHT),
    ];
    let mut mesh = MeshData::default();
    for (normal, axis_a, axis_b) in faces {
        let base = mesh.positions.len() as i32;
        for j in 0..=n {
            for i in 0..=n {
                let u = i as f32 / n as f32 * 2.0 - 1.0;
                let v = j as f32 / n as f32 * 2.0 - 1.0;
                let dir = cube_to_sphere(normal + axis_a * v + axis_b * u);
                let (r, s) = at(dir);
                mesh.positions.push(dir * r);
                mesh.samples.push(s);
            }
        }
        let row = n as i32 + 1;
        for j in 0..n as i32 {
            for i in 0..n as i32 {
                let a = base + j * row + i;
                let (b, c, d) = (a + 1, a + row, a + row + 1);
                // Wind so the outward side is the front face.
                mesh.indices.extend_from_slice(&[a, b, c, b, d, c]);
            }
        }
    }
    mesh.fix_winding();
    mesh.compute_normals();
    mesh
}

/// Positions, smooth normals, painting hints and triangle indices.
#[derive(Debug, Clone, Default)]
pub struct MeshData {
    pub positions: Vec<Vector3>,
    pub normals: Vec<Vector3>,
    pub samples: Vec<SurfaceSample>,
    pub indices: Vec<i32>,
}

impl MeshData {
    /// Godot treats clockwise triangles (seen from outside) as front faces.
    /// Flip any triangle that faces inward.
    fn fix_winding(&mut self) {
        for t in self.indices.chunks_exact_mut(3) {
            let (a, b, c) = (
                self.positions[t[0] as usize],
                self.positions[t[1] as usize],
                self.positions[t[2] as usize],
            );
            let n = (c - a).cross(b - a);
            if n.dot(a + b + c) < 0.0 {
                t.swap(1, 2);
            }
        }
    }

    fn compute_normals(&mut self) {
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
        // Cube-face seams share positions but not vertices; average across them.
        let mut by_pos: std::collections::HashMap<[i32; 3], Vector3> =
            std::collections::HashMap::new();
        let key = |p: Vector3| {
            [
                (p.x * 1000.0).round() as i32,
                (p.y * 1000.0).round() as i32,
                (p.z * 1000.0).round() as i32,
            ]
        };
        for (p, n) in self.positions.iter().zip(&normals) {
            *by_pos.entry(key(*p)).or_insert(Vector3::ZERO) += *n;
        }
        self.normals = self
            .positions
            .iter()
            .map(|p| by_pos[&key(*p)].normalized())
            .collect();
    }

    pub fn triangle_count(&self) -> usize {
        self.indices.len() / 3
    }

    /// Split a `grid_sphere` mesh into `tiles` x `tiles` pieces per cube
    /// face, so the renderer can skip the pieces that are out of view.
    pub fn split_tiles(&self, subdivisions: u32, tiles: u32) -> Vec<MeshData> {
        let n = subdivisions.max(2) as usize;
        let tiles = tiles.clamp(1, n as u32) as usize;
        let mut out: Vec<(MeshData, std::collections::HashMap<i32, i32>)> =
            (0..6 * tiles * tiles).map(|_| Default::default()).collect();
        for (k, tri) in self.indices.chunks_exact(3).enumerate() {
            let cell = k / 2;
            let face = cell / (n * n);
            let (j, i) = ((cell % (n * n)) / n, cell % n);
            let tile = face * tiles * tiles + (j * tiles / n) * tiles + i * tiles / n;
            let (mesh, remap) = &mut out[tile];
            for &v in tri {
                let idx = *remap.entry(v).or_insert_with(|| {
                    mesh.positions.push(self.positions[v as usize]);
                    mesh.normals.push(self.normals[v as usize]);
                    mesh.samples.push(self.samples[v as usize]);
                    mesh.positions.len() as i32 - 1
                });
                mesh.indices.push(idx);
            }
        }
        out.into_iter().map(|(m, _)| m).collect()
    }
}

/// Map a point on the cube [-1,1]^3 to the unit sphere with less
/// stretching than plain normalisation.
fn cube_to_sphere(p: Vector3) -> Vector3 {
    let (x2, y2, z2) = (p.x * p.x, p.y * p.y, p.z * p.z);
    Vector3::new(
        p.x * (1.0 - y2 / 2.0 - z2 / 2.0 + y2 * z2 / 3.0).sqrt(),
        p.y * (1.0 - z2 / 2.0 - x2 / 2.0 + z2 * x2 / 3.0).sqrt(),
        p.z * (1.0 - x2 / 2.0 - y2 / 2.0 + x2 * y2 / 3.0).sqrt(),
    )
    .normalized()
}

fn smoothstep(e0: f32, e1: f32, x: f32) -> f32 {
    let t = ((x - e0) / (e1 - e0)).clamp(0.0, 1.0);
    t * t * (3.0 - 2.0 * t)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn data() -> TerrainData {
        TerrainData {
            seed: 42,
            subdivisions: 16,
            hill_height_m: 1.5,
            hill_frequency: 3.0,
            hill_octaves: 4,
            ridge_height_m: 1.0,
            ridge_frequency: 2.0,
            crater_count: 12,
            crater_radius_m: (3.0, 12.0),
            crater_depth_ratio: 0.2,
            crater_rim_ratio: 0.35,
            landforms: Default::default(),
        }
    }

    #[test]
    fn determinism_same_seed_same_surface() {
        let a = TerrainShape::new(&data(), 60.0);
        let b = TerrainShape::new(&data(), 60.0);
        let dir = Vector3::new(0.3, 0.8, -0.2);
        assert_eq!(a.sample(dir), b.sample(dir));
    }

    #[test]
    fn mesh_matches_height_function_and_is_closed() {
        let shape = TerrainShape::new(&data(), 60.0);
        let mesh = shape.build_mesh();
        assert_eq!(mesh.triangle_count(), 6 * 16 * 16 * 2);
        for p in mesh.positions.iter().step_by(37) {
            assert!((p.length() - shape.surface_radius(*p)).abs() < 1e-3);
            assert!(p.length() >= shape.min_radius() - 1e-3);
        }
        // Every triangle faces outward.
        for t in mesh.indices.chunks_exact(3) {
            let (a, b, c) = (
                mesh.positions[t[0] as usize],
                mesh.positions[t[1] as usize],
                mesh.positions[t[2] as usize],
            );
            assert!((c - a).cross(b - a).dot(a + b + c) >= 0.0);
        }
        assert!(mesh.normals.iter().all(|n| (n.length() - 1.0).abs() < 1e-3));
    }

    #[test]
    fn tiles_cover_every_triangle_once() {
        let shape = TerrainShape::new(&data(), 60.0);
        let mesh = shape.build_mesh();
        let tiles = mesh.split_tiles(16, 4);
        assert_eq!(tiles.len(), 6 * 16);
        assert_eq!(
            tiles.iter().map(|t| t.triangle_count()).sum::<usize>(),
            mesh.triangle_count()
        );
        assert!(tiles.iter().all(|t| t.triangle_count() == 2 * 4 * 4));
    }

    #[test]
    fn craters_dip_below_the_surroundings() {
        let shape = TerrainShape::new(&data(), 60.0);
        let c = shape.craters[0].clone();
        let center = shape.sample(c.center).height;
        let outside = shape
            .sample(
                (c.center + c.center.cross(Vector3::UP).normalized() * c.angle * 3.0).normalized(),
            )
            .height;
        assert!(
            center < outside,
            "crater floor {center} vs outside {outside}"
        );
        assert!(shape.sample(c.center).crater_floor > 0.9);
    }
}
