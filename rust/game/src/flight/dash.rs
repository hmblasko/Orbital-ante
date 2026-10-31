//! `ShipDash`: the cockpit dashboard screen (CLAUDE.md section 5: Outer
//! Wilds-like readouts, whimsical and easy to read, every gauge a different
//! shape, three switchable styles including a black screen with green text).
//!
//! It draws into its own SubViewport, which the ship shows on the console.
//! The ship hands it a `DashReadout` every tick; styles and scales come from
//! res://data/ships/dash.ron.
//! Serves pillar 4 (physical flight you can read) and fun rule 5 (clear
//! goals, instant feedback).

use std::f32::consts::{PI, TAU};

use godot::classes::{Control, Font, FontFile, IControl, StyleBoxFlat};
use godot::global::HorizontalAlignment;
use godot::prelude::*;
use serde::Deserialize;

use crate::player::hud::{Rgb, Rgba, faded, rgb, rgba, style_box};

pub const DASH_DATA_PATH: &str = "res://data/ships/dash.ron";
const DASH_EMBEDDED: &str = include_str!("../../../../godot/data/ships/dash.ron");

#[derive(Debug, Clone, Copy, PartialEq, Eq, Deserialize)]
pub enum DashKind {
    Toon,
    Analog,
    Terminal,
}

#[derive(Debug, Clone, Deserialize)]
pub struct DashStyle {
    pub kind: DashKind,
    pub name_key: String,
    pub number_font: String,
    pub label_font: String,
    pub background: Rgb,
    pub panel: Rgba,
    pub panel_edge: Rgba,
    pub text: Rgb,
    pub dim: Rgb,
    pub speed: Rgb,
    pub altitude: Rgb,
    pub fuel: Rgb,
    pub oxygen: Rgb,
    pub drift: Rgb,
    pub warning: Rgb,
    pub needle: Rgb,
    pub corner_px: i32,
    pub scanlines: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct DashData {
    pub viewport_px: (i32, i32),
    pub default_style: usize,
    pub speed_full_scale_mps: f32,
    pub vertical_full_scale_mps: f32,
    pub altitude_tape_span_m: f32,
    pub drift_min_mps: f32,
    pub low_fuel: f32,
    pub low_oxygen: f32,
    /// The hull chip turns to a warning below this.
    pub low_hull: f32,
    pub warning_blink_hz: f32,
    pub style_banner_s: f32,
    pub styles: Vec<DashStyle>,
}

pub fn load_dash_data() -> DashData {
    crate::data::load(DASH_DATA_PATH, DASH_EMBEDDED)
}

/// What the dash shows, filled in by the ship each tick.
#[derive(Debug, Clone, Default)]
pub struct DashReadout {
    /// Speed relative to the body below (m/s).
    pub speed: f32,
    /// Climb rate, + up (m/s).
    pub vertical: f32,
    /// Height above the ground under the ship, if near a body.
    pub altitude: Option<f32>,
    /// Translation key of the body below (BODY_EARTH...), if any.
    pub body_key: Option<String>,
    /// Ship-space velocity: x = pilot's right, y = up, z = forward.
    pub drift: Vector3,
    pub fuel: f32,
    pub oxygen: f32,
    pub matching: bool,
    pub landed: bool,
    /// Locked target, if any.
    pub target: Option<TargetReadout>,
    /// Hull core health 0..1, and whether any part is damaged or missing.
    pub hull: f32,
    pub damaged: bool,
}

/// What the dash shows about the locked target.
#[derive(Debug, Clone, Default)]
pub struct TargetReadout {
    /// Translation key of the body (BODY_MOON...).
    pub body_key: String,
    /// Distance to the autopilot's arrival point above it (m).
    pub distance: f32,
    /// How fast that distance is shrinking (m/s, + closing).
    pub closing: f32,
    pub autopilot: bool,
    pub holding: bool,
}

#[derive(GodotClass)]
#[class(base = Control)]
pub struct ShipDash {
    data: DashData,
    style: usize,
    numbers: Vec<Option<Gd<Font>>>,
    labels: Vec<Option<Gd<Font>>>,
    readout: DashReadout,
    clock: f32,
    banner: f32,
    base: Base<Control>,
}

#[godot_api]
impl IControl for ShipDash {
    fn init(base: Base<Control>) -> Self {
        let data = load_dash_data();
        let style = data.default_style.min(data.styles.len().saturating_sub(1));
        Self { data, style, numbers: Vec::new(), labels: Vec::new(), readout: DashReadout::default(), clock: 0.0, banner: 0.0, base }
    }

    fn ready(&mut self) {
        let font = |p: &str| try_load::<FontFile>(p).ok().map(|f| f.upcast::<Font>());
        self.numbers = self.data.styles.iter().map(|s| font(&s.number_font)).collect();
        self.labels = self.data.styles.iter().map(|s| font(&s.label_font)).collect();
        let (w, h) = self.data.viewport_px;
        self.base_mut().set_size(Vector2::new(w as f32, h as f32));
    }

    fn process(&mut self, delta: f64) {
        self.clock += delta as f32;
        self.banner = (self.banner - delta as f32).max(0.0);
        self.base_mut().queue_redraw();
    }

    fn draw(&mut self) {
        let Some(style) = self.data.styles.get(self.style).cloned() else { return };
        let (Some(Some(num)), Some(Some(lab))) = (self.numbers.get(self.style).cloned(), self.labels.get(self.style).cloned()) else {
            return;
        };
        let size = self.base().get_size();
        let mut p = Painter { c: self.to_gd().upcast::<Control>(), num, lab, style: style.clone() };
        p.c.draw_rect(Rect2::new(Vector2::ZERO, size), rgb(style.background));
        let r = self.readout.clone();
        let blink = (self.clock * self.data.warning_blink_hz * TAU).sin() > -0.2;
        let d = self.data.clone();
        let names = Names::new(&p.c.clone(), &r);
        match style.kind {
            DashKind::Toon => draw_toon(&mut p, &d, &r, &names, blink),
            DashKind::Analog => draw_analog(&mut p, &d, &r, &names, blink),
            DashKind::Terminal => draw_terminal(&mut p, &d, &r, &names, blink, self.clock),
        }
        if style.scanlines > 0.0 {
            let mut y = 0.0;
            while y < size.y {
                p.c.draw_rect(Rect2::new(Vector2::new(0.0, y), Vector2::new(size.x, 2.0)), Color::from_rgba(0.0, 0.0, 0.0, style.scanlines));
                y += 5.0;
            }
        }
        if self.banner > 0.0 {
            let name = self.base().tr(&style.name_key).to_string();
            let a = (self.banner / 0.4).min(1.0);
            let rect = Rect2::new(Vector2::new(size.x * 0.3, size.y * 0.4), Vector2::new(size.x * 0.4, 90.0));
            let sb = style_box(faded(rgba(style.panel), a), faded(rgba(style.panel_edge), a), 4, style.corner_px);
            p.c.draw_style_box(&sb, rect);
            p.text(&p.num.clone(), rect.position + Vector2::new(0.0, 62.0), rect.size.x, HorizontalAlignment::CENTER, &name, 48, faded(rgb(style.text), a));
        }
    }
}

#[godot_api]
impl ShipDash {
    /// Switch to the next style (F4 aboard).
    #[func]
    pub fn cycle_style(&mut self) {
        if !self.data.styles.is_empty() {
            self.style = (self.style + 1) % self.data.styles.len();
            self.banner = self.data.style_banner_s;
        }
    }

    #[func]
    pub fn get_style_index(&self) -> i64 {
        self.style as i64
    }

    #[func]
    pub fn set_style_index(&mut self, i: i64) {
        if !self.data.styles.is_empty() {
            self.style = (i.max(0) as usize) % self.data.styles.len();
        }
    }

    #[func]
    pub fn get_style_count(&self) -> i64 {
        self.data.styles.len() as i64
    }
}

impl ShipDash {
    pub fn set_readout(&mut self, r: DashReadout) {
        self.readout = r;
    }

    pub fn viewport_px(&self) -> (i32, i32) {
        self.data.viewport_px
    }
}

/// Translated labels for one frame.
struct Names {
    speed: String,
    vertical: String,
    altitude: String,
    fuel: String,
    oxygen: String,
    drift: String,
    matching: String,
    landed: String,
    low_fuel: String,
    low_oxygen: String,
    body: String,
    terminal_title: String,
    autopilot: String,
    holding: String,
    target: String,
    hull: String,
    damaged: String,
}

impl Names {
    fn new(c: &Gd<Control>, r: &DashReadout) -> Self {
        let tr = |k: &str| c.tr(k).to_string();
        Self {
            speed: tr("DASH_SPEED"),
            vertical: tr("DASH_VERTICAL"),
            altitude: tr("DASH_ALTITUDE"),
            fuel: tr("DASH_FUEL"),
            oxygen: tr("DASH_OXYGEN"),
            drift: tr("DASH_DRIFT"),
            matching: tr("DASH_MATCHING"),
            landed: tr("DASH_LANDED"),
            low_fuel: tr("DASH_LOW_FUEL"),
            low_oxygen: tr("DASH_LOW_OXYGEN"),
            body: r.body_key.as_deref().map_or_else(|| tr("DASH_DEEP_SPACE"), tr),
            terminal_title: tr("DASH_TERMINAL_TITLE"),
            autopilot: tr("DASH_AUTOPILOT"),
            hull: format!("{} {:.0}%", tr("DASH_HULL"), r.hull * 100.0),
            damaged: tr("DASH_DAMAGED"),
            holding: tr("DASH_HOLDING"),
            target: r.target.as_ref().map_or_else(String::new, |t| target_line(&tr(&t.body_key), t)),
        }
    }
}

struct Painter {
    c: Gd<Control>,
    num: Gd<Font>,
    lab: Gd<Font>,
    style: DashStyle,
}

impl Painter {
    #[allow(clippy::too_many_arguments)]
    fn text(&mut self, font: &Gd<Font>, at: Vector2, width: f32, align: HorizontalAlignment, text: &str, px: i32, color: Color) {
        self.c.draw_string_ex(font, at, &GString::from(text)).alignment(align).width(width).font_size(px).modulate(color).done();
    }

    fn panel(&mut self, rect: Rect2) {
        let sb: Gd<StyleBoxFlat> = style_box(rgba(self.style.panel), rgba(self.style.panel_edge), 4, self.style.corner_px);
        self.c.draw_style_box(&sb, rect);
    }

    fn ring(&mut self, center: Vector2, radius: f32, from: f32, to: f32, color: Color, width: f32) {
        if (to - from).abs() < 1e-4 {
            return;
        }
        let points = ((to - from).abs() / TAU * 96.0).max(8.0) as i32;
        self.c.draw_arc_ex(center, radius, from, to, points, color).width(width).antialiased(true).done();
    }

    fn line(&mut self, a: Vector2, b: Vector2, color: Color, width: f32) {
        self.c.draw_line_ex(a, b, color).width(width).antialiased(true).done();
    }

    fn poly(&mut self, pts: &[Vector2], color: Color) {
        let mut arr = PackedVector2Array::new();
        for p in pts {
            arr.push(*p);
        }
        self.c.draw_colored_polygon(&arr, color);
    }

    fn label(&mut self, at: Vector2, width: f32, text: &str, px: i32, color: Color) {
        let f = self.lab.clone();
        self.text(&f, at, width, HorizontalAlignment::CENTER, text, px, color);
    }

    fn number(&mut self, at: Vector2, width: f32, align: HorizontalAlignment, text: &str, px: i32, color: Color) {
        let f = self.num.clone();
        self.text(&f, at, width, align, text, px, color);
    }

    /// A needle from `center`, `angle` radians (0 = right, clockwise in
    /// screen space), with a hub.
    fn needle(&mut self, center: Vector2, len: f32, angle: f32, color: Color, width: f32) {
        let dir = Vector2::new(angle.cos(), angle.sin());
        let side = Vector2::new(-dir.y, dir.x) * width * 0.5;
        self.poly(&[center + side, center + dir * len, center - side, center - dir * len * 0.18], color);
        self.c.draw_circle(center, width * 0.9, color);
    }

    /// The drift ball: where the ship is sliding, seen from the pilot.
    fn drift_ball(&mut self, center: Vector2, radius: f32, r: &DashReadout, min_speed: f32, face: Color, ink: Color, mark: Color) {
        self.c.draw_circle(center, radius, face);
        self.ring(center, radius, 0.0, TAU, ink, 3.0);
        self.ring(center, radius * 0.5, 0.0, TAU, faded(ink, 0.45), 2.0);
        self.line(center - Vector2::new(radius, 0.0), center + Vector2::new(radius, 0.0), faded(ink, 0.45), 2.0);
        self.line(center - Vector2::new(0.0, radius), center + Vector2::new(0.0, radius), faded(ink, 0.45), 2.0);
        if r.speed < min_speed {
            self.c.draw_circle(center, radius * 0.12, mark);
            return;
        }
        let v = r.drift / r.speed.max(1e-3);
        // Sideways and vertical parts place the marker; forward vs back
        // picks a solid dot (sliding ahead) or a hollow ring (sliding back).
        let at = center + Vector2::new(v.x, -v.y) * radius * 0.92;
        if v.z >= 0.0 {
            self.c.draw_circle(at, radius * 0.13, mark);
        } else {
            self.ring(at, radius * 0.13, 0.0, TAU, mark, 5.0);
            self.line(at - Vector2::new(radius * 0.09, radius * 0.09), at + Vector2::new(radius * 0.09, radius * 0.09), mark, 4.0);
            self.line(at - Vector2::new(radius * 0.09, -radius * 0.09), at + Vector2::new(radius * 0.09, -radius * 0.09), mark, 4.0);
        }
    }
}

fn fmt_speed(v: f32) -> String {
    if v < 100.0 { format!("{v:.1}") } else { format!("{v:.0}") }
}

fn fmt_alt(a: Option<f32>) -> String {
    match a {
        Some(a) if a < 10000.0 => format!("{:.0} m", a.max(0.0)),
        Some(a) => format!("{:.1} km", a / 1000.0),
        None => "---".into(),
    }
}

/// "Moon 1.9 km  84 m/s" with a sign on the speed when moving away.
fn target_line(name: &str, t: &TargetReadout) -> String {
    let speed = if t.closing >= 0.0 { fmt_speed(t.closing) } else { format!("-{}", fmt_speed(-t.closing)) };
    format!("{name} {}  {speed} m/s", fmt_alt(Some(t.distance)))
}

fn status_chips(r: &DashReadout, d: &DashData, n: &Names, blink: bool) -> Vec<(String, bool)> {
    let mut chips = Vec::new();
    if r.fuel < d.low_fuel && blink {
        chips.push((n.low_fuel.clone(), true));
    }
    if r.oxygen < d.low_oxygen && blink {
        chips.push((n.low_oxygen.clone(), true));
    }
    if r.hull < 1.0 {
        chips.push((n.hull.clone(), r.hull < d.low_hull));
    } else if r.damaged {
        chips.push((n.damaged.clone(), false));
    }
    if let Some(t) = &r.target {
        if t.autopilot {
            chips.push((n.autopilot.clone(), false));
        } else if t.holding {
            chips.push((n.holding.clone(), false));
        }
        chips.push((n.target.clone(), false));
    }
    if r.matching {
        chips.push((n.matching.clone(), false));
    }
    if r.landed {
        chips.push((n.landed.clone(), false));
    }
    chips
}

fn draw_toon(p: &mut Painter, d: &DashData, r: &DashReadout, n: &Names, blink: bool) {
    let s = p.style.clone();
    let (text, dim, warn) = (rgb(s.text), rgb(s.dim), rgb(s.warning));
    // Drift ball, left.
    p.panel(Rect2::new(Vector2::new(18.0, 18.0), Vector2::new(300.0, 476.0)));
    p.label(Vector2::new(18.0, 66.0), 300.0, &n.drift, 34, dim);
    p.drift_ball(Vector2::new(168.0, 270.0), 118.0, r, d.drift_min_mps, Color::from_rgb(0.08, 0.10, 0.13), faded(rgb(s.drift), 0.8), rgb(s.drift));
    p.label(Vector2::new(18.0, 466.0), 300.0, &n.body, 34, text);

    // Speed, centre.
    p.panel(Rect2::new(Vector2::new(334.0, 18.0), Vector2::new(330.0, 300.0)));
    p.label(Vector2::new(334.0, 64.0), 330.0, &n.speed, 32, dim);
    p.number(Vector2::new(334.0, 182.0), 330.0, HorizontalAlignment::CENTER, &fmt_speed(r.speed), 120, rgb(s.speed));
    p.label(Vector2::new(334.0, 224.0), 330.0, "m/s", 30, dim);
    // Climb rate as a fat arrow and number.
    let up = r.vertical >= 0.0;
    let ac = if up { rgb(s.altitude) } else { rgb(s.speed) };
    let ax = 420.0;
    let ay = 272.0;
    let tri = if up {
        [Vector2::new(ax, ay - 22.0), Vector2::new(ax + 20.0, ay + 10.0), Vector2::new(ax - 20.0, ay + 10.0)]
    } else {
        [Vector2::new(ax, ay + 18.0), Vector2::new(ax + 20.0, ay - 14.0), Vector2::new(ax - 20.0, ay - 14.0)]
    };
    p.poly(&tri, ac);
    p.number(Vector2::new(448.0, 290.0), 200.0, HorizontalAlignment::LEFT, &format!("{} {}", fmt_speed(r.vertical.abs()), n.vertical), 40, ac);

    // Status chips, centre bottom.
    p.panel(Rect2::new(Vector2::new(334.0, 334.0), Vector2::new(330.0, 160.0)));
    let chips = status_chips(r, d, n, blink);
    for (i, (label, bad)) in chips.iter().take(3).enumerate() {
        let y = 352.0 + i as f32 * 46.0;
        let col = if *bad { warn } else { rgb(s.drift) };
        let sb = style_box(faded(col, 0.22), col, 3, 18);
        p.c.draw_style_box(&sb, Rect2::new(Vector2::new(360.0, y), Vector2::new(278.0, 38.0)));
        p.label(Vector2::new(360.0, y + 30.0), 278.0, label, 30, col);
    }

    // Altitude tape, right.
    p.panel(Rect2::new(Vector2::new(680.0, 18.0), Vector2::new(120.0, 476.0)));
    p.label(Vector2::new(680.0, 60.0), 120.0, &n.altitude, 28, dim);
    let tape = Rect2::new(Vector2::new(700.0, 82.0), Vector2::new(80.0, 340.0));
    p.c.draw_rect(tape, Color::from_rgb(0.08, 0.10, 0.13));
    if let Some(alt) = r.altitude {
        let span = d.altitude_tape_span_m;
        let step = 25.0;
        let lo = ((alt - span * 0.5) / step).floor() as i32;
        let hi = ((alt + span * 0.5) / step).ceil() as i32;
        for k in lo..=hi {
            let m = k as f32 * step;
            if m < 0.0 {
                continue;
            }
            let y = tape.position.y + tape.size.y * (0.5 - (m - alt) / span);
            if y < tape.position.y || y > tape.end().y {
                continue;
            }
            let major = k % 4 == 0;
            p.line(Vector2::new(tape.position.x, y), Vector2::new(tape.position.x + if major { 30.0 } else { 16.0 }, y), faded(rgb(s.altitude), 0.8), 3.0);
            if major {
                p.number(Vector2::new(tape.position.x + 32.0, y + 9.0), 46.0, HorizontalAlignment::LEFT, &format!("{m:.0}"), 22, dim);
            }
        }
        let mid = tape.position.y + tape.size.y * 0.5;
        p.poly(&[Vector2::new(tape.end().x, mid), Vector2::new(tape.end().x - 16.0, mid - 12.0), Vector2::new(tape.end().x - 16.0, mid + 12.0)], rgb(s.altitude));
        p.line(Vector2::new(tape.position.x, mid), Vector2::new(tape.end().x - 14.0, mid), rgb(s.altitude), 3.0);
    }
    p.number(Vector2::new(680.0, 470.0), 120.0, HorizontalAlignment::CENTER, &fmt_alt(r.altitude), 34, rgb(s.altitude));

    // Fuel: a segmented bar.
    p.panel(Rect2::new(Vector2::new(816.0, 18.0), Vector2::new(86.0, 476.0)));
    p.label(Vector2::new(816.0, 60.0), 86.0, &n.fuel, 26, dim);
    let segs = 12;
    let lit = (r.fuel * segs as f32).ceil() as i32;
    let fuel_col = if r.fuel < d.low_fuel { if blink { warn } else { faded(warn, 0.3) } } else { rgb(s.fuel) };
    for k in 0..segs {
        let y = 440.0 - k as f32 * 30.0;
        let on = k < lit;
        let sb = style_box(if on { fuel_col } else { Color::from_rgb(0.08, 0.10, 0.13) }, faded(fuel_col, 0.5), 2, 6);
        p.c.draw_style_box(&sb, Rect2::new(Vector2::new(834.0, y), Vector2::new(50.0, 24.0)));
    }
    p.number(Vector2::new(816.0, 488.0), 86.0, HorizontalAlignment::CENTER, &format!("{:.0}%", r.fuel * 100.0), 26, fuel_col);

    // Oxygen: a ring dial.
    p.panel(Rect2::new(Vector2::new(918.0, 18.0), Vector2::new(88.0, 476.0)));
    p.label(Vector2::new(918.0, 60.0), 88.0, &n.oxygen, 26, dim);
    let oc = if r.oxygen < d.low_oxygen { if blink { warn } else { faded(warn, 0.3) } } else { rgb(s.oxygen) };
    let center = Vector2::new(962.0, 250.0);
    p.ring(center, 30.0, 0.0, TAU, Color::from_rgb(0.08, 0.10, 0.13), 14.0);
    p.ring(center, 30.0, -PI * 0.5, -PI * 0.5 + TAU * r.oxygen.clamp(0.0, 1.0), oc, 14.0);
    p.number(Vector2::new(918.0, 330.0), 88.0, HorizontalAlignment::CENTER, &format!("{:.0}%", r.oxygen * 100.0), 26, oc);
}

/// Classic round instrument: face, rim, ticks, needle from `from` to `to`
/// radians as `value` goes 0 to 1.
#[allow(clippy::too_many_arguments)]
fn dial(p: &mut Painter, center: Vector2, radius: f32, from: f32, to: f32, value: f32, ticks: i32, needle: Color, face: Color, rim: Color, ink: Color) {
    p.c.draw_circle(center, radius + 8.0, rim);
    p.c.draw_circle(center, radius, face);
    for k in 0..=ticks {
        let a = from + (to - from) * k as f32 / ticks as f32;
        let dir = Vector2::new(a.cos(), a.sin());
        let major = k % 2 == 0;
        p.line(center + dir * (radius - if major { 18.0 } else { 10.0 }), center + dir * (radius - 3.0), ink, if major { 4.0 } else { 2.0 });
    }
    let a = from + (to - from) * value.clamp(0.0, 1.0);
    p.needle(center, radius * 0.82, a, needle, 7.0);
}

fn draw_analog(p: &mut Painter, d: &DashData, r: &DashReadout, n: &Names, blink: bool) {
    let s = p.style.clone();
    let (face, rim, ink, needle, warn) = (rgba(s.panel), rgba(s.panel_edge), rgb(s.text), rgb(s.needle), rgb(s.warning));
    // Brushed backplate.
    p.c.draw_rect(Rect2::new(Vector2::new(8.0, 8.0), Vector2::new(1008.0, 496.0)), Color::from_rgb(0.28, 0.25, 0.21));
    for k in 0..40 {
        let y = 10.0 + k as f32 * 12.4;
        p.c.draw_rect(Rect2::new(Vector2::new(8.0, y), Vector2::new(1008.0, 1.0)), Color::from_rgba(1.0, 0.95, 0.85, 0.035));
    }
    // Speed: big semicircle dial.
    let sc = Vector2::new(512.0, 250.0);
    dial(p, sc, 170.0, PI * 0.8, PI * 2.2, r.speed / d.speed_full_scale_mps, 16, rgb(s.speed), face, rim, ink);
    p.label(sc + Vector2::new(-170.0, -60.0), 340.0, &n.speed, 30, rgb(s.dim));
    p.number(sc + Vector2::new(-170.0, 110.0), 340.0, HorizontalAlignment::CENTER, &format!("{} m/s", fmt_speed(r.speed)), 46, ink);
    // Climb rate: small dial, zero at nine o'clock.
    let vc = Vector2::new(512.0, 448.0);
    let vf = (r.vertical / d.vertical_full_scale_mps).clamp(-1.0, 1.0);
    // Needle points left at zero, up when climbing, down when sinking.
    dial(p, vc, 46.0, PI * 0.5, PI * 1.5, 0.5 + vf * 0.5, 8, needle, face, rim, ink);
    p.label(vc + Vector2::new(56.0, 10.0), 120.0, &n.vertical, 24, face);
    // Altimeter: two needles, hundreds and tens.
    let ac = Vector2::new(820.0, 190.0);
    let alt = r.altitude.unwrap_or(0.0).max(0.0);
    dial(p, ac, 130.0, -PI * 0.5, PI * 1.5, (alt % 100.0) / 100.0, 20, needle, face, rim, ink);
    p.needle(ac, 70.0, -PI * 0.5 + TAU * ((alt % 1000.0) / 1000.0), rgb(s.altitude), 10.0);
    p.label(ac + Vector2::new(-130.0, -36.0), 260.0, &n.altitude, 26, rgb(s.dim));
    p.number(ac + Vector2::new(-130.0, 72.0), 260.0, HorizontalAlignment::CENTER, &fmt_alt(r.altitude), 34, ink);
    p.label(ac + Vector2::new(-130.0, 180.0), 260.0, &n.body, 30, face);
    // Drift ball in a brass ring, left.
    let bc = Vector2::new(190.0, 190.0);
    p.c.draw_circle(bc, 138.0, rim);
    p.drift_ball(bc, 128.0, r, d.drift_min_mps, face, ink, rgb(s.drift));
    p.label(bc + Vector2::new(-130.0, 180.0), 260.0, &n.drift, 30, face);
    // Fuel: a glass tube with a level, bottom left.
    let tube = Rect2::new(Vector2::new(60.0, 400.0), Vector2::new(250.0, 44.0));
    let fc = if r.fuel < d.low_fuel && blink { warn } else { rgb(s.fuel) };
    let sb = style_box(face, rim, 5, 22);
    p.c.draw_style_box(&sb, tube);
    let fill = Rect2::new(tube.position + Vector2::new(6.0, 6.0), Vector2::new((tube.size.x - 12.0) * r.fuel.clamp(0.0, 1.0), tube.size.y - 12.0));
    p.c.draw_style_box(&style_box(fc, fc, 0, 16), fill);
    p.label(Vector2::new(60.0, 482.0), 250.0, &format!("{}  {:.0}%", n.fuel, r.fuel * 100.0), 26, face);
    // Oxygen: small round gauge, bottom right.
    let oc = Vector2::new(800.0, 440.0);
    let ocol = if r.oxygen < d.low_oxygen && blink { warn } else { rgb(s.oxygen) };
    dial(p, oc, 44.0, PI * 0.75, PI * 2.25, r.oxygen, 6, ocol, face, rim, ink);
    p.label(oc + Vector2::new(56.0, 10.0), 120.0, &n.oxygen, 26, face);
    // Lamps for status.
    let chips = status_chips(r, d, n, true);
    for (i, (label, bad)) in chips.iter().take(2).enumerate() {
        let at = Vector2::new(250.0 + i as f32 * 250.0, 30.0);
        let col = if *bad { warn } else { rgb(s.drift) };
        p.c.draw_circle(at + Vector2::new(14.0, 0.0), 12.0, col);
        p.text(&p.lab.clone(), at + Vector2::new(34.0, 10.0), 220.0, HorizontalAlignment::LEFT, label, 22, face);
    }
}

fn bar(len: usize, f: f32) -> String {
    let lit = (f.clamp(0.0, 1.0) * len as f32).round() as usize;
    format!("[{}{}]", "#".repeat(lit), "-".repeat(len - lit))
}

fn draw_terminal(p: &mut Painter, d: &DashData, r: &DashReadout, n: &Names, blink: bool, clock: f32) {
    let s = p.style.clone();
    let (text, dim, warn) = (rgb(s.text), rgb(s.dim), rgb(s.warning));
    let sb = style_box(rgba(s.panel), rgba(s.panel_edge), 3, s.corner_px);
    p.c.draw_style_box(&sb, Rect2::new(Vector2::new(10.0, 10.0), Vector2::new(1004.0, 492.0)));
    let x = 36.0;
    let row = |k: f32| 66.0 + k * 50.0;
    let px = 46;
    p.number(Vector2::new(x, row(0.0)), 700.0, HorizontalAlignment::LEFT, &format!("> {}", n.terminal_title), px, text);
    p.number(Vector2::new(x, row(0.0)), 620.0, HorizontalAlignment::RIGHT, &n.body.to_uppercase(), px, text);
    p.number(Vector2::new(x, row(1.4)), 640.0, HorizontalAlignment::LEFT, &format!("{:<6}{:>7} M/S", n.speed.to_uppercase(), fmt_speed(r.speed)), px, text);
    let arrow = if r.vertical >= 0.0 { "^" } else { "v" };
    p.number(Vector2::new(x, row(2.4)), 640.0, HorizontalAlignment::LEFT, &format!("{:<6}{:>7} M/S {arrow}", n.vertical.to_uppercase(), fmt_speed(r.vertical.abs())), px, text);
    p.number(Vector2::new(x, row(3.4)), 640.0, HorizontalAlignment::LEFT, &format!("{:<6}{:>9}", n.altitude.to_uppercase(), fmt_alt(r.altitude).to_uppercase()), px, text);
    let fc = if r.fuel < d.low_fuel { if blink { warn } else { dim } } else { text };
    p.number(Vector2::new(x, row(4.6)), 640.0, HorizontalAlignment::LEFT, &format!("{:<6}{} {:>3.0}%", n.fuel.to_uppercase(), bar(16, r.fuel), r.fuel * 100.0), px, fc);
    let oc = if r.oxygen < d.low_oxygen { if blink { warn } else { dim } } else { text };
    p.number(Vector2::new(x, row(5.6)), 640.0, HorizontalAlignment::LEFT, &format!("{:<6}{} {:>3.0}%", n.oxygen.to_uppercase(), bar(16, r.oxygen), r.oxygen * 100.0), px, oc);
    let chips = status_chips(r, d, n, blink);
    let status: Vec<String> = chips.iter().map(|c| c.0.to_uppercase()).collect();
    let cursor = if (clock * 2.0) as i32 % 2 == 0 { "_" } else { " " };
    p.number(Vector2::new(x, row(8.4)), 960.0, HorizontalAlignment::LEFT, &format!("> {}{cursor}", status.join(" / ")), px, text);
    // Drift ball as a dot grid, right.
    let c = Vector2::new(840.0, 250.0);
    let rad = 130.0;
    let cells = 9;
    for i in 0..cells {
        for j in 0..cells {
            let off = Vector2::new(i as f32 - (cells - 1) as f32 * 0.5, j as f32 - (cells - 1) as f32 * 0.5) * (rad * 2.0 / cells as f32);
            if off.length() <= rad {
                p.c.draw_rect(Rect2::new(c + off - Vector2::new(2.0, 2.0), Vector2::new(4.0, 4.0)), faded(dim, 0.8));
            }
        }
    }
    p.ring(c, rad + 8.0, 0.0, TAU, dim, 2.0);
    p.number(c + Vector2::new(-rad, -rad - 18.0), rad * 2.0, HorizontalAlignment::CENTER, &n.drift.to_uppercase(), 36, dim);
    if r.speed < d.drift_min_mps {
        p.number(c + Vector2::new(-30.0, 14.0), 60.0, HorizontalAlignment::CENTER, "+", 56, rgb(s.drift));
    } else {
        let v = r.drift / r.speed.max(1e-3);
        let at = c + Vector2::new(v.x, -v.y) * rad * 0.92;
        let glyph = if v.z >= 0.0 { "O" } else { "X" };
        p.number(at + Vector2::new(-30.0, 16.0), 60.0, HorizontalAlignment::CENTER, glyph, 56, rgb(s.drift));
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn dash_data_parses_with_three_styles_one_terminal() {
        let d: DashData = crate::data::parse(DASH_EMBEDDED).unwrap();
        assert_eq!(d.styles.len(), 3);
        assert!(d.styles.iter().any(|s| s.kind == DashKind::Terminal));
        assert!(d.default_style < d.styles.len());
    }

    #[test]
    fn terminal_bars_have_fixed_width() {
        assert_eq!(bar(10, 0.0), "[----------]");
        assert_eq!(bar(10, 1.0), "[##########]");
        assert_eq!(bar(10, 0.5).len(), 12);
    }

    #[test]
    fn target_line_shows_distance_and_signed_speed() {
        let t = TargetReadout { body_key: "BODY_MOON".into(), distance: 1900.0, closing: 84.0, autopilot: true, holding: false };
        assert_eq!(target_line("Moon", &t), "Moon 1900 m  84.0 m/s");
        let t = TargetReadout { distance: 12_500.0, closing: -3.0, ..t };
        assert_eq!(target_line("Moon", &t), "Moon 12.5 km  -3.0 m/s");
    }

    #[test]
    fn altitude_formats_switch_to_km() {
        assert_eq!(fmt_alt(Some(132.4)), "132 m");
        assert_eq!(fmt_alt(Some(12500.0)), "12.5 km");
        assert_eq!(fmt_alt(None), "---");
    }
}
