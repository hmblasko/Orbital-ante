extends SceneTree
## Phase 4 part 2 feature test: the cockpit dash and cameras.
## 1. The lander has a dash with three styles, one of them the terminal.
## 2. F4 (cycle_dash) aboard switches the style.
## 3. Buckled in, Y (cameras_screen) steps through the three ship cameras
##    and back to your own eyes, showing the camera frame meanwhile.
## 4. Unbuckling while on a camera returns the view to your eyes.

var scene: Node
var player: CharacterBody3D
var ship: RigidBody3D
var frame := 0
var failures: Array[String] = []
var passes: Array[String] = []
var seen: Array[String] = []

func _initialize() -> void:
	scene = load("res://scenes/tests/lander.tscn").instantiate()
	root.add_child(scene)
	player = scene.get_node("Player")
	ship = scene.get_node("Lander")

func check(ok: bool, what: String) -> void:
	if ok: passes.append(what)
	else: failures.append(what)

func dash() -> Node:
	return ship.find_child("Dash", true, false)

func current_camera_name() -> String:
	var c := root.get_viewport().get_camera_3d()
	return c.name if c else "none"

func tap(action: String) -> void:
	Input.action_press(action)

func _physics_process(_d: float) -> bool:
	frame += 1
	match frame:
		60:
			check(dash() != null and dash().get_style_count() == 3, "the lander has a dash with three styles")
			var t: Transform3D = ship.global_transform
			player.global_transform = Transform3D(t.basis * Basis(Vector3.UP, PI), t * Vector3(-0.9, ship.get_cabin_floor_y() + 0.95, 0.35))
			player.velocity = Vector3.ZERO
		70:
			player.debug_interact()
		90:
			check(player.is_buckled(), "the pilot buckles in")
			var before: int = dash().get_style_index()
			tap("cycle_dash")
			set_meta("before", before)
		91: Input.action_release("cycle_dash")
		93:
			check(dash().get_style_index() == (int(get_meta("before")) + 1) % 3, "F4 switches the dash style")
			tap("cameras_screen")
		94: Input.action_release("cameras_screen")
		96:
			seen.append(current_camera_name())
			tap("cameras_screen")
		97: Input.action_release("cameras_screen")
		99:
			seen.append(current_camera_name())
			tap("cameras_screen")
		100: Input.action_release("cameras_screen")
		102:
			seen.append(current_camera_name())
			var frame_node := ship.find_child("CameraFrame", true, false)
			check(frame_node != null and frame_node.visible, "the camera frame shows while on a ship camera")
			tap("cameras_screen")
		103: Input.action_release("cameras_screen")
		105:
			seen.append(current_camera_name())
			check(seen == ["ShipCamera0", "ShipCamera1", "ShipCamera2", "Camera3D"], "Y steps through the three cameras and back (%s)" % [seen])
			tap("cameras_screen")
		106: Input.action_release("cameras_screen")
		108:
			player.debug_interact()
		112:
			check(not player.is_buckled() and current_camera_name() == "Camera3D", "unbuckling returns the view to your eyes")
			for p in passes: print("cockpit_test: ok   - " + p)
			for f in failures: printerr("cockpit_test: FAIL - " + f)
			if failures.is_empty():
				print("cockpit_test: PASS (%d checks)" % passes.size())
				quit(0)
			else:
				quit(1)
	return false
