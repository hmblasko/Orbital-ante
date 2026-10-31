extends SceneTree
## Phase 3 feature test: the moving solar system.
## 1. The player starts standing on Earth in daylight, riding along with it.
## 2. Standing still for 5 s, the player stays put relative to Earth while
##    Earth orbits the sun and spins.
## 3. Launched straight up, the player leaves Earth's gravity with no jump in
##    absolute speed (momentum is kept when the frame changes) and the world
##    re-centres (floating origin) without any jump in absolute position.
## 4. Every body has moved along its orbit.
## 5. A far body (the Moon) swaps its preview for full detail in the
##    background when asked, without stalling, and drops it again.
## 6. The live map opens with the map button, sees every body, and its zoom
##    steps stay in range.
## Exits with code 1 on any failure.
##
## Run from the godot/ folder:
##   godot --headless --path . --fixed-fps 60 --script res://tests/solar_system_test.gd

const SETTLE_FRAMES := 120
const STAND_FRAMES := 300
const LAUNCH_SPEED := 140.0        # m/s straight up; Earth's escape speed here is about 90 m/s
const MAX_FLIGHT_FRAMES := 60 * 40
const MAX_SPEED_JUMP := 1.0        # m/s per tick; gravity alone changes it by at most 0.2
const MAX_POSITION_JUMP := 4.0     # m per tick at about 140 m/s is 2.3 m
const MAX_DRIFT := 0.5             # m relative to Earth while standing

var scene: Node
var player: CharacterBody3D
var sys: Node
var earth: Node3D
var frame := 0
var phase := 0
var phase_start := 0
var failures: Array[String] = []
var passes: Array[String] = []
var local_start: Vector3
var last_abs := Vector3.ZERO
var last_abs_vel := Vector3.ZERO
var max_speed_jump := 0.0
var max_pos_jump := 0.0
var frames_seen := {}
var origin_start: Vector3
var start_positions := {}

func _initialize() -> void:
	scene = load("res://scenes/tests/solar_system.tscn").instantiate()
	root.add_child(scene)
	player = scene.get_node("Player")
	sys = scene.get_node("SolarSystem")

func check(ok: bool, what: String) -> void:
	if ok: passes.append(what)
	else: failures.append(what)

func absolute() -> Vector3:
	return sys.get_origin() + player.global_position

func _physics_process(delta: float) -> bool:
	frame += 1
	var t := frame - phase_start
	match phase:
		0:
			if frame == 2:
				for b in sys.get_children():
					if b is Node3D and b.is_in_group("gravity_wells"):
						start_positions[b.name] = sys.get_origin() + b.global_position
			if t == SETTLE_FRAMES:
				earth = sys.get_node("earth")
				check(sys.get_frame_body() == "earth", "starts riding Earth (frame '%s')" % sys.get_frame_body())
				check(player.is_on_floor(), "starts standing on the ground")
				var sun: Node3D = sys.get_node("sun")
				var up := (player.global_position - earth.global_position).normalized()
				var to_sun := (sun.global_position - player.global_position).normalized()
				check(up.dot(to_sun) > 0.3, "starts in daylight (sun %.0f deg from overhead)" % rad_to_deg(acos(up.dot(to_sun))))
				local_start = earth.global_transform.affine_inverse() * player.global_position
				phase = 1; phase_start = frame
		1:
			if t == STAND_FRAMES:
				var local_now: Vector3 = earth.global_transform.affine_inverse() * player.global_position
				var drift := local_now.distance_to(local_start)
				check(drift < MAX_DRIFT, "stays put on a moving, spinning Earth (drifted %.3f m in 5 s)" % drift)
				player.velocity = (player.global_position - earth.global_position).normalized() * LAUNCH_SPEED
				origin_start = sys.get_origin()
				last_abs = absolute()
				phase = 2; phase_start = frame
		2:
			var a := absolute()
			var v := (a - last_abs) / delta
			if t > 2:
				max_pos_jump = max(max_pos_jump, a.distance_to(last_abs))
				max_speed_jump = max(max_speed_jump, absf(v.length() - last_abs_vel.length()))
			last_abs = a
			last_abs_vel = v
			frames_seen[sys.get_frame_body()] = true
			if sys.get_frame_body() != "earth" and t > 60 and not frames_seen.has("left"):
				frames_seen["left"] = true
				phase_start = frame - (MAX_FLIGHT_FRAMES - 120)  # fly 2 s more, then finish
			if t >= MAX_FLIGHT_FRAMES:
				check(frames_seen.has("left"), "leaves Earth's gravity when launched at %d m/s" % int(LAUNCH_SPEED))
				check(max_speed_jump < MAX_SPEED_JUMP, "keeps its momentum across frame changes (largest speed jump %.2f m/s per tick)" % max_speed_jump)
				check(max_pos_jump < MAX_POSITION_JUMP, "no position jumps while re-centring (largest step %.2f m)" % max_pos_jump)
				check(sys.get_origin().distance_to(origin_start) > 100.0, "the world re-centred (origin moved %.0f m)" % sys.get_origin().distance_to(origin_start))
				check(player.global_position.length() < 1600.0, "player stays near the origin (%.0f m)" % player.global_position.length())
				var moved := 0
				for name in start_positions:
					var b: Node3D = sys.get_node(NodePath(name))
					if name == "sun" or (sys.get_origin() + b.global_position).distance_to(start_positions[name]) > 1.0:
						moved += 1
				check(moved == start_positions.size(), "every body moved along its orbit (%d of %d)" % [moved, start_positions.size()])
				var moon = sys.get_node("moon")
				# Standing on the side of the Moon that faces Earth still pulls
				# you down onto the Moon at its own strength.
				var earth: Node3D = sys.get_node("earth")
				var toward_earth: Vector3 = (earth.global_position - moon.global_position).normalized()
				var near_side: Vector3 = moon.global_position + toward_earth * (moon.radius() + 1.0)
				var g: Vector3 = sys.get_gravity_at(near_side)
				check(g.normalized().dot(-toward_earth) > 0.99 and g.length() > 2.5, "the Moon's near side pulls toward the Moon (%.2f m/s2)" % g.length())
				check(not moon.has_detail() or moon.get_node_or_null("SurfaceCollision0") != null, "the Moon starts as a preview or already has full detail")
				moon.request_detail()
				phase = 3; phase_start = frame
		3:
			var moon = sys.get_node("moon")
			if moon.get_node_or_null("SurfaceCollision0") != null:
				check(true, "the Moon built full detail in the background (%.1f s)" % (t / 60.0))
				moon.release_detail()
				phase = 4; phase_start = frame
			elif t > 60 * 60:
				check(false, "the Moon built full detail within 60 s")
				_finish()
		4:
			if t == 3:
				var moon = sys.get_node("moon")
				check(moon.get_node_or_null("SurfaceCollision0") == null and moon.get_node_or_null("Preview") != null, "the Moon drops full detail again when released")
				phase = 5; phase_start = frame
		5:
			_check_map(t)
	return false

func _press_map() -> void:
	var press := InputEventAction.new()
	press.action = "open_map"
	press.pressed = true
	Input.parse_input_event(press)
	var release := InputEventAction.new()
	release.action = "open_map"
	Input.parse_input_event(release)

func _check_map(t: int) -> void:
	var map = scene.get_node("HUD/SystemMap")
	if t == 1:
		check(map.get_body_count() == start_positions.size(), "the map sees every body (%d)" % map.get_body_count())
		map.set_zoom(99)
		var widest_ok: bool = map.get_zoom() == 3
		map.set_zoom(-4)
		check(widest_ok and map.get_zoom() == 0, "map zoom stays within its steps")
		_press_map()
	elif t == 4:
		check(map.is_open(), "the map button opens the map")
		_press_map()
	elif t == 7:
		check(not map.is_open(), "the map button closes it again")
		_finish()

func _finish() -> void:
	for p in passes: print("solar_system_test: ok   - " + p)
	for f in failures: printerr("solar_system_test: FAIL - " + f)
	if failures.is_empty():
		print("solar_system_test: PASS (%d checks)" % passes.size())
		quit(0)
	else:
		quit(1)
