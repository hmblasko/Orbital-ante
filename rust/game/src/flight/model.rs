//! Flight model maths, shared by every spacecraft (CLAUDE.md section 5:
//! "6-axis thrust, real inertia, match-velocity lock ... flight assist on by
//! default"). Pure functions on plain vectors, so they are unit tested
//! without Godot.
//!
//! Serves pillar 4 (everything is physical: real inertia, no speed limit, no
//! drag in space) and fun rule 10 (game feel: the ship answers the stick
//! instantly and stops turning when you let go).

use godot::builtin::{Vector2, Vector3};
use serde::Deserialize;

#[derive(Debug, Clone, Deserialize)]
pub struct FlightData {
    pub forward: f32,
    pub back: f32,
    pub strafe: f32,
    pub up: f32,
    pub down: f32,
    pub pitch_rate_deg: f32,
    pub yaw_rate_deg: f32,
    pub roll_rate_deg: f32,
    pub turn_accel_deg: f32,
    pub mouse_turn_deg: f32,
    pub flight_assist: bool,
    pub match_velocity_accel: f32,
    pub fuel_s: f32,
    pub landed_speed: f32,
}

/// The pilot's inputs for one tick. Ship space: +X left, +Y up, +Z forward.
#[derive(Debug, Clone, Copy, Default)]
pub struct PilotInput {
    /// Left stick: x = strafe right, y = forward (both -1 to 1).
    pub move_stick: Vector2,
    /// Up minus down thrust (-1 to 1).
    pub vertical: f32,
    /// Pitch up, yaw right, roll right (-1 to 1 each, before mouse).
    pub pitch: f32,
    pub yaw: f32,
    pub roll: f32,
    pub match_velocity: bool,
}

/// Thrust acceleration in ship space, and how hard the thrusters push
/// (0 to 1, for fuel and effects).
pub fn thrust(f: &FlightData, input: &PilotInput) -> (Vector3, f32) {
    let s = input.move_stick;
    let forward = if s.y >= 0.0 { s.y * f.forward } else { s.y * f.back };
    let vertical = if input.vertical >= 0.0 { input.vertical * f.up } else { input.vertical * f.down };
    // Strafing right is toward -X (the pilot faces +Z with +Y up).
    let a = Vector3::new(-s.x * f.strafe, vertical, forward);
    let effort = (s.x.abs() * f.strafe + s.y.abs() * f.forward.max(f.back) + input.vertical.abs() * f.up.max(f.down)) / f.up.max(f.forward);
    (a, effort.min(1.0))
}

/// Target turn rate in ship space (radians per second). Pitch up turns the
/// nose (+Z) toward +Y, which is a negative turn about +X; yaw right turns
/// it toward -X (negative about +Y); roll right drops the right side (-X),
/// which is positive about +Z.
pub fn target_turn_rate(f: &FlightData, input: &PilotInput) -> Vector3 {
    Vector3::new(
        -input.pitch.clamp(-1.0, 1.0) * f.pitch_rate_deg.to_radians(),
        -input.yaw.clamp(-1.0, 1.0) * f.yaw_rate_deg.to_radians(),
        input.roll.clamp(-1.0, 1.0) * f.roll_rate_deg.to_radians(),
    )
}

/// Steps the ship's turn rate (ship space) toward the pilot's target. With
/// flight assist, letting go brings the turn to a stop; without it, the
/// ship keeps spinning on any axis you aren't touching.
pub fn step_turn_rate(f: &FlightData, current: Vector3, input: &PilotInput, dt: f32) -> Vector3 {
    let target = target_turn_rate(f, input);
    let max = f.turn_accel_deg.to_radians() * dt;
    let step = |cur: f32, tgt: f32, active: bool| {
        if active || f.flight_assist || input.match_velocity {
            cur + (tgt - cur).clamp(-max, max)
        } else {
            cur
        }
    };
    Vector3::new(
        step(current.x, target.x, input.pitch != 0.0),
        step(current.y, target.y, input.yaw != 0.0),
        step(current.z, target.z, input.roll != 0.0),
    )
}

/// Velocity change this tick from match velocity: brake toward zero speed
/// relative to the reference, never overshooting.
pub fn match_velocity(relative: Vector3, accel: f32, dt: f32) -> Vector3 {
    let speed = relative.length();
    if speed < 1e-4 {
        return Vector3::ZERO;
    }
    -relative / speed * (accel * dt).min(speed)
}

/// Ship placement: the yaw that turns ship-forward (+Z) to face from `at`
/// toward `look` on the plane perpendicular to `up`.
pub fn facing_basis(up: Vector3, forward_hint: Vector3) -> godot::builtin::Basis {
    let up = up.normalized();
    let mut fwd = forward_hint - up * forward_hint.dot(up);
    if fwd.length_squared() < 1e-6 {
        fwd = if up.x.abs() < 0.9 { Vector3::RIGHT } else { Vector3::BACK };
        fwd = fwd - up * fwd.dot(up);
    }
    let z = fwd.normalized();
    let x = up.cross(z).normalized();
    godot::builtin::Basis::from_cols(x, up, z)
}

/// The gravity lift: acceleration that brings the rider's velocity
/// (relative to the ship, in ship space) to `target`, pulling them toward
/// the shaft's axis, capped at `accel`.
pub fn lift_accel(rel_velocity: Vector3, target: Vector3, off_axis: Vector2, accel: f32, center_pull: f32) -> Vector3 {
    let want = target - rel_velocity;
    let pull = Vector3::new(-off_axis.x, 0.0, -off_axis.y) * center_pull;
    let a = want * 4.0 + pull;
    if a.length() > accel { a.normalized() * accel } else { a }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn data() -> FlightData {
        let ship: crate::flight::ship::ShipData =
            crate::data::parse(include_str!("../../../../godot/data/ships/starter_lander.ron")).unwrap();
        ship.flight
    }

    #[test]
    fn up_thrust_beats_earth_gravity() {
        let f = data();
        let (a, effort) = thrust(&f, &PilotInput { vertical: 1.0, ..Default::default() });
        assert!(a.y > 9.8 * 1.5, "takeoff needs clear margin over 9.8 m/s^2");
        assert!((effort - 1.0).abs() < 1e-6);
    }

    #[test]
    fn stick_directions_map_to_ship_axes() {
        let f = data();
        let fwd = thrust(&f, &PilotInput { move_stick: Vector2::new(0.0, 1.0), ..Default::default() }).0;
        assert!(fwd.z > 0.0 && fwd.x == 0.0);
        let right = thrust(&f, &PilotInput { move_stick: Vector2::new(1.0, 0.0), ..Default::default() }).0;
        assert!(right.x < 0.0, "right is -X when facing +Z");
        let (none, effort) = thrust(&f, &PilotInput::default());
        assert_eq!(none, Vector3::ZERO);
        assert_eq!(effort, 0.0);
    }

    #[test]
    fn flight_assist_stops_turning_when_you_let_go() {
        let f = data();
        let mut w = Vector3::ZERO;
        let push = PilotInput { yaw: 1.0, ..Default::default() };
        for _ in 0..120 {
            w = step_turn_rate(&f, w, &push, 1.0 / 60.0);
        }
        assert!((w.y + f.yaw_rate_deg.to_radians()).abs() < 1e-4, "reaches full yaw rate");
        for _ in 0..120 {
            w = step_turn_rate(&f, w, &PilotInput::default(), 1.0 / 60.0);
        }
        assert!(w.length() < 1e-4);
        let mut no_assist = f.clone();
        no_assist.flight_assist = false;
        let spinning = Vector3::new(0.0, 1.0, 0.0);
        assert_eq!(step_turn_rate(&no_assist, spinning, &PilotInput::default(), 0.1), spinning);
    }

    #[test]
    fn match_velocity_brakes_without_overshoot() {
        let mut v = Vector3::new(30.0, -4.0, 2.0);
        let mut ticks = 0;
        while v.length() > 0.0 && ticks < 1000 {
            v += match_velocity(v, 15.0, 1.0 / 60.0);
            ticks += 1;
        }
        assert!(v.length() < 1e-4);
        // About 30.3 m/s at 15 m/s^2 is about 2 s.
        assert!((115..=125).contains(&ticks), "{ticks}");
    }

    #[test]
    fn facing_basis_is_upright_and_faces_the_hint() {
        let b = facing_basis(Vector3::new(0.0, 1.0, 0.0), Vector3::new(1.0, 0.3, 0.0));
        assert!((b.col_b() - Vector3::UP).length() < 1e-5);
        assert!((b.col_c() - Vector3::RIGHT).length() < 1e-5);
        assert!((b.determinant() - 1.0).abs() < 1e-4);
    }

    #[test]
    fn lift_rises_and_centres() {
        let a = lift_accel(Vector3::ZERO, Vector3::new(0.0, 3.0, 0.0), Vector2::new(0.3, 0.0), 14.0, 6.0);
        assert!(a.y > 0.0 && a.x < 0.0);
        assert!(a.length() <= 14.0 + 1e-4);
    }
}
