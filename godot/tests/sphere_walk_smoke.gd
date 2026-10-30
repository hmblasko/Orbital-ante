extends SceneTree
## Scripted smoke test for the Phase 0 sphere-walk scene.
## Holds "forward" for 80 simulated seconds (well over a quarter lap of the
## test sphere, across whatever landforms lie ahead), taps jump and turns
## once, then checks the player never sank through the ground, never stayed
## airborne, and stayed upright. Exits with code 1 on failure.
## The ground is found by a ray against the real collision, since cliffs and
## terraces make the mesh differ from the height function between vertices.
##
## Run from the godot/ folder:
##   godot --headless --path . --fixed-fps 60 --script res://tests/sphere_walk_smoke.gd

const SETTLE_FRAMES := 120
const WALK_SECONDS := 80
const FEET := 0.9                 # capsule half-height (data/player/movement.ron)
const MAX_AIRBORNE_S := 3.0       # a jump lasts under 1 s; a fall off a 15 m mesa about 2 s
const MAX_BELOW := 0.3            # feet may sink this far into the ground mesh
const MAX_UP_ERROR_DEG := 1.0
const MIN_TRAVEL_DEG := 90.0

var scene: Node
var player: CharacterBody3D
var frame := 0
var start_dir: Vector3
var body: Node3D
var min_clear := INF
var airborne_s := 0.0
var max_airborne_s := 0.0
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
	var clear: float = ground_clearance()
	if frame == SETTLE_FRAMES:
		if not player.is_on_floor():
			failures.append("not on the floor after settling (clearance %.2f m)" % clear)
		start_dir = player.global_position.normalized()
		Input.action_press("move_forward")
	elif frame > SETTLE_FRAMES:
		min_clear = min(min_clear, clear)
		airborne_s = 0.0 if player.is_on_floor() else airborne_s + 1.0 / 60.0
		max_airborne_s = max(max_airborne_s, airborne_s)
		max_travel = max(max_travel, rad_to_deg(start_dir.angle_to(player.global_position.normalized())))
	if frame == 400: Input.action_press("jump")
	if frame == 402: Input.action_release("jump")
	if frame > 402 and frame < 460 and not player.is_on_floor(): jumped = true
	if frame == 500: Input.action_press("look_right", 1.0)
	if frame == 530: Input.action_release("look_right")
	if frame == SETTLE_FRAMES + 60 * WALK_SECONDS:
		_finish()
	return false

## Feet height above the ground collision straight below (negative = sunk in).
func ground_clearance() -> float:
	var up := player.global_position.normalized()
	var query := PhysicsRayQueryParameters3D.create(player.global_position + up * 2.0, player.global_position - up * 40.0)
	query.exclude = [player.get_rid()]
	var hit := player.get_world_3d().direct_space_state.intersect_ray(query)
	if hit.is_empty():
		return -INF
	return (player.global_position - up * FEET - hit.position).dot(up)

func _finish() -> void:
	var up_error := rad_to_deg(player.global_basis.y.angle_to(player.global_position.normalized()))
	if min_clear < -MAX_BELOW: failures.append("fell through the surface (clearance %.2f m)" % min_clear)
	if max_airborne_s > MAX_AIRBORNE_S: failures.append("left the surface (airborne %.1f s)" % max_airborne_s)
	if up_error > MAX_UP_ERROR_DEG: failures.append("not upright (%.2f deg off)" % up_error)
	if max_travel < MIN_TRAVEL_DEG: failures.append("only travelled %.1f deg" % max_travel)
	if not jumped: failures.append("jump did not leave the ground")
	print("sphere_walk_smoke: lowest clearance %.2f m, longest airborne %.1f s, travelled up to %.1f deg, upright error %.2f deg, jumped %s" % [min_clear, max_airborne_s, max_travel, up_error, jumped])
	if failures.is_empty():
		print("sphere_walk_smoke: PASS")
		quit(0)
	else:
		for f in failures: printerr("sphere_walk_smoke: FAIL - " + f)
		quit(1)
