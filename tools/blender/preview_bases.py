"""Offline pictures of the base models (Blender's renderer, not the game's shader).

Called by build_bases.py with --preview, after the base has been finished (broken up and its rooms baked):
  <Base>_air.png     the whole base from the air, on a stand-in for the round foundation the game builds
  <Base>_walk.png    from a walker's eye
  <Base>_inside.png  inside the first room: its baked light shown as it is drawn (no sunlight)
  <Base>_broken.png  every breakable thing fallen: its pieces tumbled about, the ruin left standing
A 1.8 m figure stands in the middle for scale. Lamps and windows are shown lit.
"""
import bpy, math, os, random
from mathutils import Vector, Matrix, Euler
import oa_blender as oa
import oa_nature as nat


def _only(objects):
    keep = set(objects)
    for o in bpy.context.scene.objects:
        if o.type == 'MESH':
            o.hide_render = o not in keep


def _stage(radius):
    ground = oa.lathe("ground", (0, 0, -1.2), (0, 0, 1), [(0.0, radius + 4.0), (1.0, radius + 1.2), (1.2, radius)], (0.60, 0.58, 0.54), segments=48)
    oa.densify(ground, 2.5)
    oa.paint(ground, wear=0.3, dirt=0.6, mottle=0.8, seed=3, grain=0.5)
    land = oa.box("land", (0, 0, -1.25), (radius * 14, radius * 14, 0.1), (0.46, 0.40, 0.33), bevel=0)
    nat.flat_paint(land, (0.46, 0.40, 0.33))
    figure = [oa.cylinder("figure", (0.8, 0, 0), (0.8, 0, 1.5), 0.2, (0.92, 0.9, 0.86), radius_end=0.16, segments=10),
              oa.sphere("figure_head", (0.8, 0, 1.66), 0.15, (0.95, 0.6, 0.2), segments=10)]
    for o in figure:
        nat.flat_paint(o, o["oa_colour"][:3])
    return [ground, land], figure


def _whole(b):
    drawn, lit, inside, doors = [], [], [], []
    for s in b.structs + [b.ground]:
        drawn += s.groups["Shell"] + s.groups["Frame"] + s.groups["Detail"]
        lit += s.groups["WindowGlow"] + s.groups["LampGlow"]
        inside += s.inside
        if s.door is not None:
            doors.append(s.door)
    return drawn, lit, inside, doors


def _baked_material():
    """The room as the game draws it: its own colours, no sun."""
    m = bpy.data.materials.get("oa_preview_baked")
    if m:
        return m
    m = bpy.data.materials.new("oa_preview_baked")
    m.use_nodes = True
    nodes, links = m.node_tree.nodes, m.node_tree.links
    attr = nodes.new("ShaderNodeVertexColor")
    attr.layer_name = "Col"
    emit = nodes.new("ShaderNodeEmission")
    emit.inputs[1].default_value = 1.0
    links.new(attr.outputs["Color"], emit.inputs[0])
    links.new(emit.outputs[0], nodes["Material Output"].inputs["Surface"])
    return m


def _render(path, objects, eye, target, lens, sun, world, sun_energy, sky, glow, baked=(), size=(1500, 860), samples=48):
    _only(objects)
    # (With rooms to show, this first pass only sets up the materials: the real picture is the second.)
    oa.render_preview(path, objects, eye, target, lens=lens, size=(48, 30) if baked else size, sun=sun, world=world,
                      samples=1 if baked else samples, glow=glow, sun_energy=sun_energy, sky=sky)
    if baked:
        scene = bpy.context.scene
        scene.render.resolution_x, scene.render.resolution_y = size
        scene.cycles.samples = samples
        m = _baked_material()
        for o in baked:
            o.data.materials.clear()
            o.data.materials.append(m)
        scene = bpy.context.scene
        scene.render.filepath = path
        # the picture again, with the rooms drawn as the game draws them
        light = bpy.data.lights.new("sun", 'SUN')
        light.energy = sun_energy
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
        bpy.ops.render.render(write_still=True)
        bpy.data.objects.remove(lo)
        bpy.data.objects.remove(co)


def render(folder, b, radius, dusk=True):
    os.makedirs(folder, exist_ok=True)
    stage, figure = _stage(radius)
    drawn, lit, inside, doors = _whole(b)
    everything = drawn + lit + inside + doors + stage + figure
    views = [("air", (radius * 1.25, -radius * 2.0, radius * 1.25), (0, 0, radius * 0.12), 35, (52, 0, 35), (0.42, 0.56, 0.8)),
             ("walk", (radius * 0.3, -radius * 1.55, 1.9), (0, 0, radius * 0.2), 24, (62, 0, -50), (0.30, 0.36, 0.60))]
    for tag, eye, target, lens, sun, world in views:
        _render(os.path.join(folder, "%s_%s.png" % (b.name, tag)), everything, eye, target, lens, sun, world,
                3.4 if tag == "air" else 2.2, 0.7 if tag == "air" else 0.5, lit, baked=inside)

    # Inside the first room: stand in its doorway's vestibule and look at the air station.
    for s in b.structs:
        if s.inside and "Air" in s.points and "DoorShut" in s.points:
            air = Vector(s.points["Air"])
            door = Vector(s.points["DoorShut"])
            mid = s.points.get("Mid", Vector())
            inward = (Vector((mid.x, mid.y, door.z)) - door)
            inward.z = 0
            inward.normalize()
            eye = door + inward * 0.9 + Vector((0, 0, 0.62))
            target = Vector((air.x, air.y, air.z + 0.1)).lerp(Vector((mid.x, mid.y, eye.z - 0.3)), 0.35)
            room = drawn + lit + inside + stage
            _render(os.path.join(folder, "%s_inside.png" % b.name), room, eye, target, 16, (40, 0, 30), (0.4, 0.5, 0.7), 2.0, 0.5, lit,
                    baked=inside, size=(1280, 800), samples=64)
            break

    # Everything that can fall, fallen: pieces tumbled about where they came down, ruins standing.
    broken = getattr(b, "broken", {})
    if broken:
        rng = random.Random(5)
        fallen = []
        for i, (pieces, ruin, _) in broken.items():
            mid = Vector(b.structs[i].points.get("Mid", Vector()))
            for p in pieces:
                c = sum((v.co for v in p.data.vertices), Vector()) / max(1, len(p.data.vertices))
                out = Vector((c.x - mid.x, c.y - mid.y, 0))
                out = out.normalized() if out.length > 1e-3 else Vector((1, 0, 0))
                spin = Euler((rng.uniform(-0.7, 0.7), rng.uniform(-0.7, 0.7), rng.uniform(-1, 1))).to_matrix().to_4x4()
                m = Matrix.Translation(Vector((c.x, c.y, 0)) + out * (0.4 + c.z * 0.3)) @ spin @ Matrix.Translation(-c)
                p.data.transform(m)
                lowest = min(v.co.z for v in p.data.vertices)
                p.data.transform(Matrix.Translation((0, 0, 0.02 - lowest)))
                fallen.append(p)
            fallen += ruin
        keep = [o for s in [b.ground] for o in s.groups["Shell"] + s.groups["Frame"] + s.groups["Detail"]]
        keep += [b.structs[i] for i in range(len(b.structs)) if i not in broken] and []
        for i, s in enumerate(b.structs):
            if i not in broken:
                keep += s.groups["Shell"] + s.groups["Frame"] + s.groups["Detail"] + s.groups["WindowGlow"] + s.groups["LampGlow"]
        eye, target, lens, sun, world = views[0][1], views[0][2], views[0][3], views[0][4], views[0][5]
        _render(os.path.join(folder, "%s_broken.png" % b.name), fallen + keep + stage + figure, eye, target, lens, sun, world, 3.4, 0.7, [])
    for o in stage + figure:
        bpy.data.objects.remove(o)
