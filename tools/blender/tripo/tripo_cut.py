"""Stage 1 of bringing a Tripo body in: scale it, make the low model, cut it into the 14 pieces. Saves cut.blend
and a picture with every piece in its own colour."""
import bpy, bmesh, sys, math, random
from mathutils import Vector, Matrix
sys.path.insert(0, "/home/claude/orbital-ante/tools/blender")
import oa_blender as oa
import build_enemy as be

S = 1.85            # model units to metres
LOW_TRIANGLES = 13800
ARM_THICK = 0.095

# Joints in the model's own units, after it has been turned to face +Y. From the orthographic pictures; the
# limb joints are then moved to the middle of the limb there (refine()).
J = {
    "neck": (0.0, 0.015, 0.812), "head": (0.0, 0.0, 0.900),
    "hips": (0.0, 0.0, 0.470),
    "L_shoulder": (-0.150, 0.005, 0.745), "L_elbow": (-0.220, 0.020, 0.600), "L_wrist": (-0.243, 0.070, 0.515), "L_tip": (-0.250, 0.085, 0.420),
    "R_shoulder": (0.150, 0.005, 0.745), "R_elbow": (0.215, 0.020, 0.600), "R_wrist": (0.238, 0.070, 0.515), "R_tip": (0.245, 0.085, 0.420),
    "L_hip": (-0.075, 0.0, 0.470), "L_knee": (-0.115, 0.045, 0.262), "L_ankle": (-0.147, 0.020, 0.062), "L_sole": (-0.147, 0.020, 0.0),
    "R_hip": (0.075, 0.0, 0.470), "R_knee": (0.096, 0.045, 0.262), "R_ankle": (0.133, 0.020, 0.062), "R_sole": (0.133, 0.020, 0.0),
}
J = {k: Vector(v) * S for k, v in J.items()}

BONES = {  # piece: (from joint, to joint)
    "Head": ("neck", "head"), "Torso": ("hips", "neck"),
    "LeftUpperArm": ("L_shoulder", "L_elbow"), "LeftForearm": ("L_elbow", "L_wrist"), "LeftHand": ("L_wrist", "L_tip"),
    "RightUpperArm": ("R_shoulder", "R_elbow"), "RightForearm": ("R_elbow", "R_wrist"), "RightHand": ("R_wrist", "R_tip"),
    "LeftThigh": ("L_hip", "L_knee"), "LeftShin": ("L_knee", "L_ankle"), "LeftFoot": ("L_ankle", "L_sole"),
    "RightThigh": ("R_hip", "R_knee"), "RightShin": ("R_knee", "R_ankle"), "RightFoot": ("R_ankle", "R_sole"),
}


def seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
    return (p - (a + ab * t)).length


def refine(bm, name, along, radius):
    """Moves a limb joint to the middle of the limb's cross-section there."""
    c, n = J[name], along.normalized()
    pts = [v.co for v in bm.verts if abs((v.co - c).dot(n)) < 0.012 and (v.co - c).length < radius]
    if len(pts) > 12:
        lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
        hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
        mid = (lo + hi) / 2
        mid = mid - n * (mid - c).dot(n)
        print("OA refine %s moved %.3f" % (name, (mid - c).length))
        J[name] = mid


def face_piece(c, planes):
    """Which piece a face of the body's main lump belongs to: the planes decide."""
    def beyond(name):
        p, n = planes[name]
        return (c - p).dot(n) > 0.0
    for side, word, sx in (("L", "Left", -1), ("R", "Right", 1)):
        if beyond(side + "_shoulder") and beyond(side + "_pit") and c.z > J[side + "_elbow"].z - 0.05:
            return word + "UpperArm"
        if beyond(side + "_hip") and c.x * sx > 0 and c.z < planes[side + "_hip"][0].z + 0.10:
            return word + "Thigh"
    neck = planes["neck"][0]
    if c.z > neck.z and math.hypot(c.x - neck.x, c.y - neck.y) < 0.23 and c.y > -0.17:
        return "Head"
    return "Torso"


def main():
    bpy.ops.wm.open_mainfile(filepath="/tmp/claude-0/tripo/work/soldier.blend")
    high = bpy.data.objects["high"]
    high.data.transform(Matrix.Rotation(math.pi, 4, 'Z') @ Matrix.Scale(S, 4))
    high.data.update()
    low = high.copy()
    low.data = high.data.copy()
    low.name = "low"
    bpy.context.scene.collection.objects.link(low)
    bpy.context.view_layer.objects.active = low
    mod = low.modifiers.new("d", 'DECIMATE')
    mod.ratio = LOW_TRIANGLES / len(low.data.polygons)
    bpy.ops.object.select_all(action='DESELECT')
    low.select_set(True)
    bpy.ops.object.modifier_apply(modifier="d")
    while low.data.uv_layers:
        low.data.uv_layers.remove(low.data.uv_layers[0])
    low.data.materials.clear()

    bm = bmesh.new()
    bm.from_mesh(low.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.0004)
    # Thinning leaves the surface lumpy; the lumps are eased (the bake puts the real detail back as shading).
    for _ in range(2):
        bmesh.ops.smooth_vert(bm, verts=bm.verts[:], factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    for side in "LR":
        refine(bm, side + "_elbow", J[side + "_wrist"] - J[side + "_shoulder"], 0.16)
        refine(bm, side + "_wrist", J[side + "_wrist"] - J[side + "_elbow"], 0.13)
        refine(bm, side + "_knee", Vector((0, 0, -1)), 0.19)
        refine(bm, side + "_ankle", Vector((0, 0, -1)), 0.16)
        # The elbow is under the dark band, a little above where the sleeve looks to bend.
        J[side + "_elbow"] = J[side + "_elbow"] + (J[side + "_shoulder"] - J[side + "_elbow"]).normalized() * 0.04
        J[side + "_sole"] = Vector((J[side + "_ankle"].x, J[side + "_ankle"].y, 0.0))
        tip = J[side + "_wrist"] + (J[side + "_wrist"] - J[side + "_elbow"]).normalized() * 0.19
        J[side + "_tip"] = tip

    # The cuts: (joint, the way the plane faces, how far round the joint it reaches).
    cuts = [("neck", Vector((0, 0, 1)), 0.34)]
    for side, sx in (("L", -1), ("R", 1)):
        arm = (J[side + "_elbow"] - J[side + "_shoulder"]).normalized()
        cuts.append((side + "_shoulder", (arm * 0.45 + Vector((sx, 0, 0)) * 0.55).normalized(), 0.26))
        cuts.append((side + "_elbow", (J[side + "_wrist"] - J[side + "_shoulder"]).normalized(), 0.17))
        cuts.append((side + "_wrist", (J[side + "_wrist"] - J[side + "_elbow"]).normalized(), 0.14))
        cuts.append((side + "_hip", Vector((sx * 0.45, 0, -1)).normalized(), 0.38))
        cuts.append((side + "_knee", (J[side + "_ankle"] - J[side + "_hip"]).normalized(), 0.21))
        cuts.append((side + "_ankle", Vector((0, 0, -1)), 0.19))
    # The arm hangs close to the body, so it is also cut free along its inner side: a plane lying along the arm,
    # one sleeve's thickness in from the bone, facing outward.
    planes = {}
    for side, sx in (("L", -1), ("R", 1)):
        arm = (J[side + "_elbow"] - J[side + "_shoulder"]).normalized()
        out_ = Vector((sx, 0, 0)) - arm * arm.dot(Vector((sx, 0, 0)))
        out_.normalize()
        J[side + "_pit"] = J[side + "_shoulder"] - out_ * ARM_THICK + arm * 0.14
        cuts.append((side + "_pit", out_, 0.30))
    for name, normal, reach in cuts:
        c = J[name]
        if name.endswith("_hip"):            # the leg comes off below the hip joint, along the crease of the groin
            c = c + Vector((0, 0, -0.035))
        planes[name] = (c.copy(), normal.copy())
        faces = [f for f in bm.faces if (f.calc_center_median() - c).length < reach]
        geom = list({v for f in faces for v in f.verts}) + list({e for f in faces for e in f.edges}) + faces
        out = bmesh.ops.bisect_plane(bm, geom=geom, dist=0.00005, plane_co=c, plane_no=normal, clear_inner=False, clear_outer=False)
        edges = [g for g in out["geom_cut"] if isinstance(g, bmesh.types.BMEdge)]
        bmesh.ops.split_edges(bm, edges=edges)
        print("OA cut %s: %d edges" % (name, len(edges)))

    # Whatever hangs together after the cuts is one lump; each lump belongs to the bone it lies along.
    layer = bm.faces.layers.int.new("piece")
    bm.faces.ensure_lookup_table()
    bm.faces.index_update()
    seen, lumps = set(), []
    for f in bm.faces:
        if f.index in seen:
            continue
        stack, lump = [f], []
        seen.add(f.index)
        while stack:
            g = stack.pop()
            lump.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen:
                        seen.add(h.index)
                        stack.append(h)
        lumps.append(lump)
    names = list(BONES)
    report = {}
    for lump in lumps:
        area = sum(f.calc_area() for f in lump)
        centre = sum((f.calc_center_median() * f.calc_area() for f in lump), Vector()) / max(area, 1e-9)
        best = min(names, key=lambda n: seg_dist(centre, J[BONES[n][0]], J[BONES[n][1]]) - (0.12 if n == "Torso" else 0.0))
        fused = best in ("Head", "Torso", "LeftUpperArm", "RightUpperArm", "LeftThigh", "RightThigh")
        for f in lump:
            f[layer] = names.index(face_piece(f.calc_center_median(), planes) if fused else best)
        report.setdefault(best, []).append((round(area, 3), len(lump)))
    for n in names:
        got = sorted(report.get(n, []), reverse=True)
        print("OA piece %-14s lumps %3d largest %s" % (n, len(got), got[:3]))
    count = {}
    for f in bm.faces:
        count[names[f[layer]]] = count.get(names[f[layer]], 0) + 1
    print("OA faces per piece", count)
    bm.to_mesh(low.data)
    bm.free()
    low["oa_joints"] = {k: tuple(v) for k, v in J.items()}
    bpy.ops.wm.save_as_mainfile(filepath="/tmp/claude-0/tripo/work/cut.blend")

    # A picture: every piece its own colour.
    random.seed(11)
    att = low.data.attributes["piece"]
    for i, n in enumerate(names):
        m = bpy.data.materials.new(n)
        m.use_nodes = True
        m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (random.random(), random.random(), random.random(), 1)
        low.data.materials.append(m)
    for p in low.data.polygons:
        p.material_index = att.data[p.index].value
    high.hide_render = True
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 16; sc.cycles.use_denoising = False
    sc.render.resolution_x, sc.render.resolution_y = 800, 1000
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.8, 0.8, 0.8, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 1.4
    cam = bpy.data.cameras.new("c"); cam.lens = 60
    co = bpy.data.objects.new("c", cam); sc.collection.objects.link(co); sc.camera = co
    for tag, eye in (("front", (1.2, 4.6, 1.2)), ("back", (-1.2, -4.6, 1.2))):
        co.location = eye
        co.rotation_euler = (Vector((0, 0, 0.92)) - Vector(eye)).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = "/tmp/claude-0/tripo/pics/cut_%s.png" % tag
        bpy.ops.render.render(write_still=True)


main()
