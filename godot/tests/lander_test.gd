extends SceneTree
## Phase 4 feature test: the starter lander.
## 1. The lander starts parked upright on Earth near the player, at rest.
## 2. Standing under its belly ring, the gravity lift carries the player up
##    into the cabin, and the hatch shuts behind them.
## 3. The player buckles in at the seat.
## 4. Full up-thrust lifts the ship off, with the player still in the seat.
## 5. Match velocity brings the ship to a hover; turning stops when the
##    stick is let go (flight assist).
## 6. A gentle descent lands it upright again.
## 7. The player unbuckles, opens the hatch, drops out through the lift and
##    walks away, leaving the ship (no speed jump on the way out).
## Exits with code 1 on any failure.
##
## Run from the godot/ folder:
##   godot --headless --path . --fixed-fps 60 --script res://tests/lander_test.gd

const SETTLE := 180
const LIFT_LIMIT := 60 * 8
const THRUST_FRAMES := 60 * 3
const MATCH_FRAMES := 60 * 3
const TURN_FRAMES := 60
const LAND_LIMIT := 60 * 40

var scene: Node
var player: CharacterBody3D
var ship: RigidBody3D
var frame := 0
var phase := 0
var phase_start := 0
var failures: Array[String] = []
var passes: Array[String] = []
var start_up := Vector3.ZERO
var start_height := 0.0
var max_seat_gap := 0.0
var yaw_start: Vector3
var calm_frames := 0
var last_player_abs := Vector3.ZERO
var max_exit_jump := 0.0

func _initialize() -> void:
	scene = load("res://scenes/tests/lander.tscn").instantiate()
	root.add_child(scene)
	player = scene.get_node("Player")
	ship = scene.get_node("Lander")

func check(ok: bool, what: String) -> void:
	if ok: passes.append(what)
	else: failures.append(what)

func earth() -> Node3D:
	return scene.get_node("SolarSystem/earth")

func radial_up() -> Vector3:
	return (ship.global_position - earth().global_position).normalized()

func height() -> float:
	return (ship.global_position - earth().global_position).length()

func at_ship(local: Vector3) -> Vector3:
	return ship.global_transform * local

func place_player(local: Vector3, facing_back := false) -> void:
	# Faces the ship's front (+Z) by default; facing_back looks toward the hatch.
	var t: Transform3D = ship.global_transform
	var turn := Basis.IDENTITY if facing_back else Basis(Vector3.UP, PI)
	player.global_transform = Transform3D(t.basis * turn, t * local)
	player.velocity = Vector3.ZERO

func release_all() -> void:
	for a in ["jump", "crouch", "match_velocity", "look_right", "look_left", "move_forward"]:
		Input.action_release(a)

func next() -> void:
	phase += 1
	phase_start = frame

func _physics_process(_delta: float) -> bool:
	frame += 1
	var t := frame - phase_start
	match phase:
		0:
			if t == SETTLE:
				var dist := ship.global_position.distance_to(player.global_position)
				check(dist < 40.0, "the lander is parked near the player (%.1f m)" % dist)
				check(ship.global_basis.y.normalized().dot(radial_up()) > 0.97, "it stands upright (%.1f deg tilt)" % rad_to_deg(acos(clampf(ship.global_basis.y.normalized().dot(radial_up()), -1.0, 1.0))))
				check(ship.get_speed() < 0.5, "it is at rest on the ground (%.2f m/s)" % ship.get_speed())
				# Stand under the belly ring.
				var feet_up: float = 0.9 + 0.05
				place_player(Vector3(0.0, feet_up, -1.4))
				next()
		1:
			if ship.get_player_zone() == "cabin" and player.is_on_floor():
				check(true, "the gravity lift carries the player up into the cabin (%.1f s)" % (t / 60.0))
				next()
			elif t > LIFT_LIMIT:
				check(false, "the gravity lift carries the player up into the cabin (zone '%s', local %s)" % [ship.get_player_zone(), ship.global_transform.affine_inverse() * player.global_position])
				_finish()
		2:
			if t == 90:
				check(ship.get_hatch_open() < 0.05, "the hatch shuts behind the player")
				# Walk up to the seat and buckle in.
				place_player(Vector3(-0.9, ship.get_cabin_floor_y() + 0.95, 0.35))
			if t == 100:
				player.debug_interact()
			if t == 140:
				check(player.is_buckled() and ship.is_pilot_aboard(), "the player buckles in at the seat")
				start_up = earth().global_basis.inverse() * radial_up()
				start_height = height()
				Input.action_press("jump")
				next()
		3:
			max_seat_gap = max(max_seat_gap, player.global_position.distance_to(at_ship(Vector3(0.0, 6.75, 0.35))))
			if t == THRUST_FRAMES:
				Input.action_release("jump")
				var climb := height() - start_height
				check(climb > 10.0, "full up-thrust lifts the lander off (climbed %.1f m in 3 s)" % climb)
				check(player.is_buckled() and max_seat_gap < 0.1, "the pilot stays in the seat in flight (largest gap %.3f m)" % max_seat_gap)
				Input.action_press("match_velocity")
				next()
		4:
			if t == MATCH_FRAMES:
				check(ship.get_speed() < 0.6, "match velocity brings it to a hover (%.2f m/s)" % ship.get_speed())
				yaw_start = ship.global_basis.z
				Input.action_press("look_right")
				next()
		5:
			if t == TURN_FRAMES:
				Input.action_release("look_right")
				var turned := rad_to_deg(yaw_start.angle_to(ship.global_basis.z))
				check(turned > 30.0, "the stick turns the ship (%.0f deg in 1 s)" % turned)
			if t == TURN_FRAMES + 60:
				var spin: float = rad_to_deg(ship.angular_velocity.length())
				check(spin < 1.0, "flight assist stops the turn when the stick is let go (%.2f deg/s)" % spin)
				Input.action_release("match_velocity")
				next()
		6:
			# Gentle descent: fire up-thrust whenever falling faster than 3 m/s.
			# Like a careful pilot: cancel sideways drift with match velocity.
			var v_up: float = ship.linear_velocity.dot(radial_up())
			var v_side: float = (ship.linear_velocity - radial_up() * v_up).length()
			if v_up < -3.0:
				Input.action_press("jump", 0.9)
			else:
				Input.action_release("jump")
			if v_side > 0.5:
				Input.action_press("match_velocity")
			else:
				Input.action_release("match_velocity")
			if ship.get_speed() < 0.3 and height() - start_height < 3.0:
				calm_frames += 1
			else:
				calm_frames = 0
			if calm_frames > 60:
				release_all()
				var tilt := rad_to_deg(acos(clampf(ship.global_basis.y.normalized().dot(radial_up()), -1.0, 1.0)))
				check(tilt < 15.0, "it lands upright again (%.1f deg tilt, %.1f s)" % [tilt, t / 60.0])
				player.debug_interact()
				next()
			elif t > LAND_LIMIT:
				release_all()
				check(false, "it lands within 40 s (height %.1f m above start, %.1f m sideways, speed %.2f m/s)" % [height() - start_height, start_up.angle_to(earth().global_basis.inverse() * radial_up()) * start_height, ship.get_speed()])
				_finish()
		7:
			if t == 30:
				check(not player.is_buckled() and ship.get_player_zone() == "cabin", "the player unbuckles and stands in the cabin")
				place_player(Vector3(0.0, ship.get_cabin_floor_y() + 0.95, -0.2), true)
			if t == 60:
				player.debug_interact()
			if t == 120:
				check(ship.get_hatch_open() > 0.95, "the hatch opens from inside")
				place_player(Vector3(0.0, ship.get_cabin_floor_y() + 0.95, -1.4), true)
			if t > 120 and ship.get_player_zone() == "lift":
				next()
			elif t > 300:
				check(false, "stepping onto the open hatch drops the player into the lift (zone '%s')" % ship.get_player_zone())
				_finish()
		8:
			var local: Vector3 = ship.global_transform.affine_inverse() * player.global_position
			if local.y < 1.9 and player.is_on_floor():  # feet below the belly ring, on the ground
				check(true, "the lift lowers the player to the ground (%.1f s)" % (t / 60.0))
				last_player_abs = player.global_position - ship.global_position
				# Duck under the descent stage and walk out.
				Input.action_press("crouch")
				Input.action_press("move_forward")
				next()
			elif t > 60 * 10:
				check(false, "the lift lowers the player to the ground (local %s)" % local)
				_finish()
		9:
			var rel := player.global_position - ship.global_position
			max_exit_jump = max(max_exit_jump, rel.distance_to(last_player_abs))
			last_player_abs = rel
			if ship.get_player_zone() == "outside" and t > 30:
				Input.action_release("move_forward")
				check(not player.is_aboard(), "walking out from under the ship leaves it")
				check(max_exit_jump < 0.3, "no jump while leaving the ship (largest step %.3f m)" % max_exit_jump)
				_finish()
			elif t > 60 * 8:
				Input.action_release("move_forward")
				check(false, "walking out from under the ship leaves it (zone '%s')" % ship.get_player_zone())
				_finish()
	return false

func _finish() -> void:
	release_all()
	for p in passes: print("lander_test: ok   - " + p)
	for f in failures: printerr("lander_test: FAIL - " + f)
	if failures.is_empty():
		print("lander_test: PASS (%d checks)" % passes.size())
		quit(0)
	else:
		quit(1)
