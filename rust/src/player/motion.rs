//! Movement maths for walking on curved gravity. Pure functions, unit tested.

use godot::builtin::{Basis, Quaternion, Vector3};

/// Rotate `basis` so its Y axis turns toward `up`, by at most `max_angle`
/// radians. Keeps the player's heading as stable as possible.
pub fn align_up(basis: Basis, up: Vector3, max_angle: f32) -> Basis {
    let current = basis.col_b().normalized();
    let up = up.normalized();
    let axis = current.cross(up);
    let axis_len = axis.length();
    let angle = current.angle_to(up);
    if angle < 1e-5 {
        return basis;
    }
    // Exactly upside down: any horizontal axis works; use the basis's own X.
    let axis = if axis_len < 1e-6 { basis.col_a().normalized() } else { axis / axis_len };
    let step = angle.min(max_angle);
    (Basis::from_quaternion(Quaternion::from_axis_angle(axis, step)) * basis).orthonormalized()
}

/// Split `velocity` into the part along `up` and the part across it.
pub fn split_vertical(velocity: Vector3, up: Vector3) -> (Vector3, Vector3) {
    let vertical = up * velocity.dot(up);
    (vertical, velocity - vertical)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn align_reaches_target_when_step_is_large() {
        let up = Vector3::new(1.0, 1.0, 0.0).normalized();
        let b = align_up(Basis::IDENTITY, up, 10.0);
        assert!((b.col_b() - up).length() < 1e-4);
    }

    #[test]
    fn align_is_limited_by_max_angle() {
        let b = align_up(Basis::IDENTITY, Vector3::RIGHT, 0.1);
        assert!((b.col_b().angle_to(Vector3::UP) - 0.1).abs() < 1e-4);
    }

    #[test]
    fn align_handles_upside_down() {
        let b = align_up(Basis::IDENTITY, Vector3::DOWN, 10.0);
        assert!((b.col_b() - Vector3::DOWN).length() < 1e-4);
    }

    #[test]
    fn split_recombines() {
        let v = Vector3::new(3.0, -2.0, 1.0);
        let (vert, horiz) = split_vertical(v, Vector3::UP);
        assert_eq!(vert, Vector3::new(0.0, -2.0, 0.0));
        assert_eq!(vert + horiz, v);
    }
}
