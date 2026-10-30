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
    base: Base<Object>,
}

#[godot_api]
impl IObject for SaveData {
    fn init(base: Base<Object>) -> Self {
        Self {
            current: SaveFile { version: CURRENT_VERSION, ..Default::default() },
            store: None,
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
    pub fn get_save_folder(&mut self) -> GString {
        GString::from(&self.store().dir().display().to_string())
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
