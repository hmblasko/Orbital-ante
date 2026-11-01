//! Draws and applies the weather of whichever body you are near (see
//! `weather.rs` for the timeline): blowing dust, rain or snow around the
//! camera, haze and storm fog, wind that shoves you, lightning bolts with
//! their own colour per planet, wandering dust devils, and hull corrosion
//! for ships parked in acid storms.
//!
//! Serves pillar 1 (a living system) and fun rule 7 (sensation and easy fun).

use std::collections::HashMap;

use godot::classes::base_material_3d::ShadingMode;
use godot::classes::cpu_particles_3d::{EmissionShape, Parameter};
use godot::classes::light_3d::Param as LightParam;
use godot::classes::mesh::PrimitiveType;
use godot::classes::{
    CpuParticles3D, Environment, INode3D, ImmediateMesh, MeshInstance3D, Node3D, OmniLight3D,
    StandardMaterial3D, WorldEnvironment,
};
use godot::prelude::*;

use super::body::{CelestialBody, GRAVITY_GROUP};
use super::system::SYSTEM_GROUP;
use super::weather::{WeatherData, WeatherKind, WeatherSchedule, WeatherState, fog, lerp3, wind_speed};
use crate::flight::ship::SHIP_GROUP;
use crate::player::controller::{PLAYER_GROUP, PlayerController};
use crate::seed::SeededRng;

/// Particles live this long (seconds) and spawn in a box this big around you.
const PARTICLE_LIFETIME_S: f32 = 1.6;
const PARTICLE_BOX_M: Vector3 = Vector3::new(22.0, 12.0, 22.0);
/// Lightning flash and bolt time, seconds.
const FLASH_S: f32 = 0.25;

#[derive(GodotClass)]
#[class(base = Node3D)]
pub struct Weather {
    schedules: HashMap<i64, (WeatherData, WeatherSchedule)>,
    particles: Option<Gd<CpuParticles3D>>,
    particle_kind: Option<WeatherKind>,
    bolt: Option<Gd<MeshInstance3D>>,
    flash: Option<Gd<OmniLight3D>>,
    flash_left: f32,
    next_strike: f32,
    devils: Vec<Devil>,
    rng: SeededRng,
    /// The fog we set last (so a clear sky turns it off once).
    fog_on: bool,
    /// Last state, for tests and the debug readout.
    state: WeatherState,
    body_name: GString,
    event_kind: Option<WeatherKind>,
    wind: Vector3,
    /// Tests: use this time instead of the solar system clock.
    debug_time: Option<f64>,
    /// The sun's normal brightness, so storms can dim it and give it back.
    sun_energy: Option<f32>,
    /// The camera is under water this frame.
    underwater: bool,
    base: Base<Node3D>,
}

struct Devil {
    node: Gd<CpuParticles3D>,
    /// Body-space position on the ground and wander heading.
    local: Vector3,
    heading: Vector3,
}

/// A dust devil shoves anyone inside this radius (metres) up and around.
const DEVIL_RADIUS_M: f32 = 3.5;
const DEVIL_PUSH: f32 = 9.0;
/// Dust devils appear this far from you (metres), close enough to see in the haze.
const DEVIL_SPAWN_M: (f32, f32) = (25.0, 70.0);

#[godot_api]
impl INode3D for Weather {
    fn init(base: Base<Node3D>) -> Self {
        Self {
            schedules: HashMap::new(),
            particles: None,
            particle_kind: None,
            bolt: None,
            flash: None,
            flash_left: 0.0,
            next_strike: 0.0,
            devils: Vec::new(),
            rng: SeededRng::from_u64(1),
            fog_on: false,
            state: WeatherState { event: None, strength: 0.0, next_in: f64::MAX },
            body_name: GString::new(),
            event_kind: None,
            wind: Vector3::ZERO,
            debug_time: None,
            sun_energy: None,
            underwater: false,
            base,
        }
    }

    fn ready(&mut self) {
        // After the solar system and the ship, before the player reads wind.
        self.base_mut().set_physics_process_priority(-80);
        let seed = if godot::classes::Engine::singleton().has_singleton("RunSeed") { crate::seed::singleton::run_seed() } else { 0 };
        self.rng = SeededRng::derive(seed, "weather/fx");
        let mut flash = OmniLight3D::new_alloc();
        flash.set_name("LightningFlash");
        flash.set_param(LightParam::RANGE, 500.0);
        flash.set_param(LightParam::ENERGY, 0.0);
        flash.set_visible(false);
        self.base_mut().add_child(&flash);
        self.flash = Some(flash);
        let mut bolt = MeshInstance3D::new_alloc();
        bolt.set_name("LightningBolt");
        bolt.set_mesh(&ImmediateMesh::new_gd());
        bolt.set_visible(false);
        self.base_mut().add_child(&bolt);
        self.bolt = Some(bolt);
    }

    fn physics_process(&mut self, delta: f64) {
        let dt = delta as f32;
        let Some(camera) = self.base().get_viewport().and_then(|v| v.get_camera_3d()) else { return };
        let eye = camera.get_global_position();
        let player = self.base().get_tree().get_first_node_in_group(PLAYER_GROUP).and_then(|p| p.try_cast::<PlayerController>().ok());
        let Some(body) = nearest_body(&self.base().get_tree(), eye) else { return };
        let clock = self.debug_time.unwrap_or_else(|| self
            .base()
            .get_tree()
            .get_first_node_in_group(SYSTEM_GROUP)
            .map_or(0.0, |mut s| s.call("get_clock", &[]).to::<f64>()));

        let key = body.instance_id().to_i64();
        if !self.schedules.contains_key(&key) {
            if let Some(data) = body.bind().weather_data() {
                let seed = if godot::classes::Engine::singleton().has_singleton("RunSeed") { crate::seed::singleton::run_seed() } else { 0 };
                let schedule = WeatherSchedule::new(&data, seed, &body.bind().body_name());
                self.schedules.insert(key, (data, schedule));
            }
        }
        let Some((data, schedule)) = self.schedules.get(&key).cloned() else {
            self.calm_everything(player);
            return;
        };

        let centre = body.get_global_position();
        let up = (eye - centre).normalized();
        let altitude = eye.distance_to(centre) - body.bind().surface_radius_toward(eye);
        let reach = smoothstep(data.ceiling_m, data.ceiling_m * 0.6, altitude);
        let sheltered = body.bind().is_dark_at(eye);
        let in_cabin = player.as_ref().is_some_and(|p| p.bind().is_in_cabin());
        // Under water (an ocean body, eye below the surface): the sea's
        // colour and darkness replace the sky, and only underwater events run.
        let ocean = body.bind().ocean();
        let depth = body.bind().water_depth_at(eye);
        let underwater = ocean.is_some() && depth > 0.0;
        let reach = if underwater { 1.0 } else { reach };
        let mut state = schedule.state_at(&data, clock);
        if state.event.is_some_and(|i| data.events[i].underwater != underwater) {
            state.event = None;
            state.strength = 0.0;
        }
        state.strength *= reach;
        self.state = state;
        self.body_name = GString::from(body.bind().body_name().as_str());

        // Wind: turning slowly; the body's spin carries the air with it.
        let wind_dir = schedule.wind_dir(up, clock);
        let speed = if underwater { 0.0 } else { wind_speed(&data, &state) * reach };
        self.wind = wind_dir * speed;
        if let Some(mut p) = player.clone() {
            let grip = if p.is_on_floor() { data.ground_share } else { 1.0 };
            let push = if sheltered || in_cabin { Vector3::ZERO } else { self.wind * data.push_per_wind * grip };
            p.bind_mut().set_wind_acceleration(push);
        }

        // Fog: calm haze plus the event's.
        let event = state.event.map(|i| data.events[i].clone());
        let event_dim = event.as_ref().map_or(0.0, |e| e.sun_dim) * state.strength;
        let dim = match ocean.as_ref().filter(|_| underwater) {
            Some(o) => {
                let k = (depth / o.dark_depth_m.max(0.1)).clamp(0.0, 1.0);
                let c = lerp3(o.fog, o.deep_fog, k);
                self.set_fog(Color::from_rgb(c.0, c.1, c.2), o.fog_density + (o.deep_fog_density - o.fog_density) * k);
                (o.max_sun_dim * k).max(event_dim)
            }
            None => {
                let (fog_color, density) = fog(&data, &state);
                self.set_fog(Color::from_rgb(fog_color.0, fog_color.1, fog_color.2), density * reach);
                (data.calm_sun_dim * reach).max(event_dim)
            }
        };
        self.set_sun_dim(dim);
        self.event_kind = event.as_ref().filter(|_| state.strength > 0.0).map(|e| e.kind);
        self.underwater = underwater;
        let show = !sheltered && !in_cabin;
        self.update_particles(event.as_ref(), state.strength, eye, self.wind, up, show);
        self.update_lightning(event.as_ref(), state.strength, eye, up, &body, dt);
        self.update_devils(event.as_ref(), state.strength, &body, eye, player, dt);
        self.corrode_ships(event.as_ref(), state.strength, &body, data.ceiling_m, dt);
    }
}

#[godot_api]
impl Weather {
    /// Name of the active weather event ("" when calm), for tests and the HUD.
    #[func]
    pub fn get_event_name(&self) -> GString {
        self.event_kind.map_or(GString::new(), |k| GString::from(format!("{k:?}").as_str()))
    }

    /// Tests: run the weather at time `t` (seconds) instead of the system
    /// clock; a negative `t` goes back to the clock.
    #[func]
    pub fn debug_set_time(&mut self, t: f64) {
        self.debug_time = (t >= 0.0).then_some(t);
    }

    /// Tests: the middle of the first `kind` event (e.g. "Sandstorm") on the
    /// body with id `body_id`, or -1 if it never happens.
    #[func]
    pub fn debug_event_middle(&mut self, body_id: GString, kind: GString) -> f64 {
        let Some(sys) = self.base().get_tree().get_first_node_in_group(SYSTEM_GROUP) else { return -1.0 };
        let Some(body) = sys.get_node_or_null(&NodePath::from(&body_id)).and_then(|n| n.try_cast::<CelestialBody>().ok()) else { return -1.0 };
        let Some(data) = body.bind().weather_data() else { return -1.0 };
        let seed = if godot::classes::Engine::singleton().has_singleton("RunSeed") { crate::seed::singleton::run_seed() } else { 0 };
        let schedule = WeatherSchedule::new(&data, seed, &body.bind().body_name());
        let want = kind.to_string();
        schedule
            .spells
            .iter()
            .find(|s| format!("{:?}", data.events[s.event].kind) == want)
            .map_or(-1.0, |s| s.start + s.duration * 0.5)
    }

    /// Tests: is the camera under water?
    #[func]
    pub fn is_underwater(&self) -> bool {
        self.underwater
    }

    /// Tests: is a lightning flash showing right now?
    #[func]
    pub fn is_flashing(&self) -> bool {
        self.flash_left > 0.0
    }

    /// Tests: are weather particles (rain, dust, snow) falling around you?
    #[func]
    pub fn is_precipitating(&self) -> bool {
        self.particles.as_ref().is_some_and(|p| p.is_emitting())
    }

    /// Tests: current fog density in the world environment.
    #[func]
    pub fn get_fog_density(&self) -> f32 {
        self.environment().map_or(0.0, |e| if e.is_fog_enabled() { e.get_fog_density() } else { 0.0 })
    }

    #[func]
    pub fn get_strength(&self) -> f32 {
        self.state.strength
    }

    #[func]
    pub fn get_wind(&self) -> Vector3 {
        self.wind
    }

    #[func]
    pub fn get_seconds_to_next(&self) -> f64 {
        self.state.next_in
    }

    #[func]
    pub fn get_devil_count(&self) -> i32 {
        self.devils.len() as i32
    }

    /// Positions (world) of the dust devils, for tests.
    #[func]
    pub fn get_devil_positions(&self) -> PackedVector3Array {
        self.devils.iter().map(|d| d.node.get_global_position()).collect()
    }
}

impl Weather {
    fn calm_everything(&mut self, player: Option<Gd<PlayerController>>) {
        self.state = WeatherState { event: None, strength: 0.0, next_in: f64::MAX };
        self.event_kind = None;
        self.wind = Vector3::ZERO;
        if let Some(mut p) = player {
            p.bind_mut().set_wind_acceleration(Vector3::ZERO);
        }
        self.set_fog(Color::from_rgb(0.0, 0.0, 0.0), 0.0);
        self.set_sun_dim(0.0);
        if let Some(p) = self.particles.as_mut() {
            p.set_emitting(false);
        }
        for d in self.devils.drain(..) {
            d.node.free();
        }
    }

    fn environment(&self) -> Option<Gd<Environment>> {
        let root = self.base().get_tree().get_root();
        let node = root.find_child_ex("WorldEnvironment").recursive(true).owned(false).done()?;
        node.try_cast::<WorldEnvironment>().ok()?.get_environment()
    }

    /// Dim the sunlight by `dim` (0..1) under storm clouds.
    fn set_sun_dim(&mut self, dim: f32) {
        let Some(mut sun) = self.base().get_parent().and_then(|p| p.get_node_or_null("Sunlight")).and_then(|n| n.try_cast::<godot::classes::DirectionalLight3D>().ok()) else {
            return;
        };
        let base = *self.sun_energy.get_or_insert_with(|| sun.get_param(LightParam::ENERGY));
        sun.set_param(LightParam::ENERGY, base * (1.0 - dim.clamp(0.0, 0.95)));
    }

    fn set_fog(&mut self, color: Color, density: f32) {
        let on = density > 1e-5;
        if !on && !self.fog_on {
            return;
        }
        let Some(mut env) = self.environment() else { return };
        env.set_fog_enabled(on);
        env.set_fog_light_color(color);
        env.set_fog_density(density);
        env.set_fog_sky_affect(0.8);
        self.fog_on = on;
    }

    fn update_particles(&mut self, event: Option<&super::weather::WeatherEvent>, strength: f32, eye: Vector3, wind: Vector3, up: Vector3, show: bool) {
        let Some(e) = event.filter(|e| show && e.particles_per_s > 0.0 && strength > 0.01) else {
            if let Some(p) = self.particles.as_mut() {
                p.set_emitting(false);
            }
            return;
        };
        if self.particle_kind != Some(e.kind) {
            if let Some(p) = self.particles.take() {
                p.free();
            }
            let p = make_particles(e);
            self.base_mut().add_child(&p);
            self.particles = Some(p);
            self.particle_kind = Some(e.kind);
        }
        let Some(p) = self.particles.as_mut() else { return };
        p.set_global_position(eye - wind * PARTICLE_LIFETIME_S * 0.5);
        p.set_global_basis(super::body::basis_with_up(up));
        let fall = match e.kind {
            WeatherKind::Rain | WeatherKind::Thunderstorm | WeatherKind::AcidStorm => 16.0,
            WeatherKind::Snow => 1.6,
            _ => 0.0,
        };
        let v = wind - up * fall;
        let local_dir = p.get_global_basis().inverse() * v.normalized_or_zero();
        p.set_direction(local_dir);
        p.set_param_min(Parameter::INITIAL_LINEAR_VELOCITY, v.length() * 0.85);
        p.set_param_max(Parameter::INITIAL_LINEAR_VELOCITY, v.length() * 1.15);
        let c = e.particle_color;
        // Rain streaks are thin and see-through; flakes and dust a bit denser.
        let opacity = match e.kind {
            WeatherKind::Snow => 0.9,
            WeatherKind::Sandstorm => 0.55,
            _ => 0.4,
        };
        p.set_color(Color::from_rgba(c.0, c.1, c.2, strength.clamp(0.0, 1.0) * opacity));
        p.set_emitting(true);
    }

    fn update_lightning(&mut self, event: Option<&super::weather::WeatherEvent>, strength: f32, eye: Vector3, up: Vector3, body: &Gd<CelestialBody>, dt: f32) {
        self.flash_left -= dt;
        if self.flash_left <= 0.0 {
            if let Some(f) = self.flash.as_mut() {
                f.set_visible(false);
            }
            if let Some(b) = self.bolt.as_mut() {
                b.set_visible(false);
            }
        } else if let Some(f) = self.flash.as_mut() {
            let k = (self.flash_left / FLASH_S).clamp(0.0, 1.0).sqrt();
            f.set_param(LightParam::ENERGY, 10.0 * k * k);
        }
        let Some(l) = event.and_then(|e| e.lightning.clone()).filter(|_| strength > 0.5) else { return };
        self.next_strike -= dt * strength;
        if self.next_strike > 0.0 {
            return;
        }
        self.next_strike = self.rng.range_f32(l.every_s.0, l.every_s.1);
        if event.is_some_and(|e| e.kind == WeatherKind::Bioluminescence) {
            // A soft glow somewhere in the dark water nearby, no bolt.
            let b = super::body::basis_with_up(up);
            let a = self.rng.range_f32(0.0, std::f32::consts::TAU);
            let off = (b.col_a() * a.cos() + b.col_c() * a.sin()) * self.rng.range_f32(8.0, 30.0)
                + up * self.rng.range_f32(-4.0, 8.0);
            if let Some(f) = self.flash.as_mut() {
                f.set_color(Color::from_rgb(l.color.0, l.color.1, l.color.2));
                f.set_param(LightParam::RANGE, 45.0);
                f.set_global_position(eye + off);
                f.set_visible(true);
            }
            self.flash_left = FLASH_S * 3.0;
            return;
        }
        if let Some(f) = self.flash.as_mut() {
            f.set_param(LightParam::RANGE, 500.0);
        }
        // Strike the ground somewhere between 60 and 300 m away.
        let b = super::body::basis_with_up(up);
        let a = self.rng.range_f32(0.0, std::f32::consts::TAU);
        let dist = self.rng.range_f32(60.0, 300.0);
        let centre = body.get_global_position();
        let toward = (eye - centre + (b.col_a() * a.cos() + b.col_c() * a.sin()) * dist).normalized();
        let ground = centre + toward * body.bind().surface_radius_toward(centre + toward);
        let color = Color::from_rgb(l.color.0, l.color.1, l.color.2);
        let top = ground + toward * 160.0;
        // A jagged bolt: a few kinked segments from cloud to ground.
        let mut mesh = ImmediateMesh::new_gd();
        let mut mat = StandardMaterial3D::new_gd();
        mat.set_shading_mode(ShadingMode::UNSHADED);
        mat.set_albedo(color);
        mesh.surface_begin_ex(PrimitiveType::LINE_STRIP).material(&mat).done();
        let side = toward.cross(b.col_a()).normalized();
        let steps = 9;
        for i in 0..=steps {
            let t = i as f32 / steps as f32;
            let jitter = if i == 0 || i == steps { 0.0 } else { self.rng.range_f32(-8.0, 8.0) };
            mesh.surface_add_vertex(top.lerp(ground, t) + side * jitter + toward.cross(side) * jitter * 0.5);
        }
        mesh.surface_end();
        if let Some(bolt) = self.bolt.as_mut() {
            bolt.set_mesh(&mesh);
            bolt.set_visible(true);
        }
        if let Some(f) = self.flash.as_mut() {
            f.set_color(color);
            f.set_global_position(ground + toward * 40.0);
            f.set_visible(true);
        }
        self.flash_left = FLASH_S;
    }

    fn update_devils(&mut self, event: Option<&super::weather::WeatherEvent>, strength: f32, body: &Gd<CelestialBody>, eye: Vector3, player: Option<Gd<PlayerController>>, dt: f32) {
        let want = event.filter(|e| e.kind == WeatherKind::DustDevils && strength > 0.2).map_or(0, |e| e.devils) as usize;
        while self.devils.len() > want {
            if let Some(d) = self.devils.pop() {
                d.node.free();
            }
        }
        let to_local = body.get_global_transform().affine_inverse();
        let to_world = body.get_global_transform();
        let eye_local = to_local * eye;
        let r = body.bind().radius();
        while self.devils.len() < want {
            let e = event.map(|e| e.particle_color).unwrap_or((0.7, 0.5, 0.35));
            let node = make_devil(e);
            self.base_mut().add_child(&node);
            let up = eye_local.normalized();
            let b = super::body::basis_with_up(up);
            let a = self.rng.range_f32(0.0, std::f32::consts::TAU);
            let dist = self.rng.range_f32(DEVIL_SPAWN_M.0, DEVIL_SPAWN_M.1) / r;
            let tangent = b.col_a() * a.cos() + b.col_c() * a.sin();
            let local = (up * dist.cos() + tangent * dist.sin()).normalized();
            let heading = (b.col_c() * a.cos() - b.col_a() * a.sin()).normalized();
            self.devils.push(Devil { node, local, heading });
        }
        let player_pos = player.as_ref().map(|p| p.get_global_position());
        let mut push = Vector3::ZERO;
        for d in &mut self.devils {
            // Wander across the ground, turning gently.
            let turn = self.rng.range_f32(-0.6, 0.6) * dt;
            let side = d.local.cross(d.heading);
            d.heading = (d.heading + side * turn).normalized();
            d.local = (d.local + d.heading * (3.0 * dt / r)).normalized();
            d.heading = (d.heading - d.local * d.heading.dot(d.local)).normalized();
            let ground = to_world * (d.local * body.bind().surface_radius_local(d.local));
            let up = (ground - body.get_global_position()).normalized();
            d.node.set_global_position(ground);
            d.node.set_global_basis(super::body::basis_with_up(up));
            if let Some(p) = player_pos {
                let rel = p - ground;
                let flat = rel - up * rel.dot(up);
                if flat.length() < DEVIL_RADIUS_M && rel.dot(up) < 25.0 {
                    let spin = up.cross(flat.normalized_or_zero());
                    push += (up * 0.8 + spin) * DEVIL_PUSH * strength;
                }
            }
        }
        if push != Vector3::ZERO {
            if let Some(mut p) = player {
                let w = p.bind().wind_acceleration();
                p.bind_mut().set_wind_acceleration(w + push);
            }
        }
        let _ = eye;
    }

    fn corrode_ships(&mut self, event: Option<&super::weather::WeatherEvent>, strength: f32, body: &Gd<CelestialBody>, ceiling: f32, dt: f32) {
        let Some((hp, floor)) = event.map(|e| (e.corrosion_hp_s, e.corrosion_floor)).filter(|(h, _)| *h > 0.0) else { return };
        let centre = body.get_global_position();
        for node in self.base().get_tree().get_nodes_in_group(SHIP_GROUP).iter_shared() {
            let Ok(mut ship) = node.try_cast::<crate::flight::ship::Ship>() else { continue };
            let p = ship.get_global_position();
            let altitude = p.distance_to(centre) - body.bind().surface_radius_toward(p);
            if altitude < ceiling {
                ship.bind_mut().corrode(hp * strength * dt, floor);
            }
        }
    }
}

fn nearest_body(tree: &Gd<godot::classes::SceneTree>, at: Vector3) -> Option<Gd<CelestialBody>> {
    tree.get_nodes_in_group(GRAVITY_GROUP)
        .iter_shared()
        .filter_map(|n| n.try_cast::<CelestialBody>().ok())
        .min_by(|a, b| {
            let da = a.get_global_position().distance_to(at) - a.bind().radius();
            let db = b.get_global_position().distance_to(at) - b.bind().radius();
            da.total_cmp(&db)
        })
}

/// Billboard streaks or flakes that live in world space around the camera.
fn make_particles(e: &super::weather::WeatherEvent) -> Gd<CpuParticles3D> {
    let mut p = CpuParticles3D::new_alloc();
    p.set_name("WeatherParticles");
    let amount = (e.particles_per_s * PARTICLE_LIFETIME_S).ceil().max(1.0) as i32;
    p.set_amount(amount);
    p.set_lifetime(PARTICLE_LIFETIME_S as f64);
    p.set_use_local_coordinates(false);
    p.set_emission_shape(EmissionShape::BOX);
    p.set_emission_box_extents(PARTICLE_BOX_M * 0.5);
    p.set_spread(4.0);
    p.set_gravity(Vector3::ZERO);
    let (size, stretch) = match e.kind {
        WeatherKind::Rain | WeatherKind::Thunderstorm | WeatherKind::AcidStorm => (0.035, 18.0),
        WeatherKind::Snow => (0.09, 1.0),
        WeatherKind::Sandstorm => (0.22, 2.5),
        _ => (0.2, 1.0),
    };
    // Soft round puffs (stretched into streaks for rain), never hard squares.
    let mut quad = crate::player::effects::puff_mesh(true, (0.4, 2.0));
    quad.set_size(Vector2::new(size, size * stretch));
    p.set_mesh(&quad);
    p.set_param_min(Parameter::SCALE, 0.6);
    p.set_param_max(Parameter::SCALE, 1.4);
    p.set_particle_flag(godot::classes::cpu_particles_3d::ParticleFlags::ALIGN_Y_TO_VELOCITY, stretch > 2.0);
    p
}

/// A whirling column of dust: a ring at the ground spiralling upward.
fn make_devil(color: (f32, f32, f32)) -> Gd<CpuParticles3D> {
    let mut p = CpuParticles3D::new_alloc();
    p.set_name("DustDevil");
    p.set_amount(420);
    p.set_lifetime(4.0);
    p.set_use_local_coordinates(true);
    p.set_emission_shape(EmissionShape::RING);
    p.set_emission_ring_axis(Vector3::UP);
    p.set_emission_ring_radius(2.0);
    p.set_emission_ring_inner_radius(1.0);
    p.set_emission_ring_height(0.5);
    p.set_direction(Vector3::UP);
    p.set_spread(10.0);
    p.set_gravity(Vector3::UP * 2.0);
    p.set_param_min(Parameter::INITIAL_LINEAR_VELOCITY, 1.5);
    p.set_param_max(Parameter::INITIAL_LINEAR_VELOCITY, 2.5);
    p.set_param_min(Parameter::TANGENTIAL_ACCEL, 3.0);
    p.set_param_max(Parameter::TANGENTIAL_ACCEL, 4.5);
    // Pulled in toward the axis so the column stays a column.
    p.set_param_min(Parameter::RADIAL_ACCEL, -1.2);
    p.set_param_max(Parameter::RADIAL_ACCEL, -0.8);
    p.set_param_min(Parameter::SCALE, 0.8);
    p.set_param_max(Parameter::SCALE, 2.2);
    // Soft dust puffs in the dust's own colour (unshaded, so the shadow
    // tint never turns them grey-blue).
    let mut quad = crate::player::effects::puff_mesh(true, (1.0, 4.0));
    quad.set_size(Vector2::new(3.2, 3.2));
    p.set_mesh(&quad);
    p.set_color(Color::from_rgba(color.0 * 0.85, color.1 * 0.85, color.2 * 0.85, 0.85));
    p.set_emitting(true);
    p
}

fn smoothstep(e0: f32, e1: f32, x: f32) -> f32 {
    let t = ((x - e0) / (e1 - e0)).clamp(0.0, 1.0);
    t * t * (3.0 - 2.0 * t)
}
