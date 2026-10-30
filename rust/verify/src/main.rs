//! `oa_verify`: the offline half of the verification layer (CLAUDE.md
//! section 19). Run `oa_verify help` for the commands.

mod bugs;
mod compare;
mod gates;
mod image;
mod logcheck;
mod report;

use std::path::{Path, PathBuf};
use std::process::ExitCode;

const HELP: &str = "oa_verify - Orbital Ante verification tool

  gate <run_dir> <name> <pass|fail|not_run> <detail> [severity] [run_subdir]
      Record one gate result in <run_dir>/gates.json.
  logcheck <godot.log> <out.json> [ignore.ron] [max_errors]
      Count errors and warnings in a captured Godot log. Exit 1 if over max.
  compare <baseline_dir> <current_dir> <run_dir> [threshold]
      SSIM-compare screenshots with approved baselines plus automatic image
      checks. Writes <run_dir>/screenshots_compare.json. Exit 1 if anything is flagged.
  approve <current_dir> <baseline_dir>
      Copy screenshots into the baselines. Only Matthew approves baselines.
  report <run_dir> <out.html> [title]
      Build the HTML dashboard.
  bugs <run_dir> [--file <owner/repo>]
      Draft bug reports from failed gates into <run_dir>/bugs.md, and with
      --file create GitHub issues through `gh`, skipping open duplicates.
";

fn main() -> ExitCode {
    let args: Vec<String> = std::env::args().skip(1).collect();
    match run(&args) {
        Ok(code) => code,
        Err(e) => {
            eprintln!("oa_verify: {e}\n\n{HELP}");
            ExitCode::from(2)
        }
    }
}

fn arg<'a>(args: &'a [String], i: usize, what: &str) -> Result<&'a str, String> {
    args.get(i).map(String::as_str).ok_or_else(|| format!("missing {what}"))
}

fn run(args: &[String]) -> Result<ExitCode, String> {
    let cmd = args.first().map(String::as_str).unwrap_or("help");
    match cmd {
        "gate" => {
            let gate = gates::Gate {
                name: arg(args, 2, "name")?.into(),
                status: arg(args, 3, "status")?.into(),
                detail: arg(args, 4, "detail")?.into(),
                severity: args.get(5).filter(|s| !s.is_empty()).cloned(),
                run: args.get(6).filter(|s| !s.is_empty()).cloned(),
            };
            gates::append(Path::new(arg(args, 1, "run_dir")?), gate)?;
            Ok(ExitCode::SUCCESS)
        }
        "logcheck" => {
            let log = std::fs::read(arg(args, 1, "log file")?).map_err(|e| e.to_string())?;
            let ignore: Vec<String> = match args.get(3) {
                Some(p) => ron::from_str(&std::fs::read_to_string(p).map_err(|e| e.to_string())?).map_err(|e| e.to_string())?,
                None => Vec::new(),
            };
            let max: u32 = args.get(4).map_or(Ok(0), |m| m.parse().map_err(|_| "max_errors must be a number".to_string()))?;
            let summary = logcheck::summarize(&String::from_utf8_lossy(&log), &ignore);
            std::fs::write(arg(args, 2, "output file")?, serde_json::to_string_pretty(&summary).unwrap()).map_err(|e| e.to_string())?;
            println!("{} errors, {} warnings, {} ignored", summary.errors, summary.warnings, summary.ignored);
            Ok(if summary.errors > max { ExitCode::FAILURE } else { ExitCode::SUCCESS })
        }
        "compare" => {
            let threshold: f64 = args.get(4).map_or(Ok(0.98), |t| t.parse().map_err(|_| "threshold must be a number".to_string()))?;
            let report = compare::compare(Path::new(arg(args, 1, "baseline dir")?), Path::new(arg(args, 2, "current dir")?), threshold);
            let run_dir = PathBuf::from(arg(args, 3, "run dir")?);
            std::fs::create_dir_all(&run_dir).map_err(|e| e.to_string())?;
            std::fs::write(run_dir.join(compare::COMPARE_FILE), serde_json::to_string_pretty(&report).unwrap()).map_err(|e| e.to_string())?;
            for s in &report.shots {
                println!("{:<32} {:<8} {}", s.name, s.status, s.ssim.map_or(String::new(), |v| format!("{v:.4}")));
            }
            let flagged = report.shots.iter().any(|s| s.status == "flagged" || s.status == "error") || !report.missing.is_empty();
            Ok(if flagged { ExitCode::FAILURE } else { ExitCode::SUCCESS })
        }
        "approve" => {
            let (from, to) = (Path::new(arg(args, 1, "current dir")?), Path::new(arg(args, 2, "baseline dir")?));
            std::fs::create_dir_all(to).map_err(|e| e.to_string())?;
            for entry in std::fs::read_dir(from).map_err(|e| e.to_string())?.filter_map(|e| e.ok()) {
                if entry.path().extension().is_some_and(|x| x == "png") {
                    std::fs::copy(entry.path(), to.join(entry.file_name())).map_err(|e| e.to_string())?;
                    println!("approved {}", entry.file_name().to_string_lossy());
                }
            }
            Ok(ExitCode::SUCCESS)
        }
        "report" => {
            let title = args.get(3).cloned().unwrap_or_else(|| "Verification report".into());
            report::build(Path::new(arg(args, 1, "run dir")?), Path::new(arg(args, 2, "output html")?), &title, 16.6, 25.0)?;
            Ok(ExitCode::SUCCESS)
        }
        "bugs" => {
            let dir = Path::new(arg(args, 1, "run dir")?);
            let build = std::env::var("OA_BUILD").unwrap_or_else(|_| "unknown".into());
            let drafts = bugs::drafts(dir, &build);
            std::fs::write(dir.join("bugs.md"), bugs::to_markdown(&drafts)).map_err(|e| e.to_string())?;
            println!("{} bug draft(s) in {}", drafts.len(), dir.join("bugs.md").display());
            if args.get(2).map(String::as_str) == Some("--file") {
                for line in bugs::file_with_gh(&drafts, arg(args, 3, "owner/repo")?) {
                    println!("{line}");
                }
            }
            Ok(ExitCode::SUCCESS)
        }
        "help" | "--help" | "-h" => {
            println!("{HELP}");
            Ok(ExitCode::SUCCESS)
        }
        other => Err(format!("unknown command '{other}'")),
    }
}
