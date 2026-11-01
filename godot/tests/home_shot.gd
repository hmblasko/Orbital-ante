extends SceneTree
## Renders the Earth home base for visual review. Not a gate; run with a
## real renderer:
##   godot --path . --fixed-fps 60 --resolution 1280x800 --script res://tests/home_shot.gd -- <out_dir>

var scene: Node
var player: CharacterBody3D
var sys: Node
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

func view(from_xz: Vector2, height: float, look_xz: Vector2, look_h: float) -> void:
	var home: Node3D = get_first_node_in_group("home_base")
	var pad: Node3D = get_first_node_in_group("launch_pads")
	var earth: Node3D = home.get_parent()
	var up: Vector3 = (pad.global_position - earth.global_position).normalized()
	var fwd: Vector3 = pad.global_position - player.global_position
	fwd = (fwd - up * fwd.dot(up)).normalized()
	var right := fwd.cross(up).normalized()
	var start: Vector3 = get_meta("start")
	var p := start + right * from_xz.x + fwd * from_xz.y + up * height
	var local: Vector3 = earth.global_transform.affine_inverse() * p
	sys.debug_place_player_on("earth", local)
	player.velocity = Vector3.ZERO
	player.debug_look_at(start + right * look_xz.x + fwd * look_xz.y + up * look_h)

func _process(_d: float) -> bool:
	frame += 1
	match frame:
		90:
			set_meta("start", player.global_position)
			shot("home_start_view")
		91: view(Vector2(-30, -18), 14.0, Vector2(0, 20), 2.0)
		92: view(Vector2(-30, -18), 14.0, Vector2(0, 20), 2.0)
		120: shot("home_overview")
		121: view(Vector2(25, 70), 10.0, Vector2(-5, 20), 3.0)
		150: shot("home_from_behind_pad")
		151: view(Vector2(-6, 14), 0.0, Vector2(-24, 30), 3.0)
		180:
			shot("home_hangar")
			quit(0)
	return false
