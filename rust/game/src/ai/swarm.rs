//! Hunter swarms (CLAUDE.md section 2: "more enemies (counts should be
//! high)" as antes rise). Every so often a pack of faction hunters drops in
//! round the player and hunts them down; packs grow and come more often
//! each ante. Data: data/combat/swarms.ron.
//!
//! Fair by construction (section 12 rule 8): every drop is announced and
//! marked by red flare columns `warn_s` before anyone lands, hunters only
//! come for a player on foot or in a rover near a surface, never inside the
//! home base's safe ring, and the live soldier count never passes the
//! section 14 cap. Counterplay: fight, hide and juke between tracker pings,
//! or get in a ship and leave; hunters left far behind give up.
//!
//! Serves pillar 2 (goofy but deadly: real danger) and pillar 3 (each ante
//! raises the pressure), fun rule 1 (mastery is challenged) and fun rule 2
//! (stay and fight for points, or move on).

use godot::classes::light_3d::Param;
use godot::classes::{CylinderMesh, INode, Mesh, MeshInstance3D, Node, Node3D, OmniLight3D};
use godot::prelude::*;
use serde::Deserialize;

use super::enemy::Enemy;
use crate::orbits::CelestialBody;
use crate::orbits::body::{GRAVITY_GROUP, basis_with_up};
use crate::player::PlayerController;
use crate::player::controller::PLAYER_GROUP;
use crate::seed::SeededRng;

const SWARMS_PATH: &str = "res://data/combat/swarms.ron";
const SWARMS_EMBEDDED: &str = include_str!("../../../../godot/data/combat/swarms.ron");
/// Every hunter a swarm dropped.
pub const HUNTER_GROUP: &str = "hunters";
pub const SWARM_GROUP: &str = "swarm_director";

#[derive(Debug, Clone, Deserialize)]
pub struct SwarmData {
    pub antes: Vec<AnteSwarm>,
    pub endless_size_step: u32,
    pub endless_every_mult: f32,
    pub every_min_s: f32,
    pub tier1: String,
    pub tier2_id: String,
    pub warn_s: f32,
    pub ring_m: (f32, f32),
    pub spread_m: f32,
    pub drop_height_m: f32,
    pub track_every_s: f32,
    pub track_m: f32,
    pub despawn_m: f32,
    pub max_altitude_m: f32,
    pub home_safe_m: f32,
    pub max_active: u32,
    pub flare: Flare,
}

#[derive(Debug, Clone, Copy, Deserialize, PartialEq)]
pub struct AnteSwarm {
    pub size: (u32, u32),
    pub every_s: f32,
    pub first_s: f32,
    pub tier2: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct Flare {
    pub color: (f32, f32, f32),
    pub width_m: f32,
    pub height_m: f32,
    pub light_energy: f32,
    pub light_range_m: f32,
}

pub fn load_swarms() -> SwarmData {
    crate::data::load(SWARMS_PATH, SWARMS_EMBEDDED)
}

/// The swarm settings for `ante` (1-based); Endless keeps growing.
pub fn ante_swarm(d: &SwarmData, ante: u32) -> AnteSwarm {
    let n = d.antes.len().max(1);
    let a = ante.max(1) as usize;
    let last = d.antes[n - 1];
    if a <= n {
        return d.antes[a - 1];
    }
    let extra = (a - n) as u32;
    AnteSwarm {
        size: (last.size.0 + extra * d.endless_size_step, last.size.1 + extra * d.endless_size_step),
        every_s: (last.every_s * d.endless_every_mult.powi(extra as i32)).max(d.every_min_s),
        first_s: last.first_s,
        tier2: last.tier2,
    }
}

/// One pack: which enemies, and where each lands relative to the pack's
/// centre (tangent-plane metres). Capped by the room left under the AI cap.
pub fn roll_pack(d: &SwarmData, w: &AnteSwarm, rng: &mut SeededRng, room: u32) -> Vec<(String, f32, f32)> {
    let size = if w.size.1 > w.size.0 { w.size.0 + rng.below((w.size.1 - w.size.0 + 1) as u64) as u32 } else { w.size.0 };
    let size = size.min(room);
    (0..size)
        .map(|i| {
            let id = if rng.range_f32(0.0, 1.0) < w.tier2 { d.tier2_id.clone() } else { d.tier1.clone() };
            let a = i as f32 / size.max(1) as f32 * std::f32::consts::TAU + rng.range_f32(-0.4, 0.4);
            let r = rng.range_f32(0.3, 1.0) * d.spread_m;
            (id, a.cos() * r, a.sin() * r)
        })
        .collect()
}

/// A pack on its way: flares are lit, hunters land when `left_s` runs out.
struct Pending {
    body: Gd<CelestialBody>,
    drops: Vec<(String, Vector3)>,
    flares: Vec<Gd<Node3D>>,
    left_s: f32,
}

#[derive(GodotClass)]
#[class(base = Node)]
pub struct SwarmDirector {
    data: SwarmData,
    enabled: bool,
    ante_seen: i64,
    next_s: f32,
    wave: u32,
    track_s: f32,
    pending: Option<Pending>,
    /// Packs dropped this run (tests and the run summary).
    dropped: u32,
    base: Base<Node>,
}

#[godot_api]
impl INode for SwarmDirector {
    fn init(base: Base<Node>) -> Self {
        Self { data: load_swarms(), enabled: true, ante_seen: -1, next_s: 0.0, wave: 0, track_s: 0.0, pending: None, dropped: 0, base }
    }

    fn ready(&mut self) {
        self.base_mut().add_to_group(SWARM_GROUP);
        // Pause with the world (between-ante screens, the map).
        self.base_mut().set_process_mode(godot::classes::node::ProcessMode::PAUSABLE);
    }

    fn physics_process(&mut self, delta: f64) {
        let dt = delta as f32;
        if !self.enabled || !godot::classes::Engine::singleton().has_singleton("Points") {
            return;
        }
        let points = crate::points::Points::singleton();
        let (phase, ante) = {
            let p = points.bind();
            (p.get_phase().to_string(), p.get_ante())
        };
        if phase != "playing" {
            return;
        }
        if ante != self.ante_seen {
            self.ante_seen = ante;
            self.wave = 0;
            self.next_s = ante_swarm(&self.data, ante.max(1) as u32).first_s;
            self.clear_pending();
        }
        let Some(player) = self.player() else { return };
        if player.bind().is_dead() {
            return;
        }
        self.track(&player, dt);
        if let Some(p) = &mut self.pending {
            p.left_s -= dt;
            if p.left_s <= 0.0 {
                self.land(&player);
            }
            return;
        }
        if self.eligible_body(&player).is_some() {
            self.next_s -= dt;
            if self.next_s <= 0.0 {
                let w = ante_swarm(&self.data, ante.max(1) as u32);
                self.next_s = w.every_s;
                self.start_pack(&player, ante.max(1) as u32);
            }
        }
    }
}

#[godot_api]
impl SwarmDirector {
    /// Tests and quiet scenes: stop (or restart) the swarms.
    #[func]
    pub fn set_enabled(&mut self, on: bool) {
        self.enabled = on;
        if !on {
            self.clear_pending();
        }
    }

    /// Seconds until the next pack is called (counts only while eligible).
    #[func]
    pub fn get_next_s(&self) -> f32 {
        self.next_s
    }

    #[func]
    pub fn is_incoming(&self) -> bool {
        self.pending.is_some()
    }

    #[func]
    pub fn get_packs_dropped(&self) -> i64 {
        self.dropped as i64
    }

    /// Pack size range and seconds between packs at `ante` (tests, report).
    #[func]
    pub fn get_ante_plan(&self, ante: i64) -> Vector3 {
        let w = ante_swarm(&self.data, ante.max(1) as u32);
        Vector3::new(w.size.0 as f32, w.size.1 as f32, w.every_s)
    }

    /// Whether a pack could come for the player right now.
    #[func]
    pub fn can_hunt_now(&self) -> bool {
        self.player().is_some_and(|p| self.eligible_body(&p).is_some())
    }

    /// Tests: call the next pack now (it still warns first).
    #[func]
    pub fn debug_call_now(&mut self) {
        self.next_s = 0.0;
    }
}

impl SwarmDirector {
    fn player(&self) -> Option<Gd<PlayerController>> {
        self.base().get_tree().get_first_node_in_group(PLAYER_GROUP).and_then(|n| n.try_cast::<PlayerController>().ok())
    }

    /// The body a pack could drop onto: the player is on foot or in a
    /// rover, near its ground, it has collision, and they are outside the
    /// home base's safe ring.
    fn eligible_body(&self, player: &Gd<PlayerController>) -> Option<Gd<CelestialBody>> {
        let pb = player.bind();
        if pb.aboard_ship().is_some() || pb.is_buckled() {
            return None;
        }
        drop(pb);
        let here = player.get_global_position();
        let tree = self.base().get_tree();
        let body = tree
            .get_nodes_in_group(GRAVITY_GROUP)
            .iter_shared()
            .filter_map(|n| n.try_cast::<CelestialBody>().ok())
            .max_by(|a, b| a.bind().gravity_at(here).length().total_cmp(&b.bind().gravity_at(here).length()))?;
        {
            let b = body.bind();
            if !b.has_detail() || b.shape().is_none() {
                return None;
            }
            let altitude = body.get_global_position().distance_to(here) - b.surface_radius_toward(here);
            if altitude > self.data.max_altitude_m {
                return None;
            }
        }
        for pad in tree.get_nodes_in_group(crate::orbits::home_base::PAD_GROUP).iter_shared() {
            if let Ok(p) = pad.try_cast::<Node3D>()
                && p.get_global_position().distance_to(here) < self.data.home_safe_m
            {
                return None;
            }
        }
        Some(body)
    }

    fn active_soldiers(&self) -> u32 {
        self.base()
            .get_tree()
            .get_nodes_in_group(super::enemy::ENEMY_GROUP)
            .iter_shared()
            .filter_map(|n| n.try_cast::<Enemy>().ok())
            .filter(|e| !e.bind().is_out() && !e.is_queued_for_deletion())
            .count() as u32
    }

    /// Light the flares and announce the pack.
    fn start_pack(&mut self, player: &Gd<PlayerController>, ante: u32) {
        let Some(mut body) = self.eligible_body(player) else { return };
        let room = self.data.max_active.saturating_sub(self.active_soldiers());
        if room == 0 {
            return;
        }
        let run_seed = if godot::classes::Engine::singleton().has_singleton("RunSeed") { crate::seed::singleton::run_seed() } else { 0 };
        let mut rng = SeededRng::derive_indexed(run_seed, &format!("swarm_a{ante}"), self.wave as u64);
        self.wave += 1;
        let w = ante_swarm(&self.data, ante);
        let pack = roll_pack(&self.data, &w, &mut rng, room);
        if pack.is_empty() {
            return;
        }
        let bt = body.get_global_transform();
        let p_local = bt.affine_inverse() * player.get_global_position();
        let up = p_local.normalized_or_zero();
        let t1 = up.cross(if up.y.abs() < 0.9 { Vector3::UP } else { Vector3::RIGHT }).normalized();
        let t2 = up.cross(t1).normalized();
        let r = body.bind().radius().max(1.0);
        let around = rng.range_f32(0.0, std::f32::consts::TAU);
        let dist = rng.range_f32(self.data.ring_m.0, self.data.ring_m.1);
        let centre = p_local.normalized() * r + (t1 * around.cos() + t2 * around.sin()) * dist;
        let mut drops = Vec::new();
        let mut flares = Vec::new();
        for (k, (id, x, z)) in pack.into_iter().enumerate() {
            let dir = (centre + t1 * x + t2 * z).normalized();
            let ground = body.bind().surface_radius_local(dir);
            drops.push((id, dir * (ground + self.data.drop_height_m)));
            let flare = self.flare(&format!("SwarmFlare{k}"), Transform3D::new(basis_with_up(dir), dir * ground));
            body.add_child(&flare);
            flares.push(flare);
        }
        crate::ui_bridge::notify::push(&tr("NOTE_HUNTERS_INBOUND"), crate::ui_bridge::notify::Kind::Warning);
        self.pending = Some(Pending { body, drops, flares, left_s: self.data.warn_s });
    }

    /// The pack lands from the flares and goes straight for the player.
    fn land(&mut self, player: &Gd<PlayerController>) {
        let Some(p) = self.pending.take() else { return };
        let mut body = p.body;
        let target = player.get_global_position();
        for f in p.flares {
            if f.is_instance_valid() {
                f.clone().queue_free();
            }
        }
        if !body.is_instance_valid() {
            return;
        }
        for (k, (id, local)) in p.drops.into_iter().enumerate() {
            let mut e = Enemy::new_alloc();
            e.set_name(&format!("Hunter_{}_{}_{k}", self.ante_seen, self.dropped));
            e.bind_mut().set_id(&id);
            e.add_to_group(HUNTER_GROUP);
            let up = local.normalized_or_zero();
            e.set_transform(Transform3D::new(basis_with_up(up), local));
            body.add_child(&e);
            e.bind_mut().hunt(target);
        }
        self.dropped += 1;
    }

    /// Tracker pings keep hunters closing in; far-off ones give up and go.
    fn track(&mut self, player: &Gd<PlayerController>, dt: f32) {
        self.track_s -= dt;
        if self.track_s > 0.0 {
            return;
        }
        self.track_s = self.data.track_every_s;
        let here = player.get_global_position();
        let hidden = player.bind().aboard_ship().is_some();
        for n in self.base().get_tree().get_nodes_in_group(HUNTER_GROUP).iter_shared() {
            let Ok(mut e) = n.try_cast::<Enemy>() else { continue };
            if e.bind().is_out() || e.is_queued_for_deletion() {
                continue;
            }
            let d = e.get_global_position().distance_to(here);
            if d > self.data.despawn_m {
                e.queue_free();
            } else if d < self.data.track_m && !hidden {
                e.bind_mut().hunt(here);
            }
        }
    }

    fn clear_pending(&mut self) {
        if let Some(p) = self.pending.take() {
            for f in p.flares {
                if f.is_instance_valid() {
                    f.clone().queue_free();
                }
            }
        }
    }

    /// A tall red flare column where a hunter will land, lit from inside.
    fn flare(&self, name: &str, t: Transform3D) -> Gd<Node3D> {
        use godot::classes::StandardMaterial3D;
        use godot::classes::base_material_3d::{BlendMode, ShadingMode, Transparency};
        let f = &self.data.flare;
        let mut root = Node3D::new_alloc();
        root.set_name(name);
        root.set_transform(t);
        let mut mesh = CylinderMesh::new_gd();
        mesh.set_top_radius(f.width_m * 0.4);
        mesh.set_bottom_radius(f.width_m);
        mesh.set_height(f.height_m);
        mesh.set_radial_segments(8);
        let mut mat = StandardMaterial3D::new_gd();
        mat.set_shading_mode(ShadingMode::UNSHADED);
        mat.set_transparency(Transparency::ALPHA);
        mat.set_blend_mode(BlendMode::ADD);
        mat.set_albedo(Color::from_rgba(f.color.0, f.color.1, f.color.2, 0.85));
        mesh.set_material(&mat);
        let mut mi = MeshInstance3D::new_alloc();
        mi.set_mesh(&mesh.upcast::<Mesh>());
        mi.set_position(Vector3::new(0.0, f.height_m * 0.5, 0.0));
        mi.set_cast_shadows_setting(godot::classes::geometry_instance_3d::ShadowCastingSetting::OFF);
        root.add_child(&mi);
        let mut light = OmniLight3D::new_alloc();
        light.set_color(Color::from_rgb(f.color.0, f.color.1, f.color.2));
        light.set_param(Param::ENERGY, f.light_energy);
        light.set_param(Param::RANGE, f.light_range_m);
        light.set_position(Vector3::new(0.0, 1.5, 0.0));
        root.add_child(&light);
        root
    }
}

fn tr(key: &str) -> String {
    godot::classes::TranslationServer::singleton().translate(&StringName::from(key)).to_string()
}

#[cfg(test)]
mod tests {
    use super::*;

    fn data() -> SwarmData {
        crate::data::parse(SWARMS_EMBEDDED).expect("swarms.ron parses")
    }

    #[test]
    fn eight_antes_and_they_escalate() {
        let d = data();
        assert_eq!(d.antes.len(), 8);
        for w in d.antes.windows(2) {
            assert!(w[1].size.0 >= w[0].size.0 && w[1].size.1 >= w[0].size.1, "packs never shrink");
            assert!(w[1].every_s <= w[0].every_s, "packs never come less often");
            assert!(w[1].tier2 >= w[0].tier2);
        }
        assert!(d.antes[7].size.1 > d.antes[0].size.1 * 3, "the finale's packs are several times bigger");
    }

    #[test]
    fn endless_keeps_growing_with_a_floor() {
        let d = data();
        let a8 = ante_swarm(&d, 8);
        let a12 = ante_swarm(&d, 12);
        assert!(a12.size.1 > a8.size.1 && a12.every_s < a8.every_s);
        assert!(ante_swarm(&d, 60).every_s >= d.every_min_s);
    }

    #[test]
    fn packs_are_seeded_capped_and_in_range() {
        let d = data();
        let w = ante_swarm(&d, 5);
        let a = roll_pack(&d, &w, &mut SeededRng::derive_indexed(7, "swarm_a5", 0), 40);
        let b = roll_pack(&d, &w, &mut SeededRng::derive_indexed(7, "swarm_a5", 0), 40);
        assert_eq!(a, b, "same seed, same pack");
        assert!(a.len() as u32 >= w.size.0 && a.len() as u32 <= w.size.1);
        assert!(a.iter().all(|(_, x, z)| (x * x + z * z).sqrt() <= d.spread_m + 1e-3));
        let capped = roll_pack(&d, &w, &mut SeededRng::derive_indexed(7, "swarm_a5", 0), 2);
        assert!(capped.len() <= 2, "never past the AI cap");
    }

    #[test]
    fn ante_one_is_gentle_tier_one_only() {
        let d = data();
        let w = ante_swarm(&d, 1);
        assert_eq!(w.tier2, 0.0);
        let pack = roll_pack(&d, &w, &mut SeededRng::derive_indexed(3, "swarm_a1", 0), 40);
        assert!(pack.iter().all(|(id, _, _)| *id == d.tier1));
        assert!(w.first_s >= 120.0, "a calm start before the first pack");
    }
}
