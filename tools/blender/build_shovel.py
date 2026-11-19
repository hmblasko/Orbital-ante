"""Builds the suit's shovel (the digging tool of Scripts/Runtime/Player/PlayerShovel.cs).

    blender -b -P tools/blender/build_shovel.py                          (writes Resources/Models/Shovel.bytes)
    blender -b -P tools/blender/build_shovel.py -- --preview <folder>    (also renders pictures)

The real thing it starts from: a short D-handled round-point digging shovel, the size of a military entrenching
tool with a fixed blade (about 0.84 m): a pressed steel blade with turned steps and a raised frog running up into
a riveted socket, an ash shaft, and a steel yoke holding a wooden cross grip.

Its space here: origin in the middle of the cross grip (where the hand holds it), Y along the shaft toward the
blade, Z out of the hollow face of the blade, metres. In the game that is +Z along the shaft and +Y out of the
hollow face, the same as the primitive stand-in it replaces, so the swing in PlayerShovel.Pose fits both.
One piece, "shovel"; sockets "Grip" and "Tip".
"""
import bpy, bmesh, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import oa_blender as oa
import oa_real as real
from mathutils import Matrix, Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "unity", "Assets", "OrbitalAnte", "Resources", "Models"))

ENAMEL = (0.20, 0.25, 0.22)      # the blade's stoved paint: a dull grey green
BARE = (0.62, 0.62, 0.60)        # steel the ground has scoured clean
STEEL = (0.36, 0.37, 0.39)       # the yoke and rivets: blued, unpainted
ASH = (0.60, 0.46, 0.29)         # the shaft and the grip
SOIL = (0.34, 0.27, 0.20)        # what stays in the corners
BAND = (0.80, 0.36, 0.14)        # a tool-kit colour band, so the shaft is found in grey dust

LENGTH = 0.84                    # grip to tip
BLADE_FROM = 0.575               # where the blade's shoulders are, along the shaft
BLADE_LONG = 0.27
BLADE_HALF = 0.098               # half its width
LIFT = 11.0                      # degrees the blade is lifted off the shaft's line
DISH = 0.017                     # how deep the blade is hollowed
THICK = 0.0036                   # sheet thickness (a little over the real 2 mm, so its edge reads at game size)


def half_width(s):
    """Half the blade's width at s along it: square shoulders, straight sides, then a round point."""
    shoulder = 0.90 + 0.10 * min(1.0, s / 0.035) ** 0.6
    turn_at = 0.42 * BLADE_LONG
    if s <= turn_at:
        return BLADE_HALF * shoulder
    t = (s - turn_at) / (BLADE_LONG - turn_at)
    return max(0.007, BLADE_HALF * math.sqrt(max(0.0, 1.0 - t * t)) ** 0.85)


def dish(s):
    """How deep the hollow is at s: deepest behind the middle, flattening out to the point."""
    t = s / BLADE_LONG
    return DISH * (0.75 + 0.25 * math.sin(math.pi * min(1.0, t * 1.6))) * (1.0 - t) ** 0.55


def blade_top(s, u):
    """A point of the hollow face: s along, u from -1 (left edge) to 1."""
    hw = half_width(s)
    return Vector((u * hw, s, -dish(s) * (1.0 - u * u)))


def blade():
    """The pressed sheet: a hollow face and a back face, joined round the rim."""
    along, across = 34, 22
    bm = bmesh.new()
    top, back = [], []
    for i in range(along + 1):
        # More rows toward the point, where the outline turns.
        s = BLADE_LONG * (0.45 * (i / along) + 0.55 * math.sin(0.5 * math.pi * i / along))
        row_t, row_b = [], []
        for j in range(across + 1):
            u = -1.0 + 2.0 * j / across
            p = blade_top(s, u)
            # The way out of the sheet here, from its neighbours.
            a = blade_top(min(BLADE_LONG, s + 0.002), u) - blade_top(max(0.0, s - 0.002), u)
            b = blade_top(s, min(1.0, u + 0.02)) - blade_top(s, max(-1.0, u - 0.02))
            n = b.cross(a)
            n = n.normalized() if n.length > 1e-9 else Vector((0, 0, 1))
            row_t.append(bm.verts.new(p))
            row_b.append(bm.verts.new(p - n * THICK))
        top.append(row_t)
        back.append(row_b)
    for i in range(along):
        for j in range(across):
            bm.faces.new((top[i][j], top[i][j + 1], top[i + 1][j + 1], top[i + 1][j]))
            bm.faces.new((back[i][j], back[i + 1][j], back[i + 1][j + 1], back[i][j + 1]))
    for i in range(along):                                  # the two long edges
        bm.faces.new((top[i][0], top[i + 1][0], back[i + 1][0], back[i][0]))
        bm.faces.new((top[i][across], back[i][across], back[i + 1][across], top[i + 1][across]))
    for j in range(across):                                 # the shoulders and the point
        bm.faces.new((top[0][j], back[0][j], back[0][j + 1], top[0][j + 1]))
        bm.faces.new((top[along][j], top[along][j + 1], back[along][j + 1], back[along][j]))
    return oa._new_object("blade", bm, ENAMEL, 50.0)


def to_shaft(obj):
    """Blade space (s along the blade from its shoulders) into the tool's own: lifted, and set on the shaft's end."""
    m = Matrix.Translation(Vector((0, BLADE_FROM, -0.016))) @ Matrix.Rotation(math.radians(LIFT), 4, 'X')
    obj.data.transform(m)
    obj.data.update()
    return obj


def blade_point(s, u=0.0, up=0.0):
    """Where a place on the hollow face ends up on the finished tool; `up` lifts it off the face."""
    m = Matrix.Translation(Vector((0, BLADE_FROM, -0.016))) @ Matrix.Rotation(math.radians(LIFT), 4, 'X')
    return m @ (blade_top(s, u) + Vector((0, 0, up)))


def frog():
    """The socket: a tube round the shaft's end that flattens and runs out along the blade's middle as a rib."""
    rings = []
    places = [(-0.135, 0.0205, 0.0205), (-0.10, 0.0210, 0.0210), (-0.04, 0.0215, 0.0215), (-0.01, 0.0225, 0.0200)]
    for y, rx, rz in places:                               # round the shaft
        rings.append(real.ring(Vector((0, BLADE_FROM + y, 0)), Vector((1, 0, 0)), Vector((0, 0, 1)), rx, rz, 18))
    for s, rx, rz in ((0.025, 0.0240, 0.0150), (0.06, 0.0235, 0.0105), (0.10, 0.0200, 0.0070), (0.14, 0.0150, 0.0042),
                      (0.175, 0.0090, 0.0022), (0.200, 0.0040, 0.0010)):            # out along the blade, pressed flatter and narrower
        centre = blade_point(s, 0.0, rz * 0.55 - THICK * 0.5)
        tilt = Matrix.Rotation(math.radians(LIFT), 3, 'X')
        rings.append(real.ring(centre, Vector((1, 0, 0)), tilt @ Vector((0, 0, 1)), rx, rz, 18))
    return real.loft("frog", rings, ENAMEL, smooth=70.0)


def steps():
    """The turned steps: the top edge of each shoulder rolled forward, somewhere to put a boot."""
    made = []
    for side in (-1, 1):
        path = [blade_point(0.0065, side * u, 0.0030) for u in (0.33, 0.48, 0.64, 0.80, 0.93, 1.0)]
        made.append(real.sweep("step", path, 0.0085, ENAMEL, n=8, up=(0, 0, 1), flat=0.42, power=4.0))
    return made


def shaft():
    keys = [(0.116, 0.0100, 0.0100), (0.120, 0.0150, 0.0150), (0.128, 0.0170, 0.0170), (0.30, 0.0178, 0.0178), (0.44, 0.0182, 0.0182), (0.48, 0.0178, 0.0178)]
    return real.limb("shaft", (0, 0, 0), (0, 1, 0), (0, 0, 1), keys, ASH, n=22, step=0.012, fine=((0.12, 0.012),), fine_step=0.004)


def grip():
    """The cross grip (wood, turned, a little fatter in the middle) and the steel yoke that holds it."""
    made = []
    wood = oa.lathe("grip", (-0.062, 0, 0), (1, 0, 0),
                    [(0.0, 0.0125), (0.004, 0.0150), (0.030, 0.0162), (0.062, 0.0170), (0.094, 0.0162), (0.120, 0.0150), (0.124, 0.0125)], ASH, segments=20)
    oa.densify(wood, 0.012)
    made.append(("wood", wood))
    for side in (-1, 1):
        x = side * 0.0665
        path = [(x, -0.012, 0), (x, 0.006, 0), (x * 0.98, 0.030, 0), (x * 0.80, 0.062, 0), (x * 0.46, 0.092, 0), (side * 0.0205, 0.115, 0),
                (side * 0.0200, 0.150, 0), (side * 0.0200, 0.176, 0)]
        made.append(("steel", real.sweep("yoke", path, 0.0135, STEEL, n=8, up=(side, 0, 0), flat=0.20, cuts=2, power=4.0)))
        # The grip's pin, peened over outside the yoke.
        made.append(("steel", oa.cylinder("pin", (x - side * 0.001, 0, 0), (x + side * 0.0055, 0, 0), 0.0075, STEEL, radius_end=0.0055, segments=10)))
        # Rivets through yoke and shaft.
        for y in (0.154, 0.169):
            made.append(("steel", oa.sphere("rivet", (side * 0.0222, y, 0), 0.0048, STEEL, scale=(0.55, 1, 1), segments=8)))
    # A ferrule closing the yoke round the shaft's top.
    made.append(("steel", oa.cylinder("ferrule", (0, 0.124, 0), (0, 0.142, 0), 0.0272, STEEL, segments=18, bevel=0.002)))
    return made


def socket_rivets():
    made = []
    for y in (BLADE_FROM - 0.105, BLADE_FROM - 0.055):
        for z in (-1, 1):
            made.append(oa.sphere("rivet", (0, y, z * 0.0208), 0.0052, STEEL, scale=(1, 1, 0.5), segments=8))
    return made


# ------------------------------------------------------------------ paint

def _colours(obj):
    return obj.data.color_attributes.get("Col")


def scour(obj, finish):
    """A blade that has dug: the paint is gone from the point and the edges, bright steel shows there, soil stays
    in the hollow's corners and under the steps. finish is the "Mat" layer, set per corner to match."""
    col, mat = _colours(obj), finish
    for i, v in enumerate(obj.data.vertices):
        p = v.co
        along = (p.y - BLADE_FROM) / (BLADE_LONG * math.cos(math.radians(LIFT)))
        edge = abs(p.x) / max(0.004, half_width(max(0.0, min(BLADE_LONG, along * BLADE_LONG))))
        n = noise.noise(Vector((p.x * 13, p.y * 6, p.z * 13)))
        streak = noise.noise(Vector((p.x * 42, p.y * 3, 0.0)))
        bare = max(0.0, min(1.0, (along - 0.36 + 0.22 * n + 0.12 * streak) * 2.3))
        bare = max(bare, max(0.0, min(1.0, (edge - 0.80 + 0.10 * n) * 6.0)) * max(0.0, min(1.0, along * 6.0)))
        c = Vector(col.data[i].color[:3])
        steel = Vector([oa._srgb_to_linear(k) for k in BARE]) * (0.92 + 0.14 * streak)
        c = c.lerp(steel, bare)
        dirt = max(0.0, min(1.0, (0.16 - along) * 5.0 + 0.5 * n)) * max(0.0, 1.0 - bare) * 0.55
        c = c.lerp(Vector([oa._srgb_to_linear(k) for k in SOIL]), dirt)
        col.data[i].color = (c.x, c.y, c.z, 1.0)
        mat.data[i].color = (0.22 + 0.36 * bare - 0.14 * dirt, bare, 0.0, 1.0)


def grain(obj, axis, seed=0.0, strength=0.34):
    """Wood: long streaks running with the grain, a darker hand-worn patch, never an even colour."""
    col = _colours(obj)
    ax = Vector(axis).normalized()
    for i, v in enumerate(obj.data.vertices):
        p = v.co
        a = p.dot(ax)
        across = p - ax * a
        n = noise.noise(Vector((across.x * 55 + seed, across.y * 55, across.z * 55)) + ax * (a * 1.6))
        fine = noise.noise(Vector((across.x * 150, across.y * 150 + seed, across.z * 150)) + ax * (a * 4.0))
        k = 1.0 + strength * n + 0.6 * strength * fine
        c = Vector(col.data[i].color[:3])
        # The dark of the grain is browner, not only darker.
        c = Vector((c.x * k, c.y * k ** 1.15, c.z * k ** 1.5))
        col.data[i].color = (max(0.0, c.x), max(0.0, c.y), max(0.0, c.z), 1.0)


def handled(obj, where, reach):
    """Where the forward hand holds the shaft the wood is darker and smoother from the glove."""
    col = _colours(obj)
    for i, v in enumerate(obj.data.vertices):
        k = max(0.0, 1.0 - abs(v.co.y - where) / reach)
        k = k * k * (3 - 2 * k) * (0.75 + 0.5 * noise.noise(v.co * 40.0))
        c = Vector(col.data[i].color[:3]) * (1.0 - 0.30 * max(0.0, k))
        col.data[i].color = (c.x, c.y, c.z, 1.0)


def band(obj, start, end, colour):
    col = _colours(obj)
    lin = Vector([oa._srgb_to_linear(k) for k in colour])
    for i, v in enumerate(obj.data.vertices):
        if start <= v.co.y <= end:
            n = noise.noise(v.co * 70.0)
            worn = max(0.0, min(1.0, 0.55 + 1.6 * n))          # rubbed through in places
            c = Vector(col.data[i].color[:3]).lerp(lin, 0.35 + 0.6 * worn)
            col.data[i].color = (c.x, c.y, c.z, 1.0)


def model():
    b = to_shaft(blade())
    oa.paint(b, wear=0.5, dirt=0.4, mottle=0.5, seed=3, wear_colour=BARE, grain=30.0)
    scour(b, real.made_of(b, 0.22).data.color_attributes["Mat"])

    f = frog()
    oa.paint(f, wear=0.75, dirt=0.6, mottle=0.6, seed=5, wear_colour=BARE, grain=26.0)
    scour(f, real.made_of(f, 0.22).data.color_attributes["Mat"])

    objects = [b, f]
    for s in steps():
        oa.paint(s, wear=1.0, dirt=0.5, mottle=0.5, seed=7, wear_colour=BARE, grain=30.0)
        real.made_of(s, 0.34, 0.5)
        objects.append(s)

    sh = shaft()
    oa.paint(sh, wear=0.2, dirt=0.5, mottle=0.7, seed=11, gradient=0.04, grain=12.0)
    grain(sh, (0, 1, 0), seed=1.0)
    handled(sh, 0.30, 0.10)
    band(sh, 0.405, 0.440, BAND)
    real.made_of(sh, 0.24)
    objects.append(sh)

    for kind, o in grip():
        if kind == "wood":
            oa.paint(o, wear=0.3, dirt=0.6, mottle=0.9, seed=13, gradient=0.03, grain=14.0)
            grain(o, (1, 0, 0), seed=4.0, strength=0.40)
            real.made_of(o, 0.30)          # polished by the glove
        else:
            oa.paint(o, wear=0.8, dirt=0.6, mottle=0.6, seed=17, wear_colour=BARE, grain=30.0)
            real.made_of(o, 0.40, 0.85)
        objects.append(o)
    for r in socket_rivets():
        oa.paint(r, wear=0.9, dirt=0.4, mottle=0.4, seed=19, wear_colour=BARE)
        real.made_of(r, 0.42, 0.85)
        objects.append(r)

    real.crowd_shadow(objects, strength=0.5, reach=0.03)
    tip = blade_point(BLADE_LONG, 0.0)
    sockets = [oa.socket("Grip", (0, 0, 0)), oa.socket("Tip", tuple(tip))]
    return [("shovel", objects)], sockets


# PlayerShovel.Pose: where the grip is from the eye and how the tool is turned (the game's own numbers, x right,
# y up, z forward, degrees), pulled back and at full thrust. Change both together.
HELD_BACK = ((0.28, -0.42, 0.14), (-8.0, -10.0, -20.0))
HELD_THRUST = ((0.22, -0.40, 0.30), (-25.0, -14.0, -8.0))


def in_view(objects, f):
    """Puts the tool where PlayerShovel.Pose holds it at swing amount f, seen from an eye at the origin looking
    along Blender's +Y (the game's +Z), so the picture is what the player sees."""
    def lerp(a, b):
        return a + (b - a) * f
    px, py, pz = (lerp(a, b) for a, b in zip(HELD_BACK[0], HELD_THRUST[0]))
    ex, ey, ez = (math.radians(lerp(a, b)) for a, b in zip(HELD_BACK[1], HELD_THRUST[1]))
    unity = Matrix.Translation(Vector((px, py, pz))) @ (Matrix.Rotation(ey, 4, 'Y') @ Matrix.Rotation(ex, 4, 'X') @ Matrix.Rotation(ez, 4, 'Z'))
    swap = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
    m = swap @ unity @ swap
    for o in objects:
        o.data.transform(m)
        o.data.update()
    return m


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    previews = args[args.index("--preview") + 1] if "--preview" in args else None
    oa.reset_scene()
    parts, sockets = model()
    everything = [o for _, objs in parts for o in objs]
    print("OA shovel: grip to tip %.3f m, inside out %.2f%%" % (sockets[1].location.length, 100.0 * oa.inside_out_share(everything)))
    oa.export(os.path.join(OUT, "Shovel.bytes"), parts, sockets)
    if previews:
        floor = oa.box("floor", (0, 0.4, -0.16), (40, 40, 0.01), (0.80, 0.74, 0.66), bevel=0)
        oa.paint(floor, wear=0, dirt=0, mottle=0, gradient=0)
        shown = everything + [floor]
        mid = (0, 0.42, 0)
        oa.render_preview(os.path.join(previews, "shovel_top.png"), shown, (0.14, 0.34, 2.15), mid, lens=70, size=(1280, 800))
        oa.render_preview(os.path.join(previews, "shovel_side.png"), shown, (2.05, 0.36, 0.55), mid, lens=70, size=(1280, 800))
        oa.render_preview(os.path.join(previews, "shovel_blade.png"), shown, (0.30, 1.12, 0.36), (0, 0.66, 0.0), lens=60, size=(1280, 800))
        oa.render_preview(os.path.join(previews, "shovel_grip.png"), shown, (0.26, -0.30, 0.30), (0, 0.07, 0.0), lens=60, size=(1280, 800))
        # What the player sees: mid swing and at full thrust, at the game's width of view.
        bpy.data.objects.remove(floor)
        ground = oa.box("ground", (0, 3.0, -1.7), (60, 60, 0.01), (0.55, 0.50, 0.44), bevel=0)
        oa.paint(ground, wear=0, dirt=0, mottle=0, gradient=0)
        last = Matrix.Identity(4)
        for f, name in ((0.0, "shovel_view_back.png"), (1.0, "shovel_view_thrust.png")):
            for o in everything:
                o.data.transform(last.inverted())
            last = in_view(everything, f)
            oa.render_preview(os.path.join(previews, name), everything + [ground], (0, 0, 0), (0, 1.0, -0.12), lens=13.4, size=(1280, 800))
