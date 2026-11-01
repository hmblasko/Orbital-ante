extends SceneTree
## Phase 5 feature test: Neptune's ocean.
## 1. Neptune's landing pole is a dry island above the sea.
## 2. On the sea floor the player is under water: the sea's murk replaces
##    the sky, and the suit's oxygen is used (no breathing water).
## 3. Water holds you up: sinking is slow compared with falling in air.
## 4. Purple bioluminescent flashes light the deep water.
## 5. Black-smoker vents line the mid-ocean ridge.
## Exits with code 1 on any failure.
##
## Run from the godot/ folder:
##   godot --headless --path . --fixed-fps 60 --script res://tests/ocean_test.gd

const SETTLE_FRAMES := 60
const MAX_DETAIL_FRAMES := 60 * 60
const REST_FRAMES := 90
const FLASH_FRAMES := 60 * 15
const VENTS := 6

var scene: Node
var player: CharacterBody3D
var sys: Node
var weather: Node
var neptune: Node3D
var frame := 0
var step := 0
var step_frame := 0
var failures: Array[String] = []
var passes: Array[String] = []
var seabed: Vector3
var o2_before := 0.0
var flashes := 0
var start_h := 0.0

func _initialize() -> void:
	scene = load("res://scenes/tests/solar_system.tscn").instantiate()
	root.add_child(scene)
	player = scene.get_node("Player")
	sys = scene.get_node("SolarSystem")

func check(ok: bool, what: String) -> void:
	if ok: passes.append(what)
	else: failures.append(what)

func finish() -> void:
	for p in passes: print("ocean_test: ok   - " + p)
	for f in failures: print("ocean_test: FAIL - " + f)
	print("ocean_test: " + ("PASS" if failures.is_empty() else "FAIL"))
	quit(0 if failures.is_empty() else 1)

func ground(dir: Vector3) -> Vector3:
	var d := dir.normalized()
	return d * neptune.surface_radius_toward(neptune.global_transform * (d * neptune.radius()))

func place_at(local: Vector3) -> void:
	sys.debug_place_player_on("neptune", local)

func height_above(local_ground: Vector3) -> float:
	var l: Vector3 = neptune.global_transform.affine_inverse() * player.global_position
	return l.length() - local_ground.length()

func _physics_process(_d: float) -> bool:
	frame += 1
	if frame < SETTLE_FRAMES: return false
	step_frame += 1
	match step:
		0:
			neptune = sys.get_node("neptune")
			weather = sys.get_node("Weather")
			# The deepest of a few spots well away from the island.
			var best := Vector3.ZERO
			for dir in [Vector3.DOWN, Vector3(1, -0.3, 0), Vector3(-1, 0, 0.3), Vector3(0, -0.2, 1), Vector3(0.3, -0.4, 0.86)]:
				var g := ground(dir)
				if best == Vector3.ZERO or g.length() < best.length(): best = g
			seabed = best
			var depth: float = neptune.water_depth_at(neptune.global_transform * seabed)
			check(depth > 8.0, "the sea floor lies well under water (%.1f m)" % depth)
			place_at(ground(Vector3.UP) + Vector3.UP * 1.0)
			step = 1; step_frame = 0
		1:
			if neptune.has_detail():
				place_at(ground(Vector3.UP) + Vector3.UP * 1.0)
				step = 2; step_frame = 0
			elif step_frame > MAX_DETAIL_FRAMES:
				failures.append("Neptune ground never streamed in"); finish(); return true
		2:
			if step_frame == REST_FRAMES:
				check(player.is_on_floor() and not player.is_submerged(), "the landing pole is a dry island")
				place_at(seabed + seabed.normalized() * 1.0)
				step = 3; step_frame = 0
		3:
			if step_frame == 1:
				pass
			elif neptune.has_detail() and step_frame == REST_FRAMES:
				check(player.is_submerged(), "the player is under water on the sea floor")
				check(weather.is_underwater(), "the camera sees the underwater murk")
				check(weather.get_fog_density() >= 0.02, "the sea is murky (%.3f)" % weather.get_fog_density())
				o2_before = player.get_oxygen_seconds()
			elif step_frame == REST_FRAMES + 120:
				check(player.get_oxygen_seconds() < o2_before, "the suit's oxygen runs while under water")
				# Sink from 12 m above the floor for one second.
				place_at(seabed + seabed.normalized() * 12.0)
				start_h = 12.0
			elif step_frame == REST_FRAMES + 180:
				var dropped := start_h - height_above(seabed)
				# In air at Neptune's 9.5 m/s^2 you'd fall about 4.7 m in a second.
				check(dropped > 0.05 and dropped < 2.0, "water slows the sink (%.2f m in 1 s)" % dropped)
				var at: float = weather.debug_event_middle("neptune", "Bioluminescence")
				check(at >= 0.0, "Neptune's sea has bioluminescence")
				weather.debug_set_time(at)
				place_at(seabed + seabed.normalized() * 1.0)
				step = 4; step_frame = 0
			elif step_frame > MAX_DETAIL_FRAMES:
				failures.append("Neptune sea floor never streamed in"); finish(); return true
		4:
			if step_frame > 30 and step_frame < 30 + FLASH_FRAMES:
				if weather.is_flashing(): flashes += 1
			elif step_frame == 30 + FLASH_FRAMES:
				check(weather.get_event_name() == "Bioluminescence", "bioluminescence glows in the deep (%s)" % weather.get_event_name())
				check(flashes > 0, "purple flashes light the water (%d flash frames)" % flashes)
				var g: Node = neptune.get_node_or_null("Geysers")
				check(g != null and g.get_vent_positions().size() == VENTS, "black-smoker vents line the ridge")
				finish(); return true
	return false
