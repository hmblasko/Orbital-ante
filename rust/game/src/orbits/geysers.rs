//! CO2 geysers on Mars's south polar cap (Mars kit, field guide: spring
//! jets that blast dark dust up through the ice). Each vent runs a seeded
//! cycle: dormant, a visible warning (frost puffs hissing from the vent),
//! then an eruption column that throws anyone standing on it high into the
//! air.
//!
//! Serves pillar 4 (everything is physical: the player is thrown by the
//! same physics as everything else) and fun rule 8 (fair failure: every
//! eruption has a warning first) and rule 11 (emergent stories: geyser
//! launches, rover flips).
//!
//! The audible part of the warning waits for the Phase 9 sound pass (no
//! sound assets exist yet); the warning is visual for now.

use godot::classes::cpu_particles_3d::{EmissionShape, Parameter};
use godot::classes::{CharacterBody3D, CpuParticles3D, INode3D, Node3D};
use godot::prelude::*;
use serde::Deserialize;

use super::body::{CelestialBody, basis_with_up};
use super::system::SYSTEM_GROUP;
use crate::player::controller::PLAYER_GROUP;
use crate::procgen::landforms::LandformKind;
use crate::seed::SeededRng;

#[derive(Debug, Clone, Deserialize)]
pub struct GeyserData {
    pub count: u32,
    /// Vents sit in a ring around the first landform of this kind...
    pub around: LandformKind,
    /// ...between these distances from its centre, in multiples of its radius.
    pub ring: (f32, f32),
    /// Seconds between eruptions, per vent.
    pub cycle_s: (f32, f32),
    /// Warning (frost puffs) before each eruption, seconds.
    pub warn_s: f32,
    pub erupt_s: f32,
    /// Anyone within this distance of the vent is thrown.
    pub radius_m: f32,
    /// Upward speed given to a thrown player, m/s.
    pub throw_m_s: f32,
    pub column_height_m: f32,
    pub dust_color: (f32, f32, f32),
    pub frost_color: (f32, f32, f32),
    /// Vents only animate when the camera is this close, metres.
    pub show_within_m: f32,
}

#[derive(Debug, Clone, Copy, PartialEq)]
pub enum Phase {
    Dormant,
    Warning,
    Erupting,
}

/// One vent: where it is (unit direction in body space) and its timing.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct Vent {
    pub dir: Vector3,
    pub period: f64,
    pub offset: f64,
}

impl Vent {
    pub fn phase(&self, data: &GeyserData, time: f64) -> Phase {
        let t = (time + self.offset).rem_euclid(self.period);
        if t < data.warn_s as f64 {
            Phase::Warning
        } else if t < (data.warn_s + data.erupt_s) as f64 {
            Phase::Erupting
        } else {
            Phase::Dormant
        }
    }

    /// Seconds until the next warning starts (0 while warning or erupting).
    pub fn next_warning_in(&self, data: &GeyserData, time: f64) -> f64 {
        match self.phase(data, time) {
            Phase::Dormant => self.period - (time + self.offset).rem_euclid(self.period),
            _ => 0.0,
        }
    }
}

/// Place the vents for one run: seeded, in a ring around the host landform.
pub fn vents_for(data: &GeyserData, host_center: Vector3, host_angle: f32, run_seed: u64) -> Vec<Vent> {
    let mut rng = SeededRng::derive(run_seed, "geysers");
    let c = host_center.normalized();
    let side = if c.x.abs() < 0.9 { Vector3::RIGHT } else { Vector3::FORWARD };
    let e1 = c.cross(side).normalized();
    let e2 = c.cross(e1);
    (0..data.count)
        .map(|_| {
            let a = rng.range_f32(0.0, std::f32::consts::TAU);
            let r = host_angle * rng.range_f32(data.ring.0, data.ring.1.max(data.ring.0));
            let tangent = e1 * a.cos() + e2 * a.sin();
            let dir = (c * r.cos() + tangent * r.sin()).normalized();
            let period = rng.range_f32(data.cycle_s.0, data.cycle_s.1.max(data.cycle_s.0)) as f64;
            let offset = rng.range_f32(0.0, period as f32) as f64;
            Vent { dir, period: period.max((data.warn_s + data.erupt_s) as f64 + 1.0), offset }
        })
        .collect()
}

/// The vents of one body, as child nodes that animate and throw.
#[derive(GodotClass)]
#[class(base = Node3D, init)]
pub struct Geysers {
    data: Option<GeyserData>,
    vents: Vec<Vent>,
    nodes: Vec<(Gd<CpuParticles3D>, Gd<CpuParticles3D>)>,
    /// Which vents already threw the player this eruption.
    thrown: Vec<bool>,
    /// Tests and renders: use this time instead of the solar system clock.
    debug_time: Option<f64>,
    base: Base<Node3D>,
}

#[godot_api]
impl INode3D for Geysers {
    fn physics_process(&mut self, _delta: f64) {
        let Some(data) = self.data.clone() else { return };
        let tree = self.base().get_tree();
        let clock = self
            .debug_time
            .unwrap_or_else(|| tree.get_first_node_in_group(SYSTEM_GROUP).map_or(0.0, |mut s| s.call("get_clock", &[]).to::<f64>()));
        let eye = self.base().get_viewport().and_then(|v| v.get_camera_3d()).map(|c| c.get_global_position());
        let player = tree.get_first_node_in_group(PLAYER_GROUP).and_then(|p| p.try_cast::<CharacterBody3D>().ok());
        for i in 0..self.vents.len() {
            let phase = self.vents[i].phase(&data, clock);
            let (mut frost, mut column) = self.nodes[i].clone();
            let at = column.get_global_position();
            let near = eye.is_some_and(|e| e.distance_to(at) < data.show_within_m);
            frost.set_emitting(near && phase == Phase::Warning);
            column.set_emitting(near && phase == Phase::Erupting);
            if phase != Phase::Erupting {
                self.thrown[i] = false;
                continue;
            }
            let Some(mut p) = player.clone() else { continue };
            let up = column.get_global_basis().col_b().normalized();
            let rel = p.get_global_position() - at;
            let height = rel.dot(up);
            let flat = (rel - up * height).length();
            if !self.thrown[i] && flat < data.radius_m && height > -1.0 && height < 3.0 {
                let v = p.get_velocity();
                p.set_velocity(v + up * data.throw_m_s);
                self.thrown[i] = true;
            }
        }
    }
}

#[godot_api]
impl Geysers {
    /// Vent positions (world), for tests.
    #[func]
    pub fn get_vent_positions(&self) -> PackedVector3Array {
        self.nodes.iter().map(|(_, c)| c.get_global_position()).collect()
    }

    /// 0 dormant, 1 warning, 2 erupting at time `t`, for tests.
    #[func]
    pub fn get_phase_at(&self, i: i32, t: f64) -> i32 {
        let (Some(d), Some(v)) = (self.data.as_ref(), self.vents.get(i as usize)) else { return 0 };
        match v.phase(d, t) {
            Phase::Dormant => 0,
            Phase::Warning => 1,
            Phase::Erupting => 2,
        }
    }

    /// Start time of vent `i`'s next eruption after `t` (tests).
    #[func]
    pub fn get_next_eruption_after(&self, i: i32, t: f64) -> f64 {
        let (Some(d), Some(v)) = (self.data.as_ref(), self.vents.get(i as usize)) else { return -1.0 };
        let into = (t + v.offset).rem_euclid(v.period);
        let start = t - into + d.warn_s as f64;
        if start > t { start } else { start + v.period }
    }

    /// Tests and renders: freeze the vents at time `t` (negative = clock).
    #[func]
    pub fn debug_set_time(&mut self, t: f64) {
        self.debug_time = (t >= 0.0).then_some(t);
    }

    #[func]
    pub fn get_throw_radius(&self) -> f32 {
        self.data.as_ref().map_or(0.0, |d| d.radius_m)
    }
}

impl Geysers {
    /// Build the vents on `body` (call once its shape is ready).
    pub fn build(body: &CelestialBody, data: &GeyserData, run_seed: u64) -> Option<Gd<Geysers>> {
        let shape = body.shape()?;
        let host = shape.landforms().list.iter().find(|l| l.kind == data.around)?;
        let vents = vents_for(data, host.center, host.angle, run_seed);
        let mut node = Geysers::new_alloc();
        node.set_name("Geysers");
        let mut nodes = Vec::new();
        for (i, v) in vents.iter().enumerate() {
            let ground = v.dir * shape.surface_radius(v.dir);
            let mut frost = puff(data.frost_color, 0.6, 3.0, 2.0, 40);
            let mut column = puff(data.dust_color, 3.0, data.column_height_m / 2.4, 2.4, 420);
            for p in [&mut frost, &mut column] {
                p.set_position(ground);
                p.set_basis(basis_with_up(v.dir));
                p.set_name(&format!("Vent{i}"));
            }
            frost.set_name(&format!("Vent{i}Frost"));
            node.add_child(&frost);
            node.add_child(&column);
            nodes.push((frost, column));
        }
        {
            let mut g = node.bind_mut();
            g.thrown = vec![false; vents.len()];
            g.vents = vents;
            g.data = Some(data.clone());
            g.nodes = nodes;
        }
        Some(node)
    }
}

/// A plume of billboard puffs rising from the vent.
fn puff(color: (f32, f32, f32), size: f32, speed: f32, lifetime: f32, amount: i32) -> Gd<CpuParticles3D> {
    let mut p = CpuParticles3D::new_alloc();
    p.set_amount(amount);
    p.set_lifetime(lifetime as f64);
    p.set_emitting(false);
    p.set_emission_shape(EmissionShape::SPHERE);
    p.set_emission_sphere_radius(0.4);
    p.set_direction(Vector3::UP);
    p.set_spread(8.0);
    p.set_gravity(Vector3::ZERO);
    p.set_param_min(Parameter::INITIAL_LINEAR_VELOCITY, speed * 0.8);
    p.set_param_max(Parameter::INITIAL_LINEAR_VELOCITY, speed * 1.2);
    p.set_param_min(Parameter::SCALE, 0.7);
    p.set_param_max(Parameter::SCALE, 1.8);
    p.set_param_min(Parameter::DAMPING, 0.2);
    p.set_param_max(Parameter::DAMPING, 0.6);
    let mut quad = crate::player::effects::puff_mesh(false, (1.0, 4.0));
    quad.set_size(Vector2::new(size, size));
    p.set_mesh(&quad);
    p.set_color(Color::from_rgba(color.0, color.1, color.2, 0.75));
    p
}

#[cfg(test)]
mod tests {
    use super::*;

    fn data() -> GeyserData {
        GeyserData {
            count: 6,
            around: LandformKind::PolarCap,
            ring: (0.6, 1.1),
            cycle_s: (40.0, 70.0),
            warn_s: 4.0,
            erupt_s: 5.0,
            radius_m: 3.0,
            throw_m_s: 14.0,
            column_height_m: 35.0,
            dust_color: (0.2, 0.15, 0.12),
            frost_color: (0.9, 0.92, 0.96),
            show_within_m: 400.0,
        }
    }

    #[test]
    fn determinism_vents_are_seeded() {
        let d = data();
        let c = Vector3::new(0.0, -1.0, 0.0);
        assert_eq!(vents_for(&d, c, 0.2, 9), vents_for(&d, c, 0.2, 9));
        assert_ne!(vents_for(&d, c, 0.2, 9), vents_for(&d, c, 0.2, 10));
    }

    #[test]
    fn vents_ring_the_cap() {
        let d = data();
        let c = Vector3::new(0.0, -1.0, 0.0);
        for v in vents_for(&d, c, 0.2, 3) {
            let a = v.dir.angle_to(c);
            assert!(a >= 0.2 * 0.6 - 1e-4 && a <= 0.2 * 1.1 + 1e-4, "vent at {a} rad");
        }
    }

    #[test]
    fn every_eruption_is_warned() {
        // Fair failure: scanning time finely, an eruption always follows a
        // warning of the full warn_s length.
        let d = data();
        for v in vents_for(&d, Vector3::UP, 0.2, 5) {
            let mut last = v.phase(&d, -0.05);
            let mut warned_for = d.warn_s as f64;
            let mut t = 0.0;
            while t < 400.0 {
                let p = v.phase(&d, t);
                if p == Phase::Warning {
                    warned_for += 0.05;
                }
                if p == Phase::Erupting && last == Phase::Warning {
                    assert!(warned_for >= d.warn_s as f64 - 0.11, "warned only {warned_for} s");
                }
                if p == Phase::Dormant {
                    warned_for = 0.0;
                }
                if p == Phase::Erupting {
                    assert_ne!(last, Phase::Dormant, "erupted with no warning at {t}");
                }
                last = p;
                t += 0.05;
            }
        }
    }
}
