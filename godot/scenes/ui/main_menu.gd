extends Control
## Main menu. Thin GDScript UI glue (CLAUDE.md section 14): the look comes
## from res://ui/theme.tres, the slowly turning planet behind it is the real
## test body rendered in its own viewport. Every string comes from
## text/strings.csv. Controller first: Start has focus on open.

signal start_requested

const SPIN_DEG_PER_S := 2.5      # planet turn speed
const INTRO_S := 0.6             # fade and slide-in on open
# Camera framing: side-on to the sun so the terminator shows, planet on the right.
const CAMERA_POS := Vector3(-114, 19, 152)
const CAMERA_AIM := Vector3(-44, 0, -33)

func _ready() -> void:
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	%StartButton.pressed.connect(func(): start_requested.emit())
	%NewSeedButton.pressed.connect(_on_new_seed)
	%QuitButton.pressed.connect(func(): get_tree().quit())
	%StartButton.grab_focus()
	$Backdrop/View/Camera3D.look_at_from_position(CAMERA_POS, CAMERA_AIM)
	var rule := GradientTexture2D.new()
	var g := Gradient.new()
	g.set_color(0, Color(0.98, 0.58, 0.22, 1.0))
	g.set_color(1, Color(0.98, 0.58, 0.22, 0.0))
	rule.gradient = g
	$Margin/Column/Rule.texture = rule
	_refresh()
	# Intro: fade in and slide the column from the left.
	modulate.a = 0.0
	$Margin.position.x -= 24.0
	var t := create_tween().set_parallel().set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	t.tween_property(self, "modulate:a", 1.0, INTRO_S)
	t.tween_property($Margin, "position:x", $Margin.position.x + 24.0, INTRO_S)

func _process(delta: float) -> void:
	%Spin.rotate_object_local(Vector3.UP, deg_to_rad(SPIN_DEG_PER_S) * delta)

func _on_new_seed() -> void:
	RunSeed.new_random()
	_refresh()
	var t := create_tween()
	%SeedLabel.modulate = Color(1.6, 1.4, 1.0)
	t.tween_property(%SeedLabel, "modulate", Color.WHITE, 0.4)

func _refresh() -> void:
	%SeedLabel.text = tr("MENU_SEED") % RunSeed.get_code()
	%VersionLabel.text = tr("MENU_VERSION") % [ProjectSettings.get_setting("application/config/version"), SaveData.get_runs_started()]
