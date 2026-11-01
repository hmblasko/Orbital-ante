extends SceneTree
## Renders the Phase 8 slice sites (Venera base, dishes, damaged satellite)
## for visual review against the section 11 quality bar. Not a gate; run
## with a real renderer:
##   godot --path . --fixed-fps 60 --resolution 1280x800 --script res://tests/sites_shot.gd -- <out_dir>

var scene: Node
var sys: Node
var player: CharacterBody3D
var moon: Node3D
var frame := 0
var step := 0
var wait_until := 0
var out := "user://shots"
var dishes: Array = []
var sat: Node3D
var hold_at := Vector3.ZERO
var look_at := Vector3.ZERO


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
	sys = scene.get_node("SolarSystem")
	player = scene.get_node("Player")


func shot(name: String) -> void:
	var img := root.get_texture().get_image()
	img.save_png(out + "/" + name + ".png")
	print("shot ", name)


## Stand on the Moon at `dist` metres from world point `p`, along `away`, and look at `target`.
func stand(p: Vector3, away: Vector3, dist: float, target: Vector3) -> void:
	var local: Vector3 = moon.global_transform.affine_inverse() * (p + away * dist)
	var dir := local.normalized()
	var ground: float = moon.surface_radius_toward(moon.global_transform * (dir * 100.0))
	sys.debug_place_player_on("moon", dir * (ground + 1.0))
	look_at = target


func flat(v: Vector3, up: Vector3) -> Vector3:
	return (v - up * v.dot(up)).normalized()


func _process(_d: float) -> bool:
	frame += 1
	if look_at != Vector3.ZERO: player.debug_look_at(look_at)
	if hold_at != Vector3.ZERO:
		sys.debug_place_player_on("moon", hold_at)
		player.velocity = Vector3.ZERO
	if frame < wait_until: return false
	match step:
		0:
			if frame < 5: return false
			moon = sys.get_node("moon")
			# Go there first, or streaming releases the Moon's detail
			# while the player is still on Earth.
			sys.debug_place_player_on("moon", Vector3.UP * (moon.radius() + 30.0))
			moon.request_detail()
			step = 1
		1:
			if not moon.has_detail():
				sys.debug_place_player_on("moon", Vector3.UP * (moon.radius() + 30.0))
				return false
			for e in moon.get_children():
				if e.has_method("debug_set_frozen"): e.debug_set_frozen(true)
			for n in get_nodes_in_group("repair_sites"):
				if n.get_kind_name() == "dish": dishes.append(n)
				else: sat = n
			# The base, from outside its walls.
			var c := Vector3.ZERO
			var pieces := get_nodes_in_group("base_structures")
			for p in pieces: c += p.global_position
			c /= maxf(1.0, pieces.size())
			var up := (c - moon.global_position).normalized()
			stand(c, flat(Vector3(1, 0, 0.4), up), 24.0, c + up * 1.5)
			step = 2; wait_until = frame + 40
		2:
			shot("phase8_base")
			var pieces := get_nodes_in_group("base_structures")
			var pod: Node3D
			for p in pieces:
				if String(p.name).ends_with("Pod"): pod = p
			var up := (pod.global_position - moon.global_position).normalized()
			stand(pod.global_position, flat(pod.global_transform.basis.z, up), 5.0, pod.global_position + up * 1.4)
			step = 3; wait_until = frame + 30
		3:
			shot("phase8_base_pod")
			var d: Node3D = dishes[0]
			var up := (d.global_position - moon.global_position).normalized()
			var part: Vector3 = d.get_part_position(0)
			stand(d.global_position, flat(part - d.global_position, up), 34.0, d.global_position + up * 8.0)
			step = 4; wait_until = frame + 40
		4:
			shot("phase8_dish")
			var d: Node3D = dishes[0]
			var part: Vector3 = d.get_part_position(0)
			var up := (part - moon.global_position).normalized()
			stand(part, flat(part - d.global_position, up), 2.2, part)
			step = 5; wait_until = frame + 30
		5:
			shot("phase8_dish_cabinet")
			d_switch()
			step = 6; wait_until = frame + 30
		6:
			shot("phase8_dish_on")
			sat.debug_hold_orbit(true)
			var p: Vector3 = sat.get_part_position(0)
			var out_dir: Vector3 = (p - sat.global_position).normalized()
			hold_at = moon.global_transform.affine_inverse() * (p + out_dir * 9.0 + (p - moon.global_position).normalized() * 3.0)
			look_at = sat.global_position
			step = 7; wait_until = frame + 30
		7:
			shot("phase8_satellite")
			var p: Vector3 = sat.get_part_position(0)
			var out_dir: Vector3 = (p - sat.global_position).normalized()
			hold_at = moon.global_transform.affine_inverse() * (p + out_dir * 2.0)
			look_at = p
			step = 8; wait_until = frame + 20
		8:
			shot("phase8_satellite_panel")
			Points.set_screens_mode(-1)
			quit(0)
	return false


func d_switch() -> void:
	dishes[0].debug_fix_all()
