"""Builds the game's guns: original, chunky, hand-painted models.

    blender -b -P tools/blender/build_weapons.py            (writes the game's model files)
    blender -b -P tools/blender/build_weapons.py -- --preview <folder>   (also renders pictures)

A gun's space here: origin where the firing hand holds it, Y out of the barrel, Z up, metres.
The four sockets keep the positions the placeholder guns had, so hands, tracers and reloads still line up.
"""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import oa_blender as oa

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
    body.append(wood(oa.slab("handguard", [(0.272, 0.004), (0.44, 0.012), (0.452, 0.03), (0.452, 0.058), (0.272, 0.062)], 0.064, WOOD, bevel=0.012, segments=3), 20))
    body.append(wood(oa.cylinder("upper handguard", (0, 0.278, 0.079), (0, 0.44, 0.079), 0.0215, WOOD, segments=16, bevel=0.006), 21))
    for y in (0.283, 0.435):
        body.append(metal(oa.box("handguard band", (0, y, 0.05), (0.068, 0.012, 0.098), GUNMETAL, bevel=0.005), 22))
    body.append(wood(oa.slab("grip", [(0.020, 0.014), (-0.034, 0.014), (-0.066, -0.100), (-0.020, -0.112), (0.006, -0.030)], 0.040, WOOD_DARK, bevel=0.011, segments=3), 23))
    body.append(wood(oa.slab("stock", [(-0.078, 0.072), (-0.078, 0.018), (-0.335, -0.050), (-0.352, -0.044), (-0.352, 0.056), (-0.335, 0.064)], 0.046, WOOD, bevel=0.013, segments=3), 24))
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


def build(name, maker, previews):
    oa.reset_scene()
    parts, sockets = maker()
    oa.export(os.path.join(OUT, name + ".bytes"), parts, sockets)
    if previews:
        everything = [o for _, objs in parts for o in objs]
        length = 0.75 if name == "Rifle" else 0.26
        mid = (0, 0.16, 0.0) if name == "Rifle" else (0, 0.05, 0.01)
        floor = oa.box("floor", (0, 0, -0.5 * length), (40, 40, 0.01), (0.80, 0.74, 0.66), bevel=0)
        oa.paint(floor, wear=0, dirt=0, mottle=0, gradient=0)
        shown = everything + [floor]
        oa.render_preview(os.path.join(previews, name.lower() + "_side.png"), shown, (2.4 * length, mid[1], 0.25 * length), mid, lens=85)
        oa.render_preview(os.path.join(previews, name.lower() + "_front.png"), shown, (1.2 * length, mid[1] + 1.9 * length, 0.8 * length), mid, lens=70)
        oa.render_preview(os.path.join(previews, name.lower() + "_held.png"), shown, (-0.5 * length, mid[1] - 1.5 * length, 0.55 * length), (0, mid[1] + 0.2 * length, 0), lens=45)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    previews = args[args.index("--preview") + 1] if "--preview" in args else None
    build("Rifle", rifle, previews)
    build("Pistol", pistol, previews)
