"""Builds the game's guns: original, chunky, hand-painted models.

    blender -b -P tools/blender/build_weapons.py            (writes the game's model files)
    blender -b -P tools/blender/build_weapons.py -- --preview <folder>   (also renders pictures)

A gun's space here: origin where the firing hand holds it, Y out of the barrel, Z up, metres.
The four sockets keep the positions the placeholder guns had, so hands, tracers and reloads still line up.
"""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import oa_blender as oa
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "unity", "Assets", "OrbitalAnte", "Resources", "Models"))

GUNMETAL = (0.21, 0.23, 0.29)
DARK = (0.12, 0.13, 0.16)
STEEL = (0.56, 0.58, 0.63)
WOOD = (0.66, 0.36, 0.16)
WOOD_DARK = (0.50, 0.25, 0.11)
TAPE = (0.86, 0.62, 0.20)
SIGNAL = (0.86, 0.30, 0.14)
BARE = (0.72, 0.72, 0.70)


def metal(o, seed=0):
    oa.paint(o, wear=0.7, dirt=0.6, mottle=0.6, seed=seed, wear_colour=BARE, grain=22.0)
    return o


def wood(o, seed=0):
    oa.paint(o, wear=0.55, dirt=0.7, mottle=1.1, seed=seed, wear_colour=(0.85, 0.60, 0.34), gradient=0.12, grain=16.0)
    return o


def soft(o, seed=0):
    oa.paint(o, wear=0.3, dirt=0.5, mottle=0.8, seed=seed, grain=22.0)
    return o


def banana(y0, z0, length, half, start_deg, end_deg, steps=9):
    """Outline of a curved magazine: starts at the top and curves forward as it goes down."""
    left, right = [], []
    y, z = y0, z0
    for i in range(steps + 1):
        t = i / steps
        lean = math.radians(start_deg + (end_deg - start_deg) * t)
        dy, dz = math.sin(lean), -math.cos(lean)
        w = half * (1.0 - 0.06 * t)
        left.append((y - dz * -w, z + dy * -w))
        right.append((y + dz * -w, z - dy * -w))
        if i < steps:
            y += dy * length / steps
            z += dz * length / steps
    return left + list(reversed(right))


def banana_end(y0, z0, length, start_deg, end_deg, steps=9):
    """Where the middle of that magazine's bottom edge ends up."""
    y, z = y0, z0
    for i in range(steps):
        lean = math.radians(start_deg + (end_deg - start_deg) * i / steps)
        y += math.sin(lean) * length / steps
        z -= math.cos(lean) * length / steps
    return y, z


def rifle():
    body, mag = [], []
    # Receiver and its lid.
    body.append(metal(oa.slab("receiver", [(-0.085, 0.010), (0.275, 0.010), (0.275, 0.068), (0.25, 0.082), (-0.06, 0.082), (-0.085, 0.058)], 0.052, GUNMETAL, bevel=0.007), 1))
    body.append(metal(oa.slab("lid", [(-0.062, 0.076), (0.205, 0.076), (0.205, 0.092), (0.18, 0.101), (-0.04, 0.101), (-0.062, 0.09)], 0.045, (0.27, 0.29, 0.35), bevel=0.006), 2))
    for i in range(3):
        body.append(metal(oa.box("rib", (0, -0.015 + 0.075 * i, 0.101), (0.048, 0.020, 0.008), DARK, bevel=0.003), 3 + i))
    body.append(metal(oa.box("rear sight", (0, 0.228, 0.094), (0.032, 0.040, 0.018), DARK, bevel=0.004), 9))
    for x in (-0.010, 0.010):
        body.append(metal(oa.box("rear sight ear", (x, 0.236, 0.108), (0.007, 0.014, 0.014), DARK, bevel=0.002), 10))
    # Barrel, gas tube, the block that joins them, cleaning rod.
    body.append(metal(oa.cylinder("barrel", (0, 0.27, 0.045), (0, 0.645, 0.045), 0.0125, DARK, segments=16), 11))
    body.append(metal(oa.cylinder("gas tube", (0, 0.44, 0.079), (0, 0.50, 0.079), 0.0105, GUNMETAL, segments=14, bevel=0.003), 12))
    body.append(metal(oa.box("gas block", (0, 0.492, 0.061), (0.034, 0.034, 0.064), GUNMETAL, bevel=0.007), 13))
    body.append(metal(oa.cylinder("rod", (0, 0.45, 0.024), (0, 0.635, 0.024), 0.0065, BARE, segments=8), 14))
    # Front sight: a block, two ears and a signal-orange post between them.
    body.append(metal(oa.box("front sight base", (0, 0.598, 0.062), (0.026, 0.028, 0.040), GUNMETAL, bevel=0.006), 15))
    for x in (-0.014, 0.014):
        body.append(metal(oa.slab("front sight ear", [(0.586, 0.078), (0.610, 0.078), (0.606, 0.104), (0.590, 0.104)], 0.006, GUNMETAL, bevel=0.002, x=x), 16))
    body.append(soft(oa.cylinder("front sight post", (0, 0.598, 0.078), (0, 0.598, 0.120), 0.0055, SIGNAL, segments=8, bevel=0.002), 17))
    # Muzzle brake: fat, with a slanted mouth.
    body.append(metal(oa.lathe("brake", (0, 0.632, 0.045), (0, 1, 0), [(0.0, 0.0135), (0.006, 0.0215), (0.036, 0.0215), (0.058, 0.0175), (0.058, 0.0095), (0.03, 0.0095)], GUNMETAL, segments=16), 18))
    body.append(metal(oa.box("brake fin", (0, 0.655, 0.068), (0.012, 0.030, 0.012), GUNMETAL, bevel=0.003, rot=(-18, 0, 0)), 19))
    # Wooden furniture: lower and upper handguard, grip and stock.
    body.append(wood(oa.slab("handguard", [(0.272, 0.004), (0.452, 0.014), (0.452, 0.058), (0.272, 0.062)], 0.064, WOOD, bevel=0.012, segments=3), 20))
    body.append(wood(oa.cylinder("upper handguard", (0, 0.278, 0.079), (0, 0.44, 0.079), 0.0215, WOOD, segments=16, bevel=0.006), 21))
    for y in (0.283, 0.435):
        body.append(metal(oa.box("handguard band", (0, y, 0.05), (0.068, 0.012, 0.098), GUNMETAL, bevel=0.005), 22))
    body.append(wood(oa.slab("grip", [(0.020, 0.014), (-0.034, 0.014), (-0.066, -0.100), (-0.020, -0.112), (0.006, -0.030)], 0.040, WOOD_DARK, bevel=0.011, segments=3), 23))
    body.append(wood(oa.slab("stock", [(-0.078, 0.072), (-0.078, 0.018), (-0.352, -0.050), (-0.352, 0.062)], 0.046, WOOD, bevel=0.013, segments=3), 24))
    body.append(wood(oa.slab("cheek rest", [(-0.13, 0.066), (-0.30, 0.060), (-0.29, 0.078), (-0.15, 0.084)], 0.036, WOOD_DARK, bevel=0.009, segments=3), 44))
    body.append(metal(oa.cylinder("sling pin", (-0.026, -0.30, 0.0), (0.026, -0.30, 0.0), 0.006, BARE, segments=10, bevel=0.002), 45))
    body.append(metal(oa.slab("butt plate", [(-0.348, -0.052), (-0.366, -0.046), (-0.366, 0.058), (-0.348, 0.066)], 0.049, GUNMETAL, bevel=0.005), 25))
    # A wrap of mustard tape round the stock, put on by hand and not quite straight.
    body.append(soft(oa.box("tape", (0, -0.205, 0.024), (0.051, 0.040, 0.088), TAPE, bevel=0.008, rot=(-9, 0, 3)), 26))
    body.append(soft(oa.box("tape", (0, -0.238, 0.016), (0.050, 0.022, 0.086), TAPE, bevel=0.007, rot=(-13, 0, -4)), 27))
    # Trigger, its guard, the magazine catch.
    body.append(metal(oa.box("guard bottom", (0, 0.048, -0.030), (0.014, 0.094, 0.006), DARK, bevel=0.0025), 28))
    body.append(metal(oa.box("guard front", (0, 0.092, -0.010), (0.014, 0.006, 0.045), DARK, bevel=0.0025, rot=(8, 0, 0)), 29))
    body.append(metal(oa.box("trigger", (0, 0.040, -0.001), (0.008, 0.009, 0.030), BARE, bevel=0.003, rot=(18, 0, 0)), 30))
    body.append(metal(oa.box("catch", (0, 0.106, -0.004), (0.016, 0.012, 0.030), DARK, bevel=0.003, rot=(-20, 0, 0)), 31))
    # Right side: charging handle and the long selector lever. Left side: rivets.
    body.append(metal(oa.cylinder("handle", (0.022, 0.135, 0.072), (0.052, 0.135, 0.072), 0.0065, BARE, segments=10, bevel=0.002), 32))
    body.append(metal(oa.sphere("handle knob", (0.054, 0.135, 0.072), 0.010, BARE, segments=10), 33))
    body.append(metal(oa.box("selector", (0.0275, 0.045, 0.040), (0.005, 0.105, 0.014), DARK, bevel=0.002, rot=(-7, 0, 0)), 34))
    body.append(metal(oa.cylinder("selector pin", (0.024, 0.092, 0.047), (0.033, 0.092, 0.047), 0.008, DARK, segments=10, bevel=0.002), 35))
    for x in (-0.0265, 0.0265):
        for y, z in ((-0.06, 0.03), (0.215, 0.028), (0.25, 0.058), (-0.03, 0.062)):
            body.append(metal(oa.sphere("rivet", (x, y, z), 0.0068, BARE, scale=(0.5, 1, 1), segments=8), 36))
    body.append(metal(oa.cylinder("sling ring", (-0.03, 0.46, 0.036), (-0.042, 0.46, 0.036), 0.011, BARE, segments=10, bevel=0.002), 37))

    # The magazine: the famous curve, made fat, with a hazard band and a floor plate.
    mag.append(metal(oa.slab("magazine", banana(0.150, 0.016, 0.215, 0.034, 6, 40), 0.034, (0.25, 0.27, 0.33), bevel=0.007, segments=2), 40))
    by, bz = banana_end(0.150, 0.016, 0.215 * 0.6, 6, 6 + 34 * 0.6, steps=54)
    mag.append(soft(oa.slab("magazine band", banana(by, bz, 0.028, 0.0352, 26.4, 31, steps=2), 0.0362, TAPE, bevel=0.004), 41))
    fy, fz = banana_end(0.150, 0.016, 0.211, 6, 40, steps=90)
    mag.append(metal(oa.slab("magazine floor", banana(fy, fz, 0.014, 0.038, 40, 40, steps=1), 0.040, DARK, bevel=0.005), 42))
    ry, rz = banana_end(0.150, 0.016, 0.215 * 0.12, 6, 6 + 34 * 0.12, steps=12)
    for x in (-0.0172, 0.0172):
        mag.append(metal(oa.slab("magazine rib", banana(ry, rz, 0.075, 0.0045, 10, 24, steps=5), 0.004, DARK, bevel=0.0015, x=x), 43))

    sockets = [oa.socket("Muzzle", (0, 0.69, 0.045)), oa.socket("SupportHand", (0, 0.37, 0.01)), oa.socket("Magazine", (0, 0.15, 0.01))]
    return [("Body", body), ("Magazine", mag)], sockets


def pistol():
    body, mag = [], []
    # A big two-tone slide over a dark frame.
    body.append(metal(oa.slab("slide", [(-0.046, 0.044), (0.150, 0.044), (0.150, 0.072), (0.138, 0.085), (-0.036, 0.085), (-0.046, 0.074)], 0.036, STEEL, bevel=0.006, segments=3), 1))
    for x in (-0.0183, 0.0183):
        for i in range(5):
            body.append(metal(oa.box("serration", (x, -0.034 + 0.010 * i, 0.064), (0.0035, 0.0045, 0.030), DARK, bevel=0.0012, rot=(-12, 0, 0)), 2))
    body.append(metal(oa.box("port", (0.0165, 0.062, 0.073), (0.005, 0.040, 0.014), DARK, bevel=0.002), 3))
    body.append(metal(oa.cylinder("barrel", (0, 0.120, 0.061), (0, 0.157, 0.061), 0.0118, DARK, segments=14, bevel=0.002), 4))
    body.append(metal(oa.cylinder("bore", (0, 0.150, 0.061), (0, 0.158, 0.061), 0.0066, (0.03, 0.03, 0.04), segments=10), 5))
    body.append(metal(oa.slab("frame", [(-0.042, 0.047), (0.137, 0.047), (0.137, 0.031), (0.095, 0.020), (-0.024, 0.020), (-0.042, 0.030)], 0.031, DARK, bevel=0.006), 6))
    body.append(metal(oa.slab("grip frame", [(0.016, 0.028), (-0.040, 0.028), (-0.060, -0.078), (-0.014, -0.084), (0.006, -0.018)], 0.030, DARK, bevel=0.008, segments=3), 7))
    for x in (-0.0165, 0.0165):
        body.append(wood(oa.slab("grip panel", [(0.006, 0.016), (-0.034, 0.016), (-0.051, -0.066), (-0.018, -0.070), (-0.001, -0.018)], 0.008, WOOD, bevel=0.0035, x=x), 8))
        body.append(metal(oa.sphere("grip screw", (x * 1.22, -0.022, -0.026), 0.0048, BARE, scale=(0.5, 1, 1), segments=8), 9))
    # Trigger and guard.
    body.append(metal(oa.box("guard bottom", (0, 0.048, -0.014), (0.013, 0.092, 0.006), DARK, bevel=0.0025), 10))
    body.append(metal(oa.box("guard front", (0, 0.091, 0.004), (0.013, 0.006, 0.040), DARK, bevel=0.0025, rot=(10, 0, 0)), 11))
    body.append(metal(oa.box("trigger", (0, 0.046, 0.011), (0.007, 0.008, 0.026), BARE, bevel=0.003, rot=(16, 0, 0)), 12))
    # Hammer, sights, a safety catch.
    body.append(metal(oa.slab("hammer", [(-0.044, 0.050), (-0.060, 0.060), (-0.062, 0.076), (-0.050, 0.078), (-0.044, 0.068)], 0.010, DARK, bevel=0.003), 13))
    body.append(soft(oa.box("front sight", (0, 0.136, 0.089), (0.007, 0.014, 0.010), SIGNAL, bevel=0.002), 14))
    for x in (-0.009, 0.009):
        body.append(metal(oa.box("rear sight", (x, -0.030, 0.089), (0.008, 0.012, 0.010), DARK, bevel=0.002), 15))
    body.append(metal(oa.cylinder("catch", (-0.016, -0.018, 0.050), (-0.0205, -0.018, 0.050), 0.007, SIGNAL, segments=10, bevel=0.0015), 16))
    # The magazine's floor plate is its own piece so a reload can take it away.
    mag.append(soft(oa.box("magazine floor", (0, -0.038, -0.086), (0.031, 0.056, 0.012), TAPE, bevel=0.004, rot=(-7, 0, 0)), 20))

    sockets = [oa.socket("Muzzle", (0, 0.155, 0.062)), oa.socket("SupportHand", (0, 0.0, -0.03)), oa.socket("Magazine", (0, -0.022, -0.07))]
    return [("Body", body), ("Magazine", mag)], sockets


# ------------------------------------------------------------------ the enemy kinds' guns

BRASS = (0.72, 0.56, 0.24)
YELLOW = (0.93, 0.74, 0.16)
OLIVE = (0.36, 0.42, 0.25)
OLIVE_DARK = (0.25, 0.30, 0.18)
RUBBER = (0.10, 0.10, 0.12)
CELL = (0.45, 1.0, 0.55)
LENS = (1.0, 0.25, 0.28)
GLASS = (0.16, 0.30, 0.40)


def lit(o):
    """Something that shines by itself: flat colour, drawn brighter than white by the game (piece names end in Glow)."""
    oa.paint(o, wear=0, dirt=0, mottle=0, gradient=0)
    return o


def smg():
    """The Runner's gun: a stubby box with the magazine up through the grip, far too long for it."""
    body, mag = [], []
    body.append(metal(oa.slab("receiver", [(-0.105, 0.016), (0.135, 0.016), (0.135, 0.070), (0.122, 0.080), (-0.092, 0.080), (-0.105, 0.066)], 0.046, GUNMETAL, bevel=0.007), 1))
    body.append(metal(oa.slab("lid", [(-0.080, 0.076), (0.110, 0.076), (0.110, 0.086), (0.100, 0.092), (-0.070, 0.092), (-0.080, 0.086)], 0.036, (0.27, 0.29, 0.35), bevel=0.004), 2))
    # The cocking knob on top, big enough to grab in a glove.
    body.append(metal(oa.cylinder("knob stem", (0, 0.030, 0.088), (0, 0.030, 0.104), 0.008, DARK, segments=10), 3))
    body.append(metal(oa.cylinder("knob", (0, 0.030, 0.102), (0, 0.030, 0.114), 0.016, BARE, segments=14, bevel=0.004), 4))
    # A short barrel in a fat nut, and a ring of cooling holes.
    body.append(metal(oa.lathe("barrel nut", (0, 0.132, 0.046), (0, 1, 0), [(0.0, 0.016), (0.004, 0.024), (0.026, 0.024), (0.032, 0.018), (0.032, 0.012)], DARK, segments=14), 5))
    body.append(metal(oa.cylinder("barrel", (0, 0.150, 0.046), (0, 0.218, 0.046), 0.0125, DARK, segments=14, bevel=0.002), 6))
    body.append(metal(oa.cylinder("bore", (0, 0.212, 0.046), (0, 0.2195, 0.046), 0.007, (0.03, 0.03, 0.04), segments=10), 7))
    for i in range(3):
        for x in (-0.0236, 0.0236):
            body.append(metal(oa.sphere("vent", (x, 0.050 + 0.026 * i, 0.058), 0.0075, (0.05, 0.05, 0.06), scale=(0.35, 1, 1), segments=8), 8))
    # Sights: two ears each end, a signal post at the front.
    for y in (0.112, -0.082):
        for x in (-0.012, 0.012):
            body.append(metal(oa.slab("sight ear", [(y - 0.010, 0.078), (y + 0.010, 0.078), (y + 0.007, 0.108), (y - 0.007, 0.108)], 0.005, GUNMETAL, bevel=0.0018, x=x), 9))
    body.append(soft(oa.cylinder("front post", (0, 0.112, 0.082), (0, 0.112, 0.104), 0.004, SIGNAL, segments=8), 10))
    # The grip the magazine goes up through, and a guard in front of it.
    body.append(metal(oa.slab("grip", [(0.022, 0.020), (-0.036, 0.020), (-0.044, -0.098), (0.014, -0.098)], 0.038, DARK, bevel=0.008, segments=3), 11))
    for x in (-0.0195, 0.0195):
        body.append(soft(oa.slab("grip panel", [(0.012, 0.004), (-0.028, 0.004), (-0.034, -0.084), (0.006, -0.084)], 0.006, (0.30, 0.20, 0.14), bevel=0.0025, x=x), 12))
    body.append(metal(oa.box("guard bottom", (0, 0.052, -0.024), (0.014, 0.066, 0.006), DARK, bevel=0.0025), 13))
    body.append(metal(oa.box("guard front", (0, 0.084, -0.003), (0.014, 0.006, 0.044), DARK, bevel=0.0025, rot=(8, 0, 0)), 14))
    body.append(metal(oa.box("trigger", (0, 0.040, 0.000), (0.008, 0.009, 0.028), BARE, bevel=0.003, rot=(18, 0, 0)), 15))
    # A wire stock, folded forward along both sides.
    for x in (-0.0265, 0.0265):
        body.append(metal(oa.cylinder("stock wire", (x, -0.098, 0.030), (x, 0.085, 0.030), 0.0048, BARE, segments=8, bevel=0.001), 16))
    body.append(metal(oa.cylinder("stock hinge", (-0.030, -0.100, 0.030), (0.030, -0.100, 0.030), 0.008, DARK, segments=10, bevel=0.002), 17))
    body.append(metal(oa.box("stock pad", (0, 0.092, 0.030), (0.064, 0.010, 0.030), RUBBER, bevel=0.004), 18))
    body.append(metal(oa.cylinder("sling ring", (-0.024, -0.086, 0.066), (-0.036, -0.086, 0.066), 0.010, BARE, segments=10, bevel=0.002), 19))

    # The magazine: straight, long, with a yellow band and a fat floor plate.
    mag.append(metal(oa.box("magazine", (0, -0.011, -0.170), (0.026, 0.040, 0.150), (0.25, 0.27, 0.33), bevel=0.005), 30))
    mag.append(soft(oa.box("magazine band", (0, -0.011, -0.190), (0.0275, 0.0415, 0.024), YELLOW, bevel=0.003), 31))
    mag.append(metal(oa.box("magazine floor", (0, -0.011, -0.250), (0.032, 0.048, 0.012), DARK, bevel=0.004), 32))
    for x in (-0.0135, 0.0135):
        mag.append(metal(oa.box("magazine rib", (x, -0.011, -0.140), (0.003, 0.010, 0.070), DARK, bevel=0.001), 33))

    sockets = [oa.socket("Muzzle", (0, 0.220, 0.046)), oa.socket("SupportHand", (0, 0.100, 0.006)), oa.socket("Magazine", (0, -0.011, -0.098))]
    return [("Body", body), ("Magazine", mag)], sockets


def shotgun():
    """The Bruiser's gun: two fat barrels side by side, coiled, with a power cell that shines green."""
    body, mag, glow = [], [], []
    # A chunky breech block the barrels hinge from.
    body.append(metal(oa.slab("breech", [(-0.070, 0.006), (0.215, 0.006), (0.215, 0.086), (0.190, 0.098), (-0.040, 0.098), (-0.070, 0.070)], 0.112, GUNMETAL, bevel=0.012, segments=3), 1))
    body.append(metal(oa.box("breech plate", (0, 0.150, 0.100), (0.080, 0.090, 0.008), (0.27, 0.29, 0.35), bevel=0.003), 2))
    body.append(metal(oa.slab("top lever", [(-0.050, 0.098), (0.020, 0.098), (0.020, 0.106), (-0.040, 0.112), (-0.056, 0.108)], 0.016, BARE, bevel=0.004), 3))
    for x in (-0.057, 0.057):
        for y, z in ((-0.030, 0.050), (0.180, 0.030), (0.180, 0.074)):
            body.append(metal(oa.sphere("rivet", (x, y, z), 0.008, BARE, scale=(0.5, 1, 1), segments=8), 4))
    # Two barrels, each with brass bands, cooling coils and a flared mouth.
    # From the side only one of them shows, so each is fat enough to say "this is not a rifle" by itself.
    for x in (-0.034, 0.034):
        body.append(metal(oa.cylinder("barrel", (x, 0.212, 0.052), (x, 0.590, 0.052), 0.0310, DARK, segments=16), 5))
        body.append(metal(oa.lathe("mouth", (x, 0.576, 0.052), (0, 1, 0), [(0.0, 0.0315), (0.008, 0.0390), (0.032, 0.0420), (0.038, 0.0375), (0.038, 0.0250), (0.014, 0.0240)], BRASS, segments=16), 6))
        for i in range(5):
            y = 0.452 + 0.023 * i
            body.append(metal(oa.lathe("coil", (x, y, 0.052), (0, 1, 0), [(-0.007, 0.0305), (-0.005, 0.0375), (0.005, 0.0375), (0.007, 0.0305)], (0.62, 0.34, 0.20), segments=14), 7))
        body.append(metal(oa.lathe("band", (x, 0.262, 0.052), (0, 1, 0), [(-0.009, 0.0305), (-0.007, 0.0350), (0.007, 0.0350), (0.009, 0.0305)], BRASS, segments=16), 8))
        # The light at the bottom of each barrel.
        glow.append(lit(oa.cylinder("emitter", (x, 0.596, 0.052), (x, 0.604, 0.052), 0.0235, CELL, segments=12)))
    # The block the barrels hinge from, wider than the breech behind it.
    body.append(metal(oa.box("hinge block", (0, 0.226, 0.050), (0.136, 0.034, 0.076), GUNMETAL, bevel=0.010, segments=2), 9))
    body.append(metal(oa.box("barrel tie", (0, 0.420, 0.052), (0.030, 0.022, 0.030), GUNMETAL, bevel=0.004), 9))
    body.append(metal(oa.slab("rib", [(0.215, 0.074), (0.570, 0.074), (0.570, 0.082), (0.215, 0.082)], 0.012, GUNMETAL, bevel=0.002), 10))
    body.append(soft(oa.sphere("bead", (0, 0.560, 0.089), 0.008, SIGNAL, segments=10), 11))
    # Wood: a fat fore-end under the barrels and a stock with a rubber pad.
    body.append(wood(oa.slab("fore-end", [(0.248, 0.030), (0.260, -0.016), (0.420, -0.006), (0.440, 0.030)], 0.120, WOOD, bevel=0.014, segments=3), 12))
    body.append(wood(oa.slab("grip", [(0.020, 0.014), (-0.040, 0.014), (-0.076, -0.098), (-0.026, -0.110), (0.006, -0.030)], 0.044, WOOD_DARK, bevel=0.012, segments=3), 13))
    body.append(wood(oa.slab("stock", [(-0.062, 0.078), (-0.062, 0.020), (-0.345, -0.060), (-0.345, 0.062)], 0.052, WOOD, bevel=0.015, segments=3), 14))
    body.append(metal(oa.slab("butt pad", [(-0.341, -0.064), (-0.366, -0.056), (-0.366, 0.058), (-0.341, 0.066)], 0.056, RUBBER, bevel=0.006), 15))
    body.append(soft(oa.box("tape", (0, -0.200, 0.020), (0.057, 0.040, 0.094), TAPE, bevel=0.008, rot=(-10, 0, 3)), 16))
    # Two triggers in one big guard, for a big glove.
    body.append(metal(oa.box("guard bottom", (0, 0.060, -0.034), (0.016, 0.118, 0.007), DARK, bevel=0.003), 17))
    body.append(metal(oa.box("guard front", (0, 0.116, -0.012), (0.016, 0.007, 0.050), DARK, bevel=0.003, rot=(8, 0, 0)), 18))
    body.append(metal(oa.box("trigger", (0, 0.046, -0.004), (0.008, 0.009, 0.030), BARE, bevel=0.003, rot=(18, 0, 0)), 19))
    body.append(metal(oa.box("trigger", (0, 0.074, -0.004), (0.008, 0.009, 0.030), BARE, bevel=0.003, rot=(14, 0, 0)), 20))
    # A window each side of the breech shows the charge.
    for x in (-0.0565, 0.0565):
        glow.append(lit(oa.box("charge window", (x, 0.090, 0.052), (0.004, 0.070, 0.022), CELL, bevel=0.0015)))
        body.append(metal(oa.box("window frame", (x * 0.985, 0.090, 0.052), (0.004, 0.086, 0.036), BRASS, bevel=0.003), 21))

    # The power cell: a fat can that plugs in under the breech, ahead of the guard.
    mag.append(metal(oa.cylinder("cell", (0, 0.168, 0.010), (0, 0.168, -0.070), 0.036, (0.25, 0.27, 0.33), segments=16, bevel=0.006), 30))
    mag.append(soft(oa.lathe("cell band", (0, 0.168, -0.024), (0, 0, -1), [(-0.010, 0.0360), (-0.008, 0.0385), (0.008, 0.0385), (0.010, 0.0360)], (0.30, 0.78, 0.42), segments=16), 31))
    mag.append(metal(oa.cylinder("cell cap", (0, 0.168, -0.066), (0, 0.168, -0.080), 0.030, BRASS, segments=14, bevel=0.004), 32))
    mag.append(metal(oa.box("cell pull", (0, 0.168, -0.086), (0.010, 0.030, 0.010), DARK, bevel=0.003), 33))

    sockets = [oa.socket("Muzzle", (0, 0.612, 0.052)), oa.socket("SupportHand", (0, 0.335, 0.000)), oa.socket("Magazine", (0, 0.168, 0.000))]
    return [("Body", body), ("Magazine", mag), ("CellGlow", glow)], sockets


def sniper():
    """The Marksman's gun: a very long barrel, a green stock with a hole for the thumb, and an enormous scope."""
    body, mag, glow = [], [], []
    body.append(metal(oa.cylinder("action", (0, -0.060, 0.050), (0, 0.250, 0.050), 0.025, GUNMETAL, segments=16, bevel=0.004), 1))
    body.append(metal(oa.box("port", (0.020, 0.090, 0.056), (0.014, 0.080, 0.018), DARK, bevel=0.003), 2))
    # The bolt, with a ball on the end.
    body.append(metal(oa.cylinder("bolt arm", (0.020, -0.020, 0.050), (0.050, -0.030, 0.032), 0.0058, BARE, segments=10), 3))
    body.append(metal(oa.sphere("bolt ball", (0.052, -0.031, 0.030), 0.0125, DARK, segments=12), 4))
    body.append(metal(oa.cylinder("bolt shroud", (0, -0.090, 0.050), (0, -0.058, 0.050), 0.019, DARK, segments=14, bevel=0.004), 5))
    # The barrel: heavy, fluted, ending in a brake with slots.
    body.append(metal(oa.cylinder("barrel", (0, 0.250, 0.050), (0, 0.840, 0.050), 0.0165, DARK, radius_end=0.0135, segments=16), 6))
    for i in range(6):
        a = math.radians(60 * i + 30)
        x, z = math.cos(a) * 0.0152, math.sin(a) * 0.0152
        body.append(metal(oa.box("flute", (x, 0.470, 0.050 + z), (0.0042, 0.300, 0.0042), (0.06, 0.06, 0.07), bevel=0.0012, rot=(0, -math.degrees(a), 0)), 7))
    body.append(metal(oa.lathe("brake", (0, 0.826, 0.050), (0, 1, 0), [(0.0, 0.0140), (0.006, 0.0235), (0.058, 0.0235), (0.072, 0.0190), (0.072, 0.0100), (0.030, 0.0100)], GUNMETAL, segments=16), 8))
    for y in (0.846, 0.866):
        body.append(metal(oa.box("brake slot", (0, y, 0.050), (0.050, 0.009, 0.014), (0.04, 0.04, 0.05), bevel=0.002), 9))
    # The famous green stock: fore-end, a thumb hole, a tall cheek piece and a rubber butt.
    body.append(soft(oa.slab("fore-end", [(0.060, 0.040), (0.070, -0.004), (0.440, 0.008), (0.470, 0.028), (0.470, 0.040)], 0.058, OLIVE, bevel=0.012, segments=3), 10))
    body.append(soft(oa.slab("stock top", [(-0.040, 0.044), (-0.110, 0.050), (-0.360, 0.050), (-0.360, 0.022), (-0.120, 0.018), (-0.050, 0.010)], 0.050, OLIVE, bevel=0.012, segments=3), 11))
    body.append(soft(oa.slab("stock bottom", [(-0.170, 0.000), (-0.360, -0.014), (-0.360, -0.090), (-0.200, -0.062)], 0.047, OLIVE, bevel=0.012, segments=3), 12))
    body.append(soft(oa.slab("stock back", [(-0.300, 0.040), (-0.368, 0.040), (-0.368, -0.080), (-0.300, -0.070)], 0.053, OLIVE, bevel=0.012, segments=3), 13))     # widths differ so no two sides lie in one plane
    body.append(soft(oa.slab("grip", [(0.018, 0.016), (-0.040, 0.016), (-0.078, -0.096), (-0.030, -0.108), (0.004, -0.030)], 0.044, OLIVE_DARK, bevel=0.012, segments=3), 14))
    body.append(soft(oa.slab("grip bridge", [(-0.066, -0.070), (-0.200, -0.040), (-0.200, -0.066), (-0.070, -0.100)], 0.040, OLIVE, bevel=0.010, segments=3), 15))
    body.append(soft(oa.slab("cheek piece", [(-0.130, 0.050), (-0.290, 0.050), (-0.280, 0.084), (-0.150, 0.088)], 0.040, OLIVE_DARK, bevel=0.010, segments=3), 16))
    for y in (-0.170, -0.250):
        body.append(metal(oa.cylinder("cheek post", (0, y, 0.040), (0, y, 0.056), 0.006, BARE, segments=8), 17))
    body.append(metal(oa.slab("butt pad", [(-0.364, -0.084), (-0.388, -0.076), (-0.388, 0.036), (-0.364, 0.044)], 0.054, RUBBER, bevel=0.006), 18))
    # Trigger and guard.
    body.append(metal(oa.box("guard bottom", (0, 0.050, -0.030), (0.014, 0.096, 0.006), DARK, bevel=0.0025), 19))
    body.append(metal(oa.box("guard front", (0, 0.095, -0.010), (0.014, 0.006, 0.046), DARK, bevel=0.0025, rot=(8, 0, 0)), 20))
    body.append(metal(oa.box("trigger", (0, 0.040, -0.002), (0.008, 0.009, 0.030), SIGNAL, bevel=0.003, rot=(18, 0, 0)), 21))
    # The scope: a long tube, a wide bell at the front, two turrets, on two rings.
    sz = 0.122
    scope = [(-0.150, 0.0260), (-0.142, 0.0300), (-0.085, 0.0300), (-0.070, 0.0205), (0.130, 0.0205), (0.170, 0.0390), (0.262, 0.0410), (0.270, 0.0370)]
    body.append(metal(oa.lathe("scope", (0, 0.020, sz), (0, 1, 0), scope, DARK, segments=18), 22))
    body.append(metal(oa.cylinder("eyepiece glass", (0, -0.128, sz), (0, -0.124, sz), 0.0240, GLASS, segments=14), 23))
    body.append(metal(oa.lathe("scope band", (0, 0.200, sz), (0, 1, 0), [(-0.006, 0.0392), (-0.004, 0.0425), (0.004, 0.0425), (0.006, 0.0392)], BRASS, segments=18), 24))
    body.append(metal(oa.cylinder("turret top", (0, 0.060, sz + 0.018), (0, 0.060, sz + 0.044), 0.0135, BARE, segments=12, bevel=0.003), 25))
    body.append(metal(oa.cylinder("turret side", (-0.018, 0.060, sz), (-0.044, 0.060, sz), 0.0135, BARE, segments=12, bevel=0.003), 26))
    for y in (0.005, 0.110):
        body.append(metal(oa.lathe("scope ring", (0, y, sz), (0, 1, 0), [(-0.008, 0.0205), (-0.006, 0.0265), (0.006, 0.0265), (0.008, 0.0205)], GUNMETAL, segments=16), 27))
        body.append(metal(oa.box("ring foot", (0, y, 0.086), (0.022, 0.016, 0.030), GUNMETAL, bevel=0.004), 28))
    body.append(metal(oa.box("rail", (0, 0.058, 0.076), (0.020, 0.170, 0.008), DARK, bevel=0.002), 29))
    # The front glass: this is what glints when it is looking at you.
    glow.append(lit(oa.cylinder("front glass", (0, 0.280, sz), (0, 0.286, sz), 0.0350, LENS, segments=18)))
    # A bipod folded back under the fore-end.
    body.append(metal(oa.box("bipod block", (0, 0.440, 0.000), (0.040, 0.026, 0.020), GUNMETAL, bevel=0.005), 40))
    for x in (-0.020, 0.020):
        body.append(metal(oa.cylinder("bipod leg", (x, 0.436, -0.006), (x * 1.5, 0.226, -0.014), 0.0062, DARK, segments=8), 41))
        body.append(metal(oa.cylinder("bipod foot", (x * 1.5, 0.232, -0.014), (x * 1.5, 0.214, -0.014), 0.0098, RUBBER, segments=8, bevel=0.002), 42))
    body.append(soft(oa.box("tape", (0, 0.330, 0.020), (0.060, 0.034, 0.046), TAPE, bevel=0.007, rot=(4, 0, 0)), 43))

    # The magazine: a short box under the action.
    mag.append(metal(oa.slab("magazine", [(0.118, 0.010), (0.200, 0.010), (0.206, -0.058), (0.126, -0.066)], 0.032, (0.25, 0.27, 0.33), bevel=0.006), 50))
    mag.append(metal(oa.slab("magazine floor", [(0.120, -0.058), (0.212, -0.050), (0.212, -0.062), (0.122, -0.074)], 0.038, DARK, bevel=0.004), 51))
    mag.append(soft(oa.box("magazine band", (0, 0.163, -0.030), (0.0335, 0.082, 0.012), (0.24, 0.50, 0.82), bevel=0.003, rot=(6, 0, 0)), 52))

    sockets = [oa.socket("Muzzle", (0, 0.900, 0.050)), oa.socket("SupportHand", (0, 0.330, 0.000)), oa.socket("Magazine", (0, 0.160, 0.005))]
    return [("Body", body), ("Magazine", mag), ("LensGlow", glow)], sockets


FAR_TRIANGLES = 2000    # docs/performance_budget.md: a gun in an enemy's hands or lying on the ground far off
FAR_SHARE = 0.4         # and never more than this share of the full gun, or a small gun's far copy saves nothing


def far_copy(parts):
    full = sum(oa._triangle_count(o) for _, objects in parts for o in objects)
    return oa.thin(parts, min(FAR_TRIANGLES, int(full * FAR_SHARE)), drop_below=0.012)


def build(name, maker, previews, far_only=False):
    oa.reset_scene()
    parts, sockets = maker()
    if not far_only:
        oa.export(os.path.join(OUT, name + ".bytes"), parts, sockets)
    # The far copy: the same pieces and sockets, thinned. Written with every build so the two never drift apart.
    far = far_copy(parts)
    oa.export(os.path.join(OUT, name + oa.FAR + ".bytes"), far, sockets)
    for piece, objects in far:
        if not piece.endswith("Glow"):                    # the shining pieces are the full model's own
            for o in objects:
                o.hide_render = True
    if previews and not far_only:
        everything = [o for _, objs in parts for o in objs]
        length, mid = SHOTS.get(name, (0.26, (0, 0.05, 0.01)))
        floor = oa.box("floor", (0, 0, -0.5 * length), (40, 40, 0.01), (0.80, 0.74, 0.66), bevel=0)
        oa.paint(floor, wear=0, dirt=0, mottle=0, gradient=0)
        shown = everything + [floor]
        oa.render_preview(os.path.join(previews, name.lower() + "_side.png"), shown, (2.4 * length, mid[1], 0.25 * length), mid, lens=85)
        oa.render_preview(os.path.join(previews, name.lower() + "_front.png"), shown, (1.2 * length, mid[1] + 1.9 * length, 0.8 * length), mid, lens=70)
        oa.render_preview(os.path.join(previews, name.lower() + "_held.png"), shown, (-0.5 * length, mid[1] - 1.5 * length, 0.55 * length), (0, mid[1] + 0.2 * length, 0), lens=45)


# How long each gun is and where its middle is, for framing the pictures.
SHOTS = {"Rifle": (0.75, (0, 0.16, 0.0)), "Pistol": (0.26, (0, 0.05, 0.01)), "Smg": (0.56, (0, 0.05, -0.07)),
         "Shotgun": (1.12, (0, 0.12, 0.0)), "Sniper": (1.45, (0, 0.26, 0.02))}
GUNS = (("Rifle", rifle), ("Pistol", pistol), ("Smg", smg), ("Shotgun", shotgun), ("Sniper", sniper))


def far_line_up(folder):
    """All five guns from the side, the full ones in a row above their far copies, to judge the far copies by eye.
    Seen much closer than the game ever shows a far copy (about 2 m at the game's width of view)."""
    oa.reset_scene()
    shown, y = [], 2.3
    for name, maker in GUNS:
        parts, _ = maker()
        far = far_copy(parts)
        length, mid = SHOTS[name]
        y -= 0.5 * length
        for group, z in ((parts, 0.26), (far, -0.26)):
            for _, objects in group:
                for o in objects:
                    if o in shown:                 # a shining piece is the same object in both
                        continue
                    o.data.transform(Matrix.Translation(Vector((0, y - mid[1], z))))
                    shown.append(o)
        y -= 0.5 * length + 0.18
    oa.render_preview(os.path.join(folder, "guns_far_compare.png"), shown, (7.0, 0.05, 0.0), (0, 0.05, 0.0), lens=50, size=(1280, 800), samples=96)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    previews = args[args.index("--preview") + 1] if "--preview" in args else None
    # --only Smg,Sniper builds just those: the others' files are left exactly as they are.
    only = args[args.index("--only") + 1].split(",") if "--only" in args else None
    if "--far-preview" in args:
        far_line_up(args[args.index("--far-preview") + 1])
        sys.exit(0)
    for gun, maker in GUNS:
        if only is None or gun in only:
            build(gun, maker, previews, far_only="--far-only" in args)
