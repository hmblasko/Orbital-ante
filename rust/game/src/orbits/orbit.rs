//! Orbit and spin maths for the clockwork solar system (CLAUDE.md section 4:
//! "planets orbit the sun, moons orbit planets ... every body rotates").
//!
//! Serves pillar 1 (a living clockwork solar system) and fun rule 11
//! (emergent stories: everything keeps moving whether you watch or not).
//!
//! Positions are a pure function of the run clock, so the same seed and the
//! same time always give the same sky (determinism tests), and nothing drifts
//! from accumulated integration error. Maths is done in f64: the system is
//! tens of kilometres across, and the floating origin subtracts in f64 too.

use serde::Deserialize;

use crate::seed::SeededRng;

/// A 3D vector in f64, for positions before the floating origin is applied.
#[derive(Debug, Clone, Copy, PartialEq, Default)]
pub struct DVec3 {
    pub x: f64,
    pub y: f64,
    pub z: f64,
}

impl DVec3 {
    pub const ZERO: Self = Self { x: 0.0, y: 0.0, z: 0.0 };

    pub fn new(x: f64, y: f64, z: f64) -> Self {
        Self { x, y, z }
    }

    pub fn length(self) -> f64 {
        (self.x * self.x + self.y * self.y + self.z * self.z).sqrt()
    }

    pub fn scale(self, s: f64) -> Self {
        Self::new(self.x * s, self.y * s, self.z * s)
    }

    pub fn to_f32(self) -> godot::builtin::Vector3 {
        godot::builtin::Vector3::new(self.x as f32, self.y as f32, self.z as f32)
    }

    pub fn from_f32(v: godot::builtin::Vector3) -> Self {
        Self::new(v.x as f64, v.y as f64, v.z as f64)
    }
}

impl std::ops::Add for DVec3 {
    type Output = Self;
    fn add(self, o: Self) -> Self {
        Self::new(self.x + o.x, self.y + o.y, self.z + o.z)
    }
}

impl std::ops::Sub for DVec3 {
    type Output = Self;
    fn sub(self, o: Self) -> Self {
        Self::new(self.x - o.x, self.y - o.y, self.z - o.z)
    }
}

/// A circular orbit around a parent (the sun, or a planet for moons).
#[derive(Debug, Clone, Deserialize)]
pub struct OrbitData {
    /// Distance from the parent's centre, in metres.
    pub radius_m: f64,
    /// Seconds for one full lap.
    pub period_s: f64,
    /// Tilt of the orbit plane from the system plane (XZ), in degrees.
    #[serde(default)]
    pub inclination_deg: f64,
    /// Where the tilted plane crosses the system plane, in degrees.
    #[serde(default)]
    pub node_deg: f64,
}

/// A body's spin about its own axis.
#[derive(Debug, Clone, Deserialize, Default)]
pub struct SpinData {
    /// Seconds per turn; 0 = no spin. Negative spins backwards (like Venus).
    pub period_s: f64,
    /// Tilt of the spin axis from the system's up (+Y), in degrees.
    #[serde(default)]
    pub axial_tilt_deg: f64,
}

/// Position on an orbit (relative to the parent) at time `t`, with the
/// starting angle `phase` in radians.
pub fn orbit_offset(o: &OrbitData, phase: f64, t: f64) -> DVec3 {
    let angle = phase + if o.period_s > 0.0 { std::f64::consts::TAU * t / o.period_s } else { 0.0 };
    // Circle in the XZ plane (counter-clockwise seen from above), then tilt it
    // about the line of nodes.
    let (s, c) = angle.sin_cos();
    let flat = DVec3::new(c * o.radius_m, 0.0, -s * o.radius_m);
    let inc = o.inclination_deg.to_radians();
    let node = o.node_deg.to_radians();
    // Rotate about X by the inclination, then about Y by the node angle.
    let tilted = DVec3::new(flat.x, -flat.z * inc.sin(), flat.z * inc.cos());
    let (ns, nc) = node.sin_cos();
    DVec3::new(tilted.x * nc + tilted.z * ns, tilted.y, -tilted.x * ns + tilted.z * nc)
}

/// Orbital speed in m/s (circular orbit).
pub fn orbit_speed(o: &OrbitData) -> f64 {
    if o.period_s > 0.0 { std::f64::consts::TAU * o.radius_m / o.period_s } else { 0.0 }
}

/// Spin angle (radians) about the body's axis at time `t`.
pub fn spin_angle(s: &SpinData, phase: f64, t: f64) -> f64 {
    if s.period_s == 0.0 {
        phase
    } else {
        phase + std::f64::consts::TAU * t / s.period_s
    }
}

/// Seeded starting angles for an orbit and a spin (section 4: "each body's
/// starting position along its orbit" is seeded; the orbits themselves are fixed).
pub fn start_phases(run_seed: u64, body_id: &str) -> (f64, f64) {
    let mut rng = SeededRng::derive(run_seed, &format!("orbits/{body_id}"));
    (rng.next_f64() * std::f64::consts::TAU, rng.next_f64() * std::f64::consts::TAU)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn orbit() -> OrbitData {
        OrbitData { radius_m: 6000.0, period_s: 2400.0, inclination_deg: 3.0, node_deg: 40.0 }
    }

    #[test]
    fn orbit_keeps_its_radius_and_closes_after_one_period() {
        let o = orbit();
        for t in [0.0, 100.0, 777.0, 2399.0] {
            assert!((orbit_offset(&o, 0.4, t).length() - 6000.0).abs() < 1e-6);
        }
        let a = orbit_offset(&o, 0.4, 10.0);
        let b = orbit_offset(&o, 0.4, 10.0 + o.period_s);
        assert!((a - b).length() < 1e-6);
    }

    #[test]
    fn orbit_speed_matches_motion() {
        let o = orbit();
        let a = orbit_offset(&o, 1.0, 50.0);
        let b = orbit_offset(&o, 1.0, 50.001);
        let speed = (b - a).length() / 0.001;
        assert!((speed - orbit_speed(&o)).abs() < 0.01, "{speed}");
    }

    #[test]
    fn inclination_lifts_the_orbit_out_of_the_plane() {
        let flat = OrbitData { inclination_deg: 0.0, ..orbit() };
        let tilted = OrbitData { inclination_deg: 30.0, ..orbit() };
        let max_y = |o: &OrbitData| {
            (0..100).map(|i| orbit_offset(o, 0.0, i as f64 * 24.0).y.abs()).fold(0.0, f64::max)
        };
        assert!(max_y(&flat) < 1e-9);
        assert!((max_y(&tilted) - 3000.0).abs() < 20.0, "{}", max_y(&tilted));
    }

    #[test]
    fn determinism_orbit_phases_are_seeded() {
        assert_eq!(start_phases(42, "earth"), start_phases(42, "earth"));
        assert_ne!(start_phases(42, "earth"), start_phases(43, "earth"));
        assert_ne!(start_phases(42, "earth"), start_phases(42, "mars"));
    }

    #[test]
    fn spin_turns_once_per_period_and_can_run_backwards() {
        let s = SpinData { period_s: 600.0, axial_tilt_deg: 0.0 };
        assert!((spin_angle(&s, 0.0, 600.0) - std::f64::consts::TAU).abs() < 1e-9);
        let back = SpinData { period_s: -600.0, axial_tilt_deg: 0.0 };
        assert!(spin_angle(&back, 0.0, 300.0) < 0.0);
    }
}
