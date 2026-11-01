//! Procedural weapon models: goofy spoofs with original designs (section 7:
//! no real brands, no other games' names). Built from bevelled, rounded
//! parts (section 11: no plain primitives, no hard 90-degree edges) and
//! painted with the shared TF2-style shader. The same models serve as the
//! first-person view model, the gun an enemy holds and a dropped pickup.
//!
//! Model space: metres, the muzzle points along -Z, the grip is at the
//! origin, +Y up.

use godot::classes::base_material_3d::{EmissionOperator, Feature, ShadingMode};
use godot::classes::{Material, MeshInstance3D, Node3D, Shader, ShaderMaterial, StandardMaterial3D};
use godot::prelude::*;

use crate::flight::meshes::{Geo, rounded_box, rounded_disc, swept_tube, torus};

const PAINTED: &str = "res://shaders/painted_model.gdshader";

#[derive(Clone, Copy)]
enum Paint {
    /// Painted, lit surface: colour, metalness.
    Lit((f32, f32, f32), f32),
    /// Self-lit glow (laser coils, power cells).
    Glow((f32, f32, f32), f32),
}

fn material(p: Paint) -> Gd<Material> {
    match p {
        Paint::Lit(c, metal) => {
            let mut m = ShaderMaterial::new_gd();
            if let Ok(shader) = try_load::<Shader>(PAINTED) {
                m.set_shader(&shader);
            }
            m.set_shader_parameter("albedo_color", &Color::from_rgb(c.0, c.1, c.2).to_variant());
            m.set_shader_parameter("metallic", &metal.to_variant());
            m.set_shader_parameter("rim_strength", &0.5f32.to_variant());
            m.upcast()
        }
        Paint::Glow(c, energy) => {
            let mut m = StandardMaterial3D::new_gd();
            m.set_shading_mode(ShadingMode::UNSHADED);
            m.set_albedo(Color::from_rgb(c.0, c.1, c.2));
            m.set_feature(Feature::EMISSION, true);
            m.set_emission(Color::from_rgb(c.0, c.1, c.2));
            m.set_emission_energy_multiplier(energy);
            m.set_emission_operator(EmissionOperator::ADD);
            m.upcast()
        }
    }
}

fn add(parent: &mut Gd<Node3D>, name: &str, g: Geo, paint: Paint) -> Gd<MeshInstance3D> {
    let mut mi = MeshInstance3D::new_alloc();
    mi.set_name(name);
    mi.set_mesh(&g.to_array_mesh());
    mi.set_surface_override_material(0, &material(paint));
    parent.add_child(&mi);
    mi
}

fn place(g: Geo, t: Transform3D) -> Geo {
    let mut out = Geo::default();
    out.append(&g, t);
    out
}

fn at(x: f32, y: f32, z: f32) -> Transform3D {
    Transform3D::new(Basis::IDENTITY, Vector3::new(x, y, z))
}

fn tilted(x_deg: f32, origin: Vector3) -> Transform3D {
    Transform3D::new(Basis::from_axis_angle(Vector3::RIGHT, x_deg.to_radians()), origin)
}

/// A straight tube along -Z from z0 to z1 (z1 < z0), with rounded caps.
fn barrel(radius: f32, z0: f32, z1: f32, x: f32, y: f32) -> Geo {
    let pts = [Vector3::new(x, y, z0), Vector3::new(x, y, z1)];
    let refs = [Vector3::UP, Vector3::UP];
    let mut g = swept_tube(&pts, &refs, radius, 14, false);
    // Caps: thin rounded discs facing along the barrel.
    let cap = rounded_disc(radius * 1.02, radius * 0.25, 14);
    let face = Basis::from_axis_angle(Vector3::RIGHT, std::f32::consts::FRAC_PI_2);
    g.append(&cap, Transform3D::new(face, Vector3::new(x, y, z0)));
    g.append(&cap, Transform3D::new(face, Vector3::new(x, y, z1)));
    g
}

const WOOD: Paint = Paint::Lit((0.55, 0.30, 0.16), 0.0);
const WOOD_DARK: Paint = Paint::Lit((0.38, 0.20, 0.11), 0.0);
const GUNMETAL: Paint = Paint::Lit((0.24, 0.25, 0.28), 0.7);
const STEEL: Paint = Paint::Lit((0.55, 0.57, 0.6), 0.8);
const BRASS: Paint = Paint::Lit((0.78, 0.6, 0.28), 0.8);

/// The rifle (AK spoof): a chunky receiver, banana magazine, wooden
/// furniture and a goofy teal cooling coil wrapped round the barrel.
fn rifle(root: &mut Gd<Node3D>) {
    add(root, "Receiver", place(rounded_box(Vector3::new(0.07, 0.09, 0.36), 0.02, 4), at(0.0, 0.03, -0.12)), GUNMETAL);
    add(root, "DustCover", place(rounded_box(Vector3::new(0.06, 0.03, 0.30), 0.012, 3), at(0.0, 0.085, -0.12)), STEEL);
    add(root, "Barrel", barrel(0.014, -0.28, -0.72, 0.0, 0.05), GUNMETAL);
    add(root, "GasTube", barrel(0.011, -0.28, -0.5, 0.0, 0.085), STEEL);
    add(root, "Handguard", place(rounded_box(Vector3::new(0.075, 0.07, 0.2), 0.025, 4), at(0.0, 0.045, -0.38)), WOOD);
    add(root, "FrontSight", place(rounded_box(Vector3::new(0.02, 0.06, 0.025), 0.008, 3), at(0.0, 0.09, -0.66)), GUNMETAL);
    // Stock, sloping down behind the grip.
    add(root, "Stock", place(rounded_box(Vector3::new(0.055, 0.1, 0.3), 0.025, 4), tilted(-8.0, Vector3::new(0.0, -0.005, 0.2))), WOOD);
    add(root, "Butt", place(rounded_box(Vector3::new(0.06, 0.14, 0.03), 0.014, 3), tilted(-8.0, Vector3::new(0.0, -0.03, 0.35))), WOOD_DARK);
    add(root, "Grip", place(rounded_box(Vector3::new(0.045, 0.12, 0.05), 0.018, 3), tilted(18.0, Vector3::new(0.0, -0.045, 0.0))), WOOD_DARK);
    // Banana magazine: a curved, flattened tube.
    let pts: Vec<Vector3> = (0..7)
        .map(|i| {
            let t = i as f32 / 6.0;
            Vector3::new(0.0, -0.01 - t * 0.2, -0.13 - (t * 1.6).sin() * 0.07)
        })
        .collect();
    let refs = vec![Vector3::RIGHT; pts.len()];
    let mag = swept_tube(&pts, &refs, 0.028, 10, false);
    let squash = Transform3D::new(Basis::from_scale(Vector3::new(0.7, 1.0, 1.45)), Vector3::new(0.0, 0.0, 0.06));
    add(root, "Magazine", place(mag, squash * at(0.0, 0.0, -0.04)), Paint::Lit((0.62, 0.32, 0.14), 0.2));
    // The cooling coil: three glowing rings round the barrel.
    for (i, z) in [-0.52, -0.57, -0.62].iter().enumerate() {
        let ring = torus(0.022, 0.006, 16, 8);
        let face = Basis::from_axis_angle(Vector3::RIGHT, std::f32::consts::FRAC_PI_2);
        add(root, &format!("Coil{i}"), place(ring, Transform3D::new(face, Vector3::new(0.0, 0.05, *z))), Paint::Glow((0.3, 1.0, 0.85), 2.0));
    }
}

/// The double-barrel laser shotgun: two fat barrels with glowing red
/// lenses, a brass power cell and a stubby wooden stock.
fn laser_shotgun(root: &mut Gd<Node3D>) {
    add(root, "Breech", place(rounded_box(Vector3::new(0.11, 0.08, 0.16), 0.025, 4), at(0.0, 0.035, -0.08)), GUNMETAL);
    add(root, "BarrelL", barrel(0.026, -0.14, -0.62, 0.027, 0.05), STEEL);
    add(root, "BarrelR", barrel(0.026, -0.14, -0.62, -0.027, 0.05), STEEL);
    add(root, "Rib", place(rounded_box(Vector3::new(0.02, 0.015, 0.46), 0.006, 3), at(0.0, 0.08, -0.38)), GUNMETAL);
    for (i, x) in [0.027f32, -0.027].iter().enumerate() {
        let lens = rounded_disc(0.02, 0.01, 16);
        let face = Basis::from_axis_angle(Vector3::RIGHT, std::f32::consts::FRAC_PI_2);
        add(root, &format!("Lens{i}"), place(lens, Transform3D::new(face, Vector3::new(*x, 0.05, -0.625))), Paint::Glow((1.0, 0.2, 0.3), 3.0));
        let ring = torus(0.03, 0.007, 18, 8);
        add(root, &format!("Ring{i}"), place(ring, Transform3D::new(face, Vector3::new(*x, 0.05, -0.45))), Paint::Glow((1.0, 0.35, 0.4), 1.6));
    }
    add(root, "Forend", place(rounded_box(Vector3::new(0.09, 0.05, 0.18), 0.022, 4), at(0.0, 0.0, -0.3)), WOOD);
    // Power cell under the breech.
    let cell = swept_tube(&[Vector3::new(0.0, -0.02, -0.02), Vector3::new(0.0, -0.02, -0.16)], &[Vector3::UP, Vector3::UP], 0.03, 14, false);
    add(root, "Cell", cell, BRASS);
    add(root, "CellGlow", place(torus(0.031, 0.006, 16, 8), Transform3D::new(Basis::from_axis_angle(Vector3::RIGHT, std::f32::consts::FRAC_PI_2), Vector3::new(0.0, -0.02, -0.09))), Paint::Glow((1.0, 0.45, 0.2), 2.0));
    add(root, "Grip", place(rounded_box(Vector3::new(0.045, 0.11, 0.05), 0.018, 3), tilted(20.0, Vector3::new(0.0, -0.04, 0.02))), WOOD_DARK);
    add(root, "Stock", place(rounded_box(Vector3::new(0.06, 0.11, 0.24), 0.028, 4), tilted(-10.0, Vector3::new(0.0, -0.01, 0.15))), WOOD);
}

/// The baseball bat: a lathe-turned taper with a taped handle.
fn bat(root: &mut Gd<Node3D>) {
    // Profile along -Z: knob, handle, then the barrel swelling out.
    let profile = [(0.0, 0.03), (0.02, 0.022), (0.05, 0.017), (0.25, 0.019), (0.45, 0.03), (0.62, 0.037), (0.74, 0.036)];
    let mut g = Geo::default();
    for w in profile.windows(2) {
        let (z0, r0) = w[0];
        let (z1, r1) = w[1];
        let seg = swept_tube(&[Vector3::new(0.0, 0.0, -z0), Vector3::new(0.0, 0.0, -z1)], &[Vector3::UP, Vector3::UP], (r0 + r1) * 0.5, 14, false);
        g.append(&seg, Transform3D::IDENTITY);
    }
    let cap = rounded_disc(0.036, 0.02, 14);
    g.append(&cap, Transform3D::new(Basis::from_axis_angle(Vector3::RIGHT, std::f32::consts::FRAC_PI_2), Vector3::new(0.0, 0.0, -0.74)));
    add(root, "Bat", place(g, at(0.0, 0.0, 0.08)), Paint::Lit((0.86, 0.68, 0.42), 0.0));
    let tape = swept_tube(&[Vector3::new(0.0, 0.0, 0.06), Vector3::new(0.0, 0.0, -0.14)], &[Vector3::UP, Vector3::UP], 0.021, 12, false);
    add(root, "Tape", tape, Paint::Lit((0.15, 0.15, 0.17), 0.0));
}

/// A palm-sized rock (also what you throw).
fn rock(root: &mut Gd<Node3D>) {
    let m = crate::procgen::rocks::rock_mesh(7, 2, 0.35, 0.8);
    let mut g = Geo::default();
    g.positions = m.positions.iter().map(|p| *p * 0.06).collect();
    g.normals = m.normals.clone();
    g.uvs = vec![Vector2::ZERO; g.positions.len()];
    g.indices = m.indices.clone();
    add(root, "Rock", g, Paint::Lit((0.52, 0.5, 0.47), 0.0));
}

/// The model for a weapon id, or None for an unknown id.
pub fn weapon_model(id: &str) -> Option<Gd<Node3D>> {
    let mut root = Node3D::new_alloc();
    root.set_name(&format!("Model_{id}"));
    match id {
        "rifle" => rifle(&mut root),
        "laser_shotgun" => laser_shotgun(&mut root),
        "bat" => bat(&mut root),
        "rock" => rock(&mut root),
        _ => {
            root.free();
            return None;
        }
    }
    Some(root)
}

/// Where the muzzle is in model space (beams start here).
pub fn muzzle(id: &str) -> Vector3 {
    match id {
        "rifle" => Vector3::new(0.0, 0.05, -0.73),
        "laser_shotgun" => Vector3::new(0.0, 0.05, -0.64),
        _ => Vector3::new(0.0, 0.0, -0.2),
    }
}

/// Rope coils for a tied-up target (section 9): fat, slightly tilted loops
/// so "tied up" reads at a glance. `loops` are (height, radius, tilt in
/// degrees) in the target's own space.
pub fn rope_coils(color: (f32, f32, f32), thickness: f32, loops: &[(f32, f32, f32)]) -> Gd<Node3D> {
    let mut root = Node3D::new_alloc();
    root.set_name("Rope");
    let mut g = Geo::default();
    for (i, (y, r, tilt)) in loops.iter().enumerate() {
        let side = if i % 2 == 0 { 1.0 } else { -1.0 };
        let t = Transform3D::new(Basis::from_axis_angle(Vector3::RIGHT, (tilt * side).to_radians()), Vector3::new(0.0, *y, 0.0));
        g.append(&torus(*r, thickness, 20, 8), t);
    }
    add(&mut root, "Coils", g, Paint::Lit(color, 0.0));
    root
}
