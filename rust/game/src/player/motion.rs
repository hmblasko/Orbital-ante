//! Movement maths for walking on curved gravity. Pure functions, unit tested.

use godot::builtin::{Basis, Quaternion, Vector2, Vector3};

/// Rotate `basis` so its Y axis turns toward `up`, by at most `max_angle`
/// radians. Keeps the player's heading as stable as possible.
/// Shape a stick reading (already past its deadzone, 0..1 long) with a
/// response curve: `exponent` > 1 makes small tilts finer for aiming
/// while full tilt stays full speed. Direction is kept.
pub fn stick_curve(v: Vector2, exponent: f32) -> Vector2 {
    let len = v.length().min(1.0);
    if len < 1e-6 {
        return Vector2::ZERO;
    }
    v / v.length() * len.powf(exponent.max(0.1))
}

/// Extra turn speed after the stick is held near full tilt for a moment
/// (so big turns are quick without making aiming twitchy). `held_s` is how
/// long it has been held; returns degrees per second to add.
pub fn turn_boost(held_s: f32, delay_s: f32, ramp_s: f32, boost_deg_s: f32) -> f32 {
    ((held_s - delay_s) / ramp_s.max(0.01)).clamp(0.0, 1.0) * boost_deg_s
}

/// Hold-or-tap crouch: held, you crouch while it is down; a quick tap
/// (released within `tap_s`) toggles a crouch that stays. Returns the new
/// (toggled, crouching now) state. `press_s` is how long the button has
/// been down (or was down, on the release frame).
pub fn crouch_state(toggled: bool, pressed: bool, released: bool, press_s: f32, tap_s: f32) -> (bool, bool) {
    let mut toggled = toggled;
    if released {
        // A tap flips the toggle; a long hold always ends standing.
        toggled = if press_s <= tap_s { !toggled } else { false };
    }
    (toggled, pressed || toggled)
}

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

/// How hard the jetpack is asked to fire, 0.0 to 1.0, from the stick and
/// up/down inputs. This is what burns fuel.
pub fn thrust_demand(stick: Vector2, vertical: f32) -> f32 {
    (stick.length() + vertical.abs()).min(1.0)
}

/// Velocity change from the jetpack this step. `stick.y` is negative for
/// forward (Godot's convention); `vertical` is +1 up, -1 down; `available`
/// is how much thrust the fuel allows (0.0 to 1.0).
#[allow(clippy::too_many_arguments)]
pub fn jetpack_delta_v(
    basis: Basis,
    camera_forward: Vector3,
    stick: Vector2,
    vertical: f32,
    thrust_acceleration: f32,
    vertical_acceleration: f32,
    available: f32,
    dt: f32,
) -> Vector3 {
    if available <= 0.0 {
        return Vector3::ZERO;
    }
    let right = basis.col_a().normalized();
    let up = basis.col_b().normalized();
    let planar = right * stick.x - camera_forward.normalized() * stick.y;
    let planar = if planar.length() > 1.0 { planar.normalized() } else { planar };
    (planar * thrust_acceleration + up * vertical.clamp(-1.0, 1.0) * vertical_acceleration) * available * dt
}

/// Velocity change that brings `relative` velocity toward zero at
/// `acceleration`, without overshooting.
pub fn match_velocity_delta(relative: Vector3, acceleration: f32, dt: f32) -> Vector3 {
    let step = acceleration * dt;
    let speed = relative.length();
    if speed <= step { -relative } else { -relative / speed * step }
}

/// Safety tether: once the player is at or beyond `length` from the anchor,
/// remove any velocity carrying them further away. It never pulls them in.
pub fn tether_velocity(anchor: Vector3, position: Vector3, velocity: Vector3, length: f32) -> Vector3 {
    let offset = position - anchor;
    let distance = offset.length();
    if distance < length || distance < 1e-6 {
        return velocity;
    }
    let outward = offset / distance;
    let away = velocity.dot(outward);
    if away > 0.0 { velocity - outward * away } else { velocity }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn stick_curve_is_finer_near_centre_and_full_at_the_edge() {
        let half = stick_curve(Vector2::new(0.5, 0.0), 2.0);
        assert!((half.x - 0.25).abs() < 1e-6);
        let full = stick_curve(Vector2::new(0.0, 1.0), 2.0);
        assert!((full.y - 1.0).abs() < 1e-6);
        assert_eq!(stick_curve(Vector2::ZERO, 2.0), Vector2::ZERO);
        let diag = stick_curve(Vector2::new(0.6, 0.8), 1.8);
        assert!((diag.x / diag.y - 0.75).abs() < 1e-5, "direction kept");
    }

    #[test]
    fn turn_boost_waits_then_ramps() {
        assert_eq!(turn_boost(0.1, 0.3, 0.4, 120.0), 0.0);
        assert!((turn_boost(0.5, 0.3, 0.4, 120.0) - 60.0).abs() < 1e-4);
        assert_eq!(turn_boost(3.0, 0.3, 0.4, 120.0), 120.0);
    }

    #[test]
    fn crouch_hold_and_tap() {
        // Held: crouched while down, standing after a long hold.
        let (t, c) = crouch_state(false, true, false, 0.1, 0.25);
        assert!(!t && c);
        let (t, c) = crouch_state(false, false, true, 1.0, 0.25);
        assert!(!t && !c);
        // Tap: stays crouched; tap again: stands.
        let (t, c) = crouch_state(false, false, true, 0.1, 0.25);
        assert!(t && c);
        let (t, c) = crouch_state(true, false, true, 0.1, 0.25);
        assert!(!t && !c);
    }

    #[test]
    fn jetpack_thrusts_where_you_look_and_needs_fuel() {
        let forward = Vector3::new(0.0, 0.0, -1.0);
        let dv = jetpack_delta_v(Basis::IDENTITY, forward, Vector2::new(0.0, -1.0), 0.0, 6.0, 8.0, 1.0, 0.5);
        assert!((dv - Vector3::new(0.0, 0.0, -3.0)).length() < 1e-5);
        let up = jetpack_delta_v(Basis::IDENTITY, forward, Vector2::ZERO, 1.0, 6.0, 8.0, 1.0, 0.5);
        assert!((up - Vector3::new(0.0, 4.0, 0.0)).length() < 1e-5);
        assert_eq!(jetpack_delta_v(Basis::IDENTITY, forward, Vector2::new(1.0, 0.0), 1.0, 6.0, 8.0, 0.0, 0.5), Vector3::ZERO);
    }

    #[test]
    fn diagonal_stick_is_not_faster() {
        let dv = jetpack_delta_v(Basis::IDENTITY, Vector3::FORWARD, Vector2::new(1.0, -1.0), 0.0, 6.0, 8.0, 1.0, 1.0);
        assert!((dv.length() - 6.0).abs() < 1e-4);
    }

    #[test]
    fn match_velocity_never_overshoots() {
        let rel = Vector3::new(3.0, 0.0, 0.0);
        assert_eq!(rel + match_velocity_delta(rel, 10.0, 1.0), Vector3::ZERO);
        let partial = rel + match_velocity_delta(rel, 1.0, 1.0);
        assert!((partial - Vector3::new(2.0, 0.0, 0.0)).length() < 1e-5);
    }

    #[test]
    fn tether_stops_drifting_away_but_never_reels_in() {
        let anchor = Vector3::ZERO;
        let at_end = Vector3::new(10.0, 0.0, 0.0);
        // Moving away at the end of the line: outward part removed, sideways kept.
        let v = tether_velocity(anchor, at_end, Vector3::new(4.0, 1.0, 0.0), 10.0);
        assert!((v - Vector3::new(0.0, 1.0, 0.0)).length() < 1e-5);
        // Moving back toward the anchor: untouched.
        assert_eq!(tether_velocity(anchor, at_end, Vector3::new(-2.0, 0.0, 0.0), 10.0), Vector3::new(-2.0, 0.0, 0.0));
        // Inside the length: untouched.
        assert_eq!(tether_velocity(anchor, Vector3::new(5.0, 0.0, 0.0), Vector3::new(4.0, 0.0, 0.0), 10.0), Vector3::new(4.0, 0.0, 0.0));
    }

    #[test]
    fn thrust_demand_is_capped() {
        assert_eq!(thrust_demand(Vector2::new(1.0, 0.0), 1.0), 1.0);
        assert_eq!(thrust_demand(Vector2::ZERO, 0.0), 0.0);
    }

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
