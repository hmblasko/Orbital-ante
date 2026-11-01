extends SceneTree
## Phase 2 feature test: suit, oxygen, supply point, tanks, jetpack, zero-g
## inertia, match velocity, tether and suffocation death.
## Each step moves the player somewhere, presses inputs exactly like a
## player would, and checks the result. Exits with code 1 on any failure.
##
## Run from the godot/ folder:
##   godot --headless --path . --fixed-fps 60 --script res://tests/suit_feature_test.gd

const FEET := 0.9                     # capsule half-height (data/player/movement.ron)
const OXYGEN_REFILL_PER_S := 40.0     # data/player/suit.ron
const OXYGEN_PER_TANK := 90.0         # data/player/suit.ron
const TETHER_LENGTH := 25.0           # data/player/interactables.ron
const SUFFOCATION_SECONDS := 10.0     # health 100 / 10 per second
const ZERO_G_POINT := Vector3(-1500, 0, 0)  # outside both test bodies' gravity

var scene: Node
var player: CharacterBody3D
var frame := 0
var step := 0
var step_start := 0
var mark := {}
var failures: Array[String] = []
var passes: Array[String] = []
var died_key := ""

func _initialize() -> void:
	scene = load("res://scenes/tests/sphere_walk.tscn").instantiate()
	# Tanks have no model yet, so the play scene has none; this test adds them.
	for body_name in ["TestSphere", "TestMoonlet"]:
		var scatter = ClassDB.instantiate("OxygenTankScatter")
		scatter.name = "OxygenTanks"
		scatter.body_index = 0 if body_name == "TestSphere" else 1
		scene.get_node(body_name).add_child(scatter)
	root.add_child(scene)
	player = scene.get_node("Player")
	player.died.connect(func(key): died_key = key)

func check(ok: bool, what: String) -> void:
	if ok: passes.append(what)
	else: failures.append(what)

func t() -> int:
	return frame - step_start

func next() -> void:
	step += 1
	step_start = frame

func place(pos: Vector3, velocity := Vector3.ZERO) -> void:
	player.global_position = pos
	player.velocity = velocity

func ground_clearance() -> float:
	var body: Node3D = scene.get_node("TestSphere")
	return player.global_position.length() - FEET - body.surface_radius_toward(player.global_position)

func on_top() -> Vector3:
	var body: Node3D = scene.get_node("TestSphere")
	return Vector3(0, body.surface_radius_toward(Vector3.UP * 100.0) + FEET + 0.3, 0)

func _physics_process(_delta: float) -> bool:
	frame += 1
	match step:
		0: # settle on the sphere
			if t() == 120:
				mark.o2 = player.get_oxygen_seconds()
				next()
		1: # oxygen drains about one second per second in vacuum
			if t() == 300:
				var used: float = mark.o2 - player.get_oxygen_seconds()
				check(absf(used - 5.0) < 0.2, "oxygen drains in vacuum (used %.2f s of air in 5 s)" % used)
				# The supply is inside the parked lander: stand beside its hull.
				player.debug_set_oxygen(100.0)
				place(beside_lander())
				next()
		2: # the supply point refills oxygen
			if t() == 120:
				var o2: float = player.get_oxygen_seconds()
				check(o2 > 100.0 + OXYGEN_REFILL_PER_S * 1.5, "supply point refills oxygen (100 -> %.1f s in 2 s)" % o2)
				var tanks := get_nodes_in_group("oxygen_tanks")
				check(tanks.size() == 8, "4 seeded tanks on each body (found %d)" % tanks.size())
				var tank: Node3D = null
				for n in tanks:
					if n.get_parent().get_parent().name == "TestSphere": tank = n; break
				mark.tank = tank
				place(tank.global_position + tank.global_basis.y * 1.0)
				next()
		3: # pick up a tank with Interact
			if t() == 5: Input.action_press("interact")
			if t() == 6: Input.action_release("interact")
			if t() == 10:
				check(player.get_tanks() == 1, "Interact picks up a tank (carrying %d)" % player.get_tanks())
				player.debug_set_oxygen(10.0)
				Input.action_press("use_oxygen_tank")
			if t() == 11: Input.action_release("use_oxygen_tank")
			if t() == 15:
				var o2: float = player.get_oxygen_seconds()
				check(player.get_tanks() == 0 and absf(o2 - (10.0 + OXYGEN_PER_TANK)) < 0.5, "using a tank adds %d s of air (now %.1f s)" % [OXYGEN_PER_TANK, o2])
				place(on_top())
				next()
		4: # jetpack: holding A after a jump flies far higher than a plain jump
			if t() == 60:
				mark.fuel = player.get_fuel_fraction()
				mark.max_r = 0.0
				Input.action_press("jump")
			if t() == 120: Input.action_release("jump")
			if t() > 60: mark.max_r = maxf(mark.max_r, ground_clearance())
			if t() == 240:
				var height: float = mark.max_r
				check(height > 4.0, "jetpack lifts the player (apex %.1f m; a plain jump is about 1.7 m)" % height)
				check(player.get_fuel_fraction() < mark.fuel, "jetpack burns fuel (%.2f -> %.2f)" % [mark.fuel, player.get_fuel_fraction()])
				place(ZERO_G_POINT)
				next()
		5: # zero-g keeps its momentum; match velocity stops it
			# Push one tick after the teleport: a body that was standing on the
			# ground loses part of its first slide to floor snapping.
			if t() == 2: player.velocity = Vector3(3, 0, 0)
			if t() == 60:
				check(player.is_in_zero_g(), "zero-g detected away from both bodies")
				check(player.velocity.distance_to(Vector3(3, 0, 0)) < 0.01, "inertia: still drifting at %s after 1 s with no input" % player.velocity)
				Input.action_press("match_velocity")
			if t() == 120:
				Input.action_release("match_velocity")
				check(player.velocity.length() < 0.01, "match velocity stops the drift (speed %.3f m/s)" % player.velocity.length())
				var anchor: Node3D = get_nodes_in_group("tether_anchors")[0]
				mark.anchor = anchor
				place(beside_lander())
				next()
		6: # tether: clip on, then a hard push away is stopped at the line's length
			if t() == 5: Input.action_press("tether_toggle")
			if t() == 6: Input.action_release("tether_toggle")
			if t() == 10:
				check(player.is_tethered(), "tether clips on near an anchor")
				player.velocity = mark.anchor.global_basis.y * 30.0
				mark.max_d = 0.0
			if t() > 10: mark.max_d = maxf(mark.max_d, player.global_position.distance_to(mark.anchor.global_position))
			if t() == 190:
				check(mark.max_d <= TETHER_LENGTH + 1.0, "tether holds at %.0f m (furthest %.2f m after a 30 m/s push)" % [TETHER_LENGTH, mark.max_d])
				Input.action_press("tether_toggle")
			if t() == 191: Input.action_release("tether_toggle")
			if t() == 195:
				check(not player.is_tethered(), "tether unclips")
				place(on_top())
				next()
		7: # out of air: health drains and the player dies of suffocation
			if t() == 30: player.debug_set_oxygen(0.0)
			if t() == 30 + int(60 * (SUFFOCATION_SECONDS - 1.0)):
				check(not player.is_dead(), "still alive just before the suffocation time")
			if t() == 30 + int(60 * (SUFFOCATION_SECONDS + 0.5)):
				check(player.is_dead() and died_key == "DEATH_SUFFOCATION", "dies of suffocation about %d s after air runs out (cause '%s')" % [SUFFOCATION_SECONDS, died_key])
				check(tr(died_key) != died_key, "death recap line is in the string table")
				_finish()
	return false

func _finish() -> void:
	for p in passes: print("suit_feature_test: ok   - " + p)
	for f in failures: printerr("suit_feature_test: FAIL - " + f)
	if failures.is_empty():
		print("suit_feature_test: PASS (%d checks)" % passes.size())
		quit(0)
	else:
		quit(1)

## A spot on the ground just outside the parked lander's hull, between two legs.
func beside_lander() -> Vector3:
	var supply: Node3D = get_nodes_in_group("supply_points")[0]
	var b: Basis = supply.get_parent().global_basis
	var up: Vector3 = supply.global_position.normalized()
	var out: Vector3 = (b.x.normalized() + b.z.normalized()).normalized()
	return supply.global_position + out * 5.0 + up * 0.6
