//! Turning a ship's single hull model into breakable pieces, and the
//! pieces that fly off when a part breaks (CLAUDE.md section 11: "split
//! destructibles into natural components with separate meshes and
//! colliders"). The hull model arrives as one merged mesh, so at load each
//! triangle is sorted into the part whose slice holds it (see `damage.rs`);
//! each part gets its own mesh beside the hull's, sharing its materials.
//!
//! Serves pillar 4 (everything is physical) and fun rule 11 (emergent,
//! screenshot-worthy crashes).

use godot::classes::mesh::ArrayType;
use godot::classes::{ArrayMesh, BoxShape3D, CollisionShape3D, IRigidBody3D, MeshInstance3D, Node3D, RigidBody3D};
use godot::prelude::*;

use super::damage::{PartData, part_at};
use crate::orbits::body::{CelestialBody, GRAVITY_GROUP};
use crate::orbits::system::{FLOATING_GROUP, RIDER_GROUP};

/// Splits every mesh under `model` by part. Returns, per part, the new mesh
/// instances holding its triangles (siblings of the originals, same
/// transform and materials); the originals keep whatever is left.
pub fn split_by_part(model: &Gd<Node3D>, ship_from_model: Transform3D, parts: &[PartData]) -> Vec<Vec<Gd<MeshInstance3D>>> {
    let mut out: Vec<Vec<Gd<MeshInstance3D>>> = vec![Vec::new(); parts.len()];
    let meshes = model.find_children_ex("*").type_("MeshInstance3D").recursive(true).owned(false).done();
    for node in meshes.iter_shared() {
        let Ok(mut mi) = node.try_cast::<MeshInstance3D>() else { continue };
        let Some(mesh) = mi.get_mesh().and_then(|m| m.try_cast::<ArrayMesh>().ok()) else { continue };
        let to_ship = ship_from_model * relative_to(&mi, model);
        // Per part (index parts.len() = the hull), per surface: kept indices.
        let surfaces = mesh.get_surface_count();
        let mut picks: Vec<Vec<PackedInt32Array>> = vec![vec![PackedInt32Array::new(); surfaces as usize]; parts.len() + 1];
        let mut any = false;
        for s in 0..surfaces {
            let arrays = mesh.surface_get_arrays(s);
            let verts: PackedVector3Array = arrays.at(ArrayType::VERTEX.ord() as usize).to();
            let index_v = arrays.at(ArrayType::INDEX.ord() as usize);
            let index: PackedInt32Array = if index_v.is_nil() { (0..verts.len() as i32).collect() } else { index_v.to() };
            let idx = index.as_slice();
            let v = verts.as_slice();
            for tri in idx.chunks_exact(3) {
                let c = (v[tri[0] as usize] + v[tri[1] as usize] + v[tri[2] as usize]) / 3.0;
                let slot = part_at(parts, to_ship * c).unwrap_or(parts.len());
                if slot < parts.len() {
                    any = true;
                }
                picks[slot][s as usize].extend_array(&PackedInt32Array::from(tri));
            }
        }
        if !any {
            continue;
        }
        let Some(mut parent) = mi.get_parent() else { continue };
        let transform = mi.get_transform();
        for (slot, per_surface) in picks.iter().enumerate() {
            let mut built = ArrayMesh::new_gd();
            let mut materials = Vec::new();
            for (s, indices) in per_surface.iter().enumerate() {
                if indices.is_empty() {
                    continue;
                }
                let mut arrays = mesh.surface_get_arrays(s as i32);
                arrays.set(ArrayType::INDEX.ord() as usize, &indices.to_variant());
                built.add_surface_from_arrays(mesh.surface_get_primitive_type(s as i32), &arrays);
                let mat = mi.get_surface_override_material(s as i32).or_else(|| mesh.surface_get_material(s as i32));
                materials.push(mat);
            }
            if slot == parts.len() {
                // The hull keeps the rest.
                mi.set_mesh(&built);
                for (i, m) in materials.iter().enumerate() {
                    if let Some(m) = m {
                        mi.set_surface_override_material(i as i32, m);
                    }
                }
                continue;
            }
            if built.get_surface_count() == 0 {
                continue;
            }
            let mut piece = MeshInstance3D::new_alloc();
            piece.set_name(&format!("Part_{}", parts[slot].id));
            piece.set_mesh(&built);
            piece.set_transform(transform);
            for (i, m) in materials.iter().enumerate() {
                if let Some(m) = m {
                    piece.set_surface_override_material(i as i32, m);
                }
            }
            parent.add_child(&piece);
            out[slot].push(piece);
        }
    }
    out
}

/// `node`'s transform relative to `ancestor`.
fn relative_to(node: &Gd<MeshInstance3D>, ancestor: &Gd<Node3D>) -> Transform3D {
    let mut rel = node.get_transform();
    let mut parent = node.get_parent_node_3d();
    while let Some(p) = parent {
        if p == *ancestor {
            break;
        }
        rel = p.get_transform() * rel;
        parent = p.get_parent_node_3d();
    }
    rel
}

/// A broken-off piece: a rigid body under every body's gravity that rides
/// the solar system like ships do, so it tumbles, lands and stays put.
#[derive(GodotClass)]
#[class(base = RigidBody3D)]
pub struct ShipDebris {
    base: Base<RigidBody3D>,
}

#[godot_api]
impl IRigidBody3D for ShipDebris {
    fn init(base: Base<RigidBody3D>) -> Self {
        Self { base }
    }

    fn ready(&mut self) {
        let mut b = self.base_mut();
        b.add_to_group(RIDER_GROUP);
        b.add_to_group(FLOATING_GROUP);
        b.add_to_group(DEBRIS_GROUP);
        b.set_gravity_scale(0.0);
    }

    fn physics_process(&mut self, delta: f64) {
        let here = self.base().get_global_position();
        let g = self
            .base()
            .get_tree()
            .get_nodes_in_group(GRAVITY_GROUP)
            .iter_shared()
            .filter_map(|n| n.try_cast::<CelestialBody>().ok())
            .fold(Vector3::ZERO, |a, b| a + b.bind().gravity_at(here));
        let v = self.base().get_linear_velocity() + g * delta as f32;
        self.base_mut().set_linear_velocity(v);
    }
}

pub const DEBRIS_GROUP: &str = "ship_debris";

/// Moves a part's meshes into a new debris body flung away from the ship.
/// `centre` is the part's centre and `ship` the ship's transform; the piece
/// keeps the ship's velocity plus a kick outward.
#[allow(clippy::too_many_arguments)]
pub fn break_off(
    world: &mut Gd<Node>,
    meshes: &[Gd<MeshInstance3D>],
    ship: Transform3D,
    centre: Vector3,
    half_size: Vector3,
    velocity: Vector3,
    spin: Vector3,
    kick: f32,
    mass: f32,
    name: &str,
    collision_layer: u32,
    collision_mask: u32,
) -> Gd<RigidBody3D> {
    let mut body = ShipDebris::new_alloc();
    body.set_name(name);
    let origin = ship * centre;
    body.set_mass(mass.max(0.1));
    body.set_collision_layer(collision_layer);
    body.set_collision_mask(collision_mask);
    world.add_child(&body);
    body.set_global_transform(Transform3D::new(ship.basis, origin));
    for m in meshes {
        let mut m = m.clone();
        let t = m.get_global_transform();
        if let Some(mut p) = m.get_parent() {
            p.remove_child(&m);
        }
        body.add_child(&m);
        m.set_global_transform(t);
    }
    let mut shape = BoxShape3D::new_gd();
    shape.set_size(half_size * 2.0);
    let mut col = CollisionShape3D::new_alloc();
    col.set_shape(&shape);
    body.add_child(&col);
    let out = (origin - ship.origin).normalized_or_zero();
    body.set_linear_velocity(velocity + out * kick);
    body.set_angular_velocity(spin + out.cross(ship.basis.col_b()) * kick * 0.5);
    body.upcast()
}
