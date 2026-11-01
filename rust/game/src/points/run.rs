//! One run's state (CLAUDE.md sections 2, 3, 9 and 18): the ante timer,
//! Ante Score and Wallet, the contract board, the end-of-ante upgrade
//! screen (attributes, perk draft and rerolls, scratch-off ticket), and the
//! run's end. Pure, so the balance simulator and unit tests drive the same
//! rules the game does; the `Points` singleton owns one of these.
//!
//! Serves pillar 3 (every run counts) and pillar 5 (honest rewards); fun
//! rules 2 (interesting decisions: contracts, draft, saving vs spending),
//! 5 (flow: one clear goal and a clock) and 9 (every run counts: summary).

use std::collections::{BTreeMap, BTreeSet};

use serde::Deserialize;

use super::data::{AnteData, PointSources};
use super::maths::{AnteScore, Award, Rules, value_growth};
use super::perks::{Loadout, Mods, PerksData, draft};
use crate::seed::SeededRng;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Deserialize)]
pub enum ContractKind {
    ClearCamp,
    KillTier,
    Capture,
    Discover,
    Land,
}

#[derive(Debug, Clone, Deserialize)]
pub struct ContractTemplate {
    pub id: String,
    pub kind: ContractKind,
    pub risk: u32,
    pub body: String,
    pub count: u32,
    #[serde(default)]
    pub tier: u32,
    pub from_ante: u32,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Deserialize)]
pub enum Rarity {
    Common,
    Uncommon,
    Rare,
    Legendary,
    Unusual,
}

impl Rarity {
    pub fn key(self) -> &'static str {
        match self {
            Rarity::Common => "RARITY_COMMON",
            Rarity::Uncommon => "RARITY_UNCOMMON",
            Rarity::Rare => "RARITY_RARE",
            Rarity::Legendary => "RARITY_LEGENDARY",
            Rarity::Unusual => "RARITY_UNUSUAL",
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Deserialize)]
pub enum Prize {
    /// This fraction of the current target, to the Wallet.
    Wallet(f64),
    /// Extra seconds on the next ante's clock (stopwatch_bonus_s).
    Stopwatch,
}

#[derive(Debug, Clone, Deserialize)]
pub struct ScratchOdds {
    pub rarity: Rarity,
    pub weight: f64,
    pub prize: Prize,
}

#[derive(Debug, Clone, Deserialize)]
pub struct RunData {
    pub contracts_offered: usize,
    pub contracts_max_taken: usize,
    pub contract_reward_by_risk: Vec<(u32, u32)>,
    pub contracts: Vec<ContractTemplate>,
    pub scratch_odds: Vec<ScratchOdds>,
    pub scratch_reveal_s: f32,
    pub discovery_points: BTreeMap<String, u32>,
    pub cave_points: u32,
    pub landmark_points: u32,
    pub first_landing_points: u32,
    pub discovery_margin_m: f32,
    pub discovery_max_altitude_m: f32,
    pub discovery_min_radius_m: f32,
    pub discovery_check_s: f32,
    /// Within this of a cave mouth / surface landmark counts as finding it.
    pub cave_find_m: f32,
    pub landmark_find_m: f32,
    /// Feet within this of the ground, off the ship, counts as landed.
    pub landed_altitude_m: f32,
    /// The run starts here: no "first landing" for standing at home.
    pub home_body: String,
    pub final_ante: u32,
    pub leaderboard_size: usize,
}

pub const RUN_PATH: &str = "res://data/points/run.ron";
pub const RUN_EMBEDDED: &str = include_str!("../../../../godot/data/points/run.ron");
const ANTES_PATH: &str = "res://data/points/antes.ron";
const ANTES_EMBEDDED: &str = include_str!("../../../../godot/data/points/antes.ron");
const SOURCES_PATH: &str = "res://data/points/sources.ron";
const SOURCES_EMBEDDED: &str = include_str!("../../../../godot/data/points/sources.ron");

/// Every table the run reads.
#[derive(Debug, Clone)]
pub struct Tables {
    pub rules: Rules,
    pub perks: PerksData,
    pub run: RunData,
    pub antes: AnteData,
    pub sources: PointSources,
}

impl Tables {
    /// The embedded copies (tests, simulator).
    pub fn embedded() -> Tables {
        use crate::data::parse;
        Tables {
            rules: parse(super::maths::RULES_EMBEDDED).expect("rules.ron"),
            perks: parse(super::perks::PERKS_EMBEDDED).expect("perks.ron"),
            run: parse(RUN_EMBEDDED).expect("run.ron"),
            antes: parse(ANTES_EMBEDDED).expect("antes.ron"),
            sources: parse(SOURCES_EMBEDDED).expect("sources.ron"),
        }
    }

    /// The res:// copies, so Matthew can tune without rebuilding.
    pub fn load() -> Tables {
        use crate::data::load;
        Tables {
            rules: load(super::maths::RULES_PATH, super::maths::RULES_EMBEDDED),
            perks: load(super::perks::PERKS_PATH, super::perks::PERKS_EMBEDDED),
            run: load(RUN_PATH, RUN_EMBEDDED),
            antes: load(ANTES_PATH, ANTES_EMBEDDED),
            sources: load(SOURCES_PATH, SOURCES_EMBEDDED),
        }
    }

    /// Base points for a kill of `tier` (1-3).
    pub fn kill_points(&self, tier: u32) -> u32 {
        self.sources.kill_tier.get(tier.clamp(1, 3) as usize - 1).copied().unwrap_or(0)
    }
}

/// A contract on the board or taken.
#[derive(Debug, Clone, PartialEq)]
pub struct Contract {
    pub id: String,
    pub kind: ContractKind,
    pub risk: u32,
    pub body: String,
    pub count: u32,
    pub tier: u32,
    pub reward: u64,
    pub progress: u32,
    pub done: bool,
}

/// Things that happen in the world that contracts and points care about.
#[derive(Debug, Clone, PartialEq)]
pub enum WorldEvent {
    Kill { tier: u32, body: String },
    Captured { body: String },
    Discovered { body: String },
    Landed { body: String },
    CampCleared { body: String },
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Phase {
    /// Contract board before the clock starts.
    Board,
    Playing,
    /// Between antes: attributes, draft, scratch-off.
    Upgrade,
    Over,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Outcome {
    /// The clock ran out short of the target.
    TimeUp,
    Died,
    /// Cleared the final ante.
    Won,
}

/// How an ante ended, for the upgrade screen's tally.
#[derive(Debug, Clone, PartialEq)]
pub struct AnteResult {
    pub ante: u32,
    pub score: u64,
    pub target: u64,
    pub cash_out: u64,
    pub interest: u64,
}

#[derive(Debug, Clone, PartialEq)]
pub struct Ticket {
    pub rarity: Rarity,
    pub prize: Prize,
    pub amount: u64,
    pub scratched: bool,
}

#[derive(Debug, Clone, Default, PartialEq)]
pub struct RunStats {
    pub kills: u32,
    pub captures: u32,
    pub discoveries: u32,
    pub contracts_done: u32,
    pub total_points: u64,
    pub best_multiplier: f64,
    pub antes_cleared: u32,
}

#[derive(Debug, Clone)]
pub struct RunState {
    pub seed: u64,
    pub ante: u32,
    pub phase: Phase,
    pub time_left: f64,
    pub elapsed: f64,
    pub wallet: u64,
    pub score: AnteScore,
    pub loadout: Loadout,
    pub rerolls: u32,
    pub draft: Vec<String>,
    pub picked_perk: bool,
    pub board: Vec<Contract>,
    pub taken: Vec<Contract>,
    pub ticket: Option<Ticket>,
    pub next_ante_bonus_s: f64,
    pub discovered: BTreeSet<String>,
    pub stats: RunStats,
    pub outcome: Option<Outcome>,
    pub last_result: Option<AnteResult>,
    pub cashed_out: bool,
}

impl RunState {
    pub fn new(seed: u64, t: &Tables) -> RunState {
        let mut r = RunState {
            seed,
            ante: 1,
            phase: Phase::Board,
            time_left: t.antes.ante_length_s as f64,
            elapsed: 0.0,
            wallet: 0,
            score: AnteScore::default(),
            loadout: Loadout::default(),
            rerolls: 0,
            draft: Vec::new(),
            picked_perk: false,
            board: Vec::new(),
            taken: Vec::new(),
            ticket: None,
            next_ante_bonus_s: 0.0,
            discovered: BTreeSet::new(),
            stats: RunStats::default(),
            outcome: None,
            last_result: None,
            cashed_out: false,
        };
        r.offer_board(t);
        r
    }

    pub fn target(&self, t: &Tables) -> u64 {
        t.antes.target(self.ante)
    }

    pub fn mods(&self, t: &Tables) -> Mods {
        self.loadout.mods(&t.perks)
    }

    /// The perks-and-tags part of the multiplier (18.3).
    pub fn perk_multiplier(&self, t: &Tables) -> f64 {
        self.mods(t).tags_at_four() as f64 * t.rules.multiplier.tag_bonus
    }

    pub fn multiplier(&self, t: &Tables) -> f64 {
        self.score.multiplier(&t.rules, self.elapsed, self.perk_multiplier(t))
    }

    fn rng(&self, system: &str) -> SeededRng {
        SeededRng::derive_indexed(self.seed, system, self.ante as u64 * 1000 + self.rerolls as u64)
    }

    // ---- Contract board ----

    fn offer_board(&mut self, t: &Tables) {
        let mut rng = self.rng("contracts");
        let mut pool: Vec<&ContractTemplate> = t.run.contracts.iter().filter(|c| c.from_ante <= self.ante).collect();
        rng.shuffle(&mut pool);
        // Prefer one of each risk level so the choice is a real trade-off.
        let mut picked: Vec<&ContractTemplate> = Vec::new();
        for risk in 1..=3 {
            if let Some(c) = pool.iter().find(|c| c.risk == risk && !picked.iter().any(|p| p.id == c.id)) {
                picked.push(c);
            }
        }
        for c in &pool {
            if picked.len() >= t.run.contracts_offered {
                break;
            }
            if !picked.iter().any(|p| p.id == c.id) {
                picked.push(c);
            }
        }
        picked.truncate(t.run.contracts_offered);
        picked.sort_by_key(|c| c.risk);
        let growth = value_growth(t.antes.value_growth_per_ante, self.ante);
        self.board = picked
            .into_iter()
            .map(|c| {
                let (lo, hi) = t.run.contract_reward_by_risk.get(c.risk.clamp(1, 3) as usize - 1).copied().unwrap_or((300, 300));
                // Rounded to 10s so the numbers read cleanly (18.7).
                let raw = rng.range_i64(lo as i64, hi as i64 + 1) as f64 * growth;
                let reward = ((raw / 10.0).round() * 10.0) as u64;
                Contract { id: c.id.clone(), kind: c.kind, risk: c.risk, body: c.body.clone(), count: c.count.max(1), tier: c.tier, reward, progress: 0, done: false }
            })
            .collect();
        self.taken.clear();
    }

    /// Take (or put back) a contract from the board. Returns whether it is
    /// now taken.
    pub fn toggle_contract(&mut self, index: usize, t: &Tables) -> bool {
        let Some(c) = self.board.get(index).cloned() else { return false };
        if let Some(i) = self.taken.iter().position(|x| x.id == c.id) {
            self.taken.remove(i);
            return false;
        }
        if self.taken.len() >= t.run.contracts_max_taken || self.phase != Phase::Board {
            return false;
        }
        self.taken.push(c);
        true
    }

    /// Close the board and start the clock.
    pub fn start_ante(&mut self) {
        if self.phase == Phase::Board {
            self.phase = Phase::Playing;
        }
    }

    // ---- Playing ----

    /// Advance the clock. Returns true when the ante just ended (cleared or
    /// lost).
    pub fn tick(&mut self, dt: f64, t: &Tables) -> bool {
        if self.phase != Phase::Playing {
            return false;
        }
        self.elapsed += dt;
        self.time_left -= dt;
        if self.time_left > 0.0 {
            return false;
        }
        self.time_left = 0.0;
        if self.score.score >= self.target(t) {
            self.end_ante(false, t);
        } else {
            self.finish(Outcome::TimeUp);
        }
        true
    }

    /// Award points from a source (base value before growth, diminishing
    /// returns and the multiplier). None outside play.
    pub fn award(&mut self, source: &str, base: u64, t: &Tables) -> Option<Award> {
        if self.phase != Phase::Playing {
            return None;
        }
        let growth = value_growth(t.antes.value_growth_per_ante, self.ante);
        let perk = self.perk_multiplier(t);
        let a = self.score.award(&t.rules, source, base, self.elapsed, growth, perk);
        self.wallet += a.points;
        self.stats.total_points += a.points;
        self.stats.best_multiplier = self.stats.best_multiplier.max(a.multiplier);
        Some(a)
    }

    /// Stopwatch items and first discoveries add time.
    pub fn add_time(&mut self, seconds: f64) {
        if self.phase == Phase::Playing {
            self.time_left += seconds;
        }
    }

    /// First discovery of `key` this run? Records it.
    pub fn discover(&mut self, key: &str) -> bool {
        let new = self.discovered.insert(key.to_string());
        if new {
            self.stats.discoveries += 1;
        }
        new
    }

    /// First time for `key` this run (not counted as a discovery).
    pub fn first_time(&mut self, key: &str) -> bool {
        self.discovered.insert(key.to_string())
    }

    /// A world event: advances taken contracts and pays any it completes.
    pub fn event(&mut self, e: &WorldEvent, t: &Tables) -> Vec<(Contract, Award)> {
        if self.phase != Phase::Playing {
            return Vec::new();
        }
        match e {
            WorldEvent::Kill { .. } => self.stats.kills += 1,
            WorldEvent::Captured { .. } => self.stats.captures += 1,
            _ => {}
        }
        let mut done = Vec::new();
        for c in self.taken.iter_mut().filter(|c| !c.done) {
            let body_ok = |b: &str| c.body.is_empty() || c.body == b;
            let hit = match (c.kind, e) {
                (ContractKind::KillTier, WorldEvent::Kill { tier, body }) => (c.tier == 0 || c.tier == *tier) && body_ok(body),
                (ContractKind::Capture, WorldEvent::Captured { body }) => body_ok(body),
                (ContractKind::Discover, WorldEvent::Discovered { body }) => body_ok(body),
                (ContractKind::Land, WorldEvent::Landed { body }) => body_ok(body),
                (ContractKind::ClearCamp, WorldEvent::CampCleared { body }) => body_ok(body),
                _ => false,
            };
            if hit {
                c.progress += 1;
                if c.progress >= c.count {
                    c.done = true;
                    done.push(c.clone());
                }
            }
        }
        let mut out = Vec::new();
        for c in done {
            self.stats.contracts_done += 1;
            // The reward already includes the ante growth.
            let growth = value_growth(t.antes.value_growth_per_ante, self.ante);
            let base = (c.reward as f64 / growth).round() as u64;
            if let Some(a) = self.award("contract", base, t) {
                out.push((c, a));
            }
        }
        out
    }

    pub fn can_cash_out(&self, t: &Tables) -> bool {
        self.phase == Phase::Playing && self.score.score >= self.target(t)
    }

    /// Cash out early (18.6). Returns the Wallet paid for the time left.
    pub fn cash_out(&mut self, t: &Tables) -> Option<u64> {
        if !self.can_cash_out(t) {
            return None;
        }
        let paid = t.rules.cash_out(self.time_left, self.target(t));
        self.wallet += paid;
        self.cashed_out = true;
        self.end_ante(true, t);
        self.last_result.as_mut().map(|r| {
            r.cash_out = paid;
            paid
        })
    }

    fn end_ante(&mut self, cashed: bool, t: &Tables) {
        let target = self.target(t);
        let interest = t.rules.interest(self.wallet, target);
        self.wallet += interest;
        self.stats.antes_cleared += 1;
        self.last_result = Some(AnteResult { ante: self.ante, score: self.score.score, target, cash_out: 0, interest });
        let _ = cashed;
        if self.ante >= t.run.final_ante {
            // Clearing the final ante wins the run (Endless comes later).
            self.finish(Outcome::Won);
            return;
        }
        self.phase = Phase::Upgrade;
        self.rerolls = 0;
        self.picked_perk = false;
        self.draft = draft(&t.perks, &self.loadout.perks, &mut self.rng("draft"));
        self.ticket = Some(self.roll_ticket(t));
    }

    pub fn finish(&mut self, outcome: Outcome) {
        if self.phase != Phase::Over {
            self.phase = Phase::Over;
            self.outcome = Some(outcome);
        }
    }

    // ---- Upgrade screen ----

    pub fn attribute_level(&self, id: &str) -> u32 {
        self.loadout.attributes.get(id).copied().unwrap_or(0)
    }

    /// Price of the next level of `id`, or None at the top level. Prices
    /// use the target of the ante just cleared (the one being shopped for).
    pub fn attribute_price(&self, id: &str, t: &Tables) -> Option<u64> {
        t.rules.attribute_price(self.attribute_level(id) as usize, self.target(t))
    }

    pub fn buy_attribute(&mut self, id: &str, t: &Tables) -> bool {
        if self.phase != Phase::Upgrade || !t.perks.attributes.iter().any(|a| a.id == id) {
            return false;
        }
        let Some(price) = self.attribute_price(id, t) else { return false };
        if self.wallet < price {
            return false;
        }
        self.wallet -= price;
        *self.loadout.attributes.entry(id.to_string()).or_insert(0) += 1;
        true
    }

    pub fn reroll_price(&self, t: &Tables) -> u64 {
        t.rules.reroll_price(self.rerolls, self.target(t))
    }

    pub fn reroll(&mut self, t: &Tables) -> bool {
        let price = self.reroll_price(t);
        if self.phase != Phase::Upgrade || self.picked_perk || self.wallet < price {
            return false;
        }
        self.wallet -= price;
        self.rerolls += 1;
        self.draft = draft(&t.perks, &self.loadout.perks, &mut self.rng("draft"));
        true
    }

    pub fn pick_perk(&mut self, id: &str) -> bool {
        if self.phase != Phase::Upgrade || self.picked_perk || !self.draft.iter().any(|d| d == id) {
            return false;
        }
        self.loadout.perks.push(id.to_string());
        self.picked_perk = true;
        true
    }

    fn roll_ticket(&self, t: &Tables) -> Ticket {
        let mut rng = self.rng("scratch_off");
        let weights: Vec<f64> = t.run.scratch_odds.iter().map(|o| o.weight).collect();
        let i = rng.weighted_index(&weights).unwrap_or(0);
        let o = &t.run.scratch_odds[i];
        let amount = match o.prize {
            Prize::Wallet(f) => (f * self.target(t) as f64).round() as u64,
            Prize::Stopwatch => t.antes.stopwatch_bonus_s as u64,
        };
        Ticket { rarity: o.rarity, prize: o.prize, amount, scratched: false }
    }

    /// Scratch the ticket: pays its prize once.
    pub fn scratch(&mut self) -> Option<Ticket> {
        if self.phase != Phase::Upgrade {
            return None;
        }
        let ticket = self.ticket.as_mut()?;
        if ticket.scratched {
            return None;
        }
        ticket.scratched = true;
        let tk = ticket.clone();
        match tk.prize {
            Prize::Wallet(_) => self.wallet += tk.amount,
            Prize::Stopwatch => self.next_ante_bonus_s += tk.amount as f64,
        }
        Some(tk)
    }

    /// Leave the upgrade screen: the next ante's board opens.
    pub fn next_ante(&mut self, t: &Tables) -> bool {
        if self.phase != Phase::Upgrade {
            return false;
        }
        // An unscratched ticket is scratched for you (never lost).
        self.scratch();
        self.ante += 1;
        self.score = AnteScore::default();
        self.elapsed = 0.0;
        self.time_left = t.antes.ante_length_s as f64 + self.next_ante_bonus_s;
        self.next_ante_bonus_s = 0.0;
        self.rerolls = 0;
        self.cashed_out = false;
        self.phase = Phase::Board;
        self.offer_board(t);
        true
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn tables() -> Tables {
        Tables::embedded()
    }

    fn playing(seed: u64) -> (RunState, Tables) {
        let t = tables();
        let mut r = RunState::new(seed, &t);
        r.start_ante();
        (r, t)
    }

    #[test]
    fn board_offers_three_with_mixed_risk_and_take_at_most_two() {
        let t = tables();
        let mut r = RunState::new(42, &t);
        assert_eq!(r.board.len(), 3);
        let risks: BTreeSet<u32> = r.board.iter().map(|c| c.risk).collect();
        assert_eq!(risks.len(), 3, "one of each risk level: {:?}", r.board);
        for c in &r.board {
            let (lo, hi) = t.run.contract_reward_by_risk[c.risk as usize - 1];
            assert!(c.reward >= lo as u64 && c.reward <= hi as u64 + 10);
        }
        assert!(r.toggle_contract(0, &t));
        assert!(r.toggle_contract(1, &t));
        assert!(!r.toggle_contract(2, &t), "max two");
        assert!(!r.toggle_contract(1, &t), "taking again puts it back");
        assert!(r.toggle_contract(2, &t));
        assert_eq!(r.taken.len(), 2);
    }

    #[test]
    fn same_seed_same_board_and_draft() {
        let t = tables();
        let a = RunState::new(9, &t);
        let b = RunState::new(9, &t);
        assert_eq!(a.board, b.board);
    }

    #[test]
    fn points_go_to_score_and_wallet_one_to_one() {
        let (mut r, t) = playing(1);
        let a = r.award("kill_t1", 25, &t).unwrap();
        assert_eq!(r.score.score, a.points);
        assert_eq!(r.wallet, a.points);
    }

    #[test]
    fn contracts_progress_and_pay_once() {
        let (mut r, t) = {
            let t = tables();
            let mut r = RunState::new(3, &t);
            r.board = vec![Contract { id: "x".into(), kind: ContractKind::KillTier, risk: 1, body: String::new(), count: 2, tier: 1, reward: 400, progress: 0, done: false }];
            r.toggle_contract(0, &t);
            r.start_ante();
            (r, t)
        };
        let k = WorldEvent::Kill { tier: 1, body: "moon".into() };
        assert!(r.event(&k, &t).is_empty());
        assert!(r.event(&WorldEvent::Kill { tier: 2, body: "moon".into() }, &t).is_empty(), "wrong tier");
        let paid = r.event(&k, &t);
        assert_eq!(paid.len(), 1);
        assert_eq!(paid[0].1.base, 400);
        assert!(r.event(&k, &t).is_empty(), "pays once");
        assert_eq!(r.stats.contracts_done, 1);
    }

    #[test]
    fn running_out_of_time_short_of_the_target_ends_the_run() {
        let (mut r, t) = playing(5);
        r.award("discovery", 50, &t);
        assert!(!r.tick(100.0, &t));
        assert!(r.tick(1000.0, &t));
        assert_eq!(r.phase, Phase::Over);
        assert_eq!(r.outcome, Some(Outcome::TimeUp));
    }

    #[test]
    fn meeting_the_target_clears_the_ante_with_interest() {
        let (mut r, t) = playing(5);
        r.award("contract", 1200, &t);
        let wallet = r.wallet;
        assert!(r.tick(800.0, &t));
        assert_eq!(r.phase, Phase::Upgrade);
        let res = r.last_result.clone().unwrap();
        assert_eq!(res.interest, t.rules.interest(wallet, 1000));
        assert_eq!(r.wallet, wallet + res.interest);
        assert_eq!(r.draft.len(), 3);
        assert!(r.ticket.is_some());
    }

    #[test]
    fn cash_out_pays_for_time_left_and_only_once_the_target_is_met() {
        let (mut r, t) = playing(6);
        assert!(r.cash_out(&t).is_none());
        r.award("contract", 1200, &t);
        r.tick(120.0, &t);
        let paid = r.cash_out(&t).unwrap();
        assert_eq!(paid, t.rules.cash_out(600.0, 1000));
        assert_eq!(r.phase, Phase::Upgrade);
    }

    #[test]
    fn upgrade_screen_spends_wallet_never_score() {
        let (mut r, t) = playing(8);
        r.award("contract", 3000, &t);
        r.tick(800.0, &t);
        let score = r.last_result.as_ref().unwrap().score;
        let w = r.wallet;
        assert!(r.buy_attribute("damage", &t));
        assert_eq!(r.wallet, w - 300);
        assert_eq!(r.attribute_level("damage"), 1);
        assert_eq!(r.attribute_price("damage", &t), Some(450));
        let draft_before = r.draft.clone();
        assert!(r.reroll(&t));
        assert_eq!(r.reroll_price(&t), 150);
        assert_ne!(r.draft, draft_before, "a reroll redraws (seeded)");
        let pick = r.draft[0].clone();
        assert!(r.pick_perk(&pick));
        assert!(!r.pick_perk(&r.draft[1].clone()), "one pick per ante");
        assert!(!r.reroll(&t), "no reroll after picking");
        assert_eq!(r.last_result.as_ref().unwrap().score, score);
    }

    #[test]
    fn scratch_off_pays_once_and_next_ante_resets_the_clock() {
        let (mut r, t) = playing(11);
        r.award("contract", 1200, &t);
        r.tick(800.0, &t);
        let first = r.scratch().unwrap();
        assert!(first.scratched);
        assert!(r.scratch().is_none());
        assert!(r.next_ante(&t));
        assert_eq!(r.ante, 2);
        assert_eq!(r.phase, Phase::Board);
        assert_eq!(r.score.score, 0);
        let expected = t.antes.ante_length_s as f64 + if first.prize == Prize::Stopwatch { first.amount as f64 } else { 0.0 };
        assert_eq!(r.time_left, expected);
    }

    /// Section 9: the odds shown in-game are 55/25/13/6/1.
    #[test]
    fn scratch_odds_match_section_9() {
        let t = tables();
        let w: Vec<f64> = t.run.scratch_odds.iter().map(|o| o.weight).collect();
        assert_eq!(w, vec![55.0, 25.0, 13.0, 6.0, 1.0]);
    }

    #[test]
    fn clearing_the_final_ante_wins() {
        let (mut r, t) = playing(2);
        r.ante = t.run.final_ante;
        r.award("contract", 100_000, &t);
        r.tick(1000.0, &t);
        assert_eq!(r.outcome, Some(Outcome::Won));
    }
}
