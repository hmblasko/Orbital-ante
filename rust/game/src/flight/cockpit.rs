//! The cockpit's working parts (CLAUDE.md section 5, "Cockpit and dash"):
//! the dash screen on the console, a small 3D model of the ship that lights
//! up the thrusters you're firing, and the landing and selfie-stick
//! cameras, which fill your view when you flick through them with Y.
//! Positions come from the ship's data file (`cockpit:`).
//! Serves pillar 4 (physical flight you can read) and fun rule 10 (game feel).

use godot::classes::base_material_3d::{EmissionOperator, Feature, ShadingMode, Transparency};
use godot::classes::sub_viewport::UpdateMode;
use godot::classes::{
    Camera3D, Control, CylinderMesh, Font, FontFile, IControl, MeshInstance3D, Node3D, QuadMesh, StandardMaterial3D,
    SubViewport,
};
use godot::global::HorizontalAlignment;
use godot::prelude::*;
use serde::Deserialize;

use super::dash::ShipDash;
use super::meshes;

type V3 = (f32, f32, f32);

fn v3((x, y, z): V3) -> Vector3 {
    Vector3::new(x, y, z)
}

#[derive(Debug, Clone, Deserialize)]
pub struct CockpitData {
    /// Dash screen size on the console, metres.
    pub screen_size_m: (f32, f32),
    /// Ship-space centre of the dash screen, which stands up on the back of
    /// the console facing the pilot...
    pub screen_at: V3,
    /// ...leaning back this far from vertical.
    pub screen_tilt_deg: f32,
    pub mini_ship: MiniShipData,
    pub cameras: Vec<CameraData>,
    pub camera_frame: CameraFrameData,
    pub target_marker: TargetMarkerData,
}

#[derive(Debug, Clone, Deserialize)]
pub struct MiniShipData {
    /// Where it stands, in console-top space (x = toward the pilot's left).
    pub at: V3,
    pub scale: f32,
    pub arrow_len_m: f32,
    pub arrow_color: V3,
    pub arrow_energy: f32,
    pub pedestal_radius_m: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct CameraData {
    pub name_key: String,
    /// Ship-space camera position and the point it looks at.
    pub at: V3,
    pub look_at: V3,
    /// Up direction for the view (landing camera looks straight down).
    pub up: V3,
    pub fov_deg: f32,
    /// Where the camera's pole meets the hull, if it has one.
    #[serde(default)]
    pub pole_from: Option<V3>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct CameraFrameData {
    pub border_px: f32,
    pub label_px: i32,
    pub color: (f32, f32, f32, f32),
    pub rec_color: (f32, f32, f32),
    pub hint_key: String,
}

/// The bracket drawn round the locked target in your view.
#[derive(Debug, Clone, Deserialize)]
pub struct TargetMarkerData {
    /// Half size of the brackets, pixels at 800 px screen height.
    pub size_px: f32,
    pub line_px: f32,
    pub label_px: i32,
    /// Distance kept from the screen edge when the target is off-screen.
    pub edge_margin_px: f32,
    pub color: (f32, f32, f32, f32),
    pub autopilot_color: (f32, f32, f32, f32),
}

/// Handles the ship keeps to drive the cockpit each tick.
pub struct Cockpit {
    pub viewport: Gd<SubViewport>,
    pub dash: Gd<ShipDash>,
    pub arrows: Vec<(Vector3, Gd<MeshInstance3D>)>,
    pub cameras: Vec<Gd<Camera3D>>,
    pub frame: Gd<CameraFrame>,
    pub marker: Gd<TargetMarker>,
    /// 0 = your own eyes, 1.. = cameras[i - 1].
    pub view: usize,
}

/// Builds the cockpit under `parent` (the ship's interior node).
/// `desk` is the console top's transform in ship space (its +Y is the top
/// face's normal, +Z toward the windows).
pub fn build(parent: &mut Gd<Node3D>, ship_root: &mut Gd<Node3D>, data: &CockpitData, desk: Transform3D, _desk_half_thickness: f32, model_path: &str, model_scale: f32, model_offset: Vector3) -> Cockpit {
    // The dash draws into its own viewport.
    let mut viewport = SubViewport::new_alloc();
    viewport.set_name("DashViewport");
    viewport.set_disable_3d(true);
    viewport.set_transparent_background(false);
    viewport.set_update_mode(UpdateMode::DISABLED);
    let mut dash = ShipDash::new_alloc();
    dash.set_name("Dash");
    let (w, h) = dash.bind().viewport_px();
    viewport.set_size(Vector2i::new(w, h));
    viewport.add_child(&dash);
    parent.add_child(&viewport);

    // The screen: texture right = the pilot's right (-X), texture up =
    // toward the windows (+Z), facing up off the desk.
    let tilt = data.screen_tilt_deg.to_radians();
    let (st, ct) = (tilt.sin(), tilt.cos());
    let facing = Basis::from_cols(Vector3::new(-1.0, 0.0, 0.0), Vector3::new(0.0, ct, st), Vector3::new(0.0, st, -ct));
    let screen_at = v3(data.screen_at);
    let mut quad = QuadMesh::new_gd();
    quad.set_size(Vector2::new(data.screen_size_m.0, data.screen_size_m.1));
    let mut screen = MeshInstance3D::new_alloc();
    screen.set_name("DashScreen");
    screen.set_mesh(&quad);
    let mut mat = StandardMaterial3D::new_gd();
    mat.set_shading_mode(ShadingMode::UNSHADED);
    mat.set_texture(godot::classes::base_material_3d::TextureParam::ALBEDO, &viewport.get_texture().unwrap());
    screen.set_material_override(&mat);
    screen.set_transform(Transform3D::new(facing, screen_at));
    parent.add_child(&screen);
    // A bezel round the screen.
    let (sw, sh) = data.screen_size_m;
    let bezel = meshes::rounded_box(Vector3::new(sw + 0.1, sh + 0.1, 0.08), 0.03, 5);
    let mut bz = MeshInstance3D::new_alloc();
    bz.set_name("DashBezel");
    bz.set_mesh(&bezel.to_array_mesh());
    let mut bm = StandardMaterial3D::new_gd();
    bm.set_albedo(Color::from_rgb(0.12, 0.13, 0.15));
    bm.set_roughness(0.45);
    bm.set_metallic(0.3);
    bz.set_material_override(&bm);
    bz.set_transform(Transform3D::new(facing, screen_at - facing.col_c() * 0.045));
    parent.add_child(&bz);

    // The mini ship on a pedestal, with thruster arrows.
    let m = &data.mini_ship;
    let base_at = desk * v3(m.at);
    let mut pedestal = MeshInstance3D::new_alloc();
    pedestal.set_name("MiniPedestal");
    pedestal.set_mesh(&meshes::rounded_disc(m.pedestal_radius_m, 0.03, 48).to_array_mesh());
    let mut pm = StandardMaterial3D::new_gd();
    pm.set_albedo(Color::from_rgb(0.16, 0.18, 0.22));
    pm.set_feature(Feature::EMISSION, true);
    pm.set_emission(rgb(m.arrow_color));
    pm.set_emission_energy_multiplier(0.25);
    pm.set_emission_operator(EmissionOperator::ADD);
    pedestal.set_material_override(&pm);
    pedestal.set_transform(Transform3D::new(desk.basis, base_at));
    parent.add_child(&pedestal);
    let mut mini_root = Node3D::new_alloc();
    mini_root.set_name("MiniShip");
    // The model stays upright in ship space (it mirrors the real ship).
    let model_up = Vector3::new(0.0, m.arrow_len_m * 1.6, 0.0);
    mini_root.set_position(base_at + model_up);
    parent.add_child(&mini_root);
    if let Some(mut model) = crate::orbits::landmarks::instantiate(model_path, "Mini") {
        model.set_scale(Vector3::splat(model_scale * m.scale));
        model.set_position(model_offset * m.scale - Vector3::new(0.0, 5.0 * m.scale, 0.0));
        mini_root.add_child(&model);
    }
    let mut arrows = Vec::new();
    let mut am = StandardMaterial3D::new_gd();
    am.set_shading_mode(ShadingMode::UNSHADED);
    am.set_albedo(rgb(m.arrow_color));
    am.set_transparency(Transparency::ALPHA);
    for axis in [Vector3::RIGHT, Vector3::LEFT, Vector3::UP, Vector3::DOWN, Vector3::BACK, Vector3::FORWARD] {
        let mut cone = CylinderMesh::new_gd();
        cone.set_top_radius(0.0);
        cone.set_bottom_radius(m.arrow_len_m * 0.28);
        cone.set_height(m.arrow_len_m);
        cone.set_radial_segments(16);
        let mut a = MeshInstance3D::new_alloc();
        a.set_name(&format!("Thrust{}", arrows.len()));
        a.set_mesh(&cone);
        a.set_material_override(&am);
        // Cylinder points up its +Y; turn that onto the axis.
        let basis = rotation_between(Vector3::UP, axis);
        a.set_transform(Transform3D::new(basis, axis * m.arrow_len_m * 1.4));
        a.set_visible(false);
        mini_root.add_child(&a);
        arrows.push((axis, a));
    }

    // Cameras, with poles where they stand off the hull.
    let mut cameras = Vec::new();
    for (i, c) in data.cameras.iter().enumerate() {
        let at = v3(c.at);
        let mut cam = Camera3D::new_alloc();
        cam.set_name(&format!("ShipCamera{i}"));
        cam.set_fov(c.fov_deg);
        cam.set_near(0.05);
        let look = Transform3D::new(Basis::looking_at_ex(v3(c.look_at) - at).up(v3(c.up)).done(), at);
        cam.set_transform(look);
        ship_root.add_child(&cam);
        cameras.push(cam);
        if let Some(from) = c.pole_from {
            let from = v3(from);
            let refs = [Vector3::UP.cross(at - from).normalized(); 2];
            let refs = if refs[0].length_squared() < 0.5 { [Vector3::RIGHT; 2] } else { refs };
            let pole = meshes::swept_tube(&[from, at], &refs, 0.035, 10, false);
            let mut pm = MeshInstance3D::new_alloc();
            pm.set_name(&format!("CameraPole{i}"));
            pm.set_mesh(&pole.to_array_mesh());
            let mut mat = StandardMaterial3D::new_gd();
            mat.set_albedo(Color::from_rgb(0.7, 0.72, 0.75));
            mat.set_metallic(0.8);
            mat.set_roughness(0.35);
            pm.set_material_override(&mat);
            ship_root.add_child(&pm);
        }
        // A little camera head.
        let mut head = MeshInstance3D::new_alloc();
        head.set_name(&format!("CameraHead{i}"));
        head.set_mesh(&meshes::rounded_box(Vector3::new(0.18, 0.14, 0.24), 0.04, 4).to_array_mesh());
        let mut hm = StandardMaterial3D::new_gd();
        hm.set_albedo(Color::from_rgb(0.15, 0.16, 0.18));
        hm.set_roughness(0.5);
        head.set_material_override(&hm);
        head.set_transform(Transform3D::new(look.basis, at - look.basis.col_c() * 0.14));
        ship_root.add_child(&head);
    }

    let mut layer = godot::classes::CanvasLayer::new_alloc();
    layer.set_name("CameraFrameLayer");
    layer.set_layer(5);
    let mut frame = CameraFrame::new_alloc();
    frame.set_name("CameraFrame");
    frame.bind_mut().data = Some(data.camera_frame.clone());
    frame.set_visible(false);
    layer.add_child(&frame);
    let mut marker = TargetMarker::new_alloc();
    marker.set_name("TargetMarker");
    marker.bind_mut().data = Some(data.target_marker.clone());
    layer.add_child(&marker);
    ship_root.add_child(&layer);

    Cockpit { viewport, dash, arrows, cameras, frame, marker, view: 0 }
}

/// Smallest rotation turning `from` onto `to` (both unit length).
pub fn rotation_between(from: Vector3, to: Vector3) -> Basis {
    let axis = from.cross(to);
    let dot = from.dot(to).clamp(-1.0, 1.0);
    if axis.length_squared() < 1e-8 {
        return if dot > 0.0 { Basis::IDENTITY } else { Basis::from_axis_angle(Vector3::RIGHT, std::f32::consts::PI) };
    }
    Basis::from_axis_angle(axis.normalized(), dot.acos())
}

fn rgb((r, g, b): V3) -> Color {
    Color::from_rgb(r, g, b)
}

impl Cockpit {
    /// Light the arrows for this tick's thrust (ship space, -1..1 per axis).
    pub fn show_thrust(&mut self, thrust: Vector3, data: &MiniShipData) {
        for (axis, arrow) in self.arrows.iter_mut() {
            let amount = thrust.dot(*axis).max(0.0).min(1.0);
            arrow.set_visible(amount > 0.02);
            if let Some(mut m) = arrow.get_material_override().and_then(|m| m.try_cast::<StandardMaterial3D>().ok()) {
                let c = rgb(data.arrow_color);
                m.set_albedo(Color::from_rgba(c.r, c.g, c.b, 0.35 + 0.65 * amount));
            }
        }
    }

    /// Step to the next view: your eyes, then each camera in turn.
    pub fn next_view(&mut self, player_camera: Option<Gd<Camera3D>>, names: &[String]) {
        self.view = (self.view + 1) % (self.cameras.len() + 1);
        self.apply_view(player_camera, names);
    }

    pub fn reset_view(&mut self, player_camera: Option<Gd<Camera3D>>, names: &[String]) {
        if self.view != 0 {
            self.view = 0;
            self.apply_view(player_camera, names);
        }
    }

    fn apply_view(&mut self, player_camera: Option<Gd<Camera3D>>, names: &[String]) {
        if self.view == 0 {
            if let Some(mut c) = player_camera {
                c.make_current();
            }
            self.frame.set_visible(false);
        } else {
            let i = self.view - 1;
            self.cameras[i].make_current();
            self.frame.bind_mut().label = names.get(i).cloned().unwrap_or_default();
            self.frame.set_visible(true);
        }
    }
}

/// Brackets round the locked target, or an arrow at the screen edge
/// pointing the way when it's behind you or off-screen.
#[derive(GodotClass)]
#[class(base = Control)]
pub struct TargetMarker {
    pub data: Option<TargetMarkerData>,
    /// World position of the target and its label; None hides the marker.
    pub target: Option<(Vector3, String)>,
    pub autopilot: bool,
    font: Option<Gd<Font>>,
    base: Base<Control>,
}

#[godot_api]
impl IControl for TargetMarker {
    fn init(base: Base<Control>) -> Self {
        Self { data: None, target: None, autopilot: false, font: None, base }
    }

    fn ready(&mut self) {
        self.base_mut().set_anchors_and_offsets_preset(godot::classes::control::LayoutPreset::FULL_RECT);
        self.base_mut().set_mouse_filter(godot::classes::control::MouseFilter::IGNORE);
        self.font = try_load::<FontFile>("res://assets/fonts/LilitaOne-Regular.ttf").ok().map(|f| f.upcast());
    }

    fn process(&mut self, _delta: f64) {
        self.base_mut().queue_redraw();
    }

    fn draw(&mut self) {
        let (Some(d), Some((world, label))) = (self.data.clone(), self.target.clone()) else { return };
        let Some(cam) = self.base().get_viewport().and_then(|v| v.get_camera_3d()) else { return };
        let size = self.base().get_size();
        if size.y < 100.0 {
            return;
        }
        let k = size.y / 800.0;
        let c = d.autopilot_color;
        let col = if self.autopilot { Color::from_rgba(c.0, c.1, c.2, c.3) } else { Color::from_rgba(d.color.0, d.color.1, d.color.2, d.color.3) };
        let behind = cam.is_position_behind(world);
        let at = cam.unproject_position(world);
        let centre = size * 0.5;
        let margin = d.edge_margin_px * k;
        let inside = !behind && at.x > margin && at.y > margin && at.x < size.x - margin && at.y < size.y - margin;
        // Off-screen: which way to turn, from the target's position in the
        // camera's own space (x right, y up), which stays right when behind.
        let local = cam.get_global_transform().affine_inverse() * world;
        let way = Vector2::new(local.x, -local.y);
        let mut c2 = self.base().clone().upcast::<Control>();
        let w = d.line_px * k;
        if inside {
            let h = d.size_px * k;
            for (sx, sy) in [(-1.0, -1.0), (1.0, -1.0), (1.0, 1.0), (-1.0, 1.0)] {
                let corner = at + Vector2::new(sx * h, sy * h);
                c2.draw_line_ex(corner, corner - Vector2::new(sx * h * 0.45, 0.0), col).width(w).done();
                c2.draw_line_ex(corner, corner - Vector2::new(0.0, sy * h * 0.45), col).width(w).done();
            }
            if let Some(font) = &self.font {
                let px = (d.label_px as f32 * k) as i32;
                c2.draw_string_ex(font, at + Vector2::new(-200.0 * k, h + px as f32 * 1.1), &label)
                    .alignment(HorizontalAlignment::CENTER)
                    .width(400.0 * k)
                    .font_size(px)
                    .modulate(col)
                    .done();
            }
        } else {
            let dir = way.normalized_or_zero();
            let dir = if dir == Vector2::ZERO { Vector2::new(0.0, -1.0) } else { dir };
            // Walk out from the centre to the margin rectangle.
            let half = centre - Vector2::new(margin, margin);
            let t = (half.x / dir.x.abs().max(1e-4)).min(half.y / dir.y.abs().max(1e-4));
            let tip = centre + dir * t;
            let side = Vector2::new(-dir.y, dir.x);
            let s = d.size_px * k * 0.7;
            let pts = PackedVector2Array::from(&[tip, tip - dir * s * 2.4 + side * s * 0.8, tip - dir * s * 1.7, tip - dir * s * 2.4 - side * s * 0.8]);
            c2.draw_colored_polygon(&pts, col);
        }
    }
}

/// The screen border shown while a ship camera fills the view.
#[derive(GodotClass)]
#[class(base = Control)]
pub struct CameraFrame {
    pub data: Option<CameraFrameData>,
    pub label: String,
    font: Option<Gd<Font>>,
    clock: f32,
    base: Base<Control>,
}

#[godot_api]
impl IControl for CameraFrame {
    fn init(base: Base<Control>) -> Self {
        Self { data: None, label: String::new(), font: None, clock: 0.0, base }
    }

    fn ready(&mut self) {
        self.base_mut().set_anchors_and_offsets_preset(godot::classes::control::LayoutPreset::FULL_RECT);
        self.base_mut().set_mouse_filter(godot::classes::control::MouseFilter::IGNORE);
        self.font = try_load::<FontFile>("res://assets/fonts/VT323-Regular.ttf").ok().map(|f| f.upcast());
    }

    fn process(&mut self, delta: f64) {
        self.clock += delta as f32;
        if self.base().is_visible() {
            self.base_mut().queue_redraw();
        }
    }

    fn draw(&mut self) {
        let (Some(d), Some(font)) = (self.data.clone(), self.font.clone()) else { return };
        let size = self.base().get_size();
        if size.y < 100.0 {
            return;
        }
        let s = size.y / 800.0;
        let col = Color::from_rgba(d.color.0, d.color.1, d.color.2, d.color.3);
        let b = d.border_px * s;
        let label = self_label(&self.label);
        let rec_on = (self.clock * 1.5) as i32 % 2 == 0;
        let mut c = self.base_mut();
        // Dark border like a monitor bezel, with corner brackets.
        for r in [
            Rect2::new(Vector2::ZERO, Vector2::new(size.x, b)),
            Rect2::new(Vector2::new(0.0, size.y - b), Vector2::new(size.x, b)),
            Rect2::new(Vector2::ZERO, Vector2::new(b, size.y)),
            Rect2::new(Vector2::new(size.x - b, 0.0), Vector2::new(b, size.y)),
        ] {
            c.draw_rect(r, Color::from_rgba(0.02, 0.02, 0.03, 0.92));
        }
        let k = 60.0 * s;
        let inset = b + 18.0 * s;
        for (corner, dx, dy) in [
            (Vector2::new(inset, inset), 1.0, 1.0),
            (Vector2::new(size.x - inset, inset), -1.0, 1.0),
            (Vector2::new(inset, size.y - inset), 1.0, -1.0),
            (Vector2::new(size.x - inset, size.y - inset), -1.0, -1.0),
        ] {
            c.draw_line_ex(corner, corner + Vector2::new(k * dx, 0.0), col).width(4.0 * s).done();
            c.draw_line_ex(corner, corner + Vector2::new(0.0, k * dy), col).width(4.0 * s).done();
        }
        let px = (d.label_px as f32 * s) as i32;
        c.draw_string_ex(&font, Vector2::new(inset + 20.0 * s, inset + 50.0 * s), &GString::from(label.as_str()))
            .font_size(px)
            .modulate(col)
            .done();
        if rec_on {
            c.draw_circle(Vector2::new(size.x - inset - 40.0 * s, inset + 36.0 * s), 12.0 * s, Color::from_rgb(d.rec_color.0, d.rec_color.1, d.rec_color.2));
        }
        let hint = c.tr(&d.hint_key).to_string();
        c.draw_string_ex(&font, Vector2::new(0.0, size.y - inset - 16.0 * s), &GString::from(hint.as_str()))
            .alignment(HorizontalAlignment::CENTER)
            .width(size.x)
            .font_size((px as f32 * 0.7) as i32)
            .modulate(col)
            .done();
    }
}

fn self_label(s: &str) -> String {
    s.to_uppercase()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn rotation_between_turns_up_onto_each_axis() {
        for to in [Vector3::RIGHT, Vector3::LEFT, Vector3::UP, Vector3::DOWN, Vector3::BACK, Vector3::FORWARD] {
            let r = rotation_between(Vector3::UP, to) * Vector3::UP;
            assert!((r - to).length() < 1e-4, "{to:?} -> {r:?}");
        }
    }
}
