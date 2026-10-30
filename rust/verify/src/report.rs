//! The HTML verification dashboard (section 19.6).

use std::fmt::Write;
use std::path::Path;

use crate::compare::{COMPARE_FILE, CompareReport};
use crate::gates;
use crate::logcheck::LogSummary;

fn esc(s: &str) -> String {
    s.replace('&', "&amp;").replace('<', "&lt;").replace('>', "&gt;").replace('"', "&quot;")
}

fn subdirs(dir: &Path) -> Vec<String> {
    let mut v: Vec<String> = std::fs::read_dir(dir)
        .map(|r| r.filter_map(|e| e.ok()).filter(|e| e.path().is_dir()).map(|e| e.file_name().to_string_lossy().into_owned()).collect())
        .unwrap_or_default();
    v.sort();
    v
}

/// Inline SVG chart of a frame-time CSV written by the in-game overlay.
fn frame_chart(csv: &str, budget: f64, floor: f64) -> String {
    let rows: Vec<(f64, f64, f64)> = csv
        .lines()
        .skip(1)
        .filter_map(|l| {
            let c: Vec<&str> = l.split(',').collect();
            Some((c.first()?.parse().ok()?, c.get(2)?.parse().ok()?, c.get(3)?.parse().ok()?))
        })
        .collect();
    if rows.len() < 2 {
        return "<p>Not enough rows to chart.</p>".into();
    }
    let (w, h) = (640.0, 180.0);
    let t_max = rows.last().unwrap().0.max(1.0);
    let ms_max = rows.iter().map(|r| r.2).fold(floor * 1.4, f64::max);
    let x = |t: f64| t / t_max * w;
    let y = |ms: f64| h - ms / ms_max * h;
    let line = |f: &dyn Fn(&(f64, f64, f64)) -> f64| rows.iter().map(|r| format!("{:.1},{:.1}", x(r.0), y(f(r)))).collect::<Vec<_>>().join(" ");
    format!(
        r#"<svg viewBox="0 0 {w} {h}" role="img" aria-label="Frame time over the session"><line x1="0" x2="{w}" y1="{b:.1}" y2="{b:.1}" class="budget"/><line x1="0" x2="{w}" y1="{f:.1}" y2="{f:.1}" class="floor"/><polyline points="{avg}" class="avg"/><polyline points="{worst}" class="worst"/></svg><p class="legend"><span class="k avg">average</span> <span class="k worst">worst 1%</span> <span class="k budget">{budget} ms budget</span> <span class="k floor">{floor} ms floor</span></p>"#,
        b = y(budget),
        f = y(floor),
        avg = line(&|r| r.1),
        worst = line(&|r| r.2),
    )
}

pub fn build(dir: &Path, out_html: &Path, title: &str, budget_ms: f64, floor_ms: f64) -> Result<(), String> {
    let assets_name = format!("{}_files", out_html.file_stem().unwrap().to_string_lossy());
    let assets = out_html.parent().unwrap_or(Path::new(".")).join(&assets_name);
    std::fs::create_dir_all(&assets).map_err(|e| e.to_string())?;
    let copy = |src: &Path, prefix: &str| -> String {
        let name = format!("{prefix}_{}", src.file_name().unwrap().to_string_lossy());
        let _ = std::fs::copy(src, assets.join(&name));
        format!("{assets_name}/{name}")
    };

    let mut h = String::new();
    let gates = gates::load(dir);
    let count = |s: &str| gates.iter().filter(|g| g.status == s).count();
    write!(h, "<h1>{}</h1><p class=\"muted\">{} gates: {} passed, {} failed, {} not run.</p>", esc(title), gates.len(), count("pass"), count("fail"), count("not_run")).unwrap();

    h += "<h2>Gates</h2><table><thead><tr><th>Gate</th><th>Result</th><th>Detail</th></tr></thead><tbody>";
    for g in &gates {
        let label = match g.status.as_str() {
            "pass" => "PASS",
            "fail" => "FAIL",
            _ => "NOT RUN",
        };
        write!(h, "<tr><td>{}</td><td><span class=\"pill {}\">{label}</span></td><td>{}</td></tr>", esc(&g.name), g.status, esc(&g.detail)).unwrap();
    }
    h += "</tbody></table>";

    h += "<h2>Game runs</h2>";
    let mut any_run = false;
    for sub in subdirs(dir) {
        let run_dir = dir.join(&sub);
        let Ok(text) = std::fs::read_to_string(run_dir.join("results.json")) else { continue };
        let Ok(v) = serde_json::from_str::<serde_json::Value>(&text) else { continue };
        any_run = true;
        let ft = &v["frame_times"];
        write!(
            h,
            "<h3>{}</h3><table><tbody><tr><th>Kind</th><td>{}</td></tr><tr><th>Seed</th><td><code>{}</code></td></tr><tr><th>Result</th><td>{}</td></tr><tr><th>Game time</th><td>{:.1} s</td></tr>",
            esc(&sub),
            esc(v["kind"].as_str().unwrap_or("")),
            esc(v["seed_code"].as_str().unwrap_or("")),
            if v["passed"].as_bool().unwrap_or(false) { "<span class=\"pill pass\">PASS</span>" } else { "<span class=\"pill fail\">FAIL</span>" },
            v["game_seconds"].as_f64().unwrap_or(0.0)
        )
        .unwrap();
        if let Some(d) = v["replay_drift_m"].as_f64() {
            write!(h, "<tr><th>Replay drift</th><td>{d:.4} m</td></tr>").unwrap();
        }
        let hits = v["detector_hits"].as_array().map_or(0, |a| a.len());
        write!(h, "<tr><th>Detector hits</th><td>{hits}</td></tr>").unwrap();
        if v["headless"].as_bool().unwrap_or(false) {
            h += "<tr><th>Frame times</th><td>Headless run: no rendering, so no frame times are reported.</td></tr>";
        } else if ft["frames"].as_u64().unwrap_or(0) > 0 {
            write!(h, "<tr><th>Frame times (this machine, not a Deck)</th><td>avg {:.2} ms, worst 1% {:.2} ms, max {:.2} ms</td></tr>", ft["average_ms"].as_f64().unwrap_or(0.0), ft["worst_1_percent_ms"].as_f64().unwrap_or(0.0), ft["max_ms"].as_f64().unwrap_or(0.0)).unwrap();
        }
        if let Ok(log) = std::fs::read_to_string(run_dir.join("logcheck.json"))
            && let Ok(s) = serde_json::from_str::<LogSummary>(&log)
        {
            write!(h, "<tr><th>Log</th><td>{} errors, {} warnings ({} known machine-only lines ignored)</td></tr>", s.errors, s.warnings, s.ignored).unwrap();
        }
        h += "</tbody></table>";
        if let Some(fails) = v["failures"].as_array().filter(|a| !a.is_empty()) {
            h += "<ul>";
            for f in fails {
                write!(h, "<li>{}</li>", esc(f.as_str().unwrap_or(""))).unwrap();
            }
            h += "</ul>";
        }
    }
    if !any_run {
        h += "<p>No game runs in this report.</p>";
    }

    h += "<h2>Screenshots</h2>";
    match std::fs::read_to_string(dir.join(COMPARE_FILE)).ok().and_then(|t| serde_json::from_str::<CompareReport>(&t).ok()) {
        None => h += "<p>Screenshot comparison not run.</p>",
        Some(c) => {
            write!(h, "<p class=\"muted\">SSIM threshold {}. New baselines are approved by Matthew only.</p><div class=\"shots\">", c.threshold).unwrap();
            for s in &c.shots {
                let cur = copy(&s.current, "current");
                let base = s.baseline.as_ref().map(|b| copy(b, "baseline"));
                write!(h, "<figure><img src=\"{cur}\" alt=\"{} current\">", esc(&s.name)).unwrap();
                if let Some(b) = base.filter(|_| s.status != "match") {
                    write!(h, "<img src=\"{b}\" alt=\"{} baseline\">", esc(&s.name)).unwrap();
                }
                let score = s.ssim.map_or("no baseline".to_string(), |v| format!("SSIM {v:.4}"));
                write!(h, "<figcaption><b>{}</b> <span class=\"pill {}\">{}</span> {score}", esc(&s.name), if s.status == "match" { "pass" } else if s.status == "new" { "not_run" } else { "fail" }, esc(&s.status)).unwrap();
                for p in &s.problems {
                    write!(h, "<br>{}", esc(p)).unwrap();
                }
                h += "</figcaption></figure>";
            }
            h += "</div>";
            if !c.missing.is_empty() {
                write!(h, "<p>Baselines with no new screenshot: {}</p>", esc(&c.missing.join(", "))).unwrap();
            }
        }
    }

    h += "<h2>Frame times on the Steam Deck</h2>";
    let csv = std::fs::read_to_string(dir.join("deck_frametime_log.csv")).ok();
    match csv {
        Some(csv) => h += &frame_chart(&csv, budget_ms, floor_ms),
        None => h += "<p>Not measured: no Deck frame-time log in this run. Copy <code>frametime_log.csv</code> from the Deck into the run folder as <code>deck_frametime_log.csv</code>.</p>",
    }

    h += "<h2>Not verified</h2><ul>";
    for g in gates.iter().filter(|g| g.status == "not_run") {
        write!(h, "<li><b>{}</b>: {}</li>", esc(&g.name), esc(&g.detail)).unwrap();
    }
    h += "</ul>";

    let page = format!("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Verification report</title><style>{CSS}</style></head><body>{h}</body></html>");
    std::fs::write(out_html, page).map_err(|e| e.to_string())
}

const CSS: &str = r#"
:root{--bg:#fff;--fg:#1b1d22;--muted:#5d6370;--line:#dde0e6;--pass:#1d7a3a;--fail:#b3261e;--nr:#8a6d00;--chart1:#2f6fd6;--chart2:#c2501b}
@media (prefers-color-scheme:dark){:root{--bg:#15171b;--fg:#e8eaee;--muted:#9aa1ad;--line:#2c3038;--pass:#5fd17f;--fail:#ff7a6e;--nr:#e2bf4a;--chart1:#7aa7ff;--chart2:#ff9a66}}
body{background:var(--bg);color:var(--fg);font:15px/1.5 system-ui,sans-serif;max-width:960px;margin:0 auto;padding:16px}
.muted{color:var(--muted)}table{border-collapse:collapse;width:100%;margin:8px 0}td,th{border-bottom:1px solid var(--line);padding:6px 8px;text-align:left;vertical-align:top}
.pill{font-weight:700;font-size:13px}.pill.pass{color:var(--pass)}.pill.fail{color:var(--fail)}.pill.not_run{color:var(--nr)}
.shots{display:grid;grid-template-columns:repeat(auto-fill,minmax(280px,1fr));gap:12px}figure{margin:0}img{width:100%;border:1px solid var(--line)}
svg{width:100%;height:auto;border:1px solid var(--line)}polyline{fill:none;stroke-width:2}.avg{stroke:var(--chart1)}.worst{stroke:var(--chart2)}
line.budget{stroke:var(--pass);stroke-dasharray:4 4}line.floor{stroke:var(--fail);stroke-dasharray:4 4}.legend .k{margin-right:12px}
"#;
