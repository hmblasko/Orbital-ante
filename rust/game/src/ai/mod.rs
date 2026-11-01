//! Enemy AI (CLAUDE.md section 7): Patrol, Suspicious, Alert, Search, Return.
//!
//! - `brain`: the pure state machine.
//! - `enemy`: the Enemy node (body, senses, movement, shooting, damage).
//! - `camp`: spawns seeded enemy camps on bodies.

pub mod brain;
pub mod base;
pub mod camp;
pub mod enemy;
