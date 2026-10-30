//! Counts errors and warnings in a captured Godot log (stdout + stderr).

use serde::{Deserialize, Serialize};

#[derive(Debug, Default, Serialize, Deserialize)]
pub struct LogSummary {
    pub errors: u32,
    pub warnings: u32,
    pub error_lines: Vec<String>,
    pub ignored: u32,
}

/// `ignore` lists text that marks known, harmless lines from the test
/// machine itself (for example "no sound card"), from data/verify/log_ignore.ron.
pub fn summarize(log: &str, ignore: &[String]) -> LogSummary {
    let mut s = LogSummary::default();
    for line in log.lines() {
        let line = line.trim();
        let is_error = line.starts_with("ERROR:") || line.starts_with("SCRIPT ERROR:");
        let is_warning = line.starts_with("WARNING:");
        if !is_error && !is_warning {
            continue;
        }
        if ignore.iter().any(|pat| line.contains(pat.as_str())) {
            s.ignored += 1;
        } else if is_error {
            s.errors += 1;
            if s.error_lines.len() < 10 {
                s.error_lines.push(line.to_string());
            }
        } else {
            s.warnings += 1;
        }
    }
    s
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn counts_and_ignores() {
        let log = "Godot Engine v4.7\nERROR: real problem\nWARNING: All audio drivers failed\n   at: x\nWARNING: odd\nSCRIPT ERROR: oops\n";
        let s = summarize(log, &["All audio drivers failed".to_string()]);
        assert_eq!((s.errors, s.warnings, s.ignored), (2, 1, 1));
        assert_eq!(s.error_lines[0], "ERROR: real problem");
    }
}
