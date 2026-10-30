//! `Harness`: attached by `GameRoot` to the running scene when the game is
//! launched with test options. Drives the player through input actions,
//! records or replays input, runs the detectors, takes screenshots, writes
//! results JSON and quits with exit code 0 (pass) or 1 (fail).

use godot::classes::DisplayServer;
use godot::classes::node::ProcessMode;
use godot::classes::{Camera3D, DirAccess, FileAccess, INode, Input, InputEvent, InputEventMouseMotion, Node, ProjectSettings};
use godot::classes::file_access::ModeFlags;
use godot::obj::Singleton;
use godot::prelude::*;
use serde::Deserialize;

use super::bot::{Wanderer, WandererSettings};
use super::detectors::{DetectorSettings, Detectors, Sample};
use super::replay::{RECORDED_ACTIONS, REPLAY_VERSION, Replay, ReplayFrame};
use super::results::{FrameTimeSummary, RunResults};
use crate::orbits::CelestialBody;
use crate::orbits::body::GRAVITY_GROUP;
use crate::player::PlayerController;
use crate::player::controller::PLAYER_GROUP;
use crate::seed::{RunSeed, SeededRng};
use crate::ui_bridge::frame_overlay::frame_stats;

const DATA_PATH: &str = "res://data/verify/harness.ron";
const DATA_EMBEDDED: &str = include_str!("../../../../godot/data/verify/harness.ron");
const VIEWPOINTS_PATH: &str = "res://data/verify/viewpoints.ron";
const VIEWPOINTS_EMBEDDED: &str = include_str!("../../../../godot/data/verify/viewpoints.ron");
const RESULTS_FILE: &str = "results.json";
const REPLAY_FILE: &str = "replay.ron";
/// Node name of the developer overlay layer, hidden for stable screenshots.
const DEBUG_UI_NODE: &str = "DebugUI";

#[derive(Debug, Clone, Deserialize)]
pub struct HarnessSettings {
    pub default_smoke_duration_s: f32,
    pub default_bot: String,
    /// Errors in the log above this fail the run. Checked by `oa_verify`,
    /// which reads Godot's output (in-process log hooks proved unreliable).
    pub max_errors: u32,
    /// A replay passes if it ends within this distance of the recording.
    pub replay_tolerance_m: f32,
    pub wanderer: WandererSettings,
    pub detectors: DetectorSettings,
    /// Frames to wait before the first screenshot and between viewpoints.
    pub screenshot_settle_frames: u32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct Viewpoint {
    pub name: String,
    pub position: (f32, f32, f32),
    pub look_at: (f32, f32, f32),
    pub up: (f32, f32, f32),
}

#[derive(Debug, Clone)]
pub enum HarnessMode {
    Smoke { duration_s: Option<f32>, bot: Option<String>, out: String },
    Replay { path: String, out: String },
    Screenshots { out: String },
    Record { path: String },
}

#[derive(GodotClass)]
#[class(base = Node)]
pub struct Harness {
    settings: HarnessSettings,
    mode: Option<HarnessMode>,
    scene: Option<Gd<Node>>,
    player: Option<Gd<PlayerController>>,
    bot: Option<Wanderer>,
    detectors: Detectors,
    replay_in: Option<Replay>,
    recorded: Vec<ReplayFrame>,
    tick: u32,
    game_time: f32,
    mouse_accum: Vector2,
    frame_ms: Vec<f32>,
    viewpoints: Vec<Viewpoint>,
    shot_index: usize,
    shot_wait: u32,
    shots: Vec<String>,
    camera: Option<Gd<Camera3D>>,
    failures: Vec<String>,
    death_reported: bool,
    finished: bool,
    base: Base<Node>,
}

#[godot_api]
impl INode for Harness {
    fn init(base: Base<Node>) -> Self {
        let settings: HarnessSettings = crate::data::load(DATA_PATH, DATA_EMBEDDED);
        Self {
            detectors: Detectors::new(settings.detectors.clone()),
            settings,
            mode: None,
            scene: None,
            player: None,
            bot: None,
            replay_in: None,
            recorded: Vec::new(),
            tick: 0,
            game_time: 0.0,
            mouse_accum: Vector2::ZERO,
            frame_ms: Vec::new(),
            viewpoints: Vec::new(),
            shot_index: 0,
            shot_wait: 0,
            shots: Vec::new(),
            camera: None,
            failures: Vec::new(),
            death_reported: false,
            finished: false,
            base,
        }
    }

    fn ready(&mut self) {
        // Run before the player each physics tick, so the inputs we set are
        // the ones the player reads that same tick.
        self.base_mut().set_physics_process_priority(-100);

        let tree = self.base().get_tree();
        self.player = tree
            .get_nodes_in_group(PLAYER_GROUP)
            .iter_shared()
            .find_map(|n| n.try_cast::<PlayerController>().ok());
        if self.player.is_none() && !matches!(self.mode, Some(HarnessMode::Screenshots { .. })) {
            self.fail("no player found in the scene".into());
        }

        match self.mode.clone() {
            Some(HarnessMode::Smoke { bot, .. }) => {
                let profile = bot.unwrap_or_else(|| self.settings.default_bot.clone());
                if profile != "wanderer" {
                    self.fail(format!("unknown bot profile '{profile}' (Phase 1 has: wanderer)"));
                }
                let rng = SeededRng::derive(crate::seed::singleton::run_seed(), "bot");
                self.bot = Some(Wanderer::new(self.settings.wanderer.clone(), rng));
            }
            Some(HarnessMode::Replay { path, .. }) => match load_replay(&path) {
                Ok(replay) => {
                    let current = RunSeed::singleton().bind().get_code().to_string();
                    if replay.seed_code != current {
                        self.fail(format!("replay was recorded with seed {} but this run uses {current}", replay.seed_code));
                    }
                    self.replay_in = Some(replay);
                }
                Err(e) => self.fail(format!("cannot read replay {path}: {e}")),
            },
            Some(HarnessMode::Screenshots { .. }) => self.prepare_screenshots(),
            Some(HarnessMode::Record { ref path }) => godot_print!("Harness: recording input to {path}"),
            None => self.fail("harness started without a mode".into()),
        }
        if !self.failures.is_empty() {
            self.finish();
        }
    }

    fn input(&mut self, event: Gd<InputEvent>) {
        if let Ok(motion) = event.try_cast::<InputEventMouseMotion>() {
            self.mouse_accum += motion.get_relative();
        }
    }

    fn process(&mut self, delta: f64) {
        if self.finished {
            return;
        }
        self.frame_ms.push((delta * 1000.0) as f32);
        if matches!(self.mode, Some(HarnessMode::Screenshots { .. })) {
            self.step_screenshots();
        }
    }

    fn physics_process(&mut self, delta: f64) {
        if self.finished {
            return;
        }
        let dt = delta as f32;
        // Smoke runs end at the start of a tick, before any input for it, so
        // the recording holds exactly one frame per player physics step.
        if let Some(HarnessMode::Smoke { duration_s, .. }) = &self.mode
            && self.game_time >= duration_s.unwrap_or(self.settings.default_smoke_duration_s)
        {
            self.finish();
            return;
        }
        self.tick += 1;
        self.game_time += dt;

        match self.mode.clone() {
            Some(HarnessMode::Smoke { .. }) => {
                let strengths = self.bot.as_mut().map(|b| b.tick(dt)).unwrap_or_default();
                apply_actions(&strengths);
                self.recorded.push(ReplayFrame { a: strengths, m: (0.0, 0.0) });
                self.run_detectors();
            }
            Some(HarnessMode::Replay { .. }) => {
                let index = (self.tick - 1) as usize;
                let frame = self.replay_in.as_ref().and_then(|r| r.frames.get(index).cloned());
                match frame {
                    Some(frame) => {
                        apply_actions(&frame.a);
                        if let Some(player) = self.player.as_mut() {
                            player.bind_mut().add_look_delta(Vector2::new(frame.m.0, frame.m.1));
                        }
                        self.run_detectors();
                    }
                    None => self.finish(),
                }
            }
            Some(HarnessMode::Record { .. }) => {
                let input = Input::singleton();
                let a = RECORDED_ACTIONS.iter().map(|name| input.get_action_raw_strength(*name)).collect();
                let m = std::mem::replace(&mut self.mouse_accum, Vector2::ZERO);
                self.recorded.push(ReplayFrame { a, m: (m.x, m.y) });
            }
            _ => {}
        }
    }

    fn exit_tree(&mut self) {
        // Normal play with --record: save when the game closes.
        if let Some(HarnessMode::Record { path }) = self.mode.clone()
            && !self.finished
        {
            self.finished = true;
            self.write_replay(&path);
        }
    }
}

impl Harness {
    pub fn configure(&mut self, mode: HarnessMode, scene: Gd<Node>) {
        self.mode = Some(mode);
        self.scene = Some(scene);
    }

    fn fail(&mut self, why: String) {
        godot_error!("Harness: {why}");
        self.failures.push(why);
    }

    fn player_position(&self) -> Vector3 {
        self.player.as_ref().map_or(Vector3::ZERO, |p| p.get_global_position())
    }

    fn run_detectors(&mut self) {
        let Some(player) = self.player.clone() else { return };
        // Bots aren't meant to die in the test scene; an unexpected death is a failure.
        if player.bind().is_dead() && !self.death_reported {
            let key = player.bind().get_death_key();
            self.death_reported = true;
            self.fail(format!("player died at {:.1} s: {key}", self.game_time));
        }
        let position = player.get_global_position();
        let velocity = player.get_velocity();
        let nearest = self
            .base()
            .get_tree()
            .get_nodes_in_group(GRAVITY_GROUP)
            .iter_shared()
            .filter_map(|n| n.try_cast::<CelestialBody>().ok())
            .map(|b| (b.get_global_position(), b.bind().radius()))
            .min_by(|a, b| (a.0.distance_to(position) - a.1).total_cmp(&(b.0.distance_to(position) - b.1)));
        let wants_to_move = match &self.bot {
            Some(bot) => bot.wants_to_move(),
            None => false,
        };
        self.detectors.check(Sample {
            time_s: self.game_time,
            position: [position.x, position.y, position.z],
            velocity: [velocity.x, velocity.y, velocity.z],
            nearest_body: nearest.map(|(c, r)| ([c.x, c.y, c.z], r)),
            wants_to_move,
        });
    }

    fn prepare_screenshots(&mut self) {
        if DisplayServer::singleton().get_name() == "headless" {
            self.fail("screenshots need rendering; run without --headless (use xvfb-run on CI)".into());
            return;
        }
        self.viewpoints = crate::data::load(VIEWPOINTS_PATH, VIEWPOINTS_EMBEDDED);
        if let Some(scene) = self.scene.as_ref()
            && let Some(mut ui) = scene.try_get_node_as::<Node>(DEBUG_UI_NODE)
        {
            ui.set("visible", &false.to_variant());
        }
        if let Some(player) = self.player.as_mut() {
            player.set_process_mode(ProcessMode::DISABLED);
        }
        let camera = Camera3D::new_alloc();
        if let Some(mut scene) = self.scene.clone() {
            scene.add_child(&camera);
        }
        self.camera = Some(camera);
        self.shot_wait = self.settings.screenshot_settle_frames;
        self.place_camera();
    }

    fn place_camera(&mut self) {
        let Some(vp) = self.viewpoints.get(self.shot_index).cloned() else { return };
        if let Some(camera) = self.camera.as_mut() {
            let p = Vector3::new(vp.position.0, vp.position.1, vp.position.2);
            let target = Vector3::new(vp.look_at.0, vp.look_at.1, vp.look_at.2);
            let up = Vector3::new(vp.up.0, vp.up.1, vp.up.2);
            camera.set_global_position(p);
            camera.look_at_ex(target).up(up).done();
            camera.make_current();
        }
    }

    fn step_screenshots(&mut self) {
        if self.shot_wait > 0 {
            self.shot_wait -= 1;
            return;
        }
        let Some(vp) = self.viewpoints.get(self.shot_index).cloned() else {
            self.finish();
            return;
        };
        let out = self.out_dir();
        let path = format!("{out}/{}.png", vp.name);
        let image = self.base().get_viewport().and_then(|v| v.get_texture()).and_then(|t| t.get_image());
        match image {
            Some(image) if image.save_png(&path) == godot::global::Error::OK => self.shots.push(path),
            _ => self.fail(format!("could not capture screenshot {}", vp.name)),
        }
        self.shot_index += 1;
        self.shot_wait = self.settings.screenshot_settle_frames;
        self.place_camera();
    }

    fn out_dir(&self) -> String {
        let raw = match &self.mode {
            Some(HarnessMode::Smoke { out, .. } | HarnessMode::Replay { out, .. } | HarnessMode::Screenshots { out }) => out.clone(),
            _ => "user://verify".to_string(),
        };
        let abs = ProjectSettings::singleton().globalize_path(&raw).to_string();
        DirAccess::make_dir_recursive_absolute(&abs);
        abs
    }

    fn write_replay(&mut self, path: &str) {
        let p = self.player_position();
        let replay = Replay {
            version: REPLAY_VERSION,
            seed_code: RunSeed::singleton().bind().get_code().to_string(),
            scene: self.scene.as_ref().map(|s| s.get_scene_file_path().to_string()).unwrap_or_default(),
            physics_ticks_per_second: godot::classes::Engine::singleton().get_physics_ticks_per_second(),
            frames: std::mem::take(&mut self.recorded),
            final_position: (p.x, p.y, p.z),
        };
        if let Err(e) = write_text(path, &replay.to_text()) {
            self.fail(format!("cannot write replay {path}: {e}"));
        }
    }

    fn finish(&mut self) {
        if self.finished {
            return;
        }
        self.finished = true;
        let out = self.out_dir();
        let mut results = RunResults {
            seed_code: RunSeed::singleton().bind().get_code().to_string(),
            scene: self.scene.as_ref().map(|s| s.get_scene_file_path().to_string()).unwrap_or_default(),
            game_seconds: self.game_time,
            headless: DisplayServer::singleton().get_name() == "headless",
            ..Default::default()
        };
        let p = self.player_position();
        results.final_position = (p.x, p.y, p.z);

        match self.mode.clone() {
            Some(HarnessMode::Smoke { bot, .. }) => {
                results.kind = "smoke".into();
                results.bot = bot.unwrap_or_else(|| self.settings.default_bot.clone());
                let path = format!("{out}/{REPLAY_FILE}");
                self.write_replay(&path);
                results.replay_file = Some(path);
            }
            Some(HarnessMode::Replay { path, .. }) => {
                results.kind = "replay".into();
                results.replay_file = Some(path);
                if let Some(replay) = &self.replay_in {
                    let expected = Vector3::new(replay.final_position.0, replay.final_position.1, replay.final_position.2);
                    let drift = expected.distance_to(p);
                    results.replay_drift_m = Some(drift);
                    if drift > self.settings.replay_tolerance_m {
                        self.fail(format!("replay ended {drift:.4} m from the recording (tolerance {} m)", self.settings.replay_tolerance_m));
                    }
                }
            }
            Some(HarnessMode::Screenshots { .. }) => {
                results.kind = "screenshots".into();
                results.screenshots = self.shots.clone();
            }
            _ => {}
        }

        results.detector_hits = self.detectors.hits().to_vec();
        for hit in &results.detector_hits {
            self.failures.push(format!("detector {:?} at {:.1} s: {}", hit.kind, hit.time_s, hit.detail));
        }
        if let Some(stats) = frame_stats(&self.frame_ms) {
            results.frame_times = FrameTimeSummary {
                frames: self.frame_ms.len() as u32,
                average_ms: stats.average,
                worst_1_percent_ms: stats.worst_1_percent,
                max_ms: stats.max,
            };
        }
        results.failures = self.failures.clone();
        results.passed = self.failures.is_empty();

        let json = serde_json::to_string_pretty(&results).unwrap_or_default();
        if let Err(e) = write_text(&format!("{out}/{RESULTS_FILE}"), &json) {
            godot_error!("Harness: cannot write results: {e}");
        }
        let verdict = if results.passed { "PASS" } else { "FAIL" };
        godot_print!("Harness: {} {verdict} after {:.1} game seconds; results in {out}", results.kind, self.game_time);
        for f in &results.failures {
            godot_print!("Harness:   - {f}");
        }
        release_all();
        let code = if results.passed { 0 } else { 1 };
        self.base().get_tree().quit_ex().exit_code(code).done();
    }
}

fn apply_actions(strengths: &[f32]) {
    let mut input = Input::singleton();
    for (name, strength) in RECORDED_ACTIONS.iter().zip(strengths) {
        if *strength > 0.0 {
            input.action_press_ex(*name).strength(*strength).done();
        } else {
            input.action_release(*name);
        }
    }
}

fn release_all() {
    let mut input = Input::singleton();
    for name in RECORDED_ACTIONS {
        input.action_release(*name);
    }
}

/// Read and validate a replay file (absolute, user:// or res:// path).
pub fn load_replay(path: &str) -> Result<Replay, String> {
    read_text(path).and_then(|t| Replay::from_text(&t))
}

fn read_text(path: &str) -> Result<String, String> {
    let abs = ProjectSettings::singleton().globalize_path(path).to_string();
    std::fs::read_to_string(&abs).map_err(|e| e.to_string())
}

fn write_text(path: &str, text: &str) -> Result<(), String> {
    let abs = ProjectSettings::singleton().globalize_path(path).to_string();
    let mut file = FileAccess::open(&abs, ModeFlags::WRITE).ok_or_else(|| format!("cannot open {abs}"))?;
    file.store_string(text);
    file.close();
    Ok(())
}
