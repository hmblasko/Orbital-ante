//! Procedural meshes for ship interiors: the rounded cabin bowl with its
//! window openings, the floor with the hatch hole, rounded boxes (seat,
//! console), rings and tubes.
//!
//! Everything is smooth and bevelled (CLAUDE.md section 11: no plain
//! primitives, no hard 90-degree edges in final art). Pure geometry so it can
//! be unit tested; `to_array_mesh` turns it into a Godot mesh.

use std::f32::consts::{PI, TAU};

use godot::classes::ArrayMesh;
use godot::classes::mesh::{ArrayType, PrimitiveType};
use godot::prelude::*;

/// Triangle mesh with normals and UVs. Triangles are wound so their front
/// face is the side the normal points to (Godot: clockwise from the front).
#[derive(Debug, Default, Clone)]
pub struct Geo {
    pub positions: Vec<Vector3>,
    pub normals: Vec<Vector3>,
    pub uvs: Vec<Vector2>,
    pub indices: Vec<i32>,
}

impl Geo {
    fn push(&mut self, p: Vector3, n: Vector3, uv: Vector2) -> i32 {
        self.positions.push(p);
        self.normals.push(n);
        self.uvs.push(uv);
        self.positions.len() as i32 - 1
    }

    /// A quad a-b-c-d (counter-clockwise seen from the front) as two
    /// triangles, wound for Godot.
    fn quad(&mut self, a: i32, b: i32, c: i32, d: i32) {
        self.indices.extend_from_slice(&[a, c, b, a, d, c]);
    }

    /// Flip triangles whose winding disagrees with their vertex normals.
    pub fn fix_winding(&mut self) {
        for t in self.indices.chunks_mut(3) {
            let (a, b, c) = (self.positions[t[0] as usize], self.positions[t[1] as usize], self.positions[t[2] as usize]);
            let face = (b - a).cross(c - a);
            let n = self.normals[t[0] as usize] + self.normals[t[1] as usize] + self.normals[t[2] as usize];
            // Godot's front faces are clockwise, so the cross product of a
            // front face points away from its normal.
            if face.dot(n) > 0.0 {
                t.swap(1, 2);
            }
        }
    }

    pub fn append(&mut self, other: &Geo, transform: Transform3D) {
        let base = self.positions.len() as i32;
        for i in 0..other.positions.len() {
            self.positions.push(transform * other.positions[i]);
            self.normals.push((transform.basis * other.normals[i]).normalized());
            self.uvs.push(other.uvs[i]);
        }
        self.indices.extend(other.indices.iter().map(|i| i + base));
    }

    pub fn triangle_count(&self) -> usize {
        self.indices.len() / 3
    }

    pub fn to_array_mesh(&self) -> Gd<ArrayMesh> {
        let mut arrays = VarArray::new();
        arrays.resize(ArrayType::MAX.ord() as usize, &Variant::nil());
        let positions: PackedVector3Array = self.positions.iter().copied().collect();
        let normals: PackedVector3Array = self.normals.iter().copied().collect();
        let uvs: PackedVector2Array = self.uvs.iter().copied().collect();
        let indices: PackedInt32Array = self.indices.iter().copied().collect();
        arrays.set(ArrayType::VERTEX.ord() as usize, &positions.to_variant());
        arrays.set(ArrayType::NORMAL.ord() as usize, &normals.to_variant());
        arrays.set(ArrayType::TEX_UV.ord() as usize, &uvs.to_variant());
        arrays.set(ArrayType::INDEX.ord() as usize, &indices.to_variant());
        let mut mesh = ArrayMesh::new_gd();
        mesh.add_surface_from_arrays(PrimitiveType::TRIANGLES, &arrays);
        mesh
    }
}

/// The cabin bowl's shape: an elliptical room with a rounded floor edge,
/// straight walls and a dome.
#[derive(Debug, Clone, Copy)]
pub struct Bowl {
    pub floor_y: f32,
    pub wall_top_y: f32,
    pub dome_top_y: f32,
    pub radius_x: f32,
    pub radius_z: f32,
    pub center_z: f32,
}

/// A window opening in the bowl wall.
#[derive(Debug, Clone, Copy)]
pub struct WindowCut {
    pub angle: f32,
    pub half_width: f32,
    pub bottom_y: f32,
    pub top_y: f32,
    pub taper: f32,
}

impl WindowCut {
    /// Whether the wall point at angle `a` (radians, 0 = +Z) and height `y`
    /// is open.
    pub fn contains(&self, a: f32, y: f32) -> bool {
        if y < self.bottom_y || y > self.top_y {
            return false;
        }
        let t = (y - self.bottom_y) / (self.top_y - self.bottom_y);
        let half = self.half_width * (self.taper + (1.0 - self.taper) * t);
        angle_gap(a, self.angle).abs() <= half
    }
}

/// Signed smallest difference between two angles.
pub fn angle_gap(a: f32, b: f32) -> f32 {
    let mut d = (a - b) % TAU;
    if d > PI {
        d -= TAU;
    } else if d < -PI {
        d += TAU;
    }
    d
}

/// Fillet radius where the floor meets the wall.
const FILLET: f32 = 0.35;

impl Bowl {
    /// Profile of the wall from the floor's edge up to the dome's top:
    /// (scale of the ellipse, height, inward normal in (radial, up)).
    fn profile(&self, steps_wall: usize, steps_dome: usize) -> Vec<(f32, f32, Vector2)> {
        let r = self.radius_x.min(self.radius_z);
        let f = FILLET / r;
        let mut out = Vec::new();
        // Fillet: a quarter circle from the floor to the wall.
        for i in 0..=4 {
            let a = (i as f32 / 4.0) * PI * 0.5;
            let s = 1.0 - f + f * a.sin();
            let y = self.floor_y + FILLET * (1.0 - a.cos());
            out.push((s, y, Vector2::new(-a.sin(), a.cos())));
        }
        // Straight wall.
        for i in 1..=steps_wall {
            let y = self.floor_y + FILLET + (self.wall_top_y - self.floor_y - FILLET) * i as f32 / steps_wall as f32;
            out.push((1.0, y, Vector2::new(-1.0, 0.0)));
        }
        // Dome: a quarter ellipse closing at the top.
        let h = self.dome_top_y - self.wall_top_y;
        for i in 1..=steps_dome {
            let a = (i as f32 / steps_dome as f32) * PI * 0.5;
            let s = a.cos().max(0.0);
            let y = self.wall_top_y + h * a.sin();
            let n = Vector2::new(-a.cos() / r, -a.sin() / h.max(0.01)).normalized();
            out.push((s, y, n));
        }
        out
    }

    fn point(&self, angle: f32, scale: f32, y: f32) -> Vector3 {
        Vector3::new(self.radius_x * scale * angle.sin(), y, self.center_z + self.radius_z * scale * angle.cos())
    }

    /// Whether a point (ship space) is inside the bowl.
    pub fn contains(&self, p: Vector3, margin: f32) -> bool {
        if p.y < self.floor_y - margin || p.y > self.dome_top_y + margin {
            return false;
        }
        let ex = p.x / (self.radius_x + margin);
        let ez = (p.z - self.center_z) / (self.radius_z + margin);
        ex * ex + ez * ez <= 1.0
    }

    /// The walls and dome, facing inward, with the windows left open.
    pub fn shell(&self, segments: usize, windows: &[WindowCut]) -> Geo {
        let profile = self.profile(10, 8);
        let mut g = Geo::default();
        let rows = profile.len();
        let cols = segments + 1;
        // Height along the wall in metres, for the panel pattern.
        for j in 0..cols {
            let a = TAU * j as f32 / segments as f32;
            for (s, y, n) in &profile {
                let p = self.point(a, *s, *y);
                // The profile normal is (outward part, up part); outward
                // follows the ellipse's own normal.
                let outward = Vector3::new(a.sin() / self.radius_x, 0.0, a.cos() / self.radius_z).normalized();
                let normal = (outward * n.x + Vector3::UP * n.y).normalized();
                g.push(p, normal, Vector2::new(j as f32 / segments as f32, *y - self.floor_y));
            }
        }
        for j in 0..segments {
            for i in 0..rows - 1 {
                let a0 = TAU * (j as f32 + 0.5) / segments as f32;
                let ym = (profile[i].1 + profile[i + 1].1) * 0.5;
                if windows.iter().any(|w| w.contains(a0, ym)) {
                    continue;
                }
                let k = |jj: usize, ii: usize| (jj * rows + ii) as i32;
                g.quad(k(j, i), k(j + 1, i), k(j + 1, i + 1), k(j, i + 1));
            }
        }
        g.fix_winding();
        g
    }

    /// The floor: an ellipse out to where the fillet starts, with a round
    /// hole for the hatch. Faces up.
    pub fn floor(&self, hole_center: Vector2, hole_radius: f32, segments: usize, rings: usize) -> Geo {
        let r = self.radius_x.min(self.radius_z);
        let edge = 1.0 - FILLET / r;
        let mut g = Geo::default();
        // Rings from the hole's edge out to the floor's edge, blending the
        // circle into the ellipse.
        for i in 0..=rings {
            let t = i as f32 / rings as f32;
            for j in 0..=segments {
                let a = TAU * j as f32 / segments as f32;
                let inner = Vector2::new(hole_center.x + hole_radius * a.sin(), hole_center.y + hole_radius * a.cos());
                let outer3 = self.point(a, edge, self.floor_y);
                let outer = Vector2::new(outer3.x, outer3.z);
                let q = inner.lerp(outer, t);
                g.push(Vector3::new(q.x, self.floor_y, q.y), Vector3::UP, Vector2::new(q.x, q.y));
            }
        }
        let cols = segments + 1;
        for i in 0..rings {
            for j in 0..segments {
                let k = |ii: usize, jj: usize| (ii * cols + jj) as i32;
                g.quad(k(i, j), k(i, j + 1), k(i + 1, j + 1), k(i + 1, j));
            }
        }
        g.fix_winding();
        g
    }
}

/// A box with rounded edges and corners (the cube-to-sphere trick): every
/// vertex of a subdivided cube is pulled onto the surface `radius` away from
/// the inner box.
pub fn rounded_box(size: Vector3, radius: f32, divisions: usize) -> Geo {
    let half = size * 0.5;
    let r = radius.min(half.x).min(half.y).min(half.z);
    let inner = half - Vector3::new(r, r, r);
    let mut g = Geo::default();
    let n = divisions.max(2);
    // Six faces: (normal axis, u axis, v axis).
    let faces = [
        (Vector3::RIGHT, Vector3::BACK, Vector3::UP),
        (Vector3::LEFT, Vector3::FORWARD, Vector3::UP),
        (Vector3::UP, Vector3::RIGHT, Vector3::BACK),
        (Vector3::DOWN, Vector3::RIGHT, Vector3::FORWARD),
        (Vector3::BACK, Vector3::LEFT, Vector3::UP),
        (Vector3::FORWARD, Vector3::RIGHT, Vector3::UP),
    ];
    for (normal, u, v) in faces {
        let base = g.positions.len() as i32;
        for i in 0..=n {
            for j in 0..=n {
                let s = i as f32 / n as f32 * 2.0 - 1.0;
                let t = j as f32 / n as f32 * 2.0 - 1.0;
                // Spread vertices toward the edges, where the curve is.
                let s = s.signum() * s.abs().powf(0.6);
                let t = t.signum() * t.abs().powf(0.6);
                let cube = (normal + u * s + v * t) * half;
                let clamped = Vector3::new(
                    cube.x.clamp(-inner.x, inner.x),
                    cube.y.clamp(-inner.y, inner.y),
                    cube.z.clamp(-inner.z, inner.z),
                );
                let out = cube - clamped;
                let dir = if out.length_squared() > 1e-10 { out.normalized() } else { normal };
                g.push(clamped + dir * r, dir, Vector2::new(i as f32 / n as f32, j as f32 / n as f32));
            }
        }
        for i in 0..n {
            for j in 0..n {
                let k = |ii: usize, jj: usize| base + (ii * (n + 1) + jj) as i32;
                g.quad(k(i, j), k(i + 1, j), k(i + 1, j + 1), k(i, j + 1));
            }
        }
    }
    g.fix_winding();
    g
}

/// A ring lying flat (around +Y).
pub fn torus(major: f32, minor: f32, segments: usize, sides: usize) -> Geo {
    let mut g = Geo::default();
    for j in 0..=segments {
        let a = TAU * j as f32 / segments as f32;
        let center = Vector3::new(a.cos() * major, 0.0, a.sin() * major);
        let out = Vector3::new(a.cos(), 0.0, a.sin());
        for i in 0..=sides {
            let b = TAU * i as f32 / sides as f32;
            let n = out * b.cos() + Vector3::UP * b.sin();
            g.push(center + n * minor, n, Vector2::new(j as f32 / segments as f32, i as f32 / sides as f32));
        }
    }
    for j in 0..segments {
        for i in 0..sides {
            let k = |jj: usize, ii: usize| (jj * (sides + 1) + ii) as i32;
            g.quad(k(j, i), k(j + 1, i), k(j + 1, i + 1), k(j, i + 1));
        }
    }
    g.fix_winding();
    g
}

/// A flat annulus (washer) lying flat, facing up, with a rounded outer lip.
pub fn flange(inner: f32, outer: f32, lip: f32, segments: usize) -> Geo {
    let mut g = Geo::default();
    // Profile across the flange: inner edge drops down, top, rounded outer lip.
    let profile: Vec<(f32, f32, Vector2)> = {
        let mut p = vec![(inner, -lip, Vector2::new(-1.0, 0.0)), (inner, 0.0, Vector2::new(-0.7, 0.7).normalized()), (inner + lip * 0.6, lip * 0.4, Vector2::new(0.0, 1.0))];
        p.push((outer - lip, lip * 0.4, Vector2::new(0.0, 1.0)));
        for i in 1..=4 {
            let a = i as f32 / 4.0 * PI * 0.5;
            p.push((outer - lip + lip * a.sin(), lip * 0.4 - lip * 0.4 * (1.0 - a.cos()) - lip * (1.0 - a.cos()) * 0.6, Vector2::new(a.sin(), a.cos())));
        }
        p
    };
    let rows = profile.len();
    for j in 0..=segments {
        let a = TAU * j as f32 / segments as f32;
        let (s, c) = a.sin_cos();
        for (r, y, n) in &profile {
            g.push(Vector3::new(s * r, *y, c * r), Vector3::new(s * n.x, n.y, c * n.x).normalized(), Vector2::new(j as f32 / segments as f32, *r));
        }
    }
    for j in 0..segments {
        for i in 0..rows - 1 {
            let k = |jj: usize, ii: usize| (jj * rows + ii) as i32;
            g.quad(k(j, i), k(j + 1, i), k(j + 1, i + 1), k(j, i + 1));
        }
    }
    g.fix_winding();
    g
}

/// The inside of a vertical tube, facing inward.
pub fn tube_inside(radius: f32, y0: f32, y1: f32, segments: usize, rows: usize) -> Geo {
    let mut g = Geo::default();
    for j in 0..=segments {
        let a = TAU * j as f32 / segments as f32;
        let (s, c) = a.sin_cos();
        for i in 0..=rows {
            let y = y0 + (y1 - y0) * i as f32 / rows as f32;
            g.push(Vector3::new(s * radius, y, c * radius), Vector3::new(-s, 0.0, -c), Vector2::new(j as f32 / segments as f32, y - y0));
        }
    }
    for j in 0..segments {
        for i in 0..rows {
            let k = |jj: usize, ii: usize| (jj * (rows + 1) + ii) as i32;
            g.quad(k(j, i), k(j + 1, i), k(j + 1, i + 1), k(j, i + 1));
        }
    }
    g.fix_winding();
    g
}

/// A rounded disc (the hatch door): flat top and bottom, rounded rim.
pub fn rounded_disc(radius: f32, thickness: f32, segments: usize) -> Geo {
    let mut g = Geo::default();
    let h = thickness * 0.5;
    let rim = h;
    let mut profile = vec![(0.0, h, Vector2::new(0.0, 1.0)), (radius - rim, h, Vector2::new(0.0, 1.0))];
    for i in 1..8 {
        let a = PI * 0.5 - PI * i as f32 / 8.0;
        profile.push((radius - rim + rim * a.cos(), h * a.sin(), Vector2::new(a.cos(), a.sin())));
    }
    profile.push((radius - rim, -h, Vector2::new(0.0, -1.0)));
    profile.push((0.0, -h, Vector2::new(0.0, -1.0)));
    let rows = profile.len();
    for j in 0..=segments {
        let a = TAU * j as f32 / segments as f32;
        let (s, c) = a.sin_cos();
        for (r, y, n) in &profile {
            g.push(Vector3::new(s * r, *y, c * r), Vector3::new(s * n.x, n.y, c * n.x).normalized(), Vector2::new(0.5 + 0.5 * s * r / radius, 0.5 + 0.5 * c * r / radius));
        }
    }
    for j in 0..segments {
        for i in 0..rows - 1 {
            let k = |jj: usize, ii: usize| (jj * rows + ii) as i32;
            g.quad(k(j, i), k(j + 1, i), k(j + 1, i + 1), k(j, i + 1));
        }
    }
    g.fix_winding();
    g
}

#[cfg(test)]
mod tests {
    use super::*;

    fn bowl() -> Bowl {
        Bowl { floor_y: 6.2, wall_top_y: 9.0, dome_top_y: 10.0, radius_x: 1.85, radius_z: 2.7, center_z: 0.0 }
    }

    fn faces_inward(g: &Geo, centre: Vector3) -> f32 {
        // Fraction of vertex normals pointing toward the centre.
        let inward = g.positions.iter().zip(&g.normals).filter(|(p, n)| n.dot(centre - **p) > 0.0).count();
        inward as f32 / g.positions.len() as f32
    }

    #[test]
    fn shell_faces_inward_and_stays_in_the_bowl() {
        let b = bowl();
        let g = b.shell(48, &[]);
        assert!(faces_inward(&g, Vector3::new(0.0, 8.0, 0.3)) > 0.95);
        for p in &g.positions {
            assert!(b.contains(*p, 0.01), "{p:?}");
        }
    }

    #[test]
    fn windows_cut_holes_in_the_shell() {
        let b = bowl();
        let w = WindowCut { angle: 0.0, half_width: 0.3, bottom_y: 7.3, top_y: 8.6, taper: 0.5 };
        let solid = b.shell(64, &[]).triangle_count();
        let open = b.shell(64, &[w]).triangle_count();
        assert!(open < solid);
        assert!(w.contains(0.0, 8.0) && !w.contains(0.0, 9.5) && !w.contains(PI, 8.0));
        // Narrower at the bottom.
        assert!(w.contains(0.25, 8.55) && !w.contains(0.25, 7.4));
    }

    #[test]
    fn rounded_box_has_the_requested_size_and_no_hard_edges() {
        let g = rounded_box(Vector3::new(2.0, 1.0, 0.6), 0.15, 6);
        let (mut lo, mut hi) = (Vector3::splat(1e9), Vector3::splat(-1e9));
        for p in &g.positions {
            lo = Vector3::new(lo.x.min(p.x), lo.y.min(p.y), lo.z.min(p.z));
            hi = Vector3::new(hi.x.max(p.x), hi.y.max(p.y), hi.z.max(p.z));
        }
        assert!(((hi - lo) - Vector3::new(2.0, 1.0, 0.6)).length() < 1e-3);
        assert!(faces_inward(&g, Vector3::ZERO) < 0.01, "normals point out");
        // Corner normals are diagonal (bevelled), not axis-aligned.
        let corner = g.positions.iter().zip(&g.normals).max_by(|a, b| (a.0.x + a.0.y + a.0.z).total_cmp(&(b.0.x + b.0.y + b.0.z))).unwrap();
        assert!(corner.1.x > 0.3 && corner.1.y > 0.3 && corner.1.z > 0.3);
    }

    #[test]
    fn floor_hole_is_open() {
        let b = bowl();
        let g = b.floor(Vector2::new(0.0, -1.4), 0.65, 48, 6);
        for p in &g.positions {
            assert!(Vector2::new(p.x, p.z - -1.4).length() >= 0.649, "{p:?}");
        }
        assert!(g.normals.iter().all(|n| n.y > 0.99));
    }
}
