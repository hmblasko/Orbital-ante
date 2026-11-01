//! What a save file contains, and how old versions are upgraded.

use serde::{Deserialize, Serialize};

/// Bump this whenever `SaveFile` changes shape, and add a migration below.
pub const CURRENT_VERSION: u32 = 2;

#[derive(Debug, Clone, PartialEq, Default, Serialize, Deserialize)]
pub struct SaveFile {
    pub version: u32,
    /// Game version that wrote the file, for bug reports.
    #[serde(default)]
    pub game_version: String,
    /// Increases with every write; the highest valid one is the newest save.
    #[serde(default)]
    pub save_counter: u64,
    #[serde(default)]
    pub meta: MetaProgress,
    /// The run in progress, if any.
    #[serde(default)]
    pub run: Option<RunSave>,
}

/// Kept across runs (CLAUDE.md section 2, "Kept").
#[derive(Debug, Clone, PartialEq, Default, Serialize, Deserialize)]
pub struct MetaProgress {
    #[serde(default)]
    pub runs_started: u32,
    #[serde(default)]
    pub unlocks: Vec<String>,
    #[serde(default)]
    pub notebook: Vec<String>,
    #[serde(default)]
    pub locker: Vec<String>,
    /// Local leaderboard (section 3): best runs, tagged with the seed.
    /// Added in save version 2.
    #[serde(default)]
    pub leaderboard: Vec<LeaderEntry>,
}

/// One finished run on the local leaderboard.
#[derive(Debug, Clone, PartialEq, Default, Serialize, Deserialize)]
pub struct LeaderEntry {
    pub seed_code: String,
    /// Highest ante reached.
    pub ante: u32,
    pub points: u64,
    /// "won", "died" or "time_up".
    pub outcome: String,
}

/// Keep the best `size` entries: highest ante first, then most points.
pub fn add_to_leaderboard(board: &mut Vec<LeaderEntry>, entry: LeaderEntry, size: usize) -> Option<usize> {
    board.push(entry.clone());
    board.sort_by(|a, b| b.ante.cmp(&a.ante).then(b.points.cmp(&a.points)));
    board.truncate(size);
    board.iter().position(|e| *e == entry)
}

/// Lost when the run ends (CLAUDE.md section 2, "Lost").
#[derive(Debug, Clone, PartialEq, Default, Serialize, Deserialize)]
pub struct RunSave {
    pub seed_code: String,
    pub ante: u32,
    #[serde(default)]
    pub ante_score: i64,
    #[serde(default)]
    pub wallet: i64,
    #[serde(default)]
    pub player: Option<PlayerSave>,
    /// Perks picked and attribute levels bought this run (version 2).
    #[serde(default)]
    pub perks: Vec<String>,
    #[serde(default)]
    pub attributes: Vec<(String, u32)>,
}

#[derive(Debug, Clone, PartialEq, Default, Serialize, Deserialize)]
pub struct PlayerSave {
    /// Which body the player stands on or near.
    pub body: String,
    /// Position relative to that body's centre, in metres.
    pub position: (f32, f32, f32),
}

#[derive(Debug, PartialEq)]
pub enum LoadError {
    Unreadable(String),
    /// Written by a newer build; never overwrite or "fix" it.
    TooNew { found: u32 },
    MigrationFailed { from: u32, reason: String },
}

impl std::fmt::Display for LoadError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            LoadError::Unreadable(e) => write!(f, "unreadable: {e}"),
            LoadError::TooNew { found } => write!(f, "save version {found} is newer than this build ({CURRENT_VERSION})"),
            LoadError::MigrationFailed { from, reason } => write!(f, "could not upgrade from version {from}: {reason}"),
        }
    }
}

#[derive(Deserialize)]
struct VersionProbe {
    version: u32,
}

/// One upgrade step: turns a version N file (as generic RON) into version N+1.
type Migration = fn(ron::Value) -> Result<ron::Value, String>;

/// `MIGRATIONS[i]` upgrades version `i + 1` to `i + 2`. Each entry also
/// needs a fixture file in tests/fixtures.
const MIGRATIONS: &[Migration] = &[v1_to_v2];

/// Version 2 added the leaderboard and the run's build. Old files get an
/// empty leaderboard and build; nothing else changes.
fn v1_to_v2(value: ron::Value) -> Result<ron::Value, String> {
    let ron::Value::Map(mut map) = value else { return Err("save is not a map".into()) };
    let key = ron::Value::String("version".into());
    map.insert(key, ron::Value::Number(ron::Number::from(2u32)));
    Ok(ron::Value::Map(map))
}

pub fn parse_save(text: &str) -> Result<SaveFile, LoadError> {
    let probe: VersionProbe = ron::from_str(text).map_err(|e| LoadError::Unreadable(e.to_string()))?;
    if probe.version > CURRENT_VERSION {
        return Err(LoadError::TooNew { found: probe.version });
    }
    if probe.version == CURRENT_VERSION {
        return ron::from_str(text).map_err(|e| LoadError::Unreadable(e.to_string()));
    }
    let mut value: ron::Value = ron::from_str(text).map_err(|e| LoadError::Unreadable(e.to_string()))?;
    for from in probe.version..CURRENT_VERSION {
        let step = from
            .checked_sub(1)
            .and_then(|i| MIGRATIONS.get(i as usize))
            .ok_or(LoadError::MigrationFailed { from, reason: "no migration for this version".into() })?;
        value = step(value).map_err(|reason| LoadError::MigrationFailed { from, reason })?;
    }
    let mut save: SaveFile = value.into_rust().map_err(|e| LoadError::Unreadable(e.to_string()))?;
    save.version = CURRENT_VERSION;
    Ok(save)
}

pub fn write_save(save: &SaveFile) -> String {
    let config = ron::ser::PrettyConfig::new().struct_names(false);
    ron::ser::to_string_pretty(save, config).expect("SaveFile always serialises")
}

#[cfg(test)]
mod tests {
    use super::*;

    fn sample() -> SaveFile {
        SaveFile {
            version: CURRENT_VERSION,
            game_version: "0.0.1".into(),
            save_counter: 7,
            meta: MetaProgress { runs_started: 3, unlocks: vec!["test_unlock".into()], ..Default::default() },
            run: Some(RunSave {
                seed_code: "7K3M-Q9TD".into(),
                ante: 2,
                ante_score: 450,
                wallet: 120,
                player: Some(PlayerSave { body: "test_sphere".into(), position: (0.0, 60.9, 0.0) }),
                perks: vec!["ghost".into()],
                attributes: vec![("damage".into(), 2)],
            }),
        }
    }

    #[test]
    fn round_trip() {
        let save = sample();
        assert_eq!(parse_save(&write_save(&save)).unwrap(), save);
    }

    #[test]
    fn newer_versions_are_refused() {
        let text = write_save(&SaveFile { version: CURRENT_VERSION + 1, ..sample() });
        assert_eq!(parse_save(&text), Err(LoadError::TooNew { found: CURRENT_VERSION + 1 }));
    }

    #[test]
    fn garbage_is_unreadable() {
        assert!(matches!(parse_save("(version: 1, meta: ("), Err(LoadError::Unreadable(_))));
        assert!(matches!(parse_save(""), Err(LoadError::Unreadable(_))));
    }

    /// Every save version ever shipped keeps a fixture that must still load.
    #[test]
    fn v1_fixture_still_loads() {
        let save = parse_save(include_str!("../../tests/fixtures/save_v1.ron")).unwrap();
        assert_eq!(save.meta.runs_started, 3);
        let run = save.run.unwrap();
        assert_eq!(run.seed_code, "7K3M-Q9TD");
        assert!(run.perks.is_empty(), "v1 had no build; migration leaves it empty");
        assert!(save.meta.leaderboard.is_empty());
        assert_eq!(save.version, CURRENT_VERSION);
    }

    #[test]
    fn leaderboard_keeps_the_best_runs() {
        let mut board = Vec::new();
        let e = |ante, points| LeaderEntry { seed_code: "A".into(), ante, points, outcome: "died".into() };
        add_to_leaderboard(&mut board, e(2, 900), 3);
        add_to_leaderboard(&mut board, e(4, 100), 3);
        add_to_leaderboard(&mut board, e(2, 1500), 3);
        let rank = add_to_leaderboard(&mut board, e(1, 50), 3);
        assert_eq!(rank, None, "too low to place");
        assert_eq!(board.iter().map(|x| (x.ante, x.points)).collect::<Vec<_>>(), vec![(4, 100), (2, 1500), (2, 900)]);
    }
}
