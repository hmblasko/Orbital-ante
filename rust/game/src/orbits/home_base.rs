//! The home base on Earth (CLAUDE.md sections 2, 5 and 6): where every run
//! starts. A raised launch pad with the lander on it beside the NASA launch
//! gantry, a hangar, a control tower, crew habs, the locker building, fuel
//! tanks, a tracking dish, and lit paths between them. Built round the
//! player's start spot in the body's own frame, so it turns with Earth.
//! Layout and colours live in data/bodies/home_base.ron.
//!
//! Every piece sinks to the lowest ground under it so nothing floats on
//! the hills; the pad's plinth reaches down to the ground on every side.
//!
//! Serves pillar 1 (a living place you launch from and come back to) and
//! fun rule 5 (flow: the first minute is a short, clear walk from the base
//! to the lander on the pad).

use godot::classes::light_3d::Param;
use godot::classes::{
    BoxShape3D, CollisionShape3D, CylinderShape3D, Label3D, MeshInstance3D, Node3D, OmniLight3D, StaticBody3D,
};
use godot::prelude::*;
use serde::Deserialize;

use super::body::basis_with_up;
use super::landmarks::{grime, instantiate, paint_glow, paint_lit};
use super::CelestialBody;
use crate::flight::meshes::{Geo, rounded_box, rounded_disc, swept_tube, torus};

const HOME_PATH: &str = "res://data/bodies/home_base.ron";
const HOME_EMBEDDED: &str = include_str!("../../../../godot/data/bodies/home_base.ron");
/// The landing spot on the pad (a marker in this group): ships park here.
pub const PAD_GROUP: &str = "launch_pads";
/// Every home-base piece, for tests.
pub const HOME_GROUP: &str = "home_base";

#[derive(Debug, Clone, Deserialize)]
pub struct HomeData {
    pub body: String,
    pub colors: HomeColors,
    pub pad: PadData,
    pub gantry: ModelPlace,
    pub buildings: Vec<Building>,
    pub dish: ModelPlace,
    pub paths: Vec<PathData>,
    pub lamp_every_m: f32,
    pub lamp_height_m: f32,
    pub sink_m: f32,
    /// Ground graded flat round the start (inner radius, blend-out radius).
    pub flat_inner_m: f32,
    pub flat_outer_m: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct HomeColors {
    pub wall: (f32, f32, f32),
    pub trim: (f32, f32, f32),
    pub dark: (f32, f32, f32),
    pub concrete: (f32, f32, f32),
    pub stripe: (f32, f32, f32),
    pub glass: (f32, f32, f32),
    pub lamp: (f32, f32, f32),
    pub steel: (f32, f32, f32),
}

#[derive(Debug, Clone, Deserialize)]
pub struct PadData {
    pub at: (f32, f32),
    pub radius_m: f32,
    pub lip_m: f32,
    pub lamps: u32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct ModelPlace {
    pub model: String,
    pub at: (f32, f32),
    pub yaw_deg: f32,
    pub scale: f32,
    pub sink_m: f32,
}

#[derive(Debug, Clone, Copy, Deserialize, PartialEq)]
pub enum BuildingKind {
    Hangar,
    ControlTower,
    Hab,
    Locker,
    FuelTank,
}

#[derive(Debug, Clone, Deserialize)]
pub struct Building {
    pub kind: BuildingKind,
    pub at: (f32, f32),
    pub size: (f32, f32, f32),
    pub yaw_deg: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct PathData {
    pub from: (f32, f32),
    pub to: (f32, f32),
    pub width_m: f32,
}

pub fn load_home() -> HomeData {
    crate::data::load(HOME_PATH, HOME_EMBEDDED)
}

/// The ground frame round the start spot, in the body's local space.
struct Frame {
    up: Vector3,
    x: Vector3,
    z: Vector3,
    r: f32,
}

impl Frame {
    /// The direction (from the body's centre) under ground point (x, z).
    fn dir(&self, x: f32, z: f32) -> Vector3 {
        (self.up * self.r + self.x * x + self.z * z).normalized()
    }
}

/// Lowest and highest ground within `radius` of (x, z) (9 samples).
fn ground_span(body: &Gd<CelestialBody>, f: &Frame, x: f32, z: f32, radius: f32) -> (f32, f32) {
    let b = body.bind();
    let mut lo = f32::MAX;
    let mut hi = f32::MIN;
    let mut sample = |dx: f32, dz: f32| {
        let g = b.surface_radius_local(f.dir(x + dx, z + dz));
        lo = lo.min(g);
        hi = hi.max(g);
    };
    sample(0.0, 0.0);
    for k in 0..8 {
        let a = k as f32 * std::f32::consts::FRAC_PI_4;
        sample(a.cos() * radius, a.sin() * radius);
    }
    (lo, hi)
}

/// A transform standing on the ground at (x, z), turned by `yaw_deg`, at
/// radius `height` from the body's centre.
fn stand(f: &Frame, x: f32, z: f32, yaw_deg: f32, height: f32) -> Transform3D {
    let dir = f.dir(x, z);
    // Keep "forward" (+Z) pointing along the frame's z everywhere.
    let fwd = (f.z - dir * f.z.dot(dir)).normalized();
    let right = dir.cross(fwd).normalized();
    let base = Basis::from_cols(-right, dir, fwd).orthonormalized();
    let _ = basis_with_up;
    Transform3D::new(base * Basis::from_axis_angle(Vector3::UP, yaw_deg.to_radians()), dir * height)
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

fn tube(a: Vector3, b: Vector3, r: f32, sides: usize) -> Geo {
    let refv = if (b - a).normalized().dot(Vector3::UP).abs() > 0.9 { Vector3::RIGHT } else { Vector3::UP };
    swept_tube(&[a, b], &[refv, refv], r, sides, false)
}

fn solid_box(name: &str, size: Vector3, centre: Vector3) -> Gd<StaticBody3D> {
    let mut s = StaticBody3D::new_alloc();
    s.set_name(name);
    s.add_to_group(HOME_GROUP);
    let mut col = CollisionShape3D::new_alloc();
    let mut bx = BoxShape3D::new_gd();
    bx.set_size(size);
    col.set_shape(&bx);
    col.set_position(centre);
    s.add_child(&col);
    s
}

fn lamp_light(c: (f32, f32, f32), energy: f32, range: f32) -> Gd<OmniLight3D> {
    let mut l = OmniLight3D::new_alloc();
    l.set_color(Color::from_rgb(c.0, c.1, c.2));
    l.set_param(Param::ENERGY, energy);
    l.set_param(Param::RANGE, range);
    l
}

/// A curved-roof hangar: soft vaulted shell, a teal band, a big dark door
/// with a glowing header light, roof vents, on a concrete apron.
fn hangar(c: &HomeColors, w: f32, h: f32, d: f32, foot: f32) -> Gd<StaticBody3D> {
    let mut s = solid_box("Hangar", Vector3::new(w, h + foot, d), Vector3::new(0.0, (h - foot) * 0.5, 0.0));
    let mut root = s.clone().upcast::<Node3D>();
    let r = (h * 0.75).min(w * 0.45);
    mesh(&mut root, "Apron", one(rounded_box(Vector3::new(w + 1.2, foot + 0.5, d + 3.0), 0.2, 3), at(0.0, 0.25 - foot * 0.5, 1.0)), paint_lit(c.concrete, 0.05));
    mesh(&mut root, "Shell", one(rounded_box(Vector3::new(w, h * 1.4, d), r, 8), at(0.0, h * 0.3, 0.0)), paint_lit(c.wall, 0.15));
    let mut trim = Geo::default();
    trim.append(&rounded_box(Vector3::new(w + 0.1, 0.45, d + 0.1), 0.2, 3), at(0.0, h * 0.42, 0.0));
    for z in [-d * 0.3, 0.0, d * 0.3] {
        trim.append(&rounded_box(Vector3::new(w * 0.86, h * 0.2, 0.35), 0.15, 3), at(0.0, h * 0.95, z));
    }
    mesh(&mut root, "Bands", trim, paint_lit(c.trim, 0.3));
    let door_w = w * 0.62;
    let door_h = h * 0.7;
    mesh(&mut root, "Door", one(rounded_box(Vector3::new(door_w, door_h, 0.3), 0.12, 3), at(0.0, door_h * 0.5, d * 0.5)), paint_lit(c.dark, 0.5));
    let mut frame = Geo::default();
    for x in [-door_w * 0.5 - 0.25, door_w * 0.5 + 0.25] {
        frame.append(&rounded_box(Vector3::new(0.45, door_h + 0.4, 0.5), 0.12, 3), at(x, (door_h + 0.4) * 0.5, d * 0.5));
    }
    frame.append(&rounded_box(Vector3::new(door_w + 0.95, 0.45, 0.5), 0.12, 3), at(0.0, door_h + 0.3, d * 0.5));
    mesh(&mut root, "DoorFrame", frame, paint_lit(c.trim, 0.3));
    mesh(&mut root, "DoorLight", one(rounded_box(Vector3::new(door_w * 0.8, 0.14, 0.14), 0.06, 2), at(0.0, door_h + 0.65, d * 0.5 + 0.2)), paint_glow(c.lamp, 2.2));
    let mut light = lamp_light(c.lamp, 2.0, 10.0);
    light.set_position(Vector3::new(0.0, door_h + 0.4, d * 0.5 + 1.2));
    root.add_child(&light);
    s.add_to_group(HOME_GROUP);
    s
}

/// The control tower: a round shaft, a soft cab ringed with glowing
/// windows, a roof disc and an antenna with a blinking lamp.
fn control_tower(c: &HomeColors, w: f32, h: f32, foot: f32) -> Gd<StaticBody3D> {
    let s = solid_box("ControlTower", Vector3::new(w * 0.7, h + foot, w * 0.7), Vector3::new(0.0, (h - foot) * 0.5, 0.0));
    let mut root = s.clone().upcast::<Node3D>();
    let shaft_r = w * 0.3;
    mesh(&mut root, "Footing", one(rounded_box(Vector3::new(w * 1.1, foot + 0.6, w * 1.1), 0.25, 3), at(0.0, 0.3 - foot * 0.5, 0.0)), paint_lit(c.concrete, 0.05));
    let mut shaft = tube(Vector3::new(0.0, 0.0, 0.0), Vector3::new(0.0, h * 0.74, 0.0), shaft_r, 24);
    for y in [h * 0.25, h * 0.5] {
        shaft.append(&torus(shaft_r + 0.02, 0.08, 24, 6), at(0.0, y, 0.0));
    }
    mesh(&mut root, "Shaft", shaft, paint_lit(c.wall, 0.15));
    let cab_y = h * 0.82;
    mesh(&mut root, "Cab", one(rounded_box(Vector3::new(w, h * 0.24, w), w * 0.3, 6), at(0.0, cab_y, 0.0)), paint_lit(c.wall, 0.15));
    mesh(&mut root, "Windows", one(rounded_box(Vector3::new(w + 0.08, h * 0.1, w + 0.08), w * 0.3, 6), at(0.0, cab_y + h * 0.02, 0.0)), paint_glow(c.glass, 0.9));
    mesh(&mut root, "Roof", one(rounded_disc(w * 0.62, 0.35, 28), at(0.0, cab_y + h * 0.13, 0.0)), paint_lit(c.trim, 0.3));
    mesh(&mut root, "Antenna", tube(Vector3::new(0.0, cab_y + h * 0.13, 0.0), Vector3::new(0.0, h + 2.0, 0.0), 0.07, 8), paint_lit(c.dark, 0.7));
    let mut blink = crate::ai::base::BlinkLamp::new_alloc();
    blink.set_position(Vector3::new(0.0, h + 2.1, 0.0));
    let mut b = blink.clone().upcast::<Node3D>();
    mesh(&mut b, "Glow", rounded_box(Vector3::splat(0.2), 0.09, 3), paint_glow((1.0, 0.3, 0.2), 3.0));
    b.add_child(&lamp_light((1.0, 0.3, 0.2), 1.2, 7.0));
    root.add_child(&blink);
    let mut light = lamp_light(c.glass, 1.4, 9.0);
    light.set_position(Vector3::new(0.0, cab_y, 0.0));
    root.add_child(&light);
    s
}

/// A crew hab: a soft dome with a teal skirt, round portholes and a door.
fn hab(c: &HomeColors, w: f32, h: f32, d: f32, foot: f32) -> Gd<StaticBody3D> {
    let s = solid_box("Hab", Vector3::new(w * 0.85, h + foot, d * 0.85), Vector3::new(0.0, (h - foot) * 0.5, 0.0));
    let mut root = s.clone().upcast::<Node3D>();
    let r = w.min(d) * 0.48;
    mesh(&mut root, "Dome", one(rounded_box(Vector3::new(w, h * 2.0 + foot, d), r, 10), at(0.0, -foot * 0.5, 0.0)), paint_lit(c.wall, 0.15));
    mesh(&mut root, "Skirt", one(rounded_box(Vector3::new(w + 0.2, 0.6, d + 0.2), r, 8), at(0.0, 0.3, 0.0)), paint_lit(c.trim, 0.3));
    let mut rims = Geo::default();
    let mut glass = Geo::default();
    for k in 0..3 {
        let a = std::f32::consts::FRAC_PI_2 + (k as f32 - 1.0) * 1.1;
        let p = Vector3::new(a.cos() * w * 0.47, h * 0.55, a.sin() * d * 0.47);
        let n = Vector3::new(a.cos(), 0.0, a.sin());
        let x = Vector3::UP.cross(n).normalized();
        let face = Basis::from_cols(x, n, x.cross(n));
        rims.append(&torus(0.38, 0.07, 20, 8), Transform3D::new(face, p));
        glass.append(&rounded_disc(0.34, 0.05, 20), Transform3D::new(face, p));
    }
    mesh(&mut root, "PortholeRims", rims, paint_lit(c.dark, 0.5));
    mesh(&mut root, "PortholeGlass", glass, paint_glow(c.lamp, 1.2));
    mesh(&mut root, "Door", one(rounded_box(Vector3::new(1.2, 2.1, 0.5), 0.15, 3), at(0.0, 1.05, -d * 0.47)), paint_lit(c.dark, 0.5));
    s
}

/// The locker building: where loot counts once delivered (section 6).
fn locker(c: &HomeColors, w: f32, h: f32, d: f32, foot: f32) -> Gd<StaticBody3D> {
    let s = solid_box("Locker", Vector3::new(w, h + foot, d), Vector3::new(0.0, (h - foot) * 0.5, 0.0));
    let mut root = s.clone().upcast::<Node3D>();
    mesh(&mut root, "Walls", one(rounded_box(Vector3::new(w, h + foot, d), 0.5, 4), at(0.0, (h - foot) * 0.5, 0.0)), paint_lit(c.wall, 0.15));
    mesh(&mut root, "Roof", one(rounded_box(Vector3::new(w + 0.5, 0.45, d + 0.5), 0.2, 3), at(0.0, h + 0.1, 0.0)), paint_lit(c.trim, 0.3));
    mesh(&mut root, "Door", one(rounded_box(Vector3::new(1.5, 2.3, 0.3), 0.12, 3), at(0.0, 1.15, d * 0.5)), paint_lit(c.dark, 0.5));
    mesh(&mut root, "Sign", one(rounded_box(Vector3::new(w * 0.6, 0.7, 0.16), 0.1, 3), at(0.0, h - 0.6, d * 0.5 + 0.05)), paint_glow(c.stripe, 0.8));
    let mut label = Label3D::new_alloc();
    label.set_text(&GString::from("BASE_LOCKER"));
    label.set_font_size(96);
    label.set_pixel_size(0.004);
    label.set_modulate(Color::from_rgb(0.12, 0.12, 0.14));
    label.set_outline_size(0);
    label.set_position(Vector3::new(0.0, h - 0.6, d * 0.5 + 0.15));
    root.add_child(&label);
    let mut light = lamp_light(c.lamp, 1.2, 6.0);
    light.set_position(Vector3::new(0.0, 2.6, d * 0.5 + 0.8));
    root.add_child(&light);
    s
}

/// A fuel tank: a white pill on dark legs with teal bands.
fn fuel_tank(c: &HomeColors, w: f32, h: f32, foot: f32) -> Gd<StaticBody3D> {
    let s = solid_box("FuelTank", Vector3::new(w, h + foot, w), Vector3::new(0.0, (h - foot) * 0.5, 0.0));
    let mut root = s.clone().upcast::<Node3D>();
    let lift = 0.9;
    mesh(&mut root, "Tank", one(rounded_box(Vector3::new(w, h - lift, w), w * 0.48, 8), at(0.0, lift + (h - lift) * 0.5, 0.0)), paint_lit(c.wall, 0.35));
    let mut bands = Geo::default();
    for y in [lift + (h - lift) * 0.3, lift + (h - lift) * 0.7] {
        bands.append(&torus(w * 0.5 + 0.02, 0.09, 24, 6), at(0.0, y, 0.0));
    }
    mesh(&mut root, "Bands", bands, paint_lit(c.trim, 0.3));
    let mut legs = Geo::default();
    for k in 0..4 {
        let a = k as f32 * std::f32::consts::FRAC_PI_2 + 0.785;
        let p = Vector3::new(a.cos() * w * 0.36, 0.0, a.sin() * w * 0.36);
        legs.append(&tube(p - Vector3::new(0.0, foot, 0.0), p + Vector3::new(0.0, lift + 0.4, 0.0), 0.1, 8), Transform3D::IDENTITY);
    }
    mesh(&mut root, "Legs", legs, paint_lit(c.dark, 0.6));
    s
}

/// A lamp post (pole, arm, glowing head and a warm light).
fn lamp_post(c: &HomeColors, height: f32, foot: f32) -> Gd<Node3D> {
    let mut root = Node3D::new_alloc();
    root.set_name("LampPost");
    root.add_to_group(HOME_GROUP);
    let mut pole = tube(Vector3::new(0.0, -foot, 0.0), Vector3::new(0.0, height, 0.0), 0.07, 10);
    pole.append(&tube(Vector3::new(0.0, height, 0.0), Vector3::new(0.55, height + 0.15, 0.0), 0.05, 8), Transform3D::IDENTITY);
    mesh(&mut root, "Pole", pole, paint_lit(c.dark, 0.6));
    mesh(&mut root, "Head", one(rounded_box(Vector3::new(0.42, 0.14, 0.26), 0.06, 3), at(0.6, height + 0.08, 0.0)), paint_glow(c.lamp, 2.5));
    let mut light = lamp_light(c.lamp, 1.1, 9.0);
    light.set_position(Vector3::new(0.6, height - 0.3, 0.0));
    root.add_child(&light);
    root
}

/// Build the home base round the player's start on `body`. `start_dir` is
/// the start direction in the body's local space, `facing` the player's
/// look direction (local). Returns the pad's top centre (local) so the
/// player can be turned toward it.
pub fn build(body: &mut Gd<CelestialBody>, start_dir: Vector3, facing: Vector3) -> Option<Vector3> {
    let data = load_home();
    if body.get_name().to_string() != data.body {
        return None;
    }
    let up = start_dir.normalized();
    let fwd = (facing - up * facing.dot(up)).normalized_or_zero();
    let fwd = if fwd.length_squared() < 0.5 { basis_with_up(up).col_c() } else { fwd };
    let x = fwd.cross(up).normalized();
    let r = body.bind().surface_radius_local(up);
    let f = Frame { up, x, z: fwd, r };
    let c = &data.colors;
    let mut root = Node3D::new_alloc();
    root.set_name("HomeBase");
    root.add_to_group(HOME_GROUP);

    // The launch pad.
    let pad = &data.pad;
    let (lo, hi) = ground_span(body, &f, pad.at.0, pad.at.1, pad.radius_m);
    let top = hi + pad.lip_m;
    let depth = top - lo + 1.0;
    let mut pad_body = StaticBody3D::new_alloc();
    pad_body.set_name("LaunchPad");
    pad_body.add_to_group(HOME_GROUP);
    pad_body.set_transform(stand(&f, pad.at.0, pad.at.1, 0.0, top));
    {
        let mut col = CollisionShape3D::new_alloc();
        let mut cyl = CylinderShape3D::new_gd();
        cyl.set_radius(pad.radius_m);
        cyl.set_height(depth);
        col.set_shape(&cyl);
        col.set_position(Vector3::new(0.0, -depth * 0.5, 0.0));
        pad_body.add_child(&col);
        let mut n = pad_body.clone().upcast::<Node3D>();
        mesh(&mut n, "Plinth", one(rounded_disc(pad.radius_m, depth, 48), at(0.0, -depth * 0.5, 0.0)), paint_lit(c.concrete, 0.05));
        let mut stripes = Geo::default();
        stripes.append(&torus(pad.radius_m * 0.8, 0.16, 64, 6), at(0.0, 0.0, 0.0));
        stripes.append(&torus(pad.radius_m * 0.42, 0.12, 48, 6), at(0.0, 0.0, 0.0));
        mesh(&mut n, "Stripes", stripes, paint_lit(c.stripe, 0.1));
        mesh(&mut n, "Rim", one(torus(pad.radius_m - 0.05, 0.22, 64, 8), at(0.0, -0.05, 0.0)), paint_lit(c.dark, 0.4));
        let mut lamps = Geo::default();
        for k in 0..pad.lamps {
            let a = k as f32 / pad.lamps.max(1) as f32 * std::f32::consts::TAU;
            lamps.append(&rounded_box(Vector3::new(0.3, 0.14, 0.3), 0.06, 2), at(a.cos() * (pad.radius_m - 0.6), 0.05, a.sin() * (pad.radius_m - 0.6)));
        }
        mesh(&mut n, "EdgeLamps", lamps, paint_glow(c.trim, 2.2));
        for k in 0..4 {
            let a = k as f32 * std::f32::consts::FRAC_PI_2 + 0.4;
            let mut l = lamp_light(c.trim, 1.0, 8.0);
            l.set_position(Vector3::new(a.cos() * pad.radius_m * 0.9, 0.6, a.sin() * pad.radius_m * 0.9));
            n.add_child(&l);
        }
        let mut spot = Node3D::new_alloc();
        spot.set_name("LandingSpot");
        spot.add_to_group(PAD_GROUP);
        n.add_child(&spot);
    }
    root.add_child(&pad_body);
    let pad_top = f.dir(pad.at.0, pad.at.1) * top;

    // The gantry, painted service-tower steel, with exact (trimesh) collision.
    let g = &data.gantry;
    if let Some(mut model) = instantiate(&g.model, "Gantry") {
        let (lo, _) = ground_span(body, &f, g.at.0, g.at.1, 6.0);
        let mut solid = StaticBody3D::new_alloc();
        solid.set_name("Gantry");
        solid.add_to_group(HOME_GROUP);
        solid.set_transform(stand(&f, g.at.0, g.at.1, g.yaw_deg, lo - g.sink_m + data.sink_m));
        model.set_scale(Vector3::splat(g.scale));
        super::landmarks::tint(&model, c.steel);
        solid.add_child(&model);
        for part in model.find_children_ex("*").type_("MeshInstance3D").recursive(true).owned(false).done().iter_shared() {
            let Ok(mi) = part.try_cast::<MeshInstance3D>() else { continue };
            let Some(m) = mi.get_mesh() else { continue };
            let Some(shape) = m.create_trimesh_shape() else { continue };
            let mut col = CollisionShape3D::new_alloc();
            col.set_shape(&shape);
            col.set_transform(model.get_transform() * mi.get_transform());
            solid.add_child(&col);
        }
        root.add_child(&solid);
    }

    // Buildings.
    for (k, b) in data.buildings.iter().enumerate() {
        let (w, h, d) = b.size;
        let (lo, hi) = ground_span(body, &f, b.at.0, b.at.1, w.max(d) * 0.5);
        let foot = (hi - lo) + 0.6;
        let mut node = match b.kind {
            BuildingKind::Hangar => hangar(c, w, h, d, foot),
            BuildingKind::ControlTower => control_tower(c, w, h, foot),
            BuildingKind::Hab => hab(c, w, h, d, foot),
            BuildingKind::Locker => locker(c, w, h, d, foot),
            BuildingKind::FuelTank => fuel_tank(c, w, h, foot),
        };
        node.set_name(&format!("{:?}{k}", b.kind));
        // Stand on the highest ground under it; the footing reaches down.
        node.set_transform(stand(&f, b.at.0, b.at.1, b.yaw_deg, hi - data.sink_m));
        root.add_child(&node);
    }

    // The tracking dish.
    let dish = &data.dish;
    if let Some(mut model) = instantiate(&dish.model, "HomeDish") {
        let (lo, _) = ground_span(body, &f, dish.at.0, dish.at.1, 4.0);
        let mut solid = StaticBody3D::new_alloc();
        solid.set_name("HomeDish");
        solid.add_to_group(HOME_GROUP);
        solid.set_transform(stand(&f, dish.at.0, dish.at.1, dish.yaw_deg, lo - dish.sink_m));
        model.set_scale(Vector3::splat(dish.scale));
        grime(&model, 0.1);
        solid.add_child(&model);
        if let Some(m) = model.find_children_ex("*").type_("MeshInstance3D").recursive(true).owned(false).done().iter_shared().next().and_then(|n| n.try_cast::<MeshInstance3D>().ok())
            && let Some(mesh_res) = m.get_mesh()
            && let Some(shape) = mesh_res.create_convex_shape_ex().clean(true).simplify(true).done()
        {
            let mut col = CollisionShape3D::new_alloc();
            col.set_shape(&shape);
            col.set_transform(model.get_transform() * m.get_transform());
            solid.add_child(&col);
        }
        root.add_child(&solid);
    }

    // Paths in short slabs that follow the ground, with lamp posts.
    let mut since_lamp = data.lamp_every_m * 0.5;
    for (pi, p) in data.paths.iter().enumerate() {
        let a = Vector2::new(p.from.0, p.from.1);
        let b = Vector2::new(p.to.0, p.to.1);
        let len = a.distance_to(b);
        let steps = (len / 3.0).ceil().max(1.0) as usize;
        let along = (b - a).normalized();
        let yaw = (-along.x).atan2(along.y).to_degrees();
        let seg = len / steps as f32;
        let mut slabs = Geo::default();
        let mut holder = Node3D::new_alloc();
        holder.set_name(&format!("Path{pi}"));
        holder.add_to_group(HOME_GROUP);
        let mut lamps: Vec<(Vector2, f32)> = Vec::new();
        for s in 0..steps {
            let m = a + along * (seg * (s as f32 + 0.5));
            let (lo, hi) = ground_span(body, &f, m.x, m.y, seg * 0.5);
            // Top a little above the highest ground under the slab.
            let thick = 0.4 + (hi - lo);
            let t = stand(&f, m.x, m.y, yaw, hi + 0.12 - thick * 0.5);
            slabs.append(&rounded_box(Vector3::new(p.width_m, thick, seg + 0.05), 0.08, 2), t);
            since_lamp += seg;
            if since_lamp >= data.lamp_every_m {
                since_lamp = 0.0;
                let side = Vector2::new(along.y, -along.x) * (p.width_m * 0.5 + 0.5);
                lamps.push((m + side, lo));
            }
        }
        mesh(&mut holder, "Slabs", slabs, paint_lit(c.concrete, 0.05));
        root.add_child(&holder);
        for (q, lo) in lamps {
            let mut post = lamp_post(c, data.lamp_height_m, 0.4);
            post.set_transform(stand(&f, q.x, q.y, yaw + 90.0, lo));
            root.add_child(&post);
        }
    }

    body.add_child(&root);
    Some(pad_top)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn data_home_base_parses_and_is_medium() {
        let d: HomeData = crate::data::parse(HOME_EMBEDDED).expect("home_base.ron parses");
        assert_eq!(d.body, "earth");
        assert!(d.buildings.len() >= 6, "a medium base");
        assert!(d.buildings.iter().any(|b| b.kind == BuildingKind::Locker), "the locker is at home (section 6)");
        assert!(d.pad.radius_m >= 9.0, "the lander (about 15 m across the pads) fits on the pad");
        // Nothing overlaps the pad.
        let pad = Vector2::new(d.pad.at.0, d.pad.at.1);
        for b in &d.buildings {
            let c = Vector2::new(b.at.0, b.at.1);
            assert!(c.distance_to(pad) > d.pad.radius_m + b.size.0.max(b.size.2) * 0.5, "{:?} clears the pad", b.kind);
        }
    }
}
