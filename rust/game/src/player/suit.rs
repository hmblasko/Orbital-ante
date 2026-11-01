//! The spacesuit: oxygen, health, jetpack fuel and carried oxygen tanks
//! (CLAUDE.md section 6). Pure Rust state with a fixed-step update, so it is
//! unit tested and replays deterministically.
//!
//! Serves pillar 2 (goofy but deadly: real danger from running out of air)
//! and pillar 3 (each death teaches). Fun rules 8 (fair failure: a visible
//! warning before oxygen can hurt you, and a one-line cause of death) and
//! 3 (competence: three readable gauges, nothing hidden).

use serde::Deserialize;

#[derive(Debug, Clone, Deserialize)]
pub struct SuitData {
    pub health_max: f32,
    /// Seconds of air in a full suit.
    pub oxygen_capacity_s: f32,
    /// Seconds of air restored per second at a supply point (the ship, later).
    pub oxygen_refill_per_s: f32,
    /// Health lost per second with an empty tank.
    pub suffocation_damage_per_s: f32,
    /// At or below this fraction the HUD warns and the helmet sounds muffled.
    pub low_oxygen_fraction: f32,
    pub jetpack: JetpackData,
    pub tanks: TankData,
}

#[derive(Debug, Clone, Deserialize)]
pub struct JetpackData {
    /// Seconds of full thrust in a full tank.
    pub fuel_capacity_s: f32,
    /// Acceleration in m/s^2 for stick (forward/back/strafe) thrust.
    pub thrust_acceleration: f32,
    /// Acceleration in m/s^2 for up/down thrust.
    pub vertical_acceleration: f32,
    /// Acceleration in m/s^2 used to cancel velocity relative to the nearest body.
    pub match_velocity_acceleration: f32,
    /// Fuel refills on the ground after this many seconds without thrusting.
    pub ground_refuel_delay_s: f32,
    pub ground_refuel_per_s: f32,
    /// Supply points refill fuel this fast.
    pub supply_refuel_per_s: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct TankData {
    /// Seconds of air one tank adds when used.
    pub oxygen_per_tank_s: f32,
    pub max_carried: u32,
}

/// What the player and the world are doing this step.
#[derive(Debug, Clone, Copy, Default)]
pub struct SuitInput {
    /// The player is somewhere without breathable air (all of Phase 2).
    pub in_vacuum: bool,
    pub at_supply_point: bool,
    pub on_ground: bool,
    /// How hard the jetpack is firing this step, 0.0 to 1.0 (all thrusters combined).
    pub thrust: f32,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum DeathCause {
    Suffocation,
    /// Thrown about inside a crashing ship without the seatbelt on.
    Crash,
    /// Shot or punched by an enemy.
    Shot,
}

impl DeathCause {
    /// String-table key for the one-line death recap.
    pub fn text_key(self) -> &'static str {
        match self {
            DeathCause::Suffocation => "DEATH_SUFFOCATION",
            DeathCause::Crash => "DEATH_CRASH",
            DeathCause::Shot => "DEATH_SHOT",
        }
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct SuitState {
    pub health: f32,
    pub oxygen_s: f32,
    pub fuel_s: f32,
    pub tanks: u32,
    pub dead: Option<DeathCause>,
    since_thrust_s: f32,
}

impl SuitState {
    pub fn new(data: &SuitData) -> Self {
        Self {
            health: data.health_max,
            oxygen_s: data.oxygen_capacity_s,
            fuel_s: data.jetpack.fuel_capacity_s,
            tanks: 0,
            dead: None,
            since_thrust_s: 0.0,
        }
    }

    pub fn oxygen_fraction(&self, data: &SuitData) -> f32 {
        (self.oxygen_s / data.oxygen_capacity_s).clamp(0.0, 1.0)
    }

    pub fn fuel_fraction(&self, data: &SuitData) -> f32 {
        (self.fuel_s / data.jetpack.fuel_capacity_s).clamp(0.0, 1.0)
    }

    pub fn low_oxygen(&self, data: &SuitData) -> bool {
        self.oxygen_fraction(data) <= data.low_oxygen_fraction
    }

    /// Take `amount` damage from `cause`; dies at zero health.
    pub fn hurt(&mut self, amount: f32, cause: DeathCause) {
        if self.dead.is_some() || amount <= 0.0 {
            return;
        }
        self.health = (self.health - amount).max(0.0);
        if self.health <= 0.0 {
            self.dead = Some(cause);
        }
    }

    /// How much of the requested thrust the fuel allows (0.0 to 1.0).
    pub fn available_thrust(&self, requested: f32) -> f32 {
        if self.fuel_s > 0.0 && self.dead.is_none() { requested.clamp(0.0, 1.0) } else { 0.0 }
    }

    /// Advance by `dt` seconds.
    pub fn step(&mut self, data: &SuitData, input: SuitInput, dt: f32) {
        if self.dead.is_some() {
            return;
        }
        // Oxygen.
        if input.at_supply_point {
            self.oxygen_s = (self.oxygen_s + data.oxygen_refill_per_s * dt).min(data.oxygen_capacity_s);
        } else if input.in_vacuum {
            self.oxygen_s = (self.oxygen_s - dt).max(0.0);
        }
        if self.oxygen_s <= 0.0 {
            self.health -= data.suffocation_damage_per_s * dt;
            if self.health <= 0.0 {
                self.health = 0.0;
                self.dead = Some(DeathCause::Suffocation);
            }
        }

        // Jetpack fuel.
        let thrust = self.available_thrust(input.thrust);
        if thrust > 0.0 {
            self.fuel_s = (self.fuel_s - thrust * dt).max(0.0);
            self.since_thrust_s = 0.0;
        } else {
            self.since_thrust_s += dt;
        }
        let jp = &data.jetpack;
        if input.at_supply_point {
            self.fuel_s = (self.fuel_s + jp.supply_refuel_per_s * dt).min(jp.fuel_capacity_s);
        } else if input.on_ground && self.since_thrust_s >= jp.ground_refuel_delay_s {
            self.fuel_s = (self.fuel_s + jp.ground_refuel_per_s * dt).min(jp.fuel_capacity_s);
        }
    }

    /// Pick up a tank. Returns false if already carrying the maximum.
    pub fn take_tank(&mut self, data: &SuitData) -> bool {
        if self.tanks >= data.tanks.max_carried || self.dead.is_some() {
            return false;
        }
        self.tanks += 1;
        true
    }

    /// Use a carried tank. Returns false if there is none, or the suit is
    /// already full (so a tank is never wasted).
    pub fn use_tank(&mut self, data: &SuitData) -> bool {
        if self.tanks == 0 || self.oxygen_s >= data.oxygen_capacity_s || self.dead.is_some() {
            return false;
        }
        self.tanks -= 1;
        self.oxygen_s = (self.oxygen_s + data.tanks.oxygen_per_tank_s).min(data.oxygen_capacity_s);
        // Fresh air also stops suffocation damage from continuing.
        true
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn hurt_kills_at_zero_with_its_cause() {
        let d: SuitData = crate::data::parse(include_str!("../../../../godot/data/player/suit.ron")).unwrap();
        let mut s = SuitState::new(&d);
        s.hurt(d.health_max * 0.4, DeathCause::Crash);
        assert!(s.dead.is_none());
        s.hurt(d.health_max, DeathCause::Crash);
        assert_eq!(s.dead, Some(DeathCause::Crash));
        assert_eq!(s.health, 0.0);
    }

    pub fn data() -> SuitData {
        crate::data::parse(include_str!("../../../../godot/data/player/suit.ron")).unwrap()
    }

    fn vacuum() -> SuitInput {
        SuitInput { in_vacuum: true, ..Default::default() }
    }

    #[test]
    fn oxygen_drains_one_second_per_second_in_vacuum() {
        let d = data();
        let mut s = SuitState::new(&d);
        for _ in 0..600 {
            s.step(&d, vacuum(), 0.1);
        }
        assert!((s.oxygen_s - (d.oxygen_capacity_s - 60.0)).abs() < 0.01);
        assert_eq!(s.health, d.health_max);
    }

    #[test]
    fn warning_comes_before_any_damage() {
        let d = data();
        let mut s = SuitState::new(&d);
        let mut warned_at = None;
        let mut hurt_at = None;
        for i in 0..100_000 {
            s.step(&d, vacuum(), 0.1);
            if warned_at.is_none() && s.low_oxygen(&d) {
                warned_at = Some(i);
            }
            if hurt_at.is_none() && s.health < d.health_max {
                hurt_at = Some(i);
            }
            if s.dead.is_some() {
                break;
            }
        }
        let (w, h) = (warned_at.unwrap(), hurt_at.unwrap());
        // Fair failure: at least 30 seconds of warning before damage starts.
        assert!(h - w >= 300, "warned at tick {w}, hurt at tick {h}");
        assert_eq!(s.dead, Some(DeathCause::Suffocation));
    }

    #[test]
    fn supply_point_refills_air_and_fuel() {
        let d = data();
        let mut s = SuitState::new(&d);
        s.oxygen_s = 1.0;
        s.fuel_s = 0.0;
        for _ in 0..1000 {
            s.step(&d, SuitInput { in_vacuum: true, at_supply_point: true, ..Default::default() }, 0.1);
        }
        assert_eq!(s.oxygen_s, d.oxygen_capacity_s);
        assert_eq!(s.fuel_s, d.jetpack.fuel_capacity_s);
    }

    #[test]
    fn thrust_burns_fuel_and_stops_when_empty() {
        let d = data();
        let mut s = SuitState::new(&d);
        let input = SuitInput { thrust: 1.0, ..Default::default() };
        let steps = (d.jetpack.fuel_capacity_s / 0.1).ceil() as usize + 5;
        for _ in 0..steps {
            s.step(&d, input, 0.1);
        }
        assert_eq!(s.fuel_s, 0.0);
        assert_eq!(s.available_thrust(1.0), 0.0);
    }

    #[test]
    fn ground_refuel_waits_for_the_delay() {
        let d = data();
        let mut s = SuitState::new(&d);
        s.fuel_s = 0.0;
        let ground = SuitInput { on_ground: true, ..Default::default() };
        let before_delay = ((d.jetpack.ground_refuel_delay_s / 0.1) as usize).saturating_sub(1);
        for _ in 0..before_delay {
            s.step(&d, ground, 0.1);
        }
        assert_eq!(s.fuel_s, 0.0);
        for _ in 0..50 {
            s.step(&d, ground, 0.1);
        }
        assert!(s.fuel_s > 0.0);
    }

    #[test]
    fn tanks_are_capped_and_never_wasted() {
        let d = data();
        let mut s = SuitState::new(&d);
        for _ in 0..d.tanks.max_carried {
            assert!(s.take_tank(&d));
        }
        assert!(!s.take_tank(&d));
        assert!(!s.use_tank(&d), "full suit: keep the tank");
        s.oxygen_s = 0.0;
        assert!(s.use_tank(&d));
        assert_eq!(s.oxygen_s, d.tanks.oxygen_per_tank_s.min(d.oxygen_capacity_s));
        assert_eq!(s.tanks, d.tanks.max_carried - 1);
    }

    #[test]
    fn breathable_air_does_not_drain() {
        let d = data();
        let mut s = SuitState::new(&d);
        s.step(&d, SuitInput::default(), 10.0);
        assert_eq!(s.oxygen_s, d.oxygen_capacity_s);
    }
}
