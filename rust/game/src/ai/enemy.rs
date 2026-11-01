//! A faction enemy (CLAUDE.md sections 7 and 17.1): a suited soldier built
//! from an imported suit model cut into limbs, repainted in its faction's
//! colours, walking on any body under that body's gravity. It senses the
//! player (sight cone, line of sight, gunshots), thinks with `brain`,
//! shows its state as a tell over its head, shoots with visible beams and
//! falls apart under the `combat::limbs` rules: limbs fly off as gibs, a
//! hit gun arm drops the gun, a lost leg drops it to a crawl, death turns
//! it into a jointed ragdoll.
//!
//! Enemies live as children of the body they stand on, so they ride its
//! orbit and spin for free. Far from the player (or before the ground has
//! streamed in) they stand still: section 7's "distant AI runs simplified
//! logic".
//!
//! Serves pillar 2 (goofy but deadly) and pillar 4 (they obey the same
//! physics), fun rules 8 (fair failure: a tell before every attack, every
//! shot visible) and 11 (emergent moments).

use std::cell::RefCell;
use std::collections::HashMap;

use godot::classes::base_material_3d::{BlendMode, ShadingMode, Transparency};
use godot::classes::label_3d::AlphaCutMode;
use godot::classes::mesh::ArrayType;
use godot::classes::{
    Area3D, ArrayMesh, BaseMaterial3D, CapsuleShape3D, CharacterBody3D, CollisionShape3D, ConeTwistJoint3D, CylinderMesh, FontFile,
    ICharacterBody3D, Label3D, Mesh, MeshInstance3D, Node, Node3D, PackedScene, PhysicsRayQueryParameters3D, Shader, ShaderMaterial,
    StandardMaterial3D, Texture2D,
};
use godot::prelude::*;

use super::brain::{AiData, AiState, Brain, Intent, Senses};
use crate::combat::gibs::{GIB_GROUP, PieceSpec, RAGDOLL_GROUP, drop_weapon, enforce_cap, spawn_piece, stump_burst};
use crate::combat::limbs::{HitEvent, Limb, LimbRules, LimbState};
use crate::combat::weapons::{WeaponData, WeaponsData, pellet_directions};
use crate::combat::{BodyModel, ENEMY_LAYER, EnemyData, FactionData, FightStyle, GibData, HITBOX_LAYER, PROP_LAYER};
use crate::orbits::CelestialBody;
use crate::player::PlayerController;
use crate::player::controller::PLAYER_GROUP;
use crate::seed::SeededRng;

pub const ENEMY_GROUP: &str = "enemies";
const SUIT_SHADER: &str = "res://shaders/faction_suit.gdshader";
const TELL_FONT: &str = "res://assets/fonts/LilitaOne-Regular.ttf";
/// Planets and props (1) and ship hulls (4) block sight and shots.
const WORLD_MASK: u32 = 1 | 4;
const PLAYER_LAYER: u32 = 8;

thread_local! {
    /// Limb meshes per body model, cut once and shared by every enemy.
    static CUT_CACHE: RefCell<HashMap<String, CutBody>> = RefCell::new(HashMap::new());
}

/// Drop the shared limb meshes so their GPU resources are freed before
/// the engine checks for leaks at exit.
pub fn clear_cut_cache() {
    CUT_CACHE.with(|c| c.borrow_mut().clear());
}

#[derive(Clone)]
struct CutBody {
    /// Index by `Limb::index()`.
    meshes: Vec<Option<Gd<ArrayMesh>>>,
    texture: Option<Gd<Texture2D>>,
}

fn v3(t: (f32, f32, f32)) -> Vector3 {
    Vector3::new(t.0, t.1, t.2)
}

fn rgb(t: (f32, f32, f32)) -> Color {
    Color::from_rgb(t.0, t.1, t.2)
}

/// Distance from `p` to the segment a-b.
fn segment_distance(p: Vector3, a: Vector3, b: Vector3) -> f32 {
    let ab = b - a;
    let t = ((p - a).dot(ab) / ab.length_squared().max(1e-6)).clamp(0.0, 1.0);
    (a + ab * t).distance_to(p)
}

/// Which limb a point (model space) belongs to: the first capsule that
/// holds it, else the torso.
pub fn limb_at(model: &BodyModel, p: Vector3) -> Limb {
    for l in &model.limbs {
        if segment_distance(p, v3(l.a), v3(l.b)) <= l.radius {
            return l.limb;
        }
    }
    Limb::Torso
}

/// Cut the model's single mesh into one compact mesh per limb.
fn cut_body(model: &BodyModel) -> Option<CutBody> {
    if let Some(c) = CUT_CACHE.with(|c| c.borrow().get(&model.id).cloned()) {
        return Some(c);
    }
    let scene = try_load::<PackedScene>(&model.model).ok()?;
    let root = scene.instantiate()?;
    let mi = root.find_children_ex("*").type_("MeshInstance3D").recursive(true).owned(false).done().iter_shared().next()?.try_cast::<MeshInstance3D>().ok()?;
    let mesh = mi.get_mesh()?.try_cast::<ArrayMesh>().ok()?;
    let to_model = mi.get_transform();
    let texture = mesh
        .surface_get_material(0)
        .and_then(|m| m.try_cast::<BaseMaterial3D>().ok())
        .and_then(|m| m.get_texture(godot::classes::base_material_3d::TextureParam::ALBEDO));
    let mut meshes: Vec<Option<Gd<ArrayMesh>>> = vec![None; 6];
    let mut parts: Vec<(Vec<Vector3>, Vec<Vector3>, Vec<Vector2>, Vec<i32>, HashMap<i32, i32>)> = (0..6).map(|_| (Vec::new(), Vec::new(), Vec::new(), Vec::new(), HashMap::new())).collect();
    for s in 0..mesh.get_surface_count() {
        let arrays = mesh.surface_get_arrays(s);
        let verts: PackedVector3Array = arrays.at(ArrayType::VERTEX.ord() as usize).to();
        let normals: PackedVector3Array = arrays.at(ArrayType::NORMAL.ord() as usize).try_to().unwrap_or_default();
        let uvs: PackedVector2Array = arrays.at(ArrayType::TEX_UV.ord() as usize).try_to().unwrap_or_default();
        let index_v = arrays.at(ArrayType::INDEX.ord() as usize);
        let index: PackedInt32Array = if index_v.is_nil() { (0..verts.len() as i32).collect() } else { index_v.to() };
        let (v, n, uv, idx) = (verts.as_slice(), normals.as_slice(), uvs.as_slice(), index.as_slice());
        for tri in idx.chunks_exact(3) {
            let c = to_model * ((v[tri[0] as usize] + v[tri[1] as usize] + v[tri[2] as usize]) / 3.0);
            let li = limb_at(model, c).index();
            let part = &mut parts[li];
            for &k in tri {
                let next = part.0.len() as i32;
                let new = *part.4.entry(k).or_insert_with(|| {
                    part.0.push(to_model * v[k as usize]);
                    part.1.push(n.get(k as usize).map_or(Vector3::UP, |nn| (to_model.basis * *nn).normalized()));
                    part.2.push(uv.get(k as usize).copied().unwrap_or(Vector2::ZERO));
                    next
                });
                part.3.push(new);
            }
        }
    }
    for (i, (p, n, uv, idx, _)) in parts.into_iter().enumerate() {
        if idx.is_empty() {
            continue;
        }
        let mut arrays = VarArray::new();
        arrays.resize(ArrayType::MAX.ord() as usize, &Variant::nil());
        arrays.set(ArrayType::VERTEX.ord() as usize, &p.into_iter().collect::<PackedVector3Array>().to_variant());
        arrays.set(ArrayType::NORMAL.ord() as usize, &n.into_iter().collect::<PackedVector3Array>().to_variant());
        arrays.set(ArrayType::TEX_UV.ord() as usize, &uv.into_iter().collect::<PackedVector2Array>().to_variant());
        arrays.set(ArrayType::INDEX.ord() as usize, &idx.into_iter().collect::<PackedInt32Array>().to_variant());
        let mut m = ArrayMesh::new_gd();
        m.add_surface_from_arrays(godot::classes::mesh::PrimitiveType::TRIANGLES, &arrays);
        meshes[i] = Some(m);
    }
    root.free();
    let cut = CutBody { meshes, texture };
    CUT_CACHE.with(|c| c.borrow_mut().insert(model.id.clone(), cut.clone()));
    Some(cut)
}

/// One limb's nodes.
struct LimbNodes {
    pivot: Gd<Node3D>,
    mesh: Option<Gd<MeshInstance3D>>,
    hitbox: Option<Gd<Area3D>>,
    /// Pivot position in model space.
    at: Vector3,
    radius: f32,
    length: f32,
    flash: f32,
}

#[derive(GodotClass)]
#[class(base = CharacterBody3D)]
pub struct Enemy {
    /// Which entry of enemies.ron this is.
    #[export]
    enemy_id: GString,
    data: Option<EnemyData>,
    weapon: Option<WeaponData>,
    model: Option<BodyModel>,
    faction: Option<FactionData>,
    rules: Option<LimbRules>,
    ai: Option<AiData>,
    gibs: Option<GibData>,
    brain: Brain,
    limbs: LimbState,
    nodes: Vec<Option<LimbNodes>>,
    root: Option<Gd<Node3D>>,
    gun: Option<Gd<Node3D>>,
    tell: Option<Gd<Label3D>>,
    rng: SeededRng,
    /// Home and current goal, in the parent body's local space.
    home: Vector3,
    goal: Option<Vector3>,
    pause_s: f32,
    burst_left: u32,
    shot_timer: f32,
    punch_timer: f32,
    walk_phase: f32,
    flank_side: f32,
    flank_timer: f32,
    look_dir: Vector3,
    aim_pitch: f32,
    // What the senses picked up since the last tick.
    heard: Option<Vector3>,
    hurt_from: Option<Vector3>,
    shout: Option<Vector3>,
    last_hit: Option<(usize, Vector3, f32)>,
    state_was: AiState,
    shots_fired: i64,
    hits_on_player: i64,
    debug_freeze: bool,
    /// Roped (section 9): no thinking, no shooting; can be carried.
    tied: bool,
    carried: bool,
    base: Base<CharacterBody3D>,
}

#[godot_api]
impl ICharacterBody3D for Enemy {
    fn init(base: Base<CharacterBody3D>) -> Self {
        let rules = crate::combat::load_enemies().limbs;
        Self {
            enemy_id: GString::from("venera_t1"),
            data: None,
            weapon: None,
            model: None,
            faction: None,
            limbs: LimbState::new(100.0, &rules),
            rules: Some(rules),
            ai: None,
            gibs: None,
            brain: Brain::default(),
            nodes: Vec::new(),
            root: None,
            gun: None,
            tell: None,
            rng: SeededRng::from_u64(1),
            home: Vector3::ZERO,
            goal: None,
            pause_s: 0.0,
            burst_left: 0,
            shot_timer: 0.0,
            punch_timer: 0.0,
            walk_phase: 0.0,
            flank_side: 1.0,
            flank_timer: 0.0,
            look_dir: Vector3::ZERO,
            aim_pitch: 0.0,
            heard: None,
            hurt_from: None,
            shout: None,
            last_hit: None,
            state_was: AiState::Patrol,
            shots_fired: 0,
            hits_on_player: 0,
            debug_freeze: false,
            tied: false,
            carried: false,
            base,
        }
    }

    fn ready(&mut self) {
        let all = crate::combat::load_enemies();
        let weapons: WeaponsData = crate::combat::weapons::load_data();
        let id = self.enemy_id.to_string();
        let Some(data) = all.enemy(&id).cloned() else {
            godot_error!("Enemy: unknown enemy id {id}");
            return;
        };
        let model = all.body(&data.body).cloned();
        self.faction = all.faction(&data.faction).cloned();
        self.weapon = weapons.get(&data.weapon).cloned();
        self.limbs = LimbState::new(data.health, &all.limbs);
        self.rules = Some(all.limbs.clone());
        self.ai = Some(all.ai.clone());
        self.gibs = Some(all.gibs.clone());
        let iid = self.base().instance_id().to_i64() as u64;
        self.rng = SeededRng::derive_indexed(crate::seed::safe_run_seed(), "enemy", iid);
        self.flank_side = if self.rng.chance(0.5) { 1.0 } else { -1.0 };
        self.home = self.base().get_position();
        {
            let mut b = self.base_mut();
            b.add_to_group(ENEMY_GROUP);
            b.set_collision_layer(ENEMY_LAYER);
            b.set_collision_mask(1 | 4 | ENEMY_LAYER);
            b.set_floor_max_angle(50f32.to_radians());
            b.set_floor_snap_length(0.4);
        }
        let mut capsule = CapsuleShape3D::new_gd();
        capsule.set_radius(0.38 * data.scale);
        capsule.set_height(1.85 * data.scale);
        let mut col = CollisionShape3D::new_alloc();
        col.set_shape(&capsule);
        col.set_position(Vector3::new(0.0, 0.925 * data.scale, 0.0));
        self.base_mut().add_child(&col);
        if let Some(m) = &model {
            self.build_body(m, &data);
        }
        self.model = model;
        self.data = Some(data);
        self.add_tell();
    }

    fn physics_process(&mut self, delta: f64) {
        let dt = delta as f32;
        let (Some(data), Some(ai)) = (self.data.clone(), self.ai.clone()) else { return };
        if self.limbs.dead {
            return;
        }
        self.fade_flashes(dt);
        let Some(body) = self.body() else { return };
        let player = self.player();
        let here = self.base().get_global_position();
        let near = player.as_ref().is_some_and(|p| p.get_global_position().distance_to(here) < ai.simple_beyond_m);
        // Distant or not yet on solid ground: stand still (simplified logic).
        if !near || !body.bind().has_detail() || self.debug_freeze || self.carried {
            return;
        }
        let g = body.bind().gravity_at(here);
        let up = if g.length_squared() > 1e-6 { -g.normalized() } else { (here - body.get_global_position()).normalized() };
        self.base_mut().set_up_direction(up);

        if self.tied {
            // Bound hand and foot: just falls (or drifts, off the ground).
            self.move_body(Vector3::ZERO, g, up, dt);
            self.animate(dt, &data, up);
            return;
        }
        let senses = self.sense(&ai, &data, player.as_ref(), up);
        // The brain thinks in the body's own frame (positions stay put while
        // the body orbits and the floating origin shifts); act in world space.
        let intent = self.brain.tick(&ai, &senses, dt);
        let bt = body.get_global_transform();
        let intent = match intent {
            Intent::Look(p) => Intent::Look(bt * p),
            Intent::Investigate(p) => Intent::Investigate(bt * p),
            Intent::Fight { target, may_shoot } => Intent::Fight { target: bt * target, may_shoot },
            other => other,
        };
        if self.brain.state != self.state_was {
            if self.brain.state == AiState::Alert {
                self.shout_to_friends(&ai);
                self.shot_timer = ai.first_shot_delay_s;
            }
            self.state_was = self.brain.state;
        }
        self.update_tell();
        let wish = self.act(intent, &data, &ai, player.as_ref(), up, dt);
        self.move_body(wish, g, up, dt);
        self.animate(dt, &data, up);
    }
}

#[godot_api]
impl Enemy {
    #[signal]
    fn died(tier: i64);

    #[func]
    pub fn get_state_name(&self) -> GString {
        GString::from(if self.limbs.dead { "Dead" } else { self.brain.state.name() })
    }

    #[func]
    pub fn get_health_fraction(&self) -> f32 {
        self.limbs.health_fraction()
    }

    #[func]
    pub fn is_armed(&self) -> bool {
        self.limbs.armed
    }

    #[func]
    pub fn is_down(&self) -> bool {
        self.limbs.down
    }

    #[func]
    pub fn is_dead(&self) -> bool {
        self.limbs.dead
    }

    #[func]
    pub fn has_limb(&self, limb: i64) -> bool {
        self.limbs.is_attached(Limb::from_index(limb as usize))
    }

    #[func]
    pub fn get_tier(&self) -> i64 {
        self.data.as_ref().map_or(0, |d| d.tier as i64)
    }

    #[func]
    pub fn get_shots_fired(&self) -> i64 {
        self.shots_fired
    }

    #[func]
    pub fn get_hits_on_player(&self) -> i64 {
        self.hits_on_player
    }

    #[func]
    pub fn get_tell(&self) -> GString {
        self.tell.as_ref().filter(|t| t.is_visible()).map_or(GString::new(), |t| t.get_text())
    }

    /// World position of a limb's hitbox centre (tests aim here).
    #[func]
    pub fn get_limb_position(&self, limb: i64) -> Vector3 {
        self.nodes
            .get(limb as usize)
            .and_then(|n| n.as_ref())
            .and_then(|n| n.hitbox.as_ref())
            .map_or(self.base().get_global_position(), |h| h.get_global_position())
    }

    /// Tests: hit a limb directly.
    #[func]
    pub fn debug_hit(&mut self, limb: i64, damage: f32) -> i64 {
        let from = self.base().get_global_position() - self.base().get_global_basis().col_c() * 5.0;
        let dir = (self.base().get_global_position() - from).normalized();
        self.take_hit(limb as usize, damage, dir, 4.0, from, true)
    }

    #[func]
    pub fn is_tied(&self) -> bool {
        self.tied
    }

    #[func]
    pub fn is_carried(&self) -> bool {
        self.carried
    }

    /// The rope gun hits: tie this soldier up. Any gun drops. Returns false
    /// if already tied or dead.
    #[func]
    pub fn tie_up(&mut self, from: Vector3) -> bool {
        if self.tied || self.limbs.dead {
            return false;
        }
        self.tied = true;
        let body = GString::from(&self.body_id());
        crate::points::singleton::with_points(|p| p.on_captured(body.clone()));
        self.report_camp_member_out();
        let here = self.base().get_global_position();
        if self.limbs.armed {
            self.limbs.armed = false;
            self.drop_gun((here - from).normalized_or_zero());
        }
        self.look_dir = Vector3::ZERO;
        if let Some(t) = &mut self.tell {
            t.set_visible(false);
        }
        let rope = crate::combat::weapons::load_data().rope;
        let mut coils = crate::combat::meshes::rope_coils(rope.color, rope.coil_thickness, &rope.coils);
        let scale = self.data.as_ref().map_or(1.0, |d| d.scale);
        coils.set_scale(Vector3::splat(scale));
        self.base_mut().add_child(&coils);
        true
    }

    /// Picked up (or put down) by the player. Carried soldiers don't
    /// collide, so they never shove the player around.
    #[func]
    pub fn set_carried(&mut self, on: bool) {
        if !self.tied {
            return;
        }
        self.carried = on;
        let mut b = self.base_mut();
        if on {
            b.set_collision_layer(0);
            b.set_collision_mask(0);
        } else {
            b.set_collision_layer(ENEMY_LAYER);
            b.set_collision_mask(1 | 4 | ENEMY_LAYER);
        }
    }

    /// Tests: stop thinking and moving (still takes hits).
    #[func]
    pub fn debug_set_frozen(&mut self, on: bool) {
        self.debug_freeze = on;
    }
}

impl Enemy {
    /// A tracker ping (hunter swarms): know where the player is right now
    /// and go after them at once. Ignored once dead or tied up.
    pub fn hunt(&mut self, world: Vector3) {
        if self.limbs.dead || self.tied {
            return;
        }
        let local = self.to_local_of_body(world);
        self.shout = Some(local);
    }

    pub fn is_out(&self) -> bool {
        self.limbs.dead || self.tied
    }

    /// Which entry of enemies.ron to be (before entering the tree).
    pub fn set_id(&mut self, id: &str) {
        self.enemy_id = GString::from(id);
    }

    fn body_id(&self) -> String {
        self.body().map_or_else(String::new, |b| b.get_name().to_string())
    }

    /// This soldier is out (dead or tied): if nobody else in its camp is
    /// still fighting, the camp is cleared.
    fn report_camp_member_out(&self) {
        let Ok(key) = self.base().get_meta("camp").try_to::<GString>() else { return };
        let me = self.base().instance_id();
        let standing = self
            .base()
            .get_tree()
            .get_nodes_in_group(&key.to_string())
            .iter_shared()
            .filter(|n| n.instance_id() != me && !n.is_queued_for_deletion())
            .filter_map(|n| n.try_cast::<Enemy>().ok())
            .filter(|e| {
                let b = e.bind();
                !b.limbs.dead && !b.tied
            })
            .count();
        if standing == 0 {
            let body = GString::from(&self.body_id());
            crate::points::singleton::with_points(|p| p.on_camp_cleared(key.clone(), body.clone()));
        }
    }

    fn body(&self) -> Option<Gd<CelestialBody>> {
        self.base().get_parent().and_then(|p| p.try_cast::<CelestialBody>().ok())
    }

    fn player(&self) -> Option<Gd<PlayerController>> {
        self.base().get_tree().get_first_node_in_group(PLAYER_GROUP).and_then(|n| n.try_cast::<PlayerController>().ok())
    }

    fn world_node(&self) -> Gd<Node> {
        let tree = self.base().get_tree();
        tree.get_current_scene().unwrap_or_else(|| tree.get_root().upcast())
    }

    fn suit_material(&self, texture: &Option<Gd<Texture2D>>) -> Gd<ShaderMaterial> {
        let mut m = ShaderMaterial::new_gd();
        if let Ok(shader) = try_load::<Shader>(SUIT_SHADER) {
            m.set_shader(&shader);
        }
        if let Some(t) = texture {
            m.set_shader_parameter("albedo_texture", &t.to_variant());
        }
        if let Some(f) = &self.faction {
            m.set_shader_parameter("main_color", &rgb(f.main).to_variant());
            m.set_shader_parameter("trim_color", &rgb(f.trim).to_variant());
            m.set_shader_parameter("dark_color", &rgb(f.dark).to_variant());
            m.set_shader_parameter("visor_color", &rgb(f.visor).to_variant());
        }
        m
    }

    /// Build the limb hierarchy: Model (scaled, facing -Z) -> Torso (hips)
    /// -> Head, arms; legs hang off the Model. Each limb has its own mesh,
    /// material (for hit flash and fading) and hitbox.
    fn build_body(&mut self, model: &BodyModel, data: &EnemyData) {
        let Some(cut) = cut_body(model) else {
            godot_error!("Enemy: could not cut body model {}", model.model);
            return;
        };
        let mut root = Node3D::new_alloc();
        root.set_name("Model");
        // The suit faces +Z; Godot's forward is -Z.
        root.set_transform(Transform3D::new(Basis::from_axis_angle(Vector3::UP, std::f32::consts::PI).scaled(Vector3::splat(data.scale)), Vector3::ZERO));
        self.base_mut().add_child(&root);
        let hips = Vector3::new(0.0, 0.86, 0.03);
        let mut nodes: Vec<Option<LimbNodes>> = (0..6).map(|_| None).collect();
        let shapes: HashMap<usize, (Vector3, Vector3, Vector3, f32)> = model.limbs.iter().map(|l| (l.limb.index(), (v3(l.pivot), v3(l.a), v3(l.b), l.radius))).collect();
        let order = [Limb::Torso, Limb::Head, Limb::ArmL, Limb::ArmR, Limb::LegL, Limb::LegR];
        for limb in order {
            let i = limb.index();
            let (pivot_at, a, b, radius) = match limb {
                Limb::Torso => (hips, hips, Vector3::new(0.0, 1.45, 0.0), 0.34),
                _ => match shapes.get(&i) {
                    Some(s) => *s,
                    None => continue,
                },
            };
            let mut pivot = Node3D::new_alloc();
            pivot.set_name(&format!("Limb{:?}", limb));
            // Head and arms hang off the torso; legs off the model root.
            let parent_at = if matches!(limb, Limb::Head | Limb::ArmL | Limb::ArmR) { hips } else { Vector3::ZERO };
            pivot.set_position(pivot_at - parent_at);
            match (&limb, nodes[Limb::Torso.index()].as_mut()) {
                (Limb::Head | Limb::ArmL | Limb::ArmR, Some(torso)) => torso.pivot.add_child(&pivot),
                _ => root.add_child(&pivot),
            }
            let mut mesh_node = None;
            if let Some(m) = &cut.meshes[i] {
                let mut mi = MeshInstance3D::new_alloc();
                mi.set_name("Mesh");
                mi.set_mesh(m);
                mi.set_position(-pivot_at);
                mi.set_surface_override_material(0, &self.suit_material(&cut.texture));
                pivot.add_child(&mi);
                mesh_node = Some(mi);
            }
            // Hitbox: a capsule along a-b, in pivot space.
            let mut area = Area3D::new_alloc();
            area.set_name("Hitbox");
            area.set_collision_layer(HITBOX_LAYER);
            area.set_collision_mask(0);
            area.set_monitoring(false);
            area.set_meta("limb", &(i as i64).to_variant());
            area.set_meta("enemy", &self.to_gd().to_variant());
            let len = a.distance_to(b);
            let mut shape = CapsuleShape3D::new_gd();
            shape.set_radius(radius);
            shape.set_height((len + radius * 2.0).max(radius * 2.0 + 0.01));
            let mut col = CollisionShape3D::new_alloc();
            col.set_shape(&shape);
            let dir = (b - a).normalized_or_zero();
            let basis = if dir.length_squared() > 0.0 { rotation_from_up(dir) } else { Basis::IDENTITY };
            col.set_transform(Transform3D::new(basis, (a + b) * 0.5 - pivot_at));
            area.add_child(&col);
            pivot.add_child(&area);
            nodes[i] = Some(LimbNodes { pivot, mesh: mesh_node, hitbox: Some(area), at: pivot_at, radius, length: len + radius * 2.0, flash: 0.0 });
        }
        // The gun, held in the hands (on the gun arm so it follows the aim).
        if let (Some(w), Some(arm)) = (&self.weapon, nodes[Limb::ArmR.index()].as_mut())
            && let Some(mut gun) = crate::combat::meshes::weapon_model(&w.id)
        {
            gun.set_name("Gun");
            let grip = v3(model.grip);
            // Gun models point -Z; the suit faces +Z.
            gun.set_transform(Transform3D::new(Basis::from_axis_angle(Vector3::UP, std::f32::consts::PI), grip - arm.at + Vector3::new(0.27, 0.0, -0.25)));
            arm.pivot.add_child(&gun);
            self.gun = Some(gun);
        }
        self.nodes = nodes;
        self.root = Some(root);
    }

    fn add_tell(&mut self) {
        let mut label = Label3D::new_alloc();
        label.set_name("Tell");
        label.set_billboard_mode(godot::classes::base_material_3d::BillboardMode::ENABLED);
        label.set_draw_flag(godot::classes::label_3d::DrawFlags::FIXED_SIZE, true);
        label.set_draw_flag(godot::classes::label_3d::DrawFlags::DISABLE_DEPTH_TEST, true);
        label.set_alpha_cut_mode(AlphaCutMode::DISABLED);
        label.set_pixel_size(0.0016);
        label.set_font_size(48);
        label.set_outline_size(14);
        label.set_outline_modulate(Color::from_rgba(0.05, 0.05, 0.08, 0.9));
        if let Ok(f) = try_load::<FontFile>(TELL_FONT) {
            label.set_font(&f);
        }
        let scale = self.data.as_ref().map_or(1.0, |d| d.scale);
        label.set_position(Vector3::new(0.0, 2.25 * scale, 0.0));
        label.set_visible(false);
        self.base_mut().add_child(&label);
        self.tell = Some(label);
    }

    /// Section 7: each state has a clear visual tell. Suspicious and Search
    /// show a yellow "?", Alert a red-orange "!" (hostile colour, section 10).
    fn update_tell(&mut self) {
        let Some(t) = &mut self.tell else { return };
        let (text, color) = match self.brain.state {
            AiState::Patrol => ("", Color::WHITE),
            AiState::Suspicious => ("?", Color::from_rgb(1.0, 0.86, 0.3)),
            AiState::Search => ("?", Color::from_rgb(1.0, 0.62, 0.25)),
            AiState::Alert => ("!", Color::from_rgb(1.0, 0.36, 0.18)),
            AiState::Return => ("", Color::WHITE),
        };
        t.set_visible(!text.is_empty());
        t.set_text(text);
        t.set_modulate(color);
    }

    fn eye(&self) -> Vector3 {
        let scale = self.data.as_ref().map_or(1.0, |d| d.scale);
        let e = self.model.as_ref().map_or(Vector3::new(0.0, 1.7, 0.0), |m| v3(m.eye));
        let t = self.base().get_global_transform();
        // Model space is turned 180 degrees about Y.
        t * (Vector3::new(-e.x, e.y, -e.z) * scale)
    }

    fn ray_clear(&self, from: Vector3, to: Vector3) -> bool {
        let Some(world) = self.base().get_world_3d() else { return false };
        let Some(mut space) = world.get_direct_space_state() else { return false };
        let Some(mut q) = PhysicsRayQueryParameters3D::create_ex(from, to).collision_mask(WORLD_MASK).done() else { return false };
        let ex: Array<Rid> = [self.base().get_rid()].into_iter().collect();
        q.set_exclude(&ex);
        space.intersect_ray(&q).is_empty()
    }

    fn sense(&mut self, ai: &AiData, data: &EnemyData, player: Option<&Gd<PlayerController>>, up: Vector3) -> Senses {
        let mut s = Senses { heard: self.heard.take(), hurt_from: self.hurt_from.take(), shout: self.shout.take(), ..Default::default() };
        let here = self.base().get_global_position();
        if let Some(goal) = self.goal_world() {
            let flat = goal - here;
            s.arrived = (flat - up * flat.dot(up)).length() < 1.5;
        }
        let Some(p) = player else { return s };
        let pb = p.bind();
        if pb.is_dead() {
            return s;
        }
        let head = pb.head_position();
        s.player_pos = self.to_local_of_body(p.get_global_position());
        s.player_crouched = pb.is_crouched();
        // Stealth tag (spot_time) and Ghost (crouch_spot_time) slow noticing.
        s.notice_mult = crate::points::singleton::stat("spot_time") * if s.player_crouched { crate::points::singleton::stat("crouch_spot_time") } else { 1.0 };
        drop(pb);
        let eye = self.eye();
        let to = head - eye;
        let dist = to.length();
        if dist > ai.sight_m {
            return s;
        }
        let forward = -self.base().get_global_basis().col_c();
        let angle = forward.angle_to(to);
        // Very close, you are noticed whichever way they face (they hear you).
        let in_view = angle.to_degrees() <= ai.fov_deg || dist < 2.5;
        if in_view && self.ray_clear(eye, head) {
            s.sees_player = true;
        }
        let _ = data;
        s
    }

    fn shout_to_friends(&mut self, ai: &AiData) {
        let Some(at) = self.brain.last_known else { return };
        let here = self.base().get_global_position();
        let me = self.base().instance_id();
        for n in self.base().get_tree().get_nodes_in_group(ENEMY_GROUP).iter_shared() {
            if n.instance_id() == me {
                continue;
            }
            let Ok(mut e) = n.try_cast::<Enemy>() else { continue };
            if e.get_global_position().distance_to(here) <= ai.shout_m {
                e.bind_mut().shout = Some(at);
            }
        }
    }

    fn goal_world(&self) -> Option<Vector3> {
        let parent = self.body()?;
        Some(parent.get_global_transform() * self.goal?)
    }

    fn to_local_of_body(&self, world: Vector3) -> Vector3 {
        self.body().map_or(world, |b| b.get_global_transform().affine_inverse() * world)
    }

    /// Decide where to go and what to do; returns the wished-for ground
    /// velocity (world space, along the ground).
    fn act(&mut self, intent: Intent, data: &EnemyData, ai: &AiData, player: Option<&Gd<PlayerController>>, up: Vector3, dt: f32) -> Vector3 {
        let here = self.base().get_global_position();
        let flat = |v: Vector3| v - up * v.dot(up);
        let speed_mult = if self.limbs.down { 0.25 } else { 1.0 };
        match intent {
            Intent::Patrol => {
                if self.pause_s > 0.0 {
                    self.pause_s -= dt;
                    return Vector3::ZERO;
                }
                let goal = match self.goal_world() {
                    Some(g) if flat(g - here).length() > 1.2 => g,
                    _ => {
                        // Arrived (or no goal yet): pause, then pick a new spot near home.
                        if self.goal.is_some() {
                            self.pause_s = self.rng.range_f32(ai.patrol_pause_s.0, ai.patrol_pause_s.1);
                        }
                        let home = self.home;
                        let tangent_a = home.normalized().cross(Vector3::UP).normalized_or_zero();
                        let tangent_a = if tangent_a.length_squared() < 0.5 { Vector3::RIGHT } else { tangent_a };
                        let tangent_b = home.normalized().cross(tangent_a);
                        let a = self.rng.range_f32(0.0, std::f32::consts::TAU);
                        let r = self.rng.range_f32(0.3, 1.0) * ai.patrol_radius_m;
                        self.goal = Some(home + (tangent_a * a.cos() + tangent_b * a.sin()) * r);
                        return Vector3::ZERO;
                    }
                };
                let d = flat(goal - here).normalized_or_zero();
                self.look_dir = d;
                d * data.walk_speed * 0.6 * speed_mult
            }
            Intent::Look(p) => {
                self.look_dir = flat(p - here).normalized_or_zero();
                Vector3::ZERO
            }
            Intent::Investigate(p) => {
                self.goal = Some(self.to_local_of_body(p));
                let to = flat(p - here);
                if to.length() < 1.5 {
                    // Look around the spot.
                    let t = self.brain.in_state_s;
                    let side = to.cross(up).normalized_or_zero();
                    self.look_dir = (self.look_dir + side * (t * 0.8).sin() * dt).normalized_or_zero();
                    return Vector3::ZERO;
                }
                self.look_dir = to.normalized();
                self.look_dir * data.walk_speed * speed_mult
            }
            Intent::GoHome => {
                self.goal = Some(self.home);
                let Some(g) = self.goal_world() else { return Vector3::ZERO };
                let to = flat(g - here);
                if to.length() < 1.2 {
                    return Vector3::ZERO;
                }
                self.look_dir = to.normalized();
                self.look_dir * data.walk_speed * speed_mult
            }
            Intent::Fight { target, may_shoot } => {
                let aim_at = player.map_or(target, |p| if may_shoot { p.bind().head_position() - up * 0.45 } else { target });
                let to = flat(aim_at - here);
                let dist = to.length();
                let dir = to.normalized_or_zero();
                self.look_dir = dir;
                let mut wish = Vector3::ZERO;
                if !self.limbs.armed {
                    // Disarmed: run in and punch (goofy, still dangerous).
                    if dist > 1.4 {
                        wish = dir * data.run_speed;
                    } else {
                        self.punch_timer -= dt;
                        if self.punch_timer <= 0.0 && may_shoot {
                            self.punch_timer = 1.2;
                            if let Some(p) = player {
                                let mut p = p.clone();
                                p.bind_mut().take_hit(data.damage_to_player * 1.5, here);
                                self.hits_on_player += 1;
                            }
                        }
                    }
                    return wish * speed_mult;
                }
                let (near, far) = data.preferred_range_m;
                match data.style {
                    FightStyle::Rush => {
                        if dist > near {
                            wish = dir * data.run_speed;
                        }
                    }
                    FightStyle::Flank => {
                        self.flank_timer -= dt;
                        if self.flank_timer <= 0.0 {
                            self.flank_timer = self.rng.range_f32(2.5, 5.0);
                            if self.rng.chance(0.35) {
                                self.flank_side = -self.flank_side;
                            }
                        }
                        let side = dir.cross(up).normalized_or_zero() * self.flank_side;
                        let radial = if dist > far { 1.0 } else if dist < near { -1.0 } else { 0.0 };
                        wish = (side + dir * radial).normalized_or_zero() * data.run_speed;
                    }
                }
                if may_shoot && self.limbs.armed {
                    self.shoot(data, player, aim_at, dt);
                }
                wish * speed_mult
            }
        }
    }

    fn shoot(&mut self, data: &EnemyData, player: Option<&Gd<PlayerController>>, aim_at: Vector3, dt: f32) {
        let Some(w) = self.weapon.clone() else { return };
        self.shot_timer -= dt;
        if self.shot_timer > 0.0 {
            return;
        }
        if self.burst_left == 0 {
            self.burst_left = self.rng.range_i64(data.burst.0 as i64, data.burst.1 as i64 + 1) as u32;
        }
        let Some(gun) = self.gun.clone() else { return };
        let muzzle = gun.get_global_transform() * crate::combat::meshes::muzzle(&w.id);
        let dist = muzzle.distance_to(aim_at);
        let speed = player.map_or(0.0, |p| p.get_velocity().length());
        let error = data.aim_error_deg + data.aim_error_per_10m * dist / 10.0 + data.aim_error_per_mps * speed;
        let forward = (aim_at - muzzle).normalized_or_zero();
        // Spread the pellets around one aim error per shot.
        let aim = pellet_directions(forward, error, 1, &mut self.rng)[0];
        let dirs = pellet_directions(aim, w.spread_deg, w.pellets, &mut self.rng);
        let mut world = self.world_node();
        for d in dirs {
            let to = muzzle + d * w.range_m;
            let mut end = to;
            if let Some(world3d) = self.base().get_world_3d()
                && let Some(mut space) = world3d.get_direct_space_state()
                && let Some(mut q) = PhysicsRayQueryParameters3D::create_ex(muzzle, to).collision_mask(WORLD_MASK | PLAYER_LAYER | PROP_LAYER).done()
            {
                let ex: Array<Rid> = [self.base().get_rid()].into_iter().collect();
                q.set_exclude(&ex);
                let hit = space.intersect_ray(&q);
                if let Some(p) = hit.get("position") {
                    end = p.to();
                    let collider: Option<Gd<Object>> = hit.get("collider").and_then(|c| c.try_to().ok());
                    if let Some(c) = collider {
                        if let Ok(mut pc) = c.clone().try_cast::<PlayerController>() {
                            pc.bind_mut().take_hit(data.damage_to_player, muzzle);
                            self.hits_on_player += 1;
                        } else if let Ok(mut rb) = c.try_cast::<godot::classes::RigidBody3D>() {
                            let off = end - rb.get_global_position();
                            rb.apply_impulse_ex(d * w.impulse).position(off).done();
                        }
                    }
                }
            }
            spawn_beam(&mut world, muzzle, end, w.beam_color, w.beam_width * 1.4, (w.beam_life_s * 2.0).max(0.08));
        }
        self.shots_fired += 1;
        hear_noise(&self.base().get_tree(), muzzle, w.noise_m * 0.5, Some(self.base().instance_id()));
        self.burst_left = self.burst_left.saturating_sub(1);
        self.shot_timer = if self.burst_left == 0 { self.rng.range_f32(data.burst_pause_s.0, data.burst_pause_s.1) } else { w.fire_interval_s.max(0.12) };
    }

    fn move_body(&mut self, wish: Vector3, g: Vector3, up: Vector3, dt: f32) {
        let mut v = self.base().get_velocity();
        let vertical = up * v.dot(up);
        let horizontal = v - vertical;
        let on_floor = self.base().is_on_floor();
        let accel = if on_floor { 14.0 } else { 2.0 };
        let horizontal = horizontal.move_toward(wish, accel * dt);
        v = horizontal + vertical + g * dt;
        self.base_mut().set_velocity(v);
        self.base_mut().move_and_slide();
        // Face where we look, standing upright on the body.
        let look = if self.look_dir.length_squared() > 1e-4 { self.look_dir } else { -self.base().get_global_basis().col_c() };
        let fwd = (look - up * look.dot(up)).normalized_or_zero();
        if fwd.length_squared() > 0.5 {
            let current = -self.base().get_global_basis().col_c();
            let cur_flat = (current - up * current.dot(up)).normalized_or_zero();
            let blended = cur_flat.slerp(fwd, (dt * 6.0).min(1.0)).normalized_or_zero();
            let f = if blended.length_squared() > 0.5 { blended } else { fwd };
            let x = f.cross(up).normalized();
            let basis = Basis::from_cols(x, up, -f).orthonormalized();
            let p = self.base().get_global_position();
            self.base_mut().set_global_transform(Transform3D::new(basis, p));
        }
    }

    fn animate(&mut self, dt: f32, data: &EnemyData, up: Vector3) {
        let v = self.base().get_velocity();
        let speed = (v - up * v.dot(up)).length();
        self.walk_phase += dt * speed * 2.6;
        let swing = (self.walk_phase).sin() * (speed / data.run_speed.max(0.1)).min(1.0) * 0.6;
        // Aim pitch toward the target.
        let last_known = self.brain.last_known.and_then(|p| self.body().map(|b| b.get_global_transform() * p));
        let target_pitch = match (self.brain.state, last_known) {
            (AiState::Alert, Some(p)) => {
                let to = p - self.eye();
                let vertical = to.dot(up);
                let flat = (to - up * vertical).length();
                (vertical.atan2(flat)).clamp(-0.8, 0.8)
            }
            _ => -0.25,
        };
        self.aim_pitch += (target_pitch - self.aim_pitch) * (dt * 6.0).min(1.0);
        let armed = self.limbs.armed;
        let tied = self.tied;
        for limb in Limb::ALL {
            let Some(n) = self.nodes[limb.index()].as_mut() else { continue };
            let rot = match limb {
                // Roped: arms pinned to the sides, legs together.
                Limb::ArmL if tied => Basis::from_axis_angle(Vector3::RIGHT, 1.25) * Basis::from_axis_angle(Vector3::FORWARD, -0.25),
                Limb::ArmR if tied => Basis::from_axis_angle(Vector3::RIGHT, 1.25) * Basis::from_axis_angle(Vector3::FORWARD, 0.25),
                Limb::LegL | Limb::LegR | Limb::Torso if tied => Basis::IDENTITY,
                Limb::Head if tied => Basis::from_axis_angle(Vector3::RIGHT, 0.2),
                // The model is turned 180 degrees, so +X pitch tips forward.
                Limb::LegL => Basis::from_axis_angle(Vector3::RIGHT, swing),
                Limb::LegR => Basis::from_axis_angle(Vector3::RIGHT, -swing),
                Limb::ArmL | Limb::ArmR if armed => Basis::from_axis_angle(Vector3::RIGHT, -self.aim_pitch),
                Limb::ArmL => Basis::from_axis_angle(Vector3::RIGHT, 0.9 - swing * 0.8),
                Limb::ArmR => Basis::from_axis_angle(Vector3::RIGHT, 0.9 + swing * 0.8),
                Limb::Head => Basis::from_axis_angle(Vector3::RIGHT, -self.aim_pitch * 0.5),
                Limb::Torso => Basis::from_axis_angle(Vector3::RIGHT, (speed / data.run_speed.max(0.1)).min(1.0) * 0.12),
            };
            n.pivot.set_basis(rot);
        }
        // Lost a leg: topple over and crawl.
        if let Some(root) = &mut self.root {
            let s = data.scale;
            let target = if self.limbs.down { 1.35 } else { 0.0 };
            let cur = root.get_meta_ex("down_t").default(&0.0f32.to_variant()).done().try_to::<f32>().unwrap_or(0.0);
            let t = cur + (target - cur) * (dt * 5.0).min(1.0);
            root.set_meta("down_t", &t.to_variant());
            let basis = Basis::from_axis_angle(Vector3::RIGHT, -t) * Basis::from_axis_angle(Vector3::UP, std::f32::consts::PI);
            root.set_transform(Transform3D::new(basis.scaled(Vector3::splat(s)), Vector3::new(0.0, 0.25 * s * t / 1.35, 0.0)));
        }
    }

    fn fade_flashes(&mut self, dt: f32) {
        for n in self.nodes.iter_mut().flatten() {
            if n.flash <= 0.0 {
                continue;
            }
            n.flash = (n.flash - dt * 8.0).max(0.0);
            if let Some(m) = n.mesh.as_ref().and_then(|mi| mi.get_surface_override_material(0)).and_then(|m| m.try_cast::<ShaderMaterial>().ok()) {
                let mut m = m;
                m.set_shader_parameter("hit_flash", &n.flash.to_variant());
            }
        }
    }

    /// A hit on `limb` for `damage`. Returns 0 for a plain hit, 1 when a limb
    /// came off, 2 when it killed.
    pub fn take_hit(&mut self, limb: usize, damage: f32, dir: Vector3, impulse: f32, from: Vector3, gore: bool) -> i64 {
        let Some(rules) = self.rules.clone() else { return 0 };
        if self.limbs.dead {
            return 0;
        }
        let events = self.limbs.hit(Limb::from_index(limb), damage, &rules);
        self.hurt_from = Some(self.to_local_of_body(from));
        self.last_hit = Some((limb, dir, impulse));
        if let Some(n) = self.nodes.get_mut(limb).and_then(|n| n.as_mut()) {
            n.flash = 1.0;
        }
        // A shove from the hit.
        let v = self.base().get_velocity() + dir * impulse * 0.15;
        self.base_mut().set_velocity(v);
        let mut result = 0;
        for e in events {
            match e {
                HitEvent::Disarmed => {
                    self.drop_gun(dir);
                    crate::points::singleton::with_points(|p| p.on_disarm());
                }
                HitEvent::LimbOff(l) => {
                    self.detach_limb(l, dir, impulse, gore);
                    result = result.max(1);
                    crate::points::singleton::with_points(|p| p.on_limb_shot());
                }
                HitEvent::Killed => {
                    let tier = self.data.as_ref().map_or(1, |d| d.tier as i64);
                    let body = GString::from(&self.body_id());
                    crate::points::singleton::with_points(|p| p.on_kill(tier, body.clone()));
                    self.report_camp_member_out();
                    self.die(dir, impulse, gore);
                    result = 2;
                }
                HitEvent::Hurt { .. } | HitEvent::Dropped => {}
            }
        }
        result
    }

    fn drop_gun(&mut self, dir: Vector3) {
        let Some(gun) = self.gun.take() else { return };
        let Some(w) = self.weapon.clone() else { return };
        let Some(gibs) = self.gibs.clone() else { return };
        let ammo = self.rng.range_i64(w.drop_ammo.0 as i64, w.drop_ammo.1 as i64 + 1).max(0) as u32;
        let mut world = self.world_node();
        let t = gun.get_global_transform();
        let up = self.base().get_up_direction();
        let v = self.base().get_velocity() + dir * 2.0 + up * 2.5;
        drop_weapon(&mut world, &w.id, ammo, gun, t, v, gibs.pickup_life_s, gibs.pickup_radius_m);
    }

    fn piece_spec(&self, group: &'static str, life: f32, radius: f32, length: f32) -> PieceSpec {
        let fade = self.gibs.as_ref().map_or(1.5, |g| g.fade_s);
        let scale = self.data.as_ref().map_or(1.0, |d| d.scale);
        PieceSpec {
            group,
            life_s: life,
            fade_s: fade,
            mass: 4.0 + length * 6.0,
            radius: radius * scale,
            height: length * scale,
            shape_offset: Transform3D::IDENTITY,
            layer: PROP_LAYER,
            mask: 1 | 4 | PROP_LAYER,
        }
    }

    /// The capsule centre and axis of a limb, in world space.
    fn limb_frame(&self, n: &LimbNodes) -> Transform3D {
        let h = n.hitbox.as_ref().map(|h| h.get_global_transform());
        let col = n
            .hitbox
            .as_ref()
            .and_then(|h| h.get_child(0))
            .and_then(|c| c.try_cast::<Node3D>().ok())
            .map(|c| c.get_global_transform());
        col.or(h).unwrap_or_else(|| n.pivot.get_global_transform())
    }

    fn detach_limb(&mut self, limb: Limb, dir: Vector3, impulse: f32, gore: bool) {
        let Some(gibs) = self.gibs.clone() else { return };
        let Some(n) = self.nodes.get_mut(limb.index()).and_then(|n| n.take()) else { return };
        let frame = self.limb_frame(&n);
        let mut world = self.world_node();
        let up = self.base().get_up_direction();
        let v = self.base().get_velocity() + dir * impulse * 0.6 + up * 2.5;
        let spin = Vector3::new(self.rng.range_f32(-6.0, 6.0), self.rng.range_f32(-6.0, 6.0), self.rng.range_f32(-6.0, 6.0));
        let meshes: Vec<Gd<Node3D>> = n.mesh.iter().map(|m| m.clone().upcast()).collect();
        let spec = self.piece_spec(GIB_GROUP, gibs.gib_life_s, n.radius * 0.8, (n.length - n.radius * 2.0).max(0.0));
        spawn_piece(&mut world, &format!("Gib{:?}", limb), &meshes, frame, v, spin, &spec);
        let stump = n.pivot.get_global_position();
        let g = self.body().map_or(Vector3::ZERO, |b| b.bind().gravity_at(stump));
        stump_burst(&mut world, stump, dir + up * 0.5, g, gore, limb == Limb::Head);
        if let Some(mut h) = n.hitbox {
            h.queue_free();
        }
        enforce_cap(&self.base().get_tree(), GIB_GROUP, gibs.max_gibs as usize);
    }

    /// Death: every remaining limb becomes a jointed ragdoll piece.
    fn die(&mut self, dir: Vector3, impulse: f32, gore: bool) {
        let Some(gibs) = self.gibs.clone() else { return };
        self.drop_gun(dir);
        if let Some(t) = &mut self.tell {
            t.set_visible(false);
        }
        let mut world = self.world_node();
        let base_v = self.base().get_velocity();
        let up = self.base().get_up_direction();
        let hit_limb = self.last_hit.map(|h| h.0);
        let mut pieces: Vec<(usize, Gd<crate::combat::gibs::Gib>, Vector3)> = Vec::new();
        for i in 0..6 {
            let Some(n) = self.nodes.get_mut(i).and_then(|n| n.take()) else { continue };
            let frame = self.limb_frame(&n);
            let pivot = n.pivot.get_global_position();
            let kick = if Some(i) == hit_limb { 1.0 } else { 0.4 };
            let v = base_v + dir * impulse * kick + up * 1.2 * kick;
            let meshes: Vec<Gd<Node3D>> = n.mesh.iter().map(|m| m.clone().upcast()).collect();
            let spec = self.piece_spec(RAGDOLL_GROUP, gibs.ragdoll_life_s, n.radius * 0.8, (n.length - n.radius * 2.0).max(0.0));
            let piece = spawn_piece(&mut world, &format!("Ragdoll{:?}", Limb::from_index(i)), &meshes, frame, v, Vector3::ZERO, &spec);
            pieces.push((i, piece, pivot));
            if let Some(mut h) = n.hitbox {
                h.queue_free();
            }
        }
        // Joints from the torso to each limb at the limb's pivot.
        if let Some((_, torso, _)) = pieces.iter().find(|(i, _, _)| *i == Limb::Torso.index()).cloned() {
            for (i, piece, pivot) in &pieces {
                if *i == Limb::Torso.index() {
                    continue;
                }
                let mut joint = ConeTwistJoint3D::new_alloc();
                joint.set_name(&format!("Joint{i}"));
                world.add_child(&joint);
                joint.set_global_transform(Transform3D::new(Basis::IDENTITY, *pivot));
                joint.set_node_a(&torso.get_path());
                joint.set_node_b(&piece.get_path());
                joint.set_param(godot::classes::cone_twist_joint_3d::Param::SWING_SPAN, 1.0);
                joint.set_param(godot::classes::cone_twist_joint_3d::Param::TWIST_SPAN, 0.6);
                joint.add_to_group(crate::orbits::system::FLOATING_GROUP);
                // The joint dies with its limb.
                let mut j = joint.clone();
                let mut p = piece.clone();
                p.connect("tree_exiting", &Callable::from_fn("free_joint", move |_| {
                    if j.is_instance_valid() {
                        j.queue_free();
                    }
                    Variant::nil()
                }));
            }
        }
        let g = self.body().map_or(Vector3::ZERO, |b| b.bind().gravity_at(self.base().get_global_position()));
        stump_burst(&mut world, self.base().get_global_position() + up * 1.0, dir, g, gore, true);
        enforce_cap(&self.base().get_tree(), RAGDOLL_GROUP, (gibs.max_ragdolls * 6) as usize);
        let tier = self.data.as_ref().map_or(0, |d| d.tier as i64);
        self.signals().died().emit(tier);
        self.base_mut().queue_free();
    }
}

/// A rotation whose +Y points along `dir` (capsules are along Y).
fn rotation_from_up(dir: Vector3) -> Basis {
    let up = dir.normalized();
    let reference = if up.x.abs() < 0.9 { Vector3::RIGHT } else { Vector3::FORWARD };
    let z = reference.cross(up).normalized();
    let x = up.cross(z);
    Basis::from_cols(x, up, z)
}

/// Let every enemy within `radius` of `at` hear a noise there.
/// `except` skips the enemy making the noise (it is busy, and knows).
pub fn hear_noise(tree: &Gd<godot::classes::SceneTree>, at: Vector3, radius: f32, except: Option<InstanceId>) {
    for n in tree.get_nodes_in_group(ENEMY_GROUP).iter_shared() {
        if Some(n.instance_id()) == except {
            continue;
        }
        let Ok(mut e) = n.try_cast::<Enemy>() else { continue };
        if e.get_global_position().distance_to(at) <= radius {
            let local = e.bind().to_local_of_body(at);
            e.bind_mut().heard = Some(local);
        }
    }
}

/// A short-lived glowing beam from `from` to `to` that frees itself.
pub fn spawn_beam(world: &mut Gd<Node>, from: Vector3, to: Vector3, color: (f32, f32, f32), width: f32, life: f32) {
    let len = from.distance_to(to);
    if len < 0.05 {
        return;
    }
    let mut mesh = CylinderMesh::new_gd();
    mesh.set_top_radius(width);
    mesh.set_bottom_radius(width);
    mesh.set_height(len);
    mesh.set_radial_segments(6);
    mesh.set_rings(1);
    let mut mat = StandardMaterial3D::new_gd();
    mat.set_shading_mode(ShadingMode::UNSHADED);
    mat.set_transparency(Transparency::ALPHA);
    mat.set_blend_mode(BlendMode::ADD);
    mat.set_albedo(Color::from_rgba(color.0, color.1, color.2, 0.9));
    mesh.set_material(&mat);
    let mut node = MeshInstance3D::new_alloc();
    node.set_mesh(&mesh.upcast::<Mesh>());
    node.set_cast_shadows_setting(godot::classes::geometry_instance_3d::ShadowCastingSetting::OFF);
    world.add_child(&node);
    let dir = (to - from) / len;
    let side = if dir.y.abs() < 0.9 { Vector3::UP } else { Vector3::RIGHT };
    let x = dir.cross(side).normalized();
    let z = x.cross(dir).normalized();
    node.set_global_transform(Transform3D::new(Basis::from_cols(x, dir, z), (from + to) * 0.5));
    node.add_to_group(crate::orbits::system::FLOATING_GROUP);
    let mut timer = world.get_tree().create_timer(life as f64);
    let target = node.clone();
    timer.connect("timeout", &Callable::from_fn("free_beam", move |_| {
        let mut t = target.clone();
        if t.is_instance_valid() {
            t.queue_free();
        }
        Variant::nil()
    }));
}
