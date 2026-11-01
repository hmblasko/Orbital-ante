//! `AnteDirector`: runs the ante loop in the slice scene. It ticks the
//! clock, notices first discoveries (landforms, caves, landmarks, first
//! landings), handles cash-out, and opens the between-ante screens (the
//! contract board, the upgrade screen, the run summary), pausing the world
//! while one is up. The rules themselves live in `points::run`.
//!
//! Serves pillar 3 (every run counts and teaches) and fun rules 4
//! (curiosity: discovery pays, and adds time) and 5 (clear goals, flow).

use godot::classes::{CanvasLayer, Control, Input, PackedScene};
use godot::obj::Singleton;
use godot::prelude::*;

use super::singleton::Points;
use crate::orbits::CelestialBody;
use crate::orbits::body::GRAVITY_GROUP;
use crate::player::PlayerController;
use crate::player::controller::PLAYER_GROUP;

/// Group the director joins, so GameRoot can find it.
pub const DIRECTOR_GROUP: &str = "ante_director";
const BOARD_SCENE: &str = "res://scenes/ui/contract_board.tscn";
const UPGRADE_SCENE: &str = "res://scenes/ui/upgrade_screen.tscn";
const SUMMARY_SCENE: &str = "res://scenes/ui/run_summary.tscn";

#[derive(GodotClass)]
#[class(base = Node)]
pub struct AnteDirector {
    check_timer: f32,
    cash_hold_s: f32,
    death_seen: bool,
    /// The places around the start were marked as known (no points for
    /// being put down on top of them).
    start_marked: bool,
    screen: Option<Gd<Control>>,
    screen_name: String,
    layer: Option<Gd<CanvasLayer>>,
    base: Base<Node>,
}

#[godot_api]
impl INode for AnteDirector {
    fn init(base: Base<Node>) -> Self {
        Self { check_timer: 0.0, cash_hold_s: 0.0, death_seen: false, start_marked: false, screen: None, screen_name: String::new(), layer: None, base }
    }

    fn ready(&mut self) {
        // Keep running while the world is paused, so screens can close.
        self.base_mut().set_process_mode(godot::classes::node::ProcessMode::ALWAYS);
        self.base_mut().add_to_group(DIRECTOR_GROUP);
        let mut layer = CanvasLayer::new_alloc();
        layer.set_name("Screens");
        layer.set_layer(20);
        self.base_mut().add_child(&layer);
        self.layer = Some(layer);
        // Hunter swarms that grow with the ante (they pause with the world).
        let mut swarms = crate::ai::swarm::SwarmDirector::new_alloc();
        swarms.set_name("Swarms");
        self.base_mut().add_child(&swarms);
        let mut points = Points::singleton();
        if !points.bind().has_run() {
            points.bind_mut().start_run();
        }
        self.base_mut().call_deferred("sync_screen", &[]);
    }

    fn process(&mut self, delta: f64) {
        let phase = Points::singleton().bind().get_phase().to_string();
        if phase != "playing" {
            return;
        }
        if self.base().get_tree().is_paused() && self.screen.is_none() {
            // Someone else paused (the map, later the pause menu).
            return;
        }
        let ended = Points::singleton().bind_mut().tick(delta);
        let player = self.player();
        if let Some(p) = &player {
            if p.bind().is_dead() {
                if !self.death_seen {
                    self.death_seen = true;
                    Points::singleton().bind_mut().end_run_died();
                }
                return;
            }
            self.check_timer -= delta as f32;
            if self.check_timer <= 0.0 {
                self.check_timer = Points::singleton().bind().tables().run.discovery_check_s;
                self.check_discoveries(p);
            }
            self.cash_out_input(p, delta as f32);
        }
        if ended {
            self.ante_ended();
        }
    }
}

#[godot_api]
impl AnteDirector {
    /// Show the screen the run's phase needs (board, upgrade, summary).
    #[func]
    pub fn sync_screen(&mut self) {
        let phase = Points::singleton().bind().get_phase().to_string();
        let want = match phase.as_str() {
            "board" => "board",
            "upgrade" => "upgrade",
            "over" => "summary",
            _ => "",
        };
        if want == self.screen_name {
            return;
        }
        self.close_screen();
        if want.is_empty() {
            self.set_paused(false);
            return;
        }
        if !screens_enabled() {
            // Tests and bot runs: skip the board and upgrade screens.
            self.set_paused(false);
            match want {
                "board" => Points::singleton().bind_mut().start_ante(),
                "upgrade" => {
                    if Points::singleton().bind_mut().next_ante() {
                        self.respawn_camps();
                    }
                    Points::singleton().bind_mut().start_ante();
                }
                _ => {}
            }
            return;
        }
        let path = match want {
            "board" => BOARD_SCENE,
            "upgrade" => UPGRADE_SCENE,
            _ => SUMMARY_SCENE,
        };
        let Ok(packed) = try_load::<PackedScene>(path) else {
            godot_error!("AnteDirector: cannot load {path}");
            return;
        };
        let Some(node) = packed.instantiate() else { return };
        let Ok(mut screen) = node.try_cast::<Control>() else { return };
        screen.set_process_mode(godot::classes::node::ProcessMode::ALWAYS);
        let callable = self.to_gd().callable("on_screen_closed");
        if screen.has_signal("closed") {
            screen.connect("closed", &callable);
        }
        if let Some(layer) = &mut self.layer {
            layer.add_child(&screen);
        }
        // The ante's notes are done; don't leave them under the screen.
        crate::ui_bridge::Notifications::singleton().bind_mut().clear();
        self.screen = Some(screen);
        self.screen_name = want.to_string();
        Input::singleton().set_mouse_mode(godot::classes::input::MouseMode::VISIBLE);
        self.set_paused(true);
    }

    /// A screen finished (contracts taken, upgrades done, summary read).
    #[func]
    pub fn on_screen_closed(&mut self) {
        let name = self.screen_name.clone();
        match name.as_str() {
            "board" => Points::singleton().bind_mut().start_ante(),
            "upgrade" => {
                if Points::singleton().bind_mut().next_ante() {
                    self.respawn_camps();
                }
            }
            "summary" => {
                self.close_screen();
                self.set_paused(false);
                self.return_to_menu();
                return;
            }
            _ => {}
        }
        self.sync_screen();
        if self.screen.is_none() {
            Input::singleton().set_mouse_mode(godot::classes::input::MouseMode::CAPTURED);
        }
    }

    /// Which screen is up: "", "board", "upgrade" or "summary" (tests).
    #[func]
    pub fn get_screen_name(&self) -> GString {
        GString::from(&self.screen_name)
    }

    #[func]
    pub fn get_screen(&self) -> Option<Gd<Control>> {
        self.screen.clone()
    }

    /// Run the discovery check now (tests).
    #[func]
    pub fn debug_check_discoveries(&mut self) {
        if let Some(p) = self.player() {
            self.check_discoveries(&p);
        }
    }

    /// Cash out as if the button were held (tests).
    #[func]
    pub fn debug_cash_out(&mut self) -> i64 {
        let paid = Points::singleton().bind_mut().cash_out();
        if paid >= 0 {
            self.ante_ended();
        }
        paid
    }

    /// The player died and read the recap: show the run summary.
    #[func]
    pub fn show_summary_after_death(&mut self) {
        Points::singleton().bind_mut().end_run_died();
        self.sync_screen();
    }
}

impl AnteDirector {
    fn player(&self) -> Option<Gd<PlayerController>> {
        self.base().get_tree().get_first_node_in_group(PLAYER_GROUP).and_then(|n| n.try_cast::<PlayerController>().ok())
    }

    fn set_paused(&mut self, on: bool) {
        self.base().get_tree().set_pause(on);
    }

    fn close_screen(&mut self) {
        if let Some(mut s) = self.screen.take() {
            s.queue_free();
        }
        self.screen_name.clear();
    }

    fn ante_ended(&mut self) {
        // Autosave at ante end (section 14).
        let mut save = crate::save::SaveData::singleton();
        save.bind_mut().record_ante_end();
        self.sync_screen();
    }

    fn return_to_menu(&mut self) {
        let tree = self.base().get_tree();
        if let Some(mut root) = tree.get_first_node_in_group(crate::player::hud::GAME_ROOT_GROUP) {
            root.call_deferred("on_run_over", &[]);
        } else {
            Points::singleton().bind_mut().clear_run();
        }
    }

    /// The faction retakes its camps at the start of every ante (section 2:
    /// factions retake planets and return stronger).
    fn respawn_camps(&mut self) {
        let ante = Points::singleton().bind().get_ante();
        for n in self.base().get_tree().get_nodes_in_group(GRAVITY_GROUP).iter_shared() {
            if let Ok(mut b) = n.try_cast::<CelestialBody>() {
                b.bind_mut().respawn_camps(ante);
            }
        }
    }

    fn cash_out_input(&mut self, p: &Gd<PlayerController>, dt: f32) {
        let can = Points::singleton().bind().can_cash_out() && p.bind().is_buckled();
        if can && Input::singleton().is_action_pressed("cash_out") {
            self.cash_hold_s += dt;
            if self.cash_hold_s >= Points::singleton().bind().get_cash_out_hold_s() {
                self.cash_hold_s = 0.0;
                if Points::singleton().bind_mut().cash_out() >= 0 {
                    self.ante_ended();
                }
            }
        } else {
            self.cash_hold_s = 0.0;
        }
    }

    fn check_discoveries(&mut self, p: &Gd<PlayerController>) {
        let pos = p.get_global_position();
        let Some(body) = nearest_body(&self.base().get_tree(), pos) else { return };
        let body_id = body.get_name().to_string();
        let local = body.get_global_transform().affine_inverse() * pos;
        if local.length_squared() < 1.0 {
            return;
        }
        let dir = local.normalized();
        let b = body.bind();
        let ground = b.surface_radius_local(dir);
        let altitude = local.length() - ground;
        let run = Points::singleton().bind().tables().run.clone();
        // First landing: standing on the ground, off the ship.
        let on_foot = p.bind().aboard_ship().is_none() && !p.bind().is_buckled();
        let landed = altitude < run.landed_altitude_m && on_foot && body_id != run.home_body;
        let mut found: Vec<(String, i64, String)> = Vec::new();
        if altitude < run.discovery_max_altitude_m {
            if let Some(shape) = b.shape() {
                let r = ground.max(1.0);
                for (i, l) in shape.landforms().list.iter().enumerate() {
                    if l.radius_m < run.discovery_min_radius_m {
                        continue;
                    }
                    let kind = format!("{:?}", l.kind);
                    let Some(points) = run.discovery_points.get(&kind) else { continue };
                    let centre = l.center.normalized_or_zero();
                    let dist = dir.angle_to(centre) * r;
                    if dist <= l.radius_m + run.discovery_margin_m {
                        let label = tr(&format!("LANDFORM_{}", kind.to_uppercase()));
                        found.push((format!("lf:{body_id}:{i}"), *points as i64, label));
                    }
                }
            }
            for (i, c) in b.get_caves().iter_shared().enumerate() {
                let Ok(d) = c.try_to::<VarDictionary>() else { continue };
                let Some(mouth) = d.get("mouth").and_then(|v| v.try_to::<Vector3>().ok()) else { continue };
                if mouth.distance_to(local) <= run.cave_find_m {
                    found.push((format!("cave:{body_id}:{i}"), run.cave_points as i64, tr("DISCOVERY_CAVE")));
                }
            }
        }
        drop(b);
        if let Some(marks) = body.get_node_or_null("Landmarks") {
            for m in marks.get_children().iter_shared() {
                let Ok(m) = m.try_cast::<Node3D>() else { continue };
                if m.get_global_position().distance_to(pos) <= run.landmark_find_m {
                    let name = m.get_name().to_string();
                    found.push((format!("lm:{body_id}:{name}"), run.landmark_points as i64, tr(&format!("LANDMARK_{}", name.to_uppercase()))));
                }
            }
        }
        let mut points = Points::singleton();
        if !self.start_marked {
            // Where the run starts isn't a discovery: you were put there.
            self.start_marked = true;
            for (key, _, _) in &found {
                points.bind_mut().mark_known(GString::from(key));
            }
            return;
        }
        if landed {
            points.bind_mut().on_landed(GString::from(&body_id));
        }
        for (key, value, label) in found {
            points.bind_mut().on_discovered(GString::from(&key), GString::from(&body_id), value, GString::from(&label));
        }
    }
}

/// Screens on unless turned off (`Points.set_screens_mode(0)`), or, by
/// default, when the game runs a test script (`--script`).
fn screens_enabled() -> bool {
    match Points::singleton().bind().screens_mode() {
        1 => true,
        0 => false,
        _ => !godot::classes::Os::singleton().get_cmdline_args().as_slice().iter().any(|a| a.to_string() == "--script"),
    }
}

fn tr(key: &str) -> String {
    godot::classes::TranslationServer::singleton().translate(&StringName::from(key)).to_string()
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
