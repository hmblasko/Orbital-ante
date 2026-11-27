"""Builds the small things that used to be plain boxes and cylinders: the ante tasks' objects, the cave work lamp
and the five melee weapons.

    blender -b -P tools/blender/build_props.py
    blender -b -P tools/blender/build_props.py -- --preview <folder>
    blender -b -P tools/blender/build_props.py -- --write-to <folder>      (a trial: changes nothing in the game)

Matthew, 26 Nov 2026: the game should look realistic and not made by a machine, "including what the player sees".
These were the last things in the open still drawn as Unity's own cube, ball and cylinder. Each starts from a
named real thing (original designs, no markings, names or flags):

  Props.bytes
    mastBase      a poured footing with anchor bolts and a relay cabinet (a rural radio relay site)
    mast          a flanged steel monopole with step bolts, a cable run, two panel aerials and a microwave drum
                  dish near the top. Stands on the origin; the game tips the whole piece over when it falls.
    cell          one power cell clamped to the pole: a finned battery canister in a cradle. Built round its own
                  middle, its face toward +forward; the game stands one per cell round the pole.
    cellGlow      the cell's lit window (what the player shoots at)
    cellBurnt     the same canister burst and scorched, shown once the cell is shot out
    crate         a ribbed transit case with corner bumpers, butterfly latches and two handles (0.8 x 0.55, 0.6 tall)
    crateGlow     its status strip
    pod           a supply drop capsule: blunt heat shield, panelled barrel, four crush legs, riser lugs on top
    podGlow       the ring of marker lamps and the beacon on its stalk
    lamp          a tripod site work light with a cable to a small generator (the cave lamp)
    lampGlow      its lens
  Melee.bytes (each held at the origin, pointing up; the game's own Tip heights are in MeleeItem)
    LaserSword, LaserSwordGlow, BaseballBat, Mailbox, ButterflyKnife, FryingPan

Model space (Blender): X right, Y forward, Z up. The game turns that into X right, Y up, Z forward. The sizes the
game's colliders use are copied below and checked by the rule test PropsModelFile_FitsWhatTheGameExpects.
"""
import bpy, bmesh, sys, os, math
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import oa_blender as oa
import oa_real as real

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "unity", "Assets", "OrbitalAnte", "Resources", "Models"))

# ---- what the game's colliders and rules expect (TaskDirector, MeleeItem)
PAD = (2.4, 2.4, 0.6)                # the footing: across, deep, tall
POLE_RADIUS, POLE_TOP = 0.225, 9.0
DISH_AT, DISH_RADIUS, DISH_TILT = (0.0, 0.3, 9.3), 0.9, 35.0     # x, forward, up
CELL = (0.6, 0.6, 0.9)               # across, deep, tall, round the cell's own middle
CRATE = (0.8, 0.55, 0.6)
POD_RADIUS, POD_TALL, POD_BEACON = 0.5, 1.4, 1.5
# Triangles each drawn piece may have (the rule test PropsModelTests holds the file to a little over these).
BUDGET = {"mastBase": 1400, "mast": 2400, "cell": 700, "cellBurnt": 800, "crate": 2000, "pod": 2200, "lamp": 2200}
TIPS = {"LaserSword": 1.21, "BaseballBat": 0.86, "Mailbox": 1.15, "ButterflyKnife": 0.23, "FryingPan": 0.50}

# Colours as seen on screen: galvanised steel gone dull, weathered concrete, site-equipment grey and a worn
# safety yellow, olive transit plastic. Nothing bright but the lamps.
GALV, GALV_DARK = (0.50, 0.52, 0.53), (0.36, 0.38, 0.40)
CONCRETE = (0.52, 0.51, 0.48)
CABINET, CABINET_DARK = (0.58, 0.60, 0.58), (0.40, 0.42, 0.42)
AERIAL = (0.78, 0.78, 0.75)
RUBBER, CHARCOAL = (0.08, 0.08, 0.09), (0.15, 0.15, 0.16)
YELLOW, YELLOW_DARK = (0.66, 0.52, 0.16), (0.48, 0.37, 0.12)
OLIVE, OLIVE_DARK = (0.36, 0.38, 0.27), (0.25, 0.27, 0.20)
CASE_METAL = (0.55, 0.56, 0.57)
SHELL, SHELL_SHADE = (0.80, 0.80, 0.77), (0.62, 0.63, 0.62)
CHAR = (0.20, 0.17, 0.15)
SOOT = (0.07, 0.065, 0.06)
ORANGE = (0.80, 0.40, 0.13)
WEBBING = (0.27, 0.26, 0.22)
CELL_LIT, HAUL_LIT, LENS = (1.0, 0.36, 0.16), (0.40, 0.85, 1.0), (1.0, 0.86, 0.62)
ASH, ASH_DARK = (0.76, 0.61, 0.42), (0.58, 0.44, 0.28)
TAPE = (0.13, 0.13, 0.14)
STEEL, STEEL_DARK = (0.62, 0.63, 0.66), (0.40, 0.41, 0.44)
IRON = (0.11, 0.11, 0.12)
POST = (0.34, 0.28, 0.21)
TIN = (0.38, 0.44, 0.50)
FLAG = (0.72, 0.30, 0.16)
BLADE = (0.45, 0.85, 1.0)
BRASS = (0.66, 0.54, 0.28)
DUST = (0.47, 0.45, 0.42)


def metal(o, seed=0, wear=0.5, smooth=0.5, bare=0.8, dirt=0.6):
    oa.paint(o, wear=wear, dirt=dirt, mottle=0.55, seed=seed, grain=2.6)
    real.made_of(o, smooth, bare)
    return o


def painted(o, seed=0, smooth=0.28, wear=0.6, longest=0.22, wear_colour=(0.50, 0.50, 0.49)):
    oa.densify(o, longest)
    oa.paint(o, wear=wear, dirt=0.75, mottle=0.6, seed=seed, wear_colour=wear_colour, grain=3.0)
    real.made_of(o, smooth, 0.0)
    return o


def matte(o, seed=0, smooth=0.08, longest=None):
    if longest:
        oa.densify(o, longest)
    oa.paint(o, wear=0.3, dirt=0.65, mottle=0.7, seed=seed, grain=3.4)
    real.made_of(o, smooth, 0.0)
    return o


def lit(o):
    oa.paint(o, wear=0, dirt=0, mottle=0, gradient=0)
    return o


def lathe(name, origin, axis, profile, colour, segments=20):
    return oa.lathe(name, origin, axis, [(a, max(r, 0.003)) for a, r in profile], colour, segments)


def tube(name, points, radius, colour, seed=0, cuts=2, n=8, smooth=0.45, bare=0.8, up=(0, 0, 1)):
    """A bent bar. `up` must not lie along any stretch of the path (give the way out of the plane it bends in),
    or the bar twists where it turns."""
    return metal(real.sweep(name, points, radius, colour, n=n, cuts=cuts, up=up), seed, smooth=smooth, bare=bare)


def hose(name, points, radius=0.014, seed=0, cuts=3):
    # An oblique "up" that no stretch of a cable runs along, so it never twists.
    return matte(real.sweep(name, points, radius, RUBBER, n=6, cuts=cuts, up=(0.53, 0.76, 0.37)), seed)


def bolt(at, radius=0.022, tall=0.03, axis=(0, 0, 1), seed=0):
    a = Vector(at)
    return metal(oa.cylinder("bolt", a, a + Vector(axis).normalized() * tall, radius, GALV_DARK, segments=6), seed, wear=0.7, smooth=0.4)


def finish(parts, top, dust=0.5, on_top=0.22, shadow=0.5, reach=0.18, seed=0):
    real.crowd_shadow(parts, strength=shadow, reach=reach)
    real.dust(parts, DUST, top, amount=dust, on_top=on_top, seed=seed)
    return parts


# ------------------------------------------------------------------ the signal mast

def mast_base():
    out = []
    w, d, h = PAD
    # The footing was poured in a board form: slightly proud edges, a chamfer, weathered and stained.
    pad = oa.box("footing", (0, 0, h / 2), (w, d, h), CONCRETE, bevel=0.035, segments=2)
    oa.densify(pad, 0.3)
    oa.crumple(pad, 0.008, grain=2.2, seed=3)
    oa.paint(pad, wear=0.35, dirt=1.0, mottle=1.1, seed=4, wear_colour=(0.62, 0.61, 0.58), grain=1.6, gradient=0.16)
    real.made_of(pad, 0.04, 0.0)
    out.append(pad)
    # The pole's base flange, bolted down to eight anchor studs with a grout bed under it.
    out.append(matte(lathe("grout", (0, 0, h - 0.005), (0, 0, 1), [(0, 0.50), (0.035, 0.47), (0.035, 0.0)], (0.44, 0.43, 0.41), 24), 5))
    out.append(metal(lathe("flange", (0, 0, h + 0.03), (0, 0, 1), [(0, 0.44), (0.045, 0.44), (0.045, 0.0)], GALV, 24), 6, wear=0.6))
    for i in range(8):
        a = i * math.tau / 8 + 0.2
        p = (math.cos(a) * 0.36, math.sin(a) * 0.36, h + 0.07)
        out.append(bolt(p, 0.032, 0.035, seed=10 + i))
        out.append(metal(oa.cylinder("stud", p, (p[0], p[1], p[2] + 0.09), 0.014, GALV_DARK, segments=6), 20 + i, smooth=0.3))
    for i in range(4):
        a = i * math.tau / 4 + 0.2 + math.tau / 16
        gus = oa.slab("gusset", [(0.0, 0.0), (0.2, 0.0), (0.0, 0.34)], 0.018, GALV, bevel=0.003, segments=1)
        gus.data.transform(Matrix.Translation((0, POLE_RADIUS - 0.01, h + 0.07)))
        oa.turn(gus, -math.degrees(a) + 90)
        out.append(metal(gus, 30 + i))
    # The relay cabinet on one corner: a louvred outdoor case on a plinth, with a conduit to the pole.
    cx, cy = -0.72, -0.70
    out.append(matte(oa.box("plinth", (cx, cy, h + 0.04), (0.74, 0.50, 0.08), (0.45, 0.44, 0.42), bevel=0.012), 40))
    cab = oa.box("cabinet", (cx, cy, h + 0.08 + 0.50), (0.66, 0.42, 1.0), CABINET, bevel=0.018, segments=2)
    out.append(painted(cab, 41, smooth=0.3))
    out.append(painted(oa.box("cabinet roof", (cx, cy - 0.015, h + 1.10), (0.72, 0.50, 0.035), CABINET_DARK, bevel=0.01, rot=(4, 0, 0)), 42))
    out.append(painted(oa.box("door", (cx, cy - 0.213, h + 0.56), (0.58, 0.012, 0.88), CABINET, bevel=0.006), 43, smooth=0.32))
    for k in range(5):
        out.append(painted(oa.box("louvre", (cx, cy - 0.224, h + 0.24 + k * 0.045), (0.40, 0.016, 0.022), CABINET_DARK, bevel=0.004, rot=(-28, 0, 0)), 44 + k, longest=0.5))
    out.append(metal(oa.box("handle", (cx + 0.22, cy - 0.232, h + 0.62), (0.022, 0.03, 0.15), CHARCOAL, bevel=0.008), 50, smooth=0.3, bare=0.3))
    for z in (0.22, 0.90):
        out.append(metal(oa.cylinder("hinge", (cx - 0.295, cy - 0.218, h + z - 0.05), (cx - 0.295, cy - 0.218, h + z + 0.05), 0.012, GALV_DARK, segments=8), 51))
    out.append(tube("conduit", [(cx + 0.33, cy + 0.05, h + 0.30), (cx + 0.50, cy + 0.10, h + 0.26), (-0.10, -0.42, h + 0.12), (-0.05, -0.24, h + 0.16)], 0.028, GALV_DARK, 52, cuts=2))
    # An earthing strap off the flange and a short kerb of spilled grout: small things a real site has.
    out.append(metal(real.sweep("earth strap", [(0.30, 0.30, h + 0.075), (0.62, 0.66, h + 0.012), (1.05, 1.02, h + 0.012)], 0.02, (0.50, 0.33, 0.20), n=6, flat=0.2, cuts=2), 53, smooth=0.3, bare=0.6))
    return finish(out, 1.6, dust=0.55, seed=1)


def mast():
    out = []
    base = PAD[2] + 0.075
    # The pole: three flanged sections, each a little slimmer, as real monopoles are shipped and bolted up.
    joints = [base, 3.6, 6.4, POLE_TOP]
    radii = [POLE_RADIUS, 0.200, 0.175, 0.150]
    for k in range(3):
        z0, z1 = joints[k], joints[k + 1]
        r0, r1 = radii[k], radii[k + 1]
        sec = lathe("pole", (0, 0, z0), (0, 0, 1), [(0, r0), (z1 - z0, r1 + 0.012), (z1 - z0, 0.0)], GALV, 14)
        oa.densify(sec, 0.7)
        out.append(metal(sec, 60 + k, wear=0.55, smooth=0.42, dirt=0.8))
        if k < 2:
            out.append(metal(lathe("joint", (0, 0, z1 - 0.03), (0, 0, 1), [(0, r1 + 0.012), (0, r1 + 0.075), (0.06, r1 + 0.075), (0.06, r1)], GALV_DARK, 14), 63 + k, wear=0.7))
            for i in range(8):
                a = i * math.tau / 8
                out.append(bolt((math.cos(a) * (r1 + 0.048), math.sin(a) * (r1 + 0.048), z1 + 0.03), 0.016, 0.02, seed=70 + i))
    out.append(metal(lathe("cap", (0, 0, POLE_TOP), (0, 0, 1), [(0, 0.165), (0.03, 0.15), (0.07, 0.05), (0.07, 0.0)], GALV_DARK, 14), 66))
    # Step bolts up the back, staggered left and right, from above arm's reach to the top.
    z, side = 2.9, 1
    while z < POLE_TOP - 0.5:
        r = POLE_RADIUS - (POLE_RADIUS - 0.15) * (z - base) / (POLE_TOP - base)
        a = math.radians(-90 + side * 28)
        d = Vector((math.cos(a), math.sin(a), 0))
        out.append(metal(oa.cylinder("step", d * (r - 0.01) + Vector((0, 0, z)), d * (r + 0.17) + Vector((0, 0, z)), 0.011, GALV_DARK, segments=6), int(z * 10), smooth=0.3))
        z += 0.38
        side = -side
    # The feeder cables: two fat ones clipped up the side of the pole to the aerials.
    for i, off in enumerate((-0.035, 0.035)):
        pts = []
        for z in (base + 0.1, 2.0, 4.0, 6.0, 7.6, 8.5):
            r = POLE_RADIUS - (POLE_RADIUS - 0.15) * (z - base) / (POLE_TOP - base) + 0.03
            pts.append((r * math.cos(math.radians(200)) + off * 0.4, r * math.sin(math.radians(200)) + off, z))
        out.append(hose("feeder", pts, 0.02, 80 + i, cuts=1))
    for z in (1.2, 2.6, 4.0, 5.4, 6.8, 8.1):
        r = POLE_RADIUS - (POLE_RADIUS - 0.15) * (z - base) / (POLE_TOP - base)
        out.append(metal(lathe("clip", (0, 0, z - 0.02), (0, 0, 1), [(0, r + 0.004), (0, r + 0.062), (0.04, r + 0.062), (0.04, r + 0.004)], GALV_DARK, 14), int(z * 7), wear=0.6))
    # Two panel aerials on stand-off arms, and the microwave drum dish where the game's dish is.
    for i, (ang, z) in enumerate(((-55, 7.75), (125, 8.25))):
        a = math.radians(ang)
        d = Vector((math.cos(a), math.sin(a), 0))
        for dz in (-0.45, 0.45):
            out.append(tube("arm", [d * 0.14 + Vector((0, 0, z + dz)), d * 0.44 + Vector((0, 0, z + dz))], 0.02, GALV_DARK, 90 + i, cuts=0, n=6))
        panel = oa.box("aerial", (0, 0, 0), (0.26, 0.10, 1.25), AERIAL, bevel=0.03, segments=2)
        panel.data.transform(Matrix.Translation(d * 0.50 + Vector((0, 0, z))) @ Matrix.Rotation(a - math.pi / 2, 4, 'Z') @ Matrix.Rotation(math.radians(-4), 4, 'X'))
        out.append(painted(panel, 92 + i, smooth=0.25, longest=0.3, wear=0.4))
    dx, dy, dz = DISH_AT
    tilt = Matrix.Translation((dx, dy, dz)) @ Matrix.Rotation(math.radians(DISH_TILT), 4, 'X')
    drum = lathe("dish", (0, 0, 0), (0, 0, 1), [(-0.30, 0.0), (-0.30, 0.16), (-0.24, 0.42), (-0.10, 0.74), (0.0, DISH_RADIUS), (0.045, DISH_RADIUS), (0.07, DISH_RADIUS - 0.03), (0.04, 0.55), (0.0, 0.0)], AERIAL, 28)
    drum.data.transform(tilt)
    oa.densify(drum, 0.4)
    out.append(painted(drum, 95, smooth=0.3, wear=0.45, longest=0.4))
    rim = lathe("dish rim", (0, 0, 0.04), (0, 0, 1), [(0, DISH_RADIUS - 0.02), (0, DISH_RADIUS + 0.012), (0.035, DISH_RADIUS + 0.012), (0.035, DISH_RADIUS - 0.02)], GALV_DARK, 28)
    rim.data.transform(tilt)
    out.append(metal(rim, 96))
    feed = oa.cylinder("feed", (0, 0, 0.0), (0, 0, 0.30), 0.03, GALV_DARK, segments=8)
    feed.data.transform(tilt)
    out.append(metal(feed, 97))
    horn = lathe("horn", (0, 0, 0.26), (0, 0, 1), [(0, 0.03), (0.02, 0.085), (0.08, 0.085), (0.10, 0.03), (0.10, 0.0)], CHARCOAL, 12)
    horn.data.transform(tilt)
    out.append(matte(horn, 98, smooth=0.2))
    # The yoke that holds the dish off the pole top.
    out.append(tube("yoke", [(0, 0.02, POLE_TOP - 0.25), (0, 0.10, POLE_TOP + 0.02), tuple(tilt @ Vector((0, 0, -0.30)))], 0.05, GALV_DARK, 99, cuts=2, up=(1, 0, 0)))
    out.append(tube("yoke brace", [(0, 0.12, POLE_TOP - 0.7), tuple(tilt @ Vector((0, -0.5, -0.12)))], 0.022, GALV_DARK, 100, cuts=0, n=6, up=(1, 0, 0)))
    # A lightning spike beside the dish.
    out.append(metal(oa.cylinder("spike", (0.10, -0.08, POLE_TOP - 0.2), (0.12, -0.10, POLE_TOP + 1.15), 0.012, GALV_DARK, radius_end=0.004, segments=6), 101))
    real.crowd_shadow(out, strength=0.45, reach=0.2)
    # Rust weeping down from each joint: the paint's own dirt does the rest.
    return out


def cell(burnt=False):
    """One power cell: built round its own middle, its lit face toward +Y, the pole behind it at -Y."""
    out, glow = [], []
    w, d, h = CELL
    body = CHAR if burnt else YELLOW
    # The cradle: a back plate and two band clamps that go round the pole.
    out.append(metal(oa.box("back plate", (0, -0.20, 0), (0.40, 0.03, 0.80), GALV_DARK, bevel=0.006), 110, wear=0.6))
    for z in (-0.26, 0.26):
        out.append(metal(oa.box("clamp", (0, -0.25, z), (0.50, 0.08, 0.05), GALV_DARK, bevel=0.01), 111))
        for s in (-1, 1):
            out.append(bolt((s * 0.21, -0.215, z), 0.016, 0.03, axis=(0, 1, 0), seed=112))
    # The canister: a finned drum on its end, domed caps, a carrying bail on top.
    can = lathe("canister", (0, 0.02, -0.38), (0, 0, 1), [(0, 0.0), (0, 0.17), (0.03, 0.215), (0.10, 0.225), (0.66, 0.225), (0.73, 0.215), (0.76, 0.17), (0.76, 0.0)], body, 18)
    oa.densify(can, 0.3)
    if burnt:
        oa.crumple(can, 0.028, grain=5.0, seed=9)
        oa.paint(can, wear=0.2, dirt=1.4, mottle=1.4, seed=7, wear_colour=(0.30, 0.22, 0.16), grain=4.0)
        real.made_of(can, 0.05, 0.0)
    else:
        oa.paint(can, wear=0.75, dirt=0.8, mottle=0.6, seed=7, wear_colour=(0.42, 0.42, 0.40), grain=3.2)
        real.made_of(can, 0.3, 0.0)
    out.append(can)
    for k in range(5 if not burnt else 6):
        z = -0.24 + k * 0.095
        fin = lathe("fin", (0, 0.02, z), (0, 0, 1), [(0, 0.22), (0.006, 0.262), (0.018, 0.262), (0.024, 0.22)], SOOT if burnt else CHARCOAL, 18)
        if burnt:
            oa.crumple(fin, 0.012, grain=6.0, seed=k)
        out.append(metal(fin, 113 + k, wear=0.6, smooth=0.1 if burnt else 0.35, bare=0.0 if burnt else 0.5))
    # The window: a recessed bezel on the front with the lit cell showing through.
    out.append(metal(oa.box("bezel", (0, 0.245, 0.02), (0.31, 0.05, 0.58), SOOT if burnt else CHARCOAL, bevel=0.012), 120, smooth=0.1 if burnt else 0.3, bare=0.2))
    if burnt:
        hole = oa.box("burst", (0, 0.262, 0.02), (0.24, 0.03, 0.50), (0.02, 0.02, 0.02), bevel=0.01)
        oa.crumple(hole, 0.012, grain=9.0, seed=4)
        out.append(matte(hole, 121, smooth=0.02))
        # Petals of torn skin round the hole.
        for i, (x, z, rx, rz) in enumerate(((-0.10, 0.20, 30, 25), (0.11, 0.10, -35, -20), (0.09, -0.16, -25, 30), (-0.11, -0.10, 28, -30))):
            p = oa.box("petal", (x, 0.285, z + 0.02), (0.07, 0.008, 0.12), SOOT, bevel=0.002, segments=1, rot=(rx, rz * 0.5, rz))
            out.append(matte(p, 122 + i, smooth=0.05))
    else:
        # The lit window is what says "shoot here": wide, with one guard bar, and a lit band right round the drum
        # so a live cell shows from every side of the mast (the old stand-in glowed all round).
        win = oa.box("window", (0, 0.266, 0.02), (0.24, 0.014, 0.50), CELL_LIT, bevel=0.006)
        glow.append(lit(win))
        out.append(metal(oa.box("guard", (0, 0.276, 0.02), (0.27, 0.012, 0.016), CHARCOAL, bevel=0.003, segments=1), 126, smooth=0.3, bare=0.4))
        glow.append(lit(lathe("band", (0, 0.02, 0.265), (0, 0, 1), [(0, 0.224), (0.0, 0.232), (0.12, 0.232), (0.12, 0.224)], CELL_LIT, 18)))
    out.append(tube("bail", [(-0.12, 0.02, 0.37), (-0.10, 0.02, 0.44), (0.10, 0.02, 0.44), (0.12, 0.02, 0.37)], 0.011, GALV_DARK, 130, cuts=2, n=6, up=(0, 1, 0)))
    # The lead out of the bottom, back to the pole.
    out.append(hose("lead", [(0.06, 0.05, -0.38), (0.07, 0.0, -0.44), (0.03, -0.16, -0.43), (0.0, -0.26, -0.36)], 0.016, 131))
    real.crowd_shadow(out, strength=0.5, reach=0.12)
    return out, glow


# ------------------------------------------------------------------ the salvage crate

def crate():
    out, glow = [], []
    w, d, h = CRATE
    lid_at = h * 0.62
    low = oa.box("case", (0, 0, lid_at / 2 + 0.005), (w - 0.03, d - 0.03, lid_at - 0.01), OLIVE, bevel=0.035, segments=3)
    lid = oa.box("lid", (0, 0, lid_at + (h - lid_at) / 2), (w - 0.03, d - 0.03, h - lid_at - 0.012), OLIVE, bevel=0.035, segments=3)
    for o, s in ((low, 1), (lid, 2)):
        oa.densify(o, 0.16)
        oa.paint(o, wear=0.8, dirt=0.9, mottle=0.7, seed=s, wear_colour=(0.50, 0.52, 0.42), grain=4.2)
        real.made_of(o, 0.22, 0.0)
        out.append(o)
    # Moulded ribs across the lid and down the long sides, and the gasket line where the lid meets the case.
    for x in (-0.24, -0.08, 0.08, 0.24):
        out.append(painted(oa.box("lid rib", (x, 0, h - 0.004), (0.05, d - 0.12, 0.018), OLIVE, bevel=0.007, segments=1), 140, longest=0.4, wear=0.9, wear_colour=(0.52, 0.54, 0.44)))
        for s in (-1, 1):
            out.append(painted(oa.box("side rib", (x, s * (d / 2 - 0.012), lid_at * 0.5), (0.05, 0.018, lid_at - 0.10), OLIVE, bevel=0.007, segments=1), 141, longest=0.4, wear=0.9, wear_colour=(0.52, 0.54, 0.44)))
    out.append(matte(oa.box("gasket", (0, 0, lid_at), (w - 0.022, d - 0.022, 0.012), RUBBER, bevel=0.003, segments=1), 142))
    # Corner bumpers, top and bottom.
    for sx in (-1, 1):
        for sy in (-1, 1):
            for z, tall in ((0.05, 0.10), (h - 0.04, 0.08)):
                out.append(matte(oa.box("bumper", (sx * (w / 2 - 0.045), sy * (d / 2 - 0.045), z), (0.09, 0.09, tall), OLIVE_DARK, bevel=0.022, segments=2), 143, smooth=0.12))
    # Butterfly latches on the front, hinges on the back, a spring handle at each end.
    for x in (-0.22, 0.22):
        out.append(metal(oa.box("latch plate", (x, d / 2 - 0.004, lid_at - 0.01), (0.075, 0.012, 0.12), CASE_METAL, bevel=0.004), 150, wear=0.7))
        out.append(metal(oa.box("latch wing", (x, d / 2 + 0.006, lid_at - 0.035), (0.10, 0.012, 0.035), CASE_METAL, bevel=0.005, rot=(0, 12, 0)), 151, wear=0.8))
        out.append(metal(oa.box("hinge", (x, -d / 2 + 0.004, lid_at), (0.11, 0.014, 0.05), CASE_METAL, bevel=0.004), 152))
    for s in (-1, 1):
        x = s * (w / 2 - 0.004)
        out.append(metal(oa.box("handle plate", (x, 0, lid_at * 0.56), (0.012, 0.22, 0.09), CASE_METAL, bevel=0.004), 153, wear=0.7))
        out.append(tube("handle", [(x + s * 0.006, -0.085, lid_at * 0.56), (x + s * 0.03, -0.075, lid_at * 0.46), (x + s * 0.03, 0.075, lid_at * 0.46), (x + s * 0.006, 0.085, lid_at * 0.56)], 0.011, CHARCOAL, 154, cuts=2, n=6, smooth=0.2, bare=0.2, up=(1, 0, 0)))
    # A webbing strap round the middle with a buckle, and the status strip set into the front of the lid.
    strap = oa.box("strap", (0, 0, h / 2 + 0.002), (0.042, d + 0.004, h + 0.003), WEBBING, bevel=0.003, segments=1)
    out.append(matte(strap, 155, smooth=0.05, longest=0.1))
    out.append(metal(oa.box("buckle", (0, d / 2 + 0.008, lid_at + 0.06), (0.07, 0.012, 0.05), CASE_METAL, bevel=0.004), 156))
    out.append(metal(oa.box("status bezel", (0.19, d / 2 - 0.004, lid_at + 0.085), (0.22, 0.014, 0.075), CHARCOAL, bevel=0.005), 157, smooth=0.3, bare=0.2))
    out.append(metal(oa.box("status bezel", (-0.19, d / 2 - 0.004, lid_at + 0.085), (0.22, 0.014, 0.075), CHARCOAL, bevel=0.005), 158, smooth=0.3, bare=0.2))
    # The status strips: on all four sides and across the lid, so the crate can be found from anywhere.
    for x in (-0.19, 0.19):
        glow.append(lit(oa.box("status", (x, d / 2 + 0.004, lid_at + 0.085), (0.19, 0.006, 0.05), HAUL_LIT, bevel=0.002, segments=1)))
        glow.append(lit(oa.box("status back", (x, -d / 2 - 0.002, lid_at + 0.085), (0.19, 0.006, 0.05), HAUL_LIT, bevel=0.002, segments=1)))
        glow.append(lit(oa.box("status lid", (x * 1.7, 0, h + 0.004), (0.022, d - 0.2, 0.006), HAUL_LIT, bevel=0.002, segments=1)))
    for sx in (-1, 1):
        glow.append(lit(oa.box("status end", (sx * (w / 2 - 0.012), 0, lid_at + 0.085), (0.006, 0.24, 0.05), HAUL_LIT, bevel=0.002, segments=1)))
    finish(out, 0.5, dust=0.6, on_top=0.3, reach=0.08, seed=2)
    return out, glow


# ------------------------------------------------------------------ the supply drop pod

def pod():
    out, glow = [], []
    R, H = POD_RADIUS, POD_TALL
    # A blunt heat shield scorched from the way down, a panelled barrel, a shoulder and a flat top deck.
    shield = lathe("heat shield", (0, 0, 0.0), (0, 0, 1), [(0.0, 0.0), (0.0, 0.22), (0.03, 0.34), (0.10, R - 0.04), (0.16, R - 0.005), (0.22, R - 0.005), (0.22, 0.0)], CHAR, 24)
    oa.densify(shield, 0.2)
    oa.paint(shield, wear=0.3, dirt=1.2, mottle=1.5, seed=31, wear_colour=(0.36, 0.25, 0.17), grain=5.0)
    real.made_of(shield, 0.06, 0.0)
    out.append(shield)
    barrel = lathe("barrel", (0, 0, 0.21), (0, 0, 1), [(0.0, R - 0.03), (0.02, R - 0.02), (0.80, R - 0.02), (0.92, R - 0.07), (1.05, R - 0.22), (1.09, R - 0.24), (1.09, 0.0)], SHELL, 24)
    oa.densify(barrel, 0.22)
    oa.paint(barrel, wear=0.6, dirt=0.9, mottle=0.7, seed=32, wear_colour=(0.56, 0.56, 0.54), grain=3.0, gradient=0.14)
    real.made_of(barrel, 0.34, 0.0)
    out.append(barrel)
    # Soot licking up the barrel from the shield is the paint's dirt; the panel seams and hoops are real shapes.
    for z in (0.26, 1.00):
        out.append(metal(lathe("hoop", (0, 0, z), (0, 0, 1), [(0, R - 0.03), (0, R - 0.004), (0.035, R - 0.004), (0.035, R - 0.03)], GALV_DARK, 24), 33, wear=0.6))
    for i in range(6):
        a = i * math.tau / 6 + 0.26
        seam = oa.box("seam", (0, 0, 0), (0.012, 0.012, 0.70), SHELL_SHADE, bevel=0.003, segments=1)
        seam.data.transform(Matrix.Rotation(a, 4, 'Z') @ Matrix.Translation((R - 0.018, 0, 0.64)))
        out.append(metal(seam, 34 + i, smooth=0.3, bare=0.3))
    # The hatch on the front (+Y): a door with a recessed pull and two quarter-turn locks.
    hatch = oa.box("hatch", (0, 0, 0.62), (0.40, 0.02, 0.50), SHELL_SHADE, bevel=0.008, segments=1)
    oa.densify(hatch, 0.07)
    for v in hatch.data.vertices:
        a = v.co.x / (R - 0.02)
        r = R - 0.012 + v.co.y
        v.co.x, v.co.y = r * math.sin(a), r * math.cos(a)
    out.append(painted(hatch, 41, smooth=0.3, longest=0.3, wear=0.7))
    out.append(metal(oa.box("pull", (0, R - 0.008, 0.62), (0.16, 0.014, 0.035), CHARCOAL, bevel=0.006), 42, smooth=0.3, bare=0.3))
    for x in (-0.15, 0.15):
        out.append(metal(lathe("lock", (x, R - 0.014, 0.80), (0, 1, 0), [(0, 0.024), (0.014, 0.024), (0.014, 0.0)], CASE_METAL, 8), 43))
    # Four short crush legs, splayed, with pads: bent a little, as things that land hard are.
    for i in range(4):
        a = i * math.tau / 4 + math.tau / 8
        c, s = math.cos(a), math.sin(a)
        top = (c * (R - 0.04), s * (R - 0.04), 0.34)
        knee = (c * (R + 0.12), s * (R + 0.12), 0.20)
        foot = (c * (R + 0.20), s * (R + 0.20), 0.02 + (0.012 if i == 2 else 0.0))
        out.append(tube("leg", [top, knee, foot], 0.026, GALV_DARK, 50 + i, cuts=1, n=8))
        out.append(tube("leg brace", [(c * (R - 0.03), s * (R - 0.03), 0.12), knee], 0.016, GALV_DARK, 54 + i, cuts=0, n=6))
        out.append(metal(lathe("pad", (foot[0], foot[1], 0.0), (0, 0, 1), [(0, 0.085), (0.02, 0.095), (0.03, 0.06), (0.03, 0.0)], GALV_DARK, 10), 58 + i, wear=0.7))
    # The top deck: riser lugs, the cut ends of the parachute bridle lying over the shoulder, the beacon stalk.
    top = 0.21 + 1.09
    for i in range(3):
        a = i * math.tau / 3 + 0.5
        c, s = math.cos(a), math.sin(a)
        out.append(metal(oa.box("lug", (c * 0.19, s * 0.19, top + 0.035), (0.03, 0.06, 0.07), CASE_METAL, bevel=0.01, rot=(0, 0, math.degrees(a) + 90)), 60 + i))
        strap = real.sweep("riser", [(c * 0.19, s * 0.19, top + 0.05), (c * 0.30, s * 0.30, top + 0.02), (c * 0.43, s * 0.43, top - 0.14), (c * 0.50 + s * 0.05, s * 0.50 - c * 0.05, top - 0.36 - 0.07 * i)],
                           0.03, ORANGE, n=6, flat=0.14, cuts=3, up=lambda p: (p.x, p.y, 0.6))
        out.append(matte(strap, 63 + i, smooth=0.05))
    out.append(metal(lathe("deck ring", (0, 0, top - 0.005), (0, 0, 1), [(0, 0.27), (0.02, 0.27), (0.02, 0.10), (0.0, 0.10)], GALV_DARK, 20), 66))
    stalk_top = POD_BEACON - 0.06
    out.append(tube("stalk", [(0, 0, top), (0, 0, stalk_top)], 0.018, GALV_DARK, 67, cuts=0, n=8, up=(0, 1, 0)))
    out.append(metal(lathe("lamp base", (0, 0, stalk_top - 0.02), (0, 0, 1), [(0, 0.03), (0.0, 0.05), (0.03, 0.05), (0.03, 0.0)], CHARCOAL, 10), 68, smooth=0.3, bare=0.3))
    glow.append(lit(lathe("beacon", (0, 0, stalk_top + 0.01), (0, 0, 1), [(0, 0.042), (0.06, 0.046), (0.10, 0.03), (0.115, 0.0)], HAUL_LIT, 10)))
    out.append(metal(lathe("lamp cap", (0, 0, stalk_top + 0.118), (0, 0, 1), [(0, 0.034), (0.012, 0.034), (0.02, 0.012), (0.02, 0.0)], CHARCOAL, 10), 69, smooth=0.3, bare=0.3))
    # The ring of marker lamps round the shoulder (where the stand-in's glowing band was).
    for i in range(8):
        a = i * math.tau / 8
        m = oa.box("marker", (0, 0, 0), (0.016, 0.11, 0.035), HAUL_LIT, bevel=0.004, segments=1)
        m.data.transform(Matrix.Rotation(a, 4, 'Z') @ Matrix.Translation((R - 0.012, 0, 1.0 + 0.017)))
        glow.append(lit(m))
    finish(out, 1.0, dust=0.45, on_top=0.15, reach=0.14, seed=3)
    return out, glow


# ------------------------------------------------------------------ the cave work lamp

def lamp():
    out, glow = [], []
    # A builder's tripod light: three telescoping legs with rubber feet, a sliding collar and stay rods.
    hub = Vector((0, 0, 0.92))
    for i in range(3):
        a = i * math.tau / 3 + 0.5
        foot = Vector((math.cos(a) * 0.56, math.sin(a) * 0.56, 0.02))
        mid = hub.lerp(foot, 0.55)
        out.append(tube("leg upper", [hub, mid], 0.016, YELLOW_DARK, 200 + i, cuts=0, n=8, smooth=0.3, bare=0.0))
        out.append(tube("leg lower", [mid, foot], 0.012, GALV, 203 + i, cuts=0, n=8))
        out.append(metal(oa.cylinder("leg lock", mid - (foot - hub).normalized() * 0.02, mid + (foot - hub).normalized() * 0.02, 0.021, CHARCOAL, segments=8), 206, smooth=0.2, bare=0.1))
        out.append(matte(lathe("foot", foot - Vector((0, 0, 0.02)), (0, 0, 1), [(0, 0.03), (0.03, 0.024), (0.035, 0.0)], RUBBER, 8), 207))
        out.append(tube("stay", [Vector((0, 0, 0.50)), hub.lerp(foot, 0.42)], 0.007, GALV_DARK, 208 + i, cuts=0, n=5))
    out.append(metal(oa.cylinder("hub", (0, 0, 0.87), (0, 0, 0.97), 0.045, CHARCOAL, segments=10, bevel=0.008), 211, smooth=0.2, bare=0.1))
    out.append(metal(oa.cylinder("slider", (0, 0, 0.47), (0, 0, 0.53), 0.034, CHARCOAL, segments=10, bevel=0.006), 212, smooth=0.2, bare=0.1))
    out.append(tube("mast", [(0, 0, 0.45), (0, 0, 1.30)], 0.019, YELLOW_DARK, 213, cuts=0, n=8, smooth=0.3, bare=0.0, up=(0, 1, 0)))
    out.append(tube("mast inner", [(0, 0, 1.28), (0, 0, 1.60)], 0.014, GALV, 214, cuts=0, n=8, up=(0, 1, 0)))
    out.append(metal(oa.cylinder("mast lock", (0, 0, 1.27), (0, 0, 1.32), 0.026, CHARCOAL, segments=8), 215, smooth=0.2, bare=0.1))
    out.append(metal(oa.cylinder("lock knob", (0.02, 0, 1.295), (0.065, 0, 1.295), 0.012, CHARCOAL, segments=6), 216, smooth=0.2, bare=0.1))
    # The head: a U bracket, a finned cast body tipped a little down, a deep rubber hood round the lens.
    out.append(tube("bracket", [(-0.27, 0.0, 1.72), (-0.27, 0.0, 1.60), (0.27, 0.0, 1.60), (0.27, 0.0, 1.72)], 0.012, GALV_DARK, 217, cuts=1, n=6, up=(0, 1, 0)))
    tip = Matrix.Translation((0, 0.02, 1.72)) @ Matrix.Rotation(math.radians(-8), 4, 'X')
    body = oa.box("head", (0, 0.0, 0), (0.48, 0.14, 0.33), YELLOW, bevel=0.022, segments=2)
    body.data.transform(tip)
    out.append(painted(body, 218, smooth=0.3, longest=0.12, wear=0.8, wear_colour=(0.45, 0.44, 0.42)))
    for k in range(9):
        fin = oa.box("fin", (-0.20 + k * 0.05, -0.095, 0), (0.012, 0.06, 0.27), CHARCOAL, bevel=0.003, segments=1)
        fin.data.transform(tip)
        out.append(metal(fin, 219 + k, smooth=0.3, bare=0.5))
    hood = oa.box("hood", (0, 0.085, 0), (0.52, 0.05, 0.37), RUBBER, bevel=0.016, segments=2)
    hood.data.transform(tip)
    out.append(matte(hood, 230))
    lens = oa.box("lens", (0, 0.112, 0), (0.43, 0.012, 0.28), LENS, bevel=0.004, segments=1)
    lens.data.transform(tip)
    glow.append(lit(lens))
    for s in (-1, 1):
        k = lathe("pivot knob", (s * 0.255, 0.02, 1.72), (s, 0, 0), [(0, 0.03), (0.035, 0.03), (0.035, 0.0)], CHARCOAL, 8)
        out.append(metal(k, 231, smooth=0.2, bare=0.1))
    # The cable: off the back of the head, down the mast in loose turns, across the floor to the generator.
    out.append(hose("cable", [(0.05, -0.08, 1.66), (0.06, -0.10, 1.45), (0.03, -0.03, 1.20), (-0.03, 0.02, 0.98), (0.04, 0.03, 0.70), (0.03, -0.04, 0.40), (0.10, -0.10, 0.06),
                              (0.30, -0.22, 0.014), (0.52, -0.30, 0.014), (0.62, -0.50, 0.014), (0.70, -0.58, 0.10)], 0.012, 232, cuts=3))
    # The generator: a small framed set with a tank on top, a pull start and an exhaust.
    gx, gy = 0.85, -0.65
    out.append(painted(oa.box("generator", (gx, gy, 0.20), (0.44, 0.30, 0.26), YELLOW, bevel=0.03, segments=2), 240, smooth=0.3, longest=0.12, wear=0.8, wear_colour=(0.45, 0.44, 0.42)))
    out.append(painted(oa.box("tank", (gx, gy, 0.385), (0.36, 0.24, 0.10), CHARCOAL, bevel=0.035, segments=3), 241, smooth=0.35, longest=0.12))
    out.append(metal(lathe("filler", (gx - 0.08, gy, 0.43), (0, 0, 1), [(0, 0.03), (0.022, 0.03), (0.022, 0.0)], GALV_DARK, 8), 242))
    for sx in (-1, 1):
        fr = [(gx + sx * 0.25, gy - 0.18, 0.02), (gx + sx * 0.25, gy - 0.18, 0.47), (gx + sx * 0.25, gy + 0.18, 0.47), (gx + sx * 0.25, gy + 0.18, 0.02)]
        out.append(tube("frame", fr, 0.014, GALV_DARK, 243, cuts=2, n=6, up=(1, 0, 0)))
    for sy in (-1, 1):
        out.append(tube("frame rail", [(gx - 0.25, gy + sy * 0.18, 0.03), (gx + 0.25, gy + sy * 0.18, 0.03)], 0.014, GALV_DARK, 244, cuts=0, n=6))
    out.append(metal(lathe("pull start", (gx, gy + 0.15, 0.22), (0, 1, 0), [(0, 0.075), (0.02, 0.075), (0.03, 0.05), (0.03, 0.0)], CHARCOAL, 12), 245, smooth=0.2, bare=0.1))
    out.append(metal(oa.cylinder("exhaust", (gx + 0.20, gy - 0.06, 0.26), (gx + 0.31, gy - 0.06, 0.26), 0.022, (0.30, 0.24, 0.20), segments=8), 246, smooth=0.2, bare=0.5))
    out.append(metal(oa.box("socket", (gx - 0.225, gy + 0.04, 0.16), (0.014, 0.07, 0.07), CHARCOAL, bevel=0.005), 247, smooth=0.2, bare=0.1))
    finish(out, 0.5, dust=0.5, on_top=0.2, reach=0.1, seed=4)
    return out, glow


# ------------------------------------------------------------------ the melee weapons (held at the origin, up is Z)

def laser_sword():
    out, glow = [], []
    # A machined grip: knurled sleeve, a pommel with a lanyard ring, a guard collar, an emitter with cooling slots.
    out.append(metal(lathe("pommel", (0, 0, -0.012), (0, 0, 1), [(0, 0.0), (0, 0.016), (0.008, 0.021), (0.026, 0.021), (0.032, 0.017), (0.032, 0.0)], STEEL, 14), 300, smooth=0.6))
    grip = lathe("grip", (0, 0, 0.02), (0, 0, 1), [(0, 0.0165), (0.17, 0.0165), (0.17, 0.0)], TAPE, 14)
    oa.densify(grip, 0.02)
    out.append(real.stripes(matte(grip, 301, smooth=0.1), (0, 0, 1), 0.012, (0.05, 0.05, 0.055), share=0.35, strength=0.8))
    out.append(metal(lathe("switch ring", (0, 0, 0.155), (0, 0, 1), [(0, 0.0165), (0.0, 0.0195), (0.02, 0.0195), (0.02, 0.0165)], BRASS, 14), 302, smooth=0.6))
    out.append(metal(oa.box("switch", (0, 0.019, 0.125), (0.010, 0.006, 0.03), (0.60, 0.16, 0.12), bevel=0.002, segments=1), 303, smooth=0.3, bare=0.0))
    out.append(metal(lathe("guard", (0, 0, 0.19), (0, 0, 1), [(0, 0.0165), (0.006, 0.027), (0.024, 0.027), (0.034, 0.021), (0.07, 0.019), (0.07, 0.0)], STEEL, 14), 304, smooth=0.6))
    for i in range(6):
        a = i * math.tau / 6
        s = oa.box("slot", (0, 0, 0), (0.004, 0.006, 0.026), (0.03, 0.03, 0.035), bevel=0.001, segments=1)
        s.data.transform(Matrix.Rotation(a, 4, 'Z') @ Matrix.Translation((0.0185, 0, 0.243)))
        out.append(matte(s, 305))
    out.append(metal(lathe("emitter", (0, 0, 0.258), (0, 0, 1), [(0, 0.019), (0.004, 0.0215), (0.012, 0.0215), (0.014, 0.012), (0.006, 0.010), (0.006, 0.0)], STEEL_DARK, 14), 306, smooth=0.5))
    # The blade: a round bar of light with a rounded tip, drawn glowing.
    glow.append(lit(lathe("blade", (0, 0, 0.262), (0, 0, 1), [(0, 0.0), (0.0, 0.012), (0.02, 0.0145), (0.90, 0.0135), (0.935, 0.009), (0.948, 0.0)], BLADE, 10)))
    real.crowd_shadow(out, strength=0.4, reach=0.03)
    return out, glow


def baseball_bat():
    out = []
    # An ash bat's real taper: knob, thin handle, long flare to the barrel, a cupped end. Grip tape on the handle.
    prof = [(-0.02, 0.0), (-0.02, 0.022), (-0.008, 0.026), (0.004, 0.022), (0.012, 0.0145), (0.10, 0.0135), (0.26, 0.0145), (0.38, 0.0185), (0.50, 0.026), (0.62, 0.0315), (0.76, 0.033), (0.835, 0.032), (0.855, 0.027), (0.86, 0.018), (0.848, 0.0)]
    bat = lathe("bat", (0, 0, 0), (0, 0, 1), prof, ASH, 16)
    oa.densify(bat, 0.035)
    oa.paint(bat, wear=0.5, dirt=0.9, mottle=0.9, seed=310, wear_colour=ASH_DARK, grain=22.0, gradient=0.06)
    # Wood grain runs along the bat: long streaks, not blotches.
    layer = bat.data.color_attributes["Col"]
    from mathutils import noise
    for v in bat.data.vertices:
        a = math.atan2(v.co.y, v.co.x)
        g = 0.90 + 0.12 * noise.noise(Vector((a * 2.6, v.co.z * 1.4, 3.0))) + 0.05 * noise.noise(Vector((a * 9.0, v.co.z * 3.0, 9.0)))
        scuff = 1.0 - 0.25 * max(0.0, noise.noise(Vector((a * 3.0, v.co.z * 14.0, 5.0)))) * (1.0 if v.co.z > 0.5 else 0.3)
        c = layer.data[v.index].color
        layer.data[v.index].color = (c[0] * g * scuff, c[1] * g * scuff, c[2] * g * scuff * 0.97, 1.0)
    real.made_of(bat, 0.3, 0.0)
    out.append(bat)
    tape = real.stripes(matte(oa.densify(lathe("tape", (0, 0, 0.014), (0, 0, 1), [(0, 0.0155), (0.24, 0.0158), (0.245, 0.0145)], TAPE, 14), 0.02), 311, smooth=0.06), (0.25, 0, 1), 0.022, (0.06, 0.06, 0.065), share=0.18, strength=0.9)
    out.append(tape)
    real.crowd_shadow(out, strength=0.35, reach=0.03)
    return out, []


def mailbox():
    out = []
    # A rural post box torn up post and all: a split cedar post with a clod of the footing still on it, the tin
    # box with its arched roof and dented door, the flag on its pivot.
    post = oa.box("post", (0, 0, 0.44), (0.075, 0.075, 0.92), POST, bevel=0.006, segments=1)
    oa.densify(post, 0.05)
    oa.crumple(post, 0.004, grain=14.0, seed=2)
    oa.paint(post, wear=0.5, dirt=1.0, mottle=1.2, seed=320, wear_colour=(0.46, 0.40, 0.32), grain=18.0, gradient=0.10)
    real.made_of(post, 0.05, 0.0)
    out.append(post)
    out.append(real.stripes(matte(oa.densify(oa.box("grip wrap", (0, 0, 0.10), (0.082, 0.082, 0.20), TAPE, bevel=0.006, segments=1), 0.03), 321, smooth=0.06), (0.2, 0, 1), 0.03, (0.06, 0.06, 0.065), share=0.2, strength=0.9))
    out.append(matte(oa.box("shelf", (0, 0.04, 0.905), (0.16, 0.42, 0.025), POST, bevel=0.004, segments=1), 322, smooth=0.05, longest=0.06))
    out.append(matte(oa.box("brace", (0, 0.10, 0.80), (0.05, 0.22, 0.035), POST, bevel=0.004, segments=1, rot=(42, 0, 0)), 323, smooth=0.05, longest=0.06))
    # The box: an arch-roofed tunnel 0.44 long, thickened along X by slab(), door at the +Y end.
    arch = [(-0.11, 0.0)] + [(-0.11 * math.cos(math.radians(t)), 0.10 + 0.105 * math.sin(math.radians(t))) for t in range(0, 181, 20)] + [(0.11, 0.0)]
    bm = bmesh.new()
    y0, y1 = -0.18, 0.26
    a = [bm.verts.new((p[0], y0, 0.918 + p[1])) for p in arch]
    b = [bm.verts.new((p[0], y1, 0.918 + p[1])) for p in arch]
    for i in range(len(arch)):
        j = (i + 1) % len(arch)
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    tin = oa._new_object("box", bm, TIN, 40.0)
    oa.densify(tin, 0.045)
    oa.crumple(tin, 0.005, grain=7.0, seed=6)
    oa.paint(tin, wear=0.8, dirt=0.9, mottle=0.9, seed=324, wear_colour=(0.55, 0.56, 0.56), grain=5.0)
    real.made_of(tin, 0.4, 0.3)
    out.append(tin)
    door = oa.slab("door", [(p[0], p[1]) for p in arch], 0.012, TIN, bevel=0.003, segments=1)
    # slab() makes outlines in (forward, up) thickened along X: turn it to face +Y.
    door.data.transform(Matrix.Translation((0, y1 + 0.008, 0.918)) @ Matrix.Rotation(math.radians(-7), 4, 'X') @ Matrix.Rotation(math.radians(90), 4, 'Z'))
    out.append(metal(door, 325, wear=0.8, smooth=0.4, bare=0.3))
    out.append(metal(oa.box("door catch", (0, y1 + 0.02, 0.918 + 0.20), (0.03, 0.02, 0.03), STEEL_DARK, bevel=0.005), 326))
    for y in (y0 + 0.015, y1 - 0.02):
        out.append(metal(oa.box("seam", (0, y, 0.918 + 0.003), (0.226, 0.012, 0.012), STEEL_DARK, bevel=0.003, segments=1), 327))
    # The flag: a pivot and an arm standing up, with its paddle.
    out.append(metal(lathe("flag pivot", (0.112, -0.02, 0.918 + 0.07), (1, 0, 0), [(0, 0.014), (0.012, 0.014), (0.012, 0.0)], STEEL_DARK, 8), 328))
    out.append(metal(oa.box("flag arm", (0.120, -0.02, 0.918 + 0.15), (0.006, 0.022, 0.18), FLAG, bevel=0.002, segments=1), 329, wear=0.7, smooth=0.3, bare=0.0))
    out.append(metal(oa.box("flag", (0.120, 0.012, 0.918 + 0.215), (0.006, 0.07, 0.05), FLAG, bevel=0.002, segments=1), 330, wear=0.7, smooth=0.3, bare=0.0))
    real.crowd_shadow(out, strength=0.5, reach=0.08)
    return out, []


def butterfly_knife():
    out = []
    # A balisong held open: two skeleton handles side by side with their latch, pivot pins, a clip-point blade.
    for s in (-1, 1):
        h = oa.box("handle", (s * 0.0085, 0, 0.058), (0.012, 0.016, 0.116), STEEL_DARK, bevel=0.003, segments=1)
        out.append(metal(h, 340 + s, wear=0.7, smooth=0.55))
        for k in range(4):
            out.append(matte(oa.box("hole", (s * 0.0085, 0, 0.022 + k * 0.022), (0.0125, 0.007, 0.011), (0.03, 0.03, 0.035), bevel=0.002, segments=1), 341))
        out.append(metal(lathe("pivot", (-0.0155, 0, 0.108) if s < 0 else (0.0025, 0, 0.108), (1, 0, 0), [(0, 0.0045), (0.013, 0.0045), (0.013, 0.0)], BRASS, 8), 342, smooth=0.6))
    out.append(metal(oa.box("latch", (0, 0, 0.004), (0.026, 0.006, 0.008), STEEL, bevel=0.002, segments=1), 343, smooth=0.6))
    # The blade is a flat outline in (forward, up), thickened across X then turned edge-forward: spine at -Y.
    outline = [(-0.010, 0.100), (0.010, 0.100), (0.011, 0.118), (0.010, 0.185), (0.004, 0.214), (-0.003, 0.230), (-0.006, 0.205), (-0.010, 0.190)]
    blade = oa.slab("blade", outline, 0.0032, STEEL, bevel=0.0012, segments=1)
    out.append(metal(blade, 344, wear=0.4, smooth=0.8, bare=1.0))
    out.append(metal(oa.box("tang pin", (0, -0.006, 0.106), (0.006, 0.004, 0.008), STEEL_DARK, bevel=0.001, segments=1), 345))
    real.crowd_shadow(out, strength=0.4, reach=0.012)
    return out, []


def frying_pan():
    out = []
    # A cast iron skillet held by its handle: the pan's face looks forward (+Y), so a swing lands on the flat.
    out.append(real.stripes(matte(oa.densify(lathe("grip wrap", (0, 0, 0.0), (0, 0, 1), [(0, 0.0), (0, 0.0145), (0.15, 0.0155), (0.15, 0.0)], (0.30, 0.22, 0.15), 10), 0.02), 350, smooth=0.06), (0.2, 0, 1), 0.018, (0.18, 0.13, 0.09), share=0.2, strength=0.8))
    handle = real.sweep("handle", [(0, 0, 0.0), (0, 0, 0.10), (0, -0.004, 0.20), (0, -0.010, 0.245)], 0.013, IRON, n=8, flat=0.62, cuts=2, up=(0, 1, 0))
    out.append(metal(handle, 351, wear=0.5, smooth=0.3, bare=0.5, dirt=0.9))
    out.append(metal(lathe("hang ring", (0, 0, -0.012), (0, 1, 0), [(-0.005, 0.009), (-0.005, 0.017), (0.005, 0.017), (0.005, 0.009)], IRON, 10), 352, smooth=0.3, bare=0.5))
    # The pan: floor, flared wall, rolled rim, two pour lips; the cooking face a little glossier from seasoning.
    centre = (0, -0.022, 0.37)
    pan = lathe("pan", centre, (0, 1, 0), [(0.0, 0.0), (0.0, 0.108), (0.006, 0.116), (0.040, 0.134), (0.046, 0.137), (0.046, 0.130), (0.040, 0.128), (0.010, 0.110), (0.008, 0.104), (0.008, 0.0)], IRON, 24)
    oa.densify(pan, 0.04)
    oa.paint(pan, wear=0.45, dirt=1.0, mottle=1.2, seed=353, wear_colour=(0.24, 0.23, 0.22), grain=9.0)
    real.made_of(pan, 0.36, 0.5)
    out.append(pan)
    for s in (-1, 1):
        out.append(metal(oa.box("pour lip", (s * 0.136, 0.020, 0.37), (0.016, 0.012, 0.03), IRON, bevel=0.004, segments=1), 354, smooth=0.3, bare=0.5))
    out.append(metal(oa.box("helper tab", (0, 0.018, 0.37 + 0.142), (0.05, 0.010, 0.022), IRON, bevel=0.004, segments=1), 355, smooth=0.3, bare=0.5))
    real.crowd_shadow(out, strength=0.45, reach=0.04)
    return out, []


# ------------------------------------------------------------------ building, checking, pictures

def props():
    pieces = [("mastBase", mast_base()), ("mast", mast())]
    c, cg = cell()
    burnt, _ = cell(burnt=True)
    pieces += [("cell", c), ("cellGlow", cg), ("cellBurnt", burnt)]
    k, kg = crate()
    pieces += [("crate", k), ("crateGlow", kg)]
    p, pg = pod()
    pieces += [("pod", p), ("podGlow", pg)]
    l, lg = lamp()
    pieces += [("lamp", l), ("lampGlow", lg)]
    return pieces


def melee():
    pieces = []
    for name, make in (("LaserSword", laser_sword), ("BaseballBat", baseball_bat), ("Mailbox", mailbox), ("ButterflyKnife", butterfly_knife), ("FryingPan", frying_pan)):
        body, glow = make()
        pieces.append((name, body))
        if glow:
            pieces.append((name + "Glow", glow))
        top = max((o.matrix_world @ v.co).z for o in body + glow for v in o.data.vertices)
        if abs(top - TIPS[name]) > 0.03:
            raise SystemExit("%s reaches %.3f, but the game's tip is at %.3f" % (name, top, TIPS[name]))
    sockets = [oa.socket(name + "Tip", (0, 0, z)) for name, z in TIPS.items()]
    return pieces, sockets


def placed(objects, matrix):
    out = []
    for o in objects:
        c = o.copy()
        c.data = o.data.copy()
        c.data.transform(matrix)
        bpy.context.scene.collection.objects.link(c)
        out.append(c)
    return out


def count(pieces):
    for name, objs in pieces:
        print("  %-16s %6d triangles" % (name, sum(oa._triangle_count(o) for o in objs)))


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    previews = args[args.index("--preview") + 1] if "--preview" in args else None
    out_dir = args[args.index("--write-to") + 1] if "--write-to" in args else OUT
    os.makedirs(out_dir, exist_ok=True)
    oa.reset_scene()
    # Every piece is thinned to its budget (docs/performance_budget.md, "Task views"): a world can have two dozen
    # masts standing at once. Faces that can never be seen are dropped on the way.
    prop_pieces = []
    for name, objects in props():
        if name in BUDGET:
            prop_pieces += oa.thin([(name, objects)], BUDGET[name], drop_below=0.0, whole=48, least=16)
            for o in objects:
                bpy.data.objects.remove(o)
        else:
            prop_pieces.append((name, objects))
    count(prop_pieces)
    oa.export(os.path.join(out_dir, "Props.bytes"), prop_pieces, [])
    melee_pieces, melee_sockets = melee()
    count(melee_pieces)
    oa.export(os.path.join(out_dir, "Melee.bytes"), melee_pieces, melee_sockets)
    if previews:
        os.makedirs(previews, exist_ok=True)
        by = dict(prop_pieces)
        mby = dict(melee_pieces)
        every = [o for _, objs in prop_pieces + melee_pieces for o in objs]

        def shot(name, shown, glow, eye, target, lens=40, floor=(0.40, 0.37, 0.33), **look):
            for o in every:
                o.hide_render = True
            for o in shown:
                o.hide_render = False
            ground = oa.box("ground", (0, 0, -0.01), (300, 300, 0.02), floor, bevel=0)
            oa.paint(ground, wear=0, dirt=0, mottle=0, gradient=0)
            oa.render_preview(os.path.join(previews, name), shown + [ground], eye, target, lens=lens, size=(1280, 800), samples=48, glow=glow, **look)
            bpy.data.objects.remove(ground)

        # The mast as the game stands it up: three cells round the pole, the middle one shot out.
        cells, cell_glow = [], []
        for i in range(3):
            turn_ = i * 120.0
            m = Matrix.Rotation(-math.radians(turn_), 4, 'Z') @ Matrix.Translation((0, 0.55, 2.2 + i * 2.4))
            cells += placed(by["cellBurnt"] if i == 1 else by["cell"], m)
            if i != 1:
                cell_glow += placed(by["cellGlow"], m)
        mast_all = by["mastBase"] + by["mast"] + cells + cell_glow
        every += cells + cell_glow
        shot("mast.png", mast_all, cell_glow, (7.5, 9.5, 3.4), (0, 0, 4.6), lens=24)
        shot("mast_foot.png", mast_all, cell_glow, (2.6, 4.2, 2.3), (0, 0, 1.7), lens=30)
        shot("crate.png", by["crate"] + by["crateGlow"], by["crateGlow"], (1.0, 1.5, 0.75), (0, 0, 0.28), lens=45)
        shot("pod.png", by["pod"] + by["podGlow"], by["podGlow"], (1.9, 2.9, 1.35), (0, 0, 0.75), lens=40)
        shot("lamp.png", by["lamp"] + by["lampGlow"], by["lampGlow"], (2.6, 3.0, 1.6), (0.35, -0.2, 0.95), lens=32, floor=(0.22, 0.20, 0.19), world=(0.10, 0.10, 0.12), sky=0.8, sun_energy=2.4)
        # The five weapons leant in a row, as on the arena's table.
        row, row_glow = [], []
        for i, name in enumerate(("ButterflyKnife", "FryingPan", "BaseballBat", "Mailbox", "LaserSword")):
            m = Matrix.Translation((-0.9 + i * 0.45, 0, 0.02)) @ Matrix.Rotation(math.radians(-25 if name != "FryingPan" else 205), 4, 'Z')
            row += placed(mby[name], m)
            if name + "Glow" in mby:
                g = placed(mby[name + "Glow"], m)
                row += g
                row_glow += g
        every += row
        shot("melee.png", row, row_glow, (0.5, 2.5, 0.95), (0.0, 0, 0.55), lens=45, floor=(0.30, 0.29, 0.28))
        knife = placed(mby["ButterflyKnife"], Matrix.Translation((0, 0, 0.004)))
        pan = placed(mby["FryingPan"], Matrix.Translation((0.30, 0, 0.02)) @ Matrix.Rotation(math.radians(200), 4, 'Z'))
        every += knife + pan
        shot("melee_close.png", knife + pan, [], (0.35, 0.62, 0.30), (0.14, 0, 0.20), lens=50, floor=(0.30, 0.29, 0.28))
