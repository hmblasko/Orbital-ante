//! Lava tubes: walkable caves that open from the side of a collapse pit
//! (CLAUDE.md section 4: "caves"; the Moon kit's "lava tube + skylight").
//!
//! Serves pillar 3 (every run teaches: a hidden place to find and learn)
//! and fun rule 4 (curiosity: a dark mouth in a pit wall pulls you in).
//!
//! A tube is a winding centreline that runs from the foot of a pit wall out
//! under the plain, sinking where the ground above gets thin, and ending in
//! a wider chamber. The terrain has no overhangs, so the ground's triangles
//! inside the tube are removed (`inside_cut`) and the tube's own rounded
//! rock walls (normals facing in) take over. Rubble boulders around the
//! mouth hide the seam. Everything here is pure and seeded, so the mesh,
//! the collision and the tests agree.

use godot::builtin::Vector3;
use serde::Deserialize;

use super::landforms::{Landform, LandformKind};
use super::noise;
use super::terrain::{MeshData, SurfaceSample, TerrainShape};
use crate::seed::SeededRng;

#[derive(Debug, Clone, Deserialize)]
pub struct CaveData {
    /// How many of the body's sinkholes (in placement order) open into a tube.
    pub count: u32,
    /// Tube length range, metres.
    pub length_m: (f32, f32),
    /// Half-width of the tube, metres (the ceiling is a little lower).
    pub radius_m: (f32, f32),
    /// Ceiling height as a share of the half-width.
    pub height_ratio: f32,
    /// Rock left above the ceiling, metres (the tube sinks to keep it).
    pub cover_m: f32,
    /// How much wider the end chamber is than the tube.
    pub chamber_scale: f32,
    /// Length between cross-section rings, metres.
    pub ring_spacing_m: f32,
    /// Vertices around each ring.
    pub ring_sides: u32,
    /// Bumpiness of the rock walls, metres.
    pub roughness_m: f32,
    /// The mouth flares out by this factor so it overlaps the pit wall.
    pub mouth_flare: f32,
    /// Boulders piled around the mouth.
    pub rubble: u32,
    /// Fine bump strength for the cave's paint (the ground uses about 0.03).
    pub bump_strength: f32,
}

/// One cross-section of a tube.
#[derive(Debug, Clone, Copy)]
pub struct Ring {
    /// Centre, in body space.
    pub centre: Vector3,
    /// Unit vectors: along the tube, to its side, and up.
    pub forward: Vector3,
    pub side: Vector3,
    pub up: Vector3,
    /// Half-width and ceiling height.
    pub width: f32,
    pub height: f32,
    /// Floor level below the centre (a flat walking floor).
    pub floor: f32,
}

#[derive(Debug, Clone)]
pub struct Cave {
    pub rings: Vec<Ring>,
    /// Bounding sphere (body space) for quick rejection.
    pub bound_centre: Vector3,
    pub bound_radius: f32,
    /// Where the mouth is: centre of the first ring.
    pub mouth: Vector3,
    seed: u32,
}

/// Cut volume is this much larger than the tube, so no ground pokes inside.
const CUT_MARGIN: f32 = 1.1;
/// Rings at the mouth left uncut (about 2 m at 1 m ring spacing).
const CUT_START_RING: usize = 2;
/// The floor is flat below this share of the ceiling height.
const FLOOR_SHARE: f32 = 0.55;

impl Cave {
    /// Build the tube for one pit. `shape` gives the ground to stay under.
    pub fn new(pit: &Landform, shape: &TerrainShape, data: &CaveData, seed: u64) -> Self {
        let mut rng = SeededRng::derive(seed, "cave");
        let s = rng.next_u64() as u32;
        let r_body = shape.radius();
        let length = rng.range_f32(data.length_m.0, data.length_m.1);
        let width = rng.range_f32(data.radius_m.0, data.radius_m.1);
        let height = width * data.height_ratio;
        let floor = height * FLOOR_SHARE;
        let heading0 = random_tangent(pit.center, &mut rng);

        // Start inside the pit, a couple of metres in front of the wall foot.
        let wall_foot = pit.angle * 0.72;
        let start_dir = rotate(pit.center, heading0, wall_foot - 2.5 / r_body);
        let floor_r = shape.surface_radius(start_dir);
        let mut centre_r = floor_r + floor - 0.3;

        let n = (length / data.ring_spacing_m).ceil().max(4.0) as usize;
        let mut dirs = Vec::with_capacity(n + 1);
        let mut dir = start_dir;
        let mut heading = heading0;
        for i in 0..=n {
            dirs.push(dir);
            // Meander: turn the heading slowly with noise.
            let t = i as f32 * data.ring_spacing_m;
            let turn = noise::fbm3([t * 0.05, 3.7, 1.3], 2, 0.5, s) * 0.09;
            let left = dir.cross(heading).normalized();
            heading = (heading + left * turn).normalized();
            let next = rotate(dir, heading, data.ring_spacing_m / centre_r);
            // Parallel-transport the heading to the new point.
            heading = (heading - next * heading.dot(next)).normalized();
            dir = next;
        }

        // Radius profile: a bulging chamber near the end, closing at the tip.
        let profile = |t: f32| -> f32 {
            let chamber = (-((t - 0.8) / 0.11).powi(2)).exp() * (data.chamber_scale - 1.0);
            let tip = smoothstep(1.0, 0.93, t);
            let mouth = 1.0 + (data.mouth_flare - 1.0) * smoothstep(4.0, 0.0, t * length);
            (1.0 + chamber) * tip.max(0.05).sqrt() * mouth
        };

        // Sink the tube where the ground above is thin; never rise again.
        let mut rings = Vec::with_capacity(dirs.len());
        for (i, d) in dirs.iter().enumerate() {
            let t = i as f32 / n as f32;
            let k = profile(t);
            let (w, h) = (width * k, height * k);
            // Past the pit wall, keep `cover_m` of rock over the ceiling.
            let past_wall = d.angle_to(pit.center) > pit.angle * 1.05;
            if past_wall {
                let ground = shape.surface_radius(*d);
                let ceiling_limit = ground - data.cover_m - (h - floor);
                centre_r = centre_r.min(ceiling_limit);
            }
            let centre = *d * centre_r;
            rings.push(Ring {
                centre,
                forward: Vector3::ZERO,
                side: Vector3::ZERO,
                up: *d,
                width: w,
                height: h,
                floor: floor * k.min(1.0),
            });
        }
        // Smooth the floor line (a moving average of the centre radius),
        // keeping only drops so the ceiling stays covered.
        let radii: Vec<f32> = rings.iter().map(|r| r.centre.length()).collect();
        for i in 0..rings.len() {
            let lo = i.saturating_sub(3);
            let hi = (i + 3).min(rings.len() - 1);
            let avg = radii[lo..=hi].iter().sum::<f32>() / (hi - lo + 1) as f32;
            let r = avg.min(radii[i] + 0.2).min(radii[lo..=i].iter().copied().fold(f32::MAX, f32::min) + 0.2);
            rings[i].centre = rings[i].up * r;
        }
        for i in 0..rings.len() {
            let a = rings[i.saturating_sub(1)].centre;
            let b = rings[(i + 1).min(rings.len() - 1)].centre;
            let up = rings[i].up;
            let fwd = (b - a).normalized();
            let fwd = (fwd - up * fwd.dot(up)).normalized();
            rings[i].forward = fwd;
            rings[i].side = fwd.cross(up).normalized();
        }

        let mouth = rings[0].centre;
        let bound_centre = rings.iter().fold(Vector3::ZERO, |a, r| a + r.centre) / rings.len() as f32;
        let bound_radius = rings
            .iter()
            .map(|r| r.centre.distance_to(bound_centre) + r.width.max(r.height) * CUT_MARGIN * 1.2)
            .fold(0.0, f32::max);
        Self { rings, bound_centre, bound_radius, mouth, seed: s }
    }

    /// True if `p` (body space) is inside the tube's cut volume: the ground
    /// there must go so the tube can be walked.
    pub fn inside_cut(&self, p: Vector3) -> bool {
        self.local(p).is_some_and(|(i, u, v, ring)| {
            // The first rings stay under the intact pit floor, so the ground
            // overlaps the tube's front edge and no gap shows beneath it.
            if i < CUT_START_RING {
                return false;
            }
            let vy = if v < 0.0 { v / ring.floor.max(0.1) } else { v / ring.height };
            (u / ring.width).powi(2) + vy.powi(2) < CUT_MARGIN * CUT_MARGIN
        })
    }

    /// True if `p` is inside the walkable tube (used for the helmet lamp
    /// and to find the cave's floor in tests).
    pub fn contains(&self, p: Vector3) -> bool {
        self.local(p).is_some_and(|(i, u, v, ring)| {
            i > 1 && (u / ring.width).powi(2) + (v / ring.height).powi(2) < 1.0 && v > -ring.floor - 0.5
        })
    }

    /// Nearest ring and `p`'s side/up offsets from its centre, if `p` lies
    /// between the first and last ring.
    fn local(&self, p: Vector3) -> Option<(usize, f32, f32, Ring)> {
        if p.distance_to(self.bound_centre) > self.bound_radius {
            return None;
        }
        let (i, ring) = self
            .rings
            .iter()
            .enumerate()
            .min_by(|a, b| a.1.centre.distance_squared_to(p).total_cmp(&b.1.centre.distance_squared_to(p)))?;
        let rel = p - ring.centre;
        let along = rel.dot(ring.forward);
        let spacing = if self.rings.len() > 1 { self.rings[0].centre.distance_to(self.rings[1].centre) } else { 1.0 };
        if (i == 0 && along < -spacing * 0.5) || (i == self.rings.len() - 1 && along > spacing * 0.5) {
            return None;
        }
        Some((i, rel.dot(ring.side), rel.dot(ring.up), *ring))
    }

    /// The point on the tube floor nearest the middle of the tube.
    pub fn floor_point(&self, t: f32) -> Vector3 {
        let i = ((self.rings.len() - 1) as f32 * t.clamp(0.0, 1.0)).round() as usize;
        let r = &self.rings[i];
        r.centre - r.up * r.floor
    }

    /// The tube's rock walls: rings of vertices joined into a mesh whose
    /// faces point inward, with a flat floor and bumpy, seeded rock.
    pub fn build_mesh(&self, data: &CaveData) -> MeshData {
        let sides = data.ring_sides.max(8) as usize;
        let mut m = MeshData::default();
        for ring in &self.rings {
            for k in 0..=sides {
                let a = k as f32 / sides as f32 * std::f32::consts::TAU;
                let (sa, ca) = a.sin_cos();
                let mut v = sa * ring.height;
                if v < -ring.floor {
                    // Flat floor with a slight dip in the middle.
                    v = -ring.floor - 0.15 * (1.0 - ca * ca);
                }
                let u = ca * ring.width;
                let base = ring.centre + ring.side * u + ring.up * v;
                let q = base * 0.45;
                let bump = noise::fbm3([q.x, q.y, q.z], 3, 0.5, self.seed) * data.roughness_m;
                let floorish = (v <= -ring.floor + 0.05) as i32 as f32;
                let out = (ring.side * ca + ring.up * sa.max(0.0)).normalized();
                let p = base + out * bump * (1.0 - floorish * 0.8);
                m.positions.push(p);
                m.normals.push(Vector3::ZERO);
                m.samples.push(SurfaceSample {
                    height: 0.0,
                    crater_floor: 0.0,
                    crater_rim: 0.0,
                    land: super::landforms::LandHints {
                        strata: (1.0 - floorish) * 0.5,
                        lava: 0.15,
                        ..Default::default()
                    },
                });
            }
        }
        let row = sides + 1;
        for i in 0..self.rings.len() - 1 {
            for k in 0..sides {
                let a = (i * row + k) as i32;
                let b = a + 1;
                let c = a + row as i32;
                let d = c + 1;
                // Wound so the faces point into the tube.
                m.indices.extend_from_slice(&[a, b, c, b, d, c]);
            }
        }
        // Close the far end with a fan to a point just past the last ring.
        let last = self.rings[self.rings.len() - 1];
        let tip = m.positions.len() as i32;
        m.positions.push(last.centre + last.forward * 0.6 - last.up * last.floor * 0.3);
        m.normals.push(Vector3::ZERO);
        m.samples.push(m.samples[m.samples.len() - 1]);
        let first = ((self.rings.len() - 1) * row) as i32;
        for k in 0..sides as i32 {
            m.indices.extend_from_slice(&[first + k, tip, first + k + 1]);
        }
        // Normals: accumulate face normals, then flip any facing out.
        let mut normals = vec![Vector3::ZERO; m.positions.len()];
        for t in m.indices.chunks_exact(3) {
            let (a, b, c) = (m.positions[t[0] as usize], m.positions[t[1] as usize], m.positions[t[2] as usize]);
            let n = (c - a).cross(b - a);
            for &i in t {
                normals[i as usize] += n;
            }
        }
        for (i, n) in normals.iter_mut().enumerate() {
            let ring = &self.rings[(i / row).min(self.rings.len() - 1)];
            let inward = if i == tip as usize { -ring.forward } else { ring.centre - m.positions[i] };
            if n.dot(inward) < 0.0 {
                *n = -*n;
            }
        }
        // Make every triangle face the same way as its vertex normals.
        for t in m.indices.chunks_exact_mut(3) {
            let (a, b, c) = (m.positions[t[0] as usize], m.positions[t[1] as usize], m.positions[t[2] as usize]);
            let face = (c - a).cross(b - a);
            if face.dot(normals[t[0] as usize]) < 0.0 {
                t.swap(1, 2);
            }
        }
        m.normals = normals.into_iter().map(|n| n.normalized()).collect();
        m
    }

    /// Spots for rubble boulders: (body-space position, size). Heaped in the
    /// floor corners of the mouth and along the pit wall's foot either side,
    /// then a fallen block every few metres along the tube.
    pub fn rubble(&self, data: &CaveData) -> Vec<(Vector3, f32)> {
        let mut rng = SeededRng::from_u64(self.seed as u64 ^ 0xC0FFEE);
        let ring = &self.rings[(2).min(self.rings.len() - 1)];
        let mut spots: Vec<(Vector3, f32)> = (0..data.rubble)
            .map(|_| {
                let side = if rng.next_f32() < 0.5 { -1.0 } else { 1.0 };
                let u = side * ring.width * rng.range_f32(0.6, 1.4);
                let (v, size) = (-ring.floor + rng.range_f32(-0.2, 0.3), rng.range_f32(0.5, 2.0).powf(1.3));
                let p = ring.centre + ring.side * u + ring.up * v - ring.forward * rng.range_f32(-0.5, 3.0);
                (p, size)
            })
            .collect();
        // Fallen blocks along the floor edges further in.
        for i in (6..self.rings.len().saturating_sub(6)).step_by(7) {
            let r = &self.rings[i];
            let side = if i % 2 == 0 { 1.0 } else { -1.0 };
            let u = side * r.width * rng.range_f32(0.5, 0.8);
            spots.push((r.centre + r.side * u - r.up * r.floor, rng.range_f32(0.4, 1.1)));
        }
        spots
    }
}

/// Caves for a body: one per sinkhole, for the first `data.count` sinkholes.
pub fn caves_for(shape: &TerrainShape, data: &CaveData, seed: u64) -> Vec<Cave> {
    shape
        .landforms()
        .list
        .iter()
        .filter(|l| l.kind == LandformKind::Sinkhole)
        .take(data.count as usize)
        .enumerate()
        .map(|(i, pit)| Cave::new(pit, shape, data, seed.wrapping_add(i as u64 * 7_919)))
        .collect()
}

/// Remove the ground's triangles that touch any cave's cut
/// volume (marked by collapsing their indices; `split_tiles` drops them).
pub fn cut_ground(mesh: &mut MeshData, caves: &[Cave]) {
    // A triangle goes if any corner is inside: the flared mouth covers the
    // ragged edge this leaves, so no wall slivers hang across the opening.
    if caves.is_empty() {
        return;
    }
    let near: Vec<bool> = mesh
        .positions
        .iter()
        .map(|p| caves.iter().any(|c| c.inside_cut(*p)))
        .collect();
    for t in mesh.indices.chunks_exact_mut(3) {
        if near[t[0] as usize] || near[t[1] as usize] || near[t[2] as usize] {
            let first = t[0];
            t[1] = first;
            t[2] = first;
        }
    }
}

fn random_tangent(d: Vector3, rng: &mut SeededRng) -> Vector3 {
    let a = if d.x.abs() < 0.9 { Vector3::RIGHT } else { Vector3::FORWARD };
    let a = d.cross(a).normalized();
    let b = d.cross(a);
    let t = rng.range_f32(0.0, std::f32::consts::TAU);
    (a * t.cos() + b * t.sin()).normalized()
}

/// Move `angle` radians from `from` along the tangent `dir`.
fn rotate(from: Vector3, dir: Vector3, angle: f32) -> Vector3 {
    let tangent = (dir - from * dir.dot(from)).normalized();
    (from * angle.cos() + tangent * angle.sin()).normalized()
}

fn smoothstep(e0: f32, e1: f32, x: f32) -> f32 {
    let t = ((x - e0) / (e1 - e0)).clamp(0.0, 1.0);
    t * t * (3.0 - 2.0 * t)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::procgen::landforms::{LandformData, PlacedLandform};
    use crate::procgen::terrain::TerrainData;

    fn shape() -> TerrainShape {
        let data = TerrainData {
            seed: 5,
            subdivisions: 256,
            hill_height_m: 1.0,
            hill_frequency: 6.0,
            hill_octaves: 3,
            ridge_height_m: 0.0,
            ridge_frequency: 1.0,
            crater_count: 0,
            crater_radius_m: (1.0, 2.0),
            crater_depth_ratio: 0.2,
            crater_rim_ratio: 0.3,
            landforms: LandformData {
                water_level_m: None,
                clear_zone_deg: 0.0,
                placed: vec![PlacedLandform { kind: LandformKind::Sinkhole, at: (0.0, 1.0, 0.0), to: None, radius_m: 7.0, height_m: 13.0 }],
                random: vec![],
            },
        };
        TerrainShape::new(&data, 260.0)
    }

    fn cave_data() -> CaveData {
        CaveData {
            count: 1,
            length_m: (40.0, 50.0),
            radius_m: (3.5, 3.5),
            height_ratio: 0.9,
            cover_m: 2.0,
            chamber_scale: 1.6,
            ring_spacing_m: 1.0,
            ring_sides: 16,
            roughness_m: 0.3,
            mouth_flare: 1.4,
            rubble: 8,
            bump_strength: 0.01,
        }
    }

    #[test]
    fn determinism_cave_is_seeded() {
        let s = shape();
        let a = caves_for(&s, &cave_data(), 11);
        let b = caves_for(&s, &cave_data(), 11);
        let c = caves_for(&s, &cave_data(), 12);
        assert_eq!(a[0].rings.len(), b[0].rings.len());
        assert_eq!(a[0].rings.last().unwrap().centre, b[0].rings.last().unwrap().centre);
        assert_ne!(a[0].rings.last().unwrap().centre, c[0].rings.last().unwrap().centre);
    }

    #[test]
    fn tube_stays_under_the_ground_with_rock_above() {
        let s = shape();
        let data = cave_data();
        let cave = &caves_for(&s, &data, 11)[0];
        let pit = &s.landforms().list[0];
        for ring in cave.rings.iter().filter(|r| r.up.angle_to(pit.center) > pit.angle * 1.1) {
            let ceiling = ring.centre.length() + ring.height;
            let ground = s.surface_radius(ring.up);
            assert!(ground - ceiling > data.cover_m * 0.6, "only {} m of rock over the ceiling", ground - ceiling);
        }
        // The mouth sits on the pit floor, well below the plain.
        let mouth_floor = cave.floor_point(0.0).length() - s.radius();
        assert!(mouth_floor < -9.0, "mouth floor {mouth_floor}");
    }

    #[test]
    fn mouth_cuts_the_pit_wall_but_not_the_plain() {
        let s = shape();
        let cave = &caves_for(&s, &cave_data(), 11)[0];
        let mut ground = s.build_mesh();
        let before = ground.indices.chunks_exact(3).filter(|t| t[0] != t[1]).count();
        cut_ground(&mut ground, std::slice::from_ref(cave));
        let after = ground.indices.chunks_exact(3).filter(|t| t[0] != t[1]).count();
        assert!(before > after, "some pit wall is cut away");
        assert!(before - after < 200, "only a small patch is cut ({})", before - after);
        // A point in the middle of the tube is inside; one on the plain isn't.
        let mid = cave.floor_point(0.5) + cave.rings[cave.rings.len() / 2].up * 1.0;
        assert!(cave.contains(mid));
        let plain = s.landforms().list[0].center.normalized();
        let far = (plain + Vector3::RIGHT * 0.3).normalized() * (s.surface_radius((plain + Vector3::RIGHT * 0.3).normalized()) + 1.0);
        assert!(!cave.contains(far));
    }

    #[test]
    fn walls_face_inward_and_end_chamber_is_wider() {
        let s = shape();
        let data = cave_data();
        let cave = &caves_for(&s, &data, 11)[0];
        let mesh = cave.build_mesh(&data);
        let sides = data.ring_sides as usize + 1;
        for (i, (p, n)) in mesh.positions.iter().zip(&mesh.normals).enumerate().take(cave.rings.len() * sides).step_by(7) {
            let c = cave.rings[i / sides].centre;
            assert!(n.dot(c - *p) >= 0.0, "normal at {i} faces out");
        }
        let mid = cave.rings[cave.rings.len() / 3].width;
        let chamber = cave.rings.iter().map(|r| r.width).fold(0.0, f32::max);
        assert!(chamber > mid * 1.3, "chamber {chamber} vs tube {mid}");
    }
}
