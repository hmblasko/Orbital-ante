//! Shapes of res://data/points/*.ron.

use serde::Deserialize;

#[derive(Debug, Clone, Deserialize)]
pub struct AnteData {
    pub ante_length_s: f32,
    pub first_discovery_bonus_s: f32,
    pub stopwatch_bonus_s: f32,
    pub targets: Vec<u64>,
    pub endless_growth: f64,
    pub compact_display_above: u64,
    pub value_growth_per_ante: f64,
}

impl AnteData {
    /// Target T(n) for ante `n` (1-based), including Endless antes.
    pub fn target(&self, ante: u32) -> u64 {
        let n = ante.max(1) as usize;
        if let Some(t) = self.targets.get(n - 1) {
            return *t;
        }
        let last = *self.targets.last().unwrap_or(&0) as f64;
        let extra = (n - self.targets.len()) as i32;
        (last * self.endless_growth.powi(extra)).round() as u64
    }
}

#[derive(Debug, Clone, Copy, Deserialize)]
pub struct Range {
    pub min: u32,
    pub max: u32,
}

#[derive(Debug, Clone, Copy, Deserialize)]
pub struct BaseSizes {
    pub small: u32,
    pub medium: u32,
    pub large: u32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct PointSources {
    /// Tier 1, 2 and 3.
    pub kill_tier: Vec<u32>,
    pub limb_shot: u32,
    pub disarm: u32,
    pub base_clear: BaseSizes,
    pub all_bases_on_planet: u32,
    pub dish_on: u32,
    pub all_dishes_on_planet: u32,
    pub satellite_repaired: u32,
    pub discovery: Range,
    pub salvage_piece: Range,
    pub bounty_alive: Range,
    pub bounty_dead_fraction: f32,
    pub contract: Range,
    pub racetrack_lap: u32,
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::data::parse;

    fn antes() -> AnteData {
        parse(include_str!("../../../../godot/data/points/antes.ron")).unwrap()
    }

    #[test]
    fn sources_parse_and_ranges_are_ordered() {
        let s: PointSources = parse(include_str!("../../../../godot/data/points/sources.ron")).unwrap();
        for r in [s.discovery, s.salvage_piece, s.bounty_alive, s.contract] {
            assert!(r.min <= r.max);
        }
        assert_eq!(s.kill_tier.len(), 3);
        assert!(s.kill_tier[0] < s.kill_tier[1] && s.kill_tier[1] < s.kill_tier[2]);
    }

    /// Section 18.2: eight antes, rising, with growth ratios that shrink
    /// each ante (steep early, gentler late).
    #[test]
    fn target_curve_has_the_decided_shape() {
        let a = antes();
        assert_eq!(a.targets.len(), 8);
        let ratios: Vec<f64> = a.targets.windows(2).map(|w| w[1] as f64 / w[0] as f64).collect();
        assert!(ratios.iter().all(|r| *r > 1.0));
        assert!(ratios.windows(2).all(|w| w[1] < w[0]), "{ratios:?}");
    }

    #[test]
    fn endless_targets_grow_by_the_endless_factor() {
        let a = antes();
        assert_eq!(a.target(1), 1000);
        assert_eq!(a.target(8), 24000);
        assert_eq!(a.target(9), 38400);
    }
}
