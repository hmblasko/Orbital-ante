extends SceneTree
## Feature test: hunter swarms (they hunt you down in growing packs).
## 1. The plan escalates: ante 8 packs are bigger and come far more often
##    than ante 1's, and Endless keeps growing.
## 2. At the home base you are safe; out on the Moon a pack can come.
## 3. A called pack warns first (red flares, nobody landed yet), then
##    2-3 tier 1 hunters (ante 1) drop in and go straight for you.
## 4. Leave them far behind and they give up and go.
## Exits with code 1 on any failure.
##
## Run from the godot/ folder:
##   godot --headless --path . --fixed-fps 60 --script res://tests/swarm_test.gd

const MAX_FRAMES := 60 * 80

var scene: Node
var player: CharacterBody3D
var sys: Node
var moon: Node3D
var swarm: Node
var frame := 0
var phase := 0
var wait_until := 0
var mark := 0
var failures: Array[String] = []
var passes: Array[String] = []
var start_gap := 0.0


func _initialize() -> void:
	SaveData.set_autosave_enabled(false)
	Points.set_screens_mode(0)
	Points.start_run()
	scene = load("res://scenes/tests/solar_system.tscn").instantiate()
	root.add_child(scene)
	player = scene.get_node("Player")
	sys = scene.get_node("SolarSystem")


func check(ok: bool, what: String) -> void:
	if ok: passes.append(what)
	else: failures.append(what)


func finish() -> void:
	for p in passes: print("swarm_test: ok   - " + p)
	for f in failures: print("swarm_test: FAIL - " + f)
	print("swarm_test: " + ("PASS" if failures.is_empty() else "FAIL"))
	Points.set_screens_mode(-1)
	quit(0 if failures.is_empty() else 1)


func hunters() -> Array:
	var out := []
	for h in get_nodes_in_group("hunters"):
		if not h.is_queued_for_deletion() and not h.is_dead(): out.append(h)
	return out


func flares() -> int:
	var n := 0
	for c in moon.get_children():
		if String(c.name).begins_with("SwarmFlare") and not c.is_queued_for_deletion(): n += 1
	return n


func mean_gap() -> float:
	var hs := hunters()
	if hs.is_empty(): return 0.0
	var t := 0.0
	for h in hs: t += h.global_position.distance_to(player.global_position)
	return t / hs.size()


func place_on_moon(dir: Vector3) -> void:
	var d := dir.normalized()
	var ground: float = moon.surface_radius_toward(moon.global_transform * (d * 100.0))
	sys.debug_place_player_on("moon", d * (ground + 1.0))


func _process(_d: float) -> bool:
	frame += 1
	if frame > MAX_FRAMES:
		check(false, "test finished in time (stuck in phase %d)" % phase)
		finish(); return true
	if frame < wait_until:
		return false
	match phase:
		0:
			if frame < 10: return false
			swarm = get_first_node_in_group("swarm_director")
			check(swarm != null, "the swarm director runs in the ante scene")
			if swarm == null: finish(); return true
			var a1: Vector3 = swarm.get_ante_plan(1)
			var a8: Vector3 = swarm.get_ante_plan(8)
			var a10: Vector3 = swarm.get_ante_plan(10)
			check(a8.y >= a1.y * 3.0, "ante 8 packs are much bigger (%d-%d vs %d-%d)" % [a8.x, a8.y, a1.x, a1.y])
			check(a8.z <= a1.z * 0.5, "ante 8 packs come far more often (every %.0f s vs %.0f s)" % [a8.z, a1.z])
			check(a10.y > a8.y and a10.z < a8.z, "Endless keeps growing")
			check(not swarm.can_hunt_now(), "the home base is a safe ring")
			moon = sys.get_node("moon")
			moon.request_detail()
			# Far from the Moon camp: the far side of the landing pole.
			place_on_moon(Vector3(0.5, -0.2, 0.84))
			phase = 1
		1:
			if not moon.has_detail():
				place_on_moon(Vector3(0.5, -0.2, 0.84))
				return false
			place_on_moon(Vector3(0.5, -0.2, 0.84))
			phase = 2; wait_until = frame + 30
		2:
			check(swarm.can_hunt_now(), "out on the Moon, on foot, a pack can come for you")
			swarm.debug_call_now()
			phase = 3; wait_until = frame + 3
		3:
			check(swarm.is_incoming(), "a called pack is announced first")
			check(flares() >= 2, "red flares mark where they will land (%d)" % flares())
			check(hunters().is_empty(), "nobody lands during the warning")
			phase = 4; wait_until = frame + 60 * 5
		4:
			var hs := hunters()
			check(not swarm.is_incoming() and flares() == 0, "the flares go out when they land")
			check(hs.size() >= 2 and hs.size() <= 3, "ante 1: a pack of 2-3 hunters (%d)" % hs.size())
			check(hs.all(func(h): return h.get_tier() == 1), "ante 1 hunters are all tier 1")
			check(hs.all(func(h): return h.get_state_name() in ["Alert", "Search"]), "they come for you at once (%s)" % [hs.map(func(h): return h.get_state_name())])
			var d := mean_gap()
			check(d > 25.0 and d < 100.0, "they land a fair distance away (%.0f m)" % d)
			start_gap = d
			phase = 5; wait_until = frame + 60 * 10
		5:
			var d := mean_gap()
			check(d < start_gap - 8.0, "they close in on you (%.0f m -> %.0f m)" % [start_gap, d])
			# Run far away (the Moon's far side): they give up and go.
			place_on_moon(Vector3(-0.5, 0.2, -0.84))
			phase = 6; mark = frame
		6:
			if not hunters().is_empty() and frame - mark < 60 * 10:
				place_on_moon(Vector3(-0.5, 0.2, -0.84))
				return false
			check(hunters().is_empty(), "left far behind, the hunters give up and leave")
			check(swarm.get_packs_dropped() == 1, "one pack dropped (%d)" % swarm.get_packs_dropped())
			finish(); return true
	return false
