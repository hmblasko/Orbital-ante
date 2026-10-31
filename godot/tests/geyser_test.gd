extends SceneTree
## Phase 5 feature test: Mars's CO2 geysers.
## 1. Mars has its vents, all on the south polar cap.
## 2. Standing on a vent: frost puffs warn first, then the dust column
##    erupts (fair warning before every eruption).
## 3. The eruption throws the player high into the air.
## Exits with code 1 on any failure.
##
## Run from the godot/ folder:
##   godot --headless --path . --fixed-fps 60 --script res://tests/geyser_test.gd

const SETTLE_FRAMES := 60
const MAX_DETAIL_FRAMES := 60 * 60
const MAX_WAIT_FRAMES := 60 * 90
const VENT_COUNT := 7
## Warning length in mars.ron (geysers.warn_s).
const WARN_S := 4.0

var scene: Node
var player: CharacterBody3D
var sys: Node
var mars: Node3D
var geysers: Node
var frame := 0
var phase := 0
var phase_start := 0
var failures: Array[String] = []
var passes: Array[String] = []
var vent_local: Vector3
var erupt_at := 0.0
var saw_warning := false
var warned_before := false
var max_height := 0.0
var erupted := false

func _initialize() -> void:
	scene = load("res://scenes/tests/solar_system.tscn").instantiate()
	root.add_child(scene)
	player = scene.get_node("Player")
	sys = scene.get_node("SolarSystem")

func check(ok: bool, what: String) -> void:
	if ok: passes.append(what)
	else: failures.append(what)

func finish() -> void:
	for p in passes: print("geyser_test: ok   - " + p)
	for f in failures: print("geyser_test: FAIL - " + f)
	print("geyser_test: " + ("PASS" if failures.is_empty() else "FAIL"))
	quit(0 if failures.is_empty() else 1)

func stand_on_vent() -> void:
	sys.debug_place_player_on("mars", vent_local + vent_local.normalized() * 1.0)

func _physics_process(_d: float) -> bool:
	frame += 1
	var t := frame - phase_start
	match phase:
		0:
			if frame == SETTLE_FRAMES:
				mars = sys.get_node("mars")
				geysers = mars.get_node_or_null("Geysers")
				check(geysers != null, "Mars has geysers")
				if geysers == null:
					finish(); return true
				var vents: PackedVector3Array = geysers.get_vent_positions()
				check(vents.size() == VENT_COUNT, "Mars has %d vents (%d)" % [VENT_COUNT, vents.size()])
				var on_cap := true
				for v in vents:
					var l: Vector3 = mars.global_transform.affine_inverse() * v
					if l.normalized().dot(Vector3.DOWN) < cos(0.3): on_cap = false
				check(on_cap, "every vent is on the south polar cap")
				vent_local = mars.global_transform.affine_inverse() * vents[0]
				stand_on_vent()
				phase = 1; phase_start = frame
		1:
			if mars.has_detail():
				stand_on_vent()
				phase = 2; phase_start = frame
			elif t > MAX_DETAIL_FRAMES:
				failures.append("Mars ground never streamed in"); finish(); return true
		2:
			# Give slower machines up to 10 s to settle onto the vent.
			if t >= 60 and (player.is_on_floor() or t >= 600):
				check(player.is_on_floor(), "the player stands on the vent")
				var now: float = sys.get_clock()
				erupt_at = geysers.get_next_eruption_after(0, now + 6.0)
				phase = 3; phase_start = frame
		3:
			var now: float = sys.get_clock()
			if now < erupt_at - WARN_S - 0.5: return false
			var frost: Node = geysers.get_node("Vent0Frost")
			var column: Node = geysers.get_node("Vent0")
			if frost.emitting: saw_warning = true
			if column.emitting and not warned_before:
				warned_before = saw_warning
				check(warned_before, "frost puffs warn before the eruption")
				check(not frost.emitting, "the warning stops when the column erupts")
			var h: float = (mars.global_transform.affine_inverse() * player.global_position).length() - vent_local.length()
			if column.emitting: erupted = true
			if erupted: max_height = max(max_height, h)
			if now > erupt_at + 6.0 or t > MAX_WAIT_FRAMES:
				check(erupted, "the dust column erupted")
				check(max_height > 8.0, "the eruption throws the player high (%.1f m)" % max_height)
				finish(); return true
	return false
