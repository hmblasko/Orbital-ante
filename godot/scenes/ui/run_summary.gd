extends Control
## Run-end summary (CLAUDE.md section 2): how far you got, what you found,
## and the local leaderboard tagged with the seed (section 3). The run is
## recorded on the leaderboard and autosaved when this opens.
## Serves fun rule 9 (every run counts) and rule 8 (fair failure: the
## outcome is named plainly).

signal closed

var _rank := -1


func _ready() -> void:
	var s: Dictionary = Points.get_summary()
	_rank = SaveData.record_run_over()
	var column := ScreenKit.frame(self, 820)
	var outcome := String(s.get("outcome", ""))
	var key: String = {"won": "SUMMARY_WON", "died": "SUMMARY_DIED", "time_up": "SUMMARY_TIME_UP"}.get(outcome, "SUMMARY_TIME_UP")
	column.add_child(ScreenKit.title(tr(key)))
	var sub := ScreenKit.label(tr("SUMMARY_REACHED") % [int(s.get("ante", 1)), String(s.get("seed", ""))], 22, ScreenKit.DIM)
	sub.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(sub)

	var p := ScreenKit.panel()
	var grid := GridContainer.new()
	grid.columns = 4
	grid.add_theme_constant_override("h_separation", 34)
	grid.add_theme_constant_override("v_separation", 8)
	p.add_child(grid)
	var rows := [
		["SUMMARY_POINTS", Points.format(int(s.get("total_points", 0)))],
		["SUMMARY_ANTES", str(int(s.get("antes_cleared", 0)))],
		["SUMMARY_DISCOVERIES", str(int(s.get("discoveries", 0)))],
		["SUMMARY_KILLS", str(int(s.get("kills", 0)))],
		["SUMMARY_CAPTURES", str(int(s.get("captures", 0)))],
		["SUMMARY_CONTRACTS", str(int(s.get("contracts_done", 0)))],
		["SUMMARY_BEST_MULT", "x%.1f" % float(s.get("best_multiplier", 1.0))],
	]
	for r in rows:
		grid.add_child(ScreenKit.label(tr(r[0]), 18, ScreenKit.DIM))
		grid.add_child(ScreenKit.label(r[1], 24, ScreenKit.TEXT, true))
	column.add_child(p)

	# Local leaderboard.
	var lb := ScreenKit.panel()
	var v := ScreenKit.col(4)
	lb.add_child(v)
	v.add_child(ScreenKit.label(tr("SUMMARY_LEADERBOARD").to_upper(), 18, ScreenKit.AMBER))
	var board: Array = SaveData.get_leaderboard()
	for i in mini(board.size(), 5):
		var e: Dictionary = board[i]
		var line := "%d.  %s  %s   %s  %s" % [i + 1, tr("HUD_ANTE"), int(e["ante"]), Points.format(int(e["points"])), String(e["seed"])]
		var col := ScreenKit.GOOD if i == _rank else ScreenKit.TEXT
		v.add_child(ScreenKit.label(line + ("   " + tr("SUMMARY_THIS_RUN") if i == _rank else ""), 18, col, true))
	if board.is_empty():
		v.add_child(ScreenKit.label(tr("SUMMARY_NO_BOARD"), 18, ScreenKit.DIM))
	column.add_child(lb)

	var bottom := ScreenKit.row()
	bottom.alignment = BoxContainer.ALIGNMENT_CENTER
	var done := ScreenKit.button(tr("SUMMARY_CONTINUE"), 30)
	done.custom_minimum_size = Vector2(320, 64)
	done.pressed.connect(confirm)
	bottom.add_child(done)
	column.add_child(bottom)
	done.grab_focus()


func get_rank() -> int:
	return _rank


func confirm() -> void:
	closed.emit()
