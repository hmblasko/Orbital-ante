//! First-person capsule that walks on any body using point gravity.
//!
//! Serves pillar 4 (everything is physical: the player obeys the same gravity
//! as everything else) and fun rule 10 (game feel: responsive, predictable
//! movement). Values come from res://data/player/movement.ron.

use godot::classes::input::MouseMode;
use godot::classes::{
    CapsuleShape3D, CharacterBody3D, CollisionShape3D, ICharacterBody3D, Input, InputEvent,
    InputEventMouseButton, InputEventMouseMotion, Node3D,
};
use godot::prelude::*;
use serde::Deserialize;

use super::motion;
use crate::orbits::CelestialBody;
use crate::orbits::body::GRAVITY_GROUP;

/// Group the player joins so tools and (later) AI can find it.
pub const PLAYER_GROUP: &str = "player";

const DATA_PATH: &str = "res://data/player/movement.ron";
const DATA_EMBEDDED: &str = include_str!("../../../../godot/data/player/movement.ron");

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
        Self {
            spawn_on_nearest_body: true,
            eye_height: data.eye_height,
            data,
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
        if let Some(mut camera) = self.base().try_get_node_as::<godot::classes::Camera3D>(CAMERA_NODE) {
            camera.set_fov(self.data.field_of_view);
        }
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
        if !self.spawned {
            self.spawned = true;
            if self.spawn_on_nearest_body {
                self.place_on_nearest_body();
            }
        }

        let position = self.base().get_global_position();
        let gravity = self.total_gravity(position);
        let up = if gravity.length_squared() > 1e-6 {
            -gravity.normalized()
        } else {
            self.base().get_global_basis().col_b()
        };

        self.update_orientation(up, dt);
        self.update_velocity(gravity, up, dt);
        self.base_mut().move_and_slide();
        self.update_head(dt);
    }
}

#[godot_api]
impl PlayerController {
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
        let height = body.bind().radius() + self.data.capsule_height * 0.5 + self.data.spawn_height;
        let basis = motion::align_up(self.base().get_global_basis(), dir, std::f32::consts::PI);
        self.base_mut().set_global_transform(Transform3D::new(basis, center + dir * height));
    }

    /// Keep the body upright against gravity and apply horizontal look (yaw).
    fn update_orientation(&mut self, up: Vector3, dt: f32) {
        let input = Input::singleton();
        let stick = input.get_vector("look_left", "look_right", "look_up", "look_down");
        let stick_speed = self.data.stick_look_speed_deg.to_radians() * dt;
        let mouse_scale = self.data.mouse_sensitivity_deg.to_radians();
        let yaw = stick.x * stick_speed + self.mouse_delta.x * mouse_scale;
        let mut pitch_input = stick.y * stick_speed + self.mouse_delta.y * mouse_scale;
        if self.data.invert_y {
            pitch_input = -pitch_input;
        }
        self.mouse_delta = Vector2::ZERO;

        let max_turn = self.data.align_speed_deg.to_radians() * dt;
        let aligned = motion::align_up(self.base().get_global_basis(), up, max_turn);
        let turned = (Basis::from_axis_angle(aligned.col_b().normalized(), -yaw) * aligned).orthonormalized();
        self.base_mut().set_global_basis(turned);
        self.base_mut().set_up_direction(up);

        let limit = self.data.pitch_limit_deg.to_radians();
        self.pitch = (self.pitch - pitch_input).clamp(-limit, limit);
    }

    fn update_velocity(&mut self, gravity: Vector3, up: Vector3, dt: f32) {
        let input = Input::singleton();
        let on_floor = self.base().is_on_floor();
        let crouching = input.is_action_pressed("crouch");

        let basis = self.base().get_global_basis();
        let forward = -basis.col_c();
        let right = basis.col_a();
        let stick = input.get_vector("move_left", "move_right", "move_forward", "move_back");
        let speed = if crouching { self.data.crouch_speed } else { self.data.walk_speed };
        let wish = (right * stick.x - forward * stick.y) * speed;

        let (mut vertical, horizontal) = motion::split_vertical(self.base().get_velocity(), up);
        let accel = if on_floor { self.data.ground_acceleration } else { self.data.air_acceleration };
        let horizontal = horizontal.move_toward(wish, accel * dt);

        if on_floor && input.is_action_just_pressed("jump") {
            vertical = up * self.data.jump_speed;
        } else {
            vertical += gravity * dt;
            if vertical.length() > self.data.max_fall_speed {
                vertical = vertical.normalized() * self.data.max_fall_speed;
            }
        }
        self.base_mut().set_velocity(horizontal + vertical);
    }

    fn update_head(&mut self, dt: f32) {
        let crouching = Input::singleton().is_action_pressed("crouch");
        let target = if crouching { self.data.crouch_eye_height } else { self.data.eye_height };
        let blend = (self.data.crouch_blend_speed * dt).min(1.0);
        self.eye_height += (target - self.eye_height) * blend;
        let (pitch, eye) = (self.pitch, self.eye_height);
        if let Some(head) = self.head.as_mut() {
            head.set_position(Vector3::new(0.0, eye, 0.0));
            head.set_rotation(Vector3::new(pitch, 0.0, 0.0));
        }
    }
}
