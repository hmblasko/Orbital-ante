"""Pictures of the lander's cabin from the game's own shapes and baked light.

    blender -b -P tools/blender/preview_cabin.py -- <dump folder from tools/bake --dump> <output folder> [view names]

The pictures show the baked colours exactly as stored (no extra lighting), so they are close to what the game
draws for the cabin, without its bloom, glass, dash screen or the view out of the windows. Not a Unity screenshot.
"""
import bpy, sys, os, struct, math
from mathutils import Vector

VIEWS = {  # name: (eye, target) in ship space (x right, y up, z nose)
    "from_hatch": ((0.0, 1.62, -1.15), (0.0, 1.0, 2.0)),
    "seated": ((0.0, 1.18, 1.28), (0.0, 0.98, 2.4)),
    "to_lockers": ((0.0, 1.62, 1.0), (0.0, 1.15, -2.0)),
    "left_wall": ((1.0, 1.62, -0.2), (-2.4, 1.25, -0.5)),
    "right_wall": ((-1.0, 1.62, -0.2), (2.4, 1.25, -0.5)),
    "ceiling": ((0.3, 1.5, -1.0), (-0.3, 2.6, 0.6)),
    "desk_side": ((-1.2, 1.5, 0.9), (1.2, 0.75, 2.1)),
}


def G(p):
    return Vector((p[0], p[2], p[1]))


def load(path):
    objs = []
    with open(path, "rb") as f:
        (count,) = struct.unpack("<i", f.read(4))
        for m in range(count):
            nv, nt = struct.unpack("<ii", f.read(8))
            pos = struct.unpack("<%df" % (nv * 3), f.read(nv * 12))
            col = struct.unpack("<%df" % (nv * 3), f.read(nv * 12))
            tri = struct.unpack("<%di" % (nt * 3), f.read(nt * 12))
            mesh = bpy.data.meshes.new("piece%d" % m)
            verts = [(pos[i * 3], pos[i * 3 + 2], pos[i * 3 + 1]) for i in range(nv)]
            faces = [(tri[i * 3], tri[i * 3 + 2], tri[i * 3 + 1]) for i in range(nt)]
            mesh.from_pydata(verts, [], faces)
            attr = mesh.color_attributes.new("Col", 'FLOAT_COLOR', 'POINT')
            for i in range(nv):
                attr.data[i].color = (col[i * 3], col[i * 3 + 1], col[i * 3 + 2], 1.0)
            for p in mesh.polygons:
                p.use_smooth = True
            obj = bpy.data.objects.new(mesh.name, mesh)
            bpy.context.scene.collection.objects.link(obj)
            objs.append(obj)
    return objs


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    dump, out = args[0], args[1]
    wanted = args[2:] or list(VIEWS)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    objs = load(os.path.join(dump, "cabin.bin"))
    mat = bpy.data.materials.new("baked")
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    nodes.clear()
    attr = nodes.new("ShaderNodeVertexColor"); attr.layer_name = "Col"
    emit = nodes.new("ShaderNodeEmission")
    # The game does not draw the backs of surfaces: show them black, so a hole or an inside-out piece is plain.
    geo = nodes.new("ShaderNodeNewGeometry")
    black = nodes.new("ShaderNodeEmission"); black.inputs[0].default_value = (0, 0, 0, 1)
    mix = nodes.new("ShaderNodeMixShader")
    outn = nodes.new("ShaderNodeOutputMaterial")
    links.new(attr.outputs["Color"], emit.inputs["Color"])
    links.new(geo.outputs["Backfacing"], mix.inputs[0])
    links.new(emit.outputs[0], mix.inputs[1]); links.new(black.outputs[0], mix.inputs[2])
    links.new(mix.outputs[0], outn.inputs["Surface"])
    for o in objs:
        o.data.materials.append(mat)
    world = bpy.data.worlds.new("space"); world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.004, 0.006, 0.012, 1)
    scene.world = world
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 16
    scene.cycles.device = 'CPU'
    scene.cycles.use_denoising = False
    scene.cycles.max_bounces = 0
    scene.view_settings.view_transform = 'Standard'
    scene.render.resolution_x, scene.render.resolution_y = 1280, 800
    cam_data = bpy.data.cameras.new("cam"); cam_data.lens = 15; cam_data.clip_start = 0.05
    cam = bpy.data.objects.new("cam", cam_data); scene.collection.objects.link(cam); scene.camera = cam
    os.makedirs(out, exist_ok=True)
    for name in wanted:
        eye, target = VIEWS[name]
        cam.location = G(eye)
        cam.rotation_euler = (G(target) - G(eye)).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(out, "cabin_%s.png" % name)
        bpy.ops.render.render(write_still=True)


main()
