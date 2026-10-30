extends Control
## PLACEHOLDER main menu. Thin GDScript UI glue (CLAUDE.md section 14);
## the real TF2-style menu comes later. Every string comes from text/strings.csv.

signal start_requested

func _ready() -> void:
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	%StartButton.pressed.connect(func(): start_requested.emit())
	%NewSeedButton.pressed.connect(_on_new_seed)
	%QuitButton.pressed.connect(func(): get_tree().quit())
	%StartButton.grab_focus()
	_refresh()

func _on_new_seed() -> void:
	RunSeed.new_random()
	_refresh()

func _refresh() -> void:
	%SeedLabel.text = tr("MENU_SEED") % RunSeed.get_code()
	%VersionLabel.text = tr("MENU_VERSION") % [ProjectSettings.get_setting("application/config/version"), SaveData.get_runs_started()]
