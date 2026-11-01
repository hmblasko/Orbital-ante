//! `Points` engine singleton: the run in progress (`RunState`) plus the
//! calls the world, the HUD and the between-ante screens make. Systems ask
//! it for perk and attribute modifiers with `stat()` and `flag()`.
//!
//! Serves pillar 3 (every run counts) and pillar 5 (honest rewards: every
//! award shows base x multiplier); fun rules 2, 5 and 9.

use godot::obj::Singleton;
use godot::prelude::*;

use super::maths::{Pace, format_points};
use super::perks::Mods;
use super::run::{ContractKind, Outcome, Phase, Prize, RunState, Tables, WorldEvent};
use crate::ui_bridge::notify::{Kind, push};

#[derive(GodotClass)]
#[class(singleton, base = Object)]
pub struct Points {
    tables: Tables,
    run: Option<RunState>,
    /// Second Wind used this ante.
    second_wind_used: bool,
    /// The build's modifiers, recomputed when the loadout changes.
    mods: Mods,
    /// Bumped on every loadout change, so systems can cache scaled stats.
    rev: i64,
    base: Base<Object>,
}

#[godot_api]
impl IObject for Points {
    fn init(base: Base<Object>) -> Self {
        Self { tables: Tables::load(), run: None, second_wind_used: false, mods: Mods::default(), rev: 0, base }
    }
}

fn phase_name(p: Phase) -> &'static str {
    match p {
        Phase::Board => "board",
        Phase::Playing => "playing",
        Phase::Upgrade => "upgrade",
        Phase::Over => "over",
    }
}

fn kind_name(k: ContractKind) -> &'static str {
    match k {
        ContractKind::ClearCamp => "clear_camp",
        ContractKind::KillTier => "kill_tier",
        ContractKind::Capture => "capture",
        ContractKind::Discover => "discover",
        ContractKind::Land => "land",
    }
}

#[godot_api]
impl Points {
    // ---- Run lifecycle ----

    /// Start a fresh run with the current RunSeed (ante 1, contract board).
    #[func]
    pub fn start_run(&mut self) {
        self.tables = Tables::load();
        let seed = crate::seed::safe_run_seed();
        self.run = Some(RunState::new(seed, &self.tables));
        self.second_wind_used = false;
        self.refresh_mods();
    }

    #[func]
    pub fn has_run(&self) -> bool {
        self.run.is_some()
    }

    /// Drop the run (back to the menu).
    #[func]
    pub fn clear_run(&mut self) {
        self.run = None;
        self.refresh_mods();
    }

    /// Changes whenever the build changes (perk picked, attribute bought).
    #[func]
    pub fn get_rev(&self) -> i64 {
        self.rev
    }

    /// The player died: the run ends (section 2).
    #[func]
    pub fn end_run_died(&mut self) {
        if let Some(r) = &mut self.run {
            r.finish(Outcome::Died);
        }
    }

    #[func]
    pub fn get_phase(&self) -> GString {
        GString::from(self.run.as_ref().map_or("none", |r| phase_name(r.phase)))
    }

    #[func]
    pub fn get_outcome(&self) -> GString {
        GString::from(match self.run.as_ref().and_then(|r| r.outcome) {
            Some(Outcome::TimeUp) => "time_up",
            Some(Outcome::Died) => "died",
            Some(Outcome::Won) => "won",
            None => "",
        })
    }

    // ---- Playing ----

    #[func]
    pub fn get_ante(&self) -> i64 {
        self.run.as_ref().map_or(0, |r| r.ante as i64)
    }

    #[func]
    pub fn get_target(&self) -> i64 {
        self.run.as_ref().map_or(0, |r| r.target(&self.tables) as i64)
    }

    #[func]
    pub fn get_score(&self) -> i64 {
        self.run.as_ref().map_or(0, |r| r.score.score as i64)
    }

    #[func]
    pub fn get_wallet(&self) -> i64 {
        self.run.as_ref().map_or(0, |r| r.wallet as i64)
    }

    #[func]
    pub fn get_time_left(&self) -> f64 {
        self.run.as_ref().map_or(0.0, |r| r.time_left)
    }

    #[func]
    pub fn get_multiplier(&self) -> f64 {
        self.run.as_ref().map_or(1.0, |r| r.multiplier(&self.tables))
    }

    /// "ahead", "on_pace" or "behind" (section 18.7).
    #[func]
    pub fn get_pace(&self) -> GString {
        let Some(r) = &self.run else { return GString::new() };
        let pace = r.score.pace(&self.tables.rules, r.elapsed, r.time_left, r.target(&self.tables));
        GString::from(match pace {
            Pace::Ahead => "ahead",
            Pace::OnPace => "on_pace",
            Pace::Behind => "behind",
        })
    }

    /// Format a number the way the HUD does ("1,234", "160k").
    #[func]
    pub fn format(&self, v: i64) -> GString {
        GString::from(&format_points(v.max(0) as u64, self.tables.antes.compact_display_above))
    }

    /// Advance the ante clock. Returns true when the ante just ended.
    #[func]
    pub fn tick(&mut self, dt: f64) -> bool {
        let Some(r) = &mut self.run else { return false };
        let ended = r.tick(dt, &self.tables);
        if ended {
            self.second_wind_used = false;
        }
        ended
    }

    /// Award points from a named source (rules.ron) at its base value.
    /// Notifies the HUD. Returns the points paid (0 outside play).
    #[func]
    pub fn award(&mut self, source: GString, base: i64, label_key: GString) -> i64 {
        let Some(r) = &mut self.run else { return 0 };
        let Some(a) = r.award(&source.to_string(), base.max(0) as u64, &self.tables) else { return 0 };
        let label = tr(&label_key);
        let big = a.points as f64 >= 0.1 * r.target(&self.tables) as f64;
        // Big gains show the formula so players learn it (18.7).
        let text = if big || a.multiplier > 1.001 {
            format!("{}  {} x {:.1} = +{}", label, a.base, a.multiplier, a.points)
        } else {
            format!("{}  +{}", label, a.points)
        };
        push(&text, if big { Kind::BigPoints } else { Kind::Points });
        if a.new_category && r.score.categories.len() > 1 {
            push(&format!("{} x{:.1}", tr(&GString::from("NOTE_NEW_CATEGORY")), r.score.variety(&self.tables.rules)), Kind::Info);
        }
        a.points as i64
    }

    /// Kills by tier (1-3) on a body.
    #[func]
    pub fn on_kill(&mut self, tier: i64, body: GString) {
        let base = self.tables.kill_points(tier as u32) as i64;
        self.award(GString::from(&format!("kill_t{}", tier.clamp(1, 3))), base, GString::from(&format!("SOURCE_KILL_T{}", tier.clamp(1, 3))));
        self.world(WorldEvent::Kill { tier: tier as u32, body: body.to_string() });
    }

    #[func]
    pub fn on_limb_shot(&mut self) {
        let base = self.tables.sources.limb_shot as i64;
        self.award(GString::from("limb_shot"), base, GString::from("SOURCE_LIMB"));
    }

    #[func]
    pub fn on_disarm(&mut self) {
        let base = self.tables.sources.disarm as i64;
        self.award(GString::from("disarm"), base, GString::from("SOURCE_DISARM"));
    }

    #[func]
    pub fn on_captured(&mut self, body: GString) {
        push(&tr(&GString::from("NOTE_CAPTURED")), Kind::Info);
        self.world(WorldEvent::Captured { body: body.to_string() });
    }

    /// Every soldier of a camp is down or tied (a small base clear). Once
    /// per camp per ante (camps are re-manned every ante).
    #[func]
    pub fn on_camp_cleared(&mut self, camp: GString, body: GString) {
        let Some(r) = &mut self.run else { return };
        if r.phase != Phase::Playing || !r.first_time(&format!("{camp}:a{}", r.ante)) {
            return;
        }
        let base = self.tables.sources.base_clear.small as i64;
        self.award(GString::from("base_clear"), base, GString::from("SOURCE_CAMP_CLEAR"));
        self.world(WorldEvent::CampCleared { body: body.to_string() });
    }

    /// First discovery of `key` this run: points, +20 s, a notification.
    /// Returns false if already discovered.
    #[func]
    pub fn on_discovered(&mut self, key: GString, body: GString, points: i64, label: GString) -> bool {
        let Some(r) = &mut self.run else { return false };
        if r.phase != Phase::Playing || !r.discover(&key.to_string()) {
            return false;
        }
        r.add_time(self.tables.antes.first_discovery_bonus_s as f64);
        let paid = self.award(GString::from("discovery"), points, GString::from("SOURCE_DISCOVERY"));
        push(&format!("{}: {}  (+{} s)", tr(&GString::from("NOTE_DISCOVERED")), label, self.tables.antes.first_discovery_bonus_s as i64), Kind::Discovery);
        let _ = paid;
        self.world(WorldEvent::Discovered { body: body.to_string() });
        true
    }

    /// First time standing on a body this run.
    #[func]
    pub fn on_landed(&mut self, body: GString) -> bool {
        let key = format!("landed:{body}");
        let Some(r) = &mut self.run else { return false };
        if r.phase != Phase::Playing || !r.discover(&key) {
            return false;
        }
        r.add_time(self.tables.antes.first_discovery_bonus_s as f64);
        let points = self.tables.run.first_landing_points as i64;
        self.award(GString::from("first_landing"), points, GString::from("SOURCE_FIRST_LANDING"));
        self.world(WorldEvent::Landed { body: body.to_string() });
        true
    }

    #[func]
    pub fn add_time(&mut self, seconds: f64) {
        if let Some(r) = &mut self.run {
            r.add_time(seconds);
        }
    }

    #[func]
    pub fn can_cash_out(&self) -> bool {
        self.run.as_ref().is_some_and(|r| r.can_cash_out(&self.tables))
    }

    /// What cashing out now would pay.
    #[func]
    pub fn get_cash_out_value(&self) -> i64 {
        self.run.as_ref().map_or(0, |r| self.tables.rules.cash_out(r.time_left, r.target(&self.tables)) as i64)
    }

    #[func]
    pub fn get_cash_out_hold_s(&self) -> f32 {
        self.tables.rules.cash_out.hold_s
    }

    #[func]
    pub fn cash_out(&mut self) -> i64 {
        let Some(r) = &mut self.run else { return -1 };
        let paid = r.cash_out(&self.tables).map_or(-1, |p| p as i64);
        if paid >= 0 {
            self.second_wind_used = false;
        }
        paid
    }

    // ---- Contract board ----

    #[func]
    pub fn get_board(&self) -> VarArray {
        let mut out = VarArray::new();
        let Some(r) = &self.run else { return out };
        for c in &r.board {
            let mut d = self.contract_dict(c);
            d.set("taken", &r.taken.iter().any(|t| t.id == c.id).to_variant());
            out.push(&d.to_variant());
        }
        out
    }

    /// Taken contracts with progress (HUD and summary).
    #[func]
    pub fn get_contracts(&self) -> VarArray {
        let mut out = VarArray::new();
        if let Some(r) = &self.run {
            for c in &r.taken {
                out.push(&self.contract_dict(c).to_variant());
            }
        }
        out
    }

    #[func]
    pub fn get_max_contracts(&self) -> i64 {
        self.tables.run.contracts_max_taken as i64
    }

    #[func]
    pub fn toggle_contract(&mut self, index: i64) -> bool {
        let Some(r) = &mut self.run else { return false };
        r.toggle_contract(index.max(0) as usize, &self.tables)
    }

    /// Close the board; the clock starts.
    #[func]
    pub fn start_ante(&mut self) {
        if let Some(r) = &mut self.run {
            r.start_ante();
        }
    }

    // ---- Upgrade screen ----

    #[func]
    pub fn get_last_result(&self) -> VarDictionary {
        let mut d = VarDictionary::new();
        if let Some(res) = self.run.as_ref().and_then(|r| r.last_result.as_ref()) {
            d.set("ante", &(res.ante as i64).to_variant());
            d.set("score", &(res.score as i64).to_variant());
            d.set("target", &(res.target as i64).to_variant());
            d.set("cash_out", &(res.cash_out as i64).to_variant());
            d.set("interest", &(res.interest as i64).to_variant());
        }
        d
    }

    #[func]
    pub fn get_attributes(&self) -> VarArray {
        let mut out = VarArray::new();
        let Some(r) = &self.run else { return out };
        for a in &self.tables.perks.attributes {
            let mut d = VarDictionary::new();
            d.set("id", &GString::from(&a.id).to_variant());
            d.set("level", &(r.attribute_level(&a.id) as i64).to_variant());
            d.set("max", &(self.tables.rules.prices.attribute_t.len() as i64).to_variant());
            d.set("price", &r.attribute_price(&a.id, &self.tables).map_or(-1, |p| p as i64).to_variant());
            out.push(&d.to_variant());
        }
        out
    }

    #[func]
    pub fn buy_attribute(&mut self, id: GString) -> bool {
        let Some(r) = &mut self.run else { return false };
        let ok = r.buy_attribute(&id.to_string(), &self.tables);
        self.refresh_mods();
        ok
    }

    #[func]
    pub fn get_draft(&self) -> VarArray {
        let mut out = VarArray::new();
        let Some(r) = &self.run else { return out };
        for id in &r.draft {
            let Some(p) = self.tables.perks.perk(id) else { continue };
            out.push(&self.perk_dict(p).to_variant());
        }
        out
    }

    #[func]
    pub fn get_perks(&self) -> VarArray {
        let mut out = VarArray::new();
        let Some(r) = &self.run else { return out };
        for id in &r.loadout.perks {
            if let Some(p) = self.tables.perks.perk(id) {
                out.push(&self.perk_dict(p).to_variant());
            }
        }
        out
    }

    /// Tags held, with counts (the build screen shows 2 and 4 bonuses).
    #[func]
    pub fn get_tags(&self) -> VarDictionary {
        let mut d = VarDictionary::new();
        if let Some(r) = &self.run {
            let _ = r;
            for (tag, n) in self.mods.tag_counts.clone() {
                d.set(tag.key(), &(n as i64).to_variant());
            }
        }
        d
    }

    #[func]
    pub fn has_picked_perk(&self) -> bool {
        self.run.as_ref().is_some_and(|r| r.picked_perk)
    }

    #[func]
    pub fn get_reroll_price(&self) -> i64 {
        self.run.as_ref().map_or(0, |r| r.reroll_price(&self.tables) as i64)
    }

    #[func]
    pub fn reroll(&mut self) -> bool {
        let Some(r) = &mut self.run else { return false };
        r.reroll(&self.tables)
    }

    #[func]
    pub fn pick_perk(&mut self, id: GString) -> bool {
        let Some(r) = &mut self.run else { return false };
        let ok = r.pick_perk(&id.to_string());
        self.refresh_mods();
        ok
    }

    /// The ticket: rarity and prize are hidden until scratched.
    #[func]
    pub fn get_ticket(&self) -> VarDictionary {
        let mut d = VarDictionary::new();
        if let Some(t) = self.run.as_ref().and_then(|r| r.ticket.as_ref()) {
            d.set("scratched", &t.scratched.to_variant());
            if t.scratched {
                self.ticket_into(&mut d, t);
            }
        }
        d
    }

    #[func]
    pub fn scratch(&mut self) -> VarDictionary {
        let mut d = VarDictionary::new();
        let Some(r) = &mut self.run else { return d };
        if let Some(t) = r.scratch() {
            d.set("scratched", &true.to_variant());
            self.ticket_into(&mut d, &t);
        }
        d
    }

    /// Odds shown on the ticket (section 13: all odds documented in-game).
    #[func]
    pub fn get_scratch_odds(&self) -> VarArray {
        let mut out = VarArray::new();
        let total: f64 = self.tables.run.scratch_odds.iter().map(|o| o.weight).sum();
        for o in &self.tables.run.scratch_odds {
            let mut d = VarDictionary::new();
            d.set("rarity_key", &GString::from(o.rarity.key()).to_variant());
            d.set("percent", &(o.weight / total * 100.0).to_variant());
            d.set("prize", &self.prize_text(o.prize, None).to_variant());
            out.push(&d.to_variant());
        }
        out
    }

    #[func]
    pub fn get_scratch_reveal_s(&self) -> f32 {
        self.tables.run.scratch_reveal_s
    }

    /// Leave the upgrade screen; the next ante's board opens.
    #[func]
    pub fn next_ante(&mut self) -> bool {
        let Some(r) = &mut self.run else { return false };
        r.next_ante(&self.tables)
    }

    // ---- Summary ----

    #[func]
    pub fn get_summary(&self) -> VarDictionary {
        let mut d = VarDictionary::new();
        let Some(r) = &self.run else { return d };
        d.set("ante", &(r.ante as i64).to_variant());
        d.set("antes_cleared", &(r.stats.antes_cleared as i64).to_variant());
        d.set("total_points", &(r.stats.total_points as i64).to_variant());
        d.set("kills", &(r.stats.kills as i64).to_variant());
        d.set("captures", &(r.stats.captures as i64).to_variant());
        d.set("discoveries", &(r.stats.discoveries as i64).to_variant());
        d.set("contracts_done", &(r.stats.contracts_done as i64).to_variant());
        d.set("best_multiplier", &r.stats.best_multiplier.to_variant());
        d.set("outcome", &self.get_outcome().to_variant());
        d.set("seed", &GString::from(&crate::seed::encode_seed(r.seed)).to_variant());
        d
    }

    #[func]
    pub fn get_leaderboard_size(&self) -> i64 {
        self.tables.run.leaderboard_size as i64
    }

    // ---- Modifiers for other systems ----

    /// A stat multiplier from perks, tags and attributes (1.0 = unchanged).
    #[func]
    pub fn stat(&self, name: GString) -> f32 {
        self.stat_str(&name.to_string())
    }

    #[func]
    pub fn flag(&self, name: GString) -> bool {
        self.flag_str(&name.to_string())
    }

    /// Second Wind: once per ante, a lethal hit leaves 1 HP. Returns true
    /// (and uses it up) if it saves you now.
    #[func]
    pub fn try_second_wind(&mut self) -> bool {
        if self.second_wind_used || !self.flag_str("second_wind") {
            return false;
        }
        self.second_wind_used = true;
        push(&tr(&GString::from("NOTE_SECOND_WIND")), Kind::Warning);
        true
    }

    // ---- Tests ----

    #[func]
    pub fn debug_set_time_left(&mut self, seconds: f64) {
        if let Some(r) = &mut self.run {
            r.time_left = seconds;
        }
    }

    #[func]
    pub fn debug_give_perk(&mut self, id: GString) {
        if let Some(r) = &mut self.run {
            r.loadout.perks.push(id.to_string());
        }
        self.refresh_mods();
    }
}

impl Points {
    pub fn stat_str(&self, name: &str) -> f32 {
        self.mods.stat(name)
    }

    pub fn flag_str(&self, name: &str) -> bool {
        self.mods.has(name)
    }

    fn refresh_mods(&mut self) {
        self.mods = self.run.as_ref().map_or_else(Mods::default, |r| r.mods(&self.tables));
        self.rev += 1;
    }

    pub fn tables(&self) -> &Tables {
        &self.tables
    }

    pub fn run(&self) -> Option<&RunState> {
        self.run.as_ref()
    }

    fn world(&mut self, e: WorldEvent) {
        let Some(r) = &mut self.run else { return };
        let paid = r.event(&e, &self.tables);
        for (c, a) in paid {
            push(&format!("{}: {}  +{}", tr(&GString::from("NOTE_CONTRACT_DONE")), contract_title(&c.id), a.points), Kind::Contract);
        }
    }

    fn contract_dict(&self, c: &super::run::Contract) -> VarDictionary {
        let mut d = VarDictionary::new();
        d.set("id", &GString::from(&c.id).to_variant());
        d.set("title", &GString::from(&contract_title(&c.id)).to_variant());
        d.set("desc", &GString::from(&contract_desc(c)).to_variant());
        d.set("kind", &GString::from(kind_name(c.kind)).to_variant());
        d.set("risk", &(c.risk as i64).to_variant());
        d.set("body", &GString::from(&c.body).to_variant());
        d.set("count", &(c.count as i64).to_variant());
        d.set("reward", &(c.reward as i64).to_variant());
        d.set("progress", &(c.progress as i64).to_variant());
        d.set("done", &c.done.to_variant());
        d
    }

    fn perk_dict(&self, p: &super::perks::Perk) -> VarDictionary {
        let mut d = VarDictionary::new();
        d.set("id", &GString::from(&p.id).to_variant());
        d.set("name_key", &GString::from(&p.name_key()).to_variant());
        d.set("desc_key", &GString::from(&p.desc_key()).to_variant());
        let tags: PackedStringArray = p.tags.iter().map(|t| GString::from(t.key())).collect();
        d.set("tags", &tags.to_variant());
        d
    }

    fn prize_text(&self, prize: Prize, amount: Option<u64>) -> GString {
        match (prize, amount) {
            (Prize::Wallet(_), Some(a)) => GString::from(&format!("+{} {}", format_points(a, self.tables.antes.compact_display_above), tr(&GString::from("WORD_WALLET")))),
            (Prize::Wallet(f), None) => GString::from(&format!("{:.0}% {} {}", f * 100.0, tr(&GString::from("WORD_OF_TARGET")), tr(&GString::from("WORD_WALLET")))),
            (Prize::Stopwatch, _) => GString::from(&format!("{} (+{} s)", tr(&GString::from("PRIZE_STOPWATCH")), self.tables.antes.stopwatch_bonus_s as i64)),
        }
    }

    fn ticket_into(&self, d: &mut VarDictionary, t: &super::run::Ticket) {
        d.set("rarity_key", &GString::from(t.rarity.key()).to_variant());
        d.set("rarity", &(t.rarity as i64).to_variant());
        d.set("amount", &(t.amount as i64).to_variant());
        d.set("prize", &self.prize_text(t.prize, Some(t.amount)).to_variant());
    }
}

fn tr(key: &GString) -> String {
    godot::classes::TranslationServer::singleton().translate(&StringName::from(key)).to_string()
}

fn contract_title(id: &str) -> String {
    tr(&GString::from(&format!("CONTRACT_{}", id.to_uppercase())))
}

fn contract_desc(c: &super::run::Contract) -> String {
    let body = if c.body.is_empty() { String::new() } else { tr(&GString::from(&format!("BODY_{}", c.body.to_uppercase()))) };
    tr(&GString::from(&format!("CONTRACT_{}_DESC", c.id.to_uppercase()))).replace("{count}", &c.count.to_string()).replace("{body}", &body)
}

/// A stat multiplier from the current run (1.0 outside a run). Safe to
/// call from any system.
pub fn stat(name: &str) -> f32 {
    if !godot::classes::Engine::singleton().has_singleton("Points") {
        return 1.0;
    }
    Points::singleton().bind().stat_str(name)
}

pub fn flag(name: &str) -> bool {
    if !godot::classes::Engine::singleton().has_singleton("Points") {
        return false;
    }
    Points::singleton().bind().flag_str(name)
}

/// Run something on the Points singleton if it exists.
pub fn with_points(f: impl FnOnce(&mut Points)) {
    if !godot::classes::Engine::singleton().has_singleton("Points") {
        return;
    }
    let mut p = Points::singleton();
    f(&mut p.bind_mut());
}

/// The build revision (0 outside a run): cache scaled stats against it.
pub fn rev() -> i64 {
    if !godot::classes::Engine::singleton().has_singleton("Points") {
        return 0;
    }
    Points::singleton().bind().rev
}

/// Heat numbers (perks.ron): below-half threshold, damage multiplier,
/// kill-heal threshold, kill-heal amount (fraction of max health).
pub fn perk_numbers() -> (f32, f32, f32, f32) {
    if !godot::classes::Engine::singleton().has_singleton("Points") {
        return (0.0, 1.0, 0.0, 0.0);
    }
    let p = Points::singleton();
    let d = &p.bind().tables.perks;
    (d.heat_low_health_below, d.heat_low_health_damage, d.heat_kill_heal_below, d.heat_kill_heal_amount)
}
