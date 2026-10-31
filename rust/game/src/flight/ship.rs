//! `Ship`: a flyable spacecraft (CLAUDE.md section 5). Phase 4 starts with the
//! starter lander: Apollo Lunar Module hull, belly hatch with an Outer
//! Wilds-style blue gravity ring, a rounded cabin with its own gravity, a
//! buckle-in seat, and the shared 6-axis flight model.
//!
//! Serves pillar 4 (the ship, the player and the planets share one physics),
//! pillar 1 (the ship rides its planet as it orbits and spins) and fun rule
//! 10 (game feel). Every number is in res://data/ships/<ship>.ron.
//!
//! How riding works: the hull is a `RigidBody3D` the solar system carries
//! along with whichever body it is near (like the player). While the player
//! is aboard (in the cabin or the lift), the ship carries the player along
//! with its own motion each tick, and the player collides with a static copy
//! of the hull ("CabinBody") that follows the ship. The player never touches
//! the rigid hull from inside, so standing in it can't stop it taking off.

use godot::classes::{PhysicsServer3D, 
    BoxShape3D, CapsuleShape3D, CollisionShape3D, IRigidBody3D, Input, Material, MeshInstance3D, Node3D, OmniLight3D,
    PhysicsMaterial, RigidBody3D, Shader, ShaderMaterial, StaticBody3D,
};
use godot::classes::physics_server_3d::BodyState;
use godot::classes::light_3d::Param;
use godot::classes::rigid_body_3d::CenterOfMassMode;
use godot::prelude::*;
use serde::Deserialize;

use super::meshes::{self, Bowl, WindowCut};
use super::model::{self, FlightData, PilotInput};
use crate::orbits::CelestialBody;
use crate::orbits::body::GRAVITY_GROUP;
use crate::orbits::system::{FLOATING_GROUP, RIDER_GROUP};
use crate::player::PlayerController;
use crate::player::controller::PLAYER_GROUP;

/// Every ship joins this group.
pub const SHIP_GROUP: &str = "ships";

/// Collision layers (bit values). Planets and props use the default layer 1.
pub const LAYER_WORLD: u32 = 1;
/// The static copy of a ship's hull that the player walks inside.
pub const LAYER_CABIN: u32 = 2;
/// Ship hulls (rigid bodies).
pub const LAYER_HULL: u32 = 4;
/// The player.
pub const LAYER_PLAYER: u32 = 8;

const CABIN_SHADER: &str = "res://shaders/cabin.gdshader";

type V3 = (f32, f32, f32);

fn v3((x, y, z): V3) -> Vector3 {
    Vector3::new(x, y, z)
}

#[derive(Debug, Clone, Deserialize)]
pub struct ShipData {
    pub model: String,
    pub model_scale: f32,
    pub model_offset: V3,
    pub engine_at: V3,
    pub mass_kg: f32,
    /// Ship-local centre of mass; low, in the heavy descent stage, so the
    /// lander doesn't tip over on slopes.
    pub center_of_mass: V3,
    pub angular_damp: f32,
    pub friction: f32,
    pub bounce: f32,
    pub flight: FlightData,
    pub cabin: CabinData,
    pub hatch: HatchData,
    pub boxes: Vec<BoxData>,
    pub capsules: Vec<CapsuleData>,
    pub wall_segments: usize,
    pub wall_thickness: f32,
    pub parking: ParkingData,
    pub cockpit: super::cockpit::CockpitData,
}

#[derive(Debug, Clone, Deserialize)]
pub struct CabinData {
    pub floor_y: f32,
    pub wall_top_y: f32,
    pub dome_top_y: f32,
    pub radius_x: f32,
    pub radius_z: f32,
    pub center_z: f32,
    pub gravity: f32,
    pub windows: Vec<WindowData>,
    pub seat_at: V3,
    pub seat_reach_m: f32,
    pub console_z: f32,
    pub light_energy: f32,
    pub light_range_m: f32,
    pub light_color: V3,
}

#[derive(Debug, Clone, Deserialize)]
pub struct WindowData {
    pub angle_deg: f32,
    pub half_width_deg: f32,
    pub bottom_y: f32,
    pub top_y: f32,
    pub taper: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct HatchData {
    pub at_x: f32,
    pub at_z: f32,
    pub radius: f32,
    pub collision_half: f32,
    pub ring_y: f32,
    pub ring_color: V3,
    pub ring_energy: f32,
    pub beam_radius: f32,
    pub lift_speed: f32,
    pub lift_accel: f32,
    pub center_pull: f32,
    /// The lift hands over to cabin gravity this far above the cabin floor...
    pub top_above_floor: f32,
    /// ...after drifting the player off the hole at this speed...
    pub top_drift_speed: f32,
    /// ...until their feet are this far past the hole's edge.
    pub clear_margin: f32,
    pub reach_m: f32,
    pub open_s: f32,
}

/// Where a ship parks itself at the start of a test scene: the flattest of
/// these spots ahead of the player.
#[derive(Debug, Clone, Deserialize)]
pub struct ParkingData {
    pub extra_distances_m: Vec<f32>,
    pub bearings_deg: Vec<f32>,
    /// Score added per step away from straight ahead, to break ties.
    pub order_penalty_m: f32,
    pub drop_height_m: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct BoxData {
    pub center: V3,
    pub size: V3,
    #[serde(default)]
    pub yaw_deg: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct CapsuleData {
    pub from: V3,
    pub to: V3,
    pub radius: f32,
}

impl CabinData {
    pub fn bowl(&self) -> Bowl {
        Bowl {
            floor_y: self.floor_y,
            wall_top_y: self.wall_top_y,
            dome_top_y: self.dome_top_y,
            radius_x: self.radius_x,
            radius_z: self.radius_z,
            center_z: self.center_z,
        }
    }

    pub fn window_cuts(&self) -> Vec<WindowCut> {
        self.windows
            .iter()
            .map(|w| WindowCut {
                // Data angles are + to the pilot's right, which is -X.
                angle: -w.angle_deg.to_radians(),
                half_width: w.half_width_deg.to_radians(),
                bottom_y: w.bottom_y,
                top_y: w.top_y,
                taper: w.taper,
            })
            .collect()
    }
}

/// Where the player is, relative to a ship.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Zone {
    Outside,
    /// In the gravity lift: under the ring or in the shaft.
    Lift,
    Cabin,
}

/// Pure zone test in ship space (`feet` = the player's lowest point).
pub fn classify(d: &ShipData, feet: Vector3, hatch_open: bool) -> Zone {
    let h = &d.hatch;
    let off = Vector2::new(feet.x - h.at_x, feet.z - h.at_z);
    let in_column = off.length() <= h.beam_radius;
    let bowl = d.cabin.bowl();
    // Above the hole, below this, the player is still in the lift.
    let lift_top = d.cabin.floor_y + 0.6;
    if in_column && feet.y > -1.5 && feet.y < lift_top {
        // Under the ring always counts; the shaft and the hole only while
        // the hatch is open (standing on the shut hatch is the cabin).
        if feet.y <= h.ring_y + 0.2 || hatch_open || feet.y < d.cabin.floor_y - 0.05 {
            return Zone::Lift;
        }
    }
    if bowl.contains(feet + Vector3::new(0.0, 0.05, 0.0), 0.1) {
        return Zone::Cabin;
    }
    Zone::Outside
}

/// The flight state shown on the dashboard and used by tests.
#[derive(Debug, Clone, Copy, Default)]
pub struct Telemetry {
    pub speed: f32,
    pub thrust: Vector3,
    pub effort: f32,
}

#[derive(GodotClass)]
#[class(base = RigidBody3D)]
pub struct Ship {
    /// The ship's data file.
    #[export(file = "*.ron")]
    data_path: GString,
    /// When above 0, the ship starts parked on the ground this far ahead of
    /// the player.
    #[export]
    spawn_ahead_m: f32,
    data: Option<ShipData>,
    fuel_s: f32,
    /// Hatch openness: 0 closed, 1 open, and where it is heading.
    hatch: f32,
    hatch_wanted: bool,
    /// Opened from inside by the player (stays open until closed).
    hatch_held: bool,
    zone: Zone,
    /// +1 lifting up into the cabin, -1 lowering out.
    lift_dir: f32,
    pilot: bool,
    turn_rate: Vector3,
    mouse: Vector2,
    telemetry: Telemetry,
    /// Transform after last tick's work (before the physics step).
    previous: Option<Transform3D>,
    placed: bool,
    ticks: u32,
    cabin_body: Option<Gd<StaticBody3D>>,
    door: Option<Gd<Node3D>>,
    door_shape: Option<Gd<CollisionShape3D>>,
    door_cabin_shape: Option<Gd<CollisionShape3D>>,
    ring_light: Option<Gd<OmniLight3D>>,
    engine_light: Option<Gd<OmniLight3D>>,
    cockpit: Option<super::cockpit::Cockpit>,
    base: Base<RigidBody3D>,
}

#[godot_api]
impl IRigidBody3D for Ship {
    fn init(base: Base<RigidBody3D>) -> Self {
        Self {
            data_path: GString::from("res://data/ships/starter_lander.ron"),
            spawn_ahead_m: 0.0,
            data: None,
            fuel_s: 0.0,
            hatch: 0.0,
            hatch_wanted: false,
            hatch_held: false,
            zone: Zone::Outside,
            lift_dir: 1.0,
            pilot: false,
            turn_rate: Vector3::ZERO,
            mouse: Vector2::ZERO,
            telemetry: Telemetry::default(),
            previous: None,
            placed: false,
            ticks: 0,
            cabin_body: None,
            door: None,
            door_shape: None,
            door_cabin_shape: None,
            ring_light: None,
            engine_light: None,
            cockpit: None,
            base,
        }
    }

    fn ready(&mut self) {
        let Some(d) = crate::data::try_load::<ShipData>(&self.data_path.to_string()) else {
            godot_error!("Ship: no data at {}", self.data_path);
            return;
        };
        self.fuel_s = d.flight.fuel_s;
        // After the solar system (-100), before the player (0).
        self.base_mut().set_physics_process_priority(-90);
        {
            let mut b = self.base_mut();
            b.add_to_group(SHIP_GROUP);
            b.add_to_group(RIDER_GROUP);
            b.add_to_group(FLOATING_GROUP);
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
            let mut material = PhysicsMaterial::new_gd();
            material.set_friction(d.friction);
            material.set_bounce(d.bounce);
            b.set_physics_material_override(&material);
        }
        self.build_model(&d);
        self.build_collision(&d);
        self.build_interior(&d);
        self.data = Some(d);
    }

    fn physics_process(&mut self, delta: f64) {
        let Some(d) = self.data.clone() else { return };
        let dt = delta as f32;
        self.ticks += 1;
        // Wait a couple of ticks: the solar system places the player first.
        if !self.placed && self.ticks >= 3 {
            self.placed = true;
            if self.spawn_ahead_m > 0.0 {
                self.park_ahead_of_player(&d);
            }
        }
        let player = self.player();
        // Carry an aboard player with the ship's own motion since last tick.
        let now = self.base().get_global_transform();
        if let (Some(prev), Some(p)) = (self.previous, player.as_ref())
            && self.zone != Zone::Outside
        {
            let moved = now * prev.affine_inverse();
            let mut p = p.clone();
            let t = p.get_global_transform();
            p.set_global_transform(Transform3D::new((moved.basis * t.basis).orthonormalized(), moved * t.origin));
            let v = p.get_velocity();
            p.set_velocity(moved.basis * v);
        }
        if let Some(mut cabin) = self.cabin_body.clone() {
            cabin.set_global_transform(now);
            // Push it to the physics server now: the player's move this tick
            // must collide with the floor where the ship is, not where it was.
            let rid = cabin.get_rid();
            PhysicsServer3D::singleton().body_set_state(rid, BodyState::TRANSFORM, &now.to_variant());
        }
        if let Some(p) = player {
            self.update_zone(&d, p);
        }
        self.update_hatch(&d, dt);
        self.fly(&d, dt);
        self.update_cockpit(&d);
        self.previous = Some(self.base().get_global_transform());
    }
}

#[godot_api]
impl Ship {
    /// Hatch openness, 0 (shut) to 1 (open).
    #[func]
    pub fn get_hatch_open(&self) -> f32 {
        self.hatch
    }

    #[func]
    pub fn is_pilot_aboard(&self) -> bool {
        self.pilot
    }

    /// "outside", "lift" or "cabin".
    #[func]
    pub fn get_player_zone(&self) -> GString {
        match self.zone {
            Zone::Outside => "outside",
            Zone::Lift => "lift",
            Zone::Cabin => "cabin",
        }
        .into()
    }

    #[func]
    pub fn get_fuel_fraction(&self) -> f32 {
        self.data.as_ref().map_or(0.0, |d| (self.fuel_s / d.flight.fuel_s).clamp(0.0, 1.0))
    }

    /// Speed relative to the body the ship is riding, in m/s.
    #[func]
    pub fn get_speed(&self) -> f32 {
        self.telemetry.speed
    }

    /// Height of the cabin floor above the footpads (tests, cameras).
    #[func]
    pub fn get_cabin_floor_y(&self) -> f32 {
        self.data.as_ref().map_or(0.0, |d| d.cabin.floor_y)
    }
}

impl Ship {
    pub fn data(&self) -> Option<&ShipData> {
        self.data.as_ref()
    }

    pub fn zone(&self) -> Zone {
        self.zone
    }

    /// Velocity of the ship's material at world point `p`, relative to the
    /// body it rides.
    pub fn velocity_at(&self, p: Vector3) -> Vector3 {
        let b = self.base();
        let com = b.get_global_transform() * b.get_center_of_mass();
        b.get_linear_velocity() + b.get_angular_velocity().cross(p - com)
    }

    /// Gravity (or lift) for the player at world point `p`, moving at `v`
    /// relative to the ship. None when the ship has no say there.
    pub fn gravity_for(&self, p: Vector3, v: Vector3, feet_below: f32) -> Option<Vector3> {
        let d = self.data.as_ref()?;
        let t = self.base().get_global_transform();
        let up = t.basis.col_b().normalized();
        match self.zone {
            Zone::Outside => None,
            Zone::Cabin => Some(-up * d.cabin.gravity),
            Zone::Lift => {
                let local = t.affine_inverse() * p;
                let feet = local - Vector3::new(0.0, feet_below, 0.0);
                let h = &d.hatch;
                let off = Vector2::new(local.x - h.at_x, local.z - h.at_z);
                let rel = t.basis.inverse() * v;
                let lift_top = d.cabin.floor_y + h.top_above_floor;
                let target = if self.lift_dir > 0.0 {
                    if feet.y < lift_top {
                        Vector3::new(0.0, h.lift_speed, 0.0)
                    } else {
                        // At the top: drift off the hole toward the cabin's middle.
                        let to_mid = Vector2::new(-off.x, d.cabin.center_z - h.at_z).normalized() * h.top_drift_speed;
                        Vector3::new(to_mid.x, 0.0, to_mid.y)
                    }
                } else {
                    // Going down: only the fall speed is controlled, so the
                    // player can walk out from under the ship once down.
                    let a = model::lift_accel(Vector3::new(0.0, rel.y, 0.0), Vector3::new(0.0, -h.lift_speed, 0.0), Vector2::ZERO, h.lift_accel, 0.0);
                    return Some(t.basis * a);
                };
                let pull = if feet.y >= lift_top { 0.0 } else { h.center_pull };
                let a = model::lift_accel(rel, target, off, h.lift_accel, pull);
                Some(t.basis * a)
            }
        }
    }

    /// Where the pilot's body sits (the player's transform when buckled).
    pub fn seat_transform(&self) -> Option<Transform3D> {
        let d = self.data.as_ref()?;
        let t = self.base().get_global_transform();
        // The player looks along its -Z; the ship flies along +Z.
        let facing = Basis::from_axis_angle(Vector3::UP, std::f32::consts::PI);
        Some(Transform3D::new(t.basis * facing, t * v3(d.cabin.seat_at)))
    }

    /// Where the player stands after unbuckling: beside the seat.
    pub fn stand_transform(&self) -> Option<Transform3D> {
        let d = self.data.as_ref()?;
        let t = self.base().get_global_transform();
        let facing = Basis::from_axis_angle(Vector3::UP, std::f32::consts::PI);
        let seat = v3(d.cabin.seat_at);
        Some(Transform3D::new(t.basis * facing, t * Vector3::new(seat.x - 1.0, d.cabin.floor_y + 0.95, seat.z)))
    }

    pub fn can_buckle(&self, p: Vector3) -> bool {
        let Some(d) = self.data.as_ref() else { return false };
        if self.zone != Zone::Cabin || self.pilot {
            return false;
        }
        let seat = self.base().get_global_transform() * v3(d.cabin.seat_at);
        seat.distance_to(p) <= d.cabin.seat_reach_m
    }

    /// Standing in the cabin close enough to the hatch to work it.
    pub fn can_work_hatch(&self, p: Vector3) -> bool {
        let Some(d) = self.data.as_ref() else { return false };
        if self.zone != Zone::Cabin {
            return false;
        }
        let local = self.base().get_global_transform().affine_inverse() * p;
        Vector2::new(local.x - d.hatch.at_x, local.z - d.hatch.at_z).length() <= d.hatch.reach_m
    }

    /// World points of the seat and the hatch, to tell which one the
    /// player is facing when both are in reach.
    pub fn seat_point(&self) -> Vector3 {
        let d = self.data.as_ref().map(|d| v3(d.cabin.seat_at)).unwrap_or_default();
        self.base().get_global_transform() * d
    }

    pub fn hatch_point(&self) -> Vector3 {
        let local = self.data.as_ref().map(|d| Vector3::new(d.hatch.at_x, d.cabin.floor_y, d.hatch.at_z)).unwrap_or_default();
        self.base().get_global_transform() * local
    }

    pub fn toggle_hatch(&mut self) {
        self.hatch_held = !self.hatch_wanted;
        self.hatch_wanted = !self.hatch_wanted;
    }

    pub fn set_pilot(&mut self, on: bool) {
        self.pilot = on;
        self.mouse = Vector2::ZERO;
    }

    pub fn add_mouse(&mut self, delta: Vector2) {
        self.mouse += delta;
    }

    /// The floating origin moved everything by `-shift`.
    pub fn origin_shifted(&mut self, shift: Vector3) {
        if let Some(prev) = self.previous.as_mut() {
            prev.origin -= shift;
        }
    }

    fn player(&self) -> Option<Gd<PlayerController>> {
        self.base().get_tree().get_first_node_in_group(PLAYER_GROUP).and_then(|n| n.try_cast::<PlayerController>().ok())
    }

    fn update_zone(&mut self, d: &ShipData, mut player: Gd<PlayerController>) {
        let t = self.base().get_global_transform();
        let p = player.get_global_position();
        let feet_below = player.bind().feet_below();
        let local_feet = t.affine_inverse() * p - Vector3::new(0.0, feet_below, 0.0);
        let open = self.hatch > 0.5;
        let mut zone = if player.bind().is_buckled() { Zone::Cabin } else { classify(d, local_feet, open) };
        // Rising out of the lift: stay in it until clear of the hole, or
        // cabin gravity drops the player straight back down the shaft.
        let off_hole = Vector2::new(local_feet.x - d.hatch.at_x, local_feet.z - d.hatch.at_z).length();
        if self.zone == Zone::Lift && zone == Zone::Cabin && self.lift_dir > 0.0 && off_hole < d.hatch.radius + d.hatch.clear_margin {
            zone = Zone::Lift;
        }
        if zone == self.zone {
            return;
        }
        let me = self.to_gd();
        match (self.zone, zone) {
            (Zone::Outside, _) => {
                // Boarding: the player's speed becomes relative to the ship.
                let v = player.get_velocity() - self.velocity_at(p);
                player.set_velocity(v);
                player.bind_mut().set_aboard(Some(me));
                self.lift_dir = 1.0;
            }
            (_, Zone::Outside) => {
                let v = player.get_velocity() + self.velocity_at(p);
                player.set_velocity(v);
                player.bind_mut().set_aboard(None);
            }
            (Zone::Cabin, Zone::Lift) => self.lift_dir = -1.0,
            _ => {}
        }
        self.zone = zone;
    }

    fn update_hatch(&mut self, d: &ShipData, dt: f32) {
        // Open while the player uses the lift; shut once they're clear of
        // the hole, unless they opened it from inside.
        let player_pos = self.player().map(|p| p.get_global_position());
        match self.zone {
            Zone::Lift => self.hatch_wanted = true,
            Zone::Cabin => {
                if !self.hatch_held {
                    let clear = player_pos.is_some_and(|p| {
                        let local = self.base().get_global_transform().affine_inverse() * p;
                        Vector2::new(local.x - d.hatch.at_x, local.z - d.hatch.at_z).length() > d.hatch.radius + 0.5
                    });
                    if clear {
                        self.hatch_wanted = false;
                    }
                }
            }
            Zone::Outside => {
                self.hatch_held = false;
                self.hatch_wanted = false;
            }
        }
        let step = if d.hatch.open_s > 0.0 { dt / d.hatch.open_s } else { 1.0 };
        self.hatch = if self.hatch_wanted { (self.hatch + step).min(1.0) } else { (self.hatch - step).max(0.0) };
        let open = self.hatch;
        let solid = open < 0.05;
        if let Some(mut door) = self.door.clone() {
            // Slides sideways under the floor.
            let e = open * open * (3.0 - 2.0 * open);
            door.set_position(Vector3::new(d.hatch.at_x + e * (d.hatch.radius * 2.0 + 0.1), d.cabin.floor_y - 0.06 - e * 0.12, d.hatch.at_z));
        }
        for shape in [self.door_shape.clone(), self.door_cabin_shape.clone()].into_iter().flatten() {
            let mut shape = shape;
            if shape.is_disabled() == solid {
                shape.set_deferred("disabled", &(!solid).to_variant());
            }
        }
        if let Some(mut light) = self.ring_light.clone() {
            let pulse = if self.zone == Zone::Lift { 1.6 } else { 1.0 };
            light.set_param(Param::ENERGY, d.hatch.ring_energy * pulse);
        }
    }

    fn fly(&mut self, d: &ShipData, dt: f32) {
        let position = self.base().get_global_position();
        let basis = self.base().get_global_basis();
        let gravity = self.total_gravity(position);
        let mut v = self.base().get_linear_velocity() + gravity * dt;
        let mut thrust_local = Vector3::ZERO;
        let mut effort = 0.0;
        if self.pilot {
            let input = self.read_input(d);
            let has_fuel = self.fuel_s > 0.0;
            if has_fuel {
                let (a, e) = model::thrust(&d.flight, &input);
                thrust_local = a;
                effort = e;
                v += basis * a * dt;
            }
            if input.match_velocity && has_fuel {
                let fix = model::match_velocity(v, d.flight.match_velocity_accel, dt);
                if fix.length_squared() > 0.0 {
                    effort = effort.max(0.5);
                }
                v += fix;
            }
            self.fuel_s = (self.fuel_s - effort * dt).max(0.0);
            // Sitting still on the ground with the stick idle, let the legs
            // settle on their own; flight assist holding the attitude there
            // makes the ship skate downhill on one pad.
            let idle = input.move_stick == Vector2::ZERO && input.vertical == 0.0 && input.pitch == 0.0 && input.yaw == 0.0 && input.roll == 0.0;
            let resting = idle && v.length() < d.flight.landed_speed && self.grounded();
            if !resting {
                let current = basis.inverse() * self.base().get_angular_velocity();
                let w = model::step_turn_rate(&d.flight, current, &input, dt);
                self.base_mut().set_angular_velocity(basis * w);
                self.turn_rate = w;
            }
        }
        self.base_mut().set_linear_velocity(v);
        self.telemetry = Telemetry { speed: v.length(), thrust: thrust_local, effort };
        if let Some(mut light) = self.engine_light.clone() {
            let up = (thrust_local.y / d.flight.up.max(0.01)).max(0.0);
            light.set_param(Param::ENERGY, up * 6.0);
        }
    }

    /// Feed the dash, light the mini ship's thrusters, and handle the
    /// cockpit buttons (Y: cameras, F4: dash style).
    fn update_cockpit(&mut self, d: &ShipData) {
        let aboard = self.zone != Zone::Outside;
        let readout = self.dash_readout(d);
        let player_camera = self.player().and_then(|p| p.try_get_node_as::<godot::classes::Camera3D>("Head/Camera3D"));
        let names: Vec<String> = d.cockpit.cameras.iter().map(|c| self.base().tr(&c.name_key).to_string()).collect();
        let input = Input::singleton();
        let thrust = self.telemetry.thrust;
        let scale = Vector3::new(d.flight.strafe.max(0.01), d.flight.up.max(0.01), d.flight.forward.max(0.01));
        let pilot = self.pilot;
        let Some(cp) = self.cockpit.as_mut() else { return };
        cp.viewport.set_update_mode(if aboard { godot::classes::sub_viewport::UpdateMode::ALWAYS } else { godot::classes::sub_viewport::UpdateMode::DISABLED });
        cp.dash.bind_mut().set_readout(readout);
        cp.show_thrust(thrust / scale, &d.cockpit.mini_ship);
        if aboard && input.is_action_just_pressed("cycle_dash") {
            cp.dash.bind_mut().cycle_style();
        }
        if pilot && input.is_action_just_pressed("cameras_screen") {
            cp.next_view(player_camera, &names);
        } else if !pilot {
            cp.reset_view(player_camera, &names);
        }
    }

    fn dash_readout(&self, d: &ShipData) -> super::dash::DashReadout {
        let t = self.base().get_global_transform();
        let v = self.base().get_linear_velocity();
        let nearest = self
            .base()
            .get_tree()
            .get_nodes_in_group(GRAVITY_GROUP)
            .iter_shared()
            .filter_map(|n| n.try_cast::<CelestialBody>().ok())
            .map(|b| {
                let r = b.bind().surface_radius_toward(t.origin);
                let dist = (t.origin - b.get_global_position()).length();
                (dist - r, b)
            })
            .min_by(|a, b| a.0.total_cmp(&b.0));
        let (altitude, body_key, up) = match &nearest {
            Some((alt, b)) if *alt < b.bind().radius() * 3.0 => {
                let up = (t.origin - b.get_global_position()).normalized();
                (Some(*alt), Some(format!("BODY_{}", b.get_name().to_string().to_uppercase())), up)
            }
            _ => (None, None, t.basis.col_b().normalized()),
        };
        // Ship space with x toward the pilot's right (-X), y up, z forward.
        let local = t.basis.inverse() * v;
        let oxygen = self.player().map_or(1.0, |p| p.bind().get_oxygen_fraction());
        super::dash::DashReadout {
            speed: v.length(),
            vertical: v.dot(up),
            altitude,
            body_key,
            drift: Vector3::new(-local.x, local.y, local.z),
            fuel: self.get_fuel_fraction(),
            oxygen,
            matching: self.pilot && Input::singleton().is_action_pressed("match_velocity") && self.fuel_s > 0.0,
            landed: v.length() < d.flight.landed_speed && self.grounded(),
        }
    }

    /// Touching anything (ground, a rock, another ship).
    fn grounded(&self) -> bool {
        !self.base().get_colliding_bodies().is_empty()
    }

    fn read_input(&mut self, d: &ShipData) -> PilotInput {
        let input = Input::singleton();
        let stick = input.get_vector("move_left", "move_right", "move_back", "move_forward");
        let look = input.get_vector("look_left", "look_right", "look_down", "look_up");
        let rate = d.flight.mouse_turn_deg / d.flight.yaw_rate_deg.max(1.0);
        let mouse = self.mouse;
        self.mouse = Vector2::ZERO;
        PilotInput {
            move_stick: stick,
            vertical: input.get_action_strength("jump") - input.get_action_strength("crouch"),
            pitch: (look.y - mouse.y * rate).clamp(-1.0, 1.0),
            yaw: (look.x + mouse.x * rate).clamp(-1.0, 1.0),
            roll: input.get_action_strength("roll_right") - input.get_action_strength("roll_left"),
            match_velocity: input.is_action_pressed("match_velocity"),
        }
    }

    fn total_gravity(&self, position: Vector3) -> Vector3 {
        self.base()
            .get_tree()
            .get_nodes_in_group(GRAVITY_GROUP)
            .iter_shared()
            .filter_map(|n| n.try_cast::<CelestialBody>().ok())
            .map(|b| b.bind().gravity_at(position))
            .fold(Vector3::ZERO, |a, g| a + g)
    }

    /// Park on the ground ahead of the player, upright, with the hatch
    /// side toward them.
    fn park_ahead_of_player(&mut self, d: &ShipData) {
        let Some(player) = self.player() else { return };
        let p = player.get_global_position();
        let tree = self.base().get_tree();
        let Some(body) = tree
            .get_nodes_in_group(GRAVITY_GROUP)
            .iter_shared()
            .filter_map(|n| n.try_cast::<CelestialBody>().ok())
            .min_by(|a, b| a.get_global_position().distance_to(p).total_cmp(&b.get_global_position().distance_to(p)))
        else {
            return;
        };
        let centre = body.get_global_position();
        let up0 = (p - centre).normalized();
        let forward = -player.get_global_basis().col_c();
        let ahead0 = (forward - up0 * forward.dot(up0)).normalized();
        let r = body.bind().radius().max(1.0);
        let reach = d.capsules.iter().map(|c| v3(c.from).length().max(v3(c.to).length())).fold(0.0, f32::max);
        // Try spots ahead and around, and take the flattest (footpads at
        // the most even heights); ties go to the spot straight ahead.
        let mut best: Option<(f32, Vector3, Basis, f32)> = None;
        for (k, extra) in d.parking.extra_distances_m.iter().enumerate() {
            for (j, bearing) in d.parking.bearings_deg.iter().enumerate() {
                let ahead = Basis::from_axis_angle(up0, bearing.to_radians()) * ahead0;
                let angle = (self.spawn_ahead_m + extra) / r;
                let up = (up0 * angle.cos() + ahead * angle.sin()).normalized();
                // The ship's back (where the hatch is) faces the player.
                let tangent = (ahead - up * ahead.dot(up)).normalized();
                let basis = model::facing_basis(up, tangent);
                let mut heights = Vec::new();
                for q in 0..5 {
                    let offset = if q == 0 {
                        Vector3::ZERO
                    } else {
                        let a = std::f32::consts::FRAC_PI_2 * q as f32;
                        basis * Vector3::new(a.cos() * reach, 0.0, a.sin() * reach)
                    };
                    heights.push(body.bind().surface_radius_toward(centre + up * r + offset));
                }
                let hi = heights.iter().copied().fold(f32::MIN, f32::max);
                let lo = heights.iter().copied().fold(f32::MAX, f32::min);
                // Water counts as unusable.
                let wet = body.bind().water_level().is_some_and(|w| lo < r + w);
                let score = (hi - lo) + if wet { 1000.0 } else { 0.0 } + (k + j) as f32 * d.parking.order_penalty_m;
                if best.as_ref().is_none_or(|b| score < b.0) {
                    best = Some((score, up, basis, hi));
                }
            }
        }
        let Some((_, up, basis, ground)) = best else { return };
        let at = centre + up * (ground + d.parking.drop_height_m);
        self.base_mut().set_global_transform(Transform3D::new(basis, at));
        self.base_mut().set_linear_velocity(Vector3::ZERO);
        self.base_mut().set_angular_velocity(Vector3::ZERO);
        self.previous = None;
    }

    fn build_model(&mut self, d: &ShipData) {
        let Some(mut model) = crate::orbits::landmarks::instantiate(&d.model, "Hull") else {
            godot_error!("Ship: could not load {}", d.model);
            return;
        };
        model.set_scale(Vector3::splat(d.model_scale));
        model.set_position(v3(d.model_offset));
        self.base_mut().add_child(&model);
        self.clip_hull_interior(d, &model);
    }

    /// The hull model is solid inside; hide whatever pokes into the cabin
    /// and the hatch shaft so the interior reads clean.
    fn clip_hull_interior(&self, d: &ShipData, model: &Gd<Node3D>) {
        let c = &d.cabin;
        let h = &d.hatch;
        let ship_from_model = model.get_transform();
        let parts = model.find_children_ex("*").type_("MeshInstance3D").recursive(true).owned(false).done();
        for part in parts.iter_shared() {
            let Ok(part) = part.try_cast::<MeshInstance3D>() else { continue };
            // Part transform relative to the model root.
            let mut rel = part.get_transform();
            let mut parent = part.get_parent_node_3d();
            while let Some(p) = parent {
                if p == *model {
                    break;
                }
                rel = p.get_transform() * rel;
                parent = p.get_parent_node_3d();
            }
            let to_ship = ship_from_model * rel;
            let m = Projection::from(to_ship);
            for i in 0..part.get_surface_override_material_count() {
                if let Some(mut mat) = part.get_surface_override_material(i).and_then(|m| m.try_cast::<ShaderMaterial>().ok()) {
                    mat.set_shader_parameter("clip_cabin", &true.to_variant());
                    mat.set_shader_parameter("part_to_ship", &m.to_variant());
                    mat.set_shader_parameter("cabin_center_z", &c.center_z.to_variant());
                    mat.set_shader_parameter("cabin_radii", &Vector2::new(c.radius_x + 0.02, c.radius_z + 0.02).to_variant());
                    mat.set_shader_parameter("cabin_y", &Vector2::new(c.floor_y - 0.3, c.dome_top_y + 0.05).to_variant());
                    mat.set_shader_parameter("shaft_at", &Vector2::new(h.at_x, h.at_z).to_variant());
                    mat.set_shader_parameter("shaft_radius", &(h.radius + 0.02).to_variant());
                    mat.set_shader_parameter("shaft_y", &Vector2::new(h.ring_y - 0.05, c.floor_y).to_variant());
                }
            }
        }
    }

    /// The hull's collision, twice: on the rigid body (planets and other
    /// ships hit it) and on the static cabin copy (the player walks in it).
    fn build_collision(&mut self, d: &ShipData) {
        let mut cabin = StaticBody3D::new_alloc();
        cabin.set_name("CabinBody");
        cabin.set_as_top_level(true);
        cabin.set_collision_layer(LAYER_CABIN);
        cabin.set_collision_mask(0);
        let shapes = hull_shapes(d);
        for (i, (shape, transform)) in shapes.iter().enumerate() {
            for (target, name) in [(0, "Hull"), (1, "Cabin")] {
                let mut c = CollisionShape3D::new_alloc();
                c.set_name(&format!("{name}Shape{i}"));
                c.set_shape(shape);
                c.set_transform(*transform);
                if target == 0 {
                    self.base_mut().add_child(&c);
                } else {
                    cabin.add_child(&c);
                }
            }
        }
        // The hatch door: solid while shut.
        let mut door = BoxShape3D::new_gd();
        let s = d.hatch.collision_half * 2.0 + 0.1;
        door.set_size(Vector3::new(s, 0.3, s));
        let at = Transform3D::new(Basis::IDENTITY, Vector3::new(d.hatch.at_x, d.cabin.floor_y - 0.15, d.hatch.at_z));
        for target in 0..2 {
            let mut c = CollisionShape3D::new_alloc();
            c.set_name("DoorShape");
            c.set_shape(&door);
            c.set_transform(at);
            if target == 0 {
                self.base_mut().add_child(&c);
                self.door_shape = Some(c);
            } else {
                cabin.add_child(&c);
                self.door_cabin_shape = Some(c);
            }
        }
        self.base_mut().add_child(&cabin);
        self.cabin_body = Some(cabin);
    }

    fn build_interior(&mut self, d: &ShipData) {
        let c = &d.cabin;
        let h = &d.hatch;
        let bowl = c.bowl();
        let mut interior = Node3D::new_alloc();
        interior.set_name("Interior");
        let painted = |kind: i32, base: Color| -> Option<Gd<Material>> {
            let shader = try_load::<Shader>(CABIN_SHADER).ok()?;
            let mut m = ShaderMaterial::new_gd();
            m.set_shader(&shader);
            m.set_shader_parameter("kind", &kind.to_variant());
            m.set_shader_parameter("base_color", &base.to_variant());
            Some(m.upcast())
        };
        let mut add = |name: &str, geo: &meshes::Geo, at: Transform3D, material: Option<Gd<Material>>| {
            let mut mi = MeshInstance3D::new_alloc();
            mi.set_name(name);
            mi.set_mesh(&geo.to_array_mesh());
            mi.set_transform(at);
            if let Some(m) = material {
                mi.set_material_override(&m);
            }
            interior.add_child(&mi);
            mi
        };
        let wall = Color::from_rgb(0.86, 0.83, 0.76);
        let floor = Color::from_rgb(0.30, 0.33, 0.38);
        let trim = Color::from_rgb(0.62, 0.64, 0.68);
        let seat_col = Color::from_rgb(0.72, 0.36, 0.20);
        let console = Color::from_rgb(0.34, 0.38, 0.44);
        add("Shell", &bowl.shell(192, &c.window_cuts()), Transform3D::IDENTITY, painted(0, wall));
        let mut frames = meshes::Geo::default();
        for w in c.window_cuts() {
            frames.append(&bowl.window_frame(&w, 0.075), Transform3D::IDENTITY);
        }
        add("WindowFrames", &frames, Transform3D::IDENTITY, painted(2, Color::from_rgb(0.42, 0.45, 0.5)));
        add("Floor", &bowl.floor(Vector2::new(h.at_x, h.at_z), h.radius, 64, 8), Transform3D::IDENTITY, painted(1, floor));
        let hatch_at = Vector3::new(h.at_x, c.floor_y, h.at_z);
        add("HatchRim", &meshes::flange(h.radius - 0.02, h.radius + 0.12, 0.05, 64), Transform3D::new(Basis::IDENTITY, hatch_at + Vector3::new(0.0, 0.005, 0.0)), painted(2, trim));
        add("Shaft", &meshes::tube_inside(h.radius, h.ring_y, c.floor_y, 48, 6), Transform3D::new(Basis::IDENTITY, Vector3::new(h.at_x, 0.0, h.at_z)), painted(3, trim));
        let ring = add("GravityRing", &meshes::torus(h.radius + 0.06, 0.07, 64, 12), Transform3D::new(Basis::IDENTITY, Vector3::new(h.at_x, h.ring_y, h.at_z)), painted(4, rgb(h.ring_color)));
        drop(ring);
        let door = add("HatchDoor", &meshes::rounded_disc(h.radius + 0.04, 0.12, 64), Transform3D::new(Basis::IDENTITY, hatch_at - Vector3::new(0.0, 0.06, 0.0)), painted(2, trim));
        // Seat: cushion, back and headrest, with arm rests.
        let seat = v3(c.seat_at);
        let seat_floor = Vector3::new(seat.x, c.floor_y, seat.z);
        let parts: [(Vector3, Vector3, f32, f32); 6] = [
            // (centre above floor, size, radius, back tilt degrees)
            (Vector3::new(0.0, 0.48, 0.05), Vector3::new(0.78, 0.18, 0.72), 0.08, 0.0),
            (Vector3::new(0.0, 0.98, -0.34), Vector3::new(0.78, 0.92, 0.16), 0.07, -12.0),
            (Vector3::new(0.0, 1.58, -0.46), Vector3::new(0.42, 0.28, 0.14), 0.07, -12.0),
            (Vector3::new(0.47, 0.70, 0.02), Vector3::new(0.12, 0.10, 0.62), 0.045, 0.0),
            (Vector3::new(-0.47, 0.70, 0.02), Vector3::new(0.12, 0.10, 0.62), 0.045, 0.0),
            (Vector3::new(0.0, 0.2, -0.05), Vector3::new(0.32, 0.4, 0.32), 0.06, 0.0),
        ];
        for (i, (at, size, r, tilt)) in parts.iter().enumerate() {
            let basis = Basis::from_axis_angle(Vector3::RIGHT, tilt.to_radians());
            let col = if i == 5 { trim } else { seat_col };
            add(&format!("Seat{i}"), &meshes::rounded_box(*size, *r, 5), Transform3D::new(basis, seat_floor + *at), painted(5, col));
        }
        // Console: a slanted desk under the windows.
        let desk = Vector3::new(0.0, c.floor_y + 1.05, c.console_z);
        add("Console", &meshes::rounded_box(Vector3::new(1.9, 0.16, 0.7), 0.07, 6), Transform3D::new(Basis::from_axis_angle(Vector3::RIGHT, (-28f32).to_radians()), desk), painted(6, console));
        add("ConsoleStand", &meshes::rounded_box(Vector3::new(0.9, 1.0, 0.4), 0.1, 6), Transform3D::new(Basis::IDENTITY, Vector3::new(0.0, c.floor_y + 0.5, c.console_z + 0.15)), painted(6, console));
        let desk_t = Transform3D::new(Basis::from_axis_angle(Vector3::RIGHT, (-28f32).to_radians()), desk);
        let mut root: Gd<Node3D> = self.to_gd().upcast();
        let cockpit = super::cockpit::build(&mut interior, &mut root, &d.cockpit, desk_t, 0.08, &d.model, d.model_scale, v3(d.model_offset));
        // Lights: a warm cabin lamp, the blue ring, and the engine glow.
        let mut lamp = OmniLight3D::new_alloc();
        lamp.set_name("CabinLight");
        lamp.set_color(rgb(c.light_color));
        lamp.set_param(Param::ENERGY, c.light_energy);
        lamp.set_param(Param::RANGE, c.light_range_m);
        lamp.set_position(Vector3::new(0.0, c.dome_top_y - 0.5, c.center_z));
        interior.add_child(&lamp);
        let mut ring_light = OmniLight3D::new_alloc();
        ring_light.set_name("RingLight");
        ring_light.set_color(rgb(h.ring_color));
        ring_light.set_param(Param::ENERGY, h.ring_energy);
        ring_light.set_param(Param::RANGE, 3.5);
        ring_light.set_position(Vector3::new(h.at_x, h.ring_y - 0.4, h.at_z));
        interior.add_child(&ring_light);
        let mut engine = OmniLight3D::new_alloc();
        engine.set_name("EngineLight");
        engine.set_color(Color::from_rgb(1.0, 0.62, 0.3));
        engine.set_param(Param::ENERGY, 0.0);
        engine.set_param(Param::RANGE, 14.0);
        engine.set_position(v3(d.engine_at) - Vector3::new(0.0, 0.8, 0.0));
        interior.add_child(&engine);
        self.base_mut().add_child(&interior);
        self.door = Some(door.upcast());
        self.ring_light = Some(ring_light);
        self.engine_light = Some(engine);
        self.cockpit = Some(cockpit);
    }
}

fn rgb((r, g, b): V3) -> Color {
    Color::from_rgb(r, g, b)
}

/// Every hull collision shape with its transform in ship space.
pub fn hull_shapes(d: &ShipData) -> Vec<(Gd<godot::classes::Shape3D>, Transform3D)> {
    let mut out: Vec<(Gd<godot::classes::Shape3D>, Transform3D)> = Vec::new();
    for b in &d.boxes {
        let mut s = BoxShape3D::new_gd();
        s.set_size(v3(b.size));
        out.push((s.upcast(), Transform3D::new(Basis::from_axis_angle(Vector3::UP, b.yaw_deg.to_radians()), v3(b.center))));
    }
    for c in &d.capsules {
        let (a, b) = (v3(c.from), v3(c.to));
        let mut s = CapsuleShape3D::new_gd();
        s.set_radius(c.radius);
        s.set_height((a - b).length() + c.radius * 2.0);
        // Capsules run along +Y; turn +Y onto a->b.
        let axis = (b - a).normalized();
        let basis = model::facing_basis(axis, if axis.y.abs() < 0.9 { Vector3::UP } else { Vector3::RIGHT });
        out.push((s.upcast(), Transform3D::new(basis, (a + b) * 0.5)));
    }
    for (center, size, yaw) in cabin_walls(d) {
        let mut s = BoxShape3D::new_gd();
        s.set_size(size);
        out.push((s.upcast(), Transform3D::new(Basis::from_axis_angle(Vector3::UP, yaw), center)));
    }
    out
}

/// Boxes around the cabin bowl: a ring of walls up to the wall top, and an
/// inner ring under the dome. (centre, size, yaw in radians).
pub fn cabin_walls(d: &ShipData) -> Vec<(Vector3, Vector3, f32)> {
    let c = &d.cabin;
    let n = d.wall_segments.max(6);
    let mut out = Vec::new();
    for (scale, y0, y1) in [(1.0, c.floor_y, c.wall_top_y), (0.8, c.wall_top_y, c.dome_top_y)] {
        let (rx, rz) = (c.radius_x * scale, c.radius_z * scale);
        for i in 0..n {
            let a0 = std::f32::consts::TAU * i as f32 / n as f32;
            let a1 = std::f32::consts::TAU * (i + 1) as f32 / n as f32;
            let p = |a: f32| Vector2::new(rx * a.sin(), c.center_z + rz * a.cos());
            let (p0, p1) = (p(a0), p(a1));
            let along = p1 - p0;
            let outward = Vector2::new(along.y, -along.x).normalized();
            // Make sure "outward" points away from the middle.
            let mid = (p0 + p1) * 0.5;
            let outward = if outward.dot(mid - Vector2::new(0.0, c.center_z)) < 0.0 { -outward } else { outward };
            let centre = mid + outward * d.wall_thickness * 0.5;
            let yaw = (-along.y).atan2(along.x);
            out.push((
                Vector3::new(centre.x, (y0 + y1) * 0.5, centre.y),
                Vector3::new(along.length() + 0.12, y1 - y0, d.wall_thickness),
                yaw,
            ));
        }
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;

    fn data() -> ShipData {
        crate::data::parse(include_str!("../../../../godot/data/ships/starter_lander.ron")).unwrap()
    }

    #[test]
    fn starter_lander_data_parses() {
        let d = data();
        assert!(d.model_scale > 0.0);
        assert_eq!(d.cabin.windows.len(), 2);
    }

    #[test]
    fn zones_under_the_ring_in_the_shaft_and_in_the_cabin() {
        let d = data();
        let h = &d.hatch;
        // Standing on the ground under the ring: the lift has you.
        assert_eq!(classify(&d, Vector3::new(h.at_x, 0.0, h.at_z), false), Zone::Lift);
        // Halfway up the shaft with the hatch open.
        assert_eq!(classify(&d, Vector3::new(h.at_x, 4.0, h.at_z), true), Zone::Lift);
        // Standing on the cabin floor by the seat.
        let seat = v3(d.cabin.seat_at);
        assert_eq!(classify(&d, Vector3::new(seat.x + 1.0, d.cabin.floor_y, seat.z), false), Zone::Cabin);
        // Standing on the shut hatch is the cabin, not the lift.
        assert_eq!(classify(&d, Vector3::new(h.at_x, d.cabin.floor_y, h.at_z), false), Zone::Cabin);
        // On the open hatch, you drop into the lift.
        assert_eq!(classify(&d, Vector3::new(h.at_x, d.cabin.floor_y, h.at_z), true), Zone::Lift);
        // Out on the ground beside the ship.
        assert_eq!(classify(&d, Vector3::new(9.0, 0.0, 0.0), false), Zone::Outside);
        // Standing on top of the descent stage, outside the cabin.
        assert_eq!(classify(&d, Vector3::new(3.2, 5.6, -3.2), false), Zone::Outside);
    }

    #[test]
    fn a_standing_player_fits_under_the_ring() {
        let d = data();
        // The player capsule is 1.8 m tall; the belly sits above it.
        assert!(d.hatch.ring_y > 1.8);
    }

    #[test]
    fn seat_and_hatch_sit_inside_the_cabin_and_apart() {
        let d = data();
        let bowl = d.cabin.bowl();
        let seat = v3(d.cabin.seat_at);
        assert!(bowl.contains(Vector3::new(seat.x, d.cabin.floor_y + 0.1, seat.z), 0.0));
        let hatch = Vector3::new(d.hatch.at_x, d.cabin.floor_y + 0.1, d.hatch.at_z);
        let far = Vector3::new(d.hatch.at_x, d.cabin.floor_y + 0.1, d.hatch.at_z - d.hatch.radius - 0.05);
        assert!(bowl.contains(hatch, 0.0) && bowl.contains(far, 0.0), "the hatch hole fits inside the floor");
        assert!(Vector2::new(seat.x - d.hatch.at_x, seat.z - d.hatch.at_z).length() > d.hatch.radius + 0.8);
    }

    #[test]
    fn cabin_walls_surround_the_bowl() {
        let d = data();
        let walls = cabin_walls(&d);
        assert_eq!(walls.len(), d.wall_segments * 2);
        for (centre, _, _) in &walls[..d.wall_segments] {
            // Each lower wall box sits just outside the bowl.
            assert!(!d.cabin.bowl().contains(Vector3::new(centre.x, d.cabin.floor_y + 1.0, centre.z), 0.0));
        }
    }
}
