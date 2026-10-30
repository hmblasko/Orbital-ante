//! Shared helpers for building PLACEHOLDER prop models out of rounded
//! parts with the painted shader. Better than bare primitives, but still
//! placeholders until the asset pipeline (section 11) delivers real models.

use godot::classes::{FastNoiseLite, Mesh, MeshInstance3D, NoiseTexture2D, Shader, ShaderMaterial};
use godot::prelude::*;

const PROP_SHADER: &str = "res://shaders/painted_prop.gdshader";

pub struct PaintSpec {
    pub albedo: Color,
    pub albedo_dark: Color,
    pub specular: f32,
    pub emission: Color,
    pub emission_energy: f32,
}

impl PaintSpec {
    pub fn matte(albedo: Color, dark: Color) -> Self {
        Self {
            albedo,
            albedo_dark: dark,
            specular: 0.0,
            emission: Color::BLACK,
            emission_energy: 0.0,
        }
    }

    pub fn metal(albedo: Color, dark: Color) -> Self {
        Self {
            specular: 0.6,
            ..Self::matte(albedo, dark)
        }
    }

    pub fn glow(color: Color, energy: f32) -> Self {
        Self {
            emission: color,
            emission_energy: energy,
            ..Self::matte(color, color)
        }
    }
}

pub fn paint(spec: &PaintSpec) -> Gd<ShaderMaterial> {
    let mut m = ShaderMaterial::new_gd();
    if let Ok(shader) = try_load::<Shader>(PROP_SHADER) {
        m.set_shader(&shader);
    }
    m.set_shader_parameter("albedo", &spec.albedo.to_variant());
    m.set_shader_parameter("albedo_dark", &spec.albedo_dark.to_variant());
    m.set_shader_parameter("specular", &spec.specular.to_variant());
    m.set_shader_parameter("emission", &spec.emission.to_variant());
    m.set_shader_parameter("emission_energy", &spec.emission_energy.to_variant());
    let mut noise = FastNoiseLite::new_gd();
    noise.set_frequency(0.06);
    let mut tex = NoiseTexture2D::new_gd();
    tex.set_width(256);
    tex.set_height(256);
    tex.set_seamless(true);
    tex.set_generate_mipmaps(true);
    tex.set_noise(&noise);
    m.set_shader_parameter("grain_noise", &tex.to_variant());
    m.set_shader_parameter("grain_strength", &0.18f32.to_variant());
    m
}

/// Add one part to `parent`.
pub fn part<M: Inherits<Mesh>>(
    parent: &mut Gd<Node3D>,
    name: &str,
    mesh: Gd<M>,
    material: &Gd<ShaderMaterial>,
    transform: Transform3D,
) {
    let mut instance = MeshInstance3D::new_alloc();
    instance.set_name(name);
    instance.set_mesh(&mesh.upcast::<Mesh>());
    instance.set_material_override(material);
    instance.set_transform(transform);
    parent.add_child(&instance);
}

pub fn at(x: f32, y: f32, z: f32) -> Transform3D {
    Transform3D::new(Basis::IDENTITY, Vector3::new(x, y, z))
}
