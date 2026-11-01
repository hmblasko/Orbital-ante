extends SceneTree
## Vertical slice feature test: the Moon buggy (CLAUDE.md section 5).
## 1. A seeded buggy is parked near the Moon's landing area, on the ground,
##    and another waits at the home base on Earth.
## 2. Walking up wakes it; it rests on all four wheels without sliding.
## 3. X climbs in; the stick drives, steers and brakes; A boosts and the
##    boost fuel recharges; X climbs out beside it.
## 4. Turned over, holding A rolls it back upright.
## 5. Left far behind, it parks again on the ground.
## Exits with code 1 on any failure.
##
## Run from the godot/ folder:
##   godot --headless --path . --fixed-fps 60 --script res://tests/rover_test.gd

const MAX_FRAMES := 60 * 90

var scene: Node
var player: CharacterBody3D
var sys: Node
var moon: Node3D
var rover: RigidBody3D
var frame := 0
var phase := 0
var wait_until := 0
var mark := 0
var failures: Array[String] = []
var passes: Array[String] = []
var start_pos := Vector3.ZERO
var start_fwd := Vector3.ZERO
var top_speed := 0.0
var fuel0 := 0.0
var saw_boost := false


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
	for p in passes: print("rover_test: ok   - " + p)
	for f in failures: print("rover_test: FAIL - " + f)
	print("rover_test: " + ("PASS" if failures.is_empty() else "FAIL"))
	Points.set_screens_mode(-1)
	quit(0 if failures.is_empty() else 1)


func moon_up(p: Vector3) -> Vector3:
	return (p - moon.global_position).normalized()


func ground_gap(p: Vector3) -> float:
	return moon.global_position.distance_to(p) - moon.surface_radius_toward(p)


## Stand on the Moon `dist` metres from world point `p`, along `away`.
func stand(p: Vector3, away: Vector3, dist: float) -> void:
	var local: Vector3 = moon.global_transform.affine_inverse() * (p + away * dist)
	var dir := local.normalized()
	var ground: float = moon.surface_radius_toward(moon.global_transform * (dir * 100.0))
	sys.debug_place_player_on("moon", dir * (ground + 1.0))


func flat_fwd() -> Vector3:
	var f: Vector3 = rover.global_basis.z
	var up := moon_up(rover.global_position)
	return (f - up * f.dot(up)).normalized()


func _process(_d: float) -> bool:
	frame += 1
	if frame > MAX_FRAMES:
		check(false, "test finished in time (stuck in phase %d)" % phase)
		finish(); return true
	if OS.has_environment("ROVER_DEBUG") and phase >= 8 and phase <= 13 and rover and frame % 20 == 0:
		print("dbg f=%d tilt=%.1f awake=%s v=%.2f w=%s" % [frame, rover.get_tilt_deg(), rover.is_awake(), rover.get_speed(), rover.angular_velocity])
	if frame < wait_until:
		return false
	match phase:
		0:
			if frame < 5: return false
			moon = sys.get_node("moon")
			moon.request_detail()
			var home := false
			for r in get_nodes_in_group("rovers"):
				if String(r.name) == "HomeRover": home = true
				elif r.get_parent() == moon: rover = r
			check(home, "a buggy waits at the home base on Earth")
			check(rover != null, "a buggy is parked on the Moon")
			if rover == null: finish(); return true
			var tilt := rad_to_deg(moon.to_local(rover.global_position).normalized().angle_to(Vector3.UP))
			check(tilt < 12.0, "it is near the landing area (%.1f deg from the pole)" % tilt)
			check(not rover.is_awake(), "a far-away buggy stays parked")
			phase = 1
		1:
			if not moon.has_detail(): return false
			var g := ground_gap(rover.global_position)
			check(g > -0.2 and g < 1.2, "it is parked on the ground (%.2f m up)" % g)
			# Walk up beside it.
			stand(rover.global_position, rover.global_basis.x.normalized(), 3.0)
			phase = 2; mark = frame
		2:
			if not rover.is_awake() and frame - mark < 60: return false
			check(rover.is_awake(), "coming close wakes it")
			phase = 3; wait_until = frame + 150
		3:
			check(rover.get_grounded_wheels() == 4, "it rests on all four wheels (%d)" % rover.get_grounded_wheels())
			check(rover.get_tilt_deg() < 25.0, "it stands upright (%.1f deg)" % rover.get_tilt_deg())
			check(rover.get_speed() < 0.6, "parked, it doesn't slide (%.2f m/s)" % rover.get_speed())
			stand(rover.global_position, rover.global_basis.x.normalized(), 2.2)
			phase = 4; wait_until = frame + 5
		4:
			player.debug_look_at(rover.global_position)
			var prompt := String(player.get_interact_prompt())
			check(prompt == "drive", "next to it, X offers Drive (%s)" % prompt)
			player.debug_interact()
			phase = 5; wait_until = frame + 3
		5:
			check(player.is_driving(), "X climbs in")
			check(String(player.get_interact_prompt()) == "exit_rover", "while driving, X offers Climb out")
			start_pos = moon.to_local(rover.global_position)
			start_fwd = flat_fwd()
			top_speed = 0.0
			rover.debug_drive(1.0, 0.0, false, false)
			phase = 6; mark = frame
		6:
			top_speed = maxf(top_speed, rover.get_speed())
			if player.global_position.distance_to(rover.global_position) > 3.0:
				check(false, "the driver stays in the seat (%.1f m away)" % player.global_position.distance_to(rover.global_position))
				phase = 7; return false
			if frame - mark < 180: return false
			check(top_speed > 6.0, "full throttle gets it moving (%.1f m/s in 3 s)" % top_speed)
			var moved: float = moon.to_local(rover.global_position).distance_to(start_pos)
			check(moved > 8.0, "it drives off (%.1f m)" % moved)
			check(player.global_position.distance_to(rover.global_position) < 3.0, "the driver stays in the seat")
			start_fwd = flat_fwd()
			rover.debug_drive(0.6, 1.0, false, false)
			phase = 7; mark = frame
		7:
			if frame - mark < 120: return false
			var turned := rad_to_deg(flat_fwd().angle_to(start_fwd))
			check(turned > 20.0, "steering turns it (%.0f deg in 2 s)" % turned)
			rover.debug_drive(-1.0, 0.0, false, false)
			phase = 8; mark = frame
		8:
			if rover.get_speed() > 1.0 and frame - mark < 240: return false
			check(rover.get_speed() <= 1.0, "pulling back brakes it to a stop (%.1f s)" % ((frame - mark) / 60.0))
			fuel0 = rover.get_boost_fraction()
			saw_boost = false
			rover.debug_drive(1.0, 0.0, true, false)
			phase = 9; mark = frame
		9:
			saw_boost = saw_boost or rover.is_boosting()
			if frame - mark < 60: return false
			check(saw_boost, "A fires the rocket boost")
			check(rover.get_boost_fraction() < fuel0 - 0.2, "boosting burns boost fuel (%.2f -> %.2f)" % [fuel0, rover.get_boost_fraction()])
			fuel0 = rover.get_boost_fraction()
			rover.debug_drive(-1.0, 0.0, false, false)
			phase = 10; mark = frame
		10:
			if frame - mark < 150: return false
			check(rover.get_boost_fraction() > fuel0 + 0.2, "boost fuel recharges (%.2f -> %.2f)" % [fuel0, rover.get_boost_fraction()])
			rover.debug_release()
			player.debug_interact()
			phase = 11; wait_until = frame + 20
		11:
			check(not player.is_driving(), "X climbs out")
			var d: float = player.global_position.distance_to(rover.global_position)
			check(d < 4.5, "you climb out beside it (%.1f m)" % d)
			check(ground_gap(player.global_position) > -0.5, "climbing out never puts you underground")
			phase = 15; mark = frame
		15:
			# Let it settle, then turn it over.
			if rover.get_speed() > 0.3 and frame - mark < 300: return false
			rover.debug_flip()
			phase = 12; wait_until = frame + 5
		12:
			check(rover.get_tilt_deg() > 90.0, "a test flip turns it over (%.0f deg)" % rover.get_tilt_deg())
			rover.debug_drive(0.0, 0.0, true, false)
			phase = 13; mark = frame
		13:
			if rover.get_tilt_deg() > 25.0 and frame - mark < 480: return false
			check(rover.get_tilt_deg() <= 25.0, "holding A rolls it back upright (%.1f s)" % ((frame - mark) / 60.0))
			rover.debug_release()
			# Walk far away: it parks.
			var far_dir: Vector3 = -moon.to_local(rover.global_position).normalized()
			sys.debug_place_player_on("moon", far_dir * (moon.surface_radius_toward(moon.global_transform * (far_dir * 100.0)) + 1.0))
			phase = 14; mark = frame
		14:
			if rover.is_awake() and frame - mark < 120: return false
			check(not rover.is_awake(), "left far behind, it parks")
			check(rover.get_parent() == moon, "a parked buggy turns with the Moon")
			var g := ground_gap(rover.global_position)
			check(g > -0.2 and g < 1.2, "it parks on the ground (%.2f m up)" % g)
			finish(); return true
	return false
