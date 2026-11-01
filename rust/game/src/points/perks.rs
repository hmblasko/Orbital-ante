//! Perks, synergy tags and attributes (CLAUDE.md sections 3, 17.4): what a
//! run's build adds up to, as named stat multipliers and rule flags that
//! other systems ask for. Pure; the `Points` singleton holds the loadout.
//!
//! Serves fun rule 2 (interesting decisions: every perk is a trade-off,
//! tags stack into bonuses) and fun rule 3 (autonomy: several viable builds).

use std::collections::{BTreeMap, BTreeSet};

use serde::Deserialize;

use crate::seed::SeededRng;

#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Hash, Deserialize)]
pub enum Tag {
    Salvage,
    Stealth,
    Heat,
    Speed,
    Diplomacy,
    Chaos,
    Scavenger,
}

impl Tag {
    pub fn key(self) -> &'static str {
        match self {
            Tag::Salvage => "TAG_SALVAGE",
            Tag::Stealth => "TAG_STEALTH",
            Tag::Heat => "TAG_HEAT",
            Tag::Speed => "TAG_SPEED",
            Tag::Diplomacy => "TAG_DIPLOMACY",
            Tag::Chaos => "TAG_CHAOS",
            Tag::Scavenger => "TAG_SCAVENGER",
        }
    }
}

#[derive(Debug, Clone, Default, Deserialize)]
pub struct Effect {
    #[serde(default)]
    pub mods: BTreeMap<String, f32>,
    #[serde(default)]
    pub flags: Vec<String>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct Perk {
    pub id: String,
    pub tags: Vec<Tag>,
    #[serde(default)]
    pub mods: BTreeMap<String, f32>,
    #[serde(default)]
    pub flags: Vec<String>,
    pub active: bool,
}

impl Perk {
    pub fn name_key(&self) -> String {
        format!("PERK_{}", self.id.to_uppercase())
    }

    pub fn desc_key(&self) -> String {
        format!("PERK_{}_DESC", self.id.to_uppercase())
    }
}

#[derive(Debug, Clone, Deserialize)]
pub struct TagBonus {
    pub two: Effect,
    pub four: Effect,
}

#[derive(Debug, Clone, Deserialize)]
pub struct Attribute {
    pub id: String,
    pub per_level: BTreeMap<String, f32>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct PerksData {
    pub draft_size: usize,
    pub perks: Vec<Perk>,
    pub tags: BTreeMap<Tag, TagBonus>,
    pub heat_low_health_below: f32,
    pub heat_low_health_damage: f32,
    pub heat_kill_heal_below: f32,
    pub heat_kill_heal_amount: f32,
    pub attributes: Vec<Attribute>,
}

pub const PERKS_PATH: &str = "res://data/points/perks.ron";
pub const PERKS_EMBEDDED: &str = include_str!("../../../../godot/data/points/perks.ron");

impl PerksData {
    pub fn embedded() -> PerksData {
        crate::data::parse(PERKS_EMBEDDED).expect("perks.ron parses")
    }

    pub fn perk(&self, id: &str) -> Option<&Perk> {
        self.perks.iter().find(|p| p.id == id)
    }
}

/// The run's build: perks picked and attribute levels bought.
#[derive(Debug, Clone, Default, PartialEq)]
pub struct Loadout {
    pub perks: Vec<String>,
    /// Attribute id -> levels owned (0-5).
    pub attributes: BTreeMap<String, u32>,
}

/// What a loadout adds up to.
#[derive(Debug, Clone, Default)]
pub struct Mods {
    pub stats: BTreeMap<String, f32>,
    pub flags: BTreeSet<String>,
    pub tag_counts: BTreeMap<Tag, u32>,
}

impl Mods {
    /// A stat multiplier (1.0 when nothing changes it).
    pub fn stat(&self, name: &str) -> f32 {
        self.stats.get(name).copied().unwrap_or(1.0)
    }

    pub fn has(&self, flag: &str) -> bool {
        self.flags.contains(flag)
    }

    /// Tags held at 4 or more.
    pub fn tags_at_four(&self) -> u32 {
        self.tag_counts.values().filter(|n| **n >= 4).count() as u32
    }

    fn apply(&mut self, effect_mods: &BTreeMap<String, f32>, flags: &[String]) {
        for (k, v) in effect_mods {
            *self.stats.entry(k.clone()).or_insert(1.0) *= v;
        }
        self.flags.extend(flags.iter().cloned());
    }
}

impl Loadout {
    pub fn mods(&self, data: &PerksData) -> Mods {
        let mut m = Mods::default();
        for id in &self.perks {
            let Some(p) = data.perk(id) else { continue };
            m.apply(&p.mods, &p.flags);
            for t in &p.tags {
                *m.tag_counts.entry(*t).or_insert(0) += 1;
            }
        }
        for (tag, n) in m.tag_counts.clone() {
            let Some(bonus) = data.tags.get(&tag) else { continue };
            if n >= 2 {
                m.apply(&bonus.two.mods, &bonus.two.flags);
            }
            if n >= 4 {
                m.apply(&bonus.four.mods, &bonus.four.flags);
            }
        }
        for a in &data.attributes {
            let level = self.attributes.get(&a.id).copied().unwrap_or(0);
            for (k, v) in &a.per_level {
                *m.stats.entry(k.clone()).or_insert(1.0) *= v.powi(level as i32);
            }
        }
        m
    }
}

/// Offer `data.draft_size` different perks the player doesn't hold, from
/// those that work in this build. Seeded, so a run's drafts reproduce.
pub fn draft(data: &PerksData, owned: &[String], rng: &mut SeededRng) -> Vec<String> {
    let mut pool: Vec<String> = data.perks.iter().filter(|p| p.active && !owned.contains(&p.id)).map(|p| p.id.clone()).collect();
    rng.shuffle(&mut pool);
    pool.truncate(data.draft_size);
    pool
}

#[cfg(test)]
mod tests {
    use super::*;

    fn data() -> PerksData {
        PerksData::embedded()
    }

    /// Section 17.4: fourteen perks, each with 1-2 tags, and a 2 and 4 bonus
    /// for every tag.
    #[test]
    fn perk_list_matches_section_17_4() {
        let d = data();
        assert_eq!(d.perks.len(), 14);
        assert!(d.perks.iter().all(|p| (1..=2).contains(&p.tags.len())));
        assert_eq!(d.tags.len(), 7);
        let ids: BTreeSet<&str> = d.perks.iter().map(|p| p.id.as_str()).collect();
        assert_eq!(ids.len(), 14, "perk ids are unique");
    }

    /// Perks are trade-offs: every perk with stat changes has at least one
    /// that helps and one that costs, or a rule flag that trades something.
    #[test]
    fn perks_are_trade_offs_not_flat_buffs() {
        let d = data();
        for p in &d.perks {
            let ups = p.mods.values().filter(|v| **v > 1.0).count();
            let downs = p.mods.values().filter(|v| **v < 1.0).count();
            let costs = p.mods.keys().filter(|k| k.contains("taken") || k.contains("use") || k.contains("noise") || k.contains("hostility") || k.contains("crash")).count();
            assert!(p.mods.len() >= 2 || !p.flags.is_empty(), "{} has a single plain effect", p.id);
            if p.mods.len() >= 2 {
                assert!(downs >= 1 || costs >= 1 || ups == 0, "{} looks like a flat buff", p.id);
            }
        }
    }

    #[test]
    fn mods_multiply_and_tags_stack() {
        let d = data();
        let l = Loadout { perks: vec!["glass_cannon".into(), "second_wind".into()], ..Default::default() };
        let m = l.mods(&d);
        assert!((m.stat("damage_dealt") - 1.3).abs() < 1e-6);
        assert_eq!(m.tag_counts.get(&Tag::Heat), Some(&2));
        assert!(m.has("second_wind"));
        assert!(m.has("heat_low_health_damage"), "Heat at 2 gives its bonus");
        assert!(!m.has("heat_kill_heal"));
        assert_eq!(m.tags_at_four(), 0);
    }

    #[test]
    fn attributes_compound_per_level() {
        let d = data();
        let mut l = Loadout::default();
        l.attributes.insert("damage".into(), 3);
        let m = l.mods(&d);
        assert!((m.stat("damage_dealt") - 1.08f32.powi(3)).abs() < 1e-5);
        assert_eq!(m.stat("nothing"), 1.0);
    }

    #[test]
    fn draft_offers_distinct_active_unowned_perks_reproducibly() {
        let d = data();
        let owned = vec!["ghost".to_string()];
        let a = draft(&d, &owned, &mut SeededRng::derive(7, "draft"));
        let b = draft(&d, &owned, &mut SeededRng::derive(7, "draft"));
        assert_eq!(a, b);
        assert_eq!(a.len(), d.draft_size);
        let set: BTreeSet<_> = a.iter().collect();
        assert_eq!(set.len(), a.len());
        for id in &a {
            assert!(d.perk(id).unwrap().active);
            assert!(!owned.contains(id));
        }
    }
}
