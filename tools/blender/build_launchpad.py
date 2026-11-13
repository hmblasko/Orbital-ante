"""Builds the launch base: mound, service tower, crew arm, water tower, hut, floodlights and pad lamps.

    blender -b -P tools/blender/build_launchpad.py                       (writes Resources/Models/LaunchPad.bytes)
    blender -b -P tools/blender/build_launchpad.py -- --preview <folder> (also renders pictures)

Pad space, as in Scripts/Runtime/Launch/LaunchLayout.cs: the origin is the middle of the pad's top, the game's
+Y is up, +Z north (the ship's nose), +X east. Here in Blender that is X east, Y north, Z up. Every piece is
modelled in pad space except the Arm, which has its hinge at its own origin and reaches along +X.

The numbers below are a copy of LaunchLayout's. Whatever shines is in its own piece named after its owner with
"Glow" on the end, because the model file holds colour only and the game lights those pieces by name. No flags.
"""
import bpy, bmesh, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import oa_blender as oa
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "unity", "Assets", "OrbitalAnte", "Resources", "Models"))

# ---- a copy of LaunchLayout (x east, y north, z up here) ----
PAD_RADIUS = 15.0
DISH_RADIUS, DISH_DEPTH = 3.4, 0.28
SHIP_HEIGHT = 3.8
TOWER_FOOT = (-10.2, 0.0)
TOWER_HALF, TOWER_HEIGHT = 1.2, 15.0
ARM_LENGTH = 6.2
ARM_PIVOT_Z = SHIP_HEIGHT + 0.2
WATER_FOOT = (9.6, -9.6)
WATER_HEIGHT, WATER_RADIUS = 9.0, 1.9
HUT_CENTRE = (10.2, 7.2)
HUT_HALF = (2.2, 1.7, 1.3)          # east, north, up
MAST_DISTANCE, MAST_HEIGHT = 11.6, 9.5
SKIRT_DEPTH = 12.0

CONCRETE = (0.73, 0.70, 0.65)
CONCRETE_DARK = (0.56, 0.54, 0.50)
SKIRT = (0.64, 0.61, 0.56)
STAIN = (0.43, 0.41, 0.38)
SCORCH = (0.22, 0.20, 0.19)
STEEL = (0.76, 0.29, 0.16)          # the red oxide of a launch tower
STEEL_DARK = (0.49, 0.18, 0.11)
GREY = (0.55, 0.58, 0.60)
YELLOW = (0.91, 0.71, 0.13)
BLACK = (0.12, 0.12, 0.13)
WHITE = (0.92, 0.91, 0.87)
TEAL = (0.22, 0.43, 0.43)
LAMP_WARM = (1.0, 0.94, 0.80)
LAMP_BLUE = (0.29, 0.72, 1.0)
LAMP_AMBER = (1.0, 0.69, 0.23)
LAMP_RED = (1.0, 0.25, 0.16)


def stone(o, seed=0):
    oa.paint(o, wear=0.35, dirt=0.7, mottle=1.0, seed=seed, grain=0.45, gradient=0.03)
    return o


def iron(o, seed=0):
    oa.paint(o, wear=0.5, dirt=0.7, mottle=0.9, seed=seed, grain=1.6, wear_colour=(0.90, 0.52, 0.34), gradient=0.05)
    return o


def painted(o, seed=0):
    oa.paint(o, wear=0.45, dirt=0.6, mottle=0.6, seed=seed, grain=1.8)
    return o


def flat(o):
    oa.paint(o, wear=0, dirt=0, mottle=0, gradient=0)
    return o


def arc_slab(name, r0, r1, top, thick, colour, a0=0.0, a1=360.0, steps=48):
    """A flat ring (or a slice of one) with real thickness, its top at a given height. Angles are compass
    bearings in degrees: 0 is north, 90 east."""
    bm = bmesh.new()
    full = abs(a1 - a0) >= 359.9
    count = steps if full else steps + 1
    rings = []
    for i in range(count):
        a = math.radians(a0 + (a1 - a0) * i / steps)
        s, c = math.sin(a), math.cos(a)
        rings.append([bm.verts.new((s * r0, c * r0, top)), bm.verts.new((s * r1, c * r1, top)),
                      bm.verts.new((s * r1, c * r1, top - thick)), bm.verts.new((s * r0, c * r0, top - thick))])
    pairs = [(rings[i], rings[(i + 1) % count]) for i in range(count if full else count - 1)]
    for a, b in pairs:
        for k in range(4):
            bm.faces.new((a[k], a[(k + 1) % 4], b[(k + 1) % 4], b[k]))
    if not full:
        bm.faces.new(rings[0])
        bm.faces.new(list(reversed(rings[-1])))
    return oa._new_object(name, bm, colour, 30.0)


def beam(name, a, b, size, colour):
    """A square bar from one point to another."""
    a, b = Vector(a), Vector(b)
    o = oa.box(name, (0, 0, 0), (size, size, (b - a).length), colour, bevel=size * 0.12, segments=1)
    m = Matrix.Translation((a + b) * 0.5) @ Vector((0, 0, 1)).rotation_difference((b - a).normalized()).to_matrix().to_4x4()
    o.data.transform(m)
    return o


def place(objects, origin, right, forward, up):
    """Moves pieces built at the origin (x right, y forward, z up) to stand somewhere else, turned."""
    r, f, u = Vector(right).normalized(), Vector(forward).normalized(), Vector(up).normalized()
    m = Matrix(((r.x, f.x, u.x, origin[0]), (r.y, f.y, u.y, origin[1]), (r.z, f.z, u.z, origin[2]), (0, 0, 0, 1)))
    for o in objects:
        o.data.transform(m)
        o.data.update()
    return objects


# ------------------------------------------------------------------ pieces

def mound():
    o = []
    # The body under everything: kerb, then a long slope into the ground.
    foot = PAD_RADIUS + SKIRT_DEPTH * 1.15
    body = oa.lathe("mound body", (0, 0, 0), (0, 0, 1),
                    [(-SKIRT_DEPTH - 0.6, foot + 0.5), (-SKIRT_DEPTH, foot), (-6.0, PAD_RADIUS + 6.6), (-2.2, PAD_RADIUS + 2.3), (-0.5, PAD_RADIUS + 0.5),
                     (-0.06, PAD_RADIUS + 0.06), (-0.03, PAD_RADIUS - 0.2), (-0.03, 0.5)], SKIRT, segments=56)
    oa.densify(body, 2.6)
    oa.crumple(body, 0.05, grain=0.35, seed=4)
    o.append(stone(body, 1))
    # The blast dish: a shallow scorched bowl under the engines.
    bowl = [(-0.5, 0.05), (-0.5, DISH_RADIUS + 0.03), (0.0, DISH_RADIUS)]
    for i in range(7, -1, -1):
        t = i / 8.0
        bowl.append((-DISH_DEPTH * (1 - t * t), max(0.04, DISH_RADIUS * t)))
    dish = oa.lathe("dish", (0, 0, 0), (0, 0, 1), bowl, SCORCH, segments=40)
    oa.paint(dish, wear=0.1, dirt=0.4, mottle=1.4, seed=2, grain=0.9, gradient=0.0)
    o.append(dish)
    # Rings of concrete, each a little different, with keep-clear stripes between.
    for name, r0, r1, colour, seed in (("stained", DISH_RADIUS, 4.6, STAIN, 3), ("apron", 5.0, 12.6, CONCRETE, 4), ("rim", 13.0, PAD_RADIUS, CONCRETE_DARK, 5)):
        ring = arc_slab(name, r0, r1, 0.0, 0.06, colour, steps=56)
        oa.densify(ring, 1.3)
        o.append(stone(ring, seed))
    for r0, r1 in ((4.6, 5.0), (12.6, 13.0)):
        for i in range(24):
            piece = arc_slab("stripe", r0, r1, 0.0, 0.06, YELLOW if i % 2 == 0 else BLACK, i * 15, (i + 1) * 15, steps=3)
            o.append(painted(piece, 6 + i))
    # Cast-in joints: the apron is laid in slabs.
    for i in range(12):
        a = math.radians(i * 30 + 15)
        s, c = math.sin(a), math.cos(a)
        o.append(flat(beam("joint", (s * 5.05, c * 5.05, 0.004), (s * 12.55, c * 12.55, 0.004), 0.05, CONCRETE_DARK)))
    o.append(flat(arc_slab("joint ring", 8.75, 8.80, 0.004, 0.03, CONCRETE_DARK, steps=56)))
    # Scorch streaks fanning out of the dish.
    for k in range(12):
        a = math.radians(k * 30 + (k % 3) * 7)
        length = 2.2 + (k * 7 % 5) * 0.55
        half = 0.24 + (k % 4) * 0.07
        s, c = math.sin(a), math.cos(a)
        streak = oa.slab("scorch", [(DISH_RADIUS - 0.1, -half), (DISH_RADIUS + length * 0.6, -half * 0.75), (DISH_RADIUS + length, 0.0),
                                    (DISH_RADIUS + length * 0.6, half * 0.75), (DISH_RADIUS - 0.1, half)], 0.02, SCORCH, bevel=0)
        # The slab is drawn in (forward, up) and thickened along X: lay it flat, pointing along the bearing.
        place([streak], (0, 0, 0.008), (0, 0, 1), (s, c, 0), (c, -s, 0))
        # That turn is a mirror image, so the faces are turned back to look outward.
        streak.data.flip_normals()
        o.append(flat(streak))
    return o


def tower():
    o, glow = [], []
    fx, fy = TOWER_FOOT
    h = TOWER_HALF - 0.12
    levels = [0.0, 3.0, 6.0, 9.0, 12.0, TOWER_HEIGHT - 0.2]
    o.append(stone(oa.box("tower plinth", (fx, fy, 0.15), (TOWER_HALF * 2, TOWER_HALF * 2, 0.3), CONCRETE_DARK, bevel=0.05), 20))
    for sx in (-1, 1):
        for sy in (-1, 1):
            o.append(iron(beam("post", (fx + sx * h, fy + sy * h, 0.2), (fx + sx * h, fy + sy * h, TOWER_HEIGHT - 0.2), 0.22, STEEL), 21))
    for li, z in enumerate(levels[1:]):
        for sx, sy, ex, ey in ((-1, -1, 1, -1), (1, -1, 1, 1), (1, 1, -1, 1), (-1, 1, -1, -1)):
            o.append(iron(beam("rail", (fx + sx * h, fy + sy * h, z), (fx + ex * h, fy + ey * h, z), 0.16, STEEL), 22 + li))
    # Cross-bracing: one diagonal a bay on each face, leaning the other way on the next bay.
    for li in range(len(levels) - 1):
        z0, z1 = levels[li] + 0.2, levels[li + 1]
        flip = li % 2 == 0
        for sx, sy, ex, ey in ((-1, -1, 1, -1), (1, -1, 1, 1), (1, 1, -1, 1), (-1, 1, -1, -1)):
            a = (fx + sx * h, fy + sy * h, z0 if flip else z1)
            b = (fx + ex * h, fy + ey * h, z1 if flip else z0)
            o.append(iron(beam("brace", a, b, 0.09, STEEL_DARK), 30 + li))
    # Decks at the arm's level and at the top, a lift cage inside, a ladder up the back.
    for z in (ARM_PIVOT_Z - 0.12, TOWER_HEIGHT - 0.2):
        o.append(painted(oa.box("deck", (fx, fy, z), (TOWER_HALF * 2 + 0.25, TOWER_HALF * 2 + 0.25, 0.10), GREY, bevel=0.02), 40))
    o.append(painted(oa.box("lift", (fx, fy, 1.5), (1.3, 1.3, 2.3), YELLOW, bevel=0.06, segments=2), 41))
    o.append(painted(oa.box("lift door", (fx + 0.66, fy, 1.45), (0.03, 0.8, 1.8), BLACK, bevel=0.01), 42))
    for sy in (-0.22, 0.22):
        o.append(iron(beam("ladder", (fx - TOWER_HALF - 0.02, fy + sy, 0.3), (fx - TOWER_HALF - 0.02, fy + sy, TOWER_HEIGHT - 0.3), 0.05, GREY), 43))
    for i in range(36):
        z = 0.5 + i * 0.4
        o.append(flat(beam("rung", (fx - TOWER_HALF - 0.02, fy - 0.22, z), (fx - TOWER_HALF - 0.02, fy + 0.22, z), 0.035, GREY)))
    # Top: a railing, a lightning mast and a red beacon.
    top = TOWER_HEIGHT - 0.15
    for sx, sy, ex, ey in ((-1, -1, 1, -1), (1, -1, 1, 1), (1, 1, -1, 1), (-1, 1, -1, -1)):
        o.append(painted(beam("top rail", (fx + sx * TOWER_HALF, fy + sy * TOWER_HALF, top + 0.55), (fx + ex * TOWER_HALF, fy + ey * TOWER_HALF, top + 0.55), 0.06, YELLOW), 44))
        o.append(painted(beam("top post", (fx + sx * TOWER_HALF, fy + sy * TOWER_HALF, top), (fx + sx * TOWER_HALF, fy + sy * TOWER_HALF, top + 0.55), 0.06, YELLOW), 45))
    # (The mast stops at the tower's stated height, so it stays inside the solid the game collides with.)
    o.append(iron(oa.cylinder("mast", (fx - 0.6, fy - 0.6, top - 1.6), (fx - 0.6, fy - 0.6, TOWER_HEIGHT), 0.05, GREY, radius_end=0.02, segments=8), 46))
    o.append(painted(oa.cylinder("beacon base", (fx + 0.5, fy + 0.5, top - 0.5), (fx + 0.5, fy + 0.5, top - 0.32), 0.14, BLACK, segments=10, bevel=0.02), 47))
    glow.append(flat(oa.sphere("beacon", (fx + 0.5, fy + 0.5, top - 0.20), 0.13, LAMP_RED, segments=10)))
    return o, glow


def arm():
    o = []
    n = ARM_LENGTH
    o.append(painted(oa.box("walkway", (n * 0.5, 0, 0), (n, 1.0, 0.10), GREY, bevel=0.02), 50))
    for sy in (-0.5, 0.5):
        o.append(iron(oa.box("side beam", (n * 0.5, sy, -0.12), (n, 0.10, 0.36), STEEL, bevel=0.025), 51))
        o.append(painted(oa.cylinder("handrail", (0.1, sy, 1.0), (n - 0.1, sy, 1.0), 0.04, YELLOW, segments=8), 52))
        o.append(painted(oa.cylinder("knee rail", (0.1, sy, 0.52), (n - 0.1, sy, 0.52), 0.025, YELLOW, segments=6), 53))
        for k in range(6):
            x = 0.1 + (n - 0.2) * k / 5.0
            o.append(painted(oa.cylinder("stanchion", (x, sy, 0.0), (x, sy, 1.0), 0.03, YELLOW, segments=6), 54))
        # A truss under the walkway so it reads as a bridge, not a plank.
        for k in range(5):
            x0 = 0.2 + (n - 0.4) * k / 5.0
            x1 = 0.2 + (n - 0.4) * (k + 1) / 5.0
            o.append(iron(beam("truss", (x0, sy, -0.30 if k % 2 == 0 else -0.62), (x1, sy, -0.62 if k % 2 == 0 else -0.30), 0.06, STEEL_DARK), 55))
        o.append(iron(beam("truss chord", (0.2, sy, -0.62), (n - 0.2, sy, -0.62), 0.07, STEEL), 56))
    for k in range(11):
        x = 0.35 + (n - 0.7) * k / 10.0
        o.append(flat(oa.box("tread", (x, 0, 0.055), (0.06, 0.9, 0.012), BLACK, bevel=0.0)))
    # The soft collar that sits against the hull.
    o.append(painted(oa.box("collar", (n - 0.12, 0, 0.62), (0.24, 1.24, 1.24), WHITE, bevel=0.10, segments=3), 57))
    o.append(painted(oa.box("collar seal", (n + 0.02, 0, 0.62), (0.06, 1.0, 1.0), BLACK, bevel=0.03, segments=2), 58))
    o.append(iron(oa.cylinder("hinge", (0, 0, -0.5), (0, 0, 1.2), 0.14, STEEL_DARK, segments=12, bevel=0.03), 59))
    return o


def water_tower():
    o = []
    fx, fy = WATER_FOOT
    r = WATER_RADIUS
    tank_bottom = WATER_HEIGHT - 3.6
    tank = oa.lathe("tank", (fx, fy, 0), (0, 0, 1),
                    [(tank_bottom - 0.5, 0.3), (tank_bottom - 0.25, r * 0.72), (tank_bottom + 0.15, r - 0.03), (tank_bottom + 2.6, r - 0.03),
                     (tank_bottom + 2.75, r - 0.12), (WATER_HEIGHT - 0.25, 0.5), (WATER_HEIGHT - 0.1, 0.12)], WHITE, segments=28)
    oa.densify(tank, 0.9)
    o.append(painted(tank, 60))
    for z, colour in ((tank_bottom + 0.45, STEEL), (tank_bottom + 2.45, STEEL)):
        o.append(iron(oa.lathe("hoop", (fx, fy, z), (0, 0, 1), [(-0.07, r - 0.06), (-0.05, r + 0.012), (0.05, r + 0.012), (0.07, r - 0.06)], colour, segments=28), 61))
    o.append(painted(oa.lathe("band", (fx, fy, tank_bottom + 1.2), (0, 0, 1), [(-0.32, r - 0.06), (-0.30, r + 0.006), (0.30, r + 0.006), (0.32, r - 0.06)], TEAL, segments=28), 62))
    o.append(painted(oa.cylinder("vent", (fx, fy, WATER_HEIGHT - 0.3), (fx, fy, WATER_HEIGHT), 0.12, GREY, segments=10, bevel=0.02), 63))
    # Four splayed legs with cross-braces, and the fat pipe that drops the deluge.
    feet = []
    for i in range(4):
        a = math.radians(45 + 90 * i)
        top = (fx + math.sin(a) * (r - 0.35), fy + math.cos(a) * (r - 0.35), tank_bottom + 0.1)
        foot = (fx + math.sin(a) * (r - 0.05), fy + math.cos(a) * (r - 0.05), 0.0)
        feet.append((foot, top))
        o.append(iron(beam("leg", foot, top, 0.20, STEEL), 64))
        o.append(stone(oa.box("footing", (foot[0], foot[1], 0.1), (0.5, 0.5, 0.2), CONCRETE_DARK, bevel=0.03), 65))
    for i in range(4):
        (f0, t0), (f1, t1) = feet[i], feet[(i + 1) % 4]
        mid0 = [(f0[k] + t0[k]) * 0.5 for k in range(3)]
        mid1 = [(f1[k] + t1[k]) * 0.5 for k in range(3)]
        o.append(iron(beam("tie", mid0, mid1, 0.10, STEEL_DARK), 66))
        o.append(iron(beam("cross", f0, mid1, 0.07, STEEL_DARK), 67))
        o.append(iron(beam("cross", mid0, t1, 0.07, STEEL_DARK), 68))
    o.append(painted(oa.cylinder("downpipe", (fx, fy, 0.0), (fx, fy, tank_bottom - 0.3), 0.26, GREY, segments=12), 69))
    o.append(painted(oa.cylinder("valve wheel", (fx - 0.30, fy + 0.30, 1.1), (fx - 0.36, fy + 0.36, 1.1), 0.22, YELLOW, segments=12, bevel=0.02), 70))
    return o


def hut():
    o, glow = [], []
    cx, cy = HUT_CENTRE
    hx, hy, hz = HUT_HALF
    wall_top = 1.75
    o.append(painted(oa.box("hut walls", (cx, cy, wall_top * 0.5), (hx * 2 - 0.1, hy * 2 - 0.1, wall_top), WHITE, bevel=0.06, segments=2), 80))
    o.append(stone(oa.box("hut slab", (cx, cy, 0.06), (hx * 2, hy * 2, 0.12), CONCRETE_DARK, bevel=0.03), 81))
    # A round roof, ridge running east to west, with ribs.
    roof = oa.cylinder("roof", (cx - hx + 0.02, cy, wall_top - 0.72), (cx + hx - 0.02, cy, wall_top - 0.72), hy - 0.02, TEAL, segments=28)
    roof_top = wall_top - 0.72 + hy - 0.02
    squash = (hz * 2 - 0.02 - (wall_top - 0.72)) / (hy - 0.02)
    roof.data.transform(Matrix.Translation((0, 0, wall_top - 0.72)) @ Matrix.Diagonal((1, 1, squash, 1)) @ Matrix.Translation((0, 0, -(wall_top - 0.72))))
    oa.densify(roof, 0.8)
    o.append(painted(roof, 82))
    for k in range(5):
        x = cx - hx + 0.3 + (hx * 2 - 0.6) * k / 4.0
        rib = oa.cylinder("roof rib", (x - 0.04, cy, wall_top - 0.72), (x + 0.04, cy, wall_top - 0.72), hy, STEEL_DARK, segments=28)
        rib.data.transform(Matrix.Translation((0, 0, wall_top - 0.72)) @ Matrix.Diagonal((1, 1, squash * 0.995, 1)) @ Matrix.Translation((0, 0, -(wall_top - 0.72))))
        o.append(iron(rib, 83))
    # The door and window face the pad (west).
    face = cx - hx + 0.04
    o.append(painted(oa.box("door", (face, cy - 0.7, 0.98), (0.06, 0.86, 1.72), (0.87, 0.42, 0.14), bevel=0.02), 84))
    o.append(painted(oa.box("door frame", (face + 0.01, cy - 0.7, 1.0), (0.04, 1.0, 1.86), STEEL_DARK, bevel=0.02), 85))
    o.append(iron(oa.sphere("door knob", (face - 0.05, cy - 0.40, 0.95), 0.045, YELLOW, segments=8), 86))
    o.append(painted(oa.box("window frame", (face + 0.01, cy + 0.75, 1.25), (0.05, 1.05, 0.72), STEEL_DARK, bevel=0.02), 87))
    glow.append(flat(oa.box("window", (face - 0.012, cy + 0.75, 1.25), (0.02, 0.90, 0.58), LAMP_WARM, bevel=0.0)))
    o.append(flat(beam("window bar", (face - 0.026, cy + 0.75, 0.96), (face - 0.026, cy + 0.75, 1.54), 0.03, STEEL_DARK)))
    o.append(stone(oa.box("step", (face - 0.28, cy - 0.7, 0.07), (0.5, 1.0, 0.14), CONCRETE, bevel=0.02), 88))
    o.append(painted(oa.cylinder("stove pipe", (cx + 1.2, cy - 0.5, wall_top), (cx + 1.2, cy - 0.5, hz * 2 - 0.02), 0.09, GREY, segments=10), 89))
    # A lamp over the door.
    o.append(painted(oa.box("door lamp", (face - 0.07, cy - 0.7, 2.02), (0.12, 0.22, 0.08), BLACK, bevel=0.02), 90))
    glow.append(flat(oa.box("door lamp glass", (face - 0.07, cy - 0.7, 1.965), (0.09, 0.16, 0.03), LAMP_WARM, bevel=0.0)))
    return o, glow


def pad_lamps():
    o, glow = [], []
    for i in range(24):
        a = (i + 0.5) * 2 * math.pi / 24
        x, y = math.sin(a) * 14.2, math.cos(a) * 14.2
        o.append(flat(oa.cylinder("lamp housing", (x, y, 0.0), (x, y, 0.10), 0.13, BLACK, radius_end=0.11, segments=10, bevel=0.015)))
        lens = oa.sphere("lamp", (x, y, 0.10), 0.085, LAMP_BLUE if i % 2 == 0 else LAMP_AMBER, scale=(1, 1, 0.7), segments=8)
        glow.append(flat(lens))
    # Four yellow chocks by the footpads (footpads are 4.3 m out, 0.6 across).
    for i in range(4):
        a = math.radians(45 + 90 * i)
        s, c = math.sin(a), math.cos(a)
        chock = oa.slab("chock", [(-0.18, 0.0), (0.18, 0.0), (0.10, 0.24), (-0.10, 0.24)], 0.8, YELLOW, bevel=0.03, segments=2)
        place([chock], (s * 5.2, c * 5.2, 0.0), (c, -s, 0), (s, c, 0), (0, 0, 1))
        o.append(painted(chock, 95 + i))
    return o, glow


def floodlight(i):
    o, glow = [], []
    a = math.radians(45 + 90 * i)
    foot = Vector((math.sin(a) * MAST_DISTANCE * 1.2, math.cos(a) * MAST_DISTANCE * 1.2, 0.0))
    top = foot + Vector((0, 0, MAST_HEIGHT))
    o.append(stone(oa.box("mast base", foot + Vector((0, 0, 0.12)), (0.7, 0.7, 0.24), CONCRETE_DARK, bevel=0.04), 100 + i))
    o.append(painted(oa.cylinder("mast", foot + Vector((0, 0, 0.2)), top, 0.12, GREY, radius_end=0.07, segments=10), 104 + i))
    o.append(painted(oa.lathe("mast collar", foot + Vector((0, 0, 0.24)), (0, 0, 1), [(0.0, 0.10), (0.02, 0.19), (0.16, 0.17), (0.22, 0.10)], YELLOW, segments=10), 108 + i))
    # The head: a bank of three lamps on a cross-bar, tipped down toward the middle of the pad.
    aim = (Vector((0, 0, 1.5)) - top).normalized()
    right = aim.cross(Vector((0, 0, 1))).normalized()
    up = right.cross(aim).normalized()
    head, lit = [], []
    head.append(painted(oa.box("cross-bar", (0, 0, 0), (1.9, 0.16, 0.16), BLACK, bevel=0.03), 112))
    for k in (-1, 0, 1):
        head.append(painted(oa.box("lamp box", (k * 0.62, 0.10, 0.02), (0.52, 0.30, 0.40), BLACK, bevel=0.05, segments=2), 113))
        head.append(painted(oa.box("lamp hood", (k * 0.62, 0.20, 0.235), (0.56, 0.34, 0.03), GREY, bevel=0.01), 114))
        lit.append(flat(oa.box("lamp face", (k * 0.62, 0.252, 0.02), (0.42, 0.012, 0.30), LAMP_WARM, bevel=0.0)))
    place(head + lit, top, right, aim, up)
    return o + head, glow + lit


def make():
    parts = []
    parts.append(("Mound", mound()))
    t, tg = tower()
    parts += [("Tower", t), ("TowerGlow", tg)]
    parts.append(("WaterTower", water_tower()))
    h, hg = hut()
    parts += [("Hut", h), ("HutGlow", hg)]
    p, pg = pad_lamps()
    parts += [("PadLamps", p), ("PadLampsGlow", pg)]
    for i in range(4):
        f, fg = floodlight(i)
        parts += [("Floodlight%d" % i, f), ("Floodlight%dGlow" % i, fg)]
    parts.append(("Arm", arm()))
    return parts


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    previews = args[args.index("--preview") + 1] if "--preview" in args else None
    oa.reset_scene()
    parts = make()
    sockets = [oa.socket("TowerFoot", (TOWER_FOOT[0], TOWER_FOOT[1], 0)), oa.socket("WaterTowerFoot", (WATER_FOOT[0], WATER_FOOT[1], 0)),
               oa.socket("HutCentre", (HUT_CENTRE[0], HUT_CENTRE[1], 0)), oa.socket("ArmPivot", (-9.0, 0, ARM_PIVOT_Z))]
    for i in range(4):
        a = math.radians(45 + 90 * i)
        sockets.append(oa.socket("Mast%d" % i, (math.sin(a) * MAST_DISTANCE * 1.2, math.cos(a) * MAST_DISTANCE * 1.2, 0)))
    oa.export(os.path.join(OUT, "LaunchPad.bytes"), parts, sockets)
    if previews:
        # Swing the arm to its place by the tower for the pictures.
        for name, objects in parts:
            if name == "Arm":
                for o in objects:
                    o.data.transform(Matrix.Translation((-9.0, 0, ARM_PIVOT_Z)))
        shown = [o for _, objects in parts for o in objects]
        ground = oa.box("ground", (0, 0, -4.0), (400, 400, 0.02), (0.62, 0.50, 0.38), bevel=0)
        oa.paint(ground, wear=0, dirt=0, mottle=0, gradient=0)
        shown.append(ground)
        oa.render_preview(os.path.join(previews, "pad_wide.png"), shown, (34, -40, 17), (0, 0, 3.5), lens=38, size=(1400, 860), samples=64)
        oa.render_preview(os.path.join(previews, "pad_tower.png"), shown, (4, -16, 5.0), (-7, 0, 6.5), lens=30, size=(1100, 860), samples=64)
        oa.render_preview(os.path.join(previews, "pad_hut.png"), shown, (-1, 3, 2.4), (10, 0, 3.0), lens=28, size=(1100, 860), samples=64)
