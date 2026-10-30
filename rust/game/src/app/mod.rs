//! Start-up and top-level scene flow: boot, menu, gameplay.
//!
//! `GameRoot` is the main scene and stays loaded for the whole session; it
//! swaps menu and gameplay scenes as its children. That keeps test tooling
//! and (later) GameState alive across scene changes without extra autoloads.

pub mod args;
pub mod crash;
pub mod root;

pub use root::GameRoot;
