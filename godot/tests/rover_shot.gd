extends SceneTree
## Renders the Moon buggy at the home base for visual review (section 11).
## Not a gate; run with a real renderer:
##   godot --path . --fixed-fps 60 --resolution 1280x800 --script res://tests/rover_shot.gd -- <out_dir>

var scene: Node
var player: CharacterBody3D
var sys: Node
var rover: RigidBody3D
var earth: Node3D
var frame := 0
var out := "user://shots"

func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() > 0: out = args[0]
	DirAccess.make_dir_recursive_absolute(out)
	SaveData.set_autosave_enabled(false)
	Points.set_screens_mode(0)
	Points.start_run()
	root.size = Vector2i(1280, 800)
	scene = load("res://scenes/tests/solar_system.tscn").instantiate()
	root.add_child(scene)
	player = scene.get_node("Player")
	sys = scene.get_node("SolarSystem")

func shot(name: String) -> void:
	root.get_texture().get_image().save_png(out + "/" + name + ".png")
	print("shot ", name)

## Stand at the buggy's local offset (x right... in its own axes) and look at it.
func view(off: Vector3, look_off: Vector3) -> void:
	var t: Transform3D = rover.global_transform
	var p: Vector3 = t * off
	var local: Vector3 = earth.global_transform.affine_inverse() * p
	sys.debug_place_player_on("earth", local)
	player.velocity = Vector3.ZERO
	player.debug_look_at(t * look_off)

func _process(_d: float) -> bool:
	frame += 1
	match frame:
		60:
			for r in get_nodes_in_group("rovers"):
				if String(r.name) == "HomeRover": rover = r
			earth = sys.get_node("earth")
		61, 62: view(Vector3(4.5, 1.2, 4.5), Vector3(0, 0.9, 0))
		100: shot("rover_front_quarter")
		101, 102: view(Vector3(-5.5, 1.0, -0.5), Vector3(0, 0.9, 0))
		130: shot("rover_side")
		131, 132: view(Vector3(2.5, 2.2, -4.5), Vector3(0, 0.9, 0))
		160: shot("rover_rear_quarter")
		161:
			view(Vector3(2.0, 1.0, -0.3), Vector3(0, 1.0, 0))
		165: player.debug_interact()
		200: shot("rover_driver_view")
		201: rover.debug_drive(0.0, 0.0, true, false)
		215: shot("rover_driver_boost")
		216:
			rover.debug_release()
			player.debug_interact()
		240:
			rover.debug_drive(0.0, 0.0, true, false)
			view(Vector3(3.0, 1.5, -6.0), Vector3(0, 1.0, 0))
		243: shot("rover_boost_rear")
		244:
			quit(0)
	return false
