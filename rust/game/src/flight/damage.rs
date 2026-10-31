//! Ship damage: hard impacts hurt the parts near where you hit, damaged
//! parts work worse, and a part that takes a beating past zero breaks off
//! and tumbles away for good (CLAUDE.md section 5, "Damage, crashes and
//! repair"). A pilot who isn't buckled in gets thrown about and can die.
//!
//! The pure maths (which parts an impact reaches, how hard, when one breaks
//! off, how well the ship still flies) lives here with unit tests; the ship
//! applies it to real contacts and turns broken parts into debris.
//!
//! Serves pillar 4 (everything is physical: crashes are physics, not
//! scripts), pillar 3 (every death teaches: buckle up) and fun rule 11
//! (emergent stories: screenshot-worthy crashes).

use godot::prelude::*;
use serde::Deserialize;

type V3 = (f32, f32, f32);

#[derive(Debug, Clone, Copy, PartialEq, Eq, Deserialize)]
pub enum PartKind {
    /// Main engine: its health scales main-engine thrust.
    Engine,
    /// Attitude thrusters: their combined health scales turning and strafing.
    Thrusters,
    /// Landing leg: cosmetic for now (the ship rests on what's left).
    Leg,
    /// Antenna: cosmetic for now (the radio arrives later).
    Antenna,
}

/// A part of the hull model, picked out by a slice of a cylinder around
/// the ship's up axis (ship space: +Y up, angles from +X toward +Z).
#[derive(Debug, Clone, Deserialize)]
pub struct PartData {
    pub id: String,
    pub name_key: String,
    pub kind: PartKind,
    pub y: (f32, f32),
    pub r: (f32, f32),
    pub angle_deg: f32,
    /// Half the slice's width in degrees; 180 takes the whole ring.
    pub half_width_deg: f32,
    pub hp: f32,
    /// Breaks off once damage past zero reaches this (0 = never).
    pub detach_overkill: f32,
    /// Mass of the piece that flies off (kg-ish units, as the ship's).
    pub debris_mass: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct DamageData {
    /// Impacts slower than this (m/s change) do nothing: landings, bumps.
    pub safe_impact_mps: f32,
    /// Damage per m/s of impact above the safe speed.
    pub damage_per_mps: f32,
    /// Parts within this distance of the contact share the hit, falling
    /// off linearly; the hull core takes the rest.
    pub spread_m: f32,
    /// Hull core health, shown on the dash; hits that land on no part
    /// hurt it.
    pub hull_hp: f32,
    /// How fast a broken-off part is flung away from the ship (m/s).
    pub debris_kick_mps: f32,
    /// Seconds a part shows sparks after a hit.
    pub spark_s: f32,
    pub pilot: PilotData,
    pub repair: RepairData,
    pub parts: Vec<PartData>,
}

/// Fixing parts by hand (hold X / E while looking at one, from outside).
#[derive(Debug, Clone, Deserialize)]
pub struct RepairData {
    /// How close your eyes must be to the part's nearest point (m).
    pub reach_m: f32,
    /// How far off the centre of your view it can be (degrees).
    pub cone_deg: f32,
    /// Health restored per second of holding.
    pub hp_per_s: f32,
    /// Where you fix the hull core from (ship space).
    pub hull_at: V3,
}

/// Nearest point of a part's slice to ship-space `p` (approximate: clamps
/// height and radius, and angle to the slice's width).
pub fn part_nearest_point(d: &PartData, p: Vector3) -> Vector3 {
    let y = p.y.clamp(d.y.0, d.y.1);
    let flat = Vector2::new(p.x, p.z);
    let r = flat.length().clamp(d.r.0, d.r.1);
    let mut a = p.z.atan2(p.x).to_degrees();
    if d.half_width_deg < 180.0 {
        let diff = (a - d.angle_deg + 540.0).rem_euclid(360.0) - 180.0;
        a = d.angle_deg + diff.clamp(-d.half_width_deg, d.half_width_deg);
    }
    let a = a.to_radians();
    Vector3::new(r * a.cos(), y, r * a.sin())
}

/// Heal a part (never past full, never a broken-off one). Returns true
/// once it's at full health.
pub fn repair_part(d: &PartData, s: &mut PartState, hp: f32) -> bool {
    if !s.attached {
        return false;
    }
    s.hp = (s.hp + hp).min(d.hp);
    if s.hp >= d.hp {
        s.overkill = 0.0;
        return true;
    }
    false
}

#[derive(Debug, Clone, Deserialize)]
pub struct PilotData {
    /// Unbuckled: impacts above this hurt you, this much per m/s over.
    pub unbuckled_safe_mps: f32,
    pub unbuckled_damage_per_mps: f32,
    /// Buckled in: much harder to hurt.
    pub buckled_safe_mps: f32,
    pub buckled_damage_per_mps: f32,
}

/// Live state of one part.
#[derive(Debug, Clone, PartialEq)]
pub struct PartState {
    pub hp: f32,
    /// Damage taken past zero; reaching the part's detach_overkill breaks it off.
    pub overkill: f32,
    pub attached: bool,
}

impl PartState {
    pub fn new(d: &PartData) -> Self {
        Self { hp: d.hp, overkill: 0.0, attached: true }
    }

    pub fn fraction(&self, d: &PartData) -> f32 {
        if !self.attached || d.hp <= 0.0 { 0.0 } else { (self.hp / d.hp).clamp(0.0, 1.0) }
    }
}

/// Does the ship-space point belong to this part's slice?
pub fn part_contains(d: &PartData, p: Vector3) -> bool {
    if p.y < d.y.0 || p.y > d.y.1 {
        return false;
    }
    let r = Vector2::new(p.x, p.z).length();
    if r < d.r.0 || r > d.r.1 {
        return false;
    }
    if d.half_width_deg >= 180.0 {
        return true;
    }
    let a = p.z.atan2(p.x).to_degrees();
    let diff = (a - d.angle_deg + 540.0).rem_euclid(360.0) - 180.0;
    diff.abs() <= d.half_width_deg
}

/// Index of the first part whose slice holds `p`, if any.
pub fn part_at(parts: &[PartData], p: Vector3) -> Option<usize> {
    parts.iter().position(|d| part_contains(d, p))
}

/// Rough centre of a part's slice (ship space), for distances and sparks.
pub fn part_centre(d: &PartData) -> Vector3 {
    let r = if d.half_width_deg >= 180.0 { 0.0 } else { (d.r.0 + d.r.1) * 0.5 };
    let a = d.angle_deg.to_radians();
    Vector3::new(r * a.cos(), (d.y.0 + d.y.1) * 0.5, r * a.sin())
}

/// What one impact did.
#[derive(Debug, Clone, Default, PartialEq)]
pub struct ImpactResult {
    /// (part index, damage dealt).
    pub hits: Vec<(usize, f32)>,
    pub hull_damage: f32,
    /// Parts that broke off this impact.
    pub detached: Vec<usize>,
}

/// Apply an impact of `speed` m/s at ship-space `at` to the parts.
pub fn apply_impact(d: &DamageData, states: &mut [PartState], hull_hp: &mut f32, at: Vector3, speed: f32) -> ImpactResult {
    let mut out = ImpactResult::default();
    let total = (speed - d.safe_impact_mps).max(0.0) * d.damage_per_mps;
    if total <= 0.0 {
        return out;
    }
    let mut nearest = 0.0f32;
    for (i, (pd, st)) in d.parts.iter().zip(states.iter_mut()).enumerate() {
        if !st.attached {
            continue;
        }
        let dist = if part_contains(pd, at) { 0.0 } else { (part_centre(pd) - at).length() };
        let share = (1.0 - dist / d.spread_m.max(0.01)).clamp(0.0, 1.0);
        if share <= 0.0 {
            continue;
        }
        nearest = nearest.max(share);
        let dmg = total * share;
        out.hits.push((i, dmg));
        let left = st.hp - dmg;
        if left < 0.0 {
            st.overkill += (-left).min(dmg);
            st.hp = 0.0;
        } else {
            st.hp = left;
        }
        if pd.detach_overkill > 0.0 && st.overkill >= pd.detach_overkill {
            st.attached = false;
            out.detached.push(i);
        }
    }
    // A hit right on a part spares the hull most of it; a quarter always
    // reaches the hull core.
    let hull = total * (1.0 - nearest).max(0.25);
    out.hull_damage = hull.min(*hull_hp);
    *hull_hp = (*hull_hp - hull).max(0.0);
    out
}

/// Damage to the pilot from an impact (0 when it's under their safe speed).
pub fn pilot_damage(p: &PilotData, speed: f32, buckled: bool) -> f32 {
    let (safe, per) = if buckled { (p.buckled_safe_mps, p.buckled_damage_per_mps) } else { (p.unbuckled_safe_mps, p.unbuckled_damage_per_mps) };
    (speed - safe).max(0.0) * per
}

/// How well the ship still flies: (main engine 0..1, thrusters 0..1).
/// Thrusters average over every thruster part; a missing one counts as 0.
pub fn performance(d: &DamageData, states: &[PartState]) -> (f32, f32) {
    let mut engine = 1.0f32;
    let (mut thr_sum, mut thr_n) = (0.0f32, 0);
    for (pd, st) in d.parts.iter().zip(states) {
        match pd.kind {
            PartKind::Engine => engine = engine.min(working(st, pd)),
            PartKind::Thrusters => {
                thr_sum += working(st, pd);
                thr_n += 1;
            }
            _ => {}
        }
    }
    (engine, if thr_n == 0 { 1.0 } else { thr_sum / thr_n as f32 })
}

/// A damaged part keeps some function until it's gone: half power at
/// zero health, full at full health; broken off is nothing.
fn working(st: &PartState, d: &PartData) -> f32 {
    if !st.attached { 0.0 } else { 0.5 + 0.5 * st.fraction(d) }
}

pub fn v3((x, y, z): V3) -> Vector3 {
    Vector3::new(x, y, z)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn data() -> DamageData {
        let ship: crate::flight::ship::ShipData =
            crate::data::parse(include_str!("../../../../godot/data/ships/starter_lander.ron")).unwrap();
        ship.damage
    }

    fn fresh(d: &DamageData) -> Vec<PartState> {
        d.parts.iter().map(PartState::new).collect()
    }

    #[test]
    fn slices_pick_the_right_part() {
        let d = data();
        // A footpad straight out along +X sits in a leg.
        let i = part_at(&d.parts, Vector3::new(6.0, 0.3, 0.0)).expect("a part");
        assert_eq!(d.parts[i].kind, PartKind::Leg);
        // Out along +Z is a different leg.
        let j = part_at(&d.parts, Vector3::new(0.0, 0.3, 6.0)).expect("a part");
        assert_ne!(i, j);
        assert_eq!(d.parts[j].kind, PartKind::Leg);
        // The middle of the cabin is hull, no part.
        assert_eq!(part_at(&d.parts, Vector3::new(0.0, 8.0, 0.0)), None);
    }

    #[test]
    fn gentle_landings_do_nothing() {
        let d = data();
        let mut s = fresh(&d);
        let mut hull = d.hull_hp;
        let r = apply_impact(&d, &mut s, &mut hull, Vector3::new(6.0, 0.3, 0.0), d.safe_impact_mps * 0.9);
        assert!(r.hits.is_empty() && r.detached.is_empty());
        assert_eq!(hull, d.hull_hp);
    }

    #[test]
    fn hard_hits_damage_then_break_off_the_part_hit() {
        let d = data();
        let mut s = fresh(&d);
        let mut hull = d.hull_hp;
        let foot = Vector3::new(6.0, 0.3, 0.0);
        let leg = part_at(&d.parts, foot).unwrap();
        let r = apply_impact(&d, &mut s, &mut hull, foot, d.safe_impact_mps + 4.0);
        assert!(s[leg].hp < d.parts[leg].hp && s[leg].attached, "first hit dents it");
        assert!(r.detached.is_empty());
        assert!(hull < d.hull_hp);
        let mut broke = false;
        for _ in 0..20 {
            let r = apply_impact(&d, &mut s, &mut hull, foot, d.safe_impact_mps + 12.0);
            if r.detached.contains(&leg) {
                broke = true;
                break;
            }
        }
        assert!(broke && !s[leg].attached, "repeated big hits rip the leg off");
        // The opposite leg is too far away to be touched.
        let far = part_at(&d.parts, Vector3::new(-6.0, 0.3, 0.0)).unwrap();
        assert!(s[far].attached && s[far].hp == d.parts[far].hp);
    }

    #[test]
    fn buckled_pilots_survive_what_kills_unbuckled_ones() {
        let d = data();
        let speed = d.pilot.unbuckled_safe_mps + 10.0;
        assert!(pilot_damage(&d.pilot, speed, false) > 0.0);
        assert!(pilot_damage(&d.pilot, speed, false) > pilot_damage(&d.pilot, speed, true));
        assert_eq!(pilot_damage(&d.pilot, d.pilot.unbuckled_safe_mps - 0.1, false), 0.0);
    }

    #[test]
    fn repairs_heal_damaged_parts_but_not_lost_ones() {
        let d = data();
        let mut s = fresh(&d);
        s[0].hp = 10.0;
        s[0].overkill = 0.0;
        assert!(!repair_part(&d.parts[0], &mut s[0], 5.0));
        assert_eq!(s[0].hp, 15.0);
        assert!(repair_part(&d.parts[0], &mut s[0], 1000.0));
        assert_eq!(s[0].hp, d.parts[0].hp);
        s[1].attached = false;
        assert!(!repair_part(&d.parts[1], &mut s[1], 1000.0));
    }

    #[test]
    fn nearest_point_stays_on_the_part() {
        let d = data();
        let leg = &d.parts[0];
        let p = part_nearest_point(leg, Vector3::new(20.0, 50.0, 3.0));
        assert!((p.y - leg.y.1).abs() < 1e-3, "clamped to the top of the leg");
        assert!((Vector2::new(p.x, p.z).length() - leg.r.1).abs() < 1e-3, "clamped to its outer radius");
        let inside = Vector3::new(6.0, 1.0, 0.0);
        assert!((part_nearest_point(leg, inside) - inside).length() < 1e-3);
    }

    #[test]
    fn a_lost_engine_stops_the_main_engine() {
        let d = data();
        let mut s = fresh(&d);
        assert_eq!(performance(&d, &s), (1.0, 1.0));
        let e = d.parts.iter().position(|p| p.kind == PartKind::Engine).unwrap();
        s[e].hp = 0.0;
        assert_eq!(performance(&d, &s).0, 0.5);
        s[e].attached = false;
        assert_eq!(performance(&d, &s).0, 0.0);
    }
}
