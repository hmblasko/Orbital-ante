//! `SuitHud`: the helmet visor HUD (CLAUDE.md section 6: diegetic and
//! minimal, oxygen and jetpack fuel on the visor edge, every gauge a
//! different shape) plus the one-line death recap (fun rule 8).
//!
//! PLACEHOLDER look: flat shapes drawn in code. The final TF2-style visor art
//! is a later art task; the layout and readability are what Phase 2 tests.
//! Serves pillar 2 (real danger you can read) and fun rule 5 (clear goals,
//! instant feedback).

use godot::classes::{Control, IControl, Input, ThemeDb};
use godot::global::HorizontalAlignment;
use godot::prelude::*;
use serde::Deserialize;

use super::PlayerController;
use super::controller::PLAYER_GROUP;

const DATA_PATH: &str = "res://data/ui/suit_hud.ron";
const DATA_EMBEDDED: &str = include_str!("../../../../godot/data/ui/suit_hud.ron");

/// Group `GameRoot` joins, so the recap can return to the menu.
pub const GAME_ROOT_GROUP: &str = "game_root";

#[derive(Debug, Clone, Deserialize)]
pub struct HudData {
    /// Gap between the gauges and the screen edge, in pixels at 1280x800.
    pub margin_px: f32,
    pub oxygen_ring_radius_px: f32,
    pub oxygen_ring_width_px: f32,
    pub fuel_bar_size_px: (f32, f32),
    pub health_bar_size_px: (f32, f32),
    pub tank_pip_radius_px: f32,
    pub font_size_px: i32,
    pub recap_font_size_px: i32,
    /// Low-oxygen warning blinks this many times per second.
    pub warning_blink_hz: f32,
    pub oxygen_color: (f32, f32, f32),
    pub fuel_color: (f32, f32, f32),
    pub health_color: (f32, f32, f32),
    pub warning_color: (f32, f32, f32),
    pub track_alpha: f32,
    /// Seconds after death before the recap accepts Continue (stops an
    /// accidental press skipping it).
    pub recap_input_delay_s: f32,
}

#[derive(GodotClass)]
#[class(base = Control)]
pub struct SuitHud {
    data: HudData,
    time_s: f32,
    dead_for_s: f32,
    returned: bool,
    base: Base<Control>,
}

#[godot_api]
impl IControl for SuitHud {
    fn init(base: Base<Control>) -> Self {
        Self { data: crate::data::load(DATA_PATH, DATA_EMBEDDED), time_s: 0.0, dead_for_s: 0.0, returned: false, base }
    }

    fn ready(&mut self) {
        self.base_mut().set_anchors_and_offsets_preset(godot::classes::control::LayoutPreset::FULL_RECT);
        self.base_mut().set_mouse_filter(godot::classes::control::MouseFilter::IGNORE);
    }

    fn process(&mut self, delta: f64) {
        self.time_s += delta as f32;
        if let Some(player) = self.player()
            && player.bind().is_dead()
        {
            self.dead_for_s += delta as f32;
            if !self.returned && self.dead_for_s >= self.data.recap_input_delay_s && Input::singleton().is_action_just_pressed("ui_accept") {
                self.returned = true;
                self.return_to_menu();
            }
        }
        self.base_mut().queue_redraw();
    }

    fn draw(&mut self) {
        let Some(player) = self.player() else { return };
        let p = player.bind();
        let d = self.data.clone();
        let size = self.base().get_size();
        let font = ThemeDb::singleton().get_fallback_font();
        let Some(font) = font else { return };

        if p.is_dead() {
            let key = p.get_death_key();
            drop(p);
            self.draw_recap(&font, size, &key);
            return;
        }

        let low = p.is_low_oxygen();
        let blink_on = (self.time_s * d.warning_blink_hz).fract() < 0.5;
        let warn = rgb(d.warning_color);

        // Oxygen: a ring, bottom left.
        let r = d.oxygen_ring_radius_px;
        let center = Vector2::new(d.margin_px + r, size.y - d.margin_px - r);
        let oxygen_color = if low && blink_on { warn } else { rgb(d.oxygen_color) };
        let start = -std::f32::consts::FRAC_PI_2;
        let full = std::f32::consts::TAU;
        self.base_mut().draw_arc_ex(center, r, start, start + full, 48, faded(rgb(d.oxygen_color), d.track_alpha)).width(d.oxygen_ring_width_px).done();
        let o2 = p.get_oxygen_fraction();
        if o2 > 0.0 {
            self.base_mut().draw_arc_ex(center, r, start, start + full * o2, 48, oxygen_color).width(d.oxygen_ring_width_px).antialiased(true).done();
        }
        let seconds = p.get_oxygen_seconds().ceil() as i64;
        let o2_text = GString::from(&format!("{}:{:02}", seconds / 60, seconds % 60));
        self.centered_text(&font, center + Vector2::new(0.0, d.font_size_px as f32 * 0.35), &o2_text, d.font_size_px, oxygen_color);
        let o2_label = self.base().tr("HUD_OXYGEN");
        self.centered_text(&font, center - Vector2::new(0.0, r + d.font_size_px as f32 * 0.6), &o2_label, d.font_size_px, oxygen_color);

        // Tanks carried: pips to the right of the ring.
        let pip = d.tank_pip_radius_px;
        for i in 0..p.get_max_tanks() {
            let at = Vector2::new(center.x + r + pip * 3.0 + i as f32 * pip * 3.0, center.y);
            if i < p.get_tanks() {
                self.base_mut().draw_circle(at, pip, rgb(d.oxygen_color));
            } else {
                self.base_mut().draw_arc_ex(at, pip, 0.0, full, 16, faded(rgb(d.oxygen_color), 0.6)).width(2.0).done();
            }
        }

        // Jetpack fuel: a vertical bar, bottom right.
        let (fw, fh) = d.fuel_bar_size_px;
        let fuel_rect = Rect2::new(Vector2::new(size.x - d.margin_px - fw, size.y - d.margin_px - fh), Vector2::new(fw, fh));
        self.base_mut().draw_rect(fuel_rect, faded(rgb(d.fuel_color), d.track_alpha));
        let fuel = p.get_fuel_fraction();
        let filled = Rect2::new(Vector2::new(fuel_rect.position.x, fuel_rect.position.y + fh * (1.0 - fuel)), Vector2::new(fw, fh * fuel));
        self.base_mut().draw_rect(filled, rgb(d.fuel_color));
        let fuel_label = self.base().tr("HUD_JETPACK");
        self.centered_text(&font, Vector2::new(fuel_rect.position.x + fw * 0.5, fuel_rect.position.y - 8.0), &fuel_label, d.font_size_px, rgb(d.fuel_color));

        // Health: a thin bar along the bottom edge, centred.
        let (hw, hh) = d.health_bar_size_px;
        let health_rect = Rect2::new(Vector2::new((size.x - hw) * 0.5, size.y - d.margin_px - hh), Vector2::new(hw, hh));
        self.base_mut().draw_rect(health_rect, faded(rgb(d.health_color), d.track_alpha));
        let health = p.get_health_fraction();
        self.base_mut().draw_rect(Rect2::new(health_rect.position, Vector2::new(hw * health, hh)), rgb(d.health_color));

        // Status lines, top centre: the low-oxygen warning and the tether.
        let mut lines: Vec<(String, Color)> = Vec::new();
        if low && blink_on {
            lines.push((self.base().tr("HUD_LOW_OXYGEN").to_string(), warn));
        }
        if p.is_tethered() {
            lines.push((self.base().tr("HUD_TETHERED").to_string(), rgb(d.fuel_color)));
        }
        if p.is_in_zero_g() {
            lines.push((self.base().tr("HUD_ZERO_G").to_string(), rgb(d.oxygen_color)));
        }
        drop(p);
        for (i, (text, color)) in lines.iter().enumerate() {
            let y = d.margin_px + d.font_size_px as f32 * (1.0 + i as f32 * 1.4);
            self.centered_text(&font, Vector2::new(size.x * 0.5, y), &GString::from(text), d.font_size_px, *color);
        }
    }
}

impl SuitHud {
    fn player(&self) -> Option<Gd<PlayerController>> {
        self.base().get_tree().get_first_node_in_group(PLAYER_GROUP).and_then(|n| n.try_cast::<PlayerController>().ok())
    }

    fn centered_text(&mut self, font: &Gd<godot::classes::Font>, at: Vector2, text: &GString, size: i32, color: Color) {
        let width = 600.0;
        self.base_mut()
            .draw_string_ex(font, Vector2::new(at.x - width * 0.5, at.y), text)
            .alignment(HorizontalAlignment::CENTER)
            .width(width)
            .font_size(size)
            .modulate(color)
            .done();
    }

    /// First-person death recap: the cause in one line, then Continue.
    fn draw_recap(&mut self, font: &Gd<godot::classes::Font>, size: Vector2, key: &GString) {
        let d = self.data.clone();
        self.base_mut().draw_rect(Rect2::new(Vector2::ZERO, size), Color::from_rgba(0.0, 0.0, 0.0, 0.6));
        let cause = self.base().tr(&StringName::from(key));
        let mid = size * 0.5;
        self.centered_text(font, mid, &cause, d.recap_font_size_px, rgb(d.warning_color));
        if self.dead_for_s >= d.recap_input_delay_s {
            let hint = self.base().tr("DEATH_CONTINUE");
            self.centered_text(font, mid + Vector2::new(0.0, d.recap_font_size_px as f32 * 1.6), &hint, d.font_size_px, Color::from_rgb(0.9, 0.9, 0.9));
        }
    }

    fn return_to_menu(&mut self) {
        if let Some(mut root) = self.base().get_tree().get_first_node_in_group(GAME_ROOT_GROUP) {
            root.call_deferred("on_player_died_continue", &[]);
        }
    }
}

fn rgb((r, g, b): (f32, f32, f32)) -> Color {
    Color::from_rgb(r, g, b)
}

fn faded(c: Color, alpha: f32) -> Color {
    Color::from_rgba(c.r, c.g, c.b, alpha)
}
