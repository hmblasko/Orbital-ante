//! Drivable rovers (CLAUDE.md section 5: NASA rovers drivable in first
//! person; dune-buggy rovers get rocket boosts). The vertical slice's Moon
//! buggy is a cartoon Lunar Roving Vehicle.
//!
//! A rover is a RigidBody3D with ray-cast wheels: each wheel casts down
//! from its mount and pushes back with a spring and damper, then its tyre
//! grips sideways and drives forward inside a friction circle. Gravity
//! comes from the same bodies as everything else, so the buggy jumps,
//! lands and tumbles by the same rules as the player and the ships
//! (pillar 4, everything is physical; fun rule 10, game feel; fun rule 11,
//! emergent stories when it flies off a crater rim).
//!
//! Parked rovers are frozen children of their body (they turn with it and
//! need no terrain collision). When the player comes near and the ground
//! has collision, the rover wakes: it moves under the scene root, joins the
//! solar system's riders and floating origin, and simulates. When the
//! player leaves it far behind, it settles back onto the ground and parks.
//!
//! All tuning lives in data/vehicles/*.ron.

use godot::classes::light_3d::Param;
use godot::classes::rigid_body_3d::{CenterOfMassMode, FreezeMode};
use godot::classes::{
    BoxShape3D, CollisionShape3D, IRigidBody3D, Input, MeshInstance3D, Node3D, OmniLight3D, PhysicsDirectBodyState3D,
    PhysicsMaterial, PhysicsRayQueryParameters3D, RigidBody3D, SpotLight3D,
};
use godot::prelude::*;
use serde::Deserialize;

use super::meshes::{Geo, rounded_box, rounded_disc, swept_tube, torus};
use super::ship::{LAYER_HULL, LAYER_WORLD};
use crate::orbits::CelestialBody;
use crate::orbits::body::{GRAVITY_GROUP, basis_with_up};
use crate::orbits::landmarks::{paint_glow, paint_lit};
use crate::orbits::system::{FLOATING_GROUP, RIDER_GROUP};
use crate::player::controller::PLAYER_GROUP;
use crate::procgen::terrain::TerrainShape;
use crate::seed::SeededRng;

/// Every rover, parked or awake (the player looks here for one to drive).
pub const ROVER_GROUP: &str = "rovers";
const SPAWNS_PATH: &str = "res://data/vehicles/rovers.ron";
const SPAWNS_EMBEDDED: &str = include_str!("../../../../godot/data/vehicles/rovers.ron");
pub const BUGGY_PATH: &str = "res://data/vehicles/moon_buggy.ron";
const BUGGY_EMBEDDED: &str = include_str!("../../../../godot/data/vehicles/moon_buggy.ron");

type V3 = (f32, f32, f32);

fn v3((x, y, z): V3) -> Vector3 {
    Vector3::new(x, y, z)
}

#[derive(Debug, Clone, Deserialize)]
pub struct RoverData {
    pub mass_kg: f32,
    pub center_of_mass: V3,
    pub angular_damp: f32,
    pub friction: f32,
    pub bounce: f32,
    pub hull: Vec<HullBox>,
    pub wheels: WheelSet,
    pub drive: DriveData,
    pub boost: BoostData,
    pub air_control_deg_s2: f32,
    pub air_rate_deg_s: f32,
    pub righting: Righting,
    pub seat_at: V3,
    pub exit_at: V3,
    pub enter_range_m: f32,
    pub look_yaw_limit_deg: f32,
    pub crash: CrashData,
    pub engine_noise_m: f32,
    pub engine_noise_every_s: f32,
    pub wake_m: f32,
    pub park_m: f32,
    pub look: RoverLook,
}

#[derive(Debug, Clone, Deserialize)]
pub struct HullBox {
    pub size: V3,
    pub at: V3,
}

#[derive(Debug, Clone, Deserialize)]
pub struct WheelSet {
    pub radius_m: f32,
    pub width_m: f32,
    pub rest_m: f32,
    pub spring: f32,
    pub damper: f32,
    pub grip: f32,
    pub mu: f32,
    pub grip_floor_mps2: f32,
    pub mounts: Vec<WheelMount>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct WheelMount {
    pub pos: V3,
    pub steer: bool,
    pub drive: bool,
}

#[derive(Debug, Clone, Deserialize)]
pub struct DriveData {
    pub accel_mps2: f32,
    pub top_speed_mps: f32,
    pub reverse_mps: f32,
    pub brake_mps2: f32,
    pub coast_drag: f32,
    pub steer_deg: f32,
    pub steer_speed_deg_s: f32,
    pub steer_falloff_mps: f32,
    pub handbrake_grip: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct BoostData {
    pub accel_mps2: f32,
    pub fuel_s: f32,
    pub recharge_per_s: f32,
    pub top_speed_mps: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct Righting {
    pub tilt_deg: f32,
    pub max_speed_mps: f32,
    pub rate_deg_s: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct CrashData {
    pub safe_mps: f32,
    pub hurt_per_mps: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct RoverLook {
    pub body: V3,
    pub trim: V3,
    pub foil: V3,
    pub dark: V3,
    pub tyre: V3,
    pub seat: V3,
    pub lamp: V3,
    pub flame: V3,
    pub headlight_energy: f32,
    pub headlight_range_m: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct SpawnsData {
    pub spawns: Vec<RoverSpawn>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct RoverSpawn {
    pub body: String,
    pub data: String,
    pub angle_deg: (f32, f32),
    pub clear_of_camp_deg: f32,
}

pub fn load_rover(path: &str) -> Option<RoverData> {
    if path == BUGGY_PATH {
        return Some(crate::data::load(BUGGY_PATH, BUGGY_EMBEDDED));
    }
    crate::data::try_load::<RoverData>(path)
}

pub fn load_spawns() -> SpawnsData {
    crate::data::load(SPAWNS_PATH, SPAWNS_EMBEDDED)
}

impl RoverData {
    /// Height of the rover's origin above flat ground with the springs at
    /// rest under gravity `g` (for parking it on the ground).
    pub fn ride_height(&self, g: f32) -> f32 {
        let w = &self.wheels;
        let mount_y = w.mounts.iter().map(|m| m.pos.1).fold(0.0, f32::max);
        let sag = if w.spring > 0.0 { (g / w.spring).min(w.rest_m) } else { 0.0 };
        w.rest_m + w.radius_m - mount_y - sag
    }
}

// ----- Pure driving maths (unit tested) -----

/// Steering angle limit at `speed` (narrower when fast).
pub fn steer_limit(d: &DriveData, speed: f32) -> f32 {
    d.steer_deg.to_radians() / (1.0 + speed.abs() / d.steer_falloff_mps.max(0.1))
}

/// One wheel's suspension push (as acceleration on its share of the mass):
/// spring on compression plus damping on compression speed, never pulling.
pub fn suspension_accel(w: &WheelSet, compression: f32, compression_speed: f32) -> f32 {
    (w.spring * compression.max(0.0) + w.damper * compression_speed).max(0.0)
}

/// Drive and brake along the wheel (acceleration), from throttle -1..1 and
/// the wheel's forward speed. Pulling back while rolling forward brakes;
/// once nearly stopped it reverses.
pub fn drive_accel(d: &DriveData, throttle: f32, v_long: f32, drives: bool) -> f32 {
    if throttle > 0.0 {
        if v_long < -0.5 {
            return d.brake_mps2 * throttle;
        }
        return if drives && v_long < d.top_speed_mps { d.accel_mps2 * throttle } else { 0.0 };
    }
    if throttle < 0.0 {
        if v_long > 0.5 {
            return d.brake_mps2 * throttle;
        }
        return if drives && v_long > -d.reverse_mps { d.accel_mps2 * throttle } else { 0.0 };
    }
    -v_long * d.coast_drag
}

/// Clamp a tyre force to the friction circle `limit`.
pub fn friction_circle(f: Vector3, limit: f32) -> Vector3 {
    let l = f.length();
    if l > limit && l > 0.0 { f * (limit / l) } else { f }
}

/// Grip "weight": the wheel's load, scaled up where gravity is below the
/// floor so low-gravity driving stays controllable (the same rule
/// everywhere, so it's learnable).
pub fn grip_load(load_accel: f32, gravity: f32, floor: f32) -> f32 {
    let scale = if gravity > 0.01 { (floor / gravity).max(1.0) } else { 1.0 };
    load_accel * scale
}

/// Boost fuel after `dt` seconds (burning or recharging at `recharge_mult` x).
pub fn boost_fuel(b: &BoostData, fuel: f32, burning: bool, dt: f32, recharge_mult: f32) -> f32 {
    if burning { (fuel - dt).max(0.0) } else { (fuel + b.recharge_per_s * recharge_mult * dt).min(b.fuel_s) }
}

/// Driver hurt from a chassis impact at `speed` m/s (open cockpit).
pub fn crash_hurt(c: &CrashData, speed: f32, mult: f32) -> f32 {
    ((speed - c.safe_mps).max(0.0)) * c.hurt_per_mps * mult
}

/// Seeded parking directions (body local, unit) for one spawn entry.
pub fn spawn_dir(s: &RoverSpawn, run_seed: u64, avoid: &[Vector3]) -> Vector3 {
    let mut rng = SeededRng::derive(run_seed, &format!("rover_{}", s.body));
    let clear = s.clear_of_camp_deg.to_radians();
    let mut pick = Vector3::UP;
    for attempt in 0..64 {
        let tilt = rng.range_f32(s.angle_deg.0, s.angle_deg.1).to_radians();
        let around = rng.range_f32(0.0, std::f32::consts::TAU);
        pick = Vector3::new(tilt.sin() * around.cos(), tilt.cos(), tilt.sin() * around.sin()).normalized();
        if avoid.iter().all(|a| a.angle_to(pick) >= clear) || attempt == 63 {
            break;
        }
    }
    pick
}

/// What the driver is asking for this tick.
#[derive(Debug, Clone, Copy, Default)]
struct DriveInput {
    throttle: f32,
    steer: f32,
    boost: bool,
    brake: bool,
}

#[derive(GodotClass)]
#[class(base = RigidBody3D)]
pub struct Rover {
    #[export(file = "*.ron")]
    data_path: GString,
    data: Option<RoverData>,
    /// Awake and simulating (under the scene root), or parked and frozen.
    active: bool,
    /// A wake or park is queued (deferred: it reparents the node).
    switching: bool,
    driver: bool,
    input: DriveInput,
    /// Tests can drive without a pad.
    forced: Option<DriveInput>,
    steer: f32,
    boost_fuel: f32,
    boosting: bool,
    righting: bool,
    /// Suspension length per wheel (for the wheel meshes) and spin angle.
    wheel_len: Vec<f32>,
    wheel_spin: Vec<f32>,
    wheel_speed: Vec<f32>,
    wheel_nodes: Vec<(Gd<Node3D>, Gd<Node3D>)>,
    grounded: usize,
    noise_s: f32,
    /// Driver hurt waiting to be handed to the player (from a crash).
    pending_hurt: f32,
    crash_cooldown: f32,
    flame: Option<Gd<Node3D>>,
    flame_light: Option<Gd<OmniLight3D>>,
    /// Test hook: turn over on the next physics step.
    flip_pending: bool,
    base: Base<RigidBody3D>,
}

#[godot_api]
impl IRigidBody3D for Rover {
    fn init(base: Base<RigidBody3D>) -> Self {
        Self {
            data_path: GString::from(BUGGY_PATH),
            data: None,
            active: false,
            switching: false,
            driver: false,
            input: DriveInput::default(),
            forced: None,
            steer: 0.0,
            boost_fuel: 0.0,
            boosting: false,
            righting: false,
            wheel_len: Vec::new(),
            wheel_spin: Vec::new(),
            wheel_speed: Vec::new(),
            wheel_nodes: Vec::new(),
            grounded: 0,
            noise_s: 0.0,
            pending_hurt: 0.0,
            crash_cooldown: 0.0,
            flame: None,
            flame_light: None,
            flip_pending: false,
            base,
        }
    }

    fn ready(&mut self) {
        let Some(d) = load_rover(&self.data_path.to_string()) else {
            godot_error!("Rover: no data at {}", self.data_path);
            return;
        };
        // After the solar system (-100), before the player (0), like ships.
        self.base_mut().set_physics_process_priority(-90);
        {
            let mut b = self.base_mut();
            b.add_to_group(ROVER_GROUP);
            b.set_mass(d.mass_kg);
            b.set_gravity_scale(0.0);
            b.set_can_sleep(false);
            b.set_use_continuous_collision_detection(true);
            b.set_contact_monitor(true);
            b.set_max_contacts_reported(4);
            b.set_center_of_mass_mode(CenterOfMassMode::CUSTOM);
            b.set_center_of_mass(v3(d.center_of_mass));
            b.set_angular_damp(d.angular_damp);
            b.set_collision_layer(LAYER_HULL);
            b.set_collision_mask(LAYER_WORLD | LAYER_HULL);
            b.set_freeze_mode(FreezeMode::STATIC);
            b.set_freeze_enabled(true);
            let mut material = PhysicsMaterial::new_gd();
            material.set_friction(d.friction);
            material.set_bounce(d.bounce);
            b.set_physics_material_override(&material);
        }
        for (i, h) in d.hull.iter().enumerate() {
            let mut col = CollisionShape3D::new_alloc();
            col.set_name(&format!("Hull{i}"));
            let mut shape = BoxShape3D::new_gd();
            shape.set_size(v3(h.size));
            col.set_shape(&shape);
            col.set_position(v3(h.at));
            self.base_mut().add_child(&col);
        }
        self.boost_fuel = d.boost.fuel_s;
        let n = d.wheels.mounts.len();
        self.wheel_len = vec![d.wheels.rest_m; n];
        self.wheel_spin = vec![0.0; n];
        self.wheel_speed = vec![0.0; n];
        self.build_model(&d);
        self.data = Some(d);
    }

    fn physics_process(&mut self, delta: f64) {
        let Some(d) = self.data.clone() else { return };
        let dt = delta as f32;
        self.crash_cooldown = (self.crash_cooldown - dt).max(0.0);
        let player = player(&self.base());
        let here = self.base().get_global_position();
        let dist = player.as_ref().map_or(f32::MAX, |p| p.get_global_position().distance_to(here));
        if !self.switching {
            if !self.active && dist < d.wake_m && self.ground_ready() {
                self.switching = true;
                self.base_mut().call_deferred("wake", &[]);
            } else if self.active && !self.driver && dist > d.park_m {
                self.switching = true;
                self.base_mut().call_deferred("park", &[]);
            }
        }
        // The driver's controls (left stick drives, A boosts, B brakes).
        self.input = match self.forced {
            Some(f) => f,
            None if self.driver => {
                let i = Input::singleton();
                let stick = i.get_vector("move_left", "move_right", "move_forward", "move_back");
                DriveInput { throttle: -stick.y, steer: stick.x, boost: i.is_action_pressed("jump"), brake: i.is_action_pressed("crouch") }
            }
            None => DriveInput::default(),
        };
        let speed = self.base().get_linear_velocity().length();
        let limit = steer_limit(&d.drive, speed);
        let want = self.input.steer * limit;
        let step = d.drive.steer_speed_deg_s.to_radians() * dt;
        self.steer += (want - self.steer).clamp(-step, step);
        // A running motor is heard (section 6), like a ship's engine.
        if self.active && (self.input.throttle.abs() > 0.1 || self.boosting) {
            self.noise_s -= dt;
            if self.noise_s <= 0.0 {
                self.noise_s = d.engine_noise_every_s;
                let effort = self.input.throttle.abs().max(if self.boosting { 1.0 } else { 0.0 });
                let radius = d.engine_noise_m * effort * crate::points::singleton::stat("engine_noise");
                crate::ai::enemy::hear_noise(&self.base().get_tree(), here, radius, None);
            }
        }
        // Hand crash hurt to the driver (open cockpit).
        if self.pending_hurt > 0.0 {
            let hurt = std::mem::take(&mut self.pending_hurt);
            if self.driver
                && let Some(p) = player
                && let Ok(mut pc) = p.try_cast::<crate::player::PlayerController>()
            {
                pc.bind_mut().crash_hurt(hurt);
            }
        }
        self.update_visuals(&d, dt);
    }

    fn integrate_forces(&mut self, state: Option<Gd<PhysicsDirectBodyState3D>>) {
        let (Some(mut st), Some(d)) = (state, self.data.clone()) else { return };
        if !self.active {
            return;
        }
        let dt = st.get_step();
        if std::mem::take(&mut self.flip_pending) {
            let mut t = st.get_transform();
            let fwd = t.basis.col_c().normalized();
            let up = t.basis.col_b().normalized();
            t.basis = Basis::from_axis_angle(fwd, std::f32::consts::PI) * t.basis;
            t.origin += up * 1.5;
            st.set_transform(t);
            st.set_linear_velocity(Vector3::ZERO);
            st.set_angular_velocity(Vector3::ZERO);
            return;
        }
        let t = st.get_transform();
        let mass = d.mass_kg;
        let up = t.basis.col_b().normalized();
        let fwd = t.basis.col_c().normalized();
        let v = st.get_linear_velocity();
        let w = st.get_angular_velocity();
        let com = st.get_center_of_mass();
        let gravity = self.gravity_at(t.origin);
        let g_len = gravity.length();
        st.apply_central_force_ex().force(gravity * mass).done();

        // Crashes: the chassis hitting something hard.
        let mut worst = 0.0f32;
        for i in 0..st.get_contact_count() {
            worst = worst.max(st.get_contact_impulse(i).length() / mass.max(0.01));
        }
        if worst > d.crash.safe_mps && self.crash_cooldown <= 0.0 {
            self.pending_hurt += crash_hurt(&d.crash, worst, crate::points::singleton::stat("rover_crash_damage"));
            self.crash_cooldown = 0.4;
        }

        let input = self.input;
        let wd = &d.wheels;
        let n = wd.mounts.len().max(1);
        let mw = mass / n as f32;
        let reach = wd.rest_m + wd.radius_m;
        let rid = self.base().get_rid();
        let mut space = st.get_space_state();
        let mut grounded = 0;
        for (i, m) in wd.mounts.iter().enumerate() {
            let mount = t * v3(m.pos);
            let Some(mut q) = PhysicsRayQueryParameters3D::create_ex(mount, mount - up * reach).collision_mask(LAYER_WORLD | LAYER_HULL).done() else {
                continue;
            };
            let ex: Array<Rid> = [rid].into_iter().collect();
            q.set_exclude(&ex);
            let hit = space.intersect_ray(&q);
            if hit.is_empty() {
                self.wheel_len[i] = wd.rest_m;
                self.wheel_speed[i] *= 0.98;
                continue;
            }
            grounded += 1;
            let at: Vector3 = hit.get("position").map_or(mount - up * reach, |p| p.to());
            let normal: Vector3 = hit.get("normal").map_or(up, |p| p.to());
            let dist = (at - mount).dot(-up).max(0.0);
            self.wheel_len[i] = (dist - wd.radius_m).clamp(0.0, wd.rest_m);
            let compression = reach - dist;
            let off = at - t.origin;
            let pv = v + w.cross(off - com);
            let load = suspension_accel(wd, compression, -pv.dot(up));
            // Tyre directions on the ground plane.
            let steer = if m.steer { self.steer } else { 0.0 };
            let wf = Basis::from_axis_angle(up, -steer) * fwd;
            let wf = (wf - normal * wf.dot(normal)).normalized_or_zero();
            let lat = wf.cross(normal).normalized_or_zero();
            let v_long = pv.dot(wf);
            let v_lat = pv.dot(lat);
            self.wheel_speed[i] = v_long;
            let rear = !m.steer;
            let grip = wd.grip * if rear && input.brake { d.drive.handbrake_grip } else { 1.0 };
            let mut along = if self.driver || self.forced.is_some() {
                drive_accel(&d.drive, input.throttle, v_long, m.drive)
            } else {
                // Parking brake when nobody is aboard.
                -v_long * 4.0
            };
            if rear && input.brake {
                along -= v_long.signum() * d.drive.brake_mps2 * 0.6;
            }
            let tyre = (wf * along - lat * (v_lat * grip)) * mw;
            let limit = wd.mu * mw * grip_load(load, g_len, wd.grip_floor_mps2);
            let tyre = friction_circle(tyre, limit);
            st.apply_force_ex(up * (load * mw)).position(off).done();
            // Tyre forces act a little above the contact so the buggy leans
            // in turns without rolling over at the first bend.
            st.apply_force_ex(tyre).position(off + up * (dist * 0.7)).done();
        }
        self.grounded = grounded;

        // Rocket boost.
        let forward_speed = v.dot(fwd);
        self.boosting = input.boost && self.boost_fuel > 0.0 && !self.righting && forward_speed < d.boost.top_speed_mps;
        if self.boosting {
            st.apply_central_force_ex().force(fwd * (d.boost.accel_mps2 * mass)).done();
        }
        self.boost_fuel = boost_fuel(&d.boost, self.boost_fuel, self.boosting, dt, crate::points::singleton::stat("rover_boost_recharge"));

        // Tipped over and nearly stopped: hold A to roll back upright.
        let world_up = if g_len > 0.01 { -gravity / g_len } else { up };
        let tipped = up.dot(world_up) < d.righting.tilt_deg.to_radians().cos();
        self.righting = (self.driver || self.forced.is_some()) && input.boost && tipped && v.length() < d.righting.max_speed_mps;
        if self.righting {
            let axis = up.cross(world_up).normalized_or_zero();
            let axis = if axis.length_squared() < 0.5 { fwd } else { axis };
            st.set_angular_velocity(axis * d.righting.rate_deg_s.to_radians());
        } else if grounded == 0 && (self.driver || self.forced.is_some()) {
            // In the air the stick tilts the buggy so you can land it.
            let pitch_axis = up.cross(fwd);
            let roll_axis = up.cross(-t.basis.col_a().normalized());
            // Turn toward a capped spin rate on each axis, so holding the
            // stick tilts it steadily instead of spinning it ever faster.
            let acc = d.air_control_deg_s2.to_radians() * dt;
            let cap = d.air_rate_deg_s.to_radians();
            let mut w2 = w;
            for (axis, stick) in [(pitch_axis, input.throttle), (roll_axis, input.steer)] {
                if stick.abs() > 0.05 {
                    let now = w2.dot(axis);
                    let want = stick * cap;
                    w2 += axis * (want - now).clamp(-acc, acc);
                }
            }
            st.set_angular_velocity(w2);
        }
    }
}

#[godot_api]
impl Rover {
    /// Wake up: leave the body, join the riders and simulate.
    #[func]
    fn wake(&mut self) {
        self.switching = false;
        if self.active {
            return;
        }
        let Some(p) = player(&self.base()) else { return };
        let Some(parent) = p.get_parent() else { return };
        self.base_mut().reparent_ex(&parent).keep_global_transform(true).done();
        let mut b = self.base_mut();
        b.add_to_group(RIDER_GROUP);
        b.add_to_group(FLOATING_GROUP);
        b.set_linear_velocity(Vector3::ZERO);
        b.set_angular_velocity(Vector3::ZERO);
        b.set_freeze_enabled(false);
        drop(b);
        self.active = true;
    }

    /// Settle onto the ground under it and park as a frozen child of the
    /// body (it turns with the body and needs no collision there).
    #[func]
    fn park(&mut self) {
        self.switching = false;
        if !self.active || self.driver {
            return;
        }
        let Some(d) = self.data.clone() else { return };
        let here = self.base().get_global_position();
        let Some(body) = dominant_body(&self.base(), here) else { return };
        let g = body.bind().gravity_at(here).length();
        {
            let mut b = self.base_mut();
            b.remove_from_group(RIDER_GROUP);
            b.remove_from_group(FLOATING_GROUP);
            b.set_freeze_enabled(true);
            b.set_linear_velocity(Vector3::ZERO);
            b.set_angular_velocity(Vector3::ZERO);
        }
        self.base_mut().reparent_ex(&body.clone().upcast::<Node>()).keep_global_transform(true).done();
        let local = self.base().get_transform();
        let dir = local.origin.normalized_or_zero();
        if dir.length_squared() > 0.5 {
            let ground = body.bind().surface_radius_local(dir);
            let basis = crate::player::motion::align_up(local.basis, dir, std::f32::consts::PI);
            self.base_mut().set_transform(Transform3D::new(basis, dir * (ground + d.ride_height(g))));
        }
        self.active = false;
    }

    /// The driver sits here (world space, the player's body centre).
    pub fn seat_transform(&self) -> Option<Transform3D> {
        let d = self.data.as_ref()?;
        let t = self.base().get_global_transform();
        Some(Transform3D::new(t.basis, t * v3(d.seat_at)))
    }

    /// Where the driver climbs out (world space).
    pub fn exit_point(&self) -> Option<Vector3> {
        let d = self.data.as_ref()?;
        Some(self.base().get_global_transform() * v3(d.exit_at))
    }

    /// Close enough to climb in.
    pub fn can_enter(&self, p: Vector3) -> bool {
        let Some(d) = self.data.as_ref() else { return false };
        let t = self.base().get_global_transform();
        p.distance_to(t * v3(d.seat_at)) < d.enter_range_m || p.distance_to(t.origin) < d.enter_range_m
    }

    pub fn look_yaw_limit(&self) -> f32 {
        self.data.as_ref().map_or(150.0, |d| d.look_yaw_limit_deg).to_radians()
    }

    pub fn set_driver(&mut self, on: bool) {
        self.driver = on;
        if on && !self.active && !self.switching {
            // Climbing in wakes it at once.
            self.switching = true;
            self.base_mut().call_deferred("wake", &[]);
        }
    }

    #[func]
    pub fn is_awake(&self) -> bool {
        self.active
    }

    #[func]
    pub fn is_driven(&self) -> bool {
        self.driver
    }

    #[func]
    pub fn get_speed(&self) -> f32 {
        self.base().get_linear_velocity().length()
    }

    #[func]
    pub fn get_boost_fraction(&self) -> f32 {
        self.data.as_ref().map_or(0.0, |d| self.boost_fuel / d.boost.fuel_s.max(0.01))
    }

    #[func]
    pub fn get_grounded_wheels(&self) -> i64 {
        self.grounded as i64
    }

    #[func]
    pub fn is_boosting(&self) -> bool {
        self.boosting
    }

    /// Tilt from upright, in degrees (tests).
    #[func]
    pub fn get_tilt_deg(&self) -> f32 {
        let p = self.base().get_global_position();
        let g = self.gravity_at(p);
        if g.length_squared() < 1e-6 {
            return 0.0;
        }
        self.base().get_global_basis().col_b().normalized().angle_to(-g.normalized()).to_degrees()
    }

    /// Test hook: drive as if the stick and buttons were held.
    #[func]
    pub fn debug_drive(&mut self, throttle: f32, steer: f32, boost: bool, brake: bool) {
        self.forced = Some(DriveInput { throttle, steer, boost, brake });
    }

    #[func]
    pub fn debug_release(&mut self) {
        self.forced = None;
    }

    /// Test hook: wake now (normally the player has to come close).
    #[func]
    pub fn debug_wake(&mut self) {
        self.wake();
    }

    #[func]
    pub fn debug_park(&mut self) {
        self.park();
    }

    /// Test hook: turn the buggy over where it stands.
    #[func]
    pub fn debug_flip(&mut self) {
        self.flip_pending = true;
    }

    fn ground_ready(&self) -> bool {
        let mut n = self.base().get_parent();
        while let Some(node) = n {
            if let Ok(body) = node.clone().try_cast::<CelestialBody>() {
                return body.bind().has_detail();
            }
            n = node.get_parent();
        }
        false
    }

    fn gravity_at(&self, p: Vector3) -> Vector3 {
        self.base()
            .get_tree()
            .get_nodes_in_group(GRAVITY_GROUP)
            .iter_shared()
            .filter_map(|n| n.try_cast::<CelestialBody>().ok())
            .map(|b| b.bind().gravity_at(p))
            .fold(Vector3::ZERO, |a, g| a + g)
    }

    fn update_visuals(&mut self, d: &RoverData, dt: f32) {
        let r = d.wheels.radius_m.max(0.05);
        for (i, m) in d.wheels.mounts.iter().enumerate() {
            let Some((pivot, spin)) = self.wheel_nodes.get_mut(i) else { continue };
            let p = v3(m.pos) - Vector3::UP * self.wheel_len[i];
            let steer = if m.steer { self.steer } else { 0.0 };
            pivot.set_transform(Transform3D::new(Basis::from_axis_angle(Vector3::UP, -steer), p));
            self.wheel_spin[i] = (self.wheel_spin[i] + self.wheel_speed[i] / r * dt) % std::f32::consts::TAU;
            spin.set_basis(Basis::from_axis_angle(Vector3::RIGHT, self.wheel_spin[i]));
        }
        let on = self.boosting;
        if let Some(f) = &mut self.flame {
            f.set_visible(on);
            if on {
                // Flicker the plume's length a little each tick.
                let k = 0.85 + 0.3 * ((self.wheel_spin[0] * 37.0).sin() * 0.5 + 0.5);
                f.set_scale(Vector3::new(1.0, 1.0, k));
            }
        }
        if let Some(l) = &mut self.flame_light {
            l.set_visible(on);
        }
    }

    /// A chunky cartoon Lunar Roving Vehicle: bevelled tub, gold foil
    /// panels, roll hoops, fenders, two seats, the umbrella antenna, round
    /// tyres, headlights and a rocket on the back (section 11: bevels,
    /// smooth curves, material variety, a silhouette that reads).
    fn build_model(&mut self, d: &RoverData) {
        let l = &d.look;
        let mut root = Node3D::new_alloc();
        root.set_name("Model");
        let at = |x: f32, y: f32, z: f32| Transform3D::new(Basis::IDENTITY, Vector3::new(x, y, z));
        let one = |g: Geo, t: Transform3D| {
            let mut o = Geo::default();
            o.append(&g, t);
            o
        };
        let tube = |a: Vector3, b: Vector3, r: f32| {
            let refv = if (b - a).normalized().dot(Vector3::UP).abs() > 0.9 { Vector3::RIGHT } else { Vector3::UP };
            swept_tube(&[a, b], &[refv, refv], r, 10, false)
        };

        // Tub and deck.
        let mut tub = Geo::default();
        tub.append(&rounded_box(Vector3::new(1.75, 0.38, 3.1), 0.16, 4), at(0.0, 0.82, 0.0));
        tub.append(&rounded_box(Vector3::new(1.5, 0.22, 0.9), 0.1, 3), at(0.0, 1.05, 1.1));
        mesh(&mut root, "Tub", tub, paint_lit(l.body, 0.25));
        let mut foil = Geo::default();
        for x in [-0.89, 0.89] {
            foil.append(&rounded_box(Vector3::new(0.06, 0.26, 2.3), 0.03, 2), at(x, 0.82, 0.0));
        }
        foil.append(&rounded_box(Vector3::new(1.3, 0.3, 0.5), 0.1, 3), at(0.0, 1.05, -1.3));
        mesh(&mut root, "Foil", foil, paint_lit(l.foil, 0.8));
        let mut trim = Geo::default();
        trim.append(&rounded_box(Vector3::new(1.82, 0.08, 3.16), 0.04, 2), at(0.0, 1.0, 0.0));
        mesh(&mut root, "Trim", trim, paint_lit(l.trim, 0.3));

        // Fenders over each wheel: a half hoop.
        let mut fenders = Geo::default();
        for m in &d.wheels.mounts {
            let c = v3(m.pos) - Vector3::UP * (d.wheels.rest_m * 0.6);
            let r = d.wheels.radius_m + 0.12;
            let pts: Vec<Vector3> = (0..=10).map(|k| {
                let a = std::f32::consts::PI * (0.15 + 0.7 * k as f32 / 10.0);
                c + Vector3::new(0.0, a.sin() * r, a.cos() * r)
            }).collect();
            let refs = vec![Vector3::RIGHT; pts.len()];
            let mut arc = swept_tube(&pts, &refs, 0.07, 8, false);
            arc.fix_winding();
            fenders.append(&arc, Transform3D::IDENTITY);
            fenders.append(&rounded_box(Vector3::new(d.wheels.width_m + 0.1, 0.05, r * 1.6), 0.02, 2), at(c.x, c.y + r, c.z));
        }
        mesh(&mut root, "Fenders", fenders, paint_lit(l.body, 0.25));

        // Seats: two bucket seats, driver on the left like the LRV.
        let mut seats = Geo::default();
        for x in [0.38, -0.38] {
            seats.append(&rounded_box(Vector3::new(0.55, 0.12, 0.55), 0.05, 3), at(x, 1.15, d.seat_at.2 + 0.05));
            seats.append(&rounded_box(Vector3::new(0.55, 0.6, 0.1), 0.05, 3), at(x, 1.45, d.seat_at.2 - 0.25));
        }
        mesh(&mut root, "Seats", seats, paint_lit(l.seat, 0.05));

        // Roll hoops and the control post.
        let mut frame = Geo::default();
        // One roll hoop behind the seats (a front one blocks the view).
        for z in [d.seat_at.2 - 0.4] {
            let pts: Vec<Vector3> = (0..=12).map(|k| {
                let a = std::f32::consts::PI * k as f32 / 12.0;
                Vector3::new(a.cos() * 0.8, 1.0 + a.sin() * 0.95, z)
            }).collect();
            let refs = vec![Vector3::BACK; pts.len()];
            frame.append(&swept_tube(&pts, &refs, 0.045, 8, false), Transform3D::IDENTITY);
        }
        frame.append(&tube(Vector3::new(d.seat_at.0, 1.0, d.seat_at.2 + 0.75), Vector3::new(d.seat_at.0, 1.45, d.seat_at.2 + 0.6), 0.04), Transform3D::IDENTITY);
        frame.append(&tube(Vector3::new(0.95, 0.6, 1.25), Vector3::new(-0.95, 0.6, 1.25), 0.05), Transform3D::IDENTITY);
        frame.append(&tube(Vector3::new(0.95, 0.6, -1.25), Vector3::new(-0.95, 0.6, -1.25), 0.05), Transform3D::IDENTITY);
        mesh(&mut root, "Frame", frame, paint_lit(l.dark, 0.7));
        mesh(&mut root, "Console", one(rounded_box(Vector3::new(0.42, 0.22, 0.14), 0.05, 3), at(d.seat_at.0, 1.5, d.seat_at.2 + 0.6)), paint_lit(l.dark, 0.5));
        mesh(&mut root, "ConsoleGlow", one(rounded_box(Vector3::new(0.3, 0.1, 0.02), 0.02, 2), at(d.seat_at.0, 1.53, d.seat_at.2 + 0.52)), paint_glow(l.trim, 1.6));

        // The umbrella antenna on a mast at the front.
        // On the passenger's side, out of the driver's view.
        let ax = -d.seat_at.0 - 0.3;
        let mut mast = tube(Vector3::new(ax, 1.05, 1.3), Vector3::new(ax, 2.1, 1.3), 0.03);
        mast.append(&rounded_disc(0.36, 0.05, 20), at(ax, 2.12, 1.3));
        mesh(&mut root, "Antenna", mast, paint_lit(l.body, 0.4));

        // Headlights and the rocket.
        let mut lamps = Geo::default();
        for x in [-0.5, 0.5] {
            lamps.append(&rounded_box(Vector3::new(0.22, 0.14, 0.06), 0.04, 2), at(x, 0.95, 1.58));
        }
        mesh(&mut root, "Headlamps", lamps, paint_glow(l.lamp, 2.0));
        let mut spot = SpotLight3D::new_alloc();
        spot.set_name("Headlight");
        spot.set_color(Color::from_rgb(l.lamp.0, l.lamp.1, l.lamp.2));
        spot.set_param(Param::ENERGY, l.headlight_energy);
        spot.set_param(Param::RANGE, l.headlight_range_m);
        spot.set_param(Param::SPOT_ANGLE, 38.0);
        // A spot shines along -Z; the buggy's front is +Z.
        spot.set_transform(Transform3D::new(Basis::from_axis_angle(Vector3::UP, std::f32::consts::PI) * Basis::from_axis_angle(Vector3::RIGHT, -0.12), Vector3::new(0.0, 1.0, 1.7)));
        root.add_child(&spot);
        let mut rocket = tube(Vector3::new(0.0, 1.05, -1.2), Vector3::new(0.0, 1.05, -1.75), 0.17);
        rocket.append(&torus(0.17, 0.04, 16, 6), Transform3D::new(Basis::from_axis_angle(Vector3::RIGHT, std::f32::consts::FRAC_PI_2), Vector3::new(0.0, 1.05, -1.75)));
        mesh(&mut root, "Rocket", rocket, paint_lit(l.dark, 0.8));
        let mut flame = Node3D::new_alloc();
        flame.set_name("Flame");
        let mut fn_ = flame.clone();
        // A tapering plume: a hot pale core inside softer orange shells.
        let mut shell = Geo::default();
        for (k, (w, len)) in [(0.3f32, 0.5f32), (0.22, 0.8), (0.13, 1.1)].iter().enumerate() {
            shell.append(&rounded_box(Vector3::new(*w, *w, *len), *w * 0.48, 3), at(0.0, 1.05, -1.8 - len * 0.5 - k as f32 * 0.05));
        }
        mesh(&mut fn_, "Plume", shell, paint_glow(l.flame, 2.2));
        mesh(&mut fn_, "Core", one(rounded_box(Vector3::new(0.12, 0.12, 0.45), 0.055, 3), at(0.0, 1.05, -1.95)), paint_glow((1.0, 0.95, 0.8), 4.0));
        let mut fl = OmniLight3D::new_alloc();
        fl.set_color(Color::from_rgb(l.flame.0, l.flame.1, l.flame.2));
        fl.set_param(Param::ENERGY, 3.0);
        fl.set_param(Param::RANGE, 8.0);
        fl.set_position(Vector3::new(0.0, 1.05, -2.4));
        root.add_child(&fl);
        flame.set_visible(false);
        fl.set_visible(false);
        root.add_child(&flame);
        self.flame = Some(flame);
        self.flame_light = Some(fl);

        // Wheels: a steering pivot, then a spinning hub with a fat tyre.
        let wr = d.wheels.radius_m;
        let tube_r = (d.wheels.width_m * 0.5).min(wr * 0.45);
        let on_side = Basis::from_axis_angle(Vector3::BACK, std::f32::consts::FRAC_PI_2);
        let tyre_geo = one(torus(wr - tube_r, tube_r, 28, 10), Transform3D::new(on_side, Vector3::ZERO));
        let hub_geo = one(rounded_disc(wr - tube_r * 1.2, d.wheels.width_m * 0.7, 20), Transform3D::new(on_side, Vector3::ZERO));
        let mut tread = Geo::default();
        for k in 0..12 {
            let a = k as f32 / 12.0 * std::f32::consts::TAU;
            let p = Vector3::new(0.0, a.sin() * (wr - 0.02), a.cos() * (wr - 0.02));
            tread.append(&rounded_box(Vector3::new(d.wheels.width_m * 0.8, 0.06, 0.12), 0.025, 2), Transform3D::new(Basis::from_axis_angle(Vector3::RIGHT, -a), p));
        }
        for (i, m) in d.wheels.mounts.iter().enumerate() {
            let mut pivot = Node3D::new_alloc();
            pivot.set_name(&format!("Wheel{i}"));
            pivot.set_position(v3(m.pos) - Vector3::UP * d.wheels.rest_m);
            let mut spin = Node3D::new_alloc();
            spin.set_name("Spin");
            mesh(&mut spin, "Tyre", tyre_geo.clone(), paint_lit(l.tyre, 0.0));
            mesh(&mut spin, "Tread", tread.clone(), paint_lit(l.tyre, 0.0));
            mesh(&mut spin, "Hub", hub_geo.clone(), paint_lit(l.foil, 0.7));
            pivot.add_child(&spin);
            root.add_child(&pivot);
            self.wheel_nodes.push((pivot, spin));
        }
        self.base_mut().add_child(&root);
    }
}

fn mesh(parent: &mut Gd<Node3D>, name: &str, g: Geo, mat: Gd<godot::classes::Material>) {
    let mut mi = MeshInstance3D::new_alloc();
    mi.set_name(name);
    mi.set_mesh(&g.to_array_mesh());
    mi.set_surface_override_material(0, &mat);
    parent.add_child(&mi);
}

fn player(base: &RigidBody3D) -> Option<Gd<Node3D>> {
    base.get_tree().get_first_node_in_group(PLAYER_GROUP).and_then(|n| n.try_cast::<Node3D>().ok())
}

fn dominant_body(base: &RigidBody3D, p: Vector3) -> Option<Gd<CelestialBody>> {
    base.get_tree()
        .get_nodes_in_group(GRAVITY_GROUP)
        .iter_shared()
        .filter_map(|n| n.try_cast::<CelestialBody>().ok())
        .max_by(|a, b| a.bind().gravity_at(p).length().total_cmp(&b.bind().gravity_at(p).length()))
}

/// A parked rover at `local` (body space) on its body.
pub fn parked(data_path: &str, name: &str, local: Transform3D) -> Gd<Rover> {
    let mut r = Rover::new_alloc();
    r.set_name(name);
    r.bind_mut().data_path = GString::from(data_path);
    r.set_transform(local);
    r
}

/// The run's parked rovers on one body (seeded spots, clear of the camp).
pub fn rovers_for_body(body_id: &str, shape: &TerrainShape, run_seed: u64, gravity: f32) -> Vec<Gd<Rover>> {
    let spawns = load_spawns();
    let enemies = crate::combat::load_enemies();
    let avoid: Vec<Vector3> = enemies
        .camps
        .iter()
        .enumerate()
        .filter(|(_, c)| c.body == body_id)
        .map(|(i, c)| crate::ai::camp::camp_centre(body_id, i, c, run_seed, 1).0)
        .collect();
    let mut out = Vec::new();
    for (k, s) in spawns.spawns.iter().enumerate().filter(|(_, s)| s.body == body_id) {
        let Some(d) = load_rover(&s.data) else { continue };
        let dir = spawn_dir(s, run_seed, &avoid);
        let ground = shape.surface_radius(dir);
        let t = Transform3D::new(basis_with_up(dir), dir * (ground + d.ride_height(gravity)));
        out.push(parked(&s.data, &format!("Rover_{body_id}_{k}"), t));
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;

    fn buggy() -> RoverData {
        ron::from_str(BUGGY_EMBEDDED).expect("moon_buggy.ron parses")
    }

    #[test]
    fn data_parses() {
        let d = buggy();
        assert_eq!(d.wheels.mounts.len(), 4);
        let s: SpawnsData = ron::from_str(SPAWNS_EMBEDDED).expect("rovers.ron parses");
        assert!(s.spawns.iter().any(|s| s.body == "moon"));
    }

    #[test]
    fn steering_narrows_with_speed() {
        let d = buggy();
        assert!(steer_limit(&d.drive, 0.0) > steer_limit(&d.drive, 15.0));
        assert!((steer_limit(&d.drive, 0.0) - d.drive.steer_deg.to_radians()).abs() < 1e-6);
    }

    #[test]
    fn suspension_never_pulls() {
        let d = buggy();
        assert_eq!(suspension_accel(&d.wheels, 0.0, -5.0), 0.0);
        assert!(suspension_accel(&d.wheels, 0.1, 0.0) > 0.0);
    }

    #[test]
    fn pulling_back_brakes_then_reverses() {
        let d = buggy().drive;
        assert!(drive_accel(&d, -1.0, 5.0, true) < 0.0);
        assert!((drive_accel(&d, -1.0, 5.0, true) + d.brake_mps2).abs() < 1e-6);
        assert!((drive_accel(&d, -1.0, 0.0, true) + d.accel_mps2).abs() < 1e-6);
        // No more push past top speed or full reverse speed.
        assert_eq!(drive_accel(&d, 1.0, d.top_speed_mps + 1.0, true), 0.0);
        assert_eq!(drive_accel(&d, -1.0, -d.reverse_mps - 1.0, true), 0.0);
        // Coasting slows it.
        assert!(drive_accel(&d, 0.0, 5.0, true) < 0.0);
    }

    #[test]
    fn friction_circle_caps_force() {
        let f = friction_circle(Vector3::new(30.0, 0.0, 40.0), 10.0);
        assert!((f.length() - 10.0).abs() < 1e-4);
        let g = friction_circle(Vector3::new(3.0, 0.0, 4.0), 10.0);
        assert!((g.length() - 5.0).abs() < 1e-4);
    }

    #[test]
    fn low_gravity_grip_is_floored() {
        assert!((grip_load(3.0, 3.0, 6.0) - 6.0).abs() < 1e-6);
        assert!((grip_load(9.8, 9.8, 6.0) - 9.8).abs() < 1e-6);
    }

    #[test]
    fn boost_burns_and_recharges() {
        let b = buggy().boost;
        let f = boost_fuel(&b, b.fuel_s, true, 1.0, 1.0);
        assert!((f - (b.fuel_s - 1.0)).abs() < 1e-6);
        let r1 = boost_fuel(&b, 0.0, false, 1.0, 1.0);
        let r2 = boost_fuel(&b, 0.0, false, 1.0, 2.0);
        assert!((r2 - 2.0 * r1).abs() < 1e-6);
        assert!(boost_fuel(&b, b.fuel_s, false, 10.0, 1.0) <= b.fuel_s);
    }

    #[test]
    fn crashes_hurt_only_past_safe_speed() {
        let c = buggy().crash;
        assert_eq!(crash_hurt(&c, c.safe_mps - 0.1, 1.0), 0.0);
        assert!(crash_hurt(&c, c.safe_mps + 2.0, 1.4) > crash_hurt(&c, c.safe_mps + 2.0, 1.0));
    }

    #[test]
    fn spawn_is_seeded_and_near_the_pole() {
        let s: SpawnsData = ron::from_str(SPAWNS_EMBEDDED).unwrap();
        let sp = &s.spawns[0];
        let a = spawn_dir(sp, 42, &[]);
        let b = spawn_dir(sp, 42, &[]);
        assert_eq!(a, b);
        let tilt = a.angle_to(Vector3::UP).to_degrees();
        assert!(tilt >= sp.angle_deg.0 - 0.01 && tilt <= sp.angle_deg.1 + 0.01, "tilt {tilt}");
        assert_ne!(spawn_dir(sp, 43, &[]), a);
    }

    #[test]
    fn ride_height_rests_wheels_on_ground() {
        let d = buggy();
        let h = d.ride_height(3.0);
        assert!(h > 0.0 && h < 1.0, "ride height {h}");
    }
}
