extends SceneTree
## Renders the ante HUD and the between-ante screens for visual review.
## Not a gate; run with a real renderer:
##   godot --path . --fixed-fps 60 --script res://tests/ante_screens_shot.gd -- <out_dir>

var scene: Node
var director: Node
var frame := 0
var out := "user://shots"

func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() > 0: out = args[0]
	DirAccess.make_dir_recursive_absolute(out)
	SaveData.set_autosave_enabled(false)
	Points.set_screens_mode(1)
	Points.start_run()
	root.size = Vector2i(1280, 800)
	scene = load("res://scenes/tests/solar_system.tscn").instantiate()
	root.add_child(scene)
	director = scene.get_node("AnteDirector")

func shot(name: String) -> void:
	var img := root.get_texture().get_image()
	img.save_png(out + "/" + name + ".png")
	print("shot ", name)

func _process(_d: float) -> bool:
	frame += 1
	match frame:
		40: shot("ante_board")
		41:
			director.get_screen().toggle(0)
			director.get_screen().toggle(2)
		44: shot("ante_board_taken")
		45: director.get_screen().confirm()
		80:
			Points.on_kill(1, "moon")
			Points.on_limb_shot()
			Points.on_discovered("x:1", "moon", 70, TranslationServer.translate("LANDFORM_CRATER"))
		110: shot("ante_hud")
		111:
			Points.award("discovery", 1400, "SOURCE_DISCOVERY")
			director.debug_cash_out()
		140: shot("ante_upgrade")
		141:
			director.get_screen().buy("damage")
			director.get_screen().scratch_now()
			var d: Array = Points.get_draft()
			if d.size() > 0: director.get_screen().pick(String(d[0]["id"]))
		150: shot("ante_upgrade_done")
		151: director.get_screen().confirm()
		155: director.get_screen().confirm()
		160: Points.debug_set_time_left(0.2)
		200: shot("ante_summary")
		201:
			Points.set_screens_mode(-1)
			quit(0)
	return false
