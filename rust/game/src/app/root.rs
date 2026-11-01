//! `GameRoot`: the main scene. Boots, shows the menu, starts the slice, and
//! hands control to the test harness when launched with test options.

use godot::classes::{Node, Os, PackedScene, ProjectSettings};
use godot::obj::Singleton;
use godot::prelude::*;
use serde::Deserialize;

use super::args::LaunchArgs;
use super::crash;
use crate::save::SaveData;
use crate::seed::RunSeed;
use crate::verify::{Harness, HarnessMode};

const DATA_PATH: &str = "res://data/app/flow.ron";
const DATA_EMBEDDED: &str = include_str!("../../../../godot/data/app/flow.ron");

#[derive(Debug, Clone, Deserialize)]
pub struct FlowData {
    pub menu_scene: String,
    /// The playable scene loaded by "Start". The sphere-walk test until the slice exists.
    pub slice_scene: String,
    pub crash_log_dir: String,
}

#[derive(GodotClass)]
#[class(base = Node)]
pub struct GameRoot {
    data: FlowData,
    args: LaunchArgs,
    current: Option<Gd<Node>>,
    base: Base<Node>,
}

#[godot_api]
impl INode for GameRoot {
    fn init(base: Base<Node>) -> Self {
        Self {
            data: crate::data::load(DATA_PATH, DATA_EMBEDDED),
            args: LaunchArgs::default(),
            current: None,
            base,
        }
    }

    fn ready(&mut self) {
        self.base_mut().add_to_group(crate::player::hud::GAME_ROOT_GROUP);
        let crash_dir = ProjectSettings::singleton().globalize_path(&self.data.crash_log_dir).to_string();
        crash::install(crash_dir.into());

        let raw: Vec<String> = Os::singleton().get_cmdline_user_args().as_slice().iter().map(|s| s.to_string()).collect();
        self.args = match LaunchArgs::parse(&raw) {
            Ok(a) => a,
            Err(e) => {
                godot_error!("GameRoot: {e}; ignoring command-line options");
                LaunchArgs::default()
            }
        };

        self.choose_seed();
        SaveData::singleton().bind_mut().load_latest();
        self.show_menu();
        if self.args.is_test_run() {
            // Boot to menu, then start straight away, like a player pressing Start.
            self.start_game();
        }
    }
}

#[godot_api]
impl GameRoot {
    /// Called by the menu's Start button.
    #[func]
    pub fn start_game(&mut self) {
        SaveData::singleton().bind_mut().set_autosave_enabled(!self.args.is_test_run());
        SaveData::singleton().bind_mut().begin_run();
        // A fresh run: ante 1's contract board opens when the slice loads.
        crate::points::Points::singleton().bind_mut().start_run();
        // Bot and replay runs skip the between-ante screens.
        crate::points::Points::singleton().bind_mut().set_screens_mode(if self.args.is_test_run() { 0 } else { -1 });
        if !self.args.is_test_run() {
            SaveData::singleton().bind_mut().autosave();
        }
        let Some(scene) = self.swap_to(&self.data.slice_scene.clone()) else { return };
        if let Some(mode) = self.harness_mode() {
            let mut harness = Harness::new_alloc();
            harness.bind_mut().configure(mode, scene);
            self.base_mut().add_child(&harness);
        }
    }

    /// The death recap's Continue: death ends the run (section 2), so clear
    /// it from the save, autosave, and go back to the menu.
    #[func]
    pub fn on_player_died_continue(&mut self) {
        // With the ante loop running, the run summary comes first; it calls
        // on_run_over when read.
        let director = self.base().get_tree().get_first_node_in_group(crate::points::director::DIRECTOR_GROUP);
        if let Some(mut d) = director {
            d.call("show_summary_after_death", &[]);
            return;
        }
        SaveData::singleton().bind_mut().end_run();
        if !self.args.is_test_run() {
            SaveData::singleton().bind_mut().autosave();
        }
        self.show_menu();
    }

    /// The run summary was read (won, died or out of time; the summary
    /// already put it on the leaderboard and autosaved): back to the menu.
    #[func]
    pub fn on_run_over(&mut self) {
        SaveData::singleton().bind_mut().end_run();
        crate::points::Points::singleton().bind_mut().clear_run();
        self.show_menu();
    }

    /// Back to the menu (pause menu "quit to menu" later).
    #[func]
    pub fn show_menu(&mut self) {
        let path = self.data.menu_scene.clone();
        if let Some(mut menu) = self.swap_to(&path) {
            let callable = self.to_gd().callable("start_game");
            if menu.has_signal("start_requested") {
                menu.connect("start_requested", &callable);
            }
        }
    }

    fn choose_seed(&mut self) {
        let mut run_seed = RunSeed::singleton();
        // A replay brings its own seed unless one is given explicitly.
        let requested = self.args.seed.clone().or_else(|| {
            let path = self.args.replay.as_ref()?;
            crate::verify::harness::load_replay(path).ok().map(|r| r.seed_code)
        });
        let code = match &requested {
            Some(code) if run_seed.bind_mut().set_code(GString::from(code)) => run_seed.bind().get_code(),
            Some(code) => {
                godot_error!("GameRoot: '{code}' is not a valid seed code; using a random seed");
                run_seed.bind_mut().new_random()
            }
            None => run_seed.bind_mut().new_random(),
        };
        let version = ProjectSettings::singleton().get_setting("application/config/version");
        crash::set_context(format!("seed: {code}\nversion: {version}\nargs: {:?}", self.args));
        godot_print!("Run seed: {code}");
    }

    fn harness_mode(&self) -> Option<HarnessMode> {
        let out = self.args.out.clone().unwrap_or_else(|| "user://verify".to_string());
        if let Some(path) = &self.args.replay {
            Some(HarnessMode::Replay { path: path.clone(), out })
        } else if self.args.smoke {
            Some(HarnessMode::Smoke { duration_s: self.args.duration_s, bot: self.args.bot.clone(), out })
        } else if self.args.screenshots {
            Some(HarnessMode::Screenshots { out })
        } else {
            self.args.record.as_ref().map(|path| HarnessMode::Record { path: path.clone() })
        }
    }

    /// Replace the current child scene. Returns the new scene's root.
    fn swap_to(&mut self, path: &str) -> Option<Gd<Node>> {
        if let Some(mut old) = self.current.take() {
            // Deferred: the old scene may be the one whose button called us.
            self.base_mut().remove_child(&old);
            old.queue_free();
        }
        let Some(packed) = try_load::<PackedScene>(path).ok() else {
            godot_error!("GameRoot: cannot load {path}");
            return None;
        };
        let Some(node) = packed.instantiate() else {
            godot_error!("GameRoot: cannot instantiate {path}");
            return None;
        };
        self.base_mut().add_child(&node);
        self.current = Some(node.clone());
        Some(node)
    }
}
