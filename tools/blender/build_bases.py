"""Builds the bases that stand on the planets: five designs in three sizes.

    blender -b -P tools/blender/build_bases.py                        (writes Resources/Models/Base*.bytes)
    blender -b -P tools/blender/build_bases.py -- --preview <folder>  (also renders pictures)
    blender -b -P tools/blender/build_bases.py -- --only BaseDomes --preview <folder>   (quick look, writes nothing)

Base space, as in Scripts/Runtime/Bases/PlanetBases.cs: the origin is the middle of a level floor, the game's
+Y is up and +Z north. Here in Blender that is X east, Y north, Z up. The floor is a round foundation the game
builds itself, of radius 10, 18 or 30 metres (small, medium, large); everything here stands on z = 0 inside
that circle, and the middle is left open because that is where the fight happens.

Pieces in each file (Scripts/Runtime/Bases/BaseKinds.cs reads them by name):
  Shell, Frame, Detail   what is drawn, split by kind of surface (panels, steelwork, small things)
  WindowGlow, LampGlow   whatever shines, lit by the game (the file holds colour only)
  Hit                    what is solid: simple closed boxes and drums in one piece, never drawn
Attachment points: Spawn0.. (open ground for guards), Guard0.. (lookouts, up on towers), Loot0.., Door, Pad.

No faction names, flags or markings anywhere: whose base it is belongs to the game, not the model.
"""
import bpy, bmesh, sys, os, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import oa_blender as oa
import oa_nature as nat
from mathutils import Vector, Matrix, Euler

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "unity", "Assets", "OrbitalAnte", "Resources", "Models"))

# Triangle caps by size (the rule test BaseModelTests enforces the same numbers).
CAPS = {"small": 9000, "medium": 16000, "large": 34000}
HIT_CAP = 700
RADIUS = {"small": 10.0, "medium": 18.0, "large": 30.0}

WHITE = (0.90, 0.88, 0.82)
CREAM = (0.84, 0.78, 0.66)
ORANGE = (0.88, 0.43, 0.16)
TEAL = (0.20, 0.47, 0.48)
YELLOW = (0.88, 0.63, 0.15)
RUST = (0.62, 0.27, 0.17)
STEEL = (0.36, 0.39, 0.43)
DARK = (0.16, 0.17, 0.19)
SLATE = (0.30, 0.33, 0.38)
OLIVE = (0.42, 0.45, 0.28)
CONCRETE = (0.66, 0.64, 0.60)
SOLAR = (0.13, 0.19, 0.44)
RUBBER = (0.10, 0.10, 0.11)
WARM = (1.0, 0.84, 0.52)
CYAN = (0.42, 0.90, 1.0)
RED = (1.0, 0.26, 0.16)
GLASS = (0.50, 0.92, 0.80)


class Base:
    def __init__(self, name, size, accent):
        self.name, self.size, self.accent = name, size, accent
        self.groups = {"Shell": [], "Frame": [], "Detail": [], "WindowGlow": [], "LampGlow": []}
        self.hits = []
        self.sockets = []
        self.counts = {}
        self.rng = random.Random(sum(ord(c) for c in name))

    # --- what is drawn
    def shell(self, o, seed=0, wear=0.45):
        oa.paint(o, wear=wear, dirt=0.6, mottle=0.7, seed=seed, grain=0.9, gradient=0.05)
        self.groups["Shell"].append(o)
        return o

    def frame(self, o, seed=0):
        oa.paint(o, wear=0.55, dirt=0.5, mottle=0.6, seed=seed, grain=2.0, gradient=0.03)
        self.groups["Frame"].append(o)
        return o

    def detail(self, o, seed=0):
        oa.paint(o, wear=0.5, dirt=0.55, mottle=0.6, seed=seed, grain=2.4, gradient=0.04)
        self.groups["Detail"].append(o)
        return o

    def window(self, o):
        nat.flat_paint(o, o["oa_colour"][:3])
        self.groups["WindowGlow"].append(o)
        return o

    def lamp(self, o):
        nat.flat_paint(o, o["oa_colour"][:3])
        self.groups["LampGlow"].append(o)
        return o

    # --- what is solid
    def hit_box(self, centre, size, yaw=0.0, tilt=None):
        o = oa.box("hit", (0, 0, 0), size, (0.5, 0.5, 0.5), bevel=0)
        m = Matrix.Translation(Vector(centre)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z')
        if tilt is not None:
            m = m @ tilt
        o.data.transform(m)
        nat.flat_paint(o, (0.5, 0.5, 0.5))
        self.hits.append(o)
        return o

    def hit_drum(self, base, radius, height, radius_top=None, sides=8):
        o = oa.cylinder("hit", base, (base[0], base[1], base[2] + height), radius, (0.5, 0.5, 0.5), radius_end=radius_top, segments=sides)
        nat.flat_paint(o, (0.5, 0.5, 0.5))
        self.hits.append(o)
        return o

    def socket(self, kind, position):
        n = self.counts.get(kind, 0)
        self.counts[kind] = n + 1
        name = kind if kind in ("Door", "Pad") else "%s%d" % (kind, n)
        self.sockets.append(oa.socket(name, position))


def moved(objects, centre, yaw):
    """Turns things built at the origin (door side facing +Y) and stands them somewhere else."""
    m = Matrix.Translation(Vector(centre)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z')
    for o in objects:
        o.data.transform(m)
        o.data.update()
    return m


def beam(name, a, b, size, colour, bevel=0.12):
    a, b = Vector(a), Vector(b)
    # Thin bars are plain: nobody sees a rounded edge on a ladder rung, and there are hundreds of them.
    o = oa.box(name, (0, 0, 0), (size, size, (b - a).length), colour, bevel=size * bevel if size > 0.1 else 0, segments=1)
    o.data.transform(Matrix.Translation((a + b) * 0.5) @ Vector((0, 0, 1)).rotation_difference((b - a).normalized()).to_matrix().to_4x4())
    return o


def pipe(name, points, radius, colour, sides=8):
    return nat.tube(name, points, [radius] * len(points), colour, sides=sides, smooth_angle=50)


# ------------------------------------------------------------------ the kit

def lamp_post(b, at, height=5.5, aim=0.0, colour=WARM):
    x, y = at
    made = [b.frame(oa.cylinder("post", (0, 0, 0), (0, 0, height), 0.09, STEEL, radius_end=0.06, segments=8)),
            b.frame(oa.cylinder("post_foot", (0, 0, 0), (0, 0, 0.3), 0.22, CONCRETE, segments=10, bevel=0.04)),
            b.detail(oa.box("lamp_head", (0, 0.32, height), (0.7, 0.5, 0.28), DARK, bevel=0.06, rot=(-28, 0, 0))),
            b.frame(beam("lamp_arm", (0, 0, height - 0.25), (0, 0.3, height), 0.07, STEEL)),
            b.lamp(oa.box("lamp_face", (0, 0.44, height - 0.14), (0.56, 0.36, 0.06), colour, bevel=0.02, rot=(-28, 0, 0)))]
    moved(made, (x, y, 0), aim)
    b.hit_box((x, y, height / 2), (0.3, 0.3, height))


def crates(b, at, count=4, spread=1.4, loot=True):
    rng = b.rng
    x, y = at
    spots = []
    for i in range(count):
        s = rng.choice((0.8, 1.0, 1.2))
        if i < 3 or not spots:
            px, py, pz = x + rng.uniform(-spread, spread), y + rng.uniform(-spread, spread), s / 2
        else:
            bx, by, bs = rng.choice(spots)
            px, py, pz = bx + rng.uniform(-0.1, 0.1), by + rng.uniform(-0.1, 0.1), bs + s / 2
        yaw = rng.uniform(0, 90)
        colour = rng.choice((b.accent, OLIVE, CREAM, SLATE))
        made = [b.detail(oa.box("crate", (0, 0, 0), (s, s, s), colour, bevel=0.05, segments=1), seed=i),
                b.detail(oa.box("crate_band", (0, 0, 0), (s * 1.03, s * 0.16, s * 1.03), DARK, bevel=0.02, segments=1), seed=i),
                b.detail(oa.box("crate_lid", (0, 0, s * 0.47), (s * 1.04, s * 1.04, s * 0.1), tuple(c * 0.8 for c in colour), bevel=0.03, segments=1), seed=i)]
        moved(made, (px, py, pz), yaw)
        b.hit_box((px, py, pz), (s, s, s), yaw)
        if pz < s:
            spots.append((px, py, s))
    if loot:
        b.socket("Loot", (x, y + spread + 0.9, 0.0))


def barrels(b, at, count=3):
    rng = b.rng
    x, y = at
    for i in range(count):
        a = i * 2.2 + rng.uniform(0, 1)
        px, py = x + math.cos(a) * 0.62 * (i > 0), y + math.sin(a) * 0.62 * (i > 0)
        colour = rng.choice((b.accent, TEAL, RUST, YELLOW))
        b.detail(oa.cylinder("barrel", (px, py, 0), (px, py, 1.1), 0.36, colour, segments=12, bevel=0.05), seed=i)
        for z in (0.3, 0.8):
            b.detail(oa.cylinder("barrel_band", (px, py, z), (px, py, z + 0.06), 0.385, DARK, segments=12), seed=i)
        b.hit_drum((px, py, 0), 0.38, 1.1)


def hab(b, at, yaw, length=6.4, r=1.55, door=True, tag="hab"):
    """A living pod: a fat drum lying on cradles, with a porch, a lit door and round windows. The door faces +Y."""
    lift = 0.55
    made = []
    half = length / 2
    body = oa.cylinder(tag, (-half, 0, r + lift), (half, 0, r + lift), r, WHITE, segments=22, bevel=0.42)
    oa.densify(body, 1.4)
    made.append(b.shell(body, seed=int(at[0] * 7)))
    for i, x in enumerate((-half * 0.62, 0.0, half * 0.62)):
        made.append(b.shell(oa.cylinder(tag + "_rib", (x - 0.11, 0, r + lift), (x + 0.11, 0, r + lift), r + 0.07, b.accent, segments=22, bevel=0.03), seed=i))
    for x in (-half * 0.62, half * 0.62):
        made.append(b.frame(oa.box(tag + "_saddle", (x, 0, lift * 0.7), (0.5, r * 1.7, lift * 1.4), STEEL, bevel=0.06)))
        for s in (-1, 1):
            made.append(b.frame(beam(tag + "_leg", (x, s * r * 0.8, lift), (x, s * r * 1.05, 0), 0.16, STEEL)))
            made.append(b.frame(oa.cylinder(tag + "_shoe", (x, s * r * 1.05, 0), (x, s * r * 1.05, 0.08), 0.24, DARK, segments=10)))
    # round windows on the door side, a hatch-like vent and a whip aerial on top
    for x in ((-half * 0.31, half * 0.31) if door else (-half * 0.4, 0.0, half * 0.4)):
        made.append(b.frame(oa.cylinder(tag + "_port", (x, r * 0.86, r + lift + 0.25), (x, r * 1.0, r + lift + 0.32), 0.42, STEEL, segments=14, bevel=0.03)))
        made.append(b.window(oa.cylinder(tag + "_glass", (x, r * 0.99, r + lift + 0.315), (x, r * 1.02, r + lift + 0.33), 0.32, WARM, segments=14)))
    made.append(b.detail(oa.box(tag + "_vent", (half * 0.3, 0, 2 * r + lift), (0.9, 0.7, 0.3), SLATE, bevel=0.07)))
    made.append(b.frame(oa.cylinder(tag + "_aerial", (-half * 0.5, 0, 2 * r + lift - 0.05), (-half * 0.5, 0.25, 2 * r + lift + 2.2), 0.025, DARK, segments=5)))
    if door:
        made.append(b.shell(oa.box(tag + "_porch", (0, r * 0.95, lift + 1.25), (1.9, 0.9, 2.5), CREAM, bevel=0.12)))
        made.append(b.detail(oa.box(tag + "_door", (0, r * 0.95 + 0.44, lift + 1.15), (1.15, 0.08, 2.05), DARK, bevel=0.03, segments=1)))
        made.append(b.detail(oa.box(tag + "_door_stripe", (0, r * 0.95 + 0.47, lift + 1.5), (1.15, 0.04, 0.16), b.accent, bevel=0.01, segments=1)))
        made.append(b.lamp(oa.box(tag + "_door_lamp", (0, r * 0.95 + 0.44, lift + 2.4), (0.7, 0.12, 0.1), WARM, bevel=0.02, segments=1)))
        made.append(b.frame(oa.box(tag + "_landing", (0, r * 0.95 + 1.0, lift - 0.06), (2.0, 1.3, 0.14), STEEL, bevel=0.03, segments=1)))
        for i in range(2):
            made.append(b.frame(oa.box(tag + "_step", (0, r * 0.95 + 1.85 + i * 0.35, lift - 0.24 - i * 0.18), (1.6, 0.34, 0.1), STEEL, bevel=0.02, segments=1)))
    m = moved(made, (at[0], at[1], 0), yaw)
    b.hit_box((at[0], at[1], r + lift), (length, r * 1.8, r * 1.9), yaw)
    if door:
        c = m @ Vector((0, r * 0.95 + 0.9, (lift) / 2))
        b.hit_box(c, (2.0, 2.0, lift), yaw)
    return m


def dome(b, at, r, colour=WHITE, glass=False, porch_yaw=None, tag="dome"):
    """A pressure dome on a ring wall, ribbed like an orange. glass: a lit greenhouse."""
    x, y = at
    wall = 0.9
    profile = [(0.0, r + 0.22), (wall, r + 0.22), (wall, r)]
    for i in range(1, 9):
        a = math.radians(90 * i / 8)
        profile.append((wall + r * 0.82 * math.sin(a), r * math.cos(a) + (0.25 if i == 8 else 0)))
    skin = oa.lathe(tag, (x, y, 0), (0, 0, 1), profile[2:], GLASS if glass else colour, segments=28)
    if glass:
        b.window(skin)
    else:
        oa.densify(skin, 1.6)
        b.shell(skin, seed=int(x))
    b.shell(oa.lathe(tag + "_ring", (x, y, 0), (0, 0, 1), [(0.0, r + 0.3), (wall * 0.75, r + 0.3), (wall, r + 0.08), (wall, r - 0.2)], CONCRETE if not glass else STEEL, segments=28), seed=2)
    ribs = 10 if r > 4.5 else 8
    for k in range(ribs):
        a = 2 * math.pi * k / ribs
        pts = []
        for i in range(0, 8):
            e = math.radians(90 * i / 8)
            rr = (r + 0.05) * math.cos(e)
            pts.append((x + math.cos(a) * rr, y + math.sin(a) * rr, wall + (r * 0.82 + 0.05) * math.sin(e)))
        b.frame(pipe(tag + "_rib%d" % k, pts, 0.09 if not glass else 0.07, b.accent if not glass else STEEL, sides=5), seed=k)
    b.shell(oa.lathe(tag + "_cap", (x, y, wall + r * 0.82 - 0.12), (0, 0, 1), [(0, r * 0.2), (0.25, r * 0.2), (0.45, r * 0.1)], b.accent, segments=16), seed=4)
    b.lamp(oa.cylinder(tag + "_beacon", (x, y, wall + r * 0.82 + 0.3), (x, y, wall + r * 0.82 + 0.55), 0.14, RED if not glass else CYAN, segments=8))
    b.hit_drum((x, y, 0), r + 0.3, wall + r * 0.42, sides=12)
    b.hit_drum((x, y, wall + r * 0.42), r * 0.86, r * 0.42, radius_top=r * 0.3, sides=12)
    if porch_yaw is not None:
        made = [b.shell(oa.box(tag + "_porch", (0, r + 0.5, 1.3), (2.3, 2.0, 2.6), CREAM, bevel=0.14)),
                b.detail(oa.box(tag + "_door", (0, r + 1.52, 1.12), (1.3, 0.08, 2.1), DARK, bevel=0.03, segments=1)),
                b.detail(oa.box(tag + "_door_stripe", (0, r + 1.55, 1.55), (1.3, 0.04, 0.16), b.accent, bevel=0.01, segments=1)),
                b.lamp(oa.box(tag + "_door_lamp", (0, r + 1.5, 2.42), (0.8, 0.12, 0.1), WARM, bevel=0.02, segments=1))]
        m = moved(made, (x, y, 0), porch_yaw)
        b.hit_box(m @ Vector((0, r + 0.5, 1.3)), (2.3, 2.0, 2.6), porch_yaw)
        return m @ Vector((0, r + 2.6, 0))
    return None


def tunnel(b, a, c, r=1.0, tag="tunnel"):
    """A ribbed crawlway joining two buildings."""
    a, c = Vector((a[0], a[1], r + 0.25)), Vector((c[0], c[1], r + 0.25))
    b.shell(oa.cylinder(tag, a, c, r, CREAM, segments=14), seed=3)
    n = max(2, int((c - a).length / 1.5))
    for i in range(1, n):
        p = a.lerp(c, i / n)
        d = (c - a).normalized() * 0.09
        b.frame(oa.cylinder(tag + "_rib", p - d, p + d, r + 0.07, STEEL, segments=14))
    mid = (a + c) * 0.5
    yaw = math.degrees(math.atan2((c - a).y, (c - a).x))
    b.hit_box((mid.x, mid.y, (r * 2 + 0.25) / 2), ((c - a).length, r * 1.9, r * 2 + 0.25), yaw)


def tank(b, at, r=1.5, h=5.0, colour=None, tag="tank"):
    x, y = at
    colour = colour or WHITE
    body = oa.lathe(tag, (x, y, 0), (0, 0, 1), [(0.0, r * 0.96), (0.25, r), (h - r * 0.45, r), (h - r * 0.2, r * 0.8), (h, r * 0.3)], colour, segments=20)
    oa.densify(body, 1.5)
    b.shell(body, seed=int(x * 3))
    for z in (h * 0.25, h * 0.6):
        b.shell(oa.cylinder(tag + "_band", (x, y, z), (x, y, z + 0.2), r + 0.05, b.accent, segments=20), seed=1)
    b.detail(oa.cylinder(tag + "_valve", (x, y, h - 0.05), (x, y, h + 0.3), 0.22, STEEL, segments=8, bevel=0.03))
    for s in (-1, 1):
        b.frame(beam(tag + "_rail", (x + s * 0.22, y - r - 0.08, 0), (x + s * 0.22, y - r - 0.08, h - r * 0.4), 0.05, STEEL))
    for i in range(int((h - r * 0.4) / 0.45)):
        b.frame(beam(tag + "_rung", (x - 0.22, y - r - 0.08, 0.35 + i * 0.45), (x + 0.22, y - r - 0.08, 0.35 + i * 0.45), 0.04, STEEL))
    b.hit_drum((x, y, 0), r + 0.05, h, sides=10)


def ball_tank(b, at, r=2.0, tag="ball"):
    """A round fuel tank held up in a ring of legs."""
    x, y = at
    lift = r * 0.75
    ball = oa.sphere(tag, (x, y, lift + r), r, WHITE, segments=22)
    b.shell(ball, seed=int(x * 5 + y))
    b.shell(oa.cylinder(tag + "_belt", (x, y, lift + r - 0.16), (x, y, lift + r + 0.16), r * 1.015, b.accent, segments=22), seed=2)
    for k in range(6):
        a = 2 * math.pi * k / 6
        ox, oy = math.cos(a), math.sin(a)
        b.frame(beam(tag + "_leg", (x + ox * r * 0.86, y + oy * r * 0.86, lift + r * 0.75), (x + ox * r * 1.02, y + oy * r * 1.02, 0), 0.18, STEEL))
        b.frame(oa.cylinder(tag + "_shoe", (x + ox * r * 1.02, y + oy * r * 1.02, 0), (x + ox * r * 1.02, y + oy * r * 1.02, 0.1), 0.26, DARK, segments=8))
    b.frame(pipe(tag + "_ring", [(x + math.cos(2 * math.pi * k / 12) * r * 0.95, y + math.sin(2 * math.pi * k / 12) * r * 0.95, lift * 0.55) for k in range(13)], 0.06, STEEL, sides=5))
    b.detail(oa.cylinder(tag + "_cap", (x, y, lift + 2 * r - 0.05), (x, y, lift + 2 * r + 0.25), 0.3, STEEL, segments=10, bevel=0.04))
    b.detail(oa.cylinder(tag + "_drain", (x, y, lift - 0.02), (x, y, 0.5), 0.14, STEEL, segments=8))
    b.hit_drum((x, y, 0), r * 1.02, lift + 2 * r, sides=10)


def mast(b, at, height=13.0, dish=True, tag="mast"):
    """A three-legged lattice tower with a dish and a warning light."""
    x, y = at
    foot, head = 1.1, 0.32
    legs = []
    for k in range(3):
        a = 2 * math.pi * k / 3 + 0.5
        legs.append((Vector((x + math.cos(a) * foot, y + math.sin(a) * foot, 0)), Vector((x + math.cos(a) * head, y + math.sin(a) * head, height))))
        b.frame(beam(tag + "_leg", legs[-1][0], legs[-1][1], 0.13, b.accent), seed=k)
        b.frame(oa.cylinder(tag + "_shoe", legs[-1][0], legs[-1][0] + Vector((0, 0, 0.25)), 0.26, CONCRETE, segments=8))
    levels = int(height / 2.4)
    for i in range(1, levels + 1):
        t0, t1 = (i - 1) / levels, i / levels
        for k in range(3):
            p0, p1 = legs[k][0].lerp(legs[k][1], t1), legs[(k + 1) % 3][0].lerp(legs[(k + 1) % 3][1], t1)
            b.frame(beam(tag + "_ring", p0, p1, 0.06, STEEL))
            b.frame(beam(tag + "_brace", legs[k][0].lerp(legs[k][1], t0), p1, 0.045, STEEL))
    top = Vector((x, y, height))
    b.frame(oa.cylinder(tag + "_spike", top, top + Vector((0, 0, 1.6)), 0.05, DARK, segments=6))
    b.lamp(oa.sphere(tag + "_light", top + Vector((0, 0, 1.68)), 0.13, RED, segments=8))
    if dish:
        c = Vector((x, y, height - 1.4))
        bowl = oa.lathe(tag + "_dish", (0, 0, 0), (0, 0, 1), [(0.0, 0.12), (0.06, 0.5), (0.22, 0.95), (0.5, 1.35), (0.56, 1.37), (0.5, 1.3), (0.25, 0.92), (0.1, 0.5), (0.04, 0.12)], WHITE, segments=20)
        feed = oa.cylinder(tag + "_feed", (0, 0, 0.05), (0, 0, 0.95), 0.04, STEEL, segments=6)
        horn = oa.sphere(tag + "_horn", (0, 0, 0.98), 0.09, b.accent, segments=8)
        tilt = Matrix.Translation(c + Vector((0.55, 0, 0))) @ Euler((0, math.radians(62), 0.6)).to_matrix().to_4x4()
        for o in (bowl, feed, horn):
            o.data.transform(tilt)
        b.shell(bowl, seed=5)
        b.frame(feed)
        b.detail(horn)
        b.frame(beam(tag + "_yoke", c, c + Vector((0.5, 0, 0)), 0.14, STEEL))
    b.hit_box((x, y, height / 2), (foot * 1.5, foot * 1.5, height))


def solar(b, at, yaw, w=3.4, h=2.1, tag="solar"):
    """A solar panel on a post, tipped toward the sun: a frame holding six dark blue cells."""
    made = [b.frame(oa.cylinder(tag + "_post", (0, 0, 0), (0, 0, 1.5), 0.1, STEEL, segments=8)),
            b.frame(oa.cylinder(tag + "_foot", (0, 0, 0), (0, 0, 0.16), 0.34, CONCRETE, segments=10))]
    tilt = Matrix.Translation((0, 0, 1.55)) @ Matrix.Rotation(math.radians(-32), 4, 'X')
    frame = oa.box(tag + "_frame", (0, 0, 0), (w, 0.1, h), WHITE, bevel=0.035, segments=1)
    frame.data.transform(tilt)
    made.append(b.shell(frame))
    for i in range(3):
        for j in range(2):
            cell = oa.box(tag + "_cell", ((i - 1) * w / 3.08, 0.045, (j - 0.5) * h / 2.1), (w / 3.3, 0.05, h / 2.25), SOLAR, bevel=0.012, segments=1)
            cell.data.transform(tilt)
            oa.paint(cell, wear=0.9, dirt=0.2, mottle=0.5, seed=i + j, wear_colour=(0.35, 0.48, 0.85), grain=3)
            b.groups["Detail"].append(cell)
            made.append(cell)
    brace = beam(tag + "_brace", (0, -0.1, 0.9), (0, -0.55, 1.95), 0.06, STEEL)
    made.append(b.frame(brace))
    moved(made, (at[0], at[1], 0), yaw)
    b.hit_box((at[0], at[1], 1.3), (w, 1.4, 2.6), yaw)


def watchtower(b, at, yaw=0.0, height=6.5, tag="tower"):
    """A lookout on four splayed legs: a railed deck under a little roof, a ladder and a searchlight."""
    made = []
    foot, top = 1.7, 1.25
    corners = ((-1, -1), (1, -1), (1, 1), (-1, 1))
    for i, (sx, sy) in enumerate(corners):
        made.append(b.frame(beam(tag + "_leg", (sx * foot, sy * foot, 0), (sx * top, sy * top, height), 0.17, b.accent), seed=i))
        made.append(b.frame(oa.cylinder(tag + "_shoe", (sx * foot, sy * foot, 0), (sx * foot, sy * foot, 0.2), 0.3, CONCRETE, segments=8)))
        nx, ny = corners[(i + 1) % 4]
        for lo, hi in ((0.08, 0.5), (0.5, 0.94)):
            a0 = Vector((sx * foot, sy * foot, 0)).lerp(Vector((sx * top, sy * top, height)), lo)
            b1 = Vector((nx * foot, ny * foot, 0)).lerp(Vector((nx * top, ny * top, height)), hi)
            made.append(b.frame(beam(tag + "_brace", a0, b1, 0.06, STEEL)))
        made.append(b.frame(beam(tag + "_post", (sx * 1.45, sy * 1.45, height + 0.15), (sx * 1.45, sy * 1.45, height + 2.35), 0.09, STEEL)))
        made.append(b.frame(beam(tag + "_rail", (sx * 1.5, sy * 1.5, height + 1.1), (nx * 1.5, ny * 1.5, height + 1.1), 0.07, b.accent)))
        if i != 0:   # the side with the ladder is left open below the top rail
            mid = (Vector((sx * 1.5, sy * 1.5, 0)) + Vector((nx * 1.5, ny * 1.5, 0))) * 0.5
            panel = oa.box(tag + "_skirt", (0, 0, 0), ((Vector((sx, sy, 0)) - Vector((nx, ny, 0))).length * 1.45, 0.06, 0.75), CREAM, bevel=0.02, segments=1)
            panel.data.transform(Matrix.Translation((mid.x, mid.y, height + 0.6)) @ Matrix.Rotation(math.atan2(ny - sy, nx - sx), 4, 'Z'))
            made.append(b.shell(panel, seed=i))
    made.append(b.shell(oa.box(tag + "_deck", (0, 0, height), (3.3, 3.3, 0.32), SLATE, bevel=0.05)))
    roof = oa.lathe(tag + "_roof", (0, 0, height + 2.35), (0, 0, 1), [(0.0, 2.45), (0.12, 2.45), (0.9, 0.25), (1.0, 0.0)], b.accent, segments=4)
    roof.data.transform(Matrix.Rotation(math.radians(45), 4, 'Z'))
    made.append(b.shell(roof, seed=7))
    for s in (-1, 1):
        made.append(b.frame(beam(tag + "_ladder", (s * 0.25, -foot - 0.05, 0), (s * 0.25, -top - 0.32, height + 1.0), 0.05, STEEL)))
    for i in range(int(height / 0.45)):
        t = (i + 0.6) * 0.45 / height
        yy = -foot - 0.05 + (-top - 0.32 + foot + 0.05) * t * height / (height + 1.0)
        made.append(b.frame(beam(tag + "_rung", (-0.25, yy, t * height), (0.25, yy, t * height), 0.04, STEEL)))
    made.append(b.detail(oa.cylinder(tag + "_search", (1.1, 1.3, height + 1.35), (1.1, 1.75, height + 1.3), 0.26, DARK, segments=10, bevel=0.03)))
    made.append(b.lamp(oa.cylinder(tag + "_beam", (1.1, 1.75, height + 1.3), (1.1, 1.79, height + 1.3), 0.21, WARM, segments=10)))
    m = moved(made, (at[0], at[1], 0), yaw)
    b.hit_box((at[0], at[1], height), (3.3, 3.3, 0.32), yaw)
    b.hit_box((at[0], at[1], height / 2 - 0.1), (2.4, 2.4, height - 0.25), yaw)
    b.hit_box((at[0], at[1], height + 2.7), (3.4, 3.4, 0.7), yaw)
    b.socket("Guard", m @ Vector((0, 0, height + 0.16)))


def shed(b, at, yaw, w=7.0, d=5.0, h=3.2, tag="shed"):
    """A workshop with ribbed walls, a barrel roof and a big roller door facing +Y."""
    made = []
    walls = oa.box(tag, (0, 0, h / 2), (w, d, h), CREAM, bevel=0.1)
    oa.densify(walls, 1.3)
    made.append(b.shell(walls, seed=11))
    n = int(w / 0.7)
    for i in range(n + 1):
        x = -w / 2 + w * i / n
        for s in (-1, 1):
            if s > 0 and abs(x) < w * 0.31:
                continue
            made.append(b.shell(oa.box(tag + "_rib", (x, s * (d / 2 + 0.03), h / 2), (0.14, 0.1, h * 0.96), WHITE, bevel=0.03, segments=1), seed=i))
    roof = oa.cylinder(tag + "_roof", (-w / 2 - 0.25, 0, h - 0.1), (w / 2 + 0.25, 0, h - 0.1), d / 2 + 0.25, b.accent, segments=18)
    roof.data.transform(Matrix.Translation((0, 0, h - 0.1)) @ Matrix.Diagonal((1, 1, 0.42, 1)) @ Matrix.Translation((0, 0, -h + 0.1)))
    oa.densify(roof, 1.4)
    made.append(b.shell(roof, seed=12))
    made.append(b.detail(oa.box(tag + "_door", (0, d / 2 + 0.05, h * 0.42), (w * 0.56, 0.1, h * 0.84), SLATE, bevel=0.03, segments=1)))
    for i in range(6):
        made.append(b.detail(oa.box(tag + "_slat", (0, d / 2 + 0.11, 0.3 + i * h * 0.13), (w * 0.56, 0.04, 0.06), DARK, bevel=0.01, segments=1)))
    made.append(b.lamp(oa.box(tag + "_lamp", (0, d / 2 + 0.12, h * 0.92), (1.2, 0.1, 0.12), WARM, bevel=0.02, segments=1)))
    for s in (-1, 1):
        made.append(b.window(oa.box(tag + "_window", (s * (w / 2 + 0.02), 0, h * 0.62), (0.06, d * 0.4, 0.7), WARM, bevel=0.02, segments=1)))
        made.append(b.frame(oa.box(tag + "_window_frame", (s * (w / 2 + 0.01), 0, h * 0.62), (0.08, d * 0.4 + 0.2, 0.9), STEEL, bevel=0.03, segments=1)))
    made.append(b.detail(oa.cylinder(tag + "_flue", (w * 0.3, -d * 0.2, h), (w * 0.3, -d * 0.2, h + 1.9), 0.18, STEEL, segments=8, bevel=0.03)))
    m = moved(made, (at[0], at[1], 0), yaw)
    b.hit_box((at[0], at[1], (h + 0.8) / 2), (w, d, h + 0.8), yaw)
    return m


def wall(b, a, c, height=3.4, tag="wall"):
    """A run of prefabricated wall panels between two points, with posts at the joints and a rail on top."""
    a, c = Vector((a[0], a[1], 0)), Vector((c[0], c[1], 0))
    length = (c - a).length
    yaw = math.degrees(math.atan2((c - a).y, (c - a).x))
    n = max(1, round(length / 3.6))
    made = []
    for i in range(n):
        x = -length / 2 + length * (i + 0.5) / n
        panel = oa.box(tag, (x, 0, height / 2), (length / n - 0.12, 0.5, height), CONCRETE if i % 2 else CREAM, bevel=0.09, segments=1)
        oa.densify(panel, 2.6)
        made.append(b.shell(panel, seed=i + int(a.x)))
        made.append(b.shell(oa.box(tag + "_band", (x, 0, height * 0.72), (length / n - 0.1, 0.54, 0.34), b.accent, bevel=0.03, segments=1), seed=i))
    for i in range(n + 1):
        x = -length / 2 + length * i / n
        made.append(b.frame(oa.box(tag + "_post", (x, 0, (height + 0.4) / 2), (0.34, 0.74, height + 0.4), SLATE, bevel=0.07, segments=1), seed=i))
    made.append(b.frame(beam(tag + "_rail", (-length / 2, 0, height + 0.55), (length / 2, 0, height + 0.55), 0.09, STEEL)))
    mid = (a + c) * 0.5
    moved(made, (mid.x, mid.y, 0), yaw)
    b.hit_box((mid.x, mid.y, (height + 0.4) / 2), (length, 0.74, height + 0.4), yaw)


def gate(b, at, yaw, width=6.0, height=5.2, tag="gate"):
    """Two stout pylons and a beam across: the way in. Open, never closed."""
    made = []
    for s in (-1, 1):
        pylon = oa.box(tag + "_pylon", (s * (width / 2 + 0.8), 0, height / 2), (1.6, 1.5, height), SLATE, bevel=0.16)
        oa.densify(pylon, 1.4)
        made.append(b.shell(pylon, seed=s + 3))
        made.append(b.shell(oa.box(tag + "_cap", (s * (width / 2 + 0.8), 0, height + 0.12), (1.9, 1.8, 0.3), b.accent, bevel=0.08)))
        made.append(b.lamp(oa.box(tag + "_lamp", (s * (width / 2 + 0.8), 0.78, height * 0.72), (0.5, 0.08, 0.9), WARM, bevel=0.02, segments=1)))
        made.append(b.lamp(oa.sphere(tag + "_beacon", (s * (width / 2 + 0.8), 0, height + 0.42), 0.17, RED, segments=8)))
    made.append(b.frame(oa.box(tag + "_lintel", (0, 0, height - 0.5), (width + 1.0, 0.6, 0.6), STEEL, bevel=0.08)))
    made.append(b.frame(oa.box(tag + "_lintel_low", (0, 0, height - 1.25), (width + 0.2, 0.3, 0.25), STEEL, bevel=0.05, segments=1)))
    for i in range(5):
        x = -width / 2 + width * (i + 0.5) / 5
        made.append(b.frame(beam(tag + "_web", (x - width / 10, 0, height - 1.2), (x + width / 10, 0, height - 0.55), 0.07, STEEL)))
    m = moved(made, (at[0], at[1], 0), yaw)
    for s in (-1, 1):
        b.hit_box(m @ Vector((s * (width / 2 + 0.8), 0, height / 2)), (1.6, 1.5, height), yaw)
    b.hit_box(m @ Vector((0, 0, height - 0.75)), (width + 1.0, 0.6, 1.1), yaw)


def pad(b, at, r=4.0):
    """A marked landing circle with a ring of ground lights. Flat: nothing to trip on."""
    x, y = at
    b.shell(oa.lathe("pad", (x, y, 0), (0, 0, 1), [(0.0, r), (0.07, r), (0.07, r - 0.45), (0.02, r - 0.45)], b.accent, segments=32), seed=8)
    floor = oa.lathe("pad_floor", (x, y, 0), (0, 0, 1), [(0.0, r - 0.45), (0.045, r - 0.45)], SLATE, segments=32)
    oa.densify(floor, 1.2)
    b.shell(floor, seed=9)
    b.shell(oa.lathe("pad_mark", (x, y, 0), (0, 0, 1), [(0.04, r * 0.28), (0.07, r * 0.28)], WHITE, segments=20), seed=9)
    for k in range(4):
        a = 2 * math.pi * k / 4 + 0.785
        b.shell(oa.box("pad_tick", (x + math.cos(a) * r * 0.62, y + math.sin(a) * r * 0.62, 0.05), (r * 0.3, 0.3, 0.04), WHITE, bevel=0, rot=(0, 0, math.degrees(a))), seed=k)
    for k in range(8):
        a = 2 * math.pi * (k + 0.5) / 8
        px, py = x + math.cos(a) * (r + 0.35), y + math.sin(a) * (r + 0.35)
        b.detail(oa.cylinder("pad_pot", (px, py, 0), (px, py, 0.1), 0.17, DARK, segments=8))
        b.lamp(oa.cylinder("pad_light", (px, py, 0.1), (px, py, 0.14), 0.11, CYAN, segments=8))
    b.socket("Pad", (x, y, 0.0))


def spoil(b, at, r, h, seed, colour=((0.30, 0.27, 0.26), (0.50, 0.45, 0.41))):
    """A heap of dug rock."""
    x, y = at
    rng = random.Random(seed)
    lobes = [((0, 0, 0), (r * 0.8, r * 0.75, h)), ((r * 0.5, r * 0.2, 0), (r * 0.6, r * 0.6, h * 0.6)), ((-r * 0.45, -r * 0.3, 0), (r * 0.6, r * 0.55, h * 0.5)), ((0, 0, 0), (r, r, h * 0.3))]
    cuts = []
    o = nat.lobed("spoil", (x, y, 0.05), lobes, colour[0], n=8, lump=0.22, grain=2.6, seed=seed, flat=-0.05, smooth_angle=50, ridged=0.35)
    for v in o.data.vertices:
        v.co.z = max(v.co.z, -0.25)
    nat.rock_paint(o, colour[0], colour[1], seed=seed, strata=0.0, grain=1.2)
    b.groups["Detail"].append(o)
    hit = nat.lobed("hit", (x, y, 0.05), lobes, colour[0], n=2, lump=0.0, seed=seed, flat=-0.05)
    bm = bmesh.new()
    bm.from_mesh(hit.data)
    for v in bm.verts:
        v.co.z = max(v.co.z, -0.3)
    bm.to_mesh(hit.data)
    bm.free()
    nat.flat_paint(hit, (0.5, 0.5, 0.5))
    hull = nat.convex_hull("hit", hit)
    bpy.data.objects.remove(hit)
    b.hits.append(hull)


def headframe(b, at, yaw, height=13.0, tag="head"):
    """The winding tower over a mine shaft: two A-frames, a pair of big wheels and a cable going down the hole."""
    made = []
    top = height
    for sx in (-1, 1):
        for sy, reach in ((-1, 2.4), (1, 6.2)):   # the back legs stand close, the front struts rake out
            made.append(b.frame(beam(tag + "_leg", (sx * 1.9, sy * reach, 0), (sx * 0.95, sy * 0.35, top), 0.34, b.accent, bevel=0.1), seed=int(sx + sy)))
            made.append(b.frame(oa.box(tag + "_foot", (sx * 1.9, sy * reach, 0.2), (0.9, 0.9, 0.4), CONCRETE, bevel=0.06)))
        for t in (0.3, 0.55, 0.8):
            p0 = Vector((sx * 1.9, -2.4, 0)).lerp(Vector((sx * 0.95, -0.35, top)), t)
            p1 = Vector((sx * 1.9, 6.2, 0)).lerp(Vector((sx * 0.95, 0.35, top)), t)
            made.append(b.frame(beam(tag + "_tie", p0, p1, 0.14, STEEL)))
    for t in (0.3, 0.55, 0.8, 1.0):
        for sy, reach, y1 in ((-1, 2.4, -0.35), (1, 6.2, 0.35)):
            p0 = Vector((-1.9, sy * reach, 0)).lerp(Vector((-0.95, y1, top)), t)
            p1 = Vector((1.9, sy * reach, 0)).lerp(Vector((0.95, y1, top)), t)
            made.append(b.frame(beam(tag + "_cross", p0, p1, 0.14, STEEL)))
    made.append(b.shell(oa.box(tag + "_cabin", (0, 0, top + 0.55), (2.6, 1.6, 1.1), CREAM, bevel=0.1)))
    made.append(b.shell(oa.box(tag + "_cabin_roof", (0, 0, top + 1.17), (2.9, 1.9, 0.16), b.accent, bevel=0.05, segments=1)))
    for sx in (-0.55, 0.55):
        wheel = oa.lathe(tag + "_wheel", (sx, 0, top - 0.9), (1, 0, 0), [(-0.07, 1.2), (-0.07, 1.38), (0.07, 1.38), (0.07, 1.2)], DARK, segments=20)
        made.append(b.frame(wheel))
        for k in range(4):
            a = math.pi * k / 4
            made.append(b.frame(beam(tag + "_spoke", (sx, -math.cos(a) * 1.25, top - 0.9 - math.sin(a) * 1.25), (sx, math.cos(a) * 1.25, top - 0.9 + math.sin(a) * 1.25), 0.06, STEEL)))
        made.append(b.frame(oa.cylinder(tag + "_cable", (sx, -1.3, top - 0.9), (sx, -1.3, 0.4), 0.035, DARK, segments=5)))
    made.append(b.frame(oa.cylinder(tag + "_axle", (-0.95, 0, top - 0.9), (0.95, 0, top - 0.9), 0.12, STEEL, segments=8)))
    made.append(b.shell(oa.lathe(tag + "_collar", (0, -1.3, 0), (0, 0, 1), [(0.0, 2.0), (0.7, 2.0), (0.7, 1.55), (0.25, 1.55)], CONCRETE, segments=18), seed=2))
    made.append(b.detail(oa.cylinder(tag + "_shaft", (0, -1.3, 0.2), (0, -1.3, 0.26), 1.55, RUBBER, segments=18)))
    made.append(b.lamp(oa.box(tag + "_lamp", (0, 0.82, top + 0.55), (1.2, 0.06, 0.3), WARM, bevel=0.02, segments=1)))
    made.append(b.lamp(oa.sphere(tag + "_beacon", (0, 0, top + 1.42), 0.16, RED, segments=8)))
    m = moved(made, (at[0], at[1], 0), yaw)
    b.hit_drum((m @ Vector((0, -1.3, 0)))[:], 2.0, 0.7, sides=10)
    b.hit_box(m @ Vector((0, 0, top + 0.2)), (2.9, 1.9, 2.4), yaw)
    for sx in (-1, 1):
        lean_back = Matrix.Rotation(math.atan2(2.05, top), 4, 'X')
        b.hit_box(m @ Vector((sx * 1.42, -1.37, top / 2)), (0.5, 0.5, top), yaw, tilt=lean_back)
        lean_front = Matrix.Rotation(-math.atan2(5.85, top), 4, 'X')
        b.hit_box(m @ Vector((sx * 1.42, 3.27, top / 2)), (0.5, 0.5, math.hypot(top, 5.85)), yaw, tilt=lean_front)
    return m


def conveyor(b, a, c, rise, width=1.3, tag="belt"):
    """An ore belt climbing from a low end to a high end on trestles."""
    a, c = Vector((a[0], a[1], 0.8)), Vector((c[0], c[1], rise))
    along = c - a
    length = along.length
    yaw = math.degrees(math.atan2(along.y, along.x))
    pitch = math.atan2(along.z, math.hypot(along.x, along.y))
    turn = Matrix.Translation((a + c) * 0.5) @ Matrix.Rotation(math.radians(yaw), 4, 'Z') @ Matrix.Rotation(-pitch, 4, 'Y')
    made = [oa.box(tag + "_deck", (0, 0, 0), (length, width, 0.3), b.accent, bevel=0.06),
            oa.box(tag + "_belt", (0, 0, 0.17), (length * 0.99, width * 0.72, 0.06), RUBBER, bevel=0.02, segments=1)]
    for s in (-1, 1):
        made.append(oa.box(tag + "_side", (0, s * width * 0.48, 0.32), (length, 0.08, 0.36), STEEL, bevel=0.02, segments=1))
    n = int(length / 1.3)
    for i in range(n + 1):
        x = -length / 2 + length * i / n
        for s in (-1, 1):
            made.append(oa.box(tag + "_stay", (x, s * width * 0.48, 0.3), (0.08, 0.1, 0.42), b.accent, bevel=0.01, segments=1))
    for i in range(int(length / 2.2)):
        x = -length / 2 + 1.1 + i * 2.2
        made.append(oa.sphere(tag + "_ore", (x, 0.0, 0.3), 0.32, (0.42, 0.33, 0.28), scale=(1.3, 1.0, 0.6), segments=8))
    for o in made:
        o.data.transform(turn)
    b.shell(made[0], seed=5)
    for o in made[1:]:
        (b.detail if "ore" in o.name or "belt" in o.name else b.frame)(o)
    for t in (0.3, 0.62, 0.92):
        p = a.lerp(c, t)
        side = Vector((-along.y, along.x, 0)).normalized() * (width * 0.5)
        for s in (-1, 1):
            b.frame(beam(tag + "_trestle", (p.x + s * side.x * 1.7, p.y + s * side.y * 1.7, 0), (p.x + s * side.x, p.y + s * side.y, p.z - 0.1), 0.14, STEEL))
        b.frame(beam(tag + "_trestle_tie", (p.x - side.x * 1.4, p.y - side.y * 1.4, p.z * 0.5), (p.x + side.x * 1.4, p.y + side.y * 1.4, p.z * 0.5), 0.09, STEEL))
    mid = (a + c) * 0.5
    b.hit_box((mid.x, mid.y, mid.z + 0.05), (length, width, 0.7), yaw, tilt=Matrix.Rotation(-pitch, 4, 'Y'))


def hopper(b, at, top=7.0, tag="hopper"):
    """A bin on legs that the belt tips into, with a chute underneath."""
    x, y = at
    bin_ = oa.lathe(tag, (x, y, top - 3.0), (0, 0, 1), [(0.0, 0.5), (1.6, 2.1), (3.0, 2.1), (3.0, 1.95), (1.65, 1.95)], CREAM, segments=4)
    bin_.data.transform(Matrix.Translation((x, y, 0)) @ Matrix.Rotation(math.radians(45), 4, 'Z') @ Matrix.Translation((-x, -y, 0)))
    oa.densify(bin_, 1.3)
    b.shell(bin_, seed=6)
    b.shell(oa.box(tag + "_band", (x, y, top - 0.25), (3.1, 3.1, 0.3), b.accent, bevel=0.05, segments=1), seed=7)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.frame(beam(tag + "_leg", (x + sx * 1.6, y + sy * 1.6, 0), (x + sx * 1.35, y + sy * 1.35, top - 1.4), 0.2, STEEL))
            b.frame(oa.cylinder(tag + "_shoe", (x + sx * 1.6, y + sy * 1.6, 0), (x + sx * 1.6, y + sy * 1.6, 0.18), 0.3, CONCRETE, segments=8))
    for z in (1.6, 3.2):
        for (ax, ay), (bx, by) in (((-1, -1), (1, -1)), ((1, -1), (1, 1)), ((1, 1), (-1, 1)), ((-1, 1), (-1, -1))):
            k = 1.6 - 0.25 * z / (top - 1.4)
            b.frame(beam(tag + "_tie", (x + ax * k, y + ay * k, z), (x + bx * k, y + by * k, z), 0.08, STEEL))
    b.detail(oa.box(tag + "_chute", (x, y - 0.5, top - 3.5), (0.8, 1.6, 0.5), SLATE, bevel=0.06, rot=(28, 0, 0)))
    b.lamp(oa.box(tag + "_lamp", (x, y - 1.58, top - 0.25), (0.9, 0.06, 0.16), WARM, bevel=0.02, segments=1))
    b.hit_box((x, y, top - 1.5), (3.1, 3.1, 3.0))
    b.hit_box((x, y, (top - 3.0) / 2), (3.3, 3.3, top - 3.0))


def cart(b, at, yaw, tag="cart"):
    made = [b.detail(oa.box(tag, (0, 0, 0.85), (1.9, 1.2, 0.9), b.accent, bevel=0.09)),
            b.detail(oa.sphere(tag + "_load", (0, 0, 1.28), 0.62, (0.40, 0.31, 0.27), scale=(1.3, 0.85, 0.45), segments=10)),
            b.frame(oa.box(tag + "_chassis", (0, 0, 0.36), (2.1, 0.9, 0.14), STEEL, bevel=0.03, segments=1))]
    for sx in (-0.65, 0.65):
        for sy in (-0.5, 0.5):
            made.append(b.frame(oa.cylinder(tag + "_wheel", (sx, sy - 0.06, 0.24), (sx, sy + 0.06, 0.24), 0.24, DARK, segments=10)))
    for sy in (-0.5, 0.5):
        made.append(b.frame(beam(tag + "_track", (-2.6, sy, 0.03), (2.6, sy, 0.03), 0.07, STEEL)))
    for i in range(6):
        made.append(b.detail(oa.box(tag + "_tie", (-2.3 + i * 0.92, 0, 0.012), (0.22, 1.5, 0.05), (0.34, 0.25, 0.18), bevel=0.01, segments=1)))
    moved(made, (at[0], at[1], 0), yaw)
    b.hit_box((at[0], at[1], 0.75), (2.1, 1.25, 1.5), yaw)


def command_tower(b, at, yaw, height=15.0, tag="keep"):
    """The heart of a stronghold: a stout round tower that flares into a lit control room, with a radar on top."""
    x, y = at
    shaft = oa.lathe(tag, (x, y, 0), (0, 0, 1), [(0.0, 3.6), (0.6, 3.6), (1.4, 3.0), (height - 5.2, 2.5), (height - 4.0, 4.3), (height - 3.6, 4.5)], SLATE, segments=24)
    oa.densify(shaft, 1.6)
    b.shell(shaft, seed=21)
    b.window(oa.lathe(tag + "_glass", (x, y, 0), (0, 0, 1), [(height - 3.6, 4.48), (height - 2.2, 4.75), (height - 2.2, 4.4), (height - 3.6, 4.2)], WARM, segments=24))
    for k in range(12):
        a = 2 * math.pi * k / 12
        b.frame(beam(tag + "_mullion", (x + math.cos(a) * 4.52, y + math.sin(a) * 4.52, height - 3.65), (x + math.cos(a) * 4.8, y + math.sin(a) * 4.8, height - 2.15), 0.16, DARK))
    roof = oa.lathe(tag + "_roof", (x, y, 0), (0, 0, 1), [(height - 2.2, 4.4), (height - 2.2, 5.0), (height - 1.9, 5.0), (height - 1.2, 3.2), (height - 1.0, 1.2), (height - 1.0, 0.0)], b.accent, segments=24)
    oa.densify(roof, 1.6)
    b.shell(roof, seed=22)
    for z in (2.6, height - 5.6):
        b.shell(oa.cylinder(tag + "_band", (x, y, z), (x, y, z + 0.3), 2.95 if z < 5 else 2.58, b.accent, segments=24), seed=23)
    for k in range(4):
        a = 2 * math.pi * (k + 0.5) / 4
        b.window(oa.box(tag + "_slit", (x + math.cos(a) * 2.72, y + math.sin(a) * 2.72, height * 0.42), (0.1, 0.5, 1.5), WARM, bevel=0.02, segments=1, rot=(0, 0, math.degrees(a))))
    # radar: a slowly leaning bar dish on a pedestal, and a cluster of aerials
    b.frame(oa.cylinder(tag + "_pedestal", (x, y, height - 1.0), (x, y, height + 0.4), 0.4, STEEL, segments=10, bevel=0.05))
    dish = oa.box(tag + "_radar", (0, 0, 0), (4.2, 0.25, 1.5), WHITE, bevel=0.1)
    dish.data.transform(Matrix.Translation((x, y, height + 1.2)) @ Matrix.Rotation(0.5, 4, 'Z') @ Matrix.Rotation(math.radians(-14), 4, 'X'))
    b.shell(dish, seed=24)
    for k, (ox, oy, hgt) in enumerate(((2.2, 1.6, 2.6), (-2.0, 2.2, 1.8), (-2.6, -1.4, 3.2))):
        b.frame(oa.cylinder(tag + "_aerial", (x + ox, y + oy, height - 1.3), (x + ox, y + oy, height - 1.3 + hgt), 0.04, DARK, segments=5))
        b.lamp(oa.sphere(tag + "_aerial_light", (x + ox, y + oy, height - 1.25 + hgt), 0.1, RED, segments=6))
    made = [b.shell(oa.box(tag + "_porch", (0, 3.7, 1.4), (2.8, 1.8, 2.8), CREAM, bevel=0.14)),
            b.detail(oa.box(tag + "_door", (0, 4.62, 1.2), (1.5, 0.08, 2.2), DARK, bevel=0.03, segments=1)),
            b.detail(oa.box(tag + "_door_stripe", (0, 4.65, 1.7), (1.5, 0.04, 0.18), b.accent, bevel=0.01, segments=1)),
            b.lamp(oa.box(tag + "_door_lamp", (0, 4.6, 2.6), (1.0, 0.12, 0.12), WARM, bevel=0.02, segments=1))]
    m = moved(made, (x, y, 0), yaw)
    b.hit_box(m @ Vector((0, 3.7, 1.4)), (2.8, 1.8, 2.8), yaw)
    b.hit_drum((x, y, 0), 3.6, 1.4, radius_top=3.0, sides=12)
    b.hit_drum((x, y, 1.4), 3.0, height - 6.6, radius_top=2.5, sides=12)
    b.hit_drum((x, y, height - 5.2), 2.6, 3.0, radius_top=5.0, sides=12)
    b.hit_drum((x, y, height - 2.2), 5.0, 1.2, radius_top=1.2, sides=12)
    b.socket("Door", m @ Vector((0, 5.8, 0)))


# ------------------------------------------------------------------ the five bases

def relay_outpost():
    """Small. A lonely listening post: one living pod, a tall mast with a dish, two solar panels."""
    b = Base("BaseRelay", "small", ORANGE)
    m = hab(b, (-3.6, 4.6), 200)
    b.socket("Door", m @ Vector((0, 4.2, 0)))
    mast(b, (5.6, 4.6), 13.0)
    solar(b, (6.2, -3.4), 150)
    solar(b, (2.6, -7.2), 165)
    crates(b, (-6.0, -3.2), 4, 1.2)
    barrels(b, (-1.8, -7.6), 3)
    lamp_post(b, (-8.2, 1.6), aim=-100)
    lamp_post(b, (1.6, 8.6), 5.0, aim=185)
    b.frame(pipe("cable", [(4.9, 4.2, 0.06), (2.0, 5.2, 0.05), (-0.9, 4.2, 0.06)], 0.05, DARK, sides=5))
    for p in ((0, 0), (2.5, 1.5), (-2.5, -1.0), (0.5, -3.5)):
        b.socket("Spawn", (p[0], p[1], 0))
    return b


def fuel_depot():
    """Small. Three round tanks on legs, a pump house and a lookout tower."""
    b = Base("BaseDepot", "small", YELLOW)
    spots = [(-5.6, 4.6), (0.0, 7.0), (5.6, 4.6)]
    for i, p in enumerate(spots):
        ball_tank(b, p, 1.9, tag="ball%d" % i)
    b.frame(pipe("manifold", [(-5.6, 4.6, 0.5), (-3.4, 3.2, 0.35), (0.0, 4.2, 0.35), (3.4, 3.2, 0.35), (5.6, 4.6, 0.5)], 0.12, b.accent, sides=7))
    b.frame(pipe("feed", [(0.0, 7.0, 0.5), (0.0, 4.2, 0.35)], 0.12, b.accent, sides=7))
    b.frame(pipe("run", [(3.4, 3.2, 0.35), (4.6, -0.5, 0.35), (4.6, -2.4, 0.6)], 0.1, STEEL, sides=7))
    m = shed(b, (4.6, -4.6), 270, w=4.2, d=3.2, h=2.6, tag="pump")
    b.socket("Door", m @ Vector((0, 2.8, 0)))
    watchtower(b, (-5.0, -3.8), 40, 6.0)
    barrels(b, (-1.0, -8.0), 4)
    crates(b, (1.6, -7.4), 2, 0.6)
    lamp_post(b, (-8.4, 1.6), aim=-95)
    for p in ((0, 0), (-2.0, 1.0), (2.2, -1.5), (-1.0, -3.5)):
        b.socket("Spawn", (p[0], p[1], 0))
    return b


def dome_colony():
    """Medium. Three pressure domes joined by crawlways (one a lit greenhouse) round a landing circle."""
    b = Base("BaseDomes", "medium", TEAL)
    door = dome(b, (0.0, 10.4), 5.6, porch_yaw=180, tag="main")
    b.socket("Door", door)
    dome(b, (-11.4, 0.6), 4.2, porch_yaw=250, tag="west")
    dome(b, (11.4, 0.6), 4.4, glass=True, tag="green")
    # what grows inside the greenhouse: a few dark shapes seen through the glass
    for k in range(5):
        a = 2 * math.pi * k / 5
        o = nat.lobed("crop", (11.4 + math.cos(a) * 2.0, 0.6 + math.sin(a) * 2.0, 1.5), [((0, 0, 0), (0.9, 0.9, 1.1))], (0.10, 0.34, 0.16), n=3, lump=0.2, seed=k)
        nat.leaf_paint(o, (0.05, 0.2, 0.1), (0.10, 0.34, 0.16), (0.2, 0.5, 0.2), seed=k)
        b.groups["Detail"].append(o)
    tunnel(b, (-3.6, 7.0), (-8.6, 3.4), tag="tw")
    tunnel(b, (3.6, 7.0), (8.4, 3.6), tag="te")
    pad(b, (0.0, -3.0), 4.2)
    tank(b, (-6.4, -12.6), 1.4, 4.6, tag="tank0")
    tank(b, (-3.0, -14.6), 1.4, 5.4, tag="tank1")
    b.frame(pipe("tank_pipe", [(-6.4, -11.2, 0.3), (-8.6, -6.0, 0.3), (-10.4, -3.4, 0.5)], 0.11, STEEL, sides=7))
    solar(b, (5.0, -13.6), 170, tag="s0")
    solar(b, (9.2, -11.4), 160, tag="s1")
    solar(b, (12.6, -8.2), 150, tag="s2")
    mast(b, (12.4, 9.2), 11.0)
    crates(b, (-12.4, -6.8), 5, 1.3)
    crates(b, (6.6, -6.2), 3, 0.9, loot=False)
    barrels(b, (-9.6, 9.8), 3)
    lamp_post(b, (-7.0, -5.0), aim=-60)
    lamp_post(b, (7.4, -1.0), aim=110)
    for p in ((0, 2.2), (-5.0, -1.0), (5.0, 1.0), (-2.0, -8.6), (3.0, -8.8), (5.6, -3.0)):
        b.socket("Spawn", (p[0], p[1], 0))
    return b


def mining_works():
    """Medium. A winding tower over a shaft, an ore belt climbing to a bin, a workshop and heaps of spoil."""
    b = Base("BaseMine", "medium", RUST)
    headframe(b, (-9.0, 6.4), 270)
    conveyor(b, (-4.6, 1.2), (7.8, 8.4), 7.4)
    hopper(b, (9.6, 9.0), 7.0)
    spoil(b, (12.0, -0.6), 3.6, 2.6, 1)
    spoil(b, (9.4, 3.0), 2.4, 1.9, 2)
    spoil(b, (-12.4, -3.2), 2.5, 2.0, 3)
    m = shed(b, (-6.6, -9.4), 20, w=7.4, d=5.0, h=3.3)
    b.socket("Door", m @ Vector((0, 4.2, 0)))
    cart(b, (0.6, -11.6), 12)
    tank(b, (7.4, -13.4), 1.5, 4.8)
    watchtower(b, (11.6, -7.8), 130, 6.5)
    crates(b, (2.2, 10.6), 4, 1.3)
    barrels(b, (-14.6, 3.4), 4)
    lamp_post(b, (-2.0, -5.6), 6.0, aim=10)
    lamp_post(b, (5.6, -2.0), 6.0, aim=-140)
    pad(b, (1.0, -1.4), 3.6)
    for p in ((2.5, 3.2), (-3.8, -1.0), (4.6, -5.2), (-1.0, -7.0), (-6.0, -3.0), (6.0, 1.0)):
        b.socket("Spawn", (p[0], p[1], 0))
    return b


def stronghold():
    """Large. A walled camp: gate, four lookout towers, a command tower, barracks, a hangar and a landing circle."""
    b = Base("BaseStronghold", "large", (0.22, 0.34, 0.52))   # a plain blue: nothing here says whose it is
    ring = 25.5
    corners = [(math.sin(math.radians(22.5 + 45 * k)) * ring, math.cos(math.radians(22.5 + 45 * k)) * ring) for k in range(8)]
    for k in range(8):
        a, c = corners[k], corners[(k + 1) % 8]
        if k == 7:   # the north side is the gate
            continue
        if k == 3:   # a gap in the south wall: a second way in, on foot
            mid = ((a[0] + c[0]) / 2, (a[1] + c[1]) / 2)
            wall(b, a, (a[0] + (mid[0] - a[0]) * 0.72, a[1] + (mid[1] - a[1]) * 0.72), tag="wall%da" % k)
            wall(b, (c[0] + (mid[0] - c[0]) * 0.72, c[1] + (mid[1] - c[1]) * 0.72), c, tag="wall%db" % k)
            continue
        wall(b, a, c, tag="wall%d" % k)
    north_a, north_c = corners[7], corners[0]
    wall(b, north_a, (-4.4, north_a[1]), tag="wall_nw")
    wall(b, (4.4, north_c[1]), north_c, tag="wall_ne")
    gate(b, (0.0, north_a[1]), 0, width=6.4)
    for i, k in enumerate((1, 3, 5, 7)):
        cx, cy = corners[k]
        watchtower(b, (cx * 0.86, cy * 0.86), math.degrees(math.atan2(-cx, cy)) + 180, 7.4, tag="tower%d" % i)
    command_tower(b, (-11.5, 6.5), 120, 15.0)
    m = hab(b, (12.8, 11.4), 250, 7.4, 1.7, tag="bunk0")
    hab(b, (15.8, 1.6), 265, 7.4, 1.7, tag="bunk1")
    m = shed(b, (-12.4, -11.6), 35, w=9.6, d=6.4, h=4.2, tag="hangar")
    b.socket("Loot", m @ Vector((0, 4.6, 0)))
    tank(b, (10.2, -15.4), 1.7, 6.0, tag="tank0")
    tank(b, (14.4, -12.2), 1.7, 5.2, tag="tank1")
    ball_tank(b, (17.2, -5.6), 1.9, tag="ball0")
    mast(b, (-19.2, -2.2), 12.0)
    solar(b, (-3.4, -19.6), 180, tag="s0")
    solar(b, (0.8, -20.2), 180, tag="s1")
    pad(b, (1.0, -1.0), 6.2)
    crates(b, (-3.6, 10.4), 5, 1.5)
    crates(b, (8.4, -6.6), 4, 1.3)
    crates(b, (-8.6, -3.6), 3, 1.0, loot=False)
    barrels(b, (6.4, 14.6), 4)
    barrels(b, (-17.0, 12.0), 2)
    for p, aim in (((-6.2, 15.6), 200), ((7.4, 6.8), 120), ((-7.2, -8.0), -30), ((9.6, -10.2), 40)):
        lamp_post(b, p, 6.5, aim=aim)
    for p in ((1.0, 6.4), (-6.5, 2.5), (8.6, 1.0), (1.0, -9.0), (-5.0, -5.6), (7.4, -4.0), (0.0, 15.0), (-9.0, -1.0), (10.0, 6.0), (3.5, 9.5)):
        b.socket("Spawn", (p[0], p[1], 0))
    return b


BASES = [relay_outpost, fuel_depot, dome_colony, mining_works, stronghold]


def finish(b, write):
    """Checks a base against its caps and its floor, then writes it."""
    parts = [(name, objs) for name, objs in b.groups.items() if objs]
    drawn = sum(nat.triangles(objs) for _, objs in parts)
    solid = nat.triangles(b.hits)
    reach = 0.0
    low = 0.0
    for _, objs in parts:
        for o in objs:
            for v in o.data.vertices:
                reach = max(reach, math.hypot(v.co.x, v.co.y))
                low = min(low, v.co.z)
    print("OA %s (%s): %d triangles drawn in %d pieces, %d solid shapes of %d triangles, reaches %.1f m of %.0f, %d sockets"
          % (b.name, b.size, drawn, len(parts), len(b.hits), solid, reach, RADIUS[b.size], len(b.sockets)))
    problems = []
    if drawn > CAPS[b.size]:
        problems.append("%d triangles is over the %s cap of %d" % (drawn, b.size, CAPS[b.size]))
    if solid > HIT_CAP * (2 if b.size == "large" else 1):
        problems.append("%d solid triangles is over the cap" % solid)
    if reach > RADIUS[b.size] - 0.3:
        problems.append("it reaches %.1f m, off the edge of its %.0f m floor" % (reach, RADIUS[b.size]))
    if low < -0.35:
        problems.append("something hangs %.2f m below the floor" % -low)
    for p in problems:
        print("OA PROBLEM %s: %s" % (b.name, p))
    if write and not problems:
        oa.export(os.path.join(OUT, b.name + ".bytes"), parts + [("Hit", b.hits)], b.sockets)
    return problems


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    preview = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else None
    failed = False
    for make in BASES:
        oa.reset_scene()
        b = make()
        if only and b.name not in only:
            continue
        failed = bool(finish(b, write=not only)) or failed
        if preview:
            import preview_bases
            preview_bases.render(preview, b, RADIUS[b.size])
    if failed:
        raise SystemExit("Some bases were not written: see the lines marked PROBLEM.")


if __name__ == "__main__":
    main()
