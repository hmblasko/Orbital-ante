//! Limb damage (CLAUDE.md section 7, TF2-style hit reactions): limbs shoot
//! off easily, a hit on the gun arm disarms, a lost leg drops the enemy, a
//! lost head or an empty health bar kills. Pure maths, so it is unit-tested;
//! the Enemy node turns the events into gibs, dropped guns and ragdolls.
//!
//! Serves pillar 2 (goofy but deadly) and fun rule 10 (game feel: every hit
//! visibly does something).

use serde::Deserialize;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Deserialize)]
pub enum Limb {
    Head,
    Torso,
    ArmL,
    ArmR,
    LegL,
    LegR,
}

impl Limb {
    pub const ALL: [Limb; 6] = [Limb::Head, Limb::Torso, Limb::ArmL, Limb::ArmR, Limb::LegL, Limb::LegR];

    pub fn index(self) -> usize {
        Limb::ALL.iter().position(|l| *l == self).unwrap_or(1)
    }

    pub fn from_index(i: usize) -> Limb {
        Limb::ALL.get(i).copied().unwrap_or(Limb::Torso)
    }

    pub fn is_arm(self) -> bool {
        matches!(self, Limb::ArmL | Limb::ArmR)
    }

    pub fn is_leg(self) -> bool {
        matches!(self, Limb::LegL | Limb::LegR)
    }
}

#[derive(Debug, Clone, Copy, Deserialize)]
pub struct LimbRule {
    pub health_share: f32,
    pub body_share: f32,
    pub multiplier: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct LimbRules {
    pub head: LimbRule,
    pub torso: LimbRule,
    pub arm: LimbRule,
    pub leg: LimbRule,
    pub disarm_damage: f32,
}

impl LimbRules {
    pub fn rule(&self, limb: Limb) -> &LimbRule {
        match limb {
            Limb::Head => &self.head,
            Limb::Torso => &self.torso,
            Limb::ArmL | Limb::ArmR => &self.arm,
            Limb::LegL | Limb::LegR => &self.leg,
        }
    }
}

/// What a hit did, in the order it happened.
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum HitEvent {
    /// The limb took damage but stayed on.
    Hurt { limb: Limb, damage: f32 },
    /// The gun was knocked out of the hands.
    Disarmed,
    /// The limb came off (a gib).
    LimbOff(Limb),
    /// The enemy can't stand any more (a leg came off).
    Dropped,
    Killed,
}

/// One enemy's body: whole-body health plus each limb's.
#[derive(Debug, Clone, PartialEq)]
pub struct LimbState {
    pub health: f32,
    pub max_health: f32,
    pub limb_health: [f32; 6],
    pub attached: [bool; 6],
    pub armed: bool,
    /// Which arm holds the gun.
    pub gun_arm: Limb,
    pub dead: bool,
    pub down: bool,
}

impl LimbState {
    pub fn new(max_health: f32, rules: &LimbRules) -> Self {
        let mut limb_health = [0.0; 6];
        for l in Limb::ALL {
            limb_health[l.index()] = max_health * rules.rule(l).health_share;
        }
        Self { health: max_health, max_health, limb_health, attached: [true; 6], armed: true, gun_arm: Limb::ArmR, dead: false, down: false }
    }

    pub fn is_attached(&self, limb: Limb) -> bool {
        self.attached[limb.index()]
    }

    /// Apply `damage` to `limb`. A limb that is already off passes the hit
    /// to the torso (you hit the stump).
    pub fn hit(&mut self, limb: Limb, damage: f32, rules: &LimbRules) -> Vec<HitEvent> {
        let mut out = Vec::new();
        if self.dead || damage <= 0.0 {
            return out;
        }
        let limb = if self.is_attached(limb) { limb } else { Limb::Torso };
        let rule = rules.rule(limb);
        let dealt = damage * rule.multiplier;
        out.push(HitEvent::Hurt { limb, damage: dealt });
        if limb == self.gun_arm && self.armed && dealt >= rules.disarm_damage {
            self.armed = false;
            out.push(HitEvent::Disarmed);
        }
        let i = limb.index();
        if limb != Limb::Torso {
            self.limb_health[i] -= dealt;
            if self.limb_health[i] <= 0.0 {
                self.attached[i] = false;
                out.push(HitEvent::LimbOff(limb));
                if limb == self.gun_arm && self.armed {
                    self.armed = false;
                    out.push(HitEvent::Disarmed);
                }
                if limb.is_leg() && !self.down {
                    self.down = true;
                    out.push(HitEvent::Dropped);
                }
            }
        }
        self.health -= dealt * rule.body_share;
        if self.health <= 0.0 || !self.is_attached(Limb::Head) {
            self.health = self.health.max(0.0);
            self.dead = true;
            out.push(HitEvent::Killed);
        }
        out
    }

    pub fn health_fraction(&self) -> f32 {
        (self.health / self.max_health.max(0.01)).clamp(0.0, 1.0)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn rules() -> LimbRules {
        crate::data::parse::<crate::combat::EnemiesData>(include_str!("../../../../godot/data/combat/enemies.ron")).unwrap().limbs
    }

    #[test]
    fn a_torso_hit_only_hurts() {
        let r = rules();
        let mut s = LimbState::new(100.0, &r);
        let e = s.hit(Limb::Torso, 14.0, &r);
        assert_eq!(e, vec![HitEvent::Hurt { limb: Limb::Torso, damage: 14.0 }]);
        assert!((s.health - 86.0).abs() < 1e-4);
    }

    #[test]
    fn a_hard_gun_arm_hit_disarms_and_arms_come_off_easily() {
        let r = rules();
        let mut s = LimbState::new(100.0, &r);
        let e = s.hit(Limb::ArmR, 14.0, &r);
        assert!(e.contains(&HitEvent::Disarmed));
        assert!(!s.armed);
        // Arm health is 30: three rifle hits take it off.
        s.hit(Limb::ArmR, 14.0, &r);
        let e = s.hit(Limb::ArmR, 14.0, &r);
        assert!(e.contains(&HitEvent::LimbOff(Limb::ArmR)));
        assert!(!s.dead, "losing an arm alone doesn't kill");
    }

    #[test]
    fn losing_a_leg_drops_the_enemy() {
        let r = rules();
        let mut s = LimbState::new(100.0, &r);
        let mut events = Vec::new();
        for _ in 0..3 {
            events.extend(s.hit(Limb::LegL, 14.0, &r));
        }
        assert!(events.contains(&HitEvent::LimbOff(Limb::LegL)));
        assert!(events.contains(&HitEvent::Dropped));
        assert!(s.down);
    }

    #[test]
    fn headshots_hit_double_and_a_lost_head_kills() {
        let r = rules();
        let mut s = LimbState::new(100.0, &r);
        s.hit(Limb::Head, 14.0, &r);
        assert!((s.health - 72.0).abs() < 1e-4);
        let e = s.hit(Limb::Head, 14.0, &r);
        assert!(e.contains(&HitEvent::LimbOff(Limb::Head)));
        assert!(e.contains(&HitEvent::Killed));
    }

    #[test]
    fn hits_on_a_missing_limb_land_on_the_torso() {
        let r = rules();
        let mut s = LimbState::new(100.0, &r);
        for _ in 0..3 {
            s.hit(Limb::ArmL, 14.0, &r);
        }
        let e = s.hit(Limb::ArmL, 10.0, &r);
        assert_eq!(e[0], HitEvent::Hurt { limb: Limb::Torso, damage: 10.0 });
    }

    #[test]
    fn enough_damage_kills_once() {
        let r = rules();
        let mut s = LimbState::new(100.0, &r);
        let e = s.hit(Limb::Torso, 150.0, &r);
        assert!(e.contains(&HitEvent::Killed));
        assert!(s.hit(Limb::Torso, 10.0, &r).is_empty(), "the dead take no more hits");
    }
}
