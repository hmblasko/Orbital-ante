//! `RunSeed` engine singleton: holds the current run's master seed.
//! Rust systems call `run_seed()` and derive their own `SeededRng` from it.

use std::time::{SystemTime, UNIX_EPOCH};

use godot::obj::Singleton;
use godot::prelude::*;

use super::code::{clamp_seed, decode_seed, encode_seed};
use super::rng::splitmix64;

#[derive(GodotClass)]
#[class(singleton, base = Object, init)]
pub struct RunSeed {
    seed: u64,
    base: Base<Object>,
}

#[godot_api]
impl RunSeed {
    /// The seed as a shareable code, such as "7K3M-Q9TD".
    #[func]
    pub fn get_code(&self) -> GString {
        GString::from(&encode_seed(self.seed))
    }

    /// Use a seed typed or shared by a player. Returns false if the code is invalid.
    #[func]
    pub fn set_code(&mut self, code: GString) -> bool {
        match decode_seed(&code.to_string()) {
            Some(seed) => {
                self.seed = seed;
                true
            }
            None => false,
        }
    }

    /// Start a fresh random seed (from the clock) and return its code.
    #[func]
    pub fn new_random(&mut self) -> GString {
        let nanos = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .map_or(0, |d| d.as_nanos() as u64);
        let mut state = nanos;
        self.seed = clamp_seed(splitmix64(&mut state));
        self.get_code()
    }

    pub fn seed(&self) -> u64 {
        self.seed
    }

    pub fn set_seed(&mut self, seed: u64) {
        self.seed = clamp_seed(seed);
    }
}

/// Current master seed. Use `Singleton::singleton()` on the class for more.
pub fn run_seed() -> u64 {
    RunSeed::singleton().bind().seed()
}
