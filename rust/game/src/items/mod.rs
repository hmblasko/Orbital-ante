//! Items, rarities, chests and scratch-offs (CLAUDE.md section 9).
//! Phase 2 adds the oxygen tank pickup; Phase 8 adds dishes and damaged
//! satellites to fix (`sites`).

pub mod oxygen_tank;
pub mod sites;

pub use oxygen_tank::{OxygenTankPickup, OxygenTankScatter};
