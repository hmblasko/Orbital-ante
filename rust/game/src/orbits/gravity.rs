//! Point gravity maths. Pure functions so they can be unit tested without Godot.

use godot::builtin::Vector3;
use serde::Deserialize;

/// Gravity settings for one body, loaded from its RON file.
#[derive(Debug, Clone, Deserialize)]
pub struct GravitySettings {
    /// Acceleration at the surface, in m/s^2.
    pub surface_gravity: f32,
    /// Gravity is zero beyond this distance from the centre, in metres.
    pub influence_radius: f32,
}

/// Gravity acceleration at `point` from a body at `center` with surface `radius`.
///
/// Outside the surface it falls off with the inverse square of distance;
/// inside it falls linearly to zero at the centre (so tunnels and caves behave).
pub fn point_gravity(center: Vector3, radius: f32, settings: &GravitySettings, point: Vector3) -> Vector3 {
    let offset = center - point;
    let distance = offset.length();
    if distance <= f32::EPSILON || distance > settings.influence_radius {
        return Vector3::ZERO;
    }
    let strength = if distance >= radius {
        settings.surface_gravity * (radius / distance).powi(2)
    } else {
        settings.surface_gravity * distance / radius
    };
    offset / distance * strength
}

/// How much of a parent body's pull is left at `distance` from one of its
/// moons: none inside `inner`, all of it beyond `outer` (the moon's
/// influence radius), easing smoothly between. This keeps a planet from
/// cancelling its own moon's gravity on the side facing it.
pub fn shield_weight(distance: f32, inner: f32, outer: f32) -> f32 {
    if outer <= inner {
        return if distance >= outer { 1.0 } else { 0.0 };
    }
    let t = ((distance - inner) / (outer - inner)).clamp(0.0, 1.0);
    t * t * (3.0 - 2.0 * t)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn settings() -> GravitySettings {
        GravitySettings { surface_gravity: 10.0, influence_radius: 500.0 }
    }

    #[test]
    fn surface_gravity_points_at_centre() {
        let g = point_gravity(Vector3::ZERO, 50.0, &settings(), Vector3::new(0.0, 50.0, 0.0));
        assert!((g - Vector3::new(0.0, -10.0, 0.0)).length() < 1e-4);
    }

    #[test]
    fn shield_fades_parent_pull_near_a_moon() {
        assert_eq!(shield_weight(100.0, 300.0, 600.0), 0.0);
        assert_eq!(shield_weight(700.0, 300.0, 600.0), 1.0);
        let mid = shield_weight(450.0, 300.0, 600.0);
        assert!((mid - 0.5).abs() < 1e-5);
        assert!(shield_weight(400.0, 300.0, 600.0) < mid);
    }

    #[test]
    fn inverse_square_outside() {
        let g = point_gravity(Vector3::ZERO, 50.0, &settings(), Vector3::new(100.0, 0.0, 0.0));
        assert!((g.length() - 2.5).abs() < 1e-4);
    }

    #[test]
    fn zero_beyond_influence_and_at_centre() {
        let s = settings();
        assert_eq!(point_gravity(Vector3::ZERO, 50.0, &s, Vector3::new(0.0, 501.0, 0.0)), Vector3::ZERO);
        assert_eq!(point_gravity(Vector3::ZERO, 50.0, &s, Vector3::ZERO), Vector3::ZERO);
    }
}
