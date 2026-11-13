"""Shared helpers for Orbital Ante's Blender-made models.

Models are built by script, coloured per corner (no texture files, no UVs) and written to a small ".bytes"
file the game reads itself (Scripts/Runtime/Look/ModelFile.cs). Run a build script with:

    blender -b -P tools/blender/build_weapons.py

Working space in Blender: X is the model's right, Y is forward (out of the barrel, the ship's nose), Z is up,
in metres. The exporter turns that into the game's space (X right, Y up, Z forward).
"""
import bpy, bmesh, math, struct, os, random
from mathutils import Vector, Matrix, Euler, noise


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
    positions, normals, colours, triangles = [], [], [], []
    index = {}
    for obj in objects:
        mesh = obj.data
        mesh.use_auto_smooth = True
        mesh.auto_smooth_angle = math.radians(obj.get("oa_smooth", 40.0))
        mesh.calc_normals_split()
        mesh.calc_loop_triangles()
        col = mesh.color_attributes.get("Col")
        for tri in mesh.loop_triangles:
            corner = []
            for li in tri.loops:
                loop = mesh.loops[li]
                p = mesh.vertices[loop.vertex_index].co
                n = loop.normal
                c = col.data[loop.vertex_index].color if col else (0.5, 0.5, 0.5, 1)
                # Blender (x right, y forward, z up) to the game (x right, y up, z forward).
                key = (round(p.x, 5), round(p.z, 5), round(p.y, 5), round(n.x, 3), round(n.z, 3), round(n.y, 3),
                       round(_linear_to_srgb(c[0]), 3), round(_linear_to_srgb(c[1]), 3), round(_linear_to_srgb(c[2]), 3))
                i = index.get(key)
                if i is None:
                    i = len(positions) // 3
                    index[key] = i
                    positions.extend(key[0:3])
                    normals.extend(key[3:6])
                    colours.extend(key[6:9])
                corner.append(i)
            # Swapping two axes mirrors the model, so the order of each triangle's corners is flipped back.
            triangles.extend((corner[0], corner[2], corner[1]))
    return positions, normals, colours, triangles


def _write_string(f, text):
    data = text.encode("utf-8")
    f.write(struct.pack("<B", len(data)))
    f.write(data)


def export(path, parts, sockets=()):
    """parts: list of (name, [objects]). sockets: Blender empties made with socket()."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    report = []
    with open(path, "wb") as f:
        f.write(b"OAM1")
        f.write(struct.pack("<i", len(parts)))
        for name, objects in parts:
            positions, normals, colours, triangles = _part_arrays(objects)
            _write_string(f, name)
            count = len(positions) // 3
            f.write(struct.pack("<ii", count, len(triangles) // 3))
            f.write(struct.pack("<%df" % len(positions), *positions))
            f.write(struct.pack("<%df" % len(normals), *normals))
            f.write(struct.pack("<%dB" % len(colours), *[int(round(c * 255)) for c in colours]))
            f.write(struct.pack("<%di" % len(triangles), *triangles))
            report.append("%s: %d corners, %d triangles" % (name, count, len(triangles) // 3))
        f.write(struct.pack("<i", len(sockets)))
        for s in sockets:
            _write_string(f, s.name.replace("socket_", ""))
            f.write(struct.pack("<3f", s.location.x, s.location.z, s.location.y))
    print("OA wrote", path, os.path.getsize(path), "bytes;", "; ".join(report))


# ------------------------------------------------------------------ preview pictures

def render_preview(path, objects, eye, target, lens=50, size=(1100, 700), sun=(50, 20, 35), world=(0.36, 0.5, 0.72), samples=96):
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
    for o in objects:
        o.data.materials.clear()
        o.data.materials.append(plain if o.get("oa_unchecked") else mat)
        o.data.use_auto_smooth = True
        o.data.auto_smooth_angle = math.radians(o.get("oa_smooth", 40.0))
    if scene.world is None:
        scene.world = bpy.data.worlds.new("w")
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (*world, 1)
    bg.inputs[1].default_value = 0.7
    light = bpy.data.lights.new("sun", 'SUN')
    light.energy = 3.6
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
