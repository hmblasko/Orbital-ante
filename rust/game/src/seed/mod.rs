//! One master run seed; every system derives its own RNG stream from it
//! (CLAUDE.md standing rule 6), so runs are reproducible and shareable.
//!
//! Serves pillar 3 (every run counts and teaches: a seed can be replayed and
//! shared) and fun rule 7 (people fun: shareable seeds).

pub mod code;
pub mod rng;
pub mod singleton;

pub use code::{decode_seed, encode_seed};
pub use rng::SeededRng;
pub use singleton::RunSeed;
