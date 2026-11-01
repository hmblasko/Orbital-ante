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
    /// Lunar mare: a low, dark, smooth lava plain that buries the hills under it.
    Mare,
    /// A multi-ring impact basin: concentric scarps around a flooded centre.
    Basin,
    /// A sinuous rille: a narrow, winding dry trench (a collapsed lava channel).
    Rille,
    /// A simple bowl crater with a raised rim (`height_m` is its depth).
    Bowl,
    /// A young bowl crater with bright ejecta rays out to about five radii.
    RayedCrater,
    /// A deep bowl whose floor never sees the sun: frost lies on it.
    ColdTrap,
    /// Seeded only: a crater chain (catena), expanded into 4-8 bowls in a line.
    Catena,
    /// Seeded only: a canyon maze (labyrinthus), expanded into 4-7 short,
    /// deep crossing trenches.
    Labyrinth,
    /// Seeded only: an erosion sequence, expanded into a mesa with buttes
    /// stepping away from it (hoodoos gather around the mesa).
    Erosion,
    /// A field of yardangs: streamlined ridges lined up with the wind, blunt
    /// upwind and tapering downwind.
    Yardangs,
    /// A rampart crater: a bowl ringed by lobed, flow-like ejecta with a
    /// raised outer edge.
    Rampart,
    /// A polar ice cap: a low icy dome cut by curving spiral troughs.
    PolarCap,
    /// A lobate scarp (path): a long cliff where the crust shrank and one
    /// side was shoved up over the other (Mercury). Steep face on one side,
    /// a long gentle back slope on the other.
    Scarp,
    /// A corona (Venus): a raised ring of fractured ground around a sunken
    /// centre, with a moat outside the ring and cracks running outward.
    Corona,
    /// A pancake dome (Venus): a wide, flat-topped lava dome with steep
    /// sides, a central pit and concentric cracks.
    PancakeDome,
    /// A patera (Io): a steep-walled volcanic pit with a glowing lava lake.
    Patera,
    /// Chaos terrain (Europa): a lowered field of jumbled, tilted ice blocks.
    Chaos,
    /// A double ridge (path, Europa): twin icy crests with a trough between.
    DoubleRidge,
    /// A field of penitentes: tall, thin ice blades leaning the same way,
    /// carved by sunlight (Jupiter's ammonia ice, Pluto).
    Penitentes,
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
    /// Only place these on top of an earlier landform of this kind (for
    /// example rilles and wrinkle ridges on a mare).
    #[serde(default)]
    pub on: Option<LandformKind>,
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
    /// Dark mare basalt.
    pub mare: f32,
    /// Bright ejecta rays.
    pub rays: f32,
    /// Frost in permanently shadowed ground.
    pub ice: f32,
    /// Glassy impact melt on crater floors.
    pub melt: f32,
}

impl LandHints {
    fn max(&mut self, o: LandHints) {
        self.strata = self.strata.max(o.strata);
        self.lava = self.lava.max(o.lava);
        self.snow = self.snow.max(o.snow);
        self.wet = self.wet.max(o.wet);
        self.mineral = self.mineral.max(o.mineral);
        self.mare = self.mare.max(o.mare);
        self.rays = self.rays.max(o.rays);
        self.ice = self.ice.max(o.ice);
        self.melt = self.melt.max(o.melt);
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
        LandformKind::Mare => 1.4,
        LandformKind::Basin => 1.6,
        LandformKind::Bowl | LandformKind::ColdTrap => 1.8,
        LandformKind::RayedCrater => RAY_REACH,
        // Expanded into bowls at placement; never sampled itself.
        LandformKind::Catena | LandformKind::Labyrinth | LandformKind::Erosion => 1.0,
        LandformKind::Yardangs => 1.15,
        LandformKind::Rampart => 2.6,
        LandformKind::PolarCap => 1.3,
        LandformKind::Corona => 1.4,
        LandformKind::PancakeDome => 1.3,
        LandformKind::Patera => 1.5,
        LandformKind::Chaos => 1.2,
        LandformKind::Penitentes => 1.1,
        // Paths: half-width multiples, checked separately.
        LandformKind::Canyon => 5.0,
        LandformKind::River => 3.2,
        LandformKind::Ridge => 3.0,
        LandformKind::Rille => 2.5,
        LandformKind::Scarp => SCARP_BACK,
        LandformKind::DoubleRidge => 3.0,
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
                let dir = match group.on {
                    Some(kind) => {
                        let hosts: Vec<&Landform> = list.iter().filter(|l| l.kind == kind).collect();
                        if hosts.is_empty() {
                            break;
                        }
                        let host = hosts[rng.range_i64(0, hosts.len() as i64 - 1) as usize];
                        // Somewhere inside the host's footprint.
                        let r = host.angle * 0.8 * rng.next_f32().sqrt();
                        rotate_toward(host.center, perpendicular_random(host.center, &mut rng), r)
                    }
                    None => random_dir(&mut rng),
                };
                let radius_m = rng.range_f32(group.radius_m.0, group.radius_m.1);
                let height = rng.range_f32(group.height_m.0, group.height_m.1);
                if group.kind == LandformKind::Labyrinth {
                    // Crossing trenches around one spot, all about the same depth.
                    if dir.y > clear_cos.min(0.9999) - radius_m * 2.0 / body_radius {
                        continue;
                    }
                    let n = rng.range_i64(4, 7);
                    for _ in 0..n {
                        let at = rotate_toward(dir, perpendicular_random(dir, &mut rng), rng.range_f32(0.0, radius_m) / body_radius);
                        let heading = perpendicular_random(at, &mut rng);
                        let half = rng.range_f32(group.length_m.0, group.length_m.1.max(group.length_m.0)) * 0.5 / body_radius;
                        k += 1;
                        let s = (seed as u32).wrapping_mul(2654435761).wrapping_add(k * 7919);
                        let width = rng.range_f32(3.0, 5.0);
                        list.push(Landform::path(LandformKind::Rille, rotate_toward(at, -heading, half), rotate_toward(at, heading, half), width, height, body_radius, s));
                    }
                    placed += 1;
                    continue;
                }
                if group.kind == LandformKind::Erosion {
                    // One mesa, then buttes stepping away from its edge (a
                    // plateau worn back into remnants, never random singles).
                    let mesa = Landform::point(LandformKind::Mesa, dir, radius_m, height, body_radius, 0);
                    let footprint = mesa.angle * reach(LandformKind::Mesa);
                    if dir.y.acos() < data.clear_zone_deg.to_radians() + footprint * 3.0 {
                        continue;
                    }
                    let crowded = list.iter().filter(|o| !o.is_path() && !is_background(o.kind)).any(|o| {
                        o.center.angle_to(dir) < (footprint * 2.0 + o.angle * reach(o.kind)) * 0.85
                    });
                    if crowded {
                        continue;
                    }
                    k += 1;
                    let s = (seed as u32).wrapping_mul(2654435761).wrapping_add(k * 7919);
                    list.push(Landform::point(LandformKind::Mesa, dir, radius_m, height, body_radius, s));
                    let heading = perpendicular_random(dir, &mut rng);
                    let buttes = rng.range_i64(1, 2);
                    let mut out = radius_m * 1.6;
                    for b in 0..buttes {
                        let r = radius_m * rng.range_f32(0.35, 0.5) * (1.0 - 0.2 * b as f32);
                        out += r * rng.range_f32(1.4, 2.0);
                        let side = perpendicular_random(dir, &mut rng) * 0.25;
                        let at = rotate_toward(dir, (heading + side).normalized(), out / body_radius);
                        k += 1;
                        let s = (seed as u32).wrapping_mul(2654435761).wrapping_add(k * 7919);
                        list.push(Landform::point(LandformKind::Butte, at, r, height * rng.range_f32(1.1, 1.4), body_radius, s));
                        out += r;
                    }
                    placed += 1;
                    continue;
                }
                if group.kind == LandformKind::Catena {
                    // A line of bowls, smaller toward one end, spaced about
                    // a diameter apart (secondary craters from one impact).
                    let length = rng.range_f32(group.length_m.0, group.length_m.1.max(group.length_m.0));
                    if dir.y > clear_cos.min(0.9999) - length / body_radius {
                        continue;
                    }
                    let heading = perpendicular_random(dir, &mut rng);
                    let n = ((length / (radius_m * 2.4)).round() as u32).clamp(4, 8);
                    for i in 0..n {
                        let t = i as f32 / (n - 1) as f32;
                        let at = rotate_toward(dir, heading, length * t / body_radius);
                        let shrink = 1.0 - 0.45 * t * rng.range_f32(0.6, 1.0);
                        k += 1;
                        let s = (seed as u32).wrapping_mul(2654435761).wrapping_add(k * 7919);
                        list.push(Landform::point(LandformKind::Bowl, at, radius_m * shrink, height * shrink, body_radius, s));
                    }
                    placed += 1;
                    continue;
                }
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
                    && !is_background(l.kind)
                    && list.iter().filter(|o| !o.is_path() && !is_background(o.kind)).any(|o| {
                        o.center.angle_to(l.center) < (footprint + o.angle * reach(o.kind)) * 0.85
                    });
                if crowded {
                    continue;
                }
                list.push(l);
                placed += 1;
            }
        }
        // Plains and basins reshape the ground first, so craters, ridges
        // and rilles sit on top of them (stable: same order every run).
        list.sort_by_key(|l| !is_background(l.kind));
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
                        | LandformKind::Rille
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
                LandformKind::Bowl | LandformKind::RayedCrater | LandformKind::ColdTrap => -l.height - 6.0,
                LandformKind::Rille => -l.height - 6.0,
                LandformKind::Mare => -l.height - 6.0,
                LandformKind::Basin => -l.height * 1.3 - 8.0,
                LandformKind::Patera => -l.height - 6.0,
                LandformKind::Chaos => -l.height * CHAOS_SINK - 6.0,
                LandformKind::Corona => -l.height * 0.5 - 6.0,
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

/// How far out a rayed crater's rays reach, in crater radii.
const RAY_REACH: f32 = 5.5;

fn is_path_kind(kind: LandformKind) -> bool {
    matches!(
        kind,
        LandformKind::Canyon
            | LandformKind::River
            | LandformKind::Ridge
            | LandformKind::Rille
            | LandformKind::Scarp
            | LandformKind::DoubleRidge
    )
}

/// Broad forms that reshape the ground under other landforms rather than
/// competing with them for space.
fn is_background(kind: LandformKind) -> bool {
    matches!(kind, LandformKind::Mare | LandformKind::Basin)
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
                // A craggy massif of several summits, not a single cone.
                let f = 7.0 / l.angle;
                let crags = noise::ridged3([p[0] * f, p[1] * f, p[2] * f], 3, 0.55, l.seed ^ 57);
                let peak = smoothstep(0.36, 0.08, x) * (0.4 + 0.55 * crags);
                -depth + l.height * (CRATER_DEPTH + 0.2) * peak
            } else if x < 1.0 {
                // Slumped terraces climbing to the rim.
                let t = terrace(smoothstep(0.55, 1.0, x), 3.0);
                -depth + (depth + l.height) * t
            } else {
                // Ejecta blanket falling away from the raised rim.
                l.height * (1.0 - smoothstep(1.0, 1.8, x)).powf(2.2)
            };
            hints.strata = smoothstep(0.55, 0.7, x) * smoothstep(1.02, 0.92, x) * 0.35;
            // Half of complex craters keep a glassy impact-melt sheet on the floor.
            if l.seed % 2 == 0 {
                let f = 2.0 / l.angle;
                let patch = noise::fbm3([p[0] * f, p[1] * f, p[2] * f], 2, 0.5, l.seed ^ 87);
                hints.melt = smoothstep(0.52, 0.4, x) * smoothstep(0.12, 0.2, x) * smoothstep(-0.1, 0.15, patch);
            }
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
        LandformKind::Mare => {
            if !raise_pass {
                return (height, hints);
            }
            let x = outline(d, l, 0.35);
            if x >= 1.4 {
                return (height, hints);
            }
            // Lava filled the low ground and set flat: hills and old
            // craters drown under it, leaving a gentle swell and a shore.
            let w = smoothstep(1.4, 0.85, x);
            let f = 0.8 / l.angle;
            let swell = noise::fbm3([p[0] * f, p[1] * f, p[2] * f], 2, 0.5, l.seed ^ 19) * 0.25;
            let flooded = height * MARE_KEEP - l.height * (1.0 + swell);
            hints.mare = smoothstep(1.25, 0.95, x);
            (height + (flooded - height) * w, hints)
        }
        LandformKind::Basin => {
            if !raise_pass {
                return (height, hints);
            }
            let x = outline(d, l, 0.05);
            if x >= 1.6 {
                return (height, hints);
            }
            // Concentric rings, each a steep inward-facing scarp, stepping
            // down to a mare-flooded centre; ejecta fades outside.
            let f = 2.5 / l.angle;
            let rough = noise::ridged3([p[0] * f, p[1] * f, p[2] * f], 2, 0.5, l.seed ^ 43);
            let mut profile = -l.height * smoothstep(1.0, BASIN_RINGS[2], x);
            for (i, r) in BASIN_RINGS.iter().enumerate() {
                let w = 0.05 + 0.02 * i as f32;
                let rise = l.height * (0.55 - 0.12 * i as f32) * (0.7 + 0.5 * rough);
                // Sharp inner face, long outer slope.
                let k = if x < *r { smoothstep(r - w, *r, x) } else { smoothstep(r + w * 3.5, *r, x) };
                profile += rise * k * k;
            }
            let fade = smoothstep(1.6, 1.0, x);
            let inner = smoothstep(BASIN_RINGS[2] + 0.04, BASIN_RINGS[2] - 0.06, x);
            hints.mare = inner;
            // Flooded centre buries the hills like a mare.
            let base = height * (1.0 - 0.75 * inner);
            (base + profile * fade, hints)
        }
        LandformKind::Rille => {
            let Some((q, along)) = path_coords(l, d, body_radius) else {
                return (height, hints);
            };
            if raise_pass || q >= 2.5 {
                return (height, hints);
            }
            // Steep walls, a narrow flat floor, slight levees either side.
            let dig = l.height * smoothstep(1.0, 0.35, q) * along;
            let levee = l.height * 0.12 * smoothstep(2.5, 1.4, q) * smoothstep(0.9, 1.3, q) * along;
            hints.strata = smoothstep(1.05, 0.8, q) * smoothstep(0.45, 0.7, q) * 0.5 * along;
            (height - dig + levee, hints)
        }
        LandformKind::Bowl | LandformKind::RayedCrater | LandformKind::ColdTrap => {
            let x = outline(d, l, 0.05);
            let reach_x = reach(l.kind);
            if x >= reach_x {
                return (height, hints);
            }
            let depth = l.height;
            let rim = depth * 0.3;
            let profile = if x < 1.0 {
                -depth * (1.0 - x * x) + rim * x.powi(6)
            } else {
                rim * (1.0 - smoothstep(1.0, 1.8, x)).powf(2.0)
            };
            if l.kind == LandformKind::RayedCrater {
                // Rays: narrow bright streaks around the crater, fading out.
                let b = tangent_frame(l.center);
                let (u, w) = (d.dot(b.0), d.dot(b.1));
                let theta = w.atan2(u);
                let spokes = noise::gradient3(theta.cos() * 7.0, theta.sin() * 7.0, x * 0.35, l.seed ^ 61);
                let ray = smoothstep(0.0, 0.22, spokes) * smoothstep(RAY_REACH, 1.2, x);
                let blanket = smoothstep(1.9, 1.0, x) * smoothstep(0.7, 1.0, x);
                hints.rays = ray.max(blanket) * (x > 0.85) as i32 as f32 + smoothstep(0.6, 0.0, x) * 0.3;
            }
            if l.kind == LandformKind::ColdTrap {
                hints.ice = smoothstep(0.85, 0.45, x);
            }
            (height + profile, hints)
        }
        LandformKind::Catena | LandformKind::Labyrinth | LandformKind::Erosion => (height, hints),
        LandformKind::Yardangs => {
            if !raise_pass {
                return (height, hints);
            }
            let x = outline(d, l, 0.3);
            if x >= 1.15 {
                return (height, hints);
            }
            // Wind frame: `wind` downwind, `across` sideways (metres).
            let wind = perpendicular_seeded(l.center, l.seed);
            let across = l.center.cross(wind);
            let spacing = (l.height * YARDANG_SPACING).max(6.0);
            let v = d.dot(across) * body_radius / spacing;
            let lane = v.floor();
            let f = v - lane - 0.5;
            // Each ridge gets its own start and length along the wind.
            let h = |k: f32| ((k * 12.9898 + l.seed as f32 * 0.001).sin() * 43_758.547).fract().abs();
            let length = l.radius_m * (0.45 + 0.5 * h(lane));
            let start = (h(lane + 31.0) - 0.5) * l.radius_m;
            let t = (d.dot(wind) * body_radius - start) / length;
            // Blunt upwind head, long tapering tail; rounded across.
            let along = smoothstep(-0.05, 0.08, t) * (1.0 - smoothstep(0.15, 1.0, t)).powf(0.7);
            let width = 0.32 * (1.0 - 0.5 * t.clamp(0.0, 1.0));
            let section = smoothstep(width, width * 0.25, f.abs());
            let fade = smoothstep(1.15, 0.75, x);
            hints.strata = 0.0;
            (height + l.height * section * along * fade, hints)
        }
        LandformKind::Rampart => {
            if !raise_pass {
                return (height, hints);
            }
            let x0 = d.angle_to(l.center) / l.angle;
            if x0 >= 2.6 {
                return (height, hints);
            }
            let depth = l.height;
            let bowl = if x0 < 1.0 { -depth * (1.0 - x0 * x0) + depth * 0.25 * x0.powi(6) } else { 0.0 };
            // Lobed ejecta: a flow apron whose edge wanders with angle and
            // ends in a raised rampart.
            let (u, w) = tangent_frame(l.center);
            let theta = d.dot(w).atan2(d.dot(u));
            let lobes = noise::gradient3(theta.cos() * 2.2, theta.sin() * 2.2, 0.5, l.seed ^ 97);
            let edge = 1.9 + 0.45 * lobes;
            let apron = if x0 >= 1.0 && x0 < edge {
                let t = (x0 - 1.0) / (edge - 1.0);
                depth * (0.25 * (1.0 - t) + 0.22 * smoothstep(0.7, 0.97, t))
            } else if x0 >= edge {
                depth * 0.22 * smoothstep(edge + 0.12, edge, x0)
            } else {
                0.0
            };
            hints.strata = smoothstep(edge + 0.1, edge - 0.05, x0) * smoothstep(1.0, 1.15, x0) * 0.25;
            (height + bowl + apron, hints)
        }
        LandformKind::Scarp => {
            if !raise_pass {
                return (height, hints);
            }
            let Some((q, along)) = path_coords_signed(l, d, body_radius) else {
                return (height, hints);
            };
            if q <= -0.2 || q >= SCARP_BACK {
                return (height, hints);
            }
            // Steep lobate face over one width, then a long back slope.
            let f = 1.2 / l.angle;
            let lobes = noise::fbm3([p[0] * f, p[1] * f, p[2] * f], 2, 0.5, l.seed ^ 61) * 0.4;
            let qq = q + lobes;
            let face = smoothstep(0.0, 1.0, qq);
            let back = smoothstep(SCARP_BACK, 1.5, qq);
            hints.strata = smoothstep(-0.1, 0.4, qq) * smoothstep(1.1, 0.6, qq) * 0.6;
            (height + l.height * face * back * along, hints)
        }
        LandformKind::Corona => {
            if !raise_pass {
                return (height, hints);
            }
            let x = outline(d, l, 0.18);
            if x >= 1.4 {
                return (height, hints);
            }
            let ring = (-((x - 0.85) / 0.12).powi(2)).exp();
            let moat = (-((x - 1.12) / 0.08).powi(2)).exp() * 0.25;
            let sunk = smoothstep(0.75, 0.3, x) * 0.3;
            // Radial cracks across the ring and out over the plain.
            let (u, w) = tangent_frame(l.center);
            let theta = d.dot(w).atan2(d.dot(u));
            let spokes = (theta * CORONA_SPOKES + l.seed as f32 * 0.01).sin().abs();
            let crack = smoothstep(0.08, 0.0, spokes) * smoothstep(0.55, 0.75, x) * smoothstep(1.35, 1.0, x) * 0.25;
            hints.strata = ring * 0.5;
            hints.lava = smoothstep(0.6, 0.2, x) * 0.3;
            (height + l.height * (ring - moat - sunk - crack), hints)
        }
        LandformKind::PancakeDome => {
            if !raise_pass {
                return (height, hints);
            }
            let x = outline(d, l, 0.05);
            if x >= 1.3 {
                return (height, hints);
            }
            // Steep rim (wide enough for the mesh to draw as a wall), a
            // gently domed top, concentric cracks and a little central pit.
            let w = (3.0 / l.radius_m).max(0.06);
            let body = smoothstep(1.0, 1.0 - w, x);
            let crown = smoothstep(1.0, 0.0, x) * 0.12;
            let cracks = smoothstep(0.1, 0.0, ((x * PANCAKE_CRACKS).fract() - 0.5).abs()) * smoothstep(0.9, 0.7, x) * 0.08;
            let pit = smoothstep(0.12, 0.05, x) * 0.35;
            hints.lava = 0.35 * body;
            hints.strata = smoothstep(1.05, 1.0 - w, x) * smoothstep(1.0 - 2.0 * w, 1.0 - w, x) * 0.5;
            (height + l.height * (body * (0.88 + crown) - cracks - pit), hints)
        }
        LandformKind::Patera => {
            let x = outline(d, l, 0.3);
            if x >= 1.5 {
                return (height, hints);
            }
            // Scalloped steep walls to a flat floor of crusted lava.
            let wall = smoothstep(1.0, 0.8, x);
            let lip = smoothstep(1.5, 1.0, x) * 0.1;
            let f = 3.0 / l.angle;
            let crust = noise::fbm3([p[0] * f, p[1] * f, p[2] * f], 2, 0.5, l.seed ^ 29);
            hints.lava = smoothstep(0.8, 0.65, x) * (0.7 + 0.3 * smoothstep(-0.2, 0.2, crust));
            hints.strata = smoothstep(1.02, 0.95, x) * smoothstep(0.7, 0.85, x);
            let dig = l.height * wall - l.height * lip;
            (height - dig, hints)
        }
        LandformKind::Chaos => {
            if !raise_pass {
                return (height, hints);
            }
            let x = outline(d, l, 0.3);
            if x >= 1.2 {
                return (height, hints);
            }
            // A sunken matrix with flat-topped, tilted ice rafts in a grid
            // of jittered cells.
            let inside = smoothstep(1.2, 0.95, x);
            let (u, w) = tangent_frame(l.center);
            let cell = (l.radius_m / CHAOS_CELLS).max(6.0);
            let cu = d.dot(u) * body_radius / cell;
            let cw = d.dot(w) * body_radius / cell;
            let (iu, iw) = (cu.floor(), cw.floor());
            let h = |a: f32, b: f32, k: f32| ((a * 12.9898 + b * 78.233 + k + l.seed as f32 * 0.0007).sin() * 43_758.547).fract().abs();
            // Each raft: jittered centre, its own size and turn, its own
            // tilt; the tallest raft covering this point wins.
            let mut raft = 0.0f32;
            let mut edge = 0.0f32;
            for du in -1..=1 {
                for dw in -1..=1 {
                    let (a, b) = (iu + du as f32, iw + dw as f32);
                    if h(a, b, 1.0) < 0.3 {
                        continue;
                    }
                    let (pu, pw) = (a + 0.3 + 0.4 * h(a, b, 6.0), b + 0.3 + 0.4 * h(a, b, 7.0));
                    let turn = h(a, b, 8.0) * std::f32::consts::PI;
                    let (du_, dw_) = (cu - pu, cw - pw);
                    let lx = du_ * turn.cos() + dw_ * turn.sin();
                    let ly = -du_ * turn.sin() + dw_ * turn.cos();
                    let (hx, hy) = (0.22 + 0.2 * h(a, b, 2.0), 0.18 + 0.2 * h(a, b, 9.0));
                    let k = (lx.abs() / hx).max(ly.abs() / hy);
                    let e = smoothstep(1.0, 0.75, k);
                    if e <= 0.0 {
                        continue;
                    }
                    let tilt = (h(a, b, 3.0) - 0.5) * lx + (h(a, b, 4.0) - 0.5) * ly;
                    let top = e * (CHAOS_SINK + 0.25 * h(a, b, 5.0) + tilt * 0.6);
                    if top > raft {
                        raft = top;
                        edge = e;
                    }
                }
            }
            let present = edge > 0.0;
            hints.snow = edge * present as i32 as f32 * inside * 0.8;
            hints.ice = inside * 0.4;
            (height + l.height * inside * (raft - CHAOS_SINK), hints)
        }
        LandformKind::DoubleRidge => {
            if !raise_pass {
                return (height, hints);
            }
            let Some((q, along)) = path_coords(l, d, body_radius) else {
                return (height, hints);
            };
            if q >= 3.0 {
                return (height, hints);
            }
            let crest = |c: f32| (-((q - c) / 0.55).powi(2)).exp();
            let profile = crest(1.0) * 0.9 + smoothstep(3.0, 0.0, q) * 0.15;
            hints.snow = crest(1.0) * 0.5;
            (height + l.height * profile * along, hints)
        }
        LandformKind::Penitentes => {
            if !raise_pass {
                return (height, hints);
            }
            let x = outline(d, l, 0.3);
            if x >= 1.1 {
                return (height, hints);
            }
            // Jittered grid of blades, each a sharp cone leaning along the
            // seeded sun direction; taller towards the field's middle.
            let (u, w) = tangent_frame(l.center);
            let spacing = (l.height * PENITENTE_SPACING).max(4.0);
            let cu = d.dot(u) * body_radius / spacing;
            let cw = d.dot(w) * body_radius / spacing;
            let (iu, iw) = (cu.floor(), cw.floor());
            let h = |a: f32, b: f32, k: f32| ((a * 12.9898 + b * 78.233 + k + l.seed as f32 * 0.0013).sin() * 43_758.547).fract().abs();
            let mut best = 0.0f32;
            for du in -1..=1 {
                for dw in -1..=1 {
                    let (a, b) = (iu + du as f32, iw + dw as f32);
                    let (pu, pw) = (a + 0.2 + 0.6 * h(a, b, 1.0), b + 0.2 + 0.6 * h(a, b, 2.0));
                    // Blades are thin across and longer along their row.
                    let (ou, ow) = (cu - pu, (cw - pw) * 0.55);
                    let r = (ou * ou + ow * ow).sqrt() / (0.36 + 0.12 * h(a, b, 3.0));
                    best = best.max((1.0 - r).max(0.0).powf(1.3) * (0.6 + 0.4 * h(a, b, 4.0)));
                }
            }
            let field = smoothstep(1.1, 0.7, x);
            hints.snow = field * (0.5 + 0.5 * best);
            (height + l.height * best * field, hints)
        }
        LandformKind::PolarCap => {
            if !raise_pass {
                return (height, hints);
            }
            let x = outline(d, l, 0.12);
            if x >= 1.3 {
                return (height, hints);
            }
            let dome = l.height * smoothstep(1.15, 0.2, x);
            // Spiral troughs: lines of constant (angle - twist * radius).
            let (u, w) = tangent_frame(l.center);
            let theta = d.dot(w).atan2(d.dot(u));
            let arms = POLAR_TROUGHS;
            let phase = (theta * arms - x * 9.0 + l.seed as f32 * 0.01).rem_euclid(std::f32::consts::TAU);
            let off = std::f32::consts::PI - (phase - std::f32::consts::PI).abs();
            let trough = smoothstep(0.45, 0.1, off)
                * smoothstep(0.15, 0.3, x)
                * smoothstep(1.05, 0.8, x);
            // Layered ice shows in the trough walls.
            hints.strata = trough * 0.8;
            hints.snow = smoothstep(1.2, 0.95, x) * (1.0 - trough * 0.5);
            (height * (1.0 - 0.6 * smoothstep(1.1, 0.6, x)) + dome - l.height * 0.45 * trough, hints)
        }
    }
}

/// Spacing between yardang ridges, as a multiple of their height.
const YARDANG_SPACING: f32 = 4.0;
/// Spiral troughs on a polar cap.
const POLAR_TROUGHS: f32 = 3.0;
/// A lobate scarp's back slope runs this many half-widths behind the face.
const SCARP_BACK: f32 = 8.0;
/// Radial cracks around a corona.
const CORONA_SPOKES: f32 = 7.0;
/// Concentric cracks across a pancake dome's top.
const PANCAKE_CRACKS: f32 = 4.0;
/// Chaos terrain: blocks across the radius, and how far the matrix sinks
/// (as a share of `height_m`).
const CHAOS_CELLS: f32 = 6.0;
const CHAOS_SINK: f32 = 0.6;
/// Spacing between penitente blades, as a multiple of their height.
const PENITENTE_SPACING: f32 = 1.6;

/// Share of the old hills still showing through a mare (gentle swells, never flat).
const MARE_KEEP: f32 = 0.4;

/// Ring radii of a multi-ring basin, outermost first, as shares of its radius.
const BASIN_RINGS: [f32; 3] = [1.0, 0.68, 0.42];

/// Two unit tangents at `c` (for angles around a point on the sphere).
pub(crate) fn tangent_frame(c: Vector3) -> (Vector3, Vector3) {
    let a = perpendicular(c);
    (a, c.cross(a))
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
    path_coords_signed(l, d, body_radius).map(|(q, along)| (q.abs(), along))
}

/// Like `path_coords`, but the cross distance keeps its sign (which side
/// of the path you are on).
fn path_coords_signed(l: &Landform, d: Vector3, body_radius: f32) -> Option<(f32, f32)> {
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
    let (f, amp) = if matches!(l.kind, LandformKind::River | LandformKind::Rille) { (1.2 / l.angle, 2.0) } else { (0.4 / l.angle, 1.2) };
    let wobble = noise::fbm3([d.x * f, d.y * f, d.z * f], 3, 0.5, l.seed ^ 5) * amp;
    let edge_t = (t / l.arc).clamp(-0.5, 1.5);
    let along = smoothstep(-0.04, 0.12, edge_t) * smoothstep(1.04, 0.88, edge_t);
    let offset = (bends + wobble) * l.angle * along;
    let q = (s - offset) / l.angle / along.max(0.15).sqrt();
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
                    on: None,
                },
                RandomLandforms {
                    kind: LandformKind::River,
                    count: 2,
                    radius_m: (2.5, 3.5),
                    height_m: (0.0, 0.0),
                    length_m: (60.0, 120.0),
                    on: None,
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

    #[test]
    fn mare_is_low_and_smooth() {
        let l = one(LandformKind::Mare, 80.0, 3.0, None);
        let f = &l.list[0];
        // Hills of +-2 m underneath shrink to gentle swells under half that.
        let (hi, _) = l.apply(f.center, 2.0, 600.0);
        let (lo, hints) = l.apply(f.center, -2.0, 600.0);
        assert!(hi < -1.0 && (hi - lo).abs() < 2.0, "hi {hi} lo {lo}");
        assert!(hints.mare > 0.9);
        assert!(at(&l, 1.5).abs() < 0.01);
    }

    #[test]
    fn basin_has_ring_scarps_around_a_low_flooded_centre() {
        let l = one(LandformKind::Basin, 90.0, 10.0, None);
        let centre = at(&l, 0.0);
        let outer_ring = (95..=105).map(|i| at(&l, i as f32 * 0.01)).fold(f32::MIN, f32::max);
        let trough = (78..=88).map(|i| at(&l, i as f32 * 0.01)).fold(f32::MAX, f32::min);
        assert!(centre < -8.0, "centre {centre}");
        assert!(outer_ring > trough + 2.0, "ring {outer_ring} trough {trough}");
        assert!(centre >= l.lowest());
        let (_, hints) = l.apply(l.list[0].center, 0.0, 600.0);
        assert!(hints.mare > 0.9);
    }

    #[test]
    fn rille_is_a_trench_below_the_plain() {
        let l = one(LandformKind::Rille, 3.0, 4.0, Some((0.9, 0.2, 0.4)));
        let f = &l.list[0];
        let (a, b) = f.path_ends().unwrap();
        let mid = (a + b).normalized();
        let floor = (-60..=60)
            .map(|i| l.apply((mid + f.normal * (i as f32 * 0.0005)).normalized(), 0.0, 600.0).0)
            .fold(f32::MAX, f32::min);
        assert!(floor < -3.0, "floor {floor}");
        assert!(floor >= l.lowest());
        let far = l.apply((mid + f.normal * 0.2).normalized(), 0.0, 600.0).0;
        assert!(far.abs() < 0.01);
    }

    #[test]
    fn rayed_crater_paints_rays_and_cold_trap_holds_frost() {
        let l = one(LandformKind::RayedCrater, 10.0, 4.0, None);
        assert!(at(&l, 0.0) < -3.5);
        let f = &l.list[0];
        let (u, w) = tangent_frame(f.center);
        let rays = (0..72)
            .map(|i| {
                let a = i as f32 / 72.0 * std::f32::consts::TAU;
                let dir = rotate_toward(f.center, u * a.cos() + w * a.sin(), f.angle * 3.0);
                l.apply(dir, 0.0, 600.0).1.rays
            })
            .collect::<Vec<_>>();
        let lit = rays.iter().filter(|r| **r > 0.5).count();
        assert!(lit > 3 && lit < 60, "{lit} of 72 directions are on a ray");
        let cold = one(LandformKind::ColdTrap, 12.0, 8.0, None);
        assert!(cold.apply(cold.list[0].center, 0.0, 600.0).1.ice > 0.9);
    }

    #[test]
    fn catena_expands_into_a_line_of_bowls_on_its_host() {
        let data = LandformData {
            water_level_m: None,
            clear_zone_deg: 5.0,
            placed: vec![PlacedLandform { kind: LandformKind::Mare, at: (1.0, 0.0, 0.0), to: None, radius_m: 120.0, height_m: 3.0 }],
            random: vec![
                RandomLandforms { kind: LandformKind::Catena, count: 2, radius_m: (4.0, 5.0), height_m: (2.0, 2.5), length_m: (40.0, 60.0), on: None },
                RandomLandforms { kind: LandformKind::Rille, count: 2, radius_m: (2.0, 3.0), height_m: (3.0, 4.0), length_m: (60.0, 90.0), on: Some(LandformKind::Mare) },
            ],
        };
        let l = Landforms::new(&data, 300.0, 3);
        let bowls = l.list.iter().filter(|f| f.kind == LandformKind::Bowl).count();
        assert!((8..=16).contains(&bowls), "{bowls} bowls");
        assert_eq!(l.list[0].kind, LandformKind::Mare, "background forms come first");
        let mare = l.list[0].center;
        for r in l.list.iter().filter(|f| f.kind == LandformKind::Rille) {
            let (a, _) = r.path_ends().unwrap();
            assert!(a.angle_to(mare) < 120.0 / 300.0, "rille starts on the mare");
        }
    }

    #[test]
    fn yardangs_are_long_ridges_along_the_wind() {
        let l = one(LandformKind::Yardangs, 120.0, 5.0, None);
        let f = &l.list[0];
        let wind = perpendicular_seeded(f.center, f.seed);
        let across = f.center.cross(wind);
        let profile = |dir: Vector3| -> Vec<f32> {
            (-200..200).map(|i| l.apply(rotate_toward(f.center, dir, i as f32 * 0.25 / 600.0), 0.0, 600.0).0).collect()
        };
        let count_peaks = |h: &[f32]| h.windows(3).filter(|w| w[1] > w[0] && w[1] >= w[2] && w[1] > 2.0).count();
        // Many ridges across the wind, few along it.
        let a = count_peaks(&profile(across));
        let w = count_peaks(&profile(wind));
        assert!(a >= 4, "{a} ridges across the wind");
        assert!(w <= 2, "{w} peaks along the wind");
    }

    #[test]
    fn rampart_crater_has_a_bowl_and_a_raised_ejecta_edge() {
        let l = one(LandformKind::Rampart, 20.0, 6.0, None);
        assert!(at(&l, 0.0) < -5.0, "floor {}", at(&l, 0.0));
        let apron = (100..=250).map(|i| at(&l, i as f32 * 0.01)).fold(f32::MIN, f32::max);
        assert!(apron > 1.0, "ejecta apron {apron}");
        assert!(at(&l, 2.7).abs() < 0.01);
    }

    #[test]
    fn polar_cap_is_an_icy_dome_with_troughs() {
        let l = one(LandformKind::PolarCap, 150.0, 12.0, None);
        let f = &l.list[0];
        let (_, hints) = l.apply(f.center, 0.0, 600.0);
        assert!(hints.snow > 0.9);
        let (u, w) = tangent_frame(f.center);
        let ring: Vec<f32> = (0..180)
            .map(|i| {
                let a = i as f32 / 180.0 * std::f32::consts::TAU;
                l.apply(rotate_toward(f.center, u * a.cos() + w * a.sin(), f.angle * 0.55), 0.0, 600.0).0
            })
            .collect();
        let (lo, hi) = ring.iter().fold((f32::MAX, f32::MIN), |(a, b), &x| (a.min(x), b.max(x)));
        assert!(hi > 5.0, "dome height {hi}");
        assert!(hi - lo > 3.0, "troughs cut {} m into the cap", hi - lo);
    }

    #[test]
    fn erosion_and_labyrinth_expand_into_their_parts() {
        let data = LandformData {
            water_level_m: None,
            clear_zone_deg: 5.0,
            placed: vec![],
            random: vec![
                RandomLandforms { kind: LandformKind::Erosion, count: 3, radius_m: (25.0, 35.0), height_m: (18.0, 24.0), length_m: (0.0, 0.0), on: None },
                RandomLandforms { kind: LandformKind::Labyrinth, count: 1, radius_m: (40.0, 50.0), height_m: (6.0, 9.0), length_m: (40.0, 60.0), on: None },
            ],
        };
        let l = Landforms::new(&data, 520.0, 8);
        let mesas = l.list.iter().filter(|f| f.kind == LandformKind::Mesa).count();
        let buttes = l.list.iter().filter(|f| f.kind == LandformKind::Butte).count();
        let trenches = l.list.iter().filter(|f| f.kind == LandformKind::Rille).count();
        assert_eq!(mesas, 3);
        assert!((3..=6).contains(&buttes), "{buttes} buttes");
        assert!((4..=7).contains(&trenches), "{trenches} maze trenches");
        // Each butte stands near a mesa, beyond its cliff.
        for b in l.list.iter().filter(|f| f.kind == LandformKind::Butte) {
            let nearest = l.list.iter().filter(|f| f.kind == LandformKind::Mesa).map(|m| m.center.angle_to(b.center) * 520.0).fold(f32::MAX, f32::min);
            assert!(nearest < 160.0, "butte {nearest} m from its mesa");
        }
    }
    /// Heights straight across a path landform's middle, `n` samples each
    /// side out to `reach` half-widths.
    fn across(l: &Landforms, reach: f32, n: i32) -> Vec<f32> {
        let f = &l.list[0];
        let (a, b) = f.path_ends().unwrap();
        let mid = (a + b).normalized();
        (-n..=n)
            .map(|i| {
                let t = i as f32 / n as f32 * reach;
                l.apply(rotate_toward(mid, f.normal, f.angle * t), 0.0, 600.0).0
            })
            .collect()
    }

    #[test]
    fn scarp_has_a_steep_face_and_a_long_back_slope() {
        let l = one(LandformKind::Scarp, 10.0, 12.0, Some((0.8, 0.2, 0.55)));
        let h = across(&l, 12.0, 240);
        let (peak_i, peak) = h.iter().enumerate().fold((0, f32::MIN), |(bi, b), (i, &x)| if x > b { (i, x) } else { (bi, b) });
        assert!(peak > 9.0, "scarp height {peak}");
        let raised: Vec<usize> = h.iter().enumerate().filter(|(_, x)| **x > 0.2 * 12.0).map(|(i, _)| i).collect();
        let (lo, hi) = (*raised.first().unwrap(), *raised.last().unwrap());
        // The crest sits near one end of the raised ground (the face), not mid-way.
        let rel = (peak_i - lo) as f32 / (hi - lo).max(1) as f32;
        assert!(rel < 0.3 || rel > 0.7, "crest at {rel} of the raised ground");
    }

    #[test]
    fn corona_is_a_raised_ring_around_a_sunken_centre() {
        let l = one(LandformKind::Corona, 120.0, 10.0, None);
        let ring = (70..=100).map(|i| at(&l, i as f32 * 0.01)).fold(f32::MIN, f32::max);
        assert!(ring > 6.0, "ring {ring}");
        assert!(at(&l, 0.0) < 0.0, "centre {}", at(&l, 0.0));
        assert!(at(&l, 1.5).abs() < 0.01);
    }

    #[test]
    fn pancake_dome_is_flat_topped_with_steep_sides() {
        let l = one(LandformKind::PancakeDome, 60.0, 10.0, None);
        let top = at(&l, 0.5);
        assert!(top > 8.0 && (at(&l, 0.3) - top).abs() < 2.5, "top {top}");
        let drop = at(&l, 0.9) - at(&l, 1.02);
        assert!(drop > 6.0, "edge drop {drop}");
        assert!(at(&l, 0.0) < top - 2.0, "central pit");
    }

    #[test]
    fn patera_is_a_pit_with_a_lava_floor() {
        let l = one(LandformKind::Patera, 40.0, 12.0, None);
        let f = &l.list[0];
        let (floor, hints) = l.apply(f.center, 0.0, 600.0);
        assert!(floor < -10.0, "floor {floor}");
        assert!(hints.lava > 0.6, "lava {}", hints.lava);
        assert!(at(&l, 1.6).abs() < 0.01);
    }

    #[test]
    fn chaos_is_tilted_blocks_in_a_sunken_matrix() {
        let l = one(LandformKind::Chaos, 90.0, 8.0, None);
        let f = &l.list[0];
        let (u, w) = tangent_frame(f.center);
        let mut lo = f32::MAX;
        let mut hi = f32::MIN;
        for i in 0..40 {
            for j in 0..40 {
                let a = (i as f32 / 40.0 - 0.5) * 1.2 * f.angle;
                let b = (j as f32 / 40.0 - 0.5) * 1.2 * f.angle;
                let h = l.apply((f.center + u * a + w * b).normalized(), 0.0, 600.0).0;
                lo = lo.min(h);
                hi = hi.max(h);
            }
        }
        assert!(lo < -4.0, "matrix {lo}");
        assert!(hi > -1.0, "blocks reach {hi}");
    }

    #[test]
    fn penitentes_are_many_sharp_blades() {
        let l = one(LandformKind::Penitentes, 40.0, 5.0, None);
        let f = &l.list[0];
        let (u, w) = tangent_frame(f.center);
        let mut tall = 0;
        let mut low = 0;
        let n = 60;
        for i in 0..n {
            for j in 0..n {
                let a = (i as f32 / n as f32 - 0.5) * f.angle;
                let b = (j as f32 / n as f32 - 0.5) * f.angle;
                let h = l.apply((f.center + u * a + w * b).normalized(), 0.0, 600.0).0;
                if h > 2.5 { tall += 1; }
                if h < 0.5 { low += 1; }
            }
        }
        let frac = tall as f32 / (n * n) as f32;
        // Many separate blade tips with open ground between them.
        assert!(frac > 0.03 && frac < 0.5, "{frac} of the field is blade tips");
        assert!(low as f32 / (n * n) as f32 > 0.2, "ground between blades");
    }

    #[test]
    fn double_ridge_has_a_trough_between_two_crests() {
        let l = one(LandformKind::DoubleRidge, 6.0, 5.0, Some((0.8, 0.2, 0.55)));
        let h = across(&l, 3.0, 120);
        let peaks = h.windows(3).filter(|w| w[1] > w[0] && w[1] >= w[2] && w[1] > 3.0).count();
        assert_eq!(peaks, 2, "{peaks} crests");
    }

}
