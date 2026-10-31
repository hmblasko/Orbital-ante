//! Takeoff and landing effects for a ship's main engine (CLAUDE.md section 5,
//! "Takeoff: intense like a real launch - smoke, debris, heat shimmer,
//! light, heavy takeoff rumble"): a flame plume from the bell, a billowing
//! ground cloud and flying grit when the engine is near the ground, a
//! flickering engine light, a glow on the pad and controller rumble.
//! Heat shimmer is not built yet: a screen-distortion pass drew a grey disc
//! under the software renderer and needs checking on a real GPU first.
//! The cloud takes the ground's colour and everything scales with throttle
//! and how close the ground is. All numbers live in the ship's data file
//! (`takeoff:`).
//!
//! Sound: the heavy takeoff rumble layer is a missing asset (no audio in
//! the repo yet); see the asset list in the phase report.
//!
//! Serves pillar 4 (everything is physical) and pillar 2 (goofy but
//! deadly: a launch should feel big) and fun rule 10 (game feel).

use godot::classes::cpu_particles_3d::{EmissionShape, Parameter};
use godot::classes::light_3d::Param;
use godot::classes::{CpuParticles3D, Input, Node3D, OmniLight3D, QuadMesh};
use godot::prelude::*;
use serde::Deserialize;

use crate::player::effects::{emitter, puff_mesh, ramp};

type V3 = (f32, f32, f32);

fn rgb((r, g, b): V3) -> Color {
    Color::from_rgb(r, g, b)
}

#[derive(Debug, Clone, Deserialize)]
pub struct TakeoffData {
    /// Ship-space point at the bottom of the engine bell, and the way the
    /// exhaust leaves it.
    pub nozzle_at: V3,
    pub nozzle_dir: V3,
    pub plume: PlumeData,
    pub cloud: CloudData,
    pub grit: GritData,
    pub light: LightData,
    pub rumble: RumbleData,
}

#[derive(Debug, Clone, Deserialize)]
pub struct PlumeData {
    pub rate: f32,
    pub lifetime_s: f32,
    pub speed_m_s: (f32, f32),
    pub spread_deg: f32,
    pub size_m: (f32, f32),
    pub color_hot: V3,
    pub color_mid: V3,
    pub color_cool: V3,
}

#[derive(Debug, Clone, Deserialize)]
pub struct CloudData {
    /// The ground cloud starts when the bell is this close to the ground,
    /// and is at full strength at `full_height_m`.
    pub max_height_m: f32,
    pub full_height_m: f32,
    pub rate: f32,
    pub lifetime_s: f32,
    pub speed_m_s: (f32, f32),
    pub size_m: (f32, f32),
    /// Rolls out along the ground; this much of its speed points up.
    pub lift: f32,
    /// Ground colour lifted toward white by this much.
    pub lighten: f32,
    pub opacity: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct GritData {
    pub rate: f32,
    pub lifetime_s: f32,
    pub speed_m_s: (f32, f32),
    pub size_m: (f32, f32),
    /// Darker than the ground: grit is the ground colour times this.
    pub darken: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct LightData {
    pub energy: f32,
    pub range_m: f32,
    pub color: V3,
    /// Random flicker, as a share of the energy.
    pub flicker: f32,
    /// Extra glow on the ground near the pad.
    pub ground_energy: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct RumbleData {
    /// Controller rumble (0..1 motors) at full throttle on the ground,
    /// and in open space.
    pub weak_near: f32,
    pub strong_near: f32,
    pub weak_far: f32,
    pub strong_far: f32,
}

/// What the ship tells the effects each tick.
pub struct EngineState {
    /// 0..1 main-engine throttle.
    pub throttle: f32,
    /// Bell bottom (world) and exhaust direction (world, unit).
    pub nozzle: Vector3,
    pub exhaust_dir: Vector3,
    /// Ground point straight below along the exhaust and its "up", if a
    /// body's surface is within the cloud height.
    pub ground: Option<(Vector3, Vector3)>,
    pub ground_color: Color,
    pub gravity: Vector3,
    /// True while the pilot is aboard (rumble only for the pilot).
    pub piloted: bool,
}

/// 0..1 strength of the ground effects at `height` above the ground.
pub fn ground_strength(height: f32, d: &CloudData) -> f32 {
    if height >= d.max_height_m {
        return 0.0;
    }
    let span = (d.max_height_m - d.full_height_m).max(0.01);
    (1.0 - (height - d.full_height_m) / span).clamp(0.0, 1.0)
}

pub struct TakeoffEffects {
    data: TakeoffData,
    plume: Gd<CpuParticles3D>,
    cloud: Gd<CpuParticles3D>,
    grit: Gd<CpuParticles3D>,
    light: Gd<OmniLight3D>,
    ground_light: Gd<OmniLight3D>,
    clock: f32,
    rumbling: bool,
}

impl TakeoffEffects {
    pub fn new(parent: &mut Gd<Node3D>, data: TakeoffData) -> Self {
        let p = &data.plume;
        let mut plume = emitter("EnginePlume", p.lifetime_s, (p.rate * p.lifetime_s).ceil() as i32, p.size_m, p.speed_m_s);
        plume.set_spread(p.spread_deg);
        plume.set_color_ramp(&ramp(&[(0.0, rgb(p.color_hot), 0.95), (0.3, rgb(p.color_mid), 0.75), (1.0, rgb(p.color_cool), 0.0)]));
        plume.set_mesh(&sized(puff_mesh(true, (0.6, 2.0)), p.size_m.1));
        plume.set_emitting(false);
        parent.add_child(&plume);

        let c = &data.cloud;
        let mut cloud = emitter("TakeoffCloud", c.lifetime_s, (c.rate * c.lifetime_s).ceil() as i32, c.size_m, c.speed_m_s);
        cloud.set_emission_shape(EmissionShape::SPHERE);
        cloud.set_emission_sphere_radius(c.size_m.0);
        cloud.set_spread(90.0);
        cloud.set_flatness(1.0 - c.lift.clamp(0.0, 1.0));
        cloud.set_param_min(Parameter::DAMPING, 1.5);
        cloud.set_param_max(Parameter::DAMPING, 3.0);
        cloud.set_mesh(&sized(puff_mesh(false, (0.8, 2.5)), c.size_m.1));
        cloud.set_emitting(false);
        parent.add_child(&cloud);

        let g = &data.grit;
        let mut grit = emitter("TakeoffGrit", g.lifetime_s, (g.rate * g.lifetime_s).ceil() as i32, g.size_m, g.speed_m_s);
        grit.set_emission_shape(EmissionShape::SPHERE);
        grit.set_emission_sphere_radius(1.0);
        grit.set_spread(70.0);
        grit.set_param_min(Parameter::DAMPING, 0.0);
        grit.set_param_max(Parameter::DAMPING, 0.2);
        grit.set_mesh(&sized(puff_mesh(false, (0.3, 0.8)), g.size_m.1));
        grit.set_emitting(false);
        parent.add_child(&grit);

        let l = &data.light;
        let mut light = OmniLight3D::new_alloc();
        light.set_name("EngineFlameLight");
        light.set_color(rgb(l.color));
        light.set_param(Param::ENERGY, 0.0);
        light.set_param(Param::RANGE, l.range_m);
        parent.add_child(&light);
        let mut ground_light = OmniLight3D::new_alloc();
        ground_light.set_name("PadGlow");
        ground_light.set_color(rgb(l.color));
        ground_light.set_param(Param::ENERGY, 0.0);
        ground_light.set_param(Param::RANGE, l.range_m);
        parent.add_child(&ground_light);

        Self { data, plume, cloud, grit, light, ground_light, clock: 0.0, rumbling: false }
    }

    pub fn update(&mut self, e: &EngineState, dt: f32) {
        self.clock += dt;
        let d = self.data.clone();
        let on = e.throttle > 0.02;
        let basis = Basis::IDENTITY;

        // Flame plume from the bell.
        if on {
            self.plume.set_global_transform(Transform3D::new(basis, e.nozzle));
            self.plume.set_direction(e.exhaust_dir);
            self.plume.set_gravity(Vector3::ZERO);
            let power = e.throttle.clamp(0.25, 1.0);
            self.plume.set_param_min(Parameter::INITIAL_LINEAR_VELOCITY, d.plume.speed_m_s.0 * power);
            self.plume.set_param_max(Parameter::INITIAL_LINEAR_VELOCITY, d.plume.speed_m_s.1 * power);
            self.plume.set_param_min(Parameter::SCALE, power * 0.8);
            self.plume.set_param_max(Parameter::SCALE, power * 1.2);
        }
        set_emitting(&mut self.plume, on);

        // Ground cloud and grit when the exhaust hits the ground.
        let near = e.ground.map(|(point, _)| ground_strength((point - e.nozzle).length(), &d.cloud)).unwrap_or(0.0);
        let ground_on = on && near > 0.0;
        if let (true, Some((point, up))) = (ground_on, e.ground) {
            let k = (near * e.throttle).clamp(0.0, 1.0);
            let dust = e.ground_color.lerp(Color::WHITE, d.cloud.lighten as f64);
            let fade = Color::from_rgba(dust.r, dust.g, dust.b, 0.0);
            let flat = crate::orbits::body::basis_with_up(up);
            self.cloud.set_global_transform(Transform3D::new(flat, point + up * (d.cloud.size_m.0 * 0.5)));
            // The cloud rolls out flat along the ground (its local XZ plane
            // when the emitter's up is the ground's up).
            self.cloud.set_direction(Vector3::new(0.0, d.cloud.lift, 0.0));
            self.cloud.set_gravity(Vector3::ZERO);
            // Thinner with height and lower throttle.
            let alpha = d.cloud.opacity * k.sqrt();
            self.cloud.set_color_ramp(&ramp(&[(0.0, dust, alpha * 0.6), (0.2, dust, alpha), (1.0, fade, 0.0)]));
            self.cloud.set_param_min(Parameter::INITIAL_LINEAR_VELOCITY, d.cloud.speed_m_s.0 * (0.5 + 0.5 * k));
            self.cloud.set_param_max(Parameter::INITIAL_LINEAR_VELOCITY, d.cloud.speed_m_s.1 * (0.5 + 0.5 * k));

            let grit_col = Color::from_rgb(e.ground_color.r * d.grit.darken, e.ground_color.g * d.grit.darken, e.ground_color.b * d.grit.darken);
            self.grit.set_global_transform(Transform3D::new(Basis::IDENTITY, point + up * 0.3));
            self.grit.set_direction(up);
            self.grit.set_gravity(e.gravity);
            self.grit.set_color_ramp(&ramp(&[(0.0, grit_col, 1.0), (0.8, grit_col, 1.0), (1.0, grit_col, 0.0)]));
            self.grit.set_param_min(Parameter::SCALE, 0.6 * k.sqrt());
            self.grit.set_param_max(Parameter::SCALE, 1.2 * k.sqrt());

            let mut glow = self.ground_light.clone();
            glow.set_global_position(point + up * 1.0);
            glow.set_param(Param::ENERGY, d.light.ground_energy * k);
        } else {
            self.ground_light.set_param(Param::ENERGY, 0.0);
        }
        set_emitting(&mut self.cloud, ground_on);
        set_emitting(&mut self.grit, ground_on);

        // Flickering flame light.
        let flicker = 1.0 + d.light.flicker * ((self.clock * 37.0).sin() * 0.6 + (self.clock * 91.0).sin() * 0.4);
        self.light.set_global_position(e.nozzle + e.exhaust_dir * 1.5);
        self.light.set_param(Param::ENERGY, d.light.energy * e.throttle * flicker);

        // Rumble for the pilot, heavier with the ground close.
        let mut input = Input::singleton();
        if e.piloted && on {
            let weak = d.rumble.weak_far + (d.rumble.weak_near - d.rumble.weak_far) * near;
            let strong = d.rumble.strong_far + (d.rumble.strong_near - d.rumble.strong_far) * near;
            input.start_joy_vibration(0, weak * e.throttle, strong * e.throttle);
            self.rumbling = true;
        } else if self.rumbling {
            input.stop_joy_vibration(0);
            self.rumbling = false;
        }
    }

    /// Particles currently alive across the takeoff emitters (tests).
    pub fn emitting(&self) -> (bool, bool, bool) {
        (self.plume.is_emitting(), self.cloud.is_emitting(), self.grit.is_emitting())
    }
}

fn set_emitting(p: &mut Gd<CpuParticles3D>, on: bool) {
    if p.is_emitting() != on {
        p.set_emitting(on);
    }
}

fn sized(mesh: Gd<QuadMesh>, size: f32) -> Gd<QuadMesh> {
    let mut m = mesh;
    m.set_size(Vector2::new(size, size));
    m
}

#[cfg(test)]
mod tests {
    use super::*;

    fn cloud() -> CloudData {
        let ship: crate::flight::ship::ShipData =
            crate::data::parse(include_str!("../../../../godot/data/ships/starter_lander.ron")).unwrap();
        ship.takeoff.cloud
    }

    #[test]
    fn ground_cloud_fades_with_height() {
        let c = cloud();
        assert_eq!(ground_strength(c.max_height_m + 1.0, &c), 0.0);
        assert_eq!(ground_strength(c.full_height_m * 0.5, &c), 1.0);
        let mid = ground_strength((c.max_height_m + c.full_height_m) * 0.5, &c);
        assert!(mid > 0.4 && mid < 0.6, "mid {mid}");
    }
}
