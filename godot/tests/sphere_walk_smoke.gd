extends SceneTree
## Scripted smoke test for the Phase 0 sphere-walk scene.
## Holds "forward" for 80 simulated seconds (more than one lap of the test
## sphere), taps jump and turns once, then checks the player stayed on the
## surface and upright. Exits with code 1 on failure.
##
## Run from the godot/ folder:
##   godot --headless --path . --fixed-fps 60 --script res://tests/sphere_walk_smoke.gd

const SETTLE_FRAMES := 120
const WALK_SECONDS := 80
const FEET := 0.9                 # capsule half-height (data/player/movement.ron)
const MAX_HEIGHT_ABOVE := 3.0     # jump apex is about 1.7 m above the ground
const MAX_BELOW := 0.3            # feet may sink this far into the ground mesh
const MAX_UP_ERROR_DEG := 1.0
const MIN_TRAVEL_DEG := 90.0

var scene: Node
var player: CharacterBody3D
var frame := 0
var start_dir: Vector3
var body: Node3D
var min_clear := INF
var max_clear := -INF
var max_travel := 0.0
var jumped := false
var failures: Array[String] = []

func _initialize() -> void:
	scene = load("res://scenes/tests/sphere_walk.tscn").instantiate()
	root.add_child(scene)
	player = scene.get_node("Player")
	body = scene.get_node("TestSphere")

func _physics_process(_delta: float) -> bool:
	frame += 1
	# Height of the feet above the ground directly below (terrain is hilly).
	var clear: float = player.global_position.length() - FEET - body.surface_radius_toward(player.global_position)
	if frame == SETTLE_FRAMES:
		if not player.is_on_floor():
			failures.append("not on the floor after settling (clearance %.2f m)" % clear)
		start_dir = player.global_position.normalized()
		Input.action_press("move_forward")
	elif frame > SETTLE_FRAMES:
		min_clear = min(min_clear, clear)
		max_clear = max(max_clear, clear)
		max_travel = max(max_travel, rad_to_deg(start_dir.angle_to(player.global_position.normalized())))
	if frame == 400: Input.action_press("jump")
	if frame == 402: Input.action_release("jump")
	if frame > 402 and frame < 460 and not player.is_on_floor(): jumped = true
	if frame == 500: Input.action_press("look_right", 1.0)
	if frame == 530: Input.action_release("look_right")
	if frame == SETTLE_FRAMES + 60 * WALK_SECONDS:
		_finish()
	return false

func _finish() -> void:
	var up_error := rad_to_deg(player.global_basis.y.angle_to(player.global_position.normalized()))
	if min_clear < -MAX_BELOW: failures.append("fell through the surface (clearance %.2f m)" % min_clear)
	if max_clear > MAX_HEIGHT_ABOVE: failures.append("left the surface (clearance %.2f m)" % max_clear)
	if up_error > MAX_UP_ERROR_DEG: failures.append("not upright (%.2f deg off)" % up_error)
	if max_travel < MIN_TRAVEL_DEG: failures.append("only travelled %.1f deg" % max_travel)
	if not jumped: failures.append("jump did not leave the ground")
	print("sphere_walk_smoke: ground clearance %.2f..%.2f m, travelled up to %.1f deg, upright error %.2f deg, jumped %s" % [min_clear, max_clear, max_travel, up_error, jumped])
	if failures.is_empty():
		print("sphere_walk_smoke: PASS")
		quit(0)
	else:
		for f in failures: printerr("sphere_walk_smoke: FAIL - " + f)
		quit(1)
