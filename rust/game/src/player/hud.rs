//! `SuitHud`: the helmet visor HUD (CLAUDE.md section 6: diegetic and
//! minimal, oxygen and jetpack fuel on the visor edge, every gauge a
//! different shape), button prompts, and the one-line death recap (fun rule 8).
//!
//! Look: chunky TF2-style type (Lilita One for numbers, Fredoka for labels),
//! tinted glass plates, a soft visor frame at the screen edge. Drawn in code
//! so it stays crisp at any resolution; layout and colours come from
//! res://data/ui/suit_hud.ron.
//! Serves pillar 2 (real danger you can read) and fun rule 5 (clear goals,
//! instant feedback).

use std::f32::consts::{PI, TAU};

use godot::classes::{
    CanvasItem, ColorRect, Control, Font, FontFile, IControl, Input, InputEvent, InputEventJoypadButton,
    InputEventJoypadMotion, InputEventKey, InputEventMouseButton, Shader, ShaderMaterial, StyleBoxFlat, ThemeDb,
};
use godot::global::HorizontalAlignment;
use godot::prelude::*;
use serde::Deserialize;

use super::PlayerController;
use super::controller::PLAYER_GROUP;

const DATA_PATH: &str = "res://data/ui/suit_hud.ron";
const DATA_EMBEDDED: &str = include_str!("../../../../godot/data/ui/suit_hud.ron");
const NUMBER_FONT: &str = "res://assets/fonts/LilitaOne-Regular.ttf";
const VISOR_SHADER: &str = "res://shaders/visor.gdshader";

/// Group `GameRoot` joins, so the recap can return to the menu.
pub const GAME_ROOT_GROUP: &str = "game_root";

pub(crate) type Rgb = (f32, f32, f32);
pub(crate) type Rgba = (f32, f32, f32, f32);

#[derive(Debug, Clone, Deserialize)]
pub struct HudData {
    pub margin_px: f32,
    pub oxygen_plate_px: (f32, f32),
    pub oxygen_ring_radius_px: f32,
    pub oxygen_ring_width_px: f32,
    pub oxygen_ticks: u32,
    pub fuel_plate_px: (f32, f32),
    pub fuel_segments: u32,
    pub health_bar_px: (f32, f32),
    pub health_segments: u32,
    pub number_font_px: i32,
    pub label_font_px: i32,
    pub status_font_px: i32,
    pub prompt_font_px: i32,
    pub recap_title_px: i32,
    pub recap_line_px: i32,
    pub crosshair_radius_px: f32,
    pub warning_blink_hz: f32,
    pub oxygen_color: Rgb,
    pub fuel_color: Rgb,
    pub health_color: Rgb,
    pub warning_color: Rgb,
    pub glass_color: Rgba,
    pub glass_edge_color: Rgba,
    pub recap_input_delay_s: f32,
    pub visor_edge_darkness: f32,
    pub weapon_plate_px: (f32, f32),
    pub ammo_color: Rgb,
    pub hit_marker_px: f32,
    pub hit_colors: Vec<Rgb>,
    pub damage_arrow_s: f32,
    pub damage_arrow_radius_px: f32,
}

/// What the HUD shows of the weapon in hand.
struct WeaponView {
    name_key: String,
    loaded: i64,
    reserve: i64,
    infinite: bool,
    reload: f32,
    marker: f32,
    marker_kind: i64,
    pickup: String,
    rope_shots: i64,
    carry: String,
}

/// Snapshot of the player for one frame, so drawing never holds a borrow.
struct View {
    oxygen: f32,
    oxygen_s: f32,
    fuel: f32,
    health: f32,
    tanks: i64,
    max_tanks: i64,
    low: bool,
    tethered: bool,
    zero_g: bool,
    at_supply: bool,
    prompt: String,
    dead_key: Option<GString>,
    weapon: Option<WeaponView>,
    /// Screen angle (0 = straight ahead, clockwise) of the last hit, and
    /// how strong its arrow still is.
    hit_dir: Option<(f32, f32)>,
}

#[derive(GodotClass)]
#[class(base = Control)]
pub struct SuitHud {
    data: HudData,
    time_s: f32,
    dead_for_s: f32,
    returned: bool,
    using_keyboard: bool,
    numbers: Option<Gd<Font>>,
    body: Option<Gd<Font>>,
    glass: Option<Gd<StyleBoxFlat>>,
    pill: Option<Gd<StyleBoxFlat>>,
    visor: Option<Gd<ShaderMaterial>>,
    base: Base<Control>,
}

#[godot_api]
impl IControl for SuitHud {
    fn init(base: Base<Control>) -> Self {
        Self {
            data: crate::data::load(DATA_PATH, DATA_EMBEDDED),
            time_s: 0.0,
            dead_for_s: 0.0,
            returned: false,
            using_keyboard: false,
            numbers: None,
            body: None,
            glass: None,
            pill: None,
            visor: None,
            base,
        }
    }

    fn ready(&mut self) {
        self.base_mut().set_anchors_and_offsets_preset(godot::classes::control::LayoutPreset::FULL_RECT);
        self.base_mut().set_mouse_filter(godot::classes::control::MouseFilter::IGNORE);
        self.numbers = try_load::<FontFile>(NUMBER_FONT).ok().map(|f| f.upcast());
        let theme = ThemeDb::singleton().get_project_theme();
        self.body = theme.and_then(|t| t.get_default_font()).or_else(|| ThemeDb::singleton().get_fallback_font());
        self.glass = Some(style_box(rgba(self.data.glass_color), rgba(self.data.glass_edge_color), 2, 14));
        self.pill = Some(style_box(rgba(self.data.glass_color), Color::from_rgba(1.0, 1.0, 1.0, 0.25), 2, 40));
        self.add_visor();
    }

    fn input(&mut self, event: Gd<InputEvent>) {
        // Show keyboard or controller button names, whichever was used last.
        if event.clone().try_cast::<InputEventKey>().is_ok() || event.clone().try_cast::<InputEventMouseButton>().is_ok() {
            self.using_keyboard = true;
        } else if event.clone().try_cast::<InputEventJoypadButton>().is_ok() {
            self.using_keyboard = false;
        } else if let Ok(m) = event.try_cast::<InputEventJoypadMotion>()
            && m.get_axis_value().abs() > 0.5
        {
            self.using_keyboard = false;
        }
    }

    fn process(&mut self, delta: f64) {
        self.time_s += delta as f32;
        let view = self.view();
        if let Some(v) = &view
            && v.dead_key.is_some()
        {
            self.dead_for_s += delta as f32;
            if !self.returned && self.dead_for_s >= self.data.recap_input_delay_s && Input::singleton().is_action_just_pressed("ui_accept") {
                self.returned = true;
                self.return_to_menu();
            }
        }
        if let (Some(visor), Some(v)) = (self.visor.as_mut(), &view) {
            visor.set_shader_parameter("time_s", &self.time_s.to_variant());
            visor.set_shader_parameter("low_oxygen", &(if v.low && v.dead_key.is_none() { 1.0f32 } else { 0.0 }).to_variant());
            let hurt = if v.dead_key.is_some() { 0.0 } else { (1.0 - v.health).min(1.0) * 0.5 };
            visor.set_shader_parameter("damage", &hurt.to_variant());
        }
        self.base_mut().queue_redraw();
    }

    fn draw(&mut self) {
        let Some(v) = self.view() else { return };
        let (Some(numbers), Some(body)) = (self.numbers.clone(), self.body.clone()) else { return };
        let size = self.base().get_size();
        // Headless runs have no window; nothing to draw.
        if size.y < 100.0 {
            return;
        }
        let s = size.y / 800.0;
        if let Some(key) = &v.dead_key {
            self.draw_recap(&numbers, &body, size, s, key);
            return;
        }
        self.draw_oxygen(&numbers, &body, size, s, &v);
        self.draw_fuel(&numbers, &body, size, s, &v);
        self.draw_health(&body, size, s, &v);
        self.draw_status(&body, size, s, &v);
        self.draw_weapon(&numbers, &body, size, s, &v);
        self.draw_hit_feedback(size, s, &v);
        self.draw_crosshair_and_prompt(&body, size, s, &v);
    }
}

impl SuitHud {
    fn player(&self) -> Option<Gd<PlayerController>> {
        self.base().get_tree().get_first_node_in_group(PLAYER_GROUP).and_then(|n| n.try_cast::<PlayerController>().ok())
    }

    fn view(&self) -> Option<View> {
        let player = self.player()?;
        let weapon = player.get_node_or_null("Head/Arsenal").and_then(|n| n.try_cast::<crate::combat::arsenal::Arsenal>().ok()).map(|a| {
            let a = a.bind();
            WeaponView {
                name_key: a.get_weapon_name_key().to_string(),
                loaded: a.get_loaded(),
                reserve: a.get_reserve(),
                infinite: a.has_infinite_ammo(),
                reload: a.get_reload_fraction(),
                marker: a.get_hit_marker(),
                marker_kind: a.get_hit_kind(),
                pickup: a.get_pickup_prompt().to_string(),
                rope_shots: a.get_rope_shots(),
                carry: a.get_carry_prompt().to_string(),
            }
        });
        let camera = player.get_node_or_null("Head/Camera3D").and_then(|n| n.try_cast::<Node3D>().ok());
        let p = player.bind();
        let hit_dir = match (p.last_hit(), camera) {
            (Some((from, age)), Some(cam)) if age < self.data.damage_arrow_s && !p.is_dead() => {
                let local = cam.get_global_transform().affine_inverse() * from;
                let angle = local.x.atan2(-local.z);
                Some((angle, 1.0 - age / self.data.damage_arrow_s.max(0.01)))
            }
            _ => None,
        };
        Some(View {
            weapon,
            hit_dir,
            oxygen: p.get_oxygen_fraction(),
            oxygen_s: p.get_oxygen_seconds(),
            fuel: p.get_fuel_fraction(),
            health: p.get_health_fraction(),
            tanks: p.get_tanks(),
            max_tanks: p.get_max_tanks(),
            low: p.is_low_oxygen(),
            tethered: p.is_tethered(),
            zero_g: p.is_in_zero_g(),
            at_supply: p.is_at_supply(),
            prompt: p.get_interact_prompt().to_string(),
            dead_key: if p.is_dead() { Some(p.get_death_key()) } else { None },
        })
    }

    /// The visor frame is a sibling drawn under the gauges.
    fn add_visor(&mut self) {
        let Ok(shader) = try_load::<Shader>(VISOR_SHADER) else { return };
        let mut material = ShaderMaterial::new_gd();
        material.set_shader(&shader);
        material.set_shader_parameter("edge_darkness", &self.data.visor_edge_darkness.to_variant());
        material.set_shader_parameter("warn_color", &rgb(self.data.warning_color).to_variant());
        let mut rect = ColorRect::new_alloc();
        rect.set_name("VisorFrame");
        rect.set_material(&material);
        rect.set_mouse_filter(godot::classes::control::MouseFilter::IGNORE);
        rect.set_anchors_and_offsets_preset(godot::classes::control::LayoutPreset::FULL_RECT);
        if let Some(mut parent) = self.base().get_parent() {
            let index = self.base().get_index();
            parent.call_deferred("add_child", &[rect.to_variant()]);
            parent.call_deferred("move_child", &[rect.to_variant(), index.to_variant()]);
        }
        let size = self.base().get_size();
        material.set_shader_parameter("screen_size", &size.to_variant());
        self.visor = Some(material);
    }

    fn canvas(&mut self) -> Gd<CanvasItem> {
        self.base().clone().upcast()
    }

    #[allow(clippy::too_many_arguments)]
    fn text(&mut self, font: &Gd<Font>, at: Vector2, width: f32, align: HorizontalAlignment, text: &str, size: i32, color: Color) {
        let c = self.canvas();
        let shadow = Color::from_rgba(0.0, 0.0, 0.0, 0.55 * color.a);
        c.draw_string_ex(font, at + Vector2::new(0.0, 2.0), &GString::from(text)).alignment(align).width(width).font_size(size).modulate(shadow).done();
        c.draw_string_ex(font, at, &GString::from(text)).alignment(align).width(width).font_size(size).modulate(color).done();
    }

    fn plate(&mut self, rect: Rect2, edge: Option<Color>) {
        let Some(mut glass) = self.glass.clone() else { return };
        if let Some(e) = edge {
            glass.set_border_color(e);
        } else {
            glass.set_border_color(rgba(self.data.glass_edge_color));
        }
        self.canvas().draw_style_box(&glass, rect);
    }

    fn blink(&self) -> bool {
        (self.time_s * self.data.warning_blink_hz).fract() < 0.5
    }

    /// Oxygen: a 270-degree ring with tick marks, the time left in big
    /// numbers, and carried tanks as little capsules.
    fn draw_oxygen(&mut self, numbers: &Gd<Font>, body: &Gd<Font>, size: Vector2, s: f32, v: &View) {
        let d = self.data.clone();
        let (pw, ph) = (d.oxygen_plate_px.0 * s, d.oxygen_plate_px.1 * s);
        let rect = Rect2::new(Vector2::new(d.margin_px * s, size.y - d.margin_px * s - ph), Vector2::new(pw, ph));
        let warn = rgb(d.warning_color);
        let col = if v.low && self.blink() { warn } else { rgb(d.oxygen_color) };
        self.plate(rect, if v.low { Some(faded(warn, 0.8)) } else { None });

        let r = d.oxygen_ring_radius_px * s;
        let center = rect.position + Vector2::new(16.0 * s + r, ph * 0.5);
        let (start, sweep) = (PI * 0.75, PI * 1.5);
        let mut c = self.canvas();
        c.draw_arc_ex(center, r, start, start + sweep, 48, faded(rgb(d.oxygen_color), 0.18)).width(d.oxygen_ring_width_px * s).antialiased(true).done();
        if v.oxygen > 0.0 {
            c.draw_arc_ex(center, r, start, start + sweep * v.oxygen, 48, col).width(d.oxygen_ring_width_px * s).antialiased(true).done();
        }
        for i in 0..=d.oxygen_ticks {
            let a = start + sweep * i as f32 / d.oxygen_ticks as f32;
            let dir = Vector2::new(a.cos(), a.sin());
            let inner = r + d.oxygen_ring_width_px * s * 0.5 + 2.0 * s;
            let len = if i % 3 == 0 { 6.0 } else { 3.0 } * s;
            c.draw_line_ex(center + dir * inner, center + dir * (inner + len), faded(rgb(d.oxygen_color), 0.6)).width(1.5 * s).antialiased(true).done();
        }
        // An "O2" badge in the ring's gap.
        self.text(body, center + Vector2::new(-r, r * 0.95), r * 2.0, HorizontalAlignment::CENTER, "O2", (d.label_font_px as f32 * s) as i32, col);

        let secs = v.oxygen_s.ceil() as i64;
        let left = center.x + r + 18.0 * s;
        let label = self.base().tr("HUD_OXYGEN").to_string();
        self.text(body, Vector2::new(left, rect.position.y + 26.0 * s), 200.0 * s, HorizontalAlignment::LEFT, &label, (d.label_font_px as f32 * s) as i32, faded(rgb(d.oxygen_color), 0.85));
        self.text(numbers, Vector2::new(left, rect.position.y + 62.0 * s), 200.0 * s, HorizontalAlignment::LEFT, &format!("{}:{:02}", secs / 60, secs % 60), (d.number_font_px as f32 * s) as i32, col);

        // Tanks: small capsules, filled when carried.
        let mut c = self.canvas();
        for i in 0..v.max_tanks {
            let x = left + 112.0 * s + i as f32 * 22.0 * s;
            let tank = Rect2::new(Vector2::new(x, rect.position.y + 34.0 * s), Vector2::new(14.0 * s, 30.0 * s));
            let mut sb = style_box(
                if i < v.tanks { rgb(d.oxygen_color) } else { Color::from_rgba(0.0, 0.0, 0.0, 0.0) },
                faded(rgb(d.oxygen_color), if i < v.tanks { 1.0 } else { 0.5 }),
                (2.0 * s) as i32,
                (7.0 * s) as i32,
            );
            sb.set_anti_aliased(true);
            c.draw_style_box(&sb, tank);
        }
        if v.at_supply {
            let text = self.base().tr("HUD_REFILLING").to_string();
            self.text(body, Vector2::new(rect.position.x, rect.position.y - 10.0 * s), pw, HorizontalAlignment::CENTER, &text, (d.label_font_px as f32 * s) as i32, rgb(d.oxygen_color));
        }
    }

    /// Jetpack fuel: an upright segmented bar on its own plate.
    fn draw_fuel(&mut self, numbers: &Gd<Font>, body: &Gd<Font>, size: Vector2, s: f32, v: &View) {
        let d = self.data.clone();
        let (pw, ph) = (d.fuel_plate_px.0 * s, d.fuel_plate_px.1 * s);
        let rect = Rect2::new(Vector2::new(size.x - d.margin_px * s - pw, size.y - d.margin_px * s - ph), Vector2::new(pw, ph));
        let empty = v.fuel <= 0.001;
        let warn = rgb(d.warning_color);
        self.plate(rect, if empty { Some(faded(warn, 0.8)) } else { Some(faded(rgb(d.fuel_color), 0.45)) });
        let col = if empty && self.blink() { warn } else { rgb(d.fuel_color) };

        let bar_w = 22.0 * s;
        let top = rect.position.y + 14.0 * s;
        let bottom = rect.end().y - 14.0 * s;
        let x = rect.position.x + 16.0 * s;
        let n = d.fuel_segments.max(1);
        let gap = 3.0 * s;
        let seg_h = (bottom - top - gap * (n - 1) as f32) / n as f32;
        let filled = v.fuel * n as f32;
        let mut c = self.canvas();
        for i in 0..n {
            let y = bottom - (i + 1) as f32 * seg_h - i as f32 * gap;
            let amount = (filled - i as f32).clamp(0.0, 1.0);
            let seg = Rect2::new(Vector2::new(x, y), Vector2::new(bar_w, seg_h));
            c.draw_rect(seg, faded(rgb(d.fuel_color), 0.15));
            if amount > 0.0 {
                c.draw_rect(Rect2::new(Vector2::new(x, y + seg_h * (1.0 - amount)), Vector2::new(bar_w, seg_h * amount)), col);
            }
        }
        let label = self.base().tr("HUD_JETPACK").to_string();
        let tx = x + bar_w + 8.0 * s;
        let tw = rect.end().x - tx - 6.0 * s;
        self.text(body, Vector2::new(tx, top + 14.0 * s), tw, HorizontalAlignment::CENTER, &label, (d.label_font_px as f32 * s) as i32, faded(rgb(d.fuel_color), 0.9));
        let pct = format!("{}", (v.fuel * 100.0).round() as i32);
        self.text(numbers, Vector2::new(tx, bottom - 8.0 * s), tw, HorizontalAlignment::CENTER, &pct, (26.0 * s) as i32, col);
    }

    /// Suit integrity: a thin segmented line, bottom centre. Only drawn
    /// when damaged, so a healthy suit keeps the view clear.
    fn draw_health(&mut self, body: &Gd<Font>, size: Vector2, s: f32, v: &View) {
        if v.health >= 0.999 {
            return;
        }
        let d = self.data.clone();
        let (w, h) = (d.health_bar_px.0 * s, d.health_bar_px.1 * s);
        let origin = Vector2::new((size.x - w) * 0.5, size.y - d.margin_px * s - h);
        let n = d.health_segments.max(1);
        let gap = 4.0 * s;
        let seg_w = (w - gap * (n - 1) as f32) / n as f32;
        let filled = v.health * n as f32;
        let col = if v.health < 0.35 { rgb(d.warning_color) } else { rgb(d.health_color) };
        let mut c = self.canvas();
        for i in 0..n {
            let x = origin.x + i as f32 * (seg_w + gap);
            c.draw_rect(Rect2::new(Vector2::new(x, origin.y), Vector2::new(seg_w, h)), Color::from_rgba(0.0, 0.0, 0.0, 0.45));
            let amount = (filled - i as f32).clamp(0.0, 1.0);
            if amount > 0.0 {
                c.draw_rect(Rect2::new(Vector2::new(x, origin.y), Vector2::new(seg_w * amount, h)), col);
            }
        }
        let label = self.base().tr("HUD_SUIT").to_string();
        self.text(body, Vector2::new(origin.x, origin.y - 6.0 * s), w, HorizontalAlignment::CENTER, &label, (d.label_font_px as f32 * s) as i32, col);
    }

    /// Status pills, top centre: warnings first.
    fn draw_status(&mut self, body: &Gd<Font>, size: Vector2, s: f32, v: &View) {
        let d = self.data.clone();
        let mut pills: Vec<(String, Color)> = Vec::new();
        if v.low {
            pills.push((self.base().tr("HUD_LOW_OXYGEN").to_string(), rgb(d.warning_color)));
        }
        if v.tethered {
            pills.push((self.base().tr("HUD_TETHERED").to_string(), rgb(d.fuel_color)));
        }
        if v.zero_g {
            pills.push((self.base().tr("HUD_ZERO_G").to_string(), rgb(d.oxygen_color)));
        }
        let font_px = (d.status_font_px as f32 * s) as i32;
        let mut y = d.margin_px * s;
        for (i, (text, color)) in pills.iter().enumerate() {
            let w = body.get_string_size_ex(&GString::from(text.as_str())).font_size(font_px).done().x + 52.0 * s;
            let h = 36.0 * s;
            let rect = Rect2::new(Vector2::new((size.x - w) * 0.5, y), Vector2::new(w, h));
            let pulse = if i == 0 && v.low { 0.55 + 0.45 * (self.time_s * TAU * d.warning_blink_hz * 0.5).sin().abs() } else { 1.0 };
            if let Some(mut pill) = self.pill.clone() {
                pill.set_border_color(faded(*color, 0.85 * pulse));
                self.canvas().draw_style_box(&pill, rect);
            }
            self.canvas().draw_circle(rect.position + Vector2::new(20.0 * s, h * 0.5), 5.0 * s, faded(*color, pulse));
            self.text(body, Vector2::new(rect.position.x + 34.0 * s, rect.position.y + h * 0.5 + font_px as f32 * 0.36), w - 44.0 * s, HorizontalAlignment::LEFT, text, font_px, faded(*color, pulse.max(0.8)));
            y += h + 8.0 * s;
        }
    }

    /// The weapon in hand: name, then loaded / reserve as big numbers, with
    /// a reload bar. Empty magazines blink in the warning colour.
    fn draw_weapon(&mut self, numbers: &Gd<Font>, body: &Gd<Font>, size: Vector2, s: f32, v: &View) {
        let Some(w) = &v.weapon else { return };
        if w.name_key.is_empty() {
            return;
        }
        let d = self.data.clone();
        let (pw, ph) = (d.weapon_plate_px.0 * s, d.weapon_plate_px.1 * s);
        let right = size.x - d.margin_px * s - d.fuel_plate_px.0 * s - 14.0 * s;
        let rect = Rect2::new(Vector2::new(right - pw, size.y - d.margin_px * s - ph), Vector2::new(pw, ph));
        let warn = rgb(d.warning_color);
        let empty = !w.infinite && w.loaded == 0;
        self.plate(rect, Some(faded(if empty { warn } else { rgb(d.ammo_color) }, 0.4)));
        let name = self.base().tr(w.name_key.as_str()).to_string().to_uppercase();
        let pad = 14.0 * s;
        let inner = pw - pad * 2.0;
        self.text(body, rect.position + Vector2::new(pad, 26.0 * s), inner, HorizontalAlignment::LEFT, &name, (d.label_font_px as f32 * s) as i32, faded(rgb(d.ammo_color), 0.85));
        let col = if empty && self.blink() { warn } else { rgb(d.ammo_color) };
        let base_y = rect.end().y - 22.0 * s;
        if w.infinite {
            let inf = self.base().tr("HUD_INFINITE").to_string();
            self.text(numbers, Vector2::new(rect.position.x + pad, base_y), inner, HorizontalAlignment::LEFT, &inf, (d.number_font_px as f32 * s) as i32, col);
        } else {
            let big = (d.number_font_px as f32 * 1.15 * s) as i32;
            let loaded = format!("{}", w.loaded);
            let lw = numbers.get_string_size_ex(&GString::from(loaded.as_str())).font_size(big).done().x;
            self.text(numbers, Vector2::new(rect.position.x + pad, base_y), lw + 4.0, HorizontalAlignment::LEFT, &loaded, big, col);
            let small = (d.number_font_px as f32 * 0.62 * s) as i32;
            let reserve = format!("/ {}", w.reserve);
            self.text(numbers, Vector2::new(rect.position.x + pad + lw + 10.0 * s, base_y), inner - lw, HorizontalAlignment::LEFT, &reserve, small, faded(rgb(d.ammo_color), 0.7));
        }
        if w.reload <= 0.0 {
            // Rope shots left this run, small, top right of the plate.
            let rope = format!("{} {}", self.base().tr("HUD_ROPE"), w.rope_shots);
            let col = if w.rope_shots == 0 { faded(rgb(d.ammo_color), 0.4) } else { faded(rgb(d.fuel_color), 0.9) };
            self.text(body, rect.position + Vector2::new(pad, 26.0 * s), inner, HorizontalAlignment::RIGHT, &rope, (d.label_font_px as f32 * s) as i32, col);
        }
        if w.reload > 0.0 {
            let label = self.base().tr("HUD_RELOADING").to_string();
            self.text(body, rect.position + Vector2::new(pad, 26.0 * s), inner, HorizontalAlignment::RIGHT, &label, (d.label_font_px as f32 * s) as i32, rgb(d.fuel_color));
            let bar = Rect2::new(Vector2::new(rect.position.x + pad, rect.end().y - 12.0 * s), Vector2::new(inner * w.reload.clamp(0.0, 1.0), 4.0 * s));
            self.canvas().draw_rect(bar, rgb(d.fuel_color));
        }
    }

    /// Hit marker ticks around the crosshair (white hit, orange gib, red
    /// kill) and a red arc toward whoever just hurt you (section 12 rule 8:
    /// no unseen hits).
    fn draw_hit_feedback(&mut self, size: Vector2, s: f32, v: &View) {
        let d = self.data.clone();
        let center = size * 0.5;
        if let Some(w) = &v.weapon
            && w.marker > 0.0
        {
            let col = d.hit_colors.get(w.marker_kind.max(0) as usize).copied().unwrap_or((1.0, 1.0, 1.0));
            let a = w.marker.clamp(0.0, 1.0);
            let len = d.hit_marker_px * s * (if w.marker_kind >= 2 { 1.4 } else { 1.0 });
            let gap = 7.0 * s;
            let mut c = self.canvas();
            for (dx, dy) in [(1.0, 1.0), (-1.0, 1.0), (1.0, -1.0), (-1.0, -1.0)] {
                let dir = Vector2::new(dx, dy).normalized();
                let from = center + dir * gap;
                let to = center + dir * (gap + len);
                c.draw_line_ex(from, to, Color::from_rgba(0.0, 0.0, 0.0, 0.5 * a)).width(4.5 * s).antialiased(true).done();
                c.draw_line_ex(from, to, faded(rgb(col), a)).width(2.5 * s).antialiased(true).done();
            }
        }
        if let Some((angle, strength)) = v.hit_dir {
            let r = d.damage_arrow_radius_px * s;
            // Screen up is "ahead"; clockwise is to the right.
            let screen = angle - std::f32::consts::FRAC_PI_2;
            let half = 0.35;
            self.canvas().draw_arc_ex(center, r, screen - half, screen + half, 18, faded(rgb(d.warning_color), strength.clamp(0.0, 1.0) * 0.9)).width(7.0 * s).antialiased(true).done();
            let tip = center + Vector2::new(screen.cos(), screen.sin()) * (r + 14.0 * s);
            let side = Vector2::new(-screen.sin(), screen.cos()) * 9.0 * s;
            let back = center + Vector2::new(screen.cos(), screen.sin()) * (r + 2.0 * s);
            let pts = PackedVector2Array::from(&[tip, back + side, back - side]);
            self.canvas().draw_colored_polygon(&pts, faded(rgb(d.warning_color), strength.clamp(0.0, 1.0) * 0.9));
        }
    }

    /// A small centre dot, and a button prompt under it when something
    /// can be used (section 10: interactables read clearly).
    fn draw_crosshair_and_prompt(&mut self, body: &Gd<Font>, size: Vector2, s: f32, v: &View) {
        let d = self.data.clone();
        let center = size * 0.5;
        let mut c = self.canvas();
        c.draw_circle(center, (d.crosshair_radius_px + 1.2) * s, Color::from_rgba(0.0, 0.0, 0.0, 0.45));
        c.draw_circle(center, d.crosshair_radius_px * s, Color::from_rgba(1.0, 1.0, 1.0, 0.85));

        // "repair:PART_KEY:0.42": hold X to fix that part; the pill fills.
        let carry = v.weapon.as_ref().filter(|w| !w.carry.is_empty()).map(|w| w.carry.clone());
        let pickup = v.weapon.as_ref().filter(|w| !w.pickup.is_empty()).map(|w| format!("WEAPON_{}", w.pickup.to_uppercase()));
        let mut repair: Option<(String, f32)> = None;
        if let Some(rest) = v.prompt.strip_prefix("repair:") {
            let mut it = rest.rsplitn(2, ':');
            let frac = it.next().and_then(|f| f.parse::<f32>().ok()).unwrap_or(0.0);
            let part = it.next().unwrap_or("");
            repair = Some((part.to_string(), frac));
        }
        let (key, pad, keyboard) = match v.prompt.as_str() {
            _ if repair.is_some() => ("PROMPT_REPAIR", "X", "E"),
            "take_tank" => ("PROMPT_TAKE_TANK", "X", "E"),
            "tether" => ("PROMPT_TETHER", "\u{25BC}", "T"),
            "untether" => ("PROMPT_UNTETHER", "\u{25BC}", "T"),
            "buckle" => ("PROMPT_BUCKLE", "X", "E"),
            "unbuckle" => ("PROMPT_UNBUCKLE", "X", "E"),
            "open_hatch" => ("PROMPT_OPEN_HATCH", "X", "E"),
            "close_hatch" => ("PROMPT_CLOSE_HATCH", "X", "E"),
            _ if carry.as_deref() == Some("drop") => ("PROMPT_DROP_TARGET", "X", "E"),
            _ if carry.as_deref() == Some("carry") => ("PROMPT_CARRY_TARGET", "X", "E"),
            _ if pickup.is_some() => ("PROMPT_SWAP_WEAPON", "X", "E"),
            _ => return,
        };
        let glyph = if self.using_keyboard { keyboard } else { pad };
        let mut text = self.base().tr(key).to_string();
        if let Some((part, _)) = &repair {
            text = format!("{text}: {}", self.base().tr(part.as_str()));
        } else if key == "PROMPT_SWAP_WEAPON"
            && let Some(name) = &pickup
        {
            text = format!("{text} {}", self.base().tr(name.as_str()));
        }
        let font_px = (d.prompt_font_px as f32 * s) as i32;
        let tw = body.get_string_size_ex(&GString::from(text.as_str())).font_size(font_px).done().x;
        let h = 40.0 * s;
        let w = tw + h + 30.0 * s;
        let rect = Rect2::new(Vector2::new(center.x - w * 0.5, center.y + 60.0 * s), Vector2::new(w, h));
        if let Some(mut pill) = self.pill.clone() {
            pill.set_border_color(Color::from_rgba(1.0, 1.0, 1.0, 0.35));
            self.canvas().draw_style_box(&pill, rect);
        }
        if let Some((_, frac)) = &repair {
            // Progress along the bottom of the pill, warm like interactables.
            let bar = Rect2::new(rect.position + Vector2::new(h * 0.5, h - 7.0 * s), Vector2::new((w - h) * frac.clamp(0.0, 1.0), 4.0 * s));
            self.canvas().draw_rect(bar, Color::from_rgb(1.0, 0.78, 0.35));
        }
        // Button glyph: X is blue on Xbox-style pads and the Deck.
        let gc = rect.position + Vector2::new(h * 0.5 + 4.0 * s, h * 0.5);
        let glyph_col = if glyph == "X" { Color::from_rgb(0.28, 0.55, 1.0) } else { Color::from_rgb(0.85, 0.87, 0.92) };
        self.canvas().draw_circle(gc, h * 0.38, glyph_col);
        self.text(body, gc + Vector2::new(-h * 0.5, font_px as f32 * 0.36), h, HorizontalAlignment::CENTER, glyph, (font_px as f32 * 0.9) as i32, Color::from_rgb(0.05, 0.06, 0.1));
        self.text(body, Vector2::new(rect.position.x + h + 12.0 * s, rect.position.y + h * 0.5 + font_px as f32 * 0.36), tw + 4.0, HorizontalAlignment::LEFT, &text, font_px, Color::from_rgb(0.95, 0.96, 1.0));
    }

    /// First-person death recap: a dimmed view, "RUN OVER", the cause in
    /// one line, then Continue.
    fn draw_recap(&mut self, numbers: &Gd<Font>, body: &Gd<Font>, size: Vector2, s: f32, key: &GString) {
        let d = self.data.clone();
        let t = (self.dead_for_s / 0.6).min(1.0);
        let mut c = self.canvas();
        c.draw_rect(Rect2::new(Vector2::ZERO, size), Color::from_rgba(0.06, 0.0, 0.02, 0.55 * t));
        let band_h = 210.0 * s;
        let band = Rect2::new(Vector2::new(0.0, size.y * 0.5 - band_h * 0.5), Vector2::new(size.x, band_h));
        c.draw_rect(band, Color::from_rgba(0.02, 0.03, 0.06, 0.82 * t));
        let warn = rgb(d.warning_color);
        c.draw_rect(Rect2::new(band.position, Vector2::new(size.x, 4.0 * s)), faded(warn, t));
        c.draw_rect(Rect2::new(Vector2::new(0.0, band.end().y - 4.0 * s), Vector2::new(size.x, 4.0 * s)), faded(warn, t));

        let title = self.base().tr("DEATH_TITLE").to_string();
        self.text(numbers, Vector2::new(0.0, band.position.y + 82.0 * s), size.x, HorizontalAlignment::CENTER, &title, (d.recap_title_px as f32 * s) as i32, faded(warn, t));
        let cause = self.base().tr(&StringName::from(key)).to_string();
        self.text(body, Vector2::new(0.0, band.position.y + 132.0 * s), size.x, HorizontalAlignment::CENTER, &cause, (d.recap_line_px as f32 * s) as i32, faded(Color::from_rgb(0.95, 0.95, 0.97), t));
        if self.dead_for_s >= d.recap_input_delay_s {
            let hint = self.base().tr(if self.using_keyboard { "DEATH_CONTINUE_KEYS" } else { "DEATH_CONTINUE" }).to_string();
            self.text(body, Vector2::new(0.0, band.position.y + 180.0 * s), size.x, HorizontalAlignment::CENTER, &hint, (20.0 * s) as i32, Color::from_rgba(0.85, 0.87, 0.92, 0.9));
        }
    }

    fn return_to_menu(&mut self) {
        let tree = self.base().get_tree();
        if let Some(mut root) = tree.get_first_node_in_group(GAME_ROOT_GROUP) {
            root.call_deferred("on_player_died_continue", &[]);
        } else if let Some(mut d) = tree.get_first_node_in_group(crate::points::director::DIRECTOR_GROUP) {
            // Scene run on its own (editor F6): straight to the run summary.
            d.call_deferred("show_summary_after_death", &[]);
        }
    }
}

pub(crate) fn style_box(bg: Color, border: Color, border_px: i32, radius: i32) -> Gd<StyleBoxFlat> {
    let mut sb = StyleBoxFlat::new_gd();
    sb.set_bg_color(bg);
    sb.set_border_color(border);
    sb.set_border_width_all(border_px);
    sb.set_corner_radius_all(radius);
    sb.set_corner_detail(6);
    sb.set_anti_aliased(true);
    sb
}

pub(crate) fn rgb((r, g, b): Rgb) -> Color {
    Color::from_rgb(r, g, b)
}

pub(crate) fn rgba((r, g, b, a): Rgba) -> Color {
    Color::from_rgba(r, g, b, a)
}

pub(crate) fn faded(c: Color, alpha: f32) -> Color {
    Color::from_rgba(c.r, c.g, c.b, c.a * alpha)
}
