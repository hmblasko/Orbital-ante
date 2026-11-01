extends SceneTree
## Phase 6 feature test: combat on the Moon (vertical slice).
## 1. The Moon camp spawns its Venera Collective soldiers (3 tier 1, 1 tier 2),
##    all on patrol.
## 2. A gunshot nearby alerts them; they shoot back and the shots hurt.
## 3. The rifle hits a soldier (hit counted, hit marker shown).
## 4. Shooting an arm disarms: the gun drops as a pickup.
## 5. Shooting a leg off drops the soldier and leaves a gib.
## 6. A killing hit turns the soldier into a ragdoll.
## 7. The laser double-barrel at close range gibs; the baseball bat hurts;
##    the rock flies.
## 8. The rope gun ties a soldier up (gun drops); X picks it up and it rides
##    along; X puts it down. Only 3 rope shots per run.
## 9. Gibs never exceed the cap (enemies.ron gibs.max_gibs).
## Exits with code 1 on any failure.
##
## Run from the godot/ folder:
##   godot --headless --path . --fixed-fps 60 --script res://tests/combat_test.gd

const MAX_DETAIL_FRAMES := 60 * 60
const ALERT_WAIT_FRAMES := 60 * 20
## gibs.max_gibs in data/combat/enemies.ron.
const GIB_CAP := 60
const HEAD := 0
const TORSO := 1
const ARM_R := 3
const LEG_L := 4

var scene: Node
var player: CharacterBody3D
var sys: Node
var moon: Node3D
var arsenal: Node
var enemies: Array = []
var frame := 0
var phase := 0
var wait_until := 0
var failures: Array[String] = []
var passes: Array[String] = []
var max_gibs := 0
var health_before := 1.0
var hits_before := 0
var died_count := 0
var target: Node3D

func _initialize() -> void:
	scene = load("res://scenes/tests/solar_system.tscn").instantiate()
	root.add_child(scene)
	player = scene.get_node("Player")
	sys = scene.get_node("SolarSystem")
	arsenal = player.get_node("Head/Arsenal")

func check(ok: bool, what: String) -> void:
	if ok: passes.append(what)
	else: failures.append(what)

func finish() -> void:
	for p in passes: print("combat_test: ok   - " + p)
	for f in failures: print("combat_test: FAIL - " + f)
	print("combat_test: " + ("PASS" if failures.is_empty() else "FAIL"))
	quit(0 if failures.is_empty() else 1)

func alive() -> Array:
	var out := []
	for e in enemies:
		if is_instance_valid(e) and not e.is_dead(): out.append(e)
	return out

func up_at(n: Node3D) -> Vector3:
	return (n.global_position - moon.global_position).normalized()

## Stand `dist` metres from the soldier, on the Moon.
## Stand `dist` metres from `e`, on a side with a clear line to it (the
## camp's base walls and crates can sit in between).
func stand_near(e: Node3D, dist: float) -> void:
	var lp: Vector3 = moon.global_transform.affine_inverse() * e.global_position
	var up := lp.normalized()
	var side0 := up.cross(Vector3.RIGHT).normalized()
	var space := root.get_world_3d().direct_space_state
	var best := Vector3.ZERO
	for i in 12:
		var side := side0.rotated(up, TAU * i / 12.0)
		var dir := (lp + side * dist).normalized()
		var ground: float = moon.surface_radius_toward(moon.global_transform * (dir * 100.0))
		var spot := dir * (ground + 1.0)
		if best == Vector3.ZERO: best = spot
		var from: Vector3 = moon.global_transform * (spot + dir * 0.6)
		var q := PhysicsRayQueryParameters3D.create(from, e.global_position + (e.global_position - moon.global_position).normalized() * 1.0)
		q.exclude = [player.get_rid()]
		var hit := space.intersect_ray(q)
		if hit.is_empty() or hit.collider == e or (hit.collider is Node and e.is_ancestor_of(hit.collider)):
			best = spot
			break
	sys.debug_place_player_on("moon", best)

func aim_at(e: Node3D, limb: int) -> void:
	player.debug_look_at(e.get_limb_position(limb))

func on_died(_tier: int) -> void:
	died_count += 1

func _process(_d: float) -> bool:
	frame += 1
	max_gibs = max(max_gibs, get_nodes_in_group("gibs").size())
	if frame == 5:
		moon = sys.get_node("moon")
		moon.request_detail()
	if frame > MAX_DETAIL_FRAMES + 60 * 120:
		check(false, "test finished in time (stuck in phase %d)" % phase)
		finish(); return true
	if frame < wait_until:
		return false
	match phase:
		0:
			if frame > 5 and moon.has_detail():
				for c in moon.get_children():
					if c.has_method("get_state_name"):
						enemies.append(c)
						c.died.connect(on_died)
				var t1 := enemies.filter(func(e): return e.get_tier() == 1).size()
				var t2 := enemies.filter(func(e): return e.get_tier() == 2).size()
				check(enemies.size() == 4 and t1 == 3 and t2 == 1, "Moon camp has 3 tier 1 and 1 tier 2 soldiers (got %d: %d/%d)" % [enemies.size(), t1, t2])
				check(enemies.all(func(e): return e.get_state_name() == "Patrol"), "every soldier starts on patrol")
				# Hold them still while the player settles, so the test
				# doesn't depend on whether a patrol happens to face this way.
				for e in enemies: e.debug_set_frozen(true)
				stand_near(enemies[0], 14.0)
				phase = 1; wait_until = frame + 180
			elif frame > MAX_DETAIL_FRAMES:
				check(false, "Moon detail loaded"); finish(); return true
		1:
			check(player.is_on_floor(), "player stands near the camp")
			check(enemies.all(func(e): return e.get_state_name() == "Patrol"), "still on patrol before the shot")
			for e in enemies: e.debug_set_frozen(false)
			# A shot into the ground nearby: loud enough to bring them running.
			player.debug_look_at(player.global_position - up_at(player) * 2.0 + (enemies[0].global_position - player.global_position).normalized())
			arsenal.debug_fire_once()
			health_before = player.get_health_fraction()
			phase = 2; wait_until = frame + 2
		2:
			var states := enemies.map(func(e): return e.get_state_name())
			var alerted := states.any(func(s): return s == "Alert")
			var shots := 0
			for e in enemies: shots += e.get_shots_fired()
			var hits_on := 0
			for e in enemies: hits_on += e.get_hits_on_player()
			if alerted and hits_on > 0 or frame - wait_until > ALERT_WAIT_FRAMES:
				check(alerted, "a gunshot alerts the camp (states %s)" % [states])
				check(enemies.any(func(e): return e.get_tell() == "!"), "an alerted soldier shows the red '!' tell")
				check(shots > 0, "alerted soldiers shoot back (%d shots)" % shots)
				check(hits_on > 0 and player.get_health_fraction() < health_before, "their hits hurt the suit (%d hits, health %.2f)" % [hits_on, player.get_health_fraction()])
				check(not player.is_dead(), "the player survives the first volley")
				for e in enemies: e.debug_set_frozen(true)
				target = enemies[0]
				stand_near(target, 5.0)
				phase = 3; wait_until = frame + 60
		3:
			aim_at(target, TORSO)
			hits_before = arsenal.get_hits()
			phase = 4; wait_until = frame + 3
		4:
			arsenal.debug_fire_once()
			phase = 5; wait_until = frame + 2
		5:
			check(arsenal.get_hits() > hits_before, "the rifle hits a soldier at 5 m")
			check(arsenal.get_hit_marker() > 0.0, "a hit shows the hit marker")
			check(target.get_health_fraction() < 1.0, "the hit costs health")
			var pickups_before := get_nodes_in_group("weapon_pickups").size()
			target.debug_hit(ARM_R, 40.0)
			check(not target.is_armed(), "shooting an arm disarms")
			check(get_nodes_in_group("weapon_pickups").size() > pickups_before, "the dropped gun is a pickup")
			var gibs_before := get_nodes_in_group("gibs").size()
			for i in 6:
				if target.has_limb(LEG_L): target.debug_hit(LEG_L, 40.0)
			check(target.is_down(), "losing a leg drops the soldier")
			check(not target.has_limb(LEG_L), "the leg comes off")
			check(get_nodes_in_group("gibs").size() > gibs_before, "the lost leg is a gib")
			target.debug_hit(HEAD, 500.0)
			phase = 6; wait_until = frame + 10
		6:
			check(died_count >= 1, "a killing hit fires died")
			check(get_nodes_in_group("ragdoll_pieces").size() > 0, "the dead soldier becomes a ragdoll")
			# Laser double-barrel at point blank on the next soldier.
			target = alive()[0]
			stand_near(target, 2.5)
			arsenal.debug_give("laser_shotgun")
			phase = 7; wait_until = frame + 60
		7:
			aim_at(target, TORSO)
			hits_before = arsenal.get_hits()
			phase = 8; wait_until = frame + 3
		8:
			arsenal.debug_fire_once()
			phase = 9; wait_until = frame + 2
		9:
			check(arsenal.get_weapon_id() == "laser_shotgun", "picked up the laser double-barrel")
			check(arsenal.get_hits() > hits_before, "the laser double-barrel hits at 2.5 m")
			check(not is_instance_valid(target) or target.is_dead() or target.get_health_fraction() < 0.5, "a point-blank blast takes most of a soldier")
			# Baseball bat on the next soldier.
			target = alive()[0]
			stand_near(target, 1.4)
			arsenal.debug_give("bat")
			phase = 10; wait_until = frame + 60
		10:
			aim_at(target, TORSO)
			health_before = target.get_health_fraction()
			phase = 11; wait_until = frame + 3
		11:
			check(arsenal.get_melee_id() == "bat", "picked up the baseball bat")
			arsenal.debug_swing()
			phase = 12; wait_until = frame + 20
		12:
			check(not is_instance_valid(target) or target.is_dead() or target.get_health_fraction() < health_before, "the bat connects")
			arsenal.debug_hold_rock(true)
			arsenal.debug_fire_once()
			phase = 13; wait_until = frame + 4
		13:
			var rocks := get_nodes_in_group("gibs").filter(func(g): return String(g.name).begins_with("ThrownRock"))
			check(not rocks.is_empty(), "the rock is thrown")
			check(arsenal.has_infinite_ammo(), "rocks never run out")
			arsenal.debug_hold_rock(false)
			target = alive()[0]
			for e in alive(): e.debug_set_frozen(true)
			stand_near(target, 6.0)
			phase = 20; wait_until = frame + 60
		20:
			aim_at(target, TORSO)
			phase = 21; wait_until = frame + 3
		21:
			check(arsenal.get_rope_shots() == 3, "3 rope shots per run")
			arsenal.debug_rope_once()
			phase = 22; wait_until = frame + 3
		22:
			check(arsenal.get_rope_shots() == 2, "a rope shot is used")
			check(target.is_tied(), "the rope ties the soldier up")
			check(not target.is_armed(), "a tied soldier drops its gun")
			target.debug_set_frozen(false)
			stand_near(target, 1.6)
			phase = 23; wait_until = frame + 60
		23:
			aim_at(target, TORSO)
			phase = 24; wait_until = frame + 3
		24:
			check(arsenal.get_carry_prompt() == "carry", "looking at a tied soldier offers Pick up")
			arsenal.debug_interact()
			phase = 25; wait_until = frame + 40
		25:
			check(arsenal.is_carrying() and target.is_carried(), "X picks the tied soldier up")
			check(target.global_position.distance_to(player.global_position) < 2.5, "the carried soldier rides along with you")
			check(arsenal.get_carry_prompt() == "drop", "carrying offers Put down")
			arsenal.debug_interact()
			phase = 26; wait_until = frame + 90
		26:
			check(not arsenal.is_carrying() and not target.is_carried(), "X puts the soldier down")
			check(target.global_position.distance_to(player.global_position) < 4.0, "it lands near you")
			check(target.is_tied() and not target.is_dead(), "still tied and alive")
			arsenal.debug_rope_once()
			phase = 27; wait_until = frame + 2
		27:
			arsenal.debug_rope_once()
			phase = 28; wait_until = frame + 2
		28:
			arsenal.debug_rope_once()
			phase = 29; wait_until = frame + 2
		29:
			check(arsenal.get_rope_shots() == 0, "no more than 3 rope shots (misses count)")
			# Gib cap: blow every limb off the remaining soldiers, twice over.
			for e in alive():
				e.debug_set_frozen(true)
				for l in [2, 3, 4, 5]:
					if e.has_limb(l): e.debug_hit(l, 500.0)
			phase = 14; wait_until = frame + 30
		14:
			check(max_gibs <= GIB_CAP, "gibs stay under the cap (max %d)" % max_gibs)
			finish(); return true
	return false
