//! Ante Score, Wallet, multiplier and diminishing returns (CLAUDE.md
//! sections 3 and 18): the pure maths, perks and synergy tags, the run's
//! state, the balance simulator, the `Points` singleton and the
//! `AnteDirector` that runs the loop in the game.

pub mod data;
pub mod maths;
pub mod perks;
pub mod run;
pub mod sim;
pub mod director;
pub mod singleton;

pub use director::AnteDirector;
pub use singleton::Points;
