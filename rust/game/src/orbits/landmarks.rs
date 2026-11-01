//! `LandmarkSet`: real NASA hardware placed on and around a body, from a
//! RON file. Surface pieces (a parked lander, a tracking dish, a crashed
//! probe) stand on the terrain; orbiters (Hubble, TDRS, Voyager) circle
//! the body on inclined orbits and keep moving whether watched or not.
//!
//! Serves pillar 1 (a living clockwork system: satellites orbit) and
//! fun rule 4 (curiosity: recognisable hardware to fly to and find).
//! Models come from NASA 3D Resources; see docs/asset_licence_ledger.md.

use godot::classes::base_material_3d::TextureParam;
use godot::classes::{
    CollisionShape3D, INode3D, MeshInstance3D, Node3D, PackedScene, Shader, ShaderMaterial,
    StandardMaterial3D, StaticBody3D,
};
use godot::prelude::*;
use serde::Deserialize;

use super::CelestialBody;
use crate::player::supply::SupplyPoint;
use crate::player::tether::TetherAnchor;
use super::body::basis_with_up;

#[derive(Debug, Clone, Deserialize)]
pub struct LandmarkData {
    #[serde(default)]
    pub surface: Vec<SurfaceLandmark>,
    #[serde(default)]
    pub orbiters: Vec<Orbiter>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct SurfaceLandmark {
    pub name: String,
    pub model: String,
    /// Direction from the body's centre.
    pub at: (f32, f32, f32),
    pub scale: f32,
    pub yaw_deg: f32,
    /// Lean, for wrecks that came down hard.
    #[serde(default)]
    pub tilt_deg: f32,
    /// How far the model sinks into the ground, in metres.
    #[serde(default)]
    pub sink_m: f32,
    /// 0..1: sand settled on upward faces, for wrecks that have sat a while.
    #[serde(default)]
    pub grime: f32,
    /// Solid convex hull per part so the player can't walk through it.
    #[serde(default)]
    pub solid: bool,
    /// This is a ship the player refills air and fuel from, and can clip a
    /// tether to. The point sits this far up the model, in the model's units.
    #[serde(default)]
    pub supply_height: Option<f32>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct Orbiter {
    pub name: String,
    pub model: String,
    pub scale: f32,
    /// Distance from the body's centre, in metres.
    pub orbit_radius_m: f32,
    /// Seconds per full orbit.
    pub period_s: f32,
    pub inclination_deg: f32,
    /// Where the orbit crosses the equator (longitude of the ascending node).
    pub node_deg: f32,
    /// Starting angle along the orbit.
    pub phase_deg: f32,
    /// Slow tumble, degrees per second about each axis.
    #[serde(default)]
    pub spin_deg_s: (f32, f32, f32),
}

/// Position on a circular inclined orbit at `time` seconds (pure, for tests).
pub fn orbit_position(o: &Orbiter, time: f32) -> Vector3 {
    let angle = o.phase_deg.to_radians() + time / o.period_s.max(0.001) * std::f32::consts::TAU;
    let flat = Vector3::new(angle.cos(), 0.0, angle.sin()) * o.orbit_radius_m;
    let tilt = Basis::from_axis_angle(Vector3::RIGHT, o.inclination_deg.to_radians());
    let node = Basis::from_axis_angle(Vector3::UP, o.node_deg.to_radians());
    node * (tilt * flat)
}

#[derive(GodotClass)]
#[class(base = Node3D, init)]
pub struct LandmarkSet {
    #[export(file = "*.ron")]
    data_path: GString,
    data: Option<LandmarkData>,
    orbiter_nodes: Vec<Gd<Node3D>>,
    time: f32,
    base: Base<Node3D>,
}

#[godot_api]
impl INode3D for LandmarkSet {
    fn ready(&mut self) {
        let Some(data) = crate::data::try_load::<LandmarkData>(&self.data_path.to_string()) else {
            godot_error!("LandmarkSet: no data at {}", self.data_path);
            return;
        };
        for o in &data.orbiters {
            if let Some(model) = instantiate(&o.model, &o.name) {
                let mut holder = Node3D::new_alloc();
                holder.set_name(&o.name);
                let mut model = model;
                model.set_scale(Vector3::splat(o.scale));
                holder.add_child(&model);
                holder.set_position(orbit_position(o, 0.0));
                self.base_mut().add_child(&holder);
                self.orbiter_nodes.push(holder);
            }
        }
        self.data = Some(data);
        // The parent body builds its terrain after its children are ready.
        self.base_mut().call_deferred("place_on_surface", &[]);
    }

    fn physics_process(&mut self, delta: f64) {
        self.time += delta as f32;
        let Some(data) = &self.data else { return };
        let time = self.time;
        for (o, node) in data.orbiters.iter().zip(self.orbiter_nodes.iter_mut()) {
            node.set_position(orbit_position(o, time));
            let s = o.spin_deg_s;
            let spin = Vector3::new(s.0, s.1, s.2) * (delta as f32).to_radians();
            node.rotate_x(spin.x);
            node.rotate_y(spin.y);
            node.rotate_z(spin.z);
        }
    }
}

#[godot_api]
impl LandmarkSet {
    /// Set the data file before the set enters the tree (for spawning from code).
    pub fn configure(&mut self, data_path: &str) {
        self.data_path = GString::from(data_path);
    }

    /// Stand each surface landmark on the terrain under its direction.
    #[func]
    fn place_on_surface(&mut self) {
        // The scene may have been swapped out before this deferred call ran.
        if !self.base().is_inside_tree() {
            return;
        }
        let Some(body) = self
            .base()
            .get_parent()
            .and_then(|p| p.try_cast::<CelestialBody>().ok())
        else {
            godot_error!("LandmarkSet must be a child of a CelestialBody");
            return;
        };
        let Some(data) = self.data.clone() else {
            return;
        };
        // Placed in the body's own frame, so they turn and travel with it.
        for l in &data.surface {
            let Some(mut model) = instantiate(&l.model, &l.name) else {
                continue;
            };
            let dir = Vector3::new(l.at.0, l.at.1, l.at.2).normalized();
            let ground = body.bind().surface_radius_local(dir);
            let yaw = Basis::from_axis_angle(Vector3::UP, l.yaw_deg.to_radians());
            let tilt = Basis::from_axis_angle(Vector3::RIGHT, l.tilt_deg.to_radians());
            let basis = (basis_with_up(dir) * yaw * tilt).scaled(Vector3::splat(l.scale));
            let transform = Transform3D::new(basis, dir * (ground - l.sink_m));
            let mut holder = if l.solid {
                let mut solid = StaticBody3D::new_alloc();
                solid.set_name(&l.name);
                self.base_mut().add_child(&solid);
                solid.set_transform(transform);
                solid.add_child(&model);
                add_hulls(&mut solid, &model);
                solid.upcast::<Node3D>()
            } else {
                self.base_mut().add_child(&model);
                model.set_transform(transform);
                model
            };
            if l.grime > 0.0 {
                let dust = body.bind().dust_color();
                set_on_painted(&holder, "grime", &l.grime.to_variant());
                set_on_painted(&holder, "dust_color", &dust.to_variant());
                set_dust_up(&holder, body.get_global_basis() * dir);
            }
            if let Some(height) = l.supply_height {
                let mut supply = SupplyPoint::new_alloc();
                supply.set_name("SupplyPoint");
                supply.set_position(Vector3::new(0.0, height, 0.0));
                let mut anchor = TetherAnchor::new_alloc();
                anchor.set_name("TetherAnchor");
                supply.add_child(&anchor);
                holder.add_child(&supply);
            }
        }
    }
}

const MODEL_SHADER: &str = "res://shaders/painted_model.gdshader";

/// A painted, lit material (the shared TF2-style shader).
pub(crate) fn paint_lit(c: (f32, f32, f32), metal: f32) -> Gd<godot::classes::Material> {
    let mut m = ShaderMaterial::new_gd();
    if let Ok(shader) = try_load::<Shader>(MODEL_SHADER) {
        m.set_shader(&shader);
    }
    m.set_shader_parameter("albedo_color", &Color::from_rgb(c.0, c.1, c.2).to_variant());
    m.set_shader_parameter("metallic", &metal.to_variant());
    m.set_shader_parameter("rim_strength", &0.5f32.to_variant());
    m.upcast()
}

/// A self-lit glow (status lamps, sparks).
pub(crate) fn paint_glow(c: (f32, f32, f32), energy: f32) -> Gd<godot::classes::Material> {
    use godot::classes::base_material_3d::{EmissionOperator, Feature, ShadingMode};
    let mut m = StandardMaterial3D::new_gd();
    m.set_shading_mode(ShadingMode::UNSHADED);
    m.set_albedo(Color::from_rgb(c.0, c.1, c.2));
    m.set_feature(Feature::EMISSION, true);
    m.set_emission(Color::from_rgb(c.0, c.1, c.2));
    m.set_emission_energy_multiplier(energy);
    m.set_emission_operator(EmissionOperator::ADD);
    m.upcast()
}

/// Settle `amount` (0..1) of grime on a model's painted parts.
pub(crate) fn grime(model: &Gd<Node3D>, amount: f32) {
    if amount > 0.0 {
        set_on_painted(model, "grime", &amount.to_variant());
    }
}

pub(crate) fn instantiate(path: &str, name: &str) -> Option<Gd<Node3D>> {
    let scene = try_load::<PackedScene>(path).ok()?;
    let mut node = scene.instantiate()?.try_cast::<Node3D>().ok()?;
    node.set_name(&format!("{name}Model"));
    repaint(&node);
    Some(node)
}

/// Swap each part's imported material for the painted model shader,
/// keeping its colour texture, tint and metalness (section 11 TF2 rules).
fn repaint(model: &Gd<Node3D>) {
    let Ok(shader) = try_load::<Shader>(MODEL_SHADER) else { return };
    let parts = model.find_children_ex("*").type_("MeshInstance3D").recursive(true).owned(false).done();
    for part in parts.iter_shared() {
        let Ok(mut part) = part.try_cast::<MeshInstance3D>() else { continue };
        let Some(mesh) = part.get_mesh() else { continue };
        for i in 0..mesh.get_surface_count() {
            let Some(original) = mesh.surface_get_material(i).and_then(|m| m.try_cast::<StandardMaterial3D>().ok()) else {
                continue;
            };
            let mut painted = ShaderMaterial::new_gd();
            painted.set_shader(&shader);
            painted.set_shader_parameter("albedo_color", &original.get_albedo().to_variant());
            painted.set_shader_parameter("metallic", &original.get_metallic().to_variant());
            if let Some(texture) = original.get_texture(TextureParam::ALBEDO) {
                painted.set_shader_parameter("albedo_texture", &texture.to_variant());
                painted.set_shader_parameter("has_texture", &true.to_variant());
            }
            part.set_surface_override_material(i, &painted);
        }
    }
}

/// Set a parameter on every painted material of a model.
/// Tell each painted part which way is "up" for settled dust, in the part's
/// own space (so it stays right however the body turns).
fn set_dust_up(model: &Gd<Node3D>, world_up: Vector3) {
    let parts = model.find_children_ex("*").type_("MeshInstance3D").recursive(true).owned(false).done();
    for part in parts.iter_shared() {
        let Ok(part) = part.try_cast::<MeshInstance3D>() else { continue };
        let local_up = (part.get_global_basis().inverse() * world_up).normalized();
        for i in 0..part.get_surface_override_material_count() {
            if let Some(mut m) = part.get_surface_override_material(i).and_then(|m| m.try_cast::<ShaderMaterial>().ok()) {
                m.set_shader_parameter("dust_up", &local_up.to_variant());
            }
        }
    }
}

fn set_on_painted(model: &Gd<Node3D>, name: &str, value: &Variant) {
    let parts = model.find_children_ex("*").type_("MeshInstance3D").recursive(true).owned(false).done();
    for part in parts.iter_shared() {
        let Ok(part) = part.try_cast::<MeshInstance3D>() else { continue };
        for i in 0..part.get_surface_override_material_count() {
            if let Some(mut m) = part.get_surface_override_material(i).and_then(|m| m.try_cast::<ShaderMaterial>().ok()) {
                m.set_shader_parameter(name, value);
            }
        }
    }
}

/// One convex hull per mesh part, in the solid body's space.
pub(crate) fn add_hulls(solid: &mut Gd<StaticBody3D>, model: &Gd<Node3D>) {
    let parts = model
        .find_children_ex("*")
        .type_("MeshInstance3D")
        .recursive(true)
        .owned(false)
        .done();
    let inverse = solid.get_global_transform().affine_inverse();
    for part in parts.iter_shared() {
        let Ok(part) = part.try_cast::<MeshInstance3D>() else {
            continue;
        };
        let Some(mesh) = part.get_mesh() else {
            continue;
        };
        let Some(shape) = mesh
            .create_convex_shape_ex()
            .clean(true)
            .simplify(true)
            .done()
        else {
            continue;
        };
        let mut collision = CollisionShape3D::new_alloc();
        collision.set_shape(&shape);
        solid.add_child(&collision);
        collision.set_transform(inverse * part.get_global_transform());
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn orbiter() -> Orbiter {
        Orbiter {
            name: "Test".into(),
            model: String::new(),
            scale: 1.0,
            orbit_radius_m: 200.0,
            period_s: 100.0,
            inclination_deg: 30.0,
            node_deg: 40.0,
            phase_deg: 10.0,
            spin_deg_s: (0.0, 0.0, 0.0),
        }
    }

    #[test]
    fn determinism_orbits_are_circular_and_periodic() {
        let o = orbiter();
        for t in [0.0, 12.5, 61.0] {
            assert!((orbit_position(&o, t).length() - 200.0).abs() < 1e-2);
            assert!(orbit_position(&o, t).distance_to(orbit_position(&o, t + 100.0)) < 1e-2);
        }
        assert!(orbit_position(&o, 0.0).distance_to(orbit_position(&o, 25.0)) > 100.0);
    }
}
