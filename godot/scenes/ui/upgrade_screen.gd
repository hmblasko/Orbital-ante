extends Control
## End-of-ante upgrade screen (CLAUDE.md section 3): the ante's tally
## (score, cash-out, interest), attributes bought with the Wallet, a draft
## of 3 perks (pick 1, rerolls cost more each time), and one free
## scratch-off ticket with its odds shown (section 13).
## Serves fun rule 2 (interesting decisions: spend, save for interest, or
## reroll) and pillar 5 (honest rewards: the ticket's odds are on it and the
## fanfare matches the prize).

signal closed

var _wallet: Label
var _attr_buttons: Dictionary = {}
var _attr_levels: Dictionary = {}
var _draft_box: HBoxContainer
var _reroll: Button
var _ticket: Button
var _ticket_result: Label
var _next: Button
var _scratching := false


func _ready() -> void:
	var column := ScreenKit.frame(self, 1180)
	var res: Dictionary = Points.get_last_result()
	column.add_child(ScreenKit.title(tr("UPGRADE_TITLE") % int(res.get("ante", Points.get_ante()))))
	# Tally: score vs target, cash-out bonus, interest, Wallet.
	var tally := ScreenKit.row(28)
	tally.alignment = BoxContainer.ALIGNMENT_CENTER
	tally.add_child(_stat(tr("UPGRADE_SCORE"), "%s / %s" % [Points.format(int(res.get("score", 0))), Points.format(int(res.get("target", 0)))], ScreenKit.GOOD))
	if int(res.get("cash_out", 0)) > 0:
		tally.add_child(_stat(tr("UPGRADE_CASH_OUT"), "+%s" % Points.format(int(res["cash_out"])), ScreenKit.AMBER))
	tally.add_child(_stat(tr("UPGRADE_INTEREST"), "+%s" % Points.format(int(res.get("interest", 0))), ScreenKit.AMBER))
	var w := _stat(tr("UPGRADE_WALLET"), "", ScreenKit.TEXT)
	_wallet = w.get_child(1)
	tally.add_child(w)
	column.add_child(tally)

	var body := ScreenKit.row(18)
	column.add_child(body)
	body.add_child(_attributes_panel())
	body.add_child(_draft_panel())
	body.add_child(_ticket_panel())

	var bottom := ScreenKit.row()
	bottom.alignment = BoxContainer.ALIGNMENT_CENTER
	_next = ScreenKit.button(tr("UPGRADE_NEXT") % (Points.get_ante() + 1), 30)
	_next.custom_minimum_size = Vector2(340, 64)
	_next.pressed.connect(confirm)
	bottom.add_child(_next)
	column.add_child(bottom)
	_refresh()
	var first: Array = _draft_box.get_children()
	if first.size() > 0:
		(first[0] as Control).grab_focus()
	else:
		_next.grab_focus()


func _stat(caption: String, value: String, color: Color) -> VBoxContainer:
	var v := ScreenKit.col(0)
	var c := ScreenKit.label(caption.to_upper(), 15, ScreenKit.DIM)
	c.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(c)
	var n := ScreenKit.label(value, 30, color, true)
	n.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(n)
	return v


func _attributes_panel() -> Control:
	var p := ScreenKit.panel()
	p.custom_minimum_size = Vector2(330, 0)
	var v := ScreenKit.col(10)
	p.add_child(v)
	v.add_child(ScreenKit.label(tr("UPGRADE_ATTRIBUTES").to_upper(), 18, ScreenKit.AMBER))
	for a in Points.get_attributes():
		var id := String(a["id"])
		var r := ScreenKit.row(8)
		var names := ScreenKit.col(0)
		names.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		names.add_child(ScreenKit.label(tr("ATTR_" + id.to_upper()), 20))
		var lvl := ScreenKit.label("", 16, ScreenKit.TEAL)
		names.add_child(lvl)
		r.add_child(names)
		var b := ScreenKit.button("", 18)
		b.custom_minimum_size = Vector2(120, 44)
		b.pressed.connect(buy.bind(id))
		r.add_child(b)
		v.add_child(r)
		_attr_buttons[id] = b
		_attr_levels[id] = lvl
	return p


func _draft_panel() -> Control:
	var p := ScreenKit.panel()
	p.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var v := ScreenKit.col(10)
	p.add_child(v)
	v.add_child(ScreenKit.label(tr("UPGRADE_DRAFT").to_upper(), 18, ScreenKit.AMBER))
	_draft_box = ScreenKit.row(10)
	v.add_child(_draft_box)
	_reroll = ScreenKit.button("", 18)
	_reroll.pressed.connect(reroll)
	v.add_child(_reroll)
	return p


func _ticket_panel() -> Control:
	var p := ScreenKit.panel()
	p.custom_minimum_size = Vector2(260, 0)
	var v := ScreenKit.col(8)
	p.add_child(v)
	v.add_child(ScreenKit.label(tr("UPGRADE_TICKET").to_upper(), 18, ScreenKit.AMBER))
	_ticket = ScreenKit.button(tr("UPGRADE_SCRATCH"), 22)
	_ticket.custom_minimum_size = Vector2(0, 70)
	_ticket.pressed.connect(scratch)
	v.add_child(_ticket)
	_ticket_result = ScreenKit.para("", 20, ScreenKit.TEXT)
	_ticket_result.add_theme_font_override("font", ScreenKit.NUMBER_FONT)
	_ticket_result.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(_ticket_result)
	# The odds, always on the ticket (section 13).
	v.add_child(ScreenKit.label(tr("UPGRADE_ODDS"), 15, ScreenKit.DIM))
	for o in Points.get_scratch_odds():
		var line := ScreenKit.para("%s  %.0f%%  %s" % [tr(String(o["rarity_key"])), float(o["percent"]), String(o["prize"])], 14, o["color"])
		v.add_child(line)
	var t: Dictionary = Points.get_ticket()
	if t.is_empty():
		_ticket.disabled = true
		_ticket.text = tr("UPGRADE_NO_TICKET")
	elif bool(t.get("scratched", false)):
		_show_ticket(t)
	return p


## Buy one level of attribute `id` (also used by tests).
func buy(id: String) -> bool:
	var ok: bool = Points.buy_attribute(id)
	_refresh()
	if ok:
		ScreenKit.pop(_attr_levels[id])
	return ok


func pick(id: String) -> bool:
	var ok: bool = Points.pick_perk(id)
	_refresh()
	return ok


func reroll() -> bool:
	var ok: bool = Points.reroll()
	_refresh()
	if ok and _draft_box.get_child_count() > 0:
		(_draft_box.get_child(0) as Control).grab_focus()
	return ok


## Scratch the ticket: a short reveal (anticipation, section 10), then the
## prize with fanfare that matches its true value.
func scratch() -> void:
	if _scratching:
		return
	_scratching = true
	_ticket.disabled = true
	_ticket.text = tr("UPGRADE_SCRATCHING")
	var t: Dictionary = Points.scratch()
	var tw := create_tween()
	tw.tween_property(_ticket, "modulate", Color(1.4, 1.3, 1.0), Points.get_scratch_reveal_s() * 0.5)
	tw.tween_property(_ticket, "modulate", Color.WHITE, Points.get_scratch_reveal_s() * 0.5)
	tw.tween_callback(func():
		_scratching = false
		_show_ticket(t)
		_refresh())


## Instant scratch for tests (no reveal wait).
func scratch_now() -> Dictionary:
	var t: Dictionary = Points.scratch()
	_show_ticket(t)
	_refresh()
	return t


func _show_ticket(t: Dictionary) -> void:
	if t.is_empty():
		return
	_ticket.disabled = true
	_ticket.text = tr(String(t["rarity_key"])).to_upper()
	_ticket.add_theme_color_override("font_disabled_color", t["color"])
	_ticket_result.text = String(t["prize"])
	_ticket_result.add_theme_color_override("font_color", t["color"])
	ScreenKit.pop(_ticket_result)


func confirm() -> void:
	closed.emit()


func _refresh() -> void:
	_wallet.text = Points.format(Points.get_wallet())
	var wallet: int = Points.get_wallet()
	for a in Points.get_attributes():
		var id := String(a["id"])
		var lvl := int(a["level"])
		var mx := int(a["max"])
		_attr_levels[id].text = ScreenKit.pips(lvl, mx)
		var b: Button = _attr_buttons[id]
		var price := int(a["price"])
		if price < 0:
			b.text = tr("UPGRADE_MAXED")
			b.disabled = true
		else:
			b.text = Points.format(price)
			b.disabled = price > wallet
	# Draft cards.
	for c in _draft_box.get_children():
		_draft_box.remove_child(c)
		c.queue_free()
	var picked: bool = Points.has_picked_perk()
	for perk in Points.get_draft():
		var b := ScreenKit.card(Vector2(176, 210))
		b.disabled = picked
		b.pressed.connect(pick.bind(String(perk["id"])))
		var box := ScreenKit.col(4)
		box.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		box.offset_left = 12
		box.offset_right = -12
		box.offset_top = 12
		box.offset_bottom = -12
		box.mouse_filter = Control.MOUSE_FILTER_IGNORE
		var name := ScreenKit.para(tr(String(perk["name_key"])), 22, ScreenKit.TEXT)
		name.add_theme_font_override("font", ScreenKit.NUMBER_FONT)
		name.mouse_filter = Control.MOUSE_FILTER_IGNORE
		box.add_child(name)
		var tags: PackedStringArray = perk["tags"]
		var tag_text := PackedStringArray()
		for t in tags:
			tag_text.append(tr(t))
		var tl := ScreenKit.para(" · ".join(tag_text), 14, ScreenKit.TEAL)
		tl.mouse_filter = Control.MOUSE_FILTER_IGNORE
		box.add_child(tl)
		var desc := ScreenKit.para(tr(String(perk["desc_key"])), 15)
		desc.mouse_filter = Control.MOUSE_FILTER_IGNORE
		box.add_child(desc)
		b.add_child(box)
		_draft_box.add_child(b)
	if picked:
		var held := ScreenKit.label(tr("UPGRADE_PICKED"), 18, ScreenKit.GOOD)
		_draft_box.add_child(held)
	var price: int = Points.get_reroll_price()
	_reroll.text = tr("UPGRADE_REROLL") % Points.format(price)
	_reroll.disabled = picked or price > wallet or Points.get_draft().is_empty()
