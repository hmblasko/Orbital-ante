//! The player's weapons (CLAUDE.md section 7): one gun, the throwable rock,
//! a melee swing (the bat when found, otherwise a shove) and, in Part 3,
//! the rope gun. Reads input, draws the first-person model, fires beams by
//! ray (hitscan) against enemy limb hitboxes and loose physics props, and
//! gives the section 7 hit feedback: hit marker, brief hit-stop on gibs and
//! big impacts, camera kick (toggleable in data) and a muzzle flash.
//!
//! Lives under the player's Head node, next to the camera.
//!
//! Serves pillar 2 (goofy but deadly), pillar 4 (shots push props, gibs and
//! ragdolls with the same physics) and fun rule 10 (game feel).

use std::collections::HashMap;

use godot::classes::base_material_3d::{BlendMode, ShadingMode, Transparency};
use godot::classes::{
    Area3D, Camera3D, CylinderMesh, Engine, INode3D, Input, MeshInstance3D, Node, Node3D, OmniLight3D, PhysicsRayQueryParameters3D,
    RigidBody3D, StandardMaterial3D, Time,
};
use godot::prelude::*;

use super::gibs::{GIB_GROUP, PICKUP_GROUP, PieceSpec, WeaponPickup, enforce_cap, spawn_piece};
use super::weapons::{Ammo, WeaponData, WeaponKind, WeaponsData, falloff, load_data, pellet_directions, spread_deg};
use super::{HITBOX_LAYER, PROP_LAYER};
use crate::ai::enemy::{ENEMY_GROUP, Enemy, hear_noise};
use crate::player::PlayerController;
use crate::seed::SeededRng;

/// Planets and props on layer 1, ship hulls on 4.
const WORLD_MASK: u32 = 1 | 4;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum Held {
    Gun,
    Rock,
}

/// What the last hit did, for the hit marker.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum HitKind {
    Hit,
    Gib,
    Kill,
}

struct Beam {
    node: Gd<MeshInstance3D>,
    life: f32,
    total: f32,
}

#[derive(GodotClass)]
#[class(base = Node3D)]
pub struct Arsenal {
    data: WeaponsData,
    gun: Option<String>,
    melee: Option<String>,
    ammo: HashMap<String, Ammo>,
    held: Held,
    cooldown: f32,
    melee_cooldown: f32,
    reload_left: f32,
    view: Option<Gd<Node3D>>,
    view_id: String,
    view_root: Option<Gd<Node3D>>,
    camera: Option<Gd<Camera3D>>,
    base_fov: f32,
    aim_t: f32,
    kick: f32,
    swing_t: f32,
    bob_s: f32,
    hit_marker: f32,
    hit_kind: HitKind,
    /// Real-time (microseconds) when the hit-stop ends.
    hitstop_until_us: u64,
    rng: SeededRng,
    beams: Vec<Beam>,
    flash: Option<Gd<OmniLight3D>>,
    flash_left: f32,
    shots: i64,
    hits: i64,
    gore: bool,
    pickup_radius: f32,
    max_gibs: usize,
    gib_life: f32,
    fade_s: f32,
    /// Tests: pull the trigger / swing for one tick.
    debug_fire: bool,
    debug_melee: bool,
    debug_rope: bool,
    rope_shots: u32,
    carrying: Option<Gd<Enemy>>,
    base: Base<Node3D>,
}

#[godot_api]
impl INode3D for Arsenal {
    fn init(base: Base<Node3D>) -> Self {
        let data = load_data();
        let mut ammo = HashMap::new();
        for w in &data.weapons {
            ammo.insert(w.id.clone(), Ammo::full(w));
        }
        let enemies = super::load_enemies();
        Self {
            gun: Some(data.start_gun.clone()),
            melee: data.start_melee.clone(),
            data,
            ammo,
            held: Held::Gun,
            cooldown: 0.0,
            melee_cooldown: 0.0,
            reload_left: 0.0,
            view: None,
            view_id: String::new(),
            view_root: None,
            camera: None,
            base_fov: 75.0,
            aim_t: 0.0,
            kick: 0.0,
            swing_t: 0.0,
            bob_s: 0.0,
            hit_marker: 0.0,
            hit_kind: HitKind::Hit,
            hitstop_until_us: 0,
            rng: SeededRng::derive(crate::seed::safe_run_seed(), "player_weapons"),
            beams: Vec::new(),
            flash: None,
            flash_left: 0.0,
            shots: 0,
            hits: 0,
            gore: enemies.gibs.gore,
            pickup_radius: enemies.gibs.pickup_radius_m,
            max_gibs: enemies.gibs.max_gibs as usize,
            gib_life: enemies.gibs.gib_life_s,
            fade_s: enemies.gibs.fade_s,
            debug_fire: false,
            debug_melee: false,
            debug_rope: false,
            rope_shots: 0,
            carrying: None,
            base,
        }
    }

    fn ready(&mut self) {
        self.rope_shots = self.data.rope.shots_per_run;
        let camera = self.base().get_parent().and_then(|p| p.get_node_or_null("Camera3D")).and_then(|n| n.try_cast::<Camera3D>().ok());
        if let Some(cam) = &camera {
            self.base_fov = cam.get_fov();
            let mut root = Node3D::new_alloc();
            root.set_name("ViewModel");
            let mut cam = cam.clone();
            cam.add_child(&root);
            self.view_root = Some(root);
            let mut fill = OmniLight3D::new_alloc();
            fill.set_name("ViewFill");
            fill.set_param(godot::classes::light_3d::Param::RANGE, self.data.view.fill_range_m);
            fill.set_param(godot::classes::light_3d::Param::ENERGY, self.data.view.fill_energy);
            fill.set_cull_mask(VIEW_LAYER);
            fill.set_position(Vector3::new(-0.2, 0.25, 0.1));
            cam.add_child(&fill);
            let mut flash = OmniLight3D::new_alloc();
            flash.set_name("MuzzleFlash");
            flash.set_param(godot::classes::light_3d::Param::RANGE, 6.0);
            flash.set_param(godot::classes::light_3d::Param::ENERGY, 0.0);
            flash.set_position(Vector3::new(0.15, -0.1, -0.9));
            cam.add_child(&flash);
            self.flash = Some(flash);
        }
        self.camera = camera;
    }

    fn physics_process(&mut self, delta: f64) {
        let dt = delta as f32;
        self.end_hitstop();
        let Some(player) = self.player() else { return };
        let (dead, buckled, crouched) = {
            let p = player.bind();
            (p.is_dead(), p.is_buckled(), p.is_crouched())
        };
        let _ = crouched;
        let active = !dead && !buckled;
        self.update_beams(dt);
        self.cooldown = (self.cooldown - dt).max(0.0);
        self.melee_cooldown = (self.melee_cooldown - dt).max(0.0);
        self.hit_marker = (self.hit_marker - dt).max(0.0);
        self.flash_left = (self.flash_left - dt).max(0.0);
        if let Some(f) = &mut self.flash {
            f.set_param(godot::classes::light_3d::Param::ENERGY, if self.flash_left > 0.0 { 3.0 } else { 0.0 });
        }
        if let Some(r) = &mut self.view_root {
            r.set_visible(active);
        }
        if self.carrying.as_ref().is_some_and(|e| !e.is_instance_valid() || e.bind().is_dead()) {
            self.carrying = None;
        }
        if !active {
            // Dead or buckled in: whatever you carried is set down.
            self.put_down(&player);
            self.reload_left = 0.0;
            self.set_fov(0.0);
            return;
        }
        let input = Input::singleton();
        // Reloading.
        if self.reload_left > 0.0 {
            self.reload_left -= dt;
            if self.reload_left <= 0.0 {
                if let Some(w) = self.current().cloned() {
                    self.ammo.entry(w.id.clone()).or_insert(Ammo::full(&w)).reload(&w);
                }
            }
        }
        if input.is_action_just_pressed("swap_weapon") {
            self.held = if self.held == Held::Gun || self.gun.is_none() { Held::Rock } else { Held::Gun };
            self.reload_left = 0.0;
            self.cooldown = self.cooldown.max(0.25);
        }
        if input.is_action_just_pressed("reload") {
            self.start_reload();
        }
        if self.carrying.is_some() {
            self.hold_carried(&player);
        }
        if input.is_action_just_pressed("rope_gun") || self.debug_rope {
            self.debug_rope = false;
            self.fire_rope();
        }
        if input.is_action_just_pressed("interact") && !self.try_carry_or_drop(&player) && !self.try_swap_pickup() {
            // X with nothing else to do reloads (controller has no reload button).
            if player.bind().get_interact_prompt().is_empty() {
                self.start_reload();
            }
        }
        let aiming = input.is_action_pressed("aim") && self.held == Held::Gun;
        let target = if aiming { 1.0 } else { 0.0 };
        let rate = dt / self.data.aim_ease_s.max(0.01);
        self.aim_t += (target - self.aim_t).clamp(-rate, rate);
        self.set_fov(self.aim_t);

        let Some(w) = self.current().cloned() else { return };
        let pressed = if w.automatic { input.is_action_pressed("fire") } else { input.is_action_just_pressed("fire") };
        if (pressed || self.debug_fire) && self.cooldown <= 0.0 && self.reload_left <= 0.0 {
            self.debug_fire = false;
            self.fire(&w, aiming, &player);
        }
        if input.is_action_just_pressed("melee") || self.debug_melee {
            self.debug_melee = false;
            if self.melee_cooldown <= 0.0 {
                self.swing(&player);
            }
        }
        self.animate(dt, &player, &w);
    }
}

#[godot_api]
impl Arsenal {
    #[func]
    pub fn get_weapon_id(&self) -> GString {
        GString::from(self.current().map_or("", |w| w.id.as_str()))
    }

    #[func]
    pub fn get_weapon_name_key(&self) -> GString {
        GString::from(self.current().map_or("", |w| w.name_key.as_str()))
    }

    #[func]
    pub fn get_loaded(&self) -> i64 {
        self.current().and_then(|w| self.ammo.get(&w.id)).map_or(0, |a| a.loaded as i64)
    }

    #[func]
    pub fn get_reserve(&self) -> i64 {
        self.current().and_then(|w| self.ammo.get(&w.id)).map_or(0, |a| a.reserve as i64)
    }

    #[func]
    pub fn has_infinite_ammo(&self) -> bool {
        self.current().is_none_or(Ammo::infinite)
    }

    #[func]
    pub fn get_reload_fraction(&self) -> f32 {
        match self.current() {
            Some(w) if self.reload_left > 0.0 => 1.0 - self.reload_left / w.reload_s.max(0.01),
            _ => 0.0,
        }
    }

    /// 0..1, fading after a hit; get_hit_kind says what it was (0 hit, 1 gib, 2 kill).
    #[func]
    pub fn get_hit_marker(&self) -> f32 {
        self.hit_marker / self.data.hit_marker_s.max(0.01)
    }

    #[func]
    pub fn get_hit_kind(&self) -> i64 {
        match self.hit_kind {
            HitKind::Hit => 0,
            HitKind::Gib => 1,
            HitKind::Kill => 2,
        }
    }

    #[func]
    pub fn get_aim(&self) -> f32 {
        self.aim_t
    }

    #[func]
    pub fn get_shots(&self) -> i64 {
        self.shots
    }

    #[func]
    pub fn get_hits(&self) -> i64 {
        self.hits
    }

    #[func]
    pub fn get_melee_id(&self) -> GString {
        GString::from(self.melee.as_deref().unwrap_or(""))
    }

    /// A dropped gun you're looking at, for the HUD prompt ("" when none).
    #[func]
    pub fn get_pickup_prompt(&self) -> GString {
        self.pickup_in_view().map_or(GString::new(), |p| p.bind().weapon.clone())
    }

    #[func]
    pub fn get_rope_shots(&self) -> i64 {
        self.rope_shots as i64
    }

    #[func]
    pub fn is_carrying(&self) -> bool {
        self.carrying.is_some()
    }

    /// "carry" when looking at a tied target in reach, "drop" while
    /// carrying, else empty.
    #[func]
    pub fn get_carry_prompt(&self) -> GString {
        if self.carrying.is_some() {
            GString::from("drop")
        } else if self.tied_in_view().is_some() {
            GString::from("carry")
        } else {
            GString::new()
        }
    }

    #[func]
    pub fn debug_rope_once(&mut self) {
        self.debug_rope = true;
    }

    /// Tests: press X (carry, drop, swap or reload).
    #[func]
    pub fn debug_interact(&mut self) {
        let Some(player) = self.player() else { return };
        if !self.try_carry_or_drop(&player) {
            self.try_swap_pickup();
        }
    }

    #[func]
    pub fn debug_fire_once(&mut self) {
        self.debug_fire = true;
        self.cooldown = 0.0;
    }

    #[func]
    pub fn debug_swing(&mut self) {
        self.debug_melee = true;
        self.melee_cooldown = 0.0;
    }

    #[func]
    pub fn debug_give(&mut self, weapon: GString) {
        let id = weapon.to_string();
        let Some(w) = self.data.get(&id).cloned() else { return };
        match w.kind {
            WeaponKind::Gun => {
                self.gun = Some(id.clone());
                self.held = Held::Gun;
                self.ammo.insert(id, Ammo::full(&w));
            }
            WeaponKind::Melee => self.melee = Some(id),
            WeaponKind::Thrown => self.held = Held::Rock,
        }
    }

    #[func]
    pub fn debug_hold_rock(&mut self, on: bool) {
        self.held = if on { Held::Rock } else { Held::Gun };
    }
}

impl Arsenal {
    fn player(&self) -> Option<Gd<PlayerController>> {
        let mut n = self.base().get_parent();
        while let Some(node) = n {
            if let Ok(p) = node.clone().try_cast::<PlayerController>() {
                return Some(p);
            }
            n = node.get_parent();
        }
        None
    }

    fn current(&self) -> Option<&WeaponData> {
        match self.held {
            Held::Gun => self.gun.as_deref().and_then(|g| self.data.get(g)),
            Held::Rock => self.data.get("rock"),
        }
    }

    /// Ammo from a dropped gun of the same kind; returns rounds taken.
    pub fn take_ammo(&mut self, weapon: &str, amount: u32) -> u32 {
        if self.gun.as_deref() != Some(weapon) {
            return 0;
        }
        let Some(w) = self.data.get(weapon).cloned() else { return 0 };
        self.ammo.entry(weapon.to_string()).or_insert(Ammo::full(&w)).add(&w, amount)
    }

    fn start_reload(&mut self) {
        let Some(w) = self.current().cloned() else { return };
        if self.reload_left > 0.0 {
            return;
        }
        if self.ammo.get(&w.id).is_some_and(|a| a.needs_reload(&w)) {
            self.reload_left = w.reload_s;
        }
    }

    fn set_fov(&mut self, aim: f32) {
        let fov = self.base_fov + (self.data.aim_fov - self.base_fov) * aim;
        if let Some(c) = &mut self.camera {
            c.set_fov(fov);
        }
    }

    fn ray(&self, from: Vector3, to: Vector3, mask: u32, exclude: &[Rid]) -> Option<VarDictionary> {
        let world = self.base().get_world_3d()?;
        let mut space = world.get_direct_space_state()?;
        let mut q = PhysicsRayQueryParameters3D::create_ex(from, to).collision_mask(mask).done()?;
        q.set_collide_with_areas(true);
        let ex: Array<Rid> = exclude.iter().copied().collect();
        q.set_exclude(&ex);
        let hit = space.intersect_ray(&q);
        if hit.is_empty() { None } else { Some(hit) }
    }

    fn eye(&self) -> (Vector3, Vector3) {
        match &self.camera {
            Some(c) => {
                let t = c.get_global_transform();
                (t.origin, -t.basis.col_c())
            }
            None => (self.base().get_global_position(), -self.base().get_global_basis().col_c()),
        }
    }

    fn muzzle_world(&self, w: &WeaponData) -> Vector3 {
        match &self.view {
            Some(v) if self.view_id == w.id => v.get_global_transform() * super::meshes::muzzle(&w.id),
            _ => self.eye().0,
        }
    }

    fn fire(&mut self, w: &WeaponData, aiming: bool, player: &Gd<PlayerController>) {
        let ammo = self.ammo.entry(w.id.clone()).or_insert(Ammo::full(w));
        if !ammo.can_fire(w) {
            self.start_reload();
            return;
        }
        ammo.spend(w);
        let empty_now = !ammo.can_fire(w);
        self.cooldown = w.fire_interval_s;
        self.shots += 1;
        let (eye, forward) = self.eye();
        hear_noise(&self.base().get_tree(), eye, w.noise_m, None);
        if w.kind == WeaponKind::Thrown {
            self.throw_rock(w, player);
            return;
        }
        let moving = player.get_velocity().length() > 1.5;
        let cone = spread_deg(w, aiming, moving);
        let dirs = pellet_directions(forward, cone, w.pellets, &mut self.rng);
        let muzzle = self.muzzle_world(w);
        let exclude = [player.get_rid()];
        let mut best = HitKind::Hit;
        let mut any = false;
        let mut stop = false;
        for d in dirs {
            let to = eye + d * w.range_m;
            let end = match self.ray(eye, to, WORLD_MASK | HITBOX_LAYER | PROP_LAYER, &exclude) {
                Some(hit) => {
                    let at: Vector3 = hit.get("position").map(|v| v.to()).unwrap_or(to);
                    let collider: Option<Gd<Object>> = hit.get("collider").and_then(|v| v.try_to().ok());
                    let dist = eye.distance_to(at);
                    let dmg = w.damage * falloff(w, dist);
                    if let Some(kind) = self.apply_hit(collider, at, d, dmg, w.impulse) {
                        any = true;
                        if kind != HitKind::Hit {
                            stop = true;
                        }
                        best = best.max_by(kind);
                    }
                    at
                }
                None => to,
            };
            self.spawn_beam(muzzle, end, w);
        }
        if any {
            self.mark_hit(best);
            if stop && w.hit_stop_s > 0.0 {
                self.hitstop(w.hit_stop_s);
            }
        }
        self.flash_left = 0.05 * w.muzzle_light.min(1.0) + 0.02;
        if let Some(f) = &mut self.flash {
            let c = w.beam_color;
            f.set_color(Color::from_rgb(c.0, c.1, c.2));
        }
        if self.data.camera_kick {
            self.kick += w.kick_deg;
        }
        if empty_now {
            self.start_reload();
        }
    }

    fn mark_hit(&mut self, kind: HitKind) {
        self.hits += 1;
        self.hit_marker = self.data.hit_marker_s;
        if kind != HitKind::Hit || self.hit_kind == HitKind::Hit {
            self.hit_kind = kind;
        }
    }

    /// Hit whatever `collider` is: an enemy limb, or a loose prop to push.
    fn apply_hit(&mut self, collider: Option<Gd<Object>>, at: Vector3, dir: Vector3, damage: f32, impulse: f32) -> Option<HitKind> {
        let collider = collider?;
        if let Ok(area) = collider.clone().try_cast::<Area3D>() {
            let limb = area.get_meta_ex("limb").default(&(-1i64).to_variant()).done().try_to::<i64>().unwrap_or(-1);
            let mut enemy = area.get_meta_ex("enemy").default(&Variant::nil()).done().try_to::<Gd<Enemy>>().ok()?;
            if limb < 0 {
                return None;
            }
            let shooter = self.eye().0;
            let (damage, heal_on_kill) = self.build_damage(damage);
            let result = enemy.bind_mut().take_hit(limb as usize, damage, dir, impulse, shooter, self.gore);
            if result == 2 && heal_on_kill > 0.0
                && let Some(mut p) = self.player()
            {
                p.bind_mut().heal_fraction(heal_on_kill);
            }
            return Some(match result {
                2 => HitKind::Kill,
                1 => HitKind::Gib,
                _ => HitKind::Hit,
            });
        }
        if let Ok(mut body) = collider.try_cast::<RigidBody3D>() {
            let offset = at - body.get_global_position();
            let mass = body.get_mass().clamp(0.2, 4.0);
            body.apply_impulse_ex(dir * impulse * mass).position(offset).done();
        }
        None
    }

    /// Damage after the run's build (Glass Cannon, Damage attribute, Heat
    /// at 2: more below half health), and the Heat-at-4 heal a kill gives
    /// below a quarter health (0 when none).
    fn build_damage(&self, damage: f32) -> (f32, f32) {
        let mut d = damage * crate::points::singleton::stat("damage_dealt");
        let health = self.player().map_or(1.0, |p| p.bind().get_health_fraction());
        let mut heal = 0.0;
        let heat_two = crate::points::singleton::flag("heat_low_health_damage");
        let heat_four = crate::points::singleton::flag("heat_kill_heal");
        if heat_two || heat_four {
            let perks = crate::points::singleton::perk_numbers();
            if heat_two && health < perks.0 {
                d *= perks.1;
            }
            if heat_four && health < perks.2 {
                heal = perks.3;
            }
        }
        (d, heal)
    }

    fn swing(&mut self, player: &Gd<PlayerController>) {
        let (damage, range, impulse, interval, stop) = match self.melee.as_deref().and_then(|m| self.data.get(m)) {
            Some(w) => (w.damage, w.range_m, w.impulse, w.fire_interval_s, w.hit_stop_s),
            None => {
                let s = &self.data.shove;
                (s.damage, s.range_m, s.impulse, s.interval_s, 0.0)
            }
        };
        self.melee_cooldown = interval;
        self.swing_t = 1.0;
        let (eye, forward) = self.eye();
        // Fan the swing across the view, whichever way is up on this body.
        let right = self.camera.as_ref().map_or(forward.cross(Vector3::UP).normalized_or_zero(), |c| c.get_global_basis().col_a());
        let exclude = [player.get_rid()];
        // A small fan of rays so a swing doesn't need pixel aim.
        for side in [0.0f32, -0.25, 0.25] {
            let d = (forward + right * side).normalized();
            if let Some(hit) = self.ray(eye, eye + d * range, WORLD_MASK | HITBOX_LAYER | PROP_LAYER, &exclude) {
                let at: Vector3 = hit.get("position").map(|v| v.to()).unwrap_or(eye);
                let collider: Option<Gd<Object>> = hit.get("collider").and_then(|v| v.try_to().ok());
                if let Some(kind) = self.apply_hit(collider, at, d, damage, impulse) {
                    self.mark_hit(kind);
                    if stop > 0.0 {
                        self.hitstop(stop);
                    }
                }
                break;
            }
        }
        hear_noise(&self.base().get_tree(), eye, 6.0, None);
    }

    fn throw_rock(&mut self, w: &WeaponData, player: &Gd<PlayerController>) {
        let Some(model) = super::meshes::weapon_model("rock") else { return };
        let (eye, forward) = self.eye();
        let up = player.get_global_basis().col_b();
        let mut world = self.world_node();
        let start = eye + forward * 0.5 - up * 0.1;
        let mut holder = model.clone();
        world.add_child(&holder);
        holder.set_global_position(start);
        let v = player.get_velocity() + (forward + up * w.throw_up).normalized() * w.throw_speed;
        let spec = PieceSpec {
            group: GIB_GROUP,
            life_s: self.gib_life,
            fade_s: self.fade_s,
            mass: 0.4,
            radius: 0.06,
            height: 0.0,
            shape_offset: Transform3D::IDENTITY,
            layer: PROP_LAYER,
            mask: 1 | 4 | PROP_LAYER,
        };
        let mut rock = spawn_piece(&mut world, "ThrownRock", &[holder], Transform3D::new(Basis::IDENTITY, start), v, Vector3::new(4.0, 2.0, 1.0), &spec);
        rock.set_meta("thrown_damage", &w.damage.to_variant());
        rock.set_meta("thrown_impulse", &w.impulse.to_variant());
        rock.bind_mut().arm_as_projectile(player.get_rid());
        enforce_cap(&self.base().get_tree(), GIB_GROUP, self.max_gibs);
    }

    fn world_node(&self) -> Gd<Node> {
        let tree = self.base().get_tree();
        tree.get_current_scene().unwrap_or_else(|| tree.get_root().upcast())
    }

    fn pickup_in_view(&self) -> Option<Gd<WeaponPickup>> {
        let (eye, forward) = self.eye();
        let tree = self.base().get_tree();
        let mut best: Option<(f32, Gd<WeaponPickup>)> = None;
        for n in tree.get_nodes_in_group(PICKUP_GROUP).iter_shared() {
            let Ok(p) = n.try_cast::<WeaponPickup>() else { continue };
            let to = p.get_global_position() - eye;
            let dist = to.length();
            if dist > 2.6 || dist < 0.01 {
                continue;
            }
            let facing = forward.dot(to / dist);
            if facing < 0.85 {
                continue;
            }
            if self.gun.as_deref() == Some(p.bind().weapon.to_string().as_str()) {
                continue;
            }
            if best.as_ref().is_none_or(|(f, _)| facing > *f) {
                best = Some((facing, p));
            }
        }
        best.map(|(_, p)| p)
    }

    /// Fire the rope: a tied target can't fight and drops its gun.
    fn fire_rope(&mut self) {
        if self.rope_shots == 0 {
            return;
        }
        self.rope_shots -= 1;
        let r = self.data.rope.clone();
        let (eye, forward) = self.eye();
        let player_rid = self.player().map(|p| p.get_rid());
        let ex: Vec<Rid> = player_rid.into_iter().collect();
        let to = eye + forward * r.range_m;
        let mut end = to;
        if let Some(hit) = self.ray(eye, to, WORLD_MASK | HITBOX_LAYER, &ex) {
            end = hit.get("position").map_or(to, |p| p.to());
            let collider: Option<Gd<Object>> = hit.get("collider").and_then(|c| c.try_to().ok());
            if let Some(area) = collider.and_then(|c| c.try_cast::<godot::classes::Area3D>().ok())
                && let Ok(mut enemy) = area.get_meta_ex("enemy").default(&Variant::nil()).done().try_to::<Gd<Enemy>>()
                && enemy.bind_mut().tie_up(eye)
            {
                self.hit_marker = self.data.hit_marker_s;
                self.hit_kind = HitKind::Hit;
            }
        }
        let start = eye + forward * 0.4 + self.camera.as_ref().map_or(Vector3::ZERO, |c| c.get_global_basis().col_a() * 0.12 - c.get_global_basis().col_b() * 0.1);
        let mut world = self.world_node();
        crate::ai::enemy::spawn_beam(&mut world, start, end, r.color, r.width, r.beam_life_s);
        hear_noise(&self.base().get_tree(), eye, r.noise_m, None);
    }

    /// The tied soldier you're looking at, within reach.
    fn tied_in_view(&self) -> Option<Gd<Enemy>> {
        let r = &self.data.rope;
        let (eye, forward) = self.eye();
        let mut best: Option<(f32, Gd<Enemy>)> = None;
        for n in self.base().get_tree().get_nodes_in_group(ENEMY_GROUP).iter_shared() {
            let Ok(e) = n.try_cast::<Enemy>() else { continue };
            {
                let b = e.bind();
                if !b.is_tied() || b.is_dead() || b.is_carried() {
                    continue;
                }
            }
            // Aim at the middle of the body.
            let up = e.get_global_basis().col_b();
            let to = e.get_global_position() + up * 0.9 - eye;
            let dist = to.length();
            if dist > r.pick_range_m + 0.9 || dist < 0.01 {
                continue;
            }
            let facing = forward.dot(to / dist);
            if facing < r.pick_facing {
                continue;
            }
            if best.as_ref().is_none_or(|(f, _)| facing > *f) {
                best = Some((facing, e));
            }
        }
        best.map(|(_, e)| e)
    }

    /// X while carrying puts it down; X at a tied soldier picks it up.
    fn try_carry_or_drop(&mut self, player: &Gd<PlayerController>) -> bool {
        if self.carrying.is_some() {
            self.put_down(player);
            return true;
        }
        let Some(mut e) = self.tied_in_view() else { return false };
        e.bind_mut().set_carried(true);
        self.carrying = Some(e);
        self.hold_carried(player);
        true
    }

    /// Carried targets lie along your view, under your right arm.
    fn hold_carried(&mut self, player: &Gd<PlayerController>) {
        let Some(mut e) = self.carrying.clone() else { return };
        let Some(cam) = &self.camera else { return };
        let ct = cam.get_global_transform();
        let up = player.get_up_direction().normalized_or_zero();
        let up = if up.length_squared() < 0.5 { ct.basis.col_b() } else { up };
        let fwd = -ct.basis.col_c();
        let flat = (fwd - up * fwd.dot(up)).normalized_or_zero();
        let flat = if flat.length_squared() < 0.5 { -ct.basis.col_b() } else { flat };
        let right = flat.cross(up).normalized();
        let o = self.data.rope.carry_offset;
        // The body's own up runs forward; it faces down.
        let y = flat;
        let z = up;
        let x = y.cross(z).normalized();
        let at = ct.origin + right * o.0 + up * o.1 - flat * o.2;
        e.set_global_transform(Transform3D::new(Basis::from_cols(x, y, z), at));
        e.set_velocity(player.get_velocity());
    }

    fn put_down(&mut self, player: &Gd<PlayerController>) {
        let Some(mut e) = self.carrying.take() else { return };
        if !e.is_instance_valid() {
            return;
        }
        let up = player.get_up_direction().normalized_or_zero();
        let (_, forward) = self.eye();
        let flat = (forward - up * forward.dot(up)).normalized_or_zero();
        let flat = if flat.length_squared() < 0.5 { forward } else { flat };
        let p = e.get_global_position();
        // Stand it back upright, in front of you.
        let at = player.get_global_position() + flat * 1.1 + up * 0.1;
        let up = if up.length_squared() < 0.5 { e.get_global_basis().col_b() } else { up };
        let x = (-flat).cross(up).normalized_or_zero();
        let basis = if x.length_squared() > 0.5 { Basis::from_cols(x, up, -(-flat)).orthonormalized() } else { e.get_global_basis() };
        let _ = p;
        e.set_global_transform(Transform3D::new(basis, at));
        e.set_velocity(player.get_velocity() + flat * self.data.rope.drop_push_mps);
        e.bind_mut().set_carried(false);
    }

    /// Swap your gun for the dropped one you're looking at.
    fn try_swap_pickup(&mut self) -> bool {
        let Some(mut pickup) = self.pickup_in_view() else { return false };
        let new_id = pickup.bind().weapon.to_string();
        let Some(new_w) = self.data.get(&new_id).cloned() else { return false };
        if new_w.kind == WeaponKind::Melee {
            self.melee = Some(new_id);
            pickup.queue_free();
            return true;
        }
        let rounds = pickup.bind().ammo.max(0) as u32;
        // Drop the old gun where the new one was, with its ammo.
        if let Some(old) = self.gun.clone()
            && let Some(model) = super::meshes::weapon_model(&old)
        {
            let old_ammo = self.ammo.get(&old).map_or(0, |a| a.loaded + a.reserve);
            let mut world = self.world_node();
            let t = pickup.get_global_transform();
            super::gibs::drop_weapon(&mut world, &old, old_ammo, model, t, pickup.get_linear_velocity(), 120.0, self.pickup_radius);
        }
        self.gun = Some(new_id.clone());
        self.held = Held::Gun;
        self.reload_left = 0.0;
        let a = Ammo { loaded: rounds.min(new_w.magazine), reserve: rounds.saturating_sub(new_w.magazine).min(new_w.max_reserve) };
        self.ammo.insert(new_id, a);
        pickup.queue_free();
        true
    }

    fn hitstop(&mut self, seconds: f32) {
        let mut engine = Engine::singleton();
        engine.set_time_scale(self.data.hit_stop_time_scale as f64);
        self.hitstop_until_us = Time::singleton().get_ticks_usec() + (seconds * 1_000_000.0) as u64;
    }

    fn end_hitstop(&mut self) {
        if self.hitstop_until_us > 0 && Time::singleton().get_ticks_usec() >= self.hitstop_until_us {
            self.hitstop_until_us = 0;
            Engine::singleton().set_time_scale(1.0);
        }
    }

    fn spawn_beam(&mut self, from: Vector3, to: Vector3, w: &WeaponData) {
        let len = from.distance_to(to);
        if len < 0.05 || w.beam_life_s <= 0.0 {
            return;
        }
        let mut mesh = CylinderMesh::new_gd();
        mesh.set_top_radius(w.beam_width);
        mesh.set_bottom_radius(w.beam_width);
        mesh.set_height(len);
        mesh.set_radial_segments(6);
        mesh.set_rings(1);
        let mut mat = StandardMaterial3D::new_gd();
        mat.set_shading_mode(ShadingMode::UNSHADED);
        mat.set_transparency(Transparency::ALPHA);
        mat.set_blend_mode(BlendMode::ADD);
        let c = w.beam_color;
        mat.set_albedo(Color::from_rgba(c.0, c.1, c.2, 0.9));
        mesh.set_material(&mat);
        let mut node = MeshInstance3D::new_alloc();
        node.set_mesh(&mesh);
        node.set_cast_shadows_setting(godot::classes::geometry_instance_3d::ShadowCastingSetting::OFF);
        let mut world = self.world_node();
        world.add_child(&node);
        let dir = (to - from) / len;
        let side = if dir.y.abs() < 0.9 { Vector3::UP } else { Vector3::RIGHT };
        let x = dir.cross(side).normalized();
        let z = x.cross(dir).normalized();
        node.set_global_transform(Transform3D::new(Basis::from_cols(x, dir, z), (from + to) * 0.5));
        node.add_to_group(super::super::orbits::system::FLOATING_GROUP);
        self.beams.push(Beam { node, life: w.beam_life_s, total: w.beam_life_s });
    }

    fn update_beams(&mut self, dt: f32) {
        self.beams.retain_mut(|b| {
            b.life -= dt;
            if b.life <= 0.0 {
                b.node.queue_free();
                return false;
            }
            let k = (b.life / b.total).clamp(0.0, 1.0);
            let s = b.node.get_scale();
            b.node.set_scale(Vector3::new(k, s.y, k));
            true
        });
    }

    /// First-person model: swap meshes when the weapon changes, ease in
    /// while aiming, bob while walking, kick back on firing, swing the bat.
    fn animate(&mut self, dt: f32, player: &Gd<PlayerController>, w: &WeaponData) {
        if self.view_id != w.id {
            if let Some(mut old) = self.view.take() {
                old.queue_free();
            }
            if let (Some(root), Some(model)) = (self.view_root.as_mut(), super::meshes::weapon_model(&w.id)) {
                root.add_child(&model);
                set_view_layer(&model.clone().upcast());
                self.view = Some(model);
            }
            self.view_id = w.id.clone();
        }
        let speed = player.get_velocity().length().min(6.0);
        self.bob_s += dt * (2.0 + speed * 1.4);
        let bob = if player.is_on_floor() { speed / 6.0 } else { 0.0 };
        self.kick = (self.kick - self.kick * 10.0 * dt).max(0.0);
        self.swing_t = (self.swing_t - dt * 3.5).max(0.0);
        let vd = &self.data.view;
        let hip = Vector3::new(vd.hip.0, vd.hip.1, vd.hip.2);
        let ads = Vector3::new(vd.aim.0, vd.aim.1, vd.aim.2);
        let view_scale = vd.scale;
        let mut pos = hip.lerp(ads, self.aim_t);
        pos += Vector3::new((self.bob_s).sin() * 0.012, (self.bob_s * 2.0).sin().abs() * -0.01, 0.0) * bob * (1.0 - self.aim_t * 0.8);
        pos.z += self.kick * 0.012;
        let reload_dip = if self.reload_left > 0.0 { 1.0 - (self.get_reload_fraction() - 0.5).abs() * 2.0 } else { 0.0 };
        pos.y -= reload_dip * 0.12;
        let mut basis = Basis::from_euler(EulerOrder::YXZ, Vector3::new((self.kick * 1.2 + reload_dip * 35.0).to_radians(), 0.0, (reload_dip * 20.0).to_radians()));
        if w.kind == WeaponKind::Thrown {
            pos = Vector3::new(vd.thrown.0, vd.thrown.1, vd.thrown.2);
        }
        if self.swing_t > 0.0 {
            // A quick swipe across the view (the gun or bat sweeps left).
            let t = 1.0 - self.swing_t;
            let a = (t * std::f32::consts::PI).sin();
            pos += Vector3::new(-0.25 * a, 0.05 * a, -0.1 * a);
            basis = Basis::from_euler(EulerOrder::YXZ, Vector3::new(-0.3 * a, 0.9 * a, 0.4 * a)) * basis;
        }
        if let Some(v) = &mut self.view {
            v.set_transform(Transform3D::new(basis.scaled(Vector3::splat(view_scale)), pos));
        }
        if let Some(c) = &mut self.camera {
            let r = c.get_rotation();
            c.set_rotation(Vector3::new(self.kick.to_radians() * 0.6, r.y, r.z));
        }
    }
}

/// Visual layer 20 holds only the held weapon; the fill light lights only it.
const VIEW_LAYER: u32 = 1 << 19;

fn set_view_layer(node: &Gd<Node>) {
    if let Ok(mut g) = node.clone().try_cast::<godot::classes::GeometryInstance3D>() {
        g.set_layer_mask(VIEW_LAYER | 1);
        g.set_cast_shadows_setting(godot::classes::geometry_instance_3d::ShadowCastingSetting::OFF);
    }
    for c in node.get_children().iter_shared() {
        set_view_layer(&c);
    }
}

trait MaxBy {
    fn max_by(self, other: Self) -> Self;
}

impl MaxBy for HitKind {
    fn max_by(self, other: Self) -> Self {
        let rank = |k: HitKind| match k {
            HitKind::Hit => 0,
            HitKind::Gib => 1,
            HitKind::Kill => 2,
        };
        if rank(other) > rank(self) { other } else { self }
    }
}

#[allow(dead_code)]
fn enemies_in_tree(tree: &Gd<godot::classes::SceneTree>) -> usize {
    tree.get_nodes_in_group(ENEMY_GROUP).len()
}
