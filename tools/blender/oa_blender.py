"""Shared helpers for Orbital Ante's Blender-made models.

Models are built by script, coloured per corner (no texture files, no UVs) and written to a small ".bytes"
file the game reads itself (Scripts/Runtime/Look/ModelFile.cs). Run a build script with:

    blender -b -P tools/blender/build_weapons.py

Working space in Blender: X is the model's right, Y is forward (out of the barrel, the ship's nose), Z is up,
in metres. The exporter turns that into the game's space (X right, Y up, Z forward).
"""
import bpy, bmesh, math, struct, os, random
from mathutils import Vector, Matrix, Euler, noise, kdtree
from mathutils.bvhtree import BVHTree
import bmesh


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def _new_object(name, bm, colour, smooth_angle=40.0, recalc=True):
    mesh = bpy.data.meshes.new(name)
    # Whatever order a closed shape's corners were made in, its faces are turned to look outward.
    # (An open skin has no inside to tell by, so its maker passes recalc=False and orders it by hand.)
    if recalc:
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj["oa_colour"] = list(colour)
    obj["oa_smooth"] = smooth_angle
    for p in mesh.polygons:
        p.use_smooth = True
    return obj


def _place(obj, location, rotation=None, scale=None):
    """Moves the piece's points themselves, so every piece stays in the model's own space."""
    m = Matrix.Translation(Vector(location))
    if rotation is not None:
        m = m @ rotation.to_matrix().to_4x4()
    if scale is not None:
        m = m @ Matrix.Diagonal((*scale, 1.0))
    obj.data.transform(m)
    obj.data.update()
    return obj


def box(name, centre, size, colour, bevel=0.004, segments=2, rot=(0, 0, 0)):
    """A box with rounded edges. size is (x, y, z)."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if bevel > 0:
        b = min(bevel, min(size) * 0.45)
        bmesh.ops.bevel(bm, geom=bm.edges[:], offset=b, segments=segments, profile=0.5, affect='EDGES', clamp_overlap=True)
    obj = _new_object(name, bm, colour)
    return _place(obj, centre, Euler([math.radians(a) for a in rot]))


def cylinder(name, start, end, radius, colour, radius_end=None, segments=20, bevel=0.0, cap=True):
    """A cylinder (or cone) from one point to another."""
    start, end = Vector(start), Vector(end)
    length = (end - start).length
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=segments,
                          radius1=radius, radius2=radius if radius_end is None else radius_end, depth=length)
    if bevel > 0 and cap:
        rim = [e for e in bm.edges if abs(e.verts[0].co.z - e.verts[1].co.z) < 1e-6]
        bmesh.ops.bevel(bm, geom=rim, offset=min(bevel, radius * 0.6), segments=2, profile=0.5, affect='EDGES', clamp_overlap=True)
    obj = _new_object(name, bm, colour, 50.0)
    axis = (end - start).normalized()
    return _place(obj, (start + end) * 0.5, Vector((0, 0, 1)).rotation_difference(axis))


def lathe(name, origin, axis, profile, colour, segments=24):
    """Spins a profile round an axis. profile is a list of (distance along the axis, radius)."""
    bm = bmesh.new()
    rings = []
    for along, radius in profile:
        ring = [bm.verts.new((radius * math.cos(2 * math.pi * i / segments), radius * math.sin(2 * math.pi * i / segments), along))
                for i in range(segments)]
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for i in range(segments):
            j = (i + 1) % segments
            bm.faces.new((a[i], a[j], b[j], b[i]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    obj = _new_object(name, bm, colour, 35.0)
    return _place(obj, origin, Vector((0, 0, 1)).rotation_difference(Vector(axis).normalized()))


def slab(name, outline, width, colour, bevel=0.006, segments=3, x=0.0):
    """A flat outline given real thickness and rounded edges: stocks, grips, magazines, fins, panels.

    outline is a list of (forward, up) points going round the shape once, thickened along X.
    """
    bm = bmesh.new()
    front = [bm.verts.new((-width / 2, p[0], p[1])) for p in outline]
    back = [bm.verts.new((width / 2, p[0], p[1])) for p in outline]
    n = len(outline)
    bm.faces.new(front)
    bm.faces.new(list(reversed(back)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((front[j], front[i], back[i], back[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=bm.edges[:], offset=min(bevel, width * 0.45), segments=segments, profile=0.5, affect='EDGES', clamp_overlap=True)
    obj = _new_object(name, bm, colour)
    return _place(obj, (x, 0, 0))


def sphere(name, centre, radius, colour, scale=(1, 1, 1), segments=16):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=segments // 2, radius=radius)
    obj = _new_object(name, bm, colour, 80.0)
    return _place(obj, centre, None, scale)


def turn(obj, degrees, pivot=(0, 0, 0)):
    """Turns a piece about the upright axis through pivot. Positive is clockwise seen from above."""
    m = Matrix.Translation(Vector(pivot)) @ Matrix.Rotation(-math.radians(degrees), 4, 'Z') @ Matrix.Translation(-Vector(pivot))
    obj.data.transform(m)
    obj.data.update()
    return obj


def densify(obj, longest):
    """Cuts long edges so there are enough corners for paint and crumpling to show on big flat faces."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    for _ in range(4):
        long_edges = [e for e in bm.edges if e.calc_length() > longest]
        if not long_edges:
            break
        bmesh.ops.subdivide_edges(bm, edges=long_edges, cuts=1, use_grid_fill=True)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    bm.to_mesh(obj.data)
    bm.free()
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def crumple(obj, amount, grain=3.0, seed=0):
    """Pushes corners in and out a little, like foil or beaten metal fitted by hand."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.normal_update()
    for v in bm.verts:
        n = noise.noise(v.co * grain + Vector((seed * 7.3, seed * 1.9, 0))) + 0.5 * noise.noise(v.co * grain * 2.7 + Vector((0, 0, seed * 3.3)))
        v.co += v.normal * (n * amount)
    bm.to_mesh(obj.data)
    bm.free()
    return obj


def socket(name, position):
    """A named attachment point (muzzle, hand, magazine)."""
    e = bpy.data.objects.new("socket_" + name, None)
    e.location = position
    bpy.context.scene.collection.objects.link(e)
    return e


# ------------------------------------------------------------------ paint

def _srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def paint(obj, wear=0.5, dirt=0.5, mottle=0.5, seed=0, wear_colour=None, gradient=0.08, grain=9.0):
    """Hand-painted look, worked out per corner and stored in the mesh:
    lighter worn paint on outward edges, darker grime in creases, soft blotches of lighter and darker paint,
    and a slight top-to-bottom gradient. Colours are as seen on screen (not linear).
    """
    mesh = obj.data
    base = Vector(obj["oa_colour"][:3])
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.verts.ensure_lookup_table()
    bm.normal_update()
    zs = [v.co.z for v in bm.verts]
    zmin, zmax = min(zs), max(zs)
    values = {}
    for v in bm.verts:
        # How much the surface bulges out (+) or folds in (-) here.
        curve = 0.0
        for e in v.link_edges:
            other = e.other_vert(v)
            d = other.co - v.co
            if d.length > 1e-9:
                curve -= v.normal.dot(d.normalized())
        curve /= max(1, len(v.link_edges))
        world = v.co
        n1 = noise.noise(world * grain + Vector((seed * 3.1, 0, 0)))
        n2 = noise.noise(world * grain * 4.0 + Vector((0, seed * 5.7, 0)))
        height = 0.0 if zmax - zmin < 1e-6 else (world.z - zmin) / (zmax - zmin)
        colour = base.copy()
        colour *= 1.0 + mottle * (0.16 * n1 + 0.08 * n2)
        colour *= 1.0 - gradient + 2.0 * gradient * height
        worn = max(0.0, min(1.0, (curve - 0.07) * 5.0 * (0.7 + 1.2 * n1))) * wear
        target = Vector(wear_colour) if wear_colour else base * 1.55 + Vector((0.08, 0.08, 0.08))
        colour = colour.lerp(target, worn * 0.6)
        grime = max(0.0, min(1.0, (-curve - 0.03) * 5.0)) * dirt
        colour = colour.lerp(base * 0.45, grime * 0.75)
        values[v.index] = [max(0.0, min(1.0, c)) for c in colour]
    bm.free()
    layer = mesh.color_attributes.get("Col") or mesh.color_attributes.new("Col", 'FLOAT_COLOR', 'POINT')
    for i, v in enumerate(mesh.vertices):
        c = values[i]
        # Blender's own renderer wants linear numbers; the exporter turns them back.
        layer.data[i].color = (_srgb_to_linear(c[0]), _srgb_to_linear(c[1]), _srgb_to_linear(c[2]), 1.0)


# ------------------------------------------------------------------ export

def _linear_to_srgb(c):
    c = max(0.0, min(1.0, c))
    return c * 12.92 if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055


def _part_arrays(objects):
    """Corners of several pieces as flat lists in the game's space, sharing corners that are identical."""
    positions, normals, colours, finishes, triangles = [], [], [], [], []
    index = {}
    for obj in objects:
        mesh = obj.data
        mesh.use_auto_smooth = True
        mesh.auto_smooth_angle = math.radians(obj.get("oa_smooth", 40.0))
        mesh.calc_normals_split()
        mesh.calc_loop_triangles()
        col = mesh.color_attributes.get("Col")
        mat = mesh.color_attributes.get("Mat")      # what the surface is made of: (how smooth, bare metal); see oa_real.made_of
        for tri in mesh.loop_triangles:
            corner = []
            for li in tri.loops:
                loop = mesh.loops[li]
                p = mesh.vertices[loop.vertex_index].co
                n = loop.normal
                c = col.data[loop.vertex_index].color if col else (0.5, 0.5, 0.5, 1)
                m = mat.data[loop.vertex_index].color if mat else (0.0, 0.0, 0.0, 1)
                # Blender (x right, y forward, z up) to the game (x right, y up, z forward).
                key = (round(p.x, 5), round(p.z, 5), round(p.y, 5), round(n.x, 3), round(n.z, 3), round(n.y, 3),
                       round(_linear_to_srgb(c[0]), 3), round(_linear_to_srgb(c[1]), 3), round(_linear_to_srgb(c[2]), 3),
                       round(max(0.0, min(1.0, m[0])), 2), round(max(0.0, min(1.0, m[1])), 2))
                i = index.get(key)
                if i is None:
                    i = len(positions) // 3
                    index[key] = i
                    positions.extend(key[0:3])
                    normals.extend(key[3:6])
                    colours.extend(key[6:9])
                    finishes.extend(key[9:11])
                corner.append(i)
            # Swapping two axes mirrors the model, so the order of each triangle's corners is flipped back.
            triangles.extend((corner[0], corner[2], corner[1]))
    return positions, normals, colours, finishes, triangles


def _write_string(f, text):
    data = text.encode("utf-8")
    f.write(struct.pack("<B", len(data)))
    f.write(data)


def export(path, parts, sockets=()):
    """parts: list of (name, [objects]). sockets: Blender empties made with socket().

    The layout is "OAM1", or "OAM2" when any piece says what its surfaces are made of ("Mat", see oa_real.made_of):
    then two more bytes follow each piece's colours for every corner, how smooth and how far bare metal.
    The game reads both (Scripts/Runtime/Look/ModelFile.cs).
    """
    os.makedirs(os.path.dirname(path), exist_ok=True)
    report = []
    says_finish = any(o.data.color_attributes.get("Mat") is not None for _, objects in parts for o in objects)
    with open(path, "wb") as f:
        f.write(b"OAM2" if says_finish else b"OAM1")
        f.write(struct.pack("<i", len(parts)))
        for name, objects in parts:
            positions, normals, colours, finishes, triangles = _part_arrays(objects)
            _write_string(f, name)
            count = len(positions) // 3
            f.write(struct.pack("<ii", count, len(triangles) // 3))
            f.write(struct.pack("<%df" % len(positions), *positions))
            f.write(struct.pack("<%df" % len(normals), *normals))
            f.write(struct.pack("<%dB" % len(colours), *[int(round(c * 255)) for c in colours]))
            if says_finish:
                f.write(struct.pack("<%dB" % len(finishes), *[int(round(c * 255)) for c in finishes]))
            f.write(struct.pack("<%di" % len(triangles), *triangles))
            report.append("%s: %d corners, %d triangles" % (name, count, len(triangles) // 3))
        f.write(struct.pack("<i", len(sockets)))
        for s in sockets:
            _write_string(f, s.name.replace("socket_", ""))
            f.write(struct.pack("<3f", s.location.x, s.location.z, s.location.y))
    print("OA wrote", path, os.path.getsize(path), "bytes;", "; ".join(report))


# ------------------------------------------------------------------ far copies

FAR = "Far"     # ModelFile.FarSuffix in the game: the far copy of model X is the file XFar


def _triangle_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def _thinned(obj, ratio):
    """A copy of one piece with about `ratio` of its triangles, keeping its shape, paint and how it is shaded."""
    copy = obj.copy()
    copy.data = obj.data.copy()
    bpy.context.scene.collection.objects.link(copy)
    if ratio < 0.999:
        mod = copy.modifiers.new("thin", 'DECIMATE')
        mod.decimate_type = 'COLLAPSE'
        mod.ratio = ratio
        graph = bpy.context.evaluated_depsgraph_get()
        mesh = bpy.data.meshes.new_from_object(copy.evaluated_get(graph), preserve_all_data_layers=True, depsgraph=graph)
        copy.modifiers.remove(mod)
        old = copy.data
        copy.data = mesh
        _carry_paint(old, mesh)
        bpy.data.meshes.remove(old)
        for p in copy.data.polygons:
            p.use_smooth = True
    return copy


_VIEWS = []


def _views():
    """Twenty-four ways of looking out from a surface, spread over the half of the sky above it."""
    if not _VIEWS:
        for ring, (height, count) in enumerate(((80, 1), (55, 6), (30, 8), (10, 9))):
            for k in range(count):
                up, round_ = math.radians(height), 2.0 * math.pi * (k + 0.5 * ring) / count
                _VIEWS.append(Vector((math.cos(up) * math.cos(round_), math.cos(up) * math.sin(round_), math.sin(up))))
    return _VIEWS


def _tree_of(objects):
    """The surfaces of some shapes as one thing to cast rays at, or None if there are none."""
    corners, triangles = [], []
    for o in objects:
        mesh = o.data
        mesh.calc_loop_triangles()
        base = len(corners)
        corners.extend(v.co.copy() for v in mesh.vertices)
        triangles.extend(tuple(base + i for i in t.vertices) for t in mesh.loop_triangles)
    return BVHTree.FromPolygons(corners, triangles) if triangles else None


def _drop_hidden(copies, originals):
    """Takes out of the thinned shapes of one piece the faces nobody can ever see. Changes `copies` in place
    and returns them, with None where nothing of a shape is left.

    A model is shapes laid over and sunk into each other (a plate on a chest, a vest inside a jacket), and most
    of what is underneath is never seen. Thinning moves every surface a little, so in a far copy the shape
    underneath pokes through the one on top in patches of the wrong colour: the first thing that makes a far
    copy look cheap. A face of a thinned shape is taken out if both of these hold:

    - in the FULL model nothing can be seen from its middle in any direction past the other shapes of the same
      piece (it is buried, or it is one of those patches), and
    - taking it out cannot open a hole: in the THINNED model it is buried too, or another shape's surface lies
      just beneath it (which is what a patch poking through its cover looks like from the cover's side).

    The second test matters because a thinned cover does not reach quite as far as the full one did: a belly
    hidden under the skirt of a breastplate is no longer hidden when the skirt has drawn back, and without it
    you would see inside (the preview shows that in magenta).

    Only shapes of the SAME piece hide each other: pieces move against each other in the game (a ball joint
    turns inside a cuff). A face next to one that is kept is kept too, as a margin. And this is done after
    thinning, never before: thinning a shape with holes cut in it eats the edges of the holes back.
    """
    thin_trees = [_tree_of([o for k, o in enumerate(copies) if k != i]) for i in range(len(copies))]
    for i, copy in enumerate(copies):
        full_tree = _tree_of([o for k, o in enumerate(originals) if k != i])
        thin_tree = thin_trees[i]
        if full_tree is None or thin_tree is None:
            continue
        bm = bmesh.new()
        bm.from_mesh(copy.data)
        bm.normal_update()
        keep = set()
        for f in bm.faces:
            n = f.normal
            if not (0.5 < n.length < 1.5):
                keep.update(v.index for v in f.verts)
                continue
            across = n.orthogonal().normalized()
            along = n.cross(across)
            middle = f.calc_center_median()
            ways = [across * d.x + along * d.y + n * d.z for d in _views()]
            # A thinned face is big: its middle can be covered while a corner shows. Look from near each corner too.
            spots = [middle] + [middle.lerp(v.co, 0.7) for v in f.verts]
            starts = [spot + n * 0.0005 for spot in spots]
            if any(full_tree.ray_cast(start, way, 3.0)[0] is None for start in starts for way in ways):
                keep.update(v.index for v in f.verts)       # seen in the full model
                continue
            if all(thin_tree.ray_cast(start, way, 3.0)[0] is not None for start in starts for way in ways):
                continue                                    # buried in the far copy as well

            def covered_beneath(spot):
                hit, normal, _, _ = thin_tree.ray_cast(spot, -n, 0.04)
                return hit is not None and normal.dot(n) > 0.2

            if all(covered_beneath(spot) for spot in spots):
                continue                                    # poking through a cover that lies just beneath
            keep.update(v.index for v in f.verts)           # nothing would be behind it: it stays
        hidden = [f for f in bm.faces if not any(v.index in keep for v in f.verts)]
        if hidden and len(hidden) == len(bm.faces):
            bm.free()
            mesh = copy.data
            bpy.data.objects.remove(copy)
            bpy.data.meshes.remove(mesh)
            copies[i] = None
            continue
        if hidden:
            bmesh.ops.delete(bm, geom=hidden, context='FACES')
            bm.to_mesh(copy.data)
            copy.data.update()
        bm.free()
    return copies


def inside_out_share(objects, rays=4000, seed=7):
    """How often somebody looking at these shapes from outside would see the inside of one: the share of rays
    cast at them from all round whose first hit is the back of a face. The game does not draw backs, so there
    you would see straight through. A far copy should score no worse than its full model."""
    tree_objects = [o for o in objects if o is not None]
    corners, triangles = [], []
    for o in tree_objects:
        mesh = o.data
        mesh.calc_loop_triangles()
        base = len(corners)
        corners.extend(v.co.copy() for v in mesh.vertices)
        triangles.extend(tuple(base + i for i in t.vertices) for t in mesh.loop_triangles)
    if not triangles:
        return 0.0
    tree = BVHTree.FromPolygons(corners, triangles)
    lo = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    hi = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    middle, reach = (lo + hi) / 2, (hi - lo).length
    rng = random.Random(seed)
    hits = backs = 0
    for _ in range(rays):
        d = Vector((rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1))).normalized()
        aim = Vector((rng.uniform(lo.x, hi.x), rng.uniform(lo.y, hi.y), rng.uniform(lo.z, hi.z)))
        start = aim - d * (reach + 1.0)
        hit, normal, _, _ = tree.ray_cast(start, d, 2.0 * reach + 2.0)
        if hit is None:
            continue
        hits += 1
        if normal.dot(d) > 0.0:
            backs += 1
    return backs / float(hits) if hits else 0.0


def _carry_paint(full, thinned):
    """Gives each corner of the thinned mesh the paint of the full mesh round the same spot.

    Thinning does not carry the paint across reliably, so it is looked up again: the average of the nearest few
    painted corners of the same shape, nearer ones counting for more. Averaging also calms the worn edges and
    grime, which were worked out for corners a far copy no longer has. (Taking the paint from whatever shape
    of the full model shows at that spot was tried and is worse: a corner under a pad takes the pad's colour
    and smears it down the limb.)
    """
    source = full.color_attributes.get("Col")
    if source is None or len(full.vertices) == 0:
        return
    tree = kdtree.KDTree(len(full.vertices))
    for i, v in enumerate(full.vertices):
        tree.insert(v.co, i)
    tree.balance()
    old = thinned.color_attributes.get("Col")
    if old is not None:
        thinned.color_attributes.remove(old)
    layer = thinned.color_attributes.new("Col", 'FLOAT_COLOR', 'POINT')
    for i, v in enumerate(thinned.vertices):
        total, weight = [0.0, 0.0, 0.0], 0.0
        for _, index, distance in tree.find_n(v.co, 4):
            w = 1.0 / (distance + 0.002)
            c = source.data[index].color
            total[0] += c[0] * w; total[1] += c[1] * w; total[2] += c[2] * w
            weight += w
        layer.data[i].color = (total[0] / weight, total[1] / weight, total[2] / weight, 1.0)
    # What the surface is made of is carried the same way, from the nearest corner alone: a finish is a fact
    # about a material, and an average of cloth and metal is neither.
    made = full.color_attributes.get("Mat")
    if made is not None:
        old = thinned.color_attributes.get("Mat")
        if old is not None:
            thinned.color_attributes.remove(old)
        # Removing a layer can move the others in memory, so "Col" is not touched again after this point.
        carried = [tuple(made.data[tree.find(v.co)[1]].color) for v in thinned.vertices]
        layer = thinned.color_attributes.new("Mat", 'FLOAT_COLOR', 'POINT')
        for i, c in enumerate(carried):
            layer.data[i].color = c


def thin(parts, target, drop_below=0.035, whole=64, least=24, keep=(), leave_out=()):
    """The far copy of a model: the same pieces by the same names, in about `target` triangles in all.

    parts is a list of (name, [objects]) as export() takes it; the result is another, made of new objects (the
    originals are left alone). Fittings smaller than drop_below across (bolts, studs, rivets: a pixel or less
    from where a far copy is seen) are left out, but no piece is ever left empty. Pieces that shine by
    themselves (named ...Glow) are kept exactly as they are: they are small, and the game switches them by name.

    Faces that can never be seen (a vest inside a jacket, the chest under a plate) are taken out: see
    _drop_hidden.

    A shape of `whole` triangles or fewer (a plate, a strap, a buckle: a box with its edges eased) is kept as it
    is. Thinning one gives a crumpled lump with dark creases across it, which reads as a cheap model from much
    further off than a few saved triangles are worth. So is any shape named in `keep` (the name it was made
    with, without Blender's ".001"): for shapes that cross each other at a shallow angle, a visor let into a
    helmet, where thinning either one turns the clean line between them into a ragged blotch. Everything
    else, the rounded shapes that carry most of the triangles and thin gracefully, is thinned by the same
    share, found by trying, with none taken below `least` triangles. A shape named in `leave_out` is not in
    the far copy at all: for one that barely shows in the full model and pokes through its cover when thinned.

    The line it prints says how often the far copy is seen from inside (see inside_out_share). It should say
    0.00%, as the full model does: anything else is a hole to go and look for in the preview, in magenta.
    """
    keep, leave_out = set(keep), set(leave_out)

    def kept_whole(o, n):
        return n <= whole or o.name.split(".")[0] in keep

    plain, fixed = [], 0
    for name, objects in parts:
        if name.endswith("Glow"):
            fixed += sum(_triangle_count(o) for o in objects)
            continue
        kept = [o for o in objects if max(o.dimensions) >= drop_below and o.name.split(".")[0] not in leave_out]
        if not kept and objects:
            kept = [max(objects, key=lambda o: max(o.dimensions))]
        plain.append((name, kept))

    def made(ratio, keep):
        total, out = fixed, []
        for name, objects in plain:
            copies = []
            for o in objects:
                n = _triangle_count(o)
                copies.append(_thinned(o, 1.0 if kept_whole(o, n) else max(ratio, least / float(n))))
            biggest = max(range(len(objects)), key=lambda k: max(objects[k].dimensions)) if objects else -1
            copies = _drop_hidden(copies, objects)
            if objects and all(c is None for c in copies):
                copies[biggest] = _thinned(objects[biggest], 1.0)     # a piece is never left empty
            copies = [c for c in copies if c is not None]
            total += sum(_triangle_count(c) for c in copies)
            out.append((name, copies))
        if not keep:
            for _, copies in out:
                for c in copies:
                    mesh = c.data
                    bpy.data.objects.remove(c)
                    bpy.data.meshes.remove(mesh)
        return total, out

    low, high = 0.01, 1.0
    for _ in range(8):
        middle = (low + high) / 2
        total, _ = made(middle, keep=False)
        if total > target:
            high = middle
        else:
            low = middle
    total, out = made(low, keep=True)
    by_name = dict(out)
    result = [(name, objects if name.endswith("Glow") else by_name[name]) for name, objects in parts]
    full_share = inside_out_share([o for _, objects in parts for o in objects])
    far_share = inside_out_share([o for _, objects in out for o in objects] + [o for name, objects in parts if name.endswith("Glow") for o in objects])
    print("OA far copy: %d triangles (asked for %d), share %.3f; seen from inside %.2f%% of the time (the full model %.2f%%)"
          % (total, target, low, 100 * far_share, 100 * full_share))
    return result


# ------------------------------------------------------------------ preview pictures

def render_preview(path, objects, eye, target, lens=50, size=(1100, 700), sun=(50, 20, 35), world=(0.36, 0.5, 0.72), samples=96,
                   glow=(), glow_strength=5.0, sun_energy=3.6, sky=0.7):
    """A picture of the model with soft sun and sky light, showing the per-corner paint. Not the game's shader."""
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = samples
    scene.cycles.device = 'CPU'
    scene.cycles.use_denoising = False
    scene.render.resolution_x, scene.render.resolution_y = size
    try:
        scene.view_settings.view_transform = 'Filmic'
        scene.view_settings.look = 'Medium High Contrast'
    except Exception:
        pass
    mat = bpy.data.materials.new("oa_preview")
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes["Principled BSDF"]
    attr = nodes.new("ShaderNodeVertexColor")
    attr.layer_name = "Col"
    # A face that is inside out (the game would not draw it) shows up flat magenta to the camera.
    geo = nodes.new("ShaderNodeNewGeometry")
    camera_ray = nodes.new("ShaderNodeLightPath")
    both = nodes.new("ShaderNodeMath")
    both.operation = 'MULTIPLY'
    links.new(geo.outputs["Backfacing"], both.inputs[0])
    links.new(camera_ray.outputs["Is Camera Ray"], both.inputs[1])
    warn = nodes.new("ShaderNodeEmission")
    warn.inputs[0].default_value = (1.0, 0.0, 1.0, 1.0)
    warn.inputs[1].default_value = 2.0
    pick = nodes.new("ShaderNodeMixShader")
    out = nodes["Material Output"]
    links.new(attr.outputs["Color"], bsdf.inputs["Base Color"])
    links.new(both.outputs[0], pick.inputs[0])
    links.new(bsdf.outputs[0], pick.inputs[1])
    links.new(warn.outputs[0], pick.inputs[2])
    links.new(pick.outputs[0], out.inputs["Surface"])
    plain = bpy.data.materials.new("oa_preview_plain")
    plain.use_nodes = True
    pattr = plain.node_tree.nodes.new("ShaderNodeVertexColor")
    pattr.layer_name = "Col"
    plain.node_tree.links.new(pattr.outputs["Color"], plain.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    plain.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.62
    plain.use_backface_culling = False
    bsdf.inputs["Roughness"].default_value = 0.62
    # Models made for the realistic look say what each surface is made of ("Mat": how smooth, whether bare metal).
    # When any piece in the picture does, the picture shows it; pieces that do not are given the old plain finish.
    if any(o.data.color_attributes.get("Mat") for o in objects):
        for o in objects:
            if not o.data.color_attributes.get("Mat"):
                made = o.data.color_attributes.new("Mat", 'FLOAT_COLOR', 'POINT')
                for d in made.data:
                    d.color = (0.38, 0.0, 0.0, 1.0)
        for m in (mat, plain):
            tree = m.node_tree
            what = tree.nodes.new("ShaderNodeVertexColor")
            what.layer_name = "Mat"
            split = tree.nodes.new("ShaderNodeSeparateColor")
            rough = tree.nodes.new("ShaderNodeMath")
            rough.operation = 'SUBTRACT'
            rough.inputs[0].default_value = 1.0
            tree.links.new(what.outputs["Color"], split.inputs[0])
            tree.links.new(split.outputs[0], rough.inputs[1])
            tree.links.new(rough.outputs[0], tree.nodes["Principled BSDF"].inputs["Roughness"])
            tree.links.new(split.outputs[1], tree.nodes["Principled BSDF"].inputs["Metallic"])
    for o in objects:
        o.data.materials.clear()
        o.data.materials.append(plain if o.get("oa_unchecked") else mat)
        o.data.use_auto_smooth = True
        o.data.auto_smooth_angle = math.radians(o.get("oa_smooth", 40.0))
    if glow:
        shine = bpy.data.materials.new("oa_preview_glow")
        shine.use_nodes = True
        gattr = shine.node_tree.nodes.new("ShaderNodeVertexColor")
        gattr.layer_name = "Col"
        emit = shine.node_tree.nodes.new("ShaderNodeEmission")
        emit.inputs[1].default_value = glow_strength
        shine.node_tree.links.new(gattr.outputs["Color"], emit.inputs[0])
        shine.node_tree.links.new(emit.outputs[0], shine.node_tree.nodes["Material Output"].inputs["Surface"])
        for o in glow:
            o.data.materials.clear()
            o.data.materials.append(shine)
    if scene.world is None:
        scene.world = bpy.data.worlds.new("w")
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (*world, 1)
    bg.inputs[1].default_value = sky
    light = bpy.data.lights.new("sun", 'SUN')
    light.energy = sun_energy
    light.color = (1.0, 0.93, 0.8)
    light.angle = math.radians(6)
    lo = bpy.data.objects.new("sun", light)
    lo.rotation_euler = [math.radians(a) for a in sun]
    scene.collection.objects.link(lo)
    cam = bpy.data.cameras.new("cam")
    cam.lens = lens
    co = bpy.data.objects.new("cam", cam)
    co.location = eye
    co.rotation_euler = (Vector(target) - Vector(eye)).to_track_quat('-Z', 'Y').to_euler()
    scene.collection.objects.link(co)
    scene.camera = co
    scene.render.filepath = path
    scene.render.film_transparent = False
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(lo)
    bpy.data.objects.remove(co)
