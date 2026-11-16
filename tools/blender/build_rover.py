"""Builds the moon rover and the davit that carries it on the lander.

    blender -b -P tools/blender/build_rover.py
    blender -b -P tools/blender/build_rover.py -- --preview <folder>

Matthew, 16 Nov 2026: "a moon rover vehicle that can run enemies over ... really fast and aesthetically pleasing".
It starts from a named real thing, the Apollo Lunar Roving Vehicle (open twin seats, a T-handle hand controller
between them, wire-mesh wheels with titanium chevrons, a motor in every hub, steering at both ends, fenders over
the wheels, a dish on a mast), and borrows from the Artemis terrain vehicle concepts what a fast one needs: a
roll cage, long-travel double wishbones and two boost nozzles at the back. Original design, no markings.

Rover space (Blender): X right, Y forward, Z up, origin on the ground under the middle of the wheelbase at rest.
The game turns that into X right, Y up, Z forward. Every number the game needs is in RoverLayout
(Scripts/Runtime/Rover/RoverRules.cs); the copy here is checked by the rule test RoverModelFile_MatchesTheLayout.

Pieces of "Rover":
  body        everything that does not move (frame, cage, seats, nose box, fenders, deck, mast, nozzles)
  wheel       one wheel, built round its own hub (axle along X); the game draws it four times
  arm         the right front corner's wishbones and upright, built round the arms' inner pivot; the game turns
              a copy at each corner (the left ones turned half round) and swings it with the wheel
  lampGlow    the two headlamp lenses
  screenGlow  the driver's display
  boostGlow   the throats of the two boost nozzles (the game lights them while boosting)
Sockets: WheelFL WheelFR WheelRL WheelRR (hubs at rest), ArmFL ArmFR ArmRL ArmRR (pivots), Eye, Exit,
         BoostL BoostR (nozzle mouths), Lamp (between the lamps).

"RoverDavit" is in ship space (the lander's, see build_ship.py) and has one piece, davit: two beams out of the
back of the base with a winch at each tip, which the rover hangs from. Socket: Stow (the rover's origin there).
"""
import bpy, bmesh, sys, os, math
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import oa_blender as oa
import oa_real as real

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "unity", "Assets", "OrbitalAnte", "Resources", "Models"))

# ---- from RoverLayout (checked by the rule test RoverModelFile_MatchesTheLayout)
HALF_TRACK, HALF_BASE = 1.05, 1.30
WHEEL_RADIUS, WHEEL_WIDTH = 0.46, 0.30
ARM_PIVOT_X, ARM_PIVOT_Z, ARM_LENGTH = 0.36, 0.50, 0.46
EYE = (-0.36, -0.10, 1.58)          # x, forward, up
EXIT = (-1.75, -0.10, 0.05)
BOOST = (0.30, -1.86, 0.86)         # right nozzle mouth; the left is mirrored
LAMP = (0.0, 1.86, 0.86)
# ---- from ShipLayout (the lander) and RoverLayout.StowInShip
SHIP_BELLY_Y, SHIP_BASE_INRADIUS = -1.7, 2.3
STOW = (0.0, -3.62, -4.15)          # ship space x, y, z: the rover's origin when it hangs on the davit (facing -z)

# Colours as seen on screen. White painted panels and bare aluminium like the real rovers, charcoal rubber and
# composite, gold foil on what must be kept warm, a little safety orange, and grey moon dust low down.
WHITE, WHITE_SHADE = (0.86, 0.86, 0.83), (0.74, 0.75, 0.74)
ALU, ALU_DARK = (0.70, 0.71, 0.72), (0.50, 0.51, 0.53)
CHARCOAL, RUBBER = (0.15, 0.15, 0.16), (0.09, 0.09, 0.10)
MESH, CHEVRON = (0.30, 0.30, 0.31), (0.60, 0.60, 0.58)
GOLD, GOLD_DARK = (0.80, 0.55, 0.16), (0.62, 0.40, 0.11)
WEBBING, WEBBING_DARK = (0.34, 0.38, 0.44), (0.24, 0.27, 0.32)
ORANGE = (0.84, 0.42, 0.13)
LENS, SCREEN, THROAT = (1.0, 0.95, 0.84), (0.42, 0.80, 0.86), (1.0, 0.62, 0.30)
DUST = (0.50, 0.49, 0.47)
FENDER = (0.40, 0.40, 0.41)


def metal(o, seed=0, wear=0.5, smooth=0.55, bare=0.85):
    oa.paint(o, wear=wear, dirt=0.6, mottle=0.5, seed=seed, grain=2.2)
    real.made_of(o, smooth, bare)
    return o


def painted(o, seed=0, smooth=0.32):
    oa.densify(o, 0.25)
    oa.paint(o, wear=0.55, dirt=0.75, mottle=0.55, seed=seed, wear_colour=(0.62, 0.62, 0.60), grain=2.4)
    real.made_of(o, smooth, 0.0)
    return o


def matte(o, seed=0, smooth=0.08):
    oa.paint(o, wear=0.3, dirt=0.6, mottle=0.6, seed=seed, grain=3.0)
    real.made_of(o, smooth, 0.0)
    return o


def foil(o, seed=0):
    oa.densify(o, 0.12)
    oa.crumple(o, 0.006, grain=9.0, seed=seed)
    oa.paint(o, wear=0.5, dirt=0.6, mottle=1.3, seed=seed, wear_colour=(0.98, 0.84, 0.48), gradient=0.14, grain=6.0)
    real.made_of(o, 0.62, 0.9)
    return o


def tube(name, points, radius=0.026, colour=ALU, seed=0, cuts=3):
    o = real.sweep(name, points, radius, colour, n=10, cuts=cuts)
    return metal(o, seed, wear=0.6)


def lathe(name, origin, axis, profile, colour, segments=24):
    """oa.lathe with no zero-width ring (a ring of one point makes a cap of coincident corners, drawn inside out)."""
    return oa.lathe(name, origin, axis, [(a, max(r, 0.004)) for a, r in profile], colour, segments)


def revolve(name, loop, colour, segments=48, smooth=50.0):
    """Spins a closed outline (a loop of (along X, radius) points, going round once) about the X axis: a ring with
    a hole through the middle, like a tyre."""
    bm = bmesh.new()
    rings = []
    for along, radius in loop:
        rings.append([bm.verts.new((along, radius * math.cos(2 * math.pi * i / segments), radius * math.sin(2 * math.pi * i / segments)))
                      for i in range(segments)])
    n = len(rings)
    for k in range(n):
        a, b = rings[k], rings[(k + 1) % n]
        for i in range(segments):
            j = (i + 1) % segments
            bm.faces.new((a[i], a[j], b[j], b[i]))
    return oa._new_object(name, bm, colour, smooth)


def mirror_x(points):
    return [(-p[0], p[1], p[2]) for p in points]


# ------------------------------------------------------------------ the body

def frame():
    out = []
    # Two side rails: low along the floor, kicking up at the nose and the tail.
    rail = [(0.50, 1.80, 0.86), (0.52, 1.55, 0.66), (0.52, 0.9, 0.60), (0.52, -0.9, 0.60), (0.50, -1.55, 0.66), (0.46, -1.86, 0.92)]
    for s, pts in ((1, rail), (-1, mirror_x(rail))):
        out.append(tube("rail", pts, 0.032, ALU, 1 + s))
    # Cross members under the floor, and the wishbones' mounting towers at each axle.
    for y in (1.62, HALF_BASE + 0.16, HALF_BASE - 0.16, 0.45, -0.45, -HALF_BASE + 0.16, -HALF_BASE - 0.16, -1.62):
        out.append(tube("cross", [(-0.52, y, 0.60), (0.52, y, 0.60)], 0.024, ALU_DARK, 3, cuts=0))
    for sy in (1, -1):
        for sx in (1, -1):
            y = sy * HALF_BASE
            # The pivot blocks the arms swing on, front and back of each corner, and the shock tower above them.
            for dy in (-0.17, 0.17):
                out.append(metal(oa.box("pivot", (sx * (ARM_PIVOT_X + 0.02), y + dy, ARM_PIVOT_Z), (0.10, 0.06, 0.30), CHARCOAL, bevel=0.012), 5, wear=0.3, smooth=0.3, bare=0.2))
            out.append(tube("tower", [(sx * 0.50, y - 0.18, 0.62), (sx * 0.40, y, 0.98), (sx * 0.50, y + 0.18, 0.62)], 0.022, ALU_DARK, 6, cuts=2))
    # The roll cage: a hoop behind the seats, two bars over the heads to the front corners, a brace behind.
    hoop = [(0.52, -0.78, 0.62), (0.54, -0.80, 1.30), (0.48, -0.82, 1.80), (0.22, -0.82, 1.94), (-0.22, -0.82, 1.94), (-0.48, -0.82, 1.80), (-0.54, -0.80, 1.30), (-0.52, -0.78, 0.62)]
    out.append(tube("hoop", hoop, 0.034, ALU, 8))
    for s in (1, -1):
        bar = [(s * 0.48, -0.80, 1.84), (s * 0.58, -0.30, 1.86), (s * 0.68, 0.30, 1.56), (s * 0.66, 0.84, 1.02), (s * 0.56, 1.10, 0.76)]
        out.append(tube("roof bar", bar, 0.024, ALU, 9 + s))
        out.append(tube("brace", [(s * 0.40, -0.82, 1.70), (s * 0.30, -1.45, 0.98), (s * 0.30, -1.62, 0.66)], 0.024, ALU_DARK, 11 + s))
        # A grab handle on the cage beside each seat, for climbing in.
        out.append(tube("grab", [(s * 0.55, -0.62, 1.16), (s * 0.62, -0.40, 1.20), (s * 0.58, -0.18, 1.14)], 0.016, ORANGE, 13 + s))
    out.append(tube("cage top", [(-0.40, -0.80, 1.88), (0.40, -0.80, 1.88)], 0.024, ALU, 14, cuts=0))
    out.append(tube("dash bar", [(-0.66, 0.86, 1.00), (-0.20, 0.90, 0.98), (0.20, 0.90, 0.98), (0.66, 0.86, 1.00)], 0.022, ALU, 15))
    return out


def floor_and_deck():
    out = []
    # The floor pan the seats stand on: thin white composite, ribbed underneath.
    out.append(painted(oa.box("floor", (0, -0.15, 0.645), (1.10, 1.95, 0.035), WHITE, bevel=0.012), 20))
    out.append(matte(oa.box("skid", (0, 0.0, 0.575), (0.95, 2.6, 0.03), CHARCOAL, bevel=0.01), 21, smooth=0.25))
    for x in (-0.33, 0.0, 0.33):
        out.append(metal(oa.box("rib", (x, 0.0, 0.60), (0.04, 2.5, 0.04), ALU_DARK, bevel=0.008), 22, wear=0.4))
    # Low side pods between the wheels (battery packs, like the real rover's), white with a dark sill.
    for sx in (1, -1):
        pod = oa.slab("side pod", [(-0.86, 0.62), (0.86, 0.62), (0.96, 0.70), (0.86, 0.84), (-0.80, 0.86), (-0.92, 0.74)], 0.10, WHITE, bevel=0.025, segments=3, x=sx * 0.60)
        out.append(painted(pod, 70 + sx))
        out.append(matte(oa.box("sill", (sx * 0.655, 0.0, 0.64), (0.02, 1.70, 0.035), CHARCOAL, bevel=0.006), 72, smooth=0.25))
        out.append(foil(oa.box("pod foil", (sx * 0.652, -0.25, 0.76), (0.012, 0.70, 0.11), GOLD, bevel=0.004), 73 + sx))
    # Foot rests and a kick plate in front of the seats.
    out.append(metal(oa.box("kick", (0, 0.66, 0.74), (1.02, 0.03, 0.16), ALU_DARK, bevel=0.01, rot=(18, 0, 0)), 23, wear=0.7))
    # The rear deck: a pallet over the tail with a tool box, two air bottles and a sample rack.
    out.append(painted(oa.box("deck", (0, -1.42, 0.70), (1.02, 0.86, 0.04), WHITE_SHADE, bevel=0.012), 24))
    box = oa.box("tool box", (-0.24, -1.36, 0.86), (0.46, 0.50, 0.28), WHITE, bevel=0.03)
    out.append(painted(box, 25))
    out.append(matte(oa.box("box lid strip", (-0.24, -1.36, 1.005), (0.47, 0.06, 0.012), ORANGE, bevel=0.004), 26, smooth=0.2))
    for x in (0.18, 0.40):
        bottle = lathe("air bottle", (x, -1.12, 0.78), (0, -1, 0), [(0.0, 0.0), (0.0, 0.07), (0.02, 0.095), (0.46, 0.095), (0.52, 0.06), (0.56, 0.025), (0.62, 0.02)], WHITE, segments=18)
        out.append(painted(bottle, 27, smooth=0.5))
        for y in (-1.22, -1.48):
            out.append(matte(real.sweep("strap", [(x - 0.11, y, 0.72), (x - 0.10, y, 0.86), (x, y, 0.885), (x + 0.10, y, 0.86), (x + 0.11, y, 0.72)], 0.012, CHARCOAL, n=6, flat=0.3, cuts=2), 28))
    for y in (-1.70, -1.78):
        out.append(tube("rack", [(-0.46, y, 0.70), (-0.46, y, 0.98), (0.46, y, 0.98), (0.46, y, 0.70)], 0.012, ALU_DARK, 29, cuts=0))
    return out


def nose():
    out = []
    # The electronics box over the front axle: a tapered white housing, a mirror radiator on top (kept dusty),
    # gold-foil blankets down its flanks, and a dark bumper bar to hit people with.
    outline = [(1.04, 0.64), (1.84, 0.70), (1.88, 0.80), (1.84, 0.96), (1.40, 1.00), (1.04, 0.98)]
    body = oa.slab("nose box", outline, 0.98, WHITE, bevel=0.03, segments=3)
    out.append(painted(body, 30))
    out.append(metal(oa.box("radiator", (0, 1.38, 1.005), (0.70, 0.56, 0.012), (0.78, 0.80, 0.82), bevel=0.004, rot=(-2, 0, 0)), 31, wear=0.2, smooth=0.85, bare=1.0))
    out.append(matte(oa.box("nose stripe", (0, 1.865, 0.93), (0.90, 0.012, 0.035), ORANGE, bevel=0.004), 34, smooth=0.25))
    for i in range(6):
        out.append(metal(oa.box("fin", (0, 1.15 + i * 0.09, 1.012), (0.66, 0.008, 0.008), ALU_DARK, bevel=0.002), 32, wear=0.1))
    for s in (1, -1):
        out.append(foil(oa.box("blanket", (s * 0.495, 1.40, 0.83), (0.03, 0.66, 0.26), GOLD if s > 0 else GOLD_DARK, bevel=0.012), 33 + s))
    bumper = [(-0.58, 1.86, 0.72), (-0.50, 1.98, 0.70), (0.0, 2.02, 0.70), (0.50, 1.98, 0.70), (0.58, 1.86, 0.72)]
    out.append(matte(real.sweep("bumper", bumper, 0.045, CHARCOAL, n=10, cuts=3), 35, smooth=0.25))
    for s in (1, -1):
        out.append(tube("bumper strut", [(s * 0.40, 1.99, 0.70), (s * 0.44, 1.80, 0.66)], 0.022, ALU_DARK, 36, cuts=0))
        # Headlamp housings either side of the middle of the nose face.
        out.append(matte(oa.cylinder("lamp housing", (s * 0.30, 1.78, LAMP[2]), (s * 0.30, 1.90, LAMP[2]), 0.075, CHARCOAL, segments=18, bevel=0.012), 37, smooth=0.4))
    # The driver's display and the hand controller pedestal between the seats.
    out.append(matte(oa.box("display", (-0.30, 0.80, 1.02), (0.30, 0.05, 0.18), CHARCOAL, bevel=0.015, rot=(-28, 0, 0)), 38, smooth=0.35))
    out.append(tube("display stalk", [(-0.30, 0.84, 0.93), (-0.25, 0.88, 0.97)], 0.016, ALU_DARK, 39, cuts=0))
    out.append(matte(oa.box("pedestal", (0, -0.05, 0.80), (0.16, 0.34, 0.28), CHARCOAL, bevel=0.025), 40, smooth=0.3))
    out.append(tube("stick", [(0, 0.02, 0.94), (0, 0.04, 1.10)], 0.014, ALU, 41, cuts=0))
    out.append(matte(oa.cylinder("t handle", (-0.07, 0.04, 1.11), (0.07, 0.04, 1.11), 0.018, RUBBER, segments=10), 42))
    # Switch strip and dials on the display's bezel (paint, not shapes).
    return out


def seats():
    out = []
    for s in (-1, 1):
        x = s * 0.36
        # Tube frame, then woven webbing stretched across it, like the real rover's folding seats.
        out.append(tube("seat frame", [(x - 0.21, -0.02, 0.86), (x - 0.21, -0.56, 0.84), (x - 0.21, -0.72, 1.44)], 0.018, ALU, 50))
        out.append(tube("seat frame", [(x + 0.21, -0.02, 0.86), (x + 0.21, -0.56, 0.84), (x + 0.21, -0.72, 1.44)], 0.018, ALU, 51))
        out.append(tube("seat leg", [(x - 0.21, -0.04, 0.86), (x - 0.21, -0.04, 0.66)], 0.016, ALU_DARK, 52, cuts=0))
        out.append(tube("seat leg", [(x + 0.21, -0.04, 0.86), (x + 0.21, -0.04, 0.66)], 0.016, ALU_DARK, 52, cuts=0))
        pan = oa.slab("seat", [(-0.02, 0.85), (-0.55, 0.83), (-0.58, 0.86), (-0.74, 1.43), (-0.70, 1.45), (-0.53, 0.89), (-0.02, 0.89)], 0.40, WEBBING, bevel=0.01, segments=2, x=x)
        oa.densify(pan, 0.05)
        oa.paint(pan, wear=0.2, dirt=0.8, mottle=0.7, seed=53 + s, grain=5.0)
        real.stripes(pan, (1, 0, 0), 0.045, WEBBING_DARK, share=0.35, strength=0.6)
        real.made_of(pan, 0.06)
        out.append(pan)
        # Lap belt, hanging loose across the seat.
        out.append(matte(real.sweep("belt", [(x - 0.20, -0.48, 0.90), (x - 0.08, -0.36, 0.91), (x + 0.08, -0.36, 0.91), (x + 0.20, -0.48, 0.90)], 0.022, ORANGE, n=6, flat=0.18, cuts=2), 54))
    return out


def fenders():
    out = []
    for sy in (1, -1):
        for sx in (1, -1):
            cx, cy, cz = sx * HALF_TRACK, sy * HALF_BASE, WHEEL_RADIUS
            r = WHEEL_RADIUS + 0.19
            # An arc over the top of the wheel, open underneath, wide enough for the wheel to steer under it.
            pts = []
            for i in range(9):
                a = math.radians(38 + i * 13.0)
                pts.append((cx, cy + r * math.cos(a), cz + r * math.sin(a)))
            out_of = lambda q, cy=cy, cz=cz: (0.0, q[1] - cy, q[2] - cz)
            f = real.sweep("fender", pts, WHEEL_WIDTH * 0.62, FENDER, n=10, up=out_of, flat=0.045, power=4.0)
            out.append(painted(f, 60 + sx + 2 * sy, smooth=0.35))
            out.append(matte(real.sweep("fender edge", [(p[0] + 0.0, p[1], p[2]) for p in pts], 0.012, ORANGE, n=6, cuts=1), 66, smooth=0.3))
            # A strut from the frame to the fender.
            out.append(tube("fender strut", [(sx * 0.50, cy, 0.95), (sx * (HALF_TRACK - 0.02), cy, cz + r - 0.02)], 0.016, ALU_DARK, 64, cuts=0))
    return out


def mast_and_boost():
    out = []
    # The dish on its mast at the back right, and a helix antenna at the back left: out of the driver's way.
    out.append(tube("mast", [(0.42, -1.62, 0.72), (0.42, -1.62, 1.70)], 0.02, ALU, 70, cuts=0))
    dish = lathe("dish", (0.42, -1.58, 1.78), (0, 0.6, 0.8), [(0.0, 0.0), (0.0, 0.05), (0.07, 0.28), (0.09, 0.28), (0.09, 0.0)], (0.80, 0.80, 0.78), segments=24)
    out.append(painted(dish, 71, smooth=0.25))
    out.append(tube("feed", [(0.42, -1.55, 1.82), (0.42, -1.40, 2.02)], 0.008, ALU_DARK, 72, cuts=0))
    helix = []
    for i in range(41):
        a = i * math.pi / 5
        helix.append((-0.42 + 0.035 * math.cos(a), -1.62 + 0.035 * math.sin(a), 0.98 + i * 0.012))
    out.append(metal(real.sweep("helix", helix, 0.006, ALU, n=5), 73))
    out.append(tube("helix mast", [(-0.42, -1.62, 0.70), (-0.42, -1.62, 0.98)], 0.014, ALU_DARK, 74, cuts=0))
    # The boost pack under the deck: a tank across the tail and two bell nozzles facing back.
    tank = lathe("boost tank", (-0.44, -1.66, 0.58), (1, 0, 0), [(0.0, 0.0), (0.0, 0.08), (0.03, 0.11), (0.85, 0.11), (0.88, 0.08), (0.88, 0.0)], WHITE_SHADE, segments=20)
    out.append(painted(tank, 75, smooth=0.45))
    for s in (1, -1):
        x = s * BOOST[0]
        bell = lathe("nozzle", (x, BOOST[1] + 0.26, BOOST[2]), (0, -1, 0), [(0.0, 0.05), (0.06, 0.055), (0.10, 0.045), (0.16, 0.06), (0.22, 0.085), (0.26, 0.10), (0.26, 0.085), (0.20, 0.07), (0.12, 0.04)], CHARCOAL, segments=20)
        out.append(metal(bell, 76 + s, wear=0.3, smooth=0.35, bare=0.6))
        out.append(tube("feed line", [(x, -1.66, 0.66), (x, -1.62, 0.80), (x, BOOST[1] + 0.27, BOOST[2])], 0.014, GOLD_DARK, 78, cuts=2))
    return out


# ------------------------------------------------------------------ the moving pieces

def wheel():
    """One wheel round its hub, axle along X, the outside face toward +X (the game turns the left ones round)."""
    parts = []
    half = WHEEL_WIDTH / 2
    # The woven wire tyre: a rounded drum, dark zinc grey, with the weave painted as fine bands.
    tyre = revolve("tyre", [(-half, 0.30), (-half, 0.40), (-half + 0.025, 0.44), (-half + 0.07, 0.455), (0.0, WHEEL_RADIUS - 0.012),
                            (half - 0.07, 0.455), (half - 0.025, 0.44), (half, 0.40), (half, 0.30), (half - 0.03, 0.295), (-half + 0.03, 0.295)], MESH)
    oa.densify(tyre, 0.05)
    oa.crumple(tyre, 0.004, grain=14.0, seed=80)
    oa.paint(tyre, wear=0.2, dirt=0.4, mottle=0.9, seed=80, grain=12.0)
    real.stripes(tyre, (1, 0, 0), 0.022, (0.20, 0.20, 0.21), share=0.4, strength=0.55)
    real.made_of(tyre, 0.30, 0.6)
    parts.append(tyre)
    # Titanium chevrons over half the tread, pointing the way it rolls.
    count = 22
    for i in range(count):
        a = 2 * math.pi * i / count
        for side in (-1, 1):
            c = oa.box("chevron", (0, 0, 0), (0.12, 0.05, 0.014), CHEVRON, bevel=0.004, segments=1)
            m = (Matrix.Rotation(a, 4, 'X') @ Matrix.Translation((side * 0.055, 0, WHEEL_RADIUS - 0.002)) @ Matrix.Rotation(math.radians(-side * 32), 4, 'Z'))
            c.data.transform(m)
            parts.append(metal(c, 81 + i, wear=0.8, smooth=0.5, bare=0.9))
    # The rim and bump-stop frame inside the mesh, the hub, and the drive motor on the inboard side.
    rim = revolve("rim", [(half - 0.005, 0.255), (half - 0.005, 0.30), (half - 0.03, 0.305), (half - 0.03, 0.25)], ALU_DARK, segments=40)
    stop = revolve("bump stop", [(-0.10, 0.285), (0.10, 0.285), (0.10, 0.27), (-0.10, 0.27)], CHARCOAL, segments=32)
    parts.append(metal(stop, 87, wear=0.2, smooth=0.3, bare=0.3))
    parts.append(metal(rim, 82, wear=0.5))
    for i in range(6):
        a = 2 * math.pi * i / 6 + 0.26
        parts.append(metal(oa.cylinder("spoke", (half - 0.02, 0.10 * math.cos(a), 0.10 * math.sin(a)), (half - 0.02, 0.27 * math.cos(a), 0.27 * math.sin(a)), 0.016, ALU, segments=8), 83, wear=0.6))
    hub = lathe("hub", (half - 0.01, 0, 0), (1, 0, 0), [(0.0, 0.0), (0.0, 0.11), (0.03, 0.105), (0.05, 0.07), (0.055, 0.0)], ALU, segments=24)
    parts.append(metal(hub, 84, wear=0.5, smooth=0.7))
    motor = lathe("motor", (-half + 0.01, 0, 0), (-1, 0, 0), [(0.0, 0.0), (0.0, 0.16), (0.06, 0.155), (0.08, 0.12), (0.09, 0.0)], CHARCOAL, segments=24)
    parts.append(metal(motor, 85, wear=0.3, smooth=0.35, bare=0.3))
    parts.append(foil(oa.box("motor blanket", (-half - 0.035, 0, 0.13), (0.06, 0.14, 0.05), GOLD, bevel=0.01), 86))
    return parts


def arm():
    """The right front corner's double wishbones and upright, round the arms' inner pivot (origin)."""
    parts = []
    dz = WHEEL_RADIUS - ARM_PIVOT_Z      # the hub sits this far above (+) or below the pivot at rest
    out_x = ARM_LENGTH
    for z, spread, r in ((-0.10, 0.17, 0.022), (0.10, 0.13, 0.019)):
        for s in (1, -1):
            parts.append(tube("wishbone", [(0.02, s * spread, z), (out_x - 0.04, 0.0, z + dz * 0.4)], r, ALU, 90, cuts=0))
        parts.append(metal(oa.sphere("ball joint", (out_x - 0.03, 0.0, z + dz * 0.4), 0.03, CHARCOAL, segments=10), 91, smooth=0.5, bare=0.5))
    parts.append(metal(oa.box("upright", (out_x, 0.0, dz * 0.4), (0.05, 0.09, 0.30), ALU_DARK, bevel=0.012), 92, wear=0.5))
    # A coil-over from the lower arm up to the tower on the frame.
    parts.append(metal(oa.cylinder("shock body", (0.30, 0.0, -0.06), (0.15, 0.0, 0.26), 0.032, CHARCOAL, segments=12), 93, smooth=0.6, bare=0.5))
    spring = []
    for i in range(37):
        t = i / 36
        a = t * 2 * math.pi * 7
        p = Vector((0.30, 0.0, -0.06)).lerp(Vector((0.17, 0.0, 0.22)), t)
        axis = Vector((-0.15, 0, 0.32)).normalized()
        u = Vector((0, 1, 0))
        v = axis.cross(u)
        spring.append(tuple(p + u * (0.05 * math.cos(a)) + v * (0.05 * math.sin(a))))
    parts.append(matte(real.sweep("spring", spring, 0.009, ORANGE, n=6), 94, smooth=0.4))
    return parts


def glows():
    lamps, screen, boost = [], [], []
    for s in (1, -1):
        lamps.append(oa.cylinder("lens", (s * 0.30, 1.895, LAMP[2]), (s * 0.30, 1.905, LAMP[2]), 0.06, LENS, segments=18))
    screen.append(oa.box("screen", (-0.30, 0.772, 1.032), (0.26, 0.006, 0.14), SCREEN, bevel=0.002, rot=(-28, 0, 0)))
    for s in (1, -1):
        boost.append(oa.cylinder("throat", (s * BOOST[0], BOOST[1] + 0.20, BOOST[2]), (s * BOOST[0], BOOST[1] + 0.12, BOOST[2]), 0.05, THROAT, radius_end=0.075, segments=16))
    for o in lamps + screen + boost:
        oa.paint(o, wear=0, dirt=0, mottle=0.1, gradient=0.05)
    return lamps, screen, boost


def rover():
    body = frame() + floor_and_deck() + nose() + seats() + fenders() + mast_and_boost()
    # Moon dust settles low and on top; the crowd shadow darkens where things stand close.
    real.crowd_shadow(body, strength=0.45, reach=0.16)
    real.dust(body, DUST, top=1.1, amount=0.45, on_top=0.18, seed=3)
    w = wheel()
    real.dust(w, DUST, top=0.9, amount=0.35, on_top=0.1, seed=4)
    a = arm()
    lamps, screen, boost = glows()
    pieces = [("body", body), ("wheel", w), ("arm", a), ("lampGlow", lamps), ("screenGlow", screen), ("boostGlow", boost)]
    sockets = []
    for name, sx, sy in (("FL", -1, 1), ("FR", 1, 1), ("RL", -1, -1), ("RR", 1, -1)):
        sockets.append(oa.socket("Wheel" + name, (sx * HALF_TRACK, sy * HALF_BASE, WHEEL_RADIUS)))
        sockets.append(oa.socket("Arm" + name, (sx * ARM_PIVOT_X, sy * HALF_BASE, ARM_PIVOT_Z)))
    sockets.append(oa.socket("Eye", EYE))
    sockets.append(oa.socket("Exit", EXIT))
    sockets.append(oa.socket("BoostR", BOOST))
    sockets.append(oa.socket("BoostL", (-BOOST[0], BOOST[1], BOOST[2])))
    sockets.append(oa.socket("Lamp", LAMP))
    return pieces, sockets


# ------------------------------------------------------------------ the davit on the lander

def G(x, y, z):
    """A ship-space point (x right, y up, z nose) in Blender's axes."""
    return Vector((x, z, y))


def davit():
    parts = []
    sx0, sy0, sz0 = STOW
    hang_y = SHIP_BELLY_Y + 0.65            # the beams leave the base's back face just above the belly
    tip_z = -3.40                            # the winches sit over the rover's roll cage (MoonRover.PoseCables)
    for s in (1, -1):
        x = s * 0.62
        root = G(x, hang_y, -SHIP_BASE_INRADIUS + 0.05)
        beam = real.sweep("beam", [root, G(x, hang_y + 0.04, -2.9), G(x, hang_y + 0.02, tip_z - 0.35)], 0.06, ALU_DARK, n=8, power=3.0, cuts=2)
        parts.append(metal(beam, 100 + s, wear=0.5, smooth=0.45, bare=0.7))
        parts.append(tube("brace", [G(x, SHIP_BELLY_Y + 0.08, -SHIP_BASE_INRADIUS + 0.05), G(x, hang_y - 0.02, -3.3)], 0.035, ALU_DARK, 102, cuts=0))
        # A winch drum at each beam tip and a hook block hanging to where the rover's cage top sits.
        drum = lathe("winch", G(x - 0.12, hang_y - 0.08, tip_z), (1, 0, 0), [(0.0, 0.0), (0.0, 0.11), (0.03, 0.08), (0.21, 0.08), (0.24, 0.11), (0.24, 0.0)], CHARCOAL, segments=18)
        parts.append(metal(drum, 103, wear=0.3, smooth=0.4, bare=0.4))
        parts.append(matte(oa.box("winch motor", tuple(G(x, hang_y + 0.06, tip_z + 0.2)), (0.16, 0.22, 0.14), WHITE, bevel=0.02), 104, smooth=0.3))
        parts.append(foil(oa.box("winch blanket", tuple(G(x, hang_y + 0.06, tip_z + 0.2 + 0.0)), (0.17, 0.10, 0.12), GOLD), 105))
    parts.append(tube("davit cross", [G(-0.62, hang_y + 0.02, tip_z - 0.3), G(0.62, hang_y + 0.02, tip_z - 0.3)], 0.04, ALU_DARK, 106, cuts=0))
    real.crowd_shadow(parts, strength=0.4, reach=0.14)
    sockets = [oa.socket("Stow", G(*STOW))]
    return [("davit", parts)], sockets


# ------------------------------------------------------------------ preview

def placed(objects, matrix):
    out = []
    for o in objects:
        c = o.copy()
        c.data = o.data.copy()
        c.data.transform(matrix)
        bpy.context.scene.collection.objects.link(c)
        out.append(c)
    return out


def assembled(pieces):
    """The pieces put together as the game puts them, at rest, for pictures."""
    by = dict(pieces)
    shown = list(by["body"]) + list(by["lampGlow"]) + list(by["screenGlow"]) + list(by["boostGlow"])
    for sx, sy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
        turn = Matrix.Rotation(0 if sx > 0 else math.pi, 4, 'Z')
        shown += placed(by["wheel"], Matrix.Translation((sx * HALF_TRACK, sy * HALF_BASE, WHEEL_RADIUS)) @ turn)
        shown += placed(by["arm"], Matrix.Translation((sx * ARM_PIVOT_X, sy * HALF_BASE, ARM_PIVOT_Z)) @ turn)
    for o in by["wheel"] + by["arm"]:
        o.hide_render = True
    return shown


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    previews = args[args.index("--preview") + 1] if "--preview" in args else None
    oa.reset_scene()
    pieces, sockets = rover()
    oa.export(os.path.join(OUT, "Rover.bytes"), pieces, sockets)
    davit_pieces, davit_sockets = davit()
    oa.export(os.path.join(OUT, "RoverDavit.bytes"), davit_pieces, davit_sockets)
    if previews:
        glow = [o for n, objs in pieces if n.endswith("Glow") for o in objs]
        shown = assembled(pieces)
        floor = oa.box("ground", (0, 0, -0.01), (300, 300, 0.02), (0.42, 0.41, 0.40), bevel=0)
        oa.paint(floor, wear=0, dirt=0, mottle=0, gradient=0)
        shown.append(floor)
        moon = dict(world=(0.02, 0.02, 0.03), sky=0.35, sun_energy=4.5)
        oa.render_preview(os.path.join(previews, "rover_three_quarter.png"), shown, (4.6, 5.4, 1.9), (0, 0.1, 0.75), lens=36, size=(1280, 800), samples=64, glow=glow, **moon)
        oa.render_preview(os.path.join(previews, "rover_rear.png"), shown, (-4.4, -5.2, 2.3), (0, -0.2, 0.75), lens=36, size=(1280, 800), samples=64, glow=glow, **moon)
        oa.render_preview(os.path.join(previews, "rover_seat.png"), shown, (EYE[0], EYE[1], EYE[2]), (EYE[0] + 0.05, EYE[1] + 3.0, EYE[2] - 0.55), lens=20, size=(1280, 800), samples=64, glow=glow, **moon)
        oa.render_preview(os.path.join(previews, "rover_side.png"), shown, (6.5, 0.0, 0.9), (0, 0, 0.8), lens=40, size=(1280, 800), samples=64, glow=glow, **moon)
