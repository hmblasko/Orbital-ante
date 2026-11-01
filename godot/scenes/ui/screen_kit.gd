class_name ScreenKit
## Shared building blocks for the between-ante screens (contract board,
## upgrade screen, run summary). Thin GDScript UI glue (CLAUDE.md section
## 14): the look comes from res://ui/theme.tres, every string from
## text/strings.csv, every number from Points (data in data/points/).
## Controller first: every screen gives a button focus on open.

const NUMBER_FONT := preload("res://assets/fonts/LilitaOne-Regular.ttf")
const TEXT := Color(0.93, 0.94, 0.97)
const DIM := Color(0.70, 0.73, 0.80)
const AMBER := Color(1.0, 0.70, 0.28)
const GOOD := Color(0.55, 0.95, 0.55)
const BAD := Color(1.0, 0.36, 0.18)
const TEAL := Color(0.36, 0.90, 0.88)


## Dark backdrop over the paused world, with a centred column.
static func frame(root: Control, width: float) -> VBoxContainer:
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_STOP
	var shade := ColorRect.new()
	shade.color = Color(0.01, 0.015, 0.035, 0.82)
	shade.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.add_child(shade)
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.add_child(center)
	var column := VBoxContainer.new()
	column.custom_minimum_size = Vector2(width, 0)
	column.add_theme_constant_override("separation", 14)
	center.add_child(column)
	return column


static func label(text: String, size: int = 20, color: Color = TEXT, numbers: bool = false) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", color)
	if numbers:
		l.add_theme_font_override("font", NUMBER_FONT)
	return l


## A label that wraps inside a parent of fixed width (card descriptions).
static func para(text: String, size: int = 18, color: Color = DIM) -> Label:
	var l := label(text, size, color)
	l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	l.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	return l


## A card button: dark glass, with a thick amber edge when chosen (the
## theme's pressed style is a bright fill, too loud under card text).
static func card(min_size: Vector2) -> Button:
	var b := button("", 18)
	b.custom_minimum_size = min_size
	var chosen := StyleBoxFlat.new()
	chosen.bg_color = Color(0.10, 0.09, 0.07, 0.92)
	chosen.border_color = AMBER
	chosen.set_border_width_all(4)
	chosen.set_corner_radius_all(14)
	chosen.corner_detail = 6
	b.add_theme_stylebox_override("pressed", chosen)
	b.add_theme_stylebox_override("hover_pressed", chosen)
	return b


static func title(text: String) -> Label:
	var l := Label.new()
	l.theme_type_variation = &"TitleLabel"
	l.text = text
	l.add_theme_font_size_override("font_size", 54)
	l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	return l


static func panel() -> PanelContainer:
	var p := PanelContainer.new()
	p.theme_type_variation = &"GlassPanel"
	return p


static func button(text: String, size: int = 24) -> Button:
	var b := Button.new()
	b.text = text
	b.add_theme_font_size_override("font_size", size)
	b.focus_mode = Control.FOCUS_ALL
	return b


static func row(sep: int = 12) -> HBoxContainer:
	var h := HBoxContainer.new()
	h.add_theme_constant_override("separation", sep)
	return h


static func col(sep: int = 8) -> VBoxContainer:
	var v := VBoxContainer.new()
	v.add_theme_constant_override("separation", sep)
	return v


## Risk as filled and empty pips: shape, not just colour (colourblind safe).
static func pips(filled: int, total: int) -> String:
	return "●".repeat(filled) + "○".repeat(max(total - filled, 0))


static func clock(seconds: float) -> String:
	var s := int(ceil(max(seconds, 0.0)))
	return "%d:%02d" % [s / 60, s % 60]


## A quick pop when something is chosen or revealed.
static func pop(node: CanvasItem) -> void:
	if not node.is_inside_tree():
		return
	var t := node.create_tween()
	node.modulate = Color(1.6, 1.4, 1.0)
	t.tween_property(node, "modulate", Color.WHITE, 0.35)
