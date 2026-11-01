extends SceneTree
## Phase 7 feature test: the ante loop in the vertical slice.
## 1. A run opens on ante 1's contract board: 3 contracts (risk 1-3), the
##    world paused; up to 2 can be taken; Launch starts the clock.
## 2. The ante clock counts down; first discoveries add 20 s.
## 3. Kills, limb shots and disarms pay points; the last soldier of a camp
##    down or tied is a camp clear; every award shows a notification.
## 4. Two kinds of score raise the multiplier above x1.0.
## 5. Reaching the target allows cash-out, which ends the ante: the upgrade
##    screen opens with the tally, attributes cost 0.30T, a draft of 3,
##    a scratch-off with its odds.
## 6. Perks change stats (Glass Cannon, Deep Breath on suit oxygen).
## 7. Ante 2 re-mans the camp; running out of time ends the run with the
##    summary and a leaderboard entry.
## Exits with code 1 on any failure.
##
## Run from the godot/ folder:
##   godot --headless --path . --fixed-fps 60 --script res://tests/ante_test.gd

const MAX_FRAMES := 60 * 240
const HEAD := 0
const ARM_R := 3
const LEG_L := 4

var scene: Node
var player: CharacterBody3D
var sys: Node
var moon: Node3D
var director: Node
var frame := 0
var phase := 0
var wait_until := 0
var failures: Array[String] = []
var passes: Array[String] = []
var enemies: Array = []
var t0 := 0.0
var score0 := 0
var wallet0 := 0
var o2_base := 0.0
var disc0 := 0
var time0 := 0.0
var mult0 := 1.0


func _initialize() -> void:
	SaveData.set_autosave_enabled(false)
	Points.set_screens_mode(1)
	Points.start_run()
	scene = load("res://scenes/tests/solar_system.tscn").instantiate()
	root.add_child(scene)
	player = scene.get_node("Player")
	sys = scene.get_node("SolarSystem")
	director = scene.get_node("AnteDirector")


func check(ok: bool, what: String) -> void:
	if ok: passes.append(what)
	else: failures.append(what)


func finish() -> void:
	for p in passes: print("ante_test: ok   - " + p)
	for f in failures: print("ante_test: FAIL - " + f)
	print("ante_test: " + ("PASS" if failures.is_empty() else "FAIL"))
	Points.set_screens_mode(-1)
	quit(0 if failures.is_empty() else 1)


func notes_contain(key: String) -> bool:
	var want := TranslationServer.translate(key)
	for t in Notifications.get_texts():
		if t.contains(want): return true
	return false


func camp() -> Array:
	var out := []
	for c in moon.get_children():
		if c.has_method("get_state_name") and not c.is_queued_for_deletion(): out.append(c)
	return out


func stand_on_moon(local_dir: Vector3) -> void:
	var dir := local_dir.normalized()
	var ground: float = moon.surface_radius_toward(moon.global_transform * (dir * 100.0))
	sys.debug_place_player_on("moon", dir * (ground + 1.0))


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
			check(director.get_screen_name() == "board", "a new run opens on the contract board (got '%s')" % director.get_screen_name())
			check(paused, "the world is paused behind the board")
			var board: Array = Points.get_board()
			var risks := board.map(func(c): return int(c["risk"]))
			check(board.size() == 3, "3 contracts offered (got %d)" % board.size())
			check(risks.has(1) and risks.has(2) and risks.has(3), "one of each risk level (%s)" % [risks])
			check(board.all(func(c): return int(c["reward"]) >= 300 and int(c["reward"]) <= 1200), "rewards within section 17.5's 300-1,200")
			var screen: Control = director.get_screen()
			var a: bool = screen.toggle(0)
			var b: bool = screen.toggle(1)
			var c: bool = screen.toggle(2)
			check(a and b and not c, "take up to 2 contracts; a third is refused")
			check(Points.get_contracts().size() == 2, "2 contracts taken")
			check(Points.get_phase() == "board", "the clock waits on the board")
			screen.confirm()
			t0 = Points.get_time_left()
			phase = 1; wait_until = frame + 60
		1:
			check(Points.get_phase() == "playing" and not paused, "Launch starts the ante and unpauses")
			check(absf(t0 - 720.0) < 0.5, "ante 1 lasts 12 minutes (%.1f s)" % t0)
			check(Points.get_time_left() < t0 - 0.5, "the clock counts down (%.2f -> %.2f)" % [t0, Points.get_time_left()])
			check(Points.get_summary()["discoveries"] == 0, "the start area is not a discovery")
			check(Points.get_target() == 1000, "ante 1 target is 1,000")
			phase = 2
		2:
			moon = sys.get_node("moon")
			moon.request_detail()
			phase = 3
		3:
			if not moon.has_detail(): return false
			enemies = camp()
			check(enemies.size() == 4, "the Moon camp is manned (%d soldiers)" % enemies.size())
			for e in enemies: e.debug_set_frozen(true)
			score0 = Points.get_score()
			# Kill one, gib and disarm another.
			enemies[0].debug_hit(HEAD, 500.0)
			phase = 4; wait_until = frame + 3
		4:
			check(Points.get_score() >= score0 + 25, "a tier 1 kill pays 25 (score %d)" % Points.get_score())
			check(Notifications.get_visible_count() >= 1, "the kill shows a notification")
			score0 = Points.get_score()
			enemies[1].debug_hit(ARM_R, 40.0)
			for i in 6:
				if enemies[1].has_limb(LEG_L): enemies[1].debug_hit(LEG_L, 40.0)
			check(Points.get_score() > score0, "limb shots and disarms pay (score %d -> %d)" % [score0, Points.get_score()])
			score0 = Points.get_score()
			enemies[1].debug_hit(HEAD, 500.0)
			enemies[2].debug_hit(HEAD, 500.0)
			# Last one: tied up, not killed (still counts toward the clear).
			player.global_position = enemies[3].global_position + Vector3(0, 0, 0)
			enemies[3].tie_up(enemies[3].global_position + Vector3(3, 0, 0))
			phase = 5; wait_until = frame + 3
		5:
			check(notes_contain("SOURCE_CAMP_CLEAR"), "downing or tying the last soldier clears the camp")
			check(Points.get_score() >= score0 + 150, "a camp clear pays a small base clear (score %d -> %d)" % [score0, Points.get_score()])
			check(Points.get_summary()["captures"] == 1, "the tied soldier counts as a capture")
			# Go to a big landform far from the camp (near the pole) for a
			# discovery the camp fight could not have found already.
			var best: Dictionary = {}
			for l in moon.get_landforms():
				var dir: Vector3 = l["dir"]
				if float(l["radius_m"]) >= 18.0 and dir.normalized().y < 0.2 and String(l["kind"]) in ["Crater", "Basin", "Mare", "RayedCrater", "ColdTrap", "Mountain", "Bowl", "Ridge", "Rille"]:
					if best.is_empty() or float(l["radius_m"]) > float(best["radius_m"]): best = l
			check(not best.is_empty(), "the Moon has a discoverable landform away from the camp")
			if not best.is_empty():
				stand_on_moon(best["dir"])
			disc0 = Points.get_summary()["discoveries"]
			time0 = Points.get_time_left()
			mult0 = Points.get_multiplier()
			phase = 6; wait_until = frame + 90
		6:
			director.debug_check_discoveries()
			var found: int = Points.get_summary()["discoveries"] - disc0
			# 90 frames (1.5 s) passed; each discovery adds 20 s.
			var gained: float = Points.get_time_left() - time0 + 1.5
			check(found >= 1, "standing on a landform discovers it (%d new)" % found)
			check(absf(gained - 20.0 * found) < 0.5, "each first discovery adds 20 s (+%.1f s for %d)" % [gained, found])
			check(notes_contain("NOTE_DISCOVERED"), "a discovery notification shows")
			check(Points.get_multiplier() > mult0 or mult0 > 1.0, "variety raises the multiplier above x1.0 (x%.2f)" % Points.get_multiplier())
			var d_again: int = Points.get_summary()["discoveries"]
			director.debug_check_discoveries()
			check(Points.get_summary()["discoveries"] == d_again, "a place is only discovered once")
			check(Points.get_pace() in ["ahead", "on_pace", "behind"], "the pace meter reads (%s)" % Points.get_pace())
			# Base stats before perks.
			player.debug_set_oxygen(99999.0)
			o2_base = player.get_oxygen_seconds()
			# Reach the target and cash out.
			check(not Points.can_cash_out(), "no cash-out below the target")
			Points.award("discovery", 2000, "SOURCE_DISCOVERY")
			check(Points.can_cash_out(), "cash-out opens once the target is reached")
			var value: int = Points.get_cash_out_value()
			check(value > 0 and value <= 600, "cash-out pays 0.002T per second left, capped at 0.6T (%d)" % value)
			wallet0 = Points.get_wallet()
			var paid: int = director.debug_cash_out()
			check(paid == value, "cashing out pays what it showed")
			phase = 7; wait_until = frame + 3
		7:
			check(Points.get_phase() == "upgrade", "cash-out ends the ante")
			check(director.get_screen_name() == "upgrade" and paused, "the upgrade screen opens over the paused world")
			var res: Dictionary = Points.get_last_result()
			check(int(res["interest"]) == mini(int(0.1 * (wallet0 + int(res["cash_out"]))), 100), "interest is 10%% of the Wallet capped at 0.1T (%d)" % int(res["interest"]))
			var screen: Control = director.get_screen()
			var w: int = Points.get_wallet()
			check(screen.buy("damage"), "buy a Damage level")
			check(w - Points.get_wallet() == 300, "the first level costs 0.30T = 300 (paid %d)" % (w - Points.get_wallet()))
			var draft: Array = Points.get_draft()
			check(draft.size() == 3, "the draft offers 3 perks")
			w = Points.get_wallet()
			check(screen.reroll() and w - Points.get_wallet() == 100, "a reroll costs 0.10T")
			check(Points.get_reroll_price() == 150, "the next reroll costs 0.05T more")
			var odds: Array = Points.get_scratch_odds()
			var total := 0.0
			for o in odds: total += float(o["percent"])
			check(odds.size() == 5 and absf(total - 100.0) < 0.01, "the ticket shows 5 rarity odds adding to 100%")
			var t: Dictionary = screen.scratch_now()
			check(bool(t.get("scratched", false)) and String(t.get("prize", "")) != "", "the scratch-off reveals a prize (%s)" % t.get("prize", ""))
			check(screen.pick("glass_cannon") or screen.pick(String(Points.get_draft()[0]["id"])), "pick a perk")
			check(Points.get_perks().size() == 1 and Points.has_picked_perk(), "one perk per draft")
			Points.debug_give_perk("glass_cannon")
			Points.debug_give_perk("glass_cannon")
			Points.debug_give_perk("deep_breath")
			var ids := Points.get_perks().map(func(p): return p["id"])
			check(ids.count("glass_cannon") == 1, "a perk is never held twice")
			check(absf(Points.stat("damage_dealt") - 1.3 * 1.08) < 0.01, "Glass Cannon and a Damage level multiply damage (x%.3f)" % Points.stat("damage_dealt"))
			check(Points.stat("damage_taken") >= 1.29, "Glass Cannon raises damage taken")
			screen.confirm()
			phase = 8; wait_until = frame + 5
		8:
			check(Points.get_ante() == 2, "on to ante 2")
			check(director.get_screen_name() == "board", "ante 2 opens on its board")
			check(Points.get_target() == 1700, "ante 2 target is 1,700")
			director.get_screen().confirm()
			phase = 9; wait_until = frame + 5
		9:
			var squad := camp()
			check(squad.size() == 4 and squad.all(func(e): return not e.is_dead() and not e.is_tied()), "ante 2 re-mans the Moon camp (%d)" % squad.size())
			check(Points.get_score() == 0, "Ante Score starts at 0 each ante")
			player.debug_set_oxygen(99999.0)
			var o2: float = player.get_oxygen_seconds()
			check(absf(o2 / max(o2_base, 0.01) - 1.4) < 0.02, "Deep Breath gives +40%% suit oxygen (%.0f -> %.0f s)" % [o2_base, o2])
			# Run out of time short of the target.
			Points.debug_set_time_left(0.3)
			phase = 10; wait_until = frame + 40
		10:
			check(Points.get_phase() == "over" and Points.get_outcome() == "time_up", "running out of time below the target ends the run")
			check(director.get_screen_name() == "summary", "the run summary opens")
			var board: Array = SaveData.get_leaderboard()
			check(board.size() >= 1 and int(board[0]["ante"]) >= 2, "the run is on the local leaderboard")
			director.get_screen().confirm()
			phase = 11; wait_until = frame + 3
		11:
			check(not Points.has_run(), "after the summary the run is cleared")
			check(not paused, "the world unpauses")
			finish(); return true
	return false
