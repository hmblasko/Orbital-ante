"""Builds the cockpit console: the sculpted dash the screen sits in, with its real instruments.

    blender -b -P tools/blender/build_console.py
    blender -b -P tools/blender/build_console.py -- --preview <folder>

The console stands on the cabin's desk across the nose. Its middle frames the dash screen (CockpitDash's panel);
a wing either side carries the instruments, so the ship's numbers are hardware you look at, not words.

Pieces:
    console            everything that never moves (the bake tool lights it with the cabin's lamps)
    fuelLevelGlow      the liquid in the fuel tube, modelled full: the game shrinks it toward socket FuelBase
    airNeedleGlow      the air dial's needle, modelled at empty: the game turns it about socket AirDial
    lampLockGlow, lampAutoGlow, lampMatchGlow, lampFuelGlow, lampAirGlow
                       the lit faces of the five picture lamps: the game shows or hides each
Sockets:
    Dash               the middle of the screen the console frames
    FuelBase, FuelTop  the empty and full ends of the fuel tube
    AirDial            the needle's pivot; AirDialOut is 0.1 m out of the dial's face from it
    AirEmpty, AirHalf, AirFull   where the needle's tip points at each reading

Ship space in the game: origin in the middle of the cabin floor, Y up, Z nose, X the pilot's right.
Here in Blender that is X right, Y nose, Z up, so a game point (x, y, z) is written G(x, y, z).
"""
import bpy, bmesh, sys, os, math
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import oa_blender as oa
import build_ship as ship
from build_ship import G, rod, metal, painted, turned, swept_frame

OUT = ship.OUT

# Not from ShipLayout: the cockpit's desk panel (CockpitConfig.deskPosition, deskWidth, deskLean; the panel
# is 1000 x 300 units, so its height is 0.3 of its width). The game reads the place from the socket Dash.
DASH_CENTRE, DASH_WIDTH, DASH_HEIGHT, DASH_LEAN = (0.0, 0.9, 1.94), 1.1, 0.33, 24.0
DASH_FRAME = 0.046                       # how far the bezel reaches past the screen on every side
DASH_PROUD = 0.042                       # how far the bezel stands out towards the pilot
DASH_BROW, DASH_BROW_PROUD = 0.018, 0.022  # along the top both are less: nothing may stand above DASH_TOP,
DASH_TOP = 1.08                          # so the pilot (eye at 1.18) sees the front window over the dash
WING_TOP = 0.84                          # and nothing outside CHEEK may stand above this
CHEEK = 0.70
CABIN_RADIUS = 2.5

SHELL = (0.86, 0.79, 0.64)               # warm enamel, a shade deeper than the cabin wall
PANEL = (0.15, 0.19, 0.21)               # the dark instrument plates
FACE = (0.06, 0.07, 0.08)
BRASS = (0.74, 0.56, 0.24)
LEATHER = (0.50, 0.27, 0.15)
CREAM, ORANGE, CHARCOAL, DARK_METAL, SILVER = ship.CREAM, ship.ORANGE, ship.CHARCOAL, ship.DARK_METAL, ship.SILVER
RED = (0.80, 0.16, 0.12)
AMBER = (1.0, 0.62, 0.12)
NEEDLE = (1.0, 0.86, 0.58)
LAMPS = (("Lock", (0.35, 0.85, 1.0)), ("Auto", (0.45, 1.0, 0.55)), ("Match", (1.0, 0.76, 0.25)), ("Fuel", (1.0, 0.30, 0.18)), ("Air", (1.0, 0.30, 0.18)))

WING_END = 1.36
FACE_DEPTH, FACE_RISE = 0.218, 0.121     # the wing's sloping face, nose-ward and up
WING_MIDDLE = 1.0                        # where across each wing the instruments are centred


def plain(o):
    oa.paint(o, wear=0, dirt=0, mottle=0, gradient=0)
    return o


def wing_front(ax):
    """How far forward the console's front edge is, at |x| = ax: it wraps a little round the pilot."""
    return 1.862 - 0.0625 * max(0.0, ax - 0.6)


def section(ax):
    """The console's cross-section at |x| = ax, as (nose, up) points going round once."""
    lean = math.radians(DASH_LEAN)
    c, s = math.cos(lean), math.sin(lean)
    hh, f = DASH_HEIGHT / 2, DASH_FRAME
    mid = (DASH_CENTRE[2] + 0.014 * c, DASH_CENTRE[1] - 0.014 * s)
    reach, rise = hh + f, hh + DASH_BROW - 0.004
    low, high = (mid[0] - reach * s, mid[1] - reach * c), (mid[0] + rise * s, mid[1] + rise * c)
    centre = [(1.862, 0.655), (1.862, 0.692), low, high, (high[0] + 0.016, high[1] + 0.014), (high[0] + 0.05, DASH_TOP - 0.004),
              (high[0] + 0.09, high[1] - 0.004), (2.27, 0.96), (2.31, 0.90), (2.33, 0.70)]
    zf = wing_front(ax)
    wall = math.sqrt(max(0.01, CABIN_RADIUS ** 2 - ax * ax)) - 0.014
    zb = min(zf + 0.36, wall)
    d = min(FACE_DEPTH, zb - zf - 0.087)
    wing = [(zf, 0.655), (zf, 0.693), (zf + 0.012, 0.705), (zf + 0.012 + d, 0.705 + FACE_RISE), (zf + 0.025 + d, 0.832), (zf + 0.044 + d, 0.833),
            (zf + 0.062 + d, 0.826), (zb - 0.02, 0.80), (zb, 0.77), (zb, 0.70)]
    t = max(0.0, min(1.0, (ax - 0.612) / (CHEEK - 0.612)))
    w = t * t * (3 - 2 * t)
    return [(a[0] + (b[0] - a[0]) * w, a[1] + (b[1] - a[1]) * w) for a, b in zip(centre, wing)]


def loft(name, rings, colour):
    """A closed skin through rings of points (each going round once, all the same length), capped at both ends."""
    bm = bmesh.new()
    made = [[bm.verts.new(p) for p in ring] for ring in rings]
    n = len(made[0])
    for a, b in zip(made, made[1:]):
        for j in range(n):
            k = (j + 1) % n
            bm.faces.new((a[j], a[k], b[k], b[j]))
    bm.faces.new(made[0])
    bm.faces.new(list(reversed(made[-1])))
    return oa._new_object(name, bm, colour)


def hoop(name, origin, axis, profile, colour, segments=24):
    """Spins a closed profile of (distance along the axis, radius) points round an axis: a ring with a hole."""
    bm = bmesh.new()
    rings = [[bm.verts.new((radius * math.cos(2 * math.pi * i / segments), radius * math.sin(2 * math.pi * i / segments), along))
              for i in range(segments)] for along, radius in profile]
    for k in range(len(rings)):
        a, b = rings[k], rings[(k + 1) % len(rings)]
        for i in range(segments):
            j = (i + 1) % segments
            bm.faces.new((a[i], a[j], b[j], b[i]))
    obj = oa._new_object(name, bm, colour, 35.0)
    return oa._place(obj, origin, Vector((0, 0, 1)).rotation_difference(Vector(axis).normalized()))


def shell():
    stations = [0.0, 0.2, 0.4, 0.56, 0.612, 0.63, 0.648, 0.666, 0.684, CHEEK, 0.78, 0.9, 1.02, 1.14, 1.24, 1.30]
    xs = [-x for x in reversed(stations[1:])] + stations
    rings = []

    def ring(x, shrink=1.0, at=None):
        points = section(abs(at if at is not None else x))
        cz, cy = sum(p[0] for p in points) / len(points), 0.72
        return [G(x, cy + (p[1] - cy) * shrink, cz + (p[0] - cz) * shrink) for p in points]

    # Each end is rounded off: the section shrinks toward its own middle over the last few centimetres.
    ends = ((WING_END - 0.05, 0.97), (WING_END - 0.022, 0.86), (WING_END - 0.006, 0.66), (WING_END, 0.38))
    for x, k in reversed(ends):
        rings.append(ring(-x, k))
    for x in xs:
        rings.append(ring(x))
    for x, k in ends:
        rings.append(ring(x, k))
    o = loft("console shell", rings, SHELL)
    oa.densify(o, 0.16)
    return painted(o, 81)


def wing_frame(side):
    """The middle of a wing's sloping face, and which way is across it, up it and out of it (Blender space)."""
    ax = WING_MIDDLE
    zf = wing_front(ax)
    origin = G(side * ax, 0.705 + FACE_RISE / 2, zf + 0.012 + FACE_DEPTH / 2)
    up = G(0, FACE_RISE, FACE_DEPTH).normalized()
    across = G(1.0, 0.0, -0.0625 * side).normalized()
    out = across.cross(up).normalized()
    if out.z < 0:
        out = -out
    across = up.cross(-out).normalized()          # exactly square to the other two
    if across.x < 0:
        across = -across
    return origin, across, up, out


def on_wing(side, objects):
    """Moves pieces built facing the viewer (X right, Y away, Z up the face) onto a wing's face."""
    origin, across, up, out = wing_frame(side)
    turned(objects, origin, -out, up)
    return objects


def wing_point(side, x, z, proud=0.0):
    origin, across, up, out = wing_frame(side)
    # turned() takes right = forward x up; keep the same handedness here.
    forward = -out
    right = forward.cross(up).normalized()
    return origin + right * x + up * z + out * proud


def screw(x, z, seed):
    return metal(oa.cylinder("screw", (x, -0.0045, z), (x, -0.0075, z), 0.0065, SILVER, segments=6), seed, wear=0.2)


def plate(name, x0, x1, z0, z1, seed):
    """A dark instrument plate standing a little proud of the wing, with a screw at each corner."""
    parts = [metal(oa.box(name, ((x0 + x1) / 2, -0.002, (z0 + z1) / 2), (x1 - x0, 0.006, z1 - z0), PANEL, bevel=0.0025, segments=2), seed, wear=0.35)]
    for x in (x0 + 0.014, x1 - 0.014):
        for z in (z0 + 0.014, z1 - 0.014):
            parts.append(screw(x, z, seed + 1))
    return parts


def toggle(x, z, seed, up=True, cap=SILVER):
    parts = [metal(oa.cylinder("toggle collar", (x, -0.004, z), (x, -0.011, z), 0.011, DARK_METAL, segments=8), seed)]
    parts.append(metal(rod("toggle", (x, -0.008, z), (x, -0.034, z + (0.012 if up else -0.012)), 0.0045, cap, radius_end=0.0075, segments=6), seed + 1, wear=0.3))
    return parts


def left_wing():
    """Air and fuel. Returns (fixed pieces, {glow piece name: pieces}, {socket name: point})."""
    fixed, glow, sockets = [], {}, {}
    fixed += plate("gauge plate", -0.285, 0.265, -0.108, 0.108, 90)

    # ---- air: a round brass dial. Empty is low on the left, full low on the right, by way of the top.
    ax, az, r = 0.145, 0.0, 0.086
    bezel = [(0.0, r + 0.012), (0.010, r + 0.012), (0.017, r + 0.006), (0.017, r - 0.002), (0.008, r - 0.006), (0.0, r - 0.006)]
    fixed.append(metal(hoop("air bezel", (ax, -0.005, az), (0, -1, 0), bezel, BRASS, segments=28), 92, wear=0.5))
    fixed.append(plain(oa.cylinder("air face", (ax, -0.004, az), (ax, -0.008, az), r - 0.005, FACE, segments=28)))
    for k in range(13):
        t = k / 12.0
        a = math.radians(210 - 240 * t)
        long = k % 3 == 0
        inner, outer = r - (0.026 if long else 0.018), r - 0.009
        mx, mz = ax + math.cos(a) * (inner + outer) / 2, az + math.sin(a) * (inner + outer) / 2
        colour = RED if t < 0.2 else CREAM
        fixed.append(plain(oa.box("air tick", (mx, -0.0085, mz), (0.0045 if long else 0.003, 0.002, outer - inner), colour, bevel=0, rot=(0, -(math.degrees(a) - 90), 0))))
    # A blue band along the safe part of the scale, in short flat pieces.
    for k in range(12):
        a = math.radians(162 - 192 * (k + 0.5) / 12)
        fixed.append(plain(oa.box("air band", (ax + math.cos(a) * (r - 0.0065), -0.0085, az + math.sin(a) * (r - 0.0065)), (0.0225, 0.002, 0.0035), (0.40, 0.78, 0.95), bevel=0, rot=(0, -(math.degrees(a) - 90), 0))))
    for k in range(3):                                    # and a red one where the air is nearly gone
        a = math.radians(210 - 48 * (k + 0.5) / 3)
        fixed.append(plain(oa.box("air band", (ax + math.cos(a) * (r - 0.0065), -0.0085, az + math.sin(a) * (r - 0.0065)), (0.0225, 0.002, 0.0035), RED, bevel=0, rot=(0, -(math.degrees(a) - 90), 0))))
    # Three bubbles under the pivot: this is the air.
    for bx, bz, br in ((-0.012, -0.040, 0.008), (0.006, -0.050, 0.0055), (0.014, -0.036, 0.004)):
        fixed.append(plain(oa.cylinder("bubble", (ax + bx, -0.008, az + bz), (ax + bx, -0.0095, az + bz), br, (0.40, 0.78, 0.95), segments=10)))
    fixed.append(metal(oa.lathe("air hub", (ax, -0.008, az), (0, -1, 0), [(0.0, 0.014), (0.007, 0.013), (0.011, 0.008), (0.012, 0.0)], BRASS, segments=12), 93, wear=0.4))
    tip = r - 0.016
    a0 = math.radians(210)
    needle = oa.slab("air needle", [(-0.018, -0.006), (-0.018, 0.006), (tip, 0.002), (tip, -0.002)], 0.004, NEEDLE, bevel=0)
    # slab() thickens along X with the outline in (Y, Z): lay it flat on the dial, pointing at empty.
    needle.data.transform(Matrix(((0, math.cos(a0), -math.sin(a0), ax), (-1, 0, 0, -0.0125), (0, math.sin(a0), math.cos(a0), az), (0, 0, 0, 1))))
    glow["airNeedleGlow"] = [plain(needle)]
    sockets["AirDial"] = (ax, -0.0125, az)
    sockets["AirDialOut"] = (ax, -0.1125, az)
    for name, deg in (("AirEmpty", 210), ("AirHalf", 90), ("AirFull", -30)):
        sockets[name] = (ax + math.cos(math.radians(deg)) * tip, -0.0125, az + math.sin(math.radians(deg)) * tip)

    # ---- fuel: a tube of amber liquid lying along the wing, draining toward the pilot's side.
    x0, x1, fz, fr = -0.255, 0.015, 0.004, 0.025
    fixed.append(plain(oa.box("fuel bed", ((x0 + x1) / 2, -0.004, fz), (x1 - x0 + 0.02, 0.006, fr * 2 + 0.016), FACE, bevel=0.003, segments=2)))
    for x, flip in ((x0 - 0.012, 1), (x1 + 0.012, -1)):
        cap = [(0.0, fr + 0.006), (0.012, fr + 0.006), (0.02, fr + 0.003), (0.026, fr - 0.004), (0.026, 0.0)]
        fixed.append(metal(oa.lathe("fuel cap", (x + 0.012 * flip, -0.006 - fr, fz), (-flip, 0, 0), cap, BRASS, segments=14), 94, wear=0.5))
    for k in range(3):                                    # two thin brass hoops and one in the middle hold the glass
        x = x0 + (x1 - x0) * (0.25 + 0.25 * k)
        band = [(-0.004, fr + 0.004), (0.004, fr + 0.004), (0.004, fr + 0.0005), (-0.004, fr + 0.0005)]
        fixed.append(metal(hoop("fuel hoop", (x, -0.006 - fr, fz), (1, 0, 0), band, BRASS, segments=14), 95, wear=0.4))
    for k in range(11):                                   # the scale underneath: tenths, red from the "low" line (a fifth) down
        x = x0 + (x1 - x0) * k / 10.0
        long = k % 5 == 0 or k == 8
        fixed.append(plain(oa.box("fuel tick", (x, -0.0075, fz - fr - (0.019 if long else 0.016)), (0.0035, 0.002, 0.014 if long else 0.008), RED if k >= 8 else CREAM, bevel=0)))
    # A drop at the full end: this is the fuel.
    drop = [(-0.011, 0.0), (-0.008, 0.007), (0.0, 0.010), (0.007, 0.007), (0.017, 0.0), (0.007, -0.007), (0.0, -0.010), (-0.008, -0.007)]
    d = oa.slab("fuel drop", drop, 0.002, AMBER, bevel=0)
    d.data.transform(Matrix(((0, 0, 1, x0 + 0.012), (1, 0, 0, -0.008), (0, 1, 0, fz + fr + 0.026), (0, 0, 0, 1))))
    fixed.append(plain(d))
    liquid = oa.cylinder("fuel", (x1, -0.006 - fr, fz), (x0, -0.006 - fr, fz), fr - 0.004, AMBER, segments=14, bevel=0.006)
    glow["fuelLevelGlow"] = [plain(liquid)]
    sockets["FuelBase"] = (x1, -0.006 - fr, fz)           # empty: nearest the pilot's middle
    sockets["FuelTop"] = (x0, -0.006 - fr, fz)

    # ---- a row of switches under the tube
    caps = (SILVER, SILVER, RED, SILVER, ORANGE)
    for k, cap in enumerate(caps):
        fixed += toggle(x0 + 0.02 + 0.054 * k, -0.082, 100 + k * 2, up=k % 2 == 0, cap=cap)
    return fixed, glow, sockets


def lamp_picture(name):
    """Dark shapes that stand in front of a lamp's lit face. Built in a 50 mm square, facing the viewer."""
    y = -0.0128
    dark = (0.05, 0.05, 0.06)
    parts = []

    def bar(x, z, w, h, turn=0.0):
        parts.append(plain(oa.box("picture", (x, y, z), (w, 0.002, h), dark, bevel=0, rot=(0, turn, 0))))

    def dot(x, z, r):
        parts.append(plain(oa.cylinder("picture", (x, y + 0.001, z), (x, y - 0.001, z), r, dark, segments=10)))

    if name == "Lock":                                    # a gunsight
        ring = [(-0.001, 0.0125), (0.001, 0.0125), (0.001, 0.0095), (-0.001, 0.0095)]
        parts.append(plain(hoop("picture", (0, y, 0), (0, 1, 0), ring, dark, segments=16)))
        for a in range(4):
            bar(math.cos(a * math.pi / 2) * 0.016, math.sin(a * math.pi / 2) * 0.016, 0.003, 0.009, a * 90.0)
        dot(0, 0, 0.003)
    elif name == "Auto":                                  # the ship flying itself: an arrowhead with speed lines
        tri = oa.slab("picture", [(-0.011, -0.003), (0.0, 0.017), (0.011, -0.003), (0.0, 0.002)], 0.002, dark, bevel=0)
        tri.data.transform(Matrix(((0, 1, 0, 0), (-1, 0, 0, y), (0, 0, 1, 0), (0, 0, 0, 1))))
        parts.append(plain(tri))
        for x, h in ((-0.008, 0.008), (0.0, 0.011), (0.008, 0.008)):
            bar(x, -0.012, 0.003, h)
    elif name == "Match":                                 # two arrows the same length, side by side
        for z in (0.008, -0.008):
            bar(-0.003, z, 0.022, 0.004)
            head = oa.slab("picture", [(0.006, -0.007), (0.017, 0.0), (0.006, 0.007)], 0.002, dark, bevel=0)
            head.data.transform(Matrix(((0, 1, 0, 0), (-1, 0, 0, y), (0, 0, 1, z), (0, 0, 0, 1))))
            parts.append(plain(head))
    elif name == "Fuel":                                  # a drop
        drop = oa.slab("picture", [(0.0, 0.017), (0.008, 0.002), (0.010, -0.006), (0.006, -0.013), (0.0, -0.015), (-0.006, -0.013), (-0.010, -0.006), (-0.008, 0.002)], 0.002, dark, bevel=0)
        drop.data.transform(Matrix(((0, 1, 0, 0), (-1, 0, 0, y), (0, 0, 1, 0), (0, 0, 0, 1))))
        parts.append(plain(drop))
    else:                                                 # air: bubbles
        dot(-0.006, -0.006, 0.008)
        dot(0.008, 0.004, 0.0055)
        dot(-0.002, 0.012, 0.0035)
    return parts


def right_wing():
    """Picture lamps, the throttle and a guarded switch."""
    fixed, glow = [], {}
    fixed += plate("lamp plate", -0.285, 0.265, -0.108, 0.108, 120)
    size, gap = 0.052, 0.012
    x = -0.262 + size / 2
    for name, colour in LAMPS:
        z = 0.042
        fixed.append(metal(oa.box("lamp frame", (x, -0.006, z), (size + 0.008, 0.01, size + 0.008), BRASS, bevel=0.004, segments=2), 122, wear=0.5))
        fixed.append(plain(oa.box("lamp glass", (x, -0.0095, z), (size - 0.004, 0.004, size - 0.004), tuple(c * 0.16 for c in colour), bevel=0.002, segments=1)))
        picture = lamp_picture(name)
        for o in picture:
            o.data.transform(Matrix.Translation((x, 0, z)))
        fixed += picture
        lit = oa.box("lamp lit", (x, -0.0105, z), (size - 0.005, 0.003, size - 0.005), colour, bevel=0)
        glow["lamp%sGlow" % name] = [plain(lit)]
        x += size + gap

    # A throttle: a slotted quadrant with a chunky orange handle.
    tx = 0.215
    fixed.append(metal(oa.box("throttle gate", (tx, -0.007, 0.0), (0.05, 0.012, 0.17), DARK_METAL, bevel=0.006, segments=2), 124))
    fixed.append(plain(oa.box("throttle slot", (tx, -0.0135, 0.0), (0.012, 0.002, 0.13), FACE, bevel=0)))
    fixed.append(metal(rod("throttle arm", (tx, -0.010, -0.048), (tx, -0.046, -0.036), 0.0065, SILVER, segments=8), 125, wear=0.3))
    fixed.append(painted(oa.box("throttle grip", (tx, -0.051, -0.034), (0.05, 0.02, 0.024), ORANGE, bevel=0.009, segments=3), 126))
    for k in range(5):
        fixed.append(plain(oa.box("throttle mark", (tx + 0.031, -0.0135, -0.056 + 0.028 * k), (0.008, 0.002, 0.003), CREAM, bevel=0)))

    # Under the lamps: a guarded red switch, two knobs and a key.
    gx, gz = -0.236, -0.062
    fixed.append(plain(oa.box("guard plate", (gx, -0.005, gz), (0.05, 0.004, 0.06), (0.86, 0.72, 0.16), bevel=0.003, segments=1)))
    for k in range(3):
        fixed.append(plain(oa.box("guard stripe", (gx - 0.014 + 0.014 * k, -0.0075, gz), (0.005, 0.001, 0.072), CHARCOAL, bevel=0, rot=(0, 30, 0))))
    fixed += toggle(gx, gz - 0.004, 128, up=False, cap=RED)
    fixed.append(painted(oa.slab("guard", [(-0.006, -0.012), (-0.044, -0.002), (-0.048, 0.012), (-0.040, 0.03), (-0.006, 0.03)], 0.04, RED, bevel=0.004, segments=2, x=gx), 130))
    fixed[-1].data.transform(Matrix.Translation((0, 0, gz)))
    knob = [(0.0, 0.021), (0.005, 0.021), (0.008, 0.017), (0.022, 0.015), (0.025, 0.011), (0.025, 0.0)]
    for k, colour in enumerate((CREAM, ORANGE)):
        kx = -0.150 + 0.066 * k
        fixed.append(metal(oa.lathe("knob", (kx, -0.005, gz), (0, -1, 0), knob, colour, segments=12), 132 + k, wear=0.3))
        fixed.append(plain(oa.box("knob mark", (kx, -0.0305, gz + 0.008), (0.004, 0.002, 0.012), CHARCOAL, bevel=0)))
        for t in range(7):                                # a little scale round each knob
            a = math.radians(210 - 40 * t)
            fixed.append(plain(oa.box("knob tick", (kx + math.cos(a) * 0.031, -0.0055, gz + math.sin(a) * 0.031), (0.0025, 0.001, 0.006), CREAM, bevel=0, rot=(0, -(math.degrees(a) - 90), 0))))
    fixed.append(metal(oa.cylinder("key barrel", (0.02, -0.005, gz), (0.02, -0.014, gz), 0.014, BRASS, segments=12, bevel=0.002), 136, wear=0.5))
    fixed.append(plain(oa.box("key slot", (0.02, -0.0145, gz), (0.003, 0.001, 0.014), FACE, bevel=0)))
    fixed.append(metal(oa.box("speaker", (0.105, -0.005, gz), (0.07, 0.004, 0.05), DARK_METAL, bevel=0.003, segments=1), 137))
    for k in range(4):
        fixed.append(plain(oa.box("speaker slot", (0.105, -0.0075, gz - 0.0165 + 0.011 * k), (0.054, 0.001, 0.0045), FACE, bevel=0)))
    return fixed, glow, {}


def screen_surround():
    """The bezel the dash screen sits in. Built facing the viewer, then leaned back onto the console."""
    lean = math.radians(DASH_LEAN)
    c, s = math.cos(lean), math.sin(lean)
    hw, hh, f = DASH_WIDTH / 2, DASH_HEIGHT / 2, DASH_FRAME
    slim = (DASH_BROW / f, DASH_BROW_PROUD / DASH_PROUD)
    front = []
    liner = swept_frame("dash liner", [(0.0, 0.010), (0.0, -0.030), (0.012, -0.037)], CHARCOAL, hw, hh, top=slim)
    oa.paint(liner, wear=0.1, dirt=0.2, mottle=0.2, seed=61, gradient=0.0)
    front.append(liner)
    bezel = swept_frame("dash bezel", [(0.012, -0.037), (0.022, -DASH_PROUD), (0.032, -DASH_PROUD), (0.041, -0.033), (f, -0.016), (f, 0.03)], BRASS, hw, hh, top=slim)
    front.append(metal(bezel, 62, wear=0.5))
    back = oa.box("dash backing", (0, 0.009, 0), (DASH_WIDTH + 0.01, 0.002, DASH_HEIGHT + 0.01), (0.05, 0.055, 0.06), bevel=0)
    front.append(plain(back))
    front.append(painted(oa.box("brow stripe", (0, -DASH_BROW_PROUD - 0.0005, hh + DASH_BROW * 0.55), (DASH_WIDTH - 0.24, 0.003, 0.007), ORANGE, bevel=0), 69))
    turned(front, G(*DASH_CENTRE), Vector((0, c, -s)), Vector((0, s, c)))
    return front


def trim():
    """A padded leather roll along the whole front edge, and cooling slots down the back of the hood."""
    parts = []
    xs = [-WING_END + 0.06 + (2 * WING_END - 0.12) * k / 26.0 for k in range(27)]
    points = [G(x, 0.697, wing_front(abs(x)) - 0.006) for x in xs]
    for k, (a, b) in enumerate(zip(points, points[1:])):
        o = rod("hand roll", a, b, 0.017, LEATHER, segments=8)
        oa.paint(o, wear=0.25, dirt=0.4, mottle=0.9, seed=140, gradient=0.0, grain=5.0)
        parts.append(o)
    for end in (points[0], points[-1]):
        parts.append(metal(oa.sphere("roll end", end, 0.021, BRASS, segments=10), 141, wear=0.5))
    for k in range(1, 26, 4):                              # brass bands where the roll is strapped down
        m = (points[k] + points[k + 1]) / 2
        d = (points[k + 1] - points[k]).normalized()
        parts.append(metal(rod("roll band", m - d * 0.006, m + d * 0.006, 0.019, BRASS, segments=8), 142, wear=0.5))
    high = section(0.0)[3]
    for k in range(4):
        t = 0.2 + 0.2 * k
        z, y = high[0] + 0.09 + (2.27 - high[0] - 0.09) * t, high[1] - 0.004 + (0.96 - high[1] + 0.004) * t
        parts.append(metal(oa.box("dash vent", G(0, y + 0.004, z + 0.004), (0.7, 0.012, 0.014), CHARCOAL, bevel=0, rot=(-45, 0, 0)), 82))
    return parts


def console():
    fixed = [shell()] + screen_surround() + trim()
    glow, sockets = {}, {"Dash": G(*DASH_CENTRE)}
    for side, wing in ((-1, left_wing), (1, right_wing)):
        pieces, lit, places = wing()
        on_wing(side, pieces)
        fixed += pieces
        for name, objects in lit.items():
            on_wing(side, objects)
            glow[name] = objects
        for name, p in places.items():
            sockets[name] = wing_point(side, p[0], p[2], -p[1])
    parts = [("console", fixed)] + sorted(glow.items())
    return parts, [oa.socket(name, p) for name, p in sockets.items()]


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    previews = args[args.index("--preview") + 1] if "--preview" in args else None
    oa.reset_scene()
    pieces, sockets = console()
    oa.export(os.path.join(OUT, "Console.bytes"), pieces, sockets)
    if previews:
        shown = [o for _, objs in pieces for o in objs]
        desk = oa.slab("desk", [(1.75, 0.4), (1.75, 0.655), (1.82, 0.675), (2.49, 0.745), (2.49, 0.4)], 3.24, (0.36, 0.24, 0.16), bevel=0.02)
        wall = oa.box("wall", G(0, 1.0, 2.6), (6, 0.1, 3), (0.80, 0.72, 0.58), bevel=0)
        lean = math.radians(DASH_LEAN)
        screen = oa.box("screen", (0, 0, 0), (DASH_WIDTH, 0.002, DASH_HEIGHT), (0.03, 0.10, 0.09), bevel=0)
        turned([screen], G(*DASH_CENTRE), Vector((0, math.cos(lean), -math.sin(lean))), Vector((0, math.sin(lean), math.cos(lean))))
        for o in (desk, wall, screen):
            plain(o)
        shown += [desk, wall, screen]
        sun = (60, 0, 200)
        oa.render_preview(os.path.join(previews, "console_seat.png"), shown, G(0, 1.18, 1.28), G(0, 0.88, 1.94), lens=9, size=(1400, 700), samples=64, sun=sun)
        oa.render_preview(os.path.join(previews, "console_left.png"), shown, G(-0.45, 1.12, 1.45), G(-1.0, 0.76, 1.95), lens=30, size=(1100, 700), samples=64, sun=sun)
        oa.render_preview(os.path.join(previews, "console_right.png"), shown, G(0.45, 1.12, 1.45), G(1.0, 0.76, 1.95), lens=30, size=(1100, 700), samples=64, sun=sun)
        oa.render_preview(os.path.join(previews, "console_side.png"), shown, G(1.9, 1.5, 0.7), G(0.2, 0.8, 1.95), lens=30, size=(1100, 700), samples=64, sun=sun)
