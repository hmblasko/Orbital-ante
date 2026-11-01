extends SceneTree
## Phase 5 feature test: the Moon's lava tube.
## 1. The Moon's surface kit is placed: mares, the ring basin, rilles,
##    crater chains, rayed craters and shadowed craters.
## 2. The Moon has a lava tube, and its mouth sits low in a skylight pit.
## 3. Standing deep inside the tube, the player rests on its floor (no
##    falling through) with rock overhead, and the helmet lamp comes on.
## 4. Walking out through the mouth works: the player stays on solid ground
##    the whole way and reaches the pit floor.
## 5. Back up on the open plain in daylight, the lamp goes off.
## Exits with code 1 on any failure.
##
## Run from the godot/ folder:
##   godot --headless --path . --fixed-fps 60 --script res://tests/cave_test.gd

const SETTLE_FRAMES := 60
const MAX_DETAIL_FRAMES := 60 * 60
const REST_FRAMES := 90
const WALK_FRAMES := 60 * 14

var scene: Node
var player: CharacterBody3D
var sys: Node
var moon: Node3D
var frame := 0
var phase := 0
var phase_start := 0
var failures: Array[String] = []
var passes: Array[String] = []
var cave: Dictionary
var lowest_drop := 1e9
var start_local: Vector3

func _initialize() -> void:
	scene = load("res://scenes/tests/solar_system.tscn").instantiate()
	root.add_child(scene)
	player = scene.get_node("Player")
	sys = scene.get_node("SolarSystem")

func check(ok: bool, what: String) -> void:
	if ok: passes.append(what)
	else: failures.append(what)

func finish() -> void:
	for p in passes: print("cave_test: ok   - " + p)
	for f in failures: print("cave_test: FAIL - " + f)
	print("cave_test: " + ("PASS" if failures.is_empty() else "FAIL"))
	quit(0 if failures.is_empty() else 1)

func local(p: Vector3) -> Vector3:
	return moon.global_transform.affine_inverse() * p

func _physics_process(_d: float) -> bool:
	frame += 1
	var t := frame - phase_start
	match phase:
		0:
			if frame == SETTLE_FRAMES:
				moon = sys.get_node("moon")
				moon.request_detail()
				phase = 1; phase_start = frame
		1:
			if moon.has_detail():
				var kinds := {}
				for l in moon.get_landforms(): kinds[l["kind"]] = true
				for k in ["Mare", "Basin", "Rille", "Bowl", "RayedCrater", "ColdTrap", "Crater", "Sinkhole"]:
					check(kinds.has(k), "the Moon has a %s" % k)
				var caves: Array = moon.get_caves()
				check(caves.size() >= 1, "the Moon has %d lava tube(s)" % caves.size())
				if caves.is_empty(): return finish_now()
				cave = caves[0]
				var r: float = moon.radius()
				check(cave["mouth"].length() < r - 8.0, "the mouth sits %.1f m below the plain" % (r - cave["mouth"].length()))
				var inside: Vector3 = cave["inside"]
				sys.debug_place_player_on("moon", inside + inside.normalized() * 1.0)
				start_local = inside
				phase = 2; phase_start = frame
			elif t > MAX_DETAIL_FRAMES:
				check(false, "Moon detail built in time")
				return finish_now()
		2:
			if t == REST_FRAMES:
				var here := local(player.global_position)
				check(player.is_on_floor(), "stands on the tube floor")
				check(absf(here.length() - start_local.length()) < 1.5, "rests on the floor, no fall (%.2f m from it)" % (here.length() - start_local.length()))
				var above: float = moon.surface_radius_toward(player.global_position) - here.length()
				check(above > 3.0, "%.1f m of rock overhead" % above)
				check(player.is_lamp_on(), "helmet lamp is on in the dark")
				phase = 3; phase_start = frame
		3:
			# Walk toward the mouth.
			var here := local(player.global_position)
			var to_mouth: Vector3 = moon.global_transform.basis * (cave["mouth"] - here)
			var up := (player.global_position - moon.global_position).normalized()
			var flat := (to_mouth - up * to_mouth.dot(up))
			player.debug_walk_toward(player.global_position + flat)
			lowest_drop = minf(lowest_drop, here.length() - (moon.radius() - 40.0))
			if here.distance_to(cave["mouth"]) < 2.5 or t > WALK_FRAMES:
				check(here.distance_to(cave["mouth"]) < 2.5, "walked out to the mouth (%.1f m from it)" % here.distance_to(cave["mouth"]))
				check(lowest_drop > 0.0, "never fell out of the world on the way")
				player.debug_walk_toward(Vector3.ZERO)
				# Up onto the plain in daylight.
				var plain: Vector3 = moon.get_landforms()[0]["dir"]
				sys.debug_place_player_on("moon", plain * (moon.surface_radius_toward(moon.global_transform * (plain * moon.radius())) + 1.5))
				phase = 4; phase_start = frame
		4:
			if t == REST_FRAMES:
				check(not player.is_lamp_on(), "lamp is off on the open plain")
				return finish_now()
	return false

func finish_now() -> bool:
	finish()
	return true
