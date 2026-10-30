//! First-person suited player: walks on any body using point gravity, flies
//! a jetpack with real inertia, matches velocity, clips on a safety tether,
//! and breathes from the suit's oxygen.
//!
//! Serves pillar 4 (everything is physical: the player obeys the same gravity
//! and inertia as everything else), pillar 2 (real danger: air runs out) and
//! fun rules 10 (game feel) and 3 (controls learnable in 5 minutes).
//! Values come from res://data/player/movement.ron, suit.ron and interactables.ron.

use godot::classes::input::MouseMode;
use godot::classes::light_3d::Param;
use godot::classes::{
    CapsuleShape3D, CharacterBody3D, CollisionShape3D, ICharacterBody3D, Input, InputEvent,
    InputEventMouseButton, InputEventMouseMotion, Node3D, OmniLight3D,
};
use godot::prelude::*;
use serde::Deserialize;

use super::motion;
use super::suit::{SuitData, SuitInput, SuitState};
use super::supply::SUPPLY_GROUP;
use super::tether::{TETHER_GROUP, TetherLine};
use crate::items::oxygen_tank::TANK_GROUP;
use crate::orbits::CelestialBody;
use crate::orbits::body::GRAVITY_GROUP;

/// Group the player joins so tools and (later) AI can find it.
pub const PLAYER_GROUP: &str = "player";

const DATA_PATH: &str = "res://data/player/movement.ron";
const DATA_EMBEDDED: &str = include_str!("../../../../godot/data/player/movement.ron");
const SUIT_PATH: &str = "res://data/player/suit.ron";
const SUIT_EMBEDDED: &str = include_str!("../../../../godot/data/player/suit.ron");
const INTERACT_PATH: &str = "res://data/player/interactables.ron";
const INTERACT_EMBEDDED: &str = include_str!("../../../../godot/data/player/interactables.ron");

/// Below this gravity (m/s^2) the player counts as in zero-g and stops
/// turning to stay upright.
const ZERO_G_THRESHOLD: f32 = 0.01;

#[derive(Debug, Clone, Deserialize)]
pub struct MovementData {
    pub walk_speed: f32,
    pub crouch_speed: f32,
    pub ground_acceleration: f32,
    pub air_acceleration: f32,
    pub jump_speed: f32,
    pub max_fall_speed: f32,
    pub capsule_radius: f32,
    pub capsule_height: f32,
    pub eye_height: f32,
    pub crouch_eye_height: f32,
    pub crouch_blend_speed: f32,
    /// How fast the body turns to match changing gravity, in degrees per second.
    pub align_speed_deg: f32,
    pub stick_look_speed_deg: f32,
    pub mouse_sensitivity_deg: f32,
    pub pitch_limit_deg: f32,
    pub invert_y: bool,
    pub spawn_height: f32,
    pub field_of_view: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct InteractData {
    pub supply_radius_m: f32,
    pub tether_length_m: f32,
    pub tether_attach_range_m: f32,
    pub tank_pickup_range_m: f32,
    pub thruster_light_energy: f32,
    pub thruster_light_range_m: f32,
}

/// Node names the controller expects in player.tscn.
const HEAD_NODE: &str = "Head";
const CAMERA_NODE: &str = "Head/Camera3D";
const SHAPE_NODE: &str = "CollisionShape3D";

#[derive(GodotClass)]
#[class(base = CharacterBody3D)]
pub struct PlayerController {
    /// When on, the player starts standing on top of the nearest body.
    #[export]
    spawn_on_nearest_body: bool,
    data: MovementData,
    suit_data: SuitData,
    interact: InteractData,
    suit: SuitState,
    /// Where the tether is clipped on, if it is.
    tether_anchor: Option<Gd<Node3D>>,
    tether_line: Option<TetherLine>,
    thruster_light: Option<Gd<OmniLight3D>>,
    /// Jetpack thrust actually firing this tick, 0.0 to 1.0, and its direction.
    thrust: f32,
    thrust_dir: Vector3,
    in_zero_g: bool,
    head: Option<Gd<Node3D>>,
    pitch: f32,
    eye_height: f32,
    mouse_delta: Vector2,
    spawned: bool,
    base: Base<CharacterBody3D>,
}

#[godot_api]
impl ICharacterBody3D for PlayerController {
    fn init(base: Base<CharacterBody3D>) -> Self {
        let data: MovementData = crate::data::load(DATA_PATH, DATA_EMBEDDED);
        let suit_data: SuitData = crate::data::load(SUIT_PATH, SUIT_EMBEDDED);
        Self {
            spawn_on_nearest_body: true,
            eye_height: data.eye_height,
            data,
            suit: SuitState::new(&suit_data),
            suit_data,
            interact: crate::data::load(INTERACT_PATH, INTERACT_EMBEDDED),
            tether_anchor: None,
            tether_line: None,
            thruster_light: None,
            thrust: 0.0,
            thrust_dir: Vector3::ZERO,
            in_zero_g: false,
            head: None,
            pitch: 0.0,
            mouse_delta: Vector2::ZERO,
            spawned: false,
            base,
        }
    }

    fn ready(&mut self) {
        self.base_mut().add_to_group(PLAYER_GROUP);
        self.head = self.base().try_get_node_as::<Node3D>(HEAD_NODE);
        if self.head.is_none() {
            godot_error!("PlayerController: missing child node '{HEAD_NODE}'");
        }
        if let Some(mut shape_node) = self.base().try_get_node_as::<CollisionShape3D>(SHAPE_NODE) {
            let mut capsule = CapsuleShape3D::new_gd();
            capsule.set_radius(self.data.capsule_radius);
            capsule.set_height(self.data.capsule_height);
            shape_node.set_shape(&capsule);
        }
        if let Some(mut camera) = self
            .base()
            .try_get_node_as::<godot::classes::Camera3D>(CAMERA_NODE)
        {
            camera.set_fov(self.data.field_of_view);
        }
        let mut light = OmniLight3D::new_alloc();
        light.set_name("ThrusterLight");
        light.set_color(Color::from_rgb(1.0, 0.75, 0.45));
        light.set_param(Param::RANGE, self.interact.thruster_light_range_m);
        light.set_param(Param::ENERGY, 0.0);
        self.base_mut().add_child(&light);
        self.thruster_light = Some(light);
        let mut me = self.to_gd().upcast::<Node3D>();
        self.tether_line = Some(TetherLine::new(&mut me));
        Input::singleton().set_mouse_mode(MouseMode::CAPTURED);
    }

    fn unhandled_input(&mut self, event: Gd<InputEvent>) {
        let mut input = Input::singleton();
        // Not ui_cancel: Godot also binds that to the B button, which is crouch.
        if event.is_action_pressed("release_mouse") {
            input.set_mouse_mode(MouseMode::VISIBLE);
        } else if let Ok(motion) = event.clone().try_cast::<InputEventMouseMotion>() {
            if input.get_mouse_mode() == MouseMode::CAPTURED {
                self.mouse_delta += motion.get_relative();
            }
        } else if let Ok(click) = event.try_cast::<InputEventMouseButton>()
            && click.is_pressed()
        {
            input.set_mouse_mode(MouseMode::CAPTURED);
        }
    }

    fn physics_process(&mut self, delta: f64) {
        let dt = delta as f32;
        if !self.spawned {
            self.spawned = true;
            if self.spawn_on_nearest_body {
                self.place_on_nearest_body();
            }
        }

        let position = self.base().get_global_position();
        let gravity = self.total_gravity(position);
        self.in_zero_g = gravity.length() < ZERO_G_THRESHOLD;
        let up = if self.in_zero_g {
            self.base().get_global_basis().col_b()
        } else {
            -gravity.normalized()
        };

        let alive = self.suit.dead.is_none();
        if alive {
            self.handle_actions();
        }
        self.update_orientation(up, dt, alive);
        self.update_velocity(gravity, up, dt, alive);
        self.apply_tether();
        self.base_mut().move_and_slide();
        self.update_head(dt);
        self.update_suit(dt);
        self.update_thruster_light();
    }
}

#[godot_api]
impl PlayerController {
    /// Emitted once when the player dies, with the string-table key for the
    /// one-line death recap (fun rule 8, fair failure).
    #[signal]
    fn died(cause_key: GString);

    #[func]
    pub fn get_oxygen_fraction(&self) -> f32 {
        self.suit.oxygen_fraction(&self.suit_data)
    }

    #[func]
    pub fn get_fuel_fraction(&self) -> f32 {
        self.suit.fuel_fraction(&self.suit_data)
    }

    #[func]
    pub fn get_health_fraction(&self) -> f32 {
        (self.suit.health / self.suit_data.health_max).clamp(0.0, 1.0)
    }

    #[func]
    pub fn get_oxygen_seconds(&self) -> f32 {
        self.suit.oxygen_s
    }

    #[func]
    pub fn get_tanks(&self) -> i64 {
        self.suit.tanks as i64
    }

    #[func]
    pub fn get_max_tanks(&self) -> i64 {
        self.suit_data.tanks.max_carried as i64
    }

    #[func]
    pub fn is_low_oxygen(&self) -> bool {
        self.suit.low_oxygen(&self.suit_data)
    }

    #[func]
    pub fn is_tethered(&self) -> bool {
        self.tether_anchor.is_some()
    }

    #[func]
    pub fn is_in_zero_g(&self) -> bool {
        self.in_zero_g
    }

    #[func]
    pub fn is_dead(&self) -> bool {
        self.suit.dead.is_some()
    }

    /// What the interact-style buttons would do right now, for the HUD
    /// prompt: "take_tank", "tether", "untether", or empty.
    #[func]
    pub fn get_interact_prompt(&self) -> GString {
        if self.suit.dead.is_some() {
            return GString::new();
        }
        if self.suit.tanks < self.suit_data.tanks.max_carried
            && self
                .nearest_in_group(TANK_GROUP, self.interact.tank_pickup_range_m)
                .is_some()
        {
            return "take_tank".into();
        }
        if self.tether_anchor.is_some() {
            return "untether".into();
        }
        if self
            .nearest_in_group(TETHER_GROUP, self.interact.tether_attach_range_m)
            .is_some()
        {
            return "tether".into();
        }
        GString::new()
    }

    #[func]
    pub fn is_at_supply(&self) -> bool {
        self.nearest_in_group(SUPPLY_GROUP, self.interact.supply_radius_m)
            .is_some()
    }

    /// Empty while alive.
    #[func]
    pub fn get_death_key(&self) -> GString {
        self.suit
            .dead
            .map_or(GString::new(), |c| GString::from(c.text_key()))
    }

    /// Test hook: set suit oxygen in seconds (feature tests only).
    #[func]
    pub fn debug_set_oxygen(&mut self, seconds: f32) {
        self.suit.oxygen_s = seconds.clamp(0.0, self.suit_data.oxygen_capacity_s);
    }

    /// Look input in mouse pixels, as if the mouse moved. Used by input
    /// replays so they follow exactly the same code path as a real mouse.
    #[func]
    pub fn add_look_delta(&mut self, delta: Vector2) {
        self.mouse_delta += delta;
    }
}

impl PlayerController {
    fn total_gravity(&self, position: Vector3) -> Vector3 {
        let tree = self.base().get_tree();
        tree.get_nodes_in_group(GRAVITY_GROUP)
            .iter_shared()
            .filter_map(|node| node.try_cast::<CelestialBody>().ok())
            .map(|body| body.bind().gravity_at(position))
            .fold(Vector3::ZERO, |sum, g| sum + g)
    }

    fn place_on_nearest_body(&mut self) {
        let tree = self.base().get_tree();
        let position = self.base().get_global_position();
        let nearest = tree
            .get_nodes_in_group(GRAVITY_GROUP)
            .iter_shared()
            .filter_map(|node| node.try_cast::<CelestialBody>().ok())
            .min_by(|a, b| {
                let da = a.get_global_position().distance_to(position);
                let db = b.get_global_position().distance_to(position);
                da.total_cmp(&db)
            });
        let Some(body) = nearest else {
            godot_warn!("PlayerController: no body to spawn on");
            return;
        };
        let center = body.get_global_position();
        let mut dir = position - center;
        if dir.length_squared() < 1e-6 {
            dir = Vector3::UP;
        }
        let dir = dir.normalized();
        let ground = body.bind().surface_radius_toward(center + dir);
        let height = ground + self.data.capsule_height * 0.5 + self.data.spawn_height;
        let basis = motion::align_up(self.base().get_global_basis(), dir, std::f32::consts::PI);
        self.base_mut()
            .set_global_transform(Transform3D::new(basis, center + dir * height));
    }

    /// Keep the body upright against gravity and apply horizontal look (yaw).
    fn update_orientation(&mut self, up: Vector3, dt: f32, alive: bool) {
        let input = Input::singleton();
        let stick = if alive {
            input.get_vector("look_left", "look_right", "look_up", "look_down")
        } else {
            Vector2::ZERO
        };
        if !alive {
            self.mouse_delta = Vector2::ZERO;
        }
        let stick_speed = self.data.stick_look_speed_deg.to_radians() * dt;
        let mouse_scale = self.data.mouse_sensitivity_deg.to_radians();
        let yaw = stick.x * stick_speed + self.mouse_delta.x * mouse_scale;
        let mut pitch_input = stick.y * stick_speed + self.mouse_delta.y * mouse_scale;
        if self.data.invert_y {
            pitch_input = -pitch_input;
        }
        self.mouse_delta = Vector2::ZERO;

        // In zero-g there is no "up" to turn toward; keep the current one.
        let max_turn = if self.in_zero_g {
            0.0
        } else {
            self.data.align_speed_deg.to_radians() * dt
        };
        let aligned = motion::align_up(self.base().get_global_basis(), up, max_turn);
        let turned = (Basis::from_axis_angle(aligned.col_b().normalized(), -yaw) * aligned)
            .orthonormalized();
        self.base_mut().set_global_basis(turned);
        self.base_mut().set_up_direction(up);

        let limit = self.data.pitch_limit_deg.to_radians();
        self.pitch = (self.pitch - pitch_input).clamp(-limit, limit);
    }

    /// Walking on the ground; jetpack and pure inertia in the air.
    fn update_velocity(&mut self, gravity: Vector3, up: Vector3, dt: f32, alive: bool) {
        let input = Input::singleton();
        let on_floor = self.base().is_on_floor();
        let stick = if alive {
            input.get_vector("move_left", "move_right", "move_forward", "move_back")
        } else {
            Vector2::ZERO
        };
        let crouching = alive && input.is_action_pressed("crouch");
        let basis = self.base().get_global_basis();
        let mut velocity = self.base().get_velocity();
        self.thrust = 0.0;
        self.thrust_dir = Vector3::ZERO;

        if on_floor {
            let forward = -basis.col_c();
            let right = basis.col_a();
            let speed = if crouching {
                self.data.crouch_speed
            } else {
                self.data.walk_speed
            };
            let wish = (right * stick.x - forward * stick.y) * speed;
            let (mut vertical, horizontal) = motion::split_vertical(velocity, up);
            let horizontal = horizontal.move_toward(wish, self.data.ground_acceleration * dt);
            if alive && input.is_action_just_pressed("jump") {
                vertical = up * self.data.jump_speed;
            }
            velocity = horizontal + vertical;
        } else if alive {
            // Airborne: no air control without fuel. The stick, A and B fire the jetpack.
            let vertical = input.get_action_strength("jump") - input.get_action_strength("crouch");
            let demand = motion::thrust_demand(stick, vertical);
            let available = self.suit.available_thrust(demand);
            let jp = &self.suit_data.jetpack;
            let camera_forward = self.camera_forward();
            let dv = motion::jetpack_delta_v(
                basis,
                camera_forward,
                stick,
                vertical,
                jp.thrust_acceleration,
                jp.vertical_acceleration,
                available,
                dt,
            );
            velocity += dv;
            if available > 0.0 {
                self.thrust = available;
                self.thrust_dir = dv.normalized();
            }
            // Match velocity: cancel motion relative to the nearest body.
            if input.is_action_pressed("match_velocity") {
                let relative = velocity - self.reference_velocity();
                if relative.length() > 0.01 && self.suit.available_thrust(1.0) > 0.0 {
                    let fix =
                        motion::match_velocity_delta(relative, jp.match_velocity_acceleration, dt);
                    velocity += fix;
                    self.thrust = 1.0;
                    self.thrust_dir = fix.normalized();
                }
            }
        }

        velocity += gravity * dt;
        // Cap falling speed only, so landing on a body never tunnels through it.
        if !self.in_zero_g {
            let (vertical, horizontal) = motion::split_vertical(velocity, up);
            if vertical.dot(up) < -self.data.max_fall_speed {
                velocity = horizontal - up * self.data.max_fall_speed;
            }
        }
        self.base_mut().set_velocity(velocity);
    }

    /// Velocity of whatever the player matches against. Bodies don't move
    /// yet; Phase 3 (orbits) returns the nearest body's orbital velocity here.
    fn reference_velocity(&self) -> Vector3 {
        Vector3::ZERO
    }

    fn camera_forward(&self) -> Vector3 {
        match &self.head {
            Some(head) => -head.get_global_basis().col_c(),
            None => -self.base().get_global_basis().col_c(),
        }
    }

    fn handle_actions(&mut self) {
        let input = Input::singleton();
        if input.is_action_just_pressed("interact") {
            self.try_pick_up_tank();
        }
        if input.is_action_just_pressed("use_oxygen_tank") {
            self.suit.use_tank(&self.suit_data);
        }
        // Toggle: a press while clipped on unclips; otherwise clip to the nearest anchor.
        if input.is_action_just_pressed("tether_toggle") && self.tether_anchor.take().is_none() {
            let range = self.interact.tether_attach_range_m;
            self.tether_anchor = self.nearest_in_group(TETHER_GROUP, range);
        }
    }

    fn try_pick_up_tank(&mut self) {
        let range = self.interact.tank_pickup_range_m;
        let Some(mut tank) = self.nearest_in_group(TANK_GROUP, range) else {
            return;
        };
        if self.suit.take_tank(&self.suit_data) {
            // Leave the group now so a second press this frame can't take it again.
            tank.remove_from_group(TANK_GROUP);
            tank.queue_free();
        }
    }

    fn nearest_in_group(&self, group: &str, range: f32) -> Option<Gd<Node3D>> {
        let position = self.base().get_global_position();
        self.base()
            .get_tree()
            .get_nodes_in_group(group)
            .iter_shared()
            .filter_map(|n| n.try_cast::<Node3D>().ok())
            .map(|n| (n.get_global_position().distance_to(position), n))
            .filter(|(d, _)| *d <= range)
            .min_by(|a, b| a.0.total_cmp(&b.0))
            .map(|(_, n)| n)
    }

    fn apply_tether(&mut self) {
        let anchor = match &self.tether_anchor {
            Some(a) if a.is_instance_valid() => a.get_global_position(),
            Some(_) => {
                self.tether_anchor = None;
                return;
            }
            None => return,
        };
        let position = self.base().get_global_position();
        let velocity = motion::tether_velocity(
            anchor,
            position,
            self.base().get_velocity(),
            self.interact.tether_length_m,
        );
        self.base_mut().set_velocity(velocity);
    }

    fn update_suit(&mut self, dt: f32) {
        let position = self.base().get_global_position();
        let tree = self.base().get_tree();
        let breathable = tree
            .get_nodes_in_group(GRAVITY_GROUP)
            .iter_shared()
            .filter_map(|n| n.try_cast::<CelestialBody>().ok())
            .any(|b| b.bind().breathable_at(position));
        let at_supply = self
            .nearest_in_group(SUPPLY_GROUP, self.interact.supply_radius_m)
            .is_some();
        let was_alive = self.suit.dead.is_none();
        let input = SuitInput {
            in_vacuum: !breathable,
            at_supply_point: at_supply,
            on_ground: self.base().is_on_floor(),
            thrust: self.thrust,
        };
        self.suit.step(&self.suit_data, input, dt);
        if was_alive && let Some(cause) = self.suit.dead {
            self.tether_anchor = None;
            godot_print!("Player died: {}", cause.text_key());
            self.base_mut()
                .emit_signal("died", &[GString::from(cause.text_key()).to_variant()]);
        }

        let line = self
            .tether_anchor
            .as_ref()
            .map(|a| (a.get_global_position(), position));
        if let Some(l) = self.tether_line.as_mut() {
            l.draw(line);
        }
    }

    /// Outer Wilds-style: the light glows on the side the thrusters fire
    /// from, so you can see which way you're pushing.
    fn update_thruster_light(&mut self) {
        let (thrust, dir) = (self.thrust, self.thrust_dir);
        let energy = self.interact.thruster_light_energy * thrust;
        if let Some(light) = self.thruster_light.as_mut() {
            light.set_param(Param::ENERGY, energy);
            if thrust > 0.0 {
                let global = light
                    .get_parent_node_3d()
                    .map_or(Transform3D::IDENTITY, |p| p.get_global_transform());
                light.set_position(global.basis.inverse() * (-dir * 0.8));
            }
        }
    }

    fn update_head(&mut self, dt: f32) {
        let crouching = Input::singleton().is_action_pressed("crouch");
        let target = if crouching {
            self.data.crouch_eye_height
        } else {
            self.data.eye_height
        };
        let blend = (self.data.crouch_blend_speed * dt).min(1.0);
        self.eye_height += (target - self.eye_height) * blend;
        let (pitch, eye) = (self.pitch, self.eye_height);
        if let Some(head) = self.head.as_mut() {
            head.set_position(Vector3::new(0.0, eye, 0.0));
            head.set_rotation(Vector3::new(pitch, 0.0, 0.0));
        }
    }
}
