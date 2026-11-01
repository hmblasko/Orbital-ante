//! The enemy mind (CLAUDE.md section 7): Patrol -> Suspicious -> Alert ->
//! Search (last known position, about 45 s) -> Return. Pure state machine
//! fed with what the enemy perceives each tick; the Enemy node supplies the
//! senses and acts on the state. Each state has a visible tell (the Enemy
//! node shows it over the head; sounds wait for Phase 9).
//!
//! Serves pillar 2 (goofy but deadly) and fun rules 8 (fair failure: you
//! always see an enemy notice you before it shoots) and 2 (stealth vs
//! fighting is a real choice).

use godot::prelude::*;
use serde::Deserialize;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum AiState {
    Patrol,
    Suspicious,
    Alert,
    Search,
    Return,
}

impl AiState {
    pub fn name(self) -> &'static str {
        match self {
            AiState::Patrol => "Patrol",
            AiState::Suspicious => "Suspicious",
            AiState::Alert => "Alert",
            AiState::Search => "Search",
            AiState::Return => "Return",
        }
    }
}

#[derive(Debug, Clone, Deserialize)]
pub struct AiData {
    pub sight_m: f32,
    pub fov_deg: f32,
    pub notice_s: f32,
    pub crouch_notice_scale: f32,
    pub forget_per_s: f32,
    pub search_s: f32,
    pub shout_m: f32,
    pub first_shot_delay_s: f32,
    pub patrol_radius_m: f32,
    pub patrol_pause_s: (f32, f32),
    pub simple_beyond_m: f32,
    pub max_active: u32,
}

/// What the enemy sensed this tick.
#[derive(Debug, Clone, Copy, Default)]
pub struct Senses {
    /// The player is in view (in range, inside the field of view, line of sight clear).
    pub sees_player: bool,
    pub player_pos: Vector3,
    /// The player is crouched (harder to notice).
    pub player_crouched: bool,
    /// Perks that make the player slower to notice (1.0 or 0.0 = none).
    pub notice_mult: f32,
    /// A gunshot or loud noise was heard at this point.
    pub heard: Option<Vector3>,
    /// Took damage this tick (always alerts; the shooter's position is known).
    pub hurt_from: Option<Vector3>,
    /// A friend nearby shouted an alert about this position.
    pub shout: Option<Vector3>,
    /// Reached the point it was walking to (search spot or home).
    pub arrived: bool,
}

#[derive(Debug, Clone, PartialEq)]
pub struct Brain {
    pub state: AiState,
    /// 0 to 1: how sure the enemy is that someone's there.
    pub awareness: f32,
    pub last_known: Option<Vector3>,
    /// Seconds spent in the current state.
    pub in_state_s: f32,
    /// Seconds since the player was last seen.
    pub unseen_s: f32,
}

/// What a tick asks the body to do.
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum Intent {
    /// Follow the patrol route.
    Patrol,
    /// Stop and turn toward this point.
    Look(Vector3),
    /// Fight: move by style and shoot when allowed.
    Fight { target: Vector3, may_shoot: bool },
    /// Walk (warily) to this point and look around.
    Investigate(Vector3),
    GoHome,
}

impl Default for Brain {
    fn default() -> Self {
        Self { state: AiState::Patrol, awareness: 0.0, last_known: None, in_state_s: 0.0, unseen_s: 0.0 }
    }
}

impl Brain {
    fn enter(&mut self, s: AiState) {
        if self.state != s {
            self.state = s;
            self.in_state_s = 0.0;
        }
    }

    /// Advance by `dt`; returns what to do now.
    pub fn tick(&mut self, d: &AiData, s: &Senses, dt: f32) -> Intent {
        self.in_state_s += dt;
        if s.sees_player {
            let perk = if s.notice_mult > 0.0 { s.notice_mult } else { 1.0 };
            let scale = if s.player_crouched { d.crouch_notice_scale } else { 1.0 } * perk;
            self.awareness = (self.awareness + dt / (d.notice_s * scale).max(0.01)).min(1.0);
            self.last_known = Some(s.player_pos);
            self.unseen_s = 0.0;
        } else {
            self.unseen_s += dt;
            if self.state != AiState::Alert {
                self.awareness = (self.awareness - d.forget_per_s * dt).max(0.0);
            }
        }
        // Being shot or told by a friend means instant alert.
        if let Some(p) = s.hurt_from.or(s.shout) {
            self.awareness = 1.0;
            self.last_known = Some(p);
            self.enter(AiState::Alert);
        }
        match self.state {
            AiState::Patrol | AiState::Return => {
                if self.awareness >= 1.0 {
                    self.enter(AiState::Alert);
                } else if self.awareness > 0.0 || s.heard.is_some() {
                    if let Some(h) = s.heard {
                        self.last_known = Some(h);
                    }
                    self.enter(AiState::Suspicious);
                } else if self.state == AiState::Return && s.arrived {
                    self.enter(AiState::Patrol);
                }
            }
            AiState::Suspicious => {
                if let Some(h) = s.heard {
                    self.last_known = Some(h);
                    self.awareness = self.awareness.max(0.5);
                }
                if self.awareness >= 1.0 {
                    self.enter(AiState::Alert);
                } else if self.awareness <= 0.0 && self.in_state_s > 1.0 {
                    // Heard something, saw nothing: go and check.
                    if self.last_known.is_some() {
                        self.enter(AiState::Search);
                    } else {
                        self.enter(AiState::Return);
                    }
                }
            }
            AiState::Alert => {
                // Lost sight for a few seconds: go to the last known spot.
                if self.unseen_s > 3.0 {
                    self.enter(AiState::Search);
                }
            }
            AiState::Search => {
                if s.sees_player && self.awareness >= 0.5 {
                    self.awareness = 1.0;
                    self.enter(AiState::Alert);
                } else if self.in_state_s > d.search_s {
                    // Give up.
                    self.awareness = 0.0;
                    self.last_known = None;
                    self.enter(AiState::Return);
                }
            }
        }
        match self.state {
            AiState::Patrol => Intent::Patrol,
            AiState::Suspicious => self.last_known.map_or(Intent::Patrol, Intent::Look),
            AiState::Alert => Intent::Fight {
                target: self.last_known.unwrap_or(s.player_pos),
                may_shoot: s.sees_player && self.in_state_s >= d.first_shot_delay_s,
            },
            AiState::Search => self.last_known.map_or(Intent::GoHome, Intent::Investigate),
            AiState::Return => Intent::GoHome,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn data() -> AiData {
        crate::data::parse::<crate::combat::EnemiesData>(include_str!("../../../../godot/data/combat/enemies.ron")).unwrap().ai
    }

    fn seen(p: Vector3) -> Senses {
        Senses { sees_player: true, player_pos: p, ..Default::default() }
    }

    fn run(b: &mut Brain, d: &AiData, s: &Senses, seconds: f32) -> Intent {
        let mut last = Intent::Patrol;
        let steps = (seconds * 60.0) as usize;
        for _ in 0..steps {
            last = b.tick(d, s, 1.0 / 60.0);
        }
        last
    }

    #[test]
    fn noticing_takes_time_and_passes_through_suspicious() {
        let d = data();
        let mut b = Brain::default();
        let p = Vector3::new(10.0, 0.0, 0.0);
        run(&mut b, &d, &seen(p), d.notice_s * 0.5);
        assert_eq!(b.state, AiState::Suspicious);
        run(&mut b, &d, &seen(p), d.notice_s * 0.6);
        assert_eq!(b.state, AiState::Alert);
    }

    #[test]
    fn crouching_slows_noticing() {
        let d = data();
        let mut b = Brain::default();
        let s = Senses { player_crouched: true, ..seen(Vector3::ZERO) };
        run(&mut b, &d, &s, d.notice_s * 1.1);
        assert_ne!(b.state, AiState::Alert, "crouched, it takes longer");
    }

    #[test]
    fn there_is_a_reaction_delay_before_the_first_shot() {
        let d = data();
        let mut b = Brain::default();
        let p = Vector3::new(5.0, 0.0, 0.0);
        let hurt = Senses { hurt_from: Some(p), ..seen(p) };
        let i = b.tick(&d, &hurt, 1.0 / 60.0);
        assert_eq!(i, Intent::Fight { target: p, may_shoot: false });
        let i = run(&mut b, &d, &seen(p), d.first_shot_delay_s + 0.05);
        assert_eq!(i, Intent::Fight { target: p, may_shoot: true });
    }

    #[test]
    fn losing_the_player_leads_to_search_then_return_then_patrol() {
        let d = data();
        let mut b = Brain::default();
        let p = Vector3::new(8.0, 0.0, 0.0);
        run(&mut b, &d, &seen(p), d.notice_s + 0.5);
        assert_eq!(b.state, AiState::Alert);
        let i = run(&mut b, &d, &Senses::default(), 3.5);
        assert_eq!(b.state, AiState::Search);
        assert_eq!(i, Intent::Investigate(p));
        run(&mut b, &d, &Senses::default(), d.search_s + 0.5);
        assert_eq!(b.state, AiState::Return);
        let home = Senses { arrived: true, ..Default::default() };
        b.tick(&d, &home, 0.016);
        assert_eq!(b.state, AiState::Patrol);
    }

    #[test]
    fn a_heard_shot_makes_them_go_and_look() {
        let d = data();
        let mut b = Brain::default();
        let at = Vector3::new(0.0, 0.0, 20.0);
        let heard = Senses { heard: Some(at), ..Default::default() };
        let i = b.tick(&d, &heard, 0.016);
        assert_eq!(b.state, AiState::Suspicious);
        assert_eq!(i, Intent::Look(at));
        run(&mut b, &d, &Senses::default(), 3.0);
        assert_eq!(b.state, AiState::Search);
    }

    #[test]
    fn a_shout_alerts_friends() {
        let d = data();
        let mut b = Brain::default();
        let at = Vector3::new(3.0, 0.0, 3.0);
        b.tick(&d, &Senses { shout: Some(at), ..Default::default() }, 0.016);
        assert_eq!(b.state, AiState::Alert);
    }
}
