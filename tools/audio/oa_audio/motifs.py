"""The four recurring themes of Orbital Ante, in one place.

Every composition and every reward sound takes its notes from here, which is what ties the audio together.
Scores use the notation in seq.py (pitch:beats). All four are written around D.
"""

# Orbital Ante theme: whimsical and adventurous. A rising three-note call (5-6-1), an answer that leans on the
# flattened seventh (the "wink"), then the call again, climbing one step further and coming home.
ORBITAL_ANTE = [
    "r:.5 A4:.5 B4:.5 D5:1.5 r:1",
    "F#5:.75 E5:.75 C5:.5 A4:2",
    "r:.5 B4:.5 D5:.5 E5:1.5 r:1",
    "F#5:.5 E5:.5 D5:1 B4:.5 D5:1.5",
]
# The first three notes on their own are the "ante cell", used for rewards and as a counter-line elsewhere.
ANTE_CELL = ["A4", "B4", "D5"]

# Exploration theme: wonder and curiosity. An open rising fifth that settles a step down, then a reach up to
# the seventh that never quite resolves, like a question.
EXPLORATION = [
    "D4:2 A4:2",
    "G4:3 r:1",
    "C5:1.5 B4:.5 G4:2",
    "A4:3 r:1",
]

# Conflict theme: urgency. A tight cell worrying at the home note and the semitone above it, then a drop and a
# scramble back up.
CONFLICT = [
    "D5:.5 D5:.5 Eb5:.5 D5:1 C5:.5 A4:1",
    "Bb4:1.5 r:.5 F5:.5 E5:.5 D5:1",
    "D5:.5 D5:.5 Eb5:.5 D5:1 C5:.5 F5:1",
    "G5:1 F5:.5 E5:.5 D5:2",
]

# Cosmic Mystery theme: loneliness and awe. Wide, slow intervals; the sharpened fourth keeps it weightless.
COSMIC_MYSTERY = [
    "D4:4",
    "A4:3 G#4:1",
    "E5:4",
    "C#5:3 F#4:1",
]
