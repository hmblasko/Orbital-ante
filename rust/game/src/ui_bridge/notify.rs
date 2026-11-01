//! `Notifications` engine singleton: the top-right message queue (CLAUDE.md
//! section 10: max 3-4 on screen, colour and icon coded by type, never
//! spammy). Systems `push()`; the `AnteHud` draws what `visible()` returns.
//!
//! Serves pillar 5 (honest rewards: small gains get a small note, big ones
//! a big one) and fun rule 5 (instant feedback).

use std::collections::VecDeque;

use godot::obj::Singleton;
use godot::prelude::*;

/// What a notification is about; picks its colour and icon.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Kind {
    Points,
    BigPoints,
    Discovery,
    Contract,
    Info,
    Warning,
}

impl Kind {
    pub fn index(self) -> usize {
        self as usize
    }

    fn from_index(i: i64) -> Kind {
        match i {
            1 => Kind::BigPoints,
            2 => Kind::Discovery,
            3 => Kind::Contract,
            4 => Kind::Info,
            5 => Kind::Warning,
            _ => Kind::Points,
        }
    }
}

#[derive(Debug, Clone)]
pub struct Note {
    pub text: String,
    pub kind: Kind,
    pub age_s: f32,
    /// How many identical notes this one stands for ("x3").
    pub count: u32,
}

/// Queue rules (from res://data/ui/ante_hud.ron, set by the HUD).
#[derive(Debug, Clone, Copy)]
pub struct QueueRules {
    pub max_visible: usize,
    pub life_s: f32,
    /// Waiting notes beyond this are merged or dropped (oldest small ones first).
    pub max_waiting: usize,
}

impl Default for QueueRules {
    fn default() -> Self {
        Self { max_visible: 4, life_s: 4.0, max_waiting: 8 }
    }
}

/// The pure queue, so it can be unit-tested.
#[derive(Debug, Clone, Default)]
pub struct Queue {
    pub rules: QueueRules,
    pub shown: Vec<Note>,
    pub waiting: VecDeque<Note>,
}

impl Queue {
    pub fn push(&mut self, text: &str, kind: Kind) {
        // The same message again just counts up instead of stacking.
        if let Some(n) = self.shown.iter_mut().chain(self.waiting.iter_mut()).find(|n| n.text == text && n.kind == kind) {
            n.count += 1;
            n.age_s = n.age_s.min(self.rules.life_s * 0.5);
            return;
        }
        self.waiting.push_back(Note { text: text.to_string(), kind, age_s: 0.0, count: 1 });
        while self.waiting.len() > self.rules.max_waiting.max(1) {
            // Never drop a big one: drop the oldest small points note.
            let idx = self.waiting.iter().position(|n| n.kind == Kind::Points).unwrap_or(0);
            self.waiting.remove(idx);
        }
        self.fill();
    }

    pub fn step(&mut self, dt: f32) {
        for n in &mut self.shown {
            n.age_s += dt;
        }
        let life = self.rules.life_s;
        self.shown.retain(|n| n.age_s < life);
        self.fill();
    }

    fn fill(&mut self) {
        while self.shown.len() < self.rules.max_visible.max(1) {
            let Some(n) = self.waiting.pop_front() else { break };
            self.shown.push(n);
        }
    }

    pub fn clear(&mut self) {
        self.shown.clear();
        self.waiting.clear();
    }
}

#[derive(GodotClass)]
#[class(singleton, base = Object, init)]
pub struct Notifications {
    queue: Queue,
    base: Base<Object>,
}

#[godot_api]
impl Notifications {
    /// Push from GDScript: kind 0 points, 1 big points, 2 discovery,
    /// 3 contract, 4 info, 5 warning.
    #[func]
    pub fn notify(&mut self, text: GString, kind: i64) {
        self.queue.push(&text.to_string(), Kind::from_index(kind));
    }

    #[func]
    pub fn clear(&mut self) {
        self.queue.clear();
    }

    /// How many are on screen (tests).
    #[func]
    pub fn get_visible_count(&self) -> i64 {
        self.queue.shown.len() as i64
    }

    /// Every note on screen and waiting, as text (tests).
    #[func]
    pub fn get_texts(&self) -> PackedStringArray {
        self.queue.shown.iter().chain(self.queue.waiting.iter()).map(|n| GString::from(&n.text)).collect()
    }
}

impl Notifications {
    pub fn set_rules(&mut self, rules: QueueRules) {
        self.queue.rules = rules;
    }

    pub fn step(&mut self, dt: f32) {
        self.queue.step(dt);
    }

    pub fn shown(&self) -> &[Note] {
        &self.queue.shown
    }
}

/// Push a notification from any system (no-op if the singleton is missing).
pub fn push(text: &str, kind: Kind) {
    if !godot::classes::Engine::singleton().has_singleton("Notifications") {
        return;
    }
    Notifications::singleton().bind_mut().queue.push(text, kind);
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn at_most_max_visible_and_the_rest_wait() {
        let mut q = Queue::default();
        for i in 0..6 {
            q.push(&format!("n{i}"), Kind::Points);
        }
        assert_eq!(q.shown.len(), 4);
        assert_eq!(q.waiting.len(), 2);
        q.step(4.1);
        assert_eq!(q.shown.len(), 2, "expired ones make room for the waiting");
        assert_eq!(q.shown[0].text, "n4");
    }

    #[test]
    fn repeats_count_up_instead_of_spamming() {
        let mut q = Queue::default();
        q.push("Kill +25", Kind::Points);
        q.push("Kill +25", Kind::Points);
        q.push("Kill +25", Kind::Points);
        assert_eq!(q.shown.len(), 1);
        assert_eq!(q.shown[0].count, 3);
    }

    #[test]
    fn overflow_drops_small_points_before_big_news() {
        let mut q = Queue { rules: QueueRules { max_visible: 1, life_s: 4.0, max_waiting: 2 }, ..Default::default() };
        q.push("a", Kind::Points);
        q.push("big", Kind::Discovery);
        q.push("b", Kind::Points);
        q.push("c", Kind::Contract);
        let waiting: Vec<_> = q.waiting.iter().map(|n| n.text.as_str()).collect();
        assert_eq!(waiting, vec!["big", "c"]);
    }
}
