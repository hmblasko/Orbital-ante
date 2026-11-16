"""Builds the bases that stand on the planets: five designs in three sizes, every building in them breakable,
and the living pods, domes, sheds and the command tower hollow, with rooms you can walk into.

    blender -b -P tools/blender/build_bases.py                        (writes Resources/Models/Base*.bytes)
    blender -b -P tools/blender/build_bases.py -- --preview <folder>  (also renders pictures)
    blender -b -P tools/blender/build_bases.py -- --only BaseDomes --preview <folder>   (quick look, writes nothing)
    blender -b -P tools/blender/build_bases.py -- --rebuild BaseDomes,BaseMine         (builds and writes just those)

Base space, as in Scripts/Runtime/Bases/PlanetBases.cs: the origin is the middle of a level floor, the game's
+Y is up and +Z north. Here in Blender that is X east, Y north, Z up. The floor is a round foundation the game
builds itself, of radius 10, 18 or 30 metres (small, medium, large); everything here stands on z = 0 inside
that circle, and the middle is left open because that is where the fight happens.

A base is a list of structures (a living pod, a mast, a run of wall, a heap of crates), numbered from 0.
Pieces in each file (Scripts/Runtime/Bases/BaseKinds.cs reads them by name; i is the structure's number, two
digits):
  Ground, GroundGlow, GroundHit   what cannot be broken: the landing circle, heaps of spoil, cables
  Si                  the structure, whole, drawn in daylight
  SiGlow              whatever on it shines: windows, lamps, the lights inside
  SiHit               what is solid while it stands (closed boxes and drums, never drawn)
  SiIn                the inside of a building you can walk into, its light baked in (drawn without sunlight)
  SiDoor              a sliding door (points SiDoorShut, SiDoorOpen: where its middle is shut and open)
  SiBit0..            the pieces it breaks into: cut here, ahead of time, so the game only lets physics have them
  SiRuin, SiRuinHit   what is left standing once it has fallen: stubs, footings and a heap of rubble
Points: SiMid (the middle of its footprint, on the floor), SiAir (an air station's face, inside),
Spawn0.., Guard0.., Loot0.., Door, Pad as before.

No faction names, flags or markings anywhere: whose base it is belongs to the game, not the model.
"""
import bpy, bmesh, sys, os, math, random, functools
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import oa_blender as oa
import oa_nature as nat
import oa_real as real
import oa_fracture as frac
from mathutils import Vector, Matrix, Euler

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "unity", "Assets", "OrbitalAnte", "Resources", "Models"))

# Triangle caps by size (the rule test BaseModelTests enforces the same numbers): what is drawn from outside,
# what is drawn inside (only near), and the most the broken pieces of one structure may have.
CAPS = {"small": 16000, "medium": 30000, "large": 60000}
INSIDE_CAPS = {"small": 9000, "medium": 25000, "large": 32000}
HIT_CAP = {"small": 900, "medium": 1400, "large": 2400}
ROOM_SPACING = {"dome": 0.62, "shed": 0.6, "keep": 0.52}   # corners for the baked light: big rooms need fewer
EXPOSURE = 0.55                  # how strongly the baked lamp light is taken into a room's colours
RADIUS = {"small": 10.0, "medium": 18.0, "large": 30.0}

# Weathered, greyed colours: the realistic look (Matthew, 14 Nov 2026). Nothing toy-bright; safety colours
# are the faded paint of real kit left out in the weather.
WHITE = (0.80, 0.79, 0.75)
CREAM = (0.70, 0.67, 0.60)
ORANGE = (0.66, 0.38, 0.20)
TEAL = (0.30, 0.42, 0.42)
YELLOW = (0.68, 0.55, 0.24)
RUST = (0.50, 0.28, 0.19)
STEEL = (0.42, 0.44, 0.46)
GALV = (0.56, 0.57, 0.56)
DARK = (0.15, 0.16, 0.17)
SLATE = (0.31, 0.33, 0.36)
OLIVE = (0.38, 0.40, 0.29)
CONCRETE = (0.58, 0.57, 0.54)
SOLAR = (0.07, 0.09, 0.15)
RUBBER = (0.09, 0.09, 0.10)
WARM = (1.0, 0.84, 0.60)
COOL = (0.86, 0.93, 1.0)
CYAN = (0.50, 0.88, 0.95)
RED = (1.0, 0.24, 0.14)
GREEN = (0.45, 1.0, 0.55)
GLASS = (0.48, 0.70, 0.66)
DUST = (0.55, 0.50, 0.44)
RAW = (0.36, 0.35, 0.33)         # the inside of a broken wall: torn insulation, raw metal, concrete

# Inside a room
FLOOR = (0.34, 0.35, 0.36)
WALL_IN = (0.74, 0.73, 0.69)
PANEL_IN = (0.60, 0.61, 0.60)
CLOTH = (0.30, 0.35, 0.42)
BLANKET = (0.46, 0.33, 0.26)
WOOD = (0.48, 0.36, 0.24)
O2 = (0.30, 0.46, 0.34)          # oxygen bottles: the green of real medical/industrial oxygen


class Struct:
    """One thing that stands and can fall: everything a kit function makes between begin and end."""

    def __init__(self, kind, breakable=True, cells=6, ruin=0.6):
        self.kind, self.breakable, self.cells, self.ruin = kind, breakable, cells, ruin
        self.groups = {"Shell": [], "Frame": [], "Detail": [], "WindowGlow": [], "LampGlow": []}
        self.hits = []
        self.inside = []
        self.door = None
        self.points = {}
        self.lights = []          # (position, watts, colour) for baking the inside
        self.ruin_extra = []


class Base:
    def __init__(self, name, size, accent):
        self.name, self.size, self.accent = name, size, accent
        self.structs = []
        self.ground = Struct("ground", breakable=False)
        self.cur = None
        self.sockets = []
        self.counts = {}
        self.rng = random.Random(sum(ord(c) for c in name))

    @property
    def here(self):
        return self.cur if self.cur is not None else self.ground

    @property
    def groups(self):
        """Every drawn piece of every structure, by kind of surface (for pictures)."""
        out = {"Shell": [], "Frame": [], "Detail": [], "WindowGlow": [], "LampGlow": []}
        for s in self.structs + [self.ground]:
            for k, v in s.groups.items():
                out[k].extend(v)
        return out

    @property
    def hits(self):
        return [o for s in self.structs + [self.ground] for o in s.hits]

    # --- what is drawn
    def shell(self, o, seed=0, wear=0.45):
        oa.paint(o, wear=wear, dirt=0.7, mottle=0.8, seed=seed, grain=0.9, gradient=0.06)
        self.here.groups["Shell"].append(o)
        return o

    def frame(self, o, seed=0):
        oa.paint(o, wear=0.55, dirt=0.6, mottle=0.6, seed=seed, grain=2.0, gradient=0.03)
        self.here.groups["Frame"].append(o)
        return o

    def detail(self, o, seed=0):
        oa.paint(o, wear=0.5, dirt=0.6, mottle=0.6, seed=seed, grain=2.4, gradient=0.04)
        self.here.groups["Detail"].append(o)
        return o

    def window(self, o):
        nat.flat_paint(o, o["oa_colour"][:3])
        self.here.groups["WindowGlow"].append(o)
        return o

    def lamp(self, o):
        nat.flat_paint(o, o["oa_colour"][:3])
        self.here.groups["LampGlow"].append(o)
        return o

    def inside(self, o, seed=0, plain=False):
        """A surface of a room: painted like the rest, lit later by the bake. Big faces are cut finer first, so
        the paint's patches are its own and not a few corners' worth stretched into triangles across a floor."""
        if max(o.dimensions) > 1.2:
            oa.densify(o, ROOM_SPACING.get(self.here.kind, 0.5))
        if plain:
            nat.flat_paint(o, o["oa_colour"][:3])
        else:
            oa.paint(o, wear=0.35, dirt=0.45, mottle=0.5, seed=seed, grain=2.2, gradient=0.03)
        self.here.inside.append(o)
        return o

    # --- what is solid
    def hit_box(self, centre, size, yaw=0.0, tilt=None):
        o = oa.box("hit", (0, 0, 0), size, (0.5, 0.5, 0.5), bevel=0)
        m = Matrix.Translation(Vector(centre)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z')
        if tilt is not None:
            m = m @ tilt
        o.data.transform(m)
        nat.flat_paint(o, (0.5, 0.5, 0.5))
        self.here.hits.append(o)
        return o

    def hit_drum(self, base, radius, height, radius_top=None, sides=8):
        o = oa.cylinder("hit", base, (base[0], base[1], base[2] + height), radius, (0.5, 0.5, 0.5), radius_end=radius_top, segments=sides)
        nat.flat_paint(o, (0.5, 0.5, 0.5))
        self.here.hits.append(o)
        return o

    def hit_local(self, m, centre, size, yaw=0.0):
        """A box given in a kit's own space (before it was turned and moved by m)."""
        o = oa.box("hit", (0, 0, 0), size, (0.5, 0.5, 0.5), bevel=0)
        o.data.transform(m @ Matrix.Translation(Vector(centre)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z'))
        nat.flat_paint(o, (0.5, 0.5, 0.5))
        self.here.hits.append(o)
        return o

    def hit_ramp(self, m, low, high, width):
        """A slope from `low` (on the floor) up to `high`, so the steps to a door can be walked up."""
        low, high = Vector(low), Vector(high)
        run = high - low
        length = run.length
        pitch = math.atan2(run.z, math.hypot(run.x, run.y))
        yaw = math.atan2(run.y, run.x)
        o = oa.box("hit", (0, 0, 0), (length + 0.3, width, 0.25), (0.5, 0.5, 0.5), bevel=0)
        mid = (low + high) * 0.5 - Vector((0, 0, 0.12))
        o.data.transform(m @ Matrix.Translation(mid) @ Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Rotation(-pitch, 4, 'Y'))
        nat.flat_paint(o, (0.5, 0.5, 0.5))
        self.here.hits.append(o)
        return o

    def socket(self, kind, position):
        n = self.counts.get(kind, 0)
        self.counts[kind] = n + 1
        name = kind if kind in ("Door", "Pad") else "%s%d" % (kind, n)
        self.sockets.append(oa.socket(name, position))


def structure(kind, cells=6, ruin=0.6, breakable=True):
    """Makes a kit function build one structure of its own (nested calls join the outer one)."""
    def wrap(fn):
        @functools.wraps(fn)
        def run(b, *args, **kw):
            if b.cur is not None:
                return fn(b, *args, **kw)
            b.cur = Struct(kind, breakable, cells, ruin)
            b.structs.append(b.cur)
            try:
                return fn(b, *args, **kw)
            finally:
                here = b.cur
                b.cur = None
                if "Mid" not in here.points:
                    # props that name no middle (crates, barrels, lamp posts): the middle of what was drawn
                    vs = [o.matrix_world @ v.co for g in here.groups.values() for o in g for v in o.data.vertices]
                    if vs:
                        here.points["Mid"] = Vector(((min(v.x for v in vs) + max(v.x for v in vs)) / 2,
                                                     (min(v.y for v in vs) + max(v.y for v in vs)) / 2, 0.0))
        return run
    return wrap


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


def hoop(name, centre, axis, radius, thickness, colour, sides=6, segments=28):
    """A ring of pipe round an axis: a band round a pod that does not fill the room inside it."""
    axis = Vector(axis).normalized()
    u = axis.orthogonal().normalized()
    w = axis.cross(u)
    pts = [Vector(centre) + (u * math.cos(2 * math.pi * k / segments) + w * math.sin(2 * math.pi * k / segments)) * radius for k in range(segments + 1)]
    return nat.tube(name, pts, [thickness] * len(pts), colour, sides=sides, smooth_angle=50)


# ------------------------------------------------------------------ hollow buildings

def _tag(o, index, count=3):
    """Marks every face of a shape with a material slot, so after cutting we know which side a face came from."""
    while len(o.data.materials) < count:
        o.data.materials.append(bpy.data.materials.get("oa_tag%d" % len(o.data.materials)) or bpy.data.materials.new("oa_tag%d" % len(o.data.materials)))
    for p in o.data.polygons:
        p.material_index = index


def _boolean(target, tool, op):
    mod = target.modifiers.new("cut", 'BOOLEAN')
    mod.operation = op
    mod.object = tool
    mod.solver = 'EXACT'
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(tool)


def _split_by_tag(obj, name):
    """Splits a cut shape into its outside (slots 0 and 2) and its inside (slot 1)."""
    out = obj
    inn = obj.copy()
    inn.data = obj.data.copy()
    inn.name = name + "_in"
    bpy.context.scene.collection.objects.link(inn)
    for o, keep in ((out, (0, 2)), (inn, (1,))):
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index not in keep], context='FACES')
        bm.to_mesh(o.data)
        bm.free()
        o.data.materials.clear()
    for k in ("oa_colour", "oa_smooth"):
        inn[k] = obj[k]
    # The two halves of one closed wall: joined again before it is cut into pieces (see break_up).
    out["oa_pair"] = name
    inn["oa_pair"] = name
    return out, inn


def hollow(body, adds, rooms, cuts, name):
    """The walls of a building: `body` joined with `adds`, with `rooms` (the air inside) and `cuts` (doorways)
    taken out. Each must be painted already. Returns (outside, inside): the inside is the room's own skin."""
    _tag(body, 0)
    for a in adds:
        _tag(a, 0)
        _boolean(body, a, 'UNION')
    for r in rooms:
        _tag(r, 1)
        _boolean(body, r, 'DIFFERENCE')
    for c in cuts:
        _tag(c, 2)
        _boolean(body, c, 'DIFFERENCE')
    return _split_by_tag(body, name)


def floor_disc(name, centre, r, colour, thick=0.14):
    """A round floor in rings, so its baked light can change from the lamp's pool to the dark edges (one flat
    face would hold no light but at its rim)."""
    profile = [(0.0, r), (thick, r)] + [(thick, r * k / 5.0) for k in (4, 3, 2, 1)] + [(thick, r * 0.05)]
    return oa.lathe(name, centre, (0, 0, 1), profile, colour, segments=32)


def flip(o):
    """Turns every face of a shape to look inward (the skin of a room seen from inside)."""
    bm = bmesh.new()
    bm.from_mesh(o.data)
    for f in bm.faces:
        f.normal_flip()
    bm.to_mesh(o.data)
    bm.free()
    return o


# ------------------------------------------------------------------ the kit

@structure("lamp", cells=2, ruin=0.35)
def lamp_post(b, at, height=5.5, aim=0.0, colour=WARM):
    x, y = at
    made = [b.frame(oa.cylinder("post", (0, 0, 0), (0, 0, height), 0.08, GALV, radius_end=0.055, segments=8)),
            b.frame(oa.cylinder("post_foot", (0, 0, 0), (0, 0, 0.3), 0.22, CONCRETE, segments=10, bevel=0.04)),
            b.detail(oa.box("lamp_head", (0, 0.32, height), (0.62, 0.4, 0.16), SLATE, bevel=0.05, rot=(-12, 0, 0))),
            b.frame(beam("lamp_arm", (0, 0, height - 0.25), (0, 0.3, height), 0.06, GALV)),
            b.lamp(oa.box("lamp_face", (0, 0.32, height - 0.085), (0.5, 0.3, 0.03), colour, bevel=0.01, rot=(-12, 0, 0)))]
    moved(made, (x, y, 0), aim)
    b.hit_box((x, y, height / 2), (0.3, 0.3, height))


@structure("crates", cells=4, ruin=0.0)
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
        colour = rng.choice((tuple(c * 0.85 for c in b.accent), OLIVE, CREAM, SLATE))
        made = [b.detail(oa.box("crate", (0, 0, 0), (s, s, s), colour, bevel=0.03, segments=1), seed=i),
                b.detail(oa.box("crate_band", (0, 0, 0), (s * 1.02, s * 0.12, s * 1.02), DARK, bevel=0.01, segments=1), seed=i),
                b.detail(oa.box("crate_lid", (0, 0, s * 0.47), (s * 1.03, s * 1.03, s * 0.08), tuple(c * 0.8 for c in colour), bevel=0.02, segments=1), seed=i)]
        moved(made, (px, py, pz), yaw)
        b.hit_box((px, py, pz), (s, s, s), yaw)
        if pz < s:
            spots.append((px, py, s))
    if loot:
        b.socket("Loot", (x, y + spread + 0.9, 0.0))


@structure("barrels", cells=4, ruin=0.0)
def barrels(b, at, count=3):
    rng = b.rng
    x, y = at
    for i in range(count):
        a = i * 2.2 + rng.uniform(0, 1)
        px, py = x + math.cos(a) * 0.62 * (i > 0), y + math.sin(a) * 0.62 * (i > 0)
        colour = rng.choice((tuple(c * 0.85 for c in b.accent), TEAL, RUST, SLATE))
        b.detail(oa.cylinder("barrel", (px, py, 0), (px, py, 0.9), 0.3, colour, segments=14, bevel=0.03), seed=i)
        for z in (0.28, 0.62):
            b.detail(oa.cylinder("barrel_band", (px, py, z), (px, py, z + 0.04), 0.312, tuple(c * 0.7 for c in colour), segments=14), seed=i)
        b.hit_drum((px, py, 0), 0.31, 0.9)


def steps(b, m, made, front_y, floor_z, width, tag):
    """Steel steps from the floor up to a door sill at floor_z, with a slope to walk up in place of the treads."""
    n = max(2, int(math.ceil(floor_z / 0.2)))
    rise = floor_z / n
    for i in range(n):
        top = floor_z - i * rise
        made.append(b.frame(oa.box(tag + "_step", (0, front_y + 0.16 + i * 0.3, top - 0.04), (width, 0.32, 0.08), GALV, bevel=0.01, segments=1)))
    for s in (-1, 1):
        made.append(b.frame(beam(tag + "_stringer", (s * width / 2, front_y, floor_z), (s * width / 2, front_y + n * 0.3 + 0.1, 0.0), 0.06, STEEL)))
        made.append(b.frame(beam(tag + "_rail", (s * width / 2, front_y, floor_z + 0.95), (s * width / 2, front_y + n * 0.3 + 0.1, 0.95), 0.04, GALV)))
        made.append(b.frame(beam(tag + "_rail_post", (s * width / 2, front_y + n * 0.3 + 0.1, 0.0), (s * width / 2, front_y + n * 0.3 + 0.1, 0.95), 0.04, GALV)))
    return (0, front_y + n * 0.3 + 0.35, 0.0), (0, front_y - 0.1, floor_z)


def sliding_door(b, m, x, face_y, floor_z, width=1.05, height=2.05, slide=1.1, tag="door"):
    """A sliding door just outside a wall at y = face_y, covering a doorway centred on x. The game slides it
    open as someone comes near. Returns the door object (already in place) and the open and shut points."""
    door = oa.box(tag, (x, face_y + 0.05, floor_z + height / 2), (width + 0.1, 0.06, height + 0.06), SLATE, bevel=0.015, segments=1)
    oa.paint(door, wear=0.5, dirt=0.6, mottle=0.5, seed=7, grain=3.0)
    glass = oa.box(tag + "_pane", (x, face_y + 0.083, floor_z + height * 0.72), (width * 0.42, 0.012, 0.38), (0.12, 0.14, 0.16), bevel=0.004, segments=1)
    nat.flat_paint(glass, (0.12, 0.14, 0.16))
    handle = oa.box(tag + "_handle", (x - width * 0.38, face_y + 0.095, floor_z + 1.05), (0.04, 0.03, 0.34), GALV, bevel=0.005, segments=1)
    oa.paint(handle, wear=0.8, dirt=0.2, seed=2)
    # Door, pane and handle are one moving piece.
    for o in (glass, handle):
        door.data.materials.clear()
    joined = _join([door, glass, handle], tag)
    joined.data.transform(m)
    b.here.door = joined
    shut = m @ Vector((x, face_y + 0.05, floor_z + height / 2))
    opened = m @ Vector((x - slide, face_y + 0.05, floor_z + height / 2))
    b.here.points["DoorShut"] = shut
    b.here.points["DoorOpen"] = opened
    return joined


def turn_lights(b, m):
    """Moves the lights a kit added in its own space to where the kit now stands."""
    for L in b.here.lights:
        if len(L) < 5:
            L[0] = m @ L[0]
            if L[3] is not None:
                L[3] = m.to_3x3() @ L[3]
            L.append("placed")


def front_walls(b, m, door_x, half_gap, width, y, thick, floor_z, height):
    """The solid either side of a doorway in a flat front wall, and over it."""
    lo, hi = door_x - half_gap, door_x + half_gap
    left, right = -width / 2, width / 2
    b.hit_local(m, ((left + lo) / 2, y, floor_z + height / 2), (lo - left, thick, height))
    b.hit_local(m, ((hi + right) / 2, y, floor_z + height / 2), (right - hi, thick, height))
    b.hit_local(m, (door_x, y, floor_z + (2.15 + height) / 2), (hi - lo, thick, height - 2.15))


def _join(objs, name):
    bm = bmesh.new()
    for o in objs:
        bm.from_mesh(o.data)
    for o in objs[1:]:
        bpy.data.objects.remove(o)
    first = objs[0]
    bm.to_mesh(first.data)
    bm.free()
    first.name = name
    return first


def air_station(b, made, x, y, z, facing_y=1, tag="air"):
    """An air station on a wall: two oxygen bottles in a cage, a regulator, a hose to a mask on a hook, and a lit
    gauge. Built against a wall at y, facing +Y (facing_y=1) or -Y. Its face is the point the game looks for."""
    s = facing_y
    parts = []
    parts.append(b.inside(oa.box(tag + "_back", (x, y + s * 0.03, z + 1.0), (1.0, 0.06, 1.7), PANEL_IN, bevel=0.02, segments=1), seed=3))
    for k, dx in enumerate((-0.22, 0.22)):
        parts.append(b.inside(oa.cylinder(tag + "_bottle", (x + dx, y + s * 0.2, z + 0.2), (x + dx, y + s * 0.2, z + 1.35), 0.14, O2, segments=14, bevel=0.06), seed=k))
        parts.append(b.inside(oa.cylinder(tag + "_valve", (x + dx, y + s * 0.2, z + 1.35), (x + dx, y + s * 0.2, z + 1.48), 0.045, GALV, segments=8), seed=k))
        parts.append(b.inside(oa.cylinder(tag + "_collar", (x + dx, y + s * 0.2, z + 1.1), (x + dx, y + s * 0.2, z + 1.16), 0.15, (0.8, 0.8, 0.78), segments=14), seed=k))
    for zz in (0.45, 1.05):
        parts.append(b.inside(beam(tag + "_strap", (x - 0.42, y + s * 0.36, z + zz), (x + 0.42, y + s * 0.36, z + zz), 0.035, DARK)))
    parts.append(b.inside(oa.box(tag + "_reg", (x, y + s * 0.14, z + 1.62), (0.6, 0.18, 0.2), SLATE, bevel=0.03), seed=5))
    parts.append(b.inside(pipe(tag + "_hose", [(x + 0.28, y + s * 0.2, z + 1.6), (x + 0.42, y + s * 0.3, z + 1.25), (x + 0.4, y + s * 0.32, z + 0.95), (x + 0.36, y + s * 0.3, z + 1.1)], 0.022, RUBBER, sides=6)))
    parts.append(b.inside(oa.sphere(tag + "_mask", (x + 0.36, y + s * 0.3, z + 1.08), 0.08, (0.2, 0.22, 0.24), scale=(1.0, 0.7, 1.1), segments=10)))
    parts.append(b.inside(oa.cylinder(tag + "_gauge_rim", (x, y + s * 0.22, z + 1.62), (x, y + s * 0.25, z + 1.62), 0.085, GALV, segments=14)))
    gauge = b.lamp(oa.cylinder(tag + "_gauge", (x, y + s * 0.245, z + 1.62), (x, y + s * 0.255, z + 1.62), 0.07, GREEN, segments=14))
    parts.append(gauge)
    parts.append(b.lamp(oa.box(tag + "_button", (x - 0.2, y + s * 0.235, z + 1.62), (0.07, 0.03, 0.07), (0.45, 0.9, 1.0), bevel=0.01, segments=1)))
    made.extend(parts)
    return Vector((x, y + s * 0.7, z + 1.2))


def ceiling_strip(b, made, a, c, tag="strip"):
    """A strip light under a ceiling, from a to c: a housing and its diffuser."""
    a, c = Vector(a), Vector(c)
    mid = (a + c) * 0.5
    length = (c - a).length
    yaw = math.degrees(math.atan2((c - a).y, (c - a).x))
    house = oa.box(tag, (0, 0, 0), (length, 0.2, 0.08), PANEL_IN, bevel=0.01, segments=1)
    face = oa.box(tag + "_face", (0, 0, -0.045), (length * 0.96, 0.15, 0.015), COOL, bevel=0.004, segments=1)
    for o in (house, face):
        o.data.transform(Matrix.Translation(mid) @ Matrix.Rotation(math.radians(yaw), 4, 'Z'))
    made.append(b.inside(house, seed=4))
    made.append(b.lamp(face))
    b.here.lights.append([mid - Vector((0, 0, 0.12)), 22.0 * length, (1.0, 0.95, 0.88), (c - a)])


def bunk(b, made, x, y, z, yaw, tag="bunk"):
    """A two-high steel bunk, made beds."""
    parts = []
    L, W = 1.95, 0.85
    for lvl in (0.38, 1.28):
        parts.append(b.inside(oa.box(tag + "_deck", (0, 0, lvl), (L, W, 0.08), STEEL, bevel=0.015, segments=1), seed=1))
        parts.append(b.inside(oa.box(tag + "_mattress", (0, 0, lvl + 0.1), (L - 0.06, W - 0.06, 0.13), CLOTH, bevel=0.05), seed=2))
        parts.append(b.inside(oa.box(tag + "_blanket", (0.25, 0, lvl + 0.17), (L * 0.68, W - 0.02, 0.05), BLANKET, bevel=0.025), seed=3))
        parts.append(b.inside(oa.box(tag + "_pillow", (-L * 0.38, 0, lvl + 0.21), (0.32, W * 0.7, 0.1), (0.72, 0.72, 0.68), bevel=0.04), seed=4))
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(b.inside(oa.box(tag + "_post", (sx * (L / 2 - 0.03), sy * (W / 2 - 0.03), 0.8), (0.05, 0.05, 1.6), STEEL, bevel=0.01, segments=1)))
    for o in parts:
        o.data.transform(Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z'))
    made.extend(parts)


def table(b, made, x, y, z, w=1.2, d=0.7, tag="table", stools=2):
    parts = [b.inside(oa.box(tag + "_top", (x, y, z + 0.74), (w, d, 0.04), PANEL_IN, bevel=0.01, segments=1), seed=2)]
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(b.inside(oa.cylinder(tag + "_leg", (x + sx * (w / 2 - 0.06), y + sy * (d / 2 - 0.06), z), (x + sx * (w / 2 - 0.06), y + sy * (d / 2 - 0.06), z + 0.72), 0.022, STEEL, segments=6)))
    for k in range(stools):
        sx = -1 if k == 0 else 1
        cx, cy = x + sx * (w * 0.2), y + (d / 2 + 0.3) * (1 if k % 2 == 0 else -1)
        parts.append(b.inside(oa.cylinder(tag + "_stool", (cx, cy, z + 0.44), (cx, cy, z + 0.48), 0.18, CLOTH, segments=12, bevel=0.01)))
        parts.append(b.inside(oa.cylinder(tag + "_stool_leg", (cx, cy, z), (cx, cy, z + 0.44), 0.025, STEEL, segments=6)))
    # what is left on it: a mug, a slate, a box
    parts.append(b.inside(oa.cylinder(tag + "_mug", (x + 0.3, y + 0.1, z + 0.76), (x + 0.3, y + 0.1, z + 0.86), 0.04, (0.62, 0.3, 0.22), segments=8)))
    parts.append(b.inside(oa.box(tag + "_slate", (x - 0.2, y - 0.05, z + 0.765), (0.26, 0.18, 0.012), DARK, bevel=0.004, segments=1)))
    made.extend(parts)


def lockers(b, made, x, y, z, yaw, count=3, tag="locker"):
    parts = []
    for i in range(count):
        cx = (i - (count - 1) / 2) * 0.5
        parts.append(b.inside(oa.box(tag, (cx, 0, 0.95), (0.48, 0.5, 1.9), (0.42, 0.47, 0.50), bevel=0.012, segments=1), seed=i))
        for k in range(3):
            parts.append(b.inside(oa.box(tag + "_vent", (cx, 0.252, 1.62 + k * 0.06), (0.3, 0.01, 0.02), DARK, bevel=0, segments=1)))
        parts.append(b.inside(oa.box(tag + "_latch", (cx + 0.17, 0.255, 1.0), (0.03, 0.02, 0.14), GALV, bevel=0, segments=1)))
    for o in parts:
        o.data.transform(Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z'))
    made.extend(parts)


def shelves(b, made, x, y, z, yaw, w=1.6, h=1.9, tag="shelf"):
    rng = b.rng
    parts = []
    for sx in (-1, 1):
        parts.append(b.inside(oa.box(tag + "_side", (sx * w / 2, 0, h / 2), (0.04, 0.45, h), GALV, bevel=0, segments=1)))
    for k in range(4):
        zz = 0.15 + k * (h - 0.2) / 3
        parts.append(b.inside(oa.box(tag + "_board", (0, 0, zz), (w, 0.45, 0.03), GALV, bevel=0, segments=1), seed=k))
        if k < 3:
            px = -w / 2 + 0.15
            while px < w / 2 - 0.25:
                bw = rng.uniform(0.18, 0.4)
                bh = rng.uniform(0.15, 0.36)
                colour = rng.choice((CREAM, OLIVE, (0.55, 0.45, 0.32), SLATE, (0.62, 0.6, 0.55)))
                parts.append(b.inside(oa.box(tag + "_box", (px + bw / 2, rng.uniform(-0.04, 0.04), zz + 0.015 + bh / 2), (bw, rng.uniform(0.25, 0.4), bh), colour, bevel=0.01, segments=1), seed=k))
                px += bw + rng.uniform(0.04, 0.12)
    for o in parts:
        o.data.transform(Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z'))
    made.extend(parts)


def workbench(b, made, x, y, z, yaw, w=2.0, tag="bench"):
    parts = [b.inside(oa.box(tag + "_top", (0, 0, 0.9), (w, 0.75, 0.06), WOOD, bevel=0.01, segments=1), seed=2),
             b.inside(oa.box(tag + "_shelf", (0, 0, 0.25), (w - 0.1, 0.7, 0.03), STEEL, bevel=0, segments=1), seed=3),
             b.inside(oa.box(tag + "_board", (0, -0.36, 1.45), (w, 0.03, 1.0), (0.44, 0.42, 0.38), bevel=0.005, segments=1), seed=4),
             b.inside(oa.box(tag + "_vice", (w / 2 - 0.25, 0.1, 0.99), (0.16, 0.22, 0.12), (0.30, 0.38, 0.45), bevel=0.01), seed=5),
             b.inside(oa.box(tag + "_case", (-w / 2 + 0.35, 0.05, 1.0), (0.45, 0.3, 0.14), (0.55, 0.20, 0.16), bevel=0.02), seed=6)]
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(b.inside(oa.box(tag + "_leg", (sx * (w / 2 - 0.05), sy * 0.32, 0.45), (0.05, 0.05, 0.9), STEEL, bevel=0, segments=1)))
    for k in range(6):
        parts.append(b.inside(oa.box(tag + "_tool", (-w / 2 + 0.3 + k * 0.28, -0.34, 1.35 + (k % 2) * 0.2), (0.04, 0.03, 0.26 + (k % 3) * 0.06), GALV if k % 2 else DARK, bevel=0, segments=1)))
    for o in parts:
        o.data.transform(Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z'))
    made.extend(parts)


def console_desk(b, made, x, y, z, yaw, w=1.6, tag="desk"):
    """A desk of screens: the kind of working station every outpost has. The screens glow."""
    parts = [b.inside(oa.box(tag, (0, 0, 0.4), (w, 0.7, 0.8), SLATE, bevel=0.02, segments=1), seed=1),
             b.inside(oa.box(tag + "_top", (0, 0.04, 0.82), (w + 0.06, 0.78, 0.04), PANEL_IN, bevel=0.01, segments=1), seed=2)]
    for k in range(2):
        cx = (k - 0.5) * w * 0.5
        parts.append(b.inside(oa.box(tag + "_screen", (cx, -0.2, 1.15), (w * 0.44, 0.06, 0.5), DARK, bevel=0.01, rot=(-8, 0, 0)), seed=k))
        parts.append(b.lamp(oa.box(tag + "_glass", (cx, -0.165, 1.15), (w * 0.4, 0.01, 0.42), (0.35, 0.62, 0.70), bevel=0.003, segments=1, rot=(-8, 0, 0))))
    parts.append(b.inside(oa.box(tag + "_keys", (0, 0.18, 0.85), (0.5, 0.18, 0.02), DARK, bevel=0.003, segments=1)))
    parts.append(b.inside(oa.cylinder(tag + "_chair", (0, 0.75, 0.45), (0, 0.75, 0.52), 0.24, CLOTH, segments=12, bevel=0.02)))
    parts.append(b.inside(oa.box(tag + "_chair_back", (0, 0.98, 0.85), (0.42, 0.06, 0.55), CLOTH, bevel=0.03)))
    parts.append(b.inside(oa.cylinder(tag + "_chair_post", (0, 0.75, 0.05), (0, 0.75, 0.45), 0.03, STEEL, segments=6)))
    for o in parts:
        o.data.transform(Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z'))
    made.extend(parts)


@structure("pod", cells=8, ruin=0.75)
def hab(b, at, yaw, length=7.0, r=1.8, door=True, tag="hab", fit="quarters"):
    """A living pod: a fat drum lying low on cradles, entered through a porch with a sliding door at the top of a
    short flight of steps. Inside: a deck floor, strip lights, the furniture for what it is used for, and an air
    station. The door faces +Y."""
    lift = 0.25
    cz = r + lift
    wall = 0.14
    ri = r - wall
    floor_z = cz - 1.25
    half = length / 2
    made = []
    # --- the walls, cut hollow
    body = oa.cylinder(tag, (-half, 0, cz), (half, 0, cz), r, WHITE, segments=32, bevel=0.42)
    oa.densify(body, 0.9)
    oa.paint(body, wear=0.45, dirt=0.7, mottle=0.8, seed=int(at[0] * 7), grain=0.9, gradient=0.06)
    pw, pd = 3.2, 1.4
    dx = 0.5                      # the doorway sits off the middle, so the door has wall to slide along
    py0 = math.sqrt(max(0.0, r * r - 1.4 ** 2)) * 0.8
    porch = oa.box(tag + "_porch", (0, py0 + pd / 2, (floor_z + 2.55) / 2), (pw, pd, floor_z + 2.55), CREAM, bevel=0.1)
    oa.densify(porch, 0.8)
    oa.paint(porch, wear=0.45, dirt=0.7, mottle=0.7, seed=5, grain=1.2)
    room = oa.cylinder(tag + "_room", (-half + wall * 1.4, 0, cz), (half - wall * 1.4, 0, cz), ri, WALL_IN, segments=32)
    oa.densify(room, 0.5)
    oa.paint(room, wear=0.2, dirt=0.4, mottle=0.4, seed=3, grain=2.0)
    vest = oa.box(tag + "_vest", (0, (py0 + pd - wall - 0.2) / 2, floor_z + 1.15), (pw - 2 * wall, py0 + pd - wall + 0.2, 2.3), WALL_IN, bevel=0)
    oa.densify(vest, 0.5)
    oa.paint(vest, wear=0.2, dirt=0.4, mottle=0.4, seed=4, grain=2.0)
    cut = oa.box(tag + "_doorway", (dx, py0 + pd, floor_z + 1.05), (1.1, wall * 4, 2.1), DARK, bevel=0)
    nat.flat_paint(cut, (0.38, 0.38, 0.37))
    outside, inner = hollow(body, [porch], [room, vest], [cut] if door else [], tag)
    made.append(outside)
    b.here.groups["Shell"].append(outside)
    made.append(inner)
    b.here.inside.append(inner)
    # bands round the drum (rings, so they do not fill the room), cradles, windows, vent, aerial
    for i, x in enumerate((-half * 0.66, half * 0.66)):
        made.append(b.shell(hoop(tag + "_rib", (x, 0, cz), (1, 0, 0), r + 0.02, 0.07, b.accent), seed=i))
        made.append(b.frame(oa.box(tag + "_saddle", (x, 0, lift * 0.75), (0.45, r * 1.5, lift * 1.5), STEEL, bevel=0.05)))
    for x in (-half * 0.36, half * 0.36):
        for s in (1, -1):
            yy = s * r * 0.94
            made.append(b.frame(oa.cylinder(tag + "_port", (x, s * r * 0.95, cz + 0.37), (x, s * r * 1.0, cz + 0.4), 0.3, STEEL, segments=16, bevel=0.02)))
            made.append(b.window(oa.cylinder(tag + "_glass", (x, s * r * 0.99, cz + 0.395), (x, s * r * 1.012, cz + 0.405), 0.22, WARM, segments=16)))
            # the same window seen from inside: a frame on the wall and daylight through the glass
            made.append(b.inside(hoop(tag + "_port_in", (x, s * (ri - 0.06), cz + 0.37), (0, 1, 0), 0.25, 0.04, PANEL_IN, sides=6, segments=20), seed=6))
            made.append(b.window(oa.cylinder(tag + "_glass_in", (x, s * (ri - 0.05), cz + 0.37), (x, s * (ri - 0.065), cz + 0.37), 0.22, (0.62, 0.72, 0.82), segments=16)))
            del yy
    made.append(b.detail(oa.box(tag + "_vent", (-half * 0.3, 0, 2 * r + lift - 0.05), (0.9, 0.7, 0.24), SLATE, bevel=0.06)))
    made.append(b.frame(oa.cylinder(tag + "_aerial", (half * 0.5, 0, 2 * r + lift - 0.1), (half * 0.5, 0.2, 2 * r + lift + 2.2), 0.02, DARK, segments=5)))
    made.append(b.lamp(oa.box(tag + "_door_lamp", (dx, py0 + pd + 0.03, floor_z + 2.33), (0.6, 0.08, 0.08), WARM, bevel=0.02, segments=1)))
    # --- inside: deck floor over the curve of the drum, lights, furniture
    chord = math.sqrt(ri * ri - (cz - floor_z) ** 2)
    made.append(b.inside(oa.box(tag + "_deck", (0, 0, floor_z - 0.08), (length - wall * 2.6, chord * 2 + 0.1, 0.16), FLOOR, bevel=0.01), seed=1))
    made.append(b.inside(oa.box(tag + "_deck_porch", (0, (py0 + pd - wall) / 2, floor_z - 0.08), (pw - 2 * wall + 0.05, py0 + pd - wall, 0.16), FLOOR, bevel=0.01), seed=2))
    top = cz + ri - 0.18
    ceiling_strip(b, made, (-half + 0.6, -0.35, top), (half - 0.6, -0.35, top), tag + "_strip0")
    ceiling_strip(b, made, (-half + 0.6, 0.35, top), (half - 0.6, 0.35, top), tag + "_strip1")
    wall_y = chord - 0.02
    if fit == "quarters":
        bunk(b, made, -half + 1.25, -wall_y + 0.47, floor_z, 0, tag + "_bunk0")
        lockers(b, made, half - 1.1, -wall_y + 0.27, floor_z, 0, 2, tag + "_lock")
        table(b, made, half - 1.15, 0.45, floor_z, 1.0, 0.6, tag + "_table", stools=1)
    else:   # a radio room: desk of screens and shelves
        console_desk(b, made, -half + 1.4, -wall_y + 0.38, floor_z, 0, 1.6, tag + "_desk")
        shelves(b, made, half - 1.1, -wall_y + 0.25, floor_z, 0, 1.4, 1.9, tag + "_shelf")
    air = air_station(b, made, 0.0, -wall_y, floor_z, facing_y=1, tag=tag + "_air")
    m = moved(made, (at[0], at[1], 0), yaw)
    turn_lights(b, m)
    if door:
        b.here.points["Air"] = m @ air
        steps_made = []
        foot, sill = steps(b, m, steps_made, py0 + pd, floor_z, 1.2, tag)
        steps_made_m = Matrix.Translation((dx, 0, 0))
        for o in steps_made:
            o.data.transform(steps_made_m)
        moved(steps_made, (at[0], at[1], 0), yaw)
        made.extend(steps_made)
        b.hit_ramp(m, Vector(foot) + Vector((dx, 0, 0)), Vector(sill) + Vector((dx, 0, 0)), 1.25)
        sliding_door(b, m, dx, py0 + pd, floor_z, 1.05, 2.05, 1.15, tag + "_door")
        b.here.lights.append([m @ Vector((dx, py0 + pd + 0.4, floor_z + 2.2)), 30.0, (1.0, 0.85, 0.6), None])
    # --- solid: floor under the deck, walls with a gap at the porch, end caps, roof
    hl = lambda c, s: b.hit_local(m, c, s)
    hl((0, 0, floor_z / 2), (length, chord * 2 + 0.3, floor_z))
    hl((0, -(chord + 0.35), cz), (length, 0.7, 2 * r))
    gap = (pw - 2 * wall) / 2
    for s in (-1, 1):
        cx = s * (gap + (half - gap) / 2)
        hl((cx, chord + 0.35, cz), (half - gap, 0.7, 2 * r))
        hl((s * (half - 0.15), 0, cz), (0.3, 2 * r, 2 * r))
    hl((0, chord + 0.35, floor_z + 2.55 + 0.5), (2 * gap, 0.7, 1.0))
    hl((0, 0, floor_z + 2.55 + 0.5), (length, 2 * r, 1.0))
    # porch: floor, sides, roof, front with the doorway
    hl((0, py0 + pd / 2, floor_z / 2), (pw, pd, floor_z))
    for s in (-1, 1):
        hl((s * (pw / 2 - wall / 2), py0 + pd / 2, floor_z + 1.3), (wall, pd, 2.6))
    front_walls(b, m, dx, 0.55, pw, py0 + pd - wall / 2, wall, floor_z, 2.6)
    hl((0, py0 + pd / 2, floor_z + 2.45), (pw, pd, 0.3))
    # furniture worth bumping into
    hl((-half + 1.25 if fit == "quarters" else -half + 1.4, -wall_y + 0.45, floor_z + 0.8), (2.0, 0.85, 1.6))
    b.here.points["Mid"] = m @ Vector((0, 0, 0))
    return m


@structure("dome", cells=10, ruin=1.0)
def dome(b, at, r, colour=WHITE, glass=False, porch_yaw=None, tag="dome", fit="mess"):
    """A pressure dome on a ring wall, with a porch and a sliding door. glass: a lit greenhouse (not entered).
    Inside: a round room under the dome, a lamp hung from the crown, furniture and an air station."""
    x, y = at
    wall = 0.9
    crown = wall + r * 0.82
    profile = [(0.0, r + 0.22), (wall, r + 0.22), (wall, r)]
    for i in range(1, 10):
        a = math.radians(90 * i / 10)
        profile.append((wall + r * 0.82 * math.sin(a), max(0.0, r * math.cos(a))))
    profile[-1] = (crown, 0.0)
    made = []
    if glass:
        skin = oa.lathe(tag, (0, 0, 0), (0, 0, 1), profile[2:-1] + [(crown - 0.02, 0.3)], GLASS, segments=32)
        made.append(b.window(skin))
    else:
        skin = oa.lathe(tag, (0, 0, 0), (0, 0, 1), profile[:-1] + [(crown - 0.02, 0.3)], colour, segments=32)
        oa.densify(skin, 1.0)
        oa.paint(skin, wear=0.45, dirt=0.7, mottle=0.8, seed=int(x), grain=0.9, gradient=0.06)
    made.append(b.shell(oa.lathe(tag + "_ring", (0, 0, 0), (0, 0, 1), [(0.0, r + 0.32), (wall * 0.75, r + 0.32), (wall, r + 0.1), (wall, r - 0.1), (0.0, r - 0.1)], CONCRETE if not glass else STEEL, segments=32), seed=2))
    ribs = 12 if r > 4.5 else 10
    porch_gap = math.asin(min(1.0, 1.75 / r)) if (porch_yaw is not None and not glass) else -1.0
    for k in range(ribs):
        a = 2 * math.pi * k / ribs
        if abs(math.atan2(math.cos(a), math.sin(a))) < porch_gap:
            continue     # no rib down through the porch
        pts = []
        for i in range(0, 9):
            e = math.radians(88 * i / 8)
            rr = (r + 0.04) * math.cos(e)
            pts.append((math.cos(a) * rr, math.sin(a) * rr, wall + (r * 0.82 + 0.04) * math.sin(e)))
        made.append(b.frame(pipe(tag + "_rib%d" % k, pts, 0.06 if not glass else 0.05, GALV if not glass else STEEL, sides=5), seed=k))
    made.append(b.shell(oa.lathe(tag + "_cap", (0, 0, crown - 0.12), (0, 0, 1), [(0, r * 0.18), (0.22, r * 0.18), (0.4, r * 0.08)], SLATE, segments=16), seed=4))
    made.append(b.lamp(oa.cylinder(tag + "_beacon", (0, 0, crown + 0.28), (0, 0, crown + 0.46), 0.1, RED if not glass else CYAN, segments=8)))
    if glass or porch_yaw is None:
        if not glass:
            made.append(b.shell(skin, seed=int(x)))
        moved(made, (x, y, 0), 0)
        b.hit_drum((x, y, 0), r + 0.3, wall + r * 0.42, sides=12)
        b.hit_drum((x, y, wall + r * 0.42), r * 0.86, r * 0.42, radius_top=r * 0.3, sides=12)
        b.here.points["Mid"] = Vector((x, y, 0))
        return None
    # --- hollow: porch, room, doorway. The porch faces +Y before the turn.
    pw, pd, ph = 3.2, 2.2, 2.75
    dx = 0.5
    front = r + pd - 0.2
    porch = oa.box(tag + "_porch", (0, (r - 0.6 + front) / 2, ph / 2), (pw, front - (r - 0.6), ph), CREAM, bevel=0.12)
    oa.densify(porch, 0.8)
    oa.paint(porch, wear=0.45, dirt=0.7, mottle=0.7, seed=6, grain=1.2)
    inner_profile = [(0.1, r - 0.18)]
    for i in range(1, 10):
        a = math.radians(90 * i / 10)
        inner_profile.append((wall + (r * 0.82 - 0.18) * math.sin(a), max(0.0, (r - 0.18) * math.cos(a))))
    inner_profile[-1] = (crown - 0.2, 0.25)
    inner_profile.insert(1, (wall, r - 0.18))
    room = oa.lathe(tag + "_room", (0, 0, 0), (0, 0, 1), inner_profile, WALL_IN, segments=32)
    oa.densify(room, 0.6)
    oa.paint(room, wear=0.2, dirt=0.4, mottle=0.4, seed=8, grain=2.0)
    vest_back, vest_front = r - 1.0, front - 0.14
    vest = oa.box(tag + "_vest", (0, (vest_back + vest_front) / 2, 1.25), (pw - 0.28, vest_front - vest_back, 2.3), WALL_IN, bevel=0)
    oa.densify(vest, 0.5)
    oa.paint(vest, wear=0.2, dirt=0.4, mottle=0.4, seed=9, grain=2.0)
    cut = oa.box(tag + "_doorway", (dx, front, 1.12), (1.2, 0.6, 2.2), DARK, bevel=0)
    nat.flat_paint(cut, (0.38, 0.38, 0.37))
    outside, inner = hollow(skin, [porch], [room, vest], [cut], tag)
    made.append(outside)
    b.here.groups["Shell"].append(outside)
    made.append(inner)
    b.here.inside.append(inner)
    made.append(b.lamp(oa.box(tag + "_door_lamp", (dx, front + 0.03, 2.45), (0.7, 0.08, 0.08), WARM, bevel=0.02, segments=1)))
    # --- inside
    made.append(b.inside(floor_disc(tag + "_floor", (0, 0, 0.0), r - 0.1, FLOOR), seed=3))
    made.append(b.inside(oa.box(tag + "_floor_porch", (0, (vest_back + vest_front) / 2, 0.06), (pw - 0.25, vest_front - vest_back + 0.04, 0.12), FLOOR, bevel=0.01), seed=4))
    made.append(b.inside(oa.cylinder(tag + "_lamp_rod", (0, 0, crown - 0.3), (0, 0, crown - 1.3), 0.015, DARK, segments=5)))
    made.append(b.inside(oa.lathe(tag + "_lamp_shade", (0, 0, crown - 1.6), (0, 0, 1), [(0.0, 0.5), (0.3, 0.45), (0.32, 0.12), (0.34, 0.0)], PANEL_IN, segments=20), seed=5))
    made.append(b.lamp(oa.cylinder(tag + "_lamp_glow", (0, 0, crown - 1.62), (0, 0, crown - 1.58), 0.42, WARM, segments=20)))
    b.here.lights.append([Vector((0, 0, crown - 1.75)), 260.0, (1.0, 0.88, 0.7), None])
    b.here.lights.append([Vector((0, r - 0.6, 2.2)), 40.0, (0.9, 0.95, 1.0), None])
    zf = 0.12
    if fit == "mess":
        table(b, made, 0.0, -0.6, zf, 2.0, 0.9, tag + "_table", stools=2)
        console_desk(b, made, -(r - 1.4), -1.2, zf, -60, 1.8, tag + "_desk")
        shelves(b, made, r - 1.25, -1.6, zf, 60, 1.8, 2.0, tag + "_shelf")
        lockers(b, made, -(r - 1.0), 1.6, zf, -110, 3, tag + "_lock")
    else:   # bunk room
        bunk(b, made, -(r - 1.3), -0.6, zf, 90, tag + "_bunk0")
        bunk(b, made, r - 1.3, -0.6, zf, 90, tag + "_bunk1")
        table(b, made, 0.0, -0.4, zf, 1.2, 0.7, tag + "_table", stools=2)
        shelves(b, made, 0.0, -(r - 0.9), zf, 0, 1.6, 1.8, tag + "_shelf")
    air = air_station(b, made, -2.2, r - 1.0, zf, facing_y=-1, tag=tag + "_air")
    m = moved(made, (x, y, 0), porch_yaw)
    turn_lights(b, m)
    b.here.points["Air"] = m @ air
    sliding_door(b, m, dx, front, 0.12, 1.1, 2.1, 1.2, tag + "_door")
    b.here.lights.append([m @ Vector((dx, front + 0.5, 2.3)), 30.0, (1.0, 0.85, 0.6), None])
    # --- solid: a ring of wall with a gap at the porch, a lid overhead, the porch
    n = 18
    gap = math.asin(min(1.0, (pw / 2 - 0.1) / r))
    for k in range(n):
        a = 2 * math.pi * (k + 0.5) / n
        if abs(math.atan2(math.cos(a), math.sin(a))) < gap:     # the porch side (+Y)
            continue
        w = 2 * math.pi * (r + 0.1) / n + 0.1
        b.hit_local(m, (math.cos(a) * (r + 0.05), math.sin(a) * (r + 0.05), 1.9), (0.5, w, 3.8), yaw=math.degrees(a))
    for s in (-1, 1):
        b.hit_local(m, (s * (pw / 2 - 0.08), (r - 0.6 + front) / 2, 1.4), (0.18, front - r + 0.6, 2.8))
    front_walls(b, m, dx, 0.6, pw, front - 0.08, 0.18, 0.0, 2.8)
    b.hit_local(m, (0, (r - 0.6 + front) / 2, 2.62), (pw, front - r + 0.6, 0.3))
    top = b.hit_drum((0, 0, 3.8), r * 0.95, crown - 3.8, radius_top=r * 0.25, sides=12)
    top.data.transform(m)
    b.here.points["Mid"] = Vector((x, y, 0))
    return m @ Vector((0, front + 0.9, 0))


def tunnel(b, a, c, r=1.0, tag="tunnel"):
    """A ribbed crawlway joining two buildings (sealed: it carries cables and pipes, not people)."""
    _tunnel(b, a, c, r, tag)


@structure("tunnel", cells=4, ruin=0.4)
def _tunnel(b, a, c, r, tag):
    a, c = Vector((a[0], a[1], r + 0.25)), Vector((c[0], c[1], r + 0.25))
    b.shell(oa.cylinder(tag, a, c, r, CREAM, segments=16), seed=3)
    n = max(2, int((c - a).length / 1.5))
    for i in range(1, n):
        p = a.lerp(c, i / n)
        d = (c - a).normalized() * 0.07
        b.frame(oa.cylinder(tag + "_rib", p - d, p + d, r + 0.05, GALV, segments=16))
    mid = (a + c) * 0.5
    yaw = math.degrees(math.atan2((c - a).y, (c - a).x))
    b.hit_box((mid.x, mid.y, (r * 2 + 0.25) / 2), ((c - a).length, r * 1.9, r * 2 + 0.25), yaw)
    b.here.points["Mid"] = Vector((mid.x, mid.y, 0))


@structure("tank", cells=6, ruin=0.6)
def tank(b, at, r=1.5, h=5.0, colour=None, tag="tank"):
    x, y = at
    colour = colour or WHITE
    body = oa.lathe(tag, (x, y, 0), (0, 0, 1), [(0.0, r * 0.96), (0.25, r), (h - r * 0.45, r), (h - r * 0.2, r * 0.8), (h, r * 0.3)], colour, segments=24)
    oa.densify(body, 1.2)
    b.shell(body, seed=int(x * 3))
    for z in (h * 0.25, h * 0.6):
        b.shell(oa.cylinder(tag + "_band", (x, y, z), (x, y, z + 0.12), r + 0.03, tuple(c * 0.85 for c in b.accent), segments=24), seed=1)
    b.detail(oa.cylinder(tag + "_valve", (x, y, h - 0.05), (x, y, h + 0.3), 0.18, GALV, segments=8, bevel=0.03))
    for s in (-1, 1):
        b.frame(beam(tag + "_rail", (x + s * 0.22, y - r - 0.08, 0), (x + s * 0.22, y - r - 0.08, h - r * 0.4), 0.045, GALV))
    for i in range(int((h - r * 0.4) / 0.3)):
        b.frame(beam(tag + "_rung", (x - 0.22, y - r - 0.08, 0.3 + i * 0.3), (x + 0.22, y - r - 0.08, 0.3 + i * 0.3), 0.03, GALV))
    b.hit_drum((x, y, 0), r + 0.05, h, sides=10)
    b.here.points["Mid"] = Vector((x, y, 0))


@structure("sphere", cells=7, ruin=0.8)
def ball_tank(b, at, r=2.0, tag="ball"):
    """A round fuel tank held up in a ring of legs."""
    x, y = at
    lift = r * 0.75
    ball = oa.sphere(tag, (x, y, lift + r), r, WHITE, segments=28)
    b.shell(ball, seed=int(x * 5 + y))
    b.shell(oa.cylinder(tag + "_belt", (x, y, lift + r - 0.1), (x, y, lift + r + 0.1), r * 1.012, tuple(c * 0.85 for c in b.accent), segments=28), seed=2)
    for k in range(6):
        a = 2 * math.pi * k / 6
        ox, oy = math.cos(a), math.sin(a)
        b.frame(beam(tag + "_leg", (x + ox * r * 0.86, y + oy * r * 0.86, lift + r * 0.75), (x + ox * r * 1.02, y + oy * r * 1.02, 0), 0.16, GALV))
        b.frame(oa.cylinder(tag + "_shoe", (x + ox * r * 1.02, y + oy * r * 1.02, 0), (x + ox * r * 1.02, y + oy * r * 1.02, 0.1), 0.24, CONCRETE, segments=8))
    b.frame(pipe(tag + "_ring", [(x + math.cos(2 * math.pi * k / 12) * r * 0.95, y + math.sin(2 * math.pi * k / 12) * r * 0.95, lift * 0.55) for k in range(13)], 0.05, GALV, sides=5))
    b.detail(oa.cylinder(tag + "_cap", (x, y, lift + 2 * r - 0.05), (x, y, lift + 2 * r + 0.25), 0.3, GALV, segments=10, bevel=0.04))
    b.detail(oa.cylinder(tag + "_drain", (x, y, lift - 0.02), (x, y, 0.5), 0.12, GALV, segments=8))
    b.hit_drum((x, y, 0), r * 1.02, lift + 2 * r, sides=10)
    b.here.points["Mid"] = Vector((x, y, 0))


def drum_antenna(b, centre, aim, size=0.9, tag="drum"):
    """A microwave link antenna: a short drum with a flat grey radome over its face, the kind bolted to every
    real relay mast."""
    c = Vector(centre)
    d = Vector(aim).normalized()
    body = oa.cylinder(tag, c - d * size * 0.22, c + d * size * 0.12, size / 2, (0.62, 0.62, 0.6), segments=20, bevel=0.03)
    cover = oa.cylinder(tag + "_radome", c + d * size * 0.12, c + d * size * 0.16, size / 2 - 0.02, (0.78, 0.77, 0.74), segments=20, bevel=0.02)
    b.detail(body, seed=3)
    b.detail(cover, seed=4)
    b.frame(beam(tag + "_mount", c - d * size * 0.22, c - d * size * 0.5, 0.06, GALV))


@structure("mast", cells=6, ruin=0.5)
def mast(b, at, height=13.0, dish=True, tag="mast"):
    """A three-legged galvanised lattice tower carrying microwave link drums and a warning light."""
    x, y = at
    foot, head = 1.1, 0.32
    legs = []
    for k in range(3):
        a = 2 * math.pi * k / 3 + 0.5
        legs.append((Vector((x + math.cos(a) * foot, y + math.sin(a) * foot, 0)), Vector((x + math.cos(a) * head, y + math.sin(a) * head, height))))
        b.frame(beam(tag + "_leg", legs[-1][0], legs[-1][1], 0.11, GALV), seed=k)
        b.frame(oa.cylinder(tag + "_shoe", legs[-1][0], legs[-1][0] + Vector((0, 0, 0.25)), 0.26, CONCRETE, segments=8))
    levels = int(height / 1.6)
    for i in range(1, levels + 1):
        t0, t1 = (i - 1) / levels, i / levels
        for k in range(3):
            p0, p1 = legs[k][0].lerp(legs[k][1], t1), legs[(k + 1) % 3][0].lerp(legs[(k + 1) % 3][1], t1)
            b.frame(beam(tag + "_ring", p0, p1, 0.045, GALV))
            b.frame(beam(tag + "_brace", legs[k][0].lerp(legs[k][1], t0), p1, 0.035, GALV))
    top = Vector((x, y, height))
    b.frame(oa.cylinder(tag + "_spike", top, top + Vector((0, 0, 1.8)), 0.035, GALV, segments=6))
    b.lamp(oa.sphere(tag + "_light", top + Vector((0, 0, 1.86)), 0.09, RED, segments=8))
    # a band of red and white at the top, the way tall masts are marked for aircraft
    for i in range(2):
        z0 = height - 1.6 + i * 0.8
        b.detail(oa.cylinder(tag + "_band%d" % i, (x, y, z0), (x, y, z0 + 0.4), 0.42 + (height - z0) * 0.0, (0.62, 0.22, 0.16), segments=3), seed=i)
    if dish:
        for k, (zz, a) in enumerate(((height - 1.0, 0.3), (height - 2.6, 2.4), (height - 4.0, 4.4))):
            aim = Vector((math.cos(a), math.sin(a), 0))
            drum_antenna(b, Vector((x, y, zz)) + aim * 0.75, aim, 0.95 if k == 0 else 0.7, tag + "_drum%d" % k)
        b.frame(pipe(tag + "_feeder", [(x + 0.2, y, height - 1.0), (x + 0.25, y, height * 0.5), (x + 0.3, y + 0.1, 0.4), (x + 1.4, y + 0.4, 0.05)], 0.035, DARK, sides=5))
    b.hit_box((x, y, height / 2), (foot * 1.5, foot * 1.5, height))
    b.here.points["Mid"] = Vector((x, y, 0))


@structure("dish", cells=7, ruin=1.4)
def ground_dish(b, at, yaw=0.0, diameter=4.6, elevation=38.0, tag="dish"):
    """A ground-station dish, built the way real ones are: a concrete plinth, a turning head on a steel
    pedestal, a yoke holding the elevation axle, a paraboloid reflector of panels on a ribbed back frame, and a
    feed horn held at the focus by four struts. The reflector looks up at `elevation` degrees toward +Y."""
    made = []
    R = diameter / 2
    f = diameter * 0.36                       # focal length: f/D 0.36, a usual deep-ish dish
    depth = R * R / (4 * f)
    pivot_z = R + 0.9
    # plinth, pedestal, turning head
    made.append(b.shell(oa.box(tag + "_plinth", (0, 0, 0.3), (2.0, 2.0, 0.6), CONCRETE, bevel=0.05), seed=1))
    made.append(b.frame(oa.cylinder(tag + "_pedestal", (0, 0, 0.6), (0, 0, pivot_z - 0.75), 0.42, (0.70, 0.70, 0.67), radius_end=0.36, segments=20, bevel=0.03), seed=2))
    made.append(b.frame(oa.cylinder(tag + "_bearing", (0, 0, pivot_z - 0.75), (0, 0, pivot_z - 0.55), 0.55, GALV, segments=24, bevel=0.03), seed=3))
    made.append(b.detail(oa.box(tag + "_drive", (0.45, -0.35, pivot_z - 0.95), (0.35, 0.3, 0.35), SLATE, bevel=0.03), seed=4))
    for s in (-1, 1):
        made.append(b.frame(oa.box(tag + "_yoke", (s * 0.62, 0, pivot_z - 0.25), (0.14, 0.5, 0.75), (0.70, 0.70, 0.67), bevel=0.03), seed=s + 5))
    made.append(b.frame(oa.cylinder(tag + "_axle", (-0.75, 0, pivot_z), (0.75, 0, pivot_z), 0.09, GALV, segments=10)))
    # the turning part, built facing +Z and tipped over afterwards
    tilt = Matrix.Translation((0, 0, pivot_z)) @ Matrix.Rotation(math.radians(-(90 - elevation)), 4, 'X') @ Matrix.Translation((0, 0, 0.35))
    turning = []
    rings = 9
    face, back = [], []
    for i in range(rings + 1):
        rr = R * i / rings
        face.append((rr * rr / (4 * f), rr))
    shell_profile = [(z, rr) for z, rr in face] + [(depth + 0.04, R + 0.02)] + [(z - 0.07, rr * 0.995) for z, rr in reversed(face[1:])] + [(-0.07, 0.0)]
    reflector = oa.lathe(tag + "_reflector", (0, 0, 0), (0, 0, 1), [(z, max(rr, 0.0)) for z, rr in shell_profile], (0.82, 0.81, 0.78), segments=40)
    oa.paint(reflector, wear=0.25, dirt=0.5, mottle=0.5, seed=7, grain=1.5, gradient=0.04)
    real.made_of(reflector, 0.18)
    turning.append(reflector)
    # panel seams on the face: rings and spokes painted a hair darker as thin raised strips
    for i in (3, 6):
        rr = R * i / rings
        turning.append(hoop(tag + "_seam%d" % i, (0, 0, rr * rr / (4 * f) + 0.012), (0, 0, 1), rr, 0.012, (0.66, 0.66, 0.63), sides=4, segments=40))
    rim = hoop(tag + "_rim", (0, 0, depth + 0.02), (0, 0, 1), R + 0.03, 0.045, (0.70, 0.70, 0.67), sides=6, segments=44)
    turning.append(rim)
    # back frame: hub, ribs, a ring
    turning.append(oa.cylinder(tag + "_hub", (0, 0, -0.55), (0, 0, -0.05), 0.42, (0.70, 0.70, 0.67), segments=16, bevel=0.03))
    for k in range(12):
        a = 2 * math.pi * k / 12
        tip = Vector((math.cos(a) * R * 0.97, math.sin(a) * R * 0.97, depth - 0.08))
        mid = Vector((math.cos(a) * R * 0.5, math.sin(a) * R * 0.5, -0.3))
        turning.append(beam(tag + "_rib", (math.cos(a) * 0.35, math.sin(a) * 0.35, -0.45), mid, 0.07, GALV))
        turning.append(beam(tag + "_rib2", mid, tip, 0.06, GALV))
    turning.append(pipe(tag + "_backring", [(math.cos(2 * math.pi * k / 24) * R * 0.5, math.sin(2 * math.pi * k / 24) * R * 0.5, -0.3) for k in range(25)], 0.035, GALV, sides=5))
    # feed: four struts from the rim to a horn at the focus
    focus = Vector((0, 0, f))
    for k in range(4):
        a = 2 * math.pi * k / 4 + math.pi / 4
        turning.append(beam(tag + "_strut", (math.cos(a) * R * 0.92, math.sin(a) * R * 0.92, depth - 0.02), focus + Vector((math.cos(a) * 0.12, math.sin(a) * 0.12, -0.25)), 0.045, GALV))
    turning.append(oa.cylinder(tag + "_feed", focus - Vector((0, 0, 0.55)), focus + Vector((0, 0, 0.05)), 0.13, (0.66, 0.66, 0.64), radius_end=0.2, segments=14, bevel=0.01))
    turning.append(oa.cylinder(tag + "_feed_cover", focus + Vector((0, 0, 0.05)), focus + Vector((0, 0, 0.08)), 0.19, (0.80, 0.79, 0.76), segments=14))
    # counterweight arm under the hub
    turning.append(beam(tag + "_arm", (0, 0, -0.5), (0, 0, -1.1), 0.18, GALV))
    turning.append(oa.box(tag + "_weight", (0, 0, -1.25), (0.6, 0.4, 0.35), SLATE, bevel=0.03))
    for o in turning:
        o.data.transform(tilt)
        if o is reflector:
            b.here.groups["Shell"].append(o)
        else:
            b.frame(o, seed=len(made))
        made.append(o)
    made.append(b.lamp(oa.sphere(tag + "_light", (0.6, -0.35, pivot_z - 0.72), 0.05, RED, segments=8)))
    made.append(b.frame(pipe(tag + "_cable", [(0.0, -0.3, 0.6), (0.2, -0.9, 0.3), (0.4, -1.4, 0.05)], 0.03, DARK, sides=5)))
    m = moved(made, (at[0], at[1], 0), yaw)
    b.hit_local(m, (0, 0, 0.3), (2.0, 2.0, 0.6))
    b.hit_local(m, (0, 0, pivot_z / 2), (1.0, 1.0, pivot_z))
    # the dish itself: a thick disc, tipped like it
    disc = b.hit_drum((0, 0, -0.6), R, 0.6 + depth, sides=12)
    disc.data.transform(m @ tilt)
    b.here.points["Mid"] = m @ Vector((0, 0, 0))


@structure("solar", cells=3, ruin=0.3)
def solar(b, at, yaw, w=3.4, h=2.1, tag="solar"):
    """A solar array on a post, tipped toward the sun: an aluminium frame holding cells with a fine silver grid."""
    made = [b.frame(oa.cylinder(tag + "_post", (0, 0, 0), (0, 0, 1.5), 0.08, GALV, segments=8)),
            b.frame(oa.cylinder(tag + "_foot", (0, 0, 0), (0, 0, 0.16), 0.32, CONCRETE, segments=10))]
    tilt = Matrix.Translation((0, 0, 1.55)) @ Matrix.Rotation(math.radians(-32), 4, 'X')
    frame = oa.box(tag + "_frame", (0, 0, 0), (w, 0.06, h), (0.72, 0.73, 0.73), bevel=0.012, segments=1)
    frame.data.transform(tilt)
    made.append(b.frame(frame))
    cols, rows = 6, 4
    for i in range(cols):
        for j in range(rows):
            cell = oa.box(tag + "_cell", ((i - (cols - 1) / 2) * w / cols, 0.032, (j - (rows - 1) / 2) * h / rows), (w / cols - 0.03, 0.012, h / rows - 0.03), SOLAR, bevel=0.0, segments=1)
            cell.data.transform(tilt)
            oa.paint(cell, wear=0.3, dirt=0.25, mottle=0.4, seed=i + j * 7, wear_colour=(0.20, 0.24, 0.32), grain=3)
            real.made_of(cell, 0.6)
            b.here.groups["Detail"].append(cell)
            made.append(cell)
    made.append(b.frame(beam(tag + "_brace", (0, -0.1, 0.9), (0, -0.55, 1.95), 0.05, GALV)))
    made.append(b.detail(oa.box(tag + "_box", (0, -0.12, 1.1), (0.25, 0.12, 0.3), (0.62, 0.62, 0.6), bevel=0.02), seed=2))
    moved(made, (at[0], at[1], 0), yaw)
    b.hit_box((at[0], at[1], 1.3), (w, 1.4, 2.6), yaw)
    b.here.points["Mid"] = Vector((at[0], at[1], 0))


@structure("lookout", cells=8, ruin=0.6)
def watchtower(b, at, yaw=0.0, height=6.5, tag="tower"):
    """A lookout on four splayed legs: a railed deck under a little roof, a ladder and a searchlight."""
    made = []
    foot, top = 1.7, 1.25
    corners = ((-1, -1), (1, -1), (1, 1), (-1, 1))
    for i, (sx, sy) in enumerate(corners):
        made.append(b.frame(beam(tag + "_leg", (sx * foot, sy * foot, 0), (sx * top, sy * top, height), 0.15, GALV), seed=i))
        made.append(b.frame(oa.cylinder(tag + "_shoe", (sx * foot, sy * foot, 0), (sx * foot, sy * foot, 0.2), 0.3, CONCRETE, segments=8)))
        nx, ny = corners[(i + 1) % 4]
        for lo, hi in ((0.08, 0.5), (0.5, 0.94)):
            a0 = Vector((sx * foot, sy * foot, 0)).lerp(Vector((sx * top, sy * top, height)), lo)
            b1 = Vector((nx * foot, ny * foot, 0)).lerp(Vector((nx * top, ny * top, height)), hi)
            made.append(b.frame(beam(tag + "_brace", a0, b1, 0.05, GALV)))
        made.append(b.frame(beam(tag + "_post", (sx * 1.45, sy * 1.45, height + 0.15), (sx * 1.45, sy * 1.45, height + 2.35), 0.08, GALV)))
        made.append(b.frame(beam(tag + "_rail", (sx * 1.5, sy * 1.5, height + 1.1), (nx * 1.5, ny * 1.5, height + 1.1), 0.06, tuple(c * 0.85 for c in b.accent))))
        if i != 0:   # the side with the ladder is left open below the top rail
            mid = (Vector((sx * 1.5, sy * 1.5, 0)) + Vector((nx * 1.5, ny * 1.5, 0))) * 0.5
            panel = oa.box(tag + "_skirt", (0, 0, 0), ((Vector((sx, sy, 0)) - Vector((nx, ny, 0))).length * 1.45, 0.05, 0.75), CREAM, bevel=0.015, segments=1)
            panel.data.transform(Matrix.Translation((mid.x, mid.y, height + 0.6)) @ Matrix.Rotation(math.atan2(ny - sy, nx - sx), 4, 'Z'))
            made.append(b.shell(panel, seed=i))
    made.append(b.shell(oa.box(tag + "_deck", (0, 0, height), (3.3, 3.3, 0.24), SLATE, bevel=0.04)))
    roof = oa.lathe(tag + "_roof", (0, 0, height + 2.35), (0, 0, 1), [(0.0, 2.45), (0.1, 2.45), (0.7, 0.25), (0.78, 0.0)], tuple(c * 0.8 for c in b.accent), segments=4)
    roof.data.transform(Matrix.Rotation(math.radians(45), 4, 'Z'))
    made.append(b.shell(roof, seed=7))
    for s in (-1, 1):
        made.append(b.frame(beam(tag + "_ladder", (s * 0.25, -foot - 0.05, 0), (s * 0.25, -top - 0.32, height + 1.0), 0.045, GALV)))
    for i in range(int(height / 0.3)):
        t = (i + 0.6) * 0.3 / height
        yy = -foot - 0.05 + (-top - 0.32 + foot + 0.05) * t * height / (height + 1.0)
        made.append(b.frame(beam(tag + "_rung", (-0.25, yy, t * height), (0.25, yy, t * height), 0.03, GALV)))
    made.append(b.detail(oa.cylinder(tag + "_search", (1.1, 1.3, height + 1.35), (1.1, 1.75, height + 1.3), 0.22, DARK, segments=12, bevel=0.03)))
    made.append(b.lamp(oa.cylinder(tag + "_beam", (1.1, 1.75, height + 1.3), (1.1, 1.78, height + 1.3), 0.18, WARM, segments=12)))
    m = moved(made, (at[0], at[1], 0), yaw)
    b.hit_box((at[0], at[1], height), (3.3, 3.3, 0.32), yaw)
    b.hit_box((at[0], at[1], height / 2 - 0.1), (2.4, 2.4, height - 0.25), yaw)
    b.hit_box((at[0], at[1], height + 2.7), (3.4, 3.4, 0.7), yaw)
    b.socket("Guard", m @ Vector((0, 0, height + 0.13)))
    b.here.points["Mid"] = Vector((at[0], at[1], 0))


@structure("shed", cells=8, ruin=0.7)
def shed(b, at, yaw, w=7.0, d=5.0, h=3.2, tag="shed", fit="workshop", hangar=False, roller=(0.0, 0.5), door_x=None, slide=1.1):
    """A ribbed steel building with a barrel roof. Entered by a sliding door beside the roller door (or, for a
    hangar, through the roller door itself, standing open). Inside: strip lights, what it is for, an air
    station. The front faces +Y."""
    made = []
    t = 0.14
    walls = oa.box(tag, (0, 0, h / 2), (w, d, h), CREAM, bevel=0.06)
    oa.densify(walls, 0.9)
    oa.paint(walls, wear=0.45, dirt=0.75, mottle=0.8, seed=11, grain=0.9, gradient=0.06)
    room = oa.box(tag + "_room", (0, 0, 0.1 + (h - 0.35) / 2), (w - 2 * t, d - 2 * t, h - 0.35), WALL_IN, bevel=0)
    oa.densify(room, 0.5)
    oa.paint(room, wear=0.2, dirt=0.4, mottle=0.4, seed=12, grain=2.0)
    roller_x, roller_w = roller[0], w * roller[1]
    roller_h = h * 0.8
    if hangar:
        door_x = roller_x
        cut = oa.box(tag + "_doorway", (roller_x, d / 2, roller_h / 2 + 0.05), (roller_w, 0.8, roller_h), DARK, bevel=0)
    else:
        door_x = w * 0.5 - 0.95 if door_x is None else door_x
        cut = oa.box(tag + "_doorway", (door_x, d / 2, 1.1), (1.1, 0.8, 2.1), DARK, bevel=0)
    nat.flat_paint(cut, (0.38, 0.38, 0.37))
    outside, inner = hollow(walls, [], [room], [cut], tag)
    made.append(outside)
    b.here.groups["Shell"].append(outside)
    made.append(inner)
    b.here.inside.append(inner)
    n = int(w / 0.6)
    for i in range(n + 1):
        x = -w / 2 + w * i / n
        for s in (-1, 1):
            if s > 0 and ((roller_w > 0 and abs(x - roller_x) < roller_w / 2 + 0.1) or (not hangar and min(abs(x - door_x), abs(x - door_x + slide)) < 0.7)):
                continue
            made.append(b.shell(oa.box(tag + "_rib", (x, s * (d / 2 + 0.025), h / 2), (0.1, 0.05, h * 0.97), WHITE, bevel=0.015, segments=1), seed=i))
    roof = oa.cylinder(tag + "_roof", (-w / 2 - 0.25, 0, h - 0.1), (w / 2 + 0.25, 0, h - 0.1), d / 2 + 0.25, tuple(c * 0.85 for c in b.accent), segments=20)
    roof.data.transform(Matrix.Translation((0, 0, h - 0.1)) @ Matrix.Diagonal((1, 1, 0.42, 1)) @ Matrix.Translation((0, 0, -h + 0.1)))
    bm = bmesh.new()
    bm.from_mesh(roof.data)
    frac._cut(bm, [(Vector((0, 0, h - 0.12)), Vector((0, 0, -1)))], tuple(c * 0.85 for c in b.accent), 0)
    bm.to_mesh(roof.data)
    bm.free()
    oa.densify(roof, 1.0)
    made.append(b.shell(roof, seed=12))
    made.append(b.shell(oa.box(tag + "_eaves", (0, 0, h - 0.06), (w + 0.5, d + 0.5, 0.1), tuple(c * 0.85 for c in b.accent), bevel=0.02, segments=1), seed=13))
    if hangar:
        # the roller door, rolled up into its drum over the opening
        made.append(b.detail(oa.cylinder(tag + "_drum", (roller_x - roller_w / 2, d / 2 + 0.25, roller_h + 0.3), (roller_x + roller_w / 2, d / 2 + 0.25, roller_h + 0.3), 0.28, SLATE, segments=14)))
        for s in (-1, 1):
            made.append(b.frame(oa.box(tag + "_guide", (roller_x + s * (roller_w / 2 + 0.06), d / 2 + 0.08, roller_h / 2), (0.1, 0.12, roller_h), STEEL, bevel=0.01, segments=1)))
    else:
        if roller_w > 0:
            made.append(b.detail(oa.box(tag + "_roller", (roller_x, d / 2 + 0.04, roller_h / 2), (roller_w, 0.06, roller_h), SLATE, bevel=0.02, segments=1)))
            for i in range(int(roller_h / 0.16)):
                made.append(b.detail(oa.box(tag + "_slat", (roller_x, d / 2 + 0.075, 0.1 + i * 0.16), (roller_w, 0.012, 0.03), DARK, bevel=0, segments=1)))
        made.append(b.lamp(oa.box(tag + "_door_lamp", (door_x, d / 2 + 0.06, 2.4), (0.55, 0.08, 0.08), WARM, bevel=0.02, segments=1)))
    if roller_w > 0:
        made.append(b.lamp(oa.box(tag + "_lamp", (roller_x, d / 2 + 0.08, h * 0.9), (1.0, 0.08, 0.08), WARM, bevel=0.02, segments=1)))
    for s in (-1, 1):
        made.append(b.window(oa.box(tag + "_window", (s * (w / 2 + 0.02), 0, h * 0.62), (0.04, d * 0.36, 0.6), WARM, bevel=0.01, segments=1)))
        made.append(b.window(oa.box(tag + "_window_in", (s * (w / 2 - t - 0.01), 0, h * 0.62), (0.01, d * 0.36, 0.6), COOL, bevel=0.0, segments=1)))
        made.append(b.frame(oa.box(tag + "_window_frame", (s * (w / 2 + 0.01), 0, h * 0.62), (0.06, d * 0.36 + 0.14, 0.74), STEEL, bevel=0.02, segments=1)))
    made.append(b.detail(oa.cylinder(tag + "_flue", (w * 0.3, -d * 0.2, h + 0.4), (w * 0.3, -d * 0.2, h + 2.0), 0.14, GALV, segments=8, bevel=0.03)))
    # --- inside
    made.append(b.inside(oa.box(tag + "_floor", (0, 0, 0.07), (w - 2 * t + 0.05, d - 2 * t + 0.05, 0.14), FLOOR, bevel=0), seed=3))
    zc = h - 0.3
    ceiling_strip(b, made, (-w / 2 + 0.6, -d * 0.18, zc), (w / 2 - 0.6, -d * 0.18, zc), tag + "_strip0")
    ceiling_strip(b, made, (-w / 2 + 0.6, d * 0.18, zc), (w / 2 - 0.6, d * 0.18, zc), tag + "_strip1")
    zf = 0.1
    back = -d / 2 + t
    if fit == "workshop":
        workbench(b, made, -w / 2 + 1.6, back + 0.4, zf, 0, 2.2, tag + "_bench")
        shelves(b, made, w / 2 - 1.2, back + 0.25, zf, 0, 1.6, 1.9, tag + "_shelf")
        made.append(b.inside(oa.box(tag + "_gen", (-w / 2 + 0.9, d * 0.1, zf + 0.45), (0.9, 0.7, 0.9), (0.52, 0.48, 0.30), bevel=0.04), seed=4))
        made.append(b.inside(oa.cylinder(tag + "_drum0", (-w / 2 + 0.6, d * 0.32, zf), (-w / 2 + 0.6, d * 0.32, zf + 0.9), 0.3, RUST, segments=14, bevel=0.03), seed=5))
    elif fit == "pumps":
        for k in range(2):
            px = -w / 2 + 0.9 + k * 1.3
            made.append(b.inside(oa.cylinder(tag + "_pump%d" % k, (px, back + 0.6, zf), (px, back + 0.6, zf + 1.1), 0.35, (0.36, 0.42, 0.48), segments=16, bevel=0.03), seed=k))
            made.append(b.inside(oa.cylinder(tag + "_motor%d" % k, (px, back + 0.6, zf + 1.1), (px, back + 0.6, zf + 1.6), 0.25, SLATE, segments=14, bevel=0.03), seed=k))
            made.append(b.inside(pipe(tag + "_line%d" % k, [(px, back + 0.6, zf + 0.5), (px, back + 0.15, zf + 0.5), (px, back + 0.15, h - 0.6)], 0.07, GALV, sides=8)))
        made.append(b.inside(oa.box(tag + "_panel", (w / 2 - 0.6, back + 0.15, zf + 1.2), (0.7, 0.25, 1.1), (0.55, 0.57, 0.55), bevel=0.02), seed=6))
        made.append(b.lamp(oa.box(tag + "_panel_lamp", (w / 2 - 0.75, back + 0.28, zf + 1.5), (0.06, 0.01, 0.06), GREEN, bevel=0, segments=1)))
    else:   # hangar: a clear floor, crates and drums along the walls, a workbench at the back
        workbench(b, made, -w / 2 + 1.6, back + 0.4, zf, 0, 2.2, tag + "_bench")
        shelves(b, made, w / 2 - 1.2, back + 0.25, zf, 0, 1.6, 2.2, tag + "_shelf")
        for k in range(3):
            made.append(b.inside(oa.cylinder(tag + "_drum%d" % k, (w / 2 - 0.5, -d * 0.05 + k * 0.65, zf), (w / 2 - 0.5, -d * 0.05 + k * 0.65, zf + 0.9), 0.3, (RUST, OLIVE, SLATE)[k], segments=14, bevel=0.03), seed=k))
    air = air_station(b, made, 0.4 if fit != "pumps" else 0.9, back, zf, facing_y=1, tag=tag + "_air")
    m = moved(made, (at[0], at[1], 0), yaw)
    turn_lights(b, m)
    b.here.points["Air"] = m @ air
    if not hangar:
        sliding_door(b, m, door_x, d / 2, 0.1, 1.05, 2.05, slide, tag + "_door")
    b.here.lights.append([m @ Vector((door_x, d / 2 + 0.5, 2.3)), 30.0, (1.0, 0.85, 0.6), None])
    # --- solid: walls with the doorway left open, roof, the bench
    hl = lambda c, s: b.hit_local(m, c, s)
    hl((0, -d / 2 + t / 2, h / 2), (w, t, h))
    for s in (-1, 1):
        hl((s * (w / 2 - t / 2), 0, h / 2), (t, d, h))
    gap_l, gap_r = (roller_x - roller_w / 2, roller_x + roller_w / 2) if hangar else (door_x - 0.55, door_x + 0.55)
    hl(((-w / 2 + gap_l) / 2, d / 2 - t / 2, h / 2), (gap_l + w / 2, t, h))
    hl(((gap_r + w / 2) / 2, d / 2 - t / 2, h / 2), (w / 2 - gap_r, t, h))
    top = (roller_h if hangar else 2.1) + 0.05
    hl(((gap_l + gap_r) / 2, d / 2 - t / 2, (top + h) / 2), (gap_r - gap_l, t, h - top))
    hl((0, 0, h + 0.4), (w + 0.5, d + 0.5, 1.0))
    b.here.points["Mid"] = m @ Vector((0, 0, 0))
    return m


@structure("wall", cells=6, ruin=0.9)
def wall(b, a, c, height=3.4, tag="wall"):
    """A run of precast concrete panels between two points, posts at the joints and a rail on top."""
    a, c = Vector((a[0], a[1], 0)), Vector((c[0], c[1], 0))
    length = (c - a).length
    yaw = math.degrees(math.atan2((c - a).y, (c - a).x))
    n = max(1, round(length / 3.6))
    made = []
    for i in range(n):
        x = -length / 2 + length * (i + 0.5) / n
        panel = oa.box(tag, (x, 0, height / 2), (length / n - 0.08, 0.32, height), CONCRETE if i % 2 else (0.62, 0.61, 0.57), bevel=0.04, segments=1)
        oa.densify(panel, 1.2)
        made.append(b.shell(panel, seed=i + int(a.x)))
        made.append(b.shell(oa.box(tag + "_band", (x, 0, height * 0.82), (length / n - 0.06, 0.34, 0.16), tuple(cc * 0.8 for cc in b.accent), bevel=0.01, segments=1), seed=i))
    for i in range(n + 1):
        x = -length / 2 + length * i / n
        made.append(b.frame(oa.box(tag + "_post", (x, 0, (height + 0.3) / 2), (0.28, 0.5, height + 0.3), SLATE, bevel=0.04, segments=1), seed=i))
    made.append(b.frame(beam(tag + "_rail", (-length / 2, 0, height + 0.45), (length / 2, 0, height + 0.45), 0.06, GALV)))
    mid = (a + c) * 0.5
    moved(made, (mid.x, mid.y, 0), yaw)
    b.hit_box((mid.x, mid.y, (height + 0.3) / 2), (length, 0.5, height + 0.3), yaw)
    b.here.points["Mid"] = Vector((mid.x, mid.y, 0))


@structure("gate", cells=6, ruin=0.8)
def gate(b, at, yaw, width=6.0, height=5.2, tag="gate"):
    """Two stout pylons and a beam across: the way in. Open, never closed."""
    made = []
    for s in (-1, 1):
        pylon = oa.box(tag + "_pylon", (s * (width / 2 + 0.8), 0, height / 2), (1.6, 1.5, height), SLATE, bevel=0.08)
        oa.densify(pylon, 1.0)
        made.append(b.shell(pylon, seed=s + 3))
        made.append(b.shell(oa.box(tag + "_cap", (s * (width / 2 + 0.8), 0, height + 0.1), (1.75, 1.65, 0.2), tuple(c * 0.8 for c in b.accent), bevel=0.04)))
        made.append(b.lamp(oa.box(tag + "_lamp", (s * (width / 2 + 0.8), 0.78, height * 0.72), (0.35, 0.06, 0.6), WARM, bevel=0.015, segments=1)))
        made.append(b.lamp(oa.sphere(tag + "_beacon", (s * (width / 2 + 0.8), 0, height + 0.32), 0.12, RED, segments=8)))
    made.append(b.frame(oa.box(tag + "_lintel", (0, 0, height - 0.5), (width + 1.0, 0.5, 0.5), STEEL, bevel=0.04)))
    made.append(b.frame(oa.box(tag + "_lintel_low", (0, 0, height - 1.2), (width + 0.2, 0.25, 0.2), STEEL, bevel=0.03, segments=1)))
    for i in range(5):
        x = -width / 2 + width * (i + 0.5) / 5
        made.append(b.frame(beam(tag + "_web", (x - width / 10, 0, height - 1.15), (x + width / 10, 0, height - 0.55), 0.06, GALV)))
    m = moved(made, (at[0], at[1], 0), yaw)
    for s in (-1, 1):
        b.hit_box(m @ Vector((s * (width / 2 + 0.8), 0, height / 2)), (1.6, 1.5, height), yaw)
    b.hit_box(m @ Vector((0, 0, height - 0.75)), (width + 1.0, 0.6, 1.1), yaw)
    b.here.points["Mid"] = Vector((at[0], at[1], 0))


def pad(b, at, r=4.0):
    """A marked landing circle with a ring of ground lights. Flat: nothing to trip on. Cannot be broken."""
    x, y = at
    b.shell(oa.lathe("pad", (x, y, 0), (0, 0, 1), [(0.0, r), (0.06, r), (0.06, r - 0.4), (0.02, r - 0.4)], (0.62, 0.58, 0.40), segments=40), seed=8)
    floor = oa.lathe("pad_floor", (x, y, 0), (0, 0, 1), [(0.0, r - 0.4), (0.04, r - 0.4)], (0.30, 0.31, 0.32), segments=40)
    oa.densify(floor, 1.0)
    b.shell(floor, seed=9)
    b.shell(oa.lathe("pad_mark", (x, y, 0), (0, 0, 1), [(0.04, r * 0.28), (0.06, r * 0.28)], (0.74, 0.73, 0.70), segments=24), seed=9)
    for k in range(4):
        a = 2 * math.pi * k / 4 + 0.785
        b.shell(oa.box("pad_tick", (x + math.cos(a) * r * 0.62, y + math.sin(a) * r * 0.62, 0.045), (r * 0.3, 0.25, 0.03), (0.74, 0.73, 0.70), bevel=0, rot=(0, 0, math.degrees(a))), seed=k)
    for k in range(8):
        a = 2 * math.pi * (k + 0.5) / 8
        px, py = x + math.cos(a) * (r + 0.35), y + math.sin(a) * (r + 0.35)
        b.detail(oa.cylinder("pad_pot", (px, py, 0), (px, py, 0.08), 0.15, DARK, segments=8))
        b.lamp(oa.cylinder("pad_light", (px, py, 0.08), (px, py, 0.11), 0.09, CYAN, segments=8))
    b.socket("Pad", (x, y, 0.0))


def spoil(b, at, r, h, seed, colour=((0.30, 0.27, 0.26), (0.50, 0.45, 0.41))):
    """A heap of dug rock. Part of the ground: it cannot be broken."""
    x, y = at
    lobes = [((0, 0, 0), (r * 0.8, r * 0.75, h)), ((r * 0.5, r * 0.2, 0), (r * 0.6, r * 0.6, h * 0.6)), ((-r * 0.45, -r * 0.3, 0), (r * 0.6, r * 0.55, h * 0.5)), ((0, 0, 0), (r, r, h * 0.3))]
    o = nat.lobed("spoil", (x, y, 0.05), lobes, colour[0], n=8, lump=0.22, grain=2.6, seed=seed, flat=-0.05, smooth_angle=50, ridged=0.35)
    for v in o.data.vertices:
        v.co.z = max(v.co.z, -0.25)
    nat.rock_paint(o, colour[0], colour[1], seed=seed, strata=0.0, grain=1.2)
    b.here.groups["Detail"].append(o)
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
    b.here.hits.append(hull)


@structure("headframe", cells=10, ruin=0.6)
def headframe(b, at, yaw, height=13.0, tag="head"):
    """The winding tower over a mine shaft: two A-frames, a pair of big wheels and a cable going down the hole."""
    made = []
    top = height
    for sx in (-1, 1):
        for sy, reach in ((-1, 2.4), (1, 6.2)):
            made.append(b.frame(beam(tag + "_leg", (sx * 1.9, sy * reach, 0), (sx * 0.95, sy * 0.35, top), 0.3, tuple(c * 0.85 for c in b.accent), bevel=0.06), seed=int(sx + sy)))
            made.append(b.frame(oa.box(tag + "_foot", (sx * 1.9, sy * reach, 0.2), (0.9, 0.9, 0.4), CONCRETE, bevel=0.04)))
        for t in (0.3, 0.55, 0.8):
            p0 = Vector((sx * 1.9, -2.4, 0)).lerp(Vector((sx * 0.95, -0.35, top)), t)
            p1 = Vector((sx * 1.9, 6.2, 0)).lerp(Vector((sx * 0.95, 0.35, top)), t)
            made.append(b.frame(beam(tag + "_tie", p0, p1, 0.12, STEEL)))
    for t in (0.3, 0.55, 0.8, 1.0):
        for sy, reach, y1 in ((-1, 2.4, -0.35), (1, 6.2, 0.35)):
            p0 = Vector((-1.9, sy * reach, 0)).lerp(Vector((-0.95, y1, top)), t)
            p1 = Vector((1.9, sy * reach, 0)).lerp(Vector((0.95, y1, top)), t)
            made.append(b.frame(beam(tag + "_cross", p0, p1, 0.12, STEEL)))
    made.append(b.shell(oa.box(tag + "_cabin", (0, 0, top + 0.55), (2.6, 1.6, 1.1), CREAM, bevel=0.06)))
    made.append(b.shell(oa.box(tag + "_cabin_roof", (0, 0, top + 1.15), (2.9, 1.9, 0.12), tuple(c * 0.8 for c in b.accent), bevel=0.03, segments=1)))
    for sx in (-0.55, 0.55):
        wheel = oa.lathe(tag + "_wheel", (sx, 0, top - 0.9), (1, 0, 0), [(-0.07, 1.2), (-0.07, 1.38), (0.07, 1.38), (0.07, 1.2)], DARK, segments=24)
        made.append(b.frame(wheel))
        for k in range(4):
            a = math.pi * k / 4
            made.append(b.frame(beam(tag + "_spoke", (sx, -math.cos(a) * 1.25, top - 0.9 - math.sin(a) * 1.25), (sx, math.cos(a) * 1.25, top - 0.9 + math.sin(a) * 1.25), 0.05, STEEL)))
        made.append(b.frame(oa.cylinder(tag + "_cable", (sx, -1.3, top - 0.9), (sx, -1.3, 0.4), 0.03, DARK, segments=5)))
    made.append(b.frame(oa.cylinder(tag + "_axle", (-0.95, 0, top - 0.9), (0.95, 0, top - 0.9), 0.1, STEEL, segments=8)))
    made.append(b.shell(oa.lathe(tag + "_collar", (0, -1.3, 0), (0, 0, 1), [(0.0, 2.0), (0.7, 2.0), (0.7, 1.55), (0.25, 1.55)], CONCRETE, segments=20), seed=2))
    made.append(b.detail(oa.cylinder(tag + "_shaft", (0, -1.3, 0.2), (0, -1.3, 0.26), 1.55, RUBBER, segments=20)))
    made.append(b.lamp(oa.box(tag + "_lamp", (0, 0.82, top + 0.55), (1.2, 0.06, 0.3), WARM, bevel=0.02, segments=1)))
    made.append(b.lamp(oa.sphere(tag + "_beacon", (0, 0, top + 1.4), 0.12, RED, segments=8)))
    m = moved(made, (at[0], at[1], 0), yaw)
    b.hit_drum((m @ Vector((0, -1.3, 0)))[:], 2.0, 0.7, sides=10)
    b.hit_box(m @ Vector((0, 0, top + 0.2)), (2.9, 1.9, 2.4), yaw)
    for sx in (-1, 1):
        lean_back = Matrix.Rotation(math.atan2(2.05, top), 4, 'X')
        b.hit_box(m @ Vector((sx * 1.42, -1.37, top / 2)), (0.5, 0.5, top), yaw, tilt=lean_back)
        lean_front = Matrix.Rotation(-math.atan2(5.85, top), 4, 'X')
        b.hit_box(m @ Vector((sx * 1.42, 3.27, top / 2)), (0.5, 0.5, math.hypot(top, 5.85)), yaw, tilt=lean_front)
    b.here.points["Mid"] = Vector((at[0], at[1], 0))
    return m


@structure("conveyor", cells=6, ruin=0.5)
def conveyor(b, a, c, rise, width=1.3, tag="belt"):
    """An ore belt climbing from a low end to a high end on trestles."""
    a, c = Vector((a[0], a[1], 0.8)), Vector((c[0], c[1], rise))
    along = c - a
    length = along.length
    yaw = math.degrees(math.atan2(along.y, along.x))
    pitch = math.atan2(along.z, math.hypot(along.x, along.y))
    turn = Matrix.Translation((a + c) * 0.5) @ Matrix.Rotation(math.radians(yaw), 4, 'Z') @ Matrix.Rotation(-pitch, 4, 'Y')
    made = [oa.box(tag + "_deck", (0, 0, 0), (length, width, 0.3), tuple(cc * 0.85 for cc in b.accent), bevel=0.04),
            oa.box(tag + "_belt", (0, 0, 0.17), (length * 0.99, width * 0.72, 0.06), RUBBER, bevel=0.02, segments=1)]
    for s in (-1, 1):
        made.append(oa.box(tag + "_side", (0, s * width * 0.48, 0.32), (length, 0.08, 0.36), STEEL, bevel=0.015, segments=1))
    n = int(length / 1.3)
    for i in range(n + 1):
        x = -length / 2 + length * i / n
        for s in (-1, 1):
            made.append(oa.box(tag + "_stay", (x, s * width * 0.48, 0.3), (0.07, 0.09, 0.42), GALV, bevel=0.01, segments=1))
    for i in range(int(length / 2.2)):
        x = -length / 2 + 1.1 + i * 2.2
        made.append(oa.sphere(tag + "_ore", (x, 0.0, 0.3), 0.32, (0.38, 0.32, 0.28), scale=(1.3, 1.0, 0.6), segments=8))
    for o in made:
        o.data.transform(turn)
    b.shell(made[0], seed=5)
    for o in made[1:]:
        (b.detail if "ore" in o.name or "belt" in o.name else b.frame)(o)
    for t in (0.3, 0.62, 0.92):
        p = a.lerp(c, t)
        side = Vector((-along.y, along.x, 0)).normalized() * (width * 0.5)
        for s in (-1, 1):
            b.frame(beam(tag + "_trestle", (p.x + s * side.x * 1.7, p.y + s * side.y * 1.7, 0), (p.x + s * side.x, p.y + s * side.y, p.z - 0.1), 0.12, GALV))
        b.frame(beam(tag + "_trestle_tie", (p.x - side.x * 1.4, p.y - side.y * 1.4, p.z * 0.5), (p.x + side.x * 1.4, p.y + side.y * 1.4, p.z * 0.5), 0.08, GALV))
    mid = (a + c) * 0.5
    b.hit_box((mid.x, mid.y, mid.z + 0.05), (length, width, 0.7), yaw, tilt=Matrix.Rotation(-pitch, 4, 'Y'))
    b.here.points["Mid"] = Vector((mid.x, mid.y, 0))


@structure("hopper", cells=6, ruin=0.6)
def hopper(b, at, top=7.0, tag="hopper"):
    """A bin on legs that the belt tips into, with a chute underneath."""
    x, y = at
    bin_ = oa.lathe(tag, (x, y, top - 3.0), (0, 0, 1), [(0.0, 0.5), (1.6, 2.1), (3.0, 2.1), (3.0, 1.95), (1.65, 1.95)], CREAM, segments=4)
    bin_.data.transform(Matrix.Translation((x, y, 0)) @ Matrix.Rotation(math.radians(45), 4, 'Z') @ Matrix.Translation((-x, -y, 0)))
    oa.densify(bin_, 1.0)
    b.shell(bin_, seed=6)
    b.shell(oa.box(tag + "_band", (x, y, top - 0.25), (3.05, 3.05, 0.25), tuple(c * 0.85 for c in b.accent), bevel=0.03, segments=1), seed=7)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.frame(beam(tag + "_leg", (x + sx * 1.6, y + sy * 1.6, 0), (x + sx * 1.35, y + sy * 1.35, top - 1.4), 0.18, GALV))
            b.frame(oa.cylinder(tag + "_shoe", (x + sx * 1.6, y + sy * 1.6, 0), (x + sx * 1.6, y + sy * 1.6, 0.18), 0.28, CONCRETE, segments=8))
    for z in (1.6, 3.2):
        for (ax, ay), (bx, by) in (((-1, -1), (1, -1)), ((1, -1), (1, 1)), ((1, 1), (-1, 1)), ((-1, 1), (-1, -1))):
            k = 1.6 - 0.25 * z / (top - 1.4)
            b.frame(beam(tag + "_tie", (x + ax * k, y + ay * k, z), (x + bx * k, y + by * k, z), 0.07, GALV))
    b.detail(oa.box(tag + "_chute", (x, y - 0.5, top - 3.5), (0.8, 1.6, 0.5), SLATE, bevel=0.04, rot=(28, 0, 0)))
    b.lamp(oa.box(tag + "_lamp", (x, y - 1.58, top - 0.25), (0.8, 0.05, 0.12), WARM, bevel=0.015, segments=1))
    b.hit_box((x, y, top - 1.5), (3.1, 3.1, 3.0))
    b.hit_box((x, y, (top - 3.0) / 2), (3.3, 3.3, top - 3.0))
    b.here.points["Mid"] = Vector((x, y, 0))


@structure("cart", cells=3, ruin=0.0)
def cart(b, at, yaw, tag="cart"):
    made = [b.detail(oa.box(tag, (0, 0, 0.85), (1.9, 1.2, 0.9), tuple(c * 0.85 for c in b.accent), bevel=0.05)),
            b.detail(oa.sphere(tag + "_load", (0, 0, 1.28), 0.62, (0.38, 0.31, 0.27), scale=(1.3, 0.85, 0.45), segments=10)),
            b.frame(oa.box(tag + "_chassis", (0, 0, 0.36), (2.1, 0.9, 0.14), STEEL, bevel=0.02, segments=1))]
    for sx in (-0.65, 0.65):
        for sy in (-0.5, 0.5):
            made.append(b.frame(oa.cylinder(tag + "_wheel", (sx, sy - 0.06, 0.24), (sx, sy + 0.06, 0.24), 0.24, DARK, segments=12)))
    moved(made, (at[0], at[1], 0), yaw)
    b.hit_box((at[0], at[1], 0.75), (2.1, 1.25, 1.5), yaw)
    b.here.points["Mid"] = Vector((at[0], at[1], 0))


def track(b, at, yaw, tag="track"):
    """Rails and sleepers under the cart: part of the ground."""
    made = []
    for sy in (-0.5, 0.5):
        made.append(b.frame(beam(tag + "_rail", (-2.6, sy, 0.03), (2.6, sy, 0.03), 0.07, STEEL)))
    for i in range(6):
        made.append(b.detail(oa.box(tag + "_tie", (-2.3 + i * 0.92, 0, 0.012), (0.22, 1.5, 0.05), (0.30, 0.24, 0.18), bevel=0.01, segments=1)))
    moved(made, (at[0], at[1], 0), yaw)


@structure("keep", cells=12, ruin=1.6)
def command_tower(b, at, yaw, height=15.0, tag="keep"):
    """The heart of a stronghold: a stout round tower that flares into a lit control room, with a radar on top.
    Its ground floor is a guard room you can walk into: lockers, a desk of screens, an air station."""
    x, y = at
    made = []
    shaft = oa.lathe(tag, (0, 0, 0), (0, 0, 1), [(0.0, 3.6), (0.6, 3.6), (1.4, 3.0), (height - 5.2, 2.5), (height - 4.0, 4.3), (height - 3.6, 4.5), (height - 3.6, 0.0)], SLATE, segments=32)
    oa.densify(shaft, 1.0)
    oa.paint(shaft, wear=0.45, dirt=0.75, mottle=0.8, seed=21, grain=0.9, gradient=0.06)
    dx = 0.55
    porch = oa.box(tag + "_porch", (0, 3.4, 1.4), (3.6, 2.4, 2.8), CREAM, bevel=0.1)
    oa.densify(porch, 0.8)
    oa.paint(porch, wear=0.45, dirt=0.7, mottle=0.7, seed=22, grain=1.2)
    room = oa.lathe(tag + "_room", (0, 0, 0), (0, 0, 1), [(0.12, 2.45), (3.3, 2.3), (3.3, 0.0)], WALL_IN, segments=32)
    oa.densify(room, 0.6)
    oa.paint(room, wear=0.2, dirt=0.4, mottle=0.4, seed=23, grain=2.0)
    vest = oa.box(tag + "_vest", (0, 3.0, 1.25), (3.3, 2.6, 2.3), WALL_IN, bevel=0)
    oa.paint(vest, wear=0.2, dirt=0.4, mottle=0.4, seed=24, grain=2.0)
    cut = oa.box(tag + "_doorway", (dx, 4.6, 1.12), (1.2, 0.6, 2.2), DARK, bevel=0)
    nat.flat_paint(cut, (0.38, 0.38, 0.37))
    outside, inner = hollow(shaft, [porch], [room, vest], [cut], tag)
    made.append(outside)
    b.here.groups["Shell"].append(outside)
    made.append(inner)
    b.here.inside.append(inner)
    made.append(b.window(oa.lathe(tag + "_glass", (0, 0, 0), (0, 0, 1), [(height - 3.6, 4.48), (height - 2.2, 4.75), (height - 2.2, 4.4), (height - 3.6, 4.2)], WARM, segments=32)))
    for k in range(12):
        a = 2 * math.pi * k / 12
        made.append(b.frame(beam(tag + "_mullion", (math.cos(a) * 4.52, math.sin(a) * 4.52, height - 3.65), (math.cos(a) * 4.8, math.sin(a) * 4.8, height - 2.15), 0.12, DARK)))
    roof = oa.lathe(tag + "_roof", (0, 0, 0), (0, 0, 1), [(height - 2.2, 0.0), (height - 2.2, 4.4), (height - 2.2, 5.0), (height - 1.9, 5.0), (height - 1.2, 3.2), (height - 1.0, 1.2), (height - 1.0, 0.0)], tuple(c * 0.8 for c in b.accent), segments=32)
    # closed underneath (the room's ceiling hides it whole), so the pieces it breaks into are solid all round
    oa.densify(roof, 1.0)
    made.append(b.shell(roof, seed=22))
    for z in (2.6, height - 5.6):
        made.append(b.shell(hoop(tag + "_band", (0, 0, z + 0.1), (0, 0, 1), 2.98 if z < 5 else 2.6, 0.1, tuple(c * 0.8 for c in b.accent)), seed=23))
    for k in range(4):
        a = 2 * math.pi * (k + 0.5) / 4
        made.append(b.window(oa.box(tag + "_slit", (math.cos(a) * 2.72, math.sin(a) * 2.72, height * 0.42), (0.1, 0.4, 1.4), WARM, bevel=0.015, segments=1, rot=(0, 0, math.degrees(a)))))
    # radar: a slowly turning bar antenna on a pedestal, and a cluster of aerials
    made.append(b.frame(oa.cylinder(tag + "_pedestal", (0, 0, height - 1.0), (0, 0, height + 0.4), 0.35, GALV, segments=12, bevel=0.04)))
    radar = oa.box(tag + "_radar", (0, 0, 0), (4.2, 0.18, 0.9), (0.74, 0.74, 0.71), bevel=0.06)
    radar.data.transform(Matrix.Translation((0, 0, height + 1.0)) @ Matrix.Rotation(0.5, 4, 'Z') @ Matrix.Rotation(math.radians(-14), 4, 'X'))
    made.append(b.shell(radar, seed=24))
    for k, (ox, oy, hgt) in enumerate(((2.2, 1.6, 2.6), (-2.0, 2.2, 1.8), (-2.6, -1.4, 3.2))):
        made.append(b.frame(oa.cylinder(tag + "_aerial", (ox, oy, height - 1.3), (ox, oy, height - 1.3 + hgt), 0.03, DARK, segments=5)))
        made.append(b.lamp(oa.sphere(tag + "_aerial_light", (ox, oy, height - 1.25 + hgt), 0.07, RED, segments=6)))
    made.append(b.lamp(oa.box(tag + "_door_lamp", (dx, 4.63, 2.5), (0.8, 0.08, 0.08), WARM, bevel=0.02, segments=1)))
    # --- inside: a guard room
    made.append(b.inside(floor_disc(tag + "_floor", (0, 0, 0.0), 2.48, FLOOR), seed=3))
    made.append(b.inside(oa.box(tag + "_floor_porch", (0, 3.0, 0.07), (3.3, 2.6, 0.14), FLOOR, bevel=0), seed=4))
    made.append(b.inside(oa.cylinder(tag + "_lamp", (0, 0, 3.12), (0, 0, 3.24), 0.5, PANEL_IN, segments=20)))
    made.append(b.lamp(oa.cylinder(tag + "_lamp_glow", (0, 0, 3.10), (0, 0, 3.12), 0.42, COOL, segments=20)))
    b.here.lights.append([Vector((0, 0, 2.9)), 160.0, (1.0, 0.96, 0.9), None])
    lockers(b, made, -1.2, -1.95, 0.12, 15, 3, tag + "_lock")
    console_desk(b, made, 1.35, -1.25, 0.12, 40, 1.4, tag + "_desk")
    # a steel stair winding up toward the control room (closed off at the top)
    for k in range(10):
        a = math.radians(-160 + k * 24)
        made.append(b.inside(oa.box(tag + "_tread", (math.cos(a) * 1.5 - 0.5, math.sin(a) * 1.5 + 0.2, 0.3 + k * 0.3), (0.7, 0.3, 0.05), GALV, bevel=0, segments=1, rot=(0, 0, math.degrees(a) + 90))))
    made.append(b.inside(oa.cylinder(tag + "_newel", (-0.5, 0.2, 0.12), (-0.5, 0.2, 3.3), 0.06, GALV, segments=8)))
    air = air_station(b, made, 1.7, 1.25, 0.12, facing_y=-1, tag=tag + "_air")
    m = moved(made, (x, y, 0), yaw)
    turn_lights(b, m)
    b.here.points["Air"] = m @ air
    sliding_door(b, m, dx, 4.6, 0.12, 1.1, 2.1, 1.2, tag + "_door")
    b.here.lights.append([m @ Vector((dx, 5.1, 2.4)), 30.0, (1.0, 0.85, 0.6), None])
    # --- solid
    n = 16
    gap = math.asin(min(1.0, 1.75 / 2.75))
    for k in range(n):
        a = 2 * math.pi * (k + 0.5) / n
        if abs(math.atan2(math.cos(a), math.sin(a))) < gap:
            continue
        b.hit_local(m, (math.cos(a) * 2.75, math.sin(a) * 2.75, 1.75), (0.6, 2 * math.pi * 2.75 / n + 0.1, 3.5), yaw=math.degrees(a))
    for s in (-1, 1):
        b.hit_local(m, (s * 1.72, 3.4, 1.4), (0.16, 2.4, 2.8))
    front_walls(b, m, dx, 0.6, 3.6, 4.52, 0.16, 0.0, 2.8)
    b.hit_local(m, (0, 3.4, 2.7), (3.6, 2.4, 0.2))
    b.hit_local(m, (-0.5, 0.3, 1.8), (1.6, 1.6, 3.4))
    hi = b.hit_drum((0, 0, 3.3), 2.6, height - 6.9, radius_top=2.5, sides=12)
    hi.data.transform(m)
    for args in (((0, 0, height - 5.2), 2.6, 3.0, 5.0), ((0, 0, height - 2.2), 5.0, 1.2, 1.2)):
        o = b.hit_drum(args[0], args[1], args[2], radius_top=args[3], sides=12)
        o.data.transform(m)
    b.socket("Door", m @ Vector((dx, 5.8, 0)))
    b.here.points["Mid"] = Vector((x, y, 0))


# ------------------------------------------------------------------ the five bases

def relay_outpost():
    """Small. A lonely listening post: a radio pod, a relay mast, a ground-station dish, two solar arrays."""
    b = Base("BaseRelay", "small", ORANGE)
    m = hab(b, (-3.6, 4.8), 200, fit="radio")
    b.socket("Door", m @ Vector((0, 4.6, 0)))
    mast(b, (6.2, 4.4), 13.0)
    ground_dish(b, (-4.9, -4.3), 210, 4.2, 40)
    solar(b, (6.4, -3.0), 150)
    solar(b, (3.0, -7.0), 165)
    crates(b, (-8.2, 0.2), 3, 0.7)
    barrels(b, (0.0, -8.2), 3)
    lamp_post(b, (-7.4, 4.6), aim=-100)
    lamp_post(b, (2.4, 8.4), 5.0, aim=185)
    b.frame(pipe("cable", [(5.2, 4.2, 0.05), (2.0, 5.6, 0.04), (-0.4, 4.8, 0.05)], 0.04, DARK, sides=5))
    for p in ((0, 0), (2.5, 1.5), (-2.5, -1.0), (0.5, -3.5)):
        b.socket("Spawn", (p[0], p[1], 0))
    return b


def fuel_depot():
    """Small. Three round tanks on legs, a pump house you can walk into and a lookout tower."""
    b = Base("BaseDepot", "small", YELLOW)
    spots = [(-5.6, 4.6), (0.0, 7.0), (5.6, 4.6)]
    for i, p in enumerate(spots):
        ball_tank(b, p, 1.9, tag="ball%d" % i)
    b.frame(pipe("manifold", [(-5.6, 4.6, 0.5), (-3.4, 3.2, 0.35), (0.0, 4.2, 0.35), (3.4, 3.2, 0.35), (5.6, 4.6, 0.5)], 0.11, (0.55, 0.47, 0.25), sides=8))
    b.frame(pipe("feed", [(0.0, 7.0, 0.5), (0.0, 4.2, 0.35)], 0.11, (0.55, 0.47, 0.25), sides=8))
    b.frame(pipe("run", [(3.4, 3.2, 0.35), (4.6, -0.5, 0.35), (4.6, -2.4, 0.6)], 0.09, GALV, sides=8))
    m = shed(b, (4.1, -4.65), 270, w=4.6, d=3.6, h=2.9, tag="pump", fit="pumps", roller=(0.0, 0.0), door_x=-0.5, slide=1.15)
    b.socket("Door", m @ Vector((-0.5, 3.0, 0)))
    watchtower(b, (-5.0, -3.8), 40, 6.0)
    barrels(b, (-1.0, -8.0), 4)
    crates(b, (1.0, -7.6), 2, 0.6)
    lamp_post(b, (-8.4, 1.6), aim=-95)
    for p in ((0, 0), (-2.0, 1.0), (2.2, -1.5), (-1.0, -3.5)):
        b.socket("Spawn", (p[0], p[1], 0))
    return b


def dome_colony():
    """Medium. Three pressure domes joined by sealed crawlways (one a lit greenhouse) round a landing circle.
    The big dome is the mess and the west dome sleeps the crew: both can be walked into."""
    b = Base("BaseDomes", "medium", TEAL)
    door = dome(b, (0.0, 10.4), 5.6, porch_yaw=180, tag="main", fit="mess")
    b.socket("Door", door)
    dome(b, (-11.4, 0.6), 4.4, porch_yaw=250, tag="west", fit="bunks")
    dome(b, (11.4, 0.6), 4.4, glass=True, tag="green")
    # what grows inside the greenhouse: a few dark shapes seen through the glass
    b.cur = b.structs[-1]
    for k in range(5):
        a = 2 * math.pi * k / 5
        o = nat.lobed("crop", (11.4 + math.cos(a) * 2.0, 0.6 + math.sin(a) * 2.0, 1.5), [((0, 0, 0), (0.9, 0.9, 1.1))], (0.10, 0.30, 0.16), n=3, lump=0.2, seed=k)
        nat.leaf_paint(o, (0.05, 0.18, 0.1), (0.10, 0.30, 0.16), (0.2, 0.42, 0.2), seed=k)
        b.here.groups["Detail"].append(o)
    b.cur = None
    tunnel(b, (-3.0, 6.4), (-8.2, 3.6), tag="tw")
    tunnel(b, (3.8, 7.0), (8.6, 3.6), tag="te")
    pad(b, (0.0, -3.0), 4.2)
    tank(b, (-6.4, -12.6), 1.4, 4.6, tag="tank0")
    tank(b, (-3.0, -14.6), 1.4, 5.4, tag="tank1")
    b.frame(pipe("tank_pipe", [(-6.4, -11.2, 0.3), (-8.6, -6.0, 0.3), (-10.4, -3.4, 0.5)], 0.1, GALV, sides=8))
    solar(b, (5.0, -13.6), 170, tag="s0")
    solar(b, (9.2, -11.4), 160, tag="s1")
    ground_dish(b, (13.0, -6.6), 140, 3.6, 46, tag="dish")
    mast(b, (12.4, 9.6), 11.0)
    crates(b, (-12.4, -7.4), 5, 1.3)
    crates(b, (6.6, -6.2), 3, 0.9, loot=False)
    barrels(b, (-9.6, 10.6), 3)
    lamp_post(b, (-7.0, -5.0), aim=-60)
    lamp_post(b, (7.4, -1.0), aim=110)
    for p in ((-2.8, 2.0), (-5.6, -4.0), (5.0, 1.0), (-2.0, -8.6), (3.0, -8.8), (5.6, -3.0)):
        b.socket("Spawn", (p[0], p[1], 0))
    return b


def mining_works():
    """Medium. A winding tower over a shaft, an ore belt climbing to a bin, a workshop you can walk into and
    heaps of spoil."""
    b = Base("BaseMine", "medium", RUST)
    headframe(b, (-9.0, 6.4), 270)
    conveyor(b, (-4.6, 1.2), (7.8, 8.4), 7.4)
    hopper(b, (9.6, 9.0), 7.0)
    spoil(b, (12.0, -0.6), 3.6, 2.6, 1)
    spoil(b, (9.4, 3.0), 2.4, 1.9, 2)
    spoil(b, (-12.4, -3.2), 2.5, 2.0, 3)
    m = shed(b, (-6.6, -9.4), 20, w=7.4, d=5.0, h=3.3, fit="workshop", roller=(-1.2, 0.43), door_x=2.2, slide=1.1)
    b.socket("Door", m @ Vector((2.2, 3.6, 0)))
    cart(b, (0.6, -11.6), 12)
    track(b, (0.6, -11.6), 12)
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
    """Large. A walled camp: gate, four lookout towers, a command tower, two bunk pods, a hangar, a landing circle."""
    b = Base("BaseStronghold", "large", (0.26, 0.34, 0.44))   # a plain greyed blue: nothing here says whose it is
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
    hab(b, (12.8, 11.4), 250, 7.4, 1.8, tag="bunk0", fit="quarters")
    hab(b, (16.0, 1.6), 265, 7.4, 1.8, tag="bunk1", fit="quarters")
    m = shed(b, (-12.4, -11.6), 35, w=9.6, d=6.4, h=4.2, tag="hangar", fit="hangar", hangar=True, roller=(0.0, 0.55))
    b.socket("Loot", m @ Vector((0, 4.6, 0)))
    tank(b, (10.2, -15.4), 1.7, 6.0, tag="tank0")
    tank(b, (14.4, -12.2), 1.7, 5.2, tag="tank1")
    ball_tank(b, (17.2, -5.6), 1.9, tag="ball0")
    mast(b, (-19.2, -2.2), 12.0)
    ground_dish(b, (-18.6, 8.0), 240, 4.4, 34, tag="dish")
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
BASE_NAMES = {"relay_outpost": "BaseRelay", "fuel_depot": "BaseDepot", "dome_colony": "BaseDomes", "mining_works": "BaseMine", "stronghold": "BaseStronghold"}


# ------------------------------------------------------------------ finishing: weather, breaking, light, writing

def weather(b):
    """Dust thick at the foot of everything outside, the way real kit standing in the open gets."""
    for s in b.structs + [b.ground]:
        outside = s.groups["Shell"] + s.groups["Frame"] + s.groups["Detail"]
        if outside:
            real.dust(outside, DUST, 2.2, amount=0.45, on_top=0.12, seed=len(outside))


def copy_of(o, name=None):
    c = o.copy()
    c.data = o.data.copy()
    c.name = name or (o.name + "_copy")
    bpy.context.scene.collection.objects.link(c)
    return c


def dimmed(o):
    """A lamp or a window as it looks once it has fallen and gone dark."""
    layer = o.data.color_attributes.get("Col")
    if layer is not None:
        for d in layer.data:
            c = d.color
            d.color = (c[0] * 0.18 + 0.02, c[1] * 0.18 + 0.02, c[2] * 0.18 + 0.02, 1.0)
    return o


def break_up(b, s, index):
    """Cuts a copy of the structure into its falling pieces and its ruin."""
    drawn = s.groups["Shell"] + s.groups["Frame"] + s.groups["Detail"]
    lit = s.groups["WindowGlow"] + s.groups["LampGlow"]
    sources = [copy_of(o) for o in drawn] + [dimmed(copy_of(o)) for o in lit] + [copy_of(o) for o in s.inside]
    # a hollow wall is two open skins (outside and room); cut as one closed wall, or its cuts cannot be closed
    pairs = {}
    for o in sources:
        if o.get("oa_pair"):
            pairs.setdefault(o["oa_pair"], []).append(o)
    for name, objs in pairs.items():
        if len(objs) < 2:
            continue
        gone = objs[1:]
        sources = [o for o in sources if all(o is not g for g in gone)]
        whole = _join(objs, name + "_whole")
        bm = bmesh.new()
        bm.from_mesh(whole.data)
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-4)
        bm.to_mesh(whole.data)
        bm.free()
    if s.door is not None:
        sources.append(copy_of(s.door))
    # crates and barrels come apart as whole crates and barrels, the way a stack is knocked over
    small = 2.0 if s.kind in ("crates", "barrels") else 0.9
    pieces, ruin = frac.fracture(sources, s.cells, s.ruin, RAW, seed=index * 17 + 3, small=small, name="S%02dBit" % index)
    for o in sources:
        bpy.data.objects.remove(o)
    # the heap of what fell, left once the pieces are gone (the game shows the ruin the next time it builds the base)
    mid = s.points.get("Mid", Vector())
    xs = [v.co.x for o in drawn for v in o.data.vertices]
    ys = [v.co.y for o in drawn for v in o.data.vertices]
    reach = max(0.6, min(4.5, 0.38 * max(max(xs) - min(xs), max(ys) - min(ys)))) if xs else 1.0
    base_colour = Vector(drawn[0]["oa_colour"][:3]) if drawn else Vector(CONCRETE)
    heap = frac.rubble("S%02dRubble" % index, (mid.x, mid.y), reach, min(1.2, 0.25 + reach * 0.28),
                       (tuple(base_colour * 0.55), tuple(base_colour * 0.85)), seed=index,
                       lumps=2 if reach < 1.2 else 3 if reach < 2.5 else 5)
    ruin_parts = [o for o in (ruin, heap) if o is not None]
    ruin_hits = [frac.clip_below(h, max(s.ruin, 0.3)) for h in s.hits]
    ruin_hits = [h for h in ruin_hits if h is not None]
    for h in ruin_hits:
        nat.flat_paint(h, (0.5, 0.5, 0.5))
    return pieces, ruin_parts, ruin_hits


def bake_inside(b, samples=96):
    """Lights the rooms: Cycles works out how much light reaches every corner from the room's own lamps (and a
    little daylight through the door), and the result is multiplied into the paint. The game draws these
    pieces without sunlight, so a room is the same at noon and at midnight, lit by its own lamps."""
    rooms = [o for s in b.structs for o in s.inside]
    if not rooms:
        return
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = samples
    scene.cycles.device = 'CPU'
    scene.cycles.use_denoising = False
    scene.render.bake.target = 'VERTEX_COLORS'
    world = bpy.data.worlds.new("bake_world")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.62, 0.75, 1.0)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.35
    scene.world = world
    paint = bpy.data.materials.new("bake_paint")
    paint.use_nodes = True
    attr = paint.node_tree.nodes.new("ShaderNodeVertexColor")
    attr.layer_name = "Col"
    paint.node_tree.links.new(attr.outputs["Color"], paint.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    paint.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.8
    glow = bpy.data.materials.new("bake_glow")
    glow.use_nodes = True
    g = glow.node_tree.nodes.new("ShaderNodeVertexColor")
    g.layer_name = "Col"
    emit = glow.node_tree.nodes.new("ShaderNodeEmission")
    emit.inputs[1].default_value = 6.0
    glow.node_tree.links.new(g.outputs["Color"], emit.inputs[0])
    glow.node_tree.links.new(emit.outputs[0], glow.node_tree.nodes["Material Output"].inputs["Surface"])
    everything = []
    for s in b.structs + [b.ground]:
        for k, objs in s.groups.items():
            for o in objs:
                o.data.materials.clear()
                o.data.materials.append(glow if k in ("WindowGlow", "LampGlow") else paint)
                everything.append(o)
        for o in s.inside:
            o.data.materials.clear()
            o.data.materials.append(paint)
        if s.door is not None:
            s.door.data.materials.clear()
            s.door.data.materials.append(paint)
    lamps = []
    for s in b.structs:
        for L in s.lights:
            pos, watts, colour, along = L[:4]
            if along is not None and along.length > 0.5:
                data = bpy.data.lights.new("lamp", 'AREA')
                data.shape = 'RECTANGLE'
                data.size = along.length
                data.size_y = 0.15
                ob = bpy.data.objects.new("lamp", data)
                ob.location = pos
                ob.rotation_euler = (0, 0, math.atan2(along.y, along.x))
            else:
                data = bpy.data.lights.new("lamp", 'POINT')
                data.shadow_soft_size = 0.25
                ob = bpy.data.objects.new("lamp", data)
                ob.location = pos
            data.energy = watts
            data.color = colour
            scene.collection.objects.link(ob)
            lamps.append(ob)
    taking_part = set(everything) | set(rooms) | set(lamps) | {s.door for s in b.structs if s.door is not None}
    hidden = []
    for o in scene.objects:
        o.select_set(False)
        if o not in taking_part and o.type == 'MESH' and not o.hide_render:
            o.hide_render = True
            hidden.append(o)
    # enough corners for the light to change across a floor or a wall (a dome's big round room needs fewer)
    spacing = {id(o): ROOM_SPACING.get(st.kind, 0.45) for st in b.structs for o in st.inside}
    for o in rooms:
        oa.densify(o, spacing.get(id(o), 0.45))
        layer = o.data.color_attributes.get("Light") or o.data.color_attributes.new("Light", 'FLOAT_COLOR', 'POINT')
        o.data.color_attributes.active_color = layer
        o.select_set(True)
    bpy.context.view_layer.objects.active = rooms[0]
    bpy.ops.object.bake(type='DIFFUSE', pass_filter={'DIRECT', 'INDIRECT'}, target='VERTEX_COLORS')
    # light x paint, rolled off gently at the top so lamps never burn the walls white
    for o in rooms:
        col = o.data.color_attributes.get("Col")
        light = o.data.color_attributes.get("Light")
        for i in range(len(col.data)):
            c = col.data[i].color
            L = light.data[i].color
            out = []
            for k in range(3):
                v = c[k] * (L[k] * EXPOSURE + 0.03)
                out.append(1.15 * v / (1.0 + v))
            col.data[i].color = (out[0], out[1], out[2], 1.0)
        o.data.color_attributes.remove(light)
        o.data.color_attributes.active_color = col
    for ob in lamps:
        bpy.data.objects.remove(ob)
    for o in hidden:
        o.hide_render = False
    for o in everything + rooms:
        o.data.materials.clear()


def finish(b, write):
    """Checks a base against its caps and its floor, breaks every structure, bakes the rooms, then writes it."""
    weather(b)
    parts, sockets = [], list(b.sockets)
    problems = []
    outside_tris = inside_tris = 0
    reach = 0.0
    low = 0.0
    farthest = ""

    def check(objs, who="ground"):
        nonlocal reach, low, farthest
        for o in objs:
            for v in o.data.vertices:
                r = math.hypot(v.co.x, v.co.y)
                if r > reach:
                    reach, farthest = r, "%s (%s)" % (who, o.name)
                low = min(low, v.co.z)

    # Break first, from the painted shapes before their rooms are lit.
    broken = {}
    for i, s in enumerate(b.structs):
        if s.breakable:
            broken[i] = break_up(b, s, i)
    bake_inside(b)

    g = b.ground
    g_drawn = g.groups["Shell"] + g.groups["Frame"] + g.groups["Detail"]
    g_lit = g.groups["WindowGlow"] + g.groups["LampGlow"]
    if g_drawn:
        parts.append(("Ground", g_drawn))
    if g_lit:
        parts.append(("GroundGlow", g_lit))
    if g.hits:
        parts.append(("GroundHit", g.hits))
    outside_tris += nat.triangles(g_drawn + g_lit)
    check(g_drawn + g_lit)
    for i, s in enumerate(b.structs):
        tag = "S%02d" % i
        drawn = s.groups["Shell"] + s.groups["Frame"] + s.groups["Detail"]
        lit = s.groups["WindowGlow"] + s.groups["LampGlow"]
        check(drawn + lit + s.inside, "S%02d %s" % (i, s.kind))
        outside_tris += nat.triangles(drawn + lit)
        parts.append((tag, drawn))
        if lit:
            parts.append((tag + "Glow", lit))
        parts.append((tag + "Hit", s.hits))
        if s.inside:
            parts.append((tag + "In", s.inside))
            inside_tris += nat.triangles(s.inside)
        if s.door is not None:
            parts.append((tag + "Door", [s.door]))
            inside_tris += nat.triangles([s.door])
        if i in broken:
            pieces, ruin, ruin_hits = broken[i]
            whole = nat.triangles(drawn + lit + s.inside + ([s.door] if s.door else []))
            bits = nat.triangles(pieces)
            left = nat.triangles(ruin)
            if bits > whole * 2.4 + 400:
                problems.append("%s %s breaks into %d triangles, from %d whole" % (tag, s.kind, bits, whole))
            if left > max(1200, whole * 0.8):
                problems.append("%s %s leaves a ruin of %d triangles, from %d whole" % (tag, s.kind, left, whole))
            check(pieces + ruin, "S%02d %s broken" % (i, s.kind))
            for k, p in enumerate(pieces):
                parts.append(("%sBit%d" % (tag, k), [p]))
            if ruin:
                parts.append((tag + "Ruin", ruin))
            if ruin_hits:
                parts.append((tag + "RuinHit", ruin_hits))
        mid = s.points.get("Mid", Vector())
        sockets.append(oa.socket(tag + "Mid", (mid.x, mid.y, 0.0)))
        for name, p in s.points.items():
            if name != "Mid":
                sockets.append(oa.socket(tag + name, tuple(p)))
    solid = nat.triangles(b.hits)
    print("OA %s (%s): %d structures, %d triangles outside, %d inside, %d solid; reaches %.1f m of %.0f; %d pieces in the file"
          % (b.name, b.size, len(b.structs), outside_tris, inside_tris, solid, reach, RADIUS[b.size], len(parts)))
    if outside_tris > CAPS[b.size]:
        problems.append("%d triangles is over the %s cap of %d" % (outside_tris, b.size, CAPS[b.size]))
    if inside_tris > INSIDE_CAPS[b.size]:
        problems.append("%d triangles inside is over the %s cap of %d" % (inside_tris, b.size, INSIDE_CAPS[b.size]))
    if solid > HIT_CAP[b.size]:
        problems.append("%d solid triangles is over the cap" % solid)
    if reach > RADIUS[b.size] - 0.3:
        problems.append("it reaches %.1f m, off the edge of its %.0f m floor: %s" % (reach, RADIUS[b.size], farthest))
    if low < -0.35:
        problems.append("something hangs %.2f m below the floor" % -low)
    for p in problems:
        print("OA PROBLEM %s: %s" % (b.name, p))
    if write and not problems:
        oa.export(os.path.join(OUT, b.name + ".bytes"), parts, sockets)
    b.broken = broken
    return problems


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    preview = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else None
    rebuild = argv[argv.index("--rebuild") + 1].split(",") if "--rebuild" in argv else None
    failed = False
    for make in BASES:
        oa.reset_scene()
        if (only or rebuild) and make.__name__ not in [n for n in BASE_NAMES if BASE_NAMES[n] in (only or rebuild)]:
            continue
        b = make()
        failed = bool(finish(b, write=not only)) or failed
        if preview:
            import preview_bases
            preview_bases.render(preview, b, RADIUS[b.size])
    if failed:
        raise SystemExit("Some bases were not written: see the lines marked PROBLEM.")


if __name__ == "__main__":
    main()
