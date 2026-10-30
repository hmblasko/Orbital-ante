//! Input replay files: the exact per-physics-tick input of a run, so any bug
//! found by a bot or a player can be reproduced exactly.

use serde::{Deserialize, Serialize};

pub const REPLAY_VERSION: u32 = 2;

/// Gameplay actions captured each tick, in this order. Menu and debug
/// actions are left out on purpose.
pub const RECORDED_ACTIONS: &[&str] = &[
    "move_forward",
    "move_back",
    "move_left",
    "move_right",
    "look_up",
    "look_down",
    "look_left",
    "look_right",
    "jump",
    "crouch",
    "match_velocity",
    "interact",
    "use_oxygen_tank",
    "tether_toggle",
];

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct ReplayFrame {
    /// Raw strength of each action in `RECORDED_ACTIONS`, 0.0 to 1.0.
    pub a: Vec<f32>,
    /// Mouse movement this tick, in pixels.
    #[serde(default)]
    pub m: (f32, f32),
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct Replay {
    pub version: u32,
    pub seed_code: String,
    pub scene: String,
    pub physics_ticks_per_second: i32,
    pub frames: Vec<ReplayFrame>,
    /// Player position at the end, used to check the replay reproduced the run.
    pub final_position: (f32, f32, f32),
}

impl Replay {
    pub fn to_text(&self) -> String {
        ron::ser::to_string_pretty(self, ron::ser::PrettyConfig::new().compact_arrays(true).struct_names(false))
            .expect("Replay always serialises")
    }

    pub fn from_text(text: &str) -> Result<Self, String> {
        let replay: Replay = ron::from_str(text).map_err(|e| e.to_string())?;
        if replay.version != REPLAY_VERSION {
            return Err(format!("replay version {} not supported (expected {REPLAY_VERSION})", replay.version));
        }
        if let Some(bad) = replay.frames.iter().position(|f| f.a.len() != RECORDED_ACTIONS.len()) {
            return Err(format!("frame {bad} has the wrong number of actions"));
        }
        Ok(replay)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn round_trip() {
        let replay = Replay {
            version: REPLAY_VERSION,
            seed_code: "AAAA-BBBB".into(),
            scene: "res://x.tscn".into(),
            physics_ticks_per_second: 60,
            frames: vec![ReplayFrame { a: vec![0.0; RECORDED_ACTIONS.len()], m: (1.5, -2.0) }],
            final_position: (1.0, 2.0, 3.0),
        };
        assert_eq!(Replay::from_text(&replay.to_text()).unwrap(), replay);
    }

    #[test]
    fn rejects_wrong_action_count() {
        let replay = Replay {
            version: REPLAY_VERSION,
            seed_code: String::new(),
            scene: String::new(),
            physics_ticks_per_second: 60,
            frames: vec![ReplayFrame { a: vec![0.0; 2], m: (0.0, 0.0) }],
            final_position: (0.0, 0.0, 0.0),
        };
        assert!(Replay::from_text(&replay.to_text()).is_err());
    }
}
