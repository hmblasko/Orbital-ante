extends Control
## Contract board at the start of each ante (CLAUDE.md section 3): three
## optional contracts with different risk and reward; take up to two. The
## world is paused behind it; the clock starts on Launch.
## Serves fun rule 2 (interesting decisions) and rule 3 (autonomy).

signal closed

var _cards: Array[Button] = []
var _marks: Array[Label] = []
var _launch: Button
var _count: Label


func _ready() -> void:
	var column := ScreenKit.frame(self, 980)
	column.add_child(ScreenKit.title(tr("BOARD_TITLE") % Points.get_ante()))
	var info := ScreenKit.para(tr("BOARD_INFO") % [Points.format(Points.get_target()), ScreenKit.clock(Points.get_time_left()), Points.get_max_contracts()], 22, ScreenKit.DIM)
	info.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(info)
	var cards := ScreenKit.row(16)
	cards.alignment = BoxContainer.ALIGNMENT_CENTER
	column.add_child(cards)
	var board: Array = Points.get_board()
	for i in board.size():
		var c: Dictionary = board[i]
		var b := ScreenKit.card(Vector2(310, 230))
		b.toggle_mode = true
		b.pressed.connect(_on_card.bind(i))
		var box := ScreenKit.col(6)
		box.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		box.offset_left = 18
		box.offset_right = -18
		box.offset_top = 16
		box.offset_bottom = -16
		box.mouse_filter = Control.MOUSE_FILTER_IGNORE
		var risk := int(c["risk"])
		box.add_child(_ignore(ScreenKit.label("%s  %s" % [tr("BOARD_RISK"), ScreenKit.pips(risk, 3)], 17, [ScreenKit.TEAL, ScreenKit.AMBER, ScreenKit.BAD][clampi(risk - 1, 0, 2)])))
		box.add_child(_ignore(ScreenKit.label(String(c["title"]), 26, ScreenKit.TEXT, true)))
		box.add_child(_ignore(ScreenKit.para(String(c["desc"]), 18)))
		var spacer := Control.new()
		spacer.size_flags_vertical = Control.SIZE_EXPAND_FILL
		box.add_child(_ignore(spacer))
		var foot := ScreenKit.row(8)
		foot.add_child(_ignore(ScreenKit.label("+%s" % Points.format(int(c["reward"])), 30, ScreenKit.GOOD, true)))
		var mark := ScreenKit.label("", 18, ScreenKit.AMBER, true)
		mark.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		mark.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		mark.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		foot.add_child(_ignore(mark))
		box.add_child(_ignore(foot))
		_marks.append(mark)
		b.add_child(box)
		cards.add_child(b)
		_cards.append(b)
	_count = ScreenKit.label("", 20, ScreenKit.DIM)
	_count.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	column.add_child(_count)
	var bottom := ScreenKit.row()
	bottom.alignment = BoxContainer.ALIGNMENT_CENTER
	_launch = ScreenKit.button(tr("BOARD_LAUNCH"), 30)
	_launch.custom_minimum_size = Vector2(320, 64)
	_launch.pressed.connect(confirm)
	bottom.add_child(_launch)
	column.add_child(bottom)
	_refresh()
	if _cards.size() > 0:
		_cards[0].grab_focus()
	else:
		_launch.grab_focus()


func _ignore(c: Control) -> Control:
	c.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return c


## Take or put back contract `i` (also used by tests).
func toggle(i: int) -> bool:
	var taken: bool = Points.toggle_contract(i)
	_refresh()
	if i < _cards.size():
		ScreenKit.pop(_cards[i])
	return taken


func confirm() -> void:
	closed.emit()


func _on_card(i: int) -> void:
	toggle(i)


func _refresh() -> void:
	var board: Array = Points.get_board()
	var taken := 0
	for i in board.size():
		var on := bool(board[i]["taken"])
		if i < _cards.size():
			_cards[i].set_pressed_no_signal(on)
			_marks[i].text = ("✓ " + tr("BOARD_TAKEN_MARK")) if on else ""
		if on:
			taken += 1
	_count.text = tr("BOARD_TAKEN") % [taken, Points.get_max_contracts()]
