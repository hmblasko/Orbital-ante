//! Landforms: mountains, mesas, buttes, volcanoes, complex craters,
//! wrinkle ridges, dune fields, canyons, rivers, lakes, sinkholes and
//! spring pools shaped into a body's surface (CLAUDE.md section 4:
//! "caves, flyable canyons, large mountains ... mesas, hoodoos, cliffs ...
//! bodies of water of varied shape, menacing geography").
//!
//! Serves pillar 3 (every run teaches: landmarks to learn and navigate by)
//! and fun rule 4 (curiosity: distinct places worth flying to).
//!
//! Each landform changes the height function directly, so the mesh, the
//! collision and every placement query agree. Raised forms (mountains,
//! mesas, buttes, volcanoes, ridges, dunes, canyon plateaus) add height;
//! craters and sinkholes dig relative to the ground around them; cut forms
//! (canyons, rivers, lakes, pools) carve down to an absolute level tied to
//! the body's water table, so water always sits flat in them.

use godot::builtin::Vector3;
use serde::Deserialize;

use super::noise;
use crate::seed::SeededRng;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Deserialize)]
pub enum LandformKind {
    Mountain,
    Mesa,
    Volcano,
    Canyon,
    River,
    Lake,
    Pool,
    /// A narrow, tall remnant of a mesa: sheer cliffs, small caprock.
    Butte,
    /// A field of transverse sand dunes: gentle windward slopes, steep slip faces.
    Dunes,
    /// A complex impact crater: terraced walls, flat floor, central peak.
    Crater,
    /// A path-shaped wrinkle ridge (a long, lumpy compression ridge).
    Ridge,
    /// A steep collapse pit; floods into a cenote below the water table.
    Sinkhole,
}

#[derive(Debug, Clone, Deserialize, Default)]
pub struct LandformData {
    /// Water table relative to the nominal radius, in metres. Anything
    /// carved below it fills with water. None means a dry body.
    #[serde(default)]
    pub water_level_m: Option<f32>,
    /// Degrees around the landing zone (+Y) kept free of random landforms.
    #[serde(default)]
    pub clear_zone_deg: f32,
    /// Hand-placed landforms (for the scenes players see first).
    #[serde(default)]
    pub placed: Vec<PlacedLandform>,
    /// Seeded extras spread over the rest of the body.
    #[serde(default)]
    pub random: Vec<RandomLandforms>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct PlacedLandform {
    pub kind: LandformKind,
    /// Direction from the body's centre (need not be normalised).
    pub at: (f32, f32, f32),
    /// For canyons and rivers: where the path ends.
    #[serde(default)]
    pub to: Option<(f32, f32, f32)>,
    /// Footprint radius; for canyons and rivers, the half-width.
    pub radius_m: f32,
    /// Peak height; for canyons the plateau rise, for lakes and pools the depth.
    pub height_m: f32,
}

#[derive(Debug, Clone, Deserialize)]
pub struct RandomLandforms {
    pub kind: LandformKind,
    pub count: u32,
    pub radius_m: (f32, f32),
    pub height_m: (f32, f32),
    /// Path length range for canyons and rivers.
    #[serde(default)]
    pub length_m: (f32, f32),
}

/// One landform, precomputed for fast sampling.
#[derive(Debug, Clone)]
pub struct Landform {
    pub kind: LandformKind,
    pub center: Vector3,
    /// Footprint (or half-width) as an angle in radians.
    pub angle: f32,
    pub radius_m: f32,
    pub height: f32,
    /// Path features: start, end, plane normal and arc length (radians).
    start: Vector3,
    normal: Vector3,
    side: Vector3,
    arc: f32,
    seed: u32,
    /// cos of the angle beyond which this landform has no effect.
    reach_cos: f32,
}

/// Painting hints a landform leaves on the ground (read by the terrain shader).
#[derive(Debug, Clone, Copy, PartialEq, Default)]
pub struct LandHints {
    /// Layered rock bands (mesa and canyon walls).
    pub strata: f32,
    /// 0.3 = dark basalt, 1.0 = glowing lava.
    pub lava: f32,
    /// Pale cap on high peaks.
    pub snow: f32,
    /// Damp banks and lake shores.
    pub wet: f32,
    /// White mineral terraces around spring pools.
    pub mineral: f32,
}

impl LandHints {
    fn max(&mut self, o: LandHints) {
        self.strata = self.strata.max(o.strata);
        self.lava = self.lava.max(o.lava);
        self.snow = self.snow.max(o.snow);
        self.wet = self.wet.max(o.wet);
        self.mineral = self.mineral.max(o.mineral);
    }
}

/// How far (in multiples of the footprint) each kind reaches.
fn reach(kind: LandformKind) -> f32 {
    match kind {
        LandformKind::Mountain => 1.05,
        LandformKind::Mesa => 1.5,
        LandformKind::Volcano => 1.15,
        LandformKind::Lake => 2.0,
        LandformKind::Pool => 2.6,
        LandformKind::Butte => 1.5,
        LandformKind::Dunes => 1.25,
        LandformKind::Crater => 1.8,
        LandformKind::Sinkhole => 1.6,
        // Paths: half-width multiples, checked separately.
        LandformKind::Canyon => 5.0,
        LandformKind::River => 3.2,
        LandformKind::Ridge => 3.0,
    }
}

impl Landform {
    fn point(
        kind: LandformKind,
        center: Vector3,
        radius_m: f32,
        height: f32,
        body_radius: f32,
        seed: u32,
    ) -> Self {
        let angle = radius_m / body_radius;
        Self {
            kind,
            center: center.normalized(),
            angle,
            radius_m,
            height,
            start: Vector3::ZERO,
            normal: Vector3::ZERO,
            side: Vector3::ZERO,
            arc: 0.0,
            seed,
            reach_cos: (angle * reach(kind) * 1.3).min(std::f32::consts::PI).cos(),
        }
    }

    fn path(
        kind: LandformKind,
        from: Vector3,
        to: Vector3,
        half_width_m: f32,
        height: f32,
        body_radius: f32,
        seed: u32,
    ) -> Self {
        let (a, b) = (from.normalized(), to.normalized());
        let normal = a.cross(b).normalized();
        let arc = a.angle_to(b);
        let mut l = Self::point(
            kind,
            (a + b).normalized(),
            half_width_m,
            height,
            body_radius,
            seed,
        );
        l.start = a;
        l.normal = normal;
        l.side = normal.cross(a);
        l.arc = arc;
        // Whole path plus its meander and banks.
        l.reach_cos = (arc * 0.5 + l.angle * reach(kind) * 2.5)
            .min(std::f32::consts::PI)
            .cos();
        l
    }

    pub fn is_path(&self) -> bool {
        is_path_kind(self.kind)
    }

    /// Path end points (for rivers that run into lakes, and tests).
    pub fn path_ends(&self) -> Option<(Vector3, Vector3)> {
        if !self.is_path() {
            return None;
        }
        let end = Vector3::new(
            self.start.x * self.arc.cos() + self.side.x * self.arc.sin(),
            self.start.y * self.arc.cos() + self.side.y * self.arc.sin(),
            self.start.z * self.arc.cos() + self.side.z * self.arc.sin(),
        );
        Some((self.start, end))
    }
}

/// Every landform on one body, plus the shared water table.
#[derive(Debug, Clone, Default)]
pub struct Landforms {
    pub list: Vec<Landform>,
    pub water_level: Option<f32>,
}

impl Landforms {
    pub fn new(data: &LandformData, body_radius: f32, seed: u64) -> Self {
        let mut rng = SeededRng::derive(seed, "landforms");
        let mut list: Vec<Landform> = Vec::new();
        let mut k = 0u32;
        for p in &data.placed {
            k += 1;
            let s = (seed as u32)
                .wrapping_mul(2654435761)
                .wrapping_add(k * 7919);
            let at = v(p.at);
            let l = match (p.kind, p.to) {
                (k, Some(to)) if is_path_kind(k) => {
                    Landform::path(p.kind, at, v(to), p.radius_m, p.height_m, body_radius, s)
                }
                (k, None) if is_path_kind(k) => {
                    let to = at + perpendicular(at) * 0.6;
                    Landform::path(p.kind, at, to, p.radius_m, p.height_m, body_radius, s)
                }
                _ => Landform::point(p.kind, at, p.radius_m, p.height_m, body_radius, s),
            };
            list.push(l);
        }

        let clear_cos = data.clear_zone_deg.to_radians().cos();
        for group in &data.random {
            let mut placed = 0;
            let mut tries = 0;
            while placed < group.count && tries < group.count * 60 {
                tries += 1;
                k += 1;
                let s = (seed as u32)
                    .wrapping_mul(2654435761)
                    .wrapping_add(k * 7919);
                let dir = random_dir(&mut rng);
                let radius_m = rng.range_f32(group.radius_m.0, group.radius_m.1);
                let height = rng.range_f32(group.height_m.0, group.height_m.1);
                let l = if is_path_kind(group.kind) {
                    let length = rng.range_f32(
                        group.length_m.0.max(radius_m * 4.0),
                        group.length_m.1.max(radius_m * 6.0),
                    );
                    // Rivers prefer to drain into an existing lake.
                    let lake = list
                        .iter()
                        .filter(|l| l.kind == LandformKind::Lake)
                        .find(|l| {
                            group.kind == LandformKind::River
                                && l.center.angle_to(dir) * body_radius < length * 1.3
                        });
                    let to = match lake {
                        Some(l) => l.center,
                        None => rotate_toward(
                            dir,
                            perpendicular_random(dir, &mut rng),
                            length / body_radius,
                        ),
                    };
                    let from = if lake.is_some() {
                        rotate_toward(to, (dir - to).normalized(), length / body_radius)
                    } else {
                        dir
                    };
                    Landform::path(group.kind, from, to, radius_m, height, body_radius, s)
                } else {
                    Landform::point(group.kind, dir, radius_m, height, body_radius, s)
                };
                // Keep the landing zone open and point landforms apart.
                let footprint = l.angle * reach(l.kind);
                let near_landing = if l.is_path() {
                    let (a, b) = l.path_ends().unwrap_or((l.center, l.center));
                    [a, b, l.center]
                        .iter()
                        .any(|p| p.y > clear_cos.min(0.9999) - 0.02)
                } else {
                    (l.center.y).acos() < data.clear_zone_deg.to_radians() + footprint
                };
                if near_landing {
                    continue;
                }
                let crowded = !l.is_path()
                    && list.iter().filter(|o| !o.is_path()).any(|o| {
                        o.center.angle_to(l.center) < (footprint + o.angle * reach(o.kind)) * 0.85
                    });
                if crowded {
                    continue;
                }
                list.push(l);
                placed += 1;
            }
        }
        Self {
            list,
            water_level: data.water_level_m,
        }
    }

    /// Raised and carved landforms applied on top of the base height.
    pub fn apply(&self, d: Vector3, base_height: f32, body_radius: f32) -> (f32, LandHints) {
        let mut height = base_height;
        let mut hints = LandHints::default();
        let water = self.water_level.unwrap_or(-4.0);
        // Raised forms first, then carving, so canyons cut through mesas.
        for pass in 0..2 {
            for l in &self.list {
                let carve = matches!(
                    l.kind,
                    LandformKind::Canyon
                        | LandformKind::River
                        | LandformKind::Lake
                        | LandformKind::Pool
                );
                // Canyons both raise a plateau (pass 0) and carve (pass 1).
                let active = match pass {
                    0 => !carve || l.kind == LandformKind::Canyon,
                    _ => carve,
                };
                if !active || d.dot(l.center) < l.reach_cos {
                    continue;
                }
                let (h, hint) = sample_one(l, d, height, water, body_radius, pass == 0);
                height = h;
                hints.max(hint);
            }
        }
        (height, hints)
    }

    /// Deepest the ground can go because of carving (relative to nominal radius).
    pub fn lowest(&self) -> f32 {
        let water = self.water_level.unwrap_or(-4.0);
        self.list
            .iter()
            .map(|l| match l.kind {
                LandformKind::Lake | LandformKind::Pool => water - l.height - 0.6,
                LandformKind::River | LandformKind::Canyon => water - RIVER_DEPTH - 0.6,
                // Relative digs: the base ground can be at most a few metres low.
                LandformKind::Crater => -l.height * CRATER_DEPTH - 6.0,
                LandformKind::Sinkhole => -l.height * 1.25 - 6.0,
                _ => 0.0,
            })
            .fold(0.0, f32::min)
    }
}

/// Depth of a river channel below the water table, in metres.
const RIVER_DEPTH: f32 = 1.1;
/// A complex crater's floor sits this far below the plain, as a share of
/// its rim height (`height_m` is the rim; real complex craters have
/// shallow floors relative to their width).
const CRATER_DEPTH: f32 = 1.6;
/// Crest-to-crest dune spacing as a multiple of dune height.
const DUNE_SPACING: f32 = 6.0;

fn is_path_kind(kind: LandformKind) -> bool {
    matches!(
        kind,
        LandformKind::Canyon | LandformKind::River | LandformKind::Ridge
    )
}

fn sample_one(
    l: &Landform,
    d: Vector3,
    height: f32,
    water: f32,
    body_radius: f32,
    raise_pass: bool,
) -> (f32, LandHints) {
    let mut hints = LandHints::default();
    let p = [d.x, d.y, d.z];
    match l.kind {
        LandformKind::Mountain => {
            let x = outline(d, l, 0.25);
            if x >= 1.0 {
                return (height, hints);
            }
            let f = 1.6 / l.angle;
            let crags = noise::ridged3([p[0] * f, p[1] * f, p[2] * f], 3, 0.45, l.seed);
            let shape = smoothstep(1.0, 0.0, x).powf(1.4);
            let lift = l.height * shape * (0.7 + 0.35 * crags);
            hints.snow = smoothstep(0.55, 0.8, lift / l.height);
            (height + lift, hints)
        }
        LandformKind::Mesa => {
            let x = outline(d, l, 0.12);
            if x >= 1.5 {
                return (height, hints);
            }
            // Talus skirt, then a steep two-step cliff up to a flat caprock.
            // Cliffs span at least a few metres so the mesh (about 1.8 m
            // cells) draws them as walls, not a zig-zag of single cells.
            let w = (2.2 / l.radius_m).max(0.05);
            let skirt = smoothstep(1.3, 1.0, x) * 0.15;
            let lower = smoothstep(1.0, 1.0 - w, x) * 0.47;
            let upper = smoothstep(0.94 - w, 0.94 - 2.0 * w, x) * 0.38;
            let f = 4.0 / l.angle;
            let top_noise = noise::fbm3([p[0] * f, p[1] * f, p[2] * f], 2, 0.5, l.seed ^ 77) * 0.04;
            let lift = l.height * (skirt + lower + upper + top_noise * smoothstep(0.85, 0.6, x));
            hints.strata = smoothstep(1.5, 1.05, x)
                * (1.0 - smoothstep(0.94 - 2.0 * w, 0.9 - 2.5 * w, x) * 0.7);
            (height + lift, hints)
        }
        LandformKind::Volcano => {
            let x = outline(d, l, 0.12);
            if x >= 1.15 {
                return (height, hints);
            }
            let caldera = 0.2;
            let cone = |x: f32| (1.0 - x.min(1.0)).powf(1.35);
            let lift = if x > caldera {
                l.height * cone(x)
            } else {
                // Bowl sinking from the rim to a flat lava lake.
                let rim = l.height * cone(caldera);
                rim - l.height * 0.28 * smoothstep(caldera, caldera * 0.55, x)
            };
            // Lava streaks running down the flanks.
            let streak_f = 9.0 / l.angle;
            let around = noise::gradient3(
                p[0] * streak_f,
                p[1] * streak_f,
                p[2] * streak_f,
                l.seed ^ 33,
            );
            let streak = smoothstep(0.2, 0.34, around)
                * smoothstep(0.85, 0.3, x)
                * (x > caldera) as i32 as f32;
            let basalt = 0.3 * smoothstep(1.15, 0.7, x);
            let pool = smoothstep(caldera * 0.75, caldera * 0.55, x);
            hints.lava = basalt.max(streak * 0.95).max(pool);
            (height + lift, hints)
        }
        LandformKind::Canyon | LandformKind::River => {
            let Some((q, along)) = path_coords(l, d, body_radius) else {
                return (height, hints);
            };
            if l.kind == LandformKind::Canyon && raise_pass {
                // The plateau the canyon cuts through (like the Colorado Plateau).
                let lift = l.height * smoothstep(5.0, 1.6, q) * along;
                return (height + lift, hints);
            }
            let floor = water + 0.6;
            let profile = if l.kind == LandformKind::Canyon {
                // Flat floor, a river channel down the middle, stepped walls.
                let channel = (water - RIVER_DEPTH)
                    + (floor - (water - RIVER_DEPTH)) * smoothstep(0.16, 0.36, q);
                let walls = floor
                    + 12.0 * terrace(smoothstep(0.36, 1.5, q), 3.0)
                    + 20.0 * smoothstep(1.5, 2.6, q);
                if q < 0.36 { channel } else { walls }
            } else {
                (water - RIVER_DEPTH)
                    + (RIVER_DEPTH + 0.5) * smoothstep(0.25, 1.0, q)
                    + 6.0 * smoothstep(1.0, 3.2, q)
            };
            // Narrow to nothing at the ends.
            let profile = profile + (1.0 - along) * 60.0;
            hints.wet = smoothstep(
                if l.kind == LandformKind::Canyon {
                    0.35
                } else {
                    1.8
                },
                0.1,
                q,
            ) * along;
            if l.kind == LandformKind::Canyon {
                hints.strata = smoothstep(2.2, 0.35, q) * along;
            }
            (smooth_min(height, profile, 0.8), hints)
        }
        LandformKind::Lake => {
            let x = outline(d, l, 0.3);
            if x >= 2.0 {
                return (height, hints);
            }
            let profile = (water - l.height * (1.0 - x * x).max(0.0))
                + 0.5 * smoothstep(0.9, 1.15, x)
                + 5.0 * smoothstep(1.15, 2.0, x);
            hints.wet = smoothstep(1.35, 0.95, x);
            (smooth_min(height, profile, 0.8), hints)
        }
        LandformKind::Pool => {
            let x = d.angle_to(l.center) / l.angle;
            if x >= 2.6 {
                return (height, hints);
            }
            // A round spring pool ringed by stepped mineral terraces.
            let basin = water - l.height * (1.0 - (x * x).min(1.0));
            let terraces = water
                + 0.35
                + 1.4 * terrace(smoothstep(1.0, 2.5, x), 4.0)
                + 8.0 * smoothstep(2.3, 2.6, x);
            let profile = if x < 1.0 { basin } else { terraces };
            hints.mineral = smoothstep(2.5, 1.9, x);
            (smooth_min(height, profile, 0.3), hints)
        }
        LandformKind::Butte => {
            let x = outline(d, l, 0.18);
            if x >= 1.5 {
                return (height, hints);
            }
            // Wide talus apron, then one sheer cliff to a small domed cap.
            let w = (3.0 / l.radius_m).max(0.06);
            let apron = smoothstep(1.5, 0.75, x).powf(1.6) * 0.3;
            let cliff = smoothstep(0.62, 0.62 - w, x) * 0.62;
            let cap = smoothstep(0.55, 0.0, x) * 0.08;
            let f = 2.4 / l.angle;
            let gullies = noise::ridged3([p[0] * f, p[1] * f, p[2] * f], 2, 0.5, l.seed ^ 91);
            let lift = l.height * (apron * (0.75 + 0.4 * gullies) + cliff + cap);
            hints.strata = smoothstep(1.3, 0.7, x) * (1.0 - smoothstep(0.6 - w, 0.5 - w, x) * 0.75);
            (height + lift, hints)
        }
        LandformKind::Dunes => {
            let x = outline(d, l, 0.35);
            if x >= 1.25 {
                return (height, hints);
            }
            // Distance across the wind in metres; crests wander with noise.
            let wind = perpendicular_seeded(l.center, l.seed);
            let f = 0.9 / l.angle;
            let wander = noise::fbm3([p[0] * f, p[1] * f, p[2] * f], 2, 0.5, l.seed ^ 13);
            let spacing = (l.height * DUNE_SPACING).max(6.0);
            let u = d.dot(wind) * body_radius / spacing + wander * 0.7;
            let t = u - u.floor();
            // Long, concave windward ramp to a sharp crest, then a short
            // slip face at about the angle of repose.
            let crest = if t < 0.78 {
                (t / 0.78).powf(1.6)
            } else {
                smoothstep(1.0, 0.78, t)
            };
            let fade = smoothstep(1.25, 0.6, x);
            let size = 0.8 + 0.25 * noise::fbm3([p[0] * f * 2.0, p[1] * f * 2.0, p[2] * f * 2.0], 2, 0.5, l.seed ^ 29);
            // Sand buries the hills under it, leaving a gentle swell.
            let buried = height * (1.0 - 0.6 * fade);
            let swell = l.height * 0.2 * fade;
            (buried + swell + l.height * crest * fade * size, hints)
        }
        LandformKind::Crater => {
            let x = outline(d, l, 0.06);
            if x >= 1.8 {
                return (height, hints);
            }
            let depth = l.height * CRATER_DEPTH;
            let profile = if x < 0.55 {
                // Flat floor with a central peak cluster that tops out above
                // the plain, so it stands as an island if the crater floods.
                let f = 3.0 / l.angle;
                let crags = noise::ridged3([p[0] * f, p[1] * f, p[2] * f], 2, 0.5, l.seed ^ 57);
                let peak = smoothstep(0.2, 0.0, x).powf(1.3) * (0.75 + 0.35 * crags);
                -depth + l.height * (CRATER_DEPTH + 0.4) * peak
            } else if x < 1.0 {
                // Slumped terraces climbing to the rim.
                let t = terrace(smoothstep(0.55, 1.0, x), 3.0);
                -depth + (depth + l.height) * t
            } else {
                // Ejecta blanket falling away from the raised rim.
                l.height * (1.0 - smoothstep(1.0, 1.8, x)).powf(2.2)
            };
            hints.strata = smoothstep(0.55, 0.7, x) * smoothstep(1.02, 0.92, x) * 0.6;
            (height + profile, hints)
        }
        LandformKind::Ridge => {
            let Some((q, along)) = path_coords(l, d, body_radius) else {
                return (height, hints);
            };
            if !raise_pass || q >= 3.0 {
                return (height, hints);
            }
            // A broad arch with a narrow, lumpy crest on one side.
            let f = 1.4 / l.angle;
            let lumps = noise::ridged3([p[0] * f, p[1] * f, p[2] * f], 3, 0.5, l.seed ^ 71);
            let arch = smoothstep(3.0, 0.0, q).powf(1.5) * 0.55;
            let crest = smoothstep(1.0, 0.0, q).powf(2.0) * 0.45 * (0.6 + 0.6 * lumps);
            (height + l.height * (arch + crest) * along, hints)
        }
        LandformKind::Sinkhole => {
            let x = outline(d, l, 0.15);
            if x >= 1.6 {
                return (height, hints);
            }
            // Sheer walls to a rubble floor, with a slumped, cracked lip.
            let wall = smoothstep(1.0, 0.86, x);
            let floor = smoothstep(0.7, 0.0, x) * 0.12;
            let lip = smoothstep(1.6, 1.0, x) * 0.08;
            let dig = l.height * (wall + floor + lip);
            hints.strata = smoothstep(1.02, 0.95, x) * smoothstep(0.7, 0.85, x);
            hints.wet = smoothstep(0.9, 0.6, x) * 0.5;
            (height - dig, hints)
        }
    }
}

/// A seeded tangent direction at `c` (for dune wind and the like).
fn perpendicular_seeded(c: Vector3, seed: u32) -> Vector3 {
    let a = perpendicular(c);
    let b = c.cross(a);
    let t = (seed % 6283) as f32 * 0.001;
    (a * t.cos() + b * t.sin()).normalized()
}

/// Distance from a path in half-widths (with a meander), and an end fade.
fn path_coords(l: &Landform, d: Vector3, body_radius: f32) -> Option<(f32, f32)> {
    let s = d.dot(l.normal).clamp(-1.0, 1.0).asin();
    let flat = d - l.normal * d.dot(l.normal);
    let t = flat.dot(l.side).atan2(flat.dot(l.start));
    let margin = l.angle * 3.0;
    if t < -margin || t > l.arc + margin || s.abs() > l.angle * 10.0 {
        return None;
    }
    // Meander: slow sine bends plus noise, scaled to the width.
    let along_m = t * body_radius;
    let phase = (l.seed % 628) as f32 * 0.01;
    let bends = (along_m / (l.radius_m * 4.5) + phase).sin() * 2.6
        + (along_m / (l.radius_m * 1.9) + phase * 2.0).sin() * 0.8;
    // Canyons wander in long, smooth bends; rivers wiggle more.
    let (f, amp) = if l.kind == LandformKind::River { (1.2 / l.angle, 2.0) } else { (0.4 / l.angle, 1.2) };
    let wobble = noise::fbm3([d.x * f, d.y * f, d.z * f], 3, 0.5, l.seed ^ 5) * amp;
    let edge_t = (t / l.arc).clamp(-0.5, 1.5);
    let along = smoothstep(-0.04, 0.12, edge_t) * smoothstep(1.04, 0.88, edge_t);
    let offset = (bends + wobble) * l.angle * along;
    let q = (s - offset).abs() / l.angle / along.max(0.15).sqrt();
    Some((q, along))
}

/// Normalised distance from the centre with a lumpy outline.
fn outline(d: Vector3, l: &Landform, lumpiness: f32) -> f32 {
    let x = d.angle_to(l.center) / l.angle;
    let f = 1.8 / l.angle;
    let n = noise::fbm3([d.x * f, d.y * f, d.z * f], 3, 0.5, l.seed ^ 11);
    x * (1.0 + n * lumpiness)
}

/// Flat benches separated by steep risers.
fn terrace(v: f32, steps: f32) -> f32 {
    let f = v * steps;
    let i = f.floor();
    (i + smoothstep(0.45, 1.0, f - i)) / steps
}

/// Polynomial smooth minimum (Quilez): blends cut edges so banks aren't creased.
fn smooth_min(a: f32, b: f32, k: f32) -> f32 {
    let h = (k - (a - b).abs()).max(0.0) / k;
    a.min(b) - h * h * k * 0.25
}

fn smoothstep(e0: f32, e1: f32, x: f32) -> f32 {
    let t = ((x - e0) / (e1 - e0)).clamp(0.0, 1.0);
    t * t * (3.0 - 2.0 * t)
}

fn v((x, y, z): (f32, f32, f32)) -> Vector3 {
    Vector3::new(x, y, z).normalized()
}

fn random_dir(rng: &mut SeededRng) -> Vector3 {
    let y = rng.range_f32(-1.0, 1.0);
    let theta = rng.range_f32(0.0, std::f32::consts::TAU);
    let ring = (1.0 - y * y).sqrt();
    Vector3::new(ring * theta.cos(), y, ring * theta.sin())
}

fn perpendicular(d: Vector3) -> Vector3 {
    let r = if d.x.abs() < 0.9 {
        Vector3::RIGHT
    } else {
        Vector3::FORWARD
    };
    d.cross(r).normalized()
}

fn perpendicular_random(d: Vector3, rng: &mut SeededRng) -> Vector3 {
    let a = perpendicular(d);
    let b = d.cross(a);
    let t = rng.range_f32(0.0, std::f32::consts::TAU);
    (a * t.cos() + b * t.sin()).normalized()
}

/// Move `angle` radians from `from` along the tangent `dir`.
fn rotate_toward(from: Vector3, dir: Vector3, angle: f32) -> Vector3 {
    let tangent = (dir - from * dir.dot(from)).normalized();
    (from * angle.cos() + tangent * angle.sin()).normalized()
}

#[cfg(test)]
mod tests {
    use super::*;

    fn data() -> LandformData {
        LandformData {
            water_level_m: Some(-3.0),
            clear_zone_deg: 10.0,
            placed: vec![
                PlacedLandform {
                    kind: LandformKind::Mesa,
                    at: (0.0, 0.9, -0.4),
                    to: None,
                    radius_m: 20.0,
                    height_m: 14.0,
                },
                PlacedLandform {
                    kind: LandformKind::Lake,
                    at: (0.5, 0.8, 0.3),
                    to: None,
                    radius_m: 16.0,
                    height_m: 3.0,
                },
                PlacedLandform {
                    kind: LandformKind::Canyon,
                    at: (-0.7, 0.6, 0.3),
                    to: Some((-0.3, 0.6, -0.7)),
                    radius_m: 6.0,
                    height_m: 8.0,
                },
            ],
            random: vec![
                RandomLandforms {
                    kind: LandformKind::Volcano,
                    count: 2,
                    radius_m: (30.0, 40.0),
                    height_m: (20.0, 28.0),
                    length_m: (0.0, 0.0),
                },
                RandomLandforms {
                    kind: LandformKind::River,
                    count: 2,
                    radius_m: (2.5, 3.5),
                    height_m: (0.0, 0.0),
                    length_m: (60.0, 120.0),
                },
            ],
        }
    }

    #[test]
    fn determinism_landforms_are_seeded() {
        let a = Landforms::new(&data(), 150.0, 9);
        let b = Landforms::new(&data(), 150.0, 9);
        let c = Landforms::new(&data(), 150.0, 10);
        let centers = |l: &Landforms| l.list.iter().map(|f| f.center).collect::<Vec<_>>();
        assert_eq!(centers(&a), centers(&b));
        assert_ne!(centers(&a), centers(&c));
        assert_eq!(a.list.len(), 7);
    }

    #[test]
    fn mesa_rises_and_lake_sits_under_the_water_table() {
        let l = Landforms::new(&data(), 150.0, 9);
        let (mesa_top, hints) = l.apply(l.list[0].center, 0.0, 150.0);
        assert!(mesa_top > 10.0, "mesa top {mesa_top}");
        assert!(hints.strata < 0.5, "caprock is not banded");
        let (lake_floor, hints) = l.apply(l.list[1].center, 0.0, 150.0);
        assert!(lake_floor < -5.0, "lake floor {lake_floor}");
        assert!(lake_floor >= l.lowest());
        assert!(hints.wet > 0.9);
    }

    #[test]
    fn canyon_floor_is_below_its_rim() {
        let l = Landforms::new(&data(), 150.0, 9);
        let canyon = &l.list[2];
        let (a, b) = canyon.path_ends().unwrap();
        // Sample across the canyon at its middle and take the lowest point.
        let mid = (a + b).normalized();
        let across = canyon.normal;
        let samples: Vec<f32> = (-80..=80)
            .map(|i| {
                l.apply((mid + across * (i as f32 * 0.004)).normalized(), 0.0, 150.0)
                    .0
            })
            .collect();
        let floor = samples.iter().copied().fold(f32::MAX, f32::min);
        let rim = samples.iter().copied().fold(f32::MIN, f32::max);
        assert!(
            rim - floor > 6.0,
            "canyon depth {} (rim {rim}, floor {floor})",
            rim - floor
        );
        assert!(floor >= l.lowest());
    }

    #[test]
    fn landing_zone_stays_clear_of_random_landforms() {
        let l = Landforms::new(&data(), 150.0, 9);
        for f in l.list.iter().skip(3) {
            assert!(
                f.center.y < 10f32.to_radians().cos(),
                "{:?} at {:?}",
                f.kind,
                f.center
            );
        }
    }

    fn one(kind: LandformKind, radius_m: f32, height_m: f32, to: Option<(f32, f32, f32)>) -> Landforms {
        Landforms::new(
            &LandformData {
                water_level_m: Some(-3.0),
                clear_zone_deg: 0.0,
                placed: vec![PlacedLandform {
                    kind,
                    at: (1.0, 0.2, 0.0),
                    to,
                    radius_m,
                    height_m,
                }],
                random: vec![],
            },
            600.0,
            4,
        )
    }

    /// Height at `t` footprints out from the centre along a fixed tangent.
    fn at(l: &Landforms, t: f32) -> f32 {
        let f = &l.list[0];
        let dir = rotate_toward(f.center, perpendicular(f.center), f.angle * t);
        l.apply(dir, 0.0, 600.0).0
    }

    #[test]
    fn butte_is_tall_with_a_sheer_cliff() {
        let l = one(LandformKind::Butte, 30.0, 40.0, None);
        assert!(at(&l, 0.0) > 35.0, "cap {}", at(&l, 0.0));
        // Most of the height is lost over a few metres at the cliff.
        let drop = at(&l, 0.5) - at(&l, 0.72);
        assert!(drop > 18.0, "cliff drop {drop}");
        assert!(at(&l, 1.6).abs() < 0.01);
    }

    #[test]
    fn dunes_are_asymmetric_ridges() {
        let l = one(LandformKind::Dunes, 120.0, 4.0, None);
        let f = &l.list[0];
        let wind = perpendicular_seeded(f.center, f.seed);
        let h: Vec<f32> = (-400..400)
            .map(|i| {
                let dir = rotate_toward(f.center, wind, i as f32 * 0.1 / 600.0);
                l.apply(dir, 0.0, 600.0).0
            })
            .collect();
        let (lo, hi) = h.iter().fold((f32::MAX, f32::MIN), |(a, b), &x| (a.min(x), b.max(x)));
        assert!(hi - lo > 2.0, "dune relief {}", hi - lo);
        // Steepest drop is much steeper than the steepest rise (slip faces).
        let steps: Vec<f32> = h.windows(2).map(|w| w[1] - w[0]).collect();
        let up = steps.iter().copied().fold(0.0, f32::max);
        let down = -steps.iter().copied().fold(0.0, f32::min);
        assert!(up.max(down) > 1.8 * up.min(down), "up {up} down {down}");
    }

    #[test]
    fn crater_has_rim_floor_and_central_peak() {
        let l = one(LandformKind::Crater, 80.0, 10.0, None);
        let (peak, floor, rim) = (at(&l, 0.0), at(&l, 0.4), at(&l, 1.0));
        assert!(rim > 7.0, "rim {rim}");
        assert!(floor < -12.0, "floor {floor}");
        assert!(peak > floor + 8.0, "peak {peak} floor {floor}");
        assert!(floor >= l.lowest());
        assert!(at(&l, 1.85).abs() < 0.01);
    }

    #[test]
    fn ridge_rises_along_its_path() {
        let l = one(LandformKind::Ridge, 10.0, 8.0, Some((0.8, 0.2, 0.6)));
        let f = &l.list[0];
        let (a, b) = f.path_ends().unwrap();
        let mid = (a + b).normalized();
        let crest = (-30..=30)
            .map(|i| l.apply((mid + f.normal * (i as f32 * 0.002)).normalized(), 0.0, 600.0).0)
            .fold(f32::MIN, f32::max);
        assert!(crest > 4.0, "crest {crest}");
        let far = l.apply((mid + f.normal * 0.2).normalized(), 0.0, 600.0).0;
        assert!(far.abs() < 0.01);
    }

    #[test]
    fn sinkhole_is_a_deep_pit() {
        let l = one(LandformKind::Sinkhole, 12.0, 18.0, None);
        assert!(at(&l, 0.0) < -18.0, "floor {}", at(&l, 0.0));
        assert!(at(&l, 0.0) >= l.lowest());
        assert!(at(&l, 1.3) > -2.0);
    }
}
