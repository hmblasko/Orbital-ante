//! Headless Monte Carlo balance simulator (CLAUDE.md section 18.8). It plays
//! thousands of runs per player profile through the real `RunState` rules,
//! with a simple model of how a player spends time (data/points/sim.ron),
//! and reports ante reached, time to target, multipliers, Wallet, and
//! perk and route shares among wins. Every number it produces is SIMULATED.
//!
//! Serves pillar 5 (honest rewards: balance is checked, not guessed) and
//! fun rule 2 (no dominant strategy: route and perk shares are measured).

use std::collections::BTreeMap;

use serde::Deserialize;

use super::perks::Tag;
use super::run::{Phase, Outcome, RunState, Tables};
use crate::seed::SeededRng;

#[derive(Debug, Clone, Deserialize)]
pub struct Activity {
    pub source: String,
    pub base: (u32, u32),
    pub minutes: f64,
    #[serde(default)]
    pub per_ante: f64,
    #[serde(default)]
    pub per_ante_growth: f64,
    #[serde(default)]
    pub per_run: u32,
    pub success: f64,
    #[serde(default)]
    pub limb_chance: f64,
    #[serde(default)]
    pub disarm_chance: f64,
    #[serde(default)]
    pub from_ante: u32,
    #[serde(default)]
    pub needs_kills: u32,
    #[serde(default)]
    pub adds_time_s: f64,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Deserialize)]
pub enum Route {
    Fighter,
    Explorer,
    Hunter,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Deserialize)]
pub enum PerkChoice {
    Random,
    Mixed,
    RouteMatch,
}

#[derive(Debug, Clone, Deserialize)]
pub struct Profile {
    pub name: String,
    pub efficiency: f64,
    pub growth_per_ante: f64,
    pub wasted_time: f64,
    pub hazard_per_min: f64,
    pub hazard_growth: f64,
    pub skill: f64,
    pub perk_choice: PerkChoice,
    pub keep_wallet_t: f64,
    pub cash_out: bool,
    pub take_contracts: usize,
}

#[derive(Debug, Clone, Deserialize)]
pub struct SimTargets {
    pub novice_median_death: (f64, f64),
    pub expert_win_rate: (f64, f64),
    pub max_share_in_expert_wins: f64,
    pub max_perk_pick_share: f64,
    pub average_ante1_minutes: (f64, f64),
}

#[derive(Debug, Clone, Deserialize)]
pub struct SimData {
    pub runs_per_profile: usize,
    pub step_s: f64,
    pub activities: BTreeMap<String, Activity>,
    pub contract_minutes: Vec<f64>,
    pub contract_success: Vec<f64>,
    pub routes: BTreeMap<Route, BTreeMap<String, f64>>,
    pub route_tags: BTreeMap<Route, Vec<Tag>>,
    pub perk_efficiency: f64,
    pub attribute_efficiency: f64,
    pub profiles: Vec<Profile>,
    pub targets: SimTargets,
}

pub const SIM_EMBEDDED: &str = include_str!("../../../../godot/data/points/sim.ron");

impl SimData {
    pub fn embedded() -> SimData {
        crate::data::parse(SIM_EMBEDDED).expect("sim.ron parses")
    }
}

/// One simulated run's record.
#[derive(Debug, Clone, Default)]
pub struct RunRecord {
    pub route: Option<Route>,
    /// Last ante played (the one you died or ran out of time in, or the
    /// final ante when won).
    pub ante_reached: u32,
    pub won: bool,
    pub died: bool,
    /// Minutes to reach the target, per ante that reached it.
    pub minutes_to_target: Vec<(u32, f64)>,
    /// Multiplier at the end of each ante.
    pub multipliers: Vec<(u32, f64)>,
    /// Wallet at the end of each ante (after interest).
    pub wallets: Vec<(u32, u64)>,
    pub perks: Vec<String>,
}

/// Every table the sim uses: the game's, with every perk draftable (the
/// sim models the whole design, not just what this build has wired).
pub fn sim_tables() -> Tables {
    let mut t = Tables::embedded();
    for p in &mut t.perks.perks {
        p.active = true;
    }
    t
}

struct Work {
    key: String,
    left_min: f64,
    contract: Option<usize>,
}

pub fn simulate_run(t: &Tables, s: &SimData, p: &Profile, route: Route, seed: u64) -> RunRecord {
    let mut rng = SeededRng::derive(seed, "balance_sim");
    let mut r = RunState::new(seed, t);
    let mut rec = RunRecord { route: Some(route), ..Default::default() };
    let mut used_per_run: BTreeMap<String, u32> = BTreeMap::new();
    let weights = s.routes.get(&route).cloned().unwrap_or_default();
    let tags = s.route_tags.get(&route).cloned().unwrap_or_default();
    loop {
        // ---- Board: take contracts by reward, up to the profile's habit.
        let mut order: Vec<usize> = (0..r.board.len()).collect();
        order.sort_by_key(|i| std::cmp::Reverse(r.board[*i].reward));
        if p.take_contracts == 1 {
            order.sort_by_key(|i| r.board[*i].risk);
        }
        for i in order.into_iter().take(p.take_contracts) {
            r.toggle_contract(i, t);
        }
        r.start_ante();
        let ante = r.ante;
        let target = r.target(t);
        let mut used_this_ante: BTreeMap<String, f64> = BTreeMap::new();
        let mut kills_this_ante = 0u32;
        let mut contract_left: Vec<bool> = r.taken.iter().map(|_| true).collect();
        let mut work: Option<Work> = None;
        let mut reached = false;
        let levels: u32 = r.loadout.attributes.values().sum();
        let matching = r.loadout.perks.iter().filter(|id| t.perks.perk(id).is_some_and(|pk| pk.tags.iter().any(|tg| tags.contains(tg)))).count();
        let eff = p.efficiency * (1.0 + p.growth_per_ante).powi(ante as i32 - 1) * (1.0 + s.attribute_efficiency * levels as f64) * (1.0 + s.perk_efficiency * matching as f64);
        let defense = r.attribute_level("defense") as f64;
        let hazard = p.hazard_per_min * p.hazard_growth.powi(ante as i32 - 1) / (1.0 + 0.08 * defense);
        while r.phase == Phase::Playing {
            let dt = s.step_s;
            // Death check.
            if rng.chance((hazard * dt / 60.0).min(1.0)) {
                r.finish(Outcome::Died);
                rec.died = true;
                break;
            }
            // Pick work if idle.
            if work.is_none() {
                let mut options: Vec<(String, f64, Option<usize>)> = Vec::new();
                for (i, left) in contract_left.iter().enumerate() {
                    if *left {
                        options.push((format!("contract{i}"), 2.5, Some(i)));
                    }
                }
                for (k, w) in &weights {
                    let Some(a) = s.activities.get(k) else { continue };
                    if a.from_ante > ante {
                        continue;
                    }
                    if a.per_run > 0 && used_per_run.get(k).copied().unwrap_or(0) >= a.per_run {
                        continue;
                    }
                    if a.per_run == 0 {
                        let supply = a.per_ante + a.per_ante_growth * (ante as f64 - 1.0);
                        if used_this_ante.get(k).copied().unwrap_or(0.0) + 1.0 > supply.floor().max(0.0) + 1e-9 {
                            continue;
                        }
                    }
                    if a.needs_kills > 0 && kills_this_ante < a.needs_kills * (used_this_ante.get(k).copied().unwrap_or(0.0) as u32 + 1) {
                        continue;
                    }
                    options.push((k.clone(), *w, None));
                }
                let ws: Vec<f64> = options.iter().map(|o| o.1).collect();
                if let Some(i) = rng.weighted_index(&ws) {
                    let (key, _, contract) = options[i].clone();
                    let minutes = match contract {
                        Some(ci) => s.contract_minutes.get(r.taken[ci].risk as usize - 1).copied().unwrap_or(4.0),
                        None => s.activities[&key].minutes,
                    };
                    let left = minutes / eff / (1.0 - p.wasted_time).max(0.05) * rng.range_f32(0.7, 1.3) as f64;
                    work = Some(Work { key, left_min: left, contract });
                }
            }
            // Do it.
            if let Some(w) = &mut work {
                w.left_min -= dt / 60.0;
                if w.left_min <= 0.0 {
                    let w = work.take().unwrap();
                    if let Some(ci) = w.contract {
                        contract_left[ci] = false;
                        let chance = s.contract_success.get(r.taken[ci].risk as usize - 1).copied().unwrap_or(0.5) * p.skill;
                        if rng.chance(chance.min(1.0)) {
                            let reward = r.taken[ci].reward;
                            let growth = super::maths::value_growth(t.antes.value_growth_per_ante, ante);
                            r.taken[ci].done = true;
                            r.stats.contracts_done += 1;
                            r.award("contract", (reward as f64 / growth).round() as u64, t);
                        }
                    } else {
                        let a = &s.activities[&w.key];
                        *used_this_ante.entry(w.key.clone()).or_insert(0.0) += 1.0;
                        if a.per_run > 0 {
                            *used_per_run.entry(w.key.clone()).or_insert(0) += 1;
                        }
                        if rng.chance((a.success * p.skill).min(1.0)) {
                            let base = rng.range_i64(a.base.0 as i64, a.base.1 as i64 + 1) as u64;
                            r.award(&a.source, base, t);
                            if a.source.starts_with("kill") {
                                kills_this_ante += 1;
                                if rng.chance(a.limb_chance) {
                                    r.award("limb_shot", t.sources.limb_shot as u64, t);
                                }
                                if rng.chance(a.disarm_chance) {
                                    r.award("disarm", t.sources.disarm as u64, t);
                                }
                            }
                            if a.adds_time_s > 0.0 {
                                r.add_time(a.adds_time_s);
                            }
                        }
                    }
                }
            }
            if !reached && r.score.score >= target {
                reached = true;
                rec.minutes_to_target.push((ante, r.elapsed / 60.0));
                if p.cash_out {
                    let mult = r.multiplier(t);
                    rec.multipliers.push((ante, mult));
                    r.cash_out(t);
                    break;
                }
            }
            let mult = r.multiplier(t);
            if r.tick(dt, t) {
                rec.multipliers.push((ante, mult));
            }
        }
        rec.ante_reached = ante;
        match r.phase {
            Phase::Over => {
                rec.won = r.outcome == Some(Outcome::Won);
                if rec.won {
                    rec.wallets.push((ante, r.wallet));
                }
                break;
            }
            Phase::Upgrade => {
                rec.wallets.push((ante, r.wallet));
                r.scratch();
                // Perk.
                let pick = match p.perk_choice {
                    PerkChoice::Random => rng.pick(&r.draft).cloned(),
                    PerkChoice::Mixed if rng.chance(0.5) => rng.pick(&r.draft).cloned(),
                    _ => r
                        .draft
                        .iter()
                        .find(|id| t.perks.perk(id).is_some_and(|pk| pk.tags.iter().any(|tg| tags.contains(tg))))
                        .cloned()
                        .or_else(|| r.draft.first().cloned()),
                };
                if let Some(id) = pick {
                    r.pick_perk(&id);
                }
                // Attributes, keeping a reserve.
                let reserve = (p.keep_wallet_t * r.target(t) as f64) as u64;
                let attrs: Vec<String> = t.perks.attributes.iter().map(|a| a.id.clone()).collect();
                loop {
                    let cheapest = attrs.iter().filter_map(|a| r.attribute_price(a, t).map(|pr| (pr, a.clone()))).min();
                    match cheapest {
                        Some((price, id)) if r.wallet >= price + reserve => {
                            r.buy_attribute(&id, t);
                        }
                        _ => break,
                    }
                }
                rec.perks = r.loadout.perks.clone();
                r.next_ante(t);
            }
            _ => break,
        }
    }
    rec.perks = r.loadout.perks.clone();
    rec
}

/// Summary of many runs of one profile.
#[derive(Debug, Clone, Default)]
pub struct ProfileSummary {
    pub name: String,
    pub runs: usize,
    pub median_ante_reached: f64,
    pub ante_reached_hist: BTreeMap<u32, usize>,
    pub win_rate: f64,
    pub death_rate: f64,
    pub median_minutes_to_target: BTreeMap<u32, f64>,
    pub mean_multiplier: BTreeMap<u32, f64>,
    pub mean_wallet: BTreeMap<u32, f64>,
    pub route_share_in_wins: BTreeMap<String, f64>,
    pub perk_share_in_wins: BTreeMap<String, f64>,
}

fn median(v: &mut [f64]) -> f64 {
    if v.is_empty() {
        return f64::NAN;
    }
    v.sort_by(|a, b| a.partial_cmp(b).unwrap());
    let n = v.len();
    if n % 2 == 1 { v[n / 2] } else { (v[n / 2 - 1] + v[n / 2]) / 2.0 }
}

pub fn summarize(name: &str, recs: &[RunRecord]) -> ProfileSummary {
    let n = recs.len().max(1);
    let mut s = ProfileSummary { name: name.to_string(), runs: recs.len(), ..Default::default() };
    let mut reached: Vec<f64> = recs.iter().map(|r| r.ante_reached as f64).collect();
    s.median_ante_reached = median(&mut reached);
    for r in recs {
        *s.ante_reached_hist.entry(r.ante_reached).or_insert(0) += 1;
    }
    let wins: Vec<&RunRecord> = recs.iter().filter(|r| r.won).collect();
    s.win_rate = wins.len() as f64 / n as f64;
    s.death_rate = recs.iter().filter(|r| r.died).count() as f64 / n as f64;
    let mut per: BTreeMap<u32, Vec<f64>> = BTreeMap::new();
    for r in recs {
        for (a, m) in &r.minutes_to_target {
            per.entry(*a).or_default().push(*m);
        }
    }
    for (a, mut v) in per {
        s.median_minutes_to_target.insert(a, median(&mut v));
    }
    let mut mult: BTreeMap<u32, (f64, usize)> = BTreeMap::new();
    for r in recs {
        for (a, m) in &r.multipliers {
            let e = mult.entry(*a).or_insert((0.0, 0));
            e.0 += m;
            e.1 += 1;
        }
    }
    s.mean_multiplier = mult.into_iter().map(|(a, (sum, c))| (a, sum / c.max(1) as f64)).collect();
    let mut wal: BTreeMap<u32, (f64, usize)> = BTreeMap::new();
    for r in recs {
        for (a, w) in &r.wallets {
            let e = wal.entry(*a).or_insert((0.0, 0));
            e.0 += *w as f64;
            e.1 += 1;
        }
    }
    s.mean_wallet = wal.into_iter().map(|(a, (sum, c))| (a, sum / c.max(1) as f64)).collect();
    let wn = wins.len().max(1) as f64;
    for r in &wins {
        if let Some(route) = r.route {
            *s.route_share_in_wins.entry(format!("{route:?}")).or_insert(0.0) += 1.0 / wn;
        }
        let picks = r.perks.len().max(1) as f64;
        for p in &r.perks {
            // Share of all perk picks in winning runs (an expert holds 7 of
            // 14 by ante 8, so "in X% of wins" can't separate perks).
            *s.perk_share_in_wins.entry(p.clone()).or_insert(0.0) += 1.0 / wn / picks;
        }
    }
    s
}

/// Full report: every profile over every route, plus Average per route for
/// the ante 1 check.
pub struct Report {
    pub profiles: Vec<ProfileSummary>,
    pub average_ante1_by_route: BTreeMap<String, f64>,
    pub checks: Vec<(String, bool, String)>,
}

pub fn run_report(runs_per_profile: usize) -> Report {
    run_report_with(&SimData::embedded(), runs_per_profile)
}

pub fn run_report_with(s: &SimData, runs_per_profile: usize) -> Report {
    let t = sim_tables();
    let s = s.clone();
    let routes = [Route::Fighter, Route::Explorer, Route::Hunter];
    let mut profiles = Vec::new();
    let mut ante1 = BTreeMap::new();
    for (pi, p) in s.profiles.iter().enumerate() {
        let recs: Vec<RunRecord> = (0..runs_per_profile)
            .map(|i| {
                let route = routes[i % routes.len()];
                simulate_run(&t, &s, p, route, (pi as u64 + 1) * 1_000_003 + i as u64)
            })
            .collect();
        if p.name == "Average" {
            for route in routes {
                // Runs that never reached the target count as slower than any
                // that did, so the median is over every run.
                let mut v: Vec<f64> = recs.iter().filter(|r| r.route == Some(route)).map(|r| r.minutes_to_target.iter().find(|(a, _)| *a == 1).map_or(f64::INFINITY, |(_, m)| *m)).collect();
                ante1.insert(format!("{route:?}"), median(&mut v));
            }
        }
        profiles.push(summarize(&p.name, &recs));
    }
    let tg = &s.targets;
    let mut checks = Vec::new();
    if let Some(n) = profiles.iter().find(|p| p.name == "Novice") {
        let ok = n.median_ante_reached >= tg.novice_median_death.0 && n.median_ante_reached <= tg.novice_median_death.1;
        checks.push(("Novice median ante reached in 3-4".to_string(), ok, format!("{:.1}", n.median_ante_reached)));
    }
    if let Some(e) = profiles.iter().find(|p| p.name == "Expert") {
        let ok = e.win_rate >= tg.expert_win_rate.0 && e.win_rate <= tg.expert_win_rate.1;
        checks.push(("Expert win rate about 25%".to_string(), ok, format!("{:.1}%", e.win_rate * 100.0)));
        let worst_route = e.route_share_in_wins.iter().fold(("-".to_string(), 0.0f64), |m, (k, v)| if *v > m.1 { (k.clone(), *v) } else { m });
        checks.push((format!("No route in more than {:.0}% of Expert wins", tg.max_share_in_expert_wins * 100.0), worst_route.1 <= tg.max_share_in_expert_wins, format!("{} {:.0}%", worst_route.0, worst_route.1 * 100.0)));
        let worst_perk = e.perk_share_in_wins.iter().fold(("-".to_string(), 0.0f64), |m, (k, v)| if *v > m.1 { (k.clone(), *v) } else { m });
        checks.push((format!("No perk takes more than {:.0}% of perk picks in Expert wins", tg.max_perk_pick_share * 100.0), worst_perk.1 <= tg.max_perk_pick_share, format!("{} {:.0}%", worst_perk.0, worst_perk.1 * 100.0)));
    }
    for (route, m) in &ante1 {
        let ok = *m >= tg.average_ante1_minutes.0 && *m <= tg.average_ante1_minutes.1;
        checks.push((format!("Average clears ante 1 in about 9 min via {route}"), ok, format!("{m:.1} min")));
    }
    Report { profiles, average_ante1_by_route: ante1, checks }
}

impl Report {
    pub fn to_markdown(&self) -> String {
        let mut o = String::from("# Balance simulator report (SIMULATED, not player data)\n\n");
        o += "| Check | Result | Value |\n|---|---|---|\n";
        for (name, ok, v) in &self.checks {
            o += &format!("| {name} | {} | {v} |\n", if *ok { "pass" } else { "FAIL" });
        }
        for p in &self.profiles {
            o += &format!("\n## {} ({} runs)\n\n", p.name, p.runs);
            o += &format!("- Median ante reached: {:.1}; win rate {:.1}%; died {:.1}%\n", p.median_ante_reached, p.win_rate * 100.0, p.death_rate * 100.0);
            o += &format!("- Ante reached: {}\n", p.ante_reached_hist.iter().map(|(a, n)| format!("{a}: {n}")).collect::<Vec<_>>().join(", "));
            o += &format!("- Median minutes to target: {}\n", p.median_minutes_to_target.iter().map(|(a, m)| format!("A{a} {m:.1}")).collect::<Vec<_>>().join(", "));
            o += &format!("- Mean multiplier at ante end: {}\n", p.mean_multiplier.iter().map(|(a, m)| format!("A{a} x{m:.2}")).collect::<Vec<_>>().join(", "));
            o += &format!("- Mean Wallet at ante end: {}\n", p.mean_wallet.iter().map(|(a, w)| format!("A{a} {w:.0}")).collect::<Vec<_>>().join(", "));
            if !p.route_share_in_wins.is_empty() {
                o += &format!("- Route share of wins: {}\n", p.route_share_in_wins.iter().map(|(k, v)| format!("{k} {:.0}%", v * 100.0)).collect::<Vec<_>>().join(", "));
                let mut perks: Vec<_> = p.perk_share_in_wins.iter().collect();
                perks.sort_by(|a, b| b.1.partial_cmp(a.1).unwrap());
                o += &format!("- Top perks (share of picks in wins): {}\n", perks.iter().take(5).map(|(k, v)| format!("{k} {:.0}%", *v * 100.0)).collect::<Vec<_>>().join(", "));
            }
        }
        o
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn sim_runs_are_reproducible_and_progress() {
        let t = sim_tables();
        let s = SimData::embedded();
        let p = &s.profiles[1];
        let a = simulate_run(&t, &s, p, Route::Explorer, 77);
        let b = simulate_run(&t, &s, p, Route::Explorer, 77);
        assert_eq!(a.ante_reached, b.ante_reached);
        assert_eq!(a.minutes_to_target, b.minutes_to_target);
        assert!(a.ante_reached >= 1);
    }

    /// The full report, written where verify.sh asks (OA_SIM_OUT). Run with
    /// `cargo test --release balance_report -- --ignored`.
    #[test]
    #[ignore]
    fn balance_report() {
        let runs = std::env::var("OA_SIM_RUNS").ok().and_then(|v| v.parse().ok()).unwrap_or(SimData::embedded().runs_per_profile);
        let report = run_report(runs);
        let md = report.to_markdown();
        println!("{md}");
        if let Ok(dir) = std::env::var("OA_SIM_OUT") {
            std::fs::create_dir_all(&dir).unwrap();
            std::fs::write(format!("{dir}/balance.md"), &md).unwrap();
            let fails = report.checks.iter().filter(|c| !c.1).count();
            std::fs::write(format!("{dir}/balance_status.txt"), format!("{} {}\n", report.checks.len() - fails, fails)).unwrap();
        }
    }
}
