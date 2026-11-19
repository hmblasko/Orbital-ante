"""Breaking a model into pieces ahead of time, for things the game lets the player destroy.

Used by build_bases.py (buildings) and build_scenery.py (trees). Nothing is broken while the game runs: the
pieces are cut here, in Blender, and stored in the model file next to the whole thing, so when a building
falls the game only swaps one for the other and lets physics have the pieces.

How a building is cut:
  - everything below `ruin_z` stays standing (the ruin: wall stubs, footings, a low heap of rubble);
  - everything above is cut into cells around a few seed points (a Voronoi split): each cell is a piece;
  - big shapes are sliced along the cell walls and the cut is closed with a raw face (torn panel, broken
    concrete); small fittings go whole to the cell their middle is in, the way bolted-on things come away.
"""
import bmesh, math, random
from mathutils import Vector, noise
import oa_blender as oa
import oa_nature as nat

EPS = 1e-4


def _bm_of(obj):
    """The shape as a bmesh, with seams welded: a shape that was cut before has its cut faces on corners of
    their own, and an open seam would leave the next cut's outline open and unfillable."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    # a ring shrunk to a point (the tip of a lathed roof) welds into faces of no area: clear them away
    bmesh.ops.dissolve_degenerate(bm, dist=1e-5, edges=bm.edges[:])
    return bm


def _col_layer(bm):
    return bm.verts.layers.float_color.get("Col")


def _extent(obj):
    vs = [v.co for v in obj.data.vertices]
    if not vs:
        return 0.0, Vector()
    lo = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    hi = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    return max(hi - lo), (lo + hi) * 0.5


def _cut(bm, planes, colour, seed):
    """Keeps what is behind every plane (point, normal: the normal points at what is thrown away) and closes
    each cut with a raw face as it goes, so the shape stays closed for the next cut. At the end the faces of
    the closings are given their own corners, so their colour is their own."""
    capped = bm.faces.layers.int.get("oa_cap") or bm.faces.layers.int.new("oa_cap")
    for co, no in planes:
        if not bm.verts:
            return
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        bmesh.ops.bisect_plane(bm, geom=geom, dist=EPS, plane_co=co, plane_no=no, clear_outer=True)
        if not bm.verts:
            return
        loose = [e for e in bm.edges if len(e.link_faces) == 1
                 and abs((e.verts[0].co - co).dot(no)) < EPS * 20 and abs((e.verts[1].co - co).dot(no)) < EPS * 20]
        if len(loose) < 3:
            continue
        made = bmesh.ops.triangle_fill(bm, use_beauty=True, use_dissolve=False, edges=loose, normal=no)
        for f in made.get("geom", []):
            if not isinstance(f, bmesh.types.BMFace):
                continue
            f.normal_update()
            if f.normal.dot(no) < 0:
                f.normal_flip()
            f[capped] = 1
    faces = [f for f in bm.faces if f[capped] == 1]
    if not faces:
        return
    split = bmesh.ops.split(bm, geom=faces, use_only_faces=False)
    layer = _col_layer(bm)
    if layer is None:
        return
    for e in split["geom"]:
        if isinstance(e, bmesh.types.BMVert):
            n = noise.noise(e.co * 3.1 + Vector((seed * 1.7, 0, 0)))
            k = 0.85 + 0.25 * n
            e[layer] = (oa._srgb_to_linear(colour[0] * k), oa._srgb_to_linear(colour[1] * k), oa._srgb_to_linear(colour[2] * k), 1.0)


def _piece_from(bm, name, smooth=40.0):
    if not bm.verts:
        bm.free()
        return None
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    obj = oa._new_object(name, bm, (0.5, 0.5, 0.5), smooth, recalc=False)
    return obj


def seeds_for(objs, count, low, rng):
    """Seed points spread through what stands above `low`: picked from the shapes' own corners, so a cell is
    never wasted on empty air, then pulled a little toward the middle."""
    pts = []
    for o in objs:
        for v in o.data.vertices:
            if v.co.z > low + 0.05:
                pts.append(v.co.copy())
    if not pts:
        return []
    mid = sum(pts, Vector()) / len(pts)
    rng.shuffle(pts)
    seeds = []
    # Far apart from each other: farthest-point picking among a sample.
    sample = pts[: min(len(pts), 600)]
    seeds.append(sample[0])
    while len(seeds) < count and len(seeds) < len(sample):
        best, best_d = None, -1.0
        for p in sample:
            d = min((p - s).length for s in seeds)
            if d > best_d:
                best, best_d = p, d
        seeds.append(best)
    return [s.lerp(mid, 0.25) + Vector((rng.uniform(-0.1, 0.1), rng.uniform(-0.1, 0.1), rng.uniform(-0.1, 0.1))) for s in seeds]


def fracture(objs, cells, ruin_z, raw_colour, seed=0, small=1.0, name="bit"):
    """Cuts copies of `objs` (they are left as they are). Returns (pieces, ruin): a list of piece objects, one per
    cell that got anything, and the object left standing below ruin_z (or None)."""
    rng = random.Random(seed)
    seeds = seeds_for(objs, cells, ruin_z, rng)
    if not seeds:
        seeds = [Vector((0, 0, ruin_z + 1))]
    cell_parts = [[] for _ in seeds]
    ruin_parts = []
    for oi, o in enumerate(objs):
        size, mid = _extent(o)
        if size < small:
            # Small fittings come away whole.
            if mid.z < ruin_z and ruin_z > 0.01:
                ruin_parts.append(_bm_of(o))
            else:
                k = min(range(len(seeds)), key=lambda i: (seeds[i] - mid).length)
                cell_parts[k].append(_bm_of(o))
            continue
        # Below the ruin line (nothing stays standing when the line is on the floor).
        if ruin_z > 0.01:
            bm = _bm_of(o)
            _cut(bm, [(Vector((0, 0, ruin_z)), Vector((0, 0, 1)))], raw_colour, seed + oi)
            if bm.verts:
                ruin_parts.append(bm)
            else:
                bm.free()
        # Above it, cell by cell.
        for c, s in enumerate(seeds):
            bm = _bm_of(o)
            planes = [(Vector((0, 0, ruin_z)), Vector((0, 0, -1)))] if ruin_z > 0.01 else []
            for j, t in enumerate(seeds):
                if j == c:
                    continue
                n = (t - s)
                if n.length < 1e-6:
                    continue
                planes.append(((s + t) * 0.5, n.normalized()))
            _cut(bm, planes, raw_colour, seed + oi * 31 + c)
            if bm.verts:
                cell_parts[c].append(bm)
            else:
                bm.free()

    def join(parts, label):
        if not parts:
            return None
        out = bmesh.new()
        for p in parts:
            tmp = oa._new_object("tmp", p, (0.5, 0.5, 0.5), recalc=False)
            out.from_mesh(tmp.data)
            import bpy
            bpy.data.objects.remove(tmp)
        return _piece_from(out, label)

    pieces = []
    for c, parts in enumerate(cell_parts):
        p = join(parts, "%s%d" % (name, c))
        if p is not None and len(p.data.polygons) > 0:
            pieces.append(p)
    ruin = join(ruin_parts, name + "_ruin")
    return pieces, ruin


def rubble(name, centre, radius, height, colours, seed=0, lumps=5):
    """A low heap of broken stuff where something fell: lumpy, greyed with dust, a few slabs sticking out."""
    rng = random.Random(seed)
    lobes = []
    for i in range(lumps):
        a = rng.uniform(0, 2 * math.pi)
        d = rng.uniform(0, radius * 0.6)
        lobes.append(((math.cos(a) * d, math.sin(a) * d, 0), (radius * rng.uniform(0.35, 0.6), radius * rng.uniform(0.35, 0.6), height * rng.uniform(0.5, 1.0))))
    o = nat.lobed(name, (centre[0], centre[1], 0.02), lobes, colours[0], n=6, lump=0.35, grain=3.0, seed=seed, flat=-0.02, smooth_angle=30, ridged=0.5)
    for v in o.data.vertices:
        v.co.z = max(v.co.z, 0.0)
    nat.rock_paint(o, colours[0], colours[1], seed=seed, strata=0.0, grain=2.0)
    return o


def clip_below(obj, z):
    """Keeps the part of a closed shape below height z, closed again on top (for what stays solid in a ruin)."""
    bm = _bm_of(obj)
    _cut(bm, [(Vector((0, 0, z)), Vector((0, 0, 1)))], (0.5, 0.5, 0.5), 0)
    if not bm.verts:
        bm.free()
        return None
    return _piece_from(bm, obj.name + "_low")


def split_at(objs, z, colour, name, seed=0):
    """Cuts copies of `objs` across at height z (a tree felled at its foot): returns (below, above), two lists
    of objects, each closed again where it was cut with a face of fresh `colour` (the pale wood inside)."""
    import bpy
    below, above = [], []
    for i, o in enumerate(objs):
        for keep_below, out in ((True, below), (False, above)):
            bm = _bm_of(o)
            normal = Vector((0, 0, 1)) if keep_below else Vector((0, 0, -1))
            _cut(bm, [(Vector((0, 0, z)), normal)], colour, seed + i)
            if not bm.verts:
                bm.free()
                continue
            piece = _piece_from(bm, "%s_%s%d" % (name, "low" if keep_below else "high", i))
            if piece is not None and len(piece.data.polygons) > 0:
                out.append(piece)
            elif piece is not None:
                bpy.data.objects.remove(piece)
    return below, above
