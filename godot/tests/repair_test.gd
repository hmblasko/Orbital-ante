extends SceneTree
## Phase 4 part 3 feature test: repairs.
## 1. A buckled crash dents a leg.
## 2. Standing outside, looking at the dented leg, the prompt offers to
##    repair it by name.
## 3. Holding X fixes it to full in a few seconds.

var scene: Node
var player: CharacterBody3D
var ship: RigidBody3D
var frame := 0
var failures: Array[String] = []
var passes: Array[String] = []
const LEGS := {"leg_right": 0.0, "leg_front": 90.0, "leg_left": 180.0, "leg_back": -90.0}
var leg := ""
var held_from := 0

func _initialize() -> void:
	scene = load("res://scenes/tests/lander.tscn").instantiate()
	root.add_child(scene)
	player = scene.get_node("Player")
	ship = scene.get_node("Lander")

func check(ok: bool, what: String) -> void:
	if ok: passes.append(what)
	else: failures.append(what)

func up() -> Vector3:
	var best: Node3D = null
	for b in get_nodes_in_group("gravity_wells"):
		var bn := b as Node3D
		if best == null or bn.global_position.distance_to(ship.global_position) - bn.radius() < best.global_position.distance_to(ship.global_position) - best.radius():
			best = bn
	return (ship.global_position - best.global_position).normalized()

func finish() -> void:
	for p in passes: print("repair_test: ok   - " + p)
	for f in failures: print("repair_test: FAIL - " + f)
	print("repair_test: " + ("PASS" if failures.is_empty() else "FAIL"))
	quit(0 if failures.is_empty() else 1)

func _physics_process(_d: float) -> bool:
	frame += 1
	if frame == 60:
		var t: Transform3D = ship.global_transform
		player.global_transform = Transform3D(t.basis * Basis(Vector3.UP, PI), t * Vector3(-0.9, ship.get_cabin_floor_y() + 0.95, 0.35))
		player.velocity = Vector3.ZERO
	elif frame == 70:
		player.debug_interact()
	elif frame == 90:
		var u := up()
		var t: Transform3D = ship.global_transform
		t.origin += u * 12.0
		t.basis = Basis(t.basis.x.normalized(), deg_to_rad(20.0)) * t.basis
		ship.global_transform = t
		ship.linear_velocity = -u * 14.0
	elif frame == 220:
		for id in LEGS:
			var h: float = ship.get_part_health(id)
			if h >= 0.0 and h < 1.0: leg = id
		check(leg != "", "the crash dented a leg")
		if leg == "":
			finish()
			return false
		player.debug_interact()
	elif frame == 230:
		check(not player.is_buckled(), "unbuckled")
		# Step outside, 2 m beyond the dented leg's foot, looking at the leg.
		var a := deg_to_rad(LEGS[leg])
		var out := Vector3(cos(a), 0.0, sin(a))
		var t: Transform3D = ship.global_transform
		var stand: Vector3 = t * (out * 9.5 + Vector3(0, 1.2, 0))
		var look_at: Vector3 = t * (out * 6.5 + Vector3(0, 1.5, 0))
		player.global_position = stand
		player.velocity = Vector3.ZERO
		player.look_at(Vector3(look_at.x, look_at.y, look_at.z), up())
	elif frame == 260:
		var prompt: String = player.get_interact_prompt()
		check(prompt.begins_with("repair:PART_LEG"), "looking at the dented leg offers a repair ('%s')" % prompt)
		player.debug_hold_repair(true)
		held_from = frame
		set_meta("start", ship.get_part_health(leg))
	elif held_from > 0:
		if ship.get_part_health(leg) >= 1.0:
			var secs := (frame - held_from) / 60.0
			check(secs < 4.0, "holding X fixed the leg from %.0f%% in %.1f s" % [float(get_meta("start")) * 100.0, secs])
			player.debug_hold_repair(false)
			finish()
		elif frame - held_from > 600:
			check(false, "the leg was fixed within 10 s (now %.2f, repairing %s, prompt '%s')" % [ship.get_part_health(leg), player.is_repairing(), player.get_interact_prompt()])
			finish()
	return false
