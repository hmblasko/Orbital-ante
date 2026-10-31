extends SceneTree
## Phase 4 part 3 feature test: takeoff effects.
## 1. Parked with the engine off: no plume, cloud or grit.
## 2. Full thrust on the pad: flame plume, ground cloud and flying grit.
## 3. High above the ground (autopilot hop to 120 m): the plume still
##    burns but the ground cloud and grit stop.
## 4. Engine off again: everything stops.

var scene: Node
var player: CharacterBody3D
var ship: RigidBody3D
var frame := 0
var failures: Array[String] = []
var passes: Array[String] = []

func _initialize() -> void:
	scene = load("res://scenes/tests/lander.tscn").instantiate()
	root.add_child(scene)
	player = scene.get_node("Player")
	ship = scene.get_node("Lander")

func check(ok: bool, what: String) -> void:
	if ok: passes.append(what)
	else: failures.append(what)

func fx() -> Array:
	return Array(ship.get_takeoff_emitting())

func nearest_body() -> Node3D:
	var best: Node3D = null
	for b in get_nodes_in_group("gravity_wells"):
		var bn := b as Node3D
		if best == null or bn.global_position.distance_to(ship.global_position) - bn.radius() < best.global_position.distance_to(ship.global_position) - best.radius():
			best = bn
	return best

func finish() -> void:
	for p in passes: print("takeoff_test: ok   - " + p)
	for f in failures: print("takeoff_test: FAIL - " + f)
	print("takeoff_test: " + ("PASS" if failures.is_empty() else "FAIL"))
	quit(0 if failures.is_empty() else 1)

func _physics_process(_d: float) -> bool:
	frame += 1
	match frame:
		60:
			var t: Transform3D = ship.global_transform
			player.global_transform = Transform3D(t.basis * Basis(Vector3.UP, PI), t * Vector3(-0.9, ship.get_cabin_floor_y() + 0.95, 0.35))
			player.velocity = Vector3.ZERO
		70:
			player.debug_interact()
		90:
			check(player.is_buckled(), "the pilot buckles in")
			check(fx() == [0, 0, 0], "parked with the engine off: no plume, cloud or grit (%s)" % [fx()])
			Input.action_press("jump")
		110:
			check(fx() == [1, 1, 1], "full thrust on the pad: plume, ground cloud and grit (%s)" % [fx()])
			Input.action_release("jump")
			ship.lock_body(nearest_body())
			Input.action_press("autopilot")
		111:
			Input.action_release("autopilot")
	if frame > 111 and not has_meta("high") and ship.is_holding_match():
		set_meta("high", frame)
	if has_meta("high") and frame == int(get_meta("high")) + 180:
		# Settled into a hover at 120 m, well above the cloud height.
		var e := fx()
		check(e[1] == 0 and e[2] == 0, "high above the ground: no ground cloud or grit (%s)" % [e])
		check(e[0] == 1, "the plume still burns while hovering (%s)" % [e])
		player.debug_interact()
	if has_meta("high") and frame == int(get_meta("high")) + 210:
		check(fx() == [0, 0, 0], "engine off after unbuckling: everything stops (%s)" % [fx()])
		finish()
	if frame > 3000:
		check(false, "the autopilot hop finished in time")
		finish()
	return false
