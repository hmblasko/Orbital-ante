//! Faction base structures (CLAUDE.md sections 4 and 7): hab pods, low
//! walls, supply crates and a radio mast standing around a camp. Seeded
//! with the camp, so a base moves when its camp is re-manned each ante
//! (section 2: factions retake planets). Walls give tier 2 soldiers cover
//! and the player something to sneak behind. Sizes and colours live in
//! data/combat/bases.ron; the vertical slice has one small Venera outpost.
//!
//! Every piece is built from bevelled, rounded parts painted with the
//! shared TF2-style shader (section 11: no plain primitives, no hard
//! edges), with a simple box collider each.
//!
//! Serves pillar 2 (goofy but deadly: a chunky, readable outpost with real
//! cover) and fun rule 2 (interesting decisions: sneak along the walls or
//! go in loud).

use godot::classes::light_3d::Param;
use godot::classes::{BoxShape3D, CollisionShape3D, INode3D, MeshInstance3D, Node3D, OmniLight3D, StaticBody3D};
use godot::prelude::*;
use serde::Deserialize;
use std::collections::BTreeMap;

use crate::flight::meshes::{Geo, rounded_box, rounded_disc, swept_tube, torus};
use crate::orbits::body::basis_with_up;
use crate::orbits::landmarks::{paint_glow, paint_lit};
use crate::procgen::terrain::TerrainShape;
use crate::seed::SeededRng;

/// Every base piece joins this group (for re-manning each ante).
pub const BASE_GROUP: &str = "base_structures";
const BASES_PATH: &str = "res://data/combat/bases.ron";
const BASES_EMBEDDED: &str = include_str!("../../../../godot/data/combat/bases.ron");

#[derive(Debug, Clone, Deserialize)]
pub struct BasesData {
    pub bases: BTreeMap<String, BaseData>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct BaseColors {
    pub hull: (f32, f32, f32),
    pub trim: (f32, f32, f32),
    pub dark: (f32, f32, f32),
    pub lamp: (f32, f32, f32),
    pub window: (f32, f32, f32),
}

#[derive(Debug, Clone, Deserialize)]
pub struct BaseData {
    pub colors: BaseColors,
    pub pods: u32,
    pub pod_size_m: (f32, f32, f32),
    pub pod_ring_m: (f32, f32),
    pub walls: u32,
    pub wall_size_m: (f32, f32, f32),
    pub wall_ring_m: (f32, f32),
    pub crates: u32,
    pub crate_size_m: f32,
    pub crate_ring_m: (f32, f32),
    pub mast_height_m: f32,
    pub mast_ring_m: (f32, f32),
    pub lamp_blink_hz: f32,
    pub min_gap_m: f32,
    pub sink_m: f32,
    /// A melee weapon to find lying on top of the first crate (section 7:
    /// melee weapons are rare and found).
    #[serde(default)]
    pub crate_find: Option<String>,
}

pub fn load_bases() -> BasesData {
    crate::data::load(BASES_PATH, BASES_EMBEDDED)
}

#[derive(Debug, Clone, Copy, PartialEq)]
pub enum PieceKind {
    Pod,
    Wall,
    Crate,
    Mast,
}

/// One piece's spot in the camp's flat tangent plane: (x, z) metres from
/// the centre, a yaw, and the radius it keeps clear.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct Piece {
    pub kind: PieceKind,
    pub x: f32,
    pub z: f32,
    pub yaw: f32,
    pub clear_m: f32,
}

/// Lay out a base's pieces (pure and seeded, for determinism tests).
pub fn layout(b: &BaseData, rng: &mut SeededRng) -> Vec<Piece> {
    let mut out: Vec<Piece> = Vec::new();
    let pod = b.pod_size_m.0.max(b.pod_size_m.2) * 0.5;
    let wall = b.wall_size_m.0 * 0.5;
    let plan = [
        (PieceKind::Mast, 1, b.mast_ring_m, 0.6),
        (PieceKind::Pod, b.pods, b.pod_ring_m, pod),
        (PieceKind::Wall, b.walls, b.wall_ring_m, wall),
        (PieceKind::Crate, b.crates, b.crate_ring_m, b.crate_size_m * 0.75),
    ];
    for (kind, count, ring, clear) in plan {
        for _ in 0..count {
            for _ in 0..48 {
                let a = rng.range_f32(0.0, std::f32::consts::TAU);
                let r = rng.range_f32(ring.0, ring.1);
                let (x, z) = (a.cos() * r, a.sin() * r);
                // Walls face the centre (cover against someone coming in);
                // everything else gets a seeded turn.
                let yaw = match kind {
                    PieceKind::Wall => -a + std::f32::consts::FRAC_PI_2,
                    _ => rng.range_f32(0.0, std::f32::consts::TAU),
                };
                let ok = out.iter().all(|p| ((p.x - x).powi(2) + (p.z - z).powi(2)).sqrt() >= p.clear_m + clear + b.min_gap_m);
                if ok {
                    out.push(Piece { kind, x, z, yaw, clear_m: clear });
                    break;
                }
            }
        }
    }
    out
}

/// A lamp that blinks (the mast's warning light).
#[derive(GodotClass)]
#[class(base = Node3D, init)]
pub struct BlinkLamp {
    hz: f32,
    time: f32,
    base: Base<Node3D>,
}

#[godot_api]
impl INode3D for BlinkLamp {
    fn process(&mut self, delta: f64) {
        self.time += delta as f32;
        let on = (self.time * self.hz).fract() < 0.5;
        if on != self.base().is_visible() {
            self.base_mut().set_visible(on);
        }
    }
}

fn mesh(parent: &mut Gd<Node3D>, name: &str, g: Geo, mat: Gd<godot::classes::Material>) {
    let mut mi = MeshInstance3D::new_alloc();
    mi.set_name(name);
    mi.set_mesh(&g.to_array_mesh());
    mi.set_surface_override_material(0, &mat);
    parent.add_child(&mi);
}

fn at(x: f32, y: f32, z: f32) -> Transform3D {
    Transform3D::new(Basis::IDENTITY, Vector3::new(x, y, z))
}

fn one(g: Geo, t: Transform3D) -> Geo {
    let mut out = Geo::default();
    out.append(&g, t);
    out
}

fn collider(solid: &mut Gd<StaticBody3D>, size: Vector3, centre: Vector3) {
    let mut col = CollisionShape3D::new_alloc();
    let mut bx = BoxShape3D::new_gd();
    bx.set_size(size);
    col.set_shape(&bx);
    col.set_position(centre);
    solid.add_child(&col);
}

/// A rounded hab pod on skids: soft hull, rust hoop bands, a porthole that
/// glows warm, and a dark hatch at one end.
fn build_pod(b: &BaseData, root: &mut Gd<Node3D>) -> (Vector3, Vector3) {
    let c = &b.colors;
    let (w, h, d) = b.pod_size_m;
    let lift = 0.35;
    let r = w.min(h).min(d) * 0.42;
    mesh(root, "Hull", one(rounded_box(Vector3::new(w, h, d), r, 6), at(0.0, lift + h * 0.5, 0.0)), paint_lit(c.hull, 0.25));
    let mut bands = Geo::default();
    for x in [-w * 0.28, w * 0.28] {
        bands.append(&rounded_box(Vector3::new(0.22, h + 0.08, d + 0.08), r * 1.02, 5), at(x, lift + h * 0.5, 0.0));
    }
    mesh(root, "Bands", bands, paint_lit(c.trim, 0.4));
    let mut skids = Geo::default();
    for z in [-d * 0.32, d * 0.32] {
        skids.append(&rounded_box(Vector3::new(w * 0.9, 0.18, 0.22), 0.07, 3), at(0.0, 0.12, z));
        for x in [-w * 0.3, w * 0.3] {
            skids.append(&rounded_box(Vector3::new(0.16, lift + 0.1, 0.16), 0.06, 3), at(x, lift * 0.5 + 0.1, z));
        }
    }
    // The hatch at the +X end, with a grab bar.
    skids.append(&rounded_box(Vector3::new(0.12, h * 0.62, d * 0.42), 0.05, 3), at(w * 0.5 - 0.02, lift + h * 0.45, 0.0));
    skids.append(&swept_tube(&[Vector3::new(w * 0.5 + 0.06, lift + h * 0.3, d * 0.15), Vector3::new(w * 0.5 + 0.06, lift + h * 0.6, d * 0.15)], &[Vector3::RIGHT, Vector3::RIGHT], 0.03, 8, false), Transform3D::IDENTITY);
    // A stub antenna on top.
    skids.append(&swept_tube(&[Vector3::new(-w * 0.15, lift + h, 0.0), Vector3::new(-w * 0.15, lift + h + 0.9, 0.0)], &[Vector3::RIGHT, Vector3::RIGHT], 0.035, 8, false), Transform3D::IDENTITY);
    mesh(root, "Fittings", skids, paint_lit(c.dark, 0.6));
    // Portholes on both long sides.
    let mut rims = Geo::default();
    let mut glass = Geo::default();
    let face = Basis::from_axis_angle(Vector3::RIGHT, std::f32::consts::FRAC_PI_2);
    for s in [1.0f32, -1.0] {
        let p = Vector3::new(-w * 0.05, lift + h * 0.58, s * (d * 0.5 + 0.01));
        rims.append(&torus(0.3, 0.06, 20, 8), Transform3D::new(face, p));
        glass.append(&rounded_disc(0.27, 0.04, 20), Transform3D::new(face, p));
    }
    mesh(root, "PortholeRims", rims, paint_lit(c.trim, 0.5));
    mesh(root, "PortholeGlass", glass, paint_glow(c.window, 1.4));
    (Vector3::new(w, h + lift, d), Vector3::new(0.0, (h + lift) * 0.5, 0.0))
}

/// A low bevelled wall with a rust cap: cover.
fn build_wall(b: &BaseData, root: &mut Gd<Node3D>) -> (Vector3, Vector3) {
    let c = &b.colors;
    let (w, h, d) = b.wall_size_m;
    mesh(root, "Wall", one(rounded_box(Vector3::new(w, h, d), 0.16, 4), at(0.0, h * 0.5, 0.0)), paint_lit(c.hull, 0.2));
    let mut trim = Geo::default();
    trim.append(&rounded_box(Vector3::new(w + 0.06, 0.12, d + 0.08), 0.05, 3), at(0.0, h + 0.02, 0.0));
    for x in [-w * 0.33, 0.0, w * 0.33] {
        trim.append(&rounded_box(Vector3::new(0.08, h * 0.7, 0.04), 0.03, 2), at(x, h * 0.45, d * 0.5));
    }
    mesh(root, "Cap", trim, paint_lit(c.trim, 0.35));
    (Vector3::new(w, h, d), Vector3::new(0.0, h * 0.5, 0.0))
}

/// A supply crate with rust bands; sometimes a second one stacked on top.
fn build_crate(b: &BaseData, root: &mut Gd<Node3D>, stacked: bool) -> (Vector3, Vector3) {
    let c = &b.colors;
    let s = b.crate_size_m;
    let layers = if stacked { 2 } else { 1 };
    let mut core = Geo::default();
    let mut bands = Geo::default();
    for k in 0..layers {
        let size = if k == 0 { s } else { s * 0.8 };
        let y = if k == 0 { size * 0.5 } else { s + size * 0.5 };
        core.append(&rounded_box(Vector3::splat(size), 0.07, 3), at(0.0, y, 0.0));
        for z in [-size * 0.3, size * 0.3] {
            bands.append(&rounded_box(Vector3::new(size + 0.04, size + 0.04, 0.08), 0.06, 3), at(0.0, y, z));
        }
    }
    mesh(root, "Crate", core, paint_lit(c.dark, 0.15));
    mesh(root, "Bands", bands, paint_lit(c.trim, 0.45));
    let h = if stacked { s * 1.8 } else { s };
    (Vector3::new(s, h, s), Vector3::new(0.0, h * 0.5, 0.0))
}

/// A radio mast: footing, pole, a small dish and a blinking lamp.
fn build_mast(b: &BaseData, root: &mut Gd<Node3D>) -> (Vector3, Vector3) {
    let c = &b.colors;
    let h = b.mast_height_m;
    mesh(root, "Footing", one(rounded_box(Vector3::new(1.0, 0.35, 1.0), 0.1, 3), at(0.0, 0.17, 0.0)), paint_lit(c.hull, 0.2));
    let mut pole = Geo::default();
    pole.append(&swept_tube(&[Vector3::new(0.0, 0.3, 0.0), Vector3::new(0.0, h, 0.0)], &[Vector3::RIGHT, Vector3::RIGHT], 0.08, 10, false), Transform3D::IDENTITY);
    for y in [h * 0.35, h * 0.7] {
        pole.append(&torus(0.14, 0.03, 14, 6), at(0.0, y, 0.0));
    }
    mesh(root, "Pole", pole, paint_lit(c.dark, 0.7));
    let tilt = Basis::from_axis_angle(Vector3::RIGHT, 1.1);
    mesh(root, "Dish", one(rounded_disc(0.55, 0.08, 20), Transform3D::new(tilt, Vector3::new(0.0, h * 0.82, 0.25))), paint_lit(c.trim, 0.4));
    let mut lamp = BlinkLamp::new_alloc();
    lamp.set_name("Lamp");
    lamp.bind_mut().hz = b.lamp_blink_hz;
    lamp.set_position(Vector3::new(0.0, h + 0.08, 0.0));
    let mut l = lamp.clone().upcast::<Node3D>();
    mesh(&mut l, "Glow", rounded_box(Vector3::splat(0.16), 0.07, 3), paint_glow(c.lamp, 3.0));
    let mut light = OmniLight3D::new_alloc();
    light.set_color(Color::from_rgb(c.lamp.0, c.lamp.1, c.lamp.2));
    light.set_param(Param::ENERGY, 1.5);
    light.set_param(Param::RANGE, 6.0);
    l.add_child(&light);
    root.add_child(&lamp);
    (Vector3::new(0.5, h, 0.5), Vector3::new(0.0, h * 0.5, 0.0))
}

/// The pieces of camp `ci`'s base this ante (empty if it has none), in the
/// camp's tangent plane. Soldiers use this to stand clear of them.
pub fn camp_layout(body_id: &str, ci: usize, camp: &crate::combat::CampData, run_seed: u64, ante: u32) -> Vec<Piece> {
    let bases = load_bases();
    let Some(b) = camp.base.as_ref().and_then(|id| bases.bases.get(id)) else { return Vec::new() };
    layout(b, &mut base_rng(body_id, ci, run_seed, ante))
}

fn base_rng(body_id: &str, ci: usize, run_seed: u64, ante: u32) -> SeededRng {
    SeededRng::derive_indexed(run_seed, &format!("base_{body_id}_a{}", ante.max(1)), ci as u64)
}

/// The base structures of the camps on this body this ante, in the body's
/// local space (the body adds them as children).
pub fn base_structures(body_id: &str, shape: &TerrainShape, run_seed: u64, ante: u32) -> Vec<Gd<Node3D>> {
    let mut out = Vec::new();
    let enemies = crate::combat::load_enemies();
    let bases = load_bases();
    for (ci, camp) in enemies.camps.iter().enumerate() {
        if camp.body != body_id || camp.from_ante > ante.max(1) {
            continue;
        }
        let Some(b) = camp.base.as_ref().and_then(|id| bases.bases.get(id)) else { continue };
        let (centre, _) = super::camp::camp_centre(body_id, ci, camp, run_seed, ante);
        let mut rng = base_rng(body_id, ci, run_seed, ante);
        let side_a = centre.cross(Vector3::RIGHT).normalized();
        let side_b = centre.cross(side_a).normalized();
        let r = shape.radius().max(1.0);
        let mut find = b.crate_find.clone();
        for (k, p) in layout(b, &mut rng).into_iter().enumerate() {
            let dir = (centre * r + side_a * p.x + side_b * p.z).normalized();
            let ground = shape.surface_radius(dir);
            let mut solid = StaticBody3D::new_alloc();
            solid.set_name(&format!("Base_{ci}_{k}_{:?}", p.kind));
            solid.add_to_group(BASE_GROUP);
            let mut root = Node3D::new_alloc();
            root.set_name("Model");
            let (size, centre_off) = match p.kind {
                PieceKind::Pod => build_pod(b, &mut root),
                PieceKind::Wall => build_wall(b, &mut root),
                PieceKind::Crate => build_crate(b, &mut root, rng.chance(0.4)),
                PieceKind::Mast => build_mast(b, &mut root),
            };
            solid.add_child(&root);
            collider(&mut solid, size, centre_off);
            if p.kind == PieceKind::Crate
                && let Some(w) = find.take()
                && let Some(pickup) = crate::combat::gibs::placed_melee(&w, Transform3D::new(Basis::from_axis_angle(Vector3::UP, 0.6), Vector3::new(0.0, size.y + 0.06, 0.0)))
            {
                solid.add_child(&pickup);
            }
            let basis = basis_with_up(dir) * Basis::from_axis_angle(Vector3::UP, p.yaw);
            solid.set_transform(Transform3D::new(basis, dir * (ground - b.sink_m)));
            out.push(solid.upcast());
        }
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;

    fn small() -> BaseData {
        let d: BasesData = crate::data::parse(BASES_EMBEDDED).expect("bases.ron parses");
        d.bases["venera_small"].clone()
    }

    #[test]
    fn data_bases_parse_and_a_camp_uses_one() {
        let e: crate::combat::EnemiesData = crate::data::parse(include_str!("../../../../godot/data/combat/enemies.ron")).unwrap();
        let d: BasesData = crate::data::parse(BASES_EMBEDDED).unwrap();
        for c in &e.camps {
            if let Some(b) = &c.base {
                assert!(d.bases.contains_key(b), "camp base {b} is defined");
            }
        }
        assert!(e.camps.iter().any(|c| c.base.is_some()), "the slice camp has a base");
    }

    #[test]
    fn determinism_base_layout_same_seed() {
        let b = small();
        let a = layout(&b, &mut SeededRng::derive(9, "base"));
        let c = layout(&b, &mut SeededRng::derive(9, "base"));
        assert_eq!(a, c);
        assert_ne!(a, layout(&b, &mut SeededRng::derive(10, "base")));
    }

    #[test]
    fn base_pieces_never_overlap_and_all_fit() {
        let b = small();
        for seed in 0..200u64 {
            let ps = layout(&b, &mut SeededRng::derive(seed, "base"));
            assert_eq!(ps.iter().filter(|p| p.kind == PieceKind::Mast).count(), 1, "seed {seed}");
            assert_eq!(ps.iter().filter(|p| p.kind == PieceKind::Pod).count(), b.pods as usize, "seed {seed}: pods fit");
            assert!(ps.iter().filter(|p| p.kind == PieceKind::Wall).count() >= 3, "seed {seed}: walls for cover");
            for i in 0..ps.len() {
                for j in (i + 1)..ps.len() {
                    let d = ((ps[i].x - ps[j].x).powi(2) + (ps[i].z - ps[j].z).powi(2)).sqrt();
                    assert!(d >= ps[i].clear_m + ps[j].clear_m + b.min_gap_m - 1e-3, "seed {seed}: {i} and {j} overlap");
                }
            }
        }
    }
}
