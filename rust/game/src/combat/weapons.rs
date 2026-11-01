//! Weapon data and the pure maths behind firing: spread, falloff and ammo
//! (CLAUDE.md section 7: one gun + one melee + the rope gun; ammo is
//! limited and refills only from fallen enemies using the same gun).
//!
//! Serves pillar 2 (goofy but deadly) and fun rules 2 (interesting
//! decisions: ammo is scarce, so which gun you carry matters) and 10 (feel).

use godot::prelude::*;
use serde::Deserialize;

use crate::seed::SeededRng;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Deserialize)]
pub enum WeaponKind {
    /// Instant beams or bullets (hitscan), one or more pellets per shot.
    Gun,
    /// A thrown physical object (the rock).
    Thrown,
    /// A swing within arm's reach.
    Melee,
}

#[derive(Debug, Clone, Deserialize)]
pub struct WeaponData {
    pub id: String,
    pub name_key: String,
    pub kind: WeaponKind,
    pub damage: f32,
    pub pellets: u32,
    pub spread_deg: f32,
    pub aim_spread_deg: f32,
    pub moving_spread_deg: f32,
    pub fire_interval_s: f32,
    pub automatic: bool,
    pub magazine: u32,
    pub start_reserve: u32,
    pub max_reserve: u32,
    pub reload_s: f32,
    pub range_m: f32,
    pub falloff_start_m: f32,
    pub falloff_min: f32,
    pub impulse: f32,
    pub kick_deg: f32,
    pub hit_stop_s: f32,
    pub drop_ammo: (u32, u32),
    #[serde(default = "default_beam_color")]
    pub beam_color: (f32, f32, f32),
    #[serde(default)]
    pub beam_width: f32,
    #[serde(default)]
    pub beam_life_s: f32,
    #[serde(default)]
    pub muzzle_light: f32,
    #[serde(default)]
    pub throw_speed: f32,
    #[serde(default)]
    pub throw_up: f32,
    pub noise_m: f32,
}

fn default_beam_color() -> (f32, f32, f32) {
    (1.0, 1.0, 1.0)
}

#[derive(Debug, Clone, Deserialize)]
pub struct ShoveData {
    pub damage: f32,
    pub range_m: f32,
    pub impulse: f32,
    pub interval_s: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct WeaponsData {
    pub weapons: Vec<WeaponData>,
    pub start_gun: String,
    pub start_melee: Option<String>,
    pub shove: ShoveData,
    pub hit_marker_s: f32,
    pub hit_stop_time_scale: f32,
    pub camera_kick: bool,
    pub aim_fov: f32,
    pub aim_ease_s: f32,
    /// First-person weapon model: scale, and offsets from the camera at the
    /// hip and when aiming down the sights.
    pub view: ViewData,
    pub rope: RopeData,
}

/// The rope gun (section 9): a few shots per run that tie a target up so
/// it can be carried and delivered alive.
#[derive(Deserialize, Debug, Clone)]
pub struct RopeData {
    pub shots_per_run: u32,
    pub range_m: f32,
    pub color: (f32, f32, f32),
    pub width: f32,
    pub beam_life_s: f32,
    /// Pick up a tied target within this distance, looking roughly at it.
    pub pick_range_m: f32,
    pub pick_facing: f32,
    /// Where a carried target rides, from the camera: right, down, back
    /// (it lies along your view, under your arm).
    pub carry_offset: (f32, f32, f32),
    /// Push when you put it down (it drifts away in zero g).
    pub drop_push_mps: f32,
    pub noise_m: f32,
    /// Coils drawn on a tied target: (height, radius, tilt degrees).
    pub coils: Vec<(f32, f32, f32)>,
    pub coil_thickness: f32,
}

#[derive(Deserialize, Debug, Clone)]
pub struct ViewData {
    pub scale: f32,
    pub hip: (f32, f32, f32),
    pub aim: (f32, f32, f32),
    pub thrown: (f32, f32, f32),
    /// A soft light that only lights the held weapon, so it never turns
    /// into a black shape in a crater's shadow.
    pub fill_energy: f32,
    pub fill_range_m: f32,
}

impl WeaponsData {
    pub fn get(&self, id: &str) -> Option<&WeaponData> {
        self.weapons.iter().find(|w| w.id == id)
    }
}

pub const DATA_PATH: &str = "res://data/combat/weapons.ron";
pub const DATA_EMBEDDED: &str = include_str!("../../../../godot/data/combat/weapons.ron");

pub fn load_data() -> WeaponsData {
    crate::data::load(DATA_PATH, DATA_EMBEDDED)
}

/// Damage scale at `distance`: full up to falloff_start, then easing down
/// linearly to falloff_min at the weapon's range.
pub fn falloff(w: &WeaponData, distance: f32) -> f32 {
    if distance <= w.falloff_start_m || w.range_m <= w.falloff_start_m {
        return 1.0;
    }
    let t = ((distance - w.falloff_start_m) / (w.range_m - w.falloff_start_m)).clamp(0.0, 1.0);
    1.0 + (w.falloff_min - 1.0) * t
}

/// Cone half-angle for this shot: aiming tightens, moving loosens.
pub fn spread_deg(w: &WeaponData, aiming: bool, moving: bool) -> f32 {
    if moving && !aiming {
        w.moving_spread_deg
    } else if aiming {
        w.aim_spread_deg
    } else {
        w.spread_deg
    }
}

/// Pellet directions inside a cone of `half_angle_deg` around `forward`,
/// spread evenly over the cone's area (so a shotgun pattern isn't clumped
/// in the middle) from the weapon's seeded generator.
pub fn pellet_directions(forward: Vector3, half_angle_deg: f32, pellets: u32, rng: &mut SeededRng) -> Vec<Vector3> {
    let f = forward.normalized();
    let side = if f.y.abs() < 0.9 { Vector3::UP } else { Vector3::RIGHT };
    let right = f.cross(side).normalized();
    let up = right.cross(f).normalized();
    let max = half_angle_deg.to_radians().tan();
    (0..pellets.max(1))
        .map(|_| {
            let r = max * rng.next_f32().sqrt();
            let a = rng.next_f32() * std::f32::consts::TAU;
            (f + right * (r * a.cos()) + up * (r * a.sin())).normalized()
        })
        .collect()
}

/// A gun's ammunition: what's in the magazine and what's carried spare.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Ammo {
    pub loaded: u32,
    pub reserve: u32,
}

impl Ammo {
    pub fn full(w: &WeaponData) -> Self {
        Self { loaded: w.magazine, reserve: w.start_reserve }
    }

    /// Weapons without a magazine (rock, melee) never run out.
    pub fn infinite(w: &WeaponData) -> bool {
        w.magazine == 0
    }

    pub fn can_fire(&self, w: &WeaponData) -> bool {
        Self::infinite(w) || self.loaded > 0
    }

    pub fn spend(&mut self, w: &WeaponData) {
        if !Self::infinite(w) {
            self.loaded = self.loaded.saturating_sub(1);
        }
    }

    pub fn needs_reload(&self, w: &WeaponData) -> bool {
        !Self::infinite(w) && self.loaded < w.magazine && self.reserve > 0
    }

    pub fn reload(&mut self, w: &WeaponData) {
        let take = (w.magazine - self.loaded.min(w.magazine)).min(self.reserve);
        self.loaded += take;
        self.reserve -= take;
    }

    /// Pick up `amount` rounds; returns how many were taken (capped).
    pub fn add(&mut self, w: &WeaponData, amount: u32) -> u32 {
        let room = w.max_reserve.saturating_sub(self.reserve);
        let take = amount.min(room);
        self.reserve += take;
        take
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn data() -> WeaponsData {
        crate::data::parse::<WeaponsData>(DATA_EMBEDDED).unwrap()
    }

    #[test]
    fn the_slice_weapons_exist() {
        let d = data();
        for id in ["rifle", "laser_shotgun", "rock", "bat"] {
            assert!(d.get(id).is_some(), "{id} missing");
        }
        assert!(d.get(&d.start_gun).is_some());
    }

    #[test]
    fn falloff_eases_to_its_floor() {
        let d = data();
        let r = d.get("rifle").unwrap();
        assert_eq!(falloff(r, 10.0), 1.0);
        assert!((falloff(r, r.range_m) - r.falloff_min).abs() < 1e-5);
        assert!(falloff(r, 90.0) < 1.0 && falloff(r, 90.0) > r.falloff_min);
    }

    #[test]
    fn pellets_stay_inside_the_cone_and_are_reproducible() {
        let d = data();
        let s = d.get("laser_shotgun").unwrap();
        let mut a = SeededRng::derive(42, "weapon");
        let mut b = SeededRng::derive(42, "weapon");
        let pa = pellet_directions(Vector3::FORWARD, s.spread_deg, s.pellets, &mut a);
        let pb = pellet_directions(Vector3::FORWARD, s.spread_deg, s.pellets, &mut b);
        assert_eq!(pa, pb);
        assert_eq!(pa.len(), s.pellets as usize);
        for p in pa {
            assert!(p.angle_to(Vector3::FORWARD).to_degrees() <= s.spread_deg + 0.01);
        }
    }

    #[test]
    fn ammo_reloads_from_reserve_and_caps_pickups() {
        let d = data();
        let r = d.get("rifle").unwrap();
        let mut a = Ammo::full(r);
        for _ in 0..30 {
            assert!(a.can_fire(r));
            a.spend(r);
        }
        assert!(!a.can_fire(r));
        assert!(a.needs_reload(r));
        a.reload(r);
        assert_eq!(a, Ammo { loaded: 30, reserve: r.start_reserve - 30 });
        let took = a.add(r, 1000);
        assert_eq!(a.reserve, r.max_reserve);
        assert!(took < 1000);
    }

    #[test]
    fn the_rock_never_runs_out() {
        let d = data();
        let rock = d.get("rock").unwrap();
        let mut a = Ammo::full(rock);
        for _ in 0..100 {
            a.spend(rock);
        }
        assert!(a.can_fire(rock));
    }
}
