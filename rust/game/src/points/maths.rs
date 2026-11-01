//! The points maths of CLAUDE.md section 18, with no engine types so it can
//! be unit-tested and driven by the balance simulator.
//!
//! - Ante Score = base x multiplier (18.3); every point also goes to the
//!   Wallet, 1:1 (18.5). Spending never lowers the Ante Score.
//! - Multiplier = variety + chain + perks, capped (18.3).
//! - The k-th repeat of a source in one ante pays base x 0.85^k, floor 40%
//!   (18.4); one-off sources are exempt.
//! - Prices, interest and cash-out are indexed to the ante target T (18.5,
//!   18.6), so the economy never inflates.
//! - The pace meter projects the last two minutes' rate forward (18.7).
//!
//! Serves pillar 3 (every run counts and teaches) and pillar 5 (honest
//! rewards: every rule is visible); fun rule 2 (interesting decisions: the
//! multiplier rewards switching activities, interest rewards saving) and
//! fun rule 4 (curiosity: a Discovery adds a whole category to the
//! multiplier, so exploring matters mathematically without big payouts).

use std::collections::{BTreeMap, BTreeSet, HashMap};

use serde::Deserialize;

/// Point categories for the variety multiplier (18.3).
#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Hash, Deserialize)]
pub enum Category {
    Combat,
    Discovery,
    Repair,
    Salvage,
    Bounty,
    Contract,
    Stunt,
}

impl Category {
    pub fn key(self) -> &'static str {
        match self {
            Category::Combat => "CATEGORY_COMBAT",
            Category::Discovery => "CATEGORY_DISCOVERY",
            Category::Repair => "CATEGORY_REPAIR",
            Category::Salvage => "CATEGORY_SALVAGE",
            Category::Bounty => "CATEGORY_BOUNTY",
            Category::Contract => "CATEGORY_CONTRACT",
            Category::Stunt => "CATEGORY_STUNT",
        }
    }
}

#[derive(Debug, Clone, Deserialize)]
pub struct MultiplierRules {
    pub variety_step: f64,
    pub variety_max: f64,
    pub chain_window_s: f64,
    pub chain_per_link: f64,
    pub chain_max: f64,
    pub tag_bonus: f64,
    pub perks_max: f64,
    pub cap: f64,
}

#[derive(Debug, Clone, Deserialize)]
pub struct Diminishing {
    pub factor: f64,
    pub floor: f64,
}

#[derive(Debug, Clone, Deserialize)]
pub struct Interest {
    pub rate: f64,
    pub cap_t: f64,
}

#[derive(Debug, Clone, Deserialize)]
pub struct CashOut {
    pub per_second_t: f64,
    pub cap_t: f64,
    pub hold_s: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct Prices {
    pub attribute_t: Vec<f64>,
    pub reroll_t: f64,
    pub reroll_step_t: f64,
    pub emergency_lander_wallet: f64,
    pub casino_min_bet_t: f64,
}

#[derive(Debug, Clone, Deserialize)]
pub struct PaceRules {
    pub window_s: f64,
    pub ahead_above: f64,
    pub behind_below: f64,
    pub min_elapsed_s: f64,
}

#[derive(Debug, Clone, Deserialize)]
pub struct SourceRule {
    pub category: Category,
    pub grows: bool,
    pub one_off: bool,
}

#[derive(Debug, Clone, Deserialize)]
pub struct Rules {
    pub multiplier: MultiplierRules,
    pub diminishing: Diminishing,
    pub interest: Interest,
    pub cash_out: CashOut,
    pub prices: Prices,
    pub pace: PaceRules,
    pub sources: BTreeMap<String, SourceRule>,
}

pub const RULES_PATH: &str = "res://data/points/rules.ron";
pub const RULES_EMBEDDED: &str = include_str!("../../../../godot/data/points/rules.ron");

impl Rules {
    pub fn embedded() -> Rules {
        crate::data::parse(RULES_EMBEDDED).expect("rules.ron parses")
    }

    /// Price of the next attribute level (`level` = levels already owned).
    pub fn attribute_price(&self, level: usize, target: u64) -> Option<u64> {
        self.prices.attribute_t.get(level).map(|f| (f * target as f64).round() as u64)
    }

    /// Price of the next draft reroll this ante.
    pub fn reroll_price(&self, rerolls_done: u32, target: u64) -> u64 {
        ((self.prices.reroll_t + self.prices.reroll_step_t * rerolls_done as f64) * target as f64).round() as u64
    }

    /// Interest on unspent Wallet at ante end.
    pub fn interest(&self, wallet: u64, target: u64) -> u64 {
        let raw = wallet as f64 * self.interest.rate;
        raw.min(self.interest.cap_t * target as f64).floor().max(0.0) as u64
    }

    /// Wallet paid for cashing out with `seconds_left` on the clock.
    pub fn cash_out(&self, seconds_left: f64, target: u64) -> u64 {
        let t = target as f64;
        (seconds_left.max(0.0) * self.cash_out.per_second_t * t).min(self.cash_out.cap_t * t).floor() as u64
    }

    pub fn emergency_lander_price(&self, wallet: u64) -> u64 {
        (wallet as f64 * self.prices.emergency_lander_wallet).ceil() as u64
    }

    /// Diminishing returns: the k-th repeat (0 = first) pays this fraction.
    pub fn repeat_factor(&self, k: u32) -> f64 {
        self.diminishing.factor.powi(k as i32).max(self.diminishing.floor)
    }
}

/// One award, as shown in the count-up (base x multiplier = points).
#[derive(Debug, Clone, PartialEq)]
pub struct Award {
    pub source: String,
    pub category: Category,
    pub base: u64,
    pub multiplier: f64,
    pub points: u64,
    /// 0 for the first of its source this ante, 1 for the next...
    pub repeat: u32,
    /// True when this award brought a new category in.
    pub new_category: bool,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Pace {
    Ahead,
    OnPace,
    Behind,
}

/// One ante's score: Ante Score, the multiplier's state and the rate
/// history for the pace meter. The Wallet lives with the run.
#[derive(Debug, Clone, Default)]
pub struct AnteScore {
    pub score: u64,
    pub categories: BTreeSet<Category>,
    pub repeats: HashMap<String, u32>,
    pub chain_links: u32,
    pub last_action_s: Option<f64>,
    /// (time, points) of every award, for the pace meter.
    pub history: Vec<(f64, u64)>,
}

impl AnteScore {
    /// Variety part: 1 + step x (distinct - 1), capped.
    pub fn variety(&self, rules: &Rules) -> f64 {
        let d = self.categories.len().max(1) as f64;
        (1.0 + rules.multiplier.variety_step * (d - 1.0)).min(rules.multiplier.variety_max)
    }

    /// Chain part at time `now`: links only count while the chain is alive.
    pub fn chain(&self, rules: &Rules, now: f64) -> f64 {
        match self.last_action_s {
            Some(t) if now - t <= rules.multiplier.chain_window_s => (self.chain_links as f64 * rules.multiplier.chain_per_link).min(rules.multiplier.chain_max),
            _ => 0.0,
        }
    }

    /// Total multiplier shown on the HUD ("x2.4").
    pub fn multiplier(&self, rules: &Rules, now: f64, perk_part: f64) -> f64 {
        (self.variety(rules) + self.chain(rules, now) + perk_part.clamp(0.0, rules.multiplier.perks_max)).min(rules.multiplier.cap)
    }

    /// Award `base` points from `source` at time `now` (seconds into the
    /// ante). `growth` is the per-ante value growth for growing sources
    /// (1.15^(ante-1)). Unknown sources count as Stunt, one-off.
    pub fn award(&mut self, rules: &Rules, source: &str, base: u64, now: f64, growth: f64, perk_part: f64) -> Award {
        let rule = rules.sources.get(source).cloned().unwrap_or(SourceRule { category: Category::Stunt, grows: false, one_off: true });
        // Chain: within the window of the last action adds a link; a gap
        // breaks it (never damage).
        match self.last_action_s {
            Some(t) if now - t <= rules.multiplier.chain_window_s => self.chain_links += 1,
            _ => self.chain_links = 0,
        }
        self.last_action_s = Some(now);
        let new_category = self.categories.insert(rule.category);
        let k = if rule.one_off {
            0
        } else {
            let e = self.repeats.entry(source.to_string()).or_insert(0);
            let k = *e;
            *e += 1;
            k
        };
        let grown = if rule.grows { base as f64 * growth } else { base as f64 };
        let base_after = (grown * rules.repeat_factor(k)).round() as u64;
        let multiplier = self.multiplier(rules, now, perk_part);
        let points = (base_after as f64 * multiplier).round() as u64;
        self.score += points;
        self.history.push((now, points));
        Award { source: source.to_string(), category: rule.category, base: base_after, multiplier, points, repeat: k, new_category }
    }

    /// Pace meter (18.7): current score plus the last window's rate times
    /// the time left, against the target. Before `min_elapsed_s` it reads
    /// On pace (too early to tell).
    pub fn pace(&self, rules: &Rules, now: f64, time_left: f64, target: u64) -> Pace {
        if self.score >= target {
            return Pace::Ahead;
        }
        if now < rules.pace.min_elapsed_s {
            return Pace::OnPace;
        }
        let window = rules.pace.window_s.min(now).max(1.0);
        let recent: u64 = self.history.iter().filter(|(t, _)| now - *t <= window).map(|(_, p)| *p).sum();
        let projected = self.score as f64 + recent as f64 / window * time_left.max(0.0);
        let ratio = projected / target.max(1) as f64;
        if ratio >= rules.pace.ahead_above {
            Pace::Ahead
        } else if ratio < rules.pace.behind_below {
            Pace::Behind
        } else {
            Pace::OnPace
        }
    }
}

/// Growth of growing sources at ante `n` (section 17.5: +15% per ante).
pub fn value_growth(per_ante: f64, ante: u32) -> f64 {
    (1.0 + per_ante).powi(ante.max(1) as i32 - 1)
}

/// "1,234", or compact above `compact_above` ("160k", "1.2M").
pub fn format_points(v: u64, compact_above: u64) -> String {
    if v > compact_above {
        if v >= 1_000_000 {
            let m = v as f64 / 1_000_000.0;
            return if m >= 100.0 { format!("{m:.0}M") } else { format!("{m:.1}M").replace(".0M", "M") };
        }
        return format!("{}k", (v as f64 / 1000.0).round() as u64);
    }
    let s = v.to_string();
    let mut out = String::new();
    for (i, c) in s.chars().enumerate() {
        if i > 0 && (s.len() - i) % 3 == 0 {
            out.push(',');
        }
        out.push(c);
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;

    fn rules() -> Rules {
        Rules::embedded()
    }

    #[test]
    fn variety_adds_point_two_per_new_category() {
        let r = rules();
        let mut a = AnteScore::default();
        let first = a.award(&r, "kill_t1", 25, 0.0, 1.0, 0.0);
        assert_eq!(first.multiplier, 1.0);
        let second = a.award(&r, "discovery", 50, 100.0, 1.0, 0.0);
        assert!(second.new_category);
        assert!((second.multiplier - 1.2).abs() < 1e-9, "{}", second.multiplier);
        assert_eq!(second.points, 60);
    }

    #[test]
    fn chain_builds_within_twenty_seconds_and_breaks_after() {
        let r = rules();
        let mut a = AnteScore::default();
        for i in 0..5 {
            a.award(&r, "discovery", 50, i as f64 * 10.0, 1.0, 0.0);
        }
        // Four links (the first action starts the chain).
        assert!((a.chain(&r, 40.0) - 0.2).abs() < 1e-9);
        assert_eq!(a.chain(&r, 61.0), 0.0, "chain breaks after 20 s idle");
        let after = a.award(&r, "discovery", 50, 100.0, 1.0, 0.0);
        assert!((after.multiplier - 1.0).abs() < 1e-9);
    }

    #[test]
    fn chain_and_total_are_capped() {
        let r = rules();
        let mut a = AnteScore::default();
        for (i, src) in ["kill_t1", "discovery", "contract"].iter().cycle().take(40).enumerate() {
            a.award(&r, src, 10, i as f64, 1.0, 0.0);
        }
        assert!(a.chain(&r, 39.0) <= r.multiplier.chain_max + 1e-9);
        assert!(a.multiplier(&r, 39.0, 5.0) <= r.multiplier.cap + 1e-9);
    }

    /// Section 18.4's example: the 6th tier-1 kill pays about 44%.
    #[test]
    fn diminishing_returns_match_the_worked_example() {
        let r = rules();
        assert!((r.repeat_factor(5) - 0.4437).abs() < 0.001);
        assert_eq!(r.repeat_factor(30), r.diminishing.floor);
        let mut a = AnteScore::default();
        let mut paid = Vec::new();
        for i in 0..6 {
            paid.push(a.award(&r, "kill_t1", 100, i as f64 * 100.0, 1.0, 0.0).base);
        }
        assert_eq!(paid, vec![100, 85, 72, 61, 52, 44]);
    }

    #[test]
    fn one_off_sources_are_exempt() {
        let r = rules();
        let mut a = AnteScore::default();
        let x = a.award(&r, "discovery", 60, 0.0, 1.0, 0.0);
        let y = a.award(&r, "discovery", 60, 100.0, 1.0, 0.0);
        assert_eq!(x.base, y.base);
    }

    #[test]
    fn growing_sources_rise_fifteen_percent_per_ante() {
        let r = rules();
        let g = value_growth(0.15, 3);
        assert!((g - 1.3225).abs() < 1e-9);
        let mut a = AnteScore::default();
        assert_eq!(a.award(&r, "kill_t2", 60, 0.0, g, 0.0).base, 79);
        assert_eq!(a.award(&r, "discovery", 60, 100.0, g, 0.0).base, 60);
    }

    /// Section 17.5 prices, indexed to T.
    #[test]
    fn prices_follow_the_target() {
        let r = rules();
        assert_eq!(r.attribute_price(0, 1000), Some(300));
        assert_eq!(r.attribute_price(4, 1000), Some(1000));
        assert_eq!(r.attribute_price(5, 1000), None);
        assert_eq!(r.reroll_price(0, 2800), 280);
        assert_eq!(r.reroll_price(2, 2800), 560);
        assert_eq!(r.emergency_lander_price(1001), 251);
    }

    #[test]
    fn interest_and_cash_out_are_capped() {
        let r = rules();
        assert_eq!(r.interest(500, 1000), 50);
        assert_eq!(r.interest(50_000, 1000), 100);
        assert_eq!(r.cash_out(100.0, 1000), 200);
        assert_eq!(r.cash_out(10_000.0, 1000), 600);
    }

    #[test]
    fn pace_meter_reads_the_last_two_minutes() {
        let r = rules();
        let mut a = AnteScore::default();
        assert_eq!(a.pace(&r, 10.0, 710.0, 1000), Pace::OnPace);
        // 100 points a minute for 2 minutes, 8 minutes left: ~1,000 projected.
        a.award(&r, "discovery", 100, 60.0, 1.0, 0.0);
        a.award(&r, "discovery", 100, 120.0, 1.0, 0.0);
        assert_eq!(a.pace(&r, 120.0, 480.0, 1000), Pace::OnPace);
        assert_eq!(a.pace(&r, 120.0, 900.0, 1000), Pace::Ahead);
        assert_eq!(a.pace(&r, 400.0, 100.0, 1000), Pace::Behind);
    }

    #[test]
    fn numbers_read_with_commas_and_compact_when_huge() {
        assert_eq!(format_points(0, 100_000), "0");
        assert_eq!(format_points(1234, 100_000), "1,234");
        assert_eq!(format_points(100_000, 100_000), "100,000");
        assert_eq!(format_points(160_400, 100_000), "160k");
        assert_eq!(format_points(1_200_000, 100_000), "1.2M");
    }
}
