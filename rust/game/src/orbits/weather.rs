//! Weather: each body with air has a seeded timeline of calm spells and
//! weather events (dust storms, rain, snow, thunderstorms, acid storms,
//! haze, dust devils), plus a slowly turning wind (CLAUDE.md section 4:
//! "Weather per planet: storms, rain, sandstorms, wind, lightning in
//! different colours"; Mars kit: sandstorms and dust devils).
//!
//! Serves pillar 1 (a living system that changes whether you watch or not)
//! and fun rule 7 (sensation: easy fun in a storm, hard fun landing in one).
//! Fair warning (fun rule 8): every event ramps up over `ramp_s` seconds, so
//! the sky thickens and the wind rises before a storm is at full strength.
//!
//! This file is the pure, unit-tested part: data and the timeline. The
//! node that draws it is `weather_node.rs`.

use godot::builtin::Vector3;
use serde::Deserialize;

use crate::procgen::noise;
use crate::seed::SeededRng;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Deserialize)]
pub enum WeatherKind {
    /// Thick blowing dust, low visibility, strong wind.
    Sandstorm,
    /// Falling rain streaks.
    Rain,
    /// Slow drifting flakes.
    Snow,
    /// Rain with lightning.
    Thunderstorm,
    /// Corrosive rain that eats ship hulls (Venus).
    AcidStorm,
    /// Thick still haze (Titan).
    Haze,
    /// Whirling dust columns that wander over the plains and shove you.
    DustDevils,
    /// Under water: drifting glowing plankton and purple flashes in the dark.
    Bioluminescence,
}

#[derive(Debug, Clone, Deserialize)]
pub struct WeatherData {
    /// Weather reaches this high above the ground, metres.
    pub ceiling_m: f32,
    /// Calm spell length range between events, seconds.
    pub calm_s: (f32, f32),
    /// Everyday breeze, m/s.
    pub wind_m_s: f32,
    /// Wind push on a person: m/s^2 per m/s of wind (ships are heavier).
    pub push_per_wind: f32,
    /// Share of that push felt while standing on the ground (feet grip).
    pub ground_share: f32,
    /// Haze colour and density in calm air (0 = clear).
    pub calm_fog: (f32, f32, f32),
    pub calm_fog_density: f32,
    /// Share of sunlight lost even in calm air (thick skies, gas giant
    /// interiors under the cloud deck).
    #[serde(default)]
    pub calm_sun_dim: f32,
    pub events: Vec<WeatherEvent>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct WeatherEvent {
    pub kind: WeatherKind,
    /// Relative chance of this event when a calm spell ends.
    pub weight: f32,
    pub duration_s: (f32, f32),
    /// Seconds to build up at the start and die away at the end.
    pub ramp_s: f32,
    /// Wind at full strength, m/s.
    pub wind_m_s: f32,
    pub fog: (f32, f32, f32),
    /// Fog density at full strength (Godot exponential fog).
    pub fog_density: f32,
    /// Particles (dust, drops, flakes) around you.
    pub particle_color: (f32, f32, f32),
    pub particles_per_s: f32,
    /// Lightning bolts: colour, and seconds between strikes at full strength.
    #[serde(default)]
    pub lightning: Option<Lightning>,
    /// Hull damage per second to a ship sitting in the event at full strength.
    #[serde(default)]
    pub corrosion_hp_s: f32,
    /// Corrosion never takes the hull below this share of full health.
    #[serde(default)]
    pub corrosion_floor: f32,
    /// Dust devil columns around you (DustDevils only).
    #[serde(default)]
    pub devils: u32,
    /// Share of sunlight the clouds block at full strength (0..1).
    #[serde(default)]
    pub sun_dim: f32,
    /// Only seen and felt under water (and never above it).
    #[serde(default)]
    pub underwater: bool,
}

#[derive(Debug, Clone, Deserialize)]
pub struct Lightning {
    pub color: (f32, f32, f32),
    pub every_s: (f32, f32),
}

/// One scheduled event: which, when it starts and how long it lasts.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct Spell {
    pub event: usize,
    pub start: f64,
    pub duration: f64,
}

/// What the weather is doing at one moment.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct WeatherState {
    /// The active event (index into `events`), if any.
    pub event: Option<usize>,
    /// 0..1 strength of that event (ramping in and out).
    pub strength: f32,
    /// Seconds until the next event starts (0 while one is running).
    pub next_in: f64,
}

/// A body's seeded weather timeline for the whole run.
#[derive(Debug, Clone)]
pub struct WeatherSchedule {
    pub spells: Vec<Spell>,
    seed: u32,
}

/// The timeline covers this many seconds (8 antes of 12 minutes, plus
/// Endless); after it the pattern repeats.
const TIMELINE_S: f64 = 4.0 * 3600.0;

impl WeatherSchedule {
    pub fn new(data: &WeatherData, run_seed: u64, body: &str) -> Self {
        let mut rng = SeededRng::derive(run_seed, &format!("weather/{body}"));
        let total: f32 = data.events.iter().map(|e| e.weight.max(0.0)).sum();
        let mut spells = Vec::new();
        let mut t = 0.0f64;
        while t < TIMELINE_S && total > 0.0 {
            t += rng.range_f32(data.calm_s.0, data.calm_s.1.max(data.calm_s.0)) as f64;
            let mut pick = rng.range_f32(0.0, total);
            let mut event = 0;
            for (i, e) in data.events.iter().enumerate() {
                pick -= e.weight.max(0.0);
                if pick <= 0.0 {
                    event = i;
                    break;
                }
            }
            let e = &data.events[event];
            let duration = rng.range_f32(e.duration_s.0, e.duration_s.1.max(e.duration_s.0)) as f64;
            spells.push(Spell { event, start: t, duration });
            t += duration;
        }
        Self { spells, seed: rng.next_u64() as u32 }
    }

    pub fn state_at(&self, data: &WeatherData, time: f64) -> WeatherState {
        let t = time.rem_euclid(TIMELINE_S);
        let idx = self.spells.partition_point(|s| s.start <= t);
        if idx > 0 {
            let s = self.spells[idx - 1];
            if t < s.start + s.duration {
                let ramp = data.events[s.event].ramp_s.max(0.01) as f64;
                let into = t - s.start;
                let left = s.start + s.duration - t;
                let strength = (into / ramp).min(left / ramp).min(1.0) as f32;
                return WeatherState { event: Some(s.event), strength: smooth(strength), next_in: 0.0 };
            }
        }
        let next_in = self.spells.get(idx).map_or(f64::MAX, |s| s.start - t);
        WeatherState { event: None, strength: 0.0, next_in }
    }

    /// Wind direction at `time`: a tangent at `up`, turning slowly.
    pub fn wind_dir(&self, up: Vector3, time: f64) -> Vector3 {
        let a = noise::fbm3([(time * 0.004) as f32, 1.7, 0.3], 2, 0.5, self.seed) * std::f32::consts::TAU * 1.5;
        let side = if up.x.abs() < 0.9 { Vector3::RIGHT } else { Vector3::FORWARD };
        let e1 = up.cross(side).normalized();
        let e2 = up.cross(e1);
        (e1 * a.cos() + e2 * a.sin()).normalized()
    }
}

/// Wind speed (m/s) at a weather state.
pub fn wind_speed(data: &WeatherData, s: &WeatherState) -> f32 {
    match s.event {
        Some(i) => data.wind_m_s + (data.events[i].wind_m_s - data.wind_m_s) * s.strength,
        None => data.wind_m_s,
    }
}

/// Fog colour and density at a weather state.
pub fn fog(data: &WeatherData, s: &WeatherState) -> ((f32, f32, f32), f32) {
    match s.event {
        Some(i) => {
            let e = &data.events[i];
            let k = s.strength;
            (
                lerp3(data.calm_fog, e.fog, k),
                data.calm_fog_density + (e.fog_density - data.calm_fog_density) * k,
            )
        }
        None => (data.calm_fog, data.calm_fog_density),
    }
}

pub fn lerp3(a: (f32, f32, f32), b: (f32, f32, f32), k: f32) -> (f32, f32, f32) {
    (a.0 + (b.0 - a.0) * k, a.1 + (b.1 - a.1) * k, a.2 + (b.2 - a.2) * k)
}

fn smooth(x: f32) -> f32 {
    let t = x.clamp(0.0, 1.0);
    t * t * (3.0 - 2.0 * t)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn data() -> WeatherData {
        let ev = |kind, weight| WeatherEvent {
            kind,
            weight,
            duration_s: (60.0, 90.0),
            ramp_s: 15.0,
            wind_m_s: 14.0,
            fog: (0.8, 0.5, 0.3),
            fog_density: 0.02,
            particle_color: (0.8, 0.5, 0.3),
            particles_per_s: 400.0,
            lightning: None,
            corrosion_hp_s: 0.0,
            corrosion_floor: 0.0,
            devils: 0,
            sun_dim: 0.0,
            underwater: false,
        };
        WeatherData {
            ceiling_m: 120.0,
            calm_s: (60.0, 120.0),
            wind_m_s: 2.0,
            push_per_wind: 0.08,
            ground_share: 0.3,
            calm_fog: (0.8, 0.6, 0.5),
            calm_fog_density: 0.002,
            calm_sun_dim: 0.0,
            events: vec![ev(WeatherKind::Sandstorm, 3.0), ev(WeatherKind::DustDevils, 1.0)],
        }
    }

    #[test]
    fn determinism_weather_is_seeded_per_run_and_body() {
        let d = data();
        let a = WeatherSchedule::new(&d, 5, "mars");
        let b = WeatherSchedule::new(&d, 5, "mars");
        let c = WeatherSchedule::new(&d, 6, "mars");
        let e = WeatherSchedule::new(&d, 5, "earth");
        assert_eq!(a.spells, b.spells);
        assert_ne!(a.spells, c.spells);
        assert_ne!(a.spells, e.spells);
    }

    #[test]
    fn events_ramp_in_and_out_with_calm_between() {
        let d = data();
        let s = WeatherSchedule::new(&d, 5, "mars");
        let first = s.spells[0];
        assert!(first.start >= 60.0, "the run opens calm");
        let calm = s.state_at(&d, first.start - 1.0);
        assert_eq!(calm.event, None);
        assert!((calm.next_in - 1.0).abs() < 1e-6);
        // A warning ramp: weak at first, full in the middle, fading at the end.
        let early = s.state_at(&d, first.start + 3.0);
        let mid = s.state_at(&d, first.start + first.duration * 0.5);
        let late = s.state_at(&d, first.start + first.duration - 2.0);
        assert!(early.strength < 0.3 && early.strength > 0.0);
        assert!((mid.strength - 1.0).abs() < 1e-6);
        assert!(late.strength < 0.2);
        assert!(wind_speed(&d, &mid) > wind_speed(&d, &calm) * 4.0);
        assert!(fog(&d, &mid).1 > fog(&d, &calm).1 * 5.0);
        // Calm spells separate every pair of events.
        for w in s.spells.windows(2) {
            assert!(w[1].start - (w[0].start + w[0].duration) >= 60.0 - 1e-6);
        }
    }

    #[test]
    fn wind_turns_slowly_and_stays_level() {
        let d = data();
        let s = WeatherSchedule::new(&d, 5, "mars");
        let up = Vector3::new(0.3, 0.9, 0.1).normalized();
        let a = s.wind_dir(up, 100.0);
        let b = s.wind_dir(up, 101.0);
        assert!(a.dot(up).abs() < 1e-4);
        assert!(a.angle_to(b) < 0.05, "wind swings {} rad in a second", a.angle_to(b));
    }
}
