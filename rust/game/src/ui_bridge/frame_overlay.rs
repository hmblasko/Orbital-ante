//! Developer frame-time overlay: numbers plus a rolling graph against the
//! Steam Deck budget (CLAUDE.md section 15). Also writes a CSV summary to
//! user://frametime_log.csv so Deck sessions produce real measurements.
//!
//! Serves the Steam Deck performance budget (priority 3 in section 0.3) and
//! fun rule 10 (game feel needs a steady frame rate). Developer-only tool.

use std::collections::VecDeque;

use godot::classes::file_access::ModeFlags;
use godot::classes::performance::Monitor;
use godot::classes::{Control, FileAccess, IControl, Input, Label, Performance, RenderingServer};
use godot::prelude::*;
use serde::Deserialize;

const DATA_PATH: &str = "res://data/debug/frame_overlay.ron";
const DATA_EMBEDDED: &str = include_str!("../../../../godot/data/debug/frame_overlay.ron");

#[derive(Debug, Clone, Deserialize)]
pub struct OverlayData {
    pub visible_on_start: bool,
    pub budget_ms: f32,
    pub floor_ms: f32,
    /// Seconds of frames used for the average / 1% / max numbers.
    pub stats_window_s: f32,
    /// Number of frames drawn in the graph.
    pub graph_frames: usize,
    pub graph_size: (f32, f32),
    /// Graph top edge, in milliseconds.
    pub graph_max_ms: f32,
    pub font_size: i32,
    pub log_interval_s: f32,
    pub log_path: String,
}

/// Summary of a set of frame times, in milliseconds.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct FrameStats {
    pub average: f32,
    pub worst_1_percent: f32,
    pub max: f32,
}

/// Average, mean of the slowest 1% and max of `frames_ms`. Pure, unit tested.
pub fn frame_stats(frames_ms: &[f32]) -> Option<FrameStats> {
    if frames_ms.is_empty() {
        return None;
    }
    let mut sorted = frames_ms.to_vec();
    sorted.sort_by(f32::total_cmp);
    let worst_count = (sorted.len() as f32 * 0.01).ceil().max(1.0) as usize;
    let worst = &sorted[sorted.len() - worst_count..];
    Some(FrameStats {
        average: sorted.iter().sum::<f32>() / sorted.len() as f32,
        worst_1_percent: worst.iter().sum::<f32>() / worst_count as f32,
        max: *sorted.last().unwrap(),
    })
}

#[derive(GodotClass)]
#[class(base = Control)]
pub struct FrameTimeOverlay {
    data: OverlayData,
    /// (seconds since start, frame ms) for the stats window.
    window: VecDeque<(f64, f32)>,
    graph: VecDeque<f32>,
    label: Option<Gd<Label>>,
    log: Option<Gd<FileAccess>>,
    elapsed: f64,
    next_log: f64,
    base: Base<Control>,
}

#[godot_api]
impl IControl for FrameTimeOverlay {
    fn init(base: Base<Control>) -> Self {
        let data: OverlayData = crate::data::load(DATA_PATH, DATA_EMBEDDED);
        Self {
            next_log: data.log_interval_s as f64,
            data,
            window: VecDeque::new(),
            graph: VecDeque::new(),
            label: None,
            log: None,
            elapsed: 0.0,
            base,
        }
    }

    fn ready(&mut self) {
        let mut label = Label::new_alloc();
        label.add_theme_font_size_override("font_size", self.data.font_size);
        label.add_theme_constant_override("outline_size", self.data.font_size / 4);
        label.add_theme_color_override("font_outline_color", Color::BLACK);
        label.set_position(Vector2::new(0.0, self.data.graph_size.1 + 4.0));
        self.base_mut().add_child(&label);
        self.label = Some(label);

        self.base_mut().set_mouse_filter(godot::classes::control::MouseFilter::IGNORE);
        let visible = self.data.visible_on_start;
        self.base_mut().set_visible(visible);

        if let Some(viewport) = self.base().get_viewport() {
            RenderingServer::singleton().viewport_set_measure_render_time(viewport.get_viewport_rid(), true);
        }

        self.log = FileAccess::open(&self.data.log_path, ModeFlags::WRITE);
        match self.log.as_mut() {
            Some(file) => {
                file.store_line("time_s,frames,avg_ms,worst_1pct_ms,max_ms,cpu_render_ms,gpu_render_ms,physics_ms,draw_calls");
            }
            None => godot_warn!("FrameTimeOverlay: cannot write {}", self.data.log_path),
        }
    }

    fn process(&mut self, delta: f64) {
        if Input::singleton().is_action_just_pressed("toggle_frame_overlay") {
            let visible = !self.base().is_visible();
            self.base_mut().set_visible(visible);
        }

        let frame_ms = (delta * 1000.0) as f32;
        self.elapsed += delta;
        self.window.push_back((self.elapsed, frame_ms));
        let cutoff = self.elapsed - self.data.stats_window_s as f64;
        while self.window.front().is_some_and(|(t, _)| *t < cutoff) {
            self.window.pop_front();
        }
        self.graph.push_back(frame_ms);
        while self.graph.len() > self.data.graph_frames {
            self.graph.pop_front();
        }

        let frames: Vec<f32> = self.window.iter().map(|(_, ms)| *ms).collect();
        let Some(stats) = frame_stats(&frames) else { return };
        let render = self.render_times();
        let physics_ms = Performance::singleton().get_monitor(Monitor::TIME_PHYSICS_PROCESS) * 1000.0;
        let draw_calls = Performance::singleton().get_monitor(Monitor::RENDER_TOTAL_DRAW_CALLS_IN_FRAME);

        if self.elapsed >= self.next_log {
            self.next_log += self.data.log_interval_s as f64;
            let line = format!(
                "{:.1},{},{:.2},{:.2},{:.2},{:.2},{:.2},{:.2},{}",
                self.elapsed, frames.len(), stats.average, stats.worst_1_percent, stats.max,
                render.0, render.1, physics_ms, draw_calls
            );
            if let Some(file) = self.log.as_mut() {
                file.store_line(&line);
                file.flush();
            }
        }

        if self.base().is_visible() {
            let fps = 1000.0 / stats.average.max(0.001);
            let text = format!(
                "{fps:.0} fps   avg {:.2} ms   1% {:.2} ms   max {:.2} ms\n\
                 render cpu {:.2} ms   gpu {:.2} ms   physics {physics_ms:.2} ms   draws {draw_calls}\n\
                 budget {:.1} ms / floor {:.1} ms   [F3 / L3 toggles]",
                stats.average, stats.worst_1_percent, stats.max, render.0, render.1,
                self.data.budget_ms, self.data.floor_ms,
            );
            let colour = self.colour_for(stats.worst_1_percent);
            if let Some(label) = self.label.as_mut() {
                label.set_text(&text);
                label.add_theme_color_override("font_color", colour);
            }
            self.base_mut().queue_redraw();
        }
    }

    fn draw(&mut self) {
        let (w, h) = self.data.graph_size;
        let max_ms = self.data.graph_max_ms;
        let to_y = |ms: f32| h - (ms / max_ms).min(1.0) * h;
        let budget_y = to_y(self.data.budget_ms);
        let floor_y = to_y(self.data.floor_ms);
        let samples: Vec<f32> = self.graph.iter().copied().collect();
        let step = w / self.data.graph_frames.max(1) as f32;
        let colours: Vec<Color> = samples.iter().map(|ms| self.colour_for(*ms)).collect();

        let mut base = self.base_mut();
        base.draw_rect(Rect2::new(Vector2::ZERO, Vector2::new(w, h)), Color::from_rgba(0.0, 0.0, 0.0, 0.55));
        base.draw_line(Vector2::new(0.0, budget_y), Vector2::new(w, budget_y), Color::from_rgb(0.3, 0.9, 0.4));
        base.draw_line(Vector2::new(0.0, floor_y), Vector2::new(w, floor_y), Color::from_rgb(0.95, 0.3, 0.25));
        for (i, ms) in samples.iter().enumerate() {
            let x = i as f32 * step;
            base.draw_line(Vector2::new(x, h), Vector2::new(x, to_y(*ms)), colours[i]);
        }
    }
}

impl FrameTimeOverlay {
    /// (cpu, gpu) render time of the last frame in ms.
    fn render_times(&self) -> (f64, f64) {
        let Some(viewport) = self.base().get_viewport() else { return (0.0, 0.0) };
        let rid = viewport.get_viewport_rid();
        let rs = RenderingServer::singleton();
        (rs.viewport_get_measured_render_time_cpu(rid), rs.viewport_get_measured_render_time_gpu(rid))
    }

    /// Green within budget, yellow between budget and floor, red past the floor.
    fn colour_for(&self, ms: f32) -> Color {
        if ms <= self.data.budget_ms {
            Color::from_rgb(0.45, 1.0, 0.5)
        } else if ms <= self.data.floor_ms {
            Color::from_rgb(1.0, 0.85, 0.3)
        } else {
            Color::from_rgb(1.0, 0.4, 0.35)
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn stats_of_steady_frames() {
        let s = frame_stats(&[16.0; 100]).unwrap();
        assert_eq!(s, FrameStats { average: 16.0, worst_1_percent: 16.0, max: 16.0 });
    }

    #[test]
    fn worst_1_percent_catches_spikes() {
        let mut frames = vec![10.0; 198];
        frames.push(40.0);
        frames.push(50.0);
        let s = frame_stats(&frames).unwrap();
        assert_eq!(s.worst_1_percent, 45.0);
        assert_eq!(s.max, 50.0);
    }

    #[test]
    fn empty_has_no_stats() {
        assert!(frame_stats(&[]).is_none());
    }
}
