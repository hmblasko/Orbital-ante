//! Automatic detectors run every physics tick during bot runs (section 19.2).
//! Pure logic, unit tested; the harness feeds it the player's state.
//!
//! Phase 1 covers stuck, out of world and physics explosion. Soft-lock,
//! unfair death and economy anomalies need objectives, damage and points,
//! so they arrive with those systems.

use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Deserialize)]
pub struct DetectorSettings {
    /// Flag "stuck" if the player moves less than `stuck_distance_m` in this
    /// many seconds while the bot is trying to move.
    pub stuck_seconds: f32,
    pub stuck_distance_m: f32,
    /// Below the surface by more than this counts as out of world.
    pub below_surface_margin_m: f32,
    /// Further than this from every body counts as out of world.
    pub kill_distance_m: f32,
    pub max_sane_speed: f32,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum DetectorKind {
    Stuck,
    OutOfWorld,
    PhysicsExplosion,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct DetectorHit {
    pub kind: DetectorKind,
    pub time_s: f32,
    pub position: (f32, f32, f32),
    pub detail: String,
    /// Ticks this detector fired in total; only the first is described.
    pub count: u32,
}

/// One tick of the player's state, in world space.
#[derive(Debug, Clone, Copy)]
pub struct Sample {
    pub time_s: f32,
    pub position: [f32; 3],
    pub velocity: [f32; 3],
    /// Nearest body centre and surface radius, if any body exists.
    pub nearest_body: Option<([f32; 3], f32)>,
    pub wants_to_move: bool,
}

pub struct Detectors {
    settings: DetectorSettings,
    hits: Vec<DetectorHit>,
    anchor: Option<([f32; 3], f32)>,
}

fn distance(a: [f32; 3], b: [f32; 3]) -> f32 {
    ((a[0] - b[0]).powi(2) + (a[1] - b[1]).powi(2) + (a[2] - b[2]).powi(2)).sqrt()
}

impl Detectors {
    pub fn new(settings: DetectorSettings) -> Self {
        Self { settings, hits: Vec::new(), anchor: None }
    }

    pub fn hits(&self) -> &[DetectorHit] {
        &self.hits
    }

    pub fn check(&mut self, s: Sample) {
        let finite = s.position.iter().chain(s.velocity.iter()).all(|v| v.is_finite());
        let speed = distance(s.velocity, [0.0; 3]);
        if !finite {
            self.hit(DetectorKind::PhysicsExplosion, &s, "position or velocity is NaN or infinite".into());
            return;
        }
        if speed > self.settings.max_sane_speed {
            self.hit(DetectorKind::PhysicsExplosion, &s, format!("speed {speed:.1} m/s"));
        }

        match s.nearest_body {
            Some((center, radius)) => {
                let r = distance(s.position, center);
                if r < radius - self.settings.below_surface_margin_m {
                    self.hit(DetectorKind::OutOfWorld, &s, format!("{:.2} m below the surface", radius - r));
                } else if r > self.settings.kill_distance_m {
                    self.hit(DetectorKind::OutOfWorld, &s, format!("{r:.0} m from the nearest body"));
                }
            }
            None => self.hit(DetectorKind::OutOfWorld, &s, "no bodies in the scene".into()),
        }

        if !s.wants_to_move {
            self.anchor = None;
            return;
        }
        match self.anchor {
            Some((pos, _)) if distance(pos, s.position) >= self.settings.stuck_distance_m => {
                self.anchor = Some((s.position, s.time_s));
            }
            Some((_, since)) if s.time_s - since >= self.settings.stuck_seconds => {
                self.hit(DetectorKind::Stuck, &s, format!("moved under {} m in {:.0} s", self.settings.stuck_distance_m, s.time_s - since));
                self.anchor = Some((s.position, s.time_s));
            }
            Some(_) => {}
            None => self.anchor = Some((s.position, s.time_s)),
        }
    }

    fn hit(&mut self, kind: DetectorKind, s: &Sample, detail: String) {
        if let Some(existing) = self.hits.iter_mut().find(|h| h.kind == kind) {
            existing.count += 1;
            return;
        }
        let [x, y, z] = s.position;
        self.hits.push(DetectorHit { kind, time_s: s.time_s, position: (x, y, z), detail, count: 1 });
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn settings() -> DetectorSettings {
        DetectorSettings {
            stuck_seconds: 30.0,
            stuck_distance_m: 1.0,
            below_surface_margin_m: 0.5,
            kill_distance_m: 1000.0,
            max_sane_speed: 200.0,
        }
    }

    fn sample(t: f32, pos: [f32; 3]) -> Sample {
        Sample { time_s: t, position: pos, velocity: [0.0; 3], nearest_body: Some(([0.0; 3], 60.0)), wants_to_move: true }
    }

    #[test]
    fn healthy_walk_has_no_hits() {
        let mut d = Detectors::new(settings());
        for i in 0..3600 {
            let t = i as f32 / 60.0;
            d.check(sample(t, [t.cos() * 61.0, t.sin() * 61.0, 0.0]));
        }
        assert!(d.hits().is_empty(), "{:?}", d.hits());
    }

    #[test]
    fn standing_still_while_trying_to_move_is_stuck() {
        let mut d = Detectors::new(settings());
        for i in 0..(31 * 60) {
            d.check(sample(i as f32 / 60.0, [0.0, 61.0, 0.0]));
        }
        assert_eq!(d.hits()[0].kind, DetectorKind::Stuck);
    }

    #[test]
    fn standing_still_on_purpose_is_fine() {
        let mut d = Detectors::new(settings());
        for i in 0..(40 * 60) {
            d.check(Sample { wants_to_move: false, ..sample(i as f32 / 60.0, [0.0, 61.0, 0.0]) });
        }
        assert!(d.hits().is_empty());
    }

    #[test]
    fn falling_through_the_ground_is_out_of_world() {
        let mut d = Detectors::new(settings());
        d.check(sample(0.0, [0.0, 50.0, 0.0]));
        assert_eq!(d.hits()[0].kind, DetectorKind::OutOfWorld);
    }

    #[test]
    fn nan_and_huge_speed_are_explosions_counted_once() {
        let mut d = Detectors::new(settings());
        d.check(Sample { velocity: [500.0, 0.0, 0.0], ..sample(0.0, [0.0, 61.0, 0.0]) });
        d.check(Sample { position: [f32::NAN, 0.0, 0.0], ..sample(0.1, [0.0, 61.0, 0.0]) });
        assert_eq!(d.hits().len(), 1);
        assert_eq!(d.hits()[0].kind, DetectorKind::PhysicsExplosion);
        assert_eq!(d.hits()[0].count, 2);
    }
}
