//! Flying limbs, ragdoll pieces, dropped guns and the little bursts at a
//! stump (CLAUDE.md section 7: limbs shoot off, ragdolls on death; gore
//! toggle turns gibs into sparks and bolts). Everything here is a physics
//! body under every body's gravity that rides the solar system like ship
//! debris, and fades out on a timer under the section 14 caps (60 gibs,
//! gibs fade after 20 s).
//!
//! Serves pillar 4 (everything is physical) and fun rule 11 (emergent,
//! screenshot-worthy moments).

use godot::classes::cpu_particles_3d::{DrawOrder, Parameter};
use godot::classes::{
    CapsuleShape3D, CollisionShape3D, CpuParticles3D, GeometryInstance3D, IRigidBody3D, MeshInstance3D, Node, Node3D, PhysicsMaterial,
    RigidBody3D, ShaderMaterial, SphereShape3D,
};
use godot::prelude::*;

use crate::orbits::body::{CelestialBody, GRAVITY_GROUP};
use crate::orbits::system::{FLOATING_GROUP, RIDER_GROUP};
use crate::player::controller::PLAYER_GROUP;

pub const GIB_GROUP: &str = "gibs";
pub const RAGDOLL_GROUP: &str = "ragdoll_pieces";
pub const PICKUP_GROUP: &str = "weapon_pickups";

fn body_gravity(tree: &Gd<godot::classes::SceneTree>, at: Vector3) -> Vector3 {
    tree.get_nodes_in_group(GRAVITY_GROUP)
        .iter_shared()
        .filter_map(|n| n.try_cast::<CelestialBody>().ok())
        .fold(Vector3::ZERO, |a, b| a + b.bind().gravity_at(at))
}

/// A loose physical piece (gib, ragdoll limb or thrown rock) that falls
/// toward whatever pulls it, then fades out and frees itself.
#[derive(GodotClass)]
#[class(base = RigidBody3D)]
pub struct Gib {
    /// Seconds left before it starts fading (negative: never).
    pub life_s: f32,
    pub fade_s: f32,
    fading: f32,
    /// Spawn order, so caps remove the oldest first.
    pub born: u64,
    /// A thrown rock: hurts the first enemy limb it flies through.
    projectile: Option<(Rid, Vector3)>,
    base: Base<RigidBody3D>,
}

#[godot_api]
impl IRigidBody3D for Gib {
    fn init(base: Base<RigidBody3D>) -> Self {
        Self { life_s: 20.0, fade_s: 1.5, fading: 0.0, born: 0, projectile: None, base }
    }

    fn ready(&mut self) {
        let mut b = self.base_mut();
        b.add_to_group(RIDER_GROUP);
        b.add_to_group(FLOATING_GROUP);
        b.set_gravity_scale(0.0);
        b.set_can_sleep(true);
    }

    fn physics_process(&mut self, delta: f64) {
        let dt = delta as f32;
        let tree = self.base().get_tree();
        let here = self.base().get_global_position();
        let g = body_gravity(&tree, here);
        let v = self.base().get_linear_velocity() + g * dt;
        self.base_mut().set_linear_velocity(v);
        self.sweep_projectile(here, v);
        if self.life_s < 0.0 {
            return;
        }
        if self.life_s > 0.0 {
            self.life_s -= dt;
            return;
        }
        self.fading += dt;
        let left = 1.0 - (self.fading / self.fade_s.max(0.01)).clamp(0.0, 1.0);
        set_fade(&self.base().clone().upcast(), left);
        if left <= 0.0 {
            self.base_mut().queue_free();
        }
    }
}

impl Gib {
    /// Make this piece a thrown projectile (ignores the thrower).
    pub fn arm_as_projectile(&mut self, thrower: Rid) {
        let p = self.base().get_global_position();
        self.projectile = Some((thrower, p));
    }

    fn sweep_projectile(&mut self, here: Vector3, v: Vector3) {
        let Some((thrower, prev)) = self.projectile else { return };
        if v.length() < 3.0 {
            // Too slow to hurt any more.
            self.projectile = None;
            return;
        }
        self.projectile = Some((thrower, here));
        let Some(world) = self.base().get_world_3d() else { return };
        let Some(mut space) = world.get_direct_space_state() else { return };
        let Some(mut q) = godot::classes::PhysicsRayQueryParameters3D::create_ex(prev, here + v.normalized() * 0.08).collision_mask(super::HITBOX_LAYER).done() else { return };
        q.set_collide_with_areas(true);
        q.set_collide_with_bodies(false);
        let hit = space.intersect_ray(&q);
        let Some(area) = hit.get("collider").and_then(|c| c.try_to::<Gd<godot::classes::Area3D>>().ok()) else { return };
        let limb = area.get_meta_ex("limb").default(&(-1i64).to_variant()).done().try_to::<i64>().unwrap_or(-1);
        let Ok(mut enemy) = area.get_meta_ex("enemy").default(&Variant::nil()).done().try_to::<Gd<crate::ai::enemy::Enemy>>() else { return };
        if limb < 0 {
            return;
        }
        let damage = self.base().get_meta_ex("thrown_damage").default(&6.0f32.to_variant()).done().try_to::<f32>().unwrap_or(6.0);
        let impulse = self.base().get_meta_ex("thrown_impulse").default(&2.0f32.to_variant()).done().try_to::<f32>().unwrap_or(2.0);
        let gore = crate::combat::load_enemies().gibs.gore;
        enemy.bind_mut().take_hit(limb as usize, damage, v.normalized(), impulse, prev, gore);
        self.projectile = None;
        // Bounce off comically.
        self.base_mut().set_linear_velocity(-v * 0.3);
    }
}

#[godot_api]
impl Gib {
    /// Start fading now (used by the caps).
    #[func]
    pub fn expire(&mut self) {
        if self.life_s != 0.0 {
            self.life_s = 0.0;
        }
    }
}

/// Set the dither fade on every faction-painted mesh under `node`.
pub fn set_fade(node: &Gd<Node>, amount: f32) {
    for n in node.find_children_ex("*").type_("MeshInstance3D").recursive(true).owned(false).done().iter_shared() {
        let Ok(mi) = n.try_cast::<MeshInstance3D>() else { continue };
        let count = mi.get_mesh().map_or(0, |m| m.get_surface_count());
        for s in 0..count {
            if let Some(mut m) = mi.get_surface_override_material(s).and_then(|m| m.try_cast::<ShaderMaterial>().ok()) {
                m.set_shader_parameter("fade", &amount.to_variant());
            } else if amount < 0.5 {
                // Plain materials (gun parts) just vanish at half faded.
                let mut g = mi.clone().upcast::<GeometryInstance3D>();
                g.set_visible(false);
            }
        }
    }
}

static BORN: std::sync::atomic::AtomicU64 = std::sync::atomic::AtomicU64::new(0);

fn next_born() -> u64 {
    BORN.fetch_add(1, std::sync::atomic::Ordering::Relaxed) + 1
}

/// Keep at most `max` members of `group`: the oldest start fading.
pub fn enforce_cap(tree: &Gd<godot::classes::SceneTree>, group: &str, max: usize) {
    let mut gibs: Vec<Gd<Gib>> = tree.get_nodes_in_group(group).iter_shared().filter_map(|n| n.try_cast::<Gib>().ok()).collect();
    if gibs.len() <= max {
        return;
    }
    gibs.sort_by_key(|g| g.bind().born);
    let extra = gibs.len() - max;
    for g in gibs.iter_mut().take(extra) {
        g.bind_mut().expire();
    }
}

/// How a new loose piece should behave.
pub struct PieceSpec {
    pub group: &'static str,
    pub life_s: f32,
    pub fade_s: f32,
    pub mass: f32,
    /// Capsule (radius, height) or, with height 0, a sphere.
    pub radius: f32,
    pub height: f32,
    /// Where the collider sits relative to the piece's origin.
    pub shape_offset: Transform3D,
    pub layer: u32,
    pub mask: u32,
}

/// Make a new loose piece at `transform` and move `meshes` into it, keeping
/// their world transforms. Returns the new body (already in the tree).
pub fn spawn_piece(world: &mut Gd<Node>, name: &str, meshes: &[Gd<Node3D>], transform: Transform3D, velocity: Vector3, spin: Vector3, spec: &PieceSpec) -> Gd<Gib> {
    let mut body = Gib::new_alloc();
    body.set_name(name);
    {
        let mut b = body.bind_mut();
        b.life_s = spec.life_s;
        b.fade_s = spec.fade_s;
        b.born = next_born();
    }
    body.set_mass(spec.mass.max(0.05));
    body.set_collision_layer(spec.layer);
    body.set_collision_mask(spec.mask);
    let mut mat = PhysicsMaterial::new_gd();
    mat.set_bounce(0.25);
    mat.set_friction(0.8);
    body.set_physics_material_override(&mat);
    world.add_child(&body);
    body.set_global_transform(transform);
    for m in meshes {
        let mut m = m.clone();
        let t = m.get_global_transform();
        if let Some(mut p) = m.get_parent() {
            p.remove_child(&m);
        }
        body.add_child(&m);
        m.set_global_transform(t);
    }
    let mut col = CollisionShape3D::new_alloc();
    if spec.height > spec.radius * 2.0 {
        let mut shape = CapsuleShape3D::new_gd();
        shape.set_radius(spec.radius);
        shape.set_height(spec.height);
        col.set_shape(&shape);
    } else {
        let mut shape = SphereShape3D::new_gd();
        shape.set_radius(spec.radius.max(0.02));
        col.set_shape(&shape);
    }
    col.set_transform(spec.shape_offset);
    body.add_child(&col);
    body.set_linear_velocity(velocity);
    body.set_angular_velocity(spin);
    body.add_to_group(spec.group);
    body
}

/// A self-freeing one-shot burst at a stump or impact: dark red puffs with
/// gore on, bright sparks and little bolts with it off.
pub fn stump_burst(world: &mut Gd<Node>, at: Vector3, dir: Vector3, gravity: Vector3, gore: bool, big: bool) {
    let mut p = CpuParticles3D::new_alloc();
    p.set_name("StumpBurst");
    p.set_one_shot(true);
    p.set_explosiveness_ratio(0.95);
    p.set_amount(if big { 28 } else { 12 });
    p.set_lifetime(if gore { 0.9 } else { 0.5 });
    p.set_use_local_coordinates(false);
    p.set_draw_order(DrawOrder::VIEW_DEPTH);
    let size = if gore { 0.14 } else { 0.05 };
    let mut mesh = crate::player::effects::puff_mesh(!gore, (0.1, 0.3));
    mesh.set_size(Vector2::new(size, size));
    p.set_mesh(&mesh);
    p.set_direction(dir);
    p.set_spread(if gore { 55.0 } else { 70.0 });
    p.set_gravity(gravity);
    p.set_param_min(Parameter::INITIAL_LINEAR_VELOCITY, if gore { 1.0 } else { 3.0 });
    p.set_param_max(Parameter::INITIAL_LINEAR_VELOCITY, if gore { 3.0 } else { 7.0 });
    p.set_param_min(Parameter::SCALE, 0.6);
    p.set_param_max(Parameter::SCALE, 1.3);
    let c = if gore { Color::from_rgb(0.42, 0.06, 0.05) } else { Color::from_rgb(1.0, 0.75, 0.3) };
    let fade = Color::from_rgba(c.r, c.g, c.b, 0.0);
    p.set_color_ramp(&crate::player::effects::ramp(&[(0.0, c, 1.0), (0.6, c, 0.8), (1.0, fade, 0.0)]));
    world.add_child(&p);
    p.set_global_position(at);
    p.set_emitting(true);
    let mut timer = world.get_tree().create_timer(1.5);
    let target = p.clone();
    timer.connect("timeout", &Callable::from_fn("free_burst", move |_| {
        let mut t = target.clone();
        if t.is_instance_valid() {
            t.queue_free();
        }
        Variant::nil()
    }));
    let _ = &mut timer;
}

/// A dropped gun: walk over it for ammo if you carry the same gun; press
/// Interact while looking at it to swap guns.
#[derive(GodotClass)]
#[class(base = RigidBody3D)]
pub struct WeaponPickup {
    #[var]
    pub weapon: GString,
    #[var]
    pub ammo: i64,
    pub life_s: f32,
    pub radius_m: f32,
    base: Base<RigidBody3D>,
}

#[godot_api]
impl IRigidBody3D for WeaponPickup {
    fn init(base: Base<RigidBody3D>) -> Self {
        Self { weapon: GString::new(), ammo: 0, life_s: 90.0, radius_m: 1.6, base }
    }

    fn ready(&mut self) {
        let mut b = self.base_mut();
        b.add_to_group(RIDER_GROUP);
        b.add_to_group(FLOATING_GROUP);
        b.add_to_group(PICKUP_GROUP);
        b.set_gravity_scale(0.0);
    }

    fn physics_process(&mut self, delta: f64) {
        let dt = delta as f32;
        let tree = self.base().get_tree();
        let here = self.base().get_global_position();
        let g = body_gravity(&tree, here);
        let v = self.base().get_linear_velocity() + g * dt;
        self.base_mut().set_linear_velocity(v);
        self.life_s -= dt;
        if self.life_s <= 0.0 {
            self.base_mut().queue_free();
            return;
        }
        // Walk-over ammo.
        if self.ammo <= 0 {
            return;
        }
        let Some(player) = tree.get_first_node_in_group(PLAYER_GROUP).and_then(|p| p.try_cast::<Node3D>().ok()) else { return };
        if player.get_global_position().distance_to(here) > self.radius_m + 0.9 {
            return;
        }
        let Some(mut arsenal) = player.find_child_ex("Arsenal").recursive(true).owned(false).done().and_then(|n| n.try_cast::<super::arsenal::Arsenal>().ok()) else {
            return;
        };
        let weapon = self.weapon.to_string();
        let took = arsenal.bind_mut().take_ammo(&weapon, self.ammo as u32);
        if took > 0 {
            self.ammo -= took as i64;
        }
    }
}

#[godot_api]
impl WeaponPickup {}

/// Drop a gun model as a pickup at `transform`.
pub fn drop_weapon(world: &mut Gd<Node>, weapon: &str, ammo: u32, model: Gd<Node3D>, transform: Transform3D, velocity: Vector3, life_s: f32, radius_m: f32) -> Gd<WeaponPickup> {
    let mut body = WeaponPickup::new_alloc();
    body.set_name(&format!("Dropped_{weapon}"));
    {
        let mut b = body.bind_mut();
        b.weapon = GString::from(weapon);
        b.ammo = ammo as i64;
        b.life_s = life_s;
        b.radius_m = radius_m;
    }
    body.set_mass(3.0);
    body.set_collision_layer(super::PROP_LAYER);
    body.set_collision_mask(1 | super::PROP_LAYER);
    world.add_child(&body);
    body.set_global_transform(transform);
    let mut m = model;
    let t = m.get_global_transform();
    if let Some(mut p) = m.get_parent() {
        p.remove_child(&m);
        body.add_child(&m);
        m.set_global_transform(t);
    } else {
        body.add_child(&m);
        m.set_transform(Transform3D::IDENTITY);
    }
    let mut shape = CapsuleShape3D::new_gd();
    shape.set_radius(0.06);
    shape.set_height(0.7);
    let mut col = CollisionShape3D::new_alloc();
    col.set_shape(&shape);
    col.set_transform(Transform3D::new(Basis::from_axis_angle(Vector3::RIGHT, std::f32::consts::FRAC_PI_2), Vector3::new(0.0, 0.03, -0.25)));
    body.add_child(&col);
    body.set_linear_velocity(velocity);
    body.set_angular_velocity(Vector3::new(1.5, 2.0, 0.5));
    body
}
