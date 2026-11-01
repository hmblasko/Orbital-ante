extends SceneTree
## Phase 8 (vertical slice) feature test: things to fix and the Venera base.
## 1. The Moon has 2 seeded ground dishes and 1 damaged satellite in orbit.
## 2. A small Venera base (pods, walls, crates, a radio mast) stands around
##    the camp, and no soldier stands inside a piece.
## 3. Looking at a dish's controls shows "Hold to switch on"; holding X
##    switches it on for 120 points. The second dish pays 120 + 400 for
##    every dish on the Moon. Each dish pays once per run.
## 4. The satellite has several broken panels; each must be held on. The
##    last one pays 300 (Repair), and the restored signal draws a patrol.
## 5. Ante 2 moves the base with the re-manned camp.
## Exits with code 1 on any failure.
##
## Run from the godot/ folder:
##   godot --headless --path . --fixed-fps 60 --script res://tests/sites_test.gd

const MAX_FRAMES := 60 * 120

var scene: Node
var player: CharacterBody3D
var sys: Node
var moon: Node3D
var frame := 0
var phase := 0
var wait_until := 0
var failures: Array[String] = []
var passes: Array[String] = []
var dishes: Array = []
var sat: Node3D
var score0 := 0
var hold_frames := 0
var part := 0
var patrol0 := 0
var base0 := Vector3.ZERO


func _initialize() -> void:
	SaveData.set_autosave_enabled(false)
	Points.set_screens_mode(0)
	Points.start_run()
	scene = load("res://scenes/tests/solar_system.tscn").instantiate()
	root.add_child(scene)
	player = scene.get_node("Player")
	sys = scene.get_node("SolarSystem")


func check(ok: bool, what: String) -> void:
	if ok: passes.append(what)
	else: failures.append(what)


func finish() -> void:
	for p in passes: print("sites_test: ok   - " + p)
	for f in failures: print("sites_test: FAIL - " + f)
	print("sites_test: " + ("PASS" if failures.is_empty() else "FAIL"))
	Points.set_screens_mode(-1)
	quit(0 if failures.is_empty() else 1)


func soldiers() -> Array:
	var out := []
	for c in moon.get_children():
		if c.has_method("get_state_name") and not c.is_queued_for_deletion(): out.append(c)
	return out


func base_pieces() -> Array:
	var out := []
	for n in get_nodes_in_group("base_structures"):
		if not n.is_queued_for_deletion(): out.append(n)
	return out


func freeze_all() -> void:
	for e in soldiers(): e.debug_set_frozen(true)


## Stand beside world point `p` on the Moon, a step back along `away`.
func stand_near(p: Vector3, away: Vector3) -> void:
	var local: Vector3 = moon.global_transform.affine_inverse() * (p + away * 1.4)
	var dir := local.normalized()
	var ground: float = moon.surface_radius_toward(moon.global_transform * (dir * 100.0))
	sys.debug_place_player_on("moon", dir * (ground + 1.0))


func centroid(nodes: Array) -> Vector3:
	var c := Vector3.ZERO
	for n in nodes: c += moon.to_local(n.global_position)
	return c / maxf(1.0, nodes.size())


func _process(_d: float) -> bool:
	frame += 1
	if frame > MAX_FRAMES:
		check(false, "test finished in time (stuck in phase %d)" % phase)
		finish(); return true
	if frame < wait_until:
		return false
	match phase:
		0:
			if frame < 5: return false
			check(Points.get_phase() == "playing", "the ante is running (%s)" % Points.get_phase())
			moon = sys.get_node("moon")
			moon.request_detail()
			phase = 1
		1:
			if not moon.has_detail(): return false
			freeze_all()
			for n in get_nodes_in_group("repair_sites"):
				if n.get_body_id() != "moon": continue
				if n.get_kind_name() == "dish": dishes.append(n)
				elif n.get_kind_name() == "satellite": sat = n
			check(dishes.size() == 2, "2 ground dishes on the Moon (%d)" % dishes.size())
			check(sat != null, "a damaged satellite orbits the Moon")
			# The base around the camp.
			var pieces := base_pieces()
			check(pieces.size() >= 9, "a small Venera base stands (%d pieces)" % pieces.size())
			var names := pieces.map(func(p): return String(p.name))
			check(names.any(func(n): return n.ends_with("Pod")) and names.any(func(n): return n.ends_with("Wall")) and names.any(func(n): return n.ends_with("Mast")), "pods, walls and a mast")
			var camp_c := centroid(soldiers())
			var far := 0.0
			for p in pieces: far = maxf(far, moon.to_local(p.global_position).distance_to(camp_c))
			check(far < 25.0, "the base surrounds the camp (furthest piece %.1f m)" % far)
			var inside := 0
			for e in soldiers():
				for p in pieces:
					var local: Vector3 = p.to_local(e.global_position)
					var shape: Shape3D = (p.get_child(1) as CollisionShape3D).shape
					var half: Vector3 = (shape as BoxShape3D).size * 0.5
					var c: Vector3 = (p.get_child(1) as CollisionShape3D).position
					var q := (local - c).abs()
					if q.x < half.x and q.z < half.z and q.y < half.y + 1.0: inside += 1
			check(inside == 0, "no soldier stands inside a base piece (%d)" % inside)
			base0 = centroid(pieces)
			phase = 2
		2:
			# First dish: look at the controls.
			var d: Node3D = dishes[0]
			var p: Vector3 = d.get_part_position(0)
			# Stand in front of the cabinet: out from the dish, past the part.
			var up: Vector3 = (p - moon.global_position).normalized()
			var out := p - d.global_position
			out = (out - up * out.dot(up)).normalized()
			stand_near(p, out)
			phase = 3; wait_until = frame + 20
		3:
			var d: Node3D = dishes[0]
			player.debug_look_at(d.get_part_position(0))
			if frame < wait_until + 2: return false
			var prompt := String(player.get_interact_prompt())
			check(prompt.begins_with("switch:SITE_DISH"), "looking at the controls offers 'Hold to switch on' (%s)" % prompt)
			score0 = Points.get_score()
			player.debug_hold_repair(true)
			hold_frames = 0
			phase = 4
		4:
			var d: Node3D = dishes[0]
			player.debug_look_at(d.get_part_position(0))
			hold_frames += 1
			if hold_frames % 30 == 1 and OS.has_environment("SITES_DEBUG"):
				print("dbg dish: d=%.2f prompt=%s floor=%s v=%s alt=%.2f" % [player.global_position.distance_to(d.get_part_position(0)), player.get_interact_prompt(), player.is_on_floor(), player.velocity, moon.to_local(player.global_position).length() - moon.surface_radius_toward(player.global_position)])
			if not d.is_fixed() and hold_frames < 60 * 5: return false
			player.debug_hold_repair(false)
			check(d.is_fixed(), "holding X switches the dish on (%.1f s)" % (hold_frames / 60.0))
			check(hold_frames >= 60 and hold_frames <= 120, "it takes a quick hold, about 1.4 s (%d frames)" % hold_frames)
			var gained := Points.get_score() - score0
			check(gained >= 120 and gained < 400, "one dish pays 120 before the multiplier (+%d)" % gained)
			score0 = Points.get_score()
			dishes[0].debug_fix_all()
			check(Points.get_score() == score0, "a dish pays only once")
			# Second dish completes the set.
			dishes[1].debug_fix_all()
			gained = Points.get_score() - score0
			check(gained >= 520, "the last dish adds the all-dishes bonus: 120 + 400 (+%d)" % gained)
			phase = 5; wait_until = frame + 3
		5:
			var prompt0 := String(player.get_interact_prompt())
			check(not prompt0.begins_with("switch:"), "a switched-on dish offers nothing more (%s)" % prompt0)
			# The satellite: float up beside its first broken panel.
			sat.debug_hold_orbit(true)
			check(sat.get_part_count() >= 2, "the satellite needs a multi-part repair (%d parts)" % sat.get_part_count())
			patrol0 = get_nodes_in_group("signal_patrols").size()
			score0 = Points.get_score()
			part = 0
			hold_frames = 0
			phase = 6
		6:
			if part >= sat.get_part_count():
				phase = 7; return false
			var p: Vector3 = sat.get_part_position(part)
			var out: Vector3 = (p - sat.global_position).normalized()
			if out.length() < 0.5: out = (p - moon.global_position).normalized()
			var local: Vector3 = moon.global_transform.affine_inverse() * (p + out * 1.6)
			sys.debug_place_player_on("moon", local)
			player.velocity = Vector3.ZERO
			player.debug_look_at(p)
			var prompt := String(player.get_interact_prompt())
			if hold_frames == 0 and OS.has_environment("SITES_DEBUG"):
				print("dbg sat: part %d d=%.2f prompt=%s" % [part, player.global_position.distance_to(p), prompt])
			# The prompt follows a physics step after the move.
			if hold_frames == 2:
				check(prompt.begins_with("repair:SITE_SATELLITE"), "a broken panel offers 'Hold to repair' (part %d: %s)" % [part, prompt])
			player.debug_hold_repair(true)
			hold_frames += 1
			if sat.get_parts_fixed() > part or hold_frames > 60 * 6:
				check(sat.get_parts_fixed() > part, "holding on panel %d fixes it" % part)
				if part < sat.get_part_count() - 1:
					check(not sat.is_fixed(), "one panel is not the whole repair")
				part += 1
				hold_frames = 0
		7:
			player.debug_hold_repair(false)
			check(sat.is_fixed(), "every panel fixed repairs the satellite")
			var gained := Points.get_score() - score0
			check(gained >= 300, "a satellite repair pays 300 before the multiplier (+%d)" % gained)
			phase = 8; wait_until = frame + 3
		8:
			var patrol := get_nodes_in_group("signal_patrols").size() - patrol0
			check(patrol == 3, "the restored signal draws a patrol (%d soldiers)" % patrol)
			var warned := false
			for t in Notifications.get_texts():
				if t.contains(TranslationServer.translate("NOTE_PATROL_INCOMING")): warned = true
			check(warned, "a warning says the patrol is coming")
			# Ante 2: the base moves with the re-manned camp.
			moon.respawn_camps(2)
			phase = 9; wait_until = frame + 3
		9:
			var pieces := base_pieces()
			check(pieces.size() >= 9, "ante 2 rebuilds the base (%d pieces)" % pieces.size())
			var moved := centroid(pieces).distance_to(base0)
			check(moved > 5.0, "the base moved with the camp (%.1f m)" % moved)
			finish(); return true
	return false
