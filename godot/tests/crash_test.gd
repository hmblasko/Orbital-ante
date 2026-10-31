extends SceneTree
## Phase 4 part 3 feature test: crash damage.
## 1. A normal landing does no damage.
## 2. Buckled in, slamming into the ground damages the parts that hit and
##    the hull, with the pilot unhurt.
## 3. Slamming down again rips a part off: it becomes debris and is gone
##    from the ship.
## 4. Unbuckled, a hard crash kills you, with a one-line cause.

var scene: Node
var player: CharacterBody3D
var ship: RigidBody3D
var frame := 0
var failures: Array[String] = []
var passes: Array[String] = []
const LEGS := ["leg_right", "leg_front", "leg_left", "leg_back"]

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

## Lift the ship `height` m and fire it at the ground at `speed`, tipped
## `tilt` degrees so one side lands first.
func slam(height: float, speed: float, tilt: float) -> void:
	var u := up()
	var t: Transform3D = ship.global_transform
	t.origin += u * height
	t.basis = Basis(t.basis.x.normalized(), deg_to_rad(tilt)) * t.basis
	ship.global_transform = t
	ship.linear_velocity = -u * speed
	ship.angular_velocity = Vector3.ZERO

func leg_health() -> Array:
	var out := []
	for l in LEGS: out.append(snappedf(ship.get_part_health(l), 0.01))
	return out

func finish() -> void:
	for p in passes: print("crash_test: ok   - " + p)
	for f in failures: print("crash_test: FAIL - " + f)
	print("crash_test: " + ("PASS" if failures.is_empty() else "FAIL"))
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
			check(ship.get_impact_count() == 0 and ship.get_hull_fraction() == 1.0, "parking and settling did no damage")
			# Lift only 0.3 m so the legs touch down at about 3 m/s (a 4 m drop
			# lands at 8 m/s, which is not a gentle landing on Earth).
			slam(0.3, 2.5, 0.0)
		200:
			check(ship.get_impact_count() == 0 and ship.get_hull_fraction() == 1.0, "a gentle 2.5 m/s landing does no damage (hull %.3f, impacts %d)" % [ship.get_hull_fraction(), ship.get_impact_count()])
			slam(12.0, 14.0, 20.0)
		320:
			var legs := leg_health()
			var dented := legs.any(func(h): return h < 1.0)
			check(ship.get_impact_count() > 0 and dented and ship.get_hull_fraction() < 1.0, "a 14 m/s tipped slam damages legs and hull (legs %s, hull %.2f)" % [legs, ship.get_hull_fraction()])
			check(player.get_death_key() == "" and player.get_health_fraction() == 1.0, "buckled in, the pilot is unhurt (health %.2f)" % player.get_health_fraction())
			slam(14.0, 24.0, 25.0)
		440:
			slam(14.0, 24.0, 25.0)
		560:
			var legs := leg_health()
			var debris := get_nodes_in_group("ship_debris")
			check(legs.has(-1.0) and debris.size() > 0, "repeated hard slams rip a leg off as debris (legs %s, %d pieces)" % [legs, debris.size()])
			if debris.size() > 0:
				var piece: Node3D = debris[0]
				check(piece.get_child_count() > 1, "the piece carries the leg's own mesh (%d children)" % piece.get_child_count())
			check(player.get_death_key() == "", "still alive while buckled (health %.2f)" % player.get_health_fraction())
			player.debug_interact()
		580:
			check(not player.is_buckled(), "unbuckled")
			slam(14.0, 26.0, 0.0)
		700:
			check(player.get_death_key() == "DEATH_CRASH", "unbuckled, a hard crash kills you ('%s')" % player.get_death_key())
			finish()
	return false
