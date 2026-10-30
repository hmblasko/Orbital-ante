//! Loading tunable values from RON files under `res://data/`.
//!
//! A broken or missing data file logs an error instead of crashing the game.

use godot::classes::FileAccess;
use godot::prelude::*;
use serde::de::DeserializeOwned;

/// Parse RON text into `T`. Pure Rust, so unit tests can call it without Godot.
pub fn parse<T: DeserializeOwned>(text: &str) -> Result<T, ron::error::SpannedError> {
    ron::from_str(text)
}

/// Load `res_path` through Godot (works inside exported .pck files).
/// Logs an error and returns `None` if the file is missing or invalid.
pub fn try_load<T: DeserializeOwned>(res_path: &str) -> Option<T> {
    let text = FileAccess::get_file_as_string(res_path).to_string();
    if text.is_empty() {
        godot_error!("data: could not read {res_path}");
        return None;
    }
    parse(&text)
        .map_err(|e| godot_error!("data: {res_path} is invalid: {e}"))
        .ok()
}

/// Like [`try_load`], but falls back to `embedded`, the copy of the same file
/// compiled into the library, so the game keeps running.
pub fn load<T: DeserializeOwned>(res_path: &str, embedded: &str) -> T {
    try_load(res_path).unwrap_or_else(|| {
        godot_warn!("data: using the built-in copy of {res_path}");
        parse(embedded).unwrap_or_else(|e| panic!("built-in copy of {res_path} is invalid: {e}"))
    })
}

/// Data-file validation: every shipped RON file must parse into its struct.
#[cfg(test)]
mod tests {
    use super::parse;

    #[test]
    fn movement_data_parses() {
        parse::<crate::player::controller::MovementData>(include_str!("../../../../godot/data/player/movement.ron")).unwrap();
    }

    #[test]
    fn test_sphere_parses() {
        parse::<crate::orbits::body::BodyData>(include_str!("../../../../godot/data/bodies/test_sphere.ron")).unwrap();
    }

    #[test]
    fn frame_overlay_parses() {
        parse::<crate::ui_bridge::frame_overlay::OverlayData>(include_str!("../../../../godot/data/debug/frame_overlay.ron")).unwrap();
    }
}
