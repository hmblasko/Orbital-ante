"""Builds Earth's scenery: trees, plants, rocks and mountain crags.

    blender -b -P tools/blender/build_scenery.py                        (writes Resources/Models/Earth*.bytes)
    blender -b -P tools/blender/build_scenery.py -- --preview <folder>  (also renders pictures)
    blender -b -P tools/blender/build_scenery.py -- --only Conifer,Palm --preview <folder>   (quick look, writes nothing)

Each kind has four variants. A piece is named after the game's FeatureKind and the variant: "Conifer0".
Every piece is 1 unit tall and stands on the origin; the game scales it by its height in metres
(Scripts/Runtime/Planets/SceneryModels.cs says which file holds which kind).

What is solid is described apart from what is drawn:
- trees: three sockets per piece, "<piece>A" and "<piece>B" (the two ends of the trunk) and "<piece>R"
  (x = the trunk's radius), which the game turns into one capsule;
- rocks: a second, much simpler closed piece called "<piece>Hit".
Small plants have neither: you walk through them.
"""
import bpy, bmesh, sys, os, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import oa_blender as oa
import oa_nature as nat
from oa_nature import mix
from mathutils import Vector, Matrix, noise

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "unity", "Assets", "OrbitalAnte", "Resources", "Models"))

# Triangle caps (the rule test SceneryModelTests enforces the same numbers).
CAP = {"tree": 1700, "plant": 620, "rock": 1400, "hit": 220}

BARK = (0.37, 0.26, 0.18)
BARK_DARK = (0.24, 0.17, 0.13)
BARK_RED = (0.52, 0.26, 0.16)
BARK_PINE = (0.62, 0.36, 0.20)
BIRCH = (0.91, 0.89, 0.83)
BIRCH_MARK = (0.16, 0.14, 0.13)
SILVER = (0.63, 0.61, 0.57)
MOSS = (0.38, 0.50, 0.20)
SNOW = (0.95, 0.97, 1.0)


class Tree:
    """What one variant is made of while it is being built."""
    def __init__(self, name):
        self.name = name
        self.objects = []
        self.hit = None        # (A, B, radius) for trees
        self.hit_objects = []  # closed simple pieces for rocks

    def add(self, o):
        self.objects.append(o)
        return o

    def fit(self):
        """Scales the whole thing so it is exactly 1 tall, standing on the ground."""
        top = max((o.data.vertices[i].co.z for o in self.objects for i in range(len(o.data.vertices))))
        s = 1.0 / top
        m = Matrix.Scale(s, 4)
        for o in self.objects + self.hit_objects:
            o.data.transform(m)
            o.data.update()
        if self.hit:
            a, b, r = self.hit
            self.hit = (a * s, b * s, r * s)
        return self


# ------------------------------------------------------------------ wood

def trunk_path(rng, top, lean=0.0, sway=0.02, n=7, bearing=None):
    ax = rng.uniform(0, 6.28) if bearing is None else bearing
    phase = rng.uniform(0, 6.28)
    pts = []
    for i in range(n):
        t = i / (n - 1)
        z = -0.07 + (top + 0.07) * t
        off = lean * t * t
        s = sway * math.sin(t * math.pi * 1.4 + phase) * t
        pts.append(Vector((math.cos(ax) * off + math.cos(ax + 1.57) * s, math.sin(ax) * off + math.sin(ax + 1.57) * s, z)))
    return pts


def trunk_radii(r, n, flare=1.9, taper=0.55):
    out = []
    for i in range(n):
        t = i / (n - 1)
        out.append(r * (taper + (1 - taper) * (1 - t)) * (1 + (flare - 1) * max(0.0, 1 - t * 4.5) ** 2))
    return out


def roots(count, depth=0.22):
    """Buttress roots: the foot of the trunk swells into a few toes."""
    return lambda a, t: depth * max(0.0, 1 - t * 4.0) ** 1.5 * math.cos(count * a)


def bough(tree, name, start, end, r, colour, seed, rise=0.06):
    start, end = Vector(start), Vector(end)
    midp = (start + end) * 0.5 + Vector((0, 0, rise))
    o = nat.tube(name, [start, midp, end], [r, r * 0.7, r * 0.42], colour, sides=5)
    nat.bark_paint(o, colour, seed=seed, root_dark=0.0)
    return tree.add(o)


def at(path, t):
    """A point part-way up a trunk path (t from 0 at the root to 1 at the top)."""
    f = t * (len(path) - 1)
    i = min(int(f), len(path) - 2)
    return path[i].lerp(path[i + 1], f - i)


# ------------------------------------------------------------------ leafy trees

def clump(tree, name, centre, radii, palette, seed, n=4, lump=0.16, drift=None, snow=None, flat=0.45):
    """One rounded mass of leaves with a tidy underside. A crown is several of these, overlapping."""
    o = nat.lobed(name, centre, [((0, 0, 0), radii)], palette[1], n=n, lump=lump, grain=2.6, seed=seed, flat=-radii[2] * flat)
    nat.leaf_paint(o, palette[0], palette[1], palette[2], seed=seed, drift=drift, snow=snow)
    return tree.add(o)


def leafy(name, seed, crown_z, trunk_r, core, lobes, spread, lobe_r, squash, rise, palette, bark=BARK,
          lean=0.0, sway=0.025, wind=0.0, lump=0.16, bands=None, moss=MOSS, root_count=5,
          stack=0.0, drift=None, snow=None, cap=None):
    """A trunk that forks into boughs, each carrying one clump of the crown, round a bigger clump in the middle."""
    rng = random.Random(seed)
    tree = Tree(name)
    bearing = rng.uniform(0, 6.28)
    path = trunk_path(rng, crown_z, lean, sway, bearing=bearing)
    radii = trunk_radii(trunk_r, len(path))
    t = nat.tube(name + "_trunk", path, radii, bark, sides=8, flute=roots(root_count))
    nat.bark_paint(t, bark, seed=seed, moss=moss, bands=bands)
    tree.add(t)
    top = path[-1]
    centre = top + Vector((0, 0, core[2] * 0.6))
    clump(tree, name + "_core", centre, core, palette, seed, n=4, lump=lump, drift=drift, snow=snow)
    base = rng.uniform(0, 6.28)
    for k in range(lobes):
        a = base + k * 6.2832 / lobes + rng.uniform(-0.25, 0.25)
        d = spread * rng.uniform(0.85, 1.12)
        off = Vector((math.cos(a) * d + math.cos(bearing) * wind, math.sin(a) * d + math.sin(bearing) * wind,
                      rng.uniform(*rise) + stack * k))
        if stack > 0:
            off.x *= max(0.3, 1 - 0.15 * k)
            off.y *= max(0.3, 1 - 0.15 * k)
        r = lobe_r * rng.uniform(0.82, 1.14) * (max(0.55, 1 - 0.09 * k) if stack > 0 else 1.0)
        shade = rng.uniform(0.9, 1.06)
        pal = tuple(tuple(min(1.0, c * shade) for c in col) for col in palette)
        clump(tree, name + "_clump%d" % k, centre + off, (r, r * rng.uniform(0.9, 1.1), r * squash), pal, seed + 1 + k,
              lump=lump, drift=drift, snow=snow)
        start = at(path, rng.uniform(0.6, 0.85))
        bough(tree, name + "_bough%d" % k, start, centre + off + Vector((0, 0, -r * squash * 0.3)),
              trunk_r * 0.5, bark, seed + k)
    if cap:
        clump(tree, name + "_cap", centre + Vector((rng.uniform(-0.04, 0.04), rng.uniform(-0.04, 0.04), core[2] * 0.75)),
              (cap, cap, cap * 0.85), palette, seed + 40, lump=lump, drift=drift, snow=snow)
    tree.hit = (Vector((path[1].x, path[1].y, 0)), Vector(top), trunk_r * 1.15)
    return tree.fit()


GREENS = [
    ((0.11, 0.27, 0.15), (0.27, 0.52, 0.20), (0.60, 0.74, 0.28)),
    ((0.12, 0.30, 0.13), (0.33, 0.56, 0.19), (0.68, 0.78, 0.30)),
    ((0.09, 0.25, 0.16), (0.22, 0.47, 0.22), (0.50, 0.70, 0.30)),
    ((0.14, 0.29, 0.12), (0.36, 0.53, 0.17), (0.72, 0.76, 0.27)),
]
GREEN_DRIFT = (0.52, 0.62, 0.16)


def broadleaf(v):
    name = "Broadleaf%d" % v
    p = GREENS[v]
    if v == 0:   # a full round oak
        return leafy(name, 101, 0.38, 0.050, (0.25, 0.25, 0.23), 5, 0.27, 0.17, 0.85, (-0.10, 0.04), p, drift=GREEN_DRIFT, cap=0.15)
    if v == 1:   # wide and spreading, low boughs
        return leafy(name, 112, 0.32, 0.058, (0.27, 0.27, 0.19), 6, 0.36, 0.17, 0.78, (-0.10, 0.02), p, drift=GREEN_DRIFT, lean=0.04)
    if v == 2:   # tall and egg-shaped
        return leafy(name, 123, 0.34, 0.040, (0.19, 0.19, 0.22), 5, 0.15, 0.14, 1.0, (-0.08, -0.02), p, stack=0.085, drift=GREEN_DRIFT)
    # windswept: the crown trails off to one side
    return leafy(name, 134, 0.42, 0.048, (0.22, 0.22, 0.19), 5, 0.24, 0.16, 0.8, (-0.08, 0.04), p, lean=0.16, wind=0.2, drift=GREEN_DRIFT, cap=0.13)


AUTUMN = [
    ((0.42, 0.09, 0.08), (0.76, 0.20, 0.11), (0.97, 0.50, 0.16), (0.98, 0.72, 0.20)),   # red
    ((0.50, 0.20, 0.06), (0.88, 0.44, 0.10), (0.99, 0.74, 0.22), (0.80, 0.20, 0.10)),   # orange
    ((0.55, 0.36, 0.06), (0.93, 0.68, 0.13), (1.00, 0.88, 0.36), (0.90, 0.42, 0.10)),   # gold
    ((0.36, 0.10, 0.14), (0.66, 0.16, 0.20), (0.92, 0.36, 0.24), (0.98, 0.62, 0.20)),   # crimson
]


def maple(v):
    name = "Maple%d" % v
    p = AUTUMN[v]
    shapes = [
        dict(crown_z=0.36, trunk_r=0.044, core=(0.28, 0.28, 0.27), lobes=5, spread=0.26, lobe_r=0.22, squash=0.9, rise=(-0.02, 0.12)),
        dict(crown_z=0.33, trunk_r=0.048, core=(0.30, 0.30, 0.24), lobes=6, spread=0.31, lobe_r=0.22, squash=0.8, rise=(-0.04, 0.08)),
        dict(crown_z=0.38, trunk_r=0.040, core=(0.22, 0.22, 0.26), lobes=4, spread=0.17, lobe_r=0.19, squash=1.0, rise=(0.0, 0.10), stack=0.06),
        dict(crown_z=0.35, trunk_r=0.046, core=(0.27, 0.27, 0.25), lobes=5, spread=0.28, lobe_r=0.21, squash=0.85, rise=(-0.03, 0.10), lean=0.08),
    ]
    return leafy(name, 200 + v * 11, palette=p[:3], drift=p[3], bark=BARK_DARK, moss=None, **shapes[v])


BLOSSOM = [
    ((0.74, 0.40, 0.52), (0.95, 0.66, 0.74), (1.00, 0.90, 0.92), (1.0, 0.80, 0.86)),
    ((0.80, 0.50, 0.60), (0.98, 0.76, 0.82), (1.00, 0.95, 0.95), (0.96, 0.62, 0.72)),
    ((0.66, 0.36, 0.56), (0.90, 0.60, 0.78), (1.00, 0.86, 0.94), (1.0, 0.78, 0.82)),
    ((0.82, 0.56, 0.60), (0.99, 0.82, 0.84), (1.00, 0.97, 0.96), (0.97, 0.66, 0.74)),
]


def blossom(v):
    name = "Blossom%d" % v
    p = BLOSSOM[v]
    shapes = [
        dict(crown_z=0.38, trunk_r=0.046, core=(0.28, 0.28, 0.24), lobes=6, spread=0.29, lobe_r=0.20, squash=0.85, rise=(-0.02, 0.08)),
        dict(crown_z=0.42, trunk_r=0.042, core=(0.26, 0.26, 0.23), lobes=5, spread=0.27, lobe_r=0.20, squash=0.85, rise=(-0.03, 0.10), lean=0.10),
        dict(crown_z=0.36, trunk_r=0.050, core=(0.29, 0.29, 0.22), lobes=6, spread=0.31, lobe_r=0.19, squash=0.82, rise=(-0.05, 0.05)),
        dict(crown_z=0.40, trunk_r=0.044, core=(0.26, 0.26, 0.23), lobes=5, spread=0.27, lobe_r=0.20, squash=0.85, rise=(-0.02, 0.09), lean=0.14, wind=0.12),
    ]
    return leafy(name, 300 + v * 13, palette=p[:3], drift=p[3], bark=(0.22, 0.15, 0.14), moss=None, sway=0.05, lump=0.14, **shapes[v])


BIRCH_LEAF = [
    ((0.30, 0.44, 0.14), (0.58, 0.70, 0.22), (0.86, 0.90, 0.40)),
    ((0.52, 0.42, 0.08), (0.90, 0.76, 0.16), (1.00, 0.93, 0.44)),
    ((0.26, 0.42, 0.16), (0.50, 0.68, 0.26), (0.80, 0.90, 0.44)),
    ((0.44, 0.44, 0.10), (0.78, 0.76, 0.18), (0.98, 0.92, 0.42)),
]


def birch(v):
    name = "Birch%d" % v
    p = BIRCH_LEAF[v]
    leans = [0.03, -0.07, 0.10, 0.0]
    return leafy(name, 400 + v * 7, crown_z=0.44, trunk_r=0.026, core=(0.15, 0.15, 0.19), lobes=5, spread=0.15,
                 lobe_r=0.125, squash=1.1, rise=(-0.10, -0.03), stack=0.07, palette=p, bark=BIRCH, bands=BIRCH_MARK,
                 moss=None, lean=leans[v], sway=0.035, lump=0.2, root_count=4,
                 drift=(0.95, 0.85, 0.3))


# ------------------------------------------------------------------ flat-topped trees

def pads(name, seed, crown_z, trunk_r, count, spread, pad_r, thick, palette, bark, lean=0.0, fork=0.5,
         moss=None, stubs=0, drift=None, n=5):
    """A tall bare trunk carrying separate flat pads of leaves: umbrella pines and savanna thorn trees."""
    rng = random.Random(seed)
    tree = Tree(name)
    bearing = rng.uniform(0, 6.28)
    path = trunk_path(rng, crown_z, lean, 0.03, bearing=bearing)
    t = nat.tube(name + "_trunk", path, trunk_radii(trunk_r, len(path), taper=0.6), bark, sides=8, flute=roots(4, 0.16))
    nat.bark_paint(t, bark, seed=seed, moss=moss)
    tree.add(t)
    top = path[-1]
    base = rng.uniform(0, 6.28)
    for k in range(count):
        a = base + k * 6.2832 / max(1, count - 1) + rng.uniform(-0.3, 0.3)
        d = 0.0 if k == 0 else spread * rng.uniform(0.8, 1.1)
        c = top + Vector((math.cos(a) * d, math.sin(a) * d, (0.06 if k == 0 else rng.uniform(-0.09, 0.0))))
        r = pad_r * (1.0 if k == 0 else rng.uniform(0.62, 0.85))
        lob = [((0, 0, 0), (r, r, thick))]
        for j in range(4):
            b = rng.uniform(0, 6.28)
            lob.append(((math.cos(b) * r * 0.6, math.sin(b) * r * 0.6, rng.uniform(-0.01, 0.02)),
                        (r * 0.55, r * 0.55, thick * rng.uniform(0.8, 1.25))))
        pad = nat.lobed(name + "_pad%d" % k, c, lob, palette[1], n=n if k else n + 1, lump=0.2, seed=seed + k, flat=-thick * 0.35)
        nat.leaf_paint(pad, palette[0], palette[1], palette[2], seed=seed + k, drift=drift)
        tree.add(pad)
        start = at(path, fork + rng.uniform(0.0, 0.3)) if k else at(path, 0.9)
        bough(tree, name + "_bough%d" % k, start, c + Vector((0, 0, -thick * 0.2)), trunk_r * 0.55, bark, seed + k, rise=0.03)
    for s in range(stubs):
        a = rng.uniform(0, 6.28)
        start = at(path, rng.uniform(0.35, 0.7))
        end = start + Vector((math.cos(a) * 0.09, math.sin(a) * 0.09, 0.03))
        stub = nat.tube(name + "_stub%d" % s, [start, end], [trunk_r * 0.3, trunk_r * 0.12], SILVER, sides=4)
        nat.bark_paint(stub, SILVER, seed=seed + s, root_dark=0)
        tree.add(stub)
    tree.hit = (Vector((path[1].x, path[1].y, 0)), Vector(top), trunk_r * 1.15)
    return tree.fit()


PINE_GREEN = ((0.07, 0.19, 0.14), (0.15, 0.36, 0.20), (0.40, 0.56, 0.24))


def pine(v):
    name = "Pine%d" % v
    spec = [
        dict(crown_z=0.74, trunk_r=0.034, count=3, spread=0.16, pad_r=0.23, thick=0.085, lean=0.05, stubs=2),
        dict(crown_z=0.70, trunk_r=0.038, count=4, spread=0.19, pad_r=0.25, thick=0.09, lean=-0.10, stubs=1),
        dict(crown_z=0.78, trunk_r=0.030, count=2, spread=0.13, pad_r=0.20, thick=0.08, lean=0.02, stubs=3),
        dict(crown_z=0.68, trunk_r=0.036, count=4, spread=0.21, pad_r=0.22, thick=0.085, lean=0.16, stubs=2),
    ][v]
    return pads(name, 500 + v * 9, palette=PINE_GREEN, bark=BARK_PINE, fork=0.6, drift=(0.30, 0.46, 0.16), **spec)


ACACIA_GREEN = ((0.20, 0.30, 0.10), (0.42, 0.52, 0.16), (0.72, 0.74, 0.28))


def acacia(v):
    name = "Acacia%d" % v
    spec = [
        dict(crown_z=0.74, trunk_r=0.040, count=4, spread=0.36, pad_r=0.33, thick=0.095, lean=0.08),
        dict(crown_z=0.70, trunk_r=0.044, count=4, spread=0.42, pad_r=0.31, thick=0.09, lean=-0.14),
        dict(crown_z=0.78, trunk_r=0.036, count=3, spread=0.30, pad_r=0.35, thick=0.1, lean=0.04),
        dict(crown_z=0.72, trunk_r=0.042, count=4, spread=0.40, pad_r=0.30, thick=0.09, lean=0.2),
    ][v]
    return pads(name, 600 + v * 9, palette=ACACIA_GREEN, bark=(0.42, 0.33, 0.24), fork=0.3, drift=(0.62, 0.60, 0.20), **spec)


# ------------------------------------------------------------------ firs

FIR = [
    ((0.05, 0.17, 0.16), (0.10, 0.32, 0.23), (0.30, 0.52, 0.27)),
    ((0.06, 0.20, 0.15), (0.13, 0.36, 0.21), (0.36, 0.56, 0.25)),
    ((0.05, 0.16, 0.18), (0.09, 0.30, 0.26), (0.26, 0.50, 0.32)),
    ((0.07, 0.20, 0.14), (0.15, 0.35, 0.19), (0.40, 0.55, 0.24)),
]


def conifer(v, snowy=False):
    name = ("SnowConifer%d" if snowy else "Conifer%d") % v
    rng = random.Random(700 + v * 17 + (50 if snowy else 0))
    tiers = [7, 8, 6, 7][v]
    width = [0.25, 0.20, 0.29, 0.23][v]
    bare = [0.13, 0.19, 0.10, 0.16][v]
    lean = [0.0, 0.02, 0.0, 0.06][v]
    tree = Tree(name)
    bearing = rng.uniform(0, 6.28)
    path = trunk_path(rng, 0.9, lean, 0.01, bearing=bearing, n=6)
    t = nat.tube(name + "_trunk", path, trunk_radii(0.034, len(path), taper=0.25), BARK_DARK, sides=7, flute=roots(4, 0.15))
    nat.bark_paint(t, BARK_DARK, seed=v, moss=None if snowy else MOSS)
    tree.add(t)
    p = FIR[v]
    step = (1.0 - bare) / (tiers + 0.9)
    for i in range(tiers):
        f = i / (tiers - 1)
        z = bare + step * i
        r = width * (1.0 - 0.80 * f ** 0.9) * rng.uniform(0.93, 1.07)
        tip = z + step * (2.5 if i < tiers - 1 else 1.9)
        centre = at(path, min(1.0, (z + 0.07) / 0.97))
        s = nat.skirt(name + "_tier%d" % i, z, tip, r, p[1], sides=14 if i < tiers - 2 else 9,
                      droop=0.34 if snowy else 0.24, scallop=0.15, seed=rng.randrange(10 ** 6),
                      tip_radius=0.16 if i < tiers - 1 else 0.02, tilt=0.06)
        s.data.transform(Matrix.Translation((centre.x, centre.y, 0)))
        shade = 0.86 + 0.2 * f
        nat.leaf_paint(s, tuple(c * shade for c in p[0]), tuple(c * shade for c in p[1]), p[2], seed=i + v * 10,
                       mottle=0.12, snow=SNOW if snowy else None, snow_line=0.42, drift=None if snowy else (0.22, 0.44, 0.30))
        tree.add(s)
    tree.hit = (Vector((path[1].x, path[1].y, 0)), at(path, 0.62), 0.045)
    return tree.fit()


def giant(v):
    """A giant red-barked tree: a fluted column of a trunk, the crown starting far overhead."""
    name = "GiantTree%d" % v
    rng = random.Random(800 + v * 5)
    tree = Tree(name)
    trunk_r = [0.062, 0.070, 0.056, 0.066][v]
    path = trunk_path(rng, 0.93, [0.0, 0.02, -0.015, 0.03][v], 0.008, n=9)
    flutes = [7, 8, 6, 7][v]
    t = nat.tube(name + "_trunk", path, trunk_radii(trunk_r, len(path), flare=1.75, taper=0.22), BARK_RED, sides=14,
                 flute=lambda a, tt: 0.26 * max(0.0, 1 - tt * 3.2) ** 1.4 * math.cos(flutes * a) + 0.05 * math.cos(flutes * a) * (1 - tt))
    nat.bark_paint(t, BARK_RED, seed=v, moss=MOSS, grain=20)
    tree.add(t)
    p = FIR[(v + 1) % 4]
    start = [0.46, 0.52, 0.42, 0.5][v]
    clumps = [6, 5, 6, 6][v]
    for i in range(clumps):
        f = i / (clumps - 1)
        z = start + (0.95 - start) * f
        a = i * 2.4 + rng.uniform(-0.4, 0.4)
        reach = (0.20 - 0.16 * f) * rng.uniform(0.85, 1.15)
        c = at(path, min(1.0, (z + 0.07) / 1.0)) + Vector((math.cos(a) * reach * 0.55, math.sin(a) * reach * 0.55, 0))
        r = (0.26 - 0.18 * f) * rng.uniform(0.9, 1.1)
        lob = [((0, 0, 0), (r, r, r * 0.9)),
               ((math.cos(a) * r * 0.7, math.sin(a) * r * 0.7, -r * 0.12), (r * 0.7, r * 0.7, r * 0.6)),
               ((-math.sin(a) * r * 0.6, math.cos(a) * r * 0.6, r * 0.1), (r * 0.6, r * 0.6, r * 0.55))]
        crown = nat.lobed(name + "_clump%d" % i, c, lob, p[1], n=4, lump=0.14, seed=v * 10 + i, flat=-r * 0.3)
        nat.leaf_paint(crown, p[0], p[1], p[2], seed=i, drift=(0.28, 0.46, 0.20))
        tree.add(crown)
        if i < clumps - 1:
            bough(tree, name + "_bough%d" % i, at(path, min(1.0, (z + 0.03) / 1.0)), c + Vector((0, 0, -r * 0.2)), trunk_r * 0.28, BARK_RED, i, rise=0.01)
    tree.hit = (Vector((path[1].x, path[1].y, 0)), at(path, 0.7), trunk_r * 1.2)
    return tree.fit()


def snag(v):
    """A dead tree: silvered wood, a snapped top and a few bare arms."""
    name = "Snag%d" % v
    rng = random.Random(900 + v * 3)
    tree = Tree(name)
    top = [0.78, 0.66, 0.9, 0.72][v]
    path = trunk_path(rng, top, [0.05, -0.12, 0.03, 0.18][v], 0.035, n=7)
    radii = trunk_radii(0.05, len(path), taper=0.5)
    radii[-1] *= 0.55
    t = nat.tube(name + "_trunk", path, radii, SILVER, sides=8, flute=roots(5, 0.2))
    nat.bark_paint(t, SILVER, seed=v, grain=22, moss=None)
    tree.add(t)
    arms = [4, 3, 5, 4][v]
    for k in range(arms):
        a = k * 6.2832 / arms + rng.uniform(-0.5, 0.5)
        start = at(path, 0.45 + 0.5 * k / arms)
        reach = rng.uniform(0.16, 0.3)
        elbow = start + Vector((math.cos(a) * reach * 0.6, math.sin(a) * reach * 0.6, reach * 0.25))
        end = elbow + Vector((math.cos(a + 0.5) * reach * 0.4, math.sin(a + 0.5) * reach * 0.4, reach * rng.uniform(0.5, 0.95)))
        arm = nat.tube(name + "_arm%d" % k, [start, elbow, end], [0.022, 0.015, 0.004], SILVER, sides=5)
        nat.bark_paint(arm, SILVER, seed=k, root_dark=0)
        tree.add(arm)
        twig_end = elbow + Vector((math.cos(a - 0.9) * reach * 0.45, math.sin(a - 0.9) * reach * 0.45, reach * 0.35))
        twig = nat.tube(name + "_twig%d" % k, [elbow, twig_end], [0.011, 0.003], SILVER, sides=4)
        nat.bark_paint(twig, SILVER, seed=k + 9, root_dark=0)
        tree.add(twig)
    tree.hit = (Vector((path[1].x, path[1].y, 0)), Vector(path[-1]), 0.055)
    return tree.fit()


PALM_GREEN = [((0.10, 0.30, 0.14), (0.26, 0.56, 0.20), (0.62, 0.78, 0.30)),
              ((0.12, 0.32, 0.12), (0.32, 0.58, 0.18), (0.70, 0.80, 0.30))]


def palm(v):
    name = "Palm%d" % v
    rng = random.Random(1000 + v * 3)
    tree = Tree(name)
    lean = [0.22, -0.32, 0.08, 0.42][v]
    crown_z = [0.80, 0.77, 0.84, 0.72][v]
    bearing = rng.uniform(0, 6.28)
    n = 11
    pts, radii = [], []
    for i in range(n):
        t = i / (n - 1)
        off = lean * t ** 1.8
        pts.append(Vector((math.cos(bearing) * off, math.sin(bearing) * off, -0.07 + (crown_z + 0.07) * t)))
        radii.append((0.047 - 0.02 * math.sqrt(t)) * (1.12 if i % 2 == 0 else 0.92) * (1.5 if i == 0 else 1.0))
    trunk_colour = (0.56, 0.45, 0.31)
    t = nat.tube(name + "_trunk", pts, radii, trunk_colour, sides=8)
    nat.bark_paint(t, trunk_colour, seed=v, grain=9, moss=None, bands=(0.36, 0.27, 0.18))
    tree.add(t)
    top = pts[-1]
    heart = nat.lobed(name + "_heart", top + Vector((0, 0, 0.005)), [((0, 0, 0), (0.05, 0.05, 0.045))], (0.45, 0.50, 0.22), n=2, lump=0.1, seed=v)
    nat.leaf_paint(heart, (0.30, 0.34, 0.14), (0.48, 0.52, 0.22), (0.66, 0.70, 0.30), seed=v)
    tree.add(heart)
    count = [12, 11, 13, 12][v]
    turn = rng.uniform(0, 6.28)
    p = PALM_GREEN[v % 2]
    for i in range(count):
        a = turn + i * 6.2832 / count + rng.uniform(-0.2, 0.2)
        young = (i % 3 == 0)
        f = nat.frond(name + "_frond%d" % i, top + Vector((0, 0, 0.02)), (math.cos(a), math.sin(a), 0),
                      rng.uniform(0.60, 0.74) * (0.7 if young else 1.0), p[1], width=0.062,
                      lift=0.5 if young else 0.24, droop=0.3 if young else rng.uniform(0.6, 0.85), seed=i)
        nat.leaf_paint(f, p[0], p[1], p[2], seed=i, mottle=0.08, grain=8)
        tree.add(f)
    for i in range([3, 2, 4, 3][v]):
        a = rng.uniform(0, 6.28)
        nut = oa.sphere(name + "_nut%d" % i, top + Vector((math.cos(a) * 0.04, math.sin(a) * 0.04, -0.035)), 0.021, (0.36, 0.26, 0.14), segments=8)
        oa.paint(nut, wear=0.2, dirt=0.3, mottle=0.5, seed=i)
        tree.add(nut)
    tree.hit = (Vector((pts[1].x, pts[1].y, 0)), Vector(top), 0.05)
    return tree.fit()


# ------------------------------------------------------------------ plants

def dot(tree, name, where, radius, colour):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=1, radius=radius)
    o = oa._new_object(name, bm, colour, 80.0)
    o.data.transform(Matrix.Translation(Vector(where)))
    nat.flat_paint(o, colour)
    return tree.add(o)


BUSH = [
    ((0.10, 0.26, 0.13), (0.24, 0.48, 0.19), (0.52, 0.70, 0.27), None),
    ((0.09, 0.24, 0.14), (0.20, 0.44, 0.20), (0.44, 0.64, 0.28), (0.86, 0.16, 0.14)),    # berries
    ((0.12, 0.28, 0.14), (0.28, 0.50, 0.20), (0.58, 0.72, 0.30), (0.99, 0.95, 0.86)),    # in flower
    ((0.26, 0.28, 0.12), (0.46, 0.48, 0.20), (0.70, 0.70, 0.34), (0.98, 0.80, 0.22)),    # dry, yellow flowers
]


def bush(v):
    name = "Bush%d" % v
    rng = random.Random(1100 + v)
    tree = Tree(name)
    p = BUSH[v]
    count = [3, 3, 3, 2][v]
    tops = []
    for k in range(count):
        a = k * 6.2832 / count + rng.uniform(-0.4, 0.4)
        d = 0.0 if k == 0 else rng.uniform(0.42, 0.6)
        r = 0.55 if k == 0 else rng.uniform(0.34, 0.44)
        c = Vector((math.cos(a) * d, math.sin(a) * d, r * 0.62))
        clump(tree, name + "_clump%d" % k, c, (r, r * rng.uniform(0.9, 1.1), r * 0.85), p[:3], 1100 + v * 10 + k, n=3, lump=0.2,
              drift=(0.50, 0.60, 0.18), flat=0.7)
        tops.append((c, r))
    if p[3]:
        for i in range(6):
            c, r = rng.choice(tops)
            a, e = rng.uniform(0, 6.28), rng.uniform(0.2, 1.2)
            d = Vector((math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e)))
            dot(tree, name + "_dot%d" % i, c + Vector((d.x * r, d.y * r, d.z * r * 0.85)) * 0.98, 0.05, p[3])
    return tree.fit()


def cactus(v):
    name = "Cactus%d" % v
    rng = random.Random(1200 + v)
    tree = Tree(name)
    green = (0.30, 0.50, 0.26)
    ribs = lambda a, t: 0.12 * math.cos(5 * a)

    def column(tag, pts, r):
        n = len(pts)
        radii = [r * (0.75 if i == 0 else 1.0) * (1.0 if i < n - 2 else (0.8 if i == n - 2 else 0.38)) for i in range(n)]
        o = nat.tube(name + tag, pts, radii, green, sides=10, flute=ribs, smooth_angle=45)
        nat.leaf_paint(o, (0.14, 0.30, 0.18), (0.30, 0.50, 0.26), (0.58, 0.72, 0.36), seed=v, mottle=0.08)
        tree.add(o)
        dot(tree, name + tag + "_bloom", Vector(pts[-1]) + Vector((0, 0, 0.012)), 0.022, (0.98, 0.45, 0.62) if v % 2 else (0.99, 0.86, 0.3))

    top = [1.0, 0.9, 1.0, 0.8][v]
    column("_stem", [(0, 0, -0.06), (0, 0, 0.2), (0, 0, 0.5), (0, 0, top - 0.08), (0, 0, top - 0.02), (0, 0, top)], 0.085)
    arms = [2, 1, 3, 2][v]
    for k in range(arms):
        a = k * 6.2832 / max(arms, 2) + rng.uniform(-0.5, 0.5) + v
        z = rng.uniform(0.28, 0.5)
        reach = rng.uniform(0.17, 0.22)
        up = rng.uniform(0.22, 0.42)
        ox, oy = math.cos(a), math.sin(a)
        column("_arm%d" % k, [(ox * 0.04, oy * 0.04, z), (ox * reach * 0.8, oy * reach * 0.8, z + 0.01), (ox * reach, oy * reach, z + 0.07),
                               (ox * reach, oy * reach, z + up - 0.05), (ox * reach, oy * reach, z + up - 0.012), (ox * reach, oy * reach, z + up)], 0.058)
    tree.hit = (Vector((0, 0, 0)), Vector((0, 0, top * 0.9)), 0.09)
    return tree.fit()


PETALS = [(0.99, 0.86, 0.22), (0.95, 0.36, 0.42), (0.62, 0.48, 0.94), (0.98, 0.97, 0.92)]
GRASS_BLADE = ((0.16, 0.36, 0.14), (0.36, 0.60, 0.22), (0.70, 0.80, 0.34))


def meadow(v):
    """A tuft of long grass with a few flowers standing out of it."""
    name = "Meadow%d" % v
    rng = random.Random(1300 + v)
    tree = Tree(name)
    for i in range(13):
        a = rng.uniform(0, 6.28)
        d = 0.75 * math.sqrt(rng.random())
        b = nat.blade(name + "_blade%d" % i, (math.cos(a) * d, math.sin(a) * d, -0.05), (math.cos(a + rng.uniform(-1, 1)), math.sin(a + rng.uniform(-1, 1)), 0),
                      rng.uniform(0.5, 0.82), rng.uniform(0.05, 0.075), GRASS_BLADE[1], bend=rng.uniform(0.3, 0.75))
        nat.leaf_paint(b, GRASS_BLADE[0], GRASS_BLADE[1], GRASS_BLADE[2], seed=i, mottle=0.05)
        tree.add(b)
    for i in range([4, 5, 3, 4][v]):
        a = rng.uniform(0, 6.28)
        d = 0.6 * math.sqrt(rng.random())
        h = rng.uniform(0.75, 1.0)
        root = Vector((math.cos(a) * d, math.sin(a) * d, -0.05))
        stem = nat.blade(name + "_stem%d" % i, root, (math.cos(a), math.sin(a), 0), h, 0.02, (0.30, 0.50, 0.20), bend=0.12, segments=2)
        nat.flat_paint(stem, (0.30, 0.50, 0.20))
        tree.add(stem)
        head = root + Vector((math.cos(a) * h * 0.12, math.sin(a) * h * 0.12, h * 0.97))
        colour = PETALS[(v + (i % 2)) % 4]
        dot(tree, name + "_flower%d" % i, head, 0.085, colour)
        dot(tree, name + "_eye%d" % i, head + Vector((0, 0, 0.055)), 0.04, (0.96, 0.62, 0.12))
    return tree.fit()


def reeds(v):
    name = "Reeds%d" % v
    rng = random.Random(1400 + v)
    tree = Tree(name)
    p = ((0.20, 0.34, 0.14), (0.44, 0.58, 0.24), (0.76, 0.80, 0.42))
    for i in range(10):
        a = rng.uniform(0, 6.28)
        d = 0.28 * math.sqrt(rng.random())
        b = nat.blade(name + "_blade%d" % i, (math.cos(a) * d, math.sin(a) * d, -0.05), (math.cos(a), math.sin(a), 0),
                      rng.uniform(0.6, 0.95), rng.uniform(0.018, 0.028), p[1], bend=rng.uniform(0.08, 0.3), segments=3)
        nat.leaf_paint(b, p[0], p[1], p[2], seed=i, mottle=0.05)
        tree.add(b)
    for i in range([3, 2, 4, 3][v]):
        a = rng.uniform(0, 6.28)
        d = 0.2 * math.sqrt(rng.random())
        x, y = math.cos(a) * d, math.sin(a) * d
        h = rng.uniform(0.82, 1.0)
        stalk = nat.tube(name + "_stalk%d" % i, [(x, y, -0.05), (x * 1.1, y * 1.1, h)], [0.008, 0.005], (0.52, 0.56, 0.28), sides=4)
        nat.flat_paint(stalk, (0.52, 0.56, 0.28))
        tree.add(stalk)
        head = nat.tube(name + "_head%d" % i, [(x * 1.08, y * 1.08, h - 0.2), (x * 1.085, y * 1.085, h - 0.17), (x * 1.095, y * 1.095, h - 0.07), (x * 1.1, y * 1.1, h - 0.04)],
                        [0.012, 0.026, 0.026, 0.012], (0.36, 0.22, 0.12), sides=7)
        oa.paint(head, wear=0.3, dirt=0.2, mottle=0.6, seed=i, grain=30)
        tree.add(head)
    return tree.fit()


def fern(v):
    name = "Fern%d" % v
    rng = random.Random(1500 + v)
    tree = Tree(name)
    p = [((0.08, 0.26, 0.14), (0.20, 0.50, 0.22), (0.50, 0.74, 0.32)), ((0.10, 0.28, 0.12), (0.26, 0.52, 0.18), (0.60, 0.76, 0.30))][v % 2]
    count = [8, 9, 7, 8][v]
    for i in range(count):
        a = i * 6.2832 / count + rng.uniform(-0.25, 0.25)
        f = nat.frond(name + "_frond%d" % i, (0, 0, 0.02), (math.cos(a), math.sin(a), 0), rng.uniform(0.95, 1.3), p[1], width=0.2,
                      segments=5, lift=rng.uniform(0.42, 0.6), droop=rng.uniform(0.35, 0.6), seed=i)
        nat.leaf_paint(f, p[0], p[1], p[2], seed=i, mottle=0.08, grain=6)
        tree.add(f)
    return tree.fit()


def log(v):
    """A fallen trunk, mossy on top, with a few toadstools. One unit tall is the thickness of the trunk."""
    name = "Log%d" % v
    rng = random.Random(1600 + v)
    tree = Tree(name)
    length = [4.2, 3.2, 5.0, 3.8][v]
    r = 0.46
    pts = [Vector((-length / 2 + length * i / 5, 0.12 * math.sin(i * 1.3 + v), r * 0.82 + 0.04 * math.sin(i * 2.1))) for i in range(6)]
    radii = [r * (1.0 - 0.05 * i) * (1.08 if i in (0, 5) else 1.0) for i in range(6)]
    wood = BARK if v % 2 == 0 else BARK_DARK
    t = nat.tube(name + "_trunk", pts, radii, wood, sides=14, flute=lambda a, tt: 0.05 * math.cos(6 * a + tt * 9))
    nat.rock_paint(t, tuple(c * 0.75 for c in wood), wood, seed=v, moss=MOSS, grain=2.2)
    tree.add(t)
    for end, sign in ((pts[0], -1), (pts[-1], 1)):
        ring = oa.cylinder(name + "_end", end + Vector((sign * 0.0, 0, 0)), end + Vector((sign * 0.03, 0, 0)), r * 0.86, (0.78, 0.62, 0.40), segments=14)
        oa.paint(ring, wear=0.1, dirt=0.4, mottle=0.8, seed=v, grain=6)
        tree.add(ring)
    a = rng.uniform(0.6, 1.2)
    stub_at = pts[2]
    stub = nat.tube(name + "_stub", [stub_at, stub_at + Vector((0.1, math.cos(a) * 0.5, math.sin(a) * 0.62))], [0.15, 0.07], wood, sides=6)
    nat.bark_paint(stub, wood, seed=v, root_dark=0)
    tree.add(stub)
    for i in range([3, 2, 4, 3][v]):
        x = rng.uniform(-length * 0.4, length * 0.4)
        base = Vector((x, rng.uniform(-0.1, 0.1) - 0.0, r * 1.55))
        stalk = nat.tube(name + "_stalk%d" % i, [base, base + Vector((0, 0, 0.2))], [0.04, 0.03], (0.93, 0.90, 0.82), sides=5)
        nat.flat_paint(stalk, (0.93, 0.90, 0.82))
        tree.add(stalk)
        capc = (0.86, 0.22, 0.16) if v % 2 == 0 else (0.90, 0.62, 0.22)
        cap = nat.lobed(name + "_cap%d" % i, base + Vector((0, 0, 0.2)), [((0, 0, 0), (0.13, 0.13, 0.1))], capc, n=2, lump=0.05, seed=i, flat=0.0)
        nat.leaf_paint(cap, tuple(c * 0.6 for c in capc), capc, tuple(min(1, c * 1.25 + 0.1) for c in capc), seed=i, mottle=0.04)
        tree.add(cap)
    return tree.fit()


# ------------------------------------------------------------------ rock

GRANITE = ((0.34, 0.33, 0.35), (0.62, 0.60, 0.58))
SANDSTONE = ((0.46, 0.36, 0.30), (0.72, 0.60, 0.48))
SLATE = ((0.26, 0.29, 0.34), (0.52, 0.56, 0.60))
LICHEN = (0.74, 0.70, 0.36)


def chisel(rng, lobes, count, low=-0.1, high=0.75, keep=(0.62, 0.9)):
    """Flat faces struck off a rock, so it reads as broken stone and not as a potato."""
    cuts = []
    for _ in range(count):
        az, el = rng.uniform(0, 6.28), rng.uniform(low, high)
        n = Vector((math.cos(az) * math.cos(el), math.sin(az) * math.cos(el), math.sin(el)))
        cuts.append((n, nat._reach(n, lobes) * rng.uniform(*keep)))
    return cuts


def crag(v, snowy=False):
    """A jagged stand of rock: a main tooth with one or two shoulders. Stood on ridges, it makes the skyline."""
    name = ("SnowCrag%d" if snowy else "Crag%d") % v
    seed = 1700 + v * 7 + (30 if snowy else 0)
    rng = random.Random(seed)
    stone = [GRANITE, SLATE, GRANITE, SANDSTONE][v] if not snowy else [SLATE, GRANITE, SLATE, GRANITE][v]
    lean = Vector((rng.uniform(-0.1, 0.1), rng.uniform(-0.1, 0.1), 0))
    lobes = [((0, 0, 0), (0.34, 0.30, 0.30)),
             ((lean.x, lean.y, 0.30), (0.16, 0.14, 0.62))]
    for k in range([2, 1, 3, 2][v]):
        a = rng.uniform(0, 6.28)
        d = rng.uniform(0.17, 0.27)
        h = rng.uniform(0.32, 0.56)
        lobes.append(((math.cos(a) * d, math.sin(a) * d, h * 0.35), (rng.uniform(0.10, 0.15), rng.uniform(0.10, 0.14), h)))
    cuts = chisel(rng, lobes, 9, low=-0.25, high=0.5, keep=(0.72, 0.95))
    tree = Tree(name)
    centre = (0, 0, 0.12)
    o = nat.lobed(name, centre, lobes, stone[0], n=10, lump=0.10, grain=3.2, seed=seed, cuts=cuts, smooth_angle=32, ridged=0.6)
    nat.rock_paint(o, stone[0], stone[1], seed=seed, strata=5.0, snow=SNOW if snowy else None, snow_from=0.42,
                   moss=None if snowy else MOSS, lichen=LICHEN if not snowy else None, grain=4.0)
    tree.add(o)
    tree.hit_objects.append(nat.lobed(name + "_hit", centre, lobes, stone[0], n=3, lump=0.0, seed=seed, cuts=cuts, smooth_angle=30))
    nat.flat_paint(tree.hit_objects[0], stone[0])
    return tree.fit()


def outcrop(v):
    """Ledges of layered rock breaking out of a hillside, mossy on top."""
    name = "Outcrop%d" % v
    seed = 1800 + v * 3
    rng = random.Random(seed)
    stone = [SANDSTONE, GRANITE, SLATE, SANDSTONE][v]
    tree = Tree(name)
    count = [3, 2, 3, 4][v]
    hit_lobes = []
    z = 0.0
    for k in range(count):
        a = rng.uniform(0, 6.28)
        d = rng.uniform(0.0, 0.3) * (1 if k else 0)
        rx, ry, rz = rng.uniform(0.6, 0.95) * (1 - 0.14 * k), rng.uniform(0.45, 0.7) * (1 - 0.14 * k), rng.uniform(0.2, 0.3)
        centre = (math.cos(a) * d, math.sin(a) * d, z + rz * 0.4)
        lobes = [((0, 0, 0), (rx, ry, rz))]
        cuts = chisel(rng, lobes, 6, low=-0.2, high=0.3, keep=(0.7, 0.92)) + [(Vector((0.08 * rng.uniform(-1, 1), 0.08 * rng.uniform(-1, 1), 1)).normalized(), rz * rng.uniform(0.55, 0.8))]
        yaw = Matrix.Rotation(rng.uniform(0, 3.14), 4, 'Z')
        o = nat.lobed(name + "_slab%d" % k, (0, 0, 0), lobes, stone[0], n=(6 if k < 2 else 5) if v != 3 else 5, lump=0.08, grain=3.0, seed=seed + k, cuts=cuts, smooth_angle=32, ridged=0.4)
        o.data.transform(Matrix.Translation(centre) @ yaw)
        nat.rock_paint(o, stone[0], stone[1], seed=seed + k, strata=9.0, moss=MOSS, lichen=LICHEN, grain=3.0)
        tree.add(o)
        h = nat.lobed(name + "_hit%d" % k, (0, 0, 0), lobes, stone[0], n=2, lump=0.0, seed=seed + k, cuts=cuts, smooth_angle=30)
        h.data.transform(Matrix.Translation(centre) @ yaw)
        nat.flat_paint(h, stone[0])
        tree.hit_objects.append(h)
        z += rz * rng.uniform(0.75, 1.0)
    return tree.fit()


TREES = {
    "Conifer": conifer, "Broadleaf": broadleaf, "Palm": palm, "Birch": birch, "Maple": maple, "Blossom": blossom,
    "Pine": pine, "Acacia": acacia, "GiantTree": giant, "Snag": snag, "SnowConifer": lambda v: conifer(v, True),
}
PLANTS = {"Bush": bush, "Cactus": cactus, "Meadow": meadow, "Reeds": reeds, "Fern": fern, "Log": log}
ROCKS = {"Crag": crag, "SnowCrag": lambda v: crag(v, True), "Outcrop": outcrop}

# How tall each kind usually stands, in metres (for the preview pictures only; the game has its own rules).
TYPICAL = {"Conifer": 9, "SnowConifer": 9, "Broadleaf": 8, "Palm": 8, "Birch": 8, "Maple": 7.5, "Blossom": 5.5,
           "Pine": 11, "Acacia": 6.5, "GiantTree": 26, "Snag": 6, "Bush": 1.4, "Cactus": 4, "Meadow": 0.6,
           "Reeds": 1.8, "Fern": 0.9, "Log": 0.9, "Crag": 16, "SnowCrag": 22, "Outcrop": 5}

FAMILIES = [("EarthTrees", TREES, "tree"), ("EarthPlants", PLANTS, "plant"), ("EarthRocks", ROCKS, "rock")]


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    preview = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    wide_only = "--hillside-only" in argv
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else None
    oa.reset_scene()
    built = {}
    over = []
    for file_name, kinds, cap in FAMILIES:
        parts, sockets = [], []
        for kind, make in kinds.items():
            if only and kind not in only:
                continue
            for v in range(4):
                tree = make(v)
                count = nat.triangles(tree.objects)
                print("OA %s: %d triangles" % (tree.name, count))
                if count > CAP[cap]:
                    over.append("%s has %d triangles; the cap for a %s is %d" % (tree.name, count, cap, CAP[cap]))
                parts.append((tree.name, tree.objects))
                if tree.hit:
                    a, b, r = tree.hit
                    sockets += [oa.socket(tree.name + "A", a), oa.socket(tree.name + "B", b), oa.socket(tree.name + "R", (r, 0, 0))]
                if tree.hit_objects:
                    hit = nat.triangles(tree.hit_objects)
                    if hit > CAP["hit"]:
                        over.append("%s's solid shape has %d triangles; the cap is %d" % (tree.name, hit, CAP["hit"]))
                    parts.append((tree.name + "Hit", tree.hit_objects))
                built.setdefault(kind, []).append(tree)
        if parts and not only and not over:
            oa.export(os.path.join(OUT, file_name + ".bytes"), parts, sockets)
    for line in over:
        print("OA OVER THE CAP:", line)
    if preview:
        import preview_scenery
        preview_scenery.render(preview, built, TYPICAL, wide_only)
    if over:
        raise SystemExit("Nothing was written: some pieces are over their triangle caps.")


if __name__ == "__main__":
    main()
