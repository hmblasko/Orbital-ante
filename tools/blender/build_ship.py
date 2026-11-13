"""Builds the outside fittings of the starter lander: the base, four legs, engines, thrusters and dish.

    blender -b -P tools/blender/build_ship.py
    blender -b -P tools/blender/build_ship.py -- --preview <folder> [--hull <folder of .obj dumps of the cabin>]

The cabin itself (the dome, its windows and everything inside) is made by the game's own code, because it has
to fit the interior exactly. These pieces bolt onto it. Every position here comes from ShipLayout in the game;
the sockets written into the file let a rule test check that the two still agree.

Ship space in the game: origin in the middle of the cabin floor, Y up, Z nose, X the pilot's right.
Here in Blender that is X right, Y nose, Z up, so a game point (x, y, z) is written G(x, y, z).
"""
import bpy, bmesh, sys, os, math, glob
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import oa_blender as oa

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "unity", "Assets", "OrbitalAnte", "Resources", "Models"))

# ---- from ShipLayout (checked by the rule test ShipModelFile_MatchesTheShipLayout)
BASE_INRADIUS, BELLY_Y, BASE_TOP, SHAFT_RADIUS = 2.3, -1.7, -0.05, 0.8
GROUND_Y, FOOTPAD_RADIUS = -3.8, 0.45
ENGINE_EXIT_Y, ENGINE_EXIT_RADIUS, ENGINE_RING = -2.35, 0.4, 1.5
LEG_ROOT_R, LEG_ROOT_Y, LEG_PAD_R = 2.4, -0.55, 4.3
THRUSTER_R, THRUSTER_Y, THRUSTER_FOOT_R, THRUSTER_FOOT_Y = 3.05, 1.15, 2.598, 1.1533
DISH_CENTRE, DISH_MAST_FOOT, DISH_RADIUS = (-1.2, 3.35, -1.3), (-0.95, 2.4, -1.05), 0.62
SIDES = 64

GOLD, GOLD_DARK, GOLD_LIGHT = (0.82, 0.54, 0.13), (0.67, 0.41, 0.10), (0.90, 0.67, 0.26)
BLANKET = (0.25, 0.21, 0.20)
CHARCOAL, DARK_METAL = (0.13, 0.13, 0.15), (0.24, 0.25, 0.28)
SILVER, SILVER_DARK = (0.72, 0.74, 0.76), (0.52, 0.54, 0.58)
CREAM, ORANGE, ENGINE_ORANGE = (0.93, 0.89, 0.79), (0.86, 0.45, 0.16), (0.70, 0.32, 0.11)
BARE = (0.86, 0.86, 0.84)


def G(x, y, z):
    return Vector((x, z, y))


def at_angle(radius, y, degrees):
    """A game point at a compass angle round the ship (0 is the nose, 90 the pilot's right)."""
    a = math.radians(degrees)
    return G(math.sin(a) * radius, y, math.cos(a) * radius)


def foil(o, seed=0, amount=0.016):
    oa.densify(o, 0.34)
    oa.crumple(o, amount, grain=2.6, seed=seed)
    oa.paint(o, wear=0.5, dirt=0.75, mottle=1.25, seed=seed, wear_colour=(0.98, 0.84, 0.48), gradient=0.14, grain=2.2)
    return o


def metal(o, seed=0, wear=0.6):
    oa.paint(o, wear=wear, dirt=0.7, mottle=0.7, seed=seed, wear_colour=BARE, grain=1.6)
    return o


def painted(o, seed=0):
    oa.paint(o, wear=0.45, dirt=0.7, mottle=0.6, seed=seed, wear_colour=(0.62, 0.62, 0.6), grain=1.6)
    return o


def octagon_radius(angle, inradius, roundness=1.058):
    """Distance to an eight-sided outline with softened corners. Flat sides face 0, 45, 90... degrees."""
    quarter = math.pi / 4
    off = angle - round(angle / quarter) * quarter
    return min(inradius / math.cos(off), inradius * roundness)


def ring_skin(name, rings, colour, closed=False):
    """A skin through rings of 64 points. Each ring is ('o', inradius, height) on the eight-sided outline or
    ('c', radius, height) on a circle. List the rings the way a clock hand sweeps a cross-section drawn with
    the ship's middle on the left: out across the top, down the outside, back in underneath. Then it faces out."""
    bm = bmesh.new()
    loops = []
    for kind, value, height in rings:
        loop = []
        for i in range(SIDES):
            a = i * 2 * math.pi / SIDES
            r = octagon_radius(a, value) if kind == 'o' else value
            loop.append(bm.verts.new(G(math.sin(a) * r, height, math.cos(a) * r)))
        loops.append(loop)
    pairs = list(zip(loops, loops[1:])) + ([(loops[-1], loops[0])] if closed else [])
    for a, b in pairs:
        for i in range(SIDES):
            j = (i + 1) % SIDES
            bm.faces.new((a[i], a[j], b[j], b[i]))
    return oa._new_object(name, bm, colour, 40.0, recalc=closed)


def rod(name, a, b, radius, colour, radius_end=None, segments=10, bevel=0.0):
    return oa.cylinder(name, a, b, radius, colour, radius_end=radius_end, segments=segments, bevel=bevel)


# ------------------------------------------------------------------ base

def base():
    parts = []
    top, bottom, edge = BASE_TOP, BELLY_Y, BASE_INRADIUS - 0.10
    # The body: a dark frame with a deck on top and a plate underneath, open in the middle for the lift.
    parts.append(metal(ring_skin("base body", [
        ('c', SHAFT_RADIUS, top), ('o', edge - 0.16, top), ('o', edge - 0.04, top - 0.05), ('o', edge, top - 0.16),
        ('o', edge, bottom + 0.16), ('o', edge - 0.04, bottom + 0.05), ('o', edge - 0.16, bottom), ('c', 1.3, bottom), ('c', SHAFT_RADIUS, bottom)],
        CHARCOAL), 1, wear=0.35))
    # A fat rolled rim top and bottom.
    for k, y in enumerate((top - 0.12, bottom + 0.12)):
        parts.append(metal(ring_skin("base rim", [('o', edge + 0.00, y + 0.07), ('o', edge + 0.07, y + 0.05), ('o', edge + 0.10, y), ('o', edge + 0.07, y - 0.05), ('o', edge + 0.00, y - 0.07)], DARK_METAL, closed=True), 2 + k))
    # A silver landing plate round the lift opening underneath.
    parts.append(metal(ring_skin("belly plate", [('c', 1.3, bottom + 0.01), ('c', 1.28, bottom - 0.012), ('c', SHAFT_RADIUS + 0.02, bottom - 0.012)], SILVER), 4))

    face_width = 2 * BASE_INRADIUS * math.tan(math.pi / 8)
    panel_colours = [GOLD, GOLD_LIGHT, GOLD_DARK, GOLD, GOLD_DARK, GOLD, GOLD_LIGHT, GOLD]
    for face in range(8):
        degrees = face * 45
        made = []
        leg_face = face % 2 == 1
        y_mid = (top + bottom) / 2
        if leg_face:
            # The sides the legs hang from: one dark padded blanket with a gold patch, and upright ribs.
            made.append(foil(oa.box("blanket", G(0, y_mid, edge + 0.035), (face_width * 0.86, 0.13, 1.22), BLANKET, bevel=0.06, segments=2), 10 + face, amount=0.012))
            for seam_y in (-0.2, 0.22):
                made.append(metal(oa.box("seam", G(0, y_mid + seam_y, edge + 0.1), (face_width * 0.84, 0.03, 0.035), CHARCOAL, bevel=0.012), 30 + face, wear=0.2))
            made.append(foil(oa.box("patch", G(-0.48, y_mid - 0.32, edge + 0.085), (0.46, 0.08, 0.4), panel_colours[face], bevel=0.035, segments=2), 20 + face, amount=0.008))
        else:
            # The free sides: quilted gold foil, two panels over two, none quite the same size.
            split = (-0.06, 0.05, -0.03, 0.07)[face // 2]
            rise = (0.04, -0.05, 0.06, -0.02)[face // 2]
            w = face_width * 0.88
            cells = [(-w / 2, split, bottom + 0.2, y_mid + rise), (split, w / 2, bottom + 0.2, y_mid - rise),
                     (-w / 2, split - 0.1, y_mid + rise, top - 0.2), (split - 0.1, w / 2, y_mid - rise, top - 0.2)]
            for k, (x0, x1, y0, y1) in enumerate(cells):
                colour = panel_colours[(face * 3 + k * 5) % 8]
                made.append(foil(oa.box("foil", G((x0 + x1) / 2, (y0 + y1) / 2, edge + 0.04), (x1 - x0 - 0.035, 0.15, y1 - y0 - 0.035), colour, bevel=0.07, segments=2), face * 4 + k))
        # Upright ribs at both ends of every side.
        for sx in (-1, 1):
            made.append(metal(oa.box("rib", G(sx * face_width * 0.475, y_mid, edge + 0.03), (0.09, 0.12, 1.32), DARK_METAL, bevel=0.03), 40 + face))
        for o in made:
            oa.turn(o, degrees)
        parts += made

    # Fittings on the free sides.
    fit = []
    # Nose (0 degrees): a cream equipment locker with an orange stripe, a handle and a lamp.
    fit.append((0, painted(oa.box("locker", G(0.28, -0.98, edge + 0.2), (0.95, 0.3, 0.6), CREAM, bevel=0.07, segments=3), 50)))
    fit.append((0, painted(oa.box("locker stripe", G(0.28, -0.80, edge + 0.215), (0.97, 0.3, 0.1), ORANGE, bevel=0.03), 51)))
    fit.append((0, metal(rod("locker handle", G(0.05, -1.08, edge + 0.38), G(0.51, -1.08, edge + 0.38), 0.03, SILVER, segments=8, bevel=0.01), 52)))
    for sx in (0.05, 0.51):
        fit.append((0, metal(rod("handle post", G(sx, -1.08, edge + 0.3), G(sx, -1.08, edge + 0.38), 0.024, SILVER, segments=8), 53)))
    # Right (90): two pressure tanks strapped on side by side.
    for k, ty in enumerate((-0.62, -1.18)):
        fit.append((90, metal(rod("tank", G(-0.52, ty, edge + 0.2), G(0.52, ty, edge + 0.2), 0.215, SILVER, segments=16, bevel=0.12), 54 + k, wear=0.4)))
        for sx in (-0.3, 0.3):
            fit.append((90, metal(rod("tank strap", G(sx - 0.035, ty, edge + 0.2), G(sx + 0.035, ty, edge + 0.2), 0.232, CHARCOAL, segments=16), 56)))
    # Tail (180): a radiator, dark with pale fins.
    fit.append((180, metal(oa.box("radiator", G(0, -0.9, edge + 0.13), (1.25, 0.12, 0.95), CHARCOAL, bevel=0.04), 58, wear=0.3)))
    for k in range(6):
        fit.append((180, metal(oa.box("fin", G(-0.5 + 0.2 * k, -0.9, edge + 0.2), (0.07, 0.1, 0.82), SILVER_DARK, bevel=0.025), 59 + k)))
    # Left (270): a round service hatch with bolts and a plain lever.
    fit.append((270, metal(rod("hatch", G(-0.1, -0.9, edge + 0.08), G(-0.1, -0.9, edge + 0.19), 0.42, CREAM, segments=24, bevel=0.035), 66, wear=0.4)))
    for k in range(8):
        a = k * math.pi / 4 + 0.39
        fit.append((270, metal(oa.sphere("bolt", G(-0.1 + math.cos(a) * 0.33, -0.9 + math.sin(a) * 0.33, edge + 0.195), 0.035, BARE, segments=8), 67)))
    fit.append((270, painted(oa.box("lever", G(-0.1, -0.9, edge + 0.215), (0.34, 0.05, 0.08), DARK_METAL, bevel=0.02, rot=(0, 25, 0)), 68)))
    for degrees, o in fit:
        parts.append(oa.turn(o, degrees))
    return parts


# ------------------------------------------------------------------ legs

def leg(index):
    """Built for leg 0 (45 degrees) and turned into place."""
    parts = []
    degrees = 45
    root = at_angle(LEG_ROOT_R, LEG_ROOT_Y, degrees)
    pad = at_angle(LEG_PAD_R, GROUND_Y, degrees)
    ankle = pad + Vector((0, 0, 0.2))
    along = ankle - root
    knee = root + along * 0.55
    d = along.normalized()
    a = math.radians(degrees)
    side = G(math.cos(a), 0, -math.sin(a))

    # Where it meets the base: a bracket and a fat joint.
    parts.append(metal(oa.box("leg bracket", at_angle(LEG_ROOT_R - 0.16, LEG_ROOT_Y, degrees), (0.5, 0.5, 0.44), DARK_METAL, bevel=0.09, segments=3, rot=(0, 0, -degrees)), 1))
    parts.append(metal(oa.sphere("leg shoulder", root, 0.25, DARK_METAL, segments=14), 2))
    # Upper strut wrapped in gold foil, with two dark straps.
    parts.append(foil(rod("upper strut", root + d * 0.1, knee - d * 0.1, 0.17, GOLD, segments=14, bevel=0.05), 3, amount=0.012))
    for t in (0.3, 0.72):
        m = root + (knee - root) * t
        parts.append(metal(rod("strap", m - d * 0.035, m + d * 0.035, 0.19, CHARCOAL, segments=14, bevel=0.012), 4))
    parts.append(metal(oa.sphere("knee", knee, 0.215, DARK_METAL, segments=14), 5))
    # Lower strut: a bare piston sliding out of a collar.
    parts.append(metal(rod("collar", knee + d * 0.12, knee + d * 0.5, 0.135, SILVER_DARK, segments=12, bevel=0.03), 6))
    parts.append(metal(rod("piston", knee + d * 0.4, ankle, 0.095, SILVER, segments=12), 7, wear=0.3))
    parts.append(metal(oa.sphere("ankle", ankle, 0.15, SILVER_DARK, segments=12), 8))
    # Two side struts from the knee back to the corners of the base.
    for s in (-1, 1):
        corner = at_angle(BASE_INRADIUS / math.cos(math.pi / 8) - 0.08, BELLY_Y + 0.24, degrees + s * 22.5)
        parts.append(metal(rod("side strut", knee, corner, 0.06, SILVER, segments=8), 9))
        parts.append(metal(oa.sphere("strut end", corner, 0.11, DARK_METAL, segments=10), 10))
    # The footpad: a shallow gold dish with a rolled rim. Its flat underside sits exactly on the ground.
    k = FOOTPAD_RADIUS / 0.45
    profile = [(0.0, 0.22 * k), (0.0, 0.34 * k), (0.03, 0.43 * k), (0.09, 0.45 * k), (0.15, 0.44 * k), (0.175, 0.405 * k), (0.15, 0.36 * k), (0.085, 0.28 * k), (0.06, 0.14 * k)]
    parts.append(foil(oa.lathe("footpad", pad, (0, 0, 1), profile, GOLD, segments=22), 11, amount=0.0))

    if index == 0:
        # The ladder, standing proud of the upper strut.
        off = side.cross(d).normalized()
        if off.z < 0:
            off = -off
        rail, stand, t0, t1 = 0.2, 0.3, 0.06, 0.5
        for s in (-1, 1):
            p0 = root + along * t0 + side * (rail * s) + off * stand
            p1 = root + along * t1 + side * (rail * s) + off * stand
            parts.append(metal(rod("rail", p0, p1, 0.034, SILVER, segments=8, bevel=0.012), 12))
            parts.append(metal(rod("rail bracket", p0 + d * 0.06, root + along * t0 + d * 0.06, 0.024, SILVER_DARK, segments=6), 13))
            parts.append(metal(rod("rail bracket", p1 - d * 0.06, root + along * t1 - d * 0.06, 0.024, SILVER_DARK, segments=6), 13))
        rungs = 6
        for r in range(rungs):
            t = t0 + 0.035 + (t1 - t0 - 0.07) * r / (rungs - 1)
            m = root + along * t + off * stand
            parts.append(metal(rod("rung", m - side * rail, m + side * rail, 0.028, SILVER, segments=8), 14))
    for o in parts:
        oa.turn(o, index * 90)
    return parts


# ------------------------------------------------------------------ engines

def engines():
    parts = []
    depth = BELLY_Y - ENGINE_EXIT_Y
    throat, wall, neck = 0.15, 0.035, 0.1
    outer, inner = [], []
    steps = 6
    for s in range(steps + 1):
        t = s / steps
        r = throat + (ENGINE_EXIT_RADIUS - throat) * math.sin(t * math.pi / 2) ** 0.85
        outer.append((neck + (depth - neck) * t, r))
        inner.append((neck + (depth - neck) * t, r - wall))
    for e in range(4):
        origin = at_angle(ENGINE_RING, BELLY_Y, e * 90)
        down = (0, 0, -1)
        parts.append(metal(oa.lathe("engine collar", origin + Vector((0, 0, 0.03)), down, [(0.0, 0.24), (0.07, 0.24), (0.11, 0.2), (0.11, 0.1)], DARK_METAL, segments=18), 1 + e))
        bell = [(0.06, throat * 0.9)] + outer + [(depth, ENGINE_EXIT_RADIUS - wall * 0.5)] + list(reversed(inner)) + [(neck + 0.02, throat * 0.55)]
        o = oa.lathe("engine bell", origin, down, bell, DARK_METAL, segments=18)
        oa.paint(o, wear=0.5, dirt=0.6, mottle=0.9, seed=5 + e, wear_colour=BARE, grain=3.0)
        tint_inside(o, origin, throat)
        parts.append(o)
        # A stiffening band round the bell and three little pipes, so it is not just a cone.
        band_t = 0.55
        band_r = throat + (ENGINE_EXIT_RADIUS - throat) * math.sin(band_t * math.pi / 2) ** 0.85
        z = neck + (depth - neck) * band_t
        parts.append(metal(oa.lathe("engine band", origin, down, [(z - 0.03, band_r + 0.0), (z - 0.02, band_r + 0.035), (z + 0.02, band_r + 0.04), (z + 0.03, band_r + 0.012)], SILVER_DARK, segments=18), 9 + e))
    return parts


def tint_inside(obj, origin, throat):
    """The inside of an engine bell glows dull orange: faces that look in toward the bell's own axis."""
    mesh = obj.data
    col = mesh.color_attributes["Col"]
    for v in mesh.vertices:
        radial = Vector((v.co.x - origin.x, v.co.y - origin.y, 0))
        if radial.length < 1e-6:
            inward = True
        else:
            inward = v.normal.dot(radial.normalized()) < -0.2 or (v.normal.z < -0.6 and radial.length < throat)
        if inward:
            deep = max(0.0, min(1.0, (origin.z - v.co.z) / 0.65))
            c = Vector(ORANGE).lerp(Vector(ENGINE_ORANGE), deep)
            col.data[v.index].color = (oa._srgb_to_linear(c.x), oa._srgb_to_linear(c.y), oa._srgb_to_linear(c.z), 1.0)


# ------------------------------------------------------------------ thrusters

def thruster(index):
    parts = []
    degrees = 45
    centre = at_angle(THRUSTER_R, THRUSTER_Y, degrees)
    foot = at_angle(THRUSTER_FOOT_R, THRUSTER_FOOT_Y, degrees)
    a = math.radians(degrees)
    side = G(math.cos(a), 0, -math.sin(a))
    up = Vector((0, 0, 1))
    parts.append(metal(rod("stalk", foot, centre, 0.13, SILVER_DARK, radius_end=0.1, segments=12), 1))
    parts.append(metal(oa.lathe("stalk foot", foot - (centre - foot).normalized() * 0.02, (centre - foot), [(0.0, 0.2), (0.05, 0.19), (0.09, 0.13)], DARK_METAL, segments=12), 2))
    parts.append(painted(oa.box("block", centre, (0.36, 0.36, 0.36), CREAM, bevel=0.1, segments=3, rot=(0, 0, -degrees)), 3))
    parts.append(painted(oa.box("block stripe", centre, (0.375, 0.375, 0.09), ORANGE, bevel=0.03, rot=(0, 0, -degrees)), 4))
    for way in (up, -up, side, -side):
        parts.append(metal(oa.lathe("nozzle", centre + way * 0.13, way, [(0.0, 0.065), (0.07, 0.07), (0.2, 0.115), (0.2, 0.09), (0.08, 0.05)], DARK_METAL, segments=10), 5))
    for o in parts:
        oa.turn(o, index * 90)
    return parts


# ------------------------------------------------------------------ dish

def dish():
    parts = []
    axis = G(-0.25, 0.8, -0.55).normalized()
    centre = G(*DISH_CENTRE)
    foot = G(*DISH_MAST_FOOT)
    apex = centre - axis * 0.05
    R, depth, thick = DISH_RADIUS, 0.16, 0.035
    curve = depth / (R * R)
    parts.append(metal(oa.lathe("mast foot", foot + Vector((0, 0, 0.02)), (0, 0, 1), [(0.0, 0.17), (0.06, 0.16), (0.13, 0.09)], DARK_METAL, segments=12), 1))
    parts.append(metal(rod("mast", foot + Vector((0, 0, 0.08)), apex - axis * 0.05, 0.075, SILVER, radius_end=0.06, segments=10), 2))
    parts.append(metal(oa.sphere("mast joint", apex - axis * 0.07, 0.12, DARK_METAL, segments=12), 3))
    back, front = [], []
    rings = 5
    for k in range(1, rings + 1):
        q = R * k / rings
        back.append((curve * q * q - thick, q))
        front.append((curve * q * q, q))
    profile = [(-thick, 0.06)] + back + [(curve * R * R + 0.012, R + 0.012)] + list(reversed(front)) + [(0.0, 0.06)]
    bowl = oa.lathe("bowl", apex, axis, profile, CREAM, segments=24)
    oa.paint(bowl, wear=0.4, dirt=0.6, mottle=0.7, seed=4, wear_colour=(0.6, 0.6, 0.6), grain=2.5)
    parts.append(bowl)
    # Four orange petals painted on the face would need more corners than it is worth: a ring instead.
    q = R * 0.62
    parts.append(painted(oa.lathe("bowl ring", apex, axis, [(curve * q * q + 0.002, q - 0.03), (curve * q * q + 0.012, q), (curve * (q + 0.03) ** 2 + 0.004, q + 0.03)], ORANGE, segments=24), 5))
    feed = apex + axis * 0.6
    parts.append(metal(oa.lathe("feed horn", feed - axis * 0.07, axis, [(0.0, 0.085), (0.05, 0.07), (0.14, 0.045)], DARK_METAL, segments=10), 6))
    parts.append(painted(oa.sphere("feed tip", feed + axis * 0.08, 0.04, ORANGE, segments=8), 7))
    e1 = axis.orthogonal().normalized()
    e2 = axis.cross(e1)
    for k in range(3):
        t = math.radians(90 + 120 * k)
        q = R * 0.9
        on_dish = apex + axis * (curve * q * q) + (e1 * math.cos(t) + e2 * math.sin(t)) * q
        parts.append(metal(rod("feed strut", on_dish, feed, 0.02, SILVER, segments=6), 8))
    return parts


# ------------------------------------------------------------------ all of it

# ------------------------------------------------------------------ cameras

# Not from ShipLayout: the cockpit reads these places from the sockets (PoleCam0, PoleCam1, LandingCam).
POLE_MOUNT = (2.38, 1.5, -0.75)          # on the dome's skin, pilot's right; the left pole mirrors it
POLE_HEAD = (5.2, 2.6, -3.2)
POLE_AIM = (0.0, 0.6, 0.3)
LANDING_CAM = (0.9, BELLY_Y - 0.05, 0.9)
LENS_RED = (1.0, 0.22, 0.14)
LENS_GLASS = (0.10, 0.16, 0.26)


def turned(objects, origin, forward, up=(0, 0, 1)):
    """Moves pieces built at the origin looking along +Y so they sit at origin looking along forward."""
    f = Vector(forward).normalized()
    r = f.cross(Vector(up)).normalized()
    u = r.cross(f).normalized()
    m = Matrix(((r.x, f.x, u.x, origin[0]), (r.y, f.y, u.y, origin[1]), (r.z, f.z, u.z, origin[2]), (0, 0, 0, 1)))
    for o in objects:
        o.data.transform(m)
        o.data.update()
    return objects


def camera_head(seed):
    """A chunky, friendly camera, built at the origin looking along +Y. Returns (pieces, lamp, lens distance)."""
    o = []
    o.append(painted(oa.box("camera body", (0, 0, 0), (0.30, 0.36, 0.26), CREAM, bevel=0.07, segments=2), seed))
    o.append(painted(oa.box("camera stripe", (0, -0.06, 0), (0.312, 0.07, 0.272), ORANGE, bevel=0.02), seed + 1))
    o.append(metal(oa.lathe("lens barrel", (0, 0.16, 0), (0, 1, 0), [(0.0, 0.085), (0.02, 0.11), (0.10, 0.11), (0.12, 0.125), (0.17, 0.125), (0.17, 0.095), (0.10, 0.085)], CHARCOAL, segments=12), seed + 2))
    o.append(metal(oa.lathe("lens ring", (0, 0.255, 0), (0, 1, 0), [(0.0, 0.118), (0.008, 0.132), (0.03, 0.132), (0.038, 0.118)], SILVER, segments=12), seed + 3))
    glass = oa.sphere("lens glass", (0, 0.285, 0), 0.118, LENS_GLASS, scale=(1, 0.30, 1), segments=12)
    oa.paint(glass, wear=0, dirt=0, mottle=0.2, seed=seed, gradient=0.3)
    o.append(glass)
    o.append(painted(oa.box("sun hood", (0, 0.24, 0.135), (0.30, 0.20, 0.022), CHARCOAL, bevel=0.008), seed + 4))
    o.append(metal(oa.box("viewfinder", (0.0, -0.10, 0.155), (0.12, 0.14, 0.06), DARK_METAL, bevel=0.02), seed + 5))
    o.append(metal(rod("aerial", (-0.11, -0.12, 0.12), (-0.13, -0.16, 0.42), 0.008, CHARCOAL, segments=6), seed + 6))
    o.append(painted(oa.sphere("aerial tip", (-0.131, -0.162, 0.43), 0.02, ORANGE, segments=8), seed + 7))
    o.append(metal(oa.cylinder("lamp cup", (0.105, 0.172, 0.085), (0.105, 0.192, 0.085), 0.03, CHARCOAL, segments=10), seed + 8))
    lamp = oa.sphere("record lamp", (0.105, 0.196, 0.085), 0.022, LENS_RED, segments=8)
    oa.paint(lamp, wear=0, dirt=0, mottle=0, gradient=0)
    return o, [lamp], 0.30


def pole(index):
    """A selfie-stick camera boom: 0 on the pilot's left, 1 on the right."""
    sx = -1.0 if index == 0 else 1.0
    mount = G(sx * POLE_MOUNT[0], POLE_MOUNT[1], POLE_MOUNT[2])
    lens_at = G(sx * POLE_HEAD[0], POLE_HEAD[1], POLE_HEAD[2])
    aim = (G(*POLE_AIM) - lens_at).normalized()
    # The lens is at the stated place; the camera sits behind it and the boom comes up under the camera,
    # so nothing of the boom is in the picture.
    centre = lens_at - aim * 0.30
    head = centre - Vector((0, 0, 0.24))
    along = (head - mount).normalized()
    length = (head - mount).length
    parts = []
    parts.append(metal(oa.sphere("boom shoulder", mount, 0.17, DARK_METAL, segments=10), 1))
    parts.append(metal(oa.lathe("boom foot", mount - along * 0.12, along, [(0.0, 0.26), (0.05, 0.25), (0.12, 0.16)], DARK_METAL, segments=10), 2))
    parts.append(painted(rod("boom", mount, head, 0.06, CREAM, radius_end=0.04, segments=10), 3))
    # A thin stay under the boom with spacers, so it reads as a made thing and not a stick.
    down = (Vector((0, 0, -1)) - along * along.dot(Vector((0, 0, -1)))).normalized()
    parts.append(metal(rod("stay", mount + along * 0.3 + down * 0.26, head - along * 0.35 + down * 0.05, 0.016, SILVER, segments=6), 4))
    for k in range(5):
        t = 0.3 + (length - 0.7) * k / 4.0
        gap = 0.26 - 0.21 * k / 4.0
        p = mount + along * t
        parts.append(metal(rod("spacer", p, p + down * gap, 0.013, SILVER_DARK, segments=6), 5))
        if k in (1, 3):
            parts.append(painted(rod("boom band", p - along * 0.05, p + along * 0.05, 0.068 - 0.004 * k, ORANGE, segments=10, bevel=0.008), 6))
    parts.append(metal(oa.sphere("head joint", head, 0.085, DARK_METAL, segments=8), 7))
    parts.append(metal(rod("head yoke", head, centre - Vector((0, 0, 0.10)), 0.035, DARK_METAL, segments=8), 8))
    body, lamp, lens = camera_head(20 + index * 10)
    turned(body + lamp, centre, aim)
    return parts + body, lamp, centre + aim * lens


def landing_camera():
    """A small camera pod under the belly, lens straight down."""
    top = G(*LANDING_CAM)
    parts = []
    parts.append(metal(oa.cylinder("pod plate", top + Vector((0, 0, 0.04)), top - Vector((0, 0, 0.03)), 0.22, DARK_METAL, segments=14, bevel=0.012), 40))
    parts.append(metal(rod("pod neck", top, top - Vector((0, 0, 0.14)), 0.07, SILVER_DARK, segments=10), 41))
    centre = top - Vector((0, 0, 0.26))
    body, lamp, lens = camera_head(42)
    turned(body + lamp, centre, (0, 0, -1), up=(0, 1, 0))
    return parts + body, lamp, centre - Vector((0, 0, lens))


def lander():
    pieces = [("base", base())]
    for i in range(4):
        pieces.append(("leg%d" % i, leg(i)))
    pieces.append(("engines", engines()))
    for i in range(4):
        pieces.append(("thruster%d" % i, thruster(i)))
    pieces.append(("dish", dish()))
    cameras = []
    for i in range(2):
        body, lamp, lens = pole(i)
        pieces += [("pole%d" % i, body), ("pole%dGlow" % i, lamp)]
        cameras.append(("PoleCam%d" % i, lens))
        cameras.append(("PoleMount%d" % i, G((-1.0 if i == 0 else 1.0) * POLE_MOUNT[0], POLE_MOUNT[1], POLE_MOUNT[2])))
    body, lamp, lens = landing_camera()
    pieces += [("landingcam", body), ("landingcamGlow", lamp)]
    cameras.append(("LandingCam", lens))
    sockets = [oa.socket(name, position) for name, position in cameras]
    for i in range(4):
        sockets.append(oa.socket("LegRoot%d" % i, at_angle(LEG_ROOT_R, LEG_ROOT_Y, 45 + 90 * i)))
        sockets.append(oa.socket("Footpad%d" % i, at_angle(LEG_PAD_R, GROUND_Y, 45 + 90 * i)))
        sockets.append(oa.socket("Engine%d" % i, at_angle(ENGINE_RING, BELLY_Y, 90 * i)))
        sockets.append(oa.socket("Thruster%d" % i, at_angle(THRUSTER_R, THRUSTER_Y, 45 + 90 * i)))
    sockets.append(oa.socket("DishCentre", G(*DISH_CENTRE)))
    sockets.append(oa.socket("DishMastFoot", G(*DISH_MAST_FOOT)))
    return pieces, sockets


def load_hull(folder):
    """The cabin as the game makes it (dumped to .obj files), only so the preview shows the whole ship."""
    objs = []
    for f in sorted(glob.glob(os.path.join(folder, "*.obj"))):
        name = os.path.basename(f)
        if not (name.startswith("hull") or name.startswith("glass") or name.startswith("ring")):
            continue
        bpy.ops.wm.obj_import(filepath=f, forward_axis='Y', up_axis='Z')
        o = bpy.context.selected_objects[0]
        for v in o.data.vertices:
            v.co = (v.co.x, v.co.z, v.co.y)
        o.data.flip_normals()
        if o.data.color_attributes:
            o.data.color_attributes[0].name = "Col"
        col = o.data.color_attributes.get("Col")
        if name.startswith("glass"):
            col = col or o.data.color_attributes.new("Col", 'FLOAT_COLOR', 'POINT')
            for d in col.data:
                d.color = (0.03, 0.06, 0.09, 1)
        if not name.startswith("glass"):
            # Shade it round the way the game now does: corners at the same place share their facing.
            bpy.context.view_layer.objects.active = o
            o.data.attributes.active = o.data.attributes["Col"]
            bpy.ops.geometry.attribute_convert(mode='GENERIC', domain='CORNER', data_type='FLOAT_COLOR')
            bm = bmesh.new()
            bm.from_mesh(o.data)
            bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.0005)
            bm.to_mesh(o.data)
            bm.free()
            for p in o.data.polygons:
                p.use_smooth = True
        o["oa_smooth"] = 42.0
        o["oa_unchecked"] = 1
        objs.append(o)
    return objs


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    previews = args[args.index("--preview") + 1] if "--preview" in args else None
    hull = args[args.index("--hull") + 1] if "--hull" in args else None
    oa.reset_scene()
    pieces, sockets = lander()
    oa.export(os.path.join(OUT, "Lander.bytes"), pieces, sockets)
    if previews:
        shown = [o for _, objs in pieces for o in objs]
        if hull:
            shown += load_hull(hull)
        floor = oa.box("floor", (0, 0, GROUND_Y - 0.01), (400, 400, 0.02), (0.62, 0.42, 0.30), bevel=0)
        oa.paint(floor, wear=0, dirt=0, mottle=0, gradient=0)
        shown.append(floor)
        oa.render_preview(os.path.join(previews, "lander_a.png"), shown, (9, 13, 1.5), (0, 0, -0.4), lens=40, size=(1100, 800), samples=64)
        oa.render_preview(os.path.join(previews, "lander_b.png"), shown, (-12, -7, 4.5), (0, 0, -0.4), lens=40, size=(1100, 800), samples=64)
        oa.render_preview(os.path.join(previews, "lander_cams.png"), shown, (10.5, -10.5, 6.0), (0.5, -0.5, 0.6), lens=36, size=(1200, 820), samples=64)
        oa.render_preview(os.path.join(previews, "lander_camhead.png"), shown, (3.2, -1.0, 2.2), (5.0, -3.0, 2.5), lens=40, size=(900, 700), samples=64)
        oa.render_preview(os.path.join(previews, "lander_c.png"), shown, (5.2, 5.6, -2.9), (1.2, 1.0, -1.6), lens=30, size=(1100, 800), samples=64)
