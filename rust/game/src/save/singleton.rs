//! `SaveData` engine singleton: the in-memory save plus the calls the game
//! makes at autosave points (ante end, entering a ship) and on save-and-quit.

use godot::classes::ProjectSettings;
use godot::obj::Singleton;
use godot::prelude::*;

use super::format::{CURRENT_VERSION, SaveFile};
use super::store::{SaveSettings, SaveStore};

const DATA_PATH: &str = "res://data/save/save.ron";
const DATA_EMBEDDED: &str = include_str!("../../../../godot/data/save/save.ron");

#[derive(GodotClass)]
#[class(singleton, base = Object)]
pub struct SaveData {
    current: SaveFile,
    store: Option<SaveStore>,
    /// Off in tests and test runs, so they never touch the player's saves.
    autosave_enabled: bool,
    base: Base<Object>,
}

#[godot_api]
impl IObject for SaveData {
    fn init(base: Base<Object>) -> Self {
        Self {
            current: SaveFile { version: CURRENT_VERSION, ..Default::default() },
            store: None,
            autosave_enabled: true,
            base,
        }
    }
}

#[godot_api]
impl SaveData {
    /// Load the newest valid save. Returns false if there was none (a fresh
    /// install) and logs any damaged files that were skipped.
    #[func]
    pub fn load_latest(&mut self) -> bool {
        let report = self.store().load_latest();
        for (path, why) in &report.skipped {
            godot_warn!("SaveData: skipped {}: {why}", path.display());
        }
        match report.loaded {
            Some((save, path)) => {
                godot_print!("SaveData: loaded {}", path.display());
                self.current = save;
                true
            }
            None => false,
        }
    }

    #[func]
    pub fn autosave(&mut self) -> bool {
        self.stamp_version();
        let mut save = self.current.clone();
        let result = self.store().autosave(&mut save);
        self.finish_write(save, result)
    }

    /// Save-and-quit slot.
    #[func]
    pub fn save_manual(&mut self) -> bool {
        self.stamp_version();
        let mut save = self.current.clone();
        let result = self.store().save_manual(&mut save);
        self.finish_write(save, result)
    }

    #[func]
    pub fn get_runs_started(&self) -> u32 {
        self.current.meta.runs_started
    }

    /// Record that a run began with the current RunSeed.
    #[func]
    pub fn begin_run(&mut self) {
        self.current.meta.runs_started += 1;
        let code = crate::seed::RunSeed::singleton().bind().get_code().to_string();
        self.current.run = Some(super::format::RunSave { seed_code: code, ante: 1, ..Default::default() });
    }

    /// Death ends the run (CLAUDE.md section 2): the run's Wallet, score and
    /// items are lost; meta progress is kept.
    #[func]
    pub fn end_run(&mut self) {
        self.current.run = None;
    }

    #[func]
    pub fn set_autosave_enabled(&mut self, on: bool) {
        self.autosave_enabled = on;
    }

    /// Ante end (section 14 autosave point): store the run's progress and
    /// build, then autosave.
    #[func]
    pub fn record_ante_end(&mut self) {
        self.copy_run_progress();
        if self.autosave_enabled {
            self.autosave();
        }
    }

    /// The run is over (won, died or out of time): put it on the local
    /// leaderboard, clear the run, autosave. Returns its rank (0 = best) or
    /// -1 if it didn't place.
    #[func]
    pub fn record_run_over(&mut self) -> i64 {
        let points = crate::points::singleton::Points::singleton();
        let p = points.bind();
        let rank = if let Some(run) = p.run() {
            let entry = super::format::LeaderEntry {
                seed_code: crate::seed::encode_seed(run.seed),
                ante: run.ante,
                points: run.stats.total_points,
                outcome: p.get_outcome().to_string(),
            };
            let size = p.tables().run.leaderboard_size;
            super::format::add_to_leaderboard(&mut self.current.meta.leaderboard, entry, size).map_or(-1, |r| r as i64)
        } else {
            -1
        };
        drop(p);
        self.current.run = None;
        if self.autosave_enabled {
            self.autosave();
        }
        rank
    }

    /// The local leaderboard, best first: dictionaries with seed, ante,
    /// points and outcome.
    #[func]
    pub fn get_leaderboard(&self) -> VarArray {
        let mut out = VarArray::new();
        for e in &self.current.meta.leaderboard {
            let mut d = VarDictionary::new();
            d.set("seed", &GString::from(&e.seed_code).to_variant());
            d.set("ante", &(e.ante as i64).to_variant());
            d.set("points", &(e.points as i64).to_variant());
            d.set("outcome", &GString::from(&e.outcome).to_variant());
            out.push(&d.to_variant());
        }
        out
    }

    #[func]
    pub fn get_save_folder(&mut self) -> GString {
        GString::from(&self.store().dir().display().to_string())
    }

    fn copy_run_progress(&mut self) {
        let points = crate::points::singleton::Points::singleton();
        let p = points.bind();
        let Some(run) = p.run() else { return };
        let save = self.current.run.get_or_insert_with(Default::default);
        save.seed_code = crate::seed::encode_seed(run.seed);
        save.ante = run.ante;
        save.ante_score = run.score.score as i64;
        save.wallet = run.wallet as i64;
        save.perks = run.loadout.perks.clone();
        save.attributes = run.loadout.attributes.iter().map(|(k, v)| (k.clone(), *v)).collect();
    }

    pub fn current(&self) -> &SaveFile {
        &self.current
    }

    pub fn current_mut(&mut self) -> &mut SaveFile {
        &mut self.current
    }

    fn store(&mut self) -> &SaveStore {
        self.store.get_or_insert_with(|| {
            let settings: SaveSettings = crate::data::load(DATA_PATH, DATA_EMBEDDED);
            let dir = ProjectSettings::singleton().globalize_path(&settings.directory).to_string();
            SaveStore::new(dir, settings.keep_autosaves)
        })
    }

    fn stamp_version(&mut self) {
        self.current.version = CURRENT_VERSION;
        self.current.game_version = ProjectSettings::singleton()
            .get_setting("application/config/version")
            .try_to::<GString>()
            .map(|s| s.to_string())
            .unwrap_or_default();
    }

    fn finish_write(&mut self, save: SaveFile, result: std::io::Result<std::path::PathBuf>) -> bool {
        match result {
            Ok(path) => {
                godot_print!("SaveData: wrote {}", path.display());
                self.current = save;
                true
            }
            Err(e) => {
                godot_error!("SaveData: save failed, previous save kept: {e}");
                false
            }
        }
    }
}
