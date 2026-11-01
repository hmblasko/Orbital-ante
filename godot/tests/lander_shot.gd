extends SceneTree
## Renders the lander for visual review (cockpit view from the seat, the
## belly walk-under, the outside). Not a gate; run with a real renderer:
##   godot --path . --fixed-fps 60 --resolution 1280x800 --script res://tests/lander_shot.gd -- <out_dir> [prefix]

var scene: Node
var player: CharacterBody3D
var ship: RigidBody3D
var frame := 0
var out := "user://shots"
var prefix := "lander"

func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() > 0: out = args[0]
	if args.size() > 1: prefix = args[1]
	DirAccess.make_dir_recursive_absolute(out)
	root.size = Vector2i(1280, 800)
	scene = load("res://scenes/tests/lander.tscn").instantiate()
	root.add_child(scene)
	player = scene.get_node("Player")
	ship = scene.get_node("Lander")

func shot(name: String) -> void:
	root.get_texture().get_image().save_png(out + "/" + prefix + "_" + name + ".png")
	print("shot ", name)

func _physics_process(_d: float) -> bool:
	frame += 1
	var t: Transform3D = ship.global_transform
	match frame:
		60:
			player.global_transform = Transform3D(t.basis * Basis(Vector3.UP, PI), t * Vector3(-0.9, ship.get_cabin_floor_y() + 0.95, 0.35))
			player.velocity = Vector3.ZERO
		70: player.debug_interact()
		110: shot("seat_view")
		111: player.debug_interact()
		120:
			# Outside, behind the ship, eye on the hatch underneath.
			player.global_transform = Transform3D(t.basis, t * Vector3(0.0, 1.0, -9.0))
			player.velocity = Vector3.ZERO
		125: player.debug_look_at(t * Vector3(0.0, 2.0, -1.4))
		150: shot("belly")
		151:
			player.global_transform = Transform3D(t.basis, t * Vector3(9.0, 1.0, 14.0))
			player.velocity = Vector3.ZERO
		155: player.debug_look_at(t * Vector3(0.0, 4.0, 0.0))
		180:
			shot("outside")
			quit(0)
	return false
