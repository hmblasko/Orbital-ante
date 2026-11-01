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

use super::effects::{EffectsInput, SuitEffects};
use super::motion;
use super::suit::{SuitData, SuitInput, SuitState};
use super::supply::SUPPLY_GROUP;
use super::tether::{RopeLook, TETHER_GROUP, TetherLine};
use crate::flight::Ship;
use crate::flight::ship::{LAYER_CABIN, LAYER_HULL, LAYER_PLAYER, LAYER_WORLD, Zone};
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
const EFFECTS_PATH: &str = "res://data/player/effects.ron";
const EFFECTS_EMBEDDED: &str = include_str!("../../../../godot/data/player/effects.ron");

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
    /// Capsule height while crouched (shrinks from the top, feet stay put),
    /// so you can duck under a lander's descent stage.
    pub crouch_capsule_height: f32,
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
    pub tether_rope: RopeLook,
}

/// Node names the controller expects in player.tscn.
const HEAD_NODE: &str = "Head";
const CAMERA_NODE: &str = "Head/Camera3D";
const SHAPE_NODE: &str = "CollisionShape3D";

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum ShipAction {
    Buckle,
    Hatch,
}

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
    effects: Option<SuitEffects>,
    /// Jetpack thrust actually firing this tick, 0.0 to 1.0, and its direction.
    thrust: f32,
    thrust_dir: Vector3,
    /// Test hook: walk toward this world point as if pushing the stick.
    debug_walk: Option<Vector3>,
    in_zero_g: bool,
    head: Option<Gd<Node3D>>,
    pitch: f32,
    eye_height: f32,
    mouse_delta: Vector2,
    spawned: bool,
    /// The ship the player is in (cabin or lift), if any.
    aboard: Option<Gd<Ship>>,
    buckled: bool,
    /// Wind push from the weather (m/s^2), set each physics frame.
    wind_accel: Vector3,
    /// In water this frame (swimming physics on).
    submerged: bool,
    /// Buckle-in animation, 0 to 1, from where you stood (ship space).
    buckle_t: f32,
    buckle_from: Transform3D,
    /// The capsule is currently the short crouch one.
    crouched_shape: bool,
    /// Where the last enemy hit came from, and seconds since.
    last_hit_from: Option<Vector3>,
    last_hit_s: f32,
    /// A damaged ship part in view and reach: the ship, the part's index,
    /// its name key and health 0..1 (for the prompt).
    repair_spot: Option<(Gd<Ship>, usize, String, f32)>,
    /// Holding X on it right now (tests can force it).
    repairing: bool,
    force_repair: bool,
    /// Data before perks and attributes; `data` and `suit_data` are these
    /// scaled by the run's build (re-scaled when the build changes).
    base_data: MovementData,
    base_suit: SuitData,
    build_rev: i64,
    base: Base<CharacterBody3D>,
}

/// Seconds to sit down and buckle in.
const BUCKLE_S: f32 = 0.55;

#[godot_api]
impl ICharacterBody3D for PlayerController {
    fn init(base: Base<CharacterBody3D>) -> Self {
        let data: MovementData = crate::data::load(DATA_PATH, DATA_EMBEDDED);
        let suit_data: SuitData = crate::data::load(SUIT_PATH, SUIT_EMBEDDED);
        Self {
            base_data: data.clone(),
            base_suit: suit_data.clone(),
            build_rev: -1,
            spawn_on_nearest_body: true,
            eye_height: data.eye_height,
            data,
            suit: SuitState::new(&suit_data),
            suit_data,
            interact: crate::data::load(INTERACT_PATH, INTERACT_EMBEDDED),
            tether_anchor: None,
            tether_line: None,
            thruster_light: None,
            effects: None,
            thrust: 0.0,
            thrust_dir: Vector3::ZERO,
            debug_walk: None,
            in_zero_g: false,
            head: None,
            pitch: 0.0,
            mouse_delta: Vector2::ZERO,
            spawned: false,
            aboard: None,
            buckled: false,
            wind_accel: Vector3::ZERO,
            submerged: false,
            repair_spot: None,
            repairing: false,
            force_repair: false,
            buckle_t: 0.0,
            buckle_from: Transform3D::IDENTITY,
            crouched_shape: false,
            last_hit_from: None,
            last_hit_s: 0.0,
            base,
        }
    }

    fn ready(&mut self) {
        self.base_mut().add_to_group(PLAYER_GROUP);
        {
            let mut b = self.base_mut();
            b.set_collision_layer(LAYER_PLAYER);
            b.set_collision_mask(LAYER_WORLD | LAYER_HULL);
            // The solar system and ships carry the player; never follow a
            // floor body's velocity on top of that.
            b.set_platform_floor_layers(0);
        }
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
        if let Some(mut camera) = self.base().try_get_node_as::<godot::classes::Camera3D>(CAMERA_NODE) {
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
        self.tether_line = Some(TetherLine::new(&mut me, self.interact.tether_rope.clone()));
        self.effects = Some(SuitEffects::new(&mut me, crate::data::load(EFFECTS_PATH, EFFECTS_EMBEDDED)));
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
            && click.is_pressed() {
                input.set_mouse_mode(MouseMode::CAPTURED);
            }
    }

    fn physics_process(&mut self, delta: f64) {
        let dt = delta as f32;
        self.last_hit_s += dt;
        if !self.spawned {
            self.spawned = true;
            if self.spawn_on_nearest_body {
                self.place_on_nearest_body();
            }
        }

        let position = self.base().get_global_position();
        let gravity = self.total_gravity(position);
        // Aboard a ship, "up" is the ship's up, even while the lift pushes
        // you upward.
        let ship_up = self.aboard.as_ref().map(|s| s.get_global_basis().col_b().normalized());
        self.in_zero_g = ship_up.is_none() && gravity.length() < ZERO_G_THRESHOLD;
        let up = match ship_up {
            Some(u) => u,
            None if self.in_zero_g => self.base().get_global_basis().col_b(),
            None => -gravity.normalized(),
        };

        let alive = self.suit.dead.is_none();
        if alive {
            self.update_repair(dt);
            self.handle_actions();
        }
        if self.buckled {
            self.sit(dt);
            self.update_head(dt);
            self.update_suit(dt);
            self.update_thruster_light();
            return;
        }
        self.update_orientation(up, dt, alive);
        self.update_crouch_shape(alive && Input::singleton().is_action_pressed("crouch"));
        self.update_velocity(gravity, up, dt, alive);
        self.apply_tether();
        // Speed into the ground just before landing, for the landing puff.
        let fall_speed = (-self.base().get_velocity().dot(up)).max(0.0);
        self.base_mut().move_and_slide();
        self.update_effects(gravity, up, fall_speed, dt);
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
        if self.buckled {
            return "unbuckle".into();
        }
        if let Some(ship) = &self.aboard {
            let p = self.base().get_global_position();
            match self.ship_action() {
                Some(ShipAction::Buckle) => return "buckle".into(),
                Some(ShipAction::Hatch) => {
                    return if ship.bind().get_hatch_open() > 0.5 { "close_hatch".into() } else { "open_hatch".into() };
                }
                None => {}
            }
            let _ = p;
        }
        if let Some((_, _, key, frac)) = &self.repair_spot {
            return GString::from(&format!("repair:{key}:{frac:.3}"));
        }
        if self.suit.tanks < self.suit_data.tanks.max_carried && self.nearest_in_group(TANK_GROUP, self.interact.tank_pickup_range_m).is_some() {
            return "take_tank".into();
        }
        if self.tether_anchor.is_some() {
            return "untether".into();
        }
        if self.nearest_in_group(TETHER_GROUP, self.interact.tether_attach_range_m).is_some() {
            return "tether".into();
        }
        GString::new()
    }

    #[func]
    pub fn is_at_supply(&self) -> bool {
        self.aboard.as_ref().is_some_and(|s| s.bind().zone() == Zone::Cabin)
            || self.nearest_in_group(SUPPLY_GROUP, self.interact.supply_radius_m).is_some()
    }

    /// Empty while alive.
    #[func]
    pub fn get_death_key(&self) -> GString {
        self.suit.dead.map_or(GString::new(), |c| GString::from(c.text_key()))
    }

    #[func]
    pub fn is_buckled(&self) -> bool {
        self.buckled
    }

    #[func]
    pub fn is_aboard(&self) -> bool {
        self.aboard.is_some()
    }

    /// Inside a ship's pressurised cabin (out of the weather).
    #[func]
    pub fn is_in_cabin(&self) -> bool {
        self.aboard.as_ref().is_some_and(|s| s.bind().zone() == Zone::Cabin)
    }

    /// Swimming: the player's centre is under water on a body with an ocean.
    #[func]
    pub fn is_submerged(&self) -> bool {
        self.submerged
    }

    /// The water you are in, if any (not while aboard a ship).
    fn water_here(&self) -> Option<crate::orbits::body::OceanData> {
        if self.aboard.is_some() {
            return None;
        }
        let p = self.base().get_global_position();
        self.base()
            .get_tree()
            .get_nodes_in_group(GRAVITY_GROUP)
            .iter_shared()
            .filter_map(|n| n.try_cast::<CelestialBody>().ok())
            .find_map(|b| {
                let b = b.bind();
                b.ocean().filter(|_| b.water_depth_at(p) > 0.0)
            })
    }

    /// Wind push the weather applies this frame, m/s^2.
    pub fn set_wind_acceleration(&mut self, accel: Vector3) {
        self.wind_accel = accel;
    }

    #[func]
    pub fn wind_acceleration(&self) -> Vector3 {
        self.wind_accel
    }

    /// Test hook: press the interact button's action (buckle, unbuckle,
    /// hatch, tank, as if X were pressed).
    /// A crash throws you about inside the ship (from the ship's damage).
    pub fn crash_hurt(&mut self, amount: f32) {
        self.suit.hurt(amount, super::suit::DeathCause::Crash);
    }

    /// Test hook: turn to look straight at a world point.
    #[func]
    pub fn debug_look_at(&mut self, point: Vector3) {
        let t = self.base().get_global_transform();
        let up = t.basis.col_b().normalized();
        let eye = self.head_position();
        let to = point - eye;
        let vertical = to.dot(up);
        let flat = to - up * vertical;
        if flat.length_squared() < 1e-6 {
            return;
        }
        let f = flat.normalized();
        let x = f.cross(up).normalized();
        self.base_mut().set_global_basis(Basis::from_cols(x, up, -f).orthonormalized());
        self.pitch = vertical.atan2(flat.length());
        let pitch = self.pitch;
        if let Some(h) = &mut self.head {
            let r = h.get_rotation();
            h.set_rotation(Vector3::new(pitch, r.y, r.z));
        }
    }

    /// Test hook: hold (or let go of) the repair button.
    #[func]
    pub fn debug_hold_repair(&mut self, on: bool) {
        self.force_repair = on;
    }

    /// True while the helmet lamp is on (caves, shadowed craters).
    #[func]
    pub fn is_lamp_on(&self) -> bool {
        self.effects.as_ref().is_some_and(|e| e.lamp_on())
    }

    #[func]
    pub fn is_repairing(&self) -> bool {
        self.repairing
    }

    #[func]
    pub fn debug_interact(&mut self) {
        self.interact();
    }

    /// Test hook: walk toward a world point (Vector3.ZERO stops).
    #[func]
    pub fn debug_walk_toward(&mut self, target: Vector3) {
        self.debug_walk = (target != Vector3::ZERO).then_some(target);
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
    /// Hit by an enemy for `amount` health from `from` (world position).
    /// Remembered for the HUD's damage arrow.
    pub fn take_hit(&mut self, amount: f32, from: Vector3) {
        // Glass Cannon and Defense scale damage taken.
        let amount = amount * crate::points::singleton::stat("damage_taken");
        if self.suit.dead.is_none() && self.suit.health - amount <= 0.0 {
            // Second Wind: once per ante a lethal hit leaves 1 HP.
            let mut saved = false;
            crate::points::singleton::with_points(|p| saved = p.try_second_wind());
            if saved {
                self.suit.health = 1.0;
                self.last_hit_from = Some(from);
                self.last_hit_s = 0.0;
                return;
            }
        }
        self.suit.hurt(amount, super::suit::DeathCause::Shot);
        self.last_hit_from = Some(from);
        self.last_hit_s = 0.0;
    }

    /// Heal by a fraction of max health (Heat at 4).
    pub fn heal_fraction(&mut self, f: f32) {
        if self.suit.dead.is_none() {
            self.suit.health = (self.suit.health + f * self.suit_data.health_max).min(self.suit_data.health_max);
        }
    }

    /// Where the last enemy hit came from and how long ago (seconds).
    pub fn last_hit(&self) -> Option<(Vector3, f32)> {
        self.last_hit_from.map(|p| (p, self.last_hit_s))
    }

    /// Crouching (enemies notice you more slowly).
    pub fn is_crouched(&self) -> bool {
        self.crouched_shape
    }

    /// The eyes (camera) in world space.
    pub fn head_position(&self) -> Vector3 {
        self.head.as_ref().map_or(self.base().get_global_position(), |h| h.get_global_position())
    }

    pub fn aboard_ship(&self) -> Option<Gd<Ship>> {
        self.aboard.clone()
    }

    /// Distance from the body's centre down to the feet.
    pub fn feet_below(&self) -> f32 {
        self.data.capsule_height * 0.5
    }

    /// Called by a ship when the player boards it or leaves it. Inside, the
    /// player walks on the ship's static cabin copy instead of its hull.
    pub fn set_aboard(&mut self, ship: Option<Gd<Ship>>) {
        let mask = if ship.is_some() { LAYER_WORLD | LAYER_CABIN } else { LAYER_WORLD | LAYER_HULL };
        self.base_mut().set_collision_mask(mask);
        self.aboard = ship;
    }

    fn interact(&mut self) {
        let p = self.base().get_global_position();
        if self.buckled {
            self.unbuckle();
            return;
        }
        if let Some(mut ship) = self.aboard.clone() {
            let action = self.ship_action();
            let _ = p;
            if action == Some(ShipAction::Buckle) {
                let t = ship.get_global_transform().affine_inverse() * self.base().get_global_transform();
                self.buckle_from = t;
                self.buckle_t = 0.0;
                self.buckled = true;
                self.tether_anchor = None;
                self.set_shape_enabled(false);
                ship.bind_mut().set_pilot(true);
                return;
            }
            if action == Some(ShipAction::Hatch) {
                ship.bind_mut().toggle_hatch();
                return;
            }
        }
        self.try_pick_up_tank();
    }

    fn unbuckle(&mut self) {
        let Some(mut ship) = self.aboard.clone() else {
            self.buckled = false;
            return;
        };
        ship.bind_mut().set_pilot(false);
        let stand = ship.bind().stand_transform();
        self.buckled = false;
        self.set_shape_enabled(true);
        if let Some(t) = stand {
            self.base_mut().set_global_transform(t);
        }
        self.base_mut().set_velocity(Vector3::ZERO);
    }

    /// Swap between the standing and crouched capsule. Standing back up
    /// waits until there's headroom.
    fn update_crouch_shape(&mut self, want: bool) {
        if want == self.crouched_shape {
            return;
        }
        let (full, short) = (self.data.capsule_height, self.data.crouch_capsule_height.min(self.data.capsule_height));
        if !want {
            let up = self.base().get_global_basis().col_b().normalized();
            let from = self.base().get_global_transform();
            if self.base_mut().test_move(from, up * (full - short)) {
                return;
            }
        }
        let height = if want { short } else { full };
        if let Some(mut shape_node) = self.base().try_get_node_as::<CollisionShape3D>(SHAPE_NODE) {
            let mut capsule = CapsuleShape3D::new_gd();
            capsule.set_radius(self.data.capsule_radius);
            capsule.set_height(height);
            shape_node.set_shape(&capsule);
            shape_node.set_position(Vector3::new(0.0, -(full - height) * 0.5, 0.0));
        }
        self.crouched_shape = want;
    }

    fn set_shape_enabled(&mut self, on: bool) {
        if let Some(mut shape) = self.base().try_get_node_as::<CollisionShape3D>(SHAPE_NODE) {
            shape.set_disabled(!on);
        }
    }

    /// Buckled in: ease into the seat, then stay locked to it. Look input
    /// steers the ship instead of the head.
    fn sit(&mut self, dt: f32) {
        let Some(mut ship) = self.aboard.clone() else {
            self.buckled = false;
            self.set_shape_enabled(true);
            return;
        };
        let ship_now = ship.get_global_transform();
        let Some(seat) = ship.bind().seat_transform() else { return };
        // The physics step moves the ship after this runs, so sit where the
        // ship will be at the end of this tick, not where it was.
        let w = ship.get_angular_velocity();
        let spin = if w.length() > 1e-6 { Basis::from_axis_angle(w.normalized(), w.length() * dt) } else { Basis::IDENTITY };
        let ship_t = Transform3D::new((spin * ship_now.basis).orthonormalized(), ship_now.origin + ship.get_linear_velocity() * dt);
        let seat = ship_t * (ship_now.affine_inverse() * seat);
        self.buckle_t = (self.buckle_t + dt / BUCKLE_S).min(1.0);
        let e = self.buckle_t * self.buckle_t * (3.0 - 2.0 * self.buckle_t);
        let seat_local = ship_t.affine_inverse() * seat;
        let local = self.buckle_from.interpolate_with(&seat_local, e);
        self.base_mut().set_global_transform(ship_t * local);
        self.base_mut().set_velocity(Vector3::ZERO);
        let blend = (6.0 * dt).min(1.0);
        self.pitch += (0.0 - self.pitch) * blend;
        let delta = std::mem::take(&mut self.mouse_delta);
        ship.bind_mut().add_mouse(delta);
    }

    fn total_gravity(&self, position: Vector3) -> Vector3 {
        if let Some(ship) = &self.aboard {
            let v = self.base().get_velocity();
            if let Some(g) = ship.bind().gravity_for(position, v, self.feet_below()) {
                return g;
            }
        }
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
        self.base_mut().set_global_transform(Transform3D::new(basis, center + dir * height));
    }

    /// Keep the body upright against gravity and apply horizontal look (yaw).
    fn update_orientation(&mut self, up: Vector3, dt: f32, alive: bool) {
        let input = Input::singleton();
        let stick = if alive { input.get_vector("look_left", "look_right", "look_up", "look_down") } else { Vector2::ZERO };
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
        let max_turn = if self.in_zero_g { 0.0 } else { self.data.align_speed_deg.to_radians() * dt };
        let aligned = motion::align_up(self.base().get_global_basis(), up, max_turn);
        let turned = (Basis::from_axis_angle(aligned.col_b().normalized(), -yaw) * aligned).orthonormalized();
        self.base_mut().set_global_basis(turned);
        self.base_mut().set_up_direction(up);

        let limit = self.data.pitch_limit_deg.to_radians();
        self.pitch = (self.pitch - pitch_input).clamp(-limit, limit);
    }

    /// Walking on the ground; jetpack and pure inertia in the air.
    fn update_velocity(&mut self, gravity: Vector3, up: Vector3, dt: f32, alive: bool) {
        let input = Input::singleton();
        let on_floor = self.base().is_on_floor();
        let mut stick = if alive { input.get_vector("move_left", "move_right", "move_forward", "move_back") } else { Vector2::ZERO };
        let crouching = alive && input.is_action_pressed("crouch");
        let basis = self.base().get_global_basis();
        if let (Some(target), true) = (self.debug_walk, alive) {
            let to = target - self.base().get_global_position();
            let flat = (to - up * to.dot(up)).normalized_or_zero();
            stick = Vector2::new(flat.dot(basis.col_a()), flat.dot(basis.col_c()));
        }
        let mut velocity = self.base().get_velocity();
        self.thrust = 0.0;
        self.thrust_dir = Vector3::ZERO;
        // Under water: buoyancy, drag and slow walking (Neptune, lakes).
        let water = self.water_here();
        self.submerged = water.is_some();

        if on_floor {
            let forward = -basis.col_c();
            let right = basis.col_a();
            let walk = water.as_ref().map_or(1.0, |o| o.walk_scale);
            let speed = walk * if crouching { self.data.crouch_speed } else { self.data.walk_speed };
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
            let dv = motion::jetpack_delta_v(basis, camera_forward, stick, vertical, jp.thrust_acceleration, jp.vertical_acceleration, available, dt);
            velocity += dv;
            if available > 0.0 {
                self.thrust = available;
                self.thrust_dir = dv.normalized();
            }
            // Match velocity: cancel motion relative to the nearest body.
            if input.is_action_pressed("match_velocity") {
                let relative = velocity - self.reference_velocity();
                if relative.length() > 0.01 && self.suit.available_thrust(1.0) > 0.0 {
                    let fix = motion::match_velocity_delta(relative, jp.match_velocity_acceleration, dt);
                    velocity += fix;
                    self.thrust = 1.0;
                    self.thrust_dir = fix.normalized();
                }
            }
        }

        let lift = water.as_ref().map_or(0.0, |o| o.buoyancy.clamp(0.0, 1.0));
        velocity += gravity * (1.0 - lift) * dt;
        if let Some(o) = &water {
            velocity *= (1.0 - o.drag_per_s * dt).max(0.0);
        }
        // Wind and dust devils (never inside a ship or while buckled in).
        if self.aboard.is_none() && !self.buckled {
            velocity += self.wind_accel * dt;
        }
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

    /// What X does aboard: buckle in or work the hatch. When both are in
    /// reach, the one the player is looking more toward wins.
    fn ship_action(&self) -> Option<ShipAction> {
        let ship = self.aboard.as_ref()?;
        let p = self.base().get_global_position();
        let s = ship.bind();
        let eye = self.head.as_ref().map_or(p, |h| h.get_global_position());
        let facing = |target: Vector3| self.camera_forward().dot((target - eye).normalized());
        match (s.can_buckle(p), s.can_work_hatch(p)) {
            (true, true) => Some(if facing(s.seat_point()) >= facing(s.hatch_point()) { ShipAction::Buckle } else { ShipAction::Hatch }),
            (true, false) => Some(ShipAction::Buckle),
            (false, true) => Some(ShipAction::Hatch),
            (false, false) => None,
        }
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
            self.interact();
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

    /// Outside a ship, look at a damaged part within reach and hold X / E
    /// to fix it (CLAUDE.md section 5: multi-part, quick repairs; works
    /// mid-flight too). The repair sound is a missing asset.
    fn update_repair(&mut self, dt: f32) {
        self.repairing = false;
        if self.aboard.is_some() || self.buckled {
            self.repair_spot = None;
            return;
        }
        let eye = self.head.as_ref().map_or(self.base().get_global_position(), |h| h.get_global_position());
        let look = self.camera_forward();
        let mut found = None;
        for node in self.base().get_tree().get_nodes_in_group(crate::flight::ship::SHIP_GROUP).iter_shared() {
            let Ok(ship) = node.try_cast::<Ship>() else { continue };
            if let Some((i, _, key, frac)) = ship.bind().repair_spot(eye, look) {
                found = Some((ship.clone(), i, key, frac));
                break;
            }
        }
        self.repair_spot = found;
        let holding = self.force_repair || Input::singleton().is_action_pressed("interact");
        if let (true, Some((mut ship, i, _, _))) = (holding, self.repair_spot.clone()) {
            let frac = ship.bind_mut().repair(i, dt);
            self.repairing = true;
            if let Some(spot) = self.repair_spot.as_mut() {
                spot.3 = frac;
            }
        }
    }

    fn try_pick_up_tank(&mut self) {
        let range = self.interact.tank_pickup_range_m;
        let Some(mut tank) = self.nearest_in_group(TANK_GROUP, range) else { return };
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
        let velocity = motion::tether_velocity(anchor, position, self.base().get_velocity(), self.interact.tether_length_m);
        self.base_mut().set_velocity(velocity);
    }

    /// Perks and attributes (section 3): oxygen capacity, jetpack fuel,
    /// walking speed and jump height follow the run's build.
    fn apply_build(&mut self) {
        let rev = crate::points::singleton::rev();
        if rev == self.build_rev {
            return;
        }
        self.build_rev = rev;
        let stat = crate::points::singleton::stat;
        let mut suit = self.base_suit.clone();
        suit.oxygen_capacity_s *= stat("oxygen_capacity");
        suit.jetpack.fuel_capacity_s *= stat("jetpack_fuel");
        let mut data = self.base_data.clone();
        data.walk_speed *= stat("move_speed");
        data.crouch_speed *= stat("move_speed");
        data.jump_speed *= stat("jump");
        self.suit.oxygen_s = self.suit.oxygen_s.min(suit.oxygen_capacity_s);
        self.suit.fuel_s = self.suit.fuel_s.min(suit.jetpack.fuel_capacity_s);
        self.suit_data = suit;
        self.data = data;
    }

    fn update_suit(&mut self, dt: f32) {
        self.apply_build();
        let position = self.base().get_global_position();
        let tree = self.base().get_tree();
        let breathable = tree
            .get_nodes_in_group(GRAVITY_GROUP)
            .iter_shared()
            .filter_map(|n| n.try_cast::<CelestialBody>().ok())
            .any(|b| b.bind().breathable_at(position));
        // The cabin has air, and refills the suit from the ship's supply.
        let in_cabin = self.aboard.as_ref().is_some_and(|s| s.bind().zone() == Zone::Cabin);
        let breathable = breathable || in_cabin;
        let at_supply = in_cabin || self.nearest_in_group(SUPPLY_GROUP, self.interact.supply_radius_m).is_some();
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
            self.base_mut().emit_signal("died", &[GString::from(cause.text_key()).to_variant()]);
        }

        // The rope clips to the suit's belt, a little below the body's centre.
        let gravity = self.total_gravity(position);
        let down = if gravity.length_squared() > 0.01 { gravity.normalized() } else { Vector3::ZERO };
        let belt = position + down * self.interact.tether_rope.belt_below_center_m;
        let line = self.tether_anchor.as_ref().map(|a| (a.get_global_position(), belt));
        let length = self.interact.tether_length_m;
        // Rest the slack on top of the dust, not in it.
        let rope = &self.interact.tether_rope;
        let ground = self.data.capsule_height * 0.5 - rope.belt_below_center_m - rope.radius_m * 2.0;
        if let Some(l) = self.tether_line.as_mut() {
            l.draw(line, gravity, length, ground);
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
                let global = light.get_parent_node_3d().map_or(Transform3D::IDENTITY, |p| p.get_global_transform());
                light.set_position(global.basis.inverse() * (-dir * 0.8));
            }
        }
    }

    fn update_effects(&mut self, gravity: Vector3, up: Vector3, fall_speed: f32, dt: f32) {
        let position = self.base().get_global_position();
        let dust_color = self
            .base()
            .get_tree()
            .get_nodes_in_group(GRAVITY_GROUP)
            .iter_shared()
            .filter_map(|n| n.try_cast::<CelestialBody>().ok())
            .min_by(|a, b| {
                let da = a.get_global_position().distance_squared_to(position);
                let db = b.get_global_position().distance_squared_to(position);
                da.total_cmp(&db)
            })
            .map_or(Color::from_rgb(0.6, 0.6, 0.6), |b| b.bind().dust_color());
        let eye = self.head.as_ref().map_or(position, |h| h.get_global_position());
        let dark = self
            .base()
            .get_tree()
            .get_nodes_in_group(GRAVITY_GROUP)
            .iter_shared()
            .filter_map(|n| n.try_cast::<CelestialBody>().ok())
            .any(|b| b.bind().is_dark_at(eye));
        let (_, horizontal) = motion::split_vertical(self.base().get_velocity(), up);
        let input = EffectsInput {
            position,
            feet: position - up * self.data.capsule_height * 0.5,
            up,
            gravity,
            on_floor: self.base().is_on_floor(),
            crouching: Input::singleton().is_action_pressed("crouch"),
            ground_speed: horizontal.length(),
            fall_speed,
            thrust: self.thrust,
            thrust_dir: self.thrust_dir,
            dust_color,
            dark,
        };
        if let Some(effects) = self.effects.as_mut() {
            effects.update(&input, dt);
        }
    }

    fn update_head(&mut self, dt: f32) {
        let crouching = Input::singleton().is_action_pressed("crouch");
        let target = if crouching { self.data.crouch_eye_height } else { self.data.eye_height };
        let blend = (self.data.crouch_blend_speed * dt).min(1.0);
        self.eye_height += (target - self.eye_height) * blend;
        let dip = self.effects.as_ref().map_or(0.0, |e| e.camera_dip());
        let (pitch, eye) = (self.pitch, self.eye_height - dip);
        if let Some(head) = self.head.as_mut() {
            head.set_position(Vector3::new(0.0, eye, 0.0));
            head.set_rotation(Vector3::new(pitch, 0.0, 0.0));
        }
    }
}
