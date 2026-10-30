//! Turns failed gates into bug drafts (section 19.4), and optionally files
//! them as GitHub issues with the `gh` command-line tool, skipping any whose
//! signature already has an open issue.

use std::path::Path;
use std::process::Command;

use crate::gates::{self, Gate};

pub struct BugDraft {
    pub severity: String,
    pub title: String,
    /// Stable text used to find duplicates: same failure, same signature.
    pub signature: String,
    pub body: String,
}

fn run_details(dir: &Path, gate: &Gate) -> String {
    let Some(run) = &gate.run else { return String::new() };
    let mut text = String::new();
    if let Ok(results) = std::fs::read_to_string(dir.join(run).join("results.json"))
        && let Ok(v) = serde_json::from_str::<serde_json::Value>(&results)
    {
        text += &format!("- Seed: `{}`\n- Scene: `{}`\n", v["seed_code"].as_str().unwrap_or("?"), v["scene"].as_str().unwrap_or("?"));
        if let Some(replay) = v["replay_file"].as_str() {
            text += &format!("- Replay file: `{replay}` (reproduce with `-- --replay=<file>`)\n");
        }
        if let Some(fails) = v["failures"].as_array() {
            for f in fails {
                text += &format!("- {}\n", f.as_str().unwrap_or(""));
            }
        }
    } else {
        text += "- No results.json: the game exited before writing results (possible crash).\n";
    }
    if let Ok(log) = std::fs::read_to_string(dir.join(run).join("godot.log")) {
        let tail: Vec<&str> = log.lines().filter(|l| l.contains("ERROR") || l.contains("panic")).take(15).collect();
        if !tail.is_empty() {
            text += &format!("\nLog excerpt:\n```\n{}\n```\n", tail.join("\n"));
        }
    }
    text
}

pub fn drafts(dir: &Path, build: &str) -> Vec<BugDraft> {
    gates::load(dir)
        .into_iter()
        .filter(|g| g.status == "fail")
        .map(|g| {
            let severity = g.severity.clone().unwrap_or_else(|| "S2".into());
            let signature = format!("oa-verify:{}", g.name.to_lowercase().replace(' ', "-"));
            let body = format!(
                "Automatically drafted by `oa_verify bugs`.\n\n- Severity: {severity}\n- Gate: {}\n- Detail: {}\n- Build: {build}\n{}\nSignature: `{signature}`\n",
                g.name,
                g.detail,
                run_details(dir, &g)
            );
            BugDraft { title: format!("[{severity}] {}: {}", g.name, g.detail), severity, signature, body }
        })
        .collect()
}

pub fn to_markdown(drafts: &[BugDraft]) -> String {
    if drafts.is_empty() {
        return "# Bug drafts\n\nNo failed gates, nothing to file.\n".into();
    }
    let mut md = String::from("# Bug drafts\n\n");
    for d in drafts {
        md += &format!("## {}\n\nSeverity: {}\n\n{}\n", d.title, d.severity, d.body);
    }
    md
}

/// File drafts on GitHub. Returns one line per draft describing what happened.
pub fn file_with_gh(drafts: &[BugDraft], repo: &str) -> Vec<String> {
    drafts
        .iter()
        .map(|d| {
            let existing = Command::new("gh")
                .args(["issue", "list", "--repo", repo, "--state", "open", "--search", &format!("\"{}\" in:body", d.signature), "--json", "number", "--jq", "length"])
                .output();
            match existing {
                Ok(out) if out.status.success() && String::from_utf8_lossy(&out.stdout).trim() != "0" => {
                    format!("skipped (already open): {}", d.title)
                }
                Ok(out) if out.status.success() => {
                    let created = Command::new("gh").args(["issue", "create", "--repo", repo, "--title", &d.title, "--body", &d.body]).output();
                    match created {
                        Ok(c) if c.status.success() => format!("filed: {}", String::from_utf8_lossy(&c.stdout).trim()),
                        Ok(c) => format!("failed to file {}: {}", d.title, String::from_utf8_lossy(&c.stderr).trim()),
                        Err(e) => format!("failed to run gh: {e}"),
                    }
                }
                Ok(out) => format!("could not search issues: {}", String::from_utf8_lossy(&out.stderr).trim()),
                Err(e) => format!("failed to run gh: {e}"),
            }
        })
        .collect()
}
