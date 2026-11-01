//! Guns, melee, rope gun, limb damage, gibs and ragdolls (CLAUDE.md section 7).
//!
//! - `weapons`: weapon data and the pure firing maths (spread, falloff, ammo).
//! - `limbs`: the pure limb-damage model (disarms, leg drops, gibs, kills).
//! - `arsenal`: the player's weapons node (input, viewmodel, hits, feedback).
//! - `gibs`: flying limbs, ragdolls and ammo pickups, with the section 14 caps.
//! - `meshes`: procedural weapon models.

pub mod arsenal;
pub mod gibs;
pub mod limbs;
pub mod meshes;
pub mod weapons;

use serde::Deserialize;

use crate::ai::brain::AiData;
use limbs::{Limb, LimbRules};

/// Collision layer for enemy hitboxes (Area3D per limb). Planets use 1,
/// cabin 2, hull 4, player 8.
pub const HITBOX_LAYER: u32 = 16;
/// Collision layer for enemy bodies (they collide with the world only).
pub const ENEMY_LAYER: u32 = 32;
/// Gibs, ragdoll pieces and thrown rocks.
pub const PROP_LAYER: u32 = 64;

#[derive(Debug, Clone, Deserialize)]
pub struct LimbShape {
    pub limb: Limb,
    pub a: (f32, f32, f32),
    pub b: (f32, f32, f32),
    pub radius: f32,
    pub pivot: (f32, f32, f32),
}

#[derive(Debug, Clone, Deserialize)]
pub struct BodyModel {
    pub id: String,
    pub model: String,
    pub limbs: Vec<LimbShape>,
    pub grip: (f32, f32, f32),
    pub eye: (f32, f32, f32),
}

#[derive(Debug, Clone, Deserialize)]
pub struct FactionData {
    pub id: String,
    pub name_key: String,
    pub main: (f32, f32, f32),
    pub trim: (f32, f32, f32),
    pub dark: (f32, f32, f32),
    pub visor: (f32, f32, f32),
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Deserialize)]
pub enum FightStyle {
    /// Tier 1: charge in and spray.
    Rush,
    /// Tier 2: circle to your side at mid range, crouch while reloading.
    Flank,
}

#[derive(Debug, Clone, Deserialize)]
pub struct EnemyData {
    pub id: String,
    pub faction: String,
    pub tier: u32,
    pub body: String,
    pub scale: f32,
    pub health: f32,
    pub walk_speed: f32,
    pub run_speed: f32,
    pub weapon: String,
    pub burst: (u32, u32),
    pub burst_pause_s: (f32, f32),
    pub aim_error_deg: f32,
    pub aim_error_per_10m: f32,
    pub aim_error_per_mps: f32,
    pub damage_to_player: f32,
    pub style: FightStyle,
    pub preferred_range_m: (f32, f32),
}

#[derive(Debug, Clone, Deserialize)]
pub struct GibData {
    pub max_gibs: u32,
    pub gib_life_s: f32,
    pub fade_s: f32,
    pub max_ragdolls: u32,
    pub ragdoll_life_s: f32,
    pub gore: bool,
    pub pickup_radius_m: f32,
    pub pickup_life_s: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct CampData {
    pub body: String,
    /// First ante this camp is manned (camps are re-manned every ante).
    #[serde(default = "first_ante")]
    pub from_ante: u32,
    pub angle_deg: (f32, f32),
    pub spread_m: f32,
    pub enemies: Vec<String>,
    /// The base that stands around the camp (data/combat/bases.ron).
    #[serde(default)]
    pub base: Option<String>,
}

fn first_ante() -> u32 {
    1
}

#[derive(Debug, Clone, Deserialize)]
pub struct EnemiesData {
    pub bodies: Vec<BodyModel>,
    pub factions: Vec<FactionData>,
    pub enemies: Vec<EnemyData>,
    pub limbs: LimbRules,
    pub ai: AiData,
    pub gibs: GibData,
    pub camps: Vec<CampData>,
}

impl EnemiesData {
    pub fn enemy(&self, id: &str) -> Option<&EnemyData> {
        self.enemies.iter().find(|e| e.id == id)
    }
    pub fn body(&self, id: &str) -> Option<&BodyModel> {
        self.bodies.iter().find(|b| b.id == id)
    }
    pub fn faction(&self, id: &str) -> Option<&FactionData> {
        self.factions.iter().find(|f| f.id == id)
    }
}

pub const ENEMIES_PATH: &str = "res://data/combat/enemies.ron";
pub const ENEMIES_EMBEDDED: &str = include_str!("../../../../godot/data/combat/enemies.ron");

pub fn load_enemies() -> EnemiesData {
    crate::data::load(ENEMIES_PATH, ENEMIES_EMBEDDED)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn enemy_data_is_consistent() {
        let d = crate::data::parse::<EnemiesData>(ENEMIES_EMBEDDED).unwrap();
        let w = crate::data::parse::<weapons::WeaponsData>(weapons::DATA_EMBEDDED).unwrap();
        for e in &d.enemies {
            assert!(d.body(&e.body).is_some(), "{}: body", e.id);
            assert!(d.faction(&e.faction).is_some(), "{}: faction", e.id);
            assert!(w.get(&e.weapon).is_some(), "{}: weapon", e.id);
        }
        for c in &d.camps {
            for e in &c.enemies {
                assert!(d.enemy(e).is_some(), "camp enemy {e}");
            }
        }
    }
}
