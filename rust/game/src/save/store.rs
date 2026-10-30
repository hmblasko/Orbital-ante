//! Save files on disk. Plain Rust (`std::fs`), so it is unit tested without Godot.
//!
//! Every write goes to a temporary file first, is flushed to disk, then
//! renamed over the old one. A crash, power loss or Steam Deck suspend in
//! the middle of a save leaves the previous file intact.

use std::fs;
use std::io::Write;
use std::path::{Path, PathBuf};

use serde::Deserialize;

use super::format::{self, LoadError, SaveFile};

/// From res://data/save/save.ron.
#[derive(Debug, Clone, Deserialize)]
pub struct SaveSettings {
    /// Folder for saves, usually under user://.
    pub directory: String,
    pub keep_autosaves: usize,
}

const MANUAL_FILE: &str = "save.ron";
const AUTOSAVE_PREFIX: &str = "autosave_";
const EXTENSION: &str = ".ron";
const TEMP_SUFFIX: &str = ".tmp";

pub struct SaveStore {
    dir: PathBuf,
    keep_autosaves: usize,
}

/// Result of looking for the newest usable save.
#[derive(Debug, Default)]
pub struct LoadReport {
    pub loaded: Option<(SaveFile, PathBuf)>,
    /// Files that exist but could not be used, and why. Never deleted.
    pub skipped: Vec<(PathBuf, String)>,
}

impl SaveStore {
    pub fn new(dir: impl Into<PathBuf>, keep_autosaves: usize) -> Self {
        Self {
            dir: dir.into(),
            keep_autosaves: keep_autosaves.max(1),
        }
    }

    pub fn dir(&self) -> &Path {
        &self.dir
    }

    /// Save-and-quit / manual save slot.
    pub fn save_manual(&self, save: &mut SaveFile) -> std::io::Result<PathBuf> {
        save.save_counter = self.highest_counter() + 1;
        let path = self.dir.join(MANUAL_FILE);
        write_atomic(&path, &format::write_save(save))?;
        Ok(path)
    }

    /// New autosave; removes the oldest beyond `keep_autosaves`.
    pub fn autosave(&self, save: &mut SaveFile) -> std::io::Result<PathBuf> {
        save.save_counter = self.highest_counter() + 1;
        let next = self.autosaves().last().map_or(1, |(n, _)| n + 1);
        let path = self
            .dir
            .join(format!("{AUTOSAVE_PREFIX}{next:06}{EXTENSION}"));
        write_atomic(&path, &format::write_save(save))?;
        let all = self.autosaves();
        let excess = all.len().saturating_sub(self.keep_autosaves);
        for (_, old) in all.into_iter().take(excess) {
            fs::remove_file(old)?;
        }
        Ok(path)
    }

    /// Autosave files, oldest first, with their sequence numbers.
    pub fn autosaves(&self) -> Vec<(u64, PathBuf)> {
        let Ok(entries) = fs::read_dir(&self.dir) else {
            return Vec::new();
        };
        let mut found: Vec<(u64, PathBuf)> = entries
            .filter_map(|e| e.ok())
            .filter_map(|e| {
                let name = e.file_name().to_string_lossy().into_owned();
                let number = name
                    .strip_prefix(AUTOSAVE_PREFIX)?
                    .strip_suffix(EXTENSION)?
                    .parse()
                    .ok()?;
                Some((number, e.path()))
            })
            .collect();
        found.sort();
        found
    }

    /// The newest save that loads cleanly, falling back to older ones.
    pub fn load_latest(&self) -> LoadReport {
        let mut report = LoadReport::default();
        let mut candidates: Vec<PathBuf> = self.autosaves().into_iter().map(|(_, p)| p).collect();
        let manual = self.dir.join(MANUAL_FILE);
        if manual.exists() {
            candidates.push(manual);
        }
        for path in candidates {
            match fs::read_to_string(&path)
                .map_err(|e| LoadError::Unreadable(e.to_string()))
                .and_then(|t| format::parse_save(&t))
            {
                Ok(save) => {
                    let newer = report
                        .loaded
                        .as_ref()
                        .is_none_or(|(best, _)| save.save_counter > best.save_counter);
                    if newer {
                        report.loaded = Some((save, path));
                    }
                }
                Err(e) => report.skipped.push((path, e.to_string())),
            }
        }
        report
    }

    fn highest_counter(&self) -> u64 {
        self.load_latest().loaded.map_or(0, |(s, _)| s.save_counter)
    }
}

/// Write `text` to `path` so that `path` is always either the old or the new
/// complete file, never half-written.
pub fn write_atomic(path: &Path, text: &str) -> std::io::Result<()> {
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent)?;
    }
    let mut temp = path.as_os_str().to_owned();
    temp.push(TEMP_SUFFIX);
    let temp = PathBuf::from(temp);
    {
        let mut file = fs::File::create(&temp)?;
        file.write_all(text.as_bytes())?;
        file.sync_all()?;
    }
    fs::rename(&temp, path)?;
    #[cfg(unix)]
    if let Some(parent) = path.parent() {
        // Make the rename itself durable.
        fs::File::open(parent)?.sync_all()?;
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::save::format::{CURRENT_VERSION, MetaProgress};

    struct TempDir(PathBuf);
    impl TempDir {
        fn new(name: &str) -> Self {
            let dir =
                std::env::temp_dir().join(format!("oa_save_test_{name}_{}", std::process::id()));
            let _ = fs::remove_dir_all(&dir);
            fs::create_dir_all(&dir).unwrap();
            Self(dir)
        }
    }
    impl Drop for TempDir {
        fn drop(&mut self) {
            let _ = fs::remove_dir_all(&self.0);
        }
    }

    fn save_with_runs(runs: u32) -> SaveFile {
        SaveFile {
            version: CURRENT_VERSION,
            meta: MetaProgress {
                runs_started: runs,
                ..Default::default()
            },
            ..Default::default()
        }
    }

    #[test]
    fn keeps_only_last_three_autosaves() {
        let tmp = TempDir::new("rotate");
        let store = SaveStore::new(&tmp.0, 3);
        for runs in 1..=5 {
            store.autosave(&mut save_with_runs(runs)).unwrap();
        }
        let numbers: Vec<u64> = store.autosaves().into_iter().map(|(n, _)| n).collect();
        assert_eq!(numbers, vec![3, 4, 5]);
        assert_eq!(store.load_latest().loaded.unwrap().0.meta.runs_started, 5);
    }

    #[test]
    fn damaged_newest_falls_back_to_older() {
        let tmp = TempDir::new("fallback");
        let store = SaveStore::new(&tmp.0, 3);
        store.autosave(&mut save_with_runs(1)).unwrap();
        let newest = store.autosave(&mut save_with_runs(2)).unwrap();
        fs::write(&newest, "(version: 1, meta: (runs_sta").unwrap();
        let report = store.load_latest();
        assert_eq!(report.loaded.unwrap().0.meta.runs_started, 1);
        assert_eq!(report.skipped.len(), 1);
        assert!(newest.exists(), "damaged saves are kept for bug reports");
    }

    #[test]
    fn manual_and_autosave_newest_wins() {
        let tmp = TempDir::new("newest");
        let store = SaveStore::new(&tmp.0, 3);
        store.save_manual(&mut save_with_runs(1)).unwrap();
        store.autosave(&mut save_with_runs(2)).unwrap();
        assert_eq!(store.load_latest().loaded.unwrap().0.meta.runs_started, 2);
        store.save_manual(&mut save_with_runs(3)).unwrap();
        assert_eq!(store.load_latest().loaded.unwrap().0.meta.runs_started, 3);
    }

    #[test]
    fn leftover_temp_file_is_ignored() {
        let tmp = TempDir::new("temp");
        let store = SaveStore::new(&tmp.0, 3);
        store.autosave(&mut save_with_runs(1)).unwrap();
        // Simulates power loss during the next write.
        fs::write(tmp.0.join("autosave_000002.ron.tmp"), "(versi").unwrap();
        let report = store.load_latest();
        assert_eq!(report.loaded.unwrap().0.meta.runs_started, 1);
        assert!(report.skipped.is_empty());
    }

    #[test]
    fn empty_folder_loads_nothing() {
        let tmp = TempDir::new("empty");
        let report = SaveStore::new(&tmp.0, 3).load_latest();
        assert!(report.loaded.is_none() && report.skipped.is_empty());
    }
}
