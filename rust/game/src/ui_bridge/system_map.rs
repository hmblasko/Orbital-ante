//! `SystemMap`: the live system map on the wrist screen (CLAUDE.md section 6:
//! "Map opened from the cockpit or a wrist screen, showing live orbits. No
//! map icons for undiscovered places").
//!
//! A top-down view of the whole solar system as it is right now: every
//! orbit, every body at its live position, the player and their heading.
//! The game keeps running while it is open, so the planets visibly creep
//! along their orbits. Layout and colours come from
//! res://data/ui/system_map.ron. Landmarks are not shown at all yet; when
//! discovery lands (Phase 7) only discovered ones will get icons.
//!
//! Serves pillar 1 (a living clockwork solar system you can read at a
//! glance) and fun rule 5 (flow: clear goals, you always know where you are)
//! and rule 4 (curiosity: the map shows where things are, not what is there).

use godot::classes::{Control, Font, FontFile, IControl, InputEvent, StyleBoxFlat, ThemeDb};
use godot::global::HorizontalAlignment;
use godot::prelude::*;
use serde::Deserialize;

use crate::orbits::system::{MapSnapshot, SYSTEM_GROUP, SolarSystem};
use crate::player::hud::{Rgb, Rgba, faded, rgb, rgba, style_box};

const DATA_PATH: &str = "res://data/ui/system_map.ron";
const DATA_EMBEDDED: &str = include_str!("../../../../godot/data/ui/system_map.ron");
const NUMBER_FONT: &str = "res://assets/fonts/LilitaOne-Regular.ttf";

#[derive(Debug, Clone, Deserialize)]
pub struct MapData {
    pub panel_px: (f32, f32),
    pub panel_top_px: f32,
    pub corner_px: i32,
    pub bezel_px: f32,
    pub open_s: f32,
    pub zoom_half_height_m: Vec<f32>,
    pub start_zoom: usize,
    pub zoom_glide_s: f32,
    pub orbit_points: usize,
    pub orbit_width_px: f32,
    pub orbit_alpha: f32,
    pub min_body_px: f32,
    pub sun_glow_px: f32,
    pub label_font_px: i32,
    pub title_font_px: i32,
    pub hint_font_px: i32,
    pub moon_label_min_px: f32,
    pub player_marker_px: f32,
    pub scale_bar_px: f32,
    pub screen_color: Rgba,
    pub bezel_color: Rgba,
    pub grid_color: Rgba,
    pub text_color: Rgb,
    pub player_color: Rgb,
    pub frame_ring_color: Rgba,
}

#[derive(GodotClass)]
#[class(base = Control)]
pub struct SystemMap {
    data: MapData,
    open: bool,
    /// 0 = lowered, 1 = fully raised.
    raise: f32,
    zoom: usize,
    /// Smoothed half-height in metres and centre blend (0 = sun, 1 = player).
    half_width: f32,
    focus: f32,
    using_keyboard: bool,
    numbers: Option<Gd<Font>>,
    body: Option<Gd<Font>>,
    screen: Option<Gd<StyleBoxFlat>>,
    bezel: Option<Gd<StyleBoxFlat>>,
    base: Base<Control>,
}

#[godot_api]
impl IControl for SystemMap {
    fn init(base: Base<Control>) -> Self {
        let data: MapData = crate::data::load(DATA_PATH, DATA_EMBEDDED);
        let zoom = data.start_zoom.min(data.zoom_half_height_m.len().saturating_sub(1));
        let half_width = data.zoom_half_height_m.get(zoom).copied().unwrap_or(10000.0);
        Self {
            data,
            open: false,
            raise: 0.0,
            zoom,
            half_width,
            focus: if zoom == 0 { 0.0 } else { 1.0 },
            using_keyboard: false,
            numbers: None,
            body: None,
            screen: None,
            bezel: None,
            base,
        }
    }

    fn ready(&mut self) {
        self.base_mut().set_anchors_and_offsets_preset(godot::classes::control::LayoutPreset::FULL_RECT);
        self.base_mut().set_mouse_filter(godot::classes::control::MouseFilter::IGNORE);
        self.numbers = try_load::<FontFile>(NUMBER_FONT).ok().map(|f| f.upcast());
        let theme = ThemeDb::singleton().get_project_theme();
        self.body = theme.and_then(|t| t.get_default_font()).or_else(|| ThemeDb::singleton().get_fallback_font());
        let d = &self.data;
        self.screen = Some(style_box(rgba(d.screen_color), faded(rgb(d.player_color), 0.35), 2, d.corner_px));
        self.bezel = Some(style_box(rgba(d.bezel_color), Color::from_rgba(1.0, 1.0, 1.0, 0.12), 2, d.corner_px + d.bezel_px as i32));
    }

    fn input(&mut self, event: Gd<InputEvent>) {
        let keyboard = event.clone().try_cast::<godot::classes::InputEventKey>().is_ok()
            || event.clone().try_cast::<godot::classes::InputEventMouseButton>().is_ok();
        let pad = event.clone().try_cast::<godot::classes::InputEventJoypadButton>().is_ok();
        if keyboard {
            self.using_keyboard = true;
        } else if pad {
            self.using_keyboard = false;
        }
        if event.is_action_pressed("open_map") {
            self.set_open(!self.open);
        } else if self.open && event.is_action_pressed("map_zoom_in") {
            self.set_zoom(self.zoom as i64 + 1);
        } else if self.open && event.is_action_pressed("map_zoom_out") {
            self.set_zoom(self.zoom as i64 - 1);
        }
    }

    fn process(&mut self, delta: f64) {
        let dt = delta as f32;
        let d = &self.data;
        let step = if d.open_s > 0.0 { dt / d.open_s } else { 1.0 };
        self.raise = if self.open { (self.raise + step).min(1.0) } else { (self.raise - step).max(0.0) };
        // Glide the zoom in log space so each step feels the same speed.
        let target = d.zoom_half_height_m.get(self.zoom).copied().unwrap_or(self.half_width);
        let k = if d.zoom_glide_s > 0.0 { 1.0 - (-dt * 4.0 / d.zoom_glide_s).exp() } else { 1.0 };
        self.half_width = (self.half_width.ln() + (target.ln() - self.half_width.ln()) * k).exp();
        let focus_target = if self.zoom == 0 { 0.0 } else { 1.0 };
        self.focus += (focus_target - self.focus) * k;
        let raise = self.raise;
        self.base_mut().set_visible(raise > 0.0);
        let ease = 1.0 - (1.0 - raise).powi(3);
        self.base_mut().set_modulate(Color::from_rgba(1.0, 1.0, 1.0, ease));
        if raise > 0.0 {
            self.base_mut().queue_redraw();
        }
    }

    fn draw(&mut self) {
        let size = self.base().get_size();
        // Headless runs have no window; nothing to draw.
        if size.y < 100.0 || self.raise <= 0.0 {
            return;
        }
        let (Some(numbers), Some(body)) = (self.numbers.clone(), self.body.clone()) else { return };
        let s = size.y / 800.0;
        let d = self.data.clone();
        let ease = 1.0 - (1.0 - self.raise).powi(3);
        // The wrist screen rises from below.
        let panel = Vector2::new(d.panel_px.0 * s, d.panel_px.1 * s);
        let pos = Vector2::new((size.x - panel.x) * 0.5, d.panel_top_px * s + (1.0 - ease) * panel.y * 0.35);
        let bezel = d.bezel_px * s;
        let outer = Rect2::new(pos - Vector2::new(bezel, bezel), panel + Vector2::new(bezel, bezel) * 2.0);
        let screen = Rect2::new(pos, panel);
        if let (Some(b), Some(sc)) = (self.bezel.clone(), self.screen.clone()) {
            let mut c = self.base_mut();
            c.draw_style_box(&b, outer);
            c.draw_style_box(&sc, screen);
        }

        let title = self.base().tr("MAP_TITLE").to_string();
        self.text(&numbers, pos + Vector2::new(22.0 * s, 38.0 * s), panel.x, HorizontalAlignment::LEFT, &title, d.title_font_px, s, rgb(d.text_color));

        let snapshot = self.system().and_then(|sys| sys.bind().map_snapshot(d.orbit_points));
        let Some(snap) = snapshot else {
            let msg = self.base().tr("MAP_NO_SIGNAL").to_string();
            self.text(&body, pos + Vector2::new(0.0, panel.y * 0.5), panel.x, HorizontalAlignment::CENTER, &msg, d.label_font_px, s, rgb(d.text_color));
            return;
        };

        // The drawing area: inside the screen, below the title and above the
        // hint line, inset so nothing touches the rounded corners.
        let inset = d.corner_px as f32 * s;
        let area = Rect2::new(pos + Vector2::new(inset, 56.0 * s), panel - Vector2::new(inset * 2.0, 56.0 * s + 44.0 * s));
        let sun = snap.bodies.iter().find(|b| b.is_star).map_or(Vector3::ZERO, |b| b.position);
        let centre3 = sun.lerp(snap.player, self.focus);
        let view = View { centre: Vector2::new(centre3.x, centre3.z), area, px_per_m: area.size.y * 0.5 / self.half_width };

        self.draw_grid(&view, s, &d);
        self.draw_bodies(&view, &snap, &body, s, &d, sun);
        self.draw_player(&view, &snap, s, &d);
        self.draw_footer(&body, &snap, pos, panel, s, &d, &view);
    }
}

/// Map projection: top-down onto the system plane (XZ), +X right, +Z down.
struct View {
    centre: Vector2,
    area: Rect2,
    px_per_m: f32,
}

impl View {
    fn to_screen(&self, p: Vector3) -> Vector2 {
        project(Vector2::new(p.x, p.z), self.centre, self.area.center(), self.px_per_m)
    }
}

#[godot_api]
impl SystemMap {
    #[func]
    pub fn is_open(&self) -> bool {
        self.open
    }

    #[func]
    pub fn set_open(&mut self, open: bool) {
        self.open = open;
    }

    #[func]
    pub fn get_zoom(&self) -> i64 {
        self.zoom as i64
    }

    /// Zoom step, clamped to the steps in the data (0 = whole system).
    #[func]
    pub fn set_zoom(&mut self, zoom: i64) {
        let last = self.data.zoom_half_height_m.len().saturating_sub(1) as i64;
        self.zoom = zoom.clamp(0, last) as usize;
    }

    /// How many bodies the map can see right now (0 = no system in the scene).
    #[func]
    pub fn get_body_count(&self) -> i64 {
        self.system().and_then(|s| s.bind().map_snapshot(4)).map_or(0, |m| m.bodies.len() as i64)
    }
}

impl SystemMap {
    fn system(&self) -> Option<Gd<SolarSystem>> {
        self.base().get_tree().get_first_node_in_group(SYSTEM_GROUP).and_then(|n| n.try_cast::<SolarSystem>().ok())
    }

    #[allow(clippy::too_many_arguments)]
    fn text(&mut self, font: &Gd<Font>, at: Vector2, width: f32, align: HorizontalAlignment, text: &str, px: i32, s: f32, color: Color) {
        let size = (px as f32 * s) as i32;
        let shadow = Color::from_rgba(0.0, 0.0, 0.0, 0.6 * color.a);
        let c = self.base_mut();
        c.draw_string_ex(font, at + Vector2::new(0.0, 2.0), &GString::from(text)).alignment(align).width(width).font_size(size).modulate(shadow).done();
        c.draw_string_ex(font, at, &GString::from(text)).alignment(align).width(width).font_size(size).modulate(color).done();
    }

    fn draw_line_clipped(&mut self, a: Vector2, b: Vector2, area: Rect2, color: Color, width: f32) {
        if let Some((a, b)) = clip_segment(a, b, area) {
            self.base_mut().draw_line_ex(a, b, color).width(width).antialiased(true).done();
        }
    }

    /// Faint grid at the scale-bar spacing, so distances read at a glance.
    fn draw_grid(&mut self, view: &View, s: f32, d: &MapData) {
        let step_m = nice_length(d.scale_bar_px * s / view.px_per_m);
        let step_px = step_m * view.px_per_m;
        if step_px < 24.0 * s {
            return;
        }
        let colour = rgba(d.grid_color);
        let a = view.area;
        let first_x = ((view.centre.x - a.size.x * 0.5 / view.px_per_m) / step_m).ceil() as i64;
        let first_z = ((view.centre.y - a.size.y * 0.5 / view.px_per_m) / step_m).ceil() as i64;
        let count_x = (a.size.x / step_px) as i64 + 1;
        let count_z = (a.size.y / step_px) as i64 + 1;
        for i in 0..count_x {
            let x = view.to_screen(Vector3::new((first_x + i) as f32 * step_m, 0.0, 0.0)).x;
            self.draw_line_clipped(Vector2::new(x, a.position.y), Vector2::new(x, a.end().y), a, colour, 1.0);
        }
        for i in 0..count_z {
            let y = view.to_screen(Vector3::new(0.0, 0.0, (first_z + i) as f32 * step_m)).y;
            self.draw_line_clipped(Vector2::new(a.position.x, y), Vector2::new(a.end().x, y), a, colour, 1.0);
        }
    }

    fn draw_bodies(&mut self, view: &View, snap: &MapSnapshot, font: &Gd<Font>, s: f32, d: &MapData, sun: Vector3) {
        let area = view.area;
        // Orbits first, so every dot sits on top of every line.
        for b in &snap.bodies {
            let n = b.orbit.len();
            let colour = faded(b.color, d.orbit_alpha);
            for k in 0..n {
                let (p, q) = (view.to_screen(b.orbit[k]), view.to_screen(b.orbit[(k + 1) % n]));
                self.draw_line_clipped(p, q, area, colour, d.orbit_width_px * s);
            }
        }
        // Sun glow, then the bodies.
        for b in &snap.bodies {
            let at = view.to_screen(b.position);
            let r = (b.radius * view.px_per_m).max(d.min_body_px * s);
            // Only whole discs: a body half off the screen would spill over
            // the footer, and its orbit line already shows where it is.
            let reach = r.max(if b.ring > 0.0 { b.ring * view.px_per_m } else { 0.0 }) + if b.is_star { d.sun_glow_px * s } else { 0.0 };
            if !area.grow(-reach.min(area.size.y * 0.45)).contains_point(at) {
                continue;
            }
            let mut c = self.base_mut();
            if b.is_star {
                let glow = d.sun_glow_px * s + r;
                for i in (1..=6).rev() {
                    let f = i as f32 / 6.0;
                    c.draw_circle(at, r + (glow - r) * f, faded(b.color, 0.06 * (1.0 - f) + 0.02));
                }
                c.draw_circle(at, r, b.color);
            } else {
                if b.ring > 0.0 {
                    let ring = (b.ring * view.px_per_m).max(r + 2.5 * s);
                    c.draw_arc_ex(at, ring, 0.0, std::f32::consts::TAU, 64, faded(b.color, 0.55)).width((1.5 * s).max(1.0)).antialiased(true).done();
                }
                // Night half darker, day half facing the sun.
                let night = Color::from_rgba(b.color.r * 0.32, b.color.g * 0.32, b.color.b * 0.38, 1.0);
                c.draw_circle(at, r, night);
                let to_sun = view.to_screen(sun) - at;
                let a0 = to_sun.y.atan2(to_sun.x);
                let mut half = PackedVector2Array::new();
                for k in 0..=16 {
                    let a = a0 - std::f32::consts::FRAC_PI_2 + std::f32::consts::PI * k as f32 / 16.0;
                    half.push(at + Vector2::new(a.cos(), a.sin()) * r);
                }
                c.draw_colored_polygon(&half, b.color);
            }
            if snap.frame_body.as_deref() == Some(b.id.as_str()) {
                c.draw_arc_ex(at, r + 5.0 * s, 0.0, std::f32::consts::TAU, 48, rgba(d.frame_ring_color)).width(2.0 * s).antialiased(true).done();
            }
        }
        self.draw_labels(view, snap, font, s, d);
    }

    /// Body names under their dots. The body you are near goes first, then
    /// planets by size, then moons; a label that would overlap one already
    /// placed is left out, so the wide view never turns into a pile of text.
    fn draw_labels(&mut self, view: &View, snap: &MapSnapshot, font: &Gd<Font>, s: f32, d: &MapData) {
        let mut order: Vec<&crate::orbits::system::MapBody> = snap.bodies.iter().collect();
        let near = snap.frame_body.as_deref();
        order.sort_by(|a, b| {
            let key = |x: &crate::orbits::system::MapBody| (near != Some(x.id.as_str()), x.is_moon, -x.radius);
            key(a).partial_cmp(&key(b)).unwrap_or(std::cmp::Ordering::Equal)
        });
        let mut placed: Vec<Rect2> = Vec::new();
        for b in order {
            let at = view.to_screen(b.position);
            let r = (b.radius * view.px_per_m).max(d.min_body_px * s);
            if !view.area.contains_point(at) || (b.is_moon && r < d.moon_label_min_px * s) {
                continue;
            }
            let name = self.base().tr(&format!("BODY_{}", b.id.to_uppercase())).to_string();
            let px = if b.is_moon { d.label_font_px - 2 } else { d.label_font_px };
            let width = font.get_string_size_ex(&GString::from(name.as_str())).font_size((px as f32 * s) as i32).done().x;
            let baseline = at + Vector2::new(0.0, r + 18.0 * s);
            let rect = Rect2::new(Vector2::new(at.x - width * 0.5 - 3.0 * s, baseline.y - px as f32 * s), Vector2::new(width + 6.0 * s, px as f32 * s * 1.3));
            if placed.iter().any(|p| p.intersects(rect)) {
                continue;
            }
            placed.push(rect);
            let colour = faded(rgb(d.text_color), if b.is_moon { 0.7 } else { 0.95 });
            self.text(font, baseline - Vector2::new(100.0 * s, 0.0), 200.0 * s, HorizontalAlignment::CENTER, &name, px, s, colour);
        }
    }

    /// The player: an arrow along their heading, or a diamond when looking
    /// straight up or down.
    fn draw_player(&mut self, view: &View, snap: &MapSnapshot, s: f32, d: &MapData) {
        let at = view.to_screen(snap.player);
        if !view.area.contains_point(at) {
            return;
        }
        let size = d.player_marker_px * s;
        let flat = Vector2::new(snap.player_forward.x, snap.player_forward.z);
        let dir = if flat.length() > 0.2 { flat.normalized() } else { Vector2::new(0.0, -1.0) };
        let side = Vector2::new(-dir.y, dir.x);
        let mut points = PackedVector2Array::new();
        let colour = rgb(d.player_color);
        if flat.length() > 0.2 {
            points.push(at + dir * size);
            points.push(at - dir * size * 0.6 + side * size * 0.65);
            points.push(at - dir * size * 0.25);
            points.push(at - dir * size * 0.6 - side * size * 0.65);
        } else {
            for k in 0..4 {
                let a = std::f32::consts::FRAC_PI_2 * k as f32;
                points.push(at + Vector2::new(a.cos(), a.sin()) * size * 0.6);
            }
        }
        let mut outline = points.clone();
        outline.push(points[0]);
        let mut c = self.base_mut();
        c.draw_colored_polygon(&points, colour);
        c.draw_polyline_ex(&outline, Color::from_rgba(0.0, 0.0, 0.0, 0.7)).width(1.5 * s).antialiased(true).done();
    }

    /// Bottom line: where you are, the scale bar, and the button hints.
    #[allow(clippy::too_many_arguments)]
    fn draw_footer(&mut self, font: &Gd<Font>, snap: &MapSnapshot, pos: Vector2, panel: Vector2, s: f32, d: &MapData, view: &View) {
        let y = pos.y + panel.y - 18.0 * s;
        let text = rgb(d.text_color);
        let place = match &snap.frame_body {
            Some(id) => {
                let name = self.base().tr(&format!("BODY_{}", id.to_uppercase())).to_string();
                self.base().tr("MAP_IN_GRAVITY").to_string().replace("{body}", &name)
            }
            None => self.base().tr("MAP_DEEP_SPACE").to_string(),
        };
        self.text(font, Vector2::new(pos.x + 22.0 * s, y), panel.x * 0.4, HorizontalAlignment::LEFT, &place, d.hint_font_px, s, rgb(d.player_color));

        // Scale bar, centred.
        let len_m = nice_length(d.scale_bar_px * s / view.px_per_m);
        let len_px = len_m * view.px_per_m;
        let x0 = pos.x + (panel.x - len_px) * 0.5;
        let bar_y = y - 6.0 * s;
        {
            let mut c = self.base_mut();
            c.draw_line_ex(Vector2::new(x0, bar_y), Vector2::new(x0 + len_px, bar_y), text).width(2.0 * s).done();
            for x in [x0, x0 + len_px] {
                c.draw_line_ex(Vector2::new(x, bar_y - 5.0 * s), Vector2::new(x, bar_y + 5.0 * s), text).width(2.0 * s).done();
            }
        }
        let label = if len_m >= 1000.0 { format!("{} km", len_m / 1000.0) } else { format!("{} m", len_m) };
        self.text(font, Vector2::new(x0 - 100.0 * s, bar_y - 9.0 * s), len_px + 200.0 * s, HorizontalAlignment::CENTER, &label, d.hint_font_px - 2, s, faded(text, 0.8));

        let hint = self.base().tr(if self.using_keyboard { "MAP_HINT_KEYS" } else { "MAP_HINT_PAD" }).to_string();
        self.text(font, Vector2::new(pos.x + panel.x * 0.55, y), panel.x * 0.45 - 22.0 * s, HorizontalAlignment::RIGHT, &hint, d.hint_font_px, s, faded(text, 0.75));
    }
}

/// System-plane point to screen pixels.
pub fn project(p: Vector2, centre: Vector2, screen_centre: Vector2, px_per_m: f32) -> Vector2 {
    screen_centre + (p - centre) * px_per_m
}

/// The largest 1, 2 or 5 times a power of ten that is at most `max_m`
/// (scale bar and grid spacing).
pub fn nice_length(max_m: f32) -> f32 {
    if max_m <= 0.0 || !max_m.is_finite() {
        return 1.0;
    }
    let power = 10f32.powf(max_m.log10().floor());
    [5.0, 2.0, 1.0].into_iter().map(|m| m * power).find(|&v| v <= max_m * 1.0001).unwrap_or(power)
}

/// Clips a line segment to a rectangle (Liang-Barsky). None when it misses.
pub fn clip_segment(a: Vector2, b: Vector2, r: Rect2) -> Option<(Vector2, Vector2)> {
    let d = b - a;
    let (mut t0, mut t1) = (0.0f32, 1.0f32);
    let end = r.end();
    for (p, q) in [
        (-d.x, a.x - r.position.x),
        (d.x, end.x - a.x),
        (-d.y, a.y - r.position.y),
        (d.y, end.y - a.y),
    ] {
        if p == 0.0 {
            if q < 0.0 {
                return None;
            }
        } else {
            let t = q / p;
            if p < 0.0 {
                t0 = t0.max(t);
            } else {
                t1 = t1.min(t);
            }
            if t0 > t1 {
                return None;
            }
        }
    }
    Some((a + d * t0, a + d * t1))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn map_data_parses_and_zooms_widest_first() {
        let d: MapData = crate::data::parse(DATA_EMBEDDED).unwrap();
        assert!(d.zoom_half_height_m.windows(2).all(|w| w[0] > w[1]));
        assert!(d.start_zoom < d.zoom_half_height_m.len());
        assert!(d.label_font_px >= 9 && d.hint_font_px - 2 >= 9, "section 15: text at least 9 px");
    }

    #[test]
    fn widest_zoom_fits_the_whole_system() {
        let d: MapData = crate::data::parse(DATA_EMBEDDED).unwrap();
        let system: crate::orbits::system::SystemData =
            crate::data::parse(include_str!("../../../../godot/data/system/solar_system.ron")).unwrap();
        let widest = system.bodies.iter().filter_map(|b| b.orbit.as_ref()).map(|o| o.radius_m).fold(0.0, f64::max);
        // Zoom steps are half the map's height (its shorter side), so the
        // widest step shows every orbit whole.
        assert!(d.zoom_half_height_m[0] as f64 > widest);
    }

    #[test]
    fn nice_lengths_are_1_2_5_steps() {
        assert_eq!(nice_length(140.0), 100.0);
        assert_eq!(nice_length(2600.0), 2000.0);
        assert_eq!(nice_length(7000.0), 5000.0);
        assert_eq!(nice_length(1000.0), 1000.0);
    }

    #[test]
    fn projection_centres_and_scales() {
        let c = Vector2::new(100.0, -50.0);
        let sc = Vector2::new(640.0, 400.0);
        assert_eq!(project(c, c, sc, 0.01), sc);
        assert_eq!(project(c + Vector2::new(1000.0, 0.0), c, sc, 0.01), sc + Vector2::new(10.0, 0.0));
    }

    #[test]
    fn segments_clip_to_the_screen() {
        let r = Rect2::new(Vector2::ZERO, Vector2::new(100.0, 100.0));
        let (a, b) = clip_segment(Vector2::new(-50.0, 50.0), Vector2::new(150.0, 50.0), r).unwrap();
        assert!((a.x - 0.0).abs() < 1e-4 && (b.x - 100.0).abs() < 1e-4);
        assert!(clip_segment(Vector2::new(-10.0, -10.0), Vector2::new(-5.0, 200.0), r).is_none());
        let inside = (Vector2::new(10.0, 10.0), Vector2::new(20.0, 30.0));
        assert_eq!(clip_segment(inside.0, inside.1, r), Some(inside));
    }
}
