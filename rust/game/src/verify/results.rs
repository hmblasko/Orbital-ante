//! The JSON file each test run writes, read by the `oa_verify` tool.

use serde::{Deserialize, Serialize};

use super::detectors::DetectorHit;

#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct FrameTimeSummary {
    pub frames: u32,
    pub average_ms: f32,
    pub worst_1_percent_ms: f32,
    pub max_ms: f32,
}

#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct RunResults {
    /// "smoke", "replay" or "screenshots".
    pub kind: String,
    pub seed_code: String,
    pub bot: String,
    pub scene: String,
    pub game_seconds: f32,
    pub passed: bool,
    pub failures: Vec<String>,
    pub detector_hits: Vec<DetectorHit>,
    /// Measured on the machine that ran the test. Headless runs don't render,
    /// so these are not rendering frame times.
    pub frame_times: FrameTimeSummary,
    pub final_position: (f32, f32, f32),
    /// Replay runs: how far the end position was from the recording.
    pub replay_drift_m: Option<f32>,
    pub replay_file: Option<String>,
    pub screenshots: Vec<String>,
    pub headless: bool,
}
