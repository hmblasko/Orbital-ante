//! Safety tether (CLAUDE.md section 6): clip on near an anchor and the line
//! stops you floating away, but never reels you in.
//! `TetherAnchor` marks where a tether can clip on (ships and stations later).

use godot::classes::base_material_3d::ShadingMode;
use godot::classes::mesh::PrimitiveType;
use godot::classes::{INode3D, ImmediateMesh, MeshInstance3D, Node3D, StandardMaterial3D};
use godot::prelude::*;

pub const TETHER_GROUP: &str = "tether_anchors";

#[derive(GodotClass)]
#[class(base = Node3D, init)]
pub struct TetherAnchor {
    base: Base<Node3D>,
}

#[godot_api]
impl INode3D for TetherAnchor {
    fn ready(&mut self) {
        self.base_mut().add_to_group(TETHER_GROUP);
    }
}

/// The visible line from anchor to player. PLACEHOLDER look: a flat line.
pub struct TetherLine {
    instance: Gd<MeshInstance3D>,
    mesh: Gd<ImmediateMesh>,
}

impl TetherLine {
    pub fn new(parent: &mut Gd<Node3D>) -> Self {
        let mesh = ImmediateMesh::new_gd();
        let mut material = StandardMaterial3D::new_gd();
        material.set_shading_mode(ShadingMode::UNSHADED);
        material.set_albedo(Color::from_rgb(1.0, 0.85, 0.3));
        let mut instance = MeshInstance3D::new_alloc();
        instance.set_name("TetherLine");
        instance.set_mesh(&mesh);
        instance.set_material_override(&material);
        instance.set_as_top_level(true);
        parent.add_child(&instance);
        Self { instance, mesh }
    }

    pub fn draw(&mut self, from: Option<(Vector3, Vector3)>) {
        self.mesh.clear_surfaces();
        let Some((a, b)) = from else {
            self.instance.set_visible(false);
            return;
        };
        self.instance.set_visible(true);
        self.instance.set_global_transform(Transform3D::IDENTITY);
        self.mesh.surface_begin(PrimitiveType::LINES);
        self.mesh.surface_add_vertex(a);
        self.mesh.surface_add_vertex(b);
        self.mesh.surface_end();
    }
}
