"""Stage 2 of bringing a Tripo body in: the cut model (cut.blend) becomes the game's 14 pieces. Caps the cuts,
lays the pieces out, bakes Tripo's own pictures onto them, stands them on the game's skeleton, writes the model
and its far copy and draws pictures.   fit.py -- <out folder> <pictures folder> [--draft] [--size N]"""
import bpy, bmesh, sys, os, math
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
sys.path.insert(0, "/home/claude/orbital-ante/tools/blender")
import oa_blender as oa
import oa_materials as mats
import build_enemy as be

args = sys.argv[sys.argv.index("--") + 1:]
OUT, PICS = args[0], args[1]
DRAFT = "--draft" in args
SIZE = int(args[args.index("--size") + 1]) if "--size" in args else (1024 if DRAFT else 2048)
NAME = "Enemy"
CAGE = 0.012
STUMP_SCALE, STUMP_AT = 0.11, Vector((0.0, 0.0, -1.0))
BLANK = (0.060, 0.066, 0.055)      # what a painted-out badge becomes: a plain dull patch
BONES = {
    "Head": ("neck", "head"), "Torso": ("hips", "neck"),
    "LeftUpperArm": ("L_shoulder", "L_elbow"), "LeftForearm": ("L_elbow", "L_wrist"), "LeftHand": ("L_wrist", "L_tip"),
    "RightUpperArm": ("R_shoulder", "R_elbow"), "RightForearm": ("R_elbow", "R_wrist"), "RightHand": ("R_wrist", "R_tip"),
    "LeftThigh": ("L_hip", "L_knee"), "LeftShin": ("L_knee", "L_ankle"), "LeftFoot": ("L_ankle", "L_sole"),
    "RightThigh": ("R_hip", "R_knee"), "RightShin": ("R_knee", "R_ankle"), "RightFoot": ("R_ankle", "R_sole"),
}


def to_linear(c):
    c = np.asarray(c, dtype=np.float64)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def pixels_of(image):
    a = np.empty(image.size[0] * image.size[1] * 4, dtype=np.float32)
    image.pixels.foreach_get(a)
    return a.reshape(image.size[1], image.size[0], 4)


def loops_of(bm):
    """The open edges of a mesh, as closed runs of corners."""
    edges = {e for e in bm.edges if len(e.link_faces) == 1}
    runs = []
    while edges:
        e = edges.pop()
        run, v = [e.verts[0]], e.verts[1]
        while v is not run[0]:
            run.append(v)
            nxt = [x for x in v.link_edges if x in edges]
            if not nxt:
                break
            edges.discard(nxt[0])
            v = nxt[0].other_vert(v)
        runs.append(run)
    return runs


def cap(bm, layer):
    """Closes every open end with a low dome that pokes into the neighbouring piece, so a bent joint shows suit
    and not a hole. Returns [(faces, rim corners)] for colouring."""
    made = []
    for run in loops_of(bm):
        if len(run) < 3:
            continue
        c = sum((v.co for v in run), Vector()) / len(run)
        r = sum((v.co - c).length for v in run) / len(run)
        n = Vector()
        for i, v in enumerate(run):
            w = run[(i + 1) % len(run)]
            n += (v.co - c).cross(w.co - c)
        if n.length < 1e-12:
            continue
        n.normalize()
        inside = [f.calc_center_median() for v in run for f in v.link_faces]
        inner = sum(inside, Vector()) / len(inside)
        if (inner - c).dot(n) > 0:
            n = -n
        faces = []
        try:
            if r < 0.012 or len(run) < 6:
                faces.append(bm.faces.new(run))
            else:
                rise = max(0.042, min(0.6 * r, 0.06))      # the game wants at least 3 cm of ball behind a joint
                ring = [bm.verts.new(c + (v.co - c) * 0.62 + n * rise * 0.7) for v in run]
                top = bm.verts.new(c + n * rise)
                for i in range(len(run)):
                    j = (i + 1) % len(run)
                    faces.append(bm.faces.new((run[i], run[j], ring[j], ring[i])))
                    faces.append(bm.faces.new((ring[i], ring[j], top)))
        except ValueError:
            continue
        for f in faces:
            f[layer] = 1
        made.append((faces, [v.co.copy() for v in run]))
    return made


def pieces_from(low):
    """One closed object per piece, and one object holding copies of all the caps in the colour of the suit
    round each (what the caps are baked from)."""
    high = bpy.data.objects["high"]
    graph = bpy.context.evaluated_depsgraph_get()
    tree = BVHTree.FromObject(high, graph)
    hm = high.data
    base = pixels_of(next(n.image for n in hm.materials[0].node_tree.nodes if n.bl_idname == "ShaderNodeTexImage" and "basecolor" in n.image.name))
    uv = hm.uv_layers[0].data

    def suit_colour(points):
        got = []
        for p in points[::max(1, len(points) // 16)]:
            _, _, index, _ = tree.find_nearest(p)
            if index is None:
                continue
            u = uv[hm.polygons[index].loop_start].uv
            got.append(base[int(u[1] % 1.0 * (base.shape[0] - 1)), int(u[0] % 1.0 * (base.shape[1] - 1)), :3])
        return to_linear(np.mean(got, axis=0)) * 0.55 if got else np.array(BLANK)

    names = list(BONES)
    src = bmesh.new()
    src.from_mesh(low.data)
    piece = src.faces.layers.int["piece"]
    parts, caps_bm = {}, bmesh.new()
    cap_col = caps_bm.loops.layers.float_color.new("capcol")
    for index, name in enumerate(names):
        bm = src.copy()
        layer = bm.faces.layers.int["piece"]
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f[layer] != index], context='FACES')
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.0002)      # cuts that ended up inside one piece close again
        seen, small = set(), []
        for f in bm.faces:
            if f in seen:
                continue
            lump, stack = [f], [f]
            seen.add(f)
            while stack:
                for e in stack.pop().edges:
                    for g in e.link_faces:
                        if g not in seen:
                            seen.add(g); lump.append(g); stack.append(g)
            if len(lump) < 12:
                small += lump
        if small:
            bmesh.ops.delete(bm, geom=small, context='FACES')
        is_cap = bm.faces.layers.int.new("cap")
        for faces, rim in cap(bm, is_cap):
            colour = tuple(suit_colour(rim)) + (1.0,)
            new = {}
            for f in faces:
                vs = []
                for v in f.verts:
                    if v not in new:
                        new[v] = caps_bm.verts.new(v.co)
                    vs.append(new[v])
                g = caps_bm.faces.new(vs)
                for l in g.loops:
                    l[cap_col] = colour
        bmesh.ops.dissolve_degenerate(bm, dist=0.00002, edges=bm.edges[:])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new(name.lower(), mesh)
        bpy.context.scene.collection.objects.link(obj)
        obj["oa_smooth"] = 180.0
        for p in mesh.polygons:
            p.use_smooth = True
        parts[name] = [obj]
        print("OA piece %-14s %5d triangles" % (name, oa._triangle_count(obj)))
    src.free()
    bmesh.ops.recalc_face_normals(caps_bm, faces=caps_bm.faces[:])
    mesh = bpy.data.meshes.new("caps")
    caps_bm.to_mesh(mesh)
    caps_bm.free()
    caps = bpy.data.objects.new("caps", mesh)
    bpy.context.scene.collection.objects.link(caps)
    return parts, caps


def flat(name, colour_of, mask_of):
    """A material with the three faces the bake asks for (as oa_materials' are)."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    t = mat.node_tree
    bsdf = t.nodes["Principled BSDF"]
    out = next(n for n in t.nodes if n.bl_idname == "ShaderNodeOutputMaterial")
    colour = t.nodes.new("ShaderNodeEmission")
    mask = t.nodes.new("ShaderNodeEmission")
    colour_of(t, colour.inputs["Color"], bsdf)
    mask_of(t, mask.inputs["Color"], bsdf)
    mat["oa_surface"], mat["oa_colour"], mat["oa_mask"], mat["oa_out"] = bsdf.name, colour.name, mask.name, out.name
    return mat


def tripo_material(old, joints):
    """Tripo's pictures as a bake source: colour with the badges painted out, and the game's mask."""
    images = {}
    for n in old.node_tree.nodes:
        if n.bl_idname == "ShaderNodeTexImage":
            images["basecolor" if "basecolor" in n.image.name else "rm" if "_rm" in n.image.name else "normal"] = n.image

    def tex(t, what):
        n = t.nodes.new("ShaderNodeTexImage")
        n.image = images[what]
        return n

    def maths(t, op, a, b=None):
        n = t.nodes.new("ShaderNodeMath")
        n.operation = op
        for i, x in enumerate((a, b)):
            if x is None:
                continue
            if isinstance(x, float):
                n.inputs[i].default_value = x
            else:
                t.links.new(x, n.inputs[i])
        return n.outputs[0]

    def colour_of(t, socket, bsdf):
        base = tex(t, "basecolor")
        hsv = t.nodes.new("ShaderNodeSeparateColor")
        hsv.mode = 'HSV'
        t.links.new(base.outputs["Color"], hsv.inputs[0])
        geo = t.nodes.new("ShaderNodeNewGeometry")
        near = None
        for centre, radius in BADGES(joints):
            d = t.nodes.new("ShaderNodeVectorMath")
            d.operation = 'DISTANCE'
            t.links.new(geo.outputs["Position"], d.inputs[0])
            d.inputs[1].default_value = centre
            one = maths(t, 'LESS_THAN', d.outputs["Value"], radius)
            near = one if near is None else maths(t, 'MAXIMUM', near, one)
        loud = near
        mix = t.nodes.new("ShaderNodeMix")
        mix.data_type = 'RGBA'
        t.links.new(loud, mix.inputs[0])
        t.links.new(base.outputs["Color"], mix.inputs[6])
        mix.inputs[7].default_value = BLANK + (1.0,)
        t.links.new(mix.outputs[2], socket)
        t.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
        bump = t.nodes.new("ShaderNodeNormalMap")
        t.links.new(tex(t, "normal").outputs["Color"], bump.inputs["Color"])
        t.links.new(bump.outputs[0], bsdf.inputs["Normal"])

    def mask_of(t, socket, bsdf):
        rm = t.nodes.new("ShaderNodeSeparateColor")
        t.links.new(tex(t, "rm").outputs["Color"], rm.inputs[0])
        # Tripo's cloth is about 0.7 to 0.85 rough; the game skips the shine at 0.1 smooth or less, and cloth must.
        smooth_ = maths(t, 'MULTIPLY', maths(t, 'MAXIMUM', maths(t, 'SUBTRACT', 0.68, rm.outputs[1]), 0.0), 1.35)
        pack = t.nodes.new("ShaderNodeCombineColor")
        pack.inputs[0].default_value = 1.0
        t.links.new(smooth_, pack.inputs[1])
        t.links.new(rm.outputs[2], pack.inputs[2])
        t.links.new(pack.outputs[0], socket)
        t.links.new(rm.outputs[1], bsdf.inputs["Roughness"])
        t.links.new(rm.outputs[2], bsdf.inputs["Metallic"])

    for im in images.values():
        im.colorspace_settings.name = "sRGB" if im is images["basecolor"] else "Non-Color"
    return flat("oa_tripo", colour_of, mask_of)


def BADGES(j):
    """Where the model wears badges (found by colour, badge.py): the game's enemies carry no flags or markings."""
    return [(Vector((0.326, 0.035, 1.378)), 0.078), (Vector((-0.336, 0.04, 1.368)), 0.078),
            (Vector((-0.171, 0.185, 1.48)), 0.036), (Vector((-0.066, 0.236, 1.728)), 0.042)]


def cap_material():
    def colour_of(t, socket, bsdf):
        a = t.nodes.new("ShaderNodeAttribute")
        a.attribute_name = "capcol"
        t.links.new(a.outputs["Color"], socket)
        t.links.new(a.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = 0.95

    def mask_of(t, socket, bsdf):
        socket.default_value = (0.55, 0.0, 0.0, 1.0)
    return flat("oa_cap", colour_of, mask_of)


def paint_from_picture(objects, image):
    """Corner colours from the baked picture: what shows when the pictures are missing or switched off."""
    px = to_linear(pixels_of(image)[:, :, :3])
    h, w = px.shape[:2]
    for o in objects:
        mesh = o.data
        total = np.zeros((len(mesh.vertices), 3))
        count = np.zeros(len(mesh.vertices))
        uv = mesh.uv_layers[oa.TEX].data
        for l in mesh.loops:
            u = uv[l.index].uv
            total[l.vertex_index] += px[min(h - 1, max(0, int(u[1] * h))), min(w - 1, max(0, int(u[0] * w)))]
            count[l.vertex_index] += 1
        old = mesh.color_attributes.get("Col")
        if old is not None:
            mesh.color_attributes.remove(old)
        layer = mesh.color_attributes.new("Col", 'FLOAT_COLOR', 'POINT')
        for i in range(len(mesh.vertices)):
            c = total[i] / max(1.0, count[i])
            layer.data[i].color = (c[0], c[1], c[2], 1.0)


def rest_matrix(name, j):
    """Takes a piece from where Tripo stood it to the game's rest pose: its joint on the skeleton's joint, its
    bone along the skeleton's bone, stretched along the bone to the skeleton's length."""
    a, b = Vector(j[BONES[name][0]]), Vector(j[BONES[name][1]])
    sx = -1 if name.startswith("Left") else 1
    if name == "Head":
        return Matrix.Translation(Vector((0, 0, be.NECK_Z)) - a)
    if name == "Torso":
        z = (be.NECK_Z - be.HIP_Z) / (b.z - a.z)
        wide = be.SHOULDER_X / abs(j["R_shoulder"][0])
        # With its pack the torso is a little taller than a piece may be (0.75 m), so it is pressed 4.5% about its middle.
        mid = (be.HIP_Z + be.NECK_Z) / 2
        press = Matrix.Translation((0, 0, mid)) @ Matrix.Diagonal((1, 1, 0.955, 1)) @ Matrix.Translation((0, 0, -mid))
        return press @ Matrix.Translation((0, 0, be.HIP_Z)) @ Matrix.Diagonal((wide, (wide + z) / 2, z, 1)) @ Matrix.Translation((-a.x, -a.y, -a.z))
    start = {"UpperArm": Vector((sx * be.SHOULDER_X, 0, be.SHOULDER_Z)),
             "Forearm": Vector((sx * (be.SHOULDER_X + be.UPPER_ARM), 0, be.SHOULDER_Z)),
             "Hand": Vector((sx * (be.SHOULDER_X + be.UPPER_ARM + be.FOREARM), 0, be.SHOULDER_Z)),
             "Thigh": Vector((sx * be.HIP_X, 0, be.HIP_Z)), "Shin": Vector((sx * be.HIP_X, 0, be.KNEE_Z)),
             "Foot": Vector((sx * be.HIP_X, 0, be.ANKLE_Z))}
    kind = name.replace("Left", "").replace("Right", "")
    length = {"UpperArm": be.UPPER_ARM, "Forearm": be.FOREARM, "Hand": None, "Thigh": be.HIP_Z - be.KNEE_Z,
              "Shin": be.KNEE_Z - be.ANKLE_Z, "Foot": None}[kind]
    if kind == "Foot":
        squash = max(0.8, be.ANKLE_Z / a.z)
        return Matrix.Translation(start[kind] + Vector((sx * 0.018, 0, 0))) @ Matrix.Diagonal((0.93, 0.93, squash, 1)) @ Matrix.Translation(-a)
    d = (b - a).normalized()
    thin = Matrix.Identity(4)
    if kind in ("Thigh", "Shin"):
        # The suit's legs are wider than the hitboxes allow and stood apart; here they hang straight down.
        thin = Matrix.Translation((sx * 0.018, 0, 0)) @ Matrix.Diagonal((0.9, 0.9, 1, 1))
    to = Vector((sx, 0, 0)) if kind in ("UpperArm", "Forearm", "Hand") else Vector((0, 0, -1))
    turn = d.rotation_difference(to).to_matrix().to_4x4()
    k = 1.0 if length is None else length / (b - a).length
    along = Matrix.Identity(3) + (k - 1.0) * Matrix((d * d.x, d * d.y, d * d.z))      # stretch along the bone only
    print("OA rest %-14s turned %5.1f degrees, stretched %.2f" % (name, math.degrees(d.angle(to)), k))
    return Matrix.Translation(start[kind]) @ thin @ turn @ along.to_4x4() @ Matrix.Translation(-a)


def bake(low, high, folder):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.use_denoising = False
    target = mats._merged(low, "oa_bake_low")
    outside = mats._merged(low, "oa_bake_cage")
    for v in outside.data.vertices:
        v.co = v.co + v.normal * CAGE
    outside.hide_render = True
    for o in low:
        o.hide_render = True
    mat = bpy.data.materials.new("oa_bake_target")
    mat.use_nodes = True
    node = mat.node_tree.nodes.new("ShaderNodeTexImage")
    mat.node_tree.nodes.active = node
    target.data.materials.append(mat)
    images = {}
    for what, kind, colour in (("Normal", 'NORMAL', False), ("Color", 'EMIT', True), ("Mask", 'EMIT', False)):
        image = bpy.data.images.new("%s_%s" % (NAME, what), SIZE, SIZE, alpha=False, is_data=not colour)
        node.image = image
        mats.show({"Normal": "surface", "Color": "colour", "Mask": "mask"}[what])
        scene.cycles.samples = 4 if what == "Normal" else (4 if DRAFT else 16)
        bpy.ops.object.select_all(action='DESELECT')
        for o in high:
            o.select_set(True)
        target.select_set(True)
        bpy.context.view_layer.objects.active = target
        bpy.ops.object.bake(type=kind, use_selected_to_active=True, use_cage=True, cage_object=outside.name,
                            max_ray_distance=CAGE * 3, margin=8, margin_type='EXTEND', normal_space='TANGENT', use_clear=True)
        images[what] = image
        print("OA baked", what)
    mats.show("surface")
    for o in (target, outside):
        mesh = o.data
        bpy.data.objects.remove(o)
        bpy.data.meshes.remove(mesh)
    for o in low:
        o.hide_render = False
    paint_from_picture(low, images["Color"])
    oa.save_texture_set(NAME, images["Color"], images["Normal"], images["Mask"], folder)


def write(parts, stump):
    listed, sockets = [], []
    for name in be.ORDER:
        origin = Vector(be.ORIGINS[name])
        for o in parts[name]:
            o.data.transform(Matrix.Translation(-origin))
        listed.append((name, parts[name]))
        sockets.append(oa.socket(name, origin))
    if stump is not None:
        listed.append(("Stump", stump))
    oa.export(os.path.join(OUT, NAME + ("" if stump is not None else oa.FAR) + ".bytes"), listed, sockets, textures=NAME)
    for name in be.ORDER:
        origin = Vector(be.ORIGINS[name])
        for o in parts[name]:
            o.data.transform(Matrix.Translation(origin))
            o.data.update()


def shots(tag, objects, look, views):
    samples = 24 if DRAFT else 96
    v = {"front": ((1.5, 3.6, 1.45), (0, 0, 0.98), (900, 1300)), "back": ((-1.5, -3.6, 1.45), (0, 0, 0.98), (900, 1300)),
         "chest": ((0.55, 1.25, 1.55), (0.02, 0, 1.38), (1200, 1000)), "leg": ((0.75, 1.05, 0.62), (0.16, 0, 0.42), (1000, 1000)),
         "wide": ((0.6, 5.2, 1.3), (0, 0, 1.0), (1500, 1300)), "far": ((0.4, 9.0, 1.5), (0, 0, 1.0), (900, 1300))}
    for name in views:
        eye, at, size = v[name]
        mats.render(os.path.join(PICS, "%s_%s.png" % (tag, name)), objects, look, eye, at, lens=60, size=size, samples=samples, floor=0.0)


def main():
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(PICS, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath="/tmp/claude-0/tripo/work/cut.blend")
    low, high = bpy.data.objects["low"], bpy.data.objects["high"]
    j = {k: tuple(v) for k, v in low["oa_joints"].items()}
    for o in list(bpy.data.objects):
        if o.type in ('CAMERA', 'LIGHT'):
            bpy.data.objects.remove(o)
    parts, caps = pieces_from(low)
    bpy.data.objects.remove(low)
    high.hide_render = False
    high.data.materials[0] = tripo_material(high.data.materials[0], j)
    caps.data.materials.append(cap_material())
    stump = be.stump()
    flesh = mats.painted_flesh("oa_flesh")
    move = Matrix.Translation(STUMP_AT) @ Matrix.Scale(STUMP_SCALE, 4)
    wound = []
    for o in stump:
        o.data.transform(move)
        c = o.copy()
        c.data = o.data.copy()
        bpy.context.scene.collection.objects.link(c)
        c.data.materials.clear()
        c.data.materials.append(flesh)
        wound.append(c)
    body = [o for name in be.ORDER for o in parts[name]]
    mats.unwrap(body + stump, angle=86.0, margin=0.004)
    nudged = 0
    for o in body + stump:      # a triangle with no area on the pictures has no picture direction: give it a sliver
        uv = o.data.uv_layers[oa.TEX].data
        for p in o.data.polygons:
            a_, b_, c_ = (uv[i].uv for i in p.loop_indices)
            if abs((b_ - a_).cross(c_ - a_)) < 2e-9:
                uv[p.loop_start + 1].uv = (min(1.0, b_.x + 0.0006), b_.y)
                uv[p.loop_start + 2].uv = (c_.x, min(1.0, c_.y + 0.0006))
                nudged += 1
    print("OA triangles given a sliver of picture:", nudged)
    print("OA laid out: %d triangles" % sum(oa._triangle_count(o) for o in body))
    textures = os.path.join(OUT, "Textures")
    stump_paint = [[tuple(d.color) for d in o.data.color_attributes["Col"].data] for o in stump]
    bake(body + stump, [high, caps] + wound, textures)
    for o, paint in zip(stump, stump_paint):       # the wound keeps the paint it was made with
        layer = o.data.color_attributes["Col"]
        if len(layer.data) == len(paint):
            for d, c in zip(layer.data, paint):
                d.color = c
    for o in [high, caps] + wound:
        o.hide_render = True
    back = move.inverted()
    for o in stump:
        o.data.transform(back)
        o.data.update()
        o.hide_render = True
    look = mats.textured_material(NAME, textures)
    shots("pose", body, look, ("front", "chest", "leg") if DRAFT else ("front", "back", "chest", "leg"))

    for name in be.ORDER:
        m = rest_matrix(name, j)
        for o in parts[name]:
            o.data.transform(m)
            o.data.update()
    for name in be.ORDER:       # every limb piece must reach at least 3.5 cm behind its joint (its ball)
        if name in ("Head", "Torso"):
            continue
        o = parts[name][0]
        origin = Vector(be.ORIGINS[name])
        d = Vector((-1 if name.startswith("Left") else 1, 0, 0)) if ("Arm" in name or "Hand" in name) else Vector((0, 0, -1))
        behind = max(-(v.co - origin).dot(d) for v in o.data.vertices)
        if behind < 0.036:
            o.data.transform(Matrix.Translation(-d * min(0.03, 0.036 - behind)))
            o.data.update()
            print("OA %s moved %.3f back onto its joint" % (name, min(0.03, 0.036 - behind)))
    for name in be.ORDER:
        o = parts[name][0]
        origin = Vector(be.ORIGINS[name])
        lo = Vector((min(v.co.x for v in o.data.vertices), min(v.co.y for v in o.data.vertices), min(v.co.z for v in o.data.vertices))) - origin
        hi = Vector((max(v.co.x for v in o.data.vertices), max(v.co.y for v in o.data.vertices), max(v.co.z for v in o.data.vertices))) - origin
        print("OA box %-14s x %.3f %.3f  y %.3f %.3f  z %.3f %.3f" % (name, lo.x, hi.x, lo.y, hi.y, lo.z, hi.z))
    write(parts, stump)
    shots("rest", body, look, ("wide",))
    # Pulled apart: every piece is its own closed thing.
    apart = {"Head": (0, 0, 0.16), "LeftUpperArm": (-0.10, 0, 0.05), "LeftForearm": (-0.22, 0, 0.05), "LeftHand": (-0.34, 0, 0.05),
             "RightUpperArm": (0.10, 0, 0.05), "RightForearm": (0.22, 0, 0.05), "RightHand": (0.34, 0, 0.05),
             "LeftThigh": (-0.10, 0, -0.10), "LeftShin": (-0.14, 0, -0.24), "LeftFoot": (-0.18, 0, -0.36),
             "RightThigh": (0.10, 0, -0.10), "RightShin": (0.14, 0, -0.24), "RightFoot": (0.18, 0, -0.36)}
    lift = Matrix.Translation((0, 0, 0.40))
    for name in be.ORDER:
        m = lift @ Matrix.Translation(apart.get(name, (0, 0, 0)))
        for o in parts[name]:
            o.data.transform(m)
    shots("apart", body, look, ("wide",))
    for name in be.ORDER:
        m = (lift @ Matrix.Translation(apart.get(name, (0, 0, 0)))).inverted()
        for o in parts[name]:
            o.data.transform(m)
            o.data.update()
    far = be.far_parts(parts, NAME, keep=(), leave_out=(), thin_too=())
    write(far, None)
    faro = [o for name in be.ORDER for o in far[name]]
    holes = oa.inside_out_where(faro)
    print("OA far copy, seen from inside:", holes if holes else "nothing")
    for o in body:
        o.hide_render = True
    for o in faro:
        o.hide_render = False
    shots("farcopy", faro, look, ("wide",))
    print("OA done")


main()
