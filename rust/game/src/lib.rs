//! Orbital Ante game code. Godot loads this crate as a GDExtension library.
//!
//! Module layout follows CLAUDE.md section 14. Modules that are still empty
//! say which phase fills them in.

use godot::prelude::*;

pub mod ai;
pub mod app;
pub mod audio;
pub mod combat;
pub mod data;
pub mod flight;
pub mod horror;
pub mod items;
pub mod orbits;
pub mod player;
pub mod points;
pub mod procgen;
pub mod save;
pub mod seed;
pub mod ui_bridge;
pub mod verify;

struct OrbitalAnte;

// `unsafe impl` because Godot calls into this library through raw C function
// pointers; gdext handles the details, we only promise this is the entry point.
// Engine singletons (RunSeed, SaveData) register themselves through
// `#[class(singleton)]`. CLAUDE.md rule 7 allows only GameState, RunSeed,
// Audio, Points, Notifications and SaveData.
#[gdextension]
unsafe impl ExtensionLibrary for OrbitalAnte {}
