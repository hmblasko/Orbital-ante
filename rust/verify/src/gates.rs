//! The gate list for one verification run (section 19.6: pass/fail for every
//! gate, with "not run" where applicable).

use std::path::Path;

use serde::{Deserialize, Serialize};

pub const GATES_FILE: &str = "gates.json";

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Gate {
    pub name: String,
    /// "pass", "fail" or "not_run".
    pub status: String,
    pub detail: String,
    /// S1 to S4, for failed gates that become bug reports.
    #[serde(default)]
    pub severity: Option<String>,
    /// Sub-folder with this gate's results.json and godot.log, if any.
    #[serde(default)]
    pub run: Option<String>,
}

pub fn load(dir: &Path) -> Vec<Gate> {
    std::fs::read_to_string(dir.join(GATES_FILE))
        .ok()
        .and_then(|t| serde_json::from_str(&t).ok())
        .unwrap_or_default()
}

pub fn append(dir: &Path, gate: Gate) -> Result<(), String> {
    if !["pass", "fail", "not_run"].contains(&gate.status.as_str()) {
        return Err(format!("status must be pass, fail or not_run, not '{}'", gate.status));
    }
    let mut gates = load(dir);
    gates.retain(|g| g.name != gate.name);
    gates.push(gate);
    std::fs::create_dir_all(dir).map_err(|e| e.to_string())?;
    std::fs::write(dir.join(GATES_FILE), serde_json::to_string_pretty(&gates).unwrap()).map_err(|e| e.to_string())
}
