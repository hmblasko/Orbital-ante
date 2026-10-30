//! Crash handler (CLAUDE.md section 15): if Rust code panics, write a local
//! log with the seed and build version. Nothing is sent anywhere.
//! Engine-level crashes are covered by Godot's own log in user://logs/.

use std::backtrace::Backtrace;
use std::fs;
use std::path::PathBuf;
use std::sync::{Mutex, Once};
use std::time::{SystemTime, UNIX_EPOCH};

static CONTEXT: Mutex<String> = Mutex::new(String::new());
static INSTALL: Once = Once::new();

/// Text written at the top of every crash log, such as seed and version.
pub fn set_context(text: String) {
    if let Ok(mut ctx) = CONTEXT.lock() {
        *ctx = text;
    }
}

/// Install the panic hook once. `dir` is an absolute folder path.
pub fn install(dir: PathBuf) {
    INSTALL.call_once(move || {
        let previous = std::panic::take_hook();
        std::panic::set_hook(Box::new(move |info| {
            let when = SystemTime::now().duration_since(UNIX_EPOCH).map_or(0, |d| d.as_secs());
            let context = CONTEXT.lock().map(|c| c.clone()).unwrap_or_default();
            let text = format!("Orbital Ante crash log\nunix_time: {when}\n{context}\n\n{info}\n\n{}", Backtrace::force_capture());
            if fs::create_dir_all(&dir).is_ok() {
                let _ = fs::write(dir.join(format!("crash_{when}.txt")), text);
            }
            previous(info);
        }));
    });
}
