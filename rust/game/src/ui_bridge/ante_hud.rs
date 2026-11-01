//! `AnteHud`: the ante plate (top left: ante, timer, score / target with a
//! slot-machine count-up, the multiplier, the pace meter and taken
//! contracts) and the notification stack (top right). CLAUDE.md section 6:
//! "ante goal, timer and progress always one glance away"; section 18.7:
//! the pace meter keeps risk legible.
//!
//! Serves pillar 3 (every run counts) and fun rule 5 (clear goals, instant
//! feedback).

use godot::classes::{
    CanvasItem, Control, Font, FontFile, IControl, InputEvent, InputEventJoypadButton, InputEventJoypadMotion, InputEventKey,
    InputEventMouseButton, StyleBoxFlat, ThemeDb,
};
use godot::global::HorizontalAlignment;
use godot::obj::Singleton;
use godot::prelude::*;
use serde::Deserialize;

use super::notify::{Notifications, QueueRules};
use crate::player::hud::{Rgb, Rgba, faded, rgb, rgba, style_box};
use crate::points::singleton::Points;

const DATA_PATH: &str = "res://data/ui/ante_hud.ron";
const DATA_EMBEDDED: &str = include_str!("../../../../godot/data/ui/ante_hud.ron");
const NUMBER_FONT: &str = "res://assets/fonts/LilitaOne-Regular.ttf";

#[derive(Debug, Clone, Deserialize)]
pub struct AnteHudData {
    pub margin_px: f32,
    pub plate_px: (f32, f32),
    pub timer_font_px: i32,
    pub score_font_px: i32,
    pub label_font_px: i32,
    pub contract_font_px: i32,
    pub timer_warn_below_s: f64,
    pub count_up_rate: f64,
    pub count_up_min_rate: f64,
    pub note_width_px: f32,
    pub note_height_px: f32,
    pub note_font_px: i32,
    pub note_big_font_px: i32,
    pub note_max_visible: usize,
    pub note_life_s: f32,
    pub note_max_waiting: usize,
    pub note_slide_s: f32,
    pub note_colors: Vec<Rgb>,
    pub text_color: Rgb,
    pub target_color: Rgb,
    pub multiplier_color: Rgb,
    pub pace_colors: Vec<Rgb>,
    pub warning_color: Rgb,
    pub glass_color: Rgba,
    pub glass_edge_color: Rgba,
    pub blink_hz: f32,
}

/// One frame's snapshot of the run.
struct RunView {
    ante: i64,
    time_left: f64,
    score: i64,
    target: i64,
    multiplier: f64,
    pace: String,
    contracts: Vec<(String, i64, i64, bool)>,
    cash_out: Option<i64>,
}

#[derive(GodotClass)]
#[class(base = Control)]
pub struct AnteHud {
    data: AnteHudData,
    time_s: f32,
    shown_score: f64,
    using_keyboard: bool,
    numbers: Option<Gd<Font>>,
    body: Option<Gd<Font>>,
    glass: Option<Gd<StyleBoxFlat>>,
    base: Base<Control>,
}

#[godot_api]
impl IControl for AnteHud {
    fn init(base: Base<Control>) -> Self {
        Self {
            data: crate::data::load(DATA_PATH, DATA_EMBEDDED),
            time_s: 0.0,
            shown_score: 0.0,
            using_keyboard: false,
            numbers: None,
            body: None,
            glass: None,
            base,
        }
    }

    fn ready(&mut self) {
        self.base_mut().set_anchors_and_offsets_preset(godot::classes::control::LayoutPreset::FULL_RECT);
        self.base_mut().set_mouse_filter(godot::classes::control::MouseFilter::IGNORE);
        // Keep redrawing while a between-ante screen pauses the world, so
        // the plate and old notes don't linger frozen under it.
        self.base_mut().set_process_mode(godot::classes::node::ProcessMode::ALWAYS);
        self.numbers = try_load::<FontFile>(NUMBER_FONT).ok().map(|f| f.upcast());
        let theme = ThemeDb::singleton().get_project_theme();
        self.body = theme.and_then(|t| t.get_default_font()).or_else(|| ThemeDb::singleton().get_fallback_font());
        self.glass = Some(style_box(rgba(self.data.glass_color), rgba(self.data.glass_edge_color), 2, 14));
        let d = &self.data;
        Notifications::singleton().bind_mut().set_rules(QueueRules { max_visible: d.note_max_visible, life_s: d.note_life_s, max_waiting: d.note_max_waiting });
    }

    fn input(&mut self, event: Gd<InputEvent>) {
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
        Notifications::singleton().bind_mut().step(delta as f32);
        let target = Points::singleton().bind().get_score() as f64;
        if target < self.shown_score {
            // A new ante: snap back down.
            self.shown_score = target;
        } else {
            let gap = target - self.shown_score;
            let step = (gap * self.data.count_up_rate).max(self.data.count_up_min_rate) * delta;
            self.shown_score = (self.shown_score + step).min(target);
        }
        self.base_mut().queue_redraw();
    }

    fn draw(&mut self) {
        let (Some(numbers), Some(body)) = (self.numbers.clone(), self.body.clone()) else { return };
        let size = self.base().get_size();
        if size.y < 100.0 {
            return;
        }
        let s = size.y / 800.0;
        if let Some(v) = self.run_view() {
            self.draw_plate(&numbers, &body, s, &v);
        }
        self.draw_notes(&body, size, s);
    }
}

#[godot_api]
impl AnteHud {
    /// The score as the count-up shows it right now (tests).
    #[func]
    pub fn get_shown_score(&self) -> i64 {
        self.shown_score.round() as i64
    }
}

impl AnteHud {
    fn run_view(&self) -> Option<RunView> {
        let p = Points::singleton();
        let p = p.bind();
        if p.get_phase().to_string() != "playing" {
            return None;
        }
        let contracts = p
            .get_contracts()
            .iter_shared()
            .filter_map(|v| v.try_to::<VarDictionary>().ok())
            .map(|d| {
                let title = d.get("title").map(|v| v.to_string()).unwrap_or_default();
                let progress = d.get("progress").and_then(|v| v.try_to::<i64>().ok()).unwrap_or(0);
                let count = d.get("count").and_then(|v| v.try_to::<i64>().ok()).unwrap_or(1);
                let done = d.get("done").and_then(|v| v.try_to::<bool>().ok()).unwrap_or(false);
                (title, progress, count, done)
            })
            .collect();
        let cash_out = if p.can_cash_out() && self.player_buckled() { Some(p.get_cash_out_value()) } else { None };
        Some(RunView {
            ante: p.get_ante(),
            time_left: p.get_time_left(),
            score: p.get_score(),
            target: p.get_target(),
            multiplier: p.get_multiplier(),
            pace: p.get_pace().to_string(),
            contracts,
            cash_out,
        })
    }

    fn player_buckled(&self) -> bool {
        self.base()
            .get_tree()
            .get_first_node_in_group(crate::player::controller::PLAYER_GROUP)
            .and_then(|n| n.try_cast::<crate::player::PlayerController>().ok())
            .is_some_and(|p| p.bind().is_buckled())
    }

    fn canvas(&self) -> Gd<CanvasItem> {
        self.base().clone().upcast()
    }

    #[allow(clippy::too_many_arguments)]
    fn text(&self, font: &Gd<Font>, at: Vector2, width: f32, align: HorizontalAlignment, text: &str, size: i32, color: Color) {
        let c = self.canvas();
        let shadow = Color::from_rgba(0.0, 0.0, 0.0, 0.55 * color.a);
        c.clone().draw_string_ex(font, at + Vector2::new(0.0, 2.0), &GString::from(text)).alignment(align).width(width).font_size(size).modulate(shadow).done();
        c.clone().draw_string_ex(font, at, &GString::from(text)).alignment(align).width(width).font_size(size).modulate(color).done();
    }

    fn tr(&self, key: &str) -> String {
        self.base().tr(key).to_string()
    }

    fn format(&self, v: i64) -> String {
        Points::singleton().bind().format(v).to_string()
    }

    fn draw_plate(&self, numbers: &Gd<Font>, body: &Gd<Font>, s: f32, v: &RunView) {
        let d = &self.data;
        let (pw, ph) = (d.plate_px.0 * s, d.plate_px.1 * s);
        let rect = Rect2::new(Vector2::splat(d.margin_px * s), Vector2::new(pw, ph));
        let warn = rgb(d.warning_color);
        let low_time = v.time_left < d.timer_warn_below_s;
        let blink = (self.time_s * d.blink_hz).fract() < 0.5;
        if let Some(mut glass) = self.glass.clone() {
            glass.set_border_color(if low_time { faded(warn, 0.7) } else { rgba(d.glass_edge_color) });
            self.canvas().draw_style_box(&glass, rect);
        }
        let pad = 14.0 * s;
        let inner = pw - pad * 2.0;
        let text = rgb(d.text_color);
        let label_px = (d.label_font_px as f32 * s) as i32;
        // Row 1: ANTE n (left), clock (right).
        let ante = format!("{} {}", self.tr("HUD_ANTE"), v.ante);
        self.text(body, rect.position + Vector2::new(pad, 24.0 * s), inner, HorizontalAlignment::LEFT, &ante.to_uppercase(), label_px, faded(text, 0.85));
        let secs = v.time_left.max(0.0).ceil() as i64;
        let clock = format!("{}:{:02}", secs / 60, secs % 60);
        let clock_col = if low_time && blink { warn } else if low_time { faded(warn, 0.8) } else { text };
        self.text(numbers, rect.position + Vector2::new(pad, 40.0 * s), inner, HorizontalAlignment::RIGHT, &clock, (d.timer_font_px as f32 * s) as i32, clock_col);
        // Row 2: score / target.
        let done = v.score >= v.target;
        let score_px = (d.score_font_px as f32 * s) as i32;
        let shown = self.shown_score.round() as i64;
        let score_txt = self.format(shown);
        let sw = numbers.get_string_size_ex(&GString::from(score_txt.as_str())).font_size(score_px).done().x;
        let y2 = rect.position.y + 74.0 * s;
        self.text(numbers, Vector2::new(rect.position.x + pad, y2), sw + 4.0, HorizontalAlignment::LEFT, &score_txt, score_px, if done { rgb(d.target_color) } else { text });
        let target_txt = format!("/ {}", self.format(v.target));
        self.text(numbers, Vector2::new(rect.position.x + pad + sw + 8.0 * s, y2), inner, HorizontalAlignment::LEFT, &target_txt, (score_px as f32 * 0.72) as i32, faded(text, 0.6));
        // Multiplier, right of row 2.
        let mult = format!("x{:.1}", v.multiplier);
        self.text(numbers, Vector2::new(rect.position.x + pad, y2), inner, HorizontalAlignment::RIGHT, &mult, score_px, rgb(d.multiplier_color));
        // Progress bar.
        let bar_y = y2 + 10.0 * s;
        let frac = (self.shown_score / v.target.max(1) as f64).clamp(0.0, 1.0) as f32;
        self.canvas().draw_rect(Rect2::new(Vector2::new(rect.position.x + pad, bar_y), Vector2::new(inner, 6.0 * s)), Color::from_rgba(1.0, 1.0, 1.0, 0.12));
        self.canvas().draw_rect(Rect2::new(Vector2::new(rect.position.x + pad, bar_y), Vector2::new(inner * frac, 6.0 * s)), if done { rgb(d.target_color) } else { rgb(d.multiplier_color) });
        // Row 3: pace (or "target reached").
        let (pace_key, pace_col) = if done {
            ("HUD_TARGET_REACHED", rgb(d.target_color))
        } else {
            match v.pace.as_str() {
                "ahead" => ("HUD_PACE_AHEAD", rgb(d.pace_colors[0])),
                "behind" => ("HUD_PACE_BEHIND", rgb(d.pace_colors[2])),
                _ => ("HUD_PACE_ON", rgb(d.pace_colors[1])),
            }
        };
        self.text(body, Vector2::new(rect.position.x + pad, rect.end().y - 10.0 * s), inner, HorizontalAlignment::LEFT, &self.tr(pace_key), label_px, pace_col);
        // Contracts under the plate.
        let cpx = (d.contract_font_px as f32 * s) as i32;
        let mut y = rect.end().y + 22.0 * s;
        for (title, progress, count, finished) in &v.contracts {
            let mark = if *finished { "\u{2713}".to_string() } else { format!("{}/{}", progress.min(count), count) };
            let col = if *finished { rgb(d.target_color) } else { faded(text, 0.85) };
            self.text(body, Vector2::new(rect.position.x + 4.0 * s, y), pw - 8.0 * s, HorizontalAlignment::LEFT, title, cpx, col);
            self.text(body, Vector2::new(rect.position.x + 4.0 * s, y), pw - 8.0 * s, HorizontalAlignment::RIGHT, &mark, cpx, col);
            y += cpx as f32 * 1.4;
        }
        // Cash-out prompt (buckled in, target reached).
        if let Some(value) = v.cash_out {
            let glyph = if self.using_keyboard { "Enter" } else { "View" };
            let line = format!("[{}] {}  +{}", glyph, self.tr("PROMPT_CASH_OUT"), self.format(value));
            self.text(body, Vector2::new(rect.position.x + 4.0 * s, y + 4.0 * s), pw * 1.4, HorizontalAlignment::LEFT, &line, (d.label_font_px as f32 * 1.15 * s) as i32, rgb(d.target_color));
        }
    }

    fn draw_notes(&self, body: &Gd<Font>, size: Vector2, s: f32) {
        let d = &self.data;
        let notes: Vec<_> = Notifications::singleton().bind().shown().to_vec();
        let (w, h) = (d.note_width_px * s, d.note_height_px * s);
        let mut y = d.margin_px * s;
        for n in &notes {
            let big = matches!(n.kind, super::notify::Kind::BigPoints | super::notify::Kind::Discovery | super::notify::Kind::Contract);
            let col = rgb(d.note_colors.get(n.kind.index()).copied().unwrap_or(d.text_color));
            // Slide in from the right, fade out at the end.
            let t_in = (n.age_s / d.note_slide_s.max(0.01)).min(1.0);
            let t_out = ((d.note_life_s - n.age_s) / d.note_slide_s.max(0.01)).clamp(0.0, 1.0);
            let ease = 1.0 - (1.0 - t_in).powi(3);
            let alpha = t_out;
            let x = size.x - d.margin_px * s - w + (1.0 - ease) * (w * 0.4);
            let hh = if big { h * 1.15 } else { h };
            let rect = Rect2::new(Vector2::new(x, y), Vector2::new(w, hh));
            if let Some(mut glass) = self.glass.clone() {
                glass.set_bg_color(faded(rgba(d.glass_color), alpha));
                glass.set_border_color(faded(col, 0.55 * alpha));
                self.canvas().draw_style_box(&glass, rect);
            }
            // The kind's icon: a coloured pip (shape plus colour, so it reads
            // for colourblind players too: points round, big ones a diamond).
            let c = rect.position + Vector2::new(16.0 * s, hh * 0.5);
            if big {
                let r = 6.0 * s;
                let pts = PackedVector2Array::from(&[c + Vector2::new(0.0, -r), c + Vector2::new(r, 0.0), c + Vector2::new(0.0, r), c + Vector2::new(-r, 0.0)][..]);
                self.canvas().draw_colored_polygon(&pts, faded(col, alpha));
            } else {
                self.canvas().draw_circle(c, 4.5 * s, faded(col, alpha));
            }
            let px = ((if big { d.note_big_font_px } else { d.note_font_px }) as f32 * s) as i32;
            let mut text = n.text.clone();
            if n.count > 1 {
                text = format!("{text}  x{}", n.count);
            }
            self.text(body, Vector2::new(rect.position.x + 30.0 * s, rect.position.y + hh * 0.5 + px as f32 * 0.36), w - 40.0 * s, HorizontalAlignment::LEFT, &text, px, faded(col, alpha));
            y += hh + 6.0 * s;
        }
    }
}
