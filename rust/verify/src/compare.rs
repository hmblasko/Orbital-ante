//! Compare a folder of new screenshots with approved baselines.

use std::path::{Path, PathBuf};

use serde::{Deserialize, Serialize};

use crate::image::{automatic_checks, load_png, ssim};

pub const COMPARE_FILE: &str = "screenshots_compare.json";

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ShotResult {
    pub name: String,
    /// "match", "flagged", "new" (no baseline yet) or "error".
    pub status: String,
    pub ssim: Option<f64>,
    pub problems: Vec<String>,
    pub current: PathBuf,
    pub baseline: Option<PathBuf>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct CompareReport {
    pub threshold: f64,
    pub shots: Vec<ShotResult>,
    /// Baselines with no matching new screenshot.
    pub missing: Vec<String>,
}

fn pngs(dir: &Path) -> Vec<PathBuf> {
    let mut v: Vec<PathBuf> = std::fs::read_dir(dir)
        .map(|r| r.filter_map(|e| e.ok()).map(|e| e.path()).filter(|p| p.extension().is_some_and(|x| x == "png")).collect())
        .unwrap_or_default();
    v.sort();
    v
}

pub fn compare(baseline_dir: &Path, current_dir: &Path, threshold: f64) -> CompareReport {
    let mut shots = Vec::new();
    for current in pngs(current_dir) {
        let name = current.file_stem().unwrap().to_string_lossy().into_owned();
        let baseline = baseline_dir.join(current.file_name().unwrap());
        let mut result = ShotResult { name, status: "new".into(), ssim: None, problems: Vec::new(), current: current.clone(), baseline: None };
        match load_png(&current) {
            Err(e) => {
                result.status = "error".into();
                result.problems.push(e);
            }
            Ok(img) => {
                result.problems = automatic_checks(&img);
                if baseline.exists() {
                    result.baseline = Some(baseline.clone());
                    match load_png(&baseline).and_then(|b| ssim(&b, &img)) {
                        Ok(score) => {
                            result.ssim = Some(score);
                            result.status = if score >= threshold { "match" } else { "flagged" }.into();
                        }
                        Err(e) => {
                            result.status = "error".into();
                            result.problems.push(e);
                        }
                    }
                }
                if !result.problems.is_empty() && result.status == "match" {
                    result.status = "flagged".into();
                }
            }
        }
        shots.push(result);
    }
    let have: Vec<String> = shots.iter().map(|s| s.name.clone()).collect();
    let missing = pngs(baseline_dir)
        .into_iter()
        .map(|p| p.file_stem().unwrap().to_string_lossy().into_owned())
        .filter(|n| !have.contains(n))
        .collect();
    CompareReport { threshold, shots, missing }
}
