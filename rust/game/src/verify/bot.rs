//! Autoplay bot profiles. Phase 1 has one, "wanderer", a stand-in for the
//! Explorer profile (section 19.2) until there are landmarks to visit. It
//! drives the player only through input actions, exactly like a human.
//! A behaviour-tree framework replaces this once bots have objectives.

use serde::Deserialize;

use super::replay::RECORDED_ACTIONS;
use crate::seed::SeededRng;

#[derive(Debug, Clone, Deserialize)]
pub struct WandererSettings {
    /// Seconds between decisions, picked at random in this range.
    pub decision_interval_s: (f32, f32),
    /// Chance per decision to stop walking for a moment.
    pub pause_chance: f64,
    pub turn_chance: f64,
    /// Strength of the look input while turning.
    pub turn_strength: (f32, f32),
    pub jump_chance: f64,
    pub crouch_chance: f64,
    /// Chance a jump keeps the button held, firing the jetpack once airborne,
    /// and for how long. Short enough that it never leaves the test sphere.
    pub jetpack_chance: f64,
    pub jetpack_hold_s: (f32, f32),
    /// Chance to hold match velocity for one decision.
    pub match_velocity_chance: f64,
}

/// What the bot wants pressed this tick: one strength per recorded action.
pub type ActionStrengths = Vec<f32>;

pub struct Wanderer {
    settings: WandererSettings,
    rng: SeededRng,
    time_to_decide: f32,
    walking: bool,
    turn: f32,
    jump_next_tick: bool,
    jetpack_hold_s: f32,
    matching: bool,
    crouching: bool,
}


fn index(action: &str) -> usize {
    RECORDED_ACTIONS.iter().position(|a| *a == action).expect("action is recorded")
}

impl Wanderer {
    pub fn new(settings: WandererSettings, rng: SeededRng) -> Self {
        Self {
            settings,
            rng,
            time_to_decide: 0.0,
            walking: true,
            turn: 0.0,
            jump_next_tick: false,
            jetpack_hold_s: 0.0,
            matching: false,
            crouching: false,
        }
    }

    /// Advance by `dt` seconds and return the inputs to hold this tick.
    pub fn tick(&mut self, dt: f32) -> ActionStrengths {
        self.time_to_decide -= dt;
        if self.time_to_decide <= 0.0 {
            self.decide();
        }
        let mut out = vec![0.0; RECORDED_ACTIONS.len()];
        if self.walking {
            out[index("move_forward")] = 1.0;
        }
        if self.turn > 0.0 {
            out[index("look_right")] = self.turn;
        } else if self.turn < 0.0 {
            out[index("look_left")] = -self.turn;
        }
        if self.jump_next_tick || self.jetpack_hold_s > 0.0 {
            out[index("jump")] = 1.0;
            self.jump_next_tick = false;
            self.jetpack_hold_s -= dt;
        }
        if self.matching {
            out[index("match_velocity")] = 1.0;
        }
        if self.crouching {
            out[index("crouch")] = 1.0;
        }
        out
    }

    /// True while the bot intends to move (used by the stuck detector).
    pub fn wants_to_move(&self) -> bool {
        self.walking
    }

    fn decide(&mut self) {
        let s = &self.settings;
        let (lo, hi) = s.decision_interval_s;
        self.time_to_decide = self.rng.range_f32(lo, hi);
        self.walking = !self.rng.chance(s.pause_chance);
        self.turn = if self.rng.chance(s.turn_chance) {
            let strength = self.rng.range_f32(s.turn_strength.0, s.turn_strength.1);
            if self.rng.chance(0.5) { strength } else { -strength }
        } else {
            0.0
        };
        self.jump_next_tick = self.rng.chance(s.jump_chance);
        if self.jump_next_tick && self.rng.chance(s.jetpack_chance) {
            self.jetpack_hold_s = self.rng.range_f32(s.jetpack_hold_s.0, s.jetpack_hold_s.1);
        }
        self.matching = self.rng.chance(s.match_velocity_chance);
        self.crouching = self.rng.chance(s.crouch_chance);
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn settings() -> WandererSettings {
        WandererSettings {
            decision_interval_s: (0.5, 2.0),
            pause_chance: 0.1,
            turn_chance: 0.5,
            turn_strength: (0.2, 0.8),
            jump_chance: 0.2,
            crouch_chance: 0.1,
            jetpack_chance: 0.5,
            jetpack_hold_s: (0.3, 1.5),
            match_velocity_chance: 0.1,
        }
    }

    #[test]
    fn same_seed_same_inputs() {
        let mut a = Wanderer::new(settings(), SeededRng::derive(1, "bot"));
        let mut b = Wanderer::new(settings(), SeededRng::derive(1, "bot"));
        for _ in 0..600 {
            assert_eq!(a.tick(1.0 / 60.0), b.tick(1.0 / 60.0));
        }
    }

    #[test]
    fn strengths_are_valid_and_it_moves() {
        let mut bot = Wanderer::new(settings(), SeededRng::derive(2, "bot"));
        let mut forward_ticks = 0;
        for _ in 0..3600 {
            let s = bot.tick(1.0 / 60.0);
            assert!(s.iter().all(|v| (0.0..=1.0).contains(v)));
            if s[index("move_forward")] > 0.0 {
                forward_ticks += 1;
            }
        }
        assert!(forward_ticks > 1800);
    }

    #[test]
    fn uses_the_jetpack_in_bursts() {
        let mut bot = Wanderer::new(settings(), SeededRng::derive(3, "bot"));
        let (mut held, mut longest, mut run) = (0, 0, 0);
        for _ in 0..3600 {
            if bot.tick(1.0 / 60.0)[index("jump")] > 0.0 {
                held += 1;
                run += 1;
                longest = longest.max(run);
            } else {
                run = 0;
            }
        }
        assert!(held > 0);
        // Never longer than the longest burst (1.5 s = 90 ticks, plus the jump tick).
        assert!(longest <= 91, "held jump for {longest} ticks");
    }
}
