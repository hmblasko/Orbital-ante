extends SceneTree
## Phase 5 feature test: planet weather.
## 1. The Moon has no air: no weather, no fog, no wind.
## 2. Mars opens calm (a breeze, light haze, nothing falling).
## 3. In a Mars dust storm: thick fog, blowing dust, strong wind that shoves
##    you while airborne, much less while standing.
## 4. In a dust-devil spell, whirling columns appear near you.
## 5. In an Earth thunderstorm, rain falls and lightning strikes.
## 6. Weather timelines are seeded: asking twice gives the same storm time.
## Exits with code 1 on any failure.
##
## Run from the godot/ folder:
##   godot --headless --path . --fixed-fps 60 --script res://tests/weather_test.gd

const SETTLE_FRAMES := 60
const MAX_DETAIL_FRAMES := 60 * 60
const REST_FRAMES := 90
const LIGHTNING_FRAMES := 60 * 25

var scene: Node
var player: CharacterBody3D
var sys: Node
var weather: Node
var frame := 0
var failures: Array[String] = []
var passes: Array[String] = []
var flashes := 0
## Steps: each places the player on a body, waits for its ground, then runs.
var step := 0
var step_frame := 0
var ready_frame := -1
var body_id := ""
var up_m := 1.0

func _initialize() -> void:
	scene = load("res://scenes/tests/solar_system.tscn").instantiate()
	root.add_child(scene)
	player = scene.get_node("Player")
	sys = scene.get_node("SolarSystem")

func check(ok: bool, what: String) -> void:
	if ok: passes.append(what)
	else: failures.append(what)

func finish() -> void:
	for p in passes: print("weather_test: ok   - " + p)
	for f in failures: print("weather_test: FAIL - " + f)
	print("weather_test: " + ("PASS" if failures.is_empty() else "FAIL"))
	quit(0 if failures.is_empty() else 1)

## Stand the player `up` metres above the ground at the north pole of `id`.
func place(id: String, up: float) -> void:
	var b: Node3D = sys.get_node(id)
	var r: float = b.surface_radius_toward(b.global_transform * (Vector3.UP * b.radius()))
	sys.debug_place_player_on(id, Vector3.UP * (r + up))

## Move to body `id` (waiting for its ground to stream in) before the next check.
func go(id: String, up: float) -> void:
	body_id = id; up_m = up
	place(id, up)
	ready_frame = -1
	step += 1; step_frame = 0

## Frames since the ground under the player was ready (-1 while waiting).
func since_ready() -> int:
	var b: Node = sys.get_node(body_id)
	if ready_frame < 0:
		if b.has_detail():
			place(body_id, up_m)
			ready_frame = step_frame
		elif step_frame > MAX_DETAIL_FRAMES:
			failures.append("%s ground never streamed in" % body_id)
			finish()
		return -1
	return step_frame - ready_frame

func _physics_process(_d: float) -> bool:
	frame += 1
	if frame < SETTLE_FRAMES: return false
	if frame == SETTLE_FRAMES:
		weather = sys.get_node_or_null("Weather")
		check(weather != null, "the solar system has a weather node")
		if weather == null:
			finish(); return true
		weather.debug_set_time(500.0)
		go("moon", 1.0)
		return false
	step_frame += 1
	var t := since_ready()
	match step:
		1:
			if t == REST_FRAMES:
				check(weather.get_event_name() == "" and weather.get_wind().length() < 0.01,
					"the airless Moon has no weather or wind")
				check(weather.get_fog_density() < 1e-5, "no fog on the Moon")
				weather.debug_set_time(5.0)
				go("mars", 1.0)
		2:
			if t == REST_FRAMES:
				var w: float = weather.get_wind().length()
				check(weather.get_event_name() == "" and w > 0.5 and w < 6.0,
					"Mars opens calm with a light breeze (%.1f m/s)" % w)
				check(not weather.is_precipitating(), "nothing blowing in calm air")
				var fog: float = weather.get_fog_density()
				check(fog > 0.0 and fog < 0.005, "light haze on Mars in calm air (%.4f)" % fog)
				var at: float = weather.debug_event_middle("mars", "Sandstorm")
				check(at > 0.0, "this run's Mars weather includes a dust storm")
				check(is_equal_approx(at, weather.debug_event_middle("mars", "Sandstorm")), "the storm time is seeded and repeatable")
				weather.debug_set_time(at)
				step += 1; step_frame = 0; ready_frame = 0
		3:
			if t == REST_FRAMES:
				check(weather.get_event_name() == "Sandstorm", "the dust storm is blowing (%s)" % weather.get_event_name())
				var w: float = weather.get_wind().length()
				check(w > 12.0, "storm wind is strong (%.1f m/s)" % w)
				check(weather.get_fog_density() > 0.015, "the storm fogs the air (%.4f)" % weather.get_fog_density())
				check(weather.is_precipitating(), "dust blows around the player")
				var ground: float = player.wind_acceleration().length()
				check(player.is_on_floor() and ground > 0.0 and ground < 1.0,
					"the wind pushes a standing player only a little (%.2f m/s^2)" % ground)
				place("mars", 30.0)
			elif t == REST_FRAMES + 10:
				var air: Vector3 = player.wind_acceleration()
				check(air.length() > 1.5 and air.normalized().dot(weather.get_wind().normalized()) > 0.9,
					"airborne, the storm shoves the player downwind (%.2f m/s^2)" % air.length())
				var at: float = weather.debug_event_middle("mars", "DustDevils")
				check(at > 0.0, "this run's Mars weather includes dust devils")
				weather.debug_set_time(at)
				place("mars", 1.0)
			elif t == REST_FRAMES * 2 + 10:
				check(weather.get_event_name() == "DustDevils" and weather.get_devil_count() == 3,
					"three dust devils whirl nearby (%s, %d)" % [weather.get_event_name(), weather.get_devil_count()])
				var near := true
				for p in weather.get_devil_positions():
					if p.distance_to(player.global_position) > 160.0: near = false
				check(near, "the dust devils are within sight")
				var at: float = weather.debug_event_middle("earth", "Thunderstorm")
				check(at > 0.0, "this run's Earth weather includes a thunderstorm")
				weather.debug_set_time(at)
				go("earth", 1.0)
		4:
			if t > REST_FRAMES and t < REST_FRAMES + LIGHTNING_FRAMES:
				if weather.is_flashing(): flashes += 1
			elif t == REST_FRAMES + LIGHTNING_FRAMES:
				check(weather.get_event_name() == "Thunderstorm" and weather.is_precipitating(), "rain falls in the thunderstorm")
				check(flashes > 0, "lightning strikes during the storm (%d flash frames)" % flashes)
				check(weather.get_devil_count() == 0, "the dust devils stay on Mars")
				finish(); return true
	return false
