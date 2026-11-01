//! Things to fix for points (CLAUDE.md sections 3 and 5): ground dishes to
//! switch on and damaged satellites to repair. Both are a `RepairSite`: a
//! few parts you look at and hold X / E on, sharing the ship repair prompt
//! and its one repair sound (a missing asset). A dish has one quick part;
//! a satellite has several, out in orbit, and its restored signal draws a
//! patrol (section 3) unless you hold Signal Jammer.
//!
//! Placement is seeded per run (section 4): which way each dish sits from
//! the landing pole, the satellite's orbit and which panels are broken.
//! Values live in data/items/sites.ron; points in data/points/sources.ron.
//!
//! Serves pillar 3 (every run counts: new spots each seed) and pillar 4
//! (everything is physical: you fly up and fix the real hardware), fun rule
//! 2 (interesting decisions: a repair pays but brings a patrol) and fun
//! rule 7 (serious fun: restoring things).

use godot::classes::light_3d::Param;
use godot::classes::{BoxShape3D, CollisionShape3D, INode3D, MeshInstance3D, Node3D, OmniLight3D, StaticBody3D};
use godot::prelude::*;
use serde::Deserialize;

use crate::flight::meshes::{Geo, rounded_box, rounded_disc, torus};
use crate::orbits::landmarks::{Orbiter, instantiate, orbit_position, paint_lit, paint_glow};
use crate::procgen::terrain::TerrainShape;
use crate::seed::SeededRng;

pub const SITE_GROUP: &str = "repair_sites";
pub const PATROL_GROUP: &str = "signal_patrols";
pub const SITES_PATH: &str = "res://data/items/sites.ron";
const SITES_EMBEDDED: &str = include_str!("../../../../godot/data/items/sites.ron");

#[derive(Debug, Clone, Deserialize)]
pub struct SitesData {
    pub reach_m: f32,
    pub cone_deg: f32,
    pub look: SiteLook,
    #[serde(default)]
    pub dishes: Vec<DishSet>,
    #[serde(default)]
    pub satellites: Vec<SatelliteData>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct SiteLook {
    pub broken_color: (f32, f32, f32),
    pub fixed_color: (f32, f32, f32),
    pub light_energy: f32,
    pub light_range_m: f32,
    pub flicker_hz: f32,
    pub cabinet_color: (f32, f32, f32),
    pub trim_color: (f32, f32, f32),
}

#[derive(Debug, Clone, Deserialize)]
pub struct DishSet {
    pub body: String,
    pub count: u32,
    pub angle_deg: (f32, f32),
    pub min_apart_deg: f32,
    pub model: String,
    pub scale: f32,
    pub sink_m: f32,
    pub grime: f32,
    pub cabinet_offset_m: f32,
    pub cabinet_size_m: (f32, f32, f32),
    pub switch_s: f32,
    pub slew_deg_s: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct SatelliteData {
    pub id: String,
    pub body: String,
    pub model: String,
    pub scale: f32,
    pub altitude_m: (f32, f32),
    pub period_s: f32,
    pub inclination_deg: (f32, f32),
    #[serde(default)]
    pub spin_deg_s: (f32, f32, f32),
    pub parts: u32,
    pub part_s: f32,
    pub patrol: Vec<String>,
    pub patrol_spread_m: f32,
    pub patrol_alert_m: f32,
}

pub fn load_sites() -> SitesData {
    crate::data::load(SITES_PATH, SITES_EMBEDDED)
}

// ---- Seeded placement (pure, for determinism tests) ----

/// Directions (from the body's centre) for a body's dishes: each within
/// the set's angle band from the landing pole (+Y), apart from each other
/// and from `avoid` (the camp) by at least `min_apart_deg`.
pub fn dish_dirs(set: &DishSet, run_seed: u64, avoid: &[Vector3]) -> Vec<Vector3> {
    let mut rng = SeededRng::derive(run_seed, &format!("dishes_{}", set.body));
    let mut out: Vec<Vector3> = Vec::new();
    let min = set.min_apart_deg.to_radians();
    for _ in 0..set.count {
        // Rejection sampling, with a fixed number of tries so it always ends.
        let mut pick = Vector3::UP;
        for attempt in 0..64 {
            let tilt = rng.range_f32(set.angle_deg.0, set.angle_deg.1).to_radians();
            let around = rng.range_f32(0.0, std::f32::consts::TAU);
            pick = Vector3::new(tilt.sin() * around.cos(), tilt.cos(), tilt.sin() * around.sin()).normalized();
            let clear = out.iter().chain(avoid.iter()).all(|d| d.angle_to(pick) >= min);
            if clear || attempt == 63 {
                break;
            }
        }
        out.push(pick);
    }
    out
}

/// The satellite's seeded orbit around a body of `radius` metres.
pub fn satellite_orbit(s: &SatelliteData, run_seed: u64, radius: f32) -> Orbiter {
    let mut rng = SeededRng::derive(run_seed, &format!("satellite_{}", s.id));
    Orbiter {
        name: s.id.clone(),
        model: s.model.clone(),
        scale: s.scale,
        orbit_radius_m: radius + rng.range_f32(s.altitude_m.0, s.altitude_m.1),
        period_s: s.period_s,
        inclination_deg: rng.range_f32(s.inclination_deg.0, s.inclination_deg.1),
        node_deg: rng.range_f32(0.0, 360.0),
        phase_deg: rng.range_f32(0.0, 360.0),
        spin_deg_s: s.spin_deg_s,
    }
}

/// Which of the six faces of the satellite's bounds hold the broken
/// panels (seeded, distinct).
pub fn broken_faces(s: &SatelliteData, run_seed: u64) -> Vec<usize> {
    let mut rng = SeededRng::derive(run_seed, &format!("satellite_parts_{}", s.id));
    let mut faces: Vec<usize> = (0..6).collect();
    // Fisher-Yates with the seeded stream.
    for i in (1..faces.len()).rev() {
        let j = (rng.next_u64() % (i as u64 + 1)) as usize;
        faces.swap(i, j);
    }
    faces.truncate((s.parts as usize).clamp(1, 6));
    faces
}

const FACES: [Vector3; 6] = [Vector3::RIGHT, Vector3::LEFT, Vector3::UP, Vector3::DOWN, Vector3::BACK, Vector3::FORWARD];

// ---- The site node ----

#[derive(Debug, Clone, Copy, PartialEq, Eq, Default)]
pub enum SiteKind {
    #[default]
    Dish,
    Satellite,
}

struct Part {
    /// In the site's own space.
    local: Vector3,
    need_s: f32,
    done_s: f32,
    light: Gd<OmniLight3D>,
    glow: Gd<MeshInstance3D>,
}

#[derive(GodotClass)]
#[class(base = Node3D, init)]
pub struct RepairSite {
    kind: SiteKind,
    key: String,
    body_id: String,
    name_key: String,
    look: Option<SiteLook>,
    reach_m: f32,
    cone_deg: f32,
    parts: Vec<Part>,
    fixed: bool,
    time: f32,
    /// Dish: the model turns once switched on.
    slew_deg_s: f32,
    spinner: Option<Gd<Node3D>>,
    /// Satellite: its orbit and patrol.
    orbit: Option<Orbiter>,
    satellite: Option<SatelliteData>,
    held: bool,
    base: Base<Node3D>,
}

#[godot_api]
impl INode3D for RepairSite {
    fn ready(&mut self) {
        self.base_mut().add_to_group(SITE_GROUP);
    }

    fn physics_process(&mut self, delta: f64) {
        let dt = if self.held { 0.0 } else { delta as f32 };
        self.time += dt;
        if let Some(o) = &self.orbit {
            let p = orbit_position(o, self.time);
            let s = o.spin_deg_s;
            let spin = Vector3::new(s.0, s.1, s.2) * dt.to_radians();
            self.base_mut().set_position(p);
            self.base_mut().rotate_x(spin.x);
            self.base_mut().rotate_y(spin.y);
            self.base_mut().rotate_z(spin.z);
        }
        if self.fixed && self.slew_deg_s != 0.0 && let Some(mut s) = self.spinner.clone() {
            s.rotate_y((self.slew_deg_s * dt).to_radians());
        }
        // Broken parts sputter; fixed ones glow steady.
        let Some(look) = &self.look else { return };
        let hz = look.flicker_hz;
        let energy = look.light_energy;
        let t = self.time;
        for (i, p) in self.parts.iter_mut().enumerate() {
            if p.done_s >= p.need_s {
                continue;
            }
            let f = ((t * hz + i as f32 * 1.7).sin() * (t * hz * 2.3 + i as f32).sin()).abs();
            let on = if f > 0.35 { 1.0 } else { 0.15 };
            p.light.set_param(Param::ENERGY, energy * on);
            p.glow.set_visible(on > 0.5);
        }
    }
}

#[godot_api]
impl RepairSite {
    /// The part the player is looking at, if any:
    /// (index, prompt kind "repair" or "switch", part name key, progress 0..1).
    pub fn repair_spot(&self, eye: Vector3, look: Vector3) -> Option<(usize, &'static str, String, f32)> {
        if self.fixed {
            return None;
        }
        let t = self.base().get_global_transform();
        let mut best: Option<(f32, usize)> = None;
        for (i, p) in self.parts.iter().enumerate() {
            if p.done_s >= p.need_s {
                continue;
            }
            let to = t * p.local - eye;
            if to.length() > self.reach_m {
                continue;
            }
            let angle = look.angle_to(to).to_degrees();
            if angle > self.cone_deg && to.length() > 0.8 {
                continue;
            }
            if best.is_none_or(|(a, _)| angle < a) {
                best = Some((angle, i));
            }
        }
        let (_, i) = best?;
        let p = &self.parts[i];
        let verb = if self.kind == SiteKind::Dish { "switch" } else { "repair" };
        Some((i, verb, self.name_key.clone(), (p.done_s / p.need_s.max(0.01)).clamp(0.0, 1.0)))
    }

    /// Hold on part `i` for `dt` seconds. Returns its progress 0..1.
    pub fn work(&mut self, i: usize, dt: f32) -> f32 {
        if self.fixed || i >= self.parts.len() {
            return 1.0;
        }
        let look = self.look.clone();
        let p = &mut self.parts[i];
        let was_done = p.done_s >= p.need_s;
        p.done_s = (p.done_s + dt).min(p.need_s);
        let frac = p.done_s / p.need_s.max(0.01);
        if !was_done && p.done_s >= p.need_s {
            if let Some(look) = &look {
                let c = Color::from_rgb(look.fixed_color.0, look.fixed_color.1, look.fixed_color.2);
                p.light.set_color(c);
                p.light.set_param(Param::ENERGY, look.light_energy);
                p.glow.set_visible(true);
                p.glow.set_surface_override_material(0, &paint_glow(look.fixed_color, 2.0));
            }
            if self.parts.iter().all(|p| p.done_s >= p.need_s) {
                self.finish();
            }
        }
        frac
    }

    fn finish(&mut self) {
        self.fixed = true;
        let key = GString::from(&self.key);
        let body = GString::from(&self.body_id);
        match self.kind {
            SiteKind::Dish => {
                let all = self.all_dishes_on();
                crate::points::singleton::with_points(|p| {
                    p.on_repaired(GString::from("dish"), key, body, all);
                });
            }
            SiteKind::Satellite => {
                crate::points::singleton::with_points(|p| {
                    p.on_repaired(GString::from("satellite"), key, body, false);
                });
                if !crate::points::singleton::flag("quiet_satellites") {
                    self.call_patrol();
                }
            }
        }
    }

    /// Every dish on this body switched on (this one counts as on).
    fn all_dishes_on(&self) -> bool {
        let me = self.base().instance_id();
        self.base().get_tree().get_nodes_in_group(SITE_GROUP).iter_shared().all(|n| {
            if n.instance_id() == me {
                return true;
            }
            match n.try_cast::<RepairSite>() {
                Ok(s) => {
                    let s = s.bind();
                    s.kind != SiteKind::Dish || s.body_id != self.body_id || s.fixed
                }
                Err(_) => true,
            }
        })
    }

    /// The restored signal draws a patrol: soldiers land on the surface
    /// under the satellite and search toward the signal.
    fn call_patrol(&mut self) {
        let Some(s) = self.satellite.clone() else { return };
        let Some(mut body) = self.base().get_parent().and_then(|p| p.try_cast::<crate::orbits::CelestialBody>().ok()) else {
            return;
        };
        let Some(shape) = body.bind().shape() else { return };
        let below = self.base().get_position().normalized_or_zero();
        if below == Vector3::ZERO {
            return;
        }
        let run_seed = if godot::classes::Engine::singleton().has_singleton("RunSeed") { crate::seed::singleton::run_seed() } else { 0 };
        let mut rng = SeededRng::derive(run_seed, &format!("patrol_{}", s.id));
        let side_a = below.cross(if below.y.abs() < 0.9 { Vector3::UP } else { Vector3::RIGHT }).normalized();
        let side_b = below.cross(side_a).normalized();
        let r = shape.radius().max(1.0);
        let mut spawned = Vec::new();
        for (i, id) in s.patrol.iter().enumerate() {
            let a = i as f32 / s.patrol.len().max(1) as f32 * std::f32::consts::TAU + rng.range_f32(-0.4, 0.4);
            let dist = rng.range_f32(0.4, 1.0) * s.patrol_spread_m;
            let dir = (below * r + (side_a * a.cos() + side_b * a.sin()) * dist).normalized();
            let ground = shape.surface_radius(dir);
            let mut e = crate::ai::enemy::Enemy::new_alloc();
            e.set_name(&format!("Patrol_{}_{i}", s.id));
            e.bind_mut().set_id(id);
            e.add_to_group(PATROL_GROUP);
            let fwd = (below - dir).normalized_or_zero();
            let fwd = if fwd.length_squared() < 0.5 { side_a } else { fwd };
            let x = fwd.cross(dir).normalized();
            e.set_transform(Transform3D::new(Basis::from_cols(x, dir, -fwd).orthonormalized(), dir * (ground + 0.3)));
            body.add_child(&e);
            spawned.push(e);
        }
        // They heard it: search toward the patch of ground under the signal.
        let at = body.get_global_transform() * (below * shape.surface_radius(below));
        let tree = self.base().get_tree();
        crate::ai::enemy::hear_noise(&tree, at, s.patrol_alert_m, None);
        let text = tr("NOTE_PATROL_INCOMING");
        crate::ui_bridge::notify::push(&text, crate::ui_bridge::notify::Kind::Warning);
    }

    // ---- For tests and the HUD ----

    #[func]
    pub fn is_fixed(&self) -> bool {
        self.fixed
    }

    #[func]
    pub fn get_kind_name(&self) -> GString {
        GString::from(match self.kind {
            SiteKind::Dish => "dish",
            SiteKind::Satellite => "satellite",
        })
    }

    #[func]
    pub fn get_body_id(&self) -> GString {
        GString::from(&self.body_id)
    }

    #[func]
    pub fn get_part_count(&self) -> i64 {
        self.parts.len() as i64
    }

    #[func]
    pub fn get_parts_fixed(&self) -> i64 {
        self.parts.iter().filter(|p| p.done_s >= p.need_s).count() as i64
    }

    /// Part `i` in world space.
    #[func]
    pub fn get_part_position(&self, i: i64) -> Vector3 {
        let t = self.base().get_global_transform();
        self.parts.get(i as usize).map_or(t.origin, |p| t * p.local)
    }

    /// Fix every part at once (tests).
    #[func]
    pub fn debug_fix_all(&mut self) {
        for i in 0..self.parts.len() {
            let need = self.parts[i].need_s;
            self.work(i, need + 1.0);
        }
    }

    /// Hold the satellite still (tests: so a scripted player can float
    /// up to it).
    #[func]
    pub fn debug_hold_orbit(&mut self, on: bool) {
        self.held = on;
    }
}

fn tr(key: &str) -> String {
    godot::classes::TranslationServer::singleton().translate(&StringName::from(key)).to_string()
}

// ---- Building ----

fn part_marker(look: &SiteLook, name: &str) -> (Gd<Node3D>, Gd<OmniLight3D>, Gd<MeshInstance3D>) {
    let mut holder = Node3D::new_alloc();
    holder.set_name(name);
    let c = Color::from_rgb(look.broken_color.0, look.broken_color.1, look.broken_color.2);
    let mut light = OmniLight3D::new_alloc();
    light.set_name("Light");
    light.set_color(c);
    light.set_param(Param::ENERGY, look.light_energy);
    light.set_param(Param::RANGE, look.light_range_m);
    light.set_position(Vector3::new(0.0, 0.0, 0.35));
    holder.add_child(&light);
    let mut glow = MeshInstance3D::new_alloc();
    glow.set_name("Glow");
    glow.set_mesh(&rounded_disc(0.07, 0.03, 16).to_array_mesh());
    glow.set_rotation(Vector3::new(std::f32::consts::FRAC_PI_2, 0.0, 0.0));
    glow.set_position(Vector3::new(0.0, 0.0, 0.05));
    glow.set_surface_override_material(0, &paint_glow(look.broken_color, 2.5));
    holder.add_child(&glow);
    (holder, light, glow)
}

/// The dish's control cabinet: a bevelled steel locker with a lamp and a
/// hand-wheel, the part you switch on.
fn cabinet_mesh(look: &SiteLook, size: Vector3) -> Gd<Node3D> {
    let mut root = Node3D::new_alloc();
    root.set_name("Cabinet");
    let mut body = MeshInstance3D::new_alloc();
    body.set_name("Body");
    let mut g = Geo::default();
    g.append(&rounded_box(size, 0.08, 4), Transform3D::new(Basis::IDENTITY, Vector3::new(0.0, size.y * 0.5, 0.0)));
    body.set_mesh(&g.to_array_mesh());
    body.set_surface_override_material(0, &paint_lit(look.cabinet_color, 0.35));
    root.add_child(&body);
    // A dark plinth and a rain hood, so it reads as a cabinet, not a box.
    let mut trim = MeshInstance3D::new_alloc();
    trim.set_name("Trim");
    let mut t = Geo::default();
    t.append(&rounded_box(Vector3::new(size.x + 0.12, 0.12, size.z + 0.12), 0.04, 3), Transform3D::new(Basis::IDENTITY, Vector3::new(0.0, 0.06, 0.0)));
    t.append(&rounded_box(Vector3::new(size.x + 0.1, 0.08, size.z + 0.18), 0.035, 3), Transform3D::new(Basis::IDENTITY, Vector3::new(0.0, size.y + 0.02, 0.03)));
    // The hand-wheel on the front.
    t.append(&torus(0.13, 0.022, 20, 8), Transform3D::new(Basis::from_axis_angle(Vector3::RIGHT, std::f32::consts::FRAC_PI_2), Vector3::new(0.0, size.y * 0.45, size.z * 0.5 + 0.04)));
    trim.set_mesh(&t.to_array_mesh());
    trim.set_surface_override_material(0, &paint_lit(look.trim_color, 0.6));
    root.add_child(&trim);
    root
}

/// The dishes and damaged satellites that belong on this body.
pub fn sites_for_body(body_id: &str, shape: &TerrainShape, run_seed: u64) -> Vec<Gd<Node3D>> {
    let data = load_sites();
    let mut out = Vec::new();
    // Keep dishes away from where the ante 1 camp stands.
    let enemies = crate::combat::load_enemies();
    let avoid: Vec<Vector3> = enemies
        .camps
        .iter()
        .enumerate()
        .filter(|(_, c)| c.body == body_id)
        .map(|(i, c)| crate::ai::camp::camp_centre(body_id, i, c, run_seed, 1).0)
        .collect();
    for set in data.dishes.iter().filter(|d| d.body == body_id) {
        for (i, dir) in dish_dirs(set, run_seed, &avoid).into_iter().enumerate() {
            out.push(build_dish(&data, set, i, dir, shape));
        }
    }
    for s in data.satellites.iter().filter(|s| s.body == body_id) {
        out.push(build_satellite(&data, s, shape, run_seed));
    }
    out
}

fn build_dish(data: &SitesData, set: &DishSet, i: usize, dir: Vector3, shape: &TerrainShape) -> Gd<Node3D> {
    let mut site = RepairSite::new_alloc();
    site.set_name(&format!("Dish_{}_{i}", set.body));
    let ground = shape.surface_radius(dir);
    let up_basis = crate::orbits::body::basis_with_up(dir);
    site.set_transform(Transform3D::new(up_basis, dir * ground));
    // The dish itself, solid, turning on a spinner once it's on.
    let mut spinner = Node3D::new_alloc();
    spinner.set_name("Spinner");
    if let Some(mut model) = instantiate(&set.model, "Dish") {
        let mut solid = StaticBody3D::new_alloc();
        solid.set_name("DishSolid");
        model.set_scale(Vector3::splat(set.scale));
        model.set_position(Vector3::new(0.0, -set.sink_m, 0.0));
        solid.add_child(&model);
        local_hulls(&mut solid, &model);
        spinner.add_child(&solid);
        crate::orbits::landmarks::grime(&model, set.grime);
    }
    site.add_child(&spinner);
    // The cabinet stands off to one side, facing the dish. Sit it on the
    // ground under its own spot.
    let look = &data.look;
    let off = Vector3::new(set.cabinet_offset_m, 0.0, 0.0);
    let cab_dir = (dir * ground + up_basis * off).normalized();
    let cab_ground = shape.surface_radius(cab_dir);
    let cab_local = up_basis.inverse() * (cab_dir * cab_ground - dir * ground);
    let size = Vector3::new(set.cabinet_size_m.0, set.cabinet_size_m.1, set.cabinet_size_m.2);
    let mut cab = StaticBody3D::new_alloc();
    cab.set_name("CabinetSolid");
    // Face the dish (its front, +Z, points back toward the centre).
    let face = Basis::from_axis_angle(Vector3::UP, std::f32::consts::FRAC_PI_2);
    cab.set_transform(Transform3D::new(face, cab_local - Vector3::new(0.0, 0.05, 0.0)));
    cab.add_child(&cabinet_mesh(look, size));
    let mut col = CollisionShape3D::new_alloc();
    let mut bx = BoxShape3D::new_gd();
    bx.set_size(size);
    col.set_shape(&bx);
    col.set_position(Vector3::new(0.0, size.y * 0.5, 0.0));
    cab.add_child(&col);
    let (mut marker, light, glow) = part_marker(look, "Switch");
    marker.set_position(Vector3::new(0.0, size.y * 0.78, size.z * 0.5 + 0.01));
    cab.add_child(&marker);
    let part_local = cab.get_transform() * marker.get_position();
    site.add_child(&cab);
    {
        let mut b = site.bind_mut();
        b.kind = SiteKind::Dish;
        b.key = format!("dish:{}:{i}", set.body);
        b.body_id = set.body.clone();
        b.name_key = "SITE_DISH".into();
        b.look = Some(look.clone());
        b.reach_m = data.reach_m;
        b.cone_deg = data.cone_deg;
        b.slew_deg_s = set.slew_deg_s;
        b.spinner = Some(spinner);
        b.parts.push(Part { local: part_local, need_s: set.switch_s, done_s: 0.0, light, glow });
    }
    site.upcast()
}

fn build_satellite(data: &SitesData, s: &SatelliteData, shape: &TerrainShape, run_seed: u64) -> Gd<Node3D> {
    let mut site = RepairSite::new_alloc();
    site.set_name(&format!("Satellite_{}", s.id));
    let orbit = satellite_orbit(s, run_seed, shape.radius());
    site.set_position(orbit_position(&orbit, 0.0));
    let mut bounds = Aabb::new(Vector3::splat(-1.0), Vector3::splat(2.0));
    if let Some(mut model) = instantiate(&s.model, "Satellite") {
        model.set_scale(Vector3::splat(s.scale));
        bounds = local_bounds(&model).unwrap_or(bounds);
        site.add_child(&model);
    }
    // A clip-on point at the middle, so you can tether while you work.
    let mut anchor = crate::player::tether::TetherAnchor::new_alloc();
    anchor.set_name("TetherAnchor");
    anchor.set_position(bounds.center());
    site.add_child(&anchor);
    let look = &data.look;
    let mut parts = Vec::new();
    for (k, face) in broken_faces(s, run_seed).into_iter().enumerate() {
        let n = FACES[face];
        // On that face of the bounds, a little in from the edge.
        let half = bounds.size * 0.5;
        let p = bounds.center() + n * (half * n.abs()).length() * 0.92;
        let (mut marker, light, glow) = part_marker(look, &format!("Broken{k}"));
        marker.set_position(p);
        // The panel faces outward along that face's normal.
        let up = if n.y.abs() > 0.9 { Vector3::BACK } else { Vector3::UP };
        let x = up.cross(n).normalized();
        marker.set_basis(Basis::from_cols(x, n.cross(x), n));
        site.add_child(&marker);
        parts.push(Part { local: p, need_s: s.part_s, done_s: 0.0, light, glow });
    }
    {
        let mut b = site.bind_mut();
        b.kind = SiteKind::Satellite;
        b.key = format!("satellite:{}", s.id);
        b.body_id = s.body.clone();
        b.name_key = "SITE_SATELLITE".into();
        b.look = Some(look.clone());
        b.reach_m = data.reach_m;
        b.cone_deg = data.cone_deg;
        b.parts = parts;
        b.orbit = Some(orbit);
        b.satellite = Some(s.clone());
    }
    site.upcast()
}

/// `node`'s transform in `ancestor`'s space, from local transforms only
/// (works before either is in the tree).
fn relative_to(node: &Gd<Node3D>, ancestor: &Gd<Node3D>) -> Transform3D {
    let mut t = node.get_transform();
    let mut parent = node.get_parent();
    while let Some(p) = parent {
        if p.instance_id() == ancestor.instance_id() {
            break;
        }
        let Ok(p3) = p.try_cast::<Node3D>() else { break };
        t = p3.get_transform() * t;
        parent = p3.get_parent();
    }
    t
}

/// One convex hull per mesh part of `model`, in `solid`'s space.
fn local_hulls(solid: &mut Gd<StaticBody3D>, model: &Gd<Node3D>) {
    let frame = solid.clone().upcast::<Node3D>();
    let parts = model.find_children_ex("*").type_("MeshInstance3D").recursive(true).owned(false).done();
    for part in parts.iter_shared() {
        let Ok(mi) = part.try_cast::<MeshInstance3D>() else { continue };
        let Some(mesh) = mi.get_mesh() else { continue };
        let Some(shape) = mesh.create_convex_shape_ex().clean(true).simplify(true).done() else { continue };
        let mut col = CollisionShape3D::new_alloc();
        col.set_shape(&shape);
        col.set_transform(relative_to(&mi.clone().upcast(), &frame));
        solid.add_child(&col);
    }
}

/// Bounds of every mesh under `model`, in the space of `model`'s parent
/// (walks local transforms, so it works before the model is in the tree).
fn local_bounds(model: &Gd<Node3D>) -> Option<Aabb> {
    let parts = model.find_children_ex("*").type_("MeshInstance3D").recursive(true).owned(false).done();
    let mut out: Option<Aabb> = None;
    for part in parts.iter_shared() {
        let Ok(mi) = part.clone().try_cast::<MeshInstance3D>() else { continue };
        let Some(mesh) = mi.get_mesh() else { continue };
        // Transform from the part up to the model's parent.
        let mut t = mi.get_transform();
        let mut node = mi.get_parent();
        while let Some(n) = node {
            let Ok(n3) = n.clone().try_cast::<Node3D>() else { break };
            t = n3.get_transform() * t;
            if n3.instance_id() == model.instance_id() {
                break;
            }
            node = n3.get_parent();
        }
        let a = mesh.get_aabb();
        for c in 0..8u32 {
            let pick = |bit: u32, lo: f32, size: f32| if c & bit != 0 { lo + size } else { lo };
            let corner = Vector3::new(pick(1, a.position.x, a.size.x), pick(2, a.position.y, a.size.y), pick(4, a.position.z, a.size.z));
            let p: Vector3 = t * corner;
            out = Some(match out {
                Some(b) => b.expand(p),
                None => Aabb::new(p, Vector3::ZERO),
            });
        }
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;

    fn data() -> SitesData {
        crate::data::parse(SITES_EMBEDDED).expect("sites.ron parses")
    }

    #[test]
    fn data_sites_parse() {
        let d = data();
        assert_eq!(d.dishes.iter().filter(|s| s.body == "moon").map(|s| s.count).sum::<u32>(), 2, "two dishes on the Moon (slice)");
        assert_eq!(d.satellites.len(), 1, "one damaged satellite (slice)");
        assert!(d.satellites[0].parts >= 2, "satellite repairs are multi-part");
    }

    #[test]
    fn determinism_dishes_same_seed_same_spots() {
        let d = data();
        let set = &d.dishes[0];
        let a = dish_dirs(set, 42, &[]);
        let b = dish_dirs(set, 42, &[]);
        assert_eq!(a, b);
        assert_ne!(a, dish_dirs(set, 43, &[]));
    }

    #[test]
    fn dishes_sit_in_their_band_and_apart() {
        let d = data();
        let set = &d.dishes[0];
        for seed in 0..200u64 {
            let camp = Vector3::new(0.3, 0.95, 0.0).normalized();
            let dirs = dish_dirs(set, seed, &[camp]);
            assert_eq!(dirs.len(), set.count as usize);
            for v in &dirs {
                let from_pole = v.angle_to(Vector3::UP).to_degrees();
                assert!(from_pole >= set.angle_deg.0 - 0.01 && from_pole <= set.angle_deg.1 + 0.01, "seed {seed}: {from_pole}");
            }
            // Apart from each other and the camp (rejection gives up only after 64 tries).
            let all: Vec<Vector3> = dirs.iter().copied().chain([camp]).collect();
            let mut close = 0;
            for i in 0..all.len() {
                for j in (i + 1)..all.len() {
                    if all[i].angle_to(all[j]).to_degrees() < set.min_apart_deg {
                        close += 1;
                    }
                }
            }
            assert_eq!(close, 0, "seed {seed}");
        }
    }

    #[test]
    fn determinism_satellite_orbit_and_parts() {
        let d = data();
        let s = &d.satellites[0];
        let a = satellite_orbit(s, 7, 260.0);
        let b = satellite_orbit(s, 7, 260.0);
        assert_eq!((a.orbit_radius_m, a.node_deg, a.phase_deg), (b.orbit_radius_m, b.node_deg, b.phase_deg));
        assert!(a.orbit_radius_m >= 260.0 + s.altitude_m.0 && a.orbit_radius_m <= 260.0 + s.altitude_m.1);
        let f = broken_faces(s, 7);
        assert_eq!(f, broken_faces(s, 7));
        assert_eq!(f.len(), s.parts as usize);
        let mut u = f.clone();
        u.sort();
        u.dedup();
        assert_eq!(u.len(), f.len(), "distinct panels");
    }
}
