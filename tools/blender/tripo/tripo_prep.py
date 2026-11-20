"""Imports the Tripo soldier, makes a 300k working copy (keeps its own UVs and pictures) and saves a .blend."""
import bpy, sys
src, out = sys.argv[sys.argv.index("--")+1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
o = [x for x in bpy.data.objects if x.type == 'MESH'][0]
bpy.context.view_layer.objects.active = o
o.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
import bmesh
bm = bmesh.new(); bm.from_mesh(o.data)
before = len(bm.verts)
bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.000005)
print("OA welded", before, "->", len(bm.verts))
bm.to_mesh(o.data); bm.free()
m = o.modifiers.new("d", 'DECIMATE')
m.ratio = 300000.0 / len(o.data.polygons)
bpy.ops.object.modifier_apply(modifier="d")
o.name = "high"
print("OA high", len(o.data.polygons))
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=out)
