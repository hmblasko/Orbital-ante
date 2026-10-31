//! Movement polish: a jetpack exhaust plume, footstep puffs and a landing
//! burst with a small camera dip (CLAUDE.md section 10). Dust takes the
//! colour of the ground under you and, in vacuum, flies out on ballistic
//! arcs under the body's own gravity instead of billowing.
//!
//! Serves pillar 4 (everything is physical) and fun rule 10 (game feel).

use godot::classes::base_material_3d::{BillboardMode, DistanceFadeMode, TextureParam, ShadingMode, Transparency};
use godot::classes::cpu_particles_3d::{DrawOrder, EmissionShape, Parameter};
use godot::classes::gradient_texture_2d::Fill;
use godot::classes::{CpuParticles3D, Gradient, GradientTexture2D, Node3D, QuadMesh, StandardMaterial3D};
use godot::prelude::*;
use serde::Deserialize;

#[derive(Debug, Clone, Deserialize)]
pub struct EffectsData {
    pub exhaust: ExhaustData,
    /// 0..1: how far dust is lifted from the ground colour toward white.
    pub dust_lighten: f32,
    pub footsteps: FootstepData,
    pub landing: LandingData,
}

#[derive(Debug, Clone, Deserialize)]
pub struct ExhaustData {
    pub rate: f32,
    pub lifetime_s: f32,
    pub speed_m_s: (f32, f32),
    pub spread_deg: f32,
    pub size_m: (f32, f32),
    /// How far behind the push the plume starts, in metres.
    pub nozzle_distance_m: f32,
    /// Puffs fade out closer to the camera than this, so the plume never fills the view.
    pub camera_fade_m: (f32, f32),
    pub color_hot: (f32, f32, f32),
    pub color_mid: (f32, f32, f32),
    pub color_cool: (f32, f32, f32),
}

#[derive(Debug, Clone, Deserialize)]
pub struct FootstepData {
    pub stride_m: f32,
    pub crouch_stride_m: f32,
    pub puffs: u32,
    pub lifetime_s: f32,
    pub speed_m_s: (f32, f32),
    pub size_m: (f32, f32),
    pub crouch_scale: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct LandingData {
    pub min_speed_m_s: f32,
    pub puffs: u32,
    pub hard_speed_m_s: f32,
    pub lifetime_s: f32,
    pub speed_m_s: (f32, f32),
    pub size_m: (f32, f32),
    pub camera_dip_m: f32,
    pub dip_recover_s: f32,
}

/// What the controller tells the effects each physics tick.
pub struct EffectsInput {
    pub position: Vector3,
    /// Bottom of the capsule.
    pub feet: Vector3,
    pub up: Vector3,
    pub gravity: Vector3,
    pub on_floor: bool,
    pub crouching: bool,
    /// Speed along the ground this tick, m/s.
    pub ground_speed: f32,
    /// Speed into the ground at the moment of landing, m/s.
    pub fall_speed: f32,
    /// Jetpack thrust 0..1 and its direction (the plume points the other way).
    pub thrust: f32,
    pub thrust_dir: Vector3,
    pub dust_color: Color,
}

/// Distance walked since the last footstep puff; returns true when a step lands.
pub fn stride_step(walked: &mut f32, distance: f32, stride: f32) -> bool {
    *walked += distance;
    if stride > 0.0 && *walked >= stride {
        *walked -= stride;
        return true;
    }
    false
}

/// 0..1 strength of a landing at `speed` (0 below the threshold).
pub fn landing_strength(speed: f32, d: &LandingData) -> f32 {
    if speed < d.min_speed_m_s {
        return 0.0;
    }
    ((speed - d.min_speed_m_s) / (d.hard_speed_m_s - d.min_speed_m_s).max(0.01)).clamp(0.15, 1.0)
}

pub struct SuitEffects {
    data: EffectsData,
    exhaust: Gd<CpuParticles3D>,
    steps: Vec<Gd<CpuParticles3D>>,
    next_step: usize,
    landing: Gd<CpuParticles3D>,
    walked: f32,
    was_on_floor: bool,
    /// Current camera dip in metres, recovering toward 0.
    dip: f32,
}

impl SuitEffects {
    pub fn new(parent: &mut Gd<Node3D>, data: EffectsData) -> Self {
        let e = &data.exhaust;
        let mut exhaust = emitter("JetpackExhaust", e.lifetime_s, (e.rate * e.lifetime_s).ceil() as i32, e.size_m, e.speed_m_s);
        exhaust.set_spread(e.spread_deg);
        exhaust.set_color_ramp(&ramp(&[(0.0, rgb(e.color_hot), 0.95), (0.25, rgb(e.color_mid), 0.7), (1.0, rgb(e.color_cool), 0.0)]));
        exhaust.set_emitting(false);
        // Hot gas glows: draw it unshaded so it reads in shadow.
        exhaust.set_mesh(&puff_mesh(true, e.camera_fade_m));
        parent.add_child(&exhaust);

        let f = &data.footsteps;
        let steps = (0..4)
            .map(|i| {
                let mut p = emitter(&format!("FootstepDust{i}"), f.lifetime_s, f.puffs as i32, f.size_m, f.speed_m_s);
                p.set_one_shot(true);
                p.set_explosiveness_ratio(1.0);
                p.set_spread(70.0);
                p.set_emission_shape(EmissionShape::SPHERE);
                p.set_emission_sphere_radius(0.2);
                parent.add_child(&p);
                p
            })
            .collect();
        let l = &data.landing;
        let mut landing = emitter("LandingDust", l.lifetime_s, l.puffs as i32, l.size_m, l.speed_m_s);
        landing.set_one_shot(true);
        landing.set_explosiveness_ratio(1.0);
        // A flat ring kicked out sideways.
        landing.set_spread(85.0);
        landing.set_flatness(0.8);
        landing.set_emission_shape(EmissionShape::SPHERE);
        landing.set_emission_sphere_radius(0.45);
        parent.add_child(&landing);

        Self { data, exhaust, steps, next_step: 0, landing, walked: 0.0, was_on_floor: true, dip: 0.0 }
    }

    /// Camera offset (metres, along the player's down) from a landing dip.
    pub fn camera_dip(&self) -> f32 {
        self.dip
    }

    pub fn update(&mut self, i: &EffectsInput, dt: f32) {
        self.update_exhaust(i);
        let feet = i.feet;
        if i.on_floor && !self.was_on_floor {
            self.land(i, feet);
        } else if i.on_floor {
            let stride = if i.crouching { self.data.footsteps.crouch_stride_m } else { self.data.footsteps.stride_m };
            if i.ground_speed > 0.5 && stride_step(&mut self.walked, i.ground_speed * dt, stride) {
                self.step(i, feet);
            }
        }
        self.was_on_floor = i.on_floor;
        let recover = self.data.landing.dip_recover_s.max(0.01);
        self.dip = (self.dip - self.data.landing.camera_dip_m * dt / recover).max(0.0);
    }

    fn update_exhaust(&mut self, i: &EffectsInput) {
        let firing = i.thrust > 0.01 && i.thrust_dir.length_squared() > 0.5;
        if firing {
            // Plume shoots opposite the push, from the side the thrusters fire.
            let plume = -i.thrust_dir.normalized();
            let nozzle = i.position + plume * self.data.exhaust.nozzle_distance_m;
            self.exhaust.set_global_transform(Transform3D::new(Basis::IDENTITY, nozzle));
            self.exhaust.set_direction(plume);
            self.exhaust.set_gravity(Vector3::ZERO);
            // Gentle feathering makes a thinner, slower plume.
            let power = i.thrust.clamp(0.3, 1.0);
            let (lo, hi) = self.data.exhaust.speed_m_s;
            self.exhaust.set_param_min(Parameter::INITIAL_LINEAR_VELOCITY, lo * power);
            self.exhaust.set_param_max(Parameter::INITIAL_LINEAR_VELOCITY, hi * power);
            self.exhaust.set_param_min(Parameter::SCALE, power * 0.8);
            self.exhaust.set_param_max(Parameter::SCALE, power * 1.2);
        }
        if self.exhaust.is_emitting() != firing {
            self.exhaust.set_emitting(firing);
        }
    }

    fn dust(&self, i: &EffectsInput) -> Color {
        i.dust_color.lerp(Color::WHITE, self.data.dust_lighten as f64)
    }

    fn step(&mut self, i: &EffectsInput, feet: Vector3) {
        let scale = if i.crouching { self.data.footsteps.crouch_scale } else { 1.0 };
        let idx = self.next_step;
        self.next_step = (self.next_step + 1) % self.steps.len();
        let dust = self.dust(i);
        burst(&mut self.steps[idx], feet, i.up, i.gravity, dust, scale);
    }

    fn land(&mut self, i: &EffectsInput, feet: Vector3) {
        let strength = landing_strength(i.fall_speed, &self.data.landing);
        if strength <= 0.0 {
            return;
        }
        self.walked = 0.0;
        self.dip = self.dip.max(self.data.landing.camera_dip_m * strength);
        let dust = self.dust(i);
        burst(&mut self.landing, feet, i.up, i.gravity, dust, strength.sqrt());
    }
}

fn burst(p: &mut Gd<CpuParticles3D>, at: Vector3, up: Vector3, gravity: Vector3, color: Color, scale: f32) {
    p.set_global_transform(Transform3D::new(Basis::IDENTITY, at + up * 0.1));
    p.set_direction(up);
    // Vacuum: no air to hold dust up, so it arcs back down under the body's pull.
    p.set_gravity(gravity);
    p.set_param_min(Parameter::SCALE, scale * 0.8);
    p.set_param_max(Parameter::SCALE, scale * 1.2);
    let fade = Color::from_rgba(color.r, color.g, color.b, 0.0);
    p.set_color_ramp(&ramp(&[(0.0, color, 0.95), (0.5, color, 0.6), (1.0, fade, 0.0)]));
    p.restart();
    p.set_emitting(true);
}

pub(crate) fn emitter(name: &str, lifetime: f32, amount: i32, size: (f32, f32), speed: (f32, f32)) -> Gd<CpuParticles3D> {
    let mut p = CpuParticles3D::new_alloc();
    p.set_name(name);
    p.set_amount(amount.max(1));
    p.set_lifetime(lifetime as f64);
    // World space: puffs stay where they were made as you move on.
    p.set_use_local_coordinates(false);
    p.set_mesh(&puff_mesh(false, (0.2, 0.6)));
    p.set_draw_order(DrawOrder::VIEW_DEPTH);
    p.set_param_min(Parameter::INITIAL_LINEAR_VELOCITY, speed.0);
    p.set_param_max(Parameter::INITIAL_LINEAR_VELOCITY, speed.1);
    p.set_param_min(Parameter::DAMPING, 0.5);
    p.set_param_max(Parameter::DAMPING, 1.5);
    // Puffs grow as they spread and thin out.
    p.set_param_min(Parameter::SCALE, 1.0);
    p.set_param_max(Parameter::SCALE, 1.0);
    let mut grow = godot::classes::Curve::new_gd();
    grow.add_point(Vector2::new(0.0, size.0 / size.1.max(0.001)));
    grow.add_point(Vector2::new(1.0, 1.0));
    p.set_param_curve(Parameter::SCALE, &grow);
    p.set_param_min(Parameter::ANGLE, -180.0);
    p.set_param_max(Parameter::ANGLE, 180.0);
    if let Some(mut mesh) = p.get_mesh().and_then(|m| m.try_cast::<QuadMesh>().ok()) {
        mesh.set_size(Vector2::new(size.1, size.1));
    }
    p
}

/// A soft round billboard puff; vertex colour drives tint and fade.
pub(crate) fn puff_mesh(glow: bool, camera_fade_m: (f32, f32)) -> Gd<QuadMesh> {
    let mut gradient = Gradient::new_gd();
    gradient.set_color(0, Color::from_rgba(1.0, 1.0, 1.0, 1.0));
    gradient.set_color(1, Color::from_rgba(1.0, 1.0, 1.0, 0.0));
    gradient.add_point(0.5, Color::from_rgba(1.0, 1.0, 1.0, 0.8));
    let mut texture = GradientTexture2D::new_gd();
    texture.set_gradient(&gradient);
    texture.set_fill(Fill::RADIAL);
    texture.set_fill_from(Vector2::new(0.5, 0.5));
    texture.set_fill_to(Vector2::new(1.0, 0.5));
    texture.set_width(64);
    texture.set_height(64);

    let mut material = StandardMaterial3D::new_gd();
    material.set_texture(TextureParam::ALBEDO, &texture);
    material.set_transparency(Transparency::ALPHA);
    material.set_billboard_mode(BillboardMode::PARTICLES);
    material.set_flag(godot::classes::base_material_3d::Flags::ALBEDO_FROM_VERTEX_COLOR, true);
    material.set_shading_mode(if glow { ShadingMode::UNSHADED } else { ShadingMode::PER_VERTEX });
    // Soft particles: fade where a puff meets the ground, so no hard edge.
    material.set_proximity_fade_enabled(true);
    material.set_proximity_fade_distance(0.35);
    material.set_distance_fade(DistanceFadeMode::PIXEL_ALPHA);
    material.set_distance_fade_min_distance(camera_fade_m.0);
    material.set_distance_fade_max_distance(camera_fade_m.1);
    let mut mesh = QuadMesh::new_gd();
    mesh.set_size(Vector2::new(0.3, 0.3));
    mesh.set_material(&material);
    mesh
}

pub(crate) fn ramp(stops: &[(f32, Color, f32)]) -> Gd<Gradient> {
    let mut g = Gradient::new_gd();
    let offsets: PackedFloat32Array = stops.iter().map(|s| s.0).collect();
    let colors: PackedColorArray = stops.iter().map(|(_, c, a)| Color::from_rgba(c.r, c.g, c.b, *a)).collect();
    g.set_offsets(&offsets);
    g.set_colors(&colors);
    g
}

fn rgb((r, g, b): (f32, f32, f32)) -> Color {
    Color::from_rgb(r, g, b)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn landing() -> LandingData {
        LandingData { min_speed_m_s: 3.0, puffs: 20, hard_speed_m_s: 14.0, lifetime_s: 1.0, speed_m_s: (1.0, 2.0), size_m: (0.1, 0.3), camera_dip_m: 0.2, dip_recover_s: 0.25 }
    }

    #[test]
    fn a_puff_every_stride() {
        let mut walked = 0.0;
        let steps = (0..100).filter(|_| stride_step(&mut walked, 0.1, 1.6)).count();
        assert_eq!(steps, 6, "10 m walked at 1.6 m strides");
    }

    #[test]
    fn soft_landings_make_no_puff_and_hard_ones_cap() {
        let d = landing();
        assert_eq!(landing_strength(2.0, &d), 0.0);
        assert!(landing_strength(4.0, &d) > 0.0);
        assert_eq!(landing_strength(40.0, &d), 1.0);
    }

    #[test]
    fn effects_data_parses() {
        let _: EffectsData = crate::data::parse(include_str!("../../../../godot/data/player/effects.ron")).unwrap();
    }
}
