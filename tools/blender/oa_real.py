"""Shapes and paint for the realistic look (Matthew, 14 Nov 2026: "more realistic looking", not cartoony or blocky).

oa_blender.py has the plain shapes (box, cylinder, lathe). This file adds what real things need:

- skins drawn through a row of cross-sections (limbs, torsos, boots) instead of straight tubes,
- hoses and straps that follow a path,
- cloth that folds: gathered rings where a joint bends, pinched creases elsewhere,
- paint worked out from the shape: shadow where things crowd together, dust that settles low and on top,
- what each surface is made of (how smooth, whether it is bare metal), kept per corner in "Mat".

Everything is in metres, x to the model's right, y forward, z up, like the rest of the scripts.
"""
import bpy, bmesh, math
from mathutils import Vector, noise
from mathutils.bvhtree import BVHTree
import oa_blender as oa


# ------------------------------------------------------------------ curves through a few given points

def along(keys, t):
    """Reads a smooth curve through keys at t. Each key is (t, a, b, ...); the answer is [a, b, ...]."""
    n = len(keys)

    def slope(i, c):
        lo, hi = max(0, i - 1), min(n - 1, i + 1)
        return (keys[hi][c] - keys[lo][c]) / max(1e-9, keys[hi][0] - keys[lo][0])

    i = 0
    while i < n - 2 and t > keys[i + 1][0]:
        i += 1
    t0, t1 = keys[i][0], keys[i + 1][0]
    h = t1 - t0
    s = min(1.0, max(0.0, (t - t0) / h))
    out = []
    for c in range(1, len(keys[0])):
        p0, p1 = keys[i][c], keys[i + 1][c]
        m0, m1 = slope(i, c), slope(i + 1, c)
        out.append((2 * s ** 3 - 3 * s ** 2 + 1) * p0 + (s ** 3 - 2 * s ** 2 + s) * h * m0
                   + (-2 * s ** 3 + 3 * s ** 2) * p1 + (s ** 3 - s ** 2) * h * m1)
    return out


def smooth_path(points, cuts=3):
    """More points along a path, rounding its corners."""
    points = [Vector(p) for p in points]
    keys = [(float(i), p.x, p.y, p.z) for i, p in enumerate(points)]
    out = []
    for i in range((len(points) - 1) * cuts + 1):
        out.append(Vector(along(keys, i / cuts)))
    return out


# ------------------------------------------------------------------ skins

def ring(centre, u, v, ru, rv, n, power=2.0):
    """One cross-section: an oval (power 2) or a rounded box (power 3 and up) in the plane of u and v."""
    points = []
    e = 2.0 / power
    for i in range(n):
        a = 2 * math.pi * i / n
        c, s = math.cos(a), math.sin(a)
        points.append(centre + u * (ru * math.copysign(abs(c) ** e, c)) + v * (rv * math.copysign(abs(s) ** e, s)))
    return points


def loft(name, rings, colour, cap_start=True, cap_end=True, smooth=60.0):
    """A skin over a row of cross-sections that all have the same number of points."""
    bm = bmesh.new()
    made = [[bm.verts.new(p) for p in r] for r in rings]
    n = len(rings[0])
    for a, b in zip(made, made[1:]):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], a[j], b[j], b[i]))
    for r, wanted, first in ((made[0], cap_start, True), (made[-1], cap_end, False)):
        if not wanted:
            continue
        middle = bm.verts.new(sum((v.co for v in r), Vector()) / n)
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((middle, r[j], r[i]) if first else (middle, r[i], r[j]))
    return oa._new_object(name, bm, colour, smooth)


def limb(name, origin, axis, forward, keys, colour, n=16, step=0.030, fine=(), fine_step=0.012, power=2.0):
    """A skin along a straight line, its cross-section given at a few places and eased between them.

    keys: (distance along, radius sideways, radius forward, shift sideways, shift forward).
    "Sideways" is forward x axis. fine: (distance, reach) pairs where the rings are packed closer, for folds.
    """
    origin, axis, forward = Vector(origin), Vector(axis).normalized(), Vector(forward).normalized()
    side = forward.cross(axis)
    keys = [tuple(k) + (0.0,) * (5 - len(k)) for k in keys]
    t, end, places = keys[0][0], keys[-1][0], []
    while t < end - 1e-6:
        places.append(t)
        t += fine_step if any(abs(t - c) < r for c, r in fine) else step
    places.append(end)
    rings = []
    for t in places:
        rs, rf, os_, of = along(keys, t)
        rings.append(ring(origin + axis * t + side * os_ + forward * of, side, forward, max(rs, 0.002), max(rf, 0.002), n, power))
    return loft(name, rings, colour, smooth=80.0)


def sweep(name, points, radius, colour, n=8, up=(0, 0, 1), flat=1.0, radii=None, cuts=0, power=2.0, ribs=0.0):
    """A hose (round) or strap (flat < 1: thin along `up`) following a path. ribs: how much every other ring swells."""
    points = smooth_path(points, cuts) if cuts else [Vector(p) for p in points]
    rings = []
    last = len(points) - 1
    for i, p in enumerate(points):
        t = (points[min(i + 1, last)] - points[max(i - 1, 0)]).normalized()
        # `up` may be a rule giving the way out of the surface at each point, for a strap that goes round a body.
        u = Vector(up(p) if callable(up) else up).cross(t)
        if u.length < 1e-4:
            u = Vector((1, 0, 0)).cross(t)
        u.normalize()
        v = t.cross(u)
        r = radii[i] if radii else radius
        if ribs and i % 2 == 1:
            r *= 1.0 + ribs
        rings.append(ring(p, u, v, r, r * flat, n, power))
    return loft(name, rings, colour)


def ball_shell(name, centre, pole, up, radius, start, end, colour, n=28, rings=12, lip=0.0, close_start=False, close_end=True):
    """Part of a ball, from `start` degrees away from the pole to `end` degrees. A lip turns the open edge inward
    so the shell has thickness where you can see its rim."""
    centre, pole, up = Vector(centre), Vector(pole).normalized(), Vector(up)
    up = (up - pole * up.dot(pole)).normalized()
    across = pole.cross(up)
    rows = []

    def row(angle, r):
        a = math.radians(angle)
        return ring(centre + pole * (r * math.cos(a)), up, across, r * math.sin(a), r * math.sin(a), n)

    if lip > 0:
        rows.append(row(start + 7, radius - lip * 1.3))
        rows.append(row(start, radius - lip))
    for i in range(rings + 1):
        rows.append(row(start + (end - start) * i / rings, radius))
    return loft(name, rows, colour, cap_start=close_start, cap_end=close_end, smooth=80.0)


def ball_patch(name, centre, pole, up, radius, from_pole, round_from, round_to, colour, thickness=0.004, n=10, m=6):
    """A curved plate lying on a ball: between two angles from the pole, and between two angles round it
    (0 is toward `up`). It has thickness, outward from the ball."""
    centre, pole, up = Vector(centre), Vector(pole).normalized(), Vector(up)
    up = (up - pole * up.dot(pole)).normalized()
    across = pole.cross(up)
    bm = bmesh.new()
    grid = []
    for i in range(m + 1):
        a = math.radians(from_pole[0] + (from_pole[1] - from_pole[0]) * i / m)
        line = []
        for j in range(n + 1):
            b = math.radians(round_from + (round_to - round_from) * j / n)
            d = pole * math.cos(a) + (up * math.cos(b) + across * math.sin(b)) * math.sin(a)
            line.append(bm.verts.new(centre + d * radius))
        grid.append(line)
    faces = []
    for i in range(m):
        for j in range(n):
            faces.append(bm.faces.new((grid[i][j], grid[i][j + 1], grid[i + 1][j + 1], grid[i + 1][j])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.normal_update()
    # Turn the plate to look away from the ball's middle before giving it thickness.
    if faces[0].normal.dot(faces[0].calc_center_median() - centre) < 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=-thickness)
    return oa._new_object(name, bm, colour, 60.0)


# ------------------------------------------------------------------ cloth

def displace(obj, *folds):
    """Pushes each corner in or out along the surface by the sum of the fold rules."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * sum(f(v.co) for f in folds)
    bm.to_mesh(obj.data)
    bm.free()
    return obj


def creases(amount, scale, seed=0, where=None):
    """Pinched creases in loose cloth. scale stretches them: a large number along an axis packs creases along it,
    so (5, 5, 20) gives creases that run round an upright limb. where(p) can fade them in and out (0 to 1)."""
    sv = Vector(scale)
    shift = Vector((seed * 3.7, seed * 1.3, seed * 5.1))

    def rule(p):
        q = Vector((p.x * sv.x, p.y * sv.y, p.z * sv.z)) + shift
        a = noise.noise(q)
        b = noise.noise(q * 2.3 + Vector((7.0, 3.0, 1.0)))
        k = where(p) if where else 1.0
        return amount * k * ((1.0 - 2.4 * abs(a)) * 0.75 + b * 0.35)
    return rule


def gathers(centre, axis, reach, amount, spacing, seed=0, lean=(0, 0, 0), bias=0.0):
    """The rings of gathered cloth where a joint bends. lean: a direction the folds are deeper toward
    (the inside of an elbow, the back of a knee), by `bias` (0 to 1)."""
    c, ax, toward = Vector(centre), Vector(axis).normalized(), Vector(lean)

    def rule(p):
        d = (p - c).dot(ax)
        fall = math.exp(-(d / reach) ** 2)
        if fall < 0.02:
            return 0.0
        wobble = noise.noise(p * 11.0 + Vector((seed * 1.7, 0.0, seed))) * 1.4
        k = 1.0
        if bias:
            off = (p - c) - ax * d
            if off.length > 1e-6:
                k = 1.0 + bias * off.normalized().dot(toward)
        return amount * fall * k * math.sin(2 * math.pi * d / spacing + wobble)
    return rule


# ------------------------------------------------------------------ what a surface is made of, and paint

def made_of(obj, smooth, metal=0.0):
    """How smooth the surface is (0 chalk, 1 mirror) and whether it is bare metal. Kept per corner in "Mat"."""
    layer = obj.data.color_attributes.get("Mat") or obj.data.color_attributes.new("Mat", 'FLOAT_COLOR', 'POINT')
    for d in layer.data:
        d.color = (smooth, metal, 0.0, 1.0)
    return obj


def _hemisphere(count=18):
    out = []
    for i in range(count):
        z = (i + 0.5) / count
        r = math.sqrt(1.0 - z * z)
        a = i * 2.399963
        out.append((r * math.cos(a), r * math.sin(a), z))
    return out


def crowd_shadow(objects, strength=0.6, reach=0.20, tint=(0.80, 0.86, 1.0)):
    """Darkens the paint where other surfaces stand close in front (under straps, between pouches, in folds),
    the soft shadow that real things always have and flat-lit models never do. Worked out once, kept in the paint."""
    points, faces = [], []
    for o in objects:
        base = len(points)
        points.extend(v.co.copy() for v in o.data.vertices)
        faces.extend([base + i for i in p.vertices] for p in o.data.polygons)
    tree = BVHTree.FromPolygons(points, faces)
    rays = _hemisphere()
    for o in objects:
        layer = o.data.color_attributes.get("Col")
        if layer is None:
            continue
        for v in o.data.vertices:
            n = v.normal
            a = n.cross(Vector((0.31, 0.57, 0.76)))
            if a.length < 1e-4:
                a = n.cross(Vector((1, 0, 0)))
            a.normalize()
            b = n.cross(a)
            start = v.co + n * 0.003
            hidden = 0.0
            for x, y, z in rays:
                hit = tree.ray_cast(start, a * x + b * y + n * z, reach)
                if hit[0] is not None:
                    hidden += (1.0 - hit[3] / reach) * z
            open_share = 1.0 - min(1.0, hidden / (0.5 * len(rays)))
            k = 1.0 - strength * (1.0 - open_share)
            c = layer.data[v.index].color
            # Shadowed paint leans a little toward the sky's blue, as shade does outdoors.
            layer.data[v.index].color = (c[0] * k * (tint[0] + (1 - tint[0]) * k), c[1] * k * (tint[1] + (1 - tint[1]) * k), c[2] * k, 1.0)


def dust(objects, colour, top, amount=0.6, on_top=0.25, seed=0):
    """Fine dust: thick near the ground, thinning toward `top` (a height), with a little on everything that
    faces up. Dust is matte, so it dulls whatever it covers."""
    target = Vector([oa._srgb_to_linear(c) for c in colour])
    for o in objects:
        layer = o.data.color_attributes.get("Col")
        mat = o.data.color_attributes.get("Mat")
        if layer is None:
            continue
        for v in o.data.vertices:
            low = max(0.0, 1.0 - v.co.z / top) ** 1.4
            patch = 0.65 + 0.5 * noise.noise(v.co * 7.0 + Vector((seed, seed * 2.0, 0.0)))
            share = min(1.0, max(0.0, (amount * low + on_top * max(0.0, v.normal.z)) * patch))
            c = Vector(layer.data[v.index].color[:3]).lerp(target, share)
            layer.data[v.index].color = (c.x, c.y, c.z, 1.0)
            if mat is not None:
                m = mat.data[v.index].color
                mat.data[v.index].color = (m[0] * (1.0 - share), m[1] * (1.0 - 0.6 * share), 0.0, 1.0)


def stripes(obj, axis, spacing, colour, share=0.5, strength=1.0):
    """Paints bands across a piece (hose ribs, the tread of a sole) without spending triangles on them."""
    layer = obj.data.color_attributes.get("Col")
    ax = Vector(axis).normalized()
    target = Vector([oa._srgb_to_linear(c) for c in colour])
    for v in obj.data.vertices:
        if (v.co.dot(ax) / spacing) % 1.0 < share:
            c = Vector(layer.data[v.index].color[:3]).lerp(target, strength)
            layer.data[v.index].color = (c.x, c.y, c.z, 1.0)
    return obj
