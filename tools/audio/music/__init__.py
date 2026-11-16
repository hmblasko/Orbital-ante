"""The Orbital Ante soundtrack. Each module is one composition; PIECES lists them in build order."""
from . import combat_all_in, cues, explore_regolith, flight_burn_window, main_long_way, mystery_dish  # noqa: F401

PIECES = [explore_regolith, combat_all_in, flight_burn_window, main_long_way, mystery_dish]

# How the game's music director uses the pieces. This is data, read by MusicBrain in the game, so the musical
# decisions live beside the music and not in code.
DIRECTION: dict = {
    # Which piece belongs to which situation. An empty track means "no music here": silence is a choice.
    "contexts": [
        {"id": "menu", "track": "main_long_way", "maxLoops": 0, "restSeconds": 0, "combat": False},
        {"id": "surface", "track": "explore_regolith", "maxLoops": 2, "restSeconds": 50, "combat": True},
        {"id": "flight", "track": "flight_burn_window", "maxLoops": 2, "restSeconds": 40, "combat": True},
        {"id": "mystery", "track": "mystery_dish", "maxLoops": 0, "restSeconds": 0, "combat": True},
        {"id": "space", "track": "", "maxLoops": 0, "restSeconds": 0, "combat": True},
        {"id": "silence", "track": "", "maxLoops": 0, "restSeconds": 0, "combat": False},
    ],
    "combatTrack": "combat_all_in",
    "combatEnter": 60,       # intensity that starts the combat music
    "combatExit": 38,        # it must fall below this...
    "combatMinSeconds": 14,  # ...and combat music must have played at least this long, before it resolves
    "ambushIntensity": 85,   # a jump straight to this skips the riser and lands the hit on the next beat
    # How one piece hands over to the next. First matching rule wins; "*" matches anything, "" is silence.
    # quantize: where the hand-over may happen in the OLD piece ("beat", "bar", "phrase" = 4 bars, "now").
    # cue: a transition cue; its lead-in plays over the old piece, and the new piece starts where the cue lands.
    # fadeOut / fadeIn in seconds. With no riser, the old piece is given its whole fade and finishes on the grid.
    # hush = seconds of nothing but the cue's tail before the new piece enters.
    "transitions": [
        {"from": "*", "to": "combat_all_in", "quantize": "beat", "cue": "to_combat", "ambushCue": "hit_combat",
         "fadeOut": 2.2, "fadeIn": 0.0, "hush": 0.0, "entry": "start"},
        {"from": "combat_all_in", "to": "*", "quantize": "2bars", "cue": "resolve_combat", "ambushCue": "",
         "fadeOut": 0.12, "fadeIn": 5.0, "hush": 5.5, "entry": "start"},
        {"from": "combat_all_in", "to": "", "quantize": "2bars", "cue": "resolve_combat", "ambushCue": "",
         "fadeOut": 0.12, "fadeIn": 0.0, "hush": 0.0, "entry": "start"},
        {"from": "*", "to": "flight_burn_window", "quantize": "beat", "cue": "to_flight", "ambushCue": "",
         "fadeOut": 3.0, "fadeIn": 0.0, "hush": 0.0, "entry": "start"},
        {"from": "flight_burn_window", "to": "", "quantize": "bar", "cue": "to_calm", "ambushCue": "",
         "fadeOut": 4.0, "fadeIn": 0.0, "hush": 0.0, "entry": "start"},
        {"from": "flight_burn_window", "to": "*", "quantize": "bar", "cue": "to_calm", "ambushCue": "",
         "fadeOut": 4.0, "fadeIn": 5.0, "hush": 2.5, "entry": "start"},
        # A negative hush is an overlap: the unknown seeps in under the old piece before it has gone.
        {"from": "*", "to": "mystery_dish", "quantize": "bar", "cue": "", "ambushCue": "",
         "fadeOut": 6.0, "fadeIn": 7.0, "hush": -4.0, "entry": "start"},
        {"from": "*", "to": "*", "quantize": "bar", "cue": "", "ambushCue": "",
         "fadeOut": 4.0, "fadeIn": 4.0, "hush": 0.5, "entry": "start"},
    ],
}
