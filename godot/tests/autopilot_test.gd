extends SceneTree
## Phase 4 part 3 feature test: target lock and autopilot.
## 1. Buckled in, lock the body we're parked on and engage the autopilot:
##    the lander climbs and stops at its arrival point above the surface,
##    then holds match velocity.
## 2. Lock the Moon and engage: touching the stick cancels it.
## 3. Engage again: the lander flies to the Moon and stops above it.

var scene: Node
var player: CharacterBody3D
var ship: RigidBody3D
var frame := 0
var stage := 0
var stage_start := 0
var failures: Array[String] = []
var passes: Array[String] = []
const HOME_LIMIT := 1800
const MOON_LIMIT := 6000

func _initialize() -> void:
	scene = load("res://scenes/tests/lander.tscn").instantiate()
	root.add_child(scene)
	player = scene.get_node("Player")
	ship = scene.get_node("Lander")

func check(ok: bool, what: String) -> void:
	if ok: passes.append(what)
	else: failures.append(what)

func bodies() -> Array:
	return get_nodes_in_group("gravity_wells")

func nearest_body() -> Node3D:
	var best: Node3D = null
	for b in bodies():
		var bn := b as Node3D
		if best == null or bn.global_position.distance_to(ship.global_position) - bn.radius() < best.global_position.distance_to(ship.global_position) - best.radius():
			best = bn
	return best

func moon() -> Node3D:
	for b in bodies():
		if String(b.name).to_lower().contains("moon"): return b
	return null

func press(action: String) -> void:
	Input.action_press(action)

func next(s: int) -> void:
	stage = s
	stage_start = frame

func finish() -> void:
	for p in passes: print("autopilot_test: ok   - " + p)
	for f in failures: print("autopilot_test: FAIL - " + f)
	print("autopilot_test: " + ("PASS" if failures.is_empty() else "FAIL"))
	quit(0 if failures.is_empty() else 1)

func _physics_process(_d: float) -> bool:
	frame += 1
	match stage:
		0:
			if frame == 60:
				var t: Transform3D = ship.global_transform
				player.global_transform = Transform3D(t.basis * Basis(Vector3.UP, PI), t * Vector3(-0.9, ship.get_cabin_floor_y() + 0.95, 0.35))
				player.velocity = Vector3.ZERO
			elif frame == 70:
				player.debug_interact()
			elif frame == 90:
				check(player.is_buckled(), "the pilot buckles in")
				var home := nearest_body()
				ship.lock_body(home)
				check(ship.get_target_name() == String(home.name), "a body can be locked (%s)" % home.name)
				press("autopilot")
			elif frame == 91:
				Input.action_release("autopilot")
			elif frame == 93:
				check(ship.is_autopilot_on(), "RT / Q engages the autopilot")
				next(1)
		1:
			if ship.is_holding_match():
				var d: float = ship.get_target_arrival_distance()
				check(d < 30.0 and ship.get_target_relative_speed() < 7.0, "autopilot climbs and stops above the home body (%.1f m off, %.1f m/s, %d ticks)" % [d, ship.get_target_relative_speed(), frame - stage_start])
				print("autopilot_test: home hop took %d ticks" % (frame - stage_start))
				next(2)
			elif frame - stage_start > HOME_LIMIT:
				check(false, "autopilot reaches the home arrival point in time (%.1f m left)" % ship.get_target_arrival_distance())
				next(2)
		2:
			if frame == stage_start + 30:
				check(ship.is_holding_match() and ship.get_target_relative_speed() < 3.0, "after arriving it holds still (%.1f m/s)" % ship.get_target_relative_speed())
				var m := moon()
				check(m != null, "the Moon exists")
				if m == null:
					finish()
					return false
				ship.lock_body(m)
				press("autopilot")
			elif frame == stage_start + 31:
				Input.action_release("autopilot")
			elif frame == stage_start + 60:
				press("move_forward")
			elif frame == stage_start + 63:
				Input.action_release("move_forward")
				check(not ship.is_autopilot_on(), "touching the stick cancels the autopilot")
				press("autopilot")
			elif frame == stage_start + 64:
				Input.action_release("autopilot")
			elif frame == stage_start + 66:
				check(ship.is_autopilot_on(), "the autopilot can be re-engaged")
				set_meta("start_dist", ship.get_target_arrival_distance())
				next(3)
		3:
			if ship.is_holding_match() and not has_meta("arrived"):
				var d: float = ship.get_target_arrival_distance()
				check(d < 30.0 and ship.get_target_relative_speed() < 7.0, "autopilot flies %.0f m to the Moon and stops (%.1f m off, %.1f m/s, %d ticks)" % [float(get_meta("start_dist")), d, ship.get_target_relative_speed(), frame - stage_start])
				set_meta("arrived", frame)
			elif has_meta("arrived") and frame == int(get_meta("arrived")) + 180:
				var d: float = ship.get_target_arrival_distance()
				check(d < 30.0 and ship.get_target_relative_speed() < 1.0, "it then stays put above the moving Moon (%.1f m off, %.1f m/s)" % [d, ship.get_target_relative_speed()])
				var dash: Node = ship.find_child("Dash", true, false)
				print("autopilot_test: frame body at the Moon: %s" % scene.get_node("SolarSystem").get_frame_body())
				finish()
			elif not has_meta("arrived") and frame - stage_start > MOON_LIMIT:
				check(false, "autopilot reaches the Moon in time (%.1f m left, %.1f m/s)" % [ship.get_target_arrival_distance(), ship.get_speed()])
				finish()
			elif (frame - stage_start) % 600 == 0:
				print("autopilot_test: moon leg %d ticks, %.0f m left, %.1f m/s" % [frame - stage_start, ship.get_target_arrival_distance(), ship.get_speed()])
	return false
