"""Shared shapes and paint for growing and weathered things: tree crowns, trunks, fronds, rocks.

Used by build_scenery.py. Everything is built 1 unit tall standing on the origin (X right, Y forward, Z up),
because the game scales each placed thing by its height in metres. Colours are as seen on screen.
"""
import bpy, bmesh, math, random
from mathutils import Vector, Matrix, noise
import oa_blender as oa


def mix(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def clamp01(x):
    return 0.0 if x < 0 else 1.0 if x > 1 else x


def smooth(t):
    t = clamp01(t)
    return t * t * (3 - 2 * t)


def seed_vec(seed):
    return Vector((seed * 12.9898 % 97.0, seed * 78.233 % 89.0, seed * 37.719 % 83.0))


# ------------------------------------------------------------------ shapes

def cube_sphere(n):
    """A ball made of even four-sided faces (6 x n x n of them), with no pinched poles."""
    bm = bmesh.new()
    index = {}

    def vert(p):
        key = (round(p[0], 5), round(p[1], 5), round(p[2], 5))
        v = index.get(key)
        if v is None:
            v = bm.verts.new(Vector(p).normalized())
            index[key] = v
        return v

    for axis in range(3):
        for sign in (-1.0, 1.0):
            for i in range(n):
                for j in range(n):
                    corners = []
                    for di, dj in ((0, 0), (1, 0), (1, 1), (0, 1)):
                        # The tangent spreads the faces evenly instead of bunching them at the cube's corners.
                        a = math.tan((-1 + 2 * (i + di) / n) * math.pi / 4)
                        b = math.tan((-1 + 2 * (j + dj) / n) * math.pi / 4)
                        p = [0.0, 0.0, 0.0]
                        p[axis] = sign
                        p[(axis + 1) % 3] = a
                        p[(axis + 2) % 3] = b
                        corners.append(vert(p))
                    bm.faces.new(corners)
    return bm


def _reach(d, lobes):
    """How far the union of some ellipsoids reaches from the origin in direction d."""
    best = 0.0
    for off, rad in lobes:
        o = Vector((-off[0] / rad[0], -off[1] / rad[1], -off[2] / rad[2]))
        dd = Vector((d.x / rad[0], d.y / rad[1], d.z / rad[2]))
        a = dd.dot(dd)
        b = 2 * o.dot(dd)
        c = o.dot(o) - 1
        disc = b * b - 4 * a * c
        if disc >= 0:
            t = (-b + math.sqrt(disc)) / (2 * a)
            if t > best:
                best = t
    return best


def lobed(name, centre, lobes, colour, n=8, lump=0.10, grain=2.4, seed=0, flat=None, cuts=(), smooth_angle=80.0,
          ridged=0.0):
    """One closed skin wrapped round several overlapping ellipsoids: a cauliflower crown, a clump of rock.

    lobes: list of (offset from centre, (rx, ry, rz)). The first should sit on the centre so every direction
    hits something. flat: nothing hangs lower than this (relative to centre), giving crowns a tidy underside.
    cuts: list of (unit normal, distance): flat faces chiselled off, for rock.
    """
    bm = cube_sphere(n)
    sv = seed_vec(seed)
    core = min(min(r) for _, r in lobes[:1]) * 0.5
    for v in bm.verts:
        d = v.co.normalized()
        reach = max(_reach(d, lobes), core)
        nz = noise.noise(d * grain + sv)
        if ridged > 0:
            nz = nz * (1 - ridged) + ridged * (1 - 2 * abs(noise.noise(d * grain * 1.7 + sv * 1.3)))
        p = d * reach * (1 + lump * nz)
        for normal, distance in cuts:
            along = p.dot(normal)
            if along > distance:
                # Drawn back along its own line from the middle, so the skin can never fold over itself.
                p *= distance / along
        if flat is not None and p.z < flat:
            p.z = flat + (p.z - flat) * 0.22
        v.co = p
    # Every face of a ball looks away from its middle; say so outright, because once flat faces have been
    # chiselled off Blender can no longer be trusted to tell inside from outside.
    bm.normal_update()
    for f in bm.faces:
        if f.normal.dot(f.calc_center_median()) < 0:
            f.normal_flip()
    bmesh.ops.translate(bm, verts=bm.verts[:], vec=Vector(centre))
    return oa._new_object(name, bm, colour, smooth_angle, recalc=False)


def tube(name, points, radii, colour, sides=7, flute=None, twist=0.0, smooth_angle=60.0, cap_end=True):
    """A round limb along a bent path: trunks, boughs, stems. flute(angle, t) can swell or pinch the outline."""
    pts = [Vector(p) for p in points]
    n = len(pts)
    bm = bmesh.new()
    rings = []
    first = (pts[1] - pts[0]).normalized()
    ref = Vector((1, 0, 0)) if abs(first.x) < 0.9 else Vector((0, 1, 0))
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        u = (ref - t * ref.dot(t)).normalized()
        w = t.cross(u)
        ref = u
        ring = []
        for k in range(sides):
            a = 2 * math.pi * k / sides + twist * i
            r = radii[i] * (1 + (flute(a, i / (n - 1)) if flute else 0.0))
            ring.append(bm.verts.new(p + (u * math.cos(a) + w * math.sin(a)) * r))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for k in range(sides):
            j = (k + 1) % sides
            bm.faces.new((a[k], a[j], b[j], b[k]))
    bm.faces.new(list(reversed(rings[0])))
    if cap_end:
        bm.faces.new(rings[-1])
    return oa._new_object(name, bm, colour, smooth_angle)


def skirt(name, z_edge, z_tip, radius, colour, sides=12, droop=0.3, scallop=0.14, seed=0, tip_radius=0.1, tilt=0.05):
    """One tier of a fir tree: a soft bell of boughs with a gently ragged, drooping hem and a hollow underneath."""
    rng = random.Random(seed)
    bm = bmesh.new()
    h = z_tip - z_edge
    turn = rng.uniform(0, 6.28)
    tilt_a = rng.uniform(0, 6.28)
    waves = rng.choice((3, 4, 5))
    phase = rng.uniform(0, 6.28)
    jitter = [rng.uniform(0.94, 1.06) for _ in range(sides)]
    rings = []
    for j, t in enumerate((0.0, 0.3, 0.68, 1.0)):
        ring = []
        for k in range(sides):
            a = turn + 2 * math.pi * k / sides
            wave = math.cos(waves * a + phase)
            r = radius * (tip_radius + (1 - tip_radius) * t ** 0.7)
            r *= 1 + scallop * wave * t * t + (jitter[k] - 1) * t
            z = z_tip - h * t ** 1.5
            z -= droop * h * (0.5 + 0.5 * wave) * t ** 3
            z += tilt * r * math.cos(a - tilt_a)
            ring.append(bm.verts.new((math.cos(a) * r, math.sin(a) * r, z)))
        rings.append(ring)
    under = [bm.verts.new((math.cos(turn + 2 * math.pi * k / sides) * radius * 0.3,
                           math.sin(turn + 2 * math.pi * k / sides) * radius * 0.3, z_edge + h * 0.22)) for k in range(sides)]
    rings.append(under)
    for a, b in zip(rings, rings[1:]):
        for k in range(sides):
            j = (k + 1) % sides
            bm.faces.new((a[k], b[k], b[j], a[j]))
    bm.faces.new(rings[0])
    bm.faces.new(list(reversed(under)))
    return oa._new_object(name, bm, colour, 75.0)


def frond(name, root, outward, length, colour, width=0.07, segments=9, lift=0.25, droop=0.55, seed=0):
    """A palm leaf: a long arching blade, folded along its spine, with a feathered edge. It has thickness, so
    it shows from both sides."""
    root = Vector(root)
    out = Vector(outward).normalized()
    side = Vector((0, 0, 1)).cross(out).normalized()
    bm = bmesh.new()
    spine, left, right, keel, mids = [], [], [], [], []
    line = []
    for i in range(segments + 1):
        t = i / segments
        z = lift * length * math.sin(t * math.pi * 0.6) * 1.5 - droop * length * t ** 2.2
        line.append(root + out * (length * (t - 0.12 * t * t)) + Vector((0, 0, z)))
    for i in range(segments + 1):
        t = i / segments
        p = line[i]
        along = (line[min(i + 1, segments)] - line[max(i - 1, 0)]).normalized()
        up = side.cross(along).normalized()   # square to the blade, however steeply it hangs
        if up.z < 0 and abs(along.z) < 0.999:
            up = -up
        w = max(0.004, width * (math.sin(math.pi * (0.06 + 0.9 * t)) ** 0.6) * (1.3 if i % 2 else 0.72))
        fold = w * 0.62
        back = along * (-0.035 * length if i % 2 else 0.0)
        spine.append(bm.verts.new(p))
        left.append(bm.verts.new(p - side * w - up * fold + back))
        right.append(bm.verts.new(p + side * w - up * fold + back))
        k = p - up * (fold * 0.45 + 0.003)
        keel.append(bm.verts.new(k))
        mids.append((p + k) * 0.5)
    faces = []
    for i in range(segments):
        m = (mids[i] + mids[i + 1]) * 0.5
        for quad in ((spine[i], spine[i + 1], left[i + 1], left[i]), (spine[i + 1], spine[i], right[i], right[i + 1]),
                     (left[i], left[i + 1], keel[i + 1], keel[i]), (right[i + 1], right[i], keel[i], keel[i + 1])):
            faces.append((bm.faces.new(quad), m))
    for tri, m in (((spine[0], left[0], keel[0]), mids[0] + out), ((spine[0], keel[0], right[0]), mids[0] + out),
                   ((spine[-1], keel[-1], left[-1]), mids[-1] - out), ((spine[-1], right[-1], keel[-1]), mids[-1] - out)):
        faces.append((bm.faces.new(tri), m))
    bm.normal_update()
    # A folded blade is too thin for Blender to tell inside from outside, so each face is turned by hand to
    # look away from the blade's middle.
    for f, m in faces:
        if f.normal.dot(f.calc_center_median() - m) < 0:
            f.normal_flip()
    return oa._new_object(name, bm, colour, 40.0, recalc=False)


def blade(name, root, direction, length, width, colour, bend=0.4, segments=3):
    """A blade of grass or a reed: a thin three-sided spike that bends over."""
    root = Vector(root)
    d = Vector(direction).normalized()
    side = Vector((0, 0, 1)).cross(d)
    side = side.normalized() if side.length > 1e-6 else Vector((1, 0, 0))
    back = d.cross(side).normalized()
    pts, radii = [], []
    for i in range(segments + 1):
        t = i / segments
        pts.append(root + Vector((0, 0, 1)) * length * t * (1 - 0.25 * bend * t) + Vector((d.x, d.y, 0)) * length * bend * t * t)
        radii.append(width * (1 - t) ** 0.8 + 0.0008)
    bm = bmesh.new()
    rings = []
    for p, r in zip(pts, radii):
        rings.append([bm.verts.new(p + side * r), bm.verts.new(p - side * r), bm.verts.new(p + back * r * 0.5)])
    for a, b in zip(rings, rings[1:]):
        for k in range(3):
            j = (k + 1) % 3
            bm.faces.new((a[k], a[j], b[j], b[k]))
    bm.faces.new(rings[-1])
    return oa._new_object(name, bm, colour, 30.0)


def convex_hull(name, source, colour=(0.5, 0.5, 0.5)):
    """The tight wrapping of a shape, for the game's physics."""
    bm = bmesh.new()
    bm.from_mesh(source.data)
    result = bmesh.ops.convex_hull(bm, input=bm.verts)
    junk = [e for e in result.get("geom_interior", []) + result.get("geom_unused", []) if isinstance(e, bmesh.types.BMVert)]
    if junk:
        bmesh.ops.delete(bm, geom=junk, context='VERTS')
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    obj = oa._new_object(name, bm, colour, 30.0)
    flat_paint(obj, colour)
    return obj


# ------------------------------------------------------------------ paint

def _store(obj, colours):
    mesh = obj.data
    layer = mesh.color_attributes.get("Col") or mesh.color_attributes.new("Col", 'FLOAT_COLOR', 'POINT')
    for i in range(len(mesh.vertices)):
        c = colours[i]
        layer.data[i].color = (oa._srgb_to_linear(clamp01(c[0])), oa._srgb_to_linear(clamp01(c[1])), oa._srgb_to_linear(clamp01(c[2])), 1.0)


def _curves(bm):
    """How much the surface bulges out (+) or folds in (-) at each corner."""
    out = {}
    for v in bm.verts:
        curve = 0.0
        for e in v.link_edges:
            d = e.other_vert(v).co - v.co
            if d.length > 1e-9:
                curve -= v.normal.dot(d.normalized())
        out[v.index] = curve / max(1, len(v.link_edges))
    return out


def flat_paint(obj, colour):
    _store(obj, [colour] * len(obj.data.vertices))


def leaf_paint(obj, dark, mid, light, seed=0, grain=5.0, mottle=0.10, drift=None, snow=None, snow_line=0.55):
    """Foliage painted the way a background painter would: sunlit warm tops, cool deep undersides, darker folds
    between clumps, and soft patches drifting toward a second colour. snow: white laid on what faces up."""
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.normal_update()
    curves = _curves(bm)
    zs = [v.co.z for v in bm.verts]
    zmin, zmax = min(zs), max(zs)
    sv = seed_vec(seed)
    colours = []
    for v in bm.verts:
        h = 0.5 if zmax - zmin < 1e-6 else (v.co.z - zmin) / (zmax - zmin)
        t = clamp01(0.5 + 0.42 * v.normal.z + 0.30 * (h - 0.5))
        c = mix(dark, mid, smooth(t / 0.6)) if t < 0.6 else mix(mid, light, smooth((t - 0.6) / 0.4))
        fold = clamp01((-curves[v.index] - 0.02) * 4.5)
        c = mix(c, mix(dark, (0, 0, 0), 0.25), fold * 0.7)
        if drift is not None:
            patch = smooth(0.5 + 1.4 * noise.noise(v.co * grain * 0.45 + sv * 2.0))
            c = mix(c, drift, patch * 0.35 * (0.4 + 0.6 * t))
        m = 1.0 + mottle * (noise.noise(v.co * grain + sv) + 0.5 * noise.noise(v.co * grain * 3.1 + sv))
        c = (c[0] * m, c[1] * m, c[2] * m)
        if snow is not None:
            lie = smooth((v.normal.z - snow_line + 0.25 * noise.noise(v.co * grain * 1.5 + sv)) / 0.3)
            c = mix(c, snow, lie * 0.95)
        colours.append(c)
    bm.free()
    _store(obj, colours)
    return obj


def bark_paint(obj, colour, seed=0, moss=None, bands=None, grain=14.0, root_dark=0.35):
    """Wood: streaks running up the trunk, darker at the root and in the forks, optional moss low on one side
    and optional dark bands (birch)."""
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.normal_update()
    curves = _curves(bm)
    sv = seed_vec(seed)
    colours = []
    for v in bm.verts:
        co = v.co
        streak = noise.noise(Vector((co.x * grain, co.y * grain, co.z * grain * 0.18)) + sv)
        c = tuple(colour[i] * (1.0 + 0.16 * streak) for i in range(3))
        c = mix(c, tuple(x * 0.5 for x in colour), clamp01(1 - co.z / 0.12) * root_dark)
        c = mix(c, tuple(x * 0.55 for x in colour), clamp01((-curves[v.index] - 0.03) * 4.0) * 0.6)
        if bands is not None:
            b = noise.noise(Vector((co.x * 3.0, co.y * 3.0, co.z * 38.0)) + sv * 3.0)
            c = mix(c, bands, smooth((b - 0.18) / 0.2) * 0.85)
        if moss is not None:
            lie = clamp01(0.5 + 0.5 * v.normal.x + 0.4 * noise.noise(co * 9.0 + sv)) * clamp01(1 - co.z / 0.3)
            c = mix(c, moss, smooth(lie) * 0.75)
        colours.append(c)
    bm.free()
    _store(obj, colours)
    return obj


def rock_paint(obj, low, high, seed=0, strata=0.0, strata_colour=None, snow=None, snow_from=0.6, moss=None,
               grain=3.0, lichen=None):
    """Rock: layered bands, lighter chipped edges, dark cracks, and snow or moss lying on what faces up."""
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.normal_update()
    curves = _curves(bm)
    zs = [v.co.z for v in bm.verts]
    zmin, zmax = min(zs), max(zs)
    sv = seed_vec(seed)
    colours = []
    for v in bm.verts:
        co = v.co
        h = 0.5 if zmax - zmin < 1e-6 else (co.z - zmin) / (zmax - zmin)
        c = mix(low, high, smooth(h * 0.9 + 0.25 * noise.noise(co * grain + sv)))
        if strata > 0:
            wob = co.z + 0.035 * noise.noise(Vector((co.x * 2.2, co.y * 2.2, 0)) + sv)
            band = 0.5 + 0.5 * math.sin(wob * strata * 6.2832 + 3.0 * noise.noise(Vector((0, 0, wob * strata * 0.7)) + sv))
            c = mix(c, strata_colour or tuple(x * 0.72 for x in low), smooth(band) * 0.5)
        m = 1.0 + 0.12 * noise.noise(co * grain * 3.0 + sv) + 0.06 * noise.noise(co * grain * 9.0 + sv)
        c = (c[0] * m, c[1] * m, c[2] * m)
        curve = curves[v.index]
        c = mix(c, tuple(min(1.0, x * 1.35 + 0.05) for x in c), clamp01((curve - 0.06) * 4.0) * 0.55)
        c = mix(c, tuple(x * 0.5 for x in low), clamp01((-curve - 0.03) * 5.0) * 0.7)
        if lichen is not None:
            spot = smooth((noise.noise(co * grain * 4.0 + sv * 5.0) - 0.25) / 0.2)
            c = mix(c, lichen, spot * 0.5 * clamp01(0.3 + v.normal.z))
        if moss is not None:
            lie = smooth((v.normal.z - 0.35 + 0.3 * noise.noise(co * grain * 2.0 + sv)) / 0.35) * clamp01(1.25 - h)
            c = mix(c, moss, lie * 0.85)
        if snow is not None:
            lie = smooth((v.normal.z - 0.25 + 0.35 * noise.noise(co * grain * 1.6 + sv)) / 0.35) * smooth((h - snow_from + 0.12) / 0.2)
            c = mix(c, snow, lie)
        colours.append(c)
    bm.free()
    _store(obj, colours)
    return obj


def triangles(objects):
    total = 0
    for o in objects:
        o.data.calc_loop_triangles()
        total += len(o.data.loop_triangles)
    return total
