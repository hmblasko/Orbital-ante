//! The solar system: spawns every body in the roster, moves them along
//! their orbits and spins them each physics tick, carries the player along
//! with the body they are on, re-centres the world (floating origin) and
//! aims the sunlight (CLAUDE.md section 4 and Phase 3).
//!
//! Serves pillar 1 (a living clockwork solar system: everything keeps
//! moving whether you watch or not) and pillar 4 (one consistent physics:
//! you keep your momentum when you leave a planet). Fun rule 4 (curiosity:
//! the sky changes as you look at it) and rule 5 (flow: no loading).
//!
//! Bodies move kinematically along analytic orbits (`orbit.rs`), so the sky
//! is the same for the same seed and time. The player's velocity is kept
//! relative to the body whose gravity dominates ("the frame"): each tick
//! the player is moved by that body's own motion, and turned with its spin
//! near the surface, so standing on a moving, turning planet feels still.
//! Changing frame converts the velocity, so momentum is conserved.

use std::collections::HashMap;

use godot::classes::{CharacterBody3D, DirectionalLight3D, Engine, RigidBody3D};
use godot::prelude::*;
use serde::Deserialize;

use super::body::CelestialBody;
use super::landmarks::LandmarkSet;
use super::orbit::{self, DVec3, OrbitData, SpinData};
use crate::player::controller::PLAYER_GROUP;

/// Things that are free in the world (not riding a body) and must move when
/// the origin re-centres: dropped items, debris, ships in flight.
pub const FLOATING_GROUP: &str = "floating_origin";
/// Things that ride the bodies like the player does (ships): carried along
/// with the body whose gravity dominates, turned with its spin near the
/// surface, velocity kept relative to it.
pub const RIDER_GROUP: &str = "rides_bodies";
/// The solar system joins this group so the map can find it.
pub const SYSTEM_GROUP: &str = "solar_system";

#[derive(Debug, Clone, Deserialize)]
pub struct SystemData {
    pub bodies: Vec<SystemBody>,
    /// Re-centre the world on the player beyond this distance, in metres.
    pub recenter_m: f64,
    /// A body builds full detail when the player is within this many of its
    /// radii plus `detail_margin_m` of its centre.
    pub detail_radii: f32,
    pub detail_margin_m: f32,
    /// Bodies drop full detail beyond this many times their detail distance.
    pub release_factor: f32,
    /// Below this altitude (as a fraction of the radius) the player turns
    /// with the body's spin; above it the body turns underneath them.
    pub co_rotate_altitude: f32,
    /// A planet's pull fades out near each of its moons (and the Sun's
    /// near each planet), gone entirely within this many of the moon's
    /// radii, so the side of the Moon facing Earth isn't weightless.
    pub shield_inner_radii: f32,
    pub sun_light_energy: f32,
    pub sun_light_color: (f32, f32, f32),
    /// Where the player starts: a body id and a direction in its own frame.
    pub start_body: String,
    /// (0, 0, 0) starts on the day side instead, with the sun this many
    /// degrees from overhead.
    pub start_dir: (f32, f32, f32),
    #[serde(default)]
    pub start_sun_angle_deg: f32,
    /// Look this many degrees around the start for gentle, dry ground
    /// (0 = start exactly there).
    #[serde(default)]
    pub start_search_deg: f32,
    /// Run clock at scene start, in seconds (the seed sets the phases).
    #[serde(default)]
    pub start_time_s: f64,
}

#[derive(Debug, Clone, Deserialize)]
pub struct SystemBody {
    pub id: String,
    /// The body's own RON file (terrain, look, gravity).
    pub data: String,
    #[serde(default)]
    pub landmarks: Option<String>,
    /// Orbits this body (None = fixed at the system centre).
    #[serde(default)]
    pub parent: Option<String>,
    #[serde(default)]
    pub orbit: Option<OrbitData>,
    #[serde(default)]
    pub spin: SpinData,
    /// Dot colour on the live map.
    #[serde(default = "default_map_color")]
    pub map_color: (f32, f32, f32),
}

fn default_map_color() -> (f32, f32, f32) {
    (0.8, 0.8, 0.8)
}

/// Which body a rider moves with, and whether it turns with its spin.
#[derive(Debug, Clone, Copy, Default)]
struct Ride {
    frame: Option<usize>,
    co_rotating: bool,
}

/// A rider's position and velocity being carried through one tick, and the
/// spin turn applied to it (if any).
struct Carried {
    position: Vector3,
    velocity: Vector3,
    turn: Option<Basis>,
}

/// Everything about this tick's motion that carrying needs.
struct Step<'a> {
    data: &'a SystemData,
    eph: &'a Ephemeris,
    old: &'a [DVec3],
    new: &'a [DVec3],
    t0: f64,
    t1: f64,
    delta: f64,
}

/// One body as the live map draws it, in scene coordinates (after the
/// floating origin), seen at the current moment.
#[derive(Debug, Clone)]
pub struct MapBody {
    pub id: String,
    pub color: Color,
    /// Visible radius in metres (cloud tops for gas giants).
    pub radius: f32,
    /// Outer ring radius in metres (0 = no rings).
    pub ring: f32,
    pub position: Vector3,
    /// The orbit path as a closed loop (empty for the sun).
    pub orbit: Vec<Vector3>,
    pub is_star: bool,
    /// Orbits a planet rather than the sun.
    pub is_moon: bool,
}

/// Everything the live map needs for one frame.
#[derive(Debug, Clone)]
pub struct MapSnapshot {
    pub bodies: Vec<MapBody>,
    pub player: Vector3,
    pub player_forward: Vector3,
    pub frame_body: Option<String>,
}

/// Points along one orbit loop, relative to its parent (pure, for tests).
pub fn orbit_loop(o: &OrbitData, phase: f64, points: usize) -> Vec<DVec3> {
    (0..points)
        .map(|k| orbit::orbit_offset(o, phase, o.period_s * k as f64 / points as f64))
        .collect()
}

/// Absolute positions (system frame, f64) and turns of every body at a time.
/// Pure, so it can be tested and replayed without Godot.
#[derive(Debug, Clone)]
pub struct Ephemeris {
    parents: Vec<Option<usize>>,
    phases: Vec<(f64, f64)>,
}

impl Ephemeris {
    pub fn new(data: &SystemData, run_seed: u64) -> Result<Self, String> {
        let mut parents = Vec::new();
        for (i, b) in data.bodies.iter().enumerate() {
            let parent = match &b.parent {
                None => None,
                Some(p) => match data.bodies.iter().position(|o| &o.id == p) {
                    Some(k) if k < i => Some(k),
                    _ => return Err(format!("body {} orbits {p}, which must be listed before it", b.id)),
                },
            };
            if parent.is_some() != b.orbit.is_some() {
                return Err(format!("body {} needs both a parent and an orbit, or neither", b.id));
            }
            parents.push(parent);
        }
        let phases = data.bodies.iter().map(|b| orbit::start_phases(run_seed, &b.id)).collect();
        Ok(Self { parents, phases })
    }

    pub fn parent(&self, i: usize) -> Option<usize> {
        self.parents[i]
    }

    /// Seeded starting angle along body `i`'s orbit.
    pub fn phase(&self, i: usize) -> f64 {
        self.phases[i].0
    }

    /// Absolute centre of every body at time `t`.
    pub fn positions(&self, data: &SystemData, t: f64) -> Vec<DVec3> {
        let mut out: Vec<DVec3> = Vec::with_capacity(data.bodies.len());
        for (i, b) in data.bodies.iter().enumerate() {
            let base = self.parents[i].map_or(DVec3::ZERO, |p| out[p]);
            let offset = b.orbit.as_ref().map_or(DVec3::ZERO, |o| orbit::orbit_offset(o, self.phases[i].0, t));
            out.push(base + offset);
        }
        out
    }

    /// Orientation of body `i` at time `t`: axial tilt, then spin about the axis.
    pub fn basis(&self, data: &SystemData, i: usize, t: f64) -> Basis {
        let s = &data.bodies[i].spin;
        let tilt = Basis::from_axis_angle(Vector3::RIGHT, (s.axial_tilt_deg as f32).to_radians());
        let angle = orbit::spin_angle(s, self.phases[i].1, t);
        // Reduce in f64 first so the angle stays precise late in a long run.
        let angle = angle.rem_euclid(std::f64::consts::TAU) as f32;
        tilt * Basis::from_axis_angle(Vector3::UP, angle)
    }
}

#[derive(GodotClass)]
#[class(base = Node3D)]
pub struct SolarSystem {
    #[export(file = "*.ron")]
    data_path: GString,
    data: Option<SystemData>,
    ephemeris: Option<Ephemeris>,
    bodies: Vec<Gd<CelestialBody>>,
    clock: f64,
    /// Absolute position of the scene's origin (floating origin).
    origin: DVec3,
    /// Index of the body the player is riding, and whether they turn with it.
    frame: Option<usize>,
    co_rotating: bool,
    /// Frame state of every other rider, by instance id.
    riders: HashMap<i64, Ride>,
    sun_light: Option<Gd<DirectionalLight3D>>,
    base: Base<Node3D>,
}

#[godot_api]
impl INode3D for SolarSystem {
    fn init(base: Base<Node3D>) -> Self {
        Self {
            data_path: GString::from("res://data/system/solar_system.ron"),
            data: None,
            ephemeris: None,
            bodies: Vec::new(),
            clock: 0.0,
            origin: DVec3::ZERO,
            frame: None,
            co_rotating: false,
            riders: HashMap::new(),
            sun_light: None,
            base,
        }
    }

    fn ready(&mut self) {
        // Run before the player each tick, so bodies are already in place
        // when the player moves.
        self.base_mut().set_physics_process_priority(-100);
        let Some(data) = crate::data::try_load::<SystemData>(&self.data_path.to_string()) else {
            godot_error!("SolarSystem: no data at {}", self.data_path);
            return;
        };
        let seed = if Engine::singleton().has_singleton("RunSeed") { crate::seed::singleton::run_seed() } else { 0 };
        let ephemeris = match Ephemeris::new(&data, seed) {
            Ok(e) => e,
            Err(e) => {
                godot_error!("SolarSystem: {e}");
                return;
            }
        };
        self.clock = data.start_time_s;
        let positions = ephemeris.positions(&data, self.clock);
        let start = data.bodies.iter().position(|b| b.id == data.start_body);
        // Centre the world on the starting body so the player starts near (0,0,0).
        self.origin = start.map_or(DVec3::ZERO, |i| positions[i]);

        let mut light = DirectionalLight3D::new_alloc();
        light.set_name("Sunlight");
        let c = data.sun_light_color;
        light.set_color(Color::from_rgb(c.0, c.1, c.2));
        light.set_param(godot::classes::light_3d::Param::ENERGY, data.sun_light_energy);
        light.set_param(godot::classes::light_3d::Param::SHADOW_MAX_DISTANCE, 220.0);
        light.set_shadow(true);
        light.set_shadow_mode(godot::classes::directional_light_3d::ShadowMode::PARALLEL_2_SPLITS);
        self.base_mut().add_child(&light);
        self.sun_light = Some(light);

        for (i, b) in data.bodies.iter().enumerate() {
            let mut body = CelestialBody::new_alloc();
            body.set_name(&b.id);
            // The starting body builds in full at load so you land on real
            // ground; the rest stream in as you approach.
            body.bind_mut().configure(&b.data, Some(i) != start);
            body.set_position((positions[i] - self.origin).to_f32());
            body.set_basis(ephemeris.basis(&data, i, self.clock));
            if let Some(path) = &b.landmarks {
                let mut marks = LandmarkSet::new_alloc();
                marks.set_name("Landmarks");
                marks.bind_mut().configure(path);
                body.add_child(&marks);
            }
            self.base_mut().add_child(&body);
            self.bodies.push(body);
        }
        for (i, _) in data.bodies.iter().enumerate() {
            let moons: Vec<Gd<CelestialBody>> = (0..data.bodies.len()).filter(|&j| ephemeris.parent(j) == Some(i)).map(|j| self.bodies[j].clone()).collect();
            self.bodies[i].bind_mut().set_shields(moons, data.shield_inner_radii);
        }
        self.base_mut().add_to_group(SYSTEM_GROUP);
        self.data = Some(data);
        self.ephemeris = Some(ephemeris);
        self.base_mut().call_deferred("place_player", &[]);
    }

    fn physics_process(&mut self, delta: f64) {
        let (Some(data), Some(eph)) = (self.data.clone(), self.ephemeris.clone()) else {
            return;
        };
        let t0 = self.clock;
        let t1 = t0 + delta;
        self.clock = t1;
        let old = eph.positions(&data, t0);
        let new = eph.positions(&data, t1);
        let player = self.player();
        let step = Step { data: &data, eph: &eph, old: &old, new: &new, t0, t1, delta };

        // Ships and other riders.
        let tree = self.base().get_tree();
        let mut alive = Vec::new();
        for node in tree.get_nodes_in_group(RIDER_GROUP).iter_shared() {
            let Ok(mut body) = node.try_cast::<RigidBody3D>() else { continue };
            let id = body.instance_id().to_i64();
            alive.push(id);
            let p = body.get_global_position();
            let ride = match self.riders.get(&id) {
                Some(r) => *r,
                // New riders start out moving with whatever they're near.
                None => self.fresh_ride(&step, p),
            };
            let mut state = Carried { position: p, velocity: body.get_linear_velocity(), turn: None };
            let ride = self.carry(&step, ride, &mut state);
            self.riders.insert(id, ride);
            let mut t = body.get_global_transform();
            if let Some(turn) = state.turn {
                t.basis = (turn * t.basis).orthonormalized();
                let w = body.get_angular_velocity();
                body.set_angular_velocity(turn * w);
            }
            t.origin = state.position;
            body.set_global_transform(t);
            body.set_linear_velocity(state.velocity);
        }
        self.riders.retain(|id, _| alive.contains(id));

        if let Some(mut player) = player.clone() {
            let aboard = player
                .clone()
                .try_cast::<crate::player::PlayerController>()
                .ok()
                .and_then(|pc| pc.bind().aboard_ship());
            if let Some(ship) = aboard {
                // The ship carries the player; keep the player's frame in
                // step with the ship's so leaving it is seamless.
                if let Some(r) = self.riders.get(&ship.instance_id().to_i64()) {
                    self.frame = r.frame;
                    self.co_rotating = r.co_rotating;
                }
            } else {
                let ride = Ride { frame: self.frame, co_rotating: self.co_rotating };
                let mut state = Carried { position: player.get_global_position(), velocity: player.get_velocity(), turn: None };
                let ride = self.carry(&step, ride, &mut state);
                self.frame = ride.frame;
                self.co_rotating = ride.co_rotating;
                if let Some(turn) = state.turn {
                    let b = player.get_global_basis();
                    player.set_global_basis(turn * b);
                }
                player.set_global_position(state.position);
                player.set_velocity(state.velocity);
            }
            // Floating origin: keep the player near (0, 0, 0).
            let p = player.get_global_position();
            if (p.length() as f64) > data.recenter_m {
                let shift = p;
                self.origin = self.origin + DVec3::from_f32(shift);
                player.set_global_position(p - shift);
                self.shift_floating(shift);
            }
        }

        for (i, body) in self.bodies.iter_mut().enumerate() {
            body.set_position((new[i] - self.origin).to_f32());
            body.set_basis(eph.basis(&data, i, t1));
        }
        self.aim_sunlight(&new);
        self.stream_detail(&data);
    }
}

#[godot_api]
impl SolarSystem {
    /// Stand the player on the starting body (after bodies have built).
    #[func]
    fn place_player(&mut self) {
        let (Some(data), Some(mut player)) = (self.data.clone(), self.player()) else {
            return;
        };
        let Some(i) = data.bodies.iter().position(|b| b.id == data.start_body) else {
            godot_error!("SolarSystem: start body {} is not in the roster", data.start_body);
            return;
        };
        let body = self.bodies[i].clone();
        let center = body.get_global_position();
        let d = Vector3::new(data.start_dir.0, data.start_dir.1, data.start_dir.2);
        let world_dir = if d.length_squared() > 1e-6 {
            body.get_global_basis() * d.normalized()
        } else {
            // No fixed start: begin in daylight, with the sun part-way up the
            // sky so the ground has readable shadows.
            let sun = self.bodies.iter().zip(&data.bodies).find(|(_, b)| b.parent.is_none()).map(|(n, _)| n.get_global_position());
            let to_sun = sun.map_or(Vector3::UP, |s| (s - center).normalized());
            let side = to_sun.cross(Vector3::UP).normalized();
            let side = if side.is_finite() && side.length_squared() > 0.5 { side } else { Vector3::RIGHT };
            (to_sun * data.start_sun_angle_deg.to_radians().cos() + side * data.start_sun_angle_deg.to_radians().sin()).normalized()
        };
        let world_dir = self.gentle_spot(&body, world_dir, data.start_search_deg);
        let ground = body.bind().surface_radius_toward(center + world_dir);
        player.set_global_position(center + world_dir * (ground + 1.2));
        player.set_velocity(Vector3::ZERO);
        self.frame = Some(i);
        self.co_rotating = true;
    }

    /// Test hook: stand the player at `local_point` (body space) on body
    /// `body_id`, riding that body as if they had landed there.
    #[func]
    fn debug_place_player_on(&mut self, body_id: GString, local_point: Vector3) -> bool {
        let (Some(data), Some(mut player)) = (self.data.clone(), self.player()) else {
            return false;
        };
        let Some(i) = data.bodies.iter().position(|b| b.id == body_id.to_string()) else {
            return false;
        };
        let body = self.bodies[i].clone();
        player.set_global_position(body.get_global_transform() * local_point);
        player.set_velocity(Vector3::ZERO);
        self.frame = Some(i);
        self.co_rotating = true;
        true
    }

    /// The flattest dry spot within `search_deg` of `dir` (a world direction
    /// from the body's centre), so the run never starts on a cliff or in a lake.
    fn gentle_spot(&self, body: &Gd<CelestialBody>, dir: Vector3, search_deg: f32) -> Vector3 {
        if search_deg <= 0.0 {
            return dir;
        }
        let center = body.get_global_position();
        let b = body.bind();
        let radius = b.radius();
        let water = b.water_level().unwrap_or(f32::MIN);
        let side = super::body::basis_with_up(dir);
        let mut best = (f32::MAX, dir);
        for ring in 0..6 {
            let angle = search_deg.to_radians() * ring as f32 / 5.0;
            let steps = if ring == 0 { 1 } else { 12 };
            for k in 0..steps {
                let around = std::f32::consts::TAU * k as f32 / steps as f32;
                let tangent = side.col_a() * around.cos() + side.col_c() * around.sin();
                let d = (dir * angle.cos() + tangent * angle.sin()).normalized();
                let h = b.surface_radius_toward(center + d);
                if h - radius < water + 1.0 {
                    continue;
                }
                // Slope from four neighbours 3 m away.
                let step = 3.0 / radius;
                let slope = [side.col_a(), -side.col_a(), side.col_c(), -side.col_c()]
                    .iter()
                    .map(|t| (b.surface_radius_toward(center + (d + *t * step).normalized()) - h).abs())
                    .fold(0.0, f32::max);
                // Prefer flat ground, then staying near the sunny spot.
                let score = slope + angle * 2.0;
                if score < best.0 {
                    best = (score, d);
                }
            }
        }
        best.1
    }

    /// The live map's view of the system right now.
    pub fn map_snapshot(&self, orbit_points: usize) -> Option<MapSnapshot> {
        let (data, eph) = (self.data.as_ref()?, self.ephemeris.as_ref()?);
        let positions = eph.positions(data, self.clock);
        let bodies = data
            .bodies
            .iter()
            .enumerate()
            .map(|(i, b)| {
                let node = self.bodies[i].bind();
                let (radius, ring, is_star) = node.map_shape();
                let orbit = match (&b.orbit, eph.parent(i)) {
                    (Some(o), Some(p)) => orbit_loop(o, eph.phase(i), orbit_points)
                        .into_iter()
                        .map(|q| (positions[p] + q - self.origin).to_f32())
                        .collect(),
                    _ => Vec::new(),
                };
                MapBody {
                    id: b.id.clone(),
                    color: Color::from_rgb(b.map_color.0, b.map_color.1, b.map_color.2),
                    radius,
                    ring,
                    position: (positions[i] - self.origin).to_f32(),
                    orbit,
                    is_star,
                    is_moon: eph.parent(i).is_some_and(|p| data.bodies[p].orbit.is_some()),
                }
            })
            .collect();
        let player = self.player();
        Some(MapSnapshot {
            bodies,
            player: player.as_ref().map_or(Vector3::ZERO, |p| p.get_global_position()),
            player_forward: player.as_ref().map_or(Vector3::FORWARD, |p| -p.get_global_basis().col_c()),
            frame_body: self.frame.map(|i| data.bodies[i].id.clone()),
        })
    }

    /// Seconds on the run clock (drives every orbit).
    #[func]
    pub fn get_clock(&self) -> f64 {
        self.clock
    }

    /// Absolute position of the scene origin, for tests and the map.
    #[func]
    pub fn get_origin(&self) -> Vector3 {
        self.origin.to_f32()
    }

    /// Total pull of every body at a world point (tests and debugging).
    #[func]
    pub fn get_gravity_at(&self, point: Vector3) -> Vector3 {
        self.bodies.iter().map(|b| b.bind().gravity_at(point)).fold(Vector3::ZERO, |a, g| a + g)
    }

    /// Id of the body the player is riding ("" in open space).
    #[func]
    pub fn get_frame_body(&self) -> GString {
        match (self.frame, &self.data) {
            (Some(i), Some(d)) => GString::from(&d.bodies[i].id),
            _ => GString::new(),
        }
    }

    /// Jump the run clock (tests and debugging); bodies move on the next tick.
    #[func]
    pub fn set_clock(&mut self, seconds: f64) {
        self.clock = seconds;
    }

    /// Frame state for a rider seen for the first time: it moves with the
    /// body it's near, so its velocity is already relative to it.
    fn fresh_ride(&self, step: &Step, p: Vector3) -> Ride {
        let frame = self.dominant_body(p);
        let co_rotating = frame.is_some_and(|i| {
            let centre = (step.old[i] - self.origin).to_f32();
            (p - centre).length() < self.bodies[i].bind().radius() * (1.0 + step.data.co_rotate_altitude)
        });
        Ride { frame, co_rotating }
    }

    /// Moves one rider along with its frame body for this tick, converting
    /// its velocity when the frame changes (momentum is kept).
    fn carry(&self, step: &Step, mut ride: Ride, state: &mut Carried) -> Ride {
        let (data, eph, old, new) = (step.data, step.eph, step.old, step.new);
        let mut p = state.position;
        let mut v = state.velocity;
        let frame = self.dominant_body(p);
        let frame_velocity = |i: Option<usize>| i.map_or(Vector3::ZERO, |i| ((new[i] - old[i]).scale(1.0 / step.delta)).to_f32());
        if frame != ride.frame {
            // Keep absolute velocity: add the old frame's, remove the new one's.
            v += frame_velocity(ride.frame) - frame_velocity(frame);
            if ride.co_rotating {
                v += self.spin_velocity(data, eph, ride.frame, p, step.t0);
                ride.co_rotating = false;
            }
            ride.frame = frame;
        }
        if let Some(i) = frame {
            let center = (old[i] - self.origin).to_f32();
            let radius = self.bodies[i].bind().radius();
            let near = (p - center).length() < radius * (1.0 + data.co_rotate_altitude);
            if near != ride.co_rotating {
                // Entering or leaving the spinning air: swap the spin speed in or out.
                let spin = self.spin_velocity(data, eph, Some(i), p, step.t0);
                v += if near { -spin } else { spin };
                ride.co_rotating = near;
            }
            p += (new[i] - old[i]).to_f32();
            if ride.co_rotating {
                let turn = eph.basis(data, i, step.t1) * eph.basis(data, i, step.t0).inverse();
                let new_center = (new[i] - self.origin).to_f32();
                p = new_center + turn * (p - new_center);
                v = turn * v;
                state.turn = Some(turn);
            }
        }
        state.position = p;
        state.velocity = v;
        ride
    }

    fn player(&self) -> Option<Gd<CharacterBody3D>> {
        let tree = self.base().get_tree();
        tree.get_first_node_in_group(PLAYER_GROUP).and_then(|n| n.try_cast::<CharacterBody3D>().ok())
    }

    /// The body whose gravity is strongest at `p` (None in open space).
    fn dominant_body(&self, p: Vector3) -> Option<usize> {
        self.bodies
            .iter()
            .enumerate()
            .map(|(i, b)| (i, b.bind().gravity_at(p).length()))
            .filter(|(_, g)| *g > 0.0)
            .max_by(|a, b| a.1.total_cmp(&b.1))
            .map(|(i, _)| i)
    }

    /// Surface speed from body `i`'s spin at world point `p` (time `t`).
    fn spin_velocity(&self, data: &SystemData, eph: &Ephemeris, i: Option<usize>, p: Vector3, t: f64) -> Vector3 {
        let Some(i) = i else { return Vector3::ZERO };
        let s = &data.bodies[i].spin;
        if s.period_s == 0.0 {
            return Vector3::ZERO;
        }
        let axis = eph.basis(data, i, t) * Vector3::UP;
        let omega = axis * (std::f64::consts::TAU / s.period_s) as f32;
        omega.cross(p - self.bodies[i].get_global_position())
    }

    fn shift_floating(&mut self, shift: Vector3) {
        let tree = self.base().get_tree();
        for node in tree.get_nodes_in_group(FLOATING_GROUP).iter_shared() {
            if let Ok(mut n) = node.try_cast::<Node3D>() {
                let p = n.get_global_position();
                n.set_global_position(p - shift);
                if let Ok(mut ship) = n.try_cast::<crate::flight::Ship>() {
                    ship.bind_mut().origin_shifted(shift);
                }
            }
        }
    }

    /// Point the sunlight from the sun (the body at the system centre)
    /// toward the player, so every lit face faces the real sun.
    fn aim_sunlight(&mut self, positions: &[DVec3]) {
        let Some(sun_index) = self.data.as_ref().and_then(|d| d.bodies.iter().position(|b| b.parent.is_none())) else {
            return;
        };
        let sun = (positions[sun_index] - self.origin).to_f32();
        let target = self.player().map_or(Vector3::ZERO, |p| p.get_global_position());
        let dir = target - sun;
        if dir.length_squared() < 1.0 {
            return;
        }
        if let Some(light) = &mut self.sun_light {
            // A light shines along its -Z axis.
            let z = -dir.normalized();
            let up = if z.y.abs() > 0.99 { Vector3::RIGHT } else { Vector3::UP };
            let x = up.cross(z).normalized();
            light.set_global_basis(Basis::from_cols(x, z.cross(x), z));
        }
    }

    /// Ask nearby bodies for full detail (built in the background), and
    /// let bodies far behind drop it again. The gap between the two
    /// distances stops a body flickering between states at the boundary.
    fn stream_detail(&mut self, data: &SystemData) {
        let Some(p) = self.player().map(|p| p.get_global_position()) else { return };
        let frame = self.frame;
        for (i, body) in self.bodies.iter_mut().enumerate() {
            let reach = body.bind().radius() * data.detail_radii + data.detail_margin_m;
            let distance = body.get_global_position().distance_to(p);
            if distance < reach {
                body.bind_mut().request_detail();
            } else if distance > reach * data.release_factor && Some(i) != frame {
                body.bind_mut().release_detail();
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn data() -> SystemData {
        crate::data::parse::<SystemData>(include_str!("../../../../godot/data/system/solar_system.ron")).unwrap()
    }

    #[test]
    fn roster_parses_and_parents_come_first() {
        let d = data();
        Ephemeris::new(&d, 1).unwrap();
        for id in ["sun", "mercury", "venus", "earth", "moon", "mars", "phobos", "jupiter", "io", "europa", "saturn", "titan", "uranus", "neptune"] {
            assert!(d.bodies.iter().any(|b| b.id == id), "{id} missing");
        }
    }

    #[test]
    fn determinism_ephemeris_is_seeded_and_repeatable() {
        let d = data();
        let a = Ephemeris::new(&d, 77).unwrap().positions(&d, 123.0);
        let b = Ephemeris::new(&d, 77).unwrap().positions(&d, 123.0);
        let c = Ephemeris::new(&d, 78).unwrap().positions(&d, 123.0);
        assert_eq!(a, b);
        assert_ne!(a, c);
    }

    /// No two bodies ever overlap, over a long stretch of run time.
    #[test]
    fn bodies_never_pass_through_each_other() {
        let d = data();
        let radius = |b: &SystemBody| {
            let body = crate::data::parse::<super::super::body::BodyData>(
                &std::fs::read_to_string(format!("../../godot/{}", b.data.trim_start_matches("res://"))).unwrap(),
            )
            .unwrap();
            let top = body.clouds.as_ref().map_or(body.radius, |c| c.radius_m.max(c.ring_m.1));
            top as f64
        };
        let radii: Vec<f64> = d.bodies.iter().map(radius).collect();
        let eph = Ephemeris::new(&d, 5).unwrap();
        // Every 20 s for 3 hours (a long run is about 2 hours).
        for step in 0..540 {
            let p = eph.positions(&d, step as f64 * 20.0);
            for i in 0..p.len() {
                for j in i + 1..p.len() {
                    let gap = (p[i] - p[j]).length() - radii[i] - radii[j];
                    assert!(gap > 150.0, "{} and {} only {gap:.0} m apart at {} s", d.bodies[i].id, d.bodies[j].id, step * 20);
                }
            }
        }
    }

    #[test]
    fn orbit_loop_is_a_closed_ring_at_the_orbit_radius() {
        let d = data();
        let o = d.bodies.iter().find(|b| b.id == "mars").unwrap().orbit.clone().unwrap();
        let ring = orbit_loop(&o, 0.3, 64);
        assert_eq!(ring.len(), 64);
        for q in &ring {
            assert!((q.length() - o.radius_m).abs() < 1e-6);
        }
        // Neighbouring points are evenly spaced (no gaps in the drawn loop).
        let gap = |a: DVec3, b: DVec3| (a - b).length();
        let first = gap(ring[0], ring[1]);
        assert!((gap(ring[63], ring[0]) - first).abs() < 1e-6);
    }

    #[test]
    fn moons_stay_with_their_planet() {
        let d = data();
        let eph = Ephemeris::new(&d, 3).unwrap();
        let earth = d.bodies.iter().position(|b| b.id == "earth").unwrap();
        let moon = d.bodies.iter().position(|b| b.id == "moon").unwrap();
        let r = d.bodies[moon].orbit.as_ref().unwrap().radius_m;
        for t in [0.0, 500.0, 5000.0] {
            let p = eph.positions(&d, t);
            assert!(((p[moon] - p[earth]).length() - r).abs() < 1e-6);
        }
    }
}
