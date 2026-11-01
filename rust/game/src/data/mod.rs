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
        parse::<crate::player::controller::MovementData>(include_str!(
            "../../../../godot/data/player/movement.ron"
        ))
        .unwrap();
    }

    #[test]
    fn test_sphere_parses() {
        parse::<crate::orbits::body::BodyData>(include_str!(
            "../../../../godot/data/bodies/test_sphere.ron"
        ))
        .unwrap();
    }

    #[test]
    fn test_sphere_has_every_landform_and_landmarks_parse() {
        use crate::procgen::landforms::LandformKind::*;
        let body = parse::<crate::orbits::body::BodyData>(include_str!(
            "../../../../godot/data/bodies/test_sphere.ron"
        ))
        .unwrap();
        let kinds: Vec<_> = body
            .terrain
            .landforms
            .placed
            .iter()
            .map(|l| l.kind)
            .collect();
        for k in [Mountain, Mesa, Butte, Volcano, Crater, Ridge, Dunes, Sinkhole, Canyon, River, Lake, Pool] {
            assert!(kinds.contains(&k), "{k:?} missing from the showcase");
        }
        assert!(body.hoodoos.is_some());
        let marks = parse::<crate::orbits::landmarks::LandmarkData>(include_str!(
            "../../../../godot/data/bodies/test_sphere_landmarks.ron"
        ))
        .unwrap();
        assert_eq!((marks.surface.len(), marks.orbiters.len()), (3, 3));
    }

    #[test]
    fn test_moonlet_parses_with_air_and_ice() {
        let body = parse::<crate::orbits::body::BodyData>(include_str!(
            "../../../../godot/data/bodies/test_moonlet.ron"
        ))
        .unwrap();
        assert!(body.atmosphere.is_some_and(|a| a.breathable));
        assert!(body.land_look.frozen);
        assert!(body.terrain.landforms.water_level_m.is_some());
    }

    #[test]
    fn suit_data_parses() {
        parse::<crate::player::suit::SuitData>(include_str!(
            "../../../../godot/data/player/suit.ron"
        ))
        .unwrap();
    }

    #[test]
    fn interactables_parse() {
        parse::<crate::player::controller::InteractData>(include_str!(
            "../../../../godot/data/player/interactables.ron"
        ))
        .unwrap();
    }

    #[test]
    fn oxygen_tank_scatter_parses() {
        parse::<crate::items::oxygen_tank::ScatterData>(include_str!(
            "../../../../godot/data/items/oxygen_tanks.ron"
        ))
        .unwrap();
    }

    #[test]
    fn frame_overlay_parses() {
        parse::<crate::ui_bridge::frame_overlay::OverlayData>(include_str!(
            "../../../../godot/data/debug/frame_overlay.ron"
        ))
        .unwrap();
    }

    #[test]
    fn starter_lander_parses() {
        parse::<crate::flight::ship::ShipData>(include_str!("../../../../godot/data/ships/starter_lander.ron")).unwrap();
    }

    #[test]
    fn dash_styles_parse() {
        let d = parse::<crate::flight::dash::DashData>(include_str!("../../../../godot/data/ships/dash.ron")).unwrap();
        assert_eq!(d.styles.len(), 3);
    }

    #[test]
    fn rover_files_parse() {
        let d = parse::<crate::flight::rover::RoverData>(include_str!("../../../../godot/data/vehicles/moon_buggy.ron")).unwrap();
        assert!(!d.wheels.mounts.is_empty());
        parse::<crate::flight::rover::SpawnsData>(include_str!("../../../../godot/data/vehicles/rovers.ron")).unwrap();
        let h = parse::<crate::orbits::home_base::HomeData>(include_str!("../../../../godot/data/bodies/home_base.ron")).unwrap();
        assert!(h.rover.is_some());
    }
}
