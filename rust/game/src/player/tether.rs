//! Safety tether (CLAUDE.md section 6): clip on near an anchor and the line
//! stops you floating away, but never reels you in.
//! `TetherAnchor` marks where a tether can clip on (ships and stations later).

use godot::classes::mesh::PrimitiveType;
use godot::classes::{ImmediateMesh, INode3D, MeshInstance3D, Node3D, StandardMaterial3D};
use godot::classes::base_material_3d::{Flags, TextureParam};
use godot::classes::gradient::InterpolationMode;
use godot::classes::{Gradient, GradientTexture1D};
use godot::prelude::*;

pub const TETHER_GROUP: &str = "tether_anchors";

#[derive(GodotClass)]
#[class(base = Node3D, init)]
pub struct TetherAnchor {
    base: Base<Node3D>,
}

#[godot_api]
impl INode3D for TetherAnchor {
    fn ready(&mut self) {
        self.base_mut().add_to_group(TETHER_GROUP);
    }
}

/// How the tether rope looks (data/player/interactables.ron).
#[derive(Debug, Clone, serde::Deserialize)]
pub struct RopeLook {
    pub radius_m: f32,
    /// Lengthwise segments and sides of the tube.
    pub segments: u32,
    pub sides: u32,
    /// Two colours twisted around the rope in a helix, like braided line.
    pub color_a: (f32, f32, f32),
    pub color_b: (f32, f32, f32),
    /// Helix turns per metre of rope.
    pub twists_per_m: f32,
    /// Where the rope clips on: this far below the body's centre (the belt).
    pub belt_below_center_m: f32,
}

/// Sag of a slack line: the depth of a parabola whose length is `length`
/// over a straight `chord` (arc length of a shallow parabola is about
/// chord + 8 h^2 / (3 chord)).
pub fn sag_depth(chord: f32, length: f32) -> f32 {
    if chord <= 0.01 {
        return 0.0;
    }
    (3.0 * chord * (length - chord).max(0.0) / 8.0).sqrt()
}

/// The visible rope from anchor to player: a shaded, braided tube that sags
/// under gravity when slack and floats in a loose curve in zero-g.
pub struct TetherLine {
    instance: Gd<MeshInstance3D>,
    mesh: Gd<ImmediateMesh>,
    look: RopeLook,
}

impl TetherLine {
    pub fn new(parent: &mut Gd<Node3D>, look: RopeLook) -> Self {
        let mesh = ImmediateMesh::new_gd();
        // A two-colour stripe texture wrapped as a helix gives the braid,
        // crisp at any distance and independent of the tube's resolution.
        let mut stripes = Gradient::new_gd();
        stripes.set_interpolation_mode(InterpolationMode::CONSTANT);
        stripes.set_offsets(&PackedFloat32Array::from(&[0.0f32, 0.5][..]));
        stripes.set_colors(&PackedColorArray::from(&[rgb(look.color_a), rgb(look.color_b)][..]));
        let mut texture = GradientTexture1D::new_gd();
        texture.set_gradient(&stripes);
        texture.set_width(64);
        let mut material = StandardMaterial3D::new_gd();
        material.set_texture(TextureParam::ALBEDO, &texture);
        material.set_flag(Flags::USE_TEXTURE_REPEAT, true);
        material.set_roughness(0.85);
        let mut instance = MeshInstance3D::new_alloc();
        instance.set_name("TetherLine");
        instance.set_mesh(&mesh);
        instance.set_material_override(&material);
        instance.set_as_top_level(true);
        parent.add_child(&instance);
        Self { instance, mesh, look }
    }

    /// `line` is (anchor, player); `gravity` decides which way slack hangs.
    /// Slack never hangs lower than `ground_below_m` under the player's end,
    /// so on the ground the spare rope lies in the dust instead of sinking.
    pub fn draw(&mut self, line: Option<(Vector3, Vector3)>, gravity: Vector3, length: f32, ground_below_m: f32) {
        self.mesh.clear_surfaces();
        let Some((a, b)) = line else {
            self.instance.set_visible(false);
            return;
        };
        self.instance.set_visible(true);
        self.instance.set_global_transform(Transform3D::IDENTITY);
        let chord = b - a;
        let d = chord.length();
        if d < 0.05 {
            return;
        }
        let along = chord / d;
        // Slack hangs along gravity, squared off against the line itself.
        let pull = if gravity.length_squared() > 0.01 { gravity.normalized() } else { along.cross(Vector3::UP).normalized() * 0.5 };
        let hang = pull - along * pull.dot(along);
        let sag = sag_depth(d, length) * hang.length().min(1.0);
        let hang = if hang.length_squared() > 1e-6 { hang.normalized() } else { Vector3::ZERO };
        let n = self.look.segments.max(2) as usize;
        let down = if gravity.length_squared() > 0.01 { gravity.normalized() } else { Vector3::ZERO };
        let floor = b.dot(down) + ground_below_m;
        let points: Vec<Vector3> = (0..=n)
            .map(|i| {
                let s = i as f32 / n as f32;
                let p = a + chord * s + hang * (sag * 4.0 * s * (1.0 - s));
                let depth = p.dot(down) - floor;
                if depth > 0.0 { p - down * depth } else { p }
            })
            .collect();
        let sides = self.look.sides.max(3) as usize;
        // One ring of (position, normal, braid coordinate) per point.
        let mut rings = Vec::with_capacity(points.len());
        let mut side_ref = along.cross(if along.dot(Vector3::UP).abs() > 0.9 { Vector3::RIGHT } else { Vector3::UP }).normalized();
        for (i, p) in points.iter().enumerate() {
            let tangent = (points[(i + 1).min(n)] - points[i.saturating_sub(1)]).normalized();
            // Keep the ring frame from twisting (parallel transport).
            side_ref = (side_ref - tangent * side_ref.dot(tangent)).normalized();
            let up_ref = tangent.cross(side_ref);
            let dist = i as f32 / n as f32 * d;
            let ring: Vec<(Vector3, Vector3, f32)> = (0..=sides)
                .map(|j| {
                    let angle = j as f32 / sides as f32 * std::f32::consts::TAU;
                    let normal = side_ref * angle.cos() + up_ref * angle.sin();
                    let helix = dist * self.look.twists_per_m + angle / std::f32::consts::TAU;
                    (*p + normal * self.look.radius_m, normal, helix)
                })
                .collect();
            rings.push(ring);
        }
        self.mesh.surface_begin(PrimitiveType::TRIANGLES);
        for i in 0..n {
            for j in 0..sides {
                let quad = [rings[i][j], rings[i + 1][j], rings[i][j + 1], rings[i][j + 1], rings[i + 1][j], rings[i + 1][j + 1]];
                for (pos, normal, helix) in quad {
                    self.mesh.surface_set_normal(normal);
                    self.mesh.surface_set_uv(Vector2::new(helix, 0.5));
                    self.mesh.surface_add_vertex(pos);
                }
            }
        }
        self.mesh.surface_end();
    }
}

fn rgb((r, g, b): (f32, f32, f32)) -> Color {
    Color::from_rgb(r, g, b)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn taut_rope_has_no_sag_and_slack_rope_sags() {
        assert_eq!(sag_depth(25.0, 25.0), 0.0);
        assert_eq!(sag_depth(30.0, 25.0), 0.0);
        let h = sag_depth(10.0, 25.0);
        // The parabola with this sag is about as long as the rope.
        let arc = 10.0 + 8.0 * h * h / (3.0 * 10.0);
        assert!((arc - 25.0).abs() < 0.01, "arc {arc}");
    }
}
